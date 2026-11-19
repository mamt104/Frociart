using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Frociart;

public static class PythonSourceTests
{
    [DataContract] public sealed class ValueRecord { [DataMember] public string Name,Text,Kind; }
    [DataContract] public sealed class FileRecord { [DataMember] public string Name,Text; }
    [DataContract] public sealed class SegmentRecord { [DataMember] public double X1,Y1,X2,Y2; [DataMember] public string Color; }
    [DataContract] public sealed class Expected {
        [DataMember] public string Name,State,Error;
        [DataMember] public int Steps;
        [DataMember] public List<string> Output,Events;
        [DataMember] public List<ValueRecord> Variables;
        [DataMember] public List<FileRecord> Files;
        [DataMember] public double X,Y,Heading;
        [DataMember] public string Color;
        [DataMember] public List<SegmentRecord> Segments;
    }
    static int checks,programs;static string root;
    static void Eq(object a,object b,string label){checks++;if(!Object.Equals(a,b))throw new Exception(label+": expected "+a+", actual "+b);}
    static void Save<T>(string path,T value){using(var stream=File.Create(path))new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);}
    static T Load<T>(string path){using(var stream=File.OpenRead(path))return(T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
    static void Normalize(Diagram d,string name){var graphs=new List<Diagram>{d};if(d.Functions!=null){int f=0;foreach(var function in d.Functions){function.Id=name+"_function_"+(f++);graphs.Add(function.Body);}}
        int index=0,edgeIndex=0;foreach(var graph in graphs){var ids=new Dictionary<string,string>();foreach(var n in graph.Nodes){ids[n.Id]=name+"_n"+(index++);n.Id=ids[n.Id];}foreach(var e in graph.Edges){e.Id=name+"_e"+(edgeIndex++);e.From=ids[e.From];e.To=ids[e.To];}}}
    static string DefaultInput(Interpreter r){string name=r.InputVariable.Split('[')[0];string type;
        if(r.VariableTypes.TryGetValue(name,out type)&&type.StartsWith("Logico",StringComparison.Ordinal))return "vero";return "4";}
    static Expected Run(string name,Diagram d){
        string eventPath=Path.Combine(root,"events",name+".json");var pending=File.Exists(eventPath)?new Queue<string>(Load<List<string>>(eventPath)):new Queue<string>();
        var r=new Interpreter(d);var events=new List<string>();var saved=new List<FileRecord>();int waits=0;
        for(int t=0;t<50000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;t++){
            if(r.State==RuntimeState.WaitingForInput){
                if(++waits>100)throw new Exception(name+" excessive input retries");string value=pending.Count>0?pending.Dequeue():"I:"+DefaultInput(r);Eq(true,value.StartsWith("I:"),name+" expected input event");events.Add(value);r.ProvideInput(value.Substring(2));
            }else if(r.State==RuntimeState.WaitingForFile){var request=r.PendingFile;
                if(request.Kind==FlowFileRequestKind.Save){saved.Add(new FileRecord{Name=request.Name,Text=request.Text});Eq(true,r.ProvideFile(request),name+" save acknowledged");}
                else {string value=pending.Count>0?pending.Dequeue():"F:4\n";Eq(true,value.StartsWith("F:"),name+" expected file event");events.Add(value);Eq(true,r.ProvideFile(request,value.Substring(2)),name+" file accepted");}
            }else r.Step();
        }
        Eq(true,r.State==RuntimeState.Completed||r.State==RuntimeState.Error,name+" terminal native result");
        return new Expected{Name=name,State=r.State.ToString(),Error=r.Error,Steps=r.Steps,Output=r.Output.ToList(),Events=events,Files=saved,
            Variables=r.Variables.Select(v=>new ValueRecord{Name=v.Key,Text=ValueFormatter.Format(v.Value),Kind=v.Value is VectorValue?"vector":v.Value is string?"text":v.Value is bool?"boolean":"number"}).ToList(),
            X=r.Turtle.X,Y=r.Turtle.Y,Heading=r.Turtle.Heading,Color=r.Turtle.Color,
            Segments=r.Turtle.Segments.Select(s=>new SegmentRecord{X1=s.X1,Y1=s.Y1,X2=s.X2,Y2=s.Y2,Color=s.Color}).ToList()};
    }
    public static int Main(){try{
        root=Environment.GetEnvironmentVariable("PYTHON_QA");Directory.CreateDirectory(Path.Combine(root,"pc-code"));Directory.CreateDirectory(Path.Combine(root,"expected"));
        foreach(var file in Directory.GetFiles(Path.Combine(root,"extra-pseudo"),"*.txt")){string name="extra-"+Path.GetFileNameWithoutExtension(file);var d=FlowPseudocode.Decode(File.ReadAllText(file));Normalize(d,name);File.WriteAllBytes(Path.Combine(root,"fixtures",name+".frociart"),DiagramIO.EncodeDocument(d));}
        var outcomes=new List<string>();
        foreach(var file in Directory.GetFiles(Path.Combine(root,"fixtures"),"*.frociart").OrderBy(x=>x,StringComparer.Ordinal)){
            string name=Path.GetFileNameWithoutExtension(file);var document=DiagramIO.DecodeDocument(File.ReadAllBytes(file));var before=DiagramIO.EncodeDocument(document);var p=FlowSource.Compile(document);
            if(!p.CanGenerate){bool rejected=false;try{FlowPython.Generate(p);}catch(ArgumentException){rejected=true;}Eq(true,rejected,name+" invalid input rejected");outcomes.Add(name+"\tinvalid");continue;}
            var code=FlowPython.Generate(p);Eq(true,before.SequenceEqual(DiagramIO.EncodeDocument(document)),name+" export preserves project");Eq("python",code.Language,"language");
            Eq(code.Text,FlowPython.Generate(p).Text,name+" deterministic source");
            foreach(var scope in p.Scopes)foreach(var node in scope.Blocks)Eq(true,code.ForBlock(scope.Id,node.Id).Count>0,name+" every node maps to source");
            foreach(var line in code.Lines){Eq(true,line.Start>=0&&line.Start+line.Length<code.Text.Length,name+" source line range");Eq('\n',code.Text[line.Start+line.Length],name+" newline offset");}
            File.WriteAllText(Path.Combine(root,"pc-code",name+".py"),code.Text,new UTF8Encoding(false));
            var expected=Run(name,document);Save(Path.Combine(root,"expected",name+".json"),expected);outcomes.Add(name+"\t"+expected.State);programs++;
        }
        File.WriteAllLines(Path.Combine(root,"pc-outcomes.tsv"),outcomes,new UTF8Encoding(false));
        Eq("'\\U0001f422\\u0000\\'\\\\'",FlowPython.Literal("🐢\0'\\"),"portable literal escaping");
        Console.WriteLine("PYTHON_SOURCE_NATIVE PASS checks="+checks+" programs="+programs);return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
