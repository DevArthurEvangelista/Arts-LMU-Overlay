using System;
using System.Collections.Generic;
using System.Linq;

namespace Arthur.LmuOverlay
{
    public sealed class LearningSegment
    {
        public int Index;
        public double Start,End,TimeA=double.NaN,TimeB=double.NaN,Delta=double.NaN;
        public bool Available { get { return Format.Finite(Delta); } }
    }
    public sealed class LearningComparison
    {
        public LearningLap A,B;
        public LearningSegment[] Segments;
        public double[] Distance,Delta,SpeedA,SpeedB;
        public int Available { get { return Segments.Count(s=>s.Available); } }
        public double Length { get { return A.Session.Length; } }
        public static LearningLapInfo Best(IEnumerable<LearningLapInfo> laps)
        { return laps.Where(l=>l.Valid && l.Time>0).OrderBy(l=>l.Time).ThenByDescending(l=>l.StartObserved && !l.HasGaps).FirstOrDefault(); }
        public static LearningComparison Build(LearningLap a,LearningLap b,int size)
        {
            if(a==null || b==null || a.Session.Id!=b.Session.Id || a.Session.Track!=b.Session.Track || Math.Abs(a.Session.Length-b.Session.Length)>.5)
                throw new InvalidOperationException("Selecione duas voltas da mesma sessão e traçado.");
            double length=a.Session.Length;if(!Format.Finite(length) || length<100 || length>100000 || size<50 || size>1000)
                throw new InvalidOperationException("Distância de comparação inválida.");
            var ta=new Passage(a.Samples,length);var tb=new Passage(b.Samples,length);
            int count=(int)Math.Ceiling(length/size);
            var result=new LearningComparison { A=a,B=b,Segments=new LearningSegment[count] };
            for(int i=0;i<count;i++)
            {
                var s=new LearningSegment { Index=i,Start=i*size,End=Math.Min((i+1)*size,length) };
                s.TimeA=ta.Duration(s.Start,s.End);s.TimeB=tb.Duration(s.Start,s.End);s.Delta=s.TimeA-s.TimeB;
                result.Segments[i]=s;
            }
            // A fixed distance grid avoids comparing cars at different places
            // simply because their samples arrived at the same clock time.
            int points=Math.Min(4001,(int)(length/10)+2);
            result.Distance=new double[points];result.Delta=new double[points];result.SpeedA=new double[points];result.SpeedB=new double[points];
            for(int i=0;i<points;i++)
            {
                double d=length*i/(points-1);result.Distance[i]=d;result.Delta[i]=ta.At(d,false)-tb.At(d,false);
                result.SpeedA[i]=ta.At(d,true);result.SpeedB[i]=tb.At(d,true);
            }
            return result;
        }
        sealed class Passage
        {
            readonly LearningSample[] points;
            readonly List<Tuple<double,double>> bad=new List<Tuple<double,double>>();
            public Passage(LearningSample[] samples,double length)
            {
                points=samples??new LearningSample[0];
                if(points.Length==0) { bad.Add(Tuple.Create(0.0,length));return; }
                if(points[0].Distance>0) bad.Add(Tuple.Create(0.0,points[0].Distance));
                for(int i=1;i<points.Length;i++)
                {
                    var a=points[i-1];var b=points[i];
                    if(!LearningContinuity.Link(a,b))
                    {
                        double lo=Math.Min(a.Distance,b.Distance),hi=Math.Max(a.Distance,b.Distance);
                        if(!Format.Finite(lo) || !Format.Finite(hi)) { lo=0;hi=length; }
                        bad.Add(Tuple.Create(lo,hi));
                    }
                }
                if(points[points.Length-1].Distance<length) bad.Add(Tuple.Create(points[points.Length-1].Distance,length));
            }
            bool Unreliable(double start,double end)
            { return bad.Any(x=>x.Item1<end-1e-6 && x.Item2>start+1e-6 || x.Item1==x.Item2 && x.Item1>start && x.Item1<end); }
            public double Duration(double start,double end)
            {
                if(Unreliable(start,end)) return double.NaN;
                double value=At(end,false)-At(start,false);return value>0 ? value : double.NaN;
            }
            public double At(double distance,bool speed)
            {
                if(bad.Any(x=>distance>x.Item1+1e-6 && distance<x.Item2-1e-6 || x.Item1==x.Item2 && Math.Abs(distance-x.Item1)<1e-6)) return double.NaN;
                double result=double.NaN;bool found=false;
                for(int i=0;i<points.Length;i++)
                {
                    var p=points[i];
                    if(Math.Abs(p.Distance-distance)<1e-6)
                    {
                        double value=speed ? p.Speed : p.Time;
                        if(found && i>0 && Math.Abs(points[i-1].Distance-distance)<1e-6) continue;
                        if(found && !speed && Math.Abs(value-result)>.0001) return double.NaN;
                        result=value;found=true;continue;
                    }
                    if(i==0) continue;var previous=points[i-1];
                    if(previous.Distance>=distance || p.Distance<=distance || !LearningContinuity.Link(previous,p)) continue;
                    double ratio=(distance-previous.Distance)/(p.Distance-previous.Distance);
                    double v=speed ? previous.Speed+(p.Speed-previous.Speed)*ratio : previous.Time+(p.Time-previous.Time)*ratio;
                    if(found) return double.NaN;result=v;found=true;
                }
                return found && Format.Finite(result) ? result : double.NaN;
            }
        }
    }
}
