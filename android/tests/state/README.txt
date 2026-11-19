QUATTRO REGRESSIONI DI STATO — ANDROID 1.2.1

Non sono percorsi interamente manuali. La instrumentation usa il codice
dell'APK installato: reflection per creare fixture/controllare stato e
inoltrare callback del canvas, Dialog e View.performClick di produzione
per le azioni, un evento Back nativo. I gesti completi restano nella suite
UiProbe/ConnectionProbe separata. Non viene copiato MainActivity nell'APK
di test e non si sostituisce il motore di produzione.

SCENARI
1. Due documenti iniziali senza errori di Verifica e con coordinate valide
   ai limiti del formato; Ordina OFF. Il controller del + apre il picker
   reale, si sceglie Output e si chiude l'editor con Applica. Si verificano
   coordinate positive/negative, continuazione, FlowCodec e il salvataggio
   reale tramite FlowStore nel solo progetto QA.
2. Freccia errata Fine→Operazione: pannello reale con Scollega disponibile,
   rimozione solo della freccia e ripristino con il pulsante Undo reale.
3. Un CountDownLatch sospende la coda io prima di Verifica. Prima di
   rilasciarlo si apre Home oppure un editor in cui si scrive una bozza.
   Il risultato tardivo non deve sostituire il nuovo contesto. Il latch
   ha timeout e viene sempre rilasciato in finally; una barriera su io e
   poi sul main thread attende l'elaborazione del callback effettivo.
4. Modalità Scegli sul foglio → callback di arrivo diverso → conferma di
   riconnessione: X e Back devono lasciare isConnecting=false. Lo stesso
   vale entrando e uscendo da Home.

L'archivio di prova usa un nuovo ID dedicato. La fase finale ripristina
documento, preferenze e preferenza animazioni precedenti ed elimina solo
il progetto QA creato da questa instrumentation. Le animazioni dell'app
sono ridotte temporaneamente per isolare le transizioni di stato; la UI
animata è coperta dalle altre suite. Nessuna modifica alle preferenze
globali del dispositivo e nessuna operazione di condivisione.

COMPILARE (NON AVVIA ADB)
python3 tests/state/build_state_checks.py --sdk /percorso/sdk \
  --signing-dir /percorso/firma --output /tmp/Frociart-StateChecks.apk
Senza --signing-dir si può verificare la compilazione; prima dell'installazione
serve la stessa firma del pacchetto target.

ESEGUIRE (SOLO CONTROLLER ADB DESIGNATO)
adb -s emulator-5554 install --no-streaming -r /tmp/Frociart-StateChecks.apk
adb -s emulator-5554 shell am instrument -w \
  it.frociart.statechecks/it.frociart.statechecks.StateProbe

Marker richiesto: STATE_FINISHED checks=N errors=0.
Un errore produce STATE_ERROR e RESULT_CANCELED. Collegare i risultati
all'hash dell'APK target realmente installato e confrontare i source hash
con production_reference_sha256 del provenance.json generato dal builder.

Il report usa getTargetContext().getFilesDir(), nello spazio interno
dell'app target, con percorso relativo state-1.2.1/report.txt. Il percorso
assoluto risolto da Android viene scritto nella riga REPORT_FILE;
normalmente e' /data/user/0/it.frociart.app/files/state-1.2.1/report.txt.
L'intero report viene restituito anche nello stream finale della
instrumentation, senza richiedere accesso shell alla directory privata.

Lo storage esterno non e' un prerequisito di questi quattro scenari.
Le prove SAF reali di importazione/esportazione sono separate. Solo se
Android non rende disponibile la directory interna o non ne consente
la creazione il runner termina con STATE_ENV nello stream, checks=0
e un errore esplicito di ambiente. Non usa percorsi relativi o directory
alternative. Un errore durante la scrittura finale viene incluso nello
stream e nel conteggio errors.

Una sola esecuzione, senza matrice di schermi. Budget attivo 55 secondi
su Android già reattivo; l'avvio del dispositivo non è incluso. Il latch
ha un massimo di 8 secondi e viene rilasciato anche in caso di errore.
La compilazione da sola non costituisce un esito nativo: NOT_RUN fino
all'esecuzione da parte del controller ADB.
