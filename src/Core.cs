using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;

namespace Arthur.LmuOverlay
{
    public sealed class Driver
    {
        public int Id, Position, ClassPosition, Laps, LapsBehind, PitStops, FinishStatus, Penalties;
        public string PenaltyTypes="";
        public string CarName="",TyreFront="",TyreRear="",TyreSource="";
        public string Name = "", Class = "", VehicleKey = "", Country = "";
        public int? StartClassPosition;
        public int? PositionChange { get { return StartClassPosition.HasValue && ClassPosition > 0 ? StartClassPosition.Value-ClassPosition : (int?)null; } }
        public bool IsPlayer, InPits, InGarage;
        public int Control;
        public int Sector,CountLapFlag=-1;
        public double LapStart=-1,CurrentSector1=-1,CurrentSector2=-1,LastSector1=-1,LastSector2=-1,Speed=double.NaN;
        public double LastLap = -1, BestLap = -1, Gap = -1;
        public double LapDistance=double.NaN,WorldX=double.NaN,WorldZ=double.NaN;
    }

    public sealed class TyreReading
    {
        public double Temperature=double.NaN,Pressure=double.NaN,Optimal=double.NaN,Remaining=double.NaN;
        public bool Flat,Detached;
    }

    public sealed class Frame
    {
        public DeltaState Delta=new DeltaState();
        public bool TrainingReset;
        public bool Connected, HasTelemetry, Demo, Stale, ScoringStale, InRealtime;
        public string Status = "Aguardando o LMU", Track = "", PlayerClass = "", Server = "", VehicleModel = "";
        public int Session, Phase, Gear, MaxGears, GameVersion, PlayerId=-1;
        public uint TelemetryEvent, ScoringEvent, SessionStarted;
        public ulong GameWindow;
        public double Steering = double.NaN, SteeringRange, VisualSteeringRange, GarageSteeringRange;
        public bool PhysicalWheel,PhysicalWheelFallback,PitLimiter;
        public double PhysicalWheelAngle=double.NaN;
        public string WheelStatus="CALIBRAR";
        public string SteeringInputSource="bruta";
        public Frame Copy() { return (Frame)MemberwiseClone(); }
        public int ScoringCount, ActiveTelemetryCount, ApplicationLocation;
        public string PlayerIdentification = "Não identificado", PlayerName = "";
        public string SessionKey { get { return Track+"/"+Server+"/"+Session+"/"+SessionStarted+"/"+GameWindow+"/"+Demo; } }
        public double Elapsed, Remaining, Throttle, Brake, Rpm, MaxRpm, Speed, Fuel, Capacity;
        public List<Driver> Drivers = new List<Driver>();
        public double ReadAt, TelemetryElapsed = -1;
        public int LapNumber,MaxLaps;
        public double LapStart=-1,CurrentLap=-1,DeltaBest=double.NaN,TrackLength;
        public bool LapInvalid;
        public TyreReading[] Tyres=new TyreReading[0];
        public VehicleDamage Damage=new VehicleDamage();
        public bool DetachedParts;
        public RaceWidgetState Widgets=new RaceWidgetState();
    }

    public static class Format
    {
        public static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
        public static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        public static double Clamp(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }
        public static string Lap(double seconds)
        {
            if (!Finite(seconds) || seconds <= 0) return "—";
            long ms = (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
            return (ms / 60000).ToString() + ":" + ((ms / 1000) % 60).ToString("00") + "." + (ms % 1000).ToString("000");
        }
        public static string Gap(Driver d)
        {
            if (d.FinishStatus == 2) return "DNF";
            if (d.FinishStatus == 3) return "DSQ";
            if (d.Position == 1) return "LÍDER";
            if (d.LapsBehind > 0) return "+" + d.LapsBehind.ToString() + " V";
            return Finite(d.Gap) && d.Gap > 0 ? "+" + d.Gap.ToString("0.000", Br) : "—";
        }
        public static bool IsRace(int session) { return session>=10 && session<=13; }
        public static double BestLapDifference(Frame f,Driver d)
        {
            var player=f.Drivers.FirstOrDefault(x=>x.IsPlayer);
            return player!=null && Finite(player.BestLap) && player.BestLap>0 && Finite(d.BestLap) && d.BestLap>0 ? d.BestLap-player.BestLap : double.NaN;
        }
        public static string Gap(Frame f,Driver d)
        {
            if(IsRace(f.Session)) return Gap(d);
            if(d.FinishStatus==3) return "DSQ";
            double delta=BestLapDifference(f,d);
            if(!Finite(delta)) return "—";
            return (delta>=.0005 ? "+" : delta<=-.0005 ? "−" : "")+Math.Abs(delta).ToString("0.000",Br);
        }
        public static string Penalty(Driver d)
        { return d.Penalties<=0 ? "—" : string.IsNullOrEmpty(d.PenaltyTypes) ? d.Penalties+" PEN" : d.PenaltyTypes; }
        public static string Clock(double seconds)
        {
            if (!Finite(seconds) || seconds < 0) return "—";
            int s = (int)Math.Min(seconds, 359999);
            return (s / 3600).ToString("00") + ":" + ((s / 60) % 60).ToString("00") + ":" + (s % 60).ToString("00");
        }
        public static string Session(int n)
        {
            if (n >= 10 && n <= 13) return "CORRIDA";
            if (n >= 5 && n <= 8) return "CLASSIFICAÇÃO";
            return n == 9 ? "AQUECIMENTO" : "TREINO";
        }
    }

    // Offsets are generated from the upstream ctypes SDK mapping, never guessed.
    public static class LmuParser
    {
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        static string Text(byte[] b, int o, int count)
        {
            int n = 0;
            while (n < count && b[o + n] != 0) n++;
            try { return Utf8.GetString(b, o, n).Trim(); }
            catch (DecoderFallbackException)
            {
                // ANSI fallback without an extra runtime dependency.
                var chars = new char[n];
                for (int i = 0; i < n; i++) chars[i] = (char)b[o + i];
                return new string(chars).Trim();
            }
        }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static double D(byte[] b, int o) { return BitConverter.ToDouble(b, o); }
        static double F(byte[] b, int o) { return BitConverter.ToSingle(b, o); }
        static double Safe(double v, double lo, double hi, double fallback)
        { return Format.Finite(v) && v >= lo && v <= hi ? v : fallback; }

        public static Frame Parse(byte[] b, bool filtered)
        {
            if (b == null || b.Length < Layout.Size) throw new InvalidDataException("Mapa de telemetria incompleto.");
            int sc = Layout.Scoring, te = Layout.Telemetry;
            int count = I(b, sc + Layout.ScoringInfo_mNumVehicles);
            int active = b[te + Layout.Telemetry_activeVehicles];
            int version = I(b, Layout.Generic_gameVersion);
            if (count < 0 || count > Layout.MaxCars || active > Layout.MaxCars)
                throw new InvalidDataException("Layout de telemetria não reconhecido.");
            var f = new Frame();
            f.GameVersion = version;
            if (version <= 0) return f;
            f.Connected = true;
            f.SessionStarted = BitConverter.ToUInt32(b, Layout.EventSessionStart);
            f.GameWindow = BitConverter.ToUInt64(b, Layout.AppWindow);
            f.TelemetryEvent = BitConverter.ToUInt32(b, Layout.EventTelemetry);
            f.ScoringEvent = BitConverter.ToUInt32(b, Layout.EventScoring);
            f.Track = Text(b, sc + Layout.ScoringInfo_mTrackName, 64);
            f.Server = Text(b, sc + Layout.ScoringInfo_mServerName, 32);
            f.Session = I(b, sc + Layout.ScoringInfo_mSession);
            f.ScoringCount=count; f.ActiveTelemetryCount=active;
            f.PlayerName=Text(b,sc+Layout.ScoringInfo_mPlayerName,32);
            f.Phase = b[sc + Layout.ScoringInfo_mGamePhase];
            f.InRealtime = b[sc + Layout.ScoringInfo_mInRealtime] != 0;
            f.Elapsed = Safe(D(b, sc + Layout.ScoringInfo_mCurrentET), 0, 1e8, 0);
            f.Remaining = Safe(F(b, sc + Layout.ScoringInfo_mSessionTimeRemaining), 0, 1e8, -1);
            double end = D(b, sc + Layout.ScoringInfo_mEndET);
            if (f.Remaining <= 0 && Format.Finite(end) && end > f.Elapsed)
                f.Remaining = end - f.Elapsed;
            f.ApplicationLocation=b[Layout.AppLocation];
            f.TrackLength=Safe(D(b,sc+Layout.ScoringInfo_mLapDist),100,100000,0);
            f.MaxLaps=(int)Safe(I(b,sc+Layout.ScoringInfo_mMaxLaps),1,10000,0);
            // ApplicationState is optional in LMU. A zero menu field is NOT a veto on valid scoring/telemetry.
            int playerId = int.MinValue;
            var ids = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int p = Layout.ScoringCars + i * Layout.ScoringCarSize;
                var d = new Driver();
                d.Id = I(b, p + Layout.Car_mID);
                if (!ids.Add(d.Id)) throw new InvalidDataException("Classificação em atualização.");
                d.Name = Text(b, p + Layout.Car_mDriverName, 32);
                d.Class = Text(b, p + Layout.Car_mVehicleClass, 32);
                d.VehicleKey = Text(b, p + Layout.Car_mVehFilename, 32);
                d.CarName=Text(b,p+Layout.Car_mVehicleName,64);
                if (d.Class.Length == 0) d.Class = "SEM CLASSE";
                d.Position = b[p + Layout.Car_mPlace];
                d.Penalties=Math.Max(0,(int)BitConverter.ToInt16(b,p+Layout.Car_mNumPenalties));
                d.IsPlayer = b[p + Layout.Car_mIsPlayer] != 0;
                d.Control=unchecked((sbyte)b[p+Layout.Car_mControl]);
                d.InPits = b[p + Layout.Car_mInPits] != 0;
                d.InGarage=b[p+Layout.Car_mInGarageStall]!=0;
                d.Sector=b[p+Layout.Car_mSector];d.CountLapFlag=b[p+Layout.Car_mCountLapFlag];
                d.LapStart=Safe(D(b,p+Layout.Car_mLapStartET),0,1e8,-1);
                d.CurrentSector1=Safe(D(b,p+Layout.Car_mCurSector1),.001,1e6,-1);
                d.CurrentSector2=Safe(D(b,p+Layout.Car_mCurSector2),.001,1e6,-1);
                d.LastSector1=Safe(D(b,p+Layout.Car_mLastSector1),.001,1e6,-1);
                d.LastSector2=Safe(D(b,p+Layout.Car_mLastSector2),.001,1e6,-1);
                double svx=D(b,p+Layout.Car_mLocalVel),svy=D(b,p+Layout.Car_mLocalVel+8),svz=D(b,p+Layout.Car_mLocalVel+16);
                d.Speed=Safe(Math.Sqrt(svx*svx+svy*svy+svz*svz)*3.6,0,1500,double.NaN);
                d.PitStops = BitConverter.ToInt16(b, p + Layout.Car_mNumPitstops);
                d.Laps = BitConverter.ToInt16(b, p + Layout.Car_mTotalLaps);
                d.FinishStatus = b[p + Layout.Car_mFinishStatus];
                d.LastLap = Safe(D(b, p + Layout.Car_mLastLapTime), 0.001, 1e6, -1);
                d.BestLap = Safe(D(b, p + Layout.Car_mBestLapTime), 0.001, 1e6, -1);
                d.Gap = Safe(D(b, p + Layout.Car_mTimeBehindLeader), 0, 1e8, -1);
                d.LapsBehind = Math.Max(0, I(b, p + Layout.Car_mLapsBehindLeader));
                d.LapDistance=Safe(D(b,p+Layout.Car_mLapDist),-1000,100000,double.NaN);
                d.WorldX=Safe(D(b,p+Layout.Car_mPos),-1e7,1e7,double.NaN);
                d.WorldZ=Safe(D(b,p+Layout.Car_mPos+16),-1e7,1e7,double.NaN);
                if (d.IsPlayer) { playerId = d.Id; f.PlayerClass = d.Class; }
                f.Drivers.Add(d);
            }
            f.Drivers = f.Drivers.OrderBy(x => x.Position == 0 ? 999 : x.Position).ToList();
            var identified=f.Drivers.Where(d=>d.IsPlayer).ToList();
            Driver local=identified.Count==1 ? identified[0] : null;
            if(local!=null) f.PlayerIdentification="Indicador do jogador";
            if(local==null)
            {
                var controlled=f.Drivers.Where(d=>d.Control==0).ToList();
                if(controlled.Count==1) { local=controlled[0]; f.PlayerIdentification="Controle local"; }
            }
            if(local==null && f.PlayerName.Length>0)
            {
                var named=f.Drivers.Where(d=>d.Name==f.PlayerName).ToList();
                if(named.Count==1) { local=named[0]; f.PlayerIdentification="Nome local da sessão"; }
            }
            int ix=b[te+Layout.Telemetry_playerVehicleIdx];
            bool hasIndex=b[te+Layout.Telemetry_playerHasVehicle]!=0 && ix<active;
            if(local==null && hasIndex)
            {
                int id=I(b,Layout.TelemetryCars+ix*Layout.TelemetryCarSize+Layout.Telem_mID);
                local=f.Drivers.FirstOrDefault(d=>d.Id==id);
                if(local!=null) f.PlayerIdentification="Índice do jogador informado pelo jogo";
            }
            playerId=local==null ? int.MinValue : local.Id;
            f.PlayerId=local==null ? -1 : local.Id;
            f.PlayerClass=local==null ? "" : local.Class;
            foreach(var d in f.Drivers) d.IsPlayer=d==local;
            foreach (var group in f.Drivers.GroupBy(x => x.Class))
            {
                int pos = 1;
                foreach (var d in group) d.ClassPosition = d.Position > 0 ? pos++ : 0;
            }
            f.Status = count > 0 ? "Aguardando seu carro" : "Aguardando os pilotos";
            int selected = -1;
            // Scoring ID and telemetry ID must agree, even when their order differs.
            int scanCount=active>0 ? active : playerId!=int.MinValue ? Layout.MaxCars : 0;
            for (int i = 0; i < scanCount; i++)
            {
                int p = Layout.TelemetryCars + i * Layout.TelemetryCarSize;
                if (playerId != int.MinValue && I(b, p + Layout.Telem_mID) == playerId)
                {
                    double elapsed=D(b,p+Layout.Telem_mElapsedTime);
                    // A scan beyond a missing active-count must not revive a stale or empty slot.
                    if(active==0 && (D(b,p+Layout.Telem_mEngineMaxRPM)<=0 || !Format.Finite(elapsed) || Math.Abs(elapsed-f.Elapsed)>2)) continue;
                    selected = i; break;
                }
            }
            if (selected < 0 && playerId == int.MinValue && hasIndex) selected = ix;
            if (selected < 0) return f;
            int t = Layout.TelemetryCars + selected * Layout.TelemetryCarSize;
            double rpm = D(b, t + Layout.Telem_mEngineRPM);
            double throttle = D(b, t + (filtered ? Layout.Telem_mFilteredThrottle : Layout.Telem_mUnfilteredThrottle));
            double brake = D(b, t + (filtered ? Layout.Telem_mFilteredBrake : Layout.Telem_mUnfilteredBrake));
            if (!Format.Finite(rpm) || rpm < 0 || rpm > 50000 || !Format.Finite(throttle) ||
                throttle < -0.01 || throttle > 1.01 || !Format.Finite(brake) || brake < -0.01 || brake > 1.01)
                throw new InvalidDataException("Amostra de telemetria inválida.");
            f.HasTelemetry = true;
            f.TelemetryElapsed=Safe(D(b,t+Layout.Telem_mElapsedTime),0,1e8,-1);
            f.LapNumber=(int)Safe(I(b,t+Layout.Telem_mLapNumber),0,100000,0);
            f.LapStart=Safe(D(b,t+Layout.Telem_mLapStartET),0,1e8,-1);
            if(f.LapStart>=0 && f.TelemetryElapsed>=f.LapStart)
                f.CurrentLap=Safe(f.TelemetryElapsed-f.LapStart,0,1e5,-1);
            f.LapInvalid=b[t+Layout.Telem_mLapInvalidated]!=0;
            f.DeltaBest=Safe(D(b,t+Layout.Telem_mDeltaBest),-1e4,1e4,double.NaN);
            f.Tyres=new TyreReading[4];
            f.DetachedParts=b[t+Layout.Telem_mDetached]!=0;
            for(int i=0;i<4;i++)
            {
                int w=t+Layout.Telem_mWheels+i*Layout.WheelSize;
                double a=Safe(D(b,w+Layout.Wheel_mTemperature),173.15,773.15,double.NaN);
                double c=Safe(D(b,w+Layout.Wheel_mTemperature+8),173.15,773.15,double.NaN);
                double e=Safe(D(b,w+Layout.Wheel_mTemperature+16),173.15,773.15,double.NaN);
                f.Tyres[i]=new TyreReading { Temperature=(a+c+e)/3-273.15,
                    Pressure=Safe(D(b,w+Layout.Wheel_mPressure),1,1000,double.NaN)/100,
                    Optimal=Safe(F(b,w+Layout.Wheel_mOptimalTemp),1,300,double.NaN),
                    Remaining=Safe(D(b,w+Layout.Wheel_mWear),0,1,double.NaN),
                    Flat=b[w+Layout.Wheel_mFlat]!=0,Detached=b[w+Layout.Wheel_mDetached]!=0 };
            }
            f.Throttle = Format.Clamp(throttle, 0, 1); f.Brake = Format.Clamp(brake, 0, 1);
            f.Rpm = rpm;
            f.PitLimiter=b[t+Layout.Telem_mSpeedLimiter]==1 || b[t+Layout.Telem_mSpeedLimiterActive]==1;
            var tyreDriver=f.Drivers.FirstOrDefault(d=>d.Id==I(b,t+Layout.Telem_mID));
            if(tyreDriver!=null)
            {
                tyreDriver.TyreFront=Text(b,t+Layout.Telem_mFrontTireCompoundName,18);
                tyreDriver.TyreRear=Text(b,t+Layout.Telem_mRearTireCompoundName,18);
                if(tyreDriver.TyreFront.Length>0 || tyreDriver.TyreRear.Length>0) tyreDriver.TyreSource="telemetria";
            }
            f.MaxRpm = Safe(D(b, t + Layout.Telem_mEngineMaxRPM), 1, 50000, 0);
            f.Gear = I(b, t + Layout.Telem_mGear);
            f.MaxGears = b[t + Layout.Telem_mMaxGears];
            if (f.MaxGears > 12) f.MaxGears = 0;
            f.Steering = Safe(D(b, t + Layout.Telem_mUnfilteredSteering), -1.001, 1.001, double.NaN);
            double filteredSteering=Safe(D(b,t+Layout.Telem_mFilteredSteering),-1.001,1.001,double.NaN);
            if(!Format.Finite(f.Steering) || (f.Steering==0 && Format.Finite(filteredSteering) && Math.Abs(filteredSteering)>.0001))
            { f.Steering=filteredSteering;f.SteeringInputSource="filtrada"; }
            if (Format.Finite(f.Steering)) f.Steering = Format.Clamp(f.Steering, -1, 1);
            f.SteeringRange = Safe(F(b, t + Layout.Telem_mPhysicalSteeringWheelRange), 1, 2880, 0);
            f.VisualSteeringRange = Safe(F(b, t + Layout.Telem_mVisualSteeringWheelRange), 1, 2880, 0);
            f.VehicleModel = Text(b, t + Layout.Telem_mVehicleModel, 30);
            if (f.Gear < -1 || f.Gear > 12) throw new InvalidDataException("Marcha fora do intervalo esperado.");
            f.Fuel = Safe(D(b, t + Layout.Telem_mFuel), 0, 1000, -1);
            f.Capacity = Safe(D(b, t + Layout.Telem_mFuelCapacity), 0.01, 1000, 0);
            double vx = D(b, t + Layout.Telem_mLocalVel), vy = D(b, t + Layout.Telem_mLocalVel + 8), vz = D(b, t + Layout.Telem_mLocalVel + 16);
            f.Speed = Safe(Math.Sqrt(vx*vx + vy*vy + vz*vz) * 3.6, 0, 1500, 0);
            f.Status = f.Phase == 9 ? "Sessão pausada" : (f.InRealtime ? "Na pista" : "Sessão conectada")+" • "+count+" piloto"+(count==1 ? "" : "s");
            return f;
        }
    }

    public interface ITelemetryReader : IDisposable
    {
        Frame Read(bool filtered);
    }

    public sealed class NativeReader : ITelemetryReader
    {
        MemoryMappedFile map;
        MemoryMappedViewAccessor view;
        readonly byte[] buffer = new byte[Layout.Size];
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Action<string,Exception> error;
        double lastProbe = -10;
        readonly DataFreshness freshness=new DataFreshness();
        int errors;
        public NativeReader() : this(null) { }
        public NativeReader(Action<string,Exception> log) { error=log; }
        public Frame Read(bool filtered)
        {
            double now = clock.Elapsed.TotalSeconds;
            try
            {
                if (now - lastProbe >= 1)
                {
                    lastProbe = now;
                    Process[] games = Process.GetProcessesByName("Le Mans Ultimate");
                    bool running = games.Length > 0;
                    foreach (var game in games) game.Dispose();
                    if (!running) { Dispose(); return new Frame(); }
                    if (map == null)
                    {
                        map = MemoryMappedFile.OpenExisting("LMU_Data", MemoryMappedFileRights.Read);
                        view = map.CreateViewAccessor(0, Layout.Size, MemoryMappedFileAccess.Read);
                        freshness.Clear();
                    }
                }
                if (view == null) return new Frame { Status = "Entre em uma sessão do LMU" };
                bool stable = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    uint a = view.ReadUInt32(Layout.EventScoring), b = view.ReadUInt32(Layout.EventTelemetry);
                    view.ReadArray(0, buffer, 0, buffer.Length);
                    stable = a == view.ReadUInt32(Layout.EventScoring) && b == view.ReadUInt32(Layout.EventTelemetry)
                        && a == BitConverter.ToUInt32(buffer, Layout.EventScoring)
                        && b == BitConverter.ToUInt32(buffer, Layout.EventTelemetry);
                    if (stable) break;
                }
                if (!stable) return null; // keep the previous coherent frame briefly
                Frame f = LmuParser.Parse(buffer, filtered);
                freshness.Apply(f,now);
                f.ReadAt = now; errors = 0;
                return f;
            }
            catch (FileNotFoundException) { Dispose(); return new Frame { Status = "LMU aberto • aguardando a telemetria" }; }
            catch (UnauthorizedAccessException) { Dispose(); return new Frame { Status = "Sem acesso • abra jogo e app com o mesmo nível de permissão" }; }
            // InvalidDataException is NOT an IOException. Session changes can expose a partial packet.
            catch (InvalidDataException ex)
            {
                if(error!=null) error("Telemetria: amostra inválida descartada",ex);
                errors=Math.Min(errors+1,3);
                if(errors<3) return null;
                return new Frame { Connected=true,Status="Aguardando dados válidos da sessão" };
            }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is ArgumentException) && !(ex is InvalidOperationException) && !(ex is System.ComponentModel.Win32Exception)) throw;
                if (++errors < 3) return null;
                Dispose();
                return new Frame { Status = "Telemetria incompatível ou indisponível • reinicie o LMU" };
            }
        }
        public void Dispose()
        {
            if (view != null) { view.Dispose(); view = null; }
            if (map != null) { map.Dispose(); map = null; }
            freshness.Clear();
        }
    }

}
