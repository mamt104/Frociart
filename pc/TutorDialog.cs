using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed partial class TutorDialog : Form
    {
        private readonly TutorStore store;
        private readonly LearningStore legacy;
        private readonly FlowLayoutPanel navigation = new FlowLayoutPanel();
        private readonly Panel page = new Panel();
        private readonly Label progress = Ui.Label("", 10, Ui.Muted);
        private readonly Label notice = Ui.Label("", 9, Ui.Muted);
        private FlowButton primary;
        private readonly Stack<TutorSession> undo = new Stack<TutorSession>(), redo = new Stack<TutorSession>();
        private readonly Timer draftTimer = new Timer { Interval = 650 };
        private TutorSession session;
        private TutorLesson lesson;
        private FlowCanvas canvas;
        private FlowScrollPanel editor;
        private TextBox expression, prompt, runtimeValue;
        private FlowButton apply, undoButton, redoButton, stepRun;
        private Label task, feedbackHeading, feedbackBody;
        private TextBox runInfo, runQuestion, runInput, runOutput;
        private TableLayoutPanel runLayout;
        private ListView runVariables;
        private Panel runInputPanel;
        private TabControl inspectorTabs;
        private Interpreter runner;
        private string stepExplanation;
        private TutorFeedback feedback;
        private string editingId;
        private bool loading, working, checking, saved = true;
        private bool finalRequested,verifiedRunPending,fastRun;private long verifiedRevision;private FlowButton skipRun,continueRun,expandTutor;private TableLayoutPanel workspaceSplit;private float tutorInspectorWidth=378;private bool tutorExpanded;private Timer fastRunTimer;
        private long revision;
        internal string PracticeExample { get; private set; }
        internal TutorSession Current { get { return session; } }
        internal FlowCanvas Canvas { get { return canvas; } }
        internal TutorFeedback Feedback { get { return feedback; } }

        internal TutorDialog(TutorStore state, LearningStore previous)
        {
            store = state; legacy = previous;
            BuildDesktopShell();
        }
        private void UpdateProgress() { progress.Text = store.CompletedCount() + " / 24 lezioni completate"; }
        private void ThemeChanged(object sender, EventArgs e) {
            Ui.ApplyTheme(this); navigation.Invalidate();
            if (working) { var currentRunner = runner; string explanation = stepExplanation; int tab = inspectorTabs.SelectedIndex;
                RenderWorkspace(); runner = currentRunner; stepExplanation = explanation; inspectorTabs.SelectedIndex = tab; RefreshRun(); }
        }
        private void ReleasePage()
        {
            if(expandTutor!=null&&expandTutor.Image!=null){expandTutor.Image.Dispose();expandTutor.Image=null;}editingSelectionBeforeTrial=null;if(finishTrial!=null)finishTrial.Visible=false;
            FlowTransitions.Change(page);
            draftTimer.Stop(); StashDraft();
            foreach (Control child in page.Controls.Cast<Control>().ToArray()) { page.Controls.Remove(child); child.Dispose(); }
            canvas = null; expression = prompt = runtimeValue = null; editor = null; task = null; editingId = null;
            inspectorTabs = null; runner = null; stepExplanation = null;
        }
        private FlowLayoutPanel Reader()
        {
            ReleasePage(); working = false; UpdateTrialFocus();
            var scroll = new FlowScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.Surface };
            var content = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly,
                Padding = new Padding(26, 20, 26, 26), MinimumSize = new Size(300, 0) };
            scroll.Controls.Add(content); page.Controls.Add(scroll);
            Action resize = delegate {
                int width = Math.Min(1120, Math.Max(280, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6)); content.Left = Math.Max(0, (scroll.ClientSize.Width - width) / 2);
                content.MinimumSize = new Size(width, 0); content.MaximumSize = new Size(width, Int32.MaxValue); content.Width = width; foreach (Control child in content.Controls) { child.MaximumSize = new Size(width - 58, Int32.MaxValue); child.Width = width - 58; if (!(child is Label) && child.MinimumSize.Height > 0) child.Height = child.MinimumSize.Height; }
                scroll.AutoScrollMinSize = new Size(0, content.PreferredSize.Height);
            };
            scroll.Resize += delegate { resize(); }; content.ControlAdded += delegate { resize(); }; return content;
        }
        private static void Paragraph(FlowLayoutPanel parent, string text, bool heading = false)
        {
            if (String.IsNullOrWhiteSpace(text)) return;
            var label = Ui.Label(text, heading ? 19 : 11, heading ? Ui.Ink : Ui.Muted, heading);
            label.AutoSize = true; label.Margin = new Padding(0, heading ? 14 : 4, 0, 13); parent.Controls.Add(label);
        }
        private static void ReaderButton(FlowLayoutPanel parent, string text, Action action)
        { var button = Ui.Button(text, 300, action); button.Height = 40; button.Margin = new Padding(0, 4, 0, 9); parent.Controls.Add(button); }
        private bool LeaveCurrent()
        {
            draftTimer.Stop(); StashDraft();
            if (session == null || SaveSession()) return true;
            notice.Text = store.Error; return false;
        }
        internal void ShowOverview() { RenderOverview(); }
        internal void SelectLesson(string id) { if (!checking) OpenLesson(id); }
        internal void OpenLesson(string id)
        {
            if (!LeaveCurrent()) return;
            var item = Tutor.Lesson(id); if (item == null) return;
            TutorSession loaded;
            try { loaded = store.Load(id, item.Seed()); }
            catch (Exception ex) { notice.Text = ex.Message; return; }
            ReleasePage(); session = loaded;verifiedRunPending=fastRun=false; lesson = item; feedback = null; undo.Clear(); redo.Clear(); saved = true;
            if (!session.Completed) session.Step = Tutor.RecoverStep(lesson, session.Step, session.Document);
            RenderLessonIntroduction();
            primary.Text = session.Step == 0 ? "Costruisci il tuo flowchart" : "Riprendi il flowchart";
            primary.Enabled = true; notice.Text = store.Error ?? "Leggi l’idea e provala sul foglio."; Ui.ApplyTheme(this);
        }
        internal void OpenWorkshop()
        {
            if (!LeaveCurrent()) return;
            TutorSession loaded;
            try { loaded = store.Load("workshop", DiagramIO.CreateBlank()); }
            catch (Exception ex) { notice.Text = ex.Message; return; }
            ReleasePage(); session = loaded;verifiedRunPending=fastRun=false; lesson = null; feedback = null; undo.Clear(); redo.Clear(); saved = true; RenderWorkspace();
            notice.Text = store.Error ?? "Inserisci un blocco pronto, leggi la guida e prova il percorso.";
        }
        private void RenderWorkspace()
        {
            desktopHeading.Text = lesson == null ? "FROCIART / Blocchi guidati" : "FROCIART / Il tuo flowchart";
            string selected = canvas == null ? null : canvas.SelectedNodeId;
            ReleasePage(); working = true;
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty, Padding = new Padding(14, 0, 14, 12) };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 378));
            split.RowStyles.Add(new RowStyle(SizeType.AutoSize)); split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            task = Ui.Label("", 12, Ui.Ink, true); task.AutoSize = true; task.Padding = new Padding(8, 12, 8, 12); task.MaximumSize = new Size(1200, 0); heading.Controls.Add(task);
            resultBand = new TutorResultBand { Margin = new Padding(0, 0, 0, 12), Visible = false }; heading.Controls.Add(resultBand);
            workspaceHeading = heading; split.Controls.Add(heading, 0, 0); split.SetColumnSpan(heading, 2);
            workspaceSplit=split;tutorExpanded=false;
            var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 14, 0) };
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            split.Resize += delegate { int side = Math.Max(340, Math.Min(420, split.ClientSize.Width / 3));tutorInspectorWidth=side;split.ColumnStyles[1].Width = tutorExpanded?0:side;
                task.MaximumSize = new Size(Math.Max(300, split.ClientSize.Width - 32), 0); resultBand.Width = task.MaximumSize.Width;
                if (editor != null && !editor.IsDisposed && !loading&&!editor.ContainsFocus&&Math.Abs(editor.ClientSize.Width-lastEditorWidth)>8) { StashDraft(); ShowEditor(); } };
            canvas = new FlowCanvas { Dock = DockStyle.Fill, SnapToGrid = true, Diagram = session.Document };
            canvas.DiagramChanging += delegate { StashDraft(); Before(null); };
            canvas.DiagramChanged += delegate { AdoptRequiredNode(); Changed(); };
            canvas.PrepareClipboardSnapshot=delegate(Diagram snapshot){StashDraft();foreach(var draft in session.Drafts){var node=snapshot.Nodes.FirstOrDefault(n=>n.Id==draft.Node);if(node!=null){IoInstruction.Apply(node,draft.Text);node.Prompt=draft.Prompt;}}};
            canvas.SelectionChanged += delegate { if (!loading) { StashDraft(); FlowTransitions.Change(editor); ShowEditor(); } };
            canvas.ReadOnlyNodeTapped += id=>{if(inspectorTabs!=null)inspectorTabs.SelectedIndex=0;canvas.SelectNode(id);ShowEditor();if(expression!=null)expression.Focus();};
            canvas.EditRequested += delegate { ShowEditor(); if (expression != null) { expression.Focus(); expression.SelectAll(); } };
            canvas.StatusChanged += delegate(object sender, StatusEventArgs e) { notice.Text = e.Message; };
            center.Controls.Add(canvas, 0, 1);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(8, 5, 4, 2), BackColor = Ui.Surface };
            tools.Controls.Add(Ui.Button("Aggiungi", 88, AddBlock)); tools.Controls.Add(Ui.Button("Collega", 80, Connect));
            tools.Controls.Add(Ui.Button("Riordina", 90, delegate { canvas.ArrangeAnimated(()=>{Before(null); Tutor.Arrange(session.Document); Changed();}); },true));
            var fit=Ui.Button("Tutto",66,delegate{canvas.FitToView();});fit.Accent=Ui.Blue;fit.AccessibleName="Mostra tutto il flowchart";tools.Controls.Add(fit);
            undoButton = Ui.Button("", 42, Undo);undoButton.Glyph=ButtonGlyph.Undo;undoButton.AccessibleName="Annulla";redoButton = Ui.Button("",42,Redo);redoButton.Glyph=ButtonGlyph.Redo;redoButton.AccessibleName="Ripeti";tools.Controls.Add(undoButton);tools.Controls.Add(redoButton);tools.Controls.SetChildIndex(redoButton,0);tools.Controls.SetChildIndex(undoButton,0);
            expandTutor=Ui.Button("",42,delegate{SetTutorExpanded(!tutorExpanded);});expandTutor.AccessibleName="Espandi il flowchart";SetExpandImage(false);tools.Controls.Add(expandTutor);
            trialTools = tools; center.Controls.Add(tools, 0, 0); split.Controls.Add(center, 0, 1);
            inspectorTabs = new TutorTabs { Dock = DockStyle.Fill, Font = Ui.Font(11), Margin = Padding.Empty };
            var block = new TabPage("Scrivi"); var run = new TabPage("Prova"); inspectorTabs.TabPages.Add(block); inspectorTabs.TabPages.Add(run);
            editor = new FlowScrollPanel { Dock = DockStyle.Fill, AutoScroll = true }; block.Controls.Add(editor);
            BuildRunner(run); inspectorTabs.SelectedIndexChanged += delegate { UpdateTrialFocus(); }; split.Controls.Add(inspectorTabs, 1, 1); page.Controls.Add(split);
            if (selected != null && Tutor.Find(session.Document, selected) != null) canvas.SelectNode(selected);
            else if (lesson != null && session.Step < lesson.steps.Length) canvas.SelectNode(lesson.steps[session.Step].node);
            ShowEditor(); UpdateTask(); Ui.ApplyTheme(this);
            if (feedback == null) notice.Text = store.Error ?? (lesson == null
                ? "Costruisci il percorso, poi controllalo e provalo."
                : session.Step < lesson.steps.Length
                    ? "Passo " + (session.Step + 1) + " da verificare. Segui la consegna, poi premi Controlla."
                    : "Prova finale da verificare. Premi Prova il mio flowchart.");
            canvas.BeginInvoke(new Action(delegate { if (canvas != null && !canvas.IsDisposed) canvas.FitToView(); }));
        }
        private void AdoptRequiredNode()
        {
            // Canvas +/drag creation remains real: associate the newly created casella with this ADD task.
            if (lesson == null || session.Step >= lesson.steps.Length) return;
            var step = lesson.steps[session.Step]; if (step.action != Tutor.Add || Tutor.Find(session.Document, step.node) != null) return;
            var node = Tutor.Find(session.Document, canvas.SelectedNodeId);
            if (node == null || lesson.Part(node.Id) != null) return;
            string old = node.Id; node.Id = step.node;
            foreach (var edge in session.Document.Edges) { if (edge.From == old) edge.From = node.Id; if (edge.To == old) edge.To = node.Id; }
            canvas.SelectNode(node.Id);
        }
        private void Before(string removeDraft)
        {
            var snapshot = TutorStore.Copy(session); if (removeDraft != null) snapshot.Drafts.RemoveAll(d => d.Node == removeDraft);
            undo.Push(snapshot); if (undo.Count > 30) { var keep = undo.Take(30).Reverse().ToArray(); undo.Clear(); foreach (var item in keep) undo.Push(item); }
            redo.Clear();
        }
        private void Changed()
        {
            revision++;verifiedRunPending=fastRun=false;runner = null; feedback = null; session.Drafts.RemoveAll(d => Tutor.Find(session.Document, d.Node) == null);
            loading = true; canvas.Diagram = session.Document; loading = false; canvas.Invalidate(); SaveSession(); ShowEditor(); UpdateTask(); RefreshRun();
        }
        private void Undo()
        { if (undo.Count == 0) return; StashDraft(); redo.Push(TutorStore.Copy(session)); bool demonstrated=session.Demonstrated;session = undo.Pop();session.Demonstrated|=demonstrated; Changed(); }
        private void Redo()
        { if (redo.Count == 0) return; StashDraft(); undo.Push(TutorStore.Copy(session)); bool demonstrated=session.Demonstrated;session = redo.Pop();session.Demonstrated|=demonstrated; Changed(); }
        private bool SaveSession()
        {
            if (session == null) return true; StashDraft(); saved = store.Save(session);
            if (!saved) notice.Text = store.Error; else if (notice.Text.StartsWith("Salvataggio non riuscito")) notice.Text = "Esercizio salvato.";
            return saved;
        }
        private void StashDraft()
        {
            if (loading || session == null || expression == null || editingId == null) return;
            var node = Tutor.Find(session.Document, editingId); if (node == null) return;
            var draft = session.Drafts.FirstOrDefault(d => d.Node == editingId);
            if (!IoInstruction.Changed(node,TutorTextDialog.Source(expression.Text)) && (prompt == null || prompt.Text == (node.Prompt ?? ""))) { if (draft != null) session.Drafts.Remove(draft); return; }
            if (draft == null) { draft = new TutorDraft { Node = editingId }; session.Drafts.Add(draft); }
            draft.Text = TutorTextDialog.Source(expression.Text); draft.Prompt = prompt == null ? node.Prompt ?? "" : prompt.Text;
        }
        private int EditorText(string text, int y, float size = 10, bool bold = false, Color? color = null)
        {
            var label = Ui.Label(text, size, color ?? Ui.Muted, bold); int width = Math.Max(230, editor.ClientSize.Width - 34);
            int height = TextRenderer.MeasureText(text, label.Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + 9;
            label.SetBounds(14, y, width, height); editor.Controls.Add(label); return y + height + 8;
        }
        private int EditorButton(string text, int y, Action action, bool prominent = false)
        { var button = Ui.Button(text, Math.Max(230, editor.ClientSize.Width - 34), action, prominent); button.SetBounds(14, y, button.Width, 38); editor.Controls.Add(button); return y + 47; }
        private int lastEditorWidth;
        private void ShowEditor()
        {
            if (editor == null || editor.IsDisposed) return;
            if(inspectorTabs!=null&&inspectorTabs.SelectedIndex==1&&canvas!=null&&canvas.ViewOnly)return;
            lastEditorWidth=editor.ClientSize.Width;loading = true; editingId = null; expression = prompt = runtimeValue = null; apply = null; feedbackHeading = feedbackBody = null;
            foreach (Control control in editor.Controls.Cast<Control>().ToArray()) { editor.Controls.Remove(control); control.Dispose(); }
            int y = 16; var node = Tutor.Find(session.Document, canvas.SelectedNodeId);
            UpdateDesktopResult();
            if (feedback != null && !feedback.Pass) {
                y = EditorText("Cosa rivedere", y, 12, true, Ui.Ink);
                y = EditorText(FeedbackSummary(feedback), y);
                y = EditorButton("Capisci questa prova", y, delegate { TutorTextDialog.ShowText(this, "Perché questa prova fallisce", feedback.Text + "\n\n" + feedback.Trace); });
                y = EditorButton("Correggi questo passo", y, Correct);
            }
            if (lesson != null && session.Step < lesson.steps.Length && lesson.steps[session.Step].action == Tutor.Value)
            {
                y = EditorText("Il dato durante l'esecuzione", y, 13, true, Ui.Ink); y = EditorText("Qui scrivi il valore richiesto, non il nome della variabile né un calcolo.", y);
                runtimeValue = Ui.TextBox(); runtimeValue.Name = "TutorRuntimeValue"; runtimeValue.AccessibleName = "Valore Input dell'esercizio";
                runtimeValue.Text = session.RuntimeValue; runtimeValue.MaxLength = Values.MaxStringLength; runtimeValue.SetBounds(14, y, Math.Max(230, editor.ClientSize.Width - 34), 30); editor.Controls.Add(runtimeValue); y += 46;
                runtimeValue.TextChanged += delegate { session.RuntimeValue = runtimeValue.Text; DraftChanged(); };
            }
            if (node == null)
            {
                y = EditorText(lesson == null ? "Il tuo foglio guidato" : "Scegli una casella", y, 16, true, Ui.Ink);
                y = EditorText("Clicca una casella per leggere cosa fa e scriverci. Trascina con la rotella premuta per spostare il foglio. Tutto inquadra il diagramma intero.", y);
            }
            else if (node.Kind == NodeKind.Start || node.Kind == NodeKind.End) {
                y = EditorText(node.Kind == NodeKind.Start ? "Qui si comincia" : "Qui ci si ferma", y, 17, true, Ui.KindColor(node.Kind));
                y = EditorText(TutorDiagnostics.Purpose(node.Kind), y);
                if (lesson != null && session.Step < lesson.steps.Length) y = EditorText("Segui il compito in alto e usa il pulsante in basso. Questa casella non ha bisogno di un calcolo.", y);
                y = EditorButton("Spiegami questo blocco", y, delegate { ShowBlockGuide(node); });
            }
            else
            {
                editingId = node.Id; var draft = session.Drafts.FirstOrDefault(d => d.Node == node.Id);
                y = EditorText(Ui.KindName(node.Kind), y, 17, true, Ui.KindColor(node.Kind));
                y = EditorText(IoInstruction.IsIo(node.Kind)?IoInstruction.Hint:TutorDiagnostics.Purpose(node.Kind), y);
                y = EditorText(node.Kind == NodeKind.Input ? "Nome della variabile" : node.Kind == NodeKind.Process ? "Nome = valore o calcolo" : node.Kind == NodeKind.Decision ? "Domanda: vero oppure falso" : node.Kind == NodeKind.Output ? "Valore o testo da mostrare" : "Etichetta", y, 10, true, Ui.Ink);
                expression = Ui.TextBox(true); expression.Name = "TutorExpression"; expression.AccessibleName = "Contenuto della casella"; expression.Text = TutorTextDialog.Lines(draft == null ? IoInstruction.Editor(node) : draft.Text);
                expression.Font = Ui.Mono(12); expression.SetBounds(14, y, Math.Max(230, editor.ClientSize.Width - 34), 86); editor.Controls.Add(expression); y += 96;
                if (IoInstruction.IsIo(node.Kind))
                {
                    y = EditorText("Domanda da mostrare (facoltativa)", y, 10, true, Ui.Ink);
                    prompt = Ui.TextBox(true); prompt.Name = "TutorPrompt"; prompt.AccessibleName = "Domanda Input"; prompt.MaxLength = 1024; prompt.Text = draft == null ? node.Prompt : draft.Prompt;
                    prompt.SetBounds(14, y, Math.Max(230, editor.ClientSize.Width - 34), 56); editor.Controls.Add(prompt); y += 66; prompt.TextChanged += delegate { DraftChanged(); };
                }
                apply = Ui.Button("Applica al blocco", Math.Max(230, editor.ClientSize.Width - 34), delegate { Apply(); }); apply.SetBounds(14, y, apply.Width, 38); editor.Controls.Add(apply); y += 48;
                expression.TextChanged += delegate { DraftChanged(); };
                y = EditorButton("Spiegami questo blocco", y, delegate { ShowBlockGuide(node); });
                if (node.Kind == NodeKind.Input || node.Kind == NodeKind.Process) y = EditorButton("Rinomina variabile…", y, RenameVariable);
            }
            if (lesson == null) y = EditorButton("Inserisci un blocco pronto", y, AddBlock, true);
            if (!saved) y = EditorButton("Riprova il salvataggio", y, delegate { SaveSession(); ShowEditor(); });
            editor.AutoScrollMinSize = new Size(0, y + 12); loading = false; if(expression!=null)canvas.PreviewText(editingId,expression.Text);else canvas.PreviewText(null,null); Ui.ApplyTheme(editor);
        }
        private void DraftChanged()
        {
            if (loading) return; StashDraft(); if(expression!=null)canvas.PreviewText(editingId,expression.Text); saved = false; revision++;
            if (feedback != null) { if (feedbackHeading != null) { feedbackHeading.Text = "Hai modificato il contenuto"; feedbackHeading.ForeColor = Ui.Blue; } if (feedbackBody != null) feedbackBody.Text = "Il risultato precedente riguarda la versione precedente. Premi Controlla per verificare ciò che hai scritto adesso."; }
            feedback = null;verifiedRunPending=fastRun=false;runner = null; stepExplanation = null; notice.Text = "Modifica da verificare. Premi Controlla quando sei pronto."; draftTimer.Stop(); draftTimer.Start();
            if (apply != null) apply.Enabled = session.Drafts.Any(d => d.Node == editingId);
            UpdateTask(); RefreshRun();
        }
        private bool Apply()
        {
            StashDraft(); if (editingId == null) return true;
            var node = Tutor.Find(session.Document, editingId); var draft = session.Drafts.FirstOrDefault(d => d.Node == editingId);
            if (node == null || draft == null) return true;
            Before(editingId); canvas.ApplyTextEdit(node,()=>{IoInstruction.Apply(node,draft.Text); node.Prompt = draft.Prompt;}); session.Drafts.Remove(draft); Changed(); return saved;
        }
        private void UpdateTask()
        {
            UpdateDesktopResult();
            if (session == null) return;
            if (task != null) task.Text = lesson == null ? "Costruisci guidato\nInserisci un blocco, scopri le sue variabili e prova il percorso." : session.Step < lesson.steps.Length ?
                "Passo " + (session.Step + 1) + " di " + lesson.steps.Length + " · " + lesson.steps[session.Step].title + "\n" + lesson.steps[session.Step].instruction : "Prova finale\n" + lesson.goal;
            primary.Enabled = !checking;
            if (!working) return;
            if (checking) primary.Text = "Controllo in corso…";
            else if (feedback != null && feedback.Pass) primary.Text = lesson == null ? "Controlla il percorso" : feedback.Code == "complete" ? verifiedRunPending?"Continua l’esecuzione":"Lezione successiva" : "Continua";
            else if (lesson == null) primary.Text = "Controlla il percorso";
            else if (session.Step >= lesson.steps.Length) primary.Text = "Prova il mio flowchart";
            else {
                var step = lesson.steps[session.Step]; var node = Tutor.Find(session.Document, step.node);
                primary.Text = step.action == Tutor.Add && node == null ? "Aggiungi una casella" : step.action == Tutor.Write && node != null && String.IsNullOrWhiteSpace(node.Text) && !session.Drafts.Any(d => d.Node == node.Id && !String.IsNullOrWhiteSpace(d.Text)) ? "Scrivi nel blocco" : step.action == Tutor.ConnectAction && Tutor.Outgoing(session.Document, step.a, step.branch) == null ? "Collega le caselle" : "Controlla questo passo";
            }
            if (undoButton != null) undoButton.Enabled = undo.Count > 0; if (redoButton != null) redoButton.Enabled = redo.Count > 0; UpdateTrialFocus();
        }
        private void Primary()
        {
            if (checking) return;
            if (!working) { if (session == null) SelectLesson(RecommendedLesson().id); else RenderWorkspace(); return; }
            if (feedback != null && feedback.Pass && lesson != null)
            {
                if (feedback.Code == "complete"&&verifiedRunPending&&verifiedRevision==revision) { inspectorTabs.SelectedIndex=1;StepRunner(false);return; }
                if (feedback.Code == "complete") { int index = Array.IndexOf(Tutor.Lessons, lesson); if (index + 1 < Tutor.Lessons.Length) SelectLesson(Tutor.Lessons[index + 1].id); else ShowOverview(); return; }
                int before = session.Step; session.Step++;
                if (!SaveSession()) { session.Step = before; notice.Text = store.Error; return; }
                feedback = null; RenderWorkspace(); return;
            }
            if (lesson != null && session.Step < lesson.steps.Length)
            {
                var step = lesson.steps[session.Step]; var node = Tutor.Find(session.Document, step.node);
                if (step.action == Tutor.Add && node == null) { AddBlock(); return; }
                if (step.action == Tutor.Write && node != null && String.IsNullOrWhiteSpace(node.Text) && !session.Drafts.Any(d => d.Node == node.Id && !String.IsNullOrWhiteSpace(d.Text))) { canvas.SelectNode(node.Id); ShowEditor(); expression.Focus(); return; }
                if (step.action == Tutor.ConnectAction && Tutor.Outgoing(session.Document, step.a, step.branch) == null) { Connect(); return; }
            }
            CheckCurrent();
        }
        private void CheckCurrent()
        {
            if (checking || session == null || !working) { finalRequested = false; return; }
            Apply(); if (!SaveSession()) return;
            var document = DiagramIO.Clone(session.Document); var activeLesson = lesson; var activeSession = session; int phase = finalRequested && lesson != null ? lesson.steps.Length : session.Step; finalRequested = false; string value = session.RuntimeValue; long stamp = revision;
            feedback = null;
            checking = true; navigation.Enabled = false; canvas.Enabled = false; inspectorTabs.Enabled = false; UpdateTask();
            Task.Run(delegate { try { if (activeLesson != null) return Tutor.CheckStep(activeLesson, phase, document, value);
                    foreach (var node in document.Nodes) { var syntax = TutorDiagnostics.Before(node); if (syntax != null) return syntax; }
                    var issue = Validator.Validate(document).FirstOrDefault(i => i.Severity == Severity.Error);
                    return issue == null ? Tutor.Yes("Percorso eseguibile", "La struttura è valida. Apri Prova per fornire dati e osservare i risultati: solo questo controllo non dimostra che il problema sia risolto.") : TutorDiagnostics.Issue(issue); }
                catch (Exception ex) { return Tutor.No("check-failed", "Non riesco a completare la verifica", ex.Message, "Il foglio è conservato. Riprova il controllo."); }
            }).ContinueWith(delegate(Task<TutorFeedback> completed) {
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                try { BeginInvoke(new Action(delegate { FinishCheck(completed.Result, activeSession, stamp); })); }
                catch (InvalidOperationException) { }
            });
        }
        private void FinishCheck(TutorFeedback result, TutorSession activeSession, long stamp)
        {
            if (IsDisposed || Disposing) return;
            checking = false; navigation.Enabled = true;
            if (canvas != null) canvas.Enabled = true; if (inspectorTabs != null) inspectorTabs.Enabled = true;
            if (stamp != revision || session != activeSession || !working) { notice.Text = "Il foglio è cambiato: ricontrolla il nuovo contenuto."; UpdateTask(); return; }
            feedback = result;
            if (result.Pass && result.Code == "complete")
            {
                verifiedRunPending=true;verifiedRevision=revision;runner=null;fastRun=false;SetTutorExpanded(false);
            }
            if (feedback.NodeId != null && Tutor.Find(session.Document, feedback.NodeId) != null) { canvas.SelectNode(feedback.NodeId); canvas.CenterOnNode(feedback.NodeId); }
            TutorFeedbackSignals.Play(feedback.Pass, feedback.Code == "complete");
            ShowEditor(); inspectorTabs.SelectedIndex = 0; UpdateTask(); notice.Text = feedback.Pass ? "Corretto. Puoi continuare." : "Da correggere. Il tuo foglio è conservato.";
            if(result.Pass&&result.Code=="complete"){inspectorTabs.SelectedIndex=1;StepRunner(false);notice.Text="Flowchart corretto. Seguilo fino alla fine per completare la lezione.";}
            var visibleCanvas = canvas; visibleCanvas.BeginInvoke(new Action(delegate { if (!visibleCanvas.IsDisposed) visibleCanvas.FitToView(); }));
        }
        private void Correct()
        {
            if (feedback == null) return;
            string id = feedback.NodeId; feedback = null;
            if (id != null && Tutor.Find(session.Document, id) != null) canvas.SelectNode(id);
            if (id != null && lesson != null && Tutor.Find(session.Document, id) != null && lesson.Part(id) != null && (int)Tutor.Find(session.Document, id).Kind != lesson.Part(id).kind) { ChangeKind(); return; }
            if (lesson != null && session.Step < lesson.steps.Length)
            {
                var step = lesson.steps[session.Step]; var node = Tutor.Find(session.Document, step.node);
                if (step.action == Tutor.Add) { if (node == null) AddBlock(); else ChangeKind(); return; }
                if (step.action == Tutor.ConnectAction) { Connect(); return; }
            }
            ShowEditor(); UpdateTask(); if (runtimeValue != null) runtimeValue.Focus(); else if (expression != null) { expression.Focus(); expression.SelectAll(); }
        }
        private void AddBlock()
        {
            if (checking) return;
            if (lesson == null)
            {
                var preset = TutorChoiceDialog.Choose(this, "Inserisci un blocco pronto", Tutor.Guide.presets, p => p.title,
                    p => p.explanation + "\n\nPrima prepara\n" + p.needs + "\n\nNel blocco\n" + p.text + "\n\n" + p.example);
                if (preset == null) return;
                var after = TutorChoiceDialog.Choose(this, "Dopo quale casella?", session.Document.Nodes.Where(n => n.Kind != NodeKind.End).ToArray(), NodeLabel, n => TutorDiagnostics.Purpose(n.Kind));
                if (after == null) return;
                string branch = PickBranch(after); if (branch == null) return;
                if (session.Document.Nodes.Count >= DiagramIO.MaxNodes) { notice.Text = "Raggiunto il limite di 500 caselle."; return; }
                Before(null); var node = new FlowNode { Kind = (NodeKind)preset.kind, Text = preset.text }; session.Document.Nodes.Add(node);
                var old = Tutor.Outgoing(session.Document, after.Id, branch); string next = old == null ? null : old.To;
                if (old != null) session.Document.Edges.Remove(old);
                Tutor.Connect(session.Document, after.Id, branch, node.Id);
                if (next != null && node.Kind != NodeKind.End) { Tutor.Connect(session.Document, node.Id, node.Kind == NodeKind.Decision ? "true" : "next", next); if (node.Kind == NodeKind.Decision) Tutor.Connect(session.Document, node.Id, "false", next); }
                Tutor.Arrange(session.Document); Changed(); canvas.SelectNode(node.Id); canvas.FitToView(); return;
            }
            string target = session.Step < lesson.steps.Length ? lesson.steps[session.Step].node : null;
            if (target == null || lesson.Part(target) == null || Tutor.Find(session.Document, target) != null)
            {
                var missing = lesson.nodes.Where(p => Tutor.Find(session.Document, p.id) == null).ToArray();
                var part = TutorChoiceDialog.Choose(this, "Quale passaggio vuoi aggiungere?", missing, p => Ui.KindName((NodeKind)p.kind) + " · " + p.id, p => "Aggiungi la forma, poi scrivi il contenuto richiesto dalla lezione.");
                if (part == null) { notice.Text = "Tutte le caselle previste sono presenti. Puoi modificarle e collegarle."; return; } target = part.id;
            }
            var picked = IoBlockChoice.ChooseShape(this, "Scegli la forma della casella");
            if (!picked.HasValue) return;
            Before(null); Tutor.Insert(lesson, session.Document, target, !IoInstruction.Separate&&picked.Value==NodeKind.Input&&IoInstruction.IsIo((NodeKind)lesson.Part(target).kind)?(NodeKind)lesson.Part(target).kind:picked.Value); Changed(); canvas.SelectNode(target); canvas.FitToView();
        }
        private static string NodeLabel(FlowNode node) { string text = (node.Text ?? "").Replace('\n', ' '); return Ui.KindName(node.Kind) + " · " + (text.Length > 44 ? text.Substring(0, 44) + "…" : text.Length == 0 ? "casella vuota" : text); }
        private string PickBranch(FlowNode node)
        {
            if (node.Kind != NodeKind.Decision) return "next";
            return TutorChoiceDialog.Choose(this, "Quale uscita?", new[] { "true", "false" }, b => b == "true" ? "Sì / vero" : "No / falso", b => "Questa uscita viene seguita quando la domanda è " + (b == "true" ? "vera." : "falsa."));
        }
        private void ChangeKind()
        {
            var node = Tutor.Find(session.Document, canvas.SelectedNodeId); if (node == null) return; StashDraft();
            var kind = IoBlockChoice.ChooseShape(this, "Cambia tipo di casella");
            if(kind==NodeKind.Input&&!IoInstruction.Separate){var part=lesson==null?null:lesson.Part(node.Id);if(part!=null&&IoInstruction.IsIo((NodeKind)part.kind))kind=(NodeKind)part.kind;else if(IoInstruction.IsIo(node.Kind))kind=node.Kind;}
            if (!kind.HasValue || kind.Value == node.Kind) return;
            Before(null); node.Kind = kind.Value;
            session.Document.Edges.RemoveAll(e => e.From == node.Id && (node.Kind == NodeKind.End || (node.Kind == NodeKind.Decision ? e.Branch == "next" : e.Branch != "next")) || e.To == node.Id && node.Kind == NodeKind.Start);
            Changed();
        }
        private void Connect()
        {
            var from = TutorChoiceDialog.Choose(this, "Da quale casella parte la freccia?", session.Document.Nodes.Where(n => n.Kind != NodeKind.End).ToArray(), NodeLabel, n => TutorDiagnostics.Purpose(n.Kind));
            if (from == null) return; string branch = PickBranch(from); if (branch == null) return;
            var to = TutorChoiceDialog.Choose(this, "A quale casella arriva?", session.Document.Nodes.Where(n => n.Kind != NodeKind.Start && n.Id != from.Id).ToArray(), NodeLabel, n => "La freccia sostituirà soltanto l'uscita scelta, se già collegata.");
            if (to == null) return; Before(null); Tutor.Connect(session.Document, from.Id, branch, to.Id); Changed();
        }
        private void SaveCopy()
        {
            Apply(); using (var file = new SaveFileDialog { Filter = "Progetto Frociart (*.frociart)|*.frociart", FileName = session.Document.Title + ".frociart", DefaultExt = "frociart", AddExtension = true })
                if (file.ShowDialog(this) == DialogResult.OK) try { DiagramIO.Save(file.FileName, session.Document); var loaded = DiagramIO.Load(file.FileName); if (loaded.Nodes.Count != session.Document.Nodes.Count) throw new IOException("Rilettura non riuscita."); notice.Text = "Copia del progetto salvata."; } catch (Exception ex) { notice.Text = "Non riesco a salvare la copia: " + ex.Message; }
        }
        private void MoreCommands()
        {
            var menu = new FlowContextMenu();
            if (lesson != null) FlowMenus.Command(menu, this, "Prova direttamente il mio flowchart", delegate { finalRequested = true; CheckCurrent(); });
            var terminal = Tutor.Find(session.Document, canvas.SelectedNodeId);
            if (terminal != null && (terminal.Kind == NodeKind.Start || terminal.Kind == NodeKind.End)) FlowMenus.Command(menu, this, "Modifica etichetta…", delegate { EditTerminalLabel(terminal); });
            FlowMenus.Command(menu, this, "Leggi il mio flowchart", delegate { TutorTextDialog.ShowText(this, "Il tuo flowchart, spiegato", TutorDiagnostics.Describe(session.Document)); });
            FlowMenus.Command(menu, this, "Ingrandisci il foglio",delegate{canvas.ZoomBy(1.25f);});
            FlowMenus.Command(menu, this, "Riduci il foglio",delegate{canvas.ZoomBy(.8f);});
            FlowMenus.Command(menu, this, "Aspetto e feedback…", DesktopOptions);
            FlowMenus.Command(menu, this, "Rinomina variabile…", RenameVariable);
            var type = FlowMenus.Command(menu, this, "Cambia tipo di casella", ChangeKind);
            var remove = FlowMenus.Command(menu, this, "Elimina la selezione", delegate { canvas.DeleteSelection(); });
            type.Enabled = remove.Enabled = canvas.SelectedNodeId != null;
            FlowMenus.Command(menu, this, "Apri questo foglio nell’editor libero", OpenInFreeEditor);
            FlowMenus.Command(menu, this, "Salva copia come progetto…", SaveCopy);
            FlowMenus.ShowTransient(menu, this, Cursor.Position);
        }
        private void RenameVariable()
        {
            StashDraft(); string[] names = TutorVariables.Names(session.Document).OrderBy(n => n).ToArray();
            string oldName = TutorChoiceDialog.Choose(this, "Quale variabile vuoi rinominare?", names, n => n, n => "Verranno aggiornati gli usi del nome " + n + ". I testi tra virgolette e le domande leggibili rimangono intatti. Annulla ripristina l'intera operazione.");
            if (oldName == null) return;
            string newName = TutorNameDialog.Ask(this, oldName); if (newName == null) return;
            var candidate = TutorStore.Copy(session); int count;
            try { count = TutorVariables.Rename(candidate.Document, oldName, newName); var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { oldName, newName } };
                foreach (var draft in candidate.Drafts) draft.Text = TutorVariables.Rewrite(draft.Text, map);
            } catch (FlowException ex) { notice.Text = ex.Message; TutorTextDialog.ShowText(this, "Nome non cambiato", ex.Message); return; }
            if (count == 0) { notice.Text = "Non ho trovato usi di questo nome da aggiornare."; return; }
            Before(null); session = candidate; editingId = null; Changed(); notice.Text = "Variabile rinominata in " + count + " caselle. Annulla ripristina tutto.";
        }
        private void ShowBlockGuide(FlowNode node)
        {
            StashDraft(); var draft = session.Drafts.FirstOrDefault(d => d.Node == node.Id);
            var explained = Tutor.Find(DiagramIO.Clone(session.Document), node.Id);
            if (draft != null) { IoInstruction.Apply(explained,draft.Text); explained.Prompt = draft.Prompt; }
            var text = (draft == null ? "" : "Sto spiegando la bozza che hai scritto, non ancora applicata al foglio.\n\n") + (runner == null ? "Non ci sono ancora valori osservati.\n\n" : "Se questa casella venisse eseguita con i valori dell'ultimo passo:\n\n") + TutorDiagnostics.Explain(explained, runner == null ? null : runner.Variables) + "\n\n" + TutorDiagnostics.Inspect(explained, runner == null ? null : runner.Variables);
            foreach (string role in lesson == null ? new string[0] : lesson.variables) { var cells = role.Split('|'); if (cells.Length >= 3) text += "\n\n" + cells[0] + ": " + cells[1] + ". Tipo richiesto: " + cells[2] + "."; }
            TutorTextDialog.ShowText(this, Ui.KindName(node.Kind) + " · cosa succede qui", text + "\n\nNel blocco\n" + explained.Text);
        }
        private void ShowTerm(TutorTerm term) { TutorTextDialog.ShowText(this, term.title, term.explanation + "\n\nEsempio\n" + term.example + "\n\nAttenzione\n" + term.warning); }
        private void ShowGuide(){ShowGlossary(this);}
        internal static void ShowGlossary(Form owner)
        {
            var term = TutorChoiceDialog.Choose(owner, "Blocchi, simboli e parole — cerca qui", Tutor.Guide.terms, t => t.title + "  " + t.aliases,
                t => t.explanation + "\n\nEsempio\n" + t.example + "\n\nAttenzione\n" + t.warning);
            if (term != null) TutorTextDialog.ShowText(owner,term.title,term.explanation+"\n\nEsempio\n"+term.example+"\n\nAttenzione\n"+term.warning);
        }
        private void ShowAdvanced()
        {
            if (checking || !LeaveCurrent()) return;
            using (var dialog = new LearningDialog(legacy)) if (dialog.ShowDialog(this) == DialogResult.OK) { PracticeExample = dialog.PracticeExample; DialogResult = DialogResult.OK; Close(); }
        }
        private void BuildRunner(TabPage target)
        {
            var runnerShell=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty};runnerShell.RowStyles.Add(new RowStyle(SizeType.Absolute,34));runnerShell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(12) }; runLayout = panel;
            trialState=Ui.Label("Pronto per la prova",12,Ui.Blue,true);trialState.Dock=DockStyle.Fill;trialState.Padding=new Padding(12,5,0,0);runnerShell.Controls.Add(trialState,0,0);runnerShell.Controls.Add(panel,0,1);
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            runInfo = Ui.TextBox(true); runInfo.Font = Ui.Font(10); runInfo.ReadOnly = true; runInfo.AccessibleName = "Stato della prova e spiegazione degli errori"; runInfo.Dock = DockStyle.Fill; panel.Controls.Add(runInfo, 0, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            stepRun = Ui.Button("Avanti", 86, StepRunner, true); buttons.Controls.Add(stepRun); buttons.Controls.Add(Ui.Button("Ricomincia", 100, delegate { runner = null; StepRunner(false); }));
            var details = Ui.Button("Dettagli", 90, null); details.Click += delegate {
                bool show = !runVariables.Visible; runVariables.Visible = show; details.Text = show ? "Meno" : "Dettagli";
                runLayout.RowStyles[3].SizeType = show ? SizeType.Percent : SizeType.Absolute; runLayout.RowStyles[3].Height = show ? 50 : 0;
            }; buttons.Controls.Add(details); panel.Controls.Add(buttons, 0, 1);
            skipRun=Ui.Button("Salta animazione",145,StartFastRun);continueRun=Ui.Button("Continua",100,CompleteVerifiedRun,true);buttons.Controls.Add(skipRun);buttons.Controls.Add(continueRun);buttons.WrapContents=true;buttons.AutoSize=true;panel.RowStyles[1].SizeType=SizeType.AutoSize;
            runInputPanel = new Panel { Dock = DockStyle.Fill };
            runQuestion = Ui.TextBox(true); runQuestion.ReadOnly = true; runQuestion.Font = Ui.Font(10); runQuestion.AccessibleName = "Domanda e nome della variabile Input"; runQuestion.SetBounds(0, 0, 258, 66); runInputPanel.Controls.Add(runQuestion);
            runInput = Ui.TextBox(); runInput.Name = "TutorRunInput"; runInput.AccessibleName = "Valore richiesto durante Prova"; runInput.MaxLength = Values.MaxStringLength; runInput.SetBounds(0, 76, 142, 29); runInputPanel.Controls.Add(runInput);
            var send = Ui.Button("Invia dato", 104, SubmitRun); send.SetBounds(150, 74, 104, 34); runInputPanel.Controls.Add(send); runInputPanel.Resize += delegate { runQuestion.Width = runInputPanel.ClientSize.Width; send.Left = Math.Max(150, runInputPanel.ClientSize.Width - send.Width); runInput.Width = Math.Max(100, send.Left - 8); }; runInput.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SubmitRun(); e.SuppressKeyPress = true; } };
            panel.Controls.Add(runInputPanel, 0, 2);
            runVariables = new FlowListView { Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.Nonclickable, Font = Ui.Mono(10) };
            runVariables.Columns.Add("Variabile", 115); runVariables.Columns.Add("Valore", 140); panel.Controls.Add(runVariables, 0, 3);
            runVariables.Visible = false; panel.RowStyles[3].SizeType = SizeType.Absolute; panel.RowStyles[3].Height = 0;
            var outputLabel = Ui.Label("RISULTATO", 9, Ui.Muted, true);trialOutputLabel=outputLabel; outputLabel.Dock = DockStyle.Fill; panel.Controls.Add(outputLabel, 0, 4);
            runOutput = Ui.TextBox(true); runOutput.ReadOnly = true; runOutput.Font=Ui.Mono(13);runOutput.AccessibleName="Risultati reali del tuo flowchart"; runOutput.Dock = DockStyle.Fill; runOutput.MaxLength = Int32.MaxValue; panel.Controls.Add(runOutput, 0, 5);
            target.Controls.Add(runnerShell); RefreshRun();
        }
        private void StepRunner() { StepRunner(true); }
        private void StepRunner(bool advance)
        {
            if (runner == null) { Apply(); runner = new Interpreter(session.Document, 2000); stepExplanation = null; }
            if (advance && (runner.State == RuntimeState.Ready || runner.State == RuntimeState.Running)) { stepExplanation = TutorDiagnostics.Explain(Tutor.Find(session.Document, runner.CurrentNodeId), runner.Variables); runner.Step(); }
            RefreshRun();
        }
        private void SubmitRun()
        {
            if (runner == null || runner.State != RuntimeState.WaitingForInput) return;
            var rule = Tutor.InputRule(lesson, Tutor.CanonicalInput(lesson, session.Document, runner.InputVariable), runInput.Text);
            if (rule != null) { runInfo.Text = TutorTextDialog.Lines(rule.Title + "\n" + rule.Text); return; }
            string variable = runner.InputVariable; string raw = runInput.Text;
            runner.ProvideInput(raw); object observed;
            stepExplanation = "Hai inserito «" + raw + "». " + (runner.Variables.TryGetValue(variable, out observed) ? "L'Input conserva " + ValueFormatter.Format(observed) + " (" + Tutor.Type(observed) + ") in " + variable + "." : "Il dato non è stato conservato.");
            runInput.Clear(); RefreshRun();if(fastRun)StartFastRun();else stepRun.Focus();
        }
        private void RefreshRun()
        {
            if (runInfo == null || runInfo.IsDisposed) return;
            bool waiting = runner != null && runner.State == RuntimeState.WaitingForInput;
            runInfo.Text = TutorTextDialog.Lines(runner == null ? "Premi Avanti per attraversare Inizio. Ogni casella viene spiegata usando i valori reali del tuo diagramma. Per modificare o verificare, torna a Scrivi." : runner.State == RuntimeState.Error ? (stepExplanation ?? "") + "\n\n" + TutorDiagnostics.Runtime(runner.Error, runner.CurrentNodeId).Text : (stepExplanation ?? "") + "\n\n" + runner.LastMessage);
            stepRun.Enabled = runner == null || runner.State == RuntimeState.Ready || runner.State == RuntimeState.Running;
            stepRun.Visible = !waiting; runInputPanel.Visible = waiting;
            runLayout.RowStyles[2].Height = waiting ? 118 : 0;
            runQuestion.Text = waiting ? runner.InputPrompt + " → " + runner.InputVariable : "";
            runVariables.Items.Clear(); if (runner != null) foreach (var item in runner.Variables) runVariables.Items.Add(new ListViewItem(new[] { item.Key.ToLowerInvariant(), ValueFormatter.Format(item.Value) }));
            runOutput.Text = runner == null ? "" : TutorTextDialog.Lines(runner.OutputText);RefreshTrialState();
            bool complete=runner!=null&&runner.State==RuntimeState.Completed;
            if(complete&&!session.Demonstrated){session.Demonstrated=true;if(!SaveSession()){session.Demonstrated=false;notice.Text=store.Error;}}
            if(skipRun!=null)skipRun.Visible=session.Demonstrated&&!complete&&!fastRun;
            if(continueRun!=null)continueRun.Visible=complete&&verifiedRunPending&&verifiedRevision==revision;
            if(fastRun)stepRun.Visible=false;
            if (canvas != null) { canvas.FlowAnimationDuration=470; canvas.CurrentNodeId = runner == null ? null : runner.CurrentNodeId; canvas.CurrentEdgeId = runner == null ? null : runner.LastEdgeId; }
        }
        private void SetExpandImage(bool expanded){if(expandTutor==null)return;var previous=expandTutor.Image;using(var stream=typeof(TutorDialog).Assembly.GetManifestResourceStream("Frociart.Icons."+(expanded?"collapse":"expand")+".png")){if(stream!=null)using(var bitmap=Image.FromStream(stream))expandTutor.Image=new Bitmap(bitmap);}if(previous!=null)previous.Dispose();}
        private void SetTutorExpanded(bool value){if(workspaceSplit==null||tutorExpanded==value)return;FlowTransitions.Change(page);StashDraft();if(value)tutorInspectorWidth=workspaceSplit.ColumnStyles[1].Width;tutorExpanded=value;inspectorTabs.Visible=!value;workspaceSplit.ColumnStyles[1].Width=value?0:tutorInspectorWidth;workspaceHeading.Visible=!value;expandTutor.AccessibleName=value?"Ripristina i pannelli":"Espandi il flowchart";SetExpandImage(value);workspaceSplit.PerformLayout();if(!value&&editor!=null)ShowEditor();canvas.FitToView();}
        private void CompleteVerifiedRun(){
            if(!verifiedRunPending||verifiedRevision!=revision||runner==null||runner.State!=RuntimeState.Completed)return;
            bool completed=session.Completed;int previous=session.Step;session.Completed=true;session.Step=lesson.steps.Length;
            if(!SaveSession()){session.Completed=completed;session.Step=previous;notice.Text=store.Error;return;}
            verifiedRunPending=false;UpdateProgress();FinishTrial();UpdateTask();Primary();
        }
        private void StartFastRun(){
            if(!session.Demonstrated)return;fastRun=true;if(runner==null)StepRunner(false);
            if(fastRunTimer==null){fastRunTimer=new Timer{Interval=16};fastRunTimer.Tick+=delegate{if(!working||inspectorTabs==null||inspectorTabs.SelectedIndex!=1||!fastRun||runner==null){fastRunTimer.Stop();return;}int count=32;while(count-->0&&(runner.State==RuntimeState.Ready||runner.State==RuntimeState.Running))runner.Step();RefreshRun();if(runner.State!=RuntimeState.Ready&&runner.State!=RuntimeState.Running)fastRunTimer.Stop();};}
            fastRunTimer.Start();RefreshRun();
        }
        private void OnTutorClosing(object sender, FormClosingEventArgs e)
        {
            if (LeaveCurrent()) return;
            e.Cancel = FlowMessageDialog.Show(this, "Non riesco a salvare. Vuoi uscire perdendo soltanto le modifiche non salvate di questa sessione?", "Salvataggio non riuscito", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes;
        }
        protected override void Dispose(bool disposing)
        { if (disposing) { Ui.ThemeChanged -= ThemeChanged; draftTimer.Dispose();if(fastRunTimer!=null)fastRunTimer.Dispose();if(expandTutor!=null&&expandTutor.Image!=null)expandTutor.Image.Dispose(); } base.Dispose(disposing); }
    }

    internal static class TutorNameDialog
    {
        internal static string Ask(IWin32Window owner, string current)
        {
            using (var dialog = new Form { Text = "Nuovo nome della variabile", Size = new Size(540, 250), MinimumSize = new Size(500, 240), StartPosition = FormStartPosition.CenterParent, Font = Ui.Font(), Padding = new Padding(18), BackColor = Ui.Surface, MinimizeBox = false, MaximizeBox = false }) {
                var description = Ui.Label("Scegli un nome per " + current + ". Lettera o _ iniziale; poi lettere, cifre o _. Il valore conservato non cambia.", 11, Ui.Muted); description.Dock = DockStyle.Top; description.Height = 65;
                var input = Ui.TextBox(); input.Name = "TutorNewVariableName"; input.Text = current; input.MaxLength = 64; input.Dock = DockStyle.Top; input.Height = 30;
                var apply = Ui.Button("Rinomina", 180, delegate { if (ExpressionLanguage.Identifier(input.Text.Trim())) dialog.DialogResult = DialogResult.OK; else description.Text = "Il nome non è valido. Evita spazi, trattini, cifre iniziali e parole riservate come vero, falso, and, or, not."; }, true); apply.Dock = DockStyle.Bottom; apply.Height = 40;
                dialog.Controls.Add(input); dialog.Controls.Add(description); dialog.Controls.Add(apply); dialog.AcceptButton = apply; Ui.ApplyTheme(dialog);
                dialog.Shown += delegate { input.Focus(); input.SelectAll(); };
                return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }
    }
    internal sealed class TutorTree : TreeView
    {
        internal TutorTree()
        {
            DrawMode = TreeViewDrawMode.OwnerDrawText; ShowNodeToolTips = true;
            BackColor = Ui.Paper; ForeColor = Ui.Ink;
            DrawNode += delegate(object sender, DrawTreeNodeEventArgs e) {
                bool selected = (e.State & TreeNodeStates.Selected) != 0;
                var bounds = new Rectangle(e.Bounds.Left, e.Bounds.Top, Math.Max(1, ClientSize.Width - e.Bounds.Left - 6), ItemHeight);
                using (var brush = new SolidBrush(selected ? Ui.Tint : Ui.Paper)) e.Graphics.FillRectangle(brush, bounds);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, Font, bounds, Ui.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
        }
        protected override CreateParams CreateParams { get { var result = base.CreateParams; result.Style |= 0x8000; return result; } }
    }
    internal static class TutorChoiceDialog
    {
        internal static T Choose<T>(IWin32Window owner, string title, T[] items, Func<T, string> name, Func<T, string> explain, T initial = null) where T : class
        {
            int index = Show(owner, title, items.Select(name).ToArray(), i => explain(items[i]), initial == null ? 0 : Math.Max(0,Array.IndexOf(items,initial)));
            return index < 0 ? null : items[index];
        }
        internal static NodeKind? Choose(IWin32Window owner, string title, NodeKind[] items, Func<NodeKind, string> name, Func<NodeKind, string> explain)
        { int index = Show(owner, title, items.Select(name).ToArray(), i => explain(items[i])); return index < 0 ? (NodeKind?)null : items[index]; }
        private static int Show(IWin32Window owner, string title, string[] names, Func<int, string> explain, int initial = 0)
        {
            if (names.Length > 0 && names.Length <= 5)
            {
                string[] details = Enumerable.Range(0, names.Length).Select(i => explain(i) ?? "").ToArray();
                if (names.All(n => n.Length <= 80) && details.All(d => d.Length <= 280))
                    return ShowCompact(owner, title, names, details, initial);
            }
            using (var dialog = new Form { Text = title, Size = new Size(800, 520), MinimumSize = new Size(650, 450), StartPosition = FormStartPosition.CenterParent, Font = Ui.Font(), BackColor = Ui.Surface, MinimizeBox = false, MaximizeBox = false })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(16) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
                var search = Ui.TextBox(); search.Name = "TutorChoiceSearch"; search.AccessibleName = "Cerca fra le scelte"; search.Dock = DockStyle.Fill; layout.Controls.Add(search, 0, 0); layout.SetColumnSpan(search, 2);
                var list = new ListBox { Name = "TutorChoiceList", Dock = DockStyle.Fill, Font = Ui.Font(11), IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, AccessibleName = title };
                var content = Ui.TextBox(true); content.ReadOnly = true; content.Dock = DockStyle.Fill; content.Font = Ui.Font(11); content.Margin = new Padding(16, 0, 0, 0); content.AccessibleName = "Spiegazione della scelta";
                layout.Controls.Add(list, 0, 1); layout.Controls.Add(content, 1, 1); var indices = new List<int>();
                var confirm = Ui.Button("Scegli", 170, delegate { if (list.SelectedIndex >= 0) dialog.DialogResult = DialogResult.OK; }, true); confirm.Dock = DockStyle.Right;
                var cancel = Ui.Button("Annulla", 140, delegate { dialog.DialogResult = DialogResult.Cancel; }); cancel.Dock = DockStyle.Left;
                layout.Controls.Add(cancel, 0, 2); layout.Controls.Add(confirm, 1, 2); dialog.Controls.Add(layout);
                Action update = delegate { list.Items.Clear(); indices.Clear(); for (int i = 0; i < names.Length; i++) if (names[i].IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0) { indices.Add(i); list.Items.Add(names[i]); } if (list.Items.Count > 0) list.SelectedIndex = Math.Max(0,indices.IndexOf(initial)); else { content.Text = "Nessuna scelta corrisponde. Prova una parola più breve."; confirm.Enabled = false; } };
                list.SelectedIndexChanged += delegate { confirm.Enabled = list.SelectedIndex >= 0; if (list.SelectedIndex >= 0) { content.Text = TutorTextDialog.Lines(explain(indices[list.SelectedIndex])); content.SelectionLength = 0; } };
                search.TextChanged += delegate { update(); }; list.DoubleClick += delegate { if (confirm.Enabled) confirm.PerformClick(); };
                dialog.AcceptButton = confirm; dialog.CancelButton = cancel; update(); Ui.ApplyTheme(dialog);
                return dialog.ShowDialog(owner) == DialogResult.OK && list.SelectedIndex >= 0 ? indices[list.SelectedIndex] : -1;
            }
        }
        private static int ShowCompact(IWin32Window owner, string title, string[] names, string[] details, int initial)
        {
            int selected = -1;
            initial = Math.Max(0, Math.Min(names.Length - 1, initial));
            string[] heading = title.Split(new[] { '·' }, 2);
            using (var dialog = new Form { Name = "CompactChoiceDialog", Text = heading[0].Trim() + " — Frociart", StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Ui.Font(), BackColor = Ui.Surface })
            {
                dialog.SuspendLayout();
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                int headerHeight = heading.Length == 2 ? 60 : 36;
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, headerHeight));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
                var label = Ui.Label(heading[0].Trim(), 16, Ui.Ink, true); label.Dock = DockStyle.Top; label.Height = 30; header.Controls.Add(label);
                if (heading.Length == 2) { var help = Ui.Label(heading[1].Trim(), 9, Ui.Muted); help.Dock = DockStyle.Bottom; help.Height = 25; header.Controls.Add(help); }
                layout.Controls.Add(header, 0, 0);
                var body = new FlowScrollPanel { Name = "CompactChoiceBody", Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
                var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = names.Length, Margin = Padding.Empty };
                rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.Controls.Add(rows); layout.Controls.Add(body, 0, 1);
                var options = new List<CompactChoiceOption>();
                int total = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    int index = i;
                    var button = new CompactChoiceOption(names[i], details[i], i == initial) { Name = "CompactChoice" + i, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8), TabIndex = i };
                    int height = button.GetPreferredSize(new Size(455, 0)).Height;
                    rows.RowStyles.Add(new RowStyle(SizeType.Absolute, height + 8)); rows.Controls.Add(button, 0, i); options.Add(button); total += height + 8;
                    button.Click += delegate { selected = index; dialog.DialogResult = DialogResult.OK; };
                    button.KeyDown += delegate(object sender, KeyEventArgs e) {
                        int next = e.KeyCode == Keys.Up ? (index + names.Length - 1) % names.Length : e.KeyCode == Keys.Down ? (index + 1) % names.Length : -1;
                        if (next >= 0) { options[next].Focus(); e.Handled = e.SuppressKeyPress = true; }
                    };
                }
                var cancel = Ui.Button("Annulla", 105, delegate { dialog.DialogResult = DialogResult.Cancel; }); cancel.Dock = DockStyle.Right;
                var footer = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) }; footer.Controls.Add(cancel); layout.Controls.Add(footer, 0, 2);
                dialog.Controls.Add(layout); dialog.CancelButton = cancel;
                dialog.ClientSize = new Size(520, 40 + headerHeight + total + 44);
                Ui.ApplyTheme(dialog);
                dialog.AutoScaleDimensions = new SizeF(96, 96); dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.ResumeLayout(true);
                dialog.Shown += delegate {
                    var area = Screen.FromControl(dialog).WorkingArea;
                    dialog.Width = Math.Min(dialog.Width, Math.Max(200, area.Width - 32));
                    dialog.Height = Math.Min(dialog.Height, Math.Max(200, area.Height - 32));
                    dialog.Left = Math.Max(area.Left, Math.Min(dialog.Left, area.Right - dialog.Width));
                    dialog.Top = Math.Max(area.Top, Math.Min(dialog.Top, area.Bottom - dialog.Height));
                    rows.SuspendLayout();
                    for (int i = 0; i < options.Count; i++) rows.RowStyles[i].Height = options[i].GetPreferredSize(new Size(Math.Max(80, body.ClientSize.Width - SystemInformation.VerticalScrollBarWidth), 0)).Height + 8 * NativeTheme.DpiScale(dialog);
                    rows.ResumeLayout(true);
                    options[initial].Focus(); body.ScrollControlIntoView(options[initial]);
                };
                return dialog.ShowDialog(owner) == DialogResult.OK ? selected : -1;
            }
        }
        private sealed class CompactChoiceOption : Button
        {
            private readonly string detail;
            internal readonly bool Current;
            private readonly MotionValue hover, press;
            internal CompactChoiceOption(string text, string detail, bool current)
            {
                Text = text; this.detail = detail; Current = current; Font = Ui.Font(10.5f, FontStyle.Bold); UseMnemonic = false;
                AccessibleName = text + (current ? ", attuale" : ""); AccessibleDescription = detail;
                FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Ui.Surface; Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                hover = new MotionValue(this); press = new MotionValue(this);
            }
            public override Size GetPreferredSize(Size proposedSize)
            {
                float scale = NativeTheme.DpiScale(this);
                int width = Math.Max(1, proposedSize.Width - (int)Math.Round(56 * scale));
                using (var font = Ui.Font(9))
                    return new Size(proposedSize.Width, Math.Max((int)Math.Ceiling(76 * scale), (int)Math.Ceiling(32 * scale) + TextRenderer.MeasureText(Text, Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height +
                        TextRenderer.MeasureText(detail, font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height));
            }
            protected override bool IsInputKey(Keys keyData)
            { Keys key = keyData & Keys.KeyCode; return key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData); }
            protected override void OnMouseEnter(EventArgs e) { hover.Set(1); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover.Set(0); press.Set(0); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) press.Set(1, 65); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { press.Set(0, 160); base.OnMouseUp(e); }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent == null ? Ui.Surface : Parent.BackColor);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float scale = e.Graphics.DpiX / 96f, shift = press.Value * scale;
                using (var path = Ui.Rounded(new RectangleF(1 + shift, 1 + shift, Width - 3 - 2 * shift, Height - 3 - 2 * shift), 10 * scale))
                using (var fill = new SolidBrush(UiMotion.Blend(Current ? Ui.Tint : Ui.Raised, Ui.Hover, hover.Value)))
                using (var pen = new Pen(Focused && ShowFocusCues ? Ui.Blue : Current ? Ui.Blue : Ui.Line, (Focused && ShowFocusCues ? 2 : 1) * scale))
                { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
                int inset = (int)Math.Round(16 * scale), width = Math.Max(1, Width - inset * 2 - (int)(24 * scale));
                int titleHeight = TextRenderer.MeasureText(Text, Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(inset, inset - (int)(3 * scale), width, titleHeight), Ui.Ink, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                int detailTop = inset + titleHeight + (int)(2 * scale);
                using (var font = Ui.Font(9)) TextRenderer.DrawText(e.Graphics, detail, font, new Rectangle(inset, detailTop, width, Math.Max(1, Height - detailTop - (int)(12 * scale))), Ui.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                if (Current) using (var pen = new Pen(Ui.Blue, 1.8f * scale))
                { float x = Width - inset - 9 * scale, y = inset + 5 * scale; e.Graphics.DrawLines(pen, new[] { new PointF(x - 5 * scale, y), new PointF(x - scale, y + 4 * scale), new PointF(x + 6 * scale, y - 4 * scale) }); }
            }
        }
    }
    internal static class TutorTextDialog
    {
        internal static string Source(string text) { return (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n'); }
        internal static string Lines(string text) { return Source(text).Replace("\n", Environment.NewLine); }
        internal static void ShowText(IWin32Window owner, string title, string text)
        {
            using (var dialog = new Form { Text = title, Size = new Size(680, 550), MinimumSize = new Size(500, 380), StartPosition = FormStartPosition.CenterParent, Font = Ui.Font(), Padding = new Padding(20), BackColor = Ui.Surface })
            {
                var content = Ui.TextBox(true); content.Font = Ui.Font(12); content.Text = Lines(text); content.ReadOnly = true; content.Dock = DockStyle.Fill;
                var close = Ui.Button("Ho capito", 160, dialog.Close, true); close.Dock = DockStyle.Bottom; close.Height = 42;
                dialog.Controls.Add(content); dialog.Controls.Add(close); dialog.AcceptButton = close; dialog.CancelButton = close; Ui.ApplyTheme(dialog);
                dialog.Shown += delegate { content.Select(0, 0); close.Focus(); }; dialog.ShowDialog(owner);
            }
        }
    }
}
