package it.frociart.app;
import java.util.*;
    // Geometry only: callers decide whether the two destinations really differ.
    // Keep the same deterministic calculation in Android's FlowBranchRouting.
    public final class FlowBranchRouting
    {
        private static final float Epsilon = .1f;

        public static float[][] Separate(float[] source, float[] yesBox, float[] noBox, float[] yes, float[] no)
        {
            return Separate(source,yesBox,noBox,yes,no,new float[0][]);
        }
        public static float[][] Separate(float[] source, float[] yesBox, float[] noBox, float[] yes, float[] no, float[][] obstacles)
        {
            if (!Conflicts(yes, no)) return new float[][] { yes, no };
            float[][] anchors = new float[][] { source, yesBox, noBox };
            ArrayList<float[]> all=new ArrayList<>(Arrays.asList(anchors));all.addAll(Arrays.asList(obstacles));float[][] boxes=all.toArray(new float[0][]);
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

        public static boolean Overlaps(float[] a, float[] b)
        {
            for (int i = 2; i < a.length; i += 2)
                for (int j = 2; j < b.length; j += 2)
                    if (Shared(a[i-2], a[i-1], a[i], a[i+1], b[j-2], b[j-1], b[j], b[j+1])) return true;
            return false;
        }

        // Nearby parallel trunks also blend visually at ordinary zoom levels.
        public static boolean Conflicts(float[] a,float[] b)
        {
            for(int i=2;i<a.length;i+=2)if(Crowds(a[i-2],a[i-1],a[i],a[i+1],b))return true;
            return false;
        }
        private static boolean Crowds(float ax,float ay,float bx,float by,float[] route)
        {
            for(int i=2;i<route.length;i+=2)
            {
                float cx=route[i-2],cy=route[i-1],dx=route[i],dy=route[i+1];
                if(Shared(ax,ay,bx,by,cx,cy,dx,dy))return true;
                if(Math.abs(ay-by)<Epsilon&&Math.abs(cy-dy)<Epsilon&&Math.abs(ay-cy)<16&&
                    Math.min(Math.max(ax,bx),Math.max(cx,dx))-Math.max(Math.min(ax,bx),Math.min(cx,dx))>Epsilon)return true;
                if(Math.abs(ax-bx)<Epsilon&&Math.abs(cx-dx)<Epsilon&&Math.abs(ax-cx)<16&&
                    Math.min(Math.max(ay,by),Math.max(cy,dy))-Math.max(Math.min(ay,by),Math.min(cy,dy))>Epsilon)return true;
            }
            return false;
        }

        private static boolean Shared(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
        {
            if (Math.abs(ay-by)<Epsilon && Math.abs(cy-dy)<Epsilon && Math.abs(ay-cy)<Epsilon)
                return Math.min(Math.max(ax,bx), Math.max(cx,dx))-Math.max(Math.min(ax,bx),Math.min(cx,dx))>Epsilon;
            if (Math.abs(ax-bx)<Epsilon && Math.abs(cx-dx)<Epsilon && Math.abs(ax-cx)<Epsilon)
                return Math.min(Math.max(ay,by), Math.max(cy,dy))-Math.max(Math.min(ay,by),Math.min(cy,dy))>Epsilon;
            return false;
        }

        private static boolean Clear(float[] route, float[][] boxes)
        {
            for(int i=2;i<route.length;i+=2)
                if (Blocked(route[i-2],route[i-1],route[i],route[i+1],boxes)) return false;
            return true;
        }

        private static boolean Blocked(float ax,float ay,float bx,float by,float[][] boxes)
        {
            for(float[] b:boxes)
            {
                if(Math.abs(ay-by)<Epsilon && ay>b[1]+Epsilon && ay<b[3]-Epsilon &&
                    Math.min(ax,bx)<b[2]-Epsilon && Math.max(ax,bx)>b[0]+Epsilon) return true;
                if(Math.abs(ax-bx)<Epsilon && ax>b[0]+Epsilon && ax<b[2]-Epsilon &&
                    Math.min(ay,by)<b[3]-Epsilon && Math.max(ay,by)>b[1]+Epsilon) return true;
            }
            return false;
        }

        private static float[] Detour(boolean left,float[] original,float[] peer,float[][] anchors,float[][] boxes)
        {
            // The first and last segments remain attached to the original ports.
            // Shorter leads also support two boxes with a small vertical gap.
            float[] leads = new float[] {24,12,6};
            for(float lead:leads)
            {
                float sx=original[0],sy=original[1],ex=original[original.length-2],ey=original[original.length-1];
                float ox=sx+(left?-42:42),ty=ey-lead;
                float[] start=new float[]{sx,sy,ox,sy},end=new float[]{ex,ty,ex,ey};
                if(!Clear(start,boxes)||!Clear(end,boxes)||Conflicts(start,peer)||Conflicts(end,peer))continue;
                ArrayList<Float> xs=new ArrayList<Float>(),ys=new ArrayList<Float>();
                xs.add(ox);xs.add(ex);ys.add(sy);ys.add(ty);
                for(float[] b:anchors)
                {
                    xs.add(b[0]-42);xs.add(b[2]+42);xs.add((b[0]+b[2])/2);
                    ys.add(b[1]-32);ys.add(b[3]+32);
                }
                for(int i=0;i<peer.length;i+=2)
                {
                    xs.add(peer[i]-24);xs.add(peer[i]+24);
                    ys.add(peer[i+1]-24);ys.add(peer[i+1]+24);
                }
                float[] x=Coordinates(xs),y=Coordinates(ys);
                int w=x.length,h=y.length,count=w*h*2;
                float[] cost=new float[count];int[] previous=new int[count];
                for(int i=0;i<count;i++){cost[i]=Float.POSITIVE_INFINITY;previous[i]=-1;}
                int begin=(indexOf(y,sy)*w+indexOf(x,ox))*2;
                int goal=(indexOf(y,ty)*w+indexOf(x,ex))*2;
                TreeSet<Step> queue=new TreeSet<Step>();cost[begin]=0;queue.add(new Step(0,begin));
                int finish=-1;
                while(queue.size()>0)
                {
                    Step step=queue.first();queue.remove(step);int state=step.State,cell=state/2,px=cell%w,py=cell/w;
                    if(cell==goal/2){finish=state;break;}
                    for(int direction=0;direction<4;direction++)
                    {
                        int nx=px+(direction==0?-1:direction==1?1:0),ny=py+(direction==2?-1:direction==3?1:0);
                        if(nx<0||nx>=w||ny<0||ny>=h)continue;
                        float ax=x[px],ay=y[py],bx=x[nx],by=y[ny];
                        if(Blocked(ax,ay,bx,by,boxes)||Crowds(ax,ay,bx,by,peer)||Shares(ax,ay,bx,by,start)||Shares(ax,ay,bx,by,end))continue;
                        int axis=direction<2?0:1,next=(ny*w+nx)*2+axis;
                        float value=step.Cost+Math.abs(ax-bx)+Math.abs(ay-by)+(axis!=state%2?28:0)+Crossings(ax,ay,bx,by,peer)*64;
                        if(value>=cost[next])continue;
                        if(!Float.isInfinite(cost[next]))queue.remove(new Step(cost[next],next));
                        cost[next]=value;previous[next]=state;queue.add(new Step(value,next));
                    }
                }
                if(finish<0)continue;
                ArrayList<Float> reversed=new ArrayList<Float>();
                for(int state=finish;state>=0;state=previous[state]){int cell=state/2;reversed.add(y[cell/w]);reversed.add(x[cell%w]);}
                Collections.reverse(reversed);ArrayList<Float> result=new ArrayList<Float>();result.add(sx);result.add(sy);result.addAll(reversed);result.add(ex);result.add(ey);
                float[] cleaned=Clean(result);
                if(!Conflicts(cleaned,peer)&&Clear(cleaned,boxes))return cleaned;
            }
            return null;
        }

        private static float[] Coordinates(ArrayList<Float> values)
        {
            Collections.sort(values);ArrayList<Float> result=new ArrayList<Float>();
            for(float value:values)if(result.size()==0||value!=result.get(result.size()-1))result.add(value);
            return toArray(result);
        }
        private static boolean Shares(float ax,float ay,float bx,float by,float[] route)
        {
            for(int i=2;i<route.length;i+=2)if(Shared(ax,ay,bx,by,route[i-2],route[i-1],route[i],route[i+1]))return true;
            return false;
        }
        private static int Crossings(float ax,float ay,float bx,float by,float[] route)
        {
            int count=0;
            for(int i=2;i<route.length;i+=2)
            {
                float cx=route[i-2],cy=route[i-1],dx=route[i],dy=route[i+1];
                if(Math.abs(ay-by)<Epsilon && Math.abs(cx-dx)<Epsilon && cx>Math.min(ax,bx)+Epsilon && cx<Math.max(ax,bx)-Epsilon && ay>Math.min(cy,dy)+Epsilon && ay<Math.max(cy,dy)-Epsilon)count++;
                if(Math.abs(ax-bx)<Epsilon && Math.abs(cy-dy)<Epsilon && ax>Math.min(cx,dx)+Epsilon && ax<Math.max(cx,dx)-Epsilon && cy>Math.min(ay,by)+Epsilon && cy<Math.max(ay,by)-Epsilon)count++;
            }
            return count;
        }
        private static float[] Clean(ArrayList<Float> values)
        {
            ArrayList<Float> result=new ArrayList<Float>();
            for(int i=0;i<values.size();i+=2)
            {
                float x=values.get(i),y=values.get(i+1);int n=result.size();
                if(n>=2&&Math.abs(x-result.get(n-2))<Epsilon&&Math.abs(y-result.get(n-1))<Epsilon)continue;
                if(n>=4&&((Math.abs(result.get(n-4)-x)<Epsilon&&Math.abs(result.get(n-2)-x)<Epsilon&&(result.get(n-1)-result.get(n-3))*(y-result.get(n-1))>=0)||
                    (Math.abs(result.get(n-3)-y)<Epsilon&&Math.abs(result.get(n-1)-y)<Epsilon&&(result.get(n-2)-result.get(n-4))*(x-result.get(n-2))>=0)))
                {result.set(n-2,x);result.set(n-1,y);}else{result.add(x);result.add(y);}
            }
            return toArray(result);
        }
        private static final class Step implements Comparable<Step>
        {
            final float Cost;final int State;
            Step(float cost,int state){Cost=cost;State=state;}
            public int compareTo(Step other){int c=Float.compare(Cost,other.Cost);return c==0?Integer.compare(State,other.State):c;}
        }
    
        private static int indexOf(float[] a,float value){for(int i=0;i<a.length;i++)if(a[i]==value)return i;return -1;}
        private static float[] toArray(ArrayList<Float> values){float[] a=new float[values.size()];for(int i=0;i<a.length;i++)a[i]=values.get(i);return a;}
}