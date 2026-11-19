IMPARA — CONTENUTI E REGRESSIONI HOST

Percorso: 6 unità × 4 lezioni × 3 domande = 72 domande, con 216 feedback specifici.
Fonti: 23 pagine primarie di Flowgorithm, Harvard, MIT e Cornell.
Provenienza e mappa didattica completa: ../../FONTI_IMPARA.txt.

COMANDO

Dal repository FrociartAndroid:
  bash tests/learning/run_learning_tests.sh

Serve un JDK con il modulo jdk.compiler. I sorgenti applicativi e i test sono
compilati con --release 8. Il controllo non richiede Android né un emulatore.
Il percorso delle classi temporanee viene mostrato alla fine.

CHE COSA CONTROLLA

1. Dataset e contratto API
   - ID unici e stabili, 24 lezioni distribuite in sei unità, tre domande ciascuna.
   - Opzioni distinte, indice corretto valido, un feedback specifico per opzione.
   - Spiegazione, consiglio, controesempio e fonti presenti in tutte le lezioni.
   - Titoli di pratica corrispondenti agli esempi reali, senza riferimenti inventati.
   - Pseudocodice esplicito per funzioni dell'utente e liste non supportate dal motore.
   - I primi cinque esempi mantengono ordine e titolo; Report di misure è il sesto.

2. Provenienza degli URL
   - Catalogo confrontato con sources-reviewed.tsv, compilato da URL osservati.
   - Host istituzionali/autore, HTTPS, assenza di duplicati o riferimenti orfani.
   - Il test non effettua richieste HTTP. Il manifest distingue pagine lette live
     dagli estratti indicizzati ufficiali Flowgorithm, il cui server ha avuto timeout.

3. Risposte ed esempi
   - 37 quesiti riscontrati con il motore reale: assegnazioni e stato, scambio,
     tipi, divisione, resto, tolleranza, condizioni ai confini, diciture dei rami,
     corto circuito, conteggi, cicli, invarianti e progetto finale.
   - 5 calcoli accademici separati: ricerca su una lista e conteggio del lavoro.
     Questi non vengono attribuiti al supporto array del motore, che non esiste.
   - I test leggono le carte delle domande e le opzioni quando traducibili;
     per altre domande costruiscono il diagramma descritto dal caso.
   - Per condizioni alternative vengono cercati controesempi ai distrattori.
   - Il report esegue i casi vuoto, un solo zero, soli negativi, estremi inclusi
     e un dato scartato prima della somma. Il .frociart viene anche riaperto e provato.

4. FlowLearningProgress
   - Opzione o ID invalido non cambiano lo stato.
   - Errore → feedback → Riprova → risposta corretta; non si avanza da una risposta errata.
   - Tap ripetuti non saltano domande o cambiano una risposta già registrata.
   - Tre domande completano la lezione e attribuiscono 30 XP una sola volta.
   - Ripasso e primo tentativo hanno conteggi coerenti, senza doppiare XP.
   - Restore conserva insieme completamenti, domanda, risposta, errore e primo tentativo.
   - ID sconosciuti, valori fuori limite e schermate finali non commesse sono sanificati.

ARTEFATTI

results/learning-host-tests.log
  Esito della suite e numero di asserzioni dell'ultima esecuzione.

results/executed-question-ids.txt
  Elenco dei 42 ID di quesiti riscontrati mediante esecuzione o calcolo.

results/report-di-misure.frociart
../../examples/06_Report_di_misure.frociart
  Due copie identiche del sesto esempio generato da FlowExamples.report().
  La suite le rigenera in modo deterministico. Input e risultati attesi sono
  descritti anche in ../../examples/LEGGIMI.txt.

Il test del testo non può dimostrare automaticamente la correttezza pedagogica
di una spiegazione. Quesiti concettuali e feedback hanno una revisione editoriale;
la verifica native UI resta separata. Non vengono misurati FPS in questa suite.
