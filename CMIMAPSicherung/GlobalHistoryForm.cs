using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public class GlobalHistoryForm : Form
    {
        private readonly DataGridView grid;

        public GlobalHistoryForm(List<GlobalHistoryEntry> history)
        {
            Text = "Gesamte Backup-Historie";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1050, 470);
            MinimumSize = new Size(820, 360);
            Font = new Font("Segoe UI", 9F);

            var entries = history ?? new List<GlobalHistoryEntry>();

            var top = new Label();
            top.Dock = DockStyle.Top;
            top.Height = 38;
            top.TextAlign = ContentAlignment.MiddleLeft;
            top.Padding = new Padding(12, 0, 0, 0);
            top.Text = String.Format("Backup-Läufe: {0}   |   Doppelklick öffnet die zugehörige Logdatei", entries.Count);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = SystemColors.Window;
            grid.Columns.Add("Timestamp", "Zeitpunkt");
            grid.Columns.Add("Source", "Quelle");
            grid.Columns.Add("Mode", "Typ");
            grid.Columns.Add("Total", "Konten");
            grid.Columns.Add("Ok", "OK");
            grid.Columns.Add("Errors", "Fehler");
            grid.Columns.Add("Cancelled", "Abbruch");
            grid.Columns.Add("Duration", "Dauer");
            grid.Columns.Add("LogPath", "Logdatei");
            grid.Columns[0].FillWeight = 120;
            grid.Columns[1].FillWeight = 80;
            grid.Columns[2].FillWeight = 80;
            grid.Columns[3].FillWeight = 55;
            grid.Columns[4].FillWeight = 45;
            grid.Columns[5].FillWeight = 55;
            grid.Columns[6].FillWeight = 55;
            grid.Columns[7].FillWeight = 70;
            grid.Columns[8].FillWeight = 250;
            grid.CellDoubleClick += Grid_CellDoubleClick;

            foreach (var h in entries.AsEnumerable().Reverse())
            {
                int row = grid.Rows.Add(h.Timestamp, h.Source, h.Mode, h.Total, h.OkCount, h.ErrorCount, h.CancelledCount, h.Duration, h.LogPath);
                grid.Rows[row].Tag = h;
                if (h.ErrorCount > 0)
                    grid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(253, 236, 234);
                else if (h.CancelledCount > 0)
                    grid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
                else
                    grid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(232, 247, 235);
            }

            var close = new Button();
            close.Text = "Schließen";
            close.Dock = DockStyle.Bottom;
            close.Height = 36;
            close.DialogResult = DialogResult.OK;

            Controls.Add(grid);
            Controls.Add(top);
            Controls.Add(close);
            AcceptButton = close;
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var h = grid.Rows[e.RowIndex].Tag as GlobalHistoryEntry;
            if (h == null || String.IsNullOrWhiteSpace(h.LogPath) || !File.Exists(h.LogPath))
            {
                MessageBox.Show("Die Logdatei ist nicht mehr vorhanden.", "Backup-Historie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start("notepad.exe", "\"" + h.LogPath + "\""); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Logdatei", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
