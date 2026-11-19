package it.frociart.app;

import java.util.*;

/** Bounded, temporary execution evidence. Never serialized into a project. */
public final class FlowConsoleLog {
    public enum EntryKind { INPUT, OUTPUT }
    public static final class Entry {
        public final long sequence;
        public final EntryKind kind;
        public final String text,prompt,variable,nodeId,functionId,functionName;
        public final int step;
        public final boolean newline,textLimited;
        final int cost;
        Entry(long sequence,EntryKind kind,String text,String prompt,String variable,String nodeId,
              String functionId,String functionName,int step,boolean newline){
            this.sequence=sequence;this.kind=kind;this.prompt=bound(prompt,1024);this.variable=bound(variable,128);
            this.nodeId=bound(nodeId,128);this.functionId=functionId==null?null:bound(functionId,128);
            this.functionName=functionName==null||functionName.isEmpty()?"Principale":bound(functionName,128);
            this.step=step;this.newline=newline;
            int metadata=this.prompt.length()+this.variable.length()+this.nodeId.length()+(this.functionId==null?0:this.functionId.length())+this.functionName.length();
            this.text=bound(text,MAX_CHARACTERS-metadata);textLimited=(text==null?0:text.length())>this.text.length();cost=metadata+this.text.length();
        }
        public String label(){return kind==EntryKind.INPUT?"LEGGI · "+variable:"STAMPA"+(newline?"":" · senza a capo");}
        public String context(){return functionName+" · passo "+step;}
        @Override public String toString(){return label()+": "+(text.isEmpty()?"(testo vuoto)":text);}
    }
    public static final int MAX_ENTRIES=4096,MAX_CHARACTERS=524288;
    private final Entry[] entries=new Entry[MAX_ENTRIES];
    private int start,count,characters,discarded;
    private long nextSequence,revision;
    public int size(){return count;}
    public int discarded(){return discarded;}
    public long revision(){return revision;}
    public Entry get(int index){if(index<0||index>=count)throw new IndexOutOfBoundsException();return entries[(start+index)%MAX_ENTRIES];}
    public Entry find(long sequence){if(count==0)return null;long index=sequence-get(0).sequence;return index<0||index>=count?null:get((int)index);}
    public List<Entry> snapshot(){List<Entry> result=new ArrayList<>(count);for(int i=0;i<count;i++)result.add(get(i));return Collections.unmodifiableList(result);}
    void add(EntryKind kind,String text,String prompt,String variable,String nodeId,String functionId,String functionName,int step,boolean newline){
        Entry entry=new Entry(++nextSequence,kind,text,prompt,variable,nodeId,functionId,functionName,step,newline);
        while(count>0&&(count==MAX_ENTRIES||characters+entry.cost>MAX_CHARACTERS)){characters-=entries[start].cost;entries[start]=null;start=(start+1)%MAX_ENTRIES;count--;discarded++;}
        entries[(start+count)%MAX_ENTRIES]=entry;count++;characters+=entry.cost;revision++;
    }
    /** Clearing presentation never resets variables, files, output or the program counter. */
    public void clear(){Arrays.fill(entries,null);start=count=characters=discarded=0;revision++;}
    public String toText(){
        StringBuilder result=new StringBuilder();if(discarded>0)result.append("[Limite console: ").append(discarded).append(" messaggi precedenti rimossi]\n\n");
        for(int i=0;i<count;i++){Entry e=get(i);result.append(e.label()).append(" — ").append(e.context()).append('\n');
            if(e.kind==EntryKind.INPUT&&!e.prompt.isEmpty())result.append(e.prompt).append('\n');if(e.kind==EntryKind.INPUT)result.append("> ");
            result.append(e.text.isEmpty()?"(testo vuoto)":e.text);if(e.textLimited)result.append("\n[Testo limitato dalla memoria della console]");result.append("\n\n");}
        return result.toString();
    }
    private static String bound(String text,int max){if(text==null)return "";if(text.length()<=max)return text;if(max>0&&Character.isHighSurrogate(text.charAt(max-1)))max--;return text.substring(0,max);}
}
