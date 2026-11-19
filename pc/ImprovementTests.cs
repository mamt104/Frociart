using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace Frociart.Tests {
internal static class ImprovementTests {
    static int checks; static string resultDir;
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,flags).Invoke(o,args);}
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,flags).GetValue(o);}
    static void Pump(){for(int i=0;i<3;i++){Application.DoEvents();System.Threading.Thread.Sleep(15);}}
    static void Mouse(FlowCanvas c,string name,Point p){Call(c,name,new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
    static string Signature(Diagram d){using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(Diagram)).WriteObject(m,d);return Encoding.UTF8.GetString(m.ToArray());}}
    static void Capture(Form f,string file){Pump();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(Path.Combine(resultDir,file));}}
    static FlowNode Start(MainForm m){return m.Document.Nodes.First(n=>n.Kind==NodeKind.Start);}
    static FlowNode End(MainForm m){return m.Document.Nodes.First(n=>n.Kind==NodeKind.End);}
    static Point Port(MainForm m,FlowNode n,string branch){var p=(PointF)typeof(FlowCanvas).GetMethod("OutputPoint",flags).Invoke(m.Canvas,new object[]{n,branch});return Point.Round(m.Canvas.WorldToClient(p));}
    static void Fresh(MainForm m){m.SetDocument(DiagramIO.CreateBlank(),null,false);Pump();m.Canvas.FitToView();Pump();m.Canvas.SelectNode(Start(m).Id);Pump();}
    static ContextMenuStrip OpenPicker(MainForm m,FlowNode from,string branch,bool direct){
        m.Canvas.SelectNode(from.Id);Pump();
        if(direct){Point p=Port(m,from,branch);Mouse(m.Canvas,"OnMouseDown",p);Mouse(m.Canvas,"OnMouseUp",p);}
        else {m.Canvas.BeginConnect(from.Id,branch);Call(m.Canvas,"ShowBlockPicker",new Point(m.Canvas.Width/2,m.Canvas.Height/2));}
        Pump();return Field<ContextMenuStrip>(m.Canvas,"_blockPicker");
    }
    static FlowNode Pick(MainForm m,NodeKind kind){
        var before=new HashSet<string>(m.Document.Nodes.Select(n=>n.Id));
        var picker=Field<ContextMenuStrip>(m.Canvas,"_blockPicker");
        picker.Items.Cast<ToolStripItem>().Single(i=>i.Tag is NodeKind&&(NodeKind)i.Tag==kind).PerformClick();Pump();
        return m.Document.Nodes.FirstOrDefault(n=>!before.Contains(n.Id));
    }
    static bool Answer(Action action,DialogResult answer,string screenshot=null){
        bool seen=false;using(var timer=new Timer{Interval=25}){
            timer.Tick+=delegate {
                var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f is FlowMessageDialog||f is BranchContinuationDialog);
                if(dialog==null)return;timer.Stop();seen=true;
                if(screenshot!=null)Capture(dialog,screenshot);
                if(dialog is BranchContinuationDialog){string text=answer==DialogResult.Yes?"Sì":answer==DialogResult.No?"No":"Annulla inserimento";((Button)Find(dialog,text)).PerformClick();}
                else {dialog.DialogResult=answer;dialog.Close();}
            };timer.Start();action();timer.Stop();
        }return seen;
    }
    static bool HasEdge(MainForm m,string a,string b,string branch){return m.Document.Edges.Any(e=>e.From==a&&e.To==b&&e.Branch==branch);}
    static void Baseline(MainForm m){
        Fresh(m);var start=Start(m);var end=End(m);
        var p=OpenPicker(m,start,"next",true);Console.WriteLine("BASELINE direct_plus_picker="+(p!=null&&p.Visible));
        m.Canvas.CancelConnection();OpenPicker(m,start,"next",false);var added=Pick(m,NodeKind.Process);
        Console.WriteLine("BASELINE keeps_continuation="+HasEdge(m,added.Id,end.Id,"next"));
        var extra=m.Canvas.AddNode(NodeKind.Output);bool asked=Answer(()=>{m.Canvas.BeginConnect(start.Id,"next");Call(m.Canvas,"CompleteConnection",extra);},DialogResult.No);
        Console.WriteLine("BASELINE asks_before_replace="+asked);
    }
    static Control Find(Control c,string text){foreach(Control child in c.Controls){if(child.Text==text)return child;var nested=Find(child,text);if(nested!=null)return nested;}return null;}
    static void Candidate(MainForm m){
        Fresh(m);var start=Start(m);var end=End(m);string before=Signature(m.Document);
        var picker=OpenPicker(m,start,"next",true);Check(picker!=null&&picker.Visible,"tap on + opens picker immediately");
        var process=Pick(m,NodeKind.Process);
        Check(process!=null&&HasEdge(m,start.Id,process.Id,"next")&&HasEdge(m,process.Id,end.Id,"next"),"insert keeps A to C to B");
        Check(!m.Canvas.IsConnecting,"picker completes connection mode");
        Check(!FlowCanvas.GetNodeBounds(process).IntersectsWith(FlowCanvas.GetNodeBounds(end)),"new block does not cover original next block");
        Check(end.Y-FlowCanvas.GetNodeBounds(process).Bottom>=34,"tap insertion leaves room for a direct downward arrow");
        Call(m,"Undo");Check(Signature(m.Document)==before,"one Undo restores entire insertion");
        Call(m,"Redo");Check(m.Document.Nodes.Count==3&&m.Document.Edges.Count==2,"Redo restores insertion and continuation");
        process=m.Document.Nodes.Single(n=>n.Kind==NodeKind.Process);start=Start(m);end=End(m);
        var output=m.Canvas.AddNode(NodeKind.Output);before=Signature(m.Document);
        int undo=Field<System.Collections.ICollection>(m,"undo").Count;
        Check(Answer(()=>{m.Canvas.BeginConnect(start.Id,"next");Call(m.Canvas,"CompleteConnection",output);},DialogResult.No,"confirm-replace.png"),"replace opens real confirmation dialog");
        Check(Signature(m.Document)==before&&Field<System.Collections.ICollection>(m,"undo").Count==undo&&!m.Canvas.IsConnecting,"keep old arrival leaves graph and history unchanged");
        Check(Answer(()=>{m.Canvas.BeginConnect(start.Id,"next");Call(m.Canvas,"CompleteConnection",output);},DialogResult.Yes),"replace can be confirmed");
        Check(HasEdge(m,start.Id,output.Id,"next")&&m.Document.Nodes.Contains(process),"confirmed replacement keeps all blocks");
        Call(m,"Undo");Check(Signature(m.Document)==before,"Undo restores previous destination");
        start=Start(m);end=End(m);
        m.Canvas.BeginConnect(start.Id,"next");Call(m.Canvas,"CompleteConnection",m.Document.Nodes.Single(n=>n.Id==m.Document.Edges.Single(e=>e.From==start.Id).To));
        Check(Field<System.Collections.ICollection>(m,"undo").Count==undo,"same destination creates no history entry");

        Fresh(m);start=Start(m);end=End(m);before=Signature(m.Document);
        Point from=Port(m,start,"next"),drop=Point.Round(m.Canvas.WorldToClient(new PointF(start.X+340,start.Y+180)));
        Mouse(m.Canvas,"OnMouseDown",from);Mouse(m.Canvas,"OnMouseMove",drop);Mouse(m.Canvas,"OnMouseUp",drop);Pump();
        Check(Field<ContextMenuStrip>(m.Canvas,"_blockPicker").Visible,"drag + still opens picker at release");
        var dragged=Pick(m,NodeKind.Output);
        Check(dragged!=null&&HasEdge(m,start.Id,dragged.Id,"next")&&HasEdge(m,dragged.Id,end.Id,"next"),"drag insertion also preserves continuation");
        Call(m,"Undo");Check(Signature(m.Document)==before,"drag insertion is one Undo");
        m.SetDocument(Examples.Parity(),null,false);Pump();
        var branchSource=m.Document.Nodes.Single(n=>n.Kind==NodeKind.Decision);
        branchSource.TrueLabel="Accettato";branchSource.TrueColor="#5B8DEF";
        string previousArrival=m.Document.Edges.Single(e=>e.From==branchSource.Id&&e.Branch=="true").To;
        OpenPicker(m,branchSource,"true",true);var branchBlock=Pick(m,NodeKind.Process);
        Check(HasEdge(m,branchSource.Id,branchBlock.Id,"true")&&HasEdge(m,branchBlock.Id,previousArrival,"next"),"custom decision branch preserves its original destination");
        Check(branchSource.TrueLabel=="Accettato"&&branchSource.TrueColor=="#5B8DEF","branch label and colour survive insertion");

        foreach(var decisionAnswer in new[]{DialogResult.Yes,DialogResult.No,DialogResult.Cancel}){
            Fresh(m);start=Start(m);end=End(m);before=Signature(m.Document);OpenPicker(m,start,"next",true);
            FlowNode decision=null;Check(Answer(()=>{decision=Pick(m,NodeKind.Decision);},decisionAnswer,decisionAnswer==DialogResult.Yes?"confirm-branch.png":null),"decision insertion asks continuation");
            if(decisionAnswer==DialogResult.Cancel){Check(Signature(m.Document)==before,"cancel decision preserves full graph");continue;}
            string branch=decisionAnswer==DialogResult.Yes?"true":"false";
            Check(decision!=null&&HasEdge(m,start.Id,decision.Id,"next")&&HasEdge(m,decision.Id,end.Id,branch),"selected logical branch continues to original arrival");
            Check(!m.Document.Edges.Any(e=>e.From==decision.Id&&e.Branch!=branch),"other decision branch stays free");
            Call(m,"Undo");Check(Signature(m.Document)==before,"decision insertion is one undo");
        }
        foreach(var answer in new[]{DialogResult.No,DialogResult.Yes}){
            Fresh(m);start=Start(m);end=End(m);before=Signature(m.Document);OpenPicker(m,start,"next",true);FlowNode newEnd=null;
            Check(Answer(()=>{newEnd=Pick(m,NodeKind.End);},answer),"inserting End asks before interrupting flow");
            if(answer==DialogResult.No)Check(Signature(m.Document)==before,"declining End keeps path unchanged");
            else {Check(newEnd!=null&&m.Document.Nodes.Contains(end)&&HasEdge(m,start.Id,newEnd.Id,"next"),"confirmed End retains downstream blocks");Call(m,"Undo");Check(Signature(m.Document)==before,"Undo restores interrupted flow");}
        }
        Fresh(m);start=Start(m);end=End(m);
        for(int i=m.Document.Edges.Count;i<DiagramIO.MaxEdges;i++)m.Document.Edges.Add(new FlowEdge{From=end.Id,To=start.Id});
        before=Signature(m.Document);OpenPicker(m,start,"next",true);Check(Pick(m,NodeKind.Process)==null&&Signature(m.Document)==before,"edge capacity rejects entire insertion atomically");

        m.SetDocument(Examples.Parity(),null,false);Pump();
        m.SingleStep();m.SingleStep();Check(m.Runner.State==RuntimeState.WaitingForInput,"manual execution waits for inline input");
        Field<TextBox>(m,"inputValue").Text="7";m.SubmitInput();
        Check(!Field<Timer>(m,"runTimer").Enabled,"manual input does not switch to automatic execution");
        m.SingleStep();m.SingleStep();m.SingleStep();Check(m.Runner.State==RuntimeState.Completed&&m.Runner.Output.Single()=="Dispari","odd branch executes and displays output");
        int steps=m.Runner.Steps;m.SingleStep();Check(m.Runner.Steps==steps,"step after End preserves result");
        foreach(bool dark in new[]{false,true}){
            Ui.SetDarkMode(dark,false);m.Size=new Size(1120,720);Pump();Capture(m,dark?"windows-small-dark.png":"windows-small-light.png");
            var filebar=Find(m,"Nuovo").Parent;Check(filebar.Controls.Cast<Control>().Select(c=>c.Text).SequenceEqual(new[]{"Home","Nuovo","Apri","Salva","Altro  ···","Esempi","Guida"}),"original file toolbar preserved beside the new Home action in "+(dark?"dark":"light"));
        }
        m.Size=new Size(1420,900);Ui.SetDarkMode(false,false);Fresh(m);OpenPicker(m,Start(m),"next",true);Pick(m,NodeKind.Process);Capture(m,"windows-inserted.png");
        string file=Path.Combine(resultDir,"inserimento.frociart");m.GetType().GetField("currentPath",flags).SetValue(m,file);
        Check((bool)Call(m,"SaveDocument",false),"native Save command succeeds");
        string saved=Signature(m.Document);Check(Signature(DiagramIO.Load(file))==saved,"saved project reopens with all links");
        Check((bool)Call(m,"SaveDocument",false)&&Signature(DiagramIO.Load(file))==saved,"atomic overwrite preserves complete project");
        Check(m.LoadPath(file)&&Signature(m.Document)==saved,"MainForm reopens native project without changes");
        before=Signature(m.Document);
        OpenPicker(m,Start(m),"next",true);m.Canvas.CancelConnection();Check(Signature(m.Document)==before&&!m.Canvas.IsConnecting,"closing picker changes neither project nor connection mode");
    }
    [STAThread] static int Main(string[] args){
        resultDir=Path.GetFullPath(args.Length>1?args[1]:"review-results");Directory.CreateDirectory(resultDir);
        try{
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var m=new MainForm(null,true)){m.ShowInTaskbar=false;m.StartPosition=FormStartPosition.Manual;m.Location=new Point(-2500,-2000);m.Show();Pump();
                if(args.Length>0&&args[0]=="baseline")Baseline(m);else Candidate(m);
                m.SetDocument(DiagramIO.CreateBlank(),null,false);m.Close();
            }
            Console.WriteLine("CHECKS PASSED "+checks);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}}
