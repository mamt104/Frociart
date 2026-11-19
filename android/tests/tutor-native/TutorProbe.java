package it.frociart.tutorchecks;

import android.app.*;
import android.content.*;
import android.content.pm.ActivityInfo;
import android.graphics.*;
import android.os.*;
import android.util.Log;
import android.view.*;
import android.view.accessibility.AccessibilityWindowInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.*;
import java.io.*;
import java.lang.reflect.*;
import java.util.*;

/** Real native views, input injection and Android storage. Restricted to a dedicated emulator. */
public final class TutorProbe extends Instrumentation {
    Activity activity;File directory;String phase="course",scenario="portrait";
    int checks,lessons,steps,wrongCases;StringBuilder report=new StringBuilder();
    interface Action{void run()throws Exception;}
    interface Condition{boolean value()throws Exception;}
    @Override public void onCreate(Bundle b){super.onCreate(b);if(b!=null){phase=b.getString("phase","course");scenario=b.getString("scenario","portrait");}start();}
    @Override public void onStart(){
        directory=new File(getTargetContext().getExternalFilesDir(null),"tutor-qa/"+phase+"-"+scenario);directory.mkdirs();
        int errors=0;
        try{
            require(Build.HARDWARE.equals("ranchu")||Build.HARDWARE.equals("goldfish"),"Dedicated emulator, never a personal phone");
            android.accessibilityservice.AccessibilityServiceInfo flags=getUiAutomation().getServiceInfo();flags.flags|=android.accessibilityservice.AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS;getUiAutomation().setServiceInfo(flags);
            Intent i=new Intent().setClassName(getTargetContext().getPackageName(),"it.frociart.app.MainActivity").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK|Intent.FLAG_ACTIVITY_CLEAR_TASK);
            activity=startActivitySync(i);settle();
            if(phase.equals("seed"))seed();
            else if(phase.equals("upgrade"))upgrade();
            else if(phase.equals("course"))course();
            else if(phase.equals("persist"))persist();
            else if(phase.equals("workshop"))workshop();
            else if(phase.equals("layout"))layout();
            else if(phase.equals("rotation"))rotationReview();
            else if(phase.equals("backup"))backupRecovery();
            else if(phase.equals("surface"))surfaceReview();
            else if(phase.equals("legacy-restore"))legacyRestore();
            else if(phase.equals("editor-restore"))editorRestore();
            else if(phase.equals("edge-drag"))edgeDragReview();
            else if(phase.equals("side-plus"))sidePlusReview();
            else if(phase.equals("insertion"))insertionReview();
            else if(phase.equals("direct"))directReview();
            else if(phase.equals("drop-fixes")){dropFixesReview();trashReview();}
            else if(phase.equals("lesson-interaction"))lessonInteractionReview();
            else if(phase.equals("tap-edit"))tapEditReview();
            else if(phase.equals("learning-flow"))learningFlowReview();
            else if(phase.equals("learning-design")){boolean previous=type("Ui").getField("dark").getBoolean(null);type("Ui").getField("dark").setBoolean(null,scenario.contains("dark"));try{learningFlowReview();}finally{type("Ui").getField("dark").setBoolean(null,previous);}}
            else if(phase.equals("reconnect"))reconnectReview();
            else if(phase.equals("sizing"))sizingReview();
            else if(phase.equals("group-hold"))groupHoldReview();
            else if(phase.equals("decision-fixes"))decisionFixes();
            else if(phase.equals("cache"))cacheReview();
            else if(phase.equals("hold-fixes"))holdFixes();
            else throw new IllegalArgumentException("Unknown phase");
        }catch(Throwable t){errors++;report.append("NATIVE_ERROR ").append(Log.getStackTraceString(t)).append('\n');try{snap("99-failure");}catch(Throwable ignored){}}
        finally{
            if(errors>0){try{MotionEvent release=MotionEvent.obtain(SystemClock.uptimeMillis(),SystemClock.uptimeMillis(),MotionEvent.ACTION_UP,1,1,0);release.setSource(InputDevice.SOURCE_TOUCHSCREEN);getUiAutomation().injectInputEvent(release,true);release.recycle();}catch(Throwable ignored){}}
            String done="TUTOR_NATIVE_FINISHED checks="+checks+" lessons="+lessons+" steps="+steps+" wrongCases="+wrongCases+" errors="+errors;
            report.append(done).append('\n');Log.i("FROCIART_TUTOR_QA",report.toString());
            try{write(new File(directory,"report.txt"),report.toString());}catch(Exception ignored){}
            Bundle result=new Bundle();result.putString("stream",report.toString());finish(errors==0?Activity.RESULT_OK:Activity.RESULT_CANCELED,result);
        }
    }
    View edgeCanvas;String testProject;
    Object freeDocument()throws Exception{return get(activity,"document");}
    Object edge(String branch)throws Exception{return call(freeDocument(),"outgoing","check",branch);}
    String target(String branch)throws Exception{return(String)get(edge(branch),"to");}
    int undoCount()throws Exception{return((Deque<?>)get(activity,"undo")).size();}
    void resetEdgeFixture()throws Exception{
        ui(()->{closeSheet();call(activity,"openEditor");Object d=type("FlowExamples").getMethod("parity").invoke(null);
            set(d,"title","QA trascinamento rami");Object q=call(d,"find","check");set(q,"trueLabel","Accetta");set(q,"falseLabel","Rifiuta");set(q,"trueColor","#365CD9");set(q,"falseColor","#BA3058");
            String id=(String)call(get(activity,"store"),"newId");testProject=id;call(activity,"switchDocument",d,id,false);edgeCanvas=(View)get(activity,"canvas");call(edgeCanvas,"setReadOnly",false);});settle();ui(()->call(edgeCanvas,"fitToContent"));settle();
    }
    Object edgeVisual(String branch)throws Exception{call(edgeCanvas,"ensureCache");for(Object v:(List<?>)get(edgeCanvas,"edges"))if(branch.equals(get(get(v,"edge"),"branch")))return v;throw new AssertionError("Branch geometry missing");}
    float[] handlePoint(String branch,boolean label)throws Exception{
        final float[][] value={null};ui(()->{Object v=edgeVisual(branch);float x,y;
            if(label){RectF b=(RectF)get(v,"labelBounds");x=(float)call(edgeCanvas,"screenX",b.centerX());y=(float)call(edgeCanvas,"screenY",b.centerY());}
            else{float[] point=new float[2];call(edgeCanvas,"edgeHandle",v,point);x=point[0];y=point[1];}
            int[] origin=new int[2];edgeCanvas.getLocationOnScreen(origin);value[0]=new float[]{origin[0]+x,origin[1]+y};});return value[0];
    }
    float[] nodePoint(String id)throws Exception{final float[][] p={null};ui(()->{Object n=call(freeDocument(),"find",id);int kind=(int)get(n,"kind");float width=(float)type("FlowModel").getMethod("width",int.class).invoke(null,kind),height=(float)type("FlowModel").getMethod("height",int.class).invoke(null,kind);int[] origin=new int[2];edgeCanvas.getLocationOnScreen(origin);p[0]=new float[]{origin[0]+(float)call(edgeCanvas,"screenX",(float)get(n,"x")+width/2),origin[1]+(float)call(edgeCanvas,"screenY",(float)get(n,"y")+height/2)};});return p[0];}
    long edgeDown;
    void pointer(int action,float[] p)throws Exception{android.view.accessibility.AccessibilityNodeInfo root=getUiAutomation().getRootInActiveWindow();long until=SystemClock.uptimeMillis()+2000;while((root==null||root.getPackageName()==null)&&SystemClock.uptimeMillis()<until){pause(25);root=getUiAutomation().getRootInActiveWindow();}if(root==null||!getTargetContext().getPackageName().contentEquals(root.getPackageName()))throw new AssertionError("Touch prevented: active package="+(root==null?"null":root.getPackageName()));if(action==MotionEvent.ACTION_DOWN)edgeDown=SystemClock.uptimeMillis();MotionEvent e=MotionEvent.obtain(edgeDown,SystemClock.uptimeMillis(),action,p[0],p[1],0);e.setSource(InputDevice.SOURCE_TOUCHSCREEN);boolean ok=getUiAutomation().injectInputEvent(e,true);e.recycle();require(ok,"Native pointer event "+action);pause(35);}
    void moveArrow(float[] from,float[] to,boolean finish)throws Exception{pointer(MotionEvent.ACTION_DOWN,from);for(int i=1;i<=10;i++)pointer(MotionEvent.ACTION_MOVE,new float[]{from[0]+(to[0]-from[0])*i/10,from[1]+(to[1]-from[1])*i/10});if(finish)pointer(MotionEvent.ACTION_UP,to);settle();}
    void selectDecision()throws Exception{ui(()->call(edgeCanvas,"select","check"));settle();ui(()->call(edgeCanvas,"fitToContent"));settle();}
    float[] sidePoint(boolean yes)throws Exception{final float[][] p={null};ui(()->{call(edgeCanvas,"updateConnectionButtonGeometry");RectF r=(RectF)get(edgeCanvas,yes?"truePlusHit":"falsePlusHit");require(r.width()>=48*activity.getResources().getDisplayMetrics().density-1,"Each lateral plus has a 48dp touch target");int[] o=new int[2];edgeCanvas.getLocationOnScreen(o);p[0]=new float[]{o[0]+r.centerX(),o[1]+r.centerY()};});return p[0];}
    void quickSnap(String name)throws Exception{Bitmap image=getUiAutomation().takeScreenshot();require(image!=null,"Native animation frame available");try(FileOutputStream out=new FileOutputStream(new File(directory,name+".png"))){image.compress(Bitmap.CompressFormat.PNG,100,out);}image.recycle();}
    float[] worldPoint(float x,float y)throws Exception{final float[][] p={null};ui(()->{int[] origin=new int[2];edgeCanvas.getLocationOnScreen(origin);p[0]=new float[]{origin[0]+(float)call(edgeCanvas,"screenX",x),origin[1]+(float)call(edgeCanvas,"screenY",y)};});return p[0];}
    void groupHoldReview()throws Exception{
        resetInsertionFixture(2);ui(()->{Object d=freeDocument(),n=call(d,"find","dragged");set(n,"x",(float)get(call(d,"find","start"),"x")-24);set(n,"y",300f);type("FlowInsertion").getMethod("apply",type("FlowModel$Document"),String.class,String.class,String.class).invoke(null,d,"dragged","drop","next");call(edgeCanvas,"notifyModelChanged");call(edgeCanvas,"applySelection",new LinkedHashSet<String>(Arrays.asList("dragged","end")),false);});settle();ui(()->call(edgeCanvas,"fitToContent"));settle();String before=encode(freeDocument());float[] p=nodePoint("dragged");pointer(MotionEvent.ACTION_DOWN,p);pause(850);pointer(MotionEvent.ACTION_UP,p);settle();require(encode(freeDocument()).equals(before)&&undoCount()==0,"Long press within a group preserves all graph links and history");
    }
    void directReview()throws Exception{
        groupHoldReview();
        for(String label:new String[]{"Input","Operazione","Output"}){
            resetInsertionFixture(2);if(label.equals("Output")){rotate(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE);ui(()->call(edgeCanvas,"fitToContent"));settle();}final String original=encode(freeDocument());final int count=((List<?>)get(freeDocument(),"nodes")).size();
            float[] drop=insertionPoint(0);ui(()->call(activity,"showAdd",0f,0f,null,null));settle();View title=findNow(label);require(title!=null,"Real palette tile rendered: "+label);View tile=(View)title.getParent();require(tile.isLongClickable(),"Palette tile exposes long drag");ui(()->tile.requestRectangleOnScreen(new Rect(0,0,tile.getWidth(),tile.getHeight()),true));pause(350);require(full(tile),"Palette tile fully visible after scroll");Rect r=bounds(tile);float[] from={r.exactCenterX(),r.exactCenterY()};
            pointer(MotionEvent.ACTION_DOWN,from);await(()->sheet()==null,4000,"Native long press dismisses palette after starting drag");await(()->get(edgeCanvas,"paletteNode")!=null,4000,"Native drag starts a real canvas ghost");
            for(int i=1;i<=14;i++)pointer(MotionEvent.ACTION_MOVE,new float[]{from[0]+(drop[0]-from[0])*i/14,from[1]+(drop[1]-from[1])*i/14});
            pause(150);require((boolean)get(edgeCanvas,"insertionReady"),"Menu-to-line drag exposes the blue ready state");require(((List<?>)get(freeDocument(),"nodes")).size()==count&&undoCount()==0,"Palette preview creates no node and no history");snap("01-menu-preview-"+label);
            pointer(MotionEvent.ACTION_UP,drop);settle();require(((List<?>)get(freeDocument(),"nodes")).size()==count+1,"Actual native drop creates exactly one block");Object added=call(freeDocument(),"find",get(dropEdge(),"to"));require(added!=null&&!get(added,"id").equals("end")&&call(freeDocument(),"outgoing",get(added,"id"),"next")!=null,"New menu block is inserted with its continuation");require(undoCount()==1,"New block and local spacing share one Undo");snap("02-menu-inserted-"+label);ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Undo restores all content and positions after palette insertion");if(label.equals("Output"))rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        }
        for(boolean cancel:new boolean[]{false,true}){
            resetInsertionFixture(2);String before=encode(freeDocument());float[] drop=insertionPoint(0);ui(()->call(activity,"showAdd",0f,0f,null,null));settle();View tile=(View)findNow("Decisione").getParent();Rect r=bounds(tile);float[] from={r.exactCenterX(),r.exactCenterY()};pointer(MotionEvent.ACTION_DOWN,from);await(()->get(edgeCanvas,"paletteNode")!=null&&sheet()==null,4000,"Decision palette starts a native drag");for(int i=1;i<=12;i++)pointer(MotionEvent.ACTION_MOVE,new float[]{from[0]+(drop[0]-from[0])*i/12,from[1]+(drop[1]-from[1])*i/12});pointer(MotionEvent.ACTION_UP,drop);settle();require(sheet()!=null&&texts(sheetRoot()).contains("Quale ramo prosegue?"),"New decision asks for a continuation before creation");require(encode(freeDocument()).equals(before)&&undoCount()==0,"Unconfirmed palette decision creates nothing");tapText(cancel?"Annulla inserimento":"No");if(cancel)require(encode(freeDocument()).equals(before)&&undoCount()==0,"Cancelled new decision leaves no node or history");else{Object n=call(freeDocument(),"find",get(dropEdge(),"to"));require(n!=null&&call(freeDocument(),"outgoing",get(n,"id"),"false")!=null&&call(freeDocument(),"outgoing",get(n,"id"),"true")==null,"New decision uses the explicitly chosen No branch");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(before),"One Undo removes a newly dragged decision and local layout");}
        }
        resetInsertionFixture(2);String noneBefore=encode(freeDocument());float[] noneDrop=insertionPoint(0);ui(()->call(activity,"showAdd",0f,0f,null,null));settle();View noneTile=(View)findNow("Decisione").getParent();Rect noneBounds=bounds(noneTile);float[] noneFrom={noneBounds.exactCenterX(),noneBounds.exactCenterY()};pointer(MotionEvent.ACTION_DOWN,noneFrom);await(()->get(edgeCanvas,"paletteNode")!=null&&sheet()==null,4000,"None scenario starts native decision drag");for(int i=1;i<=12;i++)pointer(MotionEvent.ACTION_MOVE,new float[]{noneFrom[0]+(noneDrop[0]-noneFrom[0])*i/12,noneFrom[1]+(noneDrop[1]-noneFrom[1])*i/12});pointer(MotionEvent.ACTION_UP,noneDrop);settle();require(findNow("Nessuno")!=null&&findNow("Annulla inserimento")!=null,"None and cancellation are separate controls");snap("05-branch-choice");tapText("Nessuno");Object noneNode=call(freeDocument(),"find",get(dropEdge(),"to"));require(noneNode!=null&&(int)get(noneNode,"kind")==3&&call(freeDocument(),"outgoing",get(noneNode,"id"),"true")==null&&call(freeDocument(),"outgoing",get(noneNode,"id"),"false")==null,"None inserts a decision with both exits free");require(call(freeDocument(),"find","end")!=null&&undoCount()==1,"None preserves the old target and one Undo");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(noneBefore),"Undo restores exact document after None insertion");
        resetInsertionFixture(2);ui(()->{Object d=freeDocument(),n=call(d,"find","dragged");set(n,"x",(float)get(call(d,"find","start"),"x")-24);set(n,"y",300f);type("FlowInsertion").getMethod("apply",type("FlowModel$Document"),String.class,String.class,String.class).invoke(null,d,"dragged","drop","next");call(edgeCanvas,"notifyModelChanged");});settle();ui(()->call(edgeCanvas,"fitToContent"));settle();
        final String original=encode(freeDocument());float[] from=nodePoint("dragged");pointer(MotionEvent.ACTION_DOWN,from);pause(550);require((float)get(edgeCanvas,"holdFill")==0&&!(boolean)get(edgeCanvas,"stickerDetached"),"Short hold has no fill or peel");pause(500);require((float)get(edgeCanvas,"holdFill")>0&&!(boolean)get(edgeCanvas,"stickerDetached"),"Hold fills progressively before detachment");quickSnap("03-hold-fill");pause(650);require((boolean)get(edgeCanvas,"stickerDetached")&&(float)get(edgeCanvas,"liftFraction")>.8f,"Long hold animates the sticker peel");require(undoCount()==0&&get(dropEdge(),"to").equals("dragged"),"Peel preview preserves actual graph and history");snap("04-lifted-bypass");
        float[] blank=worldPoint(620,320);for(int i=1;i<=10;i++)pointer(MotionEvent.ACTION_MOVE,new float[]{from[0]+(blank[0]-from[0])*i/10,from[1]+(blank[1]-from[1])*i/10});pointer(MotionEvent.ACTION_UP,blank);settle();require(get(dropEdge(),"to").equals("end")&&call(freeDocument(),"outgoing","dragged","next")==null,"Sticker release reconnects neighbours and detaches the moved block");require(undoCount()==1,"Peel and movement are one Undo");require(((TextView)get(activity,"hint")).getText().toString().contains("staccato"),"Release hint describes the actual reconnection");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Undo restores graph and coordinates before peel");
        pointer(MotionEvent.ACTION_DOWN,nodePoint("dragged"));pause(1950);pointer(MotionEvent.ACTION_CANCEL,nodePoint("dragged"));settle();require(encode(freeDocument()).equals(original)&&undoCount()==0,"Cancelled stationary peel leaves no mutation or history");
        decisionFixes();holdFixes();dropFixesReview();trashReview();
        resetEdgeFixture();ui(()->{Object n=call(freeDocument(),"find","check");set(n,"x",(float)get(n,"x")+170);call(edgeCanvas,"notifyModelChanged");});settle();String preArrange=encode(freeDocument());
        ui(()->call(activity,"arrangeOnce"));require(get(edgeCanvas,"layoutAnimator")!=null,"Riordina retains its node animation after camera fit");pause(100);require((float)get(edgeCanvas,"layoutFraction")>0&&(float)get(edgeCanvas,"layoutFraction")<1.1f,"Riordina produces an intermediate spring frame");settle();require(get(edgeCanvas,"layoutAnimator")==null,"Riordina reaches rest and stops animating");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(preArrange),"Animated Riordina Undo restores exact positions");

    }
    void sidePlusReview()throws Exception{
        resetEdgeFixture();selectDecision();
        ui(()->{RectF bottom=(RectF)get(edgeCanvas,"plusHit"),left=(RectF)get(edgeCanvas,"truePlusHit"),right=(RectF)get(edgeCanvas,"falsePlusHit");require(bottom.isEmpty(),"Decision has no bottom plus");require(left.right<right.left&&left.centerY()==right.centerY(),"Two distinct plus controls sit at the same level on opposite sides");require(get(edgeCanvas,"connectorAnimator")==null,"Decision handles create no idle animation timer");});
        snap("01-two-lateral-plus");String before=encode(freeDocument());String id=(String)get(edge("true"),"id");float[] to=nodePoint("odd");moveArrow(sidePoint(true),to,false);require(target("true").equals("even")&&undoCount()==0,"Dragging the left plus previews without changing the graph");snap("02-line-starts-at-left-side");pointer(MotionEvent.ACTION_UP,to);settle();
        require(target("true").equals("odd")&&target("false").equals("odd")&&id.equals(get(edge("true"),"id")),"Left plus directly retargets the Sì branch without swapping identities");require(undoCount()==1&&get(activity,"activeSheet")==null,"Side drag commits one Undo and opens no branch chooser");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(before),"Undo restores every field after dragging from a side");
        resetEdgeFixture();selectDecision();moveArrow(sidePoint(false),nodePoint("even"),true);require(target("false").equals("even")&&target("true").equals("even"),"Right plus directly retargets only No");
        resetEdgeFixture();ui(()->{((List<?>)get(freeDocument(),"edges")).remove(edge("false"));call(edgeCanvas,"notifyModelChanged");});selectDecision();moveArrow(sidePoint(false),nodePoint("odd"),true);require(target("false").equals("odd")&&undoCount()==1,"A missing No branch can be drawn directly from its side plus");
        resetEdgeFixture();selectDecision();float[] from=sidePoint(false);pointer(MotionEvent.ACTION_DOWN,from);pointer(MotionEvent.ACTION_UP,from);settle();require(sheet()!=null&&!texts(sheetRoot()).contains("Scegli il ramo"),"Tapping No plus opens block creation with the branch already chosen");ui(()->closeSheet());
        resetEdgeFixture();selectDecision();String cancel=encode(freeDocument());moveArrow(sidePoint(true),nodePoint("odd"),false);pointer(MotionEvent.ACTION_CANCEL,nodePoint("odd"));settle();require(encode(freeDocument()).equals(cancel)&&undoCount()==0,"Interrupted side drag preserves the previous graph and history");
        resetEdgeFixture();selectDecision();moveArrow(sidePoint(true),nodePoint("start"),true);require(target("true").equals("even")&&undoCount()==0,"Side drop on Inizio is rejected without losing the old arrow");
        resetEdgeFixture();selectDecision();rotate(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE);ui(()->call(edgeCanvas,"fitToContent"));settle();moveArrow(sidePoint(false),nodePoint("even"),true);require(target("false").equals("even"),"Side controls keep their branch in landscape");snap("03-side-plus-landscape");rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        resetEdgeFixture();selectDecision();ui(()->call(edgeCanvas,"setDark",true));snap("04-two-lateral-plus-dark");
    }
    void resetInsertionFixture(int kind)throws Exception{
        ui(()->{closeSheet();call(activity,"openEditor");Object d=type("FlowExamples").getMethod("blank").invoke(null);set(d,"title","Inserimento su freccia");set(call(d,"find","end"),"y",650f);Object moving=type("FlowModel$Node").newInstance();set(moving,"id","dragged");set(moving,"kind",kind);set(moving,"text",kind==4?"x":kind==2?"x = 1":kind==3?"x > 0":"\"Ciao\"");set(moving,"x",800f);set(moving,"y",240f);((List)get(d,"nodes")).add(moving);Object first=((List<?>)get(d,"edges")).get(0);set(first,"id","drop");String id=(String)call(get(activity,"store"),"newId");testProject=id;call(activity,"switchDocument",d,id,false);edgeCanvas=(View)get(activity,"canvas");call(edgeCanvas,"setReadOnly",false);call(edgeCanvas,"setSnapToGrid",false);Field theme=type("Ui").getDeclaredField("dark");theme.setAccessible(true);call(edgeCanvas,"setDark",theme.getBoolean(null));call(edgeCanvas,"select","dragged");});settle();ui(()->call(edgeCanvas,"fitToContent"));settle();
    }
    float[] insertionPoint(float distanceDp)throws Exception{
        final float[][] p={null};ui(()->{call(edgeCanvas,"ensureCache");Object visual=null;for(Object e:(List<?>)get(edgeCanvas,"edges"))if("drop".equals(get(get(e,"edge"),"id")))visual=e;
            float[] points=(float[])get(visual,"points");float best=-1,cx=0,cy=0;
            for(int i=2;i+1<points.length;i+=2){float length=(float)Math.hypot(points[i]-points[i-2],points[i+1]-points[i-1]);if(length>best){best=length;cx=(points[i]+points[i-2])/2;cy=(points[i+1]+points[i-1])/2;}}
            int[] origin=new int[2];edgeCanvas.getLocationOnScreen(origin);p[0]=new float[]{origin[0]+(float)call(edgeCanvas,"screenX",cx)+distanceDp*activity.getResources().getDisplayMetrics().density,origin[1]+(float)call(edgeCanvas,"screenY",cy)};
        });return p[0];
    }
    Object dropEdge()throws Exception{for(Object e:(List<?>)get(freeDocument(),"edges"))if("drop".equals(get(e,"id")))return e;throw new AssertionError("drop edge missing");}
    void insertionReview()throws Exception{
        branchInsertionReview();
        for(int kind:new int[]{4,2,5}){
            resetInsertionFixture(kind);String original=encode(freeDocument());float[] from=nodePoint("dragged"),far=insertionPoint(65),near=insertionPoint(6);
            moveArrow(from,far,false);float faint=(float)get(edgeCanvas,"insertionStrength");require(!(boolean)get(edgeCanvas,"insertionReady"),"Approaching line glows before the drop becomes valid");snap("insert-01-approach-"+kind);
            pointer(MotionEvent.ACTION_MOVE,near);settle();float strong=(float)get(edgeCanvas,"insertionStrength");require(strong>faint&&(boolean)get(edgeCanvas,"insertionReady"),"Blue radial glow grows with block proximity");
            require(get(dropEdge(),"to").equals("end")&&((List<?>)get(freeDocument(),"edges")).size()==1,"Drag preview preserves all graph links");snap("insert-02-ready-"+kind);pointer(MotionEvent.ACTION_UP,near);settle();
            require(get(dropEdge(),"to").equals("dragged")&&get(call(freeDocument(),"outgoing","dragged","next"),"to").equals("end"),"Native drop inserts the block with both real links: "+kind);require(undoCount()==1,"Move and insertion share exactly one Undo");
            ui(()->{require((boolean)call(activity,"saveNow"),"Inserted flow saved successfully");require(encode(call(get(activity,"store"),"load",testProject)).equals(encode(freeDocument())),"Reopened project retains the actual inserted graph");call(activity,"undo");});settle();require(encode(freeDocument()).equals(original),"One Undo restores all original coordinates and links");ui(()->call(activity,"redo"));settle();require(get(dropEdge(),"to").equals("dragged"),"Redo restores insertion");snap("insert-03-committed-"+kind);
        }
        resetInsertionFixture(3);String original=encode(freeDocument());moveArrow(nodePoint("dragged"),insertionPoint(0),true);require(sheet()!=null&&texts(sheetRoot()).contains("Quale ramo prosegue?"),"A dropped decision asks which branch continues");require(get(dropEdge(),"to").equals("end"),"Decision does not guess its continuation before the choice");tapText("No");require(get(dropEdge(),"to").equals("dragged")&&call(freeDocument(),"outgoing","dragged","true")==null&&get(call(freeDocument(),"outgoing","dragged","false"),"to").equals("end"),"Chosen No branch continues and Sì remains free");require(undoCount()==1,"Decision choice remains part of the original drag Undo");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Decision Undo restores graph and position together");
        resetInsertionFixture(3);moveArrow(nodePoint("dragged"),insertionPoint(0),true);tapText("Annulla inserimento");require(get(dropEdge(),"to").equals("end")&&((List<?>)get(freeDocument(),"edges")).size()==1,"Cancelling decision insertion keeps the existing line intact");
        resetInsertionFixture(2);float[] near=insertionPoint(0);moveArrow(nodePoint("dragged"),near,false);pointer(MotionEvent.ACTION_CANCEL,near);settle();require(get(dropEdge(),"to").equals("end")&&!((boolean)get(edgeCanvas,"insertionReady")),"Cancelled contact removes the glow and cannot splice the graph");
        resetInsertionFixture(2);String before=encode(freeDocument());ui(()->call(edgeCanvas,"setReadOnly",true));moveArrow(nodePoint("dragged"),insertionPoint(0),true);require(encode(freeDocument()).equals(before)&&undoCount()==0,"Execution mode blocks insertion and movement");
        resetInsertionFixture(2);rotate(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE);ui(()->call(edgeCanvas,"fitToContent"));settle();moveArrow(nodePoint("dragged"),insertionPoint(0),true);require(get(dropEdge(),"to").equals("dragged"),"Block insertion works in landscape");snap("insert-04-landscape");rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
    }
    void branchInsertionReview()throws Exception{
        resetEdgeFixture();ui(()->{Object n=type("FlowModel$Node").newInstance();set(n,"id","dragged");set(n,"kind",2);set(n,"text","x = 1");set(n,"x",1000f);set(n,"y",250f);((List)get(freeDocument(),"nodes")).add(n);set(edge("true"),"id","drop");set(activity,"dirty",true);call(activity,"saveNow");call(edgeCanvas,"notifyModelChanged");call(edgeCanvas,"setSnapToGrid",false);Field theme=type("Ui").getDeclaredField("dark");theme.setAccessible(true);call(edgeCanvas,"setDark",theme.getBoolean(null));call(edgeCanvas,"select","dragged");Field palette=type("Ui").getDeclaredField("dark");palette.setAccessible(true);call(edgeCanvas,"setDark",palette.getBoolean(null));});settle();ui(()->call(edgeCanvas,"fitToContent"));settle();
        moveArrow(nodePoint("dragged"),insertionPoint(0),true);require(get(dropEdge(),"to").equals("dragged")&&get(dropEdge(),"branch").equals("true")&&target("false").equals("odd")&&get(call(freeDocument(),"outgoing","dragged","next"),"to").equals("even"),"Insertion on a routed Sì branch keeps No and the old destination intact");snap("insert-00-routed-branch");
    }

    String edgeState()throws Exception{StringBuilder value=new StringBuilder();for(Object e:(List<?>)get(freeDocument(),"edges"))value.append(get(e,"id")).append(get(e,"from")).append(get(e,"to")).append(get(e,"branch"));return value.toString();}
    void checkRenderedEdges()throws Exception{ui(()->{call(edgeCanvas,"ensureCache");Set<String> actual=new HashSet<>(),visual=new HashSet<>();for(Object e:(List<?>)get(freeDocument(),"edges"))actual.add((String)get(e,"id"));for(Object v:(List<?>)get(edgeCanvas,"edges"))visual.add((String)get(get(v,"edge"),"id"));require(actual.equals(visual),"Rendered edge identities exactly match the real graph; no floating stale arrows");});}
    void attachedLinear()throws Exception{resetInsertionFixture(2);ui(()->{Object d=freeDocument(),n=call(d,"find","dragged");set(n,"x",(float)get(call(d,"find","start"),"x")-24);set(n,"y",300f);type("FlowInsertion").getMethod("apply",type("FlowModel$Document"),String.class,String.class,String.class).invoke(null,d,"dragged","drop","next");call(edgeCanvas,"notifyModelChanged");call(edgeCanvas,"fitToContent");});settle();}
    void cacheReview()throws Exception{attachedLinear();float[] point=nodePoint("dragged");pointer(MotionEvent.ACTION_DOWN,point);pause(1950);float[] blank=worldPoint(620,320);pointer(MotionEvent.ACTION_MOVE,blank);pointer(MotionEvent.ACTION_UP,blank);settle();checkRenderedEdges();}
    void holdFixes()throws Exception{
        attachedLinear();String before=encode(freeDocument());float[] from=nodePoint("dragged"),blank=worldPoint(620,320);String links=edgeState();
        pointer(MotionEvent.ACTION_DOWN,from);pause(550);require((float)get(edgeCanvas,"holdFill")==0,"Shorter grace period keeps a short press visually idle");pointer(MotionEvent.ACTION_UP,from);require(edgeState().equals(links)&&undoCount()==0,"Short press preserves graph and history");
        pointer(MotionEvent.ACTION_DOWN,from);pointer(MotionEvent.ACTION_MOVE,blank);pause(1850);require((float)get(edgeCanvas,"holdFill")==0&&!(boolean)get(edgeCanvas,"stickerDetached"),"Long ordinary drag never starts filling or peeling");pointer(MotionEvent.ACTION_UP,blank);settle();require(edgeState().equals(links),"Ordinary drag preserves all links");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(before),"Ordinary drag Undo restores all fields");
        pointer(MotionEvent.ACTION_DOWN,nodePoint("dragged"));pause(1250);require((float)get(edgeCanvas,"holdFill")>0&&(float)get(edgeCanvas,"holdFill")<1,"Only stationary press starts gradual fill after grace");Object haptics=get(edgeCanvas,"holdHaptics");require((int)get(haptics,"stage")>=0,"Tactile milestones follow the real filling progress");pointer(MotionEvent.ACTION_MOVE,blank);pause(900);require((float)get(edgeCanvas,"holdFill")==0&&!(boolean)get(edgeCanvas,"stickerDetached")&&(int)get(haptics,"stage")==-1,"Moving during fill cancels visual and haptic progress together");pointer(MotionEvent.ACTION_CANCEL,blank);settle();
        cacheReview();
    }
    void clickSheetX()throws Exception{ui(()->{ArrayDeque<View> q=new ArrayDeque<>();q.add((View)get(sheet(),"root"));while(!q.isEmpty()){View v=q.remove();if("Chiudi pannello".contentEquals(v.getContentDescription()==null?"":v.getContentDescription())){require(v.performClick(),"Actual sheet X is clicked");return;}if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++)q.add(((ViewGroup)v).getChildAt(i));}throw new AssertionError("Missing sheet X");});ui(()->{});}
    void decisionFixes()throws Exception{
        for(int mode=0;mode<3;mode++){
            resetEdgeFixture();final int variant=mode;ui(()->{if(variant==1)set(edge("false"),"to",get(edge("true"),"to"));if(variant==2)((List<?>)get(freeDocument(),"edges")).remove(edge("false"));call(edgeCanvas,"notifyModelChanged");call(edgeCanvas,"select","check");});settle();String original=encode(freeDocument()),links=edgeState();float[] from=nodePoint("check"),free=worldPoint(850,300);
            moveArrow(from,free,true);require(edgeState().equals(links)&&undoCount()==1,"Ordinary decision movement preserves all branches: "+mode);ui(()->call(activity,"undo"));settle();
            pointer(MotionEvent.ACTION_DOWN,nodePoint("check"));pause(1800);require((boolean)get(edgeCanvas,mode==0?"stickerAmbiguous":"stickerDetached"),"Stationary hold prepares actual detach: "+mode);pointer(MotionEvent.ACTION_MOVE,free);pointer(MotionEvent.ACTION_UP,free);settle();require(sheet()==null,"A peeled decision dropped in blank space opens no menu: "+mode);
            for(Object e:(List<?>)get(freeDocument(),"edges"))require(!get(e,"from").equals("check")&&!get(e,"to").equals("check"),"No incident arrow survives a blank decision detach");require(undoCount()==1,"Blank detach and move are one Undo");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Blank detach Undo restores exact positions and branches");
        }
        for(String answer:new String[]{"Rifiuta","Nessuno","Annulla inserimento","X"}){
            resetEdgeFixture();ui(()->{Object line=call(freeDocument(),"outgoing","odd","next");set(line,"id","drop");call(edgeCanvas,"notifyModelChanged");call(edgeCanvas,"setSnapToGrid",false);});settle();String original=encode(freeDocument());float[] from=nodePoint("check"),drop=insertionPoint(0);pointer(MotionEvent.ACTION_DOWN,from);pause(1800);pointer(MotionEvent.ACTION_MOVE,drop);RectF released=drawnBounds("check");require((boolean)get(edgeCanvas,"insertionReady"),"Detached decision detects an actual new arrow");pointer(MotionEvent.ACTION_UP,drop);settle();require(sheet()!=null&&findNow("Annulla inserimento")!=null&&findNow("Annulla stacco")==null,"Only the new connection branch chooser opens");require(Math.abs(drawnBounds("check").left-released.left)<1,"Pending connection retains the dropped visual pose");require(encode(freeDocument()).equals(original)&&undoCount()==0,"Pending insertion preserves stored model and history");
            if(answer.equals("X"))clickSheetX();else tapText(answer);settle();
            if(answer.equals("Annulla inserimento")){require(encode(freeDocument()).equals(original)&&undoCount()==0,"Explicit named cancellation restores the pre-drag document");continue;}
            if(answer.equals("X")){require(get(dropEdge(),"to").equals("end"),"X preserves the target arrow");for(Object e:(List<?>)get(freeDocument(),"edges"))require(!get(e,"from").equals("check")&&!get(e,"to").equals("check"),"X leaves the decision disconnected");Object n=call(freeDocument(),"find","check");require(Math.abs((float)get(n,"x")-released.left)<1&&Math.abs((float)get(n,"y")-released.top)<1,"X keeps the exact dropped decision position");}
            else require(get(dropEdge(),"to").equals("check"),"Choosing a branch inserts the decision into the actual arrow");require(undoCount()==1,"Transfer or X free placement is one Undo");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Undo restores exact complete graph after branch choice or X");
        }
        resetEdgeFixture();ui(()->{set(edge("false"),"to",get(edge("true"),"to"));call(edgeCanvas,"notifyModelChanged");call(edgeCanvas,"select","check");});settle();float[] yes=handlePoint("true",false),no=handlePoint("false",false);float density=activity.getResources().getDisplayMetrics().density;require(no[0]-yes[0]>=48*density&&Math.abs(yes[1]-no[1])<1,"Converging branch handles are separate left/right touch targets");require((int)get(edgeVisual("true"),"color")==Color.parseColor("#365CD9")&&(int)get(edgeVisual("false"),"color")==Color.parseColor("#BA3058"),"Each handle keeps its own custom branch colour");for(String branch:new String[]{"true","false"}){float[] grip=handlePoint(branch,false);RectF leftHit=(RectF)get(edgeCanvas,"truePlusHit"),rightHit=(RectF)get(edgeCanvas,"falsePlusHit");require(!leftHit.contains(grip[0],grip[1])&&!rightHit.contains(grip[0],grip[1]),"Converging "+branch+" grip stays outside both lateral port hit areas");pointer(MotionEvent.ACTION_DOWN,grip);require((int)get(edgeCanvas,"gesture")==((Integer)get(edgeCanvas,"EDGE_PENDING")),"Selected decision does not steal the "+branch+" incoming grip");pointer(MotionEvent.ACTION_CANCEL,grip);}snap("01-separated-converging-branches");
        for(String branch:new String[]{"true","false"}){
            resetEdgeFixture();ui(()->call(edgeCanvas,"clearSelection"));settle();String before=encode(freeDocument()),oldTarget=target(branch),other=target(branch.equals("true")?"false":"true");float[] free=worldPoint(720,350);moveArrow(handlePoint(branch,false),free,true);require(sheet()!=null&&texts(sheetRoot()).contains("Aggiungi"),"Dropping existing "+branch+" arrow in blank space opens block palette");require(encode(freeDocument()).equals(before)&&undoCount()==0,"Opening branch palette does not change graph or history");ui(()->closeSheet());require(encode(freeDocument()).equals(before),"Closing the branch palette preserves the old route");
            moveArrow(handlePoint(branch,false),free,true);tapText("Input");require(sheet()!=null,"Branch palette opens the real node editor");tapText("Applica modifiche");settle();Object added=call(freeDocument(),"find",target(branch));require(added!=null&&!get(added,"id").equals(oldTarget)&&call(freeDocument(),"outgoing",get(added,"id"),"next")!=null,"New node is spliced into the chosen branch");Object tail=call(freeDocument(),"outgoing",get(added,"id"),"next");require(get(tail,"to").equals(oldTarget)&&target(branch.equals("true")?"false":"true").equals(other),"Old continuation and the other decision branch both survive");require(undoCount()==1,"Branch insertion has one complete Undo");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(before),"Branch insertion Undo restores exact graph and coordinates");
            ui(()->call(edgeCanvas,"fitToContent"));settle();free=worldPoint(720,350);moveArrow(handlePoint(branch,false),free,true);require(findNow("Scollega questo collegamento")!=null,"Blank drop exposes explicit disconnect");tapText("Scollega questo collegamento");require(call(freeDocument(),"outgoing","check",branch)==null&&target(branch.equals("true")?"false":"true").equals(other),"Disconnect changes only the chosen branch");require(undoCount()==1,"Explicit disconnect has one Undo");checkRenderedEdges();ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(before),"Disconnect Undo restores exact graph and branch metadata");
        }
    }
    RectF drawnBounds(String id)throws Exception{final RectF[] value={null};ui(()->{call(edgeCanvas,"ensureCache");Object visual=((Map<?,?>)get(edgeCanvas,"nodeIndex")).get(id);value[0]=new RectF((RectF)get(visual,"bounds"));});return value[0];}
    void clickImmediate(String text)throws Exception{ui(()->{View view=findNow(text);if(view==null)throw new AssertionError("Missing real control: "+text);while(!view.isClickable()&&view.getParent() instanceof View)view=(View)view.getParent();require(view.performClick(),"Real control action: "+text);});ui(()->{});}
    void canvasFrame(String name)throws Exception{ui(()->{Bitmap image=Bitmap.createBitmap(edgeCanvas.getWidth(),edgeCanvas.getHeight(),Bitmap.Config.ARGB_8888);edgeCanvas.draw(new Canvas(image));try(FileOutputStream out=new FileOutputStream(new File(directory,name+".png"))){image.compress(Bitmap.CompressFormat.PNG,100,out);}image.recycle();});}
    void dropFixesReview()throws Exception{
        {resetEdgeFixture();ui(()->call(edgeCanvas,"setSnapToGrid",false));String original=encode(freeDocument());float[] from=nodePoint("check"),blank=worldPoint(850,300);pointer(MotionEvent.ACTION_DOWN,from);pause(1800);pointer(MotionEvent.ACTION_MOVE,blank);RectF release=drawnBounds("check");pointer(MotionEvent.ACTION_UP,blank);settle();require(sheet()==null,"Blank decision release has no chooser");Object moved=call(freeDocument(),"find","check");require(Math.abs((float)get(moved,"x")-release.left)<1&&Math.abs((float)get(moved,"y")-release.top)<1,"Blank drop commits the actual released pose");require(undoCount()==1,"Blank decision drop is one Undo");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Blank drop Undo is exact");}
        for(int kind:new int[]{2,4,5}){
            resetInsertionFixture(kind);String original=encode(freeDocument());float[] target=insertionPoint(36),onLine=insertionPoint(0),from=nodePoint("dragged");moveArrow(from,target,false);
            require((boolean)get(edgeCanvas,"insertionReady"),"Magnetic capture accepts a block 36dp from the arrow: "+kind);pointer(MotionEvent.ACTION_UP,target);
            Object n=call(freeDocument(),"find","dragged");float width=(float)type("FlowModel").getMethod("width",int.class).invoke(null,kind);float sx=(float)call(edgeCanvas,"screenX",(float)get(n,"x")+width/2);int[] origin=new int[2];edgeCanvas.getLocationOnScreen(origin);
            require(Math.abs(origin[0]+sx-onLine[0])<2,"Inserted block is centered on the arrow independently of the grid: "+kind);
            require(get(edgeCanvas,"edgeMotionAnimator")!=null&&get(edgeCanvas,"layoutAnimator")!=null,"Insertion starts connector fusion and elastic position settling together");canvasFrame("drop-03-fusion-start-"+kind);pause(100);canvasFrame("drop-04-fusion-middle-"+kind);require((float)get(edgeCanvas,"edgeMotionFraction")>0&&(float)get(edgeCanvas,"edgeMotionFraction")<1.1f,"Connector fusion produces intermediate frames");settle();canvasFrame("drop-05-fusion-end-"+kind);require(get(edgeCanvas,"edgeMotionAnimator")==null,"Connector fusion reaches rest and releases its animator");require(undoCount()==1,"Centering, insertion and local arrangement have one Undo");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Undo of magnetic insertion restores exact coordinates and topology");
        }
        // Only one canvas badge may be visible while a peeled block approaches a line.
        attachedLinear();float[] point=nodePoint("dragged");pointer(MotionEvent.ACTION_DOWN,point);pause(1800);float[] away=worldPoint(620,320);pointer(MotionEvent.ACTION_MOVE,away);pointer(MotionEvent.ACTION_MOVE,insertionPoint(6));
        require((int)call(edgeCanvas,"gestureHintMode")==2,"Insertion badge replaces the hold badge, so their text never overlaps");canvasFrame("drop-06-single-badge");pointer(MotionEvent.ACTION_CANCEL,away);settle();
    }
    void trashReview()throws Exception{
        for(int kind:new int[]{2,3,4,5}){
            if(kind==3)resetEdgeFixture();else{attachedLinear();final int k=kind;ui(()->{set(call(freeDocument(),"find","dragged"),"kind",k);call(edgeCanvas,"notifyModelChanged");});settle();}
            String id=kind==3?"check":"dragged",original=encode(freeDocument());int count=((List<?>)get(freeDocument(),"nodes")).size();final float[][] target={null};ui(()->{RectF r=(RectF)call(edgeCanvas,"trashBounds");int[] o=new int[2];edgeCanvas.getLocationOnScreen(o);target[0]=new float[]{o[0]+r.centerX(),o[1]+r.centerY()};});float[] from=nodePoint(id);
            pointer(MotionEvent.ACTION_DOWN,from);pause(1800);require((float)get(edgeCanvas,"trashReveal")>.8f,"Peel reveals animated trash for kind "+kind);canvasFrame("trash-01-ready-"+kind);pointer(MotionEvent.ACTION_MOVE,target[0]);pause(80);canvasFrame("trash-02-hover-"+kind);pointer(MotionEvent.ACTION_UP,target[0]);require(call(freeDocument(),"find",id)==null&&((List<?>)get(freeDocument(),"nodes")).size()==count-1,"Trash removes exactly the peeled block: "+kind);for(Object e:(List<?>)get(freeDocument(),"edges"))require(!get(e,"from").equals(id)&&!get(e,"to").equals(id),"Trash leaves no dangling connector");require(undoCount()==1,"Trash deletion and reconnection are one Undo");require(get(edgeCanvas,"trashFlightAnimator")!=null,"Deletion starts its shrinking flight");pause(100);canvasFrame("trash-03-flight-"+kind);pause(500);require(get(edgeCanvas,"trashFlightAnimator")==null&&get(edgeCanvas,"trashFlight")==null,"Trash animation reaches rest and releases its visual");ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(original),"Trash Undo restores exact block and every original branch");
        }
    }
    void homeMotionReview()throws Exception{
        final Field motion=type("Ui").getField("motion");final boolean previous=motion.getBoolean(null);final View[] page={null};final boolean enabled=android.animation.ValueAnimator.areAnimatorsEnabled();
        final boolean[] intermediate={false};final ViewTreeObserver.OnPreDrawListener[] sample={null};
        try{ui(()->{motion.setBoolean(null,true);call(activity,"showHome");page[0]=(View)get(activity,"root");require(!enabled||page[0].getAlpha()<1,"Home entrance starts before the new page's first frame");sample[0]=()->{float alpha=page[0].getAlpha();if(alpha>0&&alpha<1)intermediate[0]=true;return true;};page[0].getViewTreeObserver().addOnPreDrawListener(sample[0]);});pause(650);
            ui(()->{page[0].getViewTreeObserver().removeOnPreDrawListener(sample[0]);require(!enabled||intermediate[0],"Home entrance has actual intermediate drawn alpha frames");});
            ui(()->{require(page[0].getAlpha()==1&&page[0].getTranslationY()==0,"Home entrance settles at its exact native geometry");call(panel(),"showPath");page[0]=(View)get(activity,"root");require(!enabled||page[0].getAlpha()<1,"Lesson path opens through the shared page transition");});pause(650);
            ui(()->{require(page[0].getAlpha()==1&&page[0].getTranslationY()==0,"Lesson path finishes its spring transition");for(int navigation=0;navigation<12;navigation++){call(activity,"showHome");call(panel(),"showPath");}call(activity,"showHome");Field pending=type("Ui").getDeclaredField("pendingPageFrames");pending.setAccessible(true);require(pending.getInt(null)<=1,"Detached pages immediately release their pending first-frame callbacks");});pause(650);
            ui(()->{View root=(View)get(activity,"root");require(root.getAlpha()==1&&root.getTranslationY()==0&&get(panel(),"route").equals("home"),"Interrupted navigation settles on the latest home without a stale overlay");Field pending=type("Ui").getDeclaredField("pendingPageFrames");pending.setAccessible(true);require(pending.getInt(null)==0,"Settled navigation leaves no pending page callback");motion.setBoolean(null,false);call(activity,"showHome");root=(View)get(activity,"root");require(root.getAlpha()==1&&root.getTranslationY()==0,"Reduced motion opens home immediately");});
        }finally{ui(()->motion.setBoolean(null,previous));}
    }
    void lessonInteractionReview()throws Exception{
        homeMotionReview();
        final Object p=panel();final String prefName="frociart_tutor_interaction_qa_"+SystemClock.uptimeMillis();final File own=new File(getTargetContext().getFilesDir(),prefName);final Map<String,Object> saved=new LinkedHashMap<>();
        for(String key:new String[]{"directory","prefs","route","activeId","selected","editingNode","lesson","step","work"})saved.put(key,get(p,key));
        try{ui(()->{closeSheet();set(p,"directory",own);set(p,"prefs",activity.getSharedPreferences(prefName,0));});
            for(Object l:(Object[])type("FlowTutorContent").getMethod("all").invoke(null)){
                final String id=(String)get(l,"id");ui(()->{closeSheet();require(((File)call(p,"file",id)).getCanonicalFile().getParentFile().equals(own.getCanonicalFile()),"Lesson fixture path stays inside its new isolated directory");call(p,"openLesson",id);require((boolean)call(p,"loadWorkspace",id,call(l,"solution")),"Isolated lesson loads: "+id);set(get(p,"work"),"document",call(l,"solution"));set(p,"step",0);call(p,"showWorkspace");});settle();edgeCanvas=(View)get(p,"canvas");
                String candidate=null;for(Object n:(List<?>)get(document(),"nodes"))if((int)get(n,"kind")==3){candidate=(String)get(n,"id");break;}if(candidate==null)for(Object n:(List<?>)get(document(),"nodes"))if((int)get(n,"kind")!=0&&(int)get(n,"kind")!=1){candidate=(String)get(n,"id");break;}if(candidate==null)candidate=(String)get(((List<?>)get(document(),"nodes")).get(0),"id");final String selectedId=candidate;
                ui(()->call(edgeCanvas,"focusNodeAnimated",selectedId));pause(450);ui(()->call(p,"updateTask"));pause(400);require(((Set<?>)call(edgeCanvas,"getSelectedIds")).contains(selectedId),"Task refresh retains selected lesson block: "+id);require(((View)get(p,"edit")).isShown(),"Selected block is editable during a pending task: "+id);String beforeDoubleTap=encode(document());long beforeRevision=(long)get(get(p,"work"),"revision");doubleTapTeachingNode(selectedId);require(sheet()!=null&&selectedId.equals(get(p,"editingNode")),"Native double tap opens the chosen block editor: "+id);require(beforeDoubleTap.equals(encode(document())),"Native double tap preserves the complete lesson graph: "+id);require((long)get(get(p,"work"),"revision")==beforeRevision,"Native double tap creates no Undo: "+id);closeSheet();ui(()->UiButtonClick((View)get(p,"edit")));settle();require(selectedId.equals(get(p,"editingNode")),"Modify opens the selected block instead of the task block: "+id);closeSheet();
                ui(()->call(p,"run"));settle();require(get(p,"activeRunner")!=null&&(boolean)get(edgeCanvas,"readOnly"),"Real lesson trial opens: "+id);
                ui(()->{UiButtonClick((View)get(p,"runAdvance"));});settle();require((int)get(get(p,"activeRunner"),"steps")>0,"Real Un passo advances the interpreter: "+id);
                ui(()->call(edgeCanvas,"frameNodeAnimated",selectedId));pause(450);Object tapped=call(document(),"find",selectedId);int tappedKind=(int)get(tapped,"kind");float width=(float)type("FlowModel").getMethod("width",int.class).invoke(null,tappedKind),height=(float)type("FlowModel").getMethod("height",int.class).invoke(null,tappedKind);float[] tapPoint=worldPoint((float)get(tapped,"x")+width/2,(float)get(tapped,"y")+height/2);pointer(MotionEvent.ACTION_DOWN,tapPoint);pointer(MotionEvent.ACTION_UP,tapPoint);settle();require(!(boolean)get(edgeCanvas,"readOnly")&&sheet()!=null,"Trial tap returns to editing: "+id);require(selectedId.equals(get(p,"editingNode")),"Editor belongs to the tapped block: "+id);ui(()->call(p,"updateTask"));pause(350);require(selectedId.equals(get(p,"selected")),"Editor refresh never jumps back to the task: "+id);closeSheet();
            }
        }finally{ui(()->{closeSheet();call(p,"stopInlineRunner");for(Map.Entry<String,Object> e:saved.entrySet())set(p,e.getKey(),e.getValue());call(activity,"showHome");});
            String prefix=getTargetContext().getFilesDir().getCanonicalPath()+File.separator;require(own.getCanonicalPath().startsWith(prefix)&&own.getName().equals(prefName),"Cleanup is restricted to the new isolated lesson fixture");File[] files=own.listFiles();if(files!=null)for(File f:files){require(f.isFile()&&f.getCanonicalFile().getParentFile().equals(own.getCanonicalFile()),"Only own flat lesson fixture is deleted");require(f.delete(),"Temporary lesson document removed");}if(own.exists())require(own.delete(),"Temporary lesson directory removed");require(activity.deleteSharedPreferences(prefName),"Only new lesson test preferences removed");}
    }
    void doubleTapTeachingNode(String id)throws Exception{
        Object node=call(document(),"find",id);int kind=(int)get(node,"kind");float width=(float)type("FlowModel").getMethod("width",int.class).invoke(null,kind),height=(float)type("FlowModel").getMethod("height",int.class).invoke(null,kind);float[] point=worldPoint((float)get(node,"x")+width/2,(float)get(node,"y")+height/2);
        for(int tap=0;tap<2;tap++){pointer(MotionEvent.ACTION_DOWN,point);pointer(MotionEvent.ACTION_MOVE,new float[]{point[0]+1,point[1]+1});pointer(MotionEvent.ACTION_UP,new float[]{point[0]+1,point[1]+1});}pause(350);
    }
    void tapEditReview()throws Exception{
        final Object p=panel();final File originalDirectory=(File)get(p,"directory");final Object originalPreferences=get(p,"prefs");final String name="frociart_tap_qa_"+SystemClock.uptimeMillis();final File own=new File(getTargetContext().getFilesDir(),name);
        try{ui(()->{closeSheet();set(p,"directory",own);set(p,"prefs",activity.getSharedPreferences(name,0));Object lesson=type("FlowTutorContent").getMethod("lesson",String.class).invoke(null,"b14");call(p,"openLesson","b14");require((boolean)call(p,"loadWorkspace","b14",call(lesson,"seed")),"User video's exact lesson opens in an isolated fixture");call(p,"showWorkspace");});pause(700);edgeCanvas=(View)get(p,"canvas");
            for(int scenario=0;scenario<3;scenario++){
                final int mode=scenario;ui(()->{closeSheet();call(p,"stopInlineRunner");Object lesson=get(p,"lesson");set(get(p,"work"),"document",call(lesson,"seed"));call(p,"changed");call(edgeCanvas,"focusNodeAnimated","domanda");if(mode==2)call(edgeCanvas,"clearSelection");((TextView)get(p,"status")).setText("Prima riga del compito\nSeconda riga del compito");});pause(700);
                Object node=call(document(),"find","domanda");String before=encode(document());long revision=(long)get(get(p,"work"),"revision");float x=(float)get(node,"x"),y=(float)get(node,"y");float[] point=worldPoint(x+142,y+76);int[] location=new int[2];edgeCanvas.getLocationOnScreen(location);
                pointer(MotionEvent.ACTION_DOWN,point);if(mode==1){ui(()->edgeCanvas.setTranslationY(24*activity.getResources().getDisplayMetrics().density));pause(45);}pointer(MotionEvent.ACTION_MOVE,new float[]{point[0]+1,point[1]+1});pointer(MotionEvent.ACTION_UP,new float[]{point[0]+1,point[1]+1});if(mode==1)ui(()->edgeCanvas.setTranslationY(0));else{float[] afterTap=worldPoint(x+142,y+76);require(Math.abs(afterTap[0]-point[0])<1.5&&Math.abs(afterTap[1]-point[1])<1.5,"First tap keeps the block at its exact screen position: "+mode);}
                pointer(MotionEvent.ACTION_DOWN,point);pointer(MotionEvent.ACTION_MOVE,new float[]{point[0]+1,point[1]+1});pointer(MotionEvent.ACTION_UP,new float[]{point[0]+1,point[1]+1});pause(400);
                report.append("TAP_TRACE mode=").append(mode).append(" beforeY=").append(y).append(" afterY=").append(get(node,"y")).append(" status=").append(((TextView)get(p,"status")).getText()).append('\n');
                require(encode(document()).equals(before),"Double tap never moves a block or changes any connection: "+mode);require((long)get(get(p,"work"),"revision")==revision,"Double tap creates no Undo entry: "+mode);require(sheet()!=null&&"domanda".equals(get(p,"editingNode")),"Double tap opens the tapped decision editor: "+mode);snap("tap-editor-"+mode);closeSheet();
            }
        }finally{ui(()->{closeSheet();edgeCanvas.setTranslationY(0);set(p,"directory",originalDirectory);set(p,"prefs",originalPreferences);call(activity,"showHome");});File[] files=own.listFiles();if(files!=null)for(File f:files){require(f.isFile()&&f.getCanonicalFile().getParentFile().equals(own.getCanonicalFile()),"Cleanup only touches new tap fixtures");require(f.delete(),"Temporary tap fixture removed");}if(own.exists())require(own.delete(),"Temporary tap directory removed");activity.deleteSharedPreferences(name);}
    }
    void UiButtonClick(View view){require(view.isShown()&&view.isEnabled()&&view.performClick(),"Visible native action executes");}
    void edgeDragReview()throws Exception{
        resetEdgeFixture();String original=encode(freeDocument());String id=(String)get(edge("true"),"id");float[] grip=handlePoint("true",false),other=nodePoint("odd");
        snap("01-arrow-grips");moveArrow(grip,other,false);
        require(target("true").equals("even")&&undoCount()==0,"Arrow preview preserves the graph and creates no history");snap("02-arrow-live-preview");pointer(MotionEvent.ACTION_UP,other);settle();
        require(target("true").equals("odd"),"Native drag of the arrow tip changes the Sì destination directly");
        require(undoCount()==1&&get(activity,"activeSheet")==null,"One drag is one Undo without a confirmation sheet");
        require(id.equals(get(edge("true"),"id"))&&target("false").equals("odd"),"Edge identity and the other branch stay intact");
        ui(()->{Object expected=type("FlowCodec").getMethod("decode",String.class).invoke(null,original);set(call(expected,"outgoing","check","true"),"to","odd");require(encode(expected).equals(encode(freeDocument())),"Only the chosen destination changes; labels, colors and all positions are preserved");require((boolean)call(activity,"saveNow"),"Retargeted arrow is durably saved");require(encode(call(get(activity,"store"),"load",testProject)).equals(encode(freeDocument())),"Reopened file retains the new arrow destination");call(activity,"undo");});settle();
        require(encode(freeDocument()).equals(original),"One Undo restores the entire original document");ui(()->call(activity,"redo"));settle();require(target("true").equals("odd"),"Redo restores the arrow change");
        snap("03-new-arrival");resetEdgeFixture();moveArrow(handlePoint("false",true),nodePoint("even"),true);require(target("false").equals("even")&&target("true").equals("even"),"The No label itself can be dragged to a new destination");
        resetEdgeFixture();String beforeSelf=encode(freeDocument());moveArrow(handlePoint("true",false),nodePoint("check"),true);
        require(target("true").equals("check")&&target("false").equals("odd"),"Returning a decision arrow to itself preserves the other branch");
        ui(()->call(activity,"undo"));settle();require(encode(freeDocument()).equals(beforeSelf),"One Undo restores the original graph after a self-loop");
        for(String invalid:new String[]{"start","blank","same","cancel","readonly","resize","document"}){
            resetEdgeFixture();String before=encode(freeDocument());int[] o=new int[2];edgeCanvas.getLocationOnScreen(o);float[] end=invalid.equals("blank")?new float[]{o[0]+8,o[1]+edgeCanvas.getHeight()-8}:nodePoint(invalid.equals("start")?"start":invalid.equals("same")?"even":"odd");
            if(invalid.equals("readonly"))ui(()->call(edgeCanvas,"setReadOnly",true));
            moveArrow(handlePoint("true",false),end,false);
            if(invalid.equals("cancel"))pointer(MotionEvent.ACTION_CANCEL,end);
            else{if(invalid.equals("resize"))ui(()->edgeCanvas.layout(edgeCanvas.getLeft(),edgeCanvas.getTop(),edgeCanvas.getRight()-1,edgeCanvas.getBottom()));
                if(invalid.equals("document"))ui(()->call(edgeCanvas,"setDocument",freeDocument(),false));pointer(MotionEvent.ACTION_UP,end);}
            settle();require(encode(freeDocument()).equals(before)&&undoCount()==0,"Invalid or interrupted arrow drop preserves document and history: "+invalid);
        }
        resetEdgeFixture();ui(()->{call(edgeCanvas,"select","check");call(edgeCanvas,"setSelectionMode",true);});settle();String before=encode(freeDocument());moveArrow(handlePoint("true",false),nodePoint("odd"),true);require(target("true").equals("even"),"Multiselection does not retarget arrows");
        resetEdgeFixture();ui(()->{call(edgeCanvas,"select","check");});settle();require(edgeCanvas.getContentDescription().toString().contains("punta"),"Canvas exposes a description and keeps accessible line menus");
        rotate(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE);ui(()->{call(edgeCanvas,"clearSelection");call(edgeCanvas,"fitToContent");});settle();moveArrow(handlePoint("true",false),nodePoint("odd"),true);require(target("true").equals("odd"),"Direct arrow dragging works in landscape");snap("04-landscape-arrow");rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        ui(()->{closeSheet();call(activity,"showHome");});tapText("Laboratorio guidato");tapText("Apri il laboratorio");
        ui(()->{Object d=type("FlowExamples").getMethod("parity").invoke(null);set(get(panel(),"work"),"document",d);call(panel(),"changed");});settle();
        Object free=freeDocument();edgeCanvas=(View)get(panel(),"canvas");ui(()->call(edgeCanvas,"fitToContent"));settle();float[] start=handlePoint("true",false);
        // Shared canvas receives the same real touch events inside the guided workshop.
        final float[][] dest={null};ui(()->{Object n=call(get(get(panel(),"work"),"document"),"find","odd");int[] xy=new int[2];edgeCanvas.getLocationOnScreen(xy);dest[0]=new float[]{xy[0]+(float)call(edgeCanvas,"screenX",(float)get(n,"x")+116),xy[1]+(float)call(edgeCanvas,"screenY",(float)get(n,"y")+43)};});
        moveArrow(start,dest[0],true);require("odd".equals(get(call(get(get(panel(),"work"),"document"),"outgoing","check","true"),"to")),"Shared direct arrow gesture works in the guided workshop");require((boolean)call(get(panel(),"work"),"canUndo"),"Guided workshop keeps the real Undo snapshot");snap("05-guided-arrow");
    }
    void seed()throws Exception{
        ui(()->{call(activity,"openEditor");Object d=call(activity,"starter");set(d,"title","QA documento da conservare");String id=(String)call(get(activity,"store"),"newId");call(activity,"switchDocument",d,id);require((Boolean)call(activity,"saveNow"),"1.4.0 saves a real project");
            String encoded=encode(d);require(getTargetContext().getSharedPreferences("frociart_upgrade_qa",0).edit().putString("project",id).putString("document",encoded).commit(),"Upgrade reference written to real SharedPreferences");
            require(getTargetContext().getSharedPreferences("frociart_learning",0).edit().putStringSet("completed",new HashSet<>(Arrays.asList("l01"))).commit(),"Legacy learning progress seeded");
        });snap("01-before-upgrade");
    }
    void upgrade()throws Exception{
        ui(()->{SharedPreferences qa=getTargetContext().getSharedPreferences("frociart_upgrade_qa",0);String id=qa.getString("project","");
            require(!id.isEmpty(),"Original project ID survives update");
            Object actual=call(get(activity,"store"),"load",id);require(encode(actual).equals(qa.getString("document","")),"Project bytes and coordinates survive install -r");
            require(getTargetContext().getSharedPreferences("frociart_learning",0).getStringSet("completed",Collections.<String>emptySet()).contains("l01"),"Legacy lesson progress survives update");
            call(activity,"showHome");
        });require(findNow("Impara passo passo")!=null,"New beginner entry is rendered");snap("02-after-upgrade");
    }
    void finishDemonstration(Object lesson)throws Exception{
        Object[] trials=(Object[])get(lesson,"trials");String[] inputs=trials.length==0?new String[0]:(String[])get(trials[0],"inputs");int input=0;
        for(int tick=0;tick<2100;tick++){
            Object runner=get(panel(),"activeRunner");String state=String.valueOf(get(runner,"state"));
            if(state.equals("COMPLETED")){require(true,"Student follows the actual flowchart to completion: "+get(lesson,"id"));return;}
            if(state.equals("ERROR"))throw new AssertionError("Demonstration error: "+get(runner,"errorMessage"));
            if(state.equals("WAITING")){final String raw=inputs[Math.min(input++,inputs.length-1)];ui(()->{((EditText)get(panel(),"inlineInput")).setText(raw);UiButtonClick((View)get(panel(),"runSend"));});}
            else{await(()->((View)get(panel(),"runAdvance")).isShown()&&((View)get(panel(),"runAdvance")).isEnabled(),5000,"Avanti is ready after its animation");ui(()->UiButtonClick((View)get(panel(),"runAdvance")));pause(385);}
        }throw new AssertionError("Demonstration did not finish");
    }

    float dimension(Object n,String name)throws Exception{return(float)type("FlowModel").getMethod(name,n.getClass()).invoke(null,n);}
    Object cachedNode(Object canvas,String id)throws Exception{call(canvas,"ensureCache");return((Map<?,?>)get(canvas,"nodeIndex")).get(id);}
    void sizingReview()throws Exception{
        String longText="totale_complessivo_delle_operazioni = quantita_disponibile * prezzo_unitario";
        StringBuilder multi=new StringBuilder();for(int j=0;j<12;j++){if(j>0)multi.append('\n');multi.append("valore_").append(j).append(" = ").append(j).append(" + 25");}multi.append("\nULTIMA_RIGA_VISIBILE");String lines=multi.toString();
        ui(()->{Class<?> model=type("FlowModel"),nt=type("FlowModel$Node");for(int kind=0;kind<6;kind++){
            Object n=nt.getConstructor(int.class,String.class,float.class,float.class).newInstance(kind,"x",0f,0f);float dw=(float)model.getMethod("width",int.class).invoke(null,kind),dh=(float)model.getMethod("height",int.class).invoke(null,kind);
            require(dimension(n,"width")==dw&&dimension(n,"height")==dh,"Kind "+kind+" starts at default dimensions");set(n,"text",longText);require(dimension(n,"width")>dw&&dimension(n,"width")<=620,"Kind "+kind+" expands for longer text");set(n,"text",lines);require(dimension(n,"height")>dh,"Kind "+kind+" expands vertically for every line");
            Object size=model.getMethod("size",nt).invoke(null,n);String[] wrapped=(String[])get(size,"lines");require(Arrays.asList(wrapped).contains("ULTIMA_RIGA_VISIBILE"),"Kind "+kind+" retains its final visible line");set(n,"text","x");require(dimension(n,"width")==dw&&dimension(n,"height")==dh,"Kind "+kind+" returns to default after deletion");
        }});
        resetEdgeFixture();final Object[] changing={null};final RectF[] old={null};final String[] previous={null};final int[] history={0};
        ui(()->{Object n=call(freeDocument(),"find","check");changing[0]=n;previous[0]=(String)get(n,"text");old[0]=new RectF((RectF)get(cachedNode(edgeCanvas,"check"),"bounds"));history[0]=undoCount();type("Ui").getField("motion").setBoolean(null,true);call(edgeCanvas,"setMotionEnabled",true);call(activity,"editNode","check");});settle();snap("01-decision-default");
        ui(()->{firstEdit().setText(longText);Object animator=get(edgeCanvas,"sizingAnimator");require(animator instanceof android.animation.ValueAnimator,"Native size animator starts on typing");((android.animation.ValueAnimator)animator).setCurrentPlayTime(45);RectF during=(RectF)get(cachedNode(edgeCanvas,"check"),"bounds");float target=dimension(changing[0],"width");report.append("SIZING_FRAME old="+old[0]+" during="+during+" target="+target+" fraction="+get(edgeCanvas,"sizingFraction")+" canAnimate="+call(edgeCanvas,"canAnimate")+"\n");require(during.width()>old[0].width()&&during.width()<target,"Native outline expands through intermediate sizes");require(((String)get(changing[0],"text")).equals(previous[0]),"Live preview leaves committed text unchanged");require(undoCount()==history[0],"Typing creates no extra history entries");});settle();snap("02-decision-live-wide");
        edit(lines);snap("03-decision-live-tall");tapText("Applica modifiche");pause(500);
        ui(()->{Object n=changing[0];require(((String)get(n,"text")).equals(lines),"Real Apply stores the complete multiline text");require(get(n,"previewText")==null,"Apply clears transient preview");require(undoCount()==history[0]+1,"Apply and local reflow are one Undo entry");RectF b=(RectF)get(cachedNode(edgeCanvas,"check"),"bounds");require(Math.abs(b.width()-dimension(n,"width"))<.1f&&Math.abs(b.height()-dimension(n,"height"))<.1f,"Native outline settles to final derived dimensions");String svg=(String)call(edgeCanvas,"createSvg");require(svg.contains("ULTIMA_RIGA_VISIBILE")&&!svg.contains("ULTIMA_RIGA_VISIB…"),"SVG exports the last visible line");
            for(Object e:(List<?>)get(edgeCanvas,"edges")){Object edge=get(e,"edge");if("check".equals(get(edge,"from"))){float[] pts=(float[])get(e,"points");String branch=(String)get(edge,"branch");require(Math.abs(pts[0]-(branch.equals("true")?b.left:b.right))<1&&Math.abs(pts[1]-b.centerY())<1,"Branch outlet follows the resized diamond");}}
        });snap("04-decision-applied");ui(()->call(activity,"undo"));settle();ui(()->require(((String)get(call(freeDocument(),"find","check"),"text")).equals(previous[0]),"One native Undo restores the previous text"));
        ui(()->call(activity,"editNode","check"));edit(longText);ui(()->closeSheet());settle();ui(()->{Object n=call(freeDocument(),"find","check");require(((String)get(n,"text")).equals(previous[0])&&get(n,"previewText")==null,"Closing the sheet keeps only the recoverable draft");});
        ui(()->{call(panel(),"openLesson","b07");Object l=get(panel(),"lesson");require((Boolean)call(panel(),"loadWorkspace","b07",call(l,"solution")),"Open native lesson for live sizing");call(panel(),"showWorkspace");call(panel(),"editNode","saluto");});edit(lines);snap("05-learning-live-preview");ui(()->require(!((String)get(node("saluto"),"text")).equals(lines),"Lesson live preview preserves unapplied draft semantics"));tapText("Applica al blocco");settle();ui(()->require(((String)get(node("saluto"),"text")).equals(lines),"Lesson Apply commits the full text"));snap("06-learning-applied");
    }
    void reconnectReview()throws Exception{
        ui(()->{closeSheet();call(activity,"showHome");});Object p=panel();File old=(File)get(p,"directory"),own=new File(getTargetContext().getFilesDir(),"reconnect-qa-"+System.nanoTime());require(own.mkdirs(),"Isolated reconnect fixture");
        try{for(int mode=0;mode<3;mode++){final int branches=mode;ui(()->{set(p,"directory",own);call(p,"openLesson","b14");Object l=get(p,"lesson"),d=call(l,"solution");call(p,"loadWorkspace","b14",d);set(get(p,"work"),"document",d);call(p,"showWorkspace");for(Object n:(List<?>)get(d,"nodes")){set(n,"x",(float)get(n,"x")+137);set(n,"y",(float)get(n,"y")+49);}((List<?>)get(d,"edges")).removeIf(e->{try{return get(e,"to").equals("domanda")||branches<2&&get(e,"from").equals("domanda")&&(branches==0||get(e,"branch").equals("false"));}catch(Exception error){throw new RuntimeException(error);}});call(get(p,"canvas"),"notifyModelChanged");});String before=positionState(document());ui(()->call(p,"connect","dato","next","domanda"));require(positionState(document()).equals(before),"Reconnecting the decision preserves all positions, branch count "+mode);}
        resetEdgeFixture();ui(()->{call(activity,"setAutoArrange",true);Object d=freeDocument();for(Object n:(List<?>)get(d,"nodes"))set(n,"x",(float)get(n,"x")+173);((List<?>)get(d,"edges")).removeIf(e->{try{return get(e,"from").equals("input");}catch(Exception error){throw new RuntimeException(error);}});});String before=positionState(freeDocument());ui(()->call(activity,"requestConnection","input","next","check",0f,0f));settle();require(positionState(freeDocument()).equals(before),"Direct connection does not run global auto-arrange");require(!(boolean)get(activity,"autoArrange"),"Manual connection opts out of global auto-arrange");
        }finally{ui(()->{closeSheet();call(activity,"showHome");set(p,"directory",old);});}
    }
    void learningFlowReview()throws Exception{
        SharedPreferences prefs=getTargetContext().getSharedPreferences("frociart_tutor_v1",0);Map<String,?> backup=new HashMap<>(prefs.getAll());
        ui(()->{closeSheet();call(activity,"showHome");});Object p=panel();File previous=(File)get(p,"directory"),own=new File(getTargetContext().getFilesDir(),"learning-flow-qa-"+System.nanoTime());require(own.mkdirs(),"New isolated learning fixture");
        try{
            ui(()->{set(p,"directory",own);prefs.edit().remove("demonstrated").remove("completed").commit();call(p,"openLesson","b13");Object l=get(p,"lesson");call(p,"loadWorkspace","b13",call(l,"solution"));set(p,"step",((Object[])get(l,"steps")).length);call(p,"showWorkspace");});settle();
            View tools=(View)get(p,"workspaceTools"),canvas=(View)get(p,"canvas");require(bounds(tools).bottom<=bounds(canvas).top,"Undo, Redo, Riordina and Tutto are above the diagram");
            require(full(findNow("Riordina"))&&full(findNow("Tutto"))&&textFits((TextView)findNow("Riordina"))&&textFits((TextView)findNow("Tutto")),"Top text controls are fully visible without clipping");require(findNow("Strumenti")==null&&find(root(),"Strumenti del foglio",true)!=null,"Tools are in the upper settings icon");
            TextView expand=(TextView)get(p,"expandCanvas");require(expand.getText().length()==0&&expand.getCompoundDrawablesRelative()[0] instanceof android.graphics.drawable.BitmapDrawable,"Expand uses the actual transparent PNG, with no text label");int normalHeight=canvas.getHeight();ui(()->UiButtonClick(expand));pause(450);require(canvas.getHeight()>normalHeight,"PNG expansion gives the diagram more room");ui(()->UiButtonClick(expand));pause(450);require(full(findNow("Riordina"))&&full(findNow("Tutto")),"Collapsing restores the top commands");snap("01-top-toolbar");
            ui(()->call(p,"checkWithValue","",true));await(()->(boolean)call(p,"trialVisible"),20000,"Successful check opens execution automatically");
            require(!prefs.getStringSet("completed",Collections.<String>emptySet()).contains("b13"),"Validation alone cannot complete the lesson");require(!((View)get(p,"runSkip")).isShown(),"First demonstration cannot be skipped");require(((TextView)get(p,"runAdvance")).getText().toString().equals("Avanti"),"Step control is named Avanti");
            ui(()->call(p,"finishVerifiedRun"));require(!prefs.getStringSet("completed",Collections.<String>emptySet()).contains("b13"),"Calling completion before executing cannot bypass the demonstration");
            ui(()->call(p,"closeInlineRun"));require(((TextView)get(p,"check")).getText().toString().equals("Continua l’esecuzione"),"Leaving a demonstration offers resuming it, not the next lesson");ui(()->call(p,"primaryTask"));settle();
            finishDemonstration(get(p,"lesson"));require(prefs.getStringSet("demonstrated",Collections.<String>emptySet()).contains("b13"),"First complete demonstration is remembered per lesson");snap("02-first-demonstration-complete");tapText("Continua il percorso");require(prefs.getStringSet("completed",Collections.<String>emptySet()).contains("b13"),"Lesson completion is saved after real execution");
            ui(()->{call(p,"openLesson","b13");call(p,"loadWorkspace","b13",call(get(p,"lesson"),"solution"));call(p,"showWorkspace");call(p,"checkWithValue","",true);});await(()->(boolean)call(p,"trialVisible"),20000,"Repeat opens a new verified execution");require(((View)get(p,"runSkip")).isShown(),"A later review offers skipping the animation");tapText("Salta animazione");await(()->String.valueOf(get(get(p,"activeRunner"),"state")).equals("WAITING"),10000,"Skipping keeps the actual Input request");snap("03-repeat-input");finishFastDemonstration(get(p,"lesson"));require(((View)get(p,"runFinish")).isShown(),"Skipping finishes the interpreter before enabling continuation");snap("04-repeat-result");
            ui(()->{call(p,"closeInlineRun");Object d=document();List<?> edges=(List<?>)get(d,"edges");edges.removeIf(e->{try{return get(e,"to").equals("domanda");}catch(Exception error){throw new RuntimeException(error);}});});
            // b13 may use a different part ID: exercise every decision in b14 explicitly.
            ui(()->{call(p,"openLesson","b14");call(p,"loadWorkspace","b14",call(get(p,"lesson"),"solution"));call(p,"showWorkspace");});settle();
            for(int mode=0;mode<3;mode++){
                final int branches=mode;ui(()->{Object d=call(get(p,"lesson"),"solution");set(get(p,"work"),"document",d);call(get(p,"canvas"),"setDocument",d,false);Object q=call(d,"find","domanda");List<?> edges=(List<?>)get(d,"edges");edges.removeIf(e->{try{return get(e,"to").equals("domanda")||branches<2&&get(e,"from").equals("domanda")&&(branches==0||get(e,"branch").equals("false"));}catch(Exception error){throw new RuntimeException(error);}});set(q,"x",540f);set(q,"y",410f);call(get(p,"canvas"),"notifyModelChanged");});String positions=positionState(document());ui(()->call(p,"connect","dato","next","domanda"));settle();require(positionState(document()).equals(positions),"Reconnect preserves every node position with "+mode+" decision branches");ui(()->{Object d=document();((List<?>)get(d,"edges")).removeIf(e->{try{return get(e,"from").equals("dato");}catch(Exception error){throw new RuntimeException(error);}});});
            }
            resetInsertionFixture(2);float[] at=nodePoint("dragged");String before=encode(freeDocument());pointer(MotionEvent.ACTION_DOWN,at);pause(200);require(!(boolean)get(edgeCanvas,"holdHintVisible"),"Hold message stays hidden during a normal tap");pause(340);require((boolean)get(edgeCanvas,"holdHintVisible"),"Hold message appears after 400 milliseconds");require((float)get(edgeCanvas,"holdFill")==0,"Hint delay does not shorten the 800 millisecond hold grace");pointer(MotionEvent.ACTION_UP,at);require(before.equals(encode(freeDocument())),"Delayed hint does not change graph or position");
        }finally{ui(()->{closeSheet();call(activity,"showHome");set(p,"directory",previous);restorePreferences(prefs,backup);});}
    }
    void finishFastDemonstration(Object lesson)throws Exception{
        Object[] trials=(Object[])get(lesson,"trials");String[] inputs=(String[])get(trials[0],"inputs");int input=0;
        for(int tick=0;tick<50;tick++){Object r=get(panel(),"activeRunner");String state=String.valueOf(get(r,"state"));if(state.equals("COMPLETED"))return;if(state.equals("ERROR"))throw new AssertionError("Fast execution failed");if(state.equals("WAITING")){final String raw=inputs[Math.min(input++,inputs.length-1)];ui(()->{((EditText)get(panel(),"inlineInput")).setText(raw);UiButtonClick((View)get(panel(),"runSend"));});}pause(150);}throw new AssertionError("Fast execution unfinished");
    }
    String positionState(Object d)throws Exception{StringBuilder text=new StringBuilder();for(Object n:(List<?>)get(d,"nodes"))text.append(get(n,"id")).append('/').append(get(n,"x")).append('/').append(get(n,"y")).append(';');return text.toString();}
    void restorePreferences(SharedPreferences prefs,Map<String,?> values){SharedPreferences.Editor e=prefs.edit().clear();for(Map.Entry<String,?> item:values.entrySet()){Object v=item.getValue();String k=item.getKey();if(v instanceof String)e.putString(k,(String)v);else if(v instanceof Boolean)e.putBoolean(k,(Boolean)v);else if(v instanceof Integer)e.putInt(k,(Integer)v);else if(v instanceof Long)e.putLong(k,(Long)v);else if(v instanceof Float)e.putFloat(k,(Float)v);else if(v instanceof Set)e.putStringSet(k,new HashSet<>((Set<String>)v));}if(!e.commit())throw new AssertionError("Preference restore");}
    void course()throws Exception{
        ui(()->{
            require(getTargetContext().getSharedPreferences("frociart_tutor_v1",0).edit().clear().commit(),"Isolated tutor fixture starts empty");
            File folder=new File(getTargetContext().getFilesDir(),"tutor-documents");File[] old=folder.listFiles();if(old!=null)for(File f:old)if(f.isFile()&&!f.delete())throw new IOException("Cannot reset isolated tutor fixture");
            call(activity,"showHome");
        });tapText("Apri le lezioni");
        Object[] all=(Object[])type("FlowTutorContent").getMethod("all").invoke(null);
        require(all.length==24,"24 native lesson entries");
        for(Object l:all){
            String id=(String)get(l,"id"),title=(String)get(l,"title");tapText(title);
            require(texts(root()).contains((String)get(l,"explanation")),"Explanation appears before task "+id);
            tapText("Prova sul foglio");Object[] tasks=(Object[])get(l,"steps");
            for(int index=0;index<tasks.length;index++){
                Object task=tasks[index];int action=(Integer)get(task,"action");String node=(String)get(task,"node");Object part=call(l,"part",node);
                require(((Integer)get(panel(),"step"))==index,"Native teaching phase "+id+"/"+index);
                if(action==0){
                    int before=nodes().size();tapText("Aggiungi una casella");
                    if(id.equals("b01")){
                        tapText(kind(5));tapText("Controlla questo passo");expectError("altro compito",id,index);
                        tapText("Correggi questo passo");tapText(kind((Integer)get(part,"kind")));
                    }else tapText(kind((Integer)get(part,"kind")));
                    require(nodes().size()==before+1,"Native picker adds one real node "+id);
                }else if(action==1){
                    tapText("Scrivi nella casella");
                    String answer=(String)get(part,"answer"),bad=wrong(id);if(!bad.isEmpty()){
                        edit(bad);tapText("Applica al blocco");tapText("Controlla questo passo");expectError(reason(id),id,index);
                        require(((String)get(node(node),"text")).equals(bad),"Wrong input remains intact "+id);
                        tapText("Correggi questo passo");require(firstEdit().getText().toString().equals(bad),"Correction reopens the student's text "+id);
                    }
                    edit(answer);
                    if(id.equals("b06"))ui(()->{List<EditText> fields=edits(sheetRoot());require(fields.size()==2,"Input has separate name and human question");fields.get(1).setText("Quanti anni hai?");});
                    if(id.equals("b02")){keyboard();snap("03-first-output-keyboard");}
                    tapText("Applica al blocco");
                    if(id.equals("b06"))require(((String)get(node(node),"prompt")).equals("Quanti anni hai?"),"Input question is stored separately");
                }else if(action==2){
                    tapText("Collega le caselle");Object target=node((String)get(task,"to"));tapText((String)get(target,"text"));
                    if(findNow("Cambia collegamento")!=null)tapText("Cambia collegamento");
                }else{
                    tapText("Inserisci il valore");edit("eta");tapText("Invia il valore");expectError("non come numero",id,index);
                    tapText("Correggi questo passo");require(firstEdit().getText().toString().equals("eta"),"Runtime input draft survives error");
                    edit((String)get(task,"hint"));tapText("Invia il valore");
                }
                if(action!=3)tapText("Controlla questo passo");
                await(()->findNow("Prossimo passo")!=null,20000,"Real engine accepts constructed step "+id+"/"+index);
                tapText("Prossimo passo");steps++;
            }
            tapText("Controlla il flowchart");await(()->(boolean)call(panel(),"trialVisible"),30000,"Final check opens mandatory execution "+id);finishDemonstration(l);await(()->findNow("Continua il percorso")!=null,30000,"Final native check runs the student's graph "+id);
            if(id.equals("b13")||id.equals("b15")||id.equals("b24"))snap(id+"-completed");
            tapText("Continua il percorso");
            require(getTargetContext().getSharedPreferences("frociart_tutor_v1",0).getStringSet("completed",Collections.<String>emptySet()).contains(id),"Completion persisted only after diagram check "+id);
            lessons++;
        }
        require(lessons==24&&steps==45,"All 24 lessons and 45 actual UI construction steps completed");
        snap("04-all-lessons");prepareDraft();
    }
    String wrong(String id){
        switch(id){case"b02":return "Ciao";case"b05":return "punti == 5";case"b06":return "eta = 21";case"b11":return "meta = numero / 0";case"b13":return "eta > 18";case"b15":return "!0";case"b16":return "eta >= 18 or eta <= 65";case"b17":return "giorno == 6 or 7";case"b20":return "i <= n";case"b21":return "somma = i";default:return "";}
    }
    String reason(String id){switch(id){case"b02":return "virgolette";case"b05":return "non assegna";case"b06":return "solo il nome";case"b11":return "dividere per zero";case"b13":return "18 è escluso";case"b15":return "produce sempre vero";case"b16":return "entrambi i limiti";case"b17":return "seconda domanda";case"b20":return "giro in più";case"b21":return "giri precedenti";default:return "";}}
    void expectError(String expected,String id,int index)throws Exception{
        await(()->sheet()!=null&&texts(sheetRoot()).contains("DA CORREGGERE"),15000,"Wrong answer is diagnosed "+id);
        if(!texts(sheetRoot()).toLowerCase(Locale.ROOT).contains(expected.toLowerCase(Locale.ROOT))&&findNow("Capisci questa prova")!=null){tapText("Capisci questa prova");await(()->texts(sheetRoot()).toLowerCase(Locale.ROOT).contains(expected.toLowerCase(Locale.ROOT)),15000,"Specific cause is explained "+id+" "+expected);tapText("Torna alla correzione");}
        else require(texts(sheetRoot()).toLowerCase(Locale.ROOT).contains(expected.toLowerCase(Locale.ROOT)),"Specific cause is explained "+id+" "+expected);
        require(((Integer)get(panel(),"step"))==index,"Wrong answer cannot advance "+id);wrongCases++;
    }
    void openCompleted(String id)throws Exception{
        ui(()->{closeSheet();call(activity,"showHome");});tapText("Apri le lezioni");
        Object l=type("FlowTutorContent").getMethod("lesson",String.class).invoke(null,id);tapText((String)get(l,"title"));tapText("Riprendi il flowchart");
    }
    void prepareDraft()throws Exception{
        openCompleted("b02");tapText("Modifica il blocco");tapText("\"Ciao\"");
        edit("\"Bozza non ancora chiusa");tapDescription("Chiudi pannello");pause(1200);
        require(((String)get(node("messaggio"),"text")).equals("\"Ciao\""),"Closing the editor preserves the committed graph");
        snap("05-draft-ready-for-process-restart");
    }
    void persist()throws Exception{
        require(getTargetContext().getSharedPreferences("frociart_tutor_v1",0).getStringSet("completed",Collections.<String>emptySet()).size()==24,"All completed lessons survive process death");
        openCompleted("b02");require(((String)get(node("messaggio"),"text")).equals("\"Ciao\""),"Saved flowchart survives process death");
        tapText("Modifica il blocco");tapText("\"Ciao\"");require(firstEdit().getText().toString().equals("\"Bozza non ancora chiusa"),"Unapplied draft survives process death");
        edit("\"Ciao\"");tapText("Applica al blocco");snap("06-restored-after-process-restart");
    }
    void layout()throws Exception{
        openCompleted("b02");tapText("Modifica il blocco");tapText("\"Ciao\"");edit("\"Bozza con tastiera aperta\"");
        keyboard();snap("07-keyboard-portrait");
        rotate(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE);
        if(sheet()!=null)closeSheet();
        tapText("Modifica il blocco");if(edits(sheetRoot()).isEmpty())tapText("\"Ciao\"");require(firstEdit().getText().toString().equals("\"Bozza con tastiera aperta\""),"Rotation preserves editor draft");
        keyboard();snap("08-keyboard-landscape");edit("\"Ciao\"");tapText("Applica al blocco");rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        tapText("Controlla il flowchart");await(()->(boolean)call(panel(),"trialVisible"),15000,"Final check opens execution after rotation");finishDemonstration(get(panel(),"lesson"));await(()->findNow("Continua il percorso")!=null,15000,"Flowchart remains valid after keyboard and rotation");tapText("Continua il percorso");
    }
    /** Regression against the installed APK: real rotation with a waiting Input and open editor. */
    void rotationReview()throws Exception{
        rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        ui(()->{closeSheet();call(activity,"showHome");});tapText("Apri le lezioni");
        Object lesson=type("FlowTutorContent").getMethod("lesson",String.class).invoke(null,"b07");
        tapText((String)get(lesson,"title"));
        if(findNow("Prova sul foglio")!=null)tapText("Prova sul foglio");else tapText("Riprendi il flowchart");
        ui(()->call(panel(),"editNode","saluto"));edit("nome");tapText("Applica al blocco");
        tapText("Prova");tapText("Avanti");tapText("Avanti");
        final int[] previousSteps={0};ui(()->{
            Object runner=get(panel(),"activeRunner");require(get(runner,"state").toString().equals("WAITING"),"Tutor reaches a real Input");previousSteps[0]=(Integer)get(runner,"steps");
            EditText input=(EditText)get(panel(),"inlineInput");input.setText("Ada Lovelace");input.requestFocus();((InputMethodManager)activity.getSystemService(Context.INPUT_METHOD_SERVICE)).showSoftInput(input,InputMethodManager.SHOW_IMPLICIT);
        });pause(700);snap("11-input-before-rotation");
        rotate(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE);snap("12-input-after-rotation");
        ui(()->{
            Object runner=get(panel(),"activeRunner");require(runner!=null,"Rotation keeps the current tutor execution");
            require(get(runner,"state").toString().equals("WAITING")&&(Integer)get(runner,"steps")==previousSteps[0],"Rotation preserves waiting state and exact step count");
            require(((EditText)get(panel(),"inlineInput")).getText().toString().equals("Ada Lovelace"),"Rotation preserves the typed Input value");
            require(((View)get(panel(),"runPanel")).isShown(),"Rotation leaves the execution controls open");
        });tapText("Invia");tapText("Avanti");tapText("Avanti");
        ui(()->require(((TextView)get(panel(),"runOutput")).getText().toString().contains("Ada Lovelace"),"The preserved Input produces the actual output"));
        tapText("Fine prova");ui(()->call(panel(),"editNode","saluto"));edit("nome + \"!\"");keyboard();
        rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        require(sheet()!=null,"Rotation keeps the tutor editor open");require(firstEdit().getText().toString().equals("nome + \"!\""),"Rotation preserves the editor draft in place");snap("13-editor-restored");
        closeSheet();ActivityMonitor monitor=addMonitor("it.frociart.app.MainActivity",null,false);ui(()->activity.recreate());Activity restored=waitForMonitorWithTimeout(monitor,12000);removeMonitor(monitor);require(restored!=null,"Actual Activity recreation completes");activity=restored;settle();
        pause(500);ui(()->{require(get(panel(),"route").equals("workspace"),"Activity recreation restores the tutor workspace");require(get(panel(),"activeId").equals("b07"),"Activity recreation restores the same lesson");require((Float)get(get(panel(),"canvas"),"zoom")>=.78f,"Restored teaching canvas keeps a readable zoom");});snap("14-workspace-after-recreation");
    }
    void backupRecovery()throws Exception{
        ui(()->{
            closeSheet();call(activity,"showHome");call(panel(),"openLesson","b03");Object lesson=get(panel(),"lesson"),solution=call(lesson,"solution");
            require((Boolean)call(panel(),"loadWorkspace","b03",solution),"Backup fixture opens a real tutor workspace");
            Object original=call(solution,"copy");
            // Use the first Output ID from the lesson; fixture contains no user data.
            for(Object n:(List<?>)get(original,"nodes"))if((Integer)get(n,"kind")==5)set(n,"text","41 + 1");
            set(get(panel(),"work"),"document",original);require((Boolean)call(panel(),"save"),"Fixture is saved by the production AtomicFile path");
            File base=(File)call(panel(),"file","b03"),backup=new File(base.getPath()+".bak");
            require(!backup.exists()&&base.renameTo(backup),"Interrupted-write fixture contains the valid backup without the base file");
            require((Boolean)call(panel(),"loadWorkspace","b03",call(lesson,"seed")),"Backup-only workspace can be reopened");
            require(encode(document()).equals(encode(original)),"Backup-only recovery restores the complete saved diagram instead of a fresh seed");
            require(base.exists()&&!backup.exists(),"AtomicFile promotes the recovered backup");
        });snap("15-backup-recovered");
    }
    void surfaceReview()throws Exception{
        ui(()->{closeSheet();call(activity,"showHome");call(panel(),"openLesson","b02");Object lesson=get(panel(),"lesson");require((Boolean)call(panel(),"loadWorkspace","b02",call(lesson,"solution")),"Surface fixture loads production workspace");set(panel(),"step",((Object[])get(lesson,"steps")).length);call(panel(),"showWorkspace");});settle();
        for(int orientation:new int[]{ActivityInfo.SCREEN_ORIENTATION_PORTRAIT,ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE}){
            rotate(orientation);final List<TextView> controls=new ArrayList<>();ui(()->{
                for(String field:new String[]{"workspaceActions","workspaceTools","workspaceFooter"}){
                    ViewGroup group=(ViewGroup)get(panel(),field);for(int i=0;i<group.getChildCount();i++){View view=group.getChildAt(i);if(view instanceof TextView&&view.isShown())controls.add((TextView)view);}
                }
                require(((View)get(panel(),"canvas")).getHeight()>=UiDp(72),"Tutor canvas has usable layout space");
            });for(TextView text:controls){ui(()->text.requestRectangleOnScreen(new Rect(0,0,text.getWidth(),text.getHeight()),true));settle();ui(()->{require(full(text),"Tutor control is fully reachable: "+text.getText());require(textFits(text),"Tutor label fits at system font scale: "+text.getText());});}snap(orientation==ActivityInfo.SCREEN_ORIENTATION_PORTRAIT?"16-surface-portrait":"17-surface-landscape");
        }
    }
    int UiDp(int value){return(int)(value*activity.getResources().getDisplayMetrics().density+.5f);}
    void recreate()throws Exception{ActivityMonitor monitor=addMonitor("it.frociart.app.MainActivity",null,false);ui(()->activity.recreate());Activity restored=waitForMonitorWithTimeout(monitor,12000);removeMonitor(monitor);require(restored!=null,"Actual Activity recreation completes");activity=restored;settle();}
    void legacyRestore()throws Exception{
        ui(()->{closeSheet();call(activity,"showHome");});tapText("Approfondimenti");
        ui(()->{require(!(Boolean)get(get(activity,"study"),"tutorVisible"),"Legacy learning path is open");require(get(get(activity,"study"),"page").equals("path"),"Legacy path is the active destination");});snap("18-legacy-before-recreation");recreate();
        ui(()->{require(!(Boolean)get(get(activity,"study"),"tutorVisible"),"Recreation preserves the selected legacy learning path");require(get(get(activity,"study"),"page").equals("path"),"Recreation keeps the previous learning destination");});snap("19-legacy-after-recreation");
    }
    void editorRestore()throws Exception{
        final String[] committedBefore={null};
        ui(()->{closeSheet();call(activity,"showHome");call(panel(),"openLesson","b07");Object l=get(panel(),"lesson");require((Boolean)call(panel(),"loadWorkspace","b07",call(l,"solution")),"Editor restoration opens the real stored lesson");call(panel(),"showWorkspace");committedBefore[0]=encode(document());call(panel(),"editNode","saluto");});edit("nome + \"!\"");snap("20-editor-before-recreation");recreate();
        require(sheet()!=null,"Recreation restores the open tutor editor");require(firstEdit().getText().toString().equals("nome + \"!\""),"Recreation restores its unapplied draft");require(encode(document()).equals(committedBefore[0]),"Recreation preserves the entire committed diagram exactly");snap("21-editor-after-recreation");
        try(android.os.ParcelFileDescriptor descriptor=getUiAutomation().executeShellCommand("logcat -d --pid="+android.os.Process.myPid()+" -s WindowManager:E");InputStream in=new android.os.ParcelFileDescriptor.AutoCloseInputStream(descriptor);ByteArrayOutputStream bytes=new ByteArrayOutputStream()){byte[] buffer=new byte[4096];int count;while((count=in.read(buffer))!=-1)bytes.write(buffer,0,count);String log=bytes.toString("UTF-8");write(new File(directory,"window-lifecycle.log"),log);require(!log.contains("WindowLeaked"),"Recreation releases the old editor window without WindowLeaked");}
    }
    boolean textFits(TextView view){android.text.Layout layout=view.getLayout();if(layout==null)return false;for(int i=0;i<layout.getLineCount();i++)if(layout.getEllipsisCount(i)>0)return false;return layout.getHeight()<=view.getHeight()-view.getCompoundPaddingTop()-view.getCompoundPaddingBottom()+2;}
    void workshop()throws Exception{
        File fixtureDirectory=new File(getTargetContext().getFilesDir(),"tutor-documents").getCanonicalFile();
        for(String suffix:new String[]{"",".bak",".new"}){File fixture=new File(fixtureDirectory,"workshop.frociart"+suffix).getCanonicalFile();require(fixture.getParentFile().equals(fixtureDirectory),"Workshop fixture stays in emulator-only tutor directory");if(fixture.exists()){try(InputStream input=new FileInputStream(fixture);OutputStream output=new FileOutputStream(new File(directory,"workshop-before-reset"+suffix+".frociart"))){byte[] bytes=new byte[4096];int n;while((n=input.read(bytes))!=-1)output.write(bytes,0,n);}if(!fixture.delete())throw new IOException("Cannot reset isolated workshop fixture");}}
        ui(()->{closeSheet();call(activity,"showHome");});tapText("Laboratorio guidato");tapText("Apri il laboratorio");
        insert("Somma due valori","Inizio");tapText("Controlla il percorso");await(()->sheet()!=null&&texts(sheetRoot()).contains("valore alla variabile"),15000,"Uninitialized preset dependencies are explained");tapText("Torna al foglio");
        insert("Chiedi un numero","Inizio");tapText("Modifica il blocco");edit("a");tapText("Applica al blocco");
        insert("Chiedi un numero","a");tapText("Modifica il blocco");edit("b");tapText("Applica al blocco");
        insert("Mostra il valore","totale = a + b");tapText("Modifica il blocco");edit("totale");tapText("Applica al blocco");
        tapText("Controlla il percorso");await(()->findNow("Percorso verificato")!=null,15000,"Guided presets form a genuinely executable graph");tapText("Torna al foglio");
        tapDescription("Strumenti del foglio");tapText("Prova un passo alla volta");int submitted=0;
        for(int ticks=0;ticks<20;ticks++){
            String state=String.valueOf(get(get(panel(),"activeRunner"),"state"));if(state.equals("COMPLETED"))break;if(state.equals("ERROR"))throw new AssertionError("Workshop interpreter failed");
            if(findNow("Invia")!=null){ui(()->((EditText)get(panel(),"inlineInput")).setText(submittedValue()));tapText("Invia");submitted++;}
            else if(findNow("Avanti")!=null){View button=findNow("Avanti");await(()->((View)get(panel(),"runAdvance")).isEnabled()||String.valueOf(get(get(panel(),"activeRunner"),"state")).equals("COMPLETED"),5000,"Workshop Avanti is ready after its animation");if(String.valueOf(get(get(panel(),"activeRunner"),"state")).equals("COMPLETED"))break;tapText("Avanti");}
            else throw new IllegalStateException("Runner controls missing");
        }
        require(submitted==2,"Runner accepted two actual Input values");
        require(((TextView)get(panel(),"runOutput")).getText().toString().equals("Risultato: 5"),"Actual workshop execution prints 5");
        snap("09-guided-sum-executed");tapText("Fine prova");
        tapDescription("Strumenti del foglio");tapText("Salva una copia nei miei progetti");
        await(()->findNow("Il mio foglio guidato")!=null,15000,"The saved copy appears in the real project archive");
        snap("10-guided-copy-in-projects");
    }
    void insert(String preset,String source)throws Exception{
        tapText("Aggiungi");tapText(preset);tapText("Inserisci nel foglio");
        require(findNow("Dopo quale blocco?")!=null,"Insertion point is chosen explicitly");tapText(source);
        await(()->findNow("Torna al foglio")!=null,10000,"Inserted block opens its guide");tapText("Torna al foglio");
    }
    int fixtureInput;String submittedValue(){return fixtureInput++==0?"2":"3";}
    void keyboard()throws Exception{
        EditText field=firstEdit();tap(field);ui(()->{field.requestFocus();((InputMethodManager)activity.getSystemService(Context.INPUT_METHOD_SERVICE)).showSoftInput(field,InputMethodManager.SHOW_IMPLICIT);});
        await(()->{WindowInsets i=sheetRoot().getRootWindowInsets();return i!=null&&i.isVisible(WindowInsets.Type.ime());},15000,"Real Android keyboard opens");
        pause(900);ui(()->{
            Rect ime=null;for(AccessibilityWindowInfo w:getUiAutomation().getWindows())if(w.getType()==AccessibilityWindowInfo.TYPE_INPUT_METHOD){ime=new Rect();w.getBoundsInScreen(ime);break;}
            View apply=find(sheetRoot(),"Applica al blocco",false);require(apply!=null&&full(apply),"Apply is fully visible with keyboard");
            if(ime!=null)require(bounds(apply).bottom<=ime.top,"Apply lies above the native IME window");
        });
    }
    void rotate(int orientation)throws Exception{ui(()->activity.setRequestedOrientation(orientation));boolean landscape=orientation==ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;await(()->(root().getWidth()>root().getHeight())==landscape,15000,"Real display rotation");pause(900);}
    Object panel()throws Exception{return get(get(activity,"study"),"tutorPanel");}
    Object document()throws Exception{return get(get(panel(),"work"),"document");}
    List<?> nodes()throws Exception{return(List<?>)get(document(),"nodes");}
    Object node(String id)throws Exception{return call(document(),"find",id);}
    String kind(int k)throws Exception{return(String)type("FlowModel").getMethod("kindName",int.class).invoke(null,k);}
    Class<?> type(String name)throws Exception{return Class.forName("it.frociart.app."+name,true,activity.getClassLoader());}
    String encode(Object d)throws Exception{return(String)type("FlowCodec").getMethod("encode",d.getClass()).invoke(null,d);}
    Object sheet()throws Exception{
        Object study=get(activity,"study");try{Object p=get(study,"tutorPanel");if(p!=null){Object s=get(p,"sheet");if(s!=null&&((Dialog)get(s,"dialog")).isShowing())return s;}}catch(NoSuchFieldException ignored){}
        Object s=get(activity,"activeSheet");return s!=null&&((Dialog)get(s,"dialog")).isShowing()?s:null;
    }
    View sheetRoot()throws Exception{Object s=sheet();if(s==null)throw new IllegalStateException("No visible dialog");return(View)get(s,"root");}
    View root(){return activity.getWindow().getDecorView();}
    void closeSheet()throws Exception{Object s=sheet();if(s!=null)((Dialog)get(s,"dialog")).dismiss();}
    EditText firstEdit()throws Exception{List<EditText> all=edits(sheetRoot());if(all.isEmpty())throw new IllegalStateException("No editor field");return all.get(0);}
    List<EditText> edits(View v){ArrayList<EditText> list=new ArrayList<>();if(v instanceof EditText)list.add((EditText)v);if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++)list.addAll(edits(((ViewGroup)v).getChildAt(i)));return list;}
    void edit(String text)throws Exception{ui(()->firstEdit().setText(text));settle();}
    View findNow(String text)throws Exception{final View[] found={null};ui(()->{Object s=sheet();found[0]=find(s==null?root():(View)get(s,"root"),text,false);});return found[0];}
    View find(View v,String text,boolean description){CharSequence candidate=description?v.getContentDescription():v instanceof TextView?((TextView)v).getText():null;if(candidate!=null&&text.contentEquals(candidate)&&v.isShown())return v;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++){View found=find(((ViewGroup)v).getChildAt(i),text,description);if(found!=null)return found;}return null;}
    void tapText(String text)throws Exception{await(()->findNow(text)!=null,15000,"Control rendered: "+text);tap(findNow(text));settle();}
    void tapDescription(String label)throws Exception{final View[] found={null};ui(()->{Object s=sheet();found[0]=find(s==null?root():(View)get(s,"root"),label,true);});require(found[0]!=null,"Accessible control "+label);tap(found[0]);settle();}
    void tap(View v)throws Exception{
        ui(()->v.requestRectangleOnScreen(new Rect(0,0,v.getWidth(),v.getHeight()),true));pause(250);final Rect[] r={null};ui(()->{require(full(v),"Touch target is visible");r[0]=bounds(v);});
        long now=SystemClock.uptimeMillis();for(int a:new int[]{MotionEvent.ACTION_DOWN,MotionEvent.ACTION_UP}){MotionEvent e=MotionEvent.obtain(now,SystemClock.uptimeMillis(),a,r[0].exactCenterX(),r[0].exactCenterY(),0);e.setSource(InputDevice.SOURCE_TOUCHSCREEN);boolean ok=getUiAutomation().injectInputEvent(e,true);e.recycle();require(ok,"Native touch injected");}
    }
    Rect bounds(View v){int[] xy=new int[2];v.getLocationOnScreen(xy);return new Rect(xy[0],xy[1],xy[0]+v.getWidth(),xy[1]+v.getHeight());}
    boolean full(View v){Rect r=new Rect();return v.isShown()&&v.getWidth()>0&&v.getHeight()>0&&v.getGlobalVisibleRect(r)&&r.width()>=v.getWidth()-2&&r.height()>=v.getHeight()-2;}
    String texts(View v){StringBuilder b=new StringBuilder();if(v instanceof TextView)b.append(((TextView)v).getText()).append('\n');if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++)b.append(texts(((ViewGroup)v).getChildAt(i)));return b.toString();}
    void snap(String name)throws Exception{settle();Bitmap bitmap=getUiAutomation().takeScreenshot();require(bitmap!=null,"Native screenshot available");try(FileOutputStream out=new FileOutputStream(new File(directory,name+".png"))){bitmap.compress(Bitmap.CompressFormat.PNG,100,out);}bitmap.recycle();final String[] tree={""};ui(()->{tree[0]=texts(root());if(sheet()!=null)tree[0]+="\nDIALOG:\n"+texts(sheetRoot());});write(new File(directory,name+".txt"),tree[0]);}
    void write(File f,String s)throws Exception{try(FileOutputStream out=new FileOutputStream(f)){out.write(s.getBytes("UTF-8"));}}
    void require(boolean value,String name){checks++;String line=(value?"PASS ":"FAIL ")+name;report.append(line).append('\n');Log.i("FROCIART_TUTOR_QA",line);if(!value)throw new AssertionError(name);}
    void await(Condition c,long timeout,String name)throws Exception{long until=SystemClock.uptimeMillis()+timeout;do{final boolean[] value={false};ui(()->value[0]=c.value());if(value[0]){require(true,name);return;}pause(180);}while(SystemClock.uptimeMillis()<until);throw new AssertionError(name);}
    void ui(Action action){final Throwable[] error={null};Runnable run=()->{try{action.run();}catch(Throwable t){error[0]=t;}};if(Looper.myLooper()==Looper.getMainLooper())run.run();else runOnMainSync(run);if(error[0]!=null)throw new RuntimeException(error[0]);}
    void pause(long ms){SystemClock.sleep(ms);}void settle(){ui(()->{});pause(300);}
    Object get(Object o,String name)throws Exception{Field f=o.getClass().getDeclaredField(name);f.setAccessible(true);return f.get(o);}
    void set(Object o,String name,Object value)throws Exception{Field f=o.getClass().getDeclaredField(name);f.setAccessible(true);f.set(o,value);}
    Object call(Object o,String name,Object...args)throws Exception{for(Method m:o.getClass().getDeclaredMethods())if(m.getName().equals(name)&&m.getParameterTypes().length==args.length){m.setAccessible(true);return m.invoke(o,args);}throw new NoSuchMethodException(name);}
}

