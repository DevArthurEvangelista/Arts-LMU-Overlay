using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Arthur.LmuOverlay;

partial class Checks
{
    static void DamageChecks(byte[] bytes)
    {
        var b=(byte[])bytes.Clone();int t=Telem(2,Layout.Telem_mWheels);
        Set(b,t+Layout.Wheel_mWear,.93);Set(b,t+Layout.WheelSize+Layout.Wheel_mWear,1.0);
        Set(b,t+2*Layout.WheelSize+Layout.Wheel_mWear,0.0);Set(b,t+3*Layout.WheelSize+Layout.Wheel_mWear,1.5);
        b[t+Layout.Wheel_mFlat]=1;b[t+Layout.WheelSize+Layout.Wheel_mDetached]=1;b[Telem(2,Layout.Telem_mDetached)]=1;
        var f=LmuParser.Parse(b,false);
        Assert(f.Tyres[0].Remaining==.93 && f.Tyres[1].Remaining==1 && f.Tyres[2].Remaining==0,"native tread fraction uses 1=new and preserves fully worn zero");
        Assert(double.IsNaN(f.Tyres[3].Remaining) && f.HasTelemetry,"invalid wear never blocks pedal telemetry");
        Assert(f.Tyres[0].Flat && f.Tyres[1].Detached && f.DetachedParts,"punctured tyre, detached wheel and other detached part are independent warnings");
        const string json="{\"fuel\":42,\"fuelCapacity\":100,\"tireCondition\":[0.93,0.9,0.87,0.8],\"suspensionDamage\":[0,0.02,0.15,0.4],\"vehicleDamage\":0.08}";
        var damage=VehicleDamage.Parse(json);
        Assert(damage.Aero==.08 && damage.Suspension.SequenceEqual(new[]{0.0,.02,.15,.4}),"local vehicle-condition field mapping keeps FL FR RL RR and overall aero damage");
        Assert(!VehicleDamage.Parse("{}").Any,"missing condition is unknown, never four healthy suspensions");
        var partial=VehicleDamage.Parse("{\"suspensionDamage\":[0.2,null,-1,2],\"vehicleDamage\":{\"unknown\":1}}");
        Assert(partial.Suspension[0]==.2 && partial.Suspension.Skip(1).All(double.IsNaN) && double.IsNaN(partial.Aero),"malformed/unsupported components stay unknown without discarding known suspension");
        Assert(!VehicleDamage.Parse("{\"suspensionDamage\":[0,0,0],\"vehicleDamage\":12}").Any,"incomplete wheel arrays and out-of-range damage are rejected");
        Assert(!VehicleDamage.Parse("{\"suspensionDamage\":[\"0.5\",true,null,{}],\"vehicleDamage\":\"NaN\"}").Any,"strings, booleans and objects cannot become numeric health");
        Reject(()=>VehicleDamage.Parse(new string('x',128*1024+1)),"damage response bound prevents unbounded local reads");
        using(var entered=new ManualResetEvent(false)) using(var release=new ManualResetEvent(false)) using(var finished=new ManualResetEvent(false))
        {
            double now=0;int requests=0;
            using(var service=new VehicleDamageService(()=>now,null,()=>
            { Interlocked.Increment(ref requests);entered.Set();release.WaitOne(2000);finished.Set();return damage; }))
            {
                f=LmuParser.Parse(bytes,false);service.Apply(f,now,true);Assert(entered.WaitOne(1500),"damage read is scheduled off the UI thread");
                var watch=Stopwatch.StartNew();for(int i=0;i<10000;i++) service.Apply(f,now,true);
                Assert(watch.ElapsedMilliseconds<600 && Volatile.Read(ref requests)==1,"slow local endpoint never blocks UI or overlaps itself");
                f.PlayerId++;service.Apply(f,now,true);release.Set();Assert(finished.WaitOne(1500),"damage worker can finish after car change");
                Thread.Sleep(20);Assert(!f.Damage.Any,"response for a previous player is not published to the new car");
                now=.1;service.Apply(f,now,true);
                Assert(SpinWait.SpinUntil(()=>{service.Apply(f,now,true);return f.Damage.Any;},1500),"fresh response for new identity is applied");
                Assert(f.Damage.Aero==.08 && f.Damage.Suspension[3]==.4,"known damage reaches display frame");
                f.InRealtime=false;service.Apply(f,now,true);Assert(!f.Damage.Any,"garage clears old suspension/aero values");
                f.InRealtime=true;service.Apply(f,now,false);Assert(!f.Damage.Any,"disabled tyre card disables damage polling");
            }
        }
        using(var entered=new ManualResetEvent(false)) using(var release=new ManualResetEvent(false))
        {
            double now=0;int calls=0;
            using(var service=new VehicleDamageService(()=>now,null,()=>
            { if(Interlocked.Increment(ref calls)>1) { entered.Set();release.WaitOne(2000); }return damage; }))
            {
                f=LmuParser.Parse(bytes,false);service.Apply(f,now,true);
                Assert(SpinWait.SpinUntil(()=>{service.Apply(f,now,true);return f.Damage.Any;},1500),"initial damage reading available");
                now=.6;service.Apply(f,now,true);Assert(entered.WaitOne(1500),"second damage request can stall independently");
                now=2.1;service.Apply(f,now,true);Assert(!f.Damage.Any && f.Tyres.Length==4,"expired REST data becomes unknown while native tyre readings remain");
                release.Set();
            }
        }
    }
    static void RenderDamage(string directory)
    {
        var f=PreviewData.Create(10);f.Connected=f.HasTelemetry=f.InRealtime=true;f.Stale=false;
        f.Tyres=Enumerable.Range(0,4).Select(i=>new TyreReading { Temperature=85+i*7,Optimal=90,Pressure=1.8+i*.04,Remaining=.98-i*.16 }).ToArray();
        f.Tyres[2].Flat=true;f.Damage=VehicleDamage.Parse("{\"suspensionDamage\":[0,0.06,0.29,0],\"vehicleDamage\":0.085}");
        foreach(bool live in new[]{true,false})
        {
            if(!live) f.Damage=new VehicleDamage();
            using(var p=new Painter()) using(var bmp=new Bitmap(420,402)) using(var g=Graphics.FromImage(bmp))
            { p.Widget(g,PanelKind.Tyres,f,new ViewOptions(),false);bmp.Save(Path.Combine(directory,live ? "pneus-danos.png" : "pneus-danos-indisponiveis.png"),ImageFormat.Png); }
        }
    }
}
