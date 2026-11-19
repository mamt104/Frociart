package it.frociart.app;

import java.io.*;
import java.util.*;
import java.util.regex.*;
import javax.xml.parsers.*;
import org.xml.sax.*;
import org.xml.sax.helpers.DefaultHandler;
import static it.frociart.app.FlowModel.*;

/** Bounded, offline .fprg adapter. Imported XML is never executable. */
public final class FlowgorithmImport {
 private FlowgorithmImport(){}
 public static final String HELP="Apri anche progetti Flowgorithm (.fprg). Dichiarazioni, vettori, assegnazioni, input, output con o senza a capo, decisioni, cicli, funzioni con parametri scalari e vettoriali per riferimento, file di testo e tartaruga. Commenti convertiti in note libere sul foglio. I canali dei file devono essere read o write; in esecuzione scegli i documenti da leggere o salvare. I punti di interruzione diventano blocchi Pausa con la condizione originale; non possono chiamare funzioni del progetto o random. I parametri vettoriali modificano gli elementi del vettore originale, con lo stesso tipo e dimensione. I costrutti non supportati vengono rifiutati con una spiegazione, mai importati in parte. L'originale resta intatto; viene salvata una copia Frociart.";
 public static String convertExpression(String raw){return convertExpression(raw,null);}
 // Parse only: user functions, random and file operations are never executed by the preview.
 static String convertExpression(String raw,Map<String,Function> functions){if(raw!=null&&raw.length()>8192)throw new IllegalArgumentException("L’espressione supera 8192 caratteri. Riducila prima di inserirla.");String result=new ExpressionReader(raw).read();FlowEngine.checkExpression(result,functions);return result;}
 private static final class Element {
  String name;int line;final Map<String,String> attr=new LinkedHashMap<>();final List<Element> children=new ArrayList<>();
  String get(String key){return get(key,"");}String get(String key,String fallback){return attr.containsKey(key)?attr.get(key):fallback;}
 }
 private static IllegalArgumentException error(Element e,String detail){return new IllegalArgumentException("Flowgorithm"+(e==null?"":", riga "+e.line+" (<"+e.name+">)")+": "+detail);}
 public static Document decode(String text){
  if(text==null||text.isEmpty()||text.length()>MAX_BYTES)throw FlowgorithmImport.error(null,"Il file è vuoto o troppo grande.");if(text.charAt(0)=='\uFEFF')text=text.substring(1);
  // Required even on Android parsers lacking the optional disallow-doctype feature.
  // String input is already decoded UTF-8; a declaration cannot bypass this check with another encoding.
  if(Pattern.compile("<!\\s*(DOCTYPE|ENTITY)",Pattern.CASE_INSENSITIVE).matcher(text).find())throw FlowgorithmImport.error(null,"DTD ed entità XML non sono consentite.");
  final Element[] root={null};final ArrayDeque<Element> stack=new ArrayDeque<>();final int[] count={0};
  try{
   SAXParserFactory factory=SAXParserFactory.newInstance();factory.setNamespaceAware(true);SAXParser parser=factory.newSAXParser();XMLReader xml=parser.getXMLReader();
   for(String feature:new String[]{"http://xml.org/sax/features/external-general-entities","http://xml.org/sax/features/external-parameter-entities","http://apache.org/xml/features/nonvalidating/load-external-dtd"})try{xml.setFeature(feature,false);}catch(SAXNotRecognizedException|SAXNotSupportedException optional){/* Lexical DTD ban and resolver are mandatory guards. */}
   DefaultHandler handler=new DefaultHandler(){Locator locator;
    public void setDocumentLocator(Locator l){locator=l;}
    public void startPrefixMapping(String prefix,String uri){throw FlowgorithmImport.error(null,"I namespace XML non sono supportati.");}
    public InputSource resolveEntity(String publicId,String systemId)throws SAXException{throw new SAXException("Entità esterne non consentite.");}
    public void startElement(String uri,String local,String q,Attributes a)throws SAXException{
     if(++count[0]>6000||stack.size()>51||a.getLength()>16||q.length()>80||!uri.isEmpty()||q.indexOf(':')>=0)throw FlowgorithmImport.error(null,"Struttura XML troppo complessa o non supportata.");
     Element e=new Element();e.name=q;e.line=locator==null?1:locator.getLineNumber();for(int i=0;i<a.getLength();i++){if(a.getValue(i).length()>8192||a.getQName(i).length()>80||!a.getURI(i).isEmpty())throw FlowgorithmImport.error(e,"Attributo troppo lungo o non supportato.");e.attr.put(a.getQName(i),a.getValue(i));}
     if(stack.isEmpty()){if(root[0]!=null)throw FlowgorithmImport.error(e,"Più documenti nello stesso file.");root[0]=e;}else stack.peek().children.add(e);stack.push(e);
    }
    public void endElement(String uri,String local,String q){stack.pop();}
    public void characters(char[] data,int start,int length){for(int i=start;i<start+length;i++)if(!Character.isWhitespace(data[i]))throw FlowgorithmImport.error(null,"Il contenuto XML non è un progetto Flowgorithm valido.");}
    public void error(SAXParseException e)throws SAXException{throw e;}public void fatalError(SAXParseException e)throws SAXException{throw e;}
   };
   xml.setContentHandler(handler);xml.setEntityResolver(handler);xml.setErrorHandler(handler);xml.parse(new InputSource(new StringReader(text)));
  }catch(SAXParseException e){throw new IllegalArgumentException("Flowgorithm, riga "+e.getLineNumber()+": XML incompleto o non valido.");}catch(ParserConfigurationException|SAXException|IOException e){throw FlowgorithmImport.error(null,"XML non valido o non supportato.");}
  Element r=root[0];if(r==null||!r.name.equals("flowgorithm"))throw FlowgorithmImport.error(r,"Scegli un progetto Flowgorithm .fprg.");attributes(r,"fileversion");if(!r.get("fileversion").matches("[1-4]\\.[0-9]{1,2}"))throw FlowgorithmImport.error(r,"Versione del formato non supportata (sono riconosciuti i formati da 1.x a 4.x).");return new Writer().read(r);
 }
 private static void attributes(Element e,String... allowed){List<String> names=Arrays.asList(allowed);for(String key:e.attr.keySet())if(!names.contains(key))throw FlowgorithmImport.error(e,"Attributo «"+key+"» non supportato. Il progetto non è stato modificato.");}
 private static Element child(Element e,String name){Element result=null;for(Element c:e.children)if(c.name.equals(name)){if(result!=null)throw FlowgorithmImport.error(e,"Serve un solo elemento <"+name+">.");result=c;}if(result==null)throw FlowgorithmImport.error(e,"Serve un solo elemento <"+name+">.");return result;}
 private static boolean flag(Element e,String key,boolean fallback){String s=e.get(key,fallback?"true":"false");if(s.equalsIgnoreCase("true"))return true;if(s.equalsIgnoreCase("false"))return false;throw FlowgorithmImport.error(e,"«"+key+"» deve essere true o false.");}
 private static String name(Element e,String value){if(!FlowEngine.identifier(value))throw FlowgorithmImport.error(e,"Nome non compatibile. Usa lettere, numeri e underscore: inizia con una lettera o underscore, massimo 64 caratteri.");return value;}
 private static String type(Element e,String value,boolean allowVoid){switch(value.toLowerCase(Locale.ROOT)){case "integer":return "INTERO";case "real":return "REALE";case "string":return "TESTO";case "boolean":return "LOGICO";case "none":if(allowVoid)return "NESSUNO";}throw FlowgorithmImport.error(e,"Tipo di dato non supportato.");}
 private static String literal(String s){return "\""+s.replace("\\","\\\\").replace("\"","\\\"").replace("\r","\\r").replace("\n","\\n").replace("\t","\\t")+"\"";}
 private static String choice(Element e,String key,String fallback,String...allowed){String value=e.get(key,fallback);if(!Arrays.asList(allowed).contains(value))throw FlowgorithmImport.error(e,"«"+key+"» deve essere "+String.join(" oppure ",allowed)+".");return value;}
 // Canonical ten-colour palette, verified against the installed Flowgorithm 4.5 data model.
 static final String[] PEN_NAMES={"black","gray","red","orange","yellow","green","teal","blue","violet","brown"},PEN_COLORS={"#101010","#808080","#E02030","#F07020","#F0C020","#30C080","#30C0E0","#2080E0","#A060C0","#C06C40"},PEN_LABELS={"Nero","Grigio","Rosso","Arancione","Giallo","Verde","Turchese","Blu","Viola","Marrone"};
 private static String pen(Element e){int at=Arrays.asList(PEN_NAMES).indexOf(e.get("color"));if(at<0)throw FlowgorithmImport.error(e,"Colore della penna non riconosciuto.");return literal(PEN_COLORS[at]);}
 private static final class Writer {
  final StringBuilder code=new StringBuilder();final List<Element> origins=new ArrayList<>();String trailingNotes="";boolean hasTurtle;
  static boolean hasTurtle(Element e){if(Arrays.asList("forward","turn","home","clear","pen").contains(e.name))return true;for(Element c:e.children)if(hasTurtle(c))return true;return false;}
  void line(Element e,String s){code.append(s).append('\n');origins.add(e);if(code.length()>FlowPseudocode.MAX_TEXT||origins.size()>FlowPseudocode.MAX_LINES)throw FlowgorithmImport.error(e,"Il progetto supera i limiti di importazione.");}
  Document read(Element root){List<Element> functions=new ArrayList<>();Element main=null;int metadata=0;for(Element e:root.children){if(e.name.equals("attributes")){if(++metadata>1)throw FlowgorithmImport.error(e,"Metadati duplicati.");continue;}if(!e.name.equals("function"))throw FlowgorithmImport.error(e,"Elemento non supportato.");functions.add(e);if(e.get("name").equalsIgnoreCase("Main")){if(main!=null)throw FlowgorithmImport.error(root,"Serve un solo programma Main.");main=e;}}
   if(functions.size()>33)throw FlowgorithmImport.error(root,"Massimo 32 funzioni oltre al programma principale.");if(main==null)throw FlowgorithmImport.error(root,"Serve un solo programma Main.");for(Element f:functions)hasTurtle|=hasTurtle(f);line(root,"VARIABILI DICHIARATE");for(Element f:functions)if(f!=main)function(f,false);function(main,true);
   Document d;try{d=FlowPseudocode.decode(code.toString());}catch(FlowPseudocode.ParseError e){throw FlowgorithmImport.error(origins.get(Math.max(0,Math.min(origins.size()-1,e.line-1))),e.getMessage().replaceFirst("^Riga [0-9]+: ",""));}
   d.title="Da Flowgorithm";FlowAutoLayout.apply(d);FlowAnnotations.placeImported(d);if(d.functions!=null)for(Function f:d.functions){FlowAutoLayout.apply(f.body);FlowAnnotations.placeImported(f.body);}FlowCodec.check(d);return d;
  }
  void function(Element f,boolean main){attributes(f,"name","type","variable");for(Element c:f.children)if(!c.name.equals("parameters")&&!c.name.equals("body"))throw FlowgorithmImport.error(f,"Elemento della funzione non supportato.");Element parameters=child(f,"parameters");attributes(parameters);List<String> args=new ArrayList<>();for(Element p:parameters.children){if(!p.name.equals("parameter")||!p.children.isEmpty())throw FlowgorithmImport.error(p,"Parametro non valido.");attributes(p,"name","type","array");boolean array=flag(p,"array",false);args.add(name(p,p.get("name"))+": "+(array?"RIF ":"")+type(p,p.get("type"),false)+(array?"[]":""));}
   String type=type(f,f.get("type"),true),ret=f.get("variable");if(main&&(!type.equals("NESSUNO")||!args.isEmpty()))throw FlowgorithmImport.error(f,"Main non deve avere parametri o un valore di ritorno.");if(!main)line(f,"FUNZIONE "+name(f,f.get("name"))+"("+String.join(", ",args)+")"+(type.equals("NESSUNO")?"":" -> "+type+" "+name(f,ret)));
   trailingNotes="";line(f,"INIZIO");if(main&&hasTurtle)line(f,"PENNA \"#101010\"");Element body=child(f,"body");attributes(body);body(body,0);if(!trailingNotes.isEmpty())line(f,"NOTA "+literal(trailingNotes));line(f,"FINE");if(!main)line(f,"FINE FUNZIONE");
  }
  void body(Element body,int depth){if(depth>FlowPseudocode.MAX_DEPTH)throw FlowgorithmImport.error(body,"Troppi cicli o decisioni annidati.");String note="";for(Element e:body.children){
   if(e.name.equals("comment")){attributes(e,"text");if(!e.children.isEmpty())throw FlowgorithmImport.error(e,"Commento non valido.");line(e,"ANNOTAZIONE "+literal(e.get("text")));continue;}String blockNote=note;if(!note.isEmpty()&&!e.name.equals("do"))line(e,"NOTA "+literal(note));note="";
   if(!Arrays.asList("if","while","do","for").contains(e.name)&&!e.children.isEmpty())throw FlowgorithmImport.error(e,"Istruzione con contenuto inatteso.");switch(e.name){
    case "declare":attributes(e,"name","type","array","size");List<String> names=new ArrayList<>();for(String n:e.get("name").split(",",-1))names.add(name(e,n.trim()));line(e,"DICHIARA "+String.join(", ",names)+": "+type(e,e.get("type"),false)+(flag(e,"array",false)?"["+expr(e,"size")+"]":""));break;
    case "assign":attributes(e,"variable","expression");line(e,"ESEGUI "+target(e)+" = "+expr(e,"expression"));break;
    case "input":attributes(e,"variable");line(e,"LEGGI "+target(e));break;
    case "output":attributes(e,"expression","newline");line(e,(flag(e,"newline",true)?"STAMPA ":"STAMPA_CONTINUA ")+expr(e,"expression"));break;
    case "call":attributes(e,"expression");line(e,"CHIAMA "+expr(e,"expression"));break;
    case "open":attributes(e,"expression","mode");line(e,"APRI_FILE "+(choice(e,"mode","","read","write").equals("read")?"LETTURA ":"SCRITTURA ")+expr(e,"expression"));break;
    case "read":attributes(e,"variable");line(e,"LEGGI_FILE "+target(e));break;
    case "write":attributes(e,"expression");line(e,"SCRIVI_FILE "+expr(e,"expression"));break;
    case "close":attributes(e,"mode");line(e,"CHIUDI_FILE "+(choice(e,"mode","","read","write").equals("read")?"LETTURA":"SCRITTURA"));break;
    case "forward":attributes(e,"expression","pen");line(e,"AVANZA "+(choice(e,"pen","down","up","down").equals("up")?"SENZA_TRACCIA ":"")+expr(e,"expression"));break;
    case "turn":attributes(e,"expression","rotate");line(e,"RUOTA "+(choice(e,"rotate","right","left","right").equals("left")?"SINISTRA ":"DESTRA ")+expr(e,"expression"));break;
    case "pen":attributes(e,"color");line(e,"PENNA "+pen(e));break;
    case "breakpoint":attributes(e,"expression","always");line(e,flag(e,"always",true)?"PAUSA":"PAUSA SE "+expr(e,"expression"));break;
    case "home":attributes(e);line(e,"ORIGINE_TARTARUGA");break;
    case "clear":attributes(e);line(e,"PULISCI_DISEGNO");line(e,"PENNA \"#101010\"");break;
    case "if":attributes(e,"expression");if(e.children.size()!=2)throw FlowgorithmImport.error(e,"La decisione richiede i rami then ed else.");for(Element c:e.children)if(!c.name.equals("then")&&!c.name.equals("else"))throw FlowgorithmImport.error(e,"La decisione richiede i rami then ed else.");line(e,"SE "+expr(e,"expression")+" ALLORA");Element yes=child(e,"then");attributes(yes);body(yes,depth+1);line(e,"ALTRIMENTI");Element no=child(e,"else");attributes(no);body(no,depth+1);line(e,"FINE SE");break;
    case "while":attributes(e,"expression");line(e,"MENTRE "+expr(e,"expression")+" FAI");body(e,depth+1);line(e,"FINE MENTRE");break;
    case "do":attributes(e,"expression");line(e,"RIPETI");body(e,depth+1);if(!blockNote.isEmpty())line(e,"NOTA "+literal(blockNote));line(e,"FINCHÉ non ("+expr(e,"expression")+")");break;
    case "for":attributes(e,"variable","start","end","direction","step");String counter=name(e,e.get("variable")),direction=e.get("direction","inc");int step;try{step=Integer.parseInt(e.get("step","1").trim());}catch(NumberFormatException ex){throw FlowgorithmImport.error(e,"Il passo del ciclo For deve essere un intero positivo costante.");}if(step<=0)throw FlowgorithmImport.error(e,"Il passo del ciclo For deve essere un intero positivo costante.");if(!direction.equals("inc")&&!direction.equals("dec"))throw FlowgorithmImport.error(e,"Direzione For non riconosciuta.");line(e,"ESEGUI "+counter+" = "+expr(e,"start"));line(e,"MENTRE "+counter+(direction.equals("inc")?" <= ":" >= ")+expr(e,"end")+" FAI");body(e,depth+1);line(e,"ESEGUI "+counter+" = "+counter+(direction.equals("inc")?" + ":" - ")+step);line(e,"FINE MENTRE");break;
    default:throw FlowgorithmImport.error(e,"Questa istruzione non è ancora importabile. Nessuna parte del progetto è stata caricata.");
   }
  }if(!note.isEmpty()){trailingNotes+=(trailingNotes.isEmpty()?"":"\n")+note;if(trailingNotes.length()>2048)throw FlowgorithmImport.error(body,"Le note finali superano 2048 caratteri.");}}
  String target(Element e){Matcher m=Pattern.compile("("+FlowEngine.IDENTIFIER_PATTERN+")(?:\\[(.*)\\])?",Pattern.DOTALL).matcher(e.get("variable").trim());if(!m.matches())throw FlowgorithmImport.error(e,"Destinazione di input o assegnazione non valida.");return name(e,m.group(1))+(m.group(2)==null?"":"["+expression(e,m.group(2))+"]");}
 }
 private static String expr(Element e,String key){return expression(e,e.get(key));}
 private static String expression(Element e,String raw){try{return new ExpressionReader(raw).read();}catch(IllegalArgumentException ex){throw FlowgorithmImport.error(e,ex.getMessage());}}
 private static final class ExpressionReader {
  final List<String> tokens=new ArrayList<>();int at,depth;
  ExpressionReader(String s){if(s==null||s.trim().isEmpty()||s.length()>8192)throw new IllegalArgumentException("Espressione vuota o troppo lunga.");for(int i=0;i<s.length();){char c=s.charAt(i);if(Character.isWhitespace(c)){i++;continue;}if(c=='"'){int start=++i;while(i<s.length()&&s.charAt(i)!='"')i++;if(i==s.length())throw new IllegalArgumentException("Chiudi le virgolette nell'espressione.");tokens.add(literal(s.substring(start,i)));i++;}else if(Character.isLetter(c)||c=='_'){int start=i++;while(i<s.length()&&(Character.isLetterOrDigit(s.charAt(i))||s.charAt(i)=='_'))i++;tokens.add(s.substring(start,i));}else if(Character.isDigit(c)||c=='.'){int start=i++;while(i<s.length()&&(Character.isDigit(s.charAt(i))||s.charAt(i)=='.'))i++;if(i<s.length()&&(s.charAt(i)=='e'||s.charAt(i)=='E')){i++;if(i<s.length()&&(s.charAt(i)=='+'||s.charAt(i)=='-'))i++;while(i<s.length()&&Character.isDigit(s.charAt(i)))i++;}tokens.add(s.substring(start,i));}else{String symbol=String.valueOf(c);if(i+1<s.length()&&Arrays.asList("==","!=","<>","<=",">=","&&","||").contains(s.substring(i,i+2))){symbol=s.substring(i,i+2);i++;}i++;switch(symbol){case "×":symbol="*";break;case "÷":symbol="/";break;case "↑":symbol="^";break;case "≤":symbol="<=";break;case "≥":symbol=">=";break;case "≠":symbol="!=";break;case "¬":symbol="not";break;case "∧":case "&&":symbol="and";break;case "∨":case "||":symbol="or";break;case "!":symbol="not";break;}tokens.add(symbol);}if(tokens.size()>1024)throw new IllegalArgumentException("Espressione troppo complessa.");}tokens.add("");}
  String current(){return tokens.get(at);}boolean take(String value){if(!current().equalsIgnoreCase(value))return false;at++;return true;}void need(String value){if(!take(value))throw new IllegalArgumentException("Espressione non valida: manca «"+value+"».");}
  static int priority(String op){switch(op.toLowerCase(Locale.ROOT)){case "or":return 1;case "and":return 2;case "=":case "==":case "!=":case "<>":case "<":case ">":case "<=":case ">=":return 3;case "&":return 4;case "+":case "-":return 5;case "*":case "/":case "%":case "mod":return 6;case "^":return 7;default:return 0;}}
  String read(){String s=parse(1);if(!current().isEmpty())throw new IllegalArgumentException("Espressione non compatibile o incompleta.");if(s.length()>8192)throw new IllegalArgumentException("Espressione convertita troppo lunga.");return s;}
  String parse(int min){if(++depth>48)throw new IllegalArgumentException("Espressione troppo annidata.");String left;if(take("-")||take("+")||take("not")){String op=tokens.get(at-1).toLowerCase(Locale.ROOT);left="("+op+" ("+parse(8)+"))";}else if(take("(")){left="("+parse(1)+")";need(")");}else{left=current();if(left.isEmpty()||!(left.charAt(0)=='"'||Character.isLetterOrDigit(left.charAt(0))||left.charAt(0)=='_'||left.charAt(0)=='.'))throw new IllegalArgumentException("Espressione non compatibile o incompleta.");at++;if(take("(")){List<String> args=new ArrayList<>();if(!take(")")){do{args.add(parse(1));}while(take(","));need(")");}left+="("+String.join(", ",args)+")";}while(take("[")){left+="["+parse(1)+"]";need("]");}}
   while(priority(current())>=min){String op=current().toLowerCase(Locale.ROOT);int priority=priority(op);at++;String right=parse(priority+1);left=op.equals("&")?"(\"\" + ("+left+") + ("+right+"))":"("+left+" "+(op.equals("mod")?"%":op)+" "+right+")";if(left.length()>8192)throw new IllegalArgumentException("Espressione convertita troppo lunga.");}depth--;return left;}
 }
}
