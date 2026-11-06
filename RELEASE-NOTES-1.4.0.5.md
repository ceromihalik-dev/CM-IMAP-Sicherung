# CM IMAP Sicherung 1.4.0.5 – Dashboard & Bedienung (Vorabversion)

Ersteller: C.Mihalik

## Übersicht

- Neu aufgebautes dunkles Dashboard: vier gleichmäßige Statuskarten, kompakte Kopfzeile und zentrale Aktionsleiste.
- Alle fünf Navigationsgruppen starten eingeklappt, bleiben aber einzeln bedienbar.
- Das **Hilfe-Menü** enthält jetzt **Benutzeranleitung**; zusätzlich F1 und Hilfe im Archivdialog.
- Kontentabelle mit Suche, Filtern, Sortierung, erweiterten Kurzinfos und weniger redundanten Spalten.
- Klare Trennung zwischen erfolgreichem Verbindungstest, letzter Sicherung und tatsächlich im Sicherungsziel vorhandenen Daten. Hinweis bei 0 B.
- Ausblendbares, bei laufenden Backups bzw. Fehlern automatisch öffnendes Protokoll.
- Verbesserte animierte Fortschrittsanzeigen und größere Prozentschrift, sofern echte Mengenwerte verfügbar sind.
- Bei neuen E-Mail-Archiven steht das **E-Mail-Konto im Exportordnernamen**, zusätzlich zum tatsächlichen Datumsbereich und einer eindeutigen Kennung.
- Bestehende Archive und die sichere IMAP-Bereinigung bleiben erhalten.

## Installation

Gesamtes Windows-x64-ZIP entpacken und `CM IMAP Sicherung.exe` starten. Die beigelegte `py.exe` und `runtime` im selben Verzeichnis belassen. Windows 10/11 x64, .NET Framework 4.8. Keine separate Python-Installation nötig; das Paket ist bislang nicht codesigniert.

## Datensicherheit

Die neuen Funktionen betreffen die Oberfläche und neue Dateinamen, nicht die Authentifizierungs- oder Backup-Engine. Archive werden **nicht automatisch gelöscht**. Für eine spätere IMAP-Postfachbereinigung weiterhin ein vollständig geprüftes Archiv plus zweite unabhängige Kopie verlangen.

## Teststatus

Windows-CI und simulierte Archiv-Sicherheitstests prüfen den Build, die Python-Laufzeit sowie die Startzustände der Oberfläche. Es liegt weiterhin kein produktiver End-to-End-Test mit einem realen 10-GB-Postfach vor. Daher weiterhin eine Vorabversion; insbesondere Bereinigung zuerst mit einem separaten Testkonto prüfen.
