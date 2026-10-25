using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Arthur.LmuOverlay;

partial class Checks
{
    static int count;
    static void Assert(bool ok,string message) { count++;if(!ok) throw new Exception("FAIL: "+message); }
    static void Reject(Action test,string message)
    { bool rejected=false;try { test(); } catch(InvalidDataException) { rejected=true; } Assert(rejected,message); }
    static void Set(byte[] b,int o,int n) { BitConverter.GetBytes(n).CopyTo(b,o); }
    static void Set(byte[] b,int o,double n) { BitConverter.GetBytes(n).CopyTo(b,o); }
    static void Name(byte[] b,int o,string n) { Array.Clear(b,o,32);Encoding.UTF8.GetBytes(n).CopyTo(b,o); }
    static int Car(int i,int field) { return Layout.ScoringCars+i*Layout.ScoringCarSize+field; }
    static int Telem(int i,int field) { return Layout.TelemetryCars+i*Layout.TelemetryCarSize+field; }
    static Frame Race(double et,int phase,int newPosition=13)
    { var f=PreviewData.Create(et,newPosition); f.Demo=false;f.Phase=phase;return f; }
    static void Main(string[] args)
    {
        if(args.Length>1 && args[1]=="--worker-telemetry") { RecoverTelemetry(File.ReadAllBytes(args[0]));return; }
        if(args.Length>1 && args[1]=="--worker-countries") { RecoverCountries(File.ReadAllBytes(args[0]));return; }
        if(args.Length>1 && args[0]=="--export-icon") { ExportIcon(args[1]);return; }
        byte[] bytes=File.ReadAllBytes(args[0]);var f=LmuParser.Parse(bytes,false);
        Assert(f.Connected && f.HasTelemetry && !f.Demo,"native fixture");
        Assert(f.GameVersion==10050 && f.Track=="Spa-Francorchamps","metadata");
        Assert(f.Gear==4 && f.Rpm==8055 && f.MaxRpm==9000,"ID matching despite mismatched array order/index");
        Assert(Math.Abs(f.Throttle-.73)<1e-10 && Math.Abs(f.Brake-.19)<1e-10,"raw pedal inputs");
        Assert(f.Fuel==42 && f.Capacity==100 && Math.Abs(f.Speed-180)<1e-10,"fuel and 3D speed");
        Assert(f.Drivers.Count==3 && f.Drivers[0].Position==1,"scoring sorted");
        Assert(f.Drivers[1].Name=="Arthur Gabriel" && f.Drivers[1].IsPlayer && f.Drivers[1].ClassPosition==1,"player class position");
        Assert(f.Drivers[2].Name=="Nicolás Silva","UTF-8 name");
        Assert(f.Drivers[2].InPits && f.Drivers[2].PitStops==2,"pit state");
        Assert(LmuParser.Parse(bytes,true).Throttle==.65,"filtered inputs distinct");
        Assert(f.MaxGears==6 && f.SteeringRange==540 && Math.Abs(Instruments.SteeringAngle(f,0)+54)<1e-9,"SDK physical steering range and left sign");
        Reject(()=>LmuParser.Parse(new byte[12],false),"short buffer");
        var b=(byte[])bytes.Clone();Set(b,Layout.Scoring+Layout.ScoringInfo_mNumVehicles,105);Reject(()=>LmuParser.Parse(b,false),"vehicle overflow");
        b=(byte[])bytes.Clone();b[Layout.Telemetry+Layout.Telemetry_activeVehicles]=255;Reject(()=>LmuParser.Parse(b,false),"telemetry overflow");
        b=(byte[])bytes.Clone();Set(b,Telem(2,Layout.Telem_mEngineRPM),double.NaN);Reject(()=>LmuParser.Parse(b,false),"non-finite rpm");
        b=(byte[])bytes.Clone();Set(b,Telem(2,Layout.Telem_mUnfilteredBrake),1.6);Reject(()=>LmuParser.Parse(b,false),"invalid pedal");
        b=(byte[])bytes.Clone();Set(b,Car(2,Layout.Car_mID),999);Assert(!LmuParser.Parse(b,false).HasTelemetry,"missing player ID never selects another car");
        b=(byte[])bytes.Clone();Set(b,Layout.Generic_gameVersion,0);Assert(!LmuParser.Parse(b,false).Connected,"empty map");
        foreach(int session in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13})
        {
            b=(byte[])bytes.Clone();b[Layout.AppLocation]=0;b[Layout.Telemetry+Layout.Telemetry_playerHasVehicle]=0;
            Set(b,Layout.Scoring+Layout.ScoringInfo_mSession,session);
            f=LmuParser.Parse(b,false);
            Assert(f.HasTelemetry && f.Drivers.Count==3 && f.Drivers.Single(d=>d.IsPlayer).Id==202 && f.Throttle==.73,"session "+session+" optional app/player flags zero with bots");
        }
        b=(byte[])bytes.Clone();b[Layout.AppLocation]=0;b[Layout.Telemetry+Layout.Telemetry_playerHasVehicle]=0;
        Array.Copy(b,Car(2,0),b,Car(0,0),Layout.ScoringCarSize);Set(b,Layout.Scoring+Layout.ScoringInfo_mNumVehicles,1);
        Name(b,Car(0,Layout.Car_mDriverName),"Jogador Solo");Name(b,Layout.Scoring+Layout.ScoringInfo_mPlayerName,"Jogador Solo");
        f=LmuParser.Parse(b,false);Assert(f.HasTelemetry && f.Drivers.Count==1 && f.Drivers[0].Name=="Jogador Solo","solo and dynamic local user name");
        b=(byte[])bytes.Clone();b[Layout.Telemetry+Layout.Telemetry_playerHasVehicle]=0;
        for(int i=0;i<3;i++) { b[Car(i,Layout.Car_mIsPlayer)]=0;b[Car(i,Layout.Car_mControl)]=2; }
        b[Car(2,Layout.Car_mControl)]=0;f=LmuParser.Parse(b,false);Assert(f.HasTelemetry && f.Drivers.Single(d=>d.IsPlayer).Id==202,"online fallback to local control");
        b[Car(2,Layout.Car_mControl)]=2;f=LmuParser.Parse(b,false);Assert(f.HasTelemetry && f.PlayerIdentification.Contains("Nome"),"unique exact local name fallback");
        Name(b,Layout.Scoring+Layout.ScoringInfo_mPlayerName,"");f=LmuParser.Parse(b,false);
        Assert(!f.HasTelemetry && f.Drivers.Count==3 && !f.Drivers.Any(d=>d.IsPlayer),"actual spectator has no fabricated player");
        b[Layout.Telemetry+Layout.Telemetry_playerHasVehicle]=1;b[Layout.Telemetry+Layout.Telemetry_playerVehicleIdx]=2;
        Assert(LmuParser.Parse(b,false).Drivers.Single(d=>d.IsPlayer).Id==202,"explicit game index as last fallback");
        b=(byte[])bytes.Clone();b[Layout.Telemetry+Layout.Telemetry_activeVehicles]=0;
        Assert(LmuParser.Parse(b,false).HasTelemetry,"missing optional count scans fresh matching slot");
        Set(b,Telem(2,Layout.Telem_mElapsedTime),100.0);Assert(!LmuParser.Parse(b,false).HasTelemetry,"full-buffer scan rejects stale slots");
        b=(byte[])bytes.Clone();Set(b,Layout.Scoring+Layout.ScoringInfo_mNumVehicles,0);b[Layout.Telemetry+Layout.Telemetry_activeVehicles]=0;b[Layout.Telemetry+Layout.Telemetry_playerHasVehicle]=0;
        Assert(!LmuParser.Parse(b,false).HasTelemetry && LmuParser.Parse(b,false).Drivers.Count==0,"empty lobby clears without demo");
        Assert(Format.Lap(59.9996)=="1:00.000" && Format.Lap(-1)=="—","lap rounding/missing");
        Assert(Format.Gap(new Driver {Position=5,LapsBehind=2})=="+2 V","lap deficit");
        Assert(Format.Gap(new Driver {Position=2,Gap=-1})=="—","missing gap");
        Assert(Format.Gap(new Driver {Position=3,FinishStatus=2})=="DNF","retired driver");
        var h=new InputHistory();for(int i=0;i<2000;i++) h.Add(i/30.0,.4,.2);
        var window=h.Window(1999/30.0,8);Assert(window.Count>=240 && window.Count<=241,"history rollover");h.Clear();Assert(h.Window(100,8).Count==0,"history reset");
        foreach(int pos in new[]{1,2,6,13,23,24})
        {
            var v=RaceView.Build(PreviewData.Create(5,pos));
            var expected=Enumerable.Range(Math.Max(1,pos-3),Math.Min(24,pos+3)-Math.Max(1,pos-3)+1).Concat(new[]{1}).Distinct().OrderBy(x=>x);
            Assert(v[0].Name=="LMGT3" && v[0].Drivers.Select(d=>d.ClassPosition).SequenceEqual(expected),"exact three ahead, three behind, player and leader at "+pos);
            Assert(v[0].Drivers.Select(d=>d.Id).Distinct().Count()==v[0].Drivers.Count && v.Skip(1).All(c=>c.Drivers.Count==5),"no duplicate leader; other top five at "+pos);
        }
        f=PreviewData.Create(5);f.Drivers=f.Drivers.Where(d=>d.Class=="LMGT3").ToList();Assert(RaceView.Build(f).Count==1,"single-class hides other classes");
        f=PreviewData.Create(5);f.Drivers=f.Drivers.Where(d=>d.Class=="LMGT3").Take(4).ToList();Assert(RaceView.Build(f)[0].Drivers.Count==4,"small grid keeps every driver");
        Assert(RaceView.Build(new Frame()).Count==0,"empty view");
        var grid=new StartPositions();f=Race(0,3);grid.Apply(f);var player=f.Drivers.Single(d=>d.IsPlayer);int playerId=player.Id;
        f=Race(1,5);player=f.Drivers.Single(d=>d.Id==playerId);player.ClassPosition=10;grid.Apply(f);
        Assert(player.PositionChange==3 && grid.HasStart,"gain from actual pre-start class position");
        f=Race(2,6);player=f.Drivers.Single(d=>d.Id==playerId);player.ClassPosition=15;grid.Apply(f);Assert(player.PositionChange==-2,"loss persists in yellow");
        var late=new StartPositions();f=Race(300,5);late.Apply(f);Assert(f.Drivers.All(d=>!d.PositionChange.HasValue),"late join has unknown start");
        f=Race(0,3);grid.Apply(f);Assert(f.Drivers.All(d=>d.PositionChange==0),"restart resets start");
        f=Race(10,5);f.Session=5;grid.Apply(f);Assert(f.Drivers.All(d=>!d.PositionChange.HasValue),"qualifying does not claim race gains");
        f=Race(0,3);grid.Apply(f);f=Race(1,5);f.SessionStarted=99;grid.Apply(f);Assert(f.Drivers.All(d=>!d.PositionChange.HasValue),"new event clears previous grid");
        var snapshot=CountrySnapshot.Parse("{\"drivers\":{\"Arthur Gabriel\":{\"nationality\":\"BR\"},\"Piloto Hypercar\":{\"nationality\":\"US\"}},\"teams\":{\"utid303\":{\"drivers\":{\"Nicolás Silva\":{\"nationality\":\"ES\"}}}}}");
        f=LmuParser.Parse(bytes,false);Assert(snapshot.For(f.Drivers[1],f)=="br" && snapshot.For(f.Drivers[2],f)=="es","country name and slot matches from lobby schema");
        f.Drivers.Add(new Driver {Id=404,Name="Arthur Gabriel"});Assert(snapshot.For(f.Drivers[1],f)=="","ambiguous name is neutral");
        Assert(snapshot.For(new Driver {Name="Invented Person",Id=777},f)=="","no country inferred from name");
        Assert(CountryCodes.Normalize("Brasil")=="br" && CountryCodes.Normalize("unknown-land")=="","explicit country aliases only");
        Assert(CountrySnapshot.Parse("{\"teams\":{}}").For(f.Drivers[0],f)=="","no nationality in solo or bots");
        f=LmuParser.Parse(bytes,false);Assert(Instruments.SteeringAngle(f,900)==-90,"manual wheel calibration");f.SteeringRange=0;Assert(Instruments.SteeringAngle(f,0)==-48,"visual range fallback");f.VisualSteeringRange=0;Assert(double.IsNaN(Instruments.SteeringAngle(f,0)),"missing degree range not invented");
        f=PreviewData.Create(5);f.Rpm=8200;Assert(Instruments.ShiftNow(f,0),"shift cue near limiter");f.Gear=6;Assert(!Instruments.ShiftNow(f,0),"no shift beyond top gear");f.Gear=0;Assert(!Instruments.ShiftNow(f,0),"no shift in neutral");f.Gear=4;f.Stale=true;Assert(!Instruments.ShiftNow(f,0),"no shift on stale telemetry");
        var freshness=new DataFreshness();f=LmuParser.Parse(bytes,false);f.ScoringEvent=f.TelemetryEvent=0;freshness.Apply(f,0);
        f.Elapsed+=5;f.TelemetryElapsed+=5;freshness.Apply(f,5);Assert(f.HasTelemetry && !f.Stale,"native elapsed advances even with optional event counters zero");
        f.Elapsed+=5;freshness.Apply(f,10);Assert(f.Stale && !f.HasTelemetry,"clock alone does not keep frozen pedal samples alive");
        f=LmuParser.Parse(bytes,false);f.Phase=9;freshness.Apply(f,11);Assert(f.Stale && !f.HasTelemetry,"pause explicit");
        freshness.Clear();f=LmuParser.Parse(bytes,false);freshness.Apply(f,12);Assert(f.HasTelemetry && !f.Stale,"reconnect resets freshness");
        var warm=new InputHistory();warm.Add(100,.2,0);warm.Add(101,.4,.1);
        var plot=warm.Plot(101,8);Assert(plot.Filling && plot.X(100)==0 && plot.X(101)==.125,"graph fills left to right from first sample");
        plot=warm.Plot(109,8);Assert(!plot.Filling && plot.Start==101,"full graph scrolls after one window");
        warm.Clear();warm.Add(200,.1,0);Assert(warm.Plot(200,8).X(200)==0,"reconnect starts at the left again");
        for(int i=1;i<=5000;i++) warm.Add(200+i/125.0,i%100/100.0,0);
        plot=warm.Plot(240,15);Assert(plot.Samples.Count>=1875 && plot.Samples.Count<=1876,"15 seconds retained at 125 Hz");
        var capture=new InputCapture();f=LmuParser.Parse(bytes,false);capture.Accept(f,0);capture.Accept(f,.01);
        Assert(capture.History.Window(.01,8).Count==1,"unchanged packet not duplicated");
        f.TelemetryElapsed+=.01;capture.Accept(f,.02);Assert(capture.History.Window(.02,8).Count==2,"new native sample accepted");
        f.Throttle=.333;capture.Accept(f,.03);Assert(capture.History.Window(.03,8).Last().Throttle==.333,"fractional analog pedal not rounded in acquisition");
        f.SessionStarted++;capture.Accept(f,.04);Assert(capture.History.Plot(.04,8).Start==.04,"new session resets graph origin");
        f.Stale=true;capture.Accept(f,1);capture.Accept(f,3);Assert(capture.History.Window(3,8).Count==0,"stale gap clears trace");
        var motion=new InputMotion();f=LmuParser.Parse(bytes,false);f.Throttle=f.Brake=0;f.Steering=0;motion.Step(f,0,true);
        f.Throttle=1;f.Brake=.6;f.Steering=.4;var displayed=motion.Step(f,.016,true);
        Assert(displayed.Throttle>0 && displayed.Throttle<1 && displayed.Brake>0 && displayed.Brake<.6,"small movement between analog display frames");
        Assert(f.Throttle==1 && f.Steering==.4,"display never overwrites native values");
        double prior=displayed.Throttle;
        for(int i=2;i<=10;i++) { displayed=motion.Step(f,i*.016,true);Assert(displayed.Throttle>=prior && displayed.Throttle<=1,"motion monotonic without overshoot "+i);prior=displayed.Throttle; }
        Assert(displayed.Throttle>.98,"display catches up within 160 ms");
        displayed=motion.Step(f,.17,false);Assert(displayed.Throttle==f.Throttle && displayed.Steering==f.Steering,"smoothing can be disabled");
        Assert(GarageRange.Parse("{\"VM_STEER_LOCK\":{\"stringValue\":\"540 deg\"}}")==540,"garage wheel range schema");
        Assert(GarageRange.Parse("{\"VM_STEER_LOCK\":{\"stringValue\":\"900°\"},\"other\":[1,2]}")==900,"garage range with degree symbol and unrelated fields");
        Assert(GarageRange.Parse("{\"VM_STEER_LOCK\":{\"stringValue\":\"20 Nm\"}}")==0,"unrelated numeric unit not treated as degrees");
        Assert(GarageRange.Parse("{\"other\":{\"stringValue\":\"540\"}}")==0,"unrelated setup field not treated as wheel range");
        f=LmuParser.Parse(bytes,false);f.SteeringRange=0;f.GarageSteeringRange=900;
        Assert(Instruments.SteeringAngle(f,0)==-90 && Instruments.SteeringSource(f,0)=="setup do carro","setup range preferred to visual fallback");
        f.GarageSteeringRange=f.VisualSteeringRange=0;
        Assert(Instruments.SteeringDrawing(f,0)==-27 && double.IsNaN(Instruments.SteeringAngle(f,0)),"normalized wheel moves without claiming physical degrees");
        Assert(Instruments.SteeringAngle(f,720)==-72,"manual physical calibration takes priority");
        f=LmuParser.Parse(bytes,false);f.Steering=.4;f.SteeringRange=900;
        Assert(Instruments.SteeringAngle(f,0,50)==90 && Instruments.SteeringDrawing(f,0,50)==90,"reported doubled right turn is corrected in both drawing and degrees");
        f.Steering=-.4;Assert(Instruments.SteeringAngle(f,0,50)==-90,"same calibration preserves left direction");
        f.Steering=.2;Assert(Instruments.SteeringAngle(f,0,50)==45,"partial steering retains analog proportion");
        Assert(Instruments.SteeringAngle(f,0,100)==90 && f.Steering==.2,"100 percent restores original conversion without modifying telemetry");
        Assert(Instruments.SteeringAngle(f,720,50)==36,"manual full range and gain compose once");
        f.Stale=true;Assert(double.IsNaN(Instruments.SteeringAngle(f,0,50)) && double.IsNaN(Instruments.SteeringDrawing(f,0,50)),"calibration never revives stale steering");
        f.Stale=false;f.SteeringRange=f.VisualSteeringRange=f.GarageSteeringRange=0;
        Assert(double.IsNaN(Instruments.SteeringAngle(f,0,50)) && Instruments.SteeringDrawing(f,0,50)==13.5,"normalized fallback applies gain without inventing degrees");
        var popup=new PopupInteraction();Assert(!popup.Blocks(1),"window work initially available");
        popup.Open();Assert(popup.Blocks(1) && popup.Blocks(100),"popup retains focus for its entire open lifetime");
        popup.Close(100);Assert(popup.Blocks(100.1) && !popup.Blocks(100.5),"window work resumes only after popup close grace");
        popup.Open();Assert(popup.Blocks(101),"opening menu again renews focus protection");
        b=(byte[])bytes.Clone();Set(b,Telem(2,Layout.Telem_mUnfilteredSteering),0.0);Set(b,Telem(2,Layout.Telem_mFilteredSteering),.3);
        f=LmuParser.Parse(b,false);Assert(f.Steering==.3 && f.SteeringInputSource=="filtrada","optional raw steering zero falls back to published filtered input");
        Set(b,Telem(2,Layout.Telem_mUnfilteredSteering),-.3);Assert(LmuParser.Parse(b,false).Steering==-.3,"valid raw steering has priority");
        var retry=new PresentationRetry();retry.Failed(1);Assert(!retry.Ready(1.1) && retry.Ready(1.21),"transient display error schedules retry");
        for(int i=0;i<50;i++) retry.Failed(2);Assert(retry.RetryAt<=4,"display retry delay stays bounded");retry.Succeeded();Assert(retry.Ready(4) && retry.Failures==0,"display resumes after recovery");
        Version05Checks(bytes);RecoverTelemetry(bytes);RecoverCountries(bytes);
        Version08(bytes);Version09();Version092(bytes);Version010(bytes);DamageChecks(bytes);Version012();Version0121();Version0122();Version0123();
        Console.WriteLine("PASS: "+count+" assertions using SDK binary fixtures, session scenarios and real background workers.");
        if(args.Length>1) { Render(args[1]);Render08(args[1]);Render09(args[1]);RenderLearning(args[1]);RenderDamage(args[1]);Render011(args[1]);Render012(args[1]); }
    }
    sealed class TransientReader : ITelemetryReader
    {
        readonly byte[] invalid,valid;
        int calls,resets;
        public int Calls { get { return Interlocked.CompareExchange(ref calls,0,0); } }
        public int Resets { get { return Interlocked.CompareExchange(ref resets,0,0); } }
        public TransientReader(byte[] bytes)
        {
            valid=bytes;invalid=(byte[])bytes.Clone();
            Set(invalid,Layout.Scoring+Layout.ScoringInfo_mNumVehicles,105);
        }
        public Frame Read(bool filtered) { return LmuParser.Parse(Interlocked.Increment(ref calls)==1 ? invalid : valid,filtered); }
        public void Dispose() { Interlocked.Increment(ref resets); }
    }
    static void RecoverTelemetry(byte[] bytes)
    {
        Assert(!typeof(IOException).IsAssignableFrom(typeof(InvalidDataException)),"invalid-data errors are not IOExceptions");
        var clock=Stopwatch.StartNew();var source=new TransientReader(bytes);Exception reported=null;
        using(var fault=new ManualResetEvent(false))
        using(var pump=new TelemetryPump(()=>clock.Elapsed.TotalSeconds,(context,ex)=>{reported=ex;fault.Set();},source))
        {
            pump.Start();
            Assert(fault.WaitOne(2000),"invalid packet is logged by the real telemetry timer without killing the process");
            Assert(reported is InvalidDataException && !pump.Latest.HasTelemetry && pump.Latest.Status.Contains("Reconectando"),"invalid packet publishes an explicit reconnect state");
            Assert(SpinWait.SpinUntil(()=>pump.Latest.HasTelemetry,4000),"telemetry timer resumes automatically after invalid packet");
            Assert(pump.Latest.Throttle==.73 && pump.Latest.Brake==.19 && pump.Latest.PlayerId==202,"recovered packet restores the correct player and analog values");
            Assert(source.Calls>=2 && source.Resets>0,"failed reader is released before retry");
        }
        Console.WriteLine("PASS: telemetry worker survived invalid data and resumed live-input processing.");
    }
    static void RecoverCountries(byte[] bytes)
    {
        int calls=0,errors=0;Exception reported=null;
        using(var service=new CountryService(()=>CountrySnapshot.Parse(Interlocked.Increment(ref calls)==1 ? "{}" :
            "{\"drivers\":{\"Arthur Gabriel\":{\"nationality\":\"Brazil\"}}}"),
            (context,ex)=>{reported=ex;Interlocked.Increment(ref errors);}))
        {
            var f=LmuParser.Parse(bytes,false);service.Apply(f,0,true);
            Assert(SpinWait.SpinUntil(()=>service.Status.StartsWith("Bandeiras indisponíveis"),2000),"empty lobby response does not kill the real country worker/process");
            Assert(Interlocked.CompareExchange(ref errors,0,0)==1 && reported is InvalidDataException,"invalid lobby response is recorded in diagnostics");
            service.Apply(f,.1,true);
            Assert(f.HasTelemetry && f.Drivers.Count==3 && f.Drivers.All(d=>d.Country==""),"missing countries preserve telemetry and all players with neutral flags");
            service.Apply(f,11,true);
            Assert(SpinWait.SpinUntil(()=>service.Status=="Bandeiras: nacionalidade informada pelo lobby",2000),"country worker retries and accepts a later valid lobby");
            service.Apply(f,11.1,true);
            Assert(f.Drivers.Single(d=>d.IsPlayer).Country=="br","nationality is restored after a transient response failure");
            Assert(Interlocked.CompareExchange(ref calls,0,0)==2 && errors==1,"country retries keep the poll interval and release the busy guard");
        }
        Console.WriteLine("PASS: country worker survived an empty lobby and recovered nationality data.");
    }
    static void Render(string directory)
    {
        Directory.CreateDirectory(directory);var o=new ViewOptions();var f=PreviewData.Create(12.9);var h=new InputHistory();
        f.PhysicalWheel=true;f.PhysicalWheelAngle=-27;f.WheelStatus="CALIBRADO";
        for(int i=0;i<=240;i++) { var s=PreviewData.Create(4.9+i/30.0);h.Add(i/30.0,s.Throttle,s.Brake); }
        using(var p=new Painter())
        {
            Save(directory+"/pedais.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,f,h,8,o,false));
            Save(directory+"/classificacao.png",Painter.TableWidth,Painter.TableHeight(f),g=>p.Standings(g,f,o,false));
            var startHistory=new InputHistory();for(int i=0;i<=30;i++) startHistory.Add(i/60.0,i/60.0,0);
            Save(directory+"/inicio-grafico.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,f,startHistory,.5,o,false));
            var noRange=PreviewData.Create(5);noRange.SteeringRange=noRange.VisualSteeringRange=noRange.GarageSteeringRange=0;noRange.Steering=.5;
            Save(directory+"/volante-sem-faixa.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,noRange,h,8,o,false));
            Save(directory+"/aguardando.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,new Frame(),h,8,o,false));
            var shift=PreviewData.Create(5);shift.Rpm=8300;shift.Steering=.5;
            Save(directory+"/troca-volante.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,shift,h,8,o,false));
            var centered=PreviewData.Create(5);centered.Steering=0;centered.SteeringRange=900;
            Save(directory+"/volante-centro.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,centered,h,8,o,false));
            centered.Steering=.2;
            Save(directory+"/volante-90-direita.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,centered,h,8,o,false));
            centered.Steering=-.2;
            Save(directory+"/volante-90-esquerda.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,centered,h,8,o,false));
            using(var logo=BrandArtwork.LoadLogo())
                Save(directory+"/cabecalho-configuracao.png",684,114,g=>{g.Clear(Theme.Background);BrandArtwork.Header(g,logo);});
            var pause=PreviewData.Create(5);pause.Stale=true;pause.HasTelemetry=false;
            Save(directory+"/pausa.png",Painter.TableWidth,Painter.TableHeight(pause),g=>p.Standings(g,pause,o,true));
            var longName=PreviewData.Create(5);longName.Drivers.Single(d=>d.IsPlayer).Name="Arthur Gabriel Evangelista Santos";
            Save(directory+"/nomes-longos.png",Painter.TableWidth,Painter.TableHeight(longName),g=>p.Standings(g,longName,o,false));
            var qualifier=PreviewData.Create(12.9);qualifier.Session=5;foreach(var d in qualifier.Drivers) d.StartClassPosition=null;
            Save(directory+"/treino-classificacao-gap.png",Painter.TableWidth,Painter.TableHeight(qualifier),g=>p.Standings(g,qualifier,o,false));
            var ownOnly=new ViewOptions {OnlyMyClass=true};
            Save(directory+"/somente-minha-classe.png",Painter.TableWidth,Painter.TableHeight(qualifier,true),g=>p.Standings(g,qualifier,ownOnly,false));
            var limiter=PreviewData.Create(5);limiter.PitLimiter=true;limiter.Speed=60;limiter.Gear=2;limiter.Rpm=2500;
            Save(directory+"/limitador-boxes.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,limiter,h,8,o,false));
            var physical=PreviewData.Create(5);physical.PhysicalWheel=true;physical.PhysicalWheelAngle=180;physical.WheelStatus="CALIBRADO";
            Save(directory+"/volante-fisico-180.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,physical,h,8,o,false));
            physical.PhysicalWheelAngle=450;
            Save(directory+"/volante-fisico-450.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,physical,h,8,o,false));
            Save(directory+"/pedais-largos.png",1000,272,g=>{g.ScaleTransform(1000f/Painter.PedalsWidth,272f/Painter.PedalsHeight);p.Pedals(g,physical,h,8,o,true);});
            Save(directory+"/pedais-altos.png",720,390,g=>{g.ScaleTransform(720f/Painter.PedalsWidth,390f/Painter.PedalsHeight);p.Pedals(g,physical,h,8,o,true);});
            Save(directory+"/tabela-estreita.png",700,Painter.TableHeight(f),g=>{g.ScaleTransform(700f/Painter.TableWidth,1);p.Standings(g,f,o,true);});
            physical.PhysicalWheelAngle=double.NaN;physical.WheelStatus="RECALIBRAR";
            Save(directory+"/volante-recalibrar.png",Painter.PedalsWidth,Painter.PedalsHeight,g=>p.Pedals(g,physical,h,8,o,false));
            var transparent=new ViewOptions {PedalOpacity=20,TableOpacity=20};
            Save(directory+"/fundo-claro-opacidade-20.png",1680,760,g=>
            {
                g.Clear(Color.FromArgb(218,222,220));var state=g.Save();g.TranslateTransform(20,20);p.Standings(g,f,transparent,false);g.Restore(state);
                state=g.Save();g.TranslateTransform(916,20);p.Pedals(g,f,h,8,transparent,false);g.Restore(state);
            });
            Save(directory+"/Previa-Arts-LMU-Overlay.png",1680,850,g=>
            {
                g.Clear(Color.FromArgb(65,76,85));
                using(var bg=new System.Drawing.Drawing2D.LinearGradientBrush(new Point(0,0),new Point(1680,850),Color.FromArgb(74,88,96),Color.FromArgb(26,37,46))) g.FillRectangle(bg,0,0,1680,850);
                using(var font=new Font(Theme.BodyFont,12,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var b=new SolidBrush(Theme.Text)) g.DrawString("ARTS LMU OVERLAY 0.8  /  PRÉVIA DO VISUAL COM DADOS DE TESTE",font,b,20,15);
                var state=g.Save();g.TranslateTransform(20,57);p.Standings(g,f,o,false);g.Restore(state);
                state=g.Save();g.TranslateTransform(916,57);
                using(var logo=BrandArtwork.LoadLogo()) BrandArtwork.Header(g,logo);g.Restore(state);
                state=g.Save();g.TranslateTransform(916,196);p.Pedals(g,f,h,8,o,false);g.Restore(state);
                using(var font=new Font(Theme.BodyFont,16,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var b=new SolidBrush(Theme.Text))
                {
                    g.DrawString("Volante físico: leitura direta e calibração até 180°.",font,b,930,510);
                    g.DrawString("Letras brancas mais fortes. Porcentagens e km/h maiores.",font,b,930,542);
                    g.DrawString("Sua classe: você + 3 à frente + 3 atrás + líder, se faltar.",font,b,930,589);
                    g.DrawString("Somente sua classe; demais categorias ficam ocultas.",font,b,930,621);
                    g.DrawString("Melhor volta no topo; GAP por melhor volta no treino/qualy.",font,b,930,668);
                    g.DrawString("Punições, marcas dos carros e compostos de pneus.",font,b,930,700);
                }
                using(var font=new Font(Theme.BodyFont,11,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var b=new SolidBrush(Theme.Muted)) g.DrawString("Render do código do app; não é captura do LMU ou da janela completa de configuração.",font,b,930,779);
            });
        }
        using(var bitmap=(Bitmap)Image.FromFile(directory+"/pedais.png"))
        {
            Assert(bitmap.GetPixel(32,120).A<=255 && bitmap.GetPixel(32,120).A>=230,"background opacity retained in PNG");
            Assert(bitmap.GetPixel(0,0).A==0,"rounded shell has transparent corners");
        }
        Console.WriteLine("PASS: "+count+" assertions total. Real drawing code rendered to "+directory);
    }
    static void ExportIcon(string path)
    {
        // Standard PNG-to-ICO conversion. The user's original artwork remains unchanged.
        int[] sizes={16,24,32,48,64,128,256};var images=new byte[sizes.Length][];
        using(var logo=BrandArtwork.LoadLogo()) for(int i=0;i<sizes.Length;i++)
        using(var bitmap=new Bitmap(sizes[i],sizes[i],PixelFormat.Format32bppArgb))
        using(var g=Graphics.FromImage(bitmap))
        using(var stream=new MemoryStream())
        {
            g.Clear(Color.Transparent);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(logo,new Rectangle(0,0,sizes[i],sizes[i]),BrandArtwork.IconBounds,GraphicsUnit.Pixel);
            bitmap.Save(stream,ImageFormat.Png);images[i]=stream.ToArray();
        }
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++)
            {
                writer.Write((byte)(sizes[i]==256 ? 0 : sizes[i]));writer.Write((byte)(sizes[i]==256 ? 0 : sizes[i]));
                writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);
                writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;
            }
            foreach(var data in images) writer.Write(data);
        }
        Console.WriteLine("Brand icon exported: "+path);
    }
    static void Save(string path,int w,int h,Action<Graphics> draw)
    {
        using(var bitmap=new Bitmap(w*2,h*2,PixelFormat.Format32bppPArgb))
        using(var g=Graphics.FromImage(bitmap)) { g.Clear(Color.Transparent);g.ScaleTransform(2,2);draw(g);bitmap.Save(path,ImageFormat.Png); }
    }
}
