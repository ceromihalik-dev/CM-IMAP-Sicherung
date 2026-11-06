using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace CMIMAPSicherung
{
    internal static class SmokeTest
    {
        public static void Run(string directory)
        {
            if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true") {Environment.ExitCode=2;return;}
            Directory.CreateDirectory(directory);
            try {
                using(var main=new MainForm(false)) {
                    WorkspaceLayout.Install(main);main.Show();Application.DoEvents();
                    using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,main.Size));image.Save(Path.Combine(directory,"workspace.png"),ImageFormat.Png);}
                    using(var archive=new ArchiveForm(new AppConfig(),null)) {
                        archive.Show(main);Application.DoEvents();
                        using(var image=new Bitmap(archive.Width,archive.Height)){archive.DrawToBitmap(image,new Rectangle(Point.Empty,archive.Size));image.Save(Path.Combine(directory,"archive.png"),ImageFormat.Png);}
                        archive.Close();
                    }
                    main.Dispose();
                }
                File.WriteAllText(Path.Combine(directory,"ui-smoke.txt"),"PASS: main and archive forms constructed, laid out, rendered and disposed. No real account used.");
            }catch(Exception ex){File.WriteAllText(Path.Combine(directory,"ui-smoke.txt"),ex.ToString());Environment.ExitCode=1;}
        }
    }
}
