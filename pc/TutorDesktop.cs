using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed partial class TutorDialog
    {
        private TutorResultBand resultBand;
        private Label desktopHeading;
        private TableLayoutPanel desktopShell;
        private Panel desktopFooter;
        private Control workspaceHeading, trialTools;
        private void UpdateTrialFocus()
        {
            bool trial = working && inspectorTabs != null && inspectorTabs.SelectedIndex == 1;
            if (desktopFooter != null) desktopFooter.Visible = !trial;
            if (desktopShell != null) desktopShell.RowStyles[2].Height = trial ? 0 : 76;
            navigation.Visible = !trial;SyncTrialMode(trial);
            ArrangeDesktopHeader();
            if (workspaceHeading != null && !workspaceHeading.IsDisposed) workspaceHeading.Visible = !trial;
            if (trialTools != null && !trialTools.IsDisposed)
                foreach (Control button in trialTools.Controls)
                    button.Visible = !trial || button.Text == "Tutto" || button.Text == "−" || button.Text == "+";
        }
        private void BuildDesktopShell()
        {
            Text = "Impara e costruisci — Frociart"; Font = Ui.Font();
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1320, 860); MinimumSize = new Size(1050, 720);
            StartPosition = FormStartPosition.CenterParent; BackColor = Ui.Paper; KeyPreview = true;
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); Controls.Add(shell); desktopShell = shell;
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface };
            desktopHeading = Ui.Label("FROCIART / Impara", 17, Ui.Ink, true); desktopHeading.SetBounds(22, 12, 310, 28); header.Controls.Add(desktopHeading);
            progress.SetBounds(23, 43, 310, 24); header.Controls.Add(progress);
            navigation.AutoSize = true; navigation.WrapContents = false; navigation.Top = 22;
            navigation.Controls.Add(Ui.Button("Percorso", 102, ShowOverview));
            navigation.Controls.Add(Ui.Button("Blocchi guidati", 144, delegate { if (!checking) OpenWorkshop(); }));
            navigation.Controls.Add(Ui.Button("Guida", 80, ShowGuide)); var options=Ui.Button("⚙",48,delegate{if(working)MoreCommands();else DesktopOptions();});options.AccessibleName="Strumenti del foglio";navigation.Controls.Add(options); header.Controls.Add(navigation);
            finishTrial=Ui.Button("Fine prova · Esc",140,FinishTrial);finishTrial.Top=22;finishTrial.Visible=false;header.Controls.Add(finishTrial);
            header.Resize += delegate { ArrangeDesktopHeader(); };
            navigation.SizeChanged += delegate { ArrangeDesktopHeader(); };
            shell.Controls.Add(header, 0, 0);
            page.Dock = DockStyle.Fill; page.BackColor = Ui.Paper; shell.Controls.Add(page, 0, 1);
            var footer = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface }; shell.Controls.Add(footer, 0, 2); desktopFooter = footer;
            var close = Ui.Button("Torna al mio foglio", 174, Close); close.SetBounds(20, 17, 174, 42); footer.Controls.Add(close);
            primary = Ui.Button("Inizia il percorso", 300, Primary, true); primary.Top = 16; primary.Height = 44; footer.Controls.Add(primary);
            notice.Top = 18; notice.Height = 42; notice.AutoEllipsis = true; footer.Controls.Add(notice);
            Action arrange = delegate { primary.Left = footer.ClientSize.Width - primary.Width - 20; notice.Left = close.Right + 20; notice.Width = Math.Max(40, primary.Left - notice.Left - 20); };
            footer.Resize += delegate { arrange(); }; Shown += delegate { Ui.FitStartupToScreen(this);arrange(); ShowOverview(); UpdateProgress(); ArrangeDesktopHeader(); };
            draftTimer.Tick += delegate { draftTimer.Stop(); SaveSession(); }; FormClosing += OnTutorClosing; Ui.ThemeChanged += ThemeChanged;
        }
        private void ArrangeDesktopHeader()
        {
            if (desktopHeading == null || desktopHeading.Parent == null || finishTrial == null) return;
            var header = desktopHeading.Parent;
            int inset = Math.Max(18, (int)Math.Round(22 * NativeTheme.DpiScale(header)));
            finishTrial.Left = header.ClientSize.Width - finishTrial.Width - inset;
            navigation.Left = header.ClientSize.Width - navigation.PreferredSize.Width - inset;
            int right = finishTrial.Visible ? finishTrial.Left : navigation.Left;
            desktopHeading.Width = Math.Max(0, right - desktopHeading.Left - inset);
            progress.Width = desktopHeading.Width;
        }
        private void DesktopOptions()
        {
            var menu = new FlowContextMenu();
            FlowMenus.Command(menu, this, "Approfondimenti e quiz precedenti", ShowAdvanced);
            FlowMenus.Toggle(menu, this, "Suoni delle verifiche", TutorFeedbackSignals.Enabled,
                delegate(bool value) { if (!TutorFeedbackSignals.SetEnabled(value)) notice.Text = "Non riesco a salvare la preferenza audio."; });
            FlowMenus.Toggle(menu, this, "Animazioni dell’interfaccia", UiMotion.PreferredEnabled,
                delegate(bool value) { if (!UiMotion.SetEnabled(value)) notice.Text = "Non riesco a salvare la preferenza delle animazioni."; });
            FlowMenus.ShowTransient(menu, this, Cursor.Position);
        }
        private Dictionary<string, TutorSession> SavedStates() { return Tutor.Lessons.ToDictionary(l => l.id, l => store.Peek(l.id)); }
        private TutorLesson RecommendedLesson()
        {
            var states = SavedStates();
            var ongoing = Tutor.Lessons.Where(l => states[l.id] != null && !states[l.id].Completed && (states[l.id].Step > 0 || states[l.id].Drafts.Count > 0 || states[l.id].Document.Nodes.Count != l.Seed().Nodes.Count)).OrderByDescending(l => store.LastTouched(l.id)).FirstOrDefault();
            return ongoing ?? Tutor.Lessons.FirstOrDefault(l => states[l.id] == null || !states[l.id].Completed) ?? Tutor.Lessons[0];
        }
        private void RenderOverview()
        {
            if (checking || !LeaveCurrent()) return; session = null; lesson = null; feedback = null;
            var content = Reader(); desktopHeading.Text = "FROCIART / Impara"; UpdateProgress();
            Paragraph(content, "Impara i flowchart. Un passo alla volta.", true);
            Paragraph(content, "Capisci una regola, usala nelle caselle e guarda cosa succede. Parti da zero, senza configurazioni.");
            var next = RecommendedLesson(); var states = SavedStates(); var savedNext = states[next.id];
            var nextBox = new Panel { Height = 84, MinimumSize = new Size(0, 84), BackColor = Ui.Tint, Tag = "tint", Margin = new Padding(0, 4, 0, 14), AccessibleName = "Prossima lezione consigliata: " + next.title };
            var eyebrow = Ui.Label(savedNext != null && !savedNext.Completed ? "RIPRENDI DA QUI" : "IL TUO PROSSIMO PASSO", 9, Ui.Blue, true); eyebrow.SetBounds(18, 12, 700, 20); nextBox.Controls.Add(eyebrow);
            var title = Ui.Label(next.title, 17, Ui.Ink, true); title.SetBounds(18, 35, 900, 33); nextBox.Controls.Add(title);
            nextBox.Resize += delegate { title.Width = nextBox.ClientSize.Width - 36; eyebrow.Width = title.Width; }; content.Controls.Add(nextBox);
            Paragraph(content, "Il percorso", true);
            var grid = new TableLayoutPanel { AutoSize = false, Height = 336, MinimumSize = new Size(0, 336), ColumnCount = 2, RowCount = 3, Margin = Padding.Empty };
            for (int i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < Tutor.Units.Length; i++) {
                int unit = i; var lessons = Tutor.Lessons.Where(l => l.unit == "f" + (i + 1).ToString("00")).ToArray(); int completed = lessons.Count(l => states[l.id] != null && states[l.id].Completed);
                var tile = new TutorUnitButton { Text = Tutor.Units[i], Number = i + 1, Detail = completed + " / " + lessons.Length + " lezioni completate", Complete = completed == lessons.Length, Dock = DockStyle.Fill, Height = 100, Margin = new Padding(i % 2 == 0 ? 0 : 8, 0, i % 2 == 0 ? 8 : 0, 12), AccessibleName = "Unità " + (i + 1) + ": " + Tutor.Units[i] + ", " + completed + " su " + lessons.Length + " completate" };
                tile.Click += delegate { ShowUnit(unit); }; grid.Controls.Add(tile, i % 2, i / 2);
            }
            content.Controls.Add(grid); primary.Text = savedNext != null && savedNext.Completed ? "Ripassa una lezione" : savedNext != null && savedNext.Step > 0 ? "Riprendi il percorso" : "Inizia il percorso";
            primary.Enabled = true; notice.Text = "Scegli un’unità oppure continua dalla lezione consigliata."; Ui.ApplyTheme(this);
        }
        private void ShowUnit(int unit)
        {
            if (checking || !LeaveCurrent()) return; session = null; lesson = null; feedback = null;
            var content = Reader(); Paragraph(content, "Unità " + (unit + 1) + " · " + Tutor.Units[unit], true);
            Paragraph(content, "Apri la lezione consigliata oppure scegli cosa ripassare. Puoi tornare qui in qualsiasi momento.");
            var states = SavedStates(); var next = RecommendedLesson();
            foreach (var item in Tutor.Lessons.Where(l => l.unit == "f" + (unit + 1).ToString("00"))) {
                var state = states[item.id]; bool done = state != null && state.Completed;
                var row = new TutorUnitButton { Number = Array.IndexOf(Tutor.Lessons, item) + 1, Text = item.title, Detail = done ? "Completata · puoi ripassarla" : item.id == next.id ? "Consigliata · " + item.goal : item.goal, Complete = done, Height = 106, MinimumSize = new Size(0,106), Margin = new Padding(0, 0, 0, 10), AccessibleName = item.title + (done ? ", completata" : ", da completare") };
                row.Click += delegate { SelectLesson(item.id); }; content.Controls.Add(row);
            }
            primary.Text = "Continua il percorso"; notice.Text = "Le lezioni completate restano disponibili per il ripasso."; Ui.ApplyTheme(this);
        }
        private FlowLayoutPanel LessonReader(TabPage tab)
        {
            var scroll = new FlowScrollPanel { Dock = DockStyle.Fill, AutoScroll = true };
            var content = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(22, 14, 22, 20), Margin = Padding.Empty };
            scroll.Controls.Add(content); tab.Controls.Add(scroll);
            Action resize = delegate { int width = Math.Max(280, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4); content.Width = width; foreach (Control child in content.Controls) { child.MaximumSize = new Size(width - 48, Int32.MaxValue); child.Width = width - 48; } scroll.AutoScrollMinSize = new Size(0, content.PreferredSize.Height); };
            scroll.Resize += delegate { resize(); }; content.ControlAdded += delegate { resize(); }; return content;
        }
        private void RenderLessonIntroduction()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(24, 12, 24, 18) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var title = Ui.Label(lesson.title, 22, Ui.Ink, true); title.Dock = DockStyle.Fill; title.Padding = new Padding(0, 8, 8, 4); layout.Controls.Add(title, 0, 0);
            var stage = Ui.Label("LEZIONE " + (Array.IndexOf(Tutor.Lessons, lesson) + 1).ToString("00") + " / 24\n" + Tutor.Units[Int32.Parse(lesson.unit.Substring(1)) - 1], 11, Ui.Muted); stage.Dock = DockStyle.Fill; stage.TextAlign = ContentAlignment.MiddleRight; layout.Controls.Add(stage, 1, 0);
            var tabs = new TutorTabs { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 18, 0), Font = Ui.Font(11) };
            var idea = new TabPage("Capisci"); var example = new TabPage("Esempio e regole"); var words = new TabPage("Parole nuove"); tabs.TabPages.Add(idea); tabs.TabPages.Add(example); tabs.TabPages.Add(words);
            var c = LessonReader(idea); Paragraph(c, "La tua missione", true); Paragraph(c, lesson.goal); Paragraph(c, lesson.explanation); Paragraph(c, "Come si legge", true); Paragraph(c, lesson.reading);
            c = LessonReader(example); Paragraph(c, "Un esempio", true); Paragraph(c, lesson.example); Paragraph(c, "L’errore da evitare", true); Paragraph(c, lesson.warning); Paragraph(c, "Cosa controlla la prova", true); Paragraph(c, Tutor.Criteria(lesson));
            c = LessonReader(words); Paragraph(c, "Le parole di questa lezione", true); foreach (var id in lesson.terms) { var term = Tutor.Guide.terms.FirstOrDefault(x => x.id == id); if (term != null) ReaderButton(c, term.title, delegate { ShowTerm(term); }); }
            layout.Controls.Add(tabs, 0, 1);
            var drawing = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 }; drawing.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); drawing.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var caption = Ui.Label("IL TUO FOGLIO\nTra poco lo completi tu.", 11, Ui.Muted, true); caption.Dock = DockStyle.Fill; drawing.Controls.Add(caption, 0, 0);
            var preview = new FlowCanvas { Diagram = DiagramIO.Clone(session.Document), Dock = DockStyle.Fill, Enabled = false, AccessibleName = "Anteprima del tuo foglio salvato" }; drawing.Controls.Add(preview, 0, 1); preview.Resize += delegate { preview.FitToView(); };
            layout.Controls.Add(drawing, 1, 1); page.Controls.Add(layout); working = false; desktopHeading.Text = "FROCIART / La lezione";
        }
        private string FeedbackSummary(TutorFeedback f)
        {
            string why = f.Why ?? ""; int marker = why.IndexOf("Un caso da correggere:", StringComparison.Ordinal);
            return marker < 0 ? why : why.Substring(marker + "Un caso da correggere:".Length).Trim();
        }
        private void UpdateDesktopResult()
        {
            if (resultBand == null || resultBand.IsDisposed) return;
            resultBand.Visible = feedback != null; if (feedback == null) return;
            string title = feedback.Pass ? feedback.Code == "complete" ? "Flowchart corretto" : lesson == null ? "Struttura valida" : "Passo corretto" : feedback.Code == "save-failed" || feedback.Code == "check-failed" ? "Verifica non completata" : "Da correggere";
            string detail = feedback.Pass ? feedback.Code == "complete" ? feedback.Passed + " / " + feedback.Total + " prove superate. "+(verifiedRunPending?"Segui l’esecuzione fino alla fine per completare la lezione.":"Puoi passare alla prossima lezione.") : lesson == null ? "Ora eseguilo con i tuoi dati per controllare il risultato." : "Hai svolto questo passo. Premi Continua per andare avanti." : feedback.Title + ". Il tuo lavoro è conservato: leggi cosa rivedere e riprova.";
            resultBand.Bind(title, detail, feedback.Pass);
        }
        private void EditTerminalLabel(FlowNode node)
        {
            using (var dialog = new Form { Text = "Etichetta della casella", Size = new Size(480, 220), StartPosition = FormStartPosition.CenterParent, Padding = new Padding(20), Font = Ui.Font() }) {
                var input = Ui.TextBox(); input.Text = node.Text; input.MaxLength = 128; input.Dock = DockStyle.Top; dialog.Controls.Add(input);
                var save = Ui.Button("Salva etichetta", 180, delegate { if (!String.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = DialogResult.OK; }, true); save.Dock = DockStyle.Bottom; dialog.Controls.Add(save); dialog.AcceptButton = save; Ui.ApplyTheme(dialog);
                if (dialog.ShowDialog(this) == DialogResult.OK) { Before(null); node.Text = input.Text.Trim(); Changed(); }
            }
        }
    }
}
