package it.frociart.app;

/** Camera geometry in physical pixels; independent of Android for regression checks. */
public final class FlowViewport {
    private FlowViewport() {}
    public static final float ABSOLUTE_MIN_ZOOM=0.000001f;
    public static final float NORMAL_MIN_ZOOM=0.035f;
    public static final float MAX_ZOOM=3f;

    public static final class Fit {
        public final float zoom,panX,panY,left,top,right,bottom,minimumZoom;
        private Fit(float zoom,float panX,float panY,float left,float top,float right,float bottom){
            this.zoom=zoom;this.panX=panX;this.panY=panY;
            this.left=left;this.top=top;this.right=right;this.bottom=bottom;
            minimumZoom=Math.max(ABSOLUTE_MIN_ZOOM,Math.min(NORMAL_MIN_ZOOM,zoom*.5f));
        }
    }

    /** Bounds include connector routes/labels, not just the node rectangles. */
    public static Fit fit(float width,float height,float density,boolean mobile,
                          float minX,float minY,float maxX,float maxY){
        if(!Float.isFinite(width)||!Float.isFinite(height)||!Float.isFinite(density)
                ||width<=0||height<=0||density<=0||!Float.isFinite(minX)||!Float.isFinite(minY)
                ||!Float.isFinite(maxX)||!Float.isFinite(maxY)||maxX<minX||maxY<minY)
            throw new IllegalArgumentException("Viewport e limiti devono essere finiti e ordinati.");
        float side=Math.min(36*density,width*.20f);
        // On phones, the controls occupy dedicated rows outside the canvas.
        float top=Math.min((mobile?16:30)*density,height*.25f);
        float bottom=Math.min((mobile?16:88)*density,height*.30f);
        float right=width-side,edgeBottom=height-bottom;
        float contentWidth=Math.max(1,maxX-minX),contentHeight=Math.max(1,maxY-minY);
        float zoom=Math.max(ABSOLUTE_MIN_ZOOM,Math.min(1,
                Math.min((right-side)/(contentWidth*density),(edgeBottom-top)/(contentHeight*density))));
        float scale=zoom*density;
        return new Fit(zoom,(side+right)/2-(minX+maxX)/2*scale,
                (top+edgeBottom)/2-(minY+maxY)/2*scale,side,top,right,edgeBottom);
    }

    /** Small content stays entirely visible; larger content remains pannable end to end. */
    public static float constrainPan(float pan,float start,float end,float scale,float visibleStart,float visibleEnd){
        float a=visibleStart-start*scale,b=visibleEnd-end*scale;
        return Math.max(Math.min(a,b),Math.min(Math.max(a,b),pan));
    }
}
