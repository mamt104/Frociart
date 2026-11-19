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
    internal sealed partial class SourceCodeDialog : Form
    {
        internal readonly TextBox Editor=Ui.TextBox(true);
        internal readonly CheckBox Support=new CheckBox();
        internal readonly FlowButton Save,Locate,Copy;
        internal readonly FlowButton Types,How;
        internal Exception LastError;
        internal Action<string> ClipboardWrite=Clipboard.SetText;
        internal FlowSourceText Source {get;private set;}
        private FlowSourceText displayedSource;
        private FlowSourceText displayedCompleteSource;
        private string openingFunction,openingNode;
        private sealed class ReadingState {internal FlowSourceText.Position Start,End,Top;internal int Horizontal;}
        private ReadingState pendingReading;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr first,IntPtr second);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr window,int bar);
        private readonly Label status=Ui.Label("",9.5f,Ui.Muted);
        private readonly Action<string,string> locate;
        private readonly FlowSourceTarget target;
        private FlowSource.Program program;
        private FlowFpgtTypes typeFacts;
        private Dictionary<string,FlowDataType> typeChoices=new Dictionary<string,FlowDataType>();
        private FlowSourceText.Origin errorOrigin;
        private int generation;
        private string Extension {get{return target.Extension;}}
        internal const string SupportMarker="# --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---";

        internal SourceCodeDialog(Diagram document,Action<string,string> locate,bool javaCode=false):this(document,locate,javaCode?FlowSourceTarget.Java:FlowSourceTarget.Python){}
        internal SourceCodeDialog(Diagram document,Action<string,string> locate,FlowSourceTarget target)
        {
            if(target==null)throw new ArgumentNullException("target");this.locate=locate;this.target=target;Text="Codice "+target.Name+" — Frociart";StartPosition=FormStartPosition.CenterParent;
            Size=new Size(900,700);MinimumSize=new Size(640,480);ShowInTaskbar=false;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=Ui.Font();BackColor=Ui.Surface;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=6};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(layout);
            var intro=Ui.Label(target.Template==null?"Il tuo flowchart diventa "+target.Name+"\nCopia o salva il programma completo. Apri Come eseguirlo per i comandi da usare sul computer.":"Il tuo modello di codice\n"+target.Name,12,Ui.Ink,true);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,0);
            Action fitIntro=delegate{if(intro.Width<20)return;int height=Math.Max(64,TextRenderer.MeasureText(intro.Text,intro.Font,new Size(intro.Width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+8);if(Math.Abs(layout.RowStyles[0].Height-height)>1)layout.RowStyles[0].Height=height;};intro.SizeChanged+=delegate{fitIntro();};intro.FontChanged+=delegate{fitIntro();};Shown+=delegate{fitIntro();};
            Support.Text="Mostra anche il codice di supporto";Support.AutoSize=true;Support.Dock=DockStyle.Fill;Support.ForeColor=Ui.Ink;Support.AccessibleName=Support.Text;Support.CheckedChanged+=delegate{FlowTransitions.Change(execution==null?Editor.Parent:executionArea);ShowCode();};var options=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};options.Controls.Add(Support);Types=Ui.Button("Scegli tipi…",145,ChooseTypes);Types.Dock=DockStyle.Left;Types.Visible=false;options.Controls.Add(Types);{Support.Dock=DockStyle.Left;How=Ui.Button("Come eseguirlo",145,()=>{PauseSourceRuntime();FlowMessageDialog.Show(this,target.Instructions+(Source==null||Source.Notices.Count==0?"":"\n\nAdattamenti applicati al sorgente\n• "+String.Join("\n• ",Source.Notices)+"\nIl modello originale resta invariato."),target.Template==null?"Eseguire il programma "+target.Name:"Istruzioni del modello",MessageBoxButtons.OK,MessageBoxIcon.Information);});How.Name="SourceInstructions";How.AccessibleName="Come eseguire il programma "+target.Name;How.Dock=DockStyle.Right;options.Controls.Add(How);}layout.Controls.Add(options,0,1);
            Editor.Name=target.Template!=null?"TemplateSource":target==FlowSourceTarget.JavaScript?"JavaScriptSource":target==FlowSourceTarget.TypeScript?"TypeScriptSource":target==FlowSourceTarget.Cpp?"CppSource":target==FlowSourceTarget.VisualBasic?"VisualBasicSource":target==FlowSourceTarget.CSharp?"CSharpSource":target==FlowSourceTarget.Java?"JavaSource":target==FlowSourceTarget.Kotlin?"KotlinSource":target==FlowSourceTarget.Groovy?"GroovySource":target==FlowSourceTarget.PowerShell?"PowerShellSource":target==FlowSourceTarget.Php?"PhpSource":target==FlowSourceTarget.Ruby?"RubySource":"PythonSource";Editor.AccessibleName="Codice "+target.Name+" generato dal flowchart";Editor.ReadOnly=true;Editor.Multiline=true;Editor.WordWrap=false;Editor.ScrollBars=ScrollBars.Both;
            Editor.Dock=DockStyle.Fill;Editor.Font=Ui.Mono(11);Editor.ShortcutsEnabled=true;Editor.HideSelection=false;Editor.MouseUp+=delegate{UpdateLocate();};Editor.KeyUp+=delegate{UpdateLocate();};var codeHost=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};codeHost.Controls.Add(Editor);layout.Controls.Add(codeHost,0,3);
            BuildSourceRuntime(layout,codeHost);
            status.Dock=DockStyle.Fill;status.Padding=new Padding(0,8,0,0);layout.Controls.Add(status,0,4);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            Save=Ui.Button("Salva ."+Extension+"…",120,SaveFile,true);footer.Controls.Add(Save);
            Copy=Ui.Button("Copia tutto",110,delegate{if(Source==null||Source.Text.Length==0)return;try{ClipboardWrite(Source.Text);status.Text="Programma completo copiato, incluso il codice di supporto.";}catch(System.Runtime.InteropServices.ExternalException){status.Text="Appunti occupati. Riprova oppure usa Salva ."+Extension+".";}});footer.Controls.Add(Copy);
            Locate=Ui.Button("Vai al blocco",125,LocateSelection);footer.Controls.Add(Locate);
            var close=Ui.Button("Chiudi",85,Close);footer.Controls.Add(close);CancelButton=close;layout.Controls.Add(footer,0,5);
            try {
                program=FlowSource.Compile(document);
                if(!program.CanGenerate)throw new ArgumentException(String.Join("\n",program.Diagnostics.Where(d=>d.Error).Take(4).Select(d=>d.Message)));
                if(target.Template!=null&&target.Template.Fpgt!=null){typeFacts=new FlowFpgtTypes(program);bool needsTypes=NeedsTypes(typeFacts);Types.Visible=needsTypes;Editor.Text=needsTypes?"Scegli i tipi per la copia di codice esportata. Il flowchart resta invariato.":"Genero il codice dal modello .fpgt…";Shown+=delegate{if(needsTypes)ChooseTypes();else GenerateFpgt();};}else{Source=target.Generate(program);ShowCode();}
            }catch(ArgumentException error){ShowError(error);}
            Save.Enabled=Copy.Enabled=Source!=null&&Source.Text.Length>0;Support.Enabled=Source!=null;bool hasSupport=Source==null||ProgramText(Source)!=Source.Text;Support.Visible=hasSupport;if(!hasSupport&&target.Template==null)layout.RowStyles[1].Height=0;UpdateLocate();
            Shown+=delegate{Editor.Focus();NavigateOpening();RefreshSourceRuntime(true);FocusSourceInput();};Ui.ApplyTheme(this);
        }
        static bool NeedsTypes(FlowFpgtTypes facts){return facts.Scopes.Any(s=>s.Symbols.Any(v=>!v.Declared&&!v.ConstantPi&&(v.InputChoice||v.Type.Kind==0)));}
        internal void ChooseTypes(){if(typeFacts==null||!NeedsTypes(typeFacts))return;PauseSourceRuntime();using(var picker=new TypeProfileDialog(typeFacts,typeChoices)){if(picker.ShowDialog(this)!=DialogResult.OK){if(Source==null)status.Text="Tipi non scelti. Premi Scegli tipi per generare il codice.";return;}typeChoices=picker.Choices;}GenerateFpgt();}
        void GenerateFpgt(){PauseSourceRuntime();int ticket=++generation;var choices=new Dictionary<string,FlowDataType>(typeChoices);if(Source!=null)pendingReading=CaptureReading();Source=null;errorOrigin=null;LastError=null;Save.Enabled=Copy.Enabled=Support.Enabled=Types.Enabled=false;UpdateLocate();RefreshSourceRuntime();Editor.Text="Genero il codice dal modello .fpgt…";status.Text="Generazione in corso…";status.ForeColor=Ui.Muted;
            Task.Factory.StartNew(()=>target.GenerateTyped(program,choices)).ContinueWith(task=>{if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(new Action(delegate{if(IsDisposed||ticket!=generation)return;Types.Enabled=true;if(task.IsFaulted)ShowError(task.Exception.InnerException);else{Source=task.Result;Types.Text="Tipi del codice…";ShowCode();}Save.Enabled=Copy.Enabled=Source!=null&&Source.Text.Length>0;Support.Enabled=Source!=null;Support.Visible=Source!=null&&ProgramText(Source)!=Source.Text;UpdateLocate();}));}catch(InvalidOperationException){/* Viewer closed; the passive generation has no state to commit. */}});
        }
        void ShowError(Exception error){Source=null;LastError=error;var linked=error as FlowFpgt.GenerationError;errorOrigin=linked==null||linked.NodeId==null?null:new FlowSourceText.Origin(linked.FunctionId,linked.NodeId);Editor.WordWrap=true;Editor.Text=error.Message.Replace("\r\n","\n").Replace("\n","\r\n");status.Text=errorOrigin!=null?"Vai al blocco per correggere il diagramma, oppure modifica il modello.":target.Template==null?"Correggi questi problemi nel flowchart, poi riapri il codice.":"Modifica il modello o verifica i requisiti del diagramma, poi riprova.";status.ForeColor=Ui.Amber;}
        internal sealed class TypeProfileDialog:Form {
            internal readonly Dictionary<string,FlowDataType> Choices=new Dictionary<string,FlowDataType>();
            internal readonly FlowButton Generate;
            internal TypeProfileDialog(FlowFpgtTypes facts,IDictionary<string,FlowDataType> previous){
                Text="Tipi per il codice esportato — Frociart";StartPosition=FormStartPosition.CenterParent;Size=new Size(620,560);MinimumSize=new Size(500,260);ShowInTaskbar=false;BackColor=Ui.Surface;Font=Ui.Font();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
                var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=3};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(layout);
                var intro=Ui.Label("Scegli i tipi mancanti\nQuesti tipi valgono solo per il codice esportato. Il diagramma e la prova restano invariati.",11,Ui.Ink);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,0);Action fitIntro=delegate{if(intro.Width>20){int height=Math.Max(76,TextRenderer.MeasureText(intro.Text,intro.Font,new Size(intro.Width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+10);if(Math.Abs(layout.RowStyles[0].Height-height)>1)layout.RowStyles[0].Height=height;}};intro.SizeChanged+=delegate{fitIntro();};intro.FontChanged+=delegate{fitIntro();};Shown+=delegate{fitIntro();};
                var scroll=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true};var rows=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));scroll.Controls.Add(rows);layout.Controls.Add(scroll,0,1);var fields=new List<ComboBox>();
                var footer=new FlowLayoutPanel{Dock=DockStyle.Fill};Generate=Ui.Button("Genera codice",150,delegate{DialogResult=DialogResult.OK;Close();},true);Generate.Enabled=false;var cancel=Ui.Button("Annulla",110,delegate{DialogResult=DialogResult.Cancel;Close();});footer.Controls.Add(Generate);footer.Controls.Add(cancel);layout.Controls.Add(footer,0,2);CancelButton=cancel;AcceptButton=Generate;
                foreach(var scope in facts.Scopes)foreach(var symbol in scope.Symbols.Where(v=>!v.Declared&&!v.ConstantPi&&(v.InputChoice||v.Type.Kind==0))){string id=FlowFpgtTypes.Key(scope.Source.Id,symbol.Name);int row=rows.RowCount++;rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));var label=Ui.Label(symbol.Name+" · "+scope.Source.Name,10.5f,Ui.Ink);label.Dock=DockStyle.Fill;label.Padding=new Padding(0,8,4,8);var type=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top,Margin=new Padding(4,6,4,8),AccessibleName="Tipo di "+symbol.Name+" in "+scope.Source.Name};type.Items.AddRange(new object[]{"Scegli il tipo…","Intero","Reale","Testo","Logico"});FlowDataType saved;int selected=previous.TryGetValue(id,out saved)?(int)saved:0;type.SelectedIndex=selected>=1&&selected<=4?selected:0;if(type.SelectedIndex>0)Choices[id]=(FlowDataType)type.SelectedIndex;fields.Add(type);type.SelectedIndexChanged+=delegate{if(type.SelectedIndex>0)Choices[id]=(FlowDataType)type.SelectedIndex;else Choices.Remove(id);Generate.Enabled=fields.All(f=>f.SelectedIndex>0);};rows.Controls.Add(label,0,row);rows.Controls.Add(type,1,row);}
                Generate.Enabled=fields.Count>0&&fields.All(f=>f.SelectedIndex>0);Height=Math.Min(620,220+fields.Count*56);Ui.ApplyTheme(this);
            }
        }
        internal static string ProgramText(FlowSourceText source){if(source.Language=="cpp"||source.Language=="powershell")return source.ProgramView().Text;string marker=FlowSourceTarget.SupportMarker(source.Language);if(marker==null)return source.Text;int end=source.Text.IndexOf("\n"+marker+"\n",StringComparison.Ordinal);return end<0?source.Text:source.Text.Substring(0,end+1);}
        private int VisibleSourceLength(){return Math.Max(0,Editor.TextLength-Editor.GetLineFromCharIndex(Editor.TextLength));}
        private int ToSourceOffset(int caret){if(displayedSource==null)return 0;int line=Editor.GetLineFromCharIndex(caret);if(line<0||line>=displayedSource.Lines.Count)return Math.Min(caret,displayedSource.Text.Length);var mapped=displayedSource.Lines[line];return mapped.Start+Math.Min(mapped.Length+1,Math.Max(0,caret-Editor.GetFirstCharIndexFromLine(line)));}
        private int ToEditorOffset(int offset){if(displayedSource==null||offset>=VisibleSourceLength())return Editor.TextLength;var line=displayedSource.AtOffset(Math.Max(0,offset));if(line==null)return 0;int first=Editor.GetFirstCharIndexFromLine(line.Number-1);return first<0?Editor.TextLength:Math.Min(Editor.TextLength,first+Math.Min(line.Length,offset-line.Start));}
        private void ScrollToLine(int line,int horizontal){if(!Editor.IsHandleCreated)return;int current=(int)SendMessage(Editor.Handle,0x00CE,IntPtr.Zero,IntPtr.Zero);SendMessage(Editor.Handle,0x00B6,new IntPtr(horizontal-GetScrollPos(Editor.Handle,0)),new IntPtr(Math.Max(0,line)-current));}
        internal void SetOpeningOrigin(string function,string node){openingFunction=function;openingNode=node;if(Source!=null)NavigateOpening();}
        private void NavigateOpening(){if(Source==null||displayedSource==null)return;var line=displayedSource.OpeningLine(openingFunction,openingNode);int caret=line==null?0:ToEditorOffset(line.Start);Editor.Select(caret,0);if(line!=null){Editor.ScrollToCaret();ScrollToLine(Math.Max(0,line.Number-1-Math.Max(1,Editor.ClientSize.Height/Math.Max(1,Editor.Font.Height))/3),0);}if(execution!=null&&RuntimeCode.Source!=null){var at=RuntimeCode.Source.OpeningLine(openingFunction,openingNode);RuntimeCode.Select(at==null?0:at.Start,0);RuntimeCode.ScrollToCaret();}UpdateLocate();}
        private ReadingState CaptureReading(){
            var previous=displayedCompleteSource;if(previous==null)return null;int visible=VisibleSourceLength(),first=Editor.IsHandleCreated?(int)SendMessage(Editor.Handle,0x00CE,IntPtr.Zero,IntPtr.Zero):0;
            return new ReadingState{Start=previous.Capture(displayedSource,ToSourceOffset(Editor.SelectionStart),visible),End=previous.Capture(displayedSource,ToSourceOffset(Editor.SelectionStart+Editor.SelectionLength),visible),Top=previous.Capture(displayedSource,ToSourceOffset(Math.Max(0,Editor.GetFirstCharIndexFromLine(first))),visible),Horizontal=Editor.IsHandleCreated?GetScrollPos(Editor.Handle,0):0};
        }
        private void ShowCode(){
            if(Source==null)return;var reading=pendingReading??CaptureReading();pendingReading=null;
            displayedCompleteSource=Source;displayedSource=Support.Checked?Source:Source.ProgramView();Editor.WordWrap=false;Editor.Text=(Support.Checked?Source.Text:ProgramText(Source)).Replace("\n","\r\n");int visible=VisibleSourceLength();
            int? restored=Source.Restore(displayedSource,reading==null?null:reading.Start,visible);if(restored.HasValue){int begin=ToEditorOffset(restored.Value),last=ToEditorOffset(Source.Restore(displayedSource,reading.End,visible)??restored.Value);Editor.Select(begin,Math.Max(0,last-begin));int? scroll=Source.Restore(displayedSource,reading.Top,visible);if(scroll.HasValue)ScrollToLine(Editor.GetLineFromCharIndex(ToEditorOffset(scroll.Value)),reading.Horizontal);}else NavigateOpening();
            status.Text=Source.Text.Length==0?"Il modello non ha prodotto testo per questo diagramma.":Source.Notices.Count>0?"Adattamenti applicati: dettagli in Come eseguirlo. Salva e Copia includono il programma completo.":"Salva e Copia includono sempre il testo completo. Seleziona una riga per trovare il blocco.";UpdateLocate();RefreshSourceRuntime(true);
        }
        internal FlowSourceText.Origin SelectedOrigin(){if(execution!=null&&!execution.Current())return null;if(Source==null)return errorOrigin;if(execution!=null&&RuntimeCode.Visible)return RuntimeCode.SelectedOrigin();var visible=displayedSource??Source;int line=Editor.GetLineFromCharIndex(Editor.SelectionStart);return line>=0&&line<visible.Lines.Count?visible.Lines[line].Origins.FirstOrDefault():null;}
        private void UpdateLocate(){if(Locate!=null)Locate.Enabled=locate!=null&&SelectedOrigin()!=null;}
        internal void LocateSelection(){var origin=SelectedOrigin();if(origin==null||locate==null)return;locate(origin.FunctionId,origin.NodeId);Close();}
        internal static void WriteSource(string path,string text){
            if(text==null||text.Length>FlowSourceText.Builder.MaxCharacters)throw new ArgumentException("Il codice supera il limite di dimensione.");
            path=Path.GetFullPath(path);string temp=Path.Combine(Path.GetDirectoryName(path),".frociart-source-"+Guid.NewGuid().ToString("N")+".tmp");
            try{File.WriteAllText(temp,text,new UTF8Encoding(Path.GetExtension(path).Equals(".ps1",StringComparison.OrdinalIgnoreCase)));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private void SaveFile(){if(Source==null)return;PauseSourceRuntime();using(var save=new SaveFileDialog{Title="Salva il programma "+target.Name,Filter=target.Name+" (*."+Extension+")|*."+Extension,DefaultExt=Extension,AddExtension=true,FileName=target.FileName(Source.Text),OverwritePrompt=true,RestoreDirectory=true})
            if(save.ShowDialog(this)==DialogResult.OK)try{WriteSource(save.FileName,Source.Text);status.Text="Programma salvato: "+Path.GetFileName(save.FileName);}
            catch(Exception error){if(!(error is IOException)&&!(error is UnauthorizedAccessException)&&!(error is ArgumentException))throw;status.Text="Salvataggio non riuscito: "+error.Message;status.ForeColor=Ui.Amber;}}
    }
}

