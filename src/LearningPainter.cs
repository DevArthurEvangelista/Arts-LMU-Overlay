using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace Arthur.LmuOverlay
{
    // Used both by the native controls and the render verification. No data
    // collection, file IO or live overlay state belongs to these drawings.
    public static class LearningPainter
    {
        static void Text(Graphics g,string text,RectangleF box,float size,Color color,bool bold=false,bool mono=false)
        {
            using(var f=new Font(mono ? Theme.DataFont : Theme.BodyFont,size,bold ? FontStyle.Bold : FontStyle.Regular,GraphicsUnit.Pixel))
            using(var b=new SolidBrush(color))
            using(var format=new StringFormat { Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap,LineAlignment=StringAlignment.Center })
                g.DrawString(text,f,b,box,format);
        }
        static void Setup(Graphics g,Rectangle bounds)
        { g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;using(var b=new SolidBrush(Theme.Surface)) g.FillRectangle(b,bounds); }
        public static Color DeltaColor(double delta)
        { return !Format.Finite(delta) ? Theme.Grid : Math.Abs(delta)<.005 ? Theme.Muted : delta<0 ? Theme.Throttle : Theme.Brake; }
        static bool Coordinate(LearningSample p)
        { return Format.Finite(p.X) && Format.Finite(p.Z) && Math.Abs(p.X)<1e6 && Math.Abs(p.Z)<1e6; }
        sealed class Projection
        {
            readonly double minX,minZ,scale;
            readonly float x,y;
            public Projection(LearningComparison c,Rectangle box)
            {
                var points=c.A.Samples.Concat(c.B.Samples).Where(Coordinate).ToArray();
                if(points.Length==0) { scale=1;return; }
                minX=points.Min(p=>p.X);minZ=points.Min(p=>p.Z);
                double w=Math.Max(1,points.Max(p=>p.X)-minX),h=Math.Max(1,points.Max(p=>p.Z)-minZ);
                scale=Math.Min(Math.Max(1,box.Width-48)/w,Math.Max(1,box.Height-100)/h);
                x=box.Left+(float)((box.Width-w*scale)/2);y=box.Top+50+(float)((box.Height-100-h*scale)/2);
            }
            public PointF Point(LearningSample p)
            { return new PointF(x+(float)((p.X-minX)*scale),y+(float)((p.Z-minZ)*scale)); }
        }
        static int Segment(LearningComparison c,double distance)
        { return Math.Max(0,Math.Min(c.Segments.Length-1,(int)(distance/c.Segments[0].End))); }
        static bool MapLink(LearningSample a,LearningSample b)
        { return Coordinate(a) && Coordinate(b) && LearningContinuity.Link(a,b) && Math.Abs(a.X-b.X)+Math.Abs(a.Z-b.Z)<500; }
        public static void Map(Graphics g,Rectangle box,LearningComparison c,int selected)
        {
            Setup(g,box);Text(g,"MAPA • GANHO / PERDA POR TRECHO",new RectangleF(box.X+16,box.Y+8,box.Width-32,28),13,Theme.Text,true);
            if(c==null) { Text(g,"Selecione duas voltas para comparar.",new RectangleF(box.X+20,box.Y+box.Height/2-15,box.Width-40,30),15,Theme.Muted);return; }
            var project=new Projection(c,box);
            using(var reference=new Pen(Theme.Grid,3))
                for(int i=1;i<c.B.Samples.Length;i++) if(MapLink(c.B.Samples[i-1],c.B.Samples[i]))
                    g.DrawLine(reference,project.Point(c.B.Samples[i-1]),project.Point(c.B.Samples[i]));
            using(var pen=new Pen(Theme.Muted,6) { StartCap=LineCap.Round,EndCap=LineCap.Round })
            using(var outline=new Pen(Theme.Text,11) { StartCap=LineCap.Round,EndCap=LineCap.Round })
            {
                for(int i=1;i<c.A.Samples.Length;i++)
                {
                    var a=c.A.Samples[i-1];var b=c.A.Samples[i];if(!MapLink(a,b)) continue;
                    if(Segment(c,(a.Distance+b.Distance)/2)==selected) g.DrawLine(outline,project.Point(a),project.Point(b));
                }
                for(int i=1;i<c.A.Samples.Length;i++)
                {
                    var a=c.A.Samples[i-1];var b=c.A.Samples[i];if(!MapLink(a,b)) continue;
                    int segment=Segment(c,(a.Distance+b.Distance)/2);var p=project.Point(a);var q=project.Point(b);
                    pen.Color=DeltaColor(c.Segments[segment].Delta);g.DrawLine(pen,p,q);
                }
            }
            var start=c.A.Samples.FirstOrDefault(p=>Coordinate(p) && p.Distance<15);
            if(start!=null)
            {
                var at=project.Point(start);
                using(var b=new SolidBrush(Theme.Text)) g.FillEllipse(b,at.X-5,at.Y-5,10,10);
                Text(g,"Largada",new RectangleF(at.X+10,at.Y-14,80,24),12,Theme.Text,true);
            }
            string caption="Clique no traçado ou escolha um trecho na tabela.";
            if(selected>=0 && selected<c.Segments.Length)
            {
                var s=c.Segments[selected];caption=s.Start.ToString("0")+"–"+s.End.ToString("0")+" m  •  "+(s.Available ? LearningFormat.Delta(s.Delta) : "sem comparação");
            }
            Text(g,caption,new RectangleF(box.X+16,box.Bottom-36,box.Width-32,28),13,Theme.Muted,true);
        }
        public static int HitMap(LearningComparison c,Rectangle box,Point location)
        {
            if(c==null) return -1;var p=new Projection(c,box);double best=28*28;int result=-1;
            for(int i=1;i<c.A.Samples.Length;i++)
            {
                var a=c.A.Samples[i-1];var b=c.A.Samples[i];if(!MapLink(a,b)) continue;var u=p.Point(a);var v=p.Point(b);
                double dx=v.X-u.X,dy=v.Y-u.Y,len=dx*dx+dy*dy;
                double t=len>0 ? Format.Clamp(((location.X-u.X)*dx+(location.Y-u.Y)*dy)/len,0,1) : 0;
                double ex=location.X-u.X-dx*t,ey=location.Y-u.Y-dy*t,d=ex*ex+ey*ey;
                if(d<best) { best=d;result=Segment(c,a.Distance+(b.Distance-a.Distance)*t); }
            }
            return result;
        }
        public static void Charts(Graphics g,Rectangle box,LearningComparison c,int selected)
        {
            Setup(g,box);
            if(c==null) { Text(g,"Delta e velocidade aparecerão aqui.",new RectangleF(box.X+16,box.Y+box.Height/2-15,box.Width-32,30),15,Theme.Muted);return; }
            int half=box.Height/2;
            Plot(g,new Rectangle(box.X,box.Y,box.Width,half),c,selected,false);
            Plot(g,new Rectangle(box.X,box.Y+half,box.Width,box.Height-half),c,selected,true);
        }
        static void Plot(Graphics g,Rectangle box,LearningComparison c,int selected,bool speed)
        {
            if(box.Width<100 || box.Height<60) return;
            var plot=new RectangleF(box.X+59,box.Y+32,box.Width-80,Math.Max(10,box.Height-59));
            var first=speed ? c.SpeedA : c.Delta;var second=speed ? c.SpeedB : null;
            double max=speed ? c.SpeedA.Concat(c.SpeedB).Where(Format.Finite).DefaultIfEmpty(100).Max()*1.08 : Math.Max(.1,c.Delta.Where(Format.Finite).Select(Math.Abs).DefaultIfEmpty(1).Max()*1.08);
            max=Math.Max(speed ? 50 : .1,max);double min=speed ? 0 : -max;
            string title=speed ? "VELOCIDADE • km/h     A branca / B azul" : "DELTA ACUMULADO • A − B (s)";
            Text(g,title,new RectangleF(box.X+15,box.Y+2,box.Width-30,27),12,Theme.Text,true);
            using(var pen=new Pen(Theme.Grid))
            {
                for(int k=0;k<=2;k++)
                {
                    float y=plot.Top+plot.Height*k/2;g.DrawLine(pen,plot.Left,y,plot.Right,y);
                    double value=max-(max-min)*k/2;Text(g,value.ToString(speed ? "0" : "+0.00;-0.00;0.00",Format.Br),new RectangleF(box.X+3,y-10,53,20),11,Theme.Muted,false,true);
                }
            }
            if(selected>=0 && selected<c.Segments.Length)
            {
                var s=c.Segments[selected];float x=plot.Left+(float)(s.Start/c.Length)*plot.Width,w=(float)((s.End-s.Start)/c.Length)*plot.Width;
                using(var b=new SolidBrush(Color.FromArgb(65,Theme.Player))) g.FillRectangle(b,x,plot.Top,Math.Max(1,w),plot.Height);
            }
            var state=g.Save();g.SetClip(plot);
            using(var pen=new Pen(Theme.Text,2))
            {
                for(int series=0;series<(speed ? 2 : 1);series++)
                {
                    var values=series==0 ? first : second;
                    for(int i=1;i<values.Length;i++)
                    {
                        if(!Format.Finite(values[i-1]) || !Format.Finite(values[i])) continue;
                        int segment=Segment(c,(c.Distance[i-1]+c.Distance[i])/2);
                        if(!speed && !c.Segments[segment].Available) continue;
                        pen.Color=speed ? (series==0 ? Theme.Text : Theme.TyreWet) : DeltaColor(values[i]);
                        float x1=plot.Left+(float)(c.Distance[i-1]/c.Length)*plot.Width,x2=plot.Left+(float)(c.Distance[i]/c.Length)*plot.Width;
                        float y1=plot.Bottom-(float)((values[i-1]-min)/(max-min))*plot.Height,y2=plot.Bottom-(float)((values[i]-min)/(max-min))*plot.Height;
                        g.DrawLine(pen,x1,y1,x2,y2);
                    }
                }
            }
            g.Restore(state);
            Text(g,"0 m",new RectangleF(plot.Left,plot.Bottom+2,55,20),11,Theme.Muted,false,true);
            Text(g,(c.Length/2).ToString("0")+" m",new RectangleF(plot.Left+plot.Width/2-28,plot.Bottom+2,90,20),11,Theme.Muted,false,true);
            Text(g,c.Length.ToString("0")+" m",new RectangleF(plot.Right-64,plot.Bottom+2,80,20),11,Theme.Muted,false,true);
        }
    }
}
