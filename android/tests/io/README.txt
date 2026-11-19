FROCIART ANDROID — CONTROLLI I/O NATIVI
Versione verificata: 1.7.2, 19 ottobre 2026.

SCOPO
IoInstrumentation esegue i componenti di produzione installati su Android:
FlowStore, FlowCodec, FlowExporter, PdfRenderer/BitmapFactory e ShareProvider.
L'archivio progetti usa un ContextWrapper con una cartella QA isolata nella
cache. Le esportazioni sono file reali; il provider interrogato è quello
effettivamente installato, con apertura in sola lettura.
Il test non apre DocumentsUI o il chooser di condivisione, non tocca progetti
personali e non invia file a persone o servizi. Non è un percorso utente
completo e non misura FPS.

COMPILA IL PROBE
Prima compila l'app da questi stessi sorgenti con build_apk.py, in un
ambiente di collaudo isolato. App e probe devono avere la medesima firma QA.
Non serve la chiave privata originale di distribuzione. Le chiavi non sono
incluse in questo archivio.

Dalla cartella FrociartAndroid, usando gli artefatti della build dell'app:

python3 tests/io/build_io_tests.py \
  --sdk /percorso/Android/Sdk --java-home /percorso/JDK \
  --app-classes build/apk/classes.jar \
  --signing-dir /percorso/firma-qa \
  --output /tmp/Frociart-IoChecks-QA.apk

Richiede Android SDK 35, Build Tools 35.0.0, JDK 17 e Python 3.
--app-classes deve indicare il classes.jar della medesima build che
installerai nell'emulatore. Il probe risolve le classi dell'app a runtime.

RUNNER E RACCOLTA
Sul solo emulatore QA, dopo avere installato l'app candidata con la stessa
firma QA, installa il probe e avvia l'instrumentation:

adb -s emulator-5554 install -r /tmp/Frociart-IoChecks-QA.apk
adb -s emulator-5554 shell am instrument -w it.frociart.iochecks/it.frociart.app.IoInstrumentation

Il runner termina con ANDROID I/O TESTS PASSED: 44 checks oppure con FAILED.
I file prodotti sono nella directory dell'app Android:
 /storage/emulated/0/Android/data/it.frociart.app.continua/files/io-test-artifacts

Per recuperarli:
adb -s emulator-5554 pull /storage/emulated/0/Android/data/it.frociart.app.continua/files/io-test-artifacts /tmp/frociart-io-artifacts

COPERTURA E RISULTATO CORRENTE
44 controlli PASS sulla candidata finale con FlowPdfPages:
salvataggio/recupero atomico e backup; rifiuto degli ingressi invalidi;
formati .frociart, PNG, SVG, PDF singolo e A4 multipagina; limiti dimensioni
e pagine; colori dei rami; output indipendente dal tema; provider MIME,
nome/dimensione, streaming, sola lettura e percorsi vietati.
Il test A4 nativo verifica produzione e numero di pagine. La leggibilità
dei nodi al confine pagina è valutata separatamente sui PDF renderizzati.
Esito visivo finale PASS sui tre PDF campione, tutte le sette pagine:
tests/results/update-1.2.1/native/export-review-final/LEGGIMI.txt.

EVIDENZE
- tests/results/update-1.2.1/native/io-final.log: 44 PASS.
- tests/results/update-1.2.1/native/io-final-artifacts/: file reali.
- tests/results/update-1.2.1/native/final-provenance.json:
  hash della candidata finale, del probe e della build eseguita.
- tests/results/update-1.2.1/native/candidate-final-source-sha256.json:
  ingressi di produzione finali.
I file io-provenance.json e candidate-source-sha256.json senza “final”
riguardano la candidata iniziale: non usarne gli hash per l'esito finale.
Il precedente percorso UI DocumentsUI in files-portrait.log conserva
timeout e azioni non raggiunte. Questi 44 PASS di componenti non lo
trasformano in una prova UI riuscita.
