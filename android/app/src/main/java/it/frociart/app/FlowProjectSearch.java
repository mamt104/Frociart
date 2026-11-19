package it.frociart.app;

import android.app.*;
import android.graphics.*;
import android.os.*;
import android.text.*;
import android.text.style.*;
import android.view.*;
import android.view.inputmethod.*;
import android.widget.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.Consumer;

/** A bounded, recycled native list; queries run outside the UI thread. */
final class FlowProjectSearch implements AutoCloseable {
    static final class State {
        String text="";boolean matchCase,wholeWord,currentOnly,expanded;int start,end;
        void save(Bundle out){out.putString("search_query",text);out.putBoolean("search_case",matchCase);out.putBoolean("search_word",wholeWord);out.putBoolean("search_current",currentOnly);out.putBoolean("search_expanded",expanded);out.putInt("search_start",start);out.putInt("search_end",end);}
        static State read(Bundle in){if(in==null||!in.containsKey("search_query"))return null;State s=new State();s.text=in.getString("search_query","");s.matchCase=in.getBoolean("search_case");s.wholeWord=in.getBoolean("search_word");s.currentOnly=in.getBoolean("search_current");s.expanded=in.getBoolean("search_expanded");s.start=in.getInt("search_start");s.end=in.getInt("search_end");return s;}
    }
    private final Activity owner;private final Ui.Sheet sheet;private final FlowSearch index;private final String function;
    private final Consumer<FlowSearch.Hit> locate;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final ExecutorService worker=Executors.newSingleThreadExecutor();
    private Future<?> pending;private int generation;private boolean closed;
    private final EditText query;private final CheckBox matchCase,wholeWord,currentOnly;private final TextView status;
    private final LinearLayout filters;private final Ui.Button options;
    private final ListView list;private List<FlowSearch.Hit> hits=Collections.emptyList();private final BaseAdapter adapter;
    private final Runnable search=this::search;
    FlowProjectSearch(Activity owner,Ui.Sheet sheet,FlowSearch index,String function,Consumer<FlowSearch.Hit> locate){
        this.owner=owner;this.sheet=sheet;this.index=index;this.function=function;this.locate=locate;
        sheet.scroll.setVisibility(View.GONE);
        LinearLayout content=Ui.column(owner);sheet.root.addView(content,sheet.root.indexOfChild(sheet.footer),new LinearLayout.LayoutParams(-1,0,1));
        LinearLayout queryRow=Ui.row(owner);content.addView(queryRow,new LinearLayout.LayoutParams(-1,-2));
        LinearLayout fields=Ui.column(owner);Ui.BoundedScroll fieldsScroll=new Ui.BoundedScroll(owner);fieldsScroll.setTag("search-fields");fieldsScroll.setVerticalScrollBarEnabled(true);fieldsScroll.maxHeight=(int)(owner.getResources().getDisplayMetrics().heightPixels*.25f);fieldsScroll.addView(fields,new ScrollView.LayoutParams(-1,-2));content.addView(fieldsScroll,new LinearLayout.LayoutParams(-1,-2));
        query=Ui.edit(owner,"","Testo, variabili, note o rami",false);query.setTag("project-search-query");query.setContentDescription("Cerca nel progetto");query.setFilters(new InputFilter[]{new InputFilter.LengthFilter(FlowSearch.MAX_QUERY)});query.setImeOptions(EditorInfo.IME_ACTION_SEARCH|EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        queryRow.addView(query,new LinearLayout.LayoutParams(0,-2,1));queryRow.addView(new View(owner),Ui.lp(owner,8,1));
        filters=Ui.column(owner);filters.setVisibility(View.GONE);
        options=new Ui.Button(owner,"","settings",0);options.setContentDescription("Opzioni di ricerca");options.setTag("search-options");options.setOnClickListener(v->{boolean opening=filters.getVisibility()!=View.VISIBLE;if(Ui.animations()){android.transition.AutoTransition transition=new android.transition.AutoTransition();transition.setDuration(240);transition.setInterpolator(Ui.SPRING);android.transition.TransitionManager.beginDelayedTransition(content,transition);}filters.setVisibility(opening?View.VISIBLE:View.GONE);options.setChosen(opening);options.setContentDescription(opening?"Nascondi opzioni di ricerca":"Opzioni di ricerca");});queryRow.addView(options,Ui.lp(owner,48,48));fields.addView(filters,new LinearLayout.LayoutParams(-1,-2));
        matchCase=filter(filters,"Maiuscole/minuscole","search-match-case");wholeWord=filter(filters,"Parola intera","search-whole-word");currentOnly=filter(filters,"Solo questo diagramma","search-current-only");
        status=Ui.note(owner,"Scrivi il testo da cercare nel progetto.");status.setTag("project-search-status");Ui.pad(status,0,8,0,8);content.addView(status,new LinearLayout.LayoutParams(-1,-2));
        content.addOnLayoutChangeListener((v,l,t,r,b,ol,ot,or,ob)->{int remaining=Math.max(0,b-t-queryRow.getHeight()-status.getHeight());int limit=(int)(remaining*.58f);if(fieldsScroll.maxHeight!=limit){fieldsScroll.maxHeight=limit;fieldsScroll.requestLayout();}});
        list=new ListView(owner);list.setTag("project-search-results");list.setContentDescription("Risultati della ricerca");list.setDivider(null);list.setCacheColorHint(Color.TRANSPARENT);list.setSelector(new android.graphics.drawable.ColorDrawable(Ui.tint()));
        adapter=new BaseAdapter(){public int getCount(){return hits.size();}public Object getItem(int at){return hits.get(at);}public long getItemId(int at){return at;}
            public View getView(int at,View recycled,ViewGroup parent){LinearLayout row;TextView context,snippet;
                if(recycled instanceof LinearLayout){row=(LinearLayout)recycled;context=(TextView)row.getChildAt(0);snippet=(TextView)row.getChildAt(1);}else{row=Ui.column(owner);Ui.pad(row,12,12,12,12);row.setMinimumHeight(Ui.dp(owner,76));context=Ui.text(owner,"",13,true);row.addView(context,new LinearLayout.LayoutParams(-1,-2));snippet=Ui.text(owner,"",15,false);snippet.setMaxLines(4);snippet.setEllipsize(TextUtils.TruncateAt.END);row.addView(snippet,new LinearLayout.LayoutParams(-1,-2));}
                FlowSearch.Hit hit=hits.get(at);context.setText(hit.context());context.setTextColor(Ui.accentInk());SpannableString text=new SpannableString(hit.snippet);int end=Math.min(text.length(),hit.snippetStart+hit.length);if(end>hit.snippetStart){text.setSpan(new StyleSpan(Typeface.BOLD),hit.snippetStart,end,Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);text.setSpan(new ForegroundColorSpan(Ui.accentInk()),hit.snippetStart,end,Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);}snippet.setText(text);row.setContentDescription(hit.toString());return row;
            }};
        list.setAdapter(adapter);content.addView(list,new LinearLayout.LayoutParams(-1,0,1));list.setOnItemClickListener((parent,view,at,id)->{if(closed||at>=hits.size())return;FlowSearch.Hit hit=hits.get(at);Ui.dismissKeyboard(query);sheet.dismiss();locate.accept(hit);});
        query.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int a,int b,int c){schedule();}public void afterTextChanged(Editable e){}});
        query.setOnEditorActionListener((v,action,event)->{if(action==EditorInfo.IME_ACTION_SEARCH){handler.removeCallbacks(search);search();Ui.dismissKeyboard(query);return true;}return false;});
        sheet.dialog.setOnKeyListener((dialog,key,event)->{if(key==KeyEvent.KEYCODE_F&&event.isCtrlPressed()){if(event.getAction()==KeyEvent.ACTION_DOWN)query.requestFocus();return true;}return false;});
    }
    private CheckBox filter(LinearLayout parent,String title,String tag){CheckBox box=new CheckBox(owner);box.setText(title);box.setTextColor(Ui.ink());box.setTag(tag);box.setMinHeight(Ui.dp(owner,48));box.setButtonTintList(android.content.res.ColorStateList.valueOf(Ui.accentInk()));parent.addView(box,new LinearLayout.LayoutParams(-1,-2));box.setOnCheckedChangeListener((b,c)->schedule());return box;}
    void show(){sheet.show();Window window=sheet.dialog.getWindow();if(window!=null){Rect visible=new Rect();owner.getWindow().getDecorView().getWindowVisibleDisplayFrame(visible);int height=owner.getResources().getDisplayMetrics().heightPixels;if(Build.VERSION.SDK_INT>=30){WindowMetrics metrics=owner.getWindowManager().getCurrentWindowMetrics();Insets bars=metrics.getWindowInsets().getInsetsIgnoringVisibility(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());height=metrics.getBounds().height()-bars.top-bars.bottom;}else if(visible.height()>0)height=Math.min(height,visible.height());window.setLayout(window.getAttributes().width,Math.max(Ui.dp(owner,240),(int)(height*.92f)));window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);}query.requestFocus();}
    State snapshot(){State s=new State();s.text=query.getText().toString();s.matchCase=matchCase.isChecked();s.wholeWord=wholeWord.isChecked();s.currentOnly=currentOnly.isChecked();s.expanded=filters.getVisibility()==View.VISIBLE;s.start=Math.max(0,query.getSelectionStart());s.end=Math.max(s.start,query.getSelectionEnd());return s;}
    void restore(State s){if(s==null||closed)return;matchCase.setChecked(s.matchCase);wholeWord.setChecked(s.wholeWord);currentOnly.setChecked(s.currentOnly);filters.setVisibility(s.expanded?View.VISIBLE:View.GONE);options.setChosen(s.expanded);options.setContentDescription(s.expanded?"Nascondi opzioni di ricerca":"Opzioni di ricerca");query.setText(s.text);query.setSelection(Math.min(s.start,query.length()),Math.min(Math.max(s.start,s.end),query.length()));}
    private void schedule(){if(closed)return;generation++;handler.removeCallbacks(search);if(pending!=null){pending.cancel(true);pending=null;}hits=Collections.emptyList();list.animate().cancel();list.setAlpha(1);list.setTranslationY(0);adapter.notifyDataSetChanged();status.setText(FlowSearch.blank(query.getText().toString())?"Scrivi il testo da cercare nel progetto.":"Ricerca…");if(!FlowSearch.blank(query.getText().toString()))handler.postDelayed(search,160);}
    private void search(){if(closed)return;int request=++generation;if(pending!=null)pending.cancel(true);String text=query.getText().toString();boolean exact=matchCase.isChecked(),word=wholeWord.isChecked(),here=currentOnly.isChecked();
        pending=worker.submit(()->{try{FlowSearch.Results found=index.find(text,exact,word,here,function,()->Thread.currentThread().isInterrupted());handler.post(()->{if(closed||generation!=request)return;hits=found.hits;adapter.notifyDataSetChanged();status.setText(found.summary());list.setSelection(0);if(!hits.isEmpty())Ui.enter(list,0);});}catch(CancellationException ignored){}});
    }
    @Override public void close(){if(closed)return;closed=true;generation++;handler.removeCallbacksAndMessages(null);if(pending!=null)pending.cancel(true);worker.shutdownNow();}
}
