using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Frociart
{
    // Emits Java instructions and methods. No diagram interpreter is exported.
    internal static class FlowJava
    {
        internal static string TemplateNumber(double value){return Number(value);}
        internal static FlowSourceText Generate(FlowSource.Program program)
        {
            using(var stream=typeof(FlowJava).Assembly.GetManifestResourceStream("Frociart.Source.JavaRuntime.txt"))
            {
                if(stream==null)throw new InvalidOperationException("Il supporto Java non è disponibile in questa installazione.");
                using(var reader=new StreamReader(stream,Encoding.UTF8))return Generate(program,reader.ReadToEnd());
            }
        }
        internal static FlowSourceText Generate(FlowSource.Program program,string runtime,string className="Programma")
        {
            if(String.IsNullOrWhiteSpace(runtime))throw new ArgumentException("Il supporto Java è vuoto.");
            if(className==null||!Regex.IsMatch(className,@"\A[A-Za-z_][A-Za-z_0-9]*\z")||Keywords.Contains(className))throw new ArgumentException("Nome della classe Java non valido.");
            return new Writer(program).Generate(runtime,className);
        }
        internal static string Literal(string text)
        {
            if(text==null)return "null";var b=new StringBuilder("\"");
            foreach(char c in text){if(c=='"')b.Append("\\\"");else if(c=='\\')b.Append("\\\\");else if(c=='\n')b.Append("\\n");else if(c=='\r')b.Append("\\r");else if(c=='\t')b.Append("\\t");else if(c<32||c==127)b.Append('\\').Append(Convert.ToString((int)c,8).PadLeft(3,'0'));else if(c>126)b.Append("\\u").Append(((int)c).ToString("x4"));else b.Append(c);}
            return b.Append('"').ToString();
        }
        static readonly HashSet<string> Keywords=new HashSet<string>(("abstract assert boolean break byte case catch char class const continue default do double else enum extends final finally float for goto if implements import instanceof int interface long native new package private protected public return short static strictfp super switch synchronized this throw throws transient try void volatile while true false null var yield record sealed permits non_sealed _").Split(' '),StringComparer.Ordinal);
        static string Bool(bool value){return value?"true":"false";}
        static string Identifier(string name){var b=new StringBuilder();foreach(char c in name)if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.Append(c);else b.Append("_u").Append(((int)c).ToString("x4"));string value=b.ToString();if(value.Length==0||Char.IsDigit(value[0]))value="value_"+value;if(value.StartsWith("_fc",StringComparison.Ordinal)||Keywords.Contains(value))value="value_"+value;return value;}
        static string Unique(string seed,HashSet<string> names){string value=seed;int n=2;while(!names.Add(value))value=seed+"_"+(n++).ToString(CultureInfo.InvariantCulture);return value;}
        static string Number(double n){if(n==0)return "Value.number(0)";if(Math.Abs(n)<=9000000000d){long power=1;for(int digits=0;digits<=6;digits++,power*=10){double scaled=n*power;if(scaled!=Math.Floor(scaled))continue;string text=((long)Math.Abs(scaled)).ToString(CultureInfo.InvariantCulture).PadLeft(digits+1,'0');if(digits>0)text=text.Insert(text.Length-digits,".");if(n<0)text="-"+text;if(Double.Parse(text,CultureInfo.InvariantCulture)==n)return "Value.number("+text+(digits==0?"d":"")+")";}}return "Value.number(Double.longBitsToDouble(0x"+BitConverter.DoubleToInt64Bits(n).ToString("x16")+"L))";}
        sealed class Symbol {internal readonly string Name,Original;internal Symbol(string n,string o){Name=n;Original=o;}}
        sealed class Helper {internal string Name;internal FlowSource.Expression Expression;internal FlowSource.Block Block;internal HashSet<string> Staged;internal bool Track;}
        sealed class Writer
        {
            readonly FlowSource.Program program;readonly FlowSourceText.Builder output;
            readonly Dictionary<string,string> functions=new Dictionary<string,string>(),classes=new Dictionary<string,string>();
            readonly HashSet<string> globals=new HashSet<string>(new[]{"Value","TypeSpec","Intrinsics","Double","Arrays","java","_fc","Programma","run"},StringComparer.Ordinal);
            Dictionary<string,Symbol> names;List<Symbol> symbols;Dictionary<string,string> blockMethods;List<Helper> helpers;FlowSource.Scope scope;int temporary;bool inlineScope;readonly bool references;
            internal Writer(FlowSource.Program p){program=p;references=p.Scopes.SelectMany(s=>s.Parameters).Any(v=>v.ByReference);output=new FlowSourceText.Builder(p,"java");int i=0;foreach(var s in p.Scopes){classes[s.Id??""]="_fc_Scope"+(i++);if(s.Id!=null)functions[s.Id]=Unique("flow_"+Identifier(s.Name),globals);}}
            void Line(string text,int indent,FlowSource.Block block=null){output.AddLine(text,indent,block==null?new FlowSourceText.Origin[0]:new[]{new FlowSourceText.Origin(scope.Id,block.Id)});}
            internal FlowSourceText Generate(string runtime,string className)
            {
                Line("// Generato con Frociart — autore del software: mamt104",0);Line("// Progetto: "+Literal(program.Title),0);Line("// Java 8+. Salva come "+className+".java; compila con javac ed esegui con java "+className+".",0);
                foreach(string import in new[]{"java.util.*","java.math.*","java.io.*","java.nio.*","java.nio.charset.*"})Line("import "+import+";",0);Line("",0);
                Line("public final class "+className+" {",0);Line("static _fc_Runtime _fc = new _fc_Runtime();",1);Line("public static void main(String[] args) {",1);Line("if (!_fc.run(() -> new _fc_Scope0().run(), true)) System.exit(1);",2);Line("}",1);
                foreach(var s in program.Scopes){if(s.Id!=null){var ps=new List<string>();var args=new List<string>();var used=new HashSet<string>(globals);foreach(var p in s.Parameters){string n=Unique(Identifier(p.Name),used);ps.Add("Value "+n);args.Add(n);}Line("static Value "+functions[s.Id]+"("+String.Join(", ",ps)+") {",1);Line("return new "+classes[s.Id]+"().run("+String.Join(", ",args)+");",2);Line("}",1);}WriteScope(s);}
                Line("",1);Line("// --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---",1);
                foreach(string text in runtime.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))Line(text,1);Line("}",0);return output.Build();
            }
            void Collect(FlowSource.Expression e,Action<string> add){if(e==null)return;if(e.Kind==FlowSource.ExpressionKind.Variable)add(e.Text);foreach(var child in e.Children)Collect(child,add);}
            void WriteScope(FlowSource.Scope s)
            {
                scope=s;inlineScope=CanInlineScope(s);foreach(var annotation in s.Notes)Line("// Annotazione: "+Literal(annotation.Text),1,annotation);foreach(var note in s.Blocks)if(!String.IsNullOrEmpty(note.Comment))Line("// Nota ("+(s.Blocks.IndexOf(note)+1)+"): "+Literal(note.Comment),1,note);temporary=0;names=new Dictionary<string,Symbol>(StringComparer.OrdinalIgnoreCase);symbols=new List<Symbol>();helpers=new List<Helper>();blockMethods=new Dictionary<string,string>();var used=new HashSet<string>(globals);
                Action<string> add=n=>{if(n!=null&&!names.ContainsKey(n)){var sym=new Symbol(Unique(Identifier(n),used),n);names.Add(n,sym);symbols.Add(sym);}};
                foreach(var p in s.Parameters)add(p.Name);int index=0;
                foreach(var b in s.Blocks){blockMethods[b.Id]="_fc_block"+(index++);var o=b.Operation;if(o.Input!=null){add(o.Input.Name);Collect(o.Input.Index,add);}if(o.Declaration!=null){foreach(string n in o.Declaration.Names)add(n);Collect(o.Declaration.Size,add);}foreach(var w in o.Writes){add(w.Target.Name);Collect(w.Value,add);Collect(w.Target.Index,add);}Collect(o.Value,add);}
                Line("",1);Line("static final class "+classes[s.Id??""]+" {",1);foreach(var sym in symbols){Line("Value "+sym.Name+";",2);Line("TypeSpec _fc_type_"+sym.Name+";",2);}
                var args=new List<string>();var pnames=new List<string>();var pvalues=new List<string>();index=0;foreach(var p in s.Parameters){string a="_fc_arg"+(index++);args.Add("Value "+a);pnames.Add(Literal(names[p.Name].Original));pvalues.Add(a);}
                Line("Value run("+String.Join(", ",args)+") {",2);Line("_fc.guardCall();",3);index=0;foreach(var p in s.Parameters)Line(names[p.Name].Name+" = _fc."+(p.ByReference?"checkReference":"checkType")+"(_fc_arg"+(index++)+", "+(int)p.Type+(p.ByReference?"":", "+Bool(p.IsArray))+");",3);
                Line("_fc.enter("+Literal(s.Id==null?null:s.Name)+", new String[]{"+String.Join(", ",pnames)+"}, new Value[]{"+String.Join(", ",pvalues)+"}"+(references?", true":"")+");",3);Line("try {",3);Line("_fc.checkMemory();",4);
                var plan=FlowSourcePlan.Create(s);if(plan.Structured)Statements(plan.Statements,4);else Explicit(4);
                Line("} catch (RuntimeException | Error failure) {",3);Line("_fc.captureError(); throw failure;",4);Line("} finally { _fc.leave(); }",3);Line("}",2);
                if(!inlineScope)foreach(var b in s.Blocks)Instruction(b);
                else foreach(var b in s.Blocks.Where(b=>!b.Reachable))Line("// Blocco non raggiungibile, escluso dall’esecuzione: "+Literal(b.Text),2,b);
                for(int i=0;i<helpers.Count;i++){var h=helpers[i];Line("private Value "+h.Name+"() {",2,h.Block);string e=Expression(h.Expression,h.Block,h.Staged,0,h.Track);Line("return "+e+";",3,h.Block);Line("}",2,h.Block);}
                Line("}",1);
            }
            // Keep large scopes split into methods: JVM bytecode has a per-method size limit.
            // This conservative work budget also bounds generated in-place statements on both hosts.
            bool CanInlineScope(FlowSource.Scope current)
            {
                int cost=0;var pending=new Stack<FlowSource.Expression>();
                Action<FlowSource.Expression> add=e=>{if(e!=null)pending.Push(e);};
                foreach(var block in current.Blocks){var o=block.Operation;cost+=64+o.Writes.Count*48;
                    add(o.Value);if(o.Input!=null)add(o.Input.Index);
                    if(o.Declaration!=null){cost+=o.Declaration.Names.Count*32;add(o.Declaration.Size);}
                    foreach(var w in o.Writes){add(w.Value);add(w.Target.Index);}
                    while(pending.Count>0){var e=pending.Pop();cost+=32;if(cost>24000)return false;foreach(var child in e.Children)add(child);}
                    if(cost>24000)return false;
                }
                return true;
            }
            string Condition(FlowSource.Block block){return inlineScope?"_fc.test(() -> "+Expression(block.Operation.Value,block)+")":blockMethods[block.Id]+"().asBoolean()";}
            string Name(string name,HashSet<string> staged){var sym=names[name];return references?"_fc.lookup("+Literal(sym.Original)+")":"_fc.value("+(staged.Contains(name.ToLowerInvariant())?"_fc.staged("+Literal(sym.Original)+")":sym.Name)+", "+Literal(sym.Original)+")";}
            bool Calls(FlowSource.Expression e){return e!=null&&(e.FunctionId!=null||e.Children.Any(Calls));}
            string Expression(FlowSource.Expression e,FlowSource.Block block){return Expression(e,block,new HashSet<string>(),0,false);}
            string Expression(FlowSource.Expression e,FlowSource.Block block,HashSet<string> staged,int depth,bool track)
            {
                if(e==null)return "null";if(depth==0)track|=Calls(e);if(depth>=24){string helper="_fc_expr"+(++temporary);helpers.Add(new Helper{Name=helper,Expression=e,Block=block,Staged=new HashSet<string>(staged),Track=track});return helper+"()";}
                bool tracked=track;int key=track?++temporary:0;
                Func<FlowSource.Expression,string> child=c=>{string value=Expression(c,block,staged,depth+1,tracked);return tracked?"_fc.hold("+key+", "+value+")":value;};
                Func<string,string> finish=value=>tracked&&e.Children.Count>0?"_fc.release("+key+", "+value+")":value;
                switch(e.Kind){
                    case FlowSource.ExpressionKind.Number:return Number(e.Number);
                    case FlowSource.ExpressionKind.Text:return "Value.text("+Literal(e.Text)+")";
                    case FlowSource.ExpressionKind.Boolean:return "Value.bool("+Bool(e.Boolean)+")";
                    case FlowSource.ExpressionKind.Variable:return Name(e.Text,staged);
                    case FlowSource.ExpressionKind.Vector:return finish("Value.vector(Arrays.asList(new Value[]{"+String.Join(", ",e.Children.Select(child))+"}))");
                    case FlowSource.ExpressionKind.Index:{string target=child(e.Children[0]);return finish(references&&e.Children[0].Kind==FlowSource.ExpressionKind.Variable?"_fc.atName("+Literal(names[e.Children[0].Text].Original)+", () -> "+child(e.Children[1])+")":"_fc.at("+target+", () -> "+child(e.Children[1])+")");}
                    case FlowSource.ExpressionKind.Unary:return finish("_fc.unary("+Literal(e.Text)+", "+child(e.Children[0])+")");
                    case FlowSource.ExpressionKind.Call:{var callee=e.FunctionId==null?null:program.Scopes.First(s=>s.Id==e.FunctionId);string args=String.Join(", ",e.Children.Select((c,i)=>callee!=null&&callee.Parameters[i].ByReference?"_fc.hold("+key+", _fc.reference("+Literal(names[c.Text].Original)+", "+Declaration(c.Text)+", "+Parameter(c.Text)+"))":child(c)));return e.FunctionId==null?finish("_fc.intrinsic("+Literal(e.Text)+(args.Length==0?"":", "+args)+")"):"_fc.call("+key+", () -> "+functions[e.FunctionId]+"("+args+"))";}
                    case FlowSource.ExpressionKind.Binary:{string a=child(e.Children[0]),b=child(e.Children[1]);if(e.Text=="and"||e.Text=="or")return finish("Value.bool("+a+".asBoolean() "+(e.Text=="and"?"&&":"||")+" "+b+".asBoolean())");return finish("_fc.binary("+Literal(e.Text)+", "+a+", "+b+")");}
                    default:throw new ArgumentException("Espressione Java non supportata.");
                }
            }
            void Statements(IEnumerable<FlowSourcePlan.Statement> list,int indent){foreach(var s in list){if(s.Kind==FlowSourcePlan.Kind.Instruction){if(inlineScope)Instruction(s.Block,indent,true);else Line((s.Block.Kind==NodeKind.End?"return ":"")+blockMethods[s.Block.Id]+"();",indent,s.Block);}else if(s.Kind==FlowSourcePlan.Kind.Repeat){Line("do {",indent,s.EntryBlock);Statements(s.Body,indent+1);Line("} while ("+(s.LoopOnTrue?"":"!")+Condition(s.Block)+");",indent,s.Block);}else if(s.Kind==FlowSourcePlan.Kind.While){Line("while ("+(s.LoopOnTrue?"":"!")+Condition(s.Block)+") {",indent,s.Block);Statements(s.Body,indent+1);Line("}",indent,s.Block);}else{Line("if ("+Condition(s.Block)+") {",indent,s.Block);Statements(s.Body,indent+1);Line("} else {",indent,s.Block);Statements(s.Otherwise,indent+1);Line("}",indent,s.Block);}}}
            void Explicit(int indent){Line("// Collegamenti espliciti per conservare questo grafo.",indent);Line("String _fc_next = "+Literal(scope.StartId)+";",indent);Line("while (true) {",indent);Line("switch (_fc_next) {",indent+1);foreach(var b in scope.Blocks){Line("case "+Literal(b.Id)+":",indent+2,b);if(b.Kind==NodeKind.Decision)Line("_fc_next = "+Condition(b)+" ? "+Literal(b.Next("true"))+" : "+Literal(b.Next("false"))+"; break;",indent+3,b);else if(b.Kind==NodeKind.End){if(inlineScope)Instruction(b,indent+3,true);else Line("return "+blockMethods[b.Id]+"();",indent+3,b);}else{if(inlineScope)Instruction(b,indent+3,true);else Line(blockMethods[b.Id]+"();",indent+3,b);Line("_fc_next = "+Literal(b.Next("next"))+"; break;",indent+3,b);}}Line("default: throw problem(\"Collegamento non valido nel sorgente modificato.\");",indent+2);Line("}",indent+1);Line("}",indent);}
            string Declaration(string n){return "_fc_type_"+names[n].Name;}
            string Parameter(string n){var p=scope.Parameters.FirstOrDefault(x=>x.Name.Equals(n,StringComparison.OrdinalIgnoreCase));return p==null?"null":"new TypeSpec("+(int)p.Type+", "+Bool(p.IsArray)+", 0)";}
            string ConvertValue(string value,string n){return "_fc.convert("+value+", "+Declaration(n)+", "+Parameter(n)+")";}
            void Instruction(FlowSource.Block b,int indent=2,bool inline=false)
            {
                var o=b.Operation;if(!inline)Line("private Value "+blockMethods[b.Id]+"() {",indent,b);else if(b.Kind==NodeKind.Declare)Line("{",indent,b);
                int bodyIndent=indent+(inline&&b.Kind!=NodeKind.Declare?0:1);Action<string> emit=text=>Line(text,bodyIndent,b);
                if(b.Kind==NodeKind.Decision){emit("return Value.bool(_fc.test(() -> "+Expression(o.Value,b)+"));");Line("}",2,b);return;}
                emit(b.Kind==NodeKind.Input?"_fc.guard();":"_fc.step();");
                if(b.Kind==NodeKind.End){if(scope.Id==null)emit("_fc.finishFiles();");emit(scope.Id==null||scope.ReturnType==FlowDataType.Void?"return null;":"return _fc.checkType("+Expression(o.Value,b)+", "+(int)scope.ReturnType+", "+Bool(scope.ReturnIsArray)+");");if(!inline)Line("}",indent,b);return;}
                if(b.Kind==NodeKind.Process&&inline){
                    emit("_fc.begin();");var staged=new HashSet<string>();
                    foreach(var w in o.Writes){string value=Expression(w.Value,b,staged,0,false);
                        if(w.Target.Index!=null)value=references?"_fc.setAfterName("+value+", "+Literal(names[w.Target.Name].Original)+", () -> "+Expression(w.Target.Index,b,staged,0,false)+")":"_fc.setAfter("+value+", "+Name(w.Target.Name,staged)+", () -> "+Expression(w.Target.Index,b,staged,0,false)+")";
                        emit("_fc.stage("+Literal(names[w.Target.Name].Original)+", "+ConvertValue(value,w.Target.Name)+(references&&w.Target.Index!=null?", true, true":"")+");");staged.Add(w.Target.Name.ToLowerInvariant());
                    }
                    emit("_fc.commit();");staged.Clear();foreach(var w in o.Writes)if(staged.Add(w.Target.Name.ToLowerInvariant()))emit(names[w.Target.Name].Name+" = _fc.variable("+Literal(names[w.Target.Name].Original)+");");return;
                }
                if(b.Kind==NodeKind.Process){emit("_fc.begin();");var staged=new HashSet<string>();var writes=o.Writes;var assignments=new List<string>();for(int i=0;i<writes.Count;i++){string part="_fc_assign"+(++temporary);assignments.Add(part);emit(part+"();");}emit("_fc.commit();");foreach(var w in writes)if(staged.Add(w.Target.Name.ToLowerInvariant()))emit(names[w.Target.Name].Name+" = _fc.variable("+Literal(names[w.Target.Name].Original)+");");emit("return null;");Line("}",2,b);staged.Clear();
                    for(int i=0;i<writes.Count;i++){var w=writes[i];Line("private void "+assignments[i]+"() {",2,b);string value=Expression(w.Value,b,staged,0,false);if(w.Target.Index!=null)value=references?"_fc.setAfterName("+value+", "+Literal(names[w.Target.Name].Original)+", () -> "+Expression(w.Target.Index,b,staged,0,false)+")":"_fc.setAfter("+value+", "+Name(w.Target.Name,staged)+", () -> "+Expression(w.Target.Index,b,staged,0,false)+")";Line("_fc.stage("+Literal(names[w.Target.Name].Original)+", "+ConvertValue(value,w.Target.Name)+(references&&w.Target.Index!=null?", true, true":"")+");",3,b);Line("}",2,b);staged.Add(w.Target.Name.ToLowerInvariant());}return;}
                if(b.Kind==NodeKind.Declare){var d=o.Declaration;emit("int _fc_size = Intrinsics.integer("+(d.IsArray?Expression(d.Size,b):"Value.number(0)")+", 0, 1024, \"La dimensione\");");emit("_fc.begin();");foreach(string n in d.Names)emit("_fc.stage("+Literal(names[n].Original)+", _fc.initial("+(int)d.Type+", "+Bool(d.IsArray)+", _fc_size), false);");emit("_fc.commit(true);");foreach(string n in d.Names){emit(names[n].Name+" = _fc.variable("+Literal(names[n].Original)+");");emit(Declaration(n)+" = new TypeSpec("+(int)d.Type+", "+Bool(d.IsArray)+", _fc_size);");}}
                else if(b.Kind==NodeKind.Input||b.Kind==NodeKind.ReadFile){var t=o.Input;string suffix=Declaration(t.Name)+", "+Parameter(t.Name)+", "+(t.Index==null?"null, null":Name(t.Name,new HashSet<string>())+", () -> "+Expression(t.Index,b));emit(names[t.Name].Name+" = _fc."+(b.Kind==NodeKind.Input?"read(":"readFile(")+Literal(names[t.Name].Original)+", "+(b.Kind==NodeKind.Input?Literal(b.Prompt??"")+", ":"")+suffix+");");if(b.Kind==NodeKind.Input)emit("_fc.step();");}
                else if(b.Kind!=NodeKind.Start&&b.Kind!=NodeKind.Pause){if(b.Kind==NodeKind.OpenFile)emit("_fc.requireClosed("+Bool(o.FileReading)+");");if(b.Kind==NodeKind.WriteFile)emit("_fc.requireWrite();");string e=Expression(o.Value,b);switch(b.Kind){case NodeKind.Output:emit("_fc.emit("+e+(o.OutputNewline?"":", false")+");");break;case NodeKind.Call:emit(e+";");break;case NodeKind.OpenFile:emit("_fc.openFile("+Bool(o.FileReading)+", "+e+");");break;case NodeKind.WriteFile:emit("_fc.writeFile("+e+", "+Bool(o.FileNewline)+");");break;case NodeKind.CloseFile:emit("_fc.closeFile("+Bool(o.FileReading)+");");break;case NodeKind.TurtleForward:case NodeKind.TurtleTurn:case NodeKind.TurtleHome:case NodeKind.TurtleClear:case NodeKind.TurtlePen:emit("_fc.turtle("+(int)b.Kind+", "+e+", "+Bool(b.Kind==NodeKind.TurtleForward?o.TurtleDraw:o.TurtleLeft)+");");break;default:throw new ArgumentException("Blocco Java non supportato.");}}
                if(!inline){emit("return null;");Line("}",indent,b);}else if(b.Kind==NodeKind.Declare)Line("}",indent,b);
            }
        }
    }
}
