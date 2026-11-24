// Minimal read-only binding to NVIDIA's documented NVAPI thermal API.
// ABI reference: https://github.com/NVIDIA/nvapi (nvapi.h, nvapi_interface.h).
// No NVIDIA source or driver binaries are bundled.
using System;
using System.Runtime.InteropServices;
namespace ASUSUtilityLite {
public sealed class NvidiaTemperature : IDisposable {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern IntPtr LoadLibraryEx(string name,IntPtr file,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)] static extern IntPtr GetProcAddress(IntPtr library,string name);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr library);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Query(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Initialize();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Enumerate([Out] IntPtr[] handles,out int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Thermal(IntPtr handle,int index,[In,Out] byte[] data);
    IntPtr library; Enumerate enumerate; Thermal thermal; Initialize unload;
    bool attempted,initialized;
    T Function<T>(Query query,uint id) where T:class {
        IntPtr p=query(id); return p==IntPtr.Zero?null:Marshal.GetDelegateForFunctionPointer(p,typeof(T)) as T;
    }
    bool Connect() {
        if(attempted)return initialized;
        attempted=true;
        // Restrict loading to the Windows system directory, where NVIDIA installs NVAPI.
        library=LoadLibraryEx("nvapi64.dll",IntPtr.Zero,0x800);
        if(library==IntPtr.Zero)return false;
        IntPtr pointer=GetProcAddress(library,"nvapi_QueryInterface");
        if(pointer==IntPtr.Zero)return false;
        var query=(Query)Marshal.GetDelegateForFunctionPointer(pointer,typeof(Query));
        var initialize=Function<Initialize>(query,0x0150E828);
        enumerate=Function<Enumerate>(query,0xE5AC921F);
        thermal=Function<Thermal>(query,0xE3640A56);
        unload=Function<Initialize>(query,0xD22BDD7E);
        if(initialize==null||enumerate==null||thermal==null)return false;
        initialized=initialize()==0; return initialized;
    }
    internal static int Decode(byte[] data) {
        if(data==null||data.Length!=68)return -1;
        int count=BitConverter.ToInt32(data,4);
        if(count<1||count>3)return -1;
        for(int i=0;i<count;i++) {
            int offset=8+i*20,temp=BitConverter.ToInt32(data,offset+12),target=BitConverter.ToInt32(data,offset+16);
            if(target==1&&temp>0&&temp<130)return temp;
        }
        return -1;
    }
    public int Read() {
        try {
            if(!Connect())return -1;
            var handles=new IntPtr[64];int count;
            if(enumerate(handles,out count)!=0||count<1||count>64)return -1;
            // This utility targets the laptop's single NVIDIA dedicated GPU.
            // Multiple adapters are ambiguous; do not label another GPU's reading.
            if(count!=1)return -1;
            byte[] data=new byte[68];Buffer.BlockCopy(BitConverter.GetBytes(68|(2<<16)),0,data,0,4);
            return thermal(handles[0],15,data)==0?Decode(data):-1;
        } catch { return -1; }
    }
    public void Dispose() {
        if(initialized&&unload!=null) {try{unload();}catch{}}
        initialized=false;
        if(library!=IntPtr.Zero) {FreeLibrary(library);library=IntPtr.Zero;}
    }
}
}
