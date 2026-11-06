# CM IMAP Sicherung 1.4.0.2 – Vorabversion

Ersteller: C.Mihalik

## Neu
- Dunkles, an VS Code orientiertes Windows-Design, Emoji-Symbole und aufklappbare Aktionsgruppen statt einer langen Buttonleiste.
- Altersbasierte E-Mail-Archivierung mit einstellbaren Jahren (Standard 10) oder konkretem Stichtag.
- Getrennte Schritte: Vorschau, Archiv erstellen, vollständig prüfen und ausdrücklich bestätigte Postfachbereinigung.
- Archivname: `EmailArchiv von yyyy.mm.dd bis yyyy.mm.dd_<Kennung>` mit dem tatsächlich enthaltenen Datumsbereich.
- Originalgetreue EML-Dateien mit Anhängen, Ordnerzuordnung, IMAP-Eingangsdatum und SHA-256-Prüfnachweis.
- Wiederherstellung des EML-Archivs in neue, getrennte IMAP-Ordner. Bestehende MBOX-Wiederherstellung und MBOX-Viewer bleiben erhalten.
- Startfertiges Windows-x64-ZIP mit `CM IMAP Sicherung.exe` und eigener Python-Laufzeit. Keine separate Python-Installation erforderlich.
- Freiwilliger Bereich „❤️ Projekt unterstützen“; der konkrete öffentliche PayPal-Link ist noch nicht hinterlegt.

## Sicher arbeiten
Das Erstellen eines Archivs löscht nichts. Erst die gesondert bestätigte Bereinigung kann Speicher auf dem Mailserver freigeben. Sie erfordert ein vollständiges, erneut geprüftes Archiv, die passende Kontozuordnung, unveränderte UIDVALIDITY und gezieltes UID EXPUNGE. Vor jeder Löschung werden die Serverbytes erneut mit der Archivkopie verglichen. Ohne passende Serverunterstützung bleibt die Bereinigung gesperrt.

Vor der Bereinigung eine zusätzliche geprüfte Archivkopie auf einem anderen Datenträger anlegen. Das EML-Archiv selbst ist nicht verschlüsselt; einen geschützten/verschlüsselten Datenträger verwenden. Bestehende lokale MBOX-Sicherungen werden nicht verkleinert oder gelöscht. Bei Anbietern mit Labels, Aufbewahrung oder zusätzlichen Kopien kann die tatsächliche Quotenfreigabe abweichen.

## Start
Das vollständige Windows-ZIP entpacken und `CM IMAP Sicherung.exe` doppelklicken. `py.exe` und `runtime` neben der Anwendung belassen. Erforderlich: Windows 10/11 x64 und .NET Framework 4.8. Die EXE ist nicht codesigniert.

## Prüfstatus und Grenzen
Automatisierte Archiv-Sicherheitstests verwenden ausschließlich simulierte Postfächer. Der Windows-Build prüft die EXE, die eingebettete Python-Laufzeit sowie das Öffnen und Rendern der Haupt- und Archivfenster. Ein realer IMAP-End-to-End-Test mit einem 10-GB-Postfach wurde nicht durchgeführt. Deshalb zunächst Vorabversion und zuerst mit einem separaten Testkonto prüfen, bevor die endgültige Bereinigung produktiv verwendet wird.

Abgebrochene Archive bleiben erhalten, sind aber für die Bereinigung gesperrt; ein neuer Archivierungslauf beginnt neu. Bei der Wiederherstellung ist die einzelne E-Mail auf 128 MiB begrenzt, nicht das Gesamtarchiv. Größere Einzelmails bleiben als vollständige EML-Dateien für einen externen Import erhalten.
