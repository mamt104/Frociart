package it.frociart.app;

import android.animation.ValueAnimator;
import android.app.Activity;
import android.app.Instrumentation;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.RectF;
import android.os.Bundle;
import android.os.SystemClock;
import android.util.Log;
import android.view.InputDevice;
import android.view.MotionEvent;
import android.view.View;
import android.view.accessibility.AccessibilityNodeInfo;
import java.io.File;
import java.io.FileOutputStream;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.Map;

/** Native Android regressions for the connection workflow; no mocked Canvas or FPS claims. */
public final class ConnectionProbe extends Instrumentation {
    private interface Action { void run() throws Exception; }
    private ConnectionChecksActivity activity;
    private File directory;
    private int checks;
    private boolean pulseOnly;
    private final StringBuilder report = new StringBuilder();
    private static long time = SystemClock.uptimeMillis() + 1000, downTime;

    private static final class Events implements FlowCanvasView.Listener {
        int before, changed, addAfter, added, connect, dropped, requested, editedEdge, manual;
        String source, target, branch, edgeId, hint;
        float x, y;
        boolean intercept;
        public void onBeforeChange() { before++; }
        public void onDiagramChanged(String label) { changed++; }
        public void onSelectionChanged() { }
        public void onEditRequested(String id) { }
        public void onAddRequested(float x, float y, String source, String branch) {
            added++; this.x=x; this.y=y; this.source=source; this.branch=branch;
        }
        public void onAddAfterRequested(String source) { addAfter++; this.source=source; }
        public void onConnectRequested(String id) { connect++; source=id; }
        public void onConnectionStateChanged(String source, String branch) { }
        public void onHint(String text) { hint=text; }
        public void onConnectionDropRequested(String source, String target, float x, float y) {
            dropped++; this.source=source; this.target=target; this.x=x; this.y=y;
        }
        public boolean onConnectionTargetRequested(String source, String branch, String target, float x, float y) {
            requested++; this.source=source; this.branch=branch; this.target=target; this.x=x; this.y=y; return intercept;
        }
        public void onEdgeEditRequested(String id) { editedEdge++; edgeId=id; }
        public void onManualNodeMove() { manual++; }
        void reset() { before=changed=addAfter=added=connect=dropped=requested=editedEdge=manual=0; source=target=branch=edgeId=hint=null; }
    }

    @Override public void onCreate(Bundle arguments) {
        super.onCreate(arguments); pulseOnly=arguments!=null && "pulse".equals(arguments.getString("phase")); start();
    }
    @Override public void onStart() {
        boolean success = false;
        directory = new File(getTargetContext().getFilesDir(), "connections-1.2.1"); directory.mkdirs();
        try {
            activity = (ConnectionChecksActivity)startActivitySync(new Intent().setClassName(getTargetContext().getPackageName(),
                    ConnectionChecksActivity.class.getName()).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            if (!pulseOnly) ui(() -> {
                String historical=FlowCanvasChecks.run(activity);
                check(historical.contains("CANVAS CHECKS PASSED: 136"), "All 136 current canvas regression checks pass");
                report.append(historical).append('\n');
                plusAndSelection(); dropContracts(); targetRequests(); snapGeometry(); edgeGestures(); accessibility();
            });
            else report.append("SCOPE: pulse/lifecycle only; the earlier functional and historical results remain separate.\n");
            ui(() -> {
                FlowCanvasView view=activity.canvas;
                view.setDocument(document(),true); view.setMobileViewport(true); view.focusNodeAnimated(view.getDocument().nodes.get(0).id);
                view.setSelectionMode(true); view.setMotionEnabled(true); draw(view);
            });
            long deadline=SystemClock.uptimeMillis()+30000; final boolean[] ready={false};
            while (SystemClock.uptimeMillis()<deadline) {
                ui(() -> { FlowCanvasView view=activity.canvas; ready[0]=view.isAttachedToWindow() && view.isShown() &&
                        view.getWindowVisibility()==View.VISIBLE && view.hasWindowFocus() && view.isEnabled() && ValueAnimator.areAnimatorsEnabled(); });
                if (ready[0]) break;
                SystemClock.sleep(100);
            }
            ui(() -> {
                FlowCanvasView view=activity.canvas; draw(view);
                report.append("PULSE DIAGNOSTICS: ").append(pulseState(view)).append('\n');
                check(view.isAttachedToWindow() && view.isShown() && view.getWindowVisibility()==View.VISIBLE &&
                        view.hasWindowFocus() && view.isEnabled() && ValueAnimator.areAnimatorsEnabled(),
                        "PRECONDITION: pulse surface is attached, shown, focused, enabled and system animations are on");
                check(!((RectF)field(view,"plusHit")).isEmpty(),"Attached Start keeps a visible + in Seleziona");
                check(field(view,"connectorAnimator")!=null,"Start + pulses in Seleziona on an attached, focused surface");
                view.setMotionEnabled(false); draw(view);
                check(field(view,"connectorAnimator")==null && !((RectF)field(view,"plusHit")).isEmpty(),"Reduced motion stops the pulse while keeping + available");
                view.setReadOnly(true); draw(view);
                check(((RectF)field(view,"plusHit")).isEmpty(),"Run preview hides editing affordances");
            });
            success=true;
        } catch (Throwable error) {
            report.append("CONNECTION CHECKS FAILED\n").append(Log.getStackTraceString(error));
            if (activity!=null) ui(() -> report.append("FAILURE SURFACE STATE: ").append(pulseState(activity.canvas)).append('\n'));
        }
        finally {
            if (activity!=null) ui(() -> { activity.canvas.setMotionEnabled(false); activity.finish(); });
            report.append(success?"CONNECTION CHECKS PASSED: ":"CONNECTION CHECKS FAILED AFTER: ").append(checks).append('\n');
            try (FileOutputStream out=new FileOutputStream(new File(directory,pulseOnly?"report-pulse.txt":"report.txt"))) { out.write(report.toString().getBytes("UTF-8")); }
            catch(Exception error) { Log.e("FROCIART_CONNECTIONS","Writing report",error); }
            Bundle result=new Bundle(); result.putString("stream",report.toString()); finish(success?Activity.RESULT_OK:Activity.RESULT_CANCELED,result);
        }
    }

    private static String pulseState(FlowCanvasView view) throws Exception {
        Method canAnimate=view.getClass().getDeclaredMethod("canAnimate"); canAnimate.setAccessible(true);
        return "attached="+view.isAttachedToWindow()+", shown="+view.isShown()+", windowVisibility="+view.getWindowVisibility()+
                ", viewVisibility="+view.getVisibility()+", windowFocus="+view.hasWindowFocus()+", enabled="+view.isEnabled()+
                ", motionReady="+field(view,"motionReady")+", motionEnabled="+field(view,"motionEnabled")+
                ", systemMotionEnabled="+field(view,"systemMotionEnabled")+", platformAnimators="+ValueAnimator.areAnimatorsEnabled()+
                ", canAnimate="+canAnimate.invoke(view)+", readOnly="+view.isReadOnly()+", selectionMode="+view.isSelectionMode()+
                ", selected="+view.getSelectedIds().size()+", plusNodeId="+field(view,"plusNodeId")+", plusHit="+field(view,"plusHit")+
                ", gesture="+field(view,"gesture")+", connecting="+view.isConnecting()+", pulseActive="+(field(view,"connectorAnimator")!=null);
    }

    private void plusAndSelection() throws Exception {
        FlowCanvasView view=sized(360,640); FlowModel.Document d=document(); Events e=new Events(); view.setListener(e); view.setDocument(d,true);
        FlowModel.Node start=d.nodes.get(0), process=d.nodes.get(1), end=d.nodes.get(2);
        view.select(start.id); draw(view); RectF plus=plus(view);
        check(!plus.isEmpty(),"Inizio has a visible + in Sposta");
        near(plus.width()/density(view),48,"+ keeps a physical 48 dp touch target");
        view.setSelectionMode(true); draw(view); plus=plus(view);
        check(!plus.isEmpty(),"Reported defect: Inizio still has + when Seleziona is active");
        e.reset(); tap(view,plus.centerX(),plus.centerY());
        check(e.addAfter==1 && e.connect==0 && e.added==0 && start.id.equals(e.source),"Tap Start + requests new-block types, separately from Collega");
        check(e.before==0 && e.changed==0 && e.manual==0,"Opening the new-block picker never mutates history or disables layout");
        check(view.getSelectedIds().size()==1 && view.getSelectedIds().contains(start.id),"Tap + keeps its Start source selected");
        view.clearSelection(); tapNode(view,start); draw(view);
        check(!plus(view).isEmpty(),"Selecting Inizio through Seleziona reveals + immediately");
        tapNode(view,process); draw(view);
        check(view.getSelectedIds().size()==2,"Seleziona still accumulates multiple blocks");
        check(plus(view).isEmpty(),"Group selection has no ambiguous single-source +");
        view.select(end.id); draw(view); check(plus(view).isEmpty(),"Fine has no outgoing +");
        view.select(process.id); draw(view); check(!plus(view).isEmpty(),"Operazione has + in selection mode too");
        e.reset(); plus=plus(view); tap(view,plus.centerX()+21*density(view),plus.centerY());
        check(e.addAfter==1,"+ responds at the visible button's enlarged touch margin");
        view.setSelectionMode(false); e.reset(); view.select(start.id); draw(view); plus=plus(view);
        single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_UP,plus.centerX()+90*density(view),plus.centerY());
        check(e.addAfter==0 && e.dropped==0,"A distant UP without MOVE never creates a false + tap or drop");
    }

    private void dropContracts() throws Exception {
        FlowCanvasView view=sized(420,720); FlowModel.Document d=document(); Events e=new Events(); view.setListener(e); view.setDocument(d,true);
        FlowModel.Node start=d.nodes.get(0), process=d.nodes.get(1), end=d.nodes.get(2);
        view.setSelectionMode(true); view.select(start.id); draw(view); RectF plus=plus(view); float[] target=center(view,process);
        String originalSvg=view.createSvg(); e.reset();
        single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_MOVE,target[0],target[1]); draw(view);
        check(integer(view,"gesture")==8,"Dragging + in Seleziona enters connector drag instead of drawing a selection box");
        check(process.id.equals(field(view,"dragConnectionTarget")),"Hover identifies the actual block destination");
        check(view.createSvg().equals(originalSvg),"Hover preview and highlights are excluded from SVG");
        save(view,"01-start-connection-hover.png");
        single(view,MotionEvent.ACTION_UP,target[0]+250,target[1]+250);
        check(e.dropped==1 && start.id.equals(e.source) && process.id.equals(e.target),"Start drop reaches the host with the last MOVE destination");
        check(d.edges.isEmpty() && e.before==0 && e.changed==0 && !view.isConnecting(),"Start drop has no hidden edge replacement or pending connect mode");
        check(e.manual==0,"Dragging a connector never counts as moving a block");
        view.completeConnectionDrop(e.source,"next",e.target,e.x,e.y);
        check(d.edges.size()==1 && d.edges.get(0).to.equals(process.id) && e.before==1 && e.changed==1,"Explicit host completion creates exactly one edge and undo transaction");
        e.reset(); view.select(start.id); draw(view); plus=plus(view); target=center(view,end);
        drag(view,plus,target); check(e.dropped==1 && d.edges.get(0).to.equals(process.id) && e.before==0,
                "Dropping on a different block preserves the occupied output until the host resolves it");
        view.completeConnectionDrop(e.source,"next",e.target,e.x,e.y);
        check(d.edges.size()==1 && d.edges.get(0).to.equals(end.id) && e.before==1 && e.changed==1,"Only explicit host completion replaces an occupied output");
        e.reset(); view.completeConnectionDrop(start.id,"next",end.id,0,0);
        check(e.before==0 && e.changed==0 && d.edges.size()==1,"Repeating the same connection is a no-op");
        e.reset(); view.select(process.id); draw(view); plus=plus(view); float[] blank=screen(view,process.x+300,process.y+100);
        drag(view,plus,blank);
        check(e.dropped==1 && e.target==null && e.added==0 && e.before==0,"Blank drop requests the host's add workflow without changing the model");
        near(e.x,process.x+300,"Blank drop preserves requested world x"); near(e.y,process.y+100,"Blank drop preserves requested world y");
        e.reset(); view.select(process.id); draw(view); plus=plus(view);
        single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_MOVE,blank[0],blank[1]);
        single(view,MotionEvent.ACTION_CANCEL,blank[0],blank[1]);
        check(e.dropped==0 && e.before==0 && field(view,"dragConnectionSource")==null,"CANCEL removes the preview without a callback or history");
        draw(view); plus=plus(view);
        single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_MOVE,blank[0],blank[1]);
        multi(view,MotionEvent.ACTION_POINTER_DOWN|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),blank[0],blank[1],blank[0]+55,blank[1]);
        multi(view,MotionEvent.ACTION_POINTER_UP|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),blank[0],blank[1],blank[0]+55,blank[1]);
        single(view,MotionEvent.ACTION_UP,blank[0],blank[1]);
        check(e.dropped==0 && e.before==0 && field(view,"dragConnectionTarget")==null,"A second finger cancels connector drag and suppresses residual release");
        e.reset(); view.select(process.id); draw(view); plus=plus(view); drag(view,plus,center(view,process));
        check(e.dropped==1 && e.before==0 && process.id.equals(e.source) && process.id.equals(e.target),"Dropping on self requests the exact source as destination");
        view.completeConnectionDrop(e.source,"next",e.target,e.x,e.y);
        check(d.outgoing(process.id,"next")!=null && d.outgoing(process.id,"next").to.equals(process.id),"Ordinary block self-loop commits through the host contract");
        e.reset(); view.select(process.id); draw(view); plus=plus(view); drag(view,plus,center(view,start));
        check(e.dropped==0 && e.before==0 && e.hint!=null && e.hint.contains("Inizio"),"Dropping into Inizio gives explicit feedback without mutation");
        FlowModel.Node decision=new FlowModel.Node(FlowModel.DECISION,"x > 0",-34,190); d.nodes.set(1,decision); view.setDocument(d,true); view.select(decision.id); draw(view);
        e.reset(); plus=new RectF((RectF)field(view,"falsePlusHit")); drag(view,plus,center(view,end));
        check(e.dropped==1 && decision.id.equals(e.source) && end.id.equals(e.target) && e.before==0,"Decision uses the same host drop contract and retains its exact destination");
        view.completeConnectionDrop(e.source,"false",e.target,e.x,e.y);
        check(d.outgoing(decision.id,"false")!=null && d.outgoing(decision.id,"true")==null,"Explicit Decision completion respects the chosen false branch");
    }

    private void targetRequests() throws Exception {
        FlowCanvasView view=sized(360,640); FlowModel.Document d=document(); Events e=new Events(); e.intercept=true; view.setListener(e); view.setDocument(d,true);
        FlowModel.Node start=d.nodes.get(0), process=d.nodes.get(1), end=d.nodes.get(2);
        view.beginConnection(start.id,"next"); tapNode(view,process);
        check(e.requested==1 && start.id.equals(e.source) && process.id.equals(e.target) && "next".equals(e.branch),"Collega tap asks the host with the known output branch");
        check(d.edges.isEmpty() && e.before==0 && view.isConnecting(),"An intercepted tap awaits the host decision without changing its edge");
        view.completeConnectionDrop(start.id,"next",process.id,0,0);
        check(e.requested==1 && e.before==1 && e.changed==1 && !view.isConnecting(),"Explicit completion bypasses request interception and cannot recurse");
        e.reset(); view.beginConnection(start.id,"next"); tapNode(view,end);
        check(e.requested==1 && d.outgoing(start.id,"next").to.equals(process.id) && e.before==0,"Collega cannot silently replace a used output before host review");
        view.cancelConnection(); e.reset(); view.beginConnection(process.id,"next"); tapNode(view,start);
        check(e.requested==0 && e.hint!=null && e.before==0,"An invalid Start tap is rejected before reaching the host");
        e.reset(); tapNode(view,process);
        check(e.requested==1 && process.id.equals(e.source) && process.id.equals(e.target) && "next".equals(e.branch) && e.before==0 && d.outgoing(process.id,"next")==null,"Collega taps request the self-loop without mutating before the host commits");
        e.reset(); float[] blank=screen(view,process.x+320,process.y+90); tap(view,blank[0],blank[1]);
        check(e.requested==1 && e.target==null && e.added==0 && e.before==0,"Collega on blank lets the host explain/create without a hidden picker");
        view.cancelConnection(); e.reset(); e.intercept=false; view.beginConnection(process.id,"next"); tapNode(view,end);
        check(e.requested==1 && d.outgoing(process.id,"next").to.equals(end.id) && e.before==1,"Legacy hosts returning false retain explicit connect-mode behavior");
        view.setReadOnly(true); e.reset(); view.completeConnectionDrop(process.id,"next",end.id,0,0);
        check(e.requested==0 && e.before==0 && e.changed==0,"Read-only mode rejects deferred host commit");
    }

    private void snapGeometry() throws Exception {
        FlowCanvasView view=sized(420,720); Events e=new Events(); view.setListener(e); FlowModel.Document d=document(); view.setDocument(d,true);
        FlowModel.Node start=d.nodes.get(0), target=d.nodes.get(1); float beforeX=target.x, beforeY=target.y;
        view.select(start.id); draw(view); RectF plus=plus(view);
        float[] outside=screen(view,target.x+FlowModel.width(target.kind),target.y+FlowModel.height(target.kind)/2);
        outside[0]+=18*density(view);
        single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_MOVE,outside[0],outside[1]); draw(view);
        check(target.id.equals(field(view,"dragConnectionTarget")),"Connection snaps within 18 physical dp of a target edge");
        outside[0]+=10*density(view); single(view,MotionEvent.ACTION_MOVE,outside[0],outside[1]);
        check(field(view,"dragConnectionTarget")==null,"Moving beyond the 20 dp magnet clears the destination");
        near(target.x,beforeX,"Hover never changes target x"); near(target.y,beforeY,"Hover never changes target y");
        single(view,MotionEvent.ACTION_CANCEL,outside[0],outside[1]);
        view.zoomBy(1.4f); view.select(start.id); draw(view); plus=plus(view);
        outside=screen(view,target.x+FlowModel.width(target.kind),target.y+FlowModel.height(target.kind)/2); outside[0]+=18*density(view);
        single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_MOVE,outside[0],outside[1]);
        check(target.id.equals(field(view,"dragConnectionTarget")),"Magnet stays 20 physical dp after zoom rather than growing in model units");
        single(view,MotionEvent.ACTION_CANCEL,outside[0],outside[1]);
        FlowModel.Node decision=new FlowModel.Node(FlowModel.DECISION,"x > 0",-34,190); d.nodes.set(1,decision); view.setDocument(d,true); view.select(start.id); draw(view); plus=plus(view);
        float[] corner=screen(view,decision.x+FlowModel.width(decision.kind),decision.y);
        single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_MOVE,corner[0],corner[1]);
        check(field(view,"dragConnectionTarget")==null,"Empty corners around a diamond do not snap as if they were a rectangle");
        single(view,MotionEvent.ACTION_CANCEL,corner[0],corner[1]);
    }

    private void edgeGestures() throws Exception {
        FlowCanvasView view=sized(420,720); Events e=new Events(); view.setListener(e); FlowModel.Document d=document();
        FlowModel.Node start=d.nodes.get(0), process=d.nodes.get(1), end=d.nodes.get(2);
        FlowModel.Edge edge=new FlowModel.Edge(start.id,process.id,"next"); d.edges.add(edge); d.edges.add(new FlowModel.Edge(process.id,end.id,"next"));
        view.setDocument(d,true); view.clearSelection(); draw(view);
        float[] line=screen(view,start.x+FlowModel.width(start.kind)/2,(start.y+FlowModel.height(start.kind)+process.y)/2);
        e.reset(); tap(view,line[0]+8*density(view),line[1]);
        check(e.editedEdge==1 && edge.id.equals(e.edgeId),"Tap near a line opens that exact connection within a 12 dp touch margin");
        check(e.before==0 && e.changed==0 && e.added==0,"Opening connection controls never modifies the diagram");
        e.reset(); float oldPan=number(view,"panX");
        single(view,MotionEvent.ACTION_DOWN,line[0],line[1]); draw(view);
        check(edge.id.equals(field(view,"downEdgeId")),"Pressing a line retains its id for visible pressed feedback");
        single(view,MotionEvent.ACTION_MOVE,line[0]+30*density(view),line[1]+10*density(view)); single(view,MotionEvent.ACTION_UP,line[0]+80*density(view),line[1]+60*density(view));
        check(e.editedEdge==0 && e.before==0 && number(view,"panX")!=oldPan,"Dragging from a line pans normally without opening controls");
        view.fitToContent(); view.select(start.id); draw(view); e.reset(); RectF plus=plus(view); tap(view,plus.centerX(),plus.centerY());
        check(e.addAfter==1 && e.editedEdge==0,"+ has priority over the outgoing line beneath it");
        e.reset(); tapNode(view,process); check(e.editedEdge==0 && view.getSelectedIds().contains(process.id),"Block bodies have priority over connection hit targets");
        view.setSelectionMode(true); e.reset(); line=screen(view,start.x+FlowModel.width(start.kind)/2,(start.y+FlowModel.height(start.kind)+process.y)/2); tap(view,line[0],line[1]);
        check(e.editedEdge==0,"A line tap cannot interrupt multi-selection mode");
        view.setSelectionMode(false); view.setReadOnly(true); e.reset(); tap(view,line[0],line[1]);
        check(e.editedEdge==0 && e.before==0,"Run preview never opens connection editing");
        view.setReadOnly(false);
        FlowModel.Node decision=new FlowModel.Node(FlowModel.DECISION,"x > 0",-34,190); decision.trueLabel="Valido"; decision.falseLabel="Riprova";
        d.nodes.set(1,decision); d.edges.clear(); FlowModel.Edge no=new FlowModel.Edge(decision.id,end.id,"false"); d.edges.add(no); view.setDocument(d,true); draw(view);
        Object visual=((Map<?,?>)field(view,"edgeIndex")).get(no.id); RectF label=(RectF)field(visual,"labelBounds"); float[] labelPoint=screen(view,label.centerX(),label.centerY());
        e.reset(); tap(view,labelPoint[0],labelPoint[1]);
        check(e.editedEdge==1 && no.id.equals(e.edgeId),"A custom branch label opens its own connection, including the false branch");
    }

    private void accessibility() throws Exception {
        FlowCanvasView view=sized(360,640); Events e=new Events(); view.setListener(e); FlowModel.Document d=document();
        FlowModel.Node start=d.nodes.get(0), process=d.nodes.get(1); FlowModel.Edge edge=new FlowModel.Edge(start.id,process.id,"next"); d.edges.add(edge);
        view.setDocument(d,true); draw(view); int add=0x02000003, connect=0x02000002;
        AccessibilityNodeInfo startInfo=view.getAccessibilityNodeProvider().createAccessibilityNodeInfo(1); boolean hasAdd=false;
        for(AccessibilityNodeInfo.AccessibilityAction action:startInfo.getActionList()) if(action.getId()==add) hasAdd=true;
        check(hasAdd,"TalkBack exposes a distinct Add-after action on Start"); startInfo.recycle();
        view.getAccessibilityNodeProvider().performAction(1,add,null);
        check(e.addAfter==1 && e.connect==0,"Accessible Add uses the same new-block contract as visible +");
        view.getAccessibilityNodeProvider().performAction(1,connect,null); check(e.connect==1,"Accessible Collega stays available as a separate command");
        int edgeVirtualId=d.nodes.size()+1; AccessibilityNodeInfo edgeInfo=view.getAccessibilityNodeProvider().createAccessibilityNodeInfo(edgeVirtualId);
        check(edgeInfo!=null && edgeInfo.getContentDescription().toString().contains("Collegamento da"),"Visible edges are discoverable as named accessibility nodes");
        edgeInfo.recycle(); e.reset(); view.getAccessibilityNodeProvider().performAction(edgeVirtualId,AccessibilityNodeInfo.ACTION_CLICK,null);
        check(e.editedEdge==1 && edge.id.equals(e.edgeId),"TalkBack opens the same exact connection controls");
        e.reset(); e.intercept=true; view.beginConnection(start.id,"next");
        view.getAccessibilityNodeProvider().performAction(2,AccessibilityNodeInfo.ACTION_CLICK,null);
        check(e.requested==1 && process.id.equals(e.target) && e.before==0,"Accessible connect destinations use host review before commit");
        view.cancelConnection(); view.setReadOnly(true); e.reset();
        check(!view.getAccessibilityNodeProvider().performAction(1,add,null),"Read-only accessibility rejects Add-after");
        check(!view.getAccessibilityNodeProvider().performAction(edgeVirtualId,AccessibilityNodeInfo.ACTION_CLICK,null) && e.editedEdge==0,"Read-only accessibility rejects editing an edge");
    }

    private FlowCanvasView sized(int width,int height) {
        FlowCanvasView view=new FlowCanvasView(activity); float d=density(view);
        view.measure(View.MeasureSpec.makeMeasureSpec((int)(width*d),View.MeasureSpec.EXACTLY),View.MeasureSpec.makeMeasureSpec((int)(height*d),View.MeasureSpec.EXACTLY));
        view.layout(0,0,(int)(width*d),(int)(height*d)); view.setMotionEnabled(false); return view;
    }
    private static FlowModel.Document document() {
        FlowModel.Document d=new FlowModel.Document(); d.nodes.add(new FlowModel.Node(FlowModel.START,"Inizio",0,0));
        d.nodes.add(new FlowModel.Node(FlowModel.PROCESS,"x = 1",-24,190)); d.nodes.add(new FlowModel.Node(FlowModel.END,"Fine",0,410)); return d;
    }
    private void check(boolean value,String label) { if(!value)throw new AssertionError(label); checks++; report.append("PASS ").append(checks).append(": ").append(label).append('\n'); }
    private void near(float actual,float expected,String label) { check(Math.abs(actual-expected)<.05f,label+" ("+actual+")"); }
    private void ui(Action action) {
        final Throwable[] failure={null}; runOnMainSync(()->{try{action.run();}catch(Throwable error){failure[0]=error;}});
        if(failure[0]!=null)throw new RuntimeException(failure[0]);
    }
    private static float density(FlowCanvasView view) { return view.getResources().getDisplayMetrics().density; }
    private static Object field(Object value,String name) throws Exception { Field f=value.getClass().getDeclaredField(name); f.setAccessible(true); return f.get(value); }
    private static float number(Object value,String name) throws Exception { return ((Number)field(value,name)).floatValue(); }
    private static int integer(Object value,String name) throws Exception { return ((Number)field(value,name)).intValue(); }
    private static RectF plus(FlowCanvasView view) throws Exception { return new RectF((RectF)field(view,"plusHit")); }
    private static float[] screen(FlowCanvasView view,float x,float y) throws Exception {
        float scale=number(view,"zoom")*density(view); return new float[]{number(view,"panX")+x*scale,number(view,"panY")+y*scale};
    }
    private static float[] center(FlowCanvasView view,FlowModel.Node node) throws Exception { return screen(view,node.x+FlowModel.width(node.kind)/2,node.y+FlowModel.height(node.kind)/2); }
    private static void draw(FlowCanvasView view) { Bitmap bitmap=Bitmap.createBitmap(view.getWidth(),view.getHeight(),Bitmap.Config.ARGB_8888); view.draw(new Canvas(bitmap)); bitmap.recycle(); }
    private void save(FlowCanvasView view,String name) throws Exception {
        Bitmap bitmap=Bitmap.createBitmap(view.getWidth(),view.getHeight(),Bitmap.Config.ARGB_8888); view.draw(new Canvas(bitmap));
        try(FileOutputStream out=new FileOutputStream(new File(directory,name))) { bitmap.compress(Bitmap.CompressFormat.PNG,100,out); } bitmap.recycle();
    }
    private static void tapNode(FlowCanvasView view,FlowModel.Node node) throws Exception { float[] p=center(view,node); tap(view,p[0],p[1]); }
    private static void tap(FlowCanvasView view,float x,float y) { time+=1000; single(view,MotionEvent.ACTION_DOWN,x,y); single(view,MotionEvent.ACTION_UP,x,y); }
    private static void drag(FlowCanvasView view,RectF plus,float[] target) {
        time+=1000; single(view,MotionEvent.ACTION_DOWN,plus.centerX(),plus.centerY()); single(view,MotionEvent.ACTION_MOVE,target[0],target[1]); single(view,MotionEvent.ACTION_UP,target[0],target[1]);
    }
    private static void single(FlowCanvasView view,int action,float x,float y) {
        time+=32; if(action==MotionEvent.ACTION_DOWN)downTime=time; MotionEvent event=MotionEvent.obtain(downTime,time,action,x,y,0); event.setSource(InputDevice.SOURCE_TOUCHSCREEN); view.onTouchEvent(event); event.recycle();
    }
    private static void multi(FlowCanvasView view,int action,float x1,float y1,float x2,float y2) {
        time+=32; MotionEvent.PointerProperties p1=new MotionEvent.PointerProperties(),p2=new MotionEvent.PointerProperties(); p1.id=0;p2.id=1;p1.toolType=p2.toolType=MotionEvent.TOOL_TYPE_FINGER;
        MotionEvent.PointerCoords c1=new MotionEvent.PointerCoords(),c2=new MotionEvent.PointerCoords(); c1.x=x1;c1.y=y1;c2.x=x2;c2.y=y2;c1.pressure=c2.pressure=1;c1.size=c2.size=1;
        MotionEvent event=MotionEvent.obtain(downTime,time,action,2,new MotionEvent.PointerProperties[]{p1,p2},new MotionEvent.PointerCoords[]{c1,c2},0,0,1,1,0,0,InputDevice.SOURCE_TOUCHSCREEN,0);
        view.onTouchEvent(event);event.recycle();
    }
}
