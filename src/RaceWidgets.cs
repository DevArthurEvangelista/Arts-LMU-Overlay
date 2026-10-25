using System;
using System.Collections.Generic;
using System.Linq;

namespace Arthur.LmuOverlay
{
    public enum PanelKind { Pedals,Standings,LapTimer,Tyres,Fuel,TrackMap,Delta }
    public sealed class TrackPoint
    {
        public int Bin;
        public double X,Z;
    }
    public sealed class RaceWidgetState
    {
        public double FuelPerLap=double.NaN,FuelLaps=double.NaN,EstimatedLaps=double.NaN;
        public double FuelNeeded=double.NaN,FuelToAdd=double.NaN,PredictedLap=double.NaN;
        public int FuelSamples;
        public TrackPoint[] Map=new TrackPoint[0];
        public double MapCoverage;
    }
    // Single writer: the telemetry worker. Published snapshots are never mutated.
    public sealed class RaceWidgets
    {
        const int Bins=720;
        string session="";
        int lap=-1;
        double lapStart=-1,startFuel,lastFuel,lastElapsed=-1,lastMap=-1;
        bool completeLap,clean;
        readonly Queue<double> consumption=new Queue<double>();
        readonly TrackPoint[] track=new TrackPoint[Bins];
        TrackPoint[] published=new TrackPoint[0];
        public void Apply(Frame f)
        {
            string key=f.SessionKey+"/"+f.PlayerId;
            if(key!=session || (lastElapsed>=0 && f.TelemetryElapsed>=0 && f.TelemetryElapsed<lastElapsed-.2))
            {
                session=key;lap=-1;lapStart=-1;lastElapsed=-1;completeLap=false;clean=false;
                consumption.Clear();Array.Clear(track,0,track.Length);published=new TrackPoint[0];lastMap=-1;
            }
            var result=new RaceWidgetState();
            if(!f.HasTelemetry || f.Stale)
            { completeLap=false;lap=-1;result.Map=published;result.MapCoverage=published.Length/(double)Bins;f.Widgets=result;return; }
            var player=f.Drivers.FirstOrDefault(d=>d.IsPlayer);
            bool fuelValid=Format.Finite(f.Fuel) && f.Fuel>=0;
            if(lap<0)
            { lap=f.LapNumber;lapStart=f.LapStart;lastFuel=startFuel=f.Fuel;clean=false; }
            bool boundary=f.LapNumber!=lap || Math.Abs(f.LapStart-lapStart)>.1;
            bool interrupted=lastElapsed>=0 && (f.TelemetryElapsed-lastElapsed>1 || f.Fuel>lastFuel+.15);
            if(interrupted || (player!=null && player.InPits) || !fuelValid) clean=false;
            if(boundary)
            {
                double used=startFuel-f.Fuel;
                if(completeLap && clean && !interrupted && f.LapNumber==lap+1 && used>.05 && used<200)
                { consumption.Enqueue(used);while(consumption.Count>5) consumption.Dequeue(); }
                completeLap=f.LapNumber==lap+1 && fuelValid && f.LapStart>=0 && f.TelemetryElapsed-f.LapStart<=1;
                clean=completeLap && (player==null || !player.InPits);
                lap=f.LapNumber;lapStart=f.LapStart;startFuel=f.Fuel;
            }
            lastFuel=f.Fuel;lastElapsed=f.TelemetryElapsed;
            if(player!=null && player.BestLap>0 && !f.LapInvalid && Format.Finite(f.DeltaBest) && f.CurrentLap>1)
            {
                double predicted=player.BestLap+f.DeltaBest;
                if(predicted>0 && predicted>=f.CurrentLap) result.PredictedLap=predicted;
            }
            result.FuelSamples=consumption.Count;
            if(consumption.Count>0)
            {
                result.FuelPerLap=consumption.Average();
                if(fuelValid) result.FuelLaps=f.Fuel/result.FuelPerLap;
                if(player!=null && Format.IsRace(f.Session))
                {
                    if(f.MaxLaps>0) result.EstimatedLaps=Math.Max(0,f.MaxLaps-player.Laps);
                    else if(f.Remaining>=0 && player.BestLap>0) result.EstimatedLaps=Math.Floor(f.Remaining/player.BestLap)+1;
                    if(Format.Finite(result.EstimatedLaps))
                    {
                        result.FuelNeeded=result.EstimatedLaps*result.FuelPerLap;
                        if(fuelValid) result.FuelToAdd=Math.Max(0,result.FuelNeeded-f.Fuel);
                    }
                }
            }
            // Scoring positions are sufficient to learn a real, partial route.
            // Separate missing bins keep unknown track segments visibly absent.
            if(f.TelemetryElapsed-lastMap>=.2 || lastMap<0)
            {
                lastMap=f.TelemetryElapsed;
                if(f.TrackLength>0 && !string.IsNullOrEmpty(f.PlayerClass)) foreach(var d in f.Drivers)
                {
                    if(d.Class!=f.PlayerClass || d.InPits || d.FinishStatus>1 || !Format.Finite(d.LapDistance) ||
                        d.LapDistance<0 || d.LapDistance>=f.TrackLength || !Format.Finite(d.WorldX) || !Format.Finite(d.WorldZ) ||
                        (Math.Abs(d.WorldX)<.001 && Math.Abs(d.WorldZ)<.001)) continue;
                    int bin=Math.Min(Bins-1,(int)(d.LapDistance/f.TrackLength*Bins));
                    track[bin]=new TrackPoint { Bin=bin,X=d.WorldX,Z=d.WorldZ };
                }
                published=track.Where(p=>p!=null).ToArray();
            }
            result.Map=published;result.MapCoverage=published.Length/(double)Bins;f.Widgets=result;
        }
    }
}
