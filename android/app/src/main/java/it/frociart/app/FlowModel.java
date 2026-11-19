package it.frociart.app;

import java.util.ArrayList;
import java.util.UUID;

/** The portable .frociart model; coordinates and enum values match the desktop format. */
public final class FlowModel {
    private FlowModel() {}
    public static final int START=0, END=1, PROCESS=2, DECISION=3, INPUT=4, OUTPUT=5, CALL=6, DECLARE=7, OPEN_FILE=8, READ_FILE=9, WRITE_FILE=10, CLOSE_FILE=11, TURTLE_FORWARD=12, TURTLE_TURN=13, TURTLE_HOME=14, TURTLE_CLEAR=15, TURTLE_PEN=16, PAUSE=17, NOTE=18;
    public static boolean isKnownKind(int kind) { return kind>=START && kind<=NOTE; }
    public static final int AUTO=0, INTEGER=1, REAL=2, TEXT=3, BOOLEAN=4, VOID=5;
    public static final int MAX_NODES=500, MAX_EDGES=1500, MAX_BYTES=4*1024*1024, MAX_STEPS=10000;
    public static String id() { return UUID.randomUUID().toString().replace("-", ""); }
    public static String kindName(int kind) { switch(kind) {case START:return "Inizio";case END:return "Fine";case PROCESS:return "Operazione";case DECISION:return "Decisione";case INPUT:return "Input";case OUTPUT:return "Output";case DECLARE:return "Dichiara";case PAUSE:return "Pausa";case NOTE:return "Nota libera";case CALL:return "Chiamata";case TURTLE_FORWARD:return "Avanza";case TURTLE_TURN:return "Ruota";case TURTLE_HOME:return "Origine tartaruga";case TURTLE_CLEAR:return "Pulisci disegno";case TURTLE_PEN:return "Colore penna";case OPEN_FILE:return "Apri file";case READ_FILE:return "Leggi file";case WRITE_FILE:return "Scrivi file";case CLOSE_FILE:return "Chiudi file";default:return "Blocco";} }
    public static float width(int kind) { return kind==START || kind==END ? 164f : kind==PROCESS || kind==CALL || kind==DECLARE ? 212f : kind==DECISION ? 232f : 226f; }
    public static float height(int kind) { return kind==START || kind==END ? 58f : kind==DECISION ? 136f : kind==PROCESS || kind==CALL || kind==DECLARE ? 88f : 92f; }
    public static boolean separateIo;
    public static boolean isIo(int kind){return kind==INPUT||kind==OUTPUT;}
    public static final String IO_HINT="Scrivi leggi numero per ricevere un dato, oppure stampa numero per mostrarlo. Per un messaggio: stampa \"Ciao!\".";
    public static final class IoText {
        public final int kind;public final String text;
        IoText(int original,String value){int k=original;String t=value==null?"":value;
            if(isIo(original)){String s=t.trim();int end=0;while(end<s.length()&&!Character.isWhitespace(s.charAt(end)))end++;String word=s.substring(0,end);
                if(word.equalsIgnoreCase("leggi")){k=INPUT;t=s.substring(end).trim();}
                else if(word.equalsIgnoreCase("stampa_continua")){k=OUTPUT;t=FlowOutput.store(s.substring(end).trim(),false);}
                else if(word.equalsIgnoreCase("stampa")||word.equalsIgnoreCase("scrivi")){k=OUTPUT;t=s.substring(end).trim();}}
            kind=k;text=t;
        }
    }
    public static IoText ioText(Node n,String value){return new IoText(n.kind,value);}
    public static String editorText(Node n){return isIo(n.kind)?(n.kind==INPUT?"leggi "+n.text:FlowOutput.editor(n.text)):n.text;}
    public static boolean editChanged(Node n,String value){IoText parsed=ioText(n,value);return parsed.kind!=n.kind||!parsed.text.equals(n.text);}
    public static void applyText(Node n,String value){IoText parsed=ioText(n,value);n.kind=parsed.kind;n.text=parsed.text;}
    public static int visualKind(Node n){return n.previewText==null||!isIo(n.kind)?n.kind:ioText(n,n.previewText).kind;}
    public static final class Node {
        public String id=FlowModel.id(), text="", prompt="";
        public String trueLabel="Sì", falseLabel="No", trueColor="#39B985", falseColor="#EF6C82";
        public boolean breakpoint,foldedCycle;
        public String breakpointCondition,comment;
        public int kind=PROCESS;
        public float x,y;
        transient String previewText,previewComment;
        transient volatile NodeSize measuredSize;
        public String displayText(){if(kind==PAUSE){String condition=previewText==null?breakpointCondition:previewText;String body=!breakpoint?"Disattivata":condition==null||condition.trim().isEmpty()?"Sempre":"Se "+condition;String note=previewComment==null?comment:previewComment;return note==null||note.isEmpty()?body:body+"\n\nNota: "+note;}String body=previewText==null?(separateIo?(kind==OUTPUT?FlowOutput.display(text):(text==null?"":text)):editorText(this)):previewText;String note=previewComment==null?comment:previewComment;return note==null||note.isEmpty()?body:body+"\n\nNota: "+note;}
        public Node() {}
        public Node(int kind, String text, float x, float y) { this.kind=kind;this.text=text;this.x=x;this.y=y;this.breakpoint=kind==PAUSE; }
        public Node copy() { Node n=new Node(kind,text,x,y);n.id=id;n.prompt=prompt;n.trueLabel=trueLabel;n.falseLabel=falseLabel;n.trueColor=trueColor;n.falseColor=falseColor;n.breakpoint=breakpoint;n.breakpointCondition=breakpointCondition;n.comment=comment;n.foldedCycle=foldedCycle;return n; }
    }

    /** Portable derived layout; Android installs its native font measurer before drawing. */
    public interface TextMetrics {float measure(String value,int kind);}
    private static TextMetrics textMetrics=(value,kind)->{float result=0;for(int i=0;i<value.length();){int cp=value.codePointAt(i);i+=Character.charCount(cp);result+=cp==' '?3.8f:"ilI.,:;'!|".indexOf(cp)>=0?3.8f:"MW@#".indexOf(cp)>=0?12:cp>0x2FFF?14:7.5f;}return result;};
    public static final class Sizing {
        final TextMetrics metrics;public final float scale;
        public Sizing(TextMetrics metrics,float scale){if(metrics==null||!Float.isFinite(scale)||scale<.8f||scale>2)throw new IllegalArgumentException("Misure del testo non valide.");this.metrics=metrics;this.scale=scale;}
    }
    private static volatile Sizing currentSizing=new Sizing(textMetrics,1);
    public static Sizing sizing(){return currentSizing;}
    public static void setSizing(Sizing sizing){if(sizing==null)throw new IllegalArgumentException("Misure del testo non valide.");currentSizing=sizing;}
    public static void setTextMetrics(TextMetrics metrics){setSizing(new Sizing(metrics,1));}
    public static final class NodeSize {
        public final float width,height,bodyWidth;
        public final String[] lines;
        final String text;final int kind;final Sizing sizing;
        NodeSize(Node n,Sizing sizing){text=n.displayText();kind=n.kind;this.sizing=sizing;float scale=sizing.scale;TextMetrics textMetrics=sizing.metrics;
            boolean diamond=kind==DECISION,terminal=kind==START||kind==END;
            float pad=(terminal?64:kind==INPUT||kind==OUTPUT?62:36)*scale;
            String value=(text.isEmpty()?placeholder(kind):text).replace("\r\n","\n").replace('\r','\n').replace("\t","    ");
            float longest=0;if(!text.trim().isEmpty())for(String line:value.split("\n",-1))longest=Math.max(longest,textMetrics.measure(line,kind));
            float desiredWidth=(float)Math.ceil(Math.max(FlowModel.width(kind)*scale,Math.min((diamond?620:520)*scale,diamond?(longest+6*scale)/.56f:longest+pad+6*scale)));
            // Preserve a balanced diamond for explicit multiline conditions.
            int explicitLines=value.split("\n",-1).length;if(diamond)desiredWidth=Math.max(desiredWidth,Math.min(620*scale,Math.max(136*scale,(explicitLines*17.5f+8)*scale/.34f)*1.55f));
            width=desiredWidth;bodyWidth=diamond?width*.56f:width-pad;
            java.util.ArrayList<String> wrapped=new java.util.ArrayList<>();
            for(String paragraph:value.split("\n",-1)){
                int start=0;if(paragraph.isEmpty()){wrapped.add("");continue;}
                while(start<paragraph.length()){
                    int lo=start+1,hi=paragraph.length(),end=start+1;
                    while(lo<=hi){int mid=(lo+hi)/2;if(textMetrics.measure(paragraph.substring(start,mid),kind)<=bodyWidth-2*scale){end=mid;lo=mid+1;}else hi=mid-1;}
                    if(end<paragraph.length()&&Character.isHighSurrogate(paragraph.charAt(end-1)))end--;
                    if(end<=start)end=Math.min(paragraph.length(),start+Character.charCount(paragraph.codePointAt(start)));
                    if(end<paragraph.length()){int space=paragraph.lastIndexOf(' ',end-1);if(space>start+(end-start)/3)end=space;}
                    wrapped.add(paragraph.substring(start,end));start=end;while(start<paragraph.length()&&paragraph.charAt(start)==' ')start++;
                }
            }
            lines=wrapped.toArray(new String[0]);float body=lines.length*17.5f*scale;
            height=(float)Math.ceil(Math.max(diamond?Math.max(136*scale,width/1.71f):FlowModel.height(kind)*scale,diamond?(body+8*scale)/.34f:body+(terminal?20:47)*scale));
        }
    }
    public static String placeholder(int kind){return kind==NOTE?"Scrivi una nota":kind==START?"Inizio":kind==END?"Fine":kind==INPUT?"Nome variabile":kind==DECISION?"Scrivi una condizione":kind==PROCESS?"Scrivi un'operazione":kind==DECLARE?"nome: TIPO":kind==CALL?"nomeFunzione()":"Valore da mostrare";}
    public static NodeSize size(Node n){return size(n,currentSizing);}
    public static NodeSize size(Node n,Sizing sizing){String text=n.displayText();NodeSize s=n.measuredSize;if(s==null||s.kind!=n.kind||s.sizing!=sizing||!s.text.equals(text))n.measuredSize=s=new NodeSize(n,sizing);return s;}
    public static float width(Node n){return size(n).width;}
    public static float height(Node n){return size(n).height;}
    public static float width(Node n,Sizing sizing){return size(n,sizing).width;}
    public static float height(Node n,Sizing sizing){return size(n,sizing).height;}
    public static final class Edge {
        public String id=FlowModel.id(), from="", to="", branch="next";
        public Edge() {}
        public Edge(String from,String to,String branch) {this.from=from;this.to=to;this.branch=branch;}
        public Edge copy() {Edge e=new Edge(from,to,branch);e.id=id;return e;}
    }
    public static final class Document {
        public int version=1;
        public String variablePolicy;
        public boolean requiresDeclarations(){return "declared".equals(variablePolicy);}
        public String title="Nuovo diagramma";
        public final ArrayList<Node> nodes=new ArrayList<>();
        public final ArrayList<Edge> edges=new ArrayList<>();
        public ArrayList<Function> functions;
        public int totalNodes(){int count=nodes.size();if(functions!=null)for(Function f:functions)count+=f.body.nodes.size();return count;}
        public int totalEdges(){int count=edges.size();if(functions!=null)for(Function f:functions)count+=f.body.edges.size();return count;}
        public Node find(String id) {if(id==null)return null;for(Node n:nodes)if(id.equals(n.id))return n;return null;}
        public Edge outgoing(String id,String branch) {for(Edge e:edges)if(id.equals(e.from)&&branch.equals(e.branch))return e;return null;}
        public Document copy() {FlowCodec.check(this);Document d=new Document();d.version=version;d.variablePolicy=variablePolicy;d.title=title;for(Node n:nodes)d.nodes.add(n.copy());for(Edge e:edges)d.edges.add(e.copy());if(functions!=null){d.functions=new ArrayList<>();for(Function f:functions)d.functions.add(f.copy());}return d;}
    }
    public static final class Parameter {
        public String name="valore";
        public int type=AUTO;
        public boolean isArray,byReference;
        public Parameter copy(){Parameter p=new Parameter();p.name=name;p.type=type;p.isArray=isArray;p.byReference=byReference;return p;}
    }
    public static final class Function {
        public String id=FlowModel.id(),name="miaFunzione",returnVariable="";
        public int returnType=VOID;
        public boolean returnIsArray;
        public ArrayList<Parameter> parameters=new ArrayList<>();
        public Document body=new Document();
        public Function copy(){Function f=new Function();f.id=id;f.name=name;f.returnVariable=returnVariable;f.returnType=returnType;f.returnIsArray=returnIsArray;f.body=body.copy();for(Parameter p:parameters)f.parameters.add(p.copy());return f;}
    }
}

