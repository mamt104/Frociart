package it.frociart.app;

import android.app.Activity;
import android.os.Bundle;
import android.widget.FrameLayout;

/** Native, attached surface for MotionProbe; never packaged in the user app. */
public final class MotionChecksActivity extends Activity {
    public FrameLayout host;
    public FlowCanvasView canvas;
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        host = new FrameLayout(this);
        canvas = new FlowCanvasView(this);
        host.addView(canvas, new FrameLayout.LayoutParams(-1, -1));
        setContentView(host);
    }
}
