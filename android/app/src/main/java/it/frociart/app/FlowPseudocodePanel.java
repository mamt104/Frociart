package it.frociart.app;

import android.app.Activity;
import android.content.*;
import android.graphics.Typeface;
import android.text.*;
import android.view.*;
import android.widget.*;
import java.util.function.*;

/** The native editor uses the same animated, themed sheet as the rest of the app. */
final class FlowPseudocodePanel {
    final Ui.Sheet sheet;final EditText editor;final TextView status;
    private final Activity owner;
    FlowPseudocodePanel(Activity owner,Ui.Sheet sheet,FlowModel.Document source,String draft,
                        Consumer<String> keepDraft,Predicate<FlowModel.Document> create,Runnable open,Consumer<String> save){
        this.owner=owner;this.sheet=sheet;
        FlowModel.Document snapshot=source.copy();
        LinearLayout tools=Ui.row(owner);sheet.body.addView(tools);
        editor=Ui.edit(owner,"","Scrivi o incolla pseudocodice",true);editor.setTypeface(Typeface.MONOSPACE);editor.setTextSize(14);editor.setMinLines(7);editor.setMaxLines(12);
        editor.setFilters(new InputFilter[]{new InputFilter.LengthFilter(FlowPseudocode.MAX_TEXT)});
        status=Ui.note(owner,"");status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        button(tools,"Dal diagramma",()->replace(()->FlowPseudocode.encode(snapshot)));
        button(tools,"Sintassi",()->{Ui.Sheet help=new Ui.Sheet(owner,"Scrivere pseudocodice",null);help.paragraph(FlowPseudocode.HELP);help.show();});
        Ui.gap(sheet.body,10);sheet.body.addView(editor,new LinearLayout.LayoutParams(-1,-2));Ui.gap(sheet.body,8);sheet.body.addView(status);Ui.gap(sheet.body,8);
        Ui.Button functions=new Ui.Button(owner,"Funzioni ed esempi",null,0);functions.setOnClickListener(v->FlowFunctionLibrary.show(owner,editor));sheet.body.addView(functions,new LinearLayout.LayoutParams(-1,-2));
        LinearLayout files=Ui.row(owner);sheet.body.addView(files);button(files,"Apri testo",open);button(files,"Salva testo",()->save.accept(editor.getText().toString()));
        LinearLayout actions=Ui.row(owner);sheet.body.addView(actions);button(actions,"Esempi",this::examples);button(actions,"Copia",()->{
            if(editor.length()==0){status.setText("Scrivi o genera il codice prima di copiarlo.");return;}
            try{((android.content.ClipboardManager)owner.getSystemService(Context.CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("Pseudocodice",editor.getText().toString()));status.setText("Pseudocodice copiato.");}catch(RuntimeException e){status.setText("Appunti non disponibili. Usa Salva testo.");}
        });
        sheet.primary("Crea diagramma","align",()->{
            try{FlowModel.Document result=FlowPseudocode.decode(editor.getText().toString());if(create.test(result)){Ui.dismissKeyboard(editor);sheet.dismiss();}}
            catch(FlowPseudocode.ParseError e){problem(e.getMessage());int offset=0;String text=editor.getText().toString();for(int i=1;i<e.line;i++){int end=text.indexOf('\n',offset);if(end<0)break;offset=end+1;}editor.requestFocus();editor.setSelection(Math.min(offset,editor.length()));}
            catch(IllegalArgumentException e){problem(e.getMessage());}
        });
        editor.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int a,int b,int c){keepDraft.accept(s.toString());status.setTextColor(Ui.muted());status.setText("Crea un nuovo progetto. L'originale resta nei tuoi progetti. Aspetto e nomi dei rami si conservano nel file .frociart.");}public void afterTextChanged(Editable e){}});
        if(draft!=null)editor.setText(draft);else try{editor.setText(FlowPseudocode.encode(snapshot));}catch(IllegalArgumentException e){problem(e.getMessage());}
        editor.setSelection(0);sheet.show();sheet.dialog.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE|WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN);
    }
    private void examples(){Ui.Sheet examples=new Ui.Sheet(owner,"Scegli un esempio",null);examples.action("Leggi, decidi e stampa","edit",0,()->{examples.dismiss();replace(()->FlowPseudocode.EXAMPLE);});examples.action("Vettori e ciclo FOR","align",0,()->{examples.dismiss();replace(()->FlowPseudocode.VECTOR_EXAMPLE);});examples.action("Funzione con parametro e risultato","call",0,()->{examples.dismiss();replace(()->FlowPseudocode.FUNCTION_EXAMPLE);});examples.show();}
    private void button(LinearLayout row,String label,Runnable action){Ui.Button b=new Ui.Button(owner,label,null,0);b.setSingleLine(false);b.setMaxLines(2);b.setOnClickListener(v->action.run());row.addView(b,new LinearLayout.LayoutParams(0,-2,1));}
    void problem(String message){status.setTextColor(Ui.danger());status.setText(message);}
    void replace(Supplier<String> supplier){
        final String text;try{text=supplier.get();}catch(IllegalArgumentException e){problem(e.getMessage());return;}
        if(editor.length()==0||editor.getText().toString().equals(text)){editor.setText(text);return;}
        Ui.Sheet confirm=new Ui.Sheet(owner,"Sostituisci pseudocodice","Sostituire il testo nell'editor? Il diagramma resta invariato.");
        confirm.primary("Sostituisci testo","check",()->{editor.setText(text);confirm.dismiss();});confirm.action("Mantieni il mio testo","back",0,confirm::dismiss);confirm.show();
    }
}
