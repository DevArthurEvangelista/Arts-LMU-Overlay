using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Arthur.LmuOverlay
{
    public sealed partial class Painter
    {
        static Color DamageColor(double damage)
        { return !Format.Finite(damage) ? Theme.Muted : damage<.02 ? Theme.Throttle : damage<.15 ? Theme.Fuel : Theme.Brake; }
        static string DamagePercent(double damage)
        { return Format.Finite(damage) ? (damage*100).ToString("0.0",Format.Br)+"%" : "—"; }
        void TyresAndDamage(Graphics g,Frame f,bool live,string state)
        {
            WidgetHeader(g,"PNEUS · DANOS",420,state.Length>0 ? state : "Pneu restante • dano na suspensão");
            var damage=live ? f.Damage : new VehicleDamage();
            string[] names={"DIANT. ESQ.","DIANT. DIR.","TRAS. ESQ.","TRAS. DIR."};
            for(int i=0;i<4;i++)
            {
                float x=i%2==0 ? 12 : 256,y=70+i/2*140;
                var t=live && f.Tyres.Length>i ? f.Tyres[i] : null;
                double suspension=damage.Suspension[i];
                Color temperature=t!=null && Format.Finite(t.Optimal) && Format.Finite(t.Temperature) ?
                    t.Temperature>t.Optimal+15 ? Theme.Brake : t.Temperature<t.Optimal-15 ? Theme.TyreWet : Theme.Throttle : Theme.Text;
                Color remaining=t==null || !Format.Finite(t.Remaining) ? Theme.Muted : t.Remaining>.7 ? Theme.Throttle : t.Remaining>.3 ? Theme.Fuel : Theme.Brake;
                Box(g,Alpha(Theme.Surface,210),x,y,152,132,12);
                Text(g,names[i],x+8,y+1,136,22,12,Theme.Muted,true);
                Text(g,t==null || !Format.Finite(t.Temperature) ? "—" : t.Temperature.ToString("0")+"°C",x+8,y+22,136,32,27,temperature,true,true,StringAlignment.Center);
                string pressure=t!=null && t.Detached ? "SEM RODA" : t!=null && t.Flat ? "FURADO" : t==null || !Format.Finite(t.Pressure) ? "— bar" : t.Pressure.ToString("0.00",Format.Br)+" bar";
                Text(g,pressure,x+8,y+54,136,23,16,t!=null && (t.Flat || t.Detached) ? Theme.Brake : Theme.Text,true,true,StringAlignment.Center);
                Text(g,"PNEU",x+9,y+78,52,22,11,Theme.Muted,true);
                Text(g,t==null || !Format.Finite(t.Remaining) ? "—" : (t.Remaining*100).ToString("0",Format.Br)+"%",x+57,y+77,86,23,18,remaining,true,true,StringAlignment.Far);
                Box(g,Theme.Grid,x+9,y+101,134,4,2);
                if(t!=null && Format.Finite(t.Remaining) && t.Remaining>0) Box(g,remaining,x+9,y+101,(float)(134*t.Remaining),4,2);
                Text(g,"SUSP.",x+9,y+107,51,23,11,Theme.Muted,true);
                Text(g,DamagePercent(suspension),x+60,y+107,83,23,17,DamageColor(suspension),true,true,StringAlignment.Far);
            }
            // This single car silhouette represents OVERALL aero damage.
            // Front and rear are not presented as independently measured parts.
            Color aero=DamageColor(damage.Aero);
            using(var body=new GraphicsPath())
            {
                body.AddPolygon(new[]{new PointF(199,112),new PointF(221,112),new PointF(238,157),new PointF(236,211),new PointF(232,252),new PointF(235,298),new PointF(185,298),new PointF(188,252),new PointF(184,211),new PointF(182,157)});
                g.FillPath(Brush(Alpha(Theme.Surface,220)),body);
                using(var pen=new Pen(Alpha(aero,175),2)) g.DrawPath(pen,body);
            }
            Line(g,Alpha(aero,170),182,94,238,94,5);Line(g,Alpha(aero,170),180,318,240,318,5);
            Line(g,Theme.Grid,188,163,232,163,2);Line(g,Theme.Grid,188,258,232,258,2);
            Text(g,"AERO",170,181,80,22,12,Theme.Muted,true,false,StringAlignment.Center);
            Text(g,DamagePercent(damage.Aero),167,204,86,31,21,aero,true,true,StringAlignment.Center);
            Text(g,"DANO TOTAL",166,234,88,20,10,Theme.Muted,true,false,StringAlignment.Center);
            string footer=live && f.DetachedParts ? "PEÇA SOLTA • posição não informada" : !live || !damage.Any ? "Suspensão / aero: aguardando leitura" : "Danos do jogo • 0% = sem dano informado";
            Text(g,footer,13,351,394,22,12,live && f.DetachedParts ? Theme.Brake : Theme.Text,true,false,StringAlignment.Center);
            Text(g,"Asas dianteira/traseira: sem leitura separada",13,373,394,21,11,Theme.Muted,false,false,StringAlignment.Center);
        }
    }
}
