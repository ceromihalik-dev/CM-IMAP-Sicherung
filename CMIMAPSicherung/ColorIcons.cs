
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    /// <summary>Colored, rasterized icons rather than monochrome WinForms emoji glyphs.</summary>
    internal static class ColorIcons
    {
        private static readonly string[] Signs = {
            "💾","📥","⏹","🗄","♻","✉","📂","➕",
            "✏","🔌","🔑","🗑","🛠","🕒","📋","📚",
            "📄","⚙","ℹ","❤️","📖","🎯","🔐","📊",
            "👤","🔎","✅","🧭","🛡","🧹","🖥","❓"
        };
        private static Bitmap atlas;
        private static Bitmap atlas2;
        private static readonly Dictionary<int, Image> cache = new Dictionary<int, Image>();
        public static void Apply(Control root)
        {
            foreach(Control control in Walk(root))
            {
                var b=control as Button;
                if(b==null || b.IsDisposed || b.Tag as string == "color-icon-applied") continue;
                string text=b.Text ?? "";
                int index=Array.FindIndex(Signs,x=>text.Contains(x));
                if(index<0) continue;
                Image picture=Picture(index);
                if(picture==null) continue;
                b.Text = text.Replace(Signs[index],"").Replace("  "," ").Trim();
                b.Image = picture;
                b.ImageAlign=ContentAlignment.MiddleLeft;
                b.TextAlign=ContentAlignment.MiddleLeft;
                b.TextImageRelation=TextImageRelation.ImageBeforeText;
                b.Padding = new Padding(9,0,5,0);
                b.Tag="color-icon-applied";
            }
        }
        private static IEnumerable<Control> Walk(Control root)
        {
            foreach(Control c in root.Controls)
            {
                yield return c;
                foreach(Control nested in Walk(c)) yield return nested;
            }
        }
        private static Image Picture(int index)
        {
            if(cache.ContainsKey(index)) return cache[index];
            try
            {
                if(atlas==null)
                {
                    using(Stream stream=typeof(ColorIcons).Assembly.GetManifestResourceStream("CMIMAPSicherung.Resources.emoji-icons.png"))
                    {
                        if(stream==null) return null;
                        using(Image original=Image.FromStream(stream)) atlas=new Bitmap(original);
                    }
                }
                if(index>=24 && atlas2==null)
                {
                    using(Stream stream=typeof(ColorIcons).Assembly.GetManifestResourceStream("CMIMAPSicherung.Resources.additional-icons.png"))
                    {
                        if(stream==null)return null;
                        using(Image original=Image.FromStream(stream))atlas2=new Bitmap(original);
                    }
                }
                Rectangle cell=new Rectangle(index%8*30,(index>=24?0:index/8*30),30,30);
                // Scale down slightly for a consistent, aligned button icon.
                Bitmap image=new Bitmap(25,25);
                using(Graphics g=Graphics.FromImage(image))
                {
                    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(index>=24?atlas2:atlas,new Rectangle(0,0,25,25),cell,GraphicsUnit.Pixel);
                }
                cache[index]=image;
                return image;
            }
            catch {return null;}
        }
    }
}
