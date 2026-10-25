using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Arthur.LmuOverlay
{
    public sealed partial class Painter
    {
        public static Size WidgetSize(PanelKind kind)
        { return kind==PanelKind.Delta ? new Size(460,90) : kind==PanelKind.TrackMap ? new Size(340,310) : kind==PanelKind.Tyres ? new Size(420,402) : new Size(320,282); }
        void WidgetHeader(Graphics g,string title,int width,string note)
        {
            Box(g,Theme.Rpm,15,15,3,17,1.5f);
            Text(g,title,26,7,width-40,30,17,Theme.Text,true);
            Text(g,note,15,35,width-30,22,12,Theme.Muted);
            Line(g,Alpha(Theme.Grid,180),15,62,width-15,62);
        }
        void Metric(Graphics g,string label,string value,float y,Color color,bool primary=false)
        {
            Text(g,label,16,y,136,32,12,Theme.Muted);
            Text(g,value,149,y,155,32,22,color,true,true,StringAlignment.Far);
            Line(g,Alpha(Theme.Grid,135),16,y+32,304,y+32);
        }
        void HeroMetric(Graphics g,string label,string value,Color color)
        {
            Box(g,Alpha(Theme.Surface,150),10,70,300,66,9);
            Text(g,label,18,71,282,22,11,Theme.Muted,true,false,StringAlignment.Near,.055f);
            Text(g,value,16,92,288,41,34,color,true,true,StringAlignment.Far);
        }
        static string Quantity(double number,string suffix)
        { return Format.Finite(number) && number>=0 ? number.ToString("0.0",Format.Br)+suffix : "—"; }
        public void Widget(Graphics g,PanelKind kind,Frame f,ViewOptions o,bool unlocked)
        {
            if(kind==PanelKind.Delta) { DeltaBar(g,f,o,unlocked);return; }
            Prepare(g);var size=WidgetSize(kind);Shell(g,size.Width,size.Height,o.TableOpacity,unlocked);
            bool live=f.HasTelemetry && !f.Stale;var player=f.Drivers.FirstOrDefault(d=>d.IsPlayer);
            string state=unlocked ? "Arraste / redimensione pelas bordas" : f.Stale ? "Leitura pausada" : !live ? "Aguardando seu carro" : "";
            var w=f.Widgets;
            if(kind==PanelKind.LapTimer)
            {
                WidgetHeader(g,"LAP TIMER",320,state.Length>0 ? state : "Sua volta • "+f.PlayerClass);
                HeroMetric(g,f.LapInvalid ? "VOLTA ATUAL · INVÁLIDA" : "VOLTA ATUAL",live && f.CurrentLap>=0 ? Format.Lap(f.CurrentLap) : "—",f.LapInvalid ? Theme.Brake : Theme.Text);
                Metric(g,"ÚLTIMA",player==null ? "—" : Format.Lap(player.LastLap),140,Theme.Text);
                Metric(g,"MELHOR",player==null ? "—" : Format.Lap(player.BestLap),173,Theme.Text);
                Metric(g,"PREVISTA · EST.",live ? Format.Lap(w.PredictedLap) : "—",206,Theme.Throttle);
                Metric(g,"VOLTAS FEITAS",player==null ? "—" : player.Laps.ToString(),239,Theme.Text);
            }
            else if(kind==PanelKind.Fuel)
            {
                WidgetHeader(g,"COMBUSTÍVEL",320,state.Length>0 ? state : w.FuelSamples==0 ? "Consumo: aguardando voltas completas" : "Média das últimas "+w.FuelSamples+" voltas");
                HeroMetric(g,"NO TANQUE",live ? Quantity(f.Fuel," L") : "—",Theme.Fuel);
                Metric(g,"MÉDIA / VOLTA",live ? Quantity(w.FuelPerLap," L") : "—",140,Theme.Text);
                Metric(g,"AUTONOMIA · EST.",live ? Quantity(w.FuelLaps," v") : "—",173,Theme.Text);
                Metric(g,"ATÉ O FIM · EST.",live ? Quantity(w.FuelNeeded," L") : "—",206,Theme.Text);
                Metric(g,"ADICIONAR · EST.",live ? Quantity(w.FuelToAdd," L") : "—",239,Theme.Fuel);
            }
            else if(kind==PanelKind.Tyres)
            {
                TyresAndDamage(g,f,live,state);
            }
            else if(kind==PanelKind.TrackMap)
            {
                WidgetHeader(g,"MAPA · SUA CLASSE",340,state.Length>0 ? state : w.MapCoverage<.85 ? "Formando o traçado com dados reais" : "Posições da sua classe");
                DrawTrackMap(g,f,live);
            }
        }
        void DrawTrackMap(Graphics g,Frame f,bool live)
        {
            var points=f.Widgets.Map;
            if(points.Length<4)
            {
                Text(g,"AGUARDANDO O TRAÇADO",18,124,304,38,18,Theme.Text,true,false,StringAlignment.Center);
                Text(g,"O mapa se forma enquanto os carros rodam.",18,169,304,48,13,Theme.Muted,false,false,StringAlignment.Center);return;
            }
            double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minZ=points.Min(p=>p.Z),maxZ=points.Max(p=>p.Z);
            double scale=Math.Min(276/Math.Max(1,maxX-minX),184/Math.Max(1,maxZ-minZ));
            Func<double,double,PointF> xy=(x,z)=>new PointF((float)(170+(x-(minX+maxX)/2)*scale),(float)(172+(z-(minZ+maxZ)/2)*scale));
            using(var outline=new Pen(Theme.Grid,9))
            using(var road=new Pen(Theme.Muted,3))
            {
                outline.StartCap=outline.EndCap=road.StartCap=road.EndCap=LineCap.Round;
                for(int i=0;i<points.Length;i++)
                {
                    var a=points[i];var b=points[(i+1)%points.Length];
                    int gap=(b.Bin-a.Bin+720)%720;
                    double dist=Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
                    if(gap<1 || gap>6 || dist>Math.Max(80,f.TrackLength*.02)) continue;
                    var p=xy(a.X,a.Z);var q=xy(b.X,b.Z);g.DrawLine(outline,p,q);g.DrawLine(road,p,q);
                }
            }
            if(live) foreach(var d in f.Drivers.Where(d=>d.Class==f.PlayerClass && d.FinishStatus<=1).OrderBy(d=>d.IsPlayer ? 1 : 0))
            {
                if(!Format.Finite(d.WorldX) || !Format.Finite(d.WorldZ) || (d.WorldX==0 && d.WorldZ==0)) continue;
                var p=xy(d.WorldX,d.WorldZ);if(p.X<12 || p.X>328 || p.Y<70 || p.Y>270) continue;
                Color c=d.IsPlayer ? Theme.Rpm : d.InPits ? Theme.Fuel : Theme.Text;
                g.FillEllipse(Brush(d.IsPlayer ? Theme.Rpm : Theme.Background),p.X-11,p.Y-11,22,22);
                using(var pen=new Pen(c,d.IsPlayer ? 2.5f : 1.5f)) g.DrawEllipse(pen,p.X-10,p.Y-10,20,20);
                Text(g,d.ClassPosition>0 ? d.ClassPosition.ToString() : "?",p.X-14,p.Y-11,28,22,d.IsPlayer ? 14 : 12,d.IsPlayer ? Theme.Text : c,true,true,StringAlignment.Center);
            }
            Text(g,"AZUL: VOCÊ   •   NÚMERO: POSIÇÃO",15,278,310,24,12,Theme.Muted,true,false,StringAlignment.Center);
        }
    }
}
