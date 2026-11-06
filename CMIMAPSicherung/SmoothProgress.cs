
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    /// <summary>
    /// Paint-only animated counterpart of an existing ProgressBar. The existing bar
    /// remains hidden as a child: old worker code still updates its Style and Value.
    /// </summary>
    internal sealed class SmoothProgress : Control
    {
        private readonly ProgressBar model;
        private readonly Timer timer;
        private int phase;
        public SmoothProgress(ProgressBar source)
        {
            model=source;
            DoubleBuffered=true;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|
                ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            BackColor=UiTheme.Window;
            ForeColor=UiTheme.Primary;
            AccessibleName="Fortschritt";
            model.Visible=false;
            Controls.Add(model);
            model.Dock=DockStyle.None;
            model.Size=new Size(1,1);
            model.Location=new Point(-10,-10);
            timer=new Timer {Interval=35};
            timer.Tick+=delegate { if(!IsDisposed && Visible) {phase=(phase+3)%600;Invalidate();} };
            timer.Start();
        }
        public static void Replace(ProgressBar source)
        {
            if(source==null || source.Parent==null) return;
            var parent=source.Parent;
            var table=parent as TableLayoutPanel;
            int row=0,col=0,rowSpan=1,colSpan=1;
            if(table!=null)
            {
                row=table.GetRow(source);col=table.GetColumn(source);
                rowSpan=table.GetRowSpan(source);colSpan=table.GetColumnSpan(source);
            }
            var box=new SmoothProgress(source) {
                Dock=source.Dock,Anchor=source.Anchor,
                Margin=source.Margin,Height=Math.Max(18,source.Height),
                MinimumSize=new Size(0,18)
            };
            parent.Controls.Remove(source);
            if(table!=null)
            {
                table.Controls.Add(box,col,row);
                table.SetRowSpan(box,rowSpan);
                table.SetColumnSpan(box,colSpan);
            }
            else parent.Controls.Add(box);
            box.BringToFront();
        }
        private static GraphicsPath Round(Rectangle rect,int radius)
        {
            GraphicsPath path=new GraphicsPath();
            if(rect.Width<=0 || rect.Height<=0)return path;
            int r=Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
            path.AddArc(rect.Left,rect.Top,r,r,180,90);
            path.AddArc(rect.Right-r,rect.Top,r,r,270,90);
            path.AddArc(rect.Right-r,rect.Bottom-r,r,r,0,90);
            path.AddArc(rect.Left,rect.Bottom-r,r,r,90,90);
            path.CloseFigure();
            return path;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g=e.Graphics;
            g.SmoothingMode=SmoothingMode.AntiAlias;
            var track=new Rectangle(1,2,Math.Max(1,Width-3),Math.Max(5,Height-5));
            using(var path=Round(track,7))
            using(var bg=new SolidBrush(Color.FromArgb(53,58,68)))
            using(var pen=new Pen(Color.FromArgb(77,88,104)))
            {
                g.FillPath(bg,path);
                g.DrawPath(pen,path);
            }
            var isBusy=model.Style==ProgressBarStyle.Marquee && model.MarqueeAnimationSpeed>0;
            var max=Math.Max(1,model.Maximum-model.Minimum);
            float ratio=(float)(model.Value-model.Minimum)/max;
            ratio=Math.Max(0,Math.Min(1,ratio));
            Rectangle fill;
            if(isBusy)
            {
                int w=Math.Max(28,track.Width/4);
                int cycle=Math.Max(1,track.Width+w);
                int x=track.Left-w+(phase*3)%cycle;
                fill=new Rectangle(x,track.Top,w,track.Height);
            }
            else
            {
                fill=new Rectangle(track.X,track.Y,(int)Math.Round(track.Width*ratio),track.Height);
            }
            if(fill.Width>1)
            {
                using(var clip=Round(track,7))
                {
                    var state=g.Save();
                    g.SetClip(clip);
                    using(var gradient=new LinearGradientBrush(fill,Color.FromArgb(24,136,222),
                        Color.FromArgb(74,214,234),LinearGradientMode.Horizontal))
                        g.FillRectangle(gradient,fill);
                    if(isBusy)
                    {
                        using(var glow=new SolidBrush(Color.FromArgb(85,190,248,255)))
                            g.FillRectangle(glow,new Rectangle(fill.Right-7,fill.Top,7,fill.Height));
                    }
                    else if(ratio>0 && ratio<1 && Width>80)
                    {
                        int pulse=(phase%120)*3;
                        var shine=new Rectangle(track.X+pulse-40,track.Y,30,track.Height);
                        using(var brush=new SolidBrush(Color.FromArgb(34,255,255,255)))
                            g.FillRectangle(brush,shine);
                    }
                    g.Restore(state);
                }
            }
            if(!isBusy && Height>=25 && Width>70)
            {
                string label=(ratio*100).ToString("0")+" %";
                TextRenderer.DrawText(g,label,new Font("Segoe UI",8F,FontStyle.Bold),track,
                    Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing) {timer.Stop();timer.Dispose();}
            base.Dispose(disposing);
        }
    }
}
