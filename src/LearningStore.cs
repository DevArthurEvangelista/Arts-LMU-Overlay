using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml;

namespace Arthur.LmuOverlay
{
    public interface ILearningWriter
    {
        void SaveSession(LearningSession session);
        void SaveLap(LearningLap lap);
    }
    public sealed class LearningStore : ILearningWriter
    {
        public readonly string DirectoryPath;
        public LearningStore(string directory) { DirectoryPath=directory; }
        static string Id(string value)
        { Guid id;if(!Guid.TryParseExact(value,"N",out id)) throw new InvalidDataException("Identificador de gravação inválido.");return value; }
        string Folder(string id) { return Path.Combine(DirectoryPath,Id(id)); }
        static DataContractSerializer Serializer<T>()
        { return new DataContractSerializer(typeof(T),new DataContractSerializerSettings { MaxItemsInObjectGraph=1500000 }); }
        static void Write<T>(string path,T value,bool compress)
        {
            if(File.Exists(path)) return;
            string temporary=path+".tmp";
            try
            {
                using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                {
                    if(compress) using(var gzip=new GZipStream(file,CompressionMode.Compress,true)) Serializer<T>().WriteObject(gzip,value);
                    else Serializer<T>().WriteObject(file,value);
                    file.Flush(true);
                }
                // Each lap is immutable and has its own GUID. Publish its small
                // index only after the complete payload is safely on disk.
                if(File.Exists(path)) File.Delete(temporary);else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
        static T Read<T>(string path,bool compressed)
        {
            var info=new FileInfo(path);if(info.Length>(compressed ? 24*1024*1024 : 128*1024)) throw new InvalidDataException("Gravação excede o tamanho permitido.");
            using(var file=File.OpenRead(path))
            using(var stream=compressed ? (Stream)new GZipStream(file,CompressionMode.Decompress) : file)
            using(var xml=XmlReader.Create(stream,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=compressed ? 64*1024*1024 : 128*1024 }))
                return (T)Serializer<T>().ReadObject(xml);
        }
        public void SaveSession(LearningSession session)
        { string directory=Folder(session.Id);Directory.CreateDirectory(directory);Write(Path.Combine(directory,"session.xml"),session,false); }
        public void SaveLap(LearningLap lap)
        {
            if(lap.Info.SessionId!=lap.Session.Id) throw new InvalidDataException("Sessão inconsistente.");
            SaveSession(lap.Session);string stem=Path.Combine(Folder(lap.Session.Id),Id(lap.Info.Id));
            Write(stem+".lap.gz",lap,true);Write(stem+".xml",lap.Info,false);
        }
        internal static bool Recoverable(Exception e)
        { return e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is SerializationException || e is XmlException || e is ArgumentException; }
        public LearningSession[] Sessions(out string warning)
        {
            warning="";var result=new List<LearningSession>();if(!Directory.Exists(DirectoryPath)) return result.ToArray();int skipped=0;
            foreach(string directory in Directory.EnumerateDirectories(DirectoryPath))
            {
                Guid id;if(!Guid.TryParseExact(Path.GetFileName(directory),"N",out id)) continue;
                try
                {
                    var s=Read<LearningSession>(Path.Combine(directory,"session.xml"),false);
                    if(s==null || s.Version!=1 || s.Id!=Path.GetFileName(directory) || !Format.Finite(s.Length) || s.Length<100 || s.Length>100000 || string.IsNullOrWhiteSpace(s.Track))
                        throw new InvalidDataException("Sessão inválida ou versão incompatível.");
                    s.Server=s.Server??"";result.Add(s);
                }
                catch(Exception e) { if(!Recoverable(e)) throw;skipped++; }
            }
            if(skipped>0) warning=skipped+" sessão(ões) não puderam ser lidas. Os arquivos foram preservados.";
            return result.OrderByDescending(x=>x.StartedUtc).ToArray();
        }
        public LearningLapInfo[] Laps(string sessionId,out string warning)
        {
            warning="";var result=new List<LearningLapInfo>();string folder=Folder(sessionId);if(!Directory.Exists(folder)) return result.ToArray();int skipped=0;
            foreach(string path in Directory.EnumerateFiles(folder,"*.xml"))
            {
                if(Path.GetFileName(path)=="session.xml") continue;
                try
                {
                    var lap=Read<LearningLapInfo>(path,false);
                    if(lap==null || lap.SessionId!=sessionId || Id(lap.Id)!=Path.GetFileNameWithoutExtension(path) || !File.Exists(Path.Combine(folder,lap.Id+".lap.gz")))
                        throw new InvalidDataException("Índice de volta incompleto.");
                    result.Add(lap);
                }
                catch(Exception e) { if(!Recoverable(e)) throw;skipped++; }
            }
            if(skipped>0) warning=skipped+" volta(s) não puderam ser lidas. Os arquivos foram preservados.";
            return result.OrderBy(x=>x.RecordedUtc).ThenBy(x=>x.Number).ToArray();
        }
        public LearningLap Load(string sessionId,string lapId)
        {
            var lap=Read<LearningLap>(Path.Combine(Folder(sessionId),Id(lapId)+".lap.gz"),true);
            if(lap==null || lap.Info==null || lap.Session==null || lap.Session.Version!=1 || lap.Info.SessionId!=sessionId || lap.Session.Id!=sessionId || lap.Info.Id!=lapId || lap.Samples==null || lap.Samples.Length>100001)
                throw new InvalidDataException("A gravação está incompleta ou tem formato incompatível.");
            if(lap.Samples.Any(p=>p==null || !Format.Finite(p.Time) || p.Time<0 || !Format.Finite(p.Distance)))
                throw new InvalidDataException("A gravação contém amostras inválidas.");
            return lap;
        }
    }
    // The producer never waits for disk, and a busy writer cannot grow the
    // queue without a limit. Any lost interval explicitly breaks comparison.
    public sealed class LearningPump : IDisposable
    {
        readonly BlockingCollection<LearningFrame> queue=new BlockingCollection<LearningFrame>(128);
        readonly Queue<LearningLap> pending=new Queue<LearningLap>();
        readonly ILearningWriter store;
        readonly Action<string,Exception> log;
        readonly Thread worker;
        int stopping,revision,lost,unsaved;
        bool dropped,wasActive;
        string previousKey="";
        double previousTime=-1;
        double sampledAt=-1;
        volatile string status="Aguardando uma sessão do LMU.",failure="";
        public string Status { get { return status; } }
        public string Error { get { return failure; } }
        public int Revision { get { return Volatile.Read(ref revision); } }
        public int DroppedFrames { get { return Volatile.Read(ref lost); } }
        public int UnsavedLaps { get { return Volatile.Read(ref unsaved); } }
        public LearningPump(ILearningWriter writer,Action<string,Exception> onError)
        { store=writer;log=onError;worker=new Thread(Run) { IsBackground=true,Name="Arts learning recorder" };worker.Start(); }
        public void Accept(Frame frame)
        {
            if(Volatile.Read(ref stopping)!=0) return;
            bool active=frame.Connected && !frame.ScoringStale && frame.Phase!=9 && !frame.Demo && frame.TrackLength>=100;
            if(active && wasActive && frame.SessionKey==previousKey && frame.Elapsed>=previousTime && frame.ReadAt>=sampledAt && frame.ReadAt-sampledAt<.04) return;
            if(active==wasActive && frame.SessionKey==previousKey && frame.Elapsed==previousTime) return;
            wasActive=active;previousKey=frame.SessionKey;previousTime=frame.Elapsed;sampledAt=frame.ReadAt;
            Offer(LearningFrame.Capture(frame));
        }
        internal void Offer(LearningFrame frame)
        {
            if(Volatile.Read(ref stopping)!=0) return;
            frame.Dropped|=dropped;
            try
            {
                if(queue.TryAdd(frame)) dropped=false;
                else { dropped=true;Interlocked.Increment(ref lost); }
            }
            catch(InvalidOperationException) { } // Shutdown can race the last telemetry callback.
        }
        void Failed(Exception e)
        {
            failure="Falha ao salvar: "+e.Message;
            if(log!=null) log("Aprendizado: gravação local",e);
        }
        void Save(LearningLap lap)
        {
            try { store.SaveLap(lap);Interlocked.Increment(ref revision);if(pending.Count==0 && unsaved==0) failure=""; }
            catch(Exception e)
            {
                if(!LearningStore.Recoverable(e)) throw;
                if(pending.Count>=64) { pending.Dequeue();Interlocked.Increment(ref unsaved); }
                pending.Enqueue(lap);Failed(e);
            }
        }
        void Retry()
        {
            while(pending.Count>0)
            {
                try { store.SaveLap(pending.Peek());pending.Dequeue();Interlocked.Increment(ref revision); }
                catch(Exception e) { if(!LearningStore.Recoverable(e)) throw;Failed(e);return; }
            }
            if(unsaved==0) failure="";
        }
        void Run()
        {
            var recorder=new LearningRecorder(s=>
            {
                try { store.SaveSession(s);Interlocked.Increment(ref revision); }
                catch(Exception e) { if(!LearningStore.Recoverable(e)) throw;Failed(e); }
            },Save);
            DateTime retry=DateTime.UtcNow;
            try
            {
                foreach(var f in queue.GetConsumingEnumerable())
                {
                    recorder.Accept(f);
                    status=f.Active && f.Cars.Any(c=>c.CanRecord) ? "Gravando voltas • "+f.Track : "Aguardando carros na pista. Voltas concluídas ficam salvas.";
                    if(pending.Count>0 && DateTime.UtcNow>=retry) { Retry();retry=DateTime.UtcNow.AddSeconds(10); }
                }
                recorder.Flush();Retry();
                if(pending.Count>0) Interlocked.Add(ref unsaved,pending.Count);
            }
            catch(Exception e)
            {
                // Failure of this optional module must not terminate the game overlay.
                failure="Gravação interrompida: "+e.Message;if(log!=null) log("Aprendizado: erro no gravador",e);
            }
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref stopping,1)!=0) return;
            queue.CompleteAdding();
            if(!worker.Join(4000) && log!=null) log("Aprendizado: disco não concluiu a gravação antes do encerramento",null);
        }
    }
}
