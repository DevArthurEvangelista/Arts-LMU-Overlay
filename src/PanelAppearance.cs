using System;
using System.Drawing;
using System.Runtime.Serialization;

namespace Arthur.LmuOverlay
{
    [DataContract]
    public sealed class PanelAppearance
    {
        public const int MinimumFont=60,MaximumFont=200;
        public static readonly string[] Families={"Segoe UI","Arial","Tahoma","Verdana","Trebuchet MS","Consolas"};
        [DataMember] public string FontFamily="Segoe UI";
        [DataMember] public int FontPercent=100;
        [OnDeserializing] void Defaults(StreamingContext context) { FontFamily="Segoe UI";FontPercent=100; }
        public void Normalize()
        {
            if(Array.IndexOf(Families,FontFamily)<0) FontFamily="Segoe UI";
            FontPercent=Math.Max(MinimumFont,Math.Min(MaximumFont,FontPercent));
        }
        public void Reset() { FontFamily="Segoe UI";FontPercent=100; }
        public static PanelAppearance[] NormalizeAll(PanelAppearance[] items)
        {
            var result=new PanelAppearance[7];
            for(int i=0;i<result.Length;i++)
            { result[i]=items!=null && i<items.Length && items[i]!=null ? items[i] : new PanelAppearance();result[i].Normalize(); }
            return result;
        }
        public Size SuggestedSize(Size original)
        { return new Size((int)Math.Ceiling(original.Width*FontPercent/100.0),(int)Math.Ceiling(original.Height*FontPercent/100.0)); }
    }
}
