package it.frociart.app;

import java.util.*;

/** An isolated trial. It never asks the host for file access or changes the editor runner. */
final class FlowDataTestSession {
    static final class Result {
        final boolean passed;
        final String title,message;
        final String[] actual;
        final int steps;
        Result(boolean passed,String title,String message,String[] actual,int steps){this.passed=passed;this.title=title;this.message=message;this.actual=actual;this.steps=steps;}
    }
    final FlowEngine.Runner runner;
    private final String[] inputs,expected;
    private int consumed;
    Result result;
    FlowDataTestSession(FlowModel.Document document,String inputText,String expectedText){
        runner=new FlowEngine.Runner(document);inputs=lines(inputText);expected=lines(expectedText);
    }
    static String[] lines(String text){
        if(text==null||text.isEmpty())return new String[0];
        String normalized=text.replace("\r\n","\n").replace('\r','\n');
        if(normalized.endsWith("\n"))normalized=normalized.substring(0,normalized.length()-1);
        String[] rows=normalized.split("\n",-1);for(int i=0;i<rows.length;i++)rows[i]=decodeLine(rows[i]);return rows;
    }
    // Escape only the reserved empty-row marker family; preserve paths and other text.
    private static String decodeLine(String value){if(value.equals("\"\""))return "";if(value.length()>2&&value.endsWith("\"\"")){boolean marker=true;for(int i=0;i<value.length()-2;i++)if(value.charAt(i)!='\\'){marker=false;break;}if(marker)return value.substring(1);}return value;}
    boolean tick(int budget){
        if(budget<1)throw new IllegalArgumentException("Serve un budget di esecuzione positivo.");
        if(result!=null)return true;
        for(int i=0;i<budget&&!Thread.currentThread().isInterrupted();i++){
            if(runner.state==FlowEngine.State.ERROR){finish(false,"Prova interrotta",runner.errorMessage);return true;}
            if(runner.state==FlowEngine.State.WAITING_FILE){finish(false,"Serve un file","Questa prova richiede di scegliere o salvare un file. Apri Esegui per provarla con i documenti. Nessun file è stato aperto o salvato dalla prova dati.");return true;}
            if(runner.state==FlowEngine.State.COMPLETED){
                if(consumed<inputs.length){finish(false,"Input in più","Input usati: "+consumed+". Input forniti: "+inputs.length+". Correggi i dati della prova.");return true;}
                String[] actual=actualLines();boolean match=Arrays.equals(actual,expected);
                String detail=(actual.length==0?"Il programma non ha prodotto output, come previsto.":actual.length==1?"La riga di output coincide con quella attesa.":"Tutte le "+actual.length+" righe di output coincidono.")+" Questo esito riguarda i dati di questa prova.";
                if(!match){int first=0;while(first<Math.min(actual.length,expected.length)&&actual[first].equals(expected[first]))first++;detail="La prima differenza è alla riga "+(first+1)+". Righe attese: "+expected.length+"; ottenute: "+actual.length+". Controlla anche spazi e maiuscole.";}
                finish(match,match?"Prova superata":"Risultato diverso",detail);return true;
            }
            if(runner.state==FlowEngine.State.WAITING){
                if(consumed>=inputs.length){finish(false,"Manca un input","Serve un altro valore per «"+runner.inputVariable+"». Inseriscilo alla riga "+(consumed+1)+".");return true;}
                if(!runner.submitInput(inputs[consumed++])){finish(false,"Input non accettato",runner.lastMessage);return true;}
            }else runner.step();
        }
        return false;
    }
    private String[] actualLines(){return runner.output.isEmpty()?new String[0]:runner.outputText().replace("\r\n","\n").replace('\r','\n').split("\n",-1);}
    private void finish(boolean passed,String title,String message){result=new Result(passed,title,message,actualLines(),runner.steps);}
}
