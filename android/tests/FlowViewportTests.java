package it.frociart.app;

import java.util.Random;

/** Physical containment and camera invariants, independent of Android rendering. */
public final class FlowViewportTests {
    private static int checks;
    private static void check(boolean ok,String why){checks++;if(!ok)throw new AssertionError(why);}
    private static void contains(FlowViewport.Fit fit,float density,float x1,float y1,float x2,float y2){
        float scale=fit.zoom*density,eps=.15f;
        check(x1*scale+fit.panX>=fit.left-eps,"left edge visible");
        check(y1*scale+fit.panY>=fit.top-eps,"top edge visible");
        check(x2*scale+fit.panX<=fit.right+eps,"right edge visible");
        check(y2*scale+fit.panY<=fit.bottom+eps,"bottom edge visible");
        check(Float.isFinite(fit.panX)&&Float.isFinite(fit.panY)&&Float.isFinite(fit.zoom),"finite transform");
        check(fit.minimumZoom<=fit.zoom&&fit.minimumZoom>0,"zoom buttons/pinch can reach the overview");
        float px=FlowViewport.constrainPan(fit.panX,x1,x2,scale,fit.left,fit.right);
        float py=FlowViewport.constrainPan(fit.panY,y1,y2,scale,fit.top,fit.bottom);
        check(Math.abs(px-fit.panX)<eps&&Math.abs(py-fit.panY)<eps,"constraints retain centered overview");
    }
    public static void main(String[] args){
        float[][] screens={{320,100},{320,400},{360,560},{390,640},{640,220},{840,600}};
        float[][] bounds={{-140,-90,140,90},{0,0,212,88},{-600,-200,1200,1900},
                {0,0,232,75000},{-100000,-100000,100232,100136},{-90000,99900,100212,100100}};
        for(float density:new float[]{.75f,1,1.5f,2,3,4})for(float[] screen:screens)
            for(float[] b:bounds)for(boolean mobile:new boolean[]{false,true}){
                FlowViewport.Fit fit=FlowViewport.fit(screen[0]*density,screen[1]*density,density,mobile,b[0],b[1],b[2],b[3]);
                contains(fit,density,b[0],b[1],b[2],b[3]);
            }
        Random random=new Random(20261018);
        for(int i=0;i<4000;i++){
            float density=.75f+random.nextFloat()*3.25f;
            float x1=-100000+random.nextFloat()*190000,y1=-100000+random.nextFloat()*190000;
            float x2=x1+random.nextFloat()*(100232-x1),y2=y1+random.nextFloat()*(100136-y1);
            FlowViewport.Fit fit=FlowViewport.fit((280+random.nextFloat()*700)*density,(80+random.nextFloat()*850)*density,density,true,x1,y1,x2,y2);
            contains(fit,density,x1,y1,x2,y2);
            float scale=fit.minimumZoom*density;
            float px=FlowViewport.constrainPan(100000,x1,x2,scale,fit.left,fit.right);
            float py=FlowViewport.constrainPan(-100000,y1,y2,scale,fit.top,fit.bottom);
            check(x1*scale+px>=fit.left-.2f&&x2*scale+px<=fit.right+.2f,"zoom out keeps both horizontal ends in view");
            check(y1*scale+py>=fit.top-.2f&&y2*scale+py<=fit.bottom+.2f,"zoom out keeps both vertical ends in view");
        }
        FlowViewport.Fit phone=FlowViewport.fit(360,560,1,true,0,0,232,75000);
        check(phone.zoom<.035f&&phone.minimumZoom<phone.zoom,"tall diagrams fit below the former desktop/mobile floors");
        check(FlowViewport.constrainPan(-2000,0,2000,1,16,544)==-1456,"large diagram bottom reachable");
        check(FlowViewport.constrainPan(2000,0,2000,1,16,544)==16,"large diagram top reachable");
        boolean rejected=false;try{FlowViewport.fit(Float.NaN,100,1,true,0,0,10,10);}catch(IllegalArgumentException e){rejected=true;}
        check(rejected,"invalid camera input rejected");
        System.out.println("VIEWPORT HOST CHECKS PASSED: "+checks+"; geometry only, no Android UI execution");
    }
}
