// Targeted offline export acceptance harness. Exclude from the production executable.
// mcs -langversion:6 -r:System.Windows.Forms -r:System.Drawing -r:System.Core -r:System.Runtime.Serialization -r:System.Xml \
//   -out:ExportTests.exe Core.cs Examples.cs Theme.cs FlowCanvas.cs DiagramExport.cs ExportDialog.cs ExportTests.cs
// xvfb-run -a mono ExportTests.exe /tmp/frociart-export-qa
// Parse and render the emitted PDFs independently with pypdf, pdfinfo and pdftoppm.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Frociart;

public static class ExportTests
{
    private static int passes;
    private static string folder;
    private static string FileName(string name) { return Path.Combine(folder, name); }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); passes++; Console.WriteLine("PASS " + message); }
    private static void Fails(Action action, string message)
    { bool failed = false; try { action(); } catch (InvalidOperationException) { failed = true; } Check(failed, message); }

    [STAThread]
    public static int Main(string[] args)
    {
        folder = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "frociart-export-qa");
        Directory.CreateDirectory(folder);
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (Form host = new Form { ClientSize = new Size(1000, 800) })
            using (FlowCanvas canvas = new FlowCanvas { Dock = DockStyle.Fill })
            {
                host.Controls.Add(canvas); host.Show(); Application.DoEvents();
                canvas.Diagram = Examples.Parity(); canvas.FitToView();
                PdfExportPlan p = DiagramExport.GetPdfPlan(canvas, false);
                Check(p.PageCount == 1 && p.CanExport && p.Scale > 0 && p.Scale <= 0.75, "single-page plan fits and never enlarges beyond natural scale");
                Check(Math.Abs(Math.Min(p.PageWidth, p.PageHeight) - 595.2756) < 0.01, "page format is ISO A4");
                Check(p.WorldBounds.Left < canvas.Diagram.Nodes.Min(n => n.X), "plan includes shape and routing margins");

                canvas.SelectNode("odd"); canvas.CurrentNodeId = "odd"; canvas.CurrentEdgeId = canvas.Diagram.Edges[0].Id;
                canvas.IssueNodeIds = new HashSet<string> { "odd" }; canvas.BeginConnect("check", "true");
                string before = Snapshot(canvas.Diagram);
                PointF worldBefore = canvas.ClientToWorld(new Point(80, 90)); float zoom = canvas.Zoom;
                int statusChanges = 0; canvas.StatusChanged += delegate { statusChanges++; };
                DiagramExport.ExportPdf(canvas, FileName("parity.pdf"));
                DiagramExport.ExportSvg(canvas, FileName("parity.svg"));
                DiagramExport.ExportPng(canvas, FileName("parity.png"));
                byte[] markedPdf = File.ReadAllBytes(FileName("parity.pdf"));
                byte[] markedSvg = File.ReadAllBytes(FileName("parity.svg"));
                byte[] markedPng = File.ReadAllBytes(FileName("parity.png"));
                Check(before == Snapshot(canvas.Diagram), "export does not edit diagram data");
                Check(statusChanges == 0, "export does not change editor status before its destination is committed");
                Check(canvas.CurrentNodeId == "odd" && canvas.IsConnecting && canvas.Zoom == zoom &&
                    canvas.ClientToWorld(new Point(80, 90)) == worldBefore, "export preserves execution, connection and view state");
                canvas.CancelConnection(); canvas.SelectNode(null); canvas.CurrentNodeId = null; canvas.CurrentEdgeId = null;
                canvas.IssueNodeIds = new HashSet<string>();
                DiagramExport.ExportPdf(canvas, FileName("parity.pdf"));
                DiagramExport.ExportSvg(canvas, FileName("parity.svg"));
                DiagramExport.ExportPng(canvas, FileName("parity.png"));
                Check(markedPdf.SequenceEqual(File.ReadAllBytes(FileName("parity.pdf"))), "PDF excludes selection, validation badges and active execution markers");
                Check(markedSvg.SequenceEqual(File.ReadAllBytes(FileName("parity.svg"))), "SVG excludes editing and execution markers");
                Check(markedPng.SequenceEqual(File.ReadAllBytes(FileName("parity.png"))), "PNG excludes editing and execution markers");
                Check(Encoding.ASCII.GetString(markedPdf, 0, 8) == "%PDF-1.4", "native PDF signature is correct");
                using (Bitmap png = new Bitmap(FileName("parity.png")))
                    Check(png.Width > 500 && png.Height > 500 && png.Width <= 8000 && png.Height <= 8000, "PNG is high-resolution and bounded");

                Diagram unicode = Examples.Countdown(); unicode.Title = "Caffè <ok> & \"Sì\" — Ω 漢字";
                foreach (FlowNode node in unicode.Nodes) { node.X -= 1000; node.Y -= 780; }
                unicode.Nodes.First(n => n.Kind == NodeKind.Input).Text = "quantità";
                unicode.Nodes.First(n => n.Kind == NodeKind.Input).Prompt = "Inserisci quantità, città e caffè";
                unicode.Nodes.First(n => n.Kind == NodeKind.Process).Text = "numero = numero - 1\nrisultato = \"Caffè <ok> & Ω\"";
                unicode.Nodes.First(n => n.Kind == NodeKind.Output).Text = "\"Risultato molto lungo: àèìòù <>& \\\"frase\\\" e altri caratteri Ω 漢字 🙂\"";
                canvas.Diagram = unicode;
                DiagramExport.ExportPdf(canvas, FileName("loop-unicode.pdf"));
                DiagramExport.ExportSvg(canvas, FileName("loop-unicode.svg"));
                XmlDocument doc = new XmlDocument { XmlResolver = null }; doc.Load(FileName("loop-unicode.svg"));
                XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable); ns.AddNamespace("s", "http://www.w3.org/2000/svg");
                Check(doc.DocumentElement.NamespaceURI == "http://www.w3.org/2000/svg" && doc.SelectNodes("//s:path", ns).Count >= unicode.Edges.Count,
                    "SVG root and route paths use the SVG namespace");
                Check(doc.SelectSingleNode("/s:svg/s:title", ns).InnerText == unicode.Title, "SVG preserves Unicode and safely escapes XML in the title");
                Check(doc.SelectNodes("//s:text", ns).Count >= unicode.Nodes.Count && doc.SelectNodes("//s:image", ns).Count == 0,
                    "SVG contains vector text and no embedded bitmap");
                Check(doc.SelectNodes("//s:clipPath", ns).Count == unicode.Nodes.Count, "long block labels have local clipping");
                Check(doc.DocumentElement.GetAttribute("viewBox").StartsWith("-", StringComparison.Ordinal), "negative coordinates remain visible in SVG viewBox");
                Check(!File.ReadAllText(FileName("loop-unicode.svg")).Contains("xmlns=\"\""), "SVG descendants never lose the namespace");

                canvas.Diagram = LongDiagram(); p = DiagramExport.GetPdfPlan(canvas, true);
                Check(p.PageCount > 1 && p.PageCount <= DiagramExport.MaxPdfPages && p.Scale == 0.75,
                    "multipage preserves readable natural scale");
                DiagramExport.ExportPdf(canvas, FileName("tiles.pdf"), true);
                File.WriteAllText(FileName("tiles-plan.txt"), p.PageCount + " " + p.Rows + " " + p.Columns + " " + p.PageWidth.ToString(CultureInfo.InvariantCulture) + " " + p.PageHeight.ToString(CultureInfo.InvariantCulture));

                Diagram landscape = DiagramIO.CreateBlank(); landscape.Nodes[0].X = 0; landscape.Nodes[0].Y = 0;
                landscape.Nodes[1].X = 900; landscape.Nodes[1].Y = 130; canvas.Diagram = landscape;
                Check(DiagramExport.GetPdfPlan(canvas, false).Landscape, "wide diagrams choose landscape automatically");

                canvas.Diagram = new Diagram { Title = "Bozza" };
                canvas.Diagram.Nodes.Add(new FlowNode { Kind = NodeKind.Process, Text = "ancora da completare\u0001\uD800", X = -50, Y = -80 });
                DiagramExport.ExportSvg(canvas, FileName("draft.svg"));
                doc.Load(FileName("draft.svg"));
                Check(doc.InnerText.Contains("ancora da completare"), "incomplete diagrams export without an execution validation gate");
                Check(doc.InnerText.Contains("\uFFFD"), "invalid XML controls and isolated surrogates are safely replaced");

                Diagram enormous = new Diagram { Title = "Limite di estensione" };
                for (int i = 0; i < 500; i++) enormous.Nodes.Add(new FlowNode { Kind = NodeKind.Process, Text = "x = " + i,
                    X = i % 2 == 0 ? -99900 : 99900, Y = -99900 + i * 400 });
                canvas.Diagram = enormous;
                Stopwatch time = Stopwatch.StartNew(); p = DiagramExport.GetPdfPlan(canvas, true);
                Check(!p.CanExport && p.PageCount > DiagramExport.MaxPdfPages && time.ElapsedMilliseconds < 2000,
                    "500 widely separated blocks reject excessive page counts before rendering");
                string existing = FileName("preserve.pdf"); File.WriteAllText(existing, "originale");
                Fails(delegate { DiagramExport.ExportPdf(canvas, existing, true); }, "unbounded multipage export fails with an actionable error");
                Check(File.ReadAllText(existing) == "originale", "failed export preserves an existing destination");
                File.Delete(existing);
                time.Restart(); DiagramExport.ExportPdf(canvas, FileName("huge.pdf"));
                Check(time.ElapsedMilliseconds < 20000 && new FileInfo(FileName("huge.pdf")).Length < 1500000,
                    "single-page export of huge world coordinates stays bounded");
                Check(Directory.GetFiles(folder, ".frociart-export-*.tmp").Length == 0, "successful and rejected exports leave no temporary files");

                canvas.Diagram = new Diagram(); Fails(delegate { DiagramExport.GetPdfPlan(canvas, false); }, "empty diagrams return a clear error");
                Check(ExportDialog.SafeFileName("CON.txt") == "Diagramma-CON.txt" && ExportDialog.SafeFileName("  a<b:c/d\\e?.  ") == "abcde" &&
                    ExportDialog.SafeFileName(null) == "Diagramma", "export filenames are safe on Windows including reserved device names");

                canvas.Diagram = Examples.Parity();
                using (ExportDialog dialog = new ExportDialog(canvas))
                {
                    dialog.Show(host); Application.DoEvents();
                    SaveDialogScreenshot(dialog, "export-dialog.png");
                    Check(dialog.ExportedPath == null && dialog.DialogResult == DialogResult.None, "opening export dialog creates no file and reports no success");
                    RadioButton pdf = Private<RadioButton>(dialog, "pdf"), svg = Private<RadioButton>(dialog, "svg"), png = Private<RadioButton>(dialog, "png");
                    Check(pdf.Checked && !svg.Checked && !png.Checked, "PDF is the initial export format");
                    svg.Checked = true; Application.DoEvents();
                    Check(!pdf.Checked && svg.Checked && !png.Checked && !Private<CheckBox>(dialog, "multipage").Visible,
                        "format selection is mutually exclusive and hides PDF options for SVG");
                    png.Checked = true; pdf.Checked = true; Private<CheckBox>(dialog, "multipage").Checked = true;
                    dialog.Size = dialog.MinimumSize; Application.DoEvents(); SaveDialogScreenshot(dialog, "export-dialog-small.png");
                    Label summary = Private<Label>(dialog, "summary");
                    Check(summary.Height >= 55 && Private<FlowButton>(dialog, "export").Visible, "minimum dialog size keeps summary and export action available");
                    dialog.Close();
                }
                host.Close();
            }
            Console.WriteLine("EXPORT TESTS PASSED: " + passes + "; artifacts: " + folder); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }

    private static T Private<T>(object instance, string field)
    { return (T)instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance); }
    private static string Snapshot(Diagram diagram)
    { return Convert.ToBase64String((byte[])typeof(DiagramIO).GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { diagram })); }
    private static void SaveDialogScreenshot(Form dialog, string name)
    {
        dialog.Refresh(); Application.DoEvents(); System.Threading.Thread.Sleep(90); Application.DoEvents();
        using (Bitmap bitmap = new Bitmap(dialog.ClientSize.Width, dialog.ClientSize.Height))
        
        { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.ClientSize)); bitmap.Save(FileName(name)); }
    }
    private static Diagram LongDiagram()
    {
        Diagram d = new Diagram { Title = "Passaggi in sequenza — esportazione su più pagine" };
        FlowNode previous = null;
        for (int i = 0; i < 19; i++)
        {
            FlowNode n = new FlowNode { Kind = i == 0 ? NodeKind.Start : i == 18 ? NodeKind.End : NodeKind.Process,
                Text = i == 0 ? "Inizio" : i == 18 ? "Fine" : "passaggio = " + i, X = -160, Y = -250 + i * 150 };
            d.Nodes.Add(n); if (previous != null) d.Edges.Add(new FlowEdge { From = previous.Id, To = n.Id }); previous = n;
        }
        return d;
    }
}
