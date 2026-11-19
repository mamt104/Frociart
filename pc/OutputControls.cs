using System;using System.Drawing;using System.Windows.Forms;
namespace Frociart {
 internal partial class MainForm {
  private CheckBox outputNewline;private bool updatingOutputNewline;
  private void InstallOutputEditor(){
   outputNewline=new CheckBox{Text="Vai a capo dopo la stampa",AutoSize=false,Visible=false,AccessibleName="Vai a capo dopo la stampa",AccessibleDescription="Disattiva per unire il risultato alla stampa successiva.",TabStop=true};inspector.Controls.Add(outputNewline);
   var order=new Control[]{titleEdit,cycleButton,expression,functionButton,prompt,outputNewline,comment,applyButton,nextButton,yesButton,noButton,branchStyleButton,disconnectButton,duplicateButton,deleteButton};for(int i=0;i<order.Length;i++)if(order[i]!=null&&order[i].Parent==inspector)order[i].TabIndex=i;
   outputNewline.CheckedChanged+=delegate{if(updatingOutputNewline||loadingInspector||changingDocument)return;var node=SelectedNode;if(node==null)return;try{expression.Text=FlowOutput.Toggle(expression.Text,node.Kind,outputNewline.Checked);FlowTransitions.Change(inspector);}catch(FlowException ex){UpdateOutputEditor(node);SetStatus(ex.Message);}};
  }
  private void UpdateOutputEditor(FlowNode node){
   if(outputNewline==null)return;updatingOutputNewline=true;try{NodeKind kind;string text;IoInstruction.Parse(node==null?NodeKind.Start:node.Kind,expression.Text,out kind,out text);outputNewline.Visible=node!=null&&IoInstruction.IsIo(node.Kind)&&kind==NodeKind.Output;outputNewline.Checked=FlowOutput.Newline(text);outputNewline.ForeColor=Ui.Ink;outputNewline.BackColor=inspector.BackColor;}finally{updatingOutputNewline=false;}
  }
 }
}
