package it.frociart.app;
import android.app.Activity;import android.text.*;import android.view.View;import android.widget.*;
import static it.frociart.app.FlowModel.*;

/** Guided fields mirror the original recoverable expression; invalid raw drafts are never rewritten on open. */
final class FlowFileEditor {
 final EditText expression,value;final View rawLabel;final Ui.Toggle read,write,newline,syntax;final int kind;boolean reading=true,syncing;
 FlowFileEditor(Activity owner,LinearLayout parent,EditText target,Node node,Document project){
  kind=node.kind;expression=target;rawLabel=parent.getChildAt(parent.indexOfChild(target)-1);LinearLayout host=Ui.column(owner);Ui.pad(host,0,8,0,8);parent.addView(host);
  read=new Ui.Toggle(owner,"Lettura",null);write=new Ui.Toggle(owner,"Scrittura",null);read.setTag("file-channel-read");write.setTag("file-channel-write");read.setContentDescription("Canale del file: lettura");write.setContentDescription("Canale del file: scrittura");
  if(kind==OPEN_FILE||kind==CLOSE_FILE){host.addView(Ui.note(owner,"Canale del file"));LinearLayout row=Ui.row(owner);row.addView(read,Ui.weight(owner,48));row.addView(write,Ui.weight(owner,48));host.addView(row);}
  value=Ui.edit(owner,"",FlowFileBlocks.fieldLabel(kind),kind==WRITE_FILE);value.setTag("file-value");
  if(kind!=CLOSE_FILE){host.addView(Ui.note(owner,FlowFileBlocks.fieldLabel(kind)));host.addView(value);Ui.Button functions=new Ui.Button(owner,"Funzioni ed esempi",null,0);functions.setOnClickListener(v->FlowFunctionLibrary.show(owner,value,project,false));host.addView(functions,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));}
  newline=new Ui.Toggle(owner,"Vai a capo dopo questo valore",null);newline.setTag("file-newline");newline.setChecked(true);if(kind==WRITE_FILE)host.addView(newline,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));
  host.addView(Ui.note(owner,FlowFileBlocks.help(kind)));syntax=new Ui.Toggle(owner,"Modifica come testo",null);syntax.setTag("file-syntax");host.addView(syntax,new LinearLayout.LayoutParams(-1,Ui.dp(owner,48)));syntax.setOnClickListener(v->{syntax.toggle();showSyntax(syntax.isChecked());if(syntax.isChecked())Ui.enter(expression,0);});
  value.setFilters(new InputFilter[]{(source,start,end,dest,dstart,dend)->{if(syncing)return null;String next=dest.subSequence(0,dstart).toString()+source.subSequence(start,end)+dest.subSequence(dend,dest.length());if(candidate(next).length()<=8192)return null;value.setError("L'istruzione può contenere al massimo 8192 caratteri.");return dest.subSequence(dstart,dend);}});
  read.setOnClickListener(v->channel(true));write.setOnClickListener(v->channel(false));newline.setOnClickListener(v->{newline.toggle();if(!write())newline.toggle();});showSyntax(false);read();
  watch(value,()->{if(!syncing)write();});watch(expression,()->{if(!syncing)read();});
 }
 void channel(boolean selected){boolean before=reading;reading=selected;if(!write())reading=before;update();}
 void update(){read.setChecked(reading);write.setChecked(!reading);}
 void showSyntax(boolean visible){syntax.setChecked(visible);expression.setVisibility(visible?View.VISIBLE:View.GONE);rawLabel.setVisibility(visible?View.VISIBLE:View.GONE);}
 void read(){syncing=true;try{String text=expression.getText().toString();if(kind==OPEN_FILE){FlowTextFiles.Open spec=new FlowTextFiles.Open(text);reading=spec.reading;value.setText(spec.expression);}else if(kind==CLOSE_FILE)reading=FlowTextFiles.channel(text);else if(kind==WRITE_FILE){FlowTextFiles.Write spec=new FlowTextFiles.Write(text);newline.setChecked(spec.newline);value.setText(spec.expression);}else value.setText(text);update();}catch(IllegalArgumentException ex){showSyntax(true);}finally{syncing=false;}}
 String candidate(String text){String channel=reading?"LETTURA":"SCRITTURA",body=text.trim();return kind==OPEN_FILE?channel+" "+body:kind==CLOSE_FILE?channel:kind==WRITE_FILE&&!newline.isChecked()?"SENZA_A_CAPO "+body:body;}
 boolean write(){if(syncing)return true;String text=candidate(value.getText().toString());if(text.length()>8192){value.setError("Accorcia il testo prima di cambiare opzione.");return false;}value.setError(null);syncing=true;expression.setText(text);expression.setSelection(expression.length());syncing=false;return true;}
 static void watch(EditText edit,Runnable action){edit.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void onTextChanged(CharSequence s,int a,int b,int c){action.run();}public void afterTextChanged(Editable e){}});}
}
