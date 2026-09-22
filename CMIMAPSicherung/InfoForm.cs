using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public class InfoForm : Form
    {
        private PictureBox picLogo;

        public InfoForm()
        {
            Text = "Info - CM IMAP Sicherung";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(500, 330);
            Font = new Font("Segoe UI", 9F);
            BuildUi();
            LoadLogo();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(20);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            picLogo = new PictureBox();
            picLogo.Size = new Size(100, 100);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.Anchor = AnchorStyles.None;
            picLogo.Margin = new Padding(0, 0, 0, 10);
            root.Controls.Add(picLogo, 0, 0);

            var title = new Label();
            title.Text = "CM IMAP Sicherung";
            title.AutoSize = true;
            title.Anchor = AnchorStyles.None;
            title.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            title.Margin = new Padding(0, 0, 0, 6);
            root.Controls.Add(title, 0, 1);

            var version = new Label();
            version.Text = "Version " + GetVersionText();
            version.AutoSize = true;
            version.Anchor = AnchorStyles.None;
            version.ForeColor = Color.DimGray;
            version.Margin = new Padding(0, 0, 0, 14);
            root.Controls.Add(version, 0, 2);

            var creator = new Label();
            creator.Text = "Ersteller: C.Mihalik";
            creator.AutoSize = true;
            creator.Anchor = AnchorStyles.None;
            creator.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            root.Controls.Add(creator, 0, 3);

            var description = new Label();
            description.Text = "Lokale Sicherung mehrerer IMAP-E-Mail-Konten.";
            description.AutoSize = true;
            description.Anchor = AnchorStyles.Top;
            description.ForeColor = Color.DimGray;
            description.Margin = new Padding(0, 12, 0, 0);
            root.Controls.Add(description, 0, 4);

            var close = new Button();
            close.Text = "Schließen";
            close.Width = 100;
            close.Height = 30;
            close.Anchor = AnchorStyles.None;
            close.DialogResult = DialogResult.OK;
            root.Controls.Add(close, 0, 5);
            AcceptButton = close;
            CancelButton = close;
        }

        private string GetVersionText()
        {
            try
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "1.4.0.1" : v.ToString();
            }
            catch { return "1.4.0.1"; }
        }

        private void LoadLogo()
        {
            try
            {
                Assembly asm = typeof(InfoForm).Assembly;
                using (Stream stream = asm.GetManifestResourceStream("CMIMAPSicherung.Resources.cm_logo.png"))
                {
                    if (stream == null) return;
                    using (Image img = Image.FromStream(stream))
                    {
                        picLogo.Image = new Bitmap(img);
                    }
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && picLogo != null && picLogo.Image != null)
            {
                picLogo.Image.Dispose();
                picLogo.Image = null;
            }
            base.Dispose(disposing);
        }
    }
}
