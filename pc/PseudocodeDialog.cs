using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frociart
{
    internal sealed class PseudocodeDialog : Form
    {
        private readonly Diagram source;
        internal readonly TextBox Editor=new TextBox();
        private readonly Label status=Ui.Label("",9.5f,Ui.Muted);
        private readonly Action<Diagram> create;
        private readonly Action<string> keepDraft;
        internal PseudocodeDialog(Diagram source,string draft,Action<Diagram> create,Action<string> keepDraft)
        {
            this.source=DiagramIO.Clone(source);this.create=create;this.keepDraft=keepDraft;
            Text="Pseudocodice — Frociart";StartPosition=FormStartPosition.CenterParent;
            Size=new Size(850,680);MinimumSize=new Size(650,480);ShowInTaskbar=false;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=Ui.Font();BackColor=Ui.Surface;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=5};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(layout);
            var intro=Ui.Label("Dal diagramma al testo, e ritorno\nModifica il codice: Crea diagramma apre un nuovo progetto.",12,Ui.Ink,true);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,0);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            actions.Controls.Add(Ui.Button("Dal diagramma",140,()=>ReplaceWith(()=>FlowPseudocode.Encode(this.source)),true));
            actions.Controls.Add(Ui.Button("Esempi",85,ShowExamples));
            actions.Controls.Add(Ui.Button("Apri testo…",110,OpenText));
            actions.Controls.Add(Ui.Button("Sintassi",85,()=>TutorTextDialog.ShowText(this,"Scrivere pseudocodice",FlowPseudocode.Help)));layout.Controls.Add(actions,0,1);
            actions.Controls.Add(Ui.Button("Funzioni",90,()=>FunctionLibraryDialog.InsertInto(this,Editor)));
            Editor.Multiline=true;Editor.AcceptsReturn=true;Editor.AcceptsTab=true;Editor.WordWrap=false;Editor.ScrollBars=ScrollBars.Both;
            Editor.MaxLength=FlowPseudocode.MaxText;Editor.Dock=DockStyle.Fill;Editor.Font=new Font("Consolas",11);Editor.AccessibleName="Editor pseudocodice";Editor.ShortcutsEnabled=true;Editor.BorderStyle=BorderStyle.FixedSingle;
            Editor.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter){e.Handled=true;e.SuppressKeyPress=true;CreateDiagram();}};
            Editor.TextChanged+=delegate{
                string raw=Editor.Text,normalized=NativeLines(raw);
                if(raw!=normalized){int caret=NativeLines(raw.Substring(0,Editor.SelectionStart)).Length;Editor.Text=normalized;Editor.Select(Math.Min(caret,Editor.TextLength),0);return;}
                keepDraft(raw);status.ForeColor=Ui.Muted;status.Text="Ctrl+Invio crea il diagramma · Il progetto attuale viene conservato finché non confermi.\nPosizioni, colori e nomi dei rami si conservano nel formato .frociart.";
            };layout.Controls.Add(Editor,0,2);
            status.Dock=DockStyle.Fill;status.Padding=new Padding(0,8,0,0);layout.Controls.Add(status,0,3);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            footer.Controls.Add(Ui.Button("Copia",75,Copy));footer.Controls.Add(Ui.Button("Salva testo…",115,SaveText));
            footer.Controls.Add(Ui.Button("Crea diagramma",165,CreateDiagram,true));
            var close=Ui.Button("Chiudi",80,Close);footer.Controls.Add(close);CancelButton=close;layout.Controls.Add(footer,0,4);
            if(draft!=null)Editor.Text=draft;else try{Editor.Text=FlowPseudocode.Encode(source);}catch(ArgumentException e){Editor.Text="";ShowProblem(e);}
            FormClosing+=delegate{keepDraft(Editor.Text);};Shown+=delegate{Editor.Focus();Editor.Select(0,0);};Ui.ApplyTheme(this);
        }
        private void ShowExamples(){var menu=new FlowContextMenu();FlowMenus.Command(menu,this,"Leggi, decidi e stampa",()=>ReplaceWith(()=>FlowPseudocode.Example));FlowMenus.Command(menu,this,"Vettori e ciclo FOR",()=>ReplaceWith(()=>FlowPseudocode.VectorExample));FlowMenus.Command(menu,this,"Funzioni con parametri e risultato",()=>ReplaceWith(()=>FlowPseudocode.FunctionExample));FlowMenus.ShowTransient(menu,this,Cursor.Position);}
        private static string NativeLines(string text){return text.Replace("\r\n","\n").Replace('\r','\n').Replace("\n","\r\n");}
        private void ReplaceWith(Func<string> producer)
        {
            try{string next=producer();if(Editor.TextLength>0&&Editor.Text!=next&&FlowMessageDialog.Show(this,"Sostituire il testo nell'editor? Il diagramma attuale resta invariato.","Sostituisci pseudocodice",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;Editor.Text=next;}catch(ArgumentException e){ShowProblem(e);}
        }
        internal void CreateDiagram()
        {
            try{Diagram result=FlowPseudocode.Decode(Editor.Text);FlowLayout.Arrange(result);try{DiagramIO.CheckDocument(result);}catch(InvalidDataException){throw new ArgumentException("Il diagramma è troppo esteso. Dividi il codice in progetti più piccoli.");}create(result);}
            catch(FlowPseudocode.ParseError e){ShowProblem(e);int pos=Editor.GetFirstCharIndexFromLine(e.Line-1);if(pos>=0){Editor.Select(pos,Editor.Lines[e.Line-1].Length);Editor.ScrollToCaret();}Editor.Focus();}
            catch(ArgumentException e){ShowProblem(e);}
        }
        private void ShowProblem(Exception e){status.Text=e.Message;status.ForeColor=Ui.Amber;}
        private void Copy(){if(Editor.TextLength==0){status.Text="Scrivi o genera il codice prima di copiarlo.";return;}try{Clipboard.SetText(Editor.Text);status.Text="Pseudocodice copiato.";}catch(System.Runtime.InteropServices.ExternalException){status.Text="Gli appunti sono occupati. Riprova oppure usa Salva testo.";}}
        internal static string ReadText(string path){
            path=DocumentLaunch.LocalPath(path);
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var bytes=new MemoryStream()){
                byte[] buffer=new byte[8192];int count;while((count=file.Read(buffer,0,buffer.Length))>0){if(bytes.Length+count>FlowPseudocode.MaxText*4)throw new ArgumentException("Il file è troppo grande.");bytes.Write(buffer,0,count);}
                string text=new UTF8Encoding(false,true).GetString(bytes.ToArray());if(text.Length>FlowPseudocode.MaxText)throw new ArgumentException("Massimo 256 Ki caratteri.");return text;
            }
        }
        internal static void WriteText(string path,string text){
            if(text.Length>FlowPseudocode.MaxText)throw new ArgumentException("Massimo 256 Ki caratteri.");
            path=Path.GetFullPath(path);string temp=Path.Combine(Path.GetDirectoryName(path),".frociart-"+Guid.NewGuid().ToString("N")+".tmp");
            try{File.WriteAllText(temp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private void OpenText(){using(var open=new OpenFileDialog{Filter="Pseudocodice (*.txt;*.pseudo)|*.txt;*.pseudo|Tutti i file (*.*)|*.*",Title="Apri pseudocodice",CheckFileExists=true,RestoreDirectory=true})if(open.ShowDialog(this)==DialogResult.OK){try{string text=ReadText(open.FileName);ReplaceWith(()=>text);}catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is ArgumentException))throw;ShowProblem(e);}}}
        private void SaveText(){using(var save=new SaveFileDialog{Filter="Pseudocodice (*.txt)|*.txt",Title="Salva pseudocodice",DefaultExt="txt",AddExtension=true,FileName="Pseudocodice.txt",OverwritePrompt=true,RestoreDirectory=true})if(save.ShowDialog(this)==DialogResult.OK){try{WriteText(save.FileName,Editor.Text);status.Text="Testo salvato: "+Path.GetFileName(save.FileName);}catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is ArgumentException))throw;ShowProblem(e);}}}
    }
}

namespace Frociart {
    // One scheduled update per edit burst. No polling, interpretation, or writes to the document.
    internal sealed class PseudocodePreview : UserControl {
        internal Action<string> ClipboardWrite=Clipboard.SetText;
        internal readonly FlowCodeView Code=new FlowCodeView();
        internal readonly FlowCodeView PythonCode=new FlowCodeView();
        internal readonly Label State=Ui.Label("Dal flowchart, in tempo reale",8.5f,Ui.Muted);
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer {Interval=180};
        private readonly Func<Diagram> snapshot;
        private readonly FlowButton copy;
        private readonly FlowButton language;
        internal readonly FlowButton Types;
        private readonly FlowButton noticeButton;
        private Dictionary<string,FlowDataType> typeChoices=new Dictionary<string,FlowDataType>();
        private string profileStart;
        private int sourceGeneration;
        private string signature;
        private string highlightedFunction,highlightedNode;
        private bool runtimeHighlight;
        internal string SourceLanguage {get;private set;}
        internal FlowSourceTarget SelectedTarget {get;private set;}
        internal event Action TemplatesRequested;
        internal bool PythonMode {get{return SourceLanguage=="python";}}
        internal bool SourceMode {get{return SelectedTarget!=null;}}
        internal event Action<string,string> BlockRequested;
        internal int Generations;
        internal PseudocodePreview(Func<Diagram> capture){
            snapshot=capture;Name="LivePseudocode";Dock=DockStyle.Fill;BackColor=Ui.Surface;Padding=new Padding(12,8,12,12);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty,Padding=Padding.Empty};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
            var heading=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};language=Ui.Button("Pseudocodice ▾",160,ChooseLanguage);language.Dock=DockStyle.Left;heading.Controls.Add(language);
            copy=Ui.Button("Copia",57,delegate{if(!copy.Enabled)return;try{ClipboardWrite(SourceMode?PythonCode.CompleteSource.Text:Code.Text);State.Text=SourceMode?"Programma completo copiato":"Pseudocodice copiato";}catch(System.Runtime.InteropServices.ExternalException){State.Text="Appunti occupati: riprova.";}});copy.Dock=DockStyle.Right;copy.Enabled=false;copy.Height=30;heading.Controls.Add(copy);
            layout.Controls.Add(heading,0,0);State.Dock=DockStyle.Fill;State.AutoEllipsis=true;var stateRow=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};stateRow.Controls.Add(State);Types=Ui.Button("Tipi…",85,ChooseTypes);Types.Dock=DockStyle.Right;Types.Visible=false;stateRow.Controls.Add(Types);noticeButton=Ui.Button("Info",58,delegate{if(PythonCode.Source!=null&&PythonCode.Source.Notices.Count>0)FlowMessageDialog.Show(FindForm(),String.Join("\n\n",PythonCode.Source.Notices)+"\n\nIl modello originale resta invariato.","Adattamenti del codice",MessageBoxButtons.OK,MessageBoxIcon.Information);});noticeButton.Name="SourceAdaptationsInfo";noticeButton.AccessibleName="Dettagli degli adattamenti al codice generato";noticeButton.Dock=DockStyle.Right;noticeButton.Visible=false;stateRow.Controls.Add(noticeButton);layout.Controls.Add(stateRow,0,1);
            var codeArea=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};layout.Controls.Add(codeArea,0,2);
            Code.Name="LivePseudocodeText";Code.AccessibleName="Pseudocodice del flowchart, aggiornato automaticamente";Code.ReadOnly=true;Code.WordWrap=false;Code.ScrollBars=RichTextBoxScrollBars.Both;Code.Dock=DockStyle.Fill;Code.Font=Ui.Mono(9.5f);Code.Margin=Padding.Empty;codeArea.Controls.Add(Code);Code.BlockRequested+=(function,node)=>{var handler=BlockRequested;if(handler!=null)handler(function,node);};
            PythonCode.Name="LivePythonText";PythonCode.Visible=false;codeArea.Controls.Add(PythonCode);PythonCode.BlockRequested+=(function,node)=>{var handler=BlockRequested;if(handler!=null)handler(function,node);};
            timer.Tick+=delegate{timer.Stop();if(Visible)RefreshNow();};VisibleChanged+=delegate{if(Visible)Schedule();else timer.Stop();};
        }
        internal void Schedule(){if(IsDisposed)return;timer.Stop();if(Visible)timer.Start();}
        private void ChooseLanguage(){var menu=new FlowContextMenu();FlowMenus.Command(menu,this,"Pseudocodice",()=>SetPythonMode(false));foreach(var target in FlowSourceTarget.All){var choice=target;FlowMenus.Command(menu,this,choice.Name,()=>SetLanguage(choice.Id));}FlowMenus.Command(menu,this,"Modelli personali…",delegate{var action=TemplatesRequested;if(action!=null)action();});FlowMenus.ShowTransient(menu,this,language.PointToScreen(new Point(0,language.Height)));}
        internal void SetPythonMode(bool python){SetLanguage(python?"python":"pseudocode");}
        internal void SetLanguage(string value){if(value!="pseudocode"&&FlowSourceTarget.Find(value)==null)throw new ArgumentException("Linguaggio non supportato.");SetTarget(value=="pseudocode"?null:FlowSourceTarget.Find(value));}
        internal void SetTarget(FlowSourceTarget target){if(SelectedTarget==target&&SourceLanguage!=null)return;SelectedTarget=target;typeChoices.Clear();sourceGeneration++;Types.Visible=false;noticeButton.Visible=false;SourceLanguage=target==null?"pseudocode":target.Id;language.Text=target==null?"Pseudocodice ▾":target.Name+" ▾";language.AccessibleName=target==null?"Linguaggio: Pseudocodice":"Linguaggio o modello: "+target.Name;Code.Visible=!SourceMode;PythonCode.Visible=SourceMode;signature=null;RefreshNow();}
        void ChooseTypes(){if(SelectedTarget==null||SelectedTarget.Template==null||SelectedTarget.Template.Fpgt==null)return;var doc=snapshot();var program=FlowSource.Compile(doc);if(!program.CanGenerate)return;using(var picker=new SourceCodeDialog.TypeProfileDialog(new FlowFpgtTypes(program),typeChoices)){if(picker.ShowDialog(FindForm())!=DialogResult.OK)return;typeChoices=picker.Choices;}signature=null;RefreshNow();}

        internal void Highlight(string function,string node,bool runtime){highlightedFunction=function;highlightedNode=node;runtimeHighlight=runtime;ApplyHighlight();}
        private void ApplyHighlight(){noticeButton.Visible=SourceMode&&PythonCode.Source!=null&&PythonCode.Source.Notices.Count>0;var view=SourceMode?PythonCode:Code;if(view.Source==null||view.Source.Text.Length==0)return;var lines=view.Highlight(highlightedFunction,highlightedNode,true);bool adapted=SourceMode&&PythonCode.Source.Notices.Count>0;State.Text=lines.Count==0?(adapted?"Codice adattato · dettagli in Info":"Dal flowchart · doppio clic su una riga"):(runtimeHighlight?"Blocco della prova":"Blocco selezionato")+" · riga "+lines[0].Number+(adapted?" · adattato":"");State.AccessibleDescription=State.Text+(adapted?"\n"+String.Join("\n",view.Source.Notices):"");}
        internal void RefreshNow(){
            timer.Stop();if(IsDisposed)return;Diagram doc=snapshot();if(doc==null)return;
            string start=doc.Nodes.Where(n=>n.Kind==NodeKind.Start).Select(n=>n.Id).FirstOrDefault();if(profileStart!=start){profileStart=start;typeChoices.Clear();}
            var stamp=new StringBuilder();Action<string> part=value=>{value=value??"";stamp.Append(value.Length).Append(':').Append(value);};
            part(SourceLanguage??"pseudocode");part(doc.VariablePolicy);if(SourceMode)part(doc.Title);
            if(doc.Functions!=null)foreach(var f in doc.Functions){part(f.Id);part(FlowPseudocode.Signature(f));}
            foreach(var graph in FunctionEditing.Graphs(doc)){
                part("diagramma");foreach(var n in graph.Nodes){part(n.Id);part(((int)n.Kind).ToString());part(n.Text);part(n.Prompt);part(n.Comment);part(n.Breakpoint?"1":"0");part(n.BreakpointCondition);}foreach(var e in graph.Edges){part(e.From);part(e.To);part(e.Branch);}
            }
            string next=stamp.ToString();if(next==signature)return;signature=next;Generations++;sourceGeneration++;
            try{if(SourceMode){var program=FlowSource.Compile(doc);if(!program.CanGenerate)throw new ArgumentException(program.Diagnostics.First(d=>d.Error).Message);if(SelectedTarget.Template!=null&&SelectedTarget.Template.Fpgt!=null){var facts=new FlowFpgtTypes(program);Types.Visible=facts.Scopes.Any(s=>s.Symbols.Any(v=>!v.Declared&&!v.ConstantPi&&(v.InputChoice||v.Type.Kind==0)));int ticket=++sourceGeneration;var target=SelectedTarget;var choices=new Dictionary<string,FlowDataType>(typeChoices);copy.Enabled=false;PythonCode.Bind(null);noticeButton.Visible=false;State.Text="Genero il codice dal modello…";State.AccessibleDescription=State.Text;Task.Factory.StartNew(()=>target.GenerateTyped(program,choices)).ContinueWith(task=>{if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(new Action(delegate{if(IsDisposed||ticket!=sourceGeneration)return;if(task.IsFaulted){State.Text=task.Exception.InnerException.Message+(Types.Visible?" · Premi Tipi…":"");State.ForeColor=Ui.Amber;State.AccessibleDescription=State.Text;}else{PythonCode.Bind(task.Result);copy.Enabled=task.Result.Text.Length>0;State.ForeColor=Ui.Muted;State.Text=task.Result.Notices.Count==0?"Dal flowchart, in tempo reale":"Codice adattato · apri il codice per i dettagli";State.AccessibleDescription=State.Text+"\n"+String.Join("\n",task.Result.Notices);ApplyHighlight();}}));}catch(InvalidOperationException){/* Preview closed or replaced. */}});return;}PythonCode.Bind(SelectedTarget.Generate(program));PythonCode.AccessibleName="Codice "+SelectedTarget.Name+" del flowchart";}
                else Code.Bind(FlowPseudocode.Generate(doc));
                copy.Enabled=!SourceMode||PythonCode.Source!=null&&PythonCode.Source.Text.Length>0;State.ForeColor=Ui.Muted;State.Text=SourceMode&&PythonCode.Source!=null&&PythonCode.Source.Text.Length==0?"Il modello non ha prodotto testo.":"Dal flowchart, in tempo reale";State.AccessibleDescription=State.Text;ApplyHighlight();}
            catch(ArgumentException error){Code.Bind(null);PythonCode.Bind(null);copy.Enabled=false;Types.Visible=false;noticeButton.Visible=false;State.ForeColor=Ui.Amber;State.Text=error.Message;State.AccessibleDescription=State.Text;}
        }
        protected override void Dispose(bool disposing){if(disposing){sourceGeneration++;timer.Stop();timer.Dispose();}base.Dispose(disposing);}
    }
}
