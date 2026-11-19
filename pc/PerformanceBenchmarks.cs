// Reproducible, bounded performance measurements. Excluded from the application.
// These timings describe the tested runtime/machine; they are not Windows-PC promises.
// Args: results-directory [label] [core-only|ui-only|issues-only]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Frociart.Tests
{
    internal static class PerformanceBenchmarks
    {
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<Exception> uiErrors = new List<Exception>();
        private static readonly List<string> rows = new List<string>();
        private static string folder, label;
        private static long checksum;

        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Benchmark fixture failure: " + message); }
        private static long Allocated()
        {
#if NET5_0_OR_GREATER
            return GC.GetAllocatedBytesForCurrentThread();
#else
            return -1;
#endif
        }
        private static object Call(object target, string name, params object[] arguments)
        {
            try { return target.GetType().GetMethod(name, PrivateInstance | BindingFlags.DeclaredOnly).Invoke(target, arguments); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }
        private static T Field<T>(object target, string name)
        { return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target); }
        private static void Pump()
        {
            Application.DoEvents();
            if (uiErrors.Count != 0) throw new Exception("Unexpected UI exception", uiErrors[0]);
        }
        private static string Number(double value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
        private static void Record(string name, int samples, double median, double p95, double max, double allocated, string fixture)
        {
            string row = String.Join("\t", new[] { label, name, samples.ToString(), Number(median), Number(p95), Number(max), Number(allocated), fixture });
            rows.Add(row); Console.WriteLine(row);
            File.WriteAllLines(Path.Combine(folder, label + ".tsv"), rows, new UTF8Encoding(false));
        }
        private static void Measure(string name, int samples, Action action, string fixture, int warmups = 2)
        {
            for (int i = 0; i < warmups; i++) action();
            List<double> times = new List<double>();
            long allocation = 0;
            for (int i = 0; i < samples; i++)
            {
                long before = Allocated();
                Stopwatch timer = Stopwatch.StartNew(); action(); timer.Stop();
                long after = Allocated();
                if (before >= 0 && after >= before) allocation += after - before;
                times.Add(timer.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            Record(name, samples, times[times.Count / 2], times[(int)Math.Ceiling(times.Count * 0.95) - 1],
                times[times.Count - 1], Allocated() < 0 ? -1 : (double)allocation / samples, fixture);
        }

        internal static Diagram Chain(int nodes = 500)
        {
            Diagram d = new Diagram { Title = "Benchmark: valid chain with distinct variables" };
            for (int i = 0; i < nodes; i++)
                d.Nodes.Add(new FlowNode { Id = "n" + i, Kind = i == 0 ? NodeKind.Start : i == nodes - 1 ? NodeKind.End : NodeKind.Process,
                    Text = i == 0 ? "Inizio" : i == nodes - 1 ? "Fine" : "v" + i + " = " + i,
                    X = (i % 20) * 270, Y = (i / 20) * 180 });
            for (int i = 0; i < nodes - 1; i++) d.Edges.Add(new FlowEdge { Id = "e" + i, From = "n" + i, To = "n" + (i + 1), Branch = "next" });
            return d;
        }
        internal static Diagram DenseInvalid()
        {
            Diagram d = Chain(); d.Title = "Benchmark: capacity graph with deliberately invalid outgoing counts";
            for (int i = d.Edges.Count; i < DiagramIO.MaxEdges; i++)
                d.Edges.Add(new FlowEdge { Id = "extra" + i, From = "n" + (1 + i % 498), To = "n" + (1 + (i * 17) % 499), Branch = "next" });
            return d;
        }
        internal static Diagram Loop(int variables, int iterations, int outputWidth)
        {
            Diagram d = new Diagram { Title = "Benchmark: bounded loop" };
            d.Nodes.Add(new FlowNode { Id = "start", Kind = NodeKind.Start, Text = "Inizio", X = 350, Y = 30 });
            string previous = "start";
            List<string> assignments = Enumerable.Range(0, variables).Select(i => "v" + i + " = " + i).ToList();
            assignments.Add("i = 0");
            for (int offset = 0; offset < assignments.Count; offset += 128)
            {
                string id = "init" + offset;
                d.Nodes.Add(new FlowNode { Id = id, Kind = NodeKind.Process, Text = String.Join(";", assignments.Skip(offset).Take(128)), X = 325, Y = 140 + offset / 128 * 130 });
                d.Edges.Add(new FlowEdge { From = previous, To = id }); previous = id;
            }
            d.Nodes.Add(new FlowNode { Id = "check", Kind = NodeKind.Decision, Text = "i < " + iterations, X = 320, Y = 700 });
            d.Nodes.Add(new FlowNode { Id = "body", Kind = NodeKind.Process, Text = variables > 0 ? "v0 = v0 + 1; i = i + 1" : "i = i + 1", X = 620, Y = 960 });
            d.Nodes.Add(new FlowNode { Id = "end", Kind = NodeKind.End, Text = "Fine", X = 160, Y = 960 });
            d.Edges.Add(new FlowEdge { From = previous, To = "check" });
            if (outputWidth > 0)
            {
                d.Nodes.Add(new FlowNode { Id = "out", Kind = NodeKind.Output, Text = "\"" + new string('x', outputWidth) + "\" + i", X = 620, Y = 820 });
                d.Edges.Add(new FlowEdge { From = "check", To = "out", Branch = "true" });
                d.Edges.Add(new FlowEdge { From = "out", To = "body" });
            }
            else d.Edges.Add(new FlowEdge { From = "check", To = "body", Branch = "true" });
            d.Edges.Add(new FlowEdge { From = "check", To = "end", Branch = "false" });
            d.Edges.Add(new FlowEdge { From = "body", To = "check" });
            return d;
        }
        private static void Complete(Interpreter runner)
        {
            while (runner.State != RuntimeState.Completed && runner.State != RuntimeState.Error)
            {
                Require(runner.State != RuntimeState.WaitingForInput, "unexpected input");
                runner.Step();
            }
            checksum += runner.Steps + runner.Output.Count;
        }

        // Experimental comparison only. The production Clone remains unchanged.
        private static Diagram ObjectCopyPrototype(Diagram source)
        {
            DiagramIO.CheckDocument(source);
            Diagram copy = new Diagram { Version = source.Version, Title = source.Title,
                Nodes = new List<FlowNode>(source.Nodes.Count), Edges = new List<FlowEdge>(source.Edges.Count) };
            foreach (FlowNode n in source.Nodes) copy.Nodes.Add(new FlowNode { Id = n.Id, Kind = n.Kind, Text = n.Text, Prompt = n.Prompt, X = n.X, Y = n.Y });
            foreach (FlowEdge e in source.Edges) copy.Edges.Add(new FlowEdge { Id = e.Id, From = e.From, To = e.To, Branch = e.Branch });
            return copy;
        }

        private static void CoreBenchmarks()
        {
            Diagram chain = Chain(), dense = DenseInvalid();
            Require(Validator.Validate(chain).All(i => i.Severity != Severity.Error), "500-node chain must be valid");
            Require(dense.Nodes.Count == 500 && dense.Edges.Count == 1500 && Validator.Validate(dense).Any(i => i.Severity == Severity.Error), "capacity fixture must be explicitly invalid");
            Measure("validate_valid_chain", 7, () => checksum += Validator.Validate(chain).Count,
                "500 nodes;499 edges;498 distinct assigned variables");
            Measure("validate_capacity_invalid", 7, () => checksum += Validator.Validate(dense).Count,
                "500 nodes;1500 edges;deliberate outgoing-count errors");
            Measure("clone_capacity_production", 15, () => checksum += DiagramIO.Clone(dense).Edges.Count,
                "500 nodes;1500 edges;in-memory snapshot");
            Measure("clone_capacity_object_copy_prototype", 15, () => checksum += ObjectCopyPrototype(dense).Edges.Count,
                "prototype only;CheckDocument and distinct model objects;immutable strings shared");
            Measure("clone_parity", 30, () => checksum += DiagramIO.Clone(Examples.Parity()).Nodes.Count, "6 nodes;6 edges");
            Measure("construct_interpreter_chain", 5, () => checksum += new Interpreter(chain).Steps,
                "500-node valid chain;includes validator+clone+compiler");
            foreach (int count in new[] { 0, 400 })
            {
                Interpreter runner = new Interpreter(Loop(count, 100000, 0));
                Require(runner.State == RuntimeState.Ready, "loop must construct successfully");
                Measure("execute_10000_steps_" + (count + 1) + "_vars", 5, () =>
                {
                    runner.Reset(); Complete(runner);
                    Require(runner.State == RuntimeState.Error && runner.Steps == 10000, "10000-step limit is unchanged");
                }, "10000 executed blocks;includes process transactions;" + (count + 1) + " variables");
            }
            Interpreter output = new Interpreter(Loop(0, 2000, 100));
            Measure("execute_long_output", 5, () =>
            {
                output.Reset(); Complete(output);
                Require(output.State == RuntimeState.Completed && output.Output.Count == 2000, "2000-line output must complete");
            }, "6004 steps;2000 lines;206890 content characters;no UI");
            string savePath = Path.Combine(folder, "capacity-recovery.frociart");
            Measure("save_capacity_atomic", 5, () => DiagramIO.Save(savePath, dense),
                "500 nodes;1500 edges;serialize+write+Flush(true)+atomic replace");
        }

        private static void ShowRuntime(MainForm main, Diagram diagram, Interpreter runner)
        {
            main.SetDocument(diagram, null, false); Pump();
            main.Runner = runner;
            main.SingleStep();
            Field<System.Windows.Forms.Timer>(main, "runTimer").Stop();
            Call(main, "RefreshRuntime"); Pump();
        }
        private static void UiBenchmarks(bool issuesOnly)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { uiErrors.Add(e.Exception); };
#if NET5_0_OR_GREATER
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); SetCursorPos(2, 2);
            using (MainForm main = new MainForm(null, true))
            {
                main.Show(); Pump();
                if (!issuesOnly)
                {
                Diagram variablesDiagram = Loop(400, 100000, 0);
                Interpreter variables = new Interpreter(variablesDiagram); Complete(variables);
                ShowRuntime(main, variablesDiagram, variables);
                Measure("refresh_unchanged_401_vars", 20, () => Call(main, "RefreshRuntime"),
                    "visible native controls;401 variables unchanged;excludes message pump");
                Measure("refresh_paint_unchanged_401_vars", 10, () => { Call(main, "RefreshRuntime"); Pump(); },
                    "401 variables unchanged;includes pending WinForms paint messages");
                Require(Field<ListView>(main, "variables").Items.Count == 401, "all variable rows must remain visible in model");

                Diagram outputDiagram = Loop(0, 2000, 100);
                Interpreter output = new Interpreter(outputDiagram); Complete(output);
                ShowRuntime(main, outputDiagram, output);
                Measure("refresh_unchanged_2000_lines", 20, () => Call(main, "RefreshRuntime"),
                    "2000 lines already displayed;no new output;excludes message pump");
                Require(Field<TextBox>(main, "output").Text == String.Join(Environment.NewLine, output.Output), "long output must be complete");

                main.SetDocument(outputDiagram, null, false); Pump();
                main.Runner = new Interpreter(outputDiagram);
                main.SingleStep();
                Field<System.Windows.Forms.Timer>(main, "runTimer").Stop();
                Field<ComboBox>(main, "speed").SelectedIndex = 3;
                List<double> batches = new List<double>();
                Stopwatch all = Stopwatch.StartNew(); long allocation = Allocated();
                while (main.Runner.State != RuntimeState.Completed && main.Runner.State != RuntimeState.Error)
                {
                    Stopwatch batch = Stopwatch.StartNew(); Call(main, "RunTick"); Pump(); batch.Stop(); batches.Add(batch.Elapsed.TotalMilliseconds);
                    if (all.Elapsed.TotalSeconds > 35) throw new Exception("Long-output UI benchmark exceeded its 35-second safety budget");
                }
                all.Stop(); long allocated = Allocated() - allocation;
                Require(main.Runner.State == RuntimeState.Completed && main.Runner.Output.Count == 2000, "UI run must finish all 2000 lines");
                Require(Field<TextBox>(main, "output").Text == String.Join(Environment.NewLine, main.Runner.Output), "visible streamed output must match runtime");
                batches.Sort();
                Record("ui_run_2000_lines_batch_latency", batches.Count, batches[batches.Count / 2], batches[(int)Math.Ceiling(batches.Count * .95) - 1],
                    batches[batches.Count - 1], allocation < 0 ? -1 : (double)allocated / batches.Count,
                    "production instant-mode batches;message pump each batch;2000 lines;total_ms=" + Number(all.Elapsed.TotalMilliseconds));
                Record("ui_run_2000_lines_total", 1, all.Elapsed.TotalMilliseconds, all.Elapsed.TotalMilliseconds, all.Elapsed.TotalMilliseconds,
                    allocation < 0 ? -1 : allocated, "full run, not a subset;native UI and interpreter together");

                }
                Diagram dense = DenseInvalid();
                main.SetDocument(dense, null, false); Pump();
                // Do not time another canvas paint here; the renderer has its own benchmark owner.
                List<ValidationIssue> issues = Validator.Validate(dense);
                typeof(MainForm).GetField("issues", PrivateInstance).SetValue(main, issues);
                Measure("render_capacity_issues_controls", 3, () => Call(main, "RenderIssues"),
                    issues.Count + " real validator issues;control construction/layout;canvas paint excluded", 1);
                main.SetDocument(Examples.Parity(), null, false); main.Close(); Pump();
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                folder = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "FrociartPerformance");
                label = args.Length > 1 ? args[1] : "measurement";
                Directory.CreateDirectory(folder);
                rows.Add("label\tbenchmark\tsamples\tmedian_ms\tp95_ms\tmax_ms\tmean_allocated_bytes\tfixture");
                Console.WriteLine("Environment: runtime=" + Environment.Version + "; OS=" + Environment.OSVersion + "; pointer=" + IntPtr.Size + "; timer_hz=" + Stopwatch.Frequency);
                if (!args.Contains("ui-only") && !args.Contains("issues-only")) CoreBenchmarks();
                if (!args.Contains("core-only")) UiBenchmarks(args.Contains("issues-only"));
                Console.WriteLine("BENCHMARK COMPLETE; checksum=" + checksum);
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
    }
}
