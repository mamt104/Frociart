using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace Frociart.Tests {
 internal static class CanvasExperienceTests {
    static int checks;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static void Pump(int ms=25){var w=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(2);}while(w.ElapsedMilliseconds<ms);}
    static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,flags).GetValue(owner);}
    static void Call(object owner,string name){owner.GetType().GetMethod(name,flags,null,Type.EmptyTypes,null).Invoke(owner,null);Pump();}
    static void Mouse(FlowCanvas canvas,string method,Point p,MouseButtons button){typeof(FlowCanvas).GetMethod(method,flags).Invoke(canvas,new object[]{new MouseEventArgs(button,1,p.X,p.Y,0)});Pump(5);}
    static Point Handle(FlowCanvas canvas,FlowEdge edge){var p=canvas.GetExportRoute(canvas.Diagram.Nodes.First(x=>x.Id==edge.From),canvas.Diagram.Nodes.First(x=>x.Id==edge.To),edge.Branch);PointF end=p[p.Count-1],before=p[p.Count-2];double length=Math.Sqrt(Math.Pow(end.X-before.X,2)+Math.Pow(end.Y-before.Y,2));float offset=Math.Min(12/Math.Max(.4f,canvas.Zoom),(float)length*.4f);return Point.Round(canvas.WorldToClient(new PointF(end.X+(before.X-end.X)*offset/(float)length,end.Y+(before.Y-end.Y)*offset/(float)length)));}
    static Point Center(FlowCanvas canvas,FlowNode node){RectangleF b=FlowCanvas.GetNodeBounds(node);return Point.Round(canvas.WorldToClient(new PointF(b.Left+b.Width/2,b.Top+b.Height/2)));}
    static void Capture(Form form,string path){Pump(240);using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(path);}}
    static Diagram Age(){var d=new Diagram{Title="Prova dell’età"};d.Nodes.AddRange(new[]{new FlowNode{Id="s",Kind=NodeKind.Start,Text="Inizio",X=150,Y=0},new FlowNode{Id="i",Kind=NodeKind.Input,Text="eta",Prompt="Quanti anni hai?",X=120,Y=130},new FlowNode{Id="q",Kind=NodeKind.Decision,Text="eta >= 18",TrueLabel="Sì",FalseLabel="No",TrueColor="#35A96C",FalseColor="#DD5C79",X=120,Y=290},new FlowNode{Id="yes",Kind=NodeKind.Output,Text="\"Maggiorenne\"",X=-100,Y=490},new FlowNode{Id="no",Kind=NodeKind.Output,Text="\"Minorenne\"",X=400,Y=490},new FlowNode{Id="e",Kind=NodeKind.End,Text="Fine",X=150,Y=660}});d.Edges.AddRange(new[]{new FlowEdge{Id="a",From="s",To="i",Branch="next"},new FlowEdge{Id="b",From="i",To="q",Branch="next"},new FlowEdge{Id="yes-edge",From="q",To="yes",Branch="true"},new FlowEdge{Id="no-edge",From="q",To="no",Branch="false"},new FlowEdge{Id="c",From="yes",To="e",Branch="next"},new FlowEdge{Id="d",From="no",To="e",Branch="next"}});return d;}
    [STAThread]static int Main(){try{
        Application.EnableVisualStyles();Ui.SetDarkMode(false,false);UiMotion.SetEnabled(true);string directory=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"canvas-experience");Directory.CreateDirectory(directory);
        var points=new[]{new PointF(0,0),new PointF(100,0),new PointF(100,100)};
        Check(FlowCanvas.Prefix(points,.25f).Last()==new PointF(50,0),"fill travels by real path length in the first segment");
        Check(FlowCanvas.Prefix(points,.75f).Last()==new PointF(100,50),"fill continues through a corner without changing speed");
        Check(FlowCanvas.Prefix(points,1).Last()==points.Last(),"fill reaches the exact arrow destination");
        using(var form=new MainForm(null,true)){
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2400,-2000);form.Size=new Size(1180,820);form.Show();Pump(200);form.EnterEditor();form.SetDocument(Age(),null,false);Pump(250);form.Canvas.FitToView();var canvas=form.Canvas;var edge=form.Document.Edges.First(x=>x.Id=="yes-edge");canvas.SelectEdge(edge.Id);Pump();
            Point handle=Handle(canvas,edge),target=Center(canvas,form.Document.Nodes.First(x=>x.Id=="no"));Mouse(canvas,"OnMouseDown",handle,MouseButtons.Left);Mouse(canvas,"OnMouseMove",target,MouseButtons.Left);
            Check(edge.To=="yes","arrow preview does not modify the saved connection");Mouse(canvas,"OnMouseUp",target,MouseButtons.Left);Check(edge.To=="no"&&edge.From=="q"&&edge.Branch=="true"&&edge.Id=="yes-edge","drag changes only the decision arrow destination");
            var q=form.Document.Nodes.First(x=>x.Id=="q");Check(q.TrueLabel=="Sì"&&q.TrueColor=="#35A96C"&&form.Document.Edges.Count(x=>x.From=="q"&&x.Branch=="true")==1,"redrag keeps branch label, color and unique exit");
            Call(form,"Undo");Check(form.Document.Edges.First(x=>x.Id==edge.Id).To=="yes","Undo restores the previous arrow destination");Call(form,"Redo");Check(form.Document.Edges.First(x=>x.Id==edge.Id).To=="no","Redo restores the new arrow destination");
            edge=form.Document.Edges.First(x=>x.Id=="yes-edge");canvas.SelectEdge(edge.Id);handle=Handle(canvas,edge);Mouse(canvas,"OnMouseDown",handle,MouseButtons.Left);Mouse(canvas,"OnMouseMove",Center(canvas,form.Document.Nodes.First(x=>x.Id=="s")),MouseButtons.Left);Mouse(canvas,"OnMouseUp",Center(canvas,form.Document.Nodes.First(x=>x.Id=="s")),MouseButtons.Left);Check(edge.To=="no","dropping on Start keeps the previous destination");
            canvas.SelectEdge(edge.Id);handle=Handle(canvas,edge);Mouse(canvas,"OnMouseDown",handle,MouseButtons.Left);Mouse(canvas,"OnMouseMove",Center(canvas,form.Document.Nodes.First(x=>x.Id=="yes")),MouseButtons.Left);typeof(FlowCanvas).GetMethod("OnKeyDown",flags).Invoke(canvas,new object[]{new KeyEventArgs(Keys.Escape)});Check(edge.To=="no"&&!canvas.IsPointerGestureActive,"Escape cancels arrow redrag without a graph mutation");
            Check(DiagramIO.Clone(form.Document).Edges.First(x=>x.Id==edge.Id).To=="no","redrag remains serializable");
            form.SetDocument(Age(),null,false);canvas=form.Canvas;canvas.FlowAnimationDuration=470;canvas.CurrentEdgeId="yes-edge";float early=canvas.FlowProgress;Pump(120);float middle=canvas.FlowProgress;
            Check(!UiMotion.Enabled||early<middle&&middle<1,"blue fill has real intermediate progress");
            using(var frame=new Bitmap(canvas.Width,canvas.Height)){canvas.DrawToBitmap(frame,new Rectangle(Point.Empty,canvas.Size));frame.Save(Path.Combine(directory,"01-blue-fill.png"));}
            Pump(600);Check(canvas.FlowProgress==1&&!canvas.HasFlowAnimation,"blue fill finishes and releases its timer");
            Check(canvas.CurrentEdgeId=="yes-edge","only the actually selected execution edge is highlighted");
            UiMotion.SetEnabled(false);canvas.CurrentEdgeId="no-edge";Check(canvas.FlowProgress==1&&!canvas.HasFlowAnimation,"reduced motion paints the final flow state immediately");UiMotion.SetEnabled(true);
            form.SetDocument(Age(),null,false);
        }
        using(var form=new MainForm(null,true)){
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2400,-2000);form.Show();Pump(200);form.EnterEditor();form.SetDocument(Age(),null,false);form.SingleStep();form.SingleStep();Pump();
            Check(form.Runner.State==RuntimeState.WaitingForInput,"native run reaches the age question");Capture(form,Path.Combine(directory,"02-runtime-input.png"));Console.WriteLine("INPUT_VIS input="+Field<Panel>(form,"inputPanel").Visible+" pane="+Field<Panel>(form,"runtimePanel").Visible+" tab="+Field<Panel>(form,"runTab").Visible+" ready="+Field<bool>(form,"runtimeDeckReady")+" selected="+Field<SidebarTabs>(form,"tabs").SelectedIndex);Check(Field<Panel>(form,"inputPanel").Visible,"new runtime pane shows the actual input question");
            Field<TextBox>(form,"inputValue").Text="18";form.SubmitInput();for(int n=0;n<8&&form.Runner.State!=RuntimeState.Completed;n++)form.SingleStep();Pump(260);
            Check(form.Runner.State==RuntimeState.Completed&&form.Runner.Output.SequenceEqual(new[]{"Maggiorenne"}),"new runtime pane executes the real age boundary");
            var result=Field<TextBox>(form,"output");var details=Field<FlowButton>(form,"runtimeDetailsToggle");Check(result.Visible&&result.Text.Contains("Maggiorenne")&&result.Parent.Top<details.Top,"result precedes optional details");Check(!Field<ListView>(form,"variables").Visible,"variables do not crowd the primary result");
            Capture(form,Path.Combine(directory,"03-runtime-result.png"));details.PerformClick();Pump(250);Check(Field<ListView>(form,"variables").Visible&&Field<ListView>(form,"variables").Items[0].SubItems[1].Text=="18","details show the real observed variable value");Capture(form,Path.Combine(directory,"04-runtime-details.png"));
            Ui.SetDarkMode(true,false);Pump(250);Capture(form,Path.Combine(directory,"05-runtime-dark.png"));
            details.PerformClick();form.Size=new Size(1080,760);Pump(250);Check(result.Visible&&result.Parent.Width<=Field<Panel>(form,"runtimePanel").ClientSize.Width,"result remains within the smaller sidebar");Capture(form,Path.Combine(directory,"06-runtime-minimum.png"));
            Ui.SetDarkMode(false,false);form.SetDocument(Age(),null,false);form.Close();Pump(250);
        }
        Check(FlowTransitions.Active==0,"page transitions leave no active overlays after closing");
        Console.WriteLine("PASS CANVAS EXPERIENCE "+checks+" checks; actual native controls. Hardware mouse and multiple DPI: NOT_RUN.");return 0;
    }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
