package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Disposable canvas geometry. The source graph remains the only editable/executable document. */
final class FlowCycleFold {
 static final class Projection {
  final Document scene=new Document();
  final Map<String,FlowCycle.Info> cards=new LinkedHashMap<>();
  final Map<String,String> owners=new HashMap<>();
  final Map<String,float[]> offsets=new HashMap<>();
  final Map<String,Node> sourceNodes=new LinkedHashMap<>(),visualNodes=new LinkedHashMap<>();
  final Map<String,Edge> sourceEdges=new HashMap<>();
  Projection(Document source){scene.title=source.title;for(Node n:source.nodes)sourceNodes.put(n.id,n);for(Edge e:source.edges)sourceEdges.put(e.id,e);}
  String owner(String id){String value=owners.get(id);return value==null?id:value;}
  Node visual(String id){return visualNodes.get(owner(id));}
  Set<String> selection(Collection<String> ids){Set<String> result=new LinkedHashSet<>();for(String id:ids){FlowCycle.Info c=cards.get(owner(id));if(c!=null)result.addAll(c.members);else result.add(id);}return result;}
  boolean containsActive(String card,String active){return Objects.equals(owner(active),card);}
  void sync(){for(Node n:scene.nodes){Node source=sourceNodes.get(n.id);float[] offset=offsets.get(n.id);n.x=source.x+offset[0];n.y=source.y+offset[1];if(!cards.containsKey(n.id)){n.text=source.text;n.prompt=source.prompt;n.comment=source.comment;n.previewText=source.previewText;n.previewComment=source.previewComment;n.trueLabel=source.trueLabel;n.falseLabel=source.falseLabel;n.trueColor=source.trueColor;n.falseColor=source.falseColor;n.breakpoint=source.breakpoint;n.breakpointCondition=source.breakpointCondition;}}}
 }
 private final Document source;
 private String signature;
 List<FlowCycle.Info> cycles;
 Projection current;
 int rebuilds;
 private final FlowModel.Sizing sizing;
 FlowCycleFold(Document document){this(document,FlowModel.sizing());}
 FlowCycleFold(Document document,FlowModel.Sizing sizing){source=document;this.sizing=sizing;update(true);}
 private float width(Node n){return FlowModel.width(n,sizing);}
 private float height(Node n){return FlowModel.height(n,sizing);}
 private String signature(){StringBuilder s=new StringBuilder();for(Node n:source.nodes)s.append(n.id).append(':').append(n.kind).append(':').append(n.text==null?-1:n.text.length()).append(':').append(n.text).append(n.foldedCycle?'1':'0').append(n.breakpoint?'1':'0').append(';');for(Edge e:source.edges)s.append(e.id).append(':').append(e.from).append(':').append(e.to).append(':').append(e.branch).append(';');return s.toString();}
 void update(){update(false);}
 void update(boolean force){String next=signature();if(!force&&next.equals(signature)){if(current!=null)current.sync();return;}signature=next;rebuilds++;
  cycles=candidates(source);Set<String> unsafe=new HashSet<>();for(FlowCycle.Info a:cycles)for(FlowCycle.Info b:cycles)if(a!=b&&!Collections.disjoint(a.members,b.members)&&!proper(a.members,b.members)&&!proper(b.members,a.members)){unsafe.add(a.check);unsafe.add(b.check);}cycles.removeIf(c->unsafe.contains(c.check)||c.members.size()<2);
  List<FlowCycle.Info> folded=new ArrayList<>(),visible=new ArrayList<>();for(FlowCycle.Info c:cycles)if(source.find(c.check).foldedCycle)folded.add(c);for(FlowCycle.Info c:folded){boolean hidden=false;for(FlowCycle.Info parent:folded)if(proper(c.members,parent.members))hidden=true;if(!hidden)visible.add(c);}
  if(visible.isEmpty()){current=null;return;}Projection p=new Projection(source);for(FlowCycle.Info c:visible){p.cards.put(c.entry,c);for(String id:c.members)p.owners.put(id,c.entry);}
  for(Node n:source.nodes){String owner=p.owners.get(n.id);if(owner!=null&&!owner.equals(n.id))continue;FlowCycle.Info card=p.cards.get(n.id);Node copy;
   if(card!=null){int stops=0;for(String id:card.members)if(p.sourceNodes.get(id).breakpoint)stops++;String header=card.header==null?"":card.header;if(header.length()>240)header=header.substring(0,239)+"…";copy=new Node(PROCESS,header+"\n"+card.members.size()+" blocchi · espandi"+(card.exit==null?"\nUscita da collegare":"")+(stops==0?"":stops==1?"\n1 punto di interruzione":"\n"+stops+" punti di interruzione"),n.x,n.y);copy.id=n.id;copy.breakpoint=stops>0;}else copy=n.copy();p.scene.nodes.add(copy);p.visualNodes.put(copy.id,copy);p.offsets.put(copy.id,new float[]{0,0});
  }
  for(Edge e:source.edges){String from=p.owner(e.from),to=p.owner(e.to);if(from.equals(to)&&p.cards.containsKey(from))continue;Edge edge=new Edge(from,to,p.owners.containsKey(e.from)?"next":e.branch);edge.id=e.id;p.scene.edges.add(edge);}
  Set<String> back=new HashSet<>();for(FlowCycle.Info c:cycles)for(Edge e:source.edges)if(c.kind==2?e.from.equals(c.check)&&e.to.equals(c.bodyEntry):e.to.equals(c.check)&&c.members.contains(e.from)&&(!e.from.equals(c.entry)||e.from.equals(e.to)))back.add(e.id);
  Map<String,List<float[]>> ranges=new HashMap<>();for(FlowCycle.Info c:visible){Node entry=p.sourceNodes.get(c.entry),capsule=p.visualNodes.get(c.entry);float bottom=Float.NEGATIVE_INFINITY;for(String id:c.members){Node n=p.sourceNodes.get(id);bottom=Math.max(bottom,n.y+height(n));}float start=entry.y+height(capsule);p.offsets.put(c.entry,new float[]{(width(entry)-width(capsule))/2,0});
   Set<String> downstream=new HashSet<>();ArrayDeque<String> queue=new ArrayDeque<>();if(c.exit!=null)queue.add(c.exit);while(!queue.isEmpty()){String id=queue.remove();if(c.members.contains(id)||!downstream.add(id))continue;for(Edge e:source.edges)if(e.from.equals(id)&&!back.contains(e.id))queue.add(e.to);}
   Set<String> visualIds=new HashSet<>();for(String id:downstream)visualIds.add(p.owner(id));for(String id:visualIds){Node original=p.sourceNodes.get(id);if(bottom<=start||id.equals(c.entry)||original==null||!p.offsets.containsKey(id)||original.y<bottom-.5f)continue;List<float[]> list=ranges.get(id);if(list==null){list=new ArrayList<>();ranges.put(id,list);}list.add(new float[]{start,bottom});}
  }
  for(Map.Entry<String,List<float[]>> item:ranges.entrySet()){item.getValue().sort((a,b)->Float.compare(a[0],b[0]));float sum=0,left=0,right=0;boolean first=true;for(float[] interval:item.getValue()){if(first){left=interval[0];right=interval[1];first=false;}else if(interval[0]<=right)right=Math.max(right,interval[1]);else{sum+=right-left;left=interval[0];right=interval[1];}}sum+=right-left;float[] offset=p.offsets.get(item.getKey());offset[1]=-sum;}
  p.sync();List<Node> ordered=new ArrayList<>(p.scene.nodes);ordered.sort((a,b)->Float.compare(p.sourceNodes.get(a.id).y,p.sourceNodes.get(b.id).y));for(Node from:ordered)for(Edge edge:p.scene.edges)if(edge.from.equals(from.id)&&!back.contains(edge.id)){Node to=p.visualNodes.get(edge.to),originalTo=p.sourceNodes.get(to.id),originalFrom=p.sourceNodes.get(from.id);if(originalTo.y<=originalFrom.y)continue;FlowCycle.Info card=p.cards.get(from.id);float bottom=originalFrom.y+height(originalFrom);if(card!=null){bottom=Float.NEGATIVE_INFINITY;for(String id:card.members){Node n=p.sourceNodes.get(id);bottom=Math.max(bottom,n.y+height(n));}}float minimum=from.y+height(from)+Math.max(16,originalTo.y-bottom);if(to.y<minimum){to.y=minimum;p.offsets.get(to.id)[1]=minimum-originalTo.y;}}
  current=p;
 }
 static List<FlowCycle.Info> candidates(Document graph){Document probe=new Document();probe.nodes.addAll(graph.nodes);probe.edges.addAll(graph.edges);Set<String> added=new HashSet<>();for(Node n:graph.nodes)if(n.kind==DECISION){List<Edge> edges=new ArrayList<>();for(Edge e:graph.edges)if(e.from.equals(n.id))edges.add(e);if(edges.size()!=1)continue;String branch=edges.get(0).branch.equals("true")?"false":edges.get(0).branch.equals("false")?"true":null;if(branch==null)continue;Node end=new Node(END,"",0,0);added.add(end.id);probe.nodes.add(end);probe.edges.add(new Edge(n.id,end.id,branch));}List<FlowCycle.Info> result=FlowCycle.find(probe);for(FlowCycle.Info c:result)if(added.contains(c.exit))c.exit=null;result.removeIf(c->!Collections.disjoint(c.members,added));return result;}
 static boolean proper(Set<String> a,Set<String> b){return a.size()<b.size()&&b.containsAll(a);}
 boolean set(String check,boolean folded){for(FlowCycle.Info c:cycles)if(c.check.equals(check)){Node n=source.find(check);if(n.foldedCycle==folded)return false;n.foldedCycle=folded;update();return true;}return false;}
 boolean all(boolean folded){boolean changed=false;for(FlowCycle.Info c:cycles){Node n=source.find(c.check);changed|=n.foldedCycle!=folded;n.foldedCycle=folded;}if(changed)update();return changed;}
 boolean reveal(String id){boolean changed=false;for(FlowCycle.Info c:cycles)if(c.members.contains(id)){Node n=source.find(c.check);changed|=n.foldedCycle;n.foldedCycle=false;}if(changed)update();return changed;}
}
