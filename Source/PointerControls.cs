using System;
using System.Windows.Forms;
namespace ASUSUtilityLite {
// Native TrackBar handles wheel input itself, so filter it before dispatch.
public class PointerTrackBar : TrackBar {
    protected override void WndProc(ref Message message) {
        int key=unchecked((int)message.WParam.ToInt64());
        if(message.Msg==0x20A||message.Msg==0x20E||
           ((message.Msg==0x100||message.Msg==0x101)&&key>=33&&key<=40)) {
            WheelScroll.Forward(this,message);
            message.Result=IntPtr.Zero;return;
        }
        base.WndProc(ref message);
    }
}
public class NoWheelNumber : NumericUpDown {
    protected override void OnMouseWheel(MouseEventArgs e) {
        var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
    }
    protected override void WndProc(ref Message message) {
        if(message.Msg==0x20A||message.Msg==0x20E){WheelScroll.Forward(this,message);message.Result=IntPtr.Zero;return;}
        base.WndProc(ref message);
    }
}
public class NoWheelComboBox : ComboBox {
    protected override void WndProc(ref Message message) {
        if(message.Msg==0x20A||message.Msg==0x20E){WheelScroll.Forward(this,message);message.Result=IntPtr.Zero;return;}
        base.WndProc(ref message);
    }
}
}
