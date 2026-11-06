# CM IMAP Sicherung 1.4.0.3 – Design & Benutzeranleitung (Vorabversion)

**Ersteller: C.Mihalik**

## Neue Funktionen

- Farbig gerenderte Emoji-Grafiksymbole statt einfarbiger Windows-Schriftzeichen; über 25 passende Symbole für Konten, Sicherung, Archiv, Verwaltung und Hilfe.
- Verbreiterte, gruppierte Navigation im dunkel gestalteten Hauptfenster.
- Eigenständig gezeichnete animierte Fortschrittsanzeigen in Hauptfenster und E-Mail-Archiv.
- Fortschritt in Prozent bei tatsächlichen Mengenangaben; Animation ohne Prozentbehauptung, wenn nur Aktivität bekannt ist.
- **Integrierte, durchsuchbare Benutzeranleitung** mit elf Kapiteln; erreichbar über Projekt → Benutzeranleitung, **F1** sowie aus dem Archivdialog.
- Offline-Benutzerhandbuch als `BENUTZERHANDBUCH.md` im Windows-Paket.
- UI-Smoke-Test prüft Hauptfenster, Archivfenster und integrierte Anleitung einschließlich farbiger Icons und animierter Balken.

## Unverändert

- Bestehende IMAP-Konten, Kennwortspeicherung und Backup-/Restore-Funktionen bleiben erhalten.
- Altersarchivierung und Bereinigung bleiben in ihrer bisherigen sicherheitsorientierten Implementierung. **Keine automatische Löschung.**
- Lokale Thunderbird-POP3-Archive werden weiterhin nicht eingelesen.

## Installation

Das komplette Windows-x64-ZIP entpacken. `CM IMAP Sicherung.exe` doppelklicken, `py.exe` und `runtime` im gleichen Ordner belassen. Windows 10/11 x64 sowie .NET Framework 4.8 erforderlich; die Anwendung ist nicht codesigniert.

## Prüfumfang

Automatische Python-Archiv-Sicherheitstests, Windows-C#-Build, eingebettete Python-Importprüfung und Smoke-Test der drei Fenster. Die Archiv-Bereinigung wurde weiterhin nicht mit realen IMAP-Postfächern eines produktiven Kontos durchgetestet: daher **Vorabversion** und vor Löschaktionen erst einen vollständigen Test mit einem separaten Konto durchführen.

Die farbigen Icon-Grafiken basieren auf Noto Color Emoji, SIL Open Font License 1.1. Die Schriftdatei selbst wird nicht verteilt.
