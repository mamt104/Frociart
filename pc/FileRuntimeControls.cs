using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Frociart
{
    internal static class FlowFileStorage
    {
        internal static string SuggestedName(string name)
        {
            string result=Path.GetFileName((name??"").Replace('/',Path.DirectorySeparatorChar));
            foreach(char c in Path.GetInvalidFileNameChars())result=result.Replace(c,'_');
            if(String.IsNullOrWhiteSpace(result)||result=="."||result=="..")result="risultato.txt";
            if(result.Length>180)result=result.Substring(0,176)+".txt";if(!Path.HasExtension(result))result+=".txt";return result;
        }
        internal static string Read(string path){using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))return FlowTextFiles.ReadText(stream);}
        internal static void Save(string path,string text)
        {
            string full=Path.GetFullPath(path),folder=Path.GetDirectoryName(full),temp=Path.Combine(folder,".frociart-text-"+Guid.NewGuid().ToString("N")+".tmp");
            bool existed=File.Exists(full);long size=existed?new FileInfo(full).Length:0;DateTime modified=existed?File.GetLastWriteTimeUtc(full):DateTime.MinValue;
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){FlowTextFiles.WriteText(stream,text);stream.Flush(true);}
                if(File.Exists(full)!=existed||existed&&(new FileInfo(full).Length!=size||File.GetLastWriteTimeUtc(full)!=modified))throw new IOException("Il file è cambiato durante il salvataggio. Scegli un nuovo nome per conservare entrambe le versioni.");
                if(existed)File.Replace(temp,full,null);else File.Move(temp,full);
            }
            finally{if(File.Exists(temp))try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
    internal sealed partial class MainForm
    {
        private Interpreter fileRunner;
        private FlowFileRecovery fileRecovery;
        private FlowButton runtimeFileAction,runtimeGeneratedFiles;
        private bool fileIoBusy;
        private int fileIoGeneration;
        internal Func<bool,string,string> RuntimeFilePicker;
        internal Func<string,string> RuntimeFileRead=FlowFileStorage.Read;
        internal Action<string,string> RuntimeFileSave=FlowFileStorage.Save;
        internal FlowFileRecovery FileRecovery
        {
            get{return fileRecovery??(fileRecovery=new FlowFileRecovery(Path.Combine(Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart"),"generated-files")));}
        }
        [System.ComponentModel.Browsable(false),System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal Interpreter Runner
        {
            get{return fileRunner;}
            set
            {
                if(Object.ReferenceEquals(fileRunner,value))return;
                fileIoGeneration++;fileIoBusy=false;
                if(fileRunner!=null)try{fileRunner.Files.CloseJournal();}catch(IOException){}
                fileRunner=value;if(value!=null)value.Files.JournalFactory=FileRecovery.Create;
            }
        }
        private string PickRuntimeFile(bool reading,string name)
        {
            if(RuntimeFilePicker!=null)return RuntimeFilePicker(reading,name);
            IWin32Window pickerOwner=sourceViewer!=null&&sourceViewer.Visible?(IWin32Window)sourceViewer:this;
            if(reading)using(var picker=new OpenFileDialog{Title="Scegli il file da leggere · "+name,Filter="File di testo (*.txt;*.csv;*.log)|*.txt;*.csv;*.log|Tutti i file (*.*)|*.*",CheckFileExists=true,Multiselect=false})return picker.ShowDialog(pickerOwner)==DialogResult.OK?picker.FileName:null;
            using(var picker=new SaveFileDialog{Title="Salva il file generato",Filter="Testo UTF-8 (*.txt)|*.txt|Tutti i file (*.*)|*.*",FileName=FlowFileStorage.SuggestedName(name),DefaultExt="txt",AddExtension=true,OverwritePrompt=true})return picker.ShowDialog(pickerOwner)==DialogResult.OK?picker.FileName:null;
        }
        internal async void ChooseRuntimeFile()
        {
            if(fileIoBusy||Runner==null||Runner.State!=RuntimeState.WaitingForFile)return;
            var owner=Runner;var request=owner.PendingFile;int generation=fileIoGeneration;string path;
            try{path=PickRuntimeFile(request.Kind==FlowFileRequestKind.Read,request.Name);}catch(Exception){owner.FileDeferred(request,"Il selettore dei file non è disponibile. Puoi riprovare.");RefreshRuntime();return;}
            if(!CurrentFileRequest(owner,request,generation))return;
            if(path==null){owner.FileDeferred(request,null);RefreshRuntime();return;}
            fileIoBusy=true;RefreshRuntime();
            try
            {
                string content=null;
                if(request.Kind==FlowFileRequestKind.Read)content=await Task.Run(()=>RuntimeFileRead(path));
                else await Task.Run(()=>RuntimeFileSave(path,request.Text));
                if(!CurrentFileRequest(owner,request,generation))return;
                if(owner.ProvideFile(request,content))
                {
                    GetDebugger().InputProvided();if(automatic&&debugger.Active&&owner.State!=RuntimeState.Completed&&owner.State!=RuntimeState.Error)runTimer.Start();
                }
            }
            catch(Exception ex)
            {
                if(CurrentFileRequest(owner,request,generation))owner.FileDeferred(request,ex is FlowException?ex.Message:request.Kind==FlowFileRequestKind.Read?"Non riesco a leggere il file. Controlla che sia disponibile e riprova.":"Salvataggio non riuscito. Il testo resta in File generati; scegli un'altra destinazione o riprova.");
            }
            finally{if(!IsDisposed&&generation==fileIoGeneration){fileIoBusy=false;RefreshRuntime();}}
        }
        private bool CurrentFileRequest(Interpreter owner,FlowFileRequest request,int generation){return !IsDisposed&&generation==fileIoGeneration&&Object.ReferenceEquals(Runner,owner)&&Object.ReferenceEquals(owner.PendingFile,request);}
        private void BuildRuntimeFiles()
        {
            runtimeFileAction=Ui.Button("Scegli file",230,ChooseRuntimeFile,true);runtimeFileAction.Name="RuntimeFileAction";runtimePanel.Controls.Add(runtimeFileAction);
            runtimeGeneratedFiles=Ui.Button("File generati e recupero",230,ShowGeneratedFiles);runtimeGeneratedFiles.Name="GeneratedFiles";runtimeGeneratedFiles.AccessibleDescription="Apri e salva copie dei file generati, anche dopo aver chiuso o ricominciato la simulazione.";runtimePanel.Controls.Add(runtimeGeneratedFiles);
        }
        private int LayoutRuntimeFiles(int y,int w)
        {
            bool pending=Runner!=null&&Runner.State==RuntimeState.WaitingForFile;
            runtimeFileAction.Visible=pending;runtimeFileAction.Enabled=pending&&!fileIoBusy;
            if(pending){runtimeFileAction.Text=fileIoBusy?"Operazione in corso…":Runner.PendingFile.Kind==FlowFileRequestKind.Read?"Scegli il file da leggere":"Salva il file generato";runtimeFileAction.AccessibleDescription=Runner.LastMessage;runtimeFileAction.SetBounds(S(12),y,w,S(44));y+=S(56);}
            runtimeGeneratedFiles.SetBounds(S(12),y,w,S(38));y+=S(50);return y;
        }
        internal void ShowGeneratedFiles()
        {
            PauseRun();
            try
            {
                var entries=FileRecovery.List();if(entries.Count==0){TutorTextDialog.ShowText(this,"File generati","Qui trovi i file prodotti dai blocchi Scrivi file. Le bozze sono conservate su questo PC anche se ricominci o chiudi la simulazione.");return;}
                var menu=new FlowContextMenu();foreach(var entry in entries){var selected=entry;string name=entry.Name.Length>45?entry.Name.Substring(0,42)+"…":entry.Name;var item=FlowMenus.Command(menu,this,name.Replace("&","&&")+" · "+entry.Modified.ToLocalTime().ToString("dd/MM HH:mm"),()=>ShowGeneratedFile(selected));item.ToolTipText=entry.Name;}
                FlowMenus.ShowTransient(menu,this,PointToScreen(new Point(Math.Max(20,ClientSize.Width-370),150)));
            }
            catch(Exception ex){ShowError("Recupero non disponibile",ex);}
        }
        private async void ShowGeneratedFile(FlowFileRecovery.Entry entry)
        {
            FlowFileRecovery.Content content;try{content=await Task.Run(()=>FileRecovery.Load(entry));}catch(Exception){if(!IsDisposed&& (Runner==null||Runner.Files.ActiveJournalId!=entry.Id)&&FlowMessageDialog.Show(this,"La bozza non è leggibile. Vuoi rimuoverla dal recupero locale? Scegli No per conservarla.","Bozza non leggibile",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){try{FileRecovery.Delete(entry);}catch(Exception){SetStatus("Non riesco a rimuovere la bozza. Puoi riprovare.");}}return;}if(IsDisposed)return;
            using(var form=new Form{Text="File generato — Frociart",Size=new Size(650,520),MinimumSize=new Size(470,420),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,AutoScaleMode=AutoScaleMode.Dpi,Font=Ui.Font(),BackColor=Ui.Surface})
            {
                var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));form.Controls.Add(layout);
                var title=Ui.Label(content.Name+"\n"+content.Text.Length+" caratteri · bozza locale",12,Ui.Ink,true);title.Dock=DockStyle.Fill;layout.Controls.Add(title,0,0);
                var preview=Ui.TextBox();preview.Multiline=true;preview.ReadOnly=true;preview.Font=Ui.Mono(11);preview.ScrollBars=ScrollBars.Both;preview.Dock=DockStyle.Fill;preview.AccessibleName="Anteprima del file generato";preview.Text=content.Text.Length>16000?content.Text.Substring(0,16000)+"\r\n… Anteprima abbreviata. Il salvataggio comprende tutto il file.":content.Text;layout.Controls.Add(preview,0,1);
                var note=Ui.Label(content.Incomplete?"L'ultima scrittura era incompleta. Puoi salvare la parte verificata qui sopra.":"Salva una copia per scegliere dove conservare il risultato.",10,content.Incomplete?Ui.Amber:Ui.Muted);note.Dock=DockStyle.Fill;layout.Controls.Add(note,0,2);
                var buttons=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));layout.Controls.Add(buttons,0,3);
                var save=Ui.Button("Salva una copia",170,null,true);save.Dock=DockStyle.Fill;save.Click+=async delegate{save.Enabled=false;try{string path=PickRuntimeFile(false,content.Name);if(path==null)return;await Task.Run(()=>RuntimeFileSave(path,content.Text));if(!form.IsDisposed)note.Text="Copia salvata. La bozza resta disponibile qui.";}catch(Exception){if(!form.IsDisposed)note.Text="Salvataggio non riuscito. La bozza è ancora disponibile: riprova.";}finally{if(!form.IsDisposed)save.Enabled=true;}};buttons.Controls.Add(save);
                var remove=Ui.Button("Rimuovi bozza",150,delegate{if(Runner!=null&&Runner.Files.ActiveJournalId==entry.Id)return;if(FlowMessageDialog.Show(form,"Rimuovere questa bozza locale? Conserva prima una copia se ti serve.","Rimuovi bozza",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{FileRecovery.Delete(entry);form.Close();}catch(Exception){note.Text="Non riesco a rimuovere la bozza. Puoi riprovare.";}});remove.Enabled=Runner==null||Runner.Files.ActiveJournalId!=entry.Id;remove.Dock=DockStyle.Fill;buttons.Controls.Add(remove);
                var close=Ui.Button("Chiudi",95,form.Close);close.Dock=DockStyle.Fill;buttons.Controls.Add(close);form.CancelButton=close;Ui.ApplyTheme(form);form.ShowDialog(this);
            }
        }
    }
}
