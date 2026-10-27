# Sicurezza di Frociart

Informazioni relative a Windows v1, Android v1 e al sito di distribuzione.

## Progetti e dati locali

Frociart funziona sul dispositivo, senza account o sincronizzazione remota. Progetti, preferenze, cronologia e diagnostica locale non vengono inviati automaticamente.

Il sito legge i documenti nel browser e non li carica su un server. GitHub ospita il sito e i download; consulta la [pagina Privacy](https://mamt104.github.io/Frociart/privacy.html) per i dettagli.

## Importazione

Il formato dei progetti prevede un massimo di 4 MB, 500 blocchi e 1.500 collegamenti. Prima dell'apertura vengono controllati struttura, identificatori e collegamenti. Aprire un progetto non ne avvia l'esecuzione.

Su Windows il comando di apertura dal browser trasferisce soltanto i dati del diagramma. Per i progetti che superano il limite del collegamento diretto, è disponibile l'apertura da file locale.

## Installazione e integrità

L'installazione Windows riguarda l'utente corrente e rispetta le associazioni dei file già scelte. Sono disponibili Windows autonomo x64 con runtime incluso, Windows classico AnyCPU e setup x64 per utente. Il setup indica i componenti Microsoft mancanti prima dell’eventuale download. I pacchetti non modificano UserChoice né sostituiscono silenziosamente le associazioni di altre applicazioni.

Gli EXE Windows non hanno una firma Authenticode; l'APK Android è firmato. Gli [hash SHA256](docs/download/SHA256SUMS_v1.txt) consentono di confrontare i file scaricati con quelli distribuiti. Un hash verifica l'integrità del file e non sostituisce una firma dell'editore.

## Segnalazioni

Nelle segnalazioni pubbliche includi versione, piattaforma e un esempio riproducibile senza informazioni personali. Non allegare credenziali o progetti riservati. Conserva una copia dei documenti importanti e mantieni aggiornato il sistema operativo.

Una firma o un hash non garantiscono l’assenza di segnalazioni antivirus. Non disattivare le protezioni per aprire un pacchetto segnalato: conserva il nome del rilevamento e l’hash per la verifica.

## Compatibilità e sito

Le verifiche riguardano i casi e gli ambienti registrati, non tutte le versioni di ogni sistema operativo. Hash e firme non certificano universalmente compatibilità o sicurezza. La pagina browser supporta il formato iniziale: preserva byte e metadati dei file riconosciuti e indirizza i formati più recenti all’app aggiornata. Gli aggiornamenti dei due siti (GitHub Pages e Cloudflare Pages) vanno verificati separatamente.
