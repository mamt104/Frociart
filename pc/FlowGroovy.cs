using System;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
namespace Frociart {
 // Share the typed JVM backend; lower its lexical differences without touching user literals.
 // This generates Groovy instructions, not a diagram interpreter or a compiler invocation.
 internal static class FlowGroovy {
  internal static string Literal(string text){return new Lowering().Line(FlowJava.Literal(text));}
  internal static string TemplateNumber(double number){return new Lowering().Line(FlowJava.TemplateNumber(number));}
  internal static FlowSourceText Generate(FlowSource.Program program){return Convert(program,FlowJava.Generate(program),"Programma");}
  internal static FlowSourceText Generate(FlowSource.Program program,string runtime,string className="Programma"){return Convert(program,FlowJava.Generate(program,runtime,className),className);}
  private static FlowSourceText Convert(FlowSource.Program program,FlowSourceText source,string className){
   var output=new FlowSourceText.Builder(program,"groovy");var lexer=new Lowering(source.Text);
   foreach(var line in source.Lines){string text=source.Text.Substring(line.Start,line.Length);
    if(text.StartsWith("// Java 8+. Salva come ",StringComparison.Ordinal))text="// Groovy 5+, Java 17+. Salva "+className+".groovy; esegui con groovy "+className+".groovy.";
    if(text=="public final class "+className+" {")output.AddLine("@groovy.transform.CompileStatic",0);
    output.AddLine(lexer.Line(text),0,line.Origins.ToArray());
   }
   return output.Build();
  }
  private sealed class Lowering {
   bool comment;int arrayDepth;
   readonly System.Collections.Generic.Dictionary<string,string> names=new System.Collections.Generic.Dictionary<string,string>(StringComparer.Ordinal);
   internal Lowering(string source=""){var used=new System.Collections.Generic.HashSet<string>(Regex.Matches(source,@"[A-Za-z_$][A-Za-z_0-9$]*").Cast<Match>().Select(m=>m.Value),StringComparer.Ordinal);foreach(string keyword in Keywords){string name="flowJava_"+keyword;int suffix=2;while(!used.Add(name))name="flowJava_"+keyword+(suffix++).ToString(CultureInfo.InvariantCulture);names.Add(keyword,name);}}

   static readonly Regex ArrayStart=new Regex(@"\w+(?:\[\])+\s+\w+\s*=\s*$",RegexOptions.CultureInvariant);
   static readonly string[] Keywords={"as","def","in","trait","metaClass","binding","delegate","owner","it"};
   internal string Line(string source){
    // Give SAM references explicit target types: Groovy does not infer them through ?:.
    // These are exact helper statements, never replacements inside project strings or comments.
    if(source.TrimStart().StartsWith("this.reader=",StringComparison.Ordinal))source=source.Replace("reader==null?this::consoleRead:reader","reader==null?(Reader)(this::consoleRead):reader").Replace("writer==null?System.out::println:writer","writer==null?(java.util.function.Consumer<String>)(System.out::println):writer");
    if(source.TrimStart().StartsWith("this.fileReader=",StringComparison.Ordinal))source=source.Replace("fileReader==null?this::readPath:fileReader","fileReader==null?(FileReader)(this::readPath):fileReader").Replace("fileSaver==null?this::savePath:fileSaver","fileSaver==null?(FileSaver)(this::savePath):fileSaver");
    var result=new StringBuilder();int i=0;
    while(i<source.Length){
     if(comment){int end=source.IndexOf("*/",i,StringComparison.Ordinal);if(end<0){result.Append(source,i,source.Length-i);break;}result.Append(source,i,end+2-i);i=end+2;comment=false;continue;}
     if(i+1<source.Length&&source[i]=='/'&&source[i+1]=='/'){result.Append(source,i,source.Length-i);break;}
     if(i+1<source.Length&&source[i]=='/'&&source[i+1]=='*'){result.Append("/*");i+=2;comment=true;continue;}
     char c=source[i];
     if(c=='"'){result.Append(c);i++;while(i<source.Length){c=source[i++];if(c=='\\'){if(i>=source.Length)throw new InvalidOperationException("Stringa JVM incompleta.");result.Append(c).Append(source[i++]);continue;}if(c=='$')result.Append('\\');result.Append(c);if(c=='"')break;}continue;}
     if(c=='\''){int start=++i;while(i<source.Length){if(source[i]=='\\'){i+=2;continue;}if(source[i]=='\'')break;i++;}if(i>=source.Length)throw new InvalidOperationException("Carattere JVM incompleto.");int value=Character(source.Substring(start,i-start));result.Append("((char)").Append(value.ToString(CultureInfo.InvariantCulture)).Append(')');i++;continue;}
     if(Char.IsLetter(c)||c=='_'||c=='$'){int start=i++;while(i<source.Length&&(Char.IsLetterOrDigit(source[i])||source[i]=='_'||source[i]=='$'))i++;string word=source.Substring(start,i-start);
      // Java permits enclosing classes to access nested private helpers. Expose that
      // generated support explicitly for Groovy's static compiler; no app API is involved.
      int following=i;while(following<source.Length&&Char.IsWhiteSpace(source[following]))following++;
      if(word=="raw"&&(start==0||source[start-1]!='.')&&source.Substring(following).StartsWith("instanceof",StringComparison.Ordinal))result.Append("((Object)raw)");else if(word=="private")result.Append("public");else if(Keywords.Contains(word)&&!(word=="in"&&start>=7&&source.Substring(start-7,7)=="System."))result.Append(names[word]);else result.Append(word);continue;
     }
     if(c=='{'&&(arrayDepth>0||ArrayStart.IsMatch(source.Substring(Math.Max(0,i-120),Math.Min(i,120))))){arrayDepth++;result.Append('[');i++;continue;}
     if(c=='}'&&arrayDepth>0){arrayDepth--;result.Append(']');i++;continue;}
     if(c=='.'&&i+1<source.Length&&source[i+1]>='0'&&source[i+1]<='9'&&(i==0||!Char.IsDigit(source[i-1]))){result.Append("0.");i++;continue;}
     result.Append(c);i++;
     if(c=='}'){int next=i;while(next<source.Length&&(source[next]==' '||source[next]=='\t'))next++;int end=next;while(end<source.Length&&(Char.IsLetterOrDigit(source[end])||source[end]=='_'))end++;if(end>next&&!new[]{"else","catch","finally","while"}.Contains(source.Substring(next,end-next)))result.Append(';');}
    }
    return result.ToString();
   }
   static int Character(string token){if(token.Length==1)return token[0];if(token.Length<2||token[0]!='\\')throw new InvalidOperationException("Carattere JVM non valido.");switch(token[1]){case 'n':return 10;case 'r':return 13;case 't':return 9;case 'b':return 8;case 'f':return 12;case '\\':return 92;case '\'':return 39;case '"':return 34;case 'u':int i=1;while(i<token.Length&&token[i]=='u')i++;return Int32.Parse(token.Substring(i),NumberStyles.HexNumber,CultureInfo.InvariantCulture);default:return System.Convert.ToInt32(token.Substring(1),8);}}
  }
 }
}
