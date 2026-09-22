using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public class AccountEditForm : Form
    {
        private TextBox txtName;
        private TextBox txtHost;
        private NumericUpDown numPort;
        private TextBox txtUser;
        private TextBox txtId;
        private CheckBox chkEnabled;
        private Button btnDetect;
        private Label lblDetectStatus;
        private Label lblHint;
        public AccountConfig Account { get; private set; }

        public AccountEditForm(AccountConfig account)
        {
            Text = account == null ? "IMAP-Konto hinzufügen" : "IMAP-Konto bearbeiten";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(660, 420);
            MinimumSize = new Size(660, 420);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(245, 247, 250);

            Account = account ?? new AccountConfig();
            BuildUi();

            txtName.Text = Account.Name ?? "";
            txtHost.Text = Account.Host ?? "";
            numPort.Value = Account.Port <= 0 ? 993 : Account.Port;
            txtUser.Text = Account.User ?? "";
            txtId.Text = Account.Id ?? "";
            chkEnabled.Checked = Account.Enabled || account == null;

            UpdateDetectStatus("E-Mail-Adresse eingeben und dann auf „IMAP automatisch erkennen“ klicken.", false);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 82;
            header.Margin = new Padding(0, 0, 0, 10);
            header.BackColor = Color.FromArgb(42, 63, 96);
            header.Padding = new Padding(16, 12, 16, 12);
            root.Controls.Add(header, 0, 0);

            var lblTitle = new Label();
            lblTitle.Text = "Kontoeinstellungen";
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(0, 0);
            header.Controls.Add(lblTitle);

            var lblSub = new Label();
            lblSub.Text = "E-Mail-Adresse eintragen, IMAP automatisch erkennen lassen oder die Daten manuell ergänzen.";
            lblSub.AutoSize = true;
            lblSub.MaximumSize = new Size(580, 0);
            lblSub.ForeColor = Color.FromArgb(222, 230, 240);
            lblSub.Location = new Point(0, 36);
            header.Controls.Add(lblSub);

            var formCard = new TableLayoutPanel();
            formCard.Dock = DockStyle.Fill;
            formCard.BackColor = Color.White;
            formCard.Padding = new Padding(16);
            formCard.Margin = new Padding(0, 0, 0, 10);
            formCard.ColumnCount = 3;
            formCard.RowCount = 8;
            formCard.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            formCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            formCard.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(formCard, 0, 1);

            AddLabel(formCard, 0, "E-Mail-Adresse / Benutzername:");
            txtUser = new TextBox();
            txtUser.Dock = DockStyle.Fill;
            txtUser.Margin = new Padding(0, 3, 10, 8);
            txtUser.Leave += delegate
            {
                if (String.IsNullOrWhiteSpace(txtId.Text)) txtId.Text = txtUser.Text.Trim();
                if (String.IsNullOrWhiteSpace(txtName.Text)) txtName.Text = txtUser.Text.Trim();
            };
            formCard.Controls.Add(txtUser, 1, 0);

            btnDetect = new Button();
            btnDetect.Text = "IMAP automatisch erkennen";
            btnDetect.Width = 185;
            btnDetect.Height = 30;
            btnDetect.Margin = new Padding(0, 1, 0, 0);
            StylePrimaryButton(btnDetect);
            btnDetect.Click += BtnDetect_Click;
            formCard.Controls.Add(btnDetect, 2, 0);

            lblDetectStatus = new Label();
            lblDetectStatus.AutoSize = true;
            lblDetectStatus.MaximumSize = new Size(600, 0);
            lblDetectStatus.Margin = new Padding(0, 0, 0, 12);
            formCard.Controls.Add(lblDetectStatus, 1, 1);
            formCard.SetColumnSpan(lblDetectStatus, 2);

            AddLabel(formCard, 2, "Anzeigename:");
            txtName = new TextBox(); txtName.Dock = DockStyle.Fill; txtName.Margin = new Padding(0, 3, 0, 8);
            formCard.Controls.Add(txtName, 1, 2); formCard.SetColumnSpan(txtName, 2);

            AddLabel(formCard, 3, "IMAP-Server:");
            txtHost = new TextBox(); txtHost.Dock = DockStyle.Fill; txtHost.Margin = new Padding(0, 3, 0, 8);
            formCard.Controls.Add(txtHost, 1, 3); formCard.SetColumnSpan(txtHost, 2);

            AddLabel(formCard, 4, "Port:");
            var portWrap = new FlowLayoutPanel();
            portWrap.AutoSize = true;
            portWrap.FlowDirection = FlowDirection.LeftToRight;
            numPort = new NumericUpDown(); numPort.Minimum = 1; numPort.Maximum = 65535; numPort.Value = 993; numPort.Width = 110;
            portWrap.Controls.Add(numPort);
            lblHint = new Label(); lblHint.Text = "Standard: 993 (SSL/TLS)"; lblHint.AutoSize = true; lblHint.Margin = new Padding(12, 7, 0, 0); lblHint.ForeColor = Color.DimGray;
            portWrap.Controls.Add(lblHint);
            formCard.Controls.Add(portWrap, 1, 4); formCard.SetColumnSpan(portWrap, 2);

            AddLabel(formCard, 5, "Konto-ID:");
            txtId = new TextBox(); txtId.Dock = DockStyle.Fill; txtId.Margin = new Padding(0, 3, 0, 8);
            formCard.Controls.Add(txtId, 1, 5); formCard.SetColumnSpan(txtId, 2);

            chkEnabled = new CheckBox();
            chkEnabled.Text = "Konto aktiv";
            chkEnabled.AutoSize = true;
            chkEnabled.Checked = true;
            chkEnabled.Margin = new Padding(0, 8, 0, 0);
            formCard.Controls.Add(chkEnabled, 1, 6);

            var note = new Label();
            note.Text = "Hinweis: Bei manchen eigenen Domains kann die automatische Erkennung nur einen Vorschlag liefern. In diesem Fall bitte die Daten kurz prüfen.";
            note.AutoSize = true;
            note.MaximumSize = new Size(600, 0);
            note.ForeColor = Color.DimGray;
            note.Margin = new Padding(0, 12, 0, 0);
            root.Controls.Add(note, 0, 2);

            var p = new FlowLayoutPanel();
            p.FlowDirection = FlowDirection.RightToLeft;
            p.Dock = DockStyle.Fill;
            p.AutoSize = true;
            var ok = new Button();
            ok.Text = "Speichern";
            ok.Width = 100;
            ok.Height = 32;
            StylePrimaryButton(ok);
            ok.Click += Ok_Click;
            var cancel = new Button();
            cancel.Text = "Abbrechen";
            cancel.Width = 100;
            cancel.Height = 32;
            StyleSecondaryButton(cancel);
            cancel.DialogResult = DialogResult.Cancel;
            p.Controls.Add(ok);
            p.Controls.Add(cancel);
            root.Controls.Add(p, 0, 3);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void BtnDetect_Click(object sender, EventArgs e)
        {
            string email = txtUser.Text.Trim();
            if (String.IsNullOrWhiteSpace(email) || email.IndexOf('@') < 1)
            {
                MessageBox.Show("Bitte zuerst eine vollständige E-Mail-Adresse eingeben.", "Automatische Erkennung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtUser.Focus();
                return;
            }

            Cursor previous = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                btnDetect.Enabled = false;
                UpdateDetectStatus("IMAP-Einstellungen werden geprüft ...", false);
                var result = ImapAutoDetector.Detect(email);
                if (result.Success)
                {
                    txtHost.Text = result.Host ?? "";
                    if (result.Port > 0) numPort.Value = result.Port;
                    if (String.IsNullOrWhiteSpace(txtName.Text)) txtName.Text = email;
                    if (String.IsNullOrWhiteSpace(txtId.Text)) txtId.Text = email;
                    if (!String.IsNullOrWhiteSpace(result.UserName)) txtUser.Text = result.UserName;
                    lblHint.Text = "Erkannt: " + (String.IsNullOrWhiteSpace(result.Security) ? "SSL/TLS" : result.Security);
                    UpdateDetectStatus("Erkannt: Server " + result.Host + ", Port " + result.Port + (String.IsNullOrWhiteSpace(result.Provider) ? "" : "  |  Anbieter: " + result.Provider) + ". " + result.Message, true);
                }
                else
                {
                    UpdateDetectStatus(result.Message, false);
                    MessageBox.Show(result.Message, "Automatische Erkennung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                btnDetect.Enabled = true;
                Cursor.Current = previous;
            }
        }

        private void UpdateDetectStatus(string text, bool success)
        {
            lblDetectStatus.Text = text;
            lblDetectStatus.ForeColor = success ? Color.FromArgb(31, 108, 62) : Color.FromArgb(110, 72, 24);
        }

        private void AddLabel(TableLayoutPanel t, int row, string text)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, 7, 12, 8);
            t.Controls.Add(l, 0, row);
        }

        private void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(45, 80, 130);
            button.ForeColor = Color.White;
        }

        private void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(192, 198, 208);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(38, 52, 70);
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string host = txtHost.Text.Trim();
            string user = txtUser.Text.Trim();
            string id = txtId.Text.Trim();
            if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(host) || String.IsNullOrEmpty(user) || String.IsNullOrEmpty(id))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen.", "Konto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id == "." || id == "..")
            {
                MessageBox.Show("Die Konto-ID enthält Zeichen, die nicht als Windows-Ordnername verwendet werden können.", "Konto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Account.Name = name;
            Account.Host = host;
            Account.Port = Decimal.ToInt32(numPort.Value);
            Account.User = user;
            Account.Id = id;
            Account.Enabled = chkEnabled.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
