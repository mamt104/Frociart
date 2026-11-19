package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Passive editor snapshot; never evaluates code or touches the live document. */
final class FlowExpressionLibrary {
    static final class Catalog {
        final List<String[]> entries;final String scopeName;final boolean targetOnly,callsOnly;
        Catalog(List<String[]> entries,String scope,boolean targets,boolean calls){this.entries=Collections.unmodifiableList(new ArrayList<>(entries));scopeName=scope;targetOnly=targets;callsOnly=calls;}
    }
    private static final class Symbol {String name,role="Variabile del foglio";int type=AUTO;boolean array;Symbol(String name){this.name=name;}}
    static Catalog build(Document project,String functionId,String nodeId,String draft,boolean callsOnly){return build(project,functionId,nodeId,draft,callsOnly,false);}
    static Catalog build(Document project,String functionId,String nodeId,String draft,boolean callsOnly,boolean callArguments){
        Map<String,Symbol> symbols=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);ArrayList<String[]> rows=new ArrayList<>();String scope="Principale";boolean targetOnly=false;
        if(project!=null){
            Document snapshot=FlowCodec.decode(FlowCodec.encode(project));Function owner=FlowFunctionEditing.find(snapshot,functionId);
            if(functionId!=null&&owner==null)throw new IllegalArgumentException("La funzione non è più disponibile. Riapri il blocco.");
            Document graph=FlowFunctionEditing.graph(snapshot,functionId);Node edited=graph.find(nodeId);
            if(nodeId!=null&&edited==null)throw new IllegalArgumentException("Il blocco non è più disponibile. Riapri l’editor.");
            if(edited!=null&&draft!=null){if(draft.length()>8192)throw new IllegalArgumentException("Il testo del blocco è troppo lungo.");applyText(edited,draft);}
            targetOnly=edited!=null&&(edited.kind==INPUT||edited.kind==READ_FILE);callsOnly=(callsOnly||edited!=null&&edited.kind==CALL)&&!callArguments;
            scope=owner==null?"Principale":owner.name;Map<String,Function> functions=FlowFunctions.catalog(snapshot);
            if(owner!=null){for(Parameter p:owner.parameters){Symbol s=new Symbol(p.name);s.type=p.type;s.array=p.isArray;s.role=p.byReference?"Parametro per riferimento":"Parametro";symbols.put(s.name,s);}
                if(owner.returnType!=VOID){Symbol result=symbols.get(owner.returnVariable);if(result==null){result=new Symbol(owner.returnVariable);result.type=owner.returnType;result.array=owner.returnIsArray;symbols.put(result.name,result);}result.role=result.role.startsWith("Parametro")?result.role+" · risultato":"Risultato della funzione";}}
            for(Node node:graph.nodes)try{
                FlowEngine.Block block=FlowEngine.compile(node,functions,owner);
                for(String name:block.definitions())if(!symbols.containsKey(name))symbols.put(name,new Symbol(originalName(node.text,name)));
                if(block.declaration!=null)for(String name:block.declaration.names){Symbol s=symbols.get(name);if(s==null){s=new Symbol(name);symbols.put(name,s);}if(s.type==AUTO&&s.role.equals("Variabile del foglio")){s.name=name;s.type=block.declaration.type;s.array=block.declaration.isArray();s.role="Dichiarata nel foglio";}}
            }catch(IllegalArgumentException unfinished){/* Keep definitions from other complete blocks. */}
            ArrayList<Symbol> sorted=new ArrayList<>(symbols.values());sorted.sort((a,b)->a.name.toUpperCase(Locale.ROOT).compareTo(b.name.toUpperCase(Locale.ROOT)));
            if(!callsOnly)for(Symbol s:sorted){String type=typeName(s.type)+(s.array?" · vettore":"");String description=s.role+" · "+scope+"\n"+type+". Il valore si controlla durante l’esecuzione.";
                if(!targetOnly||!s.array)rows.add(new String[]{"Variabili",s.name,description,s.name,""});
                if(s.array)rows.add(new String[]{"Variabili",s.name+"[indice]",description+"\nSostituisci 0 con l’indice desiderato. Il primo elemento ha indice 0.",s.name+"[0]",""});}
            ArrayList<Function> sortedFunctions=new ArrayList<>(snapshot.functions==null?Collections.<Function>emptyList():snapshot.functions);sortedFunctions.sort((a,b)->a.name.toUpperCase(Locale.ROOT).compareTo(b.name.toUpperCase(Locale.ROOT)));
            if(!targetOnly)for(Function f:sortedFunctions){if(!callsOnly&&f.returnType==VOID)continue;StringBuilder call=new StringBuilder(f.name).append('(');for(int i=0;i<f.parameters.size();i++){if(i>0)call.append(", ");call.append(f.parameters.get(i).name);}call.append(')');rows.add(new String[]{"Le tue funzioni",f.name,FlowPseudocode.signature(f)+"\nSostituisci i nomi dei parametri con i dati da passare.",call.toString(),""});}
        }
        if(!targetOnly&&!callsOnly)Collections.addAll(rows,FlowFunctionData.ENTRIES);
        return new Catalog(rows,scope,targetOnly,callsOnly);
    }
    private static String originalName(String text,String name){
        if(text==null)text="";int i=0;while(i<text.length()){char c=text.charAt(i++);
            if(c=='\''||c=='"'){boolean escape=false;while(i<text.length()){char next=text.charAt(i++);if(escape)escape=false;else if(next=='\\')escape=true;else if(next==c)break;}}
            else if(c>='0'&&c<='9'||c=='.'&&i<text.length()&&Character.isDigit(text.charAt(i))){while(i<text.length()&&(Character.isDigit(text.charAt(i))||text.charAt(i)=='.'))i++;if(i<text.length()&&(text.charAt(i)=='e'||text.charAt(i)=='E')){i++;if(i<text.length()&&(text.charAt(i)=='+'||text.charAt(i)=='-'))i++;while(i<text.length()&&Character.isDigit(text.charAt(i)))i++;}}
            else if(c=='_'||Character.isLetter(c)){int start=i-1;while(i<text.length()&&word(text.charAt(i)))i++;String token=text.substring(start,i);if(token.equalsIgnoreCase(name))return token;}
        }return name;
    }
    private static boolean word(char c){return Character.isLetterOrDigit(c)||c=='_';}
    private static String typeName(int type){switch(type){case INTEGER:return "Intero";case REAL:return "Reale";case TEXT:return "Testo";case BOOLEAN:return "Logico";default:return "Tipo automatico";}}
    static List<String[]> filter(Catalog catalog,String query,int group){
        String text=query==null?"":query.toLowerCase(Locale.ROOT);ArrayList<String> words=new ArrayList<>();int start=-1;
        for(int i=0;i<=text.length();i++){boolean space=i==text.length()||space(text.charAt(i));if(space){if(start>=0){words.add(text.substring(start,i));start=-1;}}else if(start<0)start=i;}
        ArrayList<String[]> result=new ArrayList<>();for(String[] row:catalog.entries){if(group==1&&!row[0].equals("Variabili")||group==2&&row[0].equals("Variabili"))continue;String haystack=String.join(" ",row).toLowerCase(Locale.ROOT);boolean matches=true;for(String word:words)if(!haystack.contains(word)){matches=false;break;}if(matches)result.add(row);}return result;
    }
    static boolean argumentsAt(String text,int start,int length){
        if(text==null||start<0||length<0||start>text.length()-length)throw new IllegalArgumentException("Selection bounds");
        int depth=0,atStart=0;char quote=0;boolean escaped=false;
        for(int i=0;i<start+length;i++){if(i==start)atStart=depth;char c=text.charAt(i);if(quote!=0){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote=0;}else if(c=='\''||c=='"')quote=c;else if(c=='(')depth++;else if(c==')')depth=Math.max(0,depth-1);}
        if(length==0)atStart=depth;return atStart>0&&depth>0;
    }
    private static boolean space(char c){return c>='\t'&&c<='\r'||c=='\u0085'||Character.isSpaceChar(c);}
}
