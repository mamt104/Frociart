using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
    // A private snapshot is shared by preview and print, never the editor's live canvas.
    internal sealed class DiagramPrintJob : IDisposable {
        private readonly FlowCanvas renderer;
        internal PdfExportPlan Plan { get; private set; }
        internal string Title { get { return renderer.Diagram.Title ?? "Diagramma"; } }
        internal DiagramPrintJob(FlowCanvas canvas) {
            if(canvas==null)throw new ArgumentNullException("canvas");
            renderer=new FlowCanvas();
            try { renderer.Diagram=DiagramIO.Clone(canvas.Diagram);renderer.SetAppearance(canvas.AppearanceStyle); SetMultipage(false); }
            catch { renderer.Dispose(); throw; }
        }
        internal void SetMultipage(bool tiled) { Plan=DiagramExport.GetPdfPlan(renderer,tiled); }
        internal Bitmap Render(int page) {
            if(!Plan.CanExport)throw new InvalidOperationException(Plan.Problem);
            if(page<0||page>=Plan.PageCount)throw new ArgumentOutOfRangeException("page");
            return DiagramExport.RenderPdfPage(renderer,Plan,page);
        }
        // All coordinates use hundredths of an inch, including asymmetric hardware margins.
        internal static RectangleF Fit(RectangleF area,double width,double height) {
            if(area.Width<=0||area.Height<=0||width<=0||height<=0)throw new InvalidOperationException("L'area stampabile non è valida.");
            double scale=Math.Min(area.Width/width,area.Height/height);
            float w=(float)(width*scale),h=(float)(height*scale);
            return new RectangleF(area.X+(area.Width-w)/2,area.Y+(area.Height-h)/2,w,h);
        }
        internal PrintDocument CreateDocument() {
            if(!Plan.CanExport)throw new InvalidOperationException(Plan.Problem);
            var plan=Plan;
            var doc=new PrintDocument { DocumentName=Title.Replace('\r',' ').Replace('\n',' '),OriginAtMargins=false };
            doc.DefaultPageSettings.Landscape=plan.Landscape;
            doc.DefaultPageSettings.Margins=new Margins(0,0,0,0);
            // Prefer the driver's actual A4 definition, retaining support for another selected paper.
            if(doc.PrinterSettings.IsValid) {
                var a4=doc.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(x=>x.Kind==PaperKind.A4);
                if(a4!=null)doc.DefaultPageSettings.PaperSize=a4;
            }
            doc.PrinterSettings.MinimumPage=1;doc.PrinterSettings.MaximumPage=plan.PageCount;
            doc.PrinterSettings.FromPage=1;doc.PrinterSettings.ToPage=plan.PageCount;
            int next=0,last=plan.PageCount-1;
            doc.BeginPrint+=delegate {
                next=0;last=plan.PageCount-1;
                if(doc.PrinterSettings.PrintRange==PrintRange.SomePages) {
                    next=doc.PrinterSettings.FromPage-1;last=doc.PrinterSettings.ToPage-1;
                    if(next<0||last<next||last>=plan.PageCount)throw new InvalidOperationException("Scegli un intervallo di pagine valido.");
                }
            };
            doc.PrintPage+=delegate(object sender,PrintPageEventArgs e) {
                using(var bitmap=DiagramExport.RenderPdfPage(renderer,plan,next)) {
                    var area=RectangleF.Intersect(e.PageSettings.PrintableArea,new RectangleF(0,0,e.PageBounds.Width,e.PageBounds.Height));
                    var target=Fit(area,plan.PageWidth,plan.PageHeight);
                    var state=e.Graphics.Save();
                    try {
                        e.Graphics.PageUnit=GraphicsUnit.Display;
                        e.Graphics.TranslateTransform(-e.PageSettings.HardMarginX,-e.PageSettings.HardMarginY);
                        e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                        e.Graphics.DrawImage(bitmap,target);
                    } finally {e.Graphics.Restore(state);}
                }
                next++;e.HasMorePages=next<=last;
            };
            return doc;
        }
        public void Dispose() {renderer.Dispose();}
    }

    internal sealed class DiagramPrintDialog : Form {
        private readonly DiagramPrintJob job;
        private readonly FlowScrollPanel viewport=new FlowScrollPanel {Dock=DockStyle.Fill,AutoScroll=true,Margin=new Padding(0,8,0,8)};
        private readonly PictureBox picture=new PictureBox {SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White};
        private readonly FlowCheckBox multipage=new FlowCheckBox {Text="Più pagine leggibili",AutoSize=false,Width=190,Height=30,AccessibleName="Dividi il diagramma in pagine leggibili"};
        private readonly Label summary=Ui.Label("",9,Ui.Muted),pageLabel=Ui.Label("",9,Ui.Ink,true);
        private readonly ComboBox zoom=new FlowComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=132};
        private readonly FlowButton previous,next,print;
        private int page;
        internal DiagramPrintDialog(FlowCanvas canvas) {
            job=new DiagramPrintJob(canvas);
            Text="Stampa il diagramma — Frociart";Size=new Size(900,780);MinimumSize=new Size(720,620);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;Font=Ui.Font();
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(20,14,20,14)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(float h in new[]{40f,40f,0f,62f,40f})layout.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));
            Controls.Add(layout);
            var heading=Ui.Label("Anteprima di stampa",20,null,true);heading.Dock=DockStyle.Fill;layout.Controls.Add(heading,0,0);
            var tools=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            multipage.Name="PrintMultipage";multipage.Margin=new Padding(0,8,18,0);tools.Controls.Add(multipage);
            var zlabel=Ui.Label("Zoom",9);zlabel.AutoSize=true;zlabel.Margin=new Padding(0,9,6,0);tools.Controls.Add(zlabel);
            zoom.Name="PrintZoom";zoom.AccessibleName="Zoom dell'anteprima";zoom.Items.AddRange(new object[]{"Adatta pagina","100%","150%"});zoom.SelectedIndex=0;zoom.Margin=new Padding(0,5,0,0);tools.Controls.Add(zoom);layout.Controls.Add(tools,0,1);
            picture.AccessibleName="Anteprima della pagina";viewport.Controls.Add(picture);layout.Controls.Add(viewport,0,2);
            summary.Name="PrintSummary";summary.Dock=DockStyle.Fill;summary.Margin=new Padding(0,3,0,3);layout.Controls.Add(summary,0,3);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            previous=Ui.Button("‹",40,()=>ShowPage(page-1));previous.Name="PrintPrevious";previous.AccessibleName="Pagina precedente";
            next=Ui.Button("›",40,()=>ShowPage(page+1));next.Name="PrintNext";next.AccessibleName="Pagina successiva";
            pageLabel.Name="PrintPageLabel";pageLabel.Width=105;pageLabel.Height=34;pageLabel.TextAlign=ContentAlignment.MiddleCenter;
            print=Ui.Button("Stampa…",124,Print,true);print.Name="PrintSubmit";
            var close=Ui.Button("Chiudi",90,Close);CancelButton=close;
            footer.Controls.AddRange(new Control[]{previous,pageLabel,next,print,close});layout.Controls.Add(footer,0,4);
            multipage.CheckedChanged+=delegate {Reload();};zoom.SelectedIndexChanged+=delegate {SizePreview();};viewport.Resize+=delegate {SizePreview();};
            Ui.ApplyTheme(this);
            // Large charts start in readable tiles, while small ones retain a single page.
            multipage.Checked=job.Plan.BodyTextPoints<7.5;
            Shown+=delegate {Reload();multipage.Focus();};
        }
        private void Reload() {
            try {
                job.SetMultipage(multipage.Checked);page=0;
                var p=job.Plan;
                summary.Text=p.CanExport?(p.PageCount+" pagin"+(p.PageCount==1?"a":"e")+" A4 · testo circa "+p.BodyTextPoints.ToString("0.#",CultureInfo.CurrentCulture)+" pt.\n"+
                    (p.PageCount>1?"Segui le pagine per righe, da sinistra a destra. ":"")+"La stampante adatta l'anteprima al foglio e ai suoi margini."):p.Problem;
                summary.ForeColor=!p.CanExport||p.BodyTextPoints<7.5?Ui.Amber:Ui.Muted;
                print.Enabled=p.CanExport;
                if(p.CanExport)ShowPage(0);else {ReplaceImage(null);pageLabel.Text="—";previous.Enabled=next.Enabled=false;}
            } catch(Exception ex) {summary.Text=ex.Message;summary.ForeColor=Ui.Amber;print.Enabled=false;ReplaceImage(null);previous.Enabled=next.Enabled=false;}
        }
        private void ShowPage(int index) {
            if(index<0||index>=job.Plan.PageCount)return;
            try {
                var image=job.Render(index);FlowTransitions.Change(viewport);ReplaceImage(image);page=index;
                pageLabel.Text=(page+1)+" / "+job.Plan.PageCount;picture.AccessibleDescription="Pagina "+pageLabel.Text;
                previous.Enabled=page>0;next.Enabled=page+1<job.Plan.PageCount;
            } catch(Exception ex) {summary.Text="Anteprima non disponibile: "+ex.Message;summary.ForeColor=Ui.Amber;print.Enabled=false;}
        }
        private void ReplaceImage(Image image) {var old=picture.Image;picture.Image=image;if(old!=null)old.Dispose();viewport.AutoScrollPosition=Point.Empty;SizePreview();}
        private void SizePreview() {
            if(picture.Image==null)return;
            viewport.AutoScrollPosition=Point.Empty;viewport.AutoScrollMinSize=Size.Empty;
            float scale=zoom.SelectedIndex==0?Math.Min(Math.Max(1,viewport.ClientSize.Width-32)/(float)picture.Image.Width,Math.Max(1,viewport.ClientSize.Height-32)/(float)picture.Image.Height):
                (zoom.SelectedIndex==2?1.5f:1f)*DeviceDpiScale()/DiagramExport.PdfDpi;
            var size=new Size(Math.Max(1,(int)(picture.Image.Width*scale)),Math.Max(1,(int)(picture.Image.Height*scale)));
            picture.Bounds=new Rectangle(Math.Max(12,(viewport.ClientSize.Width-size.Width)/2),12,size.Width,size.Height);
            viewport.AutoScrollMinSize=new Size(size.Width+24,size.Height+24);
        }
        private float DeviceDpiScale() {using(var g=CreateGraphics())return g.DpiX;}
        private void Print() {
            try {
                using(var doc=job.CreateDocument())using(var choose=new PrintDialog {Document=doc,UseEXDialog=true,AllowSomePages=job.Plan.PageCount>1,AllowSelection=false,AllowCurrentPage=false}) {
                    if(choose.ShowDialog(this)!=DialogResult.OK)return;
                    if(!doc.PrinterSettings.IsValid)throw new InvalidOperationException("Scegli una stampante disponibile oppure esporta in PDF.");
                    bool cancelled=false;doc.EndPrint+=delegate(object sender,PrintEventArgs e){cancelled=e.Cancel;};
                    doc.PrintController=new PrintControllerWithStatusDialog(new StandardPrintController(),"Stampa · Frociart");
                    doc.Print();summary.Text=cancelled?"Stampa annullata. Il diagramma è invariato.":"Richiesta inviata alla stampante. Puoi chiudere l'anteprima.";
                }
            } catch(Exception) {FlowMessageDialog.Show(this,"Non riesco a stampare. Controlla la stampante o usa Esporta → PDF.","Stampa non riuscita",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        protected override void Dispose(bool disposing) {if(disposing){var old=picture.Image;picture.Image=null;if(old!=null)old.Dispose();job.Dispose();}base.Dispose(disposing);}
    }
}
