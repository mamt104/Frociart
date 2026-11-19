using System;
using System.Collections.Generic;

namespace Frociart
{
    // Geometry only: callers decide whether the two destinations really differ.
    // Keep the same deterministic calculation in Android's FlowBranchRouting.
    public static class FlowBranchRouting
    {
        private const float Epsilon = .1f;

        public static float[][] Separate(float[] source, float[] yesBox, float[] noBox, float[] yes, float[] no)
        {
            return Separate(source,yesBox,noBox,yes,no,new float[0][]);
        }
        public static float[][] Separate(float[] source, float[] yesBox, float[] noBox, float[] yes, float[] no, float[][] obstacles)
        {
            if (!Conflicts(yes, no)) return new float[][] { yes, no };
            float[][] anchors = new float[][] { source, yesBox, noBox };
            var all=new List<float[]>(anchors);all.AddRange(obstacles);float[][] boxes=all.ToArray();
            float[] replacement;
            if (Clear(yes, boxes))
            {
                replacement = Detour(false, no, yes, anchors, boxes);
                if (replacement != null) return new float[][] { yes, replacement };
            }
            if (Clear(no, boxes))
            {
                replacement = Detour(true, yes, no, anchors, boxes);
                if (replacement != null) return new float[][] { replacement, no };
            }
            // A base route may run through the other destination. In that case
            // find both routes outside the three boxes before separating them.
            float[] first = Detour(true, yes, new float[0], anchors, boxes);
            if (first != null)
            {
                replacement = Detour(false, no, first, anchors, boxes);
                if (replacement != null) return new float[][] { first, replacement };
            }
            first = Detour(false, no, new float[0], anchors, boxes);
            if (first != null)
            {
                replacement = Detour(true, yes, first, anchors, boxes);
                if (replacement != null) return new float[][] { replacement, first };
            }
            // Dense or overlapping boxes may leave no route on this local grid.
            // Never change the graph or silently move the user's blocks.
            return new float[][] { yes, no };
        }

        public static bool Overlaps(float[] a, float[] b)
        {
            for (int i = 2; i < a.Length; i += 2)
                for (int j = 2; j < b.Length; j += 2)
                    if (Shared(a[i-2], a[i-1], a[i], a[i+1], b[j-2], b[j-1], b[j], b[j+1])) return true;
            return false;
        }

        // Nearby parallel trunks also blend visually at ordinary zoom levels.
        public static bool Conflicts(float[] a,float[] b)
        {
            for(int i=2;i<a.Length;i+=2)if(Crowds(a[i-2],a[i-1],a[i],a[i+1],b))return true;
            return false;
        }
        private static bool Crowds(float ax,float ay,float bx,float by,float[] route)
        {
            for(int i=2;i<route.Length;i+=2)
            {
                float cx=route[i-2],cy=route[i-1],dx=route[i],dy=route[i+1];
                if(Shared(ax,ay,bx,by,cx,cy,dx,dy))return true;
                if(Math.Abs(ay-by)<Epsilon&&Math.Abs(cy-dy)<Epsilon&&Math.Abs(ay-cy)<16&&
                    Math.Min(Math.Max(ax,bx),Math.Max(cx,dx))-Math.Max(Math.Min(ax,bx),Math.Min(cx,dx))>Epsilon)return true;
                if(Math.Abs(ax-bx)<Epsilon&&Math.Abs(cx-dx)<Epsilon&&Math.Abs(ax-cx)<16&&
                    Math.Min(Math.Max(ay,by),Math.Max(cy,dy))-Math.Max(Math.Min(ay,by),Math.Min(cy,dy))>Epsilon)return true;
            }
            return false;
        }

        private static bool Shared(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
        {
            if (Math.Abs(ay-by)<Epsilon && Math.Abs(cy-dy)<Epsilon && Math.Abs(ay-cy)<Epsilon)
                return Math.Min(Math.Max(ax,bx), Math.Max(cx,dx))-Math.Max(Math.Min(ax,bx),Math.Min(cx,dx))>Epsilon;
            if (Math.Abs(ax-bx)<Epsilon && Math.Abs(cx-dx)<Epsilon && Math.Abs(ax-cx)<Epsilon)
                return Math.Min(Math.Max(ay,by), Math.Max(cy,dy))-Math.Max(Math.Min(ay,by),Math.Min(cy,dy))>Epsilon;
            return false;
        }

        private static bool Clear(float[] route, float[][] boxes)
        {
            for(int i=2;i<route.Length;i+=2)
                if (Blocked(route[i-2],route[i-1],route[i],route[i+1],boxes)) return false;
            return true;
        }

        private static bool Blocked(float ax,float ay,float bx,float by,float[][] boxes)
        {
            foreach(float[] b in boxes)
            {
                if(Math.Abs(ay-by)<Epsilon && ay>b[1]+Epsilon && ay<b[3]-Epsilon &&
                    Math.Min(ax,bx)<b[2]-Epsilon && Math.Max(ax,bx)>b[0]+Epsilon) return true;
                if(Math.Abs(ax-bx)<Epsilon && ax>b[0]+Epsilon && ax<b[2]-Epsilon &&
                    Math.Min(ay,by)<b[3]-Epsilon && Math.Max(ay,by)>b[1]+Epsilon) return true;
            }
            return false;
        }

        private static float[] Detour(bool left,float[] original,float[] peer,float[][] anchors,float[][] boxes)
        {
            // The first and last segments remain attached to the original ports.
            // Shorter leads also support two boxes with a small vertical gap.
            float[] leads = new float[] {24,12,6};
            foreach(float lead in leads)
            {
                float sx=original[0],sy=original[1],ex=original[original.Length-2],ey=original[original.Length-1];
                float ox=sx+(left?-42:42),ty=ey-lead;
                float[] start=new float[]{sx,sy,ox,sy},end=new float[]{ex,ty,ex,ey};
                if(!Clear(start,boxes)||!Clear(end,boxes)||Conflicts(start,peer)||Conflicts(end,peer))continue;
                List<float> xs=new List<float>(),ys=new List<float>();
                xs.Add(ox);xs.Add(ex);ys.Add(sy);ys.Add(ty);
                foreach(float[] b in anchors)
                {
                    xs.Add(b[0]-42);xs.Add(b[2]+42);xs.Add((b[0]+b[2])/2);
                    ys.Add(b[1]-32);ys.Add(b[3]+32);
                }
                for(int i=0;i<peer.Length;i+=2)
                {
                    xs.Add(peer[i]-24);xs.Add(peer[i]+24);
                    ys.Add(peer[i+1]-24);ys.Add(peer[i+1]+24);
                }
                float[] x=Coordinates(xs),y=Coordinates(ys);
                int w=x.Length,h=y.Length,count=w*h*2;
                float[] cost=new float[count];int[] previous=new int[count];
                for(int i=0;i<count;i++){cost[i]=float.PositiveInfinity;previous[i]=-1;}
                int begin=(Array.IndexOf(y,sy)*w+Array.IndexOf(x,ox))*2;
                int goal=(Array.IndexOf(y,ty)*w+Array.IndexOf(x,ex))*2;
                SortedSet<Step> queue=new SortedSet<Step>();cost[begin]=0;queue.Add(new Step(0,begin));
                int finish=-1;
                while(queue.Count>0)
                {
                    Step step=queue.Min;queue.Remove(step);int state=step.State,cell=state/2,px=cell%w,py=cell/w;
                    if(cell==goal/2){finish=state;break;}
                    for(int direction=0;direction<4;direction++)
                    {
                        int nx=px+(direction==0?-1:direction==1?1:0),ny=py+(direction==2?-1:direction==3?1:0);
                        if(nx<0||nx>=w||ny<0||ny>=h)continue;
                        float ax=x[px],ay=y[py],bx=x[nx],by=y[ny];
                        if(Blocked(ax,ay,bx,by,boxes)||Crowds(ax,ay,bx,by,peer)||Shares(ax,ay,bx,by,start)||Shares(ax,ay,bx,by,end))continue;
                        int axis=direction<2?0:1,next=(ny*w+nx)*2+axis;
                        float value=step.Cost+Math.Abs(ax-bx)+Math.Abs(ay-by)+(axis!=state%2?28:0)+Crossings(ax,ay,bx,by,peer)*64;
                        if(value>=cost[next])continue;
                        if(!float.IsPositiveInfinity(cost[next]))queue.Remove(new Step(cost[next],next));
                        cost[next]=value;previous[next]=state;queue.Add(new Step(value,next));
                    }
                }
                if(finish<0)continue;
                List<float> reversed=new List<float>();
                for(int state=finish;state>=0;state=previous[state]){int cell=state/2;reversed.Add(y[cell/w]);reversed.Add(x[cell%w]);}
                reversed.Reverse();List<float> result=new List<float>();result.Add(sx);result.Add(sy);result.AddRange(reversed);result.Add(ex);result.Add(ey);
                float[] cleaned=Clean(result);
                if(!Conflicts(cleaned,peer)&&Clear(cleaned,boxes))return cleaned;
            }
            return null;
        }

        private static float[] Coordinates(List<float> values)
        {
            values.Sort();List<float> result=new List<float>();
            foreach(float value in values)if(result.Count==0||value!=result[result.Count-1])result.Add(value);
            return result.ToArray();
        }
        private static bool Shares(float ax,float ay,float bx,float by,float[] route)
        {
            for(int i=2;i<route.Length;i+=2)if(Shared(ax,ay,bx,by,route[i-2],route[i-1],route[i],route[i+1]))return true;
            return false;
        }
        private static int Crossings(float ax,float ay,float bx,float by,float[] route)
        {
            int count=0;
            for(int i=2;i<route.Length;i+=2)
            {
                float cx=route[i-2],cy=route[i-1],dx=route[i],dy=route[i+1];
                if(Math.Abs(ay-by)<Epsilon && Math.Abs(cx-dx)<Epsilon && cx>Math.Min(ax,bx)+Epsilon && cx<Math.Max(ax,bx)-Epsilon && ay>Math.Min(cy,dy)+Epsilon && ay<Math.Max(cy,dy)-Epsilon)count++;
                if(Math.Abs(ax-bx)<Epsilon && Math.Abs(cy-dy)<Epsilon && ax>Math.Min(cx,dx)+Epsilon && ax<Math.Max(cx,dx)-Epsilon && cy>Math.Min(ay,by)+Epsilon && cy<Math.Max(ay,by)-Epsilon)count++;
            }
            return count;
        }
        private static float[] Clean(List<float> values)
        {
            List<float> result=new List<float>();
            for(int i=0;i<values.Count;i+=2)
            {
                float x=values[i],y=values[i+1];int n=result.Count;
                if(n>=2&&Math.Abs(x-result[n-2])<Epsilon&&Math.Abs(y-result[n-1])<Epsilon)continue;
                if(n>=4&&((Math.Abs(result[n-4]-x)<Epsilon&&Math.Abs(result[n-2]-x)<Epsilon&&(result[n-1]-result[n-3])*(y-result[n-1])>=0)||
                    (Math.Abs(result[n-3]-y)<Epsilon&&Math.Abs(result[n-1]-y)<Epsilon&&(result[n-2]-result[n-4])*(x-result[n-2])>=0)))
                {result[n-2]=x;result[n-1]=y;}else{result.Add(x);result.Add(y);}
            }
            return result.ToArray();
        }
        private sealed class Step:IComparable<Step>
        {
            internal readonly float Cost;internal readonly int State;
            internal Step(float cost,int state){Cost=cost;State=state;}
            public int CompareTo(Step other){int c=Cost.CompareTo(other.Cost);return c==0?State.CompareTo(other.State):c;}
        }
    }
}
