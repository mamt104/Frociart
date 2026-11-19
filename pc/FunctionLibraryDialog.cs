using System;using System.Drawing;using System.Linq;using System.Collections.Generic;using System.Windows.Forms;
namespace Frociart {
 internal sealed class FunctionLibraryDialog : Form {
  internal readonly TextBox Search=Ui.TextBox();
  internal readonly ListBox Results=new ListBox();
  internal readonly Label Details=Ui.Label("",10,Ui.Ink);
  internal readonly ComboBox Group=new FlowComboBox();
  internal readonly Label Count=Ui.Label("",9,Ui.Muted);
  internal readonly FlowButton Insert;
  private readonly Panel resultsHost=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
  private readonly FlowScrollPanel descriptionScroll=new DescriptionPanel();
  private bool filtering;
  private sealed class DescriptionPanel : FlowScrollPanel {
   internal DescriptionPanel(){SetStyle(ControlStyles.Selectable,true);TabStop=true;AccessibleName="Descrizione del suggerimento";AccessibleDescription="Usa Pagina su, Pagina giù, Inizio e Fine per leggere tutto.";Padding=new Padding(2);}
   protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
   protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
   protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle,Ui.Ink,BackColor);}
  }
  private readonly List<string[]> entries=new List<string[]>();
  private readonly FlowExpressionLibrary.Catalog catalog;
  private readonly Timer filterDelay=new Timer{Interval=120};
  internal string SelectedExample {get;private set;}
  internal FunctionLibraryDialog(string selectedText="",IDictionary<string,FlowFunction> functions=null,FlowExpressionLibrary.Catalog context=null){
   catalog=context??FlowExpressionLibrary.Build(null);
   Text="Variabili e funzioni — Frociart";Size=new Size(720,Math.Min(740,510+Math.Min(6,catalog.Entries.Count)*36));MinimumSize=new Size(580,510);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;
   AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font();BackColor=Ui.Surface;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=7};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   foreach(var style in new[]{new RowStyle(SizeType.Absolute,50),new RowStyle(SizeType.Absolute,40),new RowStyle(SizeType.Absolute,36),new RowStyle(SizeType.Percent,100),new RowStyle(SizeType.Absolute,100),new RowStyle(SizeType.Absolute,catalog.TargetOnly||catalog.CallsOnly?0:38),new RowStyle(SizeType.Absolute,40)})layout.RowStyles.Add(style);Controls.Add(layout);
   var intro=Ui.Label((catalog.TargetOnly?"Scegli la variabile da leggere":catalog.CallsOnly?"Scegli la funzione da richiamare":"Trova cosa inserire nel blocco")+"\nFoglio: "+catalog.ScopeName,12,Ui.Ink,true);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,0);
   Search.Dock=DockStyle.Top;Search.AccessibleName="Cerca variabili, funzioni o operazioni";Search.MaxLength=100;Search.TextChanged+=delegate{filterDelay.Stop();filterDelay.Start();if(Insert!=null)Insert.Enabled=false;Results.Enabled=false;};filterDelay.Tick+=delegate{Filter();};FormClosed+=delegate{filterDelay.Stop();};Disposed+=delegate{filterDelay.Stop();filterDelay.Dispose();};layout.Controls.Add(Search,0,1);
   var filterBar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210));filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   Group.Items.AddRange(new object[]{"Tutto","Variabili del foglio","Funzioni e operatori"});Group.SelectedIndex=0;Group.Dock=DockStyle.Top;Group.AccessibleName="Tipo di suggerimenti";Group.SelectedIndexChanged+=delegate{Filter();};Group.Enabled=!catalog.TargetOnly&&!catalog.CallsOnly;filterBar.Controls.Add(Group,0,0);Count.Dock=DockStyle.Fill;Count.TextAlign=ContentAlignment.MiddleRight;Count.AutoEllipsis=true;Count.AccessibleName="Numero dei suggerimenti";filterBar.Controls.Add(Count,1,0);layout.Controls.Add(filterBar,0,2);
   Results.Dock=DockStyle.Fill;Results.IntegralHeight=false;Results.BorderStyle=BorderStyle.None;Results.Font=Ui.Font(11);Results.AccessibleName="Suggerimenti disponibili";Results.DrawMode=DrawMode.OwnerDrawFixed;Results.ItemHeight=36;
   Results.DrawItem+=(sender,e)=>{if(e.Index<0||e.Index>=entries.Count)return;bool selected=(e.State&DrawItemState.Selected)!=0;var bounds=e.Bounds;using(var brush=new SolidBrush(selected?Ui.Hover:Ui.Paper))e.Graphics.FillRectangle(brush,bounds);var row=entries[e.Index];float scale=e.Graphics.DpiX/96f;int padding=(int)(10*scale),category=(int)(130*scale);TextRenderer.DrawText(e.Graphics,row[1],Results.Font,new Rectangle(bounds.Left+padding,bounds.Top,Math.Max(1,bounds.Width-category-padding*2),bounds.Height),selected?Ui.Blue:Ui.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);using(var small=Ui.Font(9))TextRenderer.DrawText(e.Graphics,row[0],small,new Rectangle(bounds.Right-category-padding,bounds.Top,category,bounds.Height),Ui.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Right|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};
   Results.SelectedIndexChanged+=delegate{Describe();};Results.DoubleClick+=delegate{Choose();};resultsHost.Controls.Add(Results);layout.Controls.Add(resultsHost,0,3);
   descriptionScroll.Dock=DockStyle.Fill;descriptionScroll.AutoScroll=true;Details.AutoSize=true;Details.Dock=DockStyle.Top;Details.Padding=new Padding(0,10,0,4);descriptionScroll.Controls.Add(Details);descriptionScroll.SizeChanged+=delegate{Details.MaximumSize=new Size(Math.Max(1,descriptionScroll.Width-SystemInformation.VerticalScrollBarWidth-8),0);};layout.Controls.Add(descriptionScroll,0,4);
   if(!catalog.TargetOnly&&!catalog.CallsOnly){var convert=Ui.Button("Converti espressione Flowgorithm…",360,()=>{using(var dialog=new FlowExpressionDialog(selectedText,functions)){if(dialog.ShowDialog(this)!=DialogResult.OK)return;SelectedExample=dialog.ConvertedText;DialogResult=DialogResult.OK;Close();}});convert.Name="ConvertFlowgorithmExpression";convert.AccessibleName="Converti espressione Flowgorithm";convert.Dock=DockStyle.Fill;layout.Controls.Add(convert,0,5);}
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};Insert=Ui.Button("Inserisci",185,Choose,true);Insert.Name="InsertFunction";footer.Controls.Add(Insert);var close=Ui.Button("Chiudi",100,Close);footer.Controls.Add(close);layout.Controls.Add(footer,0,6);AcceptButton=Insert;CancelButton=close;
   Search.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){Choose();e.Handled=e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Down&&Results.Items.Count>0){if(filterDelay.Enabled)Filter();Results.Focus();e.Handled=e.SuppressKeyPress=true;}};
   Ui.ApplyTheme(this);Filter();Shown+=delegate{using(var g=CreateGraphics())Results.ItemHeight=(int)Math.Round(36*g.DpiX/96f);Search.Focus();};
  }
  private void Filter(){filterDelay.Stop();if(Insert==null)return;var matches=FlowExpressionLibrary.Filter(catalog,Search.Text,Math.Max(0,Group.SelectedIndex));if(Visible){FlowTransitions.Change(resultsHost);FlowTransitions.Change(descriptionScroll);}filtering=true;Results.BeginUpdate();entries.Clear();Results.Items.Clear();foreach(var row in matches.Take(200)){entries.Add(row);Results.Items.Add(row[0]+"  ·  "+row[1]);}if(entries.Count>0)Results.SelectedIndex=0;Results.EndUpdate();Results.Enabled=true;Count.Text=matches.Count>200?"Primi 200 di "+matches.Count+" · affina la ricerca":matches.Count+(matches.Count==1?" suggerimento":" suggerimenti");Describe();filtering=false;}
  private void Describe(){if(!filtering&&Visible)FlowTransitions.Change(descriptionScroll);descriptionScroll.AutoScrollPosition=Point.Empty;Insert.Enabled=Results.SelectedIndex>=0;if(!Insert.Enabled){Details.Text=catalog.TargetOnly?"Nessuna variabile trovata. Leggi un dato o usa Dichiara nel foglio.":catalog.CallsOnly?"Nessuna funzione trovata. Crea una funzione da Funzioni… oppure cambia ricerca.":"Nessun risultato. Cerca un nome o un’operazione, come «testo» o «decimali».";return;}var row=entries[Results.SelectedIndex];Details.Text=row[2]+"\n\n"+(String.IsNullOrEmpty(row[4])?"Inserisci: "+row[3]:row[3]+"  →  "+row[4]);}
  private void Choose(){if(filterDelay.Enabled)Filter();if(Results.SelectedIndex<0)return;SelectedExample=entries[Results.SelectedIndex][3];DialogResult=DialogResult.OK;Close();}
  internal static bool TryInsert(TextBox target,string original,int start,int length,string snippet,out string error){
   error=null;if(target.IsDisposed||target.Disposing||!target.Enabled||target.ReadOnly||target.Parent==null||target.FindForm()==null||target.Text!=original){error="Il campo è cambiato. Riapri Variabili e funzioni dalla selezione aggiornata.";return false;}
   if(start<0||length<0||start>target.TextLength-length||snippet==null||snippet.IndexOf('\0')>=0||target.TextLength-length+snippet.Length>target.MaxLength){error="Il testo è troppo lungo o non valido: libera spazio prima di inserirlo.";return false;}
   target.Select(start,length);
   // EM_REPLACESEL(TRUE) retains the native edit undo operation. SelectedText
   // uses a non-undoable replacement on the Framework TextBox control.
   if(NativeTheme.IsWindows)ReplaceSelection(target.Handle,0x00C2,new IntPtr(1),snippet);else target.SelectedText=snippet;
   target.Focus();return true;
  }
  [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,EntryPoint="SendMessageW")]
  private static extern IntPtr ReplaceSelection(IntPtr window,int message,IntPtr undo,string text);
  internal static void InsertInto(Form owner,TextBox target,bool completeWord=false){
   string original=target.Text;int start=target.SelectionStart,length=target.SelectionLength;IDictionary<string,FlowFunction> functions=null;FlowExpressionLibrary.Catalog context=null;Func<bool> current=()=>true;
   int originalStart=start,originalLength=length;
   var selection=completeWord?FlowExpressionLibrary.CompleteAt(original,start,length):new FlowExpressionLibrary.Selection(start,length,"");start=selection.Start;length=selection.Length;
   for(Form form=owner;form!=null;form=form.Owner){var main=form as MainForm;if(main!=null){functions=FunctionModel.Catalog(main.Project);context=main.ExpressionLibraryContext(target);current=main.ExpressionLibraryGuard(target);break;}}
   using(var dialog=new FunctionLibraryDialog(original.Substring(start,length),functions,context)){dialog.Search.Text=selection.Query;dialog.Filter();if(dialog.ShowDialog(owner)!=DialogResult.OK){if(current()&&!target.IsDisposed&&target.Text==original){target.Focus();target.Select(originalStart,originalLength);}return;}string error=null;if(!current())error="Il blocco o il foglio è cambiato. Riapri Variabili e funzioni.";if(error!=null||!TryInsert(target,original,start,length,dialog.SelectedExample,out error))FlowMessageDialog.Show(owner,error,"Inserimento non eseguito",MessageBoxButtons.OK,MessageBoxIcon.Information);}
  }
 }

}

namespace Frociart {
 internal sealed class FlowExpressionDialog : Form {
  internal readonly TextBox Input=Ui.TextBox(),Preview=Ui.TextBox();
  internal readonly Label Status=Ui.Label("",10,Ui.Muted);
  internal readonly FlowButton Apply;
  internal string ConvertedText {get;private set;}
  private readonly IDictionary<string,FlowFunction> functions;
  internal FlowExpressionDialog(string selectedText,IDictionary<string,FlowFunction> catalog=null){
   functions=catalog;Text="Converti espressione Flowgorithm — Frociart";Size=new Size(720,650);MinimumSize=new Size(580,560);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font();BackColor=Ui.Surface;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=7};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   foreach(var style in new[]{new RowStyle(SizeType.Absolute,90),new RowStyle(SizeType.Absolute,26),new RowStyle(SizeType.Percent,50),new RowStyle(SizeType.Absolute,32),new RowStyle(SizeType.Percent,50),new RowStyle(SizeType.Absolute,68),new RowStyle(SizeType.Absolute,40)})layout.RowStyles.Add(style);Controls.Add(layout);
   var help=Ui.Label("Incolla solo un’espressione, senza leggi, stampa o assegnazioni.\nLe parentesi conservano le precedenze di Flowgorithm; i testi mantengono le barre inverse. Variabili e tipi si verificano nel flowchart.",10,Ui.Muted);help.Dock=DockStyle.Fill;layout.Controls.Add(help,0,0);
   var source=Ui.Label("Espressione di Flowgorithm",10,Ui.Ink,true);source.Dock=DockStyle.Fill;layout.Controls.Add(source,0,1);
   // Retain an oversized paste for correction; never turn a truncated valid prefix into a conversion.
   Input.Name="FlowgorithmExpressionInput";Input.AccessibleName="Espressione di Flowgorithm";Input.Multiline=true;Input.AcceptsReturn=true;Input.ScrollBars=ScrollBars.Vertical;Input.MaxLength=65536;Input.Dock=DockStyle.Fill;layout.Controls.Add(Input,0,2);
   var dest=Ui.Label("Anteprima per Frociart",10,Ui.Ink,true);dest.Dock=DockStyle.Fill;dest.Padding=new Padding(0,8,0,0);layout.Controls.Add(dest,0,3);
   Preview.Name="FlowgorithmExpressionPreview";Preview.AccessibleName="Anteprima per Frociart";Preview.Multiline=true;Preview.ReadOnly=true;Preview.ScrollBars=ScrollBars.Both;Preview.WordWrap=false;Preview.Dock=DockStyle.Fill;layout.Controls.Add(Preview,0,4);
   Status.Dock=DockStyle.Fill;Status.Padding=new Padding(0,10,0,0);Status.AccessibleName="Stato della conversione";layout.Controls.Add(Status,0,5);
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};Apply=Ui.Button("Inserisci espressione",220,()=>{RefreshPreview();if(ConvertedText==null)return;DialogResult=DialogResult.OK;Close();},true);Apply.Name="InsertConvertedExpression";footer.Controls.Add(Apply);var cancel=Ui.Button("Annulla",110,()=>{DialogResult=DialogResult.Cancel;Close();});footer.Controls.Add(cancel);layout.Controls.Add(footer,0,6);CancelButton=cancel;
   Ui.ApplyTheme(this);Input.TextChanged+=delegate{RefreshPreview();};Input.Text=selectedText??"";RefreshPreview();Shown+=delegate{Input.Focus();Input.SelectAll();};
  }
  private void RefreshPreview(){ConvertedText=null;Preview.Text="";Apply.Enabled=false;if(String.IsNullOrWhiteSpace(Input.Text)){Status.Text="Incolla o scrivi l’espressione da convertire.";return;}try{ConvertedText=FlowgorithmImport.ConvertExpression(Input.Text,functions);Preview.Text=ConvertedText;Status.Text="Conversione pronta. Inserisci sostituisce solo la selezione originale. Nessuna espressione è stata eseguita.";Apply.Enabled=true;}catch(ArgumentException e){Status.Text=e.Message;}catch(FlowException e){Status.Text=e.Message;}}
 }
}
