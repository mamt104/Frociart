using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using System.Globalization;
namespace Frociart {
 internal static class FlowPhp {
  internal static FlowSourceText Generate(FlowSource.Program program){using(var stream=typeof(FlowPhp).Assembly.GetManifestResourceStream("Frociart.Source.PhpRuntime.php")){if(stream==null)throw new InvalidOperationException("Il supporto PHP non è disponibile.");using(var reader=new StreamReader(stream,Encoding.UTF8))return Generate(program,reader.ReadToEnd());}}
  internal static FlowSourceText Generate(FlowSource.Program program,string runtime){if(program==null||!program.CanGenerate)throw new ArgumentException("Correggi il diagramma prima di generare il codice.");if(String.IsNullOrWhiteSpace(runtime))throw new ArgumentException("Il supporto PHP è vuoto.");return new Writer(program).Generate(runtime);}
  internal static string TemplateNumber(double n){return Number(n);}
  internal static string Literal(string text){
   if(text==null)return "null";var b=new StringBuilder("\"");
   for(int i=0;i<text.Length;i++){char c=text[i];if(c=='"'||c=='\\'||c=='$')b.Append('\\').Append(c);else if(c>=32&&c<=126)b.Append(c);else if(Char.IsHighSurrogate(c)&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1]))b.Append("\\u{").Append(Char.ConvertToUtf32(c,text[++i]).ToString("x")).Append('}');else if(Char.IsSurrogate(c)){int code=c;b.Append("\\x").Append((0xe0|(code>>12)).ToString("x2")).Append("\\x").Append((0x80|((code>>6)&63)).ToString("x2")).Append("\\x").Append((0x80|(code&63)).ToString("x2"));}else b.Append("\\u{").Append(((int)c).ToString("x")).Append('}');}
   return b.Append('"').ToString();
  }
  private static string Text(string text){for(int i=0;i<text.Length;i++)if(Char.IsSurrogate(text[i])){if(Char.IsHighSurrogate(text[i])&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])){i++;continue;}return "FcText::units(["+String.Join(", ",text.Select(c=>((int)c).ToString(CultureInfo.InvariantCulture)))+"])";}return "FcText::fromUtf8("+Literal(text)+")";}
  private static string Comment(string text){return Literal(text).Replace("?>","?\\u{3e}");}
  private static string Number(double n){if(n==0)return "0.0";string simple=FlowPython.TemplateNumber(n);if(simple.StartsWith("_fc.real(",StringComparison.Ordinal))return "$fc->real(\""+BitConverter.DoubleToInt64Bits(n).ToString("x16")+"\")";return simple.IndexOf('.')<0&&simple.IndexOf('e')<0&&simple.IndexOf('E')<0?simple+".0":simple;}
  private static string Bool(bool value){return value?"true":"false";}
  private static string Identifier(string source){var b=new StringBuilder();foreach(char c in source)if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.Append(c);else b.Append("_u").Append(((int)c).ToString("x4"));string name=b.ToString();if(name.Length==0||Char.IsDigit(name[0]))name="value_"+name;return name.StartsWith("_fc",StringComparison.Ordinal)?"value_"+name:name;}
  private sealed class Symbol {internal readonly string Name,Original;internal Symbol(string n,string original){Name=n;Original=original;}}
  private sealed class Writer {
   readonly FlowSource.Program program;readonly FlowSourceText.Builder output;readonly Dictionary<string,string> functions=new Dictionary<string,string>(StringComparer.Ordinal);readonly HashSet<string> globals=new HashSet<string>(("main fc this GLOBALS _SERVER _GET _POST _FILES _COOKIE _SESSION _REQUEST _ENV __construct __destruct").Split(' '),StringComparer.OrdinalIgnoreCase);
   Dictionary<string,Symbol> names;List<Symbol> symbols;FlowSource.Scope scope;readonly bool references;int temporary;
   internal Writer(FlowSource.Program p){program=p;references=p.Scopes.SelectMany(s=>s.Parameters).Any(p1=>p1.ByReference);output=new FlowSourceText.Builder(p,"php");foreach(var s in p.Scopes.Where(s=>s.Id!=null))functions.Add(s.Id,Unique("flow_"+Identifier(s.Name),globals));}
   static string Unique(string seed,HashSet<string> used){string n=seed;int suffix=2;while(!used.Add(n))n=seed+"_"+(suffix++).ToString(CultureInfo.InvariantCulture);return n;}
   void Line(string code,int indent=0,FlowSource.Block block=null){output.AddLine(code,indent,block==null?new FlowSourceText.Origin[0]:new[]{new FlowSourceText.Origin(scope.Id,block.Id)});}
   internal FlowSourceText Generate(string runtime){
    Line("<?php");Line("// Generato con Frociart — autore del software: mamt104");Line("// Progetto: "+Comment(program.Title));Line("// PHP 8.2+, CLI a 64 bit. Salva Programma.php ed esegui php Programma.php.");Line("// Il programma non richiede Frociart o estensioni aggiuntive.");Line("");
    Line("final class Programma {");Line("public function __construct(private FcRuntime $fc) {}",1);foreach(var s in program.Scopes)WriteScope(s);Line("}");Line("");
    Line("// --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---");string body=runtime.Replace("\r\n","\n").Replace('\r','\n');if(body.StartsWith("<?php\n",StringComparison.Ordinal))body=body.Substring(6);else throw new ArgumentException("Il supporto PHP non contiene l'intestazione prevista.");foreach(string line in body.Split('\n'))Line(line);
    Line("");Line("if (isset($_SERVER['SCRIPT_FILENAME']) && realpath($_SERVER['SCRIPT_FILENAME']) === __FILE__) {");Line("$runtime = new FcRuntime();",1);Line("$program = new Programma($runtime);",1);Line("exit($runtime->run([$program, 'main']) ? 0 : 1);",1);Line("}");return output.Build();
   }
   void WriteScope(FlowSource.Scope s){
    scope=s;foreach(var n in s.Notes)Line("// Annotazione: "+Comment(n.Text),1,n);foreach(var n in s.Blocks)if(!String.IsNullOrEmpty(n.Comment))Line("// Nota ("+(s.Blocks.IndexOf(n)+1)+"): "+Comment(n.Comment),1,n);
    temporary=0;names=new Dictionary<string,Symbol>(StringComparer.OrdinalIgnoreCase);symbols=new List<Symbol>();var used=new HashSet<string>(globals,StringComparer.OrdinalIgnoreCase);
    Action<string> add=n=>{if(n!=null&&!names.ContainsKey(n)){var symbol=new Symbol(Unique(Identifier(n),used),n);names.Add(n,symbol);symbols.Add(symbol);}};
    Action<FlowSource.Expression> collect=null;collect=e=>{if(e==null)return;if(e.Kind==FlowSource.ExpressionKind.Variable)add(e.Text);foreach(var c in e.Children)collect(c);};foreach(var p in s.Parameters)add(p.Name);
    foreach(var n in s.Blocks){var o=n.Operation;if(o.Input!=null){add(o.Input.Name);collect(o.Input.Index);}if(o.Declaration!=null){foreach(string name in o.Declaration.Names)add(name);collect(o.Declaration.Size);}foreach(var write in o.Writes){add(write.Target.Name);collect(write.Target.Index);collect(write.Value);}collect(o.Value);}
    Line("public function "+(s.Id==null?"main":functions[s.Id])+"("+String.Join(", ",s.Parameters.Select(p=>"$"+names[p.Name].Name))+") {",1);Line("$fc = $this->fc;",2);Line("$fc->guardCall();",2);
    foreach(var p in s.Parameters)Line(p.ByReference?"$fc->checkReference($"+names[p.Name].Name+", "+(int)p.Type+");":"$fc->checkType($"+names[p.Name].Name+", "+(int)p.Type+", "+Bool(p.IsArray)+");",2);
    Line("$fc->enter("+Literal(s.Id==null?null:s.Name)+", ["+String.Join(", ",s.Parameters.Select(p=>Literal(names[p.Name].Original)+" => $"+names[p.Name].Name))+"]"+(references?", true":"")+");",2);Line("$_fc_types = [];",2);Line("$_fc_error = null;",2);
    foreach(var symbol in symbols)if(!s.Parameters.Any(p=>p.Name.Equals(symbol.Original,StringComparison.OrdinalIgnoreCase)))Line("$"+symbol.Name+" = $fc->unset;",2);
    Line("try {",2);Line("$fc->checkMemory();",3);var plan=FlowSourcePlan.Create(s);if(plan.Structured)WriteStatements(plan.Statements,3);else WriteExplicit(3);
    Line("} catch (\\Throwable $_fc_caught) {",2);Line("$_fc_error = $_fc_caught;",3);Line("throw $_fc_caught;",3);Line("} finally {",2);Line("$fc->leave($_fc_error);",3);Line("}",2);Line("}",1);
    foreach(var n in s.Blocks.Where(n=>!n.Reachable)){Line("");Line("// Blocco non raggiungibile, escluso dall'esecuzione: "+Comment(n.Text),1,n);}Line("");
   }
   string Name(string original,Dictionary<string,string> staged=null){if(references)return "$fc->lookup("+Literal(names[original].Original)+")";string result;string variable=staged!=null&&staged.TryGetValue(original,out result)?result:"$"+names[original].Name;return "$fc->value("+variable+", "+Literal(names[original].Original)+")";}
   static bool CallsFunction(FlowSource.Expression expression){return expression!=null&&(expression.FunctionId!=null||expression.Children.Any(CallsFunction));}
   string Expression(FlowSource.Expression e,int indent,FlowSource.Block block,Dictionary<string,string> staged=null,int depth=0,bool track=false){
    if(e==null)return "null";if(depth==0)track=track||CallsFunction(e);
    if(depth>=24){string helper="$_fc_expr_"+(++temporary);var captures=new List<string>{"$fc","&$_fc_types"};captures.AddRange(symbols.Select(s=>"&$"+s.Name));if(staged!=null)captures.AddRange(staged.Values.Distinct().Select(v=>"&"+v));Line(helper+" = function() use ("+String.Join(", ",captures)+") {",indent,block);string value=Expression(e,indent+1,block,staged,0,track);Line("return "+value+";",indent+1,block);Line("};",indent,block);return helper+"()";}
    int key=track?++temporary:0;Func<FlowSource.Expression,string> child=c=>{string value=Expression(c,indent,block,staged,depth+1,track);return track?"$fc->hold("+key+", "+value+")":value;};Func<string,string> finish=value=>track&&e.Children.Count>0?"$fc->release("+key+", "+value+")":value;
    switch(e.Kind){
     case FlowSource.ExpressionKind.Number:return Number(e.Number);
     case FlowSource.ExpressionKind.Text:return "$fc->text("+Text(e.Text)+")";
     case FlowSource.ExpressionKind.Boolean:return Bool(e.Boolean);
     case FlowSource.ExpressionKind.Variable:return Name(e.Text,staged);
     case FlowSource.ExpressionKind.Vector:return finish("$fc->vector(["+String.Join(", ",e.Children.Select(child))+"])");
     case FlowSource.ExpressionKind.Index:string array=child(e.Children[0]);return finish(references&&e.Children[0].Kind==FlowSource.ExpressionKind.Variable?"$fc->atName("+Literal(names[e.Children[0].Text].Original)+", fn() => "+child(e.Children[1])+")":"$fc->at("+array+", fn() => "+child(e.Children[1])+")");
     case FlowSource.ExpressionKind.Unary:string operand=child(e.Children[0]);return finish(e.Text=="not"?"(!$fc->truth("+operand+"))":e.Text=="-"?"(-$fc->number("+operand+"))":"$fc->number("+operand+")");
     case FlowSource.ExpressionKind.Call:
      var callee=e.FunctionId==null?null:program.Scopes.First(s=>s.Id==e.FunctionId);string args=String.Join(", ",e.Children.Select((c,i)=>callee!=null&&callee.Parameters[i].ByReference?"$fc->hold("+key+", $fc->reference("+Literal(names[c.Text].Original)+", "+Declaration(c.Text)+", "+Parameter(c.Text)+"))":child(c)));
      return e.FunctionId==null?finish("$fc->intrinsic("+Literal(e.Text)+(args.Length==0?"":", "+args)+")"):"$fc->call("+key+", [$this, "+Literal(functions[e.FunctionId])+"]"+(args.Length==0?"":", "+args)+")";
     case FlowSource.ExpressionKind.Binary:
      string a=child(e.Children[0]),b=child(e.Children[1]);if(e.Text=="and"||e.Text=="or")return finish("($fc->truth("+a+") "+(e.Text=="and"?"&&":"||")+" $fc->truth("+b+"))");
      if(e.Text=="="||e.Text=="==")return finish("$fc->equal("+a+", "+b+")");if(e.Text=="!="||e.Text=="<>")return finish("(!$fc->equal("+a+", "+b+"))");if(e.Text=="<"||e.Text=="<="||e.Text==">"||e.Text==">=")return finish("$fc->compare("+a+", "+b+", "+Literal(e.Text)+")");
      string method=e.Text=="&"?"concat":e.Text=="+"?"add":e.Text=="-"?"sub":e.Text=="*"?"mul":e.Text=="/"?"div":e.Text=="%"?"mod":"power";return finish("$fc->"+method+"("+a+", "+b+")");
     default:throw new InvalidOperationException("Espressione PHP non supportata.");
    }
   }
   string Test(FlowSource.Block n,int indent){return "$fc->test(fn() => "+Expression(n.Operation.Value,indent,n)+")";}
   void WriteStatements(IEnumerable<FlowSourcePlan.Statement> statements,int indent){foreach(var s in statements){
    if(s.Kind==FlowSourcePlan.Kind.Instruction){WriteInstruction(s.Block,indent);continue;}
    if(s.Kind==FlowSourcePlan.Kind.Repeat){Line("while (true) {",indent,s.EntryBlock);WriteBody(s.Body,indent+1,s.Block);Line("if ("+(s.LoopOnTrue?"!":"")+Test(s.Block,indent+1)+") break;",indent+1,s.Block);Line("}",indent,s.Block);continue;}
    string test=Test(s.Block,indent);if(s.Kind==FlowSourcePlan.Kind.While){Line("while ("+(s.LoopOnTrue?"":"!")+test+") {",indent,s.Block);WriteBody(s.Body,indent+1,s.Block);Line("}",indent,s.Block);}else{Line("if ("+test+") {",indent,s.Block);WriteBody(s.Body,indent+1,s.Block);Line("} else {",indent,s.Block);WriteBody(s.Otherwise,indent+1,s.Block);Line("}",indent,s.Block);}
   }}
   void WriteBody(ICollection<FlowSourcePlan.Statement> body,int indent,FlowSource.Block owner){if(body.Count==0)Line("// Nessuna operazione.",indent,owner);else WriteStatements(body,indent);}
   void WriteExplicit(int indent){Line("// Collegamenti espliciti per conservare questo grafo.",indent);Line("$_fc_block = "+Literal(scope.StartId)+";",indent);Line("while (true) {",indent);Line("switch (true) {",indent+1);
    foreach(var n in scope.Blocks){Line("case $_fc_block === "+Literal(n.Id)+":",indent+2,n);if(n.Kind==NodeKind.Decision){Line("if ("+Test(n,indent+3)+") {",indent+3,n);Line("$_fc_block = "+Literal(n.Next("true"))+";",indent+4,n);Line("} else {",indent+3,n);Line("$_fc_block = "+Literal(n.Next("false"))+";",indent+4,n);Line("}",indent+3,n);}else{WriteInstruction(n,indent+3);if(n.Kind!=NodeKind.End)Line("$_fc_block = "+Literal(n.Next("next"))+";",indent+3,n);}Line("break;",indent+3,n);}
    Line("default:",indent+2);Line("throw new FcError('Collegamento non valido nel sorgente modificato.');",indent+3);Line("}",indent+1);Line("}",indent);
   }
   string Declaration(string name){return "($_fc_types["+Literal(names[name].Name)+"] ?? null)";}
   string Parameter(string name){var parameter=scope.Parameters.FirstOrDefault(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));return parameter==null?"null":"["+(int)parameter.Type+", "+Bool(parameter.IsArray)+"]";}
   string Convert(string value,string name){return "$fc->convert("+value+", "+Declaration(name)+", "+Parameter(name)+")";}
   void WriteInstruction(FlowSource.Block n,int indent){var o=n.Operation;Action<string> emit=line=>Line(line,indent,n);emit(n.Kind==NodeKind.Input?"$fc->guard();":"$fc->step();");if(n.Kind==NodeKind.Start||n.Kind==NodeKind.Pause)return;
    if(n.Kind==NodeKind.End){if(scope.Id==null){emit("$fc->finishFiles();");emit("return;");}else if(scope.ReturnType==FlowDataType.Void)emit("return;");else emit("return $fc->checkType("+Expression(o.Value,indent,n)+", "+(int)scope.ReturnType+", "+Bool(scope.ReturnIsArray)+");");return;}
    if(n.Kind==NodeKind.Process){emit("$fc->beginValues();");var staged=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var order=new List<string>();var temps=new List<string>();foreach(var write in o.Writes){string value=Expression(write.Value,indent,n,staged);if(write.Target.Index!=null){string index=Expression(write.Target.Index,indent,n,staged);value=references?"$fc->setAfterName("+value+", "+Literal(names[write.Target.Name].Original)+", fn() => "+index+")":"$fc->setAfter("+value+", "+Name(write.Target.Name,staged)+", fn() => "+index+")";}
     value="$fc->stage("+Literal(names[write.Target.Name].Original)+", "+Convert(value,write.Target.Name)+(references&&write.Target.Index!=null?", true, true":"")+")";if(o.Writes.Count==1){emit("$"+names[write.Target.Name].Name+" = "+value+";");continue;}string temp="$_fc_tmp_"+(++temporary);temps.Add(temp);emit(temp+" = "+value+";");if(!staged.ContainsKey(write.Target.Name))order.Add(write.Target.Name);staged[write.Target.Name]=temp;}
     if(o.Writes.Count>1){foreach(string name in order)emit("$"+names[name].Name+" = "+staged[name]+";");foreach(string temp in temps)emit(temp+" = null;");}emit("$fc->commit();");return;
    }
    if(n.Kind==NodeKind.Declare){var d=o.Declaration;string size=d.IsArray?Expression(d.Size,indent,n):"0.0",temp="$_fc_size_"+(++temporary);emit(temp+" = $fc->integer("+size+", 0, 1024, 'La dimensione');");emit("$fc->beginValues();");var values=new List<string>();foreach(string name in d.Names){string value="$_fc_tmp_"+(++temporary);values.Add(value);emit(value+" = $fc->stage("+Literal(names[name].Original)+", $fc->initial("+(int)d.Type+", "+Bool(d.IsArray)+", "+temp+"), false);");}emit("$fc->commit(true);");for(int i=0;i<d.Names.Count;i++){string name=d.Names[i];emit("$"+names[name].Name+" = "+values[i]+";");emit("$_fc_types["+Literal(names[name].Name)+"] = ["+(int)d.Type+", "+Bool(d.IsArray)+", "+temp+"];");}foreach(string value in values)emit(value+" = null;");return;}
    if(n.Kind==NodeKind.Input||n.Kind==NodeKind.ReadFile){var target=o.Input;string suffix=Declaration(target.Name)+", "+Parameter(target.Name);if(target.Index!=null)suffix+=", "+Name(target.Name)+", fn() => "+Expression(target.Index,indent,n);string read=n.Kind==NodeKind.Input?"$fc->read("+Literal(names[target.Name].Original)+", "+Literal(n.Prompt??"")+", "+suffix+")":"$fc->readFile("+Literal(names[target.Name].Original)+", "+suffix+")";emit("$"+names[target.Name].Name+" = "+read+";");if(n.Kind==NodeKind.Input)emit("$fc->step();");return;}
    if(n.Kind==NodeKind.OpenFile)emit("$fc->requireClosed("+Bool(o.FileReading)+");");if(n.Kind==NodeKind.WriteFile)emit("$fc->requireWrite();");string expression=Expression(o.Value,indent,n);
    switch(n.Kind){case NodeKind.Output:emit("$fc->emit("+expression+(o.OutputNewline?"":", false")+");");break;case NodeKind.Call:emit(expression+";");break;case NodeKind.OpenFile:emit("$fc->openFile("+Bool(o.FileReading)+", "+expression+");");break;case NodeKind.WriteFile:emit("$fc->writeFile("+expression+", "+Bool(o.FileNewline)+");");break;case NodeKind.CloseFile:emit("$fc->closeFile("+Bool(o.FileReading)+");");break;case NodeKind.TurtleForward:emit("$fc->turtleForward("+expression+", "+Bool(o.TurtleDraw)+");");break;case NodeKind.TurtleTurn:emit("$fc->turtleTurn("+expression+", "+Bool(o.TurtleLeft)+");");break;case NodeKind.TurtleHome:emit("$fc->turtleHome();");break;case NodeKind.TurtleClear:emit("$fc->turtleClear();");break;case NodeKind.TurtlePen:emit("$fc->turtlePen("+expression+");");break;default:throw new InvalidOperationException("Blocco PHP non supportato.");}
   }
  }
 }
}
