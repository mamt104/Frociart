package it.frociart.app;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import static it.frociart.app.FlowModel.*;

/** Portable .frociart formats: graphs (1), functions (2), vector references (3), free annotations (4), explicit variable policy (5). */
public final class FlowCodec {
    private FlowCodec() {}
    public static Document decode(String source) {
        if (source==null || source.length()==0 || source.length()>MAX_BYTES || source.getBytes(StandardCharsets.UTF_8).length>MAX_BYTES)
            throw invalid("Il file è vuoto o supera il limite di 4 MB.");
        if (source.charAt(0)=='\ufeff') source=source.substring(1);
        Map<String,Object> root=object(new Json(source).read(),"progetto");
        Document d=readGraph(root,true);check(d);return d;
    }
    private static Document readGraph(Map<String,Object> root,boolean project){
        Document d=new Document();
        d.version=integer(root.get("Version"),"Version");
        if(root.get("VariablePolicy")!=null)d.variablePolicy=string(root.get("VariablePolicy"),"modalità delle variabili",false);
        d.title=string(root.get("Title"),"titolo",true);
        List<?> nodes=array(root.get("Nodes"),"blocchi"), edges=array(root.get("Edges"),"collegamenti");
        if (nodes.size()>MAX_NODES || edges.size()>MAX_EDGES) throw invalid("Limite superato: massimo 500 blocchi e 1500 collegamenti.");
        for(Object item:nodes) {
            Map<String,Object> m=object(item,"blocco");
            Node n=new Node(); n.id=string(m.get("Id"),"identificatore",false);
            n.kind=integer(m.get("Kind"),"tipo di blocco");
            n.text=string(m.get("Text"),"testo",true);n.prompt=string(m.get("Prompt"),"domanda",true);
            if(m.get("TrueLabel")!=null)n.trueLabel=string(m.get("TrueLabel"),"etichetta del ramo Sì",false);
            if(m.get("FalseLabel")!=null)n.falseLabel=string(m.get("FalseLabel"),"etichetta del ramo No",false);
            if(m.get("TrueColor")!=null)n.trueColor=string(m.get("TrueColor"),"colore del ramo Sì",false);
            if(m.get("FalseColor")!=null)n.falseColor=string(m.get("FalseColor"),"colore del ramo No",false);
            if(m.get("Comment")!=null)n.comment=string(m.get("Comment"),"commento del blocco",true);
            n.breakpoint=bool(m.get("Breakpoint"));
            n.foldedCycle=bool(m.get("FoldedCycle"));
            if(m.get("BreakpointCondition")!=null)n.breakpointCondition=string(m.get("BreakpointCondition"),"condizione del punto di interruzione",true);
            n.x=coordinate(m.get("X"));n.y=coordinate(m.get("Y"));d.nodes.add(n);
        }
        for(Object item:edges) {
            Map<String,Object> m=object(item,"collegamento");
            Edge e=new Edge();e.id=string(m.get("Id"),"identificatore",false);
            e.from=string(m.get("From"),"partenza",false);e.to=string(m.get("To"),"arrivo",false);
            e.branch=string(m.get("Branch"),"ramo",false);d.edges.add(e);
        }
        if(root.get("Functions")!=null){
            if(!project)throw invalid("Una funzione non può contenere altre definizioni di funzione.");
            List<?> functions=array(root.get("Functions"),"funzioni");
            if(functions.size()>FlowFunctions.MAX_FUNCTIONS)throw invalid("Massimo 32 funzioni per progetto.");
            d.functions=new ArrayList<>();
            for(Object item:functions){
                Map<String,Object> m=object(item,"funzione");Function f=new Function();
                f.id=string(m.get("Id"),"identificatore funzione",false);f.name=string(m.get("Name"),"nome funzione",false);
                f.returnType=integer(m.get("ReturnType"),"tipo del risultato");f.returnVariable=string(m.get("ReturnVariable"),"variabile del risultato",true);
                f.returnIsArray=bool(m.get("ReturnIsArray"));
                List<?> parameters=array(m.get("Parameters"),"parametri");
                if(parameters.size()>FlowFunctions.MAX_PARAMETERS)throw invalid("Massimo 16 parametri per funzione.");
                for(Object parameter:parameters){Map<String,Object> pm=object(parameter,"parametro");Parameter p=new Parameter();p.name=string(pm.get("Name"),"nome parametro",false);p.type=integer(pm.get("Type"),"tipo parametro");p.isArray=bool(pm.get("IsArray"));p.byReference=bool(pm.get("ByReference"));f.parameters.add(p);}
                f.body=readGraph(object(m.get("Body"),"diagramma funzione"),false);d.functions.add(f);
            }
        }
        return d;
    }
    public static String encode(Document d) {
        check(d);
        StringBuilder b=new StringBuilder(1024);
        writeGraph(b,d);String text=b.toString();
        if(text.length()>MAX_BYTES || text.getBytes(StandardCharsets.UTF_8).length>MAX_BYTES) throw invalid("Il diagramma supera il limite di 4 MB.");
        return text;
    }
    private static void writeGraph(StringBuilder b,Document d){
        b.append("{\"Version\":").append(d.version).append(",\"Title\":");quote(b,d.title);b.append(",\"Nodes\":[");
        for(int i=0;i<d.nodes.size();i++) {
            if(i>0)b.append(',');Node n=d.nodes.get(i);
            b.append("{\"Id\":");quote(b,n.id);b.append(",\"Kind\":").append(n.kind).append(",\"Text\":");quote(b,n.text);
            b.append(",\"Prompt\":");quote(b,n.prompt);b.append(",\"X\":").append(Float.toString(n.x)).append(",\"Y\":").append(Float.toString(n.y));
            b.append(",\"TrueLabel\":");quote(b,n.trueLabel);b.append(",\"FalseLabel\":");quote(b,n.falseLabel);
            b.append(",\"TrueColor\":");quote(b,n.trueColor);b.append(",\"FalseColor\":");quote(b,n.falseColor);
            if(n.comment!=null&&!n.comment.isEmpty()){b.append(",\"Comment\":");quote(b,n.comment);}
            if(n.breakpoint)b.append(",\"Breakpoint\":true");
            if(n.foldedCycle)b.append(",\"FoldedCycle\":true");
            if(n.breakpointCondition!=null){b.append(",\"BreakpointCondition\":");quote(b,n.breakpointCondition);}
            b.append('}');
        }
        b.append("],\"Edges\":[");
        for(int i=0;i<d.edges.size();i++) {
            if(i>0)b.append(',');Edge e=d.edges.get(i);
            b.append("{\"Id\":");quote(b,e.id);b.append(",\"From\":");quote(b,e.from);b.append(",\"To\":");quote(b,e.to);
            b.append(",\"Branch\":");quote(b,e.branch);b.append('}');
        }
        b.append(']');
        if(d.functions!=null){
            b.append(",\"Functions\":[");
            for(int i=0;i<d.functions.size();i++){
                if(i>0)b.append(',');Function f=d.functions.get(i);b.append("{\"Id\":");quote(b,f.id);b.append(",\"Name\":");quote(b,f.name);b.append(",\"Parameters\":[");
                for(int j=0;j<f.parameters.size();j++){if(j>0)b.append(',');Parameter p=f.parameters.get(j);b.append("{\"Name\":");quote(b,p.name);b.append(",\"Type\":").append(p.type).append(",\"IsArray\":").append(p.isArray);if(p.byReference)b.append(",\"ByReference\":true");b.append('}');}
                b.append("],\"ReturnType\":").append(f.returnType).append(",\"ReturnVariable\":");quote(b,f.returnVariable);b.append(",\"Body\":");writeGraph(b,f.body);b.append(",\"ReturnIsArray\":").append(f.returnIsArray).append('}');
            }b.append(']');
        }if(d.variablePolicy!=null){b.append(",\"VariablePolicy\":");quote(b,d.variablePolicy);}b.append('}');
    }
    public static void check(Document d) {
        checkGraph(d);FlowFunctions.checkProject(d);if(d.version<4&&FlowAnnotations.has(d))throw invalid("Le note libere richiedono il formato 4. Aggiorna il progetto prima di salvarlo.");
    }
    static void checkGraph(Document d){
        if(d==null)throw invalid("Il diagramma è vuoto.");
        if(d.version!=1&&d.version!=2&&d.version!=3&&d.version!=4&&d.version!=5)throw invalid("Versione del file non supportata. Aggiorna Frociart prima di aprirlo.");
        if(d.variablePolicy!=null&&!d.variablePolicy.equals("auto")&&!d.variablePolicy.equals("declared"))throw invalid("Modalità delle variabili non supportata.");
        if(d.requiresDeclarations()&&d.version!=5||d.version==5&&!d.requiresDeclarations())throw invalid("Le dichiarazioni obbligatorie richiedono il formato 5. Aggiorna il progetto prima di salvarlo.");
        if(d.nodes.size()>MAX_NODES || d.edges.size()>MAX_EDGES)throw invalid("Limite superato: massimo 500 blocchi e 1500 collegamenti.");
        limit(d.title,200,"titolo");HashSet<String> ids=new HashSet<>();
        for(Node n:d.nodes) {
            if(n==null)throw invalid("Il file contiene un blocco nullo.");limit(n.id,128,"identificatore");
            if(n.id==null || n.id.trim().isEmpty() || !ids.add(n.id))throw invalid("Il file contiene identificatori di blocco vuoti o duplicati.");
            if(!isKnownKind(n.kind))throw invalid("Il file contiene un tipo di blocco sconosciuto.");
            if(n.kind==NOTE&&n.breakpoint)throw invalid("Una nota libera non può avere punti di interruzione.");
            limit(n.comment,2048,"commento del blocco");limit(n.breakpointCondition,8192,"condizione del punto di interruzione");limit(n.text,8192,"testo del blocco");limit(n.prompt,1024,"domanda");
            limit(n.trueLabel,60,"etichetta del ramo Sì");limit(n.falseLabel,60,"etichetta del ramo No");
            if(n.trueLabel==null||n.falseLabel==null)throw invalid("Le etichette dei rami non possono essere nulle.");
            if(!color(n.trueColor)||!color(n.falseColor))throw invalid("Il colore del ramo deve avere il formato #RRGGBB.");
            if(!finite(n.x)||!finite(n.y)||Math.abs(n.x)>100000||Math.abs(n.y)>100000)throw invalid("Il file contiene coordinate di blocco non valide.");
        }
        HashSet<String> edgeIds=new HashSet<>();
        for(Edge e:d.edges) {
            if(e==null)throw invalid("Il file contiene un collegamento nullo.");
            limit(e.id,128,"identificatore del collegamento");limit(e.from,128,"partenza");limit(e.to,128,"arrivo");limit(e.branch,16,"ramo");
            if(e.id==null || e.id.trim().isEmpty() || !edgeIds.add(e.id))throw invalid("Il file contiene identificatori di collegamento vuoti o duplicati.");
            if(d.find(e.from)!=null&&d.find(e.from).kind==NOTE||d.find(e.to)!=null&&d.find(e.to).kind==NOTE)throw invalid("Una nota libera non può avere collegamenti di esecuzione.");
            if(!ids.contains(e.from)||!ids.contains(e.to))throw invalid("Un collegamento punta a un blocco che non esiste.");
            if(!"next".equals(e.branch)&&!"true".equals(e.branch)&&!"false".equals(e.branch))throw invalid("Il file contiene un ramo sconosciuto.");
        }
    }
    private static boolean finite(float n){return !Float.isInfinite(n)&&!Float.isNaN(n);}
    private static boolean color(String s){if(s==null||s.length()!=7||s.charAt(0)!='#')return false;for(int i=1;i<7;i++){char c=s.charAt(i);if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;}return true;}
    private static void limit(String s,int max,String name){if(s!=null&&s.length()>max)throw invalid("Testo troppo lungo: "+name+" (massimo "+max+" caratteri).");}
    private static IllegalArgumentException invalid(String m){return new IllegalArgumentException(m);}
    private static boolean bool(Object value){if(value==null)return false;if(!(value instanceof Boolean))throw invalid("Valore logico non valido nella funzione.");return(Boolean)value;}
    @SuppressWarnings("unchecked") private static Map<String,Object> object(Object o,String name){if(!(o instanceof Map))throw invalid("Progetto non valido: manca l'oggetto "+name+".");return (Map<String,Object>)o;}
    private static List<?> array(Object o,String name){if(!(o instanceof List))throw invalid("Progetto non valido: manca l'elenco dei "+name+".");return (List<?>)o;}
    private static String string(Object o,String name,boolean nullable){if(o==null&&nullable)return "";if(!(o instanceof String))throw invalid("Progetto non valido: "+name+" deve essere un testo.");return (String)o;}
    private static int integer(Object o,String name){if(!(o instanceof Double))throw invalid("Progetto non valido: manca "+name+".");double v=(Double)o;if(v!=Math.rint(v)||v<Integer.MIN_VALUE||v>Integer.MAX_VALUE)throw invalid("Progetto non valido: "+name+" deve essere intero.");return(int)v;}
    private static float coordinate(Object o){if(!(o instanceof Double))throw invalid("Coordinate del blocco mancanti o non valide.");double v=(Double)o;if(Math.abs(v)>100000)throw invalid("Coordinate del blocco fuori limite.");return(float)v;}
    private static void quote(StringBuilder b,String text) {
        if(text==null){b.append("null");return;}b.append('"');
        for(int i=0;i<text.length();i++) {
            char c=text.charAt(i);switch(c) {
                case '"':b.append("\\\"");break;case '\\':b.append("\\\\");break;case '\n':b.append("\\n");break;
                case '\r':b.append("\\r");break;case '\t':b.append("\\t");break;case '\b':b.append("\\b");break;case '\f':b.append("\\f");break;
                default:if(c<32 || Character.isSurrogate(c)){b.append("\\u");String hex=Integer.toHexString(c);for(int j=hex.length();j<4;j++)b.append('0');b.append(hex);}else b.append(c);
            }
        }b.append('"');
    }
    /** Bounded JSON reader: no reflection, executable expressions, permissive numbers or duplicate keys. */
    private static final class Json {
        private final String text;private int p,depth,items;
        Json(String text){this.text=text;}
        Object read(){Object result=value();space();if(p!=text.length())fail("Dati dopo la fine del progetto");return result;}
        private Object value() {
            if(++items>40000)fail("Troppi elementi JSON");space();if(p>=text.length())fail("File incompleto");char c=text.charAt(p);
            if(c=='{')return object();if(c=='[')return array();if(c=='"')return string();
            if(c=='t'&&literal("true"))return Boolean.TRUE;if(c=='f'&&literal("false"))return Boolean.FALSE;if(c=='n'&&literal("null"))return null;
            if(c=='-'||digit(c))return number();fail("Valore JSON non valido");return null;
        }
        private Map<String,Object> object() {
            enter();p++;LinkedHashMap<String,Object> m=new LinkedHashMap<>();space();if(take('}')){depth--;return m;}
            do{space();if(p>=text.length()||text.charAt(p)!='"')fail("Chiave JSON non valida");String key=string();space();expect(':');
                if(m.containsKey(key))fail("Chiave JSON duplicata: "+(key.length()>80?key.substring(0,80):key));m.put(key,value());space();
            }while(take(','));expect('}');depth--;return m;
        }
        private List<Object> array() {enter();p++;ArrayList<Object> a=new ArrayList<>();space();if(take(']')){depth--;return a;}do{a.add(value());space();}while(take(','));expect(']');depth--;return a;}
        private String string() {
            expect('"');StringBuilder b=new StringBuilder();
            while(p<text.length()) {char c=text.charAt(p++);if(c=='"')return b.toString();if(c<32)fail("Carattere di controllo nel testo");
                if(c=='\\') {if(p>=text.length())fail("Escape incompleto");c=text.charAt(p++);switch(c){
                    case '"':case '\\':case '/':b.append(c);break;case 'b':b.append('\b');break;case 'f':b.append('\f');break;
                    case 'n':b.append('\n');break;case 'r':b.append('\r');break;case 't':b.append('\t');break;
                    case 'u':if(p+4>text.length())fail("Escape Unicode incompleto");int value=0;for(int i=0;i<4;i++){int x=Character.digit(text.charAt(p++),16);if(x<0)fail("Escape Unicode non valido");value=(value<<4)|x;}b.append((char)value);break;
                    default:fail("Escape JSON non valido");
                }}else b.append(c);
                if(b.length()>MAX_BYTES)fail("Testo troppo lungo");
            }fail("Virgolette JSON non chiuse");return null;
        }
        private Double number() {
            int start=p;take('-');if(p>=text.length())fail("Numero incompleto");
            if(take('0')){if(p<text.length()&&digit(text.charAt(p)))fail("Numero con zero iniziale");}
            else{if(!digit(text.charAt(p)))fail("Numero non valido");while(p<text.length()&&digit(text.charAt(p)))p++;}
            if(take('.')){int begin=p;while(p<text.length()&&digit(text.charAt(p)))p++;if(begin==p)fail("Numero decimale incompleto");}
            if(p<text.length()&&(text.charAt(p)=='e'||text.charAt(p)=='E')){p++;if(p<text.length()&&(text.charAt(p)=='+'||text.charAt(p)=='-'))p++;int begin=p;while(p<text.length()&&digit(text.charAt(p)))p++;if(begin==p)fail("Esponente incompleto");}
            if(p-start>128)fail("Numero troppo lungo");
            try{double v=Double.parseDouble(text.substring(start,p));if(Double.isInfinite(v)||Double.isNaN(v))fail("Numero non finito");return v;}catch(NumberFormatException ex){fail("Numero non valido");return null;}
        }
        private boolean literal(String s){if(text.regionMatches(p,s,0,s.length())){p+=s.length();return true;}return false;}
        private boolean take(char c){if(p<text.length()&&text.charAt(p)==c){p++;return true;}return false;}
        private void expect(char c){if(!take(c))fail("Manca «"+c+"»");}
        private void enter(){if(++depth>32)fail("JSON troppo annidato");}
        private void space(){while(p<text.length()){char c=text.charAt(p);if(c==' '||c=='\t'||c=='\n'||c=='\r')p++;else break;}}
        private static boolean digit(char c){return c>='0'&&c<='9';}
        private void fail(String message){throw invalid(message+". Posizione "+(p+1)+".");}
    }
}
