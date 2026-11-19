using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Frociart {
 // A data-only adapter. XML never enters the runtime or the native project codec.
 internal static class FlowgorithmImport {
  internal const string Help="Apri anche file Flowgorithm (.fprg). Importazione locale di dichiarazioni, vettori, assegnazioni, input, output con o senza a capo, decisioni, cicli, funzioni con parametri scalari e vettoriali per riferimento, file di testo e tartaruga. I commenti diventano note libere sul foglio. I canali dei file devono essere indicati come read o write; in esecuzione scegli i documenti da leggere o salvare. I punti di interruzione diventano blocchi Pausa, con la condizione originale. Le condizioni di pausa non possono chiamare funzioni del progetto o random. I parametri vettoriali modificano gli elementi del vettore originale, con lo stesso tipo e dimensione. I costrutti non supportati vengono rifiutati con una spiegazione, mai importati in parte. Salva la copia come .frociart; l'originale resta intatto.";
  // Explicit conversion only: never change the dialect of an existing project or evaluate an expression.
  internal static string ConvertExpression(string raw,IDictionary<string,FlowFunction> functions=null){
   if(raw!=null&&raw.Length>8192)throw new ArgumentException("L’espressione supera 8192 caratteri. Riducila prima di inserirla.");
   string result=new ExpressionReader(raw).Read();ExpressionLanguage.Parse(result,functions);return result;
  }
  private sealed class Element {
   internal string Name;internal int Line;internal readonly Dictionary<string,string> Attr=new Dictionary<string,string>();internal readonly List<Element> Children=new List<Element>();
   internal string Get(string key,string fallback=""){string value;return Attr.TryGetValue(key,out value)?value:fallback;}
  }
  private static InvalidDataException Error(Element e,string detail){return new InvalidDataException("Flowgorithm"+(e==null?"":", riga "+e.Line+" (<"+e.Name+">)")+": "+detail);}
  internal static Diagram Decode(byte[] bytes){
   if(bytes==null||bytes.Length==0||bytes.Length>DiagramIO.MaxFileBytes)throw Error(null,"Il file è vuoto o supera 4 MB.");
   string text;try{text=new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){throw Error(null,"Salva il file in UTF-8 e riprova.");}
   return DecodeText(text);
  }
  internal static Diagram DecodeText(string text){
   if(text==null||text.Length==0||text.Length>DiagramIO.MaxFileBytes)throw Error(null,"Il file è vuoto o troppo grande.");
   if(text[0]=='\uFEFF')text=text.Substring(1);
   if(Regex.IsMatch(text,@"<!\s*(DOCTYPE|ENTITY)",RegexOptions.IgnoreCase))throw Error(null,"DTD ed entità XML non sono consentite.");
   Element root=null;var stack=new Stack<Element>();int count=0;
   var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,IgnoreComments=true,IgnoreProcessingInstructions=true,MaxCharactersInDocument=DiagramIO.MaxFileBytes};
   try{using(var xml=XmlReader.Create(new StringReader(text),settings)){while(xml.Read()){
    if(xml.NodeType==XmlNodeType.Element){
     if(++count>6000||stack.Count>51||xml.AttributeCount>16||xml.Name.Length>80||xml.NamespaceURI.Length!=0||xml.Name.IndexOf(':')>=0)throw Error(null,"Struttura XML troppo complessa o non supportata.");
     var e=new Element{Name=xml.Name,Line=((IXmlLineInfo)xml).LineNumber};
     if(xml.MoveToFirstAttribute()){do{if(xml.Value.Length>8192||xml.Name.Length>80||xml.NamespaceURI.Length!=0)throw Error(e,"Attributo troppo lungo o non supportato.");e.Attr.Add(xml.Name,xml.Value);}while(xml.MoveToNextAttribute());xml.MoveToElement();}
     if(stack.Count==0){if(root!=null)throw Error(e,"Più documenti nello stesso file.");root=e;}else stack.Peek().Children.Add(e);
     if(!xml.IsEmptyElement)stack.Push(e);
    }else if(xml.NodeType==XmlNodeType.EndElement)stack.Pop();
    else if((xml.NodeType==XmlNodeType.Text||xml.NodeType==XmlNodeType.CDATA)&&!String.IsNullOrWhiteSpace(xml.Value))throw Error(null,"Il contenuto XML non è un progetto Flowgorithm valido.");
   }}}catch(XmlException ex){throw new InvalidDataException("Flowgorithm, riga "+ex.LineNumber+": XML incompleto o non valido.");}
   if(root==null||root.Name!="flowgorithm")throw Error(root,"Scegli un progetto Flowgorithm .fprg.");
   Attributes(root,"fileversion");if(!Regex.IsMatch(root.Get("fileversion"),@"\A[1-4]\.[0-9]{1,2}\z"))throw Error(root,"Versione del formato non supportata (sono riconosciuti i formati da 1.x a 4.x).");
   var writer=new Writer();return writer.Read(root);
  }
  private static void Attributes(Element e,params string[] allowed){foreach(string key in e.Attr.Keys)if(!allowed.Contains(key))throw Error(e,"Attributo «"+key+"» non supportato. Il progetto non è stato modificato.");}
  private static Element Child(Element e,string name){var all=e.Children.Where(c=>c.Name==name).ToList();if(all.Count!=1)throw Error(e,"Serve un solo elemento <"+name+">.");return all[0];}
  private static bool Flag(Element e,string name,bool fallback){string s=e.Get(name,fallback?"true":"false");if(s.Equals("true",StringComparison.OrdinalIgnoreCase))return true;if(s.Equals("false",StringComparison.OrdinalIgnoreCase))return false;throw Error(e,"«"+name+"» deve essere true o false.");}
  private static string Name(Element e,string value){if(!ExpressionLanguage.Identifier(value))throw Error(e,"Nome non compatibile. Usa lettere, numeri e underscore: inizia con una lettera o underscore, massimo 64 caratteri.");return value;}
  private static string Type(Element e,string value,bool allowVoid=false){switch(value.ToLowerInvariant()){case "integer":return "INTERO";case "real":return "REALE";case "string":return "TESTO";case "boolean":return "LOGICO";case "none":if(allowVoid)return "NESSUNO";break;}throw Error(e,"Tipo di dato non supportato.");}
  private static string Literal(string s){return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t")+"\"";}
  private static string Choice(Element e,string key,string fallback,params string[] allowed){string value=e.Get(key,fallback);if(!allowed.Contains(value))throw Error(e,"«"+key+"» deve essere "+String.Join(" oppure ",allowed)+".");return value;}
  // Canonical ten-colour palette, verified against the installed Flowgorithm 4.5 data model.
  internal static readonly string[] PenNames={"black","gray","red","orange","yellow","green","teal","blue","violet","brown"},PenColors={"#101010","#808080","#E02030","#F07020","#F0C020","#30C080","#30C0E0","#2080E0","#A060C0","#C06C40"},PenLabels={"Nero","Grigio","Rosso","Arancione","Giallo","Verde","Turchese","Blu","Viola","Marrone"};
  private static string Pen(Element e){int at=Array.IndexOf(PenNames,e.Get("color"));if(at<0)throw Error(e,"Colore della penna non riconosciuto.");return Literal(PenColors[at]);}
  private sealed class Writer {
   private readonly StringBuilder code=new StringBuilder();private readonly List<Element> origins=new List<Element>();private string trailingNotes="";private bool hasTurtle;
   private static bool HasTurtle(Element e){return new[]{"forward","turn","home","clear","pen"}.Contains(e.Name)||e.Children.Any(HasTurtle);}
   private void Line(Element e,string s){code.Append(s).Append('\n');origins.Add(e);if(code.Length>FlowPseudocode.MaxText||origins.Count>FlowPseudocode.MaxLines)throw Error(e,"Il progetto supera i limiti di importazione.");}
   internal Diagram Read(Element root){
    var functions=new List<Element>();int metadata=0;foreach(var e in root.Children){if(e.Name=="attributes"){if(++metadata>1)throw Error(e,"Metadati duplicati.");continue;}if(e.Name!="function")throw Error(e,"Elemento non supportato.");functions.Add(e);}
    if(functions.Count>33)throw Error(root,"Massimo 32 funzioni oltre al programma principale.");var mains=functions.Where(e=>e.Get("name").Equals("Main",StringComparison.OrdinalIgnoreCase)).ToList();if(mains.Count!=1)throw Error(root,"Serve un solo programma Main.");
    hasTurtle=functions.Any(HasTurtle);Line(root,"VARIABILI DICHIARATE");
    foreach(var f in functions.Where(e=>e!=mains[0]))Function(f,false);Function(mains[0],true);
    Diagram d;try{d=FlowPseudocode.Decode(code.ToString());}catch(FlowPseudocode.ParseError ex){throw Error(origins[Math.Max(0,Math.Min(origins.Count-1,ex.Line-1))],Regex.Replace(ex.Message,@"\ARiga [0-9]+: ",""));}
    d.Title="Da Flowgorithm";FlowLayout.Arrange(d);FlowAnnotations.PlaceImported(d);if(d.Functions!=null)foreach(var f in d.Functions){FlowLayout.Arrange(f.Body);FlowAnnotations.PlaceImported(f.Body);}DiagramIO.CheckDocument(d);return d;
   }
   private void Function(Element f,bool main){
    Attributes(f,"name","type","variable");if(f.Children.Any(e=>e.Name!="parameters"&&e.Name!="body"))throw Error(f,"Elemento della funzione non supportato.");
    var parameters=Child(f,"parameters");Attributes(parameters);var args=new List<string>();foreach(var p in parameters.Children){if(p.Name!="parameter"||p.Children.Count!=0)throw Error(p,"Parametro non valido.");Attributes(p,"name","type","array");bool array=Flag(p,"array",false);args.Add(Name(p,p.Get("name"))+": "+(array?"RIF ":"")+Type(p,p.Get("type"))+(array?"[]":""));}
    string type=Type(f,f.Get("type"),true),ret=f.Get("variable");if(main&&(type!="NESSUNO"||args.Count!=0))throw Error(f,"Main non deve avere parametri o un valore di ritorno.");
    if(!main)Line(f,"FUNZIONE "+Name(f,f.Get("name"))+"("+String.Join(", ",args)+")"+(type=="NESSUNO"?"":" -> "+type+" "+Name(f,ret)));
    trailingNotes="";Line(f,"INIZIO");if(main&&hasTurtle)Line(f,"PENNA \"#101010\"");var body=Child(f,"body");Attributes(body);Body(body,0);if(trailingNotes.Length>0)Line(f,"NOTA "+Literal(trailingNotes));Line(f,"FINE");if(!main)Line(f,"FINE FUNZIONE");
   }
   private void Body(Element body,int depth){
    if(depth>FlowPseudocode.MaxDepth)throw Error(body,"Troppi cicli o decisioni annidati.");string note="";
    foreach(var e in body.Children){
     if(e.Name=="comment"){Attributes(e,"text");if(e.Children.Count!=0)throw Error(e,"Commento non valido.");Line(e,"ANNOTAZIONE "+Literal(e.Get("text")));continue;}
     string blockNote=note;if(note.Length>0&&e.Name!="do")Line(e,"NOTA "+Literal(note));note="";
     bool compound=e.Name=="if"||e.Name=="while"||e.Name=="do"||e.Name=="for";if(!compound&&e.Children.Count!=0)throw Error(e,"Istruzione con contenuto inatteso.");
     switch(e.Name){
      case "declare":Attributes(e,"name","type","array","size");var names=e.Get("name").Split(',').Select(s=>Name(e,s.Trim())).ToArray();Line(e,"DICHIARA "+String.Join(", ",names)+": "+Type(e,e.Get("type"))+(Flag(e,"array",false)?"["+Expr(e,"size")+"]":""));break;
      case "assign":Attributes(e,"variable","expression");Line(e,"ESEGUI "+Target(e)+" = "+Expr(e,"expression"));break;
      case "input":Attributes(e,"variable");Line(e,"LEGGI "+Target(e));break;
      case "output":Attributes(e,"expression","newline");Line(e,(Flag(e,"newline",true)?"STAMPA ":"STAMPA_CONTINUA ")+Expr(e,"expression"));break;
      case "call":Attributes(e,"expression");Line(e,"CHIAMA "+Expr(e,"expression"));break;
      case "open":Attributes(e,"expression","mode");Line(e,"APRI_FILE "+(Choice(e,"mode","","read","write")=="read"?"LETTURA ":"SCRITTURA ")+Expr(e,"expression"));break;
      case "read":Attributes(e,"variable");Line(e,"LEGGI_FILE "+Target(e));break;
      case "write":Attributes(e,"expression");Line(e,"SCRIVI_FILE "+Expr(e,"expression"));break;
      case "close":Attributes(e,"mode");Line(e,"CHIUDI_FILE "+(Choice(e,"mode","","read","write")=="read"?"LETTURA":"SCRITTURA"));break;
      case "forward":Attributes(e,"expression","pen");Line(e,"AVANZA "+(Choice(e,"pen","down","up","down")=="up"?"SENZA_TRACCIA ":"")+Expr(e,"expression"));break;
      case "turn":Attributes(e,"expression","rotate");Line(e,"RUOTA "+(Choice(e,"rotate","right","left","right")=="left"?"SINISTRA ":"DESTRA ")+Expr(e,"expression"));break;
      case "pen":Attributes(e,"color");Line(e,"PENNA "+Pen(e));break;
      case "breakpoint":Attributes(e,"expression","always");Line(e,Flag(e,"always",true)?"PAUSA":"PAUSA SE "+Expr(e,"expression"));break;
      case "home":Attributes(e);Line(e,"ORIGINE_TARTARUGA");break;
      case "clear":Attributes(e);Line(e,"PULISCI_DISEGNO");Line(e,"PENNA \"#101010\"");break;
      case "if":Attributes(e,"expression");if(e.Children.Count!=2||e.Children.Any(c=>c.Name!="then"&&c.Name!="else"))throw Error(e,"La decisione richiede i rami then ed else.");Line(e,"SE "+Expr(e,"expression")+" ALLORA");var yes=Child(e,"then");Attributes(yes);Body(yes,depth+1);Line(e,"ALTRIMENTI");var no=Child(e,"else");Attributes(no);Body(no,depth+1);Line(e,"FINE SE");break;
      case "while":Attributes(e,"expression");Line(e,"MENTRE "+Expr(e,"expression")+" FAI");Body(e,depth+1);Line(e,"FINE MENTRE");break;
      case "do":Attributes(e,"expression");Line(e,"RIPETI");Body(e,depth+1);if(blockNote.Length>0)Line(e,"NOTA "+Literal(blockNote));Line(e,"FINCHÉ non ("+Expr(e,"expression")+")");break;
      case "for":
       Attributes(e,"variable","start","end","direction","step");string counter=Name(e,e.Get("variable")),direction=e.Get("direction","inc");int step;if(!Int32.TryParse(e.Get("step","1"),out step)||step<=0)throw Error(e,"Il passo del ciclo For deve essere un intero positivo costante.");if(direction!="inc"&&direction!="dec")throw Error(e,"Direzione For non riconosciuta.");
       Line(e,"ESEGUI "+counter+" = "+Expr(e,"start"));Line(e,"MENTRE "+counter+(direction=="inc"?" <= ":" >= ")+Expr(e,"end")+" FAI");Body(e,depth+1);Line(e,"ESEGUI "+counter+" = "+counter+(direction=="inc"?" + ":" - ")+step);Line(e,"FINE MENTRE");break;
      default:throw Error(e,"Questa istruzione non è ancora importabile. Nessuna parte del progetto è stata caricata.");
     }
    }
    // Scope-ending notes are retained on the function terminator, never as executable nodes.
    if(note.Length>0){trailingNotes+=(trailingNotes.Length>0?"\n":"")+note;if(trailingNotes.Length>2048)throw Error(body,"Le note finali superano 2048 caratteri.");}
   }
   private static string Target(Element e){string raw=e.Get("variable").Trim();var m=Regex.Match(raw,@"\A("+ExpressionLanguage.IdentifierPattern+@")(?:\[(.*)\])?\z",RegexOptions.Singleline);if(!m.Success)throw Error(e,"Destinazione di input o assegnazione non valida.");return Name(e,m.Groups[1].Value)+(m.Groups[2].Success?"["+Expression(e,m.Groups[2].Value)+"]":"");}
  }
  private static string Expr(Element e,string key){return Expression(e,e.Get(key));}
  private static string Expression(Element e,string raw){try{return new ExpressionReader(raw).Read();}catch(ArgumentException ex){throw Error(e,ex.Message);}}
  // Fully parenthesize source expressions: & and unary precedence differ from Frociart.
  private sealed class ExpressionReader {
   private readonly List<string> tokens=new List<string>();private int at,depth;
   internal ExpressionReader(string s){if(String.IsNullOrWhiteSpace(s)||s.Length>8192)throw new ArgumentException("Espressione vuota o troppo lunga.");for(int i=0;i<s.Length;){char c=s[i];if(Char.IsWhiteSpace(c)){i++;continue;}if(c=='"'){int start=++i;while(i<s.Length&&s[i]!='"')i++;if(i==s.Length)throw new ArgumentException("Chiudi le virgolette nell'espressione.");tokens.Add(Literal(s.Substring(start,i-start)));i++;}else if(Char.IsLetter(c)||c=='_'){int start=i++;while(i<s.Length&&(Char.IsLetterOrDigit(s[i])||s[i]=='_'))i++;tokens.Add(s.Substring(start,i-start));}else if(Char.IsDigit(c)||c=='.'){int start=i++;while(i<s.Length&&(Char.IsDigit(s[i])||s[i]=='.'))i++;if(i<s.Length&&(s[i]=='e'||s[i]=='E')){i++;if(i<s.Length&&(s[i]=='+'||s[i]=='-'))i++;while(i<s.Length&&Char.IsDigit(s[i]))i++;}tokens.Add(s.Substring(start,i-start));}else{string symbol=c.ToString();if(i+1<s.Length&&new[]{"==","!=","<>","<=",">=","&&","||"}.Contains(s.Substring(i,2))){symbol=s.Substring(i,2);i++;}i++;switch(symbol){case "×":symbol="*";break;case "÷":symbol="/";break;case "↑":symbol="^";break;case "≤":symbol="<=";break;case "≥":symbol=">=";break;case "≠":symbol="!=";break;case "¬":symbol="not";break;case "∧":case "&&":symbol="and";break;case "∨":case "||":symbol="or";break;case "!":symbol="not";break;}tokens.Add(symbol);}if(tokens.Count>1024)throw new ArgumentException("Espressione troppo complessa.");}tokens.Add("");}
   private string Current{get{return tokens[at];}}
   private bool Take(string value){if(!Current.Equals(value,StringComparison.OrdinalIgnoreCase))return false;at++;return true;}
   private void Need(string value){if(!Take(value))throw new ArgumentException("Espressione non valida: manca «"+value+"».");}
   private static int Priority(string op){switch(op.ToLowerInvariant()){case "or":return 1;case "and":return 2;case "=":case "==":case "!=":case "<>":case "<":case ">":case "<=":case ">=":return 3;case "&":return 4;case "+":case "-":return 5;case "*":case "/":case "%":case "mod":return 6;case "^":return 7;default:return 0;}}
   internal string Read(){string s=Parse(1);if(Current!="")throw new ArgumentException("Espressione non compatibile o incompleta.");if(s.Length>8192)throw new ArgumentException("Espressione convertita troppo lunga.");return s;}
   private string Parse(int min){if(++depth>48)throw new ArgumentException("Espressione troppo annidata.");string left;if(Take("-")||Take("+")||Take("not")){string op=tokens[at-1].ToLowerInvariant();left="("+op+" ("+Parse(8)+"))";}else if(Take("(")){left="("+Parse(1)+")";Need(")");}else{left=Current;if(left.Length==0||!(left[0]=='"'||Char.IsLetterOrDigit(left[0])||left[0]=='_'||left[0]=='.'))throw new ArgumentException("Espressione non compatibile o incompleta.");at++;if(Take("(")){var args=new List<string>();if(!Take(")")){do{args.Add(Parse(1));}while(Take(","));Need(")");}left+="("+String.Join(", ",args)+")";}while(Take("[")){left+="["+Parse(1)+"]";Need("]");}}
    while(Priority(Current)>=min){string op=Current.ToLowerInvariant();int priority=Priority(op);at++;string right=Parse(priority+1);left=op=="&"?"(\"\" + ("+left+") + ("+right+"))":"("+left+" "+(op=="mod"?"%":op)+" "+right+")";if(left.Length>8192)throw new ArgumentException("Espressione convertita troppo lunga.");}depth--;return left;}
  }
 }
}
