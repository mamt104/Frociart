package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Conservative readable control-flow plan. Explicit edges remain available for arbitrary graphs. */
final class FlowSourcePlan {
    enum Kind { Instruction, If, While, Repeat }
    static final class Statement {
        final Kind kind;final FlowSource.Block block;final boolean loopOnTrue;
        final List<Statement> body,otherwise;
        FlowSource.Block entryBlock(){return kind==Kind.Repeat&&!body.isEmpty()?body.get(0).entryBlock():block;}
        Statement(Kind kind,FlowSource.Block block,boolean loopOnTrue,List<Statement> body,List<Statement> otherwise){
            this.kind=kind;this.block=block;this.loopOnTrue=loopOnTrue;this.body=freeze(body);this.otherwise=freeze(otherwise);
        }
    }
    final FlowSource.Scope scope;final boolean structured;final List<Statement> statements;
    private FlowSourcePlan(FlowSource.Scope scope,boolean structured,List<Statement> statements){this.scope=scope;this.structured=structured;this.statements=freeze(statements);}
    private static List<Statement> freeze(List<Statement> values){return Collections.unmodifiableList(values==null?new ArrayList<Statement>():new ArrayList<>(values));}
    static FlowSourcePlan create(FlowSource.Scope scope){
        if(scope==null)throw new IllegalArgumentException("scope");
        try{Planner planner=new Planner(scope);List<Statement> statements=planner.walk(scope.startId,null,0,new HashSet<String>());
            int reachable=0;for(FlowSource.Block n:scope.blocks)if(n.reachable)reachable++;if(planner.seen.size()!=reachable)throw new ExplicitGraph();
            return new FlowSourcePlan(scope,true,statements);
        }catch(ExplicitGraph ex){return new FlowSourcePlan(scope,false,null);}
    }
    private static final class ExplicitGraph extends RuntimeException {}
    private static final class Planner {
        private final FlowSource.Scope scope;final Set<String> seen=new HashSet<>();
        private final Map<String,List<FlowSource.Block>> postTails=new HashMap<>();
        private final Map<String,List<String>> incoming=new HashMap<>();
        Planner(FlowSource.Scope scope){this.scope=scope;for(FlowSource.Block block:scope.blocks)for(FlowSource.Link link:block.links){List<String> predecessors=incoming.get(link.to);if(predecessors==null){predecessors=new ArrayList<>();incoming.put(link.to,predecessors);}predecessors.add(block.id);if(block.reachable&&block.kind==DECISION){List<FlowSource.Block> list=postTails.get(link.to);if(list==null){list=new ArrayList<>();postTails.put(link.to,list);}list.add(block);}}}
        boolean backReach(String from,String target,String avoid,Set<String> blocked){
            if(from.equals(target))return true;if(from.equals(avoid)||blocked.contains(from))return false;Set<String> seen=new HashSet<>();ArrayDeque<String> queue=new ArrayDeque<>();queue.add(target);
            while(!queue.isEmpty()){String id=queue.remove();if(id.equals(from))return true;if(id.equals(avoid)||!id.equals(target)&&blocked.contains(id)||!seen.add(id))continue;List<String> predecessors=incoming.get(id);if(predecessors!=null)queue.addAll(predecessors);}return false;
        }
        FlowSource.Block postTail(String entry,String stop,Set<String> boundaries){
            List<FlowSource.Block> all=postTails.get(entry);if(all==null)return null;List<FlowSource.Block> candidates=new ArrayList<>();
            for(FlowSource.Block b:all)if(!b.id.equals(entry)&&!b.id.equals(stop)&&!boundaries.contains(b.id)&&!seen.contains(b.id)&&b.next("true").equals(entry)!=b.next("false").equals(entry))candidates.add(b);
            if(candidates.size()>48)throw new ExplicitGraph();
            Iterator<FlowSource.Block> iterator=candidates.iterator();while(iterator.hasNext()){FlowSource.Block b=iterator.next();Set<String> before=new HashSet<>(boundaries);before.add(b.id);if(stop!=null)before.add(stop);String exit=b.next(b.next("true").equals(entry)?"false":"true");if(backReach(exit,b.id,entry,before)||distances(entry,before).containsKey(exit))iterator.remove();}
            if(candidates.isEmpty())return null;if(candidates.size()==1)return candidates.get(0);Set<String> blocked=new HashSet<>(boundaries);blocked.add(entry);if(stop!=null)blocked.add(stop);FlowSource.Block outer=null;
            for(FlowSource.Block b:candidates){Map<String,Integer> after=distances(b.next(b.next("true").equals(entry)?"false":"true"),blocked);boolean contains=false;for(FlowSource.Block other:candidates)if(!other.id.equals(b.id)&&after.containsKey(other.id))contains=true;if(!contains){if(outer!=null)throw new ExplicitGraph();outer=b;}}
            if(outer==null)throw new ExplicitGraph();return outer;
        }
        List<Statement> walk(String id,String stop,int depth,Set<String> boundaries){
            if(depth>48)throw new ExplicitGraph();List<Statement> result=new ArrayList<>();
            while(stop==null||!Objects.equals(id,stop)){
                if(id==null||boundaries.contains(id)||seen.contains(id))throw new ExplicitGraph();FlowSource.Block node=scope.find(id);String yes=null,no=null;Set<String> blocked=null;
                if(node.kind==DECISION){yes=node.next("true");no=node.next("false");blocked=new HashSet<>(boundaries);if(stop!=null)blocked.add(stop);blocked.add(id);boolean yesLoop=reach(yes,id,no,blocked),noLoop=reach(no,id,yes,blocked);
                    if(yesLoop!=noLoop){seen.add(id);String body=yesLoop?yes:no,exit=yesLoop?no:yes;result.add(new Statement(Kind.While,node,yesLoop,walk(body,id,depth+1,blocked),null));id=exit;continue;}}
                FlowSource.Block tail=postTail(id,stop,boundaries);
                if(tail!=null){Set<String> blockedTail=new HashSet<>(boundaries);blockedTail.add(tail.id);if(stop!=null)blockedTail.add(stop);boolean onTrue=tail.next("true").equals(id);List<Statement> body=walk(id,tail.id,depth+1,blockedTail);if(!seen.add(tail.id))throw new ExplicitGraph();result.add(new Statement(Kind.Repeat,tail,onTrue,body,null));id=tail.next(onTrue?"false":"true");continue;}
                seen.add(id);
                if(node.kind!=DECISION){result.add(new Statement(Kind.Instruction,node,true,null,null));if(node.kind==END)return result;id=node.next("next");continue;}
                String join=join(yes,no,blocked,stop);List<Statement> thenBody=walk(yes,join,depth+1,blocked),elseBody=walk(no,join,depth+1,blocked);
                result.add(new Statement(Kind.If,node,true,thenBody,elseBody));if(join==null)return result;id=join;
            }
            return result;
        }
        boolean reach(String from,String target,String avoid,Set<String> blocked){
            Set<String> visited=new HashSet<>();ArrayDeque<String> queue=new ArrayDeque<>();queue.add(from);
            while(!queue.isEmpty()){String id=queue.remove();if(id.equals(target))return true;if(id.equals(avoid)||blocked.contains(id)||!visited.add(id))continue;
                for(FlowSource.Link edge:scope.find(id).links)queue.add(edge.to);}
            return false;
        }
        Map<String,Integer> distances(String from,Set<String> blocked){
            Map<String,Integer> result=new HashMap<>();ArrayDeque<String> queue=new ArrayDeque<>();result.put(from,0);queue.add(from);
            while(!queue.isEmpty()){String id=queue.remove();if(blocked.contains(id))continue;
                for(FlowSource.Link edge:scope.find(id).links)if(!result.containsKey(edge.to)){result.put(edge.to,result.get(id)+1);queue.add(edge.to);}}
            return result;
        }
        String join(String yes,String no,Set<String> blocked,String stop){
            Map<String,Integer> a=distances(yes,blocked),b=distances(no,blocked);String best=null;int score=Integer.MAX_VALUE;
            for(FlowSource.Block n:scope.blocks){Integer x=a.get(n.id),y=b.get(n.id);if(x==null||y==null||seen.contains(n.id)&&!n.id.equals(stop))continue;int cost=Math.max(x,y)*1000+x+y;if(cost<score){score=cost;best=n.id;}}
            return best;
        }
    }
}
