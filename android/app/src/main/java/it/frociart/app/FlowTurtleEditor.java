package it.frociart.app;
import android.app.Activity;import android.text.*;import android.view.View;import android.widget.*;import static it.frociart.app.FlowModel.*;
/** Mirrors guided fields into the existing recoverable node draft. */
final class FlowTurtleEditor {
 final EditText expression,value;final View rawLabel;final Ui.Toggle right,left,draw,syntax;final int kind;boolean turnLeft,syncing;
 FlowTurtleEditor(Activity owner,LinearLayout parent,EditText target,Node node,Document project){
  kind=node.kind;expression=target;rawLabel=parent.getChildAt(parent.indexOfChild(target)-1);LinearLayout host=Ui.column(owner);Ui.pad(host,0,8,0,8);parent.addView(host);
  right=new Ui.Toggle(owner,"Destra",null);left=new Ui.Toggle(owner,"Sinistra",null);right.setTag("turtle-right");left.setTag("turtle-left");right.setContentDescription("Ruota in senso orario");left.setContentDescription("Ruota in senso antiorario");
  if(kind==TURTLE_TURN){host.addView(Ui.note(owner,"Direzione"));LinearLayout row=Ui.row(owner);row.addView(right,Ui.weight(owner,48));row.addView(left,Ui.weight(owner,48));host.addView(row);}
  value=Ui.edit(owner,"",FlowTurtle.fieldLabel(kind),false);value.setTag("turtle-value");
  if(kind!=TURTLE_HOME&&kind!=TURTLE_CLEAR){host.addView(Ui.note(owner,FlowTurtle.fieldLabel(kind)));host.addView(value);Ui.Button functions=new Ui.Button(owner,"Funzioni ed esempi",null,0);functions.setOnClickListener(v->FlowFunctionLibrary.show(owner,value,project,false));host.addView(functions,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));}
  if(kind==TURTLE_PEN){Ui.Button colors=new Ui.Button(owner,"Scegli un colore",null,0);colors.setTag("turtle-colors");colors.setOnClickListener(v->{Ui.Sheet s=new Ui.Sheet(owner,"Colore della penna","Puoi anche scrivere una variabile o un codice #RRGGBB fra virgolette.");for(String[] c:FlowTurtle.COLORS){s.action(c[0],null,0,()->{value.setText("\""+c[0]+"\"");s.dismiss();});}s.show();});host.addView(colors,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));}
  draw=new Ui.Toggle(owner,"Disegna durante lo spostamento",null);draw.setTag("turtle-draw");draw.setChecked(true);if(kind==TURTLE_FORWARD)host.addView(draw,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));
  host.addView(Ui.note(owner,FlowTurtle.help(kind)));syntax=new Ui.Toggle(owner,"Modifica come testo",null);syntax.setTag("turtle-syntax");host.addView(syntax,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));syntax.setOnClickListener(v->{syntax.toggle();showSyntax(syntax.isChecked());if(syntax.isChecked())Ui.enter(expression,0);});
  value.setFilters(new InputFilter[]{(source,start,end,dest,dstart,dend)->{if(syncing)return null;String next=dest.subSequence(0,dstart).toString()+source.subSequence(start,end)+dest.subSequence(dend,dest.length());if(candidate(next).length()<=8192)return null;value.setError("L'istruzione può contenere al massimo 8192 caratteri.");return dest.subSequence(dstart,dend);}});
  right.setOnClickListener(v->direction(false));left.setOnClickListener(v->direction(true));draw.setOnClickListener(v->{draw.toggle();if(!write())draw.toggle();});showSyntax(false);read();FlowFileEditor.watch(value,()->{if(!syncing)write();});FlowFileEditor.watch(expression,()->{if(!syncing)read();});
 }
 void direction(boolean selected){boolean before=turnLeft;turnLeft=selected;if(!write())turnLeft=before;update();}
 void update(){left.setChecked(turnLeft);right.setChecked(!turnLeft);}
 void showSyntax(boolean visible){syntax.setChecked(visible);expression.setVisibility(visible?View.VISIBLE:View.GONE);rawLabel.setVisibility(visible?View.VISIBLE:View.GONE);}
 void read(){syncing=true;try{FlowTurtle.Spec spec=FlowTurtle.parse(kind,expression.getText().toString());value.setText(spec.expression==null?"":spec.expression);turnLeft=spec.left;draw.setChecked(spec.draw);update();}catch(IllegalArgumentException ex){showSyntax(true);}finally{syncing=false;}}
 String candidate(String text){return kind==TURTLE_HOME||kind==TURTLE_CLEAR?FlowTurtle.defaultText(kind):(kind==TURTLE_TURN?(turnLeft?"SINISTRA ":"DESTRA "):kind==TURTLE_FORWARD&&!draw.isChecked()?"SENZA_TRACCIA ":"")+text.trim();}
 boolean write(){if(syncing)return true;String text=candidate(value.getText().toString());if(text.length()>8192){value.setError("Accorcia il testo prima di cambiare opzione.");return false;}value.setError(null);syncing=true;expression.setText(text);expression.setSelection(expression.length());syncing=false;return true;}
}
