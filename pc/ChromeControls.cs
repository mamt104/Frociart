using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Frociart
{
    // The sidebar uses our own small segment bar. Native TabControl draws a
    // system-colored nonclient frame even when its items are owner-drawn.
    internal sealed class SidebarTabs : ContainerControl
    {
        private readonly List<Panel> pages = new List<Panel>();
        private readonly SegmentBar segments;
        private int selectedIndex = -1;
        private float scale = 1f;
        internal event EventHandler SelectedIndexChanged;

        public SidebarTabs()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Ui.Surface;
            Margin = Padding.Empty;
            segments = new SegmentBar(this) { Height = 52 };
            Controls.Add(segments);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal Panel SelectedTab
        {
            get { return selectedIndex >= 0 && selectedIndex < pages.Count ? pages[selectedIndex] : null; }
            set { SelectedIndex = pages.IndexOf(value); }
        }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < 0 || value >= pages.Count || value == selectedIndex) return;
                SuspendLayout();
                FlowTransitions.Change(this);
                Panel previous = SelectedTab;
                bool moveFocus = previous != null && previous.ContainsFocus;
                selectedIndex = value;
                foreach (Panel page in pages) page.Visible = page == SelectedTab;
                SelectedTab.BringToFront(); segments.BringToFront();
                ResumeLayout(true);
                segments.Invalidate();
                segments.NotifySelection();
                if (moveFocus) SelectedTab.SelectNextControl(null, true, true, true, false);
                var handler = SelectedIndexChanged; if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        internal void AddPage(Panel page, string title)
        {
            page.Text = title; page.AccessibleName = title;
            page.Dock = DockStyle.None; page.Margin = Padding.Empty;
            page.Visible = false; pages.Add(page); Controls.Add(page);
            segments.BringToFront();
            if (selectedIndex < 0) SelectedIndex = 0;
            PerformLayout();
        }

        internal void SetScale(float value)
        {
            scale = Math.Max(1f, value);
            segments.Height = (int)Math.Round(52 * scale);
            PerformLayout();
            segments.Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if(segments==null)return;
            int header=(int)Math.Round(52*scale);
            segments.SetBounds(0,0,ClientSize.Width,header);
            foreach(Panel page in pages)page.SetBounds(1,header,Math.Max(0,ClientSize.Width-1),Math.Max(0,ClientSize.Height-header));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Ui.Line)) e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        }

        private sealed class SegmentBar : Control
        {
            private readonly SidebarTabs owner;
            private Font normalFont, selectedFont;
            private int hover = -1;
            private readonly MotionValue position;

            internal SegmentBar(SidebarTabs owner)
            {
                this.owner = owner; position = new MotionValue(this);
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
                TabStop = true; Cursor = Cursors.Hand;
                AccessibleName = "Pannelli del diagramma"; AccessibleRole = AccessibleRole.PageTabList;
                normalFont = Ui.Font(9); selectedFont = Ui.Font(9, FontStyle.Bold);
            }

            private int S(float value) { return (int)Math.Round(value * owner.scale); }
            private Rectangle Track
            { get { return new Rectangle(S(12), S(9), Math.Max(1, Width - S(24)), S(34)); } }
            internal Rectangle ItemBounds(int index)
            {
                Rectangle r = Track; int count = Math.Max(1, owner.pages.Count);
                int left = r.Left + S(3) + (r.Width - S(6)) * index / count;
                int right = r.Left + S(3) + (r.Width - S(6)) * (index + 1) / count;
                return new Rectangle(left, r.Top + S(3), right - left, r.Height - S(6));
            }
            private int Hit(Point point)
            {
                for (int i = 0; i < owner.pages.Count; i++) if (ItemBounds(i).Contains(point)) return i;
                return -1;
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                int next = Hit(e.Location); if (next != hover) { hover = next; Invalidate(Track); }
                base.OnMouseMove(e);
            }
            protected override void OnMouseLeave(EventArgs e)
            { hover = -1; Invalidate(Track); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                { int index = Hit(e.Location); if (index >= 0) { Focus(); owner.SelectedIndex = index; } }
                base.OnMouseDown(e);
            }
            protected override void OnGotFocus(EventArgs e) { Invalidate(Track); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(Track); base.OnLostFocus(e); }
            protected override bool IsInputKey(Keys keyData)
            {
                Keys key = keyData & Keys.KeyCode;
                return key == Keys.Left || key == Keys.Right || key == Keys.Home || key == Keys.End || base.IsInputKey(keyData);
            }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                int count = owner.pages.Count;
                if (count > 0)
                {
                    int index = owner.SelectedIndex;
                    if (e.KeyCode == Keys.Left) index = (index + count - 1) % count;
                    else if (e.KeyCode == Keys.Right) index = (index + 1) % count;
                    else if (e.KeyCode == Keys.Home) index = 0;
                    else if (e.KeyCode == Keys.End) index = count - 1;
                    else { base.OnKeyDown(e); return; }
                    owner.SelectedIndex = index; e.Handled = true; e.SuppressKeyPress = true;
                }
                base.OnKeyDown(e);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics; g.Clear(Ui.Surface); g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var track = Ui.Rounded(Track, S(10)))
                using (var brush = new SolidBrush(Ui.Paper)) g.FillPath(brush, track);
                for (int i = 0; i < owner.pages.Count; i++)
                {
                    Rectangle r = ItemBounds(i); bool selected = i == owner.SelectedIndex;
                    if (selected || hover == i)
                    {
                        using (var path = Ui.Rounded(new RectangleF(selected ? ItemBounds(0).X + position.Value * r.Width : r.X, r.Y, r.Width - 1, r.Height - 1), S(7)))
                        using (var brush = new SolidBrush(selected ? Ui.Raised : Ui.Hover))
                        {
                            g.FillPath(brush, path);
                            if (selected) using (var pen = new Pen(Focused && ShowFocusCues ? Ui.Blue : Ui.Line)) g.DrawPath(pen, path);
                        }
                    }
                    TextRenderer.DrawText(g, owner.pages[i].Text, selected ? selectedFont : normalFont, r,
                        selected ? Ui.Ink : Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
                using (var pen = new Pen(Ui.Line)) g.DrawLine(pen, 0, 0, 0, Height);
            }

            internal void NotifySelection()
            { position.Set(Math.Max(0,owner.SelectedIndex),170); if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.Selection, owner.SelectedIndex); }
            protected override AccessibleObject CreateAccessibilityInstance() { return new SegmentAccessibility(this); }
            private sealed class SegmentAccessibility : ControlAccessibleObject
            {
                private readonly SegmentBar bar;
                internal SegmentAccessibility(SegmentBar bar) : base(bar) { this.bar = bar; }
                public override AccessibleRole Role { get { return AccessibleRole.PageTabList; } }
                public override int GetChildCount() { return bar.owner.pages.Count; }
                public override AccessibleObject GetChild(int index)
                { return index < 0 || index >= bar.owner.pages.Count ? null : new TabAccessibility(bar, index, this); }
                public override AccessibleObject GetSelected() { return GetChild(bar.owner.SelectedIndex); }
                public override AccessibleObject GetFocused() { return bar.Focused ? GetSelected() : null; }
            }
            private sealed class TabAccessibility : AccessibleObject
            {
                private readonly SegmentBar bar; private readonly int index; private readonly AccessibleObject parent;
                internal TabAccessibility(SegmentBar bar, int index, AccessibleObject parent)
                { this.bar = bar; this.index = index; this.parent = parent; }
                public override string Name { get { return bar.owner.pages[index].Text; } set { } }
                public override string DefaultAction { get { return "Apri pannello"; } }
                public override AccessibleRole Role { get { return AccessibleRole.PageTab; } }
                public override AccessibleObject Parent { get { return parent; } }
                public override Rectangle Bounds { get { return bar.RectangleToScreen(bar.ItemBounds(index)); } }
                public override AccessibleStates State
                {
                    get
                    {
                        AccessibleStates state = AccessibleStates.Selectable | AccessibleStates.Focusable;
                        if (bar.owner.SelectedIndex == index) state |= AccessibleStates.Selected;
                        if (bar.Focused && bar.owner.SelectedIndex == index) state |= AccessibleStates.Focused;
                        return state;
                    }
                }
                public override void DoDefaultAction() { bar.owner.SelectedIndex = index; }
                public override void Select(AccessibleSelection flags)
                {
                    bar.owner.SelectedIndex = index;
                    if ((flags & AccessibleSelection.TakeFocus) != 0) bar.Focus();
                }
            }
            protected override void Dispose(bool disposing)
            { if (disposing) { normalFont.Dispose(); selectedFont.Dispose(); } base.Dispose(disposing); }
        }
    }

    // Paint the actual background here, not on the Paint event. Opaque child
    // labels share its fill, so transparent-control background replay cannot
    // repeat a rounded edge across the middle of the card.
    internal sealed class ContextCard : Panel
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Emphasized { get; set; }
        public ContextCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Ui.Paper : Parent.BackColor);
            if (Width < 3 || Height < 3) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Ui.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 11 * e.Graphics.DpiX / 96f))
            using (var fill = new SolidBrush(BackColor))
            using (var pen = new Pen(Emphasized ? Ui.Blue : Ui.Line))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
        }
    }

    internal sealed class ValidationIssuesView : FlowScrollPanel
    {
        private sealed class Entry
        {
            internal ValidationIssue Issue;
            internal int Top, Height, MessageHeight, SuggestionHeight;
        }
        private readonly List<Entry> entries=new List<Entry>();
        private readonly Font titleFont=Ui.Font(9.3f,FontStyle.Bold),detailFont=Ui.Font(8.8f),kindFont=Ui.Font(8,FontStyle.Bold),emptyFont=Ui.Font(10);
        private float scale=1f;
        private int cardWidth,selected=-1,hover=-1;
        private bool arranging,showSuccess;
        internal event Action<ValidationIssue> IssueActivated;

        public ValidationIssuesView()
        {
            AutoScroll=true;TabStop=true;Cursor=Cursors.Default;
            SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            AccessibleRole=AccessibleRole.List;AccessibleName="Segnalazioni della verifica";
        }
        private int S(float value){return (int)Math.Round(value*scale);}
        internal int IssueCount{get{return entries.Count;}}
        internal void SetIssues(IList<ValidationIssue> value,bool completed)
        {
            entries.Clear();selected=hover=-1;showSuccess=completed;
            if(value!=null)foreach(ValidationIssue issue in value)entries.Add(new Entry{Issue=issue});
            AutoScrollPosition=Point.Empty;ArrangeEntries();Invalidate();
            if(IsHandleCreated)AccessibilityNotifyClients(AccessibleEvents.Reorder,-1);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);using(var graphics=CreateGraphics())scale=graphics.DpiX/96f;
            ArrangeEntries();
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);ArrangeEntries();}
        private void ArrangeEntries()
        {
            if(arranging||entries==null||titleFont==null||Width<30)return;
            arranging=true;
            try
            {
                // Reserve scrollbar space once. Its visibility therefore never
                // triggers another measure/reflow of hundreds of entries.
                cardWidth=Math.Max(S(100),Width-S(30)-SystemInformation.VerticalScrollBarWidth);
                int textWidth=Math.Max(1,cardWidth-S(22)),top=S(6);
                foreach(Entry entry in entries)
                {
                    entry.MessageHeight=Math.Max(S(35),TextRenderer.MeasureText(entry.Issue.Message,titleFont,new Size(textWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+S(5));
                    entry.SuggestionHeight=Math.Max(S(24),TextRenderer.MeasureText(entry.Issue.Suggestion??"",detailFont,new Size(textWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+S(5));
                    entry.Top=top;entry.Height=S(43)+entry.MessageHeight+entry.SuggestionHeight;top+=entry.Height+S(10);
                }
                AutoScrollMinSize=new Size(0,entries.Count>0?top+S(15):showSuccess?S(190):0);
            }
            finally{arranging=false;}
        }
        private Rectangle BoundsFor(int index)
        {Entry entry=entries[index];return new Rectangle(S(15),entry.Top+AutoScrollPosition.Y,cardWidth,entry.Height);}
        private int FindAt(int worldY)
        {
            int low=0,high=entries.Count-1;
            while(low<=high)
            {int mid=(low+high)/2;Entry entry=entries[mid];if(worldY<entry.Top)high=mid-1;else if(worldY>=entry.Top+entry.Height)low=mid+1;else return mid;}
            return -1;
        }
        private int Hit(Point point)
        {
            if(point.X<S(15)||point.X>S(15)+cardWidth)return -1;
            return FindAt(point.Y-AutoScrollPosition.Y);
        }
        private int FirstVisible(int top)
        {
            int low=0,high=entries.Count;
            while(low<high)
            {int middle=(low+high)/2;if(entries[middle].Top+entries[middle].Height<top)low=middle+1;else high=middle;}
            return low;
        }
        private void InvalidateEntry(int index)
        {if(index>=0&&index<entries.Count)Invalidate(BoundsFor(index));}
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index=Hit(e.Location);
            if(index!=hover){int old=hover;hover=index;InvalidateEntry(old);InvalidateEntry(index);Cursor=index<0?Cursors.Default:Cursors.Hand;}
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e){int old=hover;hover=-1;InvalidateEntry(old);base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if(e.Button==MouseButtons.Left)
            {int index=Hit(e.Location);if(index>=0){Focus();SelectIssue(index,false);Activate(index);}}
            base.OnMouseDown(e);
        }
        private void SelectIssue(int index,bool reveal)
        {
            if(index<0||index>=entries.Count)return;
            int old=selected;selected=index;InvalidateEntry(old);InvalidateEntry(index);
            if(reveal)
            {
                Rectangle bounds=BoundsFor(index);int position=-AutoScrollPosition.Y;
                if(bounds.Top<0)AutoScrollPosition=new Point(0,entries[index].Top);
                else if(bounds.Bottom>ClientSize.Height)AutoScrollPosition=new Point(0,position+bounds.Bottom-ClientSize.Height+S(8));
            }
            if(IsHandleCreated)AccessibilityNotifyClients(AccessibleEvents.Selection,index);
        }
        private void Activate(int index)
        {if(index>=0&&index<entries.Count){var handler=IssueActivated;if(handler!=null)handler(entries[index].Issue);}}
        protected override bool IsInputKey(Keys keyData)
        {Keys key=keyData&Keys.KeyCode;return key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(entries.Count==0){base.OnKeyDown(e);return;}
            int next=selected;
            if(e.KeyCode==Keys.Down)next=Math.Min(entries.Count-1,selected+1);
            else if(e.KeyCode==Keys.Up)next=Math.Max(0,selected-1);
            else if(e.KeyCode==Keys.Home)next=0;
            else if(e.KeyCode==Keys.End)next=entries.Count-1;
            else if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){Activate(selected);e.Handled=e.SuppressKeyPress=true;return;}
            else{base.OnKeyDown(e);return;}
            SelectIssue(next,true);e.Handled=e.SuppressKeyPress=true;base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);if(selected<0&&entries.Count>0)SelectIssue(0,false);InvalidateEntry(selected);}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);InvalidateEntry(selected);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Ui.Surface);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            if(entries.Count==0&&showSuccess)
                TextRenderer.DrawText(e.Graphics,"✓  Il percorso è collegato.\n✓  Le espressioni sono leggibili.\n✓  Le variabili risultano definite.\n\nIl risultato dell'algoritmo va comunque provato con dati diversi.",emptyFont,new Rectangle(S(18),S(13)+AutoScrollPosition.Y,Math.Max(S(100),cardWidth),S(170)),Ui.Green,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            int viewportTop=-AutoScrollPosition.Y+e.ClipRectangle.Top,viewportBottom=-AutoScrollPosition.Y+e.ClipRectangle.Bottom;
            // Layout is cached; painting visits only rows that reach the clip.
            for(int index=FirstVisible(viewportTop);index<entries.Count;index++)
            {
                Entry entry=entries[index];
                if(entry.Top>viewportBottom)break;
                Rectangle bounds=BoundsFor(index);
                using(var path=Ui.Rounded(new RectangleF(bounds.X,bounds.Y,bounds.Width-1,bounds.Height-1),S(10)))
                using(var brush=new SolidBrush(index==selected||index==hover?Ui.Hover:Ui.Raised))
                using(var pen=new Pen(index==selected&&Focused?Ui.Blue:Ui.Line))
                {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
                Color color=entry.Issue.Severity==Severity.Error?Ui.Red:entry.Issue.Severity==Severity.Warning?Ui.Amber:Ui.Blue;
                string kind=entry.Issue.Severity==Severity.Error?"ERRORE":entry.Issue.Severity==Severity.Warning?"AVVISO":"NOTA";
                TextRenderer.DrawText(e.Graphics,kind,kindFont,new Rectangle(bounds.X+S(11),bounds.Y+S(10),cardWidth-S(22),S(21)),color,TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics,entry.Issue.Message,titleFont,new Rectangle(bounds.X+S(11),bounds.Y+S(34),cardWidth-S(22),entry.MessageHeight),Ui.Ink,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics,entry.Issue.Suggestion??"",detailFont,new Rectangle(bounds.X+S(11),bounds.Y+S(35)+entry.MessageHeight,cardWidth-S(22),entry.SuggestionHeight),Ui.Muted,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            }
            base.OnPaint(e);
        }
        protected override AccessibleObject CreateAccessibilityInstance(){return new IssuesAccessibility(this);}
        private sealed class IssuesAccessibility : ControlAccessibleObject
        {
            private readonly ValidationIssuesView owner;
            internal IssuesAccessibility(ValidationIssuesView owner):base(owner){this.owner=owner;}
            public override AccessibleRole Role{get{return AccessibleRole.List;}}
            public override int GetChildCount(){return owner.entries.Count;}
            public override AccessibleObject GetChild(int index){return index<0||index>=owner.entries.Count?null:new IssueAccessibility(owner,index,this);}
            public override AccessibleObject GetSelected(){return GetChild(owner.selected);}
        }
        private sealed class IssueAccessibility : AccessibleObject
        {
            private readonly ValidationIssuesView owner;private readonly int index;private readonly AccessibleObject parent;
            internal IssueAccessibility(ValidationIssuesView owner,int index,AccessibleObject parent){this.owner=owner;this.index=index;this.parent=parent;}
            private bool IsValid{get{return !owner.IsDisposed&&index>=0&&index<owner.entries.Count;}}
            public override string Name{get{return IsValid?owner.entries[index].Issue.Message+" "+owner.entries[index].Issue.Suggestion:"";}set{}}
            public override AccessibleRole Role{get{return AccessibleRole.ListItem;}}
            public override AccessibleObject Parent{get{return parent;}}
            public override string DefaultAction{get{return "Vai al blocco";}}
            public override Rectangle Bounds{get{return IsValid?owner.RectangleToScreen(owner.BoundsFor(index)):Rectangle.Empty;}}
            public override AccessibleStates State{get{return !IsValid?AccessibleStates.Unavailable:AccessibleStates.Selectable|AccessibleStates.Focusable|(owner.selected==index?AccessibleStates.Selected:0)|(!owner.ClientRectangle.IntersectsWith(owner.BoundsFor(index))?AccessibleStates.Offscreen:0);}}
            public override void DoDefaultAction(){owner.Activate(index);}
            public override void Select(AccessibleSelection flags){owner.SelectIssue(index,true);if((flags&AccessibleSelection.TakeFocus)!=0)owner.Focus();}
        }
        protected override void Dispose(bool disposing)
        {if(disposing){titleFont.Dispose();detailFont.Dispose();kindFont.Dispose();emptyFont.Dispose();}base.Dispose(disposing);}
    }

    internal sealed class FlowMessageDialog : Form
    {
        private readonly Label heading, message;
        private readonly FlowScrollPanel body;
        private readonly FlowLayoutPanel actions;
        private readonly DialogResult closeResult;
        private float scale=1f;

        private FlowMessageDialog(string text,string title,MessageBoxButtons buttons,MessageBoxIcon icon,string yesLabel=null,string noLabel=null)
        {
            SuspendLayout();
            AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
            Font=Ui.Font();Text=title+" — Frociart";StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MinimizeBox=false;MaximizeBox=false;
            ShowInTaskbar=false;ClientSize=new Size(548,300);BackColor=Ui.Surface;ForeColor=Ui.Ink;
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            heading=Ui.Label(title,16,icon==MessageBoxIcon.Warning||icon==MessageBoxIcon.Error?Ui.Amber:Ui.Ink,true);
            heading.SetBounds(24,23,500,45);Controls.Add(heading);
            body=new FlowScrollPanel{AutoScroll=true,BackColor=Ui.Surface};body.SetBounds(24,77,500,145);Controls.Add(body);
            message=Ui.Label(text,10,Ui.Ink);body.Controls.Add(message);
            actions=new FlowLayoutPanel{FlowDirection=FlowDirection.LeftToRight,WrapContents=false,AutoSize=true,Margin=Padding.Empty};Controls.Add(actions);
            bool question=buttons==MessageBoxButtons.YesNo||buttons==MessageBoxButtons.YesNoCancel;
            bool cancel=buttons==MessageBoxButtons.YesNoCancel||buttons==MessageBoxButtons.OKCancel;
            closeResult=question&&!cancel?DialogResult.No:cancel?DialogResult.Cancel:DialogResult.OK;
            if(cancel)AddAction("Annulla",DialogResult.Cancel,false);
            if(question)
            {
                AddAction(noLabel??(title=="Modifiche da salvare"?"Non salvare":title=="Recupera il lavoro"?"Non ora":"No"),DialogResult.No,false);
                AddAction(yesLabel??(title=="Modifiche da salvare"?"Salva":title=="Recupera il lavoro"?"Recupera":"Sì"),DialogResult.Yes,true);
            }
            else AddAction("OK",DialogResult.OK,true);
            if(yesLabel!=null)AcceptButton=CancelButton;
            Ui.ApplyTheme(this);Ui.ThemeChanged+=ThemeChanged;ResumeLayout(true);
        }

        private void AddAction(string text,DialogResult result,bool primary)
        {
            var button=Ui.Button(text,Math.Max(100,Math.Min(168,TextRenderer.MeasureText(text,Font).Width+30)),()=>{DialogResult=result;Close();},primary);
            button.DialogResult=result;button.Margin=new Padding(8,0,0,0);actions.Controls.Add(button);
            if(primary)AcceptButton=button;
            if(result==closeResult)CancelButton=button;
        }
        private void ThemeChanged(object sender,EventArgs e)
        {if(!IsDisposed)Invalidate(true);}
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);using(var graphics=CreateGraphics())scale=graphics.DpiX/96f;
            int width=(int)Math.Round(500*scale);
            int textHeight=TextRenderer.MeasureText(message.Text,message.Font,new Size(width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+(int)Math.Round(8*scale);
            int bodyHeight=Math.Max((int)Math.Round(70*scale),Math.Min((int)Math.Round(350*scale),textHeight));
            int textWidth=textHeight>bodyHeight?width-(int)(20*scale):width;
            if(textWidth<width)textHeight=TextRenderer.MeasureText(message.Text,message.Font,new Size(textWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+(int)Math.Round(8*scale);
            ClientSize=new Size((int)Math.Round(548*scale),bodyHeight+(int)Math.Round(147*scale));
            heading.SetBounds((int)(24*scale),(int)(23*scale),width,(int)(45*scale));
            body.SetBounds((int)(24*scale),(int)(77*scale),width,bodyHeight);
            message.SetBounds(0,0,textWidth,textHeight);
            body.AutoScrollMinSize=new Size(0,textHeight);
            actions.Location=new Point(ClientSize.Width-actions.PreferredSize.Width-(int)(24*scale),ClientSize.Height-(int)(48*scale));
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {if(DialogResult==DialogResult.None)DialogResult=closeResult;base.OnFormClosing(e);}
        protected override void Dispose(bool disposing)
        {if(disposing)Ui.ThemeChanged-=ThemeChanged;base.Dispose(disposing);}

        internal static DialogResult Show(IWin32Window owner,string text,string title,MessageBoxButtons buttons,MessageBoxIcon icon)
        {using(var dialog=new FlowMessageDialog(text,title,buttons,icon))return dialog.ShowDialog(owner);}
        internal static DialogResult Choose(IWin32Window owner,string text,string title,string yes,string no,bool cancel)
        {using(var dialog=new FlowMessageDialog(text,title,cancel?MessageBoxButtons.YesNoCancel:MessageBoxButtons.YesNo,MessageBoxIcon.Question,yes,no))return dialog.ShowDialog(owner);}
    }
}
