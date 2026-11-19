using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Frociart.Tests
{
    public static class EngineTests
    {
        private static int assertions, failed;
        private static readonly string Temporary = Path.Combine(Path.GetTempPath(), "frociart-tests-" + Guid.NewGuid().ToString("N"));

        public static int Main()
        {
            Directory.CreateDirectory(Temporary);
            try
            {
                Test("Expression precedence, values and functions", Expressions);
                Test("Expression rejection and resource boundaries", ExpressionErrors);
                Test("Built-in examples and selected branches", BuiltIns);
                Test("Runtime states, inputs, snapshots and errors", Runtime);
                Test("Graph structure and reachability", Graphs);
                Test("Definite assignments across branches and loops", Assignments);
                Test("Save, load, clone and untrusted documents", Files);
            }
            finally { Directory.Delete(Temporary, true); }
            Console.WriteLine(assertions + " assertions; " + failed + " failed groups.");
            return failed == 0 ? 0 : 1;
        }

        private static void Test(string name, Action action)
        {
            try { action(); Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message + "\n" + ex.StackTrace); }
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception(message);
        }

        private static void Equal(object expected, object actual, string message)
        {
            assertions++;
            if (!Object.Equals(expected, actual))
                throw new Exception(message + ": expected «" + expected + "», got «" + actual + "».");
        }

        private static object Eval(string expr)
        {
            return ExpressionLanguage.Evaluate(expr, new Dictionary<string, object>());
        }

        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (Exception ex)
            {
                if (!(ex is FlowException) && !(ex is InvalidDataException) && !(ex is ArgumentException)) throw;
                rejected = true;
            }
            Check(rejected, message);
        }

        private static void Expressions()
        {
            Equal(14.0, Eval("2 + 3 * 4"), "Multiplication precedence");
            Equal(20.0, Eval("(2 + 3) * 4"), "Parentheses");
            Equal(512.0, Eval("2 ^ 3 ^ 2"), "Right-associative power");
            Equal(-4.0, Eval("-2 ^ 2"), "Mathematical unary-minus precedence");
            Equal(0.125, Eval("2 ^ -3"), "Negative exponent");
            Equal(4.0, Eval("(-2) ^ 2"), "Parenthesized negative base");
            Equal(1.0, Eval("7 % 2"), "Remainder");
            Equal(-1.0, Eval("-3 % 2"), "Negative remainder");
            Equal(10.5, Eval("1.05e1"), "Scientific notation");
            Equal(0.001, Eval("1e-3"), "Small scientific notation");
            Equal(0.5, Eval(".5"), "Fraction without zero");
            Equal(true, Eval("4 = 4"), "Single equals comparison");
            Equal(false, Eval("4 == 5"), "Double equals comparison");
            Equal(true, Eval("4 != 5 e 3 <> 7"), "Not equal forms and Italian AND");
            Equal(true, Eval("3 <= 3 and 4 >= 2 && 5 > 4"), "Relation forms");
            Equal(true, Eval("false or true and true"), "AND precedence before OR");
            Equal(false, Eval("non vero o falso"), "Italian logical keywords");
            Equal(true, Eval("!false || false"), "Symbol logical operators");
            Equal(true, Eval("1 and 2"), "Numeric condition truth");
            Equal(false, Eval("0 or 0"), "Numeric zero condition");
            Equal(true, Eval("'abc' < 'abd'"), "Ordinal string comparison");
            Equal(false, Eval("'4' = 4"), "No implicit equality coercion");
            Equal("Somma: 7", Eval("\"Somma: \" + (3 + 4)"), "String concatenation");
            Equal("vero!", Eval("true + '!'"), "Italian boolean formatting");
            Equal("a\nb", Eval("'a\\nb'"), "String escape");
            Equal("a\"b", Eval("\"a\\\"b\""), "Escaped quote");
            Equal("a\\b", Eval("'a\\\\b'"), "Escaped backslash");
            Equal(3.0, Eval("abs(-3)"), "Absolute value");
            Equal(9.0, Eval("sqrt(81)"), "Square root");
            Equal(-3.0, Eval("min(5, -3, 20, 0)"), "Minimum multiple args");
            Equal(20.0, Eval("max(5, -3, 20, 0)"), "Maximum multiple args");
            Equal(-2.0, Eval("floor(-1.2)"), "Floor for negative");
            Equal(3.0, Eval("round(2.5)"), "Rounding halves away from zero");
            Equal(-3.0, Eval("round(-2.5)"), "Rounding negative halves");
            Equal(2.35, Eval("round(2.345, 2)"), "Rounding decimal places");
            Equal(3.0, Eval("len('a\\nb')"), "String length");
            Equal(true, Eval("true or missing > 0"), "Short circuit OR");
            Equal(false, Eval("false and (1 / 0 > 0)"), "Short circuit AND");
            Equal(false, Eval("false and missing > 0"), "Short circuit unknown variable");
            IDictionary<string, object> vars = new Dictionary<string, object> { { "Contatore", 4.0 } };
            Equal(5.0, ExpressionLanguage.Evaluate("CONTATORE + 1", vars), "Case insensitive variables");
            Check(ExpressionLanguage.GetVariables("false and missing").Count == 0, "Definite-reads skip literal short circuit");
            Check(ExpressionLanguage.GetVariables("a + A + b").Count == 2, "Read set combines casing");
            Equal("0", ValueFormatter.Format(-0.0), "Negative zero display");
        }

        private static void ExpressionErrors()
        {
            string[] invalid = {
                "", " ", "2 +", "(2 + 3", "2 + 3)", "2 ** 3", "1e", "1e+", "1e999",
                "'open", "\"bad\\q\"", "a.b", "System.IO.File.ReadAllText('x')",
                "new Process()", "random()", "sqrt()", "sqrt(1, 2)", "min(1)", "len(3)",
                "round(1, -1)", "round(1, 1.5)", "round(1, 16)", "sqrt(-1)", "1/0", "1%0",
                "10^999", "(-1)^0.5", "missing + 1", "'hi' - 1", "true + false", "'hi' and true",
                "2,5", "a[0]", "2;3"
            };
            foreach (string expression in invalid)
            {
                string current = expression;
                Reject(() => Eval(current), "Must reject unsafe/invalid expression: " + expression);
            }
            Reject(() => Eval(new string('(', 100) + "1" + new string(')', 100)), "Nested expression limit");
            Reject(() => Eval(String.Join("+", Enumerable.Repeat("1", 600).ToArray())), "AST size limit");
            Reject(() => Eval(new string('a', 65)), "Identifier length limit");
            Reject(() => Eval(new string(' ', DiagramIO.MaxBlockText + 1) + "1"), "Expression text limit");
            string huge = new string('x', 20000);
            IDictionary<string, object> vars = new Dictionary<string, object> { { "a", huge } };
            Reject(() => ExpressionLanguage.Evaluate("a+a", vars), "Concatenation bound");
        }

        private static Interpreter Run(Diagram d, string[] inputs, int maxSteps = 10000)
        {
            Interpreter runtime = new Interpreter(d, maxSteps);
            int index = 0;
            int watchdog = 0;
            while (runtime.State != RuntimeState.Completed && runtime.State != RuntimeState.Error)
            {
                if (++watchdog > 20050) throw new Exception("Test watchdog exceeded");
                if (runtime.State == RuntimeState.WaitingForInput)
                {
                    if (index >= inputs.Length) throw new Exception("Example requested unexpected input");
                    if (!runtime.ProvideInput(inputs[index++])) throw new Exception("Input rejected: " + runtime.LastMessage);
                }
                else runtime.Step();
            }
            return runtime;
        }

        private static string Output(Interpreter runtime) { return String.Join("|", runtime.Output.ToArray()); }

        private static void BuiltIns()
        {
            Equal(7, Examples.All().Count, "Seven examples including file copying and turtle graphics");
            foreach (Diagram d in Examples.All().Take(4))
                Check(!Validator.Validate(d).Any(i => i.Severity == Severity.Error), "Valid built-in " + d.Title);
            Check(Validator.Validate(Examples.MissingBranch()).Any(i => i.Code == "decision-branches"), "Intentional missing branch diagnosed");
            Equal("Somma di due numeri", Examples.Named("somma di due numeri").Title, "Lookup case insensitive");
            Interpreter sum = Run(Examples.Sum(), new[] { "2.5", "7.5" });
            Equal(RuntimeState.Completed, sum.State, "Sum completes");
            Equal("Somma: 10", Output(sum), "Sum expected output");
            Equal(6, sum.Steps, "Sum block count");
            Equal(10.0, sum.Variables["somma"], "Sum variable exposed");
            Equal("Somma: 4", Output(Run(Examples.Sum(), new[] { "1,5", "2,5" })), "Italian decimal input");
            foreach (int n in new[] { -7, -4, -3, 0, 1, 4, 7, 128 })
                Equal(n % 2 == 0 ? "Pari" : "Dispari",
                    Output(Run(Examples.Parity(), new[] { n.ToString(CultureInfo.InvariantCulture) })), "Parity " + n);
            Interpreter countdown = Run(Examples.Countdown(), new[] { "3" });
            Equal("3|2|1|Via!", Output(countdown), "Countdown output");
            Equal(14, countdown.Steps, "Countdown block count");
            Equal("Via!", Output(Run(Examples.Countdown(), new[] { "0" })), "Zero countdown exits immediately");
            Equal("Via!", Output(Run(Examples.Countdown(), new[] { "-3" })), "Negative countdown exits immediately");
            Equal("Maggiore: 8", Output(Run(Examples.Maximum(), new[] { "8", "2" })), "Maximum true branch");
            Equal("Maggiore: 8", Output(Run(Examples.Maximum(), new[] { "2", "8" })), "Maximum false branch");
            Equal("Maggiore: -2", Output(Run(Examples.Maximum(), new[] { "-2", "-8" })), "Maximum negatives");
            Equal("Maggiore: 3", Output(Run(Examples.Maximum(), new[] { "3", "3" })), "Maximum equality");
        }

        private static Diagram Chain(params FlowNode[] middle)
        {
            Diagram d = new Diagram();
            d.Nodes.Add(new FlowNode { Id = "s", Kind = NodeKind.Start, Text = "Inizio" });
            d.Nodes.AddRange(middle);
            d.Nodes.Add(new FlowNode { Id = "z", Kind = NodeKind.End, Text = "Fine" });
            for (int i = 1; i < d.Nodes.Count; i++) Connect(d, d.Nodes[i - 1].Id, d.Nodes[i].Id);
            return d;
        }

        private static FlowNode N(string id, NodeKind kind, string text)
        {
            return new FlowNode { Id = id, Kind = kind, Text = text };
        }

        private static void Connect(Diagram d, string from, string to, string branch = "next")
        {
            d.Edges.Add(new FlowEdge { From = from, To = to, Branch = branch });
        }

        private static void Runtime()
        {
            Interpreter runtime = new Interpreter(Examples.Parity());
            Equal(RuntimeState.Ready, runtime.State, "Initial state");
            Equal("start", runtime.CurrentNodeId, "Initial node");
            Check(!runtime.ProvideInput("3"), "Unsolicited input ignored");
            Check(runtime.Step(), "Start step progresses");
            Equal("start", runtime.LastNodeId, "Last executed retained");
            Equal("input", runtime.CurrentNodeId, "Current is next block");
            Equal(1, runtime.Steps, "Start counted");
            Check(!runtime.Step(), "Input suspends");
            Equal(RuntimeState.WaitingForInput, runtime.State, "Input waiting");
            Equal("numero", runtime.InputVariable, "Variable to prompt");
            Equal("Inserisci un numero intero", runtime.InputPrompt, "Custom input prompt");
            Check(!runtime.Step(), "Repeated step while waiting is no-op");
            Equal(1, runtime.Steps, "Waiting does not consume steps");
            Check(!runtime.ProvideInput("Infinity"), "Nonfinite input rejected");
            Equal(RuntimeState.WaitingForInput, runtime.State, "Invalid input can be corrected");
            Equal(1, runtime.Steps, "Bad input does not consume steps");
            Check(!runtime.ProvideInput("1e9999"), "Overflowing numeric input is not silently treated as text");
            Check(!runtime.ProvideInput(new string('9', 1000)), "Huge numeric input rejected");
            Check(!runtime.ProvideInput(new string('x', 32769)), "Oversized input rejected");
            Equal(RuntimeState.WaitingForInput, runtime.State, "Input limit failures can be corrected");
            Check(runtime.ProvideInput("4"), "Valid input accepted");
            Equal(2, runtime.Steps, "Input counts once after supplied");
            Equal("input", runtime.LastNodeId, "Input block completes");
            runtime.Step();
            Equal("even", runtime.CurrentNodeId, "True target");
            Equal("check_true_even", runtime.LastEdgeId, "Chosen branch exposed");
            runtime.Step(); runtime.Step();
            Equal(RuntimeState.Completed, runtime.State, "End completes");
            int completedSteps = runtime.Steps;
            Check(!runtime.Step() && !runtime.ProvideInput("3"), "Calls after completion no-op");
            Equal(completedSteps, runtime.Steps, "Completion stable");
            runtime.Reset();
            Equal(RuntimeState.Ready, runtime.State, "Reset state");
            Equal(0, runtime.Steps, "Reset steps");
            Equal(0, runtime.Output.Count, "Reset output");
            Equal(0, runtime.Variables.Count, "Reset variables");
            runtime.Step(); runtime.Step(); runtime.ProvideInput("3"); runtime.Step();
            Equal("odd", runtime.CurrentNodeId, "Reset then false target");
            Equal("check_false_odd", runtime.LastEdgeId, "False edge exposed");
            bool readOnly = false;
            try { runtime.Variables.Add("injected", 42.0); } catch (NotSupportedException) { readOnly = true; }
            Check(readOnly, "Variables collection is externally immutable");

            Diagram original = Examples.Sum();
            Interpreter snapshot = new Interpreter(original);
            original.Nodes.First(n => n.Kind == NodeKind.Process).Text = "somma = 0";
            while (snapshot.State != RuntimeState.Completed && snapshot.State != RuntimeState.Error)
            {
                if (snapshot.State == RuntimeState.WaitingForInput) snapshot.ProvideInput("2");
                else snapshot.Step();
            }
            Equal("Somma: 4", Output(snapshot), "Running diagram is an isolated snapshot");

            Diagram transaction = Chain(N("p", NodeKind.Process, "x = 1; y = 1 / 0"), N("out", NodeKind.Output, "\"done\""));
            Interpreter bad = Run(transaction, new string[0]);
            Equal(RuntimeState.Error, bad.State, "Runtime division by zero");
            Check(bad.Error.Contains("zero"), "Useful arithmetic failure");
            Equal("p", bad.LastNodeId, "Failure points to current block");
            Equal(0, bad.Variables.Count, "Failed process assignments roll back together");
            Check(!bad.Step(), "Step after error does not execute");

            Diagram multi = Chain(N("p", NodeKind.Process, "x <- 2;\r\ny = x + 3\ntext = 'a;b'; z = len(text)"), N("out", NodeKind.Output, "y + z"));
            Equal("8", Output(Run(multi, new string[0])), "Sequential assignments and separators in quotes");
            Diagram texts = Chain(N("i", NodeKind.Input, "nome"), N("out", NodeKind.Output, "\"Ciao \" + nome"));
            Equal("Ciao Christian", Output(Run(texts, new[] { "Christian" })), "Text input");
            Equal("Ciao vero", Output(Run(texts, new[] { "vero" })), "Boolean input");
            Equal("Ciao ", Output(Run(texts, new[] { "" })), "Empty string input");
            Diagram typeError = Examples.Sum();
            Interpreter types = Run(typeError, new[] { "abc", "2" });
            Equal("Somma: abc2", Output(types), "Plus consistently concatenates strings");
            Diagram numericError = Chain(N("i", NodeKind.Input, "x"), N("p", NodeKind.Process, "y = x * 2"));
            Check(Run(numericError, new[] { "abc" }).Error.Contains("numero"), "Non-numeric multiplication failure");

            Diagram loop = Examples.Countdown();
            loop.Nodes.First(n => n.Id == "decrement").Text = "n = n";
            Interpreter infinite = Run(loop, new[] { "1" }, 80);
            Equal(RuntimeState.Error, infinite.State, "Infinite loop guard");
            Equal(80, infinite.Steps, "Exact configured step cap");
            Check(infinite.Error.Contains("80"), "Cap error includes effective limit");
            Interpreter invalid = new Interpreter(Examples.MissingBranch());
            Equal(RuntimeState.Error, invalid.State, "Invalid graphs refuse execution");
            invalid.Reset();
            Equal(RuntimeState.Error, invalid.State, "Reset preserves structural error");
        }

        private static bool Has(Diagram d, string code, string node = null)
        {
            return Validator.Validate(d).Any(i => i.Code == code && (node == null || i.NodeId == node));
        }

        private static void Graphs()
        {
            Check(!Validator.Validate(DiagramIO.CreateBlank()).Any(i => i.Severity == Severity.Error), "Blank is executable");
            Equal(2, Run(DiagramIO.CreateBlank(), new string[0]).Steps, "Blank executes two nodes");
            Check(Validator.Validate(null).Any(i => i.Severity == Severity.Error), "Null document rejected");
            Diagram none = new Diagram();
            Check(Has(none, "start-count"), "Missing start");
            Check(Has(none, "end-missing"), "Missing end");
            Diagram twoStarts = DiagramIO.CreateBlank();
            twoStarts.Nodes.Add(N("secondStart", NodeKind.Start, "Inizio"));
            Check(Has(twoStarts, "start-count"), "Multiple starts");
            Diagram unfinished = Chain(N("p", NodeKind.Process, "x = 1"));
            unfinished.Edges.RemoveAt(1);
            Check(Has(unfinished, "single-outgoing", "p"), "Missing outgoing edge");
            Check(Has(unfinished, "no-end-path", "p"), "No route to end");
            Diagram startIncoming = Chain(N("p", NodeKind.Process, "x = 1"));
            Connect(startIncoming, "p", "s");
            Check(Has(startIncoming, "start-incoming", "s"), "Start has no incoming");
            Check(Has(startIncoming, "single-outgoing", "p"), "Multiple outgoing on process");
            Diagram endOutgoing = Chain(N("p", NodeKind.Process, "x = 1"));
            Connect(endOutgoing, "z", "p");
            Check(Has(endOutgoing, "end-outgoing", "z"), "End has no outgoing");
            Diagram isolated = DiagramIO.CreateBlank();
            isolated.Nodes.Add(N("isolated", NodeKind.End, "Fine"));
            Check(Has(isolated, "unreachable", "isolated"), "Unreachable component diagnosed");
            Check(!Validator.Validate(isolated).Any(i => i.Severity == Severity.Error), "Valid unreachable end is warning only");
            Diagram sameTarget = Chain(N("d", NodeKind.Decision, "vero"));
            sameTarget.Edges.RemoveAt(1);
            Connect(sameTarget, "d", "z", "true");
            Connect(sameTarget, "d", "z", "false");
            Check(!Validator.Validate(sameTarget).Any(i => i.Severity == Severity.Error), "Both decision branches may share target");
            Diagram malformed = DiagramIO.CreateBlank();
            malformed.Nodes[1].Id = malformed.Nodes[0].Id;
            Check(Has(malformed, "document"), "Duplicate node IDs diagnosed safely");
            malformed = DiagramIO.CreateBlank();
            malformed.Edges[0].To = "missing";
            Check(Has(malformed, "document"), "Dangling endpoint diagnosed safely");
            malformed = DiagramIO.CreateBlank();
            malformed.Edges.Add(new FlowEdge { Id = malformed.Edges[0].Id, From = malformed.Nodes[0].Id, To = malformed.Nodes[1].Id });
            Check(Has(malformed, "document"), "Duplicate edge IDs diagnosed safely");
            malformed = DiagramIO.CreateBlank();
            malformed.Edges[0].Branch = "oops";
            Check(Has(malformed, "document"), "Unknown branch diagnosed");
            Check(Has(Examples.Countdown(), "loop"), "Reachable loop warning");
            Diagram sealedLoop = Chain(N("p", NodeKind.Process, "x = 1"));
            sealedLoop.Edges.RemoveAt(1);
            Connect(sealedLoop, "p", "p");
            Check(Has(sealedLoop, "no-end-path", "p"), "Closed loop with disconnected end is error");
            Check(Has(sealedLoop, "loop", "p"), "Self-loop detection");
            Diagram badSyntax = Chain(N("p", NodeKind.Process, "x == 1"));
            Check(Has(badSyntax, "syntax", "p"), "Comparison syntax rejected as assignment");
            Check(Has(Chain(N("i", NodeKind.Input, "e")), "syntax", "i"), "Reserved name rejected for input");
            Check(Has(Chain(N("out", NodeKind.Output, "ciao mondo")), "syntax", "out"), "Unquoted message gives syntax guidance");
        }

        private static Diagram Diamond(string leftText, string rightText, string output = "x")
        {
            Diagram d = new Diagram();
            d.Nodes.AddRange(new[] {
                N("s", NodeKind.Start, "Inizio"), N("i", NodeKind.Input, "flag"),
                N("d", NodeKind.Decision, "flag"), N("left", NodeKind.Process, leftText),
                N("right", NodeKind.Process, rightText), N("out", NodeKind.Output, output),
                N("z", NodeKind.End, "Fine")
            });
            Connect(d, "s", "i"); Connect(d, "i", "d");
            Connect(d, "d", "left", "true"); Connect(d, "d", "right", "false");
            Connect(d, "left", "out"); Connect(d, "right", "out"); Connect(d, "out", "z");
            return d;
        }

        private static void Assignments()
        {
            Check(Has(Chain(N("out", NodeKind.Output, "x")), "uninitialized", "out"), "Reading never-assigned value");
            Check(Has(Chain(N("p", NodeKind.Process, "x = x + 1")), "uninitialized", "p"), "Self-reference requires initial value");
            Check(!Has(Chain(N("p", NodeKind.Process, "x = 1; x = x + 1"), N("out", NodeKind.Output, "x")), "uninitialized"), "Sequential same-block definition");
            Check(Has(Chain(N("p", NodeKind.Process, "y = x; x = 1")), "uninitialized", "p"), "Later assignment cannot satisfy earlier read");
            Check(!Has(Chain(N("i", NodeKind.Input, "x"), N("p", NodeKind.Process, "X = x + 1"), N("out", NodeKind.Output, "X")), "uninitialized"), "Case-insensitive data flow");
            Check(Has(Diamond("x = 1", "y = 2"), "uninitialized", "out"), "Definition on only one branch is insufficient");
            Diagram both = Diamond("x = 1", "x = 2");
            Check(!Has(both, "uninitialized"), "Both branches define value");
            Equal("1", Output(Run(both, new[] { "vero" })), "Diamond yes output");
            Equal("2", Output(Run(both, new[] { "falso" })), "Diamond no output");
            Check(Has(Diamond("x = 1", "x = y"), "uninitialized", "right"), "Undefined read in branch");
            Check(!Has(Chain(N("out", NodeKind.Output, "falso and x > 0")), "uninitialized"), "Literal short circuit does not require impossible read");
            Diagram loop = Examples.Countdown();
            loop.Nodes.First(n => n.Id == "input").Text = "other";
            Check(Has(loop, "uninitialized", "check"), "Backedge cannot initialize first loop condition");
            Check(Has(loop, "uninitialized", "count"), "Backedge cannot initialize first loop output");
            loop = Examples.Countdown();
            loop.Nodes.First(n => n.Id == "decrement").Text = "x = n; n = n - 1";
            loop.Nodes.First(n => n.Id == "go").Text = "x";
            Check(Has(loop, "uninitialized", "go"), "Body definition not definite after zero iterations");
            loop = Examples.Countdown();
            Check(!Has(loop, "uninitialized"), "Preheader input remains definite through loop");
            Diagram isolatedDefinition = Chain(N("out", NodeKind.Output, "x"));
            isolatedDefinition.Nodes.Add(N("unreachable", NodeKind.Process, "x = 1"));
            Connect(isolatedDefinition, "unreachable", "out");
            Check(Has(isolatedDefinition, "uninitialized", "out"), "Unreachable predecessor cannot define a reachable read");
            Diagram noFalseNegative = Diamond("x = 1", "y = 2", "x");
            noFalseNegative.Nodes.Reverse();
            Check(Has(noFalseNegative, "uninitialized", "out"), "Data-flow result independent of node storage order");
        }

        private static void Files()
        {
            string file = Path.Combine(Temporary, "diagramma.flow");
            Diagram d = Examples.Sum();
            d.Title = "Somma è «italiana»";
            d.Nodes[1].Prompt = "Testo con \"virgolette\"\nSeconda riga";
            DiagramIO.Save(file, d);
            Check(File.Exists(file), "File saved");
            Diagram loaded = DiagramIO.Load(file);
            Equal(d.Title, loaded.Title, "Unicode title roundtrip");
            Equal(d.Nodes[1].Prompt, loaded.Nodes[1].Prompt, "Quotes/newlines roundtrip");
            Equal(d.Nodes.Count, loaded.Nodes.Count, "Node count roundtrip");
            Equal(d.Edges[0].From, loaded.Edges[0].From, "Endpoint roundtrip");
            Equal("Somma: 9", Output(Run(loaded, new[] { "4", "5" })), "Loaded program executes");
            Diagram cloned = DiagramIO.Clone(d);
            cloned.Nodes[0].Text = "Changed";
            Check(d.Nodes[0].Text != cloned.Nodes[0].Text, "Clone is deep");
            cloned.Title = "Overwritten";
            DiagramIO.Save(file, cloned);
            Equal("Overwritten", DiagramIO.Load(file).Title, "Atomic overwrite succeeds");
            byte[] previous = File.ReadAllBytes(file);
            Diagram bad = DiagramIO.Clone(cloned);
            bad.Nodes[0].X = Single.NaN;
            Reject(() => DiagramIO.Save(file, bad), "Reject nonfinite coordinate before saving");
            Check(previous.SequenceEqual(File.ReadAllBytes(file)), "Failed save preserves previous bytes");
            Equal(1, Directory.GetFiles(Temporary).Length, "Temporary save files cleaned");
            bad = DiagramIO.Clone(cloned);
            bad.Nodes[0].Text = new string('a', DiagramIO.MaxBlockText + 1);
            Reject(() => DiagramIO.Save(file, bad), "Oversized block rejected");
            bad = DiagramIO.Clone(cloned);
            bad.Nodes[0].Id = bad.Nodes[1].Id;
            Reject(() => DiagramIO.Save(file, bad), "Duplicate IDs rejected by I/O");
            bad = DiagramIO.Clone(cloned);
            bad.Edges[0].To = "unknown";
            Reject(() => DiagramIO.Save(file, bad), "Dangling edge rejected by I/O");
            bad = DiagramIO.Clone(cloned);
            bad.Version = 99;
            Reject(() => DiagramIO.Save(file, bad), "Future schema rejected");
            bad = new Diagram();
            for (int i = 0; i <= DiagramIO.MaxNodes; i++) bad.Nodes.Add(N("n" + i, NodeKind.End, "Fine"));
            Reject(() => DiagramIO.Save(file, bad), "Node count cap");
            string invalid = Path.Combine(Temporary, "bad.flow");
            foreach (string payload in new[] { "", "{", "{}", "null", "[]", "{\"Version\":99,\"Nodes\":[],\"Edges\":[]}", "{\"Version\":1,\"Nodes\":null,\"Edges\":[]}" })
            {
                File.WriteAllText(invalid, payload, new UTF8Encoding(false));
                Reject(() => DiagramIO.Load(invalid), "Malformed JSON/schema must reject: " + payload);
            }
            using (FileStream stream = File.Create(invalid)) stream.SetLength(DiagramIO.MaxFileBytes + 1);
            Reject(() => DiagramIO.Load(invalid), "Oversized file rejected before parsing");
            Diagram draft = Examples.MissingBranch();
            DiagramIO.Save(file, draft);
            Check(Has(DiagramIO.Load(file), "decision-branches"), "Incomplete draft saved and reopened for repair");
        }
    }
}
