// Standalone repeatable canvas benchmark. Exclude from the production executable.
// Compile with Core.cs, Examples.cs, Theme.cs, BlockPicker.cs and FlowCanvas.cs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Frociart;

public sealed class PerformanceCanvas : FlowCanvas
{
    public void PaintFrame(Graphics graphics) { OnPaint(new PaintEventArgs(graphics, ClientRectangle)); }
    public void Down(Point p, MouseButtons button) { OnMouseDown(new MouseEventArgs(button, 1, p.X, p.Y, 0)); }
    public void MoveTo(Point p, MouseButtons button) { OnMouseMove(new MouseEventArgs(button, 0, p.X, p.Y, 0)); }
    public void Up(Point p, MouseButtons button) { OnMouseUp(new MouseEventArgs(button, 1, p.X, p.Y, 0)); }
    public void Wheel(Point p, int delta) { OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, p.X, p.Y, delta)); }
}

public static class CanvasPerformanceTests
{
    private static readonly List<string> Lines = new List<string>();
    private static Func<long> allocated;
    private static void Measure(string graph, string operation, int count, Action<int> action)
    {
        for (int i = 0; i < 3; i++) action(i);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long memory = allocated == null ? 0 : allocated();
        var samples = new List<double>();
        var watch = new Stopwatch();
        for (int i = 0; i < count; i++)
        {
            watch.Restart(); action(i); watch.Stop(); samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        long bytes = allocated == null ? -1 : (allocated() - memory) / count;
        samples.Sort();
        string line = String.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F4},{4:F4},{5:F4},{6}",
            graph, operation, count, samples.Average(), samples[count / 2], samples[(int)Math.Floor((count - 1) * 0.95)], bytes);
        Lines.Add(line); Console.WriteLine(line);
    }

    public static Diagram LargeDiagram()
    {
        var diagram = new Diagram { Title = "Canvas stress: 500 nodes / 1500 edges" };
        for (int i = 0; i < 500; i++)
            diagram.Nodes.Add(new FlowNode
            {
                Id = "node-" + i, Kind = i == 0 ? NodeKind.Start : i == 499 ? NodeKind.End : i % 5 == 0 ? NodeKind.Decision : NodeKind.Process,
                Text = i % 5 == 0 ? "x > " + i : "x = " + i, X = (i % 10) * 280, Y = (i / 10) * 200
            });
        for (int i = 0; i < 1500; i++)
        {
            int source = i % 500;
            int target = i < 500 ? (source + 1) % 500 : i < 1000 ? (source + 10) % 500 : Math.Max(0, source - 20);
            diagram.Edges.Add(new FlowEdge { Id = "edge-" + i, From = "node-" + source, To = "node-" + target,
                Branch = i < 500 ? "next" : i < 1000 ? "true" : "false" });
        }
        return diagram;
    }

    private static void Benchmark(PerformanceCanvas canvas, Graphics graphics, Diagram diagram, string name)
    {
        canvas.Diagram = diagram; canvas.ResetView(); canvas.Zoom = 0.9f;
        var initial = Stopwatch.StartNew(); canvas.PaintFrame(graphics); initial.Stop();
        Console.WriteLine(name + " initial paint ms: " + initial.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        Measure(name, "render_visible", 24, delegate { canvas.PaintFrame(graphics); });
        Measure(name, "hover_hit_test", 100, delegate(int i) { canvas.MoveTo(new Point(9 + i % 3, 9 + i % 5), MouseButtons.None); });
        canvas.Down(new Point(100, 100), MouseButtons.Middle);
        Measure(name, "pan_and_render", 24, delegate(int i)
        {
            canvas.MoveTo(new Point(105 + i % 8 * 9, 105 + i % 7 * 7), MouseButtons.Middle); canvas.PaintFrame(graphics);
        });
        canvas.Up(new Point(175, 155), MouseButtons.Middle);
        canvas.ResetView(); canvas.Zoom = 0.9f;
        Measure(name, "zoom_and_render", 24, delegate(int i) { canvas.Wheel(new Point(530, 340), i % 2 == 0 ? 120 : -120); canvas.PaintFrame(graphics); });
        canvas.ResetView(); canvas.Zoom = 0.9f;
        FlowNode node = diagram.Nodes.First(n => n.Kind != NodeKind.Start && n.Kind != NodeKind.End);
        canvas.CenterOnNode(node.Id); canvas.SelectNode(node.Id);
        RectangleF nodeBounds = FlowCanvas.GetNodeBounds(node);
        Point start = Point.Round(canvas.WorldToClient(new PointF(node.X + nodeBounds.Width / 2, node.Y + nodeBounds.Height / 2)));
        canvas.Down(start, MouseButtons.Left);
        Measure(name, "drag_and_render", 16, delegate(int i)
        {
            canvas.MoveTo(new Point(start.X + 12 + (i % 7) * 9, start.Y + 12 + (i % 5) * 9), MouseButtons.Left);
            canvas.PaintFrame(graphics);
        });
        canvas.Up(new Point(start.X + 60, start.Y + 48), MouseButtons.Left);
        canvas.SelectNode(null);
        canvas.Down(new Point(100, 100), MouseButtons.Middle);
        canvas.MoveTo(new Point(-100000, -100000), MouseButtons.Middle);
        canvas.Up(new Point(-100000, -100000), MouseButtons.Middle);
        Measure(name, "render_empty_view", 24, delegate { canvas.PaintFrame(graphics); });
    }

    [STAThread] public static int Main(string[] args)
    {
        var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        if (method != null) allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        Application.EnableVisualStyles(); Ui.SetDarkMode(false, false);
        Lines.Add("graph,operation,iterations,mean_ms,median_ms,p95_ms,managed_bytes_per_operation");
        Console.WriteLine(Lines[0]);
        using (var form = new Form { ClientSize = new Size(1100, 780) })
        using (var canvas = new PerformanceCanvas { Dock = DockStyle.Fill })
        using (var bitmap = new Bitmap(1100, 780))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            form.Controls.Add(canvas); form.Show(); Application.DoEvents();
            Benchmark(canvas, graphics, Examples.Parity(), "small");
            Benchmark(canvas, graphics, LargeDiagram(), "500nodes_1500edges");
            form.Close();
        }
        if (args.Length > 0) File.WriteAllLines(args[0], Lines.ToArray());
        return 0;
    }
}
