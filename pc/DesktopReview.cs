using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Frociart {
 internal sealed partial class MainForm {
  private bool canvasFocus;
  private FlowButton focusCanvasButton;
  private float paletteWidth=198,detailsWidth=324;
  private void SetCanvasFocus(bool value){
   if(editorBody==null||canvasFocus==value)return;
   if(Canvas.IsPointerGestureActive)return;
   CommitInspector();FlowTransitions.Change(editorBody);
   var anchor=Canvas.ClientToWorld(new Point(Canvas.ClientSize.Width/2,Canvas.ClientSize.Height/2));
   if(value){paletteWidth=editorBody.ColumnStyles[0].Width;detailsWidth=editorBody.ColumnStyles[2].Width;}
   canvasFocus=value;
   editorBody.GetControlFromPosition(0,0).Visible=!value;editorBody.GetControlFromPosition(2,0).Visible=!value;tabs.Visible=!value;
   editorBody.ColumnStyles[0].Width=value?0:paletteWidth;editorBody.ColumnStyles[2].Width=value?0:detailsWidth;
   var center=titlePanel.Parent as TableLayoutPanel;
   if(center!=null){titlePanel.Visible=!value;contextPanel.Visible=!value;center.RowStyles[0].Height=value?0:64;center.RowStyles[1].Height=value?0:42;graphBar.Visible=!value;center.RowStyles[2].Height=value?0:57;center.PerformLayout();if(!value)ArrangeWorkspaceHeader();}
   editorBody.PerformLayout();Canvas.CenterViewOn(anchor);UpdateContext();
   if(focusCanvasButton!=null){focusCanvasButton.Text=value?"Pannelli · F8":"Solo foglio";focusCanvasButton.AccessibleName=value?"Mostra i pannelli, F8":"Nascondi i pannelli, F8";}
   Canvas.Focus();SetStatus(value?"Solo foglio. F8 ripristina i pannelli. Doppio clic su un blocco per modificarlo.":"Pannelli ripristinati. Il diagramma e l'esecuzione sono conservati.");
  }
  private void RenameDocumentVariable(){
   CommitInspector();if(FlowVariableRename.Names(Project,ActiveFunctionId).Length==0){SetStatus("Non ci sono ancora variabili. Aggiungi un Input o un'Operazione.");return;}
   using(var dialog=new VariableRenameDialog(Project,ActiveFunctionId))if(dialog.ShowDialog(this)==DialogResult.OK&&dialog.Plan!=null)ApplyVariableRenamePlan(dialog.Plan);
  }
  private bool ApplyVariableRename(string oldName,string newName){
   CommitInspector();try{return ApplyVariableRenamePlan(FlowVariableRename.Prepare(Project,ActiveFunctionId,oldName,newName));}
   catch(Exception error){if(!(error is FlowException||error is System.IO.InvalidDataException||error is ArgumentException))throw;SetStatus("Nome non cambiato: "+error.Message);return false;}
  }
  private bool ApplyVariableRenamePlan(FlowVariableRename.Plan plan){
   CommitInspector();Diagram candidate;
   try{candidate=plan.Apply(Project,ActiveFunctionId);}catch(Exception error){if(!(error is FlowException||error is System.IO.InvalidDataException||error is ArgumentException))throw;SetStatus("Nome non cambiato: "+error.Message);return false;}
   var selected=Canvas.SelectedNodeIds.ToArray();PrepareMutation();FlowTransitions.Change(Canvas);changingDocument=true;
   try{Project=candidate;Canvas.Diagram=Document;Canvas.SelectNodes(selected);editingNodeId=null;RefreshFunctionChoices();}finally{changingDocument=false;}
   MarkDirty();LoadInspector();SetStatus("Variabile rinominata · "+plan.Summary+". Ctrl+Z annulla l'intera modifica.");return true;
  }
  private void ShowShortcuts(){TutorTextDialog.ShowText(this,"Comandi rapidi","F5 — esegui o riprendi\nF6 — verifica struttura e sintassi\nF10 — esegui un blocco\nF8 — solo foglio / pannelli\n\nCtrl+K — cerca un comando nei menu\nCtrl+F — cerca testo, variabili, note e rami nel progetto\nCtrl+S — salva progetto\nCtrl+Maiusc+S — salva con nome\nCtrl+Z / Ctrl+Y — annulla / ripeti\nCtrl+C / Ctrl+X / Ctrl+V — copia / taglia / incolla i blocchi\nCtrl+A — seleziona tutti i blocchi\nCtrl+D — duplica la selezione\nCanc — elimina la selezione\nInvio — modifica il blocco selezionato\n\nRotella — zoom sul puntatore\nRotella premuta — sposta il foglio\nDestro — menu contestuale\nAlt durante uno spostamento — ignora la griglia\n\nQuando scrivi in un campo, selezione, eliminazione e annullamento agiscono sul testo.");}
 }
 internal sealed partial class TutorDialog {
  private FlowButton finishTrial;
  private Label trialState,trialOutputLabel;
  private string editingSelectionBeforeTrial;
  internal Diagram EditorDocument {get;private set;}
  private void SyncTrialMode(bool trial){
   if(finishTrial!=null)finishTrial.Visible=trial;
   if(canvas==null||canvas.IsDisposed)return;
   if(trial&&!canvas.ViewOnly){editingSelectionBeforeTrial=canvas.SelectedNodeId;canvas.ViewOnly=true;canvas.SelectNode(null);}
   else if(!trial&&canvas.ViewOnly){canvas.ViewOnly=false;if(editingSelectionBeforeTrial!=null&&Tutor.Find(session.Document,editingSelectionBeforeTrial)!=null)canvas.SelectNode(editingSelectionBeforeTrial);}
  }
  private void FinishTrial(){if(inspectorTabs!=null){inspectorTabs.SelectedIndex=0;if(canvas!=null)canvas.Focus();}}
  private void OpenInFreeEditor(){
   if(checking||session==null)return;StashDraft();var candidate=TutorStore.Copy(session);
   foreach(var draft in candidate.Drafts){var node=Tutor.Find(candidate.Document,draft.Node);if(node!=null){node.Text=draft.Text;node.Prompt=draft.Prompt;}}
   candidate.Drafts.Clear();if(!store.Save(candidate)){notice.Text=store.Error;return;}
   session=candidate;editingId=null;saved=true;EditorDocument=DiagramIO.Clone(candidate.Document);DialogResult=DialogResult.OK;Close();
  }
  private void RefreshTrialState(){
   if(trialState==null||trialState.IsDisposed)return;
   var state=runner==null?RuntimeState.Ready:runner.State;
   trialState.Text=state==RuntimeState.Completed?"Prova terminata":state==RuntimeState.Error?"C'è un problema da correggere":state==RuntimeState.WaitingForInput?"Inserisci il dato richiesto":runner==null?"Pronto per la prova":"Passo "+runner.Steps+" · segui il blocco blu";
   trialState.ForeColor=state==RuntimeState.Completed?Ui.Green:state==RuntimeState.Error?Ui.Red:Ui.Blue;
   stepRun.Text=state==RuntimeState.Completed?"Terminata":state==RuntimeState.Error?"Da rivedere":"Avanti";
   if(trialOutputLabel!=null)trialOutputLabel.Text=runner!=null&&runner.Output.Count>0?"RISULTATO · "+runner.Output.Count+" "+(runner.Output.Count==1?"riga":"righe"):"RISULTATO · apparirà quando esegui un Output";
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
   if(working&&!checking&&canvas!=null&&canvas.ContainsFocus){
    if(canvas.HandleClipboardShortcut(keyData))return true;
    bool undoKey=keyData==(Keys.Control|Keys.Z),redoKey=keyData==(Keys.Control|Keys.Y)||keyData==(Keys.Control|Keys.Shift|Keys.Z);
    if(undoKey||redoKey){if(!canvas.ViewOnly&&!canvas.IsPointerGestureActive){if(undoKey)Undo();else Redo();}return true;}
   }
   if(working&&!checking&&inspectorTabs!=null){
    if(keyData==Keys.Escape&&inspectorTabs.SelectedIndex==1){FinishTrial();return true;}
    if(keyData==(Keys.Control|Keys.Enter)){
     if(inspectorTabs.SelectedIndex==1){if(runner!=null&&runner.State==RuntimeState.WaitingForInput)SubmitRun();else StepRunner();}
     else Primary();return true;
    }
    if(keyData==Keys.F10&&inspectorTabs.SelectedIndex==1){StepRunner();return true;}
   }
   return base.ProcessCmdKey(ref msg,keyData);
  }
 }
}
