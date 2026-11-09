using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
class VoiceHudChecks
{
 [STAThread] static void Main(string[] args)
 {
  var assembly=Assembly.LoadFrom(System.IO.Path.GetFullPath(args[0]));
  var type=assembly.GetType("Arthur.LmuOverlay.VoiceStatusOverlay",true);
  using(var hud=(Form)Activator.CreateInstance(type,true))
  using(var heartbeat=new Timer{Interval=50})
  {
   int ticks=0;bool gameActive=true;var context=new ApplicationContext();
   type.GetField("ShouldRemainVisible").SetValue(hud,new Func<bool>(()=>gameActive));
   var display=type.GetMethod("Display");
   display.Invoke(hud,new object[]{"Ouvindo","Teste real da janela",Color.Cyan,.6});
   heartbeat.Tick+=delegate {
    ticks++;
    if(ticks==25){
     if(hud.Visible)throw new Exception("Voice HUD did not expire while painting");
     Console.WriteLine("PASS: UI heartbeat and HUD expiry after 25 ticks");
     display.Invoke(hud,new object[]{"Ouvindo","Simulating game close",Color.Cyan,14.0});
    }
    if(ticks==28)gameActive=false;
    if(ticks==40){
     if(hud.Visible)throw new Exception("Voice HUD stayed visible after game close signal");
     Console.WriteLine("PASS: HUD hidden after game close signal");context.ExitThread();
    }
   };
   heartbeat.Start();Application.Run(context);
  }
  if(args.Length<2){
   var input=assembly.GetType("Arthur.LmuOverlay.VoiceChatKeys",true).GetNestedType("INPUT",BindingFlags.NonPublic);
   int size=Marshal.SizeOf(input);
   if(size!=(IntPtr.Size==8?40:28))throw new Exception("Invalid native INPUT size: "+size);
   Console.WriteLine("PASS: native SendInput ABI size "+size);
   var keys=assembly.GetType("Arthur.LmuOverlay.VoiceChatKeys",true);
   var events=new List<string>();bool focused=true;
   Action<string> copy=text=>events.Add("copy:"+text);
   Action<ushort,bool> key=(code,up)=>events.Add(code+(up?"up":"down"));
   Func<int,Task> immediate=ms=>Task.FromResult(0);
   if(!Sequence(keys,()=>focused,copy,key,immediate))throw new Exception("Clipboard sequence failed");
   if(string.Join(",",events)!="copy:test message,13down,13up,17down,86down,86up,17up,13down,13up")throw new Exception("Incorrect clipboard/key order");
   events.Clear();focused=true;
   if(Sequence(keys,()=>focused,copy,key,ms=>{focused=false;return Task.FromResult(0);}))throw new Exception("Lost focus must cancel paste");
   if(events.Contains("17down"))throw new Exception("Paste happened after focus loss");
   events.Clear();focused=true;int delays=0;
   if(Sequence(keys,()=>focused,copy,key,ms=>{if(++delays==2)focused=false;return Task.FromResult(0);}))throw new Exception("Lost focus must cancel submission");
   if(events.FindAll(x=>x=="13down").Count!=1)throw new Exception("Final Enter happened after focus loss");
   events.Clear();focused=true;
   try{Sequence(keys,()=>focused,copy,(code,up)=>{events.Add(code+(up?"up":"down"));if(code==86)throw new Exception("Input failure");},immediate);throw new Exception("Expected input failure");}
   catch(Exception){if(!events.Contains("17up"))throw new Exception("Ctrl was not released on input failure");}
   Console.WriteLine("PASS: clipboard sequence, cancellation before paste/submission and Ctrl cleanup");
  }
 }
 static bool Sequence(Type keys,Func<bool> focused,Action<string> copy,Action<ushort,bool> key,Func<int,Task> delay)
 {
  return ((Task<bool>)keys.GetMethod("SendSequence").Invoke(null,new object[]{"test message",focused,copy,key,delay})).GetAwaiter().GetResult();
 }
}
