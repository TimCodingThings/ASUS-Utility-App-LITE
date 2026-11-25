using System;
using System.Collections.Generic;
using Microsoft.Win32;
namespace ASUSUtilityLite {
// Select features by firmware endpoints, not a whitelist of model names.
internal sealed class LaptopCapabilities {
    internal sealed class PowerLimit {
        internal uint Id; internal string Name; internal int Value;
        internal PowerLimit(uint id,string name,int value) {Id=id;Name=name;Value=value;}
    }
    internal static string CpuVendor {
        get { try {return Convert.ToString(Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0","VendorIdentifier",""));} catch {return "";} }
    }
    internal static bool Binary(int value) {return value==0||value==1;}
    internal static bool GpuAvailable(int target,int mux,int eco) {
        if(target<0||target>3)return false;
        if(target==2)return Binary(mux);
        if(target==1)return Binary(eco)||mux==0;
        return Binary(eco);
    }
    internal static string GpuName(int mux,int eco) {
        return mux==0?"Dedicated":eco==1?"Integrated":eco==0?"Hybrid":"Unavailable";
    }
    internal static PowerLimit[] PowerLimits(IHardware hw,string vendor) {
        var limits=new List<PowerLimit>();
        bool intel=string.Equals(vendor,"GenuineIntel",StringComparison.OrdinalIgnoreCase);
        AddPower(limits,hw,Id.Power[0],intel?"PL1":"SPL");
        AddPower(limits,hw,Id.Power[1],intel?"PL2":"sPPT");
        if(!intel)AddPower(limits,hw,Id.Power[2],"fPPT");
        // Older AMD Zephyrus firmware exposes CPU / total package limits.
        // Only use this layout if the modern endpoints are all absent.
        if(limits.Count==0&&string.Equals(vendor,"AuthenticAMD",StringComparison.OrdinalIgnoreCase)) {
            AddPower(limits,hw,Id.CpuPpt,"CPU PPT");
            AddPower(limits,hw,Id.TotalPpt,"Total PPT");
        }
        return limits.ToArray();
    }
    static void AddPower(List<PowerLimit> limits,IHardware hw,uint id,string name) {
        try {int value=hw.Get(id);if(value>=0)limits.Add(new PowerLimit(id,name,value));}
        catch(System.ComponentModel.Win32Exception ex) {if(!OptionalUnavailable(ex))throw;}
    }
    static bool OptionalUnavailable(System.ComponentModel.Win32Exception ex) {return ex.NativeErrorCode==1||ex.NativeErrorCode==50||ex.NativeErrorCode==122||ex.NativeErrorCode==234;}
    internal static int[] CurveFans(IHardware hw,int mode) {
        var fans=new List<int>();if(mode<0)return fans.ToArray();
        for(int i=0;i<Id.Curves.Length;i++) {
            try {ReadFanCurve(hw,Id.Curves[i],mode);fans.Add(i);}
            catch(System.ComponentModel.Win32Exception ex) {if(!OptionalUnavailable(ex))throw;}
            catch(InvalidOperationException) {}
        }
        return fans.ToArray();
    }
    internal static byte[] ReadFanCurve(IHardware hw,uint id,int mode) {
        byte[] raw=hw.Read(id,mode==1?2:mode==2?1:0);
        if(raw==null||raw.Length<16)throw new InvalidOperationException("Fan curve response is incomplete.");
        var curve=new byte[16];Buffer.BlockCopy(raw,0,curve,0,16);FansPanel.ValidateCurve(curve);
        // Wider driver buffers may include zero padding, but never infer a
        // curve from a status header or silently discard additional payload.
        for(int i=16;i<raw.Length;i++)if(raw[i]!=0)throw new InvalidOperationException("Fan curve response contains unexpected trailing data.");
        return curve;
    }
}
}
