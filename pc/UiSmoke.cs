using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Frociart.Tests
{
    internal static class UiSmoke
    {
        private static int checks;
        private static void Check(bool result,string name){if(!result)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);checks++;}
        private static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
        private static void Call(object o,string name){o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);}
        private static void Mouse(FlowCanvas canvas,string method,Point point)
        {typeof(FlowCanvas).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0)});}
        private static void Pump(){Application.DoEvents();System.Threading.Thread.Sleep(90);Application.DoEvents();}
        private static Control Find(Control parent,string text)
        {foreach(Control c in parent.Controls){if(c.Text==text)return c;var nested=Find(c,text);if(nested!=null)return nested;}return null;}
        private static void Shot(Form f,string path)
        {
            Pump();using(var bmp=new Bitmap(f.ClientSize.Width,f.ClientSize.Height))using(var g=Graphics.FromImage(bmp))
            {g.CopyFromScreen(f.PointToScreen(Point.Empty),Point.Empty,f.ClientSize);bmp.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
        }
        [STAThread]
        private static int Main(string[] args)
        {
            string dir=args.Length>0?args[0]:"/tmp/flow-ui";Directory.CreateDirectory(dir);
            try
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                using(var main=new MainForm(null,true))
                {
                    main.Show();Pump();main.Canvas.FitToView();Pump();
                    Check(main.Document.Title=="Pari o dispari","initial useful diagram");
                    Check(!Field<bool>(main,"dirty"),"initial clean document");
                    Check(Find(main,"Centra").Parent.Left>main.Canvas.Width-260,"zoom controls aligned at first launch");
                    string[] topLabels={"Nuovo","Apri","Salva","Altro  ···","Esempi","Guida"};int[] topWidths={76,68,76,87,82,70};
                    Control fileBar=Find(main,"Nuovo").Parent;
                    Check(fileBar.Controls.Count==6&&fileBar.Controls.Cast<FlowButton>().Select(b=>b.Text).SequenceEqual(topLabels)&&
                        fileBar.Controls.Cast<FlowButton>().All(b=>b.PreserveOriginalStyle)&&fileBar.Controls.Cast<Control>().Select(b=>b.Width).SequenceEqual(topWidths),
                        "top-right file buttons preserve appearance, order and widths");
                    Shot(main,Path.Combine(dir,"01-editor.png"));
                    main.Canvas.SelectNode("check");Pump();Shot(main,Path.Combine(dir,"02-block.png"));
                    Field<TextBox>(main,"expression").Text="numero > 0";main.CommitInspector();
                    Check(main.Document.Nodes.First(n=>n.Id=="check").Text=="numero > 0","inspector change applied");
                    Check(Field<bool>(main,"dirty"),"edited document dirty");
                    Call(main,"Undo");Check(main.Document.Nodes.First(n=>n.Id=="check").Text.Contains("%"),"undo restores expression");
                    Call(main,"Redo");Check(main.Document.Nodes.First(n=>n.Id=="check").Text=="numero > 0","redo reapplies expression");
                    main.SetDocument(Examples.Parity(),null,false);Pump();
                    Check(main.Verify(true),"valid diagram verification");Pump();Shot(main,Path.Combine(dir,"03-verify.png"));
                    main.SingleStep();Check(main.Runner.Steps==1,"first step executes Start");
                    main.SingleStep();Check(main.Runner.State==RuntimeState.WaitingForInput,"input waits inline");
                    Shot(main,Path.Combine(dir,"04-input.png"));Field<TextBox>(main,"inputValue").Text="7";main.SubmitInput();
                    Check((double)main.Runner.Variables["numero"]==7,"inline input reaches runtime");
                    main.SingleStep();Check(main.Runner.LastEdgeId.Contains("false"),"odd chooses No");
                    main.SingleStep();Check(main.Runner.Output.Single()=="Dispari","runtime output correct");
                    main.SingleStep();Check(main.Runner.State==RuntimeState.Completed,"runtime completes at End");
                    int finishedSteps=main.Runner.Steps;main.SingleStep();Check(main.Runner.Steps==finishedSteps&&main.Runner.State==RuntimeState.Completed,"step after End preserves output");
                    Check(Field<TextBox>(main,"output").Bottom<530,"runtime output remains visible after input collapses");
                    Shot(main,Path.Combine(dir,"05-run.png"));
                    main.Canvas.SelectNode("check");Field<TextBox>(main,"expression").Text="numero > 0";main.CommitInspector();Check(main.Runner==null,"editing invalidates previous run");
                    main.SetDocument(Examples.MissingBranch(),null,false);Pump();Check(!main.Verify(true),"missing branch blocks run");
                    Check(main.Canvas.IssueNodeIds.Contains("check"),"erroneous decision highlighted");Shot(main,Path.Combine(dir,"06-error.png"));
                    var issuePanel=Field<Panel>(main,"issuePanel");var card=issuePanel.Controls.Cast<Control>().FirstOrDefault(c=>c.Tag is ValidationIssue&&((ValidationIssue)c.Tag).NodeId=="check");
                    typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(card,new object[]{EventArgs.Empty});
                    Check(main.Canvas.SelectedNodeId=="check","click issue locates faulty block");
                    main.SetDocument(Examples.Parity(),null,false);main.Size=new Size(1120,720);Pump();main.Canvas.FitToView();Shot(main,Path.Combine(dir,"07-small.png"));
                    Check(main.Canvas.Width>=560&&main.Canvas.Height>350&&
                        main.ClientRectangle.Contains(main.RectangleToClient(main.Canvas.RectangleToScreen(main.Canvas.ClientRectangle))),
                        "minimum window keeps a usable canvas fully inside the window");
                    Check(!((ScrollableControl)Find(main,"Clicca per aggiungere").Parent).HorizontalScroll.Visible,"palette has no horizontal scroll");
                    main.Size=new Size(1420,900);Pump();
                    main.Canvas.FitToView();main.Canvas.Focus();Pump();
                    Point selectionStart=Point.Round(main.Canvas.WorldToClient(new PointF(65,310)));
                    Point selectionEnd=Point.Round(main.Canvas.WorldToClient(new PointF(880,610)));
                    Mouse(main.Canvas,"OnMouseDown",selectionStart);Mouse(main.Canvas,"OnMouseMove",selectionEnd);Pump();
                    Check(main.Canvas.SelectionCount==3,"marquee selects decision and both outputs in the real editor");
                    Check(!Field<TextBox>(main,"titleEdit").Visible&&!Field<TextBox>(main,"expression").Visible,
                        "group inspector prevents accidental title or single-node edits");
                    Check(Field<FlowButton>(main,"duplicateButton").Visible&&Field<FlowButton>(main,"deleteButton").Visible,
                        "group actions are visible together");
                    Shot(main,Path.Combine(dir,"11-selection.png"));Mouse(main.Canvas,"OnMouseUp",selectionEnd);Pump();
                    string groupTitle=main.Document.Title;int originalNodes=main.Document.Nodes.Count,originalEdges=main.Document.Edges.Count;
                    Field<FlowButton>(main,"duplicateButton").PerformClick();Pump();
                    Check(main.Document.Nodes.Count==originalNodes+3&&main.Document.Edges.Count==originalEdges+2,
                        "group button duplicates nodes and their two internal branch links");
                    Check(main.Canvas.SelectionCount==3&&main.Document.Title==groupTitle,"duplicate selects copies and preserves project title");
                    Call(main,"Undo");Check(main.Document.Nodes.Count==originalNodes&&main.Document.Edges.Count==originalEdges,
                        "one Undo restores the complete pre-duplicate graph");
                    main.Canvas.SelectNodes(new[]{"check","even","odd"});Field<FlowButton>(main,"deleteButton").PerformClick();
                    Check(main.Document.Nodes.Count==3&&main.Document.Edges.Count==1,"group deletion removes all incident links without dangling edges");
                    Call(main,"Undo");Check(main.Document.Nodes.Count==6&&main.Document.Edges.Count==6,"group deletion is restored by one Undo");
                    string project=Path.Combine(dir,"Prova salvataggio.frociart");
                    typeof(MainForm).GetField("currentPath",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(main,project);
                    bool saved=(bool)typeof(MainForm).GetMethod("SaveDocument",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{false});
                    Check(saved&&File.Exists(project)&&!Field<bool>(main,"dirty"),"Save writes the native .frociart project and clears modified state");
                    Check(DiagramIO.Load(project).Nodes.Count==6&&DiagramIO.Load(project).Edges.Count==6,"native project reopens with contents and links intact");
                    string legacy=Path.Combine(dir,"Compatibile.flow");File.Copy(project,legacy,true);
                    Check(main.LoadPath(legacy)&&main.Document.Title==groupTitle,"previous .flow documents remain openable");
                    using(var exporter=new ExportDialog(main.Canvas)){exporter.Show(main);Pump();Shot(exporter,Path.Combine(dir,"12-export.png"));exporter.Close();}
                    using(var examples=new ExamplesDialog()){examples.Show(main);Pump();Shot(examples,Path.Combine(dir,"08-examples.png"));examples.Close();}
                    using(var tests=new TestDialog(Examples.Parity()))
                    {tests.Show(main);Pump();Call(tests,"Start");for(int i=0;i<15;i++)Pump();Check(Field<TestSession>(tests,"session").Result.Passed,"test dialog executes expected case");Shot(tests,Path.Combine(dir,"09-test.png"));tests.Close();}
                    using(var guide=new GuideDialog()){guide.Show(main);Pump();Shot(guide,Path.Combine(dir,"10-guide.png"));guide.Close();}
                    main.Close();
                }
                var exact=new TestSession(Examples.Sum(),"2\n3","Somma: 5");while(!exact.Tick()){}Check(exact.Result.Passed,"test correct sum");
                var wrong=new TestSession(Examples.Sum(),"2\n3","Somma: 6");while(!wrong.Tick()){}Check(!wrong.Result.Passed&&wrong.Result.Title=="Risultato diverso","test wrong result");
                var missing=new TestSession(Examples.Sum(),"2","Somma: 5");while(!missing.Tick()){}Check(!missing.Result.Passed&&missing.Result.Title=="Manca un input","test detects missing input");
                var extra=new TestSession(Examples.Parity(),"2\n7","Pari");while(!extra.Tick()){}Check(!extra.Result.Passed&&extra.Result.Title=="Input in più","test detects unused input");
                var countdown=new TestSession(Examples.Countdown(),"3","3\n2\n1\nVia!");while(!countdown.Tick()){}Check(countdown.Result.Passed,"test finite loop");
                Console.WriteLine("UI SMOKE: "+checks+" checks passed");return 0;
            }
            catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
        }
    }
}
