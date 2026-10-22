![Frociart — La logica prende forma](docs/assets/frociart-cover.png)

# Frociart v1

**Disegna, esegui e comprendi i diagrammi di flusso.**

Frociart è un editor di flowchart per Windows e Android. Collega i blocchi, costruisci decisioni con due rami e prova il risultato con valori diversi. Le lezioni guidate aiutano a imparare la logica attraverso esempi ed esercizi pratici.

[Sito del progetto](https://mamt104.github.io/Frociart/) · [Scarica per Windows](https://mamt104.github.io/Frociart/download/Frociart_v1.exe) · [Scarica per Android](https://mamt104.github.io/Frociart/download/Frociart_v1_Android.apk)

## Cosa puoi fare

- **Costruire diagrammi:** usa blocchi di inizio, fine, lettura, scrittura, operazione e decisione.
- **Usare i simboli degli esercizi:** un solo parallelogramma **Input / Output**. Scegli **Leggi** per ricevere un dato o **Scrivi** per mostrare un risultato; forma e colore sono gli stessi.
- **Organizzare il flusso:** trascina i blocchi, inseriscili nei collegamenti e riordina il diagramma.
- **Gestire le decisioni:** collega separatamente i rami Sì e No e personalizzane etichette e colori.
- **Creare cicli:** collega un'uscita anche alla stessa casella. Su Android trascina il `+` sul corpo del blocco, oppure scegli **Questo blocco** nel menu degli arrivi.
- **Passare al pseudocodice:** converti il diagramma in testo oppure scrivi il codice e crea un nuovo flowchart, senza collegarti a servizi esterni.
- **Provare la logica:** verifica il diagramma, inserisci i valori richiesti e segui l'esecuzione fino al risultato.
- **Imparare facendo:** esplora lezioni guidate ed esempi, con esercizi e dimostrazioni sul diagramma.
- **Riprendere il lavoro:** salva i progetti in formato `.frociart` e riaprili quando vuoi.

L'app funziona offline e non richiede un account.

## Download

| Piattaforma | Versione | Pacchetto | Utilizzo |
| --- | --- | --- | --- |
| Windows | v1 | [EXE portatile](https://mamt104.github.io/Frociart/download/Frociart_v1.exe) | Avvia direttamente l'app. |
| Android | v1 | [APK](https://mamt104.github.io/Frociart/download/Frociart_v1_Android.apk) | Installa l'app sul telefono. |

**Requisiti:** Windows 10/11 con .NET Framework 4.8, oppure Android 8.0 e successivi. La connessione serve per scaricare i pacchetti e visitare eventuali risorse didattiche esterne.

## Installazione

### Windows

1. Scarica e apri **Frociart_v1.exe**: l'app si avvia direttamente.
2. Per icona e doppio clic sui progetti, chiudi eventuali altre copie dell'app e scegli **Altro → Attiva icona e doppio clic dei progetti**. La configurazione vale per il tuo utente.
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

Per un percorso guidato, apri **Impara**.

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

Su Windows apri **Altro → Pseudocodice ↔ diagramma**. Su Android trovi la stessa voce nel menu **⋯** dell'editor e in **Esporta**.

- **Dal diagramma** trascrive il progetto aperto.
- Scrivi o incolla il testo, oppure usa **Apri testo** per un file UTF-8. **Crea diagramma** genera un nuovo progetto disposto sul foglio.
- **Copia** e **Salva testo** conservano il pseudocodice. **Sintassi** mostra comandi ed esempi. Su PC puoi creare il diagramma anche con `Ctrl+Invio`.

```text
INIZIO
LEGGI numero
SE numero >= 0 ALLORA
    SCRIVI "Positivo o zero"
ALTRIMENTI
    SCRIVI "Negativo"
FINE SE
FINE
```

Sono supportati assegnazioni, lettura e scrittura, `SE/ALTRIMENTI`, `MENTRE` e `RIPETI/FINCHÉ`, anche con le parole chiave equivalenti in inglese. Nei grafi complessi, etichette e `VAI A` mantengono esplicite le destinazioni. `SCOLLEGATO` rappresenta un'uscita libera. Gli errori di sintassi indicano la riga da correggere e lasciano il testo nell'editor.

La conversione ricrea la logica; posizioni, colori e nomi personalizzati dei rami si conservano nel formato `.frociart`. Le espressioni usano lo stesso motore del simulatore: funzioni personali e liste non sono supportate. Il limite è di 256 Ki caratteri, 5.000 righe, 48 livelli annidati e 500 blocchi. Dopo la conversione usa **Verifica** ed **Esegui** per controllare il programma con i tuoi dati.

## Aprire un progetto dal sito

Nella [pagina del progetto](https://mamt104.github.io/Frociart/#apri), scegli o trascina un file `.frociart`. Su Windows, **Apri in Frociart** lo passa all'app installata; il browser può chiedere conferma. Se l'app manca, scarica l'EXE, attiva l'apertura dei progetti dal menu Altro e poi riapri il file.

Per i progetti più grandi è disponibile **Salva il progetto**, seguito dall'apertura del file locale. Il sito legge il documento sul dispositivo e non lo carica su un server. I limiti di importazione sono 4 MB, 500 blocchi e 1.500 collegamenti.

## Dati e download

Progetti e progressi restano sul dispositivo. I dettagli sul trattamento dei dati sono nella [pagina Privacy e sicurezza](https://mamt104.github.io/Frociart/privacy.html).

Gli [hash SHA256](docs/download/SHA256SUMS_v1.txt) permettono di verificare l'integrità dei pacchetti. Gli EXE Windows non sono firmati con Authenticode: il sistema può mostrare un avviso sull'editore. L'APK Android è firmato.

## Assistenza

Per segnalare un problema, [apri una issue](https://github.com/mamt104/Frociart/issues) indicando versione dell'app, sistema operativo, passaggi per riprodurlo e comportamento atteso. Allega, se utile, un esempio privo di dati personali.

Questo repository ospita la pagina del progetto, la documentazione e i pacchetti di distribuzione.
