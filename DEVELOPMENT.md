# Sviluppo e compilazioni di Frociart

I sorgenti Android build 105 e Windows 1.83 sono stati recuperati dagli archivi della
[release del 30 ottobre 2026](https://github.com/mamt104/Frociart/releases/tag/v1-android105).
La migrazione parte dal commit `25554df730b6ba510fce6e7acc651f9a5444a6af`.
Le release, i tag e i pacchetti sotto `docs/download/` non vengono modificati.

## Struttura

| Percorso | Contenuto |
| --- | --- |
| `android/app/src/main/` | Manifest, Java, risorse e runtime esportabili dell'app Android. |
| `android/build_apk.py` | Builder originale con gli strumenti ufficiali Android SDK. Non usa Gradle. |
| `android/tests/` | Test originali, fixture e documenti storici di verifica. |
| `pc/` | Sorgenti desktop, risorse, test e progetti .NET 10 / Framework 4.8. Il nome originale è conservato per ridurre i cambiamenti ai percorsi. |
| `tools/ci/` | Runner dal checkout, installazione SDK verificata e provenienza dell'importazione. |
| `.github/workflows/quality.yml` | Sei suite esistenti sul sito/pacchetti, più controllo dei file privati. |
| `.github/workflows/native.yml` | Build Android e Windows e test nativi disponibili senza corpora esterni. |

Le nuove modifiche vanno fatte nei sorgenti versionati. Non usare i vecchi ZIP come
input delle build CI. I file `SOURCES_SHA256.json` importati restano fotografie della
release originale: non sono manifest aggiornati della prossima versione.
`.gitattributes` conserva i byte dei file importati, comprese le terminazioni di riga
miste; una pulizia della formattazione sarebbe una modifica separata.

## Provenienza e verifica della migrazione

| Archivio originale | File | SHA256 |
| --- | ---: | --- |
| `Frociart_Android_105_Sorgenti.zip` | 255 | `065691c66678c51a19b804eb8f66aa5ff1acf29c7b932fd9f906a320a097c2da` |
| `Frociart_Windows_1.83_Sorgenti.zip` | 339 | `bf4c05e40ab308cf355752ca9ea5431b1dd0545baab1f21b62a0f12b4a94ae53` |

Scarica i due asset originali e verifica tutti i 594 file senza estrarli:

```sh
python tools/ci/verify_source_import.py Frociart_Android_105_Sorgenti.zip Frociart_Windows_1.83_Sorgenti.zip
```

Questa verifica riguarda la patch di migrazione. Le normali modifiche future ai
sorgenti faranno intenzionalmente divergere il checkout dalla fotografia iniziale.
Non è un blocco permanente della CI. I dettagli sono in `tools/ci/source-import.json`.

Sono documentate due sole differenze rispetto ai file degli archivi:

- `EngineTests.java`: il formato 3 è supportato oggi. La prova di rifiuto di una
  versione futura usa 99, conservando il controllo di rifiuto.
- `FlowBackup.java`: lo stream destinazione ha un proprietario separato nel
  `try-with-resources`. Se la chiusura ZIP fallisce durante una scrittura interrotta,
  la destinazione viene chiusa comunque. La prova originale `FlowBackupTests`
  riproduceva il problema ed è rimasta invariata.

## Android

La CI usa Temurin **17.0.21+9**, Python **3.12**, platform Android **35 r02** e
build-tools **35.0.0**. Gli archivi SDK ufficiali hanno URL e SHA256 fissati nello
script d'installazione. Il JDK è completo e include `javac` e i dati di `--release`.

Su Linux, da una cartella SDK nuova:

```sh
bash tools/ci/install_android_sdk.sh "$PWD/artifacts/sdk"
python tools/ci/test_android.py
python android/build_apk.py android \
  --sdk "$PWD/artifacts/sdk" --java-home "$JAVA_HOME" \
  --output artifacts/android/Frociart-ci-unsigned.apk \
  --build-dir artifacts/android-build \
  --compile-sdk 35 --build-tools 35.0.0 --min-sdk 26 --target-sdk 35
```

Su altri sistemi è possibile fornire al builder un SDK con le stesse versioni
già installate. Il builder compila il codice app con livello Java 8 e bootclasspath
Android, produce DEX, risorse e APK e controlla l'allineamento. Le prove host usano
Java 17: alcuni test originali usano `Files.readString` e `Files.writeString`.

Le 17 suite attive coprono motore e codec, capacità, posizionamento e inserimento,
limiti di apertura, griglia, viewport, layout, paginazione PDF, apprendimento e
pratica, bozze, backup, modifica delle funzioni, capacità del linguaggio, cicli e
recupero dei file. I log e `checks.json` sono in `artifacts/android-tests/`.
Le prove scrivono soltanto in `artifacts/`: il materiale didattico necessario viene
copiato prima dell'esecuzione. I vecchi runner restano come materiale storico.

Non vengono dichiarati eseguiti i test UI/storage Android, i probe distruttivi su
dispositivo o le suite di interoperabilità che richiedono corpora esterni assenti
dai due ZIP. Per esempio `CodeTemplateStoreTests` richiede `draft-example.txt`
prodotto dal desktop e `LoopBuilderTests` richiede `pc-loop-*.frociart`.
Non sostituire queste fixture con copie generate dall'implementazione sotto test.
L'esecuzione JVM con l'adapter SharedPreferences verifica la logica delle bozze,
non la persistenza reale di Android. Il collaudo su emulatore/dispositivo rimane
un passaggio distinto prima della distribuzione.

## Windows

`global.json` fissa l'SDK **10.0.403**, senza roll-forward. Il progetto originale
fissa il runtime **10.0.14**, `win-x64`, single-file e self-contained.

```sh
dotnet publish pc/Frociart.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows-x64
```

Questa compilazione può essere eseguita anche su Linux con Windows targeting,
ma l'EXE risultante deve essere avviato e collaudato su Windows.

Sul runner Windows, con .NET Framework 4.8 e WPF installati:

```bat
call pc\build-framework.cmd
python tools\ci\test_windows.py
```

La CI ricompila sia il pacchetto autonomo sia quello classico. Il nuovo runner
legge file e risorse dal progetto Framework, compila le suite originali
`EngineTests`, `CapabilityTests` e `CloneTests` insieme ai sorgenti di produzione
e ne propaga il risultato. Non usa `dotnet test`: queste suite hanno propri
`Main`, non sono progetti xUnit/NUnit. La destinazione dei dati e le directory
temporanee sono isolate sotto `artifacts/windows-tests/`.
Il runner misura la logica nativa su Framework, non l'interfaccia né la parità
universale tra Framework e .NET 10. Il setup Inno e i probe interattivi restano
fuori da questa CI; non sono necessari per compilare e verificare gli EXE.

## CI e risultati

Entrambi i workflow girano sulle pull request, su `main` e manualmente.
`native.yml` gira anche sul ramo di preparazione della patch. Le Actions sono
fissate a commit verificati dei rispettivi repository. I job hanno timeout,
permesso `contents: read`, checkout senza credenziali persistenti e nessun accesso
a segreti. Le build non effettuano upload nelle release o nel sito.

GitHub conserva per 14 giorni gli APK **senza firma**, gli EXE non firmati, gli
hash, il commit, le versioni degli strumenti e i log delle prove. Gli upload di
evidenze usano `if: always()`; un test fallito fa fallire il job.
La revisione Python 3.12 e l'immagine del runner possono ricevere aggiornamenti:
le versioni effettive vengono registrate. L'APK contiene timestamp ZIP e le
firme ufficiali non sono riprodotte: non si promettono file identici bit per bit
ai pacchetti pubblicati. Il primo run GitHub della patch resta necessario per
confermare l'esecuzione su runner Windows.

## Chiavi, credenziali e dati locali

`.gitignore` esclude keystore, certificati privati, password, `.env`, configurazioni
IDE, SDK locali, output e file utente. `check_source_hygiene.py` controlla i file
versionati per nomi privati, formati riconoscibili di chiavi/token e percorsi di
profili personali; non stampa il valore riconosciuto. È una barriera aggiuntiva,
non un rilevatore completo di qualsiasi segreto.

La CI non firma l'APK e non richiede la chiave ufficiale. Per una distribuzione
firmata, mantenere `frociart.jks` e `password.txt` in una directory esterna al
checkout e passarla esplicitamente al builder con `--signing-dir`. Non includerla
negli artefatti CI. Una firma di prova diversa non aggiorna l'APK ufficiale:
non usarla per sostituire un'installazione con progetti personali.
Prima di una prossima release occorre assegnare nuove versioni, eseguire il
collaudo nativo e attivare una procedura di firma e pubblicazione separata.
