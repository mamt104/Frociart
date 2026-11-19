using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace Frociart
{
    /// <summary>A bounded A4 layout. Scale is PDF points per logical canvas pixel.</summary>
    public sealed class PdfExportPlan
    {
        public int Columns { get; internal set; }
        public int Rows { get; internal set; }
        public int PageCount { get { return Columns * Rows; } }
        public bool Landscape { get; internal set; }
        public bool Multipage { get; internal set; }
        public bool CanExport { get; internal set; }
        public string Problem { get; internal set; }
        public double Scale { get; internal set; }
        internal double BodyFontScale=1;
        public double BodyTextPoints { get { return 14 * BodyFontScale * Scale; } }
        public double PageWidth { get { return Landscape ? 841.8898 : 595.2756; } }
        public double PageHeight { get { return Landscape ? 595.2756 : 841.8898; } }
        public RectangleF WorldBounds { get; internal set; }
        internal double ContentWidth { get { return PageWidth - 72; } }
        internal double ContentHeight { get { return PageHeight - 112; } }
        internal double TileStepX { get { return (ContentWidth - DiagramExport.OverlapPoints) / Scale; } }
        internal double TileStepY { get { return (ContentHeight - DiagramExport.OverlapPoints) / Scale; } }
    }

    /// <summary>
    /// Offline exports with no executable, printer, network or third-party runtime dependency.
    /// PDF uses one losslessly compressed, 200-dpi RGB image per A4 page; SVG remains vector.
    /// The canvas is read only throughout export. Incomplete algorithms can be exported.
    /// </summary>
    public static class DiagramExport
    {
        public const int MaxPdfPages = 24;
        public const int PdfDpi = 200;
        internal const double OverlapPoints = 18;
        private const double NaturalScale = 0.75;
        private const float WorldMargin = 24;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static PdfExportPlan GetPdfPlan(FlowCanvas canvas, bool multipage)
        {
            Validate(canvas);
            RectangleF bounds = canvas.GetExportContentBounds();
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("Aggiungi almeno un blocco prima di esportare.");
            bounds.Inflate(WorldMargin, WorldMargin);
            PdfExportPlan portrait = MakePlan(bounds, false, multipage);
            PdfExportPlan landscape = MakePlan(bounds, true, multipage);
            PdfExportPlan result;
            if (!multipage) result = landscape.Scale > portrait.Scale + 0.000001 ? landscape : portrait;
            else if (portrait.PageCount != landscape.PageCount)
                result = portrait.PageCount < landscape.PageCount ? portrait : landscape;
            else
            {
                double pfit = Math.Min(portrait.ContentWidth / bounds.Width, portrait.ContentHeight / bounds.Height);
                double lfit = Math.Min(landscape.ContentWidth / bounds.Width, landscape.ContentHeight / bounds.Height);
                result = lfit > pfit + 0.000001 ? landscape : portrait;
            }
            result.BodyFontScale=canvas.AppearanceStyle.Design.Scale;
            result.CanExport = result.PageCount <= MaxPdfPages;
            result.Problem = result.CanExport ? "" : "Servirebbero " + result.PageCount.ToString(Invariant) +
                " pagine A4. Il limite è " + MaxPdfPages.ToString(Invariant) +
                ": avvicina i blocchi oppure disattiva «Dividi in pagine A4».";
            return result;
        }

        private static PdfExportPlan MakePlan(RectangleF bounds, bool landscape, bool multipage)
        {
            PdfExportPlan p = new PdfExportPlan { WorldBounds = bounds, Landscape = landscape, Multipage = multipage };
            p.Scale = multipage ? NaturalScale : Math.Min(NaturalScale,
                Math.Min(p.ContentWidth / bounds.Width, p.ContentHeight / bounds.Height));
            p.Columns = multipage ? TileCount(bounds.Width, p.ContentWidth / p.Scale, p.TileStepX) : 1;
            p.Rows = multipage ? TileCount(bounds.Height, p.ContentHeight / p.Scale, p.TileStepY) : 1;
            return p;
        }

        private static int TileCount(double length, double capacity, double step)
        {
            if (length <= capacity + 0.01) return 1;
            return 1 + (int)Math.Ceiling((length - capacity) / step);
        }

        public static void ExportPdf(FlowCanvas canvas, string path, bool multipage = false)
        {
            PdfExportPlan plan = GetPdfPlan(canvas, multipage);
            if (!plan.CanExport) throw new InvalidOperationException(plan.Problem);
            WriteAtomically(path, delegate(string temporary)
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    PdfWriter writer = new PdfWriter(stream, plan.PageCount);
                    writer.Begin(canvas.Diagram.Title);
                    for (int page = 0; page < plan.PageCount; page++)
                    {
                        // One page at a time: even widely separated blocks never allocate a world-sized bitmap.
                        using (Bitmap bitmap = RenderPdfPage(canvas, plan, page)) writer.AddPage(bitmap, plan, page);
                    }
                    writer.Finish();
                    stream.Flush(true);
                }
            });
        }

        public static void ExportPng(FlowCanvas canvas, string path)
        {
            Validate(canvas);
            WriteAtomically(path, delegate(string temporary)
            {
                using (Bitmap bitmap = canvas.RenderExportBitmap()) bitmap.Save(temporary, ImageFormat.Png);
            });
        }

        public static void ExportSvg(FlowCanvas canvas, string path)
        {
            Validate(canvas);
            var style=canvas.AppearanceStyle;
            RectangleF bounds = canvas.GetExportContentBounds();
            bounds.Inflate(52, 52);
            WriteAtomically(path, delegate(string temporary)
            {
                XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, NewLineHandling = NewLineHandling.Entitize };
                using (XmlWriter xml = XmlWriter.Create(temporary, settings))
                using (Bitmap measureBitmap = new Bitmap(1, 1))
                using (Graphics measure = Graphics.FromImage(measureBitmap))
                {
                    xml.WriteStartDocument();
                    xml.WriteStartElement("svg", "http://www.w3.org/2000/svg");
                    Attr(xml, "version", "1.1");
                    Attr(xml, "width", F(bounds.Width)); Attr(xml, "height", F(bounds.Height));
                    Attr(xml, "viewBox", F(bounds.X) + " " + F(bounds.Y) + " " + F(bounds.Width) + " " + F(bounds.Height));
                    Attr(xml, "role", "img");
                    xml.WriteElementString("title", XmlSafe(canvas.Diagram.Title ?? "Diagramma"));
                    xml.WriteElementString("desc", "Diagramma di flusso creato con Frociart. Blocchi e collegamenti vettoriali.");
                    Rect(xml, bounds, 0, "#FFFFFF", null, 0);
                    Dictionary<string, FlowNode> nodes = canvas.Diagram.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
                    foreach (FlowEdge edge in canvas.Diagram.Edges)
                    {
                        FlowNode from, to;
                        if (nodes.TryGetValue(edge.From, out from) && nodes.TryGetValue(edge.To, out to))
                            SvgEdge(xml, canvas.GetExportRoute(from, to, edge.Branch), edge.Branch, from, measure);
                    }
                    int index = 0;
                    foreach (FlowNode node in canvas.Diagram.Nodes) SvgNode(xml, node, index++, measure,style);
                    xml.WriteEndElement(); xml.WriteEndDocument();
                }
                style.StyleSvg(temporary);
            });
        }

        private static void Validate(FlowCanvas canvas)
        {
            if (canvas == null) throw new ArgumentNullException("canvas");
            if (canvas.IsDisposed) throw new ObjectDisposedException("canvas");
            DiagramIO.CheckDocument(canvas.Diagram);
            if (canvas.Diagram.Nodes.Count == 0)
                throw new InvalidOperationException("Aggiungi almeno un blocco prima di esportare.");
        }

        internal static void WriteAtomically(string path, Action<string> write)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Scegli un nome per il file.");
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("La cartella scelta non esiste.");
            string temporary = Path.Combine(directory, ".frociart-export-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                write(temporary);
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static Bitmap RenderPdfPage(FlowCanvas canvas, PdfExportPlan plan, int page)
        {
            double pixelPerPoint = PdfDpi / 72.0;
            int width = (int)Math.Ceiling(plan.PageWidth * pixelPerPoint);
            int height = (int)Math.Ceiling(plan.PageHeight * pixelPerPoint);
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            try
            {
                bitmap.SetResolution(PdfDpi, PdfDpi);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.PageUnit = GraphicsUnit.Pixel;
                    g.Clear(Color.White);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    string title = String.IsNullOrWhiteSpace(canvas.Diagram.Title) ? "Diagramma di flusso" : canvas.Diagram.Title;
                    using (Font titleFont = new Font("Segoe UI", (float)(12 * pixelPerPoint), FontStyle.Bold, GraphicsUnit.Pixel))
                    using (Brush ink = new SolidBrush(canvas.AppearanceStyle.Neutral(Ui.LightInk)))
                    using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        var titleBounds = new RectangleF((float)(36 * pixelPerPoint), (float)(23 * pixelPerPoint), (float)(plan.ContentWidth * pixelPerPoint), (float)(23 * pixelPerPoint));
                        if (!FlowText.DrawUnicode(g, title, titleFont, canvas.AppearanceStyle.Neutral(Ui.LightInk), titleBounds, format)) g.DrawString(title, titleFont, ink, titleBounds, format);
                    }

                    double tileWidth = plan.ContentWidth / plan.Scale, tileHeight = plan.ContentHeight / plan.Scale;
                    int column = page % plan.Columns, row = page / plan.Columns;
                    double worldX = plan.WorldBounds.Left + column * plan.TileStepX;
                    double worldY = plan.WorldBounds.Top + row * plan.TileStepY;
                    if (plan.Columns == 1) worldX -= (tileWidth - plan.WorldBounds.Width) / 2;
                    if (plan.Rows == 1) worldY -= (tileHeight - plan.WorldBounds.Height) / 2;
                    RectangleF visible = new RectangleF((float)worldX, (float)worldY, (float)tileWidth, (float)tileHeight);
                    RectangleF contentPixels = new RectangleF((float)(36 * pixelPerPoint), (float)(56 * pixelPerPoint),
                        (float)(plan.ContentWidth * pixelPerPoint), (float)(plan.ContentHeight * pixelPerPoint));
                    GraphicsState state = g.Save();
                    try
                    {
                        g.SetClip(contentPixels);
                        float scale = (float)(plan.Scale * pixelPerPoint);
                        using (Matrix transform = new Matrix(scale, 0, 0, scale,
                            contentPixels.X - (float)worldX * scale, contentPixels.Y - (float)worldY * scale))
                            g.Transform = transform;
                        canvas.RenderExport(g, visible, scale);
                    }
                    finally { g.Restore(state); }

                    using (Font footer = new Font("Segoe UI", (float)(8.5 * pixelPerPoint), FontStyle.Regular, GraphicsUnit.Pixel))
                    using (Brush muted = new SolidBrush(canvas.AppearanceStyle.Ink(Ui.LightMuted,false)))
                    using (StringFormat left = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    using (StringFormat right = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        float y = (float)((plan.PageHeight - 35) * pixelPerPoint);
                        string location = plan.PageCount > 1 ? "Frociart  ·  riga " + (row + 1) + ", colonna " + (column + 1) : "Frociart";
                        g.DrawString(location, footer, muted, new RectangleF((float)(36 * pixelPerPoint), y,
                            (float)((plan.ContentWidth - 110) * pixelPerPoint), (float)(17 * pixelPerPoint)), left);
                        g.DrawString("Pagina " + (page + 1) + " / " + plan.PageCount, footer, muted,
                            new RectangleF((float)((plan.PageWidth - 146) * pixelPerPoint), y, (float)(110 * pixelPerPoint), (float)(17 * pixelPerPoint)), right);
                    }
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }

        private sealed class PdfWriter
        {
            private readonly Stream output;
            private readonly long[] offsets;
            private readonly int pages;
            internal PdfWriter(Stream output, int pages)
            { this.output = output; this.pages = pages; offsets = new long[4 + pages * 3]; }

            internal void Begin(string title)
            {
                Write("%PDF-1.4\n%");
                output.Write(new byte[] { 0xE2, 0xE3, 0xCF, 0xD3, 10 }, 0, 5);
                Object(1); Write("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
                Object(2); Write("<< /Type /Pages /Count " + pages + " /Kids [");
                for (int i = 0; i < pages; i++) Write((4 + i * 3) + " 0 R ");
                Write("] >>\nendobj\n");
                Object(3); Write("<< /Title <FEFF" + Hex(Encoding.BigEndianUnicode.GetBytes(title ?? "Diagramma")) +
                    "> /Producer (Frociart) >>\nendobj\n");
            }

            internal void AddPage(Bitmap bitmap, PdfExportPlan plan, int index)
            {
                int pageId = 4 + index * 3, contentId = pageId + 1, imageId = pageId + 2;
                Object(pageId);
                Write("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + F(plan.PageWidth) + " " + F(plan.PageHeight) +
                    "] /Resources << /XObject << /Im0 " + imageId + " 0 R >> >> /Contents " + contentId + " 0 R >>\nendobj\n");
                byte[] content = Encoding.ASCII.GetBytes("q\n" + F(plan.PageWidth) + " 0 0 " + F(plan.PageHeight) + " 0 0 cm\n/Im0 Do\nQ\n");
                Object(contentId); Write("<< /Length " + content.Length + " >>\nstream\n");
                output.Write(content, 0, content.Length); Write("endstream\nendobj\n");
                using (MemoryStream compressed = CompressRgb(bitmap))
                {
                    Object(imageId); Write("<< /Type /XObject /Subtype /Image /Width " + bitmap.Width + " /Height " + bitmap.Height +
                        " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Interpolate false /Filter /FlateDecode /Length " +
                        compressed.Length + " >>\nstream\n");
                    compressed.Position = 0; compressed.CopyTo(output);
                    Write("\nendstream\nendobj\n");
                }
            }

            internal void Finish()
            {
                long xref = output.Position;
                Write("xref\n0 " + offsets.Length + "\n0000000000 65535 f \n");
                for (int i = 1; i < offsets.Length; i++)
                    Write(offsets[i].ToString("0000000000", Invariant) + " 00000 n \n");
                Write("trailer\n<< /Size " + offsets.Length + " /Root 1 0 R /Info 3 0 R >>\nstartxref\n" +
                    xref.ToString(Invariant) + "\n%%EOF\n");
            }

            private void Object(int id) { offsets[id] = output.Position; Write(id + " 0 obj\n"); }
            private void Write(string value) { byte[] bytes = Encoding.ASCII.GetBytes(value); output.Write(bytes, 0, bytes.Length); }
            private static string Hex(byte[] bytes)
            { StringBuilder b = new StringBuilder(bytes.Length * 2); foreach (byte v in bytes) b.Append(v.ToString("X2", Invariant)); return b.ToString(); }

            private static MemoryStream CompressRgb(Bitmap bitmap)
            {
                MemoryStream result = new MemoryStream();
                BitmapData data = null;
                try
                {
                    // PDF FlateDecode requires a zlib stream. DeflateStream supplies its raw DEFLATE body.
                    result.WriteByte(0x78); result.WriteByte(0x9C);
                    uint a = 1, b = 0;
                    data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    byte[] bgr = new byte[bitmap.Width * 3]; byte[] rgb = new byte[bgr.Length];
                    using (DeflateStream deflate = new DeflateStream(result, CompressionLevel.Optimal, true))
                    {
                        for (int y = 0; y < bitmap.Height; y++)
                        {
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bgr, 0, bgr.Length);
                            for (int x = 0; x < bgr.Length; x += 3)
                            { rgb[x] = bgr[x + 2]; rgb[x + 1] = bgr[x + 1]; rgb[x + 2] = bgr[x]; }
                            for (int start = 0; start < rgb.Length; start += 5552)
                            {
                                int end = Math.Min(start + 5552, rgb.Length);
                                for (int i = start; i < end; i++) { a += rgb[i]; b += a; }
                                a %= 65521; b %= 65521;
                            }
                            deflate.Write(rgb, 0, rgb.Length);
                        }
                    }
                    uint checksum = (b << 16) | a;
                    result.WriteByte((byte)(checksum >> 24)); result.WriteByte((byte)(checksum >> 16));
                    result.WriteByte((byte)(checksum >> 8)); result.WriteByte((byte)checksum);
                    return result;
                }
                catch { result.Dispose(); throw; }
                finally { if (data != null) bitmap.UnlockBits(data); }
            }
        }

        private static void SvgEdge(XmlWriter xml, IReadOnlyList<PointF> points, string branch, FlowNode source, Graphics measure)
        {
            if (points.Count < 2) return;
            string customColor = branch == "true" ? source.TrueColor : branch == "false" ? source.FalseColor : null;
            string color = customColor != null ? source.GetBranchColor(branch) : branch == "true" ? "#369F8B" : branch == "false" ? "#CA758B" : "#96A5BB";
            StringBuilder path = new StringBuilder("M " + XY(points[0]));
            for (int i = 1; i < points.Count - 1; i++)
            {
                PointF previous = points[i - 1], corner = points[i], next = points[i + 1];
                double beforeLength = Distance(previous, corner), afterLength = Distance(corner, next);
                if (beforeLength < 0.1 || afterLength < 0.1) continue;
                double radius = Math.Min(10, Math.Min(beforeLength / 2, afterLength / 2));
                PointF before = new PointF((float)(corner.X + (previous.X - corner.X) / beforeLength * radius),
                    (float)(corner.Y + (previous.Y - corner.Y) / beforeLength * radius));
                PointF after = new PointF((float)(corner.X + (next.X - corner.X) / afterLength * radius),
                    (float)(corner.Y + (next.Y - corner.Y) / afterLength * radius));
                path.Append(" L ").Append(XY(before)).Append(" C ").Append(XY(corner)).Append(" ").Append(XY(corner)).Append(" ").Append(XY(after));
            }
            path.Append(" L ").Append(XY(points[points.Count - 1]));
            xml.WriteStartElement("path"); Attr(xml, "d", path.ToString()); Attr(xml, "fill", "none");
            Attr(xml, "stroke", color); Attr(xml, "stroke-width", "1.8"); Attr(xml, "stroke-linecap", "round");
            if ((branch == "true" && source.TrueLabel != null) || (branch == "false" && source.FalseLabel != null))
                xml.WriteElementString("title", XmlSafe(source.GetBranchLabel(branch)));
            xml.WriteEndElement();
            PointF tip = points[points.Count - 1], prev = points[points.Count - 2];
            double length = Distance(prev, tip);
            if (length > 0.1)
            {
                double dx = (tip.X - prev.X) / length, dy = (tip.Y - prev.Y) / length;
                Polygon(xml, new[] { tip, new PointF((float)(tip.X - dx * 8 - dy * 3.84), (float)(tip.Y - dy * 8 + dx * 3.84)),
                    new PointF((float)(tip.X - dx * 8 + dy * 3.84), (float)(tip.Y - dy * 8 - dx * 3.84)) }, color, null, 0);
            }
            if (branch == "true" || branch == "false")
            {
                PointF from = points[0], to = points[1];
                double distance = Distance(from, to);
                if (distance < 0.1) return;
                double along = Math.Min(distance * 0.55, 35);
                float x = (float)(from.X + (to.X - from.X) / distance * along);
                float y = (float)(from.Y + (to.Y - from.Y) / distance * along);
                if (Math.Abs(to.X - from.X) > Math.Abs(to.Y - from.Y)) y -= 14; else x += 17;
                string label = source.GetBranchLabel(branch);
                float width = FlowCanvas.GetBranchLabelWidth(label);
                RectangleF bounds = new RectangleF(x - width / 2, y - 10, width, 20);
                Rect(xml, bounds, 7, "#FDFEFF", null, 0);
                SvgText(xml, label, bounds, 11.067f, true,
                    customColor != null ? source.GetBranchColor(branch) : branch == "true" ? "#129178" : "#C95874", null, measure);
            }
        }

        private static void SvgNode(XmlWriter xml, FlowNode node, int index, Graphics measure,FlowAppearance style)
        {
            var design=style.Design;float textScale=design.Scale;RectangleF r = new RectangleF(node.X,node.Y,FlowSizing.Size(node,design).Width,FlowSizing.Size(node,design).Height);
            string accent = TypeColor(node.Kind), border = "#D5DEEA";if(style.Mode==3){Color mapped=style.Kind(node.Kind,ColorTranslator.FromHtml(accent),false);accent=String.Format(Invariant,"#{0:X2}{1:X2}{2:X2}",mapped.R,mapped.G,mapped.B);}
            string fill = "#FFFFFF";
            string caption = FriendlyText(node);
            xml.WriteStartElement("g"); Attr(xml, "id", "node-" + index);
            xml.WriteElementString("title", XmlSafe(caption + (String.IsNullOrEmpty(node.Prompt) ? "" : "\n" + node.Prompt)));
            if (node.Kind == NodeKind.Decision)
                Polygon(xml, new[] { new PointF(r.Left + r.Width / 2, r.Top), new PointF(r.Right, r.Top + r.Height / 2),
                    new PointF(r.Left + r.Width / 2, r.Bottom), new PointF(r.Left, r.Top + r.Height / 2) }, fill, border, 1.15);
            else if (node.Kind == NodeKind.Input || node.Kind == NodeKind.Output)
                Polygon(xml, new[] { new PointF(r.Left + 19, r.Top), new PointF(r.Right, r.Top),
                    new PointF(r.Right - 19, r.Bottom), new PointF(r.Left, r.Bottom) }, fill, border, 1.15);
            else if(node.Kind==NodeKind.Note){xml.WriteStartElement("rect");Attr(xml,"x",F(r.X));Attr(xml,"y",F(r.Y));Attr(xml,"width",F(r.Width));Attr(xml,"height",F(r.Height));Attr(xml,"rx","11");Attr(xml,"fill",fill);Attr(xml,"stroke",border);Attr(xml,"stroke-width","1.15");Attr(xml,"stroke-dasharray","6 4");xml.WriteEndElement();}
            else Rect(xml, r, node.Kind == NodeKind.Start || node.Kind == NodeKind.End ? Math.Min(29,r.Height / 2) : 11, fill, border, 1.15);
            if(node.Kind==NodeKind.Call){foreach(float x in new[]{r.Left+10,r.Right-10}){xml.WriteStartElement("line");Attr(xml,"x1",F(x));Attr(xml,"x2",F(x));Attr(xml,"y1",F(r.Top+2));Attr(xml,"y2",F(r.Bottom-2));Attr(xml,"stroke",border);Attr(xml,"stroke-width","1.15");xml.WriteEndElement();}}

            if (node.Kind == NodeKind.Start || node.Kind == NodeKind.End)
            {
                xml.WriteStartElement("circle"); Attr(xml, "cx", F(r.X + 32*textScale)); Attr(xml, "cy", F(r.Y + r.Height/2)); Attr(xml, "r", F(10*textScale));
                Attr(xml, "fill", accent); Attr(xml, "fill-opacity", "0.08"); xml.WriteEndElement();
                if (node.Kind == NodeKind.Start)
                    Polygon(xml, new[] { new PointF(r.X + 30*textScale, r.Y + r.Height/2-5*textScale), new PointF(r.X + 30*textScale, r.Y + r.Height/2+5*textScale), new PointF(r.X + 37*textScale, r.Y + r.Height/2) }, accent, null, 0);
                else Rect(xml, new RectangleF(r.X + 28*textScale, r.Y + r.Height/2-4*textScale, 8*textScale, 8*textScale), 0, accent, null, 0);
                SvgText(xml, caption, new RectangleF(r.X + 47*textScale, r.Y + 5*textScale, r.Width - 63*textScale, r.Height - 10*textScale), 15.333f*textScale, true, "#232E41", "clip-" + index, measure,design);
            }
            else if (node.Kind == NodeKind.Decision)
            {
                SvgText(xml, "CONDIZIONE", FlowSizing.DecisionTitle(r,design), 10*textScale, true, accent, null, measure,design);
                SvgText(xml, caption, FlowSizing.DecisionBody(r), 14*textScale, false, "#232E41", "clip-" + index, measure,design);
            }
            else
            {
                float inset = (node.Kind == NodeKind.Input || node.Kind == NodeKind.Output ? 30 : 18)*textScale;
                string kind = IoInstruction.IsIo(node.Kind)?(IoInstruction.Separate?(node.Kind==NodeKind.Input?"INPUT":"OUTPUT"):"INPUT / OUTPUT"):Ui.KindName(node.Kind).ToUpperInvariant();
                SvgText(xml, kind, new RectangleF(r.X + inset, r.Y + 14*textScale, r.Width - inset * 2, 14*textScale), 10*textScale, true, accent, null, measure,design);
                SvgText(xml, caption, new RectangleF(r.X + inset, r.Y + 31*textScale, r.Width - inset * 2, r.Height - 43*textScale), 15.333f*textScale, false, "#232E41", "clip-" + index, measure,design);
            }
            xml.WriteEndElement();
        }

        private static void SvgText(XmlWriter xml, string value, RectangleF bounds, float size, bool bold, string color, string clipId, Graphics measure,FlowDesign design=null)
        {
            design=design??FlowDesign.Default;
            if (clipId != null)
            {
                xml.WriteStartElement("defs"); xml.WriteStartElement("clipPath"); Attr(xml, "id", clipId);
                Rect(xml, bounds, 0, null, null, 0); xml.WriteEndElement(); xml.WriteEndElement();
            }
            float lineHeight = size * 1.22f;
            List<string> lines;
            using (Font font = new Font(design.FontName, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                lines = WrapText(XmlSafe(value), font, measure, bounds.Width - 2, Math.Max(1, (int)(bounds.Height / lineHeight)));
            xml.WriteStartElement("text"); Attr(xml, "font-family", design.SvgFamily); Attr(xml, "font-size", F(size));
            Attr(xml, "font-weight", bold ? "600" : "400"); Attr(xml, "fill", color); Attr(xml, "text-anchor", "middle");
            xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            if (clipId != null) Attr(xml, "clip-path", "url(#" + clipId + ")");
            float firstBaseline = bounds.Top + (bounds.Height - lines.Count * lineHeight) / 2 + size * 0.94f;
            for (int i = 0; i < lines.Count; i++)
            {
                xml.WriteStartElement("tspan"); Attr(xml, "x", F(bounds.Left + bounds.Width / 2)); Attr(xml, "y", F(firstBaseline + i * lineHeight));
                xml.WriteString(lines[i]); xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }

        private static List<string> WrapText(string text, Font font, Graphics measure, float width, int maxLines)
        {
            List<string> lines = new List<string>();
            string remaining = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ');
            using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                while (remaining.Length > 0 && lines.Count < maxLines)
                {
                    int newline = remaining.IndexOf('\n');
                    string paragraph = newline < 0 ? remaining : remaining.Substring(0, newline);
                    if (paragraph.Length == 0) { lines.Add(""); remaining = remaining.Substring(1); continue; }
                    int[] elements = FlowText.Elements(paragraph);
                    int lo = 1, hi = elements.Length, fit = 1;
                    while (lo <= hi)
                    {
                        int mid = (lo + hi) / 2;
                        int end = mid == elements.Length ? paragraph.Length : elements[mid];
                        if (FlowText.MeasureLine(measure, paragraph.Substring(0, end), font, format).Width <= width)
                        { fit = mid; lo = mid + 1; } else hi = mid - 1;
                    }
                    int take = fit == elements.Length ? paragraph.Length : elements[fit];
                    if (take < paragraph.Length)
                    {
                        int space = paragraph.LastIndexOf(' ', Math.Max(0, take - 1));
                        if (space > 0) take = space;
                    }
                    lines.Add(paragraph.Substring(0, take).TrimEnd());
                    remaining = remaining.Substring(take);
                    if (take == paragraph.Length && newline >= 0 && remaining.StartsWith("\n", StringComparison.Ordinal)) remaining = remaining.Substring(1);
                    else remaining = remaining.TrimStart(' ');
                }
                if (lines.Count == 0) lines.Add("");
                if (remaining.Length > 0)
                {
                    string last = lines[lines.Count - 1].TrimEnd();
                    while (last.Length > 0 && FlowText.MeasureLine(measure, last + "…", font, format).Width > width)
                    {
                        int[] elements = FlowText.Elements(last);
                        last = last.Substring(0, elements[elements.Length - 1]);
                    }
                    lines[lines.Count - 1] = last + "…";
                }
            }
            return lines;
        }

        private static void Rect(XmlWriter xml, RectangleF r, double radius, string fill, string stroke, double width)
        {
            xml.WriteStartElement("rect"); Attr(xml, "x", F(r.X)); Attr(xml, "y", F(r.Y)); Attr(xml, "width", F(r.Width)); Attr(xml, "height", F(r.Height));
            if (radius > 0) Attr(xml, "rx", F(radius));
            if (fill != null) Attr(xml, "fill", fill);
            if (stroke != null) { Attr(xml, "stroke", stroke); Attr(xml, "stroke-width", F(width)); }
            xml.WriteEndElement();
        }

        private static void Polygon(XmlWriter xml, IEnumerable<PointF> points, string fill, string stroke, double width)
        {
            xml.WriteStartElement("polygon"); Attr(xml, "points", String.Join(" ", points.Select(XY))); Attr(xml, "fill", fill);
            if (stroke != null) { Attr(xml, "stroke", stroke); Attr(xml, "stroke-width", F(width)); }
            xml.WriteEndElement();
        }

        private static string FriendlyText(FlowNode n)
        {
            if (!String.IsNullOrWhiteSpace(FlowSizing.Text(n))) return FlowSizing.Text(n);
            switch (n.Kind)
            {
                case NodeKind.Start: return "Inizio";
                case NodeKind.End: return "Fine";
                case NodeKind.Input: return "Nome variabile";
                case NodeKind.Output: return "Valore da mostrare";
                case NodeKind.Note:return "Scrivi una nota";
                case NodeKind.Decision: return "Scrivi una condizione";
                default: return "Scrivi un'operazione";
            }
        }

        private static string TypeColor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Pause:return "#BE294A";
                case NodeKind.Start: return "#129178";
                case NodeKind.End: return "#66738A";
                case NodeKind.Input: return "#3285BA";
                case NodeKind.Output: return "#775FC3";
                case NodeKind.Note:return "#667085";
                case NodeKind.Decision: return "#AD761E";
                default: return "#3166F1";
            }
        }

        private static string XmlSafe(string text)
        {
            if (String.IsNullOrEmpty(text)) return "";
            StringBuilder b = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (Char.IsHighSurrogate(c) && i + 1 < text.Length && Char.IsLowSurrogate(text[i + 1])) { b.Append(c).Append(text[++i]); }
                else if (Char.IsSurrogate(c) || (!XmlConvert.IsXmlChar(c))) b.Append('\uFFFD');
                else b.Append(c);
            }
            return b.ToString();
        }
        private static double Distance(PointF a, PointF b) { double x = a.X - b.X, y = a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
        private static string XY(PointF p) { return F(p.X) + " " + F(p.Y); }
        private static string F(double number) { return number.ToString("0.####", Invariant); }
        private static void Attr(XmlWriter xml, string name, string value) { xml.WriteAttributeString(name, value); }
    }
}
