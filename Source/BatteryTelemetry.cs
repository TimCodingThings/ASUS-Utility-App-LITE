// Read-only Windows battery-class queries. Device enumeration follows G-Helper.
// ABI: Microsoft BATTERY_INFORMATION, BATTERY_STATUS and battery IOCTL docs.
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace ASUSUtilityLite {
public sealed class BatteryReading {
    public uint State,Capacity;
    public int Rate;
    public bool Absolute;
    public bool Discharging { get { return Rate!=int.MinValue&&Rate!=0?Rate<0:(State&2)!=0; } }
    public string PowerText {
        get {
            string activity=Discharging?"Discharging":(State&4)!=0||(Rate>0&&Rate!=int.MinValue)?"Charging":"Battery idle";
            if(Rate==0)return "Battery idle";
            if(Rate==int.MinValue||!Absolute)return activity+": — W";
            return activity+": "+(Math.Abs((double)Rate)/1000).ToString("0.0",CultureInfo.CurrentCulture)+" W";
        }
    }
    internal int RemainingSeconds(int windowsEstimate) {
        if(!Discharging)return -1;
        if(Capacity!=uint.MaxValue&&Rate!=int.MinValue&&Rate<0) {
            double seconds=Capacity/-(double)Rate*3600;
            if(seconds>=0&&seconds<int.MaxValue)return (int)seconds;
        }
        return windowsEstimate;
    }
    public string RemainingText(int windowsEstimate) {
        if(!Discharging)return (State&1)!=0?"On AC power":"";
        int seconds=RemainingSeconds(windowsEstimate);
        if(seconds<0)return "Est. remaining: —";
        int minutes=seconds/60;
        return "Est. "+(minutes<1?"<1 min":minutes>=60?(minutes/60)+"h "+(minutes%60)+"m":minutes+" min")+" left";
    }
}
public sealed class BatteryTelemetry {
    string path;
    static readonly Guid BatteryGuid=new Guid("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
    [StructLayout(LayoutKind.Sequential)] struct InterfaceData {
        public uint Size;public Guid ClassGuid;public uint Flags;public IntPtr Reserved;
    }
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid guid,IntPtr enumerator,IntPtr parent,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr devices,IntPtr data,ref Guid guid,uint index,ref InterfaceData item);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr devices,ref InterfaceData item,IntPtr detail,uint size,out uint required,IntPtr data);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr devices);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool DeviceIoControl(SafeFileHandle handle,uint code,byte[] input,int inputSize,[Out] byte[] output,int outputSize,out int returned,IntPtr overlapped);
    string FindBattery() {
        Guid guid=BatteryGuid;IntPtr devices=SetupDiGetClassDevs(ref guid,IntPtr.Zero,IntPtr.Zero,0x12);
        if(devices==new IntPtr(-1))return null;
        try {
            var item=new InterfaceData {Size=(uint)Marshal.SizeOf(typeof(InterfaceData))};
            if(!SetupDiEnumDeviceInterfaces(devices,IntPtr.Zero,ref guid,0,ref item))return null;
            uint size;SetupDiGetDeviceInterfaceDetail(devices,ref item,IntPtr.Zero,0,out size,IntPtr.Zero);
            if(size<8||size>65536)return null;
            IntPtr buffer=Marshal.AllocHGlobal((int)size);
            try {
                Marshal.WriteInt32(buffer,IntPtr.Size==8?8:6);
                if(!SetupDiGetDeviceInterfaceDetail(devices,ref item,buffer,size,out size,IntPtr.Zero))return null;
                return Marshal.PtrToStringUni(IntPtr.Add(buffer,4));
            }finally {Marshal.FreeHGlobal(buffer);}
        }finally {SetupDiDestroyDeviceInfoList(devices);}
    }
    static byte[] Query(SafeFileHandle handle,uint code,byte[] input,int size) {
        byte[] output=new byte[size];int returned;
        return DeviceIoControl(handle,code,input,input.Length,output,size,out returned,IntPtr.Zero)&&returned>=size?output:null;
    }
    public BatteryReading Read() {
        try {
            if(path==null)path=FindBattery();if(path==null)return null;
            using(var handle=CreateFile(path,0xC0000000,3,IntPtr.Zero,3,0x80,IntPtr.Zero)) {
                if(handle.IsInvalid) {path=null;return null;}
                byte[] tag=Query(handle,0x294040,new byte[4],4);
                if(tag==null||BitConverter.ToUInt32(tag,0)==0) {path=null;return null;}
                byte[] request=new byte[12];Buffer.BlockCopy(tag,0,request,0,4);
                byte[] info=Query(handle,0x294044,request,36);
                byte[] wait=new byte[20];Buffer.BlockCopy(tag,0,wait,0,4);
                byte[] status=Query(handle,0x29404C,wait,16);
                if(info==null||status==null)return null;
                return new BatteryReading {State=BitConverter.ToUInt32(status,0),Capacity=BitConverter.ToUInt32(status,4),Rate=BitConverter.ToInt32(status,12),Absolute=(BitConverter.ToUInt32(info,0)&0x40000000)==0};
            }
        }catch {path=null;return null;}
    }
}
}
