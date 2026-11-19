# Note storiche di collaudo

Questo documento conserva il collaudo delle revisioni precedenti. Per la versione 1.3.1 consultare VERIFICA.txt e i nuovi log nella cartella Verifiche.

# Frociart — checklist di accettazione

Per i risultati della versione 1.3.0, vedere `VERIFICA.txt`. Questa checklist conserva anche i criteri e le prove storiche della prima versione.

Questa checklist accompagna lo sviluppo. Un punto non verificato non va presentato all'utente come superato. La verifica strutturale e sintattica del diagramma e l'esecuzione di esempi non dimostrano che un algoritmo risolva sempre il problema desiderato.

## Criteri della versione 1.3.0

- Nessuna cornice TabControl nativa nell'ispettore. Intestazione e istruzioni seguono il tema senza giunture di sfondo.
- Campi, scrollbar, combo, tabella variabili, guida, export e messaggi dell'app restano leggibili dopo cambi tema. I sei comandi in alto a destra conservano lo stile originale.
- Le scrollbar rispondono al trascinamento reale; i campi conservano modifica, selezione e input. Schede e segnalazioni sono navigabili da tastiera e tramite oggetti di accessibilità.
- Il footer del picker usa lo stesso disegno delle altre voci, e conserva Esc e riapertura ripetuta.
- Il benchmark usa le stesse fixture e lo stesso runtime per versione precedente e candidata; distingue paint, input, cronologia e simulazione, senza promettere prestazioni su hardware diverso.
- Le cache del canvas rilevano anche modifiche dirette a nodi/archi; export, hit test, trasformazioni e cancellazione delle gesture restano coerenti.
- La copia profonda della cronologia mantiene il contratto CheckDocument, tutti i campi e l'indipendenza delle liste e degli oggetti, senza cambiare il formato dei file.
- Output lungo completo, selezione delle righe, reset della simulazione, input e bozza dell'editor sopravvivono agli aggiornamenti incrementali.
- La verifica con centinaia di diagnosi conserva tutte le voci, scorrimento fino all'ultima e attivazione del blocco corretto.

## Nuovi criteri della versione 1.2.0

- Una freccia lasciata sul vuoto apre la scelta del tipo al rilascio del sinistro. La scelta crea blocco e collegamento in una sola operazione Annulla; Esc non modifica il ramo preesistente.
- Il destro trascinato sposta soltanto la vista. Sinistro sul vuoto seleziona, sinistro sui blocchi sposta la selezione. Centrale e Spazio non attivano il pan.
- Il cambio tema conserva contenuto non applicato, selezione, zoom, pan, input e stato del programma. Alla riapertura usa la preferenza salvata.
- Menu Altro, menu del foglio e scelta del blocco restano riapribili e funzionanti dopo annullamenti e cambi tema. Nessuna voce viene eliminata durante il suo callback.
- PDF, SVG e PNG sono identici a parità di diagramma in modalità chiara e scura. Un export non cambia stato o contenuto dell'editor.
- I sei pulsanti in alto a destra mantengono etichetta, ordine, dimensioni e stile. Le azioni principali restano accessibili a dimensione minima e a 125%/150%.

`WorkflowTests.cs` verifica il percorso end-to-end e le sessioni del picker; `ThemeTests.cs` verifica tema, contenuto, esportazioni e layout. I risultati effettivamente ottenuti e i limiti dell'ambiente sono in `VERIFICA.txt`.

## P0 — percorso essenziale

1. Avvio Windows: aprire l'eseguibile su un ambiente Windows compatibile senza avviare browser, Node o Python. Se è richiesto un runtime Windows, dichiararlo con precisione.
2. Nuovo diagramma: aggiungere Inizio, Input, Operazione, Decisione, Output e Fine; selezionare, spostare, modificare il contenuto e cancellare un blocco. La cancellazione rimuove anche i collegamenti che lo toccano.
3. Collegamento: collegare la porta di uscita a un altro blocco; distinguere chiaramente i rami Sì e No. Non creare collegamenti accidentali quando si intende spostare un blocco.
4. Parità: acquisire `n`, valutarne la parità e produrre la risposta corretta per 4, 7, 0 e -3. La spiegazione distingue l'operatore di confronto dall'assegnazione.
5. Decisione incompleta: aprire l'esempio con un ramo mancante, avviare la verifica e vedere l'errore associato al blocco; attivare il risultato e raggiungere il blocco interessato.
6. Riparazione: aggiungere il ramo mancante; la verifica successiva non deve mantenere un vecchio errore già corretto.
7. Passo: eseguire una sola istruzione per volta; la selezione del blocco attivo, la tabella variabili e l'output devono corrispondere alla stessa situazione. Il valore di una variabile cambia solo quando il blocco corrispondente viene eseguito.
8. Input: chiedere un valore nel momento in cui l'esecuzione raggiunge un Input; annullare non deve essere interpretato come zero. Un dato non valido deve poter essere corretto senza produrre risultati inventati.
9. Fine: raggiungere Fine arresta l'esecuzione; un ulteriore Passo non aggiunge output e non ripete l'ultima istruzione. Riavviare azzera output, variabili e conteggio dei passi.
10. Arresto: un ciclo senza termine può essere fermato e incontra un limite di passi. Il limite produce un esito esplicito, non un finto successo e non una finestra bloccata.
11. Errori runtime: divisione per zero, radice non reale se prevista, variabile mai assegnata e overflow/non finitezza producono messaggi comprensibili associati al blocco corrente.
12. Salva e riapri: posizione, contenuto, tipologia, identificatori, destinazioni e etichette dei rami restano coerenti; l'esecuzione del file riaperto coincide con quella precedente.

## P0 — validatore strutturale

| Caso | Risultato atteso |
| --- | --- |
| Diagramma vuoto | Errore comprensibile: manca Inizio. |
| Nessun Inizio o più di un Inizio | Errore; evitare la scelta silenziosa di un punto di partenza. |
| Inizio con ingresso o Fine con uscita | Errore strutturale, se questi collegamenti non sono già impediti dall'editor. |
| Blocco eseguibile privo dell'uscita richiesta | Errore associato al blocco. |
| Decisione senza Sì o senza No | Errore che nomina il ramo mancante. |
| Due uscite dello stesso tipo | Errore oppure sostituzione esplicita e coerente; mai esecuzione arbitraria. |
| Sì e No diretti allo stesso blocco | Ammissibile sul piano strutturale; eventuale avviso didattico, non errore automatico. |
| Blocco scollegato dall'Inizio | Avviso di irraggiungibilità; nessun falso errore sul percorso raggiungibile. |
| Nessun percorso verso Fine | Errore o avviso chiaro secondo il contratto scelto; non confondere la raggiungibilità statica con la terminazione per ogni input. |
| Collegamento a ID inesistente | File respinto o errore esplicito; nessun crash. |
| ID duplicati, tipi non riconosciuti, contenuti null | File respinto senza perdere il diagramma aperto. |
| Espressione malformata | Errore di sintassi sul blocco prima dell'esecuzione quando rilevabile. |
| Espressione corretta che usa una variabile non definita | Diagnostica runtime quando raggiunta; evitare promesse di analisi statica completa. |

## P1 — interazione e robustezza

- Verificare hit test, creazione dei collegamenti, trascinamento e centratura su errore dopo zoom e spostamento del canvas.
- Provare nomi e testi lunghi, caratteri accentati italiani, stringhe con virgolette e valori negativi.
- La modifica di un diagramma durante o dopo un'esecuzione deve invalidare o riavviare lo stato runtime con una regola evidente.
- Un file malformato, incompleto o troppo grande non deve cancellare il documento corrente.
- Chiudere, aprire un altro file o caricare un esempio con modifiche non salvate deve offrire la protezione prevista dall'app.
- Verificare finestra piccola, ridimensionamento e scala display Windows 125% e 150%: nessuna azione primaria inaccessibile.
- Cancellare un blocco selezionato non lascia riferimenti pendenti nell'ispettore, nelle diagnostiche o nel runtime.
- Non richiedere la disponibilità di Internet per disegnare, salvare, verificare ed eseguire.

## Casi deterministici degli esempi inclusi

Le sequenze nella tabella indicano valori o righe successive, non un unico testo separato da virgole.

| Esempio | Input, in ordine | Output, riga per riga |
| --- | --- | --- |
| Pari o dispari | `7` | `Dispari` |
| Pari o dispari | `4` | `Pari` |
| Pari o dispari | `0` | `Pari` |
| Pari o dispari | `-3` | `Dispari` |
| Somma di due numeri | `2`, `3` | `Somma: 5` |
| Somma di due numeri | `-2`, `2` | `Somma: 0` |
| Somma di due numeri | `1,5`, `2.5` | `Somma: 4` |
| Conto alla rovescia | `3` | `3`, `2`, `1`, `Via!` |
| Conto alla rovescia | `0` | `Via!` |
| Conto alla rovescia | `-3` | `Via!` |
| Maggiore tra due numeri | `2`, `3` | `Maggiore: 3` |
| Maggiore tra due numeri | `5`, `5` | `Maggiore: 5` |
| Maggiore tra due numeri | `-8`, `-2` | `Maggiore: -2` |
| Esercizio: trova l'errore | Nessuno | Il validatore segnala il ramo No mancante; l'esecuzione non parte. |

La correzione del ramo mancante dovrebbe permettere di verificare ed eseguire lo stesso documento, senza richiedere chiusura o ricaricamento dell'app.

## Verifica automatica indipendente eseguita

`QA.Tests.cs` contiene 27 gruppi di verifica significativi del nucleo, eseguiti sotto Mono il 17 ottobre 2026: **27 superati, 0 falliti**. Coprono esempi e casi limite, priorità degli operatori, cortocircuito, parser chiuso, limiti di espressioni e stringhe, errori aritmetici, input correggibile, reset e Fine, atomicità di un blocco multilinea, definizione certa delle variabili su rami e cicli, raggiungibilità, rami convergenti, grafi malformati, budget dei cicli, clonazione, salvataggio e riapertura.

Questo risultato verifica il nucleo di calcolo e del modello. Non equivale alla verifica dell'interfaccia né all'avvio su un computer Windows reale.

La classe opzionale `UIEdgeTests`, nello stesso file e abilitata con `QA_UI`, ha inoltre superato **5 controlli mirati** sotto Mono con Xvfb: confronto di output multilinea, output vuoto esplicito, nome file con titolo nullo, conservazione della modifica non ancora applicata prima di una gesture sul canvas e riconoscimento del focus sui campi di testo. Questi casi verificano l'integrazione interessata; il collaudo visuale completo resta distinto.

Comando dalla cartella del progetto:

```sh
mcs -langversion:6 -r:System.Runtime.Serialization.dll -r:System.Core.dll -out:/tmp/Frociart.QA.exe -main:FrociartQA.CoreTests Core.cs Examples.cs QA.Tests.cs
mono /tmp/Frociart.QA.exe
```

## Facility minima proposta per test ripetibili

Per ogni caso: nome descrittivo, valori degli Input nell'ordine in cui verranno richiesti e output atteso riga per riga. Il caso deve restituire uno fra **Superato**, **Output diverso**, **Errore di esecuzione**, **Input esauriti** e **Limite passi raggiunto**. I risultati mostrano atteso e ottenuto affiancati, più il blocco dell'errore quando disponibile.

Prima versione consigliata: confronto testuale esatto dopo uniformazione dei fine-riga, nessuna rimozione silenziosa di spazi significativi. Se si introduce un confronto numerico tollerante, deve essere un'opzione visibile con tolleranza dichiarata, mai una conversione implicita di ogni riga.

Esempi di copertura didattica: un valore ordinario, zero, un numero negativo e un limite. Testare più esempi aumenta la fiducia nel comportamento; non costituisce una prova matematica generale.
