using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Arthur.LmuOverlay
{
    [DataContract]
    public sealed class LearningSession
    {
        [DataMember] public int Version=1,Mode;
        [DataMember] public string Id="",Track="",Server="",Player="";
        [DataMember] public double Length;
        [DataMember] public DateTime StartedUtc;
        public override string ToString()
        { return StartedUtc.ToLocalTime().ToString("dd/MM/yy HH:mm")+" • "+Track+" • "+Format.Session(Mode)+(Server.Length>0 ? " • online" : ""); }
    }
    [DataContract]
    public sealed class LearningLapInfo
    {
        [DataMember] public string Id="",SessionId="",DriverKey="",Driver="",Car="",Class="",Tyres="";
        [DataMember] public int Number,GameLap;
        [DataMember] public bool IsPlayer,Completed,StartObserved,Invalid,Unscored,PitLap,OfficialTime,HasGaps;
        [DataMember] public double Time=-1,Sector1=-1,Sector2=-1,Sector3=-1,StartET,EndET,Fuel=-1;
        [DataMember] public DateTime RecordedUtc;
        public bool Valid { get { return Completed && OfficialTime && !Invalid && !Unscored && !PitLap; } }
        public string State
        {
            get
            {
                string value=!Completed ? "Interrompida" : Invalid ? "Invalidada" : PitLap ? "Boxes" : Unscored || !OfficialTime ? "Não cronometrada" : "Válida";
                if(!StartObserved) value+=" • parcial";if(HasGaps) value+=" • falhas";
                return value;
            }
        }
        public override string ToString() { return "Volta "+Number+" • "+LearningFormat.Time(this)+" • "+State; }
    }
    [DataContract]
    public sealed class LearningSample
    {
        [DataMember] public double Distance,Time,X,Z,Speed,Throttle=double.NaN,Brake=double.NaN;
        [DataMember] public bool BreakBefore;
    }
    [DataContract]
    public sealed class LearningLap
    {
        [DataMember] public LearningSession Session;
        [DataMember] public LearningLapInfo Info;
        [DataMember] public LearningSample[] Samples;
    }
    public static class LearningFormat
    {
        public static string Time(LearningLapInfo lap)
        { return (lap.Time>0 && !lap.OfficialTime ? "≈ " : "")+Format.Lap(lap.Time); }
        public static string Delta(double value,int decimals=2)
        { return !Format.Finite(value) ? "—" : (value>=0 ? "+" : "−")+Math.Abs(value).ToString("F"+decimals,Format.Br)+" s"; }
        public static string Seconds(double value)
        { return value<0 || !Format.Finite(value) ? "—" : value.ToString("0.000",Format.Br); }
    }
    // Small immutable-by-convention snapshots. No disk or analysis work occurs
    // on the telemetry reader; the worker owns every object from this point on.
    public sealed class LearningFrame
    {
        public string Key,Track,Server,Player;
        public int Mode;
        public double Time,Length;
        public bool Active,Dropped;
        public DateTime Utc;
        public LearningCar[] Cars;
        public static LearningFrame Capture(Frame f)
        {
            var result=new LearningFrame { Key=f.SessionKey,Track=f.Track,Server=f.Server,Player=f.PlayerName,Mode=f.Session,
                Time=f.Elapsed,Length=f.TrackLength,Utc=DateTime.UtcNow,Dropped=f.TrainingReset,
                Active=f.Connected && !f.ScoringStale && f.Phase!=9 && !f.Demo && f.TrackLength>=100 };
            result.Cars=f.Drivers.Select(d=>new LearningCar { Id=d.Id,Identity=d.Id+"/"+d.Name+"/"+d.VehicleKey+"/"+d.Class,
                Name=d.Name,Car=d.CarName,Class=d.Class,IsPlayer=d.IsPlayer,CanRecord=d.Control!=3 && d.Control!=-1 && !d.InGarage && (!d.IsPlayer || f.InRealtime),
                Laps=d.Laps,Start=d.LapStart,Distance=d.LapDistance,X=d.WorldX,Z=d.WorldZ,Speed=d.Speed,
                Sector1=d.CurrentSector1,Sector2=d.CurrentSector2,LastSector1=d.LastSector1,LastSector2=d.LastSector2,LastLap=d.LastLap,
                CountLap=d.CountLapFlag,Invalid=d.IsPlayer && f.HasTelemetry && f.LapInvalid,InPits=d.InPits,
                Throttle=d.IsPlayer && f.HasTelemetry && !f.Stale ? f.Throttle : double.NaN,
                Brake=d.IsPlayer && f.HasTelemetry && !f.Stale ? f.Brake : double.NaN,
                Fuel=d.IsPlayer && f.HasTelemetry ? f.Fuel : -1,Tyres=d.TyreFront }).ToArray();
            return result;
        }
    }
    public sealed class LearningCar
    {
        public int Id,Laps,CountLap;
        public string Identity,Name,Car,Class,Tyres;
        public bool IsPlayer,CanRecord,Invalid,InPits;
        public double Start,Distance,X,Z,Speed,Sector1,Sector2,LastSector1,LastSector2,LastLap,Fuel,Throttle,Brake;
    }
    public static class LearningContinuity
    {
        public const double MaxInterval=1.0;
        public static bool Link(LearningSample a,LearningSample b)
        {
            double dt=b.Time-a.Time,dd=b.Distance-a.Distance;
            return !b.BreakBefore && Format.Finite(dt) && Format.Finite(dd) && dt>0 && dt<=MaxInterval && dd>=0 && dd<=40+dt*150;
        }
    }
    // Single writer, independent from Windows Forms and from storage.
    public sealed class LearningRecorder
    {
        sealed class ActiveLap
        {
            public LearningLapInfo Info;
            public readonly List<LearningSample> Points=new List<LearningSample>();
            public double Cumulative2=-1,LastSeen;
            public int Laps;
            public bool BreakNext;
        }
        readonly Action<LearningSession> sessionStarted;
        readonly Action<LearningLap> completed;
        readonly Dictionary<int,ActiveLap> active=new Dictionary<int,ActiveLap>();
        readonly Dictionary<string,int> numbers=new Dictionary<string,int>();
        string sessionKey="";
        double lastTime=-1;
        LearningSession session;
        public LearningRecorder(Action<LearningSession> onSession,Action<LearningLap> onLap)
        { sessionStarted=onSession;completed=onLap; }
        public void Accept(LearningFrame f)
        {
            if(!f.Active || !Format.Finite(f.Time) || f.Length<100)
            { Break();return; }
            if(session==null || f.Key!=sessionKey || f.Time<lastTime-.01)
            {
                Flush();numbers.Clear();sessionKey=f.Key;
                session=new LearningSession { Id=Guid.NewGuid().ToString("N"),Track=f.Track,Server=f.Server??"",Player=f.Player??"",Mode=f.Mode,Length=f.Length,StartedUtc=f.Utc };
                sessionStarted(session);lastTime=-1;
            }
            if(f.Dropped) Break();
            if(f.Time<=lastTime) return;
            lastTime=f.Time;
            foreach(var car in f.Cars)
            {
                ActiveLap current;active.TryGetValue(car.Id,out current);
                if(current!=null && current.Info.DriverKey!=car.Identity)
                { Finish(current,null,f,false);active.Remove(car.Id);current=null; }
                if(!car.CanRecord || car.Start<0 || car.Start>f.Time+.05 || !Format.Finite(car.Start))
                {
                    if(current!=null) { Finish(current,null,f,false);active.Remove(car.Id); }
                    continue;
                }
                bool startObserved=false;
                if(current!=null && (Math.Abs(car.Start-current.Info.StartET)>.01 || car.Laps!=current.Laps))
                {
                    var last=current.Points.LastOrDefault();double duration=car.Start-current.Info.StartET;
                    bool official=duration>0 && car.LastLap>0 && Math.Abs(duration-car.LastLap)<.15 && car.Laps>=current.Laps && car.Laps<=current.Laps+1;
                    bool crossed=last!=null && last.Distance>f.Length-Math.Min(250,f.Length*.1) && car.Distance>=0 &&
                        car.Distance<Math.Min(250,f.Length*.1) && f.Time-current.LastSeen<=LearningContinuity.MaxInterval &&
                        car.Start>current.Info.StartET && car.Start>=current.LastSeen-.05 && car.Start<=f.Time+.05;
                    bool finish=official || crossed;
                    Finish(current,car,f,finish);active.Remove(car.Id);current=null;startObserved=crossed;
                }
                if(current==null)
                {
                    int number;numbers.TryGetValue(car.Identity,out number);numbers[car.Identity]=++number;
                    current=new ActiveLap { Laps=car.Laps,LastSeen=f.Time,Info=new LearningLapInfo {
                        Id=Guid.NewGuid().ToString("N"),SessionId=session.Id,DriverKey=car.Identity,Driver=car.Name,Car=car.Car,Class=car.Class,
                        Number=number,GameLap=car.Laps+1,IsPlayer=car.IsPlayer,StartET=car.Start,RecordedUtc=f.Utc,Fuel=car.Fuel,Tyres=car.Tyres??"",
                        StartObserved=startObserved || (f.Time-car.Start<=.35 && car.Distance>=0 && car.Distance<=15) } };
                    active[car.Id]=current;
                    if(current.Info.StartObserved)
                        current.Points.Add(new LearningSample { Distance=0,Time=0,X=car.X,Z=car.Z,Speed=car.Speed,Throttle=car.Throttle,Brake=car.Brake });
                }
                current.LastSeen=f.Time;
                current.Info.IsPlayer|=car.IsPlayer;
                double into=f.Time-current.Info.StartET;
                current.Info.Invalid|=car.Invalid || car.CountLap==1;
                current.Info.Unscored|=car.CountLap!=2;
                current.Info.PitLap|=car.InPits;
                if(car.Sector1>0 && car.Sector1<=into+.05) current.Info.Sector1=car.Sector1;
                if(car.Sector2>current.Info.Sector1 && current.Info.Sector1>0 && car.Sector2<=into+.05) current.Cumulative2=car.Sector2;
                if(Format.Finite(car.Distance) && car.Distance>=0 && car.Distance<=f.Length && into>=0 && current.Points.Count<100000)
                {
                    var point=new LearningSample { Distance=car.Distance,Time=into,X=car.X,Z=car.Z,Speed=car.Speed,
                        Throttle=car.Throttle,Brake=car.Brake,BreakBefore=current.BreakNext };
                    var previous=current.Points.LastOrDefault();
                    if(previous==null || point.Time>previous.Time)
                    {
                        if(previous!=null && !LearningContinuity.Link(previous,point)) { point.BreakBefore=true;current.Info.HasGaps=true; }
                        current.Points.Add(point);current.BreakNext=false;
                    }
                }
                else { current.BreakNext=true;current.Info.HasGaps=true; }
            }
            foreach(int id in active.Where(x=>f.Time-x.Value.LastSeen>10).Select(x=>x.Key).ToArray())
            { Finish(active[id],null,f,false);active.Remove(id); }
        }
        void Finish(ActiveLap current,LearningCar next,LearningFrame f,bool complete)
        {
            var info=current.Info;info.Completed=complete;info.EndET=complete ? next.Start : current.LastSeen;
            double duration=info.EndET-info.StartET;
            info.OfficialTime=complete && next.LastLap>0 && Math.Abs(next.LastLap-duration)<.15;
            info.Time=complete && duration>0 ? (info.OfficialTime ? next.LastLap : duration) : -1;
            // Last-sector fields can still refer to an earlier valid lap. Use
            // them only when the last-lap duration matches this exact lap start.
            if(info.OfficialTime)
            {
                if(info.Sector1<=0 && next.LastSector1>0 && next.LastSector1<info.Time) info.Sector1=next.LastSector1;
                if(current.Cumulative2<=0 && next.LastSector2>info.Sector1 && next.LastSector2<info.Time) current.Cumulative2=next.LastSector2;
            }
            info.Sector2=current.Cumulative2>info.Sector1 && info.Sector1>0 ? current.Cumulative2-info.Sector1 : -1;
            info.Sector3=info.OfficialTime && current.Cumulative2>0 && info.Time>current.Cumulative2 ? info.Time-current.Cumulative2 : -1;
            if(complete)
            {
                var previous=current.Points.LastOrDefault();
                var end=new LearningSample { Distance=session.Length,Time=duration,X=next.X,Z=next.Z,Speed=next.Speed };
                if(previous!=null && duration>=previous.Time && f.Time>=next.Start && f.Time-current.LastSeen<=LearningContinuity.MaxInterval)
                {
                    double denominator=f.Time-current.LastSeen;
                    double fraction=denominator>0 ? Format.Clamp((next.Start-current.LastSeen)/denominator,0,1) : 0;
                    end.X=previous.X+(next.X-previous.X)*fraction;end.Z=previous.Z+(next.Z-previous.Z)*fraction;
                }
                if(previous==null || end.Time>previous.Time)
                {
                    end.BreakBefore=previous==null || current.BreakNext || !LearningContinuity.Link(previous,end);
                    info.HasGaps|=end.BreakBefore;current.Points.Add(end);
                }
            }
            if(current.Points.Count>=2 || info.OfficialTime)
                completed(new LearningLap { Session=session,Info=info,Samples=current.Points.ToArray() });
        }
        public void Break() { foreach(var state in active.Values) state.BreakNext=true; }
        public void Flush()
        {
            foreach(var state in active.Values) Finish(state,null,null,false);
            active.Clear();
        }
    }
}
