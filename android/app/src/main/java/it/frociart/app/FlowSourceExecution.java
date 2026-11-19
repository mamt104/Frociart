package it.frociart.app;

import android.app.Activity;
import android.content.*;
import android.graphics.Typeface;
import android.os.SystemClock;
import android.text.*;
import android.view.*;
import android.widget.*;
import java.util.*;

/** Event-refreshed controls for the same runner used by the diagram. No second interpreter. */
final class FlowSourceExecution {
    interface Host {
        FlowEngine.Runner runner();boolean running();boolean valid();boolean fileBusy();
        String pending();void pending(String value);void submit(String value);
        void pause();void reset();void file();void save(String value);String description();
    }
    final LinearLayout area,inputArea;
    final EditText input;
    final TextView state,question,inputTitle,preview;
    final Ui.Button send,inputError,file,reset,read,copy,readQuestion;
    java.util.function.Consumer<String> clipboardWrite;
    int refreshCount;
    private final Activity owner;private final Ui.Sheet sheet;private final Host host;
    private final View sourceSave,executionControls;
    private final Set<Ui.Sheet> children=new LinkedHashSet<>();
    private FlowEngine.Runner observed;private long generation=-1,lastSend=-1000,lastOutput=-1000;
    private int outputCount=-1,mode=-1,observedSteps=-1;private String waitingKey,notice;private boolean closed,sync,compact,ready;
    private final ViewTreeObserver.OnGlobalLayoutListener keyboard=this::keyboardLayout;

    FlowSourceExecution(Activity owner,Ui.Sheet sheet,Host host,View sourceSave,View executionControls,int position){
        this.owner=owner;this.sheet=sheet;this.host=host;this.sourceSave=sourceSave;this.executionControls=executionControls;
        clipboardWrite=value->((android.content.ClipboardManager)owner.getSystemService(Context.CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("Risultato Frociart",value));
        area=Ui.column(owner);Ui.pad(area,0,10,0,10);sheet.body.addView(area,position,new LinearLayout.LayoutParams(-1,-2));
        state=Ui.note(owner,"");state.setTag("source-run-status");state.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);area.addView(state);
        Ui.gap(area,8);area.addView(Ui.text(owner,"Risultato",15,true));Ui.gap(area,6);
        preview=Ui.text(owner,"Il risultato apparirà qui.",14,false);preview.setTag("source-result");preview.setTypeface(Typeface.MONOSPACE);preview.setTextIsSelectable(true);preview.setLines(4);preview.setGravity(Gravity.TOP|Gravity.START);preview.setMovementMethod(new android.text.method.ScrollingMovementMethod());Ui.pad(preview,12,10,12,10);Ui.card(preview,Ui.bg(),14);area.addView(preview,new LinearLayout.LayoutParams(-1,-2));
        preview.setOnTouchListener((v,e)->{v.getParent().requestDisallowInterceptTouchEvent(e.getActionMasked()!=MotionEvent.ACTION_UP&&e.getActionMasked()!=MotionEvent.ACTION_CANCEL);return false;});
        LinearLayout outputActions=Ui.row(owner);read=button("Leggi tutto",null,0);copy=button("Copia risultato",null,0);outputActions.addView(read,Ui.weight(owner,-2));outputActions.addView(copy,Ui.weight(owner,-2));area.addView(outputActions);read.setOnClickListener(v->openResult());copy.setOnClickListener(v->copyResult());
        question=Ui.note(owner,"");question.setTextIsSelectable(true);question.setMaxLines(3);question.setEllipsize(TextUtils.TruncateAt.END);Ui.pad(question,0,8,0,4);area.addView(question);
        readQuestion=button("Leggi la domanda completa",null,0);readQuestion.setOnClickListener(v->openQuestion());area.addView(readQuestion,new LinearLayout.LayoutParams(-1,-2));
        reset=button("Ricomincia la prova","undo",0);reset.setOnClickListener(v->{if(canAct()){closeChildren();host.reset();refresh(ready);}});area.addView(reset,new LinearLayout.LayoutParams(-1,-2));
        inputTitle=Ui.text(owner,"",12,true);inputTitle.setMaxLines(2);inputTitle.setEllipsize(TextUtils.TruncateAt.END);Ui.pad(inputTitle,0,5,0,4);sheet.footer.addView(inputTitle,0,new LinearLayout.LayoutParams(-1,-2));
        inputArea=Ui.row(owner);inputArea.setBaselineAligned(false);input=Ui.edit(owner,"","Valore",false);input.setId(View.generateViewId());inputTitle.setLabelFor(input.getId());input.setTag("source-run-input");input.setFilters(new InputFilter[]{new InputFilter.LengthFilter(32768)});inputArea.addView(input,Ui.weight(owner,-2));inputError=button("Leggi errore",null,0);inputError.setSingleLine(true);inputError.setMaxLines(1);inputError.setEllipsize(null);inputError.setMinimumHeight(dp(52));inputError.setTextColor(Ui.danger());inputError.setTag("source-run-error-action");inputError.setVisibility(View.GONE);inputError.setOnClickListener(v->openInputError());LinearLayout.LayoutParams errorSize=new LinearLayout.LayoutParams(-2,-2);errorSize.setMarginStart(dp(8));errorSize.setMarginEnd(dp(8));inputArea.addView(inputError,errorSize);send=new Ui.Button(owner,"","right",1);send.setContentDescription("Invia valore alla prova del flowchart");send.setTag("source-run-send");inputArea.addView(send,Ui.lp(owner,52,52));sheet.footer.addView(inputArea,1,new LinearLayout.LayoutParams(-1,-2));send.setOnClickListener(v->submit());Ui.inputSubmission(input,this::submit);
        input.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void afterTextChanged(Editable e){}public void onTextChanged(CharSequence s,int a,int b,int c){if(!sync&&canSend())host.pending(s.toString());}});
        file=button("Scegli file…","folder",1);file.setTag("source-run-file");file.setOnClickListener(v->{if(canAct()&&!host.fileBusy()){host.file();refresh(ready);}});sheet.footer.addView(file,2,new LinearLayout.LayoutParams(-1,-2));
        sheet.root.getViewTreeObserver().addOnGlobalLayoutListener(keyboard);
        refresh(false);
    }
    private int dp(float value){return Ui.dp(owner,value);}
    private Ui.Button button(String label,String icon,int tone){Ui.Button b=new Ui.Button(owner,label,owner.getResources().getConfiguration().fontScale>=1.3f?null:icon,tone);b.setSingleLine(false);b.setMaxLines(3);b.setEllipsize(null);b.setMinimumHeight(dp(48));Ui.pad(b,8,8,8,8);return b;}
    boolean available(){return canAct();}
    private boolean canAct(){return !closed&&ready&&host.valid()&&host.runner()!=null;}
    private boolean canSend(){FlowEngine.Runner r=host.runner();return canAct()&&r==observed&&r.generation()==generation&&r.state==FlowEngine.State.WAITING;}
    private void transition(){if(Ui.animations()&&sheet.root.isLaidOut()&&sheet.dialog.isShowing()){android.transition.TransitionSet set=new android.transition.TransitionSet().setOrdering(android.transition.TransitionSet.ORDERING_TOGETHER);set.addTransition(new android.transition.ChangeBounds());set.addTransition(new android.transition.Fade());set.setDuration(280);set.setInterpolator(Ui.SPRING);android.transition.TransitionManager.beginDelayedTransition(sheet.root,set);}}
    void refresh(boolean ready){
        if(closed)return;refreshCount++;this.ready=ready;FlowEngine.Runner r=host.runner();boolean changed=r!=observed||r!=null&&r.generation()!=generation;
        if(changed){closeChildren();outputCount=-1;waitingKey=null;lastSend=lastOutput=-1000;notice=null;}observed=r;generation=r==null?-1:r.generation();
        if(r!=null&&observedSteps!=r.steps){observedSteps=r.steps;notice=null;}boolean available=canAct(),waiting=available&&r.state==FlowEngine.State.WAITING,fileWait=available&&r.state==FlowEngine.State.WAITING_FILE;
        int next=!available?3:waiting?1:fileWait?2:0;if(mode!=next){transition();mode=next;}
        inputArea.setVisibility(waiting?View.VISIBLE:View.GONE);inputTitle.setVisibility(waiting?View.VISIBLE:View.GONE);question.setVisibility(waiting?View.VISIBLE:View.GONE);file.setVisibility(fileWait?View.VISIBLE:View.GONE);executionControls.setVisibility(available&&!waiting&&!fileWait?View.VISIBLE:View.GONE);
        String key=waiting?r.generation()+":"+r.currentFrameId()+":"+r.currentVisit()+":"+r.inputVariable:null;boolean newQuestion=changed||!Objects.equals(waitingKey,key);waitingKey=key;
        if(waiting){updateInputTitle();String variable=r.inputVariable==null?"Valore":r.inputVariable;input.setHint(variable);input.setContentDescription("Valore per "+variable+". "+r.inputPrompt);question.setText(prefix(r.inputPrompt,220));question.setContentDescription(r.inputPrompt);String pending=host.pending()==null?"":host.pending();if(newQuestion||!input.getText().toString().equals(pending)){sync=true;try{input.setText(pending);input.setSelection(input.length());if(newQuestion)input.setError(null);}finally{sync=false;}}}
        readQuestion.setVisibility(waiting&&r.inputPrompt!=null&&r.inputPrompt.length()>220?View.VISIBLE:View.GONE);send.setEnabled(waiting);
        if(fileWait){String label=host.fileBusy()?"Operazione in corso…":r.pendingFile().kind==FlowTextFiles.Kind.READ?"Scegli il file da leggere":"Salva il file generato";file.setLabel(label);file.setContentDescription(label+". "+r.lastMessage);}file.setEnabled(fileWait&&!host.fileBusy());
        reset.setEnabled(available&&(r.steps>0||r.state==FlowEngine.State.ERROR));sourceSave.setVisibility(compact?View.GONE:View.VISIBLE);
        boolean hasOutput=available&&!r.output.isEmpty();copy.setEnabled(hasOutput);read.setEnabled(hasOutput);
        long now=SystemClock.uptimeMillis();if(available&&outputCount!=r.output.size()&&(!host.running()||now-lastOutput>=250)){outputCount=r.output.size();lastOutput=now;String text=r.outputText();preview.setText(!hasOutput?"Il risultato apparirà qui.":text.isEmpty()?"Stampa vuota":tail(text,2000));preview.post(()->{if(!closed&&preview.getLayout()!=null)preview.scrollTo(0,Math.max(0,preview.getLayout().getHeight()-preview.getHeight()+preview.getTotalPaddingTop()+preview.getTotalPaddingBottom()));});}
        if(!available&&outputCount!=-2){outputCount=-2;preview.setText("Il risultato apparirà qui.");}
        String description=!host.valid()?"Il progetto è cambiato. Riapri il codice per continuare.":!ready?"Preparo il codice collegato alla prova…":r==null?"Avvia una prova per inserire i valori qui.":host.description();String label=notice==null?description:notice;if(!TextUtils.equals(state.getText(),label))state.setText(label);state.setTextColor(available&&r.state==FlowEngine.State.ERROR?Ui.danger():Ui.muted());
        keyboardLayout();
    }
    void message(String message){if(!closed)state.setText(message);}
    void focusInput(){if(canSend())input.requestFocus();}
    void pause(){if(!closed){host.pause();notice=null;refresh(ready);}}
    private void submit(){
        if(!canSend()||SystemClock.uptimeMillis()-lastSend<250)return;FlowEngine.Runner r=observed;long revision=r.consoleLog.revision();host.submit(input.getText().toString());boolean accepted=r.consoleLog.revision()!=revision;
        if(accepted){lastSend=SystemClock.uptimeMillis();notice=null;if(r.state!=FlowEngine.State.WAITING)Ui.dismissKeyboard(input);}else{input.setError(r.errorMessage==null?r.lastMessage:r.errorMessage);input.requestFocus();}refresh(ready);
    }
    private String fullOutput(){FlowEngine.Runner r=host.runner();return r==null?"":r.outputText();}
    private void copyResult(){if(!canAct()||host.runner().output.isEmpty())return;host.pause();try{clipboardWrite.accept(fullOutput());notice="Risultato copiato.";}catch(RuntimeException error){notice="Appunti non disponibili. Apri il risultato per salvarlo.";}refresh(ready);}
    private Ui.Sheet child(String title,String subtitle){Ui.Sheet s=new Ui.Sheet(owner,title,subtitle);children.add(s);s.dialog.setOnDismissListener(d->children.remove(s));return s;}
    private EditText readOnly(String text,String name){EditText view=Ui.edit(owner,text,name,true);view.setKeyListener(null);view.setTextIsSelectable(true);view.setTypeface(Typeface.MONOSPACE);view.setMinLines(4);view.setMaxLines(12);return view;}
    private void openResult(){
        if(!canAct()||host.runner().output.isEmpty())return;host.pause();final String text=fullOutput();Ui.Sheet s=child("Risultato della prova","Testo completo, inclusi spazi e righe vuote.");s.body.addView(readOnly(text,"Risultato completo"),new LinearLayout.LayoutParams(-1,-2));TextView feedback=Ui.note(owner,"");s.body.addView(feedback);
        s.action("Copia risultato",null,0,()->{try{clipboardWrite.accept(text);feedback.setText("Risultato copiato.");}catch(RuntimeException error){feedback.setText("Appunti non disponibili. Usa Salva risultato.");}});
        s.primary("Salva risultato .txt","save",()->{if(!closed&&host.valid())host.save(text);});s.show();
    }
    private void openInputError(){
        if(!canSend()||input.getError()==null)return;
        final FlowEngine.Runner expected=observed;final long expectedGeneration=generation;final String request=waitingKey,value=input.getText().toString(),explanation=input.getError().toString();final int start=input.getSelectionStart(),end=input.getSelectionEnd();
        Ui.dismissKeyboard(input);Ui.Sheet detail=child("Valore non valido",null);TextView message=Ui.text(owner,explanation,16,false);message.setTextIsSelectable(true);message.setTag("source-run-error-detail");detail.body.addView(message);
        detail.dialog.setOnDismissListener(d->{children.remove(detail);if(canSend()&&observed==expected&&expected.generation()==expectedGeneration&&Objects.equals(waitingKey,request)&&input.getText().toString().equals(value)&&start>=0&&end>=0)input.setSelection(Math.min(start,input.length()),Math.min(end,input.length()));});detail.show();
    }
    private void openQuestion(){if(!canSend())return;host.pause();Ui.Sheet s=child("Valore per "+observed.inputVariable,null);TextView text=Ui.note(owner,observed.inputPrompt);text.setTextIsSelectable(true);s.body.addView(text);s.show();}
    private void keyboardLayout(){
        if(closed)return;android.graphics.Rect frame=new android.graphics.Rect();sheet.root.getWindowVisibleDisplayFrame(frame);boolean next=owner.getResources().getConfiguration().orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE&&frame.height()<dp(330);
        boolean ime=owner.getResources().getDisplayMetrics().heightPixels-frame.height()>dp(100);
        if(android.os.Build.VERSION.SDK_INT>=30){WindowInsets insets=sheet.dialog.getWindow().getDecorView().getRootWindowInsets();ime=insets!=null&&insets.isVisible(WindowInsets.Type.ime());next=next&&ime;}
        sheet.hideHeadingForInput(ime&&frame.height()<dp(200));
        CharSequence error=input.getError();boolean showError=next&&ime&&canSend()&&error!=null&&error.length()>0;inputError.setVisibility(showError?View.VISIBLE:View.GONE);inputError.setContentDescription(showError?"Leggi errore: "+error:"Leggi errore");
        if(compact!=next){transition();compact=next;Ui.pad(input,16,compact?6:14,16,compact?6:14);input.setMinimumHeight(dp(compact?52:54));placeInputTitle();sheet.compactHeader(compact||owner.getResources().getConfiguration().orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE&&owner.getResources().getConfiguration().screenHeightDp<500);sheet.scroll.setVisibility(compact?View.GONE:View.VISIBLE);sourceSave.setVisibility(compact?View.GONE:View.VISIBLE);inputTitle.setVisibility(mode==1?View.VISIBLE:View.GONE);updateInputTitle();}
    }
    private void updateInputTitle(){if(observed==null||observed.state!=FlowEngine.State.WAITING)return;String variable=observed.inputVariable==null?"Valore":observed.inputVariable;inputTitle.setText(compact?variable:"Valore per "+variable);inputTitle.setContentDescription("Valore per "+variable);inputTitle.setTooltipText("Valore per "+variable);}
    /** Keep the existing label in the input row while the IME leaves little height. */
    private void placeInputTitle(){
        ViewGroup parent=(ViewGroup)inputTitle.getParent();if(parent!=null)parent.removeView(inputTitle);
        if(compact){inputTitle.setMaxWidth(dp(132));inputTitle.setMaxLines(1);inputTitle.setEllipsize(TextUtils.TruncateAt.MIDDLE);inputTitle.setGravity(Gravity.CENTER_VERTICAL|Gravity.START);Ui.pad(inputTitle,0,0,8,0);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-2,-2);p.gravity=Gravity.CENTER_VERTICAL;inputArea.addView(inputTitle,0,p);}
        else{inputTitle.setMaxWidth(Integer.MAX_VALUE);inputTitle.setMaxLines(2);inputTitle.setEllipsize(TextUtils.TruncateAt.END);inputTitle.setGravity(Gravity.TOP|Gravity.START);Ui.pad(inputTitle,0,5,0,4);sheet.footer.addView(inputTitle,0,new LinearLayout.LayoutParams(-1,-2));}
    }
    static String prefix(String text,int maximum){if(text==null)return "";if(text.length()<=maximum)return text;int end=maximum;if(end>0&&Character.isHighSurrogate(text.charAt(end-1)))end--;return text.substring(0,end)+"…";}
    static String tail(String text,int maximum){if(text.length()<=maximum)return text;int start=text.length()-maximum;if(start<text.length()&&Character.isLowSurrogate(text.charAt(start)))start++;return "…"+text.substring(start);}
    private void closeChildren(){for(Ui.Sheet child:new ArrayList<>(children))child.dismissNow();children.clear();}
    void close(){if(closed)return;closed=true;closeChildren();if(sheet.root.getViewTreeObserver().isAlive())sheet.root.getViewTreeObserver().removeOnGlobalLayoutListener(keyboard);android.transition.TransitionManager.endTransitions(sheet.root);Ui.dismissKeyboard(input);}
}
