using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Frociart.Tests
{
    internal static class TutorSystematicTests
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        private static Diagram AlternateIds(Diagram source)
        {
            var d = DiagramIO.Clone(source); var names = d.Nodes.ToDictionary(n => n.Id, n => "alternative_" + n.Id);
            foreach (var node in d.Nodes) node.Id = names[node.Id]; foreach (var edge in d.Edges) { edge.From = names[edge.From]; edge.To = names[edge.To]; } return d;
        }
        private static void Accept(TutorLesson lesson, Diagram diagram, string variant)
        { var result = Tutor.Check(lesson, diagram); Check(result.Pass, lesson.id + " " + variant + ": " + result.Text); }
        private static void Refuse(TutorLesson lesson, Diagram diagram, string variant)
        { var result = Tutor.Check(lesson, diagram); Check(!result.Pass && result.Fix.Length > 0, lesson.id + " rejects " + variant); }
        private static int Main()
        {
            try {
                int cases = 0;
                foreach (var lesson in Tutor.Lessons)
                {
                    Check(lesson.criteria.Length > 100 && !String.IsNullOrEmpty(lesson.assessment), "declared contract " + lesson.id);
                    var solution = lesson.Solution(); Accept(lesson, AlternateIds(solution), "independent IDs");
                    var renamed = DiagramIO.Clone(solution); var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string name in TutorVariables.Names(renamed)) aliases[name] = "dato_" + name;
                    foreach (var node in renamed.Nodes.Where(n => n.Kind != NodeKind.Start && n.Kind != NodeKind.End)) node.Text = TutorVariables.Rewrite(node.Text, aliases);
                    Accept(lesson, renamed, "consistent variable aliases"); Accept(lesson, AlternateIds(renamed), "different IDs plus variable names");
                    foreach (var node in solution.Nodes.Where(n => n.Kind == NodeKind.Process)) {
                        var alternative = lesson.Solution(); var target = Tutor.Find(alternative, node.Id);
                        target.Text = String.Join("\n", node.Text.Split('\n').Select(s => Regex.Replace(s, @"^\s*([\p{L}_][\p{L}\p{N}_]*)\s*=\s*(.*)$", "$1 <- ($2)")));
                        Accept(lesson, alternative, "assignment alias and parentheses");
                        int phase = Array.FindIndex(lesson.steps, s => s.action == Tutor.Write && s.node == node.Id);
                        if (phase >= 0) Check(Tutor.CheckStep(lesson, phase, renamed, "").Pass, "isolated context understands aliases " + lesson.id);
                    }
                    var inverted = lesson.Solution();
                    foreach (var node in inverted.Nodes.Where(n => n.Kind == NodeKind.Decision)) { node.Text = "not (" + node.Text + ")"; foreach (var edge in inverted.Edges.Where(e => e.From == node.Id)) edge.Branch = edge.Branch == "true" ? "false" : "true"; }
                    Accept(lesson, inverted, "negated condition and exchanged branches");
                    foreach (var node in solution.Nodes.Where(n => n.Kind == NodeKind.Output).Take(1)) {
                        var wrong = lesson.Solution(); Tutor.Find(wrong, node.Id).Text = "falso"; Refuse(lesson, wrong, "wrong output type/value");
                    }
                    var broken = lesson.Solution(); broken.Edges.RemoveAt(0); Refuse(lesson, broken, "missing real connection");
                    if (solution.Nodes.Any(n => n.Kind == NodeKind.Decision)) {
                        var wrong = lesson.Solution(); wrong.Nodes.First(n => n.Kind == NodeKind.Decision).Text = "vero"; var result = Tutor.Check(lesson, wrong);
                        Check(!result.Pass && result.Passed < result.Total && result.Total == lesson.trials.Length, "always-true condition caught and whole case count retained " + lesson.id);
                        Check(!String.IsNullOrEmpty(result.Trace) && result.Trace.Contains("Decisione"), "student's actual route explained " + lesson.id);
                    }
                    cases += lesson.trials.Length; Console.WriteLine("PASS LESSON " + lesson.id + " cases=" + lesson.trials.Length + " contract=" + lesson.assessment);
                }
                Check(cases == 364, "independent goal oracle case count");
                var echo = Tutor.Lesson("b06"); int valueStep = Array.FindIndex(echo.steps, s => s.action == Tutor.Value);
                Check(Tutor.CheckStep(echo, valueStep, echo.Solution(), "22").Pass, "Input uses learning goal instead of hardcoded example 21");
                var rename = Tutor.Lesson("b06").Solution(); Tutor.Find(rename, "eta").Text = "anni";
                Check(Tutor.CheckStep(echo, 1, rename, "").Pass, "valid alternative Input name accepted in guided step");
                int changed = TutorVariables.Rename(rename, "eta", "anni"); Check(changed > 0 && Tutor.Check(echo, rename).Pass, "dangling old reads can be updated after Input rename");
                string original = "x = max(max, 1e-3)\ny = \"max e3 x\"\nz = 'x'";
                string replaced = TutorVariables.Rewrite(original, new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { { "max", "valore" }, { "e3", "oops" }, { "x", "nuovo" } });
                Check(replaced.Contains("max(valore, 1e-3)") && replaced.Contains("\"max e3 x\"") && replaced.Contains("'x'") && replaced.StartsWith("nuovo ="), "rename respects functions, scientific literals, quoted text and identifiers");
                var collision = Tutor.Lesson("b09").Solution(); var snapshot = DiagramIO.Clone(collision); bool denied = false;
                try { TutorVariables.Rename(collision, "pagato", "prezzo"); } catch (FlowException) { denied = true; }
                Check(denied && collision.Nodes.Select(n => n.Text).SequenceEqual(snapshot.Nodes.Select(n => n.Text)), "name collision rejected before mutation");
                var loop = Tutor.Lesson("b20").Solution(); loop.Edges.RemoveAll(e => e.From == "mostra"); loop.Edges.Add(new FlowEdge { From = "mostra", To = "fine" }); Refuse(Tutor.Lesson("b20"), loop, "cycle learning goal cannot be skipped");
                Console.WriteLine("PASS SYSTEMATIC " + checks + " checks; all 24 contracts, alternative names, IDs, formulas and paths; 364 goal cases."); return 0;
            } catch (Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
        }
    }
}
