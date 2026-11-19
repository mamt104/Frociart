using System;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;
namespace Frociart {
 internal sealed class TurtleEditorDialog:Form {
  internal readonly ComboBox Direction=new FlowComboBox(),ColorChoice=new FlowComboBox();internal readonly TextBox Value=Ui.TextBox();
  internal readonly CheckBox Draw=new CheckBox{Text="Disegna durante lo spostamento",Checked=true,AutoSize=true,ForeColor=Ui.Ink};
  internal readonly Label Error=Ui.Label("",9,Ui.Red),Preview=Ui.Label("",10,Ui.Ink);internal readonly FlowButton Apply;internal string Result;
  readonly NodeKind kind;readonly IDictionary<string,FlowFunction> functions;
  internal TurtleEditorDialog(NodeKind kind,string text,IDictionary<string,FlowFunction> catalog){
   if(!FlowTurtle.IsTurtle(kind))throw new ArgumentException("Blocco tartaruga richiesto.");this.kind=kind;functions=catalog;
   Text=Ui.KindName(kind)+" — Frociart";Size=new Size(590,590);MinimumSize=new Size(470,510);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font();BackColor=Ui.Surface;
   var scroll=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22)};Controls.Add(scroll);var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=0};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(layout);
   Action<Control,int> add=(control,height)=>{int row=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));control.Dock=DockStyle.Fill;layout.Controls.Add(control,0,row);};
   add(Ui.Label(FlowTurtle.Description(kind),12,Ui.Ink,true),40);
   Direction.DropDownStyle=ComboBoxStyle.DropDownList;Direction.Items.AddRange(new object[]{"Destra · senso orario","Sinistra · senso antiorario"});Direction.SelectedIndex=0;Direction.AccessibleName="Direzione della rotazione";
   if(kind==NodeKind.TurtleTurn){add(Ui.Label("Direzione",9,Ui.Muted,true),25);add(Direction,38);}
   if(kind==NodeKind.TurtlePen){ColorChoice.DropDownStyle=ComboBoxStyle.DropDownList;ColorChoice.AccessibleName="Scegli un colore";ColorChoice.Items.Add("Personalizzato o variabile");foreach(var color in FlowTurtle.Colors)ColorChoice.Items.Add(color[0]);ColorChoice.SelectedIndex=0;add(Ui.Label("Colore",9,Ui.Muted,true),25);add(ColorChoice,38);ColorChoice.SelectedIndexChanged+=delegate{if(ColorChoice.SelectedIndex>0)Value.Text="\""+FlowTurtle.Colors[ColorChoice.SelectedIndex-1][0]+"\"";};}
   Value.MaxLength=DiagramIO.MaxBlockText;Value.AccessibleName=FlowTurtle.FieldLabel(kind);
   if(kind!=NodeKind.TurtleHome&&kind!=NodeKind.TurtleClear){add(Ui.Label(Value.AccessibleName,9,Ui.Muted,true),28);add(Value,38);add(Ui.Button("Funzioni ed esempi…",260,()=>FunctionLibraryDialog.InsertInto(this,Value)),36);}
   if(kind==NodeKind.TurtleForward){Draw.AccessibleName=Draw.Text;add(Draw,38);}
   add(Ui.Label(FlowTurtle.Help(kind),10,Ui.Muted),90);add(Preview,50);add(Error,60);
   var footer=new TableLayoutPanel{ColumnCount=2,RowCount=1,Dock=DockStyle.Bottom,Height=74,Padding=new Padding(22,8,22,14)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));Apply=Ui.Button("Usa impostazioni",185,Save,true);Apply.Dock=DockStyle.Fill;footer.Controls.Add(Apply);var cancel=Ui.Button("Annulla",110,Close);cancel.Dock=DockStyle.Fill;footer.Controls.Add(cancel);Controls.Add(footer);AcceptButton=Apply;CancelButton=cancel;
   try{var spec=FlowTurtle.Parse(kind,text);Value.Text=spec.Expression??"";Direction.SelectedIndex=spec.Left?1:0;Draw.Checked=spec.Draw;}catch(FlowException){Value.Text=text??"";Error.Text="Completa i campi per correggere l'istruzione. Annulla conserva il testo precedente.";}
   Value.TextChanged+=delegate{RefreshPreview();};Direction.SelectedIndexChanged+=delegate{RefreshPreview();};Draw.CheckedChanged+=delegate{RefreshPreview();};RefreshPreview();Ui.ApplyTheme(this);Shown+=delegate{if(Value.Parent!=null){Value.Focus();Value.SelectAll();}else Apply.Focus();};
  }
  internal string Candidate(){return kind==NodeKind.TurtleHome||kind==NodeKind.TurtleClear?FlowTurtle.DefaultText(kind):(kind==NodeKind.TurtleTurn?(Direction.SelectedIndex==1?"SINISTRA ":"DESTRA "):kind==NodeKind.TurtleForward&&!Draw.Checked?"SENZA_TRACCIA ":"")+Value.Text.Trim();}
  void RefreshPreview(){Preview.Text=Candidate();}
  internal void Save(){try{string value=Candidate();if(value.Length>DiagramIO.MaxBlockText)throw new FlowException("L'istruzione può contenere al massimo 8192 caratteri.");Compiler.Block(new FlowNode{Kind=kind,Text=value},functions);Result=value;DialogResult=DialogResult.OK;Close();}catch(FlowException ex){Error.Text=ex.Message;Error.AccessibleDescription=ex.Message;}}
 }
}
