using System;
using System.Drawing;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed class BranchContinuationDialog : Form
    {
        internal string Branch {get;private set;}private bool explicitCancel,keepPlacement;
        internal static string Choose(IWin32Window owner,FlowNode node,string message,string title,bool detaching=false,bool allowTrue=true,bool allowFalse=true,bool keepPlacementOnClose=false){using(var dialog=new BranchContinuationDialog(node,message,title,detaching,allowTrue,allowFalse,keepPlacementOnClose)){return dialog.ShowDialog(owner)==DialogResult.OK?dialog.Branch:null;}}
        internal BranchContinuationDialog(FlowNode node,string message,string title,bool detaching=false,bool allowTrue=true,bool allowFalse=true,bool keepPlacementOnClose=false){
            keepPlacement=keepPlacementOnClose;SuspendLayout();Text=title+" — Frociart";StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;ClientSize=new Size(580,476);BackColor=Ui.Surface;ForeColor=Ui.Ink;Font=Ui.Font();
            var heading=Ui.Label(title,17,null,true);heading.SetBounds(24,20,532,36);Controls.Add(heading);
            var help=Ui.Label(message,9,Ui.Muted);help.SetBounds(25,68,530,56);Controls.Add(help);
            AddChoice(node.GetBranchLabel("true"),"true",node.GetBranchColor("true"),detaching?"Ricuce il punto precedente verso questo ramo.":"Questo ramo continua verso il blocco successivo.",138,72,allowTrue);
            AddChoice(node.GetBranchLabel("false"),"false",node.GetBranchColor("false"),detaching?"Ricuce il punto precedente verso questo ramo.":"Questo ramo continua verso il blocco successivo.",220,72,allowFalse);
            AddChoice("Nessuno","none",null,detaching?"Lascia liberi i collegamenti precedenti. Tutte le caselle restano sul foglio.":"Entrambi i rami restano liberi; il blocco successivo resta sul foglio.",302,72);
            var separator=new Panel{BackColor=Ui.Line};separator.SetBounds(24,395,532,1);Controls.Add(separator);
            var cancel=Ui.Button(detaching?"Annulla stacco":"Annulla inserimento",170,()=>{explicitCancel=true;DialogResult=DialogResult.Cancel;Close();});cancel.SetBounds(386,418,170,36);Controls.Add(cancel);CancelButton=cancel;
            Ui.ApplyTheme(this);FlowFormMotion.Attach(this);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ResumeLayout(false);PerformLayout();
        }
        protected override void OnFormClosing(FormClosingEventArgs e){if(keepPlacement&&!explicitCancel&&Branch==null&&e.CloseReason==CloseReason.UserClosing){Branch="skip";DialogResult=DialogResult.OK;}base.OnFormClosing(e);}
        private void AddChoice(string label,string branch,string color,string detail,int top,int height,bool enabled=true){
            Color swatch=Ui.Muted;try{if(color!=null)swatch=ColorTranslator.FromHtml(color);}catch(ArgumentException){}
            var button=new BranchOption(label,detail,swatch,branch!="none");button.SetBounds(24,top,532,height);button.AccessibleName=branch=="none"?label:"Ramo "+label;button.AccessibleDescription=detail;
            if(!enabled)button.AccessibleDescription="Questo ramo non è collegato o non può richiudere il percorso senza creare un collegamento su se stesso.";button.Enabled=enabled;button.Click+=delegate{Branch=branch;DialogResult=DialogResult.OK;Close();};Controls.Add(button);
        }
        private sealed class BranchOption:Button {
            private readonly string detail;private readonly Color swatch;private readonly bool isBranch;private readonly MotionValue hover,press;
            internal BranchOption(string label,string detail,Color swatch,bool isBranch){Text=label;this.detail=detail;this.swatch=swatch;this.isBranch=isBranch;UseMnemonic=false;FlatStyle=FlatStyle.Flat;TabStop=true;BackColor=Ui.Surface;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);hover=new MotionValue(this);press=new MotionValue(this);}
            protected override void OnMouseEnter(EventArgs e){hover.Set(1);base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover.Set(0);press.Set(0);base.OnMouseLeave(e);}
            protected override void OnMouseDown(MouseEventArgs e){press.Set(1,90);base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){press.Set(0,280);base.OnMouseUp(e);}
            protected override void OnPaint(PaintEventArgs e){
                e.Graphics.Clear(Parent==null?Ui.Surface:Parent.BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;float shift=press.Value;var bounds=new RectangleF(1+shift,1+shift,Width-3-2*shift,Height-3-2*shift);
                using(var path=Ui.Rounded(bounds,10))using(var fill=new SolidBrush(UiMotion.Blend(Ui.Raised,Ui.Hover,hover.Value)))using(var pen=new Pen(Focused&&ShowFocusCues?Ui.Blue:Ui.Line,Focused&&ShowFocusCues?2:1)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
                using(var fill=new SolidBrush(Enabled?swatch:Ui.Muted))e.Graphics.FillEllipse(fill,20,Height/2f-7,14,14);
                using(var font=Ui.Font(11,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,(isBranch?"Ramo ":"")+Text,font,new Rectangle(50,8,Width-65,34),Enabled?Ui.Ink:Ui.Muted,TextFormatFlags.WordBreak|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
                using(var font=Ui.Font(8.5f))TextRenderer.DrawText(e.Graphics,detail,font,new Rectangle(50,46,Width-65,20),Ui.Muted,TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
            }
        }
    }
    internal sealed class BranchStyleDialog : Form
    {
        private readonly TextBox trueText=Ui.TextBox(),falseText=Ui.TextBox();
        private string trueColor,falseColor;
        private readonly Label truePreview=Ui.Label("",9,null,true),falsePreview=Ui.Label("",9,null,true);
        internal string TrueLabel {get{return trueText.Text.Trim();}}
        internal string FalseLabel {get{return falseText.Text.Trim();}}
        internal string TrueColor {get{return trueColor;}}
        internal string FalseColor {get{return falseColor;}}
        internal BranchStyleDialog(FlowNode node)
        {
            // Keep the 96-DPI design coordinates intact until every child exists.
            // Otherwise an early layout consumes the DPI baseline on an empty form.
            SuspendLayout();
            Text="Personalizza le uscite";StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            ClientSize=new Size(510,430);
            Font=Ui.Font();BackColor=Ui.Surface;ForeColor=Ui.Ink;
            trueColor=node.GetBranchColor("true");falseColor=node.GetBranchColor("false");
            var title=Ui.Label("Dai un nome ai due percorsi",17,null,true);title.SetBounds(22,20,466,37);Controls.Add(title);
            var help=Ui.Label("La condizione sceglie il risultato vero o falso.\nLe etichette e i colori descrivono i tuoi percorsi.",9,Ui.Muted);help.SetBounds(23,65,464,43);Controls.Add(help);
            BuildBranch(true,118,trueText,node.GetBranchLabel("true"),truePreview);
            BuildBranch(false,248,falseText,node.GetBranchLabel("false"),falsePreview);
            var cancel=Ui.Button("Annulla",110,()=>{DialogResult=DialogResult.Cancel;Close();});cancel.SetBounds(240,380,110,34);Controls.Add(cancel);CancelButton=cancel;
            var apply=Ui.Button("Applica",128,Apply,true);apply.SetBounds(362,380,126,34);Controls.Add(apply);AcceptButton=apply;
            Ui.ApplyTheme(this);UpdatePreview();
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            ResumeLayout(false);PerformLayout();
        }
        private void BuildBranch(bool yes,int top,TextBox text,string initial,Label preview)
        {
            var label=Ui.Label(yes?"QUANDO LA CONDIZIONE È VERA":"QUANDO LA CONDIZIONE È FALSA",8.5f,Ui.Muted,true);label.SetBounds(23,top,464,22);Controls.Add(label);
            text.Text=initial;text.MaxLength=60;text.SetBounds(23,top+26,464,31);Controls.Add(text);text.TextChanged+=delegate{UpdatePreview();};
            string[] colors={"#39B985","#EF6C82","#5B8DEF","#D68A18","#9771DC","#22A5B8"};
            string[] names={"Verde","Rosa","Blu","Arancio","Viola","Turchese"};
            for(int i=0;i<colors.Length;i++)
            {
                string chosen=colors[i];var button=Ui.Button(names[i],71,()=>{if(yes)trueColor=chosen;else falseColor=chosen;UpdatePreview();});
                button.Font=Ui.Font(8.3f,FontStyle.Bold);button.Accent=ColorTranslator.FromHtml(chosen);button.SetBounds(23+i*78,top+65,71,31);button.AccessibleName=(yes?"Uscita vera: ":"Uscita falsa: ")+names[i];Controls.Add(button);
            }
            preview.SetBounds(24,top+101,462,21);Controls.Add(preview);
        }
        private void UpdatePreview()
        {
            truePreview.Text="→ "+trueText.Text;falsePreview.Text="→ "+falseText.Text;
            truePreview.ForeColor=ColorTranslator.FromHtml(trueColor);falsePreview.ForeColor=ColorTranslator.FromHtml(falseColor);
        }
        private void Apply()
        {
            if(TrueLabel.Length==0||FalseLabel.Length==0){FlowMessageDialog.Show(this,"Scrivi un nome per entrambe le uscite.","Manca un'etichetta",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            DialogResult=DialogResult.OK;Close();
        }
    }
}
