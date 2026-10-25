using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Arthur.LmuOverlay
{
    public static class PedalDesign
    {
        public const int HistorySeconds=10;
        public const float WheelX=568,WheelY=129,ArcRadius=82;
        public const int Segments=28;
        public static double RpmRatio(Frame f)
        { return f.HasTelemetry && !f.Stale && f.MaxRpm>0 ? Format.Clamp(f.Rpm/f.MaxRpm,0,1) : 0; }
    }
    public sealed partial class Painter
    {
        void RpmArc(Graphics g,Frame frame,bool flash)
        {
            // Use the same uniform scale as the wheel: free resizing must not
            // turn the RPM ring into an ellipse around a circular instrument.
            var state=g.Save();float sx,sy;
            using(var matrix=g.Transform) { var m=matrix.Elements;sx=Math.Abs(m[0]);sy=Math.Abs(m[3]); }
            g.TranslateTransform(PedalDesign.WheelX,PedalDesign.WheelY);
            float uniform=Math.Min(sx,sy);if(sx>0 && sy>0) g.ScaleTransform(uniform/sx,uniform/sy);
            double ratio=PedalDesign.RpmRatio(frame);
            var circle=new RectangleF(-PedalDesign.ArcRadius,-PedalDesign.ArcRadius,PedalDesign.ArcRadius*2,PedalDesign.ArcRadius*2);
            using(var guide=new Pen(Alpha(Theme.Grid,135),1)) g.DrawArc(guide,-71,-71,142,142,140,260);
            for(int i=0;i<PedalDesign.Segments;i++)
            {
                float start=140+i*260f/PedalDesign.Segments;
                Color color=i<17 ? Theme.Throttle : i<24 ? Theme.Fuel : Theme.Brake;
                bool lit=ratio>=(i+1)/(double)PedalDesign.Segments;
                using(var pen=new Pen(flash ? Theme.Brake : lit ? color : Alpha(Theme.Grid,215),6))
                { pen.StartCap=pen.EndCap=LineCap.Flat;g.DrawArc(pen,circle,start,260f/PedalDesign.Segments-2.2f); }
            }
            g.Restore(state);
        }
        void InputColumn(Graphics g,float x,double input,Color color,bool live)
        {
            const float y=61,height=166,width=28;
            Box(g,Alpha(Theme.Grid,160),x,y,width,height,8);
            using(var border=Rounded(x,y,width,height,8)) using(var pen=new Pen(Alpha(color,110),1)) g.DrawPath(pen,border);
            if(live && input>0)
            {
                float fill=height*(float)Format.Clamp(input,0,1);
                var state=g.Save();using(var clip=Rounded(x,y,width,height,8)) g.SetClip(clip,CombineMode.Intersect);
                Box(g,color,x,y+height-fill,width,fill);
                Line(g,Alpha(Theme.Text,150),x+4,y+height-fill+1,x+width-4,y+height-fill+1,1);
                g.Restore(state);
            }
            Text(g,live ? (input*100).ToString("0")+"%" : "—",x-20,232,68,35,26,Theme.Text,true,true,StringAlignment.Center);
        }
        public void Pedals(Graphics g,Frame f,InputHistory history,double now,ViewOptions o,bool unlocked)
        {
            Prepare(g);Shell(g,PedalsWidth,PedalsHeight,o.PedalOpacity,unlocked);
            bool live=f.HasTelemetry && !f.Stale,shift=live && Instruments.ShiftNow(f,o.ShiftRpm);
            bool pulse=shift && (!o.PulseShift || now%.5<.25);
            const float gx=20,gy=61,gw=337,gh=166;
            Text(g,"ÚLTIMOS 10 s",20,13,210,29,12,Theme.Muted,true,false,StringAlignment.Near,.075f);
            if(unlocked) Text(g,"POSICIONAR",234,13,123,29,11,Theme.Rpm,true,false,StringAlignment.Far);
            else if(live) { Box(g,Theme.Throttle,280,25,5,5,2.5f);Text(g,"AO VIVO",291,14,66,27,10,Theme.Muted,true,false,StringAlignment.Far); }
            Box(g,Alpha(Theme.Surface,116),gx,gy,gw,gh,9);
            for(int i=0;i<=4;i++) Line(g,Alpha(Theme.Grid,i==0 || i==4 ? 145 : 90),gx+1,gy+i*gh/4,gx+gw-1,gy+i*gh/4);
            for(int i=1;i<5;i++) Line(g,Alpha(Theme.Grid,75),gx+i*gw/5,gy+1,gx+i*gw/5,gy+gh-1);
            var plot=history.Plot(now,PedalDesign.HistorySeconds);
            if(live)
            {
                var state=g.Save();g.SetClip(new RectangleF(gx-2,gy-2,gw+4,gh+4));
                Trace(g,plot,false,gx,gy,gw,gh);Trace(g,plot,true,gx,gy,gw,gh);g.Restore(state);
            }
            else
            {
                Text(g,f.Stale ? "Leitura pausada" : "Aguardando seu carro",gx+10,gy+45,gw-20,38,18,Theme.Text,true,false,StringAlignment.Center);
                Text(g,"Entre na pista para acompanhar.",gx+10,gy+85,gw-20,30,12,Theme.Muted,false,false,StringAlignment.Center);
            }
            Text(g,plot.Filling ? "0 s" : "−10 s",20,239,100,24,11,Theme.Muted);
            Text(g,plot.Filling ? "10 s" : "AGORA",257,239,100,24,11,Theme.Muted,false,false,StringAlignment.Far);
            Line(g,Alpha(Theme.Grid,130),374,61,374,226);
            // Pedal order follows the physical car: brake left, throttle right.
            InputColumn(g,388,f.Brake,Theme.Brake,live);
            InputColumn(g,454,f.Throttle,Theme.Throttle,live);
            Text(g,shift ? "TROQUE" : "RPM",498,14,140,27,11,shift ? Theme.Brake : Theme.Muted,true,false,StringAlignment.Center,.09f);
            RpmArc(g,f,pulse);
            Wheel(g,Instruments.SteeringDrawing(f,o.SteeringRange,o.SteeringGain,o.CarMaximumRotation),PedalDesign.WheelX,PedalDesign.WheelY);
            double angle=Instruments.SteeringAngle(f,o.SteeringRange,o.SteeringGain,o.CarMaximumRotation);
            string degrees=Format.Finite(angle) ? (angle>1 ? "+" : angle< -1 ? "−" : "")+Math.Abs(angle).ToString("0")+"°" : "—";
            Text(g,degrees,510,183,116,29,18,Theme.Muted,true,true,StringAlignment.Center);
            Text(g,live ? f.Rpm.ToString("0") : "—",491,217,154,46,32,shift ? Theme.Brake : Theme.Text,true,true,StringAlignment.Center);
            bool limiter=live && f.PitLimiter;
            Box(g,Alpha(Theme.Surface,105),659,51,86,174,13);
            Text(g,limiter ? "LIMITADOR" : "MARCHA",655,14,94,27,limiter ? 10 : 11,limiter ? Theme.Fuel : Theme.Muted,true,false,StringAlignment.Center,.04f);
            Text(g,!live ? "—" : f.Gear==0 ? "N" : f.Gear<0 ? "R" : f.Gear.ToString(),659,57,86,113,90,limiter ? Theme.Fuel : shift ? Theme.Brake : Theme.Text,true,true,StringAlignment.Center);
            Line(g,Alpha(Theme.Grid,130),674,175,730,175);
            Text(g,live ? f.Speed.ToString("0") : "—",648,184,108,47,35,Theme.Text,true,true,StringAlignment.Center);
            Text(g,"km/h",662,237,82,24,12,Theme.Muted,false,false,StringAlignment.Center);
        }
    }
}
