![Frociart — La logica prende forma](docs/assets/frociart-cover.png)

# Frociart v1

**Disegna, esegui e comprendi i diagrammi di flusso.**

Frociart è un editor di flowchart per Windows e Android. Collega i blocchi, costruisci decisioni con due rami e prova il risultato con valori diversi. Questa versione è dedicata alla creazione e alla simulazione dei flowchart.

[Sito del progetto](https://mamt104.github.io/Frociart/) · [Scarica per Windows](https://mamt104.github.io/Frociart/download/Frociart_v1.exe) · [Scarica per Android](https://mamt104.github.io/Frociart/download/Frociart_v1_Android.apk)

## Cosa puoi fare

- **Costruire diagrammi:** usa blocchi di inizio, fine, lettura, scrittura, operazione e decisione.
- **Usare i simboli degli esercizi:** un solo parallelogramma **Input / Output**. Scrivi direttamente **leggi x** oppure **stampa x**: la lettura diventa azzurra e la stampa viola. Il toggle **Separa Input e Output** mostra due voci nella palette senza cambiare la logica del progetto.
- **Organizzare il flusso:** trascina i blocchi, inseriscili nei collegamenti e riordina il diagramma.
- **Gestire le decisioni:** collega separatamente i rami Sì e No e personalizzane etichette e colori.
- **Creare cicli:** collega un'uscita anche alla stessa casella. Su Android trascina il `+` sul corpo del blocco, oppure scegli **Questo blocco** nel menu degli arrivi.
- **Passare al pseudocodice:** converti il diagramma in testo oppure scrivi il codice e crea un nuovo flowchart, senza collegarti a servizi esterni.
- **Lavorare con vettori:** crea un elenco di valori dello stesso tipo, leggi o modifica un elemento con `valori[i]` e calcola lunghezza, somma, minimo e massimo.
- **Trovare funzioni ed esempi:** cerca «radice», «decimali» o il nome di una funzione nel catalogo e inserisci un esempio nel punto in cui stai scrivendo. Disponibile nei blocchi e nel pannello pseudocodice, su PC e Android.
- **Condividere una prova leggibile:** esporta un PNG con diagramma, variabili, indici dei vettori e risultato insieme.
- **Provare la logica:** verifica il diagramma, inserisci i valori richiesti e segui l'esecuzione fino al risultato.
- **Riprendere il lavoro:** salva i progetti in formato `.frociart` e riaprili dalla home con un clic o un tocco. Su Windows trovi i file recenti; su Android i progetti salvati sul telefono.

L'app funziona offline e non richiede un account.

## Download

| Piattaforma | Versione | Pacchetto | Utilizzo |
| --- | --- | --- | --- |
| Windows x64 | v1 · 1.82 | [Autonomo](https://mamt104.github.io/Frociart/download/Frociart_v1.exe) | Runtime incluso. |
| Windows AnyCPU | v1 · 1.82 | [Classico](https://mamt104.github.io/Frociart/download/Frociart_v1_Classico.exe) | Richiede .NET Framework 4.8. |
| Windows x64 | v1 · 1.82 | [Setup](https://mamt104.github.io/Frociart/download/Frociart_v1_Setup.exe) | Installazione per utente. |
| Android | v1 · build 100 | [APK](https://mamt104.github.io/Frociart/download/Frociart_v1_Android.apk) | Installa l'app sul telefono. |

**Requisiti:** Windows autonomo e setup per x64; il primo include .NET 10, il secondo può scaricare componenti Microsoft mancanti. La variante classica AnyCPU richiede .NET Framework 4.8. Android richiede API 26 (8.0) o successivo. Consulta il rapporto di collaudo per gli ambienti effettivamente provati. La connessione serve per scaricare i pacchetti e visitare il sito del progetto.

## Installazione

### Windows

1. Scarica e apri **Frociart_v1.exe**: l'app si avvia direttamente.
2. Per icona e doppio clic sui progetti, chiudi eventuali altre copie dell'app e scegli **Progetto → Impostazioni del progetto e dei file → Attiva icona e doppio clic dei progetti**. La configurazione vale per il tuo utente.
3. Apri i file `.frociart` dall'app oppure con doppio clic dopo la configurazione.

Se Windows usa già un'altra applicazione per questi file, scegli **Apri con → Frociart**.

### Android

Scarica l'APK sul telefono, aprilo e segui la procedura di installazione di Android. Il sistema può chiedere di autorizzare l'installazione dal browser o dal gestore file usato per aprire il pacchetto.

## Primi passi

1. Crea un nuovo diagramma oppure apri un esempio.
2. Aggiungi i blocchi e collega il percorso da Inizio a Fine.
3. Modifica operazioni, variabili e condizioni.
4. Verifica il diagramma e avvia l'esecuzione. Fornisci i valori richiesti e osserva il percorso e il risultato.
5. Salva il progetto per continuare in seguito.

La sezione Impara è sospesa in questa versione; i progetti precedenti restano conservati.

### Comandi utili

- **Riordina** organizza la disposizione dei blocchi.
- **Tutto** adatta la vista per mostrare l'intero diagramma.
- **Annulla e Ripeti** consentono di tornare sulle modifiche.

Su Windows sono disponibili le scorciatoie per lavorare con i blocchi:

| Scorciatoia | Azione |
| --- | --- |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | Copia, taglia e incolla la selezione. |
| `Ctrl+A` | Seleziona tutti i blocchi. |
| `Ctrl+D` | Duplica la selezione. |
| `Ctrl+Z` / `Ctrl+Y` | Annulla o ripeti una modifica. |
| `Canc` | Elimina i blocchi selezionati. |

Durante la modifica di un campo di testo, le scorciatoie di copia e incolla agiscono sul testo.

## Flowchart e pseudocodice

Su Windows il pseudocodice si aggiorna in tempo reale nel pannello in basso a destra. Per importarlo apri **Altro → Importa / modifica pseudocodice…**. Su Android apri **Pseudocodice ↔ diagramma** dal menu **⋯** dell’editor oppure usa **Importa pseudocodice** nella home.

- **Dal diagramma** trascrive il progetto aperto.
- Scrivi o incolla il testo, oppure usa **Apri testo** per un file UTF-8. **Crea diagramma** genera un nuovo progetto disposto sul foglio.
- **Copia** e **Salva testo** conservano il pseudocodice. **Sintassi** mostra comandi ed esempi. Su PC puoi creare il diagramma anche con `Ctrl+Invio`.

```text
INIZIO
LEGGI numero
SE numero >= 0 ALLORA
    STAMPA "Positivo o zero"
ALTRIMENTI
    STAMPA "Negativo"
FINE SE
FINE
```

Sono supportati assegnazioni, lettura e scrittura, `SE/ALTRIMENTI`, `MENTRE`, `PER/FOR` e `RIPETI/FINCHÉ`, anche con le parole chiave equivalenti in inglese. Nei grafi complessi, etichette e `VAI A` mantengono esplicite le destinazioni. `SCOLLEGATO` rappresenta un'uscita libera. Gli errori di sintassi indicano la riga da correggere e lasciano il testo nell'editor.

La conversione ricrea la logica; posizioni, colori e nomi personalizzati dei rami si conservano nel formato `.frociart`. Le espressioni usano lo stesso motore del simulatore: i vettori sono supportati; le funzioni personali non sono ancora disponibili. Il limite è di 256 Ki caratteri, 5.000 righe, 48 livelli annidati e 500 blocchi. Dopo la conversione usa **Verifica** ed **Esegui** per controllare il programma con i tuoi dati.

## Vettori e cicli FOR

Nell'editor di pseudocodice trovi **Esempi → Vettori e ciclo FOR**. Puoi anche usare le stesse espressioni direttamente nei blocchi:

```text
INIZIO
valori = [10, 20, 30]
PER i DA 0 A len(valori) - 1 FAI
    STAMPA valori[i]
FINE PER
STAMPA "Totale: " + somma(valori)
FINE
```

Gli indici partono da **0**. `valori[1] = 25` modifica il secondo elemento; `leggi valori[1]` ne chiede il valore. `vettore(5)` crea cinque zeri. Gli elementi devono essere tutti numeri, tutti testi oppure tutti valori logici; massimo 1.024 elementi. Sono disponibili `len`, `somma`/`sum`, `min` e `max`.

`PASSO` è facoltativo e vale 1; puoi usare un altro intero costante diverso da zero, anche negativo. Il limite finale viene calcolato una volta e conservato nella variabile visibile `limite_ciclo_N`. Il ciclo si trasforma in blocchi di operazione e decisione modificabili. Quando lo riconverti in testo, viene mostrato come `MENTRE` equivalente.

## Funzioni ed esempi

Apri **Funzioni ed esempi** mentre modifichi il contenuto di un blocco o il pseudocodice. Cerca il nome della funzione oppure ciò che vuoi fare, come «radice», «testo» o «decimali». Ogni voce spiega gli argomenti e mostra un esempio con il suo risultato. L'inserimento sostituisce il testo selezionato o aggiunge l'esempio alla posizione del cursore.

| Categoria | Funzioni disponibili |
| --- | --- |
| Numeri | `abs`, `sqrt`, `int`, `floor`, `round`, `sgn`, `min`, `max`, `random` |
| Trigonometria | `sin`, `cos`, `tan`, `arcsin`, `arccos`, `arctan`; costante `pi` o `π` |
| Logaritmi | `ln` / `log` per il logaritmo naturale; `log10` in base dieci |
| Testi | `len`, `char`, `tochar`, `tocode` |
| Conversioni | `tointeger`, `toreal`, `tostring`, `tofixed` |
| Vettori | `vettore` / `array`, `size`, `len`, `somma` / `sum`, `min`, `max` |

```text
INIZIO
prezzi = [12.5, 8, 24]
totale = somma(prezzi)
STAMPA "Totale: " + tofixed(totale, 2)
dado = random(6) + 1
STAMPA "Dado: " + dado
FINE
```

Il primo risultato è `Totale: 44.50`. `random(6)` restituisce un intero da 0 a 5; aggiungendo 1 ottieni un dado da 1 a 6. I nomi delle funzioni non distinguono maiuscole e minuscole.

Gli angoli sono in **radianti**: per convertire i gradi usa `gradi * pi / 180`. `int` elimina la parte decimale verso zero, mentre `floor` arrotonda verso il basso. `round` e `tofixed` accettano da 0 a 15 decimali e arrotondano le metà lontano da zero. I numeri hanno precisione finita: chiedere molti decimali non aggiunge precisione al valore calcolato.

`tointeger` converte un testo intero tra -2147483648 e 2147483647. `toreal` accetta punto o virgola decimale ed esponenti, senza separatori delle migliaia. `len` e `char` contano i punti di codice Unicode: `len("A😀é")` vale 3 e `char("A😀é", 1)` restituisce `😀`; una sequenza composta da più punti di codice può apparire come un solo simbolo. I progetti precedenti che contavano le due metà UTF-16 di un'emoji ora usano questo conteggio coerente.

Le funzioni sono locali e non richiedono servizi esterni. La lettura di file tramite `EOF` e le funzioni definite dall'utente non sono ancora disponibili.

## Diagrammi e variabili leggibili da condividere

- **Windows:** nel pannello Esecuzione scegli **Vista da condividere**, controlla l'anteprima e premi **Salva PNG**.
- **Android:** nella prova scegli **Immagine con dati**, poi **Salva immagine** oppure **Condividi immagine**. La stessa voce è disponibile nel menu di esportazione.
- La prova si mette in pausa: l'immagine conserva diagramma, valori e risultato di quel momento. I vettori mostrano ogni indice con il suo valore. I testi lunghi vanno a capo.
- Le variabili si leggono anche durante la prova. Su PC fai doppio clic su una variabile; su Android tocca **Variabili** e scegli il nome per aprire il valore completo.

L'immagine viene preparata sul dispositivo. I progetti molto estesi possono superare il limite di un singolo PNG: in quel caso l'app spiega il problema, senza tagliare i dati o renderli minuscoli. Puoi leggere e copiare le singole variabili oppure esportare il solo diagramma in SVG.

## Aprire un progetto dal sito

Nella [pagina del progetto](https://mamt104.github.io/Frociart/#apri), scegli o trascina un file `.frociart`. Su Windows, **Apri in Frociart** lo passa all'app installata; il browser può chiedere conferma. Se l'app manca, scarica l'EXE, attiva l'apertura dei progetti dal menu Altro e poi riapri il file.

Per i progetti più grandi è disponibile **Salva il progetto**, seguito dall'apertura del file locale. Il sito legge il documento sul dispositivo e non lo carica su un server. I limiti di importazione sono 4 MB, 500 blocchi e 1.500 collegamenti.

## Dati e download

Progetti, bozze e cronologia dei file restano sul dispositivo. I dettagli sul trattamento dei dati sono nella [pagina Privacy e sicurezza](https://mamt104.github.io/Frociart/privacy.html).

Gli [hash SHA256](docs/download/SHA256SUMS_v1.txt) permettono di verificare l'integrità dei pacchetti. Gli EXE Windows non sono firmati con Authenticode: il sistema può mostrare un avviso sull'editore. L'APK Android è firmato.

## Assistenza

Per segnalare un problema, [apri una issue](https://github.com/mamt104/Frociart/issues) indicando versione dell'app, sistema operativo, passaggi per riprodurlo e comportamento atteso. Allega, se utile, un esempio privo di dati personali.

Questo repository ospita la pagina del progetto, la documentazione e i pacchetti di distribuzione.

## Autore e condizioni d’uso

**Frociart è un progetto di mamt104.** Copyright © 2026 mamt104. Tutti i diritti riservati.

L’uso delle versioni ufficiali è gratuito. Non è consentito rimuovere l’attribuzione o ripubblicare l’app presentandola come un proprio prodotto. I diagrammi creati restano dell’utente. Consulta le [condizioni complete](LICENSE.txt). Per condividere Frociart, usa il collegamento a questo progetto.

## Integrità e conservazione dei progetti

Gli hash dei quattro pacchetti sono in [SHA256SUMS_v1.txt](docs/download/SHA256SUMS_v1.txt). I termini dei componenti sono conservati in [download/Licenze](docs/download/Licenze/). Il salvataggio browser dei file riconosciuti mantiene byte e metadati originali. I formati più recenti vengono segnalati: aprili direttamente nell’app aggiornata.
