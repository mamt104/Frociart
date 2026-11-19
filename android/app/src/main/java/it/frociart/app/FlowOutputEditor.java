package it.frociart.app;
import android.content.Context;import android.text.*;import android.view.View;import android.widget.*;
final class FlowOutputEditor {
 final Ui.Toggle newline;final TextView hint;final EditText expression;final FlowModel.Node node;
 FlowOutputEditor(Context owner,LinearLayout host,EditText expression,FlowModel.Node node){
  this.expression=expression;this.node=node;newline=new Ui.Toggle(owner,"Vai a capo dopo la stampa",null);newline.setTag("output-newline");newline.setSingleLine(false);newline.setMaxLines(3);host.addView(newline,new LinearLayout.LayoutParams(-1,-2));newline.setMinimumHeight(Ui.dp(owner,56));hint=Ui.note(owner,"Disattiva per unire il risultato alla stampa successiva.");host.addView(hint);
  newline.setOnClickListener(v->{try{expression.setText(FlowOutput.toggle(expression.getText().toString(),node.kind,!newline.isChecked()));expression.setSelection(expression.length());}catch(IllegalArgumentException e){expression.setError(e.getMessage());}read();});expression.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void onTextChanged(CharSequence s,int a,int b,int c){read();}public void afterTextChanged(Editable e){}});read();
 }
 void read(){FlowModel.IoText parsed=FlowModel.ioText(node,expression.getText().toString());boolean visible=FlowModel.isIo(node.kind)&&parsed.kind==FlowModel.OUTPUT;newline.setVisibility(visible?View.VISIBLE:View.GONE);hint.setVisibility(visible?View.VISIBLE:View.GONE);newline.setChecked(FlowOutput.newline(parsed.text));}
}
