using System;
using System.Windows.Forms;

namespace Frociart {
 internal sealed partial class MainForm {
  private RuntimeInputChatDialog runtimeInputChat;
  private Interpreter runtimeChatRunner;
  private Diagram runtimeChatProject;
  private long runtimeChatGeneration=-1;
  private string runtimeChatSuppressed,runtimeChatQueuedKey;
  private int runtimeChatTicket;
  private bool runtimeChatClosing;

  private string RuntimeInputRequestKey(){
   var r=Runner;if(r==null||r.State!=RuntimeState.WaitingForInput)return null;
   return r.Generation+":"+r.CurrentFrameId+":"+r.CurrentVisit+":"+r.CurrentFunctionId+":"+r.CurrentNodeId+":"+r.Steps+":"+r.InputVariable;
  }
  private bool RuntimeChatIsCurrent(Interpreter runner,Diagram project,long generation){
   return !runtimeChatClosing&&!IsDisposed&&!Disposing&&ReferenceEquals(Runner,runner)&&ReferenceEquals(Project,project)&&runner!=null&&runner.Generation==generation;
  }
  private bool RuntimeChatCanAutoOpen(){
   return !runtimeChatClosing&&!IsDisposed&&!Disposing&&IsHandleCreated&&Visible&&Enabled&&WindowState!=FormWindowState.Minimized&&
    (homePanel==null||!homePanel.Visible)&&(Form.ActiveForm==this||ContainsFocus)&&
    !(sourceViewer!=null&&sourceViewer.Visible&&sourceViewer.ContainsFocus)&&
    !(variableInspector!=null&&variableInspector.Visible&&variableInspector.ContainsFocus)&&
    !(runtimeConsole!=null&&runtimeConsole.Visible&&runtimeConsole.ContainsFocus);
  }
  private void ResetRuntimeInputChat(){
   runtimeChatTicket++;runtimeChatQueuedKey=runtimeChatSuppressed=null;
   var window=runtimeInputChat;runtimeInputChat=null;runtimeChatRunner=null;runtimeChatProject=null;runtimeChatGeneration=-1;
   if(window!=null&&!window.IsDisposed)window.ClosePermanently();
  }
  private void SyncRuntimeInputChat(){
   if(runtimeChatClosing||IsDisposed||Disposing)return;
   if(Runner==null||Project==null||(homePanel!=null&&homePanel.Visible)){ResetRuntimeInputChat();return;}
   if(!ReferenceEquals(runtimeChatRunner,Runner)||!ReferenceEquals(runtimeChatProject,Project)||runtimeChatGeneration!=Runner.Generation){
    ResetRuntimeInputChat();runtimeChatRunner=Runner;runtimeChatProject=Project;runtimeChatGeneration=Runner.Generation;
   }
   if(runtimeInputChat!=null&&!runtimeInputChat.IsDisposed)runtimeInputChat.RefreshState();
   string key=RuntimeInputRequestKey();
   if(key==null){runtimeChatTicket++;runtimeChatQueuedKey=null;return;}
   if(key==runtimeChatSuppressed||runtimeInputChat!=null&&!runtimeInputChat.IsDisposed&&runtimeInputChat.Visible||!RuntimeChatCanAutoOpen()||runtimeChatQueuedKey==key)return;
   var runner=Runner;var project=Project;long generation=runner.Generation;int ticket=++runtimeChatTicket;runtimeChatQueuedKey=key;
   // RefreshRuntime has finished updating every existing input host before this runs.
   BeginInvoke((Action)delegate{
    if(ticket!=runtimeChatTicket)return;runtimeChatQueuedKey=null;
    if(RuntimeChatIsCurrent(runner,project,generation)&&RuntimeInputRequestKey()==key&&runtimeChatSuppressed!=key&&RuntimeChatCanAutoOpen())ShowRuntimeInputChat(false);
   });
  }
  private void SyncRuntimeInputChatPendingText(){
   if(runtimeInputChat!=null&&!runtimeInputChat.IsDisposed&&!runtimeChatClosing)runtimeInputChat.RefreshPending();
  }
  private void OpenRuntimeInputChat(){ShowRuntimeInputChat(true);}
  private void FocusRuntimeInputResponse(){
   if(sourceViewer!=null&&sourceViewer.Visible&&sourceViewer.ContainsFocus){sourceViewer.RefreshSourceRuntime();sourceViewer.FocusSourceInput();return;}
   if(variableInspector!=null&&variableInspector.Visible&&variableInspector.ContainsFocus){variableInspector.RefreshState();variableInspector.FocusPendingInput();return;}
   if(runtimeConsole!=null&&runtimeConsole.Visible&&runtimeConsole.ContainsFocus){runtimeConsole.FocusPendingInput();return;}
   if(runtimeInputChat!=null&&!runtimeInputChat.IsDisposed&&runtimeInputChat.Visible&&runtimeInputChat.ContainsFocus){runtimeInputChat.FocusPendingInput();return;}
   if(RuntimeChatCanAutoOpen())OpenRuntimeInputChat();
  }
  private void ShowRuntimeInputChat(bool explicitOpen){
   if(runtimeChatClosing||IsDisposed||Disposing||Runner==null||!Enabled||homePanel!=null&&homePanel.Visible)return;
   if(!explicitOpen&&!RuntimeChatCanAutoOpen())return;
   var runner=Runner;var project=Project;long generation=runner.Generation;
   if(!ReferenceEquals(runtimeChatRunner,runner)||!ReferenceEquals(runtimeChatProject,project)||runtimeChatGeneration!=generation){ResetRuntimeInputChat();runtimeChatRunner=runner;runtimeChatProject=project;runtimeChatGeneration=generation;}
   runtimeChatTicket++;runtimeChatQueuedKey=null;if(explicitOpen)runtimeChatSuppressed=null;
   if(runtimeInputChat==null||runtimeInputChat.IsDisposed){
    var window=new RuntimeInputChatDialog(()=>RuntimeChatIsCurrent(runner,project,generation)?runner:null,
     ()=>runTimer.Enabled,RuntimeInputRequestKey,()=>inputValue.Text,v=>{if(RuntimeChatIsCurrent(runner,project,generation))inputValue.Text=v;},
     delegate{if(RuntimeChatIsCurrent(runner,project,generation))SubmitInput();},
     delegate{if(RuntimeChatIsCurrent(runner,project,generation))SingleStep();},
     delegate{if(RuntimeChatIsCurrent(runner,project,generation)){if(runTimer.Enabled)PauseRun();else StartRun();}},
     delegate{if(RuntimeChatIsCurrent(runner,project,generation)){ResetRun(false);StartRun();}},
     delegate{if(RuntimeChatIsCurrent(runner,project,generation))runtimeFileAction.PerformClick();},
     delegate{if(RuntimeChatIsCurrent(runner,project,generation)){runtimeChatSuppressed=RuntimeInputRequestKey();runtimeChatTicket++;runtimeChatQueuedKey=null;}},
     key=>{if(!RuntimeChatIsCurrent(runner,project,generation))return false;if(key==Keys.F5){if(runTimer.Enabled)PauseRun();else if(runner.State!=RuntimeState.WaitingForInput)StartRun();return true;}if(key==Keys.F11||key==Keys.F10||key==(Keys.Shift|Keys.F11)){if(runner.State!=RuntimeState.WaitingForInput&&runner.State!=RuntimeState.WaitingForFile&&runner.State!=RuntimeState.Completed&&runner.State!=RuntimeState.Error)DebugStep(key==Keys.F10?FlowDebugger.Command.Over:key==Keys.F11?FlowDebugger.Command.Into:FlowDebugger.Command.Out);return true;}return false;});
    runtimeInputChat=window;window.FormClosed+=delegate{if(ReferenceEquals(runtimeInputChat,window))runtimeInputChat=null;};
   }
   bool focus=explicitOpen||RuntimeChatCanAutoOpen();runtimeInputChat.RefreshState();
   if(!runtimeInputChat.Visible){runtimeInputChat.Show(this);runtimeInputChat.CenterOnOwner();}
   if(focus){runtimeInputChat.Activate();runtimeInputChat.FocusPendingInput();}
  }
 }
}
