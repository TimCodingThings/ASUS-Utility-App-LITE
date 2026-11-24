using System;
using System.Collections.Generic;
using System.IO;
namespace ASUSUtilityLite {
public class FakeHardware : IHardware {
    internal Dictionary<uint,int> Values=new Dictionary<uint,int> {
        {Id.Mode,1},{Id.Eco,0},{Id.Mux,1},{Id.Battery,80},{Id.CpuTemp,41},{Id.GpuTemp,41},
        {Id.Fans[0],38},{Id.Fans[1],41},{Id.Fans[2],51},{Id.Power[0],35},{Id.Power[1],45},{Id.Power[2],65}
    };
    internal int Writes,ModeReads;
    public int Get(uint id) {if(id==Id.Mode)ModeReads++;return Values.ContainsKey(id)?Values[id]:-1;}
    public byte[] Read(uint id,int argument) {return new byte[]{30,40,50,60,70,80,90,100,0,10,20,35,50,65,85,100};}
    public void Set(uint id,byte[] value) {Writes++; if(value.Length==4)Values[id]=BitConverter.ToInt32(value,0);}
    public void Dispose() {}
}
static class Tests {
    static void Assert(bool b,string name) {if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);}
    public static void Run() {
        try {
            Assert(AsusHardware.ReadOptional(()=>{throw new System.ComponentModel.Win32Exception(1);})==-1,"Unsupported ASUS read returns unavailable");
            bool denied=false;try {AsusHardware.ReadOptional(()=>{throw new System.ComponentModel.Win32Exception(5);});}catch(System.ComponentModel.Win32Exception){denied=true;}
            Assert(denied,"Access errors are preserved");
            Assert(AsusHardware.ReadOptional(()=>BitConverter.GetBytes(65536+41))==41,"Supported ASUS response decoded");
            byte[] thermal=new byte[68];Buffer.BlockCopy(BitConverter.GetBytes(1),0,thermal,4,4);Buffer.BlockCopy(BitConverter.GetBytes(46),0,thermal,20,4);Buffer.BlockCopy(BitConverter.GetBytes(1),0,thermal,24,4);
            Assert(NvidiaTemperature.Decode(thermal)==46,"NVIDIA GPU temperature decoded");
            thermal[24]=2;Assert(NvidiaTemperature.Decode(thermal)==-1,"Non-GPU temperature sensor ignored");
            Assert(NvidiaTemperature.Decode(new byte[4])==-1,"Incomplete NVIDIA response rejected");
            var discharge=new BatteryReading {State=2,Capacity=44000,Rate=-11000,Absolute=true};
            Assert(discharge.PowerText.Contains("11")&&discharge.Discharging,"Discharge power shown in watts");
            Assert(discharge.RemainingSeconds(-1)==14400,"Remaining time uses capacity and discharge rate");
            var charging=new BatteryReading {State=5,Capacity=44000,Rate=15000,Absolute=true};
            Assert(charging.PowerText.StartsWith("Charging")&&charging.RemainingSeconds(5000)==-1,"Charging does not show a discharge estimate");
            var unknown=new BatteryReading {State=2,Capacity=uint.MaxValue,Rate=int.MinValue,Absolute=true};
            Assert(unknown.RemainingSeconds(7200)==7200&&!unknown.PowerText.Contains("2147483"),"Unknown battery measurements use Windows time fallback");
            Assert(new BatteryReading {State=1,Rate=0,Absolute=true}.PowerText=="Battery idle","Charge limit or full battery shows idle");
            Assert(new BatteryReading {State=2,Rate=-100,Absolute=false}.PowerText.Contains("— W"),"Relative battery units are not mislabeled as watts");
            using(var hw=new FakeHardware()) {
                hw.EcoMode(false); Assert(hw.Writes==0,"GPU no-op avoids redundant writes");
                hw.EcoMode(true); Assert(hw.Get(Id.Eco)==1,"Eco disables dedicated GPU");
                hw.EcoMode(false); Assert(hw.Get(Id.Eco)==0,"Standard enables dedicated GPU");
                hw.Values[Id.DisplayConnect]=0x11;bool displayBlocked=false;try{hw.EcoMode(true);}catch(InvalidOperationException){displayBlocked=true;}
                Assert(displayBlocked&&hw.Get(Id.Eco)==0,"Eco preserves GPU with attached external display");hw.Values[Id.DisplayConnect]=0;
                hw.Values[Id.Eco]=-1; bool rejected=false; try {hw.EcoMode(true);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected,"Unsupported GPU switching rejected");
                FansPanel.ValidateCurve(hw.Read(0,0));
                byte[] invalid=hw.Read(0,0);invalid[2]=invalid[1]; rejected=false;try{FansPanel.ValidateCurve(invalid);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected,"Duplicate fan temperatures rejected");
                invalid=hw.Read(0,0);invalid[12]=1;rejected=false;try{FansPanel.ValidateCurve(invalid);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected,"Falling fan curve rejected");
                invalid=hw.Read(0,0);invalid[15]=69;rejected=false;try{FansPanel.ValidateCurve(invalid);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected,"Low terminal fan speed rejected");
                hw.Writes=0;
                using(var form=new MainForm(hw,new Preferences(),true)) {form.Show();System.Windows.Forms.Application.DoEvents();}
                Assert(hw.Writes==0,"Opening preview performs no hardware writes");
                Assert(hw.ModeReads==0,"Performance selection does not use ambiguous ASUS readback");
            }
            Assert(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Display.Mode))==220,"Win32 Unicode DEVMODE layout");
            string old=Preferences.Folder;
            Preferences.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-settings");
            var prefs=new Preferences {Optimized=true,AutoScreen=true,ChargeLimit=75,ChargeConfigured=true};prefs.Save();prefs.ChargeLimit=80;prefs.Save();
            var read=Preferences.Load();Assert(read.Optimized&&read.AutoScreen&&read.ChargeConfigured&&read.ChargeLimit==80,"Atomic settings round trip");
            File.Delete(Path.Combine(Preferences.Folder,"settings.xml"));Directory.Delete(Preferences.Folder);Preferences.Folder=old;
        } catch(Exception ex) {Console.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    }
}
}
