using System;
using System.Drawing;
using System.Windows.Forms;
namespace ASUSUtilityLite {
// A vertical-only viewport: its content can never create a horizontal scrollbar.
internal sealed class VerticalScrollPanel : Panel {
    internal readonly Panel Content=new Panel();
    readonly VScrollBar bar=new VScrollBar();
    int contentHeight=443,wheelRemainder;bool arranging;
    internal int Offset {get {return bar.Value;}}
    internal bool CanScroll {get {return bar.Visible;}}
    internal VerticalScrollPanel() {
        AutoScroll=false;Size=new Size(414,443);bar.Width=18;bar.Visible=false;
        Content.AutoScroll=false;Controls.Add(Content);Controls.Add(bar);
        bar.ValueChanged+=(s,e)=>Content.Top=-bar.Value;
        Content.ControlAdded+=(s,e)=>Wire(e.Control);
        Arrange();
    }
    void Wire(Control control) {
        control.Enter+=(s,e)=>EnsureVisible(control);
        control.ControlAdded+=(s,e)=>Wire(e.Control);
        foreach(Control child in control.Controls)Wire(child);
    }
    void EnsureVisible(Control control) {
        if(!control.Visible||!control.IsHandleCreated)return;
        Point position=Content.PointToClient(control.PointToScreen(Point.Empty));
        if(position.Y<Offset)ScrollTo(position.Y);
        else if(position.Y+control.Height>Offset+ClientSize.Height)ScrollTo(position.Y+control.Height-ClientSize.Height);
    }
    internal void SetContentHeight(int height) {contentHeight=Math.Max(0,height);Arrange();}
    internal void ScrollTo(int offset) {bar.Value=Math.Max(0,Math.Min(Math.Max(0,contentHeight-ClientSize.Height),offset));}
    internal void Wheel(int delta) {
        if(!CanScroll)return;
        wheelRemainder+=delta;int notches=wheelRemainder/120;wheelRemainder%=120;
        int lines=SystemInformation.MouseWheelScrollLines;
        int step=lines<0?ClientSize.Height:lines*Math.Max(1,Font.Height);
        ScrollTo(Offset-notches*step);
    }
    protected override void OnMouseWheel(MouseEventArgs e) {
        Wheel(e.Delta);var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
    }
    protected override void OnLayout(LayoutEventArgs e) {base.OnLayout(e);Arrange();}
    void Arrange() {
        if(arranging||Content==null||bar==null)return;
        arranging=true;
        try {
            bool scrolling=contentHeight>ClientSize.Height;int offset=Math.Min(bar.Value,Math.Max(0,contentHeight-ClientSize.Height));
            bar.Minimum=0;bar.Maximum=Math.Max(0,contentHeight-1);bar.LargeChange=Math.Max(1,ClientSize.Height);bar.SmallChange=Math.Max(1,Font.Height);
            bar.Visible=scrolling;bar.Value=offset;
            bar.SetBounds(Math.Max(0,ClientSize.Width-bar.Width),0,bar.Width,ClientSize.Height);
            Content.SetBounds(0,-offset,Math.Max(0,ClientSize.Width-(scrolling?bar.Width:0)),contentHeight);
        } finally {arranging=false;}
    }
}
internal static class WheelScroll {
    internal static void Forward(Control source,Message message) {
        if(message.Msg!=0x20A)return;
        int delta=(short)((message.WParam.ToInt64()>>16)&65535);
        for(Control parent=source.Parent;parent!=null;parent=parent.Parent) {
            var viewport=parent as VerticalScrollPanel;
            if(viewport!=null){viewport.Wheel(delta);return;}
        }
    }
}
}
