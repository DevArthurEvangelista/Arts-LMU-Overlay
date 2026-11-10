using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Arthur.LmuOverlay
{
 // Timing and cancellation are independent of Win32 and the recording pipeline.
 internal static class VoiceChatDelivery
 {
  public static async Task<bool> Send(string message,bool paste,Func<bool> focused,Action<string> copy,Func<char,int> map,Action<ushort,bool,bool> key,Func<int,Task> delay,Action<string> stage)
  {
   message=(message??"").Replace('\r',' ').Replace('\n',' ').Trim();
   if(message.Length==0||message.Length>180){stage("Frase vazia ou acima de 180 caracteres");return false;}
   if(!focused()){stage("Cancelado antes de abrir: jogo sem foco");return false;}
   // Build the whole plan before opening chat. Never inject control characters.
   var plan=new List<int>();
   foreach(char c in message){if(char.IsControl(c))throw new ArgumentException("A frase contém um caractere de controle.");plan.Add(map(c));}
   if(paste)copy(message);
   stage("Abrindo chat");
   if(!await Tap(13,false,focused,key,delay))return false;
   await delay(600);
   if(!focused()){stage("Cancelado após abrir: jogo sem foco");return false;}
   if(paste){
    stage("Colando texto");
    try{key(17,false,false);await delay(60);if(!await Tap(86,false,focused,key,delay))return false;}
    finally{key(17,true,false);}
   }else{
    stage("Digitando texto");
    for(int i=0;i<message.Length;i++){
     if(!focused()){stage("Digitação cancelada: jogo sem foco");return false;}
     int mapped=plan[i];var modifiers=new List<ushort>();
     try{
      if(mapped>=0){
       if((mapped&256)!=0)modifiers.Add(16);
       if((mapped&512)!=0)modifiers.Add(17);
       if((mapped&1024)!=0)modifiers.Add(18);
       foreach(ushort modifier in modifiers)key(modifier,false,false);
       if(modifiers.Count>0)await delay(40);
      }
      if(!await Tap(mapped<0?message[i]:(ushort)(mapped&255),mapped<0,focused,key,delay))return false;
     }finally{for(int n=modifiers.Count-1;n>=0;n--)key(modifiers[n],true,false);}
    }
   }
   await delay(350);
   stage("Confirmando com Enter");
   if(!await Tap(13,false,focused,key,delay))return false;
   stage("Sequência concluída; recebimento no jogo não verificado");return true;
  }
  static async Task<bool> Tap(ushort code,bool unicode,Func<bool> focused,Action<ushort,bool,bool> key,Func<int,Task> delay)
  {
   if(!focused())return false;
   try{key(code,false,unicode);await delay(70);}
   finally{key(code,true,unicode);}
   await delay(40);return focused();
  }
 }
}
