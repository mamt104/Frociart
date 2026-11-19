package it.frociart.app;
import android.app.Activity;import android.text.*;import android.view.View;import android.widget.*;

/** The existing expression field remains the single recoverable draft representation. */
final class FlowDeclarationEditor {
 final EditText names,size;final Ui.Toggle array;final Ui.Toggle[] types=new Ui.Toggle[4];
 final LinearLayout host;final TextView sizeLabel,help;final EditText expression;final View rawLabel;final Ui.Toggle syntax;int type=1;boolean syncing;
 static final String[] TOKENS={"INTERO","REALE","TESTO","LOGICO"};
 FlowDeclarationEditor(Activity owner,LinearLayout parent,EditText target){
  expression=target;rawLabel=parent.getChildAt(parent.indexOfChild(target)-1);host=Ui.column(owner);Ui.pad(host,0,8,0,8);parent.addView(host);
  host.addView(Ui.note(owner,"Nomi delle variabili, separati da virgole"));names=Ui.edit(owner,"","Nomi delle variabili",false);names.setTag("declaration-names");host.addView(names);
  Ui.gap(host,10);host.addView(Ui.note(owner,"Tipo del valore"));
  for(int row=0;row<2;row++){LinearLayout line=Ui.row(owner);host.addView(line);for(int col=0;col<2;col++){final int index=row*2+col;Ui.Toggle button=new Ui.Toggle(owner,FlowDeclaration.label(index+1),null);button.setTag("declaration-type-"+(index+1));button.setContentDescription("Tipo "+FlowDeclaration.label(index+1));types[index]=button;line.addView(button,Ui.weight(owner,48));button.setOnClickListener(v->{int old=type;type=index+1;if(!write())type=old;update();});}}
  array=new Ui.Toggle(owner,"Vettore: più elementi",null);array.setTag("declaration-array");host.addView(array,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));array.setOnClickListener(v->{array.toggle();if(!write())array.toggle();update();});
  sizeLabel=Ui.note(owner,"Numero di elementi (anche un'espressione)");host.addView(sizeLabel);size=Ui.edit(owner,"10","Numero di elementi",false);size.setTag("declaration-size");host.addView(size);
  help=Ui.note(owner,"Valori iniziali: 0, testo vuoto o falso. Gli indici dei vettori partono da 0. Un Intero elimina la parte decimale.");host.addView(help);
  syntax=new Ui.Toggle(owner,"Modifica come testo",null);syntax.setTag("declaration-syntax");host.addView(syntax,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));syntax.setOnClickListener(v->{syntax.toggle();showSyntax(syntax.isChecked());if(syntax.isChecked())Ui.enter(expression,0);});
  names.setFilters(new InputFilter[]{limit(names,true)});size.setFilters(new InputFilter[]{limit(size,false)});
  showSyntax(false);read();watch(names,this::write);watch(size,this::write);watch(expression,()->{if(!syncing)read();});
 }
 void update(){for(int i=0;i<4;i++)types[i].setChecked(type==i+1);sizeLabel.setVisibility(array.isChecked()?View.VISIBLE:View.GONE);size.setVisibility(array.isChecked()?View.VISIBLE:View.GONE);}
 void showSyntax(boolean visible){syntax.setChecked(visible);expression.setVisibility(visible?View.VISIBLE:View.GONE);rawLabel.setVisibility(visible?View.VISIBLE:View.GONE);}
 void read(){try{FlowDeclaration d=FlowDeclaration.parse(expression.getText().toString());syncing=true;names.setText(String.join(", ",d.names));type=d.type;array.setChecked(d.isArray());if(d.isArray())size.setText(d.sizeText);syncing=false;update();}catch(IllegalArgumentException ex){syncing=false;update();showSyntax(true);}}
 String candidate(String n,String s){return n.trim()+": "+TOKENS[type-1]+(array.isChecked()?"["+s.trim()+"]":"");}
 InputFilter limit(EditText field,boolean name){return(source,start,end,dest,dstart,dend)->{if(syncing)return null;String changed=dest.subSequence(0,dstart).toString()+source.subSequence(start,end)+dest.subSequence(dend,dest.length());if(candidate(name?changed:names.getText().toString(),name?size.getText().toString():changed).length()<=8192)return null;field.setError("La dichiarazione può contenere al massimo 8192 caratteri.");return dest.subSequence(dstart,dend);};}
 boolean write(){if(syncing)return true;String value=candidate(names.getText().toString(),size.getText().toString());if(value.length()>8192){names.setError("Accorcia la dichiarazione prima di cambiare tipo o attivare il vettore.");return false;}names.setError(null);size.setError(null);syncing=true;expression.setText(value);expression.setSelection(expression.length());syncing=false;return true;}
 static void watch(EditText edit,Runnable action){edit.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void onTextChanged(CharSequence s,int a,int b,int c){action.run();}public void afterTextChanged(Editable e){}});}
}
