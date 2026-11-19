using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Frociart.Tests
{
    /// <summary>
    /// Regression coverage for live WinForms dropdowns. Invoke the real
    /// ToolStripItem click/close pipeline, including nested modal message loops.
    /// These tests do not replace a hardware mouse test on a Windows PC.
    /// </summary>
    internal static class PopupTests
    {
        private static int checks;
        private static readonly List<Exception> threadErrors = new List<Exception>();
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Check(bool result, string name)
        {
            if (!result) throw new Exception("FAIL: " + name);
            checks++;
            Console.WriteLine("PASS: " + name);
        }

        private static T Field<T>(object instance, string name)
        {
            return (T)instance.GetType().GetField(name, PrivateInstance).GetValue(instance);
        }

        private static object Call(object instance, string name, params object[] args)
        {
            try { return instance.GetType().GetMethod(name, PrivateInstance | BindingFlags.DeclaredOnly).Invoke(instance, args); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        private static void Pump(int iterations = 2)
        {
            for (int i = 0; i < iterations; i++)
            {
                Application.DoEvents();
                Thread.Sleep(35);
            }
            if (threadErrors.Count > 0) throw new Exception("Unexpected UI exception", threadErrors[0]);
        }

        private static Control Find(Control parent, string prefix)
        {
            foreach (Control child in parent.Controls)
            {
                if ((child.Text ?? "").StartsWith(prefix, StringComparison.Ordinal)) return child;
                Control nested = Find(child, prefix);
                if (nested != null) return nested;
            }
            return null;
        }

        private static ContextMenuStrip MenuField(object owner)
        {
            return owner.GetType().GetFields(PrivateInstance)
                .Where(f => typeof(ContextMenuStrip).IsAssignableFrom(f.FieldType))
                .Select(f => f.GetValue(owner) as ContextMenuStrip)
                .FirstOrDefault(m => m != null);
        }

        private static ToolStripItem Item(ToolStrip menu, string prefix)
        {
            ToolStripItem item = menu.Items.Cast<ToolStripItem>()
                .FirstOrDefault(i => i.Available && (i.Text ?? "").StartsWith(prefix, StringComparison.Ordinal));
            if (item == null)
                throw new Exception("Menu item not found: " + prefix + "; available: " +
                    String.Join(", ", menu.Items.Cast<ToolStripItem>().Where(i => i.Available).Select(i => i.Text)));
            return item;
        }

        private static ContextMenuStrip More(MainForm form, string section="Progetto")
        {
            Control button = Find(form, "&"+section);
            if (button == null) throw new Exception("Altro button not found");
            Call(form, "ShowTaskMenu", section,button);
            Pump();
            ContextMenuStrip menu = MenuField(form);
            if (menu == null) throw new Exception("Main menu must be owned by the form");
            return menu;
        }

        private static ContextMenuStrip CanvasMenu(MainForm form, Point at)
        {
            Call(form.Canvas, "ShowContextMenu", at);
            Pump(3);
            return Field<ContextMenuStrip>(form.Canvas, "_menu");
        }

        private static Point At(MainForm form, string id)
        {
            FlowNode node = form.Document.Nodes.First(n => n.Id == id);
            RectangleF bounds = FlowCanvas.GetNodeBounds(node);
            return Point.Round(form.Canvas.WorldToClient(new PointF(
                bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)));
        }

        private static void Click(ToolStripItem item)
        {
            if (!item.Enabled) throw new Exception("Disabled item: " + item.Text);
            item.PerformClick();
            Pump(3);
        }

        private static void Screenshot(Form form, string path)
        {
            Pump();
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            string directory = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "FrociartPopupTests");
            Directory.CreateDirectory(directory);
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { threadErrors.Add(e.Exception); };
#if NET8_0_OR_GREATER
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                SetCursorPos(2, 2);

                Check(StartupDiagnostics.ErrorCaption(true).Contains("avvio"), "fatal error before first window is classified as startup");
                Check(StartupDiagnostics.ErrorCaption(false).EndsWith("Errore"), "recoverable error is not classified as startup");
                StartupDiagnostics.MarkWindowReady();
                Check(StartupDiagnostics.ErrorCaption(true).Contains("Arresto"), "fatal error after first window is classified as runtime");

                using (MainForm main = new MainForm(null, true))
                {
                    main.Show(); main.Hide(); main.Show(); main.Activate();
                    Pump();
                    main.Canvas.FitToView();

                    ContextMenuStrip more = More(main);
                    int count = more.Items.Count;
                    Check(more.Visible && !more.IsDisposed, "main menu opens");
                    for (int i = 0; i < 20; i++)
                    {
                        more.Close(ToolStripDropDownCloseReason.Keyboard);
                        Pump();
                        Check(!more.IsDisposed, "cancel keeps main menu alive " + (i + 1));
                        ContextMenuStrip reopened = More(main);
                        if (!Object.ReferenceEquals(more, reopened) || reopened.Items.Count != count)
                            throw new Exception("Reopening changed main menu identity or duplicated commands");
                    }
                    Check(more.Items.Count == count, "twenty reopenings do not duplicate menu commands");

                    Click(Item(more, "Rinomina"));
                    Check(!more.IsDisposed && !more.Visible, "rename click closes menu without disposing it");
                    Console.WriteLine("RENAME_DIAGNOSTIC focus="+Field<TextBox>(main,"titleEdit").Focused+" visible="+Field<TextBox>(main,"titleEdit").Visible+" enabled="+Field<TextBox>(main,"titleEdit").Enabled+" editor="+Field<Control>(main,"editorBody").Visible+" active="+(Form.ActiveForm==null?"none":Form.ActiveForm.GetType().Name));
                    Check(Field<TextBox>(main, "titleEdit").Focused, "rename action executes and focuses the title");
                    Field<TextBox>(main, "titleEdit").Text = "Menu regression";
                    main.CommitInspector();
                    Check(main.Document.Title == "Menu regression", "title edit commits");
                    Click(Item(More(main,"Modifica"), "Annulla"));
                    Check(main.Document.Title == "Pari o dispari", "Undo menu action executes once");
                    Click(Item(More(main,"Modifica"), "Ripeti"));
                    Check(main.Document.Title == "Menu regression", "Redo menu action executes once");

                    main.SetDocument(Examples.Parity(), null, false);
                    Field<TextBox>(main, "titleEdit").Text = "Titolo prima del clic";
                    main.Canvas.SelectNode("check");
                    Check(main.Document.Title == "Titolo prima del clic",
                        "selecting a block commits the previous title editor");
                    Call(main, "Undo");
                    Check(main.Document.Title == "Pari o dispari", "title committed by selection can be undone");
                    Call(main, "Redo");
                    Check(main.Document.Title == "Titolo prima del clic", "title committed by selection can be redone");
                    main.Canvas.SelectNode(null);
                    Field<TextBox>(main, "titleEdit").Text = "Titolo prima del gruppo";
                    main.Canvas.SelectNodes(new[] { "check", "input" });
                    Check(main.Document.Title == "Titolo prima del gruppo",
                        "selecting a group commits the previous title editor");
                    int historyBeforeHiddenEdit = Field<System.Collections.ICollection>(main, "undo").Count;
                    Field<TextBox>(main, "titleEdit").Text = "Campo nascosto da ignorare";
                    main.CommitInspector();
                    Check(main.Document.Title == "Titolo prima del gruppo" &&
                        Field<System.Collections.ICollection>(main, "undo").Count == historyBeforeHiddenEdit,
                        "hidden title field cannot mutate a group or add history");
                    main.Canvas.SelectNode("check");
                    Check(main.Document.Title == "Titolo prima del gruppo",
                        "leaving group selection does not commit a hidden title field");
                    main.Canvas.SelectNode(null);

                    using (DialogResponder ok = new DialogResponder(1))
                    {
                        Click(Item(More(main,"Aiuto"), "Informazioni"));
                        Check(ok.Responses > 0, "menu action can enter and return from a native modal dialog");
                    }
                    Check(!more.IsDisposed && !main.IsDisposed, "modal action keeps owner and menu usable");
                    More(main);
                    Screenshot(main, Path.Combine(directory, "01-main-menu.png"));
                    more.Close();
                    Pump();

                    main.SetDocument(Examples.Parity(), null, false);
                    main.Canvas.FitToView();
                    ContextMenuStrip canvasMenu = CanvasMenu(main, At(main, "check"));
                    Check(canvasMenu != null && canvasMenu.Visible, "canvas context menu opens on a decision");
                    for (int i = 0; i < 20; i++)
                    {
                        canvasMenu.Close(ToolStripDropDownCloseReason.Keyboard);
                        Pump();
                        ContextMenuStrip reopened = CanvasMenu(main, At(main, "check"));
                        if (!Object.ReferenceEquals(canvasMenu, reopened) || reopened.IsDisposed)
                            throw new Exception("Canvas menu was disposed/replaced on reopening " + i);
                    }
                    Check(!canvasMenu.IsDisposed, "twenty canvas cancel/reopen cycles keep menu alive");

                    int initialNodes = main.Document.Nodes.Count;
                    Click(Item(canvasMenu, "Duplica"));
                    Check(main.Document.Nodes.Count == initialNodes + 1, "canvas duplicate action executes exactly once");
                    string duplicate = main.Canvas.SelectedNodeId;
                    Check(!String.IsNullOrEmpty(duplicate), "duplicate becomes selected");
                    Click(Item(CanvasMenu(main, At(main, duplicate)), "Elimina"));
                    Check(main.Document.Nodes.Count == initialNodes, "canvas delete action removes only the selected copy");

                    canvasMenu = CanvasMenu(main, At(main, "check"));
                    Click(Item(canvasMenu, "Collega il ramo Sì"));
                    Check(main.Canvas.IsConnecting, "context menu starts the requested branch connection");
                    canvasMenu = CanvasMenu(main, new Point(15, 15));
                    Click(Item(canvasMenu, "Annulla collegamento"));
                    Check(!main.Canvas.IsConnecting, "context menu cancels a connection without disposing the popup");
                    canvasMenu = CanvasMenu(main, new Point(15, 15));
                    ToolStripMenuItem add = (ToolStripMenuItem)Item(canvasMenu, "Aggiungi");
                    add.ShowDropDown();
                    Pump();
                    Click(Item(add.DropDown, "CONDIZIONE"));
                    Check(main.Document.Nodes.Count == initialNodes + 1, "nested submenu adds exactly one block");
                    Check(main.Document.Nodes.First(n => n.Id == main.Canvas.SelectedNodeId).Kind == NodeKind.Decision,
                        "nested submenu creates the requested block type");
                    Check(!canvasMenu.IsDisposed, "nested submenu closure keeps parent popup alive");
                    CanvasMenu(main, At(main, "check"));
                    Screenshot(main, Path.Combine(directory, "02-canvas-menu.png"));
                    canvasMenu.Close();
                    Pump();

                    main.Canvas.SelectNode("check");
                    Field<TextBox>(main, "expression").Text = "numero > 0";
                    main.CommitInspector();
                    Diagram document = main.Document;
                    string currentPath = Field<string>(main, "currentPath");
                    int undoCount = Field<System.Collections.ICollection>(main, "undo").Count;
                    string invalid = Path.Combine(directory, "invalid-project.flow");
                    File.WriteAllText(invalid, "{broken json");
                    using (DialogResponder ok = new DialogResponder(1))
                    {
                        Check(!main.LoadPath(invalid), "malformed project fails cleanly");
                        Check(ok.Responses > 0, "invalid project explains the failure");
                    }
                    Check(Object.ReferenceEquals(document, main.Document) &&
                        main.Document.Nodes.First(n => n.Id == "check").Text == "numero > 0",
                        "failed open preserves current document and committed edits");
                    Check(Field<bool>(main, "dirty") && Field<string>(main, "currentPath") == currentPath &&
                        Field<System.Collections.ICollection>(main, "undo").Count == undoCount,
                        "failed open preserves dirty state, path and undo history");

                    using (DialogResponder cancel = new DialogResponder(2))
                    {
                        FormClosingEventArgs closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                        Call(main, "OnClosing", main, closing);
                        Check(cancel.Responses > 0 && closing.Cancel,
                            "safe startup mode still asks before discarding unsaved work");
                    }
                    Check(!main.IsDisposed && Field<bool>(main, "dirty"), "cancelled close keeps unsaved work open");

                    main.SetDocument(Examples.Parity(), null, false);
                    More(main);
                    main.Close();
                    Pump();
                    Check(main.IsDisposed, "clean owner closes while menu is visible");
                    Check(more.IsDisposed && canvasMenu.IsDisposed, "owner disposal releases both persistent menus");
                }
                using (MainForm recovery = new MainForm(null, true))
                {
                    string recoveryPath = Path.Combine(directory, "draft-recovery.frociart");
                    if (File.Exists(recoveryPath)) File.Delete(recoveryPath);
                    typeof(MainForm).GetField("recoveryPath", PrivateInstance).SetValue(recovery, recoveryPath);
                    typeof(MainForm).GetField("ignoreRecovery", PrivateInstance).SetValue(recovery, false);
                    recovery.Show();
                    Pump();
                    recovery.Canvas.SelectNode("check");
                    Field<TextBox>(recovery, "expression").Text = "numero >= 12";
                    Check(recovery.Text.StartsWith("*", StringComparison.Ordinal),
                        "typing an unapplied expression shows the unsaved-work indicator");
                    Call(recovery, "SaveRecovery");
                    Check(File.Exists(recoveryPath) &&
                        DiagramIO.Load(recoveryPath).Nodes.First(n => n.Id == "check").Text == "numero >= 12",
                        "recovery saves unapplied expression text");
                    recovery.Canvas.SelectNode("input");
                    Field<TextBox>(recovery, "prompt").Text = "Domanda ancora nell'ispettore";
                    Call(recovery, "SaveRecovery");
                    Check(DiagramIO.Load(recoveryPath).Nodes.First(n => n.Id == "input").Prompt ==
                        "Domanda ancora nell'ispettore", "recovery saves unapplied prompt text");
                    recovery.Canvas.SelectNode(null);
                    Field<TextBox>(recovery, "titleEdit").Text = "Titolo ancora nell'ispettore";
                    Call(recovery, "SaveRecovery");
                    Diagram restored = DiagramIO.Load(recoveryPath);
                    Check(restored.Title == "Titolo ancora nell'ispettore", "recovery saves unapplied diagram title");
                    Check(restored.Nodes.First(n => n.Id == "check").Text == "numero >= 12" &&
                        restored.Nodes.First(n => n.Id == "input").Prompt == "Domanda ancora nell'ispettore",
                        "recovery retains earlier drafts while updating a different field");

                    recovery.Canvas.SelectNode("check");
                    recovery.Canvas.FitToView();
                    FlowNode dragged = recovery.Document.Nodes.First(n => n.Id == "check");
                    PointF originalPosition = new PointF(dragged.X, dragged.Y);
                    Point press = At(recovery, "check");
                    string beforeGestureRecovery = File.ReadAllText(recoveryPath);
                    int beforeGestureHistory = Field<System.Collections.ICollection>(recovery, "undo").Count;
                    int beforeGestureNodes = recovery.Document.Nodes.Count;
                    Call(recovery.Canvas, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, press.X, press.Y, 0));
                    Call(recovery.Canvas, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, press.X + 60, press.Y + 35, 0));
                    Check(recovery.Canvas.IsPointerGestureActive &&
                        (dragged.X != originalPosition.X || dragged.Y != originalPosition.Y),
                        "drag creates a live preview before committing the gesture");
                    Call(recovery, "SaveRecovery");
                    Check(File.ReadAllText(recoveryPath) == beforeGestureRecovery &&
                        Field<System.Windows.Forms.Timer>(recovery, "recoveryTimer").Enabled,
                        "recovery postpones writes while a pointer gesture is active");
                    bool shortcutsHandled = true;
                    using (DialogResponder cancel = new DialogResponder(2))
                    {
                        foreach (Keys shortcut in new[] { Keys.Control | Keys.A, Keys.Control | Keys.D,
                            Keys.Control | Keys.S, Keys.Control | Keys.Z, Keys.Delete, Keys.F5, Keys.F6, Keys.F10 })
                        {
                            Message message = Message.Create(recovery.Handle, 0x0100, IntPtr.Zero, IntPtr.Zero);
                            shortcutsHandled &= (bool)Call(recovery, "ProcessCmdKey", message, shortcut);
                        }
                        Check(shortcutsHandled && cancel.Responses == 0 &&
                            recovery.Document.Nodes.Count == beforeGestureNodes &&
                            Field<System.Collections.ICollection>(recovery, "undo").Count == beforeGestureHistory && recovery.Runner == null,
                            "document and runtime shortcuts cannot interrupt a pointer gesture");
                    }
                    Call(recovery.Canvas, "OnKeyDown", new KeyEventArgs(Keys.Escape));
                    Check(!recovery.Canvas.IsPointerGestureActive && dragged.X == originalPosition.X &&
                        dragged.Y == originalPosition.Y, "Escape restores the preview before recovery resumes");
                    Call(recovery, "SaveRecovery");
                    Diagram afterGestureRecovery = DiagramIO.Load(recoveryPath);
                    Check(afterGestureRecovery.Nodes.First(n => n.Id == "check").X == originalPosition.X &&
                        afterGestureRecovery.Nodes.First(n => n.Id == "check").Y == originalPosition.Y,
                        "recovery never persists a cancelled drag preview");
                    recovery.SetDocument(restored, null, false);
                    recovery.Close();
                    Pump();
                    Check(!File.Exists(recoveryPath), "clean shutdown removes only the isolated test recovery file");
                }
                Check(threadErrors.Count == 0, "no unexpected UI thread exceptions");
                Console.WriteLine("POPUP REGRESSION: " + checks + " checks passed");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private sealed class DialogResponder : IDisposable
        {
            private readonly System.Windows.Forms.Timer timer;
            private readonly HashSet<IntPtr> answered = new HashSet<IntPtr>();
            private readonly int result;
            internal int Responses { get; private set; }
            internal DialogResponder(int result)
            {
                this.result = result;
                timer = new System.Windows.Forms.Timer { Interval = 70 };
                timer.Tick += Respond;
                timer.Start();
            }
            private void Respond(object sender, EventArgs e)
            {
                foreach(var dialog in Application.OpenForms.Cast<Form>().OfType<FlowMessageDialog>().ToArray())
                {
                    if(!dialog.Visible||answered.Contains(dialog.Handle))continue;
                    var choices=DialogButtons(dialog).ToArray();
                    var button=choices.FirstOrDefault(b=>(int)b.DialogResult==result);
                    if(button==null&&result==2)button=choices.FirstOrDefault(b=>b.DialogResult==DialogResult.No);
                    if(button!=null&&button.Visible&&button.Enabled){answered.Add(dialog.Handle);Responses++;button.PerformClick();}
                }
                EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr hwnd, IntPtr parameter)
                {
                    StringBuilder name = new StringBuilder(100);
                    GetClassName(hwnd, name, name.Capacity);
                    if (name.ToString() == "#32770" && IsWindowVisible(hwnd) && answered.Add(hwnd))
                    {
                        Responses++;
                        PostMessage(hwnd, 0x0111, new IntPtr(result), IntPtr.Zero);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            private static IEnumerable<Button> DialogButtons(Control parent){foreach(Control child in parent.Controls){var button=child as Button;if(button!=null)yield return button;foreach(var nested in DialogButtons(child))yield return nested;}}
            public void Dispose() { timer.Stop(); timer.Dispose(); }
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int maxCount);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
