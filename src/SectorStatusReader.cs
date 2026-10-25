using System;
using System.IO;
using System.Text;
namespace Arthur.LmuOverlay
{
 // The native host atomically replaces status.json. Readers must permit delete/rename.
 internal sealed class SectorStatusReader
 {
  readonly Func<string,string> read;
  DateTime? blockedSince;
  public SectorStatusReader():this(ReadShared){}
  internal SectorStatusReader(Func<string,string> source){read=source;}
  static string ReadShared(string path)
  {
   using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
   using(var r=new StreamReader(f,Encoding.UTF8)){
    if(f.Length>131072)throw new InvalidDataException("Status excede limite.");
    var chars=new char[131073];int n=0,k;
    while(n<chars.Length&&(k=r.Read(chars,n,chars.Length-n))>0)n+=k;
    if(n==chars.Length)throw new InvalidDataException("Status excede limite.");
    return new string(chars,0,n);
   }
  }
  public SectorState Read(string path,DateTime now)
  {
   try{var result=SectorProtocol.Status(read(path));blockedSince=null;return result;}
   catch(FileNotFoundException){return null;}
   catch(IOException e){
    int code=e.HResult&65535;
    if(code!=32&&code!=33)throw;
    if(!blockedSince.HasValue)blockedSince=now;
    if((now-blockedSince.Value).TotalSeconds>=3)throw new IOException("O arquivo de estado do treino permaneceu ocupado por mais de 3 segundos. Conecte o treino novamente.",e);
    return null;
   }
  }
 }
}
