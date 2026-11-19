using System;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;

namespace Frociart {
 internal sealed class VariableRenameDialog:Form {
  internal readonly ComboBox Variable=new FlowComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
  internal readonly TextBox NewName=Ui.TextBox(),Before=Ui.TextBox(true),After=Ui.TextBox(true);
  internal readonly ListBox Changes=new ListBox{IntegralHeight=false,BorderStyle=BorderStyle.None};
  internal readonly FlowButton Preview,Apply;internal readonly Label Status=Ui.Label("Scegli il nuovo nome, poi controlla l'anteprima.",10,Ui.Muted);
  internal FlowVariableRename.Plan Plan {get;private set;}
  readonly Diagram source;readonly string scope;
  internal VariableRenameDialog(Diagram project,string scope){
   source=DiagramIO.Clone(project);this.scope=scope;
   Text="Rinomina variabile — Frociart";Font=Ui.Font();BackColor=Ui.Surface;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;MinimumSize=new Size(580,540);Size=new Size(760,680);AutoScaleMode=AutoScaleMode.Dpi;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=7};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   foreach(var row in new[]{new RowStyle(SizeType.Absolute,68),new RowStyle(SizeType.Absolute,76),new RowStyle(SizeType.Absolute,46),new RowStyle(SizeType.Absolute,52),new RowStyle(SizeType.Percent,40),new RowStyle(SizeType.Percent,60),new RowStyle(SizeType.Absolute,52)})root.RowStyles.Add(row);Controls.Add(root);
   var owner=FunctionEditing.Find(source,scope);var intro=Ui.Label("Rinomina variabile\nFoglio: "+(owner==null?"Principale":owner.Name),14,Ui.Ink,true);intro.Dock=DockStyle.Fill;root.Controls.Add(intro,0,0);
   var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));fields.RowStyles.Add(new RowStyle(SizeType.Absolute,26));fields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   fields.Controls.Add(Ui.Label("Variabile",10,Ui.Ink,true),0,0);fields.Controls.Add(Ui.Label("Nuovo nome",10,Ui.Ink,true),1,0);
   Variable.Dock=DockStyle.Top;Variable.AccessibleName="Variabile da rinominare nel foglio";Variable.Items.AddRange(FlowVariableRename.Names(source,scope));fields.Controls.Add(Variable,0,1);
   NewName.Dock=DockStyle.Top;NewName.AccessibleName="Nuovo nome della variabile";NewName.MaxLength=64;fields.Controls.Add(NewName,1,1);root.Controls.Add(fields,0,1);
   var help=Ui.Label("Aggiorna i riferimenti nel foglio. Testi tra virgolette, note e altre funzioni restano invariati. Annulla ripristina tutta la modifica.",9.5f,Ui.Muted);help.Dock=DockStyle.Fill;root.Controls.Add(help,0,2);
   Status.Dock=DockStyle.Fill;Status.AccessibleName="Esito dell'anteprima della rinomina";root.Controls.Add(Status,0,3);
   Changes.Dock=DockStyle.Fill;Changes.AccessibleName="Modifiche previste alla variabile";Changes.Font=Ui.Font(10);Changes.Format+=(s,e)=>{var change=e.ListItem as FlowVariableRename.Change;if(change!=null){var node=FunctionEditing.Graph(source,scope).Nodes.FirstOrDefault(n=>n.Id==change.NodeId);e.Value=(node==null?change.Field:Ui.KindName(node.Kind)+" · "+change.Field)+"  ·  "+FlowInspectExpression.Truncate(change.Before.Replace('\n',' ').Replace('\r',' '),80);}};Changes.FormattingEnabled=true;Changes.SelectedIndexChanged+=(s,e)=>Describe();root.Controls.Add(Changes,0,4);
   var comparison=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));comparison.RowStyles.Add(new RowStyle(SizeType.Absolute,26));comparison.RowStyles.Add(new RowStyle(SizeType.Percent,100));comparison.Controls.Add(Ui.Label("Prima",10,Ui.Muted,true),0,0);comparison.Controls.Add(Ui.Label("Dopo",10,Ui.Ink,true),1,0);
   foreach(var box in new[]{Before,After}){box.ReadOnly=true;box.Dock=DockStyle.Fill;box.Font=Ui.Mono(10.5f);box.WordWrap=true;box.ScrollBars=ScrollBars.Vertical;box.MaxLength=Int32.MaxValue;box.Margin=new Padding(3,4,6,6);}Before.AccessibleName="Testo prima della rinomina";After.AccessibleName="Testo dopo la rinomina";comparison.Controls.Add(Before,0,1);comparison.Controls.Add(After,1,1);root.Controls.Add(comparison,0,5);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};Preview=Ui.Button("Mostra modifiche",180,Prepare);Apply=Ui.Button("Rinomina",125,Choose,true);Apply.Enabled=false;var cancel=Ui.Button("Annulla",100,Close);actions.Controls.Add(Preview);actions.Controls.Add(Apply);actions.Controls.Add(cancel);root.Controls.Add(actions,0,6);AcceptButton=Preview;CancelButton=cancel;
   NewName.TextChanged+=(s,e)=>InvalidatePreview();Variable.SelectedIndexChanged+=(s,e)=>{NewName.Text=Variable.SelectedItem as string??"";InvalidatePreview();};if(Variable.Items.Count>0)Variable.SelectedIndex=0;Preview.Enabled=Variable.Items.Count>0;
   Ui.ApplyTheme(this);FlowFormMotion.Attach(this);FlowConsoleMotion.AttachExit(this);Shown+=(s,e)=>{Ui.FitStartupToScreen(this);NewName.Focus();NewName.SelectAll();};
  }
  void InvalidatePreview(){Plan=null;Apply.Enabled=false;Changes.Items.Clear();Before.Clear();After.Clear();Status.ForeColor=Ui.Muted;Status.Text="Scegli il nuovo nome, poi controlla l'anteprima.";AcceptButton=Preview;}
  internal void Prepare(){
   Plan=null;Apply.Enabled=false;try{Plan=FlowVariableRename.Prepare(source,scope,Variable.SelectedItem as string,NewName.Text.Trim());FlowTransitions.Change(Changes);Changes.Items.Clear();Changes.Items.AddRange(Plan.Changes.Cast<object>().ToArray());if(Changes.Items.Count>0)Changes.SelectedIndex=0;Status.ForeColor=Ui.Muted;Status.Text=Plan.Summary;Apply.Enabled=true;AcceptButton=Apply;}
   catch(Exception error){if(!(error is FlowException||error is InvalidDataException||error is ArgumentException))throw;Changes.Items.Clear();Before.Clear();After.Clear();Status.ForeColor=Ui.Red;Status.Text=error.Message;NewName.Focus();}
  }
  void Describe(){var change=Changes.SelectedItem as FlowVariableRename.Change;Before.Text=change==null?"":change.Before;After.Text=change==null?"":change.After;}
  void Choose(){if(Plan==null)return;DialogResult=DialogResult.OK;Close();}
 }
}
