using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed partial class MainForm
    {
        internal Diagram Project;
        internal string ActiveFunctionId;
        [System.ComponentModel.Browsable(false),System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal Diagram Document
        {
            get{return FunctionEditing.Graph(Project,ActiveFunctionId);}
            set{var f=FunctionEditing.Find(Project,ActiveFunctionId);if(f==null)Project=value;else f.Body=value;}
        }
        private sealed class ProjectHistory
        {
            internal Diagram Project;
            internal string FunctionId;
            internal FlowAppearance Appearance;
            internal ProjectHistory(Diagram project,string id){Project=DiagramIO.Clone(project);FunctionId=id;}
        }
        private sealed class GraphChoice
        {
            internal string Id,Name;
            internal int Purpose;
            public override string ToString(){return Name;}
        }
        private sealed class GraphView
        {
            internal PointF Center;
            internal float Zoom;
        }
        private readonly Dictionary<string,GraphView> graphViews=new Dictionary<string,GraphView>();
        private sealed class NavigationPoint {internal string FunctionId,EdgeId;internal string[] Nodes;internal PointF Center;internal float Zoom;}
        private readonly List<NavigationPoint> navigationBack=new List<NavigationPoint>();
        private FlowButton navigationBackButton;
        internal int NavigationHistoryCount {get{return navigationBack.Count;}}
        internal bool CanNavigateBack {get{return Project!=null&&navigationBack.Any(NavigationPointExists);}}
        private readonly ComboBox graphChoice=new FlowComboBox();
        private bool updatingGraphChoice;
        private FlowButton functionManager,callStackButton;
        private Panel graphBar;

        private void BuildFunctionNavigation(TableLayoutPanel center)
        {
            graphBar=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,BackColor=Ui.Paper,Tag="paper"};center.Controls.Add(graphBar,0,1);
            navigationBackButton=Ui.Button("←",36,()=>NavigateBack());navigationBackButton.Name="NavigationBack";navigationBackButton.AccessibleName="Torna al punto precedente";graphBar.Controls.Add(navigationBackButton);tips.SetToolTip(navigationBackButton,"Torna al punto visitato prima di un salto. Alt+Freccia sinistra. Non annulla le modifiche.");
            graphChoice.DropDownStyle=ComboBoxStyle.DropDownList;graphChoice.AccessibleName="Diagramma attivo: principale o funzione";graphChoice.Name="ActiveDiagram";graphChoice.SetBounds(20,3,210,30);graphBar.Controls.Add(graphChoice);
            functionManager=Ui.Button("Funzioni…",108,ManageFunctions);functionManager.Name="ManageFunctions";functionManager.AccessibleDescription="Crea una funzione, modifica parametri e risultato oppure apri il suo diagramma.";functionManager.SetBounds(240,0,108,35);graphBar.Controls.Add(functionManager);
            workspaceButton=Ui.Button("",164,ChooseWorkspaceLayout);workspaceButton.Name="WorkspaceLayout";graphBar.Controls.Add(workspaceButton);ApplyWorkspacePresentation();
            graphBar.Resize+=delegate{int width=Math.Max(S(75),Math.Min(S(280),graphBar.ClientSize.Width-S(359)));navigationBackButton.SetBounds(S(12),0,S(36),S(35));graphChoice.SetBounds(S(56),S(3),width,S(30));functionManager.SetBounds(width+S(64),0,S(108),S(35));workspaceButton.SetBounds(width+S(180),0,S(164),S(35));};
            graphChoice.SelectedIndexChanged+=delegate{if(updatingGraphChoice)return;if(Canvas.IsPointerGestureActive){RefreshFunctionChoices();return;}var choice=graphChoice.SelectedItem as GraphChoice;if(choice!=null)NavigateToOrigin(choice.Id,null);};
            RefreshNavigationAvailability();
        }
        private void RefreshFunctionChoices()
        {
            if(Project==null)return;updatingGraphChoice=true;
            try{graphChoice.Items.Clear();graphChoice.Items.Add(new GraphChoice{Name="Principale"});if(Project.Functions!=null)foreach(var f in Project.Functions)graphChoice.Items.Add(new GraphChoice{Id=f.Id,Name=f.Name+"("+String.Join(", ",f.Parameters.Select(p=>p.Name))+")"});graphChoice.SelectedIndex=0;for(int i=0;i<graphChoice.Items.Count;i++)if(((GraphChoice)graphChoice.Items[i]).Id==ActiveFunctionId){graphChoice.SelectedIndex=i;break;}}
            finally{updatingGraphChoice=false;RefreshNavigationAvailability();}
        }
        private bool NavigationPointExists(NavigationPoint point){if(Project==null)return false;var f=point.FunctionId==null?null:FunctionEditing.Find(Project,point.FunctionId);if(point.FunctionId!=null&&f==null)return false;var graph=f==null?Project:f.Body;return point.Nodes.Length>0?point.Nodes.Any(id=>graph.Nodes.Any(n=>n.Id==id)):String.IsNullOrEmpty(point.EdgeId)||graph.Edges.Any(e=>e.Id==point.EdgeId);}
        private NavigationPoint CaptureNavigation(){return new NavigationPoint{FunctionId=ActiveFunctionId,Nodes=Canvas.SelectedNodeIds.ToArray(),EdgeId=Canvas.SelectedEdgeId,Center=Canvas.ClientToWorld(new Point(Canvas.ClientSize.Width/2,Canvas.ClientSize.Height/2)),Zoom=Canvas.Zoom};}
        private static bool SameNavigation(NavigationPoint a,NavigationPoint b){return a.FunctionId==b.FunctionId&&a.EdgeId==b.EdgeId&&new HashSet<string>(a.Nodes).SetEquals(b.Nodes)&&Math.Abs(a.Zoom-b.Zoom)<.0001f&&Math.Abs(a.Center.X-b.Center.X)<.01f&&Math.Abs(a.Center.Y-b.Center.Y)<.01f;}
        private void RememberNavigation(NavigationPoint point){if(navigationBack.Count>0&&SameNavigation(navigationBack[navigationBack.Count-1],point))return;navigationBack.Add(point);if(navigationBack.Count>64)navigationBack.RemoveAt(0);RefreshNavigationAvailability();}
        private void RefreshNavigationAvailability(){bool available=Project!=null&&CanNavigateBack;if(navigationBackButton!=null){navigationBackButton.Enabled=available;navigationBackButton.AccessibleDescription=available?"Ripristina funzione, selezione e vista del punto precedente. Non annulla le modifiche.":"Disponibile dopo un salto a un altro blocco o diagramma.";}foreach(var menu in taskMenus.Values){var item=menu.Items.Find("NavigationBack",true).FirstOrDefault();if(item!=null)item.Enabled=available;}}
        internal void ResetNavigationHistory(){navigationBack.Clear();RefreshNavigationAvailability();}
        internal bool NavigateToOrigin(string functionId,string nodeId,bool search=false){
            if(Project==null||Canvas.IsPointerGestureActive)return false;var function=functionId==null?null:FunctionEditing.Find(Project,functionId);if(functionId!=null&&function==null)return false;var graph=function==null?Project:function.Body;if(nodeId!=null&&!graph.Nodes.Any(n=>n.Id==nodeId))return false;
            CommitInspector();PauseRun();var before=CaptureNavigation();ActivateFunction(functionId,true);if(nodeId!=null){Canvas.SelectNode(nodeId);if(search)Canvas.CenterSearchNode(nodeId);else Canvas.CenterOnNode(nodeId);}else if(search){Canvas.SelectNode(null);Canvas.CenterSearchNode(null);}if(!SameNavigation(before,CaptureNavigation())||search)RememberNavigation(before);Canvas.Focus();return true;
        }
        internal bool NavigateBack(){
            if(Project==null||Canvas.IsPointerGestureActive)return false;CommitInspector();PauseRun();while(navigationBack.Count>0){var point=navigationBack[navigationBack.Count-1];navigationBack.RemoveAt(navigationBack.Count-1);if(!NavigationPointExists(point))continue;ActivateFunction(point.FunctionId,false);changingDocument=true;try{Canvas.CancelConnection();Canvas.SelectNodes(point.Nodes);if(point.Nodes.Length==0&&!String.IsNullOrEmpty(point.EdgeId)&&Document.Edges.Any(e=>e.Id==point.EdgeId))Canvas.SelectEdge(point.EdgeId);Canvas.RestoreNavigationView(point.Zoom,point.Center);editingNodeId=null;}finally{changingDocument=false;}LoadInspector();RefreshNavigationAvailability();Canvas.Focus();SetStatus("Tornato al punto precedente. Le modifiche al diagramma sono conservate.");return true;}RefreshNavigationAvailability();SetStatus("Non ci sono altri punti disponibili nella navigazione.");return false;
        }
        internal bool HandleNavigationKey(Keys key){if(key!=(Keys.Alt|Keys.Left))return false;NavigateBack();return true;}
        internal void ActivateFunction(string id,bool animate)
        {
            id=FunctionEditing.Find(Project,id)==null?null:id;
            if(ActiveFunctionId==id&&Object.ReferenceEquals(Canvas.Diagram,Document))return;
            if(Canvas.IsPointerGestureActive)return;
            if(Canvas.ClientSize.Width>0)graphViews[ActiveFunctionId??""]=new GraphView{Zoom=Canvas.Zoom,Center=Canvas.ClientToWorld(new Point(Canvas.ClientSize.Width/2,Canvas.ClientSize.Height/2))};
            if(animate)FlowTransitions.Change(Canvas.Parent);
            changingDocument=true;
            try
            {
                Canvas.CancelConnection();ActiveFunctionId=id;Canvas.Diagram=Document;Canvas.SelectNode(null);Canvas.SelectEdge(null);Canvas.CurrentNodeId=null;Canvas.CurrentEdgeId=null;editingNodeId=null;
                Canvas.IssueNodeIds=new HashSet<string>(issues.Where(i=>i.Severity==Severity.Error&&i.FunctionId==id&&!String.IsNullOrEmpty(i.NodeId)).Select(i=>i.NodeId));
                GraphView view;if(graphViews.TryGetValue(id??"",out view)){Canvas.Zoom=view.Zoom;Canvas.CenterViewOn(view.Center);}else Canvas.FitToView();
            }
            finally{changingDocument=false;}
            LoadInspector();UpdateDocumentLabels();RefreshFunctionChoices();
        }
        private void ManageFunctions()
        {
            if(Canvas.IsPointerGestureActive)return;
            CommitInspector();PauseRun();Canvas.CancelConnection();
            var choices=new List<GraphChoice>{new GraphChoice{Name="+ Nuova funzione"},new GraphChoice{Name="Importa da un progetto…",Purpose=1}};
            if(Project.Functions!=null)choices.AddRange(Project.Functions.Select(f=>new GraphChoice{Id=f.Id,Name=f.Name}));
            if(Project.Functions!=null&&Project.Functions.Count>0)choices.Add(new GraphChoice{Name="Esporta una funzione…",Purpose=2});
            var chosen=TutorChoiceDialog.Choose(this,"Funzioni del progetto",choices.ToArray(),c=>c.Name,c=>c.Purpose==1?"Scegli un file .frociart e la funzione da riusare. Sono incluse anche le funzioni che richiama.":c.Purpose==2?"Salva una funzione con le sue dipendenze in un file .frociart riutilizzabile su PC e telefono.":c.Id==null?"Un diagramma riutilizzabile, con parametri e risultato.":FlowPseudocode.Signature(FunctionEditing.Find(Project,c.Id)));
            if(chosen==null)return;
            if(chosen.Purpose==1){ImportProjectFunction();return;}if(chosen.Purpose==2){ExportProjectFunction();return;}
            using(var editor=new FunctionEditorDialog(Project,FunctionEditing.Find(Project,chosen.Id)))
                if(editor.ShowDialog(this)==DialogResult.OK)ApplyFunctionProject(editor.EditedProject,editor.EditedId);
        }
        private void ImportProjectFunction(){
            using(var picker=new OpenFileDialog{Title="Importa una funzione da un progetto",Filter="Progetti Frociart (*.frociart)|*.frociart|Tutti i file (*.*)|*.*",CheckFileExists=true,RestoreDirectory=true}){
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                try{ChooseFunctionImport(DiagramIO.Load(picker.FileName));}catch(Exception e){if(!(e is IOException||e is UnauthorizedAccessException||e is FlowException))throw;ShowError("Importazione non riuscita",e);}
            }
        }
        internal void ChooseFunctionImport(Diagram source){
            CommitInspector();
            if(source.Functions==null||source.Functions.Count==0){SetStatus("Il file non contiene funzioni. Apri un progetto in cui hai già creato una funzione.");return;}
            var selected=TutorChoiceDialog.Choose(this,"Quale funzione vuoi importare?",source.Functions.ToArray(),f=>f.Name,f=>FlowPseudocode.Signature(f)+"\n\nLe funzioni richiamate verranno incluse automaticamente.");if(selected==null)return;
            var plan=FunctionTransfer.Import(Project,source,selected.Id);
            if(FlowMessageDialog.Choose(this,plan.Description,plan.Added.Count==1?"Importa funzione":"Importa "+plan.Added.Count+" funzioni","Importa","Annulla",false)==DialogResult.Yes)ApplyFunctionImport(plan);
        }
        internal void ApplyFunctionImport(FunctionTransfer.Plan plan){
            if(Convert.ToBase64String(DiagramIO.EncodeDocument(Project))!=plan.BaseDocument)throw new InvalidDataException("Il progetto è cambiato. Riapri l'importazione per conservare le modifiche più recenti.");
            CommitInspector();if(Convert.ToBase64String(DiagramIO.EncodeDocument(Project))!=plan.BaseDocument)throw new InvalidDataException("Hai modifiche più recenti nel pannello. Riapri l'importazione per conservarle.");
            ApplyFunctionProject(plan.Project,plan.SelectedId);SetStatus("Importazione completata. Ctrl+Z annulla l'intera importazione.");
        }
        private void ExportProjectFunction(){
            if(Project.Functions==null||Project.Functions.Count==0)return;
            var selected=TutorChoiceDialog.Choose(this,"Quale funzione vuoi esportare?",Project.Functions.ToArray(),f=>f.Name,f=>FlowPseudocode.Signature(f)+"\n\nIl file includerà le funzioni che richiama.");if(selected==null)return;
            try{var library=FunctionTransfer.Export(Project,selected.Id);
                using(var picker=new SaveFileDialog{Title="Esporta funzione riutilizzabile",Filter="Progetto Frociart (*.frociart)|*.frociart",DefaultExt="frociart",AddExtension=true,RestoreDirectory=true,FileName="Funzioni_"+selected.Name+".frociart"}){
                    if(picker.ShowDialog(this)!=DialogResult.OK)return;DiagramIO.Save(picker.FileName,library);SetStatus("Funzione esportata con le sue dipendenze. Puoi importarla da Funzioni su PC e telefono.");
                }
            }catch(Exception e){if(!(e is IOException||e is UnauthorizedAccessException||e is FlowException))throw;ShowError("Esportazione non riuscita",e);}
        }
        internal void ApplyFunctionProject(Diagram candidate,string id)
        {
            DiagramIO.CheckDocument(candidate);CommitInspector();PrepareMutation();Project=candidate;ActiveFunctionId=null;graphViews.Clear();ActivateFunction(id,true);MarkDirty();
            SetStatus(id==null?"Funzione eliminata. Ctrl+Z annulla.":"Funzione pronta. Costruisci il diagramma; richiama il suo nome da un blocco Chiamata o da un’espressione.");
        }
        private void InsertExpressionFunction()
        {
            if(SelectedNode!=null&&FlowTurtle.IsTurtle(SelectedNode.Kind)){using(var dialog=new TurtleEditorDialog(SelectedNode.Kind,expression.Text,FunctionModel.Catalog(Project)))if(dialog.ShowDialog(this)==DialogResult.OK){expression.Text=dialog.Result;CommitInspector();LoadInspector();}return;}
            if(SelectedNode!=null&&FileBlocks.IsFile(SelectedNode.Kind)){using(var dialog=new FileEditorDialog(SelectedNode.Kind,expression.Text,FunctionModel.Catalog(Project)))if(dialog.ShowDialog(this)==DialogResult.OK){expression.Text=dialog.Result;CommitInspector();LoadInspector();}return;}
            if(SelectedNode!=null&&SelectedNode.Kind==NodeKind.Declare){using(var dialog=new DeclarationEditorDialog(expression.Text,FunctionModel.Catalog(Project)))if(dialog.ShowDialog(this)==DialogResult.OK){expression.Text=dialog.Result;CommitInspector();LoadInspector();}return;}
            FunctionLibraryDialog.InsertInto(this,expression);
        }
        internal Func<bool> ExpressionLibraryGuard(TextBox target){var expected=Project;string scope=ActiveFunctionId,node=editingNodeId;return ()=>Object.ReferenceEquals(Project,expected)&&ActiveFunctionId==scope&&editingNodeId==node;}
        internal FlowExpressionLibrary.Catalog ExpressionLibraryContext(TextBox target){return FlowExpressionLibrary.Build(Project,ActiveFunctionId,Object.ReferenceEquals(target,expression)?editingNodeId:null,Object.ReferenceEquals(target,expression)?target.Text:null,false,FlowExpressionLibrary.ArgumentsAt(target.Text,target.SelectionStart,target.SelectionLength));}

        private void ShowCallStackFrames(){ShowVariableInspector();}
        private void FollowRuntimeFunction()
        {
            if(Runner==null){varsLabel.Text="VARIABILI · doppio clic per aprire";return;}ActivateFunction(Runner.CurrentFunctionId,speed.SelectedIndex!=3);
            if(callStackButton!=null){callStackButton.Text="Variabili e chiamate · "+Runner.CallDepth;callStackButton.AccessibleDescription="Mostra le chiamate sospese e le variabili locali di ciascuna chiamata.";}
            varsLabel.Text="VARIABILI · "+Runner.CurrentFunctionName;
        }
    }
}

namespace Frociart { public partial class FlowCanvas { internal void RestoreNavigationView(float zoom,System.Drawing.PointF center){arrangeCamera=false;Zoom=zoom;CenterViewOn(center);} } }
