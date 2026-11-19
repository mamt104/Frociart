package it.frociart.app;

/** World-coordinate grid. Group offsets preserve the exact internal arrangement. */
public final class FlowGridSnap {
    public static final float STEP=24;
    private FlowGridSnap(){}
    public static float round(float value){return Math.copySign((float)Math.floor(Math.abs(value)/STEP+0.5f)*STEP,value);}
    public static float offset(float origin,float delta,float min,float max){
        float requested=Math.max(min,Math.min(max,delta));
        float low=(float)Math.ceil((origin+min)/STEP),high=(float)Math.floor((origin+max)/STEP);
        if(low>high)return requested;
        float index=Math.max(low,Math.min(high,round(origin+requested)/STEP));
        return index*STEP-origin;
    }
}
