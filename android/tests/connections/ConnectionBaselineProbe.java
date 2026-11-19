package it.frociart.app;

import android.app.Activity;
import android.app.Instrumentation;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.RectF;
import android.os.Bundle;
import android.os.SystemClock;
import android.util.Log;
import android.view.MotionEvent;
import android.view.View;
import java.io.File;
import java.io.FileOutputStream;
import java.lang.reflect.Field;

/** Compiled against the unmodified, delivered 1.2.0 source, never the fixed source. */
public final class ConnectionBaselineProbe extends Instrumentation {
    private final StringBuilder report = new StringBuilder();
    private ConnectionChecksActivity activity;
    private int reproduced;
    private static long time = SystemClock.uptimeMillis() + 1000;
    private static final class Events implements FlowCanvasView.Listener {
        int before, changed, added, connect;
        public void onBeforeChange() { before++; }
        public void onDiagramChanged(String label) { changed++; }
        public void onSelectionChanged() { }
        public void onEditRequested(String id) { }
        public void onAddRequested(float x, float y, String source, String branch) { added++; }
        public void onConnectRequested(String id) { connect++; }
        public void onConnectionStateChanged(String source, String branch) { }
        public void onHint(String text) { }
    }
    @Override public void onCreate(Bundle arguments) { super.onCreate(arguments); start(); }
    @Override public void onStart() {
        boolean success = false;
        try {
            activity = (ConnectionChecksActivity)startActivitySync(new Intent().setClassName(getTargetContext().getPackageName(),
                    ConnectionChecksActivity.class.getName()).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            final Throwable[] failure = {null};
            runOnMainSync(() -> { try { reproduce(); } catch (Throwable error) { failure[0] = error; } });
            if (failure[0] != null) throw failure[0];
            success = true;
        } catch (Throwable error) { report.append(Log.getStackTraceString(error)); }
        finally {
            if (activity != null) runOnMainSync(() -> activity.finish());
            report.append(success ? "BASELINE DEFECTS REPRODUCED: " : "BASELINE REPRODUCTION FAILED AFTER: ").append(reproduced).append('\n');
            try {
                File directory = new File(getTargetContext().getFilesDir(), "connections-baseline"); directory.mkdirs();
                try (FileOutputStream out = new FileOutputStream(new File(directory, "report.txt"))) { out.write(report.toString().getBytes("UTF-8")); }
            } catch (Exception error) { report.append(Log.getStackTraceString(error)); }
            Bundle result = new Bundle(); result.putString("stream", report.toString());
            finish(success ? Activity.RESULT_OK : Activity.RESULT_CANCELED, result);
        }
    }
    private void reproduce() throws Exception {
        FlowCanvasView view = new FlowCanvasView(activity);
        float density = view.getResources().getDisplayMetrics().density;
        view.measure(View.MeasureSpec.makeMeasureSpec((int)(360*density), View.MeasureSpec.EXACTLY),
                View.MeasureSpec.makeMeasureSpec((int)(640*density), View.MeasureSpec.EXACTLY));
        view.layout(0, 0, (int)(360*density), (int)(640*density));
        FlowModel.Document document = new FlowModel.Document();
        FlowModel.Node start = new FlowModel.Node(FlowModel.START, "Inizio", 0, 0);
        FlowModel.Node first = new FlowModel.Node(FlowModel.PROCESS, "x = 1", -24, 190);
        FlowModel.Node end = new FlowModel.Node(FlowModel.END, "Fine", 0, 410);
        document.nodes.add(start); document.nodes.add(first); document.nodes.add(end);
        view.setDocument(document, true); view.select(start.id); draw(view);
        if (((RectF)field(view, "plusHit")).isEmpty()) throw new AssertionError("Baseline precondition: Start has + in move mode");
        view.setSelectionMode(true); draw(view);
        if (!((RectF)field(view, "plusHit")).isEmpty()) throw new AssertionError("Start + disappearance was not reproduced");
        reproduced++; report.append("REPRODUCED: Start selected in Seleziona loses both visible + and touch geometry.\n");
        view.setSelectionMode(false); view.select(start.id); draw(view);
        Events events = new Events(); view.setListener(events);
        RectF plus = new RectF((RectF)field(view, "plusHit"));
        single(view, MotionEvent.ACTION_DOWN, plus.centerX(), plus.centerY()); single(view, MotionEvent.ACTION_UP, plus.centerX(), plus.centerY());
        if (events.connect != 1 || events.added != 0) throw new AssertionError("Baseline tap + no longer enters old connect command");
        reproduced++; report.append("REPRODUCED: Tap + requests Collega; it does not immediately request a new block.\n");
        document.edges.add(new FlowModel.Edge(start.id, first.id, "next")); view.notifyModelChanged(); view.select(start.id); draw(view);
        plus = new RectF((RectF)field(view, "plusHit"));
        float[] target = screen(view, end.x + FlowModel.width(end.kind)/2, end.y + FlowModel.height(end.kind)/2);
        single(view, MotionEvent.ACTION_DOWN, plus.centerX(), plus.centerY()); single(view, MotionEvent.ACTION_MOVE, target[0], target[1]);
        single(view, MotionEvent.ACTION_UP, target[0], target[1]);
        if (document.edges.size() != 1 || !end.id.equals(document.edges.get(0).to) || events.before != 1 || events.changed != 1)
            throw new AssertionError("Baseline unconditional replacement was not reproduced");
        reproduced++; report.append("REPRODUCED: Drag + to a different target silently replaces an occupied output in Canvas.\n");
    }
    private static void draw(FlowCanvasView view) {
        Bitmap bitmap = Bitmap.createBitmap(view.getWidth(), view.getHeight(), Bitmap.Config.ARGB_8888);
        view.draw(new Canvas(bitmap)); bitmap.recycle();
    }
    private static Object field(Object value, String name) throws Exception { Field f=value.getClass().getDeclaredField(name); f.setAccessible(true); return f.get(value); }
    private static float[] screen(FlowCanvasView view, float x, float y) throws Exception {
        float scale=((Number)field(view,"zoom")).floatValue()*view.getResources().getDisplayMetrics().density;
        return new float[]{((Number)field(view,"panX")).floatValue()+x*scale, ((Number)field(view,"panY")).floatValue()+y*scale};
    }
    private static void single(FlowCanvasView view, int action, float x, float y) {
        time += 32; MotionEvent event=MotionEvent.obtain(time-32,time,action,x,y,0); view.onTouchEvent(event); event.recycle();
    }
}
