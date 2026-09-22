using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public class AdvancedToolsForm : Form
    {
        private readonly AppConfig config;
        private readonly string secretDir;
        private readonly string tempDir;
        private ComboBox cmbAccount;
        private TextBox txtLog;
        private Button btnVerify;
        private Button btnCreateManifest;
        private Button btnZipPassword;
        private Button btnZip;
        private Button btnCancel;
        private TabControl tabs;
        private BackgroundWorker worker;
        private string operation;
        private volatile bool cancelRequested;

        public AdvancedToolsForm(AppConfig cfg, string secretDir, string tempDir)
        {
            config = cfg;
            this.secretDir = secretDir;
            this.tempDir = tempDir;
            Text = "Backup-Werkzeuge";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 560);
            MinimumSize = new Size(720, 480);
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
            root.Padding = new Padding(12);
            root.RowCount = 5;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = "Backup-Werkzeuge";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            title.Margin = new Padding(0, 0, 0, 10);
            root.Controls.Add(title, 0, 0);

            var row = new TableLayoutPanel();
            row.Dock = DockStyle.Top;
            row.AutoSize = true;
            row.ColumnCount = 2;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.Controls.Add(new Label { Text = "Konto:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 10, 0) }, 0, 0);
            cmbAccount = new ComboBox();
            cmbAccount.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAccount.Dock = DockStyle.Fill;
            row.Controls.Add(cmbAccount, 1, 0);
            root.Controls.Add(row, 0, 1);

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Height = 220;
            var tabVerify = new TabPage("Backup-Prüfung");
            var pv = new FlowLayoutPanel();
            pv.Dock = DockStyle.Fill;
            pv.Padding = new Padding(12);
            pv.FlowDirection = FlowDirection.TopDown;
            pv.WrapContents = false;
            pv.Controls.Add(new Label { Text = "Die Prüfung verwendet einen SHA-256-Prüfnachweis für alle Sicherungsdateien.", AutoSize = true, MaximumSize = new Size(700, 0) });
            btnVerify = new Button { Text = "Backup prüfen", Width = 160, Height = 32, Margin = new Padding(0, 12, 0, 0) };
            btnVerify.Click += delegate { StartOperation("verify"); };
            pv.Controls.Add(btnVerify);
            btnCreateManifest = new Button { Text = "Prüfnachweis neu erstellen", Width = 220, Height = 32 };
            btnCreateManifest.Click += delegate { StartOperation("manifest"); };
            pv.Controls.Add(btnCreateManifest);
            tabVerify.Controls.Add(pv);

            var tabZip = new TabPage("ZIP-Komprimierung");
            var pz = new FlowLayoutPanel();
            pz.Dock = DockStyle.Fill;
            pz.Padding = new Padding(12);
            pz.FlowDirection = FlowDirection.TopDown;
            pz.WrapContents = false;
            pz.Controls.Add(new Label { Text = "Erstellt eine AES-256-verschlüsselte ZIP-Sicherung des gewählten Kontos. Das ZIP-Kennwort wird mit Windows-DPAPI geschützt gespeichert.", AutoSize = true, MaximumSize = new Size(700, 0) });
            btnZipPassword = new Button { Text = "ZIP-Kennwort setzen/ändern", Width = 220, Height = 32, Margin = new Padding(0, 12, 0, 0) };
            btnZipPassword.Click += BtnZipPassword_Click;
            pz.Controls.Add(btnZipPassword);
            btnZip = new Button { Text = "Verschlüsseltes ZIP erstellen", Width = 220, Height = 32 };
            btnZip.Click += delegate { StartOperation("zip"); };
            pz.Controls.Add(btnZip);
            tabZip.Controls.Add(pz);

            tabs.TabPages.Add(tabVerify);
            tabs.TabPages.Add(tabZip);
            root.Controls.Add(tabs, 0, 2);

            txtLog = new TextBox();
            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.BackColor = UiTheme.LogBack;
            txtLog.ForeColor = UiTheme.LogText;
            root.Controls.Add(txtLog, 0, 3);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            var close = new Button { Text = "Schließen", Width = 95 };
            close.Click += delegate { if (worker.IsBusy) { MessageBox.Show("Bitte zuerst den laufenden Vorgang beenden."); return; } Close(); };
            btnCancel = new Button { Text = "Abbrechen", Width = 95, Enabled = false };
            btnCancel.Click += delegate { cancelRequested = true; worker.CancelAsync(); };
            buttons.Controls.Add(close);
            buttons.Controls.Add(btnCancel);
            root.Controls.Add(buttons, 0, 4);
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
            foreach (var a in config.Accounts) cmbAccount.Items.Add(new AccountItem(a));
            if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;
        }

        private AccountConfig SelectedAccount { get { var item = cmbAccount.SelectedItem as AccountItem; return item == null ? null : item.Account; } }

        private void BtnZipPassword_Click(object sender, EventArgs e)
        {
            using (var d = new PasswordForm("ZIP-Archiv"))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string err;
                string secret = Path.Combine(secretDir, BackupTools.ArchiveSecretName);
                if (!BackupTools.SaveProtectedSecret(secret, d.PasswordValue, out err)) MessageBox.Show("ZIP-Kennwort konnte nicht gespeichert werden.\r\n\r\n" + err, "ZIP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else MessageBox.Show("ZIP-Kennwort wurde Windows-verschlüsselt gespeichert.", "ZIP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void StartOperation(string op)
        {
            if (worker.IsBusy) return;
            var a = SelectedAccount;
            if (a == null) return;
            if (op == "zip" && !File.Exists(Path.Combine(secretDir, BackupTools.ArchiveSecretName)))
            {
                MessageBox.Show("Bitte zuerst ein ZIP-Kennwort setzen.", "ZIP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            operation = op;
            cancelRequested = false;
            txtLog.Clear();
            if (config.LargeMailboxMode && config.PreventSleepDuringLongOperations && op == "zip") PowerManager.SetKeepAwake(true);
            SetBusy(true);
            worker.RunWorkerAsync(a);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (operation == "verify" || operation == "manifest")
            {
                var a = e.Argument as AccountConfig;
                string dir = Path.Combine(config.BackupRoot, a.Id);
                VerificationResult r = operation == "manifest" ? BackupTools.CreateManifestAndVerify(dir) : BackupTools.VerifyManifest(dir);
                e.Result = r;
                return;
            }

            PythonCommand py = BackupTools.DetectPython();
            if (py == null) throw new InvalidOperationException("Python 3 wurde nicht gefunden.");
            if (operation == "zip") EnsurePyzipper(py);

            if (operation == "zip")
            {
                var a = e.Argument as AccountConfig;
                string dir = Path.Combine(config.BackupRoot, a.Id);
                if (!Directory.Exists(dir)) throw new DirectoryNotFoundException(dir);
                string pass = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".zip.pwd");
                string err;
                if (!BackupTools.DecryptProtectedSecret(Path.Combine(secretDir, BackupTools.ArchiveSecretName), pass, out err)) throw new InvalidOperationException(err);
                try
                {
                    string archiveDir = Path.Combine(config.BackupRoot, "_Archive", a.Id);
                    Directory.CreateDirectory(archiveDir);
                    long sourceBytes = BackupTools.GetDirectorySize(dir);
                    long freeBytes, totalBytes;
                    if (BackupTools.TryGetFreeSpace(config.BackupRoot, out freeBytes, out totalBytes))
                    {
                        long reserve = sourceBytes + (2L * 1024L * 1024L * 1024L);
                        if (freeBytes < reserve) throw new InvalidOperationException("Zu wenig freier Speicher für ZIP. Frei: " + BackupTools.FormatBytes(freeBytes) + ", empfohlene Reserve: " + BackupTools.FormatBytes(reserve));
                    }
                    string zip = Path.Combine(archiveDir, String.Format("{0}_{1:yyyy-MM-dd_HHmmss}.zip", SafeName(a.Id), DateTime.Now));
                    string output;
                    int rc = BackupTools.CreateEncryptedZip(py, dir, zip, pass, tempDir, delegate { return cancelRequested || worker.CancellationPending; }, out output);
                    if (rc != 0) throw new InvalidOperationException(output);
                    long maxArchiveBytes = (long)Math.Max(1, config.ZipMaxStorageGB) * 1024L * 1024L * 1024L;
                    BackupTools.CleanupArchives(archiveDir, Math.Max(1, config.ZipKeepCount), maxArchiveBytes);
                    e.Result = "ZIP erfolgreich erstellt:\r\n" + zip + "\r\n\r\n" + output;
                }
                finally { TryDelete(pass); }
                return;
            }
        }

        private void EnsurePyzipper(PythonCommand py)
        {
            if (BackupTools.CheckModule(py, "pyzipper")) return;
            string output;
            if (!BackupTools.InstallModule(py, "pyzipper", out output)) throw new InvalidOperationException("pyzipper konnte nicht installiert werden.\r\n" + output);
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            PowerManager.SetKeepAwake(false);
            SetBusy(false);
            if (e.Cancelled || cancelRequested) { txtLog.AppendText("Vorgang abgebrochen." + Environment.NewLine); return; }
            if (e.Error != null) { txtLog.AppendText("FEHLER: " + e.Error.Message + Environment.NewLine); MessageBox.Show(e.Error.Message, "Backup-Werkzeuge", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            var vr = e.Result as VerificationResult;
            if (vr != null)
            {
                txtLog.AppendText(vr.Message + Environment.NewLine);
                MessageBox.Show(vr.Message, "Backup-Prüfung", MessageBoxButtons.OK, vr.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return;
            }
            txtLog.AppendText((e.Result == null ? "Fertig" : e.Result.ToString()) + Environment.NewLine);
            MessageBox.Show("Vorgang abgeschlossen.", "Backup-Werkzeuge", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetBusy(bool busy)
        {
            cmbAccount.Enabled = !busy;
            btnVerify.Enabled = !busy;
            btnCreateManifest.Enabled = !busy;
            btnZipPassword.Enabled = !busy;
            btnZip.Enabled = !busy;
            btnCancel.Enabled = busy;
        }

        private static string SafeName(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }
        private static void TryDelete(string p) { try { if (!String.IsNullOrWhiteSpace(p) && File.Exists(p)) File.Delete(p); } catch { } }

        private class AccountItem
        {
            public AccountConfig Account;
            public AccountItem(AccountConfig a) { Account = a; }
            public override string ToString() { return Account.Name + "  (" + Account.User + ")"; }
        }
    }
}
