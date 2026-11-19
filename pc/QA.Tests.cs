using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Frociart;

namespace FrociartQA
{
    internal static class CoreTests
    {
        private static int passed, failed;
        private static readonly List<string> failures = new List<string>();

        public static int Main()
        {
            var watch = Stopwatch.StartNew();
            Test("Parità: positivo, zero, negativo", delegate {
                ExpectOutput(Examples.Parity(), new[] { "4" }, "Pari");
                ExpectOutput(Examples.Parity(), new[] { "7" }, "Dispari");
                ExpectOutput(Examples.Parity(), new[] { "0" }, "Pari");
                ExpectOutput(Examples.Parity(), new[] { "-3" }, "Dispari");
            });
            Test("Somma: numeri, segni e decimali italiani", delegate {
                ExpectOutput(Examples.Sum(), new[] { "2", "3" }, "Somma: 5");
                ExpectOutput(Examples.Sum(), new[] { "-2", "2" }, "Somma: 0");
                ExpectOutput(Examples.Sum(), new[] { "1,5", "2.5" }, "Somma: 4");
            });
            Test("Conto alla rovescia: ciclo finito e casi limite", delegate {
                ExpectOutput(Examples.Countdown(), new[] { "3" }, "3", "2", "1", "Via!");
                ExpectOutput(Examples.Countdown(), new[] { "0" }, "Via!");
                ExpectOutput(Examples.Countdown(), new[] { "-3" }, "Via!");
                Require(Validator.Validate(Examples.Countdown()).Any(i => i.Code == "loop" && i.Severity == Severity.Warning), "Il ciclo deve produrre un avviso.");
            });
            Test("Massimo: ordine, parità e negativi", delegate {
                ExpectOutput(Examples.Maximum(), new[] { "2", "3" }, "Maggiore: 3");
                ExpectOutput(Examples.Maximum(), new[] { "5", "5" }, "Maggiore: 5");
                ExpectOutput(Examples.Maximum(), new[] { "-8", "-2" }, "Maggiore: -2");
            });
            Test("Ramo mancante e riparazione dello stesso documento", delegate {
                Diagram d = Examples.MissingBranch();
                Require(HasError(d, "decision-branches"), "Manca errore sul ramo.");
                Require(new Interpreter(d).State == RuntimeState.Error, "Il runtime non deve partire con ramo mancante.");
                Edge(d, "check", "minor", "false");
                ExpectOutput(d, new[] { "17" }, "Minorenne");
                ExpectOutput(d, new[] { "18" }, "Maggiorenne");
            });
            Test("Priorità e associatività matematiche", delegate {
                Number("2 + 3 * 4", 14); Number("(2 + 3) * 4", 20);
                Number("-2^2", -4); Number("(-2)^2", 4);
                Number("2^-2", .25); Number("2^3^2", 512);
            });
            Test("Cortocircuito logico impedisce errori non raggiunti", delegate {
                Eq(ExpressionLanguage.Evaluate("falso and (1 / 0 > 0)", null), false);
                Eq(ExpressionLanguage.Evaluate("vero or mancante > 0", null), true);
                Eq(ExpressionLanguage.Evaluate("not (4 == 0)", null), true);
                Require(ExpressionLanguage.GetVariables("vero or mancante > 0").Count == 0, "La lettura morta di mancante non va richiesta.");
                ExpectOutput(Linear(NodeKind.Output, "vero or mancante > 0"), new string[0], "vero");
            });
            Test("Assegnazioni sequenziali e stringhe con separatori", delegate {
                ExpectOutput(ProcessOutput("a = 1; a = a + 2\nb = a", "b"), new string[0], "3");
                ExpectOutput(ProcessOutput("testo = \"A;B\\nC\"; lunghezza = len(testo)", "lunghezza"), new string[0], "5");
                ExpectOutput(ProcessOutput("età <- 18\nTotale = età + 2", "totale"), new string[0], "20");
            });
            Test("Funzioni consentite e domini", delegate {
                Number("max(2, -3, 7)", 7); Number("min(2, -3, 7)", -3);
                Number("round(-2.5)", -3); Number("round(1.234,2)", 1.23);
                Throws(delegate { ExpressionLanguage.Evaluate("sqrt(-1)", null); });
                Throws(delegate { ExpressionLanguage.Evaluate("round(2,16)", null); });
            });
            Test("Operazioni numeriche non finite e divisione per zero", delegate {
                Throws(delegate { ExpressionLanguage.Evaluate("1 / 0", null); });
                Throws(delegate { ExpressionLanguage.Evaluate("1 % 0", null); });
                Throws(delegate { ExpressionLanguage.Evaluate("1e308 * 1e308", null); });
                Throws(delegate { ExpressionLanguage.Evaluate("(-2)^.5", null); });
                Throws(delegate { ExpressionLanguage.Evaluate("1e9999", null); });
            });
            Test("Parser chiuso: chiamate esterne e sintassi errata respinte", delegate {
                foreach (string expr in new[] { "System.IO.File.ReadAllText(\"x\")", "shell(\"x\")", "a[0]", "1 +", "\"non chiusa", "1e+", "min(1)", "1; 2" })
                    Throws(delegate { ExpressionLanguage.Evaluate(expr, null); });
            });
            Test("Profondità e dimensione espressioni limitate", delegate {
                Throws(delegate { ExpressionLanguage.Evaluate(new string('(', 100) + "1" + new string(')', 100), null); });
                Throws(delegate { ExpressionLanguage.Evaluate(String.Join("+", Enumerable.Repeat("1", 600)), null); });
            });
            Test("Input rifiutato resta correggibile senza consumare passi", delegate {
                var run = new Interpreter(Examples.Parity());
                Require(run.Step(), "Inizio non eseguito.");
                Require(!run.Step() && run.State == RuntimeState.WaitingForInput, "Input non in attesa.");
                int steps = run.Steps;
                Require(!run.ProvideInput("NaN"), "NaN accettato.");
                Require(run.State == RuntimeState.WaitingForInput && run.Steps == steps && !run.Variables.ContainsKey("numero"), "Input errato ha alterato lo stato.");
                Require(run.ProvideInput("4"), "Input corretto rifiutato.");
                RunRemaining(run, new string[0]);
                Eq(String.Join("|", run.Output), "Pari");
            });
            Test("Input in attesa non avanza premendo ancora Un passo", delegate {
                var run = new Interpreter(Examples.Parity());
                run.Step(); run.Step();
                int steps = run.Steps;
                for (int i = 0; i < 100; i++) Require(!run.Step(), "Step ha eseguito un Input non compilato.");
                Require(run.Steps == steps && run.State == RuntimeState.WaitingForInput, "Attesa incoerente.");
            });
            Test("Fine è idempotente; reset pulisce output e variabili", delegate {
                var run = Run(Examples.Sum(), new[] { "2", "3" });
                int steps = run.Steps; string output = String.Join("|", run.Output);
                Require(!run.Step() && !run.ProvideInput("99"), "Il runtime deve ignorare comandi dopo Fine.");
                Require(run.Steps == steps && String.Join("|", run.Output) == output, "Fine ha ripetuto una istruzione.");
                run.Reset();
                Require(run.State == RuntimeState.Ready && run.Steps == 0 && run.Output.Count == 0 && run.Variables.Count == 0, "Reset incompleto.");
            });
            Test("Blocco multilinea fallito non lascia assegnazioni parziali", delegate {
                var run = Run(Linear(NodeKind.Process, "a = 1; b = 1 / 0"), new string[0]);
                Require(run.State == RuntimeState.Error && !run.Variables.ContainsKey("a"), "Transazione parziale visibile.");
                Require(run.CurrentNodeId == "work", "Errore non associato al blocco.");
            });
            Test("Assegnazione mancante su un ramo rilevata", delegate {
                Require(HasError(Merge(false), "uninitialized"), "La fusione ha perso il controllo delle definizioni.");
                ExpectOutput(Merge(true), new[] { "1" }, "1");
                ExpectOutput(Merge(true), new[] { "0" }, "2");
            });
            Test("Variabile inizializzata solo nel ciclo non disponibile al primo giro", delegate {
                Diagram d = Endless("x < 4");
                d.Nodes.First(n => n.Id == "decision").Text = "x < 4";
                d.Nodes.First(n => n.Id == "work").Text = "x = x + 1";
                Require(HasError(d, "uninitialized"), "La retroazione ha mascherato l'assenza di inizializzazione.");
            });
            Test("Rami Sì e No verso stessa destinazione accettati", delegate {
                Diagram d = Linear(NodeKind.Decision, "vero");
                d.Edges.RemoveAll(e => e.From == "work");
                Edge(d, "work", "end", "true"); Edge(d, "work", "end", "false");
                Require(!Validator.Validate(d).Any(i => i.Severity == Severity.Error), "Rami convergenti respinti.");
                Require(Run(d, new string[0]).State == RuntimeState.Completed, "Rami convergenti non eseguibili.");
            });
            Test("Blocco irraggiungibile corretto dà avviso", delegate {
                Diagram d = DiagramIO.CreateBlank();
                Node(d, "orphan", NodeKind.Output, "\"non eseguito\"");
                Edge(d, "orphan", d.Nodes.First(n => n.Kind == NodeKind.End).Id);
                var issues = Validator.Validate(d);
                Require(issues.Any(i => i.Code == "unreachable" && i.NodeId == "orphan"), "Avviso irraggiungibilità assente.");
                Require(!issues.Any(i => i.Severity == Severity.Error), "Blocco corretto irraggiungibile blocca il run.");
            });
            Test("Errori strutturali: Inizio, Fine e uscite", delegate {
                Diagram d = DiagramIO.CreateBlank(); d.Nodes.RemoveAll(n => n.Kind == NodeKind.Start); d.Edges.Clear();
                Require(HasError(d, "start-count"), "Inizio mancante non rilevato.");
                d = DiagramIO.CreateBlank(); Node(d, "other_start", NodeKind.Start, "Inizio"); Edge(d, "other_start", d.Nodes.Last().Id);
                Require(HasError(d, "start-count"), "Inizio duplicato non rilevato.");
                d = Linear(NodeKind.Output, "1"); Edge(d, "end", "work");
                Require(HasError(d, "end-outgoing"), "Fine con uscita non rilevata.");
                d = Linear(NodeKind.Output, "1"); Edge(d, "work", "start");
                Require(HasError(d, "start-incoming") && HasError(d, "single-outgoing"), "Ingresso Inizio/uscita doppia non rilevati.");
            });
            Test("Grafo senza possibilità strutturale di terminare respinto", delegate {
                Diagram d = Linear(NodeKind.Process, "x = 1");
                d.Edges.RemoveAll(e => e.From == "work"); Edge(d, "work", "work");
                Require(HasError(d, "no-end-path"), "Ciclo privo di uscita non rilevato.");
            });
            Test("Ciclo staticamente valido termina al budget", delegate {
                var d = Endless("vero");
                Require(!Validator.Validate(d).Any(i => i.Severity == Severity.Error), "Ciclo con uscita strutturale respinto.");
                var run = new Interpreter(d, 37); RunRemaining(run, new string[0]);
                Require(run.State == RuntimeState.Error && run.Steps == 37 && run.Error.Contains("Limite"), "Budget non rispettato.");
            });
            Test("Documenti malformati sono respinti dal validatore", delegate {
                Diagram d = DiagramIO.CreateBlank(); d.Nodes[1].Id = d.Nodes[0].Id;
                Require(HasError(d, "document"), "ID duplicato non rilevato.");
                d = DiagramIO.CreateBlank(); d.Edges[0].To = "missing";
                Require(HasError(d, "document"), "Destinazione assente non rilevata.");
                d = DiagramIO.CreateBlank(); d.Nodes[0].Kind = (NodeKind)999;
                Require(HasError(d, "document"), "Tipo sconosciuto non rilevato.");
                d = DiagramIO.CreateBlank(); d.Nodes[0].X = float.NaN;
                Require(HasError(d, "document"), "Coordinate NaN non rilevate.");
                d = DiagramIO.CreateBlank(); d.Version = 2;
                Require(HasError(d, "document"), "Versione sconosciuta non rilevata.");
            });
            Test("Clonazione e salvataggio preservano modello, rami e metadati", delegate {
                string path = Path.Combine(Path.GetTempPath(), "frociart-qa-" + Guid.NewGuid().ToString("N") + ".flow");
                try {
                    Diagram d = Examples.Parity(); d.Title = "Prova àèìòù";
                    d.Nodes[1].X = -127.5f; d.Nodes[1].Prompt = "Numero? \"Sì\"";
                    DiagramIO.Save(path, d); Diagram copy = DiagramIO.Load(path);
                    Eq(copy.Title, d.Title); Eq(copy.Nodes[1].Prompt, d.Nodes[1].Prompt); Eq(copy.Nodes[1].X, d.Nodes[1].X);
                    Require(copy.Edges.Select(e => e.Id + e.From + e.To + e.Branch).SequenceEqual(d.Edges.Select(e => e.Id + e.From + e.To + e.Branch)), "Collegamenti alterati.");
                    ExpectOutput(copy, new[] { "4" }, "Pari");
                    d.Title = "Seconda versione"; DiagramIO.Save(path, d); Eq(DiagramIO.Load(path).Title, "Seconda versione");
                } finally { if (File.Exists(path)) File.Delete(path); }
            });
            Test("Interprete usa una copia indipendente dal documento", delegate {
                var d = ProcessOutput("x = 1", "x"); var run = new Interpreter(d);
                d.Nodes.First(n => n.Id == "work").Text = "x = 99";
                RunRemaining(run, new string[0]); Eq(String.Join("|", run.Output), "1");
            });
            Test("Limite testo input e crescita stringhe", delegate {
                var d = Linear(NodeKind.Input, "testo"); var run = new Interpreter(d);
                run.Step(); run.Step(); Require(!run.ProvideInput(new string('x', 32769)), "Input oltre limite accettato.");
                Require(run.State == RuntimeState.WaitingForInput, "Input troppo lungo deve essere correggibile.");
                var variables = new Dictionary<string, object> { { "text", new string('x', 32768) } };
                Throws(delegate { ExpressionLanguage.Evaluate("text + \"x\"", variables); });
            });

            Console.WriteLine("QA CORE: " + passed + " superati, " + failed + " falliti; " + watch.ElapsedMilliseconds + " ms.");
            foreach (string failure in failures) Console.WriteLine(failure);
            return failed == 0 ? 0 : 1;
        }

        private static void Test(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; failures.Add("FAIL " + name + ": " + ex.GetType().Name + " — " + ex.Message); Console.WriteLine(failures.Last()); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Eq(object actual, object expected) { Require(Object.Equals(actual, expected), "Atteso «" + expected + "», ottenuto «" + actual + "»."); }
        private static void Number(string expr, double expected) { Eq(ExpressionLanguage.Evaluate(expr, null), expected); }
        private static void Throws(Action action)
        {
            bool didThrow = false;
            try { action(); } catch (Exception ex) { if (ex is FlowException || ex is InvalidDataException) didThrow = true; else throw; }
            Require(didThrow, "L'operazione doveva essere respinta.");
        }
        private static bool HasError(Diagram d, string code) { return Validator.Validate(d).Any(i => i.Severity == Severity.Error && i.Code == code); }
        private static Interpreter Run(Diagram d, string[] inputs) { var run = new Interpreter(d); RunRemaining(run, inputs); return run; }
        private static void RunRemaining(Interpreter run, string[] inputs)
        {
            int next = 0, guard = 0;
            while (run.State != RuntimeState.Completed && run.State != RuntimeState.Error)
            {
                if (++guard > 30000) throw new Exception("Harness: runtime non terminato oltre il limite atteso.");
                if (run.State == RuntimeState.WaitingForInput)
                {
                    Require(next < inputs.Length, "Harness: Input esauriti.");
                    Require(run.ProvideInput(inputs[next++]), "Input di prova rifiutato: " + run.LastMessage);
                }
                else run.Step();
            }
            Require(next == inputs.Length, "Harness: Input di prova non consumati.");
        }
        private static void ExpectOutput(Diagram d, string[] inputs, params string[] expected)
        {
            var errors = Validator.Validate(d).Where(i => i.Severity == Severity.Error).ToList();
            Require(errors.Count == 0, "Diagramma respinto: " + String.Join(" | ", errors.Select(i => i.Message)));
            var run = Run(d, inputs);
            Require(run.State == RuntimeState.Completed, "Esecuzione non completata: " + run.Error);
            Require(run.Output.SequenceEqual(expected), "Output atteso «" + String.Join("|", expected) + "», ottenuto «" + String.Join("|", run.Output) + "».");
        }
        private static void Node(Diagram d, string id, NodeKind kind, string text) { d.Nodes.Add(new FlowNode { Id = id, Kind = kind, Text = text }); }
        private static void Edge(Diagram d, string from, string to, string branch = "next") { d.Edges.Add(new FlowEdge { From = from, To = to, Branch = branch }); }
        private static Diagram Linear(NodeKind kind, string text)
        {
            var d = new Diagram(); Node(d, "start", NodeKind.Start, "Inizio"); Node(d, "work", kind, text); Node(d, "end", NodeKind.End, "Fine");
            Edge(d, "start", "work"); Edge(d, "work", "end"); return d;
        }
        private static Diagram ProcessOutput(string process, string output)
        {
            var d = Linear(NodeKind.Process, process); Node(d, "output", NodeKind.Output, output);
            d.Edges.RemoveAll(e => e.From == "work"); Edge(d, "work", "output"); Edge(d, "output", "end"); return d;
        }
        private static Diagram Merge(bool defineBoth)
        {
            var d = new Diagram(); Node(d, "start", NodeKind.Start, "Inizio"); Node(d, "input", NodeKind.Input, "c");
            Node(d, "decision", NodeKind.Decision, "c > 0"); Node(d, "yes", NodeKind.Process, "x = 1");
            Node(d, "no", NodeKind.Process, defineBoth ? "x = 2" : "y = 2"); Node(d, "output", NodeKind.Output, "x"); Node(d, "end", NodeKind.End, "Fine");
            Edge(d, "start", "input"); Edge(d, "input", "decision"); Edge(d, "decision", "yes", "true"); Edge(d, "decision", "no", "false");
            Edge(d, "yes", "output"); Edge(d, "no", "output"); Edge(d, "output", "end"); return d;
        }
        private static Diagram Endless(string condition)
        {
            var d = new Diagram(); Node(d, "start", NodeKind.Start, "Inizio"); Node(d, "decision", NodeKind.Decision, condition);
            Node(d, "work", NodeKind.Process, "x = 1"); Node(d, "end", NodeKind.End, "Fine");
            Edge(d, "start", "decision"); Edge(d, "decision", "work", "true"); Edge(d, "decision", "end", "false"); Edge(d, "work", "decision"); return d;
        }
    }
}

#if QA_UI
namespace FrociartQA
{
    internal static class UIEdgeTests
    {
        private static int passed, failed;
        private static T Field<T>(object value, string name)
        { return (T)value.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(value); }
        private static void Invoke(object value, string name)
        { value.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(value, null); }
        private static void Check(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
        }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static TestResult Test(Diagram d, string input, string expected)
        {
            var test = new TestSession(d, input, expected);
            int guard = 0;
            while (!test.Tick()) if (++guard > 200) throw new Exception("Sessione non terminata.");
            return test.Result;
        }
        [STAThread]
        public static int Main(string[] args)
        {
            Check("TestSession: output multilinea confrontato per righe visibili", delegate {
                var d = DiagramIO.CreateBlank(); string end = d.Nodes.Last().Id;
                var n = new FlowNode { Id = "output", Kind = NodeKind.Output, Text = "\"a\\nb\"" }; d.Nodes.Add(n);
                d.Edges[0].To = n.Id; d.Edges.Add(new FlowEdge { From = n.Id, To = end });
                var result = Test(d, "", "a\nb");
                Require(result.Passed, result.Title + ": " + result.Message);
            });
            Check("TestSession: output vuoto esplicito", delegate {
                var d = DiagramIO.CreateBlank(); string end = d.Nodes.Last().Id;
                var n = new FlowNode { Id = "output", Kind = NodeKind.Output, Text = "\"\"" }; d.Nodes.Add(n);
                d.Edges[0].To = n.Id; d.Edges.Add(new FlowEdge { From = n.Id, To = end });
                Require(Test(d, "", "\"\"").Passed, "Riga vuota non confrontabile.");
            });
            if (args.Contains("--sessions-only"))
            {
                Console.WriteLine("QA SESSION EDGE: " + passed + " superati, " + failed + " falliti.");
                return failed == 0 ? 0 : 1;
            }
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            using (var main = new MainForm(null, true))
            {
                main.Show(); System.Windows.Forms.Application.DoEvents();
                Check("Nome file robusto con titolo nullo caricato", delegate {
                    var safeName = typeof(MainForm).GetMethod("SafeName", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    var name = (string)safeName.Invoke(null, new object[] { null });
                    Require(!String.IsNullOrEmpty(name), "Nome file vuoto.");
                });
                Check("Modifica non applicata preservata prima di gesture sul canvas", delegate {
                    main.SetDocument(Examples.Parity(), null, false);
                    main.Canvas.SelectNode("check");
                    Field<System.Windows.Forms.TextBox>(main, "expression").Text = "numero > 0";
                    Invoke(main.Canvas, "RaiseDiagramChanging");
                    main.Document.Nodes.First(n => n.Id == "check").X += 12;
                    Invoke(main.Canvas, "RaiseDiagramChanged");
                    Require(main.Document.Nodes.First(n => n.Id == "check").Text == "numero > 0", "Il contenuto dell'ispettore è stato perso.");
                });
                Check("Focus su testo riconosciuto per proteggere Canc e Ctrl+Z", delegate {
                    main.SetDocument(Examples.Parity(), null, false);
                    main.Canvas.SelectNode("check");
                    var box = Field<System.Windows.Forms.TextBox>(main, "expression"); box.Focus();
                    System.Windows.Forms.Application.DoEvents();
                    var find = typeof(MainForm).GetMethod("ActiveTextBox", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    Require(Object.ReferenceEquals(find.Invoke(null, new object[] { main }), box), "Il focus testo non è riconosciuto.");
                });
                main.Close();
            }
            Console.WriteLine("QA UI EDGE: " + passed + " superati, " + failed + " falliti.");
            return failed == 0 ? 0 : 1;
        }
    }
}
#endif
