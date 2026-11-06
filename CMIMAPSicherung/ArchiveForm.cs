using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public sealed class ArchiveForm : Form
    {
        private readonly AppConfig config;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly List<Button> operationButtons=new List<Button>();
        private ComboBox accounts;
        private NumericUpDown years;
        private DateTimePicker cutoff;
        private TextBox destination, source, log;
        private Label summary;
        private ProgressBar progress;
        private Button cancel;
        private BackgroundWorker worker;
        private string temp, cancelFile, planId, archiveId, manifestHash;
        private bool busy;
        public ArchiveForm(AppConfig config, AccountConfig selected)
        {
            this.config=config;
            Text="E-Mail-Archiv | CM IMAP Sicherung";
            StartPosition=FormStartPosition.CenterParent;
            Font=new Font("Segoe UI",9F);
            Size=new Size(1030,780);MinimumSize=new Size(900,680);
            Build();
            foreach(var a in config.Accounts)accounts.Items.Add(new AccountItem(a));
            if(accounts.Items.Count>0)accounts.SelectedIndex=0;
            for(int i=0;i<accounts.Items.Count;i++)if(((AccountItem)accounts.Items[i]).Account==selected)accounts.SelectedIndex=i;
            WorkspaceLayout.Theme(this);
            SmoothProgress.Replace(progress);
            ColorIcons.Apply(this);
            KeyPreview=true;
            KeyDown+=delegate(object sender,KeyEventArgs e) {if(e.KeyCode==Keys.F1){using(var h=new UserGuideForm("Alte E-Mails"))h.ShowDialog(this);e.Handled=true;}};
            worker=new BackgroundWorker();worker.WorkerReportsProgress=true;
            worker.DoWork+=Run;
            worker.ProgressChanged+=delegate(object sender,ProgressChangedEventArgs e){OnEvent(e.UserState as Dictionary<string,object>);};
            worker.RunWorkerCompleted+=Complete;
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;RequestCancel();MessageBox.Show(this,"Abbruch angefordert. Das Fenster bleibt bis zum sicheren Ende geöffnet.","Archivierung");}};
        }
        private void Build()
        {
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=6};
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            var title=new Label {Text="🗄  Alte E-Mails sicher archivieren",AutoSize=true,Font=new Font("Segoe UI",17,FontStyle.Bold),Margin=new Padding(0,0,0,12)};root.Controls.Add(title,0,0);
            var settings=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,Padding=new Padding(10)};
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125));root.Controls.Add(settings,0,1);
            LabelRow(settings,0,"IMAP-Konto");accounts=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};settings.Controls.Add(accounts,1,0);settings.SetColumnSpan(accounts,2);
            accounts.SelectedIndexChanged+=delegate {planId=null;archiveId=null;manifestHash=null;};
            LabelRow(settings,1,"Altersgrenze / Stichtag");
            var dates=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,WrapContents=false};
            years=new NumericUpDown {Minimum=1,Maximum=100,Value=10,Width=65};dates.Controls.Add(years);dates.Controls.Add(new Label {Text="Jahre – Nachrichten vor",AutoSize=true,Margin=new Padding(8,6,8,0)});
            cutoff=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy",Value=DateTime.Today.AddYears(-10),Width=130};dates.Controls.Add(cutoff);settings.Controls.Add(dates,1,1);settings.SetColumnSpan(dates,2);
            years.ValueChanged+=delegate {cutoff.Value=DateTime.Today.AddYears(-(int)years.Value);};cutoff.ValueChanged+=delegate {planId=null;};
            LabelRow(settings,2,"Neues Archiv speichern in");destination=new TextBox {Dock=DockStyle.Fill,Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"CM-EmailArchive")};settings.Controls.Add(destination,1,2);
            var chooseDest=Button("📂  Ziel wählen",delegate {Browse(destination,false);});settings.Controls.Add(chooseDest,2,2);
            LabelRow(settings,3,"Vorhandenes Archiv");source=new TextBox {Dock=DockStyle.Fill};settings.Controls.Add(source,1,3);source.TextChanged+=delegate {archiveId=null;manifestHash=null;};
            settings.Controls.Add(Button("📂  Öffnen",delegate {Browse(source,true);}),2,3);
            var note=new Label {AutoSize=true,MaximumSize=new Size(940,0),Text="Maßgeblich ist das IMAP-Eingangsdatum (INTERNALDATE). E-Mails am Stichtag bleiben erhalten. Das Archiv enthält vollständige EML-Dateien inklusive Anhängen. Beim Erstellen wird NICHTS aus dem Postfach gelöscht.\r\n\r\nSpeicher auf dem Mailserver wird erst durch die gesondert bestätigte Bereinigung frei. Eine zusätzliche Archivkopie auf einem anderen Datenträger ist dafür erforderlich.",Margin=new Padding(0,8,0,12)};
            root.Controls.Add(note,0,2);
            var commands=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
            commands.Controls.Add(Button("🔎  1. Vorschau",delegate {Start("preview");}));
            commands.Controls.Add(Button("💾  2. Archiv erstellen",delegate {Start("archive");}));
            commands.Controls.Add(Button("✅  3. Archiv prüfen",delegate {Start("verify");}));
            commands.Controls.Add(Button("🗑  4. Postfach bereinigen",delegate {Start("cleanup");}));
            commands.Controls.Add(Button("♻  Archiv wiederherstellen",delegate {Start("restore");}));
            commands.Controls.Add(Button("📂  Archivordner",delegate {if(Directory.Exists(source.Text))Process.Start(new ProcessStartInfo(source.Text){UseShellExecute=true});}));
            var help=new System.Windows.Forms.Button {Text="📖  Anleitung",AutoSize=true,Height=34,MinimumSize=new Size(125,34),Margin=new Padding(0,4,8,4)};
            help.Click+=delegate {using(var h=new UserGuideForm("Alte E-Mails"))h.ShowDialog(this);};
            commands.Controls.Add(help);
            root.Controls.Add(commands,0,3);
            log=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Font=new Font("Consolas",9)};root.Controls.Add(log,0,4);
            var footer=new TableLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            summary=new Label {Text="Bereit. Zuerst Vorschau ausführen.",AutoSize=true,Dock=DockStyle.Fill};footer.Controls.Add(summary,0,0);
            progress=new ProgressBar {Dock=DockStyle.Fill,Height=14};footer.Controls.Add(progress,0,1);
            cancel=new Button {Text="⏹  Abbrechen",Width=130,Height=34,Enabled=false};cancel.Click+=delegate {RequestCancel();};footer.Controls.Add(cancel,1,0);footer.SetRowSpan(cancel,2);root.Controls.Add(footer,0,5);
        }
        private Button Button(string text,Action action)
        {
            var b=new Button {Text=text,AutoSize=true,Height=34,MinimumSize=new Size(118,34),Margin=new Padding(0,4,8,4)};b.Click+=delegate {try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Archivierung",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};operationButtons.Add(b);return b;
        }
        private static void LabelRow(TableLayoutPanel panel,int row,string text){panel.Controls.Add(new Label {Text=text,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,7,10,7)},0,row);}
        private void Browse(TextBox box,bool existing)
        {
            using(var d=new FolderBrowserDialog {Description=existing?"Archiv mit manifest.json auswählen":"Separates Archivziel wählen",ShowNewFolderButton=!existing}){
                if(Directory.Exists(box.Text))d.SelectedPath=box.Text;
                if(d.ShowDialog(this)==DialogResult.OK)box.Text=d.SelectedPath;
            }
        }
        private AccountConfig Selected {get{var item=accounts.SelectedItem as AccountItem;return item==null?null:item.Account;}}
        private void Start(string operation)
        {
            if(busy)return;
            var a=Selected;
            if(operation!="verify"&&a==null)throw new InvalidOperationException("Bitte zuerst ein IMAP-Konto in der Kontenverwaltung einrichten.");
            if(operation=="archive"&&String.IsNullOrEmpty(planId))throw new InvalidOperationException("Bitte zuerst eine aktuelle Vorschau ausführen.");
            if((operation=="verify"||operation=="cleanup"||operation=="restore")&&!File.Exists(Path.Combine(source.Text,"manifest.json")))throw new InvalidOperationException("Bitte einen vollständigen E-Mail-Archivordner mit manifest.json auswählen.");
            if(operation=="cleanup"){
                if(String.IsNullOrEmpty(archiveId)||String.IsNullOrEmpty(manifestHash))throw new InvalidOperationException("Bitte das gewählte Archiv zuerst prüfen.");
                if(!ConfirmDeletion(a))return;
            }
            if(operation=="restore"&&MessageBox.Show(this,"Das Archiv wird in NEUE Ordner des Kontos "+a.User+" importiert. Vorhandene Nachrichten werden nicht gelöscht. Wiederholtes Importieren erzeugt weitere Kopien. Fortfahren?","Wiederherstellung",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            string password=operation=="verify"?null:ReadPassword(a);
            temp=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CM IMAP Sicherung","Operations",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);cancelFile=Path.Combine(temp,"cancel");
            string script=Path.Combine(temp,"archive_engine.py");
            using(var stream=typeof(ArchiveForm).Assembly.GetManifestResourceStream("CMIMAPSicherung.Archive.archive_engine.py"))
            using(var target=File.Create(script)){if(stream==null)throw new InvalidOperationException("Archiv-Engine fehlt.");stream.CopyTo(target);}
            var request=new Dictionary<string,object>{{"operation",operation},{"cutoff",cutoff.Value.ToString("yyyy-MM-dd")},{"destination",destination.Text},{"archive",source.Text},{"cancel_file",cancelFile},{"plan_id",planId},{"confirmation","LOESCHEN "+archiveId},{"manifest_sha256",manifestHash}};
            if(a!=null)request["account"]=new Dictionary<string,object>{{"host",a.Host},{"port",a.Port},{"user",a.User},{"timeout",config.ImapTimeoutSeconds}};
            if(password!=null)request["password"]=password;
            var serialized=json.Serialize(request);request.Clear();password=null;
            SetBusy(true);summary.Text="Vorgang läuft …";progress.Style=ProgressBarStyle.Marquee;
            worker.RunWorkerAsync(new string[]{script,serialized});
        }
        private string ReadPassword(AccountConfig a)
        {
            var baseDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IMAP-Backup-Secrets");
            var path=Path.GetFullPath(Path.Combine(baseDir,a.Id+".cred"));
            if(!path.StartsWith(baseDir+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Ungültige Konto-ID.");
            if(!File.Exists(path))throw new InvalidOperationException("Für dieses Konto fehlt ein gespeichertes Kennwort. Bitte unter Konten → Kennwort ändern hinterlegen.");
            var value=File.ReadAllText(path).Trim();
            if(value.Length%2!=0)throw new InvalidOperationException("Gespeichertes Kennwortformat ist ungültig.");
            byte[] encrypted=new byte[value.Length/2];
            for(int i=0;i<encrypted.Length;i++)encrypted[i]=Convert.ToByte(value.Substring(i*2,2),16);
            byte[] clear=ProtectedData.Unprotect(encrypted,null,DataProtectionScope.CurrentUser);
            try{return Encoding.Unicode.GetString(clear);}finally{Array.Clear(clear,0,clear.Length);}
        }
        private bool ConfirmDeletion(AccountConfig a)
        {
            using(var d=new Form {Text="Postfachbereinigung ausdrücklich bestätigen",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(650,300),Font=Font}){
                var layout=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false};d.Controls.Add(layout);
                layout.Controls.Add(new Label {AutoSize=true,MaximumSize=new Size(605,0),Text="Nur die vollständig archivierten Nachrichten werden endgültig vom Mailserver entfernt.\r\nKonto: "+a.User+"\r\nArchiv: "+archiveId+"\r\n\r\nZur Bestätigung die vollständige E-Mail-Adresse eingeben:",Margin=new Padding(0,0,0,10)});
                var entry=new TextBox {Width=590};layout.Controls.Add(entry);
                var copy=new CheckBox {AutoSize=true,MaximumSize=new Size(590,0),Text="Ich habe eine zusätzliche, geprüfte Kopie dieses Archivs auf einem anderen Datenträger.",Margin=new Padding(0,10,0,10)};layout.Controls.Add(copy);
                var buttons=new FlowLayoutPanel {AutoSize=true};var no=new Button {Text="Abbrechen",DialogResult=DialogResult.Cancel,Width=120};var yes=new Button {Text="Endgültig bereinigen",Width=180,Enabled=false};
                EventHandler update=delegate {yes.Enabled=copy.Checked&&entry.Text==a.User;};entry.TextChanged+=update;copy.CheckedChanged+=update;yes.Click+=delegate {d.DialogResult=DialogResult.OK;};buttons.Controls.Add(no);buttons.Controls.Add(yes);layout.Controls.Add(buttons);d.CancelButton=no;d.AcceptButton=no;WorkspaceLayout.Theme(d);UiTheme.ApplyDanger(yes);return d.ShowDialog(this)==DialogResult.OK;
            }
        }
        private void Run(object sender,DoWorkEventArgs e)
        {
            var data=(string[])e.Argument;
            var exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"py.exe");
            if(!File.Exists(exe))exe="py.exe";
            var psi=new ProcessStartInfo(exe,"-3 -u \""+data[0]+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            psi.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";
            using(var p=new Process {StartInfo=psi}){
                p.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs line){if(line.Data!=null)worker.ReportProgress(0,new Dictionary<string,object>{{"event","error"},{"message","Archiv-Engine meldet einen Laufzeitfehler. Vorgang nicht freigegeben."}});};
                p.Start();p.BeginErrorReadLine();p.StandardInput.WriteLine(data[1]);p.StandardInput.Close();data[1]=null;
                string line;while((line=p.StandardOutput.ReadLine())!=null){try{var entry=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line);worker.ReportProgress(0,entry);}catch(ArgumentException){worker.ReportProgress(0,new Dictionary<string,object>{{"event","error"},{"message","Unerwartete Antwort der Archiv-Engine."}});}}
                p.WaitForExit();e.Result=p.ExitCode;
            }
        }
        private void OnEvent(Dictionary<string,object> data)
        {
            if(data==null)return;
            var kind=Convert.ToString(data["event"]);
            if(kind=="archive_path"){source.Text=Convert.ToString(data["path"]);}
            if(data.ContainsKey("message")){var message=Convert.ToString(data["message"]);summary.Text=message;if(log.TextLength>120000){log.Select(0,40000);log.SelectedText="";}log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine);}
            if(data.ContainsKey("total")&&data.ContainsKey("count")){
                progress.Style=ProgressBarStyle.Continuous;progress.Maximum=Math.Max(1,Convert.ToInt32(data["total"]));progress.Value=Math.Min(progress.Maximum,Convert.ToInt32(data["count"]));
            }
            if(kind=="result"){
                if(data.ContainsKey("plan_id")){planId=Convert.ToString(data["plan_id"]);summary.Text=String.Format("Vorschau: {0} Nachrichten | {1:N2} GiB | Zeitraum {2} bis {3}",data["count"],Convert.ToDouble(data["bytes"])/(1024*1024*1024),data["first"],data["last"]);log.AppendText(summary.Text+Environment.NewLine);}
                if(data.ContainsKey("manifest_sha256")){manifestHash=Convert.ToString(data["manifest_sha256"]);archiveId=Convert.ToString(data["archive_id"]);}
            }
        }
        private void Complete(object sender,RunWorkerCompletedEventArgs e)
        {
            progress.Style=ProgressBarStyle.Continuous;SetBusy(false);
            if(e.Error!=null){summary.Text="Fehler: "+e.Error.Message;log.AppendText(summary.Text+Environment.NewLine);}
            else if(Convert.ToInt32(e.Result)!=0){summary.Text="Vorgang nicht abgeschlossen – Einzelheiten im Protokoll.";manifestHash=null;archiveId=null;}
            else if(progress.Maximum>0)progress.Value=progress.Maximum;
            try{if(Directory.Exists(temp))Directory.Delete(temp,true);}catch(IOException){}
        }
        private void RequestCancel(){if(busy&&!String.IsNullOrEmpty(cancelFile)){File.WriteAllText(cancelFile,"cancel");summary.Text="Abbruch angefordert; laufende Serverantwort wird noch verarbeitet …";cancel.Enabled=false;}}
        private void SetBusy(bool value){busy=value;foreach(var b in operationButtons)b.Enabled=!value;accounts.Enabled=!value;years.Enabled=!value;cutoff.Enabled=!value;destination.Enabled=!value;source.Enabled=!value;cancel.Enabled=value;}
        private sealed class AccountItem {public readonly AccountConfig Account;public AccountItem(AccountConfig a){Account=a;}public override string ToString(){return Account.Name+"  ("+Account.User+")";}}
    }
}
