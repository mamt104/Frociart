// Independent input/selection regression harness; excluded from the production executable.
// mcs -langversion:6 -r:System.Windows.Forms -r:System.Drawing -r:System.Core -r:System.Runtime.Serialization \
//     -out:SelectionTests.exe Core.cs Theme.cs BlockPicker.cs FlowCanvas.cs SelectionTests.cs
// Run on Windows, or with xvfb-run -a mono SelectionTests.exe for the native Mono backend.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Frociart;

public sealed class SelectionProbe : FlowCanvas
{
    public Keys Modifiers;
    protected override Keys GetInteractionModifiers() { return Modifiers; }
    public void Down(Point p, MouseButtons button = MouseButtons.Left) { OnMouseDown(new MouseEventArgs(button, 1, p.X, p.Y, 0)); }
    public void MoveTo(Point p, MouseButtons button = MouseButtons.Left) { OnMouseMove(new MouseEventArgs(button, 0, p.X, p.Y, 0)); }
    public void Up(Point p, MouseButtons button = MouseButtons.Left) { OnMouseUp(new MouseEventArgs(button, 1, p.X, p.Y, 0)); }
    public void Key(Keys keys) { OnKeyDown(new KeyEventArgs(keys)); }
    public void ReleaseKey(Keys keys) { OnKeyUp(new KeyEventArgs(keys)); }
    public void Wheel(Point p, int delta) { OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, p.X, p.Y, delta)); }
    public Point At(float x, float y) { return Point.Round(WorldToClient(new PointF(x, y))); }
    public void ClickWorld(float x, float y) { Point p = At(x, y); Down(p); Up(p); }
    public void Marquee(float x1, float y1, float x2, float y2)
    {
        Point p = At(x1, y1), q = At(x2, y2);
        Down(p); MoveTo(q); Up(q);
    }
}

public static class SelectionTests
{
    private static int passes;
    private static int before, after;
    private static Diagram undoSnapshot;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name); passes++;
    }
    private static bool Selected(FlowCanvas canvas, params string[] ids)
    {
        return new HashSet<string>(canvas.SelectedNodeIds).SetEquals(ids);
    }
    private static bool Near(float a, float b) { return Math.Abs(a - b) < 0.01f; }
    private static FlowNode Node(Diagram diagram, string id) { return diagram.Nodes.First(n => n.Id == id); }
    private static Diagram Fixture()
    {
        var diagram = new Diagram { Title = "Selection regression" };
        diagram.Nodes.Add(new FlowNode { Id = "a", Kind = NodeKind.Process, Text = "x = 0", X = 0, Y = 0 });
        diagram.Nodes.Add(new FlowNode { Id = "b", Kind = NodeKind.Process, Text = "x = x + 1", X = 300, Y = 0 });
        diagram.Nodes.Add(new FlowNode { Id = "c", Kind = NodeKind.Decision, Text = "x < 10", X = 600, Y = 0 });
        diagram.Nodes.Add(new FlowNode { Id = "d", Kind = NodeKind.End, Text = "Fine", X = 300, Y = 280 });
        diagram.Edges.Add(new FlowEdge { Id = "ab", From = "a", To = "b", Branch = "next" });
        diagram.Edges.Add(new FlowEdge { Id = "bc", From = "b", To = "c", Branch = "next" });
        diagram.Edges.Add(new FlowEdge { Id = "cb", From = "c", To = "b", Branch = "true" });
        diagram.Edges.Add(new FlowEdge { Id = "cd", From = "c", To = "d", Branch = "false" });
        return diagram;
    }
    private static void Reset(SelectionProbe canvas)
    {
        canvas.Diagram = Fixture(); canvas.SelectNode(null); canvas.Modifiers = Keys.None;
        canvas.SnapToGrid = true; canvas.ResetView(); before = after = 0; undoSnapshot = null;
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles(); QaDialogs.Start();
        Check(GridSnap.Round(59.99999f)==72&&GridSnap.Round(-59.99999f)==-72&&GridSnap.Round(59.99f)==48,"grid midpoint tolerates float transform noise without changing ordinary rounding");
        using (var form = new Form { ClientSize = new Size(1100, 820) })
        using (var canvas = new SelectionProbe { Dock = DockStyle.Fill })
        {
            form.Controls.Add(canvas); form.Show(); Application.DoEvents();
            canvas.DiagramChanging += delegate { before++; undoSnapshot = DiagramIO.Clone(canvas.Diagram); };
            canvas.DiagramChanged += delegate { after++; };

            Reset(canvas);
            canvas.SelectNodes(new[] { "a", "b", "missing", "a" });
            Check(Selected(canvas, "a", "b") && canvas.SelectionCount == 2 && canvas.SelectedNodeId == null,
                "multi-selection ignores unknown/duplicate ids and exposes no editable single node");
            var snapshot = canvas.SelectedNodeIds;
            bool readOnly = false;
            try { ((ICollection<string>)snapshot).Add("c"); } catch (NotSupportedException) { readOnly = true; }
            Check(readOnly && !canvas.SelectedNodeIds.Contains("c"), "selection collection cannot mutate editor state");
            canvas.SelectNode("c");
            Check(snapshot.Count == 2 && canvas.SelectedNodeId == "c", "selection snapshot remains detached after later selections");
            int selectionEvents = 0;
            EventHandler countSelection = delegate { selectionEvents++; };
            canvas.SelectionChanged += countSelection;
            canvas.SelectNode("c"); canvas.SelectNodes(new[] { "c" });
            Check(selectionEvents == 0, "identical selection does not reenter inspector callbacks");
            canvas.SelectionChanged -= countSelection;
            canvas.SelectEdge("bc");
            Check(canvas.SelectedEdgeId == "bc" && canvas.SelectionCount == 0, "edge and node selections are mutually exclusive");

            Reset(canvas);
            canvas.Zoom = 0.73f;
            Point pan = new Point(40, 40);
            canvas.Down(pan, MouseButtons.Middle); canvas.MoveTo(new Point(175, 126), MouseButtons.Middle); canvas.Up(new Point(175, 126), MouseButtons.Middle);
            PointF viewBefore = canvas.ClientToWorld(new Point(150, 150));
            canvas.Marquee(-24, -24, 535, 120);
            Check(Selected(canvas, "a", "b"), "marquee hit testing uses world coordinates after zoom and pan");
            Check(canvas.ClientToWorld(new Point(150, 150)) == viewBefore && before == 0 && after == 0,
                "marquee changes neither viewport nor document/undo history");
            canvas.Modifiers = Keys.Control; canvas.ClickWorld(716, 68);
            Check(Selected(canvas, "a", "b", "c"), "Ctrl click adds a block to selection");
            canvas.ClickWorld(716, 68);
            Check(Selected(canvas, "a", "b"), "Ctrl click removes an already selected block on release");
            canvas.Modifiers = Keys.Shift; canvas.ClickWorld(716, 68); canvas.ClickWorld(716, 68);
            Check(Selected(canvas, "a", "b", "c"), "Shift click remains additive for an already selected block");
            canvas.Modifiers = Keys.Shift; canvas.Marquee(280, 250, 490, 350);
            Check(canvas.SelectionCount == 4 && canvas.SelectedNodeId == null, "Shift marquee adds its hits to existing selection");
            canvas.Modifiers = Keys.Control; canvas.ClickWorld(-100, -90);
            Check(canvas.SelectionCount == 4, "modified blank click preserves selection");
            canvas.Modifiers = Keys.None; canvas.ClickWorld(-100, -90);
            Check(canvas.SelectionCount == 0 && canvas.SelectedEdgeId == null, "plain blank click clears selection");
            canvas.SelectNode("d");
            Point first = canvas.At(-24, -24), last = canvas.At(535, 120);
            canvas.Down(first); canvas.MoveTo(last);
            Check(Selected(canvas, "a", "b"), "marquee previews replacement selection while held");
            canvas.Key(Keys.Escape); canvas.Up(last);
            Check(Selected(canvas, "d") && !canvas.Capture && before == 0 && after == 0,
                "Escape restores selection before marquee without document mutation");
            canvas.SelectEdge("bc"); canvas.Down(first); canvas.MoveTo(last); canvas.Key(Keys.Escape); canvas.Up(last);
            Check(canvas.SelectedEdgeId == "bc" && canvas.SelectionCount == 0,
                "Escape restores previous edge selection as well as node selections");
            canvas.SelectNode(null); canvas.Modifiers = Keys.Control; canvas.Marquee(535, 120, -24, -24); canvas.Modifiers = Keys.None;
            Check(Selected(canvas, "a", "b"), "reverse-direction marquee has the same hit set");

            Reset(canvas);
            canvas.SelectNodes(new[] { "a", "b" }); canvas.Zoom = 0.8f;
            FlowNode a = Node(canvas.Diagram, "a"), b = Node(canvas.Diagram, "b");
            Point origin = canvas.At(106, 44), destination = new Point(origin.X + 48, origin.Y + 38);
            canvas.Down(origin); canvas.MoveTo(destination);
            Check(Near(b.X - a.X, 300) && Near(b.Y - a.Y, 0) && a.X != 0,
                "dragging a selected block previews movement of the whole group without distortion");
            Check(before == 0 && after == 0, "drag preview does not create a premature undo record");
            canvas.Up(destination);
            Check(before == 1 && after == 1 && Selected(canvas, "a", "b"), "completed group drag creates one mutation pair and keeps group selected");
            Check(Near(Node(undoSnapshot, "a").X, 0) && Near(Node(undoSnapshot, "b").X, 300),
                "undo observer receives the exact positions before the group drag");
            Check(Near(a.X, 72) && Near(a.Y, 48) && Near(b.X, 372), "group drag snaps a common 24-unit grid offset at transformed zoom");
            canvas.Diagram = undoSnapshot;
            Check(Near(Node(canvas.Diagram, "a").X, 0) && Selected(canvas, "a", "b"), "undo replacement restores positions and surviving selected ids");
            canvas.ClickWorld(106, 44);
            Check(Selected(canvas, "a"), "click without dragging collapses a group to the clicked node");

            Reset(canvas);
            canvas.SelectNode("d"); origin = canvas.At(106, 44); destination = new Point(origin.X + 80, origin.Y + 50);
            canvas.Down(origin); canvas.MoveTo(destination); canvas.Key(Keys.Escape); canvas.Up(destination);
            Check(Selected(canvas, "d") && Near(Node(canvas.Diagram, "a").X, 0) && Near(Node(canvas.Diagram, "a").Y, 0),
                "Escape from dragging a newly selected block restores prior selection and position");
            Check(before == 0 && after == 0, "cancelled node drag creates no empty undo record");
            canvas.SelectNodes(new[] { "a", "b" }); canvas.Down(origin); canvas.MoveTo(destination); canvas.Capture = false;
            Check(Near(Node(canvas.Diagram, "a").X, 0) && Near(Node(canvas.Diagram, "b").X, 300) && before == 0 && after == 0,
                "losing mouse capture cancels a group drag atomically");
            canvas.Up(destination);

            PointF panBefore = canvas.ClientToWorld(new Point(100, 100));
            canvas.Key(Keys.Space); canvas.Down(new Point(5, 5)); canvas.MoveTo(new Point(30, 25)); canvas.Up(new Point(30, 25)); canvas.ReleaseKey(Keys.Space);
            Check(canvas.ClientToWorld(new Point(100, 100)) == panBefore && before == 0,
                "Space and left drag remains selection and cannot pan");
            canvas.SelectNodes(new[] { "a", "b" });
            canvas.Down(new Point(80, 80), MouseButtons.Right); canvas.MoveTo(new Point(200, 150), MouseButtons.Right); canvas.Up(new Point(200, 150), MouseButtons.Right);
            Check(canvas.ClientToWorld(new Point(100, 100)) == panBefore && Selected(canvas, "a", "b") && before == 0,
                "right drag cannot pan or change the selection");
            var popup=(ContextMenuStrip)typeof(FlowCanvas).GetField("_menu",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(canvas);if(popup!=null)popup.Close();
            canvas.Down(new Point(80, 80), MouseButtons.Middle); canvas.MoveTo(new Point(200, 150), MouseButtons.Middle); canvas.Up(new Point(200, 150), MouseButtons.Middle);
            Check(canvas.ClientToWorld(new Point(100, 100)) != panBefore && Selected(canvas, "a", "b") && before == 0,
                "middle drag pans while retaining multi-selection and undo history");
            Point anchor = new Point(415, 293); PointF anchorBefore = canvas.ClientToWorld(anchor); canvas.Wheel(anchor, 120);
            PointF anchorAfter = canvas.ClientToWorld(anchor);
            Check(Near(anchorBefore.X, anchorAfter.X) && Near(anchorBefore.Y, anchorAfter.Y), "wheel zoom stays anchored with a multi-selection");

            Reset(canvas); canvas.SelectNodes(new[] { "b", "c" });
            string[] originalIds = canvas.Diagram.Nodes.Select(n => n.Id).ToArray();
            FlowNode firstCopy = canvas.DuplicateSelected();
            var copies = canvas.Diagram.Nodes.Where(n => !originalIds.Contains(n.Id)).ToList();
            FlowNode copyB = copies.First(n => n.Kind == NodeKind.Process), copyC = copies.First(n => n.Kind == NodeKind.Decision);
            Check(firstCopy != null && copies.Count == 2 && canvas.SelectionCount == 2 && canvas.SelectedNodeId == null,
                "duplicate group selects the copies and returns a compatibility first-copy value");
            Check(Near(copyC.X - copyB.X, 300) && Near(copyB.X, 336) && Near(copyC.Y, 36), "duplicate preserves relative positions with one common offset");
            var newEdges = canvas.Diagram.Edges.Where(e => e.Id != "ab" && e.Id != "bc" && e.Id != "cb" && e.Id != "cd").ToList();
            Check(newEdges.Count == 2 && newEdges.Any(e => e.From == copyB.Id && e.To == copyC.Id && e.Branch == "next") &&
                newEdges.Any(e => e.From == copyC.Id && e.To == copyB.Id && e.Branch == "true"),
                "duplicate remaps internal edge endpoints and branch labels to fresh ids");
            Check(!newEdges.Any(e => originalIds.Contains(e.From) || originalIds.Contains(e.To)), "duplicate excludes incoming and outgoing edges crossing group boundary");
            Check(before == 1 && after == 1 && undoSnapshot.Nodes.Count == 4 && undoSnapshot.Edges.Count == 4,
                "group duplicate and its internal edges form one undo operation");
            canvas.DeleteSelection();
            Check(canvas.Diagram.Nodes.Count == 4 && canvas.Diagram.Edges.Count == 4 && canvas.SelectionCount == 0 && before == 2 && after == 2,
                "group delete removes all copies and incident links in one undo operation");
            canvas.SelectNodes(new[] { "b", "c" }); canvas.DeleteSelection();
            Check(canvas.Diagram.Nodes.Count == 2 && canvas.Diagram.Edges.Count == 0 && canvas.Diagram.Nodes.Any(n => n.Id == "a") && canvas.Diagram.Nodes.Any(n => n.Id == "d"),
                "deleting original group removes all cross-boundary links and retains other nodes");

            Reset(canvas); canvas.SelectNodes(new[] { "a", "b" }); a = Node(canvas.Diagram, "a"); b = Node(canvas.Diagram, "b");
            a.X = 99888; b.X = 99900;
            canvas.Key(Keys.Right | Keys.Shift);
            Check(Near(a.X, 99888) && Near(b.X, 99900) && before == 0, "group keyboard movement clamps a common delta without partial movement or empty undo");
            canvas.Key(Keys.Left);
            Check(Near(a.X, 99864) && Near(b.X, 99876) && before == 1 && after == 1 && Near(b.X - a.X, 12),
                "group arrow movement preserves spacing and emits one mutation pair");
            a.X = 99888; b.X = 99900; canvas.CenterOnNode("a");
            origin = canvas.At(a.X + 30, a.Y + 44); destination = new Point(origin.X + 240, origin.Y + 48);
            canvas.Down(origin); canvas.MoveTo(destination); canvas.Up(destination);
            Check(Near(a.X, 99888) && Near(b.X, 99900) && Near(a.Y, 48) && Near(b.Y, 48),
                "group drag at document boundary clamps one axis while allowing the other");
            canvas.DuplicateSelected();
            Check(canvas.Diagram.Nodes.All(n => Math.Abs(n.X) <= 99900 && Math.Abs(n.Y) <= 99900) && DiagramIO.Clone(canvas.Diagram).Nodes.Count == 6,
                "group duplicate at coordinate boundary remains serializable");

            Reset(canvas); canvas.SelectNodes(new[] { "a", "b" });
            for (int i = 4; i < DiagramIO.MaxNodes - 1; i++)
                canvas.Diagram.Nodes.Add(new FlowNode { Id = "capacity-" + i, Kind = NodeKind.Process, Text = "x = 0" });
            Check(canvas.DuplicateSelected() == null && canvas.Diagram.Nodes.Count == 499 && Selected(canvas, "a", "b") && before == 0 && after == 0,
                "group duplicate rejects insufficient node capacity atomically");
            Reset(canvas); canvas.SelectNodes(new[] { "b", "c" });
            for (int i = 4; i < DiagramIO.MaxEdges - 1; i++)
                canvas.Diagram.Edges.Add(new FlowEdge { Id = "capacity-edge-" + i, From = "a", To = "d", Branch = "next" });
            Check(canvas.DuplicateSelected() == null && canvas.Diagram.Nodes.Count == 4 && canvas.Diagram.Edges.Count == 1499 && before == 0 && after == 0,
                "group duplicate rejects insufficient edge capacity before adding any nodes");
            Check(DiagramIO.Clone(canvas.Diagram).Edges.Count == 1499, "capacity rejection leaves a valid save/undo document");

            Reset(canvas); canvas.Key(Keys.Control | Keys.A);
            Check(canvas.SelectionCount == canvas.Diagram.Nodes.Count && canvas.SelectedNodeId == null && before == 0,
                "Ctrl+A selects every node without modifying document");
            canvas.Key(Keys.Control | Keys.D);
            Check(canvas.SelectionCount == 4 && canvas.Diagram.Nodes.Count == 8 && canvas.Diagram.Edges.Count == 8 && before == 1 && after == 1,
                "Ctrl+D duplicates all selected nodes and their internal graph");
            canvas.Key(Keys.Delete);
            Check(canvas.Diagram.Nodes.Count == 4 && canvas.Diagram.Edges.Count == 4 && before == 2 && after == 2,
                "Delete key applies the group action exactly once");

            Reset(canvas); canvas.SelectNodes(new[] { "a", "b" });
            var replacement = Fixture(); replacement.Nodes.RemoveAll(n => n.Id == "b"); replacement.Edges.RemoveAll(e => e.From == "b" || e.To == "b");
            canvas.Diagram = replacement;
            Check(canvas.SelectionCount == 1 && canvas.SelectedNodeId == "a", "document replacement prunes stale selection ids safely");
            Console.WriteLine(passes + " selection checks passed.");
        }
    }
}

