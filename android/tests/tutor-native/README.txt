FROCIART 1.7.2 - PROVE NATIVE DEL CORSO

Usare soltanto un emulatore dedicato alle prove. Il probe rifiuta hardware
diverso da ranchu/goldfish. La fase course azzera i progressi e i documenti
didattici della fixture nell'emulatore: non usare un AVD con lavoro personale.
Il file APK dell'app di rilascio non contiene questo probe.

Fasi: surface (testo 200%, due orientamenti), rotation (runner, Input,
editor e ricreazione), backup (AtomicFile senza file base), course (24
lezioni/45 passi/12 errori), persist (dopo force-stop), layout (tastiera),
workshop (costruzione guidata, Input 2+3, output5 e copia nell'archivio),
legacy-restore, editor-restore (include controllo WindowLeaked del processo).
Fixture, introspezione e inserimento del testo sono dichiarati; navigazione
e azioni usano i controlli Android effettivi e tocchi iniettati. Non sono
partecipanti umani, né una misura di prestazioni su un telefono fisico.

Compilare un progetto test con il manifest qui presente e TutorProbe.java
in app/src/main/java/it/frociart/tutorchecks. build_apk.py compila anche
questo progetto separato. App e test devono avere la stessa firma.

adb -s <emulatore> install --no-streaming -r <TutorProbe.apk>
adb -s <emulatore> shell am instrument -w -e phase <fase> -e scenario <profilo> it.frociart.tutorchecks/it.frociart.tutorchecks.TutorProbe

Richiedere il marker TUTOR_NATIVE_FINISHED con errors=0; am instrument
può restituire exit0 anche quando una verifica è fallita. I risultati sono
in getExternalFilesDir()/tutor-qa/<fase>-<profilo>. Ogni log va collegato
all'hash dell'APK installato. I runner della sessione sono nelle verifiche.
