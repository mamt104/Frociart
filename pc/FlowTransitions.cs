using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
namespace Frociart {
 internal static class FlowFormMotion {
    private static readonly ConditionalWeakTable<Form,object> attached=new ConditionalWeakTable<Form,object>();
    // Call before theming when the window owns its opacity animation.
    internal static void UseCustomAnimation(Form form){object found;if(!attached.TryGetValue(form,out found))attached.Add(form,new object());}
    internal static void Attach(Form form){object found;if(attached.TryGetValue(form,out found))return;attached.Add(form,new object());if(UiMotion.Enabled&&!form.Visible)form.Opacity=.01;form.Shown+=delegate{if(!UiMotion.Enabled){form.Opacity=1;return;}var watch=Stopwatch.StartNew();var timer=new Timer{Interval=16};form.Opacity=.06;timer.Tick+=delegate{if(form.IsDisposed||!UiMotion.Enabled||watch.ElapsedMilliseconds>=160){timer.Stop();timer.Dispose();if(!form.IsDisposed)form.Opacity=1;return;}form.Opacity=.06+.94*UiMotion.Ease(watch.ElapsedMilliseconds/160f);};form.Disposed+=delegate{timer.Stop();timer.Dispose();};timer.Start();};}
 }
 internal static class FlowTransitions {
    private sealed class State {internal int Generation;internal PopupGhost Visual;}
    private static readonly ConditionalWeakTable<Control,State> states=new ConditionalWeakTable<Control,State>();
    internal static int Active;
    internal static void Enter(Control target){
        if(target==null||target.IsDisposed||!target.Visible||!target.IsHandleCreated||!UiMotion.Enabled||target.Width<3||target.Height<3||(long)target.Width*target.Height>8000000)return;
        foreach(var old in System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OfType<PageEntrance>(target.Controls)))old.Dispose();
        var entrance=new PageEntrance(target);if(entrance.HasFrames){target.Controls.Add(entrance);entrance.BringToFront();entrance.Start();}else entrance.Dispose();
    }
    internal static void Change(Control target,bool reveal=false){if(target==null||target.Controls.Count==0)return;ChangeSnapshot(target);}
    internal static void CanvasChange(FlowCanvas target){ChangeSnapshot(target);}
    private static void ChangeSnapshot(Control target){
        if(target==null||target.IsDisposed||!target.Visible||!target.IsHandleCreated||!UiMotion.Enabled||target.Width<3||target.Height<3||(long)target.Width*target.Height>8000000)return;
        State state=states.GetValue(target,c=>new State());int generation=++state.Generation;if(state.Visual!=null){state.Visual.Dispose();state.Visual=null;}
        Bitmap before=null;PopupGhost visual=null;bool counted=false;EventHandler disposedHandler=null;
        Action release=delegate{if(disposedHandler!=null){target.Disposed-=disposedHandler;disposedHandler=null;}if(visual!=null){visual.Dispose();visual=null;}if(counted){counted=false;Active--;}if(state.Generation==generation)state.Visual=null;};
        try{
            before=new Bitmap(target.Width,target.Height);before.SetResolution(target.DeviceDpi,target.DeviceDpi);target.DrawToBitmap(before,new Rectangle(Point.Empty,target.Size));
            visual=new PopupGhost(before,new Rectangle(target.PointToScreen(Point.Empty),target.Size),1,0,280);before=null;visual.Show(target.FindForm(),false);state.Visual=visual;counted=true;Active++;disposedHandler=(sender,args)=>release();target.Disposed+=disposedHandler;
            target.BeginInvoke(new Action(delegate{
                if(state.Generation!=generation||target.IsDisposed||!target.Visible||!UiMotion.Enabled){release();return;}
                target.PerformLayout();target.Update();if(visual!=null)visual.BeginFade();
                var timer=new Timer{Interval=16};var watch=Stopwatch.StartNew();timer.Tick+=delegate{if(target.IsDisposed||state.Generation!=generation||!UiMotion.Enabled||watch.ElapsedMilliseconds>=300){timer.Stop();timer.Dispose();release();}};timer.Start();
            }));
        }catch(ArgumentException){release();}catch(InvalidOperationException){release();}finally{if(before!=null)before.Dispose();}
    }
 }
 // Paint snapshots while the real native controls retain their final layout,
 // focus, accessibility and hit targets. No layout work runs on animation frames.
 internal sealed class PageEntrance : Control {
    private sealed class Frame {internal Bitmap Image;internal Rectangle Bounds;internal int Delay;}
    private readonly System.Collections.Generic.List<Frame> frames=new System.Collections.Generic.List<Frame>();
    private readonly Control page;private readonly Timer timer=new Timer{Interval=16};private readonly Stopwatch watch=new Stopwatch();private bool counted;
    internal bool HasFrames {get{return frames.Count>0;}}
    internal float Progress {get{return Math.Min(1,watch.ElapsedMilliseconds/580f);}}
    internal PageEntrance(Control page){
        this.page=page;Bounds=page.ClientRectangle;TabStop=false;AccessibleRole=AccessibleRole.None;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        foreach(Control child in page.Controls){if(!child.Visible||child.Width<1||child.Height<1)continue;var bitmap=new Bitmap(child.Width,child.Height);bitmap.SetResolution(page.DeviceDpi,page.DeviceDpi);child.DrawToBitmap(bitmap,new Rectangle(Point.Empty,child.Size));frames.Add(new Frame{Image=bitmap,Bounds=child.Bounds,Delay=Math.Min(frames.Count*32,144)});}
        timer.Tick+=delegate{if(!UiMotion.Enabled||!page.Visible||watch.ElapsedMilliseconds>=580){Dispose();return;}Invalidate();};
        page.SizeChanged+=PageChanged;page.VisibleChanged+=PageChanged;UiMotion.Changed+=PolicyChanged;
    }
    internal void Start(){counted=true;FlowTransitions.Active++;watch.Start();timer.Start();}
    private void PageChanged(object sender,EventArgs e){Dispose();}
    private void PolicyChanged(object sender,EventArgs e){if(!UiMotion.Enabled)Dispose();}
    protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(page.BackColor);}
    protected override void OnPaint(PaintEventArgs e){
        float scale=NativeTheme.DpiScale(this);foreach(var frame in frames){float t=Math.Max(0,Math.Min(1,(watch.ElapsedMilliseconds-frame.Delay)/420f));if(t<=0)continue;float spring=UiMotion.Spring(t);var destination=frame.Bounds;destination.Y+=(int)Math.Round(12*scale*(1-spring));using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=UiMotion.Ease(Math.Min(1,t*1.7f));attributes.SetColorMatrix(matrix);e.Graphics.DrawImage(frame.Image,destination,0,0,frame.Image.Width,frame.Image.Height,GraphicsUnit.Pixel,attributes);}}
    }
    protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();page.SizeChanged-=PageChanged;page.VisibleChanged-=PageChanged;UiMotion.Changed-=PolicyChanged;foreach(var frame in frames)frame.Image.Dispose();frames.Clear();if(counted){counted=false;FlowTransitions.Active--;}}base.Dispose(disposing);}
 }
}

