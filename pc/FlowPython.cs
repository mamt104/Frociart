using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace Frociart
{
    internal static class FlowPython
    {
        internal static string TemplateNumber(double value){return Number(value);}
        internal static FlowSourceText Generate(FlowSource.Program program)
        {
            using(var stream=typeof(FlowPython).Assembly.GetManifestResourceStream("Frociart.Source.PythonRuntime.py"))
            {
                if(stream==null)throw new InvalidOperationException("Il supporto Python non è disponibile in questa installazione.");
                using(var reader=new StreamReader(stream,Encoding.UTF8))return Generate(program,reader.ReadToEnd());
            }
        }
        internal static FlowSourceText Generate(FlowSource.Program program,string runtime)
        {
            if(String.IsNullOrWhiteSpace(runtime))throw new ArgumentException("Il supporto Python è vuoto.");
            return new Writer(program).Generate(runtime);
        }
        internal static string Literal(string text)
        {
            if(text==null)return "None";var b=new StringBuilder("'");
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];if(c=='\'')b.Append("\\'");else if(c=='\\')b.Append("\\\\");
                else if(c>=32&&c<=126)b.Append(c);
                else if(Char.IsHighSurrogate(c)&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1]))b.Append("\\U").Append(Char.ConvertToUtf32(c,text[++i]).ToString("x8"));
                else b.Append("\\u").Append(((int)c).ToString("x4"));
            }
            return b.Append('\'').ToString();
        }
        private static string Number(double number)
        {
            // Simple decimal literals remain readable. Unusual doubles retain all 64 bits on both hosts.
            if(number==0)return "0";
            if(Math.Abs(number)<=9000000000)
            {
                long power=1;
                for(int digits=0;digits<=6;digits++,power*=10)
                {
                    double scaled=number*power;
                    if(scaled!=Math.Truncate(scaled))continue;
                    long whole=(long)Math.Abs(scaled);string text=whole.ToString(CultureInfo.InvariantCulture).PadLeft(digits+1,'0');
                    if(digits>0)text=text.Insert(text.Length-digits,".");if(number<0)text="-"+text;
                    double back;if(Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out back)&&back==number)return text;
                }
            }
            return "_fc.real(0x"+BitConverter.DoubleToInt64Bits(number).ToString("x16")+")";
        }
        private static string Bool(bool value){return value?"True":"False";}
        private static readonly HashSet<string> Keywords=new HashSet<string>(("False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield match case type").Split(' '),StringComparer.Ordinal);
        private static string Identifier(string name)
        {
            var b=new StringBuilder();foreach(char c in name){if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.Append(c);else b.Append("_u").Append(((int)c).ToString("x4"));}
            string result=b.ToString();if(result.Length==0||result[0]>='0'&&result[0]<='9')result="value_"+result;
            if(result.StartsWith("_fc",StringComparison.Ordinal)||Keywords.Contains(result))result="value_"+result;
            return result;
        }
        private sealed class Symbol
        {
            internal readonly string Name,Original;
            internal Symbol(string name,string original){Name=name;Original=original;}
        }
        private sealed class Writer
        {
            private readonly FlowSource.Program program;
            private readonly FlowSourceText.Builder output;
            private readonly Dictionary<string,string> functions=new Dictionary<string,string>(StringComparer.Ordinal);
            private readonly HashSet<string> globals=new HashSet<string>(StringComparer.Ordinal){"main","_fc","_fc_builtins"};
            private Dictionary<string,Symbol> names;
            private List<Symbol> symbols;
            private FlowSource.Scope scope;
            private readonly bool references;
            private int temporary;
            internal Writer(FlowSource.Program program)
            {
                this.program=program;references=program.Scopes.SelectMany(s=>s.Parameters).Any(p=>p.ByReference);output=new FlowSourceText.Builder(program,"python");
                foreach(var f in program.Scopes.Where(s=>s.Id!=null))functions.Add(f.Id,Unique("flow_"+Identifier(f.Name),globals));
            }
            private static string Unique(string seed,HashSet<string> used){string name=seed;int suffix=2;while(!used.Add(name))name=seed+"_"+(suffix++).ToString(CultureInfo.InvariantCulture);return name;}
            private void Line(string code,int indent=0,FlowSource.Block block=null)
            {output.AddLine(code,indent,block==null?new FlowSourceText.Origin[0]:new[]{new FlowSourceText.Origin(scope.Id,block.Id)});}
            internal FlowSourceText Generate(string runtime)
            {
                Line("# -*- coding: utf-8 -*-");Line("# Generato con Frociart — autore del software: mamt104");Line("# Progetto: "+Literal(program.Title));
                Line("# Python 3. Il programma non richiede Frociart né pacchetti esterni.");Line("");
                foreach(var s in program.Scopes)WriteScope(s);
                Line("# --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---");
                foreach(string line in runtime.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))Line(line);
                Line("");Line("_fc = _fc_Runtime()");Line("");Line("if __name__ == '__main__':");Line("_fc_sys.exit(0 if _fc.run(main) else 1)",1);
                return output.Build();
            }
            private void WriteScope(FlowSource.Scope s)
            {
                scope=s;foreach(var annotation in s.Notes)Line("# Annotazione: "+Literal(annotation.Text),0,annotation);foreach(var note in s.Blocks)if(!String.IsNullOrEmpty(note.Comment))Line("# Nota ("+(s.Blocks.IndexOf(note)+1)+"): "+Literal(note.Comment),0,note);temporary=0;names=new Dictionary<string,Symbol>(StringComparer.OrdinalIgnoreCase);symbols=new List<Symbol>();
                var used=new HashSet<string>(globals,StringComparer.Ordinal);
                Action<string> add=name=>{if(name!=null&&!names.ContainsKey(name)){var symbol=new Symbol(Unique(Identifier(name),used),name);names.Add(name,symbol);symbols.Add(symbol);}};
                Action<FlowSource.Expression> collect=null;collect=e=>{if(e==null)return;if(e.Kind==FlowSource.ExpressionKind.Variable)add(e.Text);foreach(var child in e.Children)collect(child);};
                foreach(var p in s.Parameters)add(p.Name);
                foreach(var n in s.Blocks){var o=n.Operation;if(o.Input!=null){add(o.Input.Name);collect(o.Input.Index);}if(o.Declaration!=null){foreach(var name in o.Declaration.Names)add(name);collect(o.Declaration.Size);}foreach(var w in o.Writes){add(w.Target.Name);collect(w.Value);collect(w.Target.Index);}collect(o.Value);}
                Line("def "+(s.Id==null?"main":functions[s.Id])+"("+String.Join(", ",s.Parameters.Select(p=>names[p.Name].Name))+"):");
                Line("_fc.guard_call()",1);
                foreach(var p in s.Parameters)Line(p.ByReference?"_fc.check_reference("+names[p.Name].Name+", "+(int)p.Type+")":"_fc.check_type("+names[p.Name].Name+", "+(int)p.Type+", "+Bool(p.IsArray)+")",1);
                Line("_fc.enter("+Literal(s.Id==null?null:s.Name)+", {"+String.Join(", ",s.Parameters.Select(p=>Literal(names[p.Name].Original)+": "+names[p.Name].Name))+"}"+(references?", True":"")+")",1);Line("_fc_types = {}",1);
                foreach(var sym in symbols)if((sym.Original.Equals("pi",StringComparison.OrdinalIgnoreCase)||sym.Original=="π")&&!s.Parameters.Any(p=>p.Name.Equals(sym.Original,StringComparison.OrdinalIgnoreCase)))Line(sym.Name+" = _fc.unset",1);
                Line("try:",1);
                Line("_fc.check_memory()",2);
                var plan=FlowSourcePlan.Create(s);
                if(plan.Structured)WriteStatements(plan.Statements,2);else WriteExplicit(2);
                Line("finally:",1);
                Line("_fc.leave()",2);
                foreach(var n in s.Blocks.Where(n=>!n.Reachable)){Line("");Line("# Blocco non raggiungibile, escluso dall'esecuzione: "+Literal(n.Text),0,n);}
                Line("");Line("");
            }
            private string Name(string source,Dictionary<string,string> staged=null)
            {if(references)return "_fc.lookup("+Literal(names[source].Original)+")";string result;return staged!=null&&staged.TryGetValue(source,out result)?result:names[source].Name;}
            private static bool CallsFunction(FlowSource.Expression e){return e!=null&&(e.FunctionId!=null||e.Children.Any(CallsFunction));}
            private string Expression(FlowSource.Expression e,int indent,FlowSource.Block block,Dictionary<string,string> staged=null,int depth=0,bool track=false)
            {
                if(e==null)return "None";
                if(depth==0)track=track||CallsFunction(e);
                if(depth>=24){string helper="_fc_expr_"+(++temporary);Line("def "+helper+"():",indent,block);string result=Expression(e,indent+1,block,staged,0,track);Line("return "+result,indent+1,block);return helper+"()";}
                int key=track?++temporary:0;
                Func<FlowSource.Expression,string> child=c=>{string result=Expression(c,indent,block,staged,depth+1,track);return track?"_fc.hold("+key+", "+result+")":result;};
                Func<string,string> finish=value=>track&&e.Children.Count>0?"_fc.release("+key+", "+value+")":value;
                switch(e.Kind)
                {
                    case FlowSource.ExpressionKind.Number:return Number(e.Number);
                    case FlowSource.ExpressionKind.Text:return Literal(e.Text);
                    case FlowSource.ExpressionKind.Boolean:return Bool(e.Boolean);
                    case FlowSource.ExpressionKind.Variable:
                        string name=Name(e.Text,staged);return e.Text.Equals("pi",StringComparison.OrdinalIgnoreCase)||e.Text=="π"?"_fc.value("+name+", "+Literal(e.Text)+")":name;
                    case FlowSource.ExpressionKind.Vector:return finish("_fc.vector(["+String.Join(", ",e.Children.Select(child))+"])");
                    case FlowSource.ExpressionKind.Index:
                        string target=child(e.Children[0]);return finish(references&&e.Children[0].Kind==FlowSource.ExpressionKind.Variable?"_fc.at_name("+Literal(names[e.Children[0].Text].Original)+", lambda: "+child(e.Children[1])+")":"_fc.at("+target+", lambda: "+child(e.Children[1])+")");
                    case FlowSource.ExpressionKind.Unary:
                        string v=child(e.Children[0]);return finish(e.Text=="not"?"(not _fc.truth("+v+"))":e.Text=="-"?"(-_fc.number("+v+"))":"_fc.number("+v+")");
                    case FlowSource.ExpressionKind.Call:
                        var callee=e.FunctionId==null?null:program.Scopes.First(s=>s.Id==e.FunctionId);
                        string args=String.Join(", ",e.Children.Select((c,i)=>callee!=null&&callee.Parameters[i].ByReference?"_fc.hold("+key+", _fc.reference("+Literal(names[c.Text].Original)+", "+Declaration(c.Text)+", "+Parameter(c.Text)+"))":child(c)));return e.FunctionId==null?finish("_fc.intrinsic("+Literal(e.Text)+(args.Length==0?"":", "+args)+")"):"_fc.call("+key+", "+functions[e.FunctionId]+(args.Length==0?"":", "+args)+")";
                    case FlowSource.ExpressionKind.Binary:
                        string a=child(e.Children[0]),b=child(e.Children[1]);
                        if(e.Text=="and"||e.Text=="or")return finish("(_fc.truth("+a+") "+e.Text+" _fc.truth("+b+"))");
                        if(e.Text=="="||e.Text=="==")return finish("_fc.equal("+a+", "+b+")");
                        if(e.Text=="!="||e.Text=="<>")return finish("(not _fc.equal("+a+", "+b+"))");
                        if(e.Text=="<"||e.Text=="<="||e.Text==">"||e.Text==">=")return finish("_fc.compare("+a+", "+b+", "+Literal(e.Text)+")");
                        string method=e.Text=="&"?"concat":e.Text=="+"?"add":e.Text=="-"?"sub":e.Text=="*"?"mul":e.Text=="/"?"div":e.Text=="%"?"mod":"power";
                        return finish("_fc."+method+"("+a+", "+b+")");
                    default:throw new InvalidOperationException("Espressione Python non supportata.");
                }
            }
            private string Test(FlowSource.Block n,int indent){string e=Expression(n.Operation.Value,indent,n);return "_fc.test(lambda: "+e+")";}
            private void WriteStatements(IEnumerable<FlowSourcePlan.Statement> statements,int indent)
            {
                foreach(var statement in statements)
                {
                    if(statement.Kind==FlowSourcePlan.Kind.Instruction){WriteInstruction(statement.Block,indent);continue;}
                    if(statement.Kind==FlowSourcePlan.Kind.Repeat){Line("while True:",indent,statement.EntryBlock);WriteBody(statement.Body,indent+1,statement.Block);string condition=Test(statement.Block,indent+1);Line("if "+(statement.LoopOnTrue?"not ":"")+condition+":",indent+1,statement.Block);Line("break",indent+2,statement.Block);continue;}
                    string test=Test(statement.Block,indent);
                    if(statement.Kind==FlowSourcePlan.Kind.While){Line("while "+(statement.LoopOnTrue?"":"not ")+test+":",indent,statement.Block);WriteBody(statement.Body,indent+1,statement.Block);}
                    else {Line("if "+test+":",indent,statement.Block);WriteBody(statement.Body,indent+1,statement.Block);Line("else:",indent,statement.Block);WriteBody(statement.Otherwise,indent+1,statement.Block);}
                }
            }
            private void WriteBody(ICollection<FlowSourcePlan.Statement> body,int indent,FlowSource.Block owner){if(body.Count==0)Line("pass",indent,owner);else WriteStatements(body,indent);}
            private void WriteExplicit(int indent)
            {
                Line("# Collegamenti espliciti per conservare questo grafo.",indent);Line("_fc_block = "+Literal(scope.StartId),indent);Line("while True:",indent);
                bool first=true;foreach(var n in scope.Blocks)
                {
                    Line((first?"if ":"elif ")+"_fc_block == "+Literal(n.Id)+":",indent+1,n);first=false;
                    if(n.Kind==NodeKind.Decision){string test=Test(n,indent+2);Line("if "+test+":",indent+2,n);Line("_fc_block = "+Literal(n.Next("true")),indent+3,n);Line("else:",indent+2,n);Line("_fc_block = "+Literal(n.Next("false")),indent+3,n);}
                    else {WriteInstruction(n,indent+2);if(n.Kind!=NodeKind.End)Line("_fc_block = "+Literal(n.Next("next")),indent+2,n);}
                }
                Line("else:",indent+1);Line("raise _fc_Error('Collegamento non valido nel sorgente modificato.')",indent+2);
            }
            private string Declaration(string name){return "_fc_types.get("+Literal(names[name].Name)+")";}
            private string Parameter(string name){var p=scope.Parameters.FirstOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));return p==null?"None":"("+(int)p.Type+", "+Bool(p.IsArray)+")";}
            private string Convert(string value,string name){return "_fc.convert("+value+", "+Declaration(name)+", "+Parameter(name)+")";}
            private void WriteInstruction(FlowSource.Block n,int indent)
            {
                var o=n.Operation;Action<string> emit=line=>Line(line,indent,n);
                if(n.Kind==NodeKind.Input)emit("_fc.guard()");else emit("_fc.step()");
                if(n.Kind==NodeKind.Start||n.Kind==NodeKind.Pause)return;
                if(n.Kind==NodeKind.End){
                    if(scope.Id==null){emit("_fc.finish_files()");emit("return");}
                    else if(scope.ReturnType==FlowDataType.Void)emit("return");
                    else {string result=Expression(o.Value,indent,n);emit("return _fc.check_type("+result+", "+(int)scope.ReturnType+", "+Bool(scope.ReturnIsArray)+")");}return;
                }
                if(n.Kind==NodeKind.Process)
                {
                    emit("_fc.begin()");
                    var staged=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var order=new List<string>();var temps=new List<string>();
                    foreach(var write in o.Writes){string value=Expression(write.Value,indent,n,staged);
                        if(write.Target.Index!=null){string index=Expression(write.Target.Index,indent,n,staged);value=references?"_fc.set_after_name("+value+", "+Literal(names[write.Target.Name].Original)+", lambda: "+index+")":"_fc.set_after("+value+", "+Name(write.Target.Name,staged)+", lambda: "+index+")";}
                        value=Convert(value,write.Target.Name);
                        value="_fc.stage("+Literal(names[write.Target.Name].Original)+", "+value+(references&&write.Target.Index!=null?", indexed=True":"")+")";
                        if(o.Writes.Count==1){emit(names[write.Target.Name].Name+" = "+value);continue;}
                        string temp="_fc_tmp_"+(++temporary);temps.Add(temp);emit(temp+" = "+value);if(!staged.ContainsKey(write.Target.Name))order.Add(write.Target.Name);staged[write.Target.Name]=temp;
                    }
                    if(o.Writes.Count>1){emit(String.Join(", ",order.Select(name=>names[name].Name))+" = "+String.Join(", ",order.Select(name=>staged[name])));emit("del "+String.Join(", ",temps));}emit("_fc.commit()");return;
                }
                if(n.Kind==NodeKind.Declare)
                {
                    var d=o.Declaration;string size=d.IsArray?Expression(d.Size,indent,n):"0";string temp="_fc_size_"+(++temporary);
                    emit(temp+" = _fc.integer("+size+", 0, 1024, 'La dimensione')");
                    emit("_fc.begin()");var values=new List<string>();
                    foreach(string name in d.Names){string value="_fc_tmp_"+(++temporary);values.Add(value);emit(value+" = _fc.stage("+Literal(names[name].Original)+", _fc.initial("+(int)d.Type+", "+Bool(d.IsArray)+", "+temp+"), False)");}
                    emit("_fc.commit(True)");
                    for(int i=0;i<d.Names.Count;i++){string name=d.Names[i];emit(names[name].Name+" = "+values[i]);emit("_fc_types["+Literal(names[name].Name)+"] = ("+(int)d.Type+", "+Bool(d.IsArray)+", "+temp+")");}emit("del "+String.Join(", ",values));return;
                }
                if(n.Kind==NodeKind.Input||n.Kind==NodeKind.ReadFile)
                {
                    var target=o.Input;string suffix=Declaration(target.Name)+", "+Parameter(target.Name);
                    if(target.Index!=null)suffix+=", "+Name(target.Name)+", lambda: "+Expression(target.Index,indent,n);
                    string read=n.Kind==NodeKind.Input?"_fc.read("+Literal(names[target.Name].Original)+", "+Literal(n.Prompt??"")+", "+suffix+")":"_fc.read_file("+Literal(names[target.Name].Original)+", "+suffix+")";
                    emit(names[target.Name].Name+" = "+read);if(n.Kind==NodeKind.Input)emit("_fc.step()");return;
                }
                if(n.Kind==NodeKind.OpenFile)emit("_fc.require_closed("+Bool(o.FileReading)+")");
                if(n.Kind==NodeKind.WriteFile)emit("_fc.require_write()");
                string expression=Expression(o.Value,indent,n);
                switch(n.Kind)
                {
                    case NodeKind.Output:emit("_fc.emit("+expression+(o.OutputNewline?"":", False")+")");break;
                    case NodeKind.Call:emit(expression);break;
                    case NodeKind.OpenFile:emit("_fc.open_file("+Bool(o.FileReading)+", "+expression+")");break;
                    case NodeKind.WriteFile:emit("_fc.write_file("+expression+", "+Bool(o.FileNewline)+")");break;
                    case NodeKind.CloseFile:emit("_fc.close_file("+Bool(o.FileReading)+")");break;
                    case NodeKind.TurtleForward:emit("_fc.turtle_forward("+expression+", "+Bool(o.TurtleDraw)+")");break;
                    case NodeKind.TurtleTurn:emit("_fc.turtle_turn("+expression+", "+Bool(o.TurtleLeft)+")");break;
                    case NodeKind.TurtleHome:emit("_fc.turtle_home()");break;
                    case NodeKind.TurtleClear:emit("_fc.turtle_clear()");break;
                    case NodeKind.TurtlePen:emit("_fc.turtle_pen("+expression+")");break;
                    default:throw new InvalidOperationException("Blocco Python non supportato.");
                }
            }
        }
    }
}
