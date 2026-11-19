using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Frociart;
public static class CSharpSourceTests
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
    public static int Main(string[] args){try{string root=args[0];Directory.CreateDirectory(Path.Combine(root,"pc-code"));
        var extra=new Dictionary<string,string>{{"target-java-names","Arrays = [1, 2]\nDouble = 0.12345678901234567\nIntrinsics = 7\njava = 8\nValue = 9\nTypeSpec = 10\nclass = 11\nrun = 12\nSTAMPA Arrays\nSTAMPA Double\nSTAMPA Intrinsics + java + Value + TypeSpec + class + run"},{"target-subnormal","STAMPA 5e-324\nSTAMPA -5e-324\nSTAMPA 1e-320"},{"target-java-escapes","STAMPA \"\\\\u000a \\\\u0022 \\\" 🐢\""}};
        extra["target-csharp-names"]="namespace=1\nevent=2\ndelegate=3\nunsafe=4\nthis=5\nusing=6\ninternal=7\nget=8\ndynamic=9\nConsole=10\nSystem=11\nBitConverter=12\nValue=13\nIntrinsicOperations=14\nMath=15\nSTAMPA namespace+event+delegate+unsafe+this+using+internal+get+dynamic+Console+System+BitConverter+Value+IntrinsicOperations+Math";
        foreach(var pair in extra){var d=FlowPseudocode.Decode(pair.Value);d.Title="Java: \\u000a \\u0022 🐢\nFine";File.WriteAllBytes(Path.Combine(root,"fixtures",pair.Key+".frociart"),DiagramIO.EncodeDocument(d));}
        int programs=0,invalid=0;var outcomes=new List<string>();foreach(string file in Directory.GetFiles(Path.Combine(root,"fixtures"),"*.frociart").OrderBy(x=>x,StringComparer.Ordinal)){
            string name=Path.GetFileNameWithoutExtension(file);var d=DiagramIO.DecodeDocument(File.ReadAllBytes(file));byte[] before=DiagramIO.EncodeDocument(d);var p=FlowSource.Compile(d);if(!p.CanGenerate){bool rejected=false;try{FlowCSharp.Generate(p);}catch(ArgumentException){rejected=true;}Eq(true,rejected,name+" invalid rejected");outcomes.Add(name+"\tinvalid");invalid++;continue;}
            string className="Case"+programs;var code=FlowCSharp.Generate(p,File.ReadAllText(Path.Combine(root,"..","frociart-v1-capabilities","pc","Source","CSharpRuntime.txt")),className);Eq(true,before.SequenceEqual(DiagramIO.EncodeDocument(d)),name+" unchanged");Eq("csharp",code.Language,"language");foreach(var scope in p.Scopes)foreach(var note in scope.Notes)Eq(true,code.ForBlock(scope.Id,note.Id).Count>0,name+" note mapped");foreach(var s in p.Scopes)foreach(var b in s.Blocks)Eq(true,code.ForBlock(s.Id,b.Id).Count>0,name+" mapped");
            File.WriteAllText(Path.Combine(root,"pc-code",className+".cs"),code.Text,new UTF8Encoding(false));string expected=Path.Combine(root,"expected",name+".json");Save(expected,Run(name,d,expected));outcomes.Add(name+"\t"+className);programs++;
        }File.WriteAllLines(Path.Combine(root,"pc-outcomes.tsv"),outcomes,new UTF8Encoding(false));Eq(true,FlowCSharp.Generate(FlowSource.Compile(FlowPseudocode.Decode("STAMPA 1"))).Text.Contains("class Programma"),"embedded runtime");Console.WriteLine("CSHARP_SOURCE_PC PASS checks="+checks+" programs="+programs+" invalid="+invalid);return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
