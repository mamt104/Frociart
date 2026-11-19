# Installer leggero di Frociart v1

L'installer v1 (build interna del setup 1.81) contiene l'applicazione Windows 1.81, con correzione dei rami decisionali, navigazione del foglio da tastiera, blocchi e frecce accessibili e rinomina delle variabili. L'app moderna usa il runtime condiviso: il pacchetto non contiene .NET moderno. Il bootstrapper usa .NET Framework, già integrato nei PC Windows 10/11 cui è destinato questo pacchetto x64.

## Comportamento

1. Controlla piattaforma e integrità dell'app inclusa.
2. Cerca il runtime x64 registrato e avvia una verifica nascosta di .NET, Windows Forms e WPF, con gli stessi requisiti minimi e `LatestPatch` dell'app.
3. Se il componente è già compatibile, non usa la rete e non lo reinstalla.
4. Se manca, scarica l'installer ufficiale Microsoft, con HTTPS, origine fissa, timeout, limite di 128 MiB, controllo del completamento e progresso sui byte ricevuti.
5. Controlla SHA-512 dalla release Microsoft e firma tramite Windows, revoca e organizzazione del firmatario. Il certificato corrente ha CN `.NET` e organizzazione `Microsoft Corporation`. Il file resta protetto da scrittura/sostituzione tra verifica ed esecuzione.
6. Avvia l'installer Microsoft con `/install /passive /norestart`. Windows può chiedere autorizzazione. Non cambia antivirus, SmartScreen, ExecutionPolicy o politiche di sicurezza.
7. Gestisce annullamento, altra installazione in corso, errore e richiesta di riavvio. Ricontrolla il runtime prima di aggiornare Frociart.
8. Avvia il payload Inno Setup per il solo utente corrente. Installa l'app nella cartella già usata da Frociart, aggiunge il collegamento al menu Start e la voce Frociart v1 nelle App installate di Windows. Registra icona/apertura `.frociart`, rispettando applicazioni predefinite diverse e senza toccare UserChoice.
9. Verifica i file effettivi, il target del collegamento tramite la shell Windows, la voce App installate e il comando di disinstallazione prima di confermare il successo. Un semplice codice di uscita zero non basta.

## Aggiornamento e disinstallazione

Un aggiornamento riusa la stessa voce e lo stesso collegamento. Un installer più vecchio non sostituisce un'app più recente. Chiudere Frociart prima di aggiornarlo o rimuoverlo: i file occupati bloccano l'operazione, senza chiusure forzate o cancellazioni rinviate al riavvio.

Per rimuovere Frociart usare Impostazioni Windows > App > App installate > Frociart v1 > Disinstalla. La conferma spiega che progetti, impostazioni e componente Microsoft .NET restano conservati. Il programma di disinstallazione non richiede il runtime .NET moderno. Rimuove i file elencati dal pacchetto; non cancella ricorsivamente la cartella dell'app, non visita cartelle di progetti/recenti/preferenze e conserva anche un file non gestito aggiunto dall'utente nella cartella del programma.

Le associazioni e il collegamento vengono rimossi solo se appartengono ancora a questa installazione. Modifiche successive per aprire con un'altra applicazione restano conservate. Se il collegamento Start è già assegnato altrove, il setup spiega il conflitto e non lo sostituisce.

L'annullamento è disponibile durante preparazione, download e verifica. Quando Microsoft ha iniziato a modificare componenti di sistema, non si forza l'arresto: resta la gestione del suo installer. Nessun riavvio forzato. Un runtime condiviso eventualmente installato non viene disinstallato annullando o rimuovendo Frociart, perché può servire ad altre app.

La barra blu rappresenta byte reali; a lunghezza sconosciuta mostra attività, senza percentuali inventate. Le animazioni si arrestano quando nascoste e rispettano la preferenza di animazione del sistema.

## Build

Usare un SDK .NET 10 e il compilatore Inno Setup 7.1.0 x64 verificato. `InstallerCompiler.json` contiene origine e hash del pacchetto e del compilatore. Il tool può essere preparato in una cartella di lavoro con `/PORTABLE=1 /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /TASKS= /DIR="cartella"`, dopo aver verificato SHA256 e firma Authenticode del download ufficiale. Non occorre installare il compilatore globalmente.

```powershell
.\build-light-setup.ps1 -DotNetPath C:\strumenti\dotnet.exe -InnoCompilerPath C:\strumenti\inno\ISCC.exe
```

Parametri facoltativi: `OutputDirectory`, `NuGetConfig`, `NuGetPackages`. La build produce app framework-dependent, piccola verifica del runtime, payload per utente, bootstrapper e manifesto di integrità. L'app è presente una sola volta nel pacchetto compresso; i suoi hash e quelli dell'icona vengono ricontrollati prima di modificare i file. La build non installa il runtime, non esegue il setup e non cambia impostazioni del PC. I simboli incorporati del bootstrapper e dell'app usano percorsi mappati, senza cartelle personali.

Distribuire il bootstrapper, non il solo `Frociart.ApplicationPayload.exe`: il payload interno non gestisce l'installazione del componente Microsoft.

`RuntimePackage.json` fissa versione, origine e SHA-512. Per aggiornare la dipendenza, confrontare il manifesto con la metadata Microsoft, ricostruire e ripetere i controlli; non sostituire l'hash con un valore accettato indiscriminatamente. Il sistema può usare patch compatibili successive senza reinstallare quella fissata. Le politiche di aggiornamento del runtime Microsoft restano distinte da eventuali aggiornamenti di Frociart.

## Stato del collaudo

Implementazione locale in collaudo, non sostituisce ancora il download pubblico. Il payload è stato installato, aggiornato e rimosso realmente come prodotto temporaneo distinto, con file/collegamento nel workspace e associazioni in una sottochiave di collaudo. Verificati avvio della vera app dal collegamento, voce App installate, blocchi dei file occupati, ripristino e conservazione di canarie dati. Il bootstrapper è controllato anche con host di prova e runtime isolato reale. Il download e la firma Microsoft erano già verificati nell'incremento precedente; origine, hash e implementazione del componente sono invariati.

Non ancora verificati l'avvio dell'EXE finale del bootstrapper e il pulsante No nella conferma di disinstallazione. Non eseguita l'installazione Microsoft/UAC/riavvio su una macchina Windows pulita. Restano da verificare il ciclo completo di produzione, altri Windows e tecnologie assistive. Il setup e il payload Frociart non sono firmati Authenticode; la firma del compilatore upstream non firma il programma prodotto e l'avviso SmartScreen resta possibile.

L'applicazione completa offline resta disponibile. L'APK Android viene compilato e collaudato separatamente; non usa questo installer.

## Riferimenti tecnici

- [Installazione Microsoft .NET su Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [Versioni e selezione del runtime](https://learn.microsoft.com/en-us/dotnet/core/versions/selection)
- [Distribuzione in un singolo file](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Metadati ufficiali .NET 10](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
- [Sistemi supportati dal runtime .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
- [Inno Setup: progetto originale](https://github.com/jrsoftware/issrc)
- [Verifica dei download Inno Setup](https://jrsoftware.org/isdl-verify.php)
- [Installazione senza privilegi amministrativi](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)
- [Collegamenti e proprietà](https://jrsoftware.org/ishelp/topic_iconssection.htm)

Il payload e il programma di disinstallazione usano Inno Setup, copyright Jordan Russell e Martijn Laan. La licenza originale è inclusa in `Licenze/Inno-Setup-LICENSE.txt` ed è installata insieme all'app. Non sono stati modificati i binari del compilatore o cancellate le sue attribuzioni.

Il supporto dichiarato da Microsoft non equivale a un collaudo di Frociart su tutti quei sistemi.
