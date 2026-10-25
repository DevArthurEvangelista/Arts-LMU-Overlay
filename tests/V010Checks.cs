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
    static LearningFrame LearningFrameAt(double time,int mode=0,bool opponent=true)
    {
        var cars=new List<LearningCar>();
        for(int i=0;i<(opponent ? 2 : 1);i++)
        {
            double duration=i==0 ? 20 : 22;int laps=(int)Math.Floor(time/duration+.0000001);double into=time-laps*duration;
            double d=Math.Max(0,into/duration*1000),angle=d/1000*Math.PI*2;
            cars.Add(new LearningCar { Id=i+1,Identity=(i+1)+"/"+(i==0 ? "Arthur" : "Adversário"),Name=i==0 ? "Arthur" : "Adversário",Car="GT3",Class="LMGT3",Tyres="M",
                IsPlayer=i==0,CanRecord=true,Laps=laps,Start=100+laps*duration,Distance=d,X=250*Math.Sin(angle),Z=140*Math.Cos(angle),Speed=1000/duration*3.6,
                CountLap=2,Sector1=into>=7 ? 7 : -1,Sector2=into>=13 ? 13 : -1,LastLap=laps>0 ? duration : -1,LastSector1=laps>0 ? 7 : -1,LastSector2=laps>0 ? 13 : -1,
                Fuel=40,Throttle=i==0 ? .75 : double.NaN,Brake=i==0 ? 0 : double.NaN });
        }
        return new LearningFrame { Key="session/"+mode,Track="Circuito de testes",Server=opponent ? "Lobby" : "",Player="Arthur",Mode=mode,Length=1000,Time=100+time,Active=true,Utc=new DateTime(2026,9,27,10,0,0,DateTimeKind.Utc).AddSeconds(time),Cars=cars.ToArray() };
    }
    static List<LearningLap> Recorded(double from,double to,Action<LearningFrame> adjust=null)
    {
        var result=new List<LearningLap>();var recorder=new LearningRecorder(s=>{},result.Add);
        for(int i=(int)(from*10);i<=(int)(to*10);i++) { var frame=LearningFrameAt(i/10.0);if(adjust!=null) adjust(frame);recorder.Accept(frame); }
        recorder.Flush();return result;
    }
    static void Version010(byte[] bytes)
    {
        var raw=(byte[])bytes.Clone();
        Set(raw,Car(2,Layout.Car_mLapStartET),123.4);Set(raw,Car(2,Layout.Car_mCurSector1),31.25);Set(raw,Car(2,Layout.Car_mCurSector2),64.7);
        Set(raw,Car(2,Layout.Car_mLastSector1),30.1);Set(raw,Car(2,Layout.Car_mLastSector2),62.2);
        raw[Car(2,Layout.Car_mCountLapFlag)]=1;raw[Car(2,Layout.Car_mSector)]=0;raw[Car(2,Layout.Car_mInGarageStall)]=1;
        Set(raw,Car(2,Layout.Car_mLocalVel),10.0);Set(raw,Car(2,Layout.Car_mLocalVel)+8,0.0);Set(raw,Car(2,Layout.Car_mLocalVel)+16,0.0);
        var native=LmuParser.Parse(raw,false);var driver=native.Drivers.Single(d=>d.IsPlayer);
        Assert(driver.LapStart==123.4 && driver.CurrentSector1==31.25 && driver.CurrentSector2==64.7,"learning native SDK current cumulative sectors and lap start");
        Assert(driver.LastSector1==30.1 && driver.LastSector2==62.2 && driver.CountLapFlag==1 && driver.InGarage && driver.Sector==0,"learning native SDK last sectors, validity, garage");
        Assert(Math.Abs(driver.Speed-36)<.0001,"rival speed comes from scoring velocity, not local car telemetry");
        var snapshot=LearningFrame.Capture(native);driver.Name="Mutated on UI";driver.LapStart=500;
        Assert(snapshot.Cars.Single(c=>c.IsPlayer).Start==123.4 && snapshot.Cars.Single(c=>c.IsPlayer).Name!="Mutated on UI","learning snapshot isolated from shared scoring/UI annotations");
        Assert(snapshot.Cars.Where(c=>!c.IsPlayer).All(c=>double.IsNaN(c.Throttle) && double.IsNaN(c.Brake)),"opponent pedals are never captured");
        native.InRealtime=false;native.Stale=true;native.ScoringStale=false;
        snapshot=LearningFrame.Capture(native);
        Assert(snapshot.Active && !snapshot.Cars.Single(c=>c.IsPlayer).CanRecord && snapshot.Cars.Any(c=>!c.IsPlayer && c.CanRecord),"live scoring records opponents while local player is in garage");
        native.ScoringStale=true;Assert(!LearningFrame.Capture(native).Active,"frozen scoring does not create recordings");
        native.ScoringStale=false;native.Demo=true;Assert(!LearningFrame.Capture(native).Active,"demo data excluded from recorder");
        native.Demo=false;var fresh=new DataFreshness();native.Stale=false;native.ScoringEvent=native.TelemetryEvent=0;fresh.Apply(native,1);
        native.Elapsed+=3;fresh.Apply(native,4);
        Assert(native.Stale && !native.ScoringStale,"local telemetry pause does not declare opponent scoring stale");

        var recorded=Recorded(0,45);var a=recorded.First(l=>l.Info.IsPlayer && l.Info.Completed);var b=recorded.First(l=>!l.Info.IsPlayer && l.Info.Completed);
        Assert(a.Info.Valid && a.Info.Time==20 && a.Info.StartObserved && !a.Info.HasGaps,"complete valid player lap recorded with official timing");
        Assert(a.Info.Sector1==7 && a.Info.Sector2==6 && a.Info.Sector3==7,"official cumulative sectors converted to independent S1/S2/S3");
        Assert(b.Info.Valid && b.Info.Time==22 && b.Info.Sector3==9,"opponent lap sectors do not reuse local lap timing");
        Assert(recorded.Count(l=>l.Info.Completed)==4 && recorded.Count(l=>!l.Info.Completed)==2,"two cars record every lap and flush unfinished laps");
        Assert(a.Samples.First().Distance==0 && a.Samples.Last().Distance==1000 && a.Samples.Last().Time==20,"finish passage belongs to the prior lap");
        Assert(a.Samples.Any(p=>p.Throttle==.75) && b.Samples.All(p=>double.IsNaN(p.Throttle)),"own samples and opponent timing stay separated");
        var partial=Recorded(5,22).First(l=>l.Info.IsPlayer && l.Info.Completed);
        Assert(!partial.Info.StartObserved && partial.Info.Time==20 && partial.Info.Valid,"joining halfway keeps official lap total and labels partial coverage");
        var invalid=Recorded(0,22,f=>{if(f.Time<120) { f.Cars[0].Invalid=true;f.Cars[0].CountLap=1; }}).First(l=>l.Info.IsPlayer && l.Info.Completed);
        Assert(invalid.Info.Invalid && !invalid.Info.Valid && invalid.Info.State.StartsWith("Invalidada"),"invalidated laps remain in history and are excluded from valid references");
        var noOfficial=Recorded(0,21,f=>{f.Cars[0].CountLap=1;if(f.Time>=120) f.Cars[0].LastLap=19;}).First(l=>l.Info.IsPlayer && l.Info.Completed);
        Assert(!noOfficial.Info.OfficialTime && noOfficial.Info.Time==20 && noOfficial.Info.Sector3<0,"invalid finish estimates total and never borrows an earlier last lap/sector3");
        var pit=Recorded(0,22,f=>{if(f.Time<104) f.Cars[0].InPits=true;}).First(l=>l.Info.IsPlayer && l.Info.Completed);
        Assert(pit.Info.PitLap && !pit.Info.Valid,"pit and out laps remain visible but are not automatic best");
        var best=LearningComparison.Best(new[]{invalid.Info,pit.Info,a.Info});Assert(best==a.Info,"automatic best ignores invalid and pit laps");
        Assert(LearningComparison.Best(new[]{invalid.Info,pit.Info})==null,"no valid reference does not silently choose invalid data");
        var gap=Recorded(0,21,f=>{if(f.Time>=105 && f.Time<=108) f.Active=false;}).First(l=>l.Info.IsPlayer && l.Info.Completed);
        Assert(gap.Info.HasGaps && gap.Info.Time==20,"paused acquisition preserves official lap but marks gaps");
        var queueGap=Recorded(0,21,f=>{if(f.Time==108) f.Dropped=true;}).First(l=>l.Info.IsPlayer && l.Info.Completed);
        Assert(queueGap.Info.HasGaps,"queue loss explicitly breaks passage continuity");
        var fallback=Recorded(0,21,f=>{f.Cars[0].Sector1=f.Cars[0].Sector2=-1;}).First(l=>l.Info.IsPlayer && l.Info.Completed);
        Assert(fallback.Info.Sector1==7 && fallback.Info.Sector2==6 && fallback.Info.Sector3==7,"matching last-lap sectors recover missed sector notifications");

        for(int mode=0;mode<=13;mode++)
        {
            var result=new List<LearningLap>();var rec=new LearningRecorder(s=>{},result.Add);
            for(int i=0;i<=201;i++) rec.Accept(LearningFrameAt(i/10.0,mode,false));rec.Flush();
            Assert(result.Any(l=>l.Info.Valid && l.Info.IsPlayer),"recorder supports native session "+mode+" including solo and AI");
        }
        var sessions=new List<LearningSession>();var resetLaps=new List<LearningLap>();var reset=new LearningRecorder(sessions.Add,resetLaps.Add);
        for(int i=0;i<50;i++) reset.Accept(LearningFrameAt(i/10.0));reset.Accept(LearningFrameAt(0));reset.Accept(LearningFrameAt(.1));reset.Flush();
        Assert(sessions.Count==2 && resetLaps.All(l=>!l.Info.Completed),"clock regression separates sessions and never creates a negative lap");
        sessions.Clear();resetLaps.Clear();reset=new LearningRecorder(sessions.Add,resetLaps.Add);
        for(int i=0;i<50;i++) reset.Accept(LearningFrameAt(i/10.0));reset.Accept(LearningFrameAt(5,5));reset.Accept(LearningFrameAt(5.1,5));reset.Flush();
        Assert(sessions.Count==2 && sessions[0].Id!=sessions[1].Id,"practice-to-qualifying creates independent saved sessions");
        var reused=Recorded(0,22,f=>{if(f.Time>=110) { f.Cars[1].Identity="new driver";f.Cars[1].Name="New driver"; }});
        Assert(reused.Any(l=>l.Info.Driver=="Adversário" && !l.Info.Completed) && reused.Any(l=>l.Info.Driver=="New driver" && !l.Info.StartObserved),"reused remote slot does not merge two drivers");
        var garage=Recorded(0,22,f=>{if(f.Time>=110) f.Cars[0].CanRecord=false;});
        Assert(garage.Any(l=>l.Info.IsPlayer && !l.Info.Completed) && garage.Any(l=>!l.Info.IsPlayer && l.Info.Completed),"garage return flushes local out-lap without stopping other cars");
        var duplicates=new List<LearningLap>();var dedupe=new LearningRecorder(s=>{},duplicates.Add);
        for(int i=0;i<=201;i++) { var f=LearningFrameAt(i/10.0);dedupe.Accept(f);dedupe.Accept(f); }
        dedupe.Flush();Assert(duplicates.Count(l=>l.Info.IsPlayer && l.Info.Completed)==1,"repeated native frames do not duplicate laps");

        // Compare two laps sharing the same saved session, not the simulation clock.
        var compare=LearningComparison.Build(a,b,200);
        Assert(compare.Available==5 && compare.Segments.All(s=>Math.Abs(s.Delta+.4)<.0001),"distance passage comparison yields -0.4 s per 200 m despite different lap speeds");
        Assert(Math.Abs(compare.Delta.Last()+2)<.0001 && Math.Abs(compare.Segments.Sum(s=>s.Delta)+2)<.0001,"cumulative finish delta and segment sum agree with lap totals");
        var self=LearningComparison.Build(a,a,200);Assert(self.Segments.All(s=>Math.Abs(s.Delta)<1e-10),"same-lap reference yields zero");
        partial.Session=a.Session;partial.Info.SessionId=a.Session.Id;
        var incomplete=LearningComparison.Build(partial,b,200);
        Assert(!incomplete.Segments[0].Available && !incomplete.Segments[1].Available && incomplete.Segments[2].Available,"missing lap start is not extrapolated into an estimated split");
        gap.Session=a.Session;gap.Info.SessionId=a.Session.Id;var cgap=LearningComparison.Build(gap,b,200);
        Assert(!cgap.Segments[1].Available && cgap.Segments[0].Available && cgap.Segments[3].Available,"interior gap invalidates affected microsector even with available endpoints");
        var wrong=new LearningLap { Session=new LearningSession { Id="different",Track=a.Session.Track,Length=1000 },Info=b.Info,Samples=b.Samples };
        bool refused=false;try { LearningComparison.Build(a,wrong,200); } catch(InvalidOperationException) { refused=true; }Assert(refused,"different sessions cannot accidentally compare distinct layouts");
        var reversed=new LearningLap { Session=a.Session,Info=a.Info,Samples=a.Samples.Select(p=>new LearningSample { Time=p.Time,Distance=p.Distance,X=p.X,Z=p.Z,Speed=p.Speed }).ToArray() };
        reversed.Samples[61].Distance=250;
        Assert(!LearningComparison.Build(reversed,b,200).Segments[1].Available,"reversing over a timing boundary is not treated as a second valid crossing");
        var stopped=new LearningLap { Session=a.Session,Info=a.Info,Samples=new[]{new LearningSample { Distance=0,Time=0 },new LearningSample {Distance=0,Time=.1}}.Concat(a.Samples.Skip(1).Select(p=>new LearningSample { Distance=p.Distance,Time=p.Time+.1 })).ToArray() };
        Assert(LearningComparison.Build(stopped,b,200).Segments[0].Available,"brief stationary start uses initial crossing without false ambiguity");

        string directory=Path.Combine(Path.GetTempPath(),"arts-learning-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var store=new LearningStore(directory);store.SaveLap(a);store.SaveLap(b);store.SaveLap(a);
            string warning;var storedSessions=store.Sessions(out warning);var infos=store.Laps(a.Session.Id,out warning);
            Assert(storedSessions.Length==1 && infos.Length==2,"atomic storage is idempotent and groups both drivers under one session");
            var loaded=new LearningStore(directory).Load(b.Session.Id,b.Info.Id);
            Assert(loaded.Info.Driver==b.Info.Driver && loaded.Info.Time==22 && double.IsNaN(loaded.Samples[0].Throttle),"restart round-trip preserves opponent names, timing, missing pedals and samples");
            Assert(LearningComparison.Build(store.Load(a.Session.Id,a.Info.Id),loaded,200).Available==5,"saved laps can be compared after restart");
            File.WriteAllText(Path.Combine(directory,a.Session.Id,"garbage.xml"),"broken");
            Assert(store.Laps(a.Session.Id,out warning).Length==2 && warning.Length>0,"one damaged lap index does not hide healthy recordings");
            Assert(File.Exists(Path.Combine(directory,a.Session.Id,"garbage.xml")),"corrupt files are preserved for recovery");
            string original=Path.Combine(directory,a.Session.Id,a.Info.Id+".xml"),bad=Path.Combine(directory,a.Session.Id,Guid.NewGuid().ToString("N")+".xml");
            File.Copy(original,bad);Assert(store.Laps(a.Session.Id,out warning).Length==2,"mismatched immutable index identity is skipped without losing healthy laps");
            Reject(()=>store.Load("../escape",a.Info.Id),"recording IDs cannot traverse directories");
            var many=new LearningLap { Session=a.Session,Info=new LearningLapInfo { Id=Guid.NewGuid().ToString("N"),SessionId=a.Session.Id,Driver="Long lap" },Samples=Enumerable.Range(0,10000).Select(i=>new LearningSample { Distance=i/10.0,Time=i/25.0,X=i,Z=i,Speed=90 }).ToArray() };
            store.SaveLap(many);Assert(store.Load(a.Session.Id,many.Info.Id).Samples.Length==10000,"long/high-rate laps exceed default serializer quota and still round-trip");
            var corrupt=Path.Combine(directory,a.Session.Id,b.Info.Id+".lap.gz");File.WriteAllText(corrupt,"not gzip");
            bool reported=false;try { store.Load(a.Session.Id,b.Info.Id); } catch(Exception e) { reported=LearningStore.Recoverable(e); }
            Assert(reported,"damaged compressed payload produces a recoverable read error");
        }
        finally { Directory.Delete(directory,true); }
        var blocked=new BlockingLearningWriter();var background=new LearningPump(blocked,null);
        background.Offer(LearningFrameAt(0));Assert(blocked.Entered.WaitOne(2000),"learning persistence runs on its own worker");
        var watch=Stopwatch.StartNew();for(int i=1;i<=500;i++) background.Offer(LearningFrameAt(i/10.0));watch.Stop();
        Assert(watch.ElapsedMilliseconds<1000 && background.DroppedFrames>0,"blocked disk cannot block producer and queue stays bounded");
        blocked.Release.Set();background.Dispose();Assert(blocked.Saved.Count>0,"writer drains and flushes incomplete laps on shutdown");
        var failing=new TransientLearningWriter();using(var recording=new LearningPump(failing,null))
        {
            for(int i=0;i<=205;i++) { recording.Offer(LearningFrameAt(i/10.0,0,false));if(i%50==0) Thread.Sleep(3); }
        }
        Assert(failing.Laps.Any(l=>l.Info.Completed) && failing.Attempts>1,"transient disk failure retries the same immutable lap instead of losing it");
        var disk=new BlockingLearningWriter();var liveClock=Stopwatch.StartNew();var source=new LearningNativeReader(bytes);
        using(var recording=new LearningPump(disk,null)) using(var telemetry=new TelemetryPump(()=>liveClock.Elapsed.TotalSeconds,null,source))
        {
            telemetry.LearningSample=recording.Accept;telemetry.Start();
            Assert(disk.Entered.WaitOne(2000),"actual telemetry callback starts independent persistence");
            int before=Volatile.Read(ref source.Calls);
            Assert(SpinWait.SpinUntil(()=>Volatile.Read(ref source.Calls)>=before+25,2000) && telemetry.Latest.Throttle==.73 && telemetry.Latest.Brake==.19,"actual pedal reader keeps polling analog values while disk writer is blocked");
            disk.Release.Set();
        }
    }
    sealed class BlockingLearningWriter : ILearningWriter
    {
        public readonly ManualResetEvent Entered=new ManualResetEvent(false),Release=new ManualResetEvent(false);
        public readonly List<LearningLap> Saved=new List<LearningLap>();
        public void SaveSession(LearningSession s) { Entered.Set();Release.WaitOne(3000); }
        public void SaveLap(LearningLap lap) { Saved.Add(lap); }
    }
    sealed class TransientLearningWriter : ILearningWriter
    {
        public readonly List<LearningLap> Laps=new List<LearningLap>();public int Attempts;
        public void SaveSession(LearningSession s) { }
        public void SaveLap(LearningLap lap) { if(++Attempts==1) throw new IOException("Injected disk failure");Laps.Add(lap); }
    }
    sealed class LearningNativeReader : ITelemetryReader
    {
        readonly byte[] bytes;public int Calls;
        public LearningNativeReader(byte[] data) { bytes=data; }
        public Frame Read(bool filtered)
        { var f=LmuParser.Parse(bytes,false);int count=Interlocked.Increment(ref Calls);f.Elapsed+=count*.01;f.TelemetryElapsed+=count*.01;return f; }
        public void Dispose() { }
    }
    static void RenderLearning(string directory)
    {
        Directory.CreateDirectory(directory);var laps=Recorded(0,25);var a=laps.First(l=>l.Info.IsPlayer && l.Info.Completed);var b=laps.First(l=>!l.Info.IsPlayer && l.Info.Completed);
        var comparison=LearningComparison.Build(a,b,100);
        foreach(int width in new[]{1200,960})
        {
            using(var bitmap=new Bitmap(width,460)) using(var g=Graphics.FromImage(bitmap))
            {
                g.Clear(Theme.Background);LearningPainter.Map(g,new Rectangle(0,0,width*43/100-12,414),comparison,3);
                LearningPainter.Charts(g,new Rectangle(width*43/100,0,width*57/100,414),comparison,3);
                using(var font=new Font(Theme.BodyFont,13,FontStyle.Regular,GraphicsUnit.Pixel)) using(var brush=new SolidBrush(Theme.Muted))
                    g.DrawString("Verificação do desenho real do módulo • dados artificiais de teste • não é captura da janela ou do LMU",font,brush,12,435);
                bitmap.Save(Path.Combine(directory,"aprendizado-"+width+".png"),ImageFormat.Png);
            }
        }
        using(var bitmap=new Bitmap(960,400)) using(var g=Graphics.FromImage(bitmap))
        { LearningPainter.Map(g,new Rectangle(0,0,410,400),null,-1);LearningPainter.Charts(g,new Rectangle(420,0,540,400),null,-1);bitmap.Save(Path.Combine(directory,"aprendizado-vazio.png"),ImageFormat.Png); }
    }
}
