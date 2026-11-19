using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Frociart;
public static class PythonUiTests
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    [STAThread] public static int Main(){try{
        Application.EnableVisualStyles();string root=Path.Combine(Environment.GetEnvironmentVariable("PYTHON_QA"),"native-ui");Directory.CreateDirectory(root);
        var doc=FlowPseudocode.Decode("LEGGI numero\nSE numero % 2 = 0 ALLORA\nSTAMPA \"Pari\"\nALTRIMENTI\nSTAMPA \"Dispari\"\nFINE SE");
        doc.Title=SourceCodeDialog.SupportMarker;var before=DiagramIO.EncodeDocument(doc);string selectedFunction=null,selectedNode=null;
        foreach(bool dark in new[]{true,false}){
            Ui.SetDarkMode(dark,false);
            using(var dialog=new SourceCodeDialog(doc,(f,n)=>{selectedFunction=f;selectedNode=n;})){
                dialog.Show();Application.DoEvents();
                Check(dialog.Source!=null&&dialog.Save.Enabled,"valid source and save enabled");Check(dialog.Editor.ReadOnly,"read only source");
                Check(dialog.Editor.Text.Contains("def main():")&&!dialog.Editor.Text.Contains("class _fc_Runtime:"),"program-only view handles marker inside title");
                var block=doc.Nodes.First(n=>n.Kind==NodeKind.Decision);var line=dialog.Source.ForBlock(null,block.Id).First();
                dialog.Editor.Select(dialog.Editor.GetFirstCharIndexFromLine(line.Number-1),0);Check(dialog.SelectedOrigin().NodeId==block.Id,"source line maps to decision with Windows CRLF");
                dialog.Support.Checked=true;Check(dialog.Editor.Text.Contains("class _fc_Runtime:"),"support is inspectable");
                string path=Path.Combine(root,dark?"dark.py":"light.py");SourceCodeDialog.WriteSource(path,dialog.Source.Text);Check(File.ReadAllText(path)==dialog.Source.Text,"full source persisted");
                SourceCodeDialog.WriteSource(path,dialog.Source.Text);Check(File.ReadAllText(path)==dialog.Source.Text,"atomic replacement persisted");
                dialog.Support.Checked=false;
                using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,dark?"python-dark.png":"python-light.png"));}
                dialog.Editor.Select(dialog.Editor.GetFirstCharIndexFromLine(line.Number-1),0);dialog.LocateSelection();Check(selectedFunction==null&&selectedNode==block.Id,"locate callback selects exact block");
            }
        }
        Check(before.SequenceEqual(DiagramIO.EncodeDocument(doc)),"viewer/export preserve diagram");
        var invalid=new Diagram();using(var dialog=new SourceCodeDialog(invalid,null)){Check(dialog.Source==null&&!dialog.Save.Enabled,"invalid graph cannot export");Check(dialog.Editor.TextLength>0,"invalid graph diagnostics remain readable in scrollable panel");}
        string destination=Path.Combine(root,"preserved.py");File.WriteAllText(destination,"original");bool rejected=false;
        try{SourceCodeDialog.WriteSource(destination,new string('x',FlowSourceText.Builder.MaxCharacters+1));}catch(ArgumentException){rejected=true;}
        Check(rejected&&File.ReadAllText(destination)=="original","oversized output preserves original");
        Check(Directory.GetFiles(root,".frociart-python-*.tmp").Length==0,"no leftover atomic-write temporary files");
        Console.WriteLine("PYTHON_UI PASS checks="+checks);return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
