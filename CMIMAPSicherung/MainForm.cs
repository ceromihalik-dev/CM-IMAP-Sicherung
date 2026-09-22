using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace CMIMAPSicherung
{
    public class MainForm : Form
        {
            private AppConfig config;
            private readonly string appDataDir;
            private readonly string configPath;
            private readonly string secretDir;
            private readonly string tempDir;
    
            private TextBox txtBackupRoot;
            private DataGridView grid;
            private ProgressBar progressOverall;
            private ProgressBar progressCurrent;
            private Label lblProgress;
            private TextBox txtLog;
            private Button btnBackupAll;
            private Button btnBackupSelected;
            private Button btnCancel;
            private Button btnTest;
            private Button btnPassword;
            private Button btnAdd;
            private Button btnEdit;
            private Button btnDelete;
            private Button btnOpenBackup;
            private Button btnOpenLog;
            private Button btnBrowse;
            private Button btnHistory;
            private Button btnGlobalHistory;
            private Button btnSchedule;
            private Button btnSettings;
            private Button btnInfo;
            private Button btnTools;
            private Button btnRestore;
            private Button btnMboxViewer;
    
            private PictureBox picLogo;
            private Label lblSummary;
            private Label lblCardAccounts;
            private Label lblCardStatus;
            private Label lblCardStorage;
            private Label lblCardSchedule;
            private NotifyIcon notifyIcon;
    
            private BackgroundWorker worker;
            private volatile bool cancelRequested;
            private readonly object processLock = new object();
            private Process currentProcess;
            private string lastLogPath;
            private DateTime operationStarted;
            private System.Windows.Forms.Timer elapsedTimer;
            private readonly bool scheduledMode;
            private bool allowApplicationExit;
    
            public MainForm() : this(false) { }

            public MainForm(bool scheduledMode)
            {
                this.scheduledMode = scheduledMode;
                Text = "CM IMAP Sicherung";
                StartPosition = FormStartPosition.CenterScreen;
                MinimumSize = new Size(980, 680);
                Size = new Size(1140, 760);
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                BackColor = UiTheme.Window;
    
                appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IMAP-Backup-App");
                configPath = Path.Combine(appDataDir, "config.xml");
                secretDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IMAP-Backup-Secrets");
                tempDir = Path.Combine(Path.GetTempPath(), "IMAP-Backup-App");
    
                Directory.CreateDirectory(appDataDir);
                Directory.CreateDirectory(secretDir);
                Directory.CreateDirectory(tempDir);
    
                config = LoadConfig();
                BuildUi();
                LoadBranding();
                BindConfigToUi();
                SetupWorker();
                InitializeNotifyIcon();
    
                elapsedTimer = new System.Windows.Forms.Timer();
                elapsedTimer.Interval = 1000;
                elapsedTimer.Tick += delegate
                {
                    if (worker != null && worker.IsBusy)
                    {
                        TimeSpan t = DateTime.Now - operationStarted;
                        string current = lblProgress.Tag as string;
                        if (String.IsNullOrEmpty(current)) current = "Sicherung läuft";
                        lblProgress.Text = String.Format("{0}   |   Laufzeit {1:hh\\:mm\\:ss}", current, t);
                    }
                };
            

                if (scheduledMode)
                {
                    ShowInTaskbar = false;
                    WindowState = FormWindowState.Minimized;
                    Shown += delegate
                    {
                        Hide();
                        BeginInvoke(new Action(StartScheduledBackup));
                    };
                }
                else if (config.TrayEnabled && config.StartMinimizedToTray)
                {
                    Shown += delegate
                    {
                        BeginInvoke(new Action(HideToTray));
                    };
                }
            }
    
            private void InitializeNotifyIcon()
            {
                notifyIcon = new NotifyIcon();
                notifyIcon.Icon = this.Icon ?? SystemIcons.Application;
                notifyIcon.Visible = config.TrayEnabled || scheduledMode;
                notifyIcon.Text = "CM IMAP Sicherung";
                notifyIcon.DoubleClick += delegate { ShowMainWindow(); };

                var menu = new ContextMenuStrip();
                var showItem = new ToolStripMenuItem("Fenster öffnen");
                showItem.Click += delegate { ShowMainWindow(); };
                var backupItem = new ToolStripMenuItem("Jetzt sichern");
                backupItem.Click += delegate
                {
                    ShowMainWindow();
                    BeginInvoke(new Action(delegate { if (!worker.IsBusy) StartBackup(false); }));
                };
                var openBackupItem = new ToolStripMenuItem("Backup-Ordner öffnen");
                openBackupItem.Click += delegate { BtnOpenBackup_Click(this, EventArgs.Empty); };
                var infoItem = new ToolStripMenuItem("Info");
                infoItem.Click += delegate { ShowInfo(); };
                var exitItem = new ToolStripMenuItem("Beenden");
                exitItem.Click += delegate { ExitApplication(); };
                menu.Items.Add(showItem);
                menu.Items.Add(backupItem);
                menu.Items.Add(openBackupItem);
                menu.Items.Add(infoItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(exitItem);
                notifyIcon.ContextMenuStrip = menu;
            }

            private void ShowMainWindow()
            {
                if (scheduledMode) return;
                ShowInTaskbar = true;
                Show();
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Activate();
                BringToFront();
            }

            private void HideToTray()
            {
                if (scheduledMode || !config.TrayEnabled) return;
                Hide();
                ShowInTaskbar = false;
            }

            private void ExitApplication()
            {
                if (worker != null && worker.IsBusy)
                {
                    ShowMainWindow();
                    MessageBox.Show("Eine Sicherung läuft noch. Bitte zuerst warten oder den Vorgang abbrechen.", "CM IMAP Sicherung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                allowApplicationExit = true;
                Close();
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (!scheduledMode && !allowApplicationExit && config != null && config.TrayEnabled && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    HideToTray();
                    ShowNotification("CM IMAP Sicherung", "Das Programm läuft im Infobereich weiter.", ToolTipIcon.Info);
                    return;
                }
                base.OnFormClosing(e);
            }
    
            private void ShowNotification(string title, string text, ToolTipIcon icon)
            {
                try
                {
                    if (notifyIcon == null) return;
                    notifyIcon.BalloonTipTitle = title;
                    notifyIcon.BalloonTipText = text;
                    notifyIcon.BalloonTipIcon = icon;
                    notifyIcon.ShowBalloonTip(4000);
                }
                catch { }
            }
    
            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                try
                {
                    if (notifyIcon != null)
                    {
                        notifyIcon.Visible = false;
                        notifyIcon.Dispose();
                        notifyIcon = null;
                    }
                    if (picLogo != null && picLogo.Image != null)
                    {
                        picLogo.Image.Dispose();
                        picLogo.Image = null;
                    }
                }
                catch { }
                base.OnFormClosed(e);
            }
    
            private void LoadBranding()
            {
                try
                {
                    var asm = typeof(MainForm).Assembly;
                    using (var logoStream = asm.GetManifestResourceStream("CMIMAPSicherung.Resources.cm_logo.png"))
                    {
                        if (logoStream != null && picLogo != null)
                        {
                            using (var img = Image.FromStream(logoStream))
                            {
                                picLogo.Image = new Bitmap(img);
                            }
                        }
                    }
                    using (var iconStream = asm.GetManifestResourceStream("CMIMAPSicherung.Resources.cm_logo.ico"))
                    {
                        if (iconStream != null)
                        {
                            using (var ico = new Icon(iconStream))
                            {
                                this.Icon = (Icon)ico.Clone();
                            }
                        }
                    }
                }
                catch { }
            }
    
            private void BuildUi()
            {
                var root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(12);
                root.ColumnCount = 1;
                root.RowCount = 7;
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                Controls.Add(root);
    
                var header = new TableLayoutPanel();
                header.Dock = DockStyle.Top;
                header.AutoSize = true;
                header.ColumnCount = 3;
                header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                header.BackColor = UiTheme.Header;
                header.Padding = new Padding(14, 12, 14, 12);
                header.Margin = new Padding(0, 0, 0, 10);
    
                picLogo = new PictureBox();
                picLogo.Size = new Size(80, 80);
                picLogo.SizeMode = PictureBoxSizeMode.Zoom;
                picLogo.Margin = new Padding(0, 0, 16, 0);
                header.Controls.Add(picLogo, 0, 0);
    
                var headerText = new TableLayoutPanel();
                headerText.Dock = DockStyle.Fill;
                headerText.AutoSize = true;
                headerText.RowCount = 4;
                headerText.ColumnCount = 1;
    
                var lblTitle = new Label();
                lblTitle.Text = "CM IMAP Sicherung";
                lblTitle.AutoSize = true;
                lblTitle.Font = new Font("Segoe UI", 17F, FontStyle.Bold, GraphicsUnit.Point);
                lblTitle.ForeColor = Color.White;
                lblTitle.Margin = new Padding(0, 2, 0, 2);
                headerText.Controls.Add(lblTitle, 0, 0);
    
                var lblSubtitle = new Label();
                lblSubtitle.Text = "IMAP-Backups für mehrere E-Mail-Konten";
                lblSubtitle.AutoSize = true;
                lblSubtitle.ForeColor = Color.FromArgb(214, 223, 235);
                lblSubtitle.Margin = new Padding(0, 0, 0, 4);
                headerText.Controls.Add(lblSubtitle, 0, 1);
    
                lblSummary = new Label();
                lblSummary.Text = "Bereit";
                lblSummary.AutoSize = true;
                lblSummary.ForeColor = Color.FromArgb(205, 217, 231);
                headerText.Controls.Add(lblSummary, 0, 3);
                header.Controls.Add(headerText, 1, 0);

                var versionBadge = new Label();
                versionBadge.Text = "Version 1.4.0.1";
                versionBadge.AutoSize = true;
                versionBadge.ForeColor = Color.White;
                versionBadge.BackColor = UiTheme.Primary;
                versionBadge.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                versionBadge.Padding = new Padding(10, 6, 10, 6);
                versionBadge.Margin = new Padding(12, 8, 0, 0);
                header.Controls.Add(versionBadge, 2, 0);
                root.Controls.Add(header, 0, 0);
    
                var cards = new FlowLayoutPanel();
                cards.Dock = DockStyle.Top;
                cards.AutoSize = true;
                cards.WrapContents = true;
                cards.Margin = new Padding(0, 0, 0, 10);
                cards.BackColor = UiTheme.Window;
                cards.Controls.Add(UiTheme.CreateCard("Konten", out lblCardAccounts));
                cards.Controls.Add(UiTheme.CreateCard("Letzter Status", out lblCardStatus));
                cards.Controls.Add(UiTheme.CreateCard("Speicher", out lblCardStorage));
                cards.Controls.Add(UiTheme.CreateCard("Zeitplan", out lblCardSchedule));
                root.Controls.Add(cards, 0, 1);

                var top = new TableLayoutPanel();
                top.Dock = DockStyle.Top;
                top.AutoSize = true;
                top.ColumnCount = 3;
                top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                top.Margin = new Padding(0, 0, 0, 8);
    
                var lblRoot = new Label();
                lblRoot.Text = "Sicherungsziel:";
                lblRoot.AutoSize = true;
                lblRoot.Anchor = AnchorStyles.Left;
                lblRoot.Margin = new Padding(0, 7, 8, 0);
                top.Controls.Add(lblRoot, 0, 0);
    
                txtBackupRoot = new TextBox();
                txtBackupRoot.Dock = DockStyle.Fill;
                txtBackupRoot.Margin = new Padding(0, 3, 8, 3);
                txtBackupRoot.Leave += delegate { UpdateBackupRootFromUi(); UpdateStorageSummary(); };
                top.Controls.Add(txtBackupRoot, 1, 0);
    
                btnBrowse = MakeButton("Durchsuchen...", 105);
                btnBrowse.Click += BtnBrowse_Click;
                top.Controls.Add(btnBrowse, 2, 0);
                root.Controls.Add(top, 0, 2);
    
                grid = new DataGridView();
                grid.Dock = DockStyle.Fill;
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.AllowUserToResizeRows = false;
                grid.MultiSelect = false;
                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.RowHeadersVisible = false;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                grid.BackgroundColor = UiTheme.Surface;
                grid.BorderStyle = BorderStyle.FixedSingle;
                grid.GridColor = UiTheme.Border;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.HeaderAlt;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                grid.ColumnHeadersHeight = 32;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
                grid.CurrentCellDirtyStateChanged += delegate
                {
                    if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                };
                grid.CellValueChanged += Grid_CellValueChanged;
                grid.CellDoubleClick += delegate { EditSelectedAccount(); };
    
                var cEnabled = new DataGridViewCheckBoxColumn();
                cEnabled.Name = "Enabled";
                cEnabled.HeaderText = "Aktiv";
                cEnabled.FillWeight = 45;
                grid.Columns.Add(cEnabled);
    
                var cName = new DataGridViewTextBoxColumn();
                cName.Name = "Name";
                cName.HeaderText = "Konto";
                cName.ReadOnly = true;
                cName.FillWeight = 150;
                grid.Columns.Add(cName);
    
                var cUser = new DataGridViewTextBoxColumn();
                cUser.Name = "User";
                cUser.HeaderText = "Benutzer";
                cUser.ReadOnly = true;
                cUser.FillWeight = 155;
                grid.Columns.Add(cUser);
    
                var cHost = new DataGridViewTextBoxColumn();
                cHost.Name = "Host";
                cHost.HeaderText = "IMAP-Server";
                cHost.ReadOnly = true;
                cHost.FillWeight = 100;
                grid.Columns.Add(cHost);
    
                var cPort = new DataGridViewTextBoxColumn();
                cPort.Name = "Port";
                cPort.HeaderText = "Port";
                cPort.ReadOnly = true;
                cPort.FillWeight = 45;
                grid.Columns.Add(cPort);
    
                var cLast = new DataGridViewTextBoxColumn();
                cLast.Name = "Last";
                cLast.HeaderText = "Letzte Sicherung";
                cLast.ReadOnly = true;
                cLast.FillWeight = 105;
                grid.Columns.Add(cLast);
    
                var cSize = new DataGridViewTextBoxColumn();
                cSize.Name = "Size";
                cSize.HeaderText = "Speicher";
                cSize.ReadOnly = true;
                cSize.FillWeight = 80;
                grid.Columns.Add(cSize);
    
                var cStatus = new DataGridViewTextBoxColumn();
                cStatus.Name = "Status";
                cStatus.HeaderText = "Status";
                cStatus.ReadOnly = true;
                cStatus.FillWeight = 85;
                grid.Columns.Add(cStatus);
                root.Controls.Add(grid, 0, 3);
    
                var mid = new TableLayoutPanel();
                mid.Dock = DockStyle.Top;
                mid.AutoSize = true;
                mid.ColumnCount = 2;
                mid.RowCount = 3;
                mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                mid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                mid.Margin = new Padding(0, 8, 0, 8);
    
                lblProgress = new Label();
                lblProgress.Text = "Bereit";
                lblProgress.AutoSize = true;
                lblProgress.Anchor = AnchorStyles.Left;
                lblProgress.Margin = new Padding(0, 0, 0, 4);
                mid.Controls.Add(lblProgress, 0, 0);
                mid.SetColumnSpan(lblProgress, 2);
    
                progressOverall = new ProgressBar();
                progressOverall.Dock = DockStyle.Fill;
                progressOverall.Height = 19;
                progressOverall.Margin = new Padding(0, 0, 8, 4);
                mid.Controls.Add(progressOverall, 0, 1);
    
                var lblOverall = new Label();
                lblOverall.Text = "Gesamt";
                lblOverall.AutoSize = true;
                lblOverall.Anchor = AnchorStyles.Left;
                mid.Controls.Add(lblOverall, 1, 1);
    
                progressCurrent = new ProgressBar();
                progressCurrent.Dock = DockStyle.Fill;
                progressCurrent.Height = 14;
                progressCurrent.Margin = new Padding(0, 0, 8, 0);
                progressCurrent.Style = ProgressBarStyle.Marquee;
                progressCurrent.MarqueeAnimationSpeed = 0;
                mid.Controls.Add(progressCurrent, 0, 2);
    
                var lblCurrent = new Label();
                lblCurrent.Text = "Aktuelles Konto";
                lblCurrent.AutoSize = true;
                lblCurrent.Anchor = AnchorStyles.Left;
                mid.Controls.Add(lblCurrent, 1, 2);
                root.Controls.Add(mid, 0, 4);
    
                txtLog = new TextBox();
                txtLog.Dock = DockStyle.Fill;
                txtLog.Multiline = true;
                txtLog.ReadOnly = true;
                txtLog.ScrollBars = ScrollBars.Vertical;
                txtLog.Font = new Font("Consolas", 9F);
                txtLog.BackColor = UiTheme.LogBack;
                txtLog.ForeColor = UiTheme.LogText;
                root.Controls.Add(txtLog, 0, 5);
    
                var buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.AutoSize = true;
                buttons.WrapContents = true;
                buttons.FlowDirection = FlowDirection.LeftToRight;
                buttons.Margin = new Padding(0, 8, 0, 0);
    
                btnBackupAll = MakeButton("Alle sichern", 110);
                btnBackupAll.Click += delegate { StartBackup(false); };
                buttons.Controls.Add(btnBackupAll);
    
                btnBackupSelected = MakeButton("Auswahl sichern", 120);
                btnBackupSelected.Click += delegate { StartBackup(true); };
                buttons.Controls.Add(btnBackupSelected);
    
                btnCancel = MakeButton("Abbrechen", 95);
                btnCancel.Enabled = false;
                btnCancel.Click += BtnCancel_Click;
                buttons.Controls.Add(btnCancel);
    
                btnTest = MakeButton("Verbindung testen", 130);
                btnTest.Click += delegate { StartTestSelected(); };
                buttons.Controls.Add(btnTest);
    
                btnPassword = MakeButton("Kennwort ändern", 120);
                btnPassword.Click += BtnPassword_Click;
                buttons.Controls.Add(btnPassword);
    
                btnAdd = MakeButton("Konto +", 80);
                btnAdd.Click += BtnAdd_Click;
                buttons.Controls.Add(btnAdd);
    
                btnEdit = MakeButton("Bearbeiten", 90);
                btnEdit.Click += delegate { EditSelectedAccount(); };
                buttons.Controls.Add(btnEdit);
    
                btnDelete = MakeButton("Entfernen", 90);
                btnDelete.Click += BtnDelete_Click;
                buttons.Controls.Add(btnDelete);
    
                btnOpenBackup = MakeButton("Backup öffnen", 110);
                btnOpenBackup.Click += BtnOpenBackup_Click;
                buttons.Controls.Add(btnOpenBackup);
    
                btnOpenLog = MakeButton("Log öffnen", 95);
                btnOpenLog.Click += BtnOpenLog_Click;
                buttons.Controls.Add(btnOpenLog);
    
                btnHistory = MakeButton("Konto-Historie", 115);
                btnHistory.Click += BtnHistory_Click;
                buttons.Controls.Add(btnHistory);

                btnGlobalHistory = MakeButton("Gesamt-Historie", 120);
                btnGlobalHistory.Click += BtnGlobalHistory_Click;
                buttons.Controls.Add(btnGlobalHistory);

                btnSchedule = MakeButton("Zeitplan", 90);
                btnSchedule.Click += BtnSchedule_Click;
                buttons.Controls.Add(btnSchedule);

                btnSettings = MakeButton("Einstellungen", 105);
                btnSettings.Click += BtnSettings_Click;
                buttons.Controls.Add(btnSettings);

                btnTools = MakeButton("Backup-Werkzeuge", 130);
                btnTools.Click += BtnTools_Click;
                buttons.Controls.Add(btnTools);

                btnRestore = MakeButton("Wiederherstellen", 130);
                btnRestore.Click += BtnRestore_Click;
                buttons.Controls.Add(btnRestore);

                btnMboxViewer = MakeButton("MBOX Viewer", 110);
                btnMboxViewer.Click += BtnMboxViewer_Click;
                buttons.Controls.Add(btnMboxViewer);

                btnInfo = MakeButton("Info", 70);
                btnInfo.Click += delegate { ShowInfo(); };
                buttons.Controls.Add(btnInfo);
    
                root.Controls.Add(buttons, 0, 6);
            }
    
            private Button MakeButton(string text, int width)
            {
                var b = new Button();
                b.Text = text;
                b.Width = width;
                b.Height = 34;
                b.Margin = new Padding(0, 0, 8, 0);

                if (text == "Alle sichern" || text == "Auswahl sichern" || text == "Verbindung testen")
                    UiTheme.ApplyPrimary(b);
                else if (text == "Wiederherstellen")
                    UiTheme.ApplyWarning(b);
                else if (text == "Abbrechen" || text == "Entfernen")
                    UiTheme.ApplyDanger(b);
                else
                    UiTheme.ApplySecondary(b);
                return b;
            }

            private void SetupWorker()
            {
                worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.WorkerSupportsCancellation = true;
                worker.DoWork += Worker_DoWork;
                worker.ProgressChanged += Worker_ProgressChanged;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            }
    
            private AppConfig LoadConfig()
            {
                try
                {
                    if (File.Exists(configPath))
                    {
                        var xs = new XmlSerializer(typeof(AppConfig));
                        using (var fs = File.OpenRead(configPath))
                        {
                            var loaded = xs.Deserialize(fs) as AppConfig;
                            if (loaded != null && loaded.Accounts != null)
                            {
                                NormalizeConfig(loaded);
                                return loaded;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Die gespeicherte Konfiguration konnte nicht geladen werden.\r\n\r\n" + ex.Message,
                        "Konfiguration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
    
                var cfg = CreateDefaultConfig();
                NormalizeConfig(cfg);
                try { SaveConfig(cfg); } catch { }
                return cfg;
            }
    
            private void NormalizeConfig(AppConfig cfg)
            {
                if (cfg.Accounts == null) cfg.Accounts = new List<AccountConfig>();
                if (cfg.LogRetentionDays <= 0) cfg.LogRetentionDays = 30;
                if (cfg.Schedule == null) cfg.Schedule = new ScheduleConfig();
                if (cfg.GlobalHistory == null) cfg.GlobalHistory = new List<GlobalHistoryEntry>();
                if (cfg.RetryCount < 0) cfg.RetryCount = 0;
                if (cfg.RetryDelayMinutes < 0) cfg.RetryDelayMinutes = 0;
                if (cfg.ZipKeepCount <= 0) cfg.ZipKeepCount = 3;
                if (cfg.StorageWarningGB <= 0) cfg.StorageWarningGB = 10;
                if (cfg.ImapTimeoutSeconds <= 0)
                {
                    // Migration aus Version 1.4.0.0: neue Large-Mailbox-Optionen standardmäßig aktivieren.
                    cfg.ImapTimeoutSeconds = 600;
                    cfg.LargeMailboxMode = true;
                    cfg.PreventSleepDuringLongOperations = true;
                }
                if (cfg.LargeFileWarningGB <= 0) cfg.LargeFileWarningGB = 5;
                if (cfg.LargeMailboxMinFreeGB <= 0) cfg.LargeMailboxMinFreeGB = 40;
                if (cfg.ZipMaxStorageGB <= 0) cfg.ZipMaxStorageGB = 50;
                if (String.IsNullOrWhiteSpace(cfg.Schedule.Frequency)) cfg.Schedule.Frequency = "Daily";
                if (String.IsNullOrWhiteSpace(cfg.Schedule.Time)) cfg.Schedule.Time = "20:00";
                foreach (var a in cfg.Accounts)
                {
                    if (a.History == null) a.History = new List<HistoryEntry>();
                    if (a.Port <= 0) a.Port = 993;
                }
            }
    
            private AppConfig CreateDefaultConfig()
            {
                var cfg = new AppConfig();
                cfg.BackupRoot = @"D:\IMAP-Backup";
                // Public/GitHub build: no real mail accounts are preconfigured.
                // Add accounts in the application via "Konto +".
                return cfg;
            }
    
            private AccountConfig NewAccount(string name, string host, int port, string user, string id)
            {
                return new AccountConfig { Enabled = true, Name = name, Host = host, Port = port, User = user, Id = id, LastBackup = "" };
            }
    
            private void SaveConfig()
            {
                UpdateBackupRootFromUi();
                SaveConfig(config);
            }
    
            private void SaveConfig(AppConfig cfg)
            {
                Directory.CreateDirectory(appDataDir);
                string tmp = configPath + ".tmp";
                var xs = new XmlSerializer(typeof(AppConfig));
                using (var fs = File.Create(tmp)) xs.Serialize(fs, cfg);
                if (File.Exists(configPath)) File.Delete(configPath);
                File.Move(tmp, configPath);
            }
    
            private void BindConfigToUi()
            {
                txtBackupRoot.Text = config.BackupRoot;
                RefreshGrid();
            }
    
            private void RefreshGrid()
            {
                string selectedId = GetSelectedAccount() != null ? GetSelectedAccount().Id : null;
                grid.Rows.Clear();
                foreach (var a in config.Accounts)
                {
                    string status = a.History != null && a.History.Count > 0 ? a.History[a.History.Count - 1].Result : "Bereit";
                    int i = grid.Rows.Add(a.Enabled, a.Name, a.User, a.Host, a.Port.ToString(), a.LastBackup, GetAccountSizeText(a), status);
                    grid.Rows[i].Tag = a;
                    ApplyRowStyle(grid.Rows[i], status);
                    if (!String.IsNullOrEmpty(selectedId) && a.Id == selectedId) grid.Rows[i].Selected = true;
                }
                if (grid.Rows.Count > 0 && grid.SelectedRows.Count == 0) grid.Rows[0].Selected = true;
                UpdateStorageSummary();
            }
    
            private AccountConfig GetSelectedAccount()
            {
                if (grid == null || grid.SelectedRows.Count == 0) return null;
                return grid.SelectedRows[0].Tag as AccountConfig;
            }
    
            private void SetRowStatus(string accountId, string status)
            {
                foreach (DataGridViewRow r in grid.Rows)
                {
                    var a = r.Tag as AccountConfig;
                    if (a != null && a.Id == accountId)
                    {
                        r.Cells["Status"].Value = status;
                        r.Cells["Last"].Value = a.LastBackup;
                        r.Cells["Size"].Value = GetAccountSizeText(a);
                        ApplyRowStyle(r, status);
                        break;
                    }
                }
            }
    
            private void ApplyRowStyle(DataGridViewRow row, string status)
            {
                Color back = Color.White;
                Color fore = Color.Black;
                string value = status ?? "";
                if (value.StartsWith("OK") || value.Contains("Verbindung OK"))
                {
                    back = Color.FromArgb(232, 247, 235);
                    fore = Color.FromArgb(26, 87, 44);
                }
                else if (value.StartsWith("Fehler"))
                {
                    back = Color.FromArgb(253, 236, 234);
                    fore = Color.FromArgb(145, 36, 36);
                }
                else if (value.StartsWith("Läuft") || value.StartsWith("Wiederholung") || value.StartsWith("Warte auf Retry"))
                {
                    back = Color.FromArgb(232, 240, 254);
                    fore = Color.FromArgb(22, 69, 137);
                }
                else if (value.StartsWith("Abgebrochen"))
                {
                    back = Color.FromArgb(245, 245, 245);
                    fore = Color.DimGray;
                }
                row.DefaultCellStyle.BackColor = back;
                row.DefaultCellStyle.ForeColor = fore;
                row.DefaultCellStyle.SelectionBackColor = ControlPaint.Dark(back);
                row.DefaultCellStyle.SelectionForeColor = fore;
            }
    
            private string GetAccountSizeText(AccountConfig a)
            {
                try
                {
                    return FormatBytes(GetDirectorySizeSafe(Path.Combine(config.BackupRoot ?? @"D:\IMAP-Backup", a.Id)));
                }
                catch { return "-"; }
            }
    
            private long GetDirectorySizeSafe(string dir)
            {
                try
                {
                    if (!Directory.Exists(dir)) return 0;
                    long total = 0;
                    foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        try { total += new FileInfo(file).Length; } catch { }
                    }
                    return total;
                }
                catch { return 0; }
            }
    
            private string FormatBytes(long bytes)
            {
                string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
                double value = Math.Max(0, bytes);
                int idx = 0;
                while (value >= 1024 && idx < units.Length - 1)
                {
                    value /= 1024.0;
                    idx++;
                }
                return value.ToString(idx == 0 ? "0" : "0.0") + " " + units[idx];
            }
    
            private void UpdateStorageSummary()
            {
                try
                {
                    int active = config.Accounts.Count(x => x.Enabled);
                    long total = config.Accounts.Select(a => GetDirectorySizeSafe(Path.Combine(config.BackupRoot, a.Id))).Sum();
                    string retry = config.RetryEnabled && config.RetryCount > 0 ? String.Format("Retry: {0}x/{1} min", config.RetryCount, config.RetryDelayMinutes) : "Retry: aus";
                    string tray = config.TrayEnabled ? "Tray: an" : "Tray: aus";
                    string free = "Speicher: n/a"; long freeBytes, totalBytes;
                    if (BackupTools.TryGetFreeSpace(config.BackupRoot, out freeBytes, out totalBytes)) free = "frei: " + BackupTools.FormatBytes(freeBytes);
                    string protection = String.Format("Prüfung: {0} / ZIP: {1}", config.VerifyAfterBackup ? "an" : "aus", config.ZipAfterBackup ? "an" : "aus");
                    string large = config.LargeMailboxMode ? String.Format("Large: an / {0}s", config.ImapTimeoutSeconds) : "Large: aus";
                    lblSummary.Text = String.Format("{0} aktiv   •   {1}   •   {2}   •   {3}", active, protection, large, tray);

                    int okCount = config.Accounts.Count(a => a.History != null && a.History.Count > 0 && (a.History[a.History.Count - 1].Result ?? "").StartsWith("OK"));
                    int errorCount = config.Accounts.Count(a => a.History != null && a.History.Count > 0 && (a.History[a.History.Count - 1].Result ?? "").StartsWith("Fehler"));
                    if (lblCardAccounts != null) lblCardAccounts.Text = String.Format("{0} gesamt / {1} aktiv", config.Accounts.Count, active);
                    if (lblCardStatus != null) lblCardStatus.Text = String.Format("{0} OK / {1} Fehler", okCount, errorCount);
                    if (lblCardStorage != null) lblCardStorage.Text = String.Format("{0} / {1}", FormatBytes(total), free.Replace("frei: ", "frei "));
                    if (lblCardSchedule != null) lblCardSchedule.Text = GetScheduleSummary();
                }
                catch
                {
                    lblSummary.Text = String.Format("Konten: {0}", config.Accounts.Count);
                    if (lblCardAccounts != null) lblCardAccounts.Text = config.Accounts.Count + " gesamt";
                }
            }
    
            private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != "Enabled") return;
                var a = grid.Rows[e.RowIndex].Tag as AccountConfig;
                if (a == null) return;
                object v = grid.Rows[e.RowIndex].Cells["Enabled"].Value;
                a.Enabled = v != null && Convert.ToBoolean(v);
                try { SaveConfig(); } catch { }
            }
    
            private void UpdateBackupRootFromUi()
            {
                string value = txtBackupRoot.Text.Trim();
                if (!String.IsNullOrEmpty(value)) config.BackupRoot = value;
            }
    
            private void BtnBrowse_Click(object sender, EventArgs e)
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Ordner für die IMAP-Sicherungen auswählen";
                    if (Directory.Exists(txtBackupRoot.Text)) dlg.SelectedPath = txtBackupRoot.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        txtBackupRoot.Text = dlg.SelectedPath;
                        SaveConfig();
                    }
                }
            }
    
            private void BtnAdd_Click(object sender, EventArgs e)
            {
                using (var dlg = new AccountEditForm(null))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (config.Accounts.Any(x => String.Equals(x.Id, dlg.Account.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            MessageBox.Show("Diese Konto-ID ist bereits vorhanden.", "Konto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        config.Accounts.Add(dlg.Account);
                        SaveConfig();
                        RefreshGrid();
                    }
                }
            }
    
            private void EditSelectedAccount()
            {
                var a = GetSelectedAccount();
                if (a == null) return;
                var copy = new AccountConfig
                {
                    Enabled = a.Enabled,
                    Name = a.Name,
                    Host = a.Host,
                    Port = a.Port,
                    User = a.User,
                    Id = a.Id,
                    LastBackup = a.LastBackup,
                    History = a.History == null ? new List<HistoryEntry>() : new List<HistoryEntry>(a.History)
                };
                using (var dlg = new AccountEditForm(copy))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (config.Accounts.Any(x => !Object.ReferenceEquals(x, a) && String.Equals(x.Id, dlg.Account.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            MessageBox.Show("Diese Konto-ID ist bereits vorhanden.", "Konto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        string oldId = a.Id;
                        a.Name = dlg.Account.Name;
                        a.Host = dlg.Account.Host;
                        a.Port = dlg.Account.Port;
                        a.User = dlg.Account.User;
                        a.Id = dlg.Account.Id;
                        a.Enabled = dlg.Account.Enabled;
                        if (!String.Equals(oldId, a.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            string oldSecret = GetSecretPath(oldId);
                            string newSecret = GetSecretPath(a.Id);
                            try
                            {
                                if (File.Exists(oldSecret) && !File.Exists(newSecret)) File.Move(oldSecret, newSecret);
                            }
                            catch { }
                        }
                        SaveConfig();
                        RefreshGrid();
                    }
                }
            }
    
            private void BtnDelete_Click(object sender, EventArgs e)
            {
                var a = GetSelectedAccount();
                if (a == null) return;
                var result = MessageBox.Show(
                    "Konto aus dem Programm entfernen?\r\n\r\n" + a.Name +
                    "\r\n\r\nVorhandene Sicherungen und das gespeicherte Kennwort werden nicht gelöscht.",
                    "Konto entfernen", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
                config.Accounts.Remove(a);
                SaveConfig();
                RefreshGrid();
            }
    
            private void BtnPassword_Click(object sender, EventArgs e)
            {
                var a = GetSelectedAccount();
                if (a == null) return;
                PromptAndSavePassword(a);
            }
    
            private void BtnHistory_Click(object sender, EventArgs e)
            {
                var a = GetSelectedAccount();
                if (a == null) return;
                using (var dlg = new HistoryForm(a))
                {
                    dlg.ShowDialog(this);
                }
            }

            private void BtnGlobalHistory_Click(object sender, EventArgs e)
            {
                using (var dlg = new GlobalHistoryForm(config.GlobalHistory))
                {
                    dlg.ShowDialog(this);
                }
            }

            private void ShowInfo()
            {
                using (var dlg = new InfoForm())
                {
                    dlg.ShowDialog(this);
                }
            }

            private void BtnSettings_Click(object sender, EventArgs e)
            {
                using (var dlg = new SettingsForm(config))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    config.LogRetentionDays = dlg.LogRetentionDays;
                    config.RetryEnabled = dlg.RetryEnabled;
                    config.RetryCount = dlg.RetryCount;
                    config.RetryDelayMinutes = dlg.RetryDelayMinutes;
                    config.TrayEnabled = dlg.TrayEnabled;
                    config.StartMinimizedToTray = dlg.StartMinimizedToTray;
                    config.VerifyAfterBackup = dlg.VerifyAfterBackup;
                    config.ZipAfterBackup = dlg.ZipAfterBackup;
                    config.ZipKeepCount = dlg.ZipKeepCount;
                    config.StorageWarningEnabled = dlg.StorageWarningEnabled;
                    config.StorageWarningGB = dlg.StorageWarningGB;
                    config.LargeMailboxMode = dlg.LargeMailboxMode;
                    config.ImapTimeoutSeconds = dlg.ImapTimeoutSeconds;
                    config.PreventSleepDuringLongOperations = dlg.PreventSleepDuringLongOperations;
                    config.LargeFileWarningGB = dlg.LargeFileWarningGB;
                    config.LargeMailboxMinFreeGB = dlg.LargeMailboxMinFreeGB;
                    config.ZipMaxStorageGB = dlg.ZipMaxStorageGB;
                    if (!String.IsNullOrEmpty(dlg.ZipPasswordValue))
                    {
                        string err;
                        if (!BackupTools.SaveProtectedSecret(Path.Combine(secretDir, BackupTools.ArchiveSecretName), dlg.ZipPasswordValue, out err))
                            MessageBox.Show("ZIP-Kennwort konnte nicht gespeichert werden.\r\n\r\n" + err, "ZIP-Kennwort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    SaveConfig();
                    if (notifyIcon != null) notifyIcon.Visible = config.TrayEnabled || scheduledMode;
                    UpdateStorageSummary();
                    if (!config.TrayEnabled) ShowMainWindow();
                }
            }

            private void BtnTools_Click(object sender, EventArgs e)
            {
                using (var dlg = new AdvancedToolsForm(config, secretDir, tempDir))
                {
                    dlg.ShowDialog(this);
                }
                RefreshGrid();
            }

            private void BtnRestore_Click(object sender, EventArgs e)
            {
                var selected = GetSelectedAccount();
                string accountId = selected == null ? null : selected.Id;
                using (var dlg = new RestoreForm(config, secretDir, tempDir, accountId))
                {
                    dlg.ShowDialog(this);
                }
                RefreshGrid();
            }

            private void BtnMboxViewer_Click(object sender, EventArgs e)
            {
                UpdateBackupRootFromUi();
                string root = config.BackupRoot;
                var selected = GetSelectedAccount();
                if (selected != null)
                {
                    string accountRoot = Path.Combine(config.BackupRoot, selected.Id);
                    if (Directory.Exists(accountRoot)) root = accountRoot;
                }
                using (var dlg = new MboxViewerForm(root))
                {
                    dlg.ShowDialog(this);
                }
            }

            private void BtnSchedule_Click(object sender, EventArgs e)
            {
                TaskStatusInfo status = TaskSchedulerManager.QueryStatus();
                using (var dlg = new SchedulerForm(config.Schedule, status))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string details;
                    if (dlg.RemoveRequested)
                    {
                        if (!TaskSchedulerManager.Remove(out details))
                        {
                            MessageBox.Show("Der Windows-Zeitplan konnte nicht entfernt werden.\r\n\r\n" + details, "Zeitplan", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        config.Schedule = new ScheduleConfig();
                        SaveConfig();
                        UpdateStorageSummary();
                        MessageBox.Show("Der automatische Zeitplan wurde entfernt.", "Zeitplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!TaskSchedulerManager.CreateOrUpdate(dlg.Schedule, out details))
                    {
                        MessageBox.Show("Der Windows-Zeitplan konnte nicht eingerichtet werden.\r\n\r\n" + details, "Zeitplan", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    config.Schedule = dlg.Schedule;
                    SaveConfig();
                    UpdateStorageSummary();
                    MessageBox.Show("Der automatische Zeitplan wurde gespeichert.", "Zeitplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            private string GetScheduleSummary()
            {
                if (config.Schedule == null || !config.Schedule.Enabled) return "Zeitplan: aus";
                if (config.Schedule.Frequency == "Logon") return "Zeitplan: bei Anmeldung";
                string when = config.Schedule.Frequency == "Weekly" ? GetGermanDayName(config.Schedule.Weekday) + " " + config.Schedule.Time : "täglich " + config.Schedule.Time;
                DateTime? next = CalculateNextRun(config.Schedule);
                return next.HasValue ? String.Format("Zeitplan: {0} (nächste {1:dd.MM. HH:mm})", when, next.Value) : "Zeitplan: " + when;
            }

            private string GetGermanDayName(DayOfWeek day)
            {
                switch (day) { case DayOfWeek.Monday: return "Mo"; case DayOfWeek.Tuesday: return "Di"; case DayOfWeek.Wednesday: return "Mi"; case DayOfWeek.Thursday: return "Do"; case DayOfWeek.Friday: return "Fr"; case DayOfWeek.Saturday: return "Sa"; case DayOfWeek.Sunday: return "So"; default: return day.ToString(); }
            }

            private DateTime? CalculateNextRun(ScheduleConfig schedule)
            {
                try
                {
                    TimeSpan time; if (!TimeSpan.TryParse(schedule.Time, out time)) time = new TimeSpan(20, 0, 0);
                    DateTime now = DateTime.Now;
                    if (schedule.Frequency == "Daily") { DateTime c = now.Date.Add(time); if (c <= now) c = c.AddDays(1); return c; }
                    if (schedule.Frequency == "Weekly") { int delta = ((int)schedule.Weekday - (int)now.DayOfWeek + 7) % 7; DateTime c = now.Date.AddDays(delta).Add(time); if (c <= now) c = c.AddDays(7); return c; }
                }
                catch { }
                return null;
            }

            private void StartScheduledBackup()
            {
                AppendLog("Automatischer Zeitplanlauf gestartet: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
                StartBackup(false);
                if (worker == null || !worker.IsBusy)
                {
                    WriteScheduledFailureLog("Automatischer Lauf konnte nicht gestartet werden. Prüfen Sie Python, imapbackup3 und gespeicherte Kennwörter.");
                    BeginInvoke(new Action(Application.Exit));
                }
            }

            private void WriteScheduledFailureLog(string message)
            {
                try
                {
                    Directory.CreateDirectory(config.BackupRoot);
                    string logDir = Path.Combine(config.BackupRoot, "_Logs"); Directory.CreateDirectory(logDir);
                    string path = Path.Combine(logDir, String.Format("CM-IMAP-Sicherung_AUTO_FEHLER_{0:yyyy-MM-dd_HHmmss}.log", DateTime.Now));
                    File.WriteAllText(path, "CM IMAP Sicherung - Automatischer Lauf\r\n" + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "\r\n" + message, new UTF8Encoding(false));
                }
                catch { }
            }
    
            private bool PromptAndSavePassword(AccountConfig a)
            {
                using (var dlg = new PasswordForm(a.User))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                    try
                    {
                        SavePassword(a, dlg.PasswordValue);
                        MessageBox.Show("Das Kennwort wurde Windows-verschlüsselt gespeichert.", "Kennwort", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Das Kennwort konnte nicht gespeichert werden.\r\n\r\n" + ex.Message,
                            "Kennwort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
    
            private string GetSecretPath(string id)
            {
                return Path.Combine(secretDir, id + ".cred");
            }
    
            private void SavePassword(AccountConfig a, string password)
            {
                Directory.CreateDirectory(secretDir);
                string secret = GetSecretPath(a.Id);
                var psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; $p=[Console]::In.ReadToEnd(); $s=ConvertTo-SecureString $p -AsPlainText -Force; ConvertFrom-SecureString -SecureString $s | Set-Content -LiteralPath $env:SECRET_FILE -Encoding ASCII\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardError = true;
                psi.EnvironmentVariables["SECRET_FILE"] = secret;
                using (var p = Process.Start(psi))
                {
                    p.StandardInput.Write(password);
                    p.StandardInput.Close();
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new InvalidOperationException(String.IsNullOrWhiteSpace(err) ? "PowerShell-Fehler " + p.ExitCode : err.Trim());
                }
            }
    
            private bool EnsurePasswords(List<AccountConfig> accounts)
            {
                foreach (var a in accounts)
                {
                    if (!File.Exists(GetSecretPath(a.Id)))
                    {
                        if (scheduledMode)
                        {
                            WriteScheduledFailureLog("Gespeichertes IMAP-Kennwort fehlt für: " + a.User);
                            return false;
                        }
                        var res = MessageBox.Show(
                            "Für dieses Konto ist noch kein Kennwort gespeichert:\r\n\r\n" + a.User +
                            "\r\n\r\nKennwort jetzt hinterlegen?",
                            "IMAP-Kennwort", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (res != DialogResult.Yes) return false;
                        if (!PromptAndSavePassword(a)) return false;
                    }
                }
                return true;
            }
    
            private bool CheckStorageWarning(List<AccountConfig> accounts)
            {
                long freeBytes, totalBytes;
                bool haveSpace = BackupTools.TryGetFreeSpace(config.BackupRoot, out freeBytes, out totalBytes);

                if (config.LargeMailboxMode)
                {
                    string format;
                    if (BackupTools.TryGetDriveFormat(config.BackupRoot, out format))
                    {
                        if (String.Equals(format, "FAT32", StringComparison.OrdinalIgnoreCase))
                        {
                            string fatText = "Das Sicherungsziel verwendet FAT32. Einzelne MBOX- oder ZIP-Dateien über 4 GB können dort nicht gespeichert werden. Für große Postfächer bitte NTFS oder ein anderes Dateisystem ohne 4-GB-Dateigrenze verwenden.";
                            if (scheduledMode)
                            {
                                WriteScheduledFailureLog(fatText);
                                return false;
                            }
                            MessageBox.Show(fatText, "Dateisystem nicht geeignet", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }

                if (!haveSpace || (!config.StorageWarningEnabled && !config.LargeMailboxMode)) return true;

                long normalThreshold = (long)Math.Max(1, config.StorageWarningGB) * 1024L * 1024L * 1024L;
                long largeThreshold = config.LargeMailboxMode ? (long)Math.Max(5, config.LargeMailboxMinFreeGB) * 1024L * 1024L * 1024L : 0;
                long threshold = Math.Max(config.StorageWarningEnabled ? normalThreshold : 0, largeThreshold);
                if (freeBytes >= threshold) return true;

                long existing = 0;
                foreach (var a in accounts) existing += GetDirectorySizeSafe(Path.Combine(config.BackupRoot, a.Id));
                string text = String.Format(
                    "Auf dem Sicherungslaufwerk sind nur noch {0} frei. Die aktuelle Sicherheitsreserve liegt bei {1}.\r\n\r\nVorhandener lokaler Sicherungsbestand der gewählten Konten: {2}.\r\nBei Erst- oder Vollsicherungen kann der tatsächliche zusätzliche Speicherbedarf deutlich höher sein.",
                    BackupTools.FormatBytes(freeBytes), BackupTools.FormatBytes(threshold), BackupTools.FormatBytes(existing));
                if (scheduledMode)
                {
                    AppendLog("SPEICHERWARNUNG: " + text.Replace("\r\n", " ") + " Der Zeitplanlauf wird fortgesetzt.");
                    return true;
                }
                return MessageBox.Show(text + "\r\n\r\nSicherung trotzdem starten?", "Speicherwarnung", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            }

            private void StartBackup(bool selectedOnly)
            {
                if (worker.IsBusy) return;
                UpdateBackupRootFromUi();
                if (String.IsNullOrWhiteSpace(config.BackupRoot))
                {
                    MessageBox.Show("Bitte ein Sicherungsziel auswählen.", "Sicherung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
    
                List<AccountConfig> accounts;
                if (selectedOnly)
                {
                    var a = GetSelectedAccount();
                    if (a == null) return;
                    accounts = new List<AccountConfig> { a };
                }
                else
                {
                    accounts = config.Accounts.Where(x => x.Enabled).ToList();
                }
                if (accounts.Count == 0)
                {
                    MessageBox.Show("Es ist kein aktives Konto ausgewählt.", "Sicherung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!CheckStorageWarning(accounts)) return;
                if (!EnsurePasswords(accounts)) return;
    
                PythonCommand py;
                if (!EnsureEngine(out py)) return;
                if (config.ZipAfterBackup && !EnsureZipEngine(py)) return;
                if (config.ZipAfterBackup && !File.Exists(Path.Combine(secretDir, BackupTools.ArchiveSecretName)))
                {
                    if (scheduledMode) { WriteScheduledFailureLog("ZIP-Komprimierung ist aktiviert, aber das ZIP-Kennwort fehlt."); return; }
                    MessageBox.Show("ZIP-Komprimierung ist aktiviert. Bitte unter Einstellungen zuerst ein ZIP-Kennwort setzen.", "ZIP-Komprimierung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SaveConfig();
                StartOperation(new OperationRequest { Mode = OperationMode.Backup, Accounts = accounts, Python = py, Source = scheduledMode ? "Zeitplan" : "Manuell" });
            }
    
            private void StartTestSelected()
            {
                if (worker.IsBusy) return;
                var a = GetSelectedAccount();
                if (a == null) return;
                if (!EnsurePasswords(new List<AccountConfig> { a })) return;
                PythonCommand py;
                if (!EnsureEngine(out py)) return;
                StartOperation(new OperationRequest { Mode = OperationMode.Test, Accounts = new List<AccountConfig> { a }, Python = py, Source = "Manuell" });
            }
    
            private bool EnsureEngine(out PythonCommand py)
            {
                py = DetectPython();
                if (py == null)
                {
                    if (scheduledMode) { WriteScheduledFailureLog("Python 3 wurde nicht gefunden."); return false; }
                    MessageBox.Show("Python 3 wurde nicht gefunden.\r\n\r\nBitte Python installieren oder die vorhandene Python-Installation zum PATH hinzufügen.",
                        "Python", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
    
                int rc;
                string output;
                RunSimpleProcess(py, new List<string> { "-c", "import imapbackup3; print('OK')" }, out rc, out output);
                if (rc == 0) return true;
    
                if (scheduledMode) { WriteScheduledFailureLog("Das Python-Modul imapbackup3 ist nicht installiert."); return false; }

                var ask = MessageBox.Show(
                    "Das Python-Modul 'imapbackup3' ist nicht installiert.\r\n\r\nJetzt automatisch installieren?",
                    "imapbackup3", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask != DialogResult.Yes) return false;
    
                Cursor = Cursors.WaitCursor;
                try
                {
                    RunSimpleProcess(py, new List<string> { "-m", "pip", "install", "--user", "imapbackup3" }, out rc, out output);
                }
                finally { Cursor = Cursors.Default; }
    
                if (rc != 0)
                {
                    MessageBox.Show("imapbackup3 konnte nicht installiert werden.\r\n\r\n" + output,
                        "Installation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                return true;
            }
    
            private bool EnsureZipEngine(PythonCommand py)
            {
                if (BackupTools.CheckModule(py, "pyzipper")) return true;
                if (scheduledMode) { WriteScheduledFailureLog("Das Python-Modul pyzipper fehlt; ZIP-Komprimierung kann nicht ausgeführt werden."); return false; }
                var ask = MessageBox.Show("Für passwortgeschützte ZIP-Archive wird das Python-Modul 'pyzipper' benötigt.\r\n\r\nJetzt automatisch installieren?", "ZIP-Komprimierung", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask != DialogResult.Yes) return false;
                string output; Cursor = Cursors.WaitCursor;
                try { if (!BackupTools.InstallModule(py, "pyzipper", out output)) { MessageBox.Show("pyzipper konnte nicht installiert werden.\r\n\r\n" + output, "Installation", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; } }
                finally { Cursor = Cursors.Default; }
                return true;
            }

            private PythonCommand DetectPython()
            {
                var candidates = new List<PythonCommand>();
                candidates.Add(new PythonCommand { FileName = "py.exe", Prefix = new List<string> { "-3" } });
                candidates.Add(new PythonCommand { FileName = "python.exe", Prefix = new List<string>() });
                foreach (var c in candidates)
                {
                    try
                    {
                        int rc;
                        string output;
                        RunSimpleProcess(c, new List<string> { "--version" }, out rc, out output);
                        if (rc == 0) return c;
                    }
                    catch { }
                }
                return null;
            }
    
            private void RunSimpleProcess(PythonCommand py, List<string> args, out int exitCode, out string output)
            {
                var all = new List<string>();
                all.AddRange(py.Prefix);
                all.AddRange(args);
                var psi = new ProcessStartInfo();
                psi.FileName = py.FileName;
                psi.Arguments = JoinArgs(all);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                    output = (o + "\r\n" + e).Trim();
                }
            }
    
            private void StartOperation(OperationRequest req)
            {
                cancelRequested = false;
                txtLog.Clear();
                progressOverall.Minimum = 0;
                progressOverall.Maximum = Math.Max(1, req.Accounts.Count);
                progressOverall.Value = 0;
                progressCurrent.MarqueeAnimationSpeed = 30;
                lblProgress.Tag = req.Mode == OperationMode.Test ? "Verbindungstest" : "Sicherung läuft";
                lblProgress.Text = lblProgress.Tag as string;
                operationStarted = DateTime.Now;
                elapsedTimer.Start();
                if (req.Mode == OperationMode.Backup && config.LargeMailboxMode && config.PreventSleepDuringLongOperations) PowerManager.SetKeepAwake(true);
                SetBusy(true);
                worker.RunWorkerAsync(req);
            }
    
            private void SetBusy(bool busy)
            {
                btnBackupAll.Enabled = !busy;
                btnBackupSelected.Enabled = !busy;
                btnTest.Enabled = !busy;
                btnPassword.Enabled = !busy;
                btnAdd.Enabled = !busy;
                btnEdit.Enabled = !busy;
                btnDelete.Enabled = !busy;
                btnSchedule.Enabled = !busy;
                btnSettings.Enabled = !busy;
                btnGlobalHistory.Enabled = !busy;
                btnTools.Enabled = !busy;
                btnRestore.Enabled = !busy;
                btnMboxViewer.Enabled = !busy;
                btnBrowse.Enabled = !busy;
                txtBackupRoot.Enabled = !busy;
                grid.Enabled = !busy;
                btnCancel.Enabled = busy;
            }
    
            private void BtnCancel_Click(object sender, EventArgs e)
            {
                if (!worker.IsBusy) return;
                cancelRequested = true;
                worker.CancelAsync();
                lblProgress.Tag = "Abbruch wird ausgeführt";
                lblProgress.Text = "Abbruch wird ausgeführt ...";
                lock (processLock)
                {
                    try
                    {
                        if (currentProcess != null && !currentProcess.HasExited) currentProcess.Kill();
                    }
                    catch { }
                }
            }
    
            private void Worker_DoWork(object sender, DoWorkEventArgs e)
            {
                var req = e.Argument as OperationRequest;
                if (req == null) return;

                DateTime wholeStarted = DateTime.Now;
                Directory.CreateDirectory(config.BackupRoot);
                string logDir = Path.Combine(config.BackupRoot, "_Logs");
                Directory.CreateDirectory(logDir);
                CleanupOldLogs(logDir);
                lastLogPath = Path.Combine(logDir, String.Format("CM-IMAP-Sicherung_{0:yyyy-MM-dd_HHmmss}.log", DateTime.Now));

                int okCount = 0;
                int errorCount = 0;
                int cancelledCount = 0;

                using (var log = new StreamWriter(lastLogPath, false, new UTF8Encoding(false)))
                {
                    log.AutoFlush = true;
                    log.WriteLine("CM IMAP Sicherung");
                    log.WriteLine("Start : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    log.WriteLine("Quelle: " + (String.IsNullOrWhiteSpace(req.Source) ? "-" : req.Source));
                    log.WriteLine("Ziel  : " + config.BackupRoot);
                    log.WriteLine(new string('-', 70));

                    int completed = 0;
                    foreach (var a in req.Accounts)
                    {
                        if (cancelRequested || worker.CancellationPending)
                        {
                            cancelledCount++;
                            e.Cancel = true;
                            break;
                        }

                        DateTime accountStarted = DateTime.Now;
                        Report(a.Id, "Läuft ...", String.Format("Konto {0}/{1}: {2}", completed + 1, req.Accounts.Count, a.Name), completed, req.Accounts.Count, false);
                        log.WriteLine();
                        log.WriteLine("START " + a.Name);
                        log.WriteLine("Server: " + a.Host + ":" + a.Port);
                        log.WriteLine("User  : " + a.User);

                        int rc = 1;
                        string details = "";
                        int attempts = 0;
                        int maxAttempts = 1;
                        if (req.Mode == OperationMode.Backup && config.RetryEnabled && config.RetryCount > 0)
                            maxAttempts += config.RetryCount;

                        string tempPass = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".pwd");
                        try
                        {
                            DecryptSecretToFile(a, tempPass);
                            for (attempts = 1; attempts <= maxAttempts; attempts++)
                            {
                                if (cancelRequested || worker.CancellationPending)
                                {
                                    rc = 1223;
                                    break;
                                }

                                if (attempts > 1)
                                {
                                    log.WriteLine("WIEDERHOLUNG " + attempts + "/" + maxAttempts);
                                    Report(a.Id, "Wiederholung ...", String.Format("{0}: Versuch {1}/{2}", a.Name, attempts, maxAttempts), completed, req.Accounts.Count, false);
                                }

                                try
                                {
                                    rc = req.Mode == OperationMode.Backup
                                        ? RunBackupProcess(req.Python, a, tempPass, log, req.Accounts.Count, completed)
                                        : RunTestProcess(req.Python, a, tempPass, log);
                                    details = "";
                                }
                                catch (Exception ex)
                                {
                                    rc = 1;
                                    details = ex.Message;
                                    log.WriteLine("FEHLER: " + ex);
                                }

                                if (rc == 0 || req.Mode != OperationMode.Backup || attempts >= maxAttempts) break;

                                int waitSeconds = Math.Max(0, config.RetryDelayMinutes) * 60;
                                log.WriteLine("Nächster Versuch in " + waitSeconds + " Sekunde(n).");
                                if (!WaitForRetry(a, waitSeconds, completed, req.Accounts.Count))
                                {
                                    rc = 1223;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            details = ex.Message;
                            log.WriteLine("FEHLER: " + ex);
                            rc = 1;
                        }
                        finally
                        {
                            TryDelete(tempPass);
                        }

                        completed++;
                        TimeSpan duration = DateTime.Now - accountStarted;

                        if (cancelRequested || worker.CancellationPending || rc == 1223)
                        {
                            cancelledCount++;
                            Report(a.Id, "Abgebrochen", "Vorgang abgebrochen", completed, req.Accounts.Count, false);
                            AddHistory(a, req.Mode, "Abgebrochen", "Vorgang abgebrochen", duration);
                            e.Cancel = true;
                            break;
                        }

                        if (rc == 0)
                        {
                            if (req.Mode == OperationMode.Backup)
                            {
                                a.LastBackup = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                                string accountDir = Path.Combine(config.BackupRoot, a.Id);
                                if (config.LargeMailboxMode)
                                {
                                    long largeThreshold = (long)Math.Max(1, config.LargeFileWarningGB) * 1024L * 1024L * 1024L;
                                    var largeFiles = BackupTools.GetLargeFiles(accountDir, largeThreshold, 10);
                                    foreach (var lf in largeFiles)
                                    {
                                        log.WriteLine("GROSSE DATEI: " + lf.Name + " = " + BackupTools.FormatBytes(lf.Length));
                                    }
                                    if (largeFiles.Count > 0)
                                        Report(a.Id, "Läuft ...", String.Format("{0}: {1} große Datei(en) erkannt", a.Name, largeFiles.Count), completed - 1, req.Accounts.Count, false);
                                }
                                if (config.VerifyAfterBackup)
                                {
                                    try
                                    {
                                        Report(a.Id, "Prüfung ...", "Erstelle SHA-256-Prüfnachweis für " + a.Name, completed - 1, req.Accounts.Count, false);
                                        VerificationResult vr = BackupTools.CreateManifestAndVerify(accountDir);
                                        log.WriteLine("BACKUP-PRÜFUNG: " + vr.Message);
                                        if (!vr.Success) { rc = 91; details = vr.Message; }
                                    }
                                    catch (Exception vex) { rc = 91; details = "Backup-Prüfung fehlgeschlagen: " + vex.Message; log.WriteLine(details); }
                                }
                                if (rc == 0 && config.ZipAfterBackup)
                                {
                                    try
                                    {
                                        Report(a.Id, "ZIP ...", "Erstelle verschlüsseltes ZIP für " + a.Name, completed - 1, req.Accounts.Count, false);
                                        string zipPass = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".zip.pwd"); string zerr;
                                        if (!BackupTools.DecryptProtectedSecret(Path.Combine(secretDir, BackupTools.ArchiveSecretName), zipPass, out zerr)) throw new InvalidOperationException(zerr);
                                        try
                                        {
                                            string archiveDir = Path.Combine(config.BackupRoot, "_Archive", a.Id); Directory.CreateDirectory(archiveDir);
                                            long sourceBytes = BackupTools.GetDirectorySize(accountDir);
                                            long zipFree, zipTotal;
                                            if (BackupTools.TryGetFreeSpace(config.BackupRoot, out zipFree, out zipTotal))
                                            {
                                                long zipReserve = sourceBytes + (2L * 1024L * 1024L * 1024L);
                                                if (zipFree < zipReserve)
                                                    throw new InvalidOperationException("Für die ZIP-Erstellung ist zu wenig freier Speicher vorhanden. Frei: " + BackupTools.FormatBytes(zipFree) + ", empfohlene Reserve: " + BackupTools.FormatBytes(zipReserve));
                                            }
                                            string zipFile = Path.Combine(archiveDir, String.Format("{0}_{1:yyyy-MM-dd_HHmmss}.zip", a.Id.Replace('@','_'), DateTime.Now));
                                            string zout; int zrc = BackupTools.CreateEncryptedZip(req.Python, accountDir, zipFile, zipPass, tempDir, delegate { return cancelRequested || worker.CancellationPending; }, out zout);
                                            log.WriteLine("ZIP: " + zout);
                                            if (zrc != 0) { rc = zrc == 1223 ? 1223 : 92; details = "ZIP-Komprimierung fehlgeschlagen: " + zout; }
                                            else
                                            {
                                                long maxArchiveBytes = (long)Math.Max(1, config.ZipMaxStorageGB) * 1024L * 1024L * 1024L;
                                                BackupTools.CleanupArchives(archiveDir, Math.Max(1, config.ZipKeepCount), maxArchiveBytes);
                                            }
                                        }
                                        finally { TryDelete(zipPass); }
                                    }
                                    catch (Exception zex) { rc = 92; details = "ZIP-Komprimierung fehlgeschlagen: " + zex.Message; log.WriteLine(details); }
                                }
                            }
                            if (rc != 0)
                            {
                                if (rc == 1223 || cancelRequested || worker.CancellationPending)
                                {
                                    cancelledCount++;
                                    Report(a.Id, "Abgebrochen", "Vorgang abgebrochen", completed, req.Accounts.Count, false);
                                    AddHistory(a, req.Mode, "Abgebrochen", "Vorgang abgebrochen", duration);
                                    e.Cancel = true;
                                    break;
                                }
                                errorCount++;
                                string ppResult = rc == 91 ? "Prüffehler" : (rc == 92 ? "ZIP-Fehler" : "Fehler (" + rc + ")");
                                Report(a.Id, ppResult, details, completed, req.Accounts.Count, false);
                                AddHistory(a, req.Mode, ppResult, details, duration);
                                continue;
                            }
                            okCount++;
                            string resultText = req.Mode == OperationMode.Backup ? "OK" : "Verbindung OK";
                            string messageText = req.Mode == OperationMode.Backup ? "Sicherung erfolgreich" : "IMAP-Anmeldung erfolgreich";
                            if (attempts > 1) messageText += " nach " + attempts + " Versuchen";
                            Report(a.Id, resultText, messageText, completed, req.Accounts.Count, false);
                            log.WriteLine(req.Mode == OperationMode.Backup ? "ERGEBNIS: OK" : "ERGEBNIS: VERBINDUNG OK");
                            AddHistory(a, req.Mode, resultText, messageText, duration);
                        }
                        else
                        {
                            errorCount++;
                            string resultText = "Fehler (" + rc + ")";
                            string messageText = String.IsNullOrWhiteSpace(details) ? ("Fehler bei " + a.Name + " (Code " + rc + ")") : details;
                            if (attempts > 1) messageText += " | Versuche: " + attempts;
                            Report(a.Id, resultText, messageText, completed, req.Accounts.Count, false);
                            log.WriteLine("ERGEBNIS: FEHLER " + rc);
                            AddHistory(a, req.Mode, resultText, messageText, duration);
                        }
                    }

                    log.WriteLine();
                    log.WriteLine(new string('-', 70));
                    log.WriteLine("Zusammenfassung: OK=" + okCount + " | Fehler=" + errorCount + " | Abgebrochen=" + cancelledCount);
                    log.WriteLine("Ende: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                }

                TimeSpan wholeDuration = DateTime.Now - wholeStarted;
                var summary = new OperationSummary
                {
                    Mode = req.Mode,
                    Total = req.Accounts.Count,
                    OkCount = okCount,
                    ErrorCount = errorCount,
                    CancelledCount = cancelledCount,
                    LogPath = lastLogPath,
                    Source = String.IsNullOrWhiteSpace(req.Source) ? "-" : req.Source,
                    Duration = wholeDuration
                };
                AddGlobalHistory(summary);
                e.Result = summary;
            }

            private bool WaitForRetry(AccountConfig a, int seconds, int completed, int total)
            {
                if (seconds <= 0) return true;
                for (int remaining = seconds; remaining > 0; remaining--)
                {
                    if (cancelRequested || worker.CancellationPending) return false;
                    if (remaining == seconds || remaining <= 10 || remaining % 15 == 0)
                    {
                        Report(a.Id, "Warte auf Retry", String.Format("{0}: neuer Versuch in {1} s", a.Name, remaining), completed, total, false);
                    }
                    Thread.Sleep(1000);
                }
                return true;
            }

            private void AddGlobalHistory(OperationSummary summary)
            {
                if (config.GlobalHistory == null) config.GlobalHistory = new List<GlobalHistoryEntry>();
                config.GlobalHistory.Add(new GlobalHistoryEntry
                {
                    Timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                    Source = summary.Source,
                    Mode = summary.Mode == OperationMode.Backup ? "Sicherung" : "Verbindungstest",
                    Total = summary.Total,
                    OkCount = summary.OkCount,
                    ErrorCount = summary.ErrorCount,
                    CancelledCount = summary.CancelledCount,
                    Duration = summary.Duration.ToString(@"hh\:mm\:ss"),
                    LogPath = summary.LogPath
                });
                while (config.GlobalHistory.Count > 200) config.GlobalHistory.RemoveAt(0);
            }
    
            private void AddHistory(AccountConfig a, OperationMode mode, string result, string details, TimeSpan duration)
            {
                if (a.History == null) a.History = new List<HistoryEntry>();
                a.History.Add(new HistoryEntry
                {
                    Timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                    Mode = mode == OperationMode.Backup ? "Sicherung" : "Verbindungstest",
                    Result = result,
                    Details = details,
                    Duration = duration.ToString(@"hh\:mm\:ss")
                });
                while (a.History.Count > 50) a.History.RemoveAt(0);
            }
    
            private void CleanupOldLogs(string logDir)
            {
                try
                {
                    DateTime limit = DateTime.Now.AddDays(-Math.Max(1, config.LogRetentionDays));
                    foreach (string file in Directory.GetFiles(logDir, "*.log"))
                    {
                        try { if (File.GetLastWriteTime(file) < limit) File.Delete(file); } catch { }
                    }
                }
                catch { }
            }
    
            private void DecryptSecretToFile(AccountConfig a, string tempPass)
            {
                string secret = GetSecretPath(a.Id);
                if (!File.Exists(secret)) throw new FileNotFoundException("Gespeichertes Kennwort fehlt.", secret);
                Directory.CreateDirectory(tempDir);
    
                var psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; $e=(Get-Content -Raw -LiteralPath $env:SECRET_FILE).Trim(); $s=ConvertTo-SecureString $e; $b=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($s); try { $p=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($b); $enc=New-Object System.Text.UTF8Encoding($false); [IO.File]::WriteAllText($env:TEMP_PASS,$p,$enc) } finally { if($b -ne [IntPtr]::Zero){ [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) } }\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardError = true;
                psi.EnvironmentVariables["SECRET_FILE"] = secret;
                psi.EnvironmentVariables["TEMP_PASS"] = tempPass;
                using (var p = Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new InvalidOperationException(String.IsNullOrWhiteSpace(err) ? "Kennwort konnte nicht entschlüsselt werden." : err.Trim());
                }
            }
    
            private int RunBackupProcess(PythonCommand py, AccountConfig a, string tempPass, StreamWriter log, int total, int completedBefore)
            {
                string accountDir = Path.Combine(config.BackupRoot, a.Id);
                Directory.CreateDirectory(accountDir);
    
                var args = new List<string>();
                args.AddRange(py.Prefix);
                args.Add("-u");
                args.Add(ImapBackupCompatibility.EnsureScript(tempDir));
                args.Add("-e");
                args.Add("-s"); args.Add(a.Host);
                args.Add("-P"); args.Add(a.Port.ToString());
                args.Add("-u"); args.Add(a.User);
                args.Add("-p"); args.Add("@" + tempPass);
                args.Add("-m"); args.Add("mbox");
                args.Add("-t"); args.Add((config.LargeMailboxMode ? Math.Max(60, config.ImapTimeoutSeconds) : 120).ToString());
    
                return RunLoggedProcess(py.FileName, args, accountDir, log, a, total, completedBefore);
            }
    
            private int RunTestProcess(PythonCommand py, AccountConfig a, string tempPass, StreamWriter log)
            {
                string scriptPath = Path.Combine(tempDir, "imap_test.py");
                File.WriteAllText(scriptPath,
                    "import imaplib, sys\n" +
                    "host=sys.argv[1]; port=int(sys.argv[2]); user=sys.argv[3]\n" +
                    "with open(sys.argv[4], 'r', encoding='utf-8') as f: pwd=f.read()\n" +
                    "m=imaplib.IMAP4_SSL(host, port, timeout=30)\n" +
                    "m.login(user, pwd)\n" +
                    "typ, data=m.list()\n" +
                    "print('Anmeldung erfolgreich')\n" +
                    "print('IMAP-Ordner: %d' % (len(data or [])))\n" +
                    "m.logout()\n", new UTF8Encoding(false));
    
                var args = new List<string>();
                args.AddRange(py.Prefix);
                args.Add("-u");
                args.Add(scriptPath);
                args.Add(a.Host);
                args.Add(a.Port.ToString());
                args.Add(a.User);
                args.Add(tempPass);
                return RunLoggedProcess(py.FileName, args, config.BackupRoot, log, a, 1, 0);
            }
    
            private int RunLoggedProcess(string fileName, List<string> args, string workingDir, StreamWriter log, AccountConfig a, int total, int completedBefore)
            {
                var psi = new ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = JoinArgs(args);
                psi.WorkingDirectory = workingDir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
    
                var p = new Process();
                p.StartInfo = psi;
                p.EnableRaisingEvents = true;
    
                DataReceivedEventHandler handler = delegate(object sender, DataReceivedEventArgs ev)
                {
                    if (String.IsNullOrEmpty(ev.Data)) return;
                    lock (log)
                    {
                        log.WriteLine(ev.Data);
                        log.Flush();
                    }
                    Report(a.Id, "Läuft ...", ev.Data, completedBefore, total, true);
                };
                p.OutputDataReceived += handler;
                p.ErrorDataReceived += handler;
    
                lock (processLock) currentProcess = p;
                try
                {
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    DateTime lastSizeReport = DateTime.MinValue;
                    while (!p.WaitForExit(250))
                    {
                        if (cancelRequested || worker.CancellationPending)
                        {
                            try { p.Kill(); } catch { }
                            return 1223;
                        }
                        if (config.LargeMailboxMode && (DateTime.Now - lastSizeReport).TotalSeconds >= 5)
                        {
                            lastSizeReport = DateTime.Now;
                            long currentBytes = BackupTools.GetDirectorySize(Path.Combine(config.BackupRoot, a.Id));
                            Report(a.Id, "Läuft ...", String.Format("{0}: lokale Sicherungsgröße aktuell {1}", a.Name, BackupTools.FormatBytes(currentBytes)), completedBefore, total, false);
                        }
                    }
                    p.WaitForExit();
                    return p.ExitCode;
                }
                finally
                {
                    lock (processLock) currentProcess = null;
                    p.Dispose();
                }
            }
    
            private void Report(string accountId, string status, string message, int completed, int total, bool appendLog)
            {
                worker.ReportProgress(0, new ProgressInfo
                {
                    AccountId = accountId,
                    Status = status,
                    Message = message,
                    Completed = completed,
                    Total = total,
                    AppendLog = appendLog
                });
            }
    
            private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
            {
                var info = e.UserState as ProgressInfo;
                if (info == null) return;
                if (!String.IsNullOrEmpty(info.AccountId) && !String.IsNullOrEmpty(info.Status)) SetRowStatus(info.AccountId, info.Status);
                progressOverall.Maximum = Math.Max(1, info.Total);
                progressOverall.Value = Math.Max(0, Math.Min(progressOverall.Maximum, info.Completed));
                if (!String.IsNullOrEmpty(info.Message))
                {
                    lblProgress.Tag = info.Message;
                    lblProgress.Text = info.Message;
                    if (info.AppendLog) AppendLog(info.Message);
                }
            }
    
            private void AppendLog(string line)
            {
                if (String.IsNullOrEmpty(line)) return;
                if (txtLog.TextLength > 150000)
                {
                    txtLog.Select(0, 50000);
                    txtLog.SelectedText = "";
                }
                txtLog.AppendText(line + Environment.NewLine);
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            }
    
            private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
            {
                elapsedTimer.Stop();
                progressCurrent.MarqueeAnimationSpeed = 0;
                PowerManager.SetKeepAwake(false);
                SetBusy(false);
                try { SaveConfig(); } catch { }
                RefreshGrid();
    
                if (e.Cancelled || cancelRequested)
                {
                    lblProgress.Tag = "Abgebrochen";
                    lblProgress.Text = "Vorgang abgebrochen.";
                    ShowNotification("CM IMAP Sicherung", "Vorgang abgebrochen.", ToolTipIcon.Warning);
                }
                else if (e.Error != null)
                {
                    lblProgress.Tag = "Fehler";
                    lblProgress.Text = "Fehler: " + e.Error.Message;
                    AppendLog(e.Error.ToString());
                    ShowNotification("CM IMAP Sicherung", "Es ist ein Fehler aufgetreten.", ToolTipIcon.Error);
                }
                else
                {
                    progressOverall.Value = progressOverall.Maximum;
                    TimeSpan t = DateTime.Now - operationStarted;
                    var summary = e.Result as OperationSummary;
                    if (summary != null)
                    {
                        lblProgress.Text = String.Format("Abgeschlossen   |   OK: {0}   Fehler: {1}   Laufzeit {2:hh\\:mm\\:ss}", summary.OkCount, summary.ErrorCount, t);
                        ShowNotification("CM IMAP Sicherung", String.Format("Fertig: {0} OK, {1} Fehler", summary.OkCount, summary.ErrorCount), summary.ErrorCount > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
                    }
                    else
                    {
                        lblProgress.Text = String.Format("Abgeschlossen   |   Laufzeit {0:hh\\:mm\\:ss}", t);
                        ShowNotification("CM IMAP Sicherung", "Vorgang abgeschlossen.", ToolTipIcon.Info);
                    }
                    lblProgress.Tag = "Abgeschlossen";
                }
            

                if (scheduledMode)
                {
                    var exitTimer = new System.Windows.Forms.Timer();
                    exitTimer.Interval = 4500;
                    exitTimer.Tick += delegate { exitTimer.Stop(); exitTimer.Dispose(); allowApplicationExit = true; Application.Exit(); };
                    exitTimer.Start();
                }
            }
    
            private void RefreshLastBackupCells()
            {
                foreach (DataGridViewRow r in grid.Rows)
                {
                    var a = r.Tag as AccountConfig;
                    if (a != null)
                    {
                        r.Cells["Last"].Value = a.LastBackup;
                        r.Cells["Size"].Value = GetAccountSizeText(a);
                    }
                }
                UpdateStorageSummary();
            }
    
            private void BtnOpenBackup_Click(object sender, EventArgs e)
            {
                UpdateBackupRootFromUi();
                try
                {
                    Directory.CreateDirectory(config.BackupRoot);
                    Process.Start("explorer.exe", QuoteArg(config.BackupRoot));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Backup-Ordner", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
    
            private void BtnOpenLog_Click(object sender, EventArgs e)
            {
                try
                {
                    if (!String.IsNullOrEmpty(lastLogPath) && File.Exists(lastLogPath))
                    {
                        Process.Start("notepad.exe", QuoteArg(lastLogPath));
                        return;
                    }
                    string dir = Path.Combine(txtBackupRoot.Text.Trim(), "_Logs");
                    if (Directory.Exists(dir)) Process.Start("explorer.exe", QuoteArg(dir));
                    else MessageBox.Show("Es ist noch keine Logdatei vorhanden.", "Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Log", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
    
            private void TryDelete(string path)
            {
                try { if (!String.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
            }
    
            private static string JoinArgs(IEnumerable<string> args)
            {
                return String.Join(" ", args.Select(QuoteArg).ToArray());
            }
    
            private static string QuoteArg(string arg)
            {
                if (arg == null) return "\"\"";
                if (arg.Length > 0 && arg.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) < 0) return arg;
                var sb = new StringBuilder();
                sb.Append('"');
                int backslashes = 0;
                foreach (char c in arg)
                {
                    if (c == '\\')
                    {
                        backslashes++;
                    }
                    else if (c == '"')
                    {
                        sb.Append('\\', backslashes * 2 + 1);
                        sb.Append('"');
                        backslashes = 0;
                    }
                    else
                    {
                        sb.Append('\\', backslashes);
                        backslashes = 0;
                        sb.Append(c);
                    }
                }
                sb.Append('\\', backslashes * 2);
                sb.Append('"');
                return sb.ToString();
            }
        }
}
