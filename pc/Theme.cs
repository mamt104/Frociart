using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Frociart
{
    internal static class Ui
    {
        internal static readonly Color LightInk = Color.FromArgb(29, 29, 31);
        internal static readonly Color LightMuted = Color.FromArgb(110, 110, 115);
        private static readonly Color[] semanticLight={LightInk,LightMuted,Color.FromArgb(0,122,255),Color.FromArgb(36,138,83),Color.FromArgb(213,59,66),Color.FromArgb(170,106,14),Color.FromArgb(120,101,156),Color.FromArgb(13,133,171),Color.FromArgb(129,75,206),Color.FromArgb(200,134,24)};
        private static readonly Color[] legacyDark={Color.FromArgb(243,243,247),Color.FromArgb(170,170,182),Color.FromArgb(90,165,255),Color.FromArgb(81,212,146),Color.FromArgb(255,120,127),Color.FromArgb(241,186,101),Color.FromArgb(186,164,227),Color.FromArgb(102,209,232),Color.FromArgb(188,153,255),Color.FromArgb(240,180,91)};
        private static readonly Color[] semanticDark={Color.FromArgb(243,244,248),Color.FromArgb(183,189,206),Color.FromArgb(117,172,255),Color.FromArgb(82,208,172),Color.FromArgb(255,146,173),Color.FromArgb(255,202,114),Color.FromArgb(186,164,227),Color.FromArgb(102,209,232),Color.FromArgb(188,153,255),Color.FromArgb(240,180,91)};
        private static bool isDark = ReadThemePreference();
        static Ui(){try{IoInstruction.Separate=File.ReadAllText(IoPreferencePath).Trim()=="separate";}catch{}}
        private static string IoPreferencePath {get{return Path.Combine(Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart"),"io-presentation.txt");}}
        internal static void SetSeparateIo(bool separate,bool persist=true){IoInstruction.Separate=separate;if(persist)try{Directory.CreateDirectory(Path.GetDirectoryName(IoPreferencePath));File.WriteAllText(IoPreferencePath,separate?"separate":"unified");}catch{}}

        internal static bool IsDark { get { return isDark; } }
        internal static Color Ink { get { return isDark ? Color.FromArgb(243,244,248) : LightInk; } }
        internal static Color Muted { get { return isDark ? Color.FromArgb(183,189,206) : LightMuted; } }
        internal static Color Line { get { return isDark ? Color.FromArgb(62,66,77) : Color.FromArgb(226,226,231); } }
        internal static Color Paper { get { return isDark ? Color.FromArgb(23,24,28) : Color.FromArgb(245,245,247); } }
        internal static Color Surface { get { return isDark ? Color.FromArgb(32,34,40) : Color.White; } }
        internal static Color Raised { get { return isDark ? Color.FromArgb(36,38,45) : Color.FromArgb(252,252,253); } }
        internal static Color Hover { get { return isDark ? Color.FromArgb(48,61,80) : Color.FromArgb(238,245,255); } }
        internal static Color InputBackground { get { return isDark ? Color.FromArgb(22,22,26) : Color.White; } }
        internal static Color Tint { get { return isDark ? Color.FromArgb(34,48,65) : Color.FromArgb(236,245,253); } }
        internal static Color Blue { get { return isDark ? Color.FromArgb(117,172,255) : Color.FromArgb(0,122,255); } }
        internal static Color Green { get { return isDark ? Color.FromArgb(82,208,172) : Color.FromArgb(36,138,83); } }
        internal static Color Red { get { return isDark ? Color.FromArgb(255,146,173) : Color.FromArgb(213,59,66); } }
        internal static Color Amber { get { return isDark ? Color.FromArgb(255,202,114) : Color.FromArgb(170,106,14); } }
        internal static event EventHandler ThemeChanged;
        internal static void FitStartupToScreen(Form form)
        {
            if(!form.ShowInTaskbar||form.StartPosition==FormStartPosition.Manual||form.WindowState!=FormWindowState.Normal)return;
            var area=Screen.FromControl(form).WorkingArea;
            form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,area.Width),Math.Min(form.MinimumSize.Height,area.Height));
            form.Size=new Size(Math.Min(form.Width,area.Width),Math.Min(form.Height,area.Height));
            form.Location=new Point(Math.Max(area.Left,Math.Min(form.Left,area.Right-form.Width)),Math.Max(area.Top,Math.Min(form.Top,area.Bottom-form.Height)));
        }
        internal static string ThemePreferencePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart","theme.txt"); }
        }

        private static bool ReadThemePreference()
        {
            try { return String.Equals(File.ReadAllText(ThemePreferencePath).Trim(),"dark",StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        internal static void SetDarkMode(bool dark, bool persist = true)
        {
            bool changed = isDark != dark;
            isDark = dark;
            if(persist)
            {
                string temp = null;
                try
                {
                    string path = ThemePreferencePath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    temp = path+"."+Guid.NewGuid().ToString("N")+".tmp";
                    File.WriteAllText(temp,dark ? "dark" : "light");
                    if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
                }
                catch { }
                finally { if(temp!=null)try { if(File.Exists(temp))File.Delete(temp); } catch { } }
            }
            if(!changed)return;
            foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())
                if(!form.IsDisposed)ApplyTheme(form);
            var handler=ThemeChanged;if(handler!=null)handler(null,EventArgs.Empty);
        }

        // Resolve both palettes so existing controls keep their semantic colors
        // across any number of light/dark changes, including an active run.
        internal static Color ThemedColor(Color value)
        {
            for(int i=0;i<semanticLight.Length;i++)if(value.ToArgb()==semanticLight[i].ToArgb()||value.ToArgb()==semanticDark[i].ToArgb()||value.ToArgb()==legacyDark[i].ToArgb())return isDark?semanticDark[i]:semanticLight[i];
            return value;
        }

        internal static void ApplyTheme(Control root)
        {
            if(root==null||root.IsDisposed)return;
            root.SuspendLayout();
            try{ApplyThemeTree(root);}
            finally{root.ResumeLayout(true);}
            // One recursive invalidation after the complete palette is in
            // place, instead of repainting every subtree for every ancestor.
            root.Invalidate(true);
        }

        private static void ApplyThemeTree(Control root)
        {
            if(root==null||root.IsDisposed||Object.Equals(root.Tag,"fixed-light"))return;
            root.ForeColor=ThemedColor(root.ForeColor);
            var menu=root as ContextMenuStrip;
            if(menu!=null)StyleMenu(menu);
            else if(root is TextBoxBase)
            { root.BackColor=((TextBoxBase)root).ReadOnly?Raised:InputBackground;root.ForeColor=Ink; }
            else if(root is ListView || root is ComboBox || root is ListBox || root is NumericUpDown)
            { root.BackColor=InputBackground;root.ForeColor=Ink; }
            else if(root is Form || root is TabPage){root.BackColor=Surface;var tab=root as TabPage;if(tab!=null)tab.UseVisualStyleBackColor=false;}
            else if(root is TabControl)root.BackColor=Paper;
            else if(!(root is FlowButton)&&!(root is BlockButton)&&root.BackColor.A!=0)
            {
                Color back=root.BackColor;
                if(Object.Equals(root.Tag,"line"))root.BackColor=Line;
                else if(Object.Equals(root.Tag,"paper")||Same(back,245,245,247)||Same(back,23,24,28))root.BackColor=Paper;
                else if(Object.Equals(root.Tag,"tint")||Same(back,236,245,253)||Same(back,34,48,65))root.BackColor=Tint;
                else if(Object.Equals(root.Tag,"raised")||Same(back,252,252,253)||Same(back,36,38,45)||Same(back,247,248,250))root.BackColor=Raised;
                else if(!(root is Label))root.BackColor=Surface;
            }
            if(root.ContextMenuStrip!=null&&!root.ContextMenuStrip.IsDisposed)StyleMenu(root.ContextMenuStrip);
            NativeTheme.Apply(root);
            var form = root as Form; if(form!=null) FlowFormMotion.Attach(form);
            foreach(Control child in root.Controls)ApplyThemeTree(child);
        }

        private static bool Same(Color color,int r,int g,int b)
        {return color.ToArgb()==Color.FromArgb(r,g,b).ToArgb();}

        internal static ToolStripRenderer CreateMenuRenderer() { return new FlowMenuRenderer(); }
        internal static void StyleMenu(ContextMenuStrip menu)
        {
            if(menu==null||menu.IsDisposed)return;
            FlowMenus.Apply(menu);
        }
        private static void StyleMenuItem(ToolStripItem item)
        {
            item.BackColor=Surface;item.ForeColor=Ink;
            var sub=item as ToolStripDropDownItem;
            if(sub!=null&&sub.HasDropDownItems)
            {sub.DropDown.Renderer=CreateMenuRenderer();sub.DropDown.BackColor=Surface;sub.DropDown.ForeColor=Ink;foreach(ToolStripItem child in sub.DropDownItems)StyleMenuItem(child);}
        }

        internal static Font Font(float size = 9.5f, FontStyle style = FontStyle.Regular)
        { return new Font("Segoe UI", size, style, GraphicsUnit.Point); }
        internal static Font Mono(float size = 10f)
        { return new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Point); }
        internal static Label Label(string text, float size = 9.5f, Color? color = null, bool bold = false)
        {
            return new Label { Text = text, Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = color ?? Ink, AutoSize = false, BackColor = Color.Transparent, UseMnemonic = false };
        }
        internal static FlowButton Button(string text, int width, Action click, bool primary = false)
        {
            var b = new FlowButton { Text = text, Width = width, Height = 34, Primary = primary };
            if (click != null) b.Click += delegate { click(); };
            return b;
        }
        internal static TextBox TextBox(bool multiline = false)
        {
            return new FlowTextBox { Multiline = multiline, Font = multiline ? Mono() : Font(), ForeColor = Ink,
                BackColor = InputBackground, BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                AcceptsReturn = multiline, MaxLength = 8192 };
        }
        internal static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            if(r.Width<=0||r.Height<=0)return p;
            radius=Math.Max(0,Math.Min(radius,Math.Min(r.Width,r.Height)/2));
            if(radius<.1f){p.AddRectangle(r);return p;}
            float d = radius * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        internal static string KindName(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Start: return "Inizio";
                case NodeKind.End: return "Fine";
                case NodeKind.Input: return "Input";
                case NodeKind.Output: return "Output";
                case NodeKind.Decision: return "Decisione";
                case NodeKind.Declare:return "Dichiara";
                case NodeKind.Note:return "Nota libera";
                case NodeKind.Pause:return "Pausa";
                case NodeKind.Call: return "Chiamata";
                case NodeKind.TurtleForward:return "Avanza";case NodeKind.TurtleTurn:return "Ruota";case NodeKind.TurtleHome:return "Origine tartaruga";case NodeKind.TurtleClear:return "Pulisci disegno";case NodeKind.TurtlePen:return "Colore penna";case NodeKind.OpenFile:return "Apri file";case NodeKind.ReadFile:return "Leggi file";case NodeKind.WriteFile:return "Scrivi file";case NodeKind.CloseFile:return "Chiudi file";
                default: return "Operazione";
            }
        }
        internal static Color KindColor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Note:return Muted;
                case NodeKind.Pause:return Red;
                case NodeKind.Start: return Green;
                case NodeKind.End: return ThemedColor(Color.FromArgb(120, 101, 156));
                case NodeKind.Input: return ThemedColor(Color.FromArgb(13, 133, 171));
                case NodeKind.Output: return ThemedColor(Color.FromArgb(129, 75, 206));
                case NodeKind.Decision: return ThemedColor(Color.FromArgb(200, 134, 24));
                default: return Blue;
            }
        }
        internal static void Shape(Graphics g, NodeKind kind, RectangleF r, Color color)
        {
            using (var p = new Pen(color, 1.8f))
            using (var b = new SolidBrush(Color.FromArgb(20, color)))
            {
                if(kind==NodeKind.Note)p.DashStyle=DashStyle.Dash;
                if (kind == NodeKind.Decision)
                {
                    PointF[] points = { new PointF(r.Left + r.Width/2, r.Top), new PointF(r.Right, r.Top+r.Height/2),
                        new PointF(r.Left+r.Width/2,r.Bottom),new PointF(r.Left,r.Top+r.Height/2) };
                    g.FillPolygon(b, points); g.DrawPolygon(p, points);
                }
                else if (kind == NodeKind.Input || kind == NodeKind.Output)
                {
                    PointF[] points = {new PointF(r.Left+6,r.Top),new PointF(r.Right,r.Top),new PointF(r.Right-6,r.Bottom),new PointF(r.Left,r.Bottom)};
                    if(kind==NodeKind.Input&&!IoInstruction.Separate){using(var gradient=new LinearGradientBrush(r,KindColor(NodeKind.Input),KindColor(NodeKind.Output),0f))using(var outline=new Pen(gradient,1.8f)){g.FillPolygon(b,points);g.DrawPolygon(outline,points);}}
                    else {g.FillPolygon(b, points); g.DrawPolygon(p, points);}
                }
                else
                {
                    using (var path = Rounded(r, kind == NodeKind.Start || kind == NodeKind.End ? r.Height/2 : 3))
                    { g.FillPath(b, path); g.DrawPath(p, path); }
                    if(kind==NodeKind.Call){float inset=r.Width*.18f;g.DrawLine(p,r.Left+inset,r.Top,r.Left+inset,r.Bottom);g.DrawLine(p,r.Right-inset,r.Top,r.Right-inset,r.Bottom);}
                }
            }
        }
    }

    internal sealed class ThemeMenuColors : ProfessionalColorTable
    {
        public ThemeMenuColors(){UseSystemColors=false;}
        public override Color ToolStripDropDownBackground {get{return Ui.Surface;}}
        public override Color ImageMarginGradientBegin {get{return Ui.Surface;}}
        public override Color ImageMarginGradientMiddle {get{return Ui.Surface;}}
        public override Color ImageMarginGradientEnd {get{return Ui.Surface;}}
        public override Color MenuItemSelected {get{return Ui.Hover;}}
        public override Color MenuItemBorder {get{return Ui.Line;}}
        public override Color MenuBorder {get{return Ui.Line;}}
        public override Color SeparatorDark {get{return Ui.Line;}}
        public override Color SeparatorLight {get{return Ui.Surface;}}
        public override Color MenuItemSelectedGradientBegin {get{return Ui.Hover;}}
        public override Color MenuItemSelectedGradientEnd {get{return Ui.Hover;}}
        public override Color MenuItemPressedGradientBegin {get{return Ui.Hover;}}
        public override Color MenuItemPressedGradientMiddle {get{return Ui.Hover;}}
        public override Color MenuItemPressedGradientEnd {get{return Ui.Hover;}}
    }
    internal sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemeMenuRenderer():base(new ThemeMenuColors()){RoundedEdges=true;}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        { e.TextColor=e.Item.Enabled?Ui.Ink:Ui.Muted;base.OnRenderItemText(e); }
    }

    internal enum ButtonGlyph { None, Check, Play, Step, Pause, Undo, Redo, Export, Moon, Sun }

    internal sealed class FlowButton : Button
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Primary { get; set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal Color? Accent { get; set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal ButtonGlyph Glyph { get; set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool PreserveOriginalStyle { get; set; }
        private bool hover;
        private readonly MotionValue hoverMotion, pressMotion;
        public FlowButton()
        {
            hoverMotion = new MotionValue(this); pressMotion = new MotionValue(this);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Ui.Font(9.2f, FontStyle.Bold); Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; TabStop = true;
            Margin = new Padding(0, 0, 8, 0); BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; hoverMotion.Set(1); Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; hoverMotion.Set(0); pressMotion.Set(0,90); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left){pressMotion.Set(1,65);}Invalidate();base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressMotion.Set(0,110);Invalidate();base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Ui.Paper);
            if(Width<6||Height<6)return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if(PreserveOriginalStyle){PaintOriginal(e);return;}
            Color accent = Ui.ThemedColor(Accent ?? Ui.Blue);
            float hoverAmount=hoverMotion.Value, pressAmount=pressMotion.Value;
            Color bg = Primary ? accent : UiMotion.Blend(Ui.Raised, Ui.Hover, Math.Max(hoverAmount,pressAmount));
            Color fg = Primary ? (Ui.IsDark?Color.FromArgb(15,22,32):Color.White) : Ui.ThemedColor(Accent ?? Ui.Ink);
            if (!Enabled) { bg = Ui.IsDark?Ui.Surface:Color.FromArgb(247,247,249); fg = Ui.IsDark?Color.FromArgb(100,100,112):Color.FromArgb(174,174,181); }
            else if (Primary) bg = UiMotion.Blend(accent, ControlPaint.Dark(accent,.15f),Math.Max(hoverAmount*.38f,pressAmount));
            if(Enabled&&Primary&&!String.IsNullOrWhiteSpace(Text))bg=ReadablePrimaryBackground(bg,fg);
            using (var path = Ui.Rounded(new RectangleF(1+pressAmount, 1+pressAmount, Width-3-pressAmount*2, Height-3-pressAmount*2), 9))
            using (var brush = new SolidBrush(bg))
            using (var pen = new Pen(Primary ? bg : Ui.Line))
            { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            if(Image!=null){float size=Math.Min(Height-12,22*e.Graphics.DpiX/96);var r=new RectangleF((Width-size)/2,(Height-size)/2,size,size);using(var attributes=new System.Drawing.Imaging.ImageAttributes()){var matrix=new System.Drawing.Imaging.ColorMatrix(new float[][]{new float[]{0,0,0,0,0},new float[]{0,0,0,0,0},new float[]{0,0,0,0,0},new float[]{0,0,0,fg.A/255f,0},new float[]{fg.R/255f,fg.G/255f,fg.B/255f,0,1}});attributes.SetColorMatrix(matrix);e.Graphics.DrawImage(Image,Rectangle.Round(r),0,0,Image.Width,Image.Height,GraphicsUnit.Pixel,attributes);}}
            else if(Glyph==ButtonGlyph.None)
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(5, 2, Width-10, Height-4), fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (UseMnemonic ? TextFormatFlags.Default : TextFormatFlags.NoPrefix));
            else
            {
                float scale=NativeTheme.DpiScale(this);int glyph=(int)Math.Round(15*scale),gap=(int)Math.Round(7*scale),inset=(int)Math.Round(9*scale);
                int textWidth=String.IsNullOrEmpty(Text)?0:Math.Max(0,Math.Min(Width-glyph-gap-inset,TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.NoPadding | (UseMnemonic ? TextFormatFlags.Default : TextFormatFlags.NoPrefix)).Width));
                int groupWidth=glyph+(textWidth>0?gap+textWidth:0);
                int left=(Width-groupWidth)/2;
                DrawGlyph(e.Graphics,Glyph,new RectangleF(left,(Height-glyph)/2f,glyph,glyph),fg);
                if(textWidth>0)TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(left+glyph+gap,1,textWidth+1,Height-3),fg,
                    TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding | (UseMnemonic ? TextFormatFlags.Default : TextFormatFlags.NoPrefix));
            }
            if(Focused&&ShowFocusCues)
                using(var ring=Ui.Rounded(new RectangleF(3,3,Width-7,Height-7),7))
                using(var pen=new Pen(Primary?(Ui.IsDark?Ui.Ink:Color.White):Ui.Blue,1.4f))e.Graphics.DrawPath(pen,ring);
        }

        // Primary captions are normal-size text. Keep their foreground at 4.5:1
        // in each painted interaction state, while retaining the accent hue.
        // Empty colour swatches and deliberately preserved legacy controls keep
        // their exact fills; disabled controls are not interactive text.
        private static double LinearChannel(int value){double s=value/255.0;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}
        private static double Luminance(Color c){return .2126*LinearChannel(c.R)+.7152*LinearChannel(c.G)+.0722*LinearChannel(c.B);}
        private static double TextContrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return(Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        private static Color ReadablePrimaryBackground(Color original,Color foreground){
            if(TextContrast(original,foreground)>=4.5)return original;
            int target=Luminance(foreground)<.5?255:0;
            for(int percent=1;percent<=100;percent++){
                var candidate=Color.FromArgb(original.A,(original.R*(100-percent)+target*percent)/100,(original.G*(100-percent)+target*percent)/100,(original.B*(100-percent)+target*percent)/100);
                if(TextContrast(candidate,foreground)>=4.5)return candidate;
            }
            return original;
        }

        private void PaintOriginal(PaintEventArgs e)
        {
            Color accent=Accent??Color.FromArgb(65,99,235);
            Color bg=Primary?accent:(hover?Color.FromArgb(237,242,252):Color.White);
            Color fg=Primary?Color.White:(Accent??Color.FromArgb(28,42,61));
            if(!Enabled){bg=Color.FromArgb(243,245,249);fg=Color.FromArgb(166,177,192);}
            else if(hover&&Primary)bg=ControlPaint.Dark(accent,.07f);
            using(var path=Ui.Rounded(new RectangleF(1,1,Width-3,Height-3),7))
            using(var brush=new SolidBrush(bg))
            using(var pen=new Pen(Primary?bg:Color.FromArgb(227,233,241)))
            {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(5,2,Width-10,Height-4),fg,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)using(var ring=Ui.Rounded(new RectangleF(4,4,Width-9,Height-9),8))using(var pen=new Pen(Ui.Blue,1.4f))e.Graphics.DrawPath(pen,ring);
        }

        private static void DrawGlyph(Graphics g,ButtonGlyph glyph,RectangleF r,Color color)
        {
            var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/16f,r.Height/16f);
            using(var pen=new Pen(color,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
            using(var brush=new SolidBrush(color))
            {
                if(glyph==ButtonGlyph.Check)g.DrawLines(pen,new[]{new PointF(2,8),new PointF(6,12),new PointF(14,3)});
                else if(glyph==ButtonGlyph.Play)g.FillPolygon(brush,new[]{new PointF(4,2),new PointF(14,8),new PointF(4,14)});
                else if(glyph==ButtonGlyph.Step){g.FillPolygon(brush,new[]{new PointF(2,3),new PointF(10,8),new PointF(2,13)});g.DrawLine(pen,13,3,13,13);}
                else if(glyph==ButtonGlyph.Pause){g.FillRectangle(brush,3,3,3,10);g.FillRectangle(brush,10,3,3,10);}
                else if(glyph==ButtonGlyph.Export)
                {g.DrawLines(pen,new[]{new PointF(3,10),new PointF(3,14),new PointF(13,14),new PointF(13,10)});g.DrawLine(pen,8,10,8,1);g.DrawLines(pen,new[]{new PointF(4,5),new PointF(8,1),new PointF(12,5)});}
                else if(glyph==ButtonGlyph.Sun)
                {g.DrawEllipse(pen,5,5,6,6);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,8+(float)Math.Cos(a)*6,8+(float)Math.Sin(a)*6,8+(float)Math.Cos(a)*7.5f,8+(float)Math.Sin(a)*7.5f);}}
                else if(glyph==ButtonGlyph.Moon)
                {using(var moon=new GraphicsPath())using(var cutout=new GraphicsPath()){moon.AddEllipse(2,1,12,13);cutout.AddEllipse(7,-1,11,12);using(var crescent=new Region(moon)){crescent.Exclude(cutout);g.FillRegion(brush,crescent);}}}
                else
                {
                    if(glyph==ButtonGlyph.Redo){g.TranslateTransform(16,0);g.ScaleTransform(-1,1);}
                    g.DrawArc(pen,3,5,11,9,205,235);
                    g.DrawLines(pen,new[]{new PointF(3,2),new PointF(3,7),new PointF(8,7)});
                }
            }
            g.Restore(state);
        }
    }

    internal sealed class BlockButton : Button
    {
        internal readonly NodeKind Kind;
        private readonly string description;
        private readonly MotionValue hoverMotion;
        public BlockButton(NodeKind kind, string desc)
        {
            hoverMotion=new MotionValue(this); Kind=kind; description=desc; Height=63; Width=168; FlatStyle=FlatStyle.Flat;
            FlatAppearance.BorderSize=0; Cursor=Cursors.Hand; Text=kind==NodeKind.Input&&!IoInstruction.Separate?"Input / Output":Ui.KindName(kind); AccessibleName=Text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint,true);
        }
        protected override void OnMouseEnter(EventArgs e) { hoverMotion.Set(1); Invalidate();base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverMotion.Set(0); Invalidate();base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Ui.Paper);
            if(Width<6||Height<6)return;
            float scale=NativeTheme.DpiScale(this);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using (var p=Ui.Rounded(new RectangleF(scale,scale,Width-3*scale,Height-4*scale),12*scale))
            using (var b=new SolidBrush(UiMotion.Blend(Ui.Raised,Ui.Hover,hoverMotion.Value)))
            using (var pen=new Pen(UiMotion.Blend(Ui.Line,Ui.Blue,hoverMotion.Value),scale))
            {e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}
            Ui.Shape(e.Graphics,Kind,new RectangleF(13*scale,21*scale,27*scale,20*scale),Ui.KindColor(Kind));
            int left=(int)Math.Round(51*scale);
            using(var f=Ui.Font(10,FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics,Text,f,new Rectangle(left,(int)Math.Round(11*scale),Math.Max(1,Width-(int)Math.Round(58*scale)),(int)Math.Round(23*scale)),Ui.Ink,TextFormatFlags.Left|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            using(var f=Ui.Font(8.1f))
                TextRenderer.DrawText(e.Graphics,description,f,new Rectangle(left,(int)Math.Round(35*scale),Math.Max(1,Width-(int)Math.Round(54*scale)),(int)Math.Round(20*scale)),Ui.Muted,TextFormatFlags.Left|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)using(var ring=Ui.Rounded(new RectangleF(4*scale,4*scale,Width-9*scale,Height-9*scale),8*scale))using(var pen=new Pen(Ui.Blue,1.4f*scale))e.Graphics.DrawPath(pen,ring);
        }
    }

    internal sealed class BrandMark : Control
    {
        public BrandMark() { DoubleBuffered=true; Size=new Size(40,40); TabStop=false; SetStyle(ControlStyles.Selectable,false); }
        protected override void OnPaint(PaintEventArgs e)
        {
            BrandArtwork.Draw(e.Graphics,new RectangleF(0,0,Width,Height),Ui.Blue);
        }
    }
}
