package it.frociart.app;
import java.util.*;import static it.frociart.app.FlowModel.*;
/** A disposable navigation projection, never an executable or saved replacement. */
final class FlowCycleView {
 static final class Projection {Document graph=new Document();Map<String,FlowCycle.Info> cards=new LinkedHashMap<>();Map<String,String> owner=new HashMap<>();}
 private final Document source;final List<FlowCycle.Info> cycles;final Set<String> folded=new HashSet<>();
 FlowCycleView(Document graph){source=FlowCodec.decode(FlowCodec.encode(graph));cycles=FlowCycle.find(source);Set<String> unsafe=new HashSet<>();for(FlowCycle.Info a:cycles)for(FlowCycle.Info b:cycles)if(a!=b&&!Collections.disjoint(a.members,b.members)&&!properSubset(a.members,b.members)&&!properSubset(b.members,a.members)){unsafe.add(a.check);unsafe.add(b.check);}cycles.removeIf(c->unsafe.contains(c.check));all(true);}
 static boolean properSubset(Set<String> a,Set<String> b){return a.size()<b.size()&&b.containsAll(a);}
 boolean isFolded(String check){return folded.contains(check);}
 void all(boolean fold){folded.clear();if(fold)for(FlowCycle.Info c:cycles)folded.add(c.check);}
 void set(String check,boolean fold){FlowCycle.Info c=null;for(FlowCycle.Info i:cycles)if(i.check.equals(check))c=i;if(c==null)return;for(FlowCycle.Info parent:cycles)if(properSubset(c.members,parent.members))folded.remove(parent.check);if(fold)folded.add(check);else folded.remove(check);}
 Projection build(){Projection p=new Projection();p.graph.title=source.title;for(FlowCycle.Info c:cycles){if(!folded.contains(c.check))continue;boolean hidden=false;for(FlowCycle.Info parent:cycles)if(folded.contains(parent.check)&&properSubset(c.members,parent.members))hidden=true;if(hidden)continue;p.cards.put(c.entry,c);for(String id:c.members)p.owner.put(id,c.entry);}
  for(Node original:source.nodes){String owner=p.owner.get(original.id);if(owner==null)p.graph.nodes.add(original.copy());else if(original.id.equals(owner)){FlowCycle.Info c=p.cards.get(owner);int stops=0;for(Node n:source.nodes)if(c.members.contains(n.id)&&n.breakpoint)stops++;Node n=new Node(PROCESS,c.header+"\n"+c.members.size()+" blocchi"+(stops>0?" · "+stops+" punti di interruzione":""),original.x,original.y);n.id=owner;n.breakpoint=stops>0;p.graph.nodes.add(n);}}
  for(Edge e:source.edges){String a=p.owner.get(e.from),b=p.owner.get(e.to);if(a!=null&&a.equals(b))continue;Edge n=new Edge(a==null?e.from:a,b==null?e.to:b,a==null?e.branch:"next");n.id=e.id;p.graph.edges.add(n);}FlowAutoLayout.apply(p.graph);return p;
 }
 static String caption(FlowCycle.Info c){return(c.kind==0?"CICLO PER":c.kind==1?"CICLO MENTRE":"CICLO RIPETI")+" · ESPANDI";}
}
