using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed class ProjectSearchDialog : Form
    {
        internal sealed class State { internal string Query; internal bool MatchCase, WholeWord, CurrentOnly; internal int Start, Length; }
        private readonly FlowSearch index;
        private readonly string function;
        private readonly TextBox query = Ui.TextBox();
        private readonly CheckBox matchCase = new FlowCheckBox(), wholeWord = new FlowCheckBox(), currentOnly = new FlowCheckBox();
        private readonly ListBox results = new ListBox();
        private readonly Label status = Ui.Label("Scrivi il testo da cercare nel progetto.", 9, Ui.Muted);
        private readonly System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer { Interval = 160 };
        private CancellationTokenSource pending;
        private int generation;
        private readonly FlowButton open;
        internal FlowSearch.Hit Selected { get; private set; }
        internal State SavedState { get; private set; }
        internal ProjectSearchDialog(FlowSearch index, string function, string graphName, State initial = null)
        {
            SuspendLayout();
            this.index = index; this.function = function;
            Name = "ProjectSearchDialog"; Text = "Cerca nel progetto — Frociart"; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; Size = new Size(760, 580); MinimumSize = new Size(560, 400); Font = Ui.Font();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 28, 44, 40, 30 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var label = Ui.Label("Testo, variabili, note, rami e definizioni delle funzioni", 10, Ui.Ink, true); label.Dock = DockStyle.Fill; layout.Controls.Add(label, 0, 0);
            query.Name = "ProjectSearchQuery"; query.AccessibleName = "Cerca nel progetto"; query.MaxLength = FlowSearch.MaxQuery; query.Dock = DockStyle.Fill; query.Margin = new Padding(0, 2, 0, 8); layout.Controls.Add(query, 0, 1);
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            layout.RowStyles[2].SizeType = SizeType.AutoSize;
            matchCase.Text = "Maiuscole/minuscole"; matchCase.Name = "SearchMatchCase";
            wholeWord.Text = "Parola intera"; wholeWord.Name = "SearchWholeWord";
            currentOnly.Text = "Solo questo diagramma"; currentOnly.Name = "SearchCurrentOnly"; currentOnly.AccessibleDescription = graphName;
            foreach (var option in new[] { matchCase, wholeWord, currentOnly }) { option.Margin = new Padding(0, 4, 16, 0); options.Controls.Add(option); option.CheckedChanged += delegate { Schedule(); }; }
            layout.Controls.Add(options, 0, 2); status.Name = "ProjectSearchStatus"; status.Dock = DockStyle.Fill; layout.Controls.Add(status, 0, 3);
            results.Name = "ProjectSearchResults"; results.AccessibleName = "Risultati della ricerca"; results.Dock = DockStyle.Fill; results.IntegralHeight = false; results.BorderStyle = BorderStyle.None;
            results.DrawMode = DrawMode.OwnerDrawFixed; results.DrawItem += DrawResult; layout.Controls.Add(results, 0, 4);
            var footer = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var close = Ui.Button("Chiudi", 100, delegate { DialogResult = DialogResult.Cancel; }); close.Dock = DockStyle.Left; footer.Controls.Add(close);
            open = Ui.Button("Vai al blocco", 150, Choose, true); open.Name = "SearchOpen"; open.Dock = DockStyle.Right; open.Enabled = false; footer.Controls.Add(open); layout.Controls.Add(footer, 0, 5);
            results.SelectedIndexChanged += delegate { var hit = results.SelectedItem as FlowSearch.Hit; open.Enabled = hit != null; open.Text = hit != null && hit.Origin.NodeId == null ? "Vai alla funzione" : "Vai al blocco"; }; results.DoubleClick += delegate { Choose(); };
            query.TextChanged += delegate { Schedule(); }; debounce.Tick += async delegate { debounce.Stop(); await Search(); };
            Controls.Add(layout); AcceptButton = open; CancelButton = close; Ui.ApplyTheme(this);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(true);
            Shown += delegate {
                float scale = NativeTheme.DpiScale(this);
                foreach (var option in new[] { matchCase, wholeWord, currentOnly })
                    option.Size = new Size(TextRenderer.MeasureText(option.Text, option.Font).Width + (int)Math.Ceiling(32 * scale), Math.Max(option.Font.Height + (int)(8 * scale), (int)(28 * scale)));
                results.ItemHeight = Math.Max(72, (int)Math.Round(80 * scale)); query.Focus();
                if (initial != null) query.Select(Math.Min(initial.Start, query.TextLength), Math.Min(initial.Length, query.TextLength - Math.Min(initial.Start, query.TextLength)));
            };
            FormClosing += delegate { SavedState = new State { Query = query.Text, MatchCase = matchCase.Checked, WholeWord = wholeWord.Checked, CurrentOnly = currentOnly.Checked, Start = query.SelectionStart, Length = query.SelectionLength }; };
            FormClosed += delegate { generation++; debounce.Stop(); debounce.Dispose(); if (pending != null) { pending.Cancel(); pending.Dispose(); pending = null; } };
            if (initial != null) { matchCase.Checked = initial.MatchCase; wholeWord.Checked = initial.WholeWord; currentOnly.Checked = initial.CurrentOnly; query.Text = initial.Query ?? ""; }
        }
        private void Schedule()
        {
            generation++; if (pending != null) { pending.Cancel(); pending.Dispose(); pending = null; }
            debounce.Stop(); results.Items.Clear(); open.Enabled = false;
            status.Text = String.IsNullOrWhiteSpace(query.Text) ? "Scrivi il testo da cercare nel progetto." : "Ricerca…";
            if (!String.IsNullOrWhiteSpace(query.Text)) debounce.Start();
        }
        private async Task Search()
        {
            int request = generation; string text = query.Text; bool exact = matchCase.Checked, word = wholeWord.Checked, here = currentOnly.Checked;
            pending = new CancellationTokenSource(); var token = pending.Token;
            try
            {
                var found = await Task.Run(() => index.Find(text, exact, word, here, function, token), token);
                if (IsDisposed || Disposing || request != generation || token.IsCancellationRequested) return;
                results.BeginUpdate(); try { results.Items.AddRange(found.Hits.Cast<object>().ToArray()); if (results.Items.Count > 0) results.SelectedIndex = 0; } finally { results.EndUpdate(); }
                status.Text = found.Summary;
            }
            catch (OperationCanceledException) { }
        }
        private void Choose()
        { var hit = results.SelectedItem as FlowSearch.Hit; if (hit == null) return; Selected = hit; DialogResult = DialogResult.OK; }
        private void DrawResult(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= results.Items.Count) return;
            var hit = (FlowSearch.Hit)results.Items[e.Index]; bool selected = (e.State & DrawItemState.Selected) != 0; float scale = e.Graphics.DpiX / 96f;
            using (var brush = new SolidBrush(selected ? Ui.Tint : Ui.InputBackground)) e.Graphics.FillRectangle(brush, e.Bounds);
            int x = e.Bounds.Left + (int)(12 * scale), y = e.Bounds.Top + (int)(8 * scale), width = e.Bounds.Width - (int)(24 * scale);
            using (var font = Ui.Font(9, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, hit.Context, font, new Rectangle(x, y, width, (int)(22 * scale)), selected ? Ui.Blue : Ui.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var font = Ui.Font(10)) TextRenderer.DrawText(e.Graphics, hit.Snippet, font, new Rectangle(x, y + (int)(25 * scale), width, e.Bounds.Height - (int)(38 * scale)), Ui.Ink, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) != 0) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -2, -2), Ui.Blue, selected ? Ui.Tint : Ui.InputBackground);
        }
    }
    internal sealed partial class MainForm
    {
        private Diagram searchedProject;
        private ProjectSearchDialog.State searchState;
        private void ShowProjectSearch()
        {
            if (Canvas.IsPointerGestureActive) return;
            EnterEditor(); CommitInspector(); PauseRun(); Canvas.CancelConnection();
            if (!Object.ReferenceEquals(searchedProject, Project)) searchState = null;
            searchedProject = Project;
            using (var dialog = new ProjectSearchDialog(FlowSearch.Capture(Project), ActiveFunctionId, ActiveFunctionId == null ? "Principale" : FunctionEditing.Find(Project, ActiveFunctionId).Name, searchState))
            {
                var result = dialog.ShowDialog(this); searchState = dialog.SavedState;
                if (result == DialogResult.OK) LocateSearchHit(dialog.Selected);
            }
        }
        internal bool LocateSearchHit(FlowSearch.Hit hit)
        {
            if (hit == null || Canvas.IsPointerGestureActive) return false;
            var origin = hit.Origin; var function = origin.FunctionId == null ? null : FunctionEditing.Find(Project, origin.FunctionId);
            if (origin.FunctionId != null && function == null) { SetStatus("La funzione non esiste più. Riapri Cerca nel progetto."); return false; }
            var graph = function == null ? Project : function.Body;
            if (origin.NodeId != null && !graph.Nodes.Any(n => n.Id == origin.NodeId)) { SetStatus("Il blocco non esiste più. Riapri Cerca nel progetto."); return false; }
            if(!NavigateToOrigin(origin.FunctionId,origin.NodeId,true))return false;
            Canvas.Focus(); SetStatus(hit.Context); return true;
        }
    }
    public partial class FlowCanvas
    {
        internal void CenterSearchNode(string id)
        {
            if (id != null && FindNode(id) == null) return;
            float x = _panX, y = _panY, zoom = _zoom;
            if (id == null) FitToView(); else CenterOnNode(id);
            if (!UiMotion.Enabled) return;
            arrangeFromX = x; arrangeFromY = y; arrangeFromZoom = zoom; arrangeToX = _panX; arrangeToY = _panY; arrangeToZoom = _zoom;
            _panX = x; _panY = y; _zoom = zoom; arrangeCamera = true;
            if (settleFrom == null) settleStart = manipulationClock.ElapsedMilliseconds;
            manipulationTimer.Start(); RaiseViewChanged();
        }
    }
}
