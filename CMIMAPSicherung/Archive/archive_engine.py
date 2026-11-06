"""CM IMAP Sicherung: conservative age-based EML archive (no external packages).

Only the explicit `cleanup` operation writes to the source mailbox. It requires
complete local verification, account binding, UIDVALIDITY, UIDPLUS and another
byte-for-byte server hash check before each UID-specific deletion. No EXPUNGE,
CLOSE, MOVE, or sequence-number deletion is ever used.
"""
from __future__ import annotations

import base64
import contextlib
import datetime as dt
from email import policy
from email.parser import BytesHeaderParser
from email.utils import parsedate_to_datetime
import hashlib
import imaplib
import json
import os
from pathlib import Path
import re
import shutil
import ssl
import sys
import uuid

CHUNK = 1024 * 1024
RESERVE = 512 * 1024 * 1024
FORMAT = 'cm-email-archive-v1'
MONTHS = 'Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec'.split()


class ArchiveError(Exception):
    """An operation cannot proceed safely."""


class Cancelled(ArchiveError):
    pass


def emit(kind, **fields):
    print(json.dumps(dict(event=kind, **fields), ensure_ascii=True), flush=True)


def check_cancel(path=None):
    if path and Path(path).exists():
        raise Cancelled('Vorgang abgebrochen. Vorhandene Archivdateien bleiben erhalten.')


def account_key(account):
    identity = [account['host'].lower().rstrip('.'), int(account['port']), account['user']]
    return hashlib.sha256(json.dumps(identity, ensure_ascii=True).encode()).hexdigest()


def quoted(value):
    if any(c in value for c in '\r\n\x00'):
        raise ArchiveError('Ungueltiger IMAP-Ordnername.')
    return '"' + value.replace('\\', '\\\\').replace('"', '\\"') + '"'


def decode_mailbox(name):
    def convert(m):
        content = m.group(1)
        if not content:
            return '&'
        value = content.replace(',', '/')
        return base64.b64decode(value + '=' * (-len(value) % 4)).decode('utf-16-be')
    try:
        return re.sub(r'&([^-]*)-', convert, name)
    except (ValueError, UnicodeError):
        return name


def require_ok(response, operation):
    typ, data = response
    if typ != 'OK':
        raise ArchiveError(operation + ' vom IMAP-Server abgelehnt.')
    return data


def parse_list(rows):
    result = []
    pattern = rb'^\(([^)]*)\)\s+(NIL|"(?:\\.|[^"\\])*")\s+(.+)$'
    for row in rows or []:
        if row is None:
            continue
        raw, literal = (row if isinstance(row, tuple) else (row, None))
        m = re.match(pattern, raw)
        if not m:
            raise ArchiveError('IMAP-Ordnerliste nicht eindeutig lesbar; keine Archivierung gestartet.')
        if b'\\noselect' in m[1].lower().split():
            continue
        token = literal if literal is not None else m[3]
        if literal is None and token.startswith(b'"') and token.endswith(b'"'):
            token = re.sub(rb'\\(.)', rb'\1', token[1:-1])
        name = token.decode('ascii')
        quoted(name)
        if name not in result:
            result.append(name)
    return result


def select_folder(client, folder, readonly=True):
    require_ok(client.select(quoted(folder), readonly=readonly), 'Ordnerauswahl')
    kind, values = client.response('UIDVALIDITY')
    if not values or values[0] is None or not re.fullmatch(rb'[1-9]\d*', values[0]):
        raise ArchiveError('UIDVALIDITY fehlt. Vorgang sicherheitshalber gesperrt.')
    validity = int(values[0])
    _, sticky = client.response('UIDNOTSTICKY')
    if sticky and sticky != [None]:
        raise ArchiveError('Der Ordner verwendet nicht dauerhafte UIDs (UIDNOTSTICKY).')
    return validity


def internal_date(raw):
    m = re.fullmatch(r'\s*(\d{1,2})-([A-Za-z]{3})-(\d{4}) (\d\d):(\d\d):(\d\d) ([+-])(\d\d)(\d\d)', raw)
    if not m:
        raise ArchiveError('INTERNALDATE ist nicht eindeutig lesbar.')
    month = next((i + 1 for i, s in enumerate(MONTHS) if s.lower() == m[2].lower()), 0)
    if not month:
        raise ArchiveError('INTERNALDATE enthaelt einen unbekannten Monat.')
    return dt.date(int(m[3]), month, int(m[1]))


def metadata(client, uid):
    data = require_ok(client.uid('FETCH', str(uid), '(UID RFC822.SIZE INTERNALDATE FLAGS)'), 'Nachrichtenmetadaten')
    for item in data or []:
        if not isinstance(item, bytes):
            continue
        found_uid = re.search(rb'\bUID (\d+)\b', item)
        size = re.search(rb'\bRFC822.SIZE (\d+)\b', item)
        date = re.search(rb'\bINTERNALDATE "([^"]+)"', item)
        if found_uid and int(found_uid[1]) == uid and size and date:
            rawdate = date[1].decode('ascii')
            flags = [f.decode('ascii') for f in imaplib.ParseFlags(item)]
            return dict(uid=uid, size=int(size[1]), internaldate=rawdate,
                        date=internal_date(rawdate).isoformat(), flags=flags)
    raise ArchiveError('Nachricht nicht mehr vorhanden oder Metadaten unvollstaendig.')


def search_uids(client, *criteria):
    data = require_ok(client.uid('SEARCH', None, *criteria), 'Nachrichtensuche')
    raw = b' '.join(x for x in data if isinstance(x, bytes)).strip()
    if raw and not re.fullmatch(rb'\d+(?: \d+)*', raw):
        raise ArchiveError('Ungueltige UID-Antwort.')
    uids = [int(x) for x in raw.split()]
    if any(x < 1 for x in uids) or len(uids) != len(set(uids)):
        raise ArchiveError('Ungueltige oder doppelte UIDs.')
    return sorted(uids)



DATE_MODES = ('internal', 'header', 'compare')
MAX_DATE_HEADER = 65536

def validate_mode(value):
    if value not in DATE_MODES:
        raise ArchiveError('Unbekannte Datumsgrundlage. Keine Archivierung gestartet.')
    return value

def parse_header_date(data):
    """Read exactly one RFC 5322 Date header; invalid/ambiguous/future dates are excluded."""
    if not isinstance(data, bytes) or len(data) > MAX_DATE_HEADER:
        return None
    try:
        parsed = BytesHeaderParser(policy=policy.default).parsebytes(data, headersonly=True)
        values = parsed.get_all('Date') or []
        if len(values) != 1 or getattr(values[0], 'defects', []):
            return None
        moment = parsedate_to_datetime(str(values[0]))
        if moment.tzinfo is None or moment.utcoffset() is None:
            return None
        day = moment.date()
        if day.year < 1900 or day > dt.date.today() + dt.timedelta(days=1):
            return None
        return day.isoformat()
    except (ValueError, OverflowError, TypeError, IndexError, UnicodeError):
        return None

def header_date(client, uid):
    """Fetch only Date header without setting \Seen; never download full message here."""
    data = require_ok(client.uid('FETCH', str(uid), '(UID BODY.PEEK[HEADER.FIELDS (DATE)])'), 'Datum-Header')
    found = []
    for item in data or []:
        if not isinstance(item, tuple) or len(item) != 2 or not isinstance(item[1], bytes):
            continue
        uid_match = re.search(rb'\bUID (\d+)\b', item[0])
        if not uid_match or int(uid_match[1]) != uid:
            raise ArchiveError('Unklare UID-Antwort beim Datum-Header.')
        found.append(item[1])
    if len(found) != 1:
        raise ArchiveError('Datum-Header nicht eindeutig abrufbar.')
    return parse_header_date(found[0])

def eml_header_date(path):
    """Read only a bounded header prefix from an archived EML, never a whole giant file."""
    with Path(path).open('rb') as f:
        raw = f.read(MAX_DATE_HEADER)
    if b'\r\n\r\n' not in raw and b'\n\n' not in raw:
        return None
    return parse_header_date(raw)


def selected_date(record, mode, cutoff):
    """Return chosen date and reasons for conservative exclusion."""
    internal = record['date']
    header = record.get('header_date')
    if mode == 'internal':
        return (internal if internal < cutoff else None, None)
    if not header:
        return None, 'invalid'
    if mode == 'header':
        return (header if header < cutoff else None, None)
    # Comparison mode never selects conflicting classifications.
    if (internal < cutoff) != (header < cutoff):
        return None, 'conflict'
    return (internal if internal < cutoff else None, None)

def snapshot(client, account, cutoff, cancel=None, date_mode='internal'):
    date_mode = validate_mode(date_mode)
    cutoff_date = dt.date.fromisoformat(cutoff)
    if cutoff_date >= dt.date.today():
        raise ArchiveError('Der Stichtag muss in der Vergangenheit liegen.')
    query_date = '%02d-%s-%04d' % (cutoff_date.day, MONTHS[cutoff_date.month - 1], cutoff_date.year)
    folders = parse_list(require_ok(client.list(), 'Ordnerliste'))
    plan = dict(format=FORMAT, account_key=account_key(account), cutoff=cutoff,
                date_mode=date_mode, folders=[], count=0, bytes=0, first=None, last=None,
                examined=0, invalid_dates=0, disagreements=0, conflicts=0)
    for folder in folders:
        check_cancel(cancel)
        validity = select_folder(client, folder)
        # HEADER/COMPARE need all non-deleted UIDs, not merely IMAP BEFORE.
        # BEFORE uses INTERNALDATE and would silently hide older imported mail.
        uids = (search_uids(client, 'BEFORE', query_date, 'UNDELETED')
                if date_mode == 'internal' else search_uids(client, 'UNDELETED'))
        records = []
        stats = dict(examined=0, selected=0, invalid_dates=0, disagreements=0, conflicts=0)
        for uid in uids:
            check_cancel(cancel)
            record = metadata(client, uid)
            if '\\Deleted' in record['flags']:
                raise ArchiveError('Nachricht bereits als geloescht markiert.')
            if date_mode != 'internal':
                record['header_date'] = header_date(client, uid)
            chosen, reason = selected_date(record, date_mode, cutoff)
            stats['examined'] += 1
            plan['examined'] += 1
            if date_mode != 'internal':
                h = record['header_date']
                if not h:
                    plan['invalid_dates'] += 1
                    stats['invalid_dates'] += 1
                elif abs((dt.date.fromisoformat(h) - dt.date.fromisoformat(record['date'])).days) > 1:
                    plan['disagreements'] += 1
                    stats['disagreements'] += 1
            if reason == 'conflict':
                plan['conflicts'] += 1
                stats['conflicts'] += 1
            if chosen is None:
                continue
            record['selected_date'] = chosen
            records.append(record)
            stats['selected'] += 1
            plan['count'] += 1
            plan['bytes'] += record['size']
            plan['first'] = min(plan['first'] or chosen, chosen)
            plan['last'] = max(plan['last'] or chosen, chosen)
        plan['folders'].append(dict(name=folder, label=decode_mailbox(folder),
                                    uidvalidity=validity, messages=records))
        emit('progress', message='Ordner geprueft: ' + decode_mailbox(folder) +
             ' | ausgewaehlt: ' + str(stats['selected']) + ' / ' + str(stats['examined']) +
             ' | ungueltig: ' + str(stats['invalid_dates']) +
             ' | widerspruechlich: ' + str(stats['conflicts']),
             count=plan['count'], bytes=plan['bytes'])
    plan['plan_id'] = plan_id(plan)
    return plan


def plan_id(plan):
    stable = {k: v for k, v in plan.items() if k != 'plan_id'}
    return hashlib.sha256(json.dumps(stable, sort_keys=True, ensure_ascii=True).encode()).hexdigest()


def message_chunks(client, uid, expected_size, cancel=None):
    position = 0
    while position < expected_size:
        check_cancel(cancel)
        count = min(CHUNK, expected_size - position)
        result = require_ok(client.uid('FETCH', str(uid), '(UID BODY.PEEK[]<%d.%d>)' % (position, count)), 'Nachrichtenabruf')
        candidates = []
        for item in result or []:
            if isinstance(item, tuple) and len(item) == 2:
                uid_match = re.search(rb'\bUID (\d+)\b', item[0])
                offset = re.search(rb'BODY\[\](?:<(\d+)>)?\s+\{\d+\}', item[0], re.I)
                if uid_match and int(uid_match[1]) == uid and offset:
                    if offset[1] is None and (position != 0 or expected_size > count):
                        raise ArchiveError('Server unterstuetzt keinen eindeutigen Teilabruf.')
                    if offset[1] is not None and int(offset[1]) != position:
                        raise ArchiveError('Falsche Position im Teilabruf.')
                    candidates.append(item[1])
        if len(candidates) != 1 or len(candidates[0]) != count:
            raise ArchiveError('Nachricht nur unvollstaendig abgerufen; keine Loeschfreigabe.')
        yield candidates[0]
        position += count


def write_json(path, value):
    temp = Path(str(path) + '.tmp')
    with temp.open('w', encoding='utf-8', newline='\n') as f:
        json.dump(value, f, ensure_ascii=True, indent=2)
        f.flush()
        os.fsync(f.fileno())
    os.replace(temp, path)


def file_hash(path, cancel=None):
    digest = hashlib.sha256()
    with Path(path).open('rb') as f:
        while True:
            check_cancel(cancel)
            data = f.read(CHUNK)
            if not data:
                break
            digest.update(data)
    return digest.hexdigest()


def archive_file(root, relative):
    if not isinstance(relative, str) or not re.fullmatch(r'messages/[0-9a-f]{64}/[1-9]\d*\.eml', relative):
        raise ArchiveError('Ungueltiger Pfad im Archiv.')
    root = Path(root).resolve()
    path = root.joinpath(*relative.split('/'))
    if not path.resolve().is_relative_to(root) or path.is_symlink():
        raise ArchiveError('Archivpfad verlaesst den Archivordner.')
    return path


@contextlib.contextmanager
def locked(root):
    path = Path(root) / '.operation.lock'
    try:
        f = path.open('x')
    except FileExistsError:
        raise ArchiveError('Archiv wird verwendet oder ein alter Lauf wurde unterbrochen. .operation.lock nur nach Pruefung entfernen.') from None
    try:
        f.write(str(os.getpid()))
        f.close()
        yield
    finally:
        path.unlink(missing_ok=True)


def archive(client, account, cutoff, destination, expected_plan=None, cancel=None, date_mode='internal'):
    plan = snapshot(client, account, cutoff, cancel, date_mode=date_mode)
    if expected_plan and plan['plan_id'] != expected_plan:
        raise ArchiveError('Das Postfach hat sich seit der Vorschau geaendert. Bitte Vorschau erneut ausfuehren.')
    if not plan['count']:
        raise ArchiveError('Keine Nachrichten vor dem Stichtag vorhanden.')
    dest = Path(destination).resolve()
    dest.mkdir(parents=True, exist_ok=True)
    if shutil.disk_usage(dest).free < plan['bytes'] + RESERVE:
        raise ArchiveError('Nicht genug Speicher fuer vollstaendige Nachrichten und Sicherheitsreserve.')
    archive_id = uuid.uuid4().hex
    name = 'EmailArchiv von %s bis %s_%s' % (plan['first'].replace('-', '.'), plan['last'].replace('-', '.'), archive_id[:8])
    root = dest / name
    root.mkdir(exist_ok=False)
    manifest = dict(format=FORMAT, archive_id=archive_id, account_key=plan['account_key'],
                    cutoff=cutoff, date_mode=plan['date_mode'], first=plan['first'], last=plan['last'], expected_count=plan['count'],
                    expected_bytes=plan['bytes'], complete=False, folders=[])
    emit('archive_path', path=str(root))
    with locked(root):
        write_json(root / 'manifest.json', manifest)
        done = 0
        for folder in plan['folders']:
            check_cancel(cancel)
            validity = select_folder(client, folder['name'])
            if validity != folder['uidvalidity']:
                raise ArchiveError('UIDVALIDITY hat sich geaendert. Archiv bleibt unvollstaendig.')
            saved = dict(name=folder['name'], label=folder['label'], uidvalidity=validity, messages=[])
            manifest['folders'].append(saved)
            folder_key = hashlib.sha256(folder['name'].encode('ascii')).hexdigest()
            for expected in folder['messages']:
                record = metadata(client, expected['uid'])
                if date_mode != 'internal':
                    record['header_date'] = header_date(client, expected['uid'])
                record['selected_date'] = selected_date(record, date_mode, cutoff)[0]
                if record != expected:
                    raise ArchiveError('Nachrichtenmetadaten haben sich nach der Vorschau geaendert.')
                relative = 'messages/%s/%d.eml' % (folder_key, record['uid'])
                path = archive_file(root, relative)
                path.parent.mkdir(parents=True, exist_ok=True)
                if shutil.disk_usage(root).free < record['size'] + RESERVE:
                    raise ArchiveError('Speicherreserve unterschritten. Archiv bleibt unvollstaendig.')
                part = path.with_suffix('.part')
                digest = hashlib.sha256()
                with part.open('xb') as f:
                    for chunk in message_chunks(client, record['uid'], record['size'], cancel):
                        f.write(chunk)
                        digest.update(chunk)
                    f.flush()
                    os.fsync(f.fileno())
                os.replace(part, path)
                if date_mode != 'internal' and eml_header_date(path) != record['header_date']:
                    raise ArchiveError('Datum im archivierten EML weicht vom geprueften Header ab.')
                saved['messages'].append(dict(record, path=relative, sha256=digest.hexdigest()))
                done += 1
                if done % 100 == 0:
                    write_json(root / 'manifest.json', manifest)
                emit('progress', message='Nachricht vollstaendig gesichert', count=done, total=plan['count'])
        manifest['complete'] = True
        write_json(root / 'manifest.json', manifest)
        result = verify(root, cancel)
        emit('result', message='Archiv erstellt und vollstaendig geprueft. Im Postfach wurde nichts geloescht.', **result)
    return root


def load_manifest(root):
    path = Path(root) / 'manifest.json'
    if not path.is_file() or path.stat().st_size > 256 * 1024 * 1024:
        raise ArchiveError('Archivmanifest fehlt oder ist zu gross.')
    try:
        result = json.loads(path.read_text(encoding='utf-8'))
    except (ValueError, UnicodeError):
        raise ArchiveError('Archivmanifest ist unlesbar.') from None
    if result.get('format') != FORMAT or result.get('complete') is not True:
        raise ArchiveError('Unvollstaendiges oder fremdes Archiv. Bereinigung gesperrt.')
    if not re.fullmatch(r'[0-9a-f]{32}', result.get('archive_id', '')):
        raise ArchiveError('Ungueltige Archivkennung.')
    if not re.fullmatch(r'[0-9a-f]{64}', result.get('account_key', '')):
        raise ArchiveError('Kontozuordnung im Manifest fehlt.')
    dt.date.fromisoformat(result['cutoff'])
    return result


def verify(root, cancel=None):
    manifest = load_manifest(root)
    count = size = 0
    seen = set()
    for folder in manifest['folders']:
        quoted(folder['name'])
        if not isinstance(folder['uidvalidity'], int) or folder['uidvalidity'] < 1:
            raise ArchiveError('Ungueltige UIDVALIDITY im Manifest.')
        key = hashlib.sha256(folder['name'].encode('ascii')).hexdigest()
        for msg in folder['messages']:
            check_cancel(cancel)
            if not isinstance(msg['uid'], int) or msg['uid'] < 1:
                raise ArchiveError('Ungueltige UID im Manifest.')
            identity = (folder['name'], msg['uid'])
            if identity in seen or msg['path'] != 'messages/%s/%d.eml' % (key, msg['uid']):
                raise ArchiveError('Doppelte Nachricht oder falsche Pfadzuordnung.')
            seen.add(identity)
            mode = validate_mode(manifest.get('date_mode', 'internal'))
            if internal_date(msg['internaldate']).isoformat() != msg['date']:
                raise ArchiveError('Internes IMAP-Datum stimmt nicht mit dem Archiv ueberein.')
            selected, reason = selected_date(msg, mode, manifest['cutoff'])
            if not selected or selected != msg.get('selected_date', msg['date'] if mode == 'internal' else None):
                raise ArchiveError('Datumsgrundlage oder Altersauswahl stimmt nicht mit dem Archiv ueberein.')
            path = archive_file(root, msg['path'])
            if not path.is_file() or path.stat().st_size != msg['size'] or file_hash(path, cancel) != msg['sha256']:
                raise ArchiveError('Archivdatei fehlt oder ist veraendert. KEINE Bereinigung freigegeben.')
            if mode != 'internal' and eml_header_date(path) != msg.get('header_date'):
                raise ArchiveError('Datum-Header im Archiv stimmt nicht mit dem Auswahlprotokoll ueberein.')
            count += 1
            size += msg['size']
    if count != manifest['expected_count'] or size != manifest['expected_bytes'] or count < 1:
        raise ArchiveError('Archivumfang ist unvollstaendig.')
    return dict(path=str(Path(root).resolve()), count=count, bytes=size,
                archive_id=manifest['archive_id'], manifest_sha256=file_hash(Path(root) / 'manifest.json', cancel))


def cleanup(client, account, root, confirmation, expected_manifest, cancel=None):
    with locked(root):
        result = verify(root, cancel)
        manifest = load_manifest(root)
        if account_key(account) != manifest['account_key']:
            raise ArchiveError('Das Zielkonto stimmt NICHT mit dem archivierten Konto ueberein.')
        if confirmation != 'LOESCHEN ' + manifest['archive_id'] or result['manifest_sha256'] != expected_manifest:
            raise ArchiveError('Konkrete Loeschbestaetigung fehlt oder das Manifest wurde veraendert.')
        caps = b' '.join(require_ok(client.capability(), 'Faehigkeitsabfrage')).upper().split()
        if b'UIDPLUS' not in caps and b'IMAP4REV2' not in caps:
            raise ArchiveError('UIDPLUS/UID EXPUNGE fehlt. Sichere Postfachbereinigung nicht unterstuetzt.')
        for folder in manifest['folders']:
            if folder['messages'] and select_folder(client, folder['name']) != folder['uidvalidity']:
                raise ArchiveError('UIDVALIDITY abweichend. Es wird nichts geloescht.')
        journal = dict(archive_id=manifest['archive_id'], deleted=[], already_absent=[])
        for folder in manifest['folders']:
            if not folder['messages']:
                continue
            if select_folder(client, folder['name'], readonly=False) != folder['uidvalidity']:
                raise ArchiveError('UIDVALIDITY vor Schreibzugriff abweichend.')
            for msg in folder['messages']:
                check_cancel(cancel)
                if not search_uids(client, 'UID', str(msg['uid'])):
                    journal['already_absent'].append([folder['name'], msg['uid']])
                    continue
                actual = metadata(client, msg['uid'])
                if actual['size'] != msg['size'] or actual['internaldate'] != msg['internaldate']:
                    raise ArchiveError('Originalnachricht passt nicht mehr zum Archiv.')
                if '\\Deleted' in actual['flags']:
                    raise ArchiveError('Nachricht bereits anderweitig zum Loeschen markiert. Bitte Status pruefen.')
                if file_hash(archive_file(root, msg['path']), cancel) != msg['sha256']:
                    raise ArchiveError('Lokale Archivkopie vor Loeschung veraendert.')
                digest = hashlib.sha256()
                for chunk in message_chunks(client, msg['uid'], msg['size'], cancel):
                    digest.update(chunk)
                if digest.hexdigest() != msg['sha256']:
                    raise ArchiveError('Servernachricht weicht byteweise vom Archiv ab. Loeschung gesperrt.')
                check_cancel(cancel)
                uid = str(msg['uid'])
                require_ok(client.uid('STORE', uid, '+FLAGS.SILENT', '(\\Deleted)'), 'Loeschmarkierung')
                try:
                    require_ok(client.uid('EXPUNGE', uid), 'Gezieltes UID EXPUNGE')
                except Exception:
                    with contextlib.suppress(Exception):
                        client.uid('STORE', uid, '-FLAGS.SILENT', '(\\Deleted)')
                    raise ArchiveError('UID EXPUNGE nicht bestaetigt. Konto pruefen; Archiv unveraendert erhalten.') from None
                if search_uids(client, 'UID', uid):
                    raise ArchiveError('Server bestaetigt die Entfernung nicht. Keine weitere Bereinigung.')
                journal['deleted'].append([folder['name'], msg['uid']])
                write_json(Path(root) / 'cleanup-journal.json', journal)
                emit('progress', message='Archivierte Nachricht gezielt entfernt', count=len(journal['deleted']), total=result['count'])
        emit('result', message='Gezielte Bereinigung abgeschlossen.', deleted=len(journal['deleted']), absent=len(journal['already_absent']))
        return journal


def restore(client, root, cancel=None):
    with locked(root):
        result = verify(root, cancel)
        manifest = load_manifest(root)
        prefix = 'CM-Archiv-%s-%s' % (manifest['archive_id'][:8], uuid.uuid4().hex[:8])
        restored = 0
        for index, folder in enumerate(manifest['folders']):
            if not folder['messages']:
                continue
            target = prefix + '-Ordner-%03d' % (index + 1)
            require_ok(client.create(quoted(target)), 'Anlage des Wiederherstellungsordners')
            for msg in folder['messages']:
                check_cancel(cancel)
                path = archive_file(root, msg['path'])
                if msg['size'] > 128 * 1024 * 1024:
                    raise ArchiveError('Einzelmail ueber 128 MiB: bitte die EML-Datei extern importieren. Archiv bleibt erhalten.')
                raw = path.read_bytes()
                if hashlib.sha256(raw).hexdigest() != msg['sha256']:
                    raise ArchiveError('Archivdatei waehrend Wiederherstellung veraendert.')
                allowed = {'\\Seen', '\\Answered', '\\Flagged', '\\Draft'}
                flags = '(' + ' '.join(f for f in msg['flags'] if f in allowed) + ')'
                require_ok(client.append(quoted(target), flags, '"' + msg['internaldate'] + '"', raw), 'Nachrichtenwiederherstellung')
                restored += 1
                emit('progress', message='Nachricht in neuen Archivordner importiert', count=restored, total=result['count'])
        emit('result', message='Wiederherstellung abgeschlossen. Ordnerprefix: ' + prefix, count=restored)
        return restored


def connect(account, password):
    timeout = max(30, min(3600, int(account.get('timeout', 600))))
    context = ssl.create_default_context()
    host, port = account['host'], int(account['port'])
    if port == 143:
        client = imaplib.IMAP4(host, port, timeout=timeout)
        client.starttls(ssl_context=context)
    else:
        client = imaplib.IMAP4_SSL(host, port, ssl_context=context, timeout=timeout)
    try:
        require_ok(client.login(account['user'], password), 'Anmeldung')
        return client
    except Exception:
        with contextlib.suppress(Exception):
            client.logout()
        raise


def main():
    client = None
    try:
        req = json.loads(sys.stdin.readline())
        operation, cancel = req['operation'], req.get('cancel_file')
        if operation == 'verify':
            with locked(req['archive']):
                result = verify(req['archive'], cancel)
            emit('result', message='Alle Archivnachrichten erfolgreich geprueft.', **result)
            return 0
        if operation not in {'preview', 'archive', 'cleanup', 'restore'}:
            raise ArchiveError('Unbekannter Vorgang.')
        client = connect(req['account'], req.pop('password'))
        if operation == 'preview':
            plan = snapshot(client, req['account'], req['cutoff'], cancel, req.get('date_mode', 'internal'))
            emit('result', **{k: v for k, v in plan.items() if k != 'folders'})
        elif operation == 'archive':
            archive(client, req['account'], req['cutoff'], req['destination'], req.get('plan_id'), cancel, req.get('date_mode', 'internal'))
        elif operation == 'cleanup':
            cleanup(client, req['account'], req['archive'], req.get('confirmation'), req.get('manifest_sha256'), cancel)
        else:
            restore(client, req['archive'], cancel)
        return 0
    except Cancelled as ex:
        emit('cancelled', message=str(ex))
        return 2
    except ArchiveError as ex:
        emit('error', message=str(ex))
        return 1
    except Exception as ex:
        emit('error', message='Vorgang fehlgeschlagen (' + type(ex).__name__ + '). Verbindung, Berechtigungen und Speicher pruefen.')
        return 1
    finally:
        if client is not None:
            with contextlib.suppress(Exception):
                client.logout()


if __name__ == '__main__':
    raise SystemExit(main())
