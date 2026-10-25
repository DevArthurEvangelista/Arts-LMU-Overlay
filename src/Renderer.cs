using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace Arthur.LmuOverlay
{
    public static class Theme
    {
        public static readonly Color Background = ColorTranslator.FromHtml("#0C121B");
        public static readonly Color Surface = ColorTranslator.FromHtml("#182230");
        public static readonly Color Grid = ColorTranslator.FromHtml("#2C3D50");
        // Critical values stay close to white; metadata remains visibly separated
        // from the translucent game background without competing with the data.
        public static readonly Color Text = ColorTranslator.FromHtml("#FFFFFF");
        public static readonly Color Muted = ColorTranslator.FromHtml("#C5CFDD");
        public static readonly Color Throttle = ColorTranslator.FromHtml("#38E58C");
        public static readonly Color Brake = ColorTranslator.FromHtml("#FF5368");
        public static readonly Color Rpm = ColorTranslator.FromHtml("#89B9FF");
        public static readonly Color Fuel = ColorTranslator.FromHtml("#F4C56B");
        public static readonly Color Player = ColorTranslator.FromHtml("#1D3E6C");
        public static readonly Color TyreSoft = ColorTranslator.FromHtml("#AAAAAA");
        public static readonly Color TyreMedium = ColorTranslator.FromHtml("#FFCC00");
        public static readonly Color TyreHard = ColorTranslator.FromHtml("#EE2200");
        public static readonly Color TyreWet = ColorTranslator.FromHtml("#00AAFF");
        public const string BodyFont = "Segoe UI", DataFont = "Consolas", DisplayFont = "Bahnschrift";
        public static Color ClassColor(string name)
        {
            string s = name.ToUpperInvariant();
            return s.Contains("HYPER") || s.Contains("LMH") || s.Contains("LMDH") ? Brake : s.Contains("LMP") ? Fuel : Throttle;
        }
    }
    public sealed class ViewOptions
    {
        public int DeltaReference,DeltaOpacity,DeltaRange=2,DeltaThickness=16;
        public double HistorySeconds=PedalDesign.HistorySeconds, ShiftRpm, SteeringRange, WheelMaximumRotation, CarMaximumRotation;
        public int PedalOpacity=92, TableOpacity=92, SteeringGain=100;
        public bool PulseShift=true;
        public bool OnlyMyClass=true;
    }
    public sealed partial class Painter : IDisposable
    {
        public const int PedalsWidth=760, PedalsHeight=280, TableWidth=900;
        // Dense timing rows with larger data; only the player's class is drawn.
        public const int StandingsRowHeight=32;
        // Runtime panels use explicit pixel typography, independent of the
        // panel's aspect ratio. Null keeps the export renderer's DPI scaling.
        public PanelAppearance Appearance;
        readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
        readonly Dictionary<int,SolidBrush> brushes=new Dictionary<int,SolidBrush>();
        readonly Dictionary<string,Image> flags=new Dictionary<string,Image>();
        readonly Stream flagStream;
        readonly ZipArchive flagArchive;
        readonly Stream brandStream;
        readonly ZipArchive brandArchive;
        readonly Dictionary<string,Image> brands=new Dictionary<string,Image>();
        Bitmap wheelArtwork;
        public Painter()
        {
            flagStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Flags.zip");
            if(flagStream!=null) flagArchive=new ZipArchive(flagStream,ZipArchiveMode.Read);
            brandStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("CarLogos.zip");
            if(brandStream!=null) brandArchive=new ZipArchive(brandStream,ZipArchiveMode.Read);
        }
        Font Font(float size,bool bold,bool mono)
        {
            string family=mono ? Theme.DataFont : Appearance==null ? Theme.BodyFont : Appearance.FontFamily;
            string key=family+"/"+size.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+bold+"/"+mono;
            Font f;
            if(!fonts.TryGetValue(key,out f))
            {
                // Free resizing can visit many fractional sizes. Keep native GDI font handles bounded.
                if(fonts.Count>=256) { foreach(var old in fonts.Values) old.Dispose();fonts.Clear(); }
                f=new Font(family,size,bold ? FontStyle.Bold : FontStyle.Regular,GraphicsUnit.Pixel);fonts.Add(key,f);
            }
            return f;
        }
        SolidBrush Brush(Color c)
        {
            SolidBrush b;
            if(!brushes.TryGetValue(c.ToArgb(),out b)) { b=new SolidBrush(c); brushes.Add(c.ToArgb(),b); }
            return b;
        }
        static Color Alpha(Color c,int alpha) { return Color.FromArgb(Math.Max(0,Math.Min(255,alpha)),c); }
        static GraphicsPath Rounded(float x,float y,float w,float h,float r)
        {
            var p=new GraphicsPath(); float d=r*2;
            p.AddArc(x,y,d,d,180,90); p.AddArc(x+w-d,y,d,d,270,90);
            p.AddArc(x+w-d,y+h-d,d,d,0,90); p.AddArc(x,y+h-d,d,d,90,90); p.CloseFigure(); return p;
        }
        void Box(Graphics g,Color c,float x,float y,float w,float h,float radius=0)
        {
            if(w<=0 || h<=0) return;
            if(radius<=0) g.FillRectangle(Brush(c),x,y,w,h);
            else using(var p=Rounded(x,y,w,h,Math.Min(radius,Math.Min(w,h)/2))) g.FillPath(Brush(c),p);
        }
        static void DrawSpaced(Graphics g,string text,Font font,Brush brush,RectangleF box,StringFormat sf,float letterSpacingEm)
        {
            if(string.IsNullOrEmpty(text)) return;
            float spacing=Math.Max(0,font.Size*letterSpacingEm),total=0;
            for(int i=0;i<text.Length;i++) total+=g.MeasureString(text[i].ToString(),font,int.MaxValue,StringFormat.GenericTypographic).Width;
            if(text.Length>1) total+=spacing*(text.Length-1);
            float x=box.X;
            if(sf.Alignment==StringAlignment.Center) x=box.X+(box.Width-total)/2;
            else if(sf.Alignment==StringAlignment.Far) x=box.Right-total;
            var one=new StringFormat(StringFormat.GenericTypographic) { Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.None,FormatFlags=StringFormatFlags.NoWrap };
            for(int i=0;i<text.Length;i++)
            {
                string glyph=text[i].ToString();
                g.DrawString(glyph,font,brush,new RectangleF(x,box.Y,Math.Max(1,box.Right-x),box.Height),one);
                x+=g.MeasureString(glyph,font,int.MaxValue,StringFormat.GenericTypographic).Width+spacing;
            }
            one.Dispose();
        }
        void Text(Graphics g,string text,float x,float y,float w,float h,float size,Color c,bool bold=false,bool mono=false,StringAlignment align=StringAlignment.Near,float letterSpacingEm=0)
        {
            // Keep glyphs proportional under independent width/height resizing; preserve opaque text.
            var state=g.Save();PointF[] points={new PointF(x,y),new PointF(x+w,y+h)};
            float scale;
            using(var transform=g.Transform)
            { var m=transform.Elements;scale=Math.Min((float)Math.Sqrt(m[0]*m[0]+m[1]*m[1]),(float)Math.Sqrt(m[2]*m[2]+m[3]*m[3]));transform.TransformPoints(points); }
            g.ResetTransform();float pixels=(float)Math.Round(size*(Appearance==null ? scale : Appearance.FontPercent/100f)*4)/4;
            var box=new RectangleF(points[0].X,points[0].Y,points[1].X-points[0].X,points[1].Y-points[0].Y);
            // Small cards must not turn a full lap time into an ellipsis or
            // let enlarged glyphs overlap a neighbouring column.
            g.SetClip(box,CombineMode.Intersect);
            using(var sf=new StringFormat { Alignment=align,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap })
            {
                var font=Font(Math.Max(6,pixels),bold,mono);
                if(Appearance!=null)
                {
                    float fit=Math.Min(1,Math.Max(1,box.Height-2)/font.GetHeight(g));
                    if(mono || letterSpacingEm>0)
                    {
                        float width=g.MeasureString(text,font,int.MaxValue,StringFormat.GenericTypographic).Width;
                        if(letterSpacingEm>0) width+=Math.Max(0,text.Length-1)*font.Size*letterSpacingEm;
                        fit=Math.Min(fit,Math.Max(1,box.Width-2)/Math.Max(1,width));
                    }
                    if(fit<1) font=Font(Math.Max(4,(float)Math.Floor(font.Size*fit*4)/4),bold,mono);
                }
                var shadow=box;shadow.Offset(Math.Max(1f,scale),Math.Max(1f,scale));
                var shadowBrush=Brush(Color.FromArgb(210,0,0,0));
                if(letterSpacingEm>0) DrawSpaced(g,text,font,shadowBrush,shadow,sf,letterSpacingEm);
                else g.DrawString(text,font,shadowBrush,shadow,sf);
                if(letterSpacingEm>0) DrawSpaced(g,text,font,Brush(c),box,sf,letterSpacingEm);
                else g.DrawString(text,font,Brush(c),box,sf);
            }
            g.Restore(state);
        }
        void Line(Graphics g,Color c,float x1,float y1,float x2,float y2,float width=1)
        { using(var p=new Pen(c,width)) { p.StartCap=p.EndCap=LineCap.Round; g.DrawLine(p,x1,y1,x2,y2); } }
        void Prepare(Graphics g) { g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit; }
        void Shell(Graphics g,int w,int h,int opacity,bool unlocked)
        {
            using(var path=Rounded(.5f,.5f,w-1,h-1,22))
            {
                g.FillPath(Brush(Alpha(Theme.Background,(int)(255*opacity/100.0))),path);
                using(var p=new Pen(unlocked ? Theme.Rpm : Alpha(Theme.Grid,185),unlocked ? 2 : 1)) g.DrawPath(p,path);
            }
            if(unlocked) for(int i=0;i<3;i++) Line(g,Theme.Rpm,w-7-i*5,h-6,w-6,h-7-i*5,2);
        }
        public static int TableHeight(Frame f) { return TableHeight(f,true); }
        public static int TableHeight(Frame f,bool onlyOwn) { var groups=RaceView.Build(f,true);return groups.Count==0 ? 180 : 118+groups.Sum(c=>c.Drivers.Count*StandingsRowHeight); }
        Image Flag(string code)
        {
            if(string.IsNullOrEmpty(code) || flagArchive==null) return null;
            Image img;
            if(flags.TryGetValue(code,out img)) return img;
            var entry=flagArchive.GetEntry(code+".png");
            if(entry!=null) using(var stream=entry.Open()) using(var original=Image.FromStream(stream)) img=new Bitmap(original);
            flags[code]=img; return img;
        }
        void Country(Graphics g,string country,float x,float y)
        {
            var img=Flag(country);
            if(img!=null) { g.DrawImage(img,x,y+3,18,13.5f); return; }
            using(var p=new Pen(Alpha(Theme.Muted,155),1))
            { g.DrawEllipse(p,x+2,y+3,13,13); g.DrawEllipse(p,x+6,y+3,5,13); g.DrawLine(p,x+3,y+9.5f,x+14,y+9.5f); }
        }
        void CarLogo(Graphics g,Driver driver,float x,float y)
        {
            string make=CarIdentity.Make(driver);Image logo=null;
            if(make.Length>0 && brandArchive!=null && !brands.TryGetValue(make,out logo))
            {
                var entry=brandArchive.GetEntry(make+".png");
                if(entry!=null) using(var stream=entry.Open()) using(var original=Image.FromStream(stream)) logo=new Bitmap(original);
                brands[make]=logo;
            }
            if(logo!=null)
            {
                var state=g.Save();PointF[] pts={new PointF(x,y),new PointF(x+22,y+22)};
                using(var m=g.Transform) m.TransformPoints(pts);g.ResetTransform();
                float size=Math.Min(pts[1].X-pts[0].X,pts[1].Y-pts[0].Y);
                float scale=size/Math.Max(logo.Width,logo.Height),w=logo.Width*scale,h=logo.Height*scale;
                g.FillRectangle(Brush(Theme.Background),pts[0].X,pts[0].Y,pts[1].X-pts[0].X,pts[1].Y-pts[0].Y);
                g.DrawImage(logo,pts[0].X+(pts[1].X-pts[0].X-w)/2,pts[0].Y+(pts[1].Y-pts[0].Y-h)/2,w,h);g.Restore(state);
            }
            else
            {
                using(var pen=new Pen(Theme.Muted,1.2f)) { g.DrawRectangle(pen,x+2,y+8,18,8);g.DrawLines(pen,new[]{new PointF(x+5,y+8),new PointF(x+8,y+4),new PointF(x+14,y+4),new PointF(x+18,y+8)}); }
            }
        }
        static Color TyreColor(string code)
        { return code=="S" ? Theme.TyreSoft : code=="M" ? Theme.TyreMedium : code=="H" ? Theme.TyreHard : code=="W" ? Theme.TyreWet : Theme.Muted; }
        void TyreBadge(Graphics g,string code,float x,float y,float size)
        {
            Color color=TyreColor(code);
            float inset=1.3f;
            using(var fill=new SolidBrush(Alpha(color,42))) g.FillEllipse(fill,x+inset,y+inset,size-inset*2,size-inset*2);
            using(var pen=new Pen(color,1.8f)) g.DrawEllipse(pen,x+inset,y+inset,size-inset*2,size-inset*2);
            Text(g,code,x,y,size,size,11.5f,color,true,true,StringAlignment.Center);
        }
        void Tyre(Graphics g,Driver driver,float x,float y)
        {
            string front=TyreCompound.Code(driver.TyreFront),rear=TyreCompound.Code(driver.TyreRear);
            if(front=="?" && rear=="?") { Text(g,"—",x,y,50,26,14,Theme.Muted,true,true,StringAlignment.Center); return; }
            if(front==rear || rear=="?") { TyreBadge(g,front,x+1,y+2,21); return; }
            if(front=="?") { TyreBadge(g,rear,x+1,y+2,21); return; }
            // When the compounds differ, the left badge is front and the right is rear.
            TyreBadge(g,front,x,y+2,21); TyreBadge(g,rear,x+28,y+2,21);
        }
        string DriverName(Graphics g,Driver d)
        {
            string name=d.Name.Length>0 ? d.Name : "Piloto sem nome";
            float width=196,pixels=20;
            if(Appearance!=null) { using(var matrix=g.Transform) width*=Math.Abs(matrix.Elements[0]);pixels*=Appearance.FontPercent/100f; }
            Font font=Font(pixels,true,false);
            if(g.MeasureString(name,font).Width<=width) return name;
            int space=name.IndexOf(' ');
            if(space>0 && space<name.Length-1) { name=name.Substring(0,1)+". "+name.Substring(space+1);if(g.MeasureString(name,font).Width<=width) return name; }
            int[] starts=System.Globalization.StringInfo.ParseCombiningCharacters(name);
            for(int n=starts.Length-1;n>0;n--)
            {
                string shortened=name.Substring(0,starts[n])+"…";
                if(g.MeasureString(shortened,font).Width<=width) return shortened;
            }
            return "…";
        }
        public void Standings(Graphics g,Frame f,ViewOptions o,bool unlocked)
        {
            Prepare(g);int height=TableHeight(f,true);Shell(g,TableWidth,height,o.TableOpacity,unlocked);
            var groups=RaceView.Build(f,true);var best=RaceView.Best(f,true);
            Text(g,f.Track.Length>0 ? f.Track : "LE MANS ULTIMATE",16,5,458,30,21,Theme.Text,true);
            Box(g,Alpha(Theme.Surface,205),480,10,161,23,6);
            Text(g,Format.Session(f.Session).ToUpper(Format.Br),488,10,145,23,11,Theme.Muted,true,false,StringAlignment.Center,.045f);
            Text(g,f.Connected ? Format.Clock(f.Remaining) : "—",660,4,222,32,26,Theme.Text,true,true,StringAlignment.Far);
            Text(g,"MELHOR",16,35,76,23,10,Theme.Muted,true,false,StringAlignment.Near,.075f);
            Text(g,best==null ? "—" : Format.Lap(best.BestLap),92,32,145,28,19,Theme.Text,true,true);
            Text(g,best==null ? "Aguardando a primeira volta" : "P"+best.ClassPosition+"  "+best.Name,245,35,441,23,14,Theme.Muted);
            bool onAir=f.HasTelemetry && !f.Stale;
            Box(g,Alpha(onAir ? Theme.Throttle : Theme.Grid,28),735,37,145,20,6);
            Text(g,unlocked ? "POSICIONAR" : f.Stale ? "PAUSADO" : onAir ? "AO VIVO" : "AGUARDANDO",738,37,139,20,10,onAir ? Theme.Throttle : Theme.Muted,true,false,StringAlignment.Center,.04f);
            bool race=Format.IsRace(f.Session);
            if(groups.Count==0)
            {
                Text(g,"AGUARDANDO SUA CLASSE",16,88,868,33,22,Theme.Text,true);
                Text(g,"Entre na pista para identificar seu carro.",16,125,868,26,16,Theme.Muted);return;
            }
            var group=groups[0];Color cc=Theme.ClassColor(group.Name);
            Box(g,Alpha(cc,31),10,64,880,25,5);Box(g,cc,10,68,3,17,1.5f);
            Text(g,group.Name,22,62,220,27,16,cc,true);
            Text(g,group.Total+" PILOTOS",262,62,224,27,12,Theme.Text);
            Text(g,"SUA CLASSE",680,62,199,27,12,Theme.Text,true,false,StringAlignment.Far);
            string[] headers={"POS","+/−","GER","PILOTO / CARRO","ÚLTIMA","MELHOR","PNEU",race ? "GAP GER." : "Δ SUA VOLTA","PEN.","BOX"};
            float[] x={12,52,94,188,386,490,596,650,760,830};float[] widths={36,38,34,196,98,98,48,104,64,58};
            for(int i=0;i<headers.Length;i++) Text(g,headers[i],x[i],90,widths[i],21,11.5f,Theme.Muted,false,false,i>=4 ? StringAlignment.Far : StringAlignment.Near,.055f);
            float y=112;int previous=0,row=0;
            foreach(var d in group.Drivers)
            {
                if(previous>0 && d.ClassPosition>previous+1) Line(g,Alpha(Theme.Text,135),12,y,888,y);
                if(d.IsPlayer) { Box(g,Alpha(Theme.Player,248),9,y,882,StandingsRowHeight,5);Box(g,Theme.Text,9,y+3,4,StandingsRowHeight-6,2); }
                else if(row%2==0) Box(g,Alpha(Theme.Surface,110),9,y,882,StandingsRowHeight,3);
                Text(g,d.ClassPosition>0 ? d.ClassPosition.ToString() : "—",14,y,34,StandingsRowHeight,22,Theme.Text,true,true);
                string delta=!d.PositionChange.HasValue ? "—" : d.PositionChange.Value>0 ? "▲"+d.PositionChange.Value : d.PositionChange.Value<0 ? "▼"+Math.Abs(d.PositionChange.Value) : "0";
                Color dc=!d.PositionChange.HasValue || d.PositionChange.Value==0 ? Theme.Muted : d.PositionChange.Value>0 ? Theme.Throttle : Theme.Brake;
                Text(g,delta,50,y,42,StandingsRowHeight,16,dc,true);
                Text(g,d.Position>0 ? d.Position.ToString() : "—",94,y,34,StandingsRowHeight,16,Theme.Muted,false,true);
                Country(g,d.Country,134,y+5);CarLogo(g,d,158,y+5);
                Text(g,DriverName(g,d),188,y,196,StandingsRowHeight,20,Theme.Text,true);
                Text(g,Format.Lap(d.LastLap),386,y,98,StandingsRowHeight,18,Theme.Text,true,true,StringAlignment.Far);
                Text(g,Format.Lap(d.BestLap),490,y,98,StandingsRowHeight,18,Theme.Text,true,true,StringAlignment.Far);
                Tyre(g,d,596,y+3);
                Text(g,Format.Gap(f,d),650,y,104,StandingsRowHeight,20,Theme.Text,true,true,StringAlignment.Far);
                Text(g,Format.Penalty(d),760,y,64,StandingsRowHeight,16,d.Penalties>0 ? Theme.Fuel : Theme.Muted,true,true,StringAlignment.Far);
                Text(g,d.InPits ? "BOX" : d.PitStops.ToString(),830,y,58,StandingsRowHeight,17,d.InPits ? Theme.Fuel : Theme.Text,true,true,StringAlignment.Far);
                y+=StandingsRowHeight;previous=d.ClassPosition;row++;
            }
        }
        void Trace(Graphics g,PlotWindow plot,bool brake,float x,float y,float w,float h)
        {
            var data=plot.Samples;
            if(data.Count<2) return;
            var pts=new PointF[data.Count];
            for(int i=0;i<pts.Length;i++) pts[i]=new PointF(x+(float)plot.X(data[i].Time)*w,y+h-(float)(brake ? data[i].Brake : data[i].Throttle)*h);
            Color c=brake ? Theme.Brake : Theme.Throttle;
            using(var p=new Pen(c,2.5f)) { p.LineJoin=LineJoin.Round; p.StartCap=p.EndCap=LineCap.Round; g.DrawLines(p,pts); }
        }
        void Wheel(Graphics g,double angle,float cx=571,float cy=153)
        {
            if(wheelArtwork==null)
            {
                wheelArtwork=new Bitmap(320,320,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using(var art=Graphics.FromImage(wheelArtwork))
                { art.Clear(Color.Transparent);Prepare(art);art.ScaleTransform(2,2);art.TranslateTransform(80-571,80-153);RenderWheelArtwork(art); }
            }
            var state=g.Save();float sx,sy;
            using(var matrix=g.Transform) { var m=matrix.Elements;sx=Math.Abs(m[0]);sy=Math.Abs(m[3]); }
            g.TranslateTransform(cx,cy);float uniform=Math.Min(sx,sy);
            if(sx>0 && sy>0) g.ScaleTransform(uniform/sx,uniform/sy);
            if(Format.Finite(angle)) g.RotateTransform((float)angle);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.DrawImage(wheelArtwork,-80,-80,160,160);g.Restore(state);
        }
        void RenderWheelArtwork(Graphics g)
        {
            var state=g.Save();float sx,sy;
            using(var matrix=g.Transform) { var m=matrix.Elements;sx=Math.Abs(m[0]);sy=Math.Abs(m[3]); }
            g.TranslateTransform(571,153);
            float uniform=Math.Min(sx,sy);if(sx>0 && sy>0) g.ScaleTransform(uniform/sx,uniform/sy);
            g.ScaleTransform(.54f,.54f);
            Color rubber=Color.FromArgb(18,24,29),carbon=Color.FromArgb(38,46,52),edge=Color.FromArgb(90,109,120);
            Color metal=Color.FromArgb(66,82,93),red=Color.FromArgb(225,43,58),dark=Color.FromArgb(9,14,18);
            // Open grips and carbon plate follow the user's wheel; the center has no emblem.
            for(int side=-1;side<=1;side+=2)
            {
                var gripState=g.Save();g.ScaleTransform(side,1);
                using(var grip=new GraphicsPath())
                {
                    grip.AddBezier(60,-61,87,-64,109,-41,108,-11);
                    grip.AddBezier(108,-11,111,20,107,47,99,60);
                    grip.AddBezier(99,60,88,63,73,57,76,42);
                    grip.AddBezier(76,42,79,25,85,6,83,-14);
                    grip.AddBezier(83,-14,82,-32,72,-40,58,-39);
                    grip.CloseFigure();g.FillPath(Brush(rubber),grip);
                    using(var pen=new Pen(edge,1.5f)) g.DrawPath(pen,grip);
                }
                using(var cap=new GraphicsPath())
                {
                    cap.AddBezier(61,-61,80,-62,96,-53,102,-38);
                    cap.AddLine(102,-38,84,-30);cap.AddBezier(84,-30,80,-39,69,-40,58,-39);
                    cap.CloseFigure();g.FillPath(Brush(metal),cap);
                }
                // Small perforations stay quiet at the overlay's default scale.
                for(int row=0;row<8;row++) for(int col=0;col<2;col++)
                    g.FillEllipse(Brush(dark),92+col*6,-22+row*8,2.5f,3);
                Line(g,metal,82,26,56,24,7);Line(g,metal,81,51,51,46,6);
                Line(g,edge,103,-18,104,24,1);
                g.Restore(gripState);
            }
            using(var plate=new GraphicsPath())
            {
                plate.AddPolygon(new PointF[] {new PointF(-58,-61),new PointF(58,-61),new PointF(64,-46),new PointF(53,-24),new PointF(61,18),new PointF(51,57),new PointF(15,61),new PointF(4,65),new PointF(-16,61),new PointF(-53,57),new PointF(-62,20),new PointF(-53,-24),new PointF(-64,-46)});
                g.FillPath(Brush(carbon),plate);
                using(var texture=new HatchBrush(HatchStyle.ForwardDiagonal,Color.FromArgb(45,67,79,86),Color.Transparent)) g.FillPath(texture,plate);
                using(var pen=new Pen(edge,1.5f)) g.DrawPath(pen,plate);
            }
            // Unlit hardware arc, D-pad and four geometric face buttons.
            using(var pen=new Pen(dark,7)) g.DrawArc(pen,-20,-42,40,18,195,150);
            using(var pen=new Pen(metal,2)) g.DrawArc(pen,-19,-43,38,18,198,144);
            g.FillEllipse(Brush(dark),-53,-43,34,34);g.FillEllipse(Brush(metal),-49,-39,26,26);
            Box(g,rubber,-40,-39,8,26,1);Box(g,rubber,-49,-30,26,8,1);
            Box(g,dark,23,-45,29,36,8);
            using(var pen=new Pen(Theme.Muted,1.6f))
            {
                g.DrawPolygon(pen,new PointF[] {new PointF(37,-40),new PointF(33,-34),new PointF(41,-34)});
                g.DrawRectangle(pen,27,-29,6,6);g.DrawEllipse(pen,42,-29,6,6);
                g.DrawLine(pen,34,-21,40,-15);g.DrawLine(pen,40,-21,34,-15);
            }
            for(int side=-1;side<=1;side+=2)
            {
                g.FillEllipse(Brush(edge),side*34-9,-4,18,18);
                g.FillEllipse(Brush(red),side*34-7,-2,14,14);
                g.FillEllipse(Brush(edge),side*45-7,16,14,14);
                g.FillEllipse(Brush(rubber),side*45-5,18,10,10);
                foreach(int y in new int[] {-52,9,48})
                {
                    g.FillEllipse(Brush(dark),side*17-3,y-3,6,6);
                    Line(g,metal,side*17-1,y,side*17+1,y,1);
                }
            }
            g.FillEllipse(Brush(dark),-45,24,29,29);
            Line(g,metal,-44,38,-17,38,1.5f);
            Line(g,Theme.Muted,-33,31,-27,31,1.5f);Line(g,Theme.Muted,-30,28,-30,34,1.5f);
            Line(g,Theme.Muted,-33,44,-27,44,1.5f);
            g.FillEllipse(Brush(dark),8,19,37,37);g.FillEllipse(Brush(red),10,21,33,33);
            g.FillEllipse(Brush(rubber),16,27,21,21);Line(g,Theme.Muted,25,35,28,35,2);
            Box(g,dark,-10,33,13,26,2);Line(g,metal,-7,40,0,40,2);Line(g,metal,-7,49,0,49,2);
            g.Restore(state);
        }
        public void Dispose()
        {
            if(wheelArtwork!=null) wheelArtwork.Dispose();
            foreach(var f in fonts.Values) f.Dispose(); foreach(var b in brushes.Values) b.Dispose();
            foreach(var f in flags.Values) if(f!=null) f.Dispose();
            if(flagArchive!=null) flagArchive.Dispose(); if(flagStream!=null) flagStream.Dispose();
            foreach(var b in brands.Values) if(b!=null) b.Dispose();
            if(brandArchive!=null) brandArchive.Dispose();if(brandStream!=null) brandStream.Dispose();
        }
    }
}
