using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace ASUSUtilityLite {
internal sealed class PowerTransitions {
    PowerLineStatus last=PowerLineStatus.Unknown;bool resumePending;
    internal bool Observe(PowerLineStatus current,bool resumed=false) {
        if(resumed)resumePending=true;
        if(current!=PowerLineStatus.Online&&current!=PowerLineStatus.Offline)return false;
        bool changed=last!=PowerLineStatus.Unknown&&last!=current;
        last=current;bool restore=changed||resumePending;resumePending=false;return restore;
    }
}
// These overrides live only for this app session and are recorded only after
// firmware accepts a user-initiated write. Choosing a profile clears them.
internal sealed class SessionOverrides {
    readonly List<KeyValuePair<uint,byte[]>> values=new List<KeyValuePair<uint,byte[]>>();
    internal int Count {get {return values.Count;}}
    internal void Clear() {values.Clear();}
    void Remember(uint id,byte[] value) {values.RemoveAll(item=>item.Key==id);values.Add(new KeyValuePair<uint,byte[]>(id,(byte[])value.Clone()));}
    internal void ApplyCurve(IHardware hw,uint id,byte[] curve) {
        if(!Id.Curves.Contains(id))throw new ArgumentOutOfRangeException("id");
        FansPanel.ValidateCurve(curve);hw.Set(id,curve);Remember(id,curve);
    }
    internal void ApplyPower(IHardware hw,uint id,int watts) {
        if((!Id.Power.Contains(id)&&id!=Id.CpuPpt&&id!=Id.TotalPpt)||watts<5||watts>80)throw new ArgumentOutOfRangeException("watts");
        byte[] value=BitConverter.GetBytes(watts);hw.Set(id,value);Remember(id,value);
    }
    internal void Restore(IHardware hw,List<string> errors) {
        foreach(var item in values) {
            uint id=item.Key;byte[] value=(byte[])item.Value.Clone();
            PowerRestore.Attempt(errors,"Setting 0x"+id.ToString("X8"),()=>hw.Set(id,value));
        }
    }
}
internal static class PowerRestore {
    internal static void Attempt(List<string> errors,string name,Action action) {
        try {action();}catch(Exception ex) {errors.Add(name+": "+ex.Message);}
    }
    internal static string[] Apply(IHardware hw,Preferences prefs,SessionOverrides session,Action automatic,Action<bool> profileResult=null) {
        var errors=new List<string>();bool profileReady=true;
        if(prefs.LastPerformanceMode>=0&&prefs.LastPerformanceMode<=2) {
            try {hw.Write(Id.Mode,prefs.LastPerformanceMode);}
            catch(Exception ex) {profileReady=false;errors.Add("Performance mode: "+ex.Message);}
            if(profileResult!=null)profileResult(profileReady);
        }
        // Reapplying a profile resets its firmware curves and power limits.
        // Restore only values that the user successfully applied this session.
        if(profileReady)session.Restore(hw,errors);
        if(prefs.ChargeConfigured)Attempt(errors,"Charge limit",()=> {
            if(prefs.ChargeLimit<40||prefs.ChargeLimit>100)throw new InvalidOperationException("Saved charge limit is outside 40–100%.");
            hw.Write(Id.Battery,prefs.ChargeLimit);
        });
        Attempt(errors,"Automatic settings",automatic);return errors.ToArray();
    }
}
}
