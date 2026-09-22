<p align="center">
  <img src="CMIMAPSicherung/Resources/cm_logo.png" alt="CM IMAP Sicherung" width="180">
</p>

# CM IMAP Sicherung

**CM IMAP Sicherung** ist eine Windows-Anwendung zur lokalen Sicherung, Prüfung, Archivierung, Anzeige und Wiederherstellung von IMAP-Postfächern.

Aktueller Stand: **1.4.0.1**  
Ersteller: **C.Mihalik**

## Funktionen

- Mehrere IMAP-Konten verwalten und sichern
- automatische Erkennung typischer IMAP-Server und Ports
- inkrementelle Sicherung über `imapbackup3`
- Large-Mailbox-Modus für große Postfächer
- Fortschrittsanzeige, Logs und Sicherungshistorie
- Windows-Tray-Betrieb und zeitgesteuerte Sicherung
- Wiederholungsversuche bei Fehlern
- SHA-256-Backup-Prüfung
- AES-256-verschlüsselte ZIP-Archive
- Speicherplatzüberwachung und Warnungen
- IMAP-Wiederherstellung aus MBOX bzw. verschlüsselten Archiven
- integrierter MBOX-Viewer mit Suche, Anhängen, EML-Export und Druck
- DPAPI-geschützte Kennwortspeicherung unter Windows

## Sicherheit

Das Repository enthält **keine E-Mail-Konten, Kennwörter oder realen MBOX-Sicherungen**. Zugangsdaten werden von der Anwendung lokal und benutzergebunden über Windows DPAPI gespeichert.

> **Wichtig:** Niemals `.cred`, `.mbox`, reale E-Mail-Backups oder lokale `config.xml`-Dateien committen. Die `.gitignore` schließt diese Dateien bereits aus.

## Voraussetzungen

- Windows 10 oder Windows 11
- .NET Framework 4.8 bzw. ein passender C#-Build-Toolchain
- Python 3
- Python-Paket `imapbackup3`
- optional `pyzipper` für AES-verschlüsselte ZIP-Archive

Fehlende Python-Pakete können von der Anwendung teilweise automatisch installiert werden.

## Build

Einfach unter Windows ausführen:

```bat
Build-Release.bat
```

Alternativ kann die Lösung `CM-IMAP-Sicherung.sln` in Visual Studio geöffnet werden.

Die Release-EXE wird im Release-Ausgabeordner des Projekts erzeugt.

## Schnellstart

1. Anwendung bauen und starten.
2. **Konto +** wählen.
3. E-Mail-Adresse eingeben.
4. **IMAP automatisch erkennen** verwenden oder Server/Port manuell setzen.
5. Kennwort hinterlegen.
6. **Verbindung testen**.
7. Sicherungsziel wählen und Backup starten.

## Große Postfächer

Für größere Postfächer, z. B. 10 GB und mehr, stehen erhöhte IMAP-Timeouts, Speicherwarnungen, ZIP64 und weitere Large-Mailbox-Optimierungen zur Verfügung. Als Backupziel wird NTFS oder ein anderes Dateisystem ohne 4-GB-Einzeldateigrenze empfohlen.

## MBOX Viewer

Der integrierte Viewer arbeitet lesend auf den Backup-Dateien. Unterstützt werden unter anderem:

- Ordner-/MBOX-Navigation
- Nachrichtenliste
- Volltextsuche
- Vorschau von Text- und HTML-Mails
- Anhänge speichern
- Nachricht als EML exportieren
- Druck / PDF über den Windows-Druckdialog

## Wiederherstellung

Backups können wieder in ein IMAP-Konto importiert werden. Die Wiederherstellung ist bewusst nicht destruktiv; vorhandene Nachrichten werden nicht automatisch gelöscht. Bei mehrfachen Imports können daher Duplikate entstehen.

## Projektstruktur

```text
CM-IMAP-Sicherung.sln
CMIMAPSicherung/
  MainForm.cs
  AccountEditForm.cs
  ImapAutoDetect.cs
  ImapBackupCompatibility.cs
  MboxViewerForm.cs
  RestoreForm.cs
  BackupTools.cs
  SettingsForm.cs
  SchedulerForm.cs
  Resources/
Build-Release.bat
```

## Datenschutz

Alle Sicherungen werden lokal in das vom Benutzer gewählte Verzeichnis geschrieben. Vor einer Veröffentlichung eigener Forks sollten lokale Kontodaten, Protokolle und Backup-Dateien auf persönliche Informationen geprüft werden.

## Lizenz

Für dieses Repository ist derzeit **keine Open-Source-Lizenz festgelegt**. Ohne separate Lizenz bleiben die Rechte beim Urheber. Wenn das Projekt ausdrücklich als Open Source veröffentlicht werden soll, sollte vorab eine passende Lizenz wie MIT, Apache-2.0 oder GPL-3.0 ausgewählt werden.
