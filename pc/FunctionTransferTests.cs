using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Runtime.Serialization;using System.Runtime.Serialization.Json;using System.Text;using Frociart;
internal static class FunctionTransferTests {
 [DataContract]internal class Case{[DataMember]public string Name,Source,Target,Selected,Assigned,Call,Output,Inputs;[DataMember]public int Count;}
 static int checks;static string current;
 static void Check(bool ok,string why){if(!ok)throw new Exception(current+": "+why);checks++;}
 static string Bytes(Diagram d){return Convert.ToBase64String(DiagramIO.EncodeDocument(d));}
 static string Run(Diagram d,string inputs=""){var r=new Interpreter(d);var values=new Queue<string>(inputs.Length==0?new string[0]:inputs.Split('|'));for(int i=0;i<2000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;i++){if(r.State==RuntimeState.WaitingForInput){Check(values.Count>0,"input supplied");Check(r.ProvideInput(values.Dequeue()),"input accepted");}else r.Step();}Check(r.State==RuntimeState.Completed,"executable result: "+r.Error);return String.Join("|",r.Output);}
 static void Reject(Action action,string why){bool bad=false;try{action();}catch(Exception e){if(!(e is InvalidDataException||e is FlowException))throw;bad=true;}Check(bad,why);}
 static int Main(string[] args){try{
  string root=args[0];var fixture=Path.Combine(root,"cross");Directory.CreateDirectory(fixture);
  Case[] cases;using(var stream=File.OpenRead(Path.Combine(root,"cases.json")))cases=(Case[])new DataContractJsonSerializer(typeof(Case[])).ReadObject(stream);
  foreach(var c in cases){current=c.Name;var source=FlowPseudocode.Decode(c.Source);var target=FlowPseudocode.Decode(c.Target);var selected=source.Functions.Single(f=>f.Name==c.Selected);
   // Metadata, including text containing a call-like string, must survive untouched.
   foreach(var node in selected.Body.Nodes){node.Prompt="Domanda helper(n) · €";node.TrueLabel="Percorso A()";node.FalseLabel="Altro";node.TrueColor="#35A96C";node.FalseColor="#DD5C79";}
   string beforeSource=Bytes(source),beforeTarget=Bytes(target);var plan=FunctionTransfer.Import(target,source,selected.Id);
   Check(beforeSource==Bytes(source)&&beforeTarget==Bytes(target),"no source/target mutation");Check(plan.Added.Count==c.Count,"dependency closure count");
   var imported=FunctionEditing.Find(plan.Project,plan.SelectedId);Check(imported.Name==c.Assigned,"unique selected name");Check(plan.Project.Functions.Count==(target.Functions==null?0:target.Functions.Count)+c.Count,"functions appended");
   var original=DiagramIO.Clone(plan.Project);original.Functions=target.Functions;original.Version=target.Version;Check(Bytes(original)==beforeTarget,"existing project byte-equivalent after excluding imported definitions");
   var graphs=FunctionEditing.Graphs(plan.Project).ToList();Check(graphs.SelectMany(g=>g.Nodes).Select(n=>n.Id).Distinct().Count()==graphs.Sum(g=>g.Nodes.Count),"global unique node IDs");Check(graphs.SelectMany(g=>g.Edges).Select(n=>n.Id).Distinct().Count()==graphs.Sum(g=>g.Edges.Count),"global unique edge IDs");
   Check(imported.Body.Nodes.All(n=>!selected.Body.Nodes.Any(o=>o.Id==n.Id)),"fresh imported IDs");
   for(int i=0;i<selected.Body.Nodes.Count;i++){var a=selected.Body.Nodes[i];var b=imported.Body.Nodes[i];Check(a.X==b.X&&a.Y==b.Y&&a.Prompt==b.Prompt&&a.TrueLabel==b.TrueLabel&&a.TrueColor==b.TrueColor&&a.FalseColor==b.FalseColor&&a.Breakpoint==b.Breakpoint,"layout and metadata preserved");}
   Check(Run(target)==Run(plan.Project),"original main behavior preserved");
   var exercise=FlowPseudocode.Decode("STAMPA 0");exercise.Nodes.Single(n=>n.Kind==NodeKind.Output).Text=c.Call;exercise.Functions=plan.Project.Functions.Select(f=>f.Copy()).ToList();exercise.Version=2;Check(Run(exercise,c.Inputs)==c.Output,"imported function result");
   var library=FunctionTransfer.Export(source,selected.Id);Check(library.Functions.Count==c.Count&&library.Nodes.Count==2,"export contains selected dependencies and blank main");
   string path=Path.Combine(fixture,c.Name+".frociart");DiagramIO.Save(path,exercise);Check(Run(DiagramIO.Load(path),c.Inputs)==c.Output,"actual save/reopen result");
   DiagramIO.Save(Path.Combine(fixture,c.Name+"-source.frociart"),source);DiagramIO.Save(Path.Combine(fixture,c.Name+"-target.frociart"),target);
  }
  current="failures";var good=FlowPseudocode.Decode("FUNZIONE buona() -> INTERO valore\nvalore=1\nFINE FUNZIONE\nSTAMPA 0");string id=good.Functions[0].Id;var empty=DiagramIO.CreateBlank();string clean=Bytes(good),cleanTarget=Bytes(empty);
  Reject(()=>FunctionTransfer.Import(empty,good,"missing"),"missing selection rejected");Check(Bytes(good)==clean&&Bytes(empty)==cleanTarget,"failed selection preserves originals");
  var unrelated=good.Functions[0].Copy();unrelated.Id=Guid.NewGuid().ToString("N");unrelated.Name="rotta";foreach(var n in unrelated.Body.Nodes)n.Id=Guid.NewGuid().ToString("N");unrelated.Body=FlowPseudocode.Decode("STAMPA 0");unrelated.Body.Nodes.First(n=>n.Kind==NodeKind.Output).Text="assente()";good.Functions.Add(unrelated);
  Check(FunctionTransfer.Import(empty,good,id).Added.Count==1,"unrelated incomplete function ignored");Reject(()=>FunctionTransfer.Import(empty,good,unrelated.Id),"unknown call never silently binds destination");
  var bad=good.Functions[0].Body.Nodes.First(n=>n.Kind==NodeKind.Process);bad.Text="valore=";string badBefore=Bytes(good);Reject(()=>FunctionTransfer.Import(empty,good,id),"malformed selected function rejected");Check(Bytes(good)==badBefore&&Bytes(empty)==cleanTarget,"failed parse is atomic");
  good=FlowPseudocode.Decode("FUNZIONE buona() -> INTERO valore\nvalore=1\nFINE FUNZIONE\nSTAMPA 0");id=good.Functions[0].Id;
  var full=FlowPseudocode.Decode(String.Join("\n",Enumerable.Range(0,32).Select(i=>"FUNZIONE fn"+i+"() -> INTERO valore\nvalore=0\nFINE FUNZIONE"))+"\nSTAMPA 0");string fullBefore=Bytes(full);Reject(()=>FunctionTransfer.Import(full,good,id),"function capacity checked");Check(Bytes(full)==fullBefore,"capacity failure preserves original");
  var crowded=DiagramIO.CreateBlank();while(crowded.Nodes.Count<500)crowded.Nodes.Add(new FlowNode{Kind=NodeKind.Process,Text="x=0"});Reject(()=>FunctionTransfer.Import(crowded,good,id),"global node capacity checked");
  var edgeFull=DiagramIO.CreateBlank();while(edgeFull.Edges.Count<1500)edgeFull.Edges.Add(new FlowEdge{From=edgeFull.Nodes[0].Id,To=edgeFull.Nodes[1].Id});Reject(()=>FunctionTransfer.Import(edgeFull,good,id),"global edge capacity checked");
  var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"a","A_3"},{"a_2","A_2"},{"e3","changed"}};
  Check(FunctionTransfer.RewriteCalls("a (A_2(2)) + 1e3 + \"a(2)\" + 'a(3)'",map)=="A_3 (A_2(2)) + 1e3 + \"a(2)\" + 'a(3)'","simultaneous lexer rename preserves quotes and numeric exponents");
  Console.WriteLine("FUNCTION_TRANSFER_PC PASS checks="+checks+" cases="+cases.Length);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
