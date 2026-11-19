using System;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;
namespace Frociart {
 internal static class FileBlocks {
  internal static readonly NodeKind[] Kinds={NodeKind.OpenFile,NodeKind.ReadFile,NodeKind.WriteFile,NodeKind.CloseFile};
  internal static bool IsFile(NodeKind kind){return kind>=NodeKind.OpenFile&&kind<=NodeKind.CloseFile;}
  internal static string Description(NodeKind kind){switch(kind){case NodeKind.OpenFile:return "Prepara lettura o scrittura";case NodeKind.ReadFile:return "Una riga in una variabile";case NodeKind.WriteFile:return "Salva un valore nel file";case NodeKind.CloseFile:return "Concludi lettura o scrittura";default:return "";}}
  internal static string FieldLabel(NodeKind kind){return kind==NodeKind.OpenFile?"Nome del file o espressione di testo":kind==NodeKind.ReadFile?"Variabile o elemento del vettore":"Valore o testo da scrivere";}
  internal static string Help(NodeKind kind){switch(kind){case NodeKind.OpenFile:return "Il nome è un testo fra virgolette o un'espressione. In esecuzione scegli il documento da leggere o dove salvare il risultato.";case NodeKind.ReadFile:return "Legge una riga nel tipo della variabile. Dichiara TESTO per conservare parole e spazi. Controlla non EOF() prima di leggere.";case NodeKind.WriteFile:return "Il valore va nel file aperto in scrittura. Con A capo ogni passaggio aggiunge una nuova riga. Chiudi file chiede dove salvarlo.";case NodeKind.CloseFile:return "Scegli il canale da chiudere. Chiudere la scrittura chiede dove salvare; annullare conserva il testo in File generati.";default:return "";}}
 }
 internal sealed class FileEditorDialog:Form {
  internal readonly ComboBox Channel=new FlowComboBox();internal readonly TextBox Value=Ui.TextBox();
  internal readonly CheckBox Newline=new CheckBox{Text="Vai a capo dopo questo valore",AutoSize=true,ForeColor=Ui.Ink};
  internal readonly Label Error=Ui.Label("",9,Ui.Red),Preview=Ui.Label("",10,Ui.Ink);
  internal readonly FlowButton Apply;internal string Result;readonly NodeKind kind;readonly IDictionary<string,FlowFunction> functions;
  internal FileEditorDialog(NodeKind kind,string text,IDictionary<string,FlowFunction> catalog){
   if(!FileBlocks.IsFile(kind))throw new ArgumentException("Blocco file richiesto.");this.kind=kind;functions=catalog;
   Text=Ui.KindName(kind)+" — Frociart";Size=new Size(590,560);MinimumSize=new Size(470,500);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font();BackColor=Ui.Surface;
   var scroll=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22)};Controls.Add(scroll);var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=0};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(layout);
   Action<Control,int> add=(control,height)=>{int row=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));control.Dock=DockStyle.Fill;layout.Controls.Add(control,0,row);};
   add(Ui.Label(FileBlocks.Description(kind),12,Ui.Ink,true),40);Channel.DropDownStyle=ComboBoxStyle.DropDownList;Channel.Items.AddRange(new object[]{"Lettura · dal file al programma","Scrittura · dal programma al file"});Channel.SelectedIndex=0;Channel.AccessibleName="Canale del file";
   if(kind==NodeKind.OpenFile||kind==NodeKind.CloseFile){add(Ui.Label("Canale del file",9,Ui.Muted,true),25);add(Channel,38);}
   Value.MaxLength=DiagramIO.MaxBlockText;Value.AccessibleName=FileBlocks.FieldLabel(kind);Value.Multiline=kind==NodeKind.WriteFile;Value.ScrollBars=ScrollBars.Vertical;
   if(kind!=NodeKind.CloseFile){add(Ui.Label(Value.AccessibleName,9,Ui.Muted,true),28);add(Value,kind==NodeKind.WriteFile?72:38);var library=Ui.Button("Funzioni ed esempi…",260,()=>FunctionLibraryDialog.InsertInto(this,Value));add(library,36);}
   if(kind==NodeKind.WriteFile){Newline.Checked=true;Newline.AccessibleName=Newline.Text;add(Newline,38);}
   add(Ui.Label(FileBlocks.Help(kind),10,Ui.Muted),82);add(Preview,50);add(Error,60);
   var footer=new TableLayoutPanel{ColumnCount=2,RowCount=1,Dock=DockStyle.Bottom,Height=74,Padding=new Padding(22,8,22,14)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));Apply=Ui.Button("Usa impostazioni",185,Save,true);Apply.Dock=DockStyle.Fill;footer.Controls.Add(Apply);var cancel=Ui.Button("Annulla",110,Close);cancel.Dock=DockStyle.Fill;footer.Controls.Add(cancel);Controls.Add(footer);AcceptButton=Apply;CancelButton=cancel;
   try{bool flag;if(kind==NodeKind.OpenFile){Value.Text=FlowTextFiles.OpenExpression(text,out flag);Channel.SelectedIndex=flag?0:1;}else if(kind==NodeKind.CloseFile)Channel.SelectedIndex=FlowTextFiles.Channel(text)?0:1;else if(kind==NodeKind.WriteFile){Value.Text=FlowTextFiles.WriteExpression(text,out flag);Newline.Checked=flag;}else Value.Text=text??"";}catch(FlowException){Value.Text=text??"";Error.Text="Completa i campi per correggere l'istruzione. Annulla conserva il testo precedente.";}
   Value.TextChanged+=delegate{RefreshPreview();};Channel.SelectedIndexChanged+=delegate{RefreshPreview();};Newline.CheckedChanged+=delegate{RefreshPreview();};RefreshPreview();Ui.ApplyTheme(this);Shown+=delegate{if(kind==NodeKind.CloseFile)Channel.Focus();else{Value.Focus();Value.SelectAll();}};
  }
  internal string Candidate(){string channel=Channel.SelectedIndex==1?"SCRITTURA":"LETTURA",value=Value.Text.Trim();return kind==NodeKind.OpenFile?channel+" "+value:kind==NodeKind.CloseFile?channel:kind==NodeKind.WriteFile&&!Newline.Checked?"SENZA_A_CAPO "+value:value;}
  void RefreshPreview(){Preview.Text=Candidate();}
  internal void Save(){try{string value=Candidate();if(value.Length>DiagramIO.MaxBlockText)throw new FlowException("L'istruzione può contenere al massimo 8192 caratteri.");Compiler.Block(new FlowNode{Kind=kind,Text=value},functions);Result=value;DialogResult=DialogResult.OK;Close();}catch(FlowException ex){Error.Text=ex.Message;Error.AccessibleDescription=ex.Message;}}
 }
}
