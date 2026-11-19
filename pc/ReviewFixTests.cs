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
internal static class ReviewFixTests {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks, failures;
    static string output;
    static readonly List<Exception> errors=new List<Exception>();
    delegate bool EnumWindow(IntPtr handle,IntPtr data);
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint thread,EnumWindow callback,IntPtr data);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,Flags).GetValue(o);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethods(Flags).First(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);}
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static void Pump(int ms=35){var watch=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(2);}while(watch.ElapsedMilliseconds<ms);if(errors.Count>0)throw new Exception("Unhandled UI error",errors[0]);}
    static void Wait(Func<bool> done){var watch=Stopwatch.StartNew();while(!done()){Pump(5);if(watch.ElapsedMilliseconds>12000)throw new Exception("Timed out");}Pump();}
    static void Case(string name,Action action){try{errors.Clear();action();}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
    static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-2600,-2200);f.Show();Pump();}
    static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var x in All(child))yield return x;}
    static void Capture(Form f,string name){Pump();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(Path.Combine(output,name+".png"));}}
    static TutorDialog Learner(string name, bool solved){var store=new TutorStore(Path.Combine(output,name+"-"+Guid.NewGuid().ToString("N")));var lesson=Tutor.Lesson("b02");var s=store.Load("b02",lesson.Seed());s.Document=solved?lesson.Solution():lesson.Seed();store.Save(s);var f=new TutorDialog(store,new LearningStore(Path.Combine(output,name+"-legacy.json")));Show(f);f.SelectLesson("b02");Call(f,"Primary");Pump();return f;}
    static FlowContextMenu OpenCommands(TutorDialog f){Call(f,"MoreCommands");Pump();FlowContextMenu result=null;EnumThreadWindows(GetCurrentThreadId(),delegate(IntPtr h,IntPtr data){var c=Control.FromHandle(h) as FlowContextMenu;if(c!=null&&c.Visible)result=c;return true;},IntPtr.Zero);if(result==null)throw new Exception("Context menu not found on test thread");return result;}
    static void NativeClick(ToolStripDropDown menu,ToolStripItem item){var r=item.Bounds;int x=r.Left+r.Width/2,y=r.Top+r.Height/2;var point=new IntPtr((y<<16)|(x&65535));SendMessage(menu.Handle,0x200,IntPtr.Zero,point);SendMessage(menu.Handle,0x201,new IntPtr(1),point);SendMessage(menu.Handle,0x202,IntPtr.Zero,point);Pump(120);}

    static void Handoff(bool draft,bool keyboard){
        using(var f=Learner("handoff-"+draft+"-"+keyboard,true)){
            f.Canvas.SelectNode("messaggio");Pump();
            string expected=draft?"\"Bozza trasferita\"":Tutor.Find(f.Current.Document,"messaggio").Text;
            if(draft)Field<TextBox>(f,"expression").Text=expected;
            var menu=OpenCommands(f);var item=menu.Items.OfType<ToolStripMenuItem>().First(i=>i.Text.StartsWith("Apri questo foglio"));
            if(keyboard){item.Select();typeof(ToolStripDropDown).GetMethod("ProcessDialogKey",Flags).Invoke(menu,new object[]{Keys.Enter});Pump(120);}
            else NativeClick(menu,item);
            Check(f.EditorDocument!=null,"menu handoff succeeds "+(keyboard?"keyboard":"mouse")+" draft="+draft);
            Check(Tutor.Find(f.EditorDocument,"messaggio").Text==expected,"handoff retains effective text draft="+draft);
            Check(!Object.ReferenceEquals(f.EditorDocument,f.Current.Document),"handoff is an independent copy");
            Check(menu.IsDisposed,"transient menu released after native event");
        }
    }
    static void Runtime(){
        using(var f=new MainForm(null,true)){
            Show(f);f.EnterEditor();f.SetDocument(Examples.Parity(),null,false);
            f.SingleStep();f.SingleStep();Check(f.Runner.State==RuntimeState.WaitingForInput,"runtime reaches input");
            Check(Field<Label>(f,"status").Text.Contains("In attesa"),"footer identifies input wait");
            Field<TextBox>(f,"inputValue").Text="8";f.SubmitInput();
            for(int i=0;i<8&&f.Runner.State!=RuntimeState.Completed;i++)f.SingleStep();
            Check(f.Runner.Output.Single()=="Pari","completed runtime produces Pari");
            Check(Field<Label>(f,"status").Text.StartsWith("Esecuzione finita"),"footer agrees with completion");
            Call(f,"PauseRun");Check(Field<Label>(f,"status").Text.StartsWith("Esecuzione finita"),"opening another mode cannot relabel completion as paused");
            Capture(f,"runtime-completed");
            Call(f,"ResetRun",false);f.SingleStep();f.SingleStep();Field<TextBox>(f,"inputValue").Text="abc";f.SubmitInput();f.SingleStep();
            Check(f.Runner.State==RuntimeState.Error,"bad numeric operand produces runtime error");
            Check(Field<Label>(f,"status").Text.StartsWith("Esecuzione interrotta"),"footer agrees with runtime error");
            Call(f,"ResetRun",false);f.StartRun();Call(f,"RunTick");Call(f,"PauseRun");Check(Field<Label>(f,"status").Text.Contains("pausa"),"pause remains explicit during a live run");
            f.SetDocument(Examples.Parity(),null,false);
        }
    }
    static void LessonStates(){
        using(var f=Learner("lesson-state",true)){
            Call(f,"Primary");Wait(()=>f.Feedback!=null);Check(f.Feedback.Pass,"first lesson step is checked");
            Call(f,"Primary");Check(f.Current.Step==1,"continue advances to second step");
            Check(!Field<Label>(f,"notice").Text.Contains("Corretto")&&Field<Label>(f,"notice").Text.Contains("da verificare"),"new lesson step has unverified feedback");Capture(f,"lesson-next-step");
            Call(f,"Primary");Wait(()=>f.Feedback!=null);Check(f.Feedback.Pass,"second step is checked");
            Call(f,"Primary");Check(f.Current.Step==2&&!f.Current.Completed,"final test is pending");
            Check(Field<Label>(f,"notice").Text.StartsWith("Prova finale da verificare"),"final test does not inherit success");Capture(f,"lesson-final-pending");
        }
    }
    static void Header(){
        using(var f=Learner("header",true)){
            foreach(var size in new[]{new Size(1050,720),new Size(1320,860)}){
                f.Size=size;Pump();var label=Field<Label>(f,"desktopHeading");int width=TextRenderer.MeasureText(label.Text,label.Font,new Size(Int32.MaxValue,Int32.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix).Width;
                Check(label.Width>=width,"full heading fits at "+size.Width);
                var nav=Field<FlowLayoutPanel>(f,"navigation");Check(label.Right<nav.Left,"heading and navigation remain separate");
                Field<TabControl>(f,"inspectorTabs").SelectedIndex=1;Pump();Check(label.Width>=width,"full heading fits in Prova at "+size.Width);
                Field<TabControl>(f,"inspectorTabs").SelectedIndex=0;Pump();Capture(f,"header-"+size.Width);
            }
        }
    }
    static bool ClearOfRoutes(FlowCanvas canvas,FlowNode node,List<PointF[]> routes){var rect=FlowCanvas.GetNodeBounds(node);rect.Inflate(12,12);foreach(var route in routes)for(int i=1;i<route.Length;i++){var segment=RectangleF.FromLTRB(Math.Min(route[i-1].X,route[i].X)-1,Math.Min(route[i-1].Y,route[i].Y)-1,Math.Max(route[i-1].X,route[i].X)+1,Math.Max(route[i-1].Y,route[i].Y)+1);if(rect.IntersectsWith(segment))return false;}return true;}
    static void Palette(){
        using(var f=new MainForm(null,true)){
            Show(f);f.EnterEditor();f.Size=new Size(1120,720);
            foreach(float zoom in new[]{.5f,1.15f,1.8f}){
                f.SetDocument(DiagramIO.CreateBlank(),null,false);f.Canvas.FitToView();f.Canvas.Zoom=zoom;Pump();
                var old=f.Document.Nodes.ToArray();var route=f.Canvas.GetExportRoute(old[0],old[1],"next").ToArray();
                var button=All(f).OfType<BlockButton>().First(b=>b.Text=="Output");button.PerformClick();Pump();var node=f.Document.Nodes.Last();
                Check(f.Document.Nodes.Count==3&&f.Document.Edges.Count==1,"palette keeps explicit loose-block semantics at zoom "+zoom);
                Check(ClearOfRoutes(f.Canvas,node,new List<PointF[]>{route}),"palette block clears existing route at zoom "+zoom);
                Check(old.All(n=>!FlowCanvas.GetNodeBounds(n).IntersectsWith(FlowCanvas.GetNodeBounds(node))),"palette block does not cover another block");
                Check(Field<Label>(f,"status").Text.Contains("scollegata"),"palette result says the block is disconnected");
                Capture(f,"loose-block-"+zoom.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            Check(All(f).OfType<Label>().Any(l=>l.Text=="Clic: casella scollegata"),"palette explains click before use");f.SetDocument(DiagramIO.CreateBlank(),null,false);
        }
    }
    [STAThread]static int Main(){
        output=Path.Combine(Environment.GetEnvironmentVariable("REVIEW_OUTPUT"),"review-fixes");Directory.CreateDirectory(output);
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(s,e)=>errors.Add(e.Exception);
        UiMotion.SetEnabled(false);TutorFeedbackSignals.SetEnabled(false);Ui.SetDarkMode(true,false);
        Case("runtime status",Runtime);Case("lesson feedback",LessonStates);Case("header",Header);Case("palette placement",Palette);
        foreach(bool draft in new[]{false,true})foreach(bool keyboard in new[]{false,true}){bool d=draft,k=keyboard;Case("handoff "+d+" "+k,()=>Handoff(d,k));}
        Console.WriteLine("RESULT "+checks+" assertions, "+failures+" failed cases");return failures==0?0:1;
    }
}
}
