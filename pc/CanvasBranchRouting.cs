using System;
using System.Collections.Generic;
using System.Drawing;

namespace Frociart
{
    public partial class FlowCanvas
    {
        private sealed class DecisionRoutes
        {
            internal FlowNode Yes, No;
            internal int Generation=-1;
            internal List<PointF> YesPoints, NoPoints;
        }
        private sealed class BranchRoutingContext
        {
            internal readonly Dictionary<string,DecisionRoutes> Pairs=new Dictionary<string,DecisionRoutes>();
        }
        private BranchRoutingContext branchRoutes=new BranchRoutingContext();
        private int branchGeometryGeneration,branchTopologyGeneration;

        private static BranchRoutingContext BuildBranchRoutes(Diagram scene)
        {
            var result=new BranchRoutingContext();var index=new Dictionary<string,FlowNode>();
            foreach(var node in scene.Nodes)if(node.Id!=null)index[node.Id]=node;
            foreach(var edge in scene.Edges)
            {
                FlowNode source,target;
                if((edge.Branch!="true"&&edge.Branch!="false")||!index.TryGetValue(edge.From??"",out source)||
                    source.Kind!=NodeKind.Decision||!index.TryGetValue(edge.To??"",out target))continue;
                DecisionRoutes pair;
                if(!result.Pairs.TryGetValue(source.Id,out pair)){pair=new DecisionRoutes();result.Pairs.Add(source.Id,pair);}
                if(edge.Branch=="true")pair.Yes=target;else pair.No=target;
            }
            return result;
        }
        private FlowNode DecisionPeer(FlowNode source,string branch)
        {
            DecisionRoutes pair;
            if(source==null||source.Kind!=NodeKind.Decision||!branchRoutes.Pairs.TryGetValue(source.Id,out pair))return null;
            return branch=="true"?pair.No:branch=="false"?pair.Yes:null;
        }
        private static float[] RouteCoordinates(List<PointF> points)
        {
            var result=new float[points.Count*2];for(int i=0;i<points.Count;i++){result[i*2]=points[i].X;result[i*2+1]=points[i].Y;}return result;
        }
        private static float[] BoxCoordinates(RectangleF b){return new float[]{b.Left,b.Top,b.Right,b.Bottom};}
        private static List<PointF> RoutePoints(float[] points)
        {
            var result=new List<PointF>();for(int i=0;i<points.Length;i+=2)result.Add(new PointF(points[i],points[i+1]));return result;
        }
        private float[][] BranchObstacles(FlowNode source,FlowNode yes,FlowNode no,List<PointF> a,List<PointF> b)
        {
            var region=RectangleF.Union(DisplayBounds(source),RectangleF.Union(DisplayBounds(yes),DisplayBounds(no)));
            region=RectangleF.Union(region,RectangleF.Union(BoundsOf(a),BoundsOf(b)));region.Inflate(80,80);
            var result=new List<float[]>();
            foreach(var node in _nodeVisuals)
            {
                if(node.Model.Id==source.Id||node.Model.Id==yes.Id||node.Model.Id==no.Id||node.Kind==NodeKind.Note)continue;
                if(_dragging&&(_dragOrigins!=null&&_dragOrigins.ContainsKey(node.Model.Id)||_dragNode!=null&&_dragNode.Id==node.Model.Id))continue;
                if(region.IntersectsWith(node.Bounds))result.Add(BoxCoordinates(node.Bounds));
            }
            return result.ToArray();
        }
        private List<PointF> RouteEdge(FlowNode source,FlowNode target,string branch)
        {
            return RouteEdge(source,target,branch,branchRoutes);
        }
        private List<PointF> RouteEdge(FlowNode source,FlowNode target,string branch,BranchRoutingContext context)
        {
            DecisionRoutes pair;
            if(source.Kind!=NodeKind.Decision||(branch!="true"&&branch!="false")||context==null||
                !context.Pairs.TryGetValue(source.Id,out pair))return BaseRouteEdge(source,target,branch);
            NodeVisual yesVisual,noVisual;
            FlowNode yes=branch=="true"?target:pair.Yes,no=branch=="false"?target:pair.No;
            if(yes!=null&&_nodeIndex.TryGetValue(yes.Id,out yesVisual))yes=yesVisual.Model;
            if(no!=null&&_nodeIndex.TryGetValue(no.Id,out noVisual))no=noVisual.Model;
            if(yes==null||no==null||yes.Id==no.Id)return BaseRouteEdge(source,target,branch);
            bool stored=pair.Yes!=null&&pair.No!=null&&yes.Id==pair.Yes.Id&&no.Id==pair.No.Id;
            if(!stored||pair.Generation!=branchGeometryGeneration)
            {
                var a=BaseRouteEdge(source,yes,"true");var b=BaseRouteEdge(source,no,"false");var flatA=RouteCoordinates(a);var flatB=RouteCoordinates(b);
                var obstacles=FlowBranchRouting.Conflicts(flatA,flatB)?BranchObstacles(source,yes,no,a,b):new float[0][];
                var paths=FlowBranchRouting.Separate(BoxCoordinates(DisplayBounds(source)),BoxCoordinates(DisplayBounds(yes)),BoxCoordinates(DisplayBounds(no)),flatA,flatB,obstacles);
                var yesPoints=RoutePoints(paths[0]);var noPoints=RoutePoints(paths[1]);
                if(!stored)return branch=="true"?yesPoints:noPoints;
                pair.YesPoints=yesPoints;pair.NoPoints=noPoints;pair.Generation=branchGeometryGeneration;
            }
            return branch=="true"?pair.YesPoints:pair.NoPoints;
        }
    }
}
