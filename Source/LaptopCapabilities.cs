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
        int value=hw.Get(id);if(value>=0)limits.Add(new PowerLimit(id,name,value));
    }
    internal static int[] CurveFans(IHardware hw,int mode) {
        var fans=new List<int>();if(mode<0)return fans.ToArray();
        for(int i=0;i<Id.Curves.Length;i++) {
            try {FansPanel.ValidateCurve(hw.Read(Id.Curves[i],mode==1?2:mode==2?1:0));fans.Add(i);}
            catch(System.ComponentModel.Win32Exception ex) {if(ex.NativeErrorCode!=1&&ex.NativeErrorCode!=50)throw;}
            catch(InvalidOperationException) {}
        }
        return fans.ToArray();
    }
}
}
