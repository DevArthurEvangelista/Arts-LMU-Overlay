using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

// Uses a 30 Hz keyboard-state receiver: a tap with no down-time is invisible.
// This tests our timing contract, not whether LMU accepts external input.
class VoiceDeliveryChecks
{
 static readonly HashSet<ushort> held=new HashSet<ushort>();
 static int clock,nextPoll,enters,pastes;static string typed;static bool focused;
 static void Reset(){held.Clear();clock=0;nextPoll=17;enters=pastes=0;typed="";focused=true;}
 static void Key(ushort code,bool up){if(up)held.Remove(code);else held.Add(code);}
 static Task Delay(int ms){int end=clock+ms;while(nextPoll<=end){if(held.Contains(13))enters++;if(held.Contains(17)&&held.Contains(86))pastes++;nextPoll+=33;}clock=end;return Task.FromResult(0);}
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(Assembly assembly,bool baseline)
 {
  if(baseline){
   Reset();var keys=assembly.GetType("Arthur.LmuOverlay.VoiceChatKeys",true);
   var task=(Task<bool>)keys.GetMethod("SendSequence").Invoke(null,new object[]{"hello",new Func<bool>(()=>focused),new Action<string>(_=>{}),new Action<ushort,bool>(Key),new Func<int,Task>(Delay)});
   Assert(task.GetAwaiter().GetResult(),"Baseline did not attempt delivery");
   Assert(enters==0&&pastes==0,"Expected to reproduce zero-duration taps in 0.12.10");
   Console.WriteLine("REPRODUCED: 0.12.10 Enter and paste invisible to a 30 Hz receiver");return;
  }
  var type=assembly.GetType("Arthur.LmuOverlay.VoiceChatDelivery",true);
  for(int i=0;i<3;i++){
   Reset();Assert(Send(type,"Hello",true,Delay),"Paste failed");
   Assert(enters>=2&&pastes>0,"Receiver missed Enter/paste");Assert(held.Count==0,"Key stuck");
  }
  Reset();Assert(Send(type,"Hello",false,Delay),"Typing failed");
  Assert(typed=="Hello","Typed text differs: "+typed);Assert(pastes==0&&enters>=2,"Typing used paste or missed Enter");
  for(int cut=1;cut<=20;cut++){
   Reset();int calls=0;Send(type,"Hello",false,ms=>{Delay(ms);if(++calls==cut)focused=false;return Task.FromResult(0);});
   Assert(held.Count==0,"Held keys after cancellation at "+cut);
  }
  Reset();bool threw=false;
  try{Send(type,"Hello",false,ms=>{throw new InvalidOperationException("Injected timing failure");});}catch(InvalidOperationException){threw=true;}
  Assert(threw&&held.Count==0,"Failure left a key held");
  Reset();Assert(!Send(type,"",false,Delay)&&enters==0,"Empty message opened chat");
  Reset();Assert(!Send(type,new string('a',181),false,Delay)&&enters==0,"Oversized message opened chat");
  Console.WriteLine("PASS: timed input, three consecutive sends, typing without paste, cancellation and key cleanup");
 }
 static bool Send(Type type,string text,bool paste,Func<int,Task> delay)
 {
  Action<ushort,bool,bool> key=(code,up,unicode)=>{
   if(!up&&!focused)throw new Exception("Key pressed after focus loss");
   if(up&&held.Contains(code)&&code>=65&&code<=90&&!held.Contains(17))typed+=(char)(held.Contains(16)?code:code+32);
   Key(code,up);
  };
  Func<char,int> map=c=>char.IsUpper(c)?c|256:char.ToUpperInvariant(c);
  return ((Task<bool>)type.GetMethod("Send").Invoke(null,new object[]{text,paste,new Func<bool>(()=>focused),new Action<string>(_=>{}),map,key,delay,new Action<string>(_=>{})})).GetAwaiter().GetResult();
 }
}
