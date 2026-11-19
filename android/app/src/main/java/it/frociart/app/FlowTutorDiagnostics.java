package it.frociart.app;

import java.util.*;
import java.util.regex.*;
import static it.frociart.app.FlowModel.*;

/** Explains concrete errors; valid alternative expressions are left to the real interpreter. */
public final class FlowTutorDiagnostics {
    private FlowTutorDiagnostics() {}
    private static FlowTutor.Feedback no(String code,String title,String why,String fix,Node n){return FlowTutor.no(code,title,why,fix,n==null?null:n.id);}
    public static String withoutStrings(String text){
        StringBuilder b=new StringBuilder();char quote=0;boolean escape=false;
        for(int i=0;i<text.length();i++){char c=text.charAt(i);if(quote!=0){b.append(' ');if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote=0;}else if(c=='\''||c=='"'){quote=c;b.append(' ');}else b.append(c);}return b.toString();
    }
    public static FlowTutor.Feedback before(Node n){
        if(n==null||n.kind==START||n.kind==END)return null;String s=n.text==null?"":n.text.trim(),code=withoutStrings(s);
        if(s.isEmpty())return no("empty","La casella è ancora vuota","Il blocco ha una forma, ma manca l'istruzione da eseguire.",n.kind==INPUT?"Scrivi il nome della variabile, per esempio eta.":n.kind==PROCESS?"Scrivi nome = valore, per esempio punti = 5.":n.kind==DECISION?"Scrivi una domanda, per esempio eta >= 18.":"Scrivi un valore o un testo fra virgolette, per esempio \"Ciao\".",n);
        if(code.matches("(?s).*[“”‘’].*"))return no("smart-quotes","Servono virgolette dritte","Le virgolette tipografiche non delimitano un testo nel motore.","Usa il tasto \" della tastiera: \"Ciao\". Il contenuto del testo non va cambiato.",n);
        String[] symbols={"≥",">=","≤","<=","≠","!=","×","*","÷","/","←","=","−","-"};
        for(int i=0;i<symbols.length;i+=2)if(code.contains(symbols[i]))return no("unicode-operator","Il simbolo è giusto in matematica, ma qui si scrive diversamente","Il motore accetta "+symbols[i+1]+" al posto di "+symbols[i]+".","Scrivi "+symbols[i+1]+" con i tasti normali. Il testo tra virgolette può invece contenere simboli.",n);
        if(n.kind==INPUT){
            if(s.contains("=")||s.contains("<-"))return no("input-assignment","Nell'Input va solo il nome","L'Input chiede il valore durante l'esecuzione: non devi assegnarlo qui.","Scrivi eta nel blocco; quando Prova si ferma, inserisci per esempio 21 nel campo del valore.",n);
            if(!FlowEngine.identifier(s))return no("input-identifier","Questo non è un nome di variabile valido","Il nome ha al massimo 64 caratteri: comincia con una lettera o _, continua con lettere, cifre o _. Niente spazi, trattini, virgolette o parole riservate come vero, falso, true, false, and, or, not, e, o, non.","Esempi: eta, numero_1, totale. La domanda leggibile va nel campo Domanda.",n);
            return null;
        }
        if(code.contains("**"))return no("power-symbol","La potenza usa ^","Due asterischi non sono un operatore di Frociart.","Per il quadrato scrivi numero ^ 2; per moltiplicare usa un solo *.",n);
        if(code.matches("\\s*[\\p{L}_][\\p{L}\\p{N}_]*\\s*(\\+\\+|--)\\s*"))return no("increment","L'aggiornamento va scritto per intero","Frociart non usa ++ o -- per aggiornare una variabile.","Nel blocco Operazione scrivi contatore = contatore + 1, oppure contatore = contatore - 1.",n);
        if(code.matches("(?s)\\s*[\\p{L}_][\\p{L}\\p{N}_]*\\s*[+*/%-]=.*"))return no("compound-assignment","Scrivi anche il valore precedente","Le abbreviazioni +=, -= e simili non sono assegnazioni supportate.","Scrivi totale = totale + numero invece di totale += numero.",n);
        if(code.matches("(?s).*\\b(print|stampa|input|if|while|for)\\s*\\(.*"))return no("language-command","Il comando è già nella forma del blocco","Non devi scrivere comandi di un altro linguaggio dentro una casella.","Nell'Output scrivi solo l'espressione, come totale. Per chiedere un dato usa Input; per scegliere usa Decisione.",n);
        if(n.kind==PROCESS){
            Matcher m=Pattern.compile("^([\\p{L}_][\\p{L}\\p{N}_]*)\\s*==").matcher(code);if(m.find())return no("compare-in-process","== confronta, non assegna","Un'Operazione deve conservare un nuovo valore in una variabile.","Scrivi "+m.group(1)+" = valore. Per fare una domanda usa una Decisione con ==.",n);
            if(!Pattern.compile("^([\\p{L}_][\\p{L}\\p{N}_]*)\\s*(=|<-)" ).matcher(code).find())return no("assignment-missing","Manca chi conserva il risultato","Un calcolo da solo non dice al blocco Operazione dove mettere il valore.","Scrivi totale = 2 + 3. Se vuoi soltanto mostrare 2 + 3, usa Output.",n);
        }
        int parentheses=0;for(char c:code.toCharArray()){if(c=='(')parentheses++;if(c==')'&&--parentheses<0)return no("parentheses-close","Una parentesi si chiude troppo presto","Ogni ) deve chiudere una ( precedente.","Rileggi le coppie da sinistra: (a + b) / 2.",n);}
        if(parentheses>0)return no("parentheses-open","Manca una parentesi chiusa","Hai aperto un gruppo senza chiuderlo.","Aggiungi la ) del gruppo, per esempio (a + b) / 2.",n);
        if(!code.contains("(")&&Pattern.compile("\\d,\\d").matcher(code).find())return no("decimal-comma","Nel codice i decimali usano il punto","La virgola nel motore separa gli argomenti delle funzioni. Nel campo di un valore Input sono ammessi anche i decimali con virgola.","Nel blocco scrivi 1.5. Quando Prova chiede un valore puoi inserire 1,5.",n);
        return null;
    }
    public static FlowTutor.Feedback issue(FlowEngine.Issue i){
        String title="Controlla questo blocco",why=i.message,fix=i.suggestion==null?"Rivedi la guida del blocco.":i.suggestion;
        if(i.code.equals("start-count")){title="Serve un solo punto di partenza";why="L'esecuzione deve sapere da quale Inizio partire.";fix="Tieni un blocco Inizio; collegalo al primo passaggio.";}
        else if(i.code.equals("end-missing")){title="Manca la fine del percorso";why="Il programma deve avere un punto in cui fermarsi.";fix="Aggiungi Fine e collega a Fine tutti i percorsi che devono terminare.";}
        else if(i.code.equals("start-incoming")){title="Il ciclo non torna in Inizio";why="Inizio si attraversa una volta per avviare il programma.";fix="Fai tornare la freccia alla Decisione o al passaggio da ripetere.";}
        else if(i.code.equals("end-outgoing")){title="Da Fine non si riparte";why="Fine ferma l'esecuzione: una sua uscita non può essere seguita.";fix="Elimina la freccia che parte da Fine o scegli un altro tipo di blocco.";}
        else if(i.code.equals("decision-branches")){title="La domanda ha due risposte";why="Ogni Decisione richiede un'uscita per vero e una per falso, anche quando i due percorsi terminano nella stessa Fine.";fix="Collega il ramo Sì/vero e il ramo No/falso. Cambiare il nome del ramo non cambia la condizione.";}
        else if(i.code.equals("single-outgoing")){title="La vicinanza non basta: serve la freccia";why="Questo blocco ha bisogno di un unico passaggio successivo.";fix="Collega la sua uscita al blocco successivo. La Decisione è il blocco che può avere due uscite.";}
        else if(i.code.equals("no-end-path")){title="Questo percorso non può arrivare a Fine";why="C'è un ramo chiuso su se stesso o senza uscita verso Fine.";fix="Segui il ramo e aggiungi l'uscita dal ciclo. Un ciclo deve poter diventare falso.";}
        else if(i.code.contains("uninitialized")||i.code.contains("unassigned")||i.message.contains("non ha")||i.message.contains("prima di")){title="Prima dai un valore alla variabile";why=i.message+"\nUn nome non crea automaticamente un valore.";fix="Aggiungi un Input oppure un'Operazione prima di ogni percorso che legge quel nome. Controlla anche i nomi simili. Se volevi mostrare una parola fissa, scrivila fra virgolette: \"Ciao\".";}
        else if(i.code.equals("unreachable")){title="Questa casella non verrà attraversata";why="Le frecce da Inizio non arrivano a questo blocco.";fix="Collegalo nel punto giusto oppure rimuovilo se non serve.";}
        if(i.code.equals("syntax"))return runtime(i.message,i.nodeId);
        return FlowTutor.no(i.code,title,why,fix,i.nodeId);
    }
    public static FlowTutor.Feedback runtime(String message,String nodeId){
        String m=message==null?"Il blocco non è eseguibile.":message,low=m.toLowerCase(Locale.ITALIAN),code="engine",title="Rileggiamo questa istruzione",fix="Apri la guida del blocco e controlla i suoi dati.";
        if(low.contains("divisione per zero")){code="divide-zero";title="Non si può dividere per zero";fix="Prima della divisione usa Decisione: divisore != 0. Calcola soltanto nel ramo vero.";}
        else if(low.contains("resto con divisore zero")){code="mod-zero";title="Anche il resto richiede un divisore diverso da zero";fix="Controlla il divisore prima di usare %. % calcola il resto, non una percentuale.";}
        else if(low.contains("non ha ancora un valore")||low.contains("potrebbe non")){code="undefined";title="Questa variabile non ha ancora un valore";fix="Usa Input oppure nome = valore in un'Operazione prima di leggerla. Se il nome è un testo da mostrare, racchiudilo fra virgolette.";}
        else if(low.contains("chiudi le virgolette")||low.contains("escape")){code="quotes";title="Il testo non è chiuso correttamente";fix="Per una parola usa \"Ciao\". Ogni virgoletta aperta deve avere la sua chiusura; per una virgoletta nel testo scrivi \\\".";}
        else if(low.contains("serve un numero")&&(low.contains("«vero»")||low.contains("«falso»"))){code="boolean-number";title="Una risposta vero/falso non è un numero";fix="Ogni confronto produce vero o falso. Non scrivere 0 <= eta <= 65 come in matematica: separa le due domande con and, per esempio eta >= 0 and eta <= 65.";}
        else if(low.contains("serve un numero")||low.contains("richiede un testo")){code="type";title="Numero e testo non sono la stessa cosa";fix="5 è un numero, \"5\" è testo. Nell'Input di una prova numerica scrivi 5, senza virgolette o nome della variabile.";}
        else if(low.contains("condizione deve restituire")){code="condition-type";title="Una Decisione deve poter scegliere vero o falso";fix="Un testo come \"numero > 0\" resta una frase. Per fare la domanda togli le virgolette: numero > 0. Il motore accetta anche numeri: 0 vale falso, gli altri valori valgono vero.";}
        else if(low.contains("radice quadrata")){code="sqrt-domain";title="La radice richiede un numero non negativo";fix="Controlla numero >= 0 prima di usare sqrt(numero).";}
        else if(low.contains("funzione")&&low.contains("sconosciuta")){code="function-name";title="Questo nome non è una funzione disponibile";fix="Usa la guida: abs, sqrt, min, max, floor, round e len sono le funzioni disponibili. Non scrivere print o funzioni inventate.";}
        else if(low.contains("argomenti")){code="function-args";title="La funzione riceve un numero preciso di valori";fix="max richiede almeno due valori: max(a,b). sqrt richiede un solo valore: sqrt(numero). La virgola separa i valori.";}
        else if(low.contains("cifre decimali")){code="round-digits";title="Quante cifre vuoi conservare?";fix="Il secondo valore di round deve essere un intero da 0 a 15. Esempio: round(3.14159, 2).";}
        else if(low.contains("finito")||low.contains("troppo grande")){code="finite";title="Il numero supera i limiti del motore";fix="Usa valori finiti più piccoli. NaN e infinito non sono dati numerici validi da inserire.";}
        else if(low.contains("limite")&&low.contains("blocchi")){code="loop-limit";title="Il ciclo potrebbe non terminare";fix="Aggiorna la variabile del controllo e verifica che prima o poi la Decisione segua l'uscita verso Fine.";}
        else if(low.contains("elemento inatteso")||low.contains("espressione incompleta")){code="incomplete-expression";title="La frase del blocco non è completa";fix="Ogni operatore richiede i suoi valori: 2 + 3, non 2 +. Usa la forma prevista per questo blocco.";}
        return FlowTutor.no(code,title,m,fix,nodeId);
    }
    /** Interpret common misconceptions only after an actual case has failed. Never reject equivalent valid code. */
    public static String misconception(FlowTutor.Lesson lesson,Node n){
        String code=n.text.replaceAll("\\s+","").toLowerCase(Locale.ROOT),id=lesson.id;
        if(id.equals("b07")&&(code.equals("\"nome\"")||code.equals("'nome'")))return "Le virgolette chiedono di mostrare la parola nome. Qui devi leggere il valore conservato nella variabile, per esempio Ada.";
        if(id.equals("b08")&&code.equals("punti=1"))return "Questa assegnazione sostituisce punti con 1 ogni volta. Per aggiungere un punto devi leggere anche il valore precedente.";
        if(id.equals("b09")&&code.equals("resto=prezzo-pagato"))return "Hai invertito l'ordine della sottrazione. Il resto da restituire parte dal denaro pagato e sottrae il prezzo.";
        if(id.equals("b10")&&code.equals("totale=prezzo+quantita"))return "Stai aggiungendo la quantità al prezzo di un solo pezzo. Il totale richiede il prezzo ripetuto per tutti i pezzi: una moltiplicazione.";
        if(id.equals("b12")&&code.equals("media=a+b/2"))return "La divisione viene prima della somma: qui dividi soltanto b. La media richiede di dividere l'intera somma, quindi serve un gruppo fra parentesi.";
        if((id.equals("b13")||id.equals("b24"))&&code.equals("eta>18"))return "Con > il valore 18 è escluso. La missione include chi ha esattamente 18 anni: il confine deve essere compreso.";
        if(id.equals("b15")&&code.equals("!0"))return "!0 nega il numero 0 e produce sempre vero. Non legge numero e non può distinguere il caso numero = 0 dagli altri casi. != è invece un confronto tra due valori.";
        if(id.equals("b16")&&code.equals("eta>=18oreta<=65"))return "or richiede una sola parte vera: 100 supera 18 e passa, anche se supera 65. Un intervallo richiede entrambi i limiti contemporaneamente.";
        if(id.equals("b17")&&code.equals("giorno==6or7"))return "7 da solo è un numero diverso da zero, quindi per il motore vale vero. Devi confrontare giorno anche nella seconda domanda.";
        if(id.equals("b20")&&code.equals("i<=n"))return "Con i che parte da 0, <= lascia eseguire anche il giro in cui i vale n. Così fai un giro in più. Prova n = 0 per vedere subito il problema.";
        if(id.equals("b21")&&code.equals("somma=i"))return "Qui somma dimentica i giri precedenti e riceve soltanto i. Per accumulare devi includere la somma già conservata.";
        if(id.equals("b22")&&(code.equals("numero/2==0")||code.equals("numero/2=0")))return "/ calcola il quoziente: per esempio 8 / 2 vale 4. La parità dipende invece dal resto della divisione per 2, che si ottiene con %.";
        return "";
    }
}
