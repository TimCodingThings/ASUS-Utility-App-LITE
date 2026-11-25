// ASUS ACPI protocol and device IDs adapted from seerge/G-Helper (GPL-3.0).
// Modified 2026-11-24: minimal checked transport and GA403-focused controls.
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ASUSUtilityLite {
public interface IHardware : IDisposable {
    int Get(uint id);
    byte[] Read(uint id, int argument);
    void Set(uint id, byte[] value);
}
public static class Id {
    public const uint Mode=0x120075, Eco=0x90020, Mux=0x90016, Battery=0x120057, DisplayConnect=0x90030, CpuPpt=0x1200B0, TotalPpt=0x1200B1;
    public const uint CpuTemp=0x120094, GpuTemp=0x120097;
    public static readonly uint[] Fans={0x110013,0x110014,0x110031};
    public static readonly uint[] Curves={0x110024,0x110025,0x110032};
    public static readonly uint[] Power={0x1200A3,0x1200A0,0x1200C1};
    public static void Write(this IHardware hw,uint id,int value) { hw.Set(id,BitConverter.GetBytes(value)); }
    public static void EcoMode(this IHardware hw,bool eco) {
        int current=hw.Get(Eco);
        if(!LaptopCapabilities.Binary(current)) throw new InvalidOperationException("GPU switching is unavailable.");
        int display=hw.Get(DisplayConnect);
        if(eco&&current==0&&display>=0&&((display>>4)&display&15)>0)
            throw new InvalidOperationException("Disconnect the external display connected to the dedicated GPU before enabling Eco.");
        if(current!=(eco?1:0)) hw.Write(Eco,eco?1:0);
        if(hw.Get(Eco)!=(eco?1:0)) throw new InvalidOperationException("GPU mode was not accepted. Close GPU applications and try again.");
    }
}
public sealed class AsusHardware : IHardware {
    SafeFileHandle handle;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool DeviceIoControl(SafeFileHandle h,uint code,byte[] input,int inputSize,byte[] output,int outputSize,out int returned,IntPtr overlapped);
    public AsusHardware() {
        handle=CreateFile(@"\\.\ATKACPI",0xC0000000,3,IntPtr.Zero,3,0x80,IntPtr.Zero);
        if(handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot open ASUS System Control Interface. Install the ASUS driver and run as administrator.");
    }
    byte[] Call(uint method,uint id,byte[] value) {
        byte[] input=new byte[12+value.Length];
        Buffer.BlockCopy(BitConverter.GetBytes(method),0,input,0,4);
        Buffer.BlockCopy(BitConverter.GetBytes(4+value.Length),0,input,4,4);
        Buffer.BlockCopy(BitConverter.GetBytes(id),0,input,8,4);
        Buffer.BlockCopy(value,0,input,12,value.Length);
        return Transfer(method==0x53545344,capacity=> {
            byte[] output=new byte[capacity];int returned;
            if(!DeviceIoControl(handle,0x22240C,input,input.Length,output,output.Length,out returned,IntPtr.Zero)) {
                int error=Marshal.GetLastWin32Error();
                throw new Win32Exception(error,"ASUS "+(method==0x53545344?"read":"write")+" 0x"+id.ToString("X8")+": "+new Win32Exception(error).Message);
            }
            return ReturnedBytes(output,returned);
        });
    }
    internal static byte[] Transfer(bool reading,Func<int,byte[]> exchange) {
        int capacity=16;
        while(true) {
            try {return exchange(capacity);}
            catch(Win32Exception ex) {
                // Only read-only requests may be retried. A failed write could
                // already have taken effect and must never be repeated here.
                if(!reading||(ex.NativeErrorCode!=122&&ex.NativeErrorCode!=234)||capacity>=4096)throw;
                capacity*=4;
            }
        }
    }
    internal static byte[] ReturnedBytes(byte[] output,int returned) {
        if(returned<4||returned>output.Length)throw new InvalidOperationException("ASUS driver returned an incomplete or invalid response length.");
        var result=new byte[returned];Buffer.BlockCopy(output,0,result,0,returned);return result;
    }
    // Some ASUS endpoints accept DEVS but do not implement DSTS. An unsupported
    // read must not abort telemetry or be interpreted as a successful write.
    internal static int ReadOptional(Func<byte[]> read) {
        try { return BitConverter.ToInt32(read(),0)-65536; }
        catch(Win32Exception ex) { if(ex.NativeErrorCode==1||ex.NativeErrorCode==50)return -1; throw; }
    }
    public int Get(uint id) { return ReadOptional(()=>Read(id,0)); }
    public byte[] Read(uint id,int argument) { return Call(0x53545344,id,BitConverter.GetBytes(argument)); }
    public void Set(uint id,byte[] value) {
        if(BitConverter.ToInt32(Call(0x53564544,id,value),0)!=1)
            throw new InvalidOperationException("ASUS firmware rejected this setting (0x"+id.ToString("X")+").");
    }
    public void Dispose() { if(handle!=null) handle.Dispose(); }
}
}
