using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Frociart {
 // A geometry-only opening plan. No graph restructuring and no persisted style.
 internal static class FlowTextSpacing {
  private sealed class Box {internal FlowNode Node;internal SizeF Before,After;internal PointF Original;}
  internal static Dictionary<string,PointF> Plan(Diagram project,FlowDesign design){
   var result=new Dictionary<string,PointF>();if(design.FontKey==FlowDesign.Default.FontKey)return result;
   foreach(var graph in FunctionEditing.Graphs(project))foreach(var p in Graph(graph,n=>FlowSizing.Size(n,FlowDesign.Default),n=>FlowSizing.Size(n,design)))result.Add(p.Key,p.Value);
   return result;
  }
  internal static Dictionary<string,PointF> Graph(Diagram graph,Func<FlowNode,SizeF> before,Func<FlowNode,SizeF> after){
   var rows=graph.Nodes.Where(n=>n.Kind!=NodeKind.Note).Select(n=>new Box{Node=n,Before=before(n),After=after(n),Original=new PointF(n.X,n.Y)}).ToArray();int count=rows.Length;
   var result=new Dictionary<string,PointF>();if(count<2)return result;
   var ids=rows.Select((r,i)=>new{r.Node.Id,Index=i}).ToDictionary(x=>x.Id,x=>x.Index);
   int[] parent=Enumerable.Range(0,count).ToArray();bool[] active=new bool[count];
   Func<int,int> root=null;root=i=>parent[i]==i?i:(parent[i]=root(parent[i]));
   Action<int,int> join=(i,j)=>{i=root(i);j=root(j);if(i==j)return;int a=Math.Min(i,j),b=Math.Max(i,j);parent[b]=a;active[a]|=active[b];};
   foreach(var e in graph.Edges){int a,b;if(ids.TryGetValue(e.From,out a)&&ids.TryGetValue(e.To,out b))join(a,b);}
   var baseline=new bool[count,count];for(int i=0;i<count;i++)for(int j=i+1;j<count;j++)baseline[i,j]=Overlap(rows[i].Original,rows[i].Before,rows[j].Original,rows[j].Before,0);
   var positions=rows.Select(r=>r.Original).ToArray();bool adjusted=false;
   for(int pass=0;pass<=count*2;pass++){
    foreach(var group in Enumerable.Range(0,count).GroupBy(i=>root(i))){
     if(!active[group.Key]){foreach(int i in group)positions[i]=rows[i].Original;continue;}
     int[] members=group.ToArray();int anchor=members.FirstOrDefault(i=>rows[i].Node.Kind==NodeKind.Start);
     if(!members.Any(i=>rows[i].Node.Kind==NodeKind.Start))anchor=members.OrderBy(i=>rows[i].Original.Y).ThenBy(i=>rows[i].Original.X).ThenBy(i=>rows[i].Node.Id,StringComparer.Ordinal).First();
     float sx=1,sy=1;foreach(int i in members){sx=Math.Max(sx,rows[i].After.Width/rows[i].Before.Width);sy=Math.Max(sy,rows[i].After.Height/rows[i].Before.Height);}if(sx>1)sx*=1.005f;if(sy>1)sy*=1.005f;
     float ax=rows[anchor].Original.X+rows[anchor].Before.Width/2,ay=rows[anchor].Original.Y+rows[anchor].Before.Height/2;
     foreach(int i in members){var r=rows[i];positions[i]=new PointF(ax+(r.Original.X+r.Before.Width/2-ax)*sx-r.After.Width/2,ay+(r.Original.Y+r.Before.Height/2-ay)*sy-r.After.Height/2);}
    }
    bool changed=false,unresolved=false;
    for(int i=0;i<count;i++)for(int j=i+1;j<count;j++){
     if(baseline[i,j]||!Overlap(positions[i],rows[i].After,positions[j],rows[j].After,1))continue;
     int a=root(i),b=root(j);if(a==b&&active[a]){unresolved=true;continue;}
     join(a,b);active[root(a)]=true;changed=adjusted=true;
    }
    if(changed)continue;
    if(unresolved)throw new FlowException("Non riesco ad adattare questi blocchi senza cambiare la disposizione. Usa Riordina oppure riduci il testo.");
    if(!adjusted)return result;
    foreach(int i in Enumerable.Range(0,count)){
     var p=positions[i];if(Single.IsNaN(p.X)||Single.IsInfinity(p.X)||Single.IsNaN(p.Y)||Single.IsInfinity(p.Y)||Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000)throw new FlowException("L’adattamento supera i limiti del foglio. Riduci il testo o dividi il diagramma in funzioni.");
     if(Math.Abs(p.X-rows[i].Original.X)>.001f||Math.Abs(p.Y-rows[i].Original.Y)>.001f)result.Add(rows[i].Node.Id,p);
    }
    return result;
   }
   throw new FlowException("Non riesco ad adattare la spaziatura di questo diagramma. La disposizione originale è stata mantenuta.");
  }
  private static bool Overlap(PointF a,SizeF sa,PointF b,SizeF sb,float tolerance){return Math.Min(a.X+sa.Width,b.X+sb.Width)-Math.Max(a.X,b.X)>tolerance&&Math.Min(a.Y+sa.Height,b.Y+sb.Height)-Math.Max(a.Y,b.Y)>tolerance;}
 }
 public partial class FlowCanvas {
  internal void FrameOpeningView(){
   FitToView();float minimum=Math.Min(2.5f,.85f*DeviceDpi/96f);if(_zoom>=minimum||_diagram==null||_diagram.Nodes.Count==0)return;
   var node=_diagram.Nodes.FirstOrDefault(n=>n.Kind==NodeKind.Start)??_diagram.Nodes.OrderBy(n=>n.Kind==NodeKind.Note?1:0).ThenBy(n=>n.Y).ThenBy(n=>n.X).First();
   var b=DisplayBounds(node);_zoom=minimum;_panX=ClientSize.Width/2f-(b.Left+b.Width/2)*_zoom;_panY=48*DeviceDpi/96f-b.Top*_zoom;RequestFrameRepaint();RaiseViewChanged();
  }
  internal void RefreshOpeningGeometry(){ClearScene();RequestFrameRepaint();}
  internal void AnimateOpeningPositions(Dictionary<string,PointF> before){ClearScene();AnimatePlacement(before);RequestFrameRepaint();}
 }
}
