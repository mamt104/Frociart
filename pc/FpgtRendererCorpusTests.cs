using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Frociart;

public static class FpgtRendererCorpusTests
{
    static int checks;
    static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    static string Base64(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }

    public static int Main(string[] args)
    {
        try
        {
            string root = args[0];
            var rows = new List<string>();
            var errors = new List<string>();
            var mappings = new List<string>();
            Directory.CreateDirectory(Path.Combine(root, "renderer-documents"));
            string[] targets = { "CSharp", "Python", "Java", "OwnCSharp", "OwnPython", "OwnJava" };
            var cases = Directory.GetFiles(Path.Combine(root, "renderer-cases"), "*.txt").OrderBy(p => p, StringComparer.Ordinal);
            int programs = 0;
            foreach (string path in cases)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                var document = FlowPseudocode.Decode(File.ReadAllText(path));
                if (name == "comments") foreach (var graph in FunctionEditing.Graphs(document)) for (int i=0;i<graph.Nodes.Count;i++) if(graph.Nodes[i].Kind!=NodeKind.Note) graph.Nodes[i].Comment="Commento "+i;
                if (name == "graph-large")
                {
                    // Forward-only reachable graph with an edge entering the middle of another branch.
                    var branches = document.Nodes.Where(n => n.Kind == NodeKind.Decision).ToList();
                    var first = document.Edges.Single(e => e.From == branches[0].Id && e.Branch == "false");
                    first.To = document.Edges.Single(e => e.From == branches[1].Id && e.Branch == "true").To;
                }
                byte[] before = DiagramIO.EncodeDocument(document);
                File.WriteAllBytes(Path.Combine(root,"renderer-documents",name+".frociart"),before);
                var program = FlowSource.Compile(document);
                Check(program.CanGenerate, name + " compiles to source model: " + String.Join("; ",program.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));
                if (name == "graph-large") Check(!FlowSourcePlan.Create(program.Scopes[0]).Structured && program.Scopes[0].Blocks.Count > 128, "Large explicit graph fixture");
                var runtime = new Interpreter(document);
                int inputs = 0;
                for (int step = 0; step < 20000 && runtime.State != RuntimeState.Error && runtime.State != RuntimeState.Completed; step++)
                {
                    if (runtime.State == RuntimeState.WaitingForInput) { Check(runtime.ProvideInput(inputs++ == 0 ? "7" : "Ada"), name + " accepts fixture input"); }
                    else runtime.Step();
                }
                Check(runtime.State == RuntimeState.Completed, name + " interpreter completes: " + runtime.Error);
                string expected = runtime.OutputText.Replace("\r\n", "\n") + (runtime.OutputText.Length > 0 ? "\n" : "");
                foreach (string target in targets)
                {
                    try
                    {
                        var model = FlowFpgtSyntax.Parse(File.ReadAllText(Path.Combine(root, target.StartsWith("Own", StringComparison.Ordinal) ? "portable-models/" + target + ".fpgt" : "references/installed/Template_" + target + ".fpgt")));
                        var code = FlowFpgt.Generate(model, program);
                        mappings.Add(name+"\t"+target+"\tTEXT\t"+Base64(code.Text));
                        foreach (var line in code.Lines) mappings.Add(name+"\t"+target+"\tLINE\t"+line.Number+"\t"+line.Start+"\t"+line.Length+"\t"+String.Join(",",line.Origins.Select(o=>(o.FunctionId??"")+":"+o.NodeId)));
                        Check(before.SequenceEqual(DiagramIO.EncodeDocument(document)), name + " preserves document");
                        foreach (var scope in program.Scopes)
                            foreach (var block in scope.Blocks.Concat(scope.Notes)) Check(code.ForBlock(scope.Id, block.Id).Count > 0, name + " maps " + block.Id);
                        string dir = Path.Combine(root, "generated", name, target);
                        Directory.CreateDirectory(dir);
                        File.WriteAllText(Path.Combine(dir, "Program." + model.Extension), code.Text, new UTF8Encoding(false));
                        rows.Add(name + "\t" + target + "\t" + Base64(expected) + "\t" + inputs);
                    }
                    catch (Exception error) { errors.Add(name + "\t" + target + "\t" + error.Message); }
                }
                programs++;
            }
            File.WriteAllLines(Path.Combine(root, "renderer-index.tsv"), rows, new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(root, "renderer-generation-errors.tsv"), errors, new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(root, "renderer-csharp.tsv"), mappings, new UTF8Encoding(false));
            Console.WriteLine("FPGT_RENDERER_CORPUS " + (errors.Count == 0 ? "PASS" : "FAIL") + " programs=" + programs + " outputs=" + rows.Count + " checks=" + checks + " failures=" + errors.Count);
            foreach (string error in errors) Console.WriteLine(error);
            return errors.Count == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
}
