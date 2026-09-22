using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    public class SchedulerForm : Form
    {
        private CheckBox chkEnabled;
        private ComboBox cmbFrequency;
        private DateTimePicker timePicker;
        private ComboBox cmbWeekday;
        public ScheduleConfig Schedule { get; private set; }
        public bool RemoveRequested { get; private set; }

        public SchedulerForm(ScheduleConfig source, TaskStatusInfo status)
        {
            Text = "Automatische Sicherung";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(590, 350);
            Font = new Font("Segoe UI", 9F);
            Schedule = Copy(source ?? new ScheduleConfig());
            BuildUi(status);
            BindValues();
        }

        private ScheduleConfig Copy(ScheduleConfig s)
        {
            return new ScheduleConfig { Enabled = s.Enabled, Frequency = String.IsNullOrWhiteSpace(s.Frequency) ? "Daily" : s.Frequency, Time = String.IsNullOrWhiteSpace(s.Time) ? "20:00" : s.Time, Weekday = s.Weekday, LastConfigured = s.LastConfigured };
        }

        private void BuildUi(TaskStatusInfo status)
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Top;
            root.Padding = new Padding(14);
            root.AutoSize = true;
            root.ColumnCount = 2;
            root.RowCount = 7;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            var title = new Label();
            title.Text = "Zeitplan für automatische IMAP-Sicherungen";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            title.Margin = new Padding(0, 0, 0, 12);
            root.Controls.Add(title, 0, 0);
            root.SetColumnSpan(title, 2);

            chkEnabled = new CheckBox();
            chkEnabled.Text = "Automatische Sicherung aktivieren";
            chkEnabled.AutoSize = true;
            chkEnabled.CheckedChanged += delegate { UpdateEnabledState(); };
            root.Controls.Add(chkEnabled, 1, 1);

            AddLabel(root, 2, "Ausführung:");
            cmbFrequency = new ComboBox();
            cmbFrequency.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFrequency.Items.AddRange(new object[] { "Täglich", "Wöchentlich", "Bei Windows-Anmeldung" });
            cmbFrequency.Width = 230;
            cmbFrequency.SelectedIndexChanged += delegate { UpdateEnabledState(); };
            root.Controls.Add(cmbFrequency, 1, 2);

            AddLabel(root, 3, "Uhrzeit:");
            timePicker = new DateTimePicker();
            timePicker.Format = DateTimePickerFormat.Custom;
            timePicker.CustomFormat = "HH:mm";
            timePicker.ShowUpDown = true;
            timePicker.Width = 100;
            root.Controls.Add(timePicker, 1, 3);

            AddLabel(root, 4, "Wochentag:");
            cmbWeekday = new ComboBox();
            cmbWeekday.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbWeekday.Items.AddRange(new object[] { "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag" });
            cmbWeekday.Width = 160;
            root.Controls.Add(cmbWeekday, 1, 4);

            var lblStatus = new Label();
            lblStatus.AutoSize = true;
            lblStatus.Margin = new Padding(0, 12, 0, 5);
            lblStatus.Text = BuildStatusText(status);
            root.Controls.Add(lblStatus, 0, 5);
            root.SetColumnSpan(lblStatus, 2);

            var hint = new Label();
            hint.AutoSize = true;
            hint.MaximumSize = new Size(550, 0);
            hint.ForeColor = Color.DimGray;
            hint.Text = "Die Windows-Aufgabe läuft unter dem aktuell angemeldeten Benutzer. Dadurch können die bereits mit Windows-DPAPI gespeicherten IMAP-Kennwörter verwendet werden. Für automatische Zeitläufe muss dieser Benutzer angemeldet sein.";
            root.Controls.Add(hint, 0, 6);
            root.SetColumnSpan(hint, 2);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 52;
            buttons.Padding = new Padding(14, 9, 14, 9);
            buttons.FlowDirection = FlowDirection.RightToLeft;

            var save = new Button(); save.Text = "Übernehmen"; save.Width = 100; save.Click += BtnSave_Click; buttons.Controls.Add(save);
            var cancel = new Button(); cancel.Text = "Abbrechen"; cancel.Width = 90; cancel.DialogResult = DialogResult.Cancel; buttons.Controls.Add(cancel);
            var remove = new Button(); remove.Text = "Zeitplan entfernen"; remove.Width = 130; remove.Click += delegate { RemoveRequested = true; DialogResult = DialogResult.OK; Close(); }; buttons.Controls.Add(remove);
            Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void AddLabel(TableLayoutPanel root, int row, string text)
        {
            var l = new Label(); l.Text = text; l.AutoSize = true; l.Anchor = AnchorStyles.Left; l.Margin = new Padding(0, 7, 12, 6); root.Controls.Add(l, 0, row);
        }

        private void BindValues()
        {
            chkEnabled.Checked = Schedule.Enabled;
            cmbFrequency.SelectedIndex = Schedule.Frequency == "Weekly" ? 1 : (Schedule.Frequency == "Logon" ? 2 : 0);
            DateTime parsed;
            if (!DateTime.TryParseExact(Schedule.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) parsed = DateTime.Today.AddHours(20);
            timePicker.Value = DateTime.Today.AddHours(parsed.Hour).AddMinutes(parsed.Minute);
            cmbWeekday.SelectedIndex = DayToIndex(Schedule.Weekday);
            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            bool enabled = chkEnabled.Checked;
            cmbFrequency.Enabled = enabled;
            bool logon = cmbFrequency.SelectedIndex == 2;
            timePicker.Enabled = enabled && !logon;
            cmbWeekday.Enabled = enabled && cmbFrequency.SelectedIndex == 1;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            Schedule.Enabled = chkEnabled.Checked;
            Schedule.Frequency = cmbFrequency.SelectedIndex == 1 ? "Weekly" : (cmbFrequency.SelectedIndex == 2 ? "Logon" : "Daily");
            Schedule.Time = timePicker.Value.ToString("HH:mm");
            Schedule.Weekday = IndexToDay(cmbWeekday.SelectedIndex);
            Schedule.LastConfigured = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            RemoveRequested = false;
            DialogResult = DialogResult.OK;
            Close();
        }

        private string BuildStatusText(TaskStatusInfo status)
        {
            if (status == null || !status.Exists) return "Windows-Aufgabe: nicht eingerichtet";
            string next = status.NextRun.HasValue ? status.NextRun.Value.ToString("dd.MM.yyyy HH:mm") : "-";
            string last = status.LastRun.HasValue ? status.LastRun.Value.ToString("dd.MM.yyyy HH:mm") : "-";
            return String.Format("Windows-Aufgabe: {0}   |   Nächste: {1}   |   Letzte: {2}", status.State, next, last);
        }

        private int DayToIndex(DayOfWeek day)
        {
            switch (day) { case DayOfWeek.Tuesday: return 1; case DayOfWeek.Wednesday: return 2; case DayOfWeek.Thursday: return 3; case DayOfWeek.Friday: return 4; case DayOfWeek.Saturday: return 5; case DayOfWeek.Sunday: return 6; default: return 0; }
        }
        private DayOfWeek IndexToDay(int index)
        {
            switch (index) { case 1: return DayOfWeek.Tuesday; case 2: return DayOfWeek.Wednesday; case 3: return DayOfWeek.Thursday; case 4: return DayOfWeek.Friday; case 5: return DayOfWeek.Saturday; case 6: return DayOfWeek.Sunday; default: return DayOfWeek.Monday; }
        }
    }
}
