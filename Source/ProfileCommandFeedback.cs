using System;
using System.Windows.Forms;
namespace ASUSUtilityLite {
// Command acknowledgement is deliberately separate from hardware readback.
// The GA403UP performance DSTS response cannot establish the active mode.
internal sealed class ProfileCommandFeedback {
    string detail="Actual mode unavailable · no command sent this run";
    internal string Text {get {return detail;}}
    internal void Pending(PowerLineStatus source) {
        detail="Actual mode unavailable · "+(source==PowerLineStatus.Online?"AC":"battery")+" change; restore pending";
    }
    internal void Complete(int mode,bool accepted,DateTime time) {
        string[] names={"Balanced","Turbo","Silent"};
        string name=mode>=0&&mode<names.Length?names[mode]:"Profile";
        detail="Actual mode unavailable · "+name+" command "+(accepted?"accepted ":"failed ")+time.ToString("HH:mm:ss");
    }
    internal void NotSent() {detail="Actual mode unavailable · restore command not sent";}
}
}
