package it.frociart.app;

import java.util.ArrayList;
import java.util.List;
import static it.frociart.app.FlowModel.*;

/** Editable examples use the same stable IDs, language and layout as Frociart desktop. */
public final class FlowExamples {
    private FlowExamples() {}
    public static List<Document> all(){ArrayList<Document> a=new ArrayList<>();a.add(sum());a.add(parity());a.add(countdown());a.add(maximum());a.add(missingBranch());a.add(report());a.add(age());a.add(copyFile());a.add(turtleSquare());return a;}
    public static boolean isExercise(Document d){return d!=null&&"Esercizio: trova l'errore".equals(d.title);}
    public static String description(Document d){switch(d.title){
        case "Disegna un quadrato":return "Quattro lati, un ciclo e la grafica tartaruga";
        case "Copia un file di testo":return "Apri, leggi e scrivi tutte le righe usando EOF()";
        case "Somma di due numeri":return "Leggi due valori e calcola il totale";
        case "Pari o dispari":return "Prova una decisione con due uscite";
        case "Conto alla rovescia":return "Segui un ciclo, un passo alla volta";
        case "Maggiore tra due numeri":return "Confronta due numeri";
        case "Maggiorenne o minorenne":return "Prova entrambi i rami: 17, 18 e 19 anni";
        case "Esercizio: trova l'errore":return "SFIDA DA CORREGGERE: manca il ramo No. Completa il collegamento prima di eseguire; Verifica ti mostra dove intervenire.";
        case "Report di misure":return "Crea un report con conteggio, media e massimo";
        default:return "Apri un nuovo progetto da questo esempio";
    }}
    private static Document document(String title){Document d=new Document();d.title=title;return d;}
    private static void node(Document d,String id,int kind,String text,float x,float y,String prompt){Node n=new Node(kind,text,x+116-width(kind)/2,y);n.id=id;n.prompt=prompt;d.nodes.add(n);}
    private static void node(Document d,String id,int kind,String text,float x,float y){node(d,id,kind,text,x,y,"");}
    private static void edge(Document d,String from,String to,String branch){Edge e=new Edge(from,to,branch);e.id=from+"_"+branch+"_"+to;d.edges.add(e);}
    private static void edge(Document d,String from,String to){edge(d,from,to,"next");}
    public static Document blank(){Document d=document("Nuovo diagramma");node(d,"start",START,"Inizio",350,80);node(d,"end",END,"Fine",350,270);edge(d,"start","end");return d;}
    public static Document turtleSquare(){Document d=FlowPseudocode.decode(FlowTurtle.EXAMPLE);d.title="Disegna un quadrato";return d;}
    public static Document copyFile(){Document d=document("Copia un file di testo");
        node(d,"start",START,"Inizio",350,40);
        node(d,"line",DECLARE,"riga: TESTO",350,180);
        node(d,"count",DECLARE,"righe: INTERO",350,320);
        node(d,"open_read",OPEN_FILE,"LETTURA \"dati.txt\"",350,460);
        node(d,"open_write",OPEN_FILE,"SCRITTURA \"copia.txt\"",350,600);
        node(d,"check",DECISION,"non EOF()",350,760);
        node(d,"read",READ_FILE,"riga",80,950);
        node(d,"write",WRITE_FILE,"riga",80,1090);
        node(d,"increment",PROCESS,"righe = righe + 1",80,1230);
        node(d,"close_read",CLOSE_FILE,"LETTURA",620,950);
        node(d,"close_write",CLOSE_FILE,"SCRITTURA",620,1090);
        node(d,"result",OUTPUT,"\"Righe copiate: \" + righe",620,1230);
        node(d,"end",END,"Fine",620,1370);
        edge(d,"start","line");
        edge(d,"line","count");
        edge(d,"count","open_read");
        edge(d,"open_read","open_write");
        edge(d,"open_write","check");
        edge(d,"check","read","true");
        edge(d,"read","write");
        edge(d,"write","increment");
        edge(d,"increment","check");
        edge(d,"check","close_read","false");
        edge(d,"close_read","close_write");
        edge(d,"close_write","result");
        edge(d,"result","end");
        return d;
    }

    public static Document sum(){Document d=document("Somma di due numeri");node(d,"start",START,"Inizio",350,45);node(d,"input_a",INPUT,"a",350,175,"Inserisci il primo numero");node(d,"input_b",INPUT,"b",350,305,"Inserisci il secondo numero");node(d,"sum",PROCESS,"somma = a + b",350,435);node(d,"output",OUTPUT,"\"Somma: \" + somma",350,565);node(d,"end",END,"Fine",350,695);edge(d,"start","input_a");edge(d,"input_a","input_b");edge(d,"input_b","sum");edge(d,"sum","output");edge(d,"output","end");return d;}
    public static Document parity(){Document d=document("Pari o dispari");node(d,"start",START,"Inizio",350,45);node(d,"input",INPUT,"numero",350,175,"Inserisci un numero intero");node(d,"check",DECISION,"numero % 2 = 0",350,325);node(d,"even",OUTPUT,"\"Pari\"",90,495);node(d,"odd",OUTPUT,"\"Dispari\"",610,495);node(d,"end",END,"Fine",350,665);edge(d,"start","input");edge(d,"input","check");edge(d,"check","even","true");edge(d,"check","odd","false");edge(d,"even","end");edge(d,"odd","end");return d;}
    public static Document countdown(){Document d=document("Conto alla rovescia");node(d,"start",START,"Inizio",350,45);node(d,"input",INPUT,"n",350,175,"Da quale numero intero vuoi partire?");node(d,"check",DECISION,"n > 0",350,325);node(d,"count",OUTPUT,"n",80,485);node(d,"decrement",PROCESS,"n = n - 1",80,625);node(d,"go",OUTPUT,"\"Via!\"",610,485);node(d,"end",END,"Fine",610,625);edge(d,"start","input");edge(d,"input","check");edge(d,"check","count","true");edge(d,"check","go","false");edge(d,"count","decrement");edge(d,"decrement","check");edge(d,"go","end");return d;}
    public static Document maximum(){Document d=document("Maggiore tra due numeri");node(d,"start",START,"Inizio",350,45);node(d,"input_a",INPUT,"a",350,175,"Inserisci il primo numero");node(d,"input_b",INPUT,"b",350,305,"Inserisci il secondo numero");node(d,"check",DECISION,"a >= b",350,455);node(d,"a_greater",OUTPUT,"\"Maggiore: \" + a",90,625);node(d,"b_greater",OUTPUT,"\"Maggiore: \" + b",610,625);node(d,"end",END,"Fine",350,795);edge(d,"start","input_a");edge(d,"input_a","input_b");edge(d,"input_b","check");edge(d,"check","a_greater","true");edge(d,"check","b_greater","false");edge(d,"a_greater","end");edge(d,"b_greater","end");return d;}
    public static Document age(){Document d=parity();d.title="Maggiorenne o minorenne";d.find("input").text="eta";d.find("input").prompt="Quanti anni hai? Inserisci un numero.";d.find("check").text="eta >= 18";d.find("even").text="\"Maggiorenne\"";d.find("odd").text="\"Minorenne\"";return d;}
    /** Deliberately broken teaching exercise, kept separate from the runnable age example. */
    public static Document missingBranch(){Document d=age();d.title="Esercizio: trova l'errore";for(int i=d.edges.size()-1;i>=0;i--)if("false".equals(d.edges.get(i).branch))d.edges.remove(i);return d;}
    /** Learning capstone: stream numeric measures, reject outliers, then report or handle empty input. */
    public static Document report(){
        Document d=document("Report di misure");
        node(d,"start",START,"Inizio",350,40);
        node(d,"init",PROCESS,"somma = 0; conteggio = 0; massimo = 0",350,170);
        node(d,"read",INPUT,"valore",350,320,"Inserisci un numero tra -100 e 100. 9999 per terminare.");
        node(d,"sentinel",DECISION,"valore != 9999",350,495);
        node(d,"domain",DECISION,"valore >= -100 and valore <= 100",150,720);
        node(d,"rejected",OUTPUT,"\"Valore rifiutato\"",-180,925);
        node(d,"first",DECISION,"conteggio == 0",150,925);
        node(d,"setmax",PROCESS,"massimo = valore",-80,1150);
        node(d,"greater",DECISION,"valore > massimo",380,1150);
        node(d,"accumulate",PROCESS,"somma = somma + valore; conteggio = conteggio + 1",150,1380);
        node(d,"hasdata",DECISION,"conteggio > 0",920,720);
        node(d,"mean",PROCESS,"media = somma / conteggio",780,925);
        node(d,"outmean",OUTPUT,"\"Media: \" + media",780,1075);
        node(d,"outmax",OUTPUT,"\"Massimo: \" + massimo",780,1225);
        node(d,"empty",OUTPUT,"\"Nessun dato\"",1120,925);
        node(d,"end",END,"Fine",920,1420);
        edge(d,"start","init");edge(d,"init","read");edge(d,"read","sentinel");
        edge(d,"sentinel","domain","true");edge(d,"sentinel","hasdata","false");
        edge(d,"domain","first","true");edge(d,"domain","rejected","false");edge(d,"rejected","read");
        edge(d,"first","setmax","true");edge(d,"first","greater","false");
        edge(d,"greater","setmax","true");edge(d,"greater","accumulate","false");
        edge(d,"setmax","accumulate");edge(d,"accumulate","read");
        edge(d,"hasdata","mean","true");edge(d,"hasdata","empty","false");
        edge(d,"mean","outmean");edge(d,"outmean","outmax");edge(d,"outmax","end");edge(d,"empty","end");
        d.find("sentinel").trueLabel="Continua";d.find("sentinel").falseLabel="Termina";
        d.find("domain").trueLabel="Valido";d.find("domain").falseLabel="Fuori intervallo";
        d.find("first").trueLabel="Primo dato";d.find("first").falseLabel="Già presenti";
        d.find("greater").trueLabel="Nuovo massimo";d.find("greater").falseLabel="Conserva";
        d.find("hasdata").trueLabel="Report";d.find("hasdata").falseLabel="Nessun dato";
        return d;
    }
}
