using System;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;

namespace Frociart {
 // Editable canvas presentation. Scene is disposable geometry, never the document or interpreter input.
 internal sealed class FlowCycleFold {
  internal sealed class Projection {
   internal readonly Diagram Scene=new Diagram();
   internal readonly Dictionary<string,FlowCycle.Info> Cards=new Dictionary<string,FlowCycle.Info>();
   internal readonly Dictionary<string,string> Owners=new Dictionary<string,string>();
   internal readonly Dictionary<string,PointF> Offsets=new Dictionary<string,PointF>();
   internal readonly Dictionary<string,FlowNode> SourceNodes,VisualNodes=new Dictionary<string,FlowNode>();
   internal readonly Dictionary<string,FlowEdge> SourceEdges;
   internal Projection(Diagram source){Scene.Title=source.Title;SourceNodes=source.Nodes.ToDictionary(n=>n.Id);SourceEdges=source.Edges.ToDictionary(e=>e.Id);}
   internal string Owner(string id){string owner;return id!=null&&Owners.TryGetValue(id,out owner)?owner:id;}
   internal FlowNode Visual(string id){FlowNode value;return id!=null&&VisualNodes.TryGetValue(Owner(id),out value)?value:null;}
   internal IEnumerable<string> Selection(IEnumerable<string> ids){var result=new HashSet<string>();foreach(string id in ids){FlowCycle.Info card;if(Cards.TryGetValue(Owner(id),out card))result.UnionWith(card.Members);else result.Add(id);}return result;}
   internal bool ContainsActive(string card,string active){return Owner(active)==card;}
   internal void Sync(){foreach(var n in Scene.Nodes){var source=SourceNodes[n.Id];var offset=Offsets[n.Id];n.X=source.X+offset.X;n.Y=source.Y+offset.Y;if(!Cards.ContainsKey(n.Id)){n.Text=source.Text;n.Prompt=source.Prompt;n.Comment=source.Comment;n.PreviewText=source.PreviewText;n.PreviewComment=source.PreviewComment;n.TrueLabel=source.TrueLabel;n.FalseLabel=source.FalseLabel;n.TrueColor=source.TrueColor;n.FalseColor=source.FalseColor;n.Breakpoint=source.Breakpoint;n.BreakpointCondition=source.BreakpointCondition;}}}
  }
  private readonly Diagram source;private readonly FlowDesign design;
  private string signature;
  internal List<FlowCycle.Info> Cycles {get;private set;}
  internal Projection Current {get;private set;}
  internal int Rebuilds {get;private set;}
  internal FlowCycleFold(Diagram document):this(document,FlowAppearance.ActiveDesign){}
  internal FlowCycleFold(Diagram document,FlowDesign design){source=document;this.design=design??FlowAppearance.ActiveDesign;Update(true);}
  private string Signature(){var s=new StringBuilder();foreach(var n in source.Nodes){s.Append(n.Id).Append(':').Append((int)n.Kind).Append(':').Append(n.Text==null?-1:n.Text.Length).Append(':').Append(n.Text).Append(n.FoldedCycle?'1':'0').Append(n.Breakpoint?'1':'0').Append(';');}foreach(var e in source.Edges)s.Append(e.Id).Append(':').Append(e.From).Append(':').Append(e.To).Append(':').Append(e.Branch).Append(';');return s.ToString();}
  internal void Update(bool force=false){string next=Signature();if(!force&&signature==next){if(Current!=null)Current.Sync();return;}signature=next;Rebuilds++;
   Cycles=Candidates(source);var unsafeKeys=new HashSet<string>();foreach(var a in Cycles)foreach(var b in Cycles)if(a!=b&&a.Members.Overlaps(b.Members)&&!a.Members.IsProperSubsetOf(b.Members)&&!b.Members.IsProperSubsetOf(a.Members)){unsafeKeys.Add(a.Check);unsafeKeys.Add(b.Check);}Cycles.RemoveAll(c=>unsafeKeys.Contains(c.Check)||c.Members.Count<2);
   var folded=Cycles.Where(c=>source.Nodes.First(n=>n.Id==c.Check).FoldedCycle).ToList();var visible=folded.Where(c=>!folded.Any(parent=>c.Members.IsProperSubsetOf(parent.Members))).ToList();
   if(visible.Count==0){Current=null;return;}var p=new Projection(source);foreach(var c in visible){p.Cards.Add(c.Entry,c);foreach(string id in c.Members)p.Owners.Add(id,c.Entry);}
   foreach(var n in source.Nodes){string owner;if(p.Owners.TryGetValue(n.Id,out owner)&&owner!=n.Id)continue;FlowNode copy;FlowCycle.Info card;
    if(p.Cards.TryGetValue(n.Id,out card)){int stops=card.Members.Count(id=>p.SourceNodes[id].Breakpoint);string header=card.Header??"";if(header.Length>240)header=header.Substring(0,239)+"…";copy=new FlowNode{Id=n.Id,Kind=NodeKind.Process,Text=header+"\n"+card.Members.Count+" blocchi · espandi"+(card.Exit==null?"\nUscita da collegare":"")+(stops==0?"":stops==1?"\n1 punto di interruzione":"\n"+stops+" punti di interruzione"),X=n.X,Y=n.Y,Breakpoint=stops>0};}
    else copy=Copy(n);p.Scene.Nodes.Add(copy);p.VisualNodes.Add(copy.Id,copy);p.Offsets.Add(copy.Id,PointF.Empty);
   }
   foreach(var e in source.Edges){string from=p.Owner(e.From),to=p.Owner(e.To);if(from==to&&p.Cards.ContainsKey(from))continue;p.Scene.Edges.Add(new FlowEdge{Id=e.Id,From=from,To=to,Branch=p.Owners.ContainsKey(e.From)?"next":e.Branch});}
   var back=new HashSet<string>();foreach(var c in Cycles)foreach(var e in source.Edges)if(c.Kind==2?e.From==c.Check&&e.To==c.BodyEntry:e.To==c.Check&&c.Members.Contains(e.From)&&(e.From!=c.Entry||e.From==e.To))back.Add(e.Id);
   var ranges=new Dictionary<string,List<PointF>>();foreach(var c in visible){var entry=p.SourceNodes[c.Entry];var capsule=p.VisualNodes[c.Entry];float bottom=c.Members.Max(id=>p.SourceNodes[id].Y+FlowSizing.Size(p.SourceNodes[id],design).Height);float start=entry.Y+FlowSizing.Size(capsule,design).Height;
    p.Offsets[c.Entry]=new PointF((FlowSizing.Size(entry,design).Width-FlowSizing.Size(capsule,design).Width)/2,0);
    var downstream=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(c.Exit);while(queue.Count>0){string id=queue.Dequeue();if(id==null||c.Members.Contains(id)||!downstream.Add(id))continue;foreach(var edge in source.Edges.Where(e=>e.From==id&&!back.Contains(e.Id)))queue.Enqueue(edge.To);}
    foreach(string visual in downstream.Select(p.Owner).Distinct()){FlowNode original;if(bottom<=start||visual==c.Entry||!p.SourceNodes.TryGetValue(visual,out original)||!p.Offsets.ContainsKey(visual)||original.Y<bottom-.5f)continue;List<PointF> list;if(!ranges.TryGetValue(visual,out list)){list=new List<PointF>();ranges.Add(visual,list);}list.Add(new PointF(start,bottom));}
   }
   foreach(var pair in ranges){float sum=0,left=0,right=0;bool first=true;foreach(var interval in pair.Value.OrderBy(v=>v.X)){if(first){left=interval.X;right=interval.Y;first=false;}else if(interval.X<=right)right=Math.Max(right,interval.Y);else{sum+=right-left;left=interval.X;right=interval.Y;}}sum+=right-left;var offset=p.Offsets[pair.Key];p.Offsets[pair.Key]=new PointF(offset.X,-sum);}
   p.Sync();foreach(var from in p.Scene.Nodes.OrderBy(n=>p.SourceNodes[n.Id].Y))foreach(var edge in p.Scene.Edges.Where(e=>e.From==from.Id&&!back.Contains(e.Id))){var to=p.VisualNodes[edge.To];var originalTo=p.SourceNodes[to.Id];var originalFrom=p.SourceNodes[from.Id];if(originalTo.Y<=originalFrom.Y)continue;FlowCycle.Info card;float bottom=p.Cards.TryGetValue(from.Id,out card)?card.Members.Max(id=>p.SourceNodes[id].Y+FlowSizing.Size(p.SourceNodes[id],design).Height):originalFrom.Y+FlowSizing.Size(originalFrom,design).Height;float minimum=from.Y+FlowSizing.Size(from,design).Height+Math.Max(16,originalTo.Y-bottom);if(to.Y<minimum){to.Y=minimum;var offset=p.Offsets[to.Id];p.Offsets[to.Id]=new PointF(offset.X,minimum-originalTo.Y);}}
   Current=p;
  }
  internal bool Set(string check,bool folded){var cycle=Cycles.FirstOrDefault(c=>c.Check==check);if(cycle==null)return false;var node=source.Nodes.First(n=>n.Id==check);if(node.FoldedCycle==folded)return false;node.FoldedCycle=folded;Update();return true;}
  internal bool All(bool folded){bool changed=false;foreach(var cycle in Cycles){var node=source.Nodes.First(n=>n.Id==cycle.Check);changed|=node.FoldedCycle!=folded;node.FoldedCycle=folded;}if(changed)Update();return changed;}
  internal bool Reveal(string id){bool changed=false;foreach(var c in Cycles)if(c.Members.Contains(id)){var n=source.Nodes.First(v=>v.Id==c.Check);changed|=n.FoldedCycle;n.FoldedCycle=false;}if(changed)Update();return changed;}
  static List<FlowCycle.Info> Candidates(Diagram graph){var probe=new Diagram{Nodes=new List<FlowNode>(graph.Nodes),Edges=new List<FlowEdge>(graph.Edges)};var added=new HashSet<string>();foreach(var node in graph.Nodes.Where(n=>n.Kind==NodeKind.Decision)){var edges=graph.Edges.Where(e=>e.From==node.Id).ToList();if(edges.Count!=1)continue;string branch=edges[0].Branch=="true"?"false":edges[0].Branch=="false"?"true":null;if(branch==null)continue;var end=new FlowNode{Kind=NodeKind.End};added.Add(end.Id);probe.Nodes.Add(end);probe.Edges.Add(new FlowEdge{From=node.Id,To=end.Id,Branch=branch});}var result=FlowCycle.Find(probe);foreach(var c in result)if(added.Contains(c.Exit))c.Exit=null;return result.Where(c=>!c.Members.Overlaps(added)).ToList();}
  static FlowNode Copy(FlowNode n){return new FlowNode{Id=n.Id,Kind=n.Kind,Text=n.Text,Prompt=n.Prompt,Comment=n.Comment,X=n.X,Y=n.Y,TrueLabel=n.TrueLabel,FalseLabel=n.FalseLabel,TrueColor=n.TrueColor,FalseColor=n.FalseColor,Breakpoint=n.Breakpoint,BreakpointCondition=n.BreakpointCondition,FoldedCycle=n.FoldedCycle};}
 }
}
