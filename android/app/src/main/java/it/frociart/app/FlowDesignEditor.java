package it.frociart.app;

import android.app.Activity;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.InputFilter;
import android.text.TextWatcher;
import android.view.View;
import android.view.ViewGroup;
import android.widget.*;
import java.util.function.Consumer;

/** A draft preview. Preferences and the live project change only after Apply. */
final class FlowDesignEditor {
    final Ui.Sheet sheet;
    final FlowCanvasView preview;
    private final Spinner family;
    private final SeekBar scale;
    private final EditText[] colors=new EditText[6];
    private final View[] swatches=new View[6];
    private final TextView sizeLabel,error;
    private final LinearLayout colorFields;
    private final Ui.Button apply,toggle;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final Runnable refresh=()->updatePreview();
    private boolean loading,expanded;private Ui.Sheet paletteSheet;
    private FlowDesign valid;
    FlowDesignEditor(Activity owner,Ui.Sheet sheet,FlowModel.Document source,FlowDesign initial,Bundle draft,Consumer<FlowDesign> commit){
        this.sheet=sheet;family=new Spinner(owner);family.setContentDescription("Carattere dei blocchi");family.setPopupBackgroundDrawable(new ColorDrawable(Ui.surface()));family.setBackgroundTintList(ColorStateList.valueOf(Ui.muted()));
        family.setAdapter(new ArrayAdapter<String>(owner,android.R.layout.simple_spinner_item,FlowDesign.FAMILIES){
            @Override public View getView(int position,View reuse,ViewGroup parent){return row(position,reuse);}
            @Override public View getDropDownView(int position,View reuse,ViewGroup parent){return row(position,reuse);}
            private View row(int position,View reuse){TextView t=reuse instanceof TextView?(TextView)reuse:Ui.text(owner,"",16,false);t.setText(getItem(position));Ui.pad(t,12,12,12,12);t.setMinHeight(Ui.dp(owner,48));t.setBackgroundColor(Ui.surface());return t;}
        });
        sheet.body.addView(Ui.text(owner,"Carattere",15,true));sheet.body.addView(family,new LinearLayout.LayoutParams(-1,-2));Ui.gap(sheet.body,12);
        sizeLabel=Ui.text(owner,"",15,true);sheet.body.addView(sizeLabel);scale=new SeekBar(owner);scale.setMax(12);scale.setContentDescription("Dimensione del testo dei blocchi");scale.setProgressTintList(ColorStateList.valueOf(Ui.accentInk()));scale.setThumbTintList(ColorStateList.valueOf(Ui.accentInk()));sheet.body.addView(scale,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));
        preview=new FlowCanvasView(owner);preview.setMotionEnabled(Ui.motion);preview.setReadOnly(true);FlowModel.Document sample=new FlowModel.Document();sample.nodes.add(new FlowModel.Node(FlowModel.PROCESS,"totale = a + b",48,48));preview.setDocument(sample,false);preview.setDark(Ui.dark);preview.setContentDescription("Anteprima dell’aspetto dei blocchi");sheet.body.addView(preview,new LinearLayout.LayoutParams(-1,Ui.dp(owner,220)));Ui.gap(sheet.body,12);
        colorFields=Ui.column(owner);colorFields.setVisibility(View.GONE);toggle=new Ui.Button(owner,"Colori dei titoli ▸","",0);toggle.setOnClickListener(v->{expanded=!expanded;colorFields.setVisibility(expanded?View.VISIBLE:View.GONE);toggle.setLabel(expanded?"Colori dei titoli ▾":"Colori dei titoli ▸");if(expanded)Ui.enter(colorFields,0);});sheet.body.addView(toggle,new LinearLayout.LayoutParams(-1,-2));sheet.body.addView(colorFields);
        TextWatcher changed=new TextWatcher(){public void beforeTextChanged(CharSequence s,int st,int count,int after){}public void onTextChanged(CharSequence s,int start,int before,int count){queue();}public void afterTextChanged(Editable e){}};
        for(int i=0;i<colors.length;i++){colorFields.addView(Ui.text(owner,FlowDesign.ROLES[i],14,true));LinearLayout row=Ui.row(owner);EditText text=Ui.edit(owner,"","#RRGGBB",false);text.setFilters(new InputFilter[]{new InputFilter.LengthFilter(7)});text.setContentDescription("Colore: "+FlowDesign.ROLES[i]);text.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);colors[i]=text;final int role=i;Ui.Button swatch=new Ui.Button(owner,"","",0);swatch.setContentDescription("Scegli colore: "+FlowDesign.ROLES[i]);swatch.setOnClickListener(v->openPalette(owner,role));swatches[i]=swatch;row.addView(swatch,Ui.lp(owner,48,48));row.addView(text,Ui.weight(owner,-2));colorFields.addView(row);Ui.gap(colorFields,10);text.addTextChangedListener(changed);text.setOnFocusChangeListener((v,focused)->{if(focused)previewRole(role);});}
        error=Ui.note(owner,"");sheet.body.addView(error);Ui.gap(sheet.body,8);sheet.action("Ripristina font e colori","undo",0,()->load(FlowDesign.DEFAULT,null));
        LinearLayout actions=Ui.row(owner);Ui.Button cancel=new Ui.Button(owner,"Annulla","",0);cancel.setOnClickListener(v->sheet.dismiss());actions.addView(cancel,Ui.weight(owner,-2));Ui.Button button=new Ui.Button(owner,"Applica","check",1);apply=button;button.setOnClickListener(v->{if(updatePreview())try{commit.accept(valid);sheet.dismiss();}catch(IllegalArgumentException e){error.setText(e.getMessage());error.setTextColor(0xffd84e68);}});actions.addView(button,Ui.weight(owner,-2));sheet.footer.addView(actions);
        family.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener(){public void onItemSelected(AdapterView<?> p,View v,int pos,long id){queue();}public void onNothingSelected(AdapterView<?> p){}});scale.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar s,int p,boolean user){sizeLabel.setText("Dimensione · "+(80+p*10)+"%");queue();}public void onStartTrackingTouch(SeekBar s){}public void onStopTrackingTouch(SeekBar s){}});
        load(initial,draft);preview.post(()->{updatePreview();preview.fitToContent();});
    }
    private void queue(){if(loading)return;handler.removeCallbacks(refresh);handler.postDelayed(refresh,120);}
    private void load(FlowDesign design,Bundle draft){loading=true;family.setSelection(draft==null?design.family:Math.max(0,Math.min(2,draft.getInt("family",design.family))));scale.setProgress(draft==null?(design.percent-80)/10:Math.max(0,Math.min(12,draft.getInt("scale",(design.percent-80)/10))));String[] raw=draft==null?null:draft.getStringArray("colors");for(int i=0;i<colors.length;i++)colors[i].setText(raw!=null&&raw.length==6?raw[i]:design.hex(i));expanded=draft!=null&&draft.getBoolean("expanded");colorFields.setVisibility(expanded?View.VISIBLE:View.GONE);toggle.setLabel(expanded?"Colori dei titoli ▾":"Colori dei titoli ▸");sizeLabel.setText("Dimensione · "+(80+scale.getProgress()*10)+"%");loading=false;updatePreview();}
    private boolean updatePreview(){try{String[] palette=new String[6];for(int i=0;i<palette.length;i++)palette[i]=colors[i].getText().toString();FlowDesign next=new FlowDesign(family.getSelectedItemPosition(),80+scale.getProgress()*10,palette);boolean reflow=valid==null||valid.fontKey()!=next.fontKey();valid=next;preview.setAppearance(new FlowAppearance(3,next));if(reflow){FlowAutoLayout.apply(preview.getDocument(),FlowCanvasView.fontSizing(next));preview.notifyModelChanged();preview.fitToContent();}for(int i=0;i<colors.length;i++)Ui.card(swatches[i],Color.parseColor(next.hex(i)),8);error.setText("I colori vengono adattati per restare leggibili. Le caselle mantengono lo sfondo neutro.");error.setTextColor(Ui.muted());apply.setEnabled(true);return true;}catch(IllegalArgumentException e){error.setText(e.getMessage());error.setTextColor(0xffd84e68);apply.setEnabled(false);return false;}}
    Bundle snapshot(){Bundle out=new Bundle();out.putInt("family",family.getSelectedItemPosition());out.putInt("scale",scale.getProgress());String[] raw=new String[6];for(int i=0;i<raw.length;i++)raw[i]=colors[i].getText().toString();out.putStringArray("colors",raw);out.putBoolean("expanded",expanded);return out;}
    private void previewRole(int role){FlowModel.Node node=preview.getDocument().nodes.get(0);node.kind=new int[]{FlowModel.PROCESS,FlowModel.INPUT,FlowModel.OUTPUT,FlowModel.DECISION,FlowModel.START,FlowModel.END}[role];node.text=new String[]{"totale = a + b","numero","numero","numero > 0","Inizio","Fine"}[role];preview.notifyModelChanged();preview.fitToContent();}
    private void openPalette(Activity owner,int role){
        previewRole(role);
        if(paletteSheet!=null)paletteSheet.dismiss();Ui.Sheet picker=new Ui.Sheet(owner,"Colore · "+FlowDesign.ROLES[role],"Tocca un colore oppure scrivi il codice nel campo del titolo.");paletteSheet=picker;
        String[] names={"Blu","Turchese","Viola","Arancio","Verde","Grigio","Rosa","Ciano"},values={"#5B8DEF","#22A5B8","#9771DC","#D68A18","#39B985","#7A719C","#EF6C82","#26C6DA"};
        for(int i=0;i<names.length;i+=2){LinearLayout row=Ui.row(owner);for(int j=i;j<i+2;j++){String value=values[j];Ui.Button button=new Ui.Button(owner,names[j],"",0);int color=Color.parseColor(value);Ui.card(button,color,12);button.setTextColor(FlowAppearance.contrast(color,Color.WHITE)>=4.5?Color.WHITE:Color.BLACK);button.setOnClickListener(v->{colors[role].setText(value);picker.dismiss();});row.addView(button,Ui.weight(owner,-2));}picker.body.addView(row);Ui.gap(picker.body,8);}
        picker.dialog.setOnDismissListener(d->{if(paletteSheet==picker)paletteSheet=null;});picker.show();
    }
    void close(){handler.removeCallbacksAndMessages(null);if(paletteSheet!=null)paletteSheet.dismiss();}
}
