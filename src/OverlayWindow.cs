using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arthur.LmuOverlay
{
    static class Win32
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; public Point(int x,int y) { X=x;Y=y; } }
        [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width,Height; public Size(int w,int h) { Width=w;Height=h; } }
        [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct Blend { public byte Op,Flags,Alpha,Format; }
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h,int index,int value);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint processId);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr h,uint cmd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr h,int command);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
        internal static readonly uint OpenMessage=RegisterWindowMessage("ArthurLMUOverlay.OpenPanel");
        [DllImport("gdi32.dll",SetLastError=true)] internal static extern IntPtr CreateCompatibleDC(IntPtr h);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr h);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr item);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] internal static extern bool GdiFlush();
        [StructLayout(LayoutKind.Sequential)] internal struct BitmapHeader
        {
            public uint Size;public int Width,Height;public ushort Planes,BitCount;
            public uint Compression,ImageSize;public int XPels,YPels;public uint Used,Important;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo { public BitmapHeader Header;public uint Color; }
        [DllImport("gdi32.dll",SetLastError=true)] internal static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr h,IntPtr screen,ref Point location,ref Size size,IntPtr source,ref Point origin,int key,ref Blend blend,uint flags);
    }
    // Keep one surface per panel: no bitmap/DC allocation on every frame.
    sealed class LayeredSurface : IDisposable
    {
        IntPtr dc,dib,previous;
        public Bitmap Bitmap { get; private set; }
        public LayeredSurface(int w,int h)
        {
            try
            {
                dc=Win32.CreateCompatibleDC(IntPtr.Zero);if(dc==IntPtr.Zero) throw new Win32Exception();
                var info=new Win32.BitmapInfo { Header=new Win32.BitmapHeader { Size=40,Width=w,Height=-h,Planes=1,BitCount=32 } };
                IntPtr bits;dib=Win32.CreateDIBSection(dc,ref info,0,out bits,IntPtr.Zero,0);
                if(dib==IntPtr.Zero || bits==IntPtr.Zero) throw new Win32Exception();
                previous=Win32.SelectObject(dc,dib);
                if(previous==IntPtr.Zero || previous==new IntPtr(-1)) throw new Win32Exception();
                Bitmap=new Bitmap(w,h,w*4,PixelFormat.Format32bppPArgb,bits);
            }
            catch { Dispose();throw; }
        }
        public void Present(IntPtr window,System.Drawing.Point location)
        {
            var dest=new Win32.Point(location.X,location.Y);var size=new Win32.Size(Bitmap.Width,Bitmap.Height);var origin=new Win32.Point(0,0);
            var blend=new Win32.Blend { Op=0,Flags=0,Alpha=255,Format=1 };
            Win32.GdiFlush();
            if(!Win32.UpdateLayeredWindow(window,IntPtr.Zero,ref dest,ref size,dc,ref origin,0,ref blend,2)) throw new Win32Exception();
        }
        public void Dispose()
        {
            if(Bitmap!=null) { Bitmap.Dispose();Bitmap=null; }
            if(previous!=IntPtr.Zero && previous!=new IntPtr(-1)) Win32.SelectObject(dc,previous);
            if(dib!=IntPtr.Zero) Win32.DeleteObject(dib);if(dc!=IntPtr.Zero) Win32.DeleteDC(dc);
            previous=dib=dc=IntPtr.Zero;
        }
    }
    public sealed class OverlayWindow : Form
    {
        public readonly PanelKind Kind;
        bool pedals { get { return Kind==PanelKind.Pedals; } }
        readonly Painter painter=new Painter();
        readonly Controller controller;
        bool locked=true,dragging,visibilityRequested;
        int resizeEdges;
        Point dragMouse,dragLocation;
        System.Drawing.Size dragSize;
        double renderScaleX=1,renderScaleY=1;
        LayeredSurface surface;
        readonly PresentationRetry retry=new PresentationRetry();
        bool drawing;
        public string RenderStatus="";
        public double ScaleValue=1;
        public int PreferredWidth,PreferredHeight;
        public PanelAppearance Appearance=new PanelAppearance();
        public Size DesignSize
        {
            get { return pedals ? new Size(Painter.PedalsWidth,Painter.PedalsHeight) : Kind==PanelKind.Standings ?
                new Size(Painter.TableWidth,Painter.TableHeight(controller.Current,true)) : Painter.WidgetSize(Kind); }
        }
        public event Action PositionCommitted;
        public bool Locked
        {
            get { return locked; }
            set { locked=value; Cursor=value ? Cursors.Default : Cursors.SizeAll; if(IsHandleCreated) ApplyStyles(); DrawFrame(); }
        }
        public OverlayWindow(Controller owner,PanelKind kind)
        {
            controller=owner;Kind=kind;string[] names={"Pedais e motor","Classificação","Lap timer","Pneus","Combustível","Mapa","Delta central"};Text=AppInfo.Name+" • "+names[(int)kind];
            AccessibleName=Text; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false;
            TopMost=true; StartPosition=FormStartPosition.Manual; AutoScaleMode=AutoScaleMode.None; KeyPreview=true;
        }
        protected override bool ShowWithoutActivation { get { return locked; } }
        protected override CreateParams CreateParams
        {
            get { var cp=base.CreateParams; cp.ExStyle|=0x80|0x80000; if(locked) cp.ExStyle|=0x20|0x08000000; return cp; }
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyStyles(); }
        public void ApplyStyles()
        {
            int s=Win32.GetWindowLong(Handle,-20)|0x80|0x80000;
            Win32.SetWindowLong(Handle,-20,locked ? s|0x20|0x08000000 : s&~(0x20|0x08000000));
            Win32.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x33);
        }
        public void FitSize()
        {
            var design=DesignSize;int w=design.Width,h=design.Height;var minimum=PanelSizing.Minimum(design);
            var area=Screen.FromPoint(Location).WorkingArea;
            var desired=PanelSizing.Fit(PreferredWidth>0 ? PreferredWidth : (int)(w*ScaleValue),PreferredHeight>0 ? PreferredHeight : (int)(h*ScaleValue),
                minimum.Width,minimum.Height,area.Width,area.Height);
            if(ClientSize!=desired) ClientSize=desired;
            renderScaleX=ClientSize.Width/(double)w;renderScaleY=ClientSize.Height/(double)h;
            KeepOnScreen();
        }
        public void SetDimensions(Size size)
        { PreferredWidth=size.Width;PreferredHeight=size.Height;FitSize();Commit(); }
        public void KeepOnScreen()
        {
            var r=Screen.FromPoint(Location).WorkingArea;
            Location=new Point(Math.Max(r.Left,Math.Min(r.Right-Width,Left)),Math.Max(r.Top,Math.Min(r.Bottom-Height,Top)));
        }
        public void ResetSurface()
        {
            if(surface!=null) { surface.Dispose();surface=null; }
            if(IsHandleCreated)
            {
                int style=Win32.GetWindowLong(Handle,-20);
                Win32.SetWindowLong(Handle,-20,style&~0x80000);
                Win32.SetWindowLong(Handle,-20,style|0x80000);
            }
        }
        public void SetRequestedVisibility(bool show)
        {
            visibilityRequested=show;
            if(IsDisposed) return;
            if(!show) { if(Visible) Hide();return; }
            // Never reopen a card over a shell popup or a key-capture dialog.
            if(controller.WindowInteractionPaused) return;
            if(!Visible) { Show();DrawFrame(); }
        }
        public void EnsureVisible()
        {
            if(IsDisposed || !visibilityRequested || controller.WindowInteractionPaused) return;
            if(!Visible) Show();
            if(Win32.IsIconic(Handle)) Win32.ShowWindow(Handle,4);
            Win32.SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x213); // no activation, owner reorder, move or size
        }
        public void DrawFrame()
        {
            if(drawing || controller.WindowInteractionPaused || !Visible || !IsHandleCreated || IsDisposed || !retry.Ready(controller.Now)) return;
            drawing=true;
            try
            {
                if(surface==null || surface.Bitmap.Width!=Width || surface.Bitmap.Height!=Height)
                { if(surface!=null) surface.Dispose();surface=null;surface=new LayeredSurface(Width,Height); }
                Win32.GdiFlush();
                using(var g=Graphics.FromImage(surface.Bitmap))
                {
                    g.Clear(Color.Transparent);g.ScaleTransform((float)renderScaleX,(float)renderScaleY);
                    painter.Appearance=Appearance;
                    if(pedals) painter.Pedals(g,controller.Display,controller.History,controller.Now,controller.Options,!locked);
                    else if(Kind==PanelKind.Standings) painter.Standings(g,controller.Current,controller.Options,!locked);
                    else painter.Widget(g,Kind,Kind==PanelKind.Delta ? controller.Display : controller.Current,controller.Options,!locked);
                    if(!locked)
                    {
                        g.ResetTransform();
                        using(var brush=new SolidBrush(Theme.Rpm))
                        {
                            foreach(int x in new[]{6,(Width-8)/2,Width-14})
                                foreach(int y in new[]{6,(Height-8)/2,Height-14})
                                    if(x!=(Width-8)/2 || y!=(Height-8)/2) g.FillRectangle(brush,x,y,8,8);
                        }
                    }
                    g.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
                }
                surface.Present(Handle,Location);retry.Succeeded();RenderStatus="";
            }
            catch(Exception ex)
            {
                if(!(ex is ExternalException) && !(ex is ArgumentException)) throw;
                retry.Failed(controller.Now);RenderStatus="Reajustando "+(pedals ? "pedais" : "tabela")+" à janela do jogo";
                controller.ReportRecoverable(RenderStatus,ex);ResetSurface();
            }
            finally { drawing=false; }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { DrawFrame(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if(!locked && e.Button==MouseButtons.Left)
            { Activate(); dragging=true;resizeEdges=PanelSizing.HitEdges(e.Location,ClientSize);dragMouse=MousePosition;dragLocation=Location;dragSize=ClientSize;Capture=true; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(locked) return;
            int edges=dragging ? resizeEdges : PanelSizing.HitEdges(e.Location,ClientSize);
            bool horizontal=(edges&3)!=0,vertical=(edges&12)!=0;
            Cursor=horizontal && vertical ? (edges==(PanelSizing.Left|PanelSizing.Top) || edges==(PanelSizing.Right|PanelSizing.Bottom) ? Cursors.SizeNWSE : Cursors.SizeNESW) : horizontal ? Cursors.SizeWE : vertical ? Cursors.SizeNS : Cursors.SizeAll;
            if(dragging)
            {
                if(resizeEdges!=0)
                {
                    var rect=PanelSizing.Resize(new Rectangle(dragLocation,dragSize),new Point(MousePosition.X-dragMouse.X,MousePosition.Y-dragMouse.Y),
                        resizeEdges,PanelSizing.Minimum(DesignSize),Screen.FromPoint(dragLocation).WorkingArea);
                    PreferredWidth=rect.Width;PreferredHeight=rect.Height;Location=rect.Location;FitSize();
                }
                else Location=new Point(dragLocation.X+MousePosition.X-dragMouse.X,dragLocation.Y+MousePosition.Y-dragMouse.Y);
                DrawFrame();
            }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e); if(dragging) { dragging=false; Capture=false; Commit(); }
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        { base.OnMouseCaptureChanged(e);if(dragging && !Capture) { dragging=false;Commit(); } }
        void Commit() { KeepOnScreen();if(PreferredWidth>0) PreferredWidth=Width;if(PreferredHeight>0) PreferredHeight=Height;if(PositionCommitted!=null) PositionCommitted(); DrawFrame(); }
        protected override void OnMouseWheel(MouseEventArgs e)
        { base.OnMouseWheel(e);if(!locked) { double ratio=e.Delta>0 ? 1.05 : 1/1.05;PreferredWidth=(int)(Width*ratio);PreferredHeight=(int)(Height*ratio);FitSize();Commit(); } }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(!locked)
            {
                Keys key=keyData&Keys.KeyCode;
                if(key==Keys.Escape) { controller.LockPanels(); return true; }
                if(key==Keys.Left || key==Keys.Right || key==Keys.Up || key==Keys.Down)
                {
                    int direction=key==Keys.Left || key==Keys.Up ? -1 : 1;
                    if((keyData&Keys.Shift)!=0)
                    { PreferredWidth=Width+(key==Keys.Left || key==Keys.Right ? direction*8 : 0);PreferredHeight=Height+(key==Keys.Up || key==Keys.Down ? direction*8 : 0);FitSize(); }
                    else Location=new Point(Left+(key==Keys.Left || key==Keys.Right ? direction : 0),Top+(key==Keys.Up || key==Keys.Down ? direction : 0));
                    Commit(); return true;
                }
            }
            return base.ProcessCmdKey(ref msg,keyData);
        }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x7E || m.Msg==0x31E || m.Msg==0x2E0) controller.RequestDisplayRefresh();
            if(locked && m.Msg==0x21) { m.Result=new IntPtr(3); return; }
            if(locked && m.Msg==0x84) { m.Result=new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing) { if(disposing) { painter.Dispose(); if(surface!=null) surface.Dispose(); } base.Dispose(disposing); }
    }
}
