using System;
using System.Collections.Generic;

namespace Arthur.LmuOverlay
{
    public sealed class PlotWindow
    {
        public double Start,Seconds,Head;
        public bool Filling;
        public List<InputSample> Samples;
        public double X(double time) { return Format.Clamp((time-Start)/Seconds,0,1); }
    }
    public struct InputSample { public double Time,Throttle,Brake; }
    public sealed class InputHistory
    {
        readonly object gate=new object();
        readonly InputSample[] samples=new InputSample[4096];
        int cursor,count;
        double origin=double.NaN;
        public void Clear() { lock(gate) { cursor=count=0;origin=double.NaN; } }
        public void Add(double time,double throttle,double brake)
        {
            lock(gate)
            {
                if(double.IsNaN(origin)) origin=time;
                samples[cursor]=new InputSample { Time=time,Throttle=throttle,Brake=brake };
                cursor=(cursor+1)%samples.Length;count=Math.Min(count+1,samples.Length);
            }
        }
        public List<InputSample> Window(double now,double seconds) { return Plot(now,seconds).Samples; }
        public PlotWindow Plot(double now,double seconds)
        {
            lock(gate)
            {
                seconds=Math.Max(.1,seconds);
                var p=new PlotWindow { Start=double.IsNaN(origin) ? now : Math.Max(origin,now-seconds),Seconds=seconds,
                    Filling=double.IsNaN(origin) || now-origin<seconds,Samples=new List<InputSample>() };
                for(int i=0;i<count;i++)
                {
                    var s=samples[(cursor-count+i+samples.Length)%samples.Length];
                    if(s.Time>=p.Start && s.Time<=now) p.Samples.Add(s);
                }
                p.Head=p.Samples.Count==0 ? 0 : p.X(p.Samples[p.Samples.Count-1].Time);
                return p;
            }
        }
    }
    public sealed class InputCapture
    {
        public readonly InputHistory History=new InputHistory();
        string key="";
        double elapsed=-1,missingSince=-1;
        uint eventId;
        double throttle,brake,steering;
        bool hasSample;
        public void Accept(Frame f,double now)
        {
            if(!f.HasTelemetry || f.Stale)
            {
                if(missingSince<0) missingSince=now;
                if(!f.Connected || now-missingSince>1) { History.Clear();key="";hasSample=false; }
                return;
            }
            missingSince=-1;
            string next=f.SessionKey+"/"+f.PlayerId;
            if(next!=key || f.TelemetryElapsed<elapsed-.1)
            { History.Clear();hasSample=false;key=next; }
            if(!hasSample || f.TelemetryElapsed!=elapsed || f.TelemetryEvent!=eventId || f.Throttle!=throttle || f.Brake!=brake || f.Steering!=steering)
            { History.Add(now,f.Throttle,f.Brake);hasSample=true; }
            elapsed=f.TelemetryElapsed;eventId=f.TelemetryEvent;throttle=f.Throttle;brake=f.Brake;steering=f.Steering;
        }
    }
    // Short, time-based display easing. The history keeps the exact acquired pedal samples.
    public sealed class InputMotion
    {
        string key="";
        double throttle,brake,steering,last;
        bool ready;
        public Frame Step(Frame source,double now,bool smooth)
        {
            Frame f=source.Copy();string next=f.SessionKey+"/"+f.PlayerId;
            if(!f.HasTelemetry || f.Stale) { ready=false;return f; }
            if(!ready || key!=next || !smooth || now-last>.25 || now<last)
            { throttle=f.Throttle;brake=f.Brake;steering=f.Steering;ready=true;key=next; }
            else
            {
                double a=1-Math.Exp(-Math.Max(0,now-last)/.035);
                throttle+=a*(f.Throttle-throttle);brake+=a*(f.Brake-brake);
                steering=Format.Finite(f.Steering) && Format.Finite(steering) ? steering+a*(f.Steering-steering) : f.Steering;
                if(Math.Abs(throttle-f.Throttle)<.001) throttle=f.Throttle;
                if(Math.Abs(brake-f.Brake)<.001) brake=f.Brake;
            }
            last=now;f.Throttle=throttle;f.Brake=brake;f.Steering=steering;return f;
        }
    }
    public sealed class PresentationRetry
    {
        public int Failures { get; private set; }
        public double RetryAt { get; private set; }
        public bool Ready(double now) { return now>=RetryAt; }
        public void Failed(double now) { Failures++;RetryAt=now+Math.Min(2,.2*Math.Pow(2,Math.Min(Failures-1,4))); }
        public void Succeeded() { Failures=0;RetryAt=0; }
    }
}
