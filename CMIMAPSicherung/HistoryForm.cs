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
    public class HistoryForm : Form
        {
            public HistoryForm(AccountConfig account)
            {
                Text = "Backup-Historie - " + account.Name;
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(860, 420);
                MinimumSize = new Size(720, 320);
                Font = new Font("Segoe UI", 9F);
    
                var table = new DataGridView();
                table.Dock = DockStyle.Fill;
                table.AllowUserToAddRows = false;
                table.AllowUserToDeleteRows = false;
                table.RowHeadersVisible = false;
                table.ReadOnly = true;
                table.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                table.BackgroundColor = SystemColors.Window;
                table.Columns.Add("Time", "Zeitpunkt");
                table.Columns.Add("Mode", "Typ");
                table.Columns.Add("Result", "Ergebnis");
                table.Columns.Add("Duration", "Dauer");
                table.Columns.Add("Details", "Details");
                table.Columns[0].FillWeight = 115;
                table.Columns[1].FillWeight = 80;
                table.Columns[2].FillWeight = 90;
                table.Columns[3].FillWeight = 65;
                table.Columns[4].FillWeight = 230;
    
                var entries = account.History ?? new List<HistoryEntry>();
                foreach (var item in entries.AsEnumerable().Reverse())
                    table.Rows.Add(item.Timestamp, item.Mode, item.Result, item.Duration, item.Details);
    
                var header = new Label();
                header.Dock = DockStyle.Top;
                header.Height = 36;
                header.TextAlign = ContentAlignment.MiddleLeft;
                header.Padding = new Padding(10, 0, 0, 0);
                header.Text = String.Format("{0}   |   {1} Einträge", account.Name, entries.Count);
    
                var close = new Button();
                close.Dock = DockStyle.Bottom;
                close.Height = 34;
                close.Text = "Schließen";
                close.DialogResult = DialogResult.OK;
    
                Controls.Add(table);
                Controls.Add(header);
                Controls.Add(close);
                AcceptButton = close;
            }
        }
}
