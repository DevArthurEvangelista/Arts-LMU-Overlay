using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Xml;

namespace Arthur.LmuOverlay
{
    public sealed class VehicleDamage
    {
        // Fractions of DAMAGE: 0 = no reported damage, 1 = fully damaged.
        // Unknown remains NaN; never substitute suspension travel for damage.
        public double[] Suspension={double.NaN,double.NaN,double.NaN,double.NaN};
        public double Aero=double.NaN;
        public bool Any { get { return Format.Finite(Aero) || Suspension.Any(Format.Finite); } }
        static double Fraction(XmlNode node)
        {
            double value;
            return node!=null && node.Attributes["type"]!=null && node.Attributes["type"].Value=="number" &&
                double.TryParse(node.InnerText,NumberStyles.Float,CultureInfo.InvariantCulture,out value) && Format.Finite(value) && value>=0 && value<=1 ? value : double.NaN;
        }
        public static VehicleDamage Parse(string json)
        {
            if(string.IsNullOrWhiteSpace(json) || json.Length>128*1024) throw new InvalidDataException("Condição do veículo indisponível.");
            var limits=new XmlDictionaryReaderQuotas { MaxDepth=16,MaxArrayLength=128,MaxStringContentLength=128*1024 };
            var document=new XmlDocument { XmlResolver=null };
            using(var reader=JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json),limits)) document.Load(reader);
            var root=document.DocumentElement;var result=new VehicleDamage();
            if(root==null || root.GetAttribute("type")!="object") throw new InvalidDataException("Condição do veículo em formato desconhecido.");
            result.Aero=Fraction(root.SelectSingleNode("vehicleDamage"));
            var suspension=root.SelectSingleNode("suspensionDamage");
            if(suspension!=null && suspension.Attributes["type"]!=null && suspension.Attributes["type"].Value=="array" && suspension.ChildNodes.Count==4)
                for(int i=0;i<4;i++) result.Suspension[i]=Fraction(suspension.ChildNodes[i]);
            return result;
        }
    }
    // Small local GET on a background worker, never on the pedal reader or UI.
    // The endpoint returns overall aero damage, not individual wing health.
    public sealed class VehicleDamageService : IDisposable
    {
        readonly object gate=new object();
        readonly Func<VehicleDamage> read;
        readonly Func<double> clock;
        readonly Action<string,Exception> log;
        VehicleDamage latest;
        string key="";
        double received=-100,next,lastElapsed=-1,logged=-100;
        int generation;
        bool busy,disposed,eligible;
        public VehicleDamageService(Func<double> now,Action<string,Exception> error) : this(now,error,ReadLocal) { }
        internal VehicleDamageService(Func<double> now,Action<string,Exception> error,Func<VehicleDamage> fetch)
        { clock=now;log=error;read=fetch; }
        public void Apply(Frame frame,double now,bool enabled)
        {
            string wanted=frame.SessionKey+"/"+frame.PlayerId+"/"+frame.VehicleModel;int version;
            bool active=enabled && frame.Connected && frame.HasTelemetry && !frame.Stale && frame.InRealtime;
            lock(gate)
            {
                if(disposed) return;
                if(wanted!=key || (lastElapsed>=0 && frame.Elapsed<lastElapsed-.01) || active!=eligible)
                { key=wanted;generation++;latest=null;next=0; }
                lastElapsed=frame.Elapsed;eligible=active;
                frame.Damage=active && latest!=null && now>=received && now-received<=2 ? latest : new VehicleDamage();
                if(!active || busy || now<next) return;
                busy=true;next=now+.5;version=generation;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                VehicleDamage value=null;Exception error=null;
                try { value=read(); }
                catch(Exception e)
                {
                    if(!(e is IOException) && !(e is InvalidDataException) && !(e is WebException) && !(e is XmlException) && !(e is ArgumentException) && !(e is InvalidOperationException) && !(e is System.Runtime.Serialization.SerializationException)) throw;
                    error=e;
                }
                double completed=clock();bool report=false;
                lock(gate)
                {
                    if(!disposed && generation==version)
                    {
                        latest=value!=null && value.Any ? value : null;received=completed;
                        if(latest==null) next=completed+5;
                        if(error!=null && completed-logged>=30) { logged=completed;report=true; }
                    }
                    busy=false;
                }
                if(report && log!=null) log("Danos: aguardando leitura local do LMU; pneus preservados",error);
            });
        }
        static VehicleDamage ReadLocal()
        {
            WebException first=null;
            foreach(string host in new[]{"127.0.0.1","[::1]"})
            {
                try
                {
                    var request=(HttpWebRequest)WebRequest.Create("http://"+host+":6397/rest/garage/getVehicleCondition");
                    request.Method="GET";request.Proxy=null;request.AllowAutoRedirect=false;request.UseDefaultCredentials=false;request.Timeout=request.ReadWriteTimeout=700;
                    using(var response=(HttpWebResponse)request.GetResponse())
                    {
                        if(response.StatusCode!=HttpStatusCode.OK || response.ContentLength>128*1024) throw new InvalidDataException("Condição do veículo indisponível.");
                        using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))
                        {
                            var result=new StringBuilder();var buffer=new char[2048];int count;
                            while((count=reader.Read(buffer,0,buffer.Length))>0)
                            { result.Append(buffer,0,count);if(result.Length>128*1024) throw new InvalidDataException("Resposta de danos excede o limite."); }
                            return VehicleDamage.Parse(result.ToString());
                        }
                    }
                }
                catch(WebException e) { first=e; }
            }
            throw first??new WebException("Leitura local de danos indisponível.");
        }
        public void Dispose() { lock(gate) { disposed=true;generation++;latest=null; } }
    }
}
