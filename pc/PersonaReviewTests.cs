using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace Frociart.Tests {
 internal static class PersonaReviewTests {
  static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  static int checks;static string directory;
  static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
  static T Field<T>(object o,string n){return (T)o.GetType().GetField(n,flags).GetValue(o);}
  static object Call(object o,string n,params object[] args){var method=o.GetType().GetMethods(flags).First(x=>x.Name==n&&x.GetParameters().Length==args.Length);return method.Invoke(o,args);}
  static void Pump(){for(int i=0;i<5;i++)Application.DoEvents();}
  static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var nested in All(child))yield return nested;}
  static void Capture(Form f,string name){Pump();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(Path.Combine(directory,name+".png"));}}
  static void Key(FlowCanvas c,Keys key){typeof(FlowCanvas).GetMethod("OnKeyDown",flags).Invoke(c,new object[]{new KeyEventArgs(key)});}
  static void Mouse(FlowCanvas c,string method,MouseButtons button,Point p){typeof(FlowCanvas).GetMethod(method,flags).Invoke(c,new object[]{new MouseEventArgs(button,1,p.X,p.Y,0)});}
  static string Run(Diagram d,params string[] inputs){var runner=new Interpreter(d);int i=0;for(int n=0;n<100&&runner.State!=RuntimeState.Completed&&runner.State!=RuntimeState.Error;n++){runner.Step();if(runner.State==RuntimeState.WaitingForInput)runner.ProvideInput(inputs[i++]);}Check(runner.State==RuntimeState.Completed,"saved diagram executes");return String.Join("|",runner.Output);}
  [STAThread]static int Main(){try{
   directory=Path.Combine(Environment.GetEnvironmentVariable("REVIEW_OUTPUT"),"personas");Directory.CreateDirectory(directory);Application.EnableVisualStyles();UiMotion.SetEnabled(false);Ui.SetDarkMode(false,false);
   // Expert: choose, edit, rename, undo/redo, save/reopen, execute, use the larger canvas.
   using(var f=new MainForm(null,true)){
    f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-2500,-2000);f.Show();Pump();f.EnterEditor();f.Size=new Size(1120,720);f.SetDocument(Examples.Sum(),null,false);f.Canvas.SelectNode("output");Pump();
    var edit=Field<TextBox>(f,"expression");var apply=Field<FlowButton>(f,"applyButton");Check(!apply.Enabled,"unchanged block has no redundant apply action");edit.Text="\"a: \" + a";Check(apply.Enabled,"editing enables the commit action");
    Check(edit.Bottom<apply.Top&&apply.Bottom<=apply.Parent.ClientSize.Height,"content and apply visible at minimum size");apply.PerformClick();f.CommitInspector();
    Check((bool)Call(f,"ApplyVariableRename","a","primo"),"safe variable rename in free editor");Check(f.Document.Nodes.First(n=>n.Id=="output").Text=="\"a: \" + primo","rename preserves literal and updates reference");
    Call(f,"Undo");Check(f.Document.Nodes.First(n=>n.Id=="input_a").Text=="a","rename undo atomic");Call(f,"Redo");Check(f.Document.Nodes.First(n=>n.Id=="input_a").Text=="primo","rename redo atomic");
    int width=f.Canvas.Width;var before=DiagramIO.Clone(f.Document);Call(f,"SetCanvasFocus",true);Pump();Check(f.Canvas.Width>width+400&&!Field<SidebarTabs>(f,"tabs").Visible,"focus mode actually gives panel width to canvas");Capture(f,"expert-focus");
    Check(f.Document.Nodes.Zip(before.Nodes,(a,b)=>a.X==b.X&&a.Y==b.Y&&a.Text==b.Text).All(x=>x),"focus mode does not move or edit blocks");
    f.StartRun();Call(f,"PauseRun");Pump();Console.WriteLine("FOCUS="+Field<bool>(f,"canvasFocus")+" sidebar="+Field<SidebarTabs>(f,"tabs").Visible+" body="+Field<TableLayoutPanel>(f,"editorBody").Visible+" gesture="+f.Canvas.IsPointerGestureActive);Check(!Field<bool>(f,"canvasFocus")&&Field<SidebarTabs>(f,"tabs").Visible,"execution restores data and output panel");
    Call(f,"SetCanvasFocus",true);var doublePoint=Point.Round(f.Canvas.WorldToClient(new PointF(f.Document.Nodes[0].X+80,f.Document.Nodes[0].Y+25)));Mouse(f.Canvas,"OnMouseDown",MouseButtons.Left,doublePoint);typeof(FlowCanvas).GetMethod("OnMouseDoubleClick",flags).Invoke(f.Canvas,new object[]{new MouseEventArgs(MouseButtons.Left,2,doublePoint.X,doublePoint.Y,0)});Mouse(f.Canvas,"OnMouseUp",MouseButtons.Left,doublePoint);Check(!Field<bool>(f,"canvasFocus"),"double click restores block editing after the second release");
    string file=Path.Combine(directory,"expert.frociart");DiagramIO.Save(file,f.Document);var read=DiagramIO.Load(file);Check(Run(read,"2","3")=="a: 2","renamed saved graph keeps its logic");
    Capture(f,"expert-minimum");Ui.SetDarkMode(true,false);Pump();Capture(f,"expert-dark");Ui.SetDarkMode(false,false);f.SetDocument(Examples.Parity(),null,false);f.Close();
   }
   // Beginner: use guided blocks, enter Prova, provide inputs, observe state and return safely.
   string statePath=Path.Combine(directory,"learner-state");var store=new TutorStore(statePath);var state=store.Load("workshop",Examples.Sum());state.Document=Examples.Sum();Check(store.Save(state),"prepare isolated beginner workspace");
   using(var f=new TutorDialog(store,new LearningStore(Path.Combine(directory,"legacy.json")))){
    f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-2500,-2000);f.Show();Pump();f.OpenWorkshop();Pump();f.Canvas.SelectNode("output");var tabs=Field<TabControl>(f,"inspectorTabs");tabs.SelectedIndex=1;Pump();
    Check(f.Canvas.ViewOnly&&f.Canvas.SelectedNodeId==null,"trial locks editing and clears confusing selection highlight");Check(Field<FlowButton>(f,"finishTrial").Visible,"visible explicit trial exit");
    Check(Field<Label>(f,"trialState").Visible&&Field<Label>(f,"trialState").Text.Contains("Pronto"),"readable ready state");
    Check(Field<Label>(f,"trialOutputLabel").Text.Contains("Output"),"empty output explains when it appears");
    int nodes=f.Current.Document.Nodes.Count;Key(f.Canvas,Keys.Control|Keys.A);Key(f.Canvas,Keys.Delete);Key(f.Canvas,Keys.Control|Keys.D);Check(f.Current.Document.Nodes.Count==nodes,"trial cannot delete or duplicate by keyboard");
    var first=f.Current.Document.Nodes[0];var old=new PointF(first.X,first.Y);var click=Point.Round(f.Canvas.WorldToClient(new PointF(first.X+80,first.Y+25)));Mouse(f.Canvas,"OnMouseDown",MouseButtons.Left,click);Mouse(f.Canvas,"OnMouseMove",MouseButtons.Left,new Point(click.X+70,click.Y+40));Mouse(f.Canvas,"OnMouseUp",MouseButtons.Left,new Point(click.X+70,click.Y+40));Check(first.X==old.X&&first.Y==old.Y,"trial cannot drag blocks");
    var origin=f.Canvas.WorldToClient(PointF.Empty);Mouse(f.Canvas,"OnMouseDown",MouseButtons.Middle,new Point(200,200));Mouse(f.Canvas,"OnMouseMove",MouseButtons.Middle,new Point(250,225));Mouse(f.Canvas,"OnMouseUp",MouseButtons.Middle,new Point(250,225));var moved=f.Canvas.WorldToClient(PointF.Empty);Check(moved.X-origin.X==50&&moved.Y-origin.Y==25,"trial still pans with middle button");
    Call(f,"StepRunner");Call(f,"StepRunner");var runner=Field<Interpreter>(f,"runner");Check(runner.State==RuntimeState.WaitingForInput,"beginner reaches a real input");Check(Field<Label>(f,"trialState").Text.Contains("Inserisci"),"waiting state tells learner what to do");Field<TextBox>(f,"runInput").Text="2";Call(f,"SubmitRun");
    Call(f,"StepRunner");Field<TextBox>(f,"runInput").Text="3";Call(f,"SubmitRun");for(int i=0;i<8&&runner.State!=RuntimeState.Completed;i++)Call(f,"StepRunner");
    Check(runner.State==RuntimeState.Completed&&Field<TextBox>(f,"runOutput").Text=="Somma: 5","real input/output path is visible");Check(Field<Label>(f,"trialState").Text=="Prova terminata"&&Field<Label>(f,"trialState").ForeColor==Ui.Green,"completion has explicit text and color");
    f.Size=new Size(1050,720);Pump();Capture(f,"beginner-trial-minimum");Field<FlowButton>(f,"finishTrial").PerformClick();Pump();Check(!f.Canvas.ViewOnly&&f.Canvas.SelectedNodeId=="output"&&tabs.SelectedIndex==0,"finish trial restores editing and original selection");
    Field<TextBox>(f,"expression").Text="\"bozza conservata\"";Call(f,"SaveSession");f.Close();
   }
   using(var f=new TutorDialog(new TutorStore(statePath),new LearningStore(Path.Combine(directory,"legacy.json")))){
    f.Show();Pump();f.OpenWorkshop();Pump();Check(f.Current.Drafts.Any(d=>d.Text=="\"bozza conservata\""),"pending text survives restart");Call(f,"OpenInFreeEditor");Check(f.EditorDocument!=null&&f.EditorDocument.Nodes.Any(n=>n.Text=="\"bozza conservata\""),"handoff applies and preserves actual learner graph");Check(!Object.ReferenceEquals(f.EditorDocument,f.Current.Document),"free editor receives independent copy");
   }
   Console.WriteLine("PASS PERSONA REVIEW "+checks+" checks. Simulated beginner/expert walkthroughs on real Windows controls; no external human participants.");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
