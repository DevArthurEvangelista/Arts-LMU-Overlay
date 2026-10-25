using System;
using System.Collections.Generic;
using System.Threading;

namespace Arthur.LmuOverlay
{
    public interface IWheelReader : IDisposable
    {
        List<WheelAxis> Read(IntPtr owner,double now);
        void Refresh();
    }
    public sealed class WheelSnapshot
    {
        public readonly List<WheelAxis> Axes;
        public readonly double ReadAt;
        public List<WheelButton> Buttons=new List<WheelButton>();
        public WheelSnapshot(List<WheelAxis> axes,double time) { Axes=axes;ReadAt=time; }
    }
    // DirectInput creation, polling, enumeration and disposal share one background
    // thread. No driver operation or lock can delay the Windows paint/message loop.
    public sealed class WheelPump : IDisposable
    {
        readonly Func<double> clock;
        readonly Func<IWheelReader> create;
        readonly Action<string,Exception> log;
        readonly AutoResetEvent wake=new AutoResetEvent(false);
        readonly List<WheelAxis> empty=new List<WheelAxis>();
        Thread worker;
        WheelSnapshot latest=new WheelSnapshot(new List<WheelAxis>(),-100);
        int stopping,refresh;
        IntPtr owner;
        public WheelPump(Func<double> now,Action<string,Exception> error) : this(now,()=>new WheelInput(error),error) { }
        internal WheelPump(Func<double> now,Func<IWheelReader> factory,Action<string,Exception> error)
        { clock=now;create=factory;log=error; }
        public void Start(IntPtr window)
        {
            if(worker!=null) return;owner=window;
            worker=new Thread(Run) { IsBackground=true,Name="Arts volante" };
            if(Environment.OSVersion.Platform==PlatformID.Win32NT) worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }
        public List<WheelAxis> Read(double now)
        {
            var snapshot=Interlocked.CompareExchange(ref latest,null,null);
            return now-snapshot.ReadAt<=.3 ? snapshot.Axes : empty;
        }
        public List<WheelButton> ReadButtons(double now)
        { var snapshot=Interlocked.CompareExchange(ref latest,null,null);return now-snapshot.ReadAt<=.3?snapshot.Buttons:new List<WheelButton>(); }
        public void Refresh() { Interlocked.Exchange(ref refresh,1);try { wake.Set(); } catch(ObjectDisposedException) { } }
        void Run()
        {
            try
            {
                using(var reader=create()) while(Interlocked.CompareExchange(ref stopping,0,0)==0)
                {
                    if(Interlocked.Exchange(ref refresh,0)!=0) reader.Refresh();
                    var axes=reader.Read(owner,clock());
                    Interlocked.Exchange(ref latest,new WheelSnapshot(axes,clock()){Buttons=reader is IWheelButtonReader?((IWheelButtonReader)reader).Buttons:new List<WheelButton>()});
                    wake.WaitOne(8);
                }
            }
            catch(Exception ex)
            {
                if(ex is OutOfMemoryException) throw;
                if(log!=null) log("Leitura do volante interrompida; direção do jogo disponível",ex);
            }
            finally { Interlocked.Exchange(ref latest,new WheelSnapshot(empty,-100));wake.Dispose(); }
        }
        public void Dispose()
        {
            Interlocked.Exchange(ref stopping,1);
            // Never join a potentially blocked USB driver from the UI thread.
            try { wake.Set(); } catch(ObjectDisposedException) { }
            if(worker==null) wake.Dispose();
        }
    }
}
