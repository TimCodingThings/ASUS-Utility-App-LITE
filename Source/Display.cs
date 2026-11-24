// Win32 display support; layout follows Windows SDK and G-Helper DisplayNative.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace ASUSUtilityLite {
public static class Display {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    public struct Mode {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string device;
        public short spec,driver,size,extra;
        public int fields,x,y,orientation,fixedOutput;
        public short color,duplex,yResolution,tt,collate;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string form;
        public short logPixels,bits;
        public int width,height,flags,frequency,icmMethod,icmIntent,media,dither,reserved1,reserved2,panningWidth,panningHeight;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplaySettings(string device,int index,ref Mode mode);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int ChangeDisplaySettingsEx(string device,ref Mode mode,IntPtr window,uint flags,IntPtr param);
    [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
    [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags,ref uint count,[Out] byte[] paths,ref uint modes,[Out] byte[] info,IntPtr topology);
    [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo([In,Out] byte[] packet);
    static Mode NewMode() { Mode m=new Mode(); m.size=(short)Marshal.SizeOf(typeof(Mode)); return m; }
    public static string InternalPanel() {
        for(int attempt=0;attempt<3;attempt++) {
            uint p,m;
            if(GetDisplayConfigBufferSizes(2,out p,out m)!=0) return null;
            // DISPLAYCONFIG_PATH_INFO is 72 bytes; MODE_INFO is 64 bytes.
            byte[] paths=new byte[p*72], modes=new byte[m*64];
            int result=QueryDisplayConfig(2,ref p,paths,ref m,modes,IntPtr.Zero);
            if(result==122) continue;
            if(result!=0) return null;
            for(int i=0;i<p;i++) {
                int offset=i*72, technology=BitConverter.ToInt32(paths,offset+36);
                if(technology!=11 && technology!=6 && technology!=unchecked((int)0x80000000)) continue;
                byte[] name=new byte[84];
                Buffer.BlockCopy(BitConverter.GetBytes(1),0,name,0,4);
                Buffer.BlockCopy(BitConverter.GetBytes(84),0,name,4,4);
                Buffer.BlockCopy(paths,offset,name,8,12);
                if(DisplayConfigGetDeviceInfo(name)==0) return Encoding.Unicode.GetString(name,20,64).TrimEnd('\0');
            }
            return null;
        }
        return null;
    }
    public static Mode Current(string name) {
        Mode m=NewMode();
        if(name==null || !EnumDisplaySettings(name,-1,ref m)) throw new InvalidOperationException("Built-in display is unavailable.");
        return m;
    }
    public static int[] Rates(string name) {
        if(name==null) return new int[0];
        Mode current=Current(name), m=NewMode(); var rates=new HashSet<int>();
        for(int i=0;EnumDisplaySettings(name,i,ref m);i++)
            if(m.width==current.width && m.height==current.height && m.bits==current.bits && m.frequency>1) rates.Add(m.frequency);
        return rates.OrderBy(x=>x).ToArray();
    }
    public static void Set(string name,int hz) {
        if(!Rates(name).Contains(hz)) throw new InvalidOperationException("This refresh rate is not supported at the current resolution.");
        Mode m=Current(name); m.frequency=hz; m.fields=0x400000;
        int result=ChangeDisplaySettingsEx(name,ref m,IntPtr.Zero,2,IntPtr.Zero); // CDS_TEST
        if(result!=0) throw new InvalidOperationException("Display driver rejected this refresh rate ("+result+").");
        result=ChangeDisplaySettingsEx(name,ref m,IntPtr.Zero,0,IntPtr.Zero);
        if(result!=0) throw new InvalidOperationException("Could not change refresh rate ("+result+").");
    }
}
}
