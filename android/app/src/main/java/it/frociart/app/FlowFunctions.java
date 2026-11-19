package it.frociart.app;

import java.util.Arrays;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import static it.frociart.app.FlowModel.*;

/** Function signatures and format limits shared with the desktop format. */
public final class FlowFunctions {
    private FlowFunctions(){}
    public static final int MAX_FUNCTIONS=32,MAX_PARAMETERS=16,MAX_DEPTH=128;
    private static final Set<String> RESERVED=new HashSet<>(Arrays.asList(("abs sqrt floor round len sum somma min max array vettore sin cos tan arcsin arccos arctan int ln log log10 sgn random size char tochar tocode tointeger toreal tostring tofixed eof pi π main principale inizio fine leggi stampa scrivi se allora altrimenti mentre per funzione restituisci chiama").split(" ")));
    static String key(String name){return name.toLowerCase(Locale.ROOT);}
    static int projectVersion(java.util.List<Function> functions){return FlowAnnotations.version(functions,null);}
    static int projectVersion(java.util.List<Function> functions,java.util.List<Node> nodes){return FlowAnnotations.version(functions,nodes);}
    static Map<String,Function> catalog(Document project){Map<String,Function> result=new LinkedHashMap<>();if(project.functions!=null)for(Function f:project.functions)result.put(key(f.name),f);return result;}
    static void checkProject(Document project){
        if(project.functions==null||project.functions.isEmpty())return;
        if(project.version!=2&&project.version!=3&&project.version!=4&&project.version!=5)throw invalid("Un progetto con funzioni richiede il formato 2.");
        if(project.version<3)for(Function f:project.functions)if(f!=null&&f.parameters!=null)for(Parameter p:f.parameters)if(p!=null&&p.byReference)throw invalid("I parametri per riferimento richiedono il formato 3. Aggiorna il progetto prima di salvarlo.");
        if(project.functions.size()>MAX_FUNCTIONS)throw invalid("Massimo 32 funzioni per progetto.");
        Set<String> names=new HashSet<>(),ids=new HashSet<>(),nodeIds=new HashSet<>(),edgeIds=new HashSet<>();
        for(Node n:project.nodes)nodeIds.add(n.id);for(Edge e:project.edges)edgeIds.add(e.id);
        int nodes=project.nodes.size(),edges=project.edges.size();
        for(Function f:project.functions){
            if(f==null||f.id==null||f.id.trim().isEmpty()||f.id.length()>128||!ids.add(f.id))throw invalid("Identificatore di funzione mancante o duplicato.");
            if(!FlowEngine.identifier(f.name)||RESERVED.contains(key(f.name))||!names.add(key(f.name)))throw invalid("Nome di funzione non valido, riservato o duplicato. Usa un nome distinto di massimo 64 caratteri.");
            if(f.parameters==null||f.parameters.size()>MAX_PARAMETERS)throw invalid("Una funzione può avere da 0 a 16 parametri.");
            Set<String> parameters=new HashSet<>();
            for(Parameter p:f.parameters)if(p==null||!FlowEngine.identifier(p.name)||!parameters.add(key(p.name))||p.type<AUTO||p.type>=VOID||p.byReference&&!p.isArray)throw invalid("Parametro non valido o duplicato in «"+f.name+"».");
            if(f.returnType<AUTO||f.returnType>VOID||(f.returnType!=VOID&&!FlowEngine.identifier(f.returnVariable))||(f.returnType==VOID&&(f.returnIsArray||f.returnVariable!=null&&!f.returnVariable.isEmpty())))throw invalid("Definisci correttamente il risultato di «"+f.name+"».");
            if(f.body==null||f.body.variablePolicy!=null||f.body.functions!=null||f.body.version!=1&&f.body.version!=4)throw invalid("Il diagramma di una funzione non può contenere altre definizioni di funzione.");
            FlowCodec.checkGraph(f.body);if(f.body.version<4)for(Node n:f.body.nodes)if(n.kind==NOTE)throw invalid("Le note della funzione richiedono il formato 4.");nodes+=f.body.nodes.size();edges+=f.body.edges.size();
            if(nodes>MAX_NODES||edges>MAX_EDGES)throw invalid("Il progetto completo può contenere al massimo 500 blocchi e 1500 collegamenti.");
            for(Node n:f.body.nodes)if(!nodeIds.add(n.id))throw invalid("Identificatore di blocco duplicato tra funzioni.");
            for(Edge e:f.body.edges)if(!edgeIds.add(e.id))throw invalid("Identificatore di collegamento duplicato tra funzioni.");
        }
    }
    static void checkType(FlowEngine.Value value,int type,boolean array,String label){
        if(array){if(value==null||!value.isVector())throw new FlowEngine.Problem(label+" richiede un vettore.");for(int i=0;i<value.vectorSize();i++)checkType(value.element(i),type,false,label+"["+i+"]");return;}
        boolean valid=value!=null&&!value.isVector();
        if(type==VOID)valid=false;
        else if(type==INTEGER)valid=value!=null&&value.isNumber()&&Math.rint(value.asNumber())==value.asNumber();
        else if(type==REAL)valid=value!=null&&value.isNumber();
        else if(type==TEXT)valid=value!=null&&value.isText();
        else if(type==BOOLEAN)valid=value!=null&&value.isBoolean();
        if(!valid)throw new FlowEngine.Problem(label+" richiede "+typeName(type)+".");
    }
    static String typeName(int type){switch(type){case INTEGER:return "un numero intero";case REAL:return "un numero";case TEXT:return "un testo";case BOOLEAN:return "un valore logico";default:return "un valore singolo";}}
    private static IllegalArgumentException invalid(String message){return new IllegalArgumentException(message);}
}
