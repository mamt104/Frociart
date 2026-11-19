using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using Frociart;
public static class PseudocodeTests
{
    static int checks;static string folder,output;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static string Read(string path){return File.ReadAllText(path).Replace("\r\n","\n");}
    static string Run(Diagram d,string input){var r=new Interpreter(d);string[] values=input.Split('\n');int i=0;for(int step=0;step<11000&&r.State!=RuntimeState.Error&&r.State!=RuntimeState.Completed;step++){if(r.State==RuntimeState.WaitingForInput){Check(i<values.Length,"unexpected input");Check(r.ProvideInput(values[i++]),"input accepted");}else r.Step();}Check(r.State==RuntimeState.Completed,"execution completed: "+r.Error);return String.Join("\n",r.Output);}
    static void Error(string text,int line){try{FlowPseudocode.Decode(text);throw new Exception("invalid input accepted");}catch(FlowPseudocode.ParseError e){Check(e.Line==line,"error line "+e.Line+" expected "+line+": "+e.Message);}}
    [STAThread] public static int Main(){try{
        string root=Environment.GetEnvironmentVariable("PSEUDOCODE_QA");folder=Path.Combine(root,"fixtures");output=Path.Combine(root,"pc-results");Directory.CreateDirectory(output);
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        foreach(string file in Directory.GetFiles(folder,"*.pseudo").OrderBy(x=>x)){
            string name=Path.GetFileNameWithoutExtension(file),input=Read(Path.Combine(folder,name+".inputs")),expected=Read(Path.Combine(folder,name+".expected"));Diagram d=FlowPseudocode.Decode(Read(file));byte[] before=DiagramIO.EncodeDocument(d);string code=FlowPseudocode.Encode(d);Diagram back=FlowPseudocode.Decode(code);
            Check(Run(d,input)==expected,name+" original execution");Check(Run(back,input)==expected,name+" converted execution");Check(before.SequenceEqual(DiagramIO.EncodeDocument(d)),name+" original unchanged");Check(d.Nodes.Count==back.Nodes.Count,name+" block count");Check(d.Edges.Count==back.Edges.Count,name+" edge count");
            Diagram android=DiagramIO.Load(Path.Combine(root,"java-results",name+".frociart"));Check(FlowPseudocode.Encode(android)==Read(Path.Combine(root,"java-results",name+".txt")),name+" platform interchange");Check(Run(android,input)==expected,name+" Android document executes on PC");
            Check(code==Read(Path.Combine(root,"java-results",name+".txt")),name+" identical exported pseudocode");File.WriteAllText(Path.Combine(output,name+".txt"),code);
        }
        Error("",1);Error("INIZIO\nxyz\nFINE",2);Error("SE vero ALLORA\nx=1",1);Error("MENTRE vero FAI\nFINE",2);Error("VAI A manca",1);Error("a:\nx=1\na:\nx=2",3);Error("a:\nVAI A a",2);Error("RIPETI\nx=1",1);Error("ALTRIMENTI",1);Error("x=System.exit(0)",1);Error("SCRIVI unknown(2)",1);Error("ultimo:",1);Error(new string('x',FlowPseudocode.MaxText+1),1);Error(String.Join("\n",Enumerable.Repeat("x=1",501)),500);Error(String.Concat(Enumerable.Repeat("SE vero ALLORA\n",49))+"x=1\n"+String.Concat(Enumerable.Repeat("FINE SE\n",49)),50);
        var open=FlowPseudocode.Decode("x=1\nSE x>0 ALLORA\nSCOLLEGATO\nALTRIMENTI\nSCRIVI x\nFINE SE");string explicitCode=FlowPseudocode.Encode(open);Check(explicitCode.Contains("SCOLLEGATO"),"open branch exported");Check(FlowPseudocode.Decode(explicitCode).Edges.Count==open.Edges.Count,"open branch remains open");
        var loop=FlowPseudocode.Decode("x=1\ncheck:\nSE x>0 ALLORA\nVAI A check\nALTRIMENTI\nFERMA\nFINE SE");Check(FlowPseudocode.Decode(FlowPseudocode.Encode(loop)).Edges.Any(e=>e.From==e.To),"self loop retained");
        string maximum=String.Join("\n",Enumerable.Repeat("x=1",498));var watch=Stopwatch.StartNew();var big=FlowPseudocode.Decode(maximum);string bigCode=FlowPseudocode.Encode(big);var bigBack=FlowPseudocode.Decode(bigCode);watch.Stop();Check(bigBack.Nodes.Count==500,"500-node boundary works");Console.WriteLine("500-block conversion milliseconds="+watch.ElapsedMilliseconds);
        FilesTest();Fuzz();NativeUi();Console.WriteLine("PASS PseudocodeTests: "+checks+" assertions, 26 execution fixtures, cross-platform interchange, 300 random graphs and native dialog.");return 0;
    }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
    static void FilesTest(){
        string file=Path.Combine(output,"pseudocodice.txt"),text="INIZIO\nSCRIVI \"Caffè ←\"\nFINE";
        PseudocodeDialog.WriteText(file,text);Check(PseudocodeDialog.ReadText(file)==text,"UTF-8 file roundtrip");Check(Run(FlowPseudocode.Decode(PseudocodeDialog.ReadText(file)),"")=="Caffè ←","saved text executes");
        PseudocodeDialog.WriteText(file,FlowPseudocode.Example);Check(PseudocodeDialog.ReadText(file)==FlowPseudocode.Example,"atomic replacement contains new code");
        try{PseudocodeDialog.WriteText(file,new string('x',FlowPseudocode.MaxText+1));throw new Exception("oversize saved");}catch(ArgumentException){Check(PseudocodeDialog.ReadText(file)==FlowPseudocode.Example,"oversize write leaves original intact");}
        string corrupt=Path.Combine(output,"invalid-utf8.txt");File.WriteAllBytes(corrupt,new byte[]{255,254,255});try{PseudocodeDialog.ReadText(corrupt);throw new Exception("invalid UTF8 accepted");}catch(System.Text.DecoderFallbackException){Check(true,"invalid UTF8 rejected");}
        Check(Directory.GetFiles(output,".frociart-*.tmp").Length==0,"no unfinished text export files");
    }
    static void Fuzz(){var random=new Random(41021);for(int trial=0;trial<300;trial++){
        var d=new Diagram();var start=new FlowNode{Kind=NodeKind.Start,Text="Inizio"};var init=new FlowNode{Kind=NodeKind.Process,Text="x=0"};var end=new FlowNode{Kind=NodeKind.End,Text="Fine"};d.Nodes.Add(start);d.Nodes.Add(init);
        for(int i=0;i<8;i++)d.Nodes.Add(new FlowNode{Kind=i%3==0?NodeKind.Decision:NodeKind.Process,Text=i%3==0?"x < "+(2+random.Next(8)):"x=x+1"});d.Nodes.Add(end);d.Edges.Add(new FlowEdge{From=start.Id,To=init.Id});d.Edges.Add(new FlowEdge{From=init.Id,To=d.Nodes[2].Id});
        for(int i=2;i<d.Nodes.Count-1;i++){var n=d.Nodes[i];if(n.Kind==NodeKind.Decision){d.Edges.Add(new FlowEdge{From=n.Id,To=d.Nodes[2+random.Next(d.Nodes.Count-2)].Id,Branch="true"});d.Edges.Add(new FlowEdge{From=n.Id,To=d.Nodes[i+1].Id,Branch="false"});}else d.Edges.Add(new FlowEdge{From=n.Id,To=d.Nodes[i+1].Id});}
        var back=FlowPseudocode.Decode(FlowPseudocode.Encode(d));var a=new Interpreter(d);var b=new Interpreter(back);for(int step=0;step<10001;step++){Check(a.State==b.State,"fuzz state "+trial+"/"+step);Check(String.Join(";",a.Variables.Select(x=>x.Key+"="+x.Value))==String.Join(";",b.Variables.Select(x=>x.Key+"="+x.Value)),"fuzz variables "+trial+"/"+step);if(a.State==RuntimeState.Error||a.State==RuntimeState.Completed)break;a.Step();b.Step();}Check(d.Nodes.Count==back.Nodes.Count,"fuzz blocks "+trial);Check(d.Edges.Count==back.Edges.Count,"fuzz edges "+trial);
    }}
    static void NativeUi(){Diagram generated=null;string draft=null;var d=Examples.Parity();byte[] before=DiagramIO.EncodeDocument(d);using(var dialog=new PseudocodeDialog(d,null,x=>generated=x,x=>draft=x)){
        dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-3000,-3000);dialog.Show();Application.DoEvents();Check(dialog.Editor.Text.Contains("LEGGI"),"native editor generated code");
        dialog.Editor.Text="INIZIO\nnot an assignment\nFINE";dialog.CreateDiagram();Application.DoEvents();Check(generated==null,"invalid import did not create document");Check(dialog.Editor.Text.Contains("not an assignment"),"invalid text retained");Console.WriteLine("Selection="+dialog.Editor.SelectionStart+","+dialog.Editor.SelectionLength+" text=["+dialog.Editor.SelectedText+"] line2="+dialog.Editor.GetFirstCharIndexFromLine(1)+" lines="+dialog.Editor.Lines.Length);Check(dialog.Editor.SelectedText=="not an assignment","error focuses line 2");
        dialog.Editor.Text=FlowPseudocode.Example;dialog.CreateDiagram();Check(generated!=null&&Run(generated,"-3")=="Negativo","native import creates executable graph");Check(before.SequenceEqual(DiagramIO.EncodeDocument(d)),"dialog preserves source");Check(draft==FlowPseudocode.Example.Replace("\n","\r\n")||draft==FlowPseudocode.Example,"draft retained");
        foreach(bool dark in new[]{false,true}){Ui.SetDarkMode(dark);Ui.ApplyTheme(dialog);foreach(var size in new[]{new Size(850,680),new Size(650,480)}){dialog.Size=size;Application.DoEvents();Check(dialog.Editor.Width>400&&dialog.Editor.Height>140,"editor has usable area");using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(Path.Combine(output,"dialog-"+dark+"-"+size.Width+".png"));}}}dialog.Close();
    }}
}
