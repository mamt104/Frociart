using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed class ExportDialog : Form
    {
        private readonly FlowCanvas canvas;
        private readonly RadioButton pdf = new FlowRadioButton(), svg = new FlowRadioButton(), png = new FlowRadioButton();
        private readonly CheckBox multipage = new FlowCheckBox();
        private readonly Label summary = Ui.Label("", 9.7f, Ui.Muted);
        private readonly FlowButton export;
        private readonly FlowButton cancel;
        private bool working;

        internal string ExportedPath { get; private set; }

        internal ExportDialog(FlowCanvas canvas)
        {
            SuspendLayout();
            if (canvas == null) throw new ArgumentNullException("canvas");
            this.canvas = canvas;
            Text = "Esporta il diagramma — Frociart";
            Size = new Size(650, 420); MinimumSize = new Size(600, 380);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            MinimizeBox = false; MaximizeBox = false; Font = Ui.Font(); BackColor = Ui.Paper;

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                Padding = new Padding(24, 18, 24, 18) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 87));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            Controls.Add(layout);
            Panel heading = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            Label title = Ui.Label("Porta il diagramma con te", 21, null, true);
            title.Dock = DockStyle.Top; title.Height = 36; heading.Controls.Add(title);
            Label intro = Ui.Label("Scegli il formato, poi dove salvare la tua copia.", 9.8f, Ui.Muted);
            intro.Dock = DockStyle.Bottom; intro.Height = 26; heading.Controls.Add(intro);
            layout.Controls.Add(heading, 0, 0);

            TableLayoutPanel formats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            for (int i = 0; i < 3; i++) formats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            formats.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            formats.Controls.Add(FormatCard(pdf, "PDF", "Pronto da stampare\nA4 · alta risoluzione", 0), 0, 0);
            formats.Controls.Add(FormatCard(svg, "SVG", "Grafica vettoriale\nQualità a ogni zoom", 1), 1, 0);
            formats.Controls.Add(FormatCard(png, "PNG", "Immagine da condividere\nSfondo bianco", 2), 2, 0);
            layout.Controls.Add(formats, 0, 1);

            multipage.Text = "Dividi in pagine A4"; multipage.AutoSize = false;
            multipage.Dock = DockStyle.Fill; multipage.Margin = new Padding(0, 6, 0, 1);
            multipage.Font = Ui.Font(9.5f); multipage.ForeColor = Ui.Ink;
            multipage.CheckedChanged += delegate { UpdateSummary(); };
            layout.Controls.Add(multipage, 0, 2);
            summary.Dock = DockStyle.Fill; summary.Margin = new Padding(0, 8, 0, 6);
            layout.Controls.Add(summary, 0, 3);

            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false, Margin = Padding.Empty };
            export = Ui.Button("Esporta…", 128, Export, true); export.Margin = Padding.Empty;
            cancel = Ui.Button("Annulla", 102, delegate { DialogResult = DialogResult.Cancel; Close(); });
            cancel.Margin = new Padding(0, 0, 10, 0);
            footer.Controls.Add(export); footer.Controls.Add(cancel); layout.Controls.Add(footer, 0, 4);
            AcceptButton = export; CancelButton = cancel;
            pdf.Checked = true;
            Shown += delegate { UpdateSummary(); pdf.Focus(); };
            Ui.ApplyTheme(this);
            ResumeLayout(true);
        }

        private Panel FormatCard(RadioButton radio, string label, string description, int tab)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface, Margin = new Padding(0, 0, tab == 2 ? 0 : 10, 3),
                Padding = new Padding(11, 7, 7, 6), Cursor = Cursors.Hand };
            radio.Text = label; radio.Font = Ui.Font(11, FontStyle.Bold); radio.ForeColor = Ui.Ink;
            radio.Dock = DockStyle.Top; radio.Height = 26; radio.TabIndex = tab; radio.Cursor = Cursors.Hand;
            Label sub = Ui.Label(description, 8.6f, Ui.Muted); sub.Dock = DockStyle.Fill; sub.Padding = new Padding(0, 3, 0, 0);
            sub.Cursor = Cursors.Hand; card.Controls.Add(sub); card.Controls.Add(radio);
            card.Resize += delegate { card.Invalidate(); };
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (card.Width < 5 || card.Height < 5) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Ui.Rounded(new RectangleF(0.8f, 0.8f, card.Width - 1.7f, card.Height - 1.7f), 9))
                using (Pen pen = new Pen(radio.Checked ? Ui.Blue : Ui.Line, radio.Checked ? 1.5f : 1)) e.Graphics.DrawPath(pen, path);
            };
            Action choose = delegate { radio.Checked = true; radio.Focus(); };
            card.Click += delegate { choose(); }; sub.Click += delegate { choose(); };
            radio.CheckedChanged += delegate
            {
                if (!radio.Checked) { card.Invalidate(); return; }
                // The radios live in separate cards, so keep their group explicit.
                if (radio != pdf) pdf.Checked = false;
                if (radio != svg) svg.Checked = false;
                if (radio != png) png.Checked = false;
                card.Invalidate(); UpdateSummary();
            };
            return card;
        }

        private void UpdateSummary()
        {
            if (export == null || working) return;
            multipage.Visible = pdf.Checked;
            summary.ForeColor = Ui.Muted; export.Enabled = true;
            if (canvas.Diagram == null || canvas.Diagram.Nodes == null || canvas.Diagram.Nodes.Count == 0)
            { summary.Text = "Aggiungi almeno un blocco prima di esportare."; summary.ForeColor = Ui.Amber; export.Enabled = false; return; }
            try
            {
                if (pdf.Checked)
                {
                    PdfExportPlan p = DiagramExport.GetPdfPlan(canvas, multipage.Checked);
                    if (!p.CanExport) { summary.Text = p.Problem; summary.ForeColor = Ui.Amber; export.Enabled = false; return; }
                    string direction = p.Landscape ? "orizzontale" : "verticale";
                    summary.Text = (p.PageCount == 1 ? "Una pagina A4 " : p.PageCount + " pagine A4, orientamento ") + direction +
                        ". Testo dei blocchi: circa " + p.BodyTextPoints.ToString("0.#", CultureInfo.CurrentCulture) + " pt.";
                    if (p.PageCount > 1)
                        summary.Text += "\nLeggi le pagine per righe, da sinistra a destra. Una piccola sovrapposizione aiuta a seguire i collegamenti.";
                    else if (p.BodyTextPoints < 7.5)
                    { summary.Text += "\nIl disegno sarà piccolo: attiva la suddivisione in pagine per leggere meglio i blocchi."; summary.ForeColor = Ui.Amber; }
                    else summary.Text += "\nPDF ad alta risoluzione, pronto per la stampa e la condivisione.";
                }
                else if (svg.Checked)
                    summary.Text = "Blocchi, frecce e testi vettoriali: puoi ingrandirli senza perdere nitidezza.\nApri il file in un browser o in un programma di grafica.";
                else summary.Text = "Immagine PNG ad alta risoluzione su sfondo bianco.\nIdeale per inserire il diagramma in documenti, slide e messaggi.";
            }
            catch (Exception ex)
            { summary.Text = ex.Message; summary.ForeColor = Ui.Amber; export.Enabled = false; }
        }

        private void Export()
        {
            if (working) return;
            string extension = pdf.Checked ? "pdf" : svg.Checked ? "svg" : "png";
            using (SaveFileDialog save = new SaveFileDialog { Title = "Esporta il diagramma", AddExtension = true,
                DefaultExt = extension, Filter = extension.ToUpperInvariant() + " (*." + extension + ")|*." + extension,
                FileName = SafeFileName(canvas.Diagram.Title) + "." + extension, OverwritePrompt = true,
                RestoreDirectory = true, CheckPathExists = true })
            {
                if (save.ShowDialog(this) != DialogResult.OK) return;
                working = true; UseWaitCursor = true; export.Enabled = false; cancel.Enabled = false;
                summary.ForeColor = Ui.Muted; summary.Text = "Esportazione in corso…"; summary.Refresh();
                try
                {
                    if (extension == "pdf") DiagramExport.ExportPdf(canvas, save.FileName, multipage.Checked);
                    else if (extension == "svg") DiagramExport.ExportSvg(canvas, save.FileName);
                    else DiagramExport.ExportPng(canvas, save.FileName);
                    ExportedPath = Path.GetFullPath(save.FileName); DialogResult = DialogResult.OK; Close();
                }
                catch (Exception ex)
                { FlowMessageDialog.Show(this, "Non è stato possibile esportare il diagramma.\n\n" + ex.Message, "Esportazione — Frociart", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally
                {
                    working = false; UseWaitCursor = false; cancel.Enabled = true;
                    if (!IsDisposed) UpdateSummary();
                }
            }
        }

        internal static string SafeFileName(string title)
        {
            string name = new string((title ?? "").Where(c => c >= 32 && "<>:\"/\\|?*".IndexOf(c) < 0 && !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim().TrimEnd('.');
            if (String.IsNullOrWhiteSpace(name)) name = "Diagramma";
            if (name.Length > 100) name = name.Substring(0, 100).TrimEnd(' ', '.');
            if (name.Length > 0 && Char.IsHighSurrogate(name[name.Length - 1])) name = name.Substring(0, name.Length - 1);
            if (name.Length == 0) name = "Diagramma";
            string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '1' && stem[3] <= '9'))
                name = "Diagramma-" + name;
            return name;
        }
    }
}
