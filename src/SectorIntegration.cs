using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
namespace Arthur.LmuOverlay
{
 public sealed class SectorState {public string Phase="off",Message="Desconectado. Entre no treino offline para começar.";public bool Checkpoint;public int Sequence=-1,Restores,ResetAction;public string Host="";}
 public static class SectorProtocol
 {
  static XmlElement Parse(string s){if(string.IsNullOrWhiteSpace(s)||s.Length>131072)throw new InvalidDataException("Resposta do Sector indisponível.");var d=new XmlDocument{XmlResolver=null};using(var r=JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(s),new XmlDictionaryReaderQuotas{MaxDepth=24,MaxArrayLength=1024,MaxStringContentLength=131072}))d.Load(r);if(d.DocumentElement==null||d.DocumentElement.GetAttribute("type")!="object")throw new InvalidDataException("Formato do Sector desconhecido.");return d.DocumentElement;}
  static string Value(XmlNode n,string path,string type){var p=n.SelectSingleNode(path);return p!=null&&p.Attributes["type"]!=null&&p.Attributes["type"].Value==type?p.InnerText:"";}
  public static bool AllowsReset(string s){var r=Parse(s);string mode=Value(r,"state/gameSession","string");return Value(r,"state/appBuild","number")=="14200"&&Value(r,"state/settingMode","string")=="SETTING_GRANDPRIX"&&new[]{"PRACTICE1","PRACTICE2","PRACTICE3","PRACTICE4"}.Contains(mode);}
  public static SectorState Status(string s){var r=Parse(s);int sequence,count;if(Value(r,"schema_version","number")!="1"||!int.TryParse(Value(r,"sequence","number"),out sequence)||sequence<0)throw new InvalidDataException("Status do Sector incompatível.");string phase=Value(r,"phase","string");if(!new[]{"waiting","starting","active","paused","stopping","stopped","error"}.Contains(phase))throw new InvalidDataException("Estado do Sector desconhecido.");var state=new SectorState{Phase=phase,Sequence=sequence,Checkpoint=(phase=="active"||phase=="paused")&&Value(r,"checkpoint","boolean")=="true",Message=Value(r,"message","string")};state.Host=Value(r,"log_prefix","string");int reset;if(int.TryParse(Value(r,"reset_action","number"),out reset))state.ResetAction=reset;
   if(int.TryParse(Value(r,"restore_count","number"),out count)&&count>=0)state.Restores=count;if(Value(r,"blocked_reason","string")=="eac"){state.Phase="error";state.Checkpoint=false;state.Message="Easy Anti-Cheat detectado. Retorno desativado.";}return state;}
  public static string Describe(SectorState s){return s.Phase=="paused"?"Treino pausado. Aguardando pilotagem.":s.Phase=="stopping"?"Desconectando o treino…":s.Phase=="waiting"?"Aguardando pilotagem no treino offline.":s.Phase=="starting"?"Conectando ao treino…":s.Phase=="active"?(s.Checkpoint?"Ponto salvo • retornos: "+s.Restores:"Conectado. Salve um ponto com F6 ou o botão vinculado."):s.Message;}
 }
 public static class SectorRuntime
 {
  public const string Archive="SectorRuntime-1.0.0.zip",Digest="ee2402dda87da911faecc7e8771567eb8f015c8c6ff403814ddf988d80a0cb5e";
  static string Hash(Stream f){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
  public static string SafePath(string root,string name){if(string.IsNullOrEmpty(name)||name.Contains(":")||name.Contains("\\")||name.StartsWith("/")||name.Split('/').Any(x=>x==".."||x=="."))throw new InvalidDataException("Caminho inválido do Sector.");string p=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar)));if(!p.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Caminho fora do Sector.");return p;}
  public static string Prepare(string archive,string storage)
  {
   string dest=Path.Combine(storage,"1.0.0-"+Digest.Substring(0,12));
   using(var f=File.OpenRead(archive)){if(Hash(f)!=Digest)throw new InvalidDataException("Sector alterado ou incompleto. Reinstale o Arts.");f.Position=0;using(var zip=new ZipArchive(f,ZipArchiveMode.Read)){
    bool exists=Directory.Exists(dest);string output=exists?dest:dest+"-"+Guid.NewGuid().ToString("N")+".tmp";long total=0;
    try{foreach(var e in zip.Entries){if(string.IsNullOrEmpty(e.Name))continue;total+=e.Length;if(total>209715200||e.Length>67108864)throw new InvalidDataException("Sector excede limite.");string path=SafePath(output,e.FullName);if(exists){if(!File.Exists(path))throw new InvalidDataException("Sector incompleto. Consulte o diagnóstico.");using(var a=e.Open())using(var b=File.OpenRead(path))if(Hash(a)!=Hash(b))throw new InvalidDataException("Sector alterado. Consulte o diagnóstico.");}else{Directory.CreateDirectory(Path.GetDirectoryName(path));using(var a=e.Open())using(var b=File.Create(path))a.CopyTo(b);}}if(!exists)Directory.Move(output,dest);}finally{if(!exists&&Directory.Exists(output))Directory.Delete(output,true);}
   }}return dest;
  }
 }
 public sealed class SectorAdmission {public bool Allowed,Transient;public int Pid;public string Identity="",Reason="";}
 public static class SectorAccess
 {
  public static SectorAdmission Read(){return Retry(ReadOnce,Thread.Sleep);}
  internal static SectorAdmission Retry(Func<SectorAdmission> read,Action<int> delay)
  {
   SectorAdmission result=null;
   for(int attempt=0;attempt<3;attempt++) { result=read();if(!result.Transient)return result;if(attempt<2)delay(150); }
   return result;
  }
  static SectorAdmission ReadOnce()
  {
   var result=new SectorAdmission();int count=0;bool eac=false;
   foreach(var p in Process.GetProcesses())using(p)try{string name=p.ProcessName;if(name.IndexOf("EasyAntiCheat",StringComparison.OrdinalIgnoreCase)>=0)eac=true;if(name.Equals("Le Mans Ultimate",StringComparison.OrdinalIgnoreCase)){count++;result.Pid=p.Id;result.Identity=p.Id+"/"+p.StartTime.ToUniversalTime().Ticks;}}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){eac=true;}
   if(eac){result.Reason="Easy Anti-Cheat detectado ou processos não verificáveis. Retorno desativado.";return result;}if(count!=1){result.Reason="Abra somente uma instância do LMU em treino offline.";return result;}
   try{var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:6397/navigation/state");req.Proxy=null;req.AllowAutoRedirect=false;req.Timeout=req.ReadWriteTimeout=1600;req.UseDefaultCredentials=false;using(var response=(HttpWebResponse)req.GetResponse()){if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>131072)throw new InvalidDataException("Sessão indisponível.");using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){var chars=new char[131073];int n=0,k;while(n<chars.Length&&(k=reader.Read(chars,n,chars.Length-n))>0)n+=k;if(n==chars.Length)throw new InvalidDataException("Sessão excede limite.");result.Allowed=SectorProtocol.AllowsReset(new string(chars,0,n));}}result.Reason=result.Allowed?"Treino compatível.":"Exige treino offline e LMU build 14200 / Steam 25661166.";}catch(Exception e){result.Transient=e is WebException||e is IOException;result.Reason="Não foi possível confirmar o treino: "+e.Message;}return result;
  }
 }
 public interface ISectorChild:IDisposable {bool Exited{get;}int ExitCode{get;}}
 public sealed class SectorChild:ISectorChild
 {
  readonly Process process;SectorChild(Process p){process=p;}public bool Exited{get{return process.HasExited;}}public int ExitCode{get{return process.ExitCode;}}
  static string Q(string s){return "\""+s.Replace("\"","")+"\"";}
  public static ISectorChild Start(string root,string session,int pid)
  {
   var p=new Process{StartInfo=new ProcessStartInfo{FileName=Path.Combine(root,"python","python.exe"),WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,Arguments=Q(Path.Combine(root,"scripts","sector.py"))+" runtime.start_reset_hotkeys --pid "+pid+" --save-key F6 --restore-key F7 --gui-session "+Q(session)+" --offline-launch-confirmed --allow-writes"}};
   object gate=new object();string path=Path.Combine(session,"listener.log");DataReceivedEventHandler read=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;try{lock(gate){if(!File.Exists(path)||new FileInfo(path).Length<4194304)File.AppendAllText(path,e.Data+Environment.NewLine,Encoding.UTF8);}}catch(IOException){}catch(UnauthorizedAccessException){}};p.OutputDataReceived+=read;p.ErrorDataReceived+=read;
   try{p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();return new SectorChild(p);}catch{p.Dispose();throw;}
  }
  public void Dispose(){process.Dispose();}
 }
 public sealed class SectorEngine:IDisposable
 {
  readonly Func<SectorAdmission> access;readonly Func<string> prepare;readonly Func<string,string,int,ISectorChild> launch;readonly string sessions;readonly Action<string,Exception> log;readonly object gate=new object();SectorState state=new SectorState();int busy,stop,disposed;
  SectorButtonCommand pendingCommand;
  internal Func<int,bool> ForegroundCheck=TrainingForeground.Matches;
  public bool RequestWheelAction(string action)
  {
   var command=new SectorButtonCommand{id=Guid.NewGuid().ToString("N"),action=action,host=State.Host,created_at_ms=SectorButtonCommand.Now};
   if(!Busy||Volatile.Read(ref stop)!=0||Volatile.Read(ref disposed)!=0||!command.Valid(State,SectorButtonCommand.Now))return false;
   return Interlocked.CompareExchange(ref pendingCommand,command,null)==null;
  }
  public event Action Restored;public bool Busy{get{return Volatile.Read(ref busy)!=0;}}public SectorState State{get{return Interlocked.CompareExchange(ref state,null,null);}}
  public SectorEngine(string install,string data,Action<string,Exception> error):this(SectorAccess.Read,()=>SectorRuntime.Prepare(Path.Combine(install,SectorRuntime.Archive),Path.Combine(data,"SectorRuntime")),SectorChild.Start,Path.Combine(data,"TreinoSector"),error){}
  internal SectorEngine(Func<SectorAdmission> a,Func<string> unpack,Func<string,string,int,ISectorChild> start,string dir,Action<string,Exception> error){access=a;prepare=unpack;launch=start;sessions=dir;log=error;}
  void Publish(string phase,string message){Interlocked.Exchange(ref state,new SectorState{Phase=phase,Message=message});}
  public bool Start(){return Start("");}
  public bool Start(string expectedIdentity){lock(gate){if(busy!=0||disposed!=0)return false;busy=1;stop=0;Publish("checking","Verificando treino e componentes…");new Thread(()=>Run(expectedIdentity)){IsBackground=true,Name="Arts Sector supervisor"}.Start();return true;}}
  public void RequestStop(){Interlocked.Exchange(ref stop,1);Interlocked.Exchange(ref pendingCommand,null);}
  static void Touch(string path){using(var f=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.ReadWrite)){}}
  void Run(string expectedIdentity)
  {
   ISectorChild child=null;string dir="",reason="";bool failed=false;int sequence=-1,restores=0;var statusReader=new SectorStatusReader();
   try{
    var first=access();if(!first.Allowed)throw new InvalidOperationException(first.Reason);if(!string.IsNullOrEmpty(expectedIdentity)&&first.Identity!=expectedIdentity)throw new InvalidOperationException("Jogo reiniciado. Conecte novamente.");if(Volatile.Read(ref stop)!=0)return;string root=prepare();var check=access();if(!check.Allowed||check.Identity!=first.Identity)throw new InvalidOperationException(check.Allowed?"Jogo reiniciado. Conecte novamente.":check.Reason);if(Volatile.Read(ref stop)!=0)return;
    dir=Path.Combine(sessions,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);Touch(Path.Combine(dir,"heartbeat"));child=launch(root,dir,check.Pid);Publish("waiting","");DateTime next=DateTime.UtcNow.AddSeconds(1);
    while(!child.Exited){Touch(Path.Combine(dir,"heartbeat"));if(Volatile.Read(ref stop)==0&&(DateTime.UtcNow>=next||Interlocked.CompareExchange(ref pendingCommand,null,null)!=null)){next=DateTime.UtcNow.AddSeconds(1);check=access();if(!check.Allowed||check.Identity!=first.Identity){reason=check.Allowed?"Jogo reiniciado. Conecte novamente.":check.Reason;failed=true;RequestStop();}}
     if(Volatile.Read(ref stop)!=0){Touch(Path.Combine(dir,"stop"));Publish("stopping","Desconectando com segurança…");}
     else{string path=Path.Combine(dir,"status.json");var fresh=statusReader.Read(path,DateTime.UtcNow);if(fresh!=null){if(fresh.Sequence>sequence){sequence=fresh.Sequence;Interlocked.Exchange(ref state,fresh);if(fresh.Restores>restores){var notify=Restored;if(notify!=null)notify();}restores=fresh.Restores;if(fresh.Phase=="error"){reason=fresh.Message;failed=true;RequestStop();}}}
      var command=Interlocked.Exchange(ref pendingCommand,null);
      if(fresh!=null&&command!=null&&Volatile.Read(ref stop)==0&&command.Valid(State,SectorButtonCommand.Now)&&ForegroundCheck(first.Pid))
       try{SectorButtonCommand.Write(dir,command);}catch(IOException e){if(log!=null)log("Comando do volante não enviado",e);}catch(UnauthorizedAccessException e){if(log!=null)log("Comando do volante não enviado",e);}
     }Thread.Sleep(40);
    }if(child.ExitCode!=0){failed=true;if(reason.Length==0)reason="Sector não conectou. Consulte listener.log em Diagnósticos.";}
   }catch(Exception e){failed=true;reason=e.Message;if(log!=null)log("Sector: "+dir,e);if(child!=null){try{Touch(Path.Combine(dir,"stop"));}catch(IOException){}catch(UnauthorizedAccessException){}Publish("stopping","Aguardando desconexão do Sector…");while(!child.Exited)Thread.Sleep(250);}}
   finally{Interlocked.Exchange(ref pendingCommand,null);if(child!=null)child.Dispose();Publish(failed?"error":"off",failed?reason:"Desconectado. O ponto salvo foi descartado.");Interlocked.Exchange(ref busy,0);}
  }
  public void Dispose(){Interlocked.Exchange(ref disposed,1);RequestStop();}
 }
}
