using System;using System.Windows.Forms;
namespace Frociart {
 internal sealed partial class MainForm {
  private FlowVariableWindow variableInspector;
  private readonly FlowWatchExpressions.Session watchExpressions=new FlowWatchExpressions.Session();
  internal void ShowVariableInspector(string name=null){
   if(Canvas.IsPointerGestureActive)return;CommitInspector();EnterEditor();if(Runner==null&&!EnsureRunner())return;
   if(variableInspector!=null&&!variableInspector.IsDisposed){variableInspector.Activate();variableInspector.RefreshState();if(name!=null)variableInspector.SelectName(name);return;}
   PauseRun();var window=new FlowVariableWindow(()=>Runner,()=>runTimer.Enabled,()=>inputValue.Text,v=>inputValue.Text=v,v=>{inputValue.Text=v;SubmitInput();},()=>{if(runTimer.Enabled)PauseRun();else StartRun();},SingleStep,()=>runtimeFileAction.PerformClick(),PauseRun,(function,node)=>{PauseRun();CloseVariableInspector();NavigateToOrigin(function,node);Activate();},watchExpressions);
   variableInspector=window;window.ClipboardWrite=RuntimeClipboardWrite;window.CommandKey=key=>{if(key==Keys.F5){if(runTimer.Enabled)PauseRun();else StartRun();return true;}if(key==Keys.F11){DebugStep(FlowDebugger.Command.Into);return true;}if(key==Keys.F10){DebugStep(FlowDebugger.Command.Over);return true;}if(key==(Keys.Shift|Keys.F11)){DebugStep(FlowDebugger.Command.Out);return true;}if(key==Keys.F7){window.Activate();return true;}return false;};window.FormClosed+=delegate{if(ReferenceEquals(variableInspector,window))variableInspector=null;};window.Show(this);if(name!=null)window.SelectName(name);
  }
  private void RefreshVariableInspector(){if(variableInspector!=null&&!variableInspector.IsDisposed)variableInspector.RefreshState();}
  private void CloseVariableInspector(){var current=variableInspector;variableInspector=null;if(current!=null&&!current.IsDisposed)current.CloseImmediately();}
 }
}
