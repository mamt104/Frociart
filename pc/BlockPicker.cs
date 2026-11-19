using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed class BlockKindEventArgs : EventArgs
    {
        internal NodeKind Kind { get; private set; }
        internal BlockKindEventArgs(NodeKind kind) { Kind = kind; }
    }

    /// <summary>A persistent native popup. Items are never disposed from click/close callbacks.</summary>
    internal sealed class BlockPicker : FlowContextMenu
    {
        private readonly PickerHeader _header;
        private readonly Font _menuFont = Ui.Font(9f);
        private readonly PickerFooter _cancel;
        private readonly ToolStripMenuItem _disconnect;
        internal event EventHandler DisconnectRequested;
        internal event EventHandler<BlockKindEventArgs> KindSelected;
        internal event EventHandler CancelRequested;

        internal BlockPicker()
        {
            ShowImageMargin = false;
            ShowCheckMargin = false;
            Font = _menuFont;
            Padding = new Padding(7);
            _header = new PickerHeader();
            Items.Add(_header);
            foreach (NodeKind kind in IoInstruction.Separate?new[] { NodeKind.Input, NodeKind.Output, NodeKind.Process, NodeKind.Declare, NodeKind.Decision, NodeKind.Call, NodeKind.Pause, NodeKind.End }:new[] { NodeKind.Input, NodeKind.Process, NodeKind.Declare, NodeKind.Decision, NodeKind.Call, NodeKind.Pause, NodeKind.End })
            {
                Items.Add(CreateRow(kind));
            }
            var files=new ToolStripMenuItem("File · leggi e scrivi"){Name="FileBlockGroup",AccessibleDescription="Apri, leggi, scrivi e chiudi un file di testo."};
            foreach(NodeKind kind in FileBlocks.Kinds)files.DropDownItems.Add(CreateRow(kind));Items.Add(files);
            var turtle=new ToolStripMenuItem("Disegno · tartaruga"){Name="TurtleBlockGroup"};foreach(NodeKind kind in FlowTurtle.Kinds)turtle.DropDownItems.Add(CreateRow(kind));Items.Add(turtle);
            _disconnect=new ToolStripMenuItem("Scollega questo collegamento"){Visible=false,AccessibleDescription="Rimuove solo questa freccia e conserva tutte le caselle. Ctrl+Z ripristina."};_disconnect.Click+=delegate{if(DisconnectRequested!=null)DisconnectRequested(this,EventArgs.Empty);};Items.Add(_disconnect);
            _cancel = new PickerFooter();
            _cancel.Click += delegate { if (CancelRequested != null) CancelRequested(this, EventArgs.Empty); };
            Items.Add(_cancel);
            Ui.ThemeChanged += OnThemeChanged;
            ApplyMenuTheme(this);
        }

        private PickerRow CreateRow(NodeKind kind){
            var row=new PickerRow(kind);Point down=Point.Empty;bool dragged=false;
            row.MouseDown+=delegate(object sender,MouseEventArgs e){down=e.Location;dragged=false;};
            row.MouseMove+=delegate(object sender,MouseEventArgs e){if(dragged||e.Button!=MouseButtons.Left||Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)<SystemInformation.DragSize.Width)return;dragged=true;Hide();DoDragDrop(new FlowCanvas.PaletteBlock(kind,kind==NodeKind.Input&&!IoInstruction.Separate),DragDropEffects.Copy);};
            row.Click+=delegate{if(!dragged&&KindSelected!=null)KindSelected(this,new BlockKindEventArgs(kind));};return row;
        }
        private static void ScaleRows(ToolStripItemCollection items,float scale){foreach(ToolStripItem item in items){var row=item as PickerRow;if(row!=null)row.SetScale(scale);var group=item as ToolStripMenuItem;if(group!=null&&group.HasDropDownItems)ScaleRows(group.DropDownItems,scale);}}

        internal void ShowAt(Control owner, Point clientPoint, string branch)
        {
            _disconnect.Visible=owner is FlowCanvas&&((FlowCanvas)owner).HasConnectionToDisconnect;
            _header.ExistingConnection=_disconnect.Visible;
            _header.Branch = branch;
            _header.BranchLabel = null;
            FlowCanvas canvas=owner as FlowCanvas;
            if(canvas!=null&&canvas.Diagram!=null)
                foreach(FlowNode node in canvas.Diagram.Nodes)
                    if(node.Id==canvas.ConnectionSourceId){_header.BranchLabel=node.GetBranchLabel(branch);break;}
            // Finish native ToolStrip DPI initialization before applying logical item sizes.
            // Fonts remain in points; only geometry is scaled, once per opening.
            IntPtr nativeHandle = Handle;
            float scale;
            using (Graphics graphics = owner.CreateGraphics()) scale = Math.Max(0.75f, Math.Min(4f, graphics.DpiY / 96f));
            Padding = new Padding(Scaled(7, scale));
            _header.SetScale(scale);
            ScaleRows(Items,scale);
            _disconnect.AutoSize=false;_disconnect.Size=new Size(Scaled(304,scale),Scaled(40,scale));
            _cancel.SetScale(scale);
            ApplyMenuTheme(this);
            PerformLayout();
            Size desired = GetPreferredSize(Size.Empty);
            Point anchor = owner.PointToScreen(clientPoint);
            Rectangle screen = Screen.FromPoint(anchor).WorkingArea;
            Rectangle viewport = Rectangle.Intersect(owner.RectangleToScreen(owner.ClientRectangle), screen);
            Rectangle available = viewport.Width >= desired.Width + 12 && viewport.Height >= desired.Height + 12 ? viewport : screen;
            int x = anchor.X + 12, y = anchor.Y + 12;
            if (x + desired.Width > available.Right - 6) x = anchor.X - desired.Width - 12;
            if (y + desired.Height > available.Bottom - 6) y = anchor.Y - desired.Height - 12;
            x = Math.Max(available.Left + 6, Math.Min(x, available.Right - desired.Width - 6));
            y = Math.Max(available.Top + 6, Math.Min(y, available.Bottom - desired.Height - 6));
            Show(new Point(x, y));
        }

        private static int Scaled(float value, float scale) { return (int)Math.Round(value * scale); }

        internal static void ApplyMenuTheme(ContextMenuStrip menu)
        {
            Ui.StyleMenu(menu);
            menu.Invalidate();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed) ApplyMenuTheme(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Ui.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
            if (disposing) _menuFont.Dispose();
        }

        private sealed class PickerHeader : ToolStripLabel
        {
            private readonly Font _titleFont = Ui.Font(12f, FontStyle.Bold);
            private readonly Font _detailFont = Ui.Font(8.8f);
            private float _scale = 1f;
            internal string Branch,BranchLabel;internal bool ExistingConnection;
            internal PickerHeader() { AutoSize = false; Size = new Size(304, 61); Enabled = false; }
            internal void SetScale(float scale)
            {
                _scale = scale;
                Size = new Size(Scaled(304, scale), Scaled(61, scale));
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Ui.Surface);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath card = Ui.Rounded(new RectangleF(_scale, _scale, Width - 3 * _scale, Height - 6 * _scale), 10 * _scale))
                using (Brush fill = new SolidBrush(Ui.Raised)) e.Graphics.FillPath(fill, card);
                TextRenderer.DrawText(e.Graphics, ExistingConnection?"Modifica collegamento":"Crea e collega", _titleFont,
                    new Rectangle(Scaled(12, _scale), Scaled(7, _scale), Width - Scaled(24, _scale), Scaled(25, _scale)), Ui.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                string detail = Branch == "true" || Branch == "false" ? "Ramo "+(BranchLabel??(Branch=="true"?"Sì":"No"))+" · scegli il nuovo blocco" : "Scegli il blocco successivo";
                TextRenderer.DrawText(e.Graphics, detail, _detailFont,
                    new Rectangle(Scaled(12, _scale), Scaled(33, _scale), Width - Scaled(24, _scale), Scaled(20, _scale)), Ui.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { _titleFont.Dispose(); _detailFont.Dispose(); }
                base.Dispose(disposing);
            }
        }

        private sealed class PickerFooter : ToolStripMenuItem
        {
            private readonly Font _labelFont = Ui.Font(9f);
            private readonly Font _keyFont = Ui.Font(8f);
            private float _scale = 1f;
            internal PickerFooter()
            {
                Text = "Annulla";
                AccessibleName = "Annulla il collegamento, Esc";
                AutoSize = false;
                Size = new Size(304, 44);
            }
            internal void SetScale(float scale)
            {
                _scale = scale;
                Size = new Size(Scaled(304, scale), Scaled(44, scale));
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Ui.Surface);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF button = new RectangleF(_scale, 7 * _scale, Width - 3 * _scale, Height - 9 * _scale);
                using (GraphicsPath shape = Ui.Rounded(button, 9 * _scale))
                using (Brush fill = new SolidBrush(Selected ? Ui.Hover : Ui.Raised)) e.Graphics.FillPath(fill, shape);
                Color foreground = Selected ? Ui.Ink : Ui.Muted;
                using (Pen cross = new Pen(foreground, 1.25f * _scale))
                {
                    cross.StartCap = cross.EndCap = LineCap.Round;
                    float cx = 22 * _scale, cy = button.Top + button.Height / 2;
                    e.Graphics.DrawLine(cross, cx - 3 * _scale, cy - 3 * _scale, cx + 3 * _scale, cy + 3 * _scale);
                    e.Graphics.DrawLine(cross, cx + 3 * _scale, cy - 3 * _scale, cx - 3 * _scale, cy + 3 * _scale);
                }
                TextRenderer.DrawText(e.Graphics, Text, _labelFont,
                    new Rectangle(Scaled(38, _scale), Scaled(7, _scale), Width - Scaled(104, _scale), Scaled(35, _scale)), foreground,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                RectangleF key = new RectangleF(Width - 47 * _scale, button.Top + (button.Height - 20 * _scale) / 2, 34 * _scale, 20 * _scale);
                using (GraphicsPath badge = Ui.Rounded(key, 5 * _scale))
                using (Brush fill = new SolidBrush(Ui.Surface))
                using (Pen border = new Pen(Ui.Line, _scale))
                {
                    e.Graphics.FillPath(fill, badge);
                    e.Graphics.DrawPath(border, badge);
                }
                TextRenderer.DrawText(e.Graphics, "Esc", _keyFont, Rectangle.Round(key), Ui.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { _labelFont.Dispose(); _keyFont.Dispose(); }
                base.Dispose(disposing);
            }
        }

        private sealed class PickerRow : ToolStripMenuItem
        {
            private readonly NodeKind _kind;
            private readonly Font _titleFont = Ui.Font(9.8f, FontStyle.Bold);
            private readonly Font _detailFont = Ui.Font(8.3f);
            private float _scale = 1f;
            internal PickerRow(NodeKind kind)
            {
                _kind = kind;
                Tag = kind;
                Text = kind==NodeKind.Input&&!IoInstruction.Separate?"Input / Output":Ui.KindName(kind);
                AccessibleName = "Crea e collega: " + Text;
                AutoSize = false;
                Size = new Size(304, 50);
            }
            internal void SetScale(float scale)
            {
                _scale = scale;
                Size = new Size(Scaled(304, scale), Scaled(50, scale));
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Ui.Surface);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (Selected)
                    using (GraphicsPath path = Ui.Rounded(new RectangleF(_scale, _scale, Width - 3 * _scale, Height - 3 * _scale), 9 * _scale))
                    using (Brush fill = new SolidBrush(Ui.Hover)) e.Graphics.FillPath(fill, path);
                GraphicsState state = e.Graphics.Save();
                e.Graphics.ScaleTransform(_scale, _scale);
                Ui.Shape(e.Graphics, _kind, new RectangleF(13, 15, 29, 22), Ui.KindColor(_kind));
                e.Graphics.Restore(state);
                TextRenderer.DrawText(e.Graphics, Text, _titleFont,
                    new Rectangle(Scaled(56, _scale), Scaled(6, _scale), Width - Scaled(91, _scale), Scaled(22, _scale)), Ui.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                string detail = FlowTurtle.IsTurtle(_kind)?FlowTurtle.Description(_kind):FileBlocks.IsFile(_kind)?FileBlocks.Description(_kind):_kind == NodeKind.Input ? "Un parallelogramma: leggi o scrivi" : _kind == NodeKind.Process ? "Esegui un'operazione" :
                    _kind == NodeKind.Pause ? "Ferma qui la simulazione" : _kind == NodeKind.Declare ? "Scegli variabili e tipi" : _kind == NodeKind.Call ? "Richiama una funzione" : _kind == NodeKind.Decision ? "Scegli fra Sì e No" : _kind == NodeKind.Output ? "Mostra un risultato" : "Termina il percorso";
                TextRenderer.DrawText(e.Graphics, detail, _detailFont,
                    new Rectangle(Scaled(56, _scale), Scaled(27, _scale), Width - Scaled(91, _scale), Scaled(18, _scale)), Ui.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                if (Selected)
                    using (Pen pen = new Pen(Ui.Blue, 1.6f * _scale))
                    {
                        float cx = Width - 21 * _scale, cy = Height / 2f;
                        e.Graphics.DrawLine(pen, cx - 4 * _scale, cy, cx + 4 * _scale, cy);
                        e.Graphics.DrawLine(pen, cx, cy - 4 * _scale, cx, cy + 4 * _scale);
                    }
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { _titleFont.Dispose(); _detailFont.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }

}

namespace Frociart {
 internal static class IoBlockChoice {
  internal const string Explanation="Il parallelogramma è lo stesso. L'istruzione scritta indica se un dato entra o esce.";
  internal static NodeKind? ChooseShape(IWin32Window owner,string title) {
   var kind=TutorChoiceDialog.Choose(owner,title,IoInstruction.Separate?new[]{NodeKind.Start,NodeKind.Input,NodeKind.Output,NodeKind.Process,NodeKind.Declare,NodeKind.Decision,NodeKind.Call,NodeKind.Pause,NodeKind.End,NodeKind.OpenFile,NodeKind.ReadFile,NodeKind.WriteFile,NodeKind.CloseFile,NodeKind.TurtleForward,NodeKind.TurtleTurn,NodeKind.TurtleHome,NodeKind.TurtleClear,NodeKind.TurtlePen}:new[]{NodeKind.Start,NodeKind.Input,NodeKind.Process,NodeKind.Declare,NodeKind.Decision,NodeKind.Call,NodeKind.Pause,NodeKind.End,NodeKind.OpenFile,NodeKind.ReadFile,NodeKind.WriteFile,NodeKind.CloseFile,NodeKind.TurtleForward,NodeKind.TurtleTurn,NodeKind.TurtleHome,NodeKind.TurtleClear,NodeKind.TurtlePen},k=>k==NodeKind.Input?"Input / Output":Ui.KindName(k),k=>FlowTurtle.IsTurtle(k)?FlowTurtle.Description(k):FileBlocks.IsFile(k)?FileBlocks.Description(k):k==NodeKind.Input?Explanation:TutorDiagnostics.Purpose(k));
   return kind;
  }
 }
}
