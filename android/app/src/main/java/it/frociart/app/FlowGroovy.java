package it.frociart.app;
import java.util.*;
import java.util.regex.*;
/** Static Groovy lowering of the shared JVM backend. No compiler or interpreter is bundled. */
final class FlowGroovy {
 private FlowGroovy(){}
 static String literal(String text){return new Lowering().line(FlowJava.literal(text));}
 static String templateNumber(double number){return new Lowering().line(FlowJava.templateNumber(number));}
 static FlowSourceText generate(FlowSource.Program program,String runtime){return generate(program,runtime,"Programma");}
 static FlowSourceText generate(FlowSource.Program program,String runtime,String className){
  FlowSourceText source=FlowJava.generate(program,runtime,className);FlowSourceText.Builder output=new FlowSourceText.Builder(program,"groovy");Lowering lexer=new Lowering(source.text);
  for(FlowSourceText.Line line:source.lines){String text=source.text.substring(line.start,line.start+line.length);
   if(text.startsWith("// Java 8+. Salva come "))text="// Groovy 5+, Java 17+. Salva "+className+".groovy; esegui con groovy "+className+".groovy.";
   if(text.equals("public final class "+className+" {"))output.addLine("@groovy.transform.CompileStatic",0);
   output.addLine(lexer.line(text),0,line.origins.toArray(new FlowSourceText.Origin[0]));
  }return output.build();
 }
 private static final class Lowering {
  boolean comment;int arrayDepth;
  final Map<String,String> names=new HashMap<>();
  Lowering(){this("");}
  Lowering(String source){Set<String> used=new HashSet<>();Matcher matcher=Pattern.compile("[A-Za-z_$][A-Za-z_0-9$]*").matcher(source);while(matcher.find())used.add(matcher.group());for(String keyword:Arrays.asList("as","def","in","trait","metaClass","binding","delegate","owner","it")){String name="flowJava_"+keyword;int suffix=2;while(!used.add(name))name="flowJava_"+keyword+(suffix++);names.put(keyword,name);}}

  static final Pattern ARRAY_START=Pattern.compile("\\w+(?:\\[\\])+\\s+\\w+\\s*=\\s*$");
  static final Set<String> KEYWORDS=new HashSet<>(Arrays.asList("as","def","in","trait","metaClass","binding","delegate","owner","it"));
  String line(String source){
   if(source.trim().startsWith("this.reader="))source=source.replace("reader==null?this::consoleRead:reader","reader==null?(Reader)(this::consoleRead):reader").replace("writer==null?System.out::println:writer","writer==null?(java.util.function.Consumer<String>)(System.out::println):writer");
   if(source.trim().startsWith("this.fileReader="))source=source.replace("fileReader==null?this::readPath:fileReader","fileReader==null?(FileReader)(this::readPath):fileReader").replace("fileSaver==null?this::savePath:fileSaver","fileSaver==null?(FileSaver)(this::savePath):fileSaver");
   StringBuilder result=new StringBuilder();int i=0;
   while(i<source.length()){
    if(comment){int end=source.indexOf("*/",i);if(end<0){result.append(source.substring(i));break;}result.append(source,i,end+2);i=end+2;comment=false;continue;}
    if(source.startsWith("//",i)){result.append(source.substring(i));break;}
    if(source.startsWith("/*",i)){result.append("/*");i+=2;comment=true;continue;}
    char c=source.charAt(i);
    if(c=='"'){result.append(c);i++;while(i<source.length()){c=source.charAt(i++);if(c=='\\'){if(i>=source.length())throw new IllegalStateException("Stringa JVM incompleta.");result.append(c).append(source.charAt(i++));continue;}if(c=='$')result.append('\\');result.append(c);if(c=='"')break;}continue;}
    if(c=='\''){int start=++i;while(i<source.length()){if(source.charAt(i)=='\\'){i+=2;continue;}if(source.charAt(i)=='\'')break;i++;}if(i>=source.length())throw new IllegalStateException("Carattere JVM incompleto.");result.append("((char)").append(character(source.substring(start,i))).append(')');i++;continue;}
    if(Character.isLetter(c)||c=='_'||c=='$'){int start=i++;while(i<source.length()&&(Character.isLetterOrDigit(source.charAt(i))||source.charAt(i)=='_'||source.charAt(i)=='$'))i++;String word=source.substring(start,i);
     int following=i;while(following<source.length()&&Character.isWhitespace(source.charAt(following)))following++;
     if(word.equals("raw")&&(start==0||source.charAt(start-1)!='.')&&source.startsWith("instanceof",following))result.append("((Object)raw)");else if(word.equals("private"))result.append("public");else if(KEYWORDS.contains(word)&&!(word.equals("in")&&start>=7&&source.substring(start-7,start).equals("System.")))result.append(names.get(word));else result.append(word);continue;
    }
    if(c=='{'&&(arrayDepth>0||ARRAY_START.matcher(source.substring(Math.max(0,i-120),i)).find())){arrayDepth++;result.append('[');i++;continue;}
    if(c=='}'&&arrayDepth>0){arrayDepth--;result.append(']');i++;continue;}
    if(c=='.'&&i+1<source.length()&&source.charAt(i+1)>='0'&&source.charAt(i+1)<='9'&&(i==0||!Character.isDigit(source.charAt(i-1)))){result.append("0.");i++;continue;}
    result.append(c);i++;
    if(c=='}'){int next=i;while(next<source.length()&&(source.charAt(next)==' '||source.charAt(next)=='\t'))next++;int end=next;while(end<source.length()&&(Character.isLetterOrDigit(source.charAt(end))||source.charAt(end)=='_'))end++;if(end>next&&!Arrays.asList("else","catch","finally","while").contains(source.substring(next,end)))result.append(';');}
   }return result.toString();
  }
  static int character(String token){if(token.length()==1)return token.charAt(0);if(token.length()<2||token.charAt(0)!='\\')throw new IllegalStateException("Carattere JVM non valido.");switch(token.charAt(1)){case 'n':return 10;case 'r':return 13;case 't':return 9;case 'b':return 8;case 'f':return 12;case '\\':return 92;case '\'':return 39;case '"':return 34;case 'u':int i=1;while(i<token.length()&&token.charAt(i)=='u')i++;return Integer.parseInt(token.substring(i),16);default:return Integer.parseInt(token.substring(1),8);}}
 }
}
