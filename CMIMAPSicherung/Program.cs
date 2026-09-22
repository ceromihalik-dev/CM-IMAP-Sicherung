using System;
using System.Linq;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool scheduled = args != null && args.Any(a => String.Equals(a, "/scheduled", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(scheduled));
        }
    }
}
