using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Arthur.LmuOverlay
{
    public static class DeltaMode
    {
        public static readonly string[] Names = { "Melhor da sessão (espelho LMU)", "Melhor pessoal", "Última volta válida" };
        public static int Normalize(int n) { return Math.Max(0, Math.Min(2, n)); }
    }

    public enum DeltaFlashGrade
    {
        None = 0,
        Yellow = 1, // Slower than personal best / initial track-limit lap invalidation
        Green = 2,  // Personal best in session / track-limit cleared (No Further Action or recalculated)
        Purple = 3, // Overall class best on track in session
        Orange = 4, // Race track-limit warning / infringement with points added
        Red = 5,    // Severe track-limit infringement, penalty applied, or current+next lap invalid
        Cyan = 6    // Live Steward track-limit investigation in progress ("PROCESSANDO")
    }

    public sealed class TrackLimitStewardEvent
    {
        public int Id = -1, Lap = -1, Resolution = -1;
        public string Driver = "", Message = "", Signature = "";
        public double WarningPoints, CurrentPoints, EventEt = -1;

        static string Attr(string tag, string name)
        {
            string key = name + "=\"";
            int i = tag.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            i += key.Length;
            int end = tag.IndexOf('"', i);
            return end > i ? tag.Substring(i, end - i) : "";
        }

        static double NumAttr(string tag, string name)
        {
            string raw = Attr(tag, name);
            double v;
            return raw.Length > 0 && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && Format.Finite(v) ? v : 0.0;
        }

        static int IntAttr(string tag, string name, int fallback)
        {
            string raw = Attr(tag, name);
            int v;
            return raw.Length > 0 && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        static string UnescapeXml(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('&') < 0) return s ?? "";
            return s.Replace("&apos;", "'").Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        }

        public static TrackLimitStewardEvent ParseLatestForPlayer(string stream, int playerId, string playerName)
        {
            if (string.IsNullOrEmpty(stream)) return null;
            TrackLimitStewardEvent matched = null;
            int pos = 0;
            while (pos < stream.Length)
            {
                int open = stream.IndexOf("<TrackLimits", pos, StringComparison.Ordinal);
                if (open < 0) break;
                int gt = stream.IndexOf('>', open + 12);
                if (gt < 0) break;
                int close = stream.IndexOf("</TrackLimits>", gt + 1, StringComparison.Ordinal);
                if (close < 0) break;

                string attrs = stream.Substring(open + 12, gt - (open + 12));
                int id = IntAttr(attrs, "ID", -999);
                string drv = UnescapeXml(Attr(attrs, "Driver"));
                bool isPlayer = (playerId >= 0 && id == playerId)
                    || (id == -999 && !string.IsNullOrEmpty(playerName) && string.Equals(drv, playerName, StringComparison.OrdinalIgnoreCase));
                if (isPlayer)
                {
                    matched = new TrackLimitStewardEvent
                    {
                        Id = id,
                        Driver = drv,
                        Lap = IntAttr(attrs, "Lap", -1),
                        WarningPoints = Math.Max(0, NumAttr(attrs, "WarningPoints")),
                        CurrentPoints = Math.Max(0, NumAttr(attrs, "CurrentPoints")),
                        Resolution = IntAttr(attrs, "Resolution", -1),
                        EventEt = NumAttr(attrs, "et"),
                        Message = UnescapeXml(stream.Substring(gt + 1, close - (gt + 1)).Trim()),
                        Signature = stream.Substring(open, close + 14 - open)
                    };
                }
                pos = close + 14;
            }
            return matched;
        }
    }

    public sealed class DeltaFlash
    {
        public string Label = "", StatusMessage = "", PointsText = "";
        public bool IsLap, IsStatusAlert;
        public double Time = -1, Diff = 0;
        public DeltaFlashGrade Grade;
        public double IssuedAt = -1, Duration = 4.0;

        public bool Active(double clock)
        {
            return Grade != DeltaFlashGrade.None && (IsStatusAlert || Time > 0) && Format.Finite(clock)
                && clock >= IssuedAt - 0.1 && clock - IssuedAt <= Duration;
        }

        public double OpacityFactor(double clock)
        {
            if (!Active(clock)) return 0;
            double remaining = Duration - Math.Max(0, clock - IssuedAt);
            return remaining < 0.6 ? Format.Clamp(remaining / 0.6, 0, 1) : 1.0;
        }

        public DeltaFlash Copy() { return (DeltaFlash)MemberwiseClone(); }
    }

    [DataContract]
    public sealed class DeltaProfile
    {
        [DataMember] public string Track = "", Car = "", Class = "", Player = "";
        [DataMember] public double Length;
        static string Part(string s) { s = (s ?? "").Trim().ToUpperInvariant(); return s.Length + ":" + s; }
        public string Key
        {
            get
            {
                using (var h = SHA256.Create())
                    return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes("arts-delta-1/" + Part(Track) + Part(Class) + Part(Car) + Part(Player) + Math.Round(Length).ToString(CultureInfo.InvariantCulture)))).Replace("-", "").ToLowerInvariant();
            }
        }
        public bool Valid
        {
            get { return !string.IsNullOrWhiteSpace(Track) && !string.IsNullOrWhiteSpace(Car) && !string.IsNullOrWhiteSpace(Class) && Format.Finite(Length) && Length >= 100 && Length <= 100000; }
        }
        public string Label { get { return Track + " • " + Class + " • " + Car; } }
        public static DeltaProfile From(Frame f, Driver d)
        {
            return new DeltaProfile
            {
                Track = f.Track,
                Car = string.IsNullOrWhiteSpace(d.CarName) ? (string.IsNullOrWhiteSpace(f.VehicleModel) ? d.VehicleKey : f.VehicleModel) : d.CarName,
                Class = d.Class,
                Player = d.Name,
                Length = f.TrackLength
            };
        }
    }

    [DataContract]
    public sealed class DeltaPoint { [DataMember] public double Distance, Time; }

    [DataContract]
    public sealed class DeltaLap
    {
        [DataMember] public int Version = 1;
        [DataMember] public DeltaProfile Profile;
        [DataMember] public double Time;
        [DataMember] public DeltaPoint[] Points;

        public bool IsValid()
        {
            if (Version != 1 || Profile == null || !Profile.Valid || !Format.Finite(Time) || Time <= 1 || Time > 7200 || Points == null || Points.Length < 3 || Points.Length > 20000) return false;
            double d = -1, t = -1;
            foreach (var p in Points)
            {
                if (p == null || !Format.Finite(p.Distance) || !Format.Finite(p.Time) || p.Distance <= d || p.Time <= t || p.Distance > Profile.Length || p.Time > Time) return false;
                d = p.Distance; t = p.Time;
            }
            return Points[0].Distance == 0 && Points[0].Time == 0 && Math.Abs(d - Profile.Length) < .001 && Math.Abs(t - Time) < .001;
        }

        public double At(double d)
        {
            if (!Format.Finite(d) || d < 0 || d > Profile.Length) return double.NaN;
            int lo = 0, hi = Points.Length - 1;
            while (hi - lo > 1)
            {
                int m = (lo + hi) / 2;
                if (Points[m].Distance <= d) lo = m; else hi = m;
            }
            var a = Points[lo];
            var b = Points[hi];
            return a.Time + (b.Time - a.Time) * (d - a.Distance) / (b.Distance - a.Distance);
        }

        public static DeltaLap From(LearningLap lap, DeltaProfile p)
        {
            if (lap == null || !lap.Info.IsPlayer || !lap.Info.Valid || !lap.Info.StartObserved || lap.Info.HasGaps) return null;
            var points = new List<DeltaPoint>();
            foreach (var s in lap.Samples)
            {
                if (s.BreakBefore) return null;
                if (points.Count > 0 && s.Distance <= points[points.Count - 1].Distance) continue;
                points.Add(new DeltaPoint { Distance = s.Distance, Time = s.Time });
            }
            if (points.Count > 0) points[points.Count - 1].Time = lap.Info.Time;
            var result = new DeltaLap { Profile = p, Time = lap.Info.Time, Points = points.ToArray() };
            return result.IsValid() ? result : null;
        }
    }

    public sealed class DeltaState
    {
        public string Key = "", Context = "", Reason = "Complete uma volta válida.", Storage = "";
        public bool Invalid, NativeMirror;
        public DeltaFlash Flash;
        public double Clock;
        public double[] Values = { double.NaN, double.NaN, double.NaN }, ReferenceTimes = { double.NaN, double.NaN, double.NaN };
        public DeltaState Copy()
        {
            var r = (DeltaState)MemberwiseClone();
            r.Values = (double[])Values.Clone();
            if (Flash != null) r.Flash = Flash.Copy();
            return r;
        }
    }

    public sealed class DeltaTracker
    {
        readonly IDeltaRepository repository;
        LearningRecorder recorder;
        DeltaProfile profile;
        DeltaLap best, personal, previous;
        string session = "", key = "";
        double lastTime = -1, lastStart = -1, lastDistance = double.NaN;
        bool broken, displayBroken, nativeSeen;
        int revision = -1;

        // Session sector & lap benchmark tracking + dynamic Track Limits & Steward state
        readonly double[] personalBestSector = { -1, -1, -1 };
        readonly double[] sessionBestSector = { -1, -1, -1 };
        double personalBestLap = -1, sessionBestLap = -1;
        int prevSector = -1, prevLaps = -1, prevTrackSteps = -1;
        double trackedLapStart = -1, prevCurS1 = -1, prevCurS2 = -1, prevEvalClock = -1;
        bool s1Announced, s2Announced, prevLapInvalid, nextLapCarryPending, nextLapCarryAnnounced;
        bool investigatingTrackLimits, offTrackLatch, offTrackIncidentResolved, invHadPriorInvalid, invHitSlowSurface;
        double offTrackHoldSeconds, investigationRejoinClock = -1, invStartedClock = -1, lastInvalidationFlashClock = -10;
        double lastCleanTrackDelta = double.NaN, lastCleanTrackSpeed = double.NaN;
        double invEntryDelta = double.NaN, invEntrySpeed = double.NaN, invMaxBeyondEdge, invOffThrottleSeconds;
        string lastProcessedStreamSignature = "";
        double lastProcessedStreamClock = -10;
        DeltaFlash activeFlash;

        public DeltaTracker(IDeltaRepository repo) { repository = repo; }
        void NewRecorder() { recorder = new LearningRecorder(s => { }, Complete); }

        public static double FlashClock(Frame f)
        {
            return Format.Finite(f.TelemetryElapsed) && f.TelemetryElapsed >= f.Elapsed ? f.TelemetryElapsed : f.Elapsed;
        }

        static double MinPos(double current, double candidate)
        {
            if (!Format.Finite(candidate) || candidate <= 0.5) return current;
            return current <= 0 || candidate < current ? candidate : current;
        }

        void ResetSessionBenchmarks()
        {
            for (int i = 0; i < 3; i++) personalBestSector[i] = sessionBestSector[i] = -1;
            personalBestLap = sessionBestLap = -1;
            prevSector = prevLaps = prevTrackSteps = -1;
            trackedLapStart = prevCurS1 = prevCurS2 = prevEvalClock = -1;
            s1Announced = s2Announced = prevLapInvalid = nextLapCarryPending = nextLapCarryAnnounced = false;
            investigatingTrackLimits = offTrackLatch = offTrackIncidentResolved = invHadPriorInvalid = invHitSlowSurface = false;
            offTrackHoldSeconds = invMaxBeyondEdge = invOffThrottleSeconds = 0;
            investigationRejoinClock = invStartedClock = -1;
            lastCleanTrackDelta = lastCleanTrackSpeed = invEntryDelta = invEntrySpeed = double.NaN;
            lastInvalidationFlashClock = -10;
            lastProcessedStreamSignature = "";
            lastProcessedStreamClock = -10;
            activeFlash = null;
        }

        void Complete(LearningLap lap)
        {
            var r = DeltaLap.From(lap, profile);
            if (r == null) return;
            previous = r;
            if (best == null || r.Time < best.Time) best = r;
            if (personal == null || r.Time < personal.Time) { personal = r; repository.Save(r); }
        }

        public void Retry()
        {
            var p = Interlocked.CompareExchange(ref personal, null, null);
            if (p != null) repository.Save(p);
            else
            {
                var context = Interlocked.CompareExchange(ref profile, null, null);
                if (context != null) repository.Load(context);
            }
        }

        void UpdateOpponentBenchmarks(Frame f, Driver player)
        {
            foreach (var c in f.Drivers)
            {
                if (c.IsPlayer || c.FinishStatus == 3 || c.Class != player.Class) continue;
                sessionBestLap = MinPos(sessionBestLap, c.BestLap);
                sessionBestSector[0] = MinPos(sessionBestSector[0], c.BestSector1);
                sessionBestSector[0] = MinPos(sessionBestSector[0], c.BestLapSector1);

                if (!c.InPits && !c.InGarage && c.CountLapFlag == 2)
                {
                    sessionBestSector[0] = MinPos(sessionBestSector[0], c.CurrentSector1);
                    if (c.CurrentSector2 > c.CurrentSector1 + 1.0 && c.CurrentSector1 > 0.5)
                        sessionBestSector[1] = MinPos(sessionBestSector[1], c.CurrentSector2 - c.CurrentSector1);
                }

                if (c.LastLap > 1.0 && c.LastSector1 > 0.5 && c.LastSector2 > c.LastSector1 + 1.0 && c.LastLap > c.LastSector2 + 1.0)
                {
                    sessionBestSector[0] = MinPos(sessionBestSector[0], c.LastSector1);
                    sessionBestSector[1] = MinPos(sessionBestSector[1], c.LastSector2 - c.LastSector1);
                    sessionBestSector[2] = MinPos(sessionBestSector[2], c.LastLap - c.LastSector2);
                }
            }
        }

        void SeedInitialPlayerBenchmarks(Driver d)
        {
            personalBestLap = MinPos(personalBestLap, d.BestLap);
            sessionBestLap = MinPos(sessionBestLap, personalBestLap);

            personalBestSector[0] = MinPos(personalBestSector[0], d.BestSector1);
            if (d.LastLap > 1.0 && d.LastSector1 > 0.5 && d.LastSector2 > d.LastSector1 + 1.0 && d.LastLap > d.LastSector2 + 1.0)
            {
                personalBestSector[0] = MinPos(personalBestSector[0], d.LastSector1);
                personalBestSector[1] = MinPos(personalBestSector[1], d.LastSector2 - d.LastSector1);
                personalBestSector[2] = MinPos(personalBestSector[2], d.LastLap - d.LastSector2);
            }

            for (int i = 0; i < 3; i++)
                sessionBestSector[i] = MinPos(sessionBestSector[i], personalBestSector[i]);
        }

        static DeltaFlashGrade EvaluateGrade(double time, bool valid, bool gameSaysPersonalBest, double personalRef, double sessionRef, out double diff)
        {
            if (personalRef > 0) diff = time - personalRef;
            else if (sessionRef > 0) diff = time - sessionRef;
            else diff = 0.0;

            if (!valid) return DeltaFlashGrade.Yellow;
            if (sessionRef <= 0 || time <= sessionRef + 0.0025)
            {
                if (diff > 0) diff = 0.0;
                return DeltaFlashGrade.Purple;
            }
            if (gameSaysPersonalBest || personalRef <= 0 || time <= personalRef + 0.0025)
            {
                if (personalRef <= 0 || diff > 0) diff = 0.0;
                return DeltaFlashGrade.Green;
            }
            return DeltaFlashGrade.Yellow;
        }

        // Practice, Qualifying, and Warmup NEVER display Track Limits penalty points in LMU (only lap invalidation).
        // In Race sessions, points are formatted with exact two-decimal precision ("0.##" -> 0,25 / 0,5 / 0,75 / 1 / 1,25)
        // matching LMU's Steward stream and 1/4-point step counter without rounding distortion.
        static string FormatRaceTrackLimitPoints(Frame f, double addedPts, double currentPts)
        {
            if (!Format.IsRace(f.Session)) return "";
            double maxPts = f.TrackLimitsStepsPerPenalty > 0 && f.TrackLimitsStepsPerPoint > 0
                ? f.TrackLimitsStepsPerPenalty / (double)f.TrackLimitsStepsPerPoint
                : 0.0;
            if (maxPts <= 0 && addedPts <= 0.001 && currentPts <= 0.001) return "";

            if (addedPts > 0.001)
            {
                string addStr = "+" + addedPts.ToString("0.##", Format.Br);
                if (maxPts > 0)
                    return addStr + " • " + currentPts.ToString("0.##", Format.Br) + "/" + maxPts.ToString("0.##", Format.Br);
                if (currentPts > 0.001)
                    return addStr + " (" + currentPts.ToString("0.##", Format.Br) + " PTS)";
                return addStr + " PTS";
            }
            if (currentPts > 0.001)
            {
                if (maxPts > 0)
                    return currentPts.ToString("0.##", Format.Br) + "/" + maxPts.ToString("0.##", Format.Br) + " PTS";
                return currentPts.ToString("0.##", Format.Br) + " PTS";
            }
            return "";
        }

        static string FormatRacePointsFromTelemetry(Frame f, int prevSteps)
        {
            if (!Format.IsRace(f.Session) || f.TrackLimitsStepsPerPoint <= 0) return "";
            double currentPts = f.TrackLimitsSteps > 0 ? f.TrackLimitsSteps / (double)f.TrackLimitsStepsPerPoint : 0.0;
            double addedPts = prevSteps >= 0 && f.TrackLimitsSteps > prevSteps
                ? (f.TrackLimitsSteps - prevSteps) / (double)f.TrackLimitsStepsPerPoint
                : 0.0;
            return FormatRaceTrackLimitPoints(f, addedPts, currentPts);
        }

        static DeltaFlashGrade SelectInvalidationSeverity(Frame f, bool pointsAddedInRace, bool affectsNextLap, double currentPtsOverride = -1)
        {
            if (affectsNextLap) return DeltaFlashGrade.Red;
            if (Format.IsRace(f.Session) && f.TrackLimitsStepsPerPoint > 0 && f.TrackLimitsStepsPerPenalty > 0)
            {
                double maxPts = f.TrackLimitsStepsPerPenalty / (double)f.TrackLimitsStepsPerPoint;
                double curPts = currentPtsOverride >= 0 ? currentPtsOverride : (f.TrackLimitsSteps / (double)f.TrackLimitsStepsPerPoint);
                if (maxPts > 0 && curPts / maxPts >= 0.66) return DeltaFlashGrade.Red;
            }
            if (Format.IsRace(f.Session) && pointsAddedInRace) return DeltaFlashGrade.Orange;
            return DeltaFlashGrade.Yellow;
        }

        static bool IsVehicleBeyondTrackLimits(Frame f, Driver d, bool alreadyBeyond, double liveLapTime)
        {
            if (!f.InRealtime || f.InPitLane || d.InPits || d.InGarage || d.PitState >= 2) return false;
            if (f.Phase != 0 && (f.Phase < 5 || f.Phase > 6)) return false;
            double speed = Format.Finite(f.Speed) && f.Speed > 0.1 ? f.Speed : d.Speed;
            if (!Format.Finite(speed) || speed < 25.0 || liveLapTime < 0.6) return false;

            if (Format.Finite(d.PathLateral) && Format.Finite(d.TrackEdge) && Math.Abs(d.TrackEdge) > 1.5 && d.PathLateral * d.TrackEdge > 0)
            {
                double beyondEdge = Math.Abs(d.PathLateral) - Math.Abs(d.TrackEdge);
                if (beyondEdge >= (alreadyBeyond ? 0.62 : 0.88))
                    return true;
            }

            if (f.Tyres != null && f.Tyres.Length == 4)
            {
                int offRoadWheels = 0;
                for (int i = 0; i < 4; i++)
                {
                    int surf = f.Tyres[i].SurfaceType;
                    if (surf == 2 || surf == 3 || surf == 4) offRoadWheels++;
                }
                if (offRoadWheels >= 3) return true;
            }

            return false;
        }

        TrackLimitStewardEvent ConsumeStewardEvent(Frame f, Driver d, double clock)
        {
            var ev = TrackLimitStewardEvent.ParseLatestForPlayer(f.ScoringStream, f.PlayerId, d.Name);
            if (ev == null) return null;
            if (ev.Signature == lastProcessedStreamSignature && clock - lastProcessedStreamClock < 0.65)
                return null;
            lastProcessedStreamSignature = ev.Signature;
            lastProcessedStreamClock = clock;
            return ev;
        }

        void ClearInvestigation(bool rawOffTrack)
        {
            investigatingTrackLimits = false;
            investigationRejoinClock = invStartedClock = -1;
            invMaxBeyondEdge = invOffThrottleSeconds = 0;
            invHitSlowSurface = false;
            offTrackIncidentResolved = rawOffTrack;
        }

        // Mirrors LMU's live Track Limits color calculation during the ~3-4s Steward processing window:
        // - GREEN ("PROCESSANDO • OK", never asks to lift off) when no time was gained (e.g. went wide & lost time, or already lifted off enough).
        // - YELLOW / ORANGE / RED ("PROCESSANDO • TIRE O PÉ" while on throttle, "PROCESSANDO • ALIVIANDO" while lifting) when time was gained or LMU tentatively flagged invalidation.
        DeltaFlashGrade EvaluateLiveProcessingGrade(Frame f, Driver d, double liveComparisonDelta, bool currentInvalid, out string statusMsg)
        {
            bool tentativeGameInvalid = currentInvalid && !invHadPriorInvalid;
            double curSpeed = Format.Finite(f.Speed) && f.Speed > 0.1 ? f.Speed : d.Speed;
            double timeGained;

            if (Format.Finite(invEntryDelta) && Format.Finite(liveComparisonDelta))
            {
                // Positive timeGained means current delta improved (dropped) vs before leaving the track.
                timeGained = invEntryDelta - liveComparisonDelta;
            }
            else
            {
                // First lap without a reference delta yet: use surface loss, speed loss, and lift-off duration.
                bool lostSpeedOrGravel = invHitSlowSurface
                    || (Format.Finite(invEntrySpeed) && Format.Finite(curSpeed) && curSpeed <= invEntrySpeed - 4.5)
                    || invOffThrottleSeconds >= 0.45;
                if (lostSpeedOrGravel)
                    timeGained = -0.10;
                else if (f.Throttle > 0.55 && (!Format.Finite(invEntrySpeed) || !Format.Finite(curSpeed) || curSpeed >= invEntrySpeed - 2.5))
                    timeGained = invMaxBeyondEdge >= 1.9 ? 0.14 : 0.05;
                else
                    timeGained = -0.05;
            }

            if (!tentativeGameInvalid && timeGained <= 0.015)
            {
                statusMsg = "PROCESSANDO • OK";
                return DeltaFlashGrade.Green;
            }

            bool lateOrNearPenalty = (tentativeGameInvalid && f.TrackLength > 100 && d.LapDistance >= f.TrackLength * 0.87)
                || (Format.IsRace(f.Session) && f.TrackLimitsStepsPerPenalty > 0 && (f.TrackLimitsSteps + 2) / (double)f.TrackLimitsStepsPerPenalty >= 0.75);

            DeltaFlashGrade grade;
            if (timeGained >= 0.24 || lateOrNearPenalty)
                grade = DeltaFlashGrade.Red;
            else if (timeGained >= 0.10)
                grade = DeltaFlashGrade.Orange;
            else
                grade = DeltaFlashGrade.Yellow;

            statusMsg = f.Throttle > 0.18
                ? (grade == DeltaFlashGrade.Red ? "PROCESSANDO • TIRE O PÉ!" : "PROCESSANDO • TIRE O PÉ")
                : "PROCESSANDO • ALIVIANDO";
            return grade;
        }

        void EvaluateSectorAndLapTransitions(Frame f, Driver d, double clock, double liveLapTime, double liveComparisonDelta)
        {
            UpdateOpponentBenchmarks(f, d);

            bool currentInvalid = f.LapInvalid || d.CountLapFlag == 1;
            bool unscored = d.CountLapFlag == 0 && liveLapTime > 1.2;
            double curSpeed = Format.Finite(f.Speed) && f.Speed > 0.1 ? f.Speed : d.Speed;

            if (prevLaps < 0)
            {
                SeedInitialPlayerBenchmarks(d);
                prevSector = d.Sector;
                prevLaps = d.Laps;
                trackedLapStart = d.LapStart;
                prevCurS1 = d.CurrentSector1;
                prevCurS2 = d.CurrentSector2;
                prevLapInvalid = currentInvalid;
                prevTrackSteps = f.TrackLimitsSteps;
                prevEvalClock = clock;
                lastCleanTrackDelta = liveComparisonDelta;
                lastCleanTrackSpeed = curSpeed;
                s1Announced = d.Sector != 1 && d.CurrentSector1 > 0.5;
                s2Announced = d.Sector == 0 && d.CurrentSector2 > 0.5;
                ConsumeStewardEvent(f, d, clock);
                return;
            }

            if (f.TrainingReset)
            {
                prevSector = d.Sector;
                prevLaps = d.Laps;
                trackedLapStart = d.LapStart;
                prevCurS1 = d.CurrentSector1;
                prevCurS2 = d.CurrentSector2;
                prevLapInvalid = currentInvalid;
                prevTrackSteps = f.TrackLimitsSteps;
                prevEvalClock = clock;
                s1Announced = s2Announced = nextLapCarryPending = nextLapCarryAnnounced = false;
                investigatingTrackLimits = offTrackLatch = offTrackIncidentResolved = invHadPriorInvalid = invHitSlowSurface = false;
                offTrackHoldSeconds = invMaxBeyondEdge = invOffThrottleSeconds = 0;
                investigationRejoinClock = invStartedClock = -1;
                lastCleanTrackDelta = lastCleanTrackSpeed = invEntryDelta = invEntrySpeed = double.NaN;
                activeFlash = null;
                return;
            }

            double dt = prevEvalClock >= 0 && clock > prevEvalClock ? Math.Min(0.25, clock - prevEvalClock) : 0.016;
            prevEvalClock = clock;

            bool crossedLine = (prevSector == 0 && d.Sector == 1) || d.Laps == prevLaps + 1 || (trackedLapStart >= 0 && d.LapStart > trackedLapStart + 1.0);

            if (crossedLine)
            {
                // Finish line: only display VOLTA summary if the finished lap was VALID!
                bool finishedLapWasInvalid = prevLapInvalid || d.LastLap <= 1.0;
                if (!finishedLapWasInvalid)
                {
                    double lapTime = d.LastLap;
                    bool gameConfirmedBest = d.BestLap > 0 && Math.Abs(lapTime - d.BestLap) <= 0.003;
                    double lapDiff;
                    var lapGrade = EvaluateGrade(lapTime, true, gameConfirmedBest, personalBestLap, sessionBestLap, out lapDiff);

                    double cumS2 = d.LastSector2 > 0.5 ? d.LastSector2 : prevCurS2;
                    if (cumS2 > 0.5 && lapTime > cumS2 + 1.0)
                    {
                        double s3 = lapTime - cumS2;
                        personalBestSector[2] = MinPos(personalBestSector[2], s3);
                        sessionBestSector[2] = MinPos(sessionBestSector[2], s3);
                    }
                    personalBestLap = MinPos(personalBestLap, lapTime);
                    sessionBestLap = MinPos(sessionBestLap, lapTime);
                    if (d.BestLap > 0)
                    {
                        personalBestLap = MinPos(personalBestLap, d.BestLap);
                        sessionBestLap = MinPos(sessionBestLap, d.BestLap);
                    }

                    activeFlash = new DeltaFlash
                    {
                        IsLap = true,
                        Label = "VOLTA",
                        Time = lapTime,
                        Diff = lapDiff,
                        Grade = lapGrade,
                        IssuedAt = clock,
                        Duration = 5.0
                    };
                }

                s1Announced = false;
                s2Announced = false;
                trackedLapStart = d.LapStart;
                nextLapCarryPending = prevLapInvalid;
                nextLapCarryAnnounced = false;
                lastCleanTrackDelta = double.NaN;
            }
            else
            {
                // Sector 1 finalized (never missed even if CurrentSector1 updates 1 tick after mSector)
                if (!s1Announced && d.Sector != 1 && d.CurrentSector1 > 0.5)
                {
                    s1Announced = true;
                    double s1 = d.CurrentSector1;
                    bool clean = !currentInvalid && !unscored;
                    bool gameBestS1 = clean && d.BestSector1 > 0 && Math.Abs(s1 - d.BestSector1) <= 0.003;
                    double diff;
                    var grade = EvaluateGrade(s1, clean, gameBestS1, personalBestSector[0], sessionBestSector[0], out diff);
                    if (clean)
                    {
                        personalBestSector[0] = MinPos(personalBestSector[0], s1);
                        sessionBestSector[0] = MinPos(sessionBestSector[0], s1);
                    }
                    if (!investigatingTrackLimits)
                        activeFlash = new DeltaFlash { Label = "S1", Time = s1, Diff = diff, Grade = grade, IssuedAt = clock, Duration = 4.0 };
                }
                // Sector 2 finalized
                else if (!s2Announced && d.Sector == 0 && d.CurrentSector2 > 0.5)
                {
                    double baseS1 = d.CurrentSector1 > 0.5 ? d.CurrentSector1 : prevCurS1;
                    if (baseS1 > 0.5 && d.CurrentSector2 > baseS1 + 1.0)
                    {
                        s2Announced = true;
                        double s2 = d.CurrentSector2 - baseS1;
                        bool clean = !currentInvalid && !unscored;
                        double diff;
                        var grade = EvaluateGrade(s2, clean, false, personalBestSector[1], sessionBestSector[1], out diff);
                        if (clean)
                        {
                            personalBestSector[1] = MinPos(personalBestSector[1], s2);
                            sessionBestSector[1] = MinPos(sessionBestSector[1], s2);
                        }
                        if (!investigatingTrackLimits)
                            activeFlash = new DeltaFlash { Label = "S2", Time = s2, Diff = diff, Grade = grade, IssuedAt = clock, Duration = 4.0 };
                    }
                }
            }

            // 1. Live Off-Track Detection ("PROCESSANDO" colored Green vs Yellow/Orange/Red)
            bool rawOffTrack = IsVehicleBeyondTrackLimits(f, d, offTrackLatch, liveLapTime);
            offTrackLatch = rawOffTrack;
            if (!rawOffTrack && !investigatingTrackLimits)
            {
                lastCleanTrackDelta = liveComparisonDelta;
                lastCleanTrackSpeed = curSpeed;
            }

            if (rawOffTrack)
            {
                offTrackHoldSeconds += dt;
                if (offTrackHoldSeconds >= 0.05 && !offTrackIncidentResolved && !unscored)
                {
                    if (!investigatingTrackLimits)
                    {
                        investigatingTrackLimits = true;
                        invStartedClock = clock;
                        invEntryDelta = lastCleanTrackDelta;
                        invEntrySpeed = lastCleanTrackSpeed;
                        invMaxBeyondEdge = 0;
                        invHitSlowSurface = false;
                        invOffThrottleSeconds = 0;
                        invHadPriorInvalid = prevLapInvalid;
                    }
                    investigationRejoinClock = -1;
                }
            }
            else
            {
                offTrackHoldSeconds = 0;
                offTrackIncidentResolved = false;
                if (investigatingTrackLimits && investigationRejoinClock < 0)
                    investigationRejoinClock = clock;
            }

            if (rawOffTrack || investigatingTrackLimits)
            {
                if (Format.Finite(d.PathLateral) && Format.Finite(d.TrackEdge) && Math.Abs(d.TrackEdge) > 1.5 && d.PathLateral * d.TrackEdge > 0)
                {
                    double beyond = Math.Abs(d.PathLateral) - Math.Abs(d.TrackEdge);
                    if (beyond > invMaxBeyondEdge) invMaxBeyondEdge = beyond;
                }
                if (f.Tyres != null && f.Tyres.Length == 4)
                {
                    int slowWheels = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        int surf = f.Tyres[i].SurfaceType;
                        if (surf == 2 || surf == 3 || surf == 4) slowWheels++;
                    }
                    if (slowWheels >= 2) invHitSlowSurface = true;
                }
                if (f.Throttle <= 0.18) invOffThrottleSeconds += dt;
            }

            // 2. Official Steward Verdict Stream (<TrackLimits ...> in scoringStream)
            var stewardEv = ConsumeStewardEvent(f, d, clock);
            bool stepsIncreased = prevTrackSteps >= 0 && f.TrackLimitsSteps > prevTrackSteps;
            bool handledVerdictThisTick = false;

            if (stewardEv != null && !unscored)
            {
                ClearInvestigation(rawOffTrack);
                handledVerdictThisTick = true;

                if (stewardEv.Resolution >= 1 && stewardEv.Resolution <= 3)
                {
                    string penMsg = stewardEv.Resolution == 1 ? "STOP & GO"
                        : stewardEv.Resolution == 2 ? "DRIVE THROUGH"
                        : "PUNIÇÃO TEMPO";
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "PUNIÇÃO",
                        StatusMessage = penMsg,
                        PointsText = "",
                        Grade = DeltaFlashGrade.Red,
                        IssuedAt = clock,
                        Duration = 6.0
                    };
                }
                else if (stewardEv.Resolution == 7)
                {
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "OK",
                        StatusMessage = currentInvalid ? "SEM PUNIÇÃO" : "VOLTA VÁLIDA",
                        PointsText = "",
                        Grade = DeltaFlashGrade.Green,
                        IssuedAt = clock,
                        Duration = 2.8
                    };
                }
                else if (stewardEv.Resolution == 4)
                {
                    string ptsText = FormatRaceTrackLimitPoints(f, stewardEv.WarningPoints, stewardEv.CurrentPoints);
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "LIMITE",
                        StatusMessage = currentInvalid ? "VOLTA INVÁLIDA" : "AVISO LIMITE",
                        PointsText = ptsText,
                        Grade = SelectInvalidationSeverity(f, stewardEv.WarningPoints > 0, false, stewardEv.CurrentPoints),
                        IssuedAt = clock,
                        Duration = 5.2
                    };
                    lastInvalidationFlashClock = clock;
                }
                else if (stewardEv.Resolution == 5 || stewardEv.Resolution == 6)
                {
                    bool lateCorner = f.TrackLength > 100 && d.LapDistance >= f.TrackLength * 0.87;
                    string ptsText = FormatRaceTrackLimitPoints(f, stewardEv.WarningPoints, stewardEv.CurrentPoints);
                    var sev = SelectInvalidationSeverity(f, stewardEv.WarningPoints > 0, lateCorner, stewardEv.CurrentPoints);
                    double issued = (activeFlash != null && activeFlash.IsStatusAlert && activeFlash.Label == "LIMITE" && clock - lastInvalidationFlashClock <= 0.8)
                        ? activeFlash.IssuedAt
                        : clock;
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "LIMITE",
                        StatusMessage = lateCorner ? "ATUAL + PRÓX.?" : "VOLTA INVÁLIDA",
                        PointsText = ptsText,
                        Grade = sev,
                        IssuedAt = issued,
                        Duration = 5.5
                    };
                    lastInvalidationFlashClock = clock;
                }
            }

            // 3. High-frequency (100 Hz) Telemetry Transitions (LapInvalid / Steps / Next-lap carry / Recalculation)
            if (!handledVerdictThisTick)
            {
                bool allowLiveRecalcWindow = investigatingTrackLimits && invStartedClock >= 0
                    && clock - invStartedClock < 3.2 && !stepsIncreased;

                if (nextLapCarryPending)
                {
                    if (!currentInvalid)
                    {
                        nextLapCarryPending = false;
                    }
                    else if (!nextLapCarryAnnounced && liveLapTime >= 0.45 && liveLapTime <= 4.5)
                    {
                        nextLapCarryAnnounced = true;
                        nextLapCarryPending = false;
                        ClearInvestigation(rawOffTrack);
                        activeFlash = new DeltaFlash
                        {
                            IsStatusAlert = true,
                            Label = "LIMITE",
                            StatusMessage = "PRÓX. INVÁLIDA",
                            PointsText = FormatRacePointsFromTelemetry(f, -1),
                            Grade = DeltaFlashGrade.Red,
                            IssuedAt = clock,
                            Duration = 5.5
                        };
                    }
                }
                else if (!prevLapInvalid && currentInvalid && !unscored && !allowLiveRecalcWindow)
                {
                    ClearInvestigation(rawOffTrack);
                    bool lateCorner = f.TrackLength > 100 && d.LapDistance >= f.TrackLength * 0.87;
                    var sev = SelectInvalidationSeverity(f, stepsIncreased, lateCorner);
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "LIMITE",
                        StatusMessage = lateCorner ? "ATUAL + PRÓX.?" : "VOLTA INVÁLIDA",
                        PointsText = FormatRacePointsFromTelemetry(f, prevTrackSteps),
                        Grade = sev,
                        IssuedAt = clock,
                        Duration = 5.5
                    };
                    lastInvalidationFlashClock = clock;
                }
                else if (Format.IsRace(f.Session) && stepsIncreased && !unscored)
                {
                    ClearInvestigation(rawOffTrack);
                    bool lateCorner = currentInvalid && f.TrackLength > 100 && d.LapDistance >= f.TrackLength * 0.87;
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "LIMITE",
                        StatusMessage = lateCorner ? "ATUAL + PRÓX." : (currentInvalid ? "VOLTA INVÁLIDA" : "AVISO LIMITE"),
                        PointsText = FormatRacePointsFromTelemetry(f, prevTrackSteps),
                        Grade = SelectInvalidationSeverity(f, true, lateCorner),
                        IssuedAt = clock,
                        Duration = 5.5
                    };
                    lastInvalidationFlashClock = clock;
                }
                else if (prevLapInvalid && !currentInvalid && !crossedLine && !unscored)
                {
                    ClearInvestigation(rawOffTrack);
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "OK",
                        StatusMessage = "VOLTA VÁLIDA",
                        PointsText = "",
                        Grade = DeltaFlashGrade.Green,
                        IssuedAt = clock,
                        Duration = 2.8
                    };
                }
            }

            // 4. Active "PROCESSANDO" display:
            //    - GREEN ("PROCESSANDO • OK", no lift-off needed) when no time was gained;
            //    - YELLOW / ORANGE / RED ("PROCESSANDO • TIRE O PÉ") ONLY when gaining time or tentatively flagged!
            if (investigatingTrackLimits)
            {
                if (currentInvalid && !invHadPriorInvalid && invStartedClock >= 0 && clock - invStartedClock >= 3.2)
                {
                    ClearInvestigation(rawOffTrack);
                    bool lateCorner = f.TrackLength > 100 && d.LapDistance >= f.TrackLength * 0.87;
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "LIMITE",
                        StatusMessage = lateCorner ? "ATUAL + PRÓX.?" : "VOLTA INVÁLIDA",
                        PointsText = FormatRacePointsFromTelemetry(f, -1),
                        Grade = SelectInvalidationSeverity(f, false, lateCorner),
                        IssuedAt = clock,
                        Duration = 5.0
                    };
                    lastInvalidationFlashClock = clock;
                }
                else if (investigationRejoinClock >= 0 && clock - investigationRejoinClock >= 4.2)
                {
                    ClearInvestigation(false);
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "OK",
                        StatusMessage = currentInvalid ? "SEM PUNIÇÃO" : "VOLTA VÁLIDA",
                        PointsText = "",
                        Grade = DeltaFlashGrade.Green,
                        IssuedAt = clock,
                        Duration = 2.2
                    };
                }
                else
                {
                    string procMsg;
                    var procGrade = EvaluateLiveProcessingGrade(f, d, liveComparisonDelta, currentInvalid, out procMsg);
                    activeFlash = new DeltaFlash
                    {
                        IsStatusAlert = true,
                        Label = "ANÁLISE",
                        StatusMessage = procMsg,
                        PointsText = "",
                        Grade = procGrade,
                        IssuedAt = clock,
                        Duration = 1.0
                    };
                }
            }

            prevSector = d.Sector;
            prevLaps = d.Laps;
            prevLapInvalid = currentInvalid;
            prevTrackSteps = f.TrackLimitsSteps;
            if (d.CurrentSector1 > 0.5) prevCurS1 = d.CurrentSector1;
            if (d.CurrentSector2 > 0.5) prevCurS2 = d.CurrentSector2;

            if (activeFlash != null && !activeFlash.Active(clock))
                activeFlash = null;
        }

        public void Apply(Frame f)
        {
            var d = f.Drivers.FirstOrDefault(x => x.IsPlayer);
            if (!f.Connected || !f.HasTelemetry || f.Stale || f.ScoringStale || !f.InRealtime || f.Phase == 9 || f.Demo || d == null || d.InGarage || d.InPits)
            {
                if (recorder != null) recorder.Break();
                prevSector = d == null ? -1 : d.Sector;
                prevLaps = d == null ? -1 : d.Laps;
                prevLapInvalid = false;
                nextLapCarryPending = nextLapCarryAnnounced = false;
                investigatingTrackLimits = offTrackLatch = offTrackIncidentResolved = invHadPriorInvalid = invHitSlowSurface = false;
                offTrackHoldSeconds = invMaxBeyondEdge = invOffThrottleSeconds = 0;
                investigationRejoinClock = invStartedClock = -1;
                lastCleanTrackDelta = lastCleanTrackSpeed = double.NaN;
                activeFlash = null;
                f.Delta = new DeltaState { Reason = "Aguardando pilotagem.", Context = profile == null ? "" : profile.Label, Storage = repository.Latest.Status };
                return;
            }
            var incoming = DeltaProfile.From(f, d);
            if (!incoming.Valid || !Format.Finite(f.Elapsed) || !Format.Finite(d.LapStart) || d.LapStart < 0 || d.LapStart > f.Elapsed || !Format.Finite(d.LapDistance) || d.LapDistance < 0 || d.LapDistance > f.TrackLength)
            {
                if (recorder != null) recorder.Break();
                f.Delta = new DeltaState { Reason = "Aguardando posição na pista." };
                return;
            }
            string next = incoming.Key;
            if (key != next || session != f.SessionKey)
            {
                bool change = key != next;
                key = next; profile = incoming; session = f.SessionKey;
                best = previous = null; lastTime = lastStart = -1; lastDistance = double.NaN;
                broken = displayBroken = nativeSeen = false;
                ResetSessionBenchmarks();
                NewRecorder();
                if (change) { personal = null; revision = -1; repository.Load(profile); }
            }
            var stored = repository.Latest;
            if (stored.Key == key && stored.Revision != revision)
            {
                revision = stored.Revision;
                if (stored.Lap != null && (personal == null || stored.Lap.Time < personal.Time)) personal = stored.Lap;
            }
            if (lastTime >= 0 && f.Elapsed < lastTime - .01) { NewRecorder(); lastTime = -1; broken = displayBroken = true; lastStart = d.LapStart; ResetSessionBenchmarks(); }
            if (lastTime >= 0 && Math.Abs(lastStart - d.LapStart) > .01) broken = displayBroken = false;
            if (lastTime >= 0 && f.Elapsed > lastTime && Math.Abs(lastStart - d.LapStart) <= .01)
            {
                double dt = f.Elapsed - lastTime, dd = d.LapDistance - lastDistance;
                if (dt > 1 || dd < -2 || dd > 40 + 150 * dt) broken = true;
                if (dd > 40 + 150 * dt || dd < -Math.Max(40, 150 * dt)) displayBroken = true;
            }
            if (f.TrainingReset) { broken = displayBroken = true; recorder.Break(); }
            if (f.Elapsed > lastTime)
            {
                var capture = LearningFrame.Capture(f);
                capture.Cars = capture.Cars.Where(x => x.IsPlayer).ToArray();
                foreach (var car in capture.Cars)
                {
                    if (Math.Abs(lastStart - d.LapStart) <= .01 && Format.Finite(lastDistance) && car.Distance < lastDistance && lastDistance - car.Distance <= 2) car.Distance = lastDistance;
                    if (car.CountLap == 0 && f.Elapsed - car.Start <= 1 && !car.Invalid) car.CountLap = 2;
                }
                capture.Dropped |= broken;
                recorder.Accept(capture);
                lastTime = f.Elapsed; lastStart = d.LapStart;
                lastDistance = capture.Cars.Length > 0 ? capture.Cars[0].Distance : d.LapDistance;
            }

            double liveLapTime = f.Elapsed - d.LapStart;
            double liveDistance = d.LapDistance;
            if (Format.Finite(f.TelemetryElapsed) && f.TelemetryElapsed >= f.Elapsed && f.TelemetryElapsed - f.Elapsed <= 0.35)
            {
                double lead = f.TelemetryElapsed - f.Elapsed;
                if (Format.Finite(f.CurrentLap) && f.CurrentLap >= 0 && Math.Abs((f.CurrentLap - lead) - liveLapTime) <= 0.5)
                    liveLapTime = f.CurrentLap;
                else
                    liveLapTime += lead;
                if (Format.Finite(f.Speed) && f.Speed > 0)
                    liveDistance = Format.Clamp(d.LapDistance + lead * (f.Speed / 3.6), 0, f.TrackLength);
            }

            bool nativeValid = Format.Finite(f.DeltaBest) && Math.Abs(f.DeltaBest) < 999;
            if (nativeValid && Math.Abs(f.DeltaBest) > 1e-6) nativeSeen = true;
            bool unscoredOutlap = d.CountLapFlag == 0 && liveLapTime > 1.0;

            var refs = new[] { best, personal, previous };
            double liveComparisonDelta = double.NaN;
            if (nativeValid && d.BestLap > 0 && (nativeSeen || best == null) && !unscoredOutlap && !displayBroken)
            {
                liveComparisonDelta = f.DeltaBest;
            }
            else if (!displayBroken)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (refs[i] != null)
                    {
                        liveComparisonDelta = liveLapTime - refs[i].At(liveDistance);
                        break;
                    }
                }
            }

            double clock = FlashClock(f);
            EvaluateSectorAndLapTransitions(f, d, clock, liveLapTime, liveComparisonDelta);
            var state = new DeltaState
            {
                Key = session + "/" + key + "/" + d.LapStart.ToString(CultureInfo.InvariantCulture),
                Context = profile.Label,
                Invalid = f.LapInvalid || d.CountLapFlag == 1,
                Flash = activeFlash == null ? null : activeFlash.Copy(),
                Clock = clock,
                Reason = displayBroken ? "Trecho interrompido. Inicie outra volta." : unscoredOutlap ? "Volta de saída. Cruze a linha para comparar." : "Complete uma volta válida.",
                Storage = stored.Key == key ? stored.Status : "Carregando melhor pessoal…"
            };

            for (int i = 0; i < 3; i++)
            {
                if (refs[i] != null)
                {
                    state.ReferenceTimes[i] = refs[i].Time;
                    if (!displayBroken) state.Values[i] = liveLapTime - refs[i].At(liveDistance);
                }
            }

            if (nativeValid && d.BestLap > 0 && (nativeSeen || best == null) && !unscoredOutlap)
            {
                state.ReferenceTimes[0] = d.BestLap;
                state.NativeMirror = true;
                if (!displayBroken) state.Values[0] = f.DeltaBest;
            }

            f.Delta = state;
        }
    }

    public sealed class DeltaMotion
    {
        double value = double.NaN, last = -1;
        string key = "";
        int mode = -1;

        public DeltaState Step(DeltaState state, int selection, double now, bool smooth)
        {
            selection = DeltaMode.Normalize(selection);
            var r = state.Copy();
            double target = state.Values[selection];
            bool bypassSmooth = selection == 0 && state.NativeMirror;
            if (bypassSmooth || !smooth || !Format.Finite(target) || !Format.Finite(value) || mode != selection || key != state.Key || last< 0 || now < last || now - last > 1)
                value = target;
            else
                value += (target - value) * (1 - Math.Exp(-(now - last) / .06));
            r.Values[selection] = value;
            last = now; mode = selection; key = state.Key;
            return r;
        }
    }
}
