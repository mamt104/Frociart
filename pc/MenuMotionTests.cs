using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace Frociart.Tests {
internal static class MenuMotionTests {
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static int checks;static List<Exception> errors=new List<Exception>();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static void Pump(int ms=35){var watch=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(2);}while(watch.ElapsedMilliseconds<ms);if(errors.Count>0)throw new Exception("Unhandled UI error",errors[0]);}
 static T Field<T>(object owner,string name){return(T)owner.GetType().GetField(name,flags).GetValue(owner);}
 static void Mouse(Control c,int message,int button,Point p){
  // WinForms takes MouseMove.Button from global OS state, not the injected WM
  // wParam. Supply that event explicitly; press/release use actual WM routing.
  if(message==0x200)c.GetType().GetMethod("OnMouseMove",flags).Invoke(c,new object[]{new MouseEventArgs(button==16?MouseButtons.Middle:button==2?MouseButtons.Right:button==1?MouseButtons.Left:MouseButtons.None,0,p.X,p.Y,0)});
  else SendMessage(c.Handle,message,new IntPtr(button),new IntPtr((p.Y<<16)|(p.X&0xffff)));Pump(15);
 }
 static void Key(Control c,Keys key){c.GetType().GetMethod("OnKeyDown",flags).Invoke(c,new object[]{new KeyEventArgs(key)});Pump();}
 static ToolStripMenuItem Item(ToolStripDropDown menu,string prefix){return menu.Items.OfType<ToolStripMenuItem>().First(x=>x.Available&&x.Text.StartsWith(prefix,StringComparison.Ordinal));}
 static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var x in All(child))yield return x;}
 static void Capture(ToolStripDropDown menu,string name){using(var b=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(b,new Rectangle(Point.Empty,menu.Size));b.Save(name);}}
 [STAThread]static int Main(){try{
  Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(s,e)=>errors.Add(e.Exception);
  string directory=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"menu-motion");Directory.CreateDirectory(directory);
  Ui.SetDarkMode(false,false);Check(UiMotion.SetEnabled(true),"motion preference saves");Console.WriteLine("SYSTEM_ANIMATIONS "+UiMotion.Enabled);
  using(var form=new MainForm(null,true)){
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(30,30);form.Size=new Size(1180,820);form.Show();Pump(200);form.EnterEditor();form.SetDocument(Examples.Parity(),null,false);Pump(240);form.Canvas.FitToView();Pump();
   FlowCanvas canvas=form.Canvas;Point p=new Point(50,75);PointF before=canvas.ClientToWorld(p);string document=TutorVariablesFingerprint(form.Document);
   Mouse(canvas,0x207,16,p);Mouse(canvas,0x200,16,new Point(135,125));Mouse(canvas,0x208,0,new Point(135,125));PointF after=canvas.ClientToWorld(p);
   Check(Math.Abs((before.X-after.X)*canvas.Zoom-85)<.1&&Math.Abs((before.Y-after.Y)*canvas.Zoom-50)<.1,"middle-button drag pans the viewport by the exact pointer delta");
   Check(TutorVariablesFingerprint(form.Document)==document,"pan leaves graph and coordinates unchanged");
   Check(Field<ContextMenuStrip>(canvas,"_menu")==null,"middle-button release never opens the menu");
   before=canvas.ClientToWorld(p);Mouse(canvas,0x204,2,p);Mouse(canvas,0x200,2,new Point(130,120));Mouse(canvas,0x205,0,new Point(130,120));after=canvas.ClientToWorld(p);
   Check(Math.Abs(before.X-after.X)<.01&&Math.Abs(before.Y-after.Y)<.01,"right-button drag never pans");
   ContextMenuStrip menu=Field<ContextMenuStrip>(canvas,"_menu");menu.Opened+=(s,e)=>Console.WriteLine("MENU_OPENED");menu.Closing+=(s,e)=>Console.WriteLine("MENU_CLOSING "+e.CloseReason+" active="+(Form.ActiveForm==null?"none":Form.ActiveForm.GetType().Name));Check(menu is FlowContextMenu&&menu.Visible,"right button opens the custom popup");
   Check(menu.Renderer is FlowMenuRenderer&&menu.Region!=null&&!menu.DropShadowEnabled,"popup has custom drawing, rounded silhouette and no stock shadow");
   Check(menu.Items.OfType<FlowMenuHeading>().Any(),"popup has a readable context heading");
   Check(menu.Items.OfType<ToolStripMenuItem>().Where(x=>x.Available).All(x=>x.Height>=40&&x.Width>=280),"menu targets have readable custom spacing");
   double early=menu.Opacity;Pump(380);Check(menu.Opacity==1&&!FlowMenus.Motion(menu).Running,"opening animation reaches final geometry and stops");
   Check(!UiMotion.Enabled||early<1,"enabled animation starts before full opacity");
   Capture(menu,Path.Combine(directory,"01-light-popup.png"));
   var add=Item(menu,"Aggiungi");add.ShowDropDown();Pump(380);Check(add.DropDown is FlowDropDownMenu&&add.DropDown.Renderer is FlowMenuRenderer,"submenu uses the same complete custom skin");
   Capture(add.DropDown,Path.Combine(directory,"02-light-submenu.png"));
   Check(add.DropDown.Width>=300&&add.DropDownItems.OfType<ToolStripMenuItem>().All(x=>x.Bounds.Width>=280),"submenu keeps full custom row width");
   int nodes=form.Document.Nodes.Count;var process=add.DropDownItems.OfType<ToolStripMenuItem>().First(x=>x.Text.IndexOf("Operazione",StringComparison.OrdinalIgnoreCase)>=0);process.PerformClick();menu.Close();Pump(380);
   Check(form.Document.Nodes.Count==nodes+1,"submenu command inserts exactly one real block");
   Check(!menu.Visible&&PopupGhost.ActiveCount==0,"closing animation leaves no ghost windows");
   canvas.SelectNode("check");form.Activate();canvas.Focus();Key(canvas,Keys.Apps);menu=Field<ContextMenuStrip>(canvas,"_menu");Pump(380);Console.WriteLine("KEYBOARD_MENU visible="+menu.Visible+" pointer="+canvas.IsPointerGestureActive+" selected="+canvas.SelectedNodeId+" items="+String.Join("|",menu.Items.Cast<ToolStripItem>().Where(x=>x.Available).Select(x=>x.Text)));Check(menu.Visible&&Item(menu,"Modifica").Available,"keyboard context key opens the selected block menu");
   var original=form.Document.Nodes.Count;Item(menu,"Duplica").PerformClick();menu.Close();Pump(380);Check(form.Document.Nodes.Count==original+1,"duplicate keeps native command semantics");
   Ui.SetDarkMode(true,false);Key(canvas,Keys.Apps);Pump(380);Capture(menu,Path.Combine(directory,"03-dark-decision.png"));Check(menu.BackColor==Ui.Surface&&menu.Renderer is FlowMenuRenderer,"dark palette reaches the custom menu");menu.Close();Pump(160);
   Check(UiMotion.SetEnabled(false),"reduced motion preference saves");Key(canvas,Keys.Apps);Pump(20);Check(menu.Opacity==1&&!FlowMenus.Motion(menu).Running,"reduced motion opens immediately without transition");menu.Close();Pump(20);Check(PopupGhost.ActiveCount==0,"reduced motion does not create a closing animation window");
   FlowButton button=All(form).OfType<FlowButton>().First(x=>x.Visible&&x.Enabled);var enter=typeof(FlowButton).GetMethod("OnMouseEnter",flags);var leave=typeof(FlowButton).GetMethod("OnMouseLeave",flags);enter.Invoke(button,new object[]{EventArgs.Empty});Pump();Check(UiMotion.ActiveTweens==0,"reduced motion makes button response immediate");leave.Invoke(button,new object[]{EventArgs.Empty});
   UiMotion.SetEnabled(true);enter.Invoke(button,new object[]{EventArgs.Empty});Pump(25);int active=UiMotion.ActiveTweens;Pump(380);Check(UiMotion.ActiveTweens==0,"button hover animation stops after settling");Check(!UiMotion.Enabled||active>0,"enabled button animation has real intermediate frames");leave.Invoke(button,new object[]{EventArgs.Empty});Pump(380);
   for(int i=0;i<12;i++){Key(canvas,Keys.Apps);Pump(20);menu.Close();Pump(20);}Pump(380);Check(!menu.IsDisposed&&PopupGhost.ActiveCount==0&&!FlowMenus.Motion(menu).Running,"repeated open/close preserves menu identity and releases animations");
   Ui.SetDarkMode(false,false);form.SetDocument(Examples.Parity(),null,false);form.Close();Pump(200);
  }
  Check(UiMotion.ActiveTweens==0&&PopupGhost.ActiveCount==0,"form disposal leaves no running animation timers");
  Console.WriteLine("PASS MENU MOTION "+checks+" checks; real WinForms windows and WM mouse events. External hardware mouse, multiple DPIs and screen reader: NOT_RUN.");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
 static string TutorVariablesFingerprint(Diagram diagram){using(var stream=new MemoryStream()){new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Diagram)).WriteObject(stream,diagram);return Convert.ToBase64String(stream.ToArray());}}
}
}
