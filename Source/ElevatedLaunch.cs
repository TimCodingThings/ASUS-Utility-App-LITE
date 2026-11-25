using System;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using System.Xml;
namespace ASUSUtilityLite {
// An on-demand, current-user task is authorized during the first elevated run.
// Its command and arguments are fixed; it is independent of logon startup.
internal static class ElevatedLaunch {
    internal const string Arguments="--scheduled-launch";
    static string Sid {get {return WindowsIdentity.GetCurrent().User.Value;}}
    internal static string Name {get {return "ASUS Utility LITE Launch-"+Sid;}}
    static dynamic Folder() {
        dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
        service.Connect();return service.GetFolder("\\");
    }
    internal static bool TryRun() {
        try {
            dynamic task=Folder().GetTask(Name);
            if(!task.Enabled||!Matches((string)task.Xml,Application.ExecutablePath,Sid))return false;
            task.Run(null);return true;
        } catch {return false;}
    }
    internal static void Ensure() {
        dynamic folder=Folder();
        EnsureTask(()=> (string)folder.GetTask(Name).Xml,
            ()=>folder.RegisterTask(Name,Definition(Application.ExecutablePath,Sid),6,Sid,null,3,null),Application.ExecutablePath,Sid);
    }
    internal static void EnsureTask(Func<string> read,Action register,string path,string sid) {
        string xml=null;
        try {xml=read();}
        catch(Exception ex) {
            // COM HRESULTs can become specialized managed exceptions, including
            // FileNotFoundException for a task that has never been registered.
            if(ex.HResult!=unchecked((int)0x80070002)&&ex.HResult!=unchecked((int)0x80070003))throw;
        }
        if(Matches(xml,path,sid))return;
        register();
        xml=read();string problem=ValidationError(xml,path,sid);
        if(problem!=null) {
            var error=new InvalidOperationException("Approved launch task validation failed: "+problem);
            error.Data["TaskXml"]=xml;throw error;
        }
    }
    internal static string RecordSetup(Exception error) {
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Launcher setup.log");
        try {
            File.WriteAllText(path,DateTime.Now.ToString("s")+" ASUS Utility LITE "+Application.ProductVersion+Environment.NewLine+
                (error==null?"Approved launch task registered and verified.":error.ToString())+Environment.NewLine+
                (error!=null&&error.Data.Contains("TaskXml")?"Task Scheduler returned:\r\n"+error.Data["TaskXml"]:"") );
            return path;
        } catch {return null;}
    }
    internal static string Definition(string path,string sid) {
        var text=new StringBuilder();
        using(var w=XmlWriter.Create(text,new XmlWriterSettings {OmitXmlDeclaration=true})) {
            w.WriteStartElement("Task","http://schemas.microsoft.com/windows/2004/02/mit/task");w.WriteAttributeString("version","1.2");
            w.WriteStartElement("RegistrationInfo");w.WriteElementString("Description","ASUS Utility LITE: approved on-demand launch for this user");w.WriteEndElement();
            w.WriteStartElement("Triggers");w.WriteEndElement();
            w.WriteStartElement("Principals");w.WriteStartElement("Principal");w.WriteAttributeString("id","User");
            w.WriteElementString("UserId",sid);w.WriteElementString("LogonType","InteractiveToken");w.WriteElementString("RunLevel","HighestAvailable");w.WriteEndElement();w.WriteEndElement();
            w.WriteStartElement("Settings");
            w.WriteElementString("MultipleInstancesPolicy","Parallel");
            w.WriteElementString("DisallowStartIfOnBatteries","false");w.WriteElementString("StopIfGoingOnBatteries","false");
            w.WriteElementString("AllowStartOnDemand","true");w.WriteElementString("Enabled","true");w.WriteElementString("ExecutionTimeLimit","PT0S");
            w.WriteEndElement();
            w.WriteStartElement("Actions");w.WriteAttributeString("Context","User");w.WriteStartElement("Exec");
            w.WriteElementString("Command",Path.GetFullPath(path));w.WriteElementString("Arguments",Arguments);
            w.WriteElementString("WorkingDirectory",Path.GetDirectoryName(Path.GetFullPath(path)));
            w.WriteEndElement();w.WriteEndElement();w.WriteEndElement();
        }
        return text.ToString();
    }
    internal static bool Matches(string xml,string path,string sid) {
        return ValidationError(xml,path,sid)==null;
    }
    internal static string ValidationError(string xml,string path,string sid,Func<string,string> resolveAccount=null) {
        try {
            var doc=new XmlDocument {XmlResolver=null};doc.LoadXml(xml);
            var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
            Func<string,string> value=x=> {var node=doc.SelectSingleNode("/t:Task/"+x,ns);return node==null?null:node.InnerText.Trim();};
            if(doc.SelectNodes("/t:Task/t:Actions/*",ns).Count!=1||doc.SelectSingleNode("/t:Task/t:Actions/t:Exec",ns)==null)return "Expected one executable action.";
            if(doc.SelectNodes("/t:Task/t:Triggers/*",ns).Count!=0)return "Expected an on-demand task without triggers.";
            if(doc.SelectNodes("/t:Task/t:Principals/t:Principal",ns).Count!=1)return "Expected one user principal.";
            string context=value("t:Actions/@Context");
            if(context!=null&&context!=value("t:Principals/t:Principal/@id"))return "Action context differs from the user principal.";
            if(!SamePath(value("t:Actions/t:Exec/t:Command"),path))return "Executable path differs from this app.";
            if(!SamePath(value("t:Actions/t:Exec/t:WorkingDirectory"),Path.GetDirectoryName(Path.GetFullPath(path))))return "Working directory differs from this app.";
            if(value("t:Actions/t:Exec/t:Arguments")!=Arguments)return "Launch arguments differ from the approved command.";
            string user=value("t:Principals/t:Principal/t:UserId");
            if(!string.Equals(user,sid,StringComparison.OrdinalIgnoreCase)) {
                if(string.IsNullOrEmpty(user)||user.StartsWith("S-1-",StringComparison.OrdinalIgnoreCase))return "Task belongs to a different Windows user.";
                // Scheduler may export an account name instead of its equivalent SID.
                if(resolveAccount==null)resolveAccount=name=>new NTAccount(name).Translate(typeof(SecurityIdentifier)).Value;
                if(!string.Equals(resolveAccount(user),sid,StringComparison.OrdinalIgnoreCase))return "Task account does not resolve to this Windows user.";
            }
            if(value("t:Principals/t:Principal/t:GroupId")!=null)return "Group principals are not supported.";
            if(value("t:Principals/t:Principal/t:LogonType")!="InteractiveToken")return "LogonType must be InteractiveToken.";
            if(value("t:Principals/t:Principal/t:RunLevel")!="HighestAvailable")return "RunLevel must be HighestAvailable.";
            if(value("t:Settings/t:MultipleInstancesPolicy")!="Parallel")return "MultipleInstancesPolicy must be Parallel.";
            // Honor XML Schema defaults and lexical equivalents, not literal strings.
            if(XmlConvert.ToBoolean(value("t:Settings/t:DisallowStartIfOnBatteries")??"true"))return "Task cannot start on battery.";
            if(XmlConvert.ToBoolean(value("t:Settings/t:StopIfGoingOnBatteries")??"true"))return "Task stops when switching to battery.";
            if(!XmlConvert.ToBoolean(value("t:Settings/t:AllowStartOnDemand")??"true"))return "On-demand start is disabled.";
            if(!XmlConvert.ToBoolean(value("t:Settings/t:Enabled")??"true"))return "Task is disabled.";
            if(XmlConvert.ToTimeSpan(value("t:Settings/t:ExecutionTimeLimit")??"PT72H")!=TimeSpan.Zero)return "Execution time limit is not unlimited.";
            return null;
        } catch(Exception ex) {return "Could not validate task definition: "+ex.Message;}
    }
    static bool SamePath(string first,string second) {
        if(string.IsNullOrEmpty(first)||string.IsNullOrEmpty(second))return false;
        return string.Equals(Path.GetFullPath(first).TrimEnd('\\','/'),Path.GetFullPath(second).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);
    }
}
internal static class WindowActivation {
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern uint RegisterWindowMessage(string name);
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr FindWindow(string className,string title);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    internal static readonly uint Message=RegisterWindowMessage("ASUSUtilityLite.OpenWindow.v1");
    internal static bool Open() {
        // Both sender and receiver are elevated; no UAC message-filter changes.
        for(int attempt=0;attempt<40;attempt++) {
            IntPtr window=FindWindow(null,"ASUS Utility LITE");
            if(window!=IntPtr.Zero&&Message!=0)return PostMessage(window,Message,IntPtr.Zero,IntPtr.Zero);
            System.Threading.Thread.Sleep(50);
        }
        return false;
    }
}
}
