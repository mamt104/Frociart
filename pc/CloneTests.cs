// Deep-snapshot regressions. This test runner is excluded from the application.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace Frociart.Tests
{
    internal static class CloneTests
    {
        private static int checks;

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            checks++; Console.WriteLine("PASS: " + description);
        }

        // The former production implementation, retained only as a test oracle.
        private static Diagram LegacyClone(Diagram source)
        {
            DiagramIO.CheckDocument(source);
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Diagram),
                new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 40000 });
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, source);
                stream.Position = 0;
                return (Diagram)serializer.ReadObject(stream);
            }
        }

        private static bool Equal(Diagram a, Diagram b)
        {
            if (a.Version != b.Version || a.Title != b.Title || a.Nodes.Count != b.Nodes.Count || a.Edges.Count != b.Edges.Count) return false;
            for (int i = 0; i < a.Nodes.Count; i++)
            {
                FlowNode x = a.Nodes[i], y = b.Nodes[i];
                if (x.Id != y.Id || x.Kind != y.Kind || x.Text != y.Text || x.Prompt != y.Prompt || x.X != y.X || x.Y != y.Y ||
                    x.TrueLabel != y.TrueLabel || x.FalseLabel != y.FalseLabel || x.TrueColor != y.TrueColor || x.FalseColor != y.FalseColor) return false;
            }
            for (int i = 0; i < a.Edges.Count; i++)
            {
                FlowEdge x = a.Edges[i], y = b.Edges[i];
                if (x.Id != y.Id || x.From != y.From || x.To != y.To || x.Branch != y.Branch) return false;
            }
            return true;
        }

        private static Diagram AllKinds()
        {
            Diagram d = new Diagram { Version = 4, Title = "Unicode: è, 日本語, \uD83D\uDE80; \"citazione\"\nseconda riga" };
            foreach (NodeKind kind in Enum.GetValues(typeof(NodeKind)))
                d.Nodes.Add(new FlowNode { Id = "n-" + kind, Kind = kind, Text = kind == NodeKind.Input ? "numero" : "testo " + kind,
                    Prompt = "Domanda «" + kind + "»?", X = ((int)kind - 3) * 27.25f, Y = (int)kind / 3f });
            d.Edges.Add(new FlowEdge { Id = "branch-no", From = "n-Decision", To = "n-End", Branch = "false" });
            d.Edges.Add(new FlowEdge { Id = "branch-next", From = "n-Start", To = "n-Decision", Branch = "next" });
            d.Edges.Add(new FlowEdge { Id = "branch-yes", From = "n-Decision", To = "n-End", Branch = "true" });
            return d;
        }

        private static void MatchesPreviousImplementation()
        {
            Diagram all = AllKinds();
            Check(Equal(LegacyClone(all), DiagramIO.Clone(all)), "all six kinds, edge branches, fields and stored order match the previous JSON clone");
            Check(Equal(all, DiagramIO.Clone(all)), "cloning does not normalize or edit a diagram");
            Diagram defaults = new Diagram(); defaults.Nodes.Add(new FlowNode());
            Check(Equal(LegacyClone(defaults), DiagramIO.Clone(defaults)), "model defaults and generated identifiers are preserved");
            foreach (string text in new[] { null, "", "e\u0301 / è / 漢字 / \uD83D\uDE80", "\r\n\t\"\\/", "\0\u0001\u000B\uFFFF", "\uD800", "\uDC00", "x\uD800z" })
            {
                Diagram d = AllKinds(); d.Title = text; d.Nodes[0].Text = text; d.Nodes[1].Prompt = text;
                Check(Equal(LegacyClone(d), DiagramIO.Clone(d)), "nullable and UTF-16 string fidelity, case " + (text == null ? "null" : text.Length.ToString(CultureInfo.InvariantCulture)));
            }
            foreach (float coordinate in new[] { -100000f, -0f, float.Epsilon, 1f / 3f, 99999.99f, 100000f })
            {
                Diagram d = AllKinds(); d.Nodes[0].X = coordinate; d.Nodes[0].Y = -coordinate;
                Check(Equal(LegacyClone(d), DiagramIO.Clone(d)), "coordinate numeric fidelity: " + coordinate.ToString("R", CultureInfo.InvariantCulture));
            }
            Diagram bounds = AllKinds(); bounds.Title = new string('t', 200);
            bounds.Nodes[0].Text = new string('b', DiagramIO.MaxBlockText); bounds.Nodes[0].Prompt = new string('p', 1024);
            Check(Equal(LegacyClone(bounds), DiagramIO.Clone(bounds)), "maximum permitted text lengths retain their entire contents");
            Check(Equal(LegacyClone(new Diagram()), DiagramIO.Clone(new Diagram())), "empty draft lists remain empty without structural validation being introduced");
            Diagram incomplete = Examples.MissingBranch();
            Check(Equal(LegacyClone(incomplete), DiagramIO.Clone(incomplete)), "incomplete educational drafts remain cloneable");
        }

        private static void Isolation()
        {
            Diagram original = AllKinds(), copy = DiagramIO.Clone(original);
            Check(!Object.ReferenceEquals(original, copy) && !Object.ReferenceEquals(original.Nodes, copy.Nodes) &&
                !Object.ReferenceEquals(original.Edges, copy.Edges), "snapshot and both model lists are distinct objects");
            Check(original.Nodes.Zip(copy.Nodes, (a, b) => !Object.ReferenceEquals(a, b)).All(x => x) &&
                original.Edges.Zip(copy.Edges, (a, b) => !Object.ReferenceEquals(a, b)).All(x => x), "every node and edge is a distinct mutable object");
            Diagram saved = LegacyClone(original);
            copy.Title = "snapshot edited"; copy.Nodes[0].Text = "changed"; copy.Nodes[0].Prompt = null;
            copy.Nodes[0].X = 12345; copy.Nodes[0].Kind = NodeKind.Output; copy.Nodes[0].Id = "different";
            copy.Edges[0].Id = "other-edge"; copy.Edges[0].From = "other"; copy.Edges[0].To = "another"; copy.Edges[0].Branch = "next";
            copy.Nodes.RemoveAt(2); copy.Edges.Reverse();
            Check(Equal(original, saved), "editing snapshot fields, list membership and order cannot mutate the source");
            copy = DiagramIO.Clone(original); saved = LegacyClone(copy);
            original.Title = null; original.Nodes[0].Text = "source changed"; original.Edges[0].To = "source target changed";
            original.Nodes.Clear(); original.Edges.Clear();
            Check(Equal(copy, saved), "editing or clearing the source cannot mutate a captured snapshot");
            Diagram second = DiagramIO.Clone(copy); second.Nodes[1].Text = "second generation";
            Check(copy.Nodes[1].Text != second.Nodes[1].Text, "successive history snapshots do not share node objects");

            Diagram executable = Examples.Sum(); Interpreter runner = new Interpreter(executable);
            executable.Nodes.Single(n => n.Kind == NodeKind.Process).Text = "somma = 999";
            executable.Nodes.Clear(); executable.Edges.Clear();
            Queue<string> input = new Queue<string>(new[] { "4", "5" });
            while (runner.State != RuntimeState.Completed && runner.State != RuntimeState.Error)
            {
                if (runner.State == RuntimeState.WaitingForInput) runner.ProvideInput(input.Dequeue());
                else runner.Step();
            }
            Check(runner.State == RuntimeState.Completed && runner.Output.Single() == "Somma: 9", "interpreter retains its isolated program when the editor document changes");
        }

        private static void SameRejection(Diagram invalid, string name)
        {
            Exception previous = null, current = null;
            try { LegacyClone(invalid); } catch (Exception e) { previous = e; }
            try { DiagramIO.Clone(invalid); } catch (Exception e) { current = e; }
            Check(previous is InvalidDataException && current is InvalidDataException && previous.Message == current.Message,
                "document validation is preserved: " + name);
        }

        private static void CapacityAndValidation()
        {
            Diagram capacity = new Diagram();
            for (int i = 0; i < DiagramIO.MaxNodes; i++) capacity.Nodes.Add(new FlowNode { Id = "n" + i, Kind = NodeKind.End, Text = "Fine" });
            for (int i = 0; i < DiagramIO.MaxEdges; i++) capacity.Edges.Add(new FlowEdge { Id = "e" + i, From = "n" + i % 500, To = "n" + (i + 1) % 500 });
            Diagram copied = DiagramIO.Clone(capacity);
            Check(Equal(LegacyClone(capacity), copied) && copied.Nodes.Count == 500 && copied.Edges.Count == 1500,
                "500-node/1500-edge draft retains all fields without requiring executable structure");
            capacity.Nodes.Add(new FlowNode { Id = "overflow" }); SameRejection(capacity, "501 nodes");
            capacity.Nodes.RemoveAt(capacity.Nodes.Count - 1); capacity.Edges.Add(new FlowEdge { Id = "overflow", From = "n0", To = "n1" });
            SameRejection(capacity, "1501 edges");
            SameRejection(null, "null diagram");
            foreach (int version in new[] { 0, 3 }) { Diagram d = AllKinds(); d.Version = version; SameRejection(d, "version " + version); }
            Diagram supportedV2=Examples.Parity();supportedV2.Version=2;Check(Equal(LegacyClone(supportedV2),DiagramIO.Clone(supportedV2)),"supported version 2 survives cloning");
            Action<Diagram>[] mutations =
            {
                d => d.Nodes = null, d => d.Edges = null, d => d.Nodes.Add(null), d => d.Edges.Add(null),
                d => d.Nodes[0].Id = null, d => d.Nodes[0].Id = " ", d => d.Nodes[0].Id = d.Nodes[1].Id,
                d => d.Nodes[0].Id = new string('i', 129), d => d.Nodes[0].Kind = (NodeKind)99,
                d => d.Nodes[0].X = Single.NaN, d => d.Nodes[0].Y = Single.PositiveInfinity,
                d => d.Nodes[0].X = 100001, d => d.Nodes[0].Y = -100001,
                d => d.Title = new string('t', 201), d => d.Nodes[0].Text = new string('b', DiagramIO.MaxBlockText + 1),
                d => d.Nodes[0].Prompt = new string('p', 1025), d => d.Edges[0].Id = null,
                d => d.Edges[0].Id = d.Edges[1].Id, d => d.Edges[0].To = "missing", d => d.Edges[0].From = null,
                d => d.Edges[0].Branch = "unknown"
            };
            for (int i = 0; i < mutations.Length; i++) { Diagram d = AllKinds(); mutations[i](d); SameRejection(d, "malformed fixture " + (i + 1)); }
        }

        private static void BranchMetadata()
        {
            Diagram original = Examples.Parity(); FlowNode decision = original.Nodes.Single(n => n.Kind == NodeKind.Decision);
            Check(decision.GetBranchLabel("true") == "Sì" && decision.GetBranchLabel("false") == "No", "legacy branch labels use defaults");
            Check(decision.GetBranchColor("true") == "#39B985" && decision.GetBranchColor("false") == "#EF6C82", "legacy branch colors use defaults");
            decision.TrueLabel = "Divisibile \uD83D\uDE80"; decision.FalseLabel = "Resto";
            decision.TrueColor = "#123AbC"; decision.FalseColor = "#CDEF12";
            Diagram copy = DiagramIO.Clone(original), jsonCopy = LegacyClone(original);
            Check(Equal(original, copy) && Equal(original, jsonCopy), "custom branch labels and colors survive snapshot and JSON roundtrip");
            FlowNode copied = copy.Nodes.Single(n => n.Kind == NodeKind.Decision);
            copied.TrueLabel = "Modificata"; copied.FalseColor = "#112233";
            Check(decision.TrueLabel == "Divisibile \uD83D\uDE80" && decision.FalseColor == "#CDEF12", "editing branch metadata in snapshot does not mutate source");
            Diagram bounded = DiagramIO.Clone(original); FlowNode boundedDecision = bounded.Nodes.Single(n => n.Kind == NodeKind.Decision);
            boundedDecision.TrueLabel = new string('x', 60); boundedDecision.FalseLabel = "";
            Check(Equal(LegacyClone(bounded), DiagramIO.Clone(bounded)), "60-character and empty branch labels remain valid drafts");
            boundedDecision.TrueLabel += "x"; SameRejection(bounded, "61-character branch label");
            foreach (string invalid in new[] { "", "red", "#123", "#11223344", "#GG0011", "112233" })
            { Diagram d = DiagramIO.Clone(original); d.Nodes[0].TrueColor = invalid; SameRejection(d, "invalid branch color " + invalid); }
            string legacyJson = "{\"Version\":1,\"Title\":\"Legacy\",\"Nodes\":[{\"Id\":\"start\",\"Kind\":0,\"Text\":\"Inizio\",\"Prompt\":\"\",\"X\":0,\"Y\":0},{\"Id\":\"end\",\"Kind\":1,\"Text\":\"Fine\",\"Prompt\":\"\",\"X\":0,\"Y\":200}],\"Edges\":[{\"Id\":\"edge\",\"From\":\"start\",\"To\":\"end\",\"Branch\":\"next\"}]}";
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Diagram));
            using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(legacyJson)))
            {
                Diagram legacy = (Diagram)serializer.ReadObject(stream); DiagramIO.CheckDocument(legacy);
                Check(legacy.Nodes[0].TrueLabel == null && legacy.Nodes[0].TrueColor == null, "absent optional members remain absent when reading legacy JSON");
                Check(legacy.Nodes[0].GetBranchLabel("true") == "Sì" && legacy.Nodes[0].GetBranchColor("false") == "#EF6C82", "deserialized legacy nodes return defaults despite bypassed initializers");
                Check(Equal(legacy, DiagramIO.Clone(legacy)), "cloning legacy metadata does not silently normalize it");
            }
            Interpreter runner = new Interpreter(original); runner.Step(); runner.Step(); runner.ProvideInput("4"); runner.Step();
            Check(runner.LastMessage.Contains("Divisibile") && runner.CurrentNodeId == "even", "custom label appears in runtime explanation without changing true branch semantics");
        }

        public static int Main()
        {
            try
            {
                MatchesPreviousImplementation(); Isolation(); CapacityAndValidation(); BranchMetadata();
                Console.WriteLine("CLONE REGRESSION: " + checks + " checks passed"); return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
