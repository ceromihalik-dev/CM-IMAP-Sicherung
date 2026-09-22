using System;
using System.Drawing;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public class SettingsForm : Form
    {
        private CheckBox chkRetry;
        private NumericUpDown numRetries;
        private NumericUpDown numRetryDelay;
        private NumericUpDown numLogDays;
        private CheckBox chkTray;
        private CheckBox chkStartTray;
        private CheckBox chkVerify;
        private CheckBox chkZip;
        private NumericUpDown numZipKeep;
        private NumericUpDown numZipMaxGB;
        private CheckBox chkStorageWarning;
        private NumericUpDown numStorageGB;
        private Button btnZipPassword;

        private CheckBox chkLargeMailbox;
        private NumericUpDown numImapTimeout;
        private CheckBox chkPreventSleep;
        private NumericUpDown numLargeFileGB;
        private NumericUpDown numLargeMinFreeGB;

        public string ZipPasswordValue { get; private set; }

        public int LogRetentionDays { get; private set; }
        public bool RetryEnabled { get; private set; }
        public int RetryCount { get; private set; }
        public int RetryDelayMinutes { get; private set; }
        public bool TrayEnabled { get; private set; }
        public bool StartMinimizedToTray { get; private set; }
        public bool VerifyAfterBackup { get; private set; }
        public bool ZipAfterBackup { get; private set; }
        public int ZipKeepCount { get; private set; }
        public int ZipMaxStorageGB { get; private set; }
        public bool StorageWarningEnabled { get; private set; }
        public int StorageWarningGB { get; private set; }
        public bool LargeMailboxMode { get; private set; }
        public int ImapTimeoutSeconds { get; private set; }
        public bool PreventSleepDuringLongOperations { get; private set; }
        public int LargeFileWarningGB { get; private set; }
        public int LargeMailboxMinFreeGB { get; private set; }

        public SettingsForm(AppConfig cfg)
        {
            Text = "Einstellungen";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(720, 790);
            MinimumSize = new Size(680, 650);
            Font = new Font("Segoe UI", 9F);
            AutoScroll = true;
            BuildUi();

            chkRetry.Checked = cfg.RetryEnabled;
            numRetries.Value = Clamp(numRetries, cfg.RetryCount);
            numRetryDelay.Value = Clamp(numRetryDelay, cfg.RetryDelayMinutes);
            numLogDays.Value = Clamp(numLogDays, cfg.LogRetentionDays);
            chkTray.Checked = cfg.TrayEnabled;
            chkStartTray.Checked = cfg.StartMinimizedToTray;
            chkVerify.Checked = cfg.VerifyAfterBackup;
            chkZip.Checked = cfg.ZipAfterBackup;
            numZipKeep.Value = Clamp(numZipKeep, cfg.ZipKeepCount <= 0 ? 3 : cfg.ZipKeepCount);
            numZipMaxGB.Value = Clamp(numZipMaxGB, cfg.ZipMaxStorageGB <= 0 ? 50 : cfg.ZipMaxStorageGB);
            chkStorageWarning.Checked = cfg.StorageWarningEnabled;
            numStorageGB.Value = Clamp(numStorageGB, cfg.StorageWarningGB <= 0 ? 10 : cfg.StorageWarningGB);

            chkLargeMailbox.Checked = cfg.LargeMailboxMode;
            numImapTimeout.Value = Clamp(numImapTimeout, cfg.ImapTimeoutSeconds <= 0 ? 600 : cfg.ImapTimeoutSeconds);
            chkPreventSleep.Checked = cfg.PreventSleepDuringLongOperations;
            numLargeFileGB.Value = Clamp(numLargeFileGB, cfg.LargeFileWarningGB <= 0 ? 5 : cfg.LargeFileWarningGB);
            numLargeMinFreeGB.Value = Clamp(numLargeMinFreeGB, cfg.LargeMailboxMinFreeGB <= 0 ? 40 : cfg.LargeMailboxMinFreeGB);
            UpdateStates();
        }

        private decimal Clamp(NumericUpDown n, int value)
        {
            decimal v = value;
            if (v < n.Minimum) v = n.Minimum;
            if (v > n.Maximum) v = n.Maximum;
            return v;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Top;
            root.Padding = new Padding(16);
            root.AutoSize = true;
            root.ColumnCount = 2;
            root.RowCount = 23;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            var title = new Label();
            title.Text = "Sicherungs- und Programmoptionen";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            title.Margin = new Padding(0, 0, 0, 14);
            root.Controls.Add(title, 0, 0);
            root.SetColumnSpan(title, 2);

            chkVerify = new CheckBox();
            chkVerify.Text = "Backup nach erfolgreicher Sicherung automatisch mit SHA-256 prüfen";
            chkVerify.AutoSize = true;
            root.Controls.Add(chkVerify, 1, 1);

            chkZip = new CheckBox();
            chkZip.Text = "Nach erfolgreicher Sicherung zusätzlich ein passwortgeschütztes ZIP erstellen";
            chkZip.AutoSize = true;
            chkZip.CheckedChanged += delegate { UpdateStates(); };
            root.Controls.Add(chkZip, 1, 2);

            AddLabel(root, 3, "ZIP-Aufbewahrung:");
            var zipPanel = new FlowLayoutPanel();
            zipPanel.AutoSize = true;
            numZipKeep = new NumericUpDown();
            numZipKeep.Minimum = 1;
            numZipKeep.Maximum = 50;
            numZipKeep.Width = 70;
            zipPanel.Controls.Add(numZipKeep);
            zipPanel.Controls.Add(new Label { Text = "Archive je Konto", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            btnZipPassword = new Button { Text = "ZIP-Kennwort setzen/ändern", Width = 190, Margin = new Padding(12, 0, 0, 0) };
            btnZipPassword.Click += delegate
            {
                using (var p = new PasswordForm("ZIP-Archiv"))
                {
                    if (p.ShowDialog(this) == DialogResult.OK) ZipPasswordValue = p.PasswordValue;
                }
            };
            zipPanel.Controls.Add(btnZipPassword);
            root.Controls.Add(zipPanel, 1, 3);

            AddLabel(root, 4, "ZIP-Speicherlimit:");
            var zipMaxPanel = new FlowLayoutPanel();
            zipMaxPanel.AutoSize = true;
            numZipMaxGB = new NumericUpDown();
            numZipMaxGB.Minimum = 1;
            numZipMaxGB.Maximum = 100000;
            numZipMaxGB.Width = 90;
            zipMaxPanel.Controls.Add(numZipMaxGB);
            zipMaxPanel.Controls.Add(new Label { Text = "GB je Konto (älteste Archive werden zuerst gelöscht)", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            root.Controls.Add(zipMaxPanel, 1, 4);

            chkStorageWarning = new CheckBox();
            chkStorageWarning.Text = "Speicherwarnung vor Sicherungsbeginn aktivieren";
            chkStorageWarning.AutoSize = true;
            chkStorageWarning.CheckedChanged += delegate { UpdateStates(); };
            root.Controls.Add(chkStorageWarning, 1, 5);

            AddLabel(root, 6, "Warnschwelle:");
            var storagePanel = new FlowLayoutPanel();
            storagePanel.AutoSize = true;
            numStorageGB = new NumericUpDown();
            numStorageGB.Minimum = 1;
            numStorageGB.Maximum = 10000;
            numStorageGB.Width = 90;
            storagePanel.Controls.Add(numStorageGB);
            storagePanel.Controls.Add(new Label { Text = "GB freier Speicher", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            root.Controls.Add(storagePanel, 1, 6);

            var largeTitle = new Label();
            largeTitle.Text = "Große Postfächer / Large Mailbox";
            largeTitle.AutoSize = true;
            largeTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            largeTitle.Margin = new Padding(0, 14, 0, 6);
            root.Controls.Add(largeTitle, 0, 7);
            root.SetColumnSpan(largeTitle, 2);

            chkLargeMailbox = new CheckBox();
            chkLargeMailbox.Text = "Optimierungen für große Postfächer aktivieren";
            chkLargeMailbox.AutoSize = true;
            chkLargeMailbox.CheckedChanged += delegate { UpdateStates(); };
            root.Controls.Add(chkLargeMailbox, 1, 8);

            AddLabel(root, 9, "IMAP-Timeout:");
            var timeoutPanel = new FlowLayoutPanel();
            timeoutPanel.AutoSize = true;
            numImapTimeout = new NumericUpDown();
            numImapTimeout.Minimum = 60;
            numImapTimeout.Maximum = 3600;
            numImapTimeout.Increment = 60;
            numImapTimeout.Width = 90;
            timeoutPanel.Controls.Add(numImapTimeout);
            timeoutPanel.Controls.Add(new Label { Text = "Sekunden", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            root.Controls.Add(timeoutPanel, 1, 9);

            chkPreventSleep = new CheckBox();
            chkPreventSleep.Text = "Während Sicherung Windows-Energiesparmodus verhindern";
            chkPreventSleep.AutoSize = true;
            root.Controls.Add(chkPreventSleep, 1, 10);

            AddLabel(root, 11, "Große Einzeldatei:");
            var largeFilePanel = new FlowLayoutPanel();
            largeFilePanel.AutoSize = true;
            numLargeFileGB = new NumericUpDown();
            numLargeFileGB.Minimum = 1;
            numLargeFileGB.Maximum = 1000;
            numLargeFileGB.Width = 90;
            largeFilePanel.Controls.Add(numLargeFileGB);
            largeFilePanel.Controls.Add(new Label { Text = "GB – ab dieser MBOX-Größe Warnung protokollieren", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            root.Controls.Add(largeFilePanel, 1, 11);

            AddLabel(root, 12, "Sicherheitsreserve:");
            var reservePanel = new FlowLayoutPanel();
            reservePanel.AutoSize = true;
            numLargeMinFreeGB = new NumericUpDown();
            numLargeMinFreeGB.Minimum = 5;
            numLargeMinFreeGB.Maximum = 10000;
            numLargeMinFreeGB.Width = 90;
            reservePanel.Controls.Add(numLargeMinFreeGB);
            reservePanel.Controls.Add(new Label { Text = "GB freier Speicher für große Postfächer", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            root.Controls.Add(reservePanel, 1, 12);

            chkRetry = new CheckBox();
            chkRetry.Text = "Fehlgeschlagene Sicherungen automatisch wiederholen";
            chkRetry.AutoSize = true;
            chkRetry.CheckedChanged += delegate { UpdateStates(); };
            root.Controls.Add(chkRetry, 1, 13);

            AddLabel(root, 14, "Wiederholungen:");
            numRetries = new NumericUpDown();
            numRetries.Minimum = 0;
            numRetries.Maximum = 10;
            numRetries.Width = 80;
            root.Controls.Add(numRetries, 1, 14);

            AddLabel(root, 15, "Wartezeit:");
            var retryPanel = new FlowLayoutPanel();
            retryPanel.AutoSize = true;
            numRetryDelay = new NumericUpDown();
            numRetryDelay.Minimum = 0;
            numRetryDelay.Maximum = 60;
            numRetryDelay.Width = 80;
            retryPanel.Controls.Add(numRetryDelay);
            retryPanel.Controls.Add(new Label { Text = "Minute(n)", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            root.Controls.Add(retryPanel, 1, 15);

            AddLabel(root, 16, "Log-Aufbewahrung:");
            var logPanel = new FlowLayoutPanel();
            logPanel.AutoSize = true;
            numLogDays = new NumericUpDown();
            numLogDays.Minimum = 1;
            numLogDays.Maximum = 3650;
            numLogDays.Width = 90;
            logPanel.Controls.Add(numLogDays);
            logPanel.Controls.Add(new Label { Text = "Tage", AutoSize = true, Margin = new Padding(6, 5, 0, 0) });
            root.Controls.Add(logPanel, 1, 16);

            chkTray = new CheckBox();
            chkTray.Text = "Beim Schließen in den Infobereich minimieren";
            chkTray.AutoSize = true;
            chkTray.CheckedChanged += delegate { UpdateStates(); };
            root.Controls.Add(chkTray, 1, 17);

            chkStartTray = new CheckBox();
            chkStartTray.Text = "Programm beim Start direkt in den Infobereich minimieren";
            chkStartTray.AutoSize = true;
            root.Controls.Add(chkStartTray, 1, 18);

            var hint = new Label();
            hint.AutoSize = true;
            hint.MaximumSize = new Size(650, 0);
            hint.ForeColor = Color.DimGray;
            hint.Text = "ZIP-Archive werden AES-256 verschlüsselt und mit ZIP64 erzeugt. Bei großen Postfächern wird der IMAP-Timeout erhöht und der freie Speicher vor kritischen Schritten geprüft.";
            hint.Margin = new Padding(0, 14, 0, 0);
            root.Controls.Add(hint, 0, 19);
            root.SetColumnSpan(hint, 2);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 54;
            buttons.Padding = new Padding(14, 9, 14, 9);
            buttons.FlowDirection = FlowDirection.RightToLeft;
            var save = new Button();
            save.Text = "Speichern";
            save.Width = 95;
            save.Click += BtnSave_Click;
            var cancel = new Button();
            cancel.Text = "Abbrechen";
            cancel.Width = 90;
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void AddLabel(TableLayoutPanel root, int row, string text)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, 7, 12, 6);
            root.Controls.Add(l, 0, row);
        }

        private void UpdateStates()
        {
            numRetries.Enabled = chkRetry.Checked;
            numRetryDelay.Enabled = chkRetry.Checked;
            chkStartTray.Enabled = chkTray.Checked;
            if (!chkTray.Checked) chkStartTray.Checked = false;
            numZipKeep.Enabled = chkZip.Checked;
            numZipMaxGB.Enabled = chkZip.Checked;
            btnZipPassword.Enabled = chkZip.Checked;
            numStorageGB.Enabled = chkStorageWarning.Checked;

            numImapTimeout.Enabled = chkLargeMailbox.Checked;
            chkPreventSleep.Enabled = chkLargeMailbox.Checked;
            numLargeFileGB.Enabled = chkLargeMailbox.Checked;
            numLargeMinFreeGB.Enabled = chkLargeMailbox.Checked;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            LogRetentionDays = Decimal.ToInt32(numLogDays.Value);
            RetryEnabled = chkRetry.Checked;
            RetryCount = Decimal.ToInt32(numRetries.Value);
            RetryDelayMinutes = Decimal.ToInt32(numRetryDelay.Value);
            TrayEnabled = chkTray.Checked;
            StartMinimizedToTray = chkStartTray.Checked;
            VerifyAfterBackup = chkVerify.Checked;
            ZipAfterBackup = chkZip.Checked;
            ZipKeepCount = Decimal.ToInt32(numZipKeep.Value);
            ZipMaxStorageGB = Decimal.ToInt32(numZipMaxGB.Value);
            StorageWarningEnabled = chkStorageWarning.Checked;
            StorageWarningGB = Decimal.ToInt32(numStorageGB.Value);
            LargeMailboxMode = chkLargeMailbox.Checked;
            ImapTimeoutSeconds = Decimal.ToInt32(numImapTimeout.Value);
            PreventSleepDuringLongOperations = chkPreventSleep.Checked;
            LargeFileWarningGB = Decimal.ToInt32(numLargeFileGB.Value);
            LargeMailboxMinFreeGB = Decimal.ToInt32(numLargeMinFreeGB.Value);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
