package it.frociart.app;

import android.app.Activity;
import android.os.Bundle;
import android.widget.FrameLayout;

/** Isolated native surface for gesture checks; never accesses the user's projects. */
public final class ConnectionChecksActivity extends Activity {
    public FrameLayout host;
    public FlowCanvasView canvas;
    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        host = new FrameLayout(this);
        canvas = new FlowCanvasView(this);
        host.addView(canvas, new FrameLayout.LayoutParams(-1, -1));
        setContentView(host);
    }
}
