# Changelog

## 1.4.0.2 – Vorabversion

- Dunkles VS-Code-artiges Design und gruppierte, aufklappbare Navigation mit Emoji-/Symbolbeschriftungen.
- Altersbasierte E-Mail-Archivierung mit Vorschau, einstellbarem Stichtag, tatsächlichem Datumsbereich im Ordnernamen und vollständigen EML-Dateien.
- SHA-256-Prüfung jeder Archivnachricht und kontogebundene, ausdrücklich bestätigte Postfachbereinigung mittels UID EXPUNGE.
- Prüfung von UIDVALIDITY, Archivvollständigkeit und originalen Serverbytes; keine pauschale EXPUNGE-/CLOSE-Operation.
- Wiederherstellung des neuen Archivs in getrennte neue IMAP-Ordner; vorhandene MBOX-Funktionen bleiben bestehen.
- Startfertiges Windows-x64-Paket mit eigener CPython-Laufzeit, gepinnten Abhängigkeiten und direkt startbarer EXE.
- In-Process-Python-Launcher, damit der Abbruch eines Backup-Prozesses keinen weiterlaufenden Python-Kindprozess hinterlässt.
- Verifizierte TLS-Kontexte in der mitgelieferten Python-Laufzeit.
- 29 automatisierte Archiv-Sicherheitstests, Windows-Build, Laufzeit-Importtest und UI-Render-Smoke-Test.
- Paketprüfsummen, Build-Herkunft und Drittanbieter-Lizenzhinweise.
- Freiwilliger Unterstützungsbereich; der bestätigte öffentliche PayPal-Link ist noch offen.

Nicht als produktiv durchgetestet freigegeben: keine realen Mailbox-/10-GB-End-to-End-Tests. Neue Archive sind unverschlüsselte EML-Archive; vor Bereinigung ist eine zweite geprüfte Kopie erforderlich. Einzelmails über 128 MiB werden vollständig archiviert, benötigen für die Wiederherstellung aber einen externen EML-Import.

## 1.4.0.1

- Large-Mailbox-Optimierungen, IMAP-Autodetect und professionellere Oberfläche.
- SHA-256-Backup-Prüfung, AES-256-ZIP/ZIP64 und Speicherwarnungen.
- IMAP-Wiederherstellung und integrierter MBOX-Viewer.
- Volltextsuche, EML-Export und Druck im Viewer.
- Unicode-Kompatibilitäts-Fix für problematische IMAP-Header.
- Stabilitätsfix für MBOX-Viewer-Splitter.
