using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
namespace Frociart {
 internal static class FlowPlacement {
  internal static string Continuation(Diagram d,string id){
   var n=d.Nodes.FirstOrDefault(x=>x.Id==id);if(n==null||n.Kind==NodeKind.Start||n.Kind==NodeKind.End)return null;
   var output=d.Edges.Where(e=>e.From==id).ToArray();if(output.Length==0)return d.Edges.Any(e=>e.To==id)?null:"";
   string target=output[0].To;if(target==id||!d.Nodes.Any(x=>x.Id==target&&x.Kind!=NodeKind.Start)||output.Any(e=>e.To!=target)||d.Edges.Any(e=>e.To==id&&e.From==target))return null;
   if(n.Kind!=NodeKind.Decision&&(output.Length!=1||output[0].Branch!="next"))return null;return target;
  }
  internal static bool Detach(Diagram d,string id){string next=Continuation(d,id);if(next==null)return false;foreach(var e in d.Edges.Where(e=>e.To==id))e.To=next;d.Edges.RemoveAll(e=>e.From==id);return true;}
  internal static bool Disconnect(Diagram d,string id){var n=d.Nodes.FirstOrDefault(x=>x.Id==id);if(n==null||n.Kind==NodeKind.Start||n.Kind==NodeKind.End)return false;return d.Edges.RemoveAll(e=>e.From==id||e.To==id)>0;}
  internal static bool DetachVia(Diagram d,string id,string branch){var n=d.Nodes.FirstOrDefault(x=>x.Id==id);var e=d.Edges.FirstOrDefault(x=>x.From==id&&x.Branch==branch);if(n==null||n.Kind!=NodeKind.Decision||e==null||e.To==id||!d.Nodes.Any(x=>x.Id==e.To&&x.Kind!=NodeKind.Start)||d.Edges.Any(x=>x.To==id&&x.From==e.To))return false;string target=e.To;foreach(var incoming in d.Edges.Where(x=>x.To==id))incoming.To=target;d.Edges.RemoveAll(x=>x.From==id);return true;}
  internal static void Reflow(Diagram d,string anchorId,string sourceId,string targetId,bool forward){Reflow(d,anchorId,sourceId,targetId,forward,false);}
  internal static void Grow(Diagram d,string anchorId){Reflow(d,anchorId,null,null,false,true);}
  private static void Reflow(Diagram d,string anchorId,string sourceId,string targetId,bool forward,bool resizing){
   var a=d.Nodes.FirstOrDefault(n=>n.Id==anchorId);if(a==null||a.Kind==NodeKind.Note)return;d=FlowAnnotations.Executable(d);
   var originals=d.Nodes.ToDictionary(n=>n.Id,n=>FlowCanvas.GetNodeBounds(n));var active=new Queue<FlowNode>();var queued=new HashSet<string>();
   Action<FlowNode> enqueue=n=>{if(n!=null&&queued.Add(n.Id))active.Enqueue(n);};
   var source=d.Nodes.FirstOrDefault(n=>n.Id==sourceId);var target=d.Nodes.FirstOrDefault(n=>n.Id==targetId);var ab=FlowCanvas.GetNodeBounds(a);
   if(forward&&source!=null){var sb=FlowCanvas.GetNodeBounds(source);if(source.Kind!=NodeKind.Decision&&sb.Bottom+56>ab.Top){source.Y=Clamp(ab.Top-sb.Height-56);enqueue(source);}}
   if(forward&&target!=null&&target!=a){if(target.Y<ab.Bottom+56){target.Y=Clamp(ab.Bottom+56);enqueue(target);}}
   enqueue(a);int budget=d.Nodes.Count*12+12;
   while(active.Count>0&&budget-->0){var n=active.Dequeue();queued.Remove(n.Id);var b=FlowCanvas.GetNodeBounds(n);var padded=b;padded.Inflate(18,18);
    foreach(var other in d.Nodes){if(other==n||other==a)continue;var ob=FlowCanvas.GetNodeBounds(other);if(!padded.IntersectsWith(ob))continue;
     var original=originals[other.Id];var center=originals[n.Id];bool vertical=other==source||other==target||Math.Abs(original.X-center.X)<Math.Min(original.Width,center.Width)*.65f;
     if(vertical){bool below=other==target||other!=source&&(resizing?original.Top>=originals[n.Id].Top:original.Top+original.Height/2>=ab.Top+ab.Height/2);float desired=below?padded.Bottom:padded.Top-ob.Height;if(desired>99900)desired=padded.Top-ob.Height;if(desired< -99900)desired=padded.Bottom;other.Y=Clamp(desired);}
     else{bool right=original.Left+original.Width/2>=ab.Left+ab.Width/2;float desired=right?padded.Right:padded.Left-ob.Width;if(desired>99900)desired=padded.Left-ob.Width;if(desired< -99900)desired=padded.Right;other.X=Clamp(desired);}
     enqueue(other);
    }
    foreach(var edge in d.Edges.Where(e=>e.From==n.Id)){var next=d.Nodes.FirstOrDefault(x=>x.Id==edge.To);if(next==null||next==a||next==source||originals[next.Id].Top<=originals[n.Id].Top)continue;
     if(next.Y<b.Bottom+56){next.Y=Clamp(b.Bottom+56);enqueue(next);}
    }
   }
  }
  private static float Clamp(float v){return Math.Max(-99900,Math.Min(99900,v));}
 }
}
