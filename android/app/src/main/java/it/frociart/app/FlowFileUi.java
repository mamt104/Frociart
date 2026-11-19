package it.frociart.app;

import android.app.Activity;
import android.content.*;
import android.net.Uri;
import android.os.Handler;
import android.view.View;
import android.widget.TextView;
import java.io.*;
import java.util.*;
import java.util.concurrent.ExecutorService;

/** Native document picker and recovery UI. Async results belong to one exact runner request. */
final class FlowFileUi {
    interface Host {
        FlowEngine.Runner runner();boolean alive();boolean foreground();
        Ui.Sheet sheet(String title,String text);void changed(boolean completed);void message(String title,String text);
    }
    private final Activity activity;private final ExecutorService io;private final Handler handler;private final Host host;
    final FlowFileRecovery recovery;
    private Ui.Button action;private Job pending;private long generation;
    private static final int FIRST=4000,LAST=63000;
    private static final class Job {
        final FlowEngine.Runner runner;final FlowTextFiles.Request request;final String text,name;final boolean reading;final int code;
        boolean picking=true;
        Job(FlowEngine.Runner runner,FlowTextFiles.Request request,String name,String text,boolean reading,int code){this.runner=runner;this.request=request;this.name=name;this.text=text;this.reading=reading;this.code=code;}
    }
    FlowFileUi(Activity activity,ExecutorService io,Handler handler,Host host){this.activity=activity;this.io=io;this.handler=handler;this.host=host;recovery=new FlowFileRecovery(new File(activity.getFilesDir(),"generated-files"));}
    void bind(FlowEngine.Runner runner){generation++;pending=null;if(runner!=null)runner.files.journalFactory=recovery::create;}
    void close(FlowEngine.Runner runner){if(runner!=null)try{runner.files.closeJournal();}catch(IOException ex){if(host.alive())host.message("Bozza conservata","Il file generato resta disponibile nel recupero locale.");}}
    boolean isBusy(){return pending!=null;}
    boolean isPicking(){return pending!=null&&pending.picking;}
    Ui.Button createAction(){action=new Ui.Button(activity,"Scegli file",null,2);action.setTag("runtime-file-action");action.setOnClickListener(v->chooseCurrent());refresh();return action;}
    void refresh(){if(action==null)return;FlowEngine.Runner runner=host.runner();boolean waiting=runner!=null&&runner.state==FlowEngine.State.WAITING_FILE;action.setVisibility(waiting?View.VISIBLE:View.GONE);action.setEnabled(waiting&&pending==null);if(waiting){action.setText(pending!=null?"Operazione in corso…":runner.pendingFile().kind==FlowTextFiles.Kind.READ?"Scegli il file da leggere":"Salva il file generato");action.setContentDescription(action.getText()+". "+runner.lastMessage);}}
    void chooseCurrent(){FlowEngine.Runner runner=host.runner();if(pending!=null||runner==null||runner.state!=FlowEngine.State.WAITING_FILE)return;FlowTextFiles.Request request=runner.pendingFile();launch(runner,request,request.name,request.text,request.kind==FlowTextFiles.Kind.READ);}
    private void launch(FlowEngine.Runner runner,FlowTextFiles.Request request,String name,String text,boolean reading){
        if(pending!=null){host.message("Attendi il file","Completa o annulla la selezione precedente.");return;}
        SharedPreferences prefs=activity.getSharedPreferences("frociart",Context.MODE_PRIVATE);int code=prefs.getInt("runtime_file_request_serial",FIRST-1)+1;if(code<FIRST||code>LAST)code=FIRST;
        if(!prefs.edit().putInt("runtime_file_request_serial",code).commit()){host.message("Selezione non avviata","Non riesco a conservare lo stato locale. Controlla lo spazio disponibile e riprova.");return;}
        Job job=new Job(runner,request,name,text,reading,code);pending=job;
        Intent intent=new Intent(reading?Intent.ACTION_OPEN_DOCUMENT:Intent.ACTION_CREATE_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType(reading?"*/*":"text/plain");
        if(!reading)intent.putExtra(Intent.EXTRA_TITLE,suggestedName(name));
        try{activity.startActivityForResult(intent,code);refresh();}catch(ActivityNotFoundException|SecurityException ex){pending=null;defer(job,"Il selettore dei documenti non è disponibile. Puoi riprovare.");}
    }
    private static String suggestedName(String name){String value=(name==null?"risultato.txt":name).replace('\\','/');value=value.substring(value.lastIndexOf('/')+1).replaceAll("[\\x00-\\x1f<>:\"|?*]","_");if(value.trim().isEmpty()||value.equals(".")||value.equals(".."))value="risultato.txt";if(value.length()>180)value=value.substring(0,176)+".txt";if(!value.contains("."))value+=".txt";return value;}
    private boolean current(Job job){return host.alive()&&job.runner!=null&&host.runner()==job.runner&&job.runner.pendingFile()==job.request;}
    private void defer(Job job,String message){if(current(job)){job.runner.fileDeferred(job.request,message);host.changed(false);}else if(host.alive()&&job.runner==null)host.message("File generato",message==null?"Operazione annullata. La bozza è ancora disponibile.":message);refresh();}
    boolean result(int code,int result,Intent data){
        if(code<FIRST||code>LAST)return false;Job job=pending;if(job==null||job.code!=code||!job.picking)return true;
        if(result!=Activity.RESULT_OK||data==null||data.getData()==null){pending=null;defer(job,null);return true;}
        if(job.runner!=null&&!current(job)){pending=null;refresh();return true;}
        job.picking=false;Uri uri=data.getData();refresh();
        io.execute(()->{
            String content=null,error=null;
            try{
                if(job.reading){try(InputStream stream=activity.getContentResolver().openInputStream(uri)){if(stream==null)throw new IOException("File non disponibile.");content=FlowTextFiles.readText(stream);}}
                else try(OutputStream stream=activity.getContentResolver().openOutputStream(uri,"wt")){if(stream==null)throw new IOException("Destinazione non disponibile.");FlowTextFiles.writeText(stream,job.text);}
            }catch(Exception ex){error=ex instanceof FlowEngine.Problem?ex.getMessage():job.reading?"Non riesco a leggere il file. Controlla che sia disponibile e riprova.":"Salvataggio non riuscito. Il testo resta in File generati; puoi riprovare o scegliere un'altra destinazione.";}
            final String read=content,problem=error;
            handler.post(()->{
                if(pending!=job||!host.alive())return;pending=null;
                if(problem!=null){defer(job,problem);return;}
                if(current(job)){boolean accepted=job.runner.provideFile(job.request,read);host.changed(accepted);}
                else if(job.runner==null)host.message("Copia salvata","Il file è stato scritto nella destinazione scelta. La bozza resta disponibile in File generati.");
                refresh();
            });
        });return true;
    }
    void showRecovery(){
        long epoch=generation;io.execute(()->{try{List<FlowFileRecovery.Entry> entries=recovery.list();handler.post(()->{if(!host.alive()||!host.foreground()||epoch!=generation)return;Ui.Sheet sheet=host.sheet("File generati e recupero",entries.isEmpty()?"I file prodotti dai blocchi Scrivi file restano qui anche dopo aver chiuso o ricominciato la simulazione.":"Bozze conservate su questo dispositivo. Puoi aprirle e salvare una copia.");for(FlowFileRecovery.Entry entry:entries)sheet.action(entry.name,"folder",0,()->{sheet.dismiss();showEntry(entry);});sheet.show();});}catch(IOException ex){handler.post(()->{if(host.alive())host.message("Recupero non disponibile","Non riesco a leggere le bozze locali. Puoi riprovare.");});}});
    }
    private void showEntry(FlowFileRecovery.Entry entry){
        long epoch=generation;io.execute(()->{try{FlowFileRecovery.Content content=recovery.load(entry);handler.post(()->{if(!host.alive()||!host.foreground()||epoch!=generation)return;
            Ui.Sheet sheet=host.sheet(content.name,content.incomplete?"L'ultima scrittura era incompleta. Qui trovi la parte verificata che puoi ancora salvare.":content.text.length()+" caratteri · bozza locale");
            TextView preview=Ui.text(activity,content.text.length()>16000?content.text.substring(0,16000)+"\n… Anteprima abbreviata. Il salvataggio comprende tutto il file.":content.text,12,false);preview.setTypeface(android.graphics.Typeface.MONOSPACE);preview.setTextIsSelectable(true);sheet.body.addView(preview);
            sheet.primary(content.incomplete?"Salva la parte recuperata":"Salva una copia","save",()->{sheet.dismiss();launch(null,null,content.name,content.text,false);});
            FlowEngine.Runner runner=host.runner();boolean active=runner!=null&&entry.id().equals(runner.files.activeJournalId());
            if(!active)sheet.action("Rimuovi bozza","trash",3,()->{sheet.dismiss();confirmRemove(entry);});
            sheet.show();});}catch(IOException ex){handler.post(()->{if(!host.alive()||!host.foreground()||epoch!=generation)return;Ui.Sheet unreadable=host.sheet("Bozza non leggibile","Non riesco a leggere questa bozza. Puoi conservarla oppure rimuoverla dal recupero locale.");FlowEngine.Runner runner=host.runner();if(runner==null||!entry.id().equals(runner.files.activeJournalId()))unreadable.action("Rimuovi bozza","trash",3,()->{unreadable.dismiss();confirmRemove(entry);});unreadable.show();});}});
    }
    private void confirmRemove(FlowFileRecovery.Entry entry){Ui.Sheet confirm=host.sheet("Rimuovi bozza","Rimuovere questa copia locale? Salva prima il file se ti serve.");confirm.action("Rimuovi questa bozza","trash",3,()->{FlowEngine.Runner now=host.runner();if(now!=null&&entry.id().equals(now.files.activeJournalId()))return;try{recovery.delete(entry);confirm.dismiss();showRecovery();}catch(IOException ex){host.message("Bozza non rimossa","Non riesco a rimuovere la bozza. Puoi riprovare.");}});confirm.show();}
}
