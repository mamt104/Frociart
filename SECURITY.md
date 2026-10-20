# Sicurezza di Frociart

Informazioni relative a Windows 1.11.15, Android 1.7.11 e al sito di distribuzione.

## Progetti e dati locali

Frociart funziona sul dispositivo, senza account o sincronizzazione remota. Progetti, progressi e diagnostica locale non vengono inviati automaticamente. I collegamenti didattici esterni si aprono nel browser quando vengono selezionati.

Il sito legge i documenti nel browser e non li carica su un server. GitHub ospita il sito e i download; consulta la [pagina Privacy](https://mamt104.github.io/Frociart/privacy.html) per i dettagli.

## Importazione

Il formato dei progetti prevede un massimo di 4 MB, 500 blocchi e 1.500 collegamenti. Prima dell'apertura vengono controllati struttura, identificatori e collegamenti. Aprire un progetto non ne avvia l'esecuzione.

Su Windows il comando di apertura dal browser trasferisce soltanto i dati del diagramma. Per i progetti che superano il limite del collegamento diretto, è disponibile l'apertura da file locale.

## Installazione e integrità

L'installazione Windows riguarda l'utente corrente e rispetta le associazioni dei file già scelte. La rimozione tramite il pacchetto di configurazione conserva documenti e progressi.

Gli EXE Windows non hanno una firma Authenticode; l'APK Android è firmato. Gli [hash SHA256](docs/download/SHA256SUMS_1.11.15.txt) consentono di confrontare i file scaricati con quelli distribuiti. Un hash verifica l'integrità del file e non sostituisce una firma dell'editore.

## Segnalazioni

Nelle segnalazioni pubbliche includi versione, piattaforma e un esempio riproducibile senza informazioni personali. Non allegare credenziali o progetti riservati. Conserva una copia dei documenti importanti e mantieni aggiornato il sistema operativo.
