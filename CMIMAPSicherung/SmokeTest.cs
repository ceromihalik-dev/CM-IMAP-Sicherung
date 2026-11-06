using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Linq;
namespace CMIMAPSicherung
{
    internal static class SmokeTest
    {
        private static bool ContainsControl(Control parent,Type t) {
            foreach(Control c in parent.Controls)if(t.IsAssignableFrom(c.GetType())||ContainsControl(c,t))return true;
            return false;
        }
        private static bool HasGuideText(Control parent) {
            foreach(Control c in parent.Controls) {
                var rich=c as RichTextBox;
                if(rich!=null && rich.TextLength>200)return true;
                if(HasGuideText(c))return true;
            }
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
        private static Control Named(Control root,string name)
        {
            if(root.Name==name)return root;
            foreach(Control child in root.Controls) {
                var c=Named(child,name);
                if(c!=null)return c;
            }
            return null;
        }
        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
        {
            foreach(Control c in parent.Controls) {
                yield return c;
                foreach(var nested in Descendants(c))yield return nested;
            }
        }
        private static void CheckDashboard(Form main)
        {
            if(main.MainMenuStrip==null || Named(main,"MainTopMenu")==null)
                throw new Exception("Top menu and always-available Help menu missing.");
            var help=main.MainMenuStrip.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(m=>m.Text=="Hilfe");
            if(help==null || help.DropDownItems.OfType<ToolStripMenuItem>()
                .All(i=>i.Name!="HelpGuideCommand"))
                throw new Exception("Help -> Benutzeranleitung missing.");
            var groups=Descendants(main).OfType<FlowLayoutPanel>()
                .Where(p=>p.Name=="NavigationGroup").ToList();
            if(groups.Count!=5)throw new Exception("Five navigation groups expected.");
            foreach(var group in groups) {
                var buttons=group.Controls.OfType<Button>().ToList();
                var body=group.Controls.OfType<FlowLayoutPanel>().FirstOrDefault(p=>p.Name=="NavigationCommands");
                if(body==null || buttons.Count==0 || body.Visible)
                    throw new Exception("Navigation must start collapsed.");
                buttons[0].PerformClick();Application.DoEvents();
                if(!body.Visible)throw new Exception("Navigation group failed to expand.");
                buttons[0].PerformClick();Application.DoEvents();
                if(body.Visible)throw new Exception("Navigation group failed to collapse.");
            }
            var cards=Named(main,"DashboardCards") as TableLayoutPanel;
            if(cards==null || cards.Controls.Count!=4)
                throw new Exception("Four cards in dashboard row missing.");
            foreach(Control card in cards.Controls)
                if(cards.GetRow(card)!=0)throw new Exception("Cards are not in one row.");
            if(Named(main,"AccountSearch")==null || Named(main,"AccountFilter")==null ||
                Named(main,"PrimaryActionBar")==null || Named(main,"BackupStatusNotice")==null)
                throw new Exception("Table search, filter, action bar or truthful status notice missing.");
            if(Named(main,"DockedLogPanel")==null || Named(main,"ToggleLog")==null)
                throw new Exception("Collapsible log area missing.");
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
                    CheckDashboard(main);
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
                        if(!HasGuideText(guide))throw new Exception("Guide has no chapter body.");
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
