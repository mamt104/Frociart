using System;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;
namespace Frociart {
 internal sealed class DeclarationEditorDialog:Form {
  internal readonly TextBox Names=Ui.TextBox(),Dimension=Ui.TextBox();
  internal readonly ComboBox ValueType=new FlowComboBox();
  internal readonly CheckBox IsVector=new CheckBox{Text="Vettore: più elementi dello stesso tipo",AutoSize=true,ForeColor=Ui.Ink};
  internal readonly Label Error=Ui.Label("",9,Ui.Red),Preview=Ui.Label("",10,Ui.Ink);
  internal readonly FlowButton Apply;internal string Result;
  readonly IDictionary<string,FlowFunction> functions;
  readonly Label dimensionLabel=Ui.Label("Numero di elementi (anche un'espressione)",9,Ui.Muted,true);
  static readonly string[] Tokens={"INTERO","REALE","TESTO","LOGICO"};
  internal DeclarationEditorDialog(string text,IDictionary<string,FlowFunction> catalog){
   functions=catalog;Text="Dichiara variabili — Frociart";Size=new Size(580,570);MinimumSize=new Size(470,520);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font();BackColor=Ui.Surface;
   var scroll=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22)};Controls.Add(scroll);
   var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(layout);
   Action<Control,int> add=(control,height)=>{int row=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));control.Dock=DockStyle.Fill;layout.Controls.Add(control,0,row);};layout.RowCount=0;
   add(Ui.Label("Scegli che cosa può contenere la variabile.",12,Ui.Ink,true),42);
   add(Ui.Label("Nomi delle variabili, separati da virgole",9,Ui.Muted,true),25);Names.AccessibleName="Nomi delle variabili";Names.MaxLength=8192;add(Names,37);
   add(Ui.Label("Tipo del valore",9,Ui.Muted,true),25);ValueType.AccessibleName="Tipo del valore";ValueType.DropDownStyle=ComboBoxStyle.DropDownList;ValueType.Items.AddRange(new object[]{"Intero · numeri senza decimali","Reale · numeri anche decimali","Testo · parole e frasi","Logico · vero o falso"});ValueType.SelectedIndex=0;add(ValueType,37);
   IsVector.AccessibleName="La variabile è un vettore";add(IsVector,42);add(dimensionLabel,25);Dimension.AccessibleName=dimensionLabel.Text;Dimension.MaxLength=4096;Dimension.Text="10";add(Dimension,37);
   add(Preview,42);add(Ui.Label("Valori iniziali: 0, testo vuoto o falso. Nei vettori gli indici partono da 0. Un Intero elimina la parte decimale.",9,Ui.Muted),54);add(Error,55);
   var footer=new FlowLayoutPanel{WrapContents=false};Apply=Ui.Button("Usa dichiarazione",185,Save,true);footer.Controls.Add(Apply);var cancel=Ui.Button("Annulla",105,Close);footer.Controls.Add(cancel);add(footer,46);AcceptButton=Apply;CancelButton=cancel;
   try{var d=FlowDeclaration.Parse(text);Names.Text=String.Join(", ",d.Names);ValueType.SelectedIndex=(int)d.Type-1;IsVector.Checked=d.IsArray;if(d.IsArray)Dimension.Text=d.SizeText;}catch(FlowException){Names.Text=FlowDeclaration.Body(text).Split(':')[0];Error.Text="Completa i campi per correggere la dichiarazione. Annulla conserva il testo precedente.";}
   Names.TextChanged+=delegate{RefreshPreview();};Dimension.TextChanged+=delegate{RefreshPreview();};ValueType.SelectedIndexChanged+=delegate{RefreshPreview();};IsVector.CheckedChanged+=delegate{RefreshPreview();};RefreshPreview();Ui.ApplyTheme(this);Shown+=delegate{Names.Focus();Names.SelectAll();};
  }
  string Candidate(){return Names.Text.Trim()+": "+Tokens[Math.Max(0,ValueType.SelectedIndex)]+(IsVector.Checked?"["+Dimension.Text.Trim()+"]":"");}
  void RefreshPreview(){dimensionLabel.Visible=Dimension.Visible=IsVector.Checked;Dimension.Enabled=IsVector.Checked;Preview.Text=Candidate();}
  internal void Save(){try{string value=Candidate();if(value.Length>DiagramIO.MaxBlockText)throw new FlowException("La dichiarazione è troppo lunga.");var d=FlowDeclaration.Parse(value);if(d.IsArray)ExpressionMachine.CheckVoid(ExpressionLanguage.Parse(d.SizeText,functions),false);Result=value;DialogResult=DialogResult.OK;Close();}catch(FlowException ex){Error.Text=ex.Message;Error.AccessibleDescription=ex.Message;}}
 }
}
