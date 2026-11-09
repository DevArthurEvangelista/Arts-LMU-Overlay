using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
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
  }
 }
}
