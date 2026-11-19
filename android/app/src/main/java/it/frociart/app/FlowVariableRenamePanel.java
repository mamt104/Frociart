package it.frociart.app;
import android.app.Activity;
import android.os.Bundle;
import android.text.*;
import android.view.*;
import android.widget.*;
import java.util.*;
import java.util.function.Consumer;
import static it.frociart.app.FlowModel.*;

/** Explicit preview and one commit. Draft/reading state survives rotation. */
final class FlowVariableRenamePanel implements AutoCloseable {
 static final class State {
  String projectId,scope,oldName="",newName="";int start,end,page,scroll,detail=-1;boolean preview,picker;
  void save(Bundle out){Bundle b=new Bundle();b.putString("project",projectId);b.putString("scope",scope);b.putString("old",oldName);b.putString("new",newName);b.putInt("start",start);b.putInt("end",end);b.putInt("page",page);b.putInt("scroll",scroll);b.putInt("detail",detail);b.putBoolean("preview",preview);b.putBoolean("picker",picker);out.putBundle("variable_rename",b);}
  static State read(Bundle out){if(out==null)return null;Bundle b=out.getBundle("variable_rename");if(b==null)return null;State state=new State();state.projectId=b.getString("project");state.scope=b.getString("scope");state.oldName=b.getString("old","");state.newName=b.getString("new","");state.start=b.getInt("start");state.end=b.getInt("end");state.page=b.getInt("page");state.scroll=b.getInt("scroll");state.detail=b.getInt("detail",-1);state.preview=b.getBoolean("preview");state.picker=b.getBoolean("picker");return state;}
 }
 final Ui.Sheet sheet;final EditText newName;final Ui.Button variable,preview,apply;final TextView status;
 private final Activity owner;private final Document source;private final String projectId,scope;private final Consumer<FlowVariableRename.Plan> commit;private final List<String> names;
 private final LinearLayout changes;private final Ui.Button previous,next;private final TextView pageLabel;
 private String oldName="";private int page,detail=-1;private boolean closed,pickerOpen;private Ui.Sheet child;private FlowVariableRename.Plan plan;
 FlowVariableRenamePanel(Activity owner,Ui.Sheet sheet,Document project,String projectId,String scope,Consumer<FlowVariableRename.Plan> commit){
  this.owner=owner;this.sheet=sheet;source=project.copy();this.projectId=projectId;this.scope=scope;this.commit=commit;names=FlowVariableRename.names(source,scope);
  Function function=FlowFunctionEditing.find(source,scope);sheet.paragraph("Foglio: "+(function==null?"Principale":function.name));
  sheet.paragraph("Aggiorna i riferimenti nel foglio. Testi tra virgolette, note e altre funzioni restano invariati. Annulla ripristina tutta la modifica.");
  label("Variabile");variable=new Ui.Button(owner,"Scegli la variabile","chevron",0);variable.setContentDescription("Variabile da rinominare nel foglio");variable.setOnClickListener(v->chooseVariable());sheet.body.addView(variable,new LinearLayout.LayoutParams(-1,-2));
  label("Nuovo nome");newName=Ui.edit(owner,"","Nuovo nome della variabile",false);newName.setContentDescription("Nuovo nome della variabile");newName.setTag("variable-rename-name");newName.setFilters(new InputFilter[]{new InputFilter.LengthFilter(64)});sheet.body.addView(newName,new LinearLayout.LayoutParams(-1,-2));
  // The IME can resize the scroll viewport after Android has already revealed
  // the focused field. Reveal it again at that boundary, without an idle loop.
  sheet.scroll.addOnLayoutChangeListener((view,l,t,r,b,oldL,oldT,oldR,oldB)->{if(b-t!=oldB-oldT&&newName.hasFocus())newName.post(()->{if(!closed&&sheet.dialog.isShowing()&&newName.hasFocus())newName.requestRectangleOnScreen(new android.graphics.Rect(0,0,newName.getWidth(),newName.getHeight()),false);});});
  preview=new Ui.Button(owner,"Mostra modifiche","eye",0);preview.setOnClickListener(v->prepare());sheet.body.addView(preview,new LinearLayout.LayoutParams(-1,-2));
  status=paragraph(sheet,"Scegli il nuovo nome, poi controlla l'anteprima.");status.setContentDescription("Esito dell'anteprima della rinomina");
  changes=Ui.column(owner);sheet.body.addView(changes,new LinearLayout.LayoutParams(-1,-2));
  LinearLayout paging=Ui.row(owner);previous=new Ui.Button(owner,"Indietro",null,0);next=new Ui.Button(owner,"Avanti",null,0);pageLabel=Ui.note(owner,"");previous.setOnClickListener(v->{page=Math.max(0,page-8);renderChanges();});next.setOnClickListener(v->{page+=8;renderChanges();});paging.addView(previous,new LinearLayout.LayoutParams(0,-2,1));paging.addView(next,new LinearLayout.LayoutParams(0,-2,1));sheet.body.addView(paging);sheet.body.addView(pageLabel);
  apply=new Ui.Button(owner,"Rinomina","check",1);apply.setOnClickListener(v->{if(plan==null)return;try{commit.accept(plan);sheet.dismiss();}catch(IllegalArgumentException error){status.setText(error.getMessage());status.setTextColor(Ui.danger());}});apply.setEnabled(false);sheet.footer.addView(apply,new LinearLayout.LayoutParams(-1,-2));sheet.scroll.maxHeight=(int)(owner.getResources().getDisplayMetrics().heightPixels*.61f);
  newName.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int st,int c,int a){}public void onTextChanged(CharSequence s,int st,int before,int count){invalidatePreview();}public void afterTextChanged(Editable text){}});
  if(!names.isEmpty()){oldName=names.get(0);variable.setText(oldName);newName.setText(oldName);}variable.setEnabled(!names.isEmpty());preview.setEnabled(!names.isEmpty());renderChanges();
 }
 private void label(String text){TextView label=Ui.text(owner,text,14,true);Ui.pad(label,0,12,0,6);sheet.body.addView(label);}
 private TextView paragraph(Ui.Sheet target,String text){TextView view=Ui.note(owner,text);target.body.addView(view);Ui.gap(target.body,14);return view;}
 private void invalidatePreview(){plan=null;apply.setEnabled(false);page=0;status.setTextColor(Ui.muted());status.setText("Scegli il nuovo nome, poi controlla l'anteprima.");renderChanges();}
 void prepare(){
  if(closed)return;plan=null;apply.setEnabled(false);page=0;
  try{plan=FlowVariableRename.prepare(source,scope,oldName,newName.getText().toString().trim());status.setTextColor(Ui.muted());status.setText(plan.summary());apply.setEnabled(true);}
  catch(IllegalArgumentException error){status.setTextColor(Ui.danger());status.setText(error.getMessage());}
  renderChanges();
 }
 private void renderChanges(){
  changes.removeAllViews();int count=plan==null?0:plan.changes.size();page=Math.max(0,Math.min(page,Math.max(0,(count-1)/8*8)));int end=Math.min(count,page+8);
  for(int i=page;i<end;i++){final int index=i;FlowVariableRename.Change change=plan.changes.get(i);Node node=FlowFunctionEditing.graph(source,scope).find(change.nodeId);Ui.Choice row=new Ui.Choice(owner);row.bind(node==null?change.field:kindName(node.kind)+" · "+change.field,"Prima: "+shortText(change.before)+"\nDopo: "+shortText(change.after),Ui.accentInk());row.setOnClickListener(v->showChange(index));changes.addView(row,new LinearLayout.LayoutParams(-1,-2));Ui.gap(changes,6);}
  previous.setVisibility(count>8?View.VISIBLE:View.GONE);next.setVisibility(count>8?View.VISIBLE:View.GONE);pageLabel.setVisibility(count>8?View.VISIBLE:View.GONE);previous.setEnabled(page>0);next.setEnabled(end<count);pageLabel.setText(count==0?"":(page+1)+"–"+end+" di "+count);if(count>0&&sheet.dialog.isShowing())Ui.enter(changes,0);
 }
 private static String shortText(String text){text=text.replace('\r',' ').replace('\n',' ');if(text.length()<=120)return text;int end=120;if(Character.isHighSurrogate(text.charAt(end-1)))end--;return text.substring(0,end)+"…";}
 private void closeChild(){if(child!=null){Ui.Sheet previous=child;child=null;previous.dismiss();}detail=-1;pickerOpen=false;}
 private void showChange(int index){
  if(plan==null||index<0||index>=plan.changes.size())return;closeChild();detail=index;FlowVariableRename.Change change=plan.changes.get(index);Ui.Sheet current=new Ui.Sheet(owner,change.field,"Modifica prevista · il progetto non è ancora cambiato");child=current;
  current.paragraph("Prima");TextView before=paragraph(current,change.before);before.setTypeface(android.graphics.Typeface.MONOSPACE);before.setTextIsSelectable(true);before.setContentDescription("Testo prima della rinomina");
  current.paragraph("Dopo");TextView after=paragraph(current,change.after);after.setTypeface(android.graphics.Typeface.MONOSPACE);after.setTextIsSelectable(true);after.setContentDescription("Testo dopo la rinomina");current.primary("Torna all'anteprima","back",current::dismiss);current.dialog.setOnDismissListener(d->{if(child==current){child=null;detail=-1;}});current.show();
 }
 private void chooseVariable(){
  if(names.isEmpty()||closed)return;closeChild();pickerOpen=true;Ui.Sheet current=new Ui.Sheet(owner,"Variabile da rinominare",names.size()+" variabili nel foglio");child=current;
  ListView list=new ListView(owner);list.setDivider(null);list.setCacheColorHint(Ui.surface());list.setVerticalScrollBarEnabled(false);list.setOverScrollMode(View.OVER_SCROLL_NEVER);list.setAdapter(new BaseAdapter(){public int getCount(){return names.size();}public String getItem(int p){return names.get(p);}public long getItemId(int p){return p;}public View getView(int p,View recycled,ViewGroup parent){TextView view=recycled instanceof TextView?(TextView)recycled:Ui.text(owner,"",16,true);view.setText(names.get(p));view.setTextColor(Ui.ink());Ui.pad(view,14,16,14,16);view.setBackgroundColor(Ui.surface());view.setMinHeight(Ui.dp(owner,52));return view;}});
  current.body.addView(list,new LinearLayout.LayoutParams(-1,Ui.dp(owner,Math.min(320,names.size()*64))));list.setOnItemClickListener((parent,view,position,id)->{oldName=names.get(position);variable.setText(oldName);newName.setText(oldName);newName.selectAll();current.dismiss();});current.dialog.setOnDismissListener(d->{if(child==current){child=null;pickerOpen=false;}});current.show();
 }
 State snapshot(){State state=new State();state.projectId=projectId;state.scope=scope;state.oldName=oldName;state.newName=newName.getText().toString();state.start=newName.getSelectionStart();state.end=newName.getSelectionEnd();state.page=page;state.scroll=sheet.scroll.getScrollY();state.detail=detail;state.preview=plan!=null;state.picker=pickerOpen;return state;}
 void restore(State state){if(state==null)return;if(names.contains(state.oldName)){oldName=state.oldName;variable.setText(oldName);}newName.setText(state.newName);if(state.preview)prepare();page=state.page;renderChanges();int start=Math.max(0,Math.min(state.start,newName.length())),end=Math.max(start,Math.min(state.end,newName.length()));newName.setSelection(start,end);sheet.scroll.post(()->sheet.scroll.scrollTo(0,state.scroll));if(state.picker)chooseVariable();else if(state.detail>=0)showChange(state.detail);}
 @Override public void close(){if(closed)return;closed=true;closeChild();}
}
