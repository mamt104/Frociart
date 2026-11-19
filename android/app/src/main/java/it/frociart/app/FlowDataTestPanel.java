package it.frociart.app;

import android.app.*;
import android.content.*;
import android.os.*;
import android.text.*;
import android.view.*;
import android.widget.*;
import java.util.concurrent.*;

/** Native data-trial UI. Only immutable snapshots cross the worker/main-thread boundary. */
final class FlowDataTestPanel implements AutoCloseable {
    static final int MAX_TEXT=262144;
    final Ui.Sheet sheet;
    final EditText inputs,expected;
    final TextView actual,title,detail,progress;
    final Ui.Button start,stop;
    private final FlowModel.Document document;
    private final SharedPreferences prefs;
    private final String inputKey,expectedKey;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final ExecutorService worker=Executors.newSingleThreadExecutor();
    private Future<?> pending;
    private int generation;
    private boolean closed,running,interrupted;
    private String savedInputs,savedExpected;
    volatile FlowDataTestSession session;
    private final Runnable saveDraft=this::flush;

    FlowDataTestPanel(Activity owner,Ui.Sheet sheet,FlowModel.Document source,SharedPreferences prefs,String projectId){
        this.sheet=sheet;this.document=source.copy();this.prefs=prefs;inputKey="data_test_input_"+projectId;expectedKey="data_test_expected_"+projectId;
        savedInputs=bounded(prefs.getString(inputKey,""));savedExpected=bounded(prefs.getString(expectedKey,""));sheet.compactHeader(true);
        sheet.paragraph("Scrivi gli input in ordine e il risultato che ti aspetti. Una riga per valore; per una riga vuota scrivi \"\". Il confronto considera anche spazi e maiuscole.");
        sheet.section("1 · INPUT, IN ORDINE");inputs=field(owner,savedInputs,"Valori da fornire al programma","data-test-input");
        sheet.section("2 · OUTPUT ATTESO");expected=field(owner,savedExpected,"Il risultato che ti aspetti","data-test-expected");
        title=Ui.text(owner,"Prepara la tua prova",18,true);title.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);Ui.pad(title,0,16,0,8);sheet.body.addView(title);
        detail=Ui.note(owner,"La prova usa una copia del progetto. La simulazione sul foglio conserva i suoi valori.");sheet.body.addView(detail);
        sheet.section("3 · OUTPUT OTTENUTO");actual=Ui.text(owner,"Avvia la prova per vedere il risultato.",15,false);actual.setTypeface(android.graphics.Typeface.MONOSPACE);actual.setTextIsSelectable(true);Ui.pad(actual,12,12,12,12);Ui.card(actual,Ui.bg(),14);sheet.body.addView(actual,new android.widget.LinearLayout.LayoutParams(-1,-2));
        Ui.gap(sheet.footer,8);progress=Ui.note(owner,"Valori conservati in questo progetto");progress.setMaxLines(2);sheet.footer.addView(progress);
        android.widget.LinearLayout actions=Ui.row(owner);start=new Ui.Button(owner,"Avvia prova","play",1);stop=new Ui.Button(owner,"Ferma",null,0);
        for(Ui.Button button:new Ui.Button[]{start,stop}){button.setSingleLine(false);button.setMaxLines(3);button.setEllipsize(null);button.setMinimumHeight(Ui.dp(owner,54));}
        stop.setContentDescription("Interrompi la prova senza assegnare un esito");actions.addView(start,Ui.weight(owner,-2));actions.addView(new View(owner),Ui.lp(owner,8,1));actions.addView(stop,Ui.weight(owner,-2));sheet.footer.addView(actions,new android.widget.LinearLayout.LayoutParams(-1,-2));
        start.setOnClickListener(v->start());stop.setOnClickListener(v->stop());stop.setEnabled(false);
        TextWatcher watcher=new TextWatcher(){public void beforeTextChanged(CharSequence s,int st,int count,int after){}public void onTextChanged(CharSequence s,int st,int before,int count){handler.removeCallbacks(saveDraft);handler.postDelayed(saveDraft,300);if(!running){session=null;title.setText("Prepara la tua prova");title.setTextColor(Ui.ink());detail.setText("I dati sono cambiati: avvia di nuovo la prova.");actual.setText("Il risultato aggiornato apparirà qui.");}}public void afterTextChanged(Editable e){}};
        inputs.addTextChangedListener(watcher);expected.addTextChangedListener(watcher);
    }
    private static String bounded(String value){return value==null?"":value.length()>MAX_TEXT?value.substring(0,MAX_TEXT):value;}
    private EditText field(Activity owner,String value,String description,String tag){EditText input=Ui.edit(owner,value,description,true);input.setContentDescription(description);input.setTag(tag);input.setFilters(new InputFilter[]{new InputFilter.LengthFilter(MAX_TEXT)});input.setMinLines(3);input.setMaxLines(7);input.setImeOptions(android.view.inputmethod.EditorInfo.IME_FLAG_NO_EXTRACT_UI);sheet.body.addView(input,new android.widget.LinearLayout.LayoutParams(-1,-2));return input;}
    void flush(){if(closed)return;handler.removeCallbacks(saveDraft);String in=inputs.getText().toString(),out=expected.getText().toString();if(!in.equals(savedInputs)||!out.equals(savedExpected)){prefs.edit().putString(inputKey,in).putString(expectedKey,out).apply();savedInputs=in;savedExpected=out;}}
    void start(){
        if(closed||running)return;flush();Ui.dismissKeyboard(inputs);Ui.dismissKeyboard(expected);final String in=inputs.getText().toString(),out=expected.getText().toString();final int request=++generation;
        running=true;interrupted=false;session=null;setRunning(true);title.setText("Prova in corso…");title.setTextColor(Ui.accentInk());detail.setText("Controllo il risultato sui dati che hai scritto.");actual.setText("");progress.setText("Preparazione della prova…");
        pending=worker.submit(()->{try{
            FlowDataTestSession trial=new FlowDataTestSession(document,in,out);session=trial;long next=0;
            while(!Thread.currentThread().isInterrupted()){
                boolean done=trial.tick(64);if(done){final FlowDataTestSession.Result result=trial.result;handler.post(()->{if(!closed&&running&&generation==request)finish(result);});return;}
                long now=System.nanoTime();if(now>=next){next=now+100000000L;final int steps=trial.runner.steps;handler.post(()->{if(!closed&&running&&generation==request)progress.setText(steps+(steps==1?" blocco eseguito":" blocchi eseguiti")+"…");});}
            }
        }catch(RuntimeException failure){handler.post(()->{if(!closed&&running&&generation==request){running=false;setRunning(false);title.setText("Prova non completata");title.setTextColor(Ui.danger());detail.setText(failure.getMessage()==null?"Controlla il diagramma e riprova.":failure.getMessage());progress.setText("Nessun esito assegnato");}});}});
    }
    private void finish(FlowDataTestSession.Result result){
        running=false;setRunning(false);title.setText(result.title);title.setTextColor(result.passed?Ui.success():Ui.danger());detail.setText(result.message);StringBuilder shown=new StringBuilder();for(String row:result.actual){if(shown.length()>0)shown.append('\n');shown.append(row.isEmpty()?"(riga vuota)":row);}actual.setText(result.actual.length==0?"(nessun output)":shown.toString());progress.setText(result.steps+(result.steps==1?" blocco eseguito":" blocchi eseguiti"));Ui.enter(title,0);Ui.enter(actual,35);sheet.scroll.post(()->{if(!closed)sheet.scroll.smoothScrollTo(0,title.getTop());});
    }
    void stop(){if(closed||!running)return;interrupted=true;++generation;if(pending!=null)pending.cancel(true);running=false;setRunning(false);title.setText("Prova interrotta");title.setTextColor(Ui.muted());detail.setText("Nessun esito assegnato. Puoi modificare i valori e riprovare.");progress.setText("Valori conservati");actual.setText("");}
    private void setRunning(boolean value){start.setEnabled(!value);stop.setEnabled(value);inputs.setEnabled(!value);expected.setEnabled(!value);}
    void background(){flush();stop();}
    static final class Presentation {
        final String title,detail,actual,progress;final int color;
        Presentation(FlowDataTestPanel panel){title=panel.title.getText().toString();detail=panel.detail.getText().toString();actual=panel.actual.getText().toString();progress=panel.progress.getText().toString();color=panel.title.getCurrentTextColor();}
    }
    Presentation snapshotPresentation(){return running?null:new Presentation(this);}
    void restorePresentation(Presentation state){if(state==null)return;title.setText(state.title);title.setTextColor(state.color);detail.setText(state.detail);actual.setText(state.actual);progress.setText(state.progress);}
    void snapshot(Bundle out){flush();out.putBoolean("data_test_open",true);out.putBoolean("data_test_running",running||interrupted);View focused=sheet.dialog.getCurrentFocus();out.putString("data_test_focus",focused==expected?"expected":focused==inputs?"inputs":"");out.putInt("data_test_start",focused instanceof EditText?((EditText)focused).getSelectionStart():0);out.putInt("data_test_end",focused instanceof EditText?((EditText)focused).getSelectionEnd():0);}
    void restore(Bundle in){if(in.getBoolean("data_test_running")){title.setText("Prova interrotta");detail.setText("I valori sono conservati. Avvia di nuovo la prova dopo il cambio di schermata.");}String focus=in.getString("data_test_focus","");EditText input=focus.equals("expected")?expected:focus.equals("inputs")?inputs:null;if(input!=null){input.requestFocus();int n=input.length();input.setSelection(Math.max(0,Math.min(n,in.getInt("data_test_start"))),Math.max(0,Math.min(n,in.getInt("data_test_end"))));}}
    public void close(){if(closed)return;flush();closed=true;++generation;if(pending!=null)pending.cancel(true);handler.removeCallbacksAndMessages(null);worker.shutdownNow();}
}
