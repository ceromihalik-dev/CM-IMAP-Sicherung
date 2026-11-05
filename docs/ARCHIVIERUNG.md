# Altersbasierte Archivierung – 1.4.0.2

Der Ausbau verwendet getrennte Schritte: Vorschau, Archiv erstellen, Archiv prüfen und optional Postfach bereinigen. Standardmäßig werden keine Nachrichten gelöscht.

- Maßgeblich ist das IMAP-Eingangsdatum (INTERNALDATE), nicht der möglicherweise falsche Date-Header.
- „Älter als 10 Jahre“ wird als kalendarischer Stichtag umgesetzt. Nachrichten am Stichtag bleiben erhalten.
- Das Archiv liegt lokal oder auf einem vom Nutzer ausgewählten externen Laufwerk, nicht im selben IMAP-Postfach.
- Vollständige EML-Nachrichten inklusive Anhängen, Ordnerzuordnung und SHA-256-Prüfnachweis bleiben erhalten.
- Bereinigung erst nach vollständiger erneuter Prüfung, unveränderter UIDVALIDITY und ausdrücklicher Bestätigung für das konkrete Konto.
- Es wird ausschließlich UID EXPUNGE für die archivierten Nachrichten verwendet. Ohne passende Serverunterstützung bleibt die Bereinigung gesperrt; normales EXPUNGE ist kein Ersatz.
- Abgebrochene oder unvollständige Archive sind keine Freigabe zum Löschen.
- Das Archiv ist keine zweite unabhängige Kopie. Vor Bereinigung wird eine zusätzliche Sicherung auf einem getrennten Datenträger empfohlen.
- Ein lokales Archiv schafft nur dann Postfachspeicher frei, wenn anschließend die bestätigte Bereinigung durchgeführt wird. Der belegte Platz auf dem Archivlaufwerk nimmt dabei zu.

Die Umsetzung und Tests erfolgen auf einem separaten Entwicklungsbranch. Es werden keine realen E-Mail-Konten im Repository gespeichert oder durch die Entwicklung verändert.
