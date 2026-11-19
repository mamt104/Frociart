using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Frociart {
 internal sealed class FlowgorithmExportDialog:Form {
  private readonly Diagram snapshot;
  private readonly Action<string,string> locate;
  private readonly Dictionary<string,FlowDataType> choices=new Dictionary<string,FlowDataType>();
  private readonly Dictionary<string,int> colorChoices=new Dictionary<string,int>();
  private readonly HashSet<string> shownColors=new HashSet<string>();
  private readonly HashSet<string> shownChoices=new HashSet<string>();
  private readonly FlowScrollPanel types=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true};
  private readonly FlowLayoutPanel typeRows=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
  internal readonly ListBox Issues=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true};
  internal readonly TextBox Preview=Ui.TextBox(true);
  internal readonly FlowButton Save,Locate;
  internal FlowgorithmExport.Result Result {get;private set;}
  private readonly Label status=Ui.Label("Analizzo la compatibilità…",10,Ui.Muted),detail=Ui.Label("",10,Ui.Ink);
  private int request;
  internal string SavedPath {get;private set;}
  internal FlowgorithmExportDialog(Diagram document,Action<string,string> selectBlock){
   snapshot=DiagramIO.Clone(document);locate=selectBlock;Text="Esporta per Flowgorithm — Frociart";StartPosition=FormStartPosition.CenterParent;
   Size=new Size(900,780);MinimumSize=new Size(740,650);ShowInTaskbar=false;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=Ui.Font();BackColor=Ui.Surface;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=7};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   foreach(int h in new[]{58,110,42,110,76})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));Controls.Add(layout);
   var intro=Ui.Label("Apri una copia in Flowgorithm\nL’originale .frociart resta intatto. Il file conserva i calcoli compatibili; l’aspetto segue Flowgorithm.",11,Ui.Ink,true);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,0);
   types.Controls.Add(typeRows);types.AccessibleName="Tipi della copia Flowgorithm";layout.Controls.Add(types,0,1);status.Dock=DockStyle.Fill;status.AccessibleName="Stato della conversione";layout.Controls.Add(status,0,2);
   Issues.AccessibleName="Controlli di compatibilità";Issues.SelectedIndexChanged+=delegate{UpdateIssue();};layout.Controls.Add(Issues,0,3);
   detail.Dock=DockStyle.Fill;layout.Controls.Add(detail,0,4);
   Preview.ReadOnly=true;Preview.Multiline=true;Preview.WordWrap=false;Preview.ScrollBars=ScrollBars.Both;Preview.Dock=DockStyle.Fill;Preview.Font=Ui.Mono(10);Preview.AccessibleName="Anteprima del file Flowgorithm";layout.Controls.Add(Preview,0,5);
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
   Save=Ui.Button("Salva .fprg…",140,SaveFile,true);Save.Enabled=false;footer.Controls.Add(Save);Locate=Ui.Button("Vai al blocco",140,LocateIssue);Locate.Enabled=false;footer.Controls.Add(Locate);
   var close=Ui.Button("Chiudi",100,Close);footer.Controls.Add(close);CancelButton=close;layout.Controls.Add(footer,0,6);
   Ui.ApplyTheme(this);Shown+=delegate{Analyze();};
  }
  private void Analyze(){
   int ticket=++request;Save.Enabled=Locate.Enabled=false;Result=null;Preview.Clear();status.Text="Analizzo la compatibilità…";
   var selected=new Dictionary<string,FlowDataType>(choices);var selectedColors=new Dictionary<string,int>(colorChoices);
   Task.Run(delegate{
    FlowgorithmExport.Result result=null;string error=null;
    try{result=FlowgorithmExport.Analyze(snapshot,selected,selectedColors);}catch(ArgumentException e){error=e.Message;}
    if(IsDisposed||Disposing||!IsHandleCreated)return;
    try{BeginInvoke((Action)delegate{
     if(IsDisposed||Disposing||ticket!=request)return;
     if(result==null){status.Text="Conversione non disponibile: "+error;return;}
     Result=result;foreach(var choice in result.RequiredTypes)AddChoice(choice);foreach(var choice in result.RequiredColors)AddColorChoice(choice);int rows=shownChoices.Count+shownColors.Count;((TableLayoutPanel)types.Parent).RowStyles[1].Height=rows==0?0:Math.Min(110,rows*34+6);
     Issues.BeginUpdate();Issues.Items.Clear();foreach(var i in result.Issues)Issues.Items.Add(new IssueRow(i));Issues.EndUpdate();
     status.Text=result.CanExport?"Copia pronta. Controlla gli avvisi, poi salva il file separato.":result.RequiredTypes.Count>0||result.RequiredColors.Count>0?"Scegli i tipi o i colori richiesti per la copia, poi controlla gli eventuali problemi.":"Correggi i problemi indicati prima di esportare.";
     Preview.Text=(result.Xml??"").Replace("\n","\r\n");Save.Enabled=result.CanExport;
     if(Issues.Items.Count>0)Issues.SelectedIndex=0;else UpdateIssue();
    });}catch(InvalidOperationException){if(!IsDisposed&&!Disposing)throw;}
   });
  }
  private void AddChoice(FlowgorithmExport.TypeChoice choice){
   if(!shownChoices.Add(choice.Key))return;
   var row=new FlowLayoutPanel{Width=Math.Max(640,types.ClientSize.Width-24),Height=34,WrapContents=false,Margin=Padding.Empty};
   var label=Ui.Label(choice.Scope+" · "+choice.Name,10,Ui.Ink);label.Width=420;label.Height=30;row.Controls.Add(label);
   var select=new FlowComboBox{Width=205,AccessibleName="Tipo di "+choice.Name+" in "+choice.Scope};
   select.Items.AddRange(new object[]{"Scegli il tipo…","Intero","Reale","Testo","Logico"});select.SelectedIndex=0;
   select.SelectedIndexChanged+=delegate{if(select.SelectedIndex==0)choices.Remove(choice.Key);else choices[choice.Key]=(FlowDataType)select.SelectedIndex;Analyze();};row.Controls.Add(select);typeRows.Controls.Add(row);Ui.ApplyTheme(row);
  }
  private void AddColorChoice(FlowgorithmExport.ColorChoice choice){
   if(!shownColors.Add(choice.Key))return;
   var row=new FlowLayoutPanel{Width=Math.Max(640,types.ClientSize.Width-24),Height=34,WrapContents=false,Margin=Padding.Empty};
   var label=Ui.Label(choice.Scope+" · "+choice.Original+" nella copia",10,Ui.Ink);label.Width=420;label.Height=30;row.Controls.Add(label);
   var select=new FlowComboBox{Width=205,AccessibleName="Colore della copia per "+choice.Original+" in "+choice.Scope};select.Items.Add("Scegli il colore…");
   for(int i=0;i<FlowgorithmImport.PenNames.Length;i++)select.Items.Add(FlowgorithmImport.PenLabels[i]+" · "+FlowgorithmImport.PenColors[i]);select.SelectedIndex=0;
   select.SelectedIndexChanged+=delegate{if(select.SelectedIndex==0)colorChoices.Remove(choice.Key);else colorChoices[choice.Key]=select.SelectedIndex-1;Analyze();};row.Controls.Add(select);typeRows.Controls.Add(row);Ui.ApplyTheme(row);
  }
  private sealed class IssueRow {internal readonly FlowgorithmExport.Issue Issue;internal IssueRow(FlowgorithmExport.Issue issue){Issue=issue;}public override string ToString(){return(Issue.Error?"Da correggere: ":"Nota: ")+Issue.Message;}}
  private void UpdateIssue(){var row=Issues.SelectedItem as IssueRow;detail.Text=row==null?"":row.Issue.Message+"\n"+row.Issue.Suggestion;Locate.Enabled=row!=null&&row.Issue.NodeId!=null&&locate!=null;}
  internal void LocateIssue(){var row=Issues.SelectedItem as IssueRow;if(row==null||row.Issue.NodeId==null||locate==null)return;locate(row.Issue.FunctionId,row.Issue.NodeId);Close();}
  internal static void WriteFile(string path,string xml){
   if(xml==null||Encoding.UTF8.GetByteCount(xml)>DiagramIO.MaxFileBytes)throw new ArgumentException("Il file Flowgorithm supera il limite di 4 MB.");
   path=Path.GetFullPath(path);if(!Path.GetExtension(path).Equals(".fprg",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Scegli un file con estensione .fprg.");
   string temp=Path.Combine(Path.GetDirectoryName(path),".frociart-flowgorithm-"+Guid.NewGuid().ToString("N")+".tmp");
   try{File.WriteAllText(temp,xml,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  private void SaveFile(){if(Result==null||!Result.CanExport)return;
   using(var picker=new SaveFileDialog{Title="Salva una copia per Flowgorithm",Filter="Flowgorithm (*.fprg)|*.fprg",DefaultExt="fprg",AddExtension=true,FileName="Programma.fprg",OverwritePrompt=true,RestoreDirectory=true}){
    if(picker.ShowDialog(this)!=DialogResult.OK)return;
    try{WriteFile(picker.FileName,Result.Xml);SavedPath=picker.FileName;status.Text="File salvato: "+Path.GetFileName(SavedPath);}
    catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is ArgumentException))throw;status.Text="File non salvato: "+e.Message;}
   }
  }
 }
}
