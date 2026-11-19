package it.frociart.app;
import static it.frociart.app.FlowModel.*;
final class FlowFileBlocks {
 static final int[] KINDS={OPEN_FILE,READ_FILE,WRITE_FILE,CLOSE_FILE};
 static boolean isFile(int kind){return kind>=OPEN_FILE&&kind<=CLOSE_FILE;}
 static String description(int kind){switch(kind){case OPEN_FILE:return "Prepara lettura o scrittura";case READ_FILE:return "Una riga in una variabile";case WRITE_FILE:return "Salva un valore nel file";case CLOSE_FILE:return "Concludi lettura o scrittura";default:return "";}}
 static String fieldLabel(int kind){return kind==OPEN_FILE?"Nome del file o espressione di testo":kind==READ_FILE?"Variabile o elemento del vettore":"Valore o testo da scrivere";}
 static String help(int kind){switch(kind){case OPEN_FILE:return "Il nome è un testo fra virgolette o un'espressione. In esecuzione scegli il documento da leggere o dove salvare il risultato.";case READ_FILE:return "Legge una riga nel tipo della variabile. Dichiara TESTO per conservare parole e spazi. Controlla non EOF() prima di leggere.";case WRITE_FILE:return "Il valore va nel file aperto in scrittura. Con A capo ogni passaggio aggiunge una nuova riga. Chiudi file chiede dove salvarlo.";case CLOSE_FILE:return "Scegli il canale da chiudere. Chiudere la scrittura chiede dove salvare; annullare conserva il testo in File generati.";default:return "";}}
 static String defaultText(int kind){return kind==OPEN_FILE?"LETTURA \"dati.txt\"":kind==READ_FILE||kind==WRITE_FILE?"riga":"LETTURA";}
}
