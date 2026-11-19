using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace Frociart.Tests {
internal static class MenuAimTests {
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 static int checks;static readonly List<Exception> errors=new List<Exception>();
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
 static void Pump(int ms){var clock=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(2);}while(clock.ElapsedMilliseconds<ms);if(errors.Count>0)throw new Exception("UI error",errors[0]);}
 static void Move(ToolStrip menu,Point point){typeof(ToolStrip).GetMethod("OnMouseMove",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{new MouseEventArgs(MouseButtons.None,0,point.X,point.Y,0)});Pump(18);}
 static bool Waiting(ToolStrip menu){object aim=menu.GetType().GetField("aim",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);return(bool)aim.GetType().GetProperty("Waiting",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(aim,null);}
 static void Key(ToolStrip menu,Keys key){typeof(ToolStripDropDown).GetMethod("ProcessDialogKey",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{key});Pump(25);}
 static void Click(ToolStrip menu,Point p){SendMessage(menu.Handle,0x201,new IntPtr(1),new IntPtr((p.Y<<16)|(p.X&65535)));SendMessage(menu.Handle,0x202,IntPtr.Zero,new IntPtr((p.Y<<16)|(p.X&65535)));Pump(40);}
 sealed class Fixture:IDisposable {
  internal Form form;internal FlowContextMenu menu;internal ToolStripMenuItem first,second;internal Point start,cross;internal int clicks,secondEntries;
  internal Fixture(bool left,bool bottom){
   var area=Screen.PrimaryScreen.WorkingArea;form=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(area.Left+15,area.Top+15,450,500)};form.Show();
   menu=new FlowContextMenu();first=new ToolStripMenuItem("Prima sezione");second=new ToolStripMenuItem("Seconda sezione");
   for(int i=0;i<8;i++){var item=new ToolStripMenuItem("Azione "+i);item.Click+=(s,e)=>clicks++;first.DropDownItems.Add(item);second.DropDownItems.Add("Altra azione "+i);}
   second.MouseEnter+=(s,e)=>secondEntries++;menu.Items.Add(first);menu.Items.Add(second);menu.Items.Add(new ToolStripMenuItem("Esegui subito",null,(s,e)=>clicks++));menu.Items.Add("Ultima voce");FlowMenus.Apply(menu);
   menu.Show(form,new Point(left?area.Right-menu.Width-20-form.Left:35,bottom?area.Bottom-menu.Height-20-form.Top:35));Pump(220);
   first.Select();first.ShowDropDown();Pump(220);
   bool actualLeft=first.DropDown.Right<=menu.Left+menu.Width/2;
   Check(actualLeft==left,"fixture popup opens on expected "+(left?"left":"right")+" side");
   start=new Point(first.Bounds.Left+(int)(first.Width*(left?.8:.2)),first.Bounds.Top+first.Height/2);
   Move(menu,start);
   int near=left?first.DropDown.Right-menu.Left:first.DropDown.Left-menu.Left;
   cross=new Point((int)(start.X+(near-start.X)*.65),second.Bounds.Top+8);
   Check(menu.GetItemAt(cross)==second,"diagonal route crosses the other section");
  }
  public void Dispose(){menu.Close();menu.Dispose();form.Close();form.Dispose();Pump(120);}
 }
 static void Route(bool left,bool bottom){using(var f=new Fixture(left,bottom)){
  Move(f.menu,f.cross);
  Check(f.first.Selected&&f.first.DropDown.Visible&&f.secondEntries==0,"diagonal crossing keeps original popup and suppresses accidental row entry");
  Point child=f.first.DropDown.PointToClient(f.menu.PointToScreen(f.cross));child.X=left?f.first.DropDown.Width-35:35;child.Y=Math.Max(15,Math.Min(f.first.DropDown.Height-15,child.Y));
  SendMessage(f.menu.Handle,0x2A3,IntPtr.Zero,IntPtr.Zero);Move(f.first.DropDown,child);Pump(520);
  Check(f.first.DropDown.Visible&&!f.second.DropDown.Visible,"arrival in submenu cancels deferred section switch");
  var action=f.first.DropDown.GetItemAt(child);if(action==null)action=f.first.DropDown.Items[0];var r=action.Bounds;
  Click(f.first.DropDown,new Point(r.Left+r.Width/2,r.Top+r.Height/2));
  Check(f.clicks==1&&!f.menu.Visible,"reached submenu executes the intended command exactly once");
 }}
 [STAThread]static int Main(){try{
  Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(s,e)=>errors.Add(e.Exception);UiMotion.SetEnabled(false);
  Route(false,false);Route(true,false);Route(false,true);Route(true,true);
  using(var f=new Fixture(false,false)){Move(f.menu,new Point(f.start.X,f.cross.Y));Check(!Waiting(f.menu)&&f.secondEntries==1,"vertical movement reaches native row handling immediately");}
  using(var f=new Fixture(false,false)){Move(f.menu,f.cross);Move(f.menu,new Point(f.cross.X-15,f.cross.Y));Check(!Waiting(f.menu)&&f.secondEntries==1,"moving away releases the corridor immediately");}
  using(var f=new Fixture(false,false)){Move(f.menu,f.cross);Check(Waiting(f.menu),"diagonal route starts one bounded wait");Pump(600);Check(!Waiting(f.menu)&&f.secondEntries==1,"stationary pointer resumes native handling without another mouse event");}
  using(var f=new Fixture(false,false)){Move(f.menu,f.cross);Key(f.menu,Keys.Down);Check(f.second.Selected,"keyboard selection bypasses pending mouse protection");}
  using(var f=new Fixture(false,false)){Move(f.menu,f.cross);Check(Waiting(f.menu),"Parent has a pending diagonal crossing");Key(f.first.DropDown,Keys.Down);Check(!Waiting(f.menu),"Keyboard input in a child cancels the parent wait too");}
  using(var f=new Fixture(false,false)){Move(f.menu,f.cross);Click(f.menu,f.cross);Check(f.second.Selected||f.second.DropDown.Visible,"explicit click chooses the crossed section immediately");}
  using(var f=new Fixture(false,false)){Move(f.menu,f.cross);f.menu.Close();Pump(800);Check(!f.menu.Visible&&!f.first.DropDown.Visible,"close cancels pending work without reopening menus");f.menu.Show(f.form,new Point(35,35));Pump(100);Move(f.menu,new Point(f.start.X,f.cross.Y));Check(!Waiting(f.menu),"reopening has no stale corridor");}
  UiMotion.SetEnabled(true);Route(false,false);UiMotion.SetEnabled(false);
  Check(PopupGhost.ActiveCount==0,"all popup ghosts and timers settle after disposal");Console.WriteLine("MENU_AIM_FINISHED checks="+checks+" errors=0");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}finally{UiMotion.SetEnabled(true);}}
}
}
