package it.frociart.app;
import static it.frociart.app.FlowModel.*;
public final class FlowPlacementTests {
 static int checks;static void ok(boolean value,String name){if(!value)throw new AssertionError(name);checks++;}
 static Document fixture(){Document d=FlowExamples.blank();Node end=d.find("end");end.y=500;Node n=new Node(PROCESS,"x = 1",d.find("start").x-24,220);n.id="moving";d.nodes.add(n);d.edges.get(0).id="in";if(!FlowInsertion.apply(d,n.id,"in","next"))throw new AssertionError();return d;}
 public static void main(String[] args){
  for(int kind:new int[]{INPUT,PROCESS,OUTPUT}){Document d=fixture();d.find("moving").kind=kind;String before=FlowCodec.encode(d);ok("end".equals(FlowPlacement.continuation(d,"moving")),"Unique continuation");ok(FlowPlacement.detach(d,"moving"),"Detach succeeds");ok(d.edges.size()==1&&d.edges.get(0).id.equals("in")&&d.edges.get(0).to.equals("end"),"Incoming identity survives bypass");ok(d.outgoing("moving","next")==null,"Outgoing is removed");ok(FlowCodec.decode(before).nodes.size()==3,"Original persists independently");}
  Document branch=FlowExamples.parity();String original=FlowCodec.encode(branch);ok(!FlowPlacement.detach(branch,"check")&&FlowCodec.encode(branch).equals(original),"Ambiguous decision is atomic");ok(!FlowPlacement.detachVia(branch,"check","missing")&&FlowCodec.encode(branch).equals(original),"Invalid branch is atomic");ok(FlowPlacement.detachVia(branch,"check","false"),"Explicit false branch");ok(branch.outgoing("check","true")==null&&branch.outgoing("check","false")==null,"Both exits detached");ok(branch.edges.stream().anyMatch(e->e.to.equals("odd")),"Old location reaches chosen continuation");
  for(int i=0;i<50;i++){Document d=fixture();Node n=d.find("moving"),far=new Node(OUTPUT,"x",3000,1200);far.id="far";d.nodes.add(far);n.y=450+i;String edges=d.edges.toString();int size=d.edges.size();FlowPlacement.reflow(d,"moving","start","end",true);ok(d.find("end").y>=n.y+height(n.kind)+50,"Continuation has a usable gap");ok(far.x==3000&&far.y==1200,"Distant region stays fixed");ok(d.edges.size()==size&&d.edges.get(0).to.equals("moving"),"Layout changes no connections");ok(n.y==450+i,"Drop anchor remains fixed");FlowCodec.decode(FlowCodec.encode(d));checks++;}
  System.out.println("FLOW_PLACEMENT_FINISHED checks="+checks+" errors=0");
 }
}
