# Regressioni native di visualizzazione e accessibilità Windows

Richiede Windows, Python3, .NET Framework4.8 e il compilatore csc della cartella Framework64. Nessuna dipendenzaPython esterna. I file sono test separati, esclusi dagli elenchi Compile dei progetti dell’app.

Dal root dei sorgenti:

```powershell
python pc/QualityTests/DisplayScaling/run.py --output C:/percorso/di/prova/flowchart-display
```

Prima costruire la versione classica con `pc/build-framework.cmd`. `--app` permette di scegliere un altro EXE Framework; l’assembly moderno.NET10 non si carica nel processo Framework usato per questi provider. `--pc-root` e `--framework-dir` rendono configurabili sorgenti e compilatore. L’output è obbligatorio: EXE, cache, dati privati, log ePNG vengono generati soltanto lì. Usare una cartella dedicata di lavoro.

Modalità: `--mode dpi`, `compact`, `uia`, `msaa`, oppure `all` (predefinita). `--scales 100 125 150 200` controlla la matrice standard. L’esecuzione completa corrente verifica328asserzioni geometriche/raster,40nel caso compatto,92UIA e13MSAA. Un’asserzione fallita produce exit1 e un result.json; non basta l’exit del compilatore per dichiarare un test passato.

## Isolamento e significato

DesktopLauncher crea un desktop nativoWindows privato prima dell’avvioCLR. Non chiamaSwitchDesktop, non cambia DPI, schermo, lettore o impostazioni dell’utente. Richiede il permesso di creare il desktop nella sessioneWindows corrente. La chiusura libera gli handle; non esegue operazioni sui progetti dell’utente.

La matriceDPI compila copie di prova di tre file. Inietta il fattore richiesto nei punti di ingresso del layoutMainForm eNativeTheme e adegua i font a punti per riprodurre le metriche fisiche suHDC96. I controlli sonoWinForms reali, non immagini ingrandite. Il caso compatto inietta1920×1040 comeWorkingArea nella stessa funzione di clamp dell’app, con inputDPI200%. Verifica comandi primari, conferma, campo/caret e conservazione di bozza, selezione, Undo nativo e cronologia attraverso resize.

Non sono prove su monitor impostati a120/144/192DPI. Owner-draw che leggono direttamenteGraphics.DpiX conservano il DPI reale nel banco; le loro immagini non vanno usate per certificare altri monitor. Controllare semprePNG e JSON, incluse le condizioni riportate. Le copie di test e gli hook sono salvati nell’output per la riproducibilità.

UIA usa i provider pubblici degliHWND della build scelta, leggendo nome, ruolo, stato eValuePattern. La navigazione focus usaSelectNextControl. MSAA usaoleacc/IAccessible per enumerare e attivare blocchi e collegamenti reali. Non certificano l’esperienza audio completa conNarrator/NVDA o un test con persone: tale prova resta distinta.

## Difetti protetti

- Palette ricreata ad altoDPI e glifi completi confrontati con un riferimento non ritagliato.
- Altezza della barra dopoHome→editor e colonne che devono seguire la riduzione della finestra.
- Logo decorativo escluso dal cicloTab.
- Clamp200% su schermo compatto: F8/zoom/Inquadra senza sovrapposizioni; Griglia/Riordina restano nel menuVista e nella ricerca.
- Focus dell’espressione in un viewport corto e conservazione del lavoro durante resize.

Il rilevatore raster usa una soglia di luminosità tra testo principale e secondario, limitata alla fascia titolo: i pixelClearType non hanno sempre ilRGB esatto del font. Non ridurre le verifiche quando un risultato fallisce: distinguere un difetto dell’app da una modifica necessaria del banco dopo un refactor.
