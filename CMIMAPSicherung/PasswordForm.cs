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
    public class PasswordForm : Form
        {
            private TextBox txt1;
            private TextBox txt2;
            public string PasswordValue { get; private set; }
    
            public PasswordForm(string user)
            {
                Text = "IMAP-Kennwort";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(470, 185);
                Font = new Font("Segoe UI", 9F);
    
                var t = new TableLayoutPanel();
                t.Dock = DockStyle.Fill;
                t.Padding = new Padding(12);
                t.ColumnCount = 2;
                t.RowCount = 4;
                t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                Controls.Add(t);
    
                var info = new Label();
                info.Text = "Konto: " + user + "\r\nDas Kennwort wird mit Windows DPAPI geschützt gespeichert.";
                info.AutoSize = true;
                info.Margin = new Padding(0, 0, 0, 10);
                t.Controls.Add(info, 0, 0);
                t.SetColumnSpan(info, 2);
    
                AddLabel(t, 1, "Kennwort:");
                txt1 = new TextBox();
                txt1.UseSystemPasswordChar = true;
                txt1.Dock = DockStyle.Fill;
                t.Controls.Add(txt1, 1, 1);
    
                AddLabel(t, 2, "Wiederholen:");
                txt2 = new TextBox();
                txt2.UseSystemPasswordChar = true;
                txt2.Dock = DockStyle.Fill;
                t.Controls.Add(txt2, 1, 2);
    
                var p = new FlowLayoutPanel();
                p.FlowDirection = FlowDirection.RightToLeft;
                p.Dock = DockStyle.Fill;
                p.AutoSize = true;
                var ok = new Button();
                ok.Text = "Speichern";
                ok.Width = 90;
                ok.Click += delegate
                {
                    if (txt1.Text.Length == 0)
                    {
                        MessageBox.Show("Bitte ein Kennwort eingeben.", "Kennwort", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (txt1.Text != txt2.Text)
                    {
                        MessageBox.Show("Die beiden Kennwörter stimmen nicht überein.", "Kennwort", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    PasswordValue = txt1.Text;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                var cancel = new Button();
                cancel.Text = "Abbrechen";
                cancel.Width = 90;
                cancel.DialogResult = DialogResult.Cancel;
                p.Controls.Add(ok);
                p.Controls.Add(cancel);
                t.Controls.Add(p, 0, 3);
                t.SetColumnSpan(p, 2);
                AcceptButton = ok;
                CancelButton = cancel;
            }
    
            private void AddLabel(TableLayoutPanel t, int row, string text)
            {
                var l = new Label();
                l.Text = text;
                l.AutoSize = true;
                l.Anchor = AnchorStyles.Left;
                l.Margin = new Padding(0, 6, 10, 6);
                t.Controls.Add(l, 0, row);
            }
        }
}
