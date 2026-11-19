"""Targeted JVM event/keyboard policy checks; Android views and a real IME are NOT exercised."""
from pathlib import Path
import os,subprocess,json,hashlib

ROOT=Path(__file__).resolve().parent.parent
P=ROOT
S=P/'app/src/main/java/it/frociart/app'
O=ROOT/'test-results/trial-focus';O.mkdir(parents=True,exist_ok=True)
H=O/'keyboard-host';H.mkdir(exist_ok=True)
J=Path('C:/Program Files/JetBrains/IntelliJ IDEA 2026.1.3/jbr/bin')
temp=O/'temp';temp.mkdir(exist_ok=True)
env=dict(os.environ,TEMP=str(temp),TMP=str(temp))

def write(name,text):
    file=H/name;file.parent.mkdir(parents=True,exist_ok=True);file.write_text(text,encoding='utf-8')

def method(text,signature):
    start=text.index(signature);i=text.index('{',start);depth=1;i+=1
    while depth:
        if text[i]=='{':depth+=1
        elif text[i]=='}':depth-=1
        i+=1
    return text[start:i]

source=(S/'Ui.java').read_text(encoding='utf-8-sig')
write('Ui.java','import android.content.Context; import android.os.Build; import android.view.*; import android.widget.EditText;\npublic class Ui {\n'+method(source,'public static void dismissKeyboard(')+'\n'+method(source,'public static void inputSubmission(')+'\n}\n')
stubs={
 'android/os/IBinder.java':'package android.os; public interface IBinder {}',
 'android/os/Build.java':'package android.os; public class Build {public static class VERSION {public static int SDK_INT;}}',
 'android/content/Context.java':'package android.content; public class Context {public static final String INPUT_METHOD_SERVICE="input_method";public Object service;public Object getSystemService(String key){return service;}}',
 'android/view/WindowInsets.java':'package android.view; public class WindowInsets {public static class Type {public static int ime(){return 8;}}}',
 'android/view/WindowInsetsController.java':'package android.view; public class WindowInsetsController {public int hidden;public void hide(int type){hidden|=type;}}',
 'android/view/KeyEvent.java':'package android.view; public class KeyEvent {public static final int KEYCODE_ENTER=66,ACTION_DOWN=0,ACTION_UP=1;int key,action;boolean canceled;public KeyEvent(int a,int k,boolean c){action=a;key=k;canceled=c;}public int getKeyCode(){return key;}public int getAction(){return action;}public boolean isCanceled(){return canceled;}}',
 'android/view/View.java':'package android.view; import android.content.Context;import android.os.IBinder;public class View {public boolean focused=true;public Context context=new Context();public IBinder token=new IBinder(){};public WindowInsetsController controller=new WindowInsetsController();public IBinder getWindowToken(){return token;}public WindowInsetsController getWindowInsetsController(){return controller;}public Context getContext(){return context;}public void clearFocus(){focused=false;}}',
 'android/view/inputmethod/InputMethodManager.java':'package android.view.inputmethod;import android.os.IBinder;public class InputMethodManager {public int calls;public IBinder token;public boolean hideSoftInputFromWindow(IBinder t,int flags){calls++;token=t;return true;}}',
 'android/view/inputmethod/EditorInfo.java':'package android.view.inputmethod;public class EditorInfo {public static final int IME_ACTION_SEND=4,IME_ACTION_DONE=6,IME_FLAG_NO_EXTRACT_UI=268435456;}',
 'android/widget/EditText.java':'package android.widget;import android.view.*;public class EditText extends View {public interface Listener {boolean call(EditText v,int action,KeyEvent event);}public int imeOptions;public Listener listener;public void setImeOptions(int value){imeOptions=value;}public void setOnEditorActionListener(Listener value){listener=value;}public boolean event(int action,KeyEvent event){return listener.call(this,action,event);}}',
}
for name,text in stubs.items():write(name,text)
write('KeyboardChecks.java','''import android.widget.EditText;import android.view.*;import android.view.inputmethod.*;import android.os.Build;
public class KeyboardChecks {
 static int checks;static void check(boolean ok,String message){if(!ok)throw new AssertionError(message);checks++;System.out.println("PASS "+message);}
 public static void main(String[] args){
  EditText input=new EditText();final int[] submits={0};Ui.inputSubmission(input,()->submits[0]++);
  check(input.imeOptions==(EditorInfo.IME_ACTION_SEND|EditorInfo.IME_FLAG_NO_EXTRACT_UI),"IME exposes Send and does not force fullscreen editing");
  check(input.event(EditorInfo.IME_ACTION_SEND,null)&&submits[0]==1,"soft Send submits once");
  check(input.event(EditorInfo.IME_ACTION_DONE,null)&&submits[0]==2,"compatible Done submits once");
  check(!input.event(0,null)&&submits[0]==2,"unrelated IME actions do not submit");
  check(input.event(0,new KeyEvent(KeyEvent.ACTION_DOWN,KeyEvent.KEYCODE_ENTER,false))&&submits[0]==2,"Enter down is consumed without submission");
  check(input.event(0,new KeyEvent(KeyEvent.ACTION_UP,KeyEvent.KEYCODE_ENTER,false))&&submits[0]==3,"Enter release submits exactly once");
  check(input.event(0,new KeyEvent(KeyEvent.ACTION_UP,KeyEvent.KEYCODE_ENTER,true))&&submits[0]==3,"canceled Enter does not submit");
  check(!input.event(0,new KeyEvent(KeyEvent.ACTION_UP,67,false))&&submits[0]==3,"ordinary typing is not intercepted");
  Build.VERSION.SDK_INT=26;InputMethodManager old=new InputMethodManager();input.context.service=old;Ui.dismissKeyboard(input);
  check(!input.focused&&old.calls==1&&old.token==input.token,"API26 clears focus and hides using the original window token");
  check(input.controller.hidden==0,"API26 does not call newer inset APIs");
  Build.VERSION.SDK_INT=35;EditText modern=new EditText();InputMethodManager manager=new InputMethodManager();modern.context.service=manager;Ui.dismissKeyboard(modern);
  check(!modern.focused&&manager.calls==1&&modern.controller.hidden==WindowInsets.Type.ime(),"API35 requests both keyboard dismissal paths");
  EditText detached=new EditText();detached.token=null;detached.controller=null;detached.context.service=manager;Ui.dismissKeyboard(detached);
  check(!detached.focused&&manager.calls==1,"detached input with no token is safe");
  Ui.dismissKeyboard(null);check(true,"null input is safe");
  System.out.println("PASS KEYBOARD POLICY "+checks+" checks. Android IME visibility: NOT_RUN (JVM adapters only).");
 }
}''')
classes=H/'classes';classes.mkdir(exist_ok=True)
for name,command in [('keyboard-compile',[J/'javac.exe','-encoding','UTF-8','-d',classes,*H.rglob('*.java')]),('keyboard-policy',[J/'java.exe','-cp',classes,'KeyboardChecks'])]:
    r=subprocess.run([str(x) for x in command],env=env,capture_output=True,timeout=90)
    (O/(name+'.log')).write_bytes(r.stdout+r.stderr)
    print(name,r.returncode,(r.stdout+r.stderr).decode(errors='replace')[-2500:],flush=True)
    if r.returncode:raise SystemExit(r.returncode)

panel=(S/'FlowTutorPanel.java').read_text(encoding='utf-8-sig')
main=(S/'MainActivity.java').read_text(encoding='utf-8-sig')
contracts={
 'trial hides footer': 'workspaceFooter.setVisibility(trial?View.GONE:View.VISIBLE)' in panel,
 'trial hides stale verdict': 'resultNotice.setVisibility(!trial&&lastFeedback!=null?' in panel,
 'trial hides expand command': 'expandCanvas.setVisibility(trial?View.GONE:View.VISIBLE)' in panel,
 'trial uses read-only chart': 'canvas.setReadOnly(true)' in method(panel,'private void run()'),
 'trial restores editor and hides IME': all(x in method(panel,'private void closeInlineRun()') for x in ['Ui.dismissKeyboard(inlineInput)','canvas.setReadOnly(false)','setExpandedCanvas(expandedBeforeRun)']),
 'inline button and IME share handler': 'Ui.inputSubmission(inlineInput,this::submitInlineRun)' in panel,
 'inline rejected input returns before dismissal': method(panel,'private void submitInlineRun()').index('signalFeedback(false);return;')<method(panel,'private void submitInlineRun()').index('Ui.dismissKeyboard'),
 'lesson value button and IME share handler': 'Ui.inputSubmission(v,send)' in panel and 's.primary("Invia il valore","right",send)' in panel,
 'free editor shares same IME handler': 'Ui.inputSubmission(runInput,this::submitInput)' in main,
 'free editor dismisses only accepted input': 'if(runner.submitInput' in method(main,'private void submitInput()') and 'Ui.dismissKeyboard(runInput)' in method(main,'private void submitInput()'),
 'free editor hides mission check during trial': 'mission==null||runVisible?View.GONE:View.VISIBLE' in main,
 'real output is shown': 'String.join(" · ",r.output.subList' in panel,
}
assert all(contracts.values()),contracts
(O/'targeted-source-contracts.json').write_text(json.dumps({'checks':contracts,'status':'PASS','limitations':'Source contracts are not Android UI, gesture or IME execution.'},indent=2),encoding='utf-8')
print('PASS',len(contracts),'source integration contracts. These do not exercise Android UI.')
