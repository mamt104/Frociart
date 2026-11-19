using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using Frociart;

internal static class SvgBranchTests
{
    private static int count;
    private static void Check(bool ok,string message) { count++;if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message); }
    private static XmlDocument Read(string file,out XmlNamespaceManager ns)
    {
        XmlDocument doc = new XmlDocument { XmlResolver = null }; doc.Load(file);
        ns = new XmlNamespaceManager(doc.NameTable); ns.AddNamespace("s","http://www.w3.org/2000/svg"); return doc;
    }
    [STAThread] public static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using (Form host = new Form { ClientSize = new Size(1100,900) })
        using (FlowCanvas canvas = new FlowCanvas { Dock = DockStyle.Fill })
        {
            host.Controls.Add(canvas);host.Show();Application.DoEvents();canvas.Diagram=Examples.Parity();
            string legacy = Path.Combine(args[0],"legacy.svg");DiagramExport.ExportSvg(canvas,legacy);
            XmlNamespaceManager ns;XmlDocument doc=Read(legacy,out ns);
            Check(doc.SelectSingleNode("//s:path[@stroke='#369F8B']",ns)!=null,"legacy true edge retains its original export color");
            Check(doc.SelectSingleNode("//s:path[@stroke='#CA758B']",ns)!=null,"legacy false edge retains its original export color");
            Check(doc.SelectNodes("//s:tspan[text()='Sì']",ns).Count==1&&doc.SelectNodes("//s:tspan[text()='No']",ns).Count==1,"legacy decision labels remain unchanged");
            FlowNode node=canvas.Diagram.Nodes.Single(n=>n.Kind==NodeKind.Decision);
            node.TrueLabel="Accetta <ora> & ";node.FalseLabel="Aspetta";node.TrueColor="#123AbC";node.FalseColor="#CA2255";
            string custom=Path.Combine(args[0],"custom.svg");DiagramExport.ExportSvg(canvas,custom);doc=Read(custom,out ns);
            Check(doc.SelectSingleNode("//s:path[@stroke='#123AbC']",ns)!=null&&doc.SelectSingleNode("//s:path[@stroke='#CA2255']",ns)!=null,"custom branch colors appear on SVG paths");
            Check(doc.SelectSingleNode("//s:text[@fill='#123AbC']",ns)!=null&&doc.SelectSingleNode("//s:text[@fill='#CA2255']",ns)!=null,"custom branch colors appear on SVG labels");
            Check(doc.SelectSingleNode("//s:path[@stroke='#123AbC']/s:title",ns).InnerText==node.TrueLabel,"full branch title is preserved and XML escaped");
            Check(doc.SelectSingleNode("//s:tspan[text()='Aspetta']",ns)!=null,"custom false label appears as vector text");
            Check(doc.SelectNodes("//s:image",ns).Count==0,"custom-label SVG remains fully vector");
            node.TrueLabel=new string('W',60);string longFile=Path.Combine(args[0],"long-label.svg");DiagramExport.ExportSvg(canvas,longFile);doc=Read(longFile,out ns);
            Check(doc.SelectSingleNode("//s:path[@stroke='#123AbC']/s:title",ns).InnerText==node.TrueLabel,"60-character branch keeps complete accessible title");
            Check(doc.SelectSingleNode("//s:rect[@width='180']",ns)!=null,"long label uses the same bounded pill width as canvas");
            Ui.SetDarkMode(false,false);DiagramExport.ExportSvg(canvas,custom);byte[] light=File.ReadAllBytes(custom);
            Ui.SetDarkMode(true,false);string dark=Path.Combine(args[0],"dark.svg");DiagramExport.ExportSvg(canvas,dark);
            Check(light.SequenceEqual(File.ReadAllBytes(dark)),"SVG export is independent of editor theme");
            Ui.SetDarkMode(false,false);
        }
        Console.WriteLine("SVG BRANCH TESTS PASSED: "+count+" checks");
    }
}
