using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Arthur.LmuOverlay
{
    // Acquisition is independent of the Windows message loop and never overlaps itself.
    public sealed class TelemetryPump : IDisposable
    {
        readonly ITelemetryReader reader;
        readonly Func<double> now;
        readonly Action<string,Exception> error;
        readonly InputCapture capture=new InputCapture();
        readonly RaceWidgets widgets=new RaceWidgets();
        System.Threading.Timer timer;
        Frame latest=new Frame();
        int busy,stopping,trainingReset;
        public DeltaTracker Delta;
        public void BreakTraining() { Interlocked.Exchange(ref trainingReset,1); }
        double retryAt;
        public TelemetryPump(Func<double> clock,Action<string,Exception> log) : this(clock,log,new NativeReader(log)) { }
        internal TelemetryPump(Func<double> clock,Action<string,Exception> log,ITelemetryReader source)
        { now=clock;error=log;reader=source; }
        public InputHistory History { get { return capture.History; } }
        public Action<Frame> LearningSample;
        public Frame Latest { get { return Interlocked.CompareExchange(ref latest,null,null); } }
        public void Start() { if(timer==null) timer=new System.Threading.Timer(Poll,null,0,8); }
        void Poll(object state)
        {
            if(stopping!=0 || Interlocked.CompareExchange(ref busy,1,0)!=0) return;
            try
            {
                double time=now();if(time<retryAt) return;
                Frame f=reader.Read(false);if(f==null) return;
                f.TrainingReset=Interlocked.Exchange(ref trainingReset,0)!=0;capture.Accept(f,time);widgets.Apply(f);if(Delta!=null)Delta.Apply(f);f.ReadAt=time;Interlocked.Exchange(ref latest,f);
                var learning=LearningSample;if(learning!=null) learning(f);
            }
            catch(Exception ex)
            {
                if(!(ex is InvalidDataException) && !(ex is IOException) && !(ex is InvalidOperationException) && !(ex is ArgumentException) && !(ex is Win32Exception)) throw;
                reader.Dispose();retryAt=now()+1;
                var f=new Frame { Status="Reconectando ao LMU",ReadAt=now() };capture.Accept(f,f.ReadAt);Interlocked.Exchange(ref latest,f);
                var learning=LearningSample;if(learning!=null) learning(f);
                if(error!=null) error("Leitura: reconexão automática",ex);
            }
            finally { if(stopping!=0) reader.Dispose();Interlocked.Exchange(ref busy,0); }
        }
        public void Dispose()
        {
            Interlocked.Exchange(ref stopping,1);if(timer!=null) timer.Dispose();
            if(Interlocked.CompareExchange(ref busy,1,0)==0) { reader.Dispose();Interlocked.Exchange(ref busy,0); }
        }
    }
}
