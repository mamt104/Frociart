// End-to-end flow editing regressions; excluded from the production executable.
// Run the Windows WinForms build on Windows or through Wine. Pointer handlers are
// driven deterministically; picker choices use the real ToolStrip click pipeline.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Frociart.Tests
{
    internal static class WorkflowTests
    {
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<Exception> uiErrors = new List<Exception>();
        private static int checks;
        private static string resultDirectory;

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            checks++;
            Console.WriteLine("PASS: " + description);
        }

        private static T Field<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, PrivateInstance);
            if (field == null) throw new Exception("Missing test field: " + name);
            return (T)field.GetValue(instance);
        }

        private static object Call(object instance, string name, params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(name, PrivateInstance | BindingFlags.DeclaredOnly)
                ?? instance.GetType().GetMethod(name, PrivateInstance);
            if (method == null) throw new Exception("Missing test method: " + name);
            try { return method.Invoke(instance, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static void Pump(int iterations = 2)
        {
            for (int i = 0; i < iterations; i++)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
            if (uiErrors.Count != 0) throw new Exception("Unexpected UI-thread exception", uiErrors[0]);
        }

        private static void Mouse(FlowCanvas canvas, string method, Point point, MouseButtons button = MouseButtons.Left)
        {
            Call(canvas, method, new MouseEventArgs(button, 1, point.X, point.Y, 0));
        }

        private static void Key(FlowCanvas canvas, Keys keys) { Call(canvas, "OnKeyDown", new KeyEventArgs(keys)); }

        private static FlowNode Node(MainForm main, string id) { return main.Document.Nodes.First(n => n.Id == id); }
        private static int History(MainForm main) { return Field<System.Collections.ICollection>(main, "undo").Count; }
        private static bool Near(float left, float right) { return Math.Abs(left - right) < 0.02f; }
        private static bool SamePoint(PointF left, PointF right) { return Near(left.X, right.X) && Near(left.Y, right.Y); }

        private static string Signature(Diagram diagram)
        {
            return diagram.Version + "|" + diagram.Title + "\n" +
                String.Join("\n", diagram.Nodes.OrderBy(n => n.Id).Select(n =>
                    n.Id + "|" + n.Kind + "|" + n.Text + "|" + n.Prompt + "|" +
                    n.X.ToString("R", CultureInfo.InvariantCulture) + "|" + n.Y.ToString("R", CultureInfo.InvariantCulture))) + "\n--\n" +
                String.Join("\n", diagram.Edges.OrderBy(e => e.Id).Select(e => e.Id + "|" + e.From + "|" + e.To + "|" + e.Branch));
        }

        private static Diagram Fixture(string title = "Workflow")
        {
            Diagram diagram = new Diagram { Title = title };
            diagram.Nodes.Add(new FlowNode { Id = "start", Kind = NodeKind.Start, Text = "Inizio", X = 320, Y = 80 });
            diagram.Nodes.Add(new FlowNode { Id = "end", Kind = NodeKind.End, Text = "Fine", X = 320, Y = 570 });
            diagram.Edges.Add(new FlowEdge { Id = "start-end", From = "start", To = "end", Branch = "next" });
            return diagram;
        }

        private static void Set(MainForm main, Diagram diagram, string source)
        {
            main.SetDocument(diagram, null, false);
            Pump();
            main.Canvas.SelectNode(source);
            Pump();
            main.Canvas.FitToView();
            Pump();
        }

        private static Point At(MainForm main, string id)
        {
            RectangleF bounds = FlowCanvas.GetNodeBounds(Node(main, id));
            return Point.Round(main.Canvas.WorldToClient(new PointF(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)));
        }

        private static Point Port(MainForm main, string id, string branch)
        {
            RectangleF bounds = FlowCanvas.GetNodeBounds(Node(main, id));
            PointF position = branch == "true" ? new PointF(bounds.Left, bounds.Top + bounds.Height / 2) :
                branch == "false" ? new PointF(bounds.Right, bounds.Top + bounds.Height / 2) :
                new PointF(bounds.Left + bounds.Width / 2, bounds.Bottom);
            return Point.Round(main.Canvas.WorldToClient(position));
        }

        private static Point Empty(MainForm main)
        {
            foreach (float x in new[] { 0.76f, 0.22f, 0.88f, 0.12f, 0.5f })
                foreach (float y in new[] { 0.5f, 0.72f, 0.30f })
                {
                    Point point = new Point((int)(main.Canvas.ClientSize.Width * x), (int)(main.Canvas.ClientSize.Height * y));
                    PointF world = main.Canvas.ClientToWorld(point);
                    if (!main.Document.Nodes.Any(n =>
                    {
                        RectangleF bounds = FlowCanvas.GetNodeBounds(n);
                        bounds.Inflate(36, 36);
                        return bounds.Contains(world);
                    })) return point;
                }
            throw new Exception("No empty test point in viewport");
        }

        private static ContextMenuStrip Picker(MainForm main)
        {
            return Field<ContextMenuStrip>(main.Canvas, "_blockPicker");
        }

        private static ToolStripItem Choice(ContextMenuStrip menu, NodeKind kind)
        {
            ToolStripItem item = menu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Tag is NodeKind && (NodeKind)i.Tag == kind);
            if (item == null) throw new Exception("Missing picker choice: " + kind);
            return item;
        }

        private static ContextMenuStrip OpenPicker(MainForm main, string source, string branch, bool byDrag = true, bool pump = true)
        {
            main.Canvas.SelectNode(source);
            Pump();
            Point destination = Empty(main);
            if (byDrag)
            {
                Mouse(main.Canvas, "OnMouseDown", Port(main, source, branch));
                Mouse(main.Canvas, "OnMouseMove", destination);
                Mouse(main.Canvas, "OnMouseUp", destination);
            }
            else
            {
                main.Canvas.BeginConnect(source, branch);
                Mouse(main.Canvas, "OnMouseDown", destination);
                Mouse(main.Canvas, "OnMouseUp", destination);
            }
            if (pump) Pump(3);
            return Picker(main);
        }

        private static FlowNode Choose(MainForm main, NodeKind kind)
        {
            ContextMenuStrip picker = Picker(main);
            ToolStripItem item = Choice(picker, kind);
            if (!item.Available || !item.Enabled) throw new Exception("Unavailable picker choice: " + kind);
            string[] existingIds = main.Document.Nodes.Select(n => n.Id).ToArray();
            item.PerformClick();
            Pump(3);
            return main.Document.Nodes.Single(n => !existingIds.Contains(n.Id));
        }

        private static void Shot(MainForm main, string file)
        {
            Pump();
            using (Bitmap bitmap = new Bitmap(main.ClientSize.Width, main.ClientSize.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(main.PointToScreen(Point.Empty), Point.Empty, main.ClientSize);
                bitmap.Save(Path.Combine(resultDirectory, file), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static Control FindControl(Control parent, string prefix)
        {
            foreach (Control child in parent.Controls)
            {
                if ((child.Text ?? String.Empty).StartsWith(prefix, StringComparison.Ordinal)) return child;
                Control found = FindControl(child, prefix);
                if (found != null) return found;
            }
            return null;
        }

        private static ToolStripItem Command(ContextMenuStrip menu, string prefix)
        {
            ToolStripItem result = menu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Available &&
                (i.Text ?? String.Empty).StartsWith(prefix, StringComparison.Ordinal));
            if (result == null) throw new Exception("Missing menu command: " + prefix);
            return result;
        }

        private static ContextMenuStrip More(MainForm main, string section="Progetto")
        {
            Control control = FindControl(main, "&"+section);
            if (control == null) throw new Exception("Main menu button not found");
            Call(main, "ShowTaskMenu",section,control);
            Pump();
            return Field<ContextMenuStrip>(main, "moreMenu");
        }

        private static ContextMenuStrip Context(MainForm main, string id)
        {
            Call(main.Canvas, "ShowContextMenu", At(main, id));
            Pump(3);
            return Field<ContextMenuStrip>(main.Canvas, "_menu");
        }

        private static void InsertEditHistory(MainForm main)
        {
            Set(main, Fixture(), "start");
            string original = Signature(main.Document);
            Point destination = Empty(main);
            Mouse(main.Canvas, "OnMouseDown", Port(main, "start", "next"));
            Check(main.Canvas.IsConnecting && main.Canvas.IsPointerGestureActive,
                "pressing an output port starts a guarded connection gesture");
            Mouse(main.Canvas, "OnMouseMove", destination);
            Check(Signature(main.Document) == original && History(main) == 0,
                "dragging an arrow previews without changing the existing branch or history");
            Mouse(main.Canvas, "OnMouseUp", destination);
            Pump(3);
            ContextMenuStrip picker = Picker(main);
            Check(main.Canvas.IsBlockPickerOpen && picker.Visible && !main.Canvas.IsPointerGestureActive,
                "releasing an arrow on empty space opens the block picker");
            Check(picker.Items.Cast<ToolStripItem>().Where(i => i.Available && i.Tag is NodeKind)
                .Select(i => (NodeKind)i.Tag).OrderBy(k => k).SequenceEqual(
                    (IoInstruction.Separate?new[] { NodeKind.Input,NodeKind.Output,NodeKind.Process,NodeKind.Declare,NodeKind.Decision,NodeKind.Call,NodeKind.Pause,NodeKind.End }:new[] { NodeKind.Input,NodeKind.Process,NodeKind.Declare,NodeKind.Decision,NodeKind.Call,NodeKind.Pause,NodeKind.End }).OrderBy(k => k)),
                "picker offers every current core destination kind and honors unified or split Input / Output");
            Check(Signature(main.Document) == original && History(main) == 0,
                "opening the picker leaves the existing connection intact");
            Shot(main, "01-arrow-picker.png");
            FlowNode added = Choose(main, NodeKind.Process);
            string addedId = added.Id;
            string defaultText = added.Text;
            Check(main.Document.Nodes.Count == 3 && main.Document.Edges.Count == 2 &&
                main.Document.Edges.Single(e=>e.From=="start").To == addedId &&
                main.Document.Edges.Single(e=>e.From==addedId).To == "end" &&
                main.Document.Edges.Single(e=>e.From=="start").Branch == "next",
                "one picker choice inserts a block and preserves the previous successor with both arrows");
            Check(History(main) == 1, "new block and automatic connection create one Undo record");
            Check(!main.Canvas.IsBlockPickerOpen && !main.Canvas.IsConnecting && !picker.IsDisposed,
                "choosing closes the persistent picker and clears connection state");
            Check(main.Canvas.SelectedNodeId == addedId && Field<TextBox>(main, "expression").Visible,
                "newly connected block is selected and its inspector is visible");
            string beforeDoubleClick=Signature(main.Document);
            Point editPoint=At(main,addedId);
            Mouse(main.Canvas,"OnMouseDown",editPoint);Mouse(main.Canvas,"OnMouseUp",editPoint);
            Mouse(main.Canvas,"OnMouseDown",editPoint);
            Call(main.Canvas,"OnMouseDoubleClick",new MouseEventArgs(MouseButtons.Left,2,editPoint.X,editPoint.Y,0));
            Mouse(main.Canvas,"OnMouseUp",editPoint);Pump();
            Check(Signature(main.Document)==beforeDoubleClick&&History(main)==1,"complete double-click sequence preserves block position, connections and history");
            Check(Field<TextBox>(main, "expression").Focused,
                "double-click opens the new block editor for typing");
            Field<TextBox>(main, "expression").Text = "totale = 2 + 3";
            main.CommitInspector();
            Check(Node(main, addedId).Text == "totale = 2 + 3" && History(main) == 2,
                "editing the inserted block is a separate undoable action");
            Shot(main, "02-connected-editor.png");
            Call(main, "Undo");
            Check(Node(main, addedId).Text == defaultText && main.Document.Edges.Count==2 && main.Document.Edges.Single(e=>e.From=="start").To == addedId && main.Document.Edges.Single(e=>e.From==addedId).To=="end",
                "first Undo restores text while retaining the inserted block and its arrow");
            Call(main, "Undo");
            Check(Signature(main.Document) == original, "second Undo atomically restores the exact pre-insertion graph");
            Call(main, "Redo");
            Check(main.Document.Nodes.Count == 3 && Node(main, addedId).Text == defaultText &&
                main.Document.Edges.Count==2 && main.Document.Edges.Single(e=>e.From=="start").To == addedId && main.Document.Edges.Single(e=>e.From==addedId).To=="end", "Redo restores the same block ID and both automatic connections together");
            Call(main, "Redo");
            Check(Node(main, addedId).Text == "totale = 2 + 3", "second Redo restores the block edit");

            main.Canvas.BeginConnect(addedId, "next");
            Mouse(main.Canvas, "OnMouseDown", At(main, "end"));
            Mouse(main.Canvas, "OnMouseUp", At(main, "end"));
            Pump();
            Check(main.Document.Nodes.Count == 3 && main.Document.Edges.Count == 2 &&
                !main.Canvas.IsBlockPickerOpen && !main.Canvas.IsConnecting,
                "choosing an existing destination connects directly without creating another block");
            Check(main.Verify(false), "the diagram built through the picker passes verification");
            main.SingleStep(); main.SingleStep(); main.SingleStep();
            Check(main.Runner != null && main.Runner.State == RuntimeState.Completed &&
                (double)main.Runner.Variables["totale"] == 5,
                "the newly built and edited diagram executes correctly");
            string path = Path.Combine(resultDirectory, "Diagramma creato con freccia.frociart");
            typeof(MainForm).GetField("currentPath", PrivateInstance).SetValue(main, path);
            Check((bool)Call(main, "SaveDocument", false) && File.Exists(path) &&
                Signature(DiagramIO.Load(path)) == Signature(main.Document),
                "native project save preserves the block, branch and edited expression");
            string beforeExport = Signature(main.Document), selectedBeforeExport = main.Canvas.SelectedNodeId;
            Interpreter runnerBeforeExport = main.Runner;
            string svg = Path.Combine(resultDirectory, "Workflow esportato.svg");
            DiagramExport.ExportSvg(main.Canvas, svg);
            Check(File.Exists(svg) && File.ReadAllText(svg).Contains("totale = 2 + 3") &&
                Signature(main.Document) == beforeExport && main.Canvas.SelectedNodeId == selectedBeforeExport &&
                Object.ReferenceEquals(main.Runner, runnerBeforeExport),
                "export keeps the edited diagram, current selection and completed execution intact");
        }

        private static void FileCommands(MainForm main)
        {
            Control first = FindControl(main, "&Progetto");
            FlowButton[] buttons = first.Parent.Controls.OfType<FlowButton>().Where(b => b.Name.StartsWith("Task", StringComparison.Ordinal)).ToArray();
            string[] labels = { "&Progetto  ▾", "&Modifica  ▾", "&Esegui  ▾", "&Codice  ▾", "&Vista  ▾", "&Aiuto  ▾" };
            int[] widths = { 106, 100, 90, 90, 90, 90 };
            float scale; using (Graphics graphics = main.CreateGraphics()) scale = graphics.DpiX / 96f;
            Check(buttons.Select(b => b.Text).SequenceEqual(labels), "the six activity menus retain their labels and predictable order");
            Check(buttons.Select((b, i) => Math.Abs(b.Width - Math.Round(widths[i] * scale)) <= 1 &&
                Math.Abs(b.Height - Math.Round(34 * scale)) <= 1).All(x => x),
                "activity buttons retain measured logical widths and34-pixel height");
            bool original = Ui.IsDark;
            Ui.SetDarkMode(!original, false); Pump();
            Check(buttons.All(b => !b.PreserveOriginalStyle) && buttons.Select(b => b.Text).SequenceEqual(labels),
                "activity buttons retain shared app styling and order across theme changes");
            Ui.SetDarkMode(original, false); Pump();
        }

        private static Diagram RuntimeFixture()
        {
            Diagram d = new Diagram { Title = "Streaming e variabili" };
            string[] ids = { "start", "setup", "change", "first", "second", "end" };
            NodeKind[] kinds = { NodeKind.Start, NodeKind.Process, NodeKind.Process, NodeKind.Output, NodeKind.Output, NodeKind.End };
            string[] text = { "Inizio", "zeta = 1; alfa = 2; testo = \"" + new string('x', 5000) + "\"",
                "alfa = 3; beta = 4", "\"inizio:\" + testo + testo", "\"fine:\" + testo + testo", "Fine" };
            for (int i = 0; i < ids.Length; i++)
            {
                d.Nodes.Add(new FlowNode { Id = ids[i], Kind = kinds[i], Text = text[i], X = 320, Y = 50 + i * 145 });
                if (i > 0) d.Edges.Add(new FlowEdge { From = ids[i - 1], To = ids[i] });
            }
            return d;
        }

        private static void RuntimeUpdates(MainForm main)
        {
            Set(main, RuntimeFixture(), "setup");
            main.SingleStep(); main.SingleStep(); Pump();
            ListView variables = Field<ListView>(main, "variables");
            TextBox output = Field<TextBox>(main, "output");
            Check(variables.Items.Cast<ListViewItem>().Select(i => i.Text).SequenceEqual(new[] { "alfa", "testo", "zeta" }),
                "runtime adds variable rows in alphabetical order");
            ListViewItem alfa = variables.Items.Cast<ListViewItem>().Single(i => i.Text == "alfa");
            alfa.Selected = true; alfa.Focused = true; variables.Focus();
            for (int i = 0; i < 3; i++) Call(main, "RefreshRuntime");
            Pump();
            Check(Object.ReferenceEquals(alfa, variables.Items[0]) && alfa.Selected && alfa.Focused,
                "unchanged runtime refresh preserves the existing selected and focused native row");
            main.SingleStep(); Pump();
            Check(variables.Items.Cast<ListViewItem>().Select(i => i.Text).SequenceEqual(new[] { "alfa", "beta", "testo", "zeta" }) &&
                Object.ReferenceEquals(alfa, variables.Items[0]) && alfa.SubItems[1].Text == "3" && alfa.Selected,
                "an assignment updates one row and inserts a new variable without replacing the selected row");
            Check(main.Canvas.SelectedNodeId == "setup" && main.Runner.LastNodeId=="change" && main.Canvas.CurrentNodeId == main.Runner.CurrentNodeId &&
                main.Canvas.CurrentEdgeId == main.Runner.LastEdgeId,
                "paused execution highlights its next instruction and traversed edge without changing editor selection");
            main.SingleStep(); Pump();
            Check(output.TextLength > 8192 && output.Text == main.Runner.Output.Single(),
                "the first streamed output line is complete beyond the former 8192-character textbox limit");
            output.Focus(); output.Select(4, 20);
            main.SingleStep(); Pump();
            Check(output.Text == String.Join(Environment.NewLine, main.Runner.Output) && output.TextLength > 20000,
                "appending another long line preserves every character and the exact newline separator");
            Check(output.SelectionStart == 4 && output.SelectionLength == 20,
                "appended output preserves a user's active text selection");
            string text = output.Text;
            main.SingleStep(); Call(main, "RefreshRuntime"); Pump();
            Check(main.Runner.State == RuntimeState.Completed && output.Text == text && variables.Items.Count == 4 &&
                main.Canvas.CurrentNodeId == "end", "completion and a repeated refresh retain output, variables and the final-node highlight");
            Interpreter old = main.Runner;
            old.Reset(); Call(main, "RefreshRuntime"); Pump();
            Check(variables.Items.Count == 0 && output.TextLength == 0,
                "resetting the same runtime removes vanished variables and old output from the cached display");
            Call(main, "ResetRun", false); main.SingleStep(); Pump();
            Check(!Object.ReferenceEquals(main.Runner, old) && variables.Items.Count == 0 && output.TextLength == 0,
                "Ricomincia creates a fresh runtime without carrying over rows or output");
            main.SingleStep(); Pump();
            Check(variables.Items.Cast<ListViewItem>().Select(i => i.Text).SequenceEqual(new[] { "alfa", "testo", "zeta" }) &&
                variables.Items[0].SubItems[1].Text == "2", "fresh execution rebuilds only the variables assigned so far");
            Call(main, "ResetRun", false); Pump();
            Check(main.Runner == null && variables.Items.Count == 0 && output.TextLength == 0 &&
                main.Canvas.CurrentNodeId == null && main.Canvas.CurrentEdgeId == null,
                "clearing execution resets output, rows and canvas highlights together");
        }

        private static void ThemeDraftsAndInput(MainForm main)
        {
            bool theme = Ui.IsDark;
            try
            {
                Set(main, Examples.Sum(), "sum");
                TextBox expression = Field<TextBox>(main, "expression");
                string originalExpression = Node(main, "sum").Text;
                expression.Text = "somma = a + b + 0"; expression.Focus(); expression.Select(8, 5);
                Ui.SetDarkMode(!Ui.IsDark, false); Pump();
                Check(expression.Text == "somma = a + b + 0" && expression.SelectionStart == 8 && expression.SelectionLength == 5 &&
                    Node(main, "sum").Text == originalExpression && History(main) == 0,
                    "changing theme preserves an uncommitted expression and caret without committing a new Undo action");
                main.CommitInspector(); main.Canvas.SelectNode("input_a"); Pump();
                TextBox prompt = Field<TextBox>(main, "prompt"); prompt.Text = "Primo valore con virgola";
                Ui.SetDarkMode(!Ui.IsDark, false); Pump();
                Check(prompt.Text == "Primo valore con virgola", "changing theme preserves the input prompt draft");
                main.CommitInspector();
                main.SingleStep(); main.SingleStep(); Pump();
                TextBox input = Field<TextBox>(main, "inputValue"); input.Text = "12,5"; input.Select(1, 2); input.Focus();
                Interpreter waiting = main.Runner; int steps = waiting.Steps;
                Ui.SetDarkMode(!Ui.IsDark, false); Call(main, "RefreshRuntime"); Pump();
                Check(Object.ReferenceEquals(main.Runner, waiting) && waiting.State == RuntimeState.WaitingForInput && waiting.Steps == steps &&
                    input.Text == "12,5" && input.SelectionStart == 1 && input.SelectionLength == 2,
                    "changing theme while waiting preserves the entered value, caret, runtime identity and step count");
                main.SubmitInput(); main.SingleStep(); input.Text = "2"; main.SubmitInput();
                main.SingleStep(); main.SingleStep(); main.SingleStep(); Pump();
                Check(main.Runner.State == RuntimeState.Completed && main.Runner.Output.Single() == "Somma: 14.5",
                    "the preserved input and edited expression execute correctly after theme changes");
            }
            finally { Ui.SetDarkMode(theme, false); }
        }

        private static Diagram IssueCapacityFixture()
        {
            Diagram d = new Diagram { Title = "Diagnosi al limite di capacità" };
            for (int i = 0; i < 500; i++) d.Nodes.Add(new FlowNode { Id = "n" + i,
                Kind = i == 0 ? NodeKind.Start : i == 499 ? NodeKind.End : NodeKind.Process,
                Text = i == 0 ? "Inizio" : i == 499 ? "Fine" : "v" + i + " = " + i,
                X = i % 20 * 270, Y = i / 20 * 180 });
            for (int i = 0; i < 499; i++) d.Edges.Add(new FlowEdge { Id = "e" + i, From = "n" + i, To = "n" + (i + 1) });
            for (int i = 499; i < 1500; i++) d.Edges.Add(new FlowEdge { Id = "extra" + i, From = "n" + (1 + i % 498), To = "n" + (1 + i * 17 % 499) });
            return d;
        }

        private static void VirtualIssues(MainForm main)
        {
            main.SetDocument(IssueCapacityFixture(), null, false); main.Verify(true); Pump();
            ValidationIssuesView view = (ValidationIssuesView)Field<Panel>(main, "issuePanel");
            List<ValidationIssue> issues = Field<List<ValidationIssue>>(main, "issues");
            Check(issues.Count == 499 && view.IssueCount == 499 && view.Controls.Count == 0,
                "499 actual capacity diagnostics use one virtual list without per-issue child controls");
            Check(view.AccessibilityObject.Role == AccessibleRole.List && view.AccessibilityObject.GetChildCount() == 499 &&
                view.AccessibilityObject.GetChild(498).Name.Contains(issues[498].Message),
                "all virtual diagnostics remain exposed with their messages through accessibility");
            view.Focus(); Call(view, "OnKeyDown", new KeyEventArgs(Keys.End)); Pump();
            AccessibleObject last = view.AccessibilityObject.GetSelected();
            Check(Field<int>(view, "selected") == 498 && view.AutoScrollPosition.Y < 0 && last != null &&
                (last.State & AccessibleStates.Offscreen) == 0,
                "End selects and scrolls the final diagnostic into the visible viewport");
            Call(view, "OnKeyDown", new KeyEventArgs(Keys.Enter)); Pump();
            Check(main.Canvas.SelectedNodeId == issues[498].NodeId &&
                Field<SidebarTabs>(main, "tabs").SelectedTab == Field<Panel>(main, "blockTab"),
                "Enter on the final diagnostic selects the correct block and opens its inspector");
            main.Verify(true); Pump();
            Rectangle bounds = (Rectangle)Call(view, "BoundsFor", 0);
            Call(view, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, bounds.Left + 20, bounds.Top + 20, 0)); Pump();
            Check(main.Canvas.SelectedNodeId == issues[0].NodeId, "clicking a painted diagnostic activates its matching block");
            main.Verify(true); Pump();
            AccessibleObject child = view.AccessibilityObject.GetChild(250);
            child.Select(AccessibleSelection.TakeFocus | AccessibleSelection.TakeSelection); child.DoDefaultAction(); Pump();
            Check(main.Canvas.SelectedNodeId == issues[250].NodeId,
                "the accessibility default action can reach a diagnostic outside the initial viewport");
            main.SetDocument(Fixture(), null, false); Pump();
            Check(view.IssueCount == 0 && view.AccessibilityObject.GetChildCount() == 0,
                "replacing the document clears cached and accessible diagnostics");
        }

        private static void CancellationAndStaleCallbacks(MainForm main)
        {
            Set(main, Examples.Parity(), "check");
            string original = Signature(main.Document);
            ContextMenuStrip picker = OpenPicker(main, "check", "false");
            Check(main.Canvas.ConnectionSourceId == "check" && main.Canvas.ConnectionBranch == "false",
                "picker retains the requested decision branch");
            for (int attempt = 0; attempt < 8; attempt++)
            {
                picker.Close(ToolStripDropDownCloseReason.Keyboard);
                Pump();
                Check(Signature(main.Document) == original && History(main) == 0 &&
                    !main.Canvas.IsBlockPickerOpen && !main.Canvas.IsConnecting && !picker.IsDisposed,
                    "picker cancel preserves graph and history, cycle " + (attempt + 1));
                ContextMenuStrip reopened = OpenPicker(main, "check", "false", attempt % 2 == 0);
                if (!Object.ReferenceEquals(picker, reopened)) throw new Exception("Picker instance replaced during reopen");
            }
            string yesId = main.Document.Edges.Single(e => e.From == "check" && e.Branch == "true").Id;
            string yesTarget = main.Document.Edges.Single(e => e.From == "check" && e.Branch == "true").To;
            FlowNode output = Choose(main, NodeKind.Output);
            Check(main.Document.Edges.Single(e => e.From == "check" && e.Branch == "true").Id == yesId &&
                main.Document.Edges.Single(e => e.From == "check" && e.Branch == "true").To == yesTarget &&
                main.Document.Edges.Single(e => e.From == "check" && e.Branch == "false").To == output.Id,
                "creating on No preserves the existing Yes branch exactly");
            Call(main, "Undo");
            Check(Signature(main.Document) == original, "one Undo restores the replaced decision branch and removes its new block");

            Set(main, Fixture("Prima del cambio"), "start");
            OpenPicker(main, "start", "next", true, false);
            main.SetDocument(Fixture("Documento successivo"), null, false);
            Pump(3);
            Check(!main.Canvas.IsBlockPickerOpen && main.Document.Title == "Documento successivo" &&
                main.Document.Nodes.Count == 2 && History(main) == 0,
                "document replacement invalidates a queued picker opening");
            main.Canvas.SelectNode("start"); Pump();
            picker = OpenPicker(main, "start", "next");
            ToolStripItem staleChoice = Choice(picker, NodeKind.Input);
            main.SetDocument(Fixture("Altro documento con gli stessi ID"), null, false);
            Pump();
            string replacement = Signature(main.Document);
            staleChoice.PerformClick();
            Pump();
            Check(Signature(main.Document) == replacement && History(main) == 0 && !main.Canvas.IsBlockPickerOpen,
                "a stale picker callback cannot insert into a replacement document with reused node IDs");
        }

        private static void PointerMappings(MainForm main)
        {
            Set(main, Fixture(), "start");
            main.Canvas.SelectNodes(new[] { "start", "end" });
            Pump();
            string original = Signature(main.Document);
            Point anchor = new Point(90, 90);
            PointF view = main.Canvas.ClientToWorld(anchor);
            Mouse(main.Canvas, "OnMouseDown", new Point(20, 20), MouseButtons.Middle);
            Mouse(main.Canvas, "OnMouseMove", new Point(46, 38), MouseButtons.Middle);
            Mouse(main.Canvas, "OnMouseUp", new Point(46, 38), MouseButtons.Middle);
            Check(!SamePoint(view, main.Canvas.ClientToWorld(anchor)) && main.Canvas.SelectionCount == 2 &&
                Signature(main.Document) == original && History(main) == 0,
                "middle drag pans while preserving group selection, diagram and Undo history");
            view = main.Canvas.ClientToWorld(anchor);
            Mouse(main.Canvas, "OnMouseDown", new Point(20, 20), MouseButtons.Right);
            Mouse(main.Canvas, "OnMouseMove", new Point(120, 95), MouseButtons.Right);
            Mouse(main.Canvas, "OnMouseUp", new Point(120, 95), MouseButtons.Right);
            Check(SamePoint(view, main.Canvas.ClientToWorld(anchor)) && Signature(main.Document) == original,
                "right drag cannot pan or modify the document");

            ContextMenuStrip pointerMenu=Field<ContextMenuStrip>(main.Canvas,"_menu");if(pointerMenu!=null)pointerMenu.Close();Pump();
            RectangleF all = RectangleF.Union(FlowCanvas.GetNodeBounds(Node(main, "start")), FlowCanvas.GetNodeBounds(Node(main, "end")));
            all.Inflate(18, 18);
            Point first = Point.Round(main.Canvas.WorldToClient(all.Location));
            Point last = Point.Round(main.Canvas.WorldToClient(new PointF(all.Right, all.Bottom)));
            Mouse(main.Canvas, "OnMouseDown", first);
            Mouse(main.Canvas, "OnMouseMove", last);
            Check(main.Canvas.IsPointerGestureActive && main.Canvas.SelectionCount == 2,
                "left background drag previews a rectangle selection");
            Shot(main, "03-left-marquee.png");
            Mouse(main.Canvas, "OnMouseUp", last);
            Check(SamePoint(view, main.Canvas.ClientToWorld(anchor)) && History(main) == 0 &&
                Signature(main.Document) == original && !main.Canvas.IsPointerGestureActive,
                "left rectangle selection leaves viewport and document unchanged");
            Key(main.Canvas, Keys.Space);
            Mouse(main.Canvas, "OnMouseDown", first);
            Mouse(main.Canvas, "OnMouseMove", last);
            Mouse(main.Canvas, "OnMouseUp", last);
            Call(main.Canvas, "OnKeyUp", new KeyEventArgs(Keys.Space));
            Check(SamePoint(view, main.Canvas.ClientToWorld(anchor)) && main.Canvas.SelectionCount == 2 &&
                History(main) == 0, "Space plus left drag still selects instead of panning");
        }

        private static void RecoveryAndCapacity(MainForm main)
        {
            Set(main, Fixture(), "start");
            string path = Field<string>(main,"recoveryPath");
            typeof(MainForm).GetField("ignoreRecovery", PrivateInstance).SetValue(main, false);
            Field<TextBox>(main, "expression").Text = "Partenza modificata";
            main.CommitInspector();
            Call(main, "SaveRecovery");
            string saved = File.ReadAllText(path);
            int history = History(main);
            Point destination = Empty(main);
            Mouse(main.Canvas, "OnMouseDown", Port(main, "start", "next"));
            Mouse(main.Canvas, "OnMouseMove", destination);
            Call(main, "SaveRecovery");
            Check(main.Canvas.IsPointerGestureActive && File.ReadAllText(path) == saved &&
                Field<System.Windows.Forms.Timer>(main, "recoveryTimer").Enabled && History(main) == history,
                "recovery waits for an active arrow drag without touching saved work or history");
            Key(main.Canvas, Keys.Escape);
            Mouse(main.Canvas, "OnMouseUp", destination);
            Pump();
            Call(main, "SaveRecovery");
            Check(!main.Canvas.IsConnecting && !main.Canvas.IsBlockPickerOpen &&
                Signature(DiagramIO.Load(path)) == Signature(main.Document),
                "Escape cancels arrow creation and recovery contains only committed changes");
            typeof(MainForm).GetField("ignoreRecovery", PrivateInstance).SetValue(main, true);
            Field<System.Windows.Forms.Timer>(main, "recoveryTimer").Stop();

            Diagram full = Fixture("Capacità blocchi");
            for (int i = full.Nodes.Count; i < DiagramIO.MaxNodes; i++)
                full.Nodes.Add(new FlowNode { Id = "extra-" + i, Kind = NodeKind.Process, Text = "x = 0", X = 10000, Y = 10000 });
            Set(main, full, "start");
            main.Canvas.Zoom = 0.8f; main.Canvas.CenterOnNode("start"); Pump();
            string original = Signature(main.Document);
            OpenPicker(main, "start", "next");
            if (main.Canvas.IsBlockPickerOpen)
            {
                ToolStripItem choice = Choice(Picker(main), NodeKind.Process);
                if (choice.Enabled && choice.Available) choice.PerformClick();
            }
            Pump();
            Check(Signature(main.Document) == original && History(main) == 0,
                "node capacity rejects automatic creation without deleting the existing branch");
            main.Canvas.CancelBlockPicker();

            Diagram edgeFull = Fixture("Capacità collegamenti");
            edgeFull.Nodes.Add(new FlowNode { Id = "filler", Kind = NodeKind.Process, Text = "x = 0", X = 10000, Y = 10000 });
            for (int i = edgeFull.Edges.Count; i < DiagramIO.MaxEdges; i++)
                edgeFull.Edges.Add(new FlowEdge { Id = "filler-edge-" + i, From = "filler", To = "end", Branch = "next" });
            Set(main, edgeFull, "start");
            main.Canvas.Zoom = 0.8f; main.Canvas.CenterOnNode("start"); Pump();
            original=Signature(main.Document);
            OpenPicker(main,"start","next");
            ToolStripItem fullChoice=Choice(Picker(main),NodeKind.Process);if(fullChoice.Enabled&&fullChoice.Available)fullChoice.PerformClick();Pump();
            Check(Signature(main.Document)==original&&History(main)==0,"insertion at edge capacity rejects an extra continuation atomically without losing the existing path");
            main.Canvas.CancelBlockPicker();
            // Replacing with Fine requires no extra edge; ask the same real confirmation as the product.
            Exception answerError=null;bool sawConfirmation=false;
            using(var answer=new System.Windows.Forms.Timer{Interval=20}){
                answer.Tick+=delegate{
                    Form dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=main&&FindControl(f,"Aggiungi Fine")!=null);
                    if(dialog==null)return;answer.Stop();sawConfirmation=true;
                    try{((IButtonControl)FindControl(dialog,"Aggiungi Fine")).PerformClick();}catch(Exception ex){answerError=ex;dialog.Close();}
                };
                answer.Start();OpenPicker(main,"start","next");FlowNode terminal=Choose(main,NodeKind.End);
                if(answerError!=null)throw answerError;
                Check(sawConfirmation&&main.Document.Edges.Count==DiagramIO.MaxEdges&&main.Document.Nodes.Count==4&&main.Document.Edges.Single(e=>e.From=="start").To==terminal.Id&&Node(main,"end")!=null&&History(main)==1,"confirmed Fine can replace an existing branch at edge capacity while retaining the former successor block");
            }
            Call(main,"Undo");Check(Signature(main.Document)==original,"Undo at edge capacity restores every original node and edge exactly");
            main.Document.Edges.RemoveAt(main.Document.Edges.Count-1);main.SetDocument(DiagramIO.Clone(main.Document),null,false);main.Canvas.SelectNode("start");main.Canvas.Zoom=.8f;main.Canvas.CenterOnNode("start");Pump();
            string oneSlot=Signature(main.Document);OpenPicker(main,"start","next");FlowNode replacement=Choose(main,NodeKind.Process);
            Check(main.Document.Edges.Count==DiagramIO.MaxEdges&&main.Document.Nodes.Count==4&&main.Document.Edges.Single(e=>e.From=="start").To==replacement.Id&&main.Document.Edges.Single(e=>e.From==replacement.Id).To=="end"&&History(main)==1,"one free edge slot permits insertion with both incoming and continuation arrows");
            Call(main,"Undo");Check(Signature(main.Document)==oneSlot,"Undo restores every original edge ID and node after insertion in the last free slot");
            main.Document.Edges.Add(new FlowEdge{Id="restore-filler-slot",From="filler",To="end",Branch="next"});
            main.Document.Edges.RemoveAll(e => e.From == "start");
            main.Document.Edges.Add(new FlowEdge { Id = "last-filler", From = "filler", To = "end", Branch = "next" });
            main.SetDocument(DiagramIO.Clone(main.Document), null, false); Pump();
            main.Canvas.SelectNode("start"); Pump();
            main.Canvas.Zoom = 0.8f; main.Canvas.CenterOnNode("start"); Pump();
            original = Signature(main.Document);
            OpenPicker(main, "start", "next");
            if (main.Canvas.IsBlockPickerOpen)
            {
                ToolStripItem choice = Choice(Picker(main), NodeKind.Input);
                if (choice.Enabled && choice.Available) choice.PerformClick();
            }
            Pump();
            Check(Signature(main.Document) == original && History(main) == 0,
                "edge capacity rejects a new branch before creating an orphan block");
            main.Canvas.CancelBlockPicker();
        }

        private static void MenuThemeLifecycle(MainForm main)
        {
            bool originalTheme = Ui.IsDark;
            try
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    Set(main, Fixture("Menu e tema " + cycle), "start");
                    string original = Signature(main.Document);
                    ContextMenuStrip more = More(main);
                    Ui.SetDarkMode(!Ui.IsDark, false);
                    Pump();
                    Command(more, "Rinomina").PerformClick();
                    Pump();
                    Check(!more.IsDisposed && !more.Visible && Signature(main.Document) == original,
                        "main menu click survives a theme change while open, cycle " + (cycle + 1));
                    ContextMenuStrip context = Context(main, "start");
                    Ui.SetDarkMode(!Ui.IsDark, false);
                    Pump();
                    Command(context, "Duplica").PerformClick();
                    Pump();
                    string duplicate = main.Canvas.SelectedNodeId;
                    Check(!context.IsDisposed && main.Document.Nodes.Count == 3 && duplicate != "start",
                        "canvas menu executes one duplicate after changing theme, cycle " + (cycle + 1));
                    Command(Context(main, duplicate), "Elimina").PerformClick();
                    Pump();
                    Check(!context.IsDisposed && Signature(main.Document) == original,
                        "reopened canvas menu deletes only the copy, cycle " + (cycle + 1));

                    ContextMenuStrip picker = OpenPicker(main, "start", "next");
                    Ui.SetDarkMode(!Ui.IsDark, false);
                    Pump();
                    FlowNode added = Choose(main, NodeKind.Process);
                    Check(!picker.IsDisposed && main.Document.Nodes.Count == 3 &&
                        main.Document.Edges.Count==2 && main.Document.Edges.Single(e=>e.From=="start").To == added.Id && main.Document.Edges.Single(e=>e.From==added.Id).To=="end",
                        "picker click inserts exactly one block with both connections after changing theme, cycle " + (cycle + 1));
                    Command(More(main,"Modifica"), "Annulla").PerformClick();
                    Pump();
                    Check(Signature(main.Document) == original,
                        "main menu Undo restores the whole creation after theme changes, cycle " + (cycle + 1));
                    ContextMenuStrip reopened = OpenPicker(main, "start", "next");
                    Ui.SetDarkMode(!Ui.IsDark, false);
                    reopened.Close(ToolStripDropDownCloseReason.Keyboard);
                    Pump();
                    Check(Object.ReferenceEquals(picker, reopened) && !picker.IsDisposed &&
                        Signature(main.Document) == original && !main.Canvas.IsConnecting,
                        "picker cancels and reopens safely across theme changes, cycle " + (cycle + 1));
                }
            }
            finally { Ui.SetDarkMode(originalTheme, false); }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            resultDirectory = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "FrociartWorkflowTests");
            Directory.CreateDirectory(resultDirectory);
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { uiErrors.Add(e.Exception); };
#if NET8_0_OR_GREATER
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
                Application.EnableVisualStyles();
                Ui.SetSeparateIo(true);
                Application.SetCompatibleTextRenderingDefault(false);
                SetCursorPos(2, 2);
                using (MainForm main = new MainForm(null, true))
                {
                    main.Show();main.Hide();main.Show();main.Activate();Pump();
                    FileCommands(main);
                    InsertEditHistory(main);
                    RuntimeUpdates(main);
                    ThemeDraftsAndInput(main);
                    VirtualIssues(main);
                    CancellationAndStaleCallbacks(main);
                    PointerMappings(main);
                    RecoveryAndCapacity(main);
                    MenuThemeLifecycle(main);
                    Set(main, Fixture("Chiusura"), "start");
                    ContextMenuStrip picker = OpenPicker(main, "start", "next");
                    main.Close(); Pump();
                    Check(main.IsDisposed && picker.IsDisposed, "closing the owner safely disposes an open block picker");
                }
                Check(uiErrors.Count == 0, "workflow produces no unexpected UI-thread exceptions");
                Console.WriteLine("WORKFLOW REGRESSION: " + checks + " checks passed");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
    }
}
