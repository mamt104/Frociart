using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Frociart
{
    public sealed class StatusEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public StatusEventArgs(string message) { Message = message ?? string.Empty; }
    }

    /// <summary>A native, double-buffered flowchart editor. Model coordinates are in logical pixels.</summary>
    public partial class FlowCanvas : Control
    {
        private FlowAppearance appearance=new FlowAppearance(FlowAppearance.Current);
        internal FlowAppearance AppearanceStyle {get{return appearance;}}
        // The initial owned fonts already match this design. Replacing them
        // with equal fonts makes WinForms retain the old Control.Font, which
        // must not then be disposed while the control still uses it.
        private int fontKey=FlowDesign.Default.FontKey;
        internal void SetAppearance(int mode){SetAppearance(new FlowAppearance(mode));}
        internal void SetAppearance(FlowAppearance style){
            var next=new FlowAppearance(style.Mode,style.CustomDesign);bool resized=fontKey!=next.Design.FontKey;appearance=next;
            if(resized){var design=appearance.Design;var body=new Font(design.FontName,15.333f*design.Scale,FontStyle.Regular,GraphicsUnit.Pixel);var bold=new Font(design.FontName,15.333f*design.Scale,FontStyle.Bold,GraphicsUnit.Pixel);var decision=new Font(design.FontName,14*design.Scale,FontStyle.Regular,GraphicsUnit.Pixel);var label=new Font(design.FontName,10*design.Scale,FontStyle.Bold,GraphicsUnit.Pixel);var oldBody=_bodyFont;var oldBold=_bodyBoldFont;var oldDecision=_decisionFont;var oldLabel=_labelFont;_bodyFont=body;_bodyBoldFont=bold;_decisionFont=decision;_labelFont=label;Font=body;oldBody.Dispose();oldBold.Dispose();oldDecision.Dispose();oldLabel.Dispose();fontKey=design.FontKey;cycleFoldDocument=null;foldProjection=null;sizeMotion.Clear();_textLayout.Clear();ClearScene();}
            RequestFrameRepaint();
        }
        internal RectangleF DesignBounds(FlowNode node){return node==null?RectangleF.Empty:new RectangleF(new PointF(node.X,node.Y),FlowSizing.Size(node,appearance.Design));}
        private bool _exportRendering;
        private bool Dark { get { return !_exportRendering && Ui.IsDark; } }
        private Color Paper { get { return appearance.Neutral(_exportRendering ? Color.FromArgb(245, 245, 247) : Ui.Paper); } }
        private Color Ink { get { return appearance.Neutral(_exportRendering ? Color.FromArgb(29, 29, 31) : Ui.Ink); } }
        private Color Muted { get { return appearance.Neutral(_exportRendering ? Color.FromArgb(110, 110, 115) : Ui.Muted); } }
        private Color Accent { get { return _exportRendering ? Color.FromArgb(0, 122, 255) : Ui.Blue; } }
        private Color Green { get { return Dark ? Color.FromArgb(74, 195, 167) : Color.FromArgb(18, 145, 120); } }
        private Color Rose { get { return Dark ? Color.FromArgb(235, 137, 162) : Color.FromArgb(201, 88, 116); } }

        private Diagram _diagram;
        private float _zoom = 1f;
        private float _panX = 40f;
        private float _panY = 40f;
        private readonly HashSet<string> _selectedNodeIds = new HashSet<string>();
        private string _selectedNodeId { get { return _selectedNodeIds.Count == 1 ? _selectedNodeIds.First() : null; } }
        private string _selectedEdgeId;
        private string _currentNodeId;
        private string _currentEdgeId;
        private ISet<string> _issueNodeIds = new HashSet<string>();
        private string _hoverNodeId;
        private string _connectSourceId;
        private string _connectBranch;
        private Point _lastMouse;
        private Point _mouseDown;
        private PointF _dragNodeOrigin;
        private PointF _dragWorldOrigin;
        private FlowNode _dragNode;
        private Dictionary<string, PointF> _dragOrigins;
        private HashSet<string> _selectionBeforeGesture;
        private string _edgeBeforeGesture;
        private string _collapseOnClickId;
        private string _toggleOffOnClickId;
        private bool _dragging,_dragMoved;
        private bool _marquee;
        private bool _marqueeMoved;
        private bool _marqueeAdditive;
        private PointF _marqueeOrigin;
        private PointF _marqueeCurrent;
        private bool _panning;
        private bool _panMoved;
        private bool _connectionPointerActive;
        private bool _connectionPointerFromPort;
        private bool _connectionPointerFromBlank;
        private bool _pickerAutoPlace;
        private PointF _connectionPointerOrigin;
        private float _panOriginX;
        private float _panOriginY;
        private readonly ToolTip _toolTip;
        private readonly Timer _pulseTimer;
        private ContextMenuStrip _menu;
        private bool _menuQueued;
        private Point _menuLocation;
        private ToolStripMenuItem _menuEdit, _menuNext, _menuTrue, _menuFalse, _menuDuplicate, _menuDelete;
        private ToolStripMenuItem _menuCancel, _menuAdd, _menuSelectAll;
        private ToolStripSeparator _menuConnectionSeparator, _menuEditSeparator, _menuSelectionSeparator;
        private BlockPicker _blockPicker;
        private Diagram _pickerDocument;
        private string _pickerSourceId, _pickerBranch;
        private PointF _pickerCenter;
        private bool _pickerQueued;
        private int _pickerGeneration;
        private Font _bodyFont = new Font("Segoe UI", 15.333f, FontStyle.Regular, GraphicsUnit.Pixel);
        private Font _bodyBoldFont = new Font("Segoe UI", 15.333f, FontStyle.Bold, GraphicsUnit.Pixel);
        private Font _decisionFont = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        private Font _labelFont = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font _smallFont = new Font("Segoe UI", 11.333f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font _smallBoldFont = new Font("Segoe UI", 11.067f, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font _emptyTitleFont = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Pixel);
        private Diagram _sceneDocument;
        private NodeVisual[] _nodeVisuals = new NodeVisual[0];
        private NodeVisual[] _nodesByTop = new NodeVisual[0];
        private EdgeVisual[] _edgeVisuals = new EdgeVisual[0];
        private readonly Dictionary<string, NodeVisual> _nodeIndex = new Dictionary<string, NodeVisual>();
        private readonly Dictionary<string, EdgeVisual> _edgeIndex = new Dictionary<string, EdgeVisual>();
        private RectangleF _contentBounds;
        private Bitmap _gridTile;
        private TextureBrush _gridBrush;
        private GridPattern _gridPattern;
        private float _gridSpacing;
        private bool _gridDark;
        private readonly Dictionary<TextLayoutKey, bool> _textLayout = new Dictionary<TextLayoutKey, bool>();
        private FrameSurface _frame;
        private readonly List<Rectangle> _frameDirty = new List<Rectangle>();
        private bool _frameDirtyAll = true;
        private bool _retainFrame;
        private bool _frameDark;
        private float _frameZoom, _framePanX, _framePanY;

        // Keep the already rendered pixels. Panning moves this surface and repaints only
        // newly exposed strips; pointer overlays never require rebuilding the diagram.
        // Windows uses a native DIB, so presentation and scrolling are plain BitBlt calls
        // rather than a full-viewport GDI+ texture/compositing pass on every mouse move.
        private sealed class FrameSurface : IDisposable
        {
            internal readonly int Width, Height;
            internal readonly float DpiX, DpiY;
            internal Graphics Graphics;
            private IntPtr _dc, _bitmap, _previousBitmap;
            private Bitmap _portable;
            private const uint SourceCopy = 0x00CC0020;

            [StructLayout(LayoutKind.Sequential)]
            private struct BitmapInfo
            {
                internal uint Size;
                internal int Width, Height;
                internal ushort Planes, Bits;
                internal uint Compression, ImageSize;
                internal int XPixelsPerMeter, YPixelsPerMeter;
                internal uint ColorsUsed, ImportantColors, Color;
            }
            [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr source);
            [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
            [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
            [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
            [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
            [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);
            [DllImport("gdi32.dll")] private static extern bool GdiFlush();

            internal FrameSurface(int width, int height, Graphics reference)
            {
                Width = width; Height = height;
                float dpiX = reference.DpiX, dpiY = reference.DpiY;
                DpiX = dpiX; DpiY = dpiY;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    IntPtr referenceDc = IntPtr.Zero;
                    try
                    {
                        referenceDc = reference.GetHdc();
                        _dc = CreateCompatibleDC(referenceDc);
                        BitmapInfo info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, Bits = 32 };
                        IntPtr pixels = IntPtr.Zero;
                        if (_dc != IntPtr.Zero) _bitmap = CreateDIBSection(referenceDc, ref info, 0, out pixels, IntPtr.Zero, 0);
                        if (_bitmap != IntPtr.Zero)
                        {
                            _previousBitmap = SelectObject(_dc, _bitmap);
                            if (_previousBitmap != IntPtr.Zero && _previousBitmap != new IntPtr(-1))
                            {
                                // Wrap the same DIB pixels for GDI+ vector drawing. This
                                // avoids the slower HDC drawing path while retaining a
                                // zero-copy native surface for scrolling/presentation.
                                _portable = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, pixels);
                                _portable.SetResolution(dpiX, dpiY);
                                Graphics = System.Drawing.Graphics.FromImage(_portable);
                            }
                        }
                    }
                    catch (ExternalException) { }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }
                    catch (OutOfMemoryException) { Dispose(); throw; }
                    finally { if (referenceDc != IntPtr.Zero) reference.ReleaseHdc(referenceDc); }
                    if (Graphics == null)
                    {
                        if (_portable != null) { _portable.Dispose(); _portable = null; }
                        ReleaseNative();
                    }
                }
                if (Graphics == null)
                {
                    _portable = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                    _portable.SetResolution(dpiX, dpiY);
                    Graphics = System.Drawing.Graphics.FromImage(_portable);
                }
            }

            internal bool Scroll(int dx, int dy)
            {
                int width = Width - Math.Abs(dx), height = Height - Math.Abs(dy);
                if (width <= 0 || height <= 0) return false;
                int x = Math.Max(0, dx), y = Math.Max(0, dy), sx = Math.Max(0, -dx), sy = Math.Max(0, -dy);
                Graphics.Flush(FlushIntention.Sync);
                if (_dc != IntPtr.Zero)
                {
                    bool copied = BitBlt(_dc, x, y, width, height, _dc, sx, sy, SourceCopy);
                    GdiFlush();
                    return copied;
                }
                using (Bitmap copy = _portable.Clone(new Rectangle(sx, sy, width, height), PixelFormat.Format32bppPArgb))
                {
                    GraphicsState state = Graphics.Save();
                    Graphics.CompositingMode = CompositingMode.SourceCopy;
                    Graphics.CompositingQuality = CompositingQuality.HighSpeed;
                    Graphics.DrawImageUnscaled(copy, x, y);
                    Graphics.Restore(state);
                }
                return true;
            }

            internal bool Present(Graphics target, Rectangle clip)
            {
                Graphics.Flush(FlushIntention.Sync);
                if (_dc != IntPtr.Zero)
                {
                    IntPtr targetDc = target.GetHdc();
                    try
                    {
                        bool copied = BitBlt(targetDc, clip.X, clip.Y, clip.Width, clip.Height, _dc, clip.X, clip.Y, SourceCopy);
                        GdiFlush();
                        return copied;
                    }
                    finally { target.ReleaseHdc(targetDc); }
                }
                else
                {
                    GraphicsState state = target.Save();
                    target.SetClip(clip, CombineMode.Intersect);
                    target.CompositingMode = CompositingMode.SourceCopy;
                    target.CompositingQuality = CompositingQuality.HighSpeed;
                    target.DrawImageUnscaled(_portable, 0, 0);
                    target.Restore(state);
                    return true;
                }
            }

            private void ReleaseNative()
            {
                if (_dc != IntPtr.Zero && _previousBitmap != IntPtr.Zero && _previousBitmap != new IntPtr(-1)) SelectObject(_dc, _previousBitmap);
                if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
                if (_dc != IntPtr.Zero) DeleteDC(_dc);
                _dc = _bitmap = _previousBitmap = IntPtr.Zero;
            }
            public void Dispose()
            {
                if (Graphics != null) { Graphics.Dispose(); Graphics = null; }
                if (_portable != null) { _portable.Dispose(); _portable = null; }
                ReleaseNative();
            }
        }

        // The paper is an opaque, unscaled repeating bitmap. GDI's pattern fill
        // copies those pixels directly; GDI+ TextureBrush would resample the whole
        // viewport even when only the node positions or zoom have changed.
        private sealed class GridPattern : IDisposable
        {
            private IntPtr _bitmap, _brush;

            [StructLayout(LayoutKind.Sequential)]
            private struct NativeRectangle
            {
                internal int Left, Top, Right, Bottom;
            }
            [DllImport("gdi32.dll")] private static extern IntPtr CreatePatternBrush(IntPtr bitmap);
            [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
            [DllImport("gdi32.dll")] private static extern int SaveDC(IntPtr dc);
            [DllImport("gdi32.dll")] private static extern bool RestoreDC(IntPtr dc, int saved);
            [DllImport("gdi32.dll")] private static extern bool SetBrushOrgEx(IntPtr dc, int x, int y, IntPtr previous);
            [DllImport("gdi32.dll")] private static extern int ExtSelectClipRgn(IntPtr dc, IntPtr region, int mode);
            [DllImport("user32.dll")] private static extern int FillRect(IntPtr dc, ref NativeRectangle rectangle, IntPtr brush);

            internal GridPattern(Bitmap tile)
            {
                try
                {
                    _bitmap = tile.GetHbitmap();
                    if (_bitmap != IntPtr.Zero) _brush = CreatePatternBrush(_bitmap);
                }
                catch (ExternalException) { Dispose(); }
                catch (DllNotFoundException) { Dispose(); }
                catch (EntryPointNotFoundException) { Dispose(); }
                catch (OutOfMemoryException) { Dispose(); }
                if (_brush == IntPtr.Zero) Dispose();
            }

            internal bool Fill(Graphics graphics, Rectangle clip, int phaseX, int phaseY)
            {
                if (_brush == IntPtr.Zero) return false;
                IntPtr dc = IntPtr.Zero, clipRegion = IntPtr.Zero;
                int saved = 0;
                try
                {
                    // GetHdc does not inherit a GDI+ clip. Carry it across as well as
                    // the requested dirty rectangle, including nonrectangular clips.
                    using (Region region = graphics.Clip)
                        if (!region.IsInfinite(graphics)) clipRegion = region.GetHrgn(graphics);
                    dc = graphics.GetHdc();
                    saved = SaveDC(dc);
                    if (saved == 0) return false;
                    if (clipRegion != IntPtr.Zero && ExtSelectClipRgn(dc, clipRegion, 1) == 0) return false;
                    if (!SetBrushOrgEx(dc, phaseX, phaseY, IntPtr.Zero)) return false;
                    NativeRectangle rectangle = new NativeRectangle { Left = clip.Left, Top = clip.Top, Right = clip.Right, Bottom = clip.Bottom };
                    return FillRect(dc, ref rectangle, _brush) != 0;
                }
                catch (ExternalException) { return false; }
                catch (DllNotFoundException) { return false; }
                catch (EntryPointNotFoundException) { return false; }
                catch (OutOfMemoryException) { return false; }
                finally
                {
                    if (saved != 0) RestoreDC(dc, saved);
                    if (dc != IntPtr.Zero) graphics.ReleaseHdc(dc);
                    if (clipRegion != IntPtr.Zero) DeleteObject(clipRegion);
                }
            }

            public void Dispose()
            {
                if (_brush != IntPtr.Zero) { DeleteObject(_brush); _brush = IntPtr.Zero; }
                if (_bitmap != IntPtr.Zero) { DeleteObject(_bitmap); _bitmap = IntPtr.Zero; }
            }
        }

        private sealed class NodeVisual : IDisposable
        {
            internal FlowNode Model;
            internal int Index;
            internal string Id;
            internal NodeKind Kind;
            internal RectangleF Bounds;
            internal int Generation;
            internal string Text;
            internal int State, PulseFrame;
            internal string TrueLabel, FalseLabel, TrueColor, FalseColor;
            internal int BranchGeneration;
            private GraphicsPath _shape, _selectionHalo;
            private GraphicsPath[] _shadows;
            internal GraphicsPath Shape { get { return _shape ?? (_shape = NodeShape(Kind, Bounds)); } }
            internal GraphicsPath SelectionHalo
            {
                get
                {
                    if (_selectionHalo == null)
                    {
                        RectangleF bounds = Bounds; bounds.Inflate(5, 5);
                        _selectionHalo = NodeShape(Kind, bounds);
                    }
                    return _selectionHalo;
                }
            }
            internal GraphicsPath Shadow(int offset)
            {
                if (_shadows == null) _shadows = new GraphicsPath[3];
                if (_shadows[offset - 1] == null)
                {
                    RectangleF bounds = Bounds; bounds.Offset(0, offset + 1);
                    _shadows[offset - 1] = NodeShape(Kind, bounds);
                }
                return _shadows[offset - 1];
            }
            public void Dispose()
            {
                if (_shape != null) { _shape.Dispose(); _shape = null; }
                if (_selectionHalo != null) { _selectionHalo.Dispose(); _selectionHalo = null; }
                if (_shadows != null)
                {
                    foreach (GraphicsPath path in _shadows) if (path != null) path.Dispose();
                    _shadows = null;
                }
            }
        }

        private sealed class EdgeVisual : IDisposable
        {
            internal PointF[] HitPoints;
            internal FlowEdge Model;
            internal int Index;
            internal string Id, From, To, Branch;
            internal NodeVisual Source, Target, Peer;
            internal int SourceGeneration, TargetGeneration, PeerGeneration, RoutingGeneration;
            internal List<PointF> Points;
            internal RectangleF Bounds;
            internal bool UsesCorridor;
            internal float CorridorTop, CorridorBottom;
            internal int State;
            internal int BranchGeneration;
            internal float LabelPadding = 36;
            private GraphicsPath _path;
            internal GraphicsPath Path { get { return _path ?? (_path = RoundedRoute(Points, 10)); } }
            public void Dispose() { HitPoints=null; if (_path != null) { _path.Dispose(); _path = null; } }
        }

        private struct TextLayoutKey : IEquatable<TextLayoutKey>
        {
            internal string Text;
            internal Font Font;
            internal float Width, Height, Dpi;
            public bool Equals(TextLayoutKey other)
            {
                return Text == other.Text && ReferenceEquals(Font, other.Font) && Width == other.Width && Height == other.Height && Dpi == other.Dpi;
            }
            public override bool Equals(object obj) { return obj is TextLayoutKey && Equals((TextLayoutKey)obj); }
            public override int GetHashCode()
            {
                unchecked { return ((((Text.GetHashCode() * 397) ^ Font.GetHashCode()) * 397 ^ Width.GetHashCode()) * 397 ^ Height.GetHashCode()) * 397 ^ Dpi.GetHashCode(); }
            }
        }

        public event EventHandler SelectionChanged;
        public event EventHandler EditRequested;
        public event EventHandler BreakpointRequested;
        public event EventHandler RunToRequested;public Func<bool> RunToEnabled;private ToolStripMenuItem _menuRunTo;
        private ToolStripMenuItem _menuBreakpoint;
        public event EventHandler DiagramChanging;
        public event EventHandler DiagramChanged;
        public event EventHandler ViewChanged;
        public event EventHandler ConnectionStateChanged;
        public event EventHandler<StatusEventArgs> StatusChanged;

        private bool viewOnly;
        [System.ComponentModel.DefaultValue(false)]
        public bool ViewOnly {get{return viewOnly;}set{if(viewOnly==value)return;if(value){CancelPointerGesture(true,true);CancelConnection();InvalidateNode(_hoverNodeId,24);hoverPorts.Clear();_hoverNodeId=null;}viewOnly=value;NotifyAccessibleModeChanged();RequestFrameRepaint();}}

        public FlowCanvas()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable | ControlStyles.Opaque, true);
            InitManipulation();
            BackColor = Paper;
            ForeColor = Ink;
            Font = _bodyFont;
            TabStop = true;
            SnapToGrid = true;
            _toolTip = new ToolTip { InitialDelay = 700, ReshowDelay = 200, AutoPopDelay = 6000 };
            _pulseTimer = new Timer { Interval = 10 };
            _pulseTimer.Tick += delegate { TickExperience(); };
            AccessibleName = "Editor del diagramma di flusso";
            AccessibleDescription = "PagGiù e PagSu selezionano il blocco successivo o precedente. Home ed End raggiungono il primo o l’ultimo blocco. Invio modifica il blocco; durante Collega conferma il blocco di arrivo. Maiusc+F10 apre i comandi della selezione. Le frecce spostano i blocchi selezionati. Trascina dal menu su una freccia per creare e inserire. Tieni premuto un blocco per staccarlo. Trascina con il sinistro sullo sfondo per selezionare. Tieni premuta la rotella e trascina per spostare la vista. Collega nel vuoto per creare un nuovo blocco. Destro trascinato sulle frecce: taglia i collegamenti al rilascio. Esc annulla il gesto. Ctrl+Z ripristina. Doppio clic modifica. Canc elimina. Rotellina ingrandisce.";
            Ui.ThemeChanged += OnThemeChanged;
            SetAppearance(appearance);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Diagram Diagram
        {
            get { return _diagram; }
            set
            {
                if(!ReferenceEquals(_diagram,value)){StopClipboardFlight();StopBladeFeedback();}
                CancelPointerGesture(false, false);
                ClearFlowHistory();
                StopTrashMotion();settleFrom=null;arrangeCamera=false;landingId=null;
                if(!ReferenceEquals(_diagram,value)){cycleFold=null;cycleFoldDocument=null;foldProjection=null;}
                _diagram = value;foldModelDirty=true;
                NotifyAccessibleStructure();
                ClearScene();
                CancelConnection(false);
                _selectedNodeIds.RemoveWhere(id => FindNode(id) == null);
                if (FindEdge(_selectedEdgeId) == null) _selectedEdgeId = null;
                pendingDropPositions=null;pendingDropId=null;pendingDropDetached=false;hoverPorts.Clear();_hoverNodeId = null;
                _toolTip.SetToolTip(this, string.Empty);
                RequestFrameRepaint();
                RaiseSelectionChanged();
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string SelectedNodeId
        {
            get { return _selectedNodeId; }
            set { SelectNode(value); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public IReadOnlyCollection<string> SelectedNodeIds
        {
            get { return SelectedNodes().Select(n => n.Id).ToList().AsReadOnly(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int SelectionCount { get { return _selectedNodeIds.Count; } }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string SelectedEdgeId
        {
            get { return _selectedEdgeId; }
            set { SelectEdge(value); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string CurrentNodeId
        {
            get { return _currentNodeId; }
            set
            {
                if (_currentNodeId == value) return;
                if(value==null)ClearFlowHistory();
                InvalidateNode(FoldOwner(_currentNodeId), 20);
                _currentNodeId = value;
                InvalidateNode(FoldOwner(_currentNodeId), 20);
                UpdatePulseTimer();
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string CurrentEdgeId
        {
            get { return _currentEdgeId; }
            set
            {
                if (_currentEdgeId == value) return;
                InvalidateEdge(_currentEdgeId);
                _currentEdgeId = value;
                if(value==null)ClearFlowHistory();
                BeginFlowTravel(value);
                InvalidateEdge(_currentEdgeId);
                UpdatePulseTimer();
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public ISet<string> IssueNodeIds
        {
            get { return _issueNodeIds; }
            set { _issueNodeIds = value ?? new HashSet<string>(); RequestFrameRepaint(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool SnapToGrid { get; set; }
        public bool IsConnecting { get { return !string.IsNullOrEmpty(_connectSourceId); } }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ConnectionSourceId { get { return _connectSourceId; } }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ConnectionBranch { get { return _connectBranch; } }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsBlockPickerOpen { get { return _pickerQueued || (_pickerDocument != null && _blockPicker != null && _blockPicker.Visible); } }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsPointerGestureActive { get { return _dragNode != null || _marquee || _panning || _connectionPointerActive || rewireEdge != null || bladePending; } }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public float Zoom
        {
            get { return _zoom; }
            set { SetZoomAt(value, new Point(ClientSize.Width / 2, ClientSize.Height / 2)); }
        }

        public static RectangleF GetNodeBounds(FlowNode node)
        {
            if (node == null) return RectangleF.Empty;
            return new RectangleF(new PointF(node.X,node.Y),FlowSizing.Size(node));
        }

        public PointF ClientToWorld(Point point)
        {
            return new PointF((point.X - _panX) / _zoom, (point.Y - _panY) / _zoom);
        }

        public PointF WorldToClient(PointF point)
        {
            return new PointF(point.X * _zoom + _panX, point.Y * _zoom + _panY);
        }

        public void ZoomBy(float factor)
        {
            if (factor > 0 && !float.IsNaN(factor) && !float.IsInfinity(factor)) Zoom = _zoom * factor;
        }

        public void SelectNode(string id)
        {
            CancelPointerGesture(false, false);EnsureScene();RevealFold(id);
            SetSelection(id == null ? Enumerable.Empty<string>() : new[] { id }, null);
        }

        public void SelectNodes(IEnumerable<string> ids)
        {
            CancelPointerGesture(false, false);
            SetSelection(ids, null);
        }

        public void SelectAllNodes()
        {
            CancelPointerGesture(false, false);
            CancelConnection(false);
            SetSelection(_diagram == null ? Enumerable.Empty<string>() : _diagram.Nodes.Select(n => n.Id), null);
        }

        public void SelectEdge(string id)
        {
            CancelPointerGesture(false, false);
            SetSelection(Enumerable.Empty<string>(), id);
        }

        private List<FlowNode> SelectedNodes()
        {
            return _diagram == null ? new List<FlowNode>() : _diagram.Nodes.Where(n => _selectedNodeIds.Contains(n.Id)).ToList();
        }

        private void SetSelection(IEnumerable<string> ids, string edgeId)
        {
            HashSet<string> next = new HashSet<string>(FoldSelection(ids ?? Enumerable.Empty<string>()));
            next.RemoveWhere(id => FindNode(id) == null);
            if (next.Count != 0 || FindEdge(edgeId) == null) edgeId = null;
            if (_selectedNodeIds.SetEquals(next) && _selectedEdgeId == edgeId) return;
            _selectedNodeIds.Clear();
            _selectedNodeIds.UnionWith(next);
            _selectedEdgeId = edgeId;
            RequestFrameRepaint();
            RaiseSelectionChanged();
        }

        public FlowNode AddNode(NodeKind kind)
        {
            PointF center = ClientToWorld(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
            return AddNodeAt(kind, center);
        }

        // Palette clicks create loose blocks. Keep their silhouette clear of
        // existing blocks and routes so proximity never implies a connection.
        public FlowNode AddLooseNode(NodeKind kind)
        {
            if (_diagram == null || !CanAddNode()) return null;
            PointF center = ClientToWorld(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
            var obstacles = _diagram.Nodes.Select(DesignBounds).ToList();
            foreach (FlowEdge edge in _diagram.Edges)
            {
                FlowNode from = FindNode(edge.From), to = FindNode(edge.To);
                if (from == null || to == null) continue;
                var route = GetExportRoute(from, to, edge.Branch);
                for (int i = 1; i < route.Count; i++)
                    obstacles.Add(RectangleF.FromLTRB(Math.Min(route[i-1].X, route[i].X),
                        Math.Min(route[i-1].Y, route[i].Y), Math.Max(route[i-1].X, route[i].X),
                        Math.Max(route[i-1].Y, route[i].Y)));
            }
            float clearance = Math.Max(18, 14 / _zoom);
            for (int i = 0; i < obstacles.Count; i++)
            { var rect = obstacles[i]; rect.Inflate(clearance, clearance); obstacles[i] = rect; }
            var points = new List<PointF> { center };
            const int margin = 24, stride = 48;
            for (int y = margin; y < ClientSize.Height - margin; y += stride)
                for (int x = margin; x < ClientSize.Width - margin; x += stride)
                    points.Add(ClientToWorld(new Point(x, y)));
            RectangleF view = RectangleF.FromLTRB(ClientToWorld(new Point(margin, margin)).X,
                ClientToWorld(new Point(margin, margin)).Y,
                ClientToWorld(new Point(ClientSize.Width-margin, ClientSize.Height-margin)).X,
                ClientToWorld(new Point(ClientSize.Width-margin, ClientSize.Height-margin)).Y);
            PointF? chosen = null;
            foreach (PointF point in points.OrderBy(p => (p.X-center.X)*(p.X-center.X)+(p.Y-center.Y)*(p.Y-center.Y)))
            {
                RectangleF bounds = DisplayBounds(CreateNodeCandidate(kind, point));
                if (view.Contains(bounds) && !obstacles.Any(r => r.IntersectsWith(bounds))) { chosen = point; break; }
            }
            if (!chosen.HasValue)
            {
                // A crowded viewport still permits a clearly separate block.
                var content = GetContentBounds();
                var size = DisplayBounds(new FlowNode { Kind = kind, Text = DefaultText(kind) });
                chosen = new PointF(content.Right + clearance * 2 + size.Width / 2,
                    content.Top + size.Height / 2);
            }
            FlowNode node = AddNodeAt(kind, chosen.Value);
            if (node != null)
            {
                if (!view.Contains(DisplayBounds(node))) CenterOnNode(node.Id);
                Report("Casella scollegata aggiunta. Usa + sul blocco precedente per inserirne una nel percorso.");
            }
            return node;
        }

        private FlowNode AddNodeAt(NodeKind kind, PointF center)
        {
            CancelPointerGesture(false, false);
            if (_diagram == null) return null;
            if (!CanAddNode()) return null;
            FlowNode node = CreateNodeCandidate(kind, center);
            var placementBefore=ModelPositions();
            RaiseDiagramChanging();
            _diagram.Nodes.Add(node);
            FlowPlacement.Reflow(_diagram,node.Id,null,null,false);
            SelectNode(node.Id);
            RaiseDiagramChanged();placementBefore[node.Id]=new PointF(node.X,node.Y+16/_zoom);StartLanding(node.Id);AnimatePlacement(placementBefore);
            Focus();
            Report("Blocco aggiunto. Fai doppio clic per modificarlo.");
            return node;
        }

        private FlowNode CreateNodeCandidate(NodeKind kind, PointF center)
        {
            FlowNode node = new FlowNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = kind,
                Text = DefaultText(kind),
                Breakpoint = kind==NodeKind.Pause,
                // Empty means automatic: the interpreter uses the current variable name.
                Prompt = string.Empty
            };
            RectangleF size = DisplayBounds(node);
            node.X = Snap(center.X - size.Width / 2);
            node.Y = Snap(center.Y - size.Height / 2);
            int attempts = 0;
            while (attempts++ < 24 && _diagram.Nodes.Any(n => Math.Abs(n.X - node.X) < 24 && Math.Abs(n.Y - node.Y) < 24))
            {
                node.X = ClampCoordinate(node.X + 24);
                node.Y = ClampCoordinate(node.Y + 24);
            }
            return node;
        }

        public FlowNode DuplicateSelected()
        {
            CancelPointerGesture(false, false);
            List<FlowNode> originals = SelectedNodes();
            if (originals.Count == 0 || _diagram == null) return null;
            if (ProjectNodeCount + originals.Count > DiagramIO.MaxNodes)
            {
                Report("Non c'è spazio per duplicare la selezione: il limite è " + DiagramIO.MaxNodes + " blocchi.");
                return null;
            }
            List<FlowEdge> internalEdges = _diagram.Edges.Where(e => _selectedNodeIds.Contains(e.From) && _selectedNodeIds.Contains(e.To)).ToList();
            if (ProjectEdgeCount + internalEdges.Count > DiagramIO.MaxEdges)
            {
                Report("Non c'è spazio per duplicare i collegamenti: il limite è " + DiagramIO.MaxEdges + ".");
                return null;
            }
            Dictionary<string, PointF> origins = originals.ToDictionary(n => n.Id, n => new PointF(n.X, n.Y));
            PointF offset;
            if (!TryConstrainOffset(origins, new PointF(36, 36), out offset)) return null;
            if (offset.X == 0 && offset.Y == 0) TryConstrainOffset(origins, new PointF(-36, -36), out offset);
            RaiseDiagramChanging();
            Dictionary<string, string> copies = new Dictionary<string, string>();
            FlowNode firstCopy = null;
            foreach (FlowNode original in originals)
            {
                FlowNode node = new FlowNode
                {
                    Id = Guid.NewGuid().ToString("N"), Kind = original.Kind,
                    Text = original.Text, Prompt = original.Prompt, Comment=original.Comment, FoldedCycle=original.FoldedCycle, Breakpoint=original.Breakpoint, BreakpointCondition=original.BreakpointCondition,
                    TrueLabel = original.TrueLabel, FalseLabel = original.FalseLabel,
                    TrueColor = original.TrueColor, FalseColor = original.FalseColor,
                    X = original.X + offset.X, Y = original.Y + offset.Y
                };
                _diagram.Nodes.Add(node);
                copies.Add(original.Id, node.Id);
                if (firstCopy == null) firstCopy = node;
            }
            foreach (FlowEdge edge in internalEdges)
                _diagram.Edges.Add(new FlowEdge { Id = Guid.NewGuid().ToString("N"), From = copies[edge.From], To = copies[edge.To], Branch = edge.Branch });
            SetSelection(copies.Values, null);
            RaiseDiagramChanged();
            Report(originals.Count == 1 ? "Blocco duplicato." : originals.Count + " blocchi duplicati con i loro collegamenti interni.");
            return firstCopy;
        }

        public void DeleteSelection()
        {
            CancelPointerGesture(false, false);
            if (_diagram == null) return;
            HashSet<string> nodes = new HashSet<string>(SelectedNodes().Select(n => n.Id));
            FlowEdge edge = FindEdge(_selectedEdgeId);
            if (nodes.Count == 0 && edge == null) return;
            RaiseDiagramChanging();
            if (nodes.Count != 0)
            {
                _diagram.Edges.RemoveAll(e => nodes.Contains(e.From) || nodes.Contains(e.To));
                _diagram.Nodes.RemoveAll(n => nodes.Contains(n.Id));
                if (_connectSourceId != null && nodes.Contains(_connectSourceId)) CancelConnection(false);
            }
            else _diagram.Edges.Remove(edge);
            SetSelection(Enumerable.Empty<string>(), null);
            RaiseDiagramChanged();
            Report(nodes.Count == 0 ? "Collegamento eliminato." : nodes.Count == 1 ? "Blocco eliminato." : nodes.Count + " blocchi eliminati.");
        }

        public void BeginConnect(string nodeId, string branch)
        {
            ClearBlockPickerSession();
            FlowNode node = FindNode(nodeId);
            if (node == null) return;
            if (node.Kind == NodeKind.Note) { Report("Le note restano libere sul foglio."); return; }
            if (node.Kind == NodeKind.End)
            {
                Report("Il blocco Fine termina il percorso e non ha un'uscita.");
                return;
            }
            _connectSourceId = nodeId;
            _connectBranch = node.Kind == NodeKind.Decision ? (branch == "false" ? "false" : "true") : "next";
            _lastMouse = PointToClient(Cursor.Position);
            SelectNode(nodeId);
            Cursor = Cursors.Cross;
            Focus();
            RequestFrameRepaint();
            RaiseConnectionStateChanged();
            Report(ConnectionHint());
        }

        public void CancelConnection()
        {
            CancelPointerGesture(true, true);
            CancelConnection(true);
        }

        public void CancelBlockPicker()
        {
            CancelConnection();
        }

        private bool fitViewLimited;
        public void FitToView()
        {
            arrangeCamera=false;
            RectangleF bounds = GetContentBounds();
            if (bounds.IsEmpty) { bool wasLimited=fitViewLimited;fitViewLimited=false;ResetView();if(wasLimited)Report("Il foglio è vuoto.");return; }
            float width = Math.Max(80, ClientSize.Width - 100);
            float height = Math.Max(80, ClientSize.Height - 100);
            float requestedZoom=Math.Min(width / bounds.Width, height / bounds.Height);
            bool previousLimit=fitViewLimited;fitViewLimited=requestedZoom<0.18f;
            _zoom = Clamp(requestedZoom, 0.18f, 1.15f);
            _panX = ClientSize.Width / 2f - (bounds.X + bounds.Width / 2f) * _zoom;
            _panY = ClientSize.Height / 2f - (bounds.Y + bounds.Height / 2f) * _zoom;
            RequestFrameRepaint();
            RaiseViewChanged();
            if(fitViewLimited)Report("Il diagramma non entra interamente nella vista. Scorri o cerca un blocco per raggiungerlo.");
            else if(previousLimit)Report("Diagramma inquadrato.");
        }

        public void CenterOnNode(string id)
        {
            FlowNode node = FindNode(id);
            if (node == null) return;
            RectangleF bounds = DisplayBounds(node);
            _panX = ClientSize.Width / 2f - (bounds.X + bounds.Width / 2f) * _zoom;
            _panY = ClientSize.Height / 2f - (bounds.Y + bounds.Height / 2f) * _zoom;
            RequestFrameRepaint();
            RaiseViewChanged();
        }

        internal void CenterOnNodeAnimated(string id){
            var node=FindNode(FoldOwner(id));if(node==null)return;var bounds=DisplayBounds(node);
            float zoom=Math.Min(_zoom,Math.Min(ClientSize.Width/Math.Max(1,bounds.Width+64),ClientSize.Height/Math.Max(1,bounds.Height+64)));zoom=Math.Max(.18f,zoom);
            float x=ClientSize.Width/2f-(bounds.X+bounds.Width/2)*zoom,y=ClientSize.Height/2f-(bounds.Y+bounds.Height/2)*zoom;
            if(!UiMotion.Enabled){_panX=x;_panY=y;_zoom=zoom;RequestFrameRepaint();RaiseViewChanged();return;}
            arrangeFromX=_panX;arrangeFromY=_panY;arrangeFromZoom=_zoom;arrangeToX=x;arrangeToY=y;arrangeToZoom=zoom;arrangeCamera=true;AnimatePlacement(new Dictionary<string,PointF>());
        }

        internal void CenterViewOn(PointF world)
        {
            _panX=ClientSize.Width/2f-world.X*_zoom;_panY=ClientSize.Height/2f-world.Y*_zoom;
            RequestFrameRepaint();RaiseViewChanged();
        }

        public void ResetView()
        {
            _zoom = 1;
            _panX = 48;
            _panY = 48;
            RequestFrameRepaint();
            RaiseViewChanged();
        }

        public void ExportPng(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Scegli un file PNG.", "path");
            using (Bitmap bitmap = RenderExportBitmap()) bitmap.Save(path, ImageFormat.Png);
            Report("Diagramma esportato in PNG.");
        }

        /// <summary>Logical bounds of all nodes and routed edges, without an export page margin.</summary>
        public RectangleF GetExportContentBounds()
        {
            bool previous=_exportRendering;try{_exportRendering=true;return GetContentBounds();}finally{_exportRendering=previous;}
        }

        /// <summary>A detached, read-only snapshot of the orthogonal route in logical coordinates.</summary>
        public IReadOnlyList<PointF> GetExportRoute(FlowNode source, FlowNode target, string branch)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (target == null) throw new ArgumentNullException("target");
            bool previous=_exportRendering;try{_exportRendering=true;EnsureScene();return RouteEdge(source,target,branch).AsReadOnly();}finally{_exportRendering=previous;}
        }

        /// <summary>Draws the diagram using the caller's transform, without changing editor or graphics state.</summary>
        public void RenderExport(Graphics graphics, RectangleF visible, float scale)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale)) throw new ArgumentOutOfRangeException("scale");
            GraphicsState state = graphics.Save();
            bool wasExportRendering = _exportRendering;
            try
            {
                _exportRendering = true;
                PrepareGraphics(graphics);
                DrawDiagram(graphics, visible, false, scale);
            }
            finally { _exportRendering = wasExportRendering; graphics.Restore(state); }
        }

        /// <summary>The caller owns the bitmap. A 52-unit margin, 8000-pixel side limit, and 20-megapixel limit are applied.</summary>
        public Bitmap RenderExportBitmap(float preferredScale = 2f)
        {
            if (preferredScale <= 0 || float.IsNaN(preferredScale) || float.IsInfinity(preferredScale)) throw new ArgumentOutOfRangeException("preferredScale");
            bool previous=_exportRendering;
            try {
            _exportRendering=true;
            RectangleF content = GetContentBounds();
            if (content.IsEmpty) content = new RectangleF(0, 0, 600, 400);
            content.Inflate(52, 52);
            float scale = Math.Min(preferredScale, Math.Min(8000f / content.Width, 8000f / content.Height));
            scale = Math.Min(scale, (float)Math.Sqrt(20000000.0 / ((double)content.Width * content.Height)));
            int width = Math.Max(1, (int)Math.Ceiling(content.Width * scale));
            int height = Math.Max(1, (int)Math.Ceiling(content.Height * scale));
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            try
            {
                bitmap.SetResolution(96 * scale, 96 * scale);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    PrepareGraphics(graphics);
                    graphics.Clear(Color.White);
                    using (Matrix transform = new Matrix(scale, 0, 0, scale, -content.Left * scale, -content.Top * scale))
                        graphics.Transform = transform;
                    RenderExport(graphics, content, scale);
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
            }finally{_exportRendering=previous;}
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The opaque backing surface (or DrawGrid fallback) supplies every pixel.
        }

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            // Hosts may directly edit the model and call Invalidate. Internal pointer
            // repaint requests retain the surface; model/state snapshots find their dirt.
            if (!_retainFrame) _frameDirtyAll = true;
            base.OnInvalidated(e);
        }

        private void RequestFrameRepaint()
        {
            bool previous = _retainFrame;
            _retainFrame = true;
            try { Invalidate(); }
            finally { _retainFrame = previous; }
        }

        private void RequestFrameRepaint(Rectangle rectangle)
        {
            bool previous = _retainFrame;
            _retainFrame = true;
            try { Invalidate(rectangle); }
            finally { _retainFrame = previous; }
        }

        private void MarkFrameDirty(Rectangle rectangle)
        {
            if (_frameDirtyAll) return;
            rectangle.Intersect(ClientRectangle);
            if (rectangle.Width <= 0 || rectangle.Height <= 0) return;
            // Merge overlapping work so a node and its incident routes redraw together.
            for (int i = 0; i < _frameDirty.Count; i++)
            {
                Rectangle nearby = _frameDirty[i]; nearby.Inflate(2, 2);
                if (!nearby.IntersectsWith(rectangle)) continue;
                Rectangle union = Rectangle.Union(rectangle, _frameDirty[i]);
                long separateArea = (long)rectangle.Width * rectangle.Height + (long)_frameDirty[i].Width * _frameDirty[i].Height;
                // The two exposed strips on a diagonal pan form an L. Keep them
                // separate; their bounding rectangle would redraw the entire screen.
                if ((long)union.Width * union.Height > separateArea * 3 / 2) continue;
                rectangle = union;
                _frameDirty.RemoveAt(i); i = -1;
            }
            if (_frameDirty.Count >= 32 || (long)rectangle.Width * rectangle.Height > (long)ClientSize.Width * ClientSize.Height * 3 / 4)
            {
                _frameDirtyAll = true; _frameDirty.Clear(); return;
            }
            _frameDirty.Add(rectangle);
        }

        private void MarkWorldDirty(RectangleF world, float padding)
        {
            world.Inflate(padding, padding);
            float scale = _frame == null ? _zoom : _frameZoom;
            PointF point = _frame == null ? WorldToClient(world.Location) :
                new PointF(world.X * scale + _framePanX, world.Y * scale + _framePanY);
            int left = (int)Math.Floor(point.X) - 2, top = (int)Math.Floor(point.Y) - 2;
            int right = (int)Math.Ceiling(point.X + world.Width * scale) + 2;
            int bottom = (int)Math.Ceiling(point.Y + world.Height * scale) + 2;
            MarkFrameDirty(Rectangle.FromLTRB(left, top, right, bottom));
        }

        private void PaintSceneRegion(Graphics graphics, Rectangle clip)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.ResetTransform();
                if (clip != ClientRectangle) graphics.SetClip(clip, CombineMode.Intersect);
                PrepareGraphics(graphics);
                graphics.CompositingQuality=CompositingQuality.HighSpeed;
                DrawGrid(graphics, clip);
                PointF a = ClientToWorld(clip.Location);
                PointF b = ClientToWorld(new Point(clip.Right, clip.Bottom));
                RectangleF visible = RectangleF.FromLTRB(a.X - 36, a.Y - 36, b.X + 36, b.Y + 36);
                using (Matrix transform = new Matrix(_zoom, 0, 0, _zoom, _panX, _panY)) graphics.Transform = transform;
                DrawScene(graphics, visible, true, _zoom);
            }
            finally { graphics.Restore(state); }
        }

        private void ExpandDirtyNodes()
        {
            // GDI+ changes its antialias coverage slightly when a clip cuts through a
            // node outline. Repaint intersected nodes completely to avoid visible seams
            // where old pixels meet a newly rendered strip (including selection halos).
            for (int i = 0; i < _frameDirty.Count; i++)
            {
                Rectangle dirty = _frameDirty[i];
                bool grew;
                do
                {
                    grew = false;
                    foreach (NodeVisual node in _nodeVisuals)
                    {
                        float padding = (node.State & 2) != 0 ? 22 : (node.State & 16) != 0 ?
                            (node.Kind == NodeKind.Decision ? NodePaintPadding(node.Model) : 12) : 8;
                        RectangleF world = node.Bounds; world.Inflate(padding, padding);
                        PointF point = WorldToClient(world.Location);
                        Rectangle screen = Rectangle.FromLTRB((int)Math.Floor(point.X) - 2, (int)Math.Floor(point.Y) - 2,
                            (int)Math.Ceiling(point.X + world.Width * _zoom) + 2, (int)Math.Ceiling(point.Y + world.Height * _zoom) + 2);
                        screen.Intersect(ClientRectangle);
                        if (screen.Width <= 0 || screen.Height <= 0 || !dirty.IntersectsWith(screen) || dirty.Contains(screen)) continue;
                        dirty = Rectangle.Union(dirty, screen); grew = true;
                    }
                } while (grew);
                _frameDirty[i] = dirty;
            }
            // Expansion can make two original strips cover the same nodes. Merge
            // compact overlaps while retaining separated L-shaped exposed strips.
            for (int i = 0; i < _frameDirty.Count; i++)
                for (int j = i + 1; j < _frameDirty.Count; j++)
                {
                    Rectangle a = _frameDirty[i], b = _frameDirty[j];
                    if (!a.IntersectsWith(b)) continue;
                    Rectangle union = Rectangle.Union(a, b);
                    long area = (long)a.Width * a.Height + (long)b.Width * b.Height;
                    if ((long)union.Width * union.Height > area) continue;
                    _frameDirty[i] = union; _frameDirty.RemoveAt(j); j = i;
                }
        }

        private bool UpdateFrame(Graphics reference)
        {
            int width = ClientSize.Width, height = ClientSize.Height;
            if (width <= 0 || height <= 0 || (long)width * height > 20000000) return false;
            bool zoomChanged = _frame != null && _frameZoom != _zoom;
            if (_frame == null || _frame.Width != width || _frame.Height != height ||
                _frame.DpiX != reference.DpiX || _frame.DpiY != reference.DpiY)
            {
                if (_frame != null) _frame.Dispose();
                _frame = null;
                try { _frame = new FrameSurface(width, height, reference); }
                catch (OutOfMemoryException) { return false; }
                catch (ExternalException) { return false; }
                _frameDirtyAll = true;
            }
            if (_frameZoom != _zoom || _frameDark != Dark) _frameDirtyAll = true;
            if (!_frameDirtyAll && (_framePanX != _panX || _framePanY != _panY))
            {
                float movedX = _panX - _framePanX, movedY = _panY - _framePanY;
                int dx = (int)Math.Round(movedX), dy = (int)Math.Round(movedY);
                foreach (NodeVisual node in _nodeVisuals)
                {
                    RectangleF old = new RectangleF(node.Bounds.X * _frameZoom + _framePanX, node.Bounds.Y * _frameZoom + _framePanY,
                        node.Bounds.Width * _frameZoom, node.Bounds.Height * _frameZoom);
                    old.Inflate(8 * _frameZoom, 8 * _frameZoom);
                    RectangleF next = old; next.Offset(movedX, movedY);
                    // Text rendering can depend on the device clip. Refresh partially
                    // visible nodes and shadows on both the old and new viewport edge.
                    if ((old.IntersectsWith(ClientRectangle) && (old.Left < 0 || old.Top < 0 || old.Right > width || old.Bottom > height)) ||
                        (next.IntersectsWith(ClientRectangle) && (next.Left < 0 || next.Top < 0 || next.Right > width || next.Bottom > height)))
                        MarkWorldDirty(node.Bounds, NodePaintPadding(node.Model));
                }
                if (Math.Abs(movedX - dx) > 0.001f || Math.Abs(movedY - dy) > 0.001f || !_frame.Scroll(dx, dy)) _frameDirtyAll = true;
                else
                {
                    // Pending model dirt is refreshed below at the new view transform.
                    for (int i = 0; i < _frameDirty.Count; i++)
                    { Rectangle dirty = _frameDirty[i]; dirty.Offset(dx, dy); _frameDirty[i] = dirty; }
                    if (dx > 0) MarkFrameDirty(new Rectangle(0, 0, dx, height));
                    else if (dx < 0) MarkFrameDirty(new Rectangle(width + dx, 0, -dx, height));
                    if (dy > 0) MarkFrameDirty(new Rectangle(0, 0, width, dy));
                    else if (dy < 0) MarkFrameDirty(new Rectangle(0, height + dy, width, -dy));
                }
            }
            _frameZoom = _zoom; _framePanX = _panX; _framePanY = _panY; _frameDark = Dark;
            EnsureScene();
            if (!_frameDirtyAll) ExpandDirtyNodes();
            long dirtyArea = 0;
            foreach (Rectangle dirty in _frameDirty) dirtyArea += (long)dirty.Width * dirty.Height;
            if (!_frameDirtyAll && _frameDirty.Count > 1)
            {
                int fullWork = EstimatePaintWork(ClientRectangle), partialWork = 0;
                foreach (Rectangle dirty in _frameDirty) partialWork += EstimatePaintWork(dirty);
                // Long routes may cross several separate strips. Once those strips
                // would draw as much geometry as the entire viewport, rasterize once
                // instead of paying repeatedly for the same route and label paths.
                if (fullWork > 0 && partialWork >= fullWork) _frameDirtyAll = true;
            }
            // A zoom rerasterizes everything; broad node drags often reroute most of
            // the visible diagram. Render those frames directly, without adding a
            // full-screen cache copy. Rebuild the retained frame on the next settled
            // repaint, when subsequent pointer/selection work can reuse its pixels.
            if (zoomChanged || (_dragging && (_frameDirtyAll || dirtyArea > (long)width * height / 3)))
            {
                _frameDirtyAll = true; _frameDirty.Clear();
                return false;
            }
            if (_frameDirtyAll) PaintSceneRegion(_frame.Graphics, ClientRectangle);
            else for (int i = 0; i < _frameDirty.Count; i++) PaintSceneRegion(_frame.Graphics, _frameDirty[i]);
            _frameDirtyAll = false; _frameDirty.Clear();
            return true;
        }

        private int EstimatePaintWork(Rectangle clip)
        {
            PointF a = ClientToWorld(clip.Location), b = ClientToWorld(new Point(clip.Right, clip.Bottom));
            RectangleF visible = RectangleF.FromLTRB(a.X - 36, a.Y - 36, b.X + 36, b.Y + 36);
            int work = 0;
            foreach (EdgeVisual edge in _edgeVisuals)
            {
                if (edge.Points == null) continue;
                RectangleF bounds = edge.Bounds;
                float padding = edge.LabelPadding > 36 ? edge.LabelPadding : 30;
                bounds.Inflate(padding, padding);
                if (visible.IntersectsWith(bounds) && RouteIntersects(edge.Points, visible, edge.LabelPadding)) work++;
            }
            foreach (NodeVisual node in _nodeVisuals)
            {
                RectangleF bounds = node.Bounds;
                float padding = (node.State & 16) != 0 ? NodePaintPadding(node.Model) : 28;
                bounds.Inflate(padding, padding);
                if (visible.IntersectsWith(bounds)) work += 4;
            }
            return work;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            PrepareGraphics(e.Graphics);
            e.Graphics.CompositingQuality=CompositingQuality.HighSpeed;
            if (!UpdateFrame(e.Graphics) || !_frame.Present(e.Graphics, e.ClipRectangle))
            { EnsureScene(); PaintSceneRegion(e.Graphics, e.ClipRectangle); }
            GraphicsState state = e.Graphics.Save();
            using (Matrix transform = new Matrix(_zoom, 0, 0, _zoom, _panX, _panY)) e.Graphics.Transform = transform;
            PaintBladeEdges(e.Graphics);
            PaintStickerConnections(e.Graphics);
            if (IsConnecting) DrawConnectionPreview(e.Graphics);
            PaintExperience(e.Graphics);
            PaintInsertPreview(e.Graphics);
            PaintManipulationNode(e.Graphics);
            if(_dragging&&_dragNode!=null&&insertHover!=null&&!IsManipulationOverlay(_dragNode)){NodeVisual dragging;if(_nodeIndex.TryGetValue(_dragNode.Id,out dragging))DrawNode(e.Graphics,dragging,true,_zoom);}
            PaintPalettePreview(e.Graphics);
            e.Graphics.Restore(state);
            if (_diagram == null || _diagram.Nodes.Count == 0) DrawEmptyState(e.Graphics);
            if (_marquee && _marqueeMoved) DrawMarquee(e.Graphics);
            if (IsConnecting) DrawConnectionHint(e.Graphics);
            PaintInsertHint(e.Graphics);
            PaintHoldHint(e.Graphics);PaintTrash(e.Graphics);PaintClipboardFlight(e.Graphics);PaintBladeTrail(e.Graphics);
            base.OnPaint(e);
        }

        private static void PrepareGraphics(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        private void DrawGrid(Graphics graphics, Rectangle clip)
        {
            float spacing = GridSnap.Step * _zoom;
            while (spacing < 15) spacing *= 2;
            if (_gridBrush == null || _gridSpacing != spacing || _gridDark != Dark)
            {
                DisposeGrid();
                _gridSpacing = spacing;
                _gridDark = Dark;
                // A tile of sixteen cells keeps fractional zoom spacing accurate while
                // allowing an unscaled native bitmap fill (no per-dot draw calls on pan).
                int tileSize = Math.Max(16, (int)Math.Round(spacing * 16));
                _gridTile = new Bitmap(tileSize, tileSize, PixelFormat.Format32bppPArgb);
                using (Graphics tile = Graphics.FromImage(_gridTile))
                using (Brush dot = new SolidBrush(Dark ? Color.FromArgb(58, 58, 66) : Color.FromArgb(218, 218, 224)))
                {
                    tile.Clear(Paper);
                    tile.SmoothingMode = SmoothingMode.AntiAlias;
                    float cell = tileSize / 16f;
                    for (int x = 0; x < 16; x++)
                        for (int y = 0; y < 16; y++)
                            tile.FillEllipse(dot, (x + 0.5f) * cell - 0.7f, (y + 0.5f) * cell - 0.7f, 1.4f, 1.4f);
                }
                _gridBrush = new TextureBrush(_gridTile, WrapMode.Tile);
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) _gridPattern = new GridPattern(_gridTile);
            }
            float phaseX = (_panX % _gridTile.Width) - _gridTile.Width / 32f;
            float phaseY = (_panY % _gridTile.Height) - _gridTile.Height / 32f;
            int originX = (int)Math.Floor(phaseX), originY = (int)Math.Floor(phaseY);
            // Acquiring a bitmap's HDC has a fixed transfer cost. Keep the cheap
            // texture fill for small pan strips; the native copy wins on broad
            // repaints such as zoom and dragging across the viewport.
            if (_gridPattern != null && (long)clip.Width * clip.Height * 2 >= (long)ClientSize.Width * ClientSize.Height &&
                _gridPattern.Fill(graphics, clip, originX, originY)) return;
            using (Matrix transform = new Matrix(1, 0, 0, 1, originX, originY))
                _gridBrush.Transform = transform;
            CompositingMode mode = graphics.CompositingMode;
            CompositingQuality quality = graphics.CompositingQuality;
            try
            {
                // The grid tile is opaque; copying it avoids alpha compositing the full
                // viewport, and already provides the complete paper background.
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.FillRectangle(_gridBrush, clip);
            }
            finally { graphics.CompositingMode = mode; graphics.CompositingQuality = quality; }
        }

        private void DisposeGrid()
        {
            if (_gridPattern != null) { _gridPattern.Dispose(); _gridPattern = null; }
            if (_gridBrush != null) { _gridBrush.Dispose(); _gridBrush = null; }
            if (_gridTile != null) { _gridTile.Dispose(); _gridTile = null; }
        }

        private void DrawMarquee(Graphics graphics)
        {
            PointF a = WorldToClient(_marqueeOrigin), b = WorldToClient(_marqueeCurrent);
            RectangleF bounds = RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            using (Brush fill = new SolidBrush(Color.FromArgb(26, Accent))) graphics.FillRectangle(fill, bounds);
            using (Pen border = new Pen(Color.FromArgb(200, Accent), 1f))
                graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        // The model is deliberately editable by the inspector and undo history. Compare its
        // inexpensive fields at rendering/input boundaries instead of assuming notifications.
        // Pan, zoom and hover reuse geometry; a moved obstacle also refreshes affected loops.
        private void EnsureScene()
        {
            Diagram scene=CanvasScene();
            if (!ReferenceEquals(_sceneDocument, scene))
            {
                ClearScene();
                _sceneDocument = scene;
            }
            if (scene == null) return;
            bool rebuildNodes = _nodeVisuals.Length != scene.Nodes.Count;
            if (!rebuildNodes)
                for (int i = 0; i < _nodeVisuals.Length; i++)
                    if (!ReferenceEquals(_nodeVisuals[i].Model, scene.Nodes[i])) { rebuildNodes = true; break; }
            bool geometryChanged = rebuildNodes, identitiesChanged = rebuildNodes, routingKindsChanged = rebuildNodes;
            float changedTop = float.PositiveInfinity, changedBottom = float.NegativeInfinity;
            if (rebuildNodes)
            {
                _frameDirtyAll = true;
                foreach (NodeVisual node in _nodeVisuals) node.Dispose();
                _nodeVisuals = new NodeVisual[scene.Nodes.Count];
                for (int i = 0; i < _nodeVisuals.Length; i++)
                {
                    FlowNode model = scene.Nodes[i];
                    _nodeVisuals[i] = new NodeVisual { Model = model, Index = i, Id = model.Id, Kind = model.Kind, Bounds = DisplayBounds(model), Generation = 1, State = -1 };
                }
            }
            else
            {
                for (int i = 0; i < _nodeVisuals.Length; i++)
                {
                    NodeVisual node = _nodeVisuals[i];
                    FlowNode model = node.Model;
                    if (node.Id != model.Id) { node.Id = model.Id; identitiesChanged = true; }
                    RectangleF next = DisplayBounds(model);
                    if (node.Kind == model.Kind && node.Bounds == next) continue;
                    MarkWorldDirty(node.Bounds, NodePaintPadding(model));
                    MarkWorldDirty(next, NodePaintPadding(model));
                    changedTop = Math.Min(changedTop, Math.Min(node.Bounds.Top, next.Top));
                    changedBottom = Math.Max(changedBottom, Math.Max(node.Bounds.Bottom, next.Bottom));
                    if(node.Kind!=model.Kind)routingKindsChanged=true;
                    node.Dispose();
                    node.Kind = model.Kind; node.Bounds = next; node.Generation++;
                    geometryChanged = true;
                }
            }
            int pulseFrame = 0;
            foreach (NodeVisual node in _nodeVisuals)
            {
                FlowNode model = node.Model;
                int state = (_selectedNodeIds.Contains(model.Id) ? 1 : 0) | (model.Id == FoldOwner(_currentNodeId) ? 2 : 0) |
                    (FoldIssue(model.Id) ? 4 : 0) | (model.Id == _hoverNodeId ? 8 : 0) |
                    (ShowsPorts(model) ? 16 : 0) | (IsManipulationOverlay(model)?32:0);
                if (node.Text != FlowSizing.Text(model) || node.State != state || ((state & 2) != 0 && node.PulseFrame != pulseFrame))
                {
                    MarkWorldDirty(node.Bounds, NodePaintPadding(model));
                    node.Text = FlowSizing.Text(model); node.State = state; node.PulseFrame = pulseFrame;
                }
                if (node.TrueLabel != model.TrueLabel || node.FalseLabel != model.FalseLabel ||
                    node.TrueColor != model.TrueColor || node.FalseColor != model.FalseColor)
                {
                    MarkWorldDirty(node.Bounds, 200);
                    node.TrueLabel = model.TrueLabel; node.FalseLabel = model.FalseLabel;
                    node.TrueColor = model.TrueColor; node.FalseColor = model.FalseColor;
                    node.BranchGeneration++;
                }
            }
            if (identitiesChanged)
            {
                _frameDirtyAll = true;
                _nodeIndex.Clear();
                foreach (NodeVisual node in _nodeVisuals)
                    if (node.Id != null && !_nodeIndex.ContainsKey(node.Id)) _nodeIndex.Add(node.Id, node);
            }
            if (geometryChanged)
            {
                if (_nodesByTop.Length != _nodeVisuals.Length) _nodesByTop = new NodeVisual[_nodeVisuals.Length];
                Array.Copy(_nodeVisuals, _nodesByTop, _nodeVisuals.Length);
                Array.Sort(_nodesByTop, delegate(NodeVisual a, NodeVisual b) { return a.Bounds.Top.CompareTo(b.Bounds.Top); });
            }

            bool rebuildEdges = _edgeVisuals.Length != scene.Edges.Count;
            if (!rebuildEdges)
                for (int i = 0; i < _edgeVisuals.Length; i++)
                    if (!ReferenceEquals(_edgeVisuals[i].Model, scene.Edges[i])) { rebuildEdges = true; break; }
            if (rebuildEdges)
            {
                _frameDirtyAll = true;
                foreach (EdgeVisual edge in _edgeVisuals) edge.Dispose();
                _edgeVisuals = new EdgeVisual[scene.Edges.Count];
                for (int i = 0; i < _edgeVisuals.Length; i++) _edgeVisuals[i] = new EdgeVisual { Model = scene.Edges[i], Index = i };
            }
            bool edgeIndexChanged = rebuildEdges, contentChanged = geometryChanged || identitiesChanged || rebuildEdges;
            bool routingTopologyChanged=rebuildEdges||identitiesChanged||routingKindsChanged;
            foreach(EdgeVisual edge in _edgeVisuals)
                if(edge.Id!=edge.Model.Id||edge.From!=edge.Model.From||edge.To!=edge.Model.To||edge.Branch!=edge.Model.Branch){routingTopologyChanged=true;break;}
            if(routingTopologyChanged){branchRoutes=BuildBranchRoutes(scene);branchTopologyGeneration++;}
            if(geometryChanged)branchGeometryGeneration++;
            foreach (EdgeVisual edge in _edgeVisuals)
            {
                FlowEdge model = edge.Model;
                int state = (model.Id == _selectedEdgeId ? 1 : 0) | (model.Id == _currentEdgeId ? 2 : 0) | (flowHistory.Contains(model.Id) ? 4 : 0) | (SkipStickerEdge(model)?8:0);
                int branchGeneration = edge.Source == null ? 0 : edge.Source.BranchGeneration;
                if (edge.State != state || edge.BranchGeneration != branchGeneration)
                {
                    if (edge.BranchGeneration != branchGeneration) contentChanged = true;
                    MarkWorldDirty(edge.Bounds, edge.LabelPadding);
                    edge.State = state; edge.BranchGeneration = branchGeneration;
                    if (edge.Source != null) edge.LabelPadding = BranchLabelPadding(edge.Source.Model, model.Branch);
                    MarkWorldDirty(edge.Bounds, edge.LabelPadding);
                }
                bool fieldsChanged = edge.Id != model.Id || edge.From != model.From || edge.To != model.To || edge.Branch != model.Branch;
                if (edge.Id != model.Id) edgeIndexChanged = true;
                if (!fieldsChanged && !geometryChanged && !identitiesChanged && !rebuildEdges && !routingTopologyChanged) continue;
                NodeVisual source, target, peer=null;
                _nodeIndex.TryGetValue(model.From ?? string.Empty, out source);
                _nodeIndex.TryGetValue(model.To ?? string.Empty, out target);
                FlowNode peerModel=DecisionPeer(source==null?null:source.Model,model.Branch);
                if(peerModel!=null)_nodeIndex.TryGetValue(peerModel.Id,out peer);
                bool endpointsChanged = !ReferenceEquals(edge.Source, source) || !ReferenceEquals(edge.Target, target) ||
                    (source != null && edge.SourceGeneration != source.Generation) ||
                    (target != null && edge.TargetGeneration != target.Generation);
                bool peerChanged=!ReferenceEquals(edge.Peer,peer)||(peer!=null&&edge.PeerGeneration!=peer.Generation)||
                    (source!=null&&source.Kind==NodeKind.Decision&&edge.RoutingGeneration!=branchTopologyGeneration);
                bool corridorChanged = geometryChanged && edge.UsesCorridor && changedBottom >= edge.CorridorTop && changedTop <= edge.CorridorBottom;
                if (!fieldsChanged && !endpointsChanged && !peerChanged && !corridorChanged && !identitiesChanged && !rebuildEdges) continue;
                MarkWorldDirty(edge.Bounds, edge.LabelPadding);
                edge.Id = model.Id; edge.From = model.From; edge.To = model.To; edge.Branch = model.Branch;
                edge.Source = source; edge.Target = target;
                edge.BranchGeneration = source == null ? 0 : source.BranchGeneration;
                edge.LabelPadding = source == null ? 36 : BranchLabelPadding(source.Model, model.Branch);
                edge.SourceGeneration = source == null ? 0 : source.Generation;
                edge.TargetGeneration = target == null ? 0 : target.Generation;
                edge.Peer=peer;edge.PeerGeneration=peer==null?0:peer.Generation;edge.RoutingGeneration=branchTopologyGeneration;
                List<PointF> points = source == null || target == null ? null : RouteEdge(source.Model, target.Model, model.Branch);
                if (!SamePoints(edge.Points, points)) { edge.Dispose(); edge.Points = points; }
                edge.Bounds = points == null ? RectangleF.Empty : BoundsOf(points);
                MarkWorldDirty(edge.Bounds, edge.LabelPadding);
                edge.UsesCorridor = source != null && target != null && (peer!=null||UsesOutsideCorridor(source.Model, target.Model, model.Branch));
                if (edge.UsesCorridor)
                {
                    edge.CorridorTop = Math.Min(source.Bounds.Top, target.Bounds.Top) - 32;
                    edge.CorridorBottom = Math.Max(source.Bounds.Bottom, target.Bounds.Bottom) + 32;
                    if(peer!=null){edge.CorridorTop=Math.Min(edge.CorridorTop,peer.Bounds.Top-32);edge.CorridorBottom=Math.Max(edge.CorridorBottom,peer.Bounds.Bottom+32);}
                }
                contentChanged = true;
            }
            if (edgeIndexChanged)
            {
                _edgeIndex.Clear();
                foreach (EdgeVisual edge in _edgeVisuals)
                    if (edge.Id != null && !_edgeIndex.ContainsKey(edge.Id)) _edgeIndex.Add(edge.Id, edge);
            }
            if (contentChanged)
            {
                _contentBounds = _nodeVisuals.Length == 0 ? RectangleF.Empty : _nodeVisuals[0].Bounds;
                foreach (NodeVisual node in _nodeVisuals) _contentBounds = RectangleF.Union(_contentBounds, node.Bounds);
                foreach (EdgeVisual edge in _edgeVisuals)
                {
                    if (edge.Points == null) continue;
                    RectangleF route = edge.Bounds;
                    float padding = edge.LabelPadding > 36 ? edge.LabelPadding : 16;
                    route.Inflate(padding, padding);
                    _contentBounds = RectangleF.Union(_contentBounds, route);
                }
            }
        }

        private void ClearScene()
        {
            _frameDirtyAll = true;
            _frameDirty.Clear();
            foreach (NodeVisual node in _nodeVisuals) node.Dispose();
            foreach (EdgeVisual edge in _edgeVisuals) edge.Dispose();
            _nodeVisuals = new NodeVisual[0]; _nodesByTop = new NodeVisual[0]; _edgeVisuals = new EdgeVisual[0];
            _nodeIndex.Clear(); _edgeIndex.Clear(); _contentBounds = RectangleF.Empty; sizeMotion.Clear();
            _textLayout.Clear();
            _sceneDocument = null;
            branchRoutes=new BranchRoutingContext();branchGeometryGeneration++;branchTopologyGeneration++;
        }

        private static bool SamePoints(List<PointF> a, List<PointF> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private bool UsesOutsideCorridor(FlowNode source, FlowNode target, string branch)
        {
            if (source.Id == target.Id) return false;
            RectangleF from = DisplayBounds(source), to = DisplayBounds(target);
            if (source.Kind != NodeKind.Decision) return to.Top < from.Bottom + 34;
            PointF start = OutputPoint(source, branch);
            float endX = to.Left + to.Width / 2;
            bool targetOnExitSide = branch == "true" ? endX < from.Left - 26 : endX > from.Right + 26;
            return !(targetOnExitSide && to.Top > start.Y + 28) && to.Top < from.Bottom + 24;
        }

        private static bool RouteIntersects(IList<PointF> points, RectangleF visible, float padding)
        {
            float left = visible.Left - padding, right = visible.Right + padding;
            float top = visible.Top - padding, bottom = visible.Bottom + padding;
            for (int i = 1; i < points.Count; i++)
            {
                PointF a = points[i - 1], b = points[i];
                if (Math.Max(a.X, b.X) >= left && Math.Min(a.X, b.X) <= right &&
                    Math.Max(a.Y, b.Y) >= top && Math.Min(a.Y, b.Y) <= bottom) return true;
            }
            return false;
        }

        private void DrawDiagram(Graphics graphics, RectangleF visible, bool interactive, float scale)
        {
            if (_diagram == null) return;
            EnsureScene();
            DrawScene(graphics, visible, interactive, scale);
        }

        private void DrawScene(Graphics graphics, RectangleF visible, bool interactive, float scale)
        {
            foreach (EdgeVisual edge in _edgeVisuals)
            {
                if (edge.Points == null) continue;
                RectangleF bounds = edge.Bounds;
                float padding = edge.LabelPadding > 36 ? edge.LabelPadding : 30;
                bounds.Inflate(padding, padding);
                if (!visible.IntersectsWith(bounds)) continue;
                if (!RouteIntersects(edge.Points, visible, edge.LabelPadding)) continue;
                if(!interactive||!SkipStickerEdge(edge.Model)&&!HideBladeEdge(edge.Id))DrawEdge(graphics, edge.Model, edge.Points, edge.Path, visible, interactive, scale);
            }
            
            foreach (NodeVisual node in _nodeVisuals)
            {
                if(interactive&&IsManipulationOverlay(node.Model))continue;
                RectangleF bounds = node.Bounds;
                float padding = (node.State & 16) != 0 ? NodePaintPadding(node.Model) : 28;
                bounds.Inflate(padding, padding);
                if (!visible.IntersectsWith(bounds)) continue;
                DrawNode(graphics, node, interactive, scale);
            }
        }

        private void DrawNode(Graphics graphics, NodeVisual visual, bool interactive, float scale)
        {
            FlowNode node = visual.Model;
            RectangleF bounds = visual.Bounds;
            bool selected = interactive && _selectedNodeIds.Contains(node.Id);
            bool current = interactive && node.Id == FoldOwner(_currentNodeId);
            bool issue = interactive && FoldIssue(node.Id);
            bool hover = interactive && node.Id == _hoverNodeId;
            Color typeColor = TypeColor(IoInstruction.VisualKind(node));
            var stickerPaintState=BeginStickerPaint(graphics,node,bounds,interactive);

            if (current || selected)
            {
                float haloSize = current ? 7f : 5f;
                RectangleF haloBounds = bounds;
                haloBounds.Inflate(haloSize, haloSize);
                using (Brush brush = new SolidBrush(Color.FromArgb(current ? 33 : 22, Accent)))
                {
                    if (current)
                        using (GraphicsPath halo = NodeShape(node.Kind, haloBounds)) graphics.FillPath(brush, halo);
                    else graphics.FillPath(brush, visual.SelectionHalo);
                }
            }
            for (int i = 3; i >= 1; i--)
            {
                using (Brush brush = new SolidBrush(appearance.Neutral(Dark ? Color.FromArgb(i == 1 ? 42 : 18, 0, 0, 0) : Color.FromArgb(i == 1 ? 10 : 4, 42, 61, 90))))
                    graphics.FillPath(brush, visual.Shadow(i));
            }
            {
                GraphicsPath shape = visual.Shape;
                Color fill = appearance.Neutral(Dark ? Ui.Surface : Color.White);
                using (Brush brush = new SolidBrush(fill)) graphics.FillPath(brush, shape);
                Color border = selected || current ? Accent : issue ? Color.FromArgb(222, 139, 64) :
                    hover ? (Dark ? Color.FromArgb(108, 108, 118) : Color.FromArgb(168, 168, 175)) :
                    appearance.Border(Dark,Dark ? Ui.Line : Color.FromArgb(214, 214, 220));
                using (Pen pen = new Pen(border, selected || current ? 2.2f / Math.Max(0.75f, scale) : appearance.Width(1.15f) / Math.Max(0.7f, scale)))
                    {if(node.Kind==NodeKind.Note)pen.DashStyle=DashStyle.Dash;graphics.DrawPath(pen, shape);}
            }

            if(node.Kind==NodeKind.Call){using(var pen=new Pen(appearance.Border(Dark,Ui.Line),appearance.Width(1.2f))){graphics.DrawLine(pen,bounds.Left+10,bounds.Top+2,bounds.Left+10,bounds.Bottom-2);graphics.DrawLine(pen,bounds.Right-10,bounds.Top+2,bounds.Right-10,bounds.Bottom-2);}}
            PaintHoldFill(graphics,node,bounds,interactive);
            if (node.Kind == NodeKind.Start || node.Kind == NodeKind.End)
            {
                float iconScale=appearance.Design.Scale;RectangleF icon = new RectangleF(bounds.X + 22*iconScale, bounds.Y + bounds.Height/2-10*iconScale, 20*iconScale, 20*iconScale);
                using (Brush badge = new SolidBrush(Color.FromArgb(20, typeColor))) graphics.FillEllipse(badge, icon);
                using (Brush glyph = new SolidBrush(typeColor))
                {
                    if (node.Kind == NodeKind.Start)
                        graphics.FillPolygon(glyph, new[] { new PointF(icon.X + 8*iconScale, icon.Y + 5*iconScale), new PointF(icon.X + 8*iconScale, icon.Y + 15*iconScale), new PointF(icon.X + 15*iconScale, icon.Y + 10*iconScale) });
                    else graphics.FillRectangle(glyph, icon.X + 6*iconScale, icon.Y + 6*iconScale, 8*iconScale, 8*iconScale);
                }
                DrawCenteredText(graphics, FriendlyNodeText(node), _bodyBoldFont, Ink,
                    new RectangleF(bounds.X + 47*appearance.Design.Scale, bounds.Y + 5*appearance.Design.Scale, bounds.Width - 63*appearance.Design.Scale, bounds.Height - 10*appearance.Design.Scale));
            }
            else if (node.Kind == NodeKind.Decision)
            {
                DrawTypeLabel(graphics, "CONDIZIONE", typeColor, FlowSizing.DecisionTitle(bounds,appearance.Design));
                DrawCenteredText(graphics, FriendlyNodeText(node), _decisionFont, Ink,
                    FlowSizing.DecisionBody(bounds));
            }
            else
            {
                float textScale=appearance.Design.Scale;float inset = (node.Kind == NodeKind.Input || node.Kind == NodeKind.Output ? 30 : 18)*textScale;
                RectangleF title = new RectangleF(bounds.X + inset, bounds.Y + 14*textScale, bounds.Width - inset * 2, 14*textScale);
                DrawTypeLabel(graphics, NavigationCaption(node.Id,TypeLabel(node.Kind)), typeColor, title);
                DrawCenteredText(graphics, FriendlyNodeText(node), _bodyFont, Ink,
                    new RectangleF(bounds.X + inset, bounds.Y + 31*textScale, bounds.Width - inset * 2, bounds.Height - 43*textScale));
            }

            if(interactive&&node.Breakpoint){var badge=new RectangleF(bounds.Left+3,bounds.Top+3,18,18);using(var brush=new SolidBrush(Ui.Red))graphics.FillEllipse(brush,badge);using(var pen=new Pen(Color.White,2)){graphics.DrawLine(pen,badge.X+6,badge.Y+5,badge.X+6,badge.Y+13);graphics.DrawLine(pen,badge.X+11,badge.Y+5,badge.X+11,badge.Y+13);}}
            if (issue)
            {
                PointF badgePoint = node.Kind == NodeKind.Decision ? new PointF(bounds.Right - 47, bounds.Top + 18) : new PointF(bounds.Right - 5, bounds.Top + 3);
                using (Brush white = new SolidBrush(Dark ? Ui.Surface : Color.White)) graphics.FillEllipse(white, badgePoint.X - 10, badgePoint.Y - 10, 20, 20);
                using (Brush amber = new SolidBrush(Color.FromArgb(229, 159, 64))) graphics.FillEllipse(amber, badgePoint.X - 8, badgePoint.Y - 8, 16, 16);
                DrawCenteredText(graphics, "!", _smallBoldFont, Color.White, new RectangleF(badgePoint.X - 8, badgePoint.Y - 9, 16, 17));
            }
            if (interactive && ShowsPorts(node) && holdFill<=0) DrawPorts(graphics, node, scale);
            if (current)
            {
                RectangleF marker = new RectangleF(bounds.X + bounds.Width / 2 - 4, bounds.Y - 15, 8, 8);
                using (Brush active = new SolidBrush(Accent)) graphics.FillEllipse(active, marker);
            }
            if(stickerPaintState!=null)graphics.Restore(stickerPaintState);
        }

        private void DrawTypeLabel(Graphics graphics, string text, Color color, RectangleF bounds)
        {
            DrawCenteredText(graphics, text, _labelFont, color, bounds);
        }

        private static GraphicsPath NodeShape(NodeKind kind, RectangleF bounds)
        {
            GraphicsPath path = new GraphicsPath();
            if (kind == NodeKind.Decision)
            {
                path.AddPolygon(new[] { new PointF(bounds.X + bounds.Width / 2, bounds.Top),
                    new PointF(bounds.Right, bounds.Y + bounds.Height / 2),
                    new PointF(bounds.X + bounds.Width / 2, bounds.Bottom),
                    new PointF(bounds.Left, bounds.Y + bounds.Height / 2) });
            }
            else if (kind == NodeKind.Input || kind == NodeKind.Output)
            {
                float skew = 19;
                path.AddPolygon(new[] { new PointF(bounds.Left + skew, bounds.Top), new PointF(bounds.Right, bounds.Top),
                    new PointF(bounds.Right - skew, bounds.Bottom), new PointF(bounds.Left, bounds.Bottom) });
            }
            else
            {
                path.Dispose();
                return RoundedRectangle(bounds, kind == NodeKind.Start || kind == NodeKind.End ? Math.Min(29,bounds.Height / 2) : 11);
            }
            return path;
        }

        private void DrawPorts(Graphics graphics, FlowNode node, float scale)
        {
            if (node.Kind == NodeKind.End) return;
            PortMotion motion;float reveal=_selectedNodeId==node.Id?1:hoverPorts.TryGetValue(node.Id,out motion)?motion.Value:1;
            if (node.Kind == NodeKind.Decision)
            {
                bool hasTrue = _diagram != null && _diagram.Edges.Any(e => e.From == node.Id && e.Branch == "true");
                bool hasFalse = _diagram != null && _diagram.Edges.Any(e => e.From == node.Id && e.Branch == "false");
                DrawPort(graphics, OutputPoint(node, "true"), BranchLabelColor(node, "true"), hasTrue ? null : node.GetBranchLabel("true"), -1, scale,reveal);
                DrawPort(graphics, OutputPoint(node, "false"), BranchLabelColor(node, "false"), hasFalse ? null : node.GetBranchLabel("false"), 1, scale,reveal);
            }
            else DrawPort(graphics, OutputPoint(node, "next"), Accent, null, 0, scale,reveal);
        }

        private void DrawPort(Graphics graphics, PointF point, Color color, string label, int direction, float scale, float reveal=1)
        {
            if(reveal<=.01f)return;int alpha=(int)(255*Math.Min(1,reveal));color=Color.FromArgb(alpha,color);
            float radius = 8 / Math.Max(0.85f, scale)*(.8f+.2f*reveal);
            using (Brush white = new SolidBrush(Color.FromArgb(alpha,Dark ? Ui.Surface : Color.White))) graphics.FillEllipse(white, point.X - radius, point.Y - radius, radius * 2, radius * 2);
            using (Pen pen = new Pen(color, 1.6f / Math.Max(0.75f, scale)))
            {
                graphics.DrawEllipse(pen, point.X - radius, point.Y - radius, radius * 2, radius * 2);
                graphics.DrawLine(pen, point.X - 3, point.Y, point.X + 3, point.Y);
                graphics.DrawLine(pen, point.X, point.Y - 3, point.X, point.Y + 3);
            }
            if (!string.IsNullOrEmpty(label))
            {
                float width = label.Length <= 2 ? 26 : Math.Min(180, Math.Max(26, FlowText.MeasureNatural(graphics, label, _smallBoldFont).Width + 12));
                RectangleF labelBounds = new RectangleF(direction < 0 ? point.X - width - 11 : point.X + 12, point.Y - 8, width, 16);
                using (Brush paper = new SolidBrush(Paper)) graphics.FillRectangle(paper, labelBounds);
                DrawCenteredText(graphics, label, _smallBoldFont, color, labelBounds);
            }
        }

        private void DrawEdge(Graphics graphics, FlowEdge edge, List<PointF> points, GraphicsPath path, RectangleF visible, bool interactive, float scale)
        {
            bool selected = interactive && edge.Id == _selectedEdgeId;
            bool active = interactive && edge.Id == _currentEdgeId;
            FlowNode source = FindNode(edge.From);
            bool traversed=interactive&&flowHistory.Contains(edge.Id);
            Color color = traversed ? Ui.Blue : selected && !active ? Accent : BranchColor(source, edge.Branch);
            float width = traversed ? 3.6f / Math.Max(.6f, scale) : (active ? 2.8f : selected ? 2.6f : appearance.Width(1.8f)) / Math.Max(0.7f, scale);
            {
                if (selected || active)
                    using (Pen halo = new Pen(Color.FromArgb(28, Accent), width + 7 / Math.Max(scale, 0.7f)))
                        graphics.DrawPath(halo, path);
                using (Pen pen = new Pen(color, width))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawPath(pen, path);
                }
            }
            if (points.Count >= 2)
            {
                PointF tip = points[points.Count - 1];
                if (visible.IntersectsWith(new RectangleF(tip.X - 12, tip.Y - 12, 24, 24)))
                    DrawArrow(graphics, points[points.Count - 2], tip, color, 8 / Math.Max(scale, 0.8f));
            }
            if (edge.Branch == "true" || edge.Branch == "false") DrawBranchLabel(graphics, points, source, edge.Branch, visible);
        }

        private static float BranchLabelPadding(FlowNode source, string branch)
        {
            if (source == null || (branch != "true" && branch != "false")) return 36;
            string text = source.GetBranchLabel(branch);
            return text.Length <= 2 ? 36 : Math.Min(120, Math.Max(36, 22 + text.Length * 4f));
        }

        /// <summary>Shared SVG/PNG/PDF branch pill width, in logical diagram units.</summary>
        public static float GetBranchLabelWidth(string text)
        {
            return string.IsNullOrEmpty(text) || text.Length <= 2 ? 30 : Math.Min(180, Math.Max(30, text.Length * 7 + 16));
        }

        private static float NodePaintPadding(FlowNode node)
        {
            return node.Kind == NodeKind.Decision &&
                ((node.TrueLabel != null && node.TrueLabel.Length > 2) || (node.FalseLabel != null && node.FalseLabel.Length > 2)) ? 200 : 42;
        }

        private Color BranchColor(FlowNode source, string branch)
        {
            if (source == null || (branch != "true" && branch != "false")) return BranchColor(branch);
            string custom = branch == "true" ? source.TrueColor : source.FalseColor;
            if (string.IsNullOrEmpty(custom)) return BranchColor(branch);
            string text = source.GetBranchColor(branch);
            int rgb;
            if (text != null && text.Length == 7 && text[0] == '#' && int.TryParse(text.Substring(1),
                System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb))
                return appearance.Ink(Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255),Dark);
            return BranchColor(branch);
        }

        private Color BranchLabelColor(FlowNode source, string branch)
        {
            string custom = source == null ? null : branch == "true" ? source.TrueColor : source.FalseColor;
            return string.IsNullOrEmpty(custom) ? appearance.Ink(branch == "true" ? Green : Rose,Dark) : BranchColor(source, branch);
        }

        private void DrawBranchLabel(Graphics graphics, IList<PointF> points, FlowNode source, string branch, RectangleF visible)
        {
            if (points.Count < 2) return;
            PointF from = points[0], to = points[1];
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.1f) return;
            float distance = Math.Min(length * 0.55f, 35);
            PointF center = new PointF(from.X + dx / length * distance, from.Y + dy / length * distance);
            if (Math.Abs(dx) > Math.Abs(dy)) center.Y -= 14;
            else center.X += 17;
            string text = source == null ? (branch == "true" ? "Sì" : "No") : source.GetBranchLabel(branch);
            float width = GetBranchLabelWidth(text);
            RectangleF label = new RectangleF(center.X - width / 2, center.Y - 10, width, 20);
            RectangleF labelClip = label; labelClip.Inflate(1, 1);
            if (!visible.IntersectsWith(labelClip)) return;
            using (GraphicsPath shape = RoundedRectangle(label, 7))
            using (Brush paper = new SolidBrush(appearance.Neutral(Dark ? Ui.Raised : Color.FromArgb(253, 254, 255)))) graphics.FillPath(paper, shape);
            DrawCenteredText(graphics, text, _smallBoldFont, BranchLabelColor(source, branch), label);
        }

        private static void DrawArrow(Graphics graphics, PointF previous, PointF tip, Color color, float size)
        {
            float dx = tip.X - previous.X, dy = tip.Y - previous.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.1f) return;
            dx /= length; dy /= length;
            PointF left = new PointF(tip.X - dx * size - dy * size * 0.48f, tip.Y - dy * size + dx * size * 0.48f);
            PointF right = new PointF(tip.X - dx * size + dy * size * 0.48f, tip.Y - dy * size - dx * size * 0.48f);
            using (Brush brush = new SolidBrush(color)) graphics.FillPolygon(brush, new[] { tip, left, right });
        }

        private void DrawConnectionPreview(Graphics graphics)
        {
            FlowNode source = FindNode(_connectSourceId);
            if (source == null) return;
            PointF destination = ClientToWorld(_lastMouse);
            FlowNode target = HitNode(destination);
            List<PointF> points;
            if (target != null && target.Kind != NodeKind.Start && target.Kind != NodeKind.Note) points = RouteEdge(source, target, _connectBranch);
            else
            {
                PointF start = OutputPoint(source, _connectBranch);
                points = new List<PointF> { start };
                if (source.Kind == NodeKind.Decision)
                    points.Add(new PointF(start.X + (_connectBranch == "true" ? -30 : 30), start.Y));
                else points.Add(new PointF(start.X, start.Y + 24));
                points.Add(new PointF(destination.X, points[points.Count - 1].Y));
                points.Add(destination);
                points = CleanPoints(points);
            }
            using (GraphicsPath path = RoundedRoute(points, 10))
            using (Pen pen = new Pen(Accent, 2f / Math.Max(0.75f, _zoom)))
            {
                pen.DashPattern = new[] { 4f, 3f };
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawPath(pen, path);
            }
            if (points.Count > 1) DrawArrow(graphics, points[points.Count - 2], points[points.Count - 1], Accent, 8);
            if (target != null && target.Kind != NodeKind.Start && target.Kind != NodeKind.Note)
            {
                RectangleF targetBounds = DisplayBounds(target);
                targetBounds.Inflate(4, 4);
                using (GraphicsPath outline = NodeShape(target.Kind, targetBounds))
                using (Pen pen = new Pen(Color.FromArgb(130, Accent), 2f / Math.Max(0.75f, _zoom))) graphics.DrawPath(pen, outline);
            }
        }

        private void DrawConnectionHint(Graphics graphics)
        {
            string text = ConnectionHint();
            SizeF measured = FlowText.MeasureNatural(graphics, text, _smallBoldFont);
            float width = Math.Min(ClientSize.Width - 32, measured.Width + 82);
            if (width < 100) return;
            RectangleF pill = new RectangleF((ClientSize.Width - width) / 2, 18, width, 40);
            RectangleF shadow = pill; shadow.Offset(0, 3);
            using (GraphicsPath path = RoundedRectangle(shadow, 12))
            using (Brush brush = new SolidBrush(Color.FromArgb(20, 29, 42, 64))) graphics.FillPath(brush, path);
            using (GraphicsPath path = RoundedRectangle(pill, 12))
            using (Brush brush = new SolidBrush(Color.FromArgb(35, 49, 73))) graphics.FillPath(brush, path);
            DrawCenteredText(graphics, text, _smallBoldFont, Color.White, new RectangleF(pill.Left + 10, pill.Top + 2, pill.Width - 64, 36));
            RectangleF key = new RectangleF(pill.Right - 48, pill.Top + 10, 35, 21);
            using (GraphicsPath path = RoundedRectangle(key, 5))
            using (Brush brush = new SolidBrush(Color.FromArgb(67, 82, 108))) graphics.FillPath(brush, path);
            DrawCenteredText(graphics, "Esc", _smallFont, Color.FromArgb(229, 236, 246), key);
        }

        private void DrawEmptyState(Graphics graphics)
        {
            float cx = ClientSize.Width / 2f;
            float cy = ClientSize.Height / 2f - 44;
            using (Pen connector = new Pen(Dark ? Ui.Line : Color.FromArgb(197, 210, 233), 2))
            {
                graphics.DrawLine(connector, cx, cy - 38, cx, cy - 15);
                graphics.DrawLine(connector, cx, cy + 34, cx, cy + 58);
            }
            using (GraphicsPath terminal = RoundedRectangle(new RectangleF(cx - 44, cy - 72, 88, 35), 17))
            using (Brush fill = new SolidBrush(Dark ? Ui.Raised : Color.FromArgb(233, 239, 252))) graphics.FillPath(fill, terminal);
            using (GraphicsPath box = RoundedRectangle(new RectangleF(cx - 68, cy - 15, 136, 49), 10))
            using (Brush fill = new SolidBrush(Dark ? Ui.Surface : Color.White))
            using (Pen line = new Pen(Dark ? Ui.Line : Color.FromArgb(192, 208, 238), 1.4f))
            {
                graphics.FillPath(fill, box); graphics.DrawPath(line, box);
            }
            using (Brush line = new SolidBrush(Color.FromArgb(179, 199, 234)))
            {
                graphics.FillRectangle(line, cx - 34, cy + 1, 68, 3);
                graphics.FillRectangle(line, cx - 23, cy + 12, 46, 3);
            }
            using (Brush dot = new SolidBrush(Color.FromArgb(179, 199, 234))) graphics.FillEllipse(dot, cx - 5, cy + 57, 10, 10);
            DrawCenteredText(graphics, "Disegna il tuo ragionamento", _emptyTitleFont, Ink, new RectangleF(20, cy + 88, Math.Max(10, ClientSize.Width - 40), 42));
            DrawCenteredText(graphics, "Aggiungi il primo blocco dal pannello a sinistra.", _bodyFont, Muted, new RectangleF(20, cy + 136, Math.Max(10, ClientSize.Width - 40), 28));
        }

        private List<PointF> BaseRouteEdge(FlowNode source, FlowNode target, string branch)
        {
            RectangleF from = DisplayBounds(source);
            RectangleF to = DisplayBounds(target);
            PointF start = OutputPoint(source, branch);
            PointF end = new PointF(to.X + to.Width / 2, to.Top - 2);
            List<PointF> points = new List<PointF> { start };
            bool sideExit = source.Kind == NodeKind.Decision;

            if (source.Id == target.Id)
            {
                float right = from.Right + 58;
                if (sideExit && branch == "true")
                {
                    float left = from.Left - 58;
                    points.Add(new PointF(left, start.Y)); points.Add(new PointF(left, from.Top - 32));
                }
                else
                {
                    if (!sideExit) points.Add(new PointF(start.X, from.Bottom + 32));
                    points.Add(new PointF(right, points[points.Count - 1].Y));
                    points.Add(new PointF(right, from.Top - 32));
                }
                points.Add(new PointF(end.X, from.Top - 32)); points.Add(end);
                return CleanPoints(points);
            }

            if (sideExit)
            {
                int direction = branch == "true" ? -1 : 1;
                bool targetOnExitSide = direction < 0 ? end.X < from.Left - 26 : end.X > from.Right + 26;
                if (targetOnExitSide && to.Top > start.Y + 28)
                {
                    points.Add(new PointF(end.X, start.Y));
                    points.Add(end);
                }
                else
                {
                    float outerX = direction < 0 ? Math.Min(from.Left, to.Left) - 42 : Math.Max(from.Right, to.Right) + 42;
                    if (to.Top < from.Bottom + 24) outerX = OutsideCorridorX(from, to, direction, outerX);
                    float approachY = to.Top - 32;
                    points.Add(new PointF(outerX, start.Y));
                    points.Add(new PointF(outerX, approachY));
                    points.Add(new PointF(end.X, approachY));
                    points.Add(end);
                }
            }
            else if (to.Top >= from.Bottom + 34)
            {
                float middleY = from.Bottom + (to.Top - from.Bottom) / 2;
                points.Add(new PointF(start.X, middleY));
                points.Add(new PointF(end.X, middleY));
                points.Add(end);
            }
            else
            {
                bool returnsUpward = to.Top < from.Top;
                int direction = returnsUpward ? (start.X <= end.X ? -1 : 1) : (end.X < start.X - 20 ? -1 : 1);
                float outerX = direction < 0 ? Math.Min(from.Left, to.Left) - 52 : Math.Max(from.Right, to.Right) + 52;
                outerX = OutsideCorridorX(from, to, direction, outerX);
                float departureY = from.Bottom + 30;
                float approachY = to.Top - 32;
                points.Add(new PointF(start.X, departureY));
                points.Add(new PointF(outerX, departureY));
                points.Add(new PointF(outerX, approachY));
                points.Add(new PointF(end.X, approachY));
                points.Add(end);
            }
            return CleanPoints(points);
        }

        private float OutsideCorridorX(RectangleF source, RectangleF target, int direction, float initial)
        {
            if (_diagram == null) return initial;
            float top = Math.Min(source.Top, target.Top) - 32;
            float bottom = Math.Max(source.Bottom, target.Bottom) + 32;
            float result = initial;
            // Nodes are sorted vertically when geometry changes, so short return paths
            // only visit their own rows instead of scanning the entire document.
            int low = 0, high = _nodesByTop.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (_nodesByTop[middle].Bounds.Top < top - 136) low = middle + 1;
                else high = middle;
            }
            for (int i = low; i < _nodesByTop.Length; i++)
            {
                RectangleF box = _nodesByTop[i].Bounds;
                if (box.Top > bottom) break;
                if (box.Bottom < top || box.Top > bottom) continue;
                if (direction < 0) result = Math.Min(result, box.Left - 42);
                else result = Math.Max(result, box.Right + 42);
            }
            return result;
        }

        private PointF OutputPoint(FlowNode node, string branch)
        {
            RectangleF bounds = DisplayBounds(node);
            if (node.Kind == NodeKind.Decision)
                return new PointF(branch == "false" ? bounds.Right : bounds.Left, bounds.Y + bounds.Height / 2);
            return new PointF(bounds.X + bounds.Width / 2, bounds.Bottom);
        }

        private static List<PointF> CleanPoints(IEnumerable<PointF> source)
        {
            List<PointF> points = new List<PointF>();
            foreach (PointF point in source)
            {
                if (points.Count > 0 && Distance(points[points.Count - 1], point) < 0.1f) continue;
                if (points.Count > 1)
                {
                    PointF a = points[points.Count - 2], b = points[points.Count - 1];
                    bool vertical = Math.Abs(a.X - b.X) < 0.1f && Math.Abs(b.X - point.X) < 0.1f && (b.Y - a.Y) * (point.Y - b.Y) >= 0;
                    bool horizontal = Math.Abs(a.Y - b.Y) < 0.1f && Math.Abs(b.Y - point.Y) < 0.1f && (b.X - a.X) * (point.X - b.X) >= 0;
                    if (vertical || horizontal) points.RemoveAt(points.Count - 1);
                }
                points.Add(point);
            }
            return points;
        }

        private static GraphicsPath RoundedRoute(IList<PointF> points, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (points.Count < 2) return path;
            PointF last = points[0];
            for (int i = 1; i < points.Count - 1; i++)
            {
                PointF previous = points[i - 1], corner = points[i], next = points[i + 1];
                float beforeLength = Distance(previous, corner), afterLength = Distance(corner, next);
                if (beforeLength < 0.1f || afterLength < 0.1f) continue;
                float r = Math.Min(radius, Math.Min(beforeLength / 2, afterLength / 2));
                PointF before = new PointF(corner.X + (previous.X - corner.X) / beforeLength * r, corner.Y + (previous.Y - corner.Y) / beforeLength * r);
                PointF after = new PointF(corner.X + (next.X - corner.X) / afterLength * r, corner.Y + (next.Y - corner.Y) / afterLength * r);
                path.AddLine(last, before);
                path.AddBezier(before, corner, corner, after);
                last = after;
            }
            path.AddLine(last, points[points.Count - 1]);
            return path;
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = Math.Max(0.1f, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal event Action<string> ReadOnlyNodeTapped;private string readOnlyDownNode;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if(ViewOnly&&e.Button!=MouseButtons.Middle){if(e.Button==MouseButtons.Left){_mouseDown=e.Location;var read=HitNode(ClientToWorld(e.Location));readOnlyDownNode=read==null?null:read.Id;}return;}
            settleFrom=null;arrangeCamera=false;landingId=null;
            Focus();
            if (IsPointerGestureActive) return;
            _lastMouse = e.Location;
            _mouseDown = e.Location;pointerDownAt=manipulationClock.ElapsedMilliseconds;
            if (e.Button == MouseButtons.Middle)
            {
                RememberGestureSelection();
                _panning = true;
                _panMoved = false;
                _panOriginX = _panX;
                _panOriginY = _panY;
                Capture = true;
                Cursor = Cursors.SizeAll;
                return;
            }
            if (e.Button == MouseButtons.Right) { BeginBlade(e.Location); return; }
            if (e.Button != MouseButtons.Left) return;
            if (TryBeginRewire(e)) return;
            PointF world = ClientToWorld(e.Location);
            FlowNode hit = HitNode(world);
            if (IsConnecting)
            {
                FlowNode source = FindNode(_connectSourceId);
                BeginConnectionPointer(world, HitPort(source, world) != null, hit == null);
                return;
            }
            FlowNode selected = PortNodeAt(world);
            string port = HitPort(selected, world);
            if (port != null)
            {
                BeginConnect(selected.Id, port);
                BeginConnectionPointer(world, true, false);
                return;
            }
            RememberGestureSelection();
            bool additive = (GetInteractionModifiers() & (Keys.Control | Keys.Shift)) != 0;
            if (hit != null)
            {
                if (additive)
                {
                    if ((GetInteractionModifiers() & Keys.Control) != 0 && _selectedNodeIds.Contains(hit.Id)) _toggleOffOnClickId = hit.Id;
                    SetSelection(_selectedNodeIds.Concat(FoldSelection(new[] { hit.Id })), null);
                }
                else if (!_selectedNodeIds.Contains(hit.Id))
                    SetSelection(FoldSelection(new[] { hit.Id }), null);
                else if (_selectedNodeIds.Count > 1)
                    _collapseOnClickId = hit.Id;
                _dragNode = hit;
                _dragNodeOrigin = new PointF(hit.X, hit.Y);
                _dragOrigins = SelectedNodes().ToDictionary(n => n.Id, n => new PointF(n.X, n.Y));
                _dragWorldOrigin = world;
                _dragging = _dragMoved = false;
                CaptureInsertRoutes();
                if(!additive)ArmSticker();
                Capture = true;
                Cursor = Cursors.SizeAll;
            }
            else
            {
                FlowEdge edge = HitEdge(world);
                if (!additive) SetSelection(Enumerable.Empty<string>(), edge == null ? null : edge.Id);
                _marquee = true;
                _marqueeMoved = false;
                _marqueeAdditive = additive;
                _marqueeOrigin = _marqueeCurrent = world;
                Capture = true;
                Cursor = Cursors.Cross;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if(ViewOnly&&!_panning){base.OnMouseMove(e);return;}
            base.OnMouseMove(e);
            _lastMouse = e.Location;
            if (MoveBlade(e)) return;
            if (MoveRewire(e)) return;
            if (_panning)
            {
                if ((e.Button & MouseButtons.Middle) == 0) return;
                if (Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) > 4) _panMoved = true;
                if (_panMoved)
                {
                    _panX = _panOriginX + e.X - _mouseDown.X;
                    _panY = _panOriginY + e.Y - _mouseDown.Y;
                    RequestFrameRepaint();
                    RaiseViewChanged();
                }
                return;
            }
            if (_connectionPointerActive)
            {
                bool moved = Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) > 4;
                if (!_connectionPointerFromPort && _connectionPointerFromBlank && moved)
                {
                    _connectionPointerActive = false;
                    CancelConnection(false);
                    _marquee = true;
                    _marqueeMoved = true;
                    _marqueeOrigin = _connectionPointerOrigin;
                    _marqueeCurrent = ClientToWorld(e.Location);
                    if (!_marqueeAdditive) SetSelection(Enumerable.Empty<string>(), null);
                }
                else
                {
                    RequestFrameRepaint();
                    return;
                }
            }
            if (_dragNode != null && (e.Button & MouseButtons.Left) != 0)
            {
                if (DragDisplaced(e.Location)) _dragMoved=true;
                if (!_dragging && _dragMoved)
                {
                    _dragging = true;
                    if(liftStart<0)ClearSticker();
                }
                if (_dragging && _dragMoved)
                {
                    // Follow the pointer continuously. Grid placement is committed once on release.
                    UpdateDragPosition(e.Location, false);
                    UpdateInsertPreview();
                    RequestFrameRepaint();
                }
                return;
            }
            if (_marquee)
            {
                if (!_marqueeMoved && Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) > 4) _marqueeMoved = true;
                if (_marqueeMoved)
                {
                    _marqueeCurrent = ClientToWorld(e.Location);
                    RectangleF area = RectangleF.FromLTRB(Math.Min(_marqueeOrigin.X, _marqueeCurrent.X), Math.Min(_marqueeOrigin.Y, _marqueeCurrent.Y),
                        Math.Max(_marqueeOrigin.X, _marqueeCurrent.X), Math.Max(_marqueeOrigin.Y, _marqueeCurrent.Y));
                    IEnumerable<string> hits = _diagram == null ? Enumerable.Empty<string>() : _diagram.Nodes.Where(n => area.IntersectsWith(DisplayBounds(n))).Select(n => n.Id);
                    SetSelection(_marqueeAdditive ? _selectionBeforeGesture.Concat(hits) : hits, null);
                    RequestFrameRepaint();
                }
                return;
            }
            PointF location = ClientToWorld(e.Location);
            FlowNode node = HitNode(location);
            if(node==null)node=PortNodeAt(location);
            string hover = node == null ? null : node.Id;
            if (_hoverNodeId != hover)
            {
                SetHoverNode(hover);
                _toolTip.SetToolTip(this, node == null || IsConnecting ? string.Empty : TypeLabel(node.Kind) + "\n" + FriendlyNodeText(node) + "\nDoppio clic per modificare");
            }
            if (IsConnecting)
            {
                Cursor = node != null && node.Kind == NodeKind.Start ? Cursors.No : Cursors.Cross;
                RequestFrameRepaint();
            }
            else if (PortNodeAt(location) != null) Cursor = Cursors.Cross;
            else Cursor = node != null || HitEdge(location) != null ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if(ViewOnly&&e.Button!=MouseButtons.Middle){string id=readOnlyDownNode;readOnlyDownNode=null;if(e.Button==MouseButtons.Left&&id!=null&&!DragDisplaced(e.Location)){var handler=ReadOnlyNodeTapped;if(handler!=null)handler(id);}return;}
            _lastMouse = e.Location;
            if (EndBlade(e)) return;
            if (EndRewire(e) || rewireEdge != null) return;
            if (_panning && e.Button != MouseButtons.Middle) return;
            if ((_dragNode != null || _marquee || _connectionPointerActive) && e.Button != MouseButtons.Left) return;
            if (_connectionPointerActive && e.Button == MouseButtons.Left)
            {
                bool stationaryPort = _connectionPointerFromPort && Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) <= 4;
                ClearPointerState();
                Capture = false;
                if (IsConnecting && stationaryPort)
                {
                    FlowNode source = FindNode(_connectSourceId);
                    if(source!=null)ShowBlockPickerAt(e.Location, NextBlockCenter(source, _connectBranch), true);
                }
                else if (IsConnecting)
                {
                    FlowNode target = HitNode(ClientToWorld(e.Location));
                    if (target != null) CompleteConnection(target);
                    else ShowBlockPicker(e.Location);
                }
                Cursor = IsConnecting ? Cursors.Cross : Cursors.Default;
                RequestFrameRepaint();
                return;
            }
            string editId=pendingDoubleEdit;pendingDoubleEdit=null;bool editTap=editId!=null&&_dragNode!=null&&_dragNode.Id==editId&&!_dragMoved&&!DragDisplaced(e.Location)&&manipulationClock.ElapsedMilliseconds-pointerDownAt<SystemInformation.DoubleClickTime;
            bool showMenu = e.Button == MouseButtons.Right && !_panning && !_connectionPointerActive;
            if(CommitTrashDrop()){}
            else if (_dragNode != null && (_dragging||DragDisplaced(e.Location))) { _dragMoved|=DragDisplaced(e.Location);_dragging=true;if(liftStart<0)ClearSticker();CommitDrag(); }
            else
            {
                string collapse = _collapseOnClickId;
                string toggleOff = _toggleOffOnClickId;
                ClearPointerState();
                if (toggleOff != null) SetSelection(_selectedNodeIds.Except(FoldSelection(new[] { toggleOff })), null);
                else if (collapse != null) SetSelection(FoldSelection(new[] { collapse }), null);
            }
            Capture = false;
            Cursor = IsConnecting ? Cursors.Cross : Cursors.Default;
            RequestFrameRepaint();
            if(editTap){SelectNode(editId);var edit=EditRequested;if(edit!=null)edit(this,EventArgs.Empty);}
            if (showMenu) ShowContextMenu(e.Location);
        }

        private string pendingDoubleEdit;private long pointerDownAt;
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);if(ViewOnly)return;
            if (e.Button != MouseButtons.Left || IsConnecting) return;
            FlowNode node = HitNode(ClientToWorld(e.Location));
            if (node == null) return;
            if(IsFoldCard(node.Id)){ClearPointerState();Capture=false;var card=foldProjection.Cards[node.Id];FoldCycle(card.Check,false);return;}
            pendingDoubleEdit = node.Id;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (IsPointerGestureActive || IsBlockPickerOpen) return;
            SetZoomAt(_zoom * (float)Math.Pow(1.12, e.Delta / 120.0), e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHoverNode(null);
            _toolTip.SetToolTip(this, string.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.Space || key == Keys.Enter || key == Keys.Delete || key == Keys.PageUp || key == Keys.PageDown || key == Keys.Home || key == Keys.End) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(!Enabled){e.Handled=true;e.SuppressKeyPress=true;return;}
            if(HandleCanvasNavigation(e))return;
            if(ViewOnly){e.Handled=true;e.SuppressKeyPress=true;return;}
            if(HandleClipboardShortcut(e.KeyData)){e.Handled=true;e.SuppressKeyPress=true;return;}
            if (IsPointerGestureActive && e.KeyCode != Keys.Escape)
            {
                e.Handled = true; e.SuppressKeyPress = true; return;
            }
            if (e.KeyCode == Keys.Apps || (e.Shift && e.KeyCode == Keys.F10))
            {
                Point at = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
                FlowNode selected = FindNode(SelectedNodeId);
                if (selected != null) { RectangleF b = DisplayBounds(selected); at = Point.Round(WorldToClient(new PointF(b.Left + b.Width / 2, b.Top + b.Height / 2))); }
                Focus(); ShowContextMenu(at); e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                bool cancelled = CancelPointerGesture(true, true);
                CancelConnection(true);
                if (cancelled) Report("Gesto annullato.");
                e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                DeleteSelection(); e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                DuplicateSelected(); e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAllNodes(); e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter && AccessActivationNode() != null)
            {
                ActivateAccessNode(AccessActivationNode());
                e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                List<FlowNode> nodes = SelectedNodes();
                if (nodes.Count != 0)
                {
                    float amount = e.Shift ? GridSnap.Step*2 : SnapToGrid ? GridSnap.Step : 1;
                    PointF requested = new PointF(e.KeyCode == Keys.Left ? -amount : e.KeyCode == Keys.Right ? amount : 0,
                        e.KeyCode == Keys.Up ? -amount : e.KeyCode == Keys.Down ? amount : 0);
                    Dictionary<string, PointF> origins = nodes.ToDictionary(n => n.Id, n => new PointF(n.X, n.Y));
                    PointF offset;
                    if (TryConstrainOffset(origins, requested, out offset) && (offset.X != 0 || offset.Y != 0))
                    {
                        RaiseDiagramChanging();
                        ApplyOffset(origins, offset);
                        RaiseDiagramChanged();
                    }
                    e.Handled = true;
                }
            }
            if (!e.Handled) base.OnKeyDown(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            CancelPointerGesture(true, true);
            if (!_panning) Cursor = IsConnecting ? Cursors.Cross : Cursors.Default;
            base.OnLostFocus(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) CancelPointerGesture(true, true);
            base.OnMouseCaptureChanged(e);
        }

        /// <summary>Overridable so host input adapters and gesture tests can supply modifier state.</summary>
        protected virtual Keys GetInteractionModifiers() { return ModifierKeys; }

        private void RememberGestureSelection()
        {
            _selectionBeforeGesture = new HashSet<string>(_selectedNodeIds);
            _edgeBeforeGesture = _selectedEdgeId;
        }

        private void BeginConnectionPointer(PointF origin, bool fromPort, bool fromBlank)
        {
            RememberGestureSelection();
            _connectionPointerActive = true;
            _connectionPointerFromPort = fromPort;
            _connectionPointerFromBlank = fromBlank;
            _connectionPointerOrigin = origin;
            _marqueeAdditive = (GetInteractionModifiers() & (Keys.Control | Keys.Shift)) != 0;
            Capture = true;
            Cursor = Cursors.Cross;
        }

        private void ClearPointerState()
        {
            ClearBladeGesture();
            pendingDoubleEdit=null;
            ClearInsertPreview();
            ClearSticker();paletteNode=null;
            rewireEdge = null; rewireMoved = false;
            _connectionPointerActive = false;
            _connectionPointerFromPort = false;
            _connectionPointerFromBlank = false;
            _dragNode = null;
            _dragOrigins = null;
            _dragging = _dragMoved = false;
            _marquee = false;
            _marqueeMoved = false;
            _panning = false;
            _panMoved = false;
            _collapseOnClickId = null;
            _toggleOffOnClickId = null;
            _selectionBeforeGesture = null;
            _edgeBeforeGesture = null;
        }

        private bool CancelPointerGesture(bool restoreSelection, bool restoreView)
        {
            bool active = IsPointerGestureActive;
            if (!active) return false;
            if (_dragOrigins != null) ApplyOffset(_dragOrigins, PointF.Empty);
            HashSet<string> previous = _selectionBeforeGesture;
            string previousEdge = _edgeBeforeGesture;
            bool viewChanged = restoreView && _panning && _panMoved;
            if (viewChanged) { _panX = _panOriginX; _panY = _panOriginY; }
            ClearPointerState();
            if (Capture) Capture = false;
            if (restoreSelection && previous != null) SetSelection(previous, previousEdge);
            if (viewChanged) RaiseViewChanged();
            Cursor = IsConnecting ? Cursors.Cross : Cursors.Default;
            RequestFrameRepaint();
            return true;
        }

        private bool DragDisplaced(Point point){int radius=Math.Max(6,(int)Math.Ceiling(DeviceDpi*6/96f));long dx=point.X-_mouseDown.X,dy=point.Y-_mouseDown.Y;return dx*dx+dy*dy>radius*radius;}

        private void UpdateDragPosition(Point position, bool snap)
        {
            PointF world = ClientToWorld(position);
            PointF requested = new PointF(world.X - _dragWorldOrigin.X, world.Y - _dragWorldOrigin.Y);
            if (snap && SnapToGrid && (GetInteractionModifiers() & Keys.Alt) == 0)
            {
                requested.X = GridSnap.Round(_dragNodeOrigin.X + requested.X) - _dragNodeOrigin.X;
                requested.Y = GridSnap.Round(_dragNodeOrigin.Y + requested.Y) - _dragNodeOrigin.Y;
            }
            PointF offset;
            if (TryConstrainOffset(_dragOrigins, requested, out offset)) ApplyOffset(_dragOrigins, offset);
        }

        private void CommitDrag()
        {
            if(GroupDragging){CommitGroupDrag();return;}
            // Use the actual release location, even when Windows coalesced the last move.
            // One common snapped offset preserves the relative positions of a selected group.
            if(_dragMoved)UpdateDragPosition(_lastMouse,false);
            var animationBefore=ModelPositions();var presentationBefore=PresentationPositions();
            if(_dragMoved)UpdateDragPosition(_lastMouse,true);
            UpdateInsertPreview();
            string insertId=insertReady&&insertHover!=null?insertHover.Id:null;
            FlowNode inserting=_dragNode;Diagram insertingDocument=_diagram;bool detach=stickerDetached,ambiguous=stickerAmbiguous,detachNone=stickerDetachNone,peeled=liftStart>=0;
            var chosenEdge=insertId==null?null:(insertionDocument??_diagram).Edges.FirstOrDefault(x=>x.Id==insertId);string source=chosenEdge==null?null:chosenEdge.From,target=chosenEdge==null?null:chosenEdge.To;bool forward=chosenEdge!=null&&FindNode(target).Y>FindNode(source).Y;
            Dictionary<string, PointF> origins = _dragOrigins;
            Dictionary<string, PointF> final = new Dictionary<string, PointF>();
            foreach (string id in origins.Keys)
            {
                FlowNode node = FindNode(id);
                if (node != null) final[id] = new PointF(node.X, node.Y);
            }
            bool changed = final.Any(pair => pair.Value != origins[pair.Key]);
            // The undo observer must see the exact pre-drag document, including for a group.
            ApplyOffset(origins, PointF.Empty);
            ClearPointerState();
            Capture=false;
            string detachBranch=ambiguous?"none":null;
            if(ambiguous&&detachBranch==null){StartLanding(inserting.Id);RequestFrameRepaint();Report("Stacco annullato: posizione e collegamenti conservati.");return;}
            if(!ReferenceEquals(insertingDocument,_diagram))return;
            var staged=DiagramIO.Clone(_diagram);
            bool requestedDetach=detach||detachBranch!=null;
            bool plannedDetach=detachNone||detachBranch=="none"?FlowPlacement.Disconnect(staged,inserting.Id):detach?FlowPlacement.Detach(staged,inserting.Id):detachBranch!=null&&FlowPlacement.DetachVia(staged,inserting.Id,detachBranch);
            if(requestedDetach&&!plannedDetach){StartLanding(inserting.Id);Report("Il percorso non può essere richiuso in questo modo. Nessuna modifica applicata.");return;}
            string continuation=null;pendingDropPositions=animationBefore;pendingDropId=inserting.Id;pendingDropDetached=requestedDetach;RequestFrameRepaint();
            try{continuation=insertId==null?null:InsertionBranch(inserting,insertId,staged);}finally{pendingDropPositions=null;pendingDropId=null;pendingDropDetached=false;}
            bool skip=continuation=="skip";if(skip){foreach(string id in origins.Keys)final[id]=animationBefore[id];source=target=null;forward=false;}
            if(insertId!=null&&continuation==null){StartLanding(inserting.Id);RequestFrameRepaint();Report("Inserimento annullato: posizione e collegamenti conservati.");return;}
            bool splice=continuation!=null&&!skip&&ReferenceEquals(insertingDocument,_diagram);
            if(splice&&!FlowInsertion.Apply(staged,inserting.Id,insertId,continuation,ReservedEdges)){StartLanding(inserting.Id);Report("Inserimento non disponibile. Nessuna modifica applicata.");return;}
            if (changed||splice||detach||detachBranch!=null)
            {
                RaiseDiagramChanging();
                if(!ReferenceEquals(insertingDocument,_diagram))return;
                ApplyOffset(final, PointF.Empty);
                bool detached=detachNone||detachBranch=="none"?FlowPlacement.Disconnect(_diagram,inserting.Id):detach?FlowPlacement.Detach(_diagram,inserting.Id):detachBranch!=null&&FlowPlacement.DetachVia(_diagram,inserting.Id,detachBranch);
                bool inserted=splice&&FlowInsertion.Apply(_diagram,inserting.Id,insertId,continuation,ReservedEdges);
                if(_selectedNodeIds.Count==1)FlowPlacement.Reflow(_diagram,inserting.Id,source,target,inserted&&forward);
                RaiseDiagramChanged();StartLanding(inserting.Id,peeled);AnimatePlacement(presentationBefore);
                if(inserted)Report("Blocco inserito nel percorso. Ctrl+Z ripristina posizione e collegamenti.");
                else if(detached)Report(ambiguous||detachNone?"Blocco staccato. I collegamenti precedenti sono liberi; tutte le caselle restano sul foglio. Ctrl+Z ripristina tutto.":"Blocco staccato: il percorso precedente è ricucito. Ctrl+Z ripristina tutto.");
                else if(insertId!=null)Report("Blocco spostato. Inserimento annullato: la freccia precedente resta intatta.");
                else Report("Posizione aggiornata. Ctrl+Z ripristina anche gli adattamenti locali.");
            }
            else RequestFrameRepaint();
        }

        private void ApplyOffset(IDictionary<string, PointF> origins, PointF offset)
        {
            foreach (KeyValuePair<string, PointF> pair in origins)
            {
                FlowNode node = FindNode(pair.Key);
                if (node == null) continue;
                node.X = pair.Value.X + offset.X;
                node.Y = pair.Value.Y + offset.Y;
            }
        }

        private bool TryConstrainOffset(IDictionary<string, PointF> origins, PointF requested, out PointF offset)
        {
            offset = PointF.Empty;
            if (origins == null || origins.Count == 0) return false;
            float minX = origins.Values.Min(p => p.X), maxX = origins.Values.Max(p => p.X);
            float minY = origins.Values.Min(p => p.Y), maxY = origins.Values.Max(p => p.Y);
            if (maxX - minX > 199800 || maxY - minY > 199800)
            {
                Report("La selezione è troppo estesa: sposta separatamente i blocchi più lontani.");
                return false;
            }
            offset = new PointF(Clamp(requested.X, -99900 - minX, 99900 - maxX), Clamp(requested.Y, -99900 - minY, 99900 - maxY));
            return true;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (_pulseTimer != null) UpdatePulseTimer();
        }

        private void CompleteConnection(FlowNode target)
        {
            FlowNode source = FindNode(_connectSourceId);
            if (source == null || _diagram == null) { CancelConnection(false); return; }
            if (target.Kind == NodeKind.Note) { Report("Una nota non riceve collegamenti."); return; }
            if (target.Kind == NodeKind.Start)
            {
                Report("Inizio apre il diagramma: scegli un altro blocco di arrivo.");
                return;
            }
            string branch = _connectBranch;
            FlowEdge existing = _diagram.Edges.FirstOrDefault(e => e.From == source.Id && e.Branch == branch);
            if (existing != null && existing.To == target.Id)
            {
                CancelConnection(false);
                SelectEdge(existing.Id);
                Report("I due blocchi sono già collegati.");
                return;
            }
            if (existing == null && ProjectEdgeCount >= DiagramIO.MaxEdges)
            {
                Report("Hai raggiunto il limite di " + DiagramIO.MaxEdges + " collegamenti. Elimina un collegamento prima di aggiungerne un altro.");
                return;
            }
            if(existing!=null)
            {
                Diagram expected=_diagram;
                string previousTarget=existing.To;
                FlowNode previous=FindNode(existing.To);
                string text="Da: "+FriendlyNodeText(source)+"\nUscita: "+(source.Kind==NodeKind.Decision?source.GetBranchLabel(branch):"Continua")+
                    "\n\nArrivo attuale: "+(previous==null?"Blocco non disponibile":FriendlyNodeText(previous))+"\nNuovo arrivo: "+FriendlyNodeText(target)+
                    "\n\nI blocchi restano sul foglio. Puoi annullare la modifica con Ctrl+Z.";
                DialogResult answer=FlowMessageDialog.Choose(FindForm(),text,"Cambiare arrivo?","Cambia arrivo","Mantieni",false);
                if(answer!=DialogResult.Yes||!ReferenceEquals(expected,_diagram)||!_diagram.Edges.Contains(existing)||existing.To!=previousTarget)
                {CancelConnection(false);Report("Collegamento precedente mantenuto.");return;}
            }
            RaiseDiagramChanging();
            _diagram.Edges.RemoveAll(e => e.From == source.Id && e.Branch == branch);
            FlowEdge edge = new FlowEdge { Id = Guid.NewGuid().ToString("N"), From = source.Id, To = target.Id, Branch = branch };
            _diagram.Edges.Add(edge);
            CancelConnection(false);
            SelectEdge(edge.Id);
            RaiseDiagramChanged();
            Report(existing != null ? "Destinazione del collegamento aggiornata." : "Blocchi collegati.");
        }

        private void CancelConnection(bool report)
        {
            bool wasConnecting = IsConnecting;
            ClearBlockPickerSession();
            _connectSourceId = null;
            _connectBranch = null;
            Cursor = Cursors.Default;
            if (wasConnecting) RequestFrameRepaint();
            if (wasConnecting) RaiseConnectionStateChanged();
            if (report && wasConnecting) Report("Collegamento annullato.");
        }

        private void ShowBlockPicker(Point location)
        { ShowBlockPickerAt(location, ClientToWorld(location)); }

        private PointF NextBlockCenter(FlowNode source, string branch)
        {
            RectangleF from=DisplayBounds(source);
            FlowEdge edge=_diagram.Edges.FirstOrDefault(e=>e.From==source.Id&&e.Branch==branch);
            FlowNode next=edge==null?null:FindNode(edge.To);
            if(next!=null)
            {
                RectangleF to=DisplayBounds(next);
                if(to.Top>from.Bottom)
                    return new PointF(to.Left+to.Width/2,from.Bottom+104);
            }
            return new PointF(from.Left+from.Width/2+(source.Kind==NodeKind.Decision?(branch=="true"?-210:210):0),from.Bottom+110);
        }

        private void ShowBlockPickerAt(Point location, PointF center, bool autoPlace=false)
        {
            if (!IsConnecting || _diagram == null || IsDisposed || Disposing || !IsHandleCreated) return;
            ClearBlockPickerSession();
            _pickerDocument = _diagram;
            _pickerSourceId = _connectSourceId;
            _pickerBranch = _connectBranch;
            _pickerCenter = center;
            _pickerAutoPlace = autoPlace;
            _pickerQueued = true;
            int generation = _pickerGeneration;
            RaiseConnectionStateChanged();
            BeginInvoke(new Action(delegate
            {
                if (IsDisposed || Disposing || !IsHandleCreated || generation != _pickerGeneration ||
                    !ReferenceEquals(_pickerDocument, _diagram) || _pickerSourceId != _connectSourceId) return;
                _pickerQueued = false;
                EnsureBlockPicker();
                if (_menu != null && _menu.Visible) _menu.Close();
                _blockPicker.ShowAt(this, location, _pickerBranch);
                RaiseConnectionStateChanged();
            }));
        }

        private void EnsureBlockPicker()
        {
            if (_blockPicker != null) return;
            _blockPicker = new BlockPicker();
            _blockPicker.KindSelected += delegate(object sender, BlockKindEventArgs e) { CreateConnectedBlock(e.Kind); };
            _blockPicker.CancelRequested += delegate { CancelBlockPicker(); };
            _blockPicker.DisconnectRequested+=delegate { DisconnectPickerConnection(); };
            _blockPicker.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs e)
            {
                // ItemClicked can be raised before the item's Click handler. Preserve its session
                // until the selected action has run; every selectable item consumes the session.
                if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked && _pickerDocument != null)
                    CancelConnection(false);
            };
        }

        private void ClearBlockPickerSession()
        {
            bool hadSession = _pickerDocument != null || _pickerQueued;
            _pickerGeneration++;
            _pickerDocument = null;
            _pickerSourceId = null;
            _pickerBranch = null;
            _pickerQueued = false;
            if (_blockPicker != null && _blockPicker.Visible) _blockPicker.Close();
            if (hadSession) RaiseConnectionStateChanged();
        }

        internal bool HasConnectionToDisconnect {get{return !ViewOnly&&ReferenceEquals(_pickerDocument,_diagram)&&_diagram!=null&&_diagram.Edges.Any(e=>e.From==_pickerSourceId&&e.Branch==_pickerBranch);}}
        private void DisconnectPickerConnection(){
            if(!HasConnectionToDisconnect||_pickerSourceId!=_connectSourceId||_pickerBranch!=_connectBranch)return;
            var document=_diagram;var edge=document.Edges.FirstOrDefault(e=>e.From==_pickerSourceId&&e.Branch==_pickerBranch);
            CancelConnection(false);RaiseDiagramChanging();if(!ReferenceEquals(document,_diagram)||!document.Edges.Contains(edge))return;
            document.Edges.Remove(edge);SelectNode(edge.From);RaiseDiagramChanged();RequestFrameRepaint();Report("Collegamento scollegato. Tutte le caselle sono conservate. Ctrl+Z ripristina.");
        }

        private void CreateConnectedBlock(NodeKind kind)
        {
            Diagram document = _pickerDocument;
            string sourceId = _pickerSourceId, branch = _pickerBranch;
            PointF center = _pickerCenter;
            bool autoPlace = _pickerAutoPlace;
            if (document == null || !ReferenceEquals(document, _diagram) || sourceId != _connectSourceId || branch != _connectBranch) return;
            FlowNode source = FindNode(sourceId);
            if (source == null || source.Kind == NodeKind.End || source.Kind == NodeKind.Note || kind == NodeKind.Note || kind == NodeKind.Start || !Enum.IsDefined(typeof(NodeKind), kind))
            {
                CancelConnection(false);
                return;
            }
            if (!CanAddNode()) { ClearBlockPickerSession(); return; }
            FlowEdge previous=document.Edges.FirstOrDefault(e=>e.From==sourceId&&e.Branch==branch);
            int replaced = document.Edges.Count(e => e.From == sourceId && e.Branch == branch);
            int required=1+(previous!=null&&kind!=NodeKind.End?1:0);
            if (kind!=NodeKind.Decision&&ProjectEdgeCount - replaced + required > DiagramIO.MaxEdges)
            {
                ClearBlockPickerSession();
                Report("Hai raggiunto il limite di " + DiagramIO.MaxEdges + " collegamenti. Elimina un collegamento prima di aggiungerne un altro.");
                return;
            }
            string continuation="next";
            if(_blockPicker!=null&&_blockPicker.Visible)_blockPicker.Close(ToolStripDropDownCloseReason.ItemClicked);
            if(previous!=null && kind==NodeKind.Decision)
            {
                FlowNode next=FindNode(previous.To);
                continuation=BranchContinuationDialog.Choose(FindForm(),new FlowNode{Kind=NodeKind.Decision},"Stai inserendo una decisione prima di: "+(next==null?"blocco successivo":FriendlyNodeText(next))+"\nScegli quale ramo prosegue, oppure lascia entrambe le uscite libere.","Come continua il flusso?");
                if(continuation==null){CancelConnection(false);return;}
            }
            if(previous!=null && kind==NodeKind.End)
            {
                FlowNode next=FindNode(previous.To);
                if(FlowMessageDialog.Choose(FindForm(),"Fine chiude questa uscita e interrompe il collegamento verso: "+(next==null?"blocco successivo":FriendlyNodeText(next))+
                    "\n\nI blocchi successivi restano sul foglio. Ctrl+Z ripristina il percorso.","Terminare qui il percorso?","Aggiungi Fine","Mantieni",false)!=DialogResult.Yes)
                {CancelConnection(false);return;}
            }
            required=1+(previous!=null&&kind!=NodeKind.End&&continuation!="none"?1:0);
            if(ProjectEdgeCount-replaced+required>DiagramIO.MaxEdges){CancelConnection(false);Report("Limite di collegamenti raggiunto. Nessuna casella aggiunta.");return;}
            if(!ReferenceEquals(document,_diagram)||sourceId!=_connectSourceId||branch!=_connectBranch||previous!=null&&!document.Edges.Contains(previous))
            {CancelConnection(false);return;}
            FlowNode node = CreateNodeCandidate(kind, center);
            var shifted=new Dictionary<FlowNode,float>();
            FlowNode nextNode=previous==null?null:FindNode(previous.To);
            if(autoPlace&&nextNode!=null&&DisplayBounds(nextNode).Top>DisplayBounds(source).Bottom)
            {
                float offset=DisplayBounds(node).Bottom+54-nextNode.Y;
                if(offset>0)
                {
                    var queue=new Queue<string>();var visited=new HashSet<string>();queue.Enqueue(nextNode.Id);
                    while(queue.Count>0)
                    {
                        string id=queue.Dequeue();if(id==sourceId||!visited.Add(id))continue;
                        FlowNode moving=FindNode(id);if(moving==null||moving.Y<nextNode.Y)continue;
                        shifted[moving]=moving.Y+offset;
                        foreach(FlowEdge downstream in document.Edges.Where(e=>e.From==id))queue.Enqueue(downstream.To);
                    }
                    if(shifted.Values.Any(y=>y>99900))shifted.Clear();
                }
            }
            // A tap chooses a nearby position; it must not hide an existing block.
            // Search alternately on either side without moving the user's diagram.
            RectangleF addedBounds=DisplayBounds(node);
            float proposedX=node.X;
            for(int i=1;i<=DiagramIO.MaxNodes*2;i++)
            {
                RectangleF occupied=DisplayBounds(node);occupied.Inflate(12,12);
                if(!document.Nodes.Any(n=>{
                    RectangleF existing=DisplayBounds(n);float y;if(shifted.TryGetValue(n,out y))existing.Y=y;
                    return occupied.IntersectsWith(existing);
                }))break;
                float offset=((i+1)/2)*(addedBounds.Width+30)*(i%2==1?1:-1);
                node.X=ClampCoordinate(proposedX+offset);
            }
            FlowEdge edge = new FlowEdge { Id = Guid.NewGuid().ToString("N"), From = sourceId, To = node.Id, Branch = branch };
            RaiseDiagramChanging();
            if (!ReferenceEquals(document, _diagram)) { CancelConnection(false); return; }
            foreach(var position in shifted)position.Key.Y=position.Value;
            document.Nodes.Add(node);
            document.Edges.RemoveAll(e => e.From == sourceId && e.Branch == branch);
            document.Edges.Add(edge);
            if(previous!=null&&kind!=NodeKind.End&&continuation!="none")
                document.Edges.Add(new FlowEdge {Id=Guid.NewGuid().ToString("N"),From=node.Id,To=previous.To,Branch=continuation});
            CancelConnection(false);
            SelectNode(node.Id);
            RaiseDiagramChanged();
            Focus();
            Report(previous!=null&&kind!=NodeKind.End?"Passaggio inserito. Il resto del percorso è ancora collegato.":"Nuovo blocco creato e collegato.");
        }

        private void ShowContextMenu(Point location)
        {
            if(ViewOnly)return;
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            _menuLocation = location;
            EnsureContextMenu();
            if (_menu.Visible) _menu.Close();
            if (_menuQueued) return;
            _menuQueued = true;
            // Let ToolStrip complete its native click/close callback before opening another popup.
            BeginInvoke(new Action(delegate
            {
                _menuQueued = false;
                if (IsDisposed || Disposing || !IsHandleCreated || IsPointerGestureActive) return;
                ConfigureContextMenu(_menuLocation);
                _menu.Show(this, _menuLocation);
            }));
        }

        private void EnsureContextMenu()
        {
            if (_menu != null) return;
            _menu = new FlowContextMenu { ShowImageMargin = false, Font = _smallFont, Padding = new Padding(5) };
            BlockPicker.ApplyMenuTheme(_menu);
            _menu.Items.Add(new FlowMenuHeading("Sul foglio", "Rotella premuta per spostare"));
            _menuCancel = new ToolStripMenuItem("Annulla collegamento", null, delegate { CancelConnection(); });
            _menuConnectionSeparator = new ToolStripSeparator();
            _menuEdit = new ToolStripMenuItem("Modifica blocco…", null, delegate
            {
                if (SelectedNodeId != null && EditRequested != null) EditRequested(this, EventArgs.Empty);
            });
            _menuBreakpoint=new ToolStripMenuItem("Punto di interruzione…",null,delegate{if(BreakpointRequested!=null)BreakpointRequested(this,EventArgs.Empty);});
            _menuRunTo=new ToolStripMenuItem("Esegui fino a questo blocco",null,delegate{if(RunToRequested!=null)RunToRequested(this,EventArgs.Empty);});
            foldCycleMenu=new ToolStripMenuItem("Riduci ciclo sul foglio",null,delegate{ToggleSelectedCycle();});
            _menuNext = new ToolStripMenuItem("Collega al blocco successivo", null, delegate { BeginConnect(SelectedNodeId, "next"); });
            _menuTrue = new ToolStripMenuItem("Collega il ramo Sì", null, delegate { BeginConnect(SelectedNodeId, "true"); });
            _menuFalse = new ToolStripMenuItem("Collega il ramo No", null, delegate { BeginConnect(SelectedNodeId, "false"); });
            _menuEditSeparator = new ToolStripSeparator();
            _menuCopy = new ToolStripMenuItem("Copia     Ctrl+C", null, delegate { CopySelection(); });
            _menuCut = new ToolStripMenuItem("Taglia     Ctrl+X", null, delegate { CopySelection(true); });
            _menuPaste = new ToolStripMenuItem("Incolla     Ctrl+V", null, delegate { PasteSelection(ClientToWorld(_menuLocation)); });
            _menuDuplicate = new ToolStripMenuItem("Duplica blocco     Ctrl+D", null, delegate { DuplicateSelected(); });
            _menuDelete = new ToolStripMenuItem("Elimina blocco     Canc", null, delegate { DeleteSelection(); });
            _menuSelectionSeparator = new ToolStripSeparator();
            _menuAdd = new ToolStripMenuItem("Aggiungi blocco");
            foreach (NodeKind kind in IoInstruction.Separate?new[] { NodeKind.Start, NodeKind.Input, NodeKind.Output, NodeKind.Process, NodeKind.Declare, NodeKind.Decision, NodeKind.Call, NodeKind.End }:new[] { NodeKind.Start, NodeKind.Input, NodeKind.Process, NodeKind.Declare, NodeKind.Decision, NodeKind.Call, NodeKind.End })
            {
                NodeKind captured = kind;
                _menuAdd.DropDownItems.Add(kind==NodeKind.Input&&!IoInstruction.Separate?"Input / Output":TypeLabel(kind), null, delegate {AddNodeAt(captured,ClientToWorld(_menuLocation));});
            }
            var files=new ToolStripMenuItem("File · leggi e scrivi"){Name="FileBlockGroup"};
            foreach(NodeKind kind in FileBlocks.Kinds){NodeKind captured=kind;files.DropDownItems.Add(Ui.KindName(kind),null,delegate{AddNodeAt(captured,ClientToWorld(_menuLocation));});}_menuAdd.DropDownItems.Add(files);
            var turtle=new ToolStripMenuItem("Disegno · tartaruga");foreach(NodeKind kind in FlowTurtle.Kinds){NodeKind captured=kind;turtle.DropDownItems.Add(Ui.KindName(kind),null,delegate{AddNodeAt(captured,ClientToWorld(_menuLocation));});}_menuAdd.DropDownItems.Add(turtle);
            _menuSelectAll = new ToolStripMenuItem("Seleziona tutti     Ctrl+A", null, delegate { SelectAllNodes(); });
            _menu.Items.AddRange(new ToolStripItem[]
            {
                _menuCancel, _menuConnectionSeparator, _menuEdit, _menuBreakpoint, _menuRunTo, foldCycleMenu, _menuNext, _menuTrue, _menuFalse,
                _menuEditSeparator, _menuCopy, _menuCut, _menuPaste, _menuDuplicate, _menuDelete, _menuSelectionSeparator, _menuAdd,
                _menuSelectAll,
                new ToolStripMenuItem("Adatta alla finestra", null, delegate { FitToView(); }),
                new ToolStripMenuItem("Zoom 100%", null, delegate { Zoom = 1; })
            });
        }

        private void ConfigureContextMenu(Point location)
        {
            BlockPicker.ApplyMenuTheme(_menu);
            FlowNode node = HitNode(ClientToWorld(location));
            FlowEdge edge = node == null ? HitEdge(ClientToWorld(location)) : null;
            if (node != null && !_selectedNodeIds.Contains(node.Id)) SetSelection(FoldSelection(new[]{node.Id}),null);
            else if (edge != null) SelectEdge(edge.Id);
            FlowNode single = FindNode(SelectedNodeId);
            var heading = (FlowMenuHeading)_menu.Items[0]; heading.Title = SelectionCount > 1 ? SelectionCount + " blocchi selezionati" : single != null ? Ui.KindName(single.Kind) : SelectedEdgeId != null ? "Collegamento" : "Sul foglio";
            bool anyNodes = SelectionCount > 0;
            bool anySelection = anyNodes || SelectedEdgeId != null;
            _menuCancel.Visible = _menuConnectionSeparator.Visible = IsConnecting;
            _menuEdit.Visible = single != null;
            foldCycleMenu.Visible=CanFoldSelection;foldCycleMenu.Text=SelectionCycleFolded?"Espandi ciclo sul foglio":"Riduci ciclo sul foglio";
            _menuBreakpoint.Visible=single!=null&&single.Kind!=NodeKind.Note&&BreakpointRequested!=null;_menuBreakpoint.Checked=single!=null&&single.Breakpoint;
            _menuRunTo.Visible=single!=null&&single.Kind!=NodeKind.Note&&RunToRequested!=null;_menuRunTo.Enabled=RunToEnabled==null||RunToEnabled();
            _menuNext.Visible = single != null && single.Kind != NodeKind.Decision && single.Kind != NodeKind.End && single.Kind != NodeKind.Note;
            _menuTrue.Visible = _menuFalse.Visible = single != null && single.Kind == NodeKind.Decision;
            if (single != null && single.Kind == NodeKind.Decision)
            {
                _menuTrue.Text = "Collega il ramo " + single.GetBranchLabel("true").Replace("&", "&&");
                _menuFalse.Text = "Collega il ramo " + single.GetBranchLabel("false").Replace("&", "&&");
            }
            _menuEditSeparator.Visible = single != null;
            _menuCopy.Visible = _menuCut.Visible = anyNodes;
            _menuCopy.Enabled = _menuCut.Enabled = anyNodes&&!ViewOnly;
            _menuPaste.Enabled = !ViewOnly&&FlowClipboard.ContainsBlocks;
            _menuDuplicate.Visible = anyNodes;
            _menuDuplicate.Enabled = _diagram != null && ProjectNodeCount + SelectionCount <= DiagramIO.MaxNodes;
            _menuDuplicate.Text = SelectionCount > 1 ? "Duplica gruppo     Ctrl+D" : "Duplica blocco     Ctrl+D";
            _menuDelete.Visible = anySelection;
            _menuDelete.Text = SelectionCount > 1 ? "Elimina gruppo     Canc" : anyNodes ? "Elimina blocco     Canc" : "Elimina collegamento     Canc";
            _menuSelectionSeparator.Visible = anySelection;
            _menuAdd.Enabled = _diagram != null && ProjectNodeCount < DiagramIO.MaxNodes;
            _menuSelectAll.Enabled = _diagram != null && _diagram.Nodes.Count != 0;
            FlowMenus.Apply(_menu);
        }

        private FlowNode HitNode(PointF world)
        {
            if (_diagram == null) return null;
            EnsureScene();
            for (int i = _nodeVisuals.Length - 1; i >= 0; i--)
            {
                NodeVisual node = _nodeVisuals[i];
                RectangleF bounds = node.Bounds;
                RectangleF touchBounds = bounds; touchBounds.Inflate(3 / _zoom, 3 / _zoom);
                if (!touchBounds.Contains(world)) continue;
                using (Pen tolerance = new Pen(Color.Black, 7 / _zoom))
                    if (node.Shape.IsVisible(world) || node.Shape.IsOutlineVisible(world, tolerance)) return FindNode(node.Model.Id);
            }
            return null;
        }

        private FlowEdge HitEdge(PointF world)
        {
            if (_diagram == null) return null;
            EnsureScene();
            for(int i=_edgeVisuals.Length-1;i>=0;i--){
                var edge=_edgeVisuals[i];if(edge.Points==null||edge.Path==null)continue;
                var bounds=edge.Bounds;bounds.Inflate(9/_zoom,9/_zoom);if(!bounds.Contains(world))continue;
                if(edge.HitPoints==null)using(var flat=(GraphicsPath)edge.Path.Clone()){flat.Flatten(null,.35f);edge.HitPoints=flat.PathPoints;}
                float radius=6/_zoom;for(int j=1;j<edge.HitPoints.Length;j++)if(InsertSegmentDistance(world,edge.HitPoints[j-1],edge.HitPoints[j])<=radius)return FindEdge(edge.Model.Id);
            }
            return null;
        }

        private string HitPort(FlowNode node, PointF world)
        {
            if (node == null || node.Kind == NodeKind.Note || node.Kind == NodeKind.End || IsFoldCard(node.Id)) return null;
            float radius = 13 / Math.Max(0.75f, _zoom);
            if (node.Kind == NodeKind.Decision)
            {
                if (Distance(world, OutputPoint(node, "true")) < radius) return "true";
                if (Distance(world, OutputPoint(node, "false")) < radius) return "false";
            }
            else if (Distance(world, OutputPoint(node, "next")) < radius) return "next";
            return null;
        }

        private void SetZoomAt(float zoom, Point anchor)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom)) return;
            PointF world = ClientToWorld(anchor);
            arrangeCamera=false;
            float next = Clamp(zoom, 0.18f, 2.5f);
            if (Math.Abs(_zoom - next) < 0.0001f) return;
            _zoom = next;
            _panX = anchor.X - world.X * _zoom;
            _panY = anchor.Y - world.Y * _zoom;
            RequestFrameRepaint();
            RaiseViewChanged();
        }

        private RectangleF GetContentBounds()
        {
            EnsureScene();
            return _contentBounds;
        }

        private FlowNode FindNode(string id)
        {
            if (string.IsNullOrEmpty(id) || _diagram == null) return null;
            NodeVisual cached;
            if (ReferenceEquals(_sceneDocument, _diagram) && _nodeIndex.TryGetValue(id, out cached) && cached.Model.Id == id &&
                cached.Index < _diagram.Nodes.Count && ReferenceEquals(_diagram.Nodes[cached.Index], cached.Model)) return cached.Model;
            for (int i = 0; i < _diagram.Nodes.Count; i++) if (_diagram.Nodes[i].Id == id) return _diagram.Nodes[i];
            return null;
        }

        private FlowEdge FindEdge(string id)
        {
            if (string.IsNullOrEmpty(id) || _diagram == null) return null;
            EdgeVisual cached;
            if (ReferenceEquals(_sceneDocument, _diagram) && _edgeIndex.TryGetValue(id, out cached) && cached.Model.Id == id &&
                cached.Index < _diagram.Edges.Count && ReferenceEquals(_diagram.Edges[cached.Index], cached.Model)) return cached.Model;
            for (int i = 0; i < _diagram.Edges.Count; i++) if (_diagram.Edges[i].Id == id) return _diagram.Edges[i];
            return null;
        }

        private float Snap(float value)
        {
            float result = SnapToGrid && (GetInteractionModifiers() & Keys.Alt) == 0 ? GridSnap.Round(value) : value;
            return ClampCoordinate(result);
        }

        private static float ClampCoordinate(float value)
        {
            return float.IsNaN(value) ? 0 : Clamp(value, -99900f, 99900f);
        }

        internal Func<int> OtherNodeCount,OtherEdgeCount;
        private int ProjectNodeCount {get{return _diagram.Nodes.Count+(OtherNodeCount==null?0:OtherNodeCount());}}
        private int ReservedEdges {get{return OtherEdgeCount==null?0:OtherEdgeCount();}}
        private int ProjectEdgeCount {get{return _diagram.Edges.Count+ReservedEdges;}}
        private bool CanAddNode()
        {
            if (ProjectNodeCount < DiagramIO.MaxNodes) return true;
            Report("Hai raggiunto il limite di " + DiagramIO.MaxNodes + " blocchi. Elimina un blocco prima di aggiungerne un altro.");
            return false;
        }

        private void InvalidateNode(string id, int padding)
        {
            FlowNode node = FindNode(id);
            if (node == null) return;
            RectangleF world = DisplayBounds(node);
            PointF top = WorldToClient(world.Location);
            Rectangle screen = Rectangle.Ceiling(new RectangleF(top.X, top.Y, world.Width * _zoom, world.Height * _zoom));
            screen.Inflate(padding, padding);
            RequestFrameRepaint(screen);
        }

        private void InvalidateEdge(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            EnsureScene();
            EdgeVisual edge;
            if (!_edgeIndex.TryGetValue(id, out edge) || edge.Points == null) return;
            RectangleF world = edge.Bounds;
            PointF top = WorldToClient(world.Location);
            Rectangle screen = Rectangle.Ceiling(new RectangleF(top.X, top.Y, world.Width * _zoom, world.Height * _zoom));
            screen.Inflate(25, 25);
            RequestFrameRepaint(screen);
        }

        private void InvalidateExecution()
        {
            if (IsDisposed || !Visible) return;
            InvalidateNode(_currentNodeId, 30);
        }

        private void UpdatePulseTimer()
        {
            if (_pulseTimer == null) return;
            _pulseTimer.Enabled = Visible && UiMotion.Enabled && flowAnimating && flowClock.ElapsedMilliseconds < flowDuration + 120;
        }

        private static float Pulse()
        {
            return 0.5f + 0.5f * (float)Math.Sin((Environment.TickCount & int.MaxValue) / 260.0);
        }

        private string ConnectionHint()
        {
            if (_connectBranch == "true" || _connectBranch == "false")
            {
                FlowNode source = FindNode(_connectSourceId);
                string label = source == null ? (_connectBranch == "true" ? "Sì" : "No") : source.GetBranchLabel(_connectBranch);
                return "Ramo " + label + ": clicca un blocco o il foglio";
            }
            return "Clicca un blocco o il foglio per continuare";
        }

        private static string DefaultText(NodeKind kind)
        {
            if(FlowTurtle.IsTurtle(kind))return FlowTurtle.DefaultText(kind);
            switch (kind)
            {
                case NodeKind.Start: return "Inizio";
                case NodeKind.End: return "Fine";
                case NodeKind.Input: return "x";
                case NodeKind.Output: return "x";
                case NodeKind.Decision: return "x > 0";
                case NodeKind.Declare:return "numero: INTERO";
                case NodeKind.Note:return "Scrivi una nota";
                case NodeKind.Pause:return "Pausa";
                case NodeKind.Call: return "miaFunzione()";
                case NodeKind.OpenFile:return "LETTURA \"dati.txt\"";case NodeKind.ReadFile:return "riga";case NodeKind.WriteFile:return "riga";case NodeKind.CloseFile:return "LETTURA";
                default: return "x = 0";
            }
        }

        private static string FriendlyNodeText(FlowNode node)
        {
            if (!string.IsNullOrWhiteSpace(FlowSizing.Text(node))) return FlowSizing.Text(node);
            switch (node.Kind)
            {
                case NodeKind.Start: return "Inizio";
                case NodeKind.End: return "Fine";
                case NodeKind.Input: return "Nome variabile";
                case NodeKind.Output: return "Valore da mostrare";
                case NodeKind.Decision: return "Scrivi una condizione";
                case NodeKind.Declare:return "nome: TIPO";
                case NodeKind.Note:return "Scrivi una nota";
                case NodeKind.Call: return "Scrivi una chiamata";
                default: return "Scrivi un'operazione";
            }
        }

        private static string TypeLabel(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Start: return "INIZIO";
                case NodeKind.End: return "FINE";
                case NodeKind.Input: return IoInstruction.Separate?"INPUT":"INPUT / OUTPUT";
                case NodeKind.Output: return IoInstruction.Separate?"OUTPUT":"INPUT / OUTPUT";
                case NodeKind.Decision: return "CONDIZIONE";
                case NodeKind.Declare:return "DICHIARA";
                case NodeKind.Note:return "NOTA";
                case NodeKind.Pause:return "PAUSA";
                case NodeKind.Call: return "CHIAMATA";
                case NodeKind.TurtleForward:return "AVANZA";case NodeKind.TurtleTurn:return "RUOTA";case NodeKind.TurtleHome:return "ORIGINE TARTARUGA";case NodeKind.TurtleClear:return "PULISCI DISEGNO";case NodeKind.TurtlePen:return "COLORE PENNA";case NodeKind.OpenFile:return "APRI FILE";case NodeKind.ReadFile:return "LEGGI FILE";case NodeKind.WriteFile:return "SCRIVI FILE";case NodeKind.CloseFile:return "CHIUDI FILE";
                default: return "OPERAZIONE";
            }
        }

        private Color TypeColor(NodeKind kind){return appearance.Kind(kind,BaseTypeColor(kind),Dark);}
        private Color BaseTypeColor(NodeKind kind)
        {
            if (Dark) return Ui.KindColor(kind);
            switch (kind)
            {
                case NodeKind.Note: return Ui.Muted;
                case NodeKind.Start: return Green;
                case NodeKind.End: return Color.FromArgb(102, 115, 138);
                case NodeKind.Input: return Color.FromArgb(50, 133, 186);
                case NodeKind.Output: return Color.FromArgb(129, 75, 206);
                case NodeKind.Decision: return Color.FromArgb(173, 118, 30);
                default: return Accent;
            }
        }

        private Color BranchColor(string branch){return appearance.Ink(BaseBranchColor(branch),Dark);}
        private Color BaseBranchColor(string branch)
        {
            if (Dark) return branch == "true" ? Color.FromArgb(74, 195, 167) :
                branch == "false" ? Color.FromArgb(235, 137, 162) : Color.FromArgb(147, 154, 170);
            return branch == "true" ? Color.FromArgb(54, 159, 139) :
                branch == "false" ? Color.FromArgb(202, 117, 139) : Color.FromArgb(150, 165, 187);
        }

        private void DrawCenteredText(Graphics graphics, string text, Font font, Color color, RectangleF bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            GraphicsState state = graphics.Save();
            try
            {
                // GDI+ can draw multiline/truncated glyphs outside its layout rectangle.
                // Clip explicitly so long content cannot overlap a node's type label.
                graphics.SetClip(bounds, CombineMode.Intersect);
                using (StringFormat format = new StringFormat())
                using (Brush brush = new SolidBrush(color))
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    format.FormatFlags = StringFormatFlags.LineLimit;
                    string value = text ?? string.Empty;
                    TextLayoutKey key = new TextLayoutKey { Text = value, Font = font, Width = bounds.Width, Height = bounds.Height, Dpi = graphics.DpiY };
                    bool tall;
                    if (!_textLayout.TryGetValue(key, out tall))
                    {
                        SizeF measured = FlowText.Measure(graphics, value, font, new SizeF(bounds.Width, 100000f), format);
                        tall = measured.Height > bounds.Height + 0.5f;
                        if (_textLayout.Count >= 2048) _textLayout.Clear();
                        _textLayout.Add(key, tall);
                    }
                    if (tall) format.LineAlignment = StringAlignment.Near;
                    if (!FlowText.DrawUnicode(graphics, value, font, color, bounds, format)) graphics.DrawString(value, font, brush, bounds, format);
                }
            }
            finally { graphics.Restore(state); }
        }

        private static RectangleF BoundsOf(IList<PointF> points)
        {
            if (points.Count == 0) return RectangleF.Empty;
            float left = points[0].X, top = points[0].Y, right = left, bottom = top;
            for (int i = 1; i < points.Count; i++)
            {
                left = Math.Min(left, points[i].X); top = Math.Min(top, points[i].Y);
                right = Math.Max(right, points[i].X); bottom = Math.Max(bottom, points[i].Y);
            }
            return RectangleF.FromLTRB(left, top, Math.Max(left + 0.1f, right), Math.Max(top + 0.1f, bottom));
        }

        private static float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        private void Report(string message) { if (StatusChanged != null) StatusChanged(this, new StatusEventArgs(message)); }
        private void RaiseSelectionChanged() { NotifyAccessibleSelection(); if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }
        private void RaiseDiagramChanging() { if (DiagramChanging != null) DiagramChanging(this, EventArgs.Empty); }
        private void RaiseDiagramChanged() { FlowAnnotations.Promote(_diagram);NotifyFoldModelChanged();NotifyAccessibleStructure();RequestFrameRepaint(); if (DiagramChanged != null) DiagramChanged(this, EventArgs.Empty); }
        private void RaiseViewChanged() { if (ViewChanged != null) ViewChanged(this, EventArgs.Empty); }
        private void RaiseConnectionStateChanged() { if (ConnectionStateChanged != null) ConnectionStateChanged(this, EventArgs.Empty); }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            BackColor = Paper;
            ForeColor = Ink;
            if (_menu != null) BlockPicker.ApplyMenuTheme(_menu);
            RequestFrameRepaint();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearInsertPreview();
                StopBladeFeedback();
                Ui.ThemeChanged -= OnThemeChanged;
                _pickerGeneration++;
                _pickerDocument = null;
                _pickerQueued = false;
                if (_blockPicker != null) _blockPicker.Dispose();
                _pulseTimer.Stop(); _pulseTimer.Dispose();
                _toolTip.Dispose();
                if (_menu != null) _menu.Dispose();
                ClearScene();
                if (_frame != null) { _frame.Dispose(); _frame = null; }
                DisposeGrid();
                _textLayout.Clear();
                _bodyFont.Dispose(); _bodyBoldFont.Dispose(); _decisionFont.Dispose();
                _labelFont.Dispose(); _smallFont.Dispose(); _smallBoldFont.Dispose(); _emptyTitleFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
