using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Frociart
{
    // All edits are prepared on an isolated project. A failed signature, rename or
    // deletion never changes the document currently being edited.
    internal static class FunctionEditing
    {
        internal static FlowFunction Find(Diagram project, string id)
        { return project==null||project.Functions==null?null:project.Functions.FirstOrDefault(f=>f.Id==id); }
        internal static Diagram Graph(Diagram project, string id)
        { var f=Find(project,id);return f==null?project:f.Body; }
        internal static IEnumerable<Diagram> Graphs(Diagram project)
        { yield return project;if(project.Functions!=null)foreach(var f in project.Functions)yield return f.Body; }
        internal static string RewriteCalls(string text,string oldName,string newName)
        { return FlowVariableRename.RewriteSymbols(text,new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{oldName,newName}},true); }

        internal static Diagram Save(Diagram project,FlowFunction edited,IDictionary<string,string> renamedVariables)
        {
            var candidate=DiagramIO.Clone(project);var previous=Find(candidate,edited.Id);var next=edited.Copy();
            if(candidate.Functions==null)candidate.Functions=new List<FlowFunction>();
            if(previous==null)candidate.Functions.Add(next);else candidate.Functions[candidate.Functions.IndexOf(previous)]=next;
            FlowAnnotations.Promote(candidate);FunctionModel.CheckProject(candidate);
            if(renamedVariables!=null&&renamedVariables.Count>0)
            {
                foreach(var rename in renamedVariables)
                {
                    if(!ExpressionLanguage.Identifier(rename.Key)||!ExpressionLanguage.Identifier(rename.Value))throw new FlowException("Scegli nomi validi per parametri e risultato.");
                    if(!renamedVariables.ContainsKey(rename.Value))
                    {
                        var probe=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{rename.Value,"__existing_local__"}};
                        if(next.Body.Nodes.Where(n=>n.Kind!=NodeKind.Start&&n.Kind!=NodeKind.End&&n.Kind!=NodeKind.Note).Any(n=>FlowDeclaration.RewriteNode(n,probe)!=(n.Text??"")))
                            throw new FlowException("«"+rename.Value+"» è già usata nel diagramma. Scegli un nome libero per non unire due variabili.");
                    }
                }
                foreach(var node in next.Body.Nodes.Where(n=>n.Kind!=NodeKind.Start&&n.Kind!=NodeKind.End&&n.Kind!=NodeKind.Note))node.Text=FlowDeclaration.RewriteNode(node,renamedVariables);
                foreach(var node in next.Body.Nodes)if(node.BreakpointCondition!=null)node.BreakpointCondition=TutorVariables.Rewrite(node.BreakpointCondition,renamedVariables);
            }
            if(previous!=null&&previous.Name!=next.Name)
                foreach(var graph in Graphs(candidate))foreach(var node in graph.Nodes.Where(n=>n.Kind!=NodeKind.Start&&n.Kind!=NodeKind.End&&n.Kind!=NodeKind.Note))
                    node.Text=RewriteCalls(node.Text,previous.Name,next.Name);
            FunctionModel.CheckProject(candidate);return candidate;
        }
        internal static Diagram Delete(Diagram project,string id)
        {
            var candidate=DiagramIO.Clone(project);var f=Find(candidate,id);if(f==null)throw new FlowException("La funzione non esiste più.");
            foreach(var graph in Graphs(candidate).Where(g=>g!=f.Body))foreach(var n in graph.Nodes.Where(n=>n.Kind!=NodeKind.Start&&n.Kind!=NodeKind.End&&n.Kind!=NodeKind.Note))
                if(RewriteCalls(n.Text,f.Name,"__removed_function__")!=(n.Text??""))
                    throw new FlowException("«"+f.Name+"» è ancora chiamata da un altro diagramma. Rimuovi prima le chiamate, poi elimina la funzione.");
            candidate.Functions.Remove(f);if(candidate.Functions.Count==0){candidate.Functions=null;FlowAnnotations.Promote(candidate);}
            FunctionModel.CheckProject(candidate);return candidate;
        }
        internal static string NewName(Diagram project)
        {for(int i=1;;i++){string name=i==1?"miaFunzione":"miaFunzione"+i;if(project.Functions==null||!project.Functions.Any(f=>String.Equals(f.Name,name,StringComparison.OrdinalIgnoreCase)))return name;}}
    }

    internal sealed class FunctionEditorDialog : Form
    {
        internal readonly TextBox FunctionName=Ui.TextBox(), ResultName=Ui.TextBox();
        internal readonly ComboBox ReturnType=new FlowComboBox();
        internal readonly CheckBox ReturnArray=new CheckBox{ForeColor=Ui.Ink};
        internal readonly FlowButton Apply;
        internal readonly List<ParameterRow> Parameters=new List<ParameterRow>();
        internal Diagram EditedProject;
        internal string EditedId;
        private readonly Diagram project;
        private readonly FlowFunction original;
        private readonly FlowScrollPanel parameterPanel=new FlowScrollPanel();
        private readonly Label error=Ui.Label("",9,Ui.Red),resultLabel=Ui.Label("Variabile restituita",9,Ui.Muted,true);
        private FlowButton add;
        private bool arrangingRows;
        internal sealed class ParameterRow
        {
            internal string OldName;
            internal Panel Host;
            internal TextBox Name;
            internal ComboBox Type;
            internal CheckBox Array,Reference;
        }
        private static readonly string[] Types={"Automatico","Intero","Reale","Testo","Logico","Nessuno"};
        internal FunctionEditorDialog(Diagram source,FlowFunction function)
        {
            project=DiagramIO.Clone(source);original=function==null?new FlowFunction{Name=FunctionEditing.NewName(source)}:function.Copy();EditedId=original.Id;
            Text=(function==null?"Nuova funzione":"Modifica funzione")+" — Frociart";Size=new Size(760,750);MinimumSize=new Size(650,610);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;
            AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font();BackColor=Ui.Surface;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=8};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(float h in new[]{64f,65f,43f,-1f,42f,106f,55f,44f})layout.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));Controls.Add(layout);
            var intro=Ui.Label("Un diagramma che puoi richiamare\nI parametri portano i dati dentro; il risultato li riporta al chiamante.",12,Ui.Ink,true);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,0);
            var namePanel=new Panel{Dock=DockStyle.Fill};var label=Ui.Label("Nome della funzione",9,Ui.Muted,true);label.SetBounds(0,0,400,22);namePanel.Controls.Add(label);FunctionName.SetBounds(0,26,500,28);FunctionName.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;FunctionName.MaxLength=64;FunctionName.Text=original.Name;FunctionName.AccessibleName=label.Text;namePanel.Controls.Add(FunctionName);layout.Controls.Add(namePanel,0,1);
            var headings=Ui.Label("PARAMETRI   ·   nome, tipo e vettore (opzionale)",9,Ui.Muted,true);headings.Dock=DockStyle.Fill;headings.Padding=new Padding(0,14,0,0);layout.Controls.Add(headings,0,2);
            parameterPanel.Dock=DockStyle.Fill;parameterPanel.AutoScroll=true;parameterPanel.AccessibleName="Parametri in ordine di chiamata";layout.Controls.Add(parameterPanel,0,3);
            add=Ui.Button("+ Aggiungi parametro",200,delegate{AddParameter(new FlowParameter{Name=FreeParameterName()},null);FlowTransitions.Change(parameterPanel);},false);layout.Controls.Add(add,0,4);
            var result=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Margin=Padding.Empty};result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));result.RowStyles.Add(new RowStyle(SizeType.Absolute,25));result.RowStyles.Add(new RowStyle(SizeType.Absolute,34));result.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            result.Controls.Add(Ui.Label("RISULTATO",9,Ui.Muted,true),0,0);result.Controls.Add(resultLabel,1,0);ReturnType.DropDownStyle=ComboBoxStyle.DropDownList;ReturnType.Items.AddRange(Types);ReturnType.SelectedIndex=(int)original.ReturnType;ReturnType.AccessibleName="Tipo del risultato";ReturnType.Dock=DockStyle.Fill;result.Controls.Add(ReturnType,0,1);
            ResultName.MaxLength=64;ResultName.Text=original.ReturnVariable;ResultName.AccessibleName="Variabile restituita alla fine della funzione";ResultName.Dock=DockStyle.Fill;result.Controls.Add(ResultName,1,1);ReturnArray.Text="Vettore";ReturnArray.Checked=original.ReturnIsArray;ReturnArray.Dock=DockStyle.Fill;ReturnArray.AccessibleName="Il risultato è un vettore";result.Controls.Add(ReturnArray,2,1);
            var help=Ui.Label("Assegna il risultato nel diagramma: viene restituito quando raggiungi Fine.\nPer valore la funzione usa una copia. Modifica l’originale aggiorna gli elementi del vettore nel chiamante.",9,Ui.Muted);help.Dock=DockStyle.Fill;result.Controls.Add(help,0,2);result.SetColumnSpan(help,3);layout.Controls.Add(result,0,5);
            error.Dock=DockStyle.Fill;error.AccessibleName="Errore nella definizione della funzione";layout.Controls.Add(error,0,6);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};Apply=Ui.Button("Apri il diagramma",180,Save,true);Apply.Name="SaveFunction";footer.Controls.Add(Apply);var cancel=Ui.Button("Annulla",95,Close);footer.Controls.Add(cancel);CancelButton=cancel;AcceptButton=Apply;
            if(function!=null)footer.Controls.Add(Ui.Button("Elimina funzione",150,Delete));layout.Controls.Add(footer,0,7);
            foreach(var p in original.Parameters)AddParameter(p,p.Name);parameterPanel.Resize+=delegate{ArrangeRows();};ReturnType.SelectedIndexChanged+=delegate{UpdateResult();};UpdateResult();Ui.ApplyTheme(this);
            Shown+=delegate{ArrangeRows();FunctionName.Focus();FunctionName.SelectAll();};
        }
        private string FreeParameterName(){for(int i=1;;i++){string name="valore"+(i==1?"":i.ToString());if(!Parameters.Any(p=>String.Equals(p.Name.Text,name,StringComparison.OrdinalIgnoreCase)))return name;}}
        internal void AddParameter(FlowParameter value,string oldName)
        {
            if(Parameters.Count>=FunctionModel.MaxParameters)return;
            var row=new ParameterRow{OldName=oldName,Host=new Panel{Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right},Name=Ui.TextBox(),Type=new FlowComboBox(),Array=new CheckBox{Text="Vettore",ForeColor=Ui.Ink},Reference=new CheckBox{Text="Modifica l’originale (riferimento)",ForeColor=Ui.Ink}};
            row.Name.Text=value.Name;row.Name.MaxLength=64;row.Name.AccessibleName="Nome parametro "+(Parameters.Count+1);row.Type.DropDownStyle=ComboBoxStyle.DropDownList;row.Type.Items.AddRange(Types.Take(5).ToArray());row.Type.SelectedIndex=(int)value.Type;row.Type.AccessibleName="Tipo parametro "+(Parameters.Count+1);row.Array.Checked=value.IsArray;row.Array.AccessibleName="Parametro vettoriale "+(Parameters.Count+1);
            row.Host.Controls.Add(row.Name);row.Host.Controls.Add(row.Type);row.Host.Controls.Add(row.Array);var remove=Ui.Button("−",35,delegate{Parameters.Remove(row);parameterPanel.Controls.Remove(row.Host);row.Host.Dispose();ArrangeRows();});remove.AccessibleName="Rimuovi parametro "+value.Name;row.Host.Controls.Add(remove);row.Reference.Checked=value.ByReference;row.Reference.Enabled=value.IsArray;row.Reference.AccessibleName="Modifica il vettore originale del parametro "+value.Name;row.Reference.AccessibleDescription="Se attiva, le modifiche agli elementi si vedono anche nel chiamante. Se disattiva, la funzione usa una copia.";row.Array.CheckedChanged+=delegate{row.Reference.Enabled=row.Array.Checked;if(!row.Array.Checked)row.Reference.Checked=false;};row.Host.Controls.Add(row.Reference);parameterPanel.Controls.Add(row.Host);Parameters.Add(row);Ui.ApplyTheme(row.Host);ArrangeRows();
        }
        private void ArrangeRows()
        {
            if(arrangingRows)return;arrangingRows=true;parameterPanel.SuspendLayout();
            try{
            float scale=NativeTheme.DpiScale(this);int h=(int)(76*scale),gap=(int)(8*scale),button=(int)(35*scale),array=(int)(88*scale),width=Math.Max((int)(360*scale),parameterPanel.ClientSize.Width-(int)(24*scale));
            Point scroll=parameterPanel.AutoScrollPosition;
            for(int i=0;i<Parameters.Count;i++){var p=Parameters[i];p.Host.SetBounds(scroll.X,scroll.Y+i*h,width,h);int name=(width-array-button-gap*3)/2;p.Name.SetBounds(0,4,name,(int)(28*scale));p.Type.SetBounds(name+gap,4,name,(int)(28*scale));p.Array.SetBounds(2*(name+gap),2,array,(int)(32*scale));p.Host.Controls[3].SetBounds(width-button,0,button,(int)(34*scale));p.Reference.SetBounds(0,(int)(38*scale),width-button,(int)(32*scale));}
            parameterPanel.AutoScrollMinSize=new Size(0,Parameters.Count*h);if(add!=null)add.Enabled=Parameters.Count<FunctionModel.MaxParameters;
            }finally{parameterPanel.ResumeLayout(true);arrangingRows=false;}
        }
        private void UpdateResult(){bool has=ReturnType.SelectedIndex!=(int)FlowDataType.Void;resultLabel.Visible=ResultName.Visible=ReturnArray.Visible=has;if(has&&String.IsNullOrWhiteSpace(ResultName.Text))ResultName.Text="risultato";}
        internal void Save()
        {
            try
            {
                var edited=original.Copy();edited.Name=FunctionName.Text.Trim();edited.Parameters=Parameters.Select(p=>new FlowParameter{Name=p.Name.Text.Trim(),Type=(FlowDataType)p.Type.SelectedIndex,IsArray=p.Array.Checked,ByReference=p.Array.Checked&&p.Reference.Checked}).ToList();edited.ReturnType=(FlowDataType)ReturnType.SelectedIndex;edited.ReturnVariable=edited.ReturnType==FlowDataType.Void?"":ResultName.Text.Trim();edited.ReturnIsArray=edited.ReturnType!=FlowDataType.Void&&ReturnArray.Checked;
                var renamed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach(var p in Parameters)if(p.OldName!=null&&p.OldName!=p.Name.Text.Trim())renamed.Add(p.OldName,p.Name.Text.Trim());
                if(original.ReturnType!=FlowDataType.Void&&edited.ReturnType!=FlowDataType.Void&&original.ReturnVariable!=edited.ReturnVariable){string target;if(renamed.TryGetValue(original.ReturnVariable,out target)&&target!=edited.ReturnVariable)throw new FlowException("Il parametro restituito e il risultato devono avere lo stesso nuovo nome.");renamed[original.ReturnVariable]=edited.ReturnVariable;}
                EditedProject=FunctionEditing.Save(project,edited,renamed);DialogResult=DialogResult.OK;Close();
            }
            catch(Exception ex){if(!(ex is InvalidDataException)&&!(ex is FlowException)&&!(ex is ArgumentException))throw;error.Text=ex.Message;error.AccessibleDescription=ex.Message;}
        }
        private void Delete()
        {
            try
            {
                var candidate=FunctionEditing.Delete(project,original.Id);
                if(FlowMessageDialog.Show(this,"Eliminare «"+original.Name+"» e il suo diagramma? Puoi annullare con Ctrl+Z.","Elimina funzione",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                EditedProject=candidate;EditedId=null;DialogResult=DialogResult.OK;Close();
            }catch(FlowException ex){error.Text=ex.Message;}
        }
    }
}
