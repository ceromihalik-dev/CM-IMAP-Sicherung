# CM IMAP Sicherung 1.4.0.4 – Datumswahl für E-Mail-Archive (Vorabversion)

**Ersteller: C.Mihalik**

Die E-Mail-Archivierung unterstützt nun drei auswählbare Datumsgrundlagen:

1. **IMAP-Serverdatum (INTERNALDATE)** – die bisherige und weiterhin voreingestellte Methode.
2. **E-Mail-Datum (Date-Header)** – erkennt auch alte, erst später in ein IMAP-Postfach importierte Nachrichten.
3. **Beide vergleichen** – konservative Auswahl nur bei zwei gültigen Datumswerten, die beide vor dem Stichtag liegen.

Die Vorschau zeigt pro Ordner die Anzahl geprüfter und ausgewählter Nachrichten; zusätzlich werden ungültige Headerdaten, Datumsabweichungen und widersprüchliche Altersbewertungen zusammengefasst. Fehlende, doppelte, ungültige und unrealistische Headerdaten werden nicht ungeprüft übernommen. Ein Moduswechsel erfordert eine neue Vorschau.

Der Modus und die beiden Datumswerte werden im Archivmanifest mitgeführt. Vor einer Bereinigung prüft die Software weiterhin Archivkopie, Kontozuordnung, UIDVALIDITY, SHA-256 und den exakten Nachrichtentext; nur gezieltes UID EXPUNGE ist zugelassen. **Keine automatische Löschung**.

Das startfertige Windows-x64-Paket enthält die direkt startbare EXE samt Python-Laufzeit. Keine eigene Python-Installation erforderlich; allerdings müssen `py.exe` und `runtime` im entpackten Verzeichnis verbleiben. Windows 10/11 x64 und .NET Framework 4.8 erforderlich, EXE nicht codesigniert.

**Vorabversion:** Sicherheitstests verwenden simulierte IMAP-Konten. Vor produktiven Löschungen an einem separaten Testkonto prüfen. Ohne gesonderte Bereinigung bleibt das Originalpostfach unverändert. Lokale Thunderbird-POP3-Archive sind weiterhin nicht integriert.

Vollständige Anleitung: **F1 → Benutzeranleitung** und `BENUTZERHANDBUCH.md`.
