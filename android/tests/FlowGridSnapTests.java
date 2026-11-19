package it.frociart.app;
public final class FlowGridSnapTests {
 private static int checks;
 private static void near(float expected,float actual){checks++;if(Math.abs(expected-actual)>.02f)throw new AssertionError(expected+" != "+actual);}
 public static void main(String[] args){
  near(0,FlowGridSnap.round(0));near(24,FlowGridSnap.round(12));near(-24,FlowGridSnap.round(-12));near(48,FlowGridSnap.round(37));near(-48,FlowGridSnap.round(-37));
  near(43,FlowGridSnap.offset(5,32,-100005,99995));
  for(int x=-100000;x<=100000;x+=37){
   float dx=FlowGridSnap.offset(x,32,-100000-x,100000-x);float target=x+dx;
   if(target< -100000||target>100000)throw new AssertionError("outside coordinate limits");
   near(FlowGridSnap.round(target),target);
   // A common offset preserves an imported group's internal non-grid spacing.
   near(303,(x+303+dx)-(x+dx));
  }
  near(-7,FlowGridSnap.offset(99991,100,-199991,9));
  near(7,FlowGridSnap.offset(-99991,-100,-9,199991));
  near(.25f,FlowGridSnap.offset(5,.25f,0,1));
  System.out.println("GRID HOST CHECKS PASSED: "+checks+" (geometry only, no Android UI execution)");
 }
}
