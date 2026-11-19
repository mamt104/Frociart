package it.frociart.app;

import android.app.Activity;
import android.content.*;
import android.graphics.Typeface;
import android.text.*;
import android.text.style.BackgroundColorSpan;
import android.view.*;
import android.widget.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.Executor;
import java.util.function.*;
import java.util.*;

/** Native source viewer. Generation is offline and never executes the exported program. */
final class FlowSourcePanel {
    static final String SUPPORT_MARKER="# --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---";
    final Ui.Sheet sheet;final EditText editor;final TextView status;
    Consumer<String> clipboardWrite;
    private FlowSourceText source,displayedSource,displayedCompleteSource;private boolean restoringView,openingNavigation,hasHighlight;
    private static final class ReadingState{FlowSourceText.Position begin,last,top;int inset,x;}
    private ReadingState pendingReading;private boolean support,wrapped=true;private final FlowSourceTarget target;
    private String language(){return target==null?"Pseudocodice":target.name;}
    private String extension(){return target==null?".txt":"."+target.extension;}
    private final Ui.Button toggle,wrap,copy,save,locate;
    private final Ui.Button how;private final TextView instructions;
    private Ui.Button step,play,followButton;
    private LinearLayout executionControls,sourceOptions,sourceActions;private boolean reparenting,wideLayout;private Ui.Toggle followCode;private boolean followExecution=true,closed,generationFailed;
    FlowSourceExecution executionRuntime;
    static final class State {
        final FlowSourceTarget target;final boolean support,wrapped,follow;final ReadingState reading;
        State(FlowSourcePanel panel){target=panel.target;support=panel.support;wrapped=panel.wrapped;follow=panel.followExecution;reading=panel.captureReading();}
    }
    final Ui.Button types;
    private final Activity owner;private final Executor worker;
    private FlowSource.Program program;private String runtime;
    private FlowFpgtTypes typeFacts;
    private Map<String,Integer> typeChoices=new LinkedHashMap<>();
    private final List<Ui.Sheet> profiles=new ArrayList<>();
    private FlowSourceText.Origin errorOrigin;private int generation;
    private String highlightedFunction,highlightedNode;
    private final java.util.List<Object> highlights=new java.util.ArrayList<>();
    private final Runnable ready;private final boolean canLocate;
    private android.animation.ObjectAnimator scrollMotion;private int followRequest;
    private ViewTreeObserver.OnPreDrawListener followLayout;
    FlowSourcePanel(Activity owner,Ui.Sheet sheet,FlowModel.Document document,Executor worker,
                    Consumer<String> saveFile,BiConsumer<String,String> selectBlock,
                    Runnable follow,Runnable advance,Runnable toggleRun,Runnable ready){
        this(owner,sheet,document,worker,saveFile,selectBlock,follow,advance,toggleRun,ready,false);
    }
    FlowSourcePanel(Activity owner,Ui.Sheet sheet,FlowModel.Document document,Executor worker,
                    Consumer<String> saveFile,BiConsumer<String,String> selectBlock,
                    Runnable follow,Runnable advance,Runnable toggleRun,Runnable ready,boolean javaCode){
        this(owner,sheet,document,worker,saveFile,selectBlock,follow,advance,toggleRun,ready,javaCode?FlowSourceTarget.JAVA:FlowSourceTarget.PYTHON);
    }
    FlowSourcePanel(Activity owner,Ui.Sheet sheet,FlowModel.Document document,Executor worker,
                    Consumer<String> saveFile,BiConsumer<String,String> selectBlock,
                    Runnable follow,Runnable advance,Runnable toggleRun,Runnable ready,FlowSourceTarget target){
        this.owner=owner;this.worker=worker;this.target=target;this.sheet=sheet;this.ready=ready;canLocate=selectBlock!=null;clipboardWrite=text->((android.content.ClipboardManager)owner.getSystemService(Context.CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("Programma "+language(),text));FlowModel.Document snapshot=document.copy();
        status=Ui.note(owner,"Genero il codice "+language()+"…");status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);sheet.body.addView(status);
        how=new Ui.Button(owner,target==null?"Come leggerlo":"Come eseguirlo",null,0);wrapAction(how);sheet.body.addView(how,new LinearLayout.LayoutParams(-1,-2));
        instructions=Ui.note(owner,instructionsText());instructions.setVisibility(View.GONE);sheet.body.addView(instructions);
        how.setOnClickListener(v->{boolean show=instructions.getVisibility()!=View.VISIBLE;if(Ui.animations()){android.transition.AutoTransition transition=new android.transition.AutoTransition();transition.setDuration(260);transition.setInterpolator(Ui.SPRING);android.transition.TransitionManager.beginDelayedTransition(sheet.body,transition);}instructions.setVisibility(show?View.VISIBLE:View.GONE);how.setChosen(show);how.setLabel(show?"Nascondi istruzioni":target==null?"Come leggerlo":"Come eseguirlo");});
        types=new Ui.Button(owner,"Scegli tipi per il codice…",null,0);wrapAction(types);types.setVisibility(View.GONE);types.setOnClickListener(v->chooseTypes());sheet.body.addView(types,new LinearLayout.LayoutParams(-1,-2));
        editor=Ui.edit(owner,"","Codice "+language(),true);editor.setKeyListener(null);editor.setTextIsSelectable(true);editor.setTypeface(Typeface.MONOSPACE);editor.setTextSize(14);
        editor.setHorizontallyScrolling(false);editor.setMinLines(8);editor.setMaxLines(14);editor.setContentDescription(target==null?"Pseudocodice collegato ai blocchi del flowchart":"Codice "+language()+" generato dal flowchart");sheet.body.addView(editor,new LinearLayout.LayoutParams(-1,-2));
        editor.setOnTouchListener((v,event)->{if(event.getActionMasked()==MotionEvent.ACTION_DOWN)cancelScroll();return false;});
        editor.addOnAttachStateChangeListener(new View.OnAttachStateChangeListener(){public void onViewAttachedToWindow(View v){}public void onViewDetachedFromWindow(View v){if(!reparenting)close();}});
        LinearLayout views=Ui.row(owner);sourceOptions=views;sheet.body.addView(views);wrap=new Ui.Button(owner,"Righe adattate",null,0);wrapAction(wrap);wrap.setChosen(true);wrap.setContentDescription("Adatta le righe del codice alla larghezza dello schermo");wrap.setOnClickListener(v->{wrapped=!wrapped;editor.setHorizontallyScrolling(!wrapped);wrap.setChosen(wrapped);wrap.setLabel(wrapped?"Righe adattate":"Adatta righe");Ui.enter(editor,0);});views.addView(wrap,Ui.weight(owner,-2));
        toggle=new Ui.Button(owner,"Mostra supporto",null,0);wrapAction(toggle);toggle.setMaxLines(3);toggle.setOnClickListener(v->{support=!support;showCode();});views.addView(toggle,Ui.weight(owner,-2));
        LinearLayout actions=Ui.row(owner);sourceActions=actions;sheet.body.addView(actions);
        copy=new Ui.Button(owner,"Copia tutto",null,0);wrapAction(copy);copy.setOnClickListener(v->{if(source==null||source.text.isEmpty())return;try{clipboardWrite.accept(source.text);status.setText(target==null?"Pseudocodice copiato.":"Programma completo copiato, incluso il supporto.");}catch(RuntimeException e){status.setText("Appunti non disponibili. Usa Salva "+extension()+".");}});actions.addView(copy,Ui.weight(owner,-2));
        locate=new Ui.Button(owner,"Vai al blocco",null,0);wrapAction(locate);locate.setVisibility(canLocate?View.VISIBLE:View.GONE);locate.setOnClickListener(v->{if(!canLocate)return;FlowSourceText.Origin origin=selectedOrigin();if(origin==null){status.setText("Tocca una riga del programma collegata a un blocco.");return;}sheet.dismiss();selectBlock.accept(origin.functionId,origin.nodeId);});actions.addView(locate,Ui.weight(owner,-2));
        save=new Ui.Button(owner,"Salva "+extension(),"save",1);save.setOnClickListener(v->{if(source!=null&&!source.text.isEmpty())saveFile.accept(source.text);});sheet.footer.addView(save,new LinearLayout.LayoutParams(-1,-2));
        if(advance!=null){LinearLayout execution=Ui.row(owner);executionControls=execution;sheet.footer.addView(execution);
            step=new Ui.Button(owner,"Avanti","step",0);wrapAction(step);step.setOnClickListener(v->advance.run());execution.addView(step,Ui.weight(owner,-2));
            play=new Ui.Button(owner,"Esegui","play",1);wrapAction(play);play.setOnClickListener(v->toggleRun.run());execution.addView(play,Ui.weight(owner,-2));
        }else if(follow!=null){followButton=new Ui.Button(owner,"Segui l’esecuzione","play",0);followButton.setOnClickListener(v->follow.run());sheet.body.addView(followButton,new LinearLayout.LayoutParams(-1,-2));}
        setReady(false);sheet.show();adaptWindow();sheet.dialog.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN);
        worker.execute(()->{try{
            if(target==null){FlowSourceText result=FlowPseudocode.generate(snapshot);owner.runOnUiThread(()->{if(!active())return;source=result;setReady(true);showCode();});return;}
            FlowSource.Program program=FlowSource.compile(snapshot);
            if(!program.canGenerate()){StringBuilder errors=new StringBuilder();int count=0;for(FlowSource.Diagnostic d:program.diagnostics)if(d.error&&count++<4){if(errors.length()>0)errors.append('\n');errors.append(d.message);}throw new IllegalArgumentException(errors.toString());}
            String runtime="";if(target.runtime!=null)try(InputStream in=owner.getAssets().open(target.runtime)){ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int n;while((n=in.read(buffer))!=-1){if(bytes.size()+n>1048576)throw new IOException("Supporto del linguaggio non valido.");bytes.write(buffer,0,n);}runtime=new String(bytes.toByteArray(),StandardCharsets.UTF_8);}
            if(target.template!=null&&target.template.fpgt!=null){FlowFpgtTypes facts=new FlowFpgtTypes(program);String supportRuntime=runtime;owner.runOnUiThread(()->{if(!active())return;this.program=program;this.runtime=supportRuntime;typeFacts=facts;types.setVisibility(needsTypes()?View.VISIBLE:View.GONE);if(needsTypes()){editor.setText("Scegli i tipi per la copia di codice esportata. Il flowchart resta invariato.");chooseTypes();}else generateFpgt();});}
            else{FlowSourceText result=target.generate(program,runtime);owner.runOnUiThread(()->{if(!active())return;source=result;setReady(true);showCode();});}
        }catch(IOException|IllegalArgumentException e){owner.runOnUiThread(()->{if(active())showError(e);});}});
    }
    private String instructionsText(){return target==null?"Le righe evidenziate corrispondono al blocco selezionato o in esecuzione. Tocca una riga e premi Vai al blocco per ritrovarla nel diagramma. Per scrivere o importare testo usa Pseudocodice ↔ diagramma. Salva e Copia conservano il testo completo.":(target.template==null?"":"Modello: "+target.name+"\n")+target.instructions+" Salva e Copia tutto includono sempre il supporto completo. Nella prova dell’app, Avanti ed Esegui seguono il diagramma e ne evidenziano il codice.";}
    private boolean active(){return !closed&&!owner.isFinishing()&&!owner.isDestroyed()&&sheet.dialog.isShowing();}
    private boolean needsTypes(){if(typeFacts==null)return false;for(FlowFpgtTypes.Scope s:typeFacts.scopes)for(FlowFpgtTypes.Symbol v:s.symbols())if(!v.declared&&!v.constantPi&&(v.inputChoice||v.type.kind==FlowModel.AUTO))return true;return false;}
    void chooseTypes(){if(!active()||!needsTypes())return;Map<String,Integer> selected=new LinkedHashMap<>(typeChoices);List<String> required=new ArrayList<>();Ui.Sheet picker=new Ui.Sheet(owner,"Tipi per il codice esportato","Il diagramma e la prova restano invariati.");profiles.add(picker);picker.paragraph("Scegli i tipi mancanti. Queste scelte valgono solo per questa copia di codice.");boolean[] accepted={false};Ui.Button generate=new Ui.Button(owner,"Genera codice","check",1);generate.setOnClickListener(v->{accepted[0]=true;typeChoices=new LinkedHashMap<>(selected);picker.dismiss();generateFpgt();});picker.footer.addView(generate,new LinearLayout.LayoutParams(-1,-2));String[] labels={"Scegli il tipo…","Intero","Reale","Testo","Logico"};
        for(FlowFpgtTypes.Scope scope:typeFacts.scopes)for(FlowFpgtTypes.Symbol symbol:scope.symbols())if(!symbol.declared&&!symbol.constantPi&&(symbol.inputChoice||symbol.type.kind==FlowModel.AUTO)){String id=FlowFpgtTypes.key(scope.source.id,symbol.name);required.add(id);String title=symbol.name+" · "+scope.source.name;Ui.Choice choice=new Ui.Choice(owner);int initial=selected.getOrDefault(id,0);choice.bind(title,labels[initial>=1&&initial<=4?initial:0],Ui.accentInk());choice.setOnClickListener(v->{Ui.Sheet values=new Ui.Sheet(owner,"Tipo di "+symbol.name,"Solo nella copia di codice esportata");profiles.add(values);values.dialog.setOnDismissListener(d->profiles.remove(values));for(int i=1;i<=4;i++){final int type=i;values.action(labels[i],null,0,()->{selected.put(id,type);choice.bind(title,labels[type],Ui.accentInk());generate.setEnabled(required.stream().allMatch(selected::containsKey));values.dismiss();});}values.show();});picker.body.addView(choice,new LinearLayout.LayoutParams(-1,-2));Ui.gap(picker.body,8);}
        generate.setEnabled(!required.isEmpty()&&required.stream().allMatch(selected::containsKey));picker.dialog.setOnDismissListener(d->{profiles.remove(picker);if(!accepted[0]&&active()&&source==null&&types.isEnabled())status.setText("Tipi non scelti. Premi Scegli tipi per generare il codice.");});picker.show();picker.dialog.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN);
    }
    private void generateFpgt(){final int ticket=++generation;Map<String,Integer> choices=new LinkedHashMap<>(typeChoices);if(source!=null)pendingReading=captureReading();source=null;errorOrigin=null;describeInstructions();setReady(false);types.setEnabled(false);editor.setText("Genero il codice dal modello .fpgt…");status.setText("Generazione in corso…");status.setTextColor(Ui.muted());try{worker.execute(()->{try{FlowSourceText result=target.generateTyped(program,runtime,choices);owner.runOnUiThread(()->{if(!active()||ticket!=generation)return;source=result;types.setEnabled(true);types.setLabel("Tipi del codice…");setReady(true);showCode();});}catch(IllegalArgumentException error){owner.runOnUiThread(()->{if(active()&&ticket==generation){types.setEnabled(true);showError(error);}});}});}catch(java.util.concurrent.RejectedExecutionException error){if(active()){types.setEnabled(true);showError(new IllegalArgumentException("Generazione non disponibile. Riapri il pannello e riprova."));}}}
    private void showError(Exception error){generationFailed=true;source=null;describeInstructions();errorOrigin=error instanceof FlowFpgt.GenerationError&&((FlowFpgt.GenerationError)error).nodeId!=null?new FlowSourceText.Origin(((FlowFpgt.GenerationError)error).functionId,((FlowFpgt.GenerationError)error).nodeId):null;editor.setText(error.getMessage());setReady(false);locate.setEnabled(canLocate&&errorOrigin!=null);status.setText(errorOrigin==null?"Codice non generato: "+error.getMessage():"Vai al blocco per correggere il diagramma, oppure modifica il modello.");status.setTextColor(Ui.danger());refreshRuntime();}
    private static void wrapAction(Ui.Button button){button.setSingleLine(false);button.setMaxLines(2);}
    private void setReady(boolean ready){boolean content=ready&&source!=null&&!source.text.isEmpty();toggle.setEnabled(content);copy.setEnabled(content);save.setEnabled(content);locate.setEnabled(content&&canLocate);if(step!=null)step.setEnabled(content&&canExecute());if(play!=null)play.setEnabled(content&&canExecute());if(followButton!=null)followButton.setEnabled(content&&canExecute());}
    static String programText(FlowSourceText source){if(source.language.equals("cpp")||source.language.equals("powershell"))return source.programView().text;String marker=FlowSourceTarget.supportMarker(source.language);if(marker==null)return source.text;int end=source.text.indexOf("\n"+marker+"\n");return end<0?source.text:source.text.substring(0,end+1);}
    private void describeInstructions(){instructions.setText(instructionsText()+(source==null||source.notices.isEmpty()?"":"\n\nAdattamenti applicati al sorgente\n• "+String.join("\n• ",source.notices)+"\nIl modello originale resta invariato."));}
    private ReadingState captureReading(){
        FlowSourceText previous=displayedCompleteSource;if(previous==null)return null;int caret=Math.max(0,editor.getSelectionStart()),end=Math.max(0,editor.getSelectionEnd());ReadingState result=new ReadingState();
        result.begin=previous.capture(displayedSource,caret,editor.length());result.last=previous.capture(displayedSource,end,editor.length());android.text.Layout layout=editor.getLayout();int oldY=editor.getScrollY(),topOffset=layout==null?caret:layout.getLineStart(layout.getLineForVertical(oldY));
        result.x=editor.getScrollX();result.inset=layout==null?0:oldY-layout.getLineTop(layout.getLineForVertical(oldY));result.top=previous.capture(displayedSource,topOffset,editor.length());return result;
    }
    private void showCode(){
        if(source==null)return;generationFailed=false;ReadingState reading=pendingReading==null?captureReading():pendingReading;pendingReading=null;cancelScroll();
        displayedCompleteSource=source;displayedSource=support?source:source.programView();editor.setText(support?source.text:programText(source));Ui.enter(editor,0);
        Integer restored=source.restore(displayedSource,reading==null?null:reading.begin,editor.length());boolean kept=restored!=null;
        if(kept){Integer tail=source.restore(displayedSource,reading.last,editor.length());editor.setSelection(restored,Math.max(restored,tail==null?restored:tail));}
        highlights.clear();highlightedFunction=highlightedNode=null;hasHighlight=false;toggle.setVisibility(programText(source).equals(source.text)?View.GONE:View.VISIBLE);toggle.setLabel(support?"Mostra solo il programma":"Mostra supporto");describeInstructions();
        status.setText(source.text.isEmpty()?"Il modello non ha prodotto testo per questo diagramma.":source.notices.isEmpty()?"Salva e Copia includono il testo completo.":"Adattamenti applicati: dettagli in Come eseguirlo. Salva e Copia includono il programma completo.");
        restoringView=kept;openingNavigation=!kept;
        try{ready.run();if(openingNavigation){FlowSourceText.Line line=displayedSource.openingLine(null,null);if(line!=null){editor.setSelection(Math.min(editor.length(),line.start));scheduleScroll(line.start,0,true,0);}}}
        finally{restoringView=openingNavigation=false;}
        if(kept){Integer offset=source.restore(displayedSource,reading.top,editor.length());if(offset!=null)scheduleScroll(offset,reading.inset,false,reading.x);}
    }
    void highlight(String function,String node,String label){
        if(source==null)return;if(source.text.isEmpty()){status.setText("Il modello non ha prodotto testo per questo diagramma.");return;}
        FlowSourceText visible=displayedSource==null?source:displayedSource;java.util.List<FlowSourceText.Line> lines=node==null?java.util.Collections.emptyList():visible.forBlock(function,node);FlowSourceText.Line opening=visible.openingLine(function,node);
        String message=lines.isEmpty()?"Seleziona una riga per trovare il suo blocco.":label+" · riga "+(opening==null?lines.get(0).number:opening.number);if(!source.notices.isEmpty())message="Adattamenti applicati: dettagli in Come eseguirlo.\n"+message;if(!TextUtils.equals(status.getText(),message))status.setText(message);
        if(hasHighlight&&java.util.Objects.equals(function,highlightedFunction)&&java.util.Objects.equals(node,highlightedNode))return;
        hasHighlight=true;highlightedFunction=function;highlightedNode=node;Editable text=editor.getText();for(Object span:highlights)text.removeSpan(span);highlights.clear();
        for(FlowSourceText.Line line:lines)if(line.start<text.length()){Object span=new BackgroundColorSpan(Ui.tint());highlights.add(span);text.setSpan(span,line.start,Math.min(text.length(),line.start+line.length),Spannable.SPAN_EXCLUSIVE_EXCLUSIVE);}
        if(openingNavigation&&opening!=null){editor.setSelection(Math.min(editor.length(),opening.start));openingNavigation=false;scheduleScroll(opening.start,0,true,0);}
        else if(followExecution&&!restoringView&&!lines.isEmpty()&&opening!=null)scheduleScroll(opening.start,0,true,editor.getScrollX());
    }
    // Restore after layout and native caret scrolling; a new touch or closed sheet cancels it.
    private void scheduleScroll(final int offset,final int inset,final boolean center,final int horizontal){
        cancelScroll();final int ticket=followRequest;
        followLayout=()->{
            if(ticket==followRequest&&sheet.dialog.isShowing()&&(editor.getLayout()==null||editor.getHeight()<=editor.getTotalPaddingTop()+editor.getTotalPaddingBottom()))return true;
            ViewTreeObserver.OnPreDrawListener pending=followLayout;followLayout=null;
            if(pending!=null&&editor.getViewTreeObserver().isAlive())editor.getViewTreeObserver().removeOnPreDrawListener(pending);
            if(ticket!=followRequest||!sheet.dialog.isShowing()||editor.getLayout()==null)return true;
            editor.post(()->{
                if(ticket!=followRequest||!sheet.dialog.isShowing()||editor.getLayout()==null)return;
                android.text.Layout layout=editor.getLayout();int height=editor.getHeight()-editor.getTotalPaddingTop()-editor.getTotalPaddingBottom();
                int top=layout.getLineTop(layout.getLineForOffset(Math.min(editor.length(),offset)));
                int target=Math.max(0,Math.min(Math.max(0,layout.getHeight()-height),center?top-height/3:top+inset));
                if(!center||!Ui.animations()){editor.scrollTo(horizontal,target);return;}
                scrollMotion=android.animation.ObjectAnimator.ofInt(editor,"scrollY",editor.getScrollY(),target);scrollMotion.setDuration(260);scrollMotion.setInterpolator(Ui.SPRING);scrollMotion.start();
            });
            return true;
        };
        editor.getViewTreeObserver().addOnPreDrawListener(followLayout);editor.invalidate();
    }
    private void cancelScroll(){followRequest++;if(followLayout!=null){if(editor.getViewTreeObserver().isAlive())editor.getViewTreeObserver().removeOnPreDrawListener(followLayout);followLayout=null;}if(scrollMotion!=null){scrollMotion.cancel();scrollMotion=null;}}
    void execution(String label,boolean canPlay,boolean canStep){if(play!=null){play.setLabel(label);play.setIcon(label.equals("Pausa")?"pause":label.equals("Riprova")?"undo":label.equals("Inserisci valore")||label.equals("Vai alla richiesta")?"right":"play");play.setEnabled(canExecute()&&canPlay&&(executionRuntime==null||executionRuntime.available()));}if(step!=null)step.setEnabled(canExecute()&&canStep&&(executionRuntime==null||executionRuntime.available()));}
    private boolean canExecute(){if(source==null||source.text.isEmpty())return false;for(FlowSourceText.Line line:source.lines)if(!line.origins.isEmpty())return true;return false;}
    void prepareExecutionControls(Runnable advance,Runnable toggleRun){
        if(executionControls==null){executionControls=Ui.row(owner);sheet.footer.addView(executionControls);step=new Ui.Button(owner,"Avanti","step",0);wrapAction(step);executionControls.addView(step,Ui.weight(owner,-2));play=new Ui.Button(owner,"Esegui","play",1);wrapAction(play);executionControls.addView(play,Ui.weight(owner,-2));}
        step.setOnClickListener(v->advance.run());play.setOnClickListener(v->toggleRun.run());if(followButton!=null)followButton.setVisibility(View.GONE);
    }
    void bindExecution(FlowSourceExecution.Host host){
        if(executionControls==null||executionRuntime!=null)return;int at=sheet.body.indexOfChild(editor)+1;
        followCode=new Ui.Toggle(owner,"Segui blocco in esecuzione",null);followCode.setSingleLine(false);followCode.setMaxLines(2);followCode.setChecked(followExecution);followCode.setOnClickListener(v->{followCode.toggle();followExecution=followCode.isChecked();cancelScroll();if(followExecution){hasHighlight=false;ready.run();}});sheet.body.addView(followCode,at,new LinearLayout.LayoutParams(-1,-2));
        executionRuntime=new FlowSourceExecution(owner,sheet,host,save,executionControls,at+1);adaptRuntimeLayout();refreshRuntime();
    }
    private void adaptWindow(){
        android.content.res.Configuration config=owner.getResources().getConfiguration();sheet.compactHeader(config.orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE&&config.screenHeightDp<500);
        if(config.screenWidthDp<720)return;int available=owner.getResources().getDisplayMetrics().widthPixels;
        if(android.os.Build.VERSION.SDK_INT>=30){WindowMetrics metrics=owner.getWindowManager().getCurrentWindowMetrics();android.graphics.Insets bars=metrics.getWindowInsets().getInsetsIgnoringVisibility(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());available=metrics.getBounds().width()-bars.left-bars.right;}
        sheet.dialog.getWindow().setLayout(Math.max(1,Math.min(available,Ui.dp(owner,1040))),-2);
    }
    private void adaptRuntimeLayout(){
        if(executionRuntime==null||owner.getResources().getConfiguration().screenWidthDp<720||wideLayout)return;ReadingState reading=captureReading();cancelScroll();wideLayout=true;int at=sheet.body.indexOfChild(editor);reparenting=true;
        try{
            LinearLayout row=new LinearLayout(owner);row.setOrientation(LinearLayout.HORIZONTAL);row.setGravity(Gravity.TOP);LinearLayout code=Ui.column(owner);
            for(View view:new View[]{editor,followCode,sourceOptions,sourceActions,save}){if(view.getParent() instanceof ViewGroup)((ViewGroup)view.getParent()).removeView(view);code.addView(view,new LinearLayout.LayoutParams(-1,-2));}
            ((ViewGroup)executionRuntime.area.getParent()).removeView(executionRuntime.area);
            row.addView(code,new LinearLayout.LayoutParams(0,-2,1.7f));LinearLayout.LayoutParams runtimeSize=new LinearLayout.LayoutParams(0,-2,1);runtimeSize.leftMargin=Ui.dp(owner,16);row.addView(executionRuntime.area,runtimeSize);sheet.body.addView(row,Math.min(at,sheet.body.getChildCount()),new LinearLayout.LayoutParams(-1,-2));
        }finally{reparenting=false;}
        if(reading!=null&&source!=null){Integer offset=source.restore(displayedSource,reading.top,editor.length());if(offset!=null)scheduleScroll(offset,reading.inset,false,reading.x);}
    }
    void refreshRuntime(){if(executionRuntime!=null){executionRuntime.refresh(canExecute());if(generationFailed)executionRuntime.message("Codice non disponibile. Correggi il diagramma o il modello.");else if(source!=null&&!canExecute())executionRuntime.message(source.text.isEmpty()?"Il modello non ha prodotto codice da provare.":"Questo modello non collega il testo ai blocchi del diagramma.");}if(followCode!=null)followCode.setEnabled(canExecute());}
    void focusRuntimeInput(){if(executionRuntime!=null)executionRuntime.focusInput();}
    State snapshot(){return new State(this);}
    void restore(State state){if(state==null)return;support=state.support;wrapped=state.wrapped;followExecution=state.follow;pendingReading=state.reading;editor.setHorizontallyScrolling(!wrapped);wrap.setChosen(wrapped);wrap.setLabel(wrapped?"Righe adattate":"Adatta righe");if(followCode!=null)followCode.setChecked(followExecution);if(source!=null)showCode();}
    void close(){if(closed)return;closed=true;generation++;cancelScroll();if(executionRuntime!=null)executionRuntime.close();for(Ui.Sheet profile:new ArrayList<>(profiles))profile.dismissNow();profiles.clear();}
    FlowSourceText.Origin selectedOrigin(){if(source==null)return errorOrigin;FlowSourceText.Line line=(displayedSource==null?source:displayedSource).atOffset(Math.max(0,editor.getSelectionStart()));return line==null||line.origins.isEmpty()?null:line.origins.get(0);}
}
