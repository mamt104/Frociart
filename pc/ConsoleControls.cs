using System;using System.Windows.Forms;
namespace Frociart {
 internal sealed partial class MainForm {
  private FlowConsoleWindow runtimeConsole;
  private FlowButton runtimeConsoleButton;
  private void InstallConsole(){runtimeConsoleButton=Ui.Button("Console · letture e stampe",260,ShowRuntimeConsole);runtimeConsoleButton.Name="RuntimeConsole";runtimeConsoleButton.AccessibleDescription="Apri la cronologia interattiva dei valori inseriti e dei risultati, con messaggi o testo.";runtimePanel.Controls.Add(runtimeConsoleButton);}
  private void ShowRuntimeConsole(){
   if(Canvas.IsPointerGestureActive)return;CommitInspector();EnterEditor();if(Runner==null&&!EnsureRunner())return;
   if(runtimeConsole!=null&&!runtimeConsole.IsDisposed){runtimeConsole.Activate();return;}
   var window=new FlowConsoleWindow(()=>Runner,()=>runTimer.Enabled,()=>inputValue.Text,v=>inputValue.Text=v,v=>{inputValue.Text=v;SubmitInput();},()=>{if(runTimer.Enabled)PauseRun();else StartRun();},SingleStep,()=>runtimeFileAction.PerformClick(),entry=>{
    if(Runner==null||!ReferenceEquals(Runner.ConsoleLog.Find(entry.Sequence),entry))return;PauseRun();CloseRuntimeConsole();NavigateToOrigin(entry.FunctionId,entry.NodeId);Activate();
   });
   runtimeConsole=window;window.ClipboardWrite=RuntimeClipboardWrite;window.FormClosed+=delegate{if(ReferenceEquals(runtimeConsole,window))runtimeConsole=null;};window.Show(this);
  }
  private void CloseRuntimeConsole(){var current=runtimeConsole;runtimeConsole=null;if(current!=null&&!current.IsDisposed)current.CloseImmediately();}
  private void RefreshConsole(){if(runtimeConsole!=null&&!runtimeConsole.IsDisposed)runtimeConsole.RefreshState();}
 }
}
