using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
    // Captured once on the UI thread. A report never runs or modifies the program.
    internal sealed class ExecutionSnapshot {
        internal sealed class Row { internal string Name,Value,Type;internal bool Heading;internal string DisplayName {get{return Name+(String.IsNullOrEmpty(Type)?"":"\n"+Type);}} }
        internal readonly List<Row> Rows=new List<Row>();
        internal readonly string Title,Status;
        internal ExecutionSnapshot(Diagram diagram,Interpreter runner) {
            Title=diagram.Title??"Diagramma";
            Status=runner==null?"Esecuzione non avviata":runner.State==RuntimeState.Completed?"Esecuzione completata · "+runner.Steps+" passi":runner.State==RuntimeState.Error?"Esecuzione interrotta · "+runner.Error:runner.State==RuntimeState.WaitingForInput?"In attesa di "+runner.InputVariable:"Pausa · passo "+runner.Steps;
            var types=runner==null?new Dictionary<string,string>():runner.VariableTypes;
            Rows.Add(new Row{Name="VARIABILI",Value="",Heading=true});
            if(runner==null||runner.Variables.Count==0)Rows.Add(new Row{Name="",Value="Nessuna variabile ancora."});
            else foreach(var item in runner.Variables.OrderBy(v=>v.Key,StringComparer.OrdinalIgnoreCase)) {
                var vector=item.Value as VectorValue;
                if(vector==null)Rows.Add(new Row{Name=item.Key,Type=types[item.Key],Value=ValueFormatter.Format(item.Value)});
                else {Rows.Add(new Row{Name=item.Key,Type=types[item.Key],Value="Vettore · "+vector.Count+" elementi"});for(int i=0;i<vector.Count;i++)Rows.Add(new Row{Name="["+i+"]",Value=ValueFormatter.Format(vector.At(i))});}
            }
            Rows.Add(new Row{Name="RISULTATO",Value="",Heading=true});
            if(runner==null||runner.Output.Count==0)Rows.Add(new Row{Name="",Value="Nessun risultato prodotto."});
            else foreach(string line in runner.OutputText.Replace("\r\n","\n").Replace("\r","\n").Split('\n'))Rows.Add(new Row{Name="",Value=line.Length==0?"(riga vuota)":line});
            if(runner!=null&&(runner.ConsoleLog.Discarded>0||runner.ConsoleLog.Snapshot().Any(e=>e.Kind==FlowConsoleLog.EntryKind.Input)))
            {
                Rows.Add(new Row{Name="VALORI INSERITI",Value="",Heading=true});
                if(runner.ConsoleLog.Discarded>0)Rows.Add(new Row{Name="",Value="Cronologia limitata: "+runner.ConsoleLog.Discarded+" messaggi precedenti rimossi."});
                if(!runner.ConsoleLog.Snapshot().Any(e=>e.Kind==FlowConsoleLog.EntryKind.Input))Rows.Add(new Row{Name="",Value="Non restano valori inseriti nella cronologia conservata."});
                foreach(var entry in runner.ConsoleLog.Snapshot().Where(e=>e.Kind==FlowConsoleLog.EntryKind.Input))Rows.Add(new Row{Name=entry.Variable,Type=entry.Context,Value=(entry.Text.Length==0?"(testo vuoto)":entry.Text)+(entry.TextLimited?"\n[Testo limitato]":"")});
            }
        }
        internal Bitmap Render(FlowCanvas canvas) {
            RectangleF bounds=canvas.GetExportContentBounds();if(bounds.IsEmpty)bounds=new RectangleF(0,0,500,320);bounds.Inflate(32,32);
            const float scale=1.35f;int diagramWidth=Math.Max(720,(int)Math.Ceiling(bounds.Width*scale)),width=diagramWidth+560;
            if(width>4096)throw new InvalidOperationException("Il diagramma è troppo largo per un'immagine leggibile. Avvicina i blocchi o usa Riordina e riprova.");
            using(var probe=new Bitmap(1,1))using(var g=Graphics.FromImage(probe))using(var text=new Font("Segoe UI",21,FontStyle.Regular,GraphicsUnit.Pixel))using(var bold=new Font("Segoe UI",21,FontStyle.Bold,GraphicsUnit.Pixel))using(var mono=new Font("Consolas",21,FontStyle.Regular,GraphicsUnit.Pixel))using(var titleFont=new Font("Segoe UI",32,FontStyle.Bold,GraphicsUnit.Pixel))using(var format=new StringFormat(StringFormat.GenericTypographic)) {
                format.FormatFlags=StringFormatFlags.LineLimit;format.Trimming=StringTrimming.None;
                int titleH=(int)Math.Ceiling(g.MeasureString(Title,titleFont,width-96,format).Height),statusH=(int)Math.Ceiling(g.MeasureString(Status,text,width-96,format).Height);
                int top=96+titleH+statusH,rowsHeight=0;var heights=new List<int>();
                foreach(Row row in Rows){int h=row.Heading?58:Math.Max(38,(int)Math.Ceiling(Math.Max(g.MeasureString(row.DisplayName,bold,158,format).Height,g.MeasureString(row.Value,mono,row.Name.Length==0?464:282,format).Height))+20);heights.Add(h);rowsHeight+=h;if(rowsHeight>11000)throw new InvalidOperationException("I dati sono troppo lunghi per un singolo PNG leggibile. Apri una variabile per leggerla e copiarla per intero, oppure esporta il diagramma in SVG.");}
                int height=top+Math.Max((int)Math.Ceiling(bounds.Height*scale),rowsHeight)+72;
                if(height>12000||(long)width*height>16000000)throw new InvalidOperationException("Il diagramma e i dati superano le dimensioni di un singolo PNG leggibile. Riduci il contenuto della prova o esporta il diagramma in SVG.");
                var image=new Bitmap(width,height,PixelFormat.Format32bppArgb);
                try{using(var c=Graphics.FromImage(image))using(var ink=new SolidBrush(Color.FromArgb(29,35,48)))using(var muted=new SolidBrush(Color.FromArgb(80,91,110)))using(var blue=new SolidBrush(Color.FromArgb(40,103,210)))using(var card=new SolidBrush(Color.FromArgb(245,247,251)))using(var line=new Pen(Color.FromArgb(220,226,237))) {
                    c.Clear(Color.White);c.SmoothingMode=SmoothingMode.AntiAlias;c.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                    c.DrawString("FROCIART  /  DIAGRAMMA E DATI",bold,blue,48,24);c.DrawString(Title,titleFont,ink,new RectangleF(48,64,width-96,titleH+2),format);c.DrawString(Status,text,muted,new RectangleF(48,74+titleH,width-96,statusH+2),format);
                    var state=c.Save();c.TranslateTransform(24+(diagramWidth-48-bounds.Width*scale)/2,top);c.ScaleTransform(scale,scale);c.TranslateTransform(-bounds.Left,-bounds.Top);canvas.RenderExport(c,bounds,scale);c.Restore(state);
                    float x=diagramWidth+24,y=top;c.FillRectangle(card,x-16,top-8,512,rowsHeight+16);
                    for(int i=0;i<Rows.Count;i++){Row row=Rows[i];int h=heights[i];if(row.Heading)c.DrawString(row.Name,bold,blue,new RectangleF(x,y+16,464,h-16),format);else {if(row.Name.Length>0)c.DrawString(row.DisplayName,bold,ink,new RectangleF(x,y+8,158,h-10),format);c.DrawString(row.Value,mono,ink,new RectangleF(x+(row.Name.Length==0?0:182),y+8,row.Name.Length==0?464:282,h-10),format);c.DrawLine(line,x,y+h-1,x+464,y+h-1);}y+=h;}
                    c.DrawString("Frociart · realizzato da mamt104",text,muted,48,height-42);
                }return image;}catch{image.Dispose();throw;}
            }
        }
        internal static void Save(Bitmap image,string path) {
            string full=Path.GetFullPath(path),temp=Path.Combine(Path.GetDirectoryName(full),".frociart-"+Guid.NewGuid().ToString("N")+".tmp");
            try{image.Save(temp,ImageFormat.Png);if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    internal sealed class SnapshotDialog:Form {
        internal SnapshotDialog(Bitmap image,string title) {
            Text="Vista da condividere — Frociart";Size=new Size(1100,800);MinimumSize=new Size(700,480);StartPosition=FormStartPosition.CenterParent;BackColor=Ui.Paper;
            var tools=new FlowLayoutPanel{Dock=DockStyle.Top,Height=58,Padding=new Padding(10),WrapContents=false};
            var scroll=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Ui.Paper};var preview=new PictureBox{Image=image,SizeMode=PictureBoxSizeMode.Zoom};scroll.Controls.Add(preview);bool actual=false;
            Action layout=()=>{float ratio=actual?1:Math.Min(1,(scroll.ClientSize.Width-24f)/image.Width);preview.Size=new Size((int)(image.Width*ratio),(int)(image.Height*ratio));};scroll.Resize+=delegate{layout();};
            tools.Controls.Add(Ui.Button("Salva PNG…",135,()=>{using(var save=new SaveFileDialog{Filter="Immagine PNG|*.png",DefaultExt="png",AddExtension=true,FileName=ExportDialog.SafeFileName(title)+"_diagramma_e_dati.png",OverwritePrompt=true,RestoreDirectory=true})if(save.ShowDialog(this)==DialogResult.OK)try{ExecutionSnapshot.Save(image,save.FileName);}catch(Exception e){FlowMessageDialog.Show(this,e.Message,"Immagine non salvata",MessageBoxButtons.OK,MessageBoxIcon.Error);}},true));
            var zoom=Ui.Button("Dimensione reale",170,()=>{actual=!actual;layout();});tools.Controls.Add(zoom);tools.Controls.Add(Ui.Label("Diagramma e dati della prova, senza menu.",10,Ui.Muted));Controls.Add(scroll);Controls.Add(tools);Shown+=delegate{layout();};Ui.ApplyTheme(this);
        }
    }
    internal sealed partial class MainForm {
        private void ShowExecutionSnapshot(){PauseRun();CommitInspector();try{var snapshot=new ExecutionSnapshot(Document,Runner);using(var image=snapshot.Render(Canvas))using(var dialog=new SnapshotDialog(image,Document.Title))dialog.ShowDialog(this);}catch(Exception ex){if(!(ex is InvalidOperationException)&&!(ex is OutOfMemoryException))throw;ShowError("Vista da condividere",ex);}}
        private void ShowVariableValue(){if(Runner==null||variables.SelectedItems.Count==0)return;ShowVariableInspector(variables.SelectedItems[0].Text); }
    }
}
