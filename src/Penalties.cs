using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arthur.LmuOverlay
{
    [DataContract] public sealed class PendingPenaltyTypes
    {
        [DataMember] public int DT;
        [DataMember] public int SG;
        [DataMember] public int TIME;
        public string Label { get { return string.Join("+",new[]{DT>0 ? "DT" : "",SG>0 ? "SG" : "",TIME>0 ? "T" : ""}.Where(x=>x.Length>0)); } }
    }
    [DataContract] public sealed class PenaltyRow
    {
        [DataMember] public int? slotID;
        [DataMember] public string driverName;
        [DataMember] public string vehFilename;
        [DataMember] public PendingPenaltyTypes penalties;
        [DataMember] public string tireCompoundNameFront;
        [DataMember] public string tireCompoundNameRear;
        [DataMember] public string vehicleName;
    }
    [DataContract] public sealed class PenaltyMessage
    {
        [DataContract] sealed class Envelope
        { [DataMember] public string messageType { get;set; } [DataMember] public string topic { get;set; } }
        [DataMember] public string messageType;
        [DataMember] public string topic;
        [DataMember] public List<PenaltyRow> body;
        public static List<PenaltyRow> Parse(byte[] bytes)
        {
            if(bytes==null || bytes.Length>2*1024*1024) throw new InvalidDataException("Resposta de punições muito grande.");
            using(var stream=new MemoryStream(bytes))
            {
                // Other UI topics and subscription acknowledgements may use a different body shape.
                var envelope=(Envelope)new DataContractJsonSerializer(typeof(Envelope)).ReadObject(stream);
                if(envelope==null || envelope.messageType!="UPDATE" || envelope.topic!="LiveStandings") return null;
                stream.Position=0;
                var message=(PenaltyMessage)new DataContractJsonSerializer(typeof(PenaltyMessage),new DataContractJsonSerializerSettings { MaxItemsInObjectGraph=20000 }).ReadObject(stream);
                if(message==null || message.messageType!="UPDATE" || message.topic!="LiveStandings") return null;
                if(message.body==null || message.body.Count>256) throw new InvalidDataException("Lista de punições incompleta.");
                return message.body;
            }
        }
        public static void Apply(Frame frame,List<PenaltyRow> rows)
        {
            foreach(var driver in frame.Drivers)
            {
                driver.PenaltyTypes="";
                if(driver.TyreSource=="WS") { driver.TyreFront=driver.TyreRear=driver.TyreSource=""; }
                if(rows==null) continue;
                var matches=rows.Where(x=>x!=null && x.slotID==driver.Id &&
                    string.Equals(x.driverName,driver.Name,StringComparison.Ordinal) &&
                    (string.IsNullOrEmpty(x.vehFilename) || string.Equals(x.vehFilename,driver.VehicleKey,StringComparison.OrdinalIgnoreCase))).ToList();
                if(matches.Count==1)
                {
                    var row=matches[0];
                    if(driver.Penalties>0 && row.penalties!=null) driver.PenaltyTypes=row.penalties.Label;
                    if(string.IsNullOrEmpty(driver.CarName) && !string.IsNullOrEmpty(row.vehicleName)) driver.CarName=row.vehicleName;
                    if(driver.TyreSource!="telemetria" && (!string.IsNullOrEmpty(row.tireCompoundNameFront) || !string.IsNullOrEmpty(row.tireCompoundNameRear)))
                    { driver.TyreFront=row.tireCompoundNameFront ?? "";driver.TyreRear=row.tireCompoundNameRear ?? "";driver.TyreSource="WS"; }
                }
            }
        }
    }
    public sealed class PenaltyService : IDisposable
    {
        readonly object gate=new object();readonly CancellationTokenSource stop=new CancellationTokenSource();
        readonly Action<string,Exception> log;
        List<PenaltyRow> latest;DateTime received;
        ClientWebSocket socket;bool started,disposed;string key="";int generation;
        public string Status="Punições: contagem do jogo; aguardando detalhes.";
        public PenaltyService(Action<string,Exception> logger) { log=logger; }
        public void Apply(Frame frame)
        {
            lock(gate)
            {
                if(key!=frame.SessionKey || !frame.Connected) { key=frame.SessionKey;generation++;latest=null; }
                PenaltyMessage.Apply(frame,DateTime.UtcNow-received<TimeSpan.FromSeconds(3) ? latest : null);
                if(!started && !disposed && frame.Connected) { started=true;Task.Run((Func<Task>)Run); }
            }
        }
        async Task Run()
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    using(var client=new ClientWebSocket())
                    {
                        client.Options.Proxy=null;client.Options.KeepAliveInterval=TimeSpan.FromSeconds(10);
                        lock(gate) { if(disposed) return;socket=client; }
                        using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                        { timeout.CancelAfter(2000);await client.ConnectAsync(new Uri("ws://127.0.0.1:6398/websocket/ui"),timeout.Token).ConfigureAwait(false); }
                        byte[] subscription=Encoding.UTF8.GetBytes("{\"messageType\":\"SUB\",\"topic\":\"LiveStandings\"}");
                        await client.SendAsync(new ArraySegment<byte>(subscription),WebSocketMessageType.Text,true,stop.Token).ConfigureAwait(false);
                        var buffer=new byte[16384];
                        while(!stop.IsCancellationRequested && client.State==WebSocketState.Open)
                        {
                            int version;lock(gate) version=generation;
                            using(var data=new MemoryStream())
                            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                            {
                                timeout.CancelAfter(5000);WebSocketReceiveResult part;
                                do
                                {
                                    part=await client.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token).ConfigureAwait(false);
                                    if(part.MessageType==WebSocketMessageType.Close) throw new IOException("Canal de punições fechado.");
                                    if(part.MessageType!=WebSocketMessageType.Text) throw new InvalidDataException("Mensagem de punições inválida.");
                                    if(data.Length+part.Count>2*1024*1024) throw new InvalidDataException("Mensagem de punições excede o limite.");
                                    data.Write(buffer,0,part.Count);
                                } while(!part.EndOfMessage);
                                var rows=PenaltyMessage.Parse(data.ToArray());
                                if(rows!=null) lock(gate) if(version==generation && !disposed)
                                { latest=rows;received=DateTime.UtcNow;Status="Punições: detalhes do jogo disponíveis (DT / SG / T)."; }
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    if(ex is OutOfMemoryException) throw;
                    lock(gate) { latest=null;Status="Punições: mostrando a contagem quando o tipo não está disponível."; }
                    if(!stop.IsCancellationRequested && log!=null) log("Detalhes de punições indisponíveis; contagem preservada",ex);
                }
                finally { lock(gate) socket=null; }
                try { await Task.Delay(3000,stop.Token).ConfigureAwait(false); } catch(OperationCanceledException) { return; }
            }
        }
        public void Dispose() { lock(gate) { disposed=true;stop.Cancel();if(socket!=null) socket.Abort();latest=null; } }
    }
}
