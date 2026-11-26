using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
namespace ASUSUtilityLite {
public class FakeHardware : IHardware {
    internal Dictionary<uint,int> Values=new Dictionary<uint,int> {
        {Id.Mode,1},{Id.Eco,0},{Id.Mux,1},{Id.Battery,80},{Id.CpuTemp,41},{Id.GpuTemp,41},
        {Id.Fans[0],38},{Id.Fans[1],41},{Id.Fans[2],51},{Id.Power[0],35},{Id.Power[1],45},{Id.Power[2],65}
    };
    internal int Writes,ModeReads;
    internal uint? RejectWrite;
    internal bool ResetOverridesOnMode;
    internal List<uint> WriteIds=new List<uint>();
    internal HashSet<uint> UnsupportedCurves=new HashSet<uint>();
    internal Dictionary<uint,int> ReadErrors=new Dictionary<uint,int>(),GetErrors=new Dictionary<uint,int>();
    internal Dictionary<uint,byte[]> ReadReplies=new Dictionary<uint,byte[]>();
    public int Get(uint id) {if(id==Id.Mode)ModeReads++;if(GetErrors.ContainsKey(id))throw new System.ComponentModel.Win32Exception(GetErrors[id]);return Values.ContainsKey(id)?Values[id]:-1;}
    public byte[] Read(uint id,int argument) {if(ReadErrors.ContainsKey(id))throw new System.ComponentModel.Win32Exception(ReadErrors[id]);if(UnsupportedCurves.Contains(id))throw new System.ComponentModel.Win32Exception(1);if(ReadReplies.ContainsKey(id))return ReadReplies[id];return new byte[]{30,40,50,60,70,80,90,100,0,10,20,35,50,65,85,100};}
    public void Set(uint id,byte[] value) {
        if(RejectWrite==id)throw new InvalidOperationException("Simulated firmware rejection.");Writes++;WriteIds.Add(id);
        if(value.Length==4)Values[id]=BitConverter.ToInt32(value,0);else if(Id.Curves.Contains(id))ReadReplies[id]=(byte[])value.Clone();
        if(id==Id.Mode&&ResetOverridesOnMode) {foreach(uint curve in Id.Curves)ReadReplies.Remove(curve);foreach(uint power in Id.Power)Values[power]=35;}
    }
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
            Compatibility();
            Launcher();
            RememberedState();
            ScrollingFans();
            DriverBuffers();
            FirmwareDrafts();
            PowerChanges();
            string old=Preferences.Folder;
            Preferences.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-settings");
            var prefs=new Preferences {Optimized=true,AutoScreen=true,ChargeLimit=75,ChargeConfigured=true};prefs.Save();prefs.ChargeLimit=80;prefs.Save();
            var read=Preferences.Load();Assert(read.Optimized&&read.AutoScreen&&read.ChargeConfigured&&read.ChargeLimit==80,"Atomic settings round trip");
            File.Delete(Path.Combine(Preferences.Folder,"settings.xml"));Directory.Delete(Preferences.Folder);Preferences.Folder=old;
        } catch(Exception ex) {Console.WriteLine("FAIL "+ex);Environment.ExitCode=1;}
    }
    static void Compatibility() {
        Assert(!LaptopCapabilities.GpuAvailable(2,-1,0)&&LaptopCapabilities.GpuAvailable(0,-1,0)&&LaptopCapabilities.GpuAvailable(3,-1,0),"Older Zephyrus without MUX retains Eco and Optimized");
        Assert(LaptopCapabilities.GpuAvailable(2,1,-1)&&LaptopCapabilities.GpuAvailable(1,0,-1)&&!LaptopCapabilities.GpuAvailable(0,0,-1),"MUX-only laptop supports Ultimate and returning to Standard");
        Assert(!Enumerable.Range(0,4).Any(i=>LaptopCapabilities.GpuAvailable(i,-1,-1))&&LaptopCapabilities.GpuName(-1,-1)=="Unavailable","Missing GPU controls do not claim Hybrid mode");
        Assert(!Enumerable.Range(0,4).Any(i=>LaptopCapabilities.GpuAvailable(i,3,3)),"Packed or ambiguous GPU states do not enable controls");
        using(var hw=new FakeHardware()) {
            var modern=LaptopCapabilities.PowerLimits(hw,"AuthenticAMD");
            Assert(modern.Length==3&&modern[0].Id==Id.Power[0]&&modern[2].Name=="fPPT","Modern AMD Zephyrus keeps SPL sPPT fPPT layout");
            var intel=LaptopCapabilities.PowerLimits(hw,"GenuineIntel");
            Assert(intel.Length==2&&intel[0].Name=="PL1"&&intel[1].Name=="PL2"&&intel[1].Id==Id.Power[1],"Intel Zephyrus uses PL1 PL2 without AMD fast limit");
            hw.Values[Id.CpuPpt]=45;hw.Values[Id.TotalPpt]=80;
            Assert(LaptopCapabilities.PowerLimits(hw,"AuthenticAMD").Length==3,"Modern AMD limits take precedence over legacy endpoints");
            foreach(uint id in Id.Power)hw.Values.Remove(id);
            var legacy=LaptopCapabilities.PowerLimits(hw,"AuthenticAMD");
            Assert(legacy.Length==2&&legacy[0].Id==Id.CpuPpt&&legacy[1].Id==Id.TotalPpt&&legacy[1].Name=="Total PPT","Older AMD firmware uses CPU and total package limits");
            Assert(LaptopCapabilities.PowerLimits(hw,"GenuineIntel").Length==0&&LaptopCapabilities.PowerLimits(hw,"").Length==0,"Legacy AMD endpoints are not offered on Intel or unknown CPUs");
            hw.Values.Remove(Id.CpuPpt);hw.Values.Remove(Id.TotalPpt);
            Assert(LaptopCapabilities.PowerLimits(hw,"AuthenticAMD").Length==0,"No custom power controls when endpoints are missing");
            hw.UnsupportedCurves.Add(Id.Curves[2]);hw.Values[Id.Fans[0]]=0;
            Assert(LaptopCapabilities.CurveFans(hw,0).SequenceEqual(new[]{0,1}),"Two-fan Zephyrus omits absent middle curve and retains stopped CPU fan");
            hw.UnsupportedCurves.Add(Id.Curves[0]);
            Assert(LaptopCapabilities.CurveFans(hw,0).SequenceEqual(new[]{1}),"Sparse fan choices preserve GPU endpoint mapping");
            hw.UnsupportedCurves.Add(Id.Curves[1]);
            Assert(LaptopCapabilities.CurveFans(hw,0).Length==0&&LaptopCapabilities.CurveFans(hw,-1).Length==0,"No fan editor without valid curves or selected profile");
            hw.Values[Id.Mux]=-1;hw.Values[Id.Eco]=-1;hw.Values[Id.Fans[2]]=-1;hw.Writes=0;
            using(var form=new MainForm(hw,new Preferences(),true)) {
                form.Show();Application.DoEvents();form.Location=new Point(80,80);Point anchor=form.Location;
                form.ToggleFans();form.fansPanel.ToggleAdvanced();Application.DoEvents();
                var choices=Descendants(form).OfType<ComboBox>().Single();
                Assert(choices.Items.Count==0&&!choices.Enabled,"Unsupported fan curves leave an empty disabled selector");
                Assert(!Descendants(form).OfType<NumericUpDown>().Any(n=>n.Enabled),"Absent fan and power endpoints disable advanced editors");
                Assert(Descendants(form).OfType<Button>().Where(b=>new[]{"Eco","Standard","Ultimate","Optimized"}.Contains(b.Text)).All(b=>!b.Enabled),"Main window disables absent GPU controls");
                form.fansPanel.ToggleAdvanced();form.ToggleFans();
                Assert(form.Location==anchor&&!form.AutoScroll,"Compatibility layouts preserve window position without scrolling");
            }
            Assert(hw.Writes==0,"Detecting unsupported model features performs no hardware writes");
        }
        foreach(float scale in new[]{1f,1.5f,2f})foreach(string vendor in new[]{"AuthenticAMD","GenuineIntel"}) {
            using(var hw=new FakeHardware())using(var form=new MainForm(hw,new Preferences(),true,vendor,scale)) {
                form.Show();Application.DoEvents();form.Location=new Point(0,0);Point anchor=form.Location;
                form.ToggleFans();form.fansPanel.ToggleAdvanced();Application.DoEvents();
                var choice=Descendants(form).OfType<ComboBox>().Single();
                bool fits=Descendants(form).Where(c=>c.Visible&&c!=form.contentHost.Content).All(c=>c.Left>=0&&c.Top>=0&&c.Right<=c.Parent.ClientSize.Width+1&&c.Bottom<=c.Parent.ClientSize.Height+1);
                if(!fits)foreach(var c in Descendants(form).Where(c=>c.Visible&&c!=form.contentHost.Content&&!(c.Left>=0&&c.Top>=0&&c.Right<=c.Parent.ClientSize.Width+1&&c.Bottom<=c.Parent.ClientSize.Height+1)))Console.WriteLine("Layout overflow: "+c.GetType().Name+" "+c.Text+" "+c.Bounds+" parent="+c.Parent.ClientSize);
                Assert(fits&&choice.SelectedIndex==0&&choice.Text=="CPU"&&form.Location==anchor&&!form.AutoScroll,"Advanced "+vendor+" layout and selected fan at "+(scale*100)+"% DPI");
            }
        }
    }
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent) {
        foreach(Control child in parent.Controls) {yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
    static void PowerChanges() {
        var changes=new PowerTransitions();
        Assert(!changes.Observe(PowerLineStatus.Unknown)&&!changes.Observe(PowerLineStatus.Online),"Initial power reading establishes a baseline without restoring a profile");
        Assert(!changes.Observe(PowerLineStatus.Online)&&!changes.Observe(PowerLineStatus.Online),"Repeated battery status notifications do not schedule profile writes");
        Assert(changes.Observe(PowerLineStatus.Offline)&&!changes.Observe(PowerLineStatus.Offline),"Unplugging schedules one restore rather than one per notification");
        Assert(!changes.Observe(PowerLineStatus.Unknown)&&changes.Observe(PowerLineStatus.Online),"Unknown power readings preserve the last known source");
        Assert(changes.Observe(PowerLineStatus.Online,true)&&!changes.Observe(PowerLineStatus.Online),"Resume schedules a restore even when the power source is unchanged");
        Assert(!changes.Observe(PowerLineStatus.Unknown,true)&&changes.Observe(PowerLineStatus.Online),"Resume with unknown power defers restoration until a known reading");
        string old=Preferences.Folder;Preferences.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-power-settings");
        try {
            using(var hw=new FakeHardware()) {
                foreach(int mode in new[]{0,1,2}) {
                    var prefs=new Preferences {LastPerformanceMode=mode};hw.Values[Id.Mode]=3;hw.WriteIds.Clear();hw.ModeReads=0;
                    var errors=PowerRestore.Apply(hw,prefs,new SessionOverrides(),()=>{});
                    Assert(errors.Length==0&&hw.Values[Id.Mode]==mode&&hw.WriteIds.SequenceEqual(new[]{Id.Mode})&&hw.ModeReads==0,"Power change restores selected "+new[]{"Balanced","Turbo","Silent"}[mode]+" without ambiguous firmware readback");
                }
                hw.WriteIds.Clear();PowerRestore.Apply(hw,new Preferences(),new SessionOverrides(),()=>{});
                Assert(hw.WriteIds.Count==0,"Power changes do not invent a mode when the user has never selected one");
                var session=new SessionOverrides();var curve=hw.Read(0,0);curve[8]=4;var savedCurve=(byte[])curve.Clone();
                session.ApplyCurve(hw,Id.Curves[0],curve);session.ApplyPower(hw,Id.Power[0],25);curve[8]=9;
                hw.ResetOverridesOnMode=true;hw.WriteIds.Clear();var selected=new Preferences {LastPerformanceMode=2,ChargeConfigured=true,ChargeLimit=80};bool automatic=false;
                var result=PowerRestore.Apply(hw,selected,session,()=>automatic=true);
                Assert(result.Length==0&&hw.WriteIds.SequenceEqual(new[]{Id.Mode,Id.Curves[0],Id.Power[0],Id.Battery})&&hw.ReadReplies[Id.Curves[0]].SequenceEqual(savedCurve)&&hw.Values[Id.Power[0]]==25&&automatic,"Profile restores precede cloned session overrides, charge limit, and automatic settings");
                hw.RejectWrite=Id.Power[0];bool rejected=false;
                try {session.ApplyPower(hw,Id.Power[0],30);}catch(InvalidOperationException){rejected=true;}
                hw.RejectWrite=null;PowerRestore.Apply(hw,selected,session,()=>{});
                Assert(rejected&&session.Count==2&&hw.Values[Id.Power[0]]==25,"Rejected custom writes do not replace the last accepted session values");
                hw.RejectWrite=Id.Mode;hw.WriteIds.Clear();automatic=false;
                result=PowerRestore.Apply(hw,selected,session,()=>automatic=true);
                Assert(result.Length==1&&hw.WriteIds.SequenceEqual(new[]{Id.Battery})&&automatic&&session.Count==2,"Rejected profile restore skips profile-specific overrides but still applies independent settings");
                hw.RejectWrite=Id.Curves[0];hw.WriteIds.Clear();automatic=false;
                result=PowerRestore.Apply(hw,selected,session,()=>automatic=true);
                Assert(result.Length==1&&hw.WriteIds.Contains(Id.Power[0])&&hw.WriteIds.Contains(Id.Battery)&&automatic,"One failed session override does not block power limits, charging, or automatic settings");
                hw.RejectWrite=Id.Battery;automatic=false;
                result=PowerRestore.Apply(hw,selected,session,()=>automatic=true);
                Assert(result.Length==1&&automatic,"Charge-limit restoration failure does not block automatic GPU and screen settings");
                hw.RejectWrite=null;
                result=PowerRestore.Apply(hw,selected,session,()=> {throw new InvalidOperationException("Automatic setting rejected.");});
                Assert(result.Length==1&&hw.Values[Id.Mode]==2,"Automatic settings failure does not undo the restored performance mode");
                hw.RejectWrite=Id.Mode;rejected=false;
                try {selected.SelectPerformanceMode(hw,0,session.Clear);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&session.Count==2&&selected.LastPerformanceMode==2,"Failed manual mode selection preserves session overrides");
                hw.RejectWrite=null;selected.SelectPerformanceMode(hw,0,session.Clear);hw.WriteIds.Clear();PowerRestore.Apply(hw,selected,session,()=>{});
                Assert(session.Count==0&&hw.Values[Id.Mode]==0&&hw.WriteIds.SequenceEqual(new[]{Id.Mode,Id.Battery}),"A new manual mode clears previous overrides and wins over a pending power restore");
                var attempted=new List<string>();bool screen=false;
                PowerRestore.Attempt(attempted,"GPU",()=> {throw new InvalidOperationException("GPU unavailable.");});
                PowerRestore.Attempt(attempted,"Screen",()=>screen=true);
                Assert(attempted.Count==1&&screen,"Independent automatic actions continue after a GPU failure");
            }
        } finally {
            string path=Path.Combine(Preferences.Folder,"settings.xml");if(File.Exists(path))File.Delete(path);
            if(Directory.Exists(Preferences.Folder))Directory.Delete(Preferences.Folder);Preferences.Folder=old;
        }
    }
    static void FirmwareDrafts() {
        byte[][] reported={
            new byte[]{43,68,70,72,74,76,78,255,1,6,8,13,18,22,32,32},
            new byte[]{43,67,68,70,70,70,70,255,1,6,8,13,18,22,32,32},
            new byte[]{43,68,70,72,74,76,78,255,1,1,16,16,28,35,47,47}
        };
        using(var hw=new FakeHardware()) {
            for(int i=0;i<3;i++) {
                var packet=new byte[64];Buffer.BlockCopy(reported[i],0,packet,0,16);hw.ReadReplies[Id.Curves[i]]=packet;
            }
            Assert(LaptopCapabilities.CurveFans(hw,2).SequenceEqual(new[]{0,1,2}),"Captured GA403UP firmware replies enable CPU GPU and Middle fan choices");
            foreach(var curve in reported) {
                var original=(byte[])curve.Clone();var draft=AdvancedFansPanel.EditableCurve(curve);FansPanel.ValidateCurve(draft);
                Assert(curve.SequenceEqual(original)&&draft[7]==100&&draft[15]>=70,"Firmware draft repairs terminal point without changing the original reply");
            }
            var gpu=AdvancedFansPanel.EditableCurve(reported[1]);
            Assert(gpu.Take(8).SequenceEqual(new byte[]{43,67,68,70,71,72,73,100}),"Repeated firmware temperatures become distinct editable draft points");
            var valid=hw.Read(0,0);
            Assert(AdvancedFansPanel.EditableCurve(valid).SequenceEqual(valid),"Already editable firmware curves are preserved exactly");
            var invalid=(byte[])reported[0].Clone();invalid[3]=30;bool rejected=false;
            try {AdvancedFansPanel.EditableCurve(invalid);}catch(InvalidOperationException){rejected=true;}
            Assert(rejected,"Descending firmware temperatures are rejected rather than repaired");
            invalid=(byte[])reported[0].Clone();invalid[12]=1;rejected=false;
            try {AdvancedFansPanel.EditableCurve(invalid);}catch(InvalidOperationException){rejected=true;}
            Assert(rejected,"Descending firmware fan speeds remain rejected");
            invalid=(byte[])reported[0].Clone();invalid[3]=255;rejected=false;
            try {AdvancedFansPanel.EditableCurve(invalid);}catch(InvalidOperationException){rejected=true;}
            Assert(rejected,"255 is accepted only at the terminal firmware point");
            using(var form=new MainForm(hw,new Preferences {LastPerformanceMode=2},true,"AuthenticAMD",1f)) {
                form.Show();Application.DoEvents();form.ToggleFans();form.fansPanel.ToggleAdvanced();Application.DoEvents();
                var fields=Descendants(form).OfType<NoWheelNumber>().Where(n=>n.Maximum==100).ToArray();
                Assert(Descendants(form).OfType<ComboBox>().Single().Items.Count==3&&fields.Length==16&&fields.All(n=>n.Enabled)&&Descendants(form).OfType<Label>().Any(l=>l.Text=="Firmware draft adjusted. Review before Apply.")&&hw.Writes==0,"Real firmware defaults open editable fields with an adjusted-draft notice and no hardware writes");
            }
        }
    }
    static void DriverBuffers() {
        var capacities=new List<int>();
        var packet=AsusHardware.Transfer(true,capacity=> {capacities.Add(capacity);if(capacity<64)throw new System.ComponentModel.Win32Exception(122);return BitConverter.GetBytes(65536+41);});
        Assert(capacities.SequenceEqual(new[]{16,64})&&BitConverter.ToInt32(packet,0)==65577,"ASUS reads retry an insufficient buffer with a larger capacity");
        capacities.Clear();
        AsusHardware.Transfer(true,capacity=> {capacities.Add(capacity);if(capacity<256)throw new System.ComponentModel.Win32Exception(234);return new byte[16];});
        Assert(capacities.SequenceEqual(new[]{16,64,256}),"ASUS partial-data reads retry with a bounded larger buffer");
        capacities.Clear();bool failed=false;
        try {AsusHardware.Transfer(false,capacity=> {capacities.Add(capacity);throw new System.ComponentModel.Win32Exception(122);});}catch(System.ComponentModel.Win32Exception ex){failed=ex.NativeErrorCode==122;}
        Assert(failed&&capacities.Count==1,"ASUS writes are never retried after a buffer error");
        capacities.Clear();failed=false;
        try {AsusHardware.Transfer(true,capacity=> {capacities.Add(capacity);throw new System.ComponentModel.Win32Exception(5);});}catch(System.ComponentModel.Win32Exception ex){failed=ex.NativeErrorCode==5;}
        Assert(failed&&capacities.Count==1,"Read permission errors remain visible and are not retried");
        capacities.Clear();failed=false;
        try {AsusHardware.Transfer(true,capacity=> {capacities.Add(capacity);throw new System.ComponentModel.Win32Exception(122);});}catch(System.ComponentModel.Win32Exception ex){failed=ex.NativeErrorCode==122;}
        Assert(failed&&capacities.SequenceEqual(new[]{16,64,256,1024,4096}),"Read buffer growth stops at 4096 bytes and preserves the driver error");
        var output=new byte[64];output[0]=7;
        Assert(AsusHardware.ReturnedBytes(output,4).SequenceEqual(new byte[]{7,0,0,0}),"Only bytes actually returned by the driver are decoded");
        bool shortRejected=false,overrunRejected=false;
        try {AsusHardware.ReturnedBytes(output,3);}catch(InvalidOperationException){shortRejected=true;}
        try {AsusHardware.ReturnedBytes(output,65);}catch(InvalidOperationException){overrunRejected=true;}
        Assert(shortRejected&&overrunRejected,"Incomplete and out-of-range response lengths are rejected");
        using(var hw=new FakeHardware()) {
            var curve=hw.Read(Id.Curves[0],0);var padded=new byte[64];Buffer.BlockCopy(curve,0,padded,0,16);hw.ReadReplies[Id.Curves[0]]=padded;
            Assert(LaptopCapabilities.ReadFanCurve(hw,Id.Curves[0],0).SequenceEqual(curve),"Fan curve decoding accepts zero padding from a larger driver response");
            padded[20]=1;failed=false;try {LaptopCapabilities.ReadFanCurve(hw,Id.Curves[0],0);}catch(InvalidOperationException){failed=true;}
            Assert(failed&&!LaptopCapabilities.CurveFans(hw,0).Contains(0),"Unexpected extra fan payload is rejected without guessing a curve");
            hw.ReadReplies[Id.Curves[0]]=new byte[12];
            Assert(!LaptopCapabilities.CurveFans(hw,0).Contains(0),"Short fan responses cannot enable the curve editor");
            hw.ReadReplies.Clear();hw.ReadErrors[Id.Curves[0]]=122;hw.ReadErrors[Id.Curves[1]]=234;
            Assert(LaptopCapabilities.CurveFans(hw,0).SequenceEqual(new[]{2}),"A fan read buffer failure preserves other available fan choices");
            hw.ReadErrors[Id.Curves[2]]=122;hw.GetErrors[Id.Power[1]]=122;
            Assert(LaptopCapabilities.PowerLimits(hw,"AuthenticAMD").Select(p=>p.Id).SequenceEqual(new[]{Id.Power[0],Id.Power[2]}),"An unavailable optional power read preserves the other power controls");
            using(var form=new MainForm(hw,new Preferences(),true,"AuthenticAMD",1f)) {
                form.Show();Application.DoEvents();Size size=form.Size;Point anchor=form.Location;
                form.ToggleFans();form.fansPanel.ToggleAdvanced();Application.DoEvents();
                Assert(!Descendants(form).OfType<ComboBox>().Single().Enabled&&Descendants(form).OfType<CheckBox>().Count(c=>new[]{"SPL","fPPT"}.Contains(c.Text))==2&&form.Size==size&&form.Location==anchor&&hw.Writes==0,"Advanced opens with available power controls when fan curve reads fail");
            }
            hw.ReadErrors[Id.Curves[0]]=5;failed=false;try {LaptopCapabilities.CurveFans(hw,0);}catch(System.ComponentModel.Win32Exception ex){failed=ex.NativeErrorCode==5;}
            Assert(failed,"Fan capability detection does not hide access-denied errors");
        }
    }
    static void WheelMessage(Control control,int delta) {
        var message=Message.Create(control.Handle,0x20A,new IntPtr((long)(ushort)(short)delta<<16),IntPtr.Zero);
        control.GetType().GetMethod("WndProc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(control,new object[]{message});
    }
    static void ScrollingFans() {
        foreach(float scale in new[]{1f,1.5f,2f})using(var hw=new FakeHardware())using(var form=new MainForm(hw,new Preferences(),true,"AuthenticAMD",scale)) {
            form.Show();Application.DoEvents();form.Location=new Point(50,50);Point anchor=form.Location;Size size=form.Size;
            form.ToggleFans();Application.DoEvents();
            Assert(form.Size==size&&form.Location==anchor&&form.contentHost.CanScroll&&form.fansPanel.FindForm()==form,"Simple cooling scrolls inside the original window at "+(scale*100)+"% DPI");
            form.fansPanel.ToggleAdvanced();Application.DoEvents();form.contentHost.ScrollTo(int.MaxValue);
            var viewport=form.contentHost;
            Assert(form.Size==size&&form.Location==anchor&&!form.AutoScroll&&!viewport.AutoScroll&&viewport.Content.Bottom==viewport.ClientSize.Height&&Descendants(form).Where(c=>c.Parent==viewport.Content&&c.Visible).All(c=>c.Right<=viewport.Content.Width),"Advanced cooling reaches the bottom without horizontal scrolling at "+(scale*100)+"% DPI");
            var slider=Descendants(form).OfType<PointerTrackBar>().Single();int limit=slider.Value;
            viewport.ScrollTo(0);WheelMessage(slider,-120);
            Assert(slider.Value==limit&&(viewport.Offset>0||SystemInformation.MouseWheelScrollLines==0),"Wheel over charge slider scrolls without changing limit at "+(scale*100)+"% DPI");
            var number=Descendants(form).OfType<NoWheelNumber>().First();decimal value=number.Value;
            viewport.ScrollTo(0);WheelMessage(number,-120);
            Assert(number.Value==value&&(viewport.Offset>0||SystemInformation.MouseWheelScrollLines==0),"Wheel over fan editor scrolls without changing value at "+(scale*100)+"% DPI");
            var fan=Descendants(form).OfType<NoWheelComboBox>().Single();int index=fan.SelectedIndex;
            viewport.ScrollTo(0);WheelMessage(fan,-120);
            Assert(fan.SelectedIndex==index&&(viewport.Offset>0||SystemInformation.MouseWheelScrollLines==0),"Wheel over fan choice scrolls without changing selection at "+(scale*100)+"% DPI");
            viewport.ScrollTo(0);number.Focus();Application.DoEvents();
            Assert(viewport.Offset>0,"Keyboard focus reveals off-screen fan controls at "+(scale*100)+"% DPI");
            form.fansPanel.ToggleAdvanced();form.ToggleFans();Application.DoEvents();
            Assert(form.Size==size&&form.Location==anchor&&!viewport.CanScroll&&viewport.Offset==0&&hw.Writes==0,"Closing cooling restores the main view without resizing or hardware writes at "+(scale*100)+"% DPI");
        }
    }
    static void RememberedState() {
        var primary=new Rectangle(0,0,1920,1040);var left=new Rectangle(-1920,0,1920,1040);var window=new Size(828,930);
        Assert(WindowPlacement.Restore(new Point(200,70),window,new[]{primary})==new Point(200,70),"Saved window location is unchanged when it fits");
        Assert(WindowPlacement.Restore(new Point(-1700,50),window,new[]{left,primary})==new Point(-1700,50),"Window location supports a monitor left of the primary screen");
        var recovered=WindowPlacement.Restore(new Point(2500,100),window,new[]{primary});
        Assert(primary.Contains(new Rectangle(recovered,window)),"Disconnected monitor placement is recovered onto an available screen");
        Assert(WindowPlacement.Restore(new Point(1800,1000),new Size(2500,1500),new[]{primary})==primary.Location,"Oversized window restores with its title bar visible");
        Assert(primary.Contains(new Rectangle(WindowPlacement.Restore(new Point(int.MaxValue,int.MinValue),window,new[]{primary}),window)),"Extreme saved coordinates cannot overflow screen recovery");
        string old=Preferences.Folder;
        Preferences.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-window-settings");
        try {
            Directory.CreateDirectory(Preferences.Folder);
            File.WriteAllText(Path.Combine(Preferences.Folder,"settings.xml"),"<Preferences><ChargeLimit>75</ChargeLimit></Preferences>");
            var migrated=Preferences.Load();
            Assert(migrated.LastPerformanceMode==-1&&!migrated.WindowPositionSaved&&migrated.ChargeLimit==75,"Existing settings load without inventing a saved mode or position");
            using(var hw=new FakeHardware()) {
                foreach(int value in new[]{0,1,2}) {
                    var prefs=new Preferences();prefs.SelectPerformanceMode(hw,value);
                    var loaded=Preferences.Load();hw.Writes=0;hw.ModeReads=0;
                    using(var form=new MainForm(hw,loaded,true,null,1f)) {
                        form.Show();Application.DoEvents();
                        string expected=new[]{"Balanced","Turbo","Silent"}[value];
                        var modes=Descendants(form).OfType<Button>().Where(b=>new[]{"Silent","Balanced","Turbo"}.Contains(b.Text)).ToArray();
                        Assert(loaded.LastPerformanceMode==value&&modes.Count(b=>b.FlatAppearance.BorderSize==2)==1&&modes.Single(b=>b.FlatAppearance.BorderSize==2).Text==expected&&hw.Writes==0&&hw.ModeReads==0,"Reopened "+expected+" selection persists without a firmware read or write");
                    }
                }
                var previous=Preferences.Load();hw.RejectWrite=Id.Mode;bool rejected=false;
                try {previous.SelectPerformanceMode(hw,0);}catch(InvalidOperationException){rejected=true;}
                Assert(rejected&&previous.LastPerformanceMode==2&&Preferences.Load().LastPerformanceMode==2,"Rejected mode change preserves the last successful selection on disk");
                hw.RejectWrite=null;
                var area=Screen.PrimaryScreen.WorkingArea;Point saved=new Point(area.Left+30,area.Top+30);
                previous.RememberWindow(saved);var restored=Preferences.Load();
                Assert(restored.WindowPositionSaved&&new Point(restored.WindowX,restored.WindowY)==saved,"Window coordinates survive an atomic settings reload");
                using(var form=new MainForm(hw,restored,true,null,1f)) {
                    form.Show();Application.DoEvents();Point expected=WindowPlacement.Restore(saved,form.Size,Screen.AllScreens.Select(s=>s.WorkingArea).ToArray());
                    Assert(form.StartPosition==FormStartPosition.Manual&&form.Location==expected,"New window restores its saved location after DPI scaling");
                    form.ToggleFans();form.fansPanel.ToggleAdvanced();form.fansPanel.ToggleAdvanced();form.ToggleFans();
                    Assert(form.Location==expected&&!form.AutoScroll,"Restored location is preserved through cooling panel changes");
                    form.Location=new Point(expected.X+10,expected.Y+10);form.Close();
                }
                var afterPreview=Preferences.Load();
                Assert(new Point(afterPreview.WindowX,afterPreview.WindowY)==saved,"Preview closing never overwrites real saved window coordinates");
            }
        } finally {
            string path=Path.Combine(Preferences.Folder,"settings.xml");if(File.Exists(path))File.Delete(path);
            if(Directory.Exists(Preferences.Folder))Directory.Delete(Preferences.Folder);Preferences.Folder=old;
        }
    }
    sealed class ActivationForm : MainForm {
        internal ActivationForm(IHardware hw):base(hw,new Preferences(),true) {}
        internal void RequestOpen() {var message=Message.Create(Handle,(int)WindowActivation.Message,IntPtr.Zero,IntPtr.Zero);base.WndProc(ref message);}
    }
    static void Launcher() {
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Folder & Space","ASUS Utility LITE.exe"),sid="S-1-5-21-111-222-333-1001";
        string xml=ElevatedLaunch.Definition(path,sid);
        bool exists=false;int registrations=0;
        ElevatedLaunch.EnsureTask(()=> {if(!exists)throw System.Runtime.InteropServices.Marshal.GetExceptionForHR(unchecked((int)0x80070002));return xml;},()=> {exists=true;registrations++;},path,sid);
        Assert(exists&&registrations==1,"First launch creates task after managed file-not-found HRESULT");
        ElevatedLaunch.EnsureTask(()=>xml,()=>registrations++,path,sid);
        Assert(registrations==1,"Existing approved task is reused without re-registration");
        exists=false;
        ElevatedLaunch.EnsureTask(()=> {if(!exists)throw new System.Runtime.InteropServices.COMException("Missing task",unchecked((int)0x80070002));return xml;},()=> {exists=true;},path,sid);
        Assert(exists,"First launch also handles COM-style missing-task errors");
        bool denied=false;registrations=0;
        try {ElevatedLaunch.EnsureTask(()=> {throw System.Runtime.InteropServices.Marshal.GetExceptionForHR(unchecked((int)0x80070005));},()=>registrations++,path,sid);}
        catch(UnauthorizedAccessException) {denied=true;}
        Assert(denied&&registrations==0,"Scheduler access denial is preserved rather than mistaken for a missing task");
        bool invalid=false;
        try {ElevatedLaunch.EnsureTask(()=>"invalid XML",()=>registrations++,path,sid);}catch(InvalidOperationException) {invalid=true;}
        Assert(invalid&&registrations==1,"Registration must pass readback validation before reporting approval");
        exists=false;registrations=0;
        ElevatedLaunch.EnsureTask(()=>exists?xml:xml.Replace("--scheduled-launch","--background"),()=> {exists=true;registrations++;},path,sid);
        Assert(registrations==1,"Stale launcher definition is replaced and verified");
        Assert(ElevatedLaunch.Matches(xml,path,sid),"Approved launch task supports spaces and XML-special path characters");
        Assert(ElevatedLaunch.Matches(xml.Replace("<AllowStartOnDemand>true</AllowStartOnDemand>","").Replace("<Enabled>true</Enabled>",""),path,sid),"Scheduler XML may omit settings whose schema default is true");
        Assert(ElevatedLaunch.Matches(xml.Replace(" Context=\"User\"",""),path,sid),"Implicit action context uses the sole validated user principal");
        Assert(!ElevatedLaunch.Matches(xml.Replace("Context=\"User\"","Context=\"Other\""),path,sid),"Explicit action context must match the validated principal");
        Assert(ElevatedLaunch.Matches(xml.Replace(">true<",">1<").Replace(">false<",">0<").Replace("PT0S","P0D"),path,sid),"Equivalent XML boolean and unlimited-duration values are accepted");
        string accountXml=xml.Replace(sid,@"LAPTOP\Owner");
        Assert(ElevatedLaunch.ValidationError(accountXml,path,sid,name=>name==@"LAPTOP\Owner"?sid:"")==null,"Exported account name is resolved to the approved user SID");
        Assert(ElevatedLaunch.ValidationError(accountXml,path,sid,name=>sid+"0")!=null,"Account-name normalization cannot approve a different user");
        Assert(!ElevatedLaunch.Matches(xml.Replace("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>",""),path,sid),"Missing battery setting uses restrictive schema default");
        Assert(!ElevatedLaunch.Matches(xml.Replace("<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>",""),path,sid),"Missing execution limit does not imply unlimited time");
        Assert(ElevatedLaunch.ValidationError(xml,path+".old",sid).Contains("Executable path"),"Task validation identifies the mismatching field");
        Assert(!ElevatedLaunch.Matches(xml.Replace("</Actions>","<Exec><Command>other.exe</Command></Exec></Actions>"),path,sid),"Task normalization still rejects additional executable actions");
        Assert(!ElevatedLaunch.Matches(xml,path+".old",sid),"Moved executable requires fresh approval");
        Assert(!ElevatedLaunch.Matches(xml,path,sid+"0"),"Approved launch task is restricted to the current user");
        Assert(!ElevatedLaunch.Matches(xml.Replace("HighestAvailable","LeastPrivilege"),path,sid),"Unelevated launch task is rejected");
        Assert(!ElevatedLaunch.Matches(xml.Replace(ElevatedLaunch.Arguments,"--background"),path,sid),"Manual launcher cannot silently reuse background-startup action");
        Assert(!ElevatedLaunch.Matches(xml.Replace("Parallel","IgnoreNew"),path,sid),"Launch task allows reopening while the first process runs");
        Assert(!ElevatedLaunch.Matches(xml.Replace("<DisallowStartIfOnBatteries>false","<DisallowStartIfOnBatteries>true"),path,sid),"Approved launch works on battery power");
        Assert(!ElevatedLaunch.Matches(xml.Replace("<AllowStartOnDemand>true","<AllowStartOnDemand>false"),path,sid),"Disabled on-demand execution is rejected");
        Assert(!ElevatedLaunch.Matches("invalid XML",path,sid),"Malformed task definition falls back to normal UAC");
        using(var hw=new FakeHardware())using(var form=new ActivationForm(hw)) {
            form.Show();Application.DoEvents();form.Hide();form.RequestOpen();Application.DoEvents();
            Assert(WindowActivation.Message!=0&&form.Visible&&form.WindowState==FormWindowState.Normal&&hw.Writes==0,"Reopening restores the existing window without hardware writes");
        }
    }
}
}
