using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;

namespace Arthur.LmuOverlay
{
    public static class AppInfo
    {
        public const string Name="Arts LMU OVERLAY", Version="0.12.4", AssemblyVersion="0.12.4.0";
    }
    public static class BrandArtwork
    {
        // Keep the user's original PNG intact. These source rectangles fit it to each UI surface.
        public static readonly Rectangle LogoBounds=new Rectangle(108,49,296,414);
        public static readonly Rectangle IconBounds=new Rectangle(108,49,296,296);
        public static Bitmap LoadLogo()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BrandLogo.png"))
            {
                if(stream==null) throw new InvalidOperationException("A logo do Arts não está no executável.");
                using(var original=Image.FromStream(stream)) return new Bitmap(original);
            }
        }
        public static void Header(Graphics g,Image logo,float width=684,float height=108)
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            using(var tile=new GraphicsPath())
            {
                tile.AddEllipse(2,16,66,66);var state=g.Save();g.SetClip(tile,CombineMode.Intersect);
                g.DrawImage(logo,new Rectangle(2,16,66,66),IconBounds,GraphicsUnit.Pixel);g.Restore(state);
            }
            using(var primary=new SolidBrush(Theme.Text))
            using(var muted=new SolidBrush(Theme.Muted))
            using(var accent=new SolidBrush(Theme.Rpm))
            using(var arts=new Font(Theme.DisplayFont,32,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var name=new Font(Theme.BodyFont,13,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var detail=new Font(Theme.BodyFont,12,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var line=new Pen(Theme.Grid))
            {
                g.DrawString("Arts",arts,primary,87,14);
                g.DrawString("LMU OVERLAY",name,accent,168,28);
                g.DrawString("Telemetria e análise de voltas",detail,muted,89,58);
                g.DrawString("v"+AppInfo.Version,detail,muted,315,28);
                g.DrawLine(line,0,height-1,width,height-1);
            }
        }
    }

    // This guard belongs to the UI thread. Pause window work through the whole popup lifecycle.
    public sealed class PopupInteraction
    {
        bool open;
        double resumeAt;
        public void Open() { open=true; }
        public void Close(double now) { open=false;resumeAt=now+.4; }
        public bool Blocks(double now) { return open || now<resumeAt; }
    }
}
