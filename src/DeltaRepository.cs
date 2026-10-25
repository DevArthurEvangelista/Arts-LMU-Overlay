using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml;
namespace Arthur.LmuOverlay
{
 public sealed class DeltaStored {public string Key="",Status="";public DeltaLap Lap;public int Revision;}
 public interface IDeltaRepository {DeltaStored Latest{get;}void Load(DeltaProfile p);void Save(DeltaLap lap);}
 public interface IDeltaFiles {DeltaLap Load(DeltaProfile p);DeltaLap Save(DeltaLap lap);}
 public sealed class DeltaFiles:IDeltaFiles
 {
  readonly string directory;public DeltaFiles(string dir){directory=dir;}
  string PathFor(DeltaProfile p){if(p==null||!p.Valid)throw new InvalidDataException("Pista/carro sem identificação.");return Path.Combine(directory,p.Key+".xml");}
  static DataContractSerializer Serializer(){return new DataContractSerializer(typeof(DeltaLap),new DataContractSerializerSettings{MaxItemsInObjectGraph=300000});}
  public DeltaLap Load(DeltaProfile p){string path=PathFor(p);if(!File.Exists(path))return null;if(new FileInfo(path).Length>4194304)throw new InvalidDataException("Referência excede o limite.");using(var f=File.OpenRead(path))using(var xml=XmlReader.Create(f,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4194304})){var lap=(DeltaLap)Serializer().ReadObject(xml);if(lap==null||!lap.IsValid()||lap.Profile.Key!=p.Key)throw new InvalidDataException("Referência inválida. Arquivo preservado.");return lap;}}
  public DeltaLap Save(DeltaLap lap){if(lap==null||!lap.IsValid())throw new InvalidDataException("Volta incompleta.");var old=Load(lap.Profile);if(old!=null&&old.Time<=lap.Time)return old;Directory.CreateDirectory(directory);string path=PathFor(lap.Profile),tmp=path+".tmp";try{using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){Serializer().WriteObject(f,lap);f.Flush(true);}if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}return lap;}
 }
 public sealed class DeltaRepository:IDeltaRepository,IDisposable
 {
  sealed class Request {public DeltaProfile Profile;public DeltaLap Lap;}
  readonly BlockingCollection<Request> queue=new BlockingCollection<Request>(16);readonly IDeltaFiles files;readonly Action<string,Exception> log;readonly Thread worker;DeltaStored latest=new DeltaStored();int stopped,revision;
  public DeltaStored Latest{get{return Interlocked.CompareExchange(ref latest,null,null);}}
  public DeltaRepository(IDeltaFiles f,Action<string,Exception> error){files=f;log=error;worker=new Thread(Run){IsBackground=true,Name="Arts delta references"};worker.Start();}
  public void Load(DeltaProfile p){Offer(new Request{Profile=p});}public void Save(DeltaLap l){Offer(new Request{Profile=l.Profile,Lap=l});}
  void Publish(Request r,DeltaLap l,string s){Interlocked.Exchange(ref latest,new DeltaStored{Key=r.Profile.Key,Lap=l,Status=s,Revision=Interlocked.Increment(ref revision)});}
  void Offer(Request r){if(Volatile.Read(ref stopped)!=0)return;try{if(!queue.TryAdd(r))Publish(r,null,"Fila ocupada. Use Tentar novamente.");}catch(InvalidOperationException){}}
  void Run(){foreach(var r in queue.GetConsumingEnumerable())try{var l=r.Lap==null?files.Load(r.Profile):files.Save(r.Lap);Publish(r,l,l==null?"Complete uma volta válida para salvar a referência.":"Melhor pessoal salva neste computador.");}catch(Exception e){Publish(r,null,"Falha na referência: "+e.Message+" Use Tentar novamente.");if(log!=null)log("Delta: referência local",e);}}
  public void Dispose(){if(Interlocked.Exchange(ref stopped,1)!=0)return;queue.CompleteAdding();worker.Join(2000);}
 }
}
