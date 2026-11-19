using System;using System.Linq;using System.Drawing;using System.Collections.Generic;using System.Windows.Forms;
namespace Frociart {
 public partial class FlowCanvas {
  private FlowCycleFold cycleFold;
  private Diagram cycleFoldDocument;
  private FlowCycleFold.Projection foldProjection;
  private bool foldModelDirty=true;
  private ToolStripMenuItem foldCycleMenu;
  internal event EventHandler CyclePresentationChanged;
  internal void NotifyFoldModelChanged(){foldModelDirty=true;}
  private void EnsureFoldModel(bool force=false){
   if(_diagram==null)return;
   if(!ReferenceEquals(cycleFoldDocument,_diagram)){cycleFoldDocument=_diagram;cycleFold=new FlowCycleFold(_diagram,appearance.Design);foldModelDirty=false;}
   else if(foldModelDirty||force){cycleFold.Update(force);foldModelDirty=false;}
   foldProjection=cycleFold.Current;
  }
  private Diagram CanvasScene(){
   if(_diagram==null||ViewOnly||_exportRendering)return _diagram;
   if(!ReferenceEquals(cycleFoldDocument,_diagram)&&!_diagram.Nodes.Any(n=>n.FoldedCycle)){foldProjection=null;return _diagram;}
   EnsureFoldModel();if(foldProjection==null)return _diagram;foldProjection.Sync();return foldProjection.Scene;
  }
  private string FoldOwner(string id){return !_exportRendering&&foldProjection!=null?foldProjection.Owner(id):id;}
  private FlowNode FoldVisual(FlowNode n){return n!=null&&!_exportRendering&&foldProjection!=null?(foldProjection.Visual(n.Id)??n):n;}
  private bool IsFoldCard(string id){return !ViewOnly&&!_exportRendering&&id!=null&&foldProjection!=null&&foldProjection.Cards.ContainsKey(id);}
  private bool FoldIssue(string id){FlowCycle.Info card;return foldProjection!=null&&foldProjection.Cards.TryGetValue(id,out card)?card.Members.Any(_issueNodeIds.Contains):_issueNodeIds.Contains(id);}
  private IEnumerable<string> FoldSelection(IEnumerable<string> ids){return foldProjection==null?ids:foldProjection.Selection(ids);}
  private Dictionary<string,PointF> PresentationPositions(){EnsureScene();return _nodeVisuals.ToDictionary(n=>n.Id,n=>n.Bounds.Location);}
  private FlowCycle.Info SelectionCycle(){EnsureFoldModel();if(cycleFold==null)return null;return cycleFold.Cycles.Where(c=>_selectedNodeIds.Count>0&&_selectedNodeIds.All(c.Members.Contains)).OrderBy(c=>c.Members.Count).FirstOrDefault();}
  internal bool CanFoldSelection {get{return !ViewOnly&&SelectionCycle()!=null;}}
  internal bool SelectionCycleFolded {get{var c=SelectionCycle();return c!=null&&FindNode(c.Check).FoldedCycle;}}
  internal void ToggleSelectedCycle(){var c=SelectionCycle();if(c!=null)FoldCycle(c.Check,!FindNode(c.Check).FoldedCycle);}
  internal void FoldAllCycles(bool folded){ChangeFold(()=>cycleFold.All(folded),folded);}
  internal bool FoldCycle(string check,bool folded){return ChangeFold(()=>cycleFold.Set(check,folded),folded);}
  private bool RevealFold(string id){if(id==null||foldProjection==null||!foldProjection.Owners.ContainsKey(id))return false;return ChangeFold(()=>cycleFold.Reveal(id),false);}
  private bool ChangeFold(Func<bool> change,bool closing){
   if(ViewOnly||_diagram==null||IsPointerGestureActive||IsBlockPickerOpen)return false;EnsureScene();EnsureFoldModel();
   var before=_nodeVisuals.ToDictionary(v=>v.Id,v=>v.Bounds.Location);var sizes=_nodeVisuals.ToDictionary(v=>v.Id,v=>v.Bounds.Size);var old=foldProjection;
   if(UiMotion.Enabled){var ids=new HashSet<string>(_nodeVisuals.Select(v=>v.Id));StartClipboardFlight(ids,true);}
   if(!change()){StopClipboardFlight();return false;}foldProjection=cycleFold.Current;foldModelDirty=false;SetSelection(FoldSelection(_selectedNodeIds),_selectedEdgeId);
   var graph=foldProjection==null?_diagram:foldProjection.Scene;foreach(var node in graph.Nodes)if(!before.ContainsKey(node.Id)&&old!=null){PointF at;if(before.TryGetValue(old.Owner(node.Id),out at))before[node.Id]=at;}
   hoverPorts.Clear();_hoverNodeId=null;_frameDirtyAll=true;ClearScene();
   foreach(var node in graph.Nodes){SizeF size;if(sizes.TryGetValue(node.Id,out size))sizeMotion[node.Id]=new SizeMotion{From=size,To=FlowSizing.Size(node,appearance.Design),At=manipulationClock.ElapsedMilliseconds};}
   EnsureScene();AnimatePlacement(before);var handler=CyclePresentationChanged;if(handler!=null)handler(this,EventArgs.Empty);RaiseSelectionChanged();Report(closing?"Cicli ridotti sul foglio. Seleziona una scheda per spostare o copiare tutto il ciclo; doppio clic per espandere.":"Ciclo espanso. Le istruzioni e i collegamenti originali sono sul foglio.");return true;
  }
 }
}
