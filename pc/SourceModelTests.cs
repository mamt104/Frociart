using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Frociart;

public static class SourceModelTests
{
    static int checks; static string root;
    static readonly HashSet<int> kinds=new HashSet<int>();
    static readonly HashSet<FlowSource.ExpressionKind> expressionKinds=new HashSet<FlowSource.ExpressionKind>();
    static int structuredPlans,explicitPlans,traceChecks;
    static void Eq(object expected,object actual,string label){checks++;if(!Object.Equals(expected,actual))throw new Exception(label+": expected "+expected+", actual "+actual);}
    static void Reject(Action action,string label){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(FlowException){rejected=true;}catch(NotSupportedException){rejected=true;}Eq(true,rejected,label);}
    static void Clear<T>(ICollection<T> values){values.Clear();}
    static string S(string value){return value==null?"~":Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
    static void Row(StringBuilder b,params object[] values){b.Append(String.Join("\t",values.Select(v=>v is bool?((bool)v?"1":"0"):Convert.ToString(v,CultureInfo.InvariantCulture)))).Append('\n');}
    static void Expr(StringBuilder b,FlowSource.Expression e){
        if(e==null){Row(b,"null");return;}expressionKinds.Add(e.Kind);
        Row(b,e.Kind,S(e.Text),BitConverter.DoubleToInt64Bits(e.Number).ToString("x16"),e.Boolean,S(e.FunctionId),e.Children.Count);
        foreach(var c in e.Children)Expr(b,c);
    }
    static void Target(StringBuilder b,FlowSource.Target t){Row(b,"target",S(t==null?null:t.Name));Expr(b,t==null?null:t.Index);}
    static string Dump(FlowSource.Program p){
        var b=new StringBuilder();Row(b,"program",S(p.Title),p.CanGenerate,p.Scopes.Count);
        // Diagnostic text differs between the native products. The portable compilation contract starts
        // after their existing validation succeeds; invalid results never contain partial scopes.
        foreach(var s in p.Scopes){Row(b,"scope",S(s.Id),S(s.Name),S(s.StartId),S(s.ReturnVariable),(int)s.ReturnType,s.ReturnIsArray);
            foreach(var a in s.Parameters)Row(b,"parameter",S(a.Name),(int)a.Type,a.IsArray);
            foreach(var n in s.Blocks){kinds.Add((int)n.Kind);Row(b,"block",S(n.Id),(int)n.Kind,S(n.Text),S(n.Prompt),n.Reachable);
                var o=n.Operation;Row(b,"flags",o.FileReading,o.FileNewline,o.TurtleDraw,o.TurtleLeft);Expr(b,o.Value);Target(b,o.Input);
                Row(b,"writes",o.Writes.Count);foreach(var w in o.Writes){Target(b,w.Target);Expr(b,w.Value);}
                Row(b,"declaration",o.Declaration!=null);if(o.Declaration!=null){Row(b,(int)o.Declaration.Type,o.Declaration.IsArray,o.Declaration.Names.Count);foreach(var name in o.Declaration.Names)Row(b,S(name));Expr(b,o.Declaration.Size);}
                foreach(var e in n.Links)Row(b,"edge",S(e.Id),S(e.To),S(e.Branch));
            }
            var plan=FlowSourcePlan.Create(s);Row(b,"structured",plan.Structured);DumpPlan(b,plan.Statements);
        }return b.ToString();
    }
    static void DumpPlan(StringBuilder b,IEnumerable<FlowSourcePlan.Statement> statements){
        foreach(var s in statements){Row(b,"statement",s.Kind,S(s.Block.Id),s.LoopOnTrue,s.Body.Count,s.Otherwise.Count);DumpPlan(b,s.Body);DumpPlan(b,s.Otherwise);}}
    static bool Choice(string id,int step,int seed){unchecked{uint hash=(uint)(seed+1);foreach(char c in id)hash=hash*31+c;hash+=((uint)step)*0x27d4eb2d;hash=(hash^(hash>>16))*0x45d9f3b;hash=(hash^(hash>>16))*0x45d9f3b;return ((hash^(hash>>16))&1)!=0;}}
    sealed class StopTrace:Exception {}
    static void PlanChecks(FlowSource.Scope scope){
        var plan=FlowSourcePlan.Create(scope);if(!plan.Structured){explicitPlans++;Eq(0,plan.Statements.Count,"fallback has no partial tree");return;}structuredPlans++;
        for(int seed=0;seed<32;seed++){
            var direct=new List<string>();string id=scope.StartId;
            for(int i=0;i<512;i++){var n=scope.Find(id);direct.Add(id);if(n.Kind==NodeKind.End)break;id=n.Next(n.Kind==NodeKind.Decision?(Choice(id,direct.Count,seed)?"true":"false"):"next");}
            var actual=new List<string>();Action<FlowSource.Block> visit=n=>{if(actual.Count==512)throw new StopTrace();actual.Add(n.Id);if(n.Kind==NodeKind.End)throw new StopTrace();};
            Action<IEnumerable<FlowSourcePlan.Statement>> walk=null;
            walk=statements=>{foreach(var s in statements){if(s.Kind==FlowSourcePlan.Kind.While){while(true){visit(s.Block);if(Choice(s.Block.Id,actual.Count,seed)!=s.LoopOnTrue)break;walk(s.Body);}}
                else {visit(s.Block);if(s.Kind==FlowSourcePlan.Kind.If)walk(Choice(s.Block.Id,actual.Count,seed)?s.Body:s.Otherwise);}}};
            try{walk(plan.Statements);}catch(StopTrace){}
            Eq(String.Join("|",direct),String.Join("|",actual),"structured trace seed "+seed);traceChecks++;
        }
    }
    static Diagram Load(string name){return DiagramIO.DecodeDocument(File.ReadAllBytes(Path.Combine(root,"fixtures",name+".frociart")));}
    static FlowSource.Program Compile(string name){var p=FlowSource.Compile(Load(name));Eq(true,p.CanGenerate,name+" compiles "+String.Join(";",p.Diagnostics.Select(d=>d.Message)));return p;}
    static FlowSource.Expression Output(string name){return Compile(name).Scopes[0].Blocks.Single(n=>n.Kind==NodeKind.Output).Operation.Value;}
    static void StructuralOracles(){
        var a=Output("expr-0");Eq("+",a.Text,"sum precedence");Eq("*",a.Children[1].Text,"multiply nested");Eq(3.0,a.Children[1].Children[1].Number,"numeric leaf");
        a=Output("expr-1");Eq("*",a.Text,"parentheses root");Eq("+",a.Children[0].Text,"parentheses retained");
        a=Output("expr-2");Eq("^",a.Text,"power root");Eq("^",a.Children[1].Text,"right associative power");
        a=Output("expr-7");Eq("and",a.Text,"short circuit operator retained");Eq(false,a.Children[0].Boolean,"false literal retained");Eq("/",a.Children[1].Children[0].Text,"unexecuted zero division retained");
        Eq("random",Output("expr-9").Text,"random not evaluated");Eq("eof",Output("expr-10").Text,"EOF no file access");
        Eq("int",Output("expr-11").Text,"invalid dynamic step not evaluated while generating");
        Eq(FlowSource.ExpressionKind.Variable,Output("expr-14").Children[0].Kind,"pi remains shadowable variable");
        var p=Compile("all-operations");Eq(2,p.Scopes.Count,"main and function");var s=p.Scopes[0];
        Eq("greeting",s.Blocks.Single(n=>n.Kind==NodeKind.Call).Operation.Value.FunctionId,"resolved function ID");
        Eq(FlowDataType.Text,p.Scopes[1].Parameters[0].Type,"parameter type");
        var d=s.Blocks.Single(n=>n.Kind==NodeKind.Decision);Eq(2,d.Links.Count,"merged branches both retained");Eq(d.Next("true"),d.Next("false"),"branch convergence retained");
        Eq(false,s.Blocks.Single(n=>n.Kind==NodeKind.TurtleForward).Operation.TurtleDraw,"pen up");
        Eq(true,s.Blocks.Single(n=>n.Kind==NodeKind.TurtleTurn).Operation.TurtleLeft,"left turn");
        Eq(false,s.Blocks.First(n=>n.Kind==NodeKind.WriteFile).Operation.FileNewline,"write without newline");
        Eq(true,s.Blocks.Last(n=>n.Kind==NodeKind.WriteFile).Operation.FileNewline,"write newline");
        var declaration=s.Blocks.Where(n=>n.Kind==NodeKind.Declare).Last().Operation.Declaration;
        Eq(true,declaration.IsArray,"array declaration");Eq("+",declaration.Size.Text,"size expression");
        Eq("i",s.Blocks.Single(n=>n.Kind==NodeKind.Input).Operation.Input.Index.Text,"indexed input");
        var writes=Compile("transaction").Scopes[0].Blocks.Single(n=>n.Kind==NodeKind.Process).Operation.Writes;
        Eq(5,writes.Count,"transaction retains all writes");Eq("a",writes[2].Target.Name,"indexed target");Eq("i",writes[2].Target.Index.Text,"target evaluated after RHS");Eq("+",writes[2].Value.Text,"RHS is separate from index");
        Eq(false,Compile("unreachable").Scopes[0].Find("unused").Reachable,"unreachable retained");
        Eq("n1",Compile("self-loop").Scopes[0].Find("n1").Next("true"),"self loop retained");
        Eq(false,FlowSourcePlan.Create(Compile("false-loop").Scopes[0]).Statements.Single(x=>x.Kind==FlowSourcePlan.Kind.While).LoopOnTrue,"false branch loop polarity");
        Eq(true,FlowSourcePlan.Create(Compile("two-ends").Scopes[0]).Structured,"separate return paths");
        Eq(false,FlowSourcePlan.Create(Compile("deep-control").Scopes[0]).Structured,"bounded structured nesting keeps explicit graph");
        Reject(()=>Clear(p.Scopes),"immutable scopes");Reject(()=>Clear(s.Blocks),"immutable blocks");Reject(()=>Clear(d.Links),"immutable links");
        Reject(()=>Clear(d.Operation.Value.Children),"immutable expression");Reject(()=>Clear(declaration.Names),"immutable declaration");
        Eq(false,FlowSource.Compile(null).CanGenerate,"null returns diagnostics");
        Reject(()=>new FlowSourceText.Builder(FlowSource.Compile(Load("missing-branch")),"python"),"invalid program cannot produce source");
    }
    static void SourceMaps(){
        var p=Compile("all-operations");var builder=new FlowSourceText.Builder(p,"python");
        var main=new FlowSourceText.Origin(null,"n1");var function=new FlowSourceText.Origin("greeting","f-n1");
        builder.AddLine("# generated",0);builder.AddLine("print('è 🐢')",1,main,function,main);builder.AddLine("",0);
        var code=builder.Build();Eq("# generated\n    print('è 🐢')\n\n",code.Text,"source exact text");Eq(3,code.Lines.Count,"physical lines");
        Eq(2,code.Lines[1].Origins.Count,"duplicate origin collapsed");Eq(1,code.ForBlock(null,"n1").Count,"node to line");Eq(1,code.ForBlock("greeting","f-n1").Count,"function node to line");Eq(0,code.ForBlock(null,"f-n1").Count,"scope isolation");
        Eq(true,Object.ReferenceEquals(code.ForBlock(null,"n1"),code.ForBlock(null,"n1")),"indexed lookup reuses immutable lines");Reject(()=>Clear(code.ForBlock(null,"n1")),"indexed mapping immutable");
        for(int i=0;i<code.Text.Length;i++){var line=code.AtOffset(i);Eq(true,line!=null,"every UTF16 unit mapped");Eq(true,i>=line.Start&&i<=line.Start+line.Length,"offset span");}
        Eq(null,code.AtOffset(-1),"negative offset");Eq(null,code.AtOffset(code.Text.Length),"EOF offset");
        Reject(()=>builder.AddLine("bad\nline",0,main),"embedded newline");Reject(()=>builder.AddLine("bad",129,main),"indent cap");
        Reject(()=>builder.AddLine("bad",0,new FlowSourceText.Origin("wrong","n1")),"invalid origin");
        Eq(code.Text,builder.Build().Text,"failed writes are atomic");builder.AddLine("# later",0);Eq(3,code.Lines.Count,"source snapshot is immutable");
        Reject(()=>Clear(code.Lines),"immutable source lines");Reject(()=>Clear(code.Lines[1].Origins),"immutable origins");
        var limit=new FlowSourceText.Builder(p,"python");limit.AddLine(new string('x',FlowSourceText.Builder.MaxCharacters-1),0);
        Reject(()=>limit.AddLine("",0),"source size cap");Eq(FlowSourceText.Builder.MaxCharacters,limit.Build().Text.Length,"limit rejection atomic");
        limit=new FlowSourceText.Builder(p,"python");for(int i=0;i<FlowSourceText.Builder.MaxLines;i++)limit.AddLine("",0);
        Reject(()=>limit.AddLine("",0),"line count cap");Eq(FlowSourceText.Builder.MaxLines,limit.Build().Lines.Count,"line cap atomic");
    }
    public static int Main(){try{
        root=Environment.GetEnvironmentVariable("SOURCE_QA");var output=Path.Combine(root,"pc-models");Directory.CreateDirectory(output);var outcomes=new StringBuilder();
        foreach(var row in File.ReadAllLines(Path.Combine(root,"expected-valid.tsv"))){var columns=row.Split('\t');var name=columns[0];var document=Load(name);var before=DiagramIO.EncodeDocument(document);
            bool expected=!Validator.Validate(document).Any(i=>i.Severity==Severity.Error);var p=FlowSource.Compile(document);
            Eq(expected,p.CanGenerate,name+" validation gate");if(columns[1]!="?")Eq(columns[1]=="true",p.CanGenerate,name+" fixture validity: "+String.Join(";",p.Diagnostics.Select(i=>i.Message)));
            Row(outcomes,name,p.CanGenerate);
            Eq(true,before.SequenceEqual(DiagramIO.EncodeDocument(document)),name+" original unchanged");
            if(!p.CanGenerate){Eq(0,p.Scopes.Count,name+" no partial source");continue;}
            Eq(document.Nodes.Count,p.Scopes[0].Blocks.Count,name+" all main blocks");
            for(int s=0;s<p.Scopes.Count;s++){var g=s==0?document:document.Functions[s-1].Body;Eq(g.Edges.Count,p.Scopes[s].Blocks.Sum(n=>n.Links.Count),name+" all edges");}
            foreach(var scope in p.Scopes)PlanChecks(scope);
            var dump=Dump(p);File.WriteAllText(Path.Combine(output,name+".txt"),dump,new UTF8Encoding(false));
            document.Title="edited";document.Nodes[0].Text="edited";if(document.Edges.Count>0)document.Edges[0].To="edited";
            if(document.Functions!=null)foreach(var f in document.Functions){f.Name="edited";f.Parameters.Clear();}
            Eq(dump,Dump(p),name+" snapshot detached from editor");
        }
        File.WriteAllText(Path.Combine(root,"pc-outcomes.tsv"),outcomes.ToString(),new UTF8Encoding(false));
        StructuralOracles();SourceMaps();Eq(17,kinds.Count,"every node kind compiled");Eq(9,expressionKinds.Count,"every expression kind compiled");
        Console.WriteLine("SOURCE_MODEL PASS checks="+checks+" structured="+structuredPlans+" explicit="+explicitPlans+" traces="+traceChecks);return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
