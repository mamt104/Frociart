using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Frociart {
 // Structured regions, rather than BFS rows: reconvergence follows both complete arms.
 internal sealed class FlowLayout {
  private readonly Diagram diagram;private readonly FlowDesign design;
  private RectangleF Bounds(FlowNode n){return new RectangleF(n.X,n.Y,FlowSizing.Size(n,design).Width,FlowSizing.Size(n,design).Height);}
  private readonly Dictionary<string,FlowNode> nodes;
  private readonly Dictionary<string,List<FlowEdge>> outgoing;
  private readonly HashSet<string> placed=new HashSet<string>();
  private const float Gap=96, RowGap=72, SafeSpan=196000, SafeCoordinate=98000;
  private sealed class Placement {internal FlowNode Node;internal float X,Y;}
  private sealed class Region {
   internal float Width,Height;
   internal readonly List<Placement> Items=new List<Placement>();
   internal void Add(Region other,float x,float y){foreach(var p in other.Items)Items.Add(new Placement{Node=p.Node,X=p.X+x,Y=p.Y+y});}
  }
  private FlowLayout(Diagram d,FlowDesign design){diagram=d;this.design=design;nodes=d.Nodes.GroupBy(n=>n.Id).ToDictionary(g=>g.Key,g=>g.First());outgoing=d.Edges.Where(e=>nodes.ContainsKey(e.From)&&nodes.ContainsKey(e.To)).GroupBy(e=>e.From).ToDictionary(g=>g.Key,g=>g.OrderBy(e=>e.Branch=="true"?0:e.Branch=="false"?1:2).ThenBy(e=>e.To,StringComparer.Ordinal).ToList());}
  internal static void Arrange(Diagram d){if(d==null||d.Nodes.Count==0)return;Arrange(d,FlowAppearance.ActiveDesign);}
  internal static void Arrange(Diagram d,FlowDesign design){if(d==null||d.Nodes.Count==0)return;new FlowLayout(FlowAnnotations.Executable(d),design).Run();}
  internal static void ArrangeAnchored(Diagram target,Diagram original,FlowDesign before,FlowDesign after,PointF? focus){
   var source=original.Nodes.Where(n=>n.Kind!=NodeKind.Note).ToArray();if(source.Length==0)return;
   FlowNode anchor;if(focus.HasValue){PointF point=focus.Value;anchor=source.OrderBy(n=>{var s=FlowSizing.Size(n,before);double x=n.X+s.Width/2-point.X,y=n.Y+s.Height/2-point.Y;return x*x+y*y;}).First();}else anchor=source.FirstOrDefault(n=>n.Kind==NodeKind.Start)??source[0];
   var old=FlowSizing.Size(anchor,before);float x0=anchor.X+old.Width/2,y0=anchor.Y+old.Height/2;Arrange(target,after);
   var executable=target.Nodes.Where(n=>n.Kind!=NodeKind.Note).ToArray();var moved=executable.First(n=>n.Id==anchor.Id);var size=FlowSizing.Size(moved,after);
   float dx=Math.Max(-100000-executable.Min(n=>n.X),Math.Min(100000-executable.Max(n=>n.X),x0-moved.X-size.Width/2));
   float dy=Math.Max(-100000-executable.Min(n=>n.Y),Math.Min(100000-executable.Max(n=>n.Y),y0-moved.Y-size.Height/2));
   foreach(var n in executable){n.X+=dx;n.Y+=dy;}
  }
  private void Run(){
   float left=48;var plan=new List<Placement>();
   // Separate disconnected components; never overlay or discard unfinished blocks.
   var indegree=diagram.Edges.Select(e=>e.To).ToLookup(x=>x);
   var roots=diagram.Nodes.OrderBy(n=>n.Kind==NodeKind.Start?0:indegree[n.Id].Any()?2:1).ThenBy(n=>n.Id,StringComparer.Ordinal).ToList();
   foreach(var root in roots){if(placed.Contains(root.Id))continue;var region=Build(root.Id,null,new HashSet<string>(),0);foreach(var p in region.Items){var b=Bounds(p.Node);plan.Add(new Placement{Node=p.Node,X=left+region.Width/2+p.X-b.Width/2,Y=48+p.Y});}left+=region.Width+144;}
   Bound(plan);
   // Commit only a complete, portable plan. A failed layout leaves every node intact.
   foreach(var p in plan){p.Node.X=p.X;p.Node.Y=p.Y;}
  }
  private void Bound(List<Placement> plan){
   if(plan.Count==0)return;
   float minX=plan.Min(p=>p.X),maxX=plan.Max(p=>p.X),minY=plan.Min(p=>p.Y),maxY=plan.Max(p=>p.Y);
   if(maxX-minX>SafeSpan||maxY-minY>SafeSpan){
    float cellWidth=plan.Max(p=>FlowSizing.Size(p.Node,design).Width)+Gap,cellHeight=plan.Max(p=>FlowSizing.Size(p.Node,design).Height)+RowGap;
    int maxRows=Math.Max(1,(int)Math.Floor(SafeSpan/cellHeight)+1),maxColumns=Math.Max(1,(int)Math.Floor(SafeSpan/cellWidth)+1);
    int columns=(plan.Count+maxRows-1)/maxRows;
    if(columns>maxColumns)throw new FlowException("Il diagramma è troppo esteso per essere riordinato. Riduci il testo dei blocchi o dividi il lavoro in funzioni.");
    int rows=(plan.Count+columns-1)/columns;
    for(int i=0;i<plan.Count;i++){plan[i].X=48+(i/rows)*cellWidth;plan[i].Y=48+(i%rows)*cellHeight;}
    minX=plan.Min(p=>p.X);maxX=plan.Max(p=>p.X);minY=plan.Min(p=>p.Y);maxY=plan.Max(p=>p.Y);
   }
   float shiftX=maxX>SafeCoordinate?SafeCoordinate-maxX:minX<-SafeCoordinate?-SafeCoordinate-minX:0;
   float shiftY=maxY>SafeCoordinate?SafeCoordinate-maxY:minY<-SafeCoordinate?-SafeCoordinate-minY:0;
   foreach(var p in plan){p.X+=shiftX;p.Y+=shiftY;if(Single.IsNaN(p.X)||Single.IsInfinity(p.X)||Single.IsNaN(p.Y)||Single.IsInfinity(p.Y)||Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000)throw new FlowException("Non è possibile riordinare questo diagramma senza superare i limiti del foglio.");}
  }
  private Region Build(string start,string stop,HashSet<string> ancestors,int depth){
   var result=new Region();string id=start;var path=new HashSet<string>(ancestors);
   while(id!=null&&id!=stop&&nodes.ContainsKey(id)&&!placed.Contains(id)&&!path.Contains(id)){
    FlowNode node=nodes[id];placed.Add(id);path.Add(id);var size=Bounds(node);
    float top=result.Items.Count==0?0:result.Height+RowGap;
    result.Items.Add(new Placement{Node=node,X=0,Y=top});result.Width=Math.Max(result.Width,size.Width);result.Height=top+size.Height;
    List<FlowEdge> links;if(!outgoing.TryGetValue(id,out links))break;
    var next=links.Where(e=>e.To!=id&&!path.Contains(e.To)).ToList();
    if(next.Count==0)break;
    if(node.Kind==NodeKind.Decision&&next.Count>1&&depth<64){
     string join=Join(next[0].To,next[1].To,path,stop);
     // A loop's body returns to an ancestor; the other arm continues on the main axis.
     if(join==null){bool aReturns=Returns(next[0].To,id),bReturns=Returns(next[1].To,id);if(aReturns!=bReturns)join=aReturns?next[1].To:next[0].To;}
     var a=Build(next[0].To,join??stop,path,depth+1);
     var b=Build(next[1].To,join??stop,path,depth+1);
     float arm=Math.Max(a.Width,b.Width),offset=Math.Max(size.Width/2+48,arm/2+Gap/2);
     float y=result.Height+RowGap;
     result.Add(a,-offset,y);result.Add(b,offset,y);
     result.Width=Math.Max(result.Width,2*offset+arm);
     result.Height=y+Math.Max(a.Height,b.Height);
     id=join;
    }else id=next[0].To;
   }
   return result;
  }
  private Dictionary<string,int> Reach(string start,HashSet<string> blocked,string stop){
   var distances=new Dictionary<string,int>();var q=new Queue<string>();if(blocked.Contains(start))return distances;distances[start]=0;q.Enqueue(start);
   while(q.Count>0){string id=q.Dequeue();List<FlowEdge> links;if(id==stop||!outgoing.TryGetValue(id,out links))continue;foreach(var e in links)if(!blocked.Contains(e.To)&&!distances.ContainsKey(e.To)){distances[e.To]=distances[id]+1;q.Enqueue(e.To);}}
   return distances;
  }
  private string Join(string a,string b,HashSet<string> path,string stop){
   var ra=Reach(a,path,stop);var rb=Reach(b,path,stop);
   return ra.Keys.Where(k=>rb.ContainsKey(k)&&!placed.Contains(k)).OrderBy(k=>Math.Max(ra[k],rb[k])).ThenBy(k=>ra[k]+rb[k]).ThenBy(k=>k,StringComparer.Ordinal).FirstOrDefault();
  }
  private bool Returns(string start,string header){return Reach(start,new HashSet<string>(),null).ContainsKey(header);}
 }
}
