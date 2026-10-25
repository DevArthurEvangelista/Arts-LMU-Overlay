using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Arthur.LmuOverlay;

partial class Checks
{
    sealed class BlockedWheel : IWheelReader
    {
        public readonly ManualResetEvent Entered=new ManualResetEvent(false),Release=new ManualResetEvent(false),Disposed=new ManualResetEvent(false);
        public int ThreadId;
        public List<WheelAxis> Read(IntPtr owner,double now)
        { ThreadId=Thread.CurrentThread.ManagedThreadId;Entered.Set();Release.WaitOne();return new List<WheelAxis> { new WheelAxis { DeviceId="blocked-test",Value=42 } }; }
        public void Refresh() { }
        public void Dispose() { Disposed.Set(); }
    }
    sealed class CountingReader : ITelemetryReader
    {
        public int Calls;
        public Frame Read(bool filtered)
        { int n=Interlocked.Increment(ref Calls);return new Frame { Connected=true,HasTelemetry=true,TelemetryElapsed=n*.01,Throttle=.4,Brake=.2 }; }
        public void Dispose() { }
    }
    static Frame WidgetFrame(int lap,double elapsed,double start,double fuel)
    {
        var f=new Frame { Connected=true,HasTelemetry=true,Track="Widget test",Session=10,PlayerId=1,PlayerClass="LMGT3",LapNumber=lap,TelemetryElapsed=elapsed,LapStart=start,Fuel=fuel,Remaining=300,TrackLength=1000 };
        f.Drivers.Add(new Driver { Id=1,IsPlayer=true,Class="LMGT3",Laps=lap-1,BestLap=100,LastLap=101 });return f;
    }
    static void Version08(byte[] bytes)
    {
        var independent=LmuParser.Parse(bytes,false);
        Assert(independent.CurrentLap==40 && independent.LapNumber==5 && independent.MaxLaps==40,"ctypes SDK fixture independently verifies lap fields");
        Assert(Math.Abs(independent.Tyres[0].Temperature-81)<1e-8 && independent.Tyres[3].Pressure==2.1,"independent ctypes wheel layout matches parsed values");
        Assert(independent.TrackLength==7004.8 && independent.Drivers.Single(d=>d.IsPlayer).WorldX==300,"SDK world positions and circuit length are parsed by player ID");
        var b=(byte[])bytes.Clone();int t=Telem(2,0);
        Set(b,t+Layout.Telem_mLapNumber,5);Set(b,t+Layout.Telem_mLapStartET,580.0);Set(b,t+Layout.Telem_mElapsedTime,612.25);Set(b,t+Layout.Telem_mDeltaBest,-.3);
        for(int i=0;i<4;i++)
        {
            int w=t+Layout.Telem_mWheels+i*Layout.WheelSize;
            for(int j=0;j<3;j++) Set(b,w+Layout.Wheel_mTemperature+j*8,353.15+i+j);
            Set(b,w+Layout.Wheel_mPressure,180.0+i*10);
            BitConverter.GetBytes(85f).CopyTo(b,w+Layout.Wheel_mOptimalTemp);
        }
        var parsed=LmuParser.Parse(b,false);
        Assert(parsed.LapNumber==5 && parsed.CurrentLap==32.25 && parsed.DeltaBest==-.3,"lap clock uses native elapsed minus lap start");
        Assert(parsed.Tyres.Length==4 && Math.Abs(parsed.Tyres[0].Temperature-81)<1e-8 && Math.Abs(parsed.Tyres[3].Temperature-84)<1e-8,"four wheel surface averages use SDK offsets and Kelvin to Celsius");
        Assert(parsed.Tyres[0].Pressure==1.8 && parsed.Tyres[3].Pressure==2.1 && parsed.Tyres[0].Optimal==85,"SDK pressure kPa to bar and optimal is already Celsius");
        Set(b,t+Layout.Telem_mWheels+Layout.Wheel_mTemperature,0.0);
        Assert(!Format.Finite(LmuParser.Parse(b,false).Tyres[0].Temperature),"absent temperature cannot appear as minus 273 C");
        var widgets=new RaceWidgets();var f=WidgetFrame(4,340,300,50);f.CurrentLap=40;f.DeltaBest=-.25;widgets.Apply(f);
        Assert(f.Widgets.PredictedLap==99.75 && !Format.Finite(f.Widgets.FuelPerLap),"prediction uses known best; partial entry never fabricates fuel consumption");
        for(int time=341;time<=400;time++) { f=WidgetFrame(time==400 ? 5 : 4,time,time==400 ? 400 : 300,50-(time-340)*.04);widgets.Apply(f); }
        Assert(f.Widgets.FuelSamples==0,"first crossing only establishes a full-lap fuel baseline");
        for(int time=401;time<=500;time++) { f=WidgetFrame(time==500 ? 6 : 5,time,time==500 ? 500 : 400,47.6-(time-400)*.04);widgets.Apply(f); }
        Assert(f.Widgets.FuelSamples==1 && Math.Abs(f.Widgets.FuelPerLap-4)<1e-8,"full observed lap measures consumed liters");
        Assert(Math.Abs(f.Widgets.FuelLaps-10.9)<1e-8 && f.Widgets.EstimatedLaps==4,"fuel autonomy and timed estimate use data");
        for(int time=501;time<=600;time++) { f=WidgetFrame(time==600 ? 7 : 6,time,time==600 ? 600 : 500,time<550 ? 43.6-(time-500)*.04 : 70-(time-550)*.04);widgets.Apply(f); }
        Assert(f.Widgets.FuelSamples==1,"refueling invalidates that lap consumption");
        f=WidgetFrame(1,0,0,90);widgets.Apply(f);Assert(f.Widgets.FuelSamples==0,"session time rewind resets fuel history");
        f=WidgetFrame(1,1,0,90);f.LapInvalid=true;f.CurrentLap=10;f.DeltaBest=.1;widgets.Apply(f);
        Assert(!Format.Finite(f.Widgets.PredictedLap),"invalid lap cannot show a predicted valid lap");
        f=WidgetFrame(1,2,0,90);f.Drivers[0].LapDistance=200;f.Drivers[0].WorldX=10;f.Drivers[0].WorldZ=20;
        f.Drivers.Add(new Driver {Id=2,Class="HYPERCAR",LapDistance=250,WorldX=900,WorldZ=800});widgets.Apply(f);
        Assert(f.Widgets.Map.Length==1 && f.Widgets.Map[0].X==10,"map learns only real own-class positions");
        f=WidgetFrame(1,3,0,90);f.Drivers[0].InPits=true;f.Drivers[0].LapDistance=300;f.Drivers[0].WorldX=100;f.Drivers[0].WorldZ=200;widgets.Apply(f);
        Assert(f.Widgets.Map.Length==1,"pit lane cannot distort learned circuit route");
        var history=new InputHistory();for(int i=0;i<=3600;i++) history.Add(i/120.0,i%11/10.0,0);
        var plot=history.Plot(30,15);
        Assert(plot.Start==15 && plot.Samples.Count==1801 && plot.Samples[0].Time==15 && plot.Samples.Last().Time==30,"15-second history retains exact samples and endpoints");
        using(var clockWheel=new BlockedWheelResources())
        {
            var clock=Stopwatch.StartNew();var device=clockWheel.Device;var reader=new CountingReader();
            using(var wheel=new WheelPump(()=>clock.Elapsed.TotalSeconds,()=>device,null))
            using(var telemetry=new TelemetryPump(()=>clock.Elapsed.TotalSeconds,null,reader))
            {
                wheel.Start(IntPtr.Zero);telemetry.Start();Assert(device.Entered.WaitOne(2000),"fake slow driver entered on worker");
                var watch=Stopwatch.StartNew();for(int i=0;i<10000;i++) wheel.Read(clock.Elapsed.TotalSeconds);
                Assert(watch.ElapsedMilliseconds<250 && device.ThreadId!=Thread.CurrentThread.ManagedThreadId,"blocked device discovery never blocks caller/message loop");
                Assert(SpinWait.SpinUntil(()=>reader.Calls>=12,2000) && telemetry.Latest.HasTelemetry,"telemetry and pedal history keep updating while USB driver blocks");
                device.Release.Set();Assert(SpinWait.SpinUntil(()=>wheel.Read(clock.Elapsed.TotalSeconds).Count==1,2000),"wheel publishes again after slow discovery completes");
                Assert(wheel.Read(clock.Elapsed.TotalSeconds+1).Count==0,"expired physical snapshot goes to game fallback rather than frozen angle");
            }
            Assert(device.Disposed.WaitOne(2000),"device is disposed on its owner worker");
        }
    }
    sealed class BlockedWheelResources : IDisposable
    {
        public readonly BlockedWheel Device=new BlockedWheel();
        public void Dispose() { Device.Release.Set();Device.Disposed.WaitOne(2000);Device.Entered.Dispose();Device.Release.Dispose();Device.Disposed.Dispose(); }
    }
    static void Render08(string directory)
    {
        var f=PreviewData.Create(315);f.LapNumber=15;f.CurrentLap=86.253;f.DeltaBest=-.283;f.PlayerId=f.Drivers.Single(d=>d.IsPlayer).Id;
        f.Widgets=new RaceWidgetState { FuelPerLap=3.7,FuelLaps=11.5,FuelSamples=5,FuelNeeded=55.5,FuelToAdd=12.9,PredictedLap=230.873,MapCoverage=1 };
        f.Tyres=Enumerable.Range(0,4).Select(i=>new TyreReading { Temperature=87+i*2,Pressure=1.79+i*.02,Optimal=90 }).ToArray();
        // Test-only circuit with varied radii; never shipped as a live map.
        f.TrackLength=5000;f.Widgets.Map=Enumerable.Range(0,720).Select(i=>{
            double a=i*Math.PI*2/720,r=900+210*Math.Sin(a*3)+90*Math.Cos(a*5);
            return new TrackPoint { Bin=i,X=Math.Cos(a)*r,Z=Math.Sin(a)*r*.72 }; }).ToArray();
        int car=0;foreach(var d in f.Drivers.Where(d=>d.Class==f.PlayerClass))
        { var pos=f.Widgets.Map[(car++*31)%720];d.WorldX=pos.X;d.WorldZ=pos.Z; }
        var history=new InputHistory();for(int i=0;i<=1800;i++) { double x=i/120.0;history.Add(x,Math.Pow(Math.Max(0,Math.Sin(x*.7)),.6),Math.Max(0,Math.Sin(x*1.4-1))* .86); }
        var options=new ViewOptions();using(var p=new Painter())
        {
            Action<string,int,int,Action<Graphics>> save=(name,width,height,draw)=>
            { using(var b=new Bitmap(width,height,PixelFormat.Format32bppPArgb)) using(var g=Graphics.FromImage(b)) { g.Clear(Color.Transparent);draw(g);b.Save(Path.Combine(directory,name),ImageFormat.Png); } };
            save("pedais-1x.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,f,history,15,options,false));
            save("tabela-1x.png",Painter.TableWidth,Painter.TableHeight(f),g=>p.Standings(g,f,options,false));
            foreach(PanelKind kind in new[]{PanelKind.LapTimer,PanelKind.Tyres,PanelKind.Fuel,PanelKind.TrackMap})
            {
                var size=Painter.WidgetSize(kind);save(kind+"-1x.png",size.Width,size.Height,g=>p.Widget(g,kind,f,options,false));
                save(kind+"-vazio.png",size.Width,size.Height,g=>p.Widget(g,kind,new Frame(),options,false));
                var stale=f.Copy();stale.Stale=true;stale.HasTelemetry=false;
                save(kind+"-pausado.png",size.Width,size.Height,g=>p.Widget(g,kind,stale,options,false));
            }
            save("Previa-Arts-LMU-Overlay-0.8.png",1676,824,g=>
            {
                g.Clear(Color.FromArgb(42,48,54));
                using(var font=new Font(Theme.BodyFont,18,FontStyle.Bold,GraphicsUnit.Pixel))
                using(var brush=new SolidBrush(Theme.Text)) g.DrawString("ARTS LMU OVERLAY 0.8  ·  PRÉVIA DO APP COM DADOS DE TESTE",font,brush,22,21);
                var st=g.Save();g.TranslateTransform(22,72);p.Standings(g,f,options,false);g.Restore(st);
                st=g.Save();g.TranslateTransform(22,486);p.Pedals(g,f,history,15,options,false);g.Restore(st);
                PanelKind[] kinds={PanelKind.LapTimer,PanelKind.Tyres,PanelKind.Fuel,PanelKind.TrackMap};
                for(int i=0;i<4;i++) { st=g.Save();g.TranslateTransform(954+(i%2)*342,72+(i/2)*302);p.Widget(g,kinds[i],f,options,false);g.Restore(st); }
                using(var font=new Font(Theme.BodyFont,14,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var brush=new SolidBrush(Theme.Muted)) g.DrawString("Tamanho real (1×). Painéis adicionais opcionais; cada um pode ser posicionado separadamente.",font,brush,22,791);
            });
        }
    }
}
