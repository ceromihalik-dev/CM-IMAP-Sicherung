using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public class RestoreForm : Form
    {
        private readonly AppConfig config;
        private readonly string secretDir;
        private readonly string tempDir;
        private readonly string initialAccountId;

        private ComboBox cmbAccount;
        private TextBox txtSource;
        private Label lblSourceInfo;
        private Label lblTargetInfo;
        private TextBox txtLog;
        private ProgressBar progress;
        private Button btnStart;
        private Button btnCancel;
        private Button btnClose;
        private BackgroundWorker worker;
        private volatile bool cancelRequested;

        public RestoreForm(AppConfig config, string secretDir, string tempDir, string initialAccountId)
        {
            this.config = config;
            this.secretDir = secretDir;
            this.tempDir = tempDir;
            this.initialAccountId = initialAccountId;

            Text = "Backup wiederherstellen - CM IMAP Sicherung";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 620);
            Size = new Size(920, 680);
            Font = new Font("Segoe UI", 9F);
            BackColor = UiTheme.Window;

            BuildUi();
            SetupWorker();
            FillAccounts();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = UiTheme.Header;
            header.Margin = new Padding(0, 0, 0, 12);
            header.Padding = new Padding(18, 14, 18, 12);
            root.Controls.Add(header, 0, 0);

            var title = new Label();
            title.Text = "Backup wiederherstellen";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(16, 12);
            header.Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = "MBOX-Backupordner oder verschlüsseltes CM-IMAP-ZIP in ein IMAP-Konto zurückspielen";
            subtitle.AutoSize = true;
            subtitle.ForeColor = Color.FromArgb(218, 226, 237);
            subtitle.Location = new Point(18, 53);
            header.Controls.Add(subtitle);

            var warning = new Panel();
            warning.Dock = DockStyle.Top;
            warning.Height = 62;
            warning.BackColor = UiTheme.WarningBack;
            warning.Margin = new Padding(0, 0, 0, 12);
            warning.Padding = new Padding(14, 11, 14, 8);
            warning.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(warning, 0, 1);

            var warningTitle = new Label();
            warningTitle.Text = "Wichtiger Hinweis";
            warningTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            warningTitle.ForeColor = UiTheme.Warning;
            warningTitle.AutoSize = true;
            warning.Controls.Add(warningTitle);

            var warningText = new Label();
            warningText.Text = "Die Wiederherstellung löscht keine vorhandenen E-Mails. Wenn Nachrichten bereits im Zielkonto vorhanden sind, können Duplikate entstehen.";
            warningText.ForeColor = UiTheme.Text;
            warningText.AutoSize = true;
            warningText.MaximumSize = new Size(820, 0);
            warningText.Location = new Point(12, 31);
            warning.Controls.Add(warningText);

            var card = new TableLayoutPanel();
            card.Dock = DockStyle.Top;
            card.AutoSize = true;
            card.BackColor = UiTheme.Surface;
            card.Padding = new Padding(16);
            card.Margin = new Padding(0, 0, 0, 12);
            card.ColumnCount = 3;
            card.RowCount = 5;
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145F));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(card, 0, 2);

            AddLabel(card, 0, "Zielkonto:");
            cmbAccount = new ComboBox();
            cmbAccount.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAccount.Dock = DockStyle.Fill;
            cmbAccount.Margin = new Padding(0, 2, 0, 8);
            cmbAccount.SelectedIndexChanged += delegate { UpdateTargetInfo(); };
            card.Controls.Add(cmbAccount, 1, 0);
            card.SetColumnSpan(cmbAccount, 2);

            lblTargetInfo = new Label();
            lblTargetInfo.Text = "—";
            lblTargetInfo.AutoSize = true;
            lblTargetInfo.ForeColor = UiTheme.Muted;
            lblTargetInfo.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(lblTargetInfo, 1, 1);
            card.SetColumnSpan(lblTargetInfo, 2);

            AddLabel(card, 2, "Backup-Quelle:");
            txtSource = new TextBox();
            txtSource.Dock = DockStyle.Fill;
            txtSource.Margin = new Padding(0, 2, 8, 8);
            txtSource.TextChanged += delegate { UpdateSourceInfo(); };
            card.Controls.Add(txtSource, 1, 2);

            var browsePanel = new FlowLayoutPanel();
            browsePanel.AutoSize = true;
            browsePanel.WrapContents = false;
            browsePanel.Margin = new Padding(0);
            var btnFolder = new Button();
            btnFolder.Text = "Ordner...";
            btnFolder.Width = 82;
            btnFolder.Height = 30;
            UiTheme.ApplySecondary(btnFolder);
            btnFolder.Click += BrowseFolder_Click;
            var btnZip = new Button();
            btnZip.Text = "ZIP...";
            btnZip.Width = 72;
            btnZip.Height = 30;
            UiTheme.ApplySecondary(btnZip);
            btnZip.Click += BrowseZip_Click;
            browsePanel.Controls.Add(btnFolder);
            browsePanel.Controls.Add(btnZip);
            card.Controls.Add(browsePanel, 2, 2);

            lblSourceInfo = new Label();
            lblSourceInfo.Text = "Bitte Backupordner oder ZIP-Datei auswählen.";
            lblSourceInfo.AutoSize = true;
            lblSourceInfo.ForeColor = UiTheme.Muted;
            lblSourceInfo.Margin = new Padding(0, 0, 0, 4);
            card.Controls.Add(lblSourceInfo, 1, 3);
            card.SetColumnSpan(lblSourceInfo, 2);

            var progressPanel = new TableLayoutPanel();
            progressPanel.Dock = DockStyle.Top;
            progressPanel.AutoSize = true;
            progressPanel.ColumnCount = 1;
            progressPanel.Margin = new Padding(0, 0, 0, 10);
            progress = new ProgressBar();
            progress.Dock = DockStyle.Top;
            progress.Height = 18;
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 0;
            progressPanel.Controls.Add(progress, 0, 0);
            root.Controls.Add(progressPanel, 0, 3);

            txtLog = new TextBox();
            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.BackColor = UiTheme.LogBack;
            txtLog.ForeColor = UiTheme.LogText;
            txtLog.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(txtLog, 0, 4);

            var actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.AutoSize = true;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.Margin = new Padding(0, 12, 0, 0);

            btnStart = new Button();
            btnStart.Text = "Wiederherstellung starten";
            btnStart.Width = 190;
            btnStart.Height = 36;
            UiTheme.ApplyWarning(btnStart);
            btnStart.Click += StartRestore_Click;

            btnCancel = new Button();
            btnCancel.Text = "Abbrechen";
            btnCancel.Width = 105;
            btnCancel.Height = 36;
            btnCancel.Enabled = false;
            UiTheme.ApplyDanger(btnCancel);
            btnCancel.Click += delegate { cancelRequested = true; if (worker.IsBusy) worker.CancelAsync(); };

            btnClose = new Button();
            btnClose.Text = "Schließen";
            btnClose.Width = 105;
            btnClose.Height = 36;
            UiTheme.ApplySecondary(btnClose);
            btnClose.Click += delegate
            {
                if (worker.IsBusy)
                {
                    MessageBox.Show("Bitte den laufenden Vorgang zuerst abschließen oder abbrechen.", "Wiederherstellung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Close();
            };

            actions.Controls.Add(btnStart);
            actions.Controls.Add(btnCancel);
            actions.Controls.Add(btnClose);
            root.Controls.Add(actions, 0, 5);
        }

        private void AddLabel(TableLayoutPanel table, int row, string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.ForeColor = UiTheme.Text;
            label.Margin = new Padding(0, 7, 12, 8);
            table.Controls.Add(label, 0, row);
        }

        private void SetupWorker()
        {
            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_Completed;
        }

        private void FillAccounts()
        {
            cmbAccount.Items.Clear();
            foreach (var account in config.Accounts)
                cmbAccount.Items.Add(new AccountItem(account));

            if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;

            if (!String.IsNullOrWhiteSpace(initialAccountId))
            {
                for (int i = 0; i < cmbAccount.Items.Count; i++)
                {
                    var item = cmbAccount.Items[i] as AccountItem;
                    if (item != null && String.Equals(item.Account.Id, initialAccountId, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbAccount.SelectedIndex = i;
                        break;
                    }
                }
            }
            UpdateTargetInfo();
        }

        private AccountConfig SelectedAccount
        {
            get
            {
                var item = cmbAccount.SelectedItem as AccountItem;
                return item == null ? null : item.Account;
            }
        }

        private void UpdateTargetInfo()
        {
            var account = SelectedAccount;
            if (account == null)
            {
                lblTargetInfo.Text = "Kein Zielkonto ausgewählt.";
                return;
            }
            string secretState = File.Exists(Path.Combine(secretDir, account.Id + ".cred")) ? "Kennwort gespeichert" : "Kennwort fehlt";
            lblTargetInfo.Text = account.User + "  •  " + account.Host + ":" + account.Port + "  •  " + secretState;
        }

        private void UpdateSourceInfo()
        {
            try
            {
                string source = txtSource.Text.Trim();
                if (Directory.Exists(source))
                {
                    long size = BackupTools.GetDirectorySize(source);
                    int mboxCount = Directory.GetFiles(source, "*.mbox", SearchOption.AllDirectories).Length;
                    lblSourceInfo.Text = "Backupordner  •  " + mboxCount + " MBOX-Dateien  •  " + BackupTools.FormatBytes(size);
                    lblSourceInfo.ForeColor = UiTheme.Success;
                    return;
                }
                if (File.Exists(source))
                {
                    var fi = new FileInfo(source);
                    lblSourceInfo.Text = "ZIP-Datei  •  " + BackupTools.FormatBytes(fi.Length);
                    lblSourceInfo.ForeColor = UiTheme.Success;
                    return;
                }
            }
            catch { }
            lblSourceInfo.Text = "Bitte gültigen Backupordner oder ZIP-Datei auswählen.";
            lblSourceInfo.ForeColor = UiTheme.Muted;
        }

        private void BrowseFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "MBOX-Backupordner auswählen";
                if (Directory.Exists(txtSource.Text)) dialog.SelectedPath = txtSource.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) txtSource.Text = dialog.SelectedPath;
            }
        }

        private void BrowseZip_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "CM IMAP ZIP (*.zip)|*.zip|Alle Dateien (*.*)|*.*";
                dialog.Title = "Verschlüsseltes Backup-ZIP auswählen";
                if (dialog.ShowDialog(this) == DialogResult.OK) txtSource.Text = dialog.FileName;
            }
        }

        private bool EnsureImapPassword(AccountConfig account)
        {
            string secret = Path.Combine(secretDir, account.Id + ".cred");
            if (File.Exists(secret)) return true;

            var ask = MessageBox.Show("Für das Zielkonto ist noch kein IMAP-Kennwort gespeichert.\r\n\r\nKennwort jetzt hinterlegen?", "IMAP-Kennwort", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask != DialogResult.Yes) return false;

            using (var dialog = new PasswordForm(account.User))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                string error;
                if (!BackupTools.SaveProtectedSecret(secret, dialog.PasswordValue, out error))
                {
                    MessageBox.Show("Kennwort konnte nicht gespeichert werden.\r\n\r\n" + error, "IMAP-Kennwort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            UpdateTargetInfo();
            return true;
        }

        private bool EnsureZipPassword()
        {
            string secret = Path.Combine(secretDir, BackupTools.ArchiveSecretName);
            if (File.Exists(secret)) return true;

            var ask = MessageBox.Show("Für das ZIP-Archiv ist noch kein gespeichertes ZIP-Kennwort vorhanden.\r\n\r\nKennwort jetzt eingeben?", "ZIP-Kennwort", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask != DialogResult.Yes) return false;

            using (var dialog = new PasswordForm("ZIP-Archiv"))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                string error;
                if (!BackupTools.SaveProtectedSecret(secret, dialog.PasswordValue, out error))
                {
                    MessageBox.Show("ZIP-Kennwort konnte nicht gespeichert werden.\r\n\r\n" + error, "ZIP-Kennwort", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            return true;
        }

        private void StartRestore_Click(object sender, EventArgs e)
        {
            if (worker.IsBusy) return;
            var account = SelectedAccount;
            if (account == null)
            {
                MessageBox.Show("Bitte ein Zielkonto auswählen.", "Wiederherstellung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string source = txtSource.Text.Trim();
            if (!Directory.Exists(source) && !File.Exists(source))
            {
                MessageBox.Show("Die gewählte Backup-Quelle wurde nicht gefunden.", "Wiederherstellung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EnsureImapPassword(account)) return;
            bool isZip = File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            if (isZip && !EnsureZipPassword()) return;

            long freeBytes, totalBytes;
            if (config.LargeMailboxMode && BackupTools.TryGetFreeSpace(config.BackupRoot, out freeBytes, out totalBytes))
            {
                long reserve = (long)Math.Max(5, config.LargeMailboxMinFreeGB) * 1024L * 1024L * 1024L;
                if (freeBytes < reserve)
                {
                    var proceed = MessageBox.Show(
                        "Der freie Speicher liegt unter der empfohlenen Reserve.\r\n\r\nFrei: " + BackupTools.FormatBytes(freeBytes) +
                        "\r\nEmpfohlen: " + BackupTools.FormatBytes(reserve) + "\r\n\r\nTrotzdem fortfahren?",
                        "Speicherwarnung", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (proceed != DialogResult.Yes) return;
                }
            }

            var confirm = MessageBox.Show(
                "Backup jetzt in dieses IMAP-Konto wiederherstellen?\r\n\r\n" + account.User + "\r\n" + account.Host + ":" + account.Port +
                "\r\n\r\nVorhandene Nachrichten werden nicht gelöscht. Duplikate sind möglich.",
                "Wiederherstellung bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            cancelRequested = false;
            txtLog.Clear();
            txtLog.AppendText("Wiederherstellung wird vorbereitet ..." + Environment.NewLine);
            SetBusy(true);
            if (config.LargeMailboxMode && config.PreventSleepDuringLongOperations) PowerManager.SetKeepAwake(true);
            worker.RunWorkerAsync(new RestoreRequest { Account = account, Source = source, IsZip = isZip });
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var request = e.Argument as RestoreRequest;
            if (request == null) return;

            PythonCommand python = BackupTools.DetectPython();
            if (python == null) throw new InvalidOperationException("Python 3 wurde nicht gefunden.");

            if (request.IsZip && !BackupTools.CheckModule(python, "pyzipper"))
            {
                string installOutput;
                if (!BackupTools.InstallModule(python, "pyzipper", out installOutput))
                    throw new InvalidOperationException("pyzipper konnte nicht installiert werden.\r\n\r\n" + installOutput);
            }

            string imapPass = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".imap.pwd");
            string zipPass = "";
            string error;
            if (!BackupTools.DecryptProtectedSecret(Path.Combine(secretDir, request.Account.Id + ".cred"), imapPass, out error))
                throw new InvalidOperationException(error);

            try
            {
                if (request.IsZip)
                {
                    zipPass = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".zip.pwd");
                    if (!BackupTools.DecryptProtectedSecret(Path.Combine(secretDir, BackupTools.ArchiveSecretName), zipPass, out error))
                        throw new InvalidOperationException(error);
                }

                string restoreTemp = config.LargeMailboxMode ? Path.Combine(config.BackupRoot, "_RestoreTemp") : tempDir;
                Directory.CreateDirectory(restoreTemp);
                int timeout = config.LargeMailboxMode ? Math.Max(60, config.ImapTimeoutSeconds) : 600;
                string output;
                int rc = BackupTools.RestoreToImap(
                    python, request.Account, imapPass, request.Source, zipPass, restoreTemp, timeout,
                    delegate { return cancelRequested || worker.CancellationPending; }, out output);

                if (rc == 1223)
                {
                    e.Cancel = true;
                    return;
                }
                if (rc != 0) throw new InvalidOperationException(output);
                e.Result = output;
            }
            finally
            {
                TryDelete(imapPass);
                TryDelete(zipPass);
            }
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            PowerManager.SetKeepAwake(false);
            SetBusy(false);

            if (e.Cancelled || cancelRequested)
            {
                txtLog.AppendText("Wiederherstellung abgebrochen." + Environment.NewLine);
                return;
            }
            if (e.Error != null)
            {
                txtLog.AppendText("FEHLER: " + e.Error.Message + Environment.NewLine);
                MessageBox.Show(e.Error.Message, "Wiederherstellung", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string result = e.Result == null ? "Wiederherstellung abgeschlossen." : e.Result.ToString();
            txtLog.AppendText(result + Environment.NewLine);
            MessageBox.Show("Die Wiederherstellung wurde abgeschlossen.\r\n\r\nBitte das Zielpostfach stichprobenartig prüfen.", "Wiederherstellung", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetBusy(bool busy)
        {
            cmbAccount.Enabled = !busy;
            txtSource.Enabled = !busy;
            btnStart.Enabled = !busy;
            btnCancel.Enabled = busy;
            btnClose.Enabled = !busy;
            progress.MarqueeAnimationSpeed = busy ? 30 : 0;
        }

        private static void TryDelete(string path)
        {
            try { if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); } catch { }
        }

        private class RestoreRequest
        {
            public AccountConfig Account;
            public string Source;
            public bool IsZip;
        }

        private class AccountItem
        {
            public AccountConfig Account;
            public AccountItem(AccountConfig account) { Account = account; }
            public override string ToString() { return Account.Name + "  (" + Account.User + ")"; }
        }
    }
}
