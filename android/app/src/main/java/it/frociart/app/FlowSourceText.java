package it.frociart.app;

import java.util.*;

/** UTF-16 positions match both native text widgets. Lines may map to multiple flowchart blocks. */
final class FlowSourceText {
    static final class Origin {
        final String functionId,nodeId;
        Origin(String functionId,String nodeId){this.functionId=functionId;this.nodeId=nodeId;}
        boolean matches(String functionId,String nodeId){return Objects.equals(this.functionId,functionId)&&Objects.equals(this.nodeId,nodeId);}
    }
    static final class Line {
        final int number,start,length;final List<Origin> origins;
        Line(int number,int start,int length,List<Origin> origins){this.number=number;this.start=start;this.length=length;this.origins=freeze(origins);}
    }
    final String language,text;final List<Line> lines;final List<String> notices;
    private final Map<List<String>,List<Line>> byBlock=new HashMap<>();
    private volatile FlowSourceText programView;
    // Pseudocode can describe unfinished and disconnected graphs. Its mappings
    // still refer only to real blocks, without executing or compiling a program.
    static FlowSourceText fromPseudocode(FlowModel.Document doc,String text,List<List<Origin>> mapping){
        if(doc==null||text==null||text.length()>FlowPseudocode.MAX_TEXT||text.indexOf('\r')>=0)throw new IllegalArgumentException("Pseudocodice collegato non valido.");
        Set<List<String>> valid=new HashSet<>();
        for(FlowModel.Node n:doc.nodes)valid.add(Arrays.asList(null,n.id));
        if(doc.functions!=null)for(FlowModel.Function f:doc.functions)for(FlowModel.Node n:f.body.nodes)valid.add(Arrays.asList(f.id,n.id));
        List<Line> lines=new ArrayList<>();int offset=0;
        while(offset<text.length()){
            int end=text.indexOf('\n',offset);if(end<0)throw new IllegalArgumentException("Manca la fine riga del pseudocodice.");
            if(mapping==null||lines.size()>=mapping.size())throw new IllegalArgumentException("Mappa del pseudocodice incompleta.");
            List<Origin> origins=mapping.get(lines.size());if(origins==null)throw new IllegalArgumentException("Mappa del pseudocodice non valida.");
            for(Origin origin:origins)if(origin==null||!valid.contains(Arrays.asList(origin.functionId,origin.nodeId)))throw new IllegalArgumentException("Blocco del pseudocodice non presente.");
            lines.add(new Line(lines.size()+1,offset,end-offset,origins));offset=end+1;
        }
        if(mapping==null||mapping.size()!=lines.size())throw new IllegalArgumentException("Mappa del pseudocodice non valida.");
        return new FlowSourceText("pseudocode",text,lines);
    }
    private String prefixSupportMarker(){return (language.equals("powershell")?"#":"//")+" --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---";}
    /** Remove the type-support prefix and rebuild the visible UTF16 source origins. */
    FlowSourceText programView(){
        if(!language.equals("cpp")&&!language.equals("powershell"))return this;if(programView!=null)return programView;
        String support=prefixSupportMarker(),body=(language.equals("powershell")?"#":"//")+" --- Programma ---";
        int start=text.indexOf("\n"+support+"\n");if(start<0)return programView=this;start++;
        int end=text.indexOf("\n"+body+"\n",start);if(end<0)return programView=this;end++;
        StringBuilder result=new StringBuilder();List<Line> visible=new ArrayList<>();
        for(Line line:lines){if(line.start>=start&&line.start<end)continue;visible.add(new Line(visible.size()+1,result.length(),line.length,line.origins));result.append(text,line.start,line.start+line.length).append('\n');}
        return programView=new FlowSourceText(language,result.toString(),visible,notices);
    }
    private FlowSourceText(String language,String text,List<Line> lines){this(language,text,lines,Collections.emptyList());}
    private FlowSourceText(String language,String text,List<Line> lines,Collection<String> notices){this.language=language;this.text=text;this.lines=freeze(lines);this.notices=freeze(new TreeSet<>(notices));
        for(Line line:lines)for(Origin origin:line.origins){List<String> key=Arrays.asList(origin.functionId,origin.nodeId);List<Line> found=byBlock.get(key);if(found==null){found=new ArrayList<>();byBlock.put(key,found);}found.add(line);}
        for(Map.Entry<List<String>,List<Line>> entry:byBlock.entrySet())entry.setValue(freeze(entry.getValue()));
    }
    List<Line> forBlock(String functionId,String nodeId){List<Line> result=byBlock.get(Arrays.asList(functionId,nodeId));return result==null?Collections.<Line>emptyList():result;}
    FlowSourceText withNotices(Collection<String> notices){return new FlowSourceText(language,text,lines,notices);}
    /** Reading anchors retain exact projected offsets and semantic positions after regeneration. */
    static final class Position {
        final FlowSourceText owner,view;final int offset,lineNumber,column,index;final Origin origin;final boolean comment,atVisibleEnd;
        Position(FlowSourceText owner,FlowSourceText view,int offset,int visibleLength){
            this.owner=owner;this.view=view;this.offset=Math.max(0,Math.min(offset,view.text.length()));atVisibleEnd=this.offset==visibleLength;
            Line line=view.atOffset(Math.min(this.offset,Math.max(0,view.text.length()-1)));
            lineNumber=line==null?0:line.number;column=line==null?0:Math.max(0,this.offset-line.start);origin=line==null||line.origins.isEmpty()?null:line.origins.get(0);comment=line!=null&&view.isComment(line);
            int found=0;if(origin!=null)for(Line candidate:view.forBlock(origin.functionId,origin.nodeId)){if(candidate==line)break;if(view.isComment(candidate)==comment)found++;}index=found;
        }
    }
    Position capture(FlowSourceText view,int offset,int visibleLength){return view==null||view.text.isEmpty()||visibleLength<=0?null:new Position(this,view,offset,visibleLength);}
    Integer restore(FlowSourceText view,Position position,int visibleLength){
        if(view==null||position==null)return null;visibleLength=Math.max(0,Math.min(visibleLength,view.text.length()));
        if(position.owner==this){int offset=position.offset;FlowSourceText compact=programView();int removed=text.length()-compact.text.length();
            if(removed>0){int start=text.indexOf("\n"+prefixSupportMarker()+"\n")+1;
                if(position.view.text.length()<text.length()&&offset>=start)offset+=removed;
                if(view.text.length()<text.length()&&offset>=start)offset=offset<start+removed?start:offset-removed;
            }
            return Math.max(0,Math.min(offset,visibleLength));
        }
        if(position.atVisibleEnd)return visibleLength;Line chosen=null;
        if(position.origin!=null){List<Line> all=view.forBlock(position.origin.functionId,position.origin.nodeId),candidates=new ArrayList<>();for(Line line:all)if(view.isComment(line)==position.comment)candidates.add(line);if(candidates.isEmpty())candidates.addAll(all);if(candidates.isEmpty())return null;
            Line old=position.lineNumber>0&&position.lineNumber<=position.view.lines.size()?position.view.lines.get(position.lineNumber-1):null;String prior=old==null?null:position.view.text.substring(old.start,old.start+old.length);int distance=Integer.MAX_VALUE;
            for(int i=0;i<candidates.size();i++){Line line=candidates.get(i);if(view.text.substring(line.start,line.start+line.length).equals(prior)&&Math.abs(i-position.index)<distance){chosen=line;distance=Math.abs(i-position.index);}}
            if(chosen==null)chosen=candidates.get(Math.min(position.index,candidates.size()-1));
        }else if(language.equals(position.owner.language)&&!view.lines.isEmpty())chosen=view.lines.get(Math.min(Math.max(0,position.lineNumber-1),view.lines.size()-1));
        return chosen==null?null:Math.min(visibleLength,chosen.start+Math.min(position.column,chosen.length));
    }
    Line openingLine(String function,String node){
        List<Line> exact=node==null?Collections.emptyList():forBlock(function,node);for(Line line:exact)if(!isComment(line))return line;if(!exact.isEmpty())return exact.get(0);
        Line first=null;for(Line line:lines)for(Origin origin:line.origins)if(Objects.equals(origin.functionId,function)){if(!isComment(line))return line;if(first==null)first=line;break;}if(first!=null)return first;
        for(Line line:lines)if(!line.origins.isEmpty()&&!isComment(line))return line;for(Line line:lines)if(!line.origins.isEmpty())return line;return lines.isEmpty()?null:lines.get(0);
    }
    private boolean isComment(Line line){String value=text.substring(line.start,line.start+line.length).trim();return value.isEmpty()||value.startsWith("//")||value.startsWith("#")||value.startsWith("'")||value.startsWith("/*")||value.startsWith("*")||value.startsWith("--");}
    Line atOffset(int offset){
        if(offset<0||offset>=text.length())return null;int lo=0,hi=lines.size()-1;
        while(lo<=hi){int mid=lo+(hi-lo)/2;Line line=lines.get(mid);
            if(offset<line.start)hi=mid-1;else if(offset>line.start+line.length)lo=mid+1;else return line;}
        return null;
    }
    private static <T> List<T> freeze(Collection<T> values){return Collections.unmodifiableList(new ArrayList<T>(values));}
    static final class Builder {
        static final int MAX_CHARACTERS=2097152,MAX_LINES=50000;
        private final FlowSource.Program program;private final String language;
        private final StringBuilder text=new StringBuilder();private final List<Line> lines=new ArrayList<>();
        Builder(FlowSource.Program program,String language){
            if(program==null||!program.canGenerate())throw new IllegalArgumentException("Correggi il diagramma prima di generare il codice.");
            if(language==null||language.trim().isEmpty()||language.length()>64)throw new IllegalArgumentException("Linguaggio di destinazione non valido.");
            this.program=program;this.language=language;
        }
        void addLine(String code,int indent,Origin... origins){
            if(code==null||code.indexOf('\r')>=0||code.indexOf('\n')>=0)throw new IllegalArgumentException("Aggiungi una sola riga di codice per volta.");
            if(indent<0||indent>128)throw new IllegalArgumentException("Rientro non valido.");
            if(lines.size()>=MAX_LINES||code.length()>MAX_CHARACTERS-text.length()-indent*4-1)
                throw new IllegalArgumentException("Il codice generato supera il limite di dimensione.");
            List<Origin> unique=new ArrayList<>();
            if(origins!=null)for(Origin origin:origins){
                FlowSource.Scope scope=null;if(origin!=null)for(FlowSource.Scope s:program.scopes)if(Objects.equals(s.id,origin.functionId)){scope=s;break;}
                boolean exists=scope!=null&&scope.find(origin.nodeId)!=null;
                if(scope!=null&&!exists)for(FlowSource.Block note:scope.notes)if(Objects.equals(note.id,origin.nodeId)){exists=true;break;}
                if(!exists)throw new IllegalArgumentException("La riga fa riferimento a un blocco non presente nel programma.");
                boolean duplicate=false;for(Origin o:unique)if(o.matches(origin.functionId,origin.nodeId)){duplicate=true;break;}
                if(!duplicate)unique.add(origin);
            }
            Line line=new Line(lines.size()+1,text.length(),indent*4+code.length(),unique);
            for(int i=0;i<indent*4;i++)text.append(' ');text.append(code).append('\n');lines.add(line);
        }
        FlowSourceText build(){return new FlowSourceText(language,text.toString(),lines);}
    }
}
