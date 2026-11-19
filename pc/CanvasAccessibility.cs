using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
    public partial class FlowCanvas {
        private CanvasAccessGraph accessGraph;
        private bool accessGraphDirty=true;
        private string accessFocusKey;
        private bool accessibilityRequested;

        protected override AccessibleObject CreateAccessibilityInstance(){accessibilityRequested=true;return new CanvasAccessRoot(this);}
        internal void NotifyAccessibleDocumentChanged(){NotifyAccessibleStructure();}
        private void NotifyAccessibleModeChanged(){if(accessibilityRequested&&IsHandleCreated&&!IsDisposed)AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);}

        private CanvasAccessGraph AccessGraph {
            get {
                int nodes=_diagram==null?0:_diagram.Nodes.Count,edges=_diagram==null?0:_diagram.Edges.Count;
                if(accessGraphDirty||accessGraph==null||!ReferenceEquals(accessGraph.Document,_diagram)||accessGraph.NodeCount!=nodes||accessGraph.EdgeCount!=edges){
                    accessGraph=new CanvasAccessGraph(this,_diagram);accessGraphDirty=false;
                }
                return accessGraph;
            }
        }
        private void NotifyAccessibleStructure(){
            if(accessGraph!=null&&!accessGraphDirty&&accessGraph.Matches(_diagram)){
                if(accessibilityRequested&&IsHandleCreated&&!IsDisposed){
                    var selected=AccessSelection();
                    if(selected!=null){int changes=selected.AccessChanges();
                        if((changes&1)!=0)AccessibilityNotifyClients(AccessibleEvents.NameChange,selected.Position);
                        if((changes&2)!=0)AccessibilityNotifyClients(AccessibleEvents.ValueChange,selected.Position);
                        if((changes&4)!=0)AccessibilityNotifyClients(AccessibleEvents.DescriptionChange,selected.Position);
                    }
                    AccessibilityNotifyClients(AccessibleEvents.LocationChange,-1);
                }
                return;
            }
            accessGraphDirty=true;accessGraph=null;accessFocusKey=null;
            if(accessibilityRequested&&IsHandleCreated&&!IsDisposed)AccessibilityNotifyClients(AccessibleEvents.Reorder,-1);
        }
        private CanvasAccessItem AccessSelection(){
            var graph=AccessGraph;CanvasAccessItem result;
            if(_selectedEdgeId!=null&&graph.ByKey.TryGetValue("e:"+_selectedEdgeId,out result))return result;
            foreach(var item in graph.Nodes)if(_selectedNodeIds.Contains(item.Node.Id))return item;
            return null;
        }
        private CanvasAccessItem AccessFocus(){
            CanvasAccessItem item;
            if(accessFocusKey!=null&&AccessGraph.ByKey.TryGetValue(accessFocusKey,out item)&&
                (item.Node!=null?_selectedNodeIds.Contains(item.Node.Id):_selectedEdgeId==accessFocusKey.Substring(2)))return item;
            return AccessSelection();
        }
        private void TakeAccessFocus(string key){
            bool changed=accessFocusKey!=key,alreadyFocused=Focused;accessFocusKey=key;Focus();CanvasAccessItem item;
            if(changed&&alreadyFocused&&IsHandleCreated&&Focused&&AccessGraph.ByKey.TryGetValue(key,out item))AccessibilityNotifyClients(AccessibleEvents.Focus,item.Position);
        }
        private void NotifyAccessibleSelection(bool forceFocus=false){
            if(!IsHandleCreated||IsDisposed)return;
            if(!accessibilityRequested){
                if(_selectedEdgeId!=null)accessFocusKey="e:"+_selectedEdgeId;
                else if(_selectedNodeIds.Count==0)accessFocusKey=null;
                else if(_selectedNodeIds.Count==1)accessFocusKey="n:"+_selectedNodeIds.First();
                else if(accessFocusKey==null||!_selectedNodeIds.Contains(accessFocusKey.Substring(2)))accessFocusKey="n:"+FoldOwner(_selectedNodeIds.First());
                return;
            }
            string previousFocus=accessFocusKey;
            var item=AccessSelection();
            // A mutator may select a newly inserted item before DiagramChanged.
            if(item==null&&(_selectedEdgeId!=null||_selectedNodeIds.Count>0)){
                accessGraphDirty=true;item=AccessSelection();
            }
            if(item==null)accessFocusKey=null;
            else if(_selectedNodeIds.Count<=1)accessFocusKey=item.Key;
            int child=item==null?-1:item.Position;
            AccessibilityNotifyClients(AccessibleEvents.Selection,child);
            var focus=Focused?AccessFocus():null;
            if(Focused&&(forceFocus||(focus==null?null:focus.Key)!=previousFocus))AccessibilityNotifyClients(AccessibleEvents.Focus,focus==null?-1:focus.Position);
        }
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);NotifyAccessibleSelection(true);}

        private bool HandleCanvasNavigation(KeyEventArgs e){
            if(e.Modifiers!=Keys.None||!Enabled||IsPointerGestureActive)return false;
            if(e.KeyCode!=Keys.PageDown&&e.KeyCode!=Keys.PageUp&&e.KeyCode!=Keys.Home&&e.KeyCode!=Keys.End)return false;
            EnsureScene();
            var nodes=AccessGraph.Nodes.Where(item=>FoldOwner(item.Node.Id)==item.Node.Id).ToList();if(nodes.Count==0)return false;
            var focused=AccessFocus();string currentId=SelectedNodeId??(focused==null||focused.Node==null?null:focused.Node.Id);
            int current=nodes.FindIndex(item=>item.Node.Id==currentId),index;
            if(e.KeyCode==Keys.Home)index=0;
            else if(e.KeyCode==Keys.End)index=nodes.Count-1;
            else if(current<0)index=e.KeyCode==Keys.PageUp?nodes.Count-1:0;
            else index=Math.Max(0,Math.Min(nodes.Count-1,current+(e.KeyCode==Keys.PageDown?1:-1)));
            FocusAccessNode(nodes[index].Node);
            e.Handled=true;e.SuppressKeyPress=true;return true;
        }
        private bool FocusAccessNode(FlowNode node,bool takeFocus=true){
            var expected=_diagram;
            if(IsFoldCard(node.Id))SetSelection(new[]{node.Id},null);else SelectNode(node.Id);
            if(!ReferenceEquals(expected,_diagram)||!ReferenceEquals(FindNode(node.Id),node))return false;
            if(takeFocus)TakeAccessFocus("n:"+node.Id);
            RectangleF bounds=DisplayBounds(node);
            PointF top=WorldToClient(bounds.Location);
            var client=new RectangleF(top.X,top.Y,bounds.Width*_zoom,bounds.Height*_zoom);
            var viewport=new RectangleF(12,12,Math.Max(0,ClientSize.Width-24),Math.Max(0,ClientSize.Height-24));
            if(!viewport.Contains(client))CenterOnNodeAnimated(node.Id);
            return true;
        }
        private void ActivateAccessNode(FlowNode node){
            if(ViewOnly)return;
            if(IsConnecting){CompleteConnection(node);return;}
            if(IsFoldCard(node.Id)){FoldCycle(foldProjection.Cards[node.Id].Check,false);return;}
            var handler=EditRequested;if(handler!=null)handler(this,EventArgs.Empty);
        }
        private FlowNode AccessActivationNode(){
            var single=FindNode(SelectedNodeId);if(single!=null)return single;
            var focus=AccessFocus();if(focus==null||focus.Node==null||!IsFoldCard(focus.Node.Id))return null;
            return _selectedNodeIds.SetEquals(foldProjection.Cards[focus.Node.Id].Members)?focus.Node:null;
        }
        private static string AccessShort(string text){
            text=(text??"").Replace('\r',' ').Replace('\n',' ').Replace('\t',' ').Trim();
            if(text.Length<=160)return text;
            int length=159;if(Char.IsHighSurrogate(text[length-1]))length--;
            return text.Substring(0,length)+"…";
        }

        private sealed class CanvasAccessGraph {
            internal readonly Diagram Document;
            internal readonly int NodeCount,EdgeCount;
            internal readonly List<CanvasAccessItem> Items=new List<CanvasAccessItem>(),Nodes=new List<CanvasAccessItem>();
            internal readonly Dictionary<string,CanvasAccessItem> ByKey=new Dictionary<string,CanvasAccessItem>(StringComparer.Ordinal);
            private readonly FlowNode[] nodeIdentity;private readonly FlowEdge[] edgeIdentity;
            internal CanvasAccessGraph(FlowCanvas owner,Diagram document){
                Document=document;nodeIdentity=document==null?new FlowNode[0]:document.Nodes.ToArray();edgeIdentity=document==null?new FlowEdge[0]:document.Edges.ToArray();
                if(document==null)return;NodeCount=document.Nodes.Count;EdgeCount=document.Edges.Count;
                var nodes=new Dictionary<string,FlowNode>(StringComparer.Ordinal);
                var indexes=new Dictionary<string,int>(StringComparer.Ordinal);
                var outgoing=new Dictionary<string,List<int>>(StringComparer.Ordinal);
                for(int i=0;i<document.Nodes.Count;i++){
                    var node=document.Nodes[i];if(node.Id==null||nodes.ContainsKey(node.Id))continue;
                    nodes.Add(node.Id,node);indexes.Add(node.Id,i);outgoing.Add(node.Id,new List<int>());
                }
                for(int i=0;i<document.Edges.Count;i++){
                    var edge=document.Edges[i];List<int> list;
                    if(edge.Id!=null&&edge.To!=null&&nodes.ContainsKey(edge.To)&&edge.From!=null&&outgoing.TryGetValue(edge.From,out list))list.Add(i);
                }
                foreach(var list in outgoing.Values)list.Sort((a,b)=>{int result=BranchOrder(document.Edges[a].Branch).CompareTo(BranchOrder(document.Edges[b].Branch));return result!=0?result:a.CompareTo(b);});
                var visited=new HashSet<string>(StringComparer.Ordinal);
                Action<string> walk=first=>{
                    var stack=new Stack<string>();stack.Push(first);
                    while(stack.Count>0){
                        string id=stack.Pop();if(!visited.Add(id))continue;
                        var node=nodes[id];Add(new CanvasAccessItem(owner,this,node,indexes[id]));
                        foreach(int index in outgoing[id])Add(new CanvasAccessItem(owner,this,document.Edges[index],index,nodes[document.Edges[index].From],nodes[document.Edges[index].To],indexes[document.Edges[index].From],indexes[document.Edges[index].To]));
                        var next=outgoing[id];for(int j=next.Count-1;j>=0;j--)stack.Push(document.Edges[next[j]].To);
                    }
                };
                foreach(var node in document.Nodes.Where(n=>n.Kind==NodeKind.Start&&n.Id!=null))if(nodes.ContainsKey(node.Id))walk(node.Id);
                foreach(var node in document.Nodes)if(node.Id!=null&&nodes.ContainsKey(node.Id))walk(node.Id);
            }
            internal bool Matches(Diagram document){
                if(!ReferenceEquals(Document,document))return false;if(document==null)return true;
                if(NodeCount!=document.Nodes.Count||EdgeCount!=document.Edges.Count)return false;
                for(int i=0;i<NodeCount;i++)if(!ReferenceEquals(nodeIdentity[i],document.Nodes[i]))return false;
                for(int i=0;i<EdgeCount;i++)if(!ReferenceEquals(edgeIdentity[i],document.Edges[i]))return false;
                foreach(var item in Items)if(!item.IdentityMatches)return false;
                return true;
            }
            private static int BranchOrder(string branch){return branch=="true"?0:branch=="false"?1:branch=="next"?2:3;}
            private void Add(CanvasAccessItem item){
                if(ByKey.ContainsKey(item.Key))return;
                item.Position=Items.Count;item.AccessChanges();Items.Add(item);ByKey.Add(item.Key,item);if(item.Node!=null)Nodes.Add(item);
            }
        }
        private sealed class CanvasAccessRoot:ControlAccessibleObject {
            private readonly FlowCanvas owner;
            internal CanvasAccessRoot(FlowCanvas owner):base(owner){this.owner=owner;}
            public override AccessibleRole Role{get{return AccessibleRole.Diagram;}}
            public override AccessibleStates State{get{return base.State|AccessibleStates.Focusable|AccessibleStates.MultiSelectable|(owner.ViewOnly?AccessibleStates.ReadOnly:AccessibleStates.None);}}
            public override int GetChildCount(){return owner.IsDisposed?0:owner.AccessGraph.Items.Count;}
            public override AccessibleObject GetChild(int index){var items=owner.AccessGraph.Items;return owner.IsDisposed||index<0||index>=items.Count?null:items[index];}
            public override AccessibleObject GetSelected(){return owner.IsDisposed?null:owner.AccessSelection();}
            public override AccessibleObject GetFocused(){return !owner.IsDisposed&&owner.Focused?(AccessibleObject)owner.AccessFocus()??this:null;}
            public override AccessibleObject Navigate(AccessibleNavigation direction){
                if(direction==AccessibleNavigation.FirstChild)return GetChild(0);
                if(direction==AccessibleNavigation.LastChild)return GetChild(GetChildCount()-1);
                return base.Navigate(direction);
            }
            public override AccessibleObject HitTest(int x,int y){
                if(owner.IsDisposed||!owner.IsHandleCreated||!owner.Visible)return null;
                Point client=owner.PointToClient(new Point(x,y));if(!owner.ClientRectangle.Contains(client))return null;
                PointF world=owner.ClientToWorld(client);FlowNode node=owner.HitNode(world);CanvasAccessItem item;
                if(node!=null&&owner.AccessGraph.ByKey.TryGetValue("n:"+node.Id,out item))return item;
                FlowEdge edge=owner.HitEdge(world);
                return edge!=null&&owner.AccessGraph.ByKey.TryGetValue("e:"+edge.Id,out item)?(AccessibleObject)item:this;
            }
        }
        private sealed class CanvasAccessItem:AccessibleObject {
            private readonly FlowCanvas owner;private readonly CanvasAccessGraph graph;private readonly int modelIndex;
            private readonly FlowEdge edge;private readonly FlowNode source,target;private readonly string branchIdentity;private readonly int sourceIndex,targetIndex;
            private string lastName,lastValue,lastPrompt,lastComment,lastTrue,lastFalse;
            internal readonly FlowNode Node;internal readonly string Key;internal int Position;
            internal CanvasAccessItem(FlowCanvas owner,CanvasAccessGraph graph,FlowNode node,int index){this.owner=owner;this.graph=graph;Node=node;modelIndex=index;Key="n:"+node.Id;}
            internal CanvasAccessItem(FlowCanvas owner,CanvasAccessGraph graph,FlowEdge edge,int index,FlowNode source,FlowNode target,int sourceIndex,int targetIndex){this.owner=owner;this.graph=graph;this.edge=edge;modelIndex=index;this.source=source;this.target=target;this.sourceIndex=sourceIndex;this.targetIndex=targetIndex;branchIdentity=edge.Branch;Key="e:"+edge.Id;}
            private bool Live {
                get {
                    if(owner.IsDisposed||!ReferenceEquals(owner._diagram,graph.Document)||!IdentityMatches)return false;
                    if(Node!=null)return modelIndex<graph.Document.Nodes.Count&&ReferenceEquals(graph.Document.Nodes[modelIndex],Node);
                    return modelIndex<graph.Document.Edges.Count&&ReferenceEquals(graph.Document.Edges[modelIndex],edge)&&
                        sourceIndex<graph.Document.Nodes.Count&&targetIndex<graph.Document.Nodes.Count&&
                        ReferenceEquals(graph.Document.Nodes[sourceIndex],source)&&ReferenceEquals(graph.Document.Nodes[targetIndex],target);
                }
            }
            internal bool IdentityMatches{get{return Node!=null?Key=="n:"+Node.Id:Key=="e:"+edge.Id&&edge.From==source.Id&&edge.To==target.Id&&edge.Branch==branchIdentity;}}
            internal int AccessChanges(){
                string name=Name,value=Value;int result=(name!=lastName?1:0)|(value!=lastValue?2:0);lastName=name;lastValue=value;
                if(Node!=null){if(Node.Prompt!=lastPrompt||Node.Comment!=lastComment||Node.TrueLabel!=lastTrue||Node.FalseLabel!=lastFalse)result|=4;lastPrompt=Node.Prompt;lastComment=Node.Comment;lastTrue=Node.TrueLabel;lastFalse=Node.FalseLabel;}
                return result;
            }
            private bool ActionAllowed{get{return Live&&owner.Enabled&&owner.Visible&&owner.IsHandleCreated&&!owner.IsPointerGestureActive;}}
            private string NodeName(FlowNode node){
                bool card=owner.IsFoldCard(node.Id);string text=card?owner.foldProjection.Cards[node.Id].Header:node.Text;
                return (card?"Ciclo ridotto":Ui.KindName(node.Kind))+(String.IsNullOrWhiteSpace(text)?"":": "+AccessShort(text));
            }
            private string Branch{get{return source.Kind==NodeKind.Decision?source.GetBranchLabel(edge.Branch):"Continua";}}
            public override string Name{get{return !Live?"Elemento non più disponibile":Node!=null?NodeName(Node):"Collegamento · "+Branch+" · verso "+NodeName(target);}set{}}
            public override string Value{get{return !Live?"":Node!=null?(owner.IsFoldCard(Node.Id)?owner.foldProjection.Cards[Node.Id].Header:Node.Text??""):Branch;}set{}}
            public override string Description {
                get {
                    if(!Live)return "";
                    if(Node==null)return "Da "+NodeName(source)+" a "+NodeName(target)+". Ramo: "+Branch+". Seleziona per gestire il collegamento; attiva per raggiungere il blocco di arrivo.";
                    return (Node.Prompt??"")+(String.IsNullOrEmpty(Node.Comment)?"":"\nNota: "+Node.Comment)+
                        (Node.Kind==NodeKind.Decision?"\nRami: "+Node.GetBranchLabel("true")+"; "+Node.GetBranchLabel("false")+".":"")+
                        (Node.Breakpoint?"\nPunto di interruzione impostato.":"");
                }
            }
            public override AccessibleRole Role{get{return Node==null?AccessibleRole.Link:AccessibleRole.ListItem;}}
            public override AccessibleObject Parent{get{return owner.AccessibilityObject;}}
            public override string DefaultAction{get{return !Live?"":Node==null?"Vai al blocco di arrivo":owner.ViewOnly?"Seleziona blocco":owner.IsConnecting?"Collega a questo blocco":owner.IsFoldCard(Node.Id)?"Espandi ciclo":"Modifica blocco";}}
            public override Rectangle Bounds {
                get {
                    if(!Live||!owner.IsHandleCreated)return Rectangle.Empty;
                    RectangleF world;
                    if(Node!=null)world=owner.DisplayBounds(Node);
                    else {var from=owner.foldProjection==null?source:owner.foldProjection.Visual(source.Id)??source;var to=owner.foldProjection==null?target:owner.foldProjection.Visual(target.Id)??target;
                        var points=owner.RouteEdge(from,to,edge.Branch);float left=points.Min(p=>p.X),top=points.Min(p=>p.Y);world=new RectangleF(left,top,points.Max(p=>p.X)-left,points.Max(p=>p.Y)-top);world.Inflate(5,5);}
                    PointF screen=owner.WorldToClient(world.Location);
                    return owner.RectangleToScreen(Rectangle.Ceiling(new RectangleF(screen.X,screen.Y,Math.Max(1,world.Width*owner._zoom),Math.Max(1,world.Height*owner._zoom))));
                }
            }
            public override AccessibleStates State {
                get {
                    if(!Live)return AccessibleStates.Unavailable|AccessibleStates.Invisible;
                    var state=AccessibleStates.Focusable|AccessibleStates.Selectable;
                    if(!owner.Enabled)state|=AccessibleStates.Unavailable;
                    if(owner.ViewOnly)state|=AccessibleStates.ReadOnly;
                    bool selected=Node==null?owner._selectedEdgeId==edge.Id:owner._selectedNodeIds.Contains(Node.Id);
                    if(selected)state|=AccessibleStates.Selected;
                    var focus=owner.Focused?owner.AccessFocus():null;
                    if(focus!=null&&focus.Key==Key)state|=AccessibleStates.Focused;
                    bool foldedEdge=Node==null&&owner.FoldOwner(source.Id)==owner.FoldOwner(target.Id)&&owner.IsFoldCard(owner.FoldOwner(source.Id));
                    if(!owner.Visible||!owner.IsHandleCreated||Node!=null&&owner.FoldOwner(Node.Id)!=Node.Id||foldedEdge)state|=AccessibleStates.Invisible;
                    if(owner.IsHandleCreated&&!owner.RectangleToScreen(owner.ClientRectangle).IntersectsWith(Bounds))state|=AccessibleStates.Offscreen;
                    return state;
                }
            }
            public override AccessibleObject Navigate(AccessibleNavigation direction){
                if(!Live)return null;CanvasAccessItem current;var currentGraph=owner.AccessGraph;
                if(!currentGraph.ByKey.TryGetValue(Key,out current))return null;
                int delta=direction==AccessibleNavigation.Next||direction==AccessibleNavigation.Down||direction==AccessibleNavigation.Right?1:
                    direction==AccessibleNavigation.Previous||direction==AccessibleNavigation.Up||direction==AccessibleNavigation.Left?-1:0;
                int index=current.Position+delta;
                return delta!=0&&index>=0&&index<currentGraph.Items.Count?currentGraph.Items[index]:null;
            }
            private void OnOwner(Action action){
                if(!ActionAllowed)return;
                if(owner.InvokeRequired){try{owner.BeginInvoke((Action)(()=>{if(ActionAllowed)action();}));}catch(InvalidOperationException){if(!owner.IsDisposed)throw;}}
                else action();
            }
            public override void Select(AccessibleSelection flags){
                if(flags==AccessibleSelection.None)return;
                OnOwner(()=>{
                    if(Node==null){if((flags&AccessibleSelection.RemoveSelection)!=0){if(owner._selectedEdgeId==edge.Id)owner.SelectEdge(null);}
                        else owner.SelectEdge(edge.Id);
                    }else if((flags&AccessibleSelection.RemoveSelection)!=0)owner.SelectNodes(owner.SelectedNodeIds.Where(id=>id!=Node.Id));
                    else if((flags&(AccessibleSelection.AddSelection|AccessibleSelection.ExtendSelection))!=0)owner.SelectNodes(owner.SelectedNodeIds.Concat(new[]{Node.Id}));
                    else owner.FocusAccessNode(Node,(flags&AccessibleSelection.TakeFocus)!=0);
                    if((flags&AccessibleSelection.TakeFocus)!=0&&Live)owner.TakeAccessFocus(Node==null?Key:"n:"+owner.FoldOwner(Node.Id));
                });
            }
            public override void DoDefaultAction(){
                OnOwner(()=>{
                    if(Node==null){owner.FocusAccessNode(target);return;}
                    if(owner.FocusAccessNode(Node)&&Live)owner.ActivateAccessNode(Node);
                });
            }
        }
    }
}
