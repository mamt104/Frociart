using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Frociart
{
    // Only documented window APIs are used. No process-wide theme preference,
    // private uxtheme ordinals, global hooks, or replacement of native edit state.
    // API references:
    // learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
    // learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getscrollbarinfo
    // learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getscrollinfo
    // learn.microsoft.com/windows/win32/controls/em-setrectnp
    // learn.microsoft.com/windows/win32/api/uxtheme/nf-uxtheme-setwindowtheme
    internal static class NativeTheme
    {
        private static readonly ConditionalWeakTable<Control, ThemeState> states = new ConditionalWeakTable<Control, ThemeState>();
        internal static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }

        internal static void Apply(Control control)
        {
            if (control == null || control.IsDisposed || !IsWindows) return;
            if (!(control is Form) && !(control is TextBoxBase) && !(control is ListBox) &&
                !(control is ListView) && !(control is ComboBox) && !(control is FlowScrollPanel)) return;
            states.GetValue(control, c => new ThemeState(c)).Apply();
        }

        internal static void AttachAuxiliary(Control owner, IntPtr handle, bool header)
        {
            if (!IsWindows || owner == null || owner.IsDisposed || handle == IntPtr.Zero) return;
            states.GetValue(owner, c => new ThemeState(c)).AttachAuxiliary(handle, header);
        }

        private sealed class ThemeState
        {
            private readonly Control control;
            private ChromeWindow chrome, header, popup;
            private IntPtr themedHandle;
            private bool? dark;
            internal ThemeState(Control control)
            {
                this.control = control;
                control.HandleCreated += Created;
                control.HandleDestroyed += Destroyed;
                control.Disposed += Disposed;
            }
            private void Created(object sender, EventArgs e) { Apply(); }
            private void Destroyed(object sender, EventArgs e)
            {
                if (chrome != null) chrome.Dispose(); if (header != null) header.Dispose(); if (popup != null) popup.Dispose();
                chrome = header = popup = null; themedHandle = IntPtr.Zero; dark = null;
            }
            private void Disposed(object sender, EventArgs e)
            {
                Destroyed(sender, e); control.HandleCreated -= Created; control.HandleDestroyed -= Destroyed; control.Disposed -= Disposed;
            }
            internal void Apply()
            {
                if (!control.IsHandleCreated || control.IsDisposed) return;
                if (control is Form)
                {
                    if (themedHandle != control.Handle || dark != Ui.IsDark)
                    { ApplyTitleBar((Form)control); themedHandle = control.Handle; dark = Ui.IsDark; }
                    return;
                }
                if (chrome == null || chrome.Handle != control.Handle)
                {
                    if (chrome != null) chrome.Dispose();
                    // UxTheme's delayed scrollbar hover animation can repaint a
                    // light track after our non-client paint has finished. Opt
                    // out of visual styles only on controls whose chrome we draw;
                    // native editing, scroll state and accessibility stay intact.
                    if (control is TextBoxBase || control is FlowScrollPanel || control is ListBox || control is FlowListView)
                        SetWindowTheme(control.Handle, "", "");
                    chrome = new ChromeWindow(control, control.Handle, false, false);
                }
                chrome.RefreshChrome();
                if (header != null) header.RefreshChrome(); if (popup != null) popup.RefreshChrome();
            }
            internal void AttachAuxiliary(IntPtr handle, bool isHeader)
            {
                ChromeWindow current = isHeader ? header : popup;
                if (current == null || current.Handle != handle)
                {
                    if (current != null) current.Dispose();
                    if (!isHeader) SetWindowTheme(handle, "", "");
                    current = new ChromeWindow(control, handle, isHeader, !isHeader);
                    if (isHeader) header = current; else popup = current;
                }
                current.RefreshChrome();
            }
        }

        private static int ColorRef(Color color) { return color.R | (color.G << 8) | (color.B << 16); }
        private static void ApplyTitleBar(Form form)
        {
            Version version = Environment.OSVersion.Version;
            if (version.Major < 10) return;
            try
            {
                int dark = Ui.IsDark ? 1 : 0;
                // Unsupported DWM attributes return an HRESULT; they do not alter
                // other attributes. Attribute 16 is not a dark-mode fallback.
                DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
                if (version.Build >= 22000)
                {
                    int caption = ColorRef(Ui.Surface), ink = ColorRef(Ui.Ink), border = ColorRef(Ui.Line);
                    DwmSetWindowAttribute(form.Handle, 35, ref caption, 4);
                    DwmSetWindowAttribute(form.Handle, 36, ref ink, 4);
                    DwmSetWindowAttribute(form.Handle, 34, ref border, 4);
                }
                RedrawWindow(form.Handle, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0400);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        internal static float DpiScale(Control control)
        {
            if (control != null && control.IsHandleCreated)
                using (Graphics graphics = control.CreateGraphics()) return graphics.DpiX / 96f;
            return 1f;
        }

        internal static void PadEdit(TextBoxBase edit)
        {
            if (!IsWindows || !edit.IsHandleCreated || edit.ClientSize.Width < 20 || edit.ClientSize.Height < 10) return;
            int x = Math.Max(4, (int)Math.Round(7 * DpiScale(edit)));
            SendMessage(edit.Handle, 0x00D3, new IntPtr(3), new IntPtr(x | (x << 16))); // EM_SETMARGINS
            if (edit.Multiline)
            {
                int y = Math.Max(3, (int)Math.Round(5 * DpiScale(edit)));
                NativeRect rectangle = new NativeRect(x, y, Math.Max(x + 1, edit.ClientSize.Width - x), Math.Max(y + 1, edit.ClientSize.Height - y));
                SendRect(edit.Handle, 0x00B4, IntPtr.Zero, ref rectangle); // EM_SETRECTNP
            }
        }

        private sealed class ScrollGeometry
        {
            internal bool Horizontal;
            internal Rectangle Bounds;
            internal int Arrow, ThumbStart, ThumbEnd;
            internal ScrollInfo Info;
            internal int Maximum { get { return Math.Max(Info.Minimum, Info.Maximum - (int)Math.Max(1u, Info.Page) + 1); } }
            internal bool Enabled { get { return Maximum > Info.Minimum; } }
            internal int Coordinate(Point point) { return Horizontal ? point.X - Bounds.Left : point.Y - Bounds.Top; }
            internal int Length { get { return Horizontal ? Bounds.Width : Bounds.Height; } }
        }

        // A normal per-control NativeWindow subclass. Native text editing,
        // selection, keyboard navigation and accessibility objects stay intact.
        // Only non-client paint and pointer interaction on its visible scrollbar
        // are handled here, so Windows cannot flash a light scrollbar while drag
        // tracking. Scrolling uses each control's documented scrolling messages.
        private sealed class ChromeWindow : NativeWindow, IDisposable
        {
            private readonly Control owner;
            private readonly bool header, popup;
            private bool painting, dragging, held;
            private ScrollGeometry active;
            private int dragOffset, repeatDirection, repeatStep;
            private readonly Timer repeat = new Timer { Interval = 350 };
            internal ChromeWindow(Control owner, IntPtr handle, bool header, bool popup)
            {
                this.owner = owner; this.header = header; this.popup = popup;
                AssignHandle(handle);
                repeat.Tick += RepeatTick;
            }
            public void Dispose()
            {
                repeat.Stop(); repeat.Tick -= RepeatTick; repeat.Dispose();
                if (Handle != IntPtr.Zero) ReleaseHandle();
            }
            internal void RefreshChrome()
            {
                if (Handle == IntPtr.Zero || owner.IsDisposed) return;
                PaintChrome(IntPtr.Zero);
            }
            private void RepeatTick(object sender, EventArgs e)
            {
                if (!held || Handle == IntPtr.Zero || active == null) { repeat.Stop(); return; }
                repeat.Interval = 65;
                ScrollGeometry current = Geometry(active.Horizontal);
                if (current != null) MoveTo(current, current.Info.Position + repeatDirection * repeatStep);
            }
            protected override void WndProc(ref Message m)
            {
                if (!header)
                {
                    if (m.Msg == 0x0084) // WM_NCHITTEST
                    {
                        base.WndProc(ref m);
                        Point screen = PointFrom(m.LParam);
                        if (At(screen) != null) m.Result = new IntPtr(1); // HTCLIENT: use our bounded scrollbar gesture
                        return;
                    }
                    if (m.Msg == 0x0201) // WM_LBUTTONDOWN
                    {
                        Point screen = ClientPoint(m.LParam);
                        ScrollGeometry bar = At(screen);
                        if (bar != null)
                        {
                            if (bar.Enabled) BeginScroll(bar, screen);
                            m.Result = IntPtr.Zero; return;
                        }
                    }
                    if (m.Msg == 0x0200 && held) // WM_MOUSEMOVE while thumb/page is captured
                    {
                        if (dragging && active != null)
                        {
                            ScrollGeometry current = Geometry(active.Horizontal);
                            if (current != null)
                            {
                                int track = Math.Max(1, current.Length - 2 * current.Arrow - (current.ThumbEnd - current.ThumbStart));
                                int pixel = current.Coordinate(ClientPoint(m.LParam)) - current.Arrow - dragOffset;
                                int value = current.Info.Minimum + (int)Math.Round(Math.Max(0, Math.Min(track, pixel)) * (double)(current.Maximum - current.Info.Minimum) / track);
                                MoveTo(current, value);
                            }
                        }
                        m.Result = IntPtr.Zero; return;
                    }
                    if (m.Msg == 0x0202 && held)
                    { EndScroll(); m.Result = IntPtr.Zero; return; }
                    if (m.Msg == 0x0215) EndScroll(false); // WM_CAPTURECHANGED
                }
                int message = m.Msg;
                base.WndProc(ref m);
                if (message == 0x0085 || message == 0x000F || message == 0x0005 || message == 0x000C ||
                    message == 0x0114 || message == 0x0115 || message == 0x020A || message == 0x0100 ||
                    message == 0x0102 || message == 0x00B6 || message == 0x031A || message == 0x007D ||
                    message == 0x0007 || message == 0x0008 || message == 0x02A2)
                    PaintChrome(IntPtr.Zero);
                else if (message == 0x0317) PaintChrome(m.WParam); // WM_PRINT
            }
            private Point ClientPoint(IntPtr packed)
            {
                Point point = PointFrom(packed); NativePoint native = new NativePoint(point.X, point.Y);
                ClientToScreen(Handle, ref native); return new Point(native.X, native.Y);
            }
            private static Point PointFrom(IntPtr packed)
            { long value = packed.ToInt64(); return new Point((short)(value & 0xffff), (short)((value >> 16) & 0xffff)); }
            private void BeginScroll(ScrollGeometry bar, Point screen)
            {
                active = bar; held = true; dragging = false;
                int coordinate = bar.Coordinate(screen);
                if (coordinate >= bar.ThumbStart && coordinate < bar.ThumbEnd)
                { dragging = true; dragOffset = coordinate - bar.ThumbStart; }
                else
                {
                    repeatDirection = coordinate < bar.ThumbStart ? -1 : 1;
                    repeatStep = coordinate < bar.Arrow || coordinate >= bar.Length - bar.Arrow ? (owner is FlowScrollPanel?Math.Max(16,owner.Font.Height):1) : Math.Max(1, (int)bar.Info.Page);
                    MoveTo(bar, bar.Info.Position + repeatDirection * repeatStep);
                    repeat.Interval = 350; repeat.Start();
                }
                SetCapture(Handle); PaintChrome(IntPtr.Zero);
            }
            private void EndScroll(bool release = true)
            {
                if (!held) return;
                held = dragging = false; repeat.Stop(); active = null;
                if (release && GetCapture() == Handle) ReleaseCapture();
                PaintChrome(IntPtr.Zero);
            }
            private void MoveTo(ScrollGeometry bar, int requested)
            {
                int value = Math.Max(bar.Info.Minimum, Math.Min(bar.Maximum, requested));
                if (value == bar.Info.Position) return;
                var scroll = owner as ScrollableControl;
                var rich = owner as RichTextBox;
                var edit = owner as TextBoxBase;
                var list = owner as ListBox;
                var view = owner as ListView;
                if (popup)
                    SendMessage(Handle, 0x0197, new IntPtr(value), IntPtr.Zero); // LB_SETTOPINDEX
                else if (rich != null)
                {
                    NativePoint point = new NativePoint();
                    SendPoint(Handle, 0x04DD, IntPtr.Zero, ref point); // EM_GETSCROLLPOS
                    if (bar.Horizontal) point.X = value; else point.Y = value;
                    SendPoint(Handle, 0x04DE, IntPtr.Zero, ref point); // EM_SETSCROLLPOS
                }
                else if (edit != null)
                {
                    int first = SendMessage(Handle, 0x00CE, IntPtr.Zero, IntPtr.Zero).ToInt32(); // EM_GETFIRSTVISIBLELINE
                    SendMessage(Handle, 0x00B6, new IntPtr(bar.Horizontal ? value - bar.Info.Position : 0), new IntPtr(bar.Horizontal ? 0 : value - first));
                }
                else if (list != null && !bar.Horizontal)
                    list.TopIndex = Math.Max(0, Math.Min(list.Items.Count - 1, value));
                else if (view != null)
                {
                    int row = view.Items.Count > 0 ? Math.Max(1, view.GetItemRect(Math.Max(0, Math.Min(view.Items.Count - 1, view.TopItem == null ? 0 : view.TopItem.Index))).Height) : owner.Font.Height + 4;
                    SendMessage(Handle, 0x1014, new IntPtr(bar.Horizontal ? value - bar.Info.Position : 0), new IntPtr(bar.Horizontal ? 0 : (value - bar.Info.Position) * row)); // LVM_SCROLL
                }
                else if (owner is FlowScrollPanel)
                    ((FlowScrollPanel)owner).AutoScrollPosition = bar.Horizontal ? new Point(value, -((FlowScrollPanel)owner).AutoScrollPosition.Y) : new Point(-((FlowScrollPanel)owner).AutoScrollPosition.X, value);
                else if (scroll != null)
                    scroll.AutoScrollPosition = bar.Horizontal ? new Point(value, -scroll.AutoScrollPosition.Y) : new Point(-scroll.AutoScrollPosition.X, value);
                else
                    SendMessage(Handle, bar.Horizontal ? 0x0114 : 0x0115, new IntPtr(4 | ((value & 0xffff) << 16)), IntPtr.Zero);
                PaintChrome(IntPtr.Zero);
            }
            private ScrollGeometry At(Point screen)
            {
                ScrollGeometry vertical = Geometry(false);
                if (vertical != null && vertical.Bounds.Contains(screen)) return vertical;
                ScrollGeometry horizontal = Geometry(true);
                return horizontal != null && horizontal.Bounds.Contains(screen) ? horizontal : null;
            }
            private ScrollGeometry Geometry(bool horizontal)
            {
                if (Handle == IntPtr.Zero) return null;
                var panel = owner as FlowScrollPanel;
                if (panel != null)
                {
                    var properties=horizontal?panel.HorizontalScroll:panel.VerticalScroll;
                    if(!properties.Visible)return null;
                    Point origin=panel.PointToScreen(horizontal?new Point(0,panel.ClientSize.Height):new Point(panel.ClientSize.Width,0));
                    Rectangle bounds=new Rectangle(origin,horizontal?new Size(panel.ClientSize.Width,SystemInformation.HorizontalScrollBarHeight):new Size(SystemInformation.VerticalScrollBarWidth,panel.ClientSize.Height));
                    return MakeGeometry(horizontal,bounds,new ScrollInfo{Minimum=properties.Minimum,Maximum=properties.Maximum,Page=(uint)Math.Max(1,properties.LargeChange),Position=properties.Value});
                }
                ScrollBarInfo native = new ScrollBarInfo { Size = Marshal.SizeOf(typeof(ScrollBarInfo)), States = new int[6] };
                if (!GetScrollBarInfo(Handle, horizontal ? -6 : -5, ref native) || (native.States[0] & (0x8000 | 0x10000)) != 0) return null;
                Rectangle rectangle = native.Rectangle.ToRectangle();
                if (rectangle.Width < 2 || rectangle.Height < 2) return null;
                ScrollInfo info = new ScrollInfo { Size = Marshal.SizeOf(typeof(ScrollInfo)), Mask = 0x17 };
                if (!GetScrollInfo(Handle, horizontal ? 0 : 1, ref info)) return null;
                return MakeGeometry(horizontal,rectangle,info);
            }
            private static ScrollGeometry MakeGeometry(bool horizontal,Rectangle rectangle,ScrollInfo info)
            {
                int length=horizontal?rectangle.Width:rectangle.Height;
                int cross=horizontal?rectangle.Height:rectangle.Width;
                int arrow=Math.Min(cross,Math.Max(0,length/2));
                int track=Math.Max(1,length-arrow*2);
                int maximum=Math.Max(info.Minimum,info.Maximum-(int)Math.Max(1u,info.Page)+1);
                int range=Math.Max(1,info.Maximum-info.Minimum+1);
                int thumb=Math.Max(Math.Min(12,track),Math.Min(track,(int)Math.Round(track*Math.Min((double)range,info.Page)/range)));
                int offset=maximum<=info.Minimum?0:(int)Math.Round((track-thumb)*(double)(info.Position-info.Minimum)/(maximum-info.Minimum));
                return new ScrollGeometry { Horizontal=horizontal,Bounds=rectangle,Arrow=arrow,
                    ThumbStart=arrow+offset,ThumbEnd=arrow+offset+thumb,Info=info };
            }
            private void PaintChrome(IntPtr target)
            {
                if (painting || Handle == IntPtr.Zero || owner.IsDisposed || !IsWindowVisible(Handle)) return;
                NativeRect window, client;
                if (!GetWindowRect(Handle, out window) || !GetClientRect(Handle, out client)) return;
                if (window.Right <= window.Left || window.Bottom <= window.Top) return;
                painting = true;
                IntPtr dc = target == IntPtr.Zero ? GetWindowDC(Handle) : target;
                try
                {
                    if (dc == IntPtr.Zero) return;
                    using (Graphics graphics = Graphics.FromHdc(dc))
                    {
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        if (header) { PaintHeaderRemainder(graphics, window); return; }
                        NativePoint origin = new NativePoint(0, 0); ClientToScreen(Handle, ref origin);
                        Rectangle clientInWindow = new Rectangle(origin.X - window.Left, origin.Y - window.Top, client.Right, client.Bottom);
                        var saved = graphics.Save();
                        graphics.ExcludeClip(clientInWindow);
                        Color back = owner.BackColor.A == 0 ? Ui.Surface : owner.BackColor;
                        using (Brush brush = new SolidBrush(back)) graphics.FillRectangle(brush, 0, 0, window.Right - window.Left, window.Bottom - window.Top);
                        bool border = popup || owner is TextBoxBase && ((TextBoxBase)owner).BorderStyle != BorderStyle.None ||
                            owner is ListBox && ((ListBox)owner).BorderStyle != BorderStyle.None || owner is ListView && ((ListView)owner).BorderStyle != BorderStyle.None;
                        graphics.Restore(saved);
                        DrawScrollbar(graphics, Geometry(false), window);
                        DrawScrollbar(graphics, Geometry(true), window);
                        if (border)
                            using (Pen pen = new Pen(owner.Focused ? Ui.Blue : Ui.Line))
                                graphics.DrawRectangle(pen, 0, 0, window.Right - window.Left - 1, window.Bottom - window.Top - 1);
                    }
                }
                finally { if (target == IntPtr.Zero && dc != IntPtr.Zero) ReleaseDC(Handle, dc); painting = false; }
            }
            private void PaintHeaderRemainder(Graphics graphics, NativeRect window)
            {
                ListView view = owner as ListView;
                if (view == null) return;
                int right = view.Columns.Cast<ColumnHeader>().Sum(c => c.Width);
                int width = window.Right - window.Left, height = window.Bottom - window.Top;
                if (right < width)
                    using (Brush brush = new SolidBrush(Ui.Raised)) graphics.FillRectangle(brush, right, 0, width - right, height);
                using (Pen pen = new Pen(Ui.Line)) graphics.DrawLine(pen, 0, height - 1, width, height - 1);
            }
            private void DrawScrollbar(Graphics graphics, ScrollGeometry bar, NativeRect window)
            {
                if (bar == null) return;
                Rectangle rectangle = bar.Bounds; rectangle.Offset(-window.Left, -window.Top);
                Color background = owner.BackColor.A == 0 ? Ui.Surface : owner.BackColor;
                using (Brush brush = new SolidBrush(background)) graphics.FillRectangle(brush, rectangle);
                float inset = Math.Max(3, (bar.Horizontal ? rectangle.Height : rectangle.Width) * .25f);
                if (bar.Enabled && bar.ThumbEnd > bar.ThumbStart)
                {
                    RectangleF thumb = bar.Horizontal ?
                        new RectangleF(rectangle.Left + bar.ThumbStart, rectangle.Top + inset, bar.ThumbEnd - bar.ThumbStart, rectangle.Height - inset * 2) :
                        new RectangleF(rectangle.Left + inset, rectangle.Top + bar.ThumbStart, rectangle.Width - inset * 2, bar.ThumbEnd - bar.ThumbStart);
                    using (GraphicsPath path = Ui.Rounded(thumb, Math.Min(thumb.Width, thumb.Height) / 2))
                    using (Brush brush = new SolidBrush(held ? Ui.Muted : Ui.Line)) graphics.FillPath(brush, path);
                }
                if (bar.Arrow <= 0) return;
                float size = Math.Max(2, (bar.Horizontal ? rectangle.Height : rectangle.Width) * .19f);
                using (Pen pen = new Pen(bar.Enabled ? Ui.Muted : Ui.Line, 1.3f))
                {
                    if (bar.Horizontal)
                    {
                        float y = rectangle.Top + rectangle.Height / 2f;
                        Chevron(graphics, pen, rectangle.Left + bar.Arrow / 2f, y, size, true, false);
                        Chevron(graphics, pen, rectangle.Right - bar.Arrow / 2f, y, size, true, true);
                    }
                    else
                    {
                        float x = rectangle.Left + rectangle.Width / 2f;
                        Chevron(graphics, pen, x, rectangle.Top + bar.Arrow / 2f, size, false, false);
                        Chevron(graphics, pen, x, rectangle.Bottom - bar.Arrow / 2f, size, false, true);
                    }
                }
            }
        }

        internal static void Chevron(Graphics graphics, Pen pen, float x, float y, float size, bool horizontal, bool positive)
        {
            float sign = positive ? 1 : -1;
            PointF[] points = horizontal ? new[] { new PointF(x - sign * size / 2, y - size), new PointF(x + sign * size / 2, y), new PointF(x - sign * size / 2, y + size) } :
                new[] { new PointF(x - size, y - sign * size / 2), new PointF(x, y + sign * size / 2), new PointF(x + size, y - sign * size / 2) };
            graphics.DrawLines(pen, points);
        }

        [StructLayout(LayoutKind.Sequential)] internal struct NativeRect
        {
            internal int Left, Top, Right, Bottom;
            internal NativeRect(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
            internal Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint
        { internal int X, Y; internal NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] private struct ScrollBarInfo
        { internal int Size; internal NativeRect Rectangle; internal int LineButton, ThumbTop, ThumbBottom, Reserved; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] internal int[] States; }
        [StructLayout(LayoutKind.Sequential)] private struct ScrollInfo
        { internal int Size, Mask, Minimum, Maximum; internal uint Page; internal int Position, TrackPosition; }
        [StructLayout(LayoutKind.Sequential)] internal struct ComboInfo
        { internal int Size; internal NativeRect Item, Button; internal int ButtonState; internal IntPtr Combo, Edit, List; }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string subAppName, string subIdList);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
        [DllImport("user32.dll")] private static extern bool GetScrollBarInfo(IntPtr window, int objectId, ref ScrollBarInfo info);
        [DllImport("user32.dll")] private static extern bool GetScrollInfo(IntPtr window, int bar, ref ScrollInfo info);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetCapture();
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr window, IntPtr updateRectangle, IntPtr updateRegion, int flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendRect(IntPtr window, int message, IntPtr wParam, ref NativeRect rectangle);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendPoint(IntPtr window, int message, IntPtr wParam, ref NativePoint point);
        [DllImport("user32.dll")] internal static extern bool GetComboBoxInfo(IntPtr window, ref ComboInfo info);
    }

    internal sealed class FlowTextBox : TextBox
    {
        internal FlowTextBox() { BorderStyle = BorderStyle.FixedSingle; AutoSize = false; Height = 30; }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeTheme.Apply(this); NativeTheme.PadEdit(this); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); NativeTheme.PadEdit(this); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); NativeTheme.PadEdit(this); }
    }
    internal sealed class FlowRichTextBox : RichTextBox
    {
        internal FlowRichTextBox() { BorderStyle = BorderStyle.None; }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeTheme.Apply(this); }
    }
    internal class FlowScrollPanel : Panel
    {
        private bool scrolling, arranging, vertical, horizontal;
        private Size minimum, extent;
        private Point position;
        private readonly ConditionalWeakTable<Control,object> focusHooks=new ConditionalWeakTable<Control,object>();
        internal sealed class Range
        {
            private readonly FlowScrollPanel panel;private readonly bool horizontal;
            internal Range(FlowScrollPanel panel,bool horizontal){this.panel=panel;this.horizontal=horizontal;}
            internal int Minimum {get{return 0;}}
            internal int Maximum {get{Size size=panel.extent;return Math.Max(0,(horizontal?size.Width:size.Height)-1);}}
            internal int LargeChange {get{return horizontal?panel.ClientSize.Width:panel.ClientSize.Height;}}
            internal int Value {get{Point point=panel.position;return -(horizontal?point.X:point.Y);}}
            internal bool Visible {get{return horizontal?panel.horizontal:panel.vertical;}}
        }
        internal new Range VerticalScroll {get{return new Range(this,false);}}
        internal new Range HorizontalScroll {get{return new Range(this,true);}}
        internal FlowScrollPanel(){DoubleBuffered=true;ResizeRedraw=true;}
        protected override CreateParams CreateParams {get{var value=base.CreateParams;value.Style&=~0x00300000;return value;}}
        public override bool AutoScroll {get{return scrolling;}set{if(scrolling==value)return;scrolling=value;PerformLayout();}}
        [System.ComponentModel.DefaultValue(typeof(Size),"0, 0")]
        public new Size AutoScrollMinSize {get{return minimum;}set{if(minimum==value)return;minimum=value;if(value!=Size.Empty)scrolling=true;PerformLayout();}}
        [System.ComponentModel.Browsable(false),System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public new Point AutoScrollPosition {get{return position;}set{ScrollTo(Math.Max(0,value.X),Math.Max(0,value.Y));}}
        public override Rectangle DisplayRectangle {get{return scrolling?new Rectangle(position.X+Padding.Left,position.Y+Padding.Top,Math.Max(ClientSize.Width,extent.Width)-Padding.Horizontal,Math.Max(ClientSize.Height,extent.Height)-Padding.Vertical):base.DisplayRectangle;}}
        // Never enter ScrollableControl's native SetScrollInfo/WS_VSCROLL path.
        // Layout and scrolling use our range; the HWND has no system scrollbars.
        protected override void AdjustFormScrollbars(bool displayScrollbars){}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateRange();NativeTheme.Apply(this);}
        protected override void OnLayout(LayoutEventArgs e)
        {
            if(arranging)return;arranging=true;
            try{base.OnLayout(e);UpdateRange();base.OnLayout(e);}finally{arranging=false;}
            NativeTheme.Apply(this);
        }
        private void UpdateRange()
        {
            int width=minimum.Width,height=minimum.Height;
            if(scrolling)foreach(Control c in Controls)if(c.Visible){
                if(c.Dock==DockStyle.None){width=Math.Max(width,c.Right-position.X+Padding.Right);height=Math.Max(height,c.Bottom-position.Y+Padding.Bottom);}
                else if(c.Dock==DockStyle.Top||c.Dock==DockStyle.Bottom)height=Math.Max(height,c.Bottom-position.Y+Padding.Bottom);
                else if(c.Dock==DockStyle.Left||c.Dock==DockStyle.Right)width=Math.Max(width,c.Right-position.X+Padding.Right);
            }
            extent=scrolling?new Size(Math.Max(0,width),Math.Max(0,height)):Size.Empty;
            bool v=false,h=false;int fullWidth=Width-(BorderStyle==BorderStyle.None?0:SystemInformation.BorderSize.Width*2),fullHeight=Height-(BorderStyle==BorderStyle.None?0:SystemInformation.BorderSize.Height*2);
            for(int i=0;i<3;i++){v=scrolling&&height>fullHeight-(h?SystemInformation.HorizontalScrollBarHeight:0);h=scrolling&&width>fullWidth-(v?SystemInformation.VerticalScrollBarWidth:0);}
            if(v!=vertical||h!=horizontal){vertical=v;horizontal=h;if(IsHandleCreated)SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x0027);}
            ScrollTo(-position.X,-position.Y);
        }
        internal void ScrollTo(int x,int y)
        {
            x=scrolling?Math.Max(0,Math.Min(Math.Max(0,extent.Width-ClientSize.Width),x)):0;
            y=scrolling?Math.Max(0,Math.Min(Math.Max(0,extent.Height-ClientSize.Height),y)):0;
            Point next=new Point(-x,-y);if(next==position)return;Point old=position;position=next;
            bool previous=arranging;arranging=true;SuspendLayout();
            try{
                // Moving each child separately lets USER32 copy pixels from its
                // previous position while siblings are still there. Queue all
                // positions without copying or painting those stale rectangles.
                var children=Controls.Cast<Control>().ToArray();
                var destinations=children.Select(c=>new Point(c.Left+next.X-old.X,c.Top+next.Y-old.Y)).ToArray();
                IntPtr batch=NativeTheme.IsWindows&&IsHandleCreated&&children.Length>0?BeginDeferWindowPos(children.Length):IntPtr.Zero;
                for(int i=0;i<children.Length&&batch!=IntPtr.Zero;i++)if(children[i].IsHandleCreated)
                    batch=DeferWindowPos(batch,children[i].Handle,IntPtr.Zero,destinations[i].X,destinations[i].Y,0,0,0x011D); // NOREDRAW | NOCOPYBITS | NOSIZE | NOZORDER | NOACTIVATE
                if(batch!=IntPtr.Zero)EndDeferWindowPos(batch);
                // Also covers controls without HWNDs and a failed deferred batch.
                for(int i=0;i<children.Length;i++)children[i].Location=destinations[i];
            }finally{ResumeLayout(false);arranging=previous;}
            if(old.X!=next.X)OnScroll(new ScrollEventArgs(ScrollEventType.ThumbPosition,-old.X,x,ScrollOrientation.HorizontalScroll));
            if(old.Y!=next.Y)OnScroll(new ScrollEventArgs(ScrollEventType.ThumbPosition,-old.Y,y,ScrollOrientation.VerticalScroll));
            // Finish the whole viewport (including child HWNDs) in this gesture.
            // Invalidating only the parent leaves duplicated cards until another
            // paint message, especially during repeated wheel/thumb movement.
            if(NativeTheme.IsWindows&&IsHandleCreated)RedrawWindow(Handle,IntPtr.Zero,IntPtr.Zero,0x0585); // INVALIDATE | ERASE | ALLCHILDREN | UPDATENOW | FRAME
            else Invalidate(true);
            NativeTheme.Apply(this);
        }
        public new void ScrollControlIntoView(Control control)
        {
            if(!scrolling||control==null||!Contains(control)||!control.Visible)return;
            var box=RectangleToClient(control.RectangleToScreen(control.ClientRectangle));int x=-position.X,y=-position.Y;
            if(box.Top<Padding.Top)y+=box.Top-Padding.Top;else if(box.Bottom>ClientSize.Height-Padding.Bottom)y+=box.Bottom-ClientSize.Height+Padding.Bottom;
            if(box.Left<Padding.Left)x+=box.Left-Padding.Left;else if(box.Right>ClientSize.Width-Padding.Right)x+=box.Right-ClientSize.Width+Padding.Right;
            ScrollTo(x,y);
        }
        protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);HookFocus(e.Control);}
        private void HookFocus(Control c){object found;if(focusHooks.TryGetValue(c,out found))return;focusHooks.Add(c,new object());c.Enter+=delegate{ScrollControlIntoView(c);};c.ControlAdded+=delegate(object s,ControlEventArgs e){HookFocus(e.Control);};foreach(Control child in c.Controls)HookFocus(child);}
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if(!scrolling){base.OnMouseWheel(e);return;}
            int step=SystemInformation.MouseWheelScrollLines<0?Math.Max(1,ClientSize.Height):Math.Max(1,SystemInformation.MouseWheelScrollLines)*Math.Max(16,Font.Height);
            int delta=(int)Math.Round(e.Delta*step/120.0);if(vertical)ScrollTo(-position.X,-position.Y-delta);else if(horizontal)ScrollTo(-position.X-delta,-position.Y);
            var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(Focused&&scrolling){int y=-position.Y;switch(keyData){case Keys.PageDown:y+=ClientSize.Height;break;case Keys.PageUp:y-=ClientSize.Height;break;case Keys.Home:y=0;break;case Keys.End:y=extent.Height;break;default:return base.ProcessCmdKey(ref msg,keyData);}ScrollTo(-position.X,y);return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x007C&&m.WParam.ToInt64()==-16){var style=(StyleChange)Marshal.PtrToStructure(m.LParam,typeof(StyleChange));style.Next&=~0x00300000;Marshal.StructureToPtr(style,m.LParam,false);}
            base.WndProc(ref m);
            if(m.Msg==0x0083){var rect=(NativeTheme.NativeRect)Marshal.PtrToStructure(m.LParam,typeof(NativeTheme.NativeRect));if(vertical)rect.Right=Math.Max(rect.Left,rect.Right-SystemInformation.VerticalScrollBarWidth);if(horizontal)rect.Bottom=Math.Max(rect.Top,rect.Bottom-SystemInformation.HorizontalScrollBarHeight);Marshal.StructureToPtr(rect,m.LParam,false);m.Result=IntPtr.Zero;}
        }
        protected override AccessibleObject CreateAccessibilityInstance(){return new PanelAccessibility(this);}
        private sealed class PanelAccessibility : ControlAccessibleObject
        {
            private readonly FlowScrollPanel panel;
            internal PanelAccessibility(FlowScrollPanel panel):base(panel){this.panel=panel;}
            public override int GetChildCount(){return panel.Controls.Count+(panel.vertical?1:0)+(panel.horizontal?1:0);}
            public override AccessibleObject GetChild(int index){int count=panel.Controls.Count;if(index<0)return null;if(index<count)return panel.Controls[index].AccessibilityObject;index-=count;if(panel.vertical){if(index==0)return new ScrollAccessibility(panel,false);index--;}return panel.horizontal&&index==0?new ScrollAccessibility(panel,true):null;}
        }
        private sealed class ScrollAccessibility : AccessibleObject
        {
            private readonly FlowScrollPanel panel;private readonly bool horizontal;
            internal ScrollAccessibility(FlowScrollPanel panel,bool horizontal){this.panel=panel;this.horizontal=horizontal;}
            public override string Name {get{return horizontal?"Scorrimento orizzontale":"Scorrimento verticale";}}
            public override AccessibleRole Role {get{return AccessibleRole.ScrollBar;}}
            public override AccessibleObject Parent {get{return panel.AccessibilityObject;}}
            public override AccessibleStates State {get{return AccessibleStates.Focusable;}}
            public override string Value {get{return (horizontal?panel.HorizontalScroll:panel.VerticalScroll).Value.ToString(System.Globalization.CultureInfo.InvariantCulture);}set{int number;if(Int32.TryParse(value,out number)){Point current=panel.position;panel.ScrollTo(horizontal?number:-current.X,horizontal?-current.Y:number);}}}
            public override Rectangle Bounds {get{Point start=panel.PointToScreen(horizontal?new Point(0,panel.ClientSize.Height):new Point(panel.ClientSize.Width,0));return new Rectangle(start,horizontal?new Size(panel.ClientSize.Width,SystemInformation.HorizontalScrollBarHeight):new Size(SystemInformation.VerticalScrollBarWidth,panel.ClientSize.Height));}}
            public override void Select(AccessibleSelection flags){panel.Focus();}
        }
        [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [StructLayout(LayoutKind.Sequential)]private struct StyleChange {internal int Previous,Next;}
        [DllImport("user32.dll")]private static extern IntPtr BeginDeferWindowPos(int count);
        [DllImport("user32.dll")]private static extern IntPtr DeferWindowPos(IntPtr batch,IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll")]private static extern bool EndDeferWindowPos(IntPtr batch);
        [DllImport("user32.dll")]private static extern bool RedrawWindow(IntPtr h,IntPtr rect,IntPtr region,uint flags);
    }

    internal sealed class FlowComboBox : ComboBox
    {
        internal FlowComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed; DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat;
            IntegralHeight = true;
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeTheme.Apply(this); }
        protected override void OnDropDown(EventArgs e)
        {
            base.OnDropDown(e);
            if (NativeTheme.IsWindows)
            {
                NativeTheme.ComboInfo info = new NativeTheme.ComboInfo { Size = Marshal.SizeOf(typeof(NativeTheme.ComboInfo)) };
                if (NativeTheme.GetComboBoxInfo(Handle, ref info)) NativeTheme.AttachAuxiliary(this, info.List, false);
            }
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (Brush brush = new SolidBrush(selected ? Ui.Hover : Ui.InputBackground)) e.Graphics.FillRectangle(brush, e.Bounds);
            string value = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
            Rectangle text = e.Bounds; text.Inflate(-7, 0);
            TextRenderer.DrawText(e.Graphics, value, Font, text, Enabled ? Ui.Ink : Ui.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        protected override void WndProc(ref Message m)
        {
            int message = m.Msg; base.WndProc(ref m);
            bool printing=(message==0x0317||message==0x0318)&&m.WParam!=IntPtr.Zero;
            if (message == 0x000F || message == 0x0007 || message == 0x0008 || message == 0x0111 || printing)
            {
                if (!IsHandleCreated || IsDisposed || ClientSize.Width < 8 || ClientSize.Height < 8) return;
                using (Graphics graphics = printing?Graphics.FromHdc(m.WParam):Graphics.FromHwnd(Handle))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    // WM_PRINT can share the parent's target DC. Clear would erase
                    // neighboring controls already rendered into that bitmap.
                    Color paper=Parent==null?Ui.Surface:Parent.BackColor;
                    if(printing){using(var fill=new SolidBrush(paper))graphics.FillRectangle(fill,ClientRectangle);}else graphics.Clear(paper);
                    RectangleF rectangle = new RectangleF(.5f, .5f, Width - 1, Height - 1);
                    using (GraphicsPath path = Ui.Rounded(rectangle, 6))
                    using (Brush brush = new SolidBrush(Ui.InputBackground))
                    using (Pen pen = new Pen(Focused ? Ui.Blue : Ui.Line)) { graphics.FillPath(brush, path); graphics.DrawPath(pen, path); }
                    int arrowWidth = Math.Max(25, (int)Math.Round(27 * NativeTheme.DpiScale(this)));
                    Rectangle text = new Rectangle(9, 1, Math.Max(1, Width - arrowWidth - 12), Height - 2);
                    TextRenderer.DrawText(graphics, Text, Font, text, Enabled ? Ui.Ink : Ui.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    using (Pen pen = new Pen(Enabled ? Ui.Muted : Ui.Line, 1.4f)) NativeTheme.Chevron(graphics, pen, Width - arrowWidth / 2f, Height / 2f, Math.Max(3, arrowWidth * .14f), false, true);
                }
            }
        }
    }

    internal sealed class FlowListView : ListView
    {
        internal Func<ListViewItem,int,Color> CellColor=null;
        internal FlowListView()
        {
            OwnerDraw = true; BorderStyle = BorderStyle.FixedSingle; DoubleBuffered = true;
            FullRowSelect = true; HideSelection = false;
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeTheme.Apply(this); AttachHeader(); }
        protected override void OnColumnWidthChanged(ColumnWidthChangedEventArgs e) { base.OnColumnWidthChanged(e); AttachHeader(); }
        private void AttachHeader()
        { if (NativeTheme.IsWindows && IsHandleCreated) NativeTheme.AttachAuxiliary(this, NativeTheme.SendMessage(Handle, 0x101F, IntPtr.Zero, IntPtr.Zero), true); }
        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (Brush brush = new SolidBrush(Ui.Raised)) e.Graphics.FillRectangle(brush, e.Bounds);
            Rectangle text = e.Bounds; text.Inflate(-8, 0);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Font, text, Ui.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (Pen pen = new Pen(Ui.Line)) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }
        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        { if (View != View.Details) e.DrawDefault = true; }
        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            using (Brush brush = new SolidBrush(e.Item.Selected ? Ui.Hover : Ui.InputBackground)) e.Graphics.FillRectangle(brush, e.Bounds);
            Rectangle text = e.Bounds; text.Inflate(-8, 0);
            Color ink=CellColor==null?Ui.Ink:CellColor(e.Item,e.ColumnIndex);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font, text, ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class FlowCheckBox : CheckBox
    {
        internal FlowCheckBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand;ForeColor=Ui.Ink;BackColor=Ui.Surface; }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) { ChoicePaint.Draw(this, e.Graphics, false, Checked, ShowFocusCues && Focused, ShowKeyboardCues); }
    }
    internal sealed class FlowRadioButton : RadioButton
    {
        internal FlowRadioButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand;ForeColor=Ui.Ink;BackColor=Ui.Surface; }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) { ChoicePaint.Draw(this, e.Graphics, true, Checked, ShowFocusCues && Focused, ShowKeyboardCues); }
    }
    internal static class ChoicePaint
    {
        internal static void Draw(Control control, Graphics graphics, bool radio, bool selected, bool focus, bool keyboard)
        {
            graphics.Clear(control.BackColor.A == 0 ? control.Parent == null ? Ui.Surface : control.Parent.BackColor : control.BackColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = graphics.DpiX / 96f, side = 15 * scale, y = (control.Height - side) / 2;
            RectangleF box = new RectangleF(scale, y, side, side);
            Color border = focus ? Ui.Blue : Ui.Muted;
            using (Brush brush = new SolidBrush(selected ? Ui.Blue : Ui.InputBackground))
            using (Pen pen = new Pen(border, Math.Max(1, scale)))
            {
                if (radio) { graphics.FillEllipse(brush, box); graphics.DrawEllipse(pen, box); }
                else using (GraphicsPath path = Ui.Rounded(box, 3 * scale)) { graphics.FillPath(brush, path); graphics.DrawPath(pen, path); }
            }
            if (selected)
            {
                Color mark = Ui.IsDark ? Ui.InputBackground : Color.White;
                if (radio)
                    using (Brush brush = new SolidBrush(mark)) graphics.FillEllipse(brush, box.Left + side * .3f, box.Top + side * .3f, side * .4f, side * .4f);
                else using (Pen pen = new Pen(mark, 1.8f * scale))
                    graphics.DrawLines(pen, new[] { new PointF(box.Left + side * .2f, box.Top + side * .52f), new PointF(box.Left + side * .43f, box.Top + side * .73f), new PointF(box.Left + side * .82f, box.Top + side * .29f) });
            }
            int left = (int)Math.Round(24 * scale);
            Rectangle text = new Rectangle(left, 0, Math.Max(1, control.Width - left - 2), control.Height);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            if (!keyboard) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(graphics, control.Text, control.Font, text, control.Enabled ? control.ForeColor : Ui.Muted, flags);
        }
    }
}
