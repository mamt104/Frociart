using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace Frociart {
 public partial class FlowCanvas {
  private sealed class InsertRoute:IDisposable{
   internal BranchRoutingContext Context;internal int Generation=-1;internal string Id,From,To,Branch;internal PointF[] Points;internal GraphicsPath Path;
   public void Dispose(){if(Path!=null)Path.Dispose();}
  }
  private readonly List<InsertRoute> insertRoutes=new List<InsertRoute>();
  private readonly HashSet<string> blockedBypass=new HashSet<string>();private bool bypassExited;
  private InsertRoute insertHover;private float insertStrength,insertDistance;private bool insertReady;
  private void ClearInsertPreview(){foreach(var route in insertRoutes)route.Dispose();insertRoutes.Clear();insertHover=null;insertStrength=0;insertReady=false;}
  private void CaptureInsertRoutes(){
   ClearInsertPreview();groupInsertPlan=null;if(_dragNode==null)return;EnsureScene();
   if(GroupDragging){var staged=insertionDocument??DiagramIO.Clone(_diagram);if(!FlowGroupPlacement.Detach(staged,_dragOrigins.Keys))return;groupInsertPlan=FlowGroupPlacement.Analyze(staged,_dragOrigins.Keys);var context=BuildBranchRoutes(staged);foreach(var edge in staged.Edges)if(FlowGroupPlacement.Reason(staged,groupInsertPlan,edge,ReservedEdges)==null){var a=FindNode(edge.From);var b=FindNode(edge.To);if(a==null||b==null)continue;using(var path=RoundedRoute(RouteEdge(a,b,edge.Branch,context),10))using(var flat=(GraphicsPath)path.Clone()){flat.Flatten(null,.4f);insertRoutes.Add(new InsertRoute{Context=context,Id=edge.Id,From=edge.From,To=edge.To,Branch=edge.Branch,Points=flat.PathPoints,Path=(GraphicsPath)path.Clone()});}}return;}
   var singleContext=BuildBranchRoutes(insertionDocument??_diagram);foreach(var model in (insertionDocument??_diagram).Edges)if(model.From!=_dragNode.Id&&model.To!=_dragNode.Id&&FlowInsertion.Reason(insertionDocument??_diagram,_dragNode.Id,model.Id,ReservedEdges)==null){
    var from=FindNode(model.From);var to=FindNode(model.To);if(from==null||to==null)continue;
    using(var path=RoundedRoute(RouteEdge(from,to,model.Branch,singleContext),10))using(var flat=(GraphicsPath)path.Clone()){flat.Flatten(null,.4f);insertRoutes.Add(new InsertRoute{Context=singleContext,Id=model.Id,From=model.From,To=model.To,Branch=model.Branch,Points=flat.PathPoints,Path=(GraphicsPath)path.Clone()});}
   }
  }
  private void RefreshInsertRoutes(){
   // Geometry can reroute while the dragged block crosses a return corridor.
   // Keep the target and glow on the very same current route as the arrow.
   EnsureScene();foreach(var route in insertRoutes){var from=FindNode(route.From);var to=FindNode(route.To);if(from==null||to==null)continue;
    if(route.Generation==branchGeometryGeneration)continue;route.Generation=branchGeometryGeneration;
    using(var path=RoundedRoute(RouteEdge(from,to,route.Branch,route.Context),10))using(var flat=(GraphicsPath)path.Clone()){
     flat.Flatten(null,.4f);var points=flat.PathPoints;if(route.Points.SequenceEqual(points))continue;
     route.Dispose();route.Path=(GraphicsPath)path.Clone();route.Points=points;
    }
   }
  }

  private static float InsertSegmentDistance(PointF p,PointF a,PointF b){float dx=b.X-a.X,dy=b.Y-a.Y,l=dx*dx+dy*dy;float t=l<.001f?0:Math.Max(0,Math.Min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/l));float x=p.X-a.X-t*dx,y=p.Y-a.Y-t*dy;return (float)Math.Sqrt(x*x+y*y);}
  private void UpdateInsertPreview(){
   if(TrashReady&&TrashBounds().Contains(_lastMouse)){insertHover=null;insertStrength=0;insertReady=false;return;}
   if(_dragNode==null||!_dragging||paletteNode==null&&!_dragMoved){insertHover=null;insertStrength=0;insertReady=false;return;}
   RefreshInsertRoutes();
   var bounds=DesignBounds(_dragNode);var center=new PointF(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);
   float influence=90/Math.Max(.1f,_zoom),best=influence;InsertRoute found=null;
   if(blockedBypass.Count>0&&!bypassExited){float distance=float.MaxValue;foreach(var route in insertRoutes.Where(r=>blockedBypass.Contains(r.Id)))for(int i=1;i<route.Points.Length;i++)distance=Math.Min(distance,InsertSegmentDistance(center,route.Points[i-1],route.Points[i]));bypassExited=distance>38/Math.Max(.1f,_zoom)||Math.Sqrt(Math.Pow(_lastMouse.X-_mouseDown.X,2)+Math.Pow(_lastMouse.Y-_mouseDown.Y,2))>50;}
   foreach(var route in insertRoutes){if(!bypassExited&&blockedBypass.Contains(route.Id))continue;for(int i=1;i<route.Points.Length;i++){float distance=InsertSegmentDistance(center,route.Points[i-1],route.Points[i]);if(distance<best){best=distance;found=route;}}}
   bool previous=insertReady;insertHover=found;insertDistance=best;insertStrength=found==null?0:Math.Max(0,1-best/influence);insertReady=found!=null&&best<=22/Math.Max(.1f,_zoom);
   if(previous!=insertReady)Report(insertReady?GroupDragging?"Rilascia per inserire tutti i blocchi in ordine. Ctrl+Z annulla l’intero gesto.":"Rilascia per inserire il blocco nella freccia. Ctrl+Z annulla l’intero gesto.":"Trascina il blocco vicino a una freccia: il blu indica dove puoi inserirlo.");
  }
  private void PaintInsertPreview(Graphics g){
   if(!_dragging||insertHover==null||insertStrength<=0||_dragNode==null)return;
   var bounds=DesignBounds(_dragNode);var center=new PointF(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);
   float radius=(float)Math.Sqrt(bounds.Width*bounds.Width+bounds.Height*bounds.Height)/2+48/Math.Max(.1f,_zoom)*(.4f+.6f*insertStrength);
   var state=g.Save();g.IntersectClip(new RectangleF(center.X-radius-24/_zoom,center.Y-radius-24/_zoom,2*radius+48/_zoom,2*radius+48/_zoom));
   float[] widths={22,12,5,2.4f};int[] alphas={25,48,105,235};
   try{for(int i=0;i<widths.Length;i++)using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
     var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix00=Ui.Blue.R/255f;matrix.Matrix11=Ui.Blue.G/255f;matrix.Matrix22=Ui.Blue.B/255f;matrix.Matrix33=alphas[i]/255f*insertStrength;attributes.SetColorMatrix(matrix);
     using(var brush=new TextureBrush(insertGlowTile,new Rectangle(0,0,insertGlowTile.Width,insertGlowTile.Height),attributes))using(var transform=new Matrix(2*radius/insertGlowTile.Width,0,0,2*radius/insertGlowTile.Height,center.X-radius,center.Y-radius)){
      brush.WrapMode=WrapMode.Clamp;brush.Transform=transform;
      using(var pen=new Pen(brush,widths[i]/Math.Max(.2f,_zoom)){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawPath(pen,insertHover.Path);
     }
   }}finally{g.Restore(state);}
  }
  private readonly Bitmap insertGlowTile=CreateInsertGlowTile();
  private static Bitmap CreateInsertGlowTile(){var image=new Bitmap(128,128,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){double dx=(x-63.5)/63.5,dy=(y-63.5)/63.5;int alpha=(int)(255*Math.Max(0,1-Math.Sqrt(dx*dx+dy*dy)));image.SetPixel(x,y,Color.FromArgb(alpha,Color.White));}return image;}

  private void PaintInsertHint(Graphics g){
   if(!_dragging||insertHover==null||insertStrength<.1f)return;
   string text=insertReady?(GroupDragging?"Rilascia per inserire il gruppo":"Rilascia per inserire"):"Avvicina il blocco alla freccia";
   var size=TextRenderer.MeasureText(text,Font);var nodeBounds=DesignBounds(_dragNode);var above=WorldToClient(new PointF(nodeBounds.Left+nodeBounds.Width/2,nodeBounds.Top));var below=WorldToClient(new PointF(nodeBounds.Left,nodeBounds.Bottom));
   int y=(int)above.Y-size.Height-26;if(y<8)y=(int)below.Y+12;
   var at=new Point(Math.Max(8,Math.Min(ClientSize.Width-size.Width-24,(int)above.X-size.Width/2-10)),Math.Max(8,Math.Min(ClientSize.Height-size.Height-22,y)));
   var rect=new Rectangle(at.X,at.Y,size.Width+20,size.Height+14);using(var fill=new SolidBrush(Ui.Surface))g.FillRectangle(fill,rect);using(var pen=new Pen(Ui.Blue))g.DrawRectangle(pen,rect);
   TextRenderer.DrawText(g,text,Font,new Point(at.X+10,at.Y+7),Ui.Blue);
  }
  private string InsertionBranch(FlowNode n,string edgeId,Diagram context=null){
   if(n.Kind!=NodeKind.Decision)return "next";var document=context??_diagram;var edge=document.Edges.FirstOrDefault(x=>x.Id==edgeId);if(edge==null)return null;
   var target=document.Nodes.FirstOrDefault(x=>x.Id==edge.To);return BranchContinuationDialog.Choose(FindForm(),n,"Quale ramo continua verso "+(target==null?"il blocco successivo":FriendlyNodeText(target))+"?\nScegli un percorso oppure lascia libere entrambe le uscite.","Inserisci la decisione",keepPlacementOnClose:true);
  }
 }
}
