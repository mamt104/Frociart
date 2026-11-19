using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
namespace Frociart {
 // A group is a subgraph, never a flattened list of its blocks.
 internal static class FlowGroupPlacement {
  internal sealed class Part {internal List<FlowNode> Nodes;internal FlowNode Entry;internal List<FlowEdge> Slots=new List<FlowEdge>();}
  internal sealed class Plan {internal HashSet<string> Ids;internal List<Part> Parts=new List<Part>();internal string Error;internal int AddedEdges;}
  internal static bool Eligible(Diagram d,IEnumerable<string> ids){var set=new HashSet<string>(ids);return set.Count>1&&set.All(id=>d.Nodes.Any(n=>n.Id==id&&n.Kind!=NodeKind.Start&&n.Kind!=NodeKind.End));}
  internal static bool Detach(Diagram d,IEnumerable<string> ids){
   var set=new HashSet<string>(ids);if(!Eligible(d,set))return false;
   var outgoing=d.Edges.GroupBy(e=>e.From).ToDictionary(g=>g.Key,g=>g.ToArray());
   foreach(var edge in d.Edges.Where(e=>!set.Contains(e.From)&&set.Contains(e.To)).ToArray()){
    var visited=new HashSet<string>();var queue=new Queue<string>();var exits=new HashSet<string>();queue.Enqueue(edge.To);
    while(queue.Count>0){string id=queue.Dequeue();if(!visited.Add(id))continue;FlowEdge[] next;if(!outgoing.TryGetValue(id,out next))continue;foreach(var e in next)if(set.Contains(e.To))queue.Enqueue(e.To);else exits.Add(e.To);}
    if(exits.Count==1&&exits.First()!=edge.From&&d.Nodes.Any(n=>n.Id==exits.First()&&n.Kind!=NodeKind.Start))edge.To=exits.First();else d.Edges.Remove(edge);
   }
   d.Edges.RemoveAll(e=>set.Contains(e.From)&&!set.Contains(e.To));return true;
  }
  private static HashSet<string> Reach(string start,Dictionary<string,List<string>> graph){var seen=new HashSet<string>();var todo=new Stack<string>();todo.Push(start);while(todo.Count>0){var id=todo.Pop();if(!seen.Add(id))continue;foreach(var next in graph[id])todo.Push(next);}return seen;}
  internal static Plan Analyze(Diagram d,IEnumerable<string> ids){
   var p=new Plan{Ids=new HashSet<string>(ids)};if(!Eligible(d,p.Ids)){p.Error="Escludi Inizio e Fine per staccare e inserire un gruppo.";return p;}
   var nodes=d.Nodes.Where(n=>p.Ids.Contains(n.Id)&&n.Kind!=NodeKind.Note).ToDictionary(n=>n.Id);
   if(nodes.Count==0){p.Error="Le note restano libere sul foglio.";return p;}
   var forward=nodes.Keys.ToDictionary(id=>id,id=>new List<string>());var reverse=nodes.Keys.ToDictionary(id=>id,id=>new List<string>());
   var internalEdges=d.Edges.Where(e=>nodes.ContainsKey(e.From)&&nodes.ContainsKey(e.To)).ToArray();
   foreach(var e in internalEdges){forward[e.From].Add(e.To);reverse[e.To].Add(e.From);}
   var unseen=new HashSet<string>(nodes.Keys);
   while(unseen.Count>0){var component=new HashSet<string>();var todo=new Stack<string>();todo.Push(unseen.First());while(todo.Count>0){var id=todo.Pop();if(!component.Add(id))continue;unseen.Remove(id);foreach(var next in forward[id].Concat(reverse[id]))todo.Push(next);}
    var part=new Part{Nodes=component.Select(id=>nodes[id]).OrderBy(n=>n.Y).ThenBy(n=>n.X).ThenBy(n=>n.Id,StringComparer.Ordinal).ToList()};
    var visited=new HashSet<string>();string candidate=null;foreach(var n in part.Nodes)if(!visited.Contains(n.Id)){candidate=n.Id;visited.UnionWith(Reach(candidate,forward));}
    if(Reach(candidate,forward).Count!=component.Count){p.Error="Questo gruppo ha più ingressi indipendenti: includi anche il blocco che li unisce, oppure inseriscili separatamente.";return p;}
    var roots=Reach(candidate,reverse);part.Entry=part.Nodes.First(n=>roots.Contains(n.Id));
    foreach(var n in part.Nodes){var edges=internalEdges.Where(e=>e.From==n.Id).ToArray();string[] branches=n.Kind==NodeKind.Decision?new[]{"true","false"}:new[]{"next"};
     if(edges.Any(e=>!branches.Contains(e.Branch))||edges.GroupBy(e=>e.Branch).Any(g=>g.Count()>1)){p.Error="Correggi le uscite duplicate del gruppo prima di inserirlo.";return p;}
     foreach(var branch in branches)if(!edges.Any(e=>e.Branch==branch))part.Slots.Add(new FlowEdge{From=n.Id,Branch=branch});
    }
    if(part.Slots.Count==0){p.Error="Il gruppo è un ciclo chiuso: lascia libera un’uscita per reinserirlo nella freccia.";return p;}
    p.AddedEdges+=part.Slots.Count;p.Parts.Add(part);
   }
   p.Parts=p.Parts.OrderBy(a=>a.Nodes.Min(n=>n.Y)).ThenBy(a=>a.Nodes.Min(n=>n.X)).ThenBy(a=>a.Entry.Id,StringComparer.Ordinal).ToList();return p;
  }
  internal static string Reason(Diagram d,Plan p,FlowEdge edge,int reserved){if(p.Error!=null)return p.Error;if(edge==null||p.Ids.Contains(edge.From)||p.Ids.Contains(edge.To)||edge.From==edge.To)return "Scegli una freccia esterna al gruppo.";var from=d.Nodes.FirstOrDefault(n=>n.Id==edge.From);var to=d.Nodes.FirstOrDefault(n=>n.Id==edge.To);if(from==null||to==null||from.Kind==NodeKind.End||to.Kind==NodeKind.Start||from.Kind==NodeKind.Note||to.Kind==NodeKind.Note)return "La freccia non ha estremi validi.";return d.Edges.Count+reserved+p.AddedEdges>DiagramIO.MaxEdges?"Limite di collegamenti raggiunto: nessun inserimento applicato.":null;}
  internal static bool Insert(Diagram d,IEnumerable<string> ids,string edgeId,int reserved){var p=Analyze(d,ids);var edge=d.Edges.FirstOrDefault(e=>e.Id==edgeId);if(Reason(d,p,edge,reserved)!=null||d.Edges.Any(e=>p.Ids.Contains(e.From)!=p.Ids.Contains(e.To)))return false;string target=edge.To;edge.To=p.Parts[0].Entry.Id;for(int i=0;i<p.Parts.Count;i++)foreach(var slot in p.Parts[i].Slots)d.Edges.Add(new FlowEdge{From=slot.From,To=i+1<p.Parts.Count?p.Parts[i+1].Entry.Id:target,Branch=slot.Branch});return true;}
  internal static bool Arrange(Diagram d,IEnumerable<string> ids,string sourceId,string targetId){
   var p=Analyze(d,ids);if(p.Error!=null)return false;var originalY=d.Nodes.ToDictionary(n=>n.Id,n=>n.Y);float bottom=float.NegativeInfinity;
   foreach(var part in p.Parts){float top=part.Nodes.Min(n=>n.Y);float dy=float.IsNegativeInfinity(bottom)?0:Math.Max(0,bottom+56-top);foreach(var n in part.Nodes)n.Y+=dy;bottom=part.Nodes.Max(n=>FlowCanvas.GetNodeBounds(n).Bottom);}
   var source=d.Nodes.FirstOrDefault(n=>n.Id==sourceId);var target=d.Nodes.FirstOrDefault(n=>n.Id==targetId);bool forward=source!=null&&target!=null&&target.Y>source.Y;var first=p.Parts[0].Entry;
   if(forward&&source.Kind!=NodeKind.Decision){float dy=Math.Max(0,FlowCanvas.GetNodeBounds(source).Bottom+56-first.Y);foreach(var n in d.Nodes.Where(n=>p.Ids.Contains(n.Id)))n.Y+=dy;}
   var bounds=d.Nodes.Where(n=>p.Ids.Contains(n.Id)&&n.Kind!=NodeKind.Note).Select(FlowCanvas.GetNodeBounds).Aggregate(RectangleF.Union);var queue=new Queue<string>();var moved=new HashSet<string>();if(forward)queue.Enqueue(targetId);
   while(queue.Count>0){string id=queue.Dequeue();if(id==null||p.Ids.Contains(id)||id==sourceId||!moved.Add(id))continue;var n=d.Nodes.FirstOrDefault(x=>x.Id==id);if(n==null)continue;float floor=id==targetId?bounds.Bottom+56:float.NegativeInfinity;
    foreach(var e in d.Edges.Where(e=>e.To==id&&!p.Ids.Contains(e.From))){var a=d.Nodes.FirstOrDefault(x=>x.Id==e.From);if(a!=null&&originalY[a.Id]<originalY[n.Id])floor=Math.Max(floor,FlowCanvas.GetNodeBounds(a).Bottom+56);}
    if(n.Y<floor)n.Y=floor;foreach(var e in d.Edges.Where(e=>e.From==id)){var next=d.Nodes.FirstOrDefault(x=>x.Id==e.To);if(next!=null&&originalY[next.Id]>=originalY[n.Id])queue.Enqueue(next.Id);}
   }
   return d.Nodes.All(n=>n.X>=-99900&&n.X<=99900&&n.Y>=-99900&&n.Y<=99900);
  }
 }
}
