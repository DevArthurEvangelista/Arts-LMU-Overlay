using System;
using System.Drawing;
namespace Arthur.LmuOverlay
{
 public sealed partial class Painter
 {
  public void DeltaBar(Graphics g,Frame f,ViewOptions o,bool unlocked)
  {
   Prepare(g);if(o.DeltaOpacity>0||unlocked)Shell(g,460,90,unlocked?Math.Max(35,o.DeltaOpacity):o.DeltaOpacity,unlocked);
   int m=DeltaMode.Normalize(o.DeltaReference);var s=f.Delta;double v=f.HasTelemetry&&!f.Stale?s.Values[m]:double.NaN;bool live=Format.Finite(v);
   Color c=!live||Math.Abs(v)<.0005?Theme.Text:v<0?Theme.Throttle:Theme.Brake;
   if(unlocked)Text(g,"POSICIONAR · DELTA",14,0,432,18,11,Theme.Text,true);
   Text(g,live?LearningFormat.Delta(v,3):"—",12,16,436,43,36,c,true,true,StringAlignment.Center);
   float h=(float)Format.Clamp(o.DeltaThickness,6,28),y=58+(28-h)/2;
   Box(g,Alpha(Color.Black,165),21,y-1,418,h+2,Math.Min(6,h/2));Box(g,Alpha(Theme.Grid,210),22,y,416,h,Math.Min(5,h/2));
   if(live){float w=(float)(Format.Clamp(Math.Abs(v)/Math.Max(1,o.DeltaRange),0,1)*208);if(w>.1)Box(g,c,v<0?230-w:230,y,w,h,2);}
   Line(g,Theme.Text,230,y-2,230,y+h+2,2);
  }
 }
}
