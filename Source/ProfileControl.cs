using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
namespace ASUSUtilityLite {
internal interface IProfileServices {
    bool Running(string name);
    void Stop(string name);
    void Start(string name);
}
internal sealed class ProfileServices : IProfileServices {
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenSCManager(string machine,string database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenService(IntPtr manager,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool ControlService(IntPtr service,uint control,out ServiceStatus status);
    [DllImport("advapi32.dll")]static extern bool CloseServiceHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)]struct ServiceStatus {public uint Type,State,Accepted,Win32Exit,SpecificExit,Checkpoint,WaitHint;}
    static bool Missing(Exception ex) {var native=ex as Win32Exception;return native!=null&&native.NativeErrorCode==1060||ex.InnerException!=null&&Missing(ex.InnerException);}
    public bool Running(string name) {
        try {using(var service=new ServiceController(name)) {
            var status=service.Status;
            if(status==ServiceControllerStatus.Running)return true;
            if(status==ServiceControllerStatus.Stopped||status==ServiceControllerStatus.Paused)return false;
            throw new InvalidOperationException(name+" is changing state. Try again when it has settled.");
        }}
        catch(Exception ex) {if(Missing(ex))return false;throw;}
    }
    public void Stop(string name) {
        // ControlService targets only this service. ServiceController.Stop()
        // also stops dependent services, which is outside Lite's scope.
        IntPtr manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            IntPtr service=OpenService(manager,name,0x20);if(service==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            try {ServiceStatus status;if(!ControlService(service,1,out status)) {int error=Marshal.GetLastWin32Error();if(error!=1062)throw new Win32Exception(error);}}
            finally {CloseServiceHandle(service);}
        }finally {CloseServiceHandle(manager);}
        using(var service=new ServiceController(name))service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(5));
    }
    public void Start(string name) {
        try {using(var service=new ServiceController(name)) {
            service.Refresh();if(service.Status==ServiceControllerStatus.StopPending)service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(5));
            service.Refresh();if(service.Status==ServiceControllerStatus.Running)return;
            if(service.Status==ServiceControllerStatus.Stopped)service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(5));
        }}catch(Exception ex) {if(!Missing(ex))throw;}
    }
}
// Records original running state before each stop, so a failed stop or an
// interrupted app can be recovered. Never changes service startup types.
internal sealed class ProfileControl {
    internal static readonly string[] Names={"ArmouryCrateControlInterface","ArmouryCrateService"};
    readonly IProfileServices services;readonly string journal;
    readonly HashSet<string> owned=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    internal ProfileControl(IProfileServices controller,string path) {
        services=controller;journal=path;
        if(File.Exists(journal))foreach(string name in File.ReadAllLines(journal))if(Names.Contains(name,StringComparer.OrdinalIgnoreCase))owned.Add(name);
    }
    void Save() {
        Directory.CreateDirectory(Path.GetDirectoryName(journal));
        string temp=journal+".tmp";File.WriteAllLines(temp,Names.Where(name=>owned.Contains(name)).ToArray());
        if(File.Exists(journal))File.Replace(temp,journal,null);else File.Move(temp,journal);
    }
    internal static bool? Conflict(Func<string,bool> running) {
        bool unknown=false;foreach(string name in Names)try {if(running(name))return true;}catch {unknown=true;}
        return unknown?(bool?)null:false;
    }
    internal bool? Conflict() {return Conflict(services.Running);}
    internal void Acquire() {
        try {
            foreach(string name in Names) {
                if(!services.Running(name))continue;
                owned.Add(name);Save();services.Stop(name);
            }
            if(Conflict()!=false)throw new InvalidOperationException("Armoury Crate services could not be confirmed stopped.");
        }catch(Exception original) {
            try {Release();}catch(Exception restore) {throw new InvalidOperationException(original.Message+" Restoration also failed: "+restore.Message,original);}
            throw;
        }
    }
    internal void Release() {
        var errors=new List<string>();
        foreach(string name in Names.Reverse()) {
            if(!owned.Contains(name))continue;
            try {services.Start(name);owned.Remove(name);try {Save();}catch {owned.Add(name);throw;}}
            catch(Exception ex) {errors.Add(name+": "+ex.Message);}
        }
        if(errors.Count>0)throw new InvalidOperationException("Could not restore Armoury Crate services: "+string.Join("; ",errors));
    }
}
}
