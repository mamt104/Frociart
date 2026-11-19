using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed class BreakpointDialog:Form
    {
        internal readonly CheckBox EnabledPoint=new CheckBox{Text="Ferma l'esecuzione prima di questo blocco",AutoSize=true,ForeColor=Ui.Ink};
        internal readonly TextBox Condition=Ui.TextBox();
        internal readonly Label Error=Ui.Label("",10,Ui.Red);
        internal readonly FlowButton Apply;
        internal BreakpointDialog(FlowNode node)
        {
            Text="Punto di interruzione — Frociart";Size=new Size(560,390);MinimumSize=new Size(450,370);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font();BackColor=Ui.Surface;
            var scroll=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22)};Controls.Add(scroll);
            var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(layout);layout.RowCount=0;
            Action<Control,int> add=(c,h)=>{int row=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));c.Dock=DockStyle.Fill;layout.Controls.Add(c,0,row);};
            EnabledPoint.Checked=node.Breakpoint;EnabledPoint.AccessibleName=EnabledPoint.Text;add(EnabledPoint,40);
            add(Ui.Label("Condizione facoltativa · vuota significa sempre",10,Ui.Ink,true),28);
            Condition.Text=node.BreakpointCondition??"";Condition.MaxLength=8192;Condition.AccessibleName="Condizione del punto di interruzione";add(Condition,40);
            add(Ui.Label("Esempio: contatore >= 5. Usa le variabili locali e le funzioni integrate, esclusa random. Le funzioni del progetto non sono ammesse nelle condizioni di pausa.",10,Ui.Muted),64);add(Error,58);
            Condition.TextChanged+=delegate{Error.Text="";};
            var footer=new FlowLayoutPanel{WrapContents=false};Apply=Ui.Button("Applica",120,Save,true);footer.Controls.Add(Apply);var cancel=Ui.Button("Annulla",110,Close);footer.Controls.Add(cancel);add(footer,46);AcceptButton=Apply;CancelButton=cancel;Ui.ApplyTheme(this);
        }
        internal void Save(){try{if(EnabledPoint.Checked)new FlowDebugger.DebugCondition(Condition.Text);DialogResult=DialogResult.OK;Close();}catch(FlowException ex){Error.Text=ex.Message;Condition.Focus();}}
    }
    internal sealed partial class MainForm
    {
        private FlowDebugger debugger;
        private bool ignoreBreakpoints;
        private FlowButton debuggerButton;
        private FlowContextMenu debuggerMenu;
        private FlowDebugger GetDebugger()
        {
            if(Runner==null)return null;
            if(debugger==null||!Object.ReferenceEquals(debugger.Runner,Runner))debugger=new FlowDebugger(Runner,Project){IgnoreBreakpoints=ignoreBreakpoints};
            return debugger;
        }
        internal void DebugStep(FlowDebugger.Command command)
        {
            if(Canvas.IsPointerGestureActive)return;
            SetCanvasFocus(false);if(!EnsureRunner())return;tabs.SelectedTab=runTab;
            if(Runner.State==RuntimeState.WaitingForFile){RefreshRuntime();runtimeFileAction.Focus();return;}
            runTimer.Stop();automatic=false;
            var control=GetDebugger();if(!control.Begin(command)){SetStatus("Il comando richiede una funzione in esecuzione.");return;}
            if(Runner.State==RuntimeState.WaitingForInput){automatic=true;inputValue.Focus();RefreshRuntime();return;}
            control.Tick();
            if(control.Active){automatic=true;if(Runner.State!=RuntimeState.WaitingForInput&&Runner.State!=RuntimeState.WaitingForFile){runTimer.Interval=15;runTimer.Start();}}
            RefreshRuntime();
        }
        private bool CanRunToSelected(){var node=Document.Nodes.FirstOrDefault(n=>n.Id==Canvas.SelectedNodeId);return Canvas.SelectionCount==1&&node!=null&&node.Kind!=NodeKind.Note&&!Canvas.IsPointerGestureActive&&!Canvas.IsConnecting&&(Runner==null||Runner.State!=RuntimeState.WaitingForInput&&Runner.State!=RuntimeState.WaitingForFile);}
        internal void RunToSelected(){
            if(!CanRunToSelected()){SetStatus("Seleziona un solo blocco eseguibile. Completa prima eventuali richieste di input o file.");return;}
            string node=Canvas.SelectedNodeId,function=ActiveFunctionId;EnterEditor();SetCanvasFocus(false);if(!EnsureRunner())return;
            runTimer.Stop();automatic=false;if(!GetDebugger().RunTo(function,node)){SetStatus("Il blocco da raggiungere non è disponibile nella prova.");return;}
            tabs.SelectedTab=runTab;automatic=true;runTimer.Interval=15;runTimer.Start();RefreshRuntime();
        }
        private FlowNode DebugSelectedNode(){return Document.Nodes.FirstOrDefault(n=>n.Id==Canvas.SelectedNodeId)??(Runner!=null&&Runner.CurrentFunctionId==ActiveFunctionId?Document.Nodes.FirstOrDefault(n=>n.Id==Runner.CurrentNodeId):null);}
        private void PushDebuggerHistory(){undo.Push(new ProjectHistory(Project,ActiveFunctionId));if(undo.Count>80){var kept=undo.Take(80).Reverse().ToArray();undo.Clear();foreach(var history in kept)undo.Push(history);}redo.Clear();}
        internal void SetBreakpoint(FlowNode node,bool enabled,string condition)
        {
            if(node==null||node.Kind==NodeKind.Note||Canvas.IsPointerGestureActive)return;
            if(enabled)new FlowDebugger.DebugCondition(condition);
            if(node.Breakpoint==enabled&&(node.BreakpointCondition??"")==condition)return;
            PauseRun();PushDebuggerHistory();
            node.Breakpoint=enabled;node.BreakpointCondition=condition;
            if(GetDebugger()!=null)debugger.UpdateBreakpoints(Project);
            if(node.Kind==NodeKind.Pause&&editingNodeId==node.Id)LoadInspector();
            MarkDirty();Canvas.Invalidate();RefreshRuntime();
        }
        private void EditBreakpoint()
        {
            if(Canvas.IsPointerGestureActive)return;CommitInspector();var node=DebugSelectedNode();
            if(node!=null&&node.Kind==NodeKind.Note){SetStatus("Le note non vengono eseguite e non hanno punti di interruzione.");return;}
            if(node==null){SetStatus("Seleziona un blocco per impostare il punto di interruzione.");return;}
            PauseRun();using(var dialog=new BreakpointDialog(node)){if(!node.Breakpoint&&node.Kind!=NodeKind.Pause)dialog.EnabledPoint.Checked=true;if(dialog.ShowDialog(this)==DialogResult.OK)SetBreakpoint(node,dialog.EnabledPoint.Checked,dialog.Condition.Text);}
        }
        private void ToggleBreakpoint(){CommitInspector();var n=DebugSelectedNode();if(n==null)return;try{SetBreakpoint(n,!n.Breakpoint,n.BreakpointCondition??"");}catch(FlowException){EditBreakpoint();}}
        internal void ClearBreakpoints()
        {
            var graphs=new[]{Project}.Concat(Project.Functions==null?Enumerable.Empty<Diagram>():Project.Functions.Select(f=>f.Body)).ToArray();
            if(!graphs.SelectMany(g=>g.Nodes).Any(n=>n.Breakpoint||!String.IsNullOrEmpty(n.BreakpointCondition)))return;
            PauseRun();PushDebuggerHistory();foreach(var n in graphs.SelectMany(g=>g.Nodes)){n.Breakpoint=false;n.BreakpointCondition=null;}
            if(GetDebugger()!=null)debugger.UpdateBreakpoints(Project);LoadInspector();MarkDirty();Canvas.Invalidate();RefreshRuntime();SetStatus("Punti disattivati. I blocchi Pausa restano sul foglio; Annulla ripristina le pause.");
        }
        private void ShowDebuggerMenu()
        {
            if(Canvas.IsPointerGestureActive)return;PauseRun();if(debuggerMenu!=null&&!debuggerMenu.IsDisposed)debuggerMenu.Close();var menu=new FlowContextMenu();debuggerMenu=menu;
            FlowMenus.Command(menu,this,"Avanti entrando nelle funzioni · F11",()=>DebugStep(FlowDebugger.Command.Into));
            FlowMenus.Command(menu,this,"Avanti senza entrare · F10",()=>DebugStep(FlowDebugger.Command.Over));
            var exit=FlowMenus.Command(menu,this,"Torna al chiamante · Maiusc+F11",()=>DebugStep(FlowDebugger.Command.Out));exit.Enabled=Runner!=null&&Runner.CallDepth>1;
            var reach=FlowMenus.Command(menu,this,"Esegui fino al blocco selezionato · Ctrl+F10",RunToSelected);reach.Enabled=CanRunToSelected();
            menu.Items.Add(new ToolStripSeparator());
            FlowMenus.Command(menu,this,"Punto di interruzione del blocco…",EditBreakpoint);
            var ignore=FlowMenus.Command(menu,this,"Ignora i punti di interruzione",()=>{ignoreBreakpoints=!ignoreBreakpoints;if(GetDebugger()!=null)debugger.IgnoreBreakpoints=ignoreBreakpoints;SetStatus(ignoreBreakpoints?"Le pause impostate saranno ignorate.":"Le pause impostate sono attive.");});ignore.Checked=ignoreBreakpoints;
            FlowMenus.Command(menu,this,"Disattiva tutti i punti di interruzione",ClearBreakpoints);
            FlowMenus.ShowTransient(menu,this,debuggerButton.PointToScreen(new Point(0,debuggerButton.Height)));
        }
    }
}
