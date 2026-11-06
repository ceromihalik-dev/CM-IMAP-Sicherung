import contextlib
import importlib.util
import io
import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch

PATH = Path(__file__).resolve().parents[1] / 'CMIMAPSicherung' / 'Archive' / 'archive_engine.py'
spec = importlib.util.spec_from_file_location('archive_engine', PATH)
e = importlib.util.module_from_spec(spec)
spec.loader.exec_module(e)
ACCOUNT = dict(host='imap.example.invalid', port=993, user='test@example.invalid')

class FakeImap:
    def __init__(self):
        self.validity = 17
        self.caps = b'IMAP4rev1 UIDPLUS'
        self.folder = 'INBOX'
        self.readonly = True
        self.writes = []
        self.body = b'Subject: old\r\nContent-Type: application/octet-stream\r\n\r\n\x00\xff\xe4From bytes\r\n'
        self.messages = {
            5: dict(raw=self.body, date='10-Jan-2000 10:11:12 +0100', flags=[]),
            6: dict(raw=b'Subject: cutoff\r\n\r\nkeep', date='01-Jan-2016 00:00:00 +0100', flags=[]),
            7: dict(raw=b'Subject: new\r\n\r\nkeep', date='10-Jan-2026 00:00:00 +0100', flags=[]),
            8: dict(raw=b'Subject: other-deleted\r\n\r\nkeep', date='10-Jan-2000 00:00:00 +0100', flags=['\\Deleted']),
        }
        self.short_body = False
        self.expunge_fails = False
        self.imported = []
    def list(self): return 'OK', [b'(\\HasNoChildren) "/" "INBOX"']
    def select(self, name, readonly=False):
        self.readonly = readonly
        return 'OK', [str(len(self.messages)).encode()]
    def response(self, name):
        return (name, [str(self.validity).encode()] if name == 'UIDVALIDITY' else [None])
    def capability(self): return 'OK', [self.caps]
    def create(self, name): self.writes.append(('CREATE', name)); return 'OK', [b'created']
    def append(self, folder, flags, date, raw):
        self.imported.append((folder, flags, date, raw)); return 'OK', [b'appended']
    def uid(self, cmd, *args):
        if cmd == 'SEARCH':
            if args[1] == 'UID':
                return 'OK', [str(args[2]).encode() if int(args[2]) in self.messages else b'']
            return 'OK', [b' '.join(str(uid).encode() for uid, m in self.messages.items()
                                   if e.internal_date(m['date']).isoformat() < '2016-01-01' and '\\Deleted' not in m['flags'])]
        uid = int(args[0])
        if cmd == 'FETCH':
            if uid not in self.messages: return 'OK', [None]
            m = self.messages[uid]
            if 'BODY.PEEK' not in args[1]:
                return 'OK', [('1 (UID %d RFC822.SIZE %d INTERNALDATE "%s" FLAGS (%s))' %
                               (uid, len(m['raw']), m['date'], ' '.join(m['flags']))).encode()]
            p = re.search(r'<(\d+)\.(\d+)>', args[1]); pos, count = map(int, p.groups())
            raw = m['raw'][pos:pos+count]
            if self.short_body: raw = raw[:-1]
            return 'OK', [(('1 (UID %d BODY[]<%d> {%d}' % (uid, pos, len(raw))).encode(), raw), b')']
        if cmd == 'STORE':
            assert not self.readonly
            self.writes.append((cmd, uid, args[1], args[2]))
            if args[1].startswith('+'): self.messages[uid]['flags'].append('\\Deleted')
            elif '\\Deleted' in self.messages[uid]['flags']: self.messages[uid]['flags'].remove('\\Deleted')
            return 'OK', [b'stored']
        if cmd == 'EXPUNGE':
            assert not self.readonly
            self.writes.append((cmd, uid))
            if self.expunge_fails: return 'NO', [b'failure']
            if uid in self.messages and '\\Deleted' in self.messages[uid]['flags']: del self.messages[uid]
            return 'OK', [b'expunged']
        raise AssertionError(cmd)
    def close(self): raise AssertionError('CLOSE forbidden')
    def expunge(self): raise AssertionError('unqualified EXPUNGE forbidden')

class Tests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.root = Path(self.tmp.name); self.client = FakeImap()
        self.stdout = contextlib.redirect_stdout(io.StringIO()); self.stdout.__enter__()
        self.disk = patch.object(e.shutil, 'disk_usage', return_value=type('Disk', (), dict(free=100*1024**3))()); self.disk.start()
    def tearDown(self):
        self.disk.stop(); self.stdout.__exit__(None, None, None); self.tmp.cleanup()
    def create(self): return e.archive(self.client, ACCOUNT, '2016-01-01', self.root)
    def clean(self, root, account=ACCOUNT, confirmation=None, digest=None):
        v=e.verify(root)
        return e.cleanup(self.client, account, root, confirmation or 'LOESCHEN '+v['archive_id'], digest or v['manifest_sha256'])
    def rewrite(self, root, edit):
        m=json.loads((root/'manifest.json').read_text()); edit(m); e.write_json(root/'manifest.json',m)
    def test_preview_boundary_and_no_writes(self):
        p=e.snapshot(self.client,ACCOUNT,'2016-01-01'); self.assertEqual(p['count'],1); self.assertFalse(self.client.writes)
    def test_binary_message_exact(self):
        root=self.create(); m=e.load_manifest(root); p=root/m['folders'][0]['messages'][0]['path']
        self.assertEqual(p.read_bytes(),self.client.body); self.assertFalse(self.client.writes)
    def test_range_filename(self):
        self.assertTrue(self.create().name.startswith('EmailArchiv von 2000.01.10 bis 2000.01.10_'))
    def test_verify_detects_corruption(self):
        root=self.create(); next(root.rglob('*.eml')).write_bytes(b'bad')
        with self.assertRaises(e.ArchiveError): self.clean(root)
        self.assertFalse(self.client.writes)
    def test_missing_file_blocks(self):
        root=self.create(); next(root.rglob('*.eml')).unlink()
        with self.assertRaises(e.ArchiveError): self.clean(root)
        self.assertFalse(self.client.writes)
    def test_account_mismatch_blocks(self):
        root=self.create()
        with self.assertRaises(e.ArchiveError): self.clean(root,dict(ACCOUNT,user='other@example.invalid'))
        self.assertFalse(self.client.writes)
    def test_missing_confirmation_blocks(self):
        root=self.create()
        with self.assertRaises(e.ArchiveError): self.clean(root,confirmation='YES')
        self.assertFalse(self.client.writes)
    def test_wrong_manifest_hash_blocks(self):
        root=self.create()
        with self.assertRaises(e.ArchiveError): self.clean(root,digest='0'*64)
        self.assertFalse(self.client.writes)
    def test_uidvalidity_blocks(self):
        root=self.create(); self.client.validity+=1
        with self.assertRaises(e.ArchiveError): self.clean(root)
        self.assertFalse(self.client.writes)
    def test_no_uidplus_blocks(self):
        root=self.create(); self.client.caps=b'IMAP4rev1'
        with self.assertRaises(e.ArchiveError): self.clean(root)
        self.assertFalse(self.client.writes)
    def test_only_archived_uid_removed(self):
        root=self.create(); self.clean(root)
        self.assertNotIn(5,self.client.messages); self.assertEqual(set(self.client.messages),{6,7,8})
        self.assertIn(('EXPUNGE',5),self.client.writes)
    def test_server_content_change_blocks(self):
        root=self.create(); self.client.messages[5]['raw']=b'X'+self.client.body[1:]
        with self.assertRaises(e.ArchiveError): self.clean(root)
        self.assertFalse(self.client.writes)
    def test_other_client_deleted_flag_blocks(self):
        root=self.create(); self.client.messages[5]['flags']=['\\Deleted']
        with self.assertRaises(e.ArchiveError): self.clean(root)
        self.assertFalse(self.client.writes)
    def test_failed_expunge_rolls_back_own_marker(self):
        root=self.create(); self.client.expunge_fails=True
        with self.assertRaises(e.ArchiveError): self.clean(root)
        self.assertNotIn('\\Deleted',self.client.messages[5]['flags'])
        self.assertIn('\\Deleted',self.client.messages[8]['flags'])
    def test_absent_uid_never_replaced_by_sequence_number(self):
        root=self.create(); del self.client.messages[5]; journal=self.clean(root)
        self.assertEqual(len(journal['already_absent']),1); self.assertFalse(self.client.writes)
    def test_repeated_cleanup_is_safe(self):
        root=self.create(); self.clean(root); n=len(self.client.writes); self.clean(root)
        self.assertEqual(len(self.client.writes),n)
    def test_short_fetch_keeps_incomplete_archive(self):
        self.client.short_body=True
        with self.assertRaises(e.ArchiveError): self.create()
        root=next(self.root.iterdir())
        with self.assertRaises(e.ArchiveError): e.verify(root)
        self.assertFalse(self.client.writes)
    def test_cancel_no_writes(self):
        root=self.create(); cancel=self.root/'cancel'; cancel.touch(); v=e.verify(root)
        with self.assertRaises(e.Cancelled): e.cleanup(self.client,ACCOUNT,root,'LOESCHEN '+v['archive_id'],v['manifest_sha256'],str(cancel))
        self.assertFalse(self.client.writes)
    def test_snapshot_changed_blocks_archive(self):
        plan=e.snapshot(self.client,ACCOUNT,'2016-01-01'); self.client.messages[5]['flags']=['\\Seen']
        with self.assertRaises(e.ArchiveError): e.archive(self.client,ACCOUNT,'2016-01-01',self.root,plan['plan_id'])
        self.assertFalse(list(self.root.iterdir()))
    def test_manifest_path_traversal_blocks(self):
        root=self.create(); self.rewrite(root,lambda m:m['folders'][0]['messages'][0].update(path='../private.eml'))
        with self.assertRaises(e.ArchiveError): e.verify(root)
    def test_manifest_count_mismatch_blocks(self):
        root=self.create(); self.rewrite(root,lambda m:m.update(expected_count=2))
        with self.assertRaises(e.ArchiveError): e.verify(root)
    def test_duplicate_uid_blocks(self):
        root=self.create(); self.rewrite(root,lambda m:m['folders'][0]['messages'].append(m['folders'][0]['messages'][0]))
        with self.assertRaises(e.ArchiveError): e.verify(root)
    def test_chunked_fetch(self):
        with patch.object(e,'CHUNK',7):
            root=self.create(); self.assertEqual(next(root.rglob('*.eml')).read_bytes(),self.client.body)
    def test_disk_full_no_creation(self):
        with patch.object(e.shutil,'disk_usage',return_value=type('Disk',(),dict(free=0))()):
            with self.assertRaises(e.ArchiveError): self.create()
        self.assertFalse(list(self.root.iterdir()))
    def test_restore_exact_bytes_separate_folder(self):
        root=self.create(); n=e.restore(self.client,root); self.assertEqual(n,1)
        folder,flags,date,raw=self.client.imported[0]; self.assertTrue(folder.startswith('"CM-Archiv-')); self.assertEqual(raw,self.client.body)
        self.assertEqual(set(self.client.messages),{5,6,7,8})
    def test_parse_folder_quotes_literals_and_noselect(self):
        rows=[b'(\\Noselect) "/" "parent"',b'() "/" "Sent \\"Mail\\""'.replace(b'\\\\"',b'\\"'),(b'() "/" {5}',b'INBOX')]
        self.assertIn('INBOX',e.parse_list(rows))
    def test_date_not_header_and_timezone_preserved(self):
        self.assertEqual(e.internal_date('31-Dec-2015 23:30:00 -1200').isoformat(),'2015-12-31')
    def test_lock_prevents_parallel_operations(self):
        root=self.create()
        with e.locked(root):
            with self.assertRaises(e.ArchiveError): self.clean(root)
    def test_no_plaintext_connection_fallback(self):
        with patch.object(e.imaplib,'IMAP4') as cls:
            cls.return_value.starttls.side_effect=RuntimeError('TLS failed')
            with self.assertRaises(RuntimeError): e.connect(dict(ACCOUNT,port=143),'not-a-real-password')
            cls.return_value.login.assert_not_called()

if __name__=='__main__': unittest.main(verbosity=2)
