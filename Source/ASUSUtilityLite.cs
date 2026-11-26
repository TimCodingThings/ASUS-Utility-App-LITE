using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;
namespace ASUSUtilityLite {
public class Preferences {
    public bool Optimized, AutoScreen;
    public int ChargeLimit=80;
    public bool ChargeConfigured;
    public int LastPerformanceMode=-1;
    public bool WindowPositionSaved;
    public int WindowX,WindowY;
    internal void SelectPerformanceMode(IHardware hardware,int value,Action accepted=null) {
        if(value<0||value>2)throw new ArgumentOutOfRangeException("value");
        hardware.Write(Id.Mode,value);
        LastPerformanceMode=value;if(accepted!=null)accepted();Save();
    }
    internal void RememberWindow(Point location) {
        WindowX=location.X;WindowY=location.Y;WindowPositionSaved=true;Save();
    }
    public static string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ASUS Utility LITE");
    public static Preferences Load() {
        try {
            string path=Path.Combine(Folder,"settings.xml");
            bool migrate=!File.Exists(path);
            if(migrate)path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GHelperLite","settings.xml");
            Preferences value;using(var f=File.OpenRead(path))value=(Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(f);
            if(migrate)value.Save();return value;
        }
        catch { return new Preferences(); }
    }
    public void Save() {
        Directory.CreateDirectory(Folder);
        string path=Path.Combine(Folder,"settings.xml"), temp=path+".tmp";
        using(var f=File.Create(temp)) new XmlSerializer(typeof(Preferences)).Serialize(f,this);
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
}
public static class Startup {
    static string Name { get { return "ASUS Utility LITE-"+WindowsIdentity.GetCurrent().User.Value; } }
    static dynamic Service() { dynamic s=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")); s.Connect(); return s; }
    public static bool Enabled() {
        try { return Service().GetFolder("\\").GetTask(Name).Enabled; } catch { return false; }
    }
    public static void MigrateLegacy() {
        dynamic folder=Service().GetFolder("\\");dynamic legacy;
        string oldName="GHelperLite-"+WindowsIdentity.GetCurrent().User.Value;
        try {legacy=folder.GetTask(oldName);} catch {return;}
        if(legacy.Enabled)Set(true);
        folder.DeleteTask(oldName,0);
    }
    public static void Set(bool enabled) {
        dynamic service=Service(), folder=service.GetFolder("\\");
        if(!enabled) { folder.DeleteTask(Name,0); return; }
        dynamic task=service.NewTask(0);
        task.RegistrationInfo.Description="ASUS Utility LITE: ASUS laptop controls";
        task.Principal.UserId=WindowsIdentity.GetCurrent().User.Value;
        task.Principal.LogonType=3; task.Principal.RunLevel=1;
        task.Settings.Enabled=true; task.Settings.DisallowStartIfOnBatteries=false;
        task.Settings.StopIfGoingOnBatteries=false; task.Settings.ExecutionTimeLimit="PT0S";
        task.Settings.MultipleInstances=2; task.Settings.StartWhenAvailable=true;
        dynamic trigger=task.Triggers.Create(9); trigger.UserId=task.Principal.UserId;
        dynamic action=task.Actions.Create(0); action.Path=Application.ExecutablePath; action.Arguments="--background";
        folder.RegisterTaskDefinition(Name,task,6,task.Principal.UserId,null,3,null);
    }
}
static class Program {
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static void Main(string[] args) {
        SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--self-test")) { Tests.Run(); return; }
        if(args.Contains("--battery-probe")) {
            var reading=new BatteryTelemetry().Read();
            Console.WriteLine(reading==null?"Battery telemetry unavailable":reading.PowerText+"; "+reading.RemainingText(SystemInformation.PowerStatus.BatteryLifeRemaining)+"; capacity="+reading.Capacity+"; raw rate="+reading.Rate+"; absolute="+reading.Absolute);
            return;
        }
        if(args.Contains("--gpu-probe")) { using(var sensor=new NvidiaTemperature())Console.WriteLine("NVIDIA temperature: "+sensor.Read()); return; }
        if(args.Contains("--preview")||args.Contains("--preview-fans")||args.Contains("--preview-advanced")) {
            var sample=new FakeHardware();string vendor=LaptopCapabilities.CpuVendor;
            if(args.Contains("--sample-intel"))vendor="GenuineIntel";
            if(args.Contains("--sample-legacy")) {
                vendor="AuthenticAMD";foreach(uint id in Id.Power)sample.Values.Remove(id);
                sample.Values[Id.CpuPpt]=35;sample.Values[Id.TotalPpt]=65;sample.Values[Id.Mux]=-1;
                sample.UnsupportedCurves.Add(Id.Curves[2]);sample.Values[Id.Fans[2]]=-1;
            }
            if(args.Contains("--sample-unavailable")) {
                foreach(uint id in Id.Power)sample.Values.Remove(id);
                foreach(uint id in Id.Curves)sample.UnsupportedCurves.Add(id);
                sample.Values[Id.Mux]=-1;sample.Values[Id.Eco]=-1;
            }
            float previewScale=args.Contains("--scale200")?2f:args.Contains("--scale150")?1.5f:1f;
            using(var form=new MainForm(sample,new Preferences(),true,vendor,previewScale)) {
                form.Show(); Application.DoEvents();
                if(args.Contains("--preview-fans"))form.ToggleFans();
                if(args.Contains("--preview-advanced")) {form.ToggleFans();form.fansPanel.ToggleAdvanced();}
                Application.DoEvents();
                using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size)); bitmap.Save(args.Last()); }
            }
            return;
        }
        if(args.Contains("--screen-probe")) {
            string name=Display.InternalPanel();Console.WriteLine("Internal panel: "+(name??"unavailable"));
            if(name!=null)Console.WriteLine("Refresh rates: "+string.Join(", ",Display.Rates(name)));
            return;
        }
        if(args.Contains("--probe")) {
            try { using(var hw=new AsusHardware()) Console.WriteLine("ASUS driver reachable; performance="+hw.Get(Id.Mode)+" eco="+hw.Get(Id.Eco)+" mux="+hw.Get(Id.Mux)); }
            catch(Exception ex) { Console.WriteLine(ex.Message); Environment.ExitCode=1; }
            return;
        }
        bool admin=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        if(!admin) {
            if(args.Contains(ElevatedLaunch.Arguments)) {MessageBox.Show("An administrator account is required for the approved launcher.","ASUS Utility LITE");return;}
            bool special=args.Contains("--diagnostics")||args.Contains("--background")||args.Contains("--setup-launcher");
            if(!special&&ElevatedLaunch.TryRun())return;
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath,args.Contains("--setup-launcher")?"--setup-launcher":args.Contains("--diagnostics")?"--diagnostics":args.Contains("--background")?"--background":"") { UseShellExecute=true,Verb="runas" }); }
            catch(Exception ex) { MessageBox.Show(ex.Message,"ASUS Utility LITE"); }
            return;
        }
        if(args.Contains("--diagnostics")) {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ASUS Utility LITE Diagnostics.txt");
            var report=new System.Text.StringBuilder("ASUS Utility LITE "+Application.ProductVersion+" — read-only diagnostic\r\n"+DateTime.Now+"\r\n");
            report.AppendLine("Model: "+Convert.ToString(Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS","SystemProductName","Unknown")));
            report.AppendLine("CPU vendor: "+LaptopCapabilities.CpuVendor);
            try { using(var hw=new AsusHardware()) {
                uint[] ids=new[]{Id.Mode,Id.Eco,Id.Mux,Id.Battery,Id.CpuTemp,Id.GpuTemp,Id.DisplayConnect,Id.CpuPpt,Id.TotalPpt}.Concat(Id.Fans).Concat(Id.Power).ToArray();
                foreach(uint id in ids) {
                    try {byte[] raw=hw.Read(id,0);report.AppendLine("0x"+id.ToString("X8")+" raw="+BitConverter.ToString(raw)+" value="+(BitConverter.ToInt32(raw,0)-65536));}
                    catch(Exception ex) {report.AppendLine(ex.Message);}
                }
                foreach(uint id in Id.Curves)for(int profile=0;profile<3;profile++) {
                    try {report.AppendLine("Curve 0x"+id.ToString("X8")+" argument="+profile+" raw="+BitConverter.ToString(hw.Read(id,profile)));}
                    catch(Exception ex) {report.AppendLine(ex.Message);}
                }
            }} catch(Exception ex) {report.AppendLine(ex.Message);}
            try { report.AppendLine("Screen: "+Display.InternalPanel()); } catch(Exception ex) {report.AppendLine(ex.Message);}
            using(var sensor=new NvidiaTemperature())report.AppendLine("NVIDIA temperature (°C, -1=unavailable): "+sensor.Read());
            var batterySensor=new BatteryTelemetry();var batteryReading=batterySensor.Read();
            report.AppendLine(batteryReading==null?"Battery telemetry unavailable":batteryReading.PowerText+"; "+batteryReading.RemainingText(SystemInformation.PowerStatus.BatteryLifeRemaining)+"; absolute="+batteryReading.Absolute);
            try {File.WriteAllText(path,report.ToString());MessageBox.Show("Saved read-only diagnostics to:\n"+path,"ASUS Utility LITE");}
            catch(Exception ex) {MessageBox.Show(report+"\nCould not save: "+ex.Message,"ASUS Utility LITE");}
            return;
        }
        try {ElevatedLaunch.Ensure();ElevatedLaunch.RecordSetup(null);}
        catch(Exception ex) {
            string log=ElevatedLaunch.RecordSetup(ex);
            MessageBox.Show("The approved launcher could not be registered. Windows may ask for approval again next time.\n\n"+ex.Message+
                "\nError: 0x"+ex.HResult.ToString("X8")+(log==null?"":"\nDetails saved to:\n"+log),"ASUS Utility LITE");
            if(args.Contains("--setup-launcher")){Environment.ExitCode=1;return;}
        }
        if(args.Contains("--setup-launcher")) {MessageBox.Show("Launcher approved and verified. Future normal launches from this location will use the approved task.","ASUS Utility LITE");return;}
        bool created;
        using(var mutex=new Mutex(true,"Local\\ASUSUtilityLite-"+WindowsIdentity.GetCurrent().User.Value,out created)) {
            if(!created) {if(!args.Contains("--background")&&!WindowActivation.Open())MessageBox.Show("ASUS Utility LITE is starting. Open it from the system tray.");return;}
            try { using(var hw=new AsusHardware()) {
                try {Startup.MigrateLegacy();}catch(Exception ex){MessageBox.Show("Startup entry could not be migrated. Re-enable Run on startup in the app.\n"+ex.Message,"ASUS Utility LITE");}
                var form=new MainForm(hw,Preferences.Load(),false);
                if(args.Contains("--background")) form.Shown+=(s,e)=>form.Hide();
                Application.Run(form);
            }} catch(Exception ex) { MessageBox.Show(ex.Message,"ASUS Utility LITE",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        }
    }
}
public class MainForm : Form {
    internal readonly IHardware Hw;
    internal readonly string CpuVendor;
    internal float UiScale=1;
    readonly Preferences prefs; readonly bool preview;
    readonly Color accent=Color.FromArgb(76,198,238), card=Color.FromArgb(43,45,49);
    readonly Label cpu=new Label(),gpu=new Label(),mid=new Label(),charge=new Label(),status=new Label(),modeTitle=new Label(),gpuTitle=new Label(),screenTitle=new Label(),limitTitle=new Label();
    readonly Button[] modeButtons=new Button[3],gpuButtons=new Button[4],screenButtons=new Button[3];
    readonly TrackBar limit=new PointerTrackBar(); readonly CheckBox startup=new CheckBox();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer powerTimer=new System.Windows.Forms.Timer();
    readonly PowerTransitions powerTransitions=new PowerTransitions();
    readonly SessionOverrides sessionOverrides=new SessionOverrides();
    readonly NvidiaTemperature nvidia=new NvidiaTemperature();
    readonly BatteryTelemetry batteryTelemetry=new BatteryTelemetry();
    readonly Label batteryPower=new Label(),batteryTime=new Label();
    internal FansPanel fansPanel; Button fansToggle; bool fansExpanded;
    internal readonly VerticalScrollPanel contentHost=new VerticalScrollPanel();
    NotifyIcon tray; bool quitting,initializing=true,paused; string panel;
    int mode {get {return prefs.LastPerformanceMode>=0&&prefs.LastPerformanceMode<=2?prefs.LastPerformanceMode:preview?1:-1;}}
    public MainForm(IHardware hardware,Preferences settings,bool demo,string cpuVendor=null,float previewScale=0) {
        SuspendLayout();
        Hw=hardware; prefs=settings; preview=demo; CpuVendor=cpuVendor??LaptopCapabilities.CpuVendor;
        Text="ASUS Utility LITE"; ClientSize=new Size(414,390); FormBorderStyle=FormBorderStyle.FixedSingle; AutoScroll=false;
        MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(27,29,32); ForeColor=Color.WhiteSmoke;
        Font=new Font("Segoe UI",9); AutoScaleMode=AutoScaleMode.None; Icon=LoadAppIcon();
        var title=LabelAt("ASUS UTILITY LITE",18,17,240,22); title.Font=new Font(Font.FontFamily,11,FontStyle.Bold);
        string model="ASUS laptop";
        try { model=Convert.ToString(Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS","SystemProductName",model)); } catch {}
        LabelAt(model,18,43,378,20).ForeColor=Color.Silver;
        AddLabel(modeTitle,"Performance",18,73,130,20); modeTitle.Font=new Font(Font,FontStyle.Bold);
        AddLabel(cpu,"CPU —",151,73,245,20); cpu.TextAlign=ContentAlignment.MiddleRight;
        string[] modes={"Silent","Balanced","Turbo"};
        for(int i=0;i<3;i++) { int v=i; modeButtons[i]=ButtonAt(modes[i],18+i*129,97,120,30,()=>ChangeMode(v==0?2:v==1?0:1)); }
        AddLabel(gpuTitle,"GPU mode",18,140,210,20); gpuTitle.Font=new Font(Font,FontStyle.Bold);
        AddLabel(gpu,"GPU —",224,140,172,20); gpu.TextAlign=ContentAlignment.MiddleRight;
        string[] gpus={"Eco","Standard","Ultimate","Optimized"};
        for(int i=0;i<4;i++) { int v=i; gpuButtons[i]=ButtonAt(gpus[i],18+i*96,164,90,30,()=>ChangeGpu(v)); }
        AddLabel(screenTitle,"Screen",18,207,220,20); screenTitle.Font=new Font(Font,FontStyle.Bold);
        AddLabel(mid,"Mid fan —",240,207,156,20); mid.TextAlign=ContentAlignment.MiddleRight;
        screenButtons[0]=ButtonAt("Auto",18,231,120,28,()=>SetScreen(0));
        screenButtons[1]=ButtonAt("60 Hz",147,231,120,28,()=>SetScreen(60));
        screenButtons[2]=ButtonAt("120 Hz",276,231,120,28,()=>SetScreen(120));
        AddLabel(limitTitle,"Battery charge limit",18,272,280,20); limitTitle.Font=new Font(Font,FontStyle.Bold);
        // TrackBar.AutoSize otherwise restores a taller native height, allowing
        // its background to cover the top of the startup/charge row below it.
        limit.AutoSize=false; limit.SetBounds(15,296,386,25); limit.Minimum=40; limit.Maximum=100; limit.TickStyle=TickStyle.None; limit.SmallChange=5; limit.LargeChange=10;
        limit.Value=Math.Max(40,Math.Min(100,prefs.ChargeLimit)); limit.ValueChanged+=(s,e)=>limitTitle.Text="Battery charge limit: "+limit.Value+"%";
        limit.MouseUp+=(s,e)=>ApplyLimit(); limit.KeyUp+=(s,e)=> { if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right||e.KeyCode==Keys.Home||e.KeyCode==Keys.End||e.KeyCode==Keys.PageUp||e.KeyCode==Keys.PageDown) ApplyLimit(); };
        Controls.Add(limit); limitTitle.Text="Battery charge limit: "+limit.Value+"%";
        startup.Text="Run on startup"; startup.SetBounds(18,324,180,22); startup.Checked=!preview&&Startup.Enabled();
        startup.CheckedChanged+=(s,e)=> { if(initializing)return; Run(()=>Startup.Set(startup.Checked)); initializing=true; startup.Checked=Startup.Enabled(); initializing=false; }; Controls.Add(startup);
        AddLabel(charge,"Charge: —",235,324,161,22); charge.TextAlign=ContentAlignment.MiddleRight;
        AddLabel(batteryPower,"Battery power: —",18,351,192,22);
        AddLabel(batteryTime,"Est. remaining: —",212,351,184,22);batteryTime.TextAlign=ContentAlignment.MiddleRight;
        fansToggle=ButtonAt("Fans + Power   ▸",18,382,378,28,ToggleFans); fansToggle.TextAlign=ContentAlignment.MiddleLeft;
        AddLabel(status,"",18,417,378,20); status.ForeColor=Color.Silver; status.Font=new Font(Font.FontFamily,8);
        ClientSize=new Size(414,443);
        foreach(Control control in Controls.Cast<Control>().ToArray())contentHost.Content.Controls.Add(control);
        contentHost.Dock=DockStyle.Fill;Controls.Add(contentHost);
        if(!preview) {
            var menu=new ContextMenuStrip(); menu.Items.Add("Open",null,(s,e)=> { Show(); WindowState=FormWindowState.Normal; Activate(); RefreshState(); });
            menu.Items.Add("About / license",null,(s,e)=>MessageBox.Show("ASUS Utility LITE "+Application.ProductVersion+"\nModified independent app using G-Helper ASUS protocol code.\nGPL-3.0; no warranty. Source and LICENSE.txt are included.\nhttps://github.com/seerge/g-helper", "About"));
            menu.Items.Add("Exit",null,(s,e)=> { quitting=true; Close(); });
            tray=new NotifyIcon { Icon=Icon,Text="ASUS Utility LITE",Visible=true,ContextMenuStrip=menu };
            tray.DoubleClick+=(s,e)=> { Show(); Activate(); RefreshState(); };
        }
        ResumeLayout(true);
        using(var graphics=CreateGraphics()) {
            float nativeScale=graphics.DpiX/96f;
            UiScale=preview&&previewScale>0?previewScale:nativeScale;
            if(preview&&previewScale>0)ScalePreviewFonts(this,UiScale/nativeScale);
        }
        Scale(new SizeF(UiScale,UiScale));
        contentHost.SetContentHeight(contentHost.ClientSize.Height);
        powerTimer.Interval=2000;powerTimer.Tick+=(s,e)=>RestoreAfterPowerChange();
        initializing=false; RefreshState();
        timer.Interval=3000; timer.Tick+=(s,e)=> { timer.Interval=Visible?3000:30000; RefreshState(); }; if(!preview)timer.Start();
        SystemEvents.PowerModeChanged+=OnPower;
        if(prefs.WindowPositionSaved) {
            StartPosition=FormStartPosition.Manual;
            Load+=(s,e)=>Location=WindowPlacement.Restore(new Point(prefs.WindowX,prefs.WindowY),Size,Screen.AllScreens.Select(screen=>screen.WorkingArea).ToArray());
        }
        ResizeEnd+=(s,e)=>SaveWindowPosition();
        Shown+=(s,e)=> { if(!preview) Run(()=> { Guard(); if(prefs.ChargeConfigured) Hw.Write(Id.Battery,prefs.ChargeLimit); ApplyAutomatic(); }); };
        FormClosing+=(s,e)=> {SaveWindowPosition(); if(!quitting&&!preview&&e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; Hide(); timer.Interval=30000; } };
    }
    void SaveWindowPosition() {
        if(preview||initializing||!IsHandleCreated)return;
        Point location=WindowState==FormWindowState.Normal?Location:RestoreBounds.Location;
        try {prefs.RememberWindow(location);}
        catch(Exception ex) {status.Text="Could not save window position: "+ex.Message;}
    }
    Label LabelAt(string text,int x,int y,int w,int h) { var l=new Label(); AddLabel(l,text,x,y,w,h); return l; }
    static void ScalePreviewFonts(Control control,float ratio) {
        foreach(Control child in control.Controls)ScalePreviewFonts(child,ratio);
        control.Font=new Font(control.Font.FontFamily,control.Font.Size*ratio,control.Font.Style);
    }
    static Icon LoadAppIcon() {
        using(var stream=typeof(MainForm).Assembly.GetManifestResourceStream("ASUSUtilityLite.AppIcon")) {
            if(stream==null)return (Icon)SystemIcons.Application.Clone();
            using(var icon=new Icon(stream))return (Icon)icon.Clone();
        }
    }
    void AddLabel(Label l,string text,int x,int y,int w,int h) { l.Text=text; l.SetBounds(x,y,w,h); Controls.Add(l); }
    Button ButtonAt(string text,int x,int y,int w,int h,Action action) {
        var b=new Button { Text=text,BackColor=card,ForeColor=ForeColor,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,UseVisualStyleBackColor=false };
        b.FlatAppearance.BorderColor=Color.FromArgb(65,68,74); b.SetBounds(x,y,w,h); b.Click+=(s,e)=>Run(action); Controls.Add(b); return b;
    }
    void Select(Button b,bool selected) { b.FlatAppearance.BorderColor=selected?accent:Color.FromArgb(65,68,74); b.FlatAppearance.BorderSize=selected?2:1; b.BackColor=selected?Color.FromArgb(31,64,76):card; }
    internal void Guard() { if(preview) throw new InvalidOperationException("Preview mode does not change hardware."); if(OtherHelper()) throw new InvalidOperationException("Exit G-Helper before using Lite, so the apps do not overwrite each other's settings."); }
    bool OtherHelper() { return Process.GetProcessesByName("GHelper").Length>0 || Process.GetProcessesByName("G-Helper").Length>0 || Process.GetProcessesByName("GHelperLite").Length>0; }
    internal void Run(Action action) {
        try { action(); status.Text="Ready"; RefreshState(); }
        catch(Exception ex) { status.Text=ex.Message; if(!preview) MessageBox.Show(this,ex.Message,"ASUS Utility LITE",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    }
    static string Temperature(int n) { return n>0&&n<130?n+"°C":"—"; }
    static string Fan(int raw) { int n=raw&65535; return raw>=0&&n<=120?n+"%":"—"; }
    void RefreshState() {
        if(!preview)ObservePower(SystemInformation.PowerStatus.PowerLineStatus);
        // Windows battery status is independent of ASUS ACPI availability.
        float battery=SystemInformation.PowerStatus.BatteryLifePercent;
        charge.Text=battery>=0&&battery<=1?"Charge: "+(battery*100).ToString("0.0")+"%":"Charge unavailable";
        var reading=preview?new BatteryReading {State=2,Capacity=44000,Rate=-11000,Absolute=true}:batteryTelemetry.Read();
        batteryPower.Text=reading==null?"Battery power: —":reading.PowerText;
        batteryTime.Text=reading==null?(SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Offline?new BatteryReading {State=2,Rate=int.MinValue,Capacity=uint.MaxValue}.RemainingText(SystemInformation.PowerStatus.BatteryLifeRemaining):"Est. remaining: —"):reading.RemainingText(SystemInformation.PowerStatus.BatteryLifeRemaining);
        try {
            paused=!preview&&OtherHelper();
            // DSTS is not a reliable current-profile readback (this laptop
            // returns 3). Only display a profile selected successfully in Lite.
            string[] names={"Balanced","Turbo","Silent"}; modeTitle.Text=mode>=0?"Last set: "+names[mode]:"Select a mode";
            for(int i=0;i<3;i++) Select(modeButtons[i],mode==(i==0?2:i==1?0:1));
            cpu.Text="CPU "+Temperature(Hw.Get(Id.CpuTemp))+" · Fan "+Fan(Hw.Get(Id.Fans[0]));
            int gpuTemperature=Hw.Get(Id.GpuTemp);
            if(!preview&&Visible&&gpuTemperature<1&&Hw.Get(Id.Eco)!=1)gpuTemperature=nvidia.Read();
            gpu.Text=Temperature(gpuTemperature)+" · Fan "+Fan(Hw.Get(Id.Fans[1]));
            int middleFan=Hw.Get(Id.Fans[2]);mid.Text=middleFan>=0?"Mid fan "+Fan(middleFan):"";
            int mux=Hw.Get(Id.Mux),eco=Hw.Get(Id.Eco),actual=mux==0?2:eco==1?0:eco==0?1:-1;
            gpuTitle.Text="GPU: "+LaptopCapabilities.GpuName(mux,eco);
            for(int i=0;i<4;i++) { Select(gpuButtons[i],prefs.Optimized&&LaptopCapabilities.GpuAvailable(3,mux,eco)?i==3:i==actual); gpuButtons[i].Enabled=!paused&&LaptopCapabilities.GpuAvailable(i,mux,eco); }
            if(!preview) panel=Display.InternalPanel();
            int hz=preview?120:panel==null?0:Display.Current(panel).frequency;
            int[] rates=preview?new[]{60,120}:Display.Rates(panel);
            int max=rates.Length==0?120:rates.Max(); screenButtons[2].Text=max+" Hz"; screenButtons[2].Tag=max;
            screenTitle.Text=hz>0?"Screen: "+hz+" Hz":"Screen unavailable";
            for(int i=0;i<3;i++) { int target=i==1?60:max; Select(screenButtons[i],prefs.AutoScreen?i==0:i>0&&hz==target); screenButtons[i].Enabled=!paused&&(i==0?rates.Length>0:rates.Contains(target)); }
            // Charge-limit writes are supported on the target GA403 even when
            // firmware has no readable charge-limit endpoint.
            limit.Enabled=!paused;
            // DSTS for performance mode is absent on some ROG firmware even
            // though DEVS supports changing the profile. Display the last
            // successful selection saved by Lite, not ambiguous DSTS data.
            foreach(var b in modeButtons)b.Enabled=!paused;
            if(preview)status.Text="Preview · sample readings · hardware changes disabled";
            else if(paused)status.Text="Paused — exit G-Helper to enable controls";
            else if(status.Text.StartsWith("Paused")) status.Text="Ready";
        } catch(Exception ex) { status.Text=ex.Message; }
    }
    internal void ToggleFans() {
        if(fansPanel==null) { fansPanel=new FansPanel(this,mode); fansPanel.Location=new Point((int)(18*UiScale),(int)(417*UiScale)); fansPanel.SizeChanged+=(s,e)=>LayoutFans(); contentHost.Content.Controls.Add(fansPanel); }
        fansExpanded=!fansExpanded; fansPanel.Visible=fansExpanded;
        LayoutFans();
    }
    void LayoutFans() {
        Point anchor=Location;
        bool expanded=fansExpanded;
        limit.Width=(int)((expanded?381:386)*UiScale);
        fansToggle.Text="Fans + Power   "+(expanded?"▾":"▸");
        status.Top=expanded?fansPanel.Bottom+(int)(7*UiScale):(int)(417*UiScale);
        int contentHeight=expanded?status.Bottom+(int)(8*UiScale):contentHost.ClientSize.Height;
        contentHost.SetContentHeight(contentHeight);
        contentHost.ScrollTo(expanded?fansToggle.Top:0);
        Location=anchor;
    }
    internal void ChangeMode(int value) { Guard(); prefs.SelectPerformanceMode(Hw,value,sessionOverrides.Clear); if(fansPanel!=null)fansPanel.SetMode(value); }
    internal void ApplyFanCurve(uint id,byte[] curve) {Guard();sessionOverrides.ApplyCurve(Hw,id,curve);}
    internal void ApplyPowerLimit(uint id,int value) {Guard();sessionOverrides.ApplyPower(Hw,id,value);}
    void ApplyLimit() { Run(()=> { Guard(); Hw.Write(Id.Battery,limit.Value); prefs.ChargeLimit=limit.Value; prefs.ChargeConfigured=true; prefs.Save(); }); }
    void ChangeGpu(int target) {
        Guard(); int mux=Hw.Get(Id.Mux),eco=Hw.Get(Id.Eco);
        if(!LaptopCapabilities.GpuAvailable(target,mux,eco))throw new InvalidOperationException("This GPU mode is unavailable on this laptop.");
        if(mux==0 || target==2) {
            if(mux==0&&target==2)return;
            if(MessageBox.Show(this,"Save your work first. Changing Ultimate mode requires a restart.\n\nApply the change and restart now?","GPU mode",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            if(target==2&&LaptopCapabilities.Binary(eco))Hw.EcoMode(false);
            Hw.Write(Id.Mux,target==2?0:1);
            prefs.Optimized=target==3; prefs.Save();
            // Windows gets a normal, non-forced restart request so unsaved apps can intervene.
            Process.Start("shutdown.exe","/r /t 0"); return;
        }
        if(target==0 && MessageBox.Show(this,"Eco disables the dedicated GPU. Close games and other GPU apps first.\n\nContinue?","Eco mode",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
        if(target==3) {
            prefs.Optimized=true;
            try { ApplyAutomatic(); prefs.Save(); } catch { prefs.Optimized=false; throw; }
        } else { Hw.EcoMode(target==0); prefs.Optimized=false; prefs.Save(); }
    }
    void SetScreen(int hz) { Guard(); panel=Display.InternalPanel(); int[] rates=Display.Rates(panel);
        bool automatic=hz==0;
        if(hz==120 && screenButtons[2].Tag!=null) hz=(int)screenButtons[2].Tag;
        if(automatic)hz=SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Online?rates.DefaultIfEmpty(60).Max():rates.Contains(60)?60:rates.DefaultIfEmpty(60).Min();
        Display.Set(panel,hz); prefs.AutoScreen=automatic; prefs.Save();
    }
    void ApplyAutomatic() {
        Guard(); var power=SystemInformation.PowerStatus.PowerLineStatus;
        if(power==PowerLineStatus.Unknown)return;
        bool plugged=power==PowerLineStatus.Online;
        var errors=new System.Collections.Generic.List<string>();
        if(prefs.Optimized)PowerRestore.Attempt(errors,"GPU",()=> {if(Hw.Get(Id.Mux)!=0&&LaptopCapabilities.Binary(Hw.Get(Id.Eco)))Hw.EcoMode(!plugged);});
        if(prefs.AutoScreen)PowerRestore.Attempt(errors,"Screen",()=> {string name=Display.InternalPanel();int[] rates=Display.Rates(name);if(rates.Length>0)Display.Set(name,plugged?rates.Max():rates.Contains(60)?60:rates.Min());});
        if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
    }
    void ObservePower(PowerLineStatus power,bool resumed=false) {
        if(powerTransitions.Observe(power,resumed)) {powerTimer.Stop();powerTimer.Start();}
    }
    void RestoreAfterPowerChange() {
        powerTimer.Stop();if(preview||IsDisposed)return;
        var power=SystemInformation.PowerStatus.PowerLineStatus;
        if(power==PowerLineStatus.Unknown) {powerTransitions.Observe(power,true);return;}
        if(powerTransitions.Observe(power)) {powerTimer.Start();return;}
        string[] errors;
        try {Guard();errors=PowerRestore.Apply(Hw,prefs,sessionOverrides,ApplyAutomatic);}
        catch(Exception ex) {errors=new[]{ex.Message};}
        status.Text=errors.Length==0?"Settings restored — "+(power==PowerLineStatus.Online?"AC power":"battery"):"Could not restore all settings; see Power restore.log";
        if(!preview)try {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Power restore.log");
            if(File.Exists(path)&&new FileInfo(path).Length>32768)File.WriteAllText(path,"");
            File.AppendAllText(path,DateTime.Now.ToString("s")+" source="+power+" selected mode="+prefs.LastPerformanceMode+" "+(errors.Length==0?"restored":string.Join("; ",errors))+Environment.NewLine);
        } catch {}
        RefreshState();
    }
    void OnPower(object sender,PowerModeChangedEventArgs e) {
        if(preview||!IsHandleCreated||IsDisposed)return;
        try {BeginInvoke((Action)(()=> {
            if(IsDisposed)return;
            if(e.Mode==PowerModes.Suspend) {powerTimer.Stop();return;}
            ObservePower(SystemInformation.PowerStatus.PowerLineStatus,e.Mode==PowerModes.Resume);RefreshState();
        }));}catch(InvalidOperationException) {}
    }
    protected override void WndProc(ref Message message) {
        if(WindowActivation.Message!=0&&message.Msg==(int)WindowActivation.Message) {
            Show();WindowState=FormWindowState.Normal;Activate();RefreshState();return;
        }
        base.WndProc(ref message);
    }
    protected override void Dispose(bool disposing) { if(disposing) { SystemEvents.PowerModeChanged-=OnPower; timer.Dispose();powerTimer.Dispose(); nvidia.Dispose(); if(tray!=null)tray.Dispose(); } base.Dispose(disposing); }
}
}
