using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace CMIMAPSicherung
{
    internal static class SmokeTest
    {
        private static bool ContainsControl(Control parent,Type t) {
            foreach(Control c in parent.Controls)if(t.IsAssignableFrom(c.GetType())||ContainsControl(c,t))return true;
            return false;
        }
        private static bool ContainsColorIcon(Control parent) {
            foreach(Control c in parent.Controls) {
                var b=c as Button;
                if(b!=null && b.Image!=null)return true;
                if(ContainsColorIcon(c))return true;
            }
            return false;
        }
        public static void Run(string directory)
        {
            if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true") {Environment.ExitCode=2;return;}
            Directory.CreateDirectory(directory);
            try {
                using(var main=new MainForm(false)) {
                    WorkspaceLayout.Install(main);main.Show();Application.DoEvents();
                    if(!main.Controls.OfType<TableLayoutPanel>().Any())throw new Exception("Workspace root missing.");
                    if(!ContainsControl(main,typeof(SmoothProgress)))throw new Exception("Animated main progress missing.");
                    if(!ContainsColorIcon(main))throw new Exception("Color images missing from main buttons.");
                    using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,main.Size));image.Save(Path.Combine(directory,"workspace.png"),ImageFormat.Png);}
                    using(var archive=new ArchiveForm(new AppConfig(),null)) {
                        archive.Show(main);Application.DoEvents();
                        if(!ContainsControl(archive,typeof(SmoothProgress)))throw new Exception("Animated archive progress missing.");
                        using(var image=new Bitmap(archive.Width,archive.Height)){archive.DrawToBitmap(image,new Rectangle(Point.Empty,archive.Size));image.Save(Path.Combine(directory,"archive.png"),ImageFormat.Png);}
                        archive.Close();
                    }
                    using(var guide=new UserGuideForm()) {
                        guide.Show(main);Application.DoEvents();
                        if(guide.Controls.Count==0)throw new Exception("Guide failed to open.");
                        using(var image=new Bitmap(guide.Width,guide.Height)){guide.DrawToBitmap(image,new Rectangle(Point.Empty,guide.Size));image.Save(Path.Combine(directory,"user-guide.png"),ImageFormat.Png);}
                        guide.Close();
                    }
                    main.Dispose();
                }
                File.WriteAllText(Path.Combine(directory,"ui-smoke.txt"),"PASS: main and archive forms constructed, laid out, rendered and disposed. No real account used.");
            }catch(Exception ex){File.WriteAllText(Path.Combine(directory,"ui-smoke.txt"),ex.ToString());Environment.ExitCode=1;}
        }
    }
}
