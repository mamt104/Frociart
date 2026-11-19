// Independent cache correctness harness; excluded from production compilation.
// Compile with Core, Examples, Theme, NativeTheme, BlockPicker and FlowCanvas.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using Frociart;

internal static class CanvasCacheTests
{
    private static int _passed;
    private static readonly MethodInfo HitNode = typeof(FlowCanvas).GetMethod("HitNode", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo HitEdge = typeof(FlowCanvas).GetMethod("HitEdge", BindingFlags.Instance | BindingFlags.NonPublic);
    private static string _folder;

    [STAThread]
    private static void Main(string[] args)
    {
        _folder = args.Length == 0 ? Path.Combine(Path.GetTempPath(), "frociart-cache-tests") : args[0];
        Directory.CreateDirectory(_folder);
        Application.EnableVisualStyles(); QaDialogs.Start();
        UiMotion.SetEnabled(false); // Compare settled retained paths. Animated geometry is covered by SizingTests.
        Ui.SetDarkMode(false, false);
        using (FlowCanvas canvas = new FlowCanvas { Size = new Size(1100, 780), Diagram = Graph() })
        {
            if (args.Length > 1 && args[1] == "golden")
            {
                using (Bitmap image = canvas.RenderExportBitmap(1f)) image.Save(Path.Combine(_folder, "01-initial.png"), ImageFormat.Png);
                canvas.Diagram.Nodes[3].Text = string.Join("\n", Enumerable.Repeat("risultato = \"Perché € → più righe, senza invadere il titolo\"", 18));
                using (Bitmap image = canvas.RenderExportBitmap(1f)) image.Save(Path.Combine(_folder, "02-long-text.png"), ImageFormat.Png);
                return;
            }
            int changing = 0, changed = 0;
            canvas.DiagramChanging += delegate { changing++; };
            canvas.DiagramChanged += delegate { changed++; };
            Verify(canvas, "01-initial");
            canvas.Diagram.Nodes[3].Text = string.Join("\n", Enumerable.Repeat("risultato = \"Perché € → più righe, senza invadere il titolo\"", 18));
            Verify(canvas, "02-long-text");
            canvas.Diagram.Nodes[4].X = -480;
            Verify(canvas, "03-obstacle-horizontal");
            canvas.Diagram.Nodes[4].Y = 1050;
            Verify(canvas, "04-obstacle-leaves-corridor");
            canvas.Diagram.Nodes[4].Y = 200;
            canvas.Diagram.Nodes[4].Kind = NodeKind.Decision;
            Verify(canvas, "05-obstacle-kind-size");
            canvas.Diagram.Nodes[1].Y += 75;
            canvas.Diagram.Nodes[2].X -= 88;
            Verify(canvas, "06-endpoints-moved");
            canvas.Diagram.Edges[1].To = "decision";
            Verify(canvas, "07-edge-rewired");
            canvas.Diagram.Edges[3].Branch = "false";
            Verify(canvas, "08-branch-changed");
            canvas.Diagram.Edges[0] = new FlowEdge { Id = "replacement-edge", From = "input", To = "input", Branch = "next" };
            Verify(canvas, "09-edge-replaced-same-count");
            FlowNode old = canvas.Diagram.Nodes[1];
            canvas.Diagram.Nodes[1] = new FlowNode { Id = old.Id, Kind = NodeKind.Output, X = old.X + 240, Y = old.Y - 35, Text = "x + 7" };
            Verify(canvas, "10-node-replaced-same-id-count");
            canvas.Diagram.Nodes.Reverse();
            canvas.Diagram.Edges.Reverse();
            Verify(canvas, "11-list-order-changed");
            FlowNode rename = canvas.Diagram.Nodes.First(n => n.Id == "process");
            rename.Id = "renamed-process";
            foreach (FlowEdge edge in canvas.Diagram.Edges)
            {
                if (edge.From == "process") edge.From = rename.Id;
                if (edge.To == "process") edge.To = rename.Id;
            }
            canvas.Diagram.Edges[0].Id = "renamed-edge";
            Verify(canvas, "12-identities-changed");
            Check(changing == 0 && changed == 0, "rendering and cache refresh do not create document events");

            canvas.SelectNodes(new[] { "input", "renamed-process" });
            Diagram history = DiagramIO.Clone(canvas.Diagram);
            canvas.DuplicateSelected();
            Verify(canvas, "13-duplicate-group");
            Check(changing == 1 && changed == 1, "group duplication remains one mutation");
            canvas.DeleteSelection();
            Verify(canvas, "14-delete-group");
            Check(changing == 2 && changed == 2, "group deletion remains one mutation");
            canvas.Diagram = history;
            Verify(canvas, "15-history-document-replaced");
            string light = ImageHash(canvas);
            canvas.Zoom = 0.31f;
            canvas.CurrentNodeId = "input";
            canvas.CurrentEdgeId = canvas.Diagram.Edges[0].Id;
            canvas.SelectAllNodes();
            Ui.SetDarkMode(true, false);
            Check(light == ImageHash(canvas), "cached export remains identical across dark theme, zoom and selection");
            Ui.SetDarkMode(false, false);
            Check(light == ImageHash(canvas), "cached export remains identical after return to light theme");

            var nodes = canvas.Diagram.Nodes.ToList();
            var edges = canvas.Diagram.Edges.ToList();
            canvas.Diagram.Nodes.Clear(); canvas.Diagram.Edges.Clear();
            Check(canvas.GetExportContentBounds().IsEmpty, "clearing the model releases content bounds");
            canvas.Diagram.Nodes.AddRange(nodes); canvas.Diagram.Edges.AddRange(edges);
            Verify(canvas, "16-cleared-and-reused-model");
            canvas.Diagram = null;
            Check(canvas.GetExportContentBounds().IsEmpty, "null document releases scene bounds");
        }
        Console.WriteLine(_passed + " cache correctness checks passed.");
    }

    private static void Verify(FlowCanvas cached, string name)
    {
        using (FlowCanvas fresh = new FlowCanvas { Size = cached.Size, Diagram = DiagramIO.Clone(cached.Diagram), Zoom = cached.Zoom })
        {
            Check(cached.GetExportContentBounds() == fresh.GetExportContentBounds(), name + " content bounds match a fresh document");
            string cachedHash = ImageHash(cached), freshHash = ImageHash(fresh);
            Check(cachedHash == freshHash, name + " pixels match uncached geometry");
            if (name == "01-initial" || name == "02-long-text")
                using (Bitmap image = cached.RenderExportBitmap(1f)) image.Save(Path.Combine(_folder, name + ".png"), ImageFormat.Png);
            bool equal = true;
            for (int y = -60; y < 1200; y += 42)
                for (int x = -530; x < 1000; x += 47)
                {
                    PointF point = new PointF(x, y);
                    if (Identity(HitNode.Invoke(cached, new object[] { point })) != Identity(HitNode.Invoke(fresh, new object[] { point })) ||
                        Identity(HitEdge.Invoke(cached, new object[] { point })) != Identity(HitEdge.Invoke(fresh, new object[] { point }))) equal = false;
                }
            Check(equal, name + " hit tests match fresh paths after direct mutations");
        }
    }

    private static string Identity(object value)
    {
        FlowNode node = value as FlowNode;
        if (node != null) return node.Id;
        FlowEdge edge = value as FlowEdge;
        return edge == null ? null : edge.Id;
    }

    private static string ImageHash(FlowCanvas canvas)
    {
        using (Bitmap image = canvas.RenderExportBitmap(0.5f))
        using (MemoryStream bytes = new MemoryStream())
        using (SHA256 hash = SHA256.Create())
        {
            image.Save(bytes, ImageFormat.Png);
            return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
        }
    }

    private static Diagram Graph()
    {
        Diagram graph = new Diagram { Title = "Cache · connessioni e ostacoli" };
        graph.Nodes.Add(new FlowNode { Id = "start", Kind = NodeKind.Start, X = 320, Y = 0, Text = "Inizio" });
        graph.Nodes.Add(new FlowNode { Id = "input", Kind = NodeKind.Input, X = 289, Y = 130, Text = "x" });
        graph.Nodes.Add(new FlowNode { Id = "process", Kind = NodeKind.Process, X = 296, Y = 440, Text = "x = x - 1" });
        graph.Nodes.Add(new FlowNode { Id = "decision", Kind = NodeKind.Decision, X = 286, Y = 650, Text = "x > 0" });
        graph.Nodes.Add(new FlowNode { Id = "obstacle", Kind = NodeKind.Output, X = -80, Y = 260, Text = "\"Valore: \" + x" });
        graph.Nodes.Add(new FlowNode { Id = "end", Kind = NodeKind.End, X = 630, Y = 880, Text = "Fine" });
        graph.Edges.Add(new FlowEdge { Id = "entry", From = "start", To = "input", Branch = "next" });
        graph.Edges.Add(new FlowEdge { Id = "read", From = "input", To = "process", Branch = "next" });
        graph.Edges.Add(new FlowEdge { Id = "return", From = "process", To = "input", Branch = "next" });
        graph.Edges.Add(new FlowEdge { Id = "yes", From = "decision", To = "process", Branch = "true" });
        graph.Edges.Add(new FlowEdge { Id = "no", From = "decision", To = "end", Branch = "false" });
        return graph;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _passed++;
        Console.WriteLine("PASS " + name);
    }
}

