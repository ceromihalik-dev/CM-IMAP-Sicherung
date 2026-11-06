using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    /// <summary>
    /// Presentation-only shell over MainForm. The existing backup/restore commands,
    /// worker, config, password storage and archive safety rules remain untouched.
    /// </summary>
    internal sealed class DashboardEnhancements : IDisposable
    {
        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,string text);
        private const int EM_SETCUEBANNER=0x1501;
        private readonly MainForm main;
        private readonly TableLayoutPanel root;
        private readonly TableLayoutPanel outer;
        private readonly Action openArchive;
        private readonly DataGridView grid;
        private readonly AppConfig config;
        private readonly BackgroundWorker worker;
        private readonly TextBox log;
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer filterDelay = new Timer { Interval = 130 };
        private readonly List<Control> statusCards = new List<Control>();
        private TextBox search;
        private ComboBox filter;
        private Label visibleCount;
        private Label backupNotice;
        private Label logTitle;
        private TableLayoutPanel logPanel;
        private Button logToggle;
        private CheckBox showUserColumn;
        private ToolStripMenuItem logMenu;
        private bool expandedLog;
        private bool filtering;
        private int preferredLogHeight = 210;

        public DashboardEnhancements(MainForm main, TableLayoutPanel root, TableLayoutPanel outer, Action openArchive)
        {
            this.main=main;
            this.root=root;
            this.outer=outer;
            this.openArchive=openArchive;
            grid=Field<DataGridView>("grid");
            config=Field<AppConfig>("config");
            worker=Field<BackgroundWorker>("worker");
            log=Field<TextBox>("txtLog");
        }

        private T Field<T>(string name) where T:class
        {
            FieldInfo f=typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            if(f==null)throw new InvalidOperationException("Dashboard-Feld fehlt: "+name);
            T value=f.GetValue(main) as T;
            if(value==null)throw new InvalidOperationException("Dashboard-Feld nicht initialisiert: "+name);
            return value;
        }

        private void Call(string name, params object[] args)
        {
            MethodInfo method=typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic);
            if(method==null)throw new InvalidOperationException("Befehl fehlt: "+name);
            try {method.Invoke(main,args);}
            catch(TargetInvocationException ex)
            {
                MessageBox.Show(main,(ex.InnerException??ex).Message,"CM IMAP Sicherung",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        }

        private Button ActionButton(string label, Action action, Color? accent=null)
        {
            Button b=new Button {
                Text=label,Height=38,MinimumSize=new Size(104,36),
                Dock=DockStyle.Fill,Margin=new Padding(3,5,3,5),
                Font=new Font("Segoe UI",9.3f,FontStyle.Bold),
                FlatStyle=FlatStyle.Flat,UseMnemonic=false
            };
            b.FlatAppearance.BorderSize=1;
            b.FlatAppearance.BorderColor=UiTheme.Border;
            b.BackColor=accent??UiTheme.Surface;
            b.ForeColor=Color.FromArgb(238,239,245);
            b.Cursor=Cursors.Hand;
            b.Click+=delegate {action();};
            return b;
        }

        private void BindBusy(Button target,string sourceName)
        {
            Button source=Field<Button>(sourceName);
            target.Enabled=source.Enabled;
            source.EnabledChanged+=delegate {if(!target.IsDisposed)target.Enabled=source.Enabled;};
        }

        private void BuildToolBar()
        {
            var right=new TableLayoutPanel {
                Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=Padding.Empty,
                ColumnCount=1,RowCount=2,BackColor=UiTheme.Window
            };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
            right.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var toolbar=new TableLayoutPanel {
                Name="PrimaryActionBar",ColumnCount=5,RowCount=1,Dock=DockStyle.Fill,
                Padding=new Padding(8,2,8,2),Margin=Padding.Empty,BackColor=UiTheme.Header
            };
            for(int i=0;i<5;i++)toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent,100));

            var all=ActionButton("💾  Alle sichern",()=>Call("StartBackup",false),UiTheme.Primary);
            var selected=ActionButton("📥  Auswahl sichern",()=>Call("StartBackup",true));
            var archive=ActionButton("🗄  E-Mail-Archiv",()=>openArchive());
            var refresh=ActionButton("♻  Aktualisieren",()=>Call("RefreshGrid"));
            var cancel=ActionButton("⏹  Abbrechen",()=>Call("BtnCancel_Click",main,EventArgs.Empty));
            BindBusy(all,"btnBackupAll");
            BindBusy(selected,"btnBackupSelected");
            BindBusy(archive,"btnRestore");
            BindBusy(refresh,"btnBrowse");
            BindBusy(cancel,"btnCancel");
            toolbar.Controls.Add(all,0,0);
            toolbar.Controls.Add(selected,1,0);
            toolbar.Controls.Add(archive,2,0);
            toolbar.Controls.Add(refresh,3,0);
            toolbar.Controls.Add(cancel,4,0);
            right.Controls.Add(toolbar,0,0);
            outer.Controls.Remove(root);
            right.Controls.Add(root,0,1);
            outer.Controls.Add(right,1,0);
        }

        private void OpenGuide()
        {
            using(var help=new UserGuideForm())help.ShowDialog(main);
        }

        private ToolStripMenuItem Item(string label,Action action)
        {
            var item=new ToolStripMenuItem(label);
            item.Click+=delegate {action();};
            return item;
        }

        private void BuildMenu()
        {
            // A real menu strip remains accessible even if every sidebar group is collapsed.
            var strip=new MenuStrip {
                Name="MainTopMenu",Dock=DockStyle.Fill,
                BackColor=UiTheme.HeaderAlt,ForeColor=UiTheme.Text,
                Font=new Font("Segoe UI",9.4f),Padding=new Padding(12,2,6,2),
                RenderMode=ToolStripRenderMode.Professional
            };
            strip.Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors());
            var file=new ToolStripMenuItem("Datei");
            file.DropDownItems.Add(Item("Backup-Ordner öffnen",()=>Call("BtnOpenBackup_Click",main,EventArgs.Empty)));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Item("Beenden",()=>Call("ExitApplication")));
            var actions=new ToolStripMenuItem("Aktionen");
            actions.DropDownItems.Add(Item("Alle aktiven Konten sichern",()=>Call("StartBackup",false)));
            actions.DropDownItems.Add(Item("Auswahl sichern",()=>Call("StartBackup",true)));
            actions.DropDownItems.Add(Item("E-Mail-Archiv öffnen",()=>openArchive()));
            actions.DropDownItems.Add(Item("Kontenliste aktualisieren",()=>Call("RefreshGrid")));
            var view=new ToolStripMenuItem("Ansicht");
            logMenu=new ToolStripMenuItem("Protokoll anzeigen") {CheckOnClick=true,Checked=false};
            logMenu.CheckedChanged+=delegate {SetLogExpanded(logMenu.Checked);};
            view.DropDownItems.Add(logMenu);
            showUserColumn=new CheckBox {Checked=false};
            var user=new ToolStripMenuItem("Benutzer-Spalte anzeigen") {CheckOnClick=true};
            user.CheckedChanged+=delegate {showUserColumn.Checked=user.Checked;UpdateUserColumn();};
            view.DropDownItems.Add(user);
            var helpMenu=new ToolStripMenuItem("Hilfe") {Name="HelpMenu"};
            var guide=Item("📖  Benutzeranleitung  (F1)",OpenGuide);
            guide.Name="HelpGuideCommand";
            helpMenu.DropDownItems.Add(guide);
            helpMenu.DropDownItems.Add(Item("Über CM IMAP Sicherung",()=>Call("ShowInfo")));
            strip.Items.Add(file);strip.Items.Add(actions);strip.Items.Add(view);strip.Items.Add(helpMenu);
            var shell=new TableLayoutPanel {Name="MainWorkspaceShell",Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=Padding.Empty,RowCount=2,ColumnCount=1};
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            main.Controls.Remove(outer);
            shell.Controls.Add(strip,0,0);
            shell.Controls.Add(outer,0,1);
            main.Controls.Add(shell);
            main.MainMenuStrip=strip;
        }

        private void SetCards()
        {
            var old=root.GetControlFromPosition(0,1) as FlowLayoutPanel;
            if(old==null)throw new InvalidOperationException("Statuskarten wurden nicht gefunden.");
            var cards=old.Controls.Cast<Panel>().ToArray();
            if(cards.Length!=4)throw new InvalidOperationException("Es werden vier Statuskarten benötigt.");
            var titles=new [] {"Konten","Vorgänge","Speicher","Zeitplan"};
            var colors=new [] {Color.FromArgb(80,160,245),Color.FromArgb(112,207,156),
                              Color.FromArgb(193,144,248),Color.FromArgb(245,189,107)};
            var deck=new TableLayoutPanel {
                Name="DashboardCards",Dock=DockStyle.Top,MinimumSize=new Size(0,90),
                Height=90,Margin=new Padding(0,0,0,9),Padding=Padding.Empty,
                ColumnCount=4,RowCount=1,BackColor=UiTheme.Window
            };
            deck.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            for(int i=0;i<4;i++)deck.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            for(int i=0;i<cards.Length;i++)
            {
                var card=cards[i];
                card.Dock=DockStyle.Fill;
                card.MinimumSize=Size.Empty;
                card.Margin=new Padding(0,1,i==3?0:9,3);
                card.BackColor=UiTheme.Surface;
                card.BorderStyle=BorderStyle.FixedSingle;
                card.Padding=new Padding(12,8,10,8);
                foreach(Control label in card.Controls)
                {
                    var l=label as Label;
                    if(l==null)continue;
                    int originalY=l.Location.Y;
                    l.Location=new Point(19,originalY);
                    l.AutoSize=false;
                    l.Width=Math.Max(100,card.ClientSize.Width-42);
                    l.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
                    l.AutoEllipsis=true;
                    if(originalY<25)
                    {
                        l.Text=titles[i];l.ForeColor=UiTheme.Muted;
                        l.Font=new Font("Segoe UI",8.5f);l.Height=22;
                    }
                    else
                    {
                        l.ForeColor=Color.White;
                        l.Font=new Font("Segoe UI",10f,FontStyle.Bold);l.Height=34;
                    }
                }
                var stripe=new Panel{Dock=DockStyle.Left,Width=3,BackColor=colors[i]};
                card.Controls.Add(stripe);stripe.SendToBack();
                var dot=new Label{Text="●",ForeColor=colors[i],AutoSize=true,Anchor=AnchorStyles.Top|AnchorStyles.Right,BackColor=Color.Transparent,Font=new Font("Segoe UI",12f),Location=new Point(168,6)};
                card.Controls.Add(dot);
                statusCards.Add(card);
                deck.Controls.Add(card,i,0);
            }
            root.Controls.Remove(old);
            root.Controls.Add(deck,0,1);
            old.Dispose();
            tips.SetToolTip(cards[1],"Letzte protokollierte Aktion je Konto – kann ein Verbindungstest sein; kein Nachweis für eine vollständige Sicherung.");
            tips.SetToolTip(cards[2],"Lokale Größe im gewählten Sicherungsziel. 0 B bedeutet: dort wurden keine Daten gezählt.");
        }

        private void UpdateUserColumn()
        {
            var col=grid.Columns["User"];
            if(col==null)return;
            bool namesDiffer=config.Accounts.Any(a=>!String.Equals(a.Name,a.User,StringComparison.OrdinalIgnoreCase));
            col.Visible=(showUserColumn!=null&&showUserColumn.Checked)||namesDiffer;
        }

        private void SetGrid()
        {
            grid.RowTemplate.Height=34;
            grid.ColumnHeadersHeight=38;
            grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.AllowUserToOrderColumns=true;
            grid.AllowUserToResizeColumns=true;
            grid.BorderStyle=BorderStyle.None;
            grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
            grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            grid.ShowCellToolTips=true;
            grid.DefaultCellStyle.Padding=new Padding(5,1,3,1);
            grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(5,2,2,2);
            grid.Columns["Name"].FillWeight=205;
            grid.Columns["User"].FillWeight=170;
            grid.Columns["Host"].FillWeight=100;
            grid.Columns["Port"].FillWeight=46;
            grid.Columns["Enabled"].FillWeight=48;
            grid.Columns["Last"].FillWeight=121;
            grid.Columns["Size"].FillWeight=75;
            grid.Columns["Status"].FillWeight=106;
            grid.Columns["Size"].HeaderText="Lokal";
            grid.Columns["Last"].HeaderText="Letztes Backup";
            grid.Columns["Status"].HeaderText="Letzter Vorgang";
            grid.Columns["Enabled"].SortMode=DataGridViewColumnSortMode.NotSortable;
            foreach(DataGridViewColumn c in grid.Columns)
                if(c.Name!="Enabled")c.SortMode=DataGridViewColumnSortMode.Automatic;
            grid.SortCompare+=delegate(object sender,DataGridViewSortCompareEventArgs e) {
                if(e.Column.Name!="Last")return;
                DateTime a,b;
                DateTime.TryParse(Convert.ToString(e.CellValue1),new CultureInfo("de-DE"),DateTimeStyles.None,out a);
                DateTime.TryParse(Convert.ToString(e.CellValue2),new CultureInfo("de-DE"),DateTimeStyles.None,out b);
                e.SortResult=a.CompareTo(b);
                e.Handled=true;
            };
            grid.CellToolTipTextNeeded+=delegate(object sender,DataGridViewCellToolTipTextNeededEventArgs e) {
                if(e.RowIndex<0||e.ColumnIndex<0||e.RowIndex>=grid.Rows.Count)return;
                var a=grid.Rows[e.RowIndex].Tag as AccountConfig;
                if(a==null)return;
                if(grid.Columns[e.ColumnIndex].Name=="Name")e.ToolTipText=a.Name+"\n"+a.User;
                if(grid.Columns[e.ColumnIndex].Name=="Status")e.ToolTipText="Letzter protokollierter Vorgang. OK ist kein Sicherungsnachweis.";
                if(grid.Columns[e.ColumnIndex].Name=="Size")e.ToolTipText="Gemessene lokale Datenmenge im aktuellen Sicherungsziel.";
            };
            grid.CellFormatting+=delegate(object sender,DataGridViewCellFormattingEventArgs e) {
                if(e.RowIndex<0||e.RowIndex>=grid.Rows.Count)return;
                string n=grid.Columns[e.ColumnIndex].Name;
                string value=Convert.ToString(e.Value)??"";
                if(n=="Size"&&value=="0 B")e.CellStyle.ForeColor=UiTheme.Warning;
                if(n=="Status")
                {
                    if(value.StartsWith("OK",StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.ForeColor=UiTheme.Success;
                        var account=grid.Rows[e.RowIndex].Tag as AccountConfig;
                        var history=account!=null?account.History:null;
                        if(history!=null&&history.Count>0)
                        {
                            string mode=history[history.Count-1].Mode??"";
                            e.Value=mode=="Verbindungstest"?"Verbindung OK":mode=="Sicherung"?"Sicherung OK":"Vorgang OK";
                            e.FormattingApplied=true;
                        }
                    }
                    else if(value.StartsWith("Fehler",StringComparison.OrdinalIgnoreCase))e.CellStyle.ForeColor=UiTheme.Danger;
                    else if(value.Contains("Läuft")||value.Contains("Warte"))e.CellStyle.ForeColor=UiTheme.Warning;
                }
            };
            foreach(DataGridViewRow r in grid.Rows)r.Height=34;
            UpdateUserColumn();
            var pane=new TableLayoutPanel {
                Name="AccountGridPanel",Dock=DockStyle.Fill,
                ColumnCount=1,RowCount=3,Padding=new Padding(0),Margin=new Padding(0,4,0,0),
                BackColor=UiTheme.Surface
            };
            pane.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            pane.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            pane.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
            pane.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var header=new TableLayoutPanel {
                Dock=DockStyle.Fill,RowCount=1,ColumnCount=4,Margin=Padding.Empty,
                Padding=new Padding(10,4,10,3),BackColor=UiTheme.Header
            };
            header.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,157));
            header.Controls.Add(new Label {Text="E-Mail-Konten",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,
                Font=new Font("Segoe UI",11f,FontStyle.Bold),ForeColor=Color.White},0,0);
            visibleCount=new Label {Text="",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=UiTheme.Muted};
            header.Controls.Add(visibleCount,1,0);
            search=new TextBox {Name="AccountSearch",Dock=DockStyle.Fill,Margin=new Padding(5,7,10,7),
                Font=new Font("Segoe UI",9.6f),BackColor=UiTheme.LogBack,ForeColor=UiTheme.Text};
            tips.SetToolTip(search,"Konto, E-Mail-Adresse oder Server durchsuchen. Strg+F fokussiert dieses Feld.");
            search.HandleCreated+=delegate {
                SendMessage(search.Handle,EM_SETCUEBANNER,IntPtr.Zero,"Konto oder Server suchen …");
            };
            header.Controls.Add(search,2,0);
            filter=new ComboBox {Name="AccountFilter",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,
                Margin=new Padding(0,6,0,5),Font=new Font("Segoe UI",9.2f),
                BackColor=UiTheme.Surface,ForeColor=UiTheme.Text};
            filter.Items.AddRange(new object[]{"Alle Konten","Nur aktive","Letzte Fehler","Lokal 0 B"});
            filter.SelectedIndex=0;
            header.Controls.Add(filter,3,0);
            pane.Controls.Add(header,0,0);
            root.Controls.Remove(grid);
            grid.Dock=DockStyle.Fill;
            grid.Margin=new Padding(0,0,0,0);
            pane.Controls.Add(grid,0,1);
            backupNotice=new Label{Name="BackupStatusNotice",Dock=DockStyle.Fill,Margin=Padding.Empty,
                Padding=new Padding(12,5,8,4),BackColor=UiTheme.Surface,
                ForeColor=UiTheme.Muted,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
            pane.Controls.Add(backupNotice,0,2);
            root.Controls.Add(pane,0,3);
            search.TextChanged+=delegate {RequestFilter();};
            filter.SelectedIndexChanged+=delegate {RequestFilter();};
            filterDelay.Tick+=delegate {filterDelay.Stop();ApplyFilter();};
            grid.RowsAdded+=delegate {RequestFilter();};
            grid.RowsRemoved+=delegate {RequestFilter();};
            grid.CellValueChanged+=delegate {RequestFilter();};
            grid.Sorted+=delegate {RequestFilter();};
            ApplyFilter();
        }

        private void RequestFilter()
        {
            if(filtering)return;
            filterDelay.Stop();
            filterDelay.Start();
        }

        private void ApplyFilter()
        {
            if(filtering||main.IsDisposed)return;
            filtering=true;
            try
            {
                string query=(search.Text??"").Trim();
                int mode=filter.SelectedIndex;
                var chosen=grid.SelectedRows.Count>0?grid.SelectedRows[0].Tag as AccountConfig:null;
                grid.ClearSelection();
                try {grid.CurrentCell=null;}catch(InvalidOperationException){}
                int count=0,zero=0;
                bool selectedShown=false;
                foreach(DataGridViewRow row in grid.Rows)
                {
                    var a=row.Tag as AccountConfig;
                    if(a==null||row.IsNewRow)continue;
                    string size=Convert.ToString(row.Cells["Size"].Value)??"";
                    string status=Convert.ToString(row.Cells["Status"].Value)??"";
                    if(size=="0 B")zero++;
                    bool match=String.IsNullOrEmpty(query) ||
                        (a.Name??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 ||
                        (a.User??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 ||
                        (a.Host??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
                    if(mode==1)match&=a.Enabled;
                    if(mode==2)match&=status.StartsWith("Fehler",StringComparison.OrdinalIgnoreCase);
                    if(mode==3)match&=size=="0 B";
                    if(row.Visible!=match)row.Visible=match;
                    if(match)
                    {
                        count++;
                        if(Object.ReferenceEquals(chosen,a))
                        {
                            row.Selected=true;
                            selectedShown=true;
                        }
                    }
                }
                if(!selectedShown)
                {
                    var first=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>r.Visible);
                    if(first!=null)first.Selected=true;
                }
                visibleCount.Text=String.Format("{0} / {1}",count,grid.Rows.Count);
                if(grid.Rows.Count==0)
                {
                    backupNotice.Text="Noch keine IMAP-Konten eingerichtet. Über Konten → Konto hinzufügen starten.";
                    backupNotice.ForeColor=UiTheme.Muted;
                }
                else if(zero>0)
                {
                    backupNotice.ForeColor=UiTheme.Warning;
                    backupNotice.Text=String.Format("Hinweis: Bei {0} von {1} Konten sind im gewählten Sicherungsziel 0 B erfasst. „OK“ kann auch einen Verbindungstest bezeichnen.",
                        zero,grid.Rows.Count);
                }
                else
                {
                    backupNotice.ForeColor=UiTheme.Muted;
                    backupNotice.Text="Letzte Vorgänge sind keine automatische Integritätsprüfung. Sicherungen regelmäßig kontrollieren und testweise wiederherstellen.";
                }
                UpdateUserColumn();
            }
            catch(InvalidOperationException)
            {
                // Grid can be temporarily rebuilding its rows; request another pass.
                filterDelay.Stop();
                filterDelay.Start();
            }
            finally {filtering=false;}
        }

        private void BuildLog()
        {
            var current=root.GetControlFromPosition(0,5);
            if(!Object.ReferenceEquals(current,log))throw new InvalidOperationException("Protokollbereich fehlt.");
            root.RowStyles[3].SizeType=SizeType.Percent;
            root.RowStyles[3].Height=100;
            root.RowStyles[5].SizeType=SizeType.Absolute;
            root.RowStyles[5].Height=36;
            logPanel=new TableLayoutPanel {
                Name="DockedLogPanel",Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,
                Padding=Padding.Empty,Margin=new Padding(0,5,0,0),
                BackColor=UiTheme.Header
            };
            logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            logPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var row=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,
                Padding=Padding.Empty,Margin=Padding.Empty,BackColor=UiTheme.Header};
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));
            row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            logToggle=new Button {
                Name="ToggleLog",Dock=DockStyle.Fill,Text="▸  Protokoll",
                FlatStyle=FlatStyle.Flat,ForeColor=UiTheme.Text,BackColor=UiTheme.Header,
                TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty
            };
            logToggle.FlatAppearance.BorderSize=0;
            logToggle.Click+=delegate {SetLogExpanded(!expandedLog);};
            row.Controls.Add(logToggle,0,0);
            logTitle=new Label {Text="Bereit · Protokoll ausklappen für Details",Dock=DockStyle.Fill,
                ForeColor=UiTheme.Muted,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
            row.Controls.Add(logTitle,1,0);
            var bigger=new Button {Text="▣  Größer / kleiner",Dock=DockStyle.Fill,
                FlatStyle=FlatStyle.Flat,ForeColor=UiTheme.Muted,BackColor=UiTheme.Header,
                Margin=Padding.Empty};
            bigger.FlatAppearance.BorderSize=0;
            bigger.Click+=delegate {preferredLogHeight=preferredLogHeight==210?292:210;if(expandedLog)SetLogExpanded(true);};
            row.Controls.Add(bigger,2,0);
            logPanel.Controls.Add(row,0,0);
            root.Controls.Remove(log);
            log.Dock=DockStyle.Fill;
            log.Margin=Padding.Empty;
            logPanel.Controls.Add(log,0,1);
            root.Controls.Add(logPanel,0,5);
            log.Visible=false;
            log.TextChanged+=delegate {
                if(log.IsDisposed)return;
                string s=log.Text;
                if(!String.IsNullOrEmpty(s))
                {
                    string tail=s.Substring(Math.Max(0,s.Length-250));
                    logTitle.Text=tail.Split(new [] {'\r','\n'},StringSplitOptions.RemoveEmptyEntries).LastOrDefault()??"Protokoll";
                    if((worker!=null&&worker.IsBusy||tail.IndexOf("Fehler",StringComparison.OrdinalIgnoreCase)>=0)&&!expandedLog)
                        SetLogExpanded(true);
                }
            };
        }

        private void SetLogExpanded(bool value)
        {
            if(logPanel==null)return;
            expandedLog=value;
            root.RowStyles[5].Height=value?preferredLogHeight:36;
            log.Visible=value;
            logToggle.Text=value?"▾  Protokoll schließen":"▸  Protokoll";
            if(logMenu!=null&&logMenu.Checked!=value)logMenu.Checked=value;
            root.PerformLayout();
        }

        private void ImproveHeader()
        {
            var header=root.GetControlFromPosition(0,0) as TableLayoutPanel;
            if(header==null)return;
            header.Padding=new Padding(13,9,13,8);
            header.Margin=new Padding(0,0,0,8);
            var logo=Field<PictureBox>("picLogo");
            logo.Size=new Size(65,65);
            logo.Margin=new Padding(0,0,13,0);
            foreach(var label in WorkspaceLayout.Descendants(header).OfType<Label>())
            {
                if(label.Text=="Version 1.4.0.4"||label.Text=="Version 1.4.0.3"||label.Text=="Version 1.4.0.2")label.Text="Version 1.4.0.5";
                if(label.Text=="CM IMAP Sicherung")label.Font=new Font("Segoe UI",17f,FontStyle.Bold);
            }
        }

        public void Install()
        {
            main.SuspendLayout();
            ImproveHeader();
            BuildToolBar();
            SetCards();
            SetGrid();
            BuildLog();
            BuildMenu();
            main.MinimumSize=new Size(1050,690);
            main.Text="CM IMAP Sicherung 1.4.0.5";
            // Unlike auto-launch or background jobs, the ordinary window opens normally.
            main.KeyPreview=true;
            main.KeyDown+=delegate(object sender,KeyEventArgs e) {
                if(e.Control&&e.KeyCode==Keys.F){search.Focus();e.SuppressKeyPress=true;}
            };
            filterDelay.Start();
            main.FormClosed+=delegate {Dispose();};
            main.ResumeLayout(true);
        }

        public void Dispose()
        {
            filterDelay.Stop();filterDelay.Dispose();tips.Dispose();
        }

        private sealed class DarkMenuColors:ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground {get{return UiTheme.Surface;}}
            public override Color MenuItemSelected {get{return Color.FromArgb(51,67,86);}}
            public override Color MenuItemBorder {get{return UiTheme.Border;}}
            public override Color ImageMarginGradientBegin {get{return UiTheme.Surface;}}
            public override Color ImageMarginGradientMiddle {get{return UiTheme.Surface;}}
            public override Color ImageMarginGradientEnd {get{return UiTheme.Surface;}}
            public override Color MenuItemSelectedGradientBegin {get{return Color.FromArgb(51,67,86);}}
            public override Color MenuItemSelectedGradientEnd {get{return Color.FromArgb(51,67,86);}}
        }
    }
}
