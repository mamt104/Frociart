package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Prepare signature edits on an isolated complete project; failures keep the original intact. */
final class FlowFunctionEditing {
    private FlowFunctionEditing(){}
    static Function find(Document project,String id){if(project!=null&&project.functions!=null&&id!=null)for(Function f:project.functions)if(id.equals(f.id))return f;return null;}
    static Document graph(Document project,String id){Function f=find(project,id);return f==null?project:f.body;}
    static List<Document> graphs(Document project){ArrayList<Document> all=new ArrayList<>();all.add(project);if(project.functions!=null)for(Function f:project.functions)all.add(f.body);return all;}
    static String ownerOf(Document project,String nodeId){if(project.functions!=null)for(Function f:project.functions)if(f.body.find(nodeId)!=null)return f.id;return null;}
    static String newName(Document project){for(int i=1;;i++){String name="miaFunzione"+(i==1?"":i);if(!FlowFunctions.catalog(project).containsKey(FlowFunctions.key(name)))return name;}}
    static Function create(Document project){Function f=new Function();f.name=newName(project);Node start=new Node(START,"Inizio",30,30),end=new Node(END,"Fine",30,180);f.body.nodes.add(start);f.body.nodes.add(end);f.body.edges.add(new Edge(start.id,end.id,"next"));return f;}
    static String rewriteCalls(String text,String oldName,String newName){Map<String,String> map=FlowVariableRename.symbolMap();map.put(oldName,newName);return rewrite(text,map,true);}
    static String rewrite(String text,Map<String,String> names,boolean calls){
        return FlowVariableRename.rewriteSymbols(text,names,calls);
    }

    static Document save(Document project,Function edited,Map<String,String> renames){
        Document candidate=project.copy();Function previous=find(candidate,edited.id),next=edited.copy();
        if(candidate.functions==null)candidate.functions=new ArrayList<>();
        if(previous==null)candidate.functions.add(next);else candidate.functions.set(candidate.functions.indexOf(previous),next);
        next.body.title=next.name;FlowAnnotations.promote(candidate);FlowCodec.check(candidate);
        Map<String,String> names=FlowVariableRename.symbolMap();if(renames!=null)names.putAll(renames);
        for(Map.Entry<String,String> rename:names.entrySet()){
            if(!FlowEngine.identifier(rename.getKey())||!FlowEngine.identifier(rename.getValue()))throw new IllegalArgumentException("Scegli nomi validi per parametri e risultato.");
            if(!names.containsKey(rename.getValue())){
                Map<String,String> probe=FlowVariableRename.symbolMap();probe.put(rename.getValue(),"__existing_local__");
                for(Node node:next.body.nodes)if(node.kind!=START&&node.kind!=END&&node.kind!=NOTE&&!FlowDeclaration.rewriteNode(node,node.text,probe).equals(node.text))throw new IllegalArgumentException("«"+rename.getValue()+"» è già usata nel diagramma. Scegli un nome libero per non unire due variabili.");
            }
        }
        for(Node node:next.body.nodes){if(node.kind!=START&&node.kind!=END&&node.kind!=NOTE)node.text=FlowDeclaration.rewriteNode(node,node.text,names);if(node.breakpointCondition!=null)node.breakpointCondition=rewrite(node.breakpointCondition,names,false);}
        if(previous!=null&&!previous.name.equals(next.name))for(Document graph:graphs(candidate))for(Node node:graph.nodes)if(node.kind!=START&&node.kind!=END&&node.kind!=NOTE)node.text=rewriteCalls(node.text,previous.name,next.name);
        FlowCodec.check(candidate);return candidate;
    }
    static Document delete(Document project,String id){
        Document candidate=project.copy();Function function=find(candidate,id);if(function==null)throw new IllegalArgumentException("La funzione non esiste più.");
        for(Document graph:graphs(candidate))if(graph!=function.body)for(Node node:graph.nodes)if(node.kind!=START&&node.kind!=END&&node.kind!=NOTE&&!rewriteCalls(node.text,function.name,"__removed_function__").equals(node.text))throw new IllegalArgumentException("«"+function.name+"» è ancora chiamata da un altro diagramma. Rimuovi prima le chiamate.");
        candidate.functions.remove(function);if(candidate.functions.isEmpty()){candidate.functions=null;FlowAnnotations.promote(candidate);}FlowCodec.check(candidate);return candidate;
    }
    /** Reversible name changes belonging to one undoable signature edit. */
    static final class Rename {
        final String functionId,oldName,newName;
        final Map<String,String> forward=FlowVariableRename.symbolMap(),backward=FlowVariableRename.symbolMap();
        private final Map<String,DraftCaseRepair> draftCases=new HashMap<>();private int repairBytes;
        Rename(String function,String from,String to){functionId=function;oldName="";newName="";forward.put(from,to);backward.put(to,from);}
        Rename(Function before,Function after,Map<String,String> variables){
            functionId=before.id;oldName=before.name;newName=after.name;if(variables!=null)forward.putAll(variables);
            for(Map.Entry<String,String> entry:forward.entrySet()){
                if(backward.containsKey(entry.getValue())&&!FlowVariableRename.sameSymbol(backward.get(entry.getValue()),entry.getKey()))throw new IllegalArgumentException("Usa nomi distinti durante la rinomina, per poter ripristinare anche le bozze con Annulla.");
                backward.put(entry.getValue(),entry.getKey());
            }
        }
        String rewriteDraft(Document project,String nodeId,String text,boolean reverse){
            String result=rewriteDraftCore(project,nodeId,text,reverse,true);
            if(reverse){DraftCaseRepair repair=draftCases.get(nodeId);return repair!=null&&repair.matches(text)?repair.restore(result):result;}
            String roundTrip=rewriteDraftCore(project,nodeId,result,true,false);
            DraftCaseRepair replacement=roundTrip.equals(text)?null:new DraftCaseRepair(roundTrip,text,result),previous=draftCases.get(nodeId);
            int next=repairBytes-(previous==null?0:previous.bytes.length)+(replacement==null?0:replacement.bytes.length);
            if(next>4*1024*1024)throw new IllegalArgumentException("Le bozze richiedono troppa memoria per una rinomina annullabile. Applica o scarta alcune bozze e riprova.");
            repairBytes=next;if(replacement==null)draftCases.remove(nodeId);else draftCases.put(nodeId,replacement);return result;
        }
        private String rewriteDraftCore(Document project,String nodeId,String text,boolean reverse,boolean validate){
            String owner=ownerOf(project,nodeId);Document graph=graph(project,owner);Node node=graph.find(nodeId);if(node==null||node.kind==START||node.kind==END||node.kind==NOTE)return text;
            // I/O commands are editor syntax, not variable references. Preserve their exact spelling/spacing.
            int prefix=0;if(isIo(node.kind)){int start=0;while(start<text.length()&&Character.isWhitespace(text.charAt(start)))start++;int end=start;while(end<text.length()&&!Character.isWhitespace(text.charAt(end)))end++;String word=text.substring(start,end);if(word.equalsIgnoreCase("leggi")||word.equalsIgnoreCase("stampa")||word.equalsIgnoreCase("scrivi")||word.equalsIgnoreCase("stampa_continua"))prefix=end;}
            String value=text.substring(prefix);Map<String,String> names=reverse?backward:forward;
            if(Objects.equals(functionId,owner)){
                for(String target:names.values())if(validate&&!names.containsKey(target)){
                    Map<String,String> probe=FlowVariableRename.symbolMap();probe.put(target,"__draft_collision__");
                    if(!FlowDeclaration.rewriteNode(node,value,probe).equals(value))throw new IllegalArgumentException("Una bozza usa già «"+target+"». Correggi o scarta quella bozza prima di rinominare o annullare la rinomina.");
                }
                value=FlowDeclaration.rewriteNode(node,value,names);
            }
            String from=reverse?newName:oldName,to=reverse?oldName:newName;
            if(validate&&!FlowVariableRename.sameSymbol(from,to)&&!rewriteCalls(value,to,"__draft_call_collision__").equals(value))throw new IllegalArgumentException("Una bozza chiama già «"+to+"». Correggi o scarta quella bozza prima di rinominare o annullare la rinomina.");
            value=rewriteCalls(value,from,to);
            String result=text.substring(0,prefix)+value;if(result.length()>8192)throw new IllegalArgumentException("La rinomina supera il limite di testo di una bozza. Accorciala prima di continuare.");return result;
        }
    }
    // Keep only compressed spelling repairs, not copies of every large draft.
    // A UTF16-code-unit digest prevents applying an old repair over newer edits,
    // including drafts containing incomplete surrogate characters.
    private static final class DraftCaseRepair {
        final byte[] digest,bytes;
        DraftCaseRepair(String roundTrip,String original,String renamed){
            digest=digest(renamed);
            try{
                java.io.ByteArrayOutputStream data=new java.io.ByteArrayOutputStream();
                try(java.io.DataOutputStream out=new java.io.DataOutputStream(new java.util.zip.DeflaterOutputStream(data))){
                    out.writeInt(roundTrip.length());boolean same=roundTrip.length()==original.length();out.writeBoolean(same);
                    if(same){for(int i=0;i<original.length();i++)if(original.charAt(i)!=roundTrip.charAt(i)){out.writeInt(i);out.writeChar(original.charAt(i));}out.writeInt(-1);}
                    else{out.writeInt(original.length());for(int i=0;i<original.length();i++)out.writeChar(original.charAt(i));}
                }bytes=data.toByteArray();
            }catch(java.io.IOException impossible){throw new IllegalArgumentException("Non riesco a preparare il ripristino della bozza. Nessuna modifica applicata.",impossible);}
        }
        private static byte[] digest(String text){
            try{java.security.MessageDigest hash=java.security.MessageDigest.getInstance("SHA-256");byte[] buffer=new byte[2048];int count=0;for(int i=0;i<text.length();i++){char c=text.charAt(i);buffer[count++]=(byte)(c>>>8);buffer[count++]=(byte)c;if(count==buffer.length){hash.update(buffer,0,count);count=0;}}if(count>0)hash.update(buffer,0,count);return hash.digest();}
            catch(java.security.NoSuchAlgorithmException impossible){throw new IllegalStateException(impossible);}
        }
        boolean matches(String text){return java.util.Arrays.equals(digest,digest(text));}
        String restore(String roundTrip){
            try(java.io.DataInputStream input=new java.io.DataInputStream(new java.util.zip.InflaterInputStream(new java.io.ByteArrayInputStream(bytes)))){
                if(input.readInt()!=roundTrip.length())throw new java.io.IOException("Draft boundary changed");
                if(input.readBoolean()){char[] text=roundTrip.toCharArray();for(int position;(position=input.readInt())!=-1;){if(position<0||position>=text.length)throw new java.io.IOException("Invalid repair boundary");text[position]=input.readChar();}return new String(text);}
                int length=input.readInt();if(length<0||length>8192)throw new java.io.IOException("Invalid draft length");char[] text=new char[length];for(int i=0;i<length;i++)text[i]=input.readChar();return new String(text);
            }catch(java.io.IOException error){throw new IllegalArgumentException("Non riesco a ripristinare la bozza esatta. La modifica non è stata annullata.",error);}
        }
    }
    static final class History {
        final Document project;final String functionId;
        Rename rename;
        FlowAppearance appearance;
        History(Document value,String active){project=value.copy();functionId=find(project,active)==null?null:active;}
    }
}
