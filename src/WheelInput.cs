using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Arthur.LmuOverlay
{
    public sealed class WheelAxis
    {
        public string DeviceId,DeviceName,Name;
        public uint Id;
        public double Value,Minimum,Maximum;
        public bool Raw;
    }
    [DataContract]
    public sealed class WheelCalibration
    {
        [DataMember] public string DeviceId,DeviceName;
        [DataMember] public uint AxisId;
        [DataMember] public double Center,UnitsPerDegree,Minimum,Maximum,GameRange;
        [DataMember] public bool Raw;
        public bool Valid { get { return !string.IsNullOrEmpty(DeviceId) && Format.Finite(Center) && Format.Finite(Minimum) &&
            Format.Finite(Maximum) && Maximum>Minimum && Format.Finite(UnitsPerDegree) && Math.Abs(UnitsPerDegree)>.001; } }
        public double Angle(WheelAxis axis)
        { return Angle(axis,0); }
        public double Angle(WheelAxis axis,double configuredRotation)
        {
            if(!Valid || axis==null || axis.DeviceId!=DeviceId || axis.Id!=AxisId || axis.Raw!=Raw ||
                !Format.Finite(axis.Value) || !Format.Finite(axis.Minimum) || !Format.Finite(axis.Maximum) || axis.Maximum<=axis.Minimum) return double.NaN;
            // Device drivers can refresh their raw range after a game mode or
            // wheel-lock change. The persisted device/eixo remains authoritative;
            // do not freeze the wheel merely because those integer bounds moved.
            double raw=axis.Value-Center;
            if(configuredRotation>0)
            {
                double half=Math.Max(Math.Abs(Center-Minimum),Math.Abs(Maximum-Center));
                if(half>1) return Format.Clamp(raw/half,-1,1)*Math.Sign(UnitsPerDegree)*configuredRotation/2;
            }
            return raw/UnitsPerDegree;
        }
        public static WheelCalibration Create(WheelAxis center,WheelAxis right90,WheelAxis right180,double gameRange)
        {
            if(center==null || right90==null || right180==null || center.DeviceId!=right90.DeviceId || center.DeviceId!=right180.DeviceId ||
                center.Id!=right90.Id || center.Id!=right180.Id || center.Raw!=right90.Raw || center.Raw!=right180.Raw ||
                center.Minimum!=right90.Minimum || center.Minimum!=right180.Minimum || center.Maximum!=right90.Maximum || center.Maximum!=right180.Maximum)
                throw new InvalidOperationException("O dispositivo mudou. Recomece a calibração.");
            double d90=right90.Value-center.Value,d180=right180.Value-center.Value;
            double span=Math.Abs(center.Maximum-center.Minimum);
            if(!Format.Finite(span) || center.Maximum<=center.Minimum || !Format.Finite(d90) || !Format.Finite(d180) || Math.Abs(d90)<Math.Max(2,span*.01))
                throw new InvalidOperationException("Não foi detectado giro suficiente. Confirme o volante e capture novamente.");
            if(Math.Sign(d90)!=Math.Sign(d180) || Math.Abs(d180/d90-2)>.18)
                throw new InvalidOperationException("A leitura de 180° não corresponde ao dobro de 90°. Confira as posições e a amplitude no driver; recomece a calibração.");
            return new WheelCalibration { DeviceId=center.DeviceId,DeviceName=center.DeviceName,AxisId=center.Id,Center=center.Value,
                UnitsPerDegree=d180/180,Minimum=center.Minimum,Maximum=center.Maximum,Raw=center.Raw,GameRange=gameRange };
        }
    }
    public static class PhysicalWheelState
    {
        public static void Apply(Frame frame,bool enabled,WheelCalibration calibration,List<WheelAxis> axes)
        { Apply(frame,enabled,calibration,axes,0,0); }
        public static void Apply(Frame frame,bool enabled,WheelCalibration calibration,List<WheelAxis> axes,double wheelMaximumRotation,double carMaximumRotation)
        {
            frame.PhysicalWheel=enabled;frame.PhysicalWheelFallback=false;frame.PhysicalWheelAngle=double.NaN;frame.WheelStatus=enabled ? "CALIBRAR" : "DESATIVADO";
            if(!enabled) return;
            if(calibration==null || !calibration.Valid) { Fallback(frame,carMaximumRotation,"CALIBRAR");return; }
            var axis=axes==null ? null : axes.FirstOrDefault(x=>x.DeviceId==calibration.DeviceId && x.Id==calibration.AxisId);
            if(axis==null) { Fallback(frame,carMaximumRotation,"SEM VOLANTE");return; }
            frame.PhysicalWheelAngle=calibration.Angle(axis,wheelMaximumRotation);
            if(Format.Finite(frame.PhysicalWheelAngle)) { frame.WheelStatus="CALIBRADO";return; }
            Fallback(frame,carMaximumRotation,"RECALIBRAR");
        }
        static void Fallback(Frame frame,double configuredRange,string reason)
        {
            if(frame.HasTelemetry && !frame.Stale && Format.Finite(frame.Steering))
            {
                double range=configuredRange>0 ? configuredRange : Instruments.SteeringRange(frame,0);
                if(range>0)
                {
                    frame.PhysicalWheel=false;frame.PhysicalWheelFallback=true;
                    frame.PhysicalWheelAngle=Format.Clamp(frame.Steering,-1,1)*range/2;
                    frame.WheelStatus=reason+" • MODO JOGO";return;
                }
            }
            frame.PhysicalWheel=false;frame.PhysicalWheelFallback=false;frame.PhysicalWheelAngle=double.NaN;frame.WheelStatus=reason;
        }
    }
    // DirectInput is read in background/nonexclusive mode. No effects, autocenter or driver settings are changed.
    public sealed class WheelInput : IWheelReader, IWheelButtonReader
    {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        internal struct Instance
        {
            public uint Size;public Guid Id,Product;public uint Type;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string ProductName;
            public Guid ForceDriver;public ushort UsagePage,Usage;
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        internal struct AxisObject
        {
            public uint Size;public Guid Guid;public uint Offset,Type,Flags;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Name;
            public uint MaxForce,ForceResolution;public ushort Collection,Designator,UsagePage,Usage;
            public uint Dimension;public ushort Exponent,Report;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct ObjectFormat { public IntPtr Guid;public uint Offset,Type,Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct DataFormat { public uint Size,ObjectSize,Flags,DataSize,Count;public IntPtr Objects; }
        [StructLayout(LayoutKind.Sequential)] internal struct Property { public uint Size,HeaderSize,Object,How;public int Value; }
        [StructLayout(LayoutKind.Sequential)] internal struct Range { public uint Size,HeaderSize,Object,How;public int Minimum,Maximum; }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumCallback(IntPtr info,IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumDevices(IntPtr self,uint type,EnumCallback callback,IntPtr context,uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateDevice(IntPtr self,ref Guid id,out IntPtr device,IntPtr outer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumObjects(IntPtr self,EnumCallback callback,IntPtr context,uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int FormatDevice(IntPtr self,ref DataFormat format);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Cooperative(IntPtr self,IntPtr window,uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetProperty(IntPtr self,IntPtr id,ref Property property);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetRange(IntPtr self,IntPtr id,ref Range range);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Simple(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ReadState(IntPtr self,uint size,IntPtr state);
        [DllImport("dinput8.dll",CallingConvention=CallingConvention.StdCall)] static extern int DirectInput8Create(IntPtr instance,uint version,ref Guid iid,out IntPtr input,IntPtr outer);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        static T Method<T>(IntPtr self,int slot) where T:class
        { return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self),slot*IntPtr.Size),typeof(T)); }
        sealed class Device : IDisposable
        {
            public IntPtr Handle,State;
            public string Id,Name;
            public int ReadFailures,StateSize;
            public readonly List<AxisObject> Buttons=new List<AxisObject>();
            public readonly List<AxisObject> Axes=new List<AxisObject>();
            public readonly List<Range> Ranges=new List<Range>();
            public readonly List<bool> Raw=new List<bool>();
            public ReadState Read;public Simple Poll,Acquire;
            public void Dispose()
            {
                if(Handle!=IntPtr.Zero) { Method<Simple>(Handle,8)(Handle);Marshal.Release(Handle);Handle=IntPtr.Zero; }
                if(State!=IntPtr.Zero) { Marshal.FreeHGlobal(State);State=IntPtr.Zero; }
            }
        }
        readonly List<Device> devices=new List<Device>();
        readonly Action<string,Exception> log;
        IntPtr input,window;
        double nextScan;
        List<WheelButton> buttons=new List<WheelButton>();
        public List<WheelButton> Buttons { get { return buttons; } }
        public string Status="Conecte o volante e clique em Calibrar volante.";
        public WheelInput(Action<string,Exception> logger) { log=logger; }
        public List<WheelAxis> Read(IntPtr owner,double now)
        {
            var result=new List<WheelAxis>();buttons=new List<WheelButton>();
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT) return result;
            try
            {
                window=owner;
                if(now>=nextScan) { Scan();nextScan=devices.Count==0 ? now+5 : double.PositiveInfinity; }
                foreach(var d in devices)
                {
                    int hr=d.Poll(d.Handle);
                    if(hr<0) d.Acquire(d.Handle);
                    if(d.Read(d.Handle,(uint)d.StateSize,d.State)<0) { d.ReadFailures++;d.Acquire(d.Handle);if(d.ReadFailures>60) nextScan=Math.Min(nextScan,now+1);continue; }
                    d.ReadFailures=0;
                    // DIDFT_ABSAXIS is a type flag shared by every absolute axis; it is not an axis identity.
                    // The DirectInput enumeration order is stable for a device, so persist this ordinal instead.
                    for(int i=0;i<d.Axes.Count;i++) result.Add(new WheelAxis { DeviceId=d.Id,DeviceName=d.Name,Id=(uint)i,
                        Name=d.Axes[i].Name,Value=Marshal.ReadInt32(d.State,i*4),Minimum=d.Ranges[i].Minimum,Maximum=d.Ranges[i].Maximum,Raw=d.Raw[i] });
                    for(int i=0;i<d.Buttons.Count;i++) buttons.Add(new WheelButton { DeviceId=d.Id,DeviceName=d.Name,
                        ObjectId=d.Buttons[i].Type,Number=(int)((d.Buttons[i].Type>>8)&65535)+1,
                        Pressed=(Marshal.ReadByte(d.State,d.Axes.Count*4+i)&128)!=0 });
                }
                Status=result.Count==0 ? "Volante não disponível. Confira a conexão e o driver." : "Leitura direta do volante disponível.";
            }
            catch(Exception ex)
            {
                if(!(ex is ExternalException) && !(ex is DllNotFoundException) && !(ex is EntryPointNotFoundException) && !(ex is ArgumentException) && !(ex is InvalidOperationException)) throw;
                buttons.Clear();Status="Não foi possível ler o volante. Confira o driver e tente novamente.";
                if(log!=null) log("Leitura direta do volante indisponível",ex);Dispose();nextScan=now+3;
            }
            return result;
        }
        public void Refresh() { nextScan=0; }
        void Scan()
        {
            if(input==IntPtr.Zero)
            {
                Guid iid=new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
                Marshal.ThrowExceptionForHR(DirectInput8Create(GetModuleHandle(null),0x800,ref iid,out input,IntPtr.Zero));
            }
            var found=new List<Instance>();
            EnumCallback callback=(p,c)=>{ if(found.Count<32) found.Add((Instance)Marshal.PtrToStructure(p,typeof(Instance)));return found.Count<32 ? 1 : 0; };
            Marshal.ThrowExceptionForHR(Method<EnumDevices>(input,4)(input,4,callback,IntPtr.Zero,1));
            GC.KeepAlive(callback);
            for(int i=devices.Count-1;i>=0;i--) if(devices[i].ReadFailures>60 || !found.Any(x=>x.Id.ToString()==devices[i].Id)) { devices[i].Dispose();devices.RemoveAt(i); }
            foreach(var d in devices) for(int i=0;i<d.Ranges.Count;i++)
            {
                var range=d.Ranges[i];
                if(Method<GetRange>(d.Handle,5)(d.Handle,new IntPtr(4),ref range)>=0) d.Ranges[i]=range;
            }
            foreach(var item in found) if(!devices.Any(x=>x.Id==item.Id.ToString()))
            {
                Device device=null;
                try { device=Open(item);if(device!=null) devices.Add(device); }
                catch(ExternalException ex) { if(device!=null) device.Dispose();if(log!=null) log("Dispositivo de jogo não oferece eixos compatíveis",ex); }
            }
        }
        Device Open(Instance instance)
        {
            var d=new Device { Id=instance.Id.ToString(),Name=instance.Name };
            IntPtr objects=IntPtr.Zero;var guids=new List<IntPtr>();
            try
            {
                Guid id=instance.Id;Marshal.ThrowExceptionForHR(Method<CreateDevice>(input,3)(input,ref id,out d.Handle,IntPtr.Zero));
                EnumCallback cb=(p,c)=>{ var axis=(AxisObject)Marshal.PtrToStructure(p,typeof(AxisObject));if((axis.Type&3)==2 && d.Axes.Count<32) d.Axes.Add(axis);return 1; };
                Marshal.ThrowExceptionForHR(Method<EnumObjects>(d.Handle,4)(d.Handle,cb,IntPtr.Zero,3));GC.KeepAlive(cb);
                EnumCallback buttonCallback=(p,c)=>{var item=(AxisObject)Marshal.PtrToStructure(p,typeof(AxisObject));if((item.Type&12)!=0&&d.Buttons.Count<128)d.Buttons.Add(item);return 1;};
                Marshal.ThrowExceptionForHR(Method<EnumObjects>(d.Handle,4)(d.Handle,buttonCallback,IntPtr.Zero,12));GC.KeepAlive(buttonCallback);
                int count=d.Axes.Count+d.Buttons.Count;
                if(count==0) { d.Dispose();return null; }
                d.StateSize=(d.Axes.Count*4+d.Buttons.Count+3)&~3;
                int objectSize=Marshal.SizeOf(typeof(ObjectFormat));objects=Marshal.AllocHGlobal(objectSize*count);
                for(int i=0;i<count;i++)
                {
                    var item=i<d.Axes.Count?d.Axes[i]:d.Buttons[i-d.Axes.Count];
                    IntPtr guid=Marshal.AllocHGlobal(16);guids.Add(guid);Marshal.StructureToPtr(item.Guid,guid,false);
                    var obj=new ObjectFormat { Guid=guid,Offset=(uint)(i<d.Axes.Count?i*4:d.Axes.Count*4+i-d.Axes.Count),Type=item.Type,Flags=0 };
                    Marshal.StructureToPtr(obj,new IntPtr(objects.ToInt64()+i*objectSize),false);
                }
                var format=new DataFormat { Size=(uint)Marshal.SizeOf(typeof(DataFormat)),ObjectSize=(uint)objectSize,Flags=1,DataSize=(uint)d.StateSize,Count=(uint)count,Objects=objects };
                Marshal.ThrowExceptionForHR(Method<FormatDevice>(d.Handle,11)(d.Handle,ref format));
                Marshal.ThrowExceptionForHR(Method<Cooperative>(d.Handle,13)(d.Handle,window,0xA));
                for(int i=0;i<d.Axes.Count;i++)
                {
                    var range=new Range { Size=24,HeaderSize=16,Object=(uint)(i*4),How=1,Minimum=0,Maximum=65535 };
                    Method<GetRange>(d.Handle,5)(d.Handle,new IntPtr(4),ref range);d.Ranges.Add(range);
                    var raw=new Property { Size=20,HeaderSize=16,Object=(uint)(i*4),How=1,Value=1 };
                    d.Raw.Add(Method<SetProperty>(d.Handle,6)(d.Handle,new IntPtr(10),ref raw)>=0);
                    // Zero dead zone in this reader only, when raw mode is unavailable.
                    if(!d.Raw[i]) { raw.Value=0;Method<SetProperty>(d.Handle,6)(d.Handle,new IntPtr(5),ref raw); }
                }
                d.State=Marshal.AllocHGlobal(d.StateSize);
                d.Read=Method<ReadState>(d.Handle,9);d.Poll=Method<Simple>(d.Handle,25);d.Acquire=Method<Simple>(d.Handle,7);
                d.Acquire(d.Handle);return d;
            }
            catch { d.Dispose();throw; }
            finally { foreach(var p in guids) Marshal.FreeHGlobal(p);if(objects!=IntPtr.Zero) Marshal.FreeHGlobal(objects); }
        }
        public void Dispose() { foreach(var d in devices) d.Dispose();devices.Clear();if(input!=IntPtr.Zero) { Marshal.Release(input);input=IntPtr.Zero; } }
    }
}
