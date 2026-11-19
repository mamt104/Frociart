using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
 public partial class FlowCanvas {
  // A stroke is a preview until release. One mutation joins all crossed edges
  // into one existing history entry; losing capture or Escape never edits data.
  private sealed class BladeEdge {
   internal FlowEdge Model;internal PointF[] Points;internal float[] Lengths;
   internal RectangleF Bounds;internal float Length,Cut;internal PointF Hit;
   internal Color Color;internal long At;
  }
  private struct BladePoint {internal PointF Point;internal long At;internal BladePoint(PointF point,long at){Point=point;At=at;}}
  private bool bladePending,bladeActive,bladeSuppressUp;
  private Diagram bladeDocument;
  private Point bladeLast;
  private readonly List<BladeEdge> bladeCandidates=new List<BladeEdge>();
  private readonly Dictionary<string,BladeEdge> bladeCuts=new Dictionary<string,BladeEdge>();
  private readonly List<BladeEdge> bladeFading=new List<BladeEdge>();
  private readonly List<BladePoint> bladeTrail=new List<BladePoint>();
  private long bladeFadeAt=-1;

  private void BeginBlade(Point point){
   bladeSuppressUp=false;
   if(ViewOnly||_diagram==null||IsBlockPickerOpen)return;
   RememberGestureSelection();bladePending=true;bladeActive=false;
   bladeDocument=_diagram;bladeLast=point;Capture=true;
  }
  private void ActivateBlade(){
   // A right drag supersedes a pending +/Connect operation. Wait for the
   // drag threshold so a stationary right click keeps its connection menu.
   if(IsConnecting)CancelConnection(false);
   if(!bladePending||!ReferenceEquals(bladeDocument,_diagram))return;
   EnsureScene();bladeActive=true;Cursor=Cursors.Cross;_toolTip.SetToolTip(this,String.Empty);
   SetHoverNode(null);long now=manipulationClock.ElapsedMilliseconds;
   bladeTrail.Add(new BladePoint(bladeLast,now));
   foreach(var visual in _edgeVisuals){
    if(visual.Points==null||visual.Points.Count<2)continue;
    var model=FindEdge(visual.Id);if(model==null)continue;
    var bounds=visual.Bounds;bounds.Inflate(2/_zoom,2/_zoom);
    PointF top=WorldToClient(bounds.Location);if(!ClientRectangle.IntersectsWith(Rectangle.Ceiling(new RectangleF(top.X,top.Y,bounds.Width*_zoom,bounds.Height*_zoom))))continue;
    if(visual.HitPoints==null)using(var path=(GraphicsPath)visual.Path.Clone()){path.Flatten(null,.25f);visual.HitPoints=path.PathPoints;}
    var points=visual.HitPoints.ToArray();var lengths=new float[points.Length];
    for(int i=1;i<points.Length;i++)lengths[i]=lengths[i-1]+Distance(points[i-1],points[i]);
    if(points.Length<2||lengths[points.Length-1]<.01f)continue;
    bladeCandidates.Add(new BladeEdge{Model=model,Points=points,Lengths=lengths,Length=lengths[points.Length-1],Bounds=visual.Bounds,Color=BranchColor(FindNode(model.From),model.Branch)});
   }
   Report("Attraversa le frecce per tagliarle · Rilascia per confermare · Esc annulla.");
  }
  private bool MoveBlade(MouseEventArgs e){
   if(!bladePending)return false;
   if((e.Button&MouseButtons.Right)==0){CancelPointerGesture(true,true);return true;}
   if(!ReferenceEquals(bladeDocument,_diagram)){CancelPointerGesture(true,true);return true;}
   if(!bladeActive){if(!DragDisplaced(e.Location))return true;ActivateBlade();if(!bladeActive)return true;}
   Point previous=bladeLast;bladeLast=e.Location;
   if(previous==bladeLast)return true;
   PointF a=ClientToWorld(previous),b=ClientToWorld(bladeLast);long now=manipulationClock.ElapsedMilliseconds;
   var stroke=RectangleF.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X)+.01f,Math.Max(a.Y,b.Y)+.01f);stroke.Inflate(.1f,.1f);
   foreach(var edge in bladeCandidates){
    if(bladeCuts.ContainsKey(edge.Model.Id)||!stroke.IntersectsWith(edge.Bounds))continue;
    for(int i=1;i<edge.Points.Length;i++){
     PointF hit;float along;
     if(!BladeIntersection(a,b,edge.Points[i-1],edge.Points[i],out hit,out along)||!((RectangleF)ClientRectangle).Contains(WorldToClient(hit))||HitNode(hit)!=null)continue;
     edge.Hit=hit;edge.Cut=edge.Lengths[i-1]+Distance(edge.Points[i-1],edge.Points[i])*along;edge.At=now;
     bladeCuts.Add(edge.Model.Id,edge);MarkWorldDirty(edge.Bounds,60/_zoom);break;
    }
   }
   bladeTrail.Add(new BladePoint(e.Location,now));if(bladeTrail.Count>128)bladeTrail.RemoveAt(0);
   manipulationTimer.Start();RequestFrameRepaint();return true;
  }
  private bool EndBlade(MouseEventArgs e){
   if(e.Button!=MouseButtons.Right)return false;
   if(!bladePending){if(!bladeSuppressUp)return false;bladeSuppressUp=false;return true;}
   MoveBlade(e);
   bool active=bladeActive;var document=bladeDocument;
   var cuts=bladeCuts.Values.Where(c=>document!=null&&document.Edges.Contains(c.Model)).ToList();
   // Clear state before releasing capture, so CaptureChanged cannot undo a commit.
   ClearPointerState();Capture=false;bladeSuppressUp=false;Cursor=Cursors.Default;
   if(!active){ShowContextMenu(e.Location);return true;}
   if(cuts.Count==0||!ReferenceEquals(document,_diagram)){Report("Nessuna freccia attraversata.");return true;}
   RaiseDiagramChanging();
   if(!ReferenceEquals(document,_diagram))return true;
   var ids=new HashSet<string>(cuts.Where(c=>document.Edges.Contains(c.Model)).Select(c=>c.Model.Id));
   document.Edges.RemoveAll(edge=>ids.Contains(edge.Id));
   if(ids.Count==0)return true;
   if(_selectedEdgeId!=null&&ids.Contains(_selectedEdgeId))SetSelection(_selectedNodeIds,null);
   bladeFading.Clear();bladeFading.AddRange(cuts.Where(c=>ids.Contains(c.Model.Id)));bladeFadeAt=manipulationClock.ElapsedMilliseconds;
   RaiseDiagramChanged();manipulationTimer.Start();
   Report(ids.Count==1?"Collegamento tagliato · Caselle conservate · Ctrl+Z ripristina.":ids.Count+" collegamenti tagliati · Caselle conservate · Ctrl+Z ripristina tutto il gesto.");
   return true;
  }
  private void ClearBladeGesture(){
   if(bladePending)bladeSuppressUp=true;
   foreach(var cut in bladeCuts.Values)MarkWorldDirty(cut.Bounds,60/_zoom);
   bladeCuts.Clear();bladeCandidates.Clear();bladePending=bladeActive=false;bladeDocument=null;
  }
  private void StopBladeFeedback(){ClearBladeGesture();bladeTrail.Clear();bladeFading.Clear();bladeFadeAt=-1;}
  private bool TickBlade(long now){
   if(!UiMotion.Enabled){bladeTrail.Clear();bladeFading.Clear();bladeFadeAt=-1;return false;}
   // Preserve the predecessor for the oldest fading segment, but bound all data.
   while(bladeTrail.Count>1&&now-bladeTrail[1].At>220)bladeTrail.RemoveAt(0);
   if(bladeTrail.Count==1&&now-bladeTrail[0].At>220)bladeTrail.Clear();
   if(bladeFadeAt>=0&&now-bladeFadeAt>=320){bladeFading.Clear();bladeFadeAt=-1;}
   return bladeTrail.Count>0||bladeFadeAt>=0||bladeCuts.Values.Any(c=>now-c.At<180);
  }
  private bool HideBladeEdge(string id){return !_exportRendering&&bladeCuts.ContainsKey(id);}
  private void PaintBladeEdges(Graphics g){
   long now=manipulationClock.ElapsedMilliseconds;
   foreach(var cut in bladeCuts.Values){
    DrawBladeEdge(g,cut,UiMotion.Enabled?UiMotion.Spring(Math.Min(1,(now-cut.At)/180f)):1,1);
    if(cut.Model.Branch=="true"||cut.Model.Branch=="false")DrawBranchLabel(g,cut.Points,FindNode(cut.Model.From),cut.Model.Branch,new RectangleF(ClientToWorld(Point.Empty),new SizeF(ClientSize.Width/_zoom,ClientSize.Height/_zoom)));
   }
   if(bladeFadeAt>=0&&UiMotion.Enabled){float t=Math.Min(1,(now-bladeFadeAt)/320f);foreach(var cut in bladeFading)DrawBladeEdge(g,cut,1+2*UiMotion.Spring(t),1-t*t*(3-2*t));}
  }
  private void DrawBladeEdge(Graphics g,BladeEdge edge,float progress,float alpha){
   float gap=(4+10*Math.Max(0,progress))/_zoom;
   using(var pen=new Pen(Color.FromArgb(Math.Max(0,Math.Min(255,(int)(255*alpha))),edge.Color),appearance.Width(1.8f)/Math.Max(.7f,_zoom))){
    pen.StartCap=pen.EndCap=LineCap.Round;
    DrawBladeRange(g,pen,edge,0,edge.Cut-gap);DrawBladeRange(g,pen,edge,edge.Cut+gap,edge.Length);
   }
   if(edge.Cut+gap<edge.Length)DrawArrow(g,edge.Points[edge.Points.Length-2],edge.Points[edge.Points.Length-1],Color.FromArgb(Math.Max(0,Math.Min(255,(int)(255*alpha))),edge.Color),8/Math.Max(.7f,_zoom));
  }
  private static void DrawBladeRange(Graphics g,Pen pen,BladeEdge edge,float from,float to){
   from=Math.Max(0,from);to=Math.Min(edge.Length,to);if(to<=from)return;
   var points=new List<PointF>();
   for(int i=1;i<edge.Points.Length;i++){
    float lo=edge.Lengths[i-1],hi=edge.Lengths[i];if(hi<=from||lo>=to||hi-lo<.0001f)continue;
    var a=edge.Points[i-1];var b=edge.Points[i];float start=Math.Max(0,(from-lo)/(hi-lo)),end=Math.Min(1,(to-lo)/(hi-lo));
    if(points.Count==0)points.Add(new PointF(a.X+(b.X-a.X)*start,a.Y+(b.Y-a.Y)*start));
    points.Add(new PointF(a.X+(b.X-a.X)*end,a.Y+(b.Y-a.Y)*end));
   }
   if(points.Count>1)g.DrawLines(pen,points.ToArray());
  }
  private void PaintBladeTrail(Graphics g){
   if(!UiMotion.Enabled)return;long now=manipulationClock.ElapsedMilliseconds;
   for(int i=1;i<bladeTrail.Count;i++){
    float life=Math.Max(0,1-(now-bladeTrail[i].At)/220f);if(life<=0)continue;
    using(var glow=new Pen(Color.FromArgb((int)(65*life),Ui.Blue),9*life)){glow.StartCap=glow.EndCap=LineCap.Round;g.DrawLine(glow,bladeTrail[i-1].Point,bladeTrail[i].Point);}
    using(var core=new Pen(Color.FromArgb((int)(245*life),Ui.IsDark?Color.FromArgb(220,240,255):Ui.Blue),Math.Max(1,2.5f*life))){core.StartCap=core.EndCap=LineCap.Round;g.DrawLine(core,bladeTrail[i-1].Point,bladeTrail[i].Point);}
   }
   foreach(var cut in bladeCuts.Values.Concat(bladeFading)){
    float t=(now-cut.At)/280f;if(t<0||t>=1)continue;var p=WorldToClient(cut.Hit);float r=5+15*UiMotion.Spring(t);
    using(var pen=new Pen(Color.FromArgb((int)(230*(1-t)),cut.Color),1.6f))for(int i=0;i<4;i++){double a=Math.PI*(i/2.0+.25);float x=(float)Math.Cos(a),y=(float)Math.Sin(a);g.DrawLine(pen,p.X+x*r*.45f,p.Y+y*r*.45f,p.X+x*r,p.Y+y*r);}
   }
  }
  // True crossings, including endpoints, of the same flattened rounded route
  // that is painted. Running alongside a line is not a cutting gesture.
  private static bool BladeIntersection(PointF a,PointF b,PointF c,PointF d,out PointF hit,out float along){
   hit=PointF.Empty;along=0;double rx=b.X-a.X,ry=b.Y-a.Y,sx=d.X-c.X,sy=d.Y-c.Y,den=rx*sy-ry*sx;
   if(Math.Abs(den)<.000001)return false;
   double dx=c.X-a.X,dy=c.Y-a.Y,t=(dx*sy-dy*sx)/den,u=(dx*ry-dy*rx)/den;
   if(t<-.00001||t>1.00001||u<-.00001||u>1.00001)return false;
   along=(float)Math.Max(0,Math.Min(1,u));hit=new PointF((float)(a.X+t*rx),(float)(a.Y+t*ry));return true;
  }
 }
}
