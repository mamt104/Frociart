package it.frociart.app;
import java.util.*;

/** Logical reading order and spatial keyboard navigation; never changes a graph. */
final class FlowCanvasNavigation {
    static final class Item {
        final String id;final float left,top,right,bottom;
        Item(String id,float left,float top,float right,float bottom){this.id=id;this.left=left;this.top=top;this.right=right;this.bottom=bottom;}
        float x(){return(left+right)/2;}float y(){return(top+bottom)/2;}
    }
    static List<String> order(List<String> ids,Set<String> starts,List<String[]> links){
        Set<String> valid=new HashSet<>(ids),seen=new HashSet<>();Map<String,List<String[]>> outgoing=new HashMap<>();
        for(String[] link:links)if(valid.contains(link[0])&&valid.contains(link[1]))outgoing.computeIfAbsent(link[0],k->new ArrayList<>()).add(link);
        for(List<String[]> list:outgoing.values())Collections.sort(list,(a,b)->Integer.compare(rank(a[2]),rank(b[2])));
        ArrayList<String> result=new ArrayList<>();
        for(String id:ids)if(starts.contains(id))walk(id,outgoing,seen,result);
        for(String id:ids)walk(id,outgoing,seen,result);
        return result;
    }
    private static int rank(String branch){return "true".equals(branch)?0:"false".equals(branch)?1:2;}
    private static void walk(String first,Map<String,List<String[]>> edges,Set<String> seen,List<String> result){
        ArrayDeque<String> stack=new ArrayDeque<>();stack.push(first);
        while(!stack.isEmpty()){
            String id=stack.pop();if(!seen.add(id))continue;result.add(id);List<String[]> next=edges.get(id);
            if(next!=null)for(int i=next.size()-1;i>=0;i--)if(!seen.contains(next.get(i)[1]))stack.push(next.get(i)[1]);
        }
    }
    static String direction(List<Item> items,String current,int dx,int dy){
        Item origin=null;for(Item item:items)if(item.id.equals(current)){origin=item;break;}if(origin==null)return null;
        double best=Double.POSITIVE_INFINITY;String found=null;
        for(Item item:items){
            if(item==origin)continue;double along=dx!=0?(item.x()-origin.x())*dx:(item.y()-origin.y())*dy;
            if(!(along>.1))continue;double across=dx!=0?item.y()-origin.y():item.x()-origin.x();
            boolean beam=dx!=0?item.top<origin.bottom&&item.bottom>origin.top:item.left<origin.right&&item.right>origin.left;
            double score=(beam?0:1e12)+along*along+4*across*across;
            if(score<best){best=score;found=item.id;}
        }
        return found;
    }
}
