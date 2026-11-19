using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
 public partial class FlowCanvas {
  private readonly Dictionary<string,int> entranceNodeDelays=new Dictionary<string,int>();
  private readonly Stopwatch entranceClock=new Stopwatch();
  private Timer entranceTimer;private Diagram entranceDocument;private int entranceDuration,entranceNodeCount,entranceEdgeCount;
  private const int EntranceNodeMilliseconds=220,EntranceEdgeMilliseconds=200;
  [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
  internal bool DocumentEntranceActive {get{return entranceDocument!=null;}}
  [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
  internal float DocumentEntranceProgress {get{return !DocumentEntranceActive?1:Math.Min(1,entranceClock.ElapsedMilliseconds/(float)Math.Max(1,entranceDuration));}}
  [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
  internal int DocumentEntranceDuration {get{return entranceDuration;}}
  internal void BeginDocumentEntrance(){
   FinishDocumentEntrance();
   if(!UiMotion.Enabled||ViewOnly||_exportRendering||!Visible||!IsHandleCreated||_diagram==null||_diagram.Nodes.Count==0)return;
   // Opening/adaptation has already committed its final coordinates. The reveal
   // owns presentation only, without layering a second moving camera on it.
   settleFrom=null;arrangeCamera=false;landingId=null;EnsureScene();
   RectangleF viewport=new RectangleF(-_panX/_zoom,-_panY/_zoom,ClientSize.Width/_zoom,ClientSize.Height/_zoom);
   var nodes=_nodeVisuals.Where(n=>n.Bounds.IntersectsWith(viewport)).OrderBy(n=>n.Bounds.Top).ThenBy(n=>n.Bounds.Left).ThenBy(n=>n.Id,StringComparer.Ordinal).ToArray();
   if(nodes.Length==0)return;int span=nodes.Length<=8?Math.Max(180,(nodes.Length-1)*45):420;
   entranceNodeDelays.Clear();entranceDuration=EntranceNodeMilliseconds;
   for(int i=0;i<nodes.Length;i++){int delay=nodes.Length==1?0:(int)Math.Round(i*span/(double)(nodes.Length-1));entranceNodeDelays[nodes[i].Id]=delay;entranceDuration=Math.Max(entranceDuration,delay+EntranceNodeMilliseconds);}
   foreach(var edge in _edgeVisuals){if(!edge.Bounds.IntersectsWith(viewport))continue;int from,to;if(!entranceNodeDelays.TryGetValue(edge.Model.From,out from))from=-EntranceNodeMilliseconds;if(!entranceNodeDelays.TryGetValue(edge.Model.To,out to))to=-EntranceNodeMilliseconds;entranceDuration=Math.Max(entranceDuration,Math.Max(from,to)+EntranceNodeMilliseconds+EntranceEdgeMilliseconds);}
   entranceDuration=Math.Min(900,entranceDuration);entranceNodeCount=_diagram.Nodes.Count;entranceEdgeCount=_diagram.Edges.Count;entranceDocument=_diagram;
   if(entranceTimer==null){entranceTimer=new Timer{Interval=16};entranceTimer.Tick+=delegate{if(!UiMotion.Enabled||!Visible||ViewOnly||!ReferenceEquals(entranceDocument,_diagram)||_diagram==null||_diagram.Nodes.Count!=entranceNodeCount||_diagram.Edges.Count!=entranceEdgeCount||entranceClock.ElapsedMilliseconds>=entranceDuration){FinishDocumentEntrance();return;}RequestFrameRepaint();};}
   UiMotion.Changed+=EntranceMotionChanged;VisibleChanged+=EntranceVisibilityChanged;SizeChanged+=EntranceViewportChanged;entranceClock.Restart();entranceTimer.Start();RequestFrameRepaint();
  }
  internal void FinishDocumentEntrance(){
   bool active=entranceDocument!=null;entranceDocument=null;entranceClock.Stop();entranceNodeDelays.Clear();if(entranceTimer!=null)entranceTimer.Stop();UiMotion.Changed-=EntranceMotionChanged;VisibleChanged-=EntranceVisibilityChanged;SizeChanged-=EntranceViewportChanged;
   if(active&&!IsDisposed)RequestFrameRepaint();
  }
  private void EntranceMotionChanged(object sender,EventArgs e){if(!UiMotion.Enabled)FinishDocumentEntrance();}
  private void EntranceVisibilityChanged(object sender,EventArgs e){if(!Visible)FinishDocumentEntrance();}
  private void EntranceViewportChanged(object sender,EventArgs e){FinishDocumentEntrance();}
  private void DisposeDocumentEntrance(){FinishDocumentEntrance();if(entranceTimer!=null){entranceTimer.Dispose();entranceTimer=null;}}
  private bool PaintDocumentEntrance(Graphics graphics,Rectangle clip){
   if(!DocumentEntranceActive||ViewOnly||_exportRendering)return false;
   if(!UiMotion.Enabled||!ReferenceEquals(_diagram,entranceDocument)||entranceClock.ElapsedMilliseconds>=entranceDuration){FinishDocumentEntrance();return false;}
   EnsureScene();DrawGrid(graphics,clip);var state=graphics.Save();
   try{
    using(var transform=new Matrix(_zoom,0,0,_zoom,_panX,_panY))graphics.Transform=transform;RectangleF visible=new RectangleF(-_panX/_zoom,-_panY/_zoom,ClientSize.Width/_zoom,ClientSize.Height/_zoom);long elapsed=entranceClock.ElapsedMilliseconds;
    foreach(var edge in _edgeVisuals){int from,to;if(edge.Points==null||edge.Path==null)continue;if(!entranceNodeDelays.TryGetValue(edge.Model.From,out from))from=-EntranceNodeMilliseconds;if(!entranceNodeDelays.TryGetValue(edge.Model.To,out to))to=-EntranceNodeMilliseconds;
     int delay=Math.Max(from,to)+EntranceNodeMilliseconds;float t=Math.Max(0,Math.Min(1,(elapsed-delay)/(float)EntranceEdgeMilliseconds));if(t<=0)continue;RectangleF bounds=edge.Bounds;bounds.Inflate(Math.Max(36,edge.LabelPadding),Math.Max(36,edge.LabelPadding));if(!bounds.IntersectsWith(visible))continue;
     if(t>=1){DrawEdge(graphics,edge.Model,edge.Points,edge.Path,visible,false,_zoom);continue;}
     PointF[] points;using(var path=(GraphicsPath)edge.Path.Clone()){path.Flatten(null,.5f);points=path.PathPoints;}var prefix=Prefix(points,UiMotion.Ease(t));if(prefix.Count<2)continue;
     Color color=BranchColor(FindNode(edge.Model.From),edge.Model.Branch);using(var pen=new Pen(color,appearance.Width(1.8f)/Math.Max(.7f,_zoom))){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;graphics.DrawLines(pen,prefix.ToArray());}
    }
    foreach(var node in _nodeVisuals){int delay;if(!entranceNodeDelays.TryGetValue(node.Id,out delay))delay=-EntranceNodeMilliseconds;float t=Math.Max(0,Math.Min(1,(elapsed-delay)/(float)EntranceNodeMilliseconds));if(t<=0)continue;RectangleF bounds=node.Bounds;bounds.Inflate(NodePaintPadding(node.Model),NodePaintPadding(node.Model));if(!bounds.IntersectsWith(visible))continue;
     var nodeState=graphics.Save();try{float offset=8/Math.Max(.18f,_zoom)*(1-UiMotion.Ease(t));graphics.TranslateTransform(0,offset);if(t<1){RectangleF reveal=node.Bounds;reveal.Inflate(12/Math.Max(.18f,_zoom),12/Math.Max(.18f,_zoom));reveal.Height*=UiMotion.Ease(t);graphics.SetClip(reveal,CombineMode.Intersect);}DrawNode(graphics,node,false,_zoom);}finally{graphics.Restore(nodeState);}
    }
   }finally{graphics.Restore(state);}return true;
  }
 }
}
