using System.Drawing;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    internal static class UiTheme
    {
        public static readonly Color Window = Color.FromArgb(244, 247, 251);
        public static readonly Color Surface = Color.White;
        public static readonly Color Header = Color.FromArgb(30, 45, 68);
        public static readonly Color HeaderAlt = Color.FromArgb(42, 63, 96);
        public static readonly Color Primary = Color.FromArgb(45, 80, 130);
        public static readonly Color PrimaryHover = Color.FromArgb(37, 69, 116);
        public static readonly Color Text = Color.FromArgb(37, 49, 66);
        public static readonly Color Muted = Color.FromArgb(103, 116, 134);
        public static readonly Color Border = Color.FromArgb(217, 224, 233);
        public static readonly Color Success = Color.FromArgb(32, 118, 68);
        public static readonly Color SuccessBack = Color.FromArgb(232, 247, 235);
        public static readonly Color Warning = Color.FromArgb(154, 98, 15);
        public static readonly Color WarningBack = Color.FromArgb(255, 247, 226);
        public static readonly Color Danger = Color.FromArgb(150, 48, 48);
        public static readonly Color DangerBack = Color.FromArgb(253, 236, 234);
        public static readonly Color LogBack = Color.FromArgb(24, 29, 36);
        public static readonly Color LogText = Color.Gainsboro;

        public static void ApplyPrimary(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Primary;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
        }

        public static void ApplySecondary(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Surface;
            button.ForeColor = Text;
            button.Cursor = Cursors.Hand;
        }

        public static void ApplyDanger(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(225, 190, 190);
            button.BackColor = DangerBack;
            button.ForeColor = Danger;
            button.Cursor = Cursors.Hand;
        }

        public static void ApplyWarning(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(202, 126, 24);
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
        }

        public static Panel CreateCard(string title, out Label valueLabel)
        {
            var panel = new Panel();
            panel.Width = 245;
            panel.Height = 70;
            panel.BackColor = Surface;
            panel.Margin = new Padding(0, 0, 10, 0);
            panel.Padding = new Padding(14, 10, 14, 8);
            panel.BorderStyle = BorderStyle.FixedSingle;

            var caption = new Label();
            caption.Text = title;
            caption.AutoSize = true;
            caption.ForeColor = Muted;
            caption.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            caption.Location = new Point(11, 9);
            panel.Controls.Add(caption);

            valueLabel = new Label();
            valueLabel.Text = "—";
            valueLabel.AutoSize = true;
            valueLabel.ForeColor = Text;
            valueLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            valueLabel.Location = new Point(11, 33);
            panel.Controls.Add(valueLabel);

            return panel;
        }
    }
}
