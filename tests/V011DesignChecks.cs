using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Arthur.LmuOverlay;

partial class Checks
{
    static void Render011(string directory)
    {
        var options=new ViewOptions();var history=new InputHistory();
        for(int i=0;i<=2000;i++)
        {
            double t=i/100.0,cycle=t%5;
            double brake=cycle<.65 ? Math.Sin(cycle/.65*Math.PI)*.86 : 0;
            double throttle=cycle>1.1 ? Math.Min(1,(cycle-1.1)/1.7) : 0;
            history.Add(t,throttle,brake);
        }
        var plot=history.Plot(20,options.HistorySeconds);
        Assert(plot.Start==10 && plot.Samples[0].Time==10 && plot.Samples[plot.Samples.Count-1].Time==20,"current product view uses the last ten seconds with both endpoints");
        var f=PreviewData.Create(315);f.Throttle=.62;f.Brake=.18;f.Steering=0;f.PhysicalWheel=true;f.PhysicalWheelAngle=0;f.Rpm=7420;f.Gear=4;f.Speed=212;
        using(var painter=new Painter())
        {
            using(var bitmap=new Bitmap(760,280)) using(var g=Graphics.FromImage(bitmap))
            { painter.Pedals(g,f,history,20,options,false);bitmap.Save(Path.Combine(directory,"cockpit-v011.png"),ImageFormat.Png); }
            f.Throttle=f.Brake=1;
            using(var bitmap=new Bitmap(760,280)) using(var g=Graphics.FromImage(bitmap))
            { painter.Pedals(g,f,history,20,options,false);bitmap.Save(Path.Combine(directory,"cockpit-dois-pedais-100.png"),ImageFormat.Png); }
            f.Throttle=.62;f.Brake=.18;
            using(var bitmap=new Bitmap(1280,660)) using(var g=Graphics.FromImage(bitmap))
            using(var logo=BrandArtwork.LoadLogo())
            using(var text=new SolidBrush(Theme.Muted))
            using(var font=new Font(Theme.BodyFont,15,FontStyle.Regular,GraphicsUnit.Pixel))
            {
                g.Clear(Theme.Background);var state=g.Save();g.TranslateTransform(32,0);BrandArtwork.Header(g,logo,1216,108);g.Restore(state);
                state=g.Save();g.TranslateTransform(32,132);g.ScaleTransform(1.6f,1.6f);painter.Pedals(g,f,history,20,options,false);g.Restore(state);
                g.DrawString("Freio à esquerda  /  Acelerador à direita  /  RPM em arco  /  Histórico de 10 segundos",font,text,32,592);
                g.DrawString("Prévia do renderer do aplicativo com dados ilustrativos. Não é uma captura dentro do LMU.",font,text,32,622);
                bitmap.Save(Path.Combine(directory,"Previa-Arts-LMU-Overlay-0.11.0.png"),ImageFormat.Png);
            }
        }
        Console.WriteLine("PASS: "+count+" assertions including the ten-second cockpit view; renders include both pedals at 100%.");
    }
}
