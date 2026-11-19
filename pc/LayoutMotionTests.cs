using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace Frociart.Tests {
 internal sealed class MotionCanvas:FlowCanvas {internal void Frame(Graphics g){OnPaint(new PaintEventArgs(g,ClientRectangle));}}
 internal static class LayoutMotionTests {
  static int checks;
  static string folder=Environment.GetEnvironmentVariable("QA_OUTPUT");
  static void Check(bool good,string name){if(!good)throw new Exception(name);checks++;}
  static FlowNode N(Diagram d,string id,NodeKind kind=NodeKind.Process){var n=new FlowNode{Id=id,Kind=kind,Text=id};d.Nodes.Add(n);return n;}
  static void E(Diagram d,string a,string b,string branch="next"){d.Edges.Add(new FlowEdge{Id=a+branch+b,From=a,To=b,Branch=branch});}
  static FlowNode Get(Diagram d,string id){return d.Nodes.First(n=>n.Id==id);}
  static float X(FlowNode n){return n.X+FlowCanvas.GetNodeBounds(n).Width/2;}
  static void Shape(Diagram d){
   string before=String.Join("|",d.Edges.Select(e=>e.Id+":"+e.From+":"+e.To+":"+e.Branch));
   var texts=d.Nodes.Select(n=>n.Id+":"+n.Text+":"+n.TrueLabel+":"+n.FalseLabel).ToArray();
   Tutor.Arrange(d);
   Check(before==String.Join("|",d.Edges.Select(e=>e.Id+":"+e.From+":"+e.To+":"+e.Branch)),"graph unchanged");
   Check(texts.SequenceEqual(d.Nodes.Select(n=>n.Id+":"+n.Text+":"+n.TrueLabel+":"+n.FalseLabel)),"contents unchanged");
   Check(d.Nodes.All(n=>!Single.IsNaN(n.X)&&!Single.IsInfinity(n.Y)&&n.X>=0&&n.Y>=0),"finite positive coordinates");
   foreach(var a in d.Nodes)foreach(var b in d.Nodes)if(String.CompareOrdinal(a.Id,b.Id)<0)Check(!FlowCanvas.GetNodeBounds(a).IntersectsWith(FlowCanvas.GetNodeBounds(b)),"no overlap "+a.Id+" "+b.Id);
   var positions=d.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));Tutor.Arrange(d);
   Check(d.Nodes.All(n=>positions[n.Id]==new PointF(n.X,n.Y)),"idempotent");
   d.Nodes.Reverse();d.Edges.Reverse();Tutor.Arrange(d);Check(d.Nodes.All(n=>positions[n.Id]==new PointF(n.X,n.Y)),"independent of list order");
  }
  static void Render(Diagram d,string name){using(var c=new FlowCanvas()){c.Diagram=d;using(var bmp=c.RenderExportBitmap(1))bmp.Save(Path.Combine(folder,name+".png"));}}
  static void Portable(Diagram d,string label){
   var encoded=DiagramIO.EncodeDocument(d);var reopened=DiagramIO.DecodeDocument(encoded);
   Check(reopened.Nodes.Count==d.Nodes.Count&&reopened.Edges.Count==d.Edges.Count,label+" reopens with all nodes/edges");
   var positions=d.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));Tutor.Arrange(d);
   Check(d.Nodes.All(n=>positions[n.Id]==new PointF(n.X,n.Y)),label+" repeat is stable");
   for(int i=0;i<d.Nodes.Count;i++)for(int j=i+1;j<d.Nodes.Count;j++)Check(!FlowCanvas.GetNodeBounds(d.Nodes[i]).IntersectsWith(FlowCanvas.GetNodeBounds(d.Nodes[j])),label+" no overlap");
  }
  static Diagram Asymmetric(){var d=new Diagram();N(d,"s",NodeKind.Start);N(d,"d",NodeKind.Decision);N(d,"a");N(d,"a2");N(d,"a3");N(d,"b");N(d,"j",NodeKind.Output);N(d,"e",NodeKind.End);E(d,"s","d");E(d,"d","a","true");E(d,"d","b","false");E(d,"a","a2");E(d,"a2","a3");E(d,"a3","j");E(d,"b","j");E(d,"j","e");return d;}
  [STAThread]static int Main(){try{
   Directory.CreateDirectory(folder);Application.EnableVisualStyles();
   foreach(NodeKind kind in Enum.GetValues(typeof(NodeKind))) {
    var newKind=new Diagram();N(newKind,"start",NodeKind.Start);
    N(newKind,"new",kind).Text="Contenuto lungo del nuovo blocco per verificare le dimensioni\nseconda riga";
    N(newKind,"end",NodeKind.End);
    if(kind==NodeKind.Note){Get(newKind,"new").X=900;Get(newKind,"new").Y=900;E(newKind,"start","end");}
    else{E(newKind,"start","new");E(newKind,"new","end");}
    FlowAnnotations.Promote(newKind);Shape(newKind);Render(newKind,"kind-"+kind);
    Check(Get(newKind,"end").Y>FlowCanvas.GetNodeBounds(Get(newKind,kind==NodeKind.Note?"start":"new")).Bottom,"executable block clears following node "+kind);
    if(kind==NodeKind.Note)Check(Get(newKind,"new").X==900&&Get(newKind,"new").Y==900&&newKind.Edges.All(e=>e.From!="new"&&e.To!="new"),"Standalone note remains fixed and outside the execution path");
   }
   var allKinds=new Diagram();N(allKinds,"start",NodeKind.Start);N(allKinds,"decision",NodeKind.Decision);E(allKinds,"start","decision");
   string previous="decision";foreach(NodeKind kind in Enum.GetValues(typeof(NodeKind)))if(kind>=NodeKind.Call&&kind!=NodeKind.Note){string id="new"+(int)kind;N(allKinds,id,kind);E(allKinds,previous,id,previous=="decision"?"true":"next");previous=id;}
   N(allKinds,"end",NodeKind.End);E(allKinds,"decision","end","false");E(allKinds,previous,"decision");var allNote=N(allKinds,"annotation",NodeKind.Note);allNote.X=2500;allNote.Y=200;FlowAnnotations.Promote(allKinds);Shape(allKinds);Render(allKinds,"new-block-loop");
   var parity=Examples.Parity();Shape(parity);
   var decision=Get(parity,"check");var even=Get(parity,"even");var odd=Get(parity,"odd");var end=Get(parity,"end");
   Check(Math.Abs(X(even)+X(odd)-2*X(decision))<.01,"symmetric branches");
   Check(even.Y==odd.Y,"parallel arms aligned");Check(X(end)==X(decision)&&X(Get(parity,"start"))==X(decision),"centered entry and merge");Render(parity,"parity");
   var unequal=Asymmetric();Shape(unequal);Check(Get(unequal,"j").Y>FlowCanvas.GetNodeBounds(Get(unequal,"a3")).Bottom,"join below longest arm");Check(X(Get(unequal,"j"))==X(Get(unequal,"d")),"asymmetric join centered");Render(unequal,"unequal");
   var nested=Asymmetric();Get(nested,"a2").Kind=NodeKind.Decision;N(nested,"inner");E(nested,"a2","inner","false");nested.Edges.First(e=>e.From=="a2").Branch="true";E(nested,"inner","j");Shape(nested);Render(nested,"nested");
   var loop=Examples.Countdown();Shape(loop);Render(loop,"loop");
   var loose=Asymmetric();N(loose,"loose",NodeKind.Input);N(loose,"self",NodeKind.Decision);E(loose,"self","self","true");E(loose,"dangling","missing");Shape(loose);
   var large=new Diagram();for(int i=0;i<500;i++){N(large,"n"+i,i==0?NodeKind.Start:NodeKind.Process);if(i>0)E(large,"n"+(i-1),"n"+i);}var watch=Stopwatch.StartNew();Shape(large);watch.Stop();Console.WriteLine("500-block layout including pairwise tests: "+watch.ElapsedMilliseconds+" ms");
   var islands=new Diagram();for(int i=0;i<500;i++)N(islands,"island"+i,(NodeKind)(6+i%11));Tutor.Arrange(islands);
   Check(islands.Nodes.All(n=>Math.Abs(n.X)<=100000&&Math.Abs(n.Y)<=100000),"500 disconnected new blocks retain portable coordinates");
   Check(DiagramIO.EncodeDocument(islands).Length>0,"500 disconnected new blocks can be saved after Riordina");
   Portable(islands,"500 disconnected new blocks");
   var tall=new Diagram();for(int i=0;i<500;i++){var n=N(tall,"tall"+i,(NodeKind)(6+i%11));n.Text=String.Join("\n",Enumerable.Repeat("Blocco esteso",30));if(i>0)E(tall,"tall"+(i-1),n.Id);}Tutor.Arrange(tall);
   Check(tall.Nodes.All(n=>Math.Abs(n.X)<=100000&&Math.Abs(n.Y)<=100000),"tall new blocks retain portable coordinates");
   Check(DiagramIO.EncodeDocument(tall).Length>0,"tall new blocks can be saved after Riordina");
   Portable(tall,"500 tall new blocks");
   var impossible=new Diagram();string giant=String.Concat(Enumerable.Repeat("a\n",3900));for(int i=0;i<300;i++){var n=N(impossible,"giant"+i,NodeKind.Decision);n.Text=giant;n.X=13;n.Y=29;}
   Check(DiagramIO.EncodeDocument(impossible).Length>0,"oversized layout fixture is a valid saveable input");
   bool refused=false;try{Tutor.Arrange(impossible);}catch(FlowException){refused=true;}
   Check(refused,"unpackable diagram returns a recoverable error");Check(impossible.Nodes.All(n=>n.X==13&&n.Y==29),"failed layout never partially changes coordinates");
   // Render the real WinForms canvas. QA explicitly enables motion for animation checks.
   UiMotion.SetEnabled(true);typeof(UiMotion).GetField("systemAnimations",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
   using(var form=new Form{ClientSize=new Size(960,800),ShowInTaskbar=false})using(var c=new MotionCanvas{Dock=DockStyle.Fill}){
    form.Controls.Add(c);form.Show();Application.DoEvents();c.Diagram=parity;c.FitToView();
    var edge=parity.Edges.First(e=>e.From=="check"&&e.Branch=="true");c.AnimateFlowStep(edge.Id,1200);
    var timer=(System.Windows.Forms.Timer)typeof(FlowCanvas).GetField("_pulseTimer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);Check(timer.Interval==10,"timer below Windows default quantum");
    var progress=new List<float>();var paints=new List<double>();using(var bmp=new Bitmap(c.Width,c.Height))using(var g=Graphics.FromImage(bmp)){
     for(int i=0;i<65;i++){Application.DoEvents();watch.Restart();c.Frame(g);watch.Stop();paints.Add(watch.Elapsed.TotalMilliseconds);progress.Add(c.FlowProgress);if(i%4==0)bmp.Save(Path.Combine(folder,"fill-"+(i/4).ToString("00")+".png"));Thread.Sleep(10);}
    }
    Check(progress.Zip(progress.Skip(1),(a,b)=>b>=a).All(x=>x),"monotonic progress");Check(progress.Distinct().Count()>20,"intermediate fill frames");
    var history=(HashSet<string>)typeof(FlowCanvas).GetField("flowHistory",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);
    c.AnimateFlowStep(parity.Edges.First(e=>e.From=="even").Id,200);Check(history.Contains(edge.Id),"completed history retained");c.AnimateFlowStep(edge.Id,200);Check(!history.Contains(edge.Id),"repeat edge starts empty");
    var far=Get(parity,"odd");far.X+=2000;c.AnimateFlowStep(parity.Edges.First(e=>e.From=="check"&&e.Branch=="false").Id,65);Check(c.FlowAnimationDuration>500&&c.FlowAnimationDuration<=3000,"long route receives bounded travel time");far.X-=2000;
    c.CurrentNodeId=null;c.CurrentEdgeId=null;Check(history.Count==0,"reset clears path history");
    UiMotion.SetEnabled(false);c.AnimateFlowStep(edge.Id,1200);Check(c.FlowProgress==1&&!c.HasFlowAnimation,"reduced motion immediate");
    form.Close();paints.Sort();File.WriteAllText(Path.Combine(folder,"paint-metrics.txt"),"Windows / Microsoft .NET Framework: offscreen paint cost, not monitor FPS.\nMedian ms: "+paints[paints.Count/2]+"\nP95 ms: "+paints[(int)(paints.Count*.95)]+"\n");
   }
   using(var main=new MainForm(null,true)){
    main.Show();Application.DoEvents();main.SetDocument(Examples.Parity(),null,false);
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    var before=main.Document.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));
    typeof(MainForm).GetMethod("ArrangeDiagram",flags).Invoke(main,null);
    var after=main.Document.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));
    typeof(MainForm).GetMethod("Undo",flags).Invoke(main,null);Check(main.Document.Nodes.All(n=>before[n.Id]==new PointF(n.X,n.Y)),"real Riordina undo");
    typeof(MainForm).GetMethod("Redo",flags).Invoke(main,null);Check(main.Document.Nodes.All(n=>after[n.Id]==new PointF(n.X,n.Y)),"real Riordina redo");
    main.Runner=new Interpreter(main.Document);
    ((FlowDebugger)typeof(MainForm).GetMethod("GetDebugger",flags).Invoke(main,null)).Begin(FlowDebugger.Command.Continue);
    UiMotion.SetEnabled(true);typeof(UiMotion).GetField("systemAnimations",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
    main.Canvas.AnimateFlowStep(main.Document.Edges[0].Id,1200);
    int steps=main.Runner.Steps;typeof(MainForm).GetMethod("RunTick",flags).Invoke(main,null);Check(main.Runner.Steps==steps,"automatic step waits for edge fill");
    UiMotion.SetEnabled(false);typeof(MainForm).GetMethod("RunTick",flags).Invoke(main,null);Check(main.Runner.Steps>steps,"motion off does not delay execution");
    Check(AssemblyName.GetAssemblyName(Environment.GetEnvironmentVariable("PRODUCTION_EXE")).Version.ToString()=="1.0.0.0","production assembly version");
    UiMotion.SetEnabled(true);
    main.SetDocument(DiagramIO.Clone(allKinds),null,false);foreach(var n in main.Document.Nodes){n.X+=70;n.Y+=50;}
    var newBefore=main.Document.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));
    typeof(MainForm).GetMethod("ArrangeDiagram",flags).Invoke(main,null);
    var newAfter=main.Document.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));
    Check(main.Document.Nodes.Any(n=>newBefore[n.Id]!=newAfter[n.Id]),"new kinds arranged by real command");
    Check(typeof(FlowCanvas).GetField("settleFrom",flags).GetValue(main.Canvas)!=null,"new block layout animates positions");
    Check((bool)typeof(FlowCanvas).GetField("arrangeCamera",flags).GetValue(main.Canvas),"new block layout animates camera");
    typeof(MainForm).GetMethod("Undo",flags).Invoke(main,null);Check(main.Document.Nodes.All(n=>newBefore[n.Id]==new PointF(n.X,n.Y)),"new kinds Riordina undo");
    typeof(MainForm).GetMethod("Redo",flags).Invoke(main,null);Check(main.Document.Nodes.All(n=>newAfter[n.Id]==new PointF(n.X,n.Y)),"new kinds Riordina redo");
    var functions=DiagramIO.CreateBlank();functions.Version=2;var function=new FlowFunction{Name="prova",Body=DiagramIO.Clone(allKinds)};functions.Functions=new List<FlowFunction>{function};FlowAnnotations.Promote(functions);
    main.SetDocument(functions,null,false);main.ActivateFunction(function.Id,false);foreach(var n in main.Document.Nodes){n.X+=20;n.Y+=30;}
    var functionBefore=DiagramIO.EncodeDocument(main.Project);var mainPositions=main.Project.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));
    typeof(MainForm).GetMethod("ArrangeDiagram",flags).Invoke(main,null);Check(main.ActiveFunctionId==function.Id,"Riordina remains in function");
    Check(main.Project.Nodes.All(n=>mainPositions[n.Id]==new PointF(n.X,n.Y)),"function layout leaves main positions intact");
    typeof(MainForm).GetMethod("Undo",flags).Invoke(main,null);Check(functionBefore.SequenceEqual(DiagramIO.EncodeDocument(main.Project)),"function Riordina undo");
    typeof(MainForm).GetMethod("Redo",flags).Invoke(main,null);Check(DiagramIO.EncodeDocument(main.Project).Length>0,"function Riordina redo remains saveable");
    main.SetDocument(Examples.Parity(),null,false);main.Close();
   }
   Console.WriteLine("PASS "+checks+" layout/motion checks. Native WinForms on Microsoft .NET Framework / Windows.");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}

