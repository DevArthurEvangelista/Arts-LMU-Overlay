using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Arthur.LmuOverlay
{
    public static class WheelStyle
    {
        public static readonly string[] Names =
        {
            "GT Aberto (Padrão Arts)",
            "iRacing Clássico (Minimalista)",
            "Estilo G29 (Couro e Azul)",
            "Estilo G920 (Dark e Prata)",
            "Estilo MOZA / Monza (Fórmula KS/GS)",
            "Estilo Fanatec (ClubSport GT3)",
            "Estilo Thrustmaster (GT Competição)",
            "Estilo PXN (Sport D-Shape)"
        };

        public const int Count = 8;

        public static int Normalize(int style)
        {
            return style < 0 || style >= Count ? 0 : style;
        }
    }

    public sealed partial class Painter
    {
        public Bitmap GetWheelBitmap(int style)
        {
            int idx = WheelStyle.Normalize(style);
            if (wheelArtworks[idx] == null)
            {
                var bmp = new Bitmap(320, 320, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var art = Graphics.FromImage(bmp))
                {
                    art.Clear(Color.Transparent);
                    Prepare(art);
                    art.SmoothingMode = SmoothingMode.HighQuality;
                    art.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    if (idx == 0)
                    {
                        art.ScaleTransform(2, 2);
                        art.TranslateTransform(80 - 571, 80 - 153);
                        RenderWheelArtwork(art);
                    }
                    else
                    {
                        art.ScaleTransform(2, 2);
                        art.TranslateTransform(80, 80);
                        art.ScaleTransform(.54f, .54f);
                        RenderCustomWheelStyle(art, idx);
                    }
                }
                wheelArtworks[idx] = bmp;
            }
            return wheelArtworks[idx];
        }

        public void DrawWheelPreview(Graphics g, int style, Rectangle bounds, double angle = 0)
        {
            Prepare(g);
            var bmp = GetWheelBitmap(style);
            var state = g.Save();
            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f;
            float size = Math.Min(bounds.Width, bounds.Height);
            g.TranslateTransform(cx, cy);
            if (Format.Finite(angle) && Math.Abs(angle) > 0.01) g.RotateTransform((float)angle);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, -size / 2f, -size / 2f, size, size);
            g.Restore(state);
        }

        void RenderCustomWheelStyle(Graphics g, int style)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            switch (style)
            {
                case 1: DrawIracingWheel(g); break;
                case 2: DrawLogitechWheel(g, true); break;
                case 3: DrawLogitechWheel(g, false); break;
                case 4: DrawMozaWheel(g); break;
                case 5: DrawFanatecWheel(g); break;
                case 6: DrawThrustmasterWheel(g); break;
                case 7: DrawPxnWheel(g); break;
            }
        }

        void DrawIracingWheel(Graphics g)
        {
            // 100% pure subpixel anti-aliased vector geometry (no Region scanlines).
            Color rimBase = Color.FromArgb(204, 212, 222);
            Color spokeFill = Color.FromArgb(196, 205, 216);
            Color innerEdge = Color.FromArgb(118, 130, 145);
            Color outerEdge = Color.FromArgb(72, 84, 98);
            Color highlight = Color.FromArgb(95, 255, 255, 255);
            Color marker = Color.FromArgb(245, 184, 42);
            Color hubRing = Color.FromArgb(152, 163, 176);

            // 1. Sculpted T-spoke structure tucked cleanly inside the circular rim (radius 93.5)
            using (var tSpoke = new GraphicsPath())
            {
                tSpoke.AddBezier(-89, -6, -45, -7, -22, -9, 0, -9);
                tSpoke.AddBezier(0, -9, 22, -9, 45, -7, 89, -6);
                tSpoke.AddLine(89, -6, 89, 14);
                // Right smooth curved fillet into vertical bottom stem
                tSpoke.AddBezier(89, 14, 42, 14, 20, 15, 14, 21);
                tSpoke.AddBezier(14, 21, 11, 25, 11, 48, 11, 89);
                tSpoke.AddLine(11, 89, -11, 89);
                // Left smooth curved fillet back up to horizontal bar
                tSpoke.AddBezier(-11, 89, -11, 48, -11, 25, -14, 21);
                tSpoke.AddBezier(-14, 21, -20, 15, -42, 14, -89, 14);
                tSpoke.CloseFigure();

                g.FillPath(Brush(spokeFill), tSpoke);
                using (var spokeBorder = new Pen(innerEdge, 2f) { LineJoin = LineJoin.Round })
                    g.DrawPath(spokeBorder, tSpoke);
            }

            // 2. Crisp anti-aliased circular outer rim (outer R=104, inner R=83 -> mid R=93.5, width=21)
            using (var rimPen = new Pen(rimBase, 21f))
                g.DrawEllipse(rimPen, -93.5f, -93.5f, 187f, 187f);

            // Subtle vector specular highlight along center of the circular rim
            using (var shinePen = new Pen(highlight, 3.2f))
                g.DrawEllipse(shinePen, -95.5f, -95.5f, 191f, 191f);

            // 3. Radial 12-o'clock yellow/gold marker drawn as a true vector arc
            using (var markerPen = new Pen(marker, 21f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                g.DrawArc(markerPen, -93.5f, -93.5f, 187f, 187f, 264.6f, 10.8f);

            // 4. Clean anti-aliased inner and outer circular vector strokes
            using (var inPen = new Pen(innerEdge, 2f))
            {
                // Draw inner arc only along the three open windows so spoke junctions stay seamless
                g.DrawArc(inPen, -83f, -83f, 166f, 166f, 184.5f, 171f);
                g.DrawArc(inPen, -83f, -83f, 166f, 166f, 9.8f, 72.5f);
                g.DrawArc(inPen, -83f, -83f, 166f, 166f, 97.7f, 72.5f);
            }
            using (var outPen = new Pen(outerEdge, 2.2f))
                g.DrawEllipse(outPen, -104f, -104f, 208f, 208f);

            // 5. Subtle recessed vector center cap detail
            using (var capPen = new Pen(hubRing, 1.6f))
                g.DrawEllipse(capPen, -13f, -8f, 26f, 26f);
        }

        void DrawLogitechWheel(Graphics g, bool g29)
        {
            Color leather = Color.FromArgb(24, 30, 37);
            Color leatherEdge = Color.FromArgb(78, 92, 106);
            Color plateColor = g29 ? Color.FromArgb(34, 42, 52) : Color.FromArgb(22, 26, 32);
            Color metal = Color.FromArgb(136, 148, 162);
            Color dark = Color.FromArgb(11, 15, 20);
            Color stripe = g29 ? Color.FromArgb(30, 136, 255) : Color.FromArgb(214, 222, 232);
            Color accentRed = Color.FromArgb(224, 46, 62);

            for (int side = -1; side <= 1; side += 2)
            {
                using (var paddle = Rounded(side < 0 ? -82 : 52, -46, 30, 68, 6))
                {
                    g.FillPath(Brush(Color.FromArgb(96, 108, 122)), paddle);
                    using (var p = new Pen(leatherEdge, 1.5f)) g.DrawPath(p, paddle);
                }
            }

            using (var plate = new GraphicsPath())
            {
                plate.AddPolygon(new[]
                {
                    new PointF(-86, -20), new PointF(-44, -28), new PointF(44, -28), new PointF(86, -20),
                    new PointF(88, 12), new PointF(34, 24), new PointF(20, 88), new PointF(-20, 88),
                    new PointF(-34, 24), new PointF(-88, 12)
                });
                g.FillPath(Brush(plateColor), plate);
                using (var p = new Pen(leatherEdge, 1.6f)) g.DrawPath(p, plate);
            }

            if (!g29)
                Box(g, Color.FromArgb(182, 192, 204), -13, 44, 26, 40, 4);

            // Pure anti-aliased Alternate-fill path for flat-bottom leather rim
            using (var outer = MakeFlatBottomCircle(106, 95))
            using (var inner = MakeFlatBottomCircle(84, 75))
            using (var donut = new GraphicsPath(FillMode.Alternate))
            {
                donut.AddPath(outer, false);
                donut.AddPath(inner, false);
                g.FillPath(Brush(leather), donut);
                using (var stripePen = new Pen(stripe, 22f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                    g.DrawArc(stripePen, -95f, -95f, 190f, 190f, 265.5f, 9f);
                using (var p = new Pen(leatherEdge, 1.8f)) { g.DrawPath(p, outer); g.DrawPath(p, inner); }
            }

            using (var stitch = new Pen(g29 ? Color.FromArgb(110, 70, 145, 230) : Color.FromArgb(100, 175, 185, 198), 1.4f) { DashStyle = DashStyle.Dot })
                g.DrawArc(stitch, -86, -86, 172, 172, 150, 60);

            if (g29)
            {
                Box(g, dark, -26, -24, 52, 9, 4);
                Color[] leds = { Theme.Throttle, Theme.Throttle, Theme.Fuel, Theme.Fuel, Theme.Brake };
                for (int i = 0; i < 5; i++)
                {
                    g.FillEllipse(Brush(leds[i]), -20 + i * 9, -22, 5, 5);
                    g.FillEllipse(Brush(leds[4 - i]), 20 - i * 9 - 5, -22, 5, 5);
                }
            }
            else
            {
                g.FillEllipse(Brush(Color.FromArgb(225, 232, 240)), -4, -22, 8, 8);
            }

            g.FillEllipse(Brush(dark), -72, -16, 28, 28);
            Box(g, metal, -61, -13, 6, 22, 1);
            Box(g, metal, -69, -5, 22, 6, 1);

            PointF[] btns = { new PointF(58, -12), new PointF(49, -3), new PointF(67, -3), new PointF(58, 6) };
            Color[] btnColors = g29
                ? new[] { Color.FromArgb(62, 198, 168), Color.FromArgb(232, 108, 158), Color.FromArgb(235, 82, 82), Color.FromArgb(108, 156, 238) }
                : new[] { Color.FromArgb(190, 198, 208), Color.FromArgb(165, 174, 184), Color.FromArgb(165, 174, 184), Color.FromArgb(190, 198, 208) };
            for (int i = 0; i < 4; i++)
            {
                g.FillEllipse(Brush(dark), btns[i].X - 5, btns[i].Y - 5, 10, 10);
                g.FillEllipse(Brush(btnColors[i]), btns[i].X - 3.5f, btns[i].Y - 3.5f, 7, 7);
            }

            if (g29)
            {
                g.FillEllipse(Brush(dark), 14, 24, 26, 26);
                g.FillEllipse(Brush(accentRed), 16, 26, 22, 22);
                g.FillEllipse(Brush(leather), 21, 31, 12, 12);
                Box(g, dark, -34, 26, 16, 24, 4);
                Box(g, stripe, -32, 28, 12, 9, 2);
                Box(g, stripe, -32, 39, 12, 9, 2);
            }
            else
            {
                g.FillEllipse(Brush(dark), -28, 27, 13, 13);
                g.FillEllipse(Brush(metal), -25, 30, 7, 7);
                g.FillEllipse(Brush(dark), 15, 27, 13, 13);
                g.FillEllipse(Brush(metal), 18, 30, 7, 7);
            }

            g.FillEllipse(Brush(dark), -24, -14, 48, 48);
            using (var ringPen = new Pen(g29 ? stripe : metal, 2f)) g.DrawEllipse(ringPen, -18, -8, 36, 36);
            for (int i = 0; i < 6; i++)
            {
                double rad = i * Math.PI / 3.0;
                float bx = (float)(Math.Cos(rad) * 20);
                float by = 10 + (float)(Math.Sin(rad) * 20);
                g.FillEllipse(Brush(metal), bx - 2.2f, by - 2.2f, 4.4f, 4.4f);
            }
        }

        void DrawMozaWheel(Graphics g)
        {
            Color grip = Color.FromArgb(20, 25, 32);
            Color carbon = Color.FromArgb(31, 38, 46);
            Color edge = Color.FromArgb(92, 108, 124);
            Color cyan = Color.FromArgb(0, 210, 255);
            Color amber = Color.FromArgb(255, 184, 46);
            Color magenta = Color.FromArgb(218, 92, 255);
            Color green = Color.FromArgb(56, 229, 140);
            Color dark = Color.FromArgb(10, 14, 19);

            using (var body = new GraphicsPath())
            using (var cutoutL = Rounded(-86, -34, 30, 58, 12))
            using (var cutoutR = Rounded(56, -34, 30, 58, 12))
            using (var yoke = new GraphicsPath(FillMode.Alternate))
            {
                body.AddPolygon(new[]
                {
                    new PointF(-66, -62), new PointF(66, -62),
                    new PointF(100, -52), new PointF(108, -22), new PointF(106, 28), new PointF(94, 58),
                    new PointF(64, 62), new PointF(40, 44), new PointF(-40, 44), new PointF(-64, 62),
                    new PointF(-94, 58), new PointF(-106, 28), new PointF(-108, -22), new PointF(-100, -52)
                });
                yoke.AddPath(body, false);
                yoke.AddPath(cutoutL, false);
                yoke.AddPath(cutoutR, false);
                g.FillPath(Brush(carbon), yoke);
                using (var weave = new HatchBrush(HatchStyle.ForwardDiagonal, Color.FromArgb(42, 74, 90, 104), Color.Transparent))
                    g.FillPath(weave, yoke);
                using (var p = new Pen(edge, 1.8f)) { g.DrawPath(p, body); g.DrawPath(p, cutoutL); g.DrawPath(p, cutoutR); }
            }

            for (int s = -1; s <= 1; s += 2)
            {
                Box(g, grip, s < 0 ? -107 : 88, -42, 19, 92, 8);
                Box(g, Color.FromArgb(70, 84, 98), s < 0 ? -68 : 54, -26, 14, 10, 3);
                Line(g, cyan, s < 0 ? -65 : 57, -21, s < 0 ? -57 : 65, -21, 2);
            }

            Box(g, cyan, -7, -63, 14, 6, 2);
            Box(g, dark, -48, -53, 96, 10, 4);
            Color[] rpmColors = { green, green, green, amber, amber, amber, magenta, magenta };
            for (int i = 0; i < 8; i++)
                Box(g, rpmColors[i], -42 + i * 11, -51, 8, 6, 2);

            Color[] rgbLeft = { cyan, green, amber, magenta };
            Color[] rgbRight = { magenta, amber, green, cyan };
            PointF[] leftPts = { new PointF(-44, -34), new PointF(-49, -16), new PointF(-47, 3), new PointF(-38, 20) };
            for (int i = 0; i < 4; i++)
            {
                g.FillEllipse(Brush(dark), leftPts[i].X - 7, leftPts[i].Y - 7, 14, 14);
                g.FillEllipse(Brush(rgbLeft[i]), leftPts[i].X - 5, leftPts[i].Y - 5, 10, 10);
                float rx = -leftPts[i].X;
                g.FillEllipse(Brush(dark), rx - 7, leftPts[i].Y - 7, 14, 14);
                g.FillEllipse(Brush(rgbRight[i]), rx - 5, leftPts[i].Y - 5, 10, 10);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                float ex = s * 20;
                g.FillEllipse(Brush(dark), ex - 12, 12, 24, 24);
                using (var ring = new Pen(s < 0 ? cyan : amber, 2.4f)) g.DrawEllipse(ring, ex - 9, 15, 18, 18);
                g.FillEllipse(Brush(grip), ex - 5, 19, 10, 10);
            }
            Box(g, dark, -18, -28, 36, 30, 6);
            using (var hubPen = new Pen(cyan, 1.6f)) g.DrawRectangle(hubPen, -13, -23, 26, 20);
        }

        void DrawFanatecWheel(Graphics g)
        {
            Color suede = Color.FromArgb(24, 29, 36);
            Color carbon = Color.FromArgb(35, 42, 50);
            Color edge = Color.FromArgb(96, 110, 124);
            Color yellow = Color.FromArgb(255, 208, 0);
            Color blue = Color.FromArgb(42, 138, 255);
            Color red = Color.FromArgb(232, 52, 66);
            Color dark = Color.FromArgb(10, 14, 19);

            using (var outer = new GraphicsPath())
            using (var cutoutL = Rounded(-84, -34, 28, 62, 10))
            using (var cutoutR = Rounded(56, -34, 28, 62, 10))
            using (var plate = new GraphicsPath(FillMode.Alternate))
            {
                outer.AddPolygon(new[]
                {
                    new PointF(-72, -68), new PointF(72, -68),
                    new PointF(102, -48), new PointF(106, -12), new PointF(104, 34), new PointF(84, 64),
                    new PointF(54, 64), new PointF(40, 48), new PointF(-40, 48), new PointF(-54, 64),
                    new PointF(-84, 64), new PointF(-104, 34), new PointF(-106, -12), new PointF(-102, -48)
                });
                plate.AddPath(outer, false);
                plate.AddPath(cutoutL, false);
                plate.AddPath(cutoutR, false);
                g.FillPath(Brush(carbon), plate);
                using (var weave = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(46, 80, 94, 108), Color.Transparent))
                    g.FillPath(weave, plate);
                using (var p = new Pen(edge, 1.8f)) { g.DrawPath(p, outer); g.DrawPath(p, cutoutL); g.DrawPath(p, cutoutR); }
            }

            for (int s = -1; s <= 1; s += 2)
                Box(g, suede, s < 0 ? -106 : 86, -40, 20, 88, 9);
            Box(g, yellow, -7, -69, 14, 11, 2);

            Box(g, dark, -28, -54, 56, 20, 4);
            using (var oledPen = new Pen(edge, 1f)) g.DrawRectangle(oledPen, -26, -52, 52, 16);
            Box(g, yellow, -18, -46, 36, 4, 1);

            Color[] rotColors = { yellow, blue, red };
            float[] rotX = { -26, 0, 26 };
            float[] rotY = { 26, 34, 26 };
            for (int i = 0; i < 3; i++)
            {
                g.FillEllipse(Brush(dark), rotX[i] - 12, rotY[i] - 12, 24, 24);
                using (var p = new Pen(rotColors[i], 3f)) g.DrawEllipse(p, rotX[i] - 8.5f, rotY[i] - 8.5f, 17, 17);
                g.FillEllipse(Brush(suede), rotX[i] - 5, rotY[i] - 5, 10, 10);
            }

            Color[] caps = { red, yellow, blue };
            for (int i = 0; i < 3; i++)
            {
                float y = -28 + i * 18;
                g.FillEllipse(Brush(dark), -52, y - 6, 12, 12);
                g.FillEllipse(Brush(caps[i]), -49.5f, y - 3.5f, 7, 7);
                g.FillEllipse(Brush(dark), 40, y - 6, 12, 12);
                g.FillEllipse(Brush(caps[2 - i]), 42.5f, y - 3.5f, 7, 7);
            }

            g.FillEllipse(Brush(dark), -17, -22, 34, 34);
            using (var ring = new Pen(edge, 2f)) g.DrawEllipse(ring, -12, -17, 24, 24);
        }

        void DrawThrustmasterWheel(Graphics g)
        {
            Color suede = Color.FromArgb(30, 36, 44);
            Color plate = Color.FromArgb(20, 25, 31);
            Color edge = Color.FromArgb(90, 104, 118);
            Color silver = Color.FromArgb(196, 206, 218);
            Color yellow = Color.FromArgb(245, 196, 0);
            Color red = Color.FromArgb(230, 48, 60);
            Color dark = Color.FromArgb(10, 14, 18);

            for (int s = -1; s <= 1; s += 2)
                Box(g, Color.FromArgb(132, 144, 158), s < 0 ? -80 : 52, -44, 28, 66, 5);

            using (var spokes = new GraphicsPath())
            {
                spokes.AddPolygon(new[]
                {
                    new PointF(-88, -18), new PointF(-36, -24), new PointF(36, -24), new PointF(88, -18),
                    new PointF(88, 12), new PointF(30, 22), new PointF(18, 90), new PointF(-18, 90),
                    new PointF(-30, 22), new PointF(-88, 12)
                });
                g.FillPath(Brush(plate), spokes);
                using (var p = new Pen(edge, 1.5f)) g.DrawPath(p, spokes);
            }

            using (var rimPen = new Pen(suede, 22f))
                g.DrawEllipse(rimPen, -94f, -94f, 188f, 188f);
            using (var stripePen = new Pen(yellow, 22f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                g.DrawArc(stripePen, -94f, -94f, 188f, 188f, 265.2f, 9.6f);
            using (var p = new Pen(edge, 1.8f))
            {
                g.DrawEllipse(p, -105f, -105f, 210f, 210f);
                g.DrawEllipse(p, -83f, -83f, 166f, 166f);
            }

            PointF[] leftBtns = { new PointF(-60, -10), new PointF(-44, -13), new PointF(-54, 4) };
            Color[] leftCols = { red, yellow, Color.FromArgb(48, 150, 255) };
            PointF[] rightBtns = { new PointF(60, -10), new PointF(44, -13), new PointF(54, 4) };
            Color[] rightCols = { Theme.Throttle, Color.FromArgb(230, 236, 242), yellow };
            for (int i = 0; i < 3; i++)
            {
                g.FillEllipse(Brush(dark), leftBtns[i].X - 6, leftBtns[i].Y - 6, 12, 12);
                g.FillEllipse(Brush(leftCols[i]), leftBtns[i].X - 4, leftBtns[i].Y - 4, 8, 8);
                g.FillEllipse(Brush(dark), rightBtns[i].X - 6, rightBtns[i].Y - 6, 12, 12);
                g.FillEllipse(Brush(rightCols[i]), rightBtns[i].X - 4, rightBtns[i].Y - 4, 8, 8);
            }

            g.FillEllipse(Brush(silver), 18, 22, 18, 18);
            Line(g, red, 27, 31, 33, 25, 3f);

            g.FillEllipse(Brush(silver), -25, -22, 50, 50);
            g.FillEllipse(Brush(dark), -16, -13, 32, 32);
            for (int i = 0; i < 6; i++)
            {
                double rad = i * Math.PI / 3.0 - Math.PI / 2.0;
                float bx = (float)(Math.Cos(rad) * 20.5);
                float by = 3 + (float)(Math.Sin(rad) * 20.5);
                g.FillEllipse(Brush(dark), bx - 2.3f, by - 2.3f, 4.6f, 4.6f);
            }
        }

        void DrawPxnWheel(Graphics g)
        {
            Color grip = Color.FromArgb(22, 27, 34);
            Color plate = Color.FromArgb(30, 37, 46);
            Color edge = Color.FromArgb(88, 102, 118);
            Color crimson = Color.FromArgb(255, 46, 68);
            Color dark = Color.FromArgb(10, 14, 19);

            for (int s = -1; s <= 1; s += 2)
            {
                using (var paddle = Rounded(s < 0 ? -84 : 56, -46, 28, 68, 6))
                {
                    g.FillPath(Brush(Color.FromArgb(196, 36, 54)), paddle);
                    using (var p = new Pen(crimson, 1.3f)) g.DrawPath(p, paddle);
                }
            }

            using (var spokes = new GraphicsPath())
            {
                spokes.AddPolygon(new[]
                {
                    new PointF(-88, -16), new PointF(-38, -28), new PointF(38, -28), new PointF(88, -16),
                    new PointF(88, 14), new PointF(34, 24), new PointF(22, 86), new PointF(-22, 86),
                    new PointF(-34, 24), new PointF(-88, 14)
                });
                g.FillPath(Brush(plate), spokes);
                using (var p = new Pen(edge, 1.6f)) g.DrawPath(p, spokes);
            }

            using (var outer = MakeFlatBottomCircle(106, 90))
            using (var inner = MakeFlatBottomCircle(83, 69))
            using (var donut = new GraphicsPath(FillMode.Alternate))
            {
                donut.AddPath(outer, false);
                donut.AddPath(inner, false);
                g.FillPath(Brush(grip), donut);
                using (var stripePen = new Pen(crimson, 23f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                    g.DrawArc(stripePen, -94.5f, -94.5f, 189f, 189f, 265f, 10f);
                using (var p = new Pen(edge, 1.8f)) g.DrawPath(p, outer);
                using (var redInner = new Pen(Color.FromArgb(170, crimson), 1.8f)) g.DrawPath(redInner, inner);
            }

            Box(g, crimson, -63, -12, 6, 20, 1);
            Box(g, crimson, -70, -5, 20, 6, 1);
            PointF[] rBtns = { new PointF(60, -12), new PointF(51, -3), new PointF(69, -3), new PointF(60, 6) };
            for (int i = 0; i < 4; i++)
            {
                g.FillEllipse(Brush(dark), rBtns[i].X - 5, rBtns[i].Y - 5, 10, 10);
                g.FillEllipse(Brush(Color.FromArgb(215, 224, 234)), rBtns[i].X - 3.2f, rBtns[i].Y - 3.2f, 6.4f, 6.4f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                g.FillEllipse(Brush(dark), s * 22 - 10, 26, 20, 20);
                g.FillEllipse(Brush(crimson), s * 22 - 7, 29, 14, 14);
                g.FillEllipse(Brush(grip), s * 22 - 3.5f, 32.5f, 7, 7);
            }

            using (var hex = new GraphicsPath())
            {
                var pts = new PointF[6];
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3.0 + Math.PI / 6.0;
                    pts[i] = new PointF((float)(Math.Cos(a) * 21), 2 + (float)(Math.Sin(a) * 21));
                }
                hex.AddPolygon(pts);
                g.FillPath(Brush(dark), hex);
                using (var p = new Pen(crimson, 2f)) g.DrawPath(p, hex);
            }
        }

        static GraphicsPath MakeFlatBottomCircle(float r, float flatY)
        {
            var p = new GraphicsPath();
            double ratio = Math.Max(-0.99, Math.Min(0.99, flatY / r));
            float deg = (float)(Math.Asin(ratio) * 180.0 / Math.PI);
            float startDeg = 180f - deg;
            float sweepDeg = 180f + 2f * deg;
            p.AddArc(-r, -r, r * 2, r * 2, startDeg, sweepDeg);
            p.CloseFigure();
            return p;
        }
    }
}
