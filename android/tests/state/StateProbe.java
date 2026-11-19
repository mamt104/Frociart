package it.frociart.statechecks;

import android.app.Activity;
import android.app.Dialog;
import android.app.Instrumentation;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.os.SystemClock;
import android.util.Log;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.TextView;
import java.io.File;
import java.io.FileOutputStream;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.TimeUnit;

/** Four native component/controller regressions against the installed application APK.
 * Fixture setup and canvas callbacks use reflection; production dialog controls use
 * performClick and one native Back key. This is not a complete manual-touch journey. */
public final class StateProbe extends Instrumentation {
    private interface Action {void run()throws Exception;}
    private interface Condition {boolean value()throws Exception;}
    private Activity activity;private Object originalDocument;private String originalId,testId;
    private Map<String,?> originalPreferences;private boolean originalMotion,snapshotReady;
    private File directory;private long deadline;private int checks,errors;
    private volatile CountDownLatch blockedIo;
    private final StringBuilder report=new StringBuilder();

    @Override public void onCreate(Bundle args){super.onCreate(args);start();}
    @Override public void onStart(){
        long start=SystemClock.uptimeMillis();deadline=start+55000;
        report.append("SCOPE Native component/controller checks. Reflection fixtures/callbacks, production dialogs, View.performClick and native Back. No full manual gesture or FPS claim.\n");
        try{
            File internal=getTargetContext().getFilesDir();
            if(internal==null||!internal.isAbsolute())throw new IllegalStateException("STATE_ENV target internal files directory is unavailable; no relative report path will be used");
            File candidate=new File(internal,"state-1.2.1");
            if(!candidate.isDirectory()&&!candidate.mkdirs())throw new IllegalStateException("STATE_ENV cannot create report directory "+candidate);
            directory=candidate;
            report.append("REPORT_FILE ").append(new File(directory,"report.txt").getAbsolutePath()).append('\n');
            activity=startActivitySync(new Intent().setClassName("it.frociart.app","it.frociart.app.MainActivity").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            ui(()->{
                originalId=(String)get(activity,"projectId");originalDocument=call(get(activity,"document"),"copy");
                originalPreferences=new HashMap<>(((SharedPreferences)get(activity,"prefs")).getAll());
                originalMotion=(Boolean)get(type("Ui"),"motion");snapshotReady=true;set(type("Ui"),"motion",false);
                call(canvas(),"setMotionEnabled",false);dismiss();testId=(String)call(get(activity,"store"),"newId");
                ((SharedPreferences)get(activity,"prefs")).edit().putBoolean("auto_"+testId,false).apply();
            });
            boundaryInsertion();invalidEndEdge();lateValidation();cancelConnection();
        }catch(Throwable failure){errors++;report.append("STATE_ERROR ").append(Log.getStackTraceString(failure)).append('\n');Log.e("FROCIART_STATE","State regression failed",failure);}
        finally{
            if(blockedIo!=null)blockedIo.countDown();
            try{restore();}catch(Throwable failure){errors++;report.append("STATE_ERROR cleanup ").append(Log.getStackTraceString(failure)).append('\n');}
            String finished=finished(start);
            if(directory!=null)try(FileOutputStream file=new FileOutputStream(new File(directory,"report.txt"))){file.write((report.toString()+finished).getBytes("UTF-8"));}
                catch(Exception failure){errors++;report.append("STATE_ERROR report storage ").append(Log.getStackTraceString(failure)).append('\n');Log.e("FROCIART_STATE","Report write failed",failure);finished=finished(start);}
            report.append(finished);
            Bundle result=new Bundle();result.putString("stream",report.toString());Log.i("FROCIART_STATE",report.toString());finish(errors==0?Activity.RESULT_OK:Activity.RESULT_CANCELED,result);
        }
    }

    private String finished(long start){return "STATE_FINISHED checks="+checks+" errors="+errors+" elapsed_ms="+(SystemClock.uptimeMillis()-start)+"\n";}

    private void boundaryInsertion()throws Exception{
        report.append("CASE 1: valid boundary coordinates, automatic layout off, actual picker and added block\n");
        final Object[] source={null};final String[] oldTarget={null};
        ui(()->{
            Object doc=document(),start=node("START","Inizio",99800,99500),decision=node("DECISION","true",100000,100000),end=node("END","Fine",99800,99700);
            nodes(doc).add(start);nodes(doc).add(decision);nodes(doc).add(end);link(doc,start,decision,"next");link(doc,decision,end,"true");link(doc,decision,end,"false");
            checkNoEngineErrors(doc);fixture(doc);source[0]=decision;oldTarget[0]=id(end);call(activity,"onAddAfterRequested",id(decision));
        });
        clickSheetTag("false");clickSheetDescription("Aggiungi Output");clickSheetText("Applica modifiche");
        ui(()->{
            Object doc=get(activity,"document"),added=nodes(doc).get(nodes(doc).size()-1);
            check((Float)get(added,"x")==100000f&&(Float)get(added,"y")==100000f,"STATE-1 positive x/y are clamped on the new node, not only the drop center");
            check(oldTarget[0].equals(get(call(doc,"outgoing",id(added),"next"),"to")),"STATE-1 boundary insertion retains the old continuation");
            assertSaved(doc,"STATE-1 positive boundary");
        });
        ui(()->{
            Object doc=document(),start=node("START","Inizio",-100000,-99500),process=node("PROCESS","totale = 1",-99800,-99800),end=node("END","Fine",-99500,-100000);
            nodes(doc).add(start);nodes(doc).add(process);nodes(doc).add(end);link(doc,start,process,"next");link(doc,process,end,"next");checkNoEngineErrors(doc);fixture(doc);
            // A blank destination outside every node: use the same callback as a completed + drag.
            call(activity,"onConnectionDropRequested",id(process),null,-100000f,-100000f);
        });
        clickSheetDescription("Aggiungi Output");clickSheetText("Applica modifiche");
        ui(()->{Object doc=get(activity,"document"),added=nodes(doc).get(nodes(doc).size()-1);
            check((Float)get(added,"x")==-100000f&&(Float)get(added,"y")==-100000f,"STATE-1 negative drop subtracts block size without escaping coordinate limits");assertSaved(doc,"STATE-1 negative boundary");});
    }

    private void invalidEndEdge()throws Exception{
        report.append("CASE 2: remove only an erroneous outgoing End edge, then Undo\n");
        final String[] edgeId={null};final Set<String>[] originalEdges=new Set[]{null};final int[] count={0};
        ui(()->{
            Object doc=call(activity,"starter"),end=kindNode(doc,"END"),process=kindNode(doc,"PROCESS");Object wrong=edge(id(end),id(process),"next");edges(doc).add(wrong);
            call(type("FlowCodec"),"encode",doc);fixture(doc);edgeId[0]=(String)get(wrong,"id");originalEdges[0]=edgeIds(doc);count[0]=nodes(doc).size();call(activity,"onEdgeEditRequested",edgeId[0]);
        });
        awaitSheetText("Collegamento");ui(()->check(find(sheetRoot(),"Cambia arrivo",0)==null&&required(sheetRoot(),"Scollega",0).isEnabled(),"STATE-2 End edge exposes Scollega and does not permit a new outgoing connection"));
        clickSheetText("Scollega");
        ui(()->{Object doc=get(activity,"document");Set<String> expected=new HashSet<>(originalEdges[0]);expected.remove(edgeId[0]);
            check(nodes(doc).size()==count[0]&&edgeIds(doc).equals(expected),"STATE-2 unlink removes only the chosen edge and keeps every node");((View)get(activity,"undoButton")).performClick();});
        ui(()->check(nodes(get(activity,"document")).size()==count[0]&&edgeIds(get(activity,"document")).equals(originalEdges[0]),"STATE-2 one Undo restores the erroneous edge without duplicating it"));
    }

    private void lateValidation()throws Exception{
        report.append("CASE 3: validation callback queued behind a latch cannot replace Home or a new editor\n");
        ui(()->fixture(call(activity,"starter")));withPausedIo(()->{call(activity,"showValidation");call(activity,"showHome");});
        ui(()->check((Boolean)get(activity,"hubVisible")&&get(activity,"activeSheet")==null,"STATE-3 delayed validation leaves Home visible without an old result sheet"));
        final Object[] expectedSheet={null};final String[] edited={null};ui(()->fixture(call(activity,"starter")));
        withPausedIo(()->{call(activity,"showValidation");edited[0]=id(kindNode(get(activity,"document"),"PROCESS"));call(activity,"editNode",edited[0]);expectedSheet[0]=get(activity,"activeSheet");
            ((EditText)required(sheetRoot(),"Operazione",1)).setText("totale = 42");});
        ui(()->check(get(activity,"activeSheet")==expectedSheet[0]&&edited[0].equals(get(activity,"visibleEditorId"))
                &&"totale = 42".contentEquals(((EditText)required(sheetRoot(),"Operazione",1)).getText()),"STATE-3 delayed validation preserves the newly opened editor and its typed draft"));
        clickSheetText("Scarta questa modifica");
    }

    private void withPausedIo(Action beforeRelease)throws Exception{
        final ExecutorService[] executor={null};ui(()->executor[0]=(ExecutorService)get(activity,"io"));
        CountDownLatch entered=new CountDownLatch(1),release=new CountDownLatch(1);blockedIo=release;
        executor[0].execute(()->{entered.countDown();try{release.await(8,TimeUnit.SECONDS);}catch(InterruptedException interrupted){Thread.currentThread().interrupt();}});
        try{if(!entered.await(4,TimeUnit.SECONDS))throw new IllegalStateException("I/O queue did not enter the controlled pause");ui(beforeRelease);}
        finally{release.countDown();blockedIo=null;}
        // This barrier runs after validation; a main-thread barrier then runs after its posted callback.
        executor[0].submit(()->{}).get(4,TimeUnit.SECONDS);ui(()->{});
    }

    private void cancelConnection()throws Exception{
        report.append("CASE 4: X/Back from reconnect confirmation and navigation Home clear pending connection\n");
        ui(()->fixture(call(activity,"starter")));openReconnectConfirmation();clickSheetDescription("Chiudi pannello");assertCancelled("STATE-4 X closes confirmation without a pending connection");
        openReconnectConfirmation();check(await(()->((Dialog)get(get(activity,"activeSheet"),"dialog")).getWindow().getDecorView().hasWindowFocus(),3000),"STATE-4 reconnection dialog owns focus before native Back");
        sendKeyDownUpSync(KeyEvent.KEYCODE_BACK);check(await(()->get(activity,"activeSheet")==null,3000),"STATE-4 native Back closes reconnection confirmation");assertCancelled("STATE-4 Back leaves no pending connection");
        beginOnCanvas();ui(()->call(activity,"showHome"));
        ui(()->check((Boolean)get(activity,"hubVisible")&&!(Boolean)call(canvas(),"isConnecting"),"STATE-4 Home closes the canvas connection mode"));
        ui(()->call(activity,"openEditor"));ui(()->check(!(Boolean)call(canvas(),"isConnecting"),"STATE-4 returning from Home does not resurrect a pending connection"));
    }
    private void beginOnCanvas()throws Exception{
        ui(()->{Object from=kindNode(get(activity,"document"),"PROCESS");call(activity,"showDestinationPicker",id(from),"next");});clickSheetText("Scegli sul foglio");
        check(await(()->get(activity,"activeSheet")==null&&(Boolean)call(canvas(),"isConnecting"),3000),"STATE-4 Scegli sul foglio starts a real pending connection");
    }
    private void openReconnectConfirmation()throws Exception{
        beginOnCanvas();ui(()->{Object from=kindNode(get(activity,"document"),"PROCESS"),to=kindNode(get(activity,"document"),"END");
            call(activity,"onConnectionTargetRequested",id(from),"next",id(to),(Float)get(to,"x"),(Float)get(to,"y"));});awaitSheetText("Cambiare l’arrivo?");
    }
    private void assertCancelled(String label){ui(()->check(get(activity,"activeSheet")==null&&!(Boolean)call(canvas(),"isConnecting"),label));}

    private void checkNoEngineErrors(Object doc)throws Exception{int count=0;for(Object issue:(List<?>)call(type("FlowEngine"),"validate",doc))if((Boolean)get(issue,"error"))count++;check(count==0,"STATE-1 starting boundary document has no structural or expression errors");}
    private void assertSaved(Object doc,String label)throws Exception{
        String encoded=(String)call(type("FlowCodec"),"encode",doc);check(encoded.equals(call(type("FlowCodec"),"encode",call(type("FlowCodec"),"decode",encoded))),label+" codec accepts and round-trips the actual model");
        check((Boolean)call(activity,"saveNow")&&encoded.equals(call(type("FlowCodec"),"encode",call(get(activity,"store"),"load",testId))),label+" real FlowStore writes and reloads the QA project");
    }
    private void fixture(Object doc)throws Exception{
        dismiss();call(activity,"stopRun");call(canvas(),"cancelConnection");
        ((SharedPreferences)get(activity,"prefs")).edit().putBoolean("auto_"+testId,false).apply();call(activity,"switchDocument",doc,testId,false);
        if(get(activity,"document")!=doc||(Boolean)get(activity,"autoArrange"))throw new IllegalStateException("Fixture did not open in manual layout");
    }
    private void restore()throws Exception{
        if(activity==null)return;
        if(snapshotReady)ui(()->{
            dismiss();call(activity,"stopRun");call(canvas(),"cancelConnection");set(activity,"validationRequest",((Integer)get(activity,"validationRequest"))+1);
            ((android.os.Handler)get(activity,"handler")).removeCallbacks((Runnable)get(activity,"saveTask"));
            set(activity,"document",originalDocument);set(activity,"projectId",originalId);set(activity,"dirty",false);call(canvas(),"setDocument",originalDocument,true);
            if(testId!=null){call(get(activity,"drafts"),"removeProject",testId);if(!(Boolean)call(get(activity,"store"),"delete",testId))throw new IllegalStateException("QA project cleanup failed");}
            SharedPreferences.Editor edit=((SharedPreferences)get(activity,"prefs")).edit().clear();
            for(Map.Entry<String,?> entry:originalPreferences.entrySet()){String key=entry.getKey();Object value=entry.getValue();
                if(value instanceof String)edit.putString(key,(String)value);else if(value instanceof Boolean)edit.putBoolean(key,(Boolean)value);else if(value instanceof Integer)edit.putInt(key,(Integer)value);
                else if(value instanceof Long)edit.putLong(key,(Long)value);else if(value instanceof Float)edit.putFloat(key,(Float)value);else if(value instanceof Set)edit.putStringSet(key,new HashSet<>((Set<String>)value));}
            if(!edit.commit())throw new IllegalStateException("Preference restore failed");set(type("Ui"),"motion",originalMotion);activity.finish();
        });else ui(()->activity.finish());
    }

    private void clickSheetText(String text)throws Exception{clickSheet(text,0);}private void clickSheetDescription(String text)throws Exception{clickSheet(text,1);}private void clickSheetTag(String text)throws Exception{clickSheet(text,2);}
    private void clickSheet(String value,int mode)throws Exception{
        checkTime();if(!await(()->get(activity,"activeSheet")!=null&&find(sheetRoot(),value,mode)!=null,3000))throw new IllegalStateException("Missing dialog control: "+value);
        ui(()->{View control=required(sheetRoot(),value,mode);View target=control;while(!target.isClickable()&&target.getParent() instanceof View)target=(View)target.getParent();if(!target.isEnabled()||!target.performClick())throw new IllegalStateException("Dialog control could not be activated: "+value);});ui(()->{});
    }
    private void awaitSheetText(String text)throws Exception{if(!await(()->get(activity,"activeSheet")!=null&&find(sheetRoot(),text,0)!=null,3000))throw new IllegalStateException("Expected dialog: "+text);}
    private View sheetRoot()throws Exception{return(View)get(get(activity,"activeSheet"),"root");}
    private void dismiss()throws Exception{Object sheet=get(activity,"activeSheet");if(sheet!=null)((Dialog)get(sheet,"dialog")).dismiss();}
    private View find(View view,String text,int mode){Object actual=mode==2?view.getTag():mode==1?view.getContentDescription():view instanceof TextView?((TextView)view).getText():null;if(actual!=null&&text.equals(actual.toString()))return view;
        if(view instanceof ViewGroup)for(int i=0;i<((ViewGroup)view).getChildCount();i++){View result=find(((ViewGroup)view).getChildAt(i),text,mode);if(result!=null)return result;}return null;}
    private View required(View view,String text,int mode){View found=find(view,text,mode);if(found==null)throw new IllegalStateException("Missing control: "+text);return found;}
    private Class<?> type(String name)throws Exception{return Class.forName("it.frociart.app."+name,true,activity.getClassLoader());}
    private Object document()throws Exception{return type("FlowModel$Document").getDeclaredConstructor().newInstance();}
    private Object node(String kind,String text,float x,float y)throws Exception{return type("FlowModel$Node").getConstructor(int.class,String.class,float.class,float.class).newInstance((Integer)get(type("FlowModel"),kind),text,x,y);}
    private Object edge(String from,String to,String branch)throws Exception{return type("FlowModel$Edge").getConstructor(String.class,String.class,String.class).newInstance(from,to,branch);}
    private void link(Object doc,Object from,Object to,String branch)throws Exception{edges(doc).add(edge(id(from),id(to),branch));}
    @SuppressWarnings("unchecked") private List<Object> nodes(Object doc)throws Exception{return(List<Object>)get(doc,"nodes");}
    @SuppressWarnings("unchecked") private List<Object> edges(Object doc)throws Exception{return(List<Object>)get(doc,"edges");}
    private Object kindNode(Object doc,String name)throws Exception{int kind=(Integer)get(type("FlowModel"),name);for(Object node:nodes(doc))if((Integer)get(node,"kind")==kind)return node;throw new IllegalStateException("Missing node kind "+name);}
    private String id(Object node)throws Exception{return(String)get(node,"id");}
    private Set<String> edgeIds(Object doc)throws Exception{Set<String> ids=new HashSet<>();for(Object edge:edges(doc))ids.add(id(edge));return ids;}
    private Object canvas()throws Exception{return get(activity,"canvas");}
    private static Object get(Object target,String name)throws Exception{Class<?> type=target instanceof Class?(Class<?>)target:target.getClass();Field field=type.getDeclaredField(name);field.setAccessible(true);return field.get(target instanceof Class?null:target);}
    private static void set(Object target,String name,Object value)throws Exception{Class<?> type=target instanceof Class?(Class<?>)target:target.getClass();Field field=type.getDeclaredField(name);field.setAccessible(true);field.set(target instanceof Class?null:target,value);}
    private static Object call(Object target,String name,Object...args)throws Exception{Class<?> type=target instanceof Class?(Class<?>)target:target.getClass();for(Method method:type.getDeclaredMethods())if(method.getName().equals(name)&&method.getParameterTypes().length==args.length){method.setAccessible(true);return method.invoke(target instanceof Class?null:target,args);}throw new NoSuchMethodException(name);}
    private void checkTime(){if(SystemClock.uptimeMillis()>deadline)throw new IllegalStateException("State checks exceeded the 55-second active-run budget");}
    private void check(boolean ok,String text){checks++;if(!ok)throw new AssertionError(text);report.append("PASS ").append(text).append('\n');}
    private boolean await(Condition condition,long timeout)throws Exception{long until=Math.min(deadline,SystemClock.uptimeMillis()+timeout);do{final boolean[] ready={false};ui(()->ready[0]=condition.value());if(ready[0])return true;SystemClock.sleep(40);}while(SystemClock.uptimeMillis()<until);return false;}
    private void ui(Action action){final Throwable[] failure={null};runOnMainSync(()->{try{action.run();}catch(Throwable error){failure[0]=error;}});if(failure[0]!=null)throw new RuntimeException(failure[0]);}
}
