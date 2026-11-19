using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Frociart {
 internal sealed partial class SourceCodeDialog {
  internal sealed class Execution {
   internal Func<bool> Current,Running,FileBusy;internal Func<Interpreter> Runner;internal Func<string> Pending,PauseReason;
   internal Action<string> SetPending,Submit;internal Action Play,Step,Pause,Reset,File;
  }
  Execution execution;bool refreshingExecution,synchronizingInput,closedExecution;string inputVisit;long lastInputAccepted=-1000;
  readonly System.Diagnostics.Stopwatch inputClock=System.Diagnostics.Stopwatch.StartNew();
  Interpreter inputOwner;long inputGeneration=-1;
  FlowSourceText runtimeBoundSource;bool runtimeBoundSupport,runtimeHasOrigins;
  internal FlowCodeView RuntimeCode;
  internal FlowButton RuntimePlay,RuntimeStep,RuntimePause,RuntimeReset,RuntimeSend,RuntimeFile,RuntimeQuestionMore;
  internal TextBox RuntimeInput,RuntimeOutput;internal CheckBox RuntimeFollow;
  internal Label RuntimeNotice,RuntimeQuestion;
  FlowScrollPanel executionArea;TableLayoutPanel executionBody,executionInput,sourceLayout;FlowLayoutPanel executionButtons;
  Panel executionOutput;Label outputHeading;Interpreter outputOwner;long outputGeneration=-1,outputRefresh;int outputCount=-1;bool outputWasRunning;
  int executionLayoutDepth;
  int layoutWidth=-1,layoutHeight,layoutFlags;float layoutReserved;string layoutNotice,layoutQuestion;Font layoutNoticeFont,layoutQuestionFont,layoutFollowFont;Size layoutPlaySize;
  MotionValue executionHeight;bool executionGeometryAnimating,geometryQueued;int geometryTicket;float pendingGeometryHeight;long executionRefreshes;
  Panel executionCodeHost;bool codeViewportHeld;
  void BuildSourceRuntime(TableLayoutPanel layout,Panel codeHost){
   sourceLayout=layout;executionCodeHost=codeHost;
   executionHeight=new MotionValue(sourceLayout);executionHeight.Changed+=delegate{if(closedExecution)return;ApplySourceHeight();if(executionGeometryAnimating&&!executionHeight.Running){executionGeometryAnimating=false;ReleaseSourceViewport();if(RuntimeFollow.Checked)RuntimeCode.RevealHighlighted();}};
   RuntimeCode=new FlowCodeView{Dock=DockStyle.Fill,Visible=false,Name="RuntimeSource",Font=Ui.Mono(11)};codeHost.Controls.Add(RuntimeCode);
   RuntimeCode.MouseUp+=delegate{UpdateLocate();};RuntimeCode.KeyUp+=delegate{UpdateLocate();};RuntimeCode.BlockRequested+=(function,node)=>{if(locate!=null&&(execution==null||execution.Current())){locate(function,node);Close();}};
   executionArea=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,Visible=false,Margin=Padding.Empty};layout.Controls.Add(executionArea,0,2);
   executionBody=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=4,Margin=Padding.Empty,Padding=Padding.Empty};executionBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<4;i++)executionBody.RowStyles.Add(new RowStyle(SizeType.AutoSize));executionArea.Controls.Add(executionBody);
   executionButtons=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=Padding.Empty};executionBody.Controls.Add(executionButtons,0,0);
   RuntimePlay=Ui.Button("Esegui",105,()=>ExecuteSource("play"),true);RuntimePlay.Name="SourcePlay";
   RuntimeStep=Ui.Button("Avanti",90,()=>ExecuteSource("step"));RuntimeStep.Name="SourceStep";
   RuntimePause=Ui.Button("Pausa",90,()=>ExecuteSource("pause"));RuntimePause.Name="SourcePause";
   RuntimeReset=Ui.Button("Ricomincia",112,()=>ExecuteSource("reset"));RuntimeReset.Name="SourceReset";
   RuntimeFollow=new FlowCheckBox{Text="Segui blocco",Checked=true,AutoSize=false,ForeColor=Ui.Ink,Margin=new Padding(8,3,0,0),AccessibleName="Segui il blocco attivo nel codice"};
   foreach(Control c in new Control[]{RuntimePlay,RuntimeStep,RuntimePause,RuntimeReset,RuntimeFollow})executionButtons.Controls.Add(c);
   RuntimeFollow.CheckedChanged+=delegate{if(RuntimeCode.Source!=null)RuntimeCode.Highlight(null,null,false);RefreshSourceRuntime(true);};
   RuntimeNotice=Ui.Label("",9.5f,Ui.Muted);RuntimeNotice.AutoSize=true;RuntimeNotice.Dock=DockStyle.Top;RuntimeNotice.Margin=new Padding(2,4,2,7);RuntimeNotice.AccessibleName="Stato della prova nel codice";executionBody.Controls.Add(RuntimeNotice,0,1);
   executionInput=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=2,Margin=new Padding(0,0,0,8),Visible=false};executionInput.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));executionInput.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));executionInput.RowStyles.Add(new RowStyle(SizeType.AutoSize));executionInput.RowStyles.Add(new RowStyle(SizeType.AutoSize));executionBody.Controls.Add(executionInput,0,2);
   RuntimeQuestion=Ui.Label("",10,Ui.Ink,true);RuntimeQuestion.AutoSize=true;RuntimeQuestion.Dock=DockStyle.Top;executionInput.Controls.Add(RuntimeQuestion,0,0);executionInput.SetColumnSpan(RuntimeQuestion,2);
   RuntimeQuestionMore=Ui.Button("Leggi tutto",105,delegate{if(execution!=null&&execution.Current()){var r=execution.Runner();if(r!=null&&r.State==RuntimeState.WaitingForInput)TutorTextDialog.ShowText(this,"Richiesta per "+r.InputVariable,r.InputPrompt);}});RuntimeQuestionMore.Dock=DockStyle.Top;RuntimeQuestionMore.Visible=false;executionInput.Controls.Add(RuntimeQuestionMore,1,0);
   RuntimeInput=Ui.TextBox();RuntimeInput.Dock=DockStyle.Fill;RuntimeInput.MaxLength=Values.MaxStringLength;RuntimeInput.TextChanged+=delegate{if(!synchronizingInput&&CanSubmitSource())execution.SetPending(RuntimeInput.Text);};RuntimeInput.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.Handled=e.SuppressKeyPress=true;SubmitSourceInput();}};executionInput.Controls.Add(RuntimeInput,0,1);
   RuntimeSend=Ui.Button("Invia",105,SubmitSourceInput,true);RuntimeSend.Dock=DockStyle.Fill;executionInput.Controls.Add(RuntimeSend,1,1);
   RuntimeFile=Ui.Button("Scegli file…",145,()=>ExecuteSource("file"));RuntimeFile.Name="SourceFile";RuntimeFile.Visible=false;executionButtons.Controls.Add(RuntimeFile);
   executionOutput=new Panel{Dock=DockStyle.Top,Height=82,Margin=new Padding(0,0,0,8),Visible=false};outputHeading=Ui.Label("Il risultato",9.5f,Ui.Ink,true);outputHeading.Dock=DockStyle.Top;outputHeading.Height=24;RuntimeOutput=Ui.TextBox(true);RuntimeOutput.ReadOnly=true;RuntimeOutput.Dock=DockStyle.Fill;RuntimeOutput.Font=Ui.Mono(10);RuntimeOutput.ScrollBars=ScrollBars.Both;RuntimeOutput.WordWrap=false;RuntimeOutput.MaxLength=Int32.MaxValue;RuntimeOutput.AccessibleName="Risultato effettivo della prova del flowchart";executionOutput.Controls.Add(RuntimeOutput);executionOutput.Controls.Add(outputHeading);executionBody.Controls.Add(executionOutput,0,3);
   executionBody.SizeChanged+=delegate{if(!refreshingExecution)LayoutSourceExecution();};executionArea.SizeChanged+=delegate{if(!refreshingExecution&&(executionArea.Width!=layoutWidth+24*DeviceDpi/96||sourceLayout.ClientSize.Height!=layoutHeight))LayoutSourceExecution();};sourceLayout.SizeChanged+=delegate{if(!refreshingExecution)LayoutSourceExecution();};
   FormClosing+=delegate{PauseSourceRuntime();};Disposed+=delegate{closedExecution=true;};
  }
  internal void BindExecution(Execution actions){execution=actions;executionArea.Visible=actions!=null;if(actions==null){RuntimeCode.Visible=false;Editor.Visible=true;RuntimeCode.Bind(null);runtimeBoundSource=null;}RefreshSourceRuntime(true);LayoutSourceExecution();}
  internal void RefreshSourceRuntime(bool force=false){
   if(execution==null||closedExecution||IsDisposed||refreshingExecution)return;executionRefreshes++;refreshingExecution=true;bool focusInput=false;executionBody.SuspendLayout();
   try{
    bool current=execution.Current();var r=current?execution.Runner():null;bool running=current&&execution.Running();
    if(!ReferenceEquals(runtimeBoundSource,Source)||runtimeBoundSupport!=Support.Checked){runtimeBoundSource=Source;runtimeBoundSupport=Support.Checked;RuntimeCode.Bind(Source,Support.Checked);runtimeHasOrigins=Source!=null&&Source.Lines.Any(l=>l.Origins.Count>0);}
    bool hasSource=Source!=null&&Source.Text.Length>0,valid=current&&hasSource&&runtimeHasOrigins;
    RuntimeCode.Visible=hasSource;Editor.Visible=!hasSource;if(hasSource)RuntimeCode.BringToFront();
    bool waiting=r!=null&&r.State==RuntimeState.WaitingForInput,waitingFile=r!=null&&r.State==RuntimeState.WaitingForFile;
    bool terminal=r!=null&&(r.State==RuntimeState.Completed||r.State==RuntimeState.Error);
    RuntimePlay.Text=terminal?"Riprova":r!=null&&r.Steps>0?"Riprendi":"Esegui";RuntimePlay.Enabled=valid&&!running&&!waiting&&!waitingFile;
    RuntimeStep.Enabled=valid&&!running&&!waiting&&!waitingFile&&!terminal;RuntimePause.Enabled=current&&running;RuntimeReset.Enabled=current&&r!=null;
    bool fileBusy=execution.FileBusy!=null&&execution.FileBusy();RuntimeFollow.Enabled=valid;RuntimeFile.Visible=waitingFile;RuntimeFile.Enabled=current&&waitingFile&&!fileBusy;
    RuntimeFile.Text=fileBusy?"Attendi…":waitingFile&&r.PendingFile.Kind!=FlowFileRequestKind.Read?"Salva file…":"Scegli file…";
    bool inputChanged=executionInput.Visible!=waiting;if(inputChanged)FlowTransitions.Change(executionArea);executionInput.Visible=waiting;
    if(!ReferenceEquals(inputOwner,r)||r!=null&&inputGeneration!=r.Generation){inputOwner=r;inputGeneration=r==null?-1:r.Generation;inputVisit=null;lastInputAccepted=-1000;}
    string visit=waiting?r.Generation+":"+r.CurrentFrameId+":"+r.CurrentVisit+":"+r.InputVariable:null;
    if(visit!=inputVisit){inputVisit=visit;synchronizingInput=true;RuntimeInput.Text=waiting?execution.Pending():"";synchronizingInput=false;}
    RuntimeSend.Enabled=current&&waiting;
    if(waiting){string question=r.InputPrompt;bool longQuestion=question.Length>220;int length=Math.Min(220,question.Length);if(length>0&&length<question.Length&&Char.IsHighSurrogate(question[length-1])&&Char.IsLowSurrogate(question[length]))length--;RuntimeQuestion.Text=longQuestion?question.Substring(0,length)+"…":question;RuntimeQuestion.AccessibleName=question;RuntimeQuestionMore.Visible=longQuestion;RuntimeQuestionMore.AccessibleName="Leggi la richiesta completa per "+r.InputVariable;executionInput.SetColumnSpan(RuntimeQuestion,longQuestion?1:2);RuntimeInput.AccessibleName="Valore per "+r.InputVariable;}
    string pauseReason=execution.PauseReason==null?null:execution.PauseReason();
    string heading=!current?"Il flowchart è cambiato. Riapri il codice per provarlo.":!hasSource?Source==null?"Genera il codice prima di seguire la prova.":"Il modello non ha prodotto testo da seguire.":!runtimeHasOrigins?"Questo modello non collega le righe ai blocchi. Usa un generatore integrato per seguire la prova.":r==null?"Prova il flowchart · il codice esportato si esegue nel suo ambiente.":r.State==RuntimeState.Error?"Errore: "+r.Error:waiting?(String.IsNullOrEmpty(r.LastMessage)?r.InputPrompt:r.LastMessage):waitingFile?r.LastMessage:r.State==RuntimeState.Completed?"Prova completata":running?"In esecuzione":String.IsNullOrEmpty(pauseReason)?"In pausa":pauseReason;
    string function=r==null?null:r.CurrentFunctionId,node=r==null?null:r.CurrentNodeId;
    if(r!=null&&node==null){function=r.LastFunctionId;node=r.LastNodeId;}
    var lines=RuntimeCode.Highlight(current?function:null,current?node:null,RuntimeFollow.Checked);
    if(r!=null){heading+=" · "+r.CurrentFunctionName+(r.CallDepth>1?" · chiamata "+r.CallDepth:"")+" · "+r.Steps+" passi";if(lines.Count>0)heading+=" · riga "+lines[0].Number;}
    RuntimeNotice.Text=heading;RuntimeNotice.ForeColor=r!=null&&r.State==RuntimeState.Error?Ui.Red:!current?Ui.Amber:Ui.Muted;
    RefreshSourceOutput(r,running);
    focusInput=inputChanged&&waiting&&ContainsFocus;
   }finally{executionBody.ResumeLayout(true);refreshingExecution=false;}
   LayoutSourceExecution();UpdateLocate();if(focusInput)FocusSourceInput();
  }
  void RefreshSourceOutput(Interpreter r,bool running){
   bool visible=r!=null&&r.Output.Count>0;if(executionOutput.Visible!=visible)FlowTransitions.Change(executionArea);executionOutput.Visible=visible;
   if(!visible){if(RuntimeOutput.TextLength>0)RuntimeOutput.Clear();outputOwner=null;outputCount=-1;return;}
   outputHeading.Text=running?"Il risultato · anteprima durante l’esecuzione":"Il risultato";
   if(ReferenceEquals(outputOwner,r)&&outputGeneration==r.Generation&&outputWasRunning==running&&(outputCount==r.Output.Count||running&&inputClock.ElapsedMilliseconds-outputRefresh<250))return;
   string text=r.OutputText;if(running&&text.Length>2000){int start=text.Length-2000;if(start>0&&Char.IsLowSurrogate(text[start])&&Char.IsHighSurrogate(text[start-1]))start++;text="…\r\n"+text.Substring(start);}
   RuntimeOutput.Text=text;outputOwner=r;outputGeneration=r.Generation;outputWasRunning=running;outputCount=r.Output.Count;outputRefresh=inputClock.ElapsedMilliseconds;
  }
  void LayoutSourceExecution(){
   if(sourceLayout==null||executionLayoutDepth>0)return;executionLayoutDepth++;
   try{float dpi=DeviceDpi/96f;int width=Math.Max(1,executionArea.Width-(int)(24*dpi));
    float reserved=sourceLayout.Padding.Vertical;for(int i=0;i<sourceLayout.RowStyles.Count;i++)if(i!=2&&i!=3&&sourceLayout.RowStyles[i].SizeType==SizeType.Absolute)reserved+=sourceLayout.RowStyles[i].Height;
    int flags=(execution==null?0:1)|(executionInput.Visible?2:0)|(executionOutput.Visible?4:0)|(RuntimeFile.Visible?8:0)|(executionArea.Visible?16:0)|(RuntimeQuestionMore.Visible?32:0);
    if(width==layoutWidth&&sourceLayout.ClientSize.Height==layoutHeight&&flags==layoutFlags&&reserved==layoutReserved&&RuntimeNotice.Text==layoutNotice&&RuntimeQuestion.Text==layoutQuestion&&ReferenceEquals(RuntimeNotice.Font,layoutNoticeFont)&&ReferenceEquals(RuntimeQuestion.Font,layoutQuestionFont)&&ReferenceEquals(RuntimeFollow.Font,layoutFollowFont)&&RuntimePlay.Size==layoutPlaySize)return;
    layoutWidth=width;layoutHeight=sourceLayout.ClientSize.Height;layoutFlags=flags;layoutReserved=reserved;layoutNotice=RuntimeNotice.Text;layoutQuestion=RuntimeQuestion.Text;layoutNoticeFont=RuntimeNotice.Font;layoutQuestionFont=RuntimeQuestion.Font;layoutFollowFont=RuntimeFollow.Font;layoutPlaySize=RuntimePlay.Size;
    if(executionBody.MaximumSize.Width!=width)executionBody.MaximumSize=new Size(width,0);if(RuntimeNotice.MaximumSize.Width!=width)RuntimeNotice.MaximumSize=new Size(width,0);if(RuntimeQuestion.MaximumSize.Width!=width)RuntimeQuestion.MaximumSize=new Size(width,0);
    RuntimeFollow.Size=new Size(TextRenderer.MeasureText(RuntimeFollow.Text,RuntimeFollow.Font).Width+(int)(32*dpi),RuntimePlay.Height);
    int cap=Math.Max((int)(56*dpi),Math.Min((int)(215*dpi),sourceLayout.ClientSize.Height-(int)reserved-(int)(120*dpi)));
    int height=execution==null?0:Math.Min(cap,executionBody.GetPreferredSize(new Size(width,0)).Height+8);
    SetSourceHeight(height);
   }finally{executionLayoutDepth--;}
  }
  void SetSourceHeight(float height){
   pendingGeometryHeight=height;
   if(!sourceLayout.Visible||!sourceLayout.IsHandleCreated||!UiMotion.Enabled){geometryTicket++;geometryQueued=false;executionHeight.Jump(height);ApplySourceHeight();ReleaseSourceViewport();return;}
   if(geometryQueued||Math.Abs(executionHeight.Target-height)<.01f)return;
   geometryQueued=true;int ticket=++geometryTicket;
   sourceLayout.BeginInvoke(new Action(delegate{if(closedExecution||sourceLayout.IsDisposed||ticket!=geometryTicket)return;geometryQueued=false;HoldSourceViewport(pendingGeometryHeight);executionHeight.Set(pendingGeometryHeight,320);executionGeometryAnimating=executionHeight.Running;if(!executionGeometryAnimating){ApplySourceHeight();ReleaseSourceViewport();}}));
  }
  void HoldSourceViewport(float height){
   // Animate the clipping viewport. Reflowing long native rich text at every
   // geometry frame is expensive; its size is restored once at the endpoint.
   int final=Math.Max(1,executionCodeHost.Height+(int)(sourceLayout.RowStyles[2].Height-height));int extent=Math.Max(executionCodeHost.Height,final)+(int)(32*DeviceDpi/96f);
   executionCodeHost.SuspendLayout();try{foreach(Control c in new Control[]{Editor,RuntimeCode}){c.Dock=DockStyle.None;c.SetBounds(0,0,executionCodeHost.ClientSize.Width,extent);}codeViewportHeld=true;}finally{executionCodeHost.ResumeLayout(false);}
  }
  void ReleaseSourceViewport(){if(!codeViewportHeld)return;codeViewportHeld=false;executionCodeHost.SuspendLayout();try{Editor.Dock=DockStyle.Fill;RuntimeCode.Dock=DockStyle.Fill;}finally{executionCodeHost.ResumeLayout(true);}}
  void ApplySourceHeight(){executionLayoutDepth++;try{float height=Math.Max(0,executionHeight.Value);if(sourceLayout.RowStyles[2].Height!=height&&(!executionHeight.Running||Math.Abs(sourceLayout.RowStyles[2].Height-height)>.3f))sourceLayout.RowStyles[2].Height=height;}finally{executionLayoutDepth--;}}
  void ExecuteSource(string command){
   if(execution==null||closedExecution)return;
   long before=executionRefreshes;
   if(command=="pause"){PauseSourceRuntime();if(before==executionRefreshes)RefreshSourceRuntime();return;}
   if(!execution.Current()){RefreshSourceRuntime();return;}
   if(command=="reset"){execution.Reset();if(before==executionRefreshes)RefreshSourceRuntime(true);return;}
   if(Source==null||Source.Text.Length==0||!runtimeHasOrigins)return;
   var r=execution.Runner();if(command!="file"&&r!=null&&(r.State==RuntimeState.WaitingForInput||r.State==RuntimeState.WaitingForFile))return;
   if(command=="play")execution.Play();else if(command=="step")execution.Step();else if(command=="file")execution.File();
   if(before==executionRefreshes)RefreshSourceRuntime();
  }
  internal void PauseSourceRuntime(){if(execution!=null&&!closedExecution&&execution.Current())execution.Pause();}
  bool CanSubmitSource(){var r=execution==null||!execution.Current()?null:execution.Runner();return r!=null&&r.State==RuntimeState.WaitingForInput&&inputVisit==r.Generation+":"+r.CurrentFrameId+":"+r.CurrentVisit+":"+r.InputVariable;}
  void SubmitSourceInput(){if(!CanSubmitSource()||inputClock.ElapsedMilliseconds-lastInputAccepted<250)return;var r=execution.Runner();long visit=r.CurrentVisit,before=executionRefreshes;execution.Submit(RuntimeInput.Text);if(r.State!=RuntimeState.WaitingForInput||r.CurrentVisit!=visit)lastInputAccepted=inputClock.ElapsedMilliseconds;if(before==executionRefreshes)RefreshSourceRuntime();if(CanSubmitSource())RuntimeInput.Focus();}
  internal void FocusSourceInput(){if(executionInput!=null&&executionInput.Visible&&RuntimeInput.CanFocus){executionArea.ScrollControlIntoView(RuntimeInput);RuntimeInput.Focus();}}
  protected override void Dispose(bool disposing){if(disposing&&!closedExecution){closedExecution=true;if(execution!=null&&execution.Current())execution.Pause();}base.Dispose(disposing);}
  protected override bool ProcessCmdKey(ref Message msg,Keys keys){
   if(execution!=null&&execution.Current()){
    if(keys==Keys.F5){RuntimePlay.Focus();ExecuteSource(execution.Running()?"pause":"play");return true;}
    if(keys==Keys.F11){RuntimeStep.Focus();ExecuteSource("step");return true;}
   }
   return base.ProcessCmdKey(ref msg,keys);
  }
 }
 internal sealed partial class MainForm {
  SourceCodeDialog sourceViewer;long sourceDocumentEpoch;
  internal void AttachSourceRuntime(SourceCodeDialog dialog){
   var expected=Project;long epoch=sourceDocumentEpoch;sourceViewer=dialog;
   dialog.BindExecution(new SourceCodeDialog.Execution{Current=()=>!IsDisposed&&!Disposing&&ReferenceEquals(Project,expected)&&sourceDocumentEpoch==epoch,Runner=()=>Runner,Running=()=>runTimer.Enabled,FileBusy=()=>fileIoBusy,PauseReason=()=>debugger!=null&&ReferenceEquals(debugger.Runner,Runner)?debugger.Message:null,Pending=()=>inputValue.Text,SetPending=value=>inputValue.Text=value,Submit=value=>{inputValue.Text=value;SubmitInput();},Play=StartRun,Step=SingleStep,Pause=PauseRun,Reset=()=>ResetRun(false),File=()=>runtimeFileAction.PerformClick()});
   dialog.Disposed+=delegate{if(ReferenceEquals(sourceViewer,dialog))sourceViewer=null;};
  }
  void RefreshSourceRuntime(){if(sourceViewer!=null&&!sourceViewer.IsDisposed)sourceViewer.RefreshSourceRuntime();}
 }
}
