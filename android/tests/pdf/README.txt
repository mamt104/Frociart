FROCIART — REGRESSIONE PDF A4 1.2.1
18 ottobre 2026.

DIFETTO RIPRODOTTO
Il PDF multipagina originale divideva il disegno in fasce fisse di
contentH/scale. Nel file Pari/Dispari realmente salvato attraverso il
selettore Android, il primo confine attraversava i nodi Output e tagliava
anche le parole fra due pagine. Il PDF su pagina unica era corretto.
L'originale si trova in tests/results/update-1.2.1/native/export-review/.

CORREZIONE MIRATA
FlowPdfPages è un planner Java puro senza dipendenze Android.
Considera ciascun nodo come intervallo verticale con otto unità del
modello di margine sopra e sotto. Dove possibile la pagina termina nello
spazio prima del gruppo di nodi che attraverserebbe il vecchio confine.
FlowExporter usa la cima e il fondo effettivi della pagina anche nel clip:
il pezzo iniziale del nodo non rimane visibile nella pagina precedente.

Scala, formato/orientamento A4, margini orizzontali, intestazione nel
piè di pagina e limite di 24 pagine restano quelli originali. La pagina
unica non passa attraverso il planner A4. Nessun nodo viene mosso; il
renderer rimane lo snapshot separato già usato dalle altre esportazioni.

FASCE DENSE SENZA SPAZIO PER UN TAGLIO
Quando una catena di nodi verticalmente sovrapposti supera l'altezza utile,
la pagina successiva riparte dalla cima del primo nodo che attraversa il
fondo precedente. Questa sovrapposizione garantisce almeno una copia
completa di ogni nodo. Alcuni frammenti possono comparire anche nella
pagina adiacente, ma esiste sempre la copia intera. Ogni pagina avanza;
non si creano sezioni di altezza zero. Le strisce continue del diagramma
e le sue frecce non vengono omesse per eliminare arbitrariamente spazi.
Un nodo più alto della pagina o un risultato oltre 24 pagine produce un
errore chiaro e suggerisce il PDF su una pagina unica. Non cambia scala
silenziosamente e non entra in loop.

ESEGUI I CONTROLLI
Da FrociartAndroid:
python3 tests/pdf/run_pdf_tests.py

Richiede JDK 17 con modulo jdk.compiler e Python 3. Non richiede rete,
Android SDK o emulatore. I sorgenti sono compilati con --release 8.
Il runner scrive planner-host.log e planner-provenance.json in results/.

COPERTURA
- Fixture effettiva del file .frociart esportato nella UI Android.
- Output che il vecchio taglio attraversava, nodi sui bordi esatti,
  coordinate negative e invarianza alla traslazione.
- Gruppo denso che richiede pagine sovrapposte.
- Catena di 150 nodi; 24 pagine accettate e 25 rifiutate; blocco troppo alto.
- Ingressi dimensionali invalidi.
- 24 insiemi deterministici di intervalli con sei tipi di nodo.
I controlli verificano invarianti indipendenti: almeno una copia intera
di ogni nodo con margine, altezza utile rispettata, avanzamento positivo,
nessuna striscia omessa, nessuna pagina di altezza zero, limiti rispettati
e documento codificato identico prima/dopo. Non replicano l'algoritmo per
costruire un elenco atteso dei tagli.

RISULTATI
6.018 asserzioni su 35 casi PASS nel planner host.
Compilazione Android API 35 completa e zipalign PASS, APK non firmato
prodotto separatamente per validare Java/DEX/risorse. La compilazione
non dimostra il rendering PDF. Il gate I/O Android finale ha superato
44 controlli: log in tests/results/update-1.2.1/native/io-final.log.
I tre PDF reali corretti sono stati renderizzati con Poppler (exit 0)
e tutte le sette pagine ispezionate: Pari/Dispari A4 su due pagine,
campione alto su quattro, pagina singola su una. Nodi e testo interi
nei campioni. Evidenze in native/export-review-final/, compresi i PNG
e LEGGIMI.txt. Il fallback denso resta verificato dalle invarianti host.
Non dichiarare l'intera UI files PASS: DocumentsUI aveva timeout nel
percorso precedente e alcuni formati non erano stati raggiunti.
