package it.frociart.app;

import android.app.Activity;
import android.graphics.Typeface;
import android.view.View;
import android.widget.*;
import java.util.*;
import java.util.concurrent.Executor;
import java.util.function.*;

/** Export a separate immutable snapshot. No change to the project, runtime or history. */
final class FlowgorithmExportPanel {
 final Ui.Sheet sheet;
 final TextView status;
 final Ui.Button save;
 final LinearLayout types,issues;
 final EditText preview;
 FlowgorithmExport.Result result;
 private final Activity owner;
 private final FlowModel.Document snapshot;
 private final Executor worker;
 private final Map<String,Integer> choices=new LinkedHashMap<>();
 private final Map<String,Integer> colorChoices=new LinkedHashMap<>();
 private final Set<String> shownColors=new HashSet<>();
 private final Set<String> shown=new HashSet<>();
 private final BiConsumer<String,String> locate;
 private volatile int request;
 private List<FlowgorithmExport.TypeChoice> inventory;
 FlowModel.Document snapshot(){return snapshot.copy();}
 Map<String,Integer> selectedTypes(){return new LinkedHashMap<>(choices);}
 Map<String,Integer> selectedColors(){return new LinkedHashMap<>(colorChoices);}
 FlowgorithmExportPanel(Activity owner,Ui.Sheet sheet,FlowModel.Document project,Executor worker,Consumer<String> saveFile,BiConsumer<String,String> locate){
  this(owner,sheet,project,worker,saveFile,locate,Collections.emptyMap());
 }
 FlowgorithmExportPanel(Activity owner,Ui.Sheet sheet,FlowModel.Document project,Executor worker,Consumer<String> saveFile,BiConsumer<String,String> locate,Map<String,Integer> initial){
  this(owner,sheet,project,worker,saveFile,locate,initial,Collections.emptyMap());
 }
 FlowgorithmExportPanel(Activity owner,Ui.Sheet sheet,FlowModel.Document project,Executor worker,Consumer<String> saveFile,BiConsumer<String,String> locate,Map<String,Integer> initial,Map<String,Integer> initialColors){
  this.owner=owner;this.sheet=sheet;this.worker=worker;this.locate=locate;snapshot=project.copy();
  choices.putAll(initial);
  colorChoices.putAll(initialColors);
  status=Ui.note(owner,"Analizzo la compatibilità…");status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);sheet.body.addView(status);
  types=Ui.column(owner);sheet.body.addView(types);issues=Ui.column(owner);sheet.body.addView(issues);
  Ui.Button show=new Ui.Button(owner,"Mostra il contenuto del file",null,0);show.setSingleLine(false);show.setMaxLines(3);sheet.body.addView(show,new LinearLayout.LayoutParams(-1,-2));
  preview=Ui.edit(owner,"","Anteprima del file Flowgorithm",true);preview.setKeyListener(null);preview.setTextIsSelectable(true);preview.setTypeface(Typeface.MONOSPACE);preview.setHorizontallyScrolling(true);preview.setMinLines(5);preview.setMaxLines(10);preview.setVisibility(View.GONE);sheet.body.addView(preview,new LinearLayout.LayoutParams(-1,-2));
  show.setOnClickListener(v->{boolean visible=preview.getVisibility()!=View.VISIBLE;preview.setVisibility(visible?View.VISIBLE:View.GONE);show.setLabel(visible?"Nascondi il contenuto del file":"Mostra il contenuto del file");});
  save=new Ui.Button(owner,"Salva .fprg","save",1);save.setSingleLine(false);save.setMaxLines(3);save.setEnabled(false);save.setOnClickListener(v->{if(result!=null&&result.canExport())saveFile.accept(result.xml);});sheet.footer.addView(save,new LinearLayout.LayoutParams(-1,-2));
  sheet.show();analyze();
 }
 private void analyze(){
  final int ticket=++request;result=null;save.setEnabled(false);preview.setText("");status.setText("Analizzo la compatibilità…");
  final Map<String,Integer> selected=new LinkedHashMap<>(choices),selectedColors=new LinkedHashMap<>(colorChoices);
  worker.execute(()->{if(ticket!=request)return;try{
   if(inventory==null)inventory=FlowgorithmExport.analyze(snapshot).requiredTypes;
   FlowgorithmExport.Result next=FlowgorithmExport.analyze(snapshot,selected,selectedColors);
   owner.runOnUiThread(()->{if(owner.isFinishing()||owner.isDestroyed()||!sheet.dialog.isShowing()||ticket!=request)return;
    result=next;for(FlowgorithmExport.TypeChoice choice:inventory)addChoice(choice);for(FlowgorithmExport.ColorChoice choice:next.requiredColors)addColorChoice(choice);
    issues.removeAllViews();for(FlowgorithmExport.Issue i:next.issues){
     TextView text=Ui.note(owner,(i.error?"Da correggere: ":"Nota: ")+i.message+"\n"+i.suggestion);if(i.error)text.setTextColor(Ui.danger());issues.addView(text);Ui.gap(issues,8);
     if(i.nodeId!=null&&locate!=null){Ui.Button button=new Ui.Button(owner,"Vai al blocco","right",0);button.setSingleLine(false);button.setMaxLines(3);button.setContentDescription("Vai al blocco: "+i.message);button.setOnClickListener(v->{sheet.dismiss();locate.accept(i.functionId,i.nodeId);});issues.addView(button,new LinearLayout.LayoutParams(-1,-2));Ui.gap(issues,12);}
    }
    status.setText(next.canExport()?"Copia pronta. Controlla gli avvisi, poi salva il file separato.":!next.requiredTypes.isEmpty()||!next.requiredColors.isEmpty()?"Scegli i tipi o i colori richiesti per la copia, poi controlla gli eventuali problemi.":"Correggi i problemi indicati prima di esportare.");preview.setText(next.xml==null?"":next.xml);save.setEnabled(next.canExport());
   });
  }catch(IllegalArgumentException e){owner.runOnUiThread(()->{if(!owner.isFinishing()&&sheet.dialog.isShowing()&&ticket==request)status.setText("Conversione non disponibile: "+e.getMessage());});}});
 }
 private void addChoice(FlowgorithmExport.TypeChoice choice){
  if(!shown.add(choice.key))return;
  TextView label=Ui.text(owner,choice.scope+" · "+choice.name,16,true);types.addView(label);types.addView(Ui.note(owner,"Tipo nella copia Flowgorithm"));
  final Ui.Button[] buttons=new Ui.Button[4];String[] names={"Intero","Reale","Testo","Logico"};
  for(int row=0;row<2;row++){
   LinearLayout line=Ui.row(owner);types.addView(line);
   for(int col=0;col<2;col++){final int index=row*2+col;Ui.Button button=new Ui.Button(owner,names[index],null,0);buttons[index]=button;button.setChosen(Objects.equals(choices.get(choice.key),index+1));button.setSingleLine(false);button.setMaxLines(3);button.setContentDescription("Tipo "+names[index]+" per "+choice.name+" in "+choice.scope);line.addView(button,Ui.weight(owner,-2));button.setOnClickListener(v->{if(Objects.equals(choices.get(choice.key),index+1))return;choices.put(choice.key,index+1);for(int at=0;at<4;at++)buttons[at].setChosen(at==index);analyze();});}
  }
  Ui.gap(types,16);
 }
 private void addColorChoice(FlowgorithmExport.ColorChoice choice){
  if(!shownColors.add(choice.key))return;
  types.addView(Ui.text(owner,choice.scope+" · "+choice.original,16,true));types.addView(Ui.note(owner,"Scegli il colore nella copia Flowgorithm. L’originale resta "+choice.original+"."));
  final Ui.Button[] buttons=new Ui.Button[FlowgorithmImport.PEN_NAMES.length];
  for(int row=0;row<5;row++){LinearLayout line=Ui.row(owner);types.addView(line);for(int col=0;col<2;col++){final int index=row*2+col;String label=FlowgorithmImport.PEN_LABELS[index]+" · "+FlowgorithmImport.PEN_COLORS[index];Ui.Button button=new Ui.Button(owner,label,null,0);buttons[index]=button;button.setChosen(Objects.equals(colorChoices.get(choice.key),index));button.setSingleLine(false);button.setMaxLines(3);button.setContentDescription("Colore "+label+" per "+choice.original+" in "+choice.scope);line.addView(button,Ui.weight(owner,-2));button.setOnClickListener(v->{if(Objects.equals(colorChoices.get(choice.key),index))return;colorChoices.put(choice.key,index);for(int at=0;at<buttons.length;at++)buttons[at].setChosen(at==index);analyze();});}}
  Ui.gap(types,16);
 }
}
