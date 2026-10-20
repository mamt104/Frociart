# Regole di sicurezza e pubblicazione di Frociart

- Questo repository distribuisce una pagina statica e pacchetti di un'app offline. Non aggiungere account, backend, API esterne, analytics, font CDN o invio di documenti senza una richiesta esplicita dell'utente.
- I documenti scelti dal browser restano sul dispositivo. Mantieni CSP restrittiva e `connect-src 'none'`. Usa `textContent` per i dati del documento; mai `innerHTML`, `eval` o URL di download derivati dal contenuto del file.
- Valida modello, capacità e dimensione prima di generare un link all'app. Il protocollo apre soltanto dati di progetto, non comandi, percorsi o download remoti. Il progetto importato non si esegue da solo.
- Non includere password, token, credenziali, chiavi di firma, file `.env`, log del PC, cartelle di lavoro, cronologia Git privata o percorsi personali nei pacchetti pubblici.
- Crea le distribuzioni da elenchi espliciti di file. Controlla contenuto e SHA256 degli archivi finali; non pubblicare direttamente un archivio di collaudo precedente.
- Non modificare UserChoice, associazioni di altre applicazioni o progetti dell'utente. I file dell'app si installano per il solo utente; un errore non deve cancellare la versione precedente.
- Le verifiche automatiche non sono una certificazione universale. Riporta separatamente PASS, FAIL, NOT_RUN e i limiti di browser, Windows, Android e pubblicazione. Non eliminare asserzioni per presentare una verifica come riuscita.
