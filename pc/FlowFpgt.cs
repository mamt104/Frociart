using System;using System.Text;using System.Linq;using System.Globalization;using System.Collections.Generic;
using T=Frociart.FlowFpgtTypes.DataType;using S=Frociart.FlowFpgtTypes.Scope;using E=Frociart.FlowSource.Expression;using B=Frociart.FlowSource.Block;
namespace Frociart {
 internal static partial class FlowFpgt {
  internal sealed class GenerationError:ArgumentException {
   internal readonly string FunctionId,NodeId;
   internal GenerationError(S scope,B block,string message):base((scope==null?"":scope.Source.Name+" · ")+(block==null?"":UiName(block)+": ")+message){FunctionId=scope==null?null:scope.Source.Id;NodeId=block==null?null:block.Id;}
   static string UiName(B b){string text=(b.Text??"").Replace('\r',' ').Replace('\n',' ');return text.Length==0?b.Kind.ToString():"«"+(text.Length>80?text.Substring(0,80)+"…":text)+"»";}
  }
  internal static FlowSourceText Generate(FlowFpgtSyntax model,FlowSource.Program program,IDictionary<string,FlowDataType> choices=null){return new Engine(model,program,choices).Run();}
  // Recognized target syntax only. Original rows, conditions and line numbers
  // remain immutable; adaptations apply to the generated copy and are disclosed.
  sealed class Compatibility {
   readonly FlowFpgtSyntax model;readonly bool python,java,csharp;readonly HashSet<string> tokens=new HashSet<string>(StringComparer.Ordinal),notices=new HashSet<string>(StringComparer.Ordinal);
   readonly HashSet<string> reserved=new HashSet<string>(StringComparer.Ordinal);string mathAlias,builtinsAlias,vectorName,vectorBuiltins;bool mathUsed,builtinsUsed,vectorUsed,resultUsed;string resultName,resultBuiltins,penName,penBuiltins,storageName,storageBuiltins;bool penUsed,storageUsed;
   static readonly Dictionary<string,string> MathFunctions=new Dictionary<string,string>(StringComparer.Ordinal){{"arccos","acos"},{"arcsin","asin"},{"arctan","atan"},{"cos","cos"},{"sin","sin"},{"tan","tan"},{"log","log"},{"log10","log10"},{"sqrt","sqrt"}};
   internal IEnumerable<string> Reserved {get{return reserved;}}internal IEnumerable<string> Notices {get{return notices;}}
   internal string ArrayResultLanguage {get{var section=model.Find("array return");string language=section==null?"":FlowFpgtSyntax.Normalize(section.Value("language",fallback:""));return language.Length==0?ArrayCopyLanguage:language=="python"||language=="java"||language=="csharp"?language:null;}}
   internal bool HasArrayResults {get{return resultName!=null;}}
   internal string ResultArray(string value,int kind){resultUsed=true;return ArrayResultLanguage=="python"?resultName+"("+value+", "+kind+")":(ArrayResultLanguage=="csharp"?"global::":"")+resultName+"."+FlowFpgtReturn.Method(ArrayResultLanguage,kind)+"("+value+")";}
   internal bool StockPen {get{var pen=model.Find("pen");if(!python||pen==null)return false;string profile=FlowFpgtSyntax.Normalize(pen.Value("language",fallback:""));if(profile.Length>0&&profile!="python")return false;var rows=pen.Rows("text");string[] colors={"black","gray","red","orange","yellow","teal","green","blue","violet","brown"};return rows.Count==10&&colors.All(color=>rows.Count(r=>r.Indent==0&&r.Conditions.Count==1&&r.Conditions[0]==color&&r.Text.Trim()=="draw.pencolor(\""+color+"\")")==1);}}
   internal string PenLanguage {get{var pen=model.Find("pen");string language=pen==null?"":FlowFpgtSyntax.Normalize(pen.Value("language",fallback:""));return language.Length==0?(StockPen?"python":ArrayCopyLanguage):language=="python"||language=="java"||language=="csharp"?language:null;}}
   internal string PenColor(string value){notices.Add(penName==null?"Colori: espressione valutata una sola volta; regole definite dal modello.":"Colori: nomi italiani e inglesi, codici esadecimali e valori calcolati; valutazione singola.");if(penName==null)return value;penUsed=true;return PenLanguage=="python"?penName+"("+value+")":(PenLanguage=="csharp"?"global::":"")+penName+(PenLanguage=="csharp"?".Normalize(":".normalize(")+value+")";}
   internal IEnumerable<string> PenTail(){if(penUsed&&PenLanguage!="python")foreach(string line in FlowFpgtPen.Code(PenLanguage,penName,penBuiltins))yield return line;}
   internal string StorageLanguage {get{var section=model.Find("array storage");string language=section==null?"":FlowFpgtSyntax.Normalize(section.Value("language",fallback:""));return language.Length==0?ArrayCopyLanguage:language=="python"||language=="java"||language=="csharp"?language:null;}}
   internal string ArrayLength(string value){if(storageName==null)return value;storageUsed=true;return StorageLanguage=="python"?storageName+"("+value+")":(StorageLanguage=="csharp"?"global::":"")+storageName+(StorageLanguage=="csharp"?".Length(":".length(")+value+")";}
   internal void NoticeGraphArrays(){notices.Add(StorageLanguage==null?"Grafi liberi: spazio e allocazione dei vettori definiti dal modello.":"Grafi liberi: spazio dei vettori separato dall’allocazione; dimensioni di Dichiara valutate una volta e controllate da 0 a 1024.");}
   internal IEnumerable<string> StorageTail(){if(storageUsed&&StorageLanguage!="python")foreach(string line in FlowFpgtStorage.Code(StorageLanguage,storageName,storageBuiltins))yield return line;}
   internal string ArrayCopyLanguage {get{return python?"python":java?"java":csharp?"csharp":null;}}
   static IEnumerable<string> VectorCode(string language){
    if(language=="csharp")return new[]{"internal static class __NAME__ {","    static string Element(object value) {","        if(value is string) return \"\\\"\" + ((string)value).Replace(\"\\\\\", \"\\\\\\\\\").Replace(\"\\\"\", \"\\\\\\\"\").Replace(\"\\n\", \"\\\\n\").Replace(\"\\r\", \"\\\\r\").Replace(\"\\t\", \"\\\\t\") + \"\\\"\";","        if(value is bool) return (bool)value ? \"vero\" : \"falso\";","        double number = global::System.Convert.ToDouble(value, global::System.Globalization.CultureInfo.InvariantCulture);","        return number == 0 ? \"0\" : number.ToString(\"G15\", global::System.Globalization.CultureInfo.InvariantCulture);","    }","    internal static string Format(global::System.Array values) {","        var text = new global::System.Text.StringBuilder(\"[\");","        for(int i=0;i<values.Length;i++) { if(i>0) text.Append(\", \"); text.Append(Element(values.GetValue(i))); }","        return text.Append(\"]\").ToString();","    }","    internal static bool Equal(object left, object right) {","        var a=left as global::System.Array; var b=right as global::System.Array;","        if(a==null || b==null || a.Length!=b.Length) return false;","        for(int i=0;i<a.Length;i++) {","            object x=a.GetValue(i),y=b.GetValue(i);","            if(x is bool || y is bool) { if(!(x is bool && y is bool) || (bool)x!=(bool)y) return false; }","            else if(x is string || y is string) { if(!(x is string && y is string) || !global::System.String.Equals((string)x,(string)y,global::System.StringComparison.Ordinal)) return false; }","            else if(global::System.Convert.ToDouble(x,global::System.Globalization.CultureInfo.InvariantCulture)!=global::System.Convert.ToDouble(y,global::System.Globalization.CultureInfo.InvariantCulture)) return false;","        }","        return true;","    }","}"};
    if(language=="java")return new[]{"final class __NAME__ {","    static String element(Object value) {","        if(value instanceof java.lang.String) return \"\\\"\" + ((java.lang.String)value).replace(\"\\\\\", \"\\\\\\\\\").replace(\"\\\"\", \"\\\\\\\"\").replace(\"\\n\", \"\\\\n\").replace(\"\\r\", \"\\\\r\").replace(\"\\t\", \"\\\\t\") + \"\\\"\";","        if(value instanceof java.lang.Boolean) return (java.lang.Boolean)value ? \"vero\" : \"falso\";","        double number=((java.lang.Number)value).doubleValue();","        if(number==0) return \"0\";","        java.math.BigDecimal n=(java.lang.Math.abs(number)<java.lang.Double.MIN_NORMAL?new java.math.BigDecimal(number):java.math.BigDecimal.valueOf(number)).round(new java.math.MathContext(15,java.math.RoundingMode.HALF_EVEN)).stripTrailingZeros();","        int exponent=n.precision()-n.scale()-1;","        if(exponent>=15 || exponent<-4) {","            String digits=n.unscaledValue().abs().toString();","            String power=java.lang.Integer.toString(java.lang.Math.abs(exponent)); if(power.length()<2)power=\"0\"+power;","            return (number<0?\"-\":\"\")+digits.charAt(0)+(digits.length()>1?\".\"+digits.substring(1):\"\")+\"E\"+(exponent<0?\"-\":\"+\")+power;","        }","        return n.toPlainString();","    }","    static String format(Object values) {","        int count=java.lang.reflect.Array.getLength(values); java.lang.StringBuilder text=new java.lang.StringBuilder(\"[\");","        for(int i=0;i<count;i++) { if(i>0)text.append(\", \");text.append(element(java.lang.reflect.Array.get(values,i))); }","        return text.append(\"]\").toString();","    }","    static boolean equal(Object left,Object right) {","        if(left==null || right==null || !left.getClass().isArray() || !right.getClass().isArray())return false;","        int count=java.lang.reflect.Array.getLength(left);if(count!=java.lang.reflect.Array.getLength(right))return false;","        for(int i=0;i<count;i++) {","            Object x=java.lang.reflect.Array.get(left,i),y=java.lang.reflect.Array.get(right,i);","            if(x instanceof java.lang.Boolean || y instanceof java.lang.Boolean) { if(!(x instanceof java.lang.Boolean && y instanceof java.lang.Boolean) || !x.equals(y))return false; }","            else if(x instanceof java.lang.String || y instanceof java.lang.String) { if(!(x instanceof java.lang.String && y instanceof java.lang.String) || !x.equals(y))return false; }","            else if(((java.lang.Number)x).doubleValue()!=((java.lang.Number)y).doubleValue())return false;","        }","        return true;","    }","}"};
    if(language=="python")return new[]{"def __NAME___format(values):","    def element(value):","        if __BUILTINS__.isinstance(value,__BUILTINS__.str):","            return '\"'+value.replace('\\\\','\\\\\\\\').replace('\"','\\\\\"').replace('\\n','\\\\n').replace('\\r','\\\\r').replace('\\t','\\\\t')+'\"'","        if __BUILTINS__.isinstance(value,__BUILTINS__.bool):","            return 'vero' if value else 'falso'","        return '0' if value==0 else __BUILTINS__.format(value,'.15g').upper()","    return '['+', '.join(element(value) for value in values)+']'","def __NAME___equal(left,right):","    if not __BUILTINS__.isinstance(left,__BUILTINS__.list) or not __BUILTINS__.isinstance(right,__BUILTINS__.list) or __BUILTINS__.len(left)!=__BUILTINS__.len(right):","        return False","    for x,y in __BUILTINS__.zip(left,right):","        if __BUILTINS__.isinstance(x,__BUILTINS__.bool) or __BUILTINS__.isinstance(y,__BUILTINS__.bool):","            if __BUILTINS__.type(x)!=__BUILTINS__.type(y) or x!=y: return False","        elif __BUILTINS__.isinstance(x,__BUILTINS__.str) or __BUILTINS__.isinstance(y,__BUILTINS__.str):","            if __BUILTINS__.type(x)!=__BUILTINS__.type(y) or x!=y: return False","        elif x!=y: return False","    return True"};
    return new string[0];
   }
   internal string VectorLanguage {get{var section=model.Find("array values");string language=section==null?"":FlowFpgtSyntax.Normalize(section.Value("language",fallback:""));return language.Length==0?ArrayCopyLanguage:language=="python"||language=="java"||language=="csharp"?language:null;}}
   internal string VectorReference(bool equals){if(vectorName==null)return null;vectorUsed=true;notices.Add("Vettori: stampa leggibile e confronti per contenuto.");return VectorLanguage=="python"?vectorName+(equals?"_equal":"_format"):(VectorLanguage=="csharp"?"global::":"")+vectorName+(equals?(VectorLanguage=="java"?".equal":".Equal"):(VectorLanguage=="java"?".format":".Format"));}
   internal IEnumerable<string> ResultTail(){if(resultUsed&&ArrayResultLanguage!="python")foreach(string line in FlowFpgtReturn.Code(ArrayResultLanguage,resultName,resultBuiltins))yield return line;}
   internal IEnumerable<string> VectorTail(){if(vectorUsed&&VectorLanguage!="python")foreach(string line in VectorCode(VectorLanguage))yield return line.Replace("__NAME__",vectorName);}
   internal void NoticeArrayReturn(){notices.Add(HasArrayResults?"Risultati vettoriali: firma, controllo del tipo al ritorno e copia indipendente.":"Risultati vettoriali: firma e regole definite da [Array Return] nel modello.");}
   internal void NoticeArrayCopy(){notices.Add("Parametri vettoriali per copia: ogni argomento conserva una copia indipendente prima di valutare gli argomenti successivi.");}
   internal void NoticeVectorValues(){notices.Add("Valori vettoriali: letterali e copie sono valutati una sola volta, conservando l’ordine degli elementi.");}
   internal Compatibility(FlowFpgtSyntax model){this.model=model;python=model.Name.Equals("Python",StringComparison.OrdinalIgnoreCase)&&model.Extension.Equals("py",StringComparison.OrdinalIgnoreCase)&&model.Require("program").Rows("text").Where(r=>r.Text.StartsWith("-->",StringComparison.Ordinal)).All(r=>r.Indent==0);java=model.Name.Equals("Java",StringComparison.OrdinalIgnoreCase)&&model.Extension.Equals("java",StringComparison.OrdinalIgnoreCase);csharp=model.Name.Equals("C#",StringComparison.OrdinalIgnoreCase)&&model.Extension.Equals("cs",StringComparison.OrdinalIgnoreCase);foreach(System.Text.RegularExpressions.Match token in System.Text.RegularExpressions.Regex.Matches(model.Text,@"[A-Za-z_][A-Za-z_0-9]*"))tokens.Add(token.Value);}
   static string Canonical(string text){var result=new StringBuilder();bool field=false;foreach(char c in text){if(c==' '||c=='\t'||c=='\r'||c=='\n')continue;if(c=='{')field=true;result.Append(field?Char.ToLowerInvariant(c):c);if(c=='}')field=false;}return result.ToString();}
   string Alias(string seed){string name=seed;for(int suffix=2;tokens.Contains(name);suffix++)name=seed+suffix.ToString(CultureInfo.InvariantCulture);tokens.Add(name);reserved.Add(name);return name;}
   internal void Configure(ISet<string> flags){
    if(flags.Contains("graph-arrays")&&StorageLanguage!=null){storageName=Alias("FcArrayStorage");if(StorageLanguage=="python")storageBuiltins=Alias("fcArrayBuiltins");if(StorageLanguage=="java")reserved.Add("java");}
    if(flags.Contains("pen-color-values")&&PenLanguage!=null){penName=Alias("FcPenColors");if(StockPen){reserved.Add("draw");reserved.Add("turtle");}if(PenLanguage=="python")penBuiltins=Alias("fcPenBuiltins");if(PenLanguage=="java")reserved.Add("java");}
    if(flags.Contains("array-return")&&ArrayResultLanguage!=null){resultName=Alias("FcFunctionResults");if(ArrayResultLanguage=="python")resultBuiltins=Alias("fcReturnBuiltins");}
    if(flags.Contains("vector-values")&&VectorLanguage!=null){string seed="FcVectorValues";for(int n=2;tokens.Contains(seed)||tokens.Contains(seed+"_format")||tokens.Contains(seed+"_equal");n++)seed="FcVectorValues"+n;vectorName=Alias(seed);if(VectorLanguage=="python"){reserved.Add(vectorName+"_format");reserved.Add(vectorName+"_equal");vectorBuiltins=Alias("fcVectorBuiltins");}if(VectorLanguage=="java")reserved.Add("java");}

    if(java&&(flags.Contains("input")||flags.Contains("array-value-copies")||flags.Contains("array-value-transfers")))reserved.Add("java");
    if(python&&flags.Contains("array-value-transfers"))reserved.Add("ValueError");
    if(!python)return;
    foreach(string section in model.SectionNames){if(!flags.Contains(section))continue;var s=model.Find(section);foreach(var row in s.Rows("text")){string text=Canonical(row.Text),name;
     if(section=="abs"&&text=="fabs({1})"&&builtinsAlias==null)builtinsAlias=Alias("_fcBuiltins");
     else if((section=="pi"&&text=="math.pi"||MathFunctions.TryGetValue(section,out name)&&text==name+"({1})")&&mathAlias==null)mathAlias=Alias("_fcMath");
    }}
   }
   internal string Expand(FlowFpgtSyntax.Row row,IDictionary<string,string> fields){string replacement=null,text=Canonical(row.Text),name;
    if(python&&row.Key=="text"){
     if(row.Section=="abs"&&text=="fabs({1})"&&builtinsAlias!=null){replacement=builtinsAlias+".abs({1})";builtinsUsed=true;}
     else if(mathAlias!=null&&row.Section=="pi"&&text=="math.pi"){replacement=mathAlias+".pi";mathUsed=true;}
     else if(mathAlias!=null&&MathFunctions.TryGetValue(row.Section,out name)&&text==name+"({1})"){replacement=mathAlias+"."+name+"({1})";mathUsed=true;}
     if(replacement!=null)notices.Add("Python: dipendenze matematiche risolte e tipo intero di abs conservato.");
    }
    if(csharp&&row.Key=="text"&&(row.Section=="arccos"&&text=="Math.ACos({1})"||row.Section=="arcsin"&&text=="Math.ASin({1})")){replacement="global::System.Math."+(row.Section=="arccos"?"Acos":"Asin")+"({1})";notices.Add("C#: nomi delle funzioni trigonometriche .NET corretti.");}
    if(row.Section=="input"&&row.Key=="text"){
     if(python&&text=="{variable}=(input().lower=='true')"){replacement="{variable} = (input().lower() == 'true')";notices.Add("Python: lettura dei valori logici corretta.");}
     else if(csharp&&text=="{variable}=Console.ReadLine()"){replacement=row.Text+";";notices.Add("C#: completato il terminatore della lettura di testo.");}
     else if(csharp&&text=="{variable}=(boolean)inputValue();"){replacement="{variable} = global::System.Boolean.Parse(Console.ReadLine());";notices.Add("C#: lettura dei valori logici true/false resa compilabile.");}
     else if(java){if(text=="{variable}=input.nextInt();")replacement="{variable} = java.lang.Integer.parseInt(input.nextLine().trim());";else if(text=="{variable}=input.nextDouble();")replacement="{variable} = java.lang.Double.parseDouble(input.nextLine().trim());";else if(text=="{variable}=input.nextBoolean();")replacement="{variable} = new java.util.Scanner(input.nextLine()).nextBoolean();";if(replacement!=null)notices.Add("Java: ogni lettura usa una riga, senza perdere il testo dopo numeri o valori logici.");}
    }
    return replacement==null?row.Expand(fields):new FlowFpgtSyntax.Row(row.Line,row.Section,row.Key,replacement,row.Indent,row.Conditions).Expand(fields);
   }
   internal IEnumerable<string> Prelude(){if(storageUsed&&StorageLanguage=="python")foreach(string line in FlowFpgtStorage.Code("python",storageName,storageBuiltins))yield return line;if(penUsed&&PenLanguage=="python")foreach(string line in FlowFpgtPen.Code("python",penName,penBuiltins))yield return line;if(resultUsed&&ArrayResultLanguage=="python")foreach(string line in FlowFpgtReturn.Code("python",resultName,resultBuiltins))yield return line;if(builtinsUsed)yield return "import builtins as "+builtinsAlias;if(mathUsed)yield return "import math as "+mathAlias;if(vectorUsed&&VectorLanguage=="python"){yield return "import builtins as "+vectorBuiltins;foreach(string line in VectorCode("python"))yield return line.Replace("__NAME__",vectorName).Replace("__BUILTINS__",vectorBuiltins);}}
  }
  sealed partial class Engine {
   readonly FlowFpgtSyntax model;readonly FlowSource.Program program;readonly FlowFpgtTypes facts;
   readonly HashSet<string> global=new HashSet<string>(StringComparer.Ordinal),known=new HashSet<string>(StringComparer.Ordinal);
   readonly Dictionary<string,string> functions=new Dictionary<string,string>(StringComparer.Ordinal),variables=new Dictionary<string,string>(StringComparer.Ordinal);
   readonly Dictionary<string,HashSet<string>> used=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
   readonly Dictionary<string,string[]> dispatchNames=new Dictionary<string,string[]>(StringComparer.Ordinal);
   readonly Dictionary<string,string> arrayIndexNames=new Dictionary<string,string>(StringComparer.Ordinal);
   readonly Dictionary<string,string> declarationSizeNames=new Dictionary<string,string>(StringComparer.Ordinal);
   readonly Dictionary<string,B> arrayDeclarations=new Dictionary<string,B>(StringComparer.Ordinal);
   readonly Dictionary<string,string> arrayShapes=new Dictionary<string,string>(StringComparer.Ordinal);
   readonly Dictionary<B,string> penSnapshots=new Dictionary<B,string>();
   readonly Dictionary<FlowSource.Write,string[]> writeSnapshots=new Dictionary<FlowSource.Write,string[]>();
   sealed class ArrayTransfer {internal string Value,Converted,Index;internal T Type;}
   readonly Dictionary<FlowSource.Write,ArrayTransfer> arrayTransfers=new Dictionary<FlowSource.Write,ArrayTransfer>();
   readonly HashSet<string> reserved;readonly List<OutputLine> lines=new List<OutputLine>();readonly HashSet<string> sectionStack=new HashSet<string>(StringComparer.Ordinal);
   readonly Compatibility compatibility;
   S current;B block;int operations,characters,recursion,temporary;string commentPrefix="";
   sealed class OutputLine {internal string Code;internal int Indent;internal readonly List<FlowSourceText.Origin> Origins=new List<FlowSourceText.Origin>();}
   sealed class Context {
    internal readonly Dictionary<string,string> Fields;internal readonly HashSet<string> Flags;
    internal Context(HashSet<string> flags){Flags=new HashSet<string>(flags,StringComparer.Ordinal);Fields=new Dictionary<string,string>(StringComparer.Ordinal);}
    internal Context Copy(){var result=new Context(Flags);foreach(var item in Fields)result.Fields.Add(item.Key,item.Value);return result;}
    internal Context Put(string name,string value){Fields[name]=value??"";return this;}
    internal Context Flag(string name,bool yes=true){if(yes)Flags.Add(name);else Flags.Remove(name);return this;}
    internal Context Type(T type,string suffix=""){foreach(string f in new[]{"integer","real","string","boolean","none"})Flag(f+suffix,f==type.Flag);if(suffix.Length==0)Flag("array",type.Array);return this;}
   }
   internal Engine(FlowFpgtSyntax model,FlowSource.Program program,IDictionary<string,FlowDataType> choices){
    if(model==null)throw new ArgumentNullException("model");this.model=model;this.program=program;facts=new FlowFpgtTypes(program,choices);compatibility=new Compatibility(model);
    if(facts.Issues.Count>0){var issue=facts.Issues[0];var scope=facts.Find(issue.FunctionId);throw new GenerationError(scope,scope.Source.Find(issue.NodeId),issue.Message);}
    reserved=new HashSet<string>(model.CaseSensitive?StringComparer.Ordinal:StringComparer.OrdinalIgnoreCase);
    foreach(string key in new[]{"keywords","conflicts"})foreach(var row in model.Require("language").Rows(key))foreach(string name in row.Text.Split(','))if(name.Trim().Length>0)reserved.Add(name.Trim());
    foreach(string name in ("first last main functions declare return block else parameters arguments variable literal literal-1 literal-2 subscript from-parameter multiple inc step newline read write writer input output files turtle trigonometry arrays aligned hanging float left right down up black gray red orange yellow green teal blue violet brown by-reference by-value constant-size array array-parameter array-value-copies array-value-transfers vector-values array-return pen-color-values computed-color white lime cyan skyblue purple magenta pink graph-arrays hoisted").Split(' '))known.Add(name);
    foreach(string a in new[]{"integer","real","string","boolean","none"}){known.Add(a);known.Add(a+"-arrays");foreach(string suffix in new[]{"1","2","cast","variable","expression","parameter"})known.Add(a+"-"+suffix);foreach(string b in new[]{"integer","real","string","boolean","none"}){known.Add(a+"-"+b);known.Add(a+"-to-"+b);}}
    foreach(string name in ("abs sin cos tan arcsin arccos arctan asin acos int floor round log ln log10 sgn pi random size char tochar tocode tofixed tostring tointeger toreal len sqrt eof min max sum somma array vettore").Split(' '))known.Add(name);
    foreach(string section in model.SectionNames){var s=model.Find(section);foreach(var row in s.Rows("tag"))foreach(string tag in row.Text.Split(','))if(tag.Trim().Length>0)known.Add("tag-"+N(tag));}
    foreach(var scope in facts.Scopes)if(!FlowSourcePlan.Create(scope.Source).Structured){var names=new string[2];for(int i=0;i<2;i++){string seed=i==0?"fcPosition":"fcVisit";string raw=seed;int suffix=2;while(scope.Find(raw)!=null)raw=seed+suffix++;var symbol=scope.Add(raw,scope.Source.StartId);symbol.Type=new T(1);symbol.Fixed=symbol.Declared=true;names[i]=raw;}dispatchNames.Add(scope.Source.Id??"",names);}
    foreach(var scope in facts.Scopes)if(scope.Source.Blocks.Any(b=>b.Operation.Declaration!=null&&b.Operation.Declaration.IsArray)){string raw="fcElement";int suffix=2;while(scope.Find(raw)!=null)raw="fcElement"+suffix++;var symbol=scope.Add(raw,scope.Source.StartId);symbol.Type=new T(1);symbol.Fixed=symbol.Declared=true;arrayIndexNames.Add(scope.Source.Id??"",raw);global.Add("size");}
    // A template can repeat {size} for each declared name. Capture a dynamic dimension
    // at the declaration, once per execution, before allocating any of its arrays.
    foreach(var scope in facts.Scopes)foreach(var node in scope.Source.Blocks){var declaration=node.Operation.Declaration;if(declaration==null||!declaration.IsArray||declaration.Size.Kind==FlowSource.ExpressionKind.Number)continue;var dimensionType=facts.Infer(scope,declaration.Size);if(!dimensionType.Numeric)throw new GenerationError(scope,node,"La dimensione del vettore deve avere un tipo numerico.");string raw="fcLength";int suffix=2;while(scope.Find(raw)!=null)raw="fcLength"+suffix++;var symbol=scope.Add(raw,scope.Source.StartId);symbol.Type=dimensionType;symbol.Fixed=symbol.Declared=true;declarationSizeNames.Add((scope.Source.Id??"")+":"+node.Id,raw);}
    CollectArrayTransfers();CollectWriteSnapshots();CollectPenSnapshots();CollectGlobals();if(facts.Scopes.Any(s=>dispatchNames.ContainsKey(s.Source.Id??"")&&s.Symbols.Any(v=>v.Type.Array&&!v.Parameter)))global.Add("graph-arrays");compatibility.Configure(global);foreach(string name in compatibility.Reserved)reserved.Add(name);AllocateNames();var comment=model.Find("comment");if(comment!=null)foreach(var row in comment.Rows("text")){int index=row.Text.IndexOf("{text}",StringComparison.OrdinalIgnoreCase);if(index>0){commentPrefix=row.Text.Substring(0,index).Replace("{{","{").Trim();break;}}
   }
   bool PenExpressions {get{var pen=model.Find("pen");return compatibility.StockPen||pen!=null&&pen.Rows("text").Any(r=>UsesPenExpression(r.Text));}}
   static bool UsesPenExpression(string text){for(int i=0;i<text.Length;i++){if(text[i]!='{')continue;if(i+1<text.Length&&text[i+1]=='{'){i++;continue;}int end=text.IndexOf('}',i+1);if(end<0)return false;if(N(text.Substring(i+1,end-i-1))=="expression")return true;i=end;}return false;}
   void CollectPenSnapshots(){if(!PenExpressions)return;foreach(var scope in facts.Scopes)foreach(var node in scope.Source.Blocks){if(node.Kind!=NodeKind.TurtlePen)continue;var value=node.Operation.Value;var type=facts.Infer(scope,value);if(type.Kind!=3||type.Array)throw new GenerationError(scope,node,"Il colore della penna richiede un testo.");if(value.Kind==FlowSource.ExpressionKind.Text){try{FlowTurtle.ParseColor(value.Text);}catch(FlowException error){throw new GenerationError(scope,node,error.Message);}}penSnapshots.Add(node,WriteTemporary(scope,"fcPenColor",new T(3),node));global.Add("pen-color-values");}}
   void CollectWriteSnapshots(){
    foreach(var scope in facts.Scopes){current=scope;foreach(var node in scope.Source.Blocks){block=node;foreach(var write in node.Operation.Writes){Tick();if(write.Target.Index==null||!MayHaveEffects(write.Value)&&!MayHaveEffects(write.Target.Index))continue;
     var value=facts.Infer(scope,write.Value);var index=facts.Infer(scope,write.Target.Index);if(value.Array)continue;
     if(value.Kind<1||value.Kind>4||!index.Numeric)throw Fail("L’assegnazione indicizzata richiede un valore scalare e un indice numerico.");
     writeSnapshots.Add(write,new[]{WriteTemporary(scope,"fcWriteValue",value,node),WriteTemporary(scope,"fcWriteIndex",index,node)});
    }}}current=null;block=null;
   }
   string WriteTemporary(S scope,string seed,T type,B node){string raw=seed;int suffix=2;while(scope.Find(raw)!=null)raw=seed+suffix++;var symbol=scope.Add(raw,node.Id);symbol.Type=type;symbol.Fixed=symbol.Declared=true;return raw;}
   bool HasArrayCopy {get{return compatibility.ArrayCopyLanguage!=null||model.Find("array copy")!=null;}}
   bool HasArrayMismatch {get{return compatibility.ArrayCopyLanguage!=null||model.Find("array mismatch")!=null;}}
   void CollectArrayTransfers(){
    if(!HasArrayCopy)return;
    foreach(var scope in facts.Scopes){current=scope;foreach(var node in scope.Source.Blocks){block=node;foreach(var write in node.Operation.Writes){
     var target=scope.Find(write.Target.Name);if(write.Target.Index!=null||target==null||!target.Type.Array)continue;
     var source=write.Value.Kind==FlowSource.ExpressionKind.Variable?scope.Find(write.Value.Text):null;
     string targetShape=ArrayShape(target,new HashSet<string>(StringComparer.Ordinal)),sourceShape=ArrayShape(source,new HashSet<string>(StringComparer.Ordinal));
     if(ArrayDeclaration(target)!=null&&targetShape!=null&&targetShape==sourceShape)continue;
     if(target.Declared&&!HasArrayMismatch)continue;
     var type=facts.Infer(scope,write.Value);if(write.Value.Kind==FlowSource.ExpressionKind.Vector&&write.Value.Children.Count==0)type=target.Type;
     if(!type.Array||type.Kind<1||type.Kind>4)continue;
     var transfer=new ArrayTransfer{Type=type,Value=WriteTemporary(scope,"fcVectorValue",type,node)};
     if(type.Kind!=target.Type.Kind){transfer.Converted=WriteTemporary(scope,"fcVectorResult",target.Type,node);transfer.Index=WriteTemporary(scope,"fcVectorIndex",new T(1),node);}
     arrayTransfers.Add(write,transfer);
    }}}current=null;block=null;
   }
   bool MayHaveEffects(E expression){var pending=new Stack<E>();pending.Push(expression);while(pending.Count>0){Tick();var e=pending.Pop();bool ignored;if(FlowFpgtTypes.ShortCircuit(e,out ignored))continue;if(e.Kind==FlowSource.ExpressionKind.Call&&(e.FunctionId!=null||e.Text=="random"||e.Text=="eof"))return true;foreach(var child in e.Children)pending.Push(child);}return false;}
   static string N(string s){return FlowFpgtSyntax.Normalize(s);}void Tick(){if(++operations>500000)throw Fail("La generazione supera il limite di complessità del modello.");}
   GenerationError Fail(string text){return new GenerationError(current,block,text);}
   Context C(){return new Context(global);}FlowFpgtSyntax.Section Require(string section){Tick();var result=model.Find(section);if(result==null)throw Fail("Il modello non definisce ["+section+"].");return result;}
   string Value(string section,string key,Context context,string fallback=""){var s=model.Find(section);return s==null?fallback:s.Value(key,context.Flags,context.Fields,fallback,known);}
   int Number(string section,string key,Context context,int fallback){var s=model.Find(section);return s==null?fallback:s.Number(key,context.Flags,fallback,known);}
   void CollectGlobals(){
    if(program.Scopes.Any(s=>s.ReturnIsArray))global.Add("array-return");if(program.Scopes.Count>1)global.Add("functions");global.Add("aligned");
    if(program.Scopes.Any(s=>s.Parameters.Any(p=>p.IsArray&&!p.ByReference)))global.Add("array-value-copies");
    if(arrayTransfers.Count>0){global.Add("array-value-transfers");global.Add("size");}
    foreach(var scope in facts.Scopes){foreach(var symbol in scope.Symbols){if(symbol.Type.Kind>0&&symbol.Type.Kind<5)global.Add(symbol.Type.Flag);if(symbol.Type.Array){global.Add("arrays");global.Add(symbol.Type.Flag+"-arrays");}if(symbol.Type.Kind==2)global.Add("float");if(!symbol.Declared&&(symbol.Name.Equals("pi",StringComparison.OrdinalIgnoreCase)||symbol.Name=="π")){global.Add("pi");global.Add("trigonometry");}}
     foreach(var b in scope.Source.Blocks){if((b.Kind==NodeKind.Output||b.Kind==NodeKind.WriteFile)&&facts.Infer(scope,b.Operation.Value).Array)global.Add("vector-values");if(b.Kind==NodeKind.Input)global.Add("input");if(b.Kind==NodeKind.Output)global.Add("output");if(FlowTurtle.IsTurtle(b.Kind))global.Add("turtle");if(FileBlocks.IsFile(b.Kind)){global.Add("files");global.Add(b.Operation.FileReading?"read":"write");if(!b.Operation.FileReading)global.Add("writer");}
      foreach(var root in Roots(b)){var pending=new Stack<E>();pending.Push(root);while(pending.Count>0){var e=pending.Pop();if(e.Kind==FlowSource.ExpressionKind.Binary&&(facts.Infer(scope,e.Children[0]).Array||facts.Infer(scope,e.Children[1]).Array))global.Add("vector-values");if(e.Kind==FlowSource.ExpressionKind.Vector){global.Add("arrays");var item=facts.Infer(scope,e);global.Add((item.Kind<1?"integer":item.Flag)+"-arrays");}if(e.Kind==FlowSource.ExpressionKind.Call&&e.FunctionId==null){string name=e.Text=="ln"?"log":e.Text;global.Add(name);if(new[]{"sin","cos","tan","arcsin","arccos","arctan","pi"}.Contains(name))global.Add("trigonometry");if(name=="arccos")global.Add("acos");if(name=="arcsin")global.Add("asin");}if(e.Kind==FlowSource.ExpressionKind.Variable){var v=scope.Find(e.Text);if(v!=null&&v.ConstantPi){global.Add("pi");global.Add("trigonometry");}}foreach(var child in e.Children)pending.Push(child);}}
     }
    }
   }
   static IEnumerable<E> Roots(B b){var o=b.Operation;if(o.Value!=null)yield return o.Value;if(o.Input!=null&&o.Input.Index!=null)yield return o.Input.Index;if(o.Declaration!=null&&o.Declaration.Size!=null)yield return o.Declaration.Size;foreach(var w in o.Writes){yield return w.Value;if(w.Target.Index!=null)yield return w.Target.Index;}}
   void AllocateNames(){var all=new HashSet<string>(reserved,reserved.Comparer);foreach(var s in facts.Scopes.Where(s=>s.Source.Id!=null)){current=s;block=s.Source.Find(s.Source.StartId);functions.Add(s.Source.Id,Unique(Identifier("function id",s.Source.Name,s.ReturnType),all));}
    foreach(var s in facts.Scopes){current=s;block=s.Source.Find(s.Source.StartId);var names=new HashSet<string>(all,all.Comparer);used.Add(s.Source.Id??"",names);foreach(var v in s.Symbols.Where(v=>!v.ConstantPi))variables.Add(FlowFpgtTypes.Key(s.Source.Id,v.Name),Unique(Identifier("variable id",v.Name,v.Type),names));}current=null;block=null;
   }
   string Identifier(string section,string raw,T type){var ctx=C().Type(type);var config=model.Find(section);string convention=config==null?"":N(config.Value("convention",fallback:""));string name=raw;var words=raw.Split(new[]{' ','_'},StringSplitOptions.RemoveEmptyEntries);if(words.Length>0&&(convention=="camel"||convention=="proper"))name=String.Concat(words.Select((w,i)=>(i==0&&convention=="camel"?Char.ToLowerInvariant(w[0]):Char.ToUpperInvariant(w[0]))+w.Substring(1)));else if(convention=="upper"||convention=="uppercase")name=raw.ToUpperInvariant();else if(convention=="lower"||convention=="lowercase")name=raw.ToLowerInvariant();else if(convention.Length>0&&convention!="none")throw Fail("Convenzione dei nomi non supportata: "+convention+".");ctx.Put("name",name);return config==null?name:config.Value(reserved.Contains(name)?"conflict":"normal",ctx.Flags,ctx.Fields,name,known);}
   string Unique(string seed,HashSet<string> names){if(seed.Length==0||seed.Length>256||seed.Any(Char.IsControl))throw Fail("Il modello ha prodotto un identificatore vuoto o troppo lungo.");string name=seed;for(int i=2;!names.Add(name);i++){if(i>10000)throw Fail("Non riesco a creare nomi distinti nel modello.");name=seed+"_"+i.ToString(CultureInfo.InvariantCulture);}return name;}
   string Var(string name){string result;if(!variables.TryGetValue(FlowFpgtTypes.Key(current.Source.Id,name),out result))throw Fail("Variabile non risolta: "+name+".");return result;}
   string TypeName(T type){return Value("types",type.Flag,C().Type(type),type.Flag=="none"?"":type.Flag);}
   string Fresh(string seed,T type){string name=Unique(Identifier("variable id",seed+ ++temporary,type),used[current.Source.Id??""]);return name;}
   void Add(string text,int indent,B origin){Tick();if(indent<0||indent>128)throw Fail("Il rientro supera i limiti del codice generato.");foreach(string row in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n')){if(lines.Count>=50000||row.Length+indent*4+1>2097152-characters)throw Fail("Il codice generato supera il limite di dimensione.");characters+=row.Length+indent*4+1;var line=new OutputLine{Code=row,Indent=indent};if(origin!=null)line.Origins.Add(new FlowSourceText.Origin(current.Source.Id,origin.Id));lines.Add(line);}}
   bool CommentOnly(string text){return text.Trim().Length==0||commentPrefix.Length>0&&text.TrimStart().StartsWith(commentPrefix,StringComparison.Ordinal);}
   void Emit(string section,Context context,int indent,B origin,Action<string,int> special=null,bool required=true){var s=Require(section);bool meaningful=false,selected=false;foreach(var row in s.Rows("text")){Tick();if(!row.Matches(context.Flags,known))continue;selected=true;string marker=N(row.Text);if(marker.StartsWith("-->",StringComparison.Ordinal)){if(special==null)throw row.Error("Questo punto non ammette il marcatore "+row.Text+".");special(marker,indent+row.Indent);meaningful=true;}else{string code=compatibility.Expand(row,context.Fields);Add(code,indent+row.Indent,origin);meaningful|=!CommentOnly(code);}}if(required&&(!selected||!meaningful))throw Fail("Il modello non genera un’istruzione eseguibile per ["+section+"].");}
   internal FlowSourceText Run(){var ctx=C();Emit("program",ctx,0,null,(marker,indent)=>{if(marker=="-->main")EmitFunction(facts.Scopes.Single(s=>s.Source.Id==null),indent,false);else if(marker=="-->functions")foreach(var scope in facts.Scopes.Where(s=>s.Source.Id!=null))EmitFunction(scope,indent,false);else if(marker=="-->headers")foreach(var scope in facts.Scopes.Where(s=>s.Source.Id!=null))EmitFunction(scope,indent,true);else throw Fail("Marcatore del programma non supportato: "+marker+".");},false);
    foreach(string line in compatibility.StorageTail())Add(line,0,null);foreach(string line in compatibility.PenTail())Add(line,0,null);foreach(string line in compatibility.VectorTail())Add(line,0,null);foreach(string line in compatibility.ResultTail())Add(line,0,null);
    int prelude=lines.FindIndex(l=>l.Origins.Count>0);if(prelude<0)prelude=0;foreach(string line in compatibility.Prelude())lines.Insert(prelude++,new OutputLine{Code=line,Indent=0});
    var output=new FlowSourceText.Builder(program,"flowgorithm");foreach(var line in lines)output.AddLine(line.Code,line.Indent,line.Origins.ToArray());var result=output.Build().WithNotices(compatibility.Notices);foreach(var scope in program.Scopes)foreach(var b in scope.Blocks.Concat(scope.Notes))if(result.ForBlock(scope.Id,b.Id).Count==0)throw new GenerationError(facts.Find(scope.Id),b,"Il modello non include questo blocco nel codice. Nessun file è stato esportato.");return result;
   }
   void ValidateResultLayout(S scope){
    string language=compatibility.ArrayResultLanguage;if(language==null||language=="python")return;
    string type=TypeName(scope.ReturnType).Replace("global::","").Replace("System.","").Replace("java.lang.","").ToLowerInvariant();
    string expected=scope.ReturnType.Kind==1?"int":scope.ReturnType.Kind==2?"double":scope.ReturnType.Kind==3?"string":language=="java"?"boolean":"bool";
    bool valid=type==expected||language=="csharp"&&(scope.ReturnType.Kind==1&&type=="int32"||scope.ReturnType.Kind==4&&type=="boolean");
    if(!valid)throw Fail("Il tipo di risultato del modello non è compatibile con l’adattamento [Array Return]. Usa Language = custom con regole esplicite oppure un generatore integrato.");
    foreach(var node in scope.Source.Blocks)foreach(var root in Roots(node)){var pending=new Stack<E>();pending.Push(root);while(pending.Count>0){var value=pending.Pop();if(value.Kind==FlowSource.ExpressionKind.Unary&&value.Text=="-"&&value.Children[0].Kind==FlowSource.ExpressionKind.Number&&value.Children[0].Number==2147483648d)continue;if(value.Kind==FlowSource.ExpressionKind.Number&&value.Number==Math.Truncate(value.Number)&&Math.Abs(value.Number)<=9007199254740991d&&(value.Number<Int32.MinValue||value.Number>Int32.MaxValue))throw new GenerationError(scope,node,"Questo modello usa interi a 32 bit: il valore supera il suo intervallo. Per mantenere il valore scegli un generatore integrato.");foreach(var child in value.Children)pending.Push(child);}}
   }
   string FunctionResultType(S scope){
    if(!scope.Source.ReturnIsArray)return TypeName(scope.ReturnType);ValidateResultLayout(scope);
    var ctx=C().Type(scope.ReturnType).Flag("array-return").Put("type",TypeName(scope.ReturnType));
    if(model.Find("array return")!=null){string result=Scalar("array return","type",ctx);if(String.IsNullOrWhiteSpace(result))throw Fail("Il modello deve definire il tipo di [Array Return].");return result;}
    string language=compatibility.ArrayCopyLanguage;if(language==null)throw Fail("Per restituire un vettore, definisci [Array Return] nel modello oppure scegli un generatore integrato.");
    return TypeName(scope.ReturnType)+(language=="java"||language=="csharp"?"[]":"");
   }
   string FunctionResultValue(S scope){
    if(!scope.Source.ReturnIsArray)return Var(scope.Source.ReturnVariable);
    var result=Variable(scope.Find(scope.Source.ReturnVariable),null);string value;if(compatibility.HasArrayResults)result=new Rendered(compatibility.ResultArray(result.Code,scope.ReturnType.Kind),scope.ReturnType,100);
    if(model.Find("array return")!=null){var ctx=ExpressionContext(result).Flag("array-return");ctx.Put("expression",Need(result,Number("array return","needed",ctx,0)));value=Scalar("array return","text",ctx);}
    else value=compatibility.HasArrayResults?result.Code:ArrayArgument(result,scope.ReturnType,false).Code;
    compatibility.NoticeArrayReturn();return value;
   }
   Context ScopeContext(S s){var ctx=C().Flag("main",s.Source.Id==null).Flag("array-return",s.Source.ReturnIsArray).Flag("return",s.Source.Id!=null&&s.ReturnType.Kind!=5).Flag("parameters",s.Source.Parameters.Count>0).Flag("declare",s.Symbols.Any(v=>!v.Parameter&&!v.ConstantPi)).Flag("block",s.Source.Blocks.Any(b=>b.Kind!=NodeKind.Start&&b.Kind!=NodeKind.End)||s.Symbols.Any(v=>v.Return)).Type(s.ReturnType);ctx.Put("name",s.Source.Id==null?"Main":functions[s.Source.Id]).Put("type",FunctionResultType(s));if(s.ReturnType.Kind!=5&&!String.IsNullOrEmpty(s.Source.ReturnVariable))ctx.Put("return",FunctionResultValue(s));ctx.Put("parameters",Parameters(s));return ctx;}
   string Parameters(S s){var values=new List<string>();for(int i=0;i<s.Source.Parameters.Count;i++){var p=s.Source.Parameters[i];var symbol=s.Find(p.Name);if(p.IsArray&&!p.ByReference&&model.Find("array copy")==null&&compatibility.ArrayCopyLanguage==null)throw Fail("Per i parametri vettoriali per copia, definisci [Array Copy] nel modello oppure scegli un generatore integrato.");var ctx=C().Type(symbol.Type).Flag("first",i==0).Flag("last",i==s.Source.Parameters.Count-1).Flag("by-reference",p.ByReference).Flag("by-value",!p.ByReference).Put("name",Var(p.Name)).Put("type",TypeName(symbol.Type));values.Add(Scalar("parameter","text",ctx));}return String.Join(Value("parameter","separator",C(),", "),values);}
   void EmitFunction(S scope,int indent,bool header){var saved=current;var savedBlock=block;current=scope;block=scope.Source.Find(scope.Source.StartId);try{int first=lines.Count;string section=header?"function header":scope.Source.Id==null?"main":"function";var ctx=ScopeContext(scope);bool bodySeen=false;Emit(section,ctx,indent,block,(marker,level)=>{if(marker=="-->block"){if(bodySeen)throw Fail("Il modello inserisce due volte il corpo della funzione.");bodySeen=true;EmitBody(scope,level,Number(section,"block extra",ctx,0));}else if(marker=="-->variables")EmitVariableHeader(scope,level);else if(marker=="-->parameters")EmitParameterHeader(scope,level);else throw Fail("Marcatore della funzione non supportato: "+marker+".");},false);if(!header&&!bodySeen)throw Fail("Il modello non include -->BLOCK per questa funzione.");if(lines.Count>first){lines[first].Origins.Add(new FlowSourceText.Origin(scope.Source.Id,scope.Source.StartId));foreach(var end in scope.Source.Blocks.Where(b=>b.Kind==NodeKind.End))if(!lines.Any(l=>l.Origins.Any(o=>o.Matches(scope.Source.Id,end.Id))))lines[lines.Count-1].Origins.Add(new FlowSourceText.Origin(scope.Source.Id,end.Id));}}finally{current=saved;block=savedBlock;}}
   void EmitParameterHeader(S s,int indent){for(int i=0;i<s.Source.Parameters.Count;i++){var p=s.Source.Parameters[i];var ctx=C().Type(s.Find(p.Name).Type).Flag("first",i==0).Flag("last",i==s.Source.Parameters.Count-1).Put("name",Var(p.Name)).Put("type",TypeName(s.Find(p.Name).Type));Emit("parameter header",ctx,indent,block,required:false);}}
   void EmitVariableHeader(S scope,int indent){
    var symbols=scope.Symbols.Where(v=>!v.Parameter&&!v.ConstantPi).ToList();string mode=N(Value("variable header","name mode",C(),"separate"));
    var groups=mode=="singular"?symbols.Select(v=>new List<FlowFpgtTypes.Symbol>{v}).ToList():mode=="merged"?new List<List<FlowFpgtTypes.Symbol>>{symbols}:symbols.GroupBy(v=>v.Type.Kind+":"+v.Type.Array+":"+(v.Size==null?"":Expression(v.Size).Code)).Select(g=>g.ToList()).ToList();
    for(int at=0;at<groups.Count;at++){var rows=groups[at];if(rows.Count==0)continue;var first=rows[0];var ctx=C().Type(first.Type).Flag("first",at==0).Flag("last",at==groups.Count-1).Flag("constant-size",first.Size!=null&&first.Size.Kind==FlowSource.ExpressionKind.Number).Put("name",Var(first.Name)).Put("type",TypeName(first.Type)).Put("size",first.Size==null?(first.LiteralLength??0).ToString(CultureInfo.InvariantCulture):Expression(first.Size).Code);var names=new List<string>();
     for(int i=0;i<rows.Count;i++)names.Add(Scalar("variable header name","text",ctx.Copy().Flag("first",i==0).Flag("last",i==rows.Count-1).Put("name",Var(rows[i].Name))));ctx.Put("variables",String.Join(Value("variable header name","separator",ctx,", "),names));Emit("variable header",ctx,indent,scope.Source.Find(first.NodeId),required:false);
    }
   }
   void EmitBody(S scope,int indent,int extra){
    foreach(var note in scope.Source.Notes)Comment(note.Text,note,indent);
    // Dispatch states share one lexical scope. Hoist scalar declarations and initialise them
    // so target compilers do not have to prove which state executes before another one.
    bool graph=dispatchNames.ContainsKey(scope.Source.Id??"");
    foreach(var symbol in scope.Symbols.Where(v=>!v.Parameter&&!v.ConstantPi)){
     if(symbol.Type.Array){if(graph){block=scope.Source.Find(symbol.NodeId);GraphStorage(symbol.Name,symbol.Type,indent,block);continue;}if(!symbol.Declared&&HasArrayCopy)Declare(new[]{symbol.Name},symbol.Type,new E(FlowSource.ExpressionKind.Number,null,0,false,null,null),indent,scope.Source.Find(symbol.NodeId),false);continue;}
     var origin=scope.Source.Find(symbol.NodeId);Declare(new[]{symbol.Name},symbol.Type,null,indent,origin,false);
     bool pi=!symbol.Declared&&(symbol.Name.Equals("pi",StringComparison.OrdinalIgnoreCase)||symbol.Name=="π");
     Assign(new Rendered(Var(symbol.Name),symbol.Type,Int32.MaxValue,true),pi?Intrinsic("pi",new Rendered[0],new T(2)):Default(symbol.Type),C(),indent,origin);
    }
    var plan=FlowSourcePlan.Create(scope.Source);if(plan.Structured)Statements(plan.Statements,indent,extra);else Dispatch(scope,indent);
   }
   static bool Executable(FlowSourcePlan.Statement s){return s.Kind!=FlowSourcePlan.Kind.Instruction||s.Block.Kind!=NodeKind.Start&&s.Block.Kind!=NodeKind.End;}
   void Statements(IEnumerable<FlowSourcePlan.Statement> input,int indent,int extra=0){var statements=input.ToList();int count=statements.Count(Executable),at=0;foreach(var s in statements){block=s.Block;if(!Executable(s)){Comment(s.Block.Comment,s.Block,indent);continue;}var ctx=C().Flag("first",at==0).Flag("last",++at==count&&extra==0);if(s.Kind==FlowSourcePlan.Kind.Instruction)Instruction(s.Block,ctx,indent);else{if(s.Kind!=FlowSourcePlan.Kind.Repeat)Comment(s.Block.Comment,s.Block,indent);var condition=Truth(Expression(s.Block.Operation.Value));if(!s.LoopOnTrue&&s.Kind!=FlowSourcePlan.Kind.If)condition=Operator("not",condition,null,new T(4));ctx.Put("condition",condition.Code).Put("not condition",Operator("not",condition,null,new T(4)).Code).Flag("else",s.Otherwise.Any(Executable)).Flag("block",s.Body.Any(Executable));string section=s.Kind==FlowSourcePlan.Kind.If?"if":s.Kind==FlowSourcePlan.Kind.Repeat?"do":"while";int start=lines.Count;Emit(section,ctx,indent,s.Block,(marker,level)=>{if(marker=="-->block"||marker=="-->trueblock"){Statements(s.Body,level);if(s.Kind==FlowSourcePlan.Kind.Repeat)Comment(s.Block.Comment,s.Block,level);}else if(marker=="-->falseblock")Statements(s.Otherwise,level);else throw Fail("Marcatore del blocco non supportato: "+marker+".");});if(s.Kind==FlowSourcePlan.Kind.Repeat&&lines.Count>start)lines[start].Origins.Add(new FlowSourceText.Origin(current.Source.Id,s.EntryBlock.Id));}}
   }
   void Comment(string text,B node,int indent){if(String.IsNullOrEmpty(text)&&(node==null||node.Kind!=NodeKind.Note))return;foreach(string line in (text??"").Replace("\r\n","\n").Replace('\r','\n').Split('\n')){var ctx=C().Put("text",Replace("comment",line));Emit("comment",ctx,indent,node,required:false);}}
   void GraphStorage(string name,T type,int indent,B node){
    compatibility.NoticeGraphArrays();var ctx=C().Type(type).Flag("hoisted").Put("name",Var(name)).Put("variable",Var(name)).Put("type",TypeName(type)).Put("size","0");var section=model.Find("array storage");
    if(section!=null&&section.Has("text")){Emit("array storage",ctx,indent,node);return;}
    string language=compatibility.StorageLanguage;if(language==null)throw new GenerationError(current,node,"Questo modello richiede [Array Storage] e [Array Allocate] per i vettori nei grafi liberi, oppure un generatore integrato.");
    Add(language=="python"?Var(name)+" = []":TypeName(type)+"[] "+Var(name)+" = new "+TypeName(type)+"[0];",indent,node);
   }
   void GraphAllocate(string name,T type,string size,int indent,B node){
    string count=compatibility.ArrayLength(size);var ctx=C().Type(type).Put("name",Var(name)).Put("variable",Var(name)).Put("type",TypeName(type)).Put("size",count).Put("default",Default(type.Element).Code);var section=model.Find("array allocate");
    if(section!=null&&section.Has("text")){Emit("array allocate",ctx,indent,node);return;}
    string language=compatibility.StorageLanguage;if(language==null)throw new GenerationError(current,node,"Il modello deve definire [Array Allocate] con {Name} e {Size} per allocare il vettore nel blocco Dichiara.");
    Add(language=="python"?Var(name)+" = ["+Default(type.Element).Code+"] * ("+count+")":Var(name)+" = new "+TypeName(type)+"["+count+"];",indent,node);
   }
   void InitializeArray(string name,T type,int indent,B node){
      string indexName=arrayIndexNames[current.Source.Id??""];var index=new Rendered(Var(indexName),new T(1),Int32.MaxValue,true);Assign(index,Numeric(0),C(),indent,node);
      var condition=Operator("<",index,Intrinsic("size",new[]{Variable(current.Find(name),null)},new T(1)),new T(4));
      Emit("while",C().Flag("block").Put("condition",condition.Code).Put("not condition",Operator("not",condition,null,new T(4)).Code),indent,node,(marker,level)=>{
       if(marker!="-->block")throw Fail("Marcatore non valido nell’inizializzazione del vettore.");var subscript=new E(FlowSource.ExpressionKind.Variable,indexName,0,false,null,null);
       Assign(Variable(current.Find(name),subscript),Default(type.Element),C(),level,node);Assign(index,Operator("+",index,Numeric(1),new T(1)),C().Flag("last"),level,node);
      });
     
   }
   void Declare(IEnumerable<string> names,T type,E size,int indent,B node,bool initialize){
    var symbols=names.ToList();string sizeText="";if(size!=null){var dimension=Expression(size);string captured;if(initialize&&type.Array&&declarationSizeNames.TryGetValue((current.Source.Id??"")+":"+node.Id,out captured)){var target=new Rendered(Var(captured),current.Find(captured).Type,Int32.MaxValue,true);Assign(target,dimension,C(),indent,node);sizeText=target.Code;}else sizeText=dimension.Code;}
    if(type.Array&&dispatchNames.ContainsKey(current.Source.Id??"")){foreach(string name in symbols)GraphAllocate(name,type,sizeText,indent,node);if(initialize)foreach(string name in symbols)InitializeArray(name,type,indent,node);return;}
    var ctx=C().Type(type).Flag("multiple",symbols.Count>1).Flag("constant-size",size!=null&&size.Kind==FlowSource.ExpressionKind.Number).Put("type",TypeName(type)).Put("size",sizeText);
    bool singular=N(Value("declare","name mode",ctx,"combined"))=="singular";var groups=singular?symbols.Select(n=>new List<string>{n}).ToList():new List<List<string>>{symbols};
    foreach(var group in groups){var entries=new List<string>();for(int i=0;i<group.Count;i++)entries.Add(Scalar("declare name","text",ctx.Copy().Flag("first",i==0).Flag("last",i==group.Count-1).Put("name",Var(group[i]))));
     var declaration=ctx.Copy().Put("name",Var(group[0])).Put("variables",String.Join(Value("declare name","separator",ctx,", "),entries));Emit("declare",declaration,indent,node,required:false);
     if(initialize)foreach(string name in group){if(!type.Array)Assign(new Rendered(Var(name),type,Int32.MaxValue,true),Default(type),C(),indent,node);else InitializeArray(name,type,indent,node);}
    }
   }
   void Instruction(B node,Context ctx,int indent){block=node;Comment(node.Comment,node,indent);var op=node.Operation;switch(node.Kind){
    case NodeKind.Start:case NodeKind.End:return;
    case NodeKind.Process:for(int at=0;at<op.Writes.Count;at++)Write(op.Writes[at],ctx.Copy().Flag("first",ctx.Flags.Contains("first")&&at==0).Flag("last",ctx.Flags.Contains("last")&&at==op.Writes.Count-1),indent,node);return;
    case NodeKind.Declare:if(!op.Declaration.IsArray){foreach(string name in op.Declaration.Names)Assign(new Rendered(Var(name),new T((int)op.Declaration.Type),Int32.MaxValue,true),Default(new T((int)op.Declaration.Type)),ctx,indent,node);}else Declare(op.Declaration.Names,new T((int)op.Declaration.Type,true),op.Declaration.Size,indent,node,true);return;
    case NodeKind.Input:case NodeKind.ReadFile:var target=Target(op.Input);ctx.Type(target.Type).Flag("array",target.Type.Array||target.Subscript!=null).Put("variable",target.Code).Put("name",target.Name??target.Code).Put("subscript",target.Subscript??"").Put("type",TypeName(target.Type));Emit(node.Kind==NodeKind.Input?"input":"read",ctx,indent,node);return;
    case NodeKind.Output:case NodeKind.WriteFile:ctx.Type(facts.Infer(current,op.Value)).Flag("newline",node.Kind==NodeKind.Output?op.OutputNewline:op.FileNewline);string outputSection=node.Kind==NodeKind.Output?"output":"write";if(Uses(outputSection,"expression",ctx)){var expression=Expression(op.Value);if(!ExplicitBooleanRule(outputSection,ctx))expression=BooleanOutput(expression);if(expression.Type.Array&&HasArrayText&&!ExplicitArrayRule(outputSection,ctx))expression=ArrayText(expression);ctx.Type(expression.Type);expression=Prepared(outputSection,"prepare",expression,ctx);ctx.Put("expression",Need(expression,Number(outputSection,"needed",ctx,0)));}if(Uses(outputSection,"list",ctx))ctx.Put("list",OutputList(op.Value,ExplicitArrayRule(outputSection,ctx),ExplicitBooleanRule(outputSection,ctx)));Emit(outputSection,ctx,indent,node);return;
    case NodeKind.Call:CallStatement(op.Value,ctx,indent,node);return;
    case NodeKind.OpenFile:case NodeKind.CloseFile:ctx.Flag("read",op.FileReading).Flag("write",!op.FileReading);if(op.Value!=null){var path=Expression(op.Value);path=Prepared("open","prepare",path,ctx);ctx.Put("expression",path.Code);}Emit(node.Kind==NodeKind.OpenFile?"open":"close",ctx,indent,node);return;
    case NodeKind.TurtleForward:case NodeKind.TurtleTurn:ctx.Flag("down",op.TurtleDraw).Flag("up",!op.TurtleDraw).Flag("left",op.TurtleLeft).Flag("right",!op.TurtleLeft).Put("expression",Expression(op.Value).Code);Emit(node.Kind==NodeKind.TurtleForward?"forward":"turn",ctx,indent,node);return;
    case NodeKind.TurtleHome:case NodeKind.TurtleClear:Emit(node.Kind==NodeKind.TurtleHome?"home":"clear",ctx,indent,node);return;
    case NodeKind.TurtlePen:Pen(op.Value,ctx,indent,node);return;
    case NodeKind.Pause:Comment("Punto di pausa: "+node.Text,node,indent);return;
    default:throw Fail("Questo tipo di blocco non è rappresentato dal modello.");
   }}
   B ArrayDeclaration(FlowFpgtTypes.Symbol symbol){
    string key=FlowFpgtTypes.Key(current.Source.Id,symbol.Name);B found;if(arrayDeclarations.TryGetValue(key,out found))return found;
    foreach(var node in current.Source.Blocks){Tick();var d=node.Operation.Declaration;if(d==null||!d.Names.Any(name=>name.Equals(symbol.Name,StringComparison.OrdinalIgnoreCase)))continue;if(found!=null){arrayDeclarations[key]=null;return null;}found=node;}
    if(found!=null&&!found.Operation.Declaration.IsArray)found=null;arrayDeclarations[key]=found;return found;
   }
   string ArrayShape(FlowFpgtTypes.Symbol symbol,HashSet<string> pending){
    Tick();if(symbol==null||!symbol.Type.Array)return null;string key=FlowFpgtTypes.Key(current.Source.Id,symbol.Name),shape;if(arrayShapes.TryGetValue(key,out shape))return shape;
    if(pending.Count>=64||!pending.Add(key))return null;
    try{
     if(symbol.Parameter){shape=!symbol.Reference&&current.Source.Blocks.Any(b=>b.Operation.Writes.Any(w=>w.Target.Index==null&&w.Target.Name.Equals(symbol.Name,StringComparison.OrdinalIgnoreCase)))?null:"parameter:"+key;}
     else{var declaration=ArrayDeclaration(symbol);shape=null;if(declaration!=null){var size=declaration.Operation.Declaration.Size;
      if(size.Kind==FlowSource.ExpressionKind.Number){if(size.Number>=0&&size.Number<=1024&&size.Number==Math.Truncate(size.Number))shape="length:"+size.Number.ToString(CultureInfo.InvariantCulture);}
      else{shape="declaration:"+(current.Source.Id??"")+":"+declaration.Id;
       if(size.Kind==FlowSource.ExpressionKind.Call&&size.FunctionId==null&&size.Text=="size"&&size.Children.Count==1&&size.Children[0].Kind==FlowSource.ExpressionKind.Variable){var other=current.Find(size.Children[0].Text);if(other!=null&&other.Type.Array&&(other.Parameter||ArrayDeclaration(other)!=null))shape=ArrayShape(other,pending);}
      }
     }}
     arrayShapes[key]=shape;return shape;
    }finally{pending.Remove(key);}
   }
   void Write(FlowSource.Write write,Context context,int indent,B node){
    var target=current.Find(write.Target.Name);if(write.Target.Index==null&&target.Type.Array){CopyArray(write,target,context,indent,node);return;}
    var value=Expression(write.Value);if(value.Type.Array)throw Fail("La destinazione non può contenere un vettore. Per le copie vettoriali avanzate scegli un generatore integrato.");
    string[] snapshots;if(!writeSnapshots.TryGetValue(write,out snapshots)){Assign(Target(write.Target),value,context,indent,node);return;}
    // Frociart evaluates the value before the target index. Keep both snapshots
    // local to the function call, so aliases and recursive calls cannot reorder them.
    var saved=new Rendered(Var(snapshots[0]),current.Find(snapshots[0]).Type,Int32.MaxValue,true);
    var index=new Rendered(Var(snapshots[1]),current.Find(snapshots[1]).Type,Int32.MaxValue,true);
    Assign(saved,value,context.Copy().Flag("last",false),indent,node);
    Assign(index,Expression(write.Target.Index),context.Copy().Flag("first",false).Flag("last",false),indent,node);
    var subscript=new E(FlowSource.ExpressionKind.Variable,snapshots[1],0,false,null,null);
    Assign(Variable(target,subscript),saved,context.Copy().Flag("first",false),indent,node);
   }
   void CopyArray(FlowSource.Write write,FlowFpgtTypes.Symbol target,Context context,int indent,B node){
    ArrayTransfer transfer;if(arrayTransfers.TryGetValue(write,out transfer)){TransferArray(write,target,transfer,context,indent,node);return;}
    // A language-level array assignment can alias its source. Copy only with a proven
    // stable length, using ordinary scalar assignments and the model's own casts.
    if(write.Value.Kind==FlowSource.ExpressionKind.Vector)Expression(write.Value);
    var source=write.Value.Kind==FlowSource.ExpressionKind.Variable?current.Find(write.Value.Text):null;
    string targetShape=ArrayShape(target,new HashSet<string>(StringComparer.Ordinal));string sourceShape=ArrayShape(source,new HashSet<string>(StringComparer.Ordinal));
    if(ArrayDeclaration(target)==null||targetShape==null||sourceShape==null||targetShape!=sourceShape){if(HasArrayCopy&&target.Declared&&!HasArrayMismatch)throw Fail("Il modello non definisce [Array Mismatch] per controllare la dimensione del vettore.");if(!HasArrayCopy)throw Fail("Il modello non definisce [Array Copy] per trasferire i valori vettoriali.");throw Fail("Per copiare il vettore con questo modello, dichiarali insieme, con la stessa dimensione costante oppure usando size(vettore). Per gli altri casi scegli un generatore integrato.");}
    if(target.Type.Kind!=source.Type.Kind&&!((target.Type.Kind==1||target.Type.Kind==2)&&(source.Type.Kind==1||source.Type.Kind==2)))throw Fail("La copia richiede vettori con tipi di elementi compatibili.");
    string indexName=arrayIndexNames[current.Source.Id??""];var index=new Rendered(Var(indexName),new T(1),Int32.MaxValue,true);Assign(index,Numeric(0),context.Copy().Flag("last",false),indent,node);
    var condition=Operator("<",index,Intrinsic("size",new[]{Variable(target,null)},new T(1)),new T(4));
    Emit("while",C().Flag("block").Flag("last",context.Flags.Contains("last")).Put("condition",condition.Code).Put("not condition",Operator("not",condition,null,new T(4)).Code),indent,node,(marker,level)=>{
     if(marker!="-->block")throw Fail("Marcatore non valido nella copia del vettore.");var subscript=new E(FlowSource.ExpressionKind.Variable,indexName,0,false,null,null);
     Assign(Variable(target,subscript),Variable(source,subscript),C().Flag("first"),level,node);Assign(index,Operator("+",index,Numeric(1),new T(1)),C().Flag("last"),level,node);
    });
   }
   void TransferArray(FlowSource.Write write,FlowFpgtTypes.Symbol target,ArrayTransfer transfer,Context context,int indent,B node){
    if(target.Type.Kind!=transfer.Type.Kind&&!((target.Type.Kind==1||target.Type.Kind==2)&&(transfer.Type.Kind==1||transfer.Type.Kind==2)))throw Fail("La copia richiede vettori con tipi di elementi compatibili.");
    var value=write.Value.Kind==FlowSource.ExpressionKind.Vector?VectorLiteral(write.Value,transfer.Type):Expression(write.Value);
    Declare(new[]{transfer.Value},transfer.Type,new E(FlowSource.ExpressionKind.Number,null,0,false,null,null),indent,node,false);
    var snapshot=Variable(current.Find(transfer.Value),null);Assign(snapshot,ArrayArgument(value,transfer.Type,false),context.Copy().Flag("last",false),indent,node);
    if(target.Declared){var mismatch=Operator("!=",Intrinsic("size",new[]{snapshot},new T(1)),Intrinsic("size",new[]{Variable(target,null)},new T(1)),new T(4));Emit("if",C().Flag("block").Put("condition",mismatch.Code).Put("not condition",Operator("not",mismatch,null,new T(4)).Code),indent,node,(marker,level)=>{if(marker=="-->falseblock")return;if(marker!="-->trueblock"&&marker!="-->block")throw Fail("Marcatore non valido nel controllo della dimensione del vettore.");ArrayMismatch(level,node);});}
    Rendered result=snapshot;
    if(transfer.Converted!=null){
     var size=new E(FlowSource.ExpressionKind.Call,"size",0,false,null,new[]{new E(FlowSource.ExpressionKind.Variable,transfer.Value,0,false,null,null)});
     Declare(new[]{transfer.Converted},target.Type,size,indent,node,false);result=Variable(current.Find(transfer.Converted),null);var index=Variable(current.Find(transfer.Index),null);Assign(index,Numeric(0),C(),indent,node);var condition=Operator("<",index,Intrinsic("size",new[]{snapshot},new T(1)),new T(4));
     Emit("while",C().Flag("block").Put("condition",condition.Code).Put("not condition",Operator("not",condition,null,new T(4)).Code),indent,node,(marker,level)=>{if(marker!="-->block")throw Fail("Marcatore non valido nella conversione del vettore.");var subscript=new E(FlowSource.ExpressionKind.Variable,transfer.Index,0,false,null,null);Assign(Variable(current.Find(transfer.Converted),subscript),Variable(current.Find(transfer.Value),subscript),C().Flag("first"),level,node);Assign(index,Operator("+",index,Numeric(1),new T(1)),C().Flag("last"),level,node);});
    }
    Assign(Variable(target,null),result,context.Copy().Flag("first",false),indent,node);compatibility.NoticeVectorValues();
   }
   void ArrayMismatch(int indent,B node){
    var message=StringLiteral("La dimensione del vettore dichiarato non può cambiare.");var ctx=C().Put("message",message.Code);
    if(model.Find("array mismatch")!=null)Emit("array mismatch",ctx,indent,node);
    else if(compatibility.ArrayCopyLanguage=="python")Add("raise ValueError("+message.Code+")",indent,node);
    else if(compatibility.ArrayCopyLanguage=="java")Add("throw new java.lang.IllegalArgumentException("+message.Code+");",indent,node);
    else if(compatibility.ArrayCopyLanguage=="csharp")Add("throw new global::System.InvalidOperationException("+message.Code+");",indent,node);
    else throw Fail("Il modello non definisce [Array Mismatch] per controllare la dimensione del vettore.");
   }
   void Assign(Rendered target,Rendered value,Context context,int indent,B node){var ctx=context.Copy().Type(target.Type).Type(target.Type,"-variable").Type(value.Type,"-expression").Flag("array",target.Subscript!=null).Put("variable",target.Code).Put("name",target.Name??target.Code).Put("subscript",target.Subscript??"").Put("type",TypeName(target.Type));string cast=Value("assign","cast",ctx,"");if(cast.Length>0)value=Cast(value,Kind(cast),ctx);else if(target.Type.Numeric&&value.Type.Numeric&&target.Type.Kind!=value.Type.Kind)value=Cast(value,target.Type,ctx);value=Prepared("assign","prepare",value,ctx);ctx.Put("expression",Need(value,Number("assign","needed",ctx,0)));Emit("assign",ctx,indent,node);}
   string ColorHex(string value){try{return FlowTurtle.ParseColor(value);}catch(FlowException error){throw Fail(error.Message);}}
   void Pen(E expression,Context ctx,int indent,B node){string snapshot;if(penSnapshots.TryGetValue(node,out snapshot)){var value=Expression(expression);var target=new Rendered(Var(snapshot),new T(3),Int32.MaxValue,true);var normalized=new Rendered(compatibility.PenColor(Need(value,0)),new T(3),Int32.MaxValue);Assign(target,normalized,ctx.Copy().Flag("last",false),indent,node);ctx=ctx.Copy().Flag("computed-color",expression.Kind!=FlowSource.ExpressionKind.Text).Put("expression",target.Code);if(expression.Kind==FlowSource.ExpressionKind.Text)ctx.Put("color",ColorHex(expression.Text));if(compatibility.StockPen)Add("draw.pencolor("+target.Code+")",indent,node);else Emit("pen",ctx,indent,node);return;}
    if(expression.Kind!=FlowSource.ExpressionKind.Text)throw Fail("Questo modello non descrive colori calcolati: definisci [Pen] Text con {Expression}, oppure scegli un generatore integrato.");string text=expression.Text,hex=ColorHex(text),color=null;foreach(var item in FlowTurtle.Colors)if(text.Equals(item[0],StringComparison.OrdinalIgnoreCase)||text.Equals(item[1],StringComparison.OrdinalIgnoreCase)||hex.Equals(item[2],StringComparison.OrdinalIgnoreCase)){color=item[1];break;}if(color!=null)ctx.Flag(color);ctx.Put("color",color??hex).Put("expression",Expression(expression).Code);Emit("pen",ctx,indent,node);}
   void Dispatch(S scope,int indent){
    var names=dispatchNames[scope.Source.Id??""];var nodes=scope.Source.Blocks.ToList();var ids=nodes.Select((n,i)=>new{n.Id,i}).ToDictionary(x=>x.Id,x=>x.i,StringComparer.Ordinal);
    var pc=new Rendered(Var(names[0]),new T(1),Int32.MaxValue,true);var visit=new Rendered(Var(names[1]),new T(1),Int32.MaxValue,true);Assign(pc,Numeric(ids[scope.Source.StartId]),C(),indent,scope.Source.Find(scope.Source.StartId));var condition=Operator(">=",pc,Numeric(0),new T(4));
    Emit("while",C().Flag("block").Put("condition",condition.Code).Put("not condition",Operator("not",condition,null,new T(4)).Code),indent,scope.Source.Find(scope.Source.StartId),(marker,level)=>{
     if(marker!="-->block")throw Fail("Marcatore non valido nel ciclo del grafo.");Assign(visit,pc,C(),level,scope.Source.Find(scope.Source.StartId));
     for(int at=0;at<nodes.Count;at++){var node=nodes[at];block=node;var test=Operator("=",visit,Numeric(at),new T(4));var ctx=C().Flag("block").Flag("last",at==nodes.Count-1).Put("condition",test.Code).Put("not condition",Operator("not",test,null,new T(4)).Code);
      Emit("if",ctx,level,node,(part,inside)=>{if(part=="-->falseblock")return;if(part!="-->trueblock")throw Fail("Marcatore non valido nella selezione del grafo.");block=node;
       if(node.Kind==NodeKind.Decision){Comment(node.Comment,node,inside);var branch=Truth(Expression(node.Operation.Value));Emit("if",C().Flag("block").Flag("else").Flag("last").Put("condition",branch.Code).Put("not condition",Operator("not",branch,null,new T(4)).Code),inside,node,(which,body)=>{string edge=which=="-->trueblock"?"true":which=="-->falseblock"?"false":null;if(edge==null)throw Fail("Marcatore non valido nella decisione.");Assign(pc,Numeric(ids[node.Next(edge)]),C().Flag("last"),body,node);});}
       else{Instruction(node,C().Flag("first"),inside);Assign(pc,Numeric(node.Kind==NodeKind.End?-1:ids[node.Next("next")]),C().Flag("last"),inside,node);}
      });
     }
    });
   }
  }
 }
}

