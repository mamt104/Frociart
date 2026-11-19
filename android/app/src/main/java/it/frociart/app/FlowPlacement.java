package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;
/** Local graph-safe placement. Distant regions and loop semantics are preserved. */
public final class FlowPlacement {
 private FlowPlacement(){}
 public static String continuation(Document d,String id){Node n=d.find(id);if(n==null||n.kind==START||n.kind==END)return null;String next=null;int count=0;
  for(Edge e:d.edges)if(e.from.equals(id)){count++;if(next!=null&&!next.equals(e.to))return null;next=e.to;if(n.kind!=DECISION&&!e.branch.equals("next"))return null;}
  if(count==0){for(Edge e:d.edges)if(e.to.equals(id))return null;return "";}
  Node target=d.find(next);if(target==null||target==n||target.kind==START||n.kind!=DECISION&&count!=1)return null;
  for(Edge e:d.edges)if(e.to.equals(id)&&e.from.equals(next))return null;return next;
 }
 public static boolean detach(Document d,String id){String next=continuation(d,id);if(next==null)return false;for(Edge e:d.edges)if(e.to.equals(id))e.to=next;d.edges.removeIf(e->e.from.equals(id));return true;}
 public static boolean disconnect(FlowModel.Document d,String id){FlowModel.Node n=d.find(id);if(n==null||n.kind==FlowModel.START||n.kind==FlowModel.END)return false;return d.edges.removeIf(e->e.from.equals(id)||e.to.equals(id));}
    static boolean detachVia(Document d,String id,String branch){Node n=d.find(id);Edge edge=d.outgoing(id,branch);if(n==null||n.kind!=DECISION||edge==null)return false;Node target=d.find(edge.to);if(target==null||target==n||target.kind==START)return false;for(Edge incoming:d.edges)if(incoming.to.equals(id)&&incoming.from.equals(target.id))return false;for(Edge incoming:d.edges)if(incoming.to.equals(id))incoming.to=target.id;d.edges.removeIf(e->e.from.equals(id));return true;}
 public static Map<String,float[]> positions(Document d){Map<String,float[]> result=new HashMap<>();for(Node n:d.nodes)result.put(n.id,new float[]{n.x,n.y});return result;}
 public static void reflow(Document d,String anchorId,String sourceId,String targetId,boolean forward){reflow(d,anchorId,sourceId,targetId,forward,false);}
 public static void grow(Document d,String anchorId){reflow(d,anchorId,null,null,false,true);}
 private static void reflow(Document d,String anchorId,String sourceId,String targetId,boolean forward,boolean resizing){
  Node a=d.find(anchorId);if(a==null||a.kind==NOTE)return;d=FlowAnnotations.executable(d);Map<String,float[]> original=positions(d);ArrayDeque<Node> active=new ArrayDeque<>();HashSet<String> queued=new HashSet<>();
  java.util.function.Consumer<Node> enqueue=n->{if(n!=null&&queued.add(n.id))active.add(n);};Node source=d.find(sourceId),target=d.find(targetId);
  if(forward&&source!=null&&source.kind!=DECISION&&source.y+height(source)+56>a.y){source.y=clamp(a.y-height(source)-56);enqueue.accept(source);}
  if(forward&&target!=null&&target!=a&&target.y<a.y+height(a)+56){target.y=clamp(a.y+height(a)+56);enqueue.accept(target);}enqueue.accept(a);
  int budget=d.nodes.size()*12+12;
  while(!active.isEmpty()&&budget-->0){Node n=active.remove();queued.remove(n.id);float l=n.x-18,t=n.y-18,r=n.x+width(n)+18,b=n.y+height(n)+18;
   for(Node other:d.nodes){if(other==n||other==a||other.x>=r||other.x+width(other)<=l||other.y>=b||other.y+height(other)<=t)continue;
    float[] old=original.get(other.id),center=original.get(n.id);boolean vertical=other==source||other==target||Math.abs(old[0]-center[0])<Math.min(width(other),width(n))*.65f;
    if(vertical){boolean below=other==target||other!=source&&(resizing?old[1]>=original.get(n.id)[1]:old[1]+height(other)/2>=a.y+height(a)/2);float desired=below?b:t-height(other);if(desired>100000)desired=t-height(other);if(desired< -100000)desired=b;other.y=clamp(desired);}
    else{boolean right=old[0]+width(other)/2>=a.x+width(a)/2;float desired=right?r:l-width(other);if(desired>100000)desired=l-width(other);if(desired< -100000)desired=r;other.x=clamp(desired);}enqueue.accept(other);
   }
   for(Edge edge:d.edges)if(edge.from.equals(n.id)){Node next=d.find(edge.to);if(next==null||next==a||next==source||original.get(next.id)[1]<=original.get(n.id)[1])continue;if(next.y<n.y+height(n)+56){next.y=clamp(n.y+height(n)+56);enqueue.accept(next);}}
  }
 }
 private static float clamp(float v){return Math.max(-100000,Math.min(100000,v));}
}
