using System;using System.Drawing;using System.Linq;using System.Reflection;using System.Threading;using System.Windows.Forms;
namespace Frociart.Tests {
 sealed class HoldProbeCanvas:FlowCanvas {
  protected override Keys GetInteractionModifiers(){return Keys.None;}
  protected override void OnLostFocus(EventArgs e){}protected override void OnMouseCaptureChanged(EventArgs e){}
  internal void Down(Point p,int clicks=1){OnMouseDown(new MouseEventArgs(MouseButtons.Left,clicks,p.X,p.Y,0));Capture=false;}
  internal new void Move(Point p){OnMouseMove(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
  internal void Up(Point p){OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
  internal void Double(Point p){OnMouseDoubleClick(new MouseEventArgs(MouseButtons.Left,2,p.X,p.Y,0));Capture=false;}
 }
 sealed class HoldProbeHost:Form {protected override bool ShowWithoutActivation{get{return true;}}}
 static class ConditionDetachGestureTests {
  static int checks,failures;static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  static void Check(bool b,string text){checks++;if(!b)failures++;Console.WriteLine((b?"PASS ":"FAIL ")+text);}
  static T Field<T>(object o,string name){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,F);if(f!=null)return (T)f.GetValue(o);}throw new MissingFieldException(name);}
  static void Pump(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();Thread.Sleep(3);}while(DateTime.UtcNow<until);}
  static void Reset(HoldProbeCanvas c){var d=new Diagram();var start=new FlowNode{Id="s",Kind=NodeKind.Start,Y=0};var input=new FlowNode{Id="i",Kind=NodeKind.Process,Y=190,Text="x = 1"};var decision=new FlowNode{Id="d",Kind=NodeKind.Decision,Y=420,Text="x > 0"};var end=new FlowNode{Id="e",Kind=NodeKind.End,Y=800};foreach(var n in new[]{start,input,decision,end}){n.X=500-FlowCanvas.GetNodeBounds(n).Width/2;d.Nodes.Add(n);}d.Edges.AddRange(new[]{new FlowEdge{Id="a",From="s",To="i"},new FlowEdge{Id="b",From="i",To="d"},new FlowEdge{Id="yes",From="d",To="e",Branch="true"},new FlowEdge{Id="no",From="d",To="e",Branch="false"}});c.Diagram=d;c.SelectNode(null);c.SnapToGrid=false;c.FitToView();Pump(100);}
  static Point Center(HoldProbeCanvas c){var n=c.Diagram.Nodes.First(x=>x.Id=="d");var b=FlowCanvas.GetNodeBounds(n);return Point.Round(c.WorldToClient(new PointF(b.X+b.Width/2,b.Y+b.Height/2)));}
  static bool Detached(HoldProbeCanvas c){return !c.Diagram.Edges.Any(e=>e.From=="d"||e.To=="d");}
  [STAThread]static int Main(){try{Application.EnableVisualStyles();UiMotion.SetEnabled(true);Ui.SetDarkMode(true,false);using(var f=new HoldProbeHost{ClientSize=new Size(1000,900),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-3000)})using(var c=new HoldProbeCanvas{Dock=DockStyle.Fill}){f.Controls.Add(c);f.Show();Pump(100);
   Reset(c);var p=Center(c);c.Down(p);Pump(1800);Check(Field<bool>(c,"stickerDetached"),"Converging decision reaches real detach-ready state");var nearby=new Point(p.X+9,p.Y+2);c.Move(nearby);Check(!Field<bool>(c,"insertReady"),"Minor movement after lift cannot silently reinsert into the original bypass");
   int dialogs=0;using(var timer=new System.Windows.Forms.Timer{Interval=15}){timer.Tick+=delegate{var dialog=Application.OpenForms.OfType<BranchContinuationDialog>().FirstOrDefault();if(dialog==null)return;dialogs++;dialog.DialogResult=DialogResult.Cancel;dialog.Close();};timer.Start();c.Up(nearby);}Check(dialogs==0&&Detached(c),"Release near the old position actually detaches without an insertion dialog");
   Reset(c);p=Center(c);c.Down(p);c.Up(p);Pump(60);c.Down(p,2);c.Double(p);int edits=0;EventHandler edited=(s,e)=>edits++;c.EditRequested+=edited;Pump(1800);Check(Field<bool>(c,"stickerDetached"),"Selecting then quickly pressing and holding still reaches detachment");c.Up(p);Check(Detached(c)&&edits==0,"A held second click detaches instead of opening the editor");c.EditRequested-=edited;
   Reset(c);p=Center(c);edits=0;c.EditRequested+=edited;c.Down(p);c.Up(p);Pump(50);c.Down(p,2);c.Double(p);Check(edits==0,"Double click waits for the release before classifying tap versus hold");c.Up(p);Check(edits==1&&!Detached(c),"A quick double click still edits once and preserves the graph");c.EditRequested-=edited;
   Reset(c);p=Center(c);c.Down(p);Pump(1800);c.Move(new Point(p.X+90,p.Y+2));c.Move(new Point(p.X,p.Y+4));Check(Field<bool>(c,"insertReady"),"Leaving and returning to the bypass deliberately re-enables insertion");using(var timer=new System.Windows.Forms.Timer{Interval=15}){timer.Tick+=delegate{var dialog=Application.OpenForms.OfType<BranchContinuationDialog>().FirstOrDefault();if(dialog==null)return;timer.Stop();foreach(Control b in dialog.Controls)if(b is Button&&b.Text=="Sì")((Button)b).PerformClick();};timer.Start();c.Up(new Point(p.X,p.Y+4));}Check(!Detached(c)&&c.Diagram.Edges.Any(e=>e.From=="d"&&e.Branch=="true"),"Intentional return inserts the decision through the chosen branch");c.Diagram=new Diagram();f.Close();}
   Console.WriteLine("CONDITION_DETACH_GESTURE_FINISHED checks="+checks+" errors="+failures);return failures==0?0:1;
  }catch(Exception e){Console.WriteLine(e);return 2;}}
 }
}
