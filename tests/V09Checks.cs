using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Arthur.LmuOverlay;

partial class Checks
{
    static void Version09()
    {
        var baseSize=new Size(760,280);var minimum=PanelSizing.Minimum(baseSize);
        Assert(PanelSizing.Fit(500,180,minimum.Width,minimum.Height,1920,1080)==new Size(500,180),"pedal card can shrink below its 0.8 native size");
        var area=new Rectangle(0,0,1920,1080);var start=new Rectangle(500,300,760,280);
        var left=PanelSizing.Resize(start,new Point(140,0),PanelSizing.Left,minimum,area);
        Assert(left.Width==620 && left.Right==start.Right && left.Height==280,"left grip resizes while anchoring right side");
        var top=PanelSizing.Resize(start,new Point(0,70),PanelSizing.Top,minimum,area);
        Assert(top.Height==210 && top.Bottom==start.Bottom && top.Width==760,"top grip resizes independently and anchors bottom");
        var right=PanelSizing.Resize(start,new Point(120,70),PanelSizing.Right,minimum,area);
        Assert(right.Width==880 && right.Height==280 && right.Location==start.Location,"right grip changes width only");
        var bottom=PanelSizing.Resize(start,new Point(120,70),PanelSizing.Bottom,minimum,area);
        Assert(bottom.Height==350 && bottom.Width==760,"bottom grip changes height only");
        var corner=PanelSizing.Resize(start,new Point(-200,-50),PanelSizing.Right|PanelSizing.Bottom,minimum,area);
        Assert(corner.Size==new Size(560,230),"corner allows independent aspect ratio");
        corner=PanelSizing.Resize(start,new Point(4000,4000),PanelSizing.Right|PanelSizing.Bottom,minimum,area);
        Assert(corner.Right==area.Right && corner.Bottom==area.Bottom,"resizing never exceeds monitor work area");
        corner=PanelSizing.Resize(start,new Point(-4000,-4000),PanelSizing.Right|PanelSizing.Bottom,minimum,area);
        Assert(corner.Size==minimum,"invalid negative drag remains a usable card");
        Assert(PanelSizing.HitEdges(new Point(5,5),baseSize)==(PanelSizing.Left|PanelSizing.Top),"top-left hit target");
        Assert(PanelSizing.HitEdges(new Point(755,275),baseSize)==(PanelSizing.Right|PanelSizing.Bottom),"bottom-right hit target");
        Assert(PanelSizing.HitEdges(new Point(100,100),baseSize)==0,"card interior still moves the window");
        var options=PanelAppearance.NormalizeAll(null);
        Assert(options.Length==7 && options.All(x=>x.FontPercent==100 && x.FontFamily=="Segoe UI"),"legacy settings default each card to approved typography");
        options[0].FontPercent=150;options[0].FontFamily="Verdana";
        Assert(options.Skip(1).All(x=>x.FontPercent==100 && x.FontFamily=="Segoe UI"),"editing one card does not mutate siblings");
        using(var stream=new MemoryStream())
        {
            var serializer=new DataContractSerializer(typeof(PanelAppearance[]));serializer.WriteObject(stream,options);stream.Position=0;
            var read=(PanelAppearance[])serializer.ReadObject(stream);
            Assert(read[0].FontPercent==150 && read[0].FontFamily=="Verdana" && read[5].FontPercent==100,"per-card typography survives save/reopen serialization");
        }
        Assert(options[0].SuggestedSize(baseSize)==new Size(1140,420),"fit-to-text supplies space for 150 percent glyphs");
        options[0].FontFamily="invalid";options[0].FontPercent=999;options[0].Normalize();
        Assert(options[0].FontPercent==200 && options[0].FontFamily=="Segoe UI","damaged typography settings are safely bounded");
        options[0].Reset();Assert(options[0].FontPercent==100,"restore defaults resets only the selected appearance");
    }
    static void Render09(string directory)
    {
        var f=PreviewData.Create(315);f.CurrentLap=86.253;
        var history=new InputHistory();for(int i=0;i<=1800;i++) { var q=PreviewData.Create(i/120.0);history.Add(i/120.0,q.Throttle,q.Brake); }
        var options=new ViewOptions();
        foreach(var kind in (PanelKind[])Enum.GetValues(typeof(PanelKind)))
        {
            Size basis=kind==PanelKind.Pedals ? new Size(760,280) : kind==PanelKind.Standings ? new Size(900,Painter.TableHeight(f)) : Painter.WidgetSize(kind);
            foreach(int percent in new[]{60,100,150,200})
            {
                Size size=new Size((int)(basis.Width*percent/100.0),(int)(basis.Height*percent/100.0));
                using(var p=new Painter()) using(var b=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb)) using(var g=Graphics.FromImage(b))
                {
                    g.Clear(Color.Transparent);g.ScaleTransform(size.Width/(float)basis.Width,size.Height/(float)basis.Height);
                    p.Appearance=new PanelAppearance { FontPercent=percent,FontFamily=percent==150 ? "Verdana" : "Segoe UI" };
                    if(kind==PanelKind.Pedals) p.Pedals(g,f,history,15,options,false);
                    else if(kind==PanelKind.Standings) p.Standings(g,f,options,false);
                    else p.Widget(g,kind,f,options,false);
                    b.Save(Path.Combine(directory,"v09-"+kind+"-"+percent+".png"),ImageFormat.Png);
                }
            }
        }
    }
}
