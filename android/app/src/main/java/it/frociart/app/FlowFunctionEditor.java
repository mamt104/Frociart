package it.frociart.app;

import android.app.Activity;
import android.text.*;
import android.view.*;
import android.widget.*;
import java.util.*;
import java.util.function.Consumer;
import java.util.function.IntConsumer;
import org.json.*;
import static it.frociart.app.FlowModel.*;

/** A definition is a draft until Apply. Existing themed sheets handle scrolling and keyboard insets. */
final class FlowFunctionEditor {
    final Ui.Sheet sheet;
    final String id;
    final EditText name,result;
    final ArrayList<ParameterRow> parameters=new ArrayList<>();
    final TextView error;
    private final Activity owner;
    private final Document source;
    private final Function original;
    private final boolean creating;
    private final Consumer<String> keepDraft;
    private final Runnable clearDraft;
    interface Apply {void accept(Document project,String id,FlowFunctionEditing.Rename rename);}
    private final Apply apply;
    private final LinearLayout parameterList,resultFields;
    private final Ui.Button add,resultTypeButton;
    private final Ui.Toggle resultArray;
    private int returnType;
    private boolean ready;
    private Ui.Sheet child;
    private static final String[] TYPES={"Automatico","Intero","Reale","Testo","Logico","Nessuno"};
    static final class ParameterRow {String oldName;EditText name;int type;Ui.Toggle array,reference;LinearLayout host;Ui.Button typeButton;}

    FlowFunctionEditor(Activity owner,Ui.Sheet sheet,Document source,Function function,String restored,Consumer<String> keep,Runnable clear,Apply apply){
        this.owner=owner;this.sheet=sheet;this.source=source.copy();this.keepDraft=keep;this.clearDraft=clear;this.apply=apply;
        creating=function==null;original=creating?FlowFunctionEditing.create(source):function.copy();
        JSONObject draft=null;try{if(restored!=null&&restored.length()<=40000){JSONObject value=new JSONObject(restored);if(value.optBoolean("new")==creating&&(creating||original.id.equals(value.optString("id"))))draft=value;}}catch(JSONException ignored){}
        if(creating&&draft!=null&&draft.optString("id").matches("[a-f0-9]{32}"))original.id=draft.optString("id");id=original.id;
        label(sheet.body,"Nome della funzione");name=Ui.edit(owner,draft==null?original.name:draft.optString("name"),"Nome della funzione",false);limit(name,64);name.setTag("function-name");sheet.body.addView(name);
        sheet.paragraph("I parametri portano i dati dentro. I vettori usano una copia; con Modifica l’originale gli elementi aggiornati si vedono anche nel chiamante.");
        label(sheet.body,"Parametri · nell’ordine della chiamata");parameterList=Ui.column(owner);sheet.body.addView(parameterList);
        add=new Ui.Button(owner,"Aggiungi parametro","add",0);add.setOnClickListener(v->{addParameter(freeName(),AUTO,false,null);persist();});sheet.body.addView(add,new LinearLayout.LayoutParams(-1,-2));
        label(sheet.body,"Risultato");returnType=draft==null?original.returnType:Math.max(AUTO,Math.min(VOID,draft.optInt("type",VOID)));
        resultTypeButton=new Ui.Button(owner,TYPES[returnType],"down",0);resultTypeButton.setContentDescription("Tipo del risultato");resultTypeButton.setOnClickListener(v->chooseType(true,type->{returnType=type;updateResult();persist();}));sheet.body.addView(resultTypeButton,new LinearLayout.LayoutParams(-1,-2));
        resultFields=Ui.column(owner);label(resultFields,"Variabile restituita alla fine");result=Ui.edit(owner,draft==null?original.returnVariable:draft.optString("result"),"Variabile restituita",false);limit(result,64);result.setTag("function-result");resultFields.addView(result);
        resultArray=new Ui.Toggle(owner,"Restituisce un vettore",null);resultArray.setChecked(draft==null?original.returnIsArray:draft.optBoolean("array"));resultArray.setOnClickListener(v->{resultArray.toggle();persist();});resultFields.addView(resultArray,new LinearLayout.LayoutParams(-1,-2));sheet.body.addView(resultFields);
        sheet.paragraph("Assegna la variabile del risultato nel diagramma: il suo valore viene restituito quando raggiungi Fine.");
        error=Ui.note(owner,"");error.setTextColor(Ui.danger());error.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);sheet.body.addView(error);
        sheet.primary("Apri il diagramma","check",this::save);
        sheet.action("Scarta questa definizione","close",0,()->{ready=false;clearDraft.run();sheet.dismiss();});
        if(!creating)sheet.action("Elimina funzione","trash",3,this::confirmDelete);
        JSONArray rows=draft==null?null:draft.optJSONArray("parameters");
        if(rows==null){for(Parameter p:original.parameters)addParameter(p.name,p.type,p.isArray,p.byReference,p.name);}
        else for(int i=0;i<Math.min(rows.length(),FlowFunctions.MAX_PARAMETERS);i++){JSONObject row=rows.optJSONObject(i);if(row!=null)addParameter(row.optString("name"),Math.max(AUTO,Math.min(BOOLEAN,row.optInt("type"))),row.optBoolean("array"),row.optBoolean("reference"),row.isNull("old")?null:row.optString("old"));}
        watch(name);watch(result);updateResult();ready=true;persist();sheet.show();
    }
    private void label(LinearLayout parent,String text){Ui.gap(parent,10);parent.addView(Ui.text(owner,text,13,true));Ui.gap(parent,6);}
    private void limit(EditText input,int max){input.setFilters(new InputFilter[]{new InputFilter.LengthFilter(max)});input.setSelectAllOnFocus(false);}
    private void watch(EditText input){input.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void onTextChanged(CharSequence s,int a,int b,int c){persist();}public void afterTextChanged(Editable e){}});}
    private String freeName(){for(int i=1;;i++){String value="valore"+(i==1?"":i);boolean found=false;for(ParameterRow p:parameters)if(p.name.getText().toString().equalsIgnoreCase(value))found=true;if(!found)return value;}}
    void addParameter(String value,int type,boolean array,String oldName){addParameter(value,type,array,false,oldName);}
    void addParameter(String value,int type,boolean array,boolean byReference,String oldName){
        if(parameters.size()>=FlowFunctions.MAX_PARAMETERS)return;ParameterRow row=new ParameterRow();row.oldName=oldName;row.type=type;row.host=Ui.column(owner);Ui.pad(row.host,10,8,10,8);Ui.card(row.host,Ui.bg(),16);
        LinearLayout first=Ui.row(owner);row.name=Ui.edit(owner,value,"Nome parametro",false);row.name.setContentDescription("Nome parametro "+(parameters.size()+1));limit(row.name,64);first.addView(row.name,Ui.weight(owner,-2));Ui.Button remove=new Ui.Button(owner,"","trash",0);remove.setContentDescription("Rimuovi parametro "+value);remove.setOnClickListener(v->{parameters.remove(row);rebuildRows();persist();});first.addView(remove,Ui.lp(owner,48,48));row.host.addView(first);
        LinearLayout second=Ui.row(owner);row.typeButton=new Ui.Button(owner,TYPES[type],"down",0);row.typeButton.setContentDescription("Tipo parametro "+value);row.typeButton.setOnClickListener(v->chooseType(false,chosen->{row.type=chosen;row.typeButton.setLabel(TYPES[chosen]);persist();}));second.addView(row.typeButton,Ui.weight(owner,48));
        row.array=new Ui.Toggle(owner,"Vettore",null);row.array.setChecked(array);row.array.setOnClickListener(v->{row.array.toggle();row.reference.setEnabled(row.array.isChecked());if(!row.array.isChecked())row.reference.setChecked(false);persist();});second.addView(row.array,Ui.weight(owner,48));row.host.addView(second);
        LinearLayout order=Ui.row(owner);boolean compactOrder=owner.getResources().getConfiguration().fontScale>1.3f;Ui.Button up=new Ui.Button(owner,compactOrder?"":"Sposta prima","up",0),down=new Ui.Button(owner,compactOrder?"":"Sposta dopo","down",0);up.setContentDescription("Sposta parametro prima");down.setContentDescription("Sposta parametro dopo");up.setTooltipText("Sposta parametro prima");down.setTooltipText("Sposta parametro dopo");up.setTextSize(12);down.setTextSize(12);up.setOnClickListener(v->move(row,-1));down.setOnClickListener(v->move(row,1));order.addView(up,Ui.weight(owner,48));order.addView(down,Ui.weight(owner,48));row.host.addView(order);row.reference=new Ui.Toggle(owner,"Modifica l’originale",null);row.reference.setTag("parameter-reference-"+parameters.size());row.reference.setContentDescription("Modifica il vettore originale del parametro "+value);row.reference.setChecked(array&&byReference);row.reference.setEnabled(array);row.reference.setOnClickListener(v->{row.reference.toggle();persist();});row.host.addView(row.reference,new LinearLayout.LayoutParams(-1,-2));
        parameters.add(row);watch(row.name);rebuildRows();
    }
    private void move(ParameterRow row,int direction){int from=parameters.indexOf(row),to=from+direction;if(to<0||to>=parameters.size())return;Collections.swap(parameters,from,to);rebuildRows();persist();}
    private void rebuildRows(){parameterList.removeAllViews();for(int i=0;i<parameters.size();i++){ParameterRow row=parameters.get(i);parameterList.addView(row.host);LinearLayout order=(LinearLayout)row.host.getChildAt(2);order.getChildAt(0).setEnabled(i>0);order.getChildAt(1).setEnabled(i<parameters.size()-1);Ui.gap(parameterList,10);}add.setEnabled(parameters.size()<FlowFunctions.MAX_PARAMETERS);if(ready)Ui.enter(parameterList,0);}
    private void chooseType(boolean allowVoid,IntConsumer select){child=new Ui.Sheet(owner,allowVoid?"Tipo del risultato":"Tipo del parametro",null);final Ui.Sheet current=child;for(int i=0;i<(allowVoid?6:5);i++){final int type=i;current.action(TYPES[i],null,0,()->{select.accept(type);current.dismiss();});}current.show();}
    private void updateResult(){resultTypeButton.setLabel(TYPES[returnType]);resultFields.setVisibility(returnType==VOID?View.GONE:View.VISIBLE);if(returnType!=VOID&&result.length()==0)result.setText("risultato");}
    private void persist(){if(!ready)return;keepDraft.accept(state());}
    String state(){try{JSONObject value=new JSONObject();value.put("id",id);value.put("new",creating);value.put("name",name.getText().toString());value.put("type",returnType);value.put("result",result.getText().toString());value.put("array",resultArray.isChecked());JSONArray rows=new JSONArray();for(ParameterRow p:parameters){JSONObject row=new JSONObject();row.put("name",p.name.getText().toString());row.put("type",p.type);row.put("array",p.array.isChecked());row.put("reference",p.reference.isChecked());row.put("old",p.oldName==null?JSONObject.NULL:p.oldName);rows.put(row);}value.put("parameters",rows);return value.toString();}catch(JSONException e){throw new IllegalStateException("Definizione non conservabile.",e);}}
    void save(){
        try{Function edited=original.copy();edited.name=name.getText().toString().trim();edited.parameters.clear();Map<String,String> renames=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);
            for(ParameterRow row:parameters){Parameter p=new Parameter();p.name=row.name.getText().toString().trim();p.type=row.type;p.isArray=row.array.isChecked();p.byReference=p.isArray&&row.reference.isChecked();edited.parameters.add(p);if(row.oldName!=null&&!row.oldName.equals(p.name))renames.put(row.oldName,p.name);}
            edited.returnType=returnType;edited.returnVariable=returnType==VOID?"":result.getText().toString().trim();edited.returnIsArray=returnType!=VOID&&resultArray.isChecked();
            if(original.returnType!=VOID&&edited.returnType!=VOID&&!original.returnVariable.equals(edited.returnVariable)){String renamed=renames.get(original.returnVariable);if(renamed!=null&&!renamed.equalsIgnoreCase(edited.returnVariable))throw new IllegalArgumentException("Il parametro restituito e il risultato devono avere lo stesso nuovo nome.");renames.put(original.returnVariable,edited.returnVariable);}
            Document candidate=FlowFunctionEditing.save(source,edited,renames);FlowFunctionEditing.Rename rename=creating?null:new FlowFunctionEditing.Rename(original,edited,renames);apply.accept(candidate,id,rename);ready=false;clearDraft.run();sheet.dismiss();
        }catch(IllegalArgumentException e){error.setText(e.getMessage());error.requestRectangleOnScreen(new android.graphics.Rect(0,0,error.getWidth(),error.getHeight()),false);}
    }
    private void confirmDelete(){
        try{final Document candidate=FlowFunctionEditing.delete(source,id);child=new Ui.Sheet(owner,"Eliminare «"+original.name+"»?","Il diagramma della funzione verrà rimosso. Puoi ripristinarlo con Annulla.");final Ui.Sheet confirm=child;confirm.action("Elimina funzione","trash",3,()->{try{apply.accept(candidate,null,null);ready=false;clearDraft.run();confirm.dismiss();sheet.dismiss();}catch(IllegalArgumentException e){confirm.dismiss();error.setText(e.getMessage());}});confirm.primary("Mantieni funzione","back",confirm::dismiss);confirm.show();}
        catch(IllegalArgumentException e){error.setText(e.getMessage());}
    }
    void closeChild(){if(child!=null)child.dismiss();}
}
