using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace Arthur.LmuOverlay
{
    public static class GarageRange
    {
        public static double Parse(string json)
        {
            if(json==null || json.Length>1024*1024) return 0;
            var quotas=new XmlDictionaryReaderQuotas { MaxDepth=32,MaxStringContentLength=1024*1024,MaxArrayLength=8192 };
            using(var r=JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json),quotas))
            {
                while(r.Read()) if(r.NodeType==XmlNodeType.Element && r.LocalName=="VM_STEER_LOCK")
                {
                    using(var field=r.ReadSubtree()) while(field.Read())
                    {
                        if(field.NodeType!=XmlNodeType.Element || field.LocalName!="stringValue") continue;
                        string value=field.ReadElementContentAsString();
                        var m=Regex.Match(value,@"^\s*(\d{2,4}(?:[.,]\d+)?)\s*(?:°|deg(?:rees)?|graus)?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
                        double range;
                        return m.Success && double.TryParse(m.Groups[1].Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out range) && range>=90 && range<=2880 ? range : 0;
                    }
                }
            }
            return 0;
        }
    }
    public sealed class SteeringSetup : IDisposable
    {
        const string Endpoint="http://127.0.0.1:6397/rest/garage/getPlayerGarageData";
        readonly object gate=new object();
        string key="";
        double next,received,value;
        bool busy,disposed;
        public void Apply(Frame frame,double now)
        {
            string wanted=frame.SessionKey+"/"+frame.PlayerId+"/"+frame.VehicleModel;
            lock(gate)
            {
                if(disposed) return;
                if(wanted!=key || !frame.Connected) { key=wanted;value=0;next=0; }
                frame.GarageSteeringRange=now-received<=30 ? value : 0;
                if(!frame.HasTelemetry || frame.SteeringRange>0 || busy || now<next) return;
                busy=true;next=now+5;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                double result=0;
                try { result=Read(); }
                catch(Exception ex)
                {
                    if(!(ex is InvalidDataException) && !(ex is WebException) && !(ex is IOException) && !(ex is System.Runtime.Serialization.SerializationException) && !(ex is XmlException) && !(ex is ArgumentException) && !(ex is InvalidOperationException)) throw;
                }
                finally
                {
                    lock(gate) { if(!disposed && key==wanted && result>0) { value=result;received=now; } busy=false; }
                }
            });
        }
        static double Read()
        {
            var request=(HttpWebRequest)WebRequest.Create(Endpoint);request.Method="GET";request.Proxy=null;
            request.AllowAutoRedirect=false;request.UseDefaultCredentials=false;request.Timeout=request.ReadWriteTimeout=900;
            using(var response=(HttpWebResponse)request.GetResponse())
            {
                if(response.StatusCode!=HttpStatusCode.OK || response.ContentLength>1024*1024) return 0;
                using(var r=new StreamReader(response.GetResponseStream(),Encoding.UTF8))
                {
                    var data=new StringBuilder();var chars=new char[4096];int n;
                    while((n=r.Read(chars,0,chars.Length))>0) { data.Append(chars,0,n);if(data.Length>1024*1024) return 0; }
                    return GarageRange.Parse(data.ToString());
                }
            }
        }
        public void Dispose() { lock(gate) { disposed=true;value=0; } }
    }
}
