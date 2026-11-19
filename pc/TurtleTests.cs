using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using System.Globalization;
namespace Frociart.Tests {
public class TurtleTests {
 static int checks;static string current,root;
 static void eq(object expected,object actual){checks++;if(!Object.Equals(expected,actual))throw new Exception(current+": expected ["+expected+"] actual ["+actual+"]");}
 static void near(double expected,double actual){checks++;if(Double.IsNaN(actual)||Math.Abs(expected-actual)>1e-8)throw new Exception(current+": expected "+expected+" actual "+actual);}
 static double num(string s){return Double.Parse(s,CultureInfo.InvariantCulture);}static string decode(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s));}
 static void rejects(Action a){bool rejected=false;try{a();}catch(FlowException){rejected=true;}eq(true,rejected);}
 static void Shared(){foreach(string row in File.ReadAllLines(Path.Combine(root,"cases.tsv"))){var f=row.Split('\t');current=f[0];var original=FlowPseudocode.Decode(decode(f[1]));Directory.CreateDirectory(Path.Combine(root,"pc-files"));File.WriteAllBytes(Path.Combine(root,"pc-files",current+".frociart"),DiagramIO.EncodeDocument(original));string other=Path.Combine(root,"java-files",current+".frociart");
  for(int pass=0;pass<(File.Exists(other)?4:3);pass++){
   var doc=pass==0?original:pass==1?FlowPseudocode.Decode(FlowPseudocode.Encode(original)):pass==2?DiagramIO.Clone(original):DiagramIO.Load(other);var r=new Interpreter(doc);var inputs=decode(f[2]).Length==0?new string[0]:decode(f[2]).Split('\u001e');int used=0;
   for(int i=0;i<20000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;i++){
    if(r.State==RuntimeState.WaitingForInput||r.State==RuntimeState.WaitingForFile){var before=r.Turtle.Capture();int steps=r.Steps;for(int tick=0;tick<4;tick++)eq(false,r.Step());eq(before.Revision,r.Turtle.Revision);eq(steps,r.Steps);eq(true,used<inputs.Length);string input=inputs[used++];
     if(r.State==RuntimeState.WaitingForInput){eq("I:",input.Substring(0,2));eq(true,r.ProvideInput(input.Substring(2)));}else{eq("F:",input.Substring(0,2));eq(true,r.ProvideFile(r.PendingFile,input.Substring(2)));}
    }else r.Step();
   }
   string error=decode(f[8]);eq(error.Length==0?RuntimeState.Completed:RuntimeState.Error,r.State);if(error.Length>0)eq(true,(r.Error??"").IndexOf(error,StringComparison.OrdinalIgnoreCase)>=0);
   eq(inputs.Length,used);near(num(f[3]),r.Turtle.X);near(num(f[4]),r.Turtle.Y);near(num(f[5]),r.Turtle.Heading);eq(f[6],r.Turtle.Color);eq(decode(f[9]),String.Join("|",r.Output));
   var lines=decode(f[7]).Length==0?new string[0]:decode(f[7]).Split('\n');eq(lines.Length,r.Turtle.Segments.Count);for(int i=0;i<lines.Length;i++){var l=lines[i].Split(',');var s=r.Turtle.Segments[i];near(num(l[0]),s.X1);near(num(l[1]),s.Y1);near(num(l[2]),s.X2);near(num(l[3]),s.Y2);eq(l[4],s.Color);}
   var saved=r.Turtle.Capture();long revision=r.Turtle.Revision;r.Reset();eq(RuntimeState.Ready,r.State);eq(false,r.Turtle.Used);near(0,r.Turtle.X);near(0,r.Turtle.Y);near(90,r.Turtle.Heading);eq("#000000",r.Turtle.Color);eq(0,r.Turtle.Segments.Count);eq(true,r.Turtle.Revision>revision);eq(lines.Length,saved.Segments.Count);
  }
 }}
 static void Invariants(){current="atomic-errors-and-bounds";var t=new FlowTurtle();var forward=FlowTurtle.Parse(NodeKind.TurtleForward,"1");var turn=FlowTurtle.Parse(NodeKind.TurtleTurn,"DESTRA 90");var pen=FlowTurtle.Parse(NodeKind.TurtlePen,"\"blu\"");
  t.Apply(forward,1d);var shot=t.Capture();foreach(object bad in new object[]{true,"1",Double.NaN,Double.PositiveInfinity}){rejects(()=>t.Apply(forward,bad));rejects(()=>t.Apply(turn,bad));}rejects(()=>t.Apply(pen,4d));rejects(()=>t.Apply(pen,"bad"));eq(shot.Revision,t.Revision);near(shot.Y,t.Y);near(shot.Heading,t.Heading);eq(shot.Color,t.Color);eq(1,t.Segments.Count);
  bool readOnly=false;try{t.Segments.Clear();}catch(NotSupportedException){readOnly=true;}eq(true,readOnly);readOnly=false;try{shot.Segments.Clear();}catch(NotSupportedException){readOnly=true;}eq(true,readOnly);
  t.Reset();for(int i=0;i<FlowTurtle.MaxSegments;i++)t.Apply(forward,i%2==0?1d:-1d);long revision=t.Revision;rejects(()=>t.Apply(forward,1d));eq(FlowTurtle.MaxSegments,t.Segments.Count);eq(revision,t.Revision);near(0,t.Y);t.Apply(FlowTurtle.Parse(NodeKind.TurtleForward,"SENZA_TRACCIA 2"),2d);near(2,t.Y);eq(FlowTurtle.MaxSegments,t.Segments.Count);t.Apply(FlowTurtle.Parse(NodeKind.TurtleClear,"Pulisci"),null);t.Apply(forward,1d);eq(1,t.Segments.Count);
  foreach(var kind in new[]{NodeKind.TurtleForward,NodeKind.TurtleTurn,NodeKind.TurtlePen})rejects(()=>FlowTurtle.Parse(kind,""));rejects(()=>FlowTurtle.Parse(NodeKind.TurtleTurn,"UP 90"));
  current="rename-preserves-syntax";var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"destra","angolo"},{"SENZA_TRACCIA","distanza"},{"blu","colore"}};
  eq("  DESTRA angolo",FlowDeclaration.RewriteNode(new FlowNode{Kind=NodeKind.TurtleTurn,Text="  DESTRA destra"},names));eq("SENZA_TRACCIA distanza",FlowDeclaration.RewriteNode(new FlowNode{Kind=NodeKind.TurtleForward,Text="SENZA_TRACCIA SENZA_TRACCIA"},names));eq("\"blu\"",FlowDeclaration.RewriteNode(new FlowNode{Kind=NodeKind.TurtlePen,Text="\"blu\""},names));eq("colore",FlowDeclaration.RewriteNode(new FlowNode{Kind=NodeKind.TurtlePen,Text="blu"},names));
 }
 static void Debugger(){current="breakpoint-before-draw";var doc=FlowPseudocode.Decode("AVANZA 4\nRUOTA DESTRA 90\nAVANZA 5");var node=doc.Nodes.First(n=>n.Kind==NodeKind.TurtleForward);node.Breakpoint=true;var r=new Interpreter(doc);var d=new FlowDebugger(r,doc);d.Begin(FlowDebugger.Command.Continue);for(int i=0;i<20&&d.Active;i++)d.Tick();eq(true,d.AtBreakpoint);eq(0,r.Turtle.Segments.Count);d.Begin(FlowDebugger.Command.Into);for(int i=0;i<20&&d.Active;i++)d.Tick();eq(1,r.Turtle.Segments.Count);near(4,r.Turtle.Y);d.Begin(FlowDebugger.Command.Continue);for(int i=0;i<20&&d.Active;i++)d.Tick();eq(RuntimeState.Completed,r.State);eq(2,r.Turtle.Segments.Count);near(5,r.Turtle.X);
 }
 public static int Main(){try{root=Path.GetFullPath(Environment.GetEnvironmentVariable("TURTLE_CASES_ROOT"));Shared();Invariants();Debugger();Console.WriteLine("TurtleTests PASS "+checks+" assertions");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}}
