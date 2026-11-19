using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
namespace Frociart {
 public partial class FlowCanvas {
  private FlowGroupPlacement.Plan groupInsertPlan;
  private readonly HashSet<string> landingGroup=new HashSet<string>();
  private bool GroupDragging {get{return paletteNode==null&&_dragOrigins!=null&&_dragOrigins.Count>1;}}
  private bool HeldNode(FlowNode n){return holdStart>=0&&_dragOrigins!=null&&_dragOrigins.ContainsKey(n.Id);}
  private bool LandingNode(FlowNode n){return landingId==n.Id||landingId!=null&&landingGroup.Contains(n.Id);}
  private bool BeginGroupLift(){
   if(!GroupDragging)return false;
   if(!FlowGroupPlacement.Eligible(_diagram,_dragOrigins.Keys)){Report("Il gruppo si sposta insieme. Escludi Inizio e Fine per staccarlo.");return true;}
   stickerDetached=true;insertionDocument=DiagramIO.Clone(_diagram);FlowGroupPlacement.Detach(insertionDocument,_dragOrigins.Keys);
   blockedBypass.Clear();bypassExited=false;foreach(var edge in _diagram.Edges.Where(e=>!_dragOrigins.ContainsKey(e.From)&&_dragOrigins.ContainsKey(e.To)))blockedBypass.Add(edge.Id);
   CaptureInsertRoutes();Report(_dragOrigins.Count+" blocchi sollevati insieme. I collegamenti interni restano intatti."+(groupInsertPlan!=null&&groupInsertPlan.Error!=null?" "+groupInsertPlan.Error:" Trascina su una freccia per reinserirli."));return true;
  }
  private void CommitGroupDrag(){
   if(_dragMoved)UpdateDragPosition(_lastMouse,false);var before=PresentationPositions();var animation=ModelPositions();
   if(_dragMoved)UpdateDragPosition(_lastMouse,true);UpdateInsertPreview();string edgeId=insertReady&&insertHover!=null?insertHover.Id:null;
   string anchorId=_dragNode.Id;var origins=_dragOrigins;var ids=new HashSet<string>(origins.Keys);var final=ModelPositions();var expected=_diagram;bool peel=liftStart>=0&&FlowGroupPlacement.Eligible(_diagram,ids),moved=ids.Any(id=>final[id]!=origins[id]);
   var route=insertHover;ApplyOffset(origins,PointF.Empty);ClearPointerState();Capture=false;
   if(!moved&&!peel&&edgeId==null){RequestFrameRepaint();return;}
   var staged=DiagramIO.Clone(expected);foreach(var n in staged.Nodes.Where(n=>ids.Contains(n.Id))){n.X=final[n.Id].X;n.Y=final[n.Id].Y;}
   if(peel||edgeId!=null)FlowGroupPlacement.Detach(staged,ids);
   if(edgeId!=null){
    var edge=staged.Edges.FirstOrDefault(e=>e.Id==edgeId);if(edge==null){Report("Freccia non più disponibile. Nessuna modifica applicata.");return;}
    // Snap the entire group with one common offset; no block jumps independently.
    var anchor=staged.Nodes.First(n=>n.Id==anchorId);
    var ab=FlowCanvas.GetNodeBounds(anchor);var center=new PointF(ab.Left+ab.Width/2,ab.Top+ab.Height/2);var nearest=center;float best=float.MaxValue;
    for(int i=1;i<route.Points.Length;i++){var a=route.Points[i-1];var b=route.Points[i];float dx=b.X-a.X,dy=b.Y-a.Y,l=dx*dx+dy*dy,t=l<.001f?0:Math.Max(0,Math.Min(1,((center.X-a.X)*dx+(center.Y-a.Y)*dy)/l));var p=new PointF(a.X+t*dx,a.Y+t*dy);float distance=(p.X-center.X)*(p.X-center.X)+(p.Y-center.Y)*(p.Y-center.Y);if(distance<best){best=distance;nearest=p;}}
    foreach(var n in staged.Nodes.Where(n=>ids.Contains(n.Id))){n.X+=nearest.X-center.X;n.Y+=nearest.Y-center.Y;}
    if(!FlowGroupPlacement.Arrange(staged,ids,edge.From,edge.To)||!FlowGroupPlacement.Insert(staged,ids,edgeId,ReservedEdges)){Report("Questo gruppo non può essere inserito qui. Posizione e collegamenti originali conservati.");RequestFrameRepaint();return;}
   }
   RaiseDiagramChanging();if(!ReferenceEquals(expected,_diagram))return;
   foreach(var n in _diagram.Nodes){var next=staged.Nodes.First(x=>x.Id==n.Id);n.X=next.X;n.Y=next.Y;}_diagram.Edges.Clear();_diagram.Edges.AddRange(staged.Edges);
   RaiseDiagramChanged();StartLanding(ids.First(),peel);landingGroup.Clear();landingGroup.UnionWith(ids);AnimatePlacement(before);
   Report(edgeId!=null?ids.Count+" blocchi inseriti in ordine. Ctrl+Z ripristina l’intero gesto.":peel?"Gruppo staccato; collegamenti interni conservati. Ctrl+Z ripristina tutto.":"Gruppo spostato; collegamenti conservati.");
  }
 }
}
