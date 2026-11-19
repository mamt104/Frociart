using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
namespace Frociart {
    internal static class FlowRuby {
        internal static string TemplateNumber(double value){return Number(value);}
        internal static FlowSourceText Generate(FlowSource.Program program) {
            using(var stream=typeof(FlowRuby).Assembly.GetManifestResourceStream("Frociart.Source.RubyRuntime.rb")) {
                if(stream==null)throw new InvalidOperationException("Il supporto Ruby non è disponibile in questa installazione.");
                using(var reader=new StreamReader(stream,Encoding.UTF8))return Generate(program,reader.ReadToEnd());
            }
        }
        internal static FlowSourceText Generate(FlowSource.Program program,string runtime) {
            if(program==null||!program.CanGenerate)throw new ArgumentException("Correggi il diagramma prima di generare il codice.");
            if(String.IsNullOrWhiteSpace(runtime))throw new ArgumentException("Il supporto Ruby è vuoto.");
            return new Writer(program).Generate(runtime);
        }
        internal static string Literal(string text) {
            if(text==null)return "nil";
            for(int i=0;i<text.Length;i++)if(Char.IsSurrogate(text[i])) {
                if(Char.IsHighSurrogate(text[i])&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])){i++;continue;}
                return "FcRuntime.utf16_units(["+String.Join(", ",text.Select(c=>((int)c).ToString(CultureInfo.InvariantCulture)))+"])";
            }
            var b=new StringBuilder("\"");
            for(int i=0;i<text.Length;i++) {
                char c=text[i];if(c=='"'||c=='\\'||c=='#')b.Append('\\').Append(c);
                else if(c>=32&&c<=126)b.Append(c);
                else if(Char.IsHighSurrogate(c)){int code=Char.ConvertToUtf32(c,text[++i]);b.Append("\\u{").Append(code.ToString("x")).Append('}');}
                else b.Append("\\u{").Append(((int)c).ToString("x")).Append('}');
            }
            return b.Append('"').ToString();
        }
        private static string Number(double n) {
            if(n==0)return "0.0";
            string simple=FlowPython.TemplateNumber(n);
            if(simple.StartsWith("_fc.real(",StringComparison.Ordinal))return "@fc.real("+simple.Substring(9);
            return simple.IndexOf('.')<0&&simple.IndexOf('e')<0&&simple.IndexOf('E')<0?simple+".0":simple;
        }
        private static string Bool(bool v){return v?"true":"false";}
        private static readonly HashSet<string> Keywords=new HashSet<string>(("BEGIN END __ENCODING__ __LINE__ __FILE__ alias and begin break case class def defined do else elsif end ensure false for if in module next nil not or redo rescue retry return self super then true undef unless until when while yield main initialize runtime method").Split(' '),StringComparer.Ordinal);
        private static string Identifier(string name) {
            var b=new StringBuilder();foreach(char c in name){if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.Append(c);else b.Append("_u").Append(((int)c).ToString("x4"));}
            string r=b.ToString();if(r.Length==0||r[0]>='0'&&r[0]<='9'||r[0]>='A'&&r[0]<='Z')r="value_"+r;
            return r.StartsWith("_fc",StringComparison.Ordinal)||Keywords.Contains(r)?"value_"+r:r;
        }
        private sealed class Symbol { internal readonly string Name,Original;internal Symbol(string name,string original){Name=name;Original=original;} }
        private sealed class Writer {
            private readonly FlowSource.Program program;private readonly FlowSourceText.Builder output;private readonly Dictionary<string,string> functions=new Dictionary<string,string>(StringComparer.Ordinal);
            private readonly HashSet<string> globals=new HashSet<string>(StringComparer.Ordinal){"main","runtime","initialize","method"};
            private Dictionary<string,Symbol> names;private List<Symbol> symbols;private FlowSource.Scope scope;private readonly bool references;private int temporary;
            internal Writer(FlowSource.Program p){program=p;references=p.Scopes.SelectMany(s=>s.Parameters).Any(x=>x.ByReference);output=new FlowSourceText.Builder(p,"ruby");foreach(var s in p.Scopes.Where(x=>x.Id!=null))functions.Add(s.Id,Unique("flow_"+Identifier(s.Name),globals));}
            private static string Unique(string seed,HashSet<string> used){string n=seed;int suffix=2;while(!used.Add(n))n=seed+"_"+(suffix++).ToString(CultureInfo.InvariantCulture);return n;}
            private void Line(string code,int indent=0,FlowSource.Block block=null){output.AddLine(code,indent,block==null?new FlowSourceText.Origin[0]:new[]{new FlowSourceText.Origin(scope.Id,block.Id)});}
            internal FlowSourceText Generate(string runtime) {
                Line("# encoding: UTF-8");Line("# Generato con Frociart — autore del software: mamt104");Line("# Progetto: "+Literal(program.Title));Line("# Ruby 3.3+. Il programma non richiede Frociart né gem esterne.");Line("");
                Line("class Programma");Line("def initialize(runtime)",1);Line("@fc = runtime",2);Line("end",1);Line("");foreach(var s in program.Scopes)WriteScope(s);Line("end");Line("");
                Line("# --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---");foreach(string line in runtime.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))Line(line);
                Line("");Line("if $PROGRAM_NAME == __FILE__");Line("runtime = FcRuntime.new",1);Line("program = Programma.new(runtime)",1);Line("exit(runtime.run(program.method(:main)) ? 0 : 1)",1);Line("end");return output.Build();
            }
            private void WriteScope(FlowSource.Scope s) {
                scope=s;foreach(var n in s.Notes)Line("# Annotazione: "+Literal(n.Text),1,n);foreach(var n in s.Blocks)if(!String.IsNullOrEmpty(n.Comment))Line("# Nota ("+(s.Blocks.IndexOf(n)+1)+"): "+Literal(n.Comment),1,n);
                temporary=0;names=new Dictionary<string,Symbol>(StringComparer.OrdinalIgnoreCase);symbols=new List<Symbol>();var used=new HashSet<string>(globals,StringComparer.Ordinal);
                Action<string> add=n=>{if(n!=null&&!names.ContainsKey(n)){var symbol=new Symbol(Unique(Identifier(n),used),n);names.Add(n,symbol);symbols.Add(symbol);}};
                Action<FlowSource.Expression> collect=null;collect=e=>{if(e==null)return;if(e.Kind==FlowSource.ExpressionKind.Variable)add(e.Text);foreach(var c in e.Children)collect(c);};
                foreach(var p in s.Parameters)add(p.Name);
                foreach(var n in s.Blocks){var o=n.Operation;if(o.Input!=null){add(o.Input.Name);collect(o.Input.Index);}if(o.Declaration!=null){foreach(var n1 in o.Declaration.Names)add(n1);collect(o.Declaration.Size);}foreach(var w in o.Writes){add(w.Target.Name);collect(w.Value);collect(w.Target.Index);}collect(o.Value);}
                Line("def "+(s.Id==null?"main":functions[s.Id])+"("+String.Join(", ",s.Parameters.Select(p=>names[p.Name].Name))+")",1);
                Line("@fc.guard_call",2);foreach(var p in s.Parameters)Line(p.ByReference?"@fc.check_reference("+names[p.Name].Name+", "+(int)p.Type+")":"@fc.check_type("+names[p.Name].Name+", "+(int)p.Type+", "+Bool(p.IsArray)+")",2);
                Line("@fc.enter("+Literal(s.Id==null?null:s.Name)+", {"+String.Join(", ",s.Parameters.Select(p=>Literal(names[p.Name].Original)+" => "+names[p.Name].Name))+"}"+(references?", true":"")+")",2);Line("_fc_types = {}",2);
                foreach(var sym in symbols)if(!s.Parameters.Any(p=>p.Name.Equals(sym.Original,StringComparison.OrdinalIgnoreCase)))Line(sym.Name+" = @fc.unset",2);
                Line("begin",2);Line("@fc.check_memory",3);var plan=FlowSourcePlan.Create(s);if(plan.Structured)WriteStatements(plan.Statements,3);else WriteExplicit(3);
                Line("ensure",2);Line("@fc.leave",3);Line("end",2);Line("end",1);
                foreach(var n in s.Blocks.Where(n=>!n.Reachable)){Line("");Line("# Blocco non raggiungibile, escluso dall'esecuzione: "+Literal(n.Text),1,n);}Line("");
            }
            private string Name(string source,Dictionary<string,string> staged=null) {
                if(references)return "@fc.lookup("+Literal(names[source].Original)+")";
                string result;string variable=staged!=null&&staged.TryGetValue(source,out result)?result:names[source].Name;
                return "@fc.value("+variable+", "+Literal(names[source].Original)+")";
            }
            private static bool CallsFunction(FlowSource.Expression e){return e!=null&&(e.FunctionId!=null||e.Children.Any(CallsFunction));}
            private string Expression(FlowSource.Expression e,int indent,FlowSource.Block block,Dictionary<string,string> staged=null,int depth=0,bool track=false) {
                if(e==null)return "nil";if(depth==0)track=track||CallsFunction(e);
                if(depth>=24){string helper="_fc_expr_"+(++temporary);Line(helper+" = -> do",indent,block);string result=Expression(e,indent+1,block,staged,0,track);Line(result,indent+1,block);Line("end",indent,block);return helper+".call";}
                int key=track?++temporary:0;Func<FlowSource.Expression,string> child=c=>{string value=Expression(c,indent,block,staged,depth+1,track);return track?"@fc.hold("+key+", "+value+")":value;};Func<string,string> finish=v=>track&&e.Children.Count>0?"@fc.release("+key+", "+v+")":v;
                switch(e.Kind) {
                    case FlowSource.ExpressionKind.Number:return Number(e.Number);
                    case FlowSource.ExpressionKind.Text:return "@fc.text("+Literal(e.Text)+")";
                    case FlowSource.ExpressionKind.Boolean:return Bool(e.Boolean);
                    case FlowSource.ExpressionKind.Variable:return Name(e.Text,staged);
                    case FlowSource.ExpressionKind.Vector:return finish("@fc.vector(["+String.Join(", ",e.Children.Select(child))+"])");
                    case FlowSource.ExpressionKind.Index:
                        string target=child(e.Children[0]);return finish(references&&e.Children[0].Kind==FlowSource.ExpressionKind.Variable?"@fc.at_name("+Literal(names[e.Children[0].Text].Original)+", -> { "+child(e.Children[1])+" })":"@fc.at("+target+", -> { "+child(e.Children[1])+" })");
                    case FlowSource.ExpressionKind.Unary:
                        string v=child(e.Children[0]);return finish(e.Text=="not"?"(!@fc.truth("+v+"))":e.Text=="-"?"(-@fc.number("+v+"))":"@fc.number("+v+")");
                    case FlowSource.ExpressionKind.Call:
                        var callee=e.FunctionId==null?null:program.Scopes.First(s=>s.Id==e.FunctionId);
                        string args=String.Join(", ",e.Children.Select((c,i)=>callee!=null&&callee.Parameters[i].ByReference?"@fc.hold("+key+", @fc.reference("+Literal(names[c.Text].Original)+", "+Declaration(c.Text)+", "+Parameter(c.Text)+"))":child(c)));
                        return e.FunctionId==null?finish("@fc.intrinsic("+Literal(e.Text)+(args.Length==0?"":", "+args)+")"):"@fc.call("+key+", method(:"+functions[e.FunctionId]+")"+(args.Length==0?"":", "+args)+")";
                    case FlowSource.ExpressionKind.Binary:
                        string a=child(e.Children[0]),b=child(e.Children[1]);if(e.Text=="and"||e.Text=="or")return finish("(@fc.truth("+a+") "+(e.Text=="and"?"&&":"||")+" @fc.truth("+b+"))");
                        if(e.Text=="="||e.Text=="==")return finish("@fc.equal("+a+", "+b+")");if(e.Text=="!="||e.Text=="<>")return finish("(!@fc.equal("+a+", "+b+"))");
                        if(e.Text=="<"||e.Text=="<="||e.Text==">"||e.Text==">=")return finish("@fc.compare("+a+", "+b+", "+Literal(e.Text)+")");
                        string method=e.Text=="&"?"concat":e.Text=="+"?"add":e.Text=="-"?"sub":e.Text=="*"?"mul":e.Text=="/"?"div":e.Text=="%"?"mod":"power";return finish("@fc."+method+"("+a+", "+b+")");
                    default:throw new InvalidOperationException("Espressione Ruby non supportata.");
                }
            }
            private string Test(FlowSource.Block n,int indent){return "@fc.test(-> { "+Expression(n.Operation.Value,indent,n)+" })";}
            private void WriteStatements(IEnumerable<FlowSourcePlan.Statement> statements,int indent) {
                foreach(var s in statements) {
                    if(s.Kind==FlowSourcePlan.Kind.Instruction){WriteInstruction(s.Block,indent);continue;}
                    if(s.Kind==FlowSourcePlan.Kind.Repeat){Line("loop do",indent,s.EntryBlock);WriteBody(s.Body,indent+1,s.Block);Line("break if "+(s.LoopOnTrue?"!":"")+Test(s.Block,indent+1),indent+1,s.Block);Line("end",indent,s.Block);continue;}
                    string test=Test(s.Block,indent);
                    if(s.Kind==FlowSourcePlan.Kind.While){Line("while "+(s.LoopOnTrue?"":"!")+test,indent,s.Block);WriteBody(s.Body,indent+1,s.Block);Line("end",indent,s.Block);}
                    else {Line("if "+test,indent,s.Block);WriteBody(s.Body,indent+1,s.Block);Line("else",indent,s.Block);WriteBody(s.Otherwise,indent+1,s.Block);Line("end",indent,s.Block);}
                }
            }
            private void WriteBody(ICollection<FlowSourcePlan.Statement> body,int indent,FlowSource.Block owner){if(body.Count==0)Line("nil # Nessuna operazione",indent,owner);else WriteStatements(body,indent);}
            private void WriteExplicit(int indent) {
                Line("# Collegamenti espliciti per conservare questo grafo.",indent);Line("_fc_block = "+Literal(scope.StartId),indent);Line("loop do",indent);Line("case _fc_block",indent+1);
                foreach(var n in scope.Blocks) {
                    Line("when "+Literal(n.Id),indent+1,n);
                    if(n.Kind==NodeKind.Decision){Line("if "+Test(n,indent+2),indent+2,n);Line("_fc_block = "+Literal(n.Next("true")),indent+3,n);Line("else",indent+2,n);Line("_fc_block = "+Literal(n.Next("false")),indent+3,n);Line("end",indent+2,n);}
                    else {WriteInstruction(n,indent+2);if(n.Kind!=NodeKind.End)Line("_fc_block = "+Literal(n.Next("next")),indent+2,n);}
                }
                Line("else",indent+1);Line("raise FcError, 'Collegamento non valido nel sorgente modificato.'",indent+2);Line("end",indent+1);Line("end",indent);
            }
            private string Declaration(string name){return "_fc_types["+Literal(names[name].Name)+"]";}
            private string Parameter(string name){var p=scope.Parameters.FirstOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));return p==null?"nil":"["+(int)p.Type+", "+Bool(p.IsArray)+"]";}
            private string Convert(string v,string name){return "@fc.convert("+v+", "+Declaration(name)+", "+Parameter(name)+")";}
            private void WriteInstruction(FlowSource.Block n,int indent) {
                var o=n.Operation;Action<string> emit=line=>Line(line,indent,n);emit(n.Kind==NodeKind.Input?"@fc.guard":"@fc.step");if(n.Kind==NodeKind.Start||n.Kind==NodeKind.Pause)return;
                if(n.Kind==NodeKind.End){if(scope.Id==null){emit("@fc.finish_files");emit("return");}else if(scope.ReturnType==FlowDataType.Void)emit("return");else emit("return @fc.check_type("+Expression(o.Value,indent,n)+", "+(int)scope.ReturnType+", "+Bool(scope.ReturnIsArray)+")");return;}
                if(n.Kind==NodeKind.Process) {
                    emit("@fc.begin_values");var staged=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var order=new List<string>();var temps=new List<string>();
                    foreach(var w in o.Writes){string value=Expression(w.Value,indent,n,staged);
                        if(w.Target.Index!=null){string index=Expression(w.Target.Index,indent,n,staged);value=references?"@fc.set_after_name("+value+", "+Literal(names[w.Target.Name].Original)+", -> { "+index+" })":"@fc.set_after("+value+", "+Name(w.Target.Name,staged)+", -> { "+index+" })";}
                        value="@fc.stage("+Literal(names[w.Target.Name].Original)+", "+Convert(value,w.Target.Name)+(references&&w.Target.Index!=null?", true, true":"")+")";
                        if(o.Writes.Count==1){emit(names[w.Target.Name].Name+" = "+value);continue;}
                        string temp="_fc_tmp_"+(++temporary);temps.Add(temp);emit(temp+" = "+value);if(!staged.ContainsKey(w.Target.Name))order.Add(w.Target.Name);staged[w.Target.Name]=temp;
                    }
                    if(o.Writes.Count>1){emit(String.Join(", ",order.Select(x=>names[x].Name))+" = "+String.Join(", ",order.Select(x=>staged[x])));foreach(string temp in temps)emit(temp+" = nil");}emit("@fc.commit");return;
                }
                if(n.Kind==NodeKind.Declare) {
                    var d=o.Declaration;string size=d.IsArray?Expression(d.Size,indent,n):"0.0",temp="_fc_size_"+(++temporary);emit(temp+" = @fc.integer("+size+", 0, 1024, 'La dimensione')");emit("@fc.begin_values");var values=new List<string>();
                    foreach(string name in d.Names){string value="_fc_tmp_"+(++temporary);values.Add(value);emit(value+" = @fc.stage("+Literal(names[name].Original)+", @fc.initial("+(int)d.Type+", "+Bool(d.IsArray)+", "+temp+"), false)");}
                    emit("@fc.commit(true)");for(int i=0;i<d.Names.Count;i++){string name=d.Names[i];emit(names[name].Name+" = "+values[i]);emit("_fc_types["+Literal(names[name].Name)+"] = ["+(int)d.Type+", "+Bool(d.IsArray)+", "+temp+"]");}foreach(string value in values)emit(value+" = nil");return;
                }
                if(n.Kind==NodeKind.Input||n.Kind==NodeKind.ReadFile) {
                    var target=o.Input;string suffix=Declaration(target.Name)+", "+Parameter(target.Name);if(target.Index!=null)suffix+=", "+Name(target.Name)+", -> { "+Expression(target.Index,indent,n)+" }";
                    string read=n.Kind==NodeKind.Input?"@fc.read("+Literal(names[target.Name].Original)+", "+Literal(n.Prompt??"")+", "+suffix+")":"@fc.read_file("+Literal(names[target.Name].Original)+", "+suffix+")";emit(names[target.Name].Name+" = "+read);if(n.Kind==NodeKind.Input)emit("@fc.step");return;
                }
                if(n.Kind==NodeKind.OpenFile)emit("@fc.require_closed("+Bool(o.FileReading)+")");if(n.Kind==NodeKind.WriteFile)emit("@fc.require_write");string expression=Expression(o.Value,indent,n);
                switch(n.Kind) {
                    case NodeKind.Output:emit("@fc.emit("+expression+(o.OutputNewline?"":", false")+")");break;
                    case NodeKind.Call:emit(expression);break;
                    case NodeKind.OpenFile:emit("@fc.open_file("+Bool(o.FileReading)+", "+expression+")");break;
                    case NodeKind.WriteFile:emit("@fc.write_file("+expression+", "+Bool(o.FileNewline)+")");break;
                    case NodeKind.CloseFile:emit("@fc.close_file("+Bool(o.FileReading)+")");break;
                    case NodeKind.TurtleForward:emit("@fc.turtle_forward("+expression+", "+Bool(o.TurtleDraw)+")");break;
                    case NodeKind.TurtleTurn:emit("@fc.turtle_turn("+expression+", "+Bool(o.TurtleLeft)+")");break;
                    case NodeKind.TurtleHome:emit("@fc.turtle_home");break;
                    case NodeKind.TurtleClear:emit("@fc.turtle_clear");break;
                    case NodeKind.TurtlePen:emit("@fc.turtle_pen("+expression+")");break;
                    default:throw new InvalidOperationException("Blocco Ruby non supportato.");
                }
            }
        }
    }
}
