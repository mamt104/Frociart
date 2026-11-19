package it.frociart.app;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.RectF;
import android.os.SystemClock;
import android.view.InputDevice;
import android.view.MotionEvent;
import android.view.View;
import android.view.accessibility.AccessibilityNodeInfo;

import java.io.ByteArrayInputStream;
import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.HashMap;
import javax.xml.parsers.DocumentBuilderFactory;

/** Runs on the Android UI thread. No AndroidX, mocked Canvas, or external runner. */
public final class FlowCanvasChecks {
    private static int count;
    private static long time = SystemClock.uptimeMillis() + 1000;
    private static final class Events implements FlowCanvasView.Listener {
        int before, changed, edited, added, connectRequested, selected, connectionEvents;
        String source, branch, hint;
        FlowModel.Document beforeCopy;
        FlowCanvasView view;
        @Override public void onBeforeChange() { before++; beforeCopy = view.getDocument().copy(); }
        @Override public void onDiagramChanged(String label) { changed++; }
        @Override public void onSelectionChanged() { selected++; }
        @Override public void onEditRequested(String id) { edited++; }
        @Override public void onAddRequested(float x, float y, String source, String branch) { added++; this.source=source;this.branch=branch; }
        @Override public void onConnectRequested(String id) { connectRequested++; }
        @Override public void onConnectionStateChanged(String source, String branch) { connectionEvents++; }
        @Override public void onHint(String hint) { this.hint=hint; }
        void reset() { before=changed=edited=added=connectRequested=0;beforeCopy=null; }
    }
    public static String run(Context context) throws Exception {
        count=0;
        final float density = context.getResources().getDisplayMetrics().density;
        FlowCanvasView view = new FlowCanvasView(context);
        Events events = new Events(); events.view=view;view.setListener(events);
        int width=(int)(360*density), height=(int)(560*density);
        view.measure(View.MeasureSpec.makeMeasureSpec(width, View.MeasureSpec.EXACTLY), View.MeasureSpec.makeMeasureSpec(height, View.MeasureSpec.EXACTLY));
        view.layout(0,0,width,height);
        FlowModel.Document doc = document();
        view.setDocument(doc, true);
        FlowModel.Node a=doc.nodes.get(0), b=doc.nodes.get(1), c=doc.nodes.get(2);
        draw(view);
        check(view.getDocument()==doc,"model reference retained");
        check(view.getDiagramBounds().contains(a.x,a.y),"bounds include first node");

        tap(view,a);
        check(view.getSelectedIds().contains(a.id),"tap selects node");
        check(events.before==0,"selection creates no history");
        tap(view,a);
        check(events.edited==1,"double tap edits once");
        check(view.getSelectedIds().size()==1,"double tap keeps selection");
        time+=1000;events.reset();

        float[] plus=screen(view,a.x+FlowModel.width(a.kind)/2,a.y+FlowModel.height(a.kind));
        single(view,MotionEvent.ACTION_DOWN,plus[0]+21*density,plus[1]+23*density);
        single(view,MotionEvent.ACTION_UP,plus[0]+21*density,plus[1]+23*density);
        check(events.connectRequested==1,"plus responds beyond icon inside 48dp touch target");
        check(events.before==0,"connection picker request does not mutate model");
        events.reset();time+=1000;

        float[] p=screen(view,a.x+100,a.y+44);
        float ox=a.x,oy=a.y,scale=field(view,"zoom")*density;
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        single(view,MotionEvent.ACTION_MOVE,p[0]+40*density,p[1]+24*density);
        float movedX=a.x,movedY=a.y;
        single(view,MotionEvent.ACTION_UP,p[0]+110*density,p[1]+90*density);
        near(a.x,ox+40*density/scale,"drag follows MOVE x");
        near(a.y,oy+24*density/scale,"drag follows MOVE y");
        near(a.x,movedX,"release does not extend x");
        near(a.y,movedY,"release does not extend y");
        check(events.before==1&&events.changed==1,"single drag is one undo transaction");
        near(events.beforeCopy.find(a.id).x,ox,"history captured before mutation");

        events.reset();time+=1000;
        p=screen(view,a.x+100,a.y+44);ox=a.x;oy=a.y;
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        single(view,MotionEvent.ACTION_UP,p[0]+150*density,p[1]+60*density);
        near(a.x,ox,"UP without MOVE never drags");
        check(events.edited==0&&events.added==0&&events.changed==0,"distant UP is not a false tap");

        view.clearSelection();view.setSelectionMode(true);time+=1000;
        tap(view,a);tap(view,b);
        check(view.getSelectedIds().size()==2,"selection mode accumulates nodes");
        view.setSelectionMode(false);events.reset();
        float bx=b.x,by=b.y;ox=a.x;oy=a.y;p=screen(view,a.x+100,a.y+44);scale=field(view,"zoom")*density;
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        single(view,MotionEvent.ACTION_MOVE,p[0]+32*density,p[1]-18*density);
        single(view,MotionEvent.ACTION_UP,p[0]+150*density,p[1]+100*density);
        near(a.x-ox,b.x-bx,"group preserves relative x");
        near(a.y-oy,b.y-by,"group preserves relative y");
        near(a.x-ox,32*density/scale,"group uses MOVE delta");
        check(events.before==1&&events.changed==1,"group drag is one undo transaction");
        check(view.getSelectedIds().size()==2,"group remains selected");

        doc=document();view.setDocument(doc,true);a=doc.nodes.get(0);b=doc.nodes.get(1);c=doc.nodes.get(2);
        view.fitToContent();draw(view);events.reset();
        float panX=field(view,"panX"),panY=field(view,"panY");
        single(view,MotionEvent.ACTION_DOWN,8*density,12*density);
        single(view,MotionEvent.ACTION_MOVE,36*density,34*density);
        float finalPanX=field(view,"panX"),finalPanY=field(view,"panY");
        single(view,MotionEvent.ACTION_UP,150*density,180*density);
        near(finalPanX-panX,28*density,"blank drag pans horizontally");
        near(finalPanY-panY,22*density,"blank drag pans vertically");
        near(field(view,"panX"),finalPanX,"pan ignores later UP coordinate");
        near(field(view,"panY"),finalPanY,"pan release does not jump");
        check(events.before==0&&events.added==0,"pan neither changes document nor creates blocks");

        view.fitToContent();events.reset();p=screen(view,a.x+100,a.y+44);ox=a.x;oy=a.y;
        float oldZoom=field(view,"zoom"),cx=p[0]+25*density,cy=p[1];
        float pivotWorldX=(cx-field(view,"panX"))/(oldZoom*density);
        float pivotWorldY=(cy-field(view,"panY"))/(oldZoom*density);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        multi(view,MotionEvent.ACTION_POINTER_DOWN|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),p[0],p[1],p[0]+50*density,p[1]);
        multi(view,MotionEvent.ACTION_MOVE,cx-45*density,cy+10*density,cx+45*density,cy+10*density);
        near(field(view,"zoom"),oldZoom*1.8f,"pinch distance controls zoom");
        float[] transformed=screen(view,pivotWorldX,pivotWorldY);
        near(transformed[0],cx,"pinch preserves focal x");near(transformed[1],cy+10*density,"pinch preserves focal y with pan");
        multi(view,MotionEvent.ACTION_POINTER_UP|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),cx-45*density,cy+10*density,cx+45*density,cy+10*density);
        single(view,MotionEvent.ACTION_MOVE,cx-10*density,cy+80*density);
        single(view,MotionEvent.ACTION_UP,cx-10*density,cy+80*density);
        near(a.x,ox,"pinch does not drag touched node");near(a.y,oy,"remaining finger does not drag node");
        check(events.before==0&&events.changed==0&&events.added==0,"pinch has no document side effects");

        view.fitToContent();events.reset();p=screen(view,a.x+100,a.y+44);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        single(view,MotionEvent.ACTION_MOVE,p[0]+22*density,p[1]);
        movedX=a.x;
        multi(view,MotionEvent.ACTION_POINTER_DOWN|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),p[0]+22*density,p[1],p[0]+72*density,p[1]);
        multi(view,MotionEvent.ACTION_MOVE,p[0]+12*density,p[1],p[0]+92*density,p[1]);
        multi(view,MotionEvent.ACTION_POINTER_UP|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),p[0]+12*density,p[1],p[0]+92*density,p[1]);
        single(view,MotionEvent.ACTION_UP,p[0]+12*density,p[1]);
        near(a.x,movedX,"second finger stops single drag at last MOVE");
        check(events.before==1&&events.changed==1,"pinch interruption commits existing drag once");

        doc=document();view.setDocument(doc,true);view.fitToContent();a=doc.nodes.get(0);b=doc.nodes.get(1);c=doc.nodes.get(2);
        view.clearSelection();view.setSelectionMode(true);events.reset();
        float[] first=screen(view,a.x-12,a.y-12),last=screen(view,a.x+FlowModel.width(a.kind)+12,a.y+FlowModel.height(a.kind)+12);
        single(view,MotionEvent.ACTION_DOWN,first[0],first[1]);
        single(view,MotionEvent.ACTION_MOVE,last[0],last[1]);
        float[] farther=screen(view,c.x+240,c.y+120);
        single(view,MotionEvent.ACTION_UP,farther[0],farther[1]);
        check(view.getSelectedIds().size()==1&&view.getSelectedIds().contains(a.id),"marquee selects MOVE rectangle, not release rectangle");
        check(events.before==0,"marquee does not enter history");
        view.setSelectionMode(false);

        events.reset();view.beginConnection(a.id,"next");tap(view,b);
        check(doc.edges.size()==1&&doc.edges.get(0).from.equals(a.id)&&doc.edges.get(0).to.equals(b.id),"tap creates valid connection");
        check(events.before==1&&events.changed==1,"connection is one undo transaction");
        check(!view.isConnecting(),"connection mode ends after valid target");
        events.reset();view.beginConnection(a.id,"next");tap(view,c);
        check(doc.edges.size()==1&&doc.edges.get(0).to.equals(c.id),"same output replaces previous connection");
        check(events.before==1&&events.changed==1,"replacement is atomic");
        events.reset();view.beginConnection(a.id,"next");tap(view,c);
        check(events.before==0&&events.changed==0,"existing target causes no spurious history");
        FlowModel.Node start=new FlowModel.Node(FlowModel.START,"Inizio",320,0);doc.nodes.add(start);view.notifyModelChanged();view.focusNode(start.id);
        events.reset();view.beginConnection(a.id,"next");tap(view,start);
        check(view.isConnecting()&&events.before==0,"connection into Inizio is rejected atomically");
        events.reset();single(view,MotionEvent.ACTION_DOWN,10*density,15*density);single(view,MotionEvent.ACTION_UP,10*density,15*density);
        check(events.added==1&&a.id.equals(events.source)&&"next".equals(events.branch),"empty connection tap delegates position and source");
        check(events.before==0&&events.changed==0,"picker request has no premature model mutation");
        events.reset();single(view,MotionEvent.ACTION_DOWN,10*density,15*density);single(view,MotionEvent.ACTION_MOVE,10*density,90*density);single(view,MotionEvent.ACTION_UP,10*density,90*density);
        check(events.added==0&&events.before==0,"connection-mode pan never opens picker");view.cancelConnection();

        doc=document();a=doc.nodes.get(0);b=doc.nodes.get(1);c=doc.nodes.get(2);
        doc.edges.add(new FlowModel.Edge(a.id,b.id,"next"));doc.edges.add(new FlowModel.Edge(b.id,c.id,"next"));
        a.trueLabel="Ok personalizzato";a.trueColor="#AABBCC";
        view.setDocument(doc,true);view.setSelectionMode(true);tap(view,a);tap(view,b);view.setSelectionMode(false);events.reset();
        view.duplicateSelection();
        check(doc.nodes.size()==5&&doc.edges.size()==3,"duplicate copies internal edge only");
        check(events.before==1&&events.changed==1,"duplicate group is atomic");
        check(view.getSelectedIds().size()==2&&!view.getSelectedIds().contains(a.id),"duplicates receive new selected ids");
        check(doc.nodes.get(3).trueLabel.equals("Ok personalizzato")&&doc.nodes.get(3).trueColor.equals("#AABBCC"),"duplicate preserves custom branch appearance");
        check(doc.edges.get(2).from.equals(doc.nodes.get(3).id)&&doc.edges.get(2).to.equals(doc.nodes.get(4).id),"duplicate internal edge references copied nodes");
        events.reset();view.deleteSelection();
        check(doc.nodes.size()==3&&doc.edges.size()==2,"delete removes copied nodes and incident edges");
        check(events.before==1&&events.changed==1,"delete selection is atomic");
        check(view.getSelectedIds().isEmpty(),"delete clears selection");
        view.select(b.id);events.reset();view.deleteSelection();
        check(doc.nodes.size()==2&&doc.edges.isEmpty(),"delete removes incoming and outgoing incident edges");

        doc=new FlowModel.Document();
        FlowModel.Node decision=new FlowModel.Node(FlowModel.DECISION,"x < 10 && x > 0",0,0);
        decision.trueLabel="Sì & <ok>";decision.falseLabel="No";decision.trueColor="#123ABC";
        FlowModel.Node end=new FlowModel.Node(FlowModel.END,"Fine",310,240);
        doc.title="Test <diagramma> & \"emoji 🧠\"";doc.nodes.add(decision);doc.nodes.add(end);
        doc.edges.add(new FlowModel.Edge(decision.id,end.id,"true"));
        doc.edges.add(new FlowModel.Edge(decision.id,decision.id,"false"));
        view.setDocument(doc,true);String svg=view.createSvg();
        check(svg.contains("#123ABC"),"SVG contains custom branch color");
        check(svg.contains("Sì &amp; &lt;ok&gt;"),"SVG escapes custom label");
        check(svg.contains("&lt;diagramma&gt;")&&svg.contains("🧠"),"SVG preserves Unicode and escapes title");
        DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(new ByteArrayInputStream(svg.getBytes("UTF-8")));
        check(true,"SVG parses as well-formed XML");
        view.select(decision.id);view.setDark(true);view.setHighlight(decision.id);
        check(svg.equals(view.createSvg()),"SVG independent of dark mode, selection and run highlight");
        RectF exportedBounds=view.getDiagramBounds();
        Bitmap darkExport=export(view,exportedBounds);view.setDark(false);view.clearSelection();view.setHighlight(null);
        Bitmap lightExport=export(view,exportedBounds);
        check(darkExport.sameAs(lightExport),"bitmap export byte-identical across UI themes and state");
        darkExport.recycle();lightExport.recycle();
        draw(view);
        HashMap<?,?> index=objectField(view,"nodeIndex");Object retained=index.get(decision.id);
        Object shape=objectField(retained,"shape");
        view.zoomBy(1.2f);draw(view);view.zoomBy(1/1.2f);draw(view);
        check(objectField(objectFieldMap(view,"nodeIndex").get(decision.id),"shape")==shape,"zoom reuses retained node path");
        float oldRight=view.getDiagramBounds().right;end.x+=200;view.notifyModelChanged();
        check(view.getDiagramBounds().right>oldRight+100,"model mutation refreshes bounds and routes");
        decision.trueColor="bad-css;url(x)";view.notifyModelChanged();
        check(view.createSvg().contains("#39B985")&&!view.createSvg().contains("url(x)"),"invalid branch color uses safe fallback");
        view.beginConnection(decision.id,"false");view.select(decision.id);doc.nodes.remove(decision);view.notifyModelChanged();
        check(!view.isConnecting()&&view.getSelectedIds().isEmpty(),"removed source cancels connection and stale selection");

        doc=document();view.setDocument(doc,true);a=doc.nodes.get(0);events.reset();
        AccessibilityNodeInfo info=view.getAccessibilityNodeProvider().createAccessibilityNodeInfo(1);
        check(info!=null&&info.isClickable()&&info.isFocusable(),"canvas exposes accessible node actions");
        check(info.getContentDescription().toString().contains(a.text),"accessible node contains its text");
        view.getAccessibilityNodeProvider().performAction(1,AccessibilityNodeInfo.ACTION_CLICK,null);
        check(view.getSelectedIds().contains(a.id),"accessibility action selects actual node");
        check(info.getActionList().size()>=5,"accessible node has edit and connect actions");
        info.recycle();
        draw(view);

        doc=new FlowModel.Document();for(int i=0;i<FlowModel.MAX_NODES;i++)doc.nodes.add(new FlowModel.Node(FlowModel.PROCESS,"x = "+i,(i%20)*260,(i/20)*150));
        view.setDocument(doc,true);view.select(doc.nodes.get(0).id);events.reset();view.duplicateSelection();
        check(doc.nodes.size()==FlowModel.MAX_NODES&&events.before==0,"node limit rejects duplicate without partial change");
        check(field(view,"zoom")>=0.78f,"large diagram opens at readable zoom");
        view.fitToContent();check(field(view,"zoom")<0.78f,"explicit fit can show entire large diagram");
        draw(view);
        inputRegressionChecks(view,events,density);
        retainedRouteChecks(view,events,density);
        readOnlyChecks(view,events,density);
        return "CANVAS CHECKS PASSED: "+count+"; density="+density+"; Android "+android.os.Build.VERSION.SDK_INT;
    }

    private static void inputRegressionChecks(FlowCanvasView view,Events events,float density)throws Exception {
        FlowModel.Document doc=document();view.setDocument(doc,true);view.fitToContent();events.reset();time+=1000;
        float panX=field(view,"panX"),panY=field(view,"panY"),pending=field(view,"touchSlop")/4;
        single(view,MotionEvent.ACTION_DOWN,8*density,12*density);
        single(view,MotionEvent.ACTION_MOVE,8*density+pending,12*density+pending);
        near(field(view,"panX"),panX,"small pending MOVE does not prematurely pan");
        single(view,MotionEvent.ACTION_MOVE,36*density,34*density);
        near(field(view,"panX")-panX,28*density,"pan includes motion before slop threshold");
        near(field(view,"panY")-panY,22*density,"pan retains full initial vertical displacement");
        single(view,MotionEvent.ACTION_UP,90*density,100*density);
        near(field(view,"panX")-panX,28*density,"pan still ignores release coordinates after pending moves");
        check(events.before==0&&events.changed==0,"pending pan never creates model history");

        view.fitToContent();view.setSelectionMode(true);FlowModel.Node a=doc.nodes.get(0);
        float[] first=screen(view,a.x-12,a.y-12),last=screen(view,a.x+FlowModel.width(a.kind)+12,a.y+FlowModel.height(a.kind)+12);
        single(view,MotionEvent.ACTION_DOWN,first[0],first[1]);
        single(view,MotionEvent.ACTION_MOVE,last[0],last[1]);
        draw(view);check(!view.isDirty(),"a painted canvas does not request another idle frame");
        single(view,MotionEvent.ACTION_CANCEL,last[0]+100,last[1]+100);
        check(view.isDirty(),"cancelled selection rectangle requests its removal on screen");
        check(field(view,"gesture")==0&&view.getSelectedIds().contains(a.id),"CANCEL ends marquee while preserving selected blocks");
        view.setSelectionMode(false);events.reset();time+=1000;

        float[] p=screen(view,a.x+100,a.y+44);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        single(view,MotionEvent.ACTION_MOVE,p[0]+30*density,p[1]+12*density);
        float lastX=a.x,lastY=a.y;
        single(view,MotionEvent.ACTION_CANCEL,p[0]+120*density,p[1]+100*density);
        near(a.x,lastX,"CANCEL does not extend an active drag horizontally");
        near(a.y,lastY,"CANCEL does not extend an active drag vertically");
        check(events.before==1&&events.changed==1,"cancelled drag commits last MOVE exactly once");

        events.reset();time+=1000;tap(view,a);p=screen(view,a.x+100,a.y+44);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);single(view,MotionEvent.ACTION_CANCEL,p[0],p[1]);
        tap(view,a);
        check(events.edited==0,"CANCEL breaks double-tap recognition");
    }

    private static void retainedRouteChecks(FlowCanvasView view,Events events,float density)throws Exception {
        FlowModel.Document doc=new FlowModel.Document();
        FlowModel.Node source=new FlowModel.Node(FlowModel.DECISION,"totale > 3",0,0);
        source.trueLabel="Avanti quando il risultato della condizione è positivo";
        FlowModel.Node target=new FlowModel.Node(FlowModel.END,"Fine",310,240);
        FlowModel.Node unrelated=new FlowModel.Node(FlowModel.PROCESS,"x = 4",700,400);
        FlowModel.Edge edge=new FlowModel.Edge(source.id,target.id,"true");
        doc.nodes.add(source);doc.nodes.add(target);doc.nodes.add(unrelated);doc.edges.add(edge);
        view.setDocument(doc,true);draw(view);String initialSvg=view.createSvg();
        Object visual=objectFieldMap(view,"edgeIndex").get(edge.id);
        String label=objectField(visual,"label");check(label.endsWith("…"),"long branch label fixture is displayed with ellipsis");
        unrelated.x+=12;view.notifyModelChanged();draw(view);
        check(objectField(visual,"label")==label,"unrelated movement reuses the measured long branch label");
        unrelated.x-=12;view.notifyModelChanged();draw(view);view.focusNode(source.id);events.reset();time+=1000;
        float[] retained=objectField(visual,"points"),p=screen(view,source.x+FlowModel.width(source.kind)/2,source.y+FlowModel.height(source.kind)/2);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        single(view,MotionEvent.ACTION_MOVE,p[0]+20*density,p[1]+14*density);draw(view);
        float[] moved=objectField(visual,"points");
        check(moved==retained,"connected drag reuses route coordinates with unchanged bends");
        near(moved[0],source.x,"reused route follows the moved decision outlet x");
        near(moved[1],source.y+FlowModel.height(source.kind)/2,"reused route follows the moved decision outlet y");
        check(objectField(visual,"label")==label,"rerouting keeps unchanged text layout cached");
        single(view,MotionEvent.ACTION_UP,p[0]+100*density,p[1]+100*density);
        source.x=0;source.y=0;view.notifyModelChanged();
        check(initialSvg.equals(view.createSvg()),"moving back restores the exact original exported geometry");
        target.x=-420;view.notifyModelChanged();String changedSvg=view.createSvg();
        float[] changed=objectField(visual,"points");
        check(changed.length==6,"route adjusts storage when changing to a shorter orthogonal path");
        near(changed[changed.length-2],target.x+FlowModel.width(target.kind)/2,"changed route reaches target center");
        near(changed[changed.length-1],target.y-2,"changed route reaches target top");
        DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(new ByteArrayInputStream(changedSvg.getBytes("UTF-8")));
        check(true,"rerouted SVG remains well-formed");
    }

    private static void readOnlyChecks(FlowCanvasView view,Events events,float density)throws Exception {
        FlowModel.Document doc=document();view.setDocument(doc,true);view.fitToContent();
        FlowModel.Node a=doc.nodes.get(0),b=doc.nodes.get(1);view.select(a.id);events.reset();
        view.beginConnection(a.id,"next");view.setReadOnly(true);draw(view);
        check(view.isReadOnly()&&!view.isConnecting(),"read-only mode cancels the pending connection");
        check(events.before==0&&events.changed==0,"entering read-only mode creates no undo entry");
        check(((RectF)objectField(view,"plusHit")).isEmpty(),"read-only canvas hides its connection affordance");
        time+=1000;tap(view,b);tap(view,b);
        check(view.getSelectedIds().size()==1&&view.getSelectedIds().contains(a.id),"read-only taps preserve selection");
        check(events.edited==0&&events.connectRequested==0,"read-only double tap cannot edit or connect");
        time+=1000;
        single(view,MotionEvent.ACTION_DOWN,8*density,12*density);single(view,MotionEvent.ACTION_UP,8*density,12*density);
        single(view,MotionEvent.ACTION_DOWN,8*density,12*density);single(view,MotionEvent.ACTION_UP,8*density,12*density);
        check(events.added==0,"read-only blank double tap cannot add a block");

        float originalX=a.x,originalY=a.y,panX=field(view,"panX"),panY=field(view,"panY");
        float[] p=screen(view,a.x+100,a.y+44);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        ((Runnable)objectField(view,"longPress")).run();
        check(view.getSelectedIds().contains(a.id)&&field(view,"gesture")==1,"read-only long press cannot start selection");
        single(view,MotionEvent.ACTION_MOVE,p[0]+34*density,p[1]+18*density);
        single(view,MotionEvent.ACTION_UP,p[0]+100*density,p[1]+100*density);
        near(field(view,"panX")-panX,34*density,"read-only node drag pans the sheet horizontally");
        near(field(view,"panY")-panY,18*density,"read-only node drag pans the sheet vertically");
        near(a.x,originalX,"read-only pan cannot move a node x");near(a.y,originalY,"read-only pan cannot move a node y");

        float oldZoom=field(view,"zoom");p=screen(view,a.x+100,a.y+44);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);
        multi(view,MotionEvent.ACTION_POINTER_DOWN|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),p[0],p[1],p[0]+50*density,p[1]);
        multi(view,MotionEvent.ACTION_MOVE,p[0]-15*density,p[1],p[0]+65*density,p[1]);
        multi(view,MotionEvent.ACTION_POINTER_UP|(1<<MotionEvent.ACTION_POINTER_INDEX_SHIFT),p[0]-15*density,p[1],p[0]+65*density,p[1]);
        single(view,MotionEvent.ACTION_UP,p[0]-15*density,p[1]);
        near(field(view,"zoom"),oldZoom*1.6f,"read-only pinch remains available");
        check(events.before==0&&events.changed==0,"read-only navigation leaves document history unchanged");

        view.setSelectionMode(true);check(!view.isSelectionMode(),"read-only mode rejects starting selection mode");
        view.beginConnection(a.id,"next");view.duplicateSelection();view.deleteSelection();
        check(!view.isConnecting()&&doc.nodes.size()==3&&doc.edges.isEmpty(),"read-only canvas rejects structural editing methods");
        AccessibilityNodeInfo info=view.getAccessibilityNodeProvider().createAccessibilityNodeInfo(1);
        check(!info.isClickable()&&!info.isLongClickable(),"read-only accessibility exposes no editable node tap");
        int edit=(Integer)objectField(view,"ACCESS_EDIT"),connect=(Integer)objectField(view,"ACCESS_CONNECT");
        check(!view.getAccessibilityNodeProvider().performAction(1,AccessibilityNodeInfo.ACTION_CLICK,null)&&
                !view.getAccessibilityNodeProvider().performAction(1,AccessibilityNodeInfo.ACTION_LONG_CLICK,null)&&
                !view.getAccessibilityNodeProvider().performAction(1,edit,null)&&
                !view.getAccessibilityNodeProvider().performAction(1,connect,null),"read-only accessibility rejects selection, edit and connection actions");
        info.recycle();
        check(view.getAccessibilityNodeProvider().performAction(View.NO_ID,AccessibilityNodeInfo.ACTION_SCROLL_BACKWARD,null),"read-only accessibility retains viewport navigation");

        view.setReadOnly(false);events.reset();time+=1000;p=screen(view,a.x+100,a.y+44);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);view.setReadOnly(true);
        single(view,MotionEvent.ACTION_MOVE,p[0]+50*density,p[1]+30*density);single(view,MotionEvent.ACTION_UP,p[0]+50*density,p[1]+30*density);
        check(events.before==0&&events.changed==0&&a.x==originalX&&a.y==originalY,"entering read-only mode cancels a pending touch without history");

        view.setReadOnly(false);events.reset();time+=1000;p=screen(view,a.x+100,a.y+44);
        single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);single(view,MotionEvent.ACTION_MOVE,p[0]+30*density,p[1]);
        float movedX=a.x;view.setReadOnly(true);
        single(view,MotionEvent.ACTION_MOVE,p[0]+90*density,p[1]);single(view,MotionEvent.ACTION_UP,p[0]+90*density,p[1]);
        near(a.x,movedX,"switching to read-only stops an existing drag at its last MOVE");
        check(events.before==1&&events.changed==1,"read-only transition closes the existing drag without extra undo");
        view.setReadOnly(false);events.reset();time+=1000;tap(view,b);tap(view,b);
        check(view.getSelectedIds().contains(b.id)&&events.edited==1,"closing read-only mode restores normal editing gestures");
    }
    private static FlowModel.Document document(){FlowModel.Document doc=new FlowModel.Document();doc.nodes.add(new FlowModel.Node(FlowModel.PROCESS,"x = 2",0,0));doc.nodes.add(new FlowModel.Node(FlowModel.PROCESS,"y = x + 1",0,158));doc.nodes.add(new FlowModel.Node(FlowModel.END,"Fine",24,318));return doc;}
    private static void check(boolean condition,String message){if(!condition)throw new AssertionError(message);count++;}
    private static void near(float a,float b,String message){check(Math.abs(a-b)<0.10f,message+" ("+a+" / "+b+")");}
    private static float field(Object object,String name)throws Exception{return ((Number)objectField(object,name)).floatValue();}
    @SuppressWarnings("unchecked") private static <T>T objectField(Object object,String name)throws Exception{Field field=object.getClass().getDeclaredField(name);field.setAccessible(true);return(T)field.get(object);}
    private static HashMap<?,?> objectFieldMap(Object object,String name)throws Exception{return objectField(object,name);}
    private static float[] screen(FlowCanvasView view,float x,float y)throws Exception{float scale=field(view,"zoom")*view.getResources().getDisplayMetrics().density;return new float[]{x*scale+field(view,"panX"),y*scale+field(view,"panY")};}
    private static void tap(FlowCanvasView view,FlowModel.Node node)throws Exception{float[] p=screen(view,node.x+FlowModel.width(node.kind)/2,node.y+FlowModel.height(node.kind)/2);single(view,MotionEvent.ACTION_DOWN,p[0],p[1]);single(view,MotionEvent.ACTION_UP,p[0],p[1]);}
    private static void single(FlowCanvasView view,int action,float x,float y){time+=20;MotionEvent event=MotionEvent.obtain(time-20,time,action,x,y,0);event.setSource(InputDevice.SOURCE_TOUCHSCREEN);view.onTouchEvent(event);event.recycle();}
    private static void multi(FlowCanvasView view,int action,float x1,float y1,float x2,float y2){time+=20;MotionEvent.PointerProperties[] properties={new MotionEvent.PointerProperties(),new MotionEvent.PointerProperties()};MotionEvent.PointerCoords[] coords={new MotionEvent.PointerCoords(),new MotionEvent.PointerCoords()};for(int i=0;i<2;i++){properties[i].id=i;properties[i].toolType=MotionEvent.TOOL_TYPE_FINGER;coords[i].pressure=1;coords[i].size=1;}coords[0].x=x1;coords[0].y=y1;coords[1].x=x2;coords[1].y=y2;MotionEvent event=MotionEvent.obtain(time-20,time,action,2,properties,coords,0,0,1,1,0,0,InputDevice.SOURCE_TOUCHSCREEN,0);view.onTouchEvent(event);event.recycle();}
    private static void draw(FlowCanvasView view){Bitmap bitmap=Bitmap.createBitmap(view.getWidth(),view.getHeight(),Bitmap.Config.ARGB_8888);view.draw(new Canvas(bitmap));bitmap.recycle();}
    private static Bitmap export(FlowCanvasView view,RectF bounds){Bitmap bitmap=Bitmap.createBitmap((int)Math.ceil(bounds.width()+48),(int)Math.ceil(bounds.height()+48),Bitmap.Config.ARGB_8888);Canvas canvas=new Canvas(bitmap);canvas.drawColor(Color.WHITE);canvas.translate(24-bounds.left,24-bounds.top);view.drawForExport(canvas);return bitmap;}
}
