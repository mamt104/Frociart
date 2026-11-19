package it.frociart.app;

import java.util.ArrayList;

/**
 * Offline learning content. Rules are paraphrased from the primary sources listed
 * below; problems, worked values and option-specific feedback are original.
 * Stable IDs belong to the learning history: do not renumber them when editing.
 * A pseudocode card describes control flow, not text to paste into a Process node.
 * See FONTI_IMPARA.txt for provenance, scope and the host-side verification plan.
 */
public final class FlowLearning {
    private FlowLearning() {}

    public static final int CONTENT_VERSION = 1;
    public static final String INTRO = "Parti da un piccolo esempio, prevedi cosa succede e prova. "
            + "Ogni risposta spiega il ragionamento: puoi riprovare senza perdere progressi. "
            + "Le ultime unità introducono metodi avanzati; per padroneggiarli servono anche progetti e pratica.";
    public static final String PSEUDOCODE_NOTE = "Il pseudocodice descrive i passi dell'algoritmo. "
            + "In Frociart traduci letture, operazioni, decisioni e ritorni in blocchi e frecce. "
            + "Liste e funzioni personalizzate sono concetti di studio: il motore non li esegue.";

    public static final class Unit {
        public final String id, title, subtitle;
        private Unit(String id, String title, String subtitle) {
            this.id = id; this.title = title; this.subtitle = subtitle;
        }
    }

    public static final class Source {
        public final String id, title, url;
        private Source(String id, String title, String url) {
            this.id = id; this.title = title; this.url = url;
        }
    }

    public static final class Lesson {
        public final String id, unitId, title, goal, explanation, tip, counterexample;
        public final Source[] sources;
        public final Question[] questions;
        /** Exact title in FlowExamples.all(), or empty when no matching example exists. */
        public final String practiceExample;
        private Lesson(String id, String unitId, String title, String goal, String explanation,
                       String tip, String counterexample, Source[] sources, String practiceExample,
                       Question... questions) {
            this.id = id; this.unitId = unitId; this.title = title; this.goal = goal;
            this.explanation = explanation; this.tip = tip; this.counterexample = counterexample;
            this.sources = sources.clone(); this.questions = questions.clone();
            this.practiceExample = practiceExample;
        }
    }

    public static final class Question {
        public final String id, prompt, code;
        public final String[] options, feedback;
        /** Zero-based index; every other option is a deliberate, explained distractor. */
        public final int correct;
        private Question(String id, String prompt, String code, int correct,
                         String[] options, String[] feedback) {
            this.id = id; this.prompt = prompt; this.code = code; this.correct = correct;
            this.options = options.clone(); this.feedback = feedback.clone();
        }
    }

    private static final Source CS50_ZERO = new Source("s01", "Harvard CS50 · Algoritmi e pseudocodice",
            "https://cs50.harvard.edu/x/notes/0/");
    private static final Source FLOW_SHAPES = new Source("s02", "Flowgorithm · Blocchi del flowchart",
            "https://flowgorithm.org/documentation/index.html");
    private static final Source FLOW_INPUT = new Source("s03", "Flowgorithm · Input",
            "https://www.flowgorithm.org/documentation/input.html");
    private static final Source FLOW_OUTPUT = new Source("s04", "Flowgorithm · Output",
            "https://www.flowgorithm.org/documentation/output.html");
    private static final Source FLOW_ASSIGN = new Source("s05", "Flowgorithm · Assegnazione",
            "https://flowgorithm.org/documentation/assignment.html");
    private static final Source FLOW_WATCH = new Source("s06", "Flowgorithm · Breakpoint e stato delle variabili",
            "https://flowgorithm.org/documentation/breakpoint.html");
    private static final Source FLOW_TYPES = new Source("s07", "Flowgorithm · Tipi di dato",
            "https://www.flowgorithm.org/documentation/language/types.html");
    private static final Source FLOW_OPERATORS = new Source("s08", "Flowgorithm · Operatori ed espressioni",
            "https://www.flowgorithm.org/documentation/language/operators.html");
    private static final Source FLOW_IF = new Source("s09", "Flowgorithm · Decisione If",
            "https://www.flowgorithm.org/documentation/if.html");
    private static final Source FLOW_WHILE = new Source("s10", "Flowgorithm · Ciclo While",
            "https://www.flowgorithm.org/documentation/while.html");
    private static final Source FLOW_FOR = new Source("s11", "Flowgorithm · Conteggio con For",
            "https://flowgorithm.org/documentation/for.html");
    private static final Source FLOW_DO = new Source("s12", "Flowgorithm · Ciclo Do",
            "https://www.flowgorithm.org/documentation/language/do.html");
    private static final Source CS50_ONE = new Source("s13", "Harvard CS50 · Variabili, condizioni e numeri",
            "https://cs50.harvard.edu/x/notes/1/");
    private static final Source CS50_DEBUG = new Source("s14", "Harvard CS50 · Debugging passo per passo",
            "https://cs50.harvard.edu/x/notes/2/");
    private static final Source MIT_TEST = new Source("s15", "MIT 6.102 · Test, partizioni e confini",
            "https://web.mit.edu/6.102/www/sp26/classes/02-testing/");
    private static final Source MIT_DEBUG = new Source("s16", "MIT 6.031 · Debugging sistematico",
            "https://web.mit.edu/6.031/www/fa21/classes/13-debugging/");
    private static final Source CORNELL_PROOF = new Source("s17", "Cornell CS 2112 · Invarianti e terminazione",
            "https://www.cs.cornell.edu/courses/cs2112/2015fa/lectures/lec_loopinv/");
    private static final Source CORNELL_DESIGN = new Source("s18", "Cornell CS 2110 · Progettare con gli invarianti",
            "https://www.cs.cornell.edu/courses/cs2110/2026fa/lectures/lec04/");
    private static final Source MIT_SPEC = new Source("s19", "MIT 6.031 · Precondizioni e postcondizioni",
            "https://web.mit.edu/6.031/www/fa21/classes/06-specifications/");
    private static final Source MIT_DECOMPOSE = new Source("s20", "MIT 6.0001 · Decomposizione e astrazione (PDF)",
            "https://ocw.mit.edu/courses/6-0001-introduction-to-computer-science-and-programming-in-python-fall-2016/6ba59859535f1566dd57a7279aeba5d1_MIT6_0001F16_Lec4.pdf");
    private static final Source CS50_ALGORITHMS = new Source("s21", "Harvard CS50 · Ricerca e complessità",
            "https://cs50.harvard.edu/x/notes/3/");
    private static final Source CORNELL_SENTINEL = new Source("s22", "Cornell CS 99 · Letture e sentinelle",
            "https://www.cs.cornell.edu/courses/cs99/2001su/lectures/Lecture5.htm");
    private static final Source CORNELL_COMPLEXITY = new Source("s23", "Cornell CS 2110 · Analizzare la complessità",
            "https://www.cs.cornell.edu/courses/cs2110/2026fa/lectures/lec05/");

    private static final Source[] SOURCES = { CS50_ZERO, FLOW_SHAPES, FLOW_INPUT, FLOW_OUTPUT,
            FLOW_ASSIGN, FLOW_WATCH, FLOW_TYPES, FLOW_OPERATORS, FLOW_IF, FLOW_WHILE, FLOW_FOR,
            FLOW_DO, CS50_ONE, CS50_DEBUG, MIT_TEST, MIT_DEBUG, CORNELL_PROOF, CORNELL_DESIGN,
            MIT_SPEC, MIT_DECOMPOSE, CS50_ALGORITHMS, CORNELL_SENTINEL, CORNELL_COMPLEXITY };
    private static final Unit[] UNITS = {
            new Unit("u01", "Leggere un flusso", "Da zero · Input, blocchi, frecce e primi passi"),
            new Unit("u02", "Dati che cambiano", "Base · Variabili, calcoli e tipi di dato"),
            new Unit("u03", "Scegliere una strada", "Base · Condizioni chiare e rami completi"),
            new Unit("u04", "Ripetere con criterio", "Intermedio · Conteggi, totali e input ripetuti"),
            new Unit("u05", "Capire perché funziona", "Intermedio → avanzato · Test, invarianti e terminazione"),
            new Unit("u06", "Progettare e migliorare", "Avanzato · Contratti, ricerca, costo e progetto finale")
    };
    private static final Lesson[] LESSONS = buildLessons();

    public static Unit[] units() { return UNITS.clone(); }
    public static Lesson[] lessons() { return LESSONS.clone(); }
    public static Source[] sources() { return SOURCES.clone(); }
    public static Lesson lesson(String id) {
        if (id != null) for (Lesson item : LESSONS) if (item.id.equals(id)) return item;
        return null;
    }
    public static Source source(String id) {
        if (id != null) for (Source item : SOURCES) if (item.id.equals(id)) return item;
        return null;
    }
    public static Lesson[] lessonsFor(String unitId) {
        ArrayList<Lesson> found = new ArrayList<>();
        for (Lesson item : LESSONS) if (item.unitId.equals(unitId)) found.add(item);
        return found.toArray(new Lesson[found.size()]);
    }
    private static String[] a(String... values) { return values; }
    private static Source[] refs(Source... values) { return values; }
    private static Question q(String id, String prompt, String code, int correct,
                              String[] options, String[] feedback) {
        return new Question(id, prompt, code, correct, options, feedback);
    }
    private static Lesson l(String id, String unitId, String title, String goal, String explanation,
                            String tip, String counterexample, Source[] sources, String practice,
                            Question... questions) {
        return new Lesson(id, unitId, title, goal, explanation, tip, counterexample, sources, practice, questions);
    }
    private static Lesson[] buildLessons() {
        ArrayList<Lesson> all = new ArrayList<>();
        for (Lesson[] group : new Lesson[][] { unit1(), unit2(), unit3(), unit4(), unit5(), unit6() })
            for (Lesson lesson : group) all.add(lesson);
        return all.toArray(new Lesson[all.size()]);
    }

    private static Lesson[] unit1() {
        return new Lesson[] {
            l("l01", "u01", "Dal problema ai passi", "Riconosci cosa entra, cosa deve uscire e quali passi servono.",
                "Un algoritmo descrive passi precisi per risolvere un problema. L'input è ciò che ricevi; l'output è il risultato richiesto. "
                + "Prima del disegno, prova a raccontare la soluzione con un caso piccolo. Il pseudocodice è questa descrizione ordinata: "
                + "non richiede la sintassi di un linguaggio.",
                "Scrivi prima un esempio di input e il suo output atteso. Poi descrivi i passi necessari per collegarli.",
                "«Gestisci le confezioni» è ambiguo. «Leggi quante confezioni e quanti pezzi per confezione; moltiplica; mostra il totale» è verificabile.",
                refs(CS50_ZERO, FLOW_INPUT), "Somma di due numeri",
                q("l01-q1", "Vuoi calcolare i pezzi contenuti in confezioni tutte uguali. Quali sono gli input?", "", 1,
                    a("Il totale già calcolato", "Numero di confezioni e pezzi per confezione", "Il colore del blocco Output"),
                    a("Il totale è il risultato da ottenere: è l'output.", "Sono i due dati necessari: moltiplicandoli ottieni il totale.", "Il colore aiuta a leggere il diagramma, ma non determina quanti pezzi ci sono.")),
                q("l01-q2", "Quale risultato deve mostrare questo algoritmo?",
                    "Pseudocodice\nLeggi confezioni: 3\nLeggi pezzi per confezione: 4\nMoltiplica i due valori\nMostra il totale", 0,
                    a("12", "7", "34"),
                    a("Tre confezioni da quattro pezzi contengono 3 × 4 = 12 pezzi.", "7 è la somma. Qui serve moltiplicare le quantità.", "34 unisce le cifre come testo; non conta i pezzi.")),
                q("l01-q3", "Quale istruzione specifica davvero come decidere se fermarsi?", "", 2,
                    a("Controlla che vada tutto bene", "Fermati quando sembra abbastanza", "Se i pezzi prodotti sono almeno 20, termina"),
                    a("Manca un criterio osservabile per capire cosa significhi «bene».", "«Abbastanza» non indica un valore o una condizione da verificare.", "La soglia è esplicita: con 19 si continua, con 20 o più si termina.")))
            ,l("l02", "u01", "Un blocco, un compito", "Scegli il blocco in base all'azione che deve compiere.",
                "Inizio e Fine delimitano il flusso. Un solo parallelogramma rappresenta Input e Output: scrivi LEGGI per ricevere un dato oppure STAMPA per mostrare un risultato. La forma è la stessa: LEGGI è azzurro, STAMPA è viola. Operazione salva un calcolo. Decisione valuta una condizione e sceglie un’uscita. Le frecce stabiliscono l’ordine, non la posizione sul foglio.",
                "Parti dal verbo: leggere → Input, calcolare e salvare → Operazione, confrontare → Decisione, mostrare → Output.",
                "Scrivere «n > 10» in un Output mostra il risultato del confronto; non crea i due percorsi di una Decisione.",
                refs(FLOW_SHAPES, FLOW_OUTPUT, FLOW_IF), "Pari o dispari",
                q("l02-q1", "Devi scegliere due percorsi in base a n > 10. Quale blocco serve?", "", 2,
                    a("Input", "Operazione", "Decisione"),
                    a("Input riceve n, ma non sceglie quale percorso seguire.", "Operazione può salvare un valore; il bivio richiede una Decisione.", "La Decisione valuta n > 10 e segue l'uscita corrispondente a vero o falso.")),
                q("l02-q2", "Hai già calcolato totale. Quale blocco lo rende visibile durante la prova?", "", 0,
                    a("Output con l'espressione totale", "Input con il nome totale", "Fine con il testo totale"),
                    a("Output valuta totale e lo aggiunge ai risultati della prova.", "Input richiederebbe un nuovo dato, sostituendo il valore di totale.", "Fine chiude il flusso: il suo titolo non è un'istruzione di stampa.")),
                q("l02-q3", "Le frecce collegano Inizio → A → B → Fine, ma B è disegnato più in alto di A. Quale viene eseguito prima?", "", 1,
                    a("B, perché è più in alto", "A, perché è il successivo di Inizio", "Quello con il colore più acceso"),
                    a("L'altezza aiuta la lettura, ma l'esecuzione segue i collegamenti.", "La freccia da Inizio porta ad A; la posizione di B non cambia questo ordine.", "Il colore non decide l'ordine dei blocchi.")))
            ,l("l03", "u01", "L'ordine cambia il risultato", "Esegui una sequenza senza saltare o riordinare i passi.",
                "Una variabile dà un nome a un valore. In un'Operazione, x = 5 significa «salva 5 in x». "
                + "Ogni istruzione usa i valori disponibili in quel momento. Un Output mostra il valore che trova "
                + "quando viene eseguito, anche se un blocco successivo lo cambierà.",
                "Leggi una sola istruzione alla volta e aggiorna il valore prima di passare alla successiva.",
                "Con x iniziale 2, aggiungere 3 e poi raddoppiare dà 10. Raddoppiare prima e aggiungere 3 dopo dà 7.",
                refs(FLOW_ASSIGN, FLOW_OUTPUT), "Somma di due numeri",
                q("l03-q1", "Che cosa viene mostrato?",
                    "Blocchi Frociart, in ordine\nOperazione: x = 2\nOperazione: x = x + 3\nOperazione: x = x * 2\nOutput: x", 1,
                    a("7", "10", "5"),
                    a("7 si otterrebbe raddoppiando prima di aggiungere 3. Qui l'ordine è diverso.", "x passa da 2 a 5 e poi a 10: ogni blocco usa il valore appena aggiornato.", "5 è il valore intermedio, prima della moltiplicazione.")),
                q("l03-q2", "Quale valore appare nell'Output?",
                    "Blocchi Frociart, in ordine\nOperazione: x = 6\nOutput: x\nOperazione: x = 9", 0,
                    a("6", "9", "Entrambi, 6 e 9"),
                    a("Quando l'Output viene eseguito, x vale ancora 6. La modifica successiva non riscrive quel risultato.", "9 è il valore successivo di x, ma manca un altro Output che lo mostri.", "C'è un solo Output. Cambiare una variabile non la stampa automaticamente.")),
                q("l03-q3", "Devi leggere base e altezza e poi calcolare area = base * altezza. Quale ordine rende disponibili tutti i dati?", "", 2,
                    a("Calcola area; leggi base; leggi altezza", "Leggi base; calcola area; leggi altezza", "Leggi base; leggi altezza; calcola area"),
                    a("Il calcolo tenterebbe di usare due valori non ancora letti.", "Nel calcolo mancherebbe ancora altezza.", "Entrambi gli input sono disponibili prima dell'Operazione che li usa.")))
            ,l("l04", "u01", "Segui lo stato, passo per passo", "Prevedi una prova osservando variabili, blocco corrente e output.",
                "Lo stato comprende i valori delle variabili e il punto raggiunto nel flusso. Per fare una traccia, "
                + "annota i valori dopo ogni blocco. Confronta poi la tua previsione con una prova a passi. "
                + "Il primo punto in cui previsione e realtà divergono è un indizio utile.",
                "Prima di avanzare, prevedi il prossimo valore. Dopo il passo confrontalo: una differenza piccola è più facile da spiegare.",
                "Un risultato finale sbagliato non implica che sia sbagliato l'Output: potrebbe aver ricevuto un valore già errato.",
                refs(FLOW_WATCH, CS50_DEBUG), "Somma di due numeri",
                q("l04-q1", "Qual è lo stato dopo il secondo blocco Operazione?",
                    "Operazione 1: a = 7; b = 2\nOperazione 2: a = a - b\nOperazione 3: b = a + 1", 2,
                    a("a = 5, b = 6", "a = 7, b = 2", "a = 5, b = 2"),
                    a("b diventa 6 soltanto nel terzo blocco, che non è stato ancora eseguito.", "Questo è lo stato dopo il primo blocco. Il secondo riduce a di 2.", "Il secondo blocco cambia soltanto a: 7 − 2 = 5; b resta 2.")),
                q("l04-q2", "Quali valori vengono mostrati, nell'ordine?",
                    "Blocchi Frociart, in ordine\nOperazione: n = 4\nOutput: n\nOperazione: n = n + 2\nOutput: n", 1,
                    a("6, poi 6", "4, poi 6", "4, poi 2"),
                    a("Il primo Output avviene prima dell'incremento e conserva il valore 4.", "Il primo Output legge 4; dopo l'aggiornamento il secondo legge 6.", "L'Operazione aggiunge 2 al valore esistente: non sostituisce n con 2.")),
                q("l04-q3", "Vuoi raddoppiare 5. La traccia mostra prima valore = 5, poi doppio = 15. Dove conviene indagare per primo?", "", 0,
                    a("Nel blocco che ha calcolato doppio", "Nel colore del ramo di uscita", "Nell'Output, senza guardare il calcolo"),
                    a("È il primo punto in cui il valore osservato differisce da quello atteso, 10.", "Il colore non spiega perché un calcolo produce 15 invece di 10.", "L'Output può limitarsi a mostrare il 15 che ha ricevuto. Prima controlla dove è nato.")))
        };
    }

    private static Lesson[] unit2() {
        return new Lesson[] {
            l("l05", "u02", "Assegnare non è confrontare", "Distingui un aggiornamento da una domanda sul valore.",
                "Un'assegnazione calcola la parte destra e la salva nel nome a sinistra. b = a copia il valore attuale di a, "
                + "non crea un collegamento permanente. In Frociart usa = nelle Operazioni e, per chiarezza, == nei confronti. "
                + "Dentro una Decisione anche = è accettato come uguaglianza.",
                "Per capire un'assegnazione, sostituisci prima i nomi a destra con i loro valori attuali; soltanto dopo aggiorna il nome a sinistra.",
                "a = b; b = a non scambia due valori: la prima istruzione cancella il vecchio valore di a.",
                refs(FLOW_ASSIGN, FLOW_OPERATORS), "Somma di due numeri",
                q("l05-q1", "Quale valore conserva b?",
                    "Operazione: a = 4\nOperazione: b = a\nOperazione: a = 9\nOutput: b", 0,
                    a("4", "9", "13"),
                    a("b ha ricevuto il 4 presente in a al momento della copia e non viene più modificata.", "Cambiare a dopo la copia non aggiorna automaticamente b.", "Non c'è una somma: l'assegnazione b = a ha copiato un solo valore.")),
                q("l05-q2", "Con x = 4 già inizializzata, cosa fa la Decisione x == 7?", "", 2,
                    a("Sostituisce x con 7", "Aumenta x di 7", "Risulta falsa e lascia x a 4"),
                    a("== confronta: l'aggiornamento appartiene a un'Operazione di assegnazione.", "Un confronto non esegue un incremento.", "4 non è uguale a 7. La Decisione sceglie il ramo falso senza cambiare x.")),
                q("l05-q3", "Vuoi scambiare a = 3 e b = 8. Quale sequenza preserva entrambi i valori?", "", 1,
                    a("a = b; b = a", "temp = a; a = b; b = temp", "temp = b; b = temp; a = b"),
                    a("Dopo a = b, entrambi valgono 8; il vecchio 3 è perso.", "temp conserva 3, a riceve 8 e b riceve il 3 conservato.", "temp conserva 8, ma nessuna istruzione salva il 3: alla fine entrambi valgono 8.")))
            ,l("l06", "u02", "Un valore prima di ogni lettura", "Inizializza una variabile su tutti i percorsi che la usano.",
                "Una variabile deve ricevere un valore prima di essere letta. Un'assegnazione dentro un solo ramo "
                + "non basta per l'altro ramo. Scegli un valore iniziale sensato oppure assegna il risultato in entrambe "
                + "le uscite prima di ricongiungerle. In Frociart Verifica segnala le letture non inizializzate.",
                "Segui anche il ramo che non useresti nel caso normale e chiediti: «Da dove arriva questo valore?».",
                "Se bonus = 2 viene eseguito solo quando punti > 10, un Output di bonus dopo il bivio non ha un valore garantito per punti = 4.",
                refs(FLOW_ASSIGN, CS50_ONE, FLOW_IF), "Maggiore tra due numeri",
                q("l06-q1", "Un nuovo diagramma inizia con contatore = contatore + 1. Che cosa manca?", "", 1,
                    a("Un secondo Output", "Un valore iniziale per contatore", "Una freccia colorata"),
                    a("Stampare non fornisce il valore che il calcolo deve leggere.", "La parte destra legge contatore: prima, per esempio, assegna contatore = 0.", "Il colore della freccia non inizializza le variabili.")),
                q("l06-q2", "bonus deve valere 2 sopra 10 punti e 0 negli altri casi. Quale correzione completa questo flusso?",
                    "Pseudocodice\nLeggi punti\nSe punti > 10: bonus = 2\nAltrimenti: nessuna assegnazione\nMostra bonus", 0,
                    a("Assegna bonus = 0 nel ramo altrimenti", "Assegna bonus = 2 anche nel ramo altrimenti", "Lascia il ramo vuoto: il valore sarà automaticamente 0"),
                    a("Così ogni percorso assegna il valore previsto prima dell'Output.", "Il flusso sarebbe inizializzato, ma darebbe un bonus anche a chi ha 10 punti o meno.", "Non presumere un valore automatico: in Frociart la variabile deve essere assegnata.")),
                q("l06-q3", "Vuoi contare i pezzi accettati. Prima di esaminare il primo pezzo, quale stato descrive correttamente il lavoro già svolto?", "", 2,
                    a("accettati = 1", "accettati = -1", "accettati = 0"),
                    a("Con 1 conteresti un pezzo che non hai ancora esaminato.", "−1 introdurrebbe un debito artificiale e il primo pezzo porterebbe soltanto a 0.", "Non è stato ancora accettato nessun pezzo: il conteggio iniziale è 0.")))
            ,l("l07", "u02", "Calcoli senza sorprese", "Usa parentesi, divisione e resto con un significato preciso.",
                "Moltiplicazione, divisione e resto precedono addizione e sottrazione; le parentesi rendono esplicito l'ordine. "
                + "Per interi positivi, a % b restituisce il resto della divisione. In Frociart / può produrre decimali "
                + "anche se i due valori sono interi: 7 / 2 vale 3.5.",
                "Se il risultato dipende dal raggruppamento, scrivi le parentesi e verifica il calcolo con numeri piccoli.",
                "(2 + 3) * 4 vale 20; 2 + 3 * 4 vale 14. La stessa lista di numeri non implica lo stesso calcolo.",
                refs(FLOW_OPERATORS, CS50_ONE), "Pari o dispari",
                q("l07-q1", "Quale valore produce l'espressione in Frociart?", "Espressione Frociart\n2 + 3 * 4", 2,
                    a("20", "24", "14"),
                    a("20 richiede (2 + 3) * 4. Qui la moltiplicazione viene prima.", "24 si otterrebbe moltiplicando tutti e tre i numeri, ma il primo operatore è +.", "Prima 3 * 4 = 12, poi 2 + 12 = 14.")),
                q("l07-q2", "Dividi 17 pezzi in gruppi completi da 5. Quanti ne avanzano?", "Espressione Frociart\n17 % 5", 1,
                    a("3", "2", "3.4"),
                    a("3 è il numero di gruppi completi, non il resto.", "Tre gruppi usano 15 pezzi; 17 − 15 = 2 restano fuori.", "3.4 è il risultato della divisione 17 / 5; % chiede il resto.")),
                q("l07-q3", "Qual è il risultato di questa divisione nel motore di Frociart?", "Espressione Frociart\n7 / 2", 0,
                    a("3.5", "3", "1"),
                    a("Frociart conserva la parte decimale: sette diviso due vale 3.5.", "Alcuni linguaggi troncano la divisione tra interi; Frociart non applica quella regola.", "1 è il resto 7 % 2, non il quoziente.")))
            ,l("l08", "u02", "Numero, testo o vero/falso?", "Riconosci il tipo del dato e i limiti dei decimali.",
                "Il numero 6, il testo \"6\" e il booleano vero hanno ruoli diversi. Le virgolette indicano testo. "
                + "In Frociart + unisce testo se almeno un operando è testuale. I numeri usano una rappresentazione "
                + "finita: alcuni decimali sono approssimati, anche quando l'Output li mostra arrotondati.",
                "Prima del calcolo controlla il tipo. Per misure approssimate, definisci una tolleranza adatta alla precisione richiesta.",
                "\"6\" + 2 dà il testo 62. Inoltre 0.1 + 0.2 può apparire come 0.3 nell'Output pur differendo nel confronto esatto.",
                refs(FLOW_TYPES, CS50_ONE), "Somma di due numeri",
                q("l08-q1", "Che cosa mostra l'Output?", "Espressione Frociart\n\"6\" + 2", 1,
                    a("8", "62", "Un errore in ogni caso"),
                    a("8 richiede il numero 6. Qui \"6\" è un testo.", "In Frociart la presenza del testo fa unire le rappresentazioni: \"6\" e \"2\".", "Il motore supporta questa concatenazione, quindi produce testo invece di fallire.")),
                q("l08-q2", "Come risulta questo confronto in Frociart?", "Espressione Frociart\n\"6\" == 6", 2,
                    a("Vero, perché le cifre sono uguali", "Assegna il numero 6 al testo", "Falso, perché i tipi sono diversi"),
                    a("Il confronto di uguaglianza non converte automaticamente il testo in numero.", "== è un confronto: non assegna valori e il testo non è una variabile.", "Un testo e un numero sono valori diversi, anche se appaiono uguali a schermo.")),
                q("l08-q3", "Per questo esercizio accetti uno scarto inferiore a 0.000001. Quale confronto lo esprime?", "Valori da confrontare\n0.1 + 0.2\n0.3", 0,
                    a("abs((0.1 + 0.2) - 0.3) < 0.000001", "(0.1 + 0.2) == 0.3", "(0.1 + 0.2) > 0.3"),
                    a("abs misura la distanza senza segno; la confronti con la tolleranza fissata per il problema.", "L'uguaglianza esatta non ammette alcuno scarto; qui risulta falsa per l'approssimazione numerica.", "Questo chiede soltanto quale valore è maggiore, senza limitare la loro distanza.")))
        };
    }

    private static Lesson[] unit3() {
        return new Lesson[] {
            l("l09", "u03", "Le soglie contano", "Traduci parole come almeno, oltre e al massimo senza perdere il confine.",
                "Una Decisione valuta vero o falso. «Almeno 12» include 12 e diventa >= 12; «oltre 12» diventa > 12. "
                + "Prepara entrambe le uscite. In Frociart puoi cambiarne dicitura e colore: il ramo vero resta associato "
                + "alla condizione vera, qualunque sia il suo nome.",
                "Prova il valore esattamente sulla soglia: spesso basta per distinguere > da >=.",
                "Se l'accesso richiede almeno 12 punti, punti > 12 respinge per errore chi ne ha esattamente 12.",
                refs(FLOW_IF, MIT_TEST), "Maggiore tra due numeri",
                q("l09-q1", "Servono almeno 12 punti. Quale condizione accetta anche il valore 12?", "", 0,
                    a("punti >= 12", "punti > 12", "punti == 12"),
                    a(">= include l'uguaglianza e tutti i valori superiori.", "> esclude proprio il valore di confine 12.", "== accetta 12 ma respinge 13, 14 e gli altri valori che soddisfano «almeno».")),
                q("l09-q2", "Quale ramo viene seguito con a = 8 e b = 8?", "Decisione Frociart\na >= b", 2,
                    a("Il ramo falso", "Entrambi i rami", "Il ramo vero"),
                    a("Il ramo falso richiederebbe a < b; qui i due valori sono uguali.", "Una Decisione sceglie una sola uscita per ogni valutazione.", "8 >= 8 è vero: l'uguaglianza è inclusa.")),
                q("l09-q3", "Rinomini l'uscita vera da «Sì» a «Valido» e ne cambi il colore. Quando viene seguita?", "", 1,
                    a("Quando scrivi la parola Valido nell'Input", "Quando l'espressione della Decisione è vera", "Sempre, perché il nome è positivo"),
                    a("Il nome dell'uscita è un'etichetta: non introduce un nuovo confronto con l'Input.", "Dicitura e colore descrivono il ramo; la scelta continua a dipendere dall'espressione.", "Il significato del testo non modifica la logica: una condizione falsa segue l'altra uscita.")))
            ,l("l10", "u03", "Combinare le condizioni", "Usa and, or e not per esprimere un requisito completo.",
                "and richiede che entrambe le condizioni siano vere. or richiede che almeno una sia vera, "
                + "anche entrambe. not inverte vero e falso. In Frociart puoi usare questi nomi nelle espressioni. "
                + "Le parentesi aiutano a vedere quale gruppo stai combinando o negando.",
                "Per due condizioni, prova le quattro coppie vero/vero, vero/falso, falso/vero e falso/falso: vedi subito cosa accetti.",
                "x >= 1 or x <= 5 accetta anche x = 100, perché la prima parte è vera. Per restare tra 1 e 5 servono entrambe.",
                refs(FLOW_OPERATORS, CS50_ONE), "Pari o dispari",
                q("l10-q1", "Vuoi accettare x tra 1 e 5, estremi inclusi. Quale espressione serve?", "", 1,
                    a("x >= 1 or x <= 5", "x >= 1 and x <= 5", "x > 1 and x < 5"),
                    a("Con or basta una parte: anche 100 o −10 passerebbero.", "and impone contemporaneamente il limite inferiore e quello superiore, inclusi.", "Questa versione esclude 1 e 5, ma il requisito li include.")),
                q("l10-q2", "Un valore è fuori intervallo se è minore di 1 oppure maggiore di 5. Quale condizione descrive tutti questi casi?", "", 0,
                    a("x < 1 or x > 5", "x < 1 and x > 5", "x == 1 or x == 5"),
                    a("Uno dei due limiti violati è sufficiente per essere fuori intervallo.", "Un numero non può essere contemporaneamente minore di 1 e maggiore di 5.", "1 e 5 sono i confini inclusi; questa espressione selezionerebbe proprio valori ammessi.")),
                q("l10-q3", "Qual è il risultato?", "Variabili\npermesso = vero\npronto = falso\n\nEspressione Frociart\nnot (permesso and pronto)", 2,
                    a("Falso", "Non è calcolabile", "Vero"),
                    a("Dentro le parentesi and dà falso; not trasforma quel falso in vero.", "Entrambe le variabili hanno un valore booleano: il calcolo è definito.", "vero and falso è falso, e la negazione di falso è vero.")))
            ,l("l11", "u03", "Una categoria alla volta", "Distingui decisioni indipendenti da alternative esclusive.",
                "Due decisioni successive possono attivare due azioni. Una catena «se... altrimenti se...» "
                + "sceglie invece la prima alternativa vera. L'ordine dei controlli conta quando le soglie si "
                + "sovrappongono. Nel flowchart, collega il ramo falso al controllo successivo e ricongiungi le categorie alla fine.",
                "Per categorie esclusive, scrivi un valore che soddisfa più soglie e controlla quale incontrerà per prima.",
                "Se controlli prima punti >= 60 e poi, soltanto nell'altrimenti, punti >= 90, un valore 95 finisce nella categoria 60.",
                refs(FLOW_IF, CS50_ONE), "Maggiore tra due numeri",
                q("l11-q1", "Che cosa mostra questo pseudocodice con n = 12?",
                    "Pseudocodice · due decisioni indipendenti\nSe n >= 10: mostra \"Grande\"\nSe n >= 0: mostra \"Non negativo\"", 2,
                    a("Soltanto Grande", "Soltanto Non negativo", "Grande, poi Non negativo"),
                    a("La seconda Decisione viene comunque raggiunta ed è anch'essa vera.", "Anche la prima condizione è vera: 12 è almeno 10.", "Sono due controlli indipendenti, entrambi veri per 12; vengono mostrati due risultati.")),
                q("l11-q2", "Vuoi una sola categoria: Alta da 90 in su, Media da 60 a 89, Bassa sotto 60. Quale ordine funziona?", "", 1,
                    a("Se punti >= 60: Media; altrimenti se >= 90: Alta; altrimenti Bassa", "Se punti >= 90: Alta; altrimenti se >= 60: Media; altrimenti Bassa", "Se punti >= 90: Alta; poi indipendentemente se >= 60: Media"),
                    a("Un 95 soddisfa già >= 60 e non raggiunge Alta.", "La prima soglia intercetta i valori più alti; la seconda riceve solo quelli sotto 90.", "Un 95 produrrebbe sia Alta sia Media: non sarebbe una categoria unica.")),
                q("l11-q3", "Che cosa viene mostrato con temperatura = -2?",
                    "Pseudocodice · alternative esclusive\nSe temperatura < 0: mostra \"Sotto zero\"\nAltrimenti se temperatura == 0: mostra \"Zero\"\nAltrimenti: mostra \"Sopra zero\"", 0,
                    a("Sotto zero", "Zero", "Sotto zero e Sopra zero"),
                    a("La prima condizione è vera; le alternative successive non vengono eseguite.", "−2 non è uguale a 0 e ha già soddisfatto la prima condizione.", "L'altrimenti finale vale solo quando le precedenti condizioni sono false.")))
            ,l("l12", "u03", "Controlla prima di calcolare", "Proteggi divisioni e radici con condizioni sul dominio.",
                "Una formula può richiedere dati validi: il divisore deve essere diverso da zero, la radice quadrata "
                + "richiede un numero non negativo. Controlla il requisito prima del calcolo. Puoi usare due Decisioni "
                + "successive. In Frociart and valuta la parte destra soltanto se la sinistra è vera: è il corto circuito.",
                "Metti il requisito prima dell'operazione che ne dipende. Disegna anche cosa succede quando il requisito non è soddisfatto.",
                "Calcolare totale / numero e soltanto dopo chiedere numero != 0 è troppo tardi: con numero = 0 il calcolo è già fallito.",
                refs(MIT_SPEC, FLOW_IF, FLOW_OPERATORS), "Somma di due numeri",
                q("l12-q1", "Devi calcolare la media totale / numero. Quale sequenza evita la divisione per zero?", "", 1,
                    a("Calcola la media, poi controlla numero", "Controlla numero != 0; calcola solo nel ramo vero", "Calcola sempre e cambia colore all'Output"),
                    a("Quando arrivi al controllo, l'operazione pericolosa è già stata eseguita.", "Il ramo falso può spiegare l'assenza di dati senza eseguire la divisione.", "L'aspetto dell'Output non rende valida una divisione per zero.")),
                q("l12-q2", "Quale condizione ammette tutti e soli i numeri utilizzabili da sqrt(x)?", "", 2,
                    a("x > 0", "x != 0", "x >= 0"),
                    a("Escluderebbe 0, ma sqrt(0) è definita e vale 0.", "Ammetterebbe valori negativi come −4 e rifiuterebbe per errore lo zero.", "La radice quadrata reale di un numero non negativo è definita; i negativi vengono esclusi.")),
                q("l12-q3", "Con totale = 12 e numero = 0, che cosa fa questa Decisione in Frociart?",
                    "Decisione Frociart\nnumero != 0 and totale / numero > 2", 0,
                    a("Risulta falsa senza eseguire la divisione", "Divide per zero e dà errore", "Risulta vera perché totale è positivo"),
                    a("La prima parte è falsa: il corto circuito di and salta la parte destra.", "Con and la divisione non viene valutata quando numero != 0 è falso.", "totale positivo non basta: la prima condizione richiesta è già falsa.")))
        };
    }

    private static Lesson[] unit4() {
        return new Lesson[] {
            l("l13", "u04", "Il ciclo può fare zero giri", "Leggi un ciclo con controllo iniziale e riconosci quando salta il corpo.",
                "Un ciclo while controlla prima la condizione. Se è vera, esegue il corpo e torna al controllo; "
                + "se è falsa, prosegue fuori dal ciclo. In Frociart lo costruisci con una Decisione e una freccia di ritorno. "
                + "Un ciclo con controllo dopo il corpo, detto do, esegue invece almeno un giro.",
                "Prova una condizione già falsa al primo controllo: chiarisce subito se il corpo deve essere eseguito almeno una volta.",
                "Con n = 0, «mentre n > 0 mostra n» non mostra 0. Spostare l'Output prima della Decisione cambierebbe questo comportamento.",
                refs(FLOW_WHILE, FLOW_DO), "Conto alla rovescia",
                q("l13-q1", "Quante volte viene eseguito Mostra n?",
                    "Pseudocodice\nn = 0\nMentre n > 0:\n  mostra n\n  n = n - 1", 0,
                    a("Zero", "Una", "Infinite"),
                    a("0 > 0 è falso al primo controllo: il corpo viene saltato.", "Una esecuzione sarebbe garantita solo con un controllo dopo il corpo.", "Non si entra nel ciclo, quindi non si ripete nulla.")),
                q("l13-q2", "Qual è la sequenza mostrata dal corpo del ciclo?",
                    "Pseudocodice\nn = 3\nMentre n > 0:\n  mostra n\n  n = n - 1", 2,
                    a("3, 2, 1, 0", "2, 1", "3, 2, 1"),
                    a("Quando n diventa 0, il controllo è falso e l'Output non viene eseguito.", "Il primo Output precede il decremento, quindi mostra anche 3.", "Ogni giro mostra il valore positivo e lo riduce; a 0 il ciclo finisce.")),
                q("l13-q3", "Vuoi chiedere un dato almeno una volta, anche se una vecchia variabile contiene già un valore valido. Dove metti la prima lettura?", "", 1,
                    a("Soltanto dopo un controllo che potrebbe già essere falso", "Prima del primo controllo di validità", "Dopo Fine"),
                    a("Quel controllo potrebbe saltare la lettura e riutilizzare un dato vecchio.", "Così la prima lettura avviene sempre; poi decidi se serve ripeterla.", "Un blocco dopo Fine non fa parte del percorso eseguito.")))
            ,l("l14", "u04", "Conta i giri, evita il giro in più", "Collega inizio, condizione e incremento al numero di ripetizioni voluto.",
                "Un contatore descrive a quale giro sei arrivato. Inizio, confronto e incremento devono funzionare "
                + "insieme: da i = 0 con i < 5 e incremento di 1 esegui cinque giri. <= includerebbe anche 5. "
                + "In Frociart i cicli contati usano gli stessi blocchi Decisione e Operazione.",
                "Scrivi il primo e l'ultimo valore che devono entrare nel corpo, poi controlla il valore immediatamente successivo.",
                "i = 0 con i <= 5 visita 0, 1, 2, 3, 4, 5: sono sei giri, non cinque.",
                refs(FLOW_FOR, MIT_TEST), "Conto alla rovescia",
                q("l14-q1", "Parti da i = 0 e aggiungi 1 a ogni giro. Quale condizione fa entrare esattamente cinque volte?", "", 2,
                    a("i <= 5", "i < 4", "i < 5"),
                    a("Include anche i = 5: i valori da 0 a 5 sono sei.", "Visita 0, 1, 2 e 3: sono soltanto quattro giri.", "Visita 0, 1, 2, 3 e 4; con i = 5 si ferma.")),
                q("l14-q2", "Quali valori vengono mostrati?",
                    "Pseudocodice\ni = 1\nMentre i < 7:\n  mostra i\n  i = i + 2", 0,
                    a("1, 3, 5", "1, 3, 5, 7", "3, 5"),
                    a("L'incremento è 2; raggiunto 7, il confronto i < 7 è falso.", "7 è il primo valore escluso dal confronto stretto.", "Il primo Output avviene con i = 1, prima dell'incremento.")),
                q("l14-q3", "Quanto vale i subito dopo l'uscita dal ciclo?",
                    "Pseudocodice\ni = 0\nMentre i < 4:\n  i = i + 1\nMostra i", 1,
                    a("3", "4", "5"),
                    a("Con i = 3 il corpo viene ancora eseguito e porta i a 4.", "Il ciclo si ferma proprio quando 4 < 4 risulta falso.", "Non viene eseguito un altro incremento dopo il controllo falso.")))
            ,l("l15", "u04", "Conserva il lavoro già fatto", "Distingui contatore, somma e prodotto e scegli l'inizializzazione.",
                "Un contatore cresce di 1 per ogni elemento; una somma cresce del valore dell'elemento. "
                + "Un accumulatore conserva il risultato parziale tra i giri: inizializzalo prima del ciclo. "
                + "Prima di elaborare dati, una somma vale 0 e un prodotto parte da 1, che lascia invariato il primo fattore.",
                "Chiediti cosa rappresenta il risultato prima di aver letto dati: questo aiuta a scegliere il valore iniziale.",
                "Se assegni somma = 0 a ogni giro, dimentichi i dati precedenti. Se inizi un prodotto da 0, ogni moltiplicazione lo lascia a 0.",
                refs(CORNELL_DESIGN, FLOW_ASSIGN), "Somma di due numeri",
                q("l15-q1", "Quale totale ottieni dopo aver elaborato 4, 1 e 3?",
                    "Pseudocodice\nsomma = 0\nPer ciascuno dei tre valori, letto uno alla volta:\n  somma = somma + valore\nMostra somma", 1,
                    a("3", "8", "4"),
                    a("3 è l'ultimo valore letto, non il totale accumulato.", "La somma passa da 0 a 4, poi 5, poi 8.", "4 è il totale dopo il primo dato; restano da aggiungere 1 e 3.")),
                q("l15-q2", "Vuoi contare quante letture hai fatto, indipendentemente dal loro valore. Quale aggiornamento serve?", "", 2,
                    a("conteggio = valore", "conteggio = conteggio + valore", "conteggio = conteggio + 1"),
                    a("Così salveresti il dato appena letto, perdendo il conteggio precedente.", "Questo calcola una somma di valori, non il numero di letture.", "Ogni nuova lettura aggiunge esattamente un elemento al conteggio.")),
                q("l15-q3", "Vuoi moltiplicare i valori 3 e 2. Quale correzione serve?",
                    "Pseudocodice con un errore\nprodotto = 0\nprodotto = prodotto * 3\nprodotto = prodotto * 2", 0,
                    a("Inizializza prodotto a 1", "Inizializza prodotto a 2", "Lascia 0: il primo fattore lo sostituirà"),
                    a("1 * 3 * 2 = 6: uno è il valore iniziale che non altera i fattori.", "Otterresti 2 * 3 * 2 = 12, introducendo un fattore extra.", "Il primo aggiornamento moltiplica 0 per 3: non lo sostituisce con 3.")))
            ,l("l16", "u04", "Leggi fino al segnale di fine", "Gestisci una sentinella senza contarla come un dato.",
                "Una sentinella è un valore riservato che significa «basta». Se i dati ammessi sono numeri non negativi, "
                + "puoi riservare −1. Leggi un valore, controlla se è la sentinella, elaboralo soltanto se non lo è, "
                + "poi leggi il successivo. Il segnale di fine non entra nel conteggio né nel totale.",
                "Prova a inviare subito il segnale di fine: scopri se il tuo flusso gestisce anche l'assenza di dati.",
                "Se 0 è un dato valido, usarlo anche come segnale di fine impedisce di distinguere uno zero da elaborare da una richiesta di terminare.",
                refs(CORNELL_SENTINEL, MIT_TEST), "Conto alla rovescia",
                q("l16-q1", "Dati ammessi: numeri non negativi. −1 termina e non viene elaborato. Leggi 2, poi 0, poi −1. Quali sono conteggio e somma?", "", 0,
                    a("Conteggio 2, somma 2", "Conteggio 3, somma 1", "Conteggio 1, somma 2"),
                    a("2 e 0 sono due dati validi; −1 è escluso. La somma è 2 + 0 = 2.", "Hai contato e sommato anche la sentinella, che non è un dato.", "Lo zero è un dato valido e va contato, anche se non aumenta la somma.")),
                q("l16-q2", "Il primo valore letto è 4. Che cosa manca per poter ricevere in seguito la sentinella?",
                    "Pseudocodice con un errore\nLeggi valore\nMentre valore != -1:\n  mostra valore\n  torna al controllo", 1,
                    a("Un secondo Output di valore", "Una nuova lettura prima di tornare al controllo", "Cambiare il nome del ramo falso"),
                    a("Stampare di nuovo non aggiorna valore: il controllo continuerebbe a vedere 4.", "Ogni giro deve acquisire il dato successivo, così può incontrare anche −1.", "L'etichetta del ramo non modifica il valore controllato.")),
                q("l16-q3", "Lo zero è un valore ammesso. Vuoi un segnale di fine distinto da tutti i dati validi, che sono non negativi. Quale scelta è coerente?", "", 2,
                    a("0", "Un numero positivo scelto a caso", "−1, riservato esplicitamente al termine"),
                    a("0 ha già un significato come dato: usarlo come fine renderebbe ambiguo l'input.", "Un numero positivo può essere un dato valido e causare un arresto involontario.", "−1 è fuori dal dominio dei dati e ha un significato dichiarato: terminare.")))
        };
    }

    private static Lesson[] unit5() {
        return new Lesson[] {
            l("l17", "u05", "Trova la prima differenza", "Trasforma un risultato errato in un'indagine ripetibile.",
                "Per cercare un bug, conserva un input che lo riproduce e scrivi il risultato atteso. "
                + "Riduci il caso mantenendo l'errore, osserva i valori intermedi, formula un'ipotesi e verifica "
                + "una modifica mirata. Quando il caso funziona, riprova anche quelli che funzionavano prima.",
                "Spiega a voce cosa dovrebbe fare ogni blocco e confrontalo con ciò che fa davvero, usando un caso piccolo che fallisce.",
                "Cambiare tre formule insieme può far sparire il sintomo senza chiarire la causa, e può introdurre un altro errore.",
                refs(MIT_DEBUG, CS50_DEBUG), "Esercizio: trova l'errore",
                q("l17-q1", "Vuoi il doppio di 5, cioè 10. Dove compare la prima differenza rispetto a quell'obiettivo?",
                    "Traccia osservata\nInput: n = 5\nOperazione A: doppio = 2 * n → 10\nOperazione B: doppio = doppio + 1 → 11\nOutput: doppio → 11", 2,
                    a("Nell'Input", "Nell'Output", "Nell'Operazione B"),
                    a("L'Input ha letto correttamente 5 e l'Operazione A produce il doppio atteso.", "L'Output riceve già 11: la differenza è comparsa prima.", "A produce correttamente 10; B aggiunge un 1 non richiesto e genera il primo valore errato.")),
                q("l17-q2", "Un diagramma grande fallisce sempre con una certa serie di input. Qual è un esperimento utile?", "", 0,
                    a("Accorcia la serie mantenendo l'errore, poi controlla un'ipotesi", "Cambia i valori a caso finché una prova passa", "Rimuovi tutti i controlli di validità"),
                    a("Un caso piccolo e ripetibile riduce ciò che devi osservare e permette di valutare la modifica.", "Una prova diversa che passa non spiega perché il caso originale falliva.", "Togliere i controlli può nascondere il sintomo o creare errori diversi, senza isolare la causa.")),
                q("l17-q3", "Dopo una correzione, che cosa fai con il piccolo caso che prima falliva?", "", 1,
                    a("Lo elimini perché ormai è risolto", "Lo conservi e lo riprovi insieme ai casi precedenti", "Provi solo input molto più grandi"),
                    a("Perderesti un controllo utile contro la ricomparsa dello stesso errore.", "Diventa un test di regressione: controlla che l'errore non torni e che il resto continui a funzionare.", "I casi grandi possono non attraversare la stessa situazione che causava il bug.")))
            ,l("l18", "u05", "Test scelti, non numeri a caso", "Copri categorie diverse e valori vicini ai confini.",
                "Dividi gli input in categorie che il programma tratta diversamente: sotto soglia, sulla soglia, "
                + "dentro e fuori intervallo. Prova anche zero elementi, un solo elemento e più elementi nei cicli. "
                + "Scrivi l'output atteso prima di eseguire. I test danno evidenza sui casi provati, non una prova generale di correttezza.",
                "Per una soglia intera k, prova k − 1, k e k + 1. Per un intervallo fai lo stesso ai due estremi.",
                "Provare 8, 9 e 10 non rileva necessariamente un errore sul limite 20. Sono tre valori, ma possono percorrere tutti lo stesso ramo.",
                refs(MIT_TEST), "Maggiore tra due numeri",
                q("l18-q1", "Accetti gli interi tra 5 e 20 inclusi. Quale gruppo controlla da vicino entrambi i confini?", "", 1,
                    a("8, 10, 12", "4, 5, 6, 19, 20, 21", "100, 200, 300"),
                    a("Sono soltanto casi interni: non distinguono bene < da <= sui limiti.", "C'è un valore sotto, uno sul confine e uno sopra per ciascuna delle due soglie.", "Sono tutti fuori dallo stesso lato e non controllano né i valori ammessi né i due confini.")),
                q("l18-q2", "Il tuo diagramma supera sei test. Quale conclusione è giustificata?", "", 0,
                    a("Produce i risultati attesi per quei sei casi", "È corretto per ogni input possibile", "Non può contenere un ciclo infinito"),
                    a("Hai evidenza concreta su quei casi; altri input richiedono altri test o un ragionamento generale.", "Un insieme finito di esempi scelti non copre automaticamente tutti gli input.", "Un altro input potrebbe imboccare un percorso che quei sei casi non hanno visitato.")),
                q("l18-q3", "Devi provare il massimo tra due numeri a e b. Quali categorie controllano le relazioni possibili?", "", 2,
                    a("Solo a > b, con molti numeri diversi", "Solo numeri entrambi positivi", "a > b, a < b e a = b"),
                    a("Cambiare i numeri nello stesso caso non verifica l'altro ramo né l'uguaglianza.", "Il segno è utile da variare, ma da solo non copre le relazioni tra i due input.", "Le tre relazioni coprono i due ordini e il caso di confine dell'uguaglianza; aggiungi anche valori negativi nei casi concreti.")))
            ,l("l19", "u05", "Una promessa a ogni giro", "Usa un invariante per spiegare il significato delle variabili del ciclo.",
                "Un invariante è una proprietà vera ogni volta che torni al controllo del ciclo. "
                + "Deve valere all'inizio ed essere ristabilito dal corpo. Per esempio, dopo i giri che aggiungono "
                + "due pezzi ciascuno, totale = 2 * i. Alla fine, unisci questa proprietà al controllo falso per ricavare il risultato.",
                "Scrivi cosa rappresenta l'accumulatore rispetto ai giri già completati, poi verifica inizio, un giro e uscita.",
                "«totale vale il risultato finale» di solito non è un invariante: durante il ciclo il risultato è ancora parziale.",
                refs(CORNELL_PROOF, CORNELL_DESIGN), "Conto alla rovescia",
                q("l19-q1", "Quale proprietà vale a ogni controllo di questo ciclo?",
                    "Pseudocodice\ni = 0; totale = 0\nMentre i < 3:\n  totale = totale + 2\n  i = i + 1", 0,
                    a("totale == 2 * i", "totale == 6", "totale == i + 2"),
                    a("All'inizio 0 = 2 * 0; ogni giro aggiunge 2 a totale e 1 a i, conservando la relazione.", "6 è il totale soltanto all'uscita; all'inizio totale vale 0.", "Già all'inizio 0 non è uguale a 0 + 2, quindi la proprietà non è invariata ai controlli.")),
                q("l19-q2", "L'invariante è totale == 2 * i ai controlli. Da i = 1, totale = 2 esegui totale = totale + 2 ma non ancora i = i + 1. È un errore che la relazione sia temporaneamente falsa?", "", 2,
                    a("Sì: un invariante deve valere dopo ogni singola istruzione", "Sì: totale non deve mai cambiare", "No, purché sia ristabilita prima del prossimo controllo"),
                    a("Qui il punto dichiarato è il controllo del ciclo, non ogni istruzione intermedia del corpo.", "L'accumulatore deve cambiare; la proprietà riguarda la sua relazione con i.", "Dopo il successivo incremento i diventa 2 e totale = 4 torna uguale a 2 * i.")),
                q("l19-q3", "Il ciclo precedente termina con i = 3. Se l'invariante totale == 2 * i è stato mantenuto, cosa puoi concludere?", "", 1,
                    a("totale è sempre stato 6", "Il totale finale è 6", "totale può essere qualsiasi numero"),
                    a("All'inizio valeva 0 e poi era parziale: soltanto il valore finale è 6.", "Sostituendo i = 3 nella relazione ottieni totale = 2 * 3 = 6.", "La relazione mantenuta vincola il totale; con i = 3 non può avere un altro valore.")))
            ,l("l20", "u05", "Perché il ciclo si ferma?", "Individua una misura di progresso che non possa diminuire per sempre.",
                "Per spiegare la terminazione, cerca una quantità intera non negativa che diminuisce a ogni giro "
                + "finché la condizione è vera: è una variante. Un invariante da solo non basta. "
                + "La presenza di una freccia verso Fine non garantisce che venga seguita. Il limite di passi del motore "
                + "interrompe una prova troppo lunga, ma non dimostra la correttezza del ciclo.",
                "Chiediti: «Che cosa resta da fare? Diminuisce davvero a ogni giro? Può continuare a diminuire senza limite?».",
                "Partendo da 0 e aggiungendo 2, i non raggiunge mai 5. La guardia i != 5 non esprime «elabora 0, 2 e 4, poi fermati».",
                refs(CORNELL_PROOF, FLOW_WHILE), "Conto alla rovescia",
                q("l20-q1", "Quale quantità misura i giri ancora necessari?",
                    "Pseudocodice · interi piccoli\ni = 0\nMentre i < 6:\n  i = i + 1", 2,
                    a("i", "6 + i", "6 - i"),
                    a("i aumenta: descrive il lavoro fatto, non una quantità decrescente di lavoro rimasto.", "6 + i aumenta a ogni giro e non misura l'avvicinamento all'uscita.", "6 − i scende di 1 da 6 fino a 0; con 0 giri rimasti il controllo è falso.")),
                q("l20-q2", "Parti da n = 2. Perché questo ciclo è sbagliato se vuoi contare verso zero?",
                    "Pseudocodice con un errore\nMentre n > 0:\n  n = n + 1", 0,
                    a("L'aggiornamento allontana n dalla condizione di uscita", "Manca soltanto un Output", "La condizione è falsa già al primo controllo"),
                    a("n cresce invece di scendere. Un arresto imposto dai limiti del motore sarebbe un errore di esecuzione, non il conto alla rovescia richiesto.", "Stampare n non corregge la direzione dell'aggiornamento.", "Con n = 2, n > 0 è vero e rimane vero durante gli incrementi considerati.")),
                q("l20-q3", "Vuoi elaborare 0, 2 e 4, poi fermarti. Parti da i = 0 e aggiungi 2 dopo ogni elaborazione. Quale guardia funziona?", "", 1,
                    a("i != 5", "i < 5", "i <= 6"),
                    a("i passa da 4 a 6: non incontra 5, quindi questa guardia non porta all'uscita voluta.", "0, 2 e 4 sono sotto 5; dopo l'ultimo incremento i = 6 e il ciclo si ferma.", "Includerebbe anche 6, aggiungendo un'elaborazione non richiesta.")))
        };
    }

    private static Lesson[] unit6() {
        return new Lesson[] {
            l("l21", "u06", "Dividi il problema con un contratto", "Descrivi ciò che ogni parte riceve e garantisce prima di implementarla.",
                "Decomporre significa separare compiti comprensibili: leggere, validare, calcolare, presentare. "
                + "Un contratto specifica le precondizioni richieste in ingresso e il risultato promesso in uscita. "
                + "Le funzioni personalizzate qui sono pseudocodice di studio: Frociart offre blocchi e funzioni "
                + "predefinite, ma non esegue la definizione di una tua funzione.",
                "Per ogni parte scrivi una frase con «riceve» e «restituisce», aggiungendo le condizioni necessarie sui dati.",
                "Una funzione che si chiama media ma non dice cosa fare con zero dati lascia un caso fondamentale senza accordo.",
                refs(MIT_SPEC, MIT_DECOMPOSE), "Somma di due numeri",
                q("l21-q1", "Quale precondizione rende definita questa media di un insieme non vuoto?",
                    "Pseudocodice · funzione da studiare, non eseguibile come definizione in Frociart\nmedia(totale, numero):\n  restituisci totale / numero", 1,
                    a("totale deve essere positivo", "totale è numerico; numero è un intero maggiore di 0", "Il risultato deve essere maggiore di 0"),
                    a("Il totale può essere zero o negativo; inoltre questa condizione non protegge da numero = 0.", "Il conteggio positivo esclude l'insieme vuoto e il divisore zero, senza vietare totali negativi.", "È un vincolo sull'uscita, non sui dati iniziali, e molte medie valide sono zero o negative.")),
                q("l21-q2", "Il contratto promette di restituire un numero da usare in un calcolo successivo. Quale comportamento lo soddisfa?", "", 0,
                    a("Restituire il valore al chiamante", "Mostrare il valore e non restituirlo", "Cambiare soltanto il titolo della funzione"),
                    a("Restituire fornisce il valore alla parte che lo deve usare. Questo è il significato del contratto.", "Mostrare e restituire sono azioni diverse: un messaggio a schermo non consegna il risultato al calcolo successivo.", "Il nome aiuta a capire il compito, ma non produce il valore promesso.")),
                q("l21-q3", "Vuoi riutilizzare il calcolo di una media sia in un report sia in un confronto. Quale organizzazione aiuta?", "", 2,
                    a("Mescolare lettura, calcolo e messaggi in ogni copia", "Usare una sola variabile per ogni significato possibile", "Separare il calcolo dai modi di leggere e mostrare i dati"),
                    a("Ogni copia andrebbe corretta separatamente e dipenderebbe dal suo modo di mostrare i dati.", "Riutilizzare un nome per ruoli diversi rende lo stato meno chiaro, non il calcolo più riutilizzabile.", "Le due parti possono condividere lo stesso contratto del calcolo e presentare il risultato in modi diversi.")))
            ,l("l22", "u06", "Cerca senza escludere la risposta", "Confronta ricerca lineare e ricerca binaria, incluse le loro condizioni.",
                "La ricerca lineare esamina gli elementi in ordine fino a trovare il valore o finire i dati. "
                + "Su una sequenza già ordinata, la ricerca binaria confronta il centro e scarta la metà incompatibile. "
                + "Ripetere dimezzamenti richiede pochi passi anche quando la sequenza cresce. Le liste mostrate qui "
                + "sono pseudocodice: non sono array eseguibili in Frociart.",
                "Prima di scartare una parte dei dati, spiega perché non può contenere la risposta. Nella ricerca binaria lo garantisce l'ordinamento.",
                "Nella lista non ordinata [18, 4, 12], cercando 18 non puoi scartare la sinistra solo perché 18 > 4: elimineresti proprio la risposta.",
                refs(CS50_ALGORITHMS), "",
                q("l22-q1", "Una ricerca lineare parte da sinistra e si ferma quando trova 14. Quanti elementi confronta?",
                    "Pseudocodice · lista di studio\n[3, 8, 14, 21, 27, 35, 42]\nCerca: 14", 2,
                    a("1", "7", "3"),
                    a("14 non è il primo elemento: prima si controllano 3 e 8.", "Non serve arrivare alla fine quando il valore è già stato trovato.", "I confronti riguardano 3, poi 8, poi 14: il terzo conclude la ricerca.")),
                q("l22-q2", "Cerchi 27 nella lista ordinata. Il primo confronto è con il centro 21. Quale parte rimane da cercare?",
                    "Pseudocodice · lista ordinata\n[3, 8, 14, 21, 27, 35, 42]", 0,
                    a("[27, 35, 42]", "[3, 8, 14]", "Soltanto [21]"),
                    a("27 è maggiore di 21; tutti gli elementi a sinistra sono al massimo 21 e si possono escludere.", "La parte sinistra contiene soltanto valori minori di 21, quindi non può contenere 27.", "21 è già stato confrontato e non è il valore cercato.")),
                q("l22-q3", "Devi cercare una volta sola in dati non ordinati. Quale affermazione è corretta?", "", 1,
                    a("La ricerca binaria è applicabile direttamente a qualsiasi ordine", "Puoi usare la ricerca lineare; per quella binaria devi prima garantire l'ordine", "Il colore degli elementi permette di evitare il confronto"),
                    a("Senza ordine, eliminare metà dati in base al valore centrale può perdere la risposta.", "La ricerca lineare funziona anche senza ordinamento. Se ordini prima, conta anche quel lavoro nel costo complessivo.", "L'algoritmo deve confrontare i valori; un colore non sostituisce il requisito di ordinamento.")))
            ,l("l23", "u06", "Quanto lavoro cresce con i dati?", "Conta operazioni significative e distingui crescita lineare e quadratica.",
                "Chiama n la quantità di dati. Due passaggi da n visite costano 2n visite; un passaggio da n ripetuto "
                + "per ognuno degli n elementi costa n². Big O descrive un limite superiore alla crescita per input grandi, "
                + "ignorando fattori costanti: O(n), O(n²), O(log n) per i dimezzamenti. Non indica un numero esatto di secondi.",
                "Scegli l'operazione da contare. Somma i passaggi consecutivi; per cicli annidati conta quante volte viene ripetuto davvero il corpo interno.",
                "Due cicli uno dopo l'altro non equivalgono a due cicli uno dentro l'altro: n + n e n * n crescono diversamente.",
                refs(CORNELL_COMPLEXITY, CS50_ALGORITHMS), "",
                q("l23-q1", "Fai due scansioni complete da 20 elementi, una dopo l'altra. Quante visite esegui in tutto?", "", 0,
                    a("40", "400", "20"),
                    a("Le scansioni sono consecutive: 20 + 20 = 40 visite, un costo lineare al crescere di n.", "400 sarebbe il costo di 20 visite ripetute per ciascuno dei 20 elementi, cioè cicli annidati.", "20 conta soltanto una delle due scansioni.")),
                q("l23-q2", "Quante marcature esegue questo pseudocodice?",
                    "Pseudocodice · conteggio astratto\nPer ciascuna di 4 righe:\n  per ciascuna di 4 colonne:\n    esegui una marcatura", 2,
                    a("8", "4", "16"),
                    a("8 somma righe e colonne; qui ogni riga esegue tutte e quattro le colonne.", "4 conta una sola riga, ma il corpo interno viene ripetuto su quattro righe.", "Le coppie riga-colonna sono 4 * 4 = 16; una marcatura per coppia.")),
                q("l23-q3", "Nel caso con esattamente n² marcature, passi da n = 4 a n = 8. Di quanto aumenta il numero di marcature?", "", 1,
                    a("Raddoppia", "Quadruplica", "Resta uguale"),
                    a("Raddoppia ciascuna dimensione, quindi il totale viene moltiplicato per 2 * 2.", "Da 4² = 16 passi a 8² = 64: quattro volte le marcature. Non è una promessa sui millisecondi reali.", "n compare nel numero di ripetizioni; aumentando n aumenta anche il lavoro.")))
            ,l("l24", "u06", "Progetto: un report affidabile", "Metti insieme specifica, traccia, casi limite e stato minimo.",
                "Progetta un report di misure numeriche tra −100 e 100 inclusi. 9999 termina; gli altri valori fuori "
                + "intervallo vengono segnalati e scartati, poi si legge ancora. Alla fine mostra media e massimo, "
                + "oppure «Nessun dato». Bastano conteggio, somma e massimo aggiornati a ogni dato valido: "
                + "non devi conservare una lista. Prova il progetto con piccoli casi prima di ampliarlo.",
                "Scrivi i risultati attesi per nessun dato, un solo dato, soli negativi e valori ai limiti. Verifica anche un dato rifiutato.",
                "Se parti da massimo = 0 e confronti soltanto con >, la sequenza −8, −3 restituisce 0: un massimo che non era nei dati.",
                refs(MIT_SPEC, MIT_TEST, CORNELL_DESIGN, CORNELL_SENTINEL), "Report di misure",
                q("l24-q1", "Vuoi trattare anche sequenze interamente negative. Quale regola dà il massimo corretto?", "", 2,
                    a("Parti da massimo = 0 e sostituiscilo solo con numeri maggiori", "Usa sempre l'ultimo numero letto come massimo", "Usa il primo dato valido come massimo, poi confronta i successivi"),
                    a("Con −8 e −3 il risultato resterebbe 0, che è maggiore di entrambi ma non è un dato ricevuto.", "L'ultimo dato non è sempre il più grande: nella sequenza 6, 2 il massimo è 6.", "Il massimo iniziale appartiene ai dati. Per −8, −3 viene aggiornato da −8 a −3; il caso senza dati resta separato.")),
                q("l24-q2", "L'utente inserisce subito 9999. Che cosa deve fare il report?", "", 1,
                    a("Mostrare media = 0 / 0 e massimo = 0", "Mostrare Nessun dato senza calcolare la media", "Contare 9999 come unica misura"),
                    a("0 / 0 non è una media definita; inoltre non esiste un massimo senza dati.", "La sentinella non è una misura: conteggio resta zero e l'uscita dedicata evita calcoli senza significato.", "9999 è riservato alla terminazione e deve restare fuori dal report.")),
                q("l24-q3", "Con la specifica di questa lezione, leggi 105, −4, 6, 9999. Quali valori finali sono corretti?", "", 0,
                    a("Media 1, massimo 6", "Media 35.666…, massimo 105", "Media 6, massimo 6"),
                    a("105 è fuori intervallo e viene scartato; restano −4 e 6. Somma 2, conteggio 2, media 1 e massimo 6.", "Questo risultato include il 105 che la specifica richiede di rifiutare.", "−4 è una misura valida: va inclusa nella somma e nel conteggio, anche se è negativa.")))
        };
    }
}
