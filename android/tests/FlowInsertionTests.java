package it.frociart.app;
import static it.frociart.app.FlowModel.*;
public final class FlowInsertionTests{
 static int checks;static void check(boolean ok,String message){checks++;if(!ok)throw new AssertionError(message);}
 static Document fixture(int kind){Document d=new Document();for(String id:new String[]{"s","b","e","m"}){Node n=new Node(id.equals("s")?START:id.equals("e")?END:id.equals("m")?kind:OUTPUT,id,0,0);n.id=id;d.nodes.add(n);}Edge edge=new Edge("s","b","next");edge.id="drop";d.edges.add(edge);d.edges.add(new Edge("b","e","next"));return d;}
 public static void main(String[] args){
  for(int kind:new int[]{INPUT,PROCESS,OUTPUT})for(int position=0;position<50;position++){
   Document d=fixture(kind);Node n=d.find("m");n.x=position*37-900;n.y=position*13;float x=n.x,y=n.y;Edge original=d.edges.get(0);
   check(FlowInsertion.apply(d,"m","drop","next"),"insert detached");check(original==d.edges.get(0)&&original.id.equals("drop")&&original.from.equals("s")&&original.to.equals("m"),"edge identity");check(d.outgoing("m","next").to.equals("b"),"actual continuation");check(n.x==x&&n.y==y&&d.edges.size()==3,"no coordinate rewrite");
  }
  Document connected=fixture(PROCESS);connected.edges.get(1).from="m";connected.edges.add(new Edge("b","m","next"));Edge old=connected.outgoing("m","next");
  check(FlowInsertion.apply(connected,"m","drop","next"),"connected insertion");check(connected.outgoing("b","next").to.equals("e")&&old.to.equals("b")&&connected.edges.size()==3,"bypass old position and reuse outgoing edge");
  for(int kind:new int[]{START,END}){Document d=fixture(kind);String before=FlowCodec.encode(d);check(!FlowInsertion.apply(d,"m","drop","next")&&FlowCodec.encode(d).equals(before),"cannot cut with endpoint");}
  for(String branch:new String[]{"true","false"}){Document d=fixture(DECISION);String before=FlowCodec.encode(d);check(!FlowInsertion.apply(d,"m","drop",null)&&FlowCodec.encode(d).equals(before),"choice required");check(FlowInsertion.apply(d,"m","drop",branch)&&d.outgoing("m",branch).to.equals("b")&&d.outgoing("m",branch.equals("true")?"false":"true")==null,"explicit decision branch");}
  Document partial=fixture(INPUT);partial.edges.add(new Edge("b","m","next"));String before=FlowCodec.encode(partial);check(!FlowInsertion.apply(partial,"m","drop","next")&&before.equals(FlowCodec.encode(partial)),"ambiguous detach rejected atomically");
  Document cycle=fixture(INPUT);cycle.edges.add(new Edge("b","m","next"));cycle.edges.add(new Edge("m","b","next"));before=FlowCodec.encode(cycle);check(!FlowInsertion.apply(cycle,"m","drop","next")&&before.equals(FlowCodec.encode(cycle)),"bypass cannot create self loop");
  Document cap=fixture(INPUT);while(cap.edges.size()<MAX_EDGES)cap.edges.add(new Edge("b","e","next"));before=FlowCodec.encode(cap);check(!FlowInsertion.apply(cap,"m","drop","next")&&before.equals(FlowCodec.encode(cap)),"capacity failure is atomic");
  Document branch=fixture(OUTPUT);branch.find("s").kind=DECISION;branch.find("s").trueLabel="Accetta";branch.find("s").trueColor="#365CD9";branch.edges.get(0).branch="true";check(FlowInsertion.apply(branch,"m","drop","next")&&branch.edges.get(0).branch.equals("true")&&branch.find("s").trueLabel.equals("Accetta")&&branch.find("s").trueColor.equals("#365CD9"),"branch metadata preserved");
  Document none=fixture(DECISION);check(FlowInsertion.apply(none,"m","drop","none"),"None is an explicit decision insertion");check(none.edges.get(0).to.equals("m")&&none.outgoing("m","true")==null&&none.outgoing("m","false")==null&&none.find("b")!=null&&none.edges.size()==2,"None leaves both exits free and retains old target");
  Document fullDecision=fixture(DECISION);while(fullDecision.edges.size()<MAX_EDGES)fullDecision.edges.add(new Edge("b","e","next"));before=FlowCodec.encode(fullDecision);check(!FlowInsertion.apply(fullDecision,"m","drop","true")&&FlowCodec.encode(fullDecision).equals(before),"Full edge capacity rejects a new continuation atomically");check(FlowInsertion.apply(fullDecision,"m","drop","none")&&fullDecision.edges.size()==MAX_EDGES,"None needs no additional edge capacity");
  System.out.println("INSERTION HOST CHECKS PASSED: "+checks+"; graph logic only, native gestures checked separately.");
 }
}
