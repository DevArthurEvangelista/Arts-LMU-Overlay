using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
namespace Arthur.LmuOverlay
{
 public static class DeltaMode { public static readonly string[] Names={"Melhor da sessão","Melhor pessoal","Última volta válida"};public static int Normalize(int n){return Math.Max(0,Math.Min(2,n));} }
 [DataContract] public sealed class DeltaProfile
 {
  [DataMember] public string Track="",Car="",Class="",Player="";[DataMember] public double Length;
  static string Part(string s){s=(s??"").Trim().ToUpperInvariant();return s.Length+":"+s;}
  public string Key { get { using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes("arts-delta-1/"+Part(Track)+Part(Class)+Part(Car)+Part(Player)+Math.Round(Length).ToString(CultureInfo.InvariantCulture)))).Replace("-","").ToLowerInvariant(); } }
  public bool Valid {get{return !string.IsNullOrWhiteSpace(Track)&&!string.IsNullOrWhiteSpace(Car)&&!string.IsNullOrWhiteSpace(Class)&&Format.Finite(Length)&&Length>=100&&Length<=100000;}}
  public string Label {get{return Track+" • "+Class+" • "+Car;}}
  public static DeltaProfile From(Frame f,Driver d){return new DeltaProfile{Track=f.Track,Car=string.IsNullOrWhiteSpace(d.CarName)?(string.IsNullOrWhiteSpace(f.VehicleModel)?d.VehicleKey:f.VehicleModel):d.CarName,Class=d.Class,Player=d.Name,Length=f.TrackLength};}
 }
 [DataContract] public sealed class DeltaPoint {[DataMember] public double Distance,Time;}
 [DataContract] public sealed class DeltaLap
 {
  [DataMember] public int Version=1;[DataMember] public DeltaProfile Profile;[DataMember] public double Time;[DataMember] public DeltaPoint[] Points;
  public bool IsValid(){if(Version!=1||Profile==null||!Profile.Valid||!Format.Finite(Time)||Time<=1||Time>7200||Points==null||Points.Length<3||Points.Length>20000)return false;double d=-1,t=-1;foreach(var p in Points){if(p==null||!Format.Finite(p.Distance)||!Format.Finite(p.Time)||p.Distance<=d||p.Time<=t||p.Distance>Profile.Length||p.Time>Time)return false;d=p.Distance;t=p.Time;}return Points[0].Distance==0&&Points[0].Time==0&&Math.Abs(d-Profile.Length)<.001&&Math.Abs(t-Time)<.001;}
  public double At(double d){if(!Format.Finite(d)||d<0||d>Profile.Length)return double.NaN;int lo=0,hi=Points.Length-1;while(hi-lo>1){int m=(lo+hi)/2;if(Points[m].Distance<=d)lo=m;else hi=m;}var a=Points[lo];var b=Points[hi];return a.Time+(b.Time-a.Time)*(d-a.Distance)/(b.Distance-a.Distance);}
  public static DeltaLap From(LearningLap lap,DeltaProfile p){if(lap==null||!lap.Info.IsPlayer||!lap.Info.Valid||!lap.Info.StartObserved||lap.Info.HasGaps)return null;var points=new List<DeltaPoint>();foreach(var s in lap.Samples){if(s.BreakBefore)return null;if(points.Count>0&&s.Distance<=points[points.Count-1].Distance)continue;points.Add(new DeltaPoint{Distance=s.Distance,Time=s.Time});}if(points.Count>0)points[points.Count-1].Time=lap.Info.Time;var result=new DeltaLap{Profile=p,Time=lap.Info.Time,Points=points.ToArray()};return result.IsValid()?result:null;}
 }
 public sealed class DeltaState
 {
  public string Key="",Context="",Reason="Complete uma volta válida.",Storage="";public bool Invalid;
  public double[] Values={double.NaN,double.NaN,double.NaN},ReferenceTimes={double.NaN,double.NaN,double.NaN};
  public DeltaState Copy(){var r=(DeltaState)MemberwiseClone();r.Values=(double[])Values.Clone();return r;}
 }
 public sealed class DeltaTracker
 {
  readonly IDeltaRepository repository;LearningRecorder recorder;DeltaProfile profile;DeltaLap best,personal,previous;
  string session="",key="";double lastTime=-1,lastStart=-1,lastDistance=double.NaN;bool broken,displayBroken;int revision=-1;
  public DeltaTracker(IDeltaRepository repo){repository=repo;}
  void NewRecorder(){recorder=new LearningRecorder(s=>{},Complete);}
  void Complete(LearningLap lap){var r=DeltaLap.From(lap,profile);if(r==null)return;previous=r;if(best==null||r.Time<best.Time)best=r;if(personal==null||r.Time<personal.Time){personal=r;repository.Save(r);}}
  public void Retry(){var p=Interlocked.CompareExchange(ref personal,null,null);if(p!=null)repository.Save(p);else{var context=Interlocked.CompareExchange(ref profile,null,null);if(context!=null)repository.Load(context);}}
  public void Apply(Frame f)
  {
   var d=f.Drivers.FirstOrDefault(x=>x.IsPlayer);
   if(!f.Connected||!f.HasTelemetry||f.Stale||f.ScoringStale||!f.InRealtime||f.Phase==9||f.Demo||d==null||d.InGarage||d.InPits){if(recorder!=null)recorder.Break();f.Delta=new DeltaState{Reason="Aguardando pilotagem.",Context=profile==null?"":profile.Label,Storage=repository.Latest.Status};return;}
   var incoming=DeltaProfile.From(f,d);
   if(!incoming.Valid||!Format.Finite(f.Elapsed)||!Format.Finite(d.LapStart)||d.LapStart<0||d.LapStart>f.Elapsed||!Format.Finite(d.LapDistance)||d.LapDistance<0||d.LapDistance>f.TrackLength){if(recorder!=null)recorder.Break();f.Delta=new DeltaState{Reason="Aguardando posição na pista."};return;}
   string next=incoming.Key;
   if(key!=next||session!=f.SessionKey){bool change=key!=next;key=next;profile=incoming;session=f.SessionKey;best=previous=null;lastTime=lastStart=-1;lastDistance=double.NaN;broken=displayBroken=false;NewRecorder();if(change){personal=null;revision=-1;repository.Load(profile);}}
   var stored=repository.Latest;if(stored.Key==key&&stored.Revision!=revision){revision=stored.Revision;if(stored.Lap!=null&&(personal==null||stored.Lap.Time<personal.Time))personal=stored.Lap;}
   if(lastTime>=0&&f.Elapsed<lastTime-.01){NewRecorder();lastTime=-1;broken=displayBroken=true;lastStart=d.LapStart;}
   if(lastTime>=0&&Math.Abs(lastStart-d.LapStart)>.01)broken=displayBroken=false;
   if(lastTime>=0&&f.Elapsed>lastTime&&Math.Abs(lastStart-d.LapStart)<=.01){double dt=f.Elapsed-lastTime,dd=d.LapDistance-lastDistance;if(dt>1||dd< -2||dd>40+150*dt)broken=true;if(dd>40+150*dt||dd< -Math.Max(40,150*dt))displayBroken=true;}
   if(f.TrainingReset){broken=displayBroken=true;recorder.Break();}
   if(f.Elapsed>lastTime){var capture=LearningFrame.Capture(f);capture.Cars=capture.Cars.Where(x=>x.IsPlayer).ToArray();
    foreach(var car in capture.Cars){
     // Distance can jitter backwards by centimetres while scoring catches up.
     if(Math.Abs(lastStart-d.LapStart)<=.01&&Format.Finite(lastDistance)&&car.Distance<lastDistance&&lastDistance-car.Distance<=2)car.Distance=lastDistance;
     // The scoring flag may still be uninitialized immediately after crossing.
     if(car.CountLap==0&&f.Elapsed-car.Start<=1&&!car.Invalid)car.CountLap=2;
    }
    capture.Dropped|=broken;recorder.Accept(capture);lastTime=f.Elapsed;lastStart=d.LapStart;lastDistance=capture.Cars.Length>0?capture.Cars[0].Distance:d.LapDistance;}
   var refs=new[]{best,personal,previous};var state=new DeltaState{Key=session+"/"+key+"/"+d.LapStart.ToString(CultureInfo.InvariantCulture),Context=profile.Label,Invalid=f.LapInvalid||d.CountLapFlag==1,Reason=displayBroken?"Trecho interrompido. Inicie outra volta.":"Complete uma volta válida.",Storage=stored.Key==key?stored.Status:"Carregando melhor pessoal…"};
   for(int i=0;i<3;i++)if(refs[i]!=null){state.ReferenceTimes[i]=refs[i].Time;if(!displayBroken)state.Values[i]=f.Elapsed-d.LapStart-refs[i].At(d.LapDistance);}f.Delta=state;
  }
 }
 public sealed class DeltaMotion
 {
  double value=double.NaN,last=-1;string key="";int mode=-1;
  public DeltaState Step(DeltaState state,int selection,double now,bool smooth){selection=DeltaMode.Normalize(selection);var r=state.Copy();double target=state.Values[selection];if(!smooth||!Format.Finite(target)||!Format.Finite(value)||mode!=selection||key!=state.Key||last<0||now<last||now-last>1)value=target;else value+=(target-value)*(1-Math.Exp(-(now-last)/.06));r.Values[selection]=value;last=now;mode=selection;key=state.Key;return r;}
 }
}
