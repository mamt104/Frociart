![Frociart — La logica prende forma](docs/assets/frociart-cover.png)

# Frociart

**Disegna, esegui e comprendi i diagrammi di flusso.**

Frociart è un editor di flowchart per Windows e Android. Collega i blocchi, costruisci decisioni con due rami e prova il risultato con valori diversi. Le lezioni guidate aiutano a imparare la logica attraverso esempi ed esercizi pratici.

[Sito del progetto](https://mamt104.github.io/Frociart/) · [Scarica per Windows](https://mamt104.github.io/Frociart/download/Frociart_Setup_1.11.15.exe) · [Scarica per Android](https://mamt104.github.io/Frociart/download/Frociart_1.7.11_Android.apk)

## Cosa puoi fare

- **Costruire diagrammi:** usa blocchi di inizio, fine, lettura, scrittura, operazione e decisione.
- **Organizzare il flusso:** trascina i blocchi, inseriscili nei collegamenti e riordina il diagramma.
- **Gestire le decisioni:** collega separatamente i rami Sì e No e personalizzane etichette e colori.
- **Provare la logica:** verifica il diagramma, inserisci i valori richiesti e segui l'esecuzione fino al risultato.
- **Imparare facendo:** esplora lezioni guidate ed esempi, con esercizi e dimostrazioni sul diagramma.
- **Riprendere il lavoro:** salva i progetti in formato `.frociart` e riaprili quando vuoi.

L'app funziona offline e non richiede un account.

## Download

| Piattaforma | Versione | Pacchetto | Utilizzo |
| --- | --- | --- | --- |
| Windows | 1.11.15 | [Configurazione EXE](https://mamt104.github.io/Frociart/download/Frociart_Setup_1.11.15.exe) | Installa Frociart e configura icona e apertura dei progetti con doppio clic. |
| Windows | 1.11.15 | [EXE portatile](https://mamt104.github.io/Frociart/download/Frociart_1.11.15.exe) | Avvia direttamente l'app. |
| Android | 1.7.11 | [APK](https://mamt104.github.io/Frociart/download/Frociart_1.7.11_Android.apk) | Installa l'app sul telefono. |

**Requisiti:** Windows 10/11 con .NET Framework 4.8, oppure Android 8.0 e successivi. La connessione serve per scaricare i pacchetti e visitare eventuali risorse didattiche esterne.

## Installazione

### Windows

1. Scarica ed esegui il pacchetto di configurazione.
2. Installa Frociart per il tuo utente; non servono privilegi amministratore.
3. Avvia l'app oppure fai doppio clic su un file `.frociart`.

Se Windows usa già un'altra applicazione per questi file, scegli **Apri con → Frociart**. Nella versione portatile puoi configurare icona e doppio clic dal menu **Altro → Attiva icona e doppio clic dei progetti**.

Il pacchetto di configurazione permette anche di rimuovere l'app, conservando progetti e progressi.

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

## Aprire un progetto dal sito

Nella [pagina del progetto](https://mamt104.github.io/Frociart/#apri), scegli o trascina un file `.frociart`. Su Windows, **Apri in Frociart** lo passa all'app installata; il browser può chiedere conferma. Se l'app manca, scarica il pacchetto di configurazione e poi riapri il file.

Per i progetti più grandi è disponibile **Salva il progetto**, seguito dall'apertura del file locale. Il sito legge il documento sul dispositivo e non lo carica su un server. I limiti di importazione sono 4 MB, 500 blocchi e 1.500 collegamenti.

## Dati e download

Progetti e progressi restano sul dispositivo. I dettagli sul trattamento dei dati sono nella [pagina Privacy e sicurezza](https://mamt104.github.io/Frociart/privacy.html).

Gli [hash SHA256](docs/download/SHA256SUMS_1.11.15.txt) permettono di verificare l'integrità dei pacchetti. Gli EXE Windows non sono firmati con Authenticode: il sistema può mostrare un avviso sull'editore. L'APK Android è firmato.

## Assistenza

Per segnalare un problema, [apri una issue](https://github.com/mamt104/Frociart/issues) indicando versione dell'app, sistema operativo, passaggi per riprodurlo e comportamento atteso. Allega, se utile, un esempio privo di dati personali.

Questo repository ospita la pagina del progetto, la documentazione e i pacchetti di distribuzione.
