using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Frociart
{
    // A conservative control-flow plan for readable source generation. Unsupported graph shapes keep
    // their explicit edges; a backend can emit labelled/dispatch code rather than inventing a loop.
    internal sealed class FlowSourcePlan
    {
        internal enum Kind { Instruction, If, While, Repeat }
        internal sealed class Statement
        {
            internal readonly Kind Kind;
            internal readonly FlowSource.Block Block;
            internal readonly bool LoopOnTrue;
            internal readonly ReadOnlyCollection<Statement> Body, Otherwise;
            internal FlowSource.Block EntryBlock {get{return Kind==Kind.Repeat&&Body.Count>0?Body[0].EntryBlock:Block;}}
            internal Statement(Kind kind,FlowSource.Block block,bool loopOnTrue,IEnumerable<Statement> body,IEnumerable<Statement> otherwise)
            {Kind=kind;Block=block;LoopOnTrue=loopOnTrue;Body=Freeze(body);Otherwise=Freeze(otherwise);}
        }
        internal readonly FlowSource.Scope Scope;
        internal readonly bool Structured;
        internal readonly ReadOnlyCollection<Statement> Statements;
        private FlowSourcePlan(FlowSource.Scope scope,bool structured,IEnumerable<Statement> statements)
        {Scope=scope;Structured=structured;Statements=Freeze(statements);}
        private static ReadOnlyCollection<Statement> Freeze(IEnumerable<Statement> items)
        {return new List<Statement>(items??Enumerable.Empty<Statement>()).AsReadOnly();}
        internal static FlowSourcePlan Create(FlowSource.Scope scope)
        {
            if(scope==null)throw new ArgumentNullException("scope");
            try {
                var planner=new Planner(scope);var statements=planner.Walk(scope.StartId,null,0,new HashSet<string>());
                if(planner.Seen.Count!=scope.Blocks.Count(n=>n.Reachable))throw new ExplicitGraph();
                return new FlowSourcePlan(scope,true,statements);
            } catch(ExplicitGraph) {return new FlowSourcePlan(scope,false,null);}
        }
        private sealed class ExplicitGraph:Exception {}
        private sealed class Planner
        {
            private readonly FlowSource.Scope scope;
            private readonly Dictionary<string,List<FlowSource.Block>> postTails=new Dictionary<string,List<FlowSource.Block>>();
            private readonly Dictionary<string,List<string>> incoming=new Dictionary<string,List<string>>();
            internal readonly HashSet<string> Seen=new HashSet<string>(StringComparer.Ordinal);
            internal Planner(FlowSource.Scope scope){this.scope=scope;foreach(var block in scope.Blocks)foreach(var link in block.Links){List<string> predecessors;if(!incoming.TryGetValue(link.To,out predecessors))incoming.Add(link.To,predecessors=new List<string>());predecessors.Add(block.Id);if(block.Reachable&&block.Kind==NodeKind.Decision){List<FlowSource.Block> list;if(!postTails.TryGetValue(link.To,out list))postTails.Add(link.To,list=new List<FlowSource.Block>());list.Add(block);}}}
            private bool BackReach(string from,string target,string avoid,HashSet<string> blocked)
            {
                if(from==target)return true;if(from==avoid||blocked.Contains(from))return false;
                var seen=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(target);
                while(queue.Count>0){string id=queue.Dequeue();if(id==from)return true;if(id==avoid||(id!=target&&blocked.Contains(id))||!seen.Add(id))continue;
                    List<string> predecessors;if(incoming.TryGetValue(id,out predecessors))foreach(string parent in predecessors)queue.Enqueue(parent);}
                return false;
            }
            private FlowSource.Block PostTail(string entry,string stop,HashSet<string> boundaries)
            {
                List<FlowSource.Block> all;if(!postTails.TryGetValue(entry,out all))return null;
                var candidates=all.Where(b=>b.Id!=entry&&b.Id!=stop&&!boundaries.Contains(b.Id)&&!Seen.Contains(b.Id)&&(b.Next("true")==entry)!=(b.Next("false")==entry)).ToList();
                if(candidates.Count>48)throw new ExplicitGraph();
                // A latch exit must not loop to the latch without re-entering its body.
                candidates=candidates.Where(b=>{var before=new HashSet<string>(boundaries);before.Add(b.Id);if(stop!=null)before.Add(stop);string exit=b.Next(b.Next("true")==entry?"false":"true");return !BackReach(exit,b.Id,entry,before)&&!Distances(entry,before).ContainsKey(exit);}).ToList();
                if(candidates.Count==0)return null;
                if(candidates.Count==1)return candidates[0];
                // Shared entry points occur for nested repeat loops. The outer latch has no
                // other candidate latch reachable from its exit without entering the body again.
                var blocked=new HashSet<string>(boundaries);blocked.Add(entry);if(stop!=null)blocked.Add(stop);
                var outer=candidates.Where(b=>{var after=Distances(b.Next(b.Next("true")==entry?"false":"true"),blocked);return !candidates.Any(other=>other.Id!=b.Id&&after.ContainsKey(other.Id));}).ToList();
                if(outer.Count!=1)throw new ExplicitGraph();return outer[0];
            }
            internal List<Statement> Walk(string id,string stop,int depth,HashSet<string> boundaries)
            {
                if(depth>48)throw new ExplicitGraph();var result=new List<Statement>();
                while(stop==null||id!=stop)
                {
                    if(id==null||boundaries.Contains(id)||Seen.Contains(id))throw new ExplicitGraph();
                    var node=scope.Find(id);string yes=null,no=null;HashSet<string> blocked=null;
                    if(node.Kind==NodeKind.Decision){yes=node.Next("true");no=node.Next("false");blocked=new HashSet<string>(boundaries);if(stop!=null)blocked.Add(stop);blocked.Add(id);
                        bool yesLoop=Reach(yes,id,no,blocked),noLoop=Reach(no,id,yes,blocked);
                        if(yesLoop!=noLoop){Seen.Add(id);string body=yesLoop?yes:no,exit=yesLoop?no:yes;
                            result.Add(new Statement(Kind.While,node,yesLoop,Walk(body,id,depth+1,blocked),null));id=exit;continue;}}
                    var tail=PostTail(id,stop,boundaries);
                    if(tail!=null){var blockedTail=new HashSet<string>(boundaries);blockedTail.Add(tail.Id);if(stop!=null)blockedTail.Add(stop);bool onTrue=tail.Next("true")==id;
                        var body=Walk(id,tail.Id,depth+1,blockedTail);if(!Seen.Add(tail.Id))throw new ExplicitGraph();
                        result.Add(new Statement(Kind.Repeat,tail,onTrue,body,null));id=tail.Next(onTrue?"false":"true");continue;}
                    Seen.Add(id);
                    if(node.Kind!=NodeKind.Decision){result.Add(new Statement(Kind.Instruction,node,true,null,null));
                        if(node.Kind==NodeKind.End)return result;id=node.Next("next");continue;}
                    string join=Join(yes,no,blocked,stop);
                    var thenBody=Walk(yes,join,depth+1,blocked);var elseBody=Walk(no,join,depth+1,blocked);
                    result.Add(new Statement(Kind.If,node,true,thenBody,elseBody));
                    if(join==null)return result;id=join;
                }
                return result;
            }
            private bool Reach(string from,string target,string avoid,HashSet<string> blocked)
            {
                var seen=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(from);
                while(queue.Count>0){string id=queue.Dequeue();if(id==target)return true;if(id==avoid||blocked.Contains(id)||!seen.Add(id))continue;
                    foreach(var edge in scope.Find(id).Links)queue.Enqueue(edge.To);}
                return false;
            }
            private Dictionary<string,int> Distances(string from,HashSet<string> blocked)
            {
                var result=new Dictionary<string,int>();var queue=new Queue<string>();result.Add(from,0);queue.Enqueue(from);
                while(queue.Count>0){string id=queue.Dequeue();if(blocked.Contains(id))continue;
                    foreach(var edge in scope.Find(id).Links)if(!result.ContainsKey(edge.To)){result.Add(edge.To,result[id]+1);queue.Enqueue(edge.To);}}
                return result;
            }
            private string Join(string yes,string no,HashSet<string> blocked,string stop)
            {
                var a=Distances(yes,blocked);var b=Distances(no,blocked);string best=null;int score=Int32.MaxValue;
                // An enclosing loop header is already seen, but is a valid join at this stop.
                foreach(var node in scope.Blocks){int x,y;if(!a.TryGetValue(node.Id,out x)||!b.TryGetValue(node.Id,out y)||(Seen.Contains(node.Id)&&node.Id!=stop))continue;
                    int cost=Math.Max(x,y)*1000+x+y;if(cost<score){score=cost;best=node.Id;}}
                return best;
            }
        }
    }
}
