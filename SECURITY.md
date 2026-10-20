# Sicurezza e privacy

Revisione del 21 ottobre 2026: Windows 1.11.15, configurazione Windows 1.11.15, Android 1.7.11 e pagina statica di distribuzione.

L'app non richiede autenticazione, backend, database, chiavi API o servizi AI. Non include telemetria. I collegamenti didattici aprono siti esterni soltanto su richiesta. Il sito legge i progetti sul dispositivo e non li carica su un server.

Il contenuto importato è un modello di dati validato: massimo 4 MB, 500 blocchi e 1.500 collegamenti. L'apertura non avvia il programma. Il protocollo Windows accetta soltanto un progetto codificato e limitato; non apre URL remoti o comandi. Per i progetti grandi resta disponibile l'apertura da file.

Le associazioni sono per l'utente corrente e non modificano UserChoice. Windows conserva l'eventuale app predefinita già scelta. Il pacchetto di configurazione gestisce anche la rimozione dei propri file, conservando documenti e progressi.

Sono stati controllati i sorgenti di produzione, la gestione degli import, l'accesso ai file, le esportazioni, la diagnostica, le associazioni e la distribuzione. I pacchetti pubblici sono creati da un elenco esplicito, con hash SHA256, escludendo credenziali, chiavi di firma, log e dati utente. Le verifiche riducono il rischio e non garantiscono l'assenza di vulnerabilità.

Gli EXE Windows non hanno una firma Authenticode commerciale. L'APK è firmato; la chiave privata non è distribuita. Non sono stati introdotti aggiornamenti o download automatici.

GitHub ospita pagina e download e applica la propria informativa. L'identità pubblica dell'account che ospita il repository resta visibile. Una pagina statica non può garantire il rilevamento dell'app installata: offre il comando di apertura e il download.

Non inserire progetti privati, credenziali o token nelle issue pubbliche. Per segnalare un problema descrivi versione, comportamento e passaggi con un esempio privo di dati personali.

Prima di un rilascio sul sito attivo verificare HTTPS, i download e i relativi hash; il collaudo del sito pubblicato resta distinto dai test locali. La scansione locale non è un penetration test indipendente.
