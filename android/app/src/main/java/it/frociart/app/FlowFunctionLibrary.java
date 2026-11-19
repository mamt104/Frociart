package it.frociart.app;
import android.app.Activity;import android.text.*;import android.view.*;import android.widget.*;import java.util.Locale;
final class FlowFunctionLibrary {
 static Ui.Sheet show(Activity owner,EditText target){return show(owner,target,null,false);}
 static Ui.Sheet show(Activity owner,EditText target,FlowModel.Document project,boolean callsOnly){return show(owner,target,project,callsOnly,project!=null&&owner instanceof MainActivity?((MainActivity)owner).expressionScopeId():null,null);}
 static Ui.Sheet show(Activity owner,EditText target,FlowModel.Document project,boolean callsOnly,String functionId,String nodeId){
  int anchor=Math.max(0,target.getSelectionStart()),focus=Math.max(0,target.getSelectionEnd());
  final int start=Math.min(anchor,focus),end=Math.max(anchor,focus);final String original=target.getText().toString();
  final String contextNode=owner instanceof MainActivity?((MainActivity)owner).expressionEditorId():null;
  FlowExpressionLibrary.Catalog catalog=FlowExpressionLibrary.build(project,functionId,nodeId,nodeId==null?null:original,callsOnly,FlowExpressionLibrary.argumentsAt(original,start,end-start));
  Ui.Sheet sheet=new Ui.Sheet(owner,"Variabili e funzioni",(catalog.targetOnly?"Scegli la variabile da leggere":catalog.callsOnly?"Scegli la funzione da richiamare":"Trova cosa inserire nel blocco")+" · "+catalog.scopeName);
  EditText search=Ui.edit(owner,"","Cerca un nome o un’operazione",false);search.setTag("ExpressionLibrarySearch");search.setContentDescription("Cerca variabili, funzioni o operazioni");search.setFilters(new InputFilter[]{new InputFilter.LengthFilter(100)});sheet.root.addView(search,sheet.root.indexOfChild(sheet.scroll),new LinearLayout.LayoutParams(-1,-2));
  LinearLayout filters=Ui.row(owner);int[] group={0},page={0},generation={0};Ui.Button[] groups=new Ui.Button[3];String[] labels={"Tutto","Variabili","Funzioni"};Runnable[] refresh={null};
  for(int i=0;i<3;i++){final int g=i;Ui.Button button=new Ui.Button(owner,labels[i],null,0);button.setTag("ExpressionGroup"+i);button.setContentDescription(labels[i]);button.setSingleLine(false);groups[i]=button;filters.addView(button,new LinearLayout.LayoutParams(0,-2,1));button.setOnClickListener(v->{group[0]=g;page[0]=0;generation[0]++;refresh[0].run();});}
  if(!catalog.targetOnly&&!catalog.callsOnly)sheet.root.addView(filters,sheet.root.indexOfChild(sheet.scroll),new LinearLayout.LayoutParams(-1,-2));
  TextView count=Ui.note(owner,"");count.setTag("ExpressionLibraryCount");count.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);Ui.pad(count,2,7,2,7);sheet.root.addView(count,sheet.root.indexOfChild(sheet.scroll),new LinearLayout.LayoutParams(-1,-2));
  TextView status=Ui.note(owner,"");status.setTag("ExpressionLibraryStatus");status.setVisibility(View.GONE);status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);sheet.root.addView(status,sheet.root.indexOfChild(sheet.scroll),new LinearLayout.LayoutParams(-1,-2));
  LinearLayout results=Ui.column(owner);results.setTag("ExpressionLibraryResults");sheet.body.addView(results,new LinearLayout.LayoutParams(-1,-2));
  if(!catalog.targetOnly&&!catalog.callsOnly)sheet.action("Converti espressione Flowgorithm",null,0,()->convert(owner,target,project,sheet,start,end,original,()->project==null||!(owner instanceof MainActivity)||((MainActivity)owner).expressionContextCurrent(project,functionId,contextNode)));
  LinearLayout pagination=Ui.row(owner);Ui.Button previous=new Ui.Button(owner,"Precedenti",null,0),next=new Ui.Button(owner,"Successivi",null,0);previous.setTag("ExpressionPrevious");next.setTag("ExpressionNext");pagination.addView(previous,new LinearLayout.LayoutParams(0,-2,1));pagination.addView(next,new LinearLayout.LayoutParams(0,-2,1));sheet.footer.addView(pagination,new LinearLayout.LayoutParams(-1,-2));
  previous.setOnClickListener(v->{page[0]=Math.max(0,page[0]-1);generation[0]++;refresh[0].run();});next.setOnClickListener(v->{page[0]++;generation[0]++;refresh[0].run();});
  final boolean[] inserted={false};
  android.os.Handler delay=new android.os.Handler(owner.getMainLooper());
  refresh[0]=()->{
   delay.removeCallbacks(refresh[0]);final int ticket=generation[0];java.util.List<String[]> matches=FlowExpressionLibrary.filter(catalog,search.getText().toString(),group[0]);page[0]=Math.min(page[0],Math.max(0,(matches.size()-1)/12));final int from=page[0]*12,to=Math.min(matches.size(),from+12);
   for(int i=0;i<3;i++)groups[i].setChosen(group[0]==i);
   for(int i=0;i<results.getChildCount();i++)results.getChildAt(i).setEnabled(false);
   Runnable apply=()->{if(ticket!=generation[0])return;results.removeAllViews();
    for(int i=from;i<to;i++){String[] row=matches.get(i);Ui.Choice choice=new Ui.Choice(owner);String details=row[0]+" · "+row[2]+"\n"+(row[4].isEmpty()?"Inserisci: "+row[3]:row[3]+" → "+row[4]);choice.bind(row[1],details,Ui.accentInk());choice.setTag("ExpressionSuggestion:"+row[3]);choice.setOnClickListener(v->{
      if(ticket!=generation[0])return;
      if(!target.isAttachedToWindow()||!target.isEnabled()||!target.getText().toString().equals(original)||project!=null&&owner instanceof MainActivity&&!((MainActivity)owner).expressionContextCurrent(project,functionId,contextNode)){status.setVisibility(View.VISIBLE);status.setText("Il campo è cambiato. Chiudi e riapri Variabili e funzioni.");return;}
      if(!insert(target,start,end,row[3])){status.setVisibility(View.VISIBLE);status.setText("Il testo è troppo lungo: libera spazio prima di inserire.");return;}
      inserted[0]=true;sheet.dismiss();target.requestFocus();
    });results.addView(choice,new LinearLayout.LayoutParams(-1,-2));Ui.gap(results,8);}
    if(matches.isEmpty())results.addView(Ui.note(owner,catalog.targetOnly?"Nessuna variabile trovata. Leggi un dato o usa Dichiara nel foglio.":catalog.callsOnly?"Nessuna funzione trovata. Crea una funzione da Diagrammi e funzioni, oppure cambia ricerca.":"Nessun risultato. Cerca un nome o un’operazione, come testo o decimali."));
    count.setText(matches.isEmpty()?"0 suggerimenti":matches.size()==1?"1 suggerimento":(from+1)+"–"+to+" di "+matches.size()+" suggerimenti");pagination.setVisibility(matches.size()>12?View.VISIBLE:View.GONE);previous.setEnabled(page[0]>0);next.setEnabled(to<matches.size());sheet.scroll.scrollTo(0,0);results.setAlpha(Ui.animations()&&sheet.dialog.isShowing()?0:1);if(results.getAlpha()==0)results.animate().alpha(1).setDuration(110).start();
   };
   results.animate().cancel();if(Ui.animations()&&sheet.dialog.isShowing()){results.animate().alpha(0).setDuration(70).withEndAction(apply).start();}else apply.run();
  };
  search.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void onTextChanged(CharSequence s,int a,int b,int c){page[0]=0;generation[0]++;for(int i=0;i<results.getChildCount();i++)results.getChildAt(i).setEnabled(false);delay.removeCallbacks(refresh[0]);delay.postDelayed(refresh[0],120);}public void afterTextChanged(Editable s){}});
  sheet.dialog.setOnDismissListener(d->{generation[0]++;delay.removeCallbacks(refresh[0]);results.animate().cancel();if(!inserted[0]&&target.isAttachedToWindow()&&target.getText().toString().equals(original)&&(project==null||!(owner instanceof MainActivity)||((MainActivity)owner).expressionContextCurrent(project,functionId,contextNode))){target.requestFocus();target.setSelection(anchor,focus);}});refresh[0].run();sheet.show();sheet.dialog.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE|WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN);return sheet;
 }
 static Ui.Sheet convert(Activity owner,EditText target,FlowModel.Document project,Ui.Sheet library,int start,int end,String original,java.util.function.BooleanSupplier current){
  Ui.Sheet sheet=new Ui.Sheet(owner,"Converti espressione","Da Flowgorithm a Frociart.");
  sheet.paragraph("Incolla solo un’espressione, senza leggi, stampa o assegnazioni.");
  sheet.body.addView(Ui.text(owner,"Espressione di Flowgorithm",14,true));Ui.gap(sheet.body,8);
  String selected=start>=0&&end>=start&&end<=original.length()?original.substring(start,end):"";
  // Keep oversized input visible for correction; the parser rejects it instead of converting a truncated prefix.
  EditText input=Ui.edit(owner,selected,"Espressione di Flowgorithm",true);input.setTag("FlowgorithmExpressionInput");input.setFilters(new InputFilter[]{new InputFilter.LengthFilter(65536)});sheet.body.addView(input,new LinearLayout.LayoutParams(-1,-2));Ui.gap(sheet.body,16);
  sheet.body.addView(Ui.text(owner,"Anteprima per Frociart",14,true));Ui.gap(sheet.body,8);
  TextView preview=Ui.text(owner,"",15,false);preview.setTag("FlowgorithmExpressionPreview");preview.setContentDescription("Anteprima per Frociart");preview.setTypeface(android.graphics.Typeface.MONOSPACE);preview.setTextIsSelectable(true);sheet.body.addView(preview,new LinearLayout.LayoutParams(-1,-2));Ui.gap(sheet.body,12);
  TextView status=Ui.note(owner,"");status.setTag("FlowgorithmExpressionStatus");status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);sheet.body.addView(status);Ui.gap(sheet.body,12);
  sheet.paragraph("Le parentesi conservano le precedenze di Flowgorithm; i testi mantengono le barre inverse. Variabili e tipi si verificano nel flowchart.");
  final java.util.Map<String,FlowModel.Function> catalog=project==null?null:FlowFunctions.catalog(project);final String[] converted={null};
  sheet.primary("Inserisci espressione",null,()->{
   if(converted[0]==null)return;
   if(!target.isAttachedToWindow()||!target.isEnabled()||!target.getText().toString().equals(original)||!current.getAsBoolean()){status.setText("Il campo è cambiato. Riapri Variabili e funzioni dalla selezione aggiornata.");return;}
   if(!insert(target,start,end,converted[0])){status.setText("Il testo è troppo lungo: libera spazio prima di inserirlo.");return;}
   sheet.dismiss();library.dismiss();target.requestFocus();
  });
  Ui.Button apply=(Ui.Button)sheet.footer.getChildAt(sheet.footer.getChildCount()-1);apply.setTag("InsertConvertedExpression");
  Runnable refresh=()->{converted[0]=null;preview.setText("");apply.setEnabled(false);if(input.getText().toString().trim().isEmpty()){status.setText("Incolla o scrivi l’espressione da convertire.");return;}try{converted[0]=FlowgorithmImport.convertExpression(input.getText().toString(),catalog);preview.setText(converted[0]);status.setText("Conversione pronta. Inserisci sostituisce solo la selezione originale. Nessuna espressione è stata eseguita.");apply.setEnabled(true);}catch(IllegalArgumentException e){status.setText(e.getMessage());}};
  input.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void onTextChanged(CharSequence s,int a,int b,int c){refresh.run();}public void afterTextChanged(Editable s){}});refresh.run();sheet.show();sheet.dialog.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE|WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN);return sheet;
 }
 static boolean insert(EditText target,int start,int end,String snippet){if(target==null||snippet==null||start<0||end<start||end>target.length()||!target.isEnabled())return false;Editable text=target.getText();for(InputFilter f:target.getFilters()){CharSequence accepted=f.filter(snippet,0,snippet.length(),text,start,end);if(accepted!=null&&!accepted.toString().equals(snippet))return false;}text.replace(start,end,snippet);target.setSelection(start+snippet.length());return true;}
}
