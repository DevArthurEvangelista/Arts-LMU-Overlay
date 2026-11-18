using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Arthur.LmuOverlay
{
    public sealed partial class Painter
    {
        static Color FlashAccent(DeltaFlashGrade grade)
        {
            switch (grade)
            {
                case DeltaFlashGrade.Purple: return Color.FromArgb(202, 114, 255);
                case DeltaFlashGrade.Green:  return Theme.Throttle;
                case DeltaFlashGrade.Orange: return Color.FromArgb(255, 140, 52);
                case DeltaFlashGrade.Red:    return Theme.Brake;
                case DeltaFlashGrade.Cyan:   return Color.FromArgb(72, 206, 255);
                default:                     return Theme.Fuel;
            }
        }

        // Renders unified monospace bold text for the sector/lap/track-limit banner with a readability floor
        // so that even when the user shrinks FontPercent (e.g. 60%) on a compact Delta card, all values
        // stay large, crisp, and share the exact same Consolas Bold font.
        void FlashMonoText(Graphics g, string text, float x, float y, float w, float h, float designSize, Color c, StringAlignment align, bool withShadow = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            var state = g.Save();
            PointF[] points = { new PointF(x, y), new PointF(x + w, y + h) };
            float scale;
            using (var transform = g.Transform)
            {
                var m = transform.Elements;
                scale = Math.Min((float)Math.Sqrt(m[0] * m[0] + m[1] * m[1]), (float)Math.Sqrt(m[2] * m[2] + m[3] * m[3]));
                transform.TransformPoints(points);
            }
            g.ResetTransform();
            float userFactor = Appearance == null ? 1f : Math.Max(0.92f, Appearance.FontPercent / 100f);
            float pixels = (float)Math.Round(designSize * scale * userFactor * 4) / 4;
            var box = new RectangleF(points[0].X, points[0].Y, points[1].X - points[0].X, points[1].Y - points[0].Y);
            g.SetClip(box, CombineMode.Intersect);

            using (var sf = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                var font = Font(Math.Max(11f, pixels), true, true);
                float maxH = Math.Max(8f, box.Height - 2f);
                float maxW = Math.Max(12f, box.Width - 2f);
                float measuredW = g.MeasureString(text, font, int.MaxValue, sf).Width;
                float fit = Math.Min(1f, Math.Min(maxH / font.GetHeight(g), maxW / Math.Max(1f, measuredW * 1.04f)));
                if (fit < 1f)
                    font = Font(Math.Max(9f, (float)Math.Floor(font.Size * fit * 4) / 4), true, true);

                if (withShadow)
                {
                    var shadow = box;
                    shadow.Offset(Math.Max(1f, scale), Math.Max(1f, scale));
                    g.DrawString(text, font, Brush(Color.FromArgb(215, 0, 0, 0)), shadow, sf);
                }
                g.DrawString(text, font, Brush(c), box, sf);
            }
            g.Restore(state);
        }

        public void DeltaBar(Graphics g, Frame f, ViewOptions o, bool unlocked)
        {
            Prepare(g);
            int cardH = WidgetSize(PanelKind.Delta).Height;
            if (o.DeltaOpacity > 0 || unlocked)
                Shell(g, 460, cardH, unlocked ? Math.Max(35, o.DeltaOpacity) : o.DeltaOpacity, unlocked);

            int m = DeltaMode.Normalize(o.DeltaReference);
            var s = f.Delta;
            double v = f.HasTelemetry && !f.Stale ? s.Values[m] : double.NaN;
            bool live = Format.Finite(v);
            Color c = !live || Math.Abs(v) < .0005 ? Theme.Text : v < 0 ? Theme.Throttle : Theme.Brake;

            if (unlocked) Text(g, "POSICIONAR · DELTA", 14, 0, 432, 18, 11, Theme.Text, true);
            Text(g, live ? LearningFormat.Delta(v, 3) : "—", 12, 16, 436, 43, 36, c, true, true, StringAlignment.Center);

            float h = (float)Format.Clamp(o.DeltaThickness, 6, 28), y = 58 + (28 - h) / 2;
            Box(g, Alpha(Color.Black, 165), 21, y - 1, 418, h+2, Math.Min(6, h / 2));
            Box(g, Alpha(Theme.Grid, 210), 22, y, 416, h, Math.Min(5, h / 2));
            if (live)
            {
                float w = (float)(Format.Clamp(Math.Abs(v) / Math.Max(1, o.DeltaRange), 0, 1) * 208);
                if (w > .1) Box(g, c, v < 0 ? 230 - w : 230, y, w, h, 2);
            }
            // Subtle amber/red center tick when the current lap is invalidated, crisp white when valid
            bool lapInvalidNow = f.HasTelemetry && !f.Stale && (f.LapInvalid || (s != null && s.Invalid));
            Line(g, lapInvalidNow ? Theme.Fuel : Theme.Text, 230, y - 2, 230, y + h + 2, 2.2f);

            double clock = s != null && s.Clock > 0 ? s.Clock : DeltaTracker.FlashClock(f);
            var flash = f.HasTelemetry && !f.Stale && s != null ? s.Flash : null;
            if (flash != null && flash.Active(clock))
            {
                double fade = flash.OpacityFactor(clock);
                int aBg = (int)Math.Round(235 * fade);
                int aFg = (int)Math.Round(255 * fade);
                Color rawAccent = FlashAccent(flash.Grade);
                Color accent = Alpha(rawAccent, aFg);
                Color tagText = flash.Grade == DeltaFlashGrade.Red
                    ? Alpha(Color.White, aFg)
                    : Alpha(Color.FromArgb(10, 14, 20), aFg);

                float px = 21, py = 94, pw = 418, ph = 50;
                Box(g, Color.FromArgb(aBg, 10, 16, 24), px, py, pw, ph, 8);
                using (var border = Rounded(px, py, pw, ph, 8))
                using (var pen = new Pen(Alpha(rawAccent, (int)Math.Round(225 * fade)), 2f))
                    g.DrawPath(pen, border);

                if (flash.IsStatusAlert)
                {
                    float tagW = flash.Label.Length <= 3 ? 58 : flash.Label.Length >= 7 ? 102 : 92;
                    Box(g, accent, px + 6, py + 6, tagW, ph - 12, 6);
                    FlashMonoText(g, flash.Label, px + 6, py + 6, tagW, ph - 12, 18.5f, tagText, StringAlignment.Center, false);

                    float bodyStart = px + tagW + 10;
                    float availW = pw - tagW - 18;
                    if (!string.IsNullOrEmpty(flash.PointsText))
                    {
                        float msgW = availW * 0.54f;
                        float ptsW = availW * 0.46f;
                        FlashMonoText(g, flash.StatusMessage, bodyStart, py + 4, msgW, ph - 8, 22.5f, accent, StringAlignment.Near, true);
                        FlashMonoText(g, flash.PointsText, bodyStart + msgW, py + 4, ptsW, ph - 8, 20.5f, Alpha(Theme.Text, aFg), StringAlignment.Far, true);
                    }
                    else
                    {
                        FlashMonoText(g, flash.StatusMessage, bodyStart, py + 4, availW, ph - 8, 24.5f, accent, StringAlignment.Center, true);
                    }
                }
                else
                {
                    string tag = flash.IsLap ? "VOLTA" : flash.Label;
                    float tagW = flash.IsLap ? 92 : 66;
                    Box(g, accent, px + 6, py + 6, tagW, ph - 12, 6);
                    FlashMonoText(g, tag, px + 6, py + 6, tagW, ph - 12, 20f, tagText, StringAlignment.Center, false);

                    string mainTime = flash.IsLap ? Format.Lap(flash.Time) : LearningFormat.Seconds(flash.Time);
                    double safeDiff = Format.Finite(flash.Diff) ? flash.Diff : 0.0;
                    string diffStr = LearningFormat.Delta(safeDiff, 3);

                    float numStart = px + tagW + 14;
                    float availW = pw - tagW - 22;
                    float leftW = availW * 0.48f;
                    float rightW = availW * 0.52f;
                    FlashMonoText(g, mainTime, numStart, py + 4, leftW, ph - 8, 27f, Alpha(Theme.Text, aFg), StringAlignment.Near, true);
                    FlashMonoText(g, diffStr, numStart + leftW, py + 4, rightW, ph - 8, 27f, accent, StringAlignment.Far, true);
                }
            }
        }
    }
}
