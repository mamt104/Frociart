using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Frociart
{
    // Emits original Visual Basic .NET instructions/methods. No graph interpreter or converter is exported.
    internal static class FlowVisualBasic
    {
        internal static string TemplateNumber(double value){return Number(value);}
        internal static FlowSourceText Generate(FlowSource.Program program)
        {
            using(var stream=typeof(FlowVisualBasic).Assembly.GetManifestResourceStream("Frociart.Source.VisualBasicRuntime.txt"))
            {
                if(stream==null)throw new InvalidOperationException("Il supporto Visual Basic .NET non è disponibile in questa installazione.");
                using(var reader=new StreamReader(stream,Encoding.UTF8))return Generate(program,reader.ReadToEnd());
            }
        }
        internal static FlowSourceText Generate(FlowSource.Program program,string runtime,string className="Programma")
        {
            if(String.IsNullOrWhiteSpace(runtime))throw new ArgumentException("Il supporto Visual Basic .NET è vuoto.");
            if(className==null||!Regex.IsMatch(className,@"\A[A-Za-z_][A-Za-z_0-9]*\z")||Keywords.Contains(className)||className.StartsWith("_fc",StringComparison.OrdinalIgnoreCase)||ReservedClasses.Contains(className))throw new ArgumentException("Nome della classe Visual Basic .NET non valido.");
            return new Writer(program).Generate(runtime,className);
        }
        internal static string Literal(string text)
        {
            if(text==null)return "Nothing";
            var pieces=new List<string>();var part=new StringBuilder();
            Action flush=()=>{if(part.Length>0){pieces.Add("\""+part.ToString().Replace("\"","\"\"")+"\"");part.Clear();}};
            for(int i=0;i<text.Length;i++){char c=text[i];if(!Char.IsControl(c)&&c!=0x2028&&c!=0x2029&&c!=0x201C&&c!=0x201D&&c!=0xFF02&&!Char.IsSurrogate(c)){part.Append(c);continue;}if(Char.IsHighSurrogate(c)&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])){part.Append(c).Append(text[++i]);continue;}flush();pieces.Add("Global.Microsoft.VisualBasic.Strings.ChrW(&H"+((int)c).ToString("X4",CultureInfo.InvariantCulture)+")");}
            flush();if(pieces.Count==0)return "\"\"";if(pieces.Count==1&&pieces[0].StartsWith("\"",StringComparison.Ordinal))return pieces[0];
            string result="String.Concat(New String(){"+String.Join(", ",pieces.Select(v=>v.StartsWith("\"",StringComparison.Ordinal)?v:v+".ToString()"))+"})";return result.Length<=8192?result:EncodedLiteral(text);
        }
        static string EncodedLiteral(string text){byte[] bytes=new byte[text.Length*2];for(int i=0;i<text.Length;i++){bytes[i*2]=(byte)text[i];bytes[i*2+1]=(byte)(text[i]>>8);}string data=Convert.ToBase64String(bytes);return "(New Global.System.Func(Of Byte(), String)(Function(_fc_literalBytes) New String(Global.System.Linq.Enumerable.ToArray(Global.System.Linq.Enumerable.Select(Global.System.Linq.Enumerable.Range(0, _fc_literalBytes.Length \\ 2), Function(_fc_literalIndex) Global.System.Convert.ToChar(CInt(_fc_literalBytes(_fc_literalIndex * 2)) Or (CInt(_fc_literalBytes(_fc_literalIndex * 2 + 1)) << 8))))))).Invoke(Global.System.Convert.FromBase64String(\""+data+"\"))";
        }
        static readonly HashSet<string> Keywords=new HashSet<string>(("addhandler addressof aggregate alias and andalso as ascending async auto await binary boolean by byref byte byval call case catch cbool cbyte cchar cdate cdbl cdec char cint class clng cobj compare const continue csbyte cshort csng cstr ctype cuint culng cushort custom date decimal declare default delegate descending dim directcast distinct do double each else elseif end endif enum equals erase error event exit explicit false finally for friend from function get gettype getxmlnamespace global gosub goto group handles if implements imports in inherits integer interface into is isfalse isnot istrue iterator join key let lib like long loop me mid mod module mustinherit mustoverride mybase myclass namespace narrowing new next not notinheritable notoverridable nothing object of off on operator option optional or order orelse overloads overridable overrides paramarray partial preserve private property protected public raiseevent readonly redim rem removehandler resume return sbyte select set shadows shared short single skip static step stop strict string structure sub synclock take text then throw to true try trycast typeof uinteger ulong unicode until ushort using variant wend when where while widening with withevents writeonly xor yield").Split(' '),StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> ReservedClasses=new HashSet<string>("Value Values VectorValue ValueFormatter TypeSpec Intrinsics IntrinsicOperations FlowException FlowFileRequestKind FlowFileRequest FlowTextFiles FlowTurtle Variables ArrayCell ArrayArgument System Console BitConverter String Math Object Double Int32 Int64 Boolean Array List Dictionary HashSet Func Action File FileStream FileMode FileAccess FileShare Path Stream Encoding UTF8Encoding UnicodeEncoding StringBuilder CultureInfo NumberStyles BigInteger Regex Guid Exception IOException UnauthorizedAccessException ArgumentException Enumerable MidpointRounding ReadOnlyCollection Random".Split(' '),StringComparer.OrdinalIgnoreCase);
        static string Bool(bool value){return value?"True":"False";}
        static string Identifier(string name){var b=new StringBuilder();foreach(char c in name)if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.Append(c);else b.Append("_u").Append(((int)c).ToString("x4"));string value=b.ToString();if(value.Length==0||Char.IsDigit(value[0]))value="value_"+value;if(value.StartsWith("_fc",StringComparison.OrdinalIgnoreCase)||Keywords.Contains(value))value="value_"+value;return value;}
        static string Unique(string seed,HashSet<string> names){string value=seed;int n=2;while(!names.Add(value))value=seed+"_"+(n++).ToString(CultureInfo.InvariantCulture);return value;}
        static string Number(double n){if(n==0)return "Value.number(0R)";if(Math.Abs(n)<=9000000000d){long power=1;for(int digits=0;digits<=6;digits++,power*=10){double scaled=n*power;if(scaled!=Math.Floor(scaled))continue;string text=((long)Math.Abs(scaled)).ToString(CultureInfo.InvariantCulture).PadLeft(digits+1,'0');if(digits>0)text=text.Insert(text.Length-digits,".");if(n<0)text="-"+text;if(Double.Parse(text,CultureInfo.InvariantCulture)==n)return "Value.number("+text+"R)";}}return "Value.number(BitConverter.Int64BitsToDouble(&H"+BitConverter.DoubleToInt64Bits(n).ToString("X16")+"L))";}
        sealed class Symbol {internal readonly string Name,Original;internal Symbol(string n,string o){Name=n;Original=o;}}
        sealed class Helper {internal string Name;internal FlowSource.Expression Expression;internal FlowSource.Block Block;internal HashSet<string> Staged;internal bool Track;}
        sealed class Writer
        {
            readonly FlowSource.Program program;readonly FlowSourceText.Builder output;
            readonly Dictionary<string,string> functions=new Dictionary<string,string>(),classes=new Dictionary<string,string>();
            readonly HashSet<string> globals=new HashSet<string>(new[]{"Value","Values","VectorValue","ValueFormatter","TypeSpec","Intrinsics","IntrinsicOperations","FlowException","Double","System","Console","BitConverter","_fc","Programma","Main","run","ToString","GetHashCode","Equals","GetType","Finalize","MemberwiseClone","ReferenceEquals"},StringComparer.OrdinalIgnoreCase);
            Dictionary<string,Symbol> names;List<Symbol> symbols;Dictionary<string,string> blockMethods;List<Helper> helpers;FlowSource.Scope scope;int temporary;readonly bool references;
            internal Writer(FlowSource.Program p){program=p;references=p.Scopes.SelectMany(s=>s.Parameters).Any(v=>v.ByReference);output=new FlowSourceText.Builder(p,"vbnet");int i=0;foreach(var s in p.Scopes){classes[s.Id??""]="_fc_Scope"+(i++);if(s.Id!=null)functions[s.Id]=Unique("flow_"+Identifier(s.Name),globals);}}
            void AddLine(string text,int indent,FlowSource.Block block){output.AddLine(text,indent,block==null?new FlowSourceText.Origin[0]:new[]{new FlowSourceText.Origin(scope.Id,block.Id)});}
            void Line(string text,int indent,FlowSource.Block block=null){if(text.Length<=3000||text.TrimStart().StartsWith("'",StringComparison.Ordinal)){AddLine(text,indent,block);return;}int start=0,split=-1;bool quoted=false;for(int i=0;i<text.Length;i++){char c=text[i];if(c=='"'){if(quoted&&i+1<text.Length&&text[i+1]=='"'){i++;continue;}quoted=!quoted;}if(!quoted&&c==',')split=i+1;if(i-start>3000&&split>start){AddLine(text.Substring(start,split-start),indent+(start==0?0:1),block);start=split;split=-1;}}AddLine(text.Substring(start),indent+(start==0?0:1),block);}

            internal FlowSourceText Generate(string runtime,string className)
            {
                Line("Option Strict On",0);Line("Option Explicit On",0);Line("Option Infer On",0);
                Line("' Generato con Frociart — autore del software: mamt104",0);Line("' Progetto: "+Literal(program.Title),0);Line("' Visual Basic .NET. Salva come "+className+".vb; compila con vbc /r:System.Numerics.dll "+className+".vb.",0);
                foreach(string import in new[]{"Microsoft.VisualBasic","System","System.IO","System.Text","System.Linq","System.Collections.Generic","System.Collections.ObjectModel","System.Globalization","System.Numerics","System.Text.RegularExpressions"})Line("Imports "+import,0);
                Line("",0);Line("Public NotInheritable Class "+className,0);Line("Private Shared _fc As New _fc_Runtime()",1);Line("Public Shared Function Main(args As String()) As Integer",1);
                Line("Console.InputEncoding = New UTF8Encoding(False)",2);Line("Console.OutputEncoding = New UTF8Encoding(False)",2);Line("Return If(_fc.[run](Function() (New _fc_Scope0()).run(), True), 0, 1)",2);Line("End Function",1);
                foreach(var s in program.Scopes){if(s.Id!=null){var ps=new List<string>();var args=new List<string>();var used=new HashSet<string>(globals,StringComparer.OrdinalIgnoreCase);foreach(var p in s.Parameters){string n=Unique(Identifier(p.Name),used);ps.Add(n+" As Value");args.Add(n);}Line("Private Shared Function "+functions[s.Id]+"("+String.Join(", ",ps)+") As Value",1);Line("Return (New "+classes[s.Id]+"()).run("+String.Join(", ",args)+")",2);Line("End Function",1);}WriteScope(s);}
                Line("",1);Line("' --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---",1);
                foreach(string text in runtime.Replace("__FROCIART_PROGRAM__",className).Replace("\r\n","\n").Replace('\r','\n').Split('\n'))Line(text,1);Line("End Class",0);return output.Build();
            }
            void Collect(FlowSource.Expression e,Action<string> add){if(e==null)return;if(e.Kind==FlowSource.ExpressionKind.Variable)add(e.Text);foreach(var child in e.Children)Collect(child,add);}
            void WriteScope(FlowSource.Scope s)
            {
                scope=s;foreach(var annotation in s.Notes)Line("' Annotazione: "+Literal(annotation.Text),1,annotation);foreach(var note in s.Blocks)if(!String.IsNullOrEmpty(note.Comment))Line("' Nota ("+(s.Blocks.IndexOf(note)+1)+"): "+Literal(note.Comment),1,note);temporary=0;names=new Dictionary<string,Symbol>(StringComparer.OrdinalIgnoreCase);symbols=new List<Symbol>();helpers=new List<Helper>();blockMethods=new Dictionary<string,string>();var used=new HashSet<string>(globals,StringComparer.OrdinalIgnoreCase);
                Action<string> add=n=>{if(n!=null&&!names.ContainsKey(n)){var sym=new Symbol(Unique(Identifier(n),used),n);names.Add(n,sym);symbols.Add(sym);}};
                foreach(var p in s.Parameters)add(p.Name);int index=0;
                foreach(var b in s.Blocks){blockMethods[b.Id]="_fc_block"+(index++);var o=b.Operation;if(o.Input!=null){add(o.Input.Name);Collect(o.Input.Index,add);}if(o.Declaration!=null){foreach(string n in o.Declaration.Names)add(n);Collect(o.Declaration.Size,add);}foreach(var w in o.Writes){add(w.Target.Name);Collect(w.Value,add);Collect(w.Target.Index,add);}Collect(o.Value,add);}
                Line("",1);Line("Private NotInheritable Class "+classes[s.Id??""],1);foreach(var sym in symbols){Line("Private "+sym.Name+" As Value",2);Line("Private _fc_type_"+sym.Name+" As TypeSpec",2);}
                var args=new List<string>();var pnames=new List<string>();var pvalues=new List<string>();index=0;foreach(var p in s.Parameters){string a="_fc_arg"+(index++);args.Add(a+" As Value");pnames.Add(Literal(names[p.Name].Original));pvalues.Add(a);}
                Line("Friend Function run("+String.Join(", ",args)+") As Value",2);Line("_fc.[guardCall]()",3);index=0;foreach(var p in s.Parameters)Line(names[p.Name].Name+" = _fc."+(p.ByReference?"checkReference":"checkType")+"(_fc_arg"+(index++)+", "+(int)p.Type+(p.ByReference?"":", "+Bool(p.IsArray))+")",3);
                Line("_fc.[enter]("+Literal(s.Id==null?null:s.Name)+", New String(){"+String.Join(", ",pnames)+"}, New Value(){"+String.Join(", ",pvalues)+"}"+(references?", True":"")+")",3);Line("Try",3);Line("_fc.[checkMemory]()",4);
                var plan=FlowSourcePlan.Create(s);if(plan.Structured)Statements(plan.Statements,4);else Explicit(4);
                Line("Catch ex As Exception",3);Line("_fc.[captureError]()",4);Line("Throw",4);Line("Finally",3);Line("_fc.[leave]()",4);Line("End Try",3);Line("End Function",2);
                foreach(var b in s.Blocks)Instruction(b);
                for(int i=0;i<helpers.Count;i++){var h=helpers[i];Line("Private Function "+h.Name+"() As Value",2,h.Block);string e=Expression(h.Expression,h.Block,h.Staged,0,h.Track);Line("Return "+e,3,h.Block);Line("End Function",2,h.Block);}
                Line("End Class",1);
            }
            string Name(string name,HashSet<string> staged){var sym=names[name];return references?"_fc.[lookup]("+Literal(sym.Original)+")":"_fc.[value]("+(staged.Contains(name.ToLowerInvariant())?"_fc.[staged]("+Literal(sym.Original)+")":sym.Name)+", "+Literal(sym.Original)+")";}
            bool Calls(FlowSource.Expression e){return e!=null&&(e.FunctionId!=null||e.Children.Any(Calls));}
            string Expression(FlowSource.Expression e,FlowSource.Block block){return Expression(e,block,new HashSet<string>(),0,false);}
            string Expression(FlowSource.Expression e,FlowSource.Block block,HashSet<string> staged,int depth,bool track)
            {
                if(e==null)return "Nothing";if(depth==0)track|=Calls(e);if(depth>=24){string helper="_fc_expr"+(++temporary);helpers.Add(new Helper{Name=helper,Expression=e,Block=block,Staged=new HashSet<string>(staged),Track=track});return helper+"()";}
                bool tracked=track;int key=track?++temporary:0;
                Func<FlowSource.Expression,string> child=c=>{string value=Expression(c,block,staged,depth+1,tracked);return tracked?"_fc.[hold]("+key+", "+value+")":value;};
                Func<string,string> finish=value=>tracked&&e.Children.Count>0?"_fc.[release]("+key+", "+value+")":value;
                switch(e.Kind){
                    case FlowSource.ExpressionKind.Number:return Number(e.Number);
                    case FlowSource.ExpressionKind.Text:return "Value.text("+Literal(e.Text)+")";
                    case FlowSource.ExpressionKind.Boolean:return "Value.logical("+Bool(e.Boolean)+")";
                    case FlowSource.ExpressionKind.Variable:return Name(e.Text,staged);
                    case FlowSource.ExpressionKind.Vector:return finish("Value.vector(New Value(){"+String.Join(", ",e.Children.Select(child))+"})");
                    case FlowSource.ExpressionKind.Index:{string target=child(e.Children[0]);return finish(references&&e.Children[0].Kind==FlowSource.ExpressionKind.Variable?"_fc.[atName]("+Literal(names[e.Children[0].Text].Original)+", Function() "+child(e.Children[1])+")":"_fc.[at]("+target+", Function() "+child(e.Children[1])+")");}
                    case FlowSource.ExpressionKind.Unary:return finish("_fc.[unary]("+Literal(e.Text)+", "+child(e.Children[0])+")");
                    case FlowSource.ExpressionKind.Call:{var callee=e.FunctionId==null?null:program.Scopes.First(s=>s.Id==e.FunctionId);string args=String.Join(", ",e.Children.Select((c,i)=>callee!=null&&callee.Parameters[i].ByReference?"_fc.[hold]("+key+", _fc.[reference]("+Literal(names[c.Text].Original)+", "+Declaration(c.Text)+", "+Parameter(c.Text)+"))":child(c)));return e.FunctionId==null?finish("_fc.[intrinsic]("+Literal(e.Text)+(args.Length==0?"":", "+args)+")"):"_fc.[call]("+key+", Function() "+functions[e.FunctionId]+"("+args+"))";}
                    case FlowSource.ExpressionKind.Binary:{string a=child(e.Children[0]),b=child(e.Children[1]);if(e.Text=="and"||e.Text=="or")return finish("Value.logical("+a+".asBoolean() "+(e.Text=="and"?"AndAlso":"OrElse")+" "+b+".asBoolean())");return finish("_fc.[binary]("+Literal(e.Text)+", "+a+", "+b+")");}
                    default:throw new ArgumentException("Espressione Visual Basic .NET non supportata.");
                }
            }
            void Statements(IEnumerable<FlowSourcePlan.Statement> list,int indent){foreach(var s in list){if(s.Kind==FlowSourcePlan.Kind.Instruction)Line((s.Block.Kind==NodeKind.End?"Return ":"")+blockMethods[s.Block.Id]+"()",indent,s.Block);else if(s.Kind==FlowSourcePlan.Kind.Repeat){Line("Do",indent,s.EntryBlock);Statements(s.Body,indent+1);Line("Loop While "+(s.LoopOnTrue?"":"Not ")+blockMethods[s.Block.Id]+"().asBoolean()",indent,s.Block);}else if(s.Kind==FlowSourcePlan.Kind.While){Line("While "+(s.LoopOnTrue?"":"Not ")+blockMethods[s.Block.Id]+"().asBoolean()",indent,s.Block);Statements(s.Body,indent+1);Line("End While",indent,s.Block);}else{Line("If "+blockMethods[s.Block.Id]+"().asBoolean() Then",indent,s.Block);Statements(s.Body,indent+1);Line("Else",indent,s.Block);Statements(s.Otherwise,indent+1);Line("End If",indent,s.Block);}}}
            void Explicit(int indent){Line("' Collegamenti espliciti per conservare questo grafo.",indent);Line("Dim _fc_next As String = "+Literal(scope.StartId),indent);Line("Do",indent);Line("Select Case _fc_next",indent+1);foreach(var b in scope.Blocks){Line("Case "+Literal(b.Id),indent+2,b);if(b.Kind==NodeKind.Decision)Line("_fc_next = If("+blockMethods[b.Id]+"().asBoolean(), "+Literal(b.Next("true"))+", "+Literal(b.Next("false"))+")",indent+3,b);else if(b.Kind==NodeKind.End)Line("Return "+blockMethods[b.Id]+"()",indent+3,b);else{Line(blockMethods[b.Id]+"()",indent+3,b);Line("_fc_next = "+Literal(b.Next("next")),indent+3,b);}}Line("Case Else",indent+2);Line("Throw problem(\"Collegamento non valido nel sorgente modificato.\")",indent+3);Line("End Select",indent+1);Line("Loop",indent);}
            string Declaration(string n){return "_fc_type_"+names[n].Name;}
            string Parameter(string n){var p=scope.Parameters.FirstOrDefault(x=>x.Name.Equals(n,StringComparison.OrdinalIgnoreCase));return p==null?"Nothing":"New TypeSpec("+(int)p.Type+", "+Bool(p.IsArray)+", 0)";}
            string ConvertValue(string value,string n){return "_fc.[convert]("+value+", "+Declaration(n)+", "+Parameter(n)+")";}
            void Instruction(FlowSource.Block b)
            {
                var o=b.Operation;Line("Private Function "+blockMethods[b.Id]+"() As Value",2,b);Action<string> emit=text=>Line(text,3,b);
                if(b.Kind==NodeKind.Decision){emit("Return Value.logical(_fc.[test](Function() "+Expression(o.Value,b)+"))");Line("End Function",2,b);return;}
                emit(b.Kind==NodeKind.Input?"_fc.[guard]()":"_fc.[step]()");
                if(b.Kind==NodeKind.End){if(scope.Id==null)emit("_fc.[finishFiles]()");emit(scope.Id==null||scope.ReturnType==FlowDataType.Void?"Return Nothing":"Return _fc.[checkType]("+Expression(o.Value,b)+", "+(int)scope.ReturnType+", "+Bool(scope.ReturnIsArray)+")");Line("End Function",2,b);return;}
                if(b.Kind==NodeKind.Process){emit("_fc.[begin]()");var staged=new HashSet<string>();var writes=o.Writes;var assignments=new List<string>();for(int i=0;i<writes.Count;i++){string part="_fc_assign"+(++temporary);assignments.Add(part);emit(part+"()");}emit("_fc.[commit]()");foreach(var w in writes)if(staged.Add(w.Target.Name.ToLowerInvariant()))emit(names[w.Target.Name].Name+" = _fc.[variable]("+Literal(names[w.Target.Name].Original)+")");emit("Return Nothing");Line("End Function",2,b);staged.Clear();
                    for(int i=0;i<writes.Count;i++){var w=writes[i];Line("Private Sub "+assignments[i]+"()",2,b);string value=Expression(w.Value,b,staged,0,false);if(w.Target.Index!=null)value=references?"_fc.[setAfterName]("+value+", "+Literal(names[w.Target.Name].Original)+", Function() "+Expression(w.Target.Index,b,staged,0,false)+")":"_fc.[setAfter]("+value+", "+Name(w.Target.Name,staged)+", Function() "+Expression(w.Target.Index,b,staged,0,false)+")";Line("_fc.[stage]("+Literal(names[w.Target.Name].Original)+", "+ConvertValue(value,w.Target.Name)+(references&&w.Target.Index!=null?", True, True":"")+")",3,b);Line("End Sub",2,b);staged.Add(w.Target.Name.ToLowerInvariant());}return;}
                if(b.Kind==NodeKind.Declare){var d=o.Declaration;emit("Dim _fc_size As Integer = Intrinsics.[integer]("+(d.IsArray?Expression(d.Size,b):"Value.number(0R)")+", 0, 1024, \"La dimensione\")");emit("_fc.[begin]()");foreach(string n in d.Names)emit("_fc.[stage]("+Literal(names[n].Original)+", _fc.[initial]("+(int)d.Type+", "+Bool(d.IsArray)+", _fc_size), False)");emit("_fc.[commit](True)");foreach(string n in d.Names){emit(names[n].Name+" = _fc.[variable]("+Literal(names[n].Original)+")");emit(Declaration(n)+" = New TypeSpec("+(int)d.Type+", "+Bool(d.IsArray)+", _fc_size)");}}
                else if(b.Kind==NodeKind.Input||b.Kind==NodeKind.ReadFile){var t=o.Input;string suffix=Declaration(t.Name)+", "+Parameter(t.Name)+", "+(t.Index==null?"Nothing, Nothing":Name(t.Name,new HashSet<string>())+", Function() "+Expression(t.Index,b));emit(names[t.Name].Name+" = _fc."+(b.Kind==NodeKind.Input?"read(":"readFile(")+Literal(names[t.Name].Original)+", "+(b.Kind==NodeKind.Input?Literal(b.Prompt??"")+", ":"")+suffix+")");if(b.Kind==NodeKind.Input)emit("_fc.[step]()");}
                else if(b.Kind!=NodeKind.Start&&b.Kind!=NodeKind.Pause){if(b.Kind==NodeKind.OpenFile)emit("_fc.[requireClosed]("+Bool(o.FileReading)+")");if(b.Kind==NodeKind.WriteFile)emit("_fc.[requireWrite]()");string e=Expression(o.Value,b);switch(b.Kind){case NodeKind.Output:emit("_fc.[emit]("+e+(o.OutputNewline?"":", False")+")");break;case NodeKind.Call:emit("Call "+e);break;case NodeKind.OpenFile:emit("_fc.[openFile]("+Bool(o.FileReading)+", "+e+")");break;case NodeKind.WriteFile:emit("_fc.[writeFile]("+e+", "+Bool(o.FileNewline)+")");break;case NodeKind.CloseFile:emit("_fc.[closeFile]("+Bool(o.FileReading)+")");break;case NodeKind.TurtleForward:case NodeKind.TurtleTurn:case NodeKind.TurtleHome:case NodeKind.TurtleClear:case NodeKind.TurtlePen:emit("_fc.[turtle]("+(int)b.Kind+", "+e+", "+Bool(b.Kind==NodeKind.TurtleForward?o.TurtleDraw:o.TurtleLeft)+")");break;default:throw new ArgumentException("Blocco Visual Basic .NET non supportato.");}}
                emit("Return Nothing");Line("End Function",2,b);
            }

        }
    }
}
