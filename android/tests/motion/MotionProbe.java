package it.frociart.app;

import android.Manifest;
import android.animation.ValueAnimator;
import android.app.Activity;
import android.app.Instrumentation;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Path;
import android.graphics.PathMeasure;
import android.graphics.RectF;
import android.os.Bundle;
import android.os.SystemClock;
import android.provider.Settings;
import android.util.Log;
import android.view.InputDevice;
import android.view.MotionEvent;
import android.view.View;
import java.io.File;
import java.io.FileOutputStream;
import java.lang.reflect.Field;
import java.util.HashMap;
import java.util.Map;

/** Real Android Canvas/MotionEvent/ValueAnimator checks; no mocked frames or FPS benchmark. */
public final class MotionProbe extends Instrumentation {
    private interface Action { void run() throws Exception; }
    private interface Condition { boolean value() throws Exception; }
    private MotionChecksActivity activity;
    private FlowCanvasView view;
    private File directory;
    private final StringBuilder report = new StringBuilder();
    private int checks;
    private float originalScale;
    private boolean scaleSaved;
    private static long eventTime = SystemClock.uptimeMillis() + 1000;

    private static final class Events implements FlowCanvasView.Listener {
        int before, changed, added, connect, dropped, manual;
        float addX, addY, dropX, dropY;
        String addSource, addBranch, dropSource, dropTarget;
        @Override public void onBeforeChange() { before++; }
        @Override public void onDiagramChanged(String label) { changed++; }
        @Override public void onSelectionChanged() { }
        @Override public void onEditRequested(String id) { }
        @Override public void onAddRequested(float x, float y, String source, String branch) {
            added++; addX=x; addY=y; addSource=source; addBranch=branch;
        }
        @Override public void onConnectRequested(String id) { connect++; }
        @Override public void onConnectionStateChanged(String source, String branch) { }
        @Override public void onHint(String text) { }
        @Override public void onConnectionDropRequested(String source, String target, float x, float y) {
            dropped++; dropSource=source; dropTarget=target; dropX=x; dropY=y;
        }
        @Override public void onManualNodeMove() { manual++; }
        void reset() { before=changed=added=connect=dropped=manual=0; }
    }

    @Override public void onCreate(Bundle arguments) { super.onCreate(arguments); start(); }

    @Override public void onStart() {
        boolean success = false;
        directory = new File(getTargetContext().getExternalFilesDir(null), "motion-1.2.0"); directory.mkdirs();
        try {
            getUiAutomation().adoptShellPermissionIdentity(Manifest.permission.WRITE_SECURE_SETTINGS);
            originalScale = Settings.Global.getFloat(getTargetContext().getContentResolver(), Settings.Global.ANIMATOR_DURATION_SCALE, 1);
            scaleSaved = true;
            Settings.Global.putFloat(getTargetContext().getContentResolver(), Settings.Global.ANIMATOR_DURATION_SCALE, 1);
            Intent intent = new Intent().setClassName(getTargetContext().getPackageName(), MotionChecksActivity.class.getName())
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity = (MotionChecksActivity) startActivitySync(intent); view = activity.canvas;
            check(await(() -> view.isAttachedToWindow() && view.hasWindowFocus() && ValueAnimator.areAnimatorsEnabled(), 12000),
                    "Visible attached Android surface and system animations are ready");
            ui(() -> {
                String historical = FlowCanvasChecks.run(activity);
                check(historical.contains("CANVAS CHECKS PASSED: 118"), "All 118 historical canvas checks remain intact");
                report.append(historical).append('\n');
                gestureChecks(); mobileChecks();
            });
            layoutChecks(); largeLayoutChecks(); runChecks(); cameraChecks(); pulseChecks(); systemMotionChecks();
            ui(() -> {
                view.setReadOnly(false); view.setMotionEnabled(true); view.select(view.getDocument().nodes.get(0).id); draw(view);
                check(field(view,"connectorAnimator") != null, "Pulse is active before detach");
                activity.host.removeView(view);
                check(field(view,"connectorAnimator") == null && field(view,"runAnimator") == null && field(view,"cameraAnimator") == null
                        && field(view,"layoutAnimator") == null, "Detaching the real view cancels every animator");
                check(!(Boolean)field(view,"observingMotion"), "Detach unregisters the system motion observer");
            });
            success = true;
        } catch (Throwable error) {
            report.append("MOTION CHECKS FAILED\n").append(Log.getStackTraceString(error));
            Log.e("FROCIART_MOTION", "MOTION CHECKS FAILED", error);
        } finally {
            if (scaleSaved) Settings.Global.putFloat(getTargetContext().getContentResolver(), Settings.Global.ANIMATOR_DURATION_SCALE, originalScale);
            getUiAutomation().dropShellPermissionIdentity();
            if (activity != null) ui(() -> { view.setMotionEnabled(false); activity.finish(); });
            report.append(success ? "MOTION CHECKS PASSED: " : "MOTION CHECKS FAILED AFTER: ").append(checks).append('\n');
            try (FileOutputStream out = new FileOutputStream(new File(directory,"report.txt"))) { out.write(report.toString().getBytes("UTF-8")); }
            catch (Exception error) { Log.e("FROCIART_MOTION", "Writing report", error); }
            Log.i("FROCIART_MOTION", report.toString());
            Bundle result = new Bundle(); result.putString("stream", report.toString());
            finish(success ? Activity.RESULT_OK : Activity.RESULT_CANCELED, result);
        }
    }

    private void gestureChecks() throws Exception {
        FlowCanvasView v = sized(360,560); Events e = new Events(); v.setListener(e);
        FlowModel.Document d = pair(false); v.setDocument(d,true);
        FlowModel.Node a=d.nodes.get(0), b=d.nodes.get(1); v.select(a.id); draw(v); e.reset();
        float[] plus=plus(v), blank=screen(v, a.x+210, a.y+170);
        String svg=v.createSvg();
        single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]);
        single(v,MotionEvent.ACTION_MOVE,blank[0],blank[1]); draw(v);
        check(((Number)field(v,"gesture")).intValue()==8, "Dragging + enters a connector gesture, not pan or node movement");
        check(v.createSvg().equals(svg), "Connector ghost is absent from SVG and the model");
        single(v,MotionEvent.ACTION_UP,blank[0]+80,blank[1]+100);
        check(e.added==1 && e.before==0 && e.changed==0, "Dropping + on empty space requests a picker without an undo entry");
        near(e.addX,a.x+210,"Empty drop uses the last MOVE world x"); near(e.addY,a.y+170,"Empty drop uses the last MOVE world y");
        check(a.id.equals(e.addSource) && "next".equals(e.addBranch), "New block request retains its source and output");
        check(e.manual==0,"Connector drag never disables automatic layout as a manual node move");
        v.cancelConnection(); e.reset(); v.select(a.id); draw(v); plus=plus(v);
        single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]); single(v,MotionEvent.ACTION_MOVE,blank[0],blank[1]);
        single(v,MotionEvent.ACTION_CANCEL,blank[0],blank[1]);
        check(e.added==0 && e.before==0 && field(v,"dragConnectionSource")==null, "CANCEL removes a connector ghost without creation or history");
        draw(v); plus=plus(v); single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]); single(v,MotionEvent.ACTION_MOVE,blank[0],blank[1]);
        multi(v,MotionEvent.ACTION_POINTER_DOWN|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),blank[0],blank[1],blank[0]+55,blank[1]);
        multi(v,MotionEvent.ACTION_MOVE,blank[0]-12,blank[1],blank[0]+67,blank[1]);
        multi(v,MotionEvent.ACTION_POINTER_UP|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),blank[0]-12,blank[1],blank[0]+67,blank[1]);
        single(v,MotionEvent.ACTION_UP,blank[0]-12,blank[1]);
        check(e.added==0 && e.dropped==0 && e.before==0, "Pinch cancels a connector drag and suppresses the residual finger");
        v.fitToContent(); v.select(a.id); draw(v); plus=plus(v); e.reset();
        single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]); single(v,MotionEvent.ACTION_UP,plus[0],plus[1]);
        check(e.connect==1 && e.added==0,"A tap on + keeps the accessible branch/connect picker");
        e.reset(); draw(v); plus=plus(v); float[] target=center(v,b);
        single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]); single(v,MotionEvent.ACTION_MOVE,target[0],target[1]);
        single(v,MotionEvent.ACTION_UP,blank[0],blank[1]);
        check(d.edges.size()==1 && d.edges.get(0).to.equals(b.id),"Dropping + on a node connects the last MOVE target despite a different UP");
        check(e.before==1 && e.changed==1 && e.added==0,"Node drop creates exactly one atomic undo transaction");
        e.reset(); v.completeConnectionDrop(a.id,"next",b.id,0,0);
        check(e.before==0 && e.changed==0 && d.edges.size()==1,"Repeating the same drop does not create duplicate edges or history");
        FlowModel.Node c=new FlowModel.Node(FlowModel.OUTPUT,"x",260,280); d.nodes.add(c); v.notifyModelChanged();
        v.completeConnectionDrop(a.id,"next",c.id,0,0);
        check(d.edges.size()==1 && d.edges.get(0).to.equals(c.id) && e.before==1 && e.changed==1,"A different drop atomically replaces the source output");
        e.reset(); v.select(a.id); draw(v); plus=plus(v); target=center(v,a);
        single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]); single(v,MotionEvent.ACTION_MOVE,target[0],target[1]); single(v,MotionEvent.ACTION_UP,target[0],target[1]);
        check(e.added==0 && e.changed==0 && e.dropped==0,"Dropping the connector back onto its source cancels");
        d=pair(true); a=d.nodes.get(0); b=d.nodes.get(1); v.setDocument(d,true); v.select(a.id); draw(v); e.reset(); plus=plus(v); target=center(v,b);
        single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]); single(v,MotionEvent.ACTION_MOVE,target[0],target[1]); single(v,MotionEvent.ACTION_UP,target[0],target[1]);
        check(e.dropped==1 && a.id.equals(e.dropSource) && b.id.equals(e.dropTarget),"Decision drop asks for a branch with its exact target");
        check(e.before==0 && d.edges.isEmpty() && !v.isConnecting(),"Unanswered decision drop keeps no hidden creation or mutation");
        v.completeConnectionDrop(e.dropSource,"false",e.dropTarget,e.dropX,e.dropY);
        check(d.edges.size()==1 && "false".equals(d.edges.get(0).branch) && e.before==1 && e.changed==1,"Choosing the false branch completes the requested drop once");
        e.reset(); v.select(a.id); draw(v); plus=plus(v); blank=screen(v,a.x+220,a.y+195);
        single(v,MotionEvent.ACTION_DOWN,plus[0],plus[1]); single(v,MotionEvent.ACTION_MOVE,blank[0],blank[1]); single(v,MotionEvent.ACTION_UP,blank[0]+100,blank[1]+100);
        check(e.dropped==1 && e.dropTarget==null && e.added==0,"Decision drop on blank defers creation until the branch is selected");
        v.completeConnectionDrop(e.dropSource,"true",null,e.dropX,e.dropY);
        check(e.added==1 && "true".equals(e.addBranch) && e.before==0,"Decision blank drop carries the chosen branch to the block picker");
        v.cancelConnection(); e.reset(); v.setReadOnly(true); v.completeConnectionDrop(a.id,"next",b.id,0,0);
        check(e.before==0 && e.changed==0 && e.added==0,"Read-only mode rejects deferred drop completion");
        v.setReadOnly(false); e.reset(); float[] p=center(v,a); float oldX=a.x;
        single(v,MotionEvent.ACTION_DOWN,p[0],p[1]); single(v,MotionEvent.ACTION_MOVE,p[0]+35,p[1]+15); single(v,MotionEvent.ACTION_MOVE,p[0]+45,p[1]+20);
        float moved=a.x; single(v,MotionEvent.ACTION_UP,p[0]+200,p[1]+100);
        check(e.manual==1 && e.before==1 && e.changed==1 && moved!=oldX,"Real node drag emits one manual-move callback and one undo transaction");
        near(a.x,moved,"Node drag still ignores later UP coordinates");
    }

    private void mobileChecks() throws Exception {
        FlowCanvasView v=sized(320,560); v.setMobileViewport(true);
        FlowModel.Document d=new FlowModel.Document();
        FlowModel.Node first=new FlowModel.Node(FlowModel.START,"Inizio",0,0), last=new FlowModel.Node(FlowModel.END,"Fine",1000,3200);
        d.nodes.add(first); d.nodes.add(last); v.setDocument(d,true); draw(v);
        near(number(v,"zoom"),1.05f,"Mobile initial fit opens at reading zoom 1.05 on a 320 dp phone");
        float density=v.getResources().getDisplayMetrics().density;
        check(screen(v,first.x,first.y)[1]>=64*density,"Mobile start is below the 64 dp assist overlay");
        v.fitToContent(); check(number(v,"zoom")>=1.04f,"Mobile Centra restores the larger reading zoom for a long graph");
        check(FlowModel.width(FlowModel.DECISION)*number(v,"zoom")+32<=320,"Default reading width fits a decision and margins on 320 dp");
        v.zoomBy(.0001f); near(number(v,"zoom"),.8f,"Mobile zoom-out is bounded at the readable minimum");
        v.zoomBy(10000); near(number(v,"zoom"),3,"Mobile zoom-in remains bounded"); v.fitToContent(); v.setReadOnly(true);
        single(v,MotionEvent.ACTION_DOWN,10,10); single(v,MotionEvent.ACTION_MOVE,-50000,-50000);
        float finalX=number(v,"panX"),finalY=number(v,"panY"); single(v,MotionEvent.ACTION_UP,-90000,-90000);
        near(number(v,"panX"),finalX,"Bounded camera does not continue panning on release x"); near(number(v,"panY"),finalY,"Bounded camera does not continue panning on release y");
        float bottom=screen(v,last.x,last.y+FlowModel.height(last.kind))[1];
        check(bottom<v.getHeight()-88*density && bottom>v.getHeight()-150*density,"Bottom pan bound leaves the last node above the fixed controls");
        single(v,MotionEvent.ACTION_DOWN,10,10); single(v,MotionEvent.ACTION_MOVE,50000,50000); single(v,MotionEvent.ACTION_UP,50000,50000);
        check(screen(v,first.x,first.y)[1]>=64*density && screen(v,first.x,first.y)[1]<130*density,"Top pan bound keeps the first block and nearby margin reachable");
        near(first.x,0,"Mobile camera bounds never change model x"); near(last.y,3200,"Mobile camera bounds never change model y");
        v.focusNodeAnimated(last.id); check(center(v,last)[1]<v.getHeight()-88*density,"Unattached/reduced-motion focus immediately places target above controls");
        v.setMobileViewport(false); v.fitToContent(); check(number(v,"zoom")<.8f,"Turning mobile bounds off retains the compatible unrestricted overview API");
        FlowModel.Document shortDiagram=new FlowModel.Document(); shortDiagram.nodes.add(first.copy());
        v.setMobileViewport(true); v.setDocument(shortDiagram,true); v.focusNodeAnimated(first.id);
        near(screen(v,first.x,first.y)[1]/density,72,"Mobile focus keeps Inizio at the top even in a one-block diagram");
    }

    private void layoutChecks() throws Exception {
        ui(() -> {
            view.setDocument(pair(false),true); view.setMotionEnabled(true); view.clearSelection();
            FlowModel.Document d=view.getDocument(); Map<String,float[]> before=positions(d);
            d.nodes.get(0).x+=84; d.nodes.get(0).y+=50; d.nodes.get(1).x-=35; d.nodes.get(1).y+=90;
            view.notifyModelChanged(); Map<String,float[]> after=positions(d);
            String expected=view.createSvg(); RectF expectedBounds=view.getDiagramBounds(); Bitmap expectedBitmap=export(view,expectedBounds);
            Events events=new Events(); view.setListener(events);
            view.animateLayout(before,after,1000);
            ValueAnimator animator=field(view,"layoutAnimator"); check(animator!=null,"Visible layout starts a real ValueAnimator");
            animator.setCurrentFraction(.4f); draw(view);
            FlowModel.Node first=d.nodes.get(0); Object visual=((Map<?,?>)field(view,"nodeIndex")).get(first.id); RectF rendered=field(visual,"bounds");
            check(rendered.left>before.get(first.id)[0] && rendered.left<first.x,"Intermediate frame interpolates cached visual geometry");
            near(first.x,after.get(first.id)[0],"Animation frames leave final model x untouched"); near(first.y,after.get(first.id)[1],"Animation frames leave final model y untouched");
            check(events.before==0 && events.changed==0,"Layout animation creates no undo or model-change events");
            check(view.createSvg().equals(expected),"SVG exported during animation contains final positions only");
            check(view.getDiagramBounds().equals(expectedBounds),"Export bounds remain final during a layout tween");
            Bitmap actual=export(view,expectedBounds); check(actual.sameAs(expectedBitmap),"PNG/PDF canvas output is pixel-identical during a layout tween"); actual.recycle(); expectedBitmap.recycle();
            after.get(first.id)[0]+=999; before.get(first.id)[0]-=999; animator.setCurrentFraction(.5f); draw(view);
            rendered=field(visual,"bounds"); check(rendered.left>0 && rendered.left<first.x,"Layout copies caller position arrays instead of retaining mutable input");
            snap("01-layout-intermediate");
            view.setMotionEnabled(false); draw(view); rendered=field(visual,"bounds");
            near(rendered.left,first.x,"Disabling motion settles cached geometry to final positions");
            check(field(view,"layoutAnimator")==null && ((Map<?,?>)field(view,"layoutPositions")).isEmpty(),"Reduced motion leaves no layout animator or pending positions");
            view.setMotionEnabled(true);
        });
    }

    private void runChecks() throws Exception {
        ui(() -> {
            FlowModel.Document d=pair(true); FlowModel.Node a=d.nodes.get(0),b=d.nodes.get(1);
            a.trueColor="#9771DC"; a.falseColor="#D68A18";
            d.edges.add(new FlowModel.Edge(a.id,b.id,"true")); d.edges.add(new FlowModel.Edge(a.id,b.id,"false"));
            view.setDocument(d,true); view.clearSelection(); view.setReadOnly(true);
            String svg=view.createSvg(); RectF bounds=view.getDiagramBounds(); Bitmap expected=export(view,bounds);
            view.animateRunStep(a.id,b.id,"true",1000);
            ValueAnimator animator=field(view,"runAnimator"); check(animator!=null,"Executed edge starts a finite native animator"); animator.setCurrentFraction(.4f);
            check(((Integer)field(view,"runColor"))==Color.parseColor(a.trueColor),"Run uses the custom true-branch color");
            PathMeasure measure=new PathMeasure((Path)field(view,"runPath"),false); float[] p=new float[2]; measure.getPosTan(0,p,null);
            near(p[0],a.x,"True run trace begins on the left decision outlet");
            check(view.createSvg().equals(svg),"Run highlight and luminous point never enter SVG");
            Bitmap actual=export(view,bounds); check(actual.sameAs(expected),"Run trace never enters PNG/PDF export pixels"); actual.recycle(); expected.recycle(); snap("02-run-true");
            view.animateRunStep(a.id,b.id,"false",1000); animator=field(view,"runAnimator"); animator.setCurrentFraction(.4f);
            check(((Integer)field(view,"runColor"))==Color.parseColor(a.falseColor),"Run chooses the custom false color even with the same destination");
            measure.setPath((Path)field(view,"runPath"),false); measure.getPosTan(0,p,null); near(p[0],a.x+FlowModel.width(a.kind),"False run trace begins on the right decision outlet"); snap("03-run-false");
            view.animateRunStep(a.id,b.id,null,1000); check(number(view,"runLength")==0,"Ambiguous branch does not animate a guessed edge");
            view.animateRunStep(a.id,b.id,"true",1000); view.clearRunAnimation();
            check(field(view,"runAnimator")==null && field(view,"runDestination")==null && number(view,"runLength")==0,"Stop clears the finite trace and destination pulse");
            check(b.id.equals(field(view,"highlightId")),"Stopping the trace preserves the current execution node");
            view.animateRunStep(a.id,b.id,"true",300); view.setHighlight(null);
            check(field(view,"runAnimator")==null && field(view,"highlightId")==null,"Reset clears both run motion and static highlight");
            view.animateRunStep(a.id,b.id,"true",300);
        });
        check(await(() -> field(view,"runAnimator")==null,8000),"Run animation completes without an idle redraw loop");
    }

    private void largeLayoutChecks() throws Exception {
        ui(() -> {
            FlowModel.Document d=new FlowModel.Document();
            for(int i=0;i<500;i++)d.nodes.add(new FlowModel.Node(FlowModel.PROCESS,"x = "+i,(i%5)*310,(i/5)*200));
            for(int i=0;i<1500;i++){FlowModel.Node a=d.nodes.get(i%500),b=d.nodes.get((i+1)%500);d.edges.add(new FlowModel.Edge(a.id,b.id,"next"));}
            view.setDocument(d,true); Map<String,float[]> before=positions(d);for(FlowModel.Node n:d.nodes){n.x+=30;n.y+=40;}view.notifyModelChanged();
            view.animateLayout(before,positions(d),500);ValueAnimator animator=field(view,"layoutAnimator");
            check(animator!=null && ((Map<?,?>)field(view,"layoutPositions")).isEmpty(),"Large 500/1500 layout uses a finite fade instead of moving every route");
            animator.setCurrentFraction(.3f);draw(view);long generation=((Number)field(view,"geometryGeneration")).longValue();
            Object edge=((Map<?,?>)field(view,"edgeIndex")).get(d.edges.get(0).id);Object retainedPoints=field(edge,"points");Object retainedPath=field(edge,"path");
            animator.setCurrentFraction(.6f);draw(view);
            check(((Number)field(view,"geometryGeneration")).longValue()==generation,"Large-layout animation frames reuse final node geometry");
            check(field(edge,"points")==retainedPoints && field(edge,"path")==retainedPath,"Large-layout frames retain edge paths and route arrays");
            check(!(Boolean)field(view,"dirty"),"Large-layout animation does not request model geometry rebuilds each frame");
            view.setMotionEnabled(false);check(number(view,"layoutOpacity")==1,"Stopping a large-layout fade leaves the diagram fully opaque");view.setMotionEnabled(true);
        });
    }

    private void cameraChecks() throws Exception {
        final float[] stopped=new float[2];
        ui(() -> {
            FlowModel.Document d=pair(false); d.nodes.get(1).y=1300; view.setDocument(d,true); view.setMobileViewport(true); view.setReadOnly(true);
            view.zoomBy(1.2f/number(view,"zoom")); view.animateRunStep(d.nodes.get(0).id,d.nodes.get(1).id,"next",500);
            near(number(view,"cameraEndZoom"),1.2f,"Run following preserves the user's 1.2 zoom"); view.clearRunAnimation();
            view.focusNodeAnimated(d.nodes.get(1).id); ValueAnimator camera=field(view,"cameraAnimator"); check(camera!=null,"Focus starts a finite camera animation on a visible mobile canvas");
            camera.setCurrentFraction(.4f); stopped[0]=number(view,"panX"); stopped[1]=number(view,"panY");
            single(view,MotionEvent.ACTION_DOWN,5,5); single(view,MotionEvent.ACTION_UP,5,5);
            check(field(view,"cameraAnimator")==null,"Direct touch immediately cancels the camera animation");
            near(number(view,"panX"),stopped[0],"Interrupting focus does not jump to its final x"); near(number(view,"panY"),stopped[1],"Interrupting focus does not jump to its final y");
        });
        SystemClock.sleep(420);
        ui(() -> { near(number(view,"panX"),stopped[0],"Interrupted camera remains stopped on later frames x"); near(number(view,"panY"),stopped[1],"Interrupted camera remains stopped on later frames y"); view.setMobileViewport(false); });
    }

    private void pulseChecks() throws Exception {
        final float[] phase={0};
        ui(() -> {
            view.setDocument(pair(false),true); view.setReadOnly(false); view.setMotionEnabled(true); view.select(view.getDocument().nodes.get(0).id); draw(view);
            check(field(view,"connectorAnimator")!=null,"Selecting an eligible visible node starts connector motion");
            RectF hit=field(view,"plusHit"); float density=view.getResources().getDisplayMetrics().density;
            near(hit.width()/density,48,"Pulsing connector retains an exact 48 dp hit width"); near(hit.height()/density,48,"Pulsing connector retains an exact 48 dp hit height");
            phase[0]=number(view,"connectorPhase");
        });
        check(await(() -> Math.abs(number(view,"connectorPhase")-phase[0])>.01f,8000),"Connector advances through actual Android animation frames");
        ui(() -> {
            snap("04-connector-pulse"); view.clearSelection(); check(field(view,"connectorAnimator")==null,"Clearing selection immediately stops the repeating animator");
            view.select(view.getDocument().nodes.get(0).id); draw(view); view.setSelectionMode(true);
            check(field(view,"connectorAnimator")==null,"Selection-box mode stops connector motion"); view.setSelectionMode(false); draw(view);
            view.setReadOnly(true); check(field(view,"connectorAnimator")==null,"Execution read-only mode stops connector motion"); view.setReadOnly(false); draw(view);
            view.setVisibility(View.INVISIBLE);
            check(field(view,"connectorAnimator")==null,"Hiding the actual view stops repeating motion"); view.setVisibility(View.VISIBLE); draw(view);
            check(field(view,"connectorAnimator")!=null,"Showing the selected canvas restarts eligible connector motion");
            view.setMotionEnabled(false); check(!view.isMotionEnabled() && field(view,"connectorAnimator")==null,"App animation preference stops the pulse");
            FlowModel.Document d=view.getDocument(); view.animateRunStep(d.nodes.get(0).id,d.nodes.get(1).id,"next",500);
            check(field(view,"runAnimator")==null,"App reduced motion makes execution highlight immediate"); view.setMotionEnabled(true); draw(view);
            float[] p=plus(view), blank=screen(view,212,178);
            single(view,MotionEvent.ACTION_DOWN,p[0],p[1]); single(view,MotionEvent.ACTION_MOVE,blank[0],blank[1]);
            check(field(view,"connectorAnimator")==null,"Connector drag stops the repeating pulse"); snap("05-connector-drag-ghost"); single(view,MotionEvent.ACTION_CANCEL,blank[0],blank[1]); draw(view);
        });
    }

    private void systemMotionChecks() throws Exception {
        check(Settings.Global.putFloat(getTargetContext().getContentResolver(), Settings.Global.ANIMATOR_DURATION_SCALE,0),"Native Android reduce-motion setting applied for the test");
        check(await(() -> field(view,"connectorAnimator")==null && !(Boolean)field(view,"systemMotionEnabled"),8000),"System setting observer stops a running connector pulse");
        ui(() -> {
            FlowModel.Document d=view.getDocument(); Map<String,float[]> before=positions(d); d.nodes.get(0).x+=45; view.notifyModelChanged(); Map<String,float[]> after=positions(d);
            view.animateLayout(before,after,500); view.animateRunStep(d.nodes.get(0).id,d.nodes.get(1).id,"next",500); view.focusNodeAnimated(d.nodes.get(1).id); draw(view);
            check(field(view,"layoutAnimator")==null && field(view,"runAnimator")==null && field(view,"cameraAnimator")==null && field(view,"connectorAnimator")==null,
                    "Android reduced motion suppresses layout, run, focus and pulse animators");
            check(view.isMotionEnabled(),"System reduced motion does not overwrite the saved app preference");
        });
        Settings.Global.putFloat(getTargetContext().getContentResolver(),Settings.Global.ANIMATOR_DURATION_SCALE,1);
        check(await(() -> (Boolean)field(view,"systemMotionEnabled") && ValueAnimator.areAnimatorsEnabled(),8000),"System motion setting is restored before lifecycle cleanup");
    }

    private FlowCanvasView sized(int width,int height) {
        FlowCanvasView v=new FlowCanvasView(activity); float d=activity.getResources().getDisplayMetrics().density;
        int w=(int)(width*d),h=(int)(height*d); v.measure(View.MeasureSpec.makeMeasureSpec(w,View.MeasureSpec.EXACTLY),View.MeasureSpec.makeMeasureSpec(h,View.MeasureSpec.EXACTLY));v.layout(0,0,w,h); return v;
    }
    private static FlowModel.Document pair(boolean decision) { FlowModel.Document d=new FlowModel.Document();d.nodes.add(new FlowModel.Node(decision?FlowModel.DECISION:FlowModel.PROCESS,decision?"x > 0":"x = 5",0,0));d.nodes.add(new FlowModel.Node(FlowModel.OUTPUT,"x",0,280));return d; }
    private static Map<String,float[]> positions(FlowModel.Document d) { Map<String,float[]> out=new HashMap<>();for(FlowModel.Node n:d.nodes)out.put(n.id,new float[]{n.x,n.y});return out; }
    private void ui(Action action) { final Throwable[] failure={null};runOnMainSync(()->{try{action.run();}catch(Throwable error){failure[0]=error;}});if(failure[0]!=null)throw new RuntimeException(failure[0]); }
    private boolean await(Condition condition,long timeout) throws Exception { long end=SystemClock.uptimeMillis()+timeout;while(SystemClock.uptimeMillis()<end){final boolean[] yes={false};ui(()->yes[0]=condition.value());if(yes[0])return true;SystemClock.sleep(50);}return false; }
    private void check(boolean condition,String label) { if(!condition)throw new AssertionError(label);checks++;report.append("PASS ").append(label).append('\n'); }
    private void near(float actual,float expected,String label) { check(Math.abs(actual-expected)<.12f,label+" ("+actual+" / "+expected+")"); }
    @SuppressWarnings("unchecked") private static <T>T field(Object object,String name)throws Exception { Field f=object.getClass().getDeclaredField(name);f.setAccessible(true);return (T)f.get(object); }
    private static float number(Object object,String name)throws Exception { return ((Number)field(object,name)).floatValue(); }
    private static float[] screen(FlowCanvasView v,float x,float y)throws Exception { float s=number(v,"zoom")*v.getResources().getDisplayMetrics().density;return new float[]{x*s+number(v,"panX"),y*s+number(v,"panY")}; }
    private static float[] center(FlowCanvasView v,FlowModel.Node n)throws Exception { return screen(v,n.x+FlowModel.width(n.kind)/2,n.y+FlowModel.height(n.kind)/2); }
    private static float[] plus(FlowCanvasView v)throws Exception { RectF hit=field(v,"plusHit");if(hit.isEmpty())throw new AssertionError("Connector must be visible for this gesture fixture");return new float[]{hit.centerX(),hit.centerY()}; }
    private static void single(FlowCanvasView v,int action,float x,float y) { eventTime+=20;MotionEvent e=MotionEvent.obtain(eventTime-20,eventTime,action,x,y,0);e.setSource(InputDevice.SOURCE_TOUCHSCREEN);v.onTouchEvent(e);e.recycle(); }
    private static void multi(FlowCanvasView v,int action,float x1,float y1,float x2,float y2) { eventTime+=20;MotionEvent.PointerProperties[] p={new MotionEvent.PointerProperties(),new MotionEvent.PointerProperties()};MotionEvent.PointerCoords[] c={new MotionEvent.PointerCoords(),new MotionEvent.PointerCoords()};for(int i=0;i<2;i++){p[i].id=i;p[i].toolType=MotionEvent.TOOL_TYPE_FINGER;c[i].pressure=1;c[i].size=1;}c[0].x=x1;c[0].y=y1;c[1].x=x2;c[1].y=y2;MotionEvent e=MotionEvent.obtain(eventTime-20,eventTime,action,2,p,c,0,0,1,1,0,0,InputDevice.SOURCE_TOUCHSCREEN,0);v.onTouchEvent(e);e.recycle(); }
    private static void draw(FlowCanvasView v) { Bitmap b=Bitmap.createBitmap(v.getWidth(),v.getHeight(),Bitmap.Config.ARGB_8888);v.draw(new Canvas(b));b.recycle(); }
    private static Bitmap export(FlowCanvasView v,RectF bounds) { Bitmap b=Bitmap.createBitmap((int)Math.ceil(bounds.width()+48),(int)Math.ceil(bounds.height()+48),Bitmap.Config.ARGB_8888);Canvas c=new Canvas(b);c.drawColor(Color.WHITE);c.translate(24-bounds.left,24-bounds.top);v.drawForExport(c);return b; }
    private void snap(String name)throws Exception { Bitmap b=Bitmap.createBitmap(view.getWidth(),view.getHeight(),Bitmap.Config.ARGB_8888);view.draw(new Canvas(b));try(FileOutputStream out=new FileOutputStream(new File(directory,name+".png"))){b.compress(Bitmap.CompressFormat.PNG,100,out);}b.recycle(); }
}
