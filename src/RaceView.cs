using System;
using System.Collections.Generic;
using System.Linq;

namespace Arthur.LmuOverlay
{
    public sealed class ClassView
    {
        public string Name;
        public bool Own;
        public int Total;
        public List<Driver> Drivers;
    }

    public static class RaceView
    {
        public const int OwnRows = 8, OtherRows = 5;
        public static List<ClassView> Build(Frame f) { return Build(f,false); }
        public static List<ClassView> Build(Frame f,bool onlyOwn)
        {
            var groups = f.Drivers.GroupBy(d=>d.Class).ToList();
            string own = f.PlayerClass;
            var player = f.Drivers.FirstOrDefault(d=>d.IsPlayer);
            if (player != null) own = player.Class;
            if (groups.Count == 1) own = groups[0].Key;
            var result = new List<ClassView>();
            foreach (var group in groups.OrderBy(g=>g.Key==own ? 0 : 1).ThenBy(g=>g.Min(d=>d.Position>0 ? d.Position : 999)))
            {
                if(onlyOwn && group.Key!=own) continue;
                var all = group.OrderBy(d=>d.ClassPosition>0 ? d.ClassPosition : 999).ThenBy(d=>d.Position).ToList();
                bool mine = group.Key == own;
                List<Driver> shown;
                if (mine && all.Any(d=>d.IsPlayer))
                {
                    int ix=all.FindIndex(d=>d.IsPlayer);
                    int start=Math.Max(0,ix-3),end=Math.Min(all.Count-1,ix+3);
                    shown=all.Skip(start).Take(end-start+1).ToList();
                    if(start>0) shown.Insert(0,all[0]);
                }
                else shown = all.Take(mine ? OwnRows : OtherRows).ToList();
                result.Add(new ClassView { Name=group.Key, Own=mine, Total=all.Count, Drivers=shown });
            }
            return result;
        }
        public static Driver Best(Frame f,bool onlyOwn)
        {
            string own=f.Drivers.Where(x=>x.IsPlayer).Select(x=>x.Class).FirstOrDefault() ?? f.PlayerClass;
            return f.Drivers.Where(d=>d.FinishStatus!=3 && Format.Finite(d.BestLap) && d.BestLap>0 && (!onlyOwn || d.Class==own))
                .OrderBy(d=>d.BestLap).ThenBy(d=>d.Position).FirstOrDefault();
        }
    }

    // Capture the last pre-start classification. Qualification is not assumed to be the actual starting grid.
    public sealed class StartPositions
    {
        readonly Dictionary<string,int> grid = new Dictionary<string,int>();
        string session = "";
        double elapsed;
        bool frozen;
        int previousPhase;
        static string Key(Driver d) { return d.Id+"/"+d.Class+"/"+d.VehicleKey; }
        public void Clear() { grid.Clear(); session=""; frozen=false; elapsed=0; previousPhase=0; }
        public void Apply(Frame f)
        {
            if (f.Demo) return;
            if (!f.Connected || f.Track.Length==0 || f.Session < 10 || f.Session > 13)
            {
                Clear(); foreach(var d in f.Drivers) d.StartClassPosition=null; return;
            }
            if (f.SessionKey!=session || f.Elapsed+1 < elapsed || (previousPhase>=5 && f.Phase>=1 && f.Phase<=4))
            { grid.Clear(); frozen=false; session=f.SessionKey; }
            elapsed=f.Elapsed; previousPhase=f.Phase;
            if (!f.Stale && f.Phase>=1 && f.Phase<=4 && f.Drivers.Count>0)
            {
                grid.Clear();
                foreach(var d in f.Drivers) if(d.ClassPosition>0) grid[Key(d)]=d.ClassPosition;
            }
            else if(!f.Stale && (f.Phase==5 || f.Phase==6 || f.Phase==8)) frozen=true;
            foreach(var d in f.Drivers)
            {
                int initial;
                d.StartClassPosition = grid.TryGetValue(Key(d),out initial) ? initial : (int?)null;
            }
        }
        public bool HasStart { get { return frozen && grid.Count>0; } }
    }

    // Event counters can be absent in a producer. Native elapsed times also prove a sample advanced.
    public sealed class DataFreshness
    {
        string session="";
        double scoreTime=-1,telemTime=-1,lastScore,lastTelem;
        uint scoreEvent,telemEvent;
        public void Clear() { session="";scoreTime=telemTime=-1;scoreEvent=telemEvent=0;lastScore=lastTelem=0; }
        public void Apply(Frame f,double now)
        {
            if(!f.Connected) { Clear();return; }
            bool changed=session!=f.SessionKey;
            if(changed || f.ScoringEvent!=scoreEvent || f.Elapsed!=scoreTime) lastScore=now;
            if(changed || f.TelemetryEvent!=telemEvent || (f.HasTelemetry && f.TelemetryElapsed>=0 && f.TelemetryElapsed!=telemTime)) lastTelem=now;
            session=f.SessionKey;scoreEvent=f.ScoringEvent;telemEvent=f.TelemetryEvent;scoreTime=f.Elapsed;telemTime=f.TelemetryElapsed;
            f.ScoringStale=f.Phase==9 || now-lastScore>2;
            f.Stale=f.ScoringStale || (f.HasTelemetry && now-lastTelem>2);
            if(f.Stale) { f.HasTelemetry=false;f.Status=f.Phase==9 ? "Sessão pausada" : "Sem atualização • pausa ou menu"; }
        }
    }

    public static class Instruments
    {
        public static double SteeringAngle(Frame f, double overrideRange)
        { return SteeringAngle(f,overrideRange,100); }
        public static double SteeringAngle(Frame f, double overrideRange,double gainPercent)
        {
            if(f.PhysicalWheel || f.PhysicalWheelFallback) return f.PhysicalWheelAngle;
            double range = SteeringRange(f,overrideRange);
            if (!f.HasTelemetry || f.Stale || !Format.Finite(f.Steering) || !Format.Finite(gainPercent) || range<=0) return double.NaN;
            return Format.Clamp(f.Steering,-1,1)*range/2*Format.Clamp(gainPercent,10,200)/100;
        }
        public static double SteeringRange(Frame f,double configured)
        { return configured>0 ? configured : f.SteeringRange>0 ? f.SteeringRange : f.GarageSteeringRange>0 ? f.GarageSteeringRange : f.VisualSteeringRange; }
        public static double SteeringRange(Frame f,double configured,double carConfigured)
        { return configured>0 ? configured : carConfigured>0 ? carConfigured : SteeringRange(f,0); }
        public static string SteeringSource(Frame f,double configured)
        { return configured>0 ? "manual" : f.SteeringRange>0 ? "física" : f.GarageSteeringRange>0 ? "setup do carro" : f.VisualSteeringRange>0 ? "visual" : "direção normalizada"; }
        public static double SteeringDrawing(Frame f,double configured)
        { return SteeringDrawing(f,configured,100); }
        public static double SteeringDrawing(Frame f,double configured,double gainPercent)
        {
            double angle=SteeringAngle(f,configured,gainPercent);
            if(f.PhysicalWheel || f.PhysicalWheelFallback) return angle;
            return Format.Finite(angle) ? angle : f.HasTelemetry && !f.Stale && Format.Finite(f.Steering) && Format.Finite(gainPercent) ? f.Steering*135*Format.Clamp(gainPercent,10,200)/100 : double.NaN;
        }
        public static double SteeringAngle(Frame f,double overrideRange,double gainPercent,double carConfigured)
        {
            if(f.PhysicalWheel || f.PhysicalWheelFallback) return f.PhysicalWheelAngle;
            double range=SteeringRange(f,overrideRange,carConfigured);
            if(!f.HasTelemetry || f.Stale || !Format.Finite(f.Steering) || !Format.Finite(gainPercent) || range<=0) return double.NaN;
            return Format.Clamp(f.Steering,-1,1)*range/2*Format.Clamp(gainPercent,10,200)/100;
        }
        public static double SteeringDrawing(Frame f,double configured,double gainPercent,double carConfigured)
        {
            double angle=SteeringAngle(f,configured,gainPercent,carConfigured);
            if(f.PhysicalWheel || f.PhysicalWheelFallback) return angle;
            return Format.Finite(angle) ? angle : f.HasTelemetry && !f.Stale && Format.Finite(f.Steering) && Format.Finite(gainPercent) ? f.Steering*135*Format.Clamp(gainPercent,10,200)/100 : double.NaN;
        }
        public static double ShiftPoint(Frame f, double overrideRpm)
        { return overrideRpm>0 ? overrideRpm : f.MaxRpm>0 ? f.MaxRpm*.96 : 0; }
        public static bool ShiftNow(Frame f, double overrideRpm)
        {
            double point=ShiftPoint(f,overrideRpm);
            return f.HasTelemetry && !f.Stale && f.Gear>0 && f.MaxGears>f.Gear && f.Speed>10 &&
                f.Throttle>.3 && point>0 && f.Rpm>=point;
        }
    }
}
