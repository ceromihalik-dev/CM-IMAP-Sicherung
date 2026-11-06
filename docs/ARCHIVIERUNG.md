# Alte E-Mails archivieren – Version 1.4.0.2

## Zweck und Format

Die Funktion erstellt ein separates Archiv alter Nachrichten eines IMAP-Kontos. Sie sucht in allen vom Server angebotenen auswählbaren Ordnern. Ordner, die sich nicht eindeutig lesen lassen, führen zum sicheren Abbruch statt stillschweigendem Überspringen. Bereits mit `\\Deleted` markierte Nachrichten werden nicht in eine neue Altersauswahl aufgenommen.

Der Stichtag bezieht sich auf **INTERNALDATE** (Eingangs-/Importdatum auf dem Server), nicht auf den Absender-Date-Header. Bei „10 Jahre“ wird kalendarisch zurückgerechnet. Der Stichtag selbst bleibt im Postfach. Bei zuvor importierten Nachrichten kann INTERNALDATE jünger sein als das sichtbare Absenderdatum.

```text
EmailArchiv von yyyy.mm.dd bis yyyy.mm.dd_<Kennung>/
  manifest.json
  messages/
    <Ordnerkennung>/
      <IMAP-UID>.eml
  cleanup-journal.json   # nach einer Bereinigung
```

Die Datumsgrenzen im Namen stammen aus den tatsächlich ausgewählten Nachrichten. Die Kennung verhindert Namenskollisionen. Die vollständigen EML-Dateien enthalten Originalbytes einschließlich Anhängen; das Manifest enthält die Zuordnung zum IMAP-Ordner, UIDVALIDITY, UID, Eingangsdatum, Flags, Größe und SHA-256. Das Konto ist über einen Fingerabdruck zugeordnet, nicht über ein gespeichertes Kennwort.

## Bedienung

**Vorschau:** Konto und Stichtag auswählen. Anzahl, Datenmenge und tatsächlichen Zeitraum prüfen. Bei Änderungen am Postfach seit der Vorschau wird die Archivierung gestoppt und eine neue Vorschau verlangt.

**Archiv erstellen:** ein separates, ausreichend großes Ziel wählen. Der Vorgang liest die Originalnachrichten in begrenzten Blöcken, schreibt temporäre Teildateien und übernimmt sie erst nach vollständigem Schreiben. Anschließend wird das vollständige Archiv erneut geprüft. Im Quellpostfach wird dabei nichts gelöscht.

**Archiv prüfen:** vorhandenen Archivordner mit `manifest.json` auswählen und prüfen. Fehlende/veränderte Dateien, falsche Pfade, widersprüchliche Mengen oder unvollständige Manifeste blockieren die Bereinigung.

**Zweite Kopie:** vor einer endgültigen Bereinigung eine zusätzliche vollständige Kopie auf einem anderen Datenträger erstellen und auch diese mit „Archiv prüfen“ prüfen. Danach das gewünschte Quellarchiv erneut auswählen und prüfen. Die Oberfläche verlangt eine Bestätigung dieser zweiten Kopie; sie kann deren physische Unabhängigkeit nicht automatisch feststellen.

**Postfach bereinigen:** vollständige Adresse des Zielkontos eintippen und die zweite Kopie bestätigen. Es werden nur die tatsächlich archivierten UIDs dieses Kontos entfernt. Die Routine prüft die UIDVALIDITY aller betroffenen Ordner, verlangt UIDPLUS/UID EXPUNGE und vergleicht vor jeder Entfernung die Serverbytes erneut mit der Archivkopie. Sie verwendet niemals ein pauschales EXPUNGE, CLOSE oder MOVE. Bereits anderweitig zum Löschen markierte Zielnachrichten führen zum Abbruch.

## Wiederherstellung

„Archiv wiederherstellen“ im Archivdialog wählen. Es werden neue getrennte IMAP-Ordner mit eindeutiger Kennung erzeugt. Die ursprüngliche Zuordnung ist im Manifest dokumentiert. Die vorhandene Ordnerstruktur wird nicht überschrieben. Wiederholte Importe erzeugen weitere Kopien. Es werden unterstützte Standardflags und INTERNALDATE übernommen, nicht aber Löschmarkierungen oder beliebige benutzerdefinierte Flags.

Einzelne Nachrichten über 128 MiB können mit dieser Wiederherstellung nicht hochgeladen werden; ihre vollständige EML-Datei bleibt vorhanden und kann über einen geeigneten anderen Mailclient importiert werden. Die Begrenzung gilt nicht für die Gesamtgröße des Archivs.

## Abbruch, Speicher und Schutz

- Abbruch erfolgt zwischen Nachrichten bzw. Datenblöcken. Während einer laufenden Serverantwort kann der eingestellte Timeout abgewartet werden müssen. Ein bereits bestätigter Löschvorgang wird nicht rückgängig gemacht; seine Archivkopie bleibt erhalten.
- Unvollständige Archive bleiben lokal erhalten, geben aber keine Löschfreigabe. Ein neuer Archivierungslauf beginnt in einem neuen Ordner, ohne Fortsetzung des Teilarchivs.
- Nach einem Prozessabsturz kann eine `.operation.lock` zurückbleiben. Sie nur entfernen, wenn nachweislich kein Prozess dieses Archiv benutzt; anschließend erneut prüfen.
- Die freie Zielkapazität wird vor und während der Archivierung geprüft. Metadaten und UID-Listen benötigen Arbeitsspeicher proportional zur Nachrichtenanzahl; die E-Mail-Inhalte werden blockweise übertragen.
- Die neue EML-Archivfunktion verschlüsselt ihre Dateien nicht. Ein geschützter/verschlüsselter Datenträger und geeignete Zugriffsrechte sind erforderlich. Die separate bestehende ZIP-Funktion für MBOX-Kontosicherungen ist nicht identisch mit dem EML-Archivformat.
- Das Erstellen eines lokalen Archivs allein schafft keinen Postfachspeicher frei. Nach endgültiger Bereinigung hängt die tatsächliche Quotenfreigabe vom Anbieter, dessen Aufbewahrung und weiteren Kopien/Labels ab. Bestehende lokale MBOX-Backups werden nicht verkleinert.
- Ein Hash-Prüfnachweis erkennt Beschädigung, ersetzt aber keine unabhängige zweite Kopie und keine digitale Signatur gegen einen Angreifer, der Dateien und Manifest gemeinsam ändern kann.

## Teststand

29 Offline-Tests mit simulierten IMAP-Antworten decken insbesondere Byteerhalt, Stichtag, Abbruch, beschädigte/fehlende Dateien, falsches Konto, UIDVALIDITY, fehlendes UIDPLUS, Serveränderungen und gezielte Löschung ohne Mitentfernen fremder Löschmarkierungen ab. Zusätzlich prüft CI Windows-Kompilierung, Runtime-Imports und Fensterstart/Rendering. Kein Live-Test mit einem realen 10-GB-Postfach oder sämtlichen IMAP-Anbietern. Deshalb zunächst Vorabversion und nur nach einem Test mit einem separaten Testkonto produktiv bereinigen.
