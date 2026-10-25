using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using Arthur.LmuOverlay;
partial class Checks
{
 sealed class FakeButtonReader:IWheelReader,IWheelButtonReader
 {
  public List<WheelButton> Buttons{get{return new List<WheelButton>{ButtonSample("wheel-a",1,false)};}}
  public List<WheelAxis> Read(IntPtr owner,double now){return new List<WheelAxis>();}
  public void Refresh(){}public void Dispose(){}
 }
 static WheelButton ButtonSample(string device,uint id,bool down){return new WheelButton{DeviceId=device,DeviceName="Volante de teste",ObjectId=id,Number=(int)id,Pressed=down};}
 sealed class SharingError:IOException {public SharingError(int code){HResult=unchecked((int)0x80070000)|code;}}
 static void StatusSharingChecks()
 {
  string json="{\"schema_version\":1,\"sequence\":5,\"phase\":\"active\",\"checkpoint\":true}";
  var now=DateTime.UtcNow;int code=32;
  var reader=new SectorStatusReader(p=>{if(code!=0)throw new SharingError(code);return json;});
  Assert(reader.Read("unused",now)==null,"sharing violation defers status instead of stopping training");
  Assert(reader.Read("unused",now.AddSeconds(2))==null,"brief sharing contention tolerated");
  code=0;Assert(reader.Read("unused",now.AddSeconds(2.5)).Checkpoint,"checkpoint recovered after sharing contention");
  code=33;Assert(reader.Read("unused",now.AddSeconds(10))==null,"recovery resets contention deadline; lock violation tolerated");
  bool failed=false;try{reader.Read("unused",now.AddSeconds(13));}catch(IOException){failed=true;}
  Assert(failed,"persistent contention fails within bounded deadline");
  code=5;failed=false;try{reader.Read("unused",now);}catch(IOException){failed=true;}
  Assert(failed,"unrelated I/O errors are not silently swallowed");
  Reject(()=>new SectorStatusReader(p=>"{}").Read("unused",now),"invalid protocol still rejected immediately");
  Assert(new SectorStatusReader(p=>{throw new FileNotFoundException();}).Read("unused",now)==null,"not-yet-published status tolerated");
  string path=Path.GetTempFileName();try{
   File.WriteAllText(path,json);
   using(var writer=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite|FileShare.Delete))
    Assert(new SectorStatusReader().Read(path,now).Sequence==5,"actual shared status file read succeeds with another handle open");
  }finally{File.Delete(path);}
 }
 static void Version0123()
 {
  StatusSharingChecks();
  using(var pump=new WheelPump(()=>0,()=>new FakeButtonReader(),(m,e)=>{})){
   pump.Start(IntPtr.Zero);Assert(SpinWait.SpinUntil(()=>pump.ReadButtons(0).Count==1,2000),"button snapshots arrive through background reader without axes");
   Assert(pump.ReadButtons(1).Count==0,"stale USB snapshot cannot trigger a command");
  }
  var save=TrainingWheelBinding.From(ButtonSample("wheel-a",1,false));var restore=TrainingWheelBinding.From(ButtonSample("wheel-a",2,false));
  var edges=new TrainingButtonEdges();Func<bool,bool,List<WheelButton>> sample=(a,b)=>new List<WheelButton>{ButtonSample("wheel-a",1,a),ButtonSample("wheel-a",2,b)};
  Assert(edges.Step(sample(true,false),save,restore,true)=="","held button on activation never fires");
  edges.Step(sample(false,false),save,restore,true);
  Assert(edges.Step(sample(true,false),save,restore,true)=="save","new press requests save");
  Assert(edges.Step(sample(true,false),save,restore,true)=="","held save does not repeat");
  edges.Step(sample(false,false),save,restore,true);
  Assert(edges.Step(sample(false,true),save,restore,true)=="restore","new restore press recognized");
  Assert(edges.Step(sample(false,false),save,restore,false)==""&&edges.Step(sample(true,false),save,restore,false)=="","normal/disconnected game never produces actions");
  Assert(edges.Step(sample(true,false),save,restore,true)=="","reentering training while held cannot trigger");
  edges.Step(sample(false,false),save,restore,true);
  Assert(edges.Step(sample(true,true),save,restore,true)=="","simultaneous bindings are rejected as ambiguous");
  edges.Step(new List<WheelButton>(),save,restore,true);
  Assert(edges.Step(sample(true,false),save,restore,true)=="","reconnect held button ignored");
  Assert(!save.Matches(ButtonSample("wheel-b",1,true)),"different device with same button number cannot trigger binding");
  Assert(save.Same(TrainingWheelBinding.From(ButtonSample("wheel-a",1,true)))&&!save.Same(restore),"duplicate binding detection uses device and object identity");
  var state=SectorProtocol.Status("{\"schema_version\":1,\"sequence\":1,\"phase\":\"active\",\"checkpoint\":true,\"log_prefix\":\"host-test\",\"reset_action\":0}");
  var command=new SectorButtonCommand{id="abc",host="host-test",action="restore",created_at_ms=1000};
  Assert(command.Valid(state,1200),"desktop request valid for acknowledged checkpoint in current host");
  Assert(!command.Valid(state,2001)&&!command.Valid(state,999),"stale/future requests rejected");
  state.Checkpoint=false;Assert(!command.Valid(state,1200),"restore requires checkpoint");command.action="save";Assert(command.Valid(state,1200),"save allowed without checkpoint");
  state.ResetAction=3;Assert(!command.Valid(state,1200),"native pending action blocks another command");state.ResetAction=0;state.Host="other-host";Assert(!command.Valid(state,1200),"request cannot cross native host generations");
  state.Host="host-test";state.Phase="paused";Assert(!command.Valid(state,1200),"paused session never admits button command");
  string temp=Path.Combine(Path.GetTempPath(),"arts-buttons-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   SectorButtonCommand.Write(temp,command);SectorButtonCommand written;
   using(var f=File.OpenRead(Path.Combine(temp,"command.json")))written=(SectorButtonCommand)new DataContractJsonSerializer(typeof(SectorButtonCommand)).ReadObject(f);
   Assert(written.action=="save"&&written.host==command.host&&written.created_at_ms==1000&&written.schema_version==1,"native JSON protocol fields serialize exactly");
   SectorButtonCommand.Write(temp,new SectorButtonCommand{action="restore"});
   Assert(File.ReadAllText(Path.Combine(temp,"command.json")).Contains("save"),"existing unconsumed request is never overwritten");
   File.Delete(Path.Combine(temp,"command.json"));
   FakeSector child=null;var access=new SectorAdmission{Allowed=true,Identity="42/a",Pid=42};
   using(var engine=new SectorEngine(()=>access,()=>temp,(r,w,p)=>child=new FakeSector{Directory=w},temp,(m,e)=>{})){
    engine.ForegroundCheck=pid=>pid==42;
    Assert(!engine.RequestWheelAction("save"),"disconnected engine refuses button");engine.Start();
    Assert(SpinWait.SpinUntil(()=>child!=null,2000),"button test session started");
    File.WriteAllText(Path.Combine(child.Directory,"status.json"),"{\"schema_version\":1,\"sequence\":1,\"phase\":\"active\",\"checkpoint\":false,\"log_prefix\":\"host-test\",\"reset_action\":0}");
    Assert(SpinWait.SpinUntil(()=>engine.State.Phase=="active",2000),"backend acknowledges active host");
    Assert(!engine.RequestWheelAction("restore")&&!engine.RequestWheelAction("arbitrary"),"invalid action/unsaved restore blocked");
    Assert(engine.RequestWheelAction("save"),"active session accepts save button");
    string path=Path.Combine(child.Directory,"command.json");Assert(SpinWait.SpinUntil(()=>File.Exists(path),2000),"worker sends command without UI disk operation");
    File.Delete(path);engine.ForegroundCheck=pid=>false;engine.RequestWheelAction("save");Thread.Sleep(150);Assert(!File.Exists(path),"focus lost before dispatch drops request");
    engine.RequestStop();Assert(!engine.RequestWheelAction("save"),"disconnect blocks queued/new button commands immediately");Assert(SpinWait.SpinUntil(()=>!engine.Busy,2000),"disconnect completes with commands disabled");
   }
  }finally{Directory.Delete(temp,true);}
 }
}
