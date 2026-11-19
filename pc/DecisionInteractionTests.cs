using System;using System.Collections;using System.Collections.Generic;using System.Diagnostics;using System.Drawing;using System.IO;using System.Linq;using System.Reflection;using System.Threading;using System.Windows.Forms;
namespace Frociart.Tests {internal static class DecisionInteractionTests {
 static int checks;static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Flags).Invoke(owner,args);}
 static T Get<T>(object owner,string name){return(T)owner.GetType().GetField(name,Flags).GetValue(owner);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
 static void Pump(int ms=40){var clock=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(2);}while(clock.ElapsedMilliseconds<ms);}
 static void Mouse(FlowCanvas c,string name,Point p){Call(c,name,new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));Pump();}
 static string Links(Diagram d){return String.Join("|",d.Edges.Select(e=>e.Id+":"+e.From+":"+e.To+":"+e.Branch));}
 static Diagram Fixture(){var d=Examples.Parity();var q=d.Nodes.First(n=>n.Id=="check");q.TrueColor="#365CD9";q.FalseColor="#BA3058";foreach(var edge in d.Edges.Where(e=>e.From==q.Id))edge.To="even";return d;}
 static object Visual(FlowCanvas c,string branch){Call(c,"EnsureScene");return Get<IEnumerable>(c,"_edgeVisuals").Cast<object>().First(v=>Get<FlowEdge>(v,"Model").From=="check"&&Get<FlowEdge>(v,"Model").Branch==branch);}
 static Point Handle(FlowCanvas c,string branch){return Point.Round(c.WorldToClient((PointF)Call(c,"ArrowHandle",Visual(c,branch))));}
 static void Reset(MainForm form){form.SetDocument(Fixture(),null,false);form.Canvas.FitToView();form.Canvas.SelectNode("check");Pump(400);}
 [STAThread]static int Main(){try{Application.EnableVisualStyles();UiMotion.SetEnabled(true);using(var form=new MainForm(null,true)){
  form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Bounds=new Rectangle(-3000,-2500,1420,1000);form.Show();form.EnterEditor();Reset(form);var c=form.Canvas;
  var yes=Handle(c,"true");var no=Handle(c,"false");Check(no.X-yes.X>=34&&Math.Abs(yes.Y-no.Y)<=1,"Converging branches have distinct left and right screen targets");
  var q=form.Document.Nodes.First(n=>n.Id=="check");Check((Color)Call(c,"BranchLabelColor",q,"true")==ColorTranslator.FromHtml(q.TrueColor)&&(Color)Call(c,"BranchLabelColor",q,"false")==ColorTranslator.FromHtml(q.FalseColor),"Handle colours follow both custom branch colours");
  string folder=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"decision-fixes");Directory.CreateDirectory(folder);using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);b.Save(Path.Combine(folder,"01-converging-handles.png"));}
  foreach(string branch in new[]{"true","false"}){
   Reset(form);string before=Links(form.Document);int nodes=form.Document.Nodes.Count;Point from=Handle(c,branch),blank=Point.Round(c.WorldToClient(new PointF(1200,200)));Mouse(c,"OnMouseDown",from);Mouse(c,"OnMouseMove",blank);Mouse(c,"OnMouseUp",blank);Pump(380);
   var picker=Get<BlockPicker>(c,"_blockPicker");Check(picker!=null&&picker.Visible,"Existing "+branch+" arrow dropped in blank space opens the real picker");Check(Links(form.Document)==before&&Get<System.Collections.ICollection>(form,"undo").Count==0,"Picker preview preserves graph and history");Call(c,"CancelBlockPicker");Pump();Check(Links(form.Document)==before,"Closing picker preserves old continuation");
   c.SelectNode("check");from=Handle(c,branch);Mouse(c,"OnMouseDown",from);Mouse(c,"OnMouseMove",blank);Mouse(c,"OnMouseUp",blank);Pump(380);Call(c,"CreateConnectedBlock",NodeKind.Input);Pump(440);
   var source=form.Document.Edges.First(e=>e.From=="check"&&e.Branch==branch);var tail=form.Document.Edges.FirstOrDefault(e=>e.From==source.To&&e.Branch=="next");Check(form.Document.Nodes.Count==nodes+1&&tail!=null&&tail.To=="even","New node is spliced before the old branch destination");Check(form.Document.Edges.First(e=>e.From=="check"&&e.Branch!=(branch)).To=="even","The other branch remains attached to its own target");Check(Get<System.Collections.ICollection>(form,"undo").Count==1,"Branch insertion is one Undo");Call(form,"Undo");Pump();Check(Links(form.Document)==before&&form.Document.Nodes.Count==nodes,"Undo restores exact branch identities and removes only the new block");
  }
  Check(UiMotion.Spring(0)==0&&UiMotion.Spring(1)==1&&Enumerable.Range(1,99).Max(i=>UiMotion.Spring(i/100f))>1,"Shared spring has exact endpoints and a real controlled rebound");
  using(var motion=new MotionValue(c)){motion.Set(1,350);Pump(80);Call(motion,"Sample");float current=motion.Value;motion.Set(0,350);Console.WriteLine("SPRING_RETARGET before="+current+" after="+motion.Value+" velocity="+Get<float>(motion,"velocity")+" enabled="+UiMotion.Enabled);Check(!UiMotion.Enabled||Math.Abs(motion.Value-current)<.07f&&Get<float>(motion,"velocity")>0,"Retargeting a spring preserves position and incoming velocity");Pump(430);Check(motion.Value==0&&!motion.Running,"Interrupted spring settles exactly and stops its timer");}
  form.SetDocument(new Diagram(),null,false);form.Close();Pump(400);
 }Console.WriteLine("DECISION_INTERACTION_FINISHED checks="+checks+" errors=0");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
}}
