package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;
/** A selected subgraph keeps its internal branches and relative layout. */
public final class FlowGroupPlacement {
 private FlowGroupPlacement(){}
 static final class Slot {String from,branch;Slot(String f,String b){from=f;branch=b;}}
 static final class Part {List<Node> nodes=new ArrayList<>();Node entry;List<Slot> slots=new ArrayList<>();}
 static final class Plan {Set<String> ids;List<Part> parts=new ArrayList<>();String error;int addedEdges;}
 static boolean eligible(Document d,Collection<String> ids){Set<String> set=new HashSet<>(ids);if(set.size()<2)return false;for(String id:set){Node n=d.find(id);if(n==null||n.kind==START||n.kind==END)return false;}return true;}
 static boolean detach(Document d,Collection<String> ids){Set<String> set=new HashSet<>(ids);if(!eligible(d,set))return false;Map<String,List<Edge>> outgoing=new HashMap<>();for(Edge e:d.edges)outgoing.computeIfAbsent(e.from,k->new ArrayList<>()).add(e);
  for(Edge edge:new ArrayList<>(d.edges))if(!set.contains(edge.from)&&set.contains(edge.to)){Set<String> seen=new HashSet<>(),exits=new HashSet<>();ArrayDeque<String> queue=new ArrayDeque<>();queue.add(edge.to);while(!queue.isEmpty()){String id=queue.remove();if(!seen.add(id))continue;for(Edge e:outgoing.getOrDefault(id,Collections.emptyList()))if(set.contains(e.to))queue.add(e.to);else exits.add(e.to);}
   String next=exits.size()==1?exits.iterator().next():null;Node target=d.find(next);if(next!=null&&!next.equals(edge.from)&&target!=null&&target.kind!=START)edge.to=next;else d.edges.remove(edge);
  }d.edges.removeIf(e->set.contains(e.from)&&!set.contains(e.to));return true;
 }
 private static Set<String> reach(String start,Map<String,List<String>> graph){Set<String> seen=new HashSet<>();ArrayDeque<String> todo=new ArrayDeque<>();todo.push(start);while(!todo.isEmpty()){String id=todo.pop();if(!seen.add(id))continue;for(String next:graph.get(id))todo.push(next);}return seen;}
 private static final Comparator<Node> ORDER=Comparator.comparingDouble((Node n)->n.y).thenComparingDouble(n->n.x).thenComparing(n->n.id);
 static Plan analyze(Document d,Collection<String> ids){Plan p=new Plan();p.ids=new HashSet<>(ids);if(!eligible(d,p.ids)){p.error="Escludi Inizio e Fine per staccare e inserire un gruppo.";return p;}
  Map<String,Node> nodes=new HashMap<>();for(Node n:d.nodes)if(p.ids.contains(n.id)&&n.kind!=NOTE)nodes.put(n.id,n);if(nodes.isEmpty()){p.error="Le note restano libere sul foglio.";return p;}
  Map<String,List<String>> forward=new HashMap<>(),reverse=new HashMap<>();for(String id:nodes.keySet()){forward.put(id,new ArrayList<>());reverse.put(id,new ArrayList<>());}List<Edge> internal=new ArrayList<>();for(Edge e:d.edges)if(nodes.containsKey(e.from)&&nodes.containsKey(e.to)){internal.add(e);forward.get(e.from).add(e.to);reverse.get(e.to).add(e.from);}
  Set<String> unseen=new HashSet<>(nodes.keySet());while(!unseen.isEmpty()){Set<String> component=new HashSet<>();ArrayDeque<String> todo=new ArrayDeque<>();todo.push(unseen.iterator().next());while(!todo.isEmpty()){String id=todo.pop();if(!component.add(id))continue;unseen.remove(id);for(String next:forward.get(id))todo.push(next);for(String next:reverse.get(id))todo.push(next);}
   Part part=new Part();for(String id:component)part.nodes.add(nodes.get(id));part.nodes.sort(ORDER);Set<String> visited=new HashSet<>();String candidate=null;for(Node n:part.nodes)if(!visited.contains(n.id)){candidate=n.id;visited.addAll(reach(candidate,forward));}
   if(reach(candidate,forward).size()!=component.size()){p.error="Questo gruppo ha più ingressi indipendenti: includi anche il blocco che li unisce, oppure inseriscili separatamente.";return p;}Set<String> roots=reach(candidate,reverse);for(Node n:part.nodes)if(roots.contains(n.id)){part.entry=n;break;}
   for(Node n:part.nodes){Set<String> branches=new LinkedHashSet<>(n.kind==DECISION?Arrays.asList("true","false"):Collections.singletonList("next"));Set<String> used=new HashSet<>();for(Edge e:internal)if(e.from.equals(n.id)&&(!branches.contains(e.branch)||!used.add(e.branch))){p.error="Correggi le uscite duplicate del gruppo prima di inserirlo.";return p;}for(String branch:branches)if(!used.contains(branch))part.slots.add(new Slot(n.id,branch));}
   if(part.slots.isEmpty()){p.error="Il gruppo è un ciclo chiuso: lascia libera un’uscita per reinserirlo nella freccia.";return p;}p.addedEdges+=part.slots.size();p.parts.add(part);
  }
  p.parts.sort(Comparator.comparingDouble((Part a)->a.nodes.stream().mapToDouble(n->n.y).min().getAsDouble()).thenComparingDouble(a->a.nodes.stream().mapToDouble(n->n.x).min().getAsDouble()).thenComparing(a->a.entry.id));return p;
 }
 static String reason(Document d,Plan p,Edge edge,int reserved){if(p.error!=null)return p.error;if(edge==null||p.ids.contains(edge.from)||p.ids.contains(edge.to)||edge.from.equals(edge.to))return "Scegli una freccia esterna al gruppo.";Node from=d.find(edge.from),to=d.find(edge.to);if(from==null||to==null||from.kind==END||to.kind==START||from.kind==NOTE||to.kind==NOTE)return "La freccia non ha estremi validi.";return d.edges.size()+reserved+p.addedEdges>MAX_EDGES?"Limite di collegamenti raggiunto: nessun inserimento applicato.":null;}
 static boolean insert(Document d,Collection<String> ids,String edgeId,int reserved){Plan p=analyze(d,ids);Edge edge=null;for(Edge e:d.edges)if(e.id.equals(edgeId))edge=e;if(reason(d,p,edge,reserved)!=null)return false;for(Edge e:d.edges)if(p.ids.contains(e.from)!=p.ids.contains(e.to))return false;String target=edge.to;edge.to=p.parts.get(0).entry.id;for(int i=0;i<p.parts.size();i++)for(Slot slot:p.parts.get(i).slots)d.edges.add(new Edge(slot.from,i+1<p.parts.size()?p.parts.get(i+1).entry.id:target,slot.branch));return true;}
 static boolean arrange(Document d,Collection<String> ids,String sourceId,String targetId){Plan p=analyze(d,ids);if(p.error!=null)return false;Map<String,Float> originalY=new HashMap<>();for(Node n:d.nodes)originalY.put(n.id,n.y);float bottom=Float.NEGATIVE_INFINITY;
  for(Part part:p.parts){float top=Float.POSITIVE_INFINITY;for(Node n:part.nodes)top=Math.min(top,n.y);float dy=bottom==Float.NEGATIVE_INFINITY?0:Math.max(0,bottom+56-top);bottom=Float.NEGATIVE_INFINITY;for(Node n:part.nodes){n.y+=dy;bottom=Math.max(bottom,n.y+height(n));}}
  Node source=d.find(sourceId),target=d.find(targetId),first=p.parts.get(0).entry;boolean forward=source!=null&&target!=null&&target.y>source.y;if(forward&&source.kind!=DECISION){float dy=Math.max(0,source.y+height(source)+56-first.y);for(Node n:d.nodes)if(p.ids.contains(n.id))n.y+=dy;}
  bottom=Float.NEGATIVE_INFINITY;for(Node n:d.nodes)if(p.ids.contains(n.id)&&n.kind!=NOTE)bottom=Math.max(bottom,n.y+height(n));ArrayDeque<String> queue=new ArrayDeque<>();Set<String> moved=new HashSet<>();if(forward)queue.add(targetId);
  while(!queue.isEmpty()){String id=queue.remove();if(p.ids.contains(id)||id.equals(sourceId)||!moved.add(id))continue;Node n=d.find(id);if(n==null)continue;float floor=id.equals(targetId)?bottom+56:Float.NEGATIVE_INFINITY;for(Edge e:d.edges)if(e.to.equals(id)&&!p.ids.contains(e.from)){Node a=d.find(e.from);if(a!=null&&originalY.get(a.id)<originalY.get(n.id))floor=Math.max(floor,a.y+height(a)+56);}if(n.y<floor)n.y=floor;for(Edge e:d.edges)if(e.from.equals(id)){Node next=d.find(e.to);if(next!=null&&originalY.get(next.id)>=originalY.get(n.id))queue.add(next.id);}}
  for(Node n:d.nodes)if(n.x< -99900||n.x>99900||n.y< -99900||n.y>99900)return false;return true;
 }
}
