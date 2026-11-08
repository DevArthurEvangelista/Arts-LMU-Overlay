using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Text;
using System.Windows.Forms;
namespace Arthur.LmuOverlay
{
 internal sealed class VoiceDictation : IDisposable
 {
  SpeechRecognitionEngine engine;
  readonly List<string> phrases=new List<string>();
  public event Action<string> Completed;
  public event Action<string> Error;
  public bool Recording{get;private set;}
  bool stopping;
  public bool Available{get{return engine!=null;}}
  public VoiceDictation()
  {
   try{
    foreach(var recognizer in SpeechRecognitionEngine.InstalledRecognizers())
     if(recognizer.Culture.Name.StartsWith("pt",StringComparison.OrdinalIgnoreCase)){engine=new SpeechRecognitionEngine(recognizer);break;}
    if(engine==null)return;
    engine.LoadGrammar(new DictationGrammar());
    engine.SetInputToDefaultAudioDevice();
    engine.SpeechRecognized+=delegate(object s,SpeechRecognizedEventArgs e){if(Recording&&e.Result.Confidence>=.45&&e.Result.Text.Length>0)phrases.Add(e.Result.Text);};
    engine.RecognizeCompleted+=delegate(object s,RecognizeCompletedEventArgs e){Recording=false;stopping=false;string text=string.Join(" ",phrases.ToArray()).Trim();phrases.Clear();if(e.Error!=null){if(Error!=null)Error(e.Error.Message);}else if(Completed!=null)Completed(text);};
   }catch(Exception e){if(engine!=null){engine.Dispose();engine=null;}if(Error!=null)Error(e.Message);}
  }
  public bool Start(){if(engine==null||Recording)return false;phrases.Clear();try{Recording=true;stopping=false;engine.RecognizeAsync(RecognizeMode.Multiple);return true;}catch(Exception e){Recording=false;if(Error!=null)Error(e.Message);return false;}}
  public void Stop(){if(engine==null||!Recording||stopping)return;try{stopping=true;engine.RecognizeAsyncStop();}catch(InvalidOperationException){Recording=false;}}
  public void Cancel(){if(engine==null||!Recording)return;Recording=false;stopping=false;phrases.Clear();try{engine.RecognizeAsyncCancel();}catch(InvalidOperationException){}}
  public void Dispose(){Cancel();if(engine!=null)engine.Dispose();}
 }
 internal static class VoiceChatKeys
 {
  [StructLayout(LayoutKind.Sequential)] struct INPUT {public int type;public INPUTUNION u;}
  [StructLayout(LayoutKind.Explicit)] struct INPUTUNION {[FieldOffset(0)]public KEYBDINPUT ki;[FieldOffset(0)]public long padding;}
  [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT {public ushort vk,scan;public uint flags,time;public IntPtr extra;}
  [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,[In] INPUT[] inputs,int size);
  static void Key(ushort code,bool up,bool unicode)
  {var v=new INPUT{type=1,u=new INPUTUNION{ki=new KEYBDINPUT{vk=unicode?(ushort)0:code,scan=unicode?code:(ushort)0,flags=(uint)((unicode?4:0)|(up?2:0))}}};if(SendInput(1,new[]{v},Marshal.SizeOf(typeof(INPUT)))!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
  // The default in-car Realtime Chat binding must be Enter, and chat must be closed.
  public static bool Send(string message,Func<bool> gameFocused)
  {
   if(string.IsNullOrWhiteSpace(message)||message.Length>180||!gameFocused())return false;
   Key(13,false,false);Key(13,true,false);
   System.Threading.Thread.Sleep(120);
   if(!gameFocused())return false;
   foreach(char c in message){if(!gameFocused())return false;Key(c,false,true);Key(c,true,true);}
   if(!gameFocused())return false;Key(13,false,false);Key(13,true,false);return true;
  }
 }
}
