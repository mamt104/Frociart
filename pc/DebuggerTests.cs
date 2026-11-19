using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using Frociart;
namespace Frociart.Tests { public static class DebuggerTests {
 static Diagram d;static Interpreter r;static FlowDebugger debug;static string current,exchange;static int checks;
 static string dec(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s));}
 static void eq(string a,string b){checks++;if(a!=b)throw new Exception(current+": expected ["+a+"] actual ["+b+"]");}
 static Diagram graph(string name){return name==""?d:d.Functions.First(f=>f.Name==name).Body;}
 static void ensure(){if(r!=null)return;r=new Interpreter(d);eq("Ready",r.State.ToString());debug=new FlowDebugger(r,d);}
 static void drive(){ensure();int guard=0;while(debug.Active&&r.State!=RuntimeState.WaitingForInput){if(++guard>25000)throw new Exception("Debugger did not yield/terminate");debug.Tick();}}
 static string field(string key){var g=r.CurrentFunctionId==null?d:d.Functions.First(f=>f.Id==r.CurrentFunctionId).Body;var n=g.Nodes.First(x=>x.Id==r.CurrentNodeId);switch(key){case "state":return r.State.ToString();case "mode":return debug.Mode.ToString();case "point":return debug.AtBreakpoint.ToString();case "text":return n.Text;case "kind":return n.Kind.ToString();case "function":return r.CurrentFunctionName;case "depth":return r.CallDepth.ToString();case "steps":return r.Steps.ToString();case "output":return String.Join("|",r.Output);default:return r.Variables.ContainsKey(key)?ValueFormatter.Format(r.Variables[key]):"<missing>";}}
 public static int Main(){try{
 string path=Environment.GetEnvironmentVariable("DEBUGGER_CASES");exchange=Path.GetDirectoryName(path);Directory.CreateDirectory(Path.Combine(exchange,"pc-files"));
 foreach(string line in File.ReadAllLines(path)){var parts=line.Split('\t');current=parts[0];string op=parts[1],a=dec(parts[2]),b=dec(parts[3]);switch(op){
 case "source":d=FlowPseudocode.Decode(a);r=null;debug=null;break;
 case "mark":var target=a.Split('|');foreach(var n in graph(target[0]).Nodes.Where(n=>n.Text==target[1])){n.Breakpoint=true;n.BreakpointCondition=b;}eq("True",graph(target[0]).Nodes.Any(n=>n.Text==target[1]).ToString());break;
 case "markKind":foreach(var n in d.Nodes.Where(n=>n.Kind.ToString()==a))n.Breakpoint=true;break;
 case "selfloop":var node=d.Nodes.First(n=>n.Text==a);d.Edges.First(e=>e.From==node.Id&&e.Branch==b).To=node.Id;break;
 case "begin":ensure();eq("True",debug.Begin((FlowDebugger.Command)Enum.Parse(typeof(FlowDebugger.Command),a)).ToString());break;
 case "drive":drive();break;
 case "tick":ensure();debug.Tick();break;
 case "pause":debug.Pause();break;
 case "reset":r.Reset();break;
 case "ignore":ensure();debug.IgnoreBreakpoints=a=="true";break;
 case "update":debug.UpdateBreakpoints(d);break;
 case "clear":foreach(var g in new[]{d}.Concat(d.Functions==null?Enumerable.Empty<Diagram>():d.Functions.Select(f=>f.Body)))foreach(var n in g.Nodes){n.Breakpoint=false;n.BreakpointCondition=null;}debug.UpdateBreakpoints(d);break;
 case "expect":ensure();eq(b,field(a));break;
 case "missing":eq("False",r.Variables.ContainsKey(a).ToString());break;
 case "message":eq("True",(debug.Message!=null&&debug.Message.Contains(a)).ToString());break;
 case "input":bool accepted=r.ProvideInput(a);eq(b,accepted.ToString().ToLowerInvariant());if(accepted)debug.InputProvided();break;
 case "save":
   string original=Convert.ToBase64String(DiagramIO.EncodeDocument(d));d=DiagramIO.Clone(d);eq(original,Convert.ToBase64String(DiagramIO.EncodeDocument(d)));
   string file=Path.Combine(exchange,"pc-files",current+".frociart");File.WriteAllBytes(file,DiagramIO.EncodeDocument(d));d=DiagramIO.Load(file);eq(original,Convert.ToBase64String(DiagramIO.EncodeDocument(d)));
   string other=Path.Combine(exchange,"java-files",current+".frociart");if(File.Exists(other)){var old=d;d=DiagramIO.Load(other);eq(old.Nodes.Count.ToString(),d.Nodes.Count.ToString());}break;
 default:throw new Exception("Unknown action "+op);
 }}
 current="invalid-condition-validation";foreach(string text in new[]{"random(2)>0","falso e random(2)>0","missingFunction()","x +"}){bool rejected=false;try{new FlowDebugger.DebugCondition(text);}catch(FlowException){rejected=true;}eq("True",rejected.ToString());}
 current="out-at-main";d=FlowPseudocode.Decode("STAMPA 1");r=new Interpreter(d);debug=new FlowDebugger(r,d);eq("False",debug.Begin(FlowDebugger.Command.Out).ToString());eq("0",r.Steps.ToString());
 current="rename-condition";d=FlowPseudocode.Decode("FUNZIONE prova(n: INTERO) -> INTERO risultato\nrisultato=n+1\nFINE FUNZIONE\nSTAMPA prova(2)");var functionForRename=d.Functions[0];functionForRename.Body.Nodes[0].Breakpoint=true;functionForRename.Body.Nodes[0].BreakpointCondition="n = 2 e \"n\" = \"n\"";var next=functionForRename.Copy();next.Parameters[0].Name="numero";d=FunctionEditing.Save(d,next,new Dictionary<string,string>{{"n","numero"}});eq("numero = 2 e \"n\" = \"n\"",d.Functions[0].Body.Nodes[0].BreakpointCondition);
 current="condition-bounds";d.Nodes[0].BreakpointCondition=new string('x',8193);bool rejectedSize=false;try{DiagramIO.EncodeDocument(d);}catch(InvalidDataException){rejectedSize=true;}eq("True",rejectedSize.ToString());
 Console.WriteLine("DebuggerTests PASS "+checks+" assertions, 30 scenarios; project persistence and counterpart exchange when present");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}}
