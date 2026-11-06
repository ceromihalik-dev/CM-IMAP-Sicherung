
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace CMIMAPSicherung
{
    /// <summary>Offline, searchable user guide accessible from both main and archive.</summary>
    public sealed class UserGuideForm:Form
    {
        private ListBox topics;
        private RichTextBox body;
        private TextBox search;
        private Label count;
        private readonly List<KeyValuePair<string,string>> chapters=new List<KeyValuePair<string,string>>();
        public UserGuideForm(string initial=null)
        {
            Text="Benutzeranleitung | CM IMAP Sicherung";
            MinimumSize=new Size(820,570);
            Size=new Size(1060,760);
            StartPosition=FormStartPosition.CenterParent;
            Font=new Font("Segoe UI",10);
            KeyPreview=true;
            chapters.Add(new KeyValuePair<string,string>("01 · Erste Schritte",
@"CM IMAP SICHERUNG
Benutzeranleitung · Version 1.4.0.4

INSTALLATION UND START
1. Das vollständige Windows-x64-ZIP aus dem GitHub-Release herunterladen.
2. In einen eigenen Ordner entpacken, nicht nur die EXE aus dem ZIP öffnen.
3. CM IMAP Sicherung.exe doppelklicken.

Im selben Verzeichnis müssen py.exe und runtime liegen. Windows 10/11 x64 und .NET Framework 4.8 werden benötigt. Python muss nicht selbst installiert werden. Die Anwendung ist nicht codesigniert.

Die Navigation links lässt sich in Bereiche aufklappen. Über das Buchsymbol oder F1 öffnen Sie diese Anleitung jederzeit.
Zur Sicherung gehören ein ausreichend großes lokales oder externes Sicherungsziel sowie Zugriff auf das E-Mail-Konto."));
            chapters.Add(new KeyValuePair<string,string>("02 · IMAP-Konto einrichten",
@"IMAP-KONTEN VERWALTEN
1. Im Bereich Konten auf Konto hinzufügen klicken.
2. E-Mail-Adresse, IMAP-Server, Port und Authentifizierung eintragen.
3. Automatische Servererkennung bei Bedarf verwenden und die vorgeschlagenen Daten überprüfen.
4. Passwort bzw. vom Provider vorgesehenes App-Kennwort hinterlegen.
5. Verbindung testen. Danach Konto als aktiv markieren.

Typischer IMAP-SSL-Port: 993. Die gespeicherten Kennwörter werden unter Windows benutzergebunden mit DPAPI geschützt. Ein fremder PC verfügt nicht automatisch über die Zugangsdaten.

WICHTIG
Die automatische Providererkennung garantiert keine erfolgreiche Anmeldung. Anbieter können OAuth oder App-Kennwörter verlangen. Die Anwendung unterstützt nicht alle OAuth-Verfahren."));
            chapters.Add(new KeyValuePair<string,string>("03 · Sicherung durchführen",
@"SICHERUNG
Sicherungsziel in der Hauptansicht auswählen.
Alle sichern: alle aktivierten Konten im Sicherungsumfang bearbeiten.
Auswahl sichern: gezielt mit dem markierten Konto arbeiten.
Abbrechen: den aktuellen Vorgang kontrolliert unterbrechen.

Die blaue Fortschrittsanzeige zeigt abgeschlossene Konten als Prozentwert. Der animierte Balken für das aktive Konto ist eine Aktivitätsanzeige; er ist KEINE Schätzung der noch zu übertragenden Datenmenge.

Während der Sicherung Netzverbindung, freien Festplattenplatz und Protokoll beachten. Eine erfolgreiche Ausführung zusätzlich über die Sicherungshistorie kontrollieren."));
            chapters.Add(new KeyValuePair<string,string>("04 · Zeitplan und Protokolle",
@"AUTOMATISCHE SICHERUNG
Unter Verwaltung -> Zeitplan einen Sicherungszeitpunkt definieren. Die Windows-Aufgabenplanung startet die Anwendung zum gewünschten Zeitpunkt. Das Konto benötigt die erforderlichen Berechtigungen.

Unter Verwaltung -> Konto-Historie oder Gesamt-Historie vorherige Sicherungsvorgänge prüfen.
Protokolle helfen bei Verbindungsproblemen, Zeitüberschreitungen, fehlendem Speicher oder Zugriffsfehlern.

DATENSCHUTZ
Protokolle können Ordnernamen und technische Kontodaten enthalten. Vor dem Weitergeben sensible Angaben prüfen."));
            chapters.Add(new KeyValuePair<string,string>("05 · Alte E-Mails archivieren",
@"ALTERSBASIERTE E-MAIL-ARCHIVIERUNG
1. Archiv & Ansicht -> E-Mail-Archiv öffnen.
2. Konto auswählen; standardmäßig gilt ein Alter von zehn Jahren.
3. Stichtag alternativ direkt wählen.
4. Vorschau ausführen: die einzelnen IMAP-Ordner werden geprüft.
5. Archiv auf einem ausreichend großen getrennten Laufwerk erstellen.
6. Vollständigkeit über Archiv prüfen kontrollieren.

Archivformat: vollständige EML-Nachrichten einschließlich Anhängen, manifest.json und Prüfsummen. Der Ordner trägt den Datumsbereich der enthaltenen Nachrichten, beispielsweise EmailArchiv von 2008.01.01 bis 2016.11.05_<Kennung>.

DATUMSGRUNDLAGE:
1. Internes Serverdatum (INTERNALDATE): bisherige Standardeinstellung.
2. E-Mail-Datum (Date-Header): erfasst auch alte, später importierte Nachrichten, falls der Mail-Header ein gültiges Datum enthält.
3. Beide vergleichen: nur Nachrichten auswählen, wenn BEIDE Datumsangaben vor dem Stichtag liegen. Widersprüche werden gezählt und nicht archiviert.

Pro Ordner protokolliert die Vorschau ausgewählte und geprüfte Nachrichten. Gesamtwerte zeigen fehlende/ungültige E-Mail-Daten, Abweichungen von mehr als einem Tag und widersprüchliche Altersbewertungen.

Fehlende, doppelte, unrealistische oder nicht eindeutig auswertbare Date-Header werden im Header- und Vergleichsmodus ausgeschlossen. Für den Vergleichsmodus gilt die vorsichtige Schnittmenge beider Auswahlen. Nachrichten AM Stichtag bleiben im Postfach. Ein neues Vorschau-Ergebnis wird notwendig, sobald die Datumsgrundlage geändert wird.

Bei der Archivprüfung werden Date-Header gegen das EML und die gewählte Auswahlart geprüft. Beim späteren Löschen bleiben die bestehenden Konten-, UIDVALIDITY-, SHA-256- und UID-EXPUNGE-Schutzprüfungen in Kraft.

POP3-Archive und lokale Thunderbird-Ordner werden NICHT über diese IMAP-Funktion gelesen. Ein Thunderbird-Ordner mit mailbox:// ist ein lokaler Ordner.

WICHTIG: Das Anlegen eines Archivs löscht auf dem Server nichts."));
            chapters.Add(new KeyValuePair<string,string>("06 · Postfach sicher bereinigen",
@"POSTFACHSPEICHER FREIGEBEN – RISIKO DES DATENVERLUSTS
1. Nur ein vollständiges Archiv verwenden und über Archiv prüfen kontrollieren.
2. Zweite unabhängig gespeicherte Archivkopie auf einem anderen Datenträger erstellen und prüfen.
3. Erst danach die Postfachbereinigung wählen.
4. Das richtige Konto und die Bestätigung exakt prüfen.
5. Die Bereinigung verwendet ausschließlich gezielte UID-Entfernung (UID EXPUNGE), soweit vom Server unterstützt.

Die Anwendung prüft die Kontozuordnung, UIDVALIDITY, lokale Prüfsummen und vor dem Entfernen die originalen Serverbytes. Bei fehlender UIDPLUS-Unterstützung wird gesperrt.

ACHTUNG: Das Löschen ist endgültig. Nach einem Abbruch können bereits einzelne Nachrichten entfernt worden sein. Reale IMAP-Provider können Quotas anders berechnen oder Nachrichten aufgrund von Labels/Retention weiterhin speichern.

Vor produktiver Verwendung zunächst mit einem separaten Testkonto ausprobieren. Bisher kein vollständiger Live-Test mit einem 10-GB-Postfach."));
            chapters.Add(new KeyValuePair<string,string>("07 · Archive wiederherstellen",
@"ARCHIV WIEDERHERSTELLEN
Im Archivfenster ein vorhandenes Archiv auswählen und Archiv prüfen durchführen.
Dann Archiv wiederherstellen wählen. Die Nachrichten werden in NEUE IMAP-Ordner importiert; die Originalordner werden nicht überschrieben.
Wiederholtes Importieren kann Duplikate erzeugen.

Einzelne EML-Nachrichten größer als 128 MiB können nicht direkt durch diese Archivwiederherstellung hochgeladen werden. Die lokalen EML-Dateien bleiben dennoch vollständig erhalten.

Die ältere MBOX-Wiederherstellung bleibt ein eigener Bereich und ist nicht identisch mit dem neuen EML-Archiv."));
            chapters.Add(new KeyValuePair<string,string>("08 · MBOX Viewer",
@"MBOX VIEWER
Archiv & Ansicht -> MBOX Viewer öffnen.
Eine vorhandene MBOX-Datei auswählen. Nachrichten über die Listen- und Suchfunktionen finden.
Soweit unterstützt: Anhänge anzeigen oder exportieren, einzelne Nachrichten als EML speichern und E-Mails drucken.

Der MBOX Viewer ist für MBOX-Dateien vorgesehen und kein vollständiger EML-Verzeichnisbrowser. Die neu erstellten EML-Archive besitzen eigene Manifest- und Prüffunktionen."));
            chapters.Add(new KeyValuePair<string,string>("09 · ZIP und Datensicherheit",
@"SICHERUNG PRÜFEN UND ZIP
Über Verwaltung -> Prüfen & ZIP vorhandene Backups prüfen sowie die verfügbare AES-256-Verschlüsselung verwenden.
ZIP-Kennwort unabhängig von E-Mail-Kennwörtern sicher aufbewahren; ohne Kennwort kann ein verschlüsseltes ZIP im Regelfall nicht geöffnet werden.

Ein Hashwert dient zur Erkennung unbeabsichtigter Beschädigung und ersetzt keine zweite Sicherung auf einem separaten Datenträger.

Das neue altersbasierte EML-Archiv ist zunächst NICHT automatisch verschlüsselt. Es muss auf einem geschützten oder verschlüsselten Datenträger gespeichert werden.

Backup-Regel: mindestens zwei voneinander unabhängige Kopien und eine regelmäßige echte Wiederherstellungsprobe."));
            chapters.Add(new KeyValuePair<string,string>("10 · Fehlerbehebung",
@"HÄUFIGE PROBLEME
ANMELDUNG FEHLGESCHLAGEN: Hostname, Port, TLS, App-Kennwort und Providerfreigabe kontrollieren.
SICHERUNG STARTET NICHT: Konto aktiv? Sicherungsziel beschreibbar? py.exe und runtime vorhanden?
ARCHIVVORSCHAU 0 NACHRICHTEN: Stichtag und INTERNALDATE prüfen. Thunderbird mailbox://-Ordner sind nicht auf dem IMAP-Server.
ARCHIV WIRD NICHT BEREINIGT: Vollständigkeit, Prüfsumme, richtige Kontozuordnung, UIDVALIDITY, UIDPLUS und zweite Kopie überprüfen.
ZU WENIG SPEICHER: anderen Datenträger verwenden; unvollständige Archive nicht als fertige Backups behandeln.
ZEITPLAN: Windows-Aufgabenplanung, Benutzerrechte und Protokolle prüfen.

Bei Fehlern zuerst die genaue Meldung und den Zeitpunkt dokumentieren, keine Zugangsdaten oder Mailinhalte veröffentlichen."));
            chapters.Add(new KeyValuePair<string,string>("11 · Über das Projekt",
@"CM IMAP SICHERUNG
Ersteller: C.Mihalik.
Projekt: https://github.com/ceromihalik-dev/CM-IMAP-Sicherung

Die Unterstützung des Projekts ist freiwillig und beeinflusst keine Programmfunktionen. Es werden keine Bank- oder PayPal-Zugangsdaten in der Anwendung benötigt.

Die Software einschließlich der neuen Archivbereinigung wurde noch nicht auf allen IMAP-Anbietern produktiv geprüft.
Version 1.4.0.4 verbessert die Bedienoberfläche und ergänzt diese integrierte Hilfe; sie ändert die bestehende Archivierungslogik nicht."));
            var outer=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(15)};
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,285));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Controls.Add(outer);
            search=new TextBox {Dock=DockStyle.Fill,Font=new Font("Segoe UI",11),Margin=new Padding(0,7,10,8)};
            search.TextChanged+=delegate {Filter();};
            outer.Controls.Add(search,0,0);
            var title=new Label {Text="📖  Benutzeranleitung  ·  F1",Dock=DockStyle.Fill,Font=new Font("Segoe UI",16,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
            outer.Controls.Add(title,1,0);
            var left=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
            left.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute,27));
            topics=new ListBox {Dock=DockStyle.Fill,IntegralHeight=false,Font=new Font("Segoe UI",10),BorderStyle=BorderStyle.None};
            topics.SelectedIndexChanged+=delegate {UpdateChapter();};
            count=new Label {Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};
            left.Controls.Add(topics,0,0);left.Controls.Add(count,0,1);
            outer.Controls.Add(left,0,1);
            body=new RichTextBox {Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11),DetectUrls=true};
            body.LinkClicked+=delegate(object s,LinkClickedEventArgs a) {if(a.LinkText.StartsWith("https://",StringComparison.OrdinalIgnoreCase))System.Diagnostics.Process.Start(a.LinkText);};
            outer.Controls.Add(body,1,1);
            WorkspaceLayout.Theme(this);
            ColorIcons.Apply(this);
            search.Text="";Filter();UpdateChapter();
            if(initial!=null){for(int i=0;i<topics.Items.Count;i++)if(topics.Items[i].ToString().IndexOf(initial,StringComparison.OrdinalIgnoreCase)>=0){topics.SelectedIndex=i;break;}}
            KeyDown+=delegate(object s,KeyEventArgs e) {if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}if(e.Control&&e.KeyCode==Keys.F){search.Focus();e.SuppressKeyPress=true;}};
        }
        private void Filter()
        {
            string needle=search.Text.Trim();
            string old=topics.SelectedItem as string;
            topics.BeginUpdate();topics.Items.Clear();
            foreach(var c in chapters)
                if(needle.Length==0 || c.Key.IndexOf(needle,StringComparison.OrdinalIgnoreCase)>=0
                    || c.Value.IndexOf(needle,StringComparison.OrdinalIgnoreCase)>=0)
                    topics.Items.Add(c.Key);
            topics.EndUpdate();
            count.Text=topics.Items.Count+" von "+chapters.Count+" Kapiteln";
            if(topics.Items.Count>0)topics.SelectedIndex=old==null?0:Math.Max(0,topics.Items.IndexOf(old));
            else body.Text="Keine Treffer. Anderen Suchbegriff verwenden.";
        }
        private void UpdateChapter()
        {
            string title=topics.SelectedItem as string;
            if(title==null)return;
            var item=chapters.First(x=>x.Key==title);
            body.Text=item.Key+Environment.NewLine+Environment.NewLine+item.Value;
            body.Select(0,item.Key.Length);body.SelectionFont=new Font("Segoe UI",16,FontStyle.Bold);
            body.Select(0,0);
        }
    }
}
