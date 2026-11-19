package it.frociart.app;

import java.util.*;
import java.util.function.BooleanSupplier;
import java.util.concurrent.CancellationException;

/** Immutable, transient text index. No model references, writes, regex or file access. */
public final class FlowSearch {
    public static final int MAX_QUERY=256, MAX_RESULTS=200;
    public static final class Entry {
        public final String functionId,nodeId,graph,kind,field,text;
        Entry(String function,String node,String graph,String kind,String field,String text){functionId=function;nodeId=node;this.graph=graph;this.kind=kind;this.field=field;this.text=text;}
    }
    public static final class Hit {
        public final Entry origin;public final int start,length,snippetStart;public final String snippet;
        Hit(Entry e,int start,int length){origin=e;this.start=start;this.length=length;
            int from=Math.max(0,start-36),to=Math.min(e.text.length(),start+length+72);
            if(from>0&&Character.isLowSurrogate(e.text.charAt(from))&&Character.isHighSurrogate(e.text.charAt(from-1)))from--;
            if(to<e.text.length()&&to>0&&Character.isHighSurrogate(e.text.charAt(to-1)))to++;
            String left=from>0?"…":"";StringBuilder text=new StringBuilder(left);
            for(int i=from;i<to;i++){char c=e.text.charAt(i);text.append(Character.isISOControl(c)?' ':c);}if(to<e.text.length())text.append('…');
            snippet=text.toString();snippetStart=left.length()+start-from;
        }
        public String context(){return origin.graph+" · "+origin.kind+" · "+origin.field;}
        @Override public String toString(){return context()+": "+snippet;}
    }
    public static final class Results {
        public final List<Hit> hits;public final boolean limited;
        Results(ArrayList<Hit> hits,boolean limited){this.hits=Collections.unmodifiableList(hits);this.limited=limited;}
        public String summary(){return limited?"Primi "+MAX_RESULTS+" risultati · restringi la ricerca":hits.isEmpty()?"Nessun risultato":hits.size()==1?"1 risultato":hits.size()+" risultati";}
    }
    private final List<Entry> entries;
    private FlowSearch(ArrayList<Entry> entries){this.entries=Collections.unmodifiableList(entries);}
    public static FlowSearch capture(FlowModel.Document project){ArrayList<Entry> entries=new ArrayList<>();addGraph(entries,project,null,"Principale");
        if(project.functions!=null)for(FlowModel.Function f:project.functions){add(entries,f.id,null,f.name,"Funzione","Definizione",FlowPseudocode.signature(f));addGraph(entries,f.body,f.id,f.name);}return new FlowSearch(entries);
    }
    private static void addGraph(ArrayList<Entry> entries,FlowModel.Document graph,String function,String name){for(FlowModel.Node n:graph.nodes){String kind=FlowModel.kindName(n.kind);
        if(n.kind!=FlowModel.PAUSE)add(entries,function,n.id,name,kind,"Testo",FlowModel.editorText(n));
        add(entries,function,n.id,name,kind,"Domanda di lettura",n.prompt);add(entries,function,n.id,name,kind,"Nota",n.comment);
        if(n.kind==FlowModel.DECISION){add(entries,function,n.id,name,kind,"Ramo vero",n.trueLabel==null?"Sì":n.trueLabel);add(entries,function,n.id,name,kind,"Ramo falso",n.falseLabel==null?"No":n.falseLabel);}
        add(entries,function,n.id,name,kind,"Condizione di pausa",n.breakpointCondition);
    }}
    private static void add(ArrayList<Entry> entries,String function,String node,String graph,String kind,String field,String text){if(!blank(text))entries.add(new Entry(function,node,graph,kind,field,text));}
    public Results find(String query,boolean matchCase,boolean wholeWord,boolean currentOnly,String function,BooleanSupplier cancelled){
        if(query!=null&&query.length()>MAX_QUERY)throw new IllegalArgumentException("La ricerca può contenere al massimo 256 caratteri.");
        ArrayList<Hit> result=new ArrayList<>();if(blank(query))return new Results(result,false);
        int[] pattern=new int[query.codePointCount(0,query.length())];int p=0;
        for(int at=0;at<query.length();){int point=query.codePointAt(at);pattern[p++]=fold(point,matchCase);at+=Character.charCount(point);}
        int[] prefix=new int[pattern.length];for(int i=1,j=0;i<pattern.length;i++){while(j>0&&pattern[i]!=pattern[j])j=prefix[j-1];if(pattern[i]==pattern[j])j++;prefix[i]=j;}
        for(Entry e:entries){if(cancelled.getAsBoolean())throw new CancellationException();if(currentOnly&&!Objects.equals(e.functionId,function))continue;
            int[] found=findIn(e.text,pattern,prefix,matchCase,wholeWord,cancelled);
            if(found!=null){if(result.size()==MAX_RESULTS)return new Results(result,true);result.add(new Hit(e,found[0],found[1]));}
        }return new Results(result,false);
    }
    // Linear-time literal matching, including long nearly-matching inputs.
    // Keep UTF-16 source offsets while comparing complete Unicode code points.
    private static int[] findIn(String text,int[] pattern,int[] prefix,boolean matchCase,boolean wholeWord,BooleanSupplier cancelled){
        int matched=0,number=0;int[] starts=new int[pattern.length];
        for(int at=0;at<text.length();number++){
            if((number&255)==0&&cancelled.getAsBoolean())throw new CancellationException();
            starts[number%pattern.length]=at;int point=text.codePointAt(at),end=at+Character.charCount(point),value=fold(point,matchCase);
            while(matched>0&&value!=pattern[matched])matched=prefix[matched-1];if(value==pattern[matched])matched++;
            if(matched==pattern.length){int start=starts[(number-pattern.length+1)%pattern.length];if(!wholeWord||!wordBefore(text,start)&&!wordAt(text,end))return new int[]{start,end-start};matched=prefix[matched-1];}
            at=end;
        }return null;
    }
    private static int fold(int point,boolean matchCase){return matchCase?point:Character.toLowerCase(Character.toUpperCase(point));}
    static boolean blank(String text){if(text==null||text.isEmpty())return true;for(int i=0;i<text.length();i++){char c=text.charAt(i);if(!(c>=9&&c<=13||c==' '||c==0x85||c==0xA0||c==0x1680||c>=0x2000&&c<=0x200A||c==0x2028||c==0x2029||c==0x202F||c==0x205F||c==0x3000))return false;}return true;}
    private static boolean wordBefore(String text,int at){return at>0&&word(text.codePointBefore(at));}
    private static boolean wordAt(String text,int at){return at<text.length()&&word(text.codePointAt(at));}
    private static boolean word(int point){int type=Character.getType(point);return Character.isLetterOrDigit(point)||type==Character.NON_SPACING_MARK||type==Character.COMBINING_SPACING_MARK||type==Character.CONNECTOR_PUNCTUATION;}
}
