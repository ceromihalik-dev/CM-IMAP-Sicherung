using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace CMIMAPSicherung
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            // Prefer the packaged interpreter over a Python installed on the PC.
            Environment.SetEnvironmentVariable("PATH",AppDomain.CurrentDomain.BaseDirectory+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH"));
            bool scheduled=args!=null&&args.Any(a=>String.Equals(a,"/scheduled",StringComparison.OrdinalIgnoreCase));
            if(args!=null&&args.Length==2&&args[0]=="/ui-smoke-test"){SmokeTest.Run(args[1]);return;}
            try {var main=new MainForm(scheduled);if(!scheduled)WorkspaceLayout.Install(main);Application.Run(main);}
            catch(Exception ex){MessageBox.Show("Programmstart fehlgeschlagen: "+ex.Message,"CM IMAP Sicherung",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;}
        }
    }
}
