using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Frociart
{
    // These fields are populated by DataContractJsonSerializer from embedded resources.
#pragma warning disable 0649
    [DataContract] internal sealed class TutorPart
    {
        [DataMember] public string id, answer, initial;
        [DataMember] public int kind;
        [DataMember] public bool present;
    }
    [DataContract] internal sealed class TutorLink
    {
        [DataMember] public string a, b, branch;
        [DataMember] public bool present;
    }
    [DataContract] internal sealed class TutorStep
    {
        [DataMember] public int action;
        [DataMember] public string node, title, instruction, hint, a, branch, b;
    }
    [DataContract] internal sealed class TutorTrial
    {
        [DataMember] public string[] inputs, outputs;
    }
    [DataContract] internal sealed class TutorLesson
    {
        [DataMember] public string id, unit, title, goal, explanation, reading, example, warning, assessment, criteria;
        [DataMember] public string[] terms, introduces, variables, skills;
        [DataMember] public TutorPart[] nodes;
        [DataMember] public TutorLink[] edges;
        [DataMember] public TutorStep[] steps;
        [DataMember] public TutorTrial[] trials;
        [DataMember] public bool guided;
        internal TutorPart Part(string key) { return nodes.FirstOrDefault(p => p.id == key); }
        internal Diagram Seed() { return Create(false); }
        internal Diagram Solution() { return Create(true); }
        private Diagram Create(bool complete)
        {
            var diagram = new Diagram { Title = title };
            foreach (var part in nodes.Where(p => complete || p.present))
                diagram.Nodes.Add(new FlowNode { Id = part.id, Kind = (NodeKind)part.kind,
                    Text = complete ? part.answer : part.initial });
            foreach (var link in edges.Where(e => complete || e.present))
                if (Tutor.Find(diagram, link.a) != null && Tutor.Find(diagram, link.b) != null)
                    diagram.Edges.Add(new FlowEdge { From = link.a, To = link.b, Branch = link.branch });
            Tutor.Arrange(diagram);
            return diagram;
        }
    }
    [DataContract] internal sealed class TutorTerm
    {
        [DataMember] public string id, aliases, title, explanation, example, warning;
    }
    [DataContract] internal sealed class TutorPreset
    {
        [DataMember] public string id, title, text, explanation, needs, example;
        [DataMember] public int kind;
    }
    [DataContract] internal sealed class TutorGuide
    {
        [DataMember] public TutorTerm[] terms;
        [DataMember] public TutorPreset[] presets;
    }
#pragma warning restore 0649
    internal sealed class TutorFeedback
    {
        internal bool Pass;
        internal string Code, Title, Why, Fix, NodeId;
        internal int Passed, Total;
        internal HashSet<string> Executed;
        internal string Trace;
        internal string Text { get { return Why + (String.IsNullOrWhiteSpace(Fix) ? "" : Pass ? "\n\nCome continuare\n" + Fix : "\n\nCome correggere\n" + Fix); } }
    }
    internal static class Tutor
    {
        internal const int Add = 0, Write = 1, ConnectAction = 2, Value = 3;
        internal static readonly TutorLesson[] Lessons = Read<TutorLesson[]>("curriculum");
        internal static readonly TutorGuide Guide = Read<TutorGuide>("guide");
        internal static readonly string[] Units = { "Il foglio ti risponde", "Memoria e dati", "Calcola con i numeri",
            "Fai domande chiare", "Scegli e ripeti", "Metti insieme i blocchi" };
        private static T Read<T>(string resource)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Frociart.Tutor." + resource + ".json"))
            {
                if (stream == null) throw new InvalidDataException("Manca il contenuto del percorso: " + resource);
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }
        internal static TutorFeedback No(string code, string title, string why, string fix, string node = null)
        { return new TutorFeedback { Code = code, Title = title, Why = why, Fix = fix, NodeId = node }; }
        internal static TutorFeedback Yes(string title, string why)
        { return new TutorFeedback { Pass = true, Code = "ok", Title = title, Why = why, Fix = "" }; }
        internal static TutorLesson Lesson(string id) { return Lessons.FirstOrDefault(l => l.id == id); }
        internal static FlowNode Find(Diagram d, string id) { return d.Nodes.FirstOrDefault(n => n.Id == id); }
        internal static FlowEdge Outgoing(Diagram d, string id, string branch)
        { return d.Edges.FirstOrDefault(e => e.From == id && e.Branch == branch); }
        internal static string Type(object value) { return Values.Numeric(value) ? "numero" : value is string ? "testo" : "vero/falso"; }
        internal static object Literal(string text)
        { return ExpressionLanguage.Evaluate(text, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)); }
        internal static bool Same(object a, object b)
        {
            if (Type(a) != Type(b)) return false;
            if (Values.Numeric(a)) return Math.Abs(Values.Number(a) - Values.Number(b)) <= 1e-10 * Math.Max(1, Math.Abs(Values.Number(b)));
            return Object.Equals(a, b);
        }
        internal static bool FreeMessage(TutorLesson lesson) { return lesson != null && lesson.assessment == "message"; }
        internal static bool LabelOutput(TutorLesson lesson)
        { return lesson != null && lesson.assessment == "labels"; }
        internal static bool Matches(TutorLesson lesson, object actual, object expected)
        {
            if (FreeMessage(lesson)) return actual is string && !String.IsNullOrWhiteSpace((string)actual);
            if (LabelOutput(lesson) && actual is string && expected is string)
                return SameLabel(actual, expected);
            return Same(actual, expected);
        }
        internal static string Criteria(TutorLesson lesson)
        {
            return lesson.criteria + "\nProve previste: " + lesson.trials.Length + ".";
        }
        internal static void Arrange(Diagram d)
        {
            FlowLayout.Arrange(d);
        }
        internal static bool Insert(TutorLesson lesson, Diagram d, string target, NodeKind kind)
        {
            var part = lesson.Part(target);
            if (part == null || Find(d, target) != null || d.Nodes.Count >= DiagramIO.MaxNodes) return false;
            d.Nodes.Add(new FlowNode { Id = target, Kind = kind,
                Text = kind == NodeKind.Start ? "Inizio" : kind == NodeKind.End ? "Fine" : "" });
            if (lesson.guided)
                foreach (var edge in lesson.edges.Where(e => e.a == target || e.b == target))
                    if (Find(d, edge.a) != null && Find(d, edge.b) != null && Outgoing(d, edge.a, edge.branch) == null)
                        d.Edges.Add(new FlowEdge { From = edge.a, To = edge.b, Branch = edge.branch });
            Arrange(d); return true;
        }
        internal static bool Connect(Diagram d, string from, string branch, string to)
        {
            var a = Find(d, from); var b = Find(d, to);
            if (a == null || b == null || a == b || a.Kind == NodeKind.End || b.Kind == NodeKind.Start) return false;
            if (a.Kind == NodeKind.Decision ? branch != "true" && branch != "false" : branch != "next") return false;
            var old = Outgoing(d, from, branch);
            if (old != null && old.To == to) return false;
            if (old != null) d.Edges.Remove(old);
            d.Edges.Add(new FlowEdge { From = from, To = to, Branch = branch }); return true;
        }
        internal static int RecoverStep(TutorLesson lesson, int requested, Diagram d)
        {
            int count = Math.Max(0, Math.Min(requested, lesson.steps.Length));
            for (int i = 0; i < count; i++)
            {
                var step = lesson.steps[i]; var node = Find(d, step.node); var part = lesson.Part(step.node);
                if (step.action == Add && (node == null || (int)node.Kind != part.kind)) return i;
                if (step.action == Write && (node == null || String.IsNullOrWhiteSpace(node.Text))) return i;
                if (step.action == ConnectAction)
                { var edge = Outgoing(d, step.a, step.branch); if (edge == null || edge.To != step.b) return i; }
            }
            return count;
        }
        internal static TutorFeedback InputRule(TutorLesson lesson, string variable, string raw)
        {
            object value;
            try { value = Values.Input(raw); } catch (FlowException ex) { return TutorDiagnostics.Runtime(ex.Message, null); }
            if (lesson == null) return null;
            foreach (string role in lesson.variables)
            {
                string[] cells = role.Split('|');
                if (cells.Length < 3 || !String.Equals(cells[0], variable, StringComparison.OrdinalIgnoreCase)) continue;
                string expected = cells[2];
                if (expected.StartsWith("numero") && !Values.Numeric(value)) return No("input-type", "Questo esercizio chiede un numero",
                    "Il dato inserito viene letto come " + Type(value) + ", non come numero.", "Scrivi un valore come 5, senza nome della variabile, operazioni o virgolette.");
                if (expected == "testo" && !(value is string)) return No("input-type", "Qui proviamo un testo",
                    "Il dato inserito viene riconosciuto come " + Type(value) + ".", "Per questo esempio scrivi un nome come Ada, senza virgolette.");
                if (Values.Numeric(value) && expected.Contains("intero") && Values.Number(value) != Math.Floor(Values.Number(value)))
                    return No("input-integer", "Serve un intero", "Un intero non ha una parte decimale. La missione è definita per gli interi.", "Prova 0, 1, 2, 3 o gli altri interi consentiti dalla missione.");
                if (Values.Numeric(value) && expected.Contains("non negativo") && Values.Number(value) < 0)
                    return No("input-range", "Qui il valore parte da zero", "La missione richiede un valore non negativo: zero o maggiore.", "Scegli 0 oppure un numero positivo, come 3.");
            }
            return null;
        }
        internal static TutorFeedback CheckStep(TutorLesson lesson, int index, Diagram source, string runtimeValue)
        {
            if (index >= lesson.steps.Length) return Check(lesson, source);
            var step = lesson.steps[index]; var part = lesson.Part(step.node); var node = Find(source, step.node);
            if (step.action == Value)
            {
                var rule = InputRule(lesson, part == null ? "" : part.answer, runtimeValue);
                if (rule != null) { rule.NodeId = step.node; return rule; }
                object expected = Values.Input(runtimeValue);
                string literal = expected is string ? "\"" + ((string)expected).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : ValueFormatter.Format(expected);
                return Execute(source, new TutorTrial { inputs = new[] { runtimeValue }, outputs = new[] { literal } }, 0, 1);
            }
            if (step.action == ConnectAction)
            {
                var edge = Outgoing(source, step.a, step.branch);
                return edge != null && edge.To == step.b ? Yes("Freccia collegata", "Il percorso va dal blocco di partenza all'arrivo scelto.") :
                    No("connection", "Manca questa freccia", "Le caselle vicine non sono automaticamente collegate.", step.instruction, step.a);
            }
            if (node == null) return No("missing-block", "Aggiungi il blocco", "Questo passaggio deve esistere nel tuo diagramma.", step.instruction, step.node);
            if (part == null || (int)node.Kind != part.kind) return No("block-kind", "Questo blocco ha un altro compito",
                "Hai scelto " + Ui.KindName(node.Kind) + "; per questo passo serve " + Ui.KindName((NodeKind)part.kind) + ".", step.instruction, node.Id);
            if (step.action == Add) return Yes("Blocco aggiunto", lesson.guided ? "La guida lo ha agganciato al punto indicato. Il contenuto lo scriverai tu nel passo successivo." : "Ora puoi scrivere il contenuto e collegare le frecce richieste.");
            var syntax = TutorDiagnostics.Before(node); if (syntax != null) return syntax;
            if (node.Kind == NodeKind.Input) return Yes("Nome valido", "Il blocco conserverà il dato in " + node.Text.Trim() + ". Usa lo stesso nome nelle caselle che leggono questo dato. Rinomina variabile aggiorna gli usi senza modificare i messaggi.");
            if (!Validator.Validate(source).Any(i => i.Severity == Severity.Error) && Check(lesson, source).Pass)
                return Yes("Il tuo percorso funziona", "Ho eseguito il tuo foglio così com'è su " + lesson.trials.Length + " casi. Questo controllo non richiede che tu copi il mio esempio.");
            // Check only the submitted statement in the disclosed reference context.
            var reference = StatementContext(lesson, source, node);
            var result = Check(lesson, reference);
            if (result.Pass) return Yes("Istruzione verificata", "Ho provato la tua casella nel resto dell'esempio già pronto, usando " + lesson.trials.Length + " dati di prova. Non ho completato le altre caselle sul tuo foglio. La prova finale eseguirà tutto il tuo flowchart.");
            result.NodeId = node.Id; result.Why = "Controllo della tua casella nel contesto preparato dell'esempio.\n" + result.Why;
            if (result.Trace != null) result.Trace = result.Trace.Replace("del tuo diagramma", "dell'esempio con la tua casella");
            result.Fix = step.hint + "\n" + result.Fix; return result;
        }
        internal static TutorFeedback Check(TutorLesson lesson, Diagram source)
        {
            foreach (var node in source.Nodes) { var syntax = TutorDiagnostics.Before(node); if (syntax != null) return syntax; }
            foreach (var issue in Validator.Validate(source).Where(i => i.Severity == Severity.Error))
            {
                return TutorDiagnostics.Issue(issue);
            }
            var requirement = CheckSkills(lesson, source); if (requirement != null) return requirement;
            int passed = 0; TutorFeedback firstFailure = null;
            foreach (var trial in lesson.trials)
            {
                var result = Execute(source, trial, passed, lesson.trials.Length, lesson);
                if (!result.Pass)
                {
                    if (firstFailure == null) firstFailure = result;
                }
                else passed++;
            }
            if (firstFailure != null) {
                firstFailure.Passed = passed; firstFailure.Total = lesson.trials.Length;
                firstFailure.Why = passed + " prove superate su " + lesson.trials.Length + ". Il foglio e i passi completati restano al loro posto.\n\nUn caso da correggere:\n" + firstFailure.Why;
                foreach (var node in source.Nodes.Where(n => firstFailure.Executed != null && firstFailure.Executed.Contains(n.Id))) { string hint = TutorDiagnostics.Misconception(lesson, node); if (hint.Length > 0) firstFailure.Fix += "\n\nRegola da rileggere in «" + node.Text + "»: " + hint; }
                return firstFailure;
            }
            return new TutorFeedback { Pass = true, Code = "complete", Title = "Flowchart completato", Why = passed + " casi superati sul tuo diagramma. Hai costruito un percorso eseguibile.", Fix = "Puoi provare altri valori con Prova passo passo.", Passed = passed, Total = lesson.trials.Length };
        }
        private static Diagram StatementContext(TutorLesson lesson, Diagram actual, FlowNode submitted)
        {
            var context = lesson.Solution(); var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in lesson.nodes) {
                var original = Find(context, part.id); var node = Find(actual, part.id);
                if (node == null || node.Kind != original.Kind) continue;
                try { var before = Compiler.Block(original); var after = Compiler.Block(node);
                    if (node.Kind == NodeKind.Input) aliases[before.InputVariable] = after.InputVariable;
                    else if (node.Kind == NodeKind.Process) {
                        var a = before.Assignments.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); var b = after.Assignments.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        if (a.Length == b.Length) for (int i = 0; i < a.Length; i++) aliases[a[i]] = b[i];
                    }
                } catch (FlowException) { }
            }
            foreach (var node in context.Nodes.Where(n => n.Kind != NodeKind.Start && n.Kind != NodeKind.End)) node.Text = FlowDeclaration.RewriteNode(node, aliases);
            Find(context, submitted.Id).Text = submitted.Text; return context;
        }
        internal static string CanonicalInput(TutorLesson lesson, Diagram document, string variable)
        {
            if (lesson == null) return variable;
            var node = document.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Input && String.Equals(n.Text.Trim(), variable, StringComparison.OrdinalIgnoreCase));
            var part = node == null ? null : lesson.Part(node.Id);
            if (part != null) return part.answer;
            var inputs = lesson.nodes.Where(n => n.kind == (int)NodeKind.Input).ToArray();
            return inputs.Length == 1 ? inputs[0].answer : variable;
        }
        private static TutorFeedback CheckSkills(TutorLesson lesson, Diagram document)
        {
            var reached = new HashSet<string>(); var queue = new Queue<string>();
            foreach (var node in document.Nodes.Where(n => n.Kind == NodeKind.Start)) { reached.Add(node.Id); queue.Enqueue(node.Id); }
            while (queue.Count > 0) { string id = queue.Dequeue(); foreach (var edge in document.Edges.Where(e => e.From == id)) if (reached.Add(edge.To)) queue.Enqueue(edge.To); }
            var nodes = document.Nodes.Where(n => reached.Contains(n.Id)).ToList();
            foreach (string skill in lesson.skills ?? new string[0]) {
                bool found = false; string description = skill;
                if (skill == "input") { found = nodes.Any(n => n.Kind == NodeKind.Input); description = "leggere i dati con Input"; }
                if (skill == "output") { found = nodes.Any(n => n.Kind == NodeKind.Output); description = "mostrare un risultato con Output"; }
                if (skill == "decision") { found = nodes.Any(n => n.Kind == NodeKind.Decision); description = "scegliere il percorso con Decisione"; }
                if (skill == "assignment") { found = nodes.Any(n => n.Kind == NodeKind.Process && Compiler.Block(n).Assignments.Count > 0); description = "conservare un calcolo in una variabile"; }
                if (skill == "variable-output") { found = nodes.Any(n => n.Kind == NodeKind.Output && ExpressionLanguage.GetVariables(n.Text).Count > 0); description = "leggere una variabile nell'Output"; }
                if (skill == "function") { found = nodes.Any(n => System.Text.RegularExpressions.Regex.IsMatch(TutorDiagnostics.WithoutStrings(n.Text), @"\b(abs|sqrt|min|max|floor|round|len)\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase)); description = "usare una funzione disponibile"; }
                if (skill == "cycle") {
                    var degree = nodes.ToDictionary(n => n.Id, n => 0); foreach (var edge in document.Edges.Where(e => reached.Contains(e.From) && reached.Contains(e.To))) degree[edge.To]++;
                    var ready = new Queue<string>(degree.Where(p => p.Value == 0).Select(p => p.Key)); int removed = 0;
                    while (ready.Count > 0) { string id = ready.Dequeue(); removed++; foreach (var edge in document.Edges.Where(e => e.From == id && degree.ContainsKey(e.To))) if (--degree[edge.To] == 0) ready.Enqueue(edge.To); }
                    found = removed < nodes.Count; description = "costruire una ripetizione con un ciclo raggiungibile da Inizio";
                }
                if (!found) return No("learning-goal", "Manca una parte dell'obiettivo", "Questa lezione chiede anche di " + description + ".", "Puoi scegliere nomi, formule e disposizione diversi, ma usa la competenza indicata nella missione.");
            }
            return null;
        }
        internal static TutorFeedback Execute(Diagram source, TutorTrial trial, int passed, int total, TutorLesson assessment = null)
        {
            var runner = new Interpreter(source, 2000); var actual = new List<object>(); int input = 0;
            var outputNodes = new List<string>(); var visited = new HashSet<string>(); var trace = new Queue<string>();
            var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); string lastDecision = null, focus = null;
            string wrongNode = null; IDictionary<string, object> wrongValues = null;
            while (runner.State != RuntimeState.Completed && runner.State != RuntimeState.Error)
            {
                var current = Find(source, runner.CurrentNodeId); string executed = current == null ? "" : Ui.KindName(current.Kind) + " «" + Snippet(current.Text) + "»";
                if (runner.State == RuntimeState.WaitingForInput)
                {
                    if (input >= trial.inputs.Length) return WithEvidence(No("extra-input", "Chiedi un dato di troppo", Context(trial), "Controlla quanti Input vengono attraversati, anche nei cicli.", runner.CurrentNodeId), trace, visited, passed, total);
                    runner.ProvideInput(trial.inputs[input++]);
                }
                else
                {
                    if (current != null && current.Kind == NodeKind.Output)
                        try {
                            object value = ExpressionLanguage.Evaluate(current.Text, runner.Variables); int index = actual.Count;
                            if (wrongNode == null && (index >= trial.outputs.Length || !Matches(assessment, value, Literal(trial.outputs[index])))) {
                                wrongNode = current.Id; focus = current.Id; wrongValues = new Dictionary<string, object>(runner.Variables, StringComparer.OrdinalIgnoreCase);
                                foreach (string name in ExpressionLanguage.GetVariables(current.Text)) { string writer; if (origins.TryGetValue(name, out writer)) { focus = writer; break; } }
                                if (focus == current.Id && lastDecision != null && LabelOutput(assessment) && value is string && assessment.trials.SelectMany(t => t.outputs).Any(t => SameLabel(value, Literal(t)))) focus = lastDecision;
                            }
                            actual.Add(value); outputNodes.Add(current.Id);
                        } catch (FlowException) { }
                    runner.Step();
                }
                if (current != null) visited.Add(current.Id);
                if (current != null && runner.State != RuntimeState.Error && runner.State != RuntimeState.WaitingForInput) {
                    if (current.Kind == NodeKind.Process || current.Kind == NodeKind.Input) foreach (string name in Compiler.Block(current).Definitions()) origins[name] = current.Id;
                    if (current.Kind == NodeKind.Decision) lastDecision = current.Id;
                }
                if (runner.State != RuntimeState.WaitingForInput) { trace.Enqueue(executed + " → " + Snippet(runner.LastMessage)); if (trace.Count > 12) trace.Dequeue(); }
            }
            if (runner.State == RuntimeState.Error) {
                var failure = TutorDiagnostics.Runtime(runner.Error, runner.CurrentNodeId);
                failure.Why = Context(trial) + "\n" + failure.Why; return WithEvidence(failure, trace, visited, passed, total);
            }
            if (input != trial.inputs.Length) return WithEvidence(No("missing-input", "Un dato non viene letto", Context(trial), "Attraversa tutti gli Input richiesti, nello stesso ordine."), trace, visited, passed, total);
            bool good = actual.Count == trial.outputs.Length;
            if (good) for (int i = 0; i < actual.Count; i++) if (!Matches(assessment, actual[i], Literal(trial.outputs[i]))) { good = false; break; }
            if (!good) return WithEvidence(new TutorFeedback { Code = "wrong-result", Title = "Questo caso dà un risultato diverso", NodeId = focus ?? wrongNode ?? outputNodes.LastOrDefault(),
                Why = Context(trial) + "\nAtteso: " + (FreeMessage(assessment) ? "un messaggio di testo non vuoto, a tua scelta" : Describe(trial.outputs.Select(Literal))) + "\nOttenuto: " + Describe(actual),
                Fix = (wrongNode == null ? "" : TutorDiagnostics.Explain(Find(source, wrongNode), wrongValues) + "\n\n") + (focus != null && focus != wrongNode ? "Casella evidenziata: " + Ui.KindName(Find(source, focus).Kind) + " «" + Find(source, focus).Text + "». È l'ultima scrittura del dato letto oppure l'ultima Decisione prima del risultato.\n\n" : "") + "Segui Prova un passo alla volta con questi dati. Controlla dove il valore o il ramo cambia rispetto alla missione.", Passed = passed, Total = total }, trace, visited, passed, total);
            return new TutorFeedback { Pass = true, Code = "case", Title = "Caso superato", Why = Context(trial), Fix = "", Passed = passed + 1, Total = total };
        }
        private static string Context(TutorTrial trial) { return "Input della prova: " + (trial.inputs.Length == 0 ? "nessuno" : String.Join(" · ", trial.inputs)); }
        private static string LabelKey(string text) { return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim().TrimEnd('.', '!', '?', ',', ';', ':').TrimEnd(); }
        private static bool SameLabel(object actual, object expected) { return actual is string && expected is string && String.Equals(LabelKey((string)actual), LabelKey((string)expected), StringComparison.OrdinalIgnoreCase); }
        private static string Describe(IEnumerable<object> values)
        { var parts = values.Select(v => (v is string ? "«" + ValueFormatter.Format(v) + "»" : ValueFormatter.Format(v)) + " (" + Type(v) + ")").ToArray(); return parts.Length == 0 ? "nessun output" : String.Join(" · ", parts); }
        private static string Snippet(string text) { text = (text ?? "").Replace('\n', ' ').Replace('\r', ' '); return text.Length <= 150 ? text : text.Substring(0, 147) + "…"; }
        private static TutorFeedback WithEvidence(TutorFeedback feedback, Queue<string> trace, HashSet<string> visited, int passed, int total)
        { feedback.Executed = visited; feedback.Passed = passed; feedback.Total = total; feedback.Trace = trace.Count == 0 ? "" : "Ultimi passaggi del tuo diagramma in questa prova:\n" + String.Join("\n", trace); return feedback; }
    }
}
