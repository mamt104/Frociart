package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Six real, offline construction tasks. Checks execute a copy, never the user's document. */
public final class FlowPractice {
    private FlowPractice() {}
    public static final class Mission {
        public final String id,title,goal,hint;
        public final boolean repair;
        private final String[][] inputs,expected;
        private final boolean decision,cycle;
        private Mission(String id,String title,String goal,String hint,boolean repair,boolean decision,boolean cycle,String[][] inputs,String[][] expected){
            this.id=id;this.title=title;this.goal=goal;this.hint=hint;this.repair=repair;this.decision=decision;this.cycle=cycle;this.inputs=inputs;this.expected=expected;
        }
        public int cases(){return inputs.length;}
        public String example(){return "Input: "+show(inputs[inputs.length-1])+"\nOutput: "+show(expected[expected.length-1]);}
    }
    private static String[] values(String... s){return s;}
    private static final Mission[] MISSIONS={
        new Mission("u01","Somma due numeri","Leggi a e b. Calcola la loro somma in un blocco Operazione, poi mostrala con un Output.","Tocca b e usa il +: inserisci Operazione con totale = a + b. Dal suo + aggiungi Output con totale. Conserva il collegamento a Fine.",false,false,false,
            new String[][]{values("2","3"),values("0","0"),values("-4","9"),values("1.5","2.5")},new String[][]{values("5"),values("0"),values("5"),values("4")}),
        new Mission("u02","Da minuti a secondi","Leggi minuti. Calcola secondi = minuti * 60 in un blocco Operazione e mostra secondi.","Una variabile cambia con l'assegnazione. Inserisci Operazione dopo minuti, poi Output. Prova anche 0 e un minuto e mezzo.",false,false,false,
            new String[][]{values("0"),values("1"),values("1.5"),values("12")},new String[][]{values("0"),values("60"),values("90"),values("720")}),
        new Mission("u03","Maggiorenne o minorenne","Leggi eta. Con una Decisione scegli: da 18 in su mostra il testo \"maggiorenne\"; sotto 18 mostra \"minorenne\". Chiudi entrambi i rami su Fine.","La condizione è eta >= 18. I testi negli Output vanno fra virgolette. Per il secondo ramo usa Collega o il + della Decisione e scegli No.",false,true,false,
            new String[][]{values("0"),values("17"),values("17.9"),values("18"),values("19"),values("100")},new String[][]{values("minorenne"),values("minorenne"),values("minorenne"),values("maggiorenne"),values("maggiorenne"),values("maggiorenne")}),
        new Mission("u04","Somma da 1 a N","Leggi un intero n maggiore o uguale a 0. Costruisci un ciclo che sommi i numeri da 1 a n. Alla fine mostra solo la somma: con n = 0 il risultato è 0.","Inizializza somma = 0 e i = 1. Chiedi i <= n. Nel ramo Sì aggiorna somma = somma + i e i = i + 1, poi torna alla Decisione. Il ramo No mostra somma.",false,true,true,
            new String[][]{values("0"),values("1"),values("2"),values("5"),values("10")},new String[][]{values("0"),values("1"),values("3"),values("15"),values("55")}),
        new Mission("u05","Ripara il conto alla rovescia","Il flowchart stampa uno zero di troppo. Correggilo: da n deve mostrare n, n - 1, fino a 1. Con n = 0 non deve stampare niente.","Confronta l'ultimo giro corretto con il primo sbagliato. Deve entrare nel ciclo quando n è ancora positivo. Usa Prova e Passo per trovare il punto.",true,true,true,
            new String[][]{values("0"),values("1"),values("3"),values("5")},new String[][]{values(),values("1"),values("3","2","1"),values("5","4","3","2","1")}),
        new Mission("u06","Report su tre numeri","Leggi a, b e c. Mostra tre risultati, uno per Output, in questo ordine: somma, massimo, quanti sono strettamente positivi. Usa almeno una Decisione per il conteggio.","Separa i compiti: somma = a + b + c; massimo = max(a,b,c); positivi = 0. Per ciascun valore chiedi se è > 0 e incrementa positivi nel ramo Sì. Poi mostra i tre risultati. Qui lavori su tre valori: liste e funzioni personalizzate restano concetti di studio.",false,true,false,
            new String[][]{values("1","2","3"),values("-3","-1","-2"),values("0","0","0"),values("-2","0","5"),values("7","7","-4")},new String[][]{values("6","3","3"),values("-6","-1","0"),values("0","0","0"),values("3","5","1"),values("10","7","2")})
    };
    public static Mission[] all(){return MISSIONS.clone();}
    public static Mission mission(String id){for(Mission m:MISSIONS)if(m.id.equals(id))return m;return null;}
    public static Mission forLesson(FlowLearning.Lesson lesson){return lesson==null?null:mission(lesson.unitId);}
    private static Node add(Document d,int kind,String text){Node n=new Node(kind,text,0,d.nodes.size()*160);d.nodes.add(n);return n;}
    private static void edge(Document d,Node from,Node to,String branch){d.edges.add(new Edge(from.id,to.id,branch));}
    public static Document seed(Mission m){
        if(m==null)throw new IllegalArgumentException("Laboratorio non disponibile.");
        Document d=new Document();d.title=m.title;Node start=add(d,START,"Inizio"),last=start;
        String[] names=m.id.equals("u01")?values("a","b"):m.id.equals("u02")?values("minuti"):m.id.equals("u03")?values("eta"):m.id.equals("u06")?values("a","b","c"):values("n");
        for(String name:names){Node input=add(d,INPUT,name);input.prompt="Inserisci "+name;edge(d,last,input,"next");last=input;}
        if(m.repair){Node condition=add(d,DECISION,"n >= 0"),output=add(d,OUTPUT,"n"),update=add(d,PROCESS,"n = n - 1"),end=add(d,END,"Fine");edge(d,last,condition,"next");edge(d,condition,output,"true");edge(d,output,update,"next");edge(d,update,condition,"next");edge(d,condition,end,"false");}
        else{Node end=add(d,END,"Fine");edge(d,last,end,"next");}
        FlowAutoLayout.apply(d);return d;
    }
    public static final class Result {
        public final boolean success;public final int passed,total;public final String title,detail,nodeId;
        private Result(boolean success,int passed,int total,String title,String detail,String nodeId){this.success=success;this.passed=passed;this.total=total;this.title=title;this.detail=detail;this.nodeId=nodeId;}
    }
    private static Result fail(Mission m,int passed,String title,String detail,String node){return new Result(false,passed,m.cases(),title,detail,node);}
    private static String show(String[] values){return values.length==0?"nessuno":String.join(" · ",values);}
    /** Bounded checks on explicit examples, not a proof for every possible input. */
    public static Result check(Mission m,Document source){
        if(m==null)return new Result(false,0,0,"Laboratorio non disponibile","Apri un laboratorio dal percorso Impara.",null);
        if(source==null)return fail(m,0,"Manca il diagramma","Torna al foglio e aggiungi i blocchi.",null);
        Document d=source.copy();
        for(FlowEngine.Issue issue:FlowEngine.validate(d))if(issue.error)return fail(m,0,"C’è un punto da correggere",issue.message+(issue.suggestion==null?"":"\n"+issue.suggestion),issue.nodeId);
        Set<Integer> used=new HashSet<>();boolean repeated=false;int passed=0;
        for(int c=0;c<m.cases();c++){
            FlowEngine.Runner r=new FlowEngine.Runner(d);int input=0;Set<String> visited=new HashSet<>();
            while(r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR&&r.steps<2000){
                String id=r.currentNodeId;int before=r.steps;
                if(r.state==FlowEngine.State.WAITING){
                    if(input>=m.inputs[c].length)return fail(m,passed,"Chiedi un valore di troppo","Input di prova: "+show(m.inputs[c])+"\nIl diagramma deve leggere solo i valori richiesti dalla missione.",id);
                    r.submitInput(m.inputs[c][input++]);
                }else r.step();
                if(r.steps>before){Node n=d.find(id);if(n!=null)used.add(n.kind);if(!visited.add(id))repeated=true;}
            }
            String context="Input di prova: "+show(m.inputs[c])+"\n";
            if(r.state==FlowEngine.State.ERROR)return fail(m,passed,"Un passaggio non funziona",context+r.errorMessage,r.currentNodeId);
            if(r.state!=FlowEngine.State.COMPLETED)return fail(m,passed,"Il ciclo non si conclude",context+"Dopo 2000 passaggi non hai raggiunto Fine. Controlla condizione e aggiornamento.",r.currentNodeId);
            if(input!=m.inputs[c].length)return fail(m,passed,"Manca una lettura",context+"Leggi tutti i valori richiesti, nello stesso ordine.",null);
            boolean same=r.output.size()==m.expected[c].length;
            if(same)for(int j=0;j<r.output.size();j++)if(!r.output.get(j).trim().equalsIgnoreCase(m.expected[c][j])){same=false;break;}
            if(!same)return fail(m,passed,"Guarda questo caso",context+"Atteso: "+show(m.expected[c])+"\nOttenuto: "+show(r.output.toArray(new String[0]))+"\nCorreggi il flowchart e riprova.",null);
            passed++;
        }
        if(!used.contains(OUTPUT))return fail(m,passed,"Usa un blocco Output","Il risultato deve essere mostrato dal diagramma.",null);
        if((m.id.equals("u01")||m.id.equals("u02")||m.cycle)&&!used.contains(PROCESS))return fail(m,passed,"Separa il calcolo dall'output","Usa un blocco Operazione per aggiornare le variabili.",null);
        if(m.decision&&!used.contains(DECISION))return fail(m,passed,"Costruisci la scelta","La missione richiede una Decisione con due percorsi.",null);
        if(m.cycle&&!repeated)return fail(m,passed,"Costruisci il ciclo","Collega il ritorno alla Decisione: questa missione allena la ripetizione.",null);
        return new Result(true,passed,m.cases(),"Flowchart riuscito!",passed+" casi di prova superati. Hai costruito un flusso che rispetta l'obiettivo su questi casi. Puoi continuare a sperimentare.",null);
    }
}
