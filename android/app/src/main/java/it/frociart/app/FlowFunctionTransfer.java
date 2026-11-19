package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Pure, staged transfer: parse dependency expressions, never execute a source project. */
final class FlowFunctionTransfer {
    static final class Plan {
        final Document project;final String selectedId,baseDocument;final List<String> added,renamed;
        Plan(Document project,String selected,List<String> added,List<String> renamed,String basis){this.project=project;selectedId=selected;baseDocument=basis;this.added=Collections.unmodifiableList(new ArrayList<>(added));this.renamed=Collections.unmodifiableList(new ArrayList<>(renamed));}
        String description(){return "Funzioni incluse: "+String.join(", ",added)+"."+(renamed.isEmpty()?"":"\n\nNomi già occupati:\n"+String.join("\n",renamed))+"\n\nI diagrammi già presenti restano intatti. Puoi annullare l'importazione con un solo Annulla.";}
    }
    private static void push(ArrayDeque<FlowSource.Expression> stack,FlowSource.Expression expression){if(expression!=null)stack.push(expression);}
    private static List<Function> closure(Document source,String id){
        FlowCodec.check(source);Function selected=FlowFunctionEditing.find(source,id);if(selected==null)throw new IllegalArgumentException("Scegli una funzione presente nel progetto di origine.");
        Map<String,Function> catalog=FlowFunctions.catalog(source),byId=new HashMap<>();for(Function f:catalog.values())byId.put(f.id,f);
        Set<String> required=new HashSet<>();ArrayDeque<Function> pending=new ArrayDeque<>();pending.add(selected);
        while(!pending.isEmpty()){Function f=pending.remove();if(!required.add(f.id))continue;
            for(Node node:f.body.nodes){FlowSource.Operation op;try{op=FlowSource.compileOperation(node,catalog,f);}catch(IllegalArgumentException e){throw new IllegalArgumentException("Correggi «"+f.name+"» prima di importarla: "+e.getMessage(),e);}
                ArrayDeque<FlowSource.Expression> expressions=new ArrayDeque<>();push(expressions,op.value);if(op.input!=null)push(expressions,op.input.index);if(op.declaration!=null)push(expressions,op.declaration.size);
                for(FlowSource.Write w:op.writes){push(expressions,w.value);push(expressions,w.target.index);}
                while(!expressions.isEmpty()){FlowSource.Expression expression=expressions.pop();if(expression.functionId!=null)pending.add(byId.get(expression.functionId));for(FlowSource.Expression child:expression.children)push(expressions,child);}
            }
        }
        List<Function> result=new ArrayList<>();for(Function f:source.functions)if(required.contains(f.id))result.add(f);return result;
    }
    static Document export(Document source,String id){
        List<Function> required=closure(source,id);Document library=new Document();library.title="Funzioni · "+FlowFunctionEditing.find(source,id).name;
        Node start=new Node(START,"Inizio",30,30),end=new Node(END,"Fine",30,180);library.nodes.add(start);library.nodes.add(end);library.edges.add(new Edge(start.id,end.id,"next"));
        library.functions=new ArrayList<>();for(Function f:required)library.functions.add(f.copy());library.variablePolicy=source.variablePolicy;FlowAnnotations.promote(library);FlowCodec.check(library);
        for(FlowEngine.Issue issue:FlowEngine.validate(library))if(issue.error)throw new IllegalArgumentException("La funzione non è pronta: "+issue.message);
        if(FlowCodec.encode(library).getBytes(java.nio.charset.StandardCharsets.UTF_8).length>MAX_BYTES)throw new IllegalArgumentException("Il progetto supera il limite di 4 MB.");return library;
    }
    static Plan prepare(Document target,Document source,String id){
        FlowCodec.check(target);Document library=export(source,id),candidate=target.copy();
        Set<String> original=new TreeSet<>(String.CASE_INSENSITIVE_ORDER),used=new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
        if(target.functions!=null)for(Function f:target.functions)original.add(f.name);used.addAll(original);for(Function f:library.functions)used.add(f.name);
        Map<String,String> names=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);
        for(Function f:library.functions){String name=f.name;if(original.contains(name))for(int i=2;;i++){String suffix="_"+i,next=name.substring(0,Math.min(name.length(),64-suffix.length()))+suffix;if(used.add(next)){name=next;break;}}names.put(f.name,name);}
        if(candidate.functions==null)candidate.functions=new ArrayList<>();candidate.version=2;
        String selected=null;List<String> added=new ArrayList<>(),renamed=new ArrayList<>();
        for(Function f:library.functions){Function copy=f.copy();copy.id=id();copy.name=names.get(f.name);copy.body.title=copy.name;if(f.id.equals(id))selected=copy.id;
            if(!f.name.equals(copy.name))renamed.add(f.name+" → "+copy.name);added.add(copy.name);
            Map<String,String> ids=new HashMap<>();for(Node node:copy.body.nodes){String next=id();ids.put(node.id,next);node.id=next;if(node.kind!=START&&node.kind!=END&&node.kind!=NOTE)node.text=FlowFunctionEditing.rewrite(node.text,names,true);if(node.breakpointCondition!=null)node.breakpointCondition=FlowFunctionEditing.rewrite(node.breakpointCondition,names,true);}
            for(Edge edge:copy.body.edges){edge.id=id();edge.from=ids.get(edge.from);edge.to=ids.get(edge.to);}candidate.functions.add(copy);
        }
        FlowAnnotations.promote(candidate);FlowCodec.check(candidate);if(FlowCodec.encode(candidate).getBytes(java.nio.charset.StandardCharsets.UTF_8).length>MAX_BYTES)throw new IllegalArgumentException("Il progetto supera il limite di 4 MB.");return new Plan(candidate,selected,added,renamed,FlowCodec.encode(target));
    }
}
