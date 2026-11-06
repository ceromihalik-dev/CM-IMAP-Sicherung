using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    // Adapter for the existing backup UI. Existing command handlers stay intact.
    internal static class WorkspaceLayout
    {
        private static readonly HashSet<Form> themed = new HashSet<Form>();
        private static bool hooked;
        private static T Field<T>(MainForm form, string name) where T : class
        {
            var info = typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if(info == null) throw new InvalidOperationException("UI-Feld fehlt: " + name);
            return info.GetValue(form) as T;
        }
        public static void Install(MainForm main)
        {
            main.SuspendLayout();
            var root = main.Controls.OfType<TableLayoutPanel>().First();
            var oldButtons = root.GetControlFromPosition(0, 6);
            root.Controls.Remove(oldButtons);
            root.RowCount = 6;
            if(root.RowStyles.Count > 6) root.RowStyles.RemoveAt(6);
            main.Controls.Remove(root);
            var outer = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Margin=Padding.Empty, Padding=Padding.Empty };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var side = new FlowLayoutPanel {Dock=DockStyle.Fill, AutoScroll=true, FlowDirection=FlowDirection.TopDown, WrapContents=false, BackColor=UiTheme.Surface, Padding=new Padding(10), Margin=Padding.Empty};
            outer.Controls.Add(side,0,0);outer.Controls.Add(root,1,0);main.Controls.Add(outer);
            side.Controls.Add(new Label { Text="ARBEITSBEREICHE", Width=190, Height=30, ForeColor=UiTheme.Muted, Font=new Font("Segoe UI",9,FontStyle.Bold) });
            var archive = new Button {Text="🗄  E-Mail-Archiv", Name="ArchiveCommand"};
            archive.Click += delegate {
                var worker=Field<BackgroundWorker>(main,"worker");
                if(worker!=null&&worker.IsBusy){MessageBox.Show(main,"Bitte den laufenden Sicherungsvorgang zuerst beenden.","E-Mail-Archiv");return;}
                var grid=Field<DataGridView>(main,"grid");
                var account=grid.SelectedRows.Count>0?grid.SelectedRows[0].Tag as AccountConfig:null;
                using(var dialog=new ArchiveForm(Field<AppConfig>(main,"config"),account)) dialog.ShowDialog(main);
            };
            var support=new Button {Text="❤️  Projekt unterstützen"};
            support.Click+=delegate {SupportLink.Show(main);};
            Group(side,"💾  Sicherung",true,
                Command(main,"btnBackupAll","💾  Alle sichern"),Command(main,"btnBackupSelected","📥  Auswahl sichern"),Command(main,"btnCancel","⏹  Abbrechen"));
            Group(side,"🗄  Archiv & Ansicht",true,archive,
                Command(main,"btnRestore","♻  Backup wiederherstellen"),Command(main,"btnMboxViewer","✉  MBOX Viewer"),Command(main,"btnOpenBackup","📂  Backup-Ordner"));
            Group(side,"👤  Konten",false,
                Command(main,"btnAdd","➕  Konto hinzufügen"),Command(main,"btnEdit","✏  Konto bearbeiten"),Command(main,"btnTest","🔌  Verbindung testen"),Command(main,"btnPassword","🔑  Kennwort ändern"),Command(main,"btnDelete","🗑  Konto entfernen"));
            Group(side,"⚙  Verwaltung",false,
                Command(main,"btnTools","🛠  Prüfen & ZIP"),Command(main,"btnSchedule","🕒  Zeitplan"),Command(main,"btnHistory","📋  Konto-Historie"),Command(main,"btnGlobalHistory","📚  Gesamt-Historie"),Command(main,"btnOpenLog","📄  Protokoll öffnen"),Command(main,"btnSettings","⚙  Einstellungen"));
            Group(side,"ℹ  Projekt",false,Command(main,"btnInfo","ℹ  Info"),support);
            if(oldButtons!=null)oldButtons.Dispose();
            foreach(var label in Descendants(root).OfType<Label>())
                if(label.Text=="Version 1.4.0.1")label.Text="Version 1.4.0.2";
            main.Text="CM IMAP Sicherung 1.4.0.2";
            main.MinimumSize=new Size(1050,700);
            var area=Screen.FromControl(main).WorkingArea;
            main.Size=new Size(Math.Min(1400,area.Width),Math.Min(900,area.Height));
            Theme(main);themed.Add(main);
            main.FormClosed+=delegate {themed.Remove(main);};
            main.ResumeLayout(true);
            if(!hooked){hooked=true;Application.Idle+=delegate {
                foreach(var form in Application.OpenForms.Cast<Form>().ToArray())
                    if(!themed.Contains(form)){Theme(form);themed.Add(form);form.FormClosed+=delegate {themed.Remove(form);};}
            };}
        }
        private static Button Command(MainForm main,string field,string text)
        {
            var button=Field<Button>(main,field);
            if(button==null)throw new InvalidOperationException("UI-Befehl fehlt: "+field);
            button.Text=text;return button;
        }
        private static void Group(FlowLayoutPanel parent,string title,bool expanded,params Button[] commands)
        {
            var group=new FlowLayoutPanel {Width=190,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,0,0,10)};
            var heading=new Button {Text=(expanded?"▾ ":"▸ ")+title,Width=190,Height=36,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9.5F,FontStyle.Bold)};
            UiTheme.ApplySecondary(heading);group.Controls.Add(heading);
            var body=new FlowLayoutPanel {Width=190,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty,Visible=expanded};
            foreach(var button in commands){button.Width=188;button.Height=37;button.Margin=new Padding(0,2,0,2);button.TextAlign=ContentAlignment.MiddleLeft;button.Padding=new Padding(8,0,0,0);button.AccessibleName=button.Text;body.Controls.Add(button);}
            heading.Click+=delegate {body.Visible=!body.Visible;heading.Text=(body.Visible?"▾ ":"▸ ")+title;};
            group.Controls.Add(body);parent.Controls.Add(group);
        }
        private static IEnumerable<Control> Descendants(Control parent)
        {
            foreach(Control child in parent.Controls){yield return child;foreach(var c in Descendants(child))yield return c;}
        }
        public static void Theme(Control root)
        {
            root.BackColor=UiTheme.Window;root.ForeColor=UiTheme.Text;
            foreach(var c in Descendants(root))
            {
                if(c is Button){var b=(Button)c;UiTheme.ApplySecondary(b);if(b.Text.Contains("Alle sichern")||b.Text.Contains("Vorschau"))UiTheme.ApplyPrimary(b);if(b.Text.Contains("bereinigen")||b.Text.Contains("entfernen"))UiTheme.ApplyDanger(b);continue;}
                if(c is DataGridView){var g=(DataGridView)c;g.EnableHeadersVisualStyles=false;g.BackgroundColor=UiTheme.Surface;g.GridColor=UiTheme.Border;g.ColumnHeadersDefaultCellStyle.BackColor=UiTheme.HeaderAlt;g.ColumnHeadersDefaultCellStyle.ForeColor=UiTheme.Text;g.DefaultCellStyle.BackColor=UiTheme.Surface;g.DefaultCellStyle.ForeColor=UiTheme.Text;g.DefaultCellStyle.SelectionBackColor=Color.FromArgb(4,57,94);g.DefaultCellStyle.SelectionForeColor=Color.White;g.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(42,42,43);g.CellFormatting+=GridColor;continue;}
                if(c is TextBoxBase){c.BackColor=UiTheme.LogBack;c.ForeColor=UiTheme.Text;continue;}
                if(c is ComboBox||c is NumericUpDown||c is ListBox||c is TreeView||c is ListView){c.BackColor=UiTheme.Surface;c.ForeColor=UiTheme.Text;continue;}
                if(c is Label){c.ForeColor=UiTheme.Text;if(c.BackColor!=Color.Transparent)c.BackColor=Color.Transparent;continue;}
                if(c is PictureBox||c is ProgressBar)continue;
                c.BackColor=UiTheme.Window;c.ForeColor=UiTheme.Text;
            }
        }
        private static void GridColor(object sender,DataGridViewCellFormattingEventArgs e)
        {
            if(e.RowIndex<0)return;
            var g=(DataGridView)sender;
            e.CellStyle.BackColor=e.RowIndex%2==0?UiTheme.Surface:Color.FromArgb(42,42,43);
            e.CellStyle.ForeColor=UiTheme.Text;e.CellStyle.SelectionBackColor=Color.FromArgb(4,57,94);e.CellStyle.SelectionForeColor=Color.White;
            if(g.Columns[e.ColumnIndex].Name=="Status"){
                var value=Convert.ToString(e.Value);
                if(value.StartsWith("Fehler"))e.CellStyle.ForeColor=UiTheme.Danger;
                else if(value.StartsWith("OK")||value.Contains("Verbindung OK"))e.CellStyle.ForeColor=UiTheme.Success;
                else if(value.Contains("Läuft")||value.Contains("Warte"))e.CellStyle.ForeColor=UiTheme.Warning;
            }
        }
    }
}
