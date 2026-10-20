![Frociart: il personaggio inserisce un blocco nel diagramma](docs/assets/frociart-cover.png)

# Frociart

Crea, controlla e prova diagrammi di flusso. Un'app locale per Windows e Android, con lezioni guidate e file `.frociart`.

[Apri la pagina di Frociart](https://mamt104.github.io/Frociart/) · [Scarica per Windows](https://mamt104.github.io/Frociart/download/Frociart_Setup_1.11.15.exe) · [Scarica per Android](https://mamt104.github.io/Frociart/download/Frociart_1.7.11_Android.apk)

Questo repository distribuisce la pagina del progetto, la documentazione e i pacchetti scaricabili. La pagina è statica: non usa API, account dell'app o servizi di intelligenza artificiale.

## Windows

Scarica **Frociart_Setup_1.11.15.exe** dalla pagina del progetto per configurare l'icona dei file e l'apertura con doppio clic. Non servono privilegi amministratore. I progetti e i progressi esistenti sono conservati. È disponibile anche l'EXE portatile; da **Altro → Attiva icona e doppio clic dei progetti** puoi attivare la stessa integrazione.

Windows rispetta una precedente scelta esplicita dell'app predefinita: in quel caso usa **Apri con → Frociart**.

## Aprire un progetto ricevuto

Con l'app configurata, fai doppio clic sul file `.frociart`. Dal sito puoi anche scegliere un file e premere **Apri in Frociart**. Se l'app non è presente, la pagina offre il download. I progetti grandi si aprono tramite file locale; il browser non può rilevare in modo affidabile se l'app è installata.

Un file sconosciuto non può reindirizzare da solo Windows a un sito: senza un'app associata, Windows mostra il proprio selettore delle applicazioni.

## Privacy

Niente chiavi API, telemetria o invio automatico dei progetti. La pagina elabora i file nel browser. Il servizio di hosting GitHub applica la propria informativa agli accessi e ai download. Leggi la pagina Privacy per i dettagli e i limiti delle verifiche.

## Integrità

Confronta il digest SHA256 del pacchetto con `docs/download/SHA256SUMS_1.11.15.txt`. Mantieni aggiornato Windows o Android. I pacchetti Windows non hanno una firma Authenticode commerciale: Windows può mostrare un avviso per un editore non verificato.
