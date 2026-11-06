using System.Drawing;
using System.Windows.Forms;
namespace CMIMAPSicherung
{
    internal static class UiTheme
    {
        public static readonly Color Window=Color.FromArgb(30,30,30);
        public static readonly Color Surface=Color.FromArgb(37,37,38);
        public static readonly Color Header=Color.FromArgb(45,45,48);
        public static readonly Color HeaderAlt=Color.FromArgb(51,51,51);
        public static readonly Color Primary=Color.FromArgb(0,122,204);
        public static readonly Color PrimaryHover=Color.FromArgb(28,151,234);
        public static readonly Color Text=Color.FromArgb(220,220,220);
        public static readonly Color Muted=Color.FromArgb(155,155,155);
        public static readonly Color Border=Color.FromArgb(62,62,66);
        public static readonly Color Success=Color.FromArgb(137,209,133);
        public static readonly Color SuccessBack=Color.FromArgb(38,58,42);
        public static readonly Color Warning=Color.FromArgb(220,189,104);
        public static readonly Color WarningBack=Color.FromArgb(61,51,32);
        public static readonly Color Danger=Color.FromArgb(244,135,113);
        public static readonly Color DangerBack=Color.FromArgb(67,40,40);
        public static readonly Color LogBack=Color.FromArgb(24,24,24);
        public static readonly Color LogText=Color.Gainsboro;
        public static void ApplyPrimary(Button b){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Primary;b.ForeColor=Color.White;b.Cursor=Cursors.Hand;}
        public static void ApplySecondary(Button b){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Border;b.FlatAppearance.BorderSize=1;b.BackColor=Surface;b.ForeColor=Text;b.Cursor=Cursors.Hand;}
        public static void ApplyDanger(Button b){ApplySecondary(b);b.BackColor=DangerBack;b.ForeColor=Danger;}
        public static void ApplyWarning(Button b){ApplySecondary(b);b.BackColor=WarningBack;b.ForeColor=Warning;}
        public static Panel CreateCard(string title,out Label valueLabel)
        {
            var p=new Panel {Width=245,Height=70,BackColor=Surface,Margin=new Padding(0,0,10,0),Padding=new Padding(14,10,14,8),BorderStyle=BorderStyle.FixedSingle};
            p.Controls.Add(new Label {Text=title,AutoSize=true,ForeColor=Muted,Font=new Font("Segoe UI",8.5F),Location=new Point(11,9)});
            valueLabel=new Label {Text="—",AutoSize=true,ForeColor=Text,Font=new Font("Segoe UI",11F,FontStyle.Bold),Location=new Point(11,33)};p.Controls.Add(valueLabel);return p;
        }
    }
}
