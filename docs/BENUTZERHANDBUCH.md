# CM IMAP Sicherung — Benutzerhandbuch

**Version 1.4.0.4 · Windows 10/11 x64 · Ersteller C.Mihalik**

Diese Anleitung ist sowohl als Datei im Release-Paket als auch **direkt in der Anwendung** über **Projekt → Benutzeranleitung** und **F1** verfügbar. In der Anwendung können alle Kapitel durchsucht werden; **Strg+F** öffnet die Suche. Die Hilfe funktioniert auch ohne Internet.

## 1. Installation und Programmstart

1. Das Windows-Paket aus dem GitHub-Release herunterladen und **vollständig entpacken**.
2. `CM IMAP Sicherung.exe` doppelklicken; nicht nur die EXE aus dem ZIP herausziehen.
3. `py.exe` und den Ordner `runtime` im selben Programmverzeichnis belassen. Eine separate Python-Installation ist nicht erforderlich.
4. Unter Windows muss **.NET Framework 4.8** verfügbar sein. Die EXE ist bisher nicht codesigniert.

Die Programmdaten bzw. gespeicherten Konten liegen weiterhin im jeweiligen Windows-Benutzerprofil und nicht im ZIP-Paket. Das Entpacken einer neuen Version über den alten Programmordner ist nur bei geschlossener Anwendung sinnvoll. Eigene Mailarchive niemals in den Programmordner legen.

## 2. Das neue Hauptfenster

Die Oberfläche ist in Arbeitsbereiche gegliedert. Die Navigation links ist aufklappbar. Die Buttons haben farbige Grafiksymbole; bei hoher Windows-Skalierung können die Farben anders wirken. Die Fortschrittsanzeigen sind animiert:

- **Gesamt:** Zahlenfortschritt der bereits abgearbeiteten Konten, mit Prozentwert, sofern Platz vorhanden ist.
- **Aktuelles Konto:** animiertes Aktivitätsband ohne erfundene Prozentanzeige, da die Gesamtdatenmenge nicht immer zuverlässig bekannt ist.
- Eine laufende Animation ist **kein Beweis** dafür, dass Daten erfolgreich übertragen wurden. Protokoll und Abschlussstatus prüfen.

## 3. E-Mail-Konto hinzufügen

Unter **Konten → Konto hinzufügen** die E-Mail-Adresse, IMAP-Server und den Port eintragen. Die automatische Erkennung ist ein Vorschlag; die Angaben müssen zum Anbieter passen. Für IMAP über TLS ist Port 993 üblich. Manche Provider verlangen ein App-Kennwort oder ein gesondertes Anmeldeverfahren.

**Verbindung testen** prüft die erreichbare Verbindung und die Anmeldedaten. Danach das Konto aktivieren. Kennwörter werden unter Windows mit DPAPI benutzergebunden geschützt, sind aber keine Garantie gegen Schadsoftware im laufenden Benutzerkonto.

## 4. Sicherungsziel und manuelles Backup

Im Hauptfenster das **Sicherungsziel** wählen und auf ausreichenden freien Platz achten. Danach:

- **Alle sichern:** alle für die Sicherung aktivierten Konten.
- **Auswahl sichern:** gezieltes Sichern des markierten Kontos.
- **Abbrechen:** laufenden Vorgang kontrolliert anhalten; unvollständige Sicherungen nicht als geprüft behandeln.

Nach Abschluss Konto-Historie oder Gesamt-Historie öffnen, Protokoll lesen und Sicherungsdateien kontrollieren.

## 5. Automatische Sicherungen

Unter **Verwaltung → Zeitplan** die gewünschte automatische Ausführung einrichten. Die Aufgabe verwendet die Windows-Aufgabenplanung. Der entsprechende Windows-Benutzer benötigt passende Zugriffsrechte auf Zielverzeichnis und Anmeldedaten. Für die Diagnose der Ausführung Windows-Aufgabenplanung und Programmprotokolle heranziehen.

## 6. Backups prüfen, ZIP verschlüsseln und MBOX lesen

**Verwaltung → Prüfen & ZIP** stellt vorhandene Prüffunktionen und die AES-256-ZIP-Verschlüsselung bereit. Ein verschlüsseltes ZIP-Kennwort muss unabhängig sicher aufbewahrt werden. Prüfsummen erkennen unbeabsichtigte Beschädigung, ersetzen aber keine zweite Sicherung.

Unter **Archiv & Ansicht → MBOX Viewer** eine vorhandene MBOX öffnen, suchen, E-Mails lesen und bei Bedarf einzelne EML-Dateien oder Anhänge exportieren. EML-Verzeichnisarchive sind nicht das gleiche Format wie MBOX.

## 7. Alte IMAP-E-Mails archivieren

Unter **Archiv & Ansicht → E-Mail-Archiv** das richtige Konto wählen. Die **Altersgrenze** steht standardmäßig auf zehn Jahren und lässt sich verändern; alternativ den Stichtag direkt auswählen.

### Datumsgrundlage wählen
- **INTERNALDATE** (Standard): ursprüngliches IMAP-Serverdatum, schnellste Auswahl.
- **E-Mail-Datum (Date-Header)**: der in der Nachricht gespeicherte Versandzeitpunkt. Er kann bei später importierten Nachrichten erheblich vom internen Serverdatum abweichen.
- **Beide vergleichen**: ausschließlich Nachrichten archivieren, bei denen beide gültigen Daten vor dem Stichtag liegen. Diese Einstellung ist bewusst konservativer.

Die Vorschau prüft bei Header- und Vergleichsmodus alle nicht zum Löschen markierten IMAP-Nachrichten; sie kann deshalb länger dauern. Ungültige, doppelte, fehlende oder unrealistisch zukünftige Date-Header werden ausgeschlossen und gezählt. Ein Unterschied der beiden Daten um mehr als einen Tag wird als Abweichung aufgeführt; unterschiedliche Altersbewertungen werden im Vergleichsmodus ausgeschlossen. Beim Ändern der Datumsgrundlage muss die Vorschau erneut ausgeführt werden.

Die Datumsgrundlage und beide Daten werden im neuen Archivmanifest gespeichert und mit der EML-Kopie überprüft. Die spätere Bereinigung bezieht sich stets auf die geprüfte Auswahl des Archivs, nicht auf die derzeit im Dialog gewählte Option.

1. **Vorschau:** Alle vom Server gelieferten auswählbaren IMAP-Ordner werden überprüft. Das Datum basiert auf **INTERNALDATE**, nicht zwingend dem sichtbaren Absenderdatum.
2. **Archiv erstellen:** Original-E-Mails inklusive Anhängen werden als EML-Dateien in ein separates Verzeichnis gespeichert. Die Datei `manifest.json` dokumentiert UID, Ordner, Datumsbereich und Prüfsummen.
3. **Archiv prüfen:** Die Dateien werden erneut eingelesen und mit SHA-256 kontrolliert.
4. **Zweite Kopie erstellen:** Vor einer Bereinigung eine zusätzliche vollständig geprüfte Archivkopie auf **einem anderen Datenträger** anlegen.
5. **Postfach bereinigen:** Nur bei Bedarf. Konto nochmals bestätigen. Diese Aktion kann Nachrichten **endgültig vom IMAP-Server entfernen**.

Ein Archiv heißt etwa `EmailArchiv von 2008.01.01 bis 2016.11.05_ab12cd34`, wobei die Daten den tatsächlichen Zeitraum der archivierten Nachrichten wiedergeben. Das lokale Archiv ist **nicht automatisch verschlüsselt**; für vertrauliche Inhalte einen geschützten oder verschlüsselten Datenträger verwenden.

### Welche Ordner sind enthalten?

Die Anwendung fragt die **IMAP-Ordner des jeweiligen Servers** ab. Die Ordner werden bei der Vorschau im Protokoll angezeigt. Nicht auswählbare `\Noselect`-Ordner werden übersprungen; ihre auswählbaren Unterordner können enthalten sein.

**Wichtig:** Thunderbirds lokale oder POP3-Archive mit einer `mailbox://`-Adresse liegen nicht auf dem IMAP-Server und sind deshalb nicht Teil dieser Archivfunktion. Thunderbird-POP3-/Lokalarchivimport wurde bewusst nicht integriert.

### Was passiert mit dem Speicherplatz?

Beim **Archivieren allein wird nichts gelöscht**. Speicher auf dem Mailserver kann erst durch die gesonderte Bereinigung freigegeben werden. Der Anbieter kann aufgrund seiner Aufbewahrungs- und Labelregeln dennoch weiter Speicher belegen. Bereits vorhandene lokale MBOX-Sicherungen bleiben unverändert.

## 8. Gefahren der Postfachbereinigung

Diese Funktion verlangt vollständige Prüfung und Kontozuordnung, unveränderte UIDVALIDITY, passende UIDPLUS-Fähigkeit und **gezieltes UID EXPUNGE**. Vor jeder Löschung wird die ursprüngliche Nachricht erneut gelesen und ihr Hash mit dem lokalen Archiv verglichen. Ein pauschales EXPUNGE oder CLOSE ist kein Ersatz.

**Die Bereinigung darf erst nach einer geprüften zweiten Kopie erfolgen.** Ein Abbruch macht bereits entfernte Nachrichten nicht automatisch rückgängig. Vor produktivem Einsatz zunächst ein separates Testkonto verwenden; ein vollständiger 10-GB-Live-End-to-End-Test wurde noch nicht durchgeführt.

## 9. Wiederherstellung

Im Archivdialog ein vorhandenes Archiv auswählen, prüfen und **Archiv wiederherstellen**. Der Vorgang importiert die EML-Nachrichten in **neue IMAP-Ordner**, nicht über bestehende Ordner. Wiederholter Import kann Duplikate erzeugen.

Für einzelne E-Mails über 128 MiB ist die Archiv-Wiederherstellung begrenzt; die lokalen EML-Dateien bleiben dennoch erhalten und können anderweitig importiert werden. Die bisherige MBOX-Wiederherstellung ist eine separate Funktion.

## 10. Fehlerdiagnose

| Beobachtung | Prüfung |
|---|---|
| Verbindung fehlgeschlagen | IMAP-Server, TLS, Port, Benutzername und App-Kennwort prüfen |
| 0 Nachrichten in Vorschau | Stichtag, INTERNALDATE und geprüfte Serverordner im Protokoll kontrollieren |
| Thunderbird-Archiv fehlt | Lokaler `mailbox://`-Ordner? Dann kein IMAP-Ordner |
| Sicherung bleibt bei einem Konto | Protokoll, Serverantwort, Dateigröße, freie Kapazität, Timeout kontrollieren |
| Archivprüfung fehlgeschlagen | Unvollständige, fehlende oder geänderte EML-Dateien; niemals bereinigen |
| Bereinigung gesperrt | Falsches Konto, UIDVALIDITY, Prüfsumme, fehlendes UIDPLUS oder zweite Kopie |
| EXE startet nicht | Vollständiges ZIP entpackt? `runtime` und `py.exe` vorhanden? .NET 4.8? |
| Anzeige wirkt ungewohnt | Windows-Skalierung/Schriftgrößen prüfen; farbige Symbole sind Grafiken |

## 11. Datensicherheit und Unterstützung

E-Mail-Archive und Protokolle können vertrauliche Informationen enthalten. Keine Zugangsdaten, EML-/MBOX-Dateien oder persönliche Pfade in öffentliche GitHub-Issues hochladen.

Die freiwillige Unterstützung über das Projektmenü hat keinerlei Einfluss auf die Funktionen. Ein öffentlicher PayPal-Hosted-Link ist noch nicht bestätigt und wird nicht erfunden.

**GitHub-Projekt:** https://github.com/ceromihalik-dev/CM-IMAP-Sicherung

**Hinweis zum Versionsstand:** 1.4.0.4 verbessert Design, Fortschrittsanzeigen und Benutzeranleitung; die bestehende IMAP-Archivierungs- und Bereinigungslogik wird nicht erweitert.
