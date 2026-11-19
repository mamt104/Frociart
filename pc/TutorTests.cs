using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Frociart.Tests
{
    internal static class TutorTests
    {
        private static int checks, walked, cases;
        private static string directory;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static void Check(bool good, string message) { if (!good) throw new Exception(message); checks++; }
        private static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name, Flags).GetValue(target); }
        private static void Invoke(object target, string name) { target.GetType().GetMethod(name, Flags, null, Type.EmptyTypes, null).Invoke(target, null); Pump(); }
        private static void Pump() { for (int i = 0; i < 3; i++) Application.DoEvents(); }
        private static Control Find(Control parent, string text) { foreach (Control child in parent.Controls) { if (child.Text == text) return child; var found = Find(child, text); if (found != null) return found; } return null; }
        private static void Click(Control parent, string text) { var button = Find(parent, text) as Button; if (button == null) throw new Exception("Missing button: " + text); button.PerformClick(); Pump(); }
        private static void Wait(Func<bool> condition, string failure) { var watch = System.Diagnostics.Stopwatch.StartNew(); while (!condition()) { Pump(); Thread.Sleep(3); if (watch.ElapsedMilliseconds > 12000) throw new Exception(failure); } Pump(); }
        private static void Capture(Form form, string name) { Pump(); using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(directory, name)); } }
        private static string NodeName(FlowNode node) { string text = (node.Text ?? "").Replace('\n', ' '); return Ui.KindName(node.Kind) + " · " + (text.Length > 44 ? text.Substring(0, 44) + "…" : text.Length == 0 ? "casella vuota" : text); }
        private static void Choose(Action launch, params string[] choices)
        {
            var queue = new Queue<string>(choices); Exception failure = null; int ticks = 0;
            using (var timer = new System.Windows.Forms.Timer { Interval = 15 })
            {
                timer.Tick += delegate {
                    if (++ticks > 700) { failure = new Exception("Choice dialog timed out"); foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => f.Text.Contains("casella") || f.Text.Contains("freccia")).ToArray()) form.Close(); return; }
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Controls.Find("TutorChoiceList", true).Length > 0);
                    if (dialog == null || queue.Count == 0) return;
                    var list = (ListBox)dialog.Controls.Find("TutorChoiceList", true)[0]; string wanted = queue.Peek();
                    int index = -1; for (int i = 0; i < list.Items.Count; i++) if ((string)list.Items[i] == wanted) { index = i; break; }
                    if(index<0&&(wanted==Ui.KindName(NodeKind.Input)||wanted==Ui.KindName(NodeKind.Output)))index=list.Items.IndexOf("Input / Output");queue.Dequeue();
                    if (index < 0) { failure = new Exception("Missing choice: " + wanted); dialog.Close(); timer.Stop(); return; }
                    list.SelectedIndex = index; ((Button)Find(dialog, "Scegli")).PerformClick();
                };
                timer.Start(); launch(); timer.Stop();
            }
            if (failure != null) throw failure; Check(queue.Count == 0, "all native choices consumed; remaining " + String.Join(",", queue)); Pump();
        }
        private static void Model()
        {
            ExplanationTests(); PresetTests();
            Check(Tutor.Lessons.Length == 24 && Tutor.Guide.terms.Length == 39 && Tutor.Guide.presets.Length == 13, "embedded curriculum and guide complete");
            foreach (var lesson in Tutor.Lessons)
            {
                var document = lesson.Seed();
                for (int i = 0; i < lesson.steps.Length; i++) {
                    var step = lesson.steps[i]; var part = lesson.Part(step.node);
                    if (step.action == Tutor.Add) Tutor.Insert(lesson, document, step.node, (NodeKind)part.kind);
                    if (step.action == Tutor.Write) Tutor.Find(document, step.node).Text = part.answer;
                    if (step.action == Tutor.ConnectAction) Tutor.Connect(document, step.a, step.branch, step.b);
                    var result = Tutor.CheckStep(lesson, i, document, step.action == Tutor.Value ? step.hint : "");
                    Check(result.Pass, "construction " + lesson.id + " / " + i + ": " + result.Text); walked++;
                }
                var final = Tutor.Check(lesson, document); Check(final.Pass, "student graph " + lesson.id + ": " + final.Text); cases += lesson.trials.Length;
                Check(Tutor.Check(lesson, lesson.Solution()).Pass, "reference " + lesson.id);
                Check(Tutor.Criteria(lesson).Length > 80, "criteria shown " + lesson.id);
            }
            var greeting = Tutor.Lesson("b02");
            foreach (string message in new[] { "\"ciao\"", "\"CIAO\"", "\"Ciao\"", "\"Benvenuto Christian\"", "'salve'", "\"Ciao\" + \"!\"", "\"5\"" }) {
                var d = greeting.Solution(); Tutor.Find(d, "messaggio").Text = message; Check(Tutor.Check(greeting, d).Pass, "free text accepted " + message);
                Check(Tutor.CheckStep(greeting, 1, d, "").Pass, "same rule in individual step " + message);
            }
            foreach (string message in new[] { "5", "vero", "\"\"", "\"   \"", "ciao", "“ciao”" }) {
                var d = greeting.Solution(); Tutor.Find(d, "messaggio").Text = message; Check(!Tutor.Check(greeting, d).Pass, "wrong type/blank/syntax rejected " + message);
            }
            foreach (var lesson in Tutor.Lessons.Where(Tutor.LabelOutput)) {
                var d = lesson.Solution(); foreach (var node in d.Nodes.Where(n => n.Kind == NodeKind.Output)) { object literal; try { literal = Tutor.Literal(node.Text); } catch { continue; } if (literal is string) node.Text = "\"  " + ((string)literal).ToUpperInvariant().Replace(" ", "   ") + "!!!  \""; }
                Check(Tutor.Check(lesson, d).Pass, "label formatting accepted " + lesson.id);
                var different = lesson.Solution(); var target = different.Nodes.First(n => n.Kind == NodeKind.Output); target.Text = "\"un altro risultato\""; Check(!Tutor.Check(lesson, different).Pass, "different labels rejected " + lesson.id);
            }
            var numbers = Tutor.Lesson("b03").Solution(); numbers.Nodes.First(n => n.Kind == NodeKind.Output).Text = "\"5\""; Check(!Tutor.Check(Tutor.Lesson("b03"), numbers).Pass, "text number is not numeric");
            numbers.Nodes.First(n => n.Kind == NodeKind.Output).Text = "2 + 3"; Check(Tutor.Check(Tutor.Lesson("b03"), numbers).Pass, "equivalent expression accepted");
            var age = Tutor.Lesson("b24").Solution(); age.Nodes.First(n => n.Kind == NodeKind.Decision).Text = "eta > 18"; var failure = Tutor.Check(Tutor.Lesson("b24"), age); Check(!failure.Pass && failure.Text.Contains("18"), "boundary error explained");
            var negation = Tutor.Lesson("b15").Solution(); negation.Nodes.First(n => n.Kind == NodeKind.Decision).Text = "!0"; Check(!Tutor.Check(Tutor.Lesson("b15"), negation).Pass, "not-zero confusion rejected");
            foreach (var node in new[] { new FlowNode { Kind = NodeKind.Input, Text = "eta = 18" }, new FlowNode { Kind = NodeKind.Process, Text = "x++" }, new FlowNode { Kind = NodeKind.Process, Text = "x == 2" }, new FlowNode { Kind = NodeKind.Output, Text = "print(2)" }, new FlowNode { Kind = NodeKind.Decision, Text = "eta ≥ 18" }, new FlowNode { Kind = NodeKind.Process, Text = "x = (2 + 3" } })
                Check(TutorDiagnostics.Before(node) != null && TutorDiagnostics.Before(node).Fix.Length > 10, "beginner diagnostic " + node.Text);
            var inputLesson = Tutor.Lesson("b06"); int vi = Array.FindIndex(inputLesson.steps, s => s.action == Tutor.Value);
            Check(Tutor.CheckStep(inputLesson, vi, inputLesson.Solution(), "21,0").Pass, "decimal input equivalent");
            Check(Tutor.CheckStep(inputLesson, vi, inputLesson.Solution(), "eta").Code == "input-type", "runtime variable is not input value");
            Check(Tutor.CheckStep(inputLesson, vi, inputLesson.Solution(), "22").Pass, "different valid input follows the declared echo goal");
            var before = DiagramIO.Clone(age); Tutor.Arrange(age); Check(age.Nodes.Select(n => n.Id).SequenceEqual(before.Nodes.Select(n => n.Id)) && age.Edges.Count == before.Edges.Count, "arrange preserves graph");
        }
        private static void RenameInNativeDialog(TutorDialog owner, string before, string after)
        {
            int phase = 0, ticks = 0; Exception failure = null;
            using (var timer = new System.Windows.Forms.Timer { Interval = 15 }) {
                timer.Tick += delegate {
                    if (++ticks > 700) { failure = new Exception("Rename dialogs timed out"); timer.Stop(); foreach (Form f in Application.OpenForms.Cast<Form>().Where(f => f != owner).ToArray()) f.Close(); return; }
                    var choice = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Controls.Find("TutorChoiceList", true).Length > 0);
                    if (phase == 0 && choice != null) { var list = (ListBox)choice.Controls.Find("TutorChoiceList", true)[0]; list.SelectedItem = before; phase = 1; Click(choice, "Scegli"); return; }
                    var name = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Controls.Find("TutorNewVariableName", true).Length > 0);
                    if (phase == 1 && name != null) { ((TextBox)name.Controls.Find("TutorNewVariableName", true)[0]).Text = after; phase = 2; Capture(name, "11-rename-variable.png"); Click(name, "Rinomina"); }
                };
                timer.Start(); Click(owner, "Rinomina variabile…"); timer.Stop();
            }
            if (failure != null) throw failure; Check(phase == 2, "native rename dialogs completed");
        }
        private static void FollowDemonstration(TutorDialog dialog,TutorLesson lesson){
            Check(!dialog.Current.Completed,"validation has not prematurely completed "+lesson.id);int input=0;
            for(int tick=0;tick<2050;tick++){var run=Field<Interpreter>(dialog,"runner");if(run.State==RuntimeState.Completed){Check(dialog.Current.Demonstrated,"demonstration remembered "+lesson.id);return;}if(run.State==RuntimeState.Error)throw new Exception(run.Error);if(run.State==RuntimeState.WaitingForInput){Field<TextBox>(dialog,"runInput").Text=lesson.trials[0].inputs[input++%lesson.trials[0].inputs.Length];Invoke(dialog,"SubmitRun");}else Field<FlowButton>(dialog,"stepRun").PerformClick();Pump();}throw new Exception("Demonstration limit");
        }
        private static void NativeIndependentCompletion()
        {
            string folder = Path.Combine(directory, "independent-state-" + Guid.NewGuid().ToString("N")); var store = new TutorStore(folder); var lesson = Tutor.Lesson("b02");
            var state = store.Load(lesson.id, lesson.Seed()); state.Document = lesson.Solution();
            foreach (var edge in state.Document.Edges) { edge.From = "own_" + edge.From; edge.To = "own_" + edge.To; }
            foreach (var node in state.Document.Nodes) node.Id = "own_" + node.Id;
            state.Document.Nodes.First(n => n.Kind == NodeKind.Output).Text = "\"Benvenuto!\""; state.Step = 0; Check(store.Save(state), "independently structured diagram fixture persisted");
            using (var dialog = new TutorDialog(store, new LearningStore(Path.Combine(directory, "independent-legacy.json")))) {
                dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-2500, -2200); dialog.Show(); Pump(); dialog.OpenLesson(lesson.id); Field<FlowButton>(dialog, "primary").PerformClick(); Pump();
                dialog.GetType().GetField("finalRequested", Flags).SetValue(dialog, true); Invoke(dialog, "CheckCurrent"); Wait(delegate { return dialog.Feedback != null; }, "independent final check");
                FollowDemonstration(dialog,lesson);Capture(dialog,"13-independent-completion.png");Field<FlowButton>(dialog,"continueRun").PerformClick();Check(store.Peek(lesson.id).Completed&&store.Peek(lesson.id).Step==lesson.steps.Length,"direct final completes independent graph after actual demonstration"); dialog.Close();
            }
            using (var dialog = new TutorDialog(new TutorStore(folder), new LearningStore(Path.Combine(directory, "independent-legacy.json")))) {
                dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-2500, -2200); dialog.Show(); Pump(); dialog.OpenLesson(lesson.id); Field<FlowButton>(dialog, "primary").PerformClick(); Pump();
                Check(dialog.Current.Completed && dialog.Current.Step == lesson.steps.Length && dialog.Current.Document.Nodes.All(n => n.Id.StartsWith("own_")), "reopen preserves independent graph, final phase and completion"); dialog.Close();
            }
        }
        private static void ExplanationTests()
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { { "punti", 4.0 }, { "eta", 17.0 }, { "nome", "Ada" } };
            string explain = TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Process, Text = "punti = punti + 1" }, values);
            Check(explain.Contains("passa da 4 a 5") && explain.Contains("Legge punti = 4"), "actual expression explains old/new values");
            Check((double)values["punti"] == 4.0, "explanation never mutates observed state");
            explain = TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Decision, Text = "eta >= 18" }, values);
            Check(explain.Contains("falso") && explain.Contains("No"), "decision explanation uses actual threshold input");
            values["eta"] = 18.0; Check(TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Decision, Text = "eta >= 18" }, values).Contains("vero"), "decision includes boundary 18");
            Check(TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Output, Text = "nome" }, values).Contains("«Ada»"), "variable output explains value");
            Check(TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Output, Text = "\"nome\"" }, values).Contains("«nome»"), "literal output explains exact text");
            Check(TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Output, Text = "totale" }, null).Contains("serve prima un valore per totale"), "missing values are not fabricated");
            Check(TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Input, Text = "eta" }, null).Contains("Il nome non è il valore"), "input name is distinct from data");
            Check(TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Process, Text = "x = 2\ny = x * 3" }, null).Contains("y riceve 6"), "sequential assignments explained in order");
            Check(TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Output, Text = "1 / 0" }, null).Contains("Non si può") || TutorDiagnostics.Explain(new FlowNode { Kind = NodeKind.Output, Text = "1 / 0" }, null).Contains("divisore != 0"), "runtime failure includes correction");
        }
        private static void PresetTests()
        {
            foreach (var preset in Tutor.Guide.presets)
            {
                switch (preset.id) {
                    case "input-number": Preset(preset, "", "numero", new[] { "7" }, new[] { "7" }); break;
                    case "input-age": Preset(preset, "", "eta", new[] { "18" }, new[] { "18" }); break;
                    case "zero": Preset(preset, "", "contatore", new string[0], new[] { "0" }); break;
                    case "increment": Preset(preset, "contatore = 2", "contatore", new string[0], new[] { "3" }); break;
                    case "double": Preset(preset, "numero = 4", "doppio", new string[0], new[] { "8" }); break;
                    case "sum": Preset(preset, "a = 2\nb = 3", "totale", new string[0], new[] { "5" }); break;
                    case "accumulate": Preset(preset, "somma = 8\nnumero = 3", "somma", new string[0], new[] { "11" }); break;
                    case "age": Preset(preset, "eta = 17", "", new string[0], new[] { "\"no\"" }); Preset(preset, "eta = 18", "", new string[0], new[] { "\"si\"" }); break;
                    case "nonzero": Preset(preset, "numero = 0", "", new string[0], new[] { "\"no\"" }); Preset(preset, "numero = 4", "", new string[0], new[] { "\"si\"" }); break;
                    case "positive": Preset(preset, "numero = -2", "", new string[0], new[] { "\"no\"" }); Preset(preset, "numero = 0", "", new string[0], new[] { "\"no\"" }); Preset(preset, "numero = 5", "", new string[0], new[] { "\"si\"" }); break;
                    case "output-value": Preset(preset, "numero = 7", "", new string[0], new[] { "7" }); break;
                    case "output-text": Preset(preset, "", "", new string[0], new[] { "\"Ciao!\"" }); break;
                    case "end": Preset(preset, "", "", new string[0], new string[0]); break;
                    default: throw new Exception("Preset has no execution case: " + preset.id);
                }
            }
        }
        private static void Preset(TutorPreset preset, string initialize, string output, string[] inputs, string[] expected)
        {
            var d = new Diagram(); var start = new FlowNode { Kind = NodeKind.Start }; var block = new FlowNode { Kind = (NodeKind)preset.kind, Text = preset.text }; var end = new FlowNode { Kind = NodeKind.End };
            d.Nodes.Add(start); var previous = start;
            if (initialize.Length > 0) { var init = new FlowNode { Kind = NodeKind.Process, Text = initialize }; d.Nodes.Add(init); d.Edges.Add(new FlowEdge { From = previous.Id, To = init.Id }); previous = init; }
            d.Nodes.Add(block); d.Edges.Add(new FlowEdge { From = previous.Id, To = block.Id });
            if (block.Kind == NodeKind.Decision) {
                foreach (string branch in new[] { "true", "false" }) { var label = new FlowNode { Kind = NodeKind.Output, Text = branch == "true" ? "\"si\"" : "\"no\"" }; d.Nodes.Add(label); d.Edges.Add(new FlowEdge { From = block.Id, To = label.Id, Branch = branch }); d.Edges.Add(new FlowEdge { From = label.Id, To = end.Id }); }
                d.Nodes.Add(end);
            }
            else if (block.Kind != NodeKind.End) {
                previous = block; if (output.Length > 0) { var print = new FlowNode { Kind = NodeKind.Output, Text = output }; d.Nodes.Add(print); d.Edges.Add(new FlowEdge { From = block.Id, To = print.Id }); previous = print; }
                d.Nodes.Add(end); d.Edges.Add(new FlowEdge { From = previous.Id, To = end.Id });
            }
            var result = Tutor.Execute(d, new TutorTrial { inputs = inputs, outputs = expected }, 0, 1); Check(result.Pass, "preset execution " + preset.id + ": " + result.Text);
        }
        private static void Storage()
        {
            string folder = Path.Combine(directory, "state-" + Guid.NewGuid().ToString("N")); var store = new TutorStore(folder); var lesson = Tutor.Lesson("b02");
            var state = store.Load(lesson.id, lesson.Seed()); state.Document = lesson.Solution(); state.Step = 1; state.Drafts.Add(new TutorDraft { Node = "messaggio", Text = "\"ciao\"", Prompt = "" });
            Check(store.Save(state), "native storage saves document and unapplied draft");
            var childStart = new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--check-persist \"" + folder + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
            using (var child = System.Diagnostics.Process.Start(childStart)) { Check(child.WaitForExit(15000) && child.ExitCode == 0, "fresh process reopens document, phase and draft"); }
            var read = new TutorStore(folder).Load(lesson.id, lesson.Seed()); Check(read.Step == 1 && read.Drafts[0].Text == "\"ciao\"", "new store restores pending draft and phase");
            state.Completed = true; Check(store.Save(state) && new TutorStore(folder).CompletedCount() == 1, "completed progress saved once");
            File.WriteAllText(Path.Combine(folder, "b02.json"), "broken"); var recovered = new TutorStore(folder); read = recovered.Load("b02", lesson.Seed()); Check(recovered.Error != null && read.Step == 1, "valid backup recovers corrupt primary");
            Check(recovered.Save(read), "recovered session can be saved");
            File.WriteAllText(Path.Combine(folder, "b02.json"), "{\"Version\":2}"); bool blocked = false; try { new TutorStore(folder).Load("b02", lesson.Seed()); } catch (IOException) { blocked = true; } Check(blocked && File.ReadAllText(Path.Combine(folder, "b02.json")).Contains(":2"), "future version protected");
            string locked = Path.Combine(directory, "not-a-folder-" + Guid.NewGuid().ToString("N")); File.WriteAllText(locked, "keep"); var denied = new TutorStore(locked); Check(!denied.Save(state) && File.ReadAllText(locked) == "keep", "save failure preserves existing data");
        }
        private static void NativeJourney()
        {
            var store = new TutorStore(Path.Combine(directory, "native-state-" + Guid.NewGuid().ToString("N")));
            using (var dialog = new TutorDialog(store, new LearningStore(Path.Combine(directory, "legacy-progress.json"))))
            {
                dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-2500, -2200); dialog.Show(); Pump(); Capture(dialog, "01-home.png");
                foreach (var lesson in Tutor.Lessons)
                {
                    Console.WriteLine("NATIVE " + lesson.id); Console.Out.Flush();
                    dialog.SelectLesson(lesson.id); Pump(); if (lesson.id == "b02") Capture(dialog, "02-lesson-intro.png");
                    Field<FlowButton>(dialog, "primary").PerformClick(); Pump();
                    for (int i = 0; i < lesson.steps.Length; i++)
                    {
                        var step = lesson.steps[i];
                        Console.WriteLine("STEP " + lesson.id + "/" + i + " primary=" + Field<FlowButton>(dialog, "primary").Text + " enabled=" + Field<FlowButton>(dialog, "primary").Enabled + " phase=" + dialog.Current.Step); Console.Out.Flush();
                        if (step.action == Tutor.Add) {
                            if (lesson.id == "b02") {
                                Choose(delegate { Field<FlowButton>(dialog, "primary").PerformClick(); }, Ui.KindName(NodeKind.Process));
                                Field<FlowButton>(dialog, "primary").PerformClick(); Wait(delegate { return dialog.Feedback != null; }, "wrong shape feedback");
                                Check(!dialog.Feedback.Pass && dialog.Feedback.Code == "block-kind" && dialog.Current.Step == i, "wrong shape explains and retains phase"); Capture(dialog, "09-wrong-shape.png");
                                Check(Field<TutorResultBand>(dialog,"resultBand").Visible && Field<TutorResultBand>(dialog,"resultBand").AccessibleName.Contains("Da correggere"),"incorrect step has a visible explicit verdict");
                                Choose(delegate { Click(dialog, "Correggi questo passo"); }, Ui.KindName(NodeKind.Output));
                            }
                            else Choose(delegate { Field<FlowButton>(dialog, "primary").PerformClick(); }, Ui.KindName((NodeKind)lesson.Part(step.node).kind));
                        }
                        if (step.action == Tutor.Write) {
                            dialog.Canvas.SelectNode(step.node); Pump(); var box = Field<TextBox>(dialog, "expression"); box.Text = lesson.id == "b02" ? "\"ciao\"" : lesson.Part(step.node).answer;
                            if (lesson.id == "b02") Capture(dialog, "03-writing-lowercase.png");
                            if (lesson.id == "b15") {
                                box.Text = "!0"; Field<FlowButton>(dialog, "primary").PerformClick(); Wait(delegate { return dialog.Feedback != null; }, "wrong logic feedback");
                                Check(!dialog.Feedback.Pass && dialog.Feedback.Text.Contains("!0") && dialog.Current.Step == i, "real wrong negation explains student's error"); Capture(dialog, "10-logic-error.png");
                                bool explained = false;
                                using (var timer = new System.Windows.Forms.Timer { Interval = 15 }) {
                                    timer.Tick += delegate { var report = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Perché questa prova fallisce"); if (report == null) return;
                                        var content = report.Controls.OfType<TextBox>().First(); explained = content.Text.Contains("Decisione") && content.Text.Contains("!0") && content.Text.Contains("Ultimi passaggi") && content.Lines.Length > 12 && content.SelectionLength == 0; Capture(report, "12-actual-failed-route.png"); Click(report, "Ho capito"); };
                                    timer.Start(); Click(dialog, "Capisci questa prova"); timer.Stop();
                                }
                                Check(explained, "native error details show the actual executed route and learner expression");
                                Click(dialog, "Correggi questo passo"); Field<TextBox>(dialog, "expression").Text = lesson.Part(step.node).answer;
                            }
                        }
                        if (step.action == Tutor.Value) Field<TextBox>(dialog, "runtimeValue").Text = step.hint;
                        if (step.action == Tutor.ConnectAction) {
                            var from = Tutor.Find(dialog.Current.Document, step.a); var to = Tutor.Find(dialog.Current.Document, step.b);
                            var choices = new List<string> { NodeName(from) }; if (from.Kind == NodeKind.Decision) choices.Add(step.branch == "true" ? "Sì / vero" : "No / falso"); choices.Add(NodeName(to));
                            Choose(delegate { Field<FlowButton>(dialog, "primary").PerformClick(); }, choices.ToArray());
                        }
                        Field<FlowButton>(dialog, "primary").PerformClick(); Wait(delegate { return dialog.Feedback != null; }, "native step check " + lesson.id + "/" + i);
                        Check(dialog.Feedback.Pass, "native step " + lesson.id + "/" + i + ": " + dialog.Feedback.Text);
                        Check(Field<TutorResultBand>(dialog,"resultBand").Visible && Field<TutorResultBand>(dialog,"resultBand").AccessibleName.Contains("Passo corretto"),"each verified step has an explicit visible success");
                        if (lesson.id == "b02" && step.action == Tutor.Write) {
                            Field<TextBox>(dialog, "expression").Text = "\"CIAO\"";
                            Check(dialog.Feedback == null && Field<FlowButton>(dialog, "primary").Text == "Controlla questo passo", "editing invalidates old success without advancing");
                            Check(!Field<TutorResultBand>(dialog,"resultBand").Visible,"editing removes the prior success banner");
                            Field<TextBox>(dialog, "expression").Text = "\"ciao\""; Field<FlowButton>(dialog, "primary").PerformClick(); Wait(delegate { return dialog.Feedback != null; }, "recheck edited message"); Check(dialog.Feedback.Pass, "edited message can be checked again");
                        }
                        Field<FlowButton>(dialog, "primary").PerformClick(); Pump(); Check(dialog.Current.Step == i + 1, "native phase advances only after success");
                    }
                    Field<FlowButton>(dialog, "primary").PerformClick(); Wait(delegate { return dialog.Feedback != null; }, "native final " + lesson.id);
                    Check(dialog.Feedback.Pass,"native final checks student graph "+lesson.id);FollowDemonstration(dialog,lesson);
                    Check(Field<FlowButton>(dialog,"continueRun").Visible && Field<TutorResultBand>(dialog,"resultBand").AccessibleName.Contains("Flowchart corretto"),"lesson completion has an explicit final verdict");
                    if (lesson.id == "b02") Capture(dialog, "04-lowercase-accepted.png");
                    if (lesson.id == "b24") Capture(dialog, "05-full-flowchart.png");
                    Field<FlowButton>(dialog,"continueRun").PerformClick();Check(store.Peek(lesson.id).Completed,"native lesson completion is saved after demonstration "+lesson.id);
                }
                Check(store.CompletedCount() == 24, "all native lesson completions persisted");
                Console.WriteLine("NATIVE all complete; pending draft scenario");
                dialog.OpenLesson("b02"); Field<FlowButton>(dialog, "primary").PerformClick(); Pump(); dialog.Canvas.SelectNode("messaggio"); Field<TextBox>(dialog, "expression").Text = "\"Bozza da riprendere\"";
                dialog.Close(); Pump();
            }
            using (var dialog = new TutorDialog(store, new LearningStore(Path.Combine(directory, "legacy-progress.json")))) {
                Console.WriteLine("NATIVE restarting form");
                dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-2500, -2200); dialog.Show(); Pump(); dialog.OpenLesson("b02"); Field<FlowButton>(dialog, "primary").PerformClick(); Pump(); dialog.Canvas.SelectNode("messaggio");
                Check(Field<TextBox>(dialog, "expression").Text == "\"Bozza da riprendere\"", "actual UI restores unapplied draft after form restart");
                dialog.OpenWorkshop(); Pump(); Capture(dialog, "06-workshop.png");
                Choose(delegate { Click(dialog, "Aggiungi"); }, "Chiedi un numero", "Inizio · Inizio");
                Check(dialog.Current.Document.Nodes.Any(n => n.Kind == NodeKind.Input && n.Text == "numero"), "preset inserted in native workshop");
                var input = dialog.Current.Document.Nodes.First(n => n.Kind == NodeKind.Input); dialog.Canvas.SelectNode(input.Id); Field<TextBox>(dialog, "prompt").Text = "Quale numero scegli?"; Click(dialog, "Applica al blocco");
                Check(input.Prompt == "Quale numero scegli?", "prompt stored separately from variable name");
                Choose(delegate { Click(dialog, "Aggiungi"); }, "Mostra il valore", NodeName(input));
                Check(!Validator.Validate(dialog.Current.Document).Any(x => x.Severity == Severity.Error), "workshop preset chain is executable");
                var output = dialog.Current.Document.Nodes.First(n => n.Kind == NodeKind.Output); dialog.Canvas.SelectNode(output.Id); Field<TextBox>(dialog, "expression").Text = "numero + 0";
                dialog.Canvas.SelectNode(input.Id); RenameInNativeDialog(dialog, "numero", "valore");
                Check(dialog.Current.Document.Nodes.Any(n => n.Kind == NodeKind.Input && n.Text == "valore") && dialog.Current.Document.Nodes.Any(n => n.Kind == NodeKind.Output && n.Text == "valore") && dialog.Current.Drafts.Any(d => d.Text == "valore + 0"), "rename updates real boxes and pending draft together");
                Invoke(dialog, "Undo"); Check(dialog.Current.Document.Nodes.Any(n => n.Kind == NodeKind.Input && n.Text == "numero") && dialog.Current.Drafts.Any(d => d.Text == "numero + 0"), "undo restores names and draft atomically");
                Invoke(dialog, "Redo"); Check(dialog.Current.Document.Nodes.Any(n => n.Kind == NodeKind.Input && n.Text == "valore") && dialog.Current.Drafts.Any(d => d.Text == "valore + 0"), "redo restores renamed boxes and draft");
                dialog.Canvas.SelectNode(output.Id); Click(dialog, "Applica al blocco");
                Field<TabControl>(dialog, "inspectorTabs").SelectedIndex = 1;
                Invoke(dialog, "StepRunner"); Invoke(dialog, "StepRunner");
                var runner = Field<Interpreter>(dialog, "runner"); Check(runner.State == RuntimeState.WaitingForInput && !Field<FlowButton>(dialog, "stepRun").Enabled, "waiting input disables step");
                Field<TextBox>(dialog, "runInput").Text = "7"; Click(dialog, "Invia dato");
                for (int n = 0; n < 5 && runner.State != RuntimeState.Completed; n++) Invoke(dialog, "StepRunner");
                Check(runner.State == RuntimeState.Completed && runner.Output.SequenceEqual(new[] { "7" }) && !Field<FlowButton>(dialog, "stepRun").Enabled, "real native runner ends with output 7");
                Check(Field<TextBox>(dialog, "runInfo").Text.Contains("ferma qui"), "step execution explains the actual terminal");
                Capture(dialog, "07-real-run.png");
                Ui.SetDarkMode(true, false); Pump(); dialog.Size = new Size(1050, 720); Pump(); Click(dialog, "Tutto"); Capture(dialog, "08-dark-minimum.png");
                Check(Object.ReferenceEquals(runner, Field<Interpreter>(dialog, "runner")), "theme changes preserve simulation state");
                Check(!Field<FlowButton>(dialog, "primary").Visible, "verification is hidden during trial at minimum window size");
                Field<TabControl>(dialog, "inspectorTabs").SelectedIndex = 0; Pump();
                Check(Field<FlowButton>(dialog, "primary").Visible && Field<FlowButton>(dialog, "primary").Bottom <= Field<FlowButton>(dialog, "primary").Parent.ClientSize.Height, "returning to Scrivi restores primary within minimum window");
                Ui.SetDarkMode(false, false); dialog.Close();
            }
            using (var main = new MainForm(null, true)) {
                main.ShowInTaskbar = false; main.StartPosition = FormStartPosition.Manual; main.Location = new Point(-2500, -2200); main.Show(); Pump(); main.EnterEditor(); main.Canvas.AddNode(NodeKind.Process); var original = main.Document;
                bool seen = false;
                using (var timer = new System.Windows.Forms.Timer { Interval = 20 }) {
                    timer.Tick += delegate { var tutor = Application.OpenForms.Cast<Form>().OfType<TutorDialog>().FirstOrDefault(); if (tutor != null) { seen = true; tutor.Close(); } };
                    timer.Start(); Invoke(main, "ShowLearning"); timer.Stop();
                }
                Check(seen && Object.ReferenceEquals(original, main.Document), "actual Impara entry opens new tutor and preserves dirty editor");
                main.SetDocument(DiagramIO.Clone(original), null, false); main.Close();
            }
            NativeIndependentCompletion();
        }
        [STAThread] private static int Main(string[] args)
        {
            try {
                if (args[0] == "--check-persist") { var state = new TutorStore(args[1]).Load("b02", Tutor.Lesson("b02").Seed()); return state.Step == 1 && state.Drafts.Count == 1 && state.Drafts[0].Text == "\"ciao\"" && state.Document.Nodes.Any(n => n.Id == "messaggio") ? 0 : 1; }
                directory = args[0]; Directory.CreateDirectory(directory); Application.EnableVisualStyles(); Ui.SetDarkMode(false, false); TutorFeedbackSignals.SetEnabled(false); Model(); Storage(); NativeJourney(); Console.WriteLine("PASS " + checks + " checks; " + walked + " construction steps; " + cases + " goal cases (71 authored and 293 independently generated); 24 lessons completed through actual WinForms controls. External human usability: NOT_RUN."); return 0; }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
        }
    }
}

