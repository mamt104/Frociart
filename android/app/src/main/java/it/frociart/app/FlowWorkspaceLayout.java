package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Layout changes presentation and discoverability, never the document capabilities. */
final class FlowWorkspaceLayout {
    static final String[] NAMES={"Essenziale","Sviluppo","Completo"};
    static final String[] DESCRIPTIONS={
        "Blocchi comuni in primo piano. Variabili, funzioni, file e disegno restano disponibili nei gruppi.",
        "Accesso rapido a costruzione, esecuzione e codice. I blocchi restano organizzati per gruppo.",
        "Tutti i comandi rapidi, inclusa Vista. Ogni famiglia di blocchi ha il proprio gruppo."};
    static int valid(int mode){return mode>=0&&mode<3?mode:0;}
    static int[] kinds(int mode,int group,boolean separate){
        if(group==1)return FlowFileBlocks.KINDS.clone();
        if(group==2)return FlowTurtle.KINDS.clone();
        if(group==3)return new int[]{DECLARE,CALL};
        if(group==4)return new int[]{PAUSE};
        if(group==5)return new int[]{NOTE};
        ArrayList<Integer> kinds=new ArrayList<>();
        for(int kind:new int[]{START,INPUT,OUTPUT,PROCESS,DECISION,END}){
            if(kind==OUTPUT&&!separate)continue;kinds.add(kind);
        }
        int[] result=new int[kinds.size()];for(int i=0;i<result.length;i++)result[i]=kinds.get(i);return result;
    }
    static String icon(int kind){
        if(FlowFileBlocks.isFile(kind))return "folder";
        switch(kind){case NOTE:return "edit";case PAUSE:return "pause";case START:return "start";case INPUT:return separateIo?"input":"io";case OUTPUT:return "input";case DECISION:return "decision";case CALL:return "call";case END:return "end";case TURTLE_FORWARD:return "right";case TURTLE_TURN:return "redo";case TURTLE_HOME:return "home";case TURTLE_CLEAR:return "trash";case TURTLE_PEN:return "edit";default:return "process";}
    }
    static String description(int kind){
        if(FlowFileBlocks.isFile(kind))return FlowFileBlocks.description(kind);if(FlowTurtle.isTurtle(kind))return FlowTurtle.description(kind);
        switch(kind){case NOTE:return "Spiega senza collegamenti";case PAUSE:return "Ferma qui la simulazione";case START:return "Da dove parti";case INPUT:return separateIo?"Leggi un dato":"Leggi o stampa";case OUTPUT:return "Stampa un risultato";case PROCESS:return "Fai un calcolo";case DECLARE:return "Scegli nomi e tipi";case DECISION:return "Scegli una strada";case CALL:return "Richiama una funzione";default:return "Chiudi il percorso";}
    }
}
