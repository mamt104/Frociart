using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Frociart;
public static class PostTestCorpusTests
{
    [DataContract] public sealed class ValueRecord{[DataMember]public string Name,Text,Kind;}
    [DataContract] public sealed class FileRecord{[DataMember]public string Name,Text;}
    [DataContract] public sealed class SegmentRecord{[DataMember]public double X1,Y1,X2,Y2;[DataMember]public string Color;}
    [DataContract] public sealed class Expected{[DataMember]public string Name,State,Error;[DataMember]public int Steps;[DataMember]public List<string> Output,Events;[DataMember]public List<bool> OutputNewlines;[DataMember]public List<ValueRecord> Variables;[DataMember]public List<FileRecord> Files;[DataMember]public double X,Y,Heading;[DataMember]public string Color;[DataMember]public List<SegmentRecord> Segments;}
    static int checks;static void Eq(object a,object b,string label){checks++;if(!Object.Equals(a,b))throw new Exception(label+": "+a+" != "+b);}
    static T Load<T>(string path){using(var f=File.OpenRead(path))return(T)new DataContractJsonSerializer(typeof(T)).ReadObject(f);}
    static void Save<T>(string path,T value){using(var f=File.Create(path))new DataContractJsonSerializer(typeof(T)).WriteObject(f,value);}
    static Expected Run(string name,Diagram d,string expectedPath){var old=File.Exists(expectedPath)?Load<Expected>(expectedPath):null;var events=new Queue<string>(old==null?new List<string>():old.Events);var used=new List<string>();var saved=new List<FileRecord>();var r=new Interpreter(d);for(int t=0;t<50000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;t++){
        if(r.State==RuntimeState.WaitingForInput){Eq(true,events.Count>0,name+" input expected");string e=events.Dequeue();Eq(true,e.StartsWith("I:"),name+" input event");used.Add(e);r.ProvideInput(e.Substring(2));}
        else if(r.State==RuntimeState.WaitingForFile){var request=r.PendingFile;if(request.Kind==FlowFileRequestKind.Save){saved.Add(new FileRecord{Name=request.Name,Text=request.Text});Eq(true,r.ProvideFile(request),name+" save acknowledged");}else{Eq(true,events.Count>0,name+" file expected");string e=events.Dequeue();Eq(true,e.StartsWith("F:"),name+" file event");used.Add(e);Eq(true,r.ProvideFile(request,e.Substring(2)),name+" file accepted");}}else r.Step();}
        Eq(true,r.State==RuntimeState.Completed||r.State==RuntimeState.Error,name+" native terminal");Eq(0,events.Count,name+" native events consumed");return new Expected{Name=name,State=r.State.ToString(),Error=r.Error,Steps=r.Steps,Output=r.Output.ToList(),OutputNewlines=r.OutputNewlines.ToList(),Events=used,Files=saved,Variables=r.Variables.Select(v=>new ValueRecord{Name=v.Key,Text=ValueFormatter.Format(v.Value),Kind=v.Value is VectorValue?"vector":v.Value is string?"text":v.Value is bool?"boolean":"number"}).ToList(),X=r.Turtle.X,Y=r.Turtle.Y,Heading=r.Turtle.Heading,Color=r.Turtle.Color,Segments=r.Turtle.Segments.Select(s=>new SegmentRecord{X1=s.X1,Y1=s.Y1,X2=s.X2,Y2=s.Y2,Color=s.Color}).ToList()};
    }
    static string Hash(string value){using(var hash=System.Security.Cryptography.SHA256.Create())return String.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b=>b.ToString("x2")));}
    static string Mapping(FlowSourceText text){var b=new StringBuilder();foreach(var line in text.Lines){b.Append(line.Number).Append(',').Append(line.Start).Append(',').Append(line.Length);foreach(var o in line.Origins)b.Append('|').Append(o.FunctionId??"").Append(':').Append(o.NodeId);b.Append('\n');}return Hash(b.ToString());}
    public static int Main(string[] args){try{string root=args[0];Directory.CreateDirectory(Path.Combine(root,"pc-code"));
        int programs=0,invalid=0,oldStructured=0,newStructured=0;var mappings=new List<string>();var outcomes=new List<string>();foreach(string file in Directory.GetFiles(Path.Combine(root,"fixtures"),"*.frociart").OrderBy(x=>x,StringComparer.Ordinal)){
            string name=Path.GetFileNameWithoutExtension(file);var d=DiagramIO.DecodeDocument(File.ReadAllBytes(file));byte[] before=DiagramIO.EncodeDocument(d);var p=FlowSource.Compile(d);if(!p.CanGenerate){foreach(var target in FlowSourceTarget.All){bool rejected=false;try{target.Generate(p);}catch(ArgumentException){rejected=true;}Eq(true,rejected,name+" invalid rejected by "+target.Id);}outcomes.Add(name+"\tinvalid");invalid++;continue;}
            string className="Case"+programs;foreach(var scope in p.Scopes){bool structured=FlowSourcePlan.Create(scope).Structured,legacy=LegacyFlowSourcePlan.Create(scope).Structured;if(legacy&&!structured)throw new Exception("Structured-source regression: "+name+" / "+scope.Name);if(structured)newStructured++;if(legacy)oldStructured++;}
            foreach(var target in FlowSourceTarget.All){FlowSourceText code=target==FlowSourceTarget.Java?FlowJava.Generate(p,File.ReadAllText(Path.Combine(root,"../frociart-v1-capabilities/pc/Source/JavaRuntime.txt")),className):target==FlowSourceTarget.CSharp?FlowCSharp.Generate(p,File.ReadAllText(Path.Combine(root,"../frociart-v1-capabilities/pc/Source/CSharpRuntime.txt")),className):target.Generate(p);Eq(true,before.SequenceEqual(DiagramIO.EncodeDocument(d)),name+" unchanged");foreach(var scope in p.Scopes){foreach(var block in scope.Blocks)Eq(true,code.ForBlock(scope.Id,block.Id).Count>0,name+" mapped "+target.Id);foreach(var note in scope.Notes)Eq(true,code.ForBlock(scope.Id,note.Id).Count>0,name+" note mapped");}string filename=target==FlowSourceTarget.Python?name+".py":className+"."+target.Extension;File.WriteAllText(Path.Combine(root,target==FlowSourceTarget.Java?"java-code":"pc-code",filename),code.Text,new UTF8Encoding(false));mappings.Add(name+"\t"+target.Id+"\t"+Hash(code.Text)+"\t"+Mapping(code));}
            var planText=FlowCodeTemplate.Create("Piano","custom","txt","","{% macro walk items %}{% for s in items %}{{ s.kind }} {{ s.loop }} {{ s.postTest }} {{ s.loopOnTrue }} {{ s.block.id }} {{ s.entryBlock.id }}\n{% render walk s.body %}{% render walk s.otherwise %}{% endfor %}{% endmacro %}{% for scope in program.scopes %}{{ scope.structured }}\n{% render walk scope.statements %}{% endfor %}").Generate(p,null);mappings.Add(name+"\tplan\t"+Hash(planText.Text)+"\t"+Mapping(planText));
            string expected=Path.Combine(root,"expected",name+".json");Save(expected,Run(name,d,expected));outcomes.Add(name+"\t"+className);programs++;
        }File.WriteAllLines(Path.Combine(root,"pc-outcomes.tsv"),outcomes,new UTF8Encoding(false));File.WriteAllLines(Path.Combine(root,"java-outcomes.tsv"),outcomes,new UTF8Encoding(false));File.WriteAllLines(Path.Combine(root,"pc-mapping.tsv"),mappings,new UTF8Encoding(false));File.WriteAllText(Path.Combine(root,"structure-counts.txt"),"oldStructured="+oldStructured+" newStructured="+newStructured);Eq(true,FlowJavaScript.Generate(FlowSource.Compile(FlowPseudocode.Decode("STAMPA 1"))).Text.Contains("class Runtime"),"embedded runtime");Console.WriteLine("POST_TEST_CORPUS_PC PASS checks="+checks+" programs="+programs+" invalid="+invalid);return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

namespace Frociart
{
    // A conservative control-flow plan for readable source generation. Unsupported graph shapes keep
    // their explicit edges; a backend can emit labelled/dispatch code rather than inventing a loop.
    internal sealed class LegacyFlowSourcePlan
    {
        internal enum Kind { Instruction, If, While }
        internal sealed class Statement
        {
            internal readonly Kind Kind;
            internal readonly FlowSource.Block Block;
            internal readonly bool LoopOnTrue;
            internal readonly ReadOnlyCollection<Statement> Body, Otherwise;
            internal Statement(Kind kind,FlowSource.Block block,bool loopOnTrue,IEnumerable<Statement> body,IEnumerable<Statement> otherwise)
            {Kind=kind;Block=block;LoopOnTrue=loopOnTrue;Body=Freeze(body);Otherwise=Freeze(otherwise);}
        }
        internal readonly FlowSource.Scope Scope;
        internal readonly bool Structured;
        internal readonly ReadOnlyCollection<Statement> Statements;
        private LegacyFlowSourcePlan(FlowSource.Scope scope,bool structured,IEnumerable<Statement> statements)
        {Scope=scope;Structured=structured;Statements=Freeze(statements);}
        private static ReadOnlyCollection<Statement> Freeze(IEnumerable<Statement> items)
        {return new List<Statement>(items??Enumerable.Empty<Statement>()).AsReadOnly();}
        internal static LegacyFlowSourcePlan Create(FlowSource.Scope scope)
        {
            if(scope==null)throw new ArgumentNullException("scope");
            try {
                var planner=new Planner(scope);var statements=planner.Walk(scope.StartId,null,0,new HashSet<string>());
                if(planner.Seen.Count!=scope.Blocks.Count(n=>n.Reachable))throw new ExplicitGraph();
                return new LegacyFlowSourcePlan(scope,true,statements);
            } catch(ExplicitGraph) {return new LegacyFlowSourcePlan(scope,false,null);}
        }
        private sealed class ExplicitGraph:Exception {}
        private sealed class Planner
        {
            private readonly FlowSource.Scope scope;
            internal readonly HashSet<string> Seen=new HashSet<string>(StringComparer.Ordinal);
            internal Planner(FlowSource.Scope scope){this.scope=scope;}
            internal List<Statement> Walk(string id,string stop,int depth,HashSet<string> boundaries)
            {
                if(depth>48)throw new ExplicitGraph();var result=new List<Statement>();
                while(stop==null||id!=stop)
                {
                    if(id==null||boundaries.Contains(id)||!Seen.Add(id))throw new ExplicitGraph();var node=scope.Find(id);
                    if(node.Kind!=NodeKind.Decision){result.Add(new Statement(Kind.Instruction,node,true,null,null));
                        if(node.Kind==NodeKind.End)return result;id=node.Next("next");continue;}
                    string yes=node.Next("true"),no=node.Next("false");
                    var blocked=new HashSet<string>(boundaries);if(stop!=null)blocked.Add(stop);blocked.Add(id);
                    bool yesLoop=Reach(yes,id,no,blocked),noLoop=Reach(no,id,yes,blocked);
                    if(yesLoop!=noLoop){string body=yesLoop?yes:no,exit=yesLoop?no:yes;
                        result.Add(new Statement(Kind.While,node,yesLoop,Walk(body,id,depth+1,blocked),null));id=exit;continue;}
                    string join=Join(yes,no,blocked);
                    var thenBody=Walk(yes,join,depth+1,blocked);var elseBody=Walk(no,join,depth+1,blocked);
                    result.Add(new Statement(Kind.If,node,true,thenBody,elseBody));
                    if(join==null)return result;id=join;
                }
                return result;
            }
            private bool Reach(string from,string target,string avoid,HashSet<string> blocked)
            {
                var seen=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(from);
                while(queue.Count>0){string id=queue.Dequeue();if(id==target)return true;if(id==avoid||blocked.Contains(id)||!seen.Add(id))continue;
                    foreach(var edge in scope.Find(id).Links)queue.Enqueue(edge.To);}
                return false;
            }
            private Dictionary<string,int> Distances(string from,HashSet<string> blocked)
            {
                var result=new Dictionary<string,int>();var queue=new Queue<string>();result.Add(from,0);queue.Enqueue(from);
                while(queue.Count>0){string id=queue.Dequeue();if(blocked.Contains(id))continue;
                    foreach(var edge in scope.Find(id).Links)if(!result.ContainsKey(edge.To)){result.Add(edge.To,result[id]+1);queue.Enqueue(edge.To);}}
                return result;
            }
            private string Join(string yes,string no,HashSet<string> blocked)
            {
                var a=Distances(yes,blocked);var b=Distances(no,blocked);string best=null;int score=Int32.MaxValue;
                foreach(var node in scope.Blocks){int x,y;if(!a.TryGetValue(node.Id,out x)||!b.TryGetValue(node.Id,out y)||Seen.Contains(node.Id))continue;
                    int cost=Math.Max(x,y)*1000+x+y;if(cost<score){score=cost;best=node.Id;}}
                return best;
            }
        }
    }
}
