using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using Arthur.LmuOverlay;

partial class Checks
{
    static WheelAxis Axis(double angle,string device="wheel-A",uint axis=2)
    { return new WheelAxis { DeviceId=device,DeviceName="Volante de teste",Id=axis,Value=32768+angle*60,Minimum=0,Maximum=65535,Raw=true }; }
    static void RejectCalibration(Action action,string message)
    { bool rejected=false;try { action(); } catch(InvalidOperationException) { rejected=true; } Assert(rejected,message); }
    static void Field(byte[] b,int offset,int size,string value)
    { Array.Clear(b,offset,size);Encoding.UTF8.GetBytes(value).CopyTo(b,offset); }
    static List<PenaltyRow> PenaltyJson(string json) { return PenaltyMessage.Parse(Encoding.UTF8.GetBytes(json)); }
    static void Version05Checks(byte[] fixture)
    {
        var calibration=WheelCalibration.Create(Axis(0),Axis(90),Axis(180),500);
        foreach(double angle in new[]{-450.0,-360,-180,-90,-45,0,22.5,90,135,180,270,360,450})
            Assert(Math.Abs(calibration.Angle(Axis(angle))-angle)<1e-9,"physical wheel retains actual angle, without 90-degree clipping: "+angle);
        Assert(calibration.Valid,"saved physical calibration valid");
        Assert(double.IsNaN(calibration.Angle(Axis(90,"wheel-B"))),"new wheel cannot inherit the old device calibration");
        Assert(double.IsNaN(calibration.Angle(Axis(90,"wheel-A",3))),"pedal axis cannot be mistaken for calibrated steering");
        var changed=Axis(90);changed.Raw=false;Assert(double.IsNaN(calibration.Angle(changed)),"raw/calibrated mode change invalidates physical calibration");
        changed=Axis(90);changed.Maximum=32767;Assert(Math.Abs(calibration.Angle(changed)-90)<1e-9,"driver range refresh does not freeze a calibrated physical wheel");
        RejectCalibration(()=>WheelCalibration.Create(Axis(0),Axis(90),Axis(110),500),"nonlinear or saturated turn rejected at 180");
        RejectCalibration(()=>WheelCalibration.Create(Axis(0),Axis(90),Axis(-180),500),"opposite direction rejected");
        RejectCalibration(()=>WheelCalibration.Create(Axis(0),Axis(0),Axis(0),500),"no movement rejected");
        RejectCalibration(()=>WheelCalibration.Create(Axis(0),Axis(90),Axis(180,"other"),500),"unplug/replacement during calibration rejected");
        RejectCalibration(()=>WheelCalibration.Create(Axis(0),Axis(90),changed,500),"changed range during capture rejected");
        changed=Axis(90);changed.Value=double.NaN;Assert(double.IsNaN(calibration.Angle(changed)),"invalid physical sample never enters drawing transform");
        RejectCalibration(()=>WheelCalibration.Create(Axis(0),changed,Axis(180),500),"NaN capture rejected");
        var reverse=WheelCalibration.Create(Axis(0),Axis(-90),Axis(-180),900);
        Assert(reverse.Angle(Axis(-270))==270,"driver with reversed native axis still matches right-positive convention");
        Assert(Math.Abs(reverse.Angle(Axis(-32767),900)-450)<.1,"configured wheel rotation preserves reversed-axis direction");
        var f=PreviewData.Create(5);f.SteeringRange=500;f.Steering=1;
        PhysicalWheelState.Apply(f,true,calibration,new List<WheelAxis>{Axis(270)});
        Assert(f.PhysicalWheel && Instruments.SteeringAngle(f,900,50)==270 && Instruments.SteeringDrawing(f,900,50)==270,"physical direction ignores game saturation, manual game range and legacy gain");
        f.Stale=true;f.HasTelemetry=false;
        Assert(Instruments.SteeringAngle(f,0)==270,"live device may turn while the game telemetry is paused");
        PhysicalWheelState.Apply(f,true,calibration,new List<WheelAxis>());
        Assert(f.WheelStatus=="SEM VOLANTE" && double.IsNaN(Instruments.SteeringDrawing(f,0)),"disconnect clears rather than freezes or invents angle");
        PhysicalWheelState.Apply(f,true,calibration,new List<WheelAxis>{Axis(-180)});
        Assert(f.PhysicalWheelAngle==-180,"same calibrated device recovers after reconnect");
        f.SteeringRange=900;PhysicalWheelState.Apply(f,true,calibration,new List<WheelAxis>{Axis(90)});
        Assert(f.WheelStatus=="CALIBRADO" && f.PhysicalWheelAngle==90,"car steering-range changes do not invalidate physical-wheel degrees");
        PhysicalWheelState.Apply(f,true,null,new List<WheelAxis>{Axis(90)});Assert(f.WheelStatus=="CALIBRAR","first use is explicit calibration state");
        f.Stale=false;f.HasTelemetry=true;f.Steering=.25;PhysicalWheelState.Apply(f,true,null,new List<WheelAxis>(),0,720);
        Assert(f.PhysicalWheelFallback && Math.Abs(Instruments.SteeringAngle(f,0)-90)<1e-9 && f.WheelStatus.Contains("MODO JOGO"),"missing physical axis keeps the wheel moving with the configured car range");
        f.Stale=false;f.HasTelemetry=true;f.Steering=.2;PhysicalWheelState.Apply(f,false,calibration,null);
        Assert(!f.PhysicalWheel && Instruments.SteeringAngle(f,0)==90,"optional game mode still works separately");
        using(var data=new MemoryStream())
        {
            var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(WheelCalibration));serializer.WriteObject(data,calibration);data.Position=0;
            var restored=(WheelCalibration)serializer.ReadObject(data);Assert(restored.Valid && restored.Angle(Axis(360))==360,"calibration persists without losing angle precision");
        }
        Assert(Marshal.SizeOf(typeof(WheelInput.Instance))==1100,"DirectInput Unicode device ABI size");
        Assert(Marshal.SizeOf(typeof(WheelInput.AxisObject))==576,"DirectInput Unicode object ABI size");
        Assert(Marshal.SizeOf(typeof(WheelInput.DataFormat))==(IntPtr.Size==8 ? 32 : 24),"DirectInput data format architecture ABI");
        Assert(Marshal.SizeOf(typeof(WheelInput.ObjectFormat))==(IntPtr.Size==8 ? 24 : 16),"DirectInput object format architecture ABI");

        f=PreviewData.Create(5);var player=f.Drivers.Single(d=>d.IsPlayer);var other=f.Drivers.First(d=>d.Class==player.Class && !d.IsPlayer);
        player.BestLap=120;other.BestLap=118.25;other.Gap=999;other.LapsBehind=4;
        for(int session=0;session<=9;session++)
        { f.Session=session;Assert(Format.Gap(f,other)=="−1,750","practice/qualifying/warmup uses signed own best-lap comparison, session "+session); }
        Assert(Format.Gap(f,player)=="0,000","self gap is zero");
        other.BestLap=121.25;Assert(Format.Gap(f,other)=="+1,250","slower best lap has positive delta");
        other.BestLap=120.0001;Assert(Format.Gap(f,other)=="0,000","rounded equal lap has no false sign");
        other.BestLap=-1;Assert(Format.Gap(f,other)=="—","opponent without valid best lap has no comparison");
        other.BestLap=118;player.BestLap=0;Assert(Format.Gap(f,other)=="—","no comparison until user has a valid best lap");
        player.BestLap=120;other.FinishStatus=3;Assert(Format.Gap(f,other)=="DSQ","disqualification stays explicit");other.FinishStatus=0;
        for(int session=10;session<=13;session++) { f.Session=session;Assert(Format.Gap(f,other)=="+4 V","race retains native lap deficit, session "+session); }
        other.LapsBehind=0;Assert(Format.Gap(f,other)=="+999,000","race retains native overall-leader time gap");
        f=PreviewData.Create(5);Assert(RaceView.Build(f,true).Count==1 && RaceView.Build(f,true)[0].Name=="LMGT3","only mine hides other classes");
        Assert(RaceView.Build(f,true)[0].Drivers.Count==8,"middle of grid gives seven neighbors/self plus leader");
        Assert(RaceView.Best(f,false).Class=="HYPERCAR" && RaceView.Best(f,true).Class=="LMGT3","header best follows visible scope");
        var best=RaceView.Best(f,false);best.Position=12;Assert(RaceView.Best(f,false)==best && best.Position==12,"fastest lap is independent of current race rank");
        best.FinishStatus=3;Assert(RaceView.Best(f,false)!=best,"DSQ lap excluded from best header");
        foreach(var d in f.Drivers) d.BestLap=double.NaN;
        Assert(RaceView.Best(f,false)==null,"best header handles missing times");
        f=new Frame();Assert(RaceView.Build(f,true).Count==0 && RaceView.Best(f,true)==null,"filtered empty lobby safe");
        f.Drivers.Add(new Driver {Id=1,IsPlayer=true,Class="GT3",ClassPosition=1,Position=1});
        Assert(RaceView.Build(f,true)[0].Drivers.Count==1,"solo player remains visible");

        var b=(byte[])fixture.Clone();BitConverter.GetBytes((short)2).CopyTo(b,Car(2,Layout.Car_mNumPenalties));
        Field(b,Car(2,Layout.Car_mVehicleName),64,"Ferrari 296 GT3");
        Field(b,Telem(2,Layout.Telem_mFrontTireCompoundName),18,"Medium");Field(b,Telem(2,Layout.Telem_mRearTireCompoundName),18,"Wet");
        b[Telem(2,Layout.Telem_mSpeedLimiter)]=1;f=LmuParser.Parse(b,false);player=f.Drivers.Single(d=>d.IsPlayer);
        Assert(f.PitLimiter && player.Penalties==2 && Format.Penalty(player)=="2 PEN","native limiter and pending penalty count by player ID");
        Assert(player.CarName=="Ferrari 296 GT3" && player.TyreFront=="Medium" && player.TyreRear=="Wet" && player.TyreSource=="telemetria","native own-car identity and compounds");
        b[Telem(2,Layout.Telem_mSpeedLimiter)]=0;b[Telem(2,Layout.Telem_mSpeedLimiterActive)]=1;
        Assert(LmuParser.Parse(b,false).PitLimiter,"alternate native limiter-active flag");
        b[Telem(2,Layout.Telem_mSpeedLimiterActive)]=0;b[Telem(0,Layout.Telem_mSpeedLimiter)]=1;
        Assert(!LmuParser.Parse(b,false).PitLimiter,"another car limiter cannot tint the local gear");
        Assert(PenaltyJson("{\"messageType\":\"ACK\",\"body\":{\"ok\":true}}") == null,"websocket acknowledgements with non-list bodies ignored");
        Assert(PenaltyJson("{\"messageType\":\"UPDATE\",\"topic\":\"Other\",\"body\":123}")==null,"other UI topics ignored");
        Assert(PenaltyJson("{\"messageType\":\"UPDATE\",\"topic\":\"LiveStandings\",\"body\":[]}").Count==0,"empty LiveStandings accepted");
        Reject(()=>PenaltyJson("{\"messageType\":\"UPDATE\",\"topic\":\"LiveStandings\"}"),"incomplete standings body rejected");
        bool malformed=false;try { PenaltyJson("not-json"); } catch(SerializationException) { malformed=true; } Assert(malformed,"malformed websocket JSON rejected for service recovery");
        var rows=PenaltyJson("{\"messageType\":\"UPDATE\",\"topic\":\"LiveStandings\",\"body\":[{\"slotID\":202,\"driverName\":\"Arthur Gabriel\",\"penalties\":{\"DT\":1,\"SG\":2,\"TIME\":0},\"tireCompoundNameFront\":\"Hard\",\"tireCompoundNameRear\":\"Hard\"}]}");
        PenaltyMessage.Apply(f,rows);Assert(Format.Penalty(player)=="DT+SG","real websocket penalty schema decoded");
        Assert(player.TyreFront=="Medium" && player.TyreRear=="Wet","fresh native compound takes priority over websocket");
        player.Penalties=0;PenaltyMessage.Apply(f,rows);Assert(Format.Penalty(player)=="—" && player.PenaltyTypes=="","served penalty clears immediately despite cached details");
        player.Penalties=1;rows[0].driverName="Different driver";PenaltyMessage.Apply(f,rows);
        Assert(Format.Penalty(player)=="1 PEN","reused slot with another name never transfers penalty type");
        rows[0].driverName=player.Name;rows[0].vehFilename="other-car";PenaltyMessage.Apply(f,rows);Assert(player.PenaltyTypes=="","vehicle identity mismatch rejects stale details");
        rows[0].vehFilename=null;rows.Add(rows[0]);PenaltyMessage.Apply(f,rows);Assert(player.PenaltyTypes=="","duplicate identity is ambiguous");rows.RemoveAt(1);
        player.TyreFront=player.TyreRear=player.TyreSource="";rows[0].penalties=new PendingPenaltyTypes { TIME=1 };PenaltyMessage.Apply(f,rows);
        Assert(Format.Penalty(player)=="T" && player.TyreFront=="Hard" && player.TyreSource=="WS","time penalty and compound websocket fallback");
        PenaltyMessage.Apply(f,null);Assert(player.TyreSource=="" && player.TyreFront=="" && Format.Penalty(player)=="1 PEN","expired details clear tire info and retain only native pending count");
        Assert(TyreCompound.Code("Soft")=="S" && TyreCompound.Code("Medium")=="M" && TyreCompound.Code("Hard")=="H" && TyreCompound.Code("Wet")=="W","LMU compound labels recognized");
        Assert(TyreCompound.Code("")=="?" && TyreCompound.Code("0")=="?" && TyreCompound.Code(null)=="?","unknown tire indexes not assigned invented compounds");
        Assert(CarIdentity.Make(player)=="ferrari","car manufacturer from native vehicle name");
        Assert(CarIdentity.Make(new Driver {VehicleKey="Aston_Martin_GT3_1"})=="astonmartin","vehicle filename explicit make alias");
        Assert(CarIdentity.Make(new Driver {CarName="Corvette Z06 GT3"})=="chevrolet","Corvette recognized as Chevrolet");
        Assert(CarIdentity.Make(new Driver {CarName="unknown car"})=="","unknown manufacturer uses generic icon");
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("CarLogos.zip"))
        using(var archive=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            Assert(archive.Entries.Count==14,"all fourteen manufacturer logos embedded");
            foreach(var entry in archive.Entries)
            using(var source=entry.Open()) using(var image=Image.FromStream(source))
            {
                Assert(image.Width>0 && image.Height>0,"manufacturer PNG decodes: "+entry.Name);
                if(entry.Name!="alpine.png" && entry.Name!="lexus.png")
                {
                    bool colored=false;
                    using(var bitmap=new Bitmap(image))
                    {
                        for(int iy=0;iy<bitmap.Height && !colored;iy++) for(int ix=0;ix<bitmap.Width;ix++)
                        {
                            Color pixel=bitmap.GetPixel(ix,iy);
                            if(pixel.A>24 && Math.Max(pixel.R,Math.Max(pixel.G,pixel.B))-Math.Min(pixel.R,Math.Min(pixel.G,pixel.B))>18) { colored=true; break; }
                        }
                    }
                    Assert(colored,"manufacturer logo keeps a brand color: "+entry.Name);
                }
            }
        }
        Assert(PanelSizing.Fit(1100,250,560,215,1800,1000)==new Size(1100,250),"wide panel height remains independent");
        Assert(PanelSizing.Fit(700,650,560,215,1800,1000)==new Size(700,650),"tall panel width remains independent");
        Assert(PanelSizing.Fit(3000,2000,560,215,1000,700)==new Size(1000,700),"panel respects monitor on both axes");
        Assert(PanelSizing.Fit(1,1,560,215,1800,1000)==new Size(560,215),"legibility minimum dimensions");
        Assert(PanelSizing.Fit(1,1,560,215,300,100)==new Size(300,100),"tiny monitor bounds take priority over minimum");
        using(var painter=new Painter())
        {
            var create=typeof(Painter).GetMethod("Font",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int i=0;i<600;i++) create.Invoke(painter,new object[]{6f+i*.25f,false,false});
            var fonts=(System.Collections.IDictionary)typeof(Painter).GetField("fonts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(painter);
            Assert(fonts.Count<=256,"repeated free resizing cannot grow the GDI font cache without a bound");
        }
    }
}
