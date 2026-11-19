using System;using System.Collections.Generic;using System.Linq;
namespace Frociart {
 // A navigation projection. Its capsules are NEVER compiled, saved or copied to the document.
 internal sealed class FlowCycleView {
  internal sealed class Projection {internal Diagram Graph;internal Dictionary<string,FlowCycle.Info> Cards=new Dictionary<string,FlowCycle.Info>();internal Dictionary<string,string> Owner=new Dictionary<string,string>();}
  private readonly Diagram source;
  internal readonly List<FlowCycle.Info> Cycles;
  internal readonly HashSet<string> Folded=new HashSet<string>();
  internal FlowCycleView(Diagram graph){source=DiagramIO.Clone(graph);Cycles=FlowCycle.Find(source);var unsafeKeys=new HashSet<string>();foreach(var a in Cycles)foreach(var b in Cycles)if(a!=b&&a.Members.Overlaps(b.Members)&&!a.Members.IsProperSubsetOf(b.Members)&&!b.Members.IsProperSubsetOf(a.Members)){unsafeKeys.Add(a.Check);unsafeKeys.Add(b.Check);}Cycles.RemoveAll(c=>unsafeKeys.Contains(c.Check));All(true);}
  internal bool IsFolded(string check){return Folded.Contains(check);}
  internal void All(bool folded){Folded.Clear();if(folded)foreach(var c in Cycles)Folded.Add(c.Check);}
  internal void Set(string check,bool folded){var c=Cycles.FirstOrDefault(i=>i.Check==check);if(c==null)return;foreach(var parent in Cycles)if(c.Members.IsProperSubsetOf(parent.Members))Folded.Remove(parent.Check);if(folded)Folded.Add(check);else Folded.Remove(check);}
  internal Projection Build(){var p=new Projection{Graph=new Diagram{Title=source.Title}};var visible=Cycles.Where(c=>Folded.Contains(c.Check)&&!Cycles.Any(parent=>Folded.Contains(parent.Check)&&c.Members.IsProperSubsetOf(parent.Members))).ToList();
   foreach(var c in visible){p.Cards.Add(c.Entry,c);foreach(string id in c.Members)p.Owner.Add(id,c.Entry);}
   var copy=DiagramIO.Clone(source);foreach(var n in copy.Nodes){string owner;if(!p.Owner.TryGetValue(n.Id,out owner))p.Graph.Nodes.Add(n);else if(n.Id==owner){var c=p.Cards[owner];int stops=source.Nodes.Count(b=>c.Members.Contains(b.Id)&&b.Breakpoint);p.Graph.Nodes.Add(new FlowNode{Id=owner,Kind=NodeKind.Process,Text=c.Header+"\n"+c.Members.Count+" blocchi"+(stops>0?" · "+stops+" punti di interruzione":""),X=n.X,Y=n.Y,Breakpoint=stops>0});}}
   foreach(var e in source.Edges){string from,to;bool a=p.Owner.TryGetValue(e.From,out from),b=p.Owner.TryGetValue(e.To,out to);if(!a)from=e.From;if(!b)to=e.To;if(a&&b&&from==to)continue;p.Graph.Edges.Add(new FlowEdge{Id=e.Id,From=from,To=to,Branch=a?"next":e.Branch});}
   FlowLayout.Arrange(p.Graph);return p;
  }
  internal static string Caption(FlowCycle.Info c){return (c.Kind==0?"CICLO PER":c.Kind==1?"CICLO MENTRE":"CICLO RIPETI")+" · ESPANDI";}
 }
}
