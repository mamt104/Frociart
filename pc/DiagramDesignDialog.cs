using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
    internal sealed class DiagramDesignDialog : Form {
        private readonly FlowCanvas preview=new FlowCanvas{Dock=DockStyle.Fill,ViewOnly=true};
        private readonly ComboBox family=new FlowComboBox{Name="DesignFamily"},size=new FlowComboBox{Name="DesignSize"};
        private readonly TextBox[] colors=new TextBox[6];
        private readonly FlowButton[] swatches=new FlowButton[6];
        private readonly Label error=Ui.Label("",9,Ui.Red);
        private readonly Timer refresh=new Timer{Interval=120};
        private readonly FlowButton apply;
        private bool loading;
        internal FlowDesign Design {get;private set;}
        internal Action<FlowDesign> ApplyDesign;
        internal DiagramDesignDialog(Diagram diagram,FlowDesign initial) {
            SuspendLayout();Name="DiagramDesignDialog";Text="Personalizza il diagramma — Frociart";Font=Ui.Font();BackColor=Ui.Surface;
            StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=MaximizeBox=false;MinimumSize=new Size(720,600);ClientSize=new Size(850,620);
            var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(20)};
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,94));
            var help=Ui.Label("Font e colori dei titoli · le caselle mantengono lo sfondo neutro.\nSe cambi il carattere o la dimensione, adatto anche la spaziatura. Puoi annullare con Ctrl+Z.",10,Ui.Muted);help.AutoSize=true;help.Margin=new Padding(0,0,0,16);shell.Controls.Add(help);
            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,300));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var scroll=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,Margin=new Padding(0,0,16,0)};
            var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,Margin=Padding.Empty};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,38));
            family.Items.AddRange(FlowDesign.Families);family.AccessibleName="Carattere dei blocchi";family.Dock=DockStyle.Fill;
            for(int p=80;p<=200;p+=10)size.Items.Add(p+"%");size.AccessibleName="Dimensione del testo dei blocchi";size.Dock=DockStyle.Fill;
            AddField(fields,"Carattere",family,0);AddField(fields,"Dimensione",size,1);
            for(int i=0;i<colors.Length;i++){int role=i;var label=Ui.Label(FlowDesign.Roles[i],9);label.Dock=DockStyle.Fill;label.TextAlign=ContentAlignment.MiddleLeft;fields.Controls.Add(label,0,i+2);
                var input=Ui.TextBox();input.Name="DesignColor"+i;input.MaxLength=7;input.AccessibleName="Colore: "+FlowDesign.Roles[i];input.Dock=DockStyle.Fill;colors[i]=input;fields.Controls.Add(input,1,i+2);
                var pick=Ui.Button("…",34,delegate{PreviewRole(role);using(var chooser=new ColorDialog{FullOpen=true,AnyColor=true}){try{chooser.Color=ColorTranslator.FromHtml(FlowDesign.Normalize(input.Text));}catch(ArgumentException){}if(chooser.ShowDialog(this)==DialogResult.OK)input.Text=String.Format("#{0:X2}{1:X2}{2:X2}",chooser.Color.R,chooser.Color.G,chooser.Color.B);}});pick.Text="";pick.Primary=true;pick.AccessibleName="Scegli colore: "+FlowDesign.Roles[role];pick.Dock=DockStyle.Fill;swatches[i]=pick;fields.Controls.Add(pick,2,i+2);fields.RowStyles.Add(new RowStyle(SizeType.Absolute,44));input.TextChanged+=delegate{QueuePreview();};input.Enter+=delegate{PreviewRole(role);};}
            scroll.Controls.Add(fields);body.Controls.Add(scroll,0,0);preview.Name="DesignPreview";preview.AccessibleName="Anteprima dell’aspetto dei blocchi";preview.Diagram=new Diagram();preview.Diagram.Nodes.Add(new FlowNode{Id="style-preview",Kind=NodeKind.Process,Text="totale = a + b",X=48,Y=48});body.Controls.Add(preview,1,0);shell.Controls.Add(body,0,1);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Margin=Padding.Empty};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));error.Dock=DockStyle.Fill;footer.Controls.Add(error,0,0);footer.SetColumnSpan(error,3);
            var reset=Ui.Button("Ripristina",110,delegate{LoadDesign(FlowDesign.Default);});reset.Dock=DockStyle.Left;footer.Controls.Add(reset,0,1);
            var cancel=Ui.Button("Annulla",100,delegate{DialogResult=DialogResult.Cancel;});cancel.Dock=DockStyle.Fill;footer.Controls.Add(cancel,1,1);CancelButton=cancel;
            apply=Ui.Button("Applica",110,delegate{if(UpdatePreview())try{if(ApplyDesign!=null)ApplyDesign(Design);DialogResult=DialogResult.OK;}catch(FlowException ex){error.Text=ex.Message;error.ForeColor=Ui.Red;}},true);apply.Name="DesignApply";apply.Dock=DockStyle.Fill;footer.Controls.Add(apply,2,1);AcceptButton=apply;shell.Controls.Add(footer,0,2);Controls.Add(shell);
            family.SelectedIndexChanged+=delegate{QueuePreview();};size.SelectedIndexChanged+=delegate{QueuePreview();};refresh.Tick+=delegate{refresh.Stop();UpdatePreview();};LoadDesign(initial);
            Ui.ApplyTheme(this);FlowFormMotion.Attach(this);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ResumeLayout(true);Shown+=delegate{var area=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(MinimumSize.Width,Math.Max(200,area.Width-32)),Math.Min(MinimumSize.Height,Math.Max(200,area.Height-32)));Width=Math.Min(Width,area.Width-32);Height=Math.Min(Height,area.Height-32);Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width));Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height));UpdatePreview();preview.FitToView();};
        }
        private static void AddField(TableLayoutPanel fields,string name,Control input,int row){var label=Ui.Label(name,9);label.Dock=DockStyle.Fill;label.TextAlign=ContentAlignment.MiddleLeft;fields.Controls.Add(label,0,row);fields.Controls.Add(input,1,row);fields.SetColumnSpan(input,2);fields.RowStyles.Add(new RowStyle(SizeType.Absolute,44));}
        private void LoadDesign(FlowDesign design){loading=true;family.SelectedIndex=design.Family;size.SelectedIndex=(design.Percent-80)/10;for(int i=0;i<colors.Length;i++)colors[i].Text=design.Hex(i);loading=false;UpdatePreview();}
        private void PreviewRole(int role){var node=preview.Diagram.Nodes[0];node.Kind=new[]{NodeKind.Process,NodeKind.Input,NodeKind.Output,NodeKind.Decision,NodeKind.Start,NodeKind.End}[role];node.Text=new[]{"totale = a + b","numero","numero","numero > 0","Inizio","Fine"}[role];preview.Invalidate();preview.FitToView();}
        private void QueuePreview(){if(loading)return;refresh.Stop();refresh.Start();}
        private bool UpdatePreview(){try{var next=new FlowDesign(family.SelectedIndex,80+size.SelectedIndex*10,colors.Select(c=>c.Text).ToArray());bool reflow=Design==null||Design.FontKey!=next.FontKey;if(Design!=null)FlowTransitions.CanvasChange(preview);Design=next;preview.SetAppearance(new FlowAppearance(3,next));if(reflow){FlowLayout.Arrange(preview.Diagram,next);preview.FitToView();}for(int i=0;i<colors.Length;i++){swatches[i].Accent=ColorTranslator.FromHtml(next.Hex(i));swatches[i].Invalidate();}error.Text="I colori troppo chiari o scuri vengono adattati per restare leggibili.";error.ForeColor=Ui.Muted;apply.Enabled=true;return true;}catch(ArgumentException ex){error.Text=ex.Message;error.ForeColor=Ui.Red;apply.Enabled=false;return false;}}
        protected override void Dispose(bool disposing){if(disposing)refresh.Dispose();base.Dispose(disposing);}
    }
}
