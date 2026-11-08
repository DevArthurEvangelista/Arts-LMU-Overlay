using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Arthur.LmuOverlay
{
    // A passive HUD: it never takes focus or intercepts steering/mouse input.
    internal sealed class VoiceStatusOverlay : Form
    {
        string heading="",detail="";
        Color accent=Color.DeepSkyBlue;
        readonly Timer watchdog=new Timer { Interval=200 };
        DateTime expires=DateTime.MinValue;
        public Func<bool> ShouldRemainVisible;
        public event Action<string> Expired;
        public VoiceStatusOverlay()
        {
            FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=false;
            StartPosition=FormStartPosition.Manual;
            TopMost=true;
            Size=new Size(380,78);
            BackColor=Color.FromArgb(17,24,33);
            ForeColor=Color.White;
            DoubleBuffered=true;
            watchdog.Tick+=delegate
            {
                if(!Visible) return;
                if((ShouldRemainVisible!=null&&!ShouldRemainVisible()) || DateTime.UtcNow>=expires)
                {
                    string previous=heading;Clear();
                    var handler=Expired;if(handler!=null&&(previous=="Ouvindo"||previous=="Transcrevendo")) handler(previous);
                }
            };
            watchdog.Start();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p=base.CreateParams; p.ExStyle|=0x80|0x20|0x08000000; return p; }
        }
        public void Display(string title,string description,Color color,double durationSeconds)
        {
            heading=title;detail=description;accent=color;
            // The HUD owns its lifetime even if the main telemetry timer stalls.
            expires=DateTime.UtcNow.AddSeconds(durationSeconds>0?durationSeconds:title=="Ouvindo"?14:95);
            var foreground=Win32.GetForegroundWindow();
            var area=foreground==IntPtr.Zero ? Screen.PrimaryScreen.WorkingArea : Screen.FromHandle(foreground).WorkingArea;
            Location=new Point(area.Left+(area.Width-Width)/2,area.Bottom-Height-92);
            if(!Visible) Show();
            Invalidate();
        }
        public void Clear() { if(Visible) Hide(); }
        protected override void Dispose(bool disposing) { if(disposing) watchdog.Dispose();base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=new GraphicsPath())
            {
                int r=18;
                path.AddArc(0,0,r,r,180,90);path.AddArc(Width-r-1,0,r,r,270,90);
                path.AddArc(Width-r-1,Height-r-1,r,r,0,90);path.AddArc(0,Height-r-1,r,r,90,90);path.CloseFigure();
                var old=Region;Region=new Region(path);if(old!=null)old.Dispose();
                using(var background=new SolidBrush(Color.FromArgb(18,25,35))) g.FillPath(background,path);
                using(var border=new Pen(Color.FromArgb(80,accent),1)) g.DrawPath(border,path);
            }
            using(var bar=new SolidBrush(accent)) g.FillRectangle(bar,0,1,5,Height-2);
            using(var ring=new Pen(accent,3)) g.DrawEllipse(ring,19,22,30,30);
            using(var dot=new SolidBrush(accent)) g.FillEllipse(dot,29,32,10,10);
            using(var titleFont=new Font("Segoe UI",12,FontStyle.Bold))
            using(var detailFont=new Font("Segoe UI",9,FontStyle.Regular))
            using(var main=new SolidBrush(Color.FromArgb(247,249,252)))
            using(var muted=new SolidBrush(Color.FromArgb(198,213,228)))
            {
                g.DrawString(heading,titleFont,main,62,12);
                g.DrawString(detail,detailFont,muted,new RectangleF(62,42,Width-73,27));
            }
        }
    }
}
