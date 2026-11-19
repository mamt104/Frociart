FLOWDRAFTSTORE — CONTRATTO E TEST HOST

API
FlowDraftStore(SharedPreferences prefs)
Draft get(String project, String node)
String lastNode(String project)
void put(String project, String node, Draft value)
void remove(String project, String node)
void removeProject(String project)

Draft ha sei campi public final String, nell'ordine del costruttore:
text, prompt, trueLabel, falseLabel, trueColor, falseColor.
I colori mantengono la dicitura esadecimale del modello. I campi mancanti
usano i default del modello; testo/etichette vuoti sono consentiti nelle
bozze perché l'utente può non avere finito di scriverli.

INTEGRAZIONE
- X/chiusura editor: put del blocco modificato; nessuna applicazione al modello.
- Applica o Scarta: remove soltanto del progetto/blocco interessato.
- Cambio progetto: nessuna eliminazione di bozze.
- Riapertura editor: get(projectId, nodeId).
- Riapertura progetto: lastNode(projectId), controllando che il blocco esista.
- Eliminazione blocco: remove per ogni blocco effettivamente eliminato.
- Eliminazione progetto: removeProject(projectId).

Le chiavi di archivio appartengono allo spazio frociart.drafts.v2. e ogni
bozza occupa un record distinto. IDs e testi sono codificati con Base64 URL
safe di unità UTF-16, per preservare anche testo Unicode incompleto mentre
si digita. L'ordine relativo viene salvato e lastNode torna all'ultima
bozza ancora presente quando si elimina quella più recente.

La migrazione legge il vecchio slot draft_* solo se i dati sono validi.
Scrive e conferma prima il nuovo record, poi elimina le chiavi legacy.
Una bozza nuova già valida prevale sulla copia legacy. Un fallimento di
commit mantiene un percorso di recupero; un record illeggibile viene
ignorato senza cancellare altre bozze. Discard/Delete rimuovono anche una
copia legacy corrispondente, per non farla ricomparire al riavvio.
Le scritture ordinarie usano apply; i commit sincroni sono limitati alla
migrazione una tantum. Il contatore si ricostruisce da record presenti e
può riparare metadati fuori scala senza cancellare i contenuti.

ESEGUIRE
python3 tests/drafts/run_draft_tests.py --results /tmp/frociart-draft-tests
JDK 17+ richiesto per compilare; il codice prodotto è compatibile Java 8.
Lo stub in stubs/android/content non deve entrare nell'APK.

LIMITI DELLA PROVA
L'adattatore host simula SharedPreferences e distingue memoria/dati
persistiti, compresi commit falliti. apply è immediato nell'adattatore:
non misura pianificazione/disco Android o resa della UI. Le prove native
J23-J26 di tests/user-journeys/CHECKLIST.txt devono confermare l'integrazione
nel nuovo APK, inclusi due blocchi e due progetti attraverso l'interfaccia.
