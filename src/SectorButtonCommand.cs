using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
namespace Arthur.LmuOverlay
{
 [DataContract] public sealed class SectorButtonCommand
 {
  [DataMember] public int schema_version=1;
  [DataMember] public string id,action,host;
  [DataMember] public long created_at_ms;
  public static long Now {get{return (long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds;}}
  public bool Valid(SectorState state,long now){return schema_version==1&&(action=="save"||action=="restore")&&
   !string.IsNullOrEmpty(host)&&host==state.Host&&state.Phase=="active"&&state.ResetAction==0&&
   (action!="restore"||state.Checkpoint)&&now>=created_at_ms&&now-created_at_ms<=1000;}
  public static void Write(string directory,SectorButtonCommand command)
  {
   string path=Path.Combine(directory,"command.json"),temp=Path.Combine(directory,"command-"+Guid.NewGuid().ToString("N")+".tmp");
   if(File.Exists(path))return;
   try{using(var stream=File.Create(temp))new DataContractJsonSerializer(typeof(SectorButtonCommand)).WriteObject(stream,command);File.Move(temp,path);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 internal static class TrainingForeground
 {
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  public static bool Matches(int pid){if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return false;uint current;GetWindowThreadProcessId(GetForegroundWindow(),out current);return current==(uint)pid;}
 }
}
