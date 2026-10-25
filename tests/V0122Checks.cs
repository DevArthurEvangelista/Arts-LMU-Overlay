using System;
using System.IO;
using System.Threading;
using Arthur.LmuOverlay;
partial class Checks
{
 static void Version0122()
 {
  foreach(string phase in new[]{"paused","stopping","waiting","starting","active","stopped","error"})
  {
   var s=SectorProtocol.Status("{\"schema_version\":1,\"sequence\":2,\"phase\":\""+phase+"\",\"checkpoint\":true,\"restore_count\":1}");
   Assert(s.Phase==phase,"recognize real Sector phase "+phase);
  }
  Reject(()=>SectorProtocol.Status("{\"schema_version\":1,\"sequence\":2,\"phase\":\"unexpected\"}"),"unrecognized Sector protocol remains rejected");
  int reads=0,delays=0;
  var admission=SectorAccess.Retry(()=>++reads<3?new SectorAdmission{Transient=true}:new SectorAdmission{Allowed=true},ms=>delays++);
  Assert(admission.Allowed&&reads==3&&delays==2,"temporary navigation failures recover without disconnect");
  reads=0;admission=SectorAccess.Retry(()=>{reads++;return new SectorAdmission{Reason="EAC"};},ms=>{});
  Assert(!admission.Allowed&&reads==1,"EAC and incompatible contexts never delayed or allowed by retry");
  reads=0;admission=SectorAccess.Retry(()=>{reads++;return new SectorAdmission{Transient=true};},ms=>{});
  Assert(!admission.Allowed&&reads==3,"persistent navigation failure still blocks reset");
  var repo=new MemoryDelta();var delta=new DeltaTracker(repo);DriveLap(delta,0,100,0);
  var normal=DFrame(110,100,100,1);delta.Apply(normal);
  var excursion=DFrame(110.25,100,96,1);delta.Apply(excursion);
  Assert(!double.IsNaN(excursion.Delta.Values[0]),"ordinary backwards movement does not disable live comparison for the lap");
  var invalid=DFrame(110.5,100,100,1);invalid.LapInvalid=true;delta.Apply(invalid);
  Assert(invalid.Delta.Invalid&&!double.IsNaN(invalid.Delta.Values[0]),"officially invalid lap still shows time loss against existing reference");
  var unknown=DFrame(110.75,100,104,1);unknown.Drivers[0].CountLapFlag=-1;delta.Apply(unknown);
  Assert(!unknown.Delta.Invalid,"unknown scoring flag is not labelled an invalid lap");
  var reset=DFrame(111,100,20,1);reset.TrainingReset=true;delta.Apply(reset);
  Assert(double.IsNaN(reset.Delta.Values[0]),"confirmed F7 still suspends invalid clock comparison");
  var jitterRepo=new MemoryDelta();var jitter=new DeltaTracker(jitterRepo);
  for(int i=0;i<400;i++){
   double t=i*.25;var f=DFrame(t,0,t*10);if(i%20==2)f.Drivers[0].LapDistance=(t-.25)*10-.02;
   if(i<4)f.Drivers[0].CountLapFlag=0;
   jitter.Apply(f);
  }
  var finish=DFrame(100,100,0,1,100);jitter.Apply(finish);
  Assert(jitterRepo.Saved!=null&&finish.Delta.ReferenceTimes[0]==100,"small scoring jitter and initial uninitialized count flag do not discard a complete valid reference");
  var visibility=new OverlayVisibility();var visible=DFrame(10,0,100);visible.ReadAt=10;
  Assert(visibility.ShouldShow(true,visible,10,false,true)&&!visibility.ShouldShow(true,visible,10,false,false),"Alt-Tab hides enabled delta and all sibling cards");
  visibility.Toggle(visible,10,false);
  Assert(!visibility.ShouldShow(true,visible,10,false,true),"returning focus preserves F8 hidden state");
  Assert(visibility.ShouldShow(true,visible,10,true,false),"positioning remains available outside game focus");
  using(var painter=new Painter())using(var thin=new System.Drawing.Bitmap(460,90))using(var thick=new System.Drawing.Bitmap(460,90)){
   visible.Delta=new DeltaState{Values=new[]{-.5,-.5,-.5}};
   using(var g=System.Drawing.Graphics.FromImage(thin))painter.DeltaBar(g,visible,new ViewOptions{DeltaThickness=6},false);
   using(var g=System.Drawing.Graphics.FromImage(thick))painter.DeltaBar(g,visible,new ViewOptions{DeltaThickness=28},false);
   Assert(thin.GetPixel(100,60).A==0&&thick.GetPixel(100,60).A>0,"thickness setting changes actual rendered bar");
   int top=0;for(int y=0;y<15;y++)for(int x=0;x<460;x++)top+=thick.GetPixel(x,y).A;
   Assert(top==0,"normal delta has no reference/invalidity header text");
  }
  string temp=Path.Combine(Path.GetTempPath(),"sector0122-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   FakeSector child=null;var access=new SectorAdmission{Allowed=true,Identity="42/a",Pid=42};
   using(var engine=new SectorEngine(()=>access,()=>temp,(r,w,p)=>child=new FakeSector{Directory=w},temp,(m,e)=>{})){
    int restored=0;engine.Restored+=()=>Interlocked.Increment(ref restored);engine.Start();Assert(SpinWait.SpinUntil(()=>child!=null,2000),"test backend started");
    string path=Path.Combine(child.Directory,"status.json");
    File.WriteAllText(path,"{\"schema_version\":1,\"sequence\":1,\"phase\":\"paused\",\"checkpoint\":true,\"restore_count\":1}");
    Assert(SpinWait.SpinUntil(()=>restored==1,2000)&&engine.Busy&&engine.State.Phase=="paused","pause following restore keeps supervision active and signals reset");
    File.WriteAllText(path,"{\"schema_version\":1,\"sequence\":2,\"phase\":\"active\",\"checkpoint\":true,\"restore_count\":1}");
    Assert(SpinWait.SpinUntil(()=>engine.State.Phase=="active",2000)&&restored==1,"resume does not repeat restore event");engine.RequestStop();Assert(SpinWait.SpinUntil(()=>!engine.Busy,2000),"disconnect still stops backend cooperatively");
   }
  }finally{Directory.Delete(temp,true);}
 }
}
