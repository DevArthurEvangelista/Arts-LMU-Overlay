using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Arthur.LmuOverlay
{
    internal sealed class VoiceStatusOverlay : Form
    {
        string heading = "";
        string detail = "";
        Color accent = Color.DeepSkyBlue;
        readonly Timer watchdog = new Timer { Interval = 200 };
        DateTime expires = DateTime.MinValue;
        public Func<bool> ShouldRemainVisible;
        public event Action<string> Expired;

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x080000A0; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT
                return cp;
            }
        }

        public VoiceStatusOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = new Size(380, 78);
            BackColor = Color.FromArgb(17, 24, 33);
            ForeColor = Color.White;
            DoubleBuffered = true;
            watchdog.Tick += delegate
            {
                if (Visible && ((ShouldRemainVisible != null && !ShouldRemainVisible()) || DateTime.UtcNow >= expires))
                {
                    string previous = heading;
                    Clear();
                    var handler = Expired;
                    if (handler != null && (previous == "Ouvindo" || previous == "Transcrevendo"))
                        handler(previous);
                }
            };
            watchdog.Start();
        }

        public void Display(string title, string description, Color color, double durationSeconds)
        {
            heading = title;
            detail = description;
            accent = color;
            expires = DateTime.UtcNow.AddSeconds(durationSeconds > 0 ? durationSeconds : (title == "Ouvindo" ? 14 : 95));
            IntPtr fg = Win32.GetForegroundWindow();
            Rectangle area = fg == IntPtr.Zero ? Screen.PrimaryScreen.WorkingArea : Screen.FromHandle(fg).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 92);
            if (!Visible) Show();
            Invalidate();
        }

        public void Clear()
        {
            if (Visible) Hide();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) watchdog.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width < 20 || Height < 20) return;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, 18, 18, 180f, 90f);
                path.AddArc(Width - 18 - 1, 0, 18, 18, 270f, 90f);
                path.AddArc(Width - 18 - 1, Height - 18 - 1, 18, 18, 0f, 90f);
                path.AddArc(0, Height - 18 - 1, 18, 18, 90f, 90f);
                path.CloseFigure();
                var old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = new GraphicsPath())
            {
                int r = 18;
                path.AddArc(0, 0, r, r, 180f, 90f);
                path.AddArc(Width - r - 1, 0, r, r, 270f, 90f);
                path.AddArc(Width - r - 1, Height - r - 1, r, r, 0f, 90f);
                path.AddArc(0, Height - r - 1, r, r, 90f, 90f);
                path.CloseFigure();
                using (var bg = new SolidBrush(Color.FromArgb(18, 25, 35))) g.FillPath(bg, path);
                using (var border = new Pen(Color.FromArgb(80, accent), 1f)) g.DrawPath(border, path);
            }
            using (var bar = new SolidBrush(accent)) g.FillRectangle(bar, 0, 1, 5, Height - 2);
            using (var ring = new Pen(accent, 3f)) g.DrawEllipse(ring, 19, 22, 30, 30);
            using (var dot = new SolidBrush(accent)) g.FillEllipse(dot, 29, 32, 10, 10);
            using (var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var detailFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var titleBrush = new SolidBrush(Color.FromArgb(247, 249, 252)))
            using (var detailBrush = new SolidBrush(Color.FromArgb(198, 213, 228)))
            {
                g.DrawString(heading, titleFont, titleBrush, 62f, 12f);
                g.DrawString(detail, detailFont, detailBrush, new RectangleF(62f, 42f, Width - 73, 27f));
            }
        }
    }
}
