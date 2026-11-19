using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Frociart
{
    internal static class FlowConsoleMotion
    {
        internal static Action AttachExit(Form form)
        {
            bool leaving=false,allow=false;DialogResult requestedResult=DialogResult.None;var timer=new Timer{Interval=16};var clock=new System.Diagnostics.Stopwatch();
            form.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(allow||e.CloseReason!=CloseReason.UserClosing||!UiMotion.Enabled||!form.Visible)return;e.Cancel=true;if(leaving)return;requestedResult=form.DialogResult;leaving=true;form.Enabled=false;clock.Start();timer.Start();};
            timer.Tick+=delegate{float t=Math.Min(1,clock.ElapsedMilliseconds/160f);if(form.IsDisposed){timer.Stop();return;}form.Opacity=UiMotion.Enabled?Math.Max(.01,1-UiMotion.Ease(t)):0;if(t>=1||!UiMotion.Enabled){timer.Stop();allow=true;if(requestedResult!=DialogResult.None)form.DialogResult=requestedResult;form.Close();}};
            form.Disposed+=delegate{timer.Stop();timer.Dispose();};return delegate{allow=true;form.Close();};
        }
    }
    // Native virtual list: an execution with thousands of events creates no row controls.
    internal sealed class FlowConsoleMessages : ListView
    {
        private FlowConsoleLog log;
        private ReadOnlyCollection<FlowConsoleLog.Entry> entries=new System.Collections.Generic.List<FlowConsoleLog.Entry>().AsReadOnly();
        private long revision=-1,animatedSequence;
        private readonly Font labelFont=Ui.Font(9,FontStyle.Bold),bodyFont=Ui.Mono(11),contextFont=Ui.Font(8.5f);
        private readonly Timer animation=new Timer{Interval=16};
        private readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        private long arrived;
        private readonly ImageList height;
        internal event Action<FlowConsoleLog.Entry> OpenEntry;
        internal FlowConsoleMessages()
        {
            View=View.Details;HeaderStyle=ColumnHeaderStyle.None;VirtualMode=true;OwnerDraw=true;FullRowSelect=true;
            HideSelection=false;MultiSelect=false;BorderStyle=BorderStyle.None;Columns.Add("Messaggio",600);
            AccessibleName="Letture e stampe in ordine di esecuzione";Tag="paper";DoubleBuffered=true;
            height=new ImageList{ImageSize=new Size(1,Math.Min(256,(int)Math.Round(126*DeviceDpi/96f))),ColorDepth=ColorDepth.Depth32Bit};
            height.Images.Add(new Bitmap(1,height.ImageSize.Height));SmallImageList=height;
            RetrieveVirtualItem+=delegate(object s,RetrieveVirtualItemEventArgs e){e.Item=new ListViewItem(e.ItemIndex<entries.Count?entries[e.ItemIndex].Label+": "+Preview(entries[e.ItemIndex].Text,512):"");};
            DoubleClick+=delegate{ShowSelected();};KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.Handled=true;ShowSelected();}};
            Resize+=delegate{if(Columns.Count>0)Columns[0].Width=Math.Max(40,ClientSize.Width-3);};
            animation.Tick+=delegate{if(!UiMotion.Enabled||clock.ElapsedMilliseconds-arrived>=360){animation.Stop();animatedSequence=0;}Invalidate();};
        }
        internal FlowConsoleLog.Entry SelectedEntry {get{return SelectedIndices.Count>0&&SelectedIndices[0]<entries.Count?entries[SelectedIndices[0]]:null;}}
        private void ShowSelected(){var entry=SelectedEntry;if(entry!=null&&OpenEntry!=null)OpenEntry(entry);}
        internal void Bind(FlowConsoleLog next)
        {
            if(ReferenceEquals(log,next)&&(next==null||revision==next.Revision))return;
            int oldCount=VirtualListSize,top=IsHandleCreated&&oldCount>0&&TopItem!=null?TopItem.Index:0;
            bool bottom=oldCount==0||top+Math.Max(1,ClientSize.Height/Math.Max(1,height.ImageSize.Height))>=oldCount-1;
            bool same=ReferenceEquals(log,next);long previousTail=entries.Count==0?0:entries[entries.Count-1].Sequence;
            long anchor=entries.Count>top?entries[top].Sequence:0,selected=SelectedEntry==null?0:SelectedEntry.Sequence;
            log=next;revision=next==null?-1:next.Revision;entries=next==null?new System.Collections.Generic.List<FlowConsoleLog.Entry>().AsReadOnly():next.Snapshot();VirtualListSize=entries.Count;
            SelectedIndices.Clear();if(same&&selected>0&&entries.Count>0){long index=selected-entries[0].Sequence;if(index>=0&&index<entries.Count)SelectedIndices.Add((int)index);}
            if(VirtualListSize>0&&IsHandleCreated){if(bottom)EnsureVisible(VirtualListSize-1);else if(same)TopItem=Items[(int)Math.Max(0,Math.Min(entries.Count-1,anchor-entries[0].Sequence))];}
            if(VirtualListSize>0&&bottom&&UiMotion.Enabled&&Visible&&entries[entries.Count-1].Sequence!=previousTail){animatedSequence=entries[entries.Count-1].Sequence;arrived=clock.ElapsedMilliseconds;animation.Start();}
            Invalidate();
        }
        internal void RefreshColors(){BackColor=Ui.Paper;ForeColor=Ui.Ink;Invalidate();}
        internal static string Preview(string text,int max){if(text.Length<=max)return text;if(max>0&&Char.IsHighSurrogate(text[max-1]))max--;return text.Substring(0,max)+"…";}
        protected override void OnDrawItem(DrawListViewItemEventArgs e){ }
        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            if(e.ItemIndex>=entries.Count)return;var entry=entries[e.ItemIndex];var g=e.Graphics;
            using(var paper=new SolidBrush(Ui.Paper))g.FillRectangle(paper,e.Bounds);
            float scale=DeviceDpi/96f,t=entry.Sequence==animatedSequence?Math.Min(1,(clock.ElapsedMilliseconds-arrived)/360f):1;
            float reveal=UiMotion.Spring(t);var state=g.Save();g.TranslateTransform(0,10*scale*(1-reveal));g.SmoothingMode=SmoothingMode.AntiAlias;
            bool input=entry.Kind==FlowConsoleLog.EntryKind.Input,selected=SelectedIndices.Contains(e.ItemIndex);
            Color color=Ui.KindColor(input?NodeKind.Input:NodeKind.Output);
            var box=new RectangleF(e.Bounds.Left+(input?30:8)*scale,e.Bounds.Top+6*scale,Math.Max(20,e.Bounds.Width-42*scale),e.Bounds.Height-12*scale);
            using(var path=Ui.Rounded(box,12*scale))using(var fill=new SolidBrush(Color.FromArgb((int)(255*Math.Min(1,t*1.8f)),Ui.Surface)))using(var border=new Pen(selected?Ui.Blue:Color.FromArgb(100,color),selected?2:1)) {g.FillPath(fill,path);g.DrawPath(border,path);}
            {
                TextRenderer.DrawText(g,entry.Label,labelFont,Rectangle.Round(new RectangleF(box.Left+12*scale,box.Top+9*scale,box.Width-24*scale,19*scale)),color,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                string preview=entry.Text.Length==0?"(testo vuoto)":Preview(entry.Text,1000);
                TextRenderer.DrawText(g,preview,bodyFont,Rectangle.Round(new RectangleF(box.Left+12*scale,box.Top+31*scale,box.Width-24*scale,45*scale)),Ui.Ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g,entry.Context+" · Invio per aprire",contextFont,Rectangle.Round(new RectangleF(box.Left+12*scale,box.Bottom-27*scale,box.Width-24*scale,19*scale)),Ui.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            }
            if(Focused&&selected)ControlPaint.DrawFocusRectangle(g,Rectangle.Round(box),Ui.Ink,Ui.Surface);g.Restore(state);
        }
        protected override void Dispose(bool disposing){if(disposing){animation.Stop();animation.Dispose();height.Dispose();labelFont.Dispose();bodyFont.Dispose();contextFont.Dispose();}base.Dispose(disposing);}
    }

    internal sealed class FlowConsoleWindow : Form
    {
        private readonly Func<Interpreter> getRunner;
        private readonly Func<bool> running;
        private readonly Func<string> getPending;
        private readonly Action<string> setPending,send;
        private readonly Action play,step,file;
        private readonly Action<FlowConsoleLog.Entry> locate;
        internal readonly FlowConsoleMessages Messages=new FlowConsoleMessages();
        internal readonly TextBox Plain=Ui.TextBox(true),Input=Ui.TextBox(false);
        internal readonly ComboBox Mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
        internal readonly FlowButton Copy,Save,Clear,Play,Step,Send,FileAction;
        private readonly Label info=Ui.Label("",9.5f,Ui.Muted),question=Ui.Label("",11,Ui.Ink,true),error=Ui.Label("",9.5f,Ui.Red);
        private readonly TableLayoutPanel inputArea,root;
        private readonly Panel viewer;
        private readonly Label empty=Ui.Label("Non ci sono messaggi nella console.\nLe letture e le stampe compariranno qui.",11,Ui.Muted);
        private readonly Timer poll=new Timer{Interval=100};
        private Interpreter observed;private long generation=-1,revision=-1;private string waitingKey;private bool synchronizing;
        private readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        private string notice;private long noticeUntil,noticeRevision=-1,lastSend=-1000;
        private readonly Action closeImmediately;
        [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr first,IntPtr second);
        internal Action<string> ClipboardWrite=delegate(string value){var data=new DataObject();data.SetData(DataFormats.UnicodeText,false,value??"");Clipboard.SetDataObject(data,true,3,40);};
        internal string ExportText {get{var r=getRunner();return r==null?"":r.ConsoleLog.ToText();}}
        internal FlowConsoleWindow(Func<Interpreter> getRunner,Func<bool> running,Func<string> getPending,Action<string> setPending,
            Action<string> send,Action play,Action step,Action file,Action<FlowConsoleLog.Entry> locate)
        {
            this.getRunner=getRunner;this.running=running;this.getPending=getPending;this.setPending=setPending;this.send=send;this.play=play;this.step=step;this.file=file;this.locate=locate;
            Text="Console — Frociart";StartPosition=FormStartPosition.CenterParent;Size=new Size(800,710);MinimumSize=new Size(620,530);ShowInTaskbar=false;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=Ui.Font();BackColor=Ui.Surface;
            root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=6};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(var size in new[]{44,42,0,48,0,48})root.RowStyles.Add(new RowStyle(size==0?SizeType.Percent:SizeType.Absolute,size==0?100:size));
            root.RowStyles[4].SizeType=SizeType.AutoSize;Controls.Add(root);
            var title=Ui.Label("La tua esecuzione, messaggio per messaggio",14,Ui.Ink,true);title.Dock=DockStyle.Fill;root.Controls.Add(title,0,0);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};Mode.Items.AddRange(new object[]{"Messaggi","Testo"});Mode.Width=150;Mode.AccessibleName="Vista della console";Mode.SelectedIndex=ReadMode();Mode.SelectedIndexChanged+=delegate{FlowTransitions.Change(viewer);ApplyMode();WriteMode();};actions.Controls.Add(Mode);
            Copy=Ui.Button("Copia",90,CopyAll);Copy.AccessibleDescription="Copia la cronologia conservata, con i valori inseriti e le stampe.";Save=Ui.Button("Salva .txt…",115,SaveAll);Clear=Ui.Button("Svuota",90,ClearLog);
            actions.Controls.Add(Copy);actions.Controls.Add(Save);actions.Controls.Add(Clear);root.Controls.Add(actions,0,1);
            viewer=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};Plain.Dock=Messages.Dock=DockStyle.Fill;Plain.ReadOnly=true;Plain.WordWrap=false;Plain.ScrollBars=ScrollBars.Both;Plain.Font=Ui.Mono(11);Plain.MaxLength=Int32.MaxValue;Plain.AccessibleName="Cronologia di letture e stampe in testo";
            Messages.OpenEntry+=OpenMessage;empty.Dock=DockStyle.Fill;empty.TextAlign=ContentAlignment.MiddleCenter;empty.BackColor=Ui.Paper;empty.Tag="paper";viewer.Controls.Add(Plain);viewer.Controls.Add(Messages);viewer.Controls.Add(empty);root.Controls.Add(viewer,0,2);
            info.Dock=DockStyle.Fill;info.Padding=new Padding(0,8,0,0);info.AutoEllipsis=true;root.Controls.Add(info,0,3);
            inputArea=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=2,RowCount=3,AutoSize=true,Margin=Padding.Empty};inputArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));inputArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
            question.Dock=DockStyle.Fill;question.AutoSize=true;question.AutoEllipsis=true;question.MaximumSize=new Size(540,80);inputArea.Controls.Add(question,0,0);
            var fullQuestion=Ui.Button("Domanda…",90,delegate{var r=getRunner();if(r!=null&&r.State==RuntimeState.WaitingForInput)TutorTextDialog.ShowText(this,"Domanda del programma",r.InputPrompt);});fullQuestion.AccessibleName="Leggi la domanda completa del programma";inputArea.Controls.Add(fullQuestion,1,0);
            Input.Dock=DockStyle.Fill;Input.Font=Ui.Mono(12);Input.MaxLength=Values.MaxStringLength;Input.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.Handled=e.SuppressKeyPress=true;Submit();}};
            Input.TextChanged+=delegate{if(!synchronizing&&CanSend())setPending(Input.Text);};inputArea.Controls.Add(Input,0,1);Send=Ui.Button("Invia",90,Submit,true);inputArea.Controls.Add(Send,1,1);error.AutoSize=true;error.Dock=DockStyle.Fill;inputArea.Controls.Add(error,0,2);inputArea.SetColumnSpan(error,2);root.Controls.Add(inputArea,0,4);
            var controls=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};Play=Ui.Button("Esegui",120,delegate{play();RefreshState();},true);Step=Ui.Button("Avanti",100,delegate{step();RefreshState();});FileAction=Ui.Button("Scegli file…",135,delegate{file();RefreshState();});var close=Ui.Button("Chiudi",90,Close);controls.Controls.Add(Play);controls.Controls.Add(Step);controls.Controls.Add(FileAction);controls.Controls.Add(close);root.Controls.Add(controls,0,5);CancelButton=close;
            poll.Tick+=delegate{RefreshState();};Shown+=delegate{RefreshState();poll.Start();if(Messages.VirtualListSize>0)Messages.EnsureVisible(Messages.VirtualListSize-1);};FormClosed+=delegate{poll.Stop();};Ui.ThemeChanged+=ThemeChanged;Ui.ApplyTheme(this);Messages.RefreshColors();ApplyMode();closeImmediately=FlowConsoleMotion.AttachExit(this);
        }
        private static string ModePath {get{return Path.Combine(Path.GetDirectoryName(WorkspaceLayout.PathName),"console-view.txt");}}
        private static int ReadMode(){try{return File.Exists(ModePath)&&new FileInfo(ModePath).Length<8&&File.ReadAllText(ModePath).Trim()=="1"?1:0;}catch(IOException){return 0;}catch(UnauthorizedAccessException){return 0;}}
        private void WriteMode(){try{Directory.CreateDirectory(Path.GetDirectoryName(ModePath));File.WriteAllText(ModePath,Mode.SelectedIndex.ToString());}catch(IOException){SetNotice("Vista cambiata per questa sessione; preferenza non salvata.");}catch(UnauthorizedAccessException){SetNotice("Vista cambiata per questa sessione; preferenza non salvata.");}}
        internal void CloseImmediately(){closeImmediately();}
        private void ThemeChanged(object sender,EventArgs e){Ui.ApplyTheme(this);Messages.RefreshColors();}
        private bool CanSend(){var r=getRunner();return r!=null&&ReferenceEquals(r,observed)&&r.Generation==generation&&r.State==RuntimeState.WaitingForInput;}
        private void Submit(){if(!CanSend()||clock.ElapsedMilliseconds-lastSend<250)return;long before=getRunner().ConsoleLog.Revision;send(Input.Text);if(getRunner()!=null&&getRunner().ConsoleLog.Revision!=before)lastSend=clock.ElapsedMilliseconds;RefreshState();if(inputArea.Visible)Input.Focus();}
        internal void FocusPendingInput(){if(inputArea.Visible){Input.Focus();Input.SelectAll();}}
        private void SetNotice(string value,bool tiedToContents=false){notice=value;noticeUntil=clock.ElapsedMilliseconds+3000;noticeRevision=tiedToContents&&getRunner()!=null?getRunner().ConsoleLog.Revision:-1;info.Text=value;}
        private void ApplyMode(){if(viewer==null)return;Plain.Visible=Mode.SelectedIndex==1;Messages.Visible=!Plain.Visible;if(Plain.Visible){Plain.Text=ExportText.Replace("\r\n","\n").Replace("\n","\r\n");Plain.BringToFront();}else Messages.BringToFront();empty.Visible=getRunner()==null||getRunner().ConsoleLog.Count==0;if(empty.Visible)empty.BringToFront();}
        private void UpdatePlain()
        {
            int caret=Plain.SelectionStart,length=Plain.SelectionLength;
            int first=Plain.IsHandleCreated?(int)SendMessage(Plain.Handle,0xCE,IntPtr.Zero,IntPtr.Zero):0;
            bool tail=length==0&&first+Math.Max(1,Plain.ClientSize.Height/Math.Max(1,Plain.Font.Height))>=Plain.GetLineFromCharIndex(Plain.TextLength)-1;
            string next=ExportText.Replace("\r\n","\n").Replace("\n","\r\n");
            if(next.StartsWith(Plain.Text,StringComparison.Ordinal))Plain.AppendText(next.Substring(Plain.TextLength));else Plain.Text=next;
            Plain.Select(tail?Plain.TextLength:Math.Min(caret,Plain.TextLength),Math.Min(length,Math.Max(0,Plain.TextLength-caret)));
            if(tail)Plain.ScrollToCaret();else if(Plain.IsHandleCreated){int current=(int)SendMessage(Plain.Handle,0xCE,IntPtr.Zero,IntPtr.Zero);SendMessage(Plain.Handle,0xB6,IntPtr.Zero,(IntPtr)(first-current));}
        }
        internal void RefreshState()
        {
            if(IsDisposed)return;var r=getRunner();bool session=!ReferenceEquals(observed,r)||r!=null&&generation!=r.Generation;
            observed=r;generation=r==null?-1:r.Generation;var log=r==null?null:r.ConsoleLog;long version=log==null?-1:log.Revision;
            bool changed=session||revision!=version;if(changed){revision=version;Messages.Bind(log);if(Plain.Visible)UpdatePlain();if(session||noticeRevision>=0&&noticeRevision!=version)notice=null;}
            empty.Visible=log==null||log.Count==0;if(empty.Visible)empty.BringToFront();
            bool waiting=r!=null&&r.State==RuntimeState.WaitingForInput;string key=waiting?r.CurrentFunctionId+":"+r.CurrentNodeId+":"+r.Steps:null;bool nextQuestion=session||key!=waitingKey;waitingKey=key;
            inputArea.Visible=waiting;root.RowStyles[4].SizeType=SizeType.AutoSize;
            if(waiting){question.Text=r.InputPrompt;Input.AccessibleName="Valore per "+r.InputVariable;error.Text=r.LastMessage!=null&&!r.LastMessage.StartsWith("In attesa di un valore",StringComparison.Ordinal)?r.LastMessage:"";if(nextQuestion||!Input.Focused&&Input.Text!=getPending()){synchronizing=true;try{Input.Text=getPending()??"";}finally{synchronizing=false;}}if(nextQuestion&&Visible){Input.Focus();Input.SelectAll();}}
            question.MaximumSize=new Size(Math.Max(120,inputArea.ClientSize.Width-110),Math.Max(60,(int)(80*DeviceDpi/96f)));Send.Enabled=waiting&&clock.ElapsedMilliseconds-lastSend>=250;
            Play.Text=running()?"Pausa":r!=null&&(r.State==RuntimeState.Completed||r.State==RuntimeState.Error)?"Riprova":r!=null&&r.Steps>0?"Riprendi":"Esegui";Play.Enabled=!waiting&&(r==null||r.State!=RuntimeState.WaitingForFile);
            Step.Enabled=r==null||r.State!=RuntimeState.WaitingForInput&&r.State!=RuntimeState.WaitingForFile&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;FileAction.Visible=r!=null&&r.State==RuntimeState.WaitingForFile;
            Copy.Enabled=Save.Enabled=Clear.Enabled=log!=null&&log.Count>0;
            string state=r==null?"Pronto":r.State==RuntimeState.Completed?"Completato":r.State==RuntimeState.Error?"Interrotto: "+r.Error:waiting?"In attesa di "+r.InputVariable:r.State==RuntimeState.WaitingForFile?"Scegli il file per continuare":running()?"In esecuzione":"In pausa";
            info.Text=state+" · "+(log==null?0:log.Count)+" messaggi"+(log!=null&&log.Discarded>0?" · "+log.Discarded+" precedenti rimossi dal limite di memoria":"");info.ForeColor=r!=null&&r.State==RuntimeState.Error?Ui.Red:Ui.Muted;
            if(notice!=null&&clock.ElapsedMilliseconds<noticeUntil)info.Text=notice;
        }
        private void ClearLog(){var r=getRunner();if(r==null)return;FlowTransitions.Change(viewer);r.ConsoleLog.Clear();RefreshState();}
        private void CopyAll(){if(getRunner()==null||getRunner().ConsoleLog.Count==0)return;try{ClipboardWrite(ExportText);SetNotice("Cronologia copiata, compresi i valori inseriti.",true);}catch(System.Runtime.InteropServices.ExternalException){SetNotice("Appunti occupati. Riprova Copia oppure Salva .txt.");}}
        private void SaveAll(){if(!Save.Enabled)return;if(running())play();string snapshot=ExportText;using(var dialog=new SaveFileDialog{Title="Salva la console",Filter="Testo UTF-8 (*.txt)|*.txt",FileName="Frociart_console.txt",DefaultExt="txt",AddExtension=true,OverwritePrompt=true})if(dialog.ShowDialog(this)==DialogResult.OK){try{WriteText(dialog.FileName,snapshot);SetNotice("Console salvata nel file scelto.");}catch(IOException){SetNotice("Non riesco a salvare il file. La console resta disponibile.");}catch(UnauthorizedAccessException){SetNotice("Accesso al file negato. Scegli un'altra cartella.");}}}
        internal static void WriteText(string path,string text){SourceCodeDialog.WriteSource(path,text);}
        private void OpenMessage(FlowConsoleLog.Entry entry)
        {
            var r=getRunner();if(r==null||!ReferenceEquals(r.ConsoleLog.Find(entry.Sequence),entry))return;
            using(var dialog=new Form{Text=entry.Label+" — Frociart",Size=new Size(670,540),MinimumSize=new Size(540,420),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,AutoScaleMode=AutoScaleMode.Dpi,AutoScaleDimensions=new SizeF(96,96),Font=Ui.Font(),BackColor=Ui.Surface})
            {
                var body=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=3};body.RowStyles.Add(new RowStyle(SizeType.Absolute,52));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Absolute,48));dialog.Controls.Add(body);
                var caption=Ui.Label(entry.Context+(entry.TextLimited?" · testo limitato":""),10,Ui.Muted);caption.Dock=DockStyle.Fill;body.Controls.Add(caption,0,0);
                var text=Ui.TextBox(true);text.ReadOnly=true;text.WordWrap=true;text.Font=Ui.Mono(12);text.ScrollBars=ScrollBars.Vertical;text.Dock=DockStyle.Fill;text.Text=(entry.Prompt.Length==0?"":entry.Prompt+"\r\n\r\n")+entry.Text;text.AccessibleName="Messaggio completo della console";body.Controls.Add(text,0,1);
                var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};actions.Controls.Add(Ui.Button("Copia valore",125,delegate{try{ClipboardWrite(entry.Text);}catch(System.Runtime.InteropServices.ExternalException){caption.Text="Appunti occupati. Seleziona il testo e riprova.";}}));
                if(locate!=null)actions.Controls.Add(Ui.Button("Vai al blocco",130,delegate{dialog.Close();locate(entry);}));var close=Ui.Button("Chiudi",90,dialog.Close);actions.Controls.Add(close);dialog.CancelButton=close;body.Controls.Add(actions,0,2);Ui.ApplyTheme(dialog);FlowConsoleMotion.AttachExit(dialog);dialog.ShowDialog(this);
            }
        }
        protected override void Dispose(bool disposing){if(disposing){poll.Stop();poll.Dispose();Ui.ThemeChanged-=ThemeChanged;}base.Dispose(disposing);}
        protected override bool ProcessCmdKey(ref Message message,Keys keys)
        {
            if(keys==(Keys.Control|Keys.S)){SaveAll();return true;}
            if(Messages.ContainsFocus&&keys==(Keys.Control|Keys.A)){Mode.SelectedIndex=1;Plain.Focus();Plain.SelectAll();return true;}
            if(Messages.ContainsFocus&&keys==(Keys.Control|Keys.C)){var entry=Messages.SelectedEntry;if(entry!=null)try{ClipboardWrite(entry.Text);SetNotice("Valore copiato.");}catch(System.Runtime.InteropServices.ExternalException){SetNotice("Appunti occupati. Riprova Copia.");}return true;}
            if(keys==Keys.F5){if(Play.Enabled)Play.PerformClick();return true;}if(keys==Keys.F11||keys==Keys.F10){if(Step.Enabled)Step.PerformClick();return true;}
            return base.ProcessCmdKey(ref message,keys);
        }
    }
}

