using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace ASUSUtilityLite {
public class AdvancedFansPanel : UserControl {
    readonly MainForm main; int mode; readonly ComboBox fan=new NoWheelComboBox();
    readonly NumericUpDown[] temps=new NumericUpDown[8],speeds=new NumericUpDown[8],power=new NumericUpDown[3];
    readonly CheckBox[] powerEnabled=new CheckBox[3]; readonly Label result=new Label(); readonly Button apply=new Button();
    readonly LaptopCapabilities.PowerLimit[] limits;
    int[] curveFans=new int[0]; bool curveAvailable,loadingFans;
    public AdvancedFansPanel(MainForm owner,int currentMode) {
        SuspendLayout();
        main=owner; mode=currentMode;
        Size=new Size(378,334); BackColor=owner.BackColor; ForeColor=owner.ForeColor; Font=owner.Font;
        LabelAt("Fan curve",0,3,200,23).Font=new Font(Font,FontStyle.Bold);
        fan.SetBounds(250,0,128,25); fan.DropDownStyle=ComboBoxStyle.DropDownList;
        fan.BackColor=Color.White;fan.ForeColor=Color.Black;
        fan.SelectedIndexChanged+=(s,e)=>LoadCurve(); Controls.Add(fan);
        LabelAt("°C",0,37,45,22); LabelAt("Fan %",0,69,50,22);
        for(int i=0;i<8;i++) { temps[i]=Number(59+i*40,34,38,1,100); speeds[i]=Number(59+i*40,66,38,0,100); }
        apply.Text="Apply fan"; apply.SetBounds(250,101,128,28); apply.Click+=(s,e)=>Execute(()=> {
            byte[] curve=new byte[16]; for(int i=0;i<8;i++) {curve[i]=(byte)temps[i].Value;curve[i+8]=(byte)speeds[i].Value;}
            ValidateCurve(curve); main.Guard(); main.Hw.Set(Id.Curves[curveFans[fan.SelectedIndex]],curve);
            result.Text="Fan curve accepted. Other fans were unchanged.";
        }); Controls.Add(apply);
        var reset=new Button { Text="Restore firmware defaults" }; reset.SetBounds(0,101,220,28);
        reset.Click+=(s,e)=>Execute(()=> {main.Guard();if(mode<0)throw new InvalidOperationException("Select a performance mode in the main window first.");main.Hw.Write(Id.Mode,mode);LoadCurve();result.Text="Firmware performance profile reapplied.";}); Controls.Add(reset);
        LabelAt("CPU power limits",0,143,300,23).Font=new Font(Font,FontStyle.Bold);
        limits=LaptopCapabilities.PowerLimits(main.Hw,main.CpuVendor);
        for(int i=0;i<limits.Length;i++) {
            powerEnabled[i]=new CheckBox { Text=limits[i].Name }; powerEnabled[i].SetBounds(i*130,170,112,23); Controls.Add(powerEnabled[i]);
            int value=limits[i].Value; bool supported=value>=0;
            power[i]=Number(i*130,198,76,5,80); power[i].Value=Math.Max(5,Math.Min(80,value>0?value:35));
            powerEnabled[i].Enabled=supported; power[i].Enabled=supported;
            LabelAt(supported?"W":"—",82+i*130,201,35,23);
        }
        var powerApply=new Button { Text="Apply checked limits" }; powerApply.SetBounds(200,234,178,28);
        powerApply.Click+=(s,e)=>Execute(()=> {
            if(!powerEnabled.Any(c=>c!=null&&c.Checked))throw new InvalidOperationException("Check at least one limit to apply.");
            for(int i=0;i<limits.Length;i++)for(int j=i+1;j<limits.Length;j++)
                if(powerEnabled[i].Checked&&powerEnabled[j].Checked&&power[i].Value>power[j].Value)
                    throw new InvalidOperationException(limits[i].Name+" must not exceed "+limits[j].Name+".");
            main.Guard();
            for(int i=0;i<limits.Length;i++) if(powerEnabled[i].Checked) { main.Hw.Write(limits[i].Id,(int)power[i].Value); result.Text=limits[i].Name+" applied."; }
            result.Text="Selected power limits accepted.";
        }); Controls.Add(powerApply);
        powerApply.Enabled=limits.Length>0;
        if(limits.Length==0)LabelAt("Custom power limits unavailable on this laptop.",0,174,378,43).ForeColor=Color.Silver;
        LabelAt("Session settings · 5–80 W editing range\nOnly limits reported by this laptop are offered.",0,272,378,34).ForeColor=Color.Silver;
        result.SetBounds(0,310,378,20); result.ForeColor=Color.FromArgb(76,198,238); result.Font=new Font(Font.FontFamily,Font.Size*8f/9f); Controls.Add(result);
        RefreshFanChoices();
        AutoScaleMode=AutoScaleMode.None;ResumeLayout(true);Scale(new SizeF(main.UiScale,main.UiScale));
    }
    internal void SetMode(int value) {mode=value;foreach(var c in powerEnabled)if(c!=null)c.Checked=false;RefreshFanChoices();}
    void RefreshFanChoices() {
        int previous=fan.SelectedIndex>=0?curveFans[fan.SelectedIndex]:-1;
        loadingFans=true;
        try {
            curveFans=LaptopCapabilities.CurveFans(main.Hw,mode);fan.Items.Clear();
            string[] names={"CPU","GPU","Middle"};foreach(int index in curveFans)fan.Items.Add(names[index]);
            fan.Enabled=curveFans.Length>0;
            if(curveFans.Length>0)fan.SelectedIndex=Math.Max(0,Array.IndexOf(curveFans,previous));
        } finally {loadingFans=false;}
        LoadCurve();
    }
    void Execute(Action action) { try { action(); } catch(Exception ex) { MessageBox.Show(this,ex.Message+"\n\nIf some settings were accepted before the error, restore firmware defaults to reset them.","Fans + Power"); } }
    Label LabelAt(string text,int x,int y,int w,int h) { var l=new Label { Text=text }; l.SetBounds(x,y,w,h);Controls.Add(l);return l; }
    NumericUpDown Number(int x,int y,int w,int min,int max) { var n=new NoWheelNumber { Minimum=min,Maximum=max,BackColor=Color.FromArgb(43,45,49),ForeColor=ForeColor };n.SetBounds(x,y,w,25);Controls.Add(n);return n; }
    void LoadCurve() {
        if(loadingFans)return;
        curveAvailable=false;
        try {
            if(mode<0)throw new InvalidOperationException("Select a performance mode first.");
            if(fan.SelectedIndex<0)throw new InvalidOperationException("Fan curves unavailable.");
            byte[] firmware=LaptopCapabilities.ReadFanCurve(main.Hw,Id.Curves[curveFans[fan.SelectedIndex]],mode);
            byte[] curve=EditableCurve(firmware);
            for(int i=0;i<8;i++) { temps[i].Value=curve[i]; speeds[i].Value=curve[i+8]; }
            curveAvailable=true; result.Text=curve.SequenceEqual(firmware)?"Firmware curve loaded. Edit then apply.":"Firmware draft adjusted. Review before Apply.";
        } catch { result.Text=mode<0?"Select a performance mode first.":"No valid firmware curve available for this fan/profile."; }
        apply.Enabled=curveAvailable;
        foreach(var n in temps.Concat(speeds))n.Enabled=curveAvailable;
    }
    internal static void ValidateCurve(byte[] curve) {
        if(curve==null||curve.Length!=16)throw new InvalidOperationException("Fan curve must contain eight points.");
        for(int i=0;i<8;i++) {
            if(curve[i]<1||curve[i]>100||curve[i+8]>100 || (i>0&&(curve[i]<=curve[i-1]||curve[i+8]<curve[i+7])))
                throw new InvalidOperationException("Use increasing temperatures (1–100°C) and nondecreasing fan speeds (0–100%).");
        }
        if(curve[15]<70)throw new InvalidOperationException("The last fan point must be at least 70%.");
    }
    internal static void ValidateFirmwareCurve(byte[] curve) {
        if(curve==null||curve.Length!=16)throw new InvalidOperationException("Fan curve must contain eight points.");
        for(int i=0;i<8;i++) {
            bool temperature=curve[i]>=1&&(curve[i]<=110||(i==7&&curve[i]==255));
            if(!temperature||curve[i+8]>100||(i>0&&(curve[i]<curve[i-1]||curve[i+8]<curve[i+7])))
                throw new InvalidOperationException("Firmware fan curve has invalid temperatures or speeds.");
        }
    }
    internal static byte[] EditableCurve(byte[] firmware) {
        ValidateFirmwareCurve(firmware);byte[] draft=(byte[])firmware.Clone();int previous=0;
        for(int i=0;i<8;i++) {
            // Reserve space for the remaining points within the editor's range.
            draft[i]=(byte)Math.Max(previous+1,Math.Min(100-(7-i),firmware[i]));previous=draft[i];
        }
        draft[15]=Math.Max((byte)70,draft[15]);ValidateCurve(draft);return draft;
    }
}
public class FansPanel : UserControl {
    readonly MainForm main; int mode; readonly Button[] profiles=new Button[3];
    readonly Label summary=new Label(); readonly Button advancedToggle=new Button();
    AdvancedFansPanel advanced; bool advancedExpanded;
    public FansPanel(MainForm owner,int currentMode) {
        SuspendLayout();
        main=owner;mode=currentMode;Size=new Size(378,177);BackColor=owner.BackColor;ForeColor=owner.ForeColor;Font=owner.Font;
        var intro=new Label {Text="ASUS adjusts fans and power automatically.\nChoose the balance you prefer.",AutoSize=false};intro.SetBounds(0,0,378,36);Controls.Add(intro);
        string[] labels={"Quiet","Everyday","Performance"};int[] modes={2,0,1};
        for(int i=0;i<3;i++) {
            int selected=modes[i];
            profiles[i]=new Button {Text=labels[i],FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(43,45,49),ForeColor=ForeColor};
            profiles[i].SetBounds(i*129,43,120,30);profiles[i].Click+=(s,e)=>main.Run(()=>main.ChangeMode(selected));Controls.Add(profiles[i]);
        }
        summary.SetBounds(0,81,378,22);summary.ForeColor=Color.Silver;Controls.Add(summary);
        var reset=new Button {Text="Use ASUS defaults",FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(43,45,49),ForeColor=ForeColor};
        reset.SetBounds(0,110,180,28);reset.Click+=(s,e)=>main.Run(()=> {
            if(mode<0)throw new InvalidOperationException("Choose Quiet, Everyday or Performance first.");
            main.ChangeMode(mode);
        });Controls.Add(reset);
        advancedToggle.Text="Advanced settings   ▸";advancedToggle.FlatStyle=FlatStyle.Flat;advancedToggle.BackColor=Color.FromArgb(43,45,49);advancedToggle.ForeColor=ForeColor;
        advancedToggle.SetBounds(190,110,188,28);advancedToggle.Click+=(s,e)=>main.Run(ToggleAdvanced);Controls.Add(advancedToggle);
        var hint=new Label {Text="Changes last until you switch modes or restart.",ForeColor=Color.Silver};hint.SetBounds(0,147,378,22);Controls.Add(hint);
        SetMode(mode);
        AutoScaleMode=AutoScaleMode.None;ResumeLayout(true);Scale(new SizeF(main.UiScale,main.UiScale));
    }
    internal void ToggleAdvanced() {
        if(advanced==null) {advanced=new AdvancedFansPanel(main,mode);advanced.Location=new Point(0,(int)(42*main.UiScale));Controls.Add(advanced);}
        advancedExpanded=!advancedExpanded;advanced.Visible=advancedExpanded;
        foreach(Control control in Controls)if(control!=advanced&&control!=advancedToggle)control.Visible=!advancedExpanded;
        advancedToggle.Text=advancedExpanded?"‹  Back to simple settings":"Advanced settings   ▸";
        advancedToggle.SetBounds((int)((advancedExpanded?0:190)*main.UiScale),(int)((advancedExpanded?0:110)*main.UiScale),(int)((advancedExpanded?378:188)*main.UiScale),(int)(28*main.UiScale));
        Height=advancedExpanded?advanced.Bottom+(int)(4*main.UiScale):(int)(177*main.UiScale);
    }
    internal void SetMode(int value) {
        mode=value;int[] modes={2,0,1};
        for(int i=0;i<3;i++) {profiles[i].FlatAppearance.BorderColor=mode==modes[i]?Color.FromArgb(76,198,238):Color.FromArgb(65,68,74);profiles[i].FlatAppearance.BorderSize=mode==modes[i]?2:1;}
        summary.Text=mode==2?"Quiet: less fan noise; lower performance.":mode==0?"Everyday: balanced cooling and performance.":mode==1?"Performance: more speed; louder fans.":"Select a profile to let ASUS manage cooling.";
        if(advanced!=null)advanced.SetMode(value);
    }
    internal static void ValidateCurve(byte[] curve) {AdvancedFansPanel.ValidateCurve(curve);}
    internal static void ValidateFirmwareCurve(byte[] curve) {AdvancedFansPanel.ValidateFirmwareCurve(curve);}
}
}
