using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Arthur.LmuOverlay;

partial class Checks
{
    static Frame SectorTestFrame(double elapsed, double lapStart, int laps, int sector, double curS1, double curS2, double lastLap, double lastS2, double playerBestLap, double rivalBestS1, double rivalBestLap)
    {
        var f = DFrame(elapsed, lapStart, sector == 1 ? 500 : sector == 2 ? 2200 : 3600, laps);
        f.TrackLength = 5000;
        var p = f.Drivers[0];
        p.Class = "LMGT3";
        p.Sector = sector;
        p.CountLapFlag = 2;
        p.CurrentSector1 = curS1;
        p.CurrentSector2 = curS2;
        p.LastLap = lastLap;
        p.LastSector1 = 30.800;
        p.LastSector2 = lastS2;
        p.BestLap = playerBestLap;

        var rival = new Driver
        {
            Id = 99,
            Name = "Rival GT3",
            Class = "LMGT3",
            Position = 1,
            ClassPosition = 1,
            CountLapFlag = 2,
            BestSector1 = rivalBestS1,
            BestLap = rivalBestLap,
            LastSector1 = rivalBestS1,
            LastSector2 = rivalBestS1 + 35.000,
            LastLap = rivalBestLap,
            // Simulate LMU storing individual S2 (35.0) in BestLapSector2 (> BestLapSector1 30.0)
            // which previously caused a bogus 5.0s S2 benchmark if subtracted!
            BestLapSector1 = rivalBestS1,
            BestLapSector2 = 35.000
        };
        f.Drivers.Add(rival);
        return f;
    }

    static void Version0130()
    {
        // 1. Native LMU mDeltaBest 1:1 mirror (no 60ms smoothing lag, outlap guard, fallback preserved)
        var repo = new MemoryDelta();
        var tracker = new DeltaTracker(repo);
        var outlap = DFrame(15, 0, 420, 0);
        outlap.Drivers[0].BestLap = 100.250;
        outlap.Drivers[0].CountLapFlag = 0;
        outlap.DeltaBest = 0.0;
        tracker.Apply(outlap);
        Assert(double.IsNaN(outlap.Delta.Values[0]), "unscored outlap flat zero does not display fake 0.000 delta");

        var hotlap = DFrame(125, 110, 850, 1);
        hotlap.Drivers[0].BestLap = 100.250;
        hotlap.Drivers[0].CountLapFlag = 2;
        hotlap.DeltaBest = -0.418;
        tracker.Apply(hotlap);
        Assert(hotlap.Delta.NativeMirror && Math.Abs(hotlap.Delta.Values[0] - (-0.418)) < 1e-9 && Math.Abs(hotlap.Delta.ReferenceTimes[0] - 100.250) < 1e-9,
            "session delta mirrors LMU native mDeltaBest 1:1");

        var motion = new DeltaMotion();
        var step1 = motion.Step(hotlap.Delta, 0, 10.0, true);
        hotlap.Delta.Values[0] = +0.274;
        var step2 = motion.Step(hotlap.Delta, 0, 10.016, true);
        Assert(Math.Abs(step1.Values[0] - (-0.418)) < 1e-9 && Math.Abs(step2.Values[0] - 0.274) < 1e-9,
            "native LMU delta mirror bypasses exponential lag filter so values match in-game HUD immediately");

        hotlap.DeltaBest = 0.0;
        tracker.Apply(hotlap);
        Assert(hotlap.Delta.NativeMirror && Math.Abs(hotlap.Delta.Values[0]) < 1e-9,
            "once native mDeltaBest is active in session, crossing exact 0.000 stays valid");

        // 2. Sector (S1 / S2), Track Limits Recalculation, and Finish-Line Rules
        var secTracker = new DeltaTracker(new MemoryDelta());
        var init = SectorTestFrame(200, 190, 2, 1, -1, -1, 91.500, 66.200, 91.500, 30.000, 90.000);
        init.Drivers[0].BestSector1 = 30.800;
        secTracker.Apply(init);
        Assert(init.Delta.Flash == null, "connecting mid-session does not emit a fake sector popup");

        // Simulate Track Limits warning mid-S1, then lifting off ("tirar o pé") so LMU clears LapInvalid!
        var tlWarn = SectorTestFrame(210.0, 190, 2, 1, -1, -1, 91.500, 66.200, 91.500, 30.000, 90.000);
        tlWarn.LapInvalid = true;
        secTracker.Apply(tlWarn);
        Assert(tlWarn.Delta.Flash != null && tlWarn.Delta.Flash.IsStatusAlert
            && tlWarn.Delta.Flash.Grade == DeltaFlashGrade.Yellow
            && tlWarn.Delta.Flash.StatusMessage == "VOLTA INVÁLIDA", "initial track-limits warning shows yellow VOLTA INVÁLIDA");

        var tlCleared = SectorTestFrame(211.5, 190, 2, 1, -1, -1, 91.500, 66.200, 91.500, 30.000, 90.000);
        tlCleared.LapInvalid = false;
        secTracker.Apply(tlCleared);
        Assert(tlCleared.Delta.Flash != null && tlCleared.Delta.Flash.IsStatusAlert
            && tlCleared.Delta.Flash.Grade == DeltaFlashGrade.Green
            && tlCleared.Delta.Flash.StatusMessage == "VOLTA VÁLIDA", "lifting off and clearing track limits immediately confirms green VOLTA VÁLIDA");

        // Player crosses S1 in 30.400 AFTER clearing track limits -> MUST still get GREEN (not latched Yellow!)
        var s1Green = SectorTestFrame(220.4, 190, 2, 2, 30.400, -1, 91.500, 66.200, 91.500, 30.000, 90.000);
        secTracker.Apply(s1Green);
        Assert(s1Green.Delta.Flash != null && s1Green.Delta.Flash.Label == "S1"
            && s1Green.Delta.Flash.Grade == DeltaFlashGrade.Green
            && Math.Abs(s1Green.Delta.Flash.Diff - (-0.400)) < 1e-6, "personal best sector after cleared track-limit shows GREEN with negative delta");

        // 4.5 seconds later, S1 popup expires automatically
        var afterS1 = SectorTestFrame(225.0, 190, 2, 2, 30.400, -1, 91.500, 66.200, 91.500, 30.000, 90.000);
        secTracker.Apply(afterS1);
        Assert(afterS1.Delta.Flash == null, "sector popup automatically disappears after 4 seconds");

        // Player crosses S2 with cumulative 65.200 (S2 = 34.800 < rival's 35.000 -> PURPLE, not poisoned by BestLapSector2!)
        var s2Purple = SectorTestFrame(255.2, 190, 2, 0, 30.400, 65.200, 91.500, 66.200, 91.500, 30.000, 90.000);
        secTracker.Apply(s2Purple);
        Assert(s2Purple.Delta.Flash != null && s2Purple.Delta.Flash.Label == "S2"
            && s2Purple.Delta.Flash.Grade == DeltaFlashGrade.Purple
            && Math.Abs(s2Purple.Delta.Flash.Time - 34.800) < 1e-6, "fastest class sector on track shows PURPLE");

        // Player crosses Finish Line with 89.850 (beats rival's 90.000 -> PURPLE finish flash!)
        var finishPurple = SectorTestFrame(279.85, 279.85, 3, 1, -1, -1, 89.850, 65.200, 89.850, 30.000, 90.000);
        secTracker.Apply(finishPurple);
        Assert(finishPurple.Delta.Flash != null && finishPurple.Delta.Flash.IsLap
            && finishPurple.Delta.Flash.Grade == DeltaFlashGrade.Purple
            && Math.Abs(finishPurple.Delta.Flash.Time - 89.850) < 1e-6, "fastest session lap at finish line shows PURPLE lap");

        // Next lap: cut final corner (invalidating current + next lap), cross finish line -> NO finish VOLTA flash, then Red PRÓX. INVÁLIDA!
        var lateCut = SectorTestFrame(365.0, 279.85, 3, 0, 30.9, 66.5, 89.850, 65.200, 89.850, 30.000, 90.000);
        lateCut.Drivers[0].LapDistance = 4600;
        lateCut.LapInvalid = true;
        lateCut.TrackLimitsSteps = 4; lateCut.TrackLimitsStepsPerPoint = 2; lateCut.TrackLimitsStepsPerPenalty = 6;
        secTracker.Apply(lateCut);
        Assert(lateCut.Delta.Flash != null && lateCut.Delta.Flash.IsStatusAlert && lateCut.Delta.Flash.Grade == DeltaFlashGrade.Red,
            "late-lap track cut near penalty threshold shows RED alert");

        var crossInvalid = SectorTestFrame(372.0, 372.0, 4, 1, -1, -1, 92.150, 66.5, 89.850, 30.000, 90.000);
        crossInvalid.LapInvalid = true;
        secTracker.Apply(crossInvalid);
        Assert(crossInvalid.Delta.Flash == null || !crossInvalid.Delta.Flash.IsLap,
            "crossing finish line on an invalidated lap never displays finish-line VOLTA summary");

        var nextLapInvalid = SectorTestFrame(372.8, 372.0, 4, 1, -1, -1, -1, 66.5, 89.850, 30.000, 90.000);
        nextLapInvalid.LapInvalid = true;
        nextLapInvalid.TrackLimitsSteps = 4; nextLapInvalid.TrackLimitsStepsPerPoint = 2; nextLapInvalid.TrackLimitsStepsPerPenalty = 6;
        secTracker.Apply(nextLapInvalid);
        Assert(nextLapInvalid.Delta.Flash != null && nextLapInvalid.Delta.Flash.IsStatusAlert
            && nextLapInvalid.Delta.Flash.StatusMessage == "PRÓX. INVÁLIDA"
            && nextLapInvalid.Delta.Flash.PointsText == ""
            && nextLapInvalid.Delta.Flash.Grade == DeltaFlashGrade.Red, "carrying invalidation into Practice/Qualy announces PRÓX. INVÁLIDA in Red with NO points");

        // 2b. Practice & Qualifying suppress points, Live "PROCESSANDO" (Green vs Yellow/Orange/Red), and Race exact 0.25-step Steward points
        var stewardTracker = new DeltaTracker(new MemoryDelta());
        var pracStart = SectorTestFrame(500.0, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        pracStart.Session = 5; // Qualifying
        pracStart.Phase = 5;
        pracStart.DeltaBest = -0.100;
        pracStart.Drivers[0].TrackEdge = 4.80;
        pracStart.Drivers[0].PathLateral = 2.10;
        stewardTracker.Apply(pracStart);

        // Case A: Driver runs wide and LOSES time (DeltaBest goes from -0.100 to +0.080) -> processes in GREEN ("PROCESSANDO • OK", NO lift-off!)
        var wideLostTime = SectorTestFrame(500.08, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        wideLostTime.Session = 5; wideLostTime.Phase = 5; wideLostTime.Throttle = 0.90; wideLostTime.DeltaBest = +0.080;
        wideLostTime.Drivers[0].TrackEdge = 4.80; wideLostTime.Drivers[0].PathLateral = 5.85;
        stewardTracker.Apply(wideLostTime);
        Assert(wideLostTime.Delta.Flash != null && wideLostTime.Delta.Flash.Grade == DeltaFlashGrade.Green
            && wideLostTime.Delta.Flash.Label == "ANÁLISE"
            && wideLostTime.Delta.Flash.StatusMessage == "PROCESSANDO • OK", "going wide while losing time processes in GREEN without asking to lift off");

        // Steward clears that wide excursion -> reset back on track
        var clearWide = SectorTestFrame(503.00, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        clearWide.Session = 5; clearWide.Phase = 5; clearWide.DeltaBest = +0.080;
        clearWide.Drivers[0].TrackEdge = 4.80; clearWide.Drivers[0].PathLateral = 2.00;
        clearWide.ScoringStream = "<TrackLimits Driver=\"Arthur\" ID=\"1\" Lap=\"1\" WarningPoints=\"0\" CurrentPoints=\"0\" Resolution=\"7\" et=\"503.0\">No Further Action</TrackLimits>";
        stewardTracker.Apply(clearWide);

        // Next clean point before a corner cut (DeltaBest = -0.100)
        var preCut = SectorTestFrame(505.00, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        preCut.Session = 5; preCut.Phase = 5; preCut.DeltaBest = -0.100;
        preCut.Drivers[0].TrackEdge = 4.80; preCut.Drivers[0].PathLateral = 2.20;
        stewardTracker.Apply(preCut);

        // Case B: Driver cuts corner GAINING 0.160s (DeltaBest goes from -0.100 to -0.260) on throttle -> ORANGE "PROCESSANDO • TIRE O PÉ"!
        var offGas = SectorTestFrame(505.08, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        offGas.Session = 5; offGas.Phase = 5; offGas.Throttle = 0.85; offGas.DeltaBest = -0.260;
        offGas.Drivers[0].TrackEdge = 4.80; offGas.Drivers[0].PathLateral = 5.95;
        stewardTracker.Apply(offGas);
        Assert(offGas.Delta.Flash != null && offGas.Delta.Flash.Grade == DeltaFlashGrade.Orange
            && offGas.Delta.Flash.Label == "ANÁLISE"
            && offGas.Delta.Flash.StatusMessage == "PROCESSANDO • TIRE O PÉ", "cutting track and gaining time triggers Orange PROCESSANDO • TIRE O PÉ");

        // Driver lifts off throttle (Throttle = 0), giving back time (still +0.110s gained -> Orange PROCESSANDO • ALIVIANDO)
        var lifted = SectorTestFrame(505.60, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        lifted.Session = 5; lifted.Phase = 5; lifted.Throttle = 0.0; lifted.DeltaBest = -0.210;
        lifted.Drivers[0].TrackEdge = 4.80; lifted.Drivers[0].PathLateral = 3.20;
        stewardTracker.Apply(lifted);
        Assert(lifted.Delta.Flash != null && lifted.Delta.Flash.Grade == DeltaFlashGrade.Orange
            && lifted.Delta.Flash.StatusMessage == "PROCESSANDO • ALIVIANDO", "lifting off throttle while still ahead shows PROCESSANDO • ALIVIANDO");

        // Driver finishes giving back the gained time (DeltaBest rises to -0.090 <= entry -0.100) -> drops live to GREEN PROCESSANDO • OK!
        var gaveBack = SectorTestFrame(506.20, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        gaveBack.Session = 5; gaveBack.Phase = 5; gaveBack.Throttle = 0.80; gaveBack.DeltaBest = -0.090;
        gaveBack.Drivers[0].TrackEdge = 4.80; gaveBack.Drivers[0].PathLateral = 2.50;
        stewardTracker.Apply(gaveBack);
        Assert(gaveBack.Delta.Flash != null && gaveBack.Delta.Flash.Grade == DeltaFlashGrade.Green
            && gaveBack.Delta.Flash.StatusMessage == "PROCESSANDO • OK", "once time gained is given back, banner turns GREEN PROCESSANDO • OK so driver can push again");

        // LMU Steward emits No Further Action (Resolution="7") in ScoringStream -> confirms GREEN VOLTA VÁLIDA!
        var nfa = SectorTestFrame(507.80, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        nfa.Session = 5; nfa.Phase = 5; nfa.DeltaBest = -0.090;
        nfa.Drivers[0].TrackEdge = 4.80; nfa.Drivers[0].PathLateral = 2.00;
        nfa.ScoringStream = "<TrackLimits Driver=\"Arthur\" ID=\"1\" Lap=\"1\" WarningPoints=\"0\" CurrentPoints=\"0\" Resolution=\"7\" et=\"507.8\">No Further Action</TrackLimits>";
        stewardTracker.Apply(nfa);
        Assert(nfa.Delta.Flash != null && nfa.Delta.Flash.Grade == DeltaFlashGrade.Green
            && nfa.Delta.Flash.Label == "OK"
            && nfa.Delta.Flash.StatusMessage == "VOLTA VÁLIDA"
            && nfa.Delta.Flash.PointsText == "", "Steward No Further Action immediately clears PROCESSANDO into green VOLTA VÁLIDA");

        // In Qualifying (Session=5), even if LMU increments TrackLimitsSteps and logs WarningPoints="0.25" on Resolution="5", NO points are shown!
        var qualCut = SectorTestFrame(512.0, 490.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        qualCut.Session = 5; qualCut.Phase = 5; qualCut.LapInvalid = true;
        qualCut.TrackLimitsSteps = 1; qualCut.TrackLimitsStepsPerPoint = 4; qualCut.TrackLimitsStepsPerPenalty = 12;
        qualCut.ScoringStream = "<TrackLimits Driver=\"Arthur\" ID=\"1\" Lap=\"1\" WarningPoints=\"0.25\" CurrentPoints=\"0.25\" Resolution=\"5\" et=\"512.0\">Invalid Lap Cut Track</TrackLimits>";
        stewardTracker.Apply(qualCut);
        Assert(qualCut.Delta.Flash != null && qualCut.Delta.Flash.StatusMessage == "VOLTA INVÁLIDA"
            && qualCut.Delta.Flash.PointsText == "", "Qualifying and Practice never display Track Limits penalty points");

        // In Race (Session=10), exact LMU points (0.25, 0.75, 1.0) are displayed faithfully without 1-decimal rounding!
        var raceTracker = new DeltaTracker(new MemoryDelta());
        var raceInit = SectorTestFrame(600.0, 590.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        raceInit.Session = 10; raceInit.Phase = 5;
        raceInit.TrackLimitsSteps = 1; raceInit.TrackLimitsStepsPerPoint = 4; raceInit.TrackLimitsStepsPerPenalty = 12;
        raceTracker.Apply(raceInit);

        var raceCut1 = SectorTestFrame(608.0, 590.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        raceCut1.Session = 10; raceCut1.Phase = 5; raceCut1.LapInvalid = true;
        raceCut1.TrackLimitsSteps = 5; raceCut1.TrackLimitsStepsPerPoint = 4; raceCut1.TrackLimitsStepsPerPenalty = 12;
        raceCut1.ScoringStream = "<TrackLimits Driver=\"Arthur\" ID=\"1\" Lap=\"1\" WarningPoints=\"1\" CurrentPoints=\"1.25\" Resolution=\"5\" et=\"608.0\">Invalid Lap Cut Track</TrackLimits>";
        raceTracker.Apply(raceCut1);
        Assert(raceCut1.Delta.Flash != null && raceCut1.Delta.Flash.PointsText == "+1 • 1,25/3"
            && raceCut1.Delta.Flash.Grade == DeltaFlashGrade.Orange, "Race session displays exact 1-point increment and 1,25/3 total from LMU Steward");

        var raceDt = SectorTestFrame(620.0, 590.0, 1, 1, -1, -1, -1, -1, 91.500, 30.000, 90.000);
        raceDt.Session = 10; raceDt.Phase = 5; raceDt.LapInvalid = true;
        raceDt.TrackLimitsSteps = 0; raceDt.TrackLimitsStepsPerPoint = 4; raceDt.TrackLimitsStepsPerPenalty = 12;
        raceDt.ScoringStream = "<TrackLimits Driver=\"Arthur\" ID=\"1\" Lap=\"1\" WarningPoints=\"1.75\" CurrentPoints=\"0\" Resolution=\"2\" et=\"620.0\">Drive Through Penalty</TrackLimits>";
        raceTracker.Apply(raceDt);
        Assert(raceDt.Delta.Flash != null && raceDt.Delta.Flash.Grade == DeltaFlashGrade.Red
            && raceDt.Delta.Flash.Label == "PUNIÇÃO"
            && raceDt.Delta.Flash.StatusMessage == "DRIVE THROUGH", "exceeding race track limits threshold announces DRIVE THROUGH penalty");

        // 3. All 8 steering wheel styles render distinctly and cleanly within circular bounds
        var signatures = new HashSet<string>();
        using (var painter = new Painter())
        {
            for (int style = 0; style < WheelStyle.Count; style++)
            {
                var bmp = painter.GetWheelBitmap(style);
                Assert(bmp.Width == 320 && bmp.Height == 320, "wheel style " + style + " bitmap size");
                long alphaSum = 0, rSum = 0, gSum = 0, bSum = 0;
                for (int y = 0; y < 320; y += 4)
                {
                    for (int x = 0; x < 320; x += 4)
                    {
                        Color px = bmp.GetPixel(x, y);
                        alphaSum += px.A; rSum += px.R; gSum += px.G; bSum += px.B;
                    }
                }
                Assert(alphaSum > 50000, "wheel style " + style + " renders visible artwork");
                Assert(bmp.GetPixel(2, 2).A == 0 && bmp.GetPixel(317, 317).A == 0, "wheel style " + style + " keeps corners transparent inside RPM ring");
                string sig = alphaSum + ":" + rSum + ":" + gSum + ":" + bSum;
                Assert(signatures.Add(sig), "wheel style " + style + " (" + WheelStyle.Names[style] + ") is visually distinct");
            }
        }
    }

    static void Render0130(string directory)
    {
        Directory.CreateDirectory(directory);
        int cols = 4, rows = 2, cardW = 280, cardH = 310, pad = 20;
        int width = pad + cols * (cardW + pad), height = 84 + rows * (cardH + pad);
        using (var painter = new Painter())
        {
            using (var sheet = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(sheet))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Theme.Background);

                using (var titleFont = new Font(Theme.DisplayFont, 24, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var subFont = new Font(Theme.BodyFont, 13, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var white = new SolidBrush(Theme.Text))
                using (var muted = new SolidBrush(Theme.Muted))
                {
                    g.DrawString("Arts LMU OVERLAY v0.13.4 — Catálogo de Volantes", titleFont, white, pad, 18);
                    g.DrawString("Escolha na aba Pilotagem > Visual do volante no overlay (100% vetorial, rotação real 1:1 preservada)", subFont, muted, pad, 50);

                    for (int i = 0; i < WheelStyle.Count; i++)
                    {
                        int col = i % cols, row = i / cols;
                        int x = pad + col * (cardW + pad), y = 84 + row * (cardH + pad);
                        using (var cardBg = new SolidBrush(Theme.Surface))
                        using (var border = new Pen(Theme.Grid, 1.5f))
                        {
                            g.FillRectangle(cardBg, x, y, cardW, cardH);
                            g.DrawRectangle(border, x, y, cardW, cardH);
                        }

                        int cx = x + cardW / 2, cy = y + 136;
                        using (var guide = new Pen(Color.FromArgb(135, Theme.Grid), 1.5f))
                            g.DrawArc(guide, cx - 96, cy - 96, 192, 192, 140, 260);
                        for (int seg = 0; seg < 28; seg++)
                        {
                            float start = 140 + seg * 260f / 28f;
                            Color c = seg < 17 ? Theme.Throttle : seg < 24 ? Theme.Fuel : Theme.Brake;
                            bool lit = seg < 23;
                            using (var arcPen = new Pen(lit ? c : Color.FromArgb(180, Theme.Grid), 7f))
                                g.DrawArc(arcPen, cx - 108, cy - 108, 216, 216, start, 260f / 28f - 2.2f);
                        }

                        painter.DrawWheelPreview(g, i, new Rectangle(cx - 104, cy - 104, 208, 208), 0);

                        using (var nameFont = new Font(Theme.BodyFont, 13.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (var idxFont = new Font(Theme.DataFont, 11.5f, FontStyle.Regular, GraphicsUnit.Pixel))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                        {
                            g.DrawString(WheelStyle.Names[i], nameFont, white, new RectangleF(x + 8, y + 258, cardW - 16, 24), sf);
                            g.DrawString("Opção " + (i + 1), idxFont, muted, new RectangleF(x + 8, y + 281, cardW - 16, 20), sf);
                        }
                    }
                }
                sheet.Save(Path.Combine(directory, "catalogo-volantes.png"), ImageFormat.Png);
            }

            // Render 6-panel Delta showcase: Sectors + Finish Line + Track Limits (Orange/Red/Green recalculated)
            using (var deltaSheet = new Bitmap(980, 585, PixelFormat.Format32bppArgb))
            using (var dg = Graphics.FromImage(deltaSheet))
            {
                dg.SmoothingMode = SmoothingMode.AntiAlias;
                dg.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                dg.Clear(Color.FromArgb(15, 22, 31));

                using (var titleFont = new Font(Theme.DisplayFont, 22, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var subFont = new Font(Theme.BodyFont, 12.5f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var white = new SolidBrush(Theme.Text))
                using (var muted = new SolidBrush(Theme.Muted))
                {
                    dg.DrawString("Delta Central v0.13.4 — Processando Inteligente (Verde vs Amarelo/Laranja/Vermelho)", titleFont, white, 20, 16);
                    dg.DrawString("Só pede TIRE O PÉ se ganhou tempo (Amarelo/Laranja/Vermelho)  •  Processa em Verde (OK) se não ganhou  •  Pontos 1:1 só na Corrida", subFont, muted, 20, 46);
                }

                var cases = new[]
                {
                    new { X = 20,  Y = 78,  Val = +0.110, Inv = false, Flash = new DeltaFlash { IsStatusAlert = true, Label = "ANÁLISE", StatusMessage = "PROCESSANDO • OK", PointsText = "", Grade = DeltaFlashGrade.Green, IssuedAt = 10, Duration = 4 } },
                    new { X = 500, Y = 78,  Val = -0.240, Inv = false, Flash = new DeltaFlash { IsStatusAlert = true, Label = "ANÁLISE", StatusMessage = "PROCESSANDO • TIRE O PÉ", PointsText = "", Grade = DeltaFlashGrade.Orange, IssuedAt = 10, Duration = 4 } },
                    new { X = 20,  Y = 246, Val = -0.220, Inv = true,  Flash = new DeltaFlash { IsStatusAlert = true, Label = "LIMITE", StatusMessage = "VOLTA INVÁLIDA", PointsText = "", Grade = DeltaFlashGrade.Yellow, IssuedAt = 10, Duration = 5.5 } },
                    new { X = 500, Y = 246, Val = -0.220, Inv = true,  Flash = new DeltaFlash { IsStatusAlert = true, Label = "LIMITE", StatusMessage = "VOLTA INVÁLIDA", PointsText = "+0,75 • 1,25/3", Grade = DeltaFlashGrade.Orange, IssuedAt = 10, Duration = 5.5 } },
                    new { X = 20,  Y = 414, Val = -0.412, Inv = false, Flash = new DeltaFlash { IsLap = true, Label = "VOLTA", Time = 102.318, Diff = -0.412, Grade = DeltaFlashGrade.Purple, IssuedAt = 10, Duration = 5 } },
                    new { X = 500, Y = 414, Val = +0.310, Inv = true,  Flash = new DeltaFlash { IsStatusAlert = true, Label = "PUNIÇÃO", StatusMessage = "DRIVE THROUGH", PointsText = "", Grade = DeltaFlashGrade.Red, IssuedAt = 10, Duration = 6.0 } }
                };

                var opts = new ViewOptions { DeltaOpacity = 35, DeltaThickness = 16, DeltaRange = 2 };
                foreach (var c in cases)
                {
                    var st = dg.Save();
                    dg.TranslateTransform(c.X, c.Y);
                    var frame = PreviewData.Create(10);
                    frame.Elapsed = 10.5;
                    frame.LapInvalid = c.Inv;
                    frame.Delta = new DeltaState { Values = new[] { c.Val, c.Val, c.Val }, Invalid = c.Inv, Clock = 10.5, Flash = c.Flash };
                    painter.DeltaBar(dg, frame, opts, false);
                    dg.Restore(st);
                }
                deltaSheet.Save(Path.Combine(directory, "previa-delta-setores.png"), ImageFormat.Png);
            }
        }
    }
}
