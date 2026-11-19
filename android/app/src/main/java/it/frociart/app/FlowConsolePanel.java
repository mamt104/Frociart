package it.frociart.app;

import android.app.*;import android.content.*;import android.graphics.Typeface;import android.graphics.drawable.GradientDrawable;
import android.os.*;import android.text.*;import android.view.*;import android.view.inputmethod.EditorInfo;import android.view.inputmethod.InputMethodManager;import android.widget.*;
import java.util.*;

/** Interactive, temporary native console. The runner remains the only execution controller. */
final class FlowConsolePanel {
    interface Host {
        FlowEngine.Runner runner();boolean running();String pending();void pending(String text);void submit(String text);
        void play();void step();void file();void save(String text);void locate(FlowConsoleLog.Entry entry);
    }
    final Ui.Sheet sheet;
    final ListView messages;final EditText plain,input;
    final Ui.Button messageMode,textMode,copy,save,clear,play,step,send,file;
    final TextView status,question;
    private final Activity owner;private final Host host;private final android.content.SharedPreferences preferences;
    private final FrameLayout viewer;private final TextView empty;private final LinearLayout inputArea;
    private final Handler handler=new Handler(Looper.getMainLooper());private final MessageAdapter adapter;
    private List<FlowConsoleLog.Entry> entries=Collections.emptyList();private FlowEngine.Runner observed;
    private long generation=-1,revision=-1,previousTail,arrived,lastSend=-1000,noticeUntil,noticeRevision=-1;
    private String waitingKey,notice;private boolean closed,synchronizing,textView;private Ui.Sheet details;
    java.util.function.Consumer<String> clipboardWrite=this::systemClipboard;
    private final Runnable tick=new Runnable(){public void run(){if(closed)return;refresh();handler.postDelayed(this,120);}};

    FlowConsolePanel(Activity owner,Ui.Sheet sheet,Host host,android.content.SharedPreferences preferences){
        this.owner=owner;this.sheet=sheet;this.host=host;this.preferences=preferences;textView=preferences.getBoolean("console_text",false);
        LinearLayout modes=Ui.row(owner);messageMode=button("Messaggi",null,0);textMode=button("Testo",null,0);modes.addView(messageMode,Ui.weight(owner,-2));modes.addView(textMode,Ui.weight(owner,-2));sheet.body.addView(modes);
        messageMode.setContentDescription("Mostra letture e stampe come messaggi");textMode.setContentDescription("Mostra la console come testo selezionabile");messageMode.setOnClickListener(v->mode(false));textMode.setOnClickListener(v->mode(true));
        LinearLayout actions=Ui.row(owner);copy=button("Copia",null,0);save=button("Salva .txt",null,0);clear=button("Svuota",null,0);if(owner.getResources().getConfiguration().fontScale>=1.3f)save.setLabel("Salva");for(Ui.Button b:new Ui.Button[]{copy,save,clear})actions.addView(b,Ui.weight(owner,-2));sheet.body.addView(actions);
        copy.setContentDescription("Copia la console, inclusi i valori inseriti");save.setContentDescription("Salva la console in testo, inclusi i valori inseriti");
        copy.setOnClickListener(v->copyAll());save.setOnClickListener(v->{FlowEngine.Runner r=host.runner();if(r!=null&&r.consoleLog.size()>0)host.save(r.consoleLog.toText());});clear.setOnClickListener(v->clearLog());
        viewer=new FrameLayout(owner);int screenDp=(int)(owner.getResources().getDisplayMetrics().heightPixels/owner.getResources().getDisplayMetrics().density);sheet.body.addView(viewer,new LinearLayout.LayoutParams(-1,dp(Math.max(160,Math.min(360,screenDp/3)))));
        messages=new ListView(owner);messages.setDivider(null);messages.setClipToPadding(false);messages.setContentDescription("Cronologia di letture e stampe");messages.setNestedScrollingEnabled(true);adapter=new MessageAdapter();messages.setAdapter(adapter);messages.setOnItemClickListener((p,v,position,id)->{if(position<entries.size())openMessage(entries.get(position));});viewer.addView(messages,new FrameLayout.LayoutParams(-1,-1));
        plain=Ui.edit(owner,"","Testo della console",true);plain.setKeyListener(null);plain.setTextIsSelectable(true);plain.setTypeface(Typeface.MONOSPACE);plain.setTextSize(14);plain.setHorizontallyScrolling(true);plain.setContentDescription("Cronologia completa conservata, in testo");viewer.addView(plain,new FrameLayout.LayoutParams(-1,-1));
        View.OnTouchListener ownScroll=(v,e)->{if(e.getActionMasked()==MotionEvent.ACTION_DOWN)v.getParent().requestDisallowInterceptTouchEvent(true);if(e.getActionMasked()==MotionEvent.ACTION_UP||e.getActionMasked()==MotionEvent.ACTION_CANCEL)v.getParent().requestDisallowInterceptTouchEvent(false);return false;};messages.setOnTouchListener(ownScroll);plain.setOnTouchListener(ownScroll);
        empty=Ui.note(owner,"Non ci sono messaggi.\nLe letture e le stampe compariranno qui.");empty.setGravity(Gravity.CENTER);Ui.pad(empty,16,20,16,20);viewer.addView(empty,new FrameLayout.LayoutParams(-1,-1));
        status=Ui.note(owner,"");Ui.pad(status,0,8,0,6);status.setTag("console-status");sheet.body.addView(status);
        inputArea=Ui.column(owner);LinearLayout heading=Ui.row(owner);question=Ui.text(owner,"",14,true);question.setMaxLines(2);question.setEllipsize(TextUtils.TruncateAt.END);heading.addView(question,Ui.weight(owner,-2));Ui.Button fullQuestion=new Ui.Button(owner,"","help",0);fullQuestion.setTag("console-question");fullQuestion.setContentDescription("Leggi la domanda completa del programma");fullQuestion.setOnClickListener(v->openQuestion());heading.addView(fullQuestion,Ui.lp(owner,48,48));inputArea.addView(heading);
        LinearLayout inputRow=Ui.row(owner);input=Ui.edit(owner,"","Valore",false);input.setTag("console-input");input.setImeOptions(EditorInfo.IME_ACTION_SEND|EditorInfo.IME_FLAG_NO_EXTRACT_UI);input.setFilters(new InputFilter[]{new InputFilter.LengthFilter(32768)});inputRow.addView(input,Ui.weight(owner,-2));send=new Ui.Button(owner,"","right",1);send.setTag("console-send");send.setContentDescription("Invia valore al programma");send.setOnClickListener(v->submit());inputRow.addView(send,Ui.lp(owner,52,52));Ui.inputSubmission(input,this::submit);inputArea.addView(inputRow);sheet.footer.addView(inputArea);
        input.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void afterTextChanged(Editable e){}public void onTextChanged(CharSequence s,int a,int b,int c){if(!synchronizing&&canSend())host.pending(s.toString());}});
        LinearLayout controls=Ui.row(owner);step=button("Avanti","step",0);play=button("Esegui","play",1);step.setTag("console-step");play.setTag("console-play");step.setOnClickListener(v->{host.step();refresh();});play.setOnClickListener(v->{host.play();refresh();});controls.addView(step,Ui.weight(owner,-2));controls.addView(play,Ui.weight(owner,-2));sheet.footer.addView(controls);
        file=button("Scegli file…","folder",0);file.setOnClickListener(v->{host.file();refresh();});sheet.footer.addView(file,new LinearLayout.LayoutParams(-1,-2));
        applyMode();sheet.root.addOnAttachStateChangeListener(new View.OnAttachStateChangeListener(){public void onViewAttachedToWindow(View v){handler.removeCallbacks(tick);handler.post(tick);}public void onViewDetachedFromWindow(View v){handler.removeCallbacks(tick);}});
    }
    private Ui.Button button(String text,String icon,int tone){Ui.Button b=new Ui.Button(owner,text,owner.getResources().getConfiguration().fontScale>=1.3f?null:icon,tone);b.setSingleLine(false);b.setMaxLines(3);b.setEllipsize(null);b.setMinimumHeight(dp(48));Ui.pad(b,8,8,8,8);return b;}
    private int dp(float value){return Ui.dp(owner,value);}
    private void mode(boolean value){if(textView==value)return;textView=value;preferences.edit().putBoolean("console_text",value).apply();Ui.enter(viewer,0);applyMode();}
    private void applyMode(){plain.setVisibility(textView?View.VISIBLE:View.GONE);messages.setVisibility(textView?View.GONE:View.VISIBLE);messageMode.setChosen(!textView);textMode.setChosen(textView);if(textView)plain.setText(exportText());}
    String exportText(){FlowEngine.Runner r=host.runner();return r==null?"":r.consoleLog.toText();}
    private void clearLog(){FlowEngine.Runner r=host.runner();if(r==null)return;r.consoleLog.clear();Ui.enter(viewer,0);refresh();}
    private boolean canSend(){FlowEngine.Runner r=host.runner();return !closed&&r!=null&&r==observed&&r.generation()==generation&&r.state==FlowEngine.State.WAITING;}
    private void submit(){
        if(!canSend()||SystemClock.uptimeMillis()-lastSend<250)return;FlowEngine.Runner r=host.runner();long before=r.consoleLog.revision();host.submit(input.getText().toString());boolean accepted=r.consoleLog.revision()!=before;
        if(accepted){lastSend=SystemClock.uptimeMillis();input.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK);if(r.state!=FlowEngine.State.WAITING)Ui.dismissKeyboard(input);}else{input.setError(r.errorMessage==null?r.lastMessage:r.errorMessage);input.requestFocus();}refresh();
    }
    void focusPendingInput(){if(canSend()&&inputArea.getVisibility()==View.VISIBLE){input.requestFocus();input.post(()->{if(canSend()&&sheet.dialog.isShowing()&&input.isAttachedToWindow())((InputMethodManager)owner.getSystemService(Context.INPUT_METHOD_SERVICE)).showSoftInput(input,InputMethodManager.SHOW_IMPLICIT);});}}
    private void notice(String text,boolean tied){notice=text;noticeUntil=SystemClock.uptimeMillis()+3000;noticeRevision=tied&&host.runner()!=null?host.runner().consoleLog.revision():-1;status.setText(text);}
    private void copyAll(){FlowEngine.Runner r=host.runner();if(r==null||r.consoleLog.size()==0)return;try{clipboard(r.consoleLog.toText());notice("Console copiata, inclusi i valori inseriti.",true);}catch(RuntimeException e){notice("Appunti non disponibili. Usa Salva .txt oppure riprova.",false);}}
    private void clipboard(String text){clipboardWrite.accept(text);}
    private void systemClipboard(String text){((android.content.ClipboardManager)owner.getSystemService(Context.CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("Console Frociart",text));}
    void refresh(){
        if(closed)return;FlowEngine.Runner r=host.runner();boolean session=r!=observed||r!=null&&generation!=r.generation();observed=r;generation=r==null?-1:r.generation();FlowConsoleLog log=r==null?null:r.consoleLog;long nextRevision=log==null?-1:log.revision();
        if(session||revision!=nextRevision){
            revision=nextRevision;boolean bottom=entries.isEmpty()||messages.getLastVisiblePosition()>=entries.size()-2;int first=messages.getFirstVisiblePosition(),offset=messages.getChildAt(0)==null?0:messages.getChildAt(0).getTop();long anchor=first<entries.size()&&first>=0?entries.get(first).sequence:0;previousTail=entries.isEmpty()?0:entries.get(entries.size()-1).sequence;entries=log==null?Collections.emptyList():log.snapshot();arrived=SystemClock.uptimeMillis();adapter.notifyDataSetChanged();
            if(!entries.isEmpty()){if(bottom){int last=entries.size()-1;messages.post(()->{if(!closed&&adapter.getCount()>0){int end=adapter.getCount()-1;if(Ui.animations()&&end<last+8&&end-last<8)messages.smoothScrollToPosition(end);else messages.setSelection(end);}});}else if(!session)messages.setSelectionFromTop((int)Math.max(0,Math.min(entries.size()-1,anchor-entries.get(0).sequence)),offset);}
            if(textView){int at=plain.getSelectionStart(),end=plain.getSelectionEnd(),scroll=plain.getScrollY();String next=exportText();boolean follow=plain.getLayout()==null||scroll+plain.getHeight()>=plain.getLayout().getHeight()-dp(24);plain.setText(next);int caret=Math.min(Math.max(0,at),plain.length()),tail=Math.min(Math.max(caret,end),plain.length());plain.setSelection(caret,tail);plain.post(()->{if(!closed)plain.scrollTo(plain.getScrollX(),follow&&plain.getLayout()!=null?Math.max(0,plain.getLayout().getHeight()-plain.getHeight()):scroll);});}
            if(session||noticeRevision>=0&&noticeRevision!=nextRevision)notice=null;
        }
        boolean waiting=r!=null&&r.state==FlowEngine.State.WAITING;String key=waiting?r.currentFunctionId+":"+r.currentNodeId+":"+r.steps:null;boolean nextQuestion=session||!Objects.equals(waitingKey,key);waitingKey=key;inputArea.setVisibility(waiting?View.VISIBLE:View.GONE);
        if(waiting){question.setText(r.inputPrompt);question.setContentDescription(r.inputPrompt);input.setHint(r.inputVariable);input.setContentDescription("Valore per "+r.inputVariable);if(nextQuestion||!input.hasFocus()&&!input.getText().toString().equals(host.pending())){synchronizing=true;try{input.setText(host.pending()==null?"":host.pending());if(nextQuestion)input.setError(null);}finally{synchronizing=false;}}if(nextQuestion)focusPendingInput();}
        send.setEnabled(waiting&&SystemClock.uptimeMillis()-lastSend>=250);empty.setVisibility(entries.isEmpty()?View.VISIBLE:View.GONE);copy.setEnabled(!entries.isEmpty());save.setEnabled(!entries.isEmpty());clear.setEnabled(!entries.isEmpty());
        boolean fileWait=r!=null&&r.state==FlowEngine.State.WAITING_FILE,done=r!=null&&(r.state==FlowEngine.State.COMPLETED||r.state==FlowEngine.State.ERROR);
        play.setLabel(host.running()?"Pausa":done?"Riprova":r!=null&&r.steps>0?"Riprendi":"Esegui");play.setEnabled(!waiting&&!fileWait);step.setEnabled(!host.running()&&!waiting&&!fileWait&&!done);file.setVisibility(fileWait?View.VISIBLE:View.GONE);
        String state=r==null?"Pronto":r.state==FlowEngine.State.ERROR?"Interrotto: "+r.errorMessage:r.state==FlowEngine.State.COMPLETED?"Completato":waiting?"In attesa di "+r.inputVariable:fileWait?"Scegli il file per continuare":host.running()?"In esecuzione":"In pausa";
        status.setText(notice!=null&&SystemClock.uptimeMillis()<noticeUntil?notice:state+" · "+entries.size()+" messaggi"+(log!=null&&log.discarded()>0?" · "+log.discarded()+" precedenti rimossi dal limite di memoria":""));
    }
    private void openQuestion(){FlowEngine.Runner r=host.runner();if(r==null||r.state!=FlowEngine.State.WAITING)return;showText("Domanda del programma",r.inputVariable,r.inputPrompt,null);}
    private void openMessage(FlowConsoleLog.Entry entry){FlowEngine.Runner r=host.runner();if(r==null||r.consoleLog.find(entry.sequence)!=entry)return;showText(entry.label(),entry.context()+(entry.textLimited?" · testo limitato":""),(entry.prompt.isEmpty()?"":entry.prompt+"\n\n")+entry.text,entry);}
    private void showText(String title,String subtitle,String value,FlowConsoleLog.Entry entry){
        if(details!=null)details.dismiss();Ui.Sheet detail=new Ui.Sheet(owner,title,subtitle);details=detail;EditText text=Ui.edit(owner,value,"Testo completo",true);text.setKeyListener(null);text.setTextIsSelectable(true);text.setTypeface(Typeface.MONOSPACE);text.setMinLines(3);text.setContentDescription("Messaggio completo della console");detail.body.addView(text);
        detail.action(entry==null?"Copia domanda":"Copia valore","copy",0,()->{try{clipboard(entry==null?value:entry.text);}catch(RuntimeException e){notice("Appunti non disponibili. Seleziona il testo e riprova.",false);}});
        if(entry!=null)detail.action("Vai al blocco","center",0,()->{FlowEngine.Runner r=host.runner();if(r!=null&&r.consoleLog.find(entry.sequence)==entry){detail.dismiss();sheet.dismiss();host.locate(entry);}});
        detail.primary("Chiudi","close",detail::dismiss);detail.dialog.setOnDismissListener(d->{if(details==detail)details=null;});detail.show();
    }
    void close(){closed=true;handler.removeCallbacks(tick);if(details!=null){details.dismiss();details=null;}}
    private static String preview(String value,int max){if(value.length()<=max)return value;if(max>0&&Character.isHighSurrogate(value.charAt(max-1)))max--;return value.substring(0,max)+"…";}
    private final class MessageAdapter extends BaseAdapter {
        public int getCount(){return entries.size();}public Object getItem(int position){return entries.get(position);}public long getItemId(int position){return entries.get(position).sequence;}public boolean hasStableIds(){return true;}
        public View getView(int position,View reused,ViewGroup parent){
            Holder holder=reused!=null?(Holder)reused.getTag():new Holder();FlowConsoleLog.Entry entry=entries.get(position);boolean fresh=holder.sequence!=entry.sequence;holder.sequence=entry.sequence;
            boolean isInput=entry.kind==FlowConsoleLog.EntryKind.INPUT;int color=isInput?(Ui.dark?0xFF75BEEC:0xFF388DBD):(Ui.dark?0xFFBC99FF:0xFF814BCE);holder.label.setText(entry.label());holder.label.setTextColor(color);holder.value.setText(entry.text.isEmpty()?"(testo vuoto)":preview(entry.text,1000));holder.context.setText(entry.context());
            LinearLayout.LayoutParams p=(LinearLayout.LayoutParams)holder.card.getLayoutParams();p.leftMargin=dp(isInput?24:0);p.rightMargin=dp(isInput?0:24);holder.card.setLayoutParams(p);GradientDrawable fill=Ui.round(Ui.surface(),18,owner);fill.setStroke(dp(1),android.graphics.Color.argb(110,android.graphics.Color.red(color),android.graphics.Color.green(color),android.graphics.Color.blue(color)));holder.card.setBackground(fill);
            holder.outer.setContentDescription(entry.label()+". "+preview(entry.text,512)+". "+entry.context()+". Tocca per leggere tutto.");holder.outer.animate().cancel();holder.outer.setAlpha(1);holder.outer.setTranslationY(0);if(fresh&&entry.sequence>previousTail&&SystemClock.uptimeMillis()-arrived<400&&Ui.animations())Ui.enter(holder.outer,0);return holder.outer;
        }
    }
    private final class Holder {
        final LinearLayout outer=Ui.column(owner),card=Ui.column(owner);final TextView label=Ui.text(owner,"",12,true),value=Ui.text(owner,"",16,false),context=Ui.note(owner,"");long sequence;
        Holder(){Ui.pad(outer,0,4,0,4);Ui.pad(card,12,10,12,10);label.setMaxLines(2);label.setEllipsize(TextUtils.TruncateAt.END);value.setTypeface(Typeface.MONOSPACE);value.setMaxLines(3);value.setEllipsize(TextUtils.TruncateAt.END);context.setMaxLines(1);context.setEllipsize(TextUtils.TruncateAt.END);Ui.pad(value,0,5,0,5);card.addView(label);card.addView(value);card.addView(context);outer.addView(card,new LinearLayout.LayoutParams(-1,-2));outer.setTag(this);}
    }
}
