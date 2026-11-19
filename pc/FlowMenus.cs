using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Frociart
{
    // Preserve native menu semantics, keyboard traversal and accessibility;
    // own every pixel of the popup instead of using the Windows menu renderer.
    internal class FlowContextMenu : ContextMenuStrip
    {
        private readonly MenuAim aim;
        internal FlowContextMenu() { DoubleBuffered = true; DropShadowEnabled = false; AllowTransparency = true; aim=new MenuAim(this,e=>base.OnMouseMove(e)); }
        internal void ResetAim(){if(aim!=null)aim.Reset();}
        protected override void OnMouseMove(MouseEventArgs e) { FlowMenus.ResetParentAim(this);if(aim==null||!aim.Defer(e))base.OnMouseMove(e); }
        protected override void OnMouseDown(MouseEventArgs e) { FlowMenus.ResetAimChain(this);base.OnMouseDown(e); }
        protected override void OnMouseLeave(EventArgs e) { if(aim!=null)aim.Reset();base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { if(aim!=null)aim.Reset();base.OnMouseWheel(e); }
        protected override bool ProcessCmdKey(ref Message m,Keys key) { FlowMenus.ResetAimChain(this);return base.ProcessCmdKey(ref m,key); }
        protected override bool ProcessDialogKey(Keys key) { FlowMenus.ResetAimChain(this);return base.ProcessDialogKey(key); }
        protected override void OnOpening(CancelEventArgs e) { FlowMenus.Apply(this); base.OnOpening(e); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); FlowMenus.Round(this); }
    }
    internal sealed class FlowDropDownMenu : ToolStripDropDownMenu
    {
        private readonly MenuAim aim;
        internal FlowDropDownMenu() { DoubleBuffered = true; DropShadowEnabled = false; AllowTransparency = true; aim=new MenuAim(this,e=>base.OnMouseMove(e)); }
        internal void ResetAim(){if(aim!=null)aim.Reset();}
        protected override void OnMouseMove(MouseEventArgs e) { FlowMenus.ResetParentAim(this);if(aim==null||!aim.Defer(e))base.OnMouseMove(e); }
        protected override void OnMouseDown(MouseEventArgs e) { FlowMenus.ResetAimChain(this);base.OnMouseDown(e); }
        protected override void OnMouseLeave(EventArgs e) { if(aim!=null)aim.Reset();base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { if(aim!=null)aim.Reset();base.OnMouseWheel(e); }
        protected override bool ProcessCmdKey(ref Message m,Keys key) { FlowMenus.ResetAimChain(this);return base.ProcessCmdKey(ref m,key); }
        protected override bool ProcessDialogKey(Keys key) { FlowMenus.ResetAimChain(this);return base.ProcessDialogKey(key); }
        protected override void OnOpening(CancelEventArgs e) { FlowMenus.Apply(this); base.OnOpening(e); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); FlowMenus.Round(this); }
    }
    // Delay only incidental row crossings on a trajectory into the visible child.
    // Native ToolStrip still owns selection, clicks, keyboard and accessibility.
    internal sealed class MenuAim : IDisposable
    {
        private readonly ToolStripDropDown menu;
        private readonly Action<MouseEventArgs> resume;
        private readonly Timer timer=new Timer{Interval=450};
        private ToolStripMenuItem owner;
        private Point? anchor,previous;
        private Point pending;
        private bool disposed;
        internal bool Waiting { get { return timer.Enabled; } }
        internal MenuAim(ToolStripDropDown menu,Action<MouseEventArgs> resume) {
            this.menu=menu;this.resume=resume;timer.Tick+=Expired;menu.Closed+=Closed;menu.Disposed+=Disposed;
        }
        internal bool Defer(MouseEventArgs e) {
            if(disposed||!menu.Visible||e.Button!=MouseButtons.None){Reset();return false;}
            var active=menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i=>i.Available&&i.Enabled&&i.HasDropDownItems&&i.DropDown.Visible);
            if(active==null){Reset();return false;}
            if(owner!=active){Reset();owner=active;}
            Point screen=menu.PointToScreen(e.Location);
            if(active.Bounds.Contains(e.Location)){timer.Stop();anchor=previous=screen;return false;}
            Point? last=previous;previous=screen;
            if(!anchor.HasValue||!last.HasValue){timer.Stop();return false;}
            if(screen==last.Value&&timer.Enabled){pending=e.Location;return true;}
            if(TowardChild(anchor.Value,last.Value,screen,active.DropDown.Bounds,8*FlowMenus.Scale(menu))){
                pending=e.Location;if(!timer.Enabled)timer.Start();return true;
            }
            timer.Stop();anchor=null;return false;
        }
        private static bool TowardChild(Point a,Point last,Point p,Rectangle child,float tolerance) {
            int edge,sign;
            if(child.Left>a.X){edge=child.Left;sign=1;}
            else if(child.Right<a.X){edge=child.Right;sign=-1;}
            else return false;
            if(sign*(p.X-last.X)<=0||sign*(p.X-a.X)<0||sign*(edge-p.X)<0)return false;
            double t=(p.X-a.X)/(double)(edge-a.X);
            double top=a.Y+(child.Top-tolerance-a.Y)*t,bottom=a.Y+(child.Bottom+tolerance-a.Y)*t;
            return p.Y>=top&&p.Y<=bottom;
        }
        private void Expired(object sender,EventArgs e) {
            var active=owner;Point point=pending;Reset();
            if(!disposed&&!menu.IsDisposed&&menu.Visible&&active!=null&&active.DropDown.Visible&&menu.ClientRectangle.Contains(point))
                resume(new MouseEventArgs(MouseButtons.None,0,point.X,point.Y,0));
        }
        internal void Reset(){timer.Stop();owner=null;anchor=previous=null;}
        private void Closed(object sender,ToolStripDropDownClosedEventArgs e){Reset();}
        private void Disposed(object sender,EventArgs e){Dispose();}
        public void Dispose(){if(disposed)return;Reset();disposed=true;timer.Dispose();menu.Closed-=Closed;menu.Disposed-=Disposed;}
    }
    internal sealed class FlowMenuHeading : ToolStripItem
    {
        internal string Title, Hint;
        internal FlowMenuHeading(string title, string hint) { Title = title; Hint = hint; AutoSize = false; Size = new Size(300, 68); Enabled = false; AccessibleRole = AccessibleRole.StaticText; }
        protected override void OnPaint(PaintEventArgs e) {
            float s = e.Graphics.DpiX / 96f;
            using (var f = Ui.Font(11, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, Title, f, new Rectangle((int)(14*s), (int)(9*s), Width-(int)(28*s), (int)(25*s)), Ui.Ink, TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
            using (var f = Ui.Font(8.5f)) TextRenderer.DrawText(e.Graphics, Hint, f, new Rectangle((int)(14*s), (int)(35*s), Width-(int)(28*s), (int)(20*s)), Ui.Muted, TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
            using (var p = new Pen(Ui.Line)) e.Graphics.DrawLine(p,(int)(14*s),Height-3,Width-(int)(14*s),Height-3);
        }
    }
    internal static class FlowMenus
    {
        internal static void ResetParentAim(ToolStripDropDown menu){if(menu.OwnerItem!=null)ResetAimChain(menu.OwnerItem.Owner as ToolStripDropDown);}
        internal static void ResetAimChain(ToolStripDropDown menu){
            while(menu!=null){var context=menu as FlowContextMenu;if(context!=null)context.ResetAim();var child=menu as FlowDropDownMenu;if(child!=null)child.ResetAim();menu=menu.OwnerItem==null?null:menu.OwnerItem.Owner as ToolStripDropDown;}
        }
        // Transient menus must finish native click/close handling before a command
        // opens another message loop, closes its owner, or releases the menu.
        internal static ToolStripMenuItem Command(FlowContextMenu menu, Control owner, string text, Action action)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += delegate { Dispatch(owner, action); };
            menu.Items.Add(item);
            return item;
        }
        internal static void Toggle(FlowContextMenu menu, Control owner, string text, bool value, Action<bool> action)
        {
            var item = new ToolStripMenuItem(text) { Checked = value, CheckOnClick = true };
            item.Click += delegate { bool selected = item.Checked; Dispatch(owner, delegate { action(selected); }); };
            menu.Items.Add(item);
        }
        private static void Dispatch(Control owner, Action action)
        {
            if (owner.IsDisposed || owner.Disposing || !owner.IsHandleCreated) return;
            owner.BeginInvoke(new Action(delegate { if (!owner.IsDisposed && !owner.Disposing) action(); }));
        }
        internal static void ShowTransient(FlowContextMenu menu, Control owner, Point screenPosition)
        {
            if(owner==null||owner.IsDisposed){menu.Dispose();return;}
            Form form=owner.FindForm();
            FormClosingEventHandler closing=delegate{if(!menu.IsDisposed)menu.Close();};
            if(form!=null){form.FormClosing+=closing;menu.Disposed+=delegate{form.FormClosing-=closing;};}
            bool queued = false;
            menu.Closed += delegate {
                if (queued) return;
                queued = true;
                // The menu's handle survives closing the dialog that launched it.
                // Queuing on the dialog would lose this cleanup when handoff closes it.
                menu.BeginInvoke(new Action(delegate { if (!menu.IsDisposed) menu.Dispose(); }));
            };
            Apply(menu);
            // Keep SourceControl: closing fades belong to the form and must die with it.
            menu.Show(owner,owner.PointToClient(screenPosition));
        }
        private static readonly ConditionalWeakTable<ToolStripDropDown, PopupMotion> motions = new ConditionalWeakTable<ToolStripDropDown, PopupMotion>();
        internal static PopupMotion Motion(ToolStripDropDown menu) { return motions.GetValue(menu, p => new PopupMotion(p)); }
        internal static float Scale(ToolStrip menu) { return Math.Max(.75f, Math.Min(4, NativeTheme.DpiScale(menu))); }
        internal static string[] TextParts(ToolStripItem item) {
            string text = Regex.Replace(item.Text ?? "", @"(?<!&)&(?!&)", "").Replace("&&", "&");
            string[] parts = Regex.Split(text, @"\s{2,}", RegexOptions.None); return new[] {parts[0], parts.Length > 1 ? String.Join(" ", parts.Skip(1)) : ""};
        }
        internal static void Apply(ToolStripDropDown menu) {
            if (menu == null || menu.IsDisposed) return;
            if (!(menu.Renderer is FlowMenuRenderer)) menu.Renderer = new FlowMenuRenderer();
            menu.BackColor = Ui.Surface; menu.ForeColor = Ui.Ink; menu.DropShadowEnabled = false;
            menu.AutoSize = true; menu.AllowTransparency = true; float s = Scale(menu); menu.Padding = new Padding((int)(8*s));
            var context = menu as ContextMenuStrip; if (context != null) { context.ShowImageMargin = false; context.ShowCheckMargin = false; }
            var drop = menu as ToolStripDropDownMenu; if (drop != null) { drop.ShowImageMargin = false; drop.ShowCheckMargin = false; }
            int width = (int)(292*s);
            using (var font = Ui.Font(10)) using (var shortcut = Ui.Font(8.5f)) {
                foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>()) {
                    string[] parts = TextParts(item); int measure = TextRenderer.MeasureText(parts[0], font).Width + TextRenderer.MeasureText(parts[1], shortcut).Width + (int)(104*s);
                    width = Math.Max(width, measure);
                }
            }
            Rectangle available = Screen.FromPoint(menu.Visible ? menu.Location : Cursor.Position).WorkingArea;
            width = Math.Min(width, Math.Max((int)(220*s), available.Width-(int)(24*s)));
            if(menu.Items.OfType<ToolStripMenuItem>().Any())menu.MinimumSize=new Size(width+menu.Padding.Horizontal,0);
            foreach (ToolStripItem item in menu.Items) {
                item.BackColor = Ui.Surface; item.ForeColor = Ui.Ink;
                var standard = item as ToolStripMenuItem;
                if (standard != null) {
                    standard.AutoSize = false; standard.Size = new Size(width, (int)(42*s)); standard.Padding = Padding.Empty; standard.Margin = Padding.Empty;
                    string[] text = TextParts(item); standard.AccessibleName = text[0]; standard.AccessibleDescription = text[1];
                    if (standard.HasDropDownItems) {
                        if (!(standard.DropDown is FlowDropDownMenu)) {
                            var previous = standard.DropDown; var items = previous.Items.Cast<ToolStripItem>().ToArray(); previous.Items.Clear();
                            var replacement = new FlowDropDownMenu(); standard.DropDown = replacement; replacement.Items.AddRange(items); previous.Dispose();
                        }
                        Apply(standard.DropDown);
                    }
                } else if (item is ToolStripSeparator) { item.AutoSize = false; item.Size = new Size(width, (int)(13*s)); }
                else if (item is FlowMenuHeading) { item.Size = new Size(width, (int)(68*s)); item.AccessibleName = ((FlowMenuHeading)item).Title; }
            }
            Motion(menu); Round(menu); menu.Invalidate();
        }
        internal static void Round(ToolStripDropDown menu) {
            if (menu.Width < 3 || menu.Height < 3) return;
            using (var path = Ui.Rounded(new RectangleF(0,0,menu.Width,menu.Height),14*Scale(menu))) {
                Region old = menu.Region; menu.Region = new Region(path); if (old != null) old.Dispose();
            }
        }
    }
    internal sealed class PopupMotion : IDisposable
    {
        private sealed class Hover { internal float Value, From, Target; internal long Started; }
        private readonly ToolStripDropDown menu;
        private readonly Timer timer = new Timer { Interval = 16 };
        private readonly Stopwatch watch = Stopwatch.StartNew();
        private readonly Dictionary<ToolStripItem,Hover> items = new Dictionary<ToolStripItem,Hover>();
        private Point anchor; private long opened; private bool entering, disposed;
        internal bool Running { get { return timer.Enabled; } }
        internal PopupMotion(ToolStripDropDown menu) {
            this.menu=menu; timer.Tick+=Tick; menu.Opened+=Opened; menu.Closing+=Closing; menu.Closed+=Closed; menu.Disposed+=Disposed; UiMotion.Changed+=PolicyChanged;
        }
        private void Opened(object sender,EventArgs e) {
            items.Clear(); anchor=menu.Location; entering=UiMotion.Enabled; opened=watch.ElapsedMilliseconds;
            if(entering){menu.Opacity=.06;menu.Location=new Point(anchor.X,anchor.Y+10);timer.Start();}else menu.Opacity=1;
        }
        internal float HoverValue(ToolStripItem item) {
            float target=item.Selected&&item.Enabled?1:0; if(!UiMotion.Enabled)return target;
            Hover h; if(!items.TryGetValue(item,out h)){h=new Hover();items[item]=h;}
            if(h.Target!=target){h.From=h.Value;h.Target=target;h.Started=watch.ElapsedMilliseconds;if(menu.Visible)timer.Start();}
            return h.Value;
        }
        private void Tick(object sender,EventArgs e) {
            if(disposed||!menu.Visible){timer.Stop();return;}bool active=false;long now=watch.ElapsedMilliseconds;
            if(entering){float t=Math.Min(1,(now-opened)/320f);float ease=UiMotion.Ease(t);menu.Opacity=.06+.94*ease;menu.Location=new Point(anchor.X,anchor.Y+(int)Math.Round(10*(1-UiMotion.Spring(t))));entering=t<1;active|=entering;}
            foreach(Hover h in items.Values){float t=Math.Min(1,(now-h.Started)/115f);h.Value=h.From+(h.Target-h.From)*UiMotion.Ease(t);active|=t<1&&Math.Abs(h.Value-h.Target)>.002;}
            menu.Invalidate();if(!active)timer.Stop();
        }
        private void Closing(object sender,ToolStripDropDownClosingEventArgs e) {
            if(!e.Cancel&&UiMotion.Enabled&&menu.Visible&&menu.Width>0&&menu.Height>0&&PopupGhost.ActiveCount<3) {
                Bitmap bitmap=null;
                try { bitmap=new Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,menu.Size));var ghost=new PopupGhost(bitmap,new Rectangle(menu.Location,menu.Size),menu.Opacity);bitmap=null;ToolStripDropDown root=menu;while(root.OwnerItem!=null&&root.OwnerItem.Owner is ToolStripDropDown)root=(ToolStripDropDown)root.OwnerItem.Owner;var context=root as ContextMenuStrip;Form owner=context==null||context.SourceControl==null?null:context.SourceControl.FindForm();if(owner!=null&&!owner.IsDisposed)ghost.Show(owner);else ghost.Show(); }
                catch(ArgumentException) { } catch(InvalidOperationException) { }
                finally { if(bitmap!=null)bitmap.Dispose(); }
            }
        }
        private void Closed(object sender,ToolStripDropDownClosedEventArgs e) { timer.Stop(); entering=false;items.Clear();menu.Opacity=1; }
        private void PolicyChanged(object sender,EventArgs e) { if(!UiMotion.Enabled){timer.Stop();entering=false;if(!menu.IsDisposed){menu.Opacity=1;if(menu.Visible)menu.Location=anchor;menu.Invalidate();}} }
        private void Disposed(object sender,EventArgs e){Dispose();}
        public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Dispose();items.Clear();menu.Opened-=Opened;menu.Closing-=Closing;menu.Closed-=Closed;menu.Disposed-=Disposed;UiMotion.Changed-=PolicyChanged;}
    }
    // The real menu closes immediately. This short, click-through visual cannot
    // postpone commands, block a modal dialog, or steal keyboard focus.
    internal sealed class PopupGhost : NativeWindow, IDisposable
    {
        private readonly Bitmap bitmap;private readonly Timer timer=new Timer{Interval=16};private readonly Stopwatch watch=new Stopwatch();private readonly Rectangle bounds;private readonly byte initialAlpha;private readonly int duration;private bool counted,disposed;
        private IntPtr screenDc,memoryDc,nativeBitmap,previousBitmap;private Form owner;
        internal static int ActiveCount;
        internal bool SurfaceReady {get;private set;}
        internal byte CurrentAlpha {get;private set;}
        internal PopupGhost(Bitmap source,Rectangle bounds,double opacity=1,float cornerRadius=14,int durationMs=160){
            this.bounds=bounds;duration=Math.Max(80,durationMs);initialAlpha=(byte)(255*Math.Max(0,Math.Min(1,opacity)));
            bitmap=new Bitmap(source.Width,source.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(bitmap))using(var shape=Ui.Rounded(new RectangleF(0,0,source.Width,source.Height),Math.Max(.01f,cornerRadius*source.HorizontalResolution/96f))){
                g.Clear(Color.Transparent);g.SetClip(shape);g.DrawImageUnscaled(source,0,0);
            }source.Dispose();timer.Tick+=Tick;UiMotion.Changed+=MotionChanged;
        }
        internal void Show(){Show(null);}
        private void MotionChanged(object sender,EventArgs e){if(!UiMotion.Enabled)Dispose();}
        private void OwnerClosed(object sender,EventArgs e){Dispose();}
        internal void Show(Form owner,bool beginFade=true){
            if(owner!=null&&owner.IsDisposed){Dispose();return;}this.owner=owner;if(owner!=null){owner.FormClosed+=OwnerClosed;owner.Disposed+=OwnerClosed;}
            try{
                CreateHandle(new CreateParams{Style=unchecked((int)0x80000000),ExStyle=0x08000000|0x80|0x20|0x80000,X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height,Parent=owner==null?IntPtr.Zero:owner.Handle});
                screenDc=GetDC(IntPtr.Zero);memoryDc=CreateCompatibleDC(screenDc);nativeBitmap=bitmap.GetHbitmap(Color.FromArgb(0));previousBitmap=SelectObject(memoryDc,nativeBitmap);
                // Publish a complete premultiplied surface before making the window visible.
                // Painting after ShowWindow can expose an uninitialized backing surface.
                SurfaceReady=Present(initialAlpha);if(!SurfaceReady){Dispose();return;}
                ShowWindow(Handle,4);counted=true;ActiveCount++;if(beginFade)BeginFade();
            }catch{Dispose();throw;}
        }
        internal void BeginFade(){if(disposed)return;watch.Restart();timer.Start();}
        private bool Present(byte alpha){var destination=new NativePoint{X=bounds.X,Y=bounds.Y};var source=new NativePoint();var size=new NativeSize{Width=bounds.Width,Height=bounds.Height};var blend=new Blend{Operation=0,Alpha=alpha,Format=1};CurrentAlpha=alpha;return UpdateLayeredWindow(Handle,screenDc,ref destination,ref size,memoryDc,ref source,0,ref blend,2);}
        private void Tick(object sender,EventArgs e){float t=Math.Min(1,watch.ElapsedMilliseconds/(float)duration);if(!UiMotion.Enabled||t>=1){Dispose();return;}Present((byte)(initialAlpha*(1-UiMotion.Ease(t))));}
        protected override void WndProc(ref Message m){if(m.Msg==0x0014){m.Result=new IntPtr(1);return;}if(m.Msg==0x0084){m.Result=new IntPtr(-1);return;}if(m.Msg==0x0021){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
        public void Dispose(){if(disposed)return;disposed=true;UiMotion.Changed-=MotionChanged;if(owner!=null){owner.FormClosed-=OwnerClosed;owner.Disposed-=OwnerClosed;owner=null;}timer.Stop();timer.Dispose();if(Handle!=IntPtr.Zero)DestroyHandle();if(memoryDc!=IntPtr.Zero&&previousBitmap!=IntPtr.Zero)SelectObject(memoryDc,previousBitmap);if(nativeBitmap!=IntPtr.Zero)DeleteObject(nativeBitmap);if(memoryDc!=IntPtr.Zero)DeleteDC(memoryDc);if(screenDc!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screenDc);bitmap.Dispose();if(counted){counted=false;ActiveCount--;}}
        [StructLayout(LayoutKind.Sequential)]private struct NativePoint{internal int X,Y;}
        [StructLayout(LayoutKind.Sequential)]private struct NativeSize{internal int Width,Height;}
        [StructLayout(LayoutKind.Sequential,Pack=1)]private struct Blend{internal byte Operation,Flags,Alpha,Format;}
        [DllImport("user32.dll",SetLastError=true)]private static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dc,ref NativePoint destination,ref NativeSize size,IntPtr source,ref NativePoint origin,uint key,ref Blend blend,uint flags);
        [DllImport("user32.dll")]private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
        [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
        [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr value);
        [DllImport("user32.dll")]private static extern bool ShowWindow(IntPtr hwnd,int command);
    }
    internal sealed class FlowMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Ui.Surface);}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Ui.Rounded(new RectangleF(.5f,.5f,e.ToolStrip.Width-1.5f,e.ToolStrip.Height-1.5f),14*FlowMenus.Scale(e.ToolStrip)))using(var p=new Pen(Ui.Line))e.Graphics.DrawPath(p,path);}
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){}
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){}
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){}
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){float s=FlowMenus.Scale(e.ToolStrip);using(var p=new Pen(Ui.Line))e.Graphics.DrawLine(p,14*s,e.Item.Height/2f,e.Item.Width-14*s,e.Item.Height/2f);}
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){
            var menu=e.ToolStrip as ToolStripDropDown;if(menu==null)return;float t=FlowMenus.Motion(menu).HoverValue(e.Item);if(t<=0)return;
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=FlowMenus.Scale(e.ToolStrip);using(var path=Ui.Rounded(new RectangleF(0,1,e.Item.Width-1,e.Item.Height-2),9*s))using(var b=new SolidBrush(UiMotion.Blend(Ui.Surface,Ui.Hover,t)))e.Graphics.FillPath(b,path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
            var item=e.Item as ToolStripMenuItem;if(item==null){base.OnRenderItemText(e);return;}float s=FlowMenus.Scale(e.ToolStrip);string[] parts=FlowMenus.TextParts(item);
            bool danger=parts[0].StartsWith("Elimina",StringComparison.OrdinalIgnoreCase)||parts[0].StartsWith("Rimuovi",StringComparison.OrdinalIgnoreCase);
            Color ink=!item.Enabled?Ui.Muted:danger?Ui.Red:Ui.Ink;float hover=FlowMenus.Motion((ToolStripDropDown)e.ToolStrip).HoverValue(item);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            DrawIcon(e.Graphics,parts[0],new RectangleF(13*s,(item.Height-18*s)/2,18*s,18*s),item.Checked?Ui.Blue:ink,item.Checked);
            int shortcutWidth=0;if(parts[1].Length>0)using(var f=Ui.Font(8.3f)){shortcutWidth=TextRenderer.MeasureText(parts[1],f).Width+(int)(16*s);var r=new Rectangle(item.Width-shortcutWidth-(int)(14*s),(item.Height-(int)(24*s))/2,shortcutWidth,(int)(24*s));using(var p=Ui.Rounded(r,5*s))using(var b=new SolidBrush(UiMotion.Blend(Ui.Paper,Ui.Tint,hover)))e.Graphics.FillPath(b,p);TextRenderer.DrawText(e.Graphics,parts[1],f,r,Ui.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);}
            int right=(int)((item.HasDropDownItems?34:16)*s)+shortcutWidth+(shortcutWidth>0?(int)(8*s):0);
            using(var f=Ui.Font(10))TextRenderer.DrawText(e.Graphics,parts[0],f,new Rectangle((int)(44*s),0,Math.Max(1,item.Width-(int)(44*s)-right),item.Height),ink,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
            if(item.HasDropDownItems)using(var p=new Pen(UiMotion.Blend(Ui.Muted,Ui.Blue,hover),1.7f*s){StartCap=LineCap.Round,EndCap=LineCap.Round}){float x=item.Width-19*s+hover*s,y=item.Height/2f;e.Graphics.DrawLines(p,new[]{new PointF(x-3*s,y-4*s),new PointF(x+1*s,y),new PointF(x-3*s,y+4*s)});}
        }
        internal static void DrawIcon(Graphics g,string caption,RectangleF r,Color color,bool check=false){
            var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/20,r.Height/20);string t=caption.ToLowerInvariant();
            using(var p=new Pen(color,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){
                if(check){g.DrawLines(p,new[]{new PointF(3,10),new PointF(8,15),new PointF(17,5)});}
                else if(t.StartsWith("aggiungi")){g.DrawLine(p,10,3,10,17);g.DrawLine(p,3,10,17,10);}
                else if(t.StartsWith("elimina")||t.StartsWith("rimuovi")){g.DrawLine(p,3,5,17,5);g.DrawLines(p,new[]{new PointF(5,5),new PointF(6,17),new PointF(14,17),new PointF(15,5)});g.DrawLine(p,8,2,12,2);g.DrawLine(p,8,8,8,14);g.DrawLine(p,12,8,12,14);}
                else if(t.StartsWith("taglia")){g.DrawEllipse(p,2,12,5,5);g.DrawEllipse(p,12,12,5,5);g.DrawLine(p,6,13,16,2);g.DrawLine(p,13,13,4,2);}
                else if(t.StartsWith("copia")){g.DrawRectangle(p,6,6,11,12);g.DrawLines(p,new[]{new PointF(12,2),new PointF(2,2),new PointF(2,13)});}
                else if(t.StartsWith("incolla")){g.DrawRectangle(p,3,4,14,14);g.DrawRectangle(p,7,2,6,4);g.DrawLine(p,6,10,14,10);g.DrawLine(p,6,14,12,14);}
                else if(t.StartsWith("cerca")||t.StartsWith("trova")){g.DrawEllipse(p,2,2,11,11);g.DrawLine(p,12,12,18,18);}
                else if(t.StartsWith("funzioni")){g.DrawLines(p,new[]{new PointF(7,3),new PointF(4,3),new PointF(4,17),new PointF(7,17)});g.DrawLines(p,new[]{new PointF(13,3),new PointF(16,3),new PointF(16,17),new PointF(13,17)});g.DrawLine(p,8,10,12,10);}
                else if(t.StartsWith("cicli")){g.DrawArc(p,2,2,16,16,35,295);g.DrawLines(p,new[]{new PointF(13,2),new PointF(18,2),new PointF(18,7)});}
                else if(t.StartsWith("riordina")){g.DrawRectangle(p,7,1,6,4);g.DrawLine(p,10,5,10,8);g.DrawLine(p,4,8,16,8);g.DrawLine(p,4,8,4,12);g.DrawLine(p,16,8,16,12);g.DrawRectangle(p,1,12,6,5);g.DrawRectangle(p,13,12,6,5);}
                else if(t.StartsWith("duplica")){g.DrawRectangle(p,7,7,10,10);g.DrawLines(p,new[]{new PointF(12,3),new PointF(3,3),new PointF(3,12)});}
                else if(t.StartsWith("modifica")||t.StartsWith("rinomina")){g.DrawLines(p,new[]{new PointF(3,17),new PointF(4,12),new PointF(14,2),new PointF(18,6),new PointF(8,16),new PointF(3,17)});g.DrawLine(p,12,4,16,8);}
                else if(t.StartsWith("collega")){g.DrawRectangle(p,2,2,6,5);g.DrawRectangle(p,12,13,6,5);g.DrawLines(p,new[]{new PointF(5,7),new PointF(5,10),new PointF(15,10),new PointF(15,13)});}
                else if(t.StartsWith("seleziona")){p.DashStyle=DashStyle.Dash;g.DrawRectangle(p,3,3,14,14);p.DashStyle=DashStyle.Solid;}
                else if(t.StartsWith("adatta")){g.DrawLines(p,new[]{new PointF(7,3),new PointF(3,3),new PointF(3,7)});g.DrawLines(p,new[]{new PointF(13,3),new PointF(17,3),new PointF(17,7)});g.DrawLines(p,new[]{new PointF(3,13),new PointF(3,17),new PointF(7,17)});g.DrawLines(p,new[]{new PointF(17,13),new PointF(17,17),new PointF(13,17)});g.DrawEllipse(p,8,8,4,4);}
                else if(t.StartsWith("zoom")){g.DrawEllipse(p,2,2,11,11);g.DrawLine(p,12,12,18,18);g.DrawLine(p,5,7.5f,10,7.5f);}
                else if(t.StartsWith("salva")){g.DrawLines(p,new[]{new PointF(3,2),new PointF(15,2),new PointF(18,5),new PointF(18,18),new PointF(2,18),new PointF(2,2),new PointF(3,2)});g.DrawRectangle(p,6,2,7,6);g.DrawRectangle(p,6,12,8,6);}
                else if(t.StartsWith("esporta")){g.DrawLines(p,new[]{new PointF(3,10),new PointF(3,17),new PointF(17,17),new PointF(17,10)});g.DrawLine(p,10,12,10,2);g.DrawLines(p,new[]{new PointF(6,6),new PointF(10,2),new PointF(14,6)});}
                else if(t.StartsWith("home")){g.DrawLines(p,new[]{new PointF(2,9),new PointF(10,2),new PointF(18,9)});g.DrawLines(p,new[]{new PointF(4,8),new PointF(4,18),new PointF(16,18),new PointF(16,8)});}
                else if(t.StartsWith("annulla")||t.StartsWith("ripeti")){if(t.StartsWith("ripeti")){g.TranslateTransform(20,0);g.ScaleTransform(-1,1);}g.DrawArc(p,3,6,14,11,205,235);g.DrawLines(p,new[]{new PointF(3,3),new PointF(3,9),new PointF(9,9)});}
                else if(t.StartsWith("input")||t.StartsWith("leggi un valore")){Ui.Shape(g,NodeKind.Input,new RectangleF(1,3,18,14),color);}
                else if(t.StartsWith("output")||t.StartsWith("mostra un risultato")){Ui.Shape(g,NodeKind.Output,new RectangleF(1,3,18,14),color);}
                else if(t.StartsWith("decisione")||t.StartsWith("condizione")){Ui.Shape(g,NodeKind.Decision,new RectangleF(1,1,18,18),color);}
                else if(t.StartsWith("operazione")){Ui.Shape(g,NodeKind.Process,new RectangleF(1,3,18,14),color);}
                else if(t.StartsWith("inizio")||t.StartsWith("fine")){Ui.Shape(g,t.StartsWith("inizio")?NodeKind.Start:NodeKind.End,new RectangleF(1,4,18,12),color);}
                else if(t.StartsWith("animazioni")){g.DrawLine(p,3,5,17,5);g.DrawLine(p,3,10,17,10);g.DrawLine(p,3,15,17,15);g.DrawEllipse(p,6,3,4,4);g.DrawEllipse(p,11,8,4,4);g.DrawEllipse(p,5,13,4,4);}
                else if(t.StartsWith("suoni")){g.DrawLines(p,new[]{new PointF(3,8),new PointF(6,8),new PointF(11,4),new PointF(11,16),new PointF(6,12),new PointF(3,12),new PointF(3,8)});g.DrawArc(p,9,4,8,12,-55,110);}
                else {g.DrawEllipse(p,2,2,16,16);g.DrawLine(p,10,9,10,14);g.DrawEllipse(p,9.5f,5.5f,1,1);}
            }g.Restore(state);
        }
    }
}
