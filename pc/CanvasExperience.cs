using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
 public partial class FlowCanvas {
    private readonly Stopwatch flowClock=Stopwatch.StartNew();
    private PointF[] flowPoints;
    private GraphicsPath flowPath;
    private string flowEdge;
    private int flowDuration=470;
    private bool flowAnimating;
    private readonly HashSet<string> flowHistory=new HashSet<string>();
    private string rewireEdge;
    private bool rewireMoved;
    private Point rewireOrigin;
    [System.ComponentModel.DefaultValue(470)]
    public int FlowAnimationDuration { get { return flowDuration; } set { flowDuration=Math.Max(65,Math.Min(1500,value)); } }
    internal float FlowProgress { get { return !UiMotion.Enabled||!flowAnimating?1:Math.Min(1,(float)(flowClock.ElapsedTicks*1000.0/Stopwatch.Frequency/flowDuration)); } }
    internal bool HasFlowAnimation { get { return flowAnimating&&FlowProgress<1; } }
    private void BeginFlowTravel(string id) {
        if(!String.IsNullOrEmpty(flowEdge)&&flowEdge!=id)flowHistory.Add(flowEdge);
        flowEdge=id;flowPoints=null;flowPath=null;
        // Repeated loop edges start empty instead of revealing the completed backing route.
        if(id!=null)flowHistory.Remove(id);
        EnsureScene();EdgeVisual visual;
        if(id!=null&&_edgeIndex.TryGetValue(id,out visual)&&visual.Path!=null){
            using(var flat=(GraphicsPath)visual.Path.Clone()){flat.Flatten(null,.25f);flowPoints=flat.PathPoints;}flowPath=visual.Path;
            double distance=0;for(int i=1;i<flowPoints.Length;i++)distance+=ExperienceDistance(flowPoints[i-1],flowPoints[i]);
            flowDuration=Math.Max(flowDuration,Math.Min(3000,(int)Math.Ceiling(distance*_zoom/650.0*1000)));
        }
        InvalidateEdge(id);flowClock.Restart();flowAnimating=UiMotion.Enabled&&!String.IsNullOrEmpty(id);UpdatePulseTimer();
    }
    public void AnimateFlowStep(string edgeId,int milliseconds){FlowAnimationDuration=milliseconds;if(_currentEdgeId==edgeId)BeginFlowTravel(edgeId);else CurrentEdgeId=edgeId;}
    private void ClearFlowHistory(){foreach(string id in flowHistory)InvalidateEdge(id);flowHistory.Clear();flowEdge=null;flowPoints=null;flowPath=null;flowAnimating=false;}
    private void TickExperience() {
        if(!Visible||!UiMotion.Enabled){flowAnimating=false;_pulseTimer.Stop();return;}
        RequestFrameRepaint();if(flowClock.ElapsedMilliseconds>=flowDuration+120){flowAnimating=false;_pulseTimer.Stop();}
    }
    private void PaintExperience(Graphics g) {
        if(!String.IsNullOrEmpty(_currentEdgeId)) {
            EnsureScene();EdgeVisual edge;
            if(_edgeIndex.TryGetValue(_currentEdgeId,out edge)&&edge.Path!=null) {
                if(flowPoints==null||flowEdge!=_currentEdgeId||!Object.ReferenceEquals(flowPath,edge.Path)) {using(var flat=(GraphicsPath)edge.Path.Clone()){flat.Flatten(null,.25f);flowPoints=flat.PathPoints;}flowEdge=_currentEdgeId;flowPath=edge.Path;}
                float progress=FlowProgress;List<PointF> prefix=Prefix(flowPoints,progress);
                using(var faint=new Pen(Color.FromArgb(27,Ui.Blue),7/Math.Max(.6f,_zoom))){faint.StartCap=faint.EndCap=LineCap.Round;g.DrawPath(faint,edge.Path);}
                if(prefix.Count>=2)using(var p=new Pen(Ui.Blue,3.6f/Math.Max(.6f,_zoom)){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(p,prefix.ToArray());
                if(prefix.Count>0&&progress<1){var head=prefix[prefix.Count-1];float r=4/_zoom;using(var b=new SolidBrush(Ui.Blue))g.FillEllipse(b,head.X-r,head.Y-r,r*2,r*2);}
                if(progress>=1&&edge.Points.Count>=2)DrawArrow(g,edge.Points[edge.Points.Count-2],edge.Points[edge.Points.Count-1],Ui.Blue,8/Math.Max(.8f,_zoom));
                FlowNode arrival=FindNode(edge.Model.To);float fill=UiMotion.Enabled?Math.Max(0,Math.Min(1,(flowClock.ElapsedMilliseconds-flowDuration*.72f)/(flowDuration*.28f+120))):1;
                if(arrival!=null&&fill>0){var bounds=DesignBounds(arrival);var state=g.Save();using(var shape=NodeShape(arrival.Kind,bounds))g.SetClip(shape,CombineMode.Intersect);g.IntersectClip(new RectangleF(bounds.Left,bounds.Bottom-bounds.Height*fill,bounds.Width,bounds.Height*fill));using(var b=new SolidBrush(Color.FromArgb(Dark?58:36,Ui.Blue)))g.FillRectangle(b,bounds);g.Restore(state);}
                if(edge.Model.Branch=="true"||edge.Model.Branch=="false")DrawBranchLabel(g,edge.Points,FindNode(edge.Model.From),edge.Model.Branch,new RectangleF(-100000,-100000,200000,200000));
            }
        }
        PaintDecisionHandles(g);if(rewireEdge!=null)PaintRewire(g);
    }
    internal static List<PointF> Prefix(PointF[] points,float progress) {
        var result=new List<PointF>();if(points==null||points.Length==0)return result;result.Add(points[0]);
        double length=0;for(int i=1;i<points.Length;i++)length+=ExperienceDistance(points[i-1],points[i]);double remaining=length*Math.Max(0,Math.Min(1,progress));
        for(int i=1;i<points.Length;i++){double segment=ExperienceDistance(points[i-1],points[i]);if(segment<.001)continue;if(remaining>=segment){result.Add(points[i]);remaining-=segment;}else{float t=(float)(remaining/segment);result.Add(new PointF(points[i-1].X+(points[i].X-points[i-1].X)*t,points[i-1].Y+(points[i].Y-points[i-1].Y)*t));break;}}
        return result;
    }
    private static double ExperienceDistance(PointF a,PointF b){double x=b.X-a.X,y=b.Y-a.Y;return Math.Sqrt(x*x+y*y);}
    private PointF ArrowHandle(EdgeVisual edge) {
        if(_diagram.Edges.Any(e=>e.From==edge.Model.From&&e.To==edge.Model.To&&e.Branch!=edge.Model.Branch)){
            var arrival=FindNode(edge.Model.To);if(arrival!=null){var b=DisplayBounds(arrival);return new PointF(b.Left+b.Width/2+(edge.Model.Branch=="true"?-18:18)/Math.Max(.1f,_zoom),b.Top-18/Math.Max(.1f,_zoom));}
        }
        var p=edge.Points;PointF end=p[p.Count-1],before=p[p.Count-2];double length=ExperienceDistance(before,end);float offset=Math.Min(12/Math.Max(.4f,_zoom),(float)length*.4f);
        return length<.01?end:new PointF(end.X+(before.X-end.X)*offset/(float)length,end.Y+(before.Y-end.Y)*offset/(float)length);
    }
    private bool DecisionHandleVisible(EdgeVisual edge){FlowNode source=FindNode(edge.Model.From);return !ViewOnly&&!IsPointerGestureActive&&source!=null&&edge.Points!=null&&edge.Points.Count>=2&&(edge.Model.Id==_selectedEdgeId||source.Kind==NodeKind.Decision&&_selectedNodeIds.Contains(source.Id));}
    private void PaintDecisionHandles(Graphics g) {
        EnsureScene();foreach(var edge in _edgeVisuals)if(DecisionHandleVisible(edge)){
            PointF point=ArrowHandle(edge);float radius=7.5f/Math.Max(.4f,_zoom);Color color=edge.Model.Branch=="next"?Ui.Blue:BranchLabelColor(FindNode(edge.Model.From),edge.Model.Branch);using(var b=new SolidBrush(Ui.Surface))g.FillEllipse(b,point.X-radius,point.Y-radius,radius*2,radius*2);using(var p=new Pen(color,1.8f/Math.Max(.5f,_zoom))){g.DrawEllipse(p,point.X-radius,point.Y-radius,radius*2,radius*2);g.DrawLine(p,point.X-radius*.43f,point.Y,point.X+radius*.43f,point.Y);g.DrawLine(p,point.X,point.Y-radius*.43f,point.X,point.Y+radius*.43f);}
        }
    }
    private bool TryBeginRewire(MouseEventArgs e) {
        if(_diagram==null||e.Button!=MouseButtons.Left)return false;EnsureScene();PointF world=ClientToWorld(e.Location);
        foreach(var edge in _edgeVisuals.OrderByDescending(x=>x.Model.Id==_selectedEdgeId))if(DecisionHandleVisible(edge)&&ExperienceDistance(world,ArrowHandle(edge))<=11/Math.Max(.4f,_zoom)){
            RememberGestureSelection();SetSelection(Enumerable.Empty<string>(),edge.Model.Id);rewireEdge=edge.Model.Id;rewireOrigin=e.Location;rewireMoved=false;Capture=true;Cursor=Cursors.Cross;RequestFrameRepaint();Report("Trascina la punta su un altro blocco. Esc annulla senza cambiare il collegamento.");return true;
        }return false;
    }
    private bool MoveRewire(MouseEventArgs e){if(rewireEdge==null)return false;rewireMoved|=Math.Abs(e.X-rewireOrigin.X)+Math.Abs(e.Y-rewireOrigin.Y)>4;FlowNode target=HitNode(ClientToWorld(e.Location));Cursor=target!=null&&(target.Kind==NodeKind.Start||target.Kind==NodeKind.Note)?Cursors.No:Cursors.Cross;RequestFrameRepaint();return true;}
    private bool EndRewire(MouseEventArgs e) {
        if(rewireEdge==null||e.Button!=MouseButtons.Left)return false;var document=_diagram;FlowEdge edge=document.Edges.FirstOrDefault(x=>x.Id==rewireEdge);FlowNode target=HitNode(ClientToWorld(e.Location));bool moved=rewireMoved;rewireEdge=null;rewireMoved=false;ClearPointerState();Capture=false;Cursor=Cursors.Default;
        if(moved&&edge!=null&&target==null){BeginConnect(edge.From,edge.Branch);ShowBlockPicker(e.Location);Report("Scegli una casella da inserire in questo ramo. Chiudi per mantenere il collegamento.");}
        else if(moved&&edge!=null&&target!=null&&target.Kind!=NodeKind.Start&&target.Kind!=NodeKind.Note&&target.Id!=edge.To){RaiseDiagramChanging();if(ReferenceEquals(document,_diagram)){edge.To=target.Id;RaiseDiagramChanged();SelectEdge(edge.Id);Report("Freccia ricollegata. Annulla ripristina la destinazione precedente.");}}
        else Report(moved?"Collegamento precedente mantenuto. Rilascia su un blocco valido.":"Freccia selezionata. Trascina il punto blu per cambiare la destinazione.");
        RequestFrameRepaint();return true;
    }
    private void PaintRewire(Graphics g) {
        FlowEdge edge=_diagram==null?null:_diagram.Edges.FirstOrDefault(x=>x.Id==rewireEdge);if(edge==null)return;FlowNode source=FindNode(edge.From),target=HitNode(ClientToWorld(_lastMouse));if(source==null)return;
        List<PointF> points;if(target!=null&&target.Kind!=NodeKind.Start&&target.Kind!=NodeKind.Note)points=RouteEdge(source,target,edge.Branch);else{PointF start=OutputPoint(source,edge.Branch),end=ClientToWorld(_lastMouse);points=CleanPoints(new List<PointF>{start,new PointF(start.X+(edge.Branch=="true"?-28:28),start.Y),end});}
        using(var path=RoundedRoute(points,10))using(var p=new Pen(Ui.Blue,2.5f/Math.Max(.5f,_zoom)){DashStyle=DashStyle.Dash,StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawPath(p,path);
        if(target!=null&&target.Kind!=NodeKind.Start&&target.Kind!=NodeKind.Note)using(var shape=NodeShape(target.Kind,DesignBounds(target)))using(var p=new Pen(Ui.Blue,2/_zoom))g.DrawPath(p,shape);
    }
 }
}

