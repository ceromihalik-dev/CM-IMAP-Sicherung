<p align="center"><img src="CMIMAPSicherung/Resources/cm_logo.png" alt="CM-Logo" width="140"></p>

# CM IMAP Sicherung

Windows-Anwendung für IMAP-Sicherungen, Prüfung, ZIP-Archive, Wiederherstellung und MBOX-Anzeige.

**Aktueller Entwicklungs-/Vorabstand: 1.4.0.2** · **Ersteller: C.Mihalik**

## Direkt starten

Das Windows-Paket aus dem Bereich **Releases** vollständig entpacken und **`CM IMAP Sicherung.exe`** doppelklicken. Nicht das automatisch angebotene „Source code“-Archiv verwenden, wenn eine fertige Anwendung gewünscht ist.

```text
CM-IMAP-Sicherung/
  CM IMAP Sicherung.exe
  py.exe
  runtime/
  README.md
  RELEASE-NOTES.md
  ARCHIVIERUNG.md
  BUILD-INFO.json
  FILES.sha256
```

`py.exe` und `runtime` müssen neben der Anwendung bleiben. Das ist ein startfertiges Programmpaket, keine einzelne selbstentpackende EXE. Python und die benötigten Backup-/ZIP-Pakete sind mitgeliefert; keine separate Python-Installation und kein eigener Build erforderlich. Erforderlich sind **Windows 10/11 x64 und .NET Framework 4.8**. Die Anwendung ist **nicht codesigniert**.

## Oberfläche

Dunkles, an VS Code orientiertes Farbschema mit Emoji-/Symbolbeschriftungen. Die Aktionen sind links in aufklappbare Gruppen aufgeteilt:

- **Sicherung:** alle Konten, ausgewähltes Konto, Abbrechen.
- **Archiv & Ansicht:** E-Mail-Archiv, bisherige Backup-Wiederherstellung, MBOX Viewer, Backup-Ordner.
- **Konten:** Hinzufügen, Bearbeiten, Verbindungstest, Kennwort, Entfernen.
- **Verwaltung:** Prüfung/ZIP, Zeitplan, Konto-/Gesamthistorie, Protokolle, Einstellungen.
- **Projekt:** Info und freiwillige Unterstützung.

Die Darstellung der Emoji kann je nach Windows-Version und Schriftart farbig oder einfarbig ausfallen.

## E-Mail-Archiv und Postfachspeicher

Die neue Funktion archiviert Nachrichten **vor einem einstellbaren Stichtag**, standardmäßig älter als zehn Kalenderjahre. Maßgeblich ist das IMAP-Eingangsdatum `INTERNALDATE`, nicht der möglicherweise abweichende `Date`-Header. Der Stichtag selbst ist ausgeschlossen.

1. Konto auswählen und **Vorschau** ausführen: Anzahl, Datenmenge und tatsächlicher Zeitraum.
2. **Archiv erstellen**: vollständige EML-Dateien inklusive Anhängen, Ordnerzuordnung und Prüfnachweis. Der Archivordner heißt beispielsweise `EmailArchiv von 2004.02.01 bis 2015.12.31_<Kennung>`.
3. **Archiv prüfen**: jede Nachricht wird erneut gelesen und mit ihrem SHA-256-Wert verglichen.
4. Eine zusätzliche, geprüfte Kopie auf einem anderen Datenträger anlegen.
5. Erst bei Bedarf **Postfach bereinigen** wählen und das konkrete Konto ausdrücklich bestätigen.

**Archiv erstellen löscht nichts.** Nur die getrennte Bereinigung entfernt exakt die geprüften Nachrichten vom Mailserver. Sie ist an das Quellkonto, die Ordner-UIDVALIDITY und den Prüfnachweis gebunden. Vor jeder Entfernung werden zusätzlich die Originalbytes vom Server mit dem Archiv verglichen. Ohne gezieltes `UID EXPUNGE` bleibt die Bereinigung gesperrt. Fremde Löschmarkierungen werden nicht pauschal ausgeführt.

Die Bereinigung verkleinert **keine bereits vorhandenen lokalen MBOX-Sicherungen**. Lokale Archivdateien benötigen zusätzlichen Speicher. Serverregeln, Labels, Aufbewahrung und weitere Kopien können beeinflussen, wie viel Serverquota tatsächlich freigegeben wird.

Das neue Archiv verwendet **EML + `manifest.json`** und ist selbst **nicht verschlüsselt**. Es gehört auf einen geschützten bzw. verschlüsselten Datenträger. Der bisherige MBOX-Viewer liest MBOX-Dateien; die neuen EML-Dateien können mit einem geeigneten Mailprogramm geöffnet und über den Archivdialog wieder in neue IMAP-Ordner importiert werden. Die ZIP-Funktion für bisherige Kontosicherungen bleibt separat bestehen.

Details: [Archivierung und Schutzmaßnahmen](docs/ARCHIVIERUNG.md).

## Bestehende Funktionen

Mehrkonten-Backup über `imapbackup3`, IMAP-Erkennung mit manueller Korrektur, Fortschritt, Wiederholungsversuche, Zeitplan, Tray, Historien, Speicherwarnung, SHA-256-Dateiprüfung, AES-256-ZIP/ZIP64, MBOX-Wiederherstellung und MBOX-Viewer mit Suche, Anhang-/EML-Export und Druck.

Eine Servererkennung ist keine erfolgreiche Anmeldung. OAuth-Anmeldung für Anbieter, die sie zwingend verlangen, ist in dieser Fassung nicht implementiert. Gespeicherte Kennwörter bzw. ein unterstütztes App-Kennwort werden benötigt.

## Einstellungen und Datenschutz

Vorhandene Einstellungen werden weiterhin aus `%APPDATA%\IMAP-Backup-App` gelesen; gespeicherte Kennwörter liegen Windows-benutzergebunden unter `%LOCALAPPDATA%\IMAP-Backup-Secrets`. Das Windows-ZIP enthält keine Kontokonfiguration oder privaten Maildaten. Der Erststart auf einem anderen PC übernimmt nicht automatisch die Zugangsdaten des Entwicklungs-PCs.

Die neue Archivfunktion übergibt das entschlüsselte Kennwort ausschließlich über die Standardeingabe an ihren lokalen Prozess. Die bisherigen Backup-/ZIP-/Restore-Routinen verwenden teilweise kurzzeitig entschlüsselte temporäre Kennwortdateien. DPAPI schützt die gespeicherten Kennwörter, ist aber kein Schutz vor Schadsoftware im bereits angemeldeten Benutzerkonto.

Protokolle, Archive und Mailinhalte können personenbezogene Daten enthalten und gehören nicht ins öffentliche Repository. `.gitignore` ist lediglich ein zusätzlicher Schutz; vor jeder Veröffentlichung müssen die tatsächlich enthaltenen Dateien geprüft werden.

## Unterstützung

**❤️ Projekt unterstützen** ist freiwillig; alle Funktionen bleiben unabhängig davon verfügbar. In diesem Paket fehlt noch der eindeutig bestätigte öffentliche PayPal-Hosted-Button-Link. Deshalb öffnet der Button keine erfundene Zahlungsadresse. Es werden keine Bank- oder PayPal-Zugangsdaten benötigt.

## Prüfumfang / Vorabversion

Der CI-Workflow führt 29 Offline-Sicherheitstests mit simulierten Postfächern aus, kompiliert die Windows-EXE, prüft die mitgelieferte Python-Laufzeit und öffnet/rendert Haupt- und Archivfenster. Die Testprotokolle und Screenshots sind als separate QA-Artefakte verfügbar.

Ein echter End-to-End-Test mit einem 10-GB-Postfach und verschiedenen IMAP-Anbietern ist **nicht** erfolgt. Die neue Archivbereinigung daher zuerst mit einem separaten Testkonto erproben. Der Release wird zunächst als **Vorabversion** veröffentlicht; Version 1.4.0.1 bleibt unverändert.

## Selbst bauen

`Build-Release.bat` erzeugt die Anwendung und den Python-Launcher. Für das vollständige Windows-Paket anschließend `tools/Package-Windows.ps1` ausführen. Der Paket-Build benötigt eine Python-Installation und Internetzugang; der ausgelieferte Anwender benötigt beides für die lokale Anzeige nicht. Online-Sicherung/-Wiederherstellung benötigen natürlich eine Verbindung zum Mailserver.

`BUILD-INFO.json` dokumentiert Versions- und Quellstände. `FILES.sha256` enthält die Paketdateiprüfsummen; neben dem ZIP liegt dessen SHA-256-Prüfsumme. Hinweise zu mitgelieferten Lizenzen: [THIRD-PARTY.md](docs/THIRD-PARTY.md).

## Lizenz

Für den eigenen CM-IMAP-Sicherung-Quellcode ist weiterhin keine Open-Source-Lizenz festgelegt. Mitgelieferte Drittanbieter-Lizenzen bleiben erhalten.
