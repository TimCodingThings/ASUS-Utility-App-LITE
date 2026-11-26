using System;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;
namespace ASUSUtilityLite {
// Read-only evidence around a command, never a firmware-mode verifier.
internal sealed class ProfileDiagnostics : IDisposable {
    readonly IHardware hw;
    readonly Timer timer=new Timer();
    int requested,ticks;string reason;
    internal ProfileDiagnostics(IHardware hardware) {
        hw=hardware;timer.Interval=2000;
        timer.Tick+=(s,e)=> {ticks++;if(ticks==1||ticks==4)Snapshot("sample "+(ticks*2)+"s");if(ticks>=4)timer.Stop();};
    }
    internal void Begin(int mode,string source) {
        if(!(hw is AsusHardware))return;
        timer.Stop();requested=mode;reason=source;ticks=0;
        Snapshot("before command",true);
    }
    internal void Complete(bool accepted) {
        if(!(hw is AsusHardware))return;
        Snapshot(accepted?"driver accepted":"command failed");
        if(accepted)timer.Start();
    }
    void Snapshot(string phase,bool services=false) {
        try {
            var text=new StringBuilder();
            text.AppendLine(DateTime.Now.ToString("s")+" version="+Application.ProductVersion+" reason="+reason+" requested="+requested+" phase="+phase+" source="+SystemInformation.PowerStatus.PowerLineStatus+" actual_mode=unverified");
            foreach(uint id in new[]{Id.CpuTemp,Id.CpuPpt,Id.TotalPpt,Id.Fans[0],Id.Fans[1],Id.Fans[2]}) {
                try {text.AppendLine("  read 0x"+id.ToString("X8")+"="+hw.Get(id));}
                catch(Exception ex) {text.AppendLine("  read 0x"+id.ToString("X8")+" unavailable: "+ex.Message);}
            }
            if(services) {
                try {
                    foreach(var service in ServiceController.GetServices())using(service) {
                        string name=service.ServiceName;
                        if(name.IndexOf("asus",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("armoury",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("rog",StringComparison.OrdinalIgnoreCase)<0)continue;
                        try {text.AppendLine("  service "+name+"="+service.Status);}
                        catch(Exception ex) {text.AppendLine("  service "+name+" unavailable: "+ex.Message);}
                    }
                }catch(Exception ex) {text.AppendLine("  services unavailable: "+ex.Message);}
            }
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Profile diagnostics.log");
            if(File.Exists(path)&&new FileInfo(path).Length>65536)File.WriteAllText(path,"");
            File.AppendAllText(path,text.ToString());
        }catch { /* Diagnostics must never change command success or interrupt controls. */ }
    }
    public void Dispose() {timer.Dispose();}
}
}
