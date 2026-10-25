using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace Arthur.LmuOverlay
{
    [DataContract] public sealed class LobbyCountry { [DataMember] public string nationality; }
    [DataContract] public sealed class LobbyCountryTeam { [DataMember] public Dictionary<string,LobbyCountry> drivers; }
    [DataContract] public sealed class LobbyCountries
    {
        [DataMember] public Dictionary<string,LobbyCountry> drivers;
        [DataMember] public Dictionary<string,LobbyCountryTeam> teams;
    }
    public sealed class CountrySnapshot
    {
        readonly Dictionary<string,string> names = new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<int,Dictionary<string,string>> slots = new Dictionary<int,Dictionary<string,string>>();
        public static CountrySnapshot Parse(string json)
        {
            if (json==null || json.Length>1024*1024) throw new InvalidDataException("Resposta de nacionalidades inválida.");
            var serializer = new DataContractJsonSerializer(typeof(LobbyCountries),new DataContractJsonSerializerSettings {
                UseSimpleDictionaryFormat=true, MaxItemsInObjectGraph=8192 });
            LobbyCountries payload;
            using(var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) payload=(LobbyCountries)serializer.ReadObject(stream);
            if(payload==null || (payload.drivers==null && payload.teams==null)) throw new InvalidDataException("Lobby sem nacionalidades.");
            var s = new CountrySnapshot();
            Copy(payload.drivers,s.names);
            if(payload.teams!=null) foreach(var team in payload.teams)
            {
                int slot;
                if(team.Key.StartsWith("utid",StringComparison.Ordinal) && int.TryParse(team.Key.Substring(4),out slot) && team.Value!=null)
                {
                    var map=new Dictionary<string,string>(StringComparer.Ordinal);
                    Copy(team.Value.drivers,map); s.slots[slot]=map;
                }
            }
            return s;
        }
        static void Copy(Dictionary<string,LobbyCountry> source, Dictionary<string,string> target)
        {
            if(source==null) return;
            foreach(var pair in source)
            {
                if(pair.Key==null || pair.Key.Length>128 || pair.Value==null) continue;
                string country=CountryCodes.Normalize(pair.Value.nationality);
                if(country.Length>0) target[pair.Key]=country;
            }
        }
        public string For(Driver d, Frame frame)
        {
            string c; Dictionary<string,string> team;
            if(slots.TryGetValue(d.Id,out team) && team.TryGetValue(d.Name,out c)) return c;
            // Exact lobby name only. Never infer a country from a name or use an ambiguous match.
            if(frame.Drivers.Count(x=>x.Name==d.Name)==1 && names.TryGetValue(d.Name,out c)) return c;
            return "";
        }
    }

    public sealed class CountryService : IDisposable
    {
        public const string Endpoint="http://127.0.0.1:6397/rest/multiplayer/teams";
        readonly object gate=new object();
        readonly Func<CountrySnapshot> read;
        readonly Action<string,Exception> error;
        string session="";
        CountrySnapshot snapshot;
        double nextPoll, received;
        bool busy, disposed;
        public volatile string Status="Bandeiras: aguardando o lobby";
        public CountryService() : this(ReadLobby,null) { }
        public CountryService(Action<string,Exception> log) : this(ReadLobby,log) { }
        internal CountryService(Func<CountrySnapshot> source,Action<string,Exception> log) { read=source;error=log; }
        public void Clear()
        {
            lock(gate) { session=""; snapshot=null; nextPoll=0; Status="Bandeiras: aguardando o lobby"; }
        }
        public void Apply(Frame f,double now,bool enabled)
        {
            if(f.Demo) return;
            if(!enabled || !f.Connected || f.Drivers.Count==0)
            {
                Clear(); if(!enabled) Status="Consulta de bandeiras desativada"; foreach(var d in f.Drivers) d.Country=""; return;
            }
            CountrySnapshot current;
            string requested=null;
            lock(gate)
            {
                if(disposed) return;
                if(session!=f.SessionKey) { session=f.SessionKey; snapshot=null; nextPoll=0; }
                current=now-received<=30 ? snapshot : null;
                if(!busy && now>=nextPoll) { busy=true; nextPoll=now+10; requested=session; }
            }
            foreach(var d in f.Drivers) d.Country=current==null ? "" : current.For(d,f);
            if(requested!=null)
            {
                string key=requested;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    CountrySnapshot answer=null;
                    try { answer=read(); }
                    catch(Exception e)
                    {
                        if(!(e is InvalidDataException) && !(e is WebException) && !(e is IOException) && !(e is SerializationException) &&
                            !(e is System.Xml.XmlException) && !(e is ArgumentException) && !(e is InvalidOperationException)) throw;
                        if(error!=null) error("Bandeiras: consulta indisponível; overlay mantido",e);
                    }
                    finally
                    {
                        lock(gate)
                        {
                            if(!disposed && session==key)
                            {
                                if(answer!=null) { snapshot=answer; received=now; Status="Bandeiras: nacionalidade informada pelo lobby"; }
                                else Status="Bandeiras indisponíveis neste lobby • sem país presumido";
                            }
                            busy=false;
                        }
                    }
                });
            }
        }
        static CountrySnapshot ReadLobby()
        {
            // GET only, loopback only, no proxy, no redirects, no credentials, no external lookup.
            var request=(HttpWebRequest)WebRequest.Create(Endpoint);
            request.Method="GET"; request.Proxy=null; request.AllowAutoRedirect=false;
            request.Timeout=900; request.ReadWriteTimeout=900; request.UseDefaultCredentials=false;
            request.Accept="application/json";
            using(var response=(HttpWebResponse)request.GetResponse())
            {
                if(response.StatusCode!=HttpStatusCode.OK || response.ContentLength>1024*1024) throw new InvalidDataException();
                using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))
                {
                    var b=new StringBuilder(); var chars=new char[2048]; int count;
                    while((count=reader.Read(chars,0,chars.Length))>0)
                    { b.Append(chars,0,count); if(b.Length>1024*1024) throw new InvalidDataException(); }
                    return CountrySnapshot.Parse(b.ToString());
                }
            }
        }
        public void Dispose() { lock(gate) { disposed=true; snapshot=null; } }
    }
}
