using System;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
    internal sealed class LearningDialog : Form {
        private readonly LearningStore store;
        private readonly TreeView path=new TreeView();
        private readonly FlowScrollPanel reader=new FlowScrollPanel();
        private readonly Label progress=Ui.Label("",10,Ui.Muted);
        private readonly Label notice=Ui.Label("",9,Ui.Amber);
        private readonly Panel content=new Panel();
        private readonly LessonSteps steps=new LessonSteps();
        private FlowButton advance,reviewEntry;
        private System.Action advanceAction;
        private LearningLesson lesson;
        private bool rendering,showSources,completionNew;
        private int y,introStep;
        private Control feedbackTarget;
        internal string PracticeExample {get;private set;}
        internal LearningDialog(LearningStore state){
            store=state;Text="Impara — Frociart";StartPosition=FormStartPosition.CenterParent;
            Size=new Size(1080,790);MinimumSize=new Size(900,660);Font=Ui.Font();BackColor=Ui.Paper;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=Padding.Empty};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,276));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,118));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,86));Controls.Add(layout);
            var header=new Panel{Dock=DockStyle.Fill,BackColor=Ui.Surface};layout.Controls.Add(header,0,0);layout.SetColumnSpan(header,2);
            var title=Ui.Label("Impara. Un passo alla volta.",21,Ui.Ink,true);title.SetBounds(24,16,680,34);header.Controls.Add(title);
            progress.SetBounds(25,55,700,25);header.Controls.Add(progress);
            progress.AutoEllipsis=true;reviewEntry=Ui.Button("Allenamento mirato",190,Review);reviewEntry.Top=47;header.Controls.Add(reviewEntry);
            System.Action arrangeHeader=delegate{reviewEntry.Left=header.ClientSize.Width-reviewEntry.Width-24;progress.Width=Math.Max(200,reviewEntry.Left-40);};header.Resize+=delegate{arrangeHeader();};Shown+=delegate{arrangeHeader();};
            steps.SetBounds(25,87,1000,12);steps.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;header.Controls.Add(steps);
            path.Dock=DockStyle.Fill;path.BorderStyle=BorderStyle.None;path.Font=Ui.Font(10);path.ItemHeight=31;path.HideSelection=false;path.FullRowSelect=true;path.ShowLines=false;path.ShowPlusMinus=true;path.AccessibleName="Percorso: sei tappe, ventiquattro lezioni";
            layout.Controls.Add(path,0,1);
            reader.Dock=DockStyle.Fill;reader.AutoScroll=true;content.Location=Point.Empty;reader.Controls.Add(content);layout.Controls.Add(reader,1,1);
            reader.Resize+=delegate {Render();};
            var footer=new Panel{Dock=DockStyle.Fill,BackColor=Ui.Surface};layout.Controls.Add(footer,0,2);layout.SetColumnSpan(footer,2);
            notice.SetBounds(20,59,700,22);notice.AutoEllipsis=true;notice.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;footer.Controls.Add(notice);
            var close=Ui.Button("Chiudi Impara",146,()=>Close());close.SetBounds(20,9,146,40);footer.Controls.Add(close);
            advance=Ui.Button("Continua",290,()=>{if(advanceAction!=null){advanceAction();FocusStep();}},true);advance.Height=42;advance.Top=8;footer.Controls.Add(advance);
            System.Action arrangeFooter=delegate {advance.Left=footer.ClientSize.Width-advance.Width-20;notice.Width=footer.ClientSize.Width-40;};
            footer.Resize+=delegate {arrangeFooter();};Shown+=delegate{arrangeFooter();};
            foreach(var unit in LearningContent.Current.units){
                var parent=new TreeNode(unit.title){Tag=unit.id};path.Nodes.Add(parent);
                foreach(var item in LearningContent.Current.lessons.Where(l=>l.unitId==unit.id))parent.Nodes.Add(new TreeNode(item.title){Tag=item});
                parent.Expand();
            }
            path.AfterSelect+=delegate(object sender,TreeViewEventArgs e){
                var selected=e.Node.Tag as LearningLesson;
                introStep=0;showSources=false;completionNew=false;
                if(selected!=null){lesson=selected;store.State.Begin(lesson.id,false);Save();}else lesson=null;
                reader.AutoScrollPosition=Point.Empty;Render();
            };
            Ui.ThemeChanged+=ThemeChanged;UpdateProgress();
            Shown+=delegate {SelectLesson(store.State.Recommended.id);};
        }
        private void ThemeChanged(object sender,EventArgs e){Render();}
        private void Save(){store.Save();UpdateProgress();}
        private void UpdateProgress(){
            progress.Text=store.State.Completed.Count+" / 24 lezioni · "+store.State.Xp+" XP · "+store.State.Review.Count+(store.State.Review.Count==1?" idea da allenare":" idee da allenare");
            reviewEntry.Visible=store.State.Review.Count>0;
            notice.Text=store.Error??"Un passo alla volta. Le risposte sbagliate diventano spunti per il ripasso.";
            foreach(TreeNode unit in path.Nodes){
                var data=LearningContent.Current.units.First(u=>u.id==(string)unit.Tag);
                int count=unit.Nodes.Cast<TreeNode>().Count(n=>store.State.Done(((LearningLesson)n.Tag).id));unit.Text=data.title+"  "+count+"/4";
                foreach(TreeNode node in unit.Nodes){
                    var item=(LearningLesson)node.Tag;node.Text=(store.State.Done(item.id)?"✓  ":item.id==store.State.Recommended.id?"›  ":"")+item.title;
                }
            }
            steps.Completed=lesson==null?0:Math.Max(0,Math.Min(3,store.State.Question+(store.State.Correct?1:0)));steps.Invalidate();
        }
        internal void SelectLesson(string id){
            foreach(TreeNode unit in path.Nodes)foreach(TreeNode node in unit.Nodes)
                if(((LearningLesson)node.Tag).id==id){path.SelectedNode=node;node.EnsureVisible();return;}
        }
        private int WidthAvailable {get{return Math.Max(280,content.Width-48);}}
        private Label Paragraph(string text,float size=10.5f,Color? color=null,bool bold=false){
            if(String.IsNullOrEmpty(text))return null;var label=Ui.Label(text,size,color,bold);
            int height=TextRenderer.MeasureText(text,label.Font,new Size(WidthAvailable,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+8;
            label.SetBounds(24,y,WidthAvailable,height);content.Controls.Add(label);y+=height+10;return label;
        }
        private void Primary(string text,System.Action action,bool enabled=true){advance.Text=text;advanceAction=action;advance.Enabled=enabled;}
        private void FocusStep(){var option=content.Controls.Cast<Control>().FirstOrDefault(c=>Object.Equals(c.Tag,"answer")&&c.Enabled);if(option!=null)option.Focus();else if(advance.Enabled)advance.Focus();}
        private void Review(){
            var target=store.State.ReviewLesson;if(target==null)return;
            store.State.Begin(target.id,true);introStep=0;showSources=false;completionNew=false;SelectLesson(target.id);Save();reader.AutoScrollPosition=Point.Empty;Render();
        }
        private void Action(string text,Action action,bool primary=false){
            var button=Ui.Button(text,Math.Min(WidthAvailable,350),action,primary);button.SetBounds(24,y,Math.Min(WidthAvailable,350),40);content.Controls.Add(button);y+=51;
        }
        private void Code(string text){
            if(String.IsNullOrEmpty(text))return;
            var box=Ui.TextBox(true);box.Text=text;box.Font=Ui.Mono(10);box.ReadOnly=true;box.WordWrap=false;box.ScrollBars=ScrollBars.Both;box.AccessibleName="Pseudocodice della domanda";
            box.SetBounds(24,y,WidthAvailable,Math.Min(180,Math.Max(70,(text.Split('\n').Length+1)*19)));content.Controls.Add(box);y+=box.Height+17;
        }
        private void Render(){
            if(IsDisposed||rendering||reader.ClientSize.Width<100)return;
            rendering=true;content.Width=Math.Max(280,reader.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-8);content.Left=0;
            content.SuspendLayout();
            try{
                foreach(Control child in content.Controls.Cast<Control>().ToArray()){content.Controls.Remove(child);child.Dispose();}
                content.BackColor=reader.BackColor=Ui.Surface;path.BackColor=Ui.Paper;path.ForeColor=Ui.Ink;y=24;feedbackTarget=null;
                Primary("Continua",null,false);
                if(lesson==null){
                    Paragraph("Il tuo percorso",21,Ui.Ink,true);Paragraph(LearningContent.Current.intro);
                    Paragraph("Leggi un'idea. Prevedi il risultato. Poi mettila alla prova.",13,Ui.Blue,true);
                    Paragraph("Ogni lezione contiene una spiegazione, un trucco, un controesempio e tre domande. Le tappe restano sempre consultabili.");
                    Action("Continua da "+store.State.Recommended.title,()=>SelectLesson(store.State.Recommended.id),true);
                    Primary("Riprendi il percorso",()=>SelectLesson(store.State.Recommended.id));
                    if(store.State.Review.Count>0)Action("Allena le idee da ripassare ("+store.State.Review.Count+")",Review);
                }else{
                    Paragraph(LearningContent.Current.units.First(u=>u.id==lesson.unitId).title.ToUpperInvariant(),9,Ui.Muted,true);
                    Paragraph(lesson.title,store.State.Question<0?21:11,store.State.Question<0?Ui.Ink:Ui.Muted,true);
                    if(store.State.Question<0)ReadLesson();
                    else if(store.State.Finished)Result();
                    else Question();
                }
                content.Height=y+15;
                // WinForms retains the old virtual width when a scrolling child shrinks.
                // Recompute the scroll extent while keeping the current vertical reading position.
                Point scroll=reader.AutoScrollPosition;reader.AutoScroll=false;reader.AutoScrollMinSize=Size.Empty;
                reader.AutoScroll=true;reader.AutoScrollMinSize=new Size(0,content.Height);reader.AutoScrollPosition=new Point(0,-scroll.Y);
                Ui.ApplyTheme(content);UpdateProgress();
                if(feedbackTarget!=null)reader.ScrollControlIntoView(feedbackTarget);
            }finally{content.ResumeLayout(true);rendering=false;}
        }
        private void ReadLesson(){
            Paragraph(introStep==0?"LA TUA MISSIONE · 3 SFIDE · +30 XP":"IL TRUCCO · POI TOCCA A TE",9,Ui.Blue,true);
            if(introStep==0){
                Paragraph(lesson.goal,14,Ui.Ink,true);Paragraph(lesson.explanation,12);
                Primary("Continua",()=>{introStep=1;reader.AutoScrollPosition=Point.Empty;Render();});
            }else{
                Paragraph("Un trucco da provare",16,Ui.Ink,true);Paragraph(lesson.tip,12);
                Paragraph("Occhio a questo caso",13,Ui.Amber,true);Paragraph(lesson.counterexample,12);
                if(String.IsNullOrEmpty(lesson.practiceExample))Paragraph(LearningContent.Current.pseudocodeNote,10,Ui.Muted);
                Primary("Prova le 3 domande",()=>{store.State.Start();Save();reader.AutoScrollPosition=Point.Empty;Render();});
                Action("Rileggi l'idea",()=>{introStep=0;reader.AutoScrollPosition=Point.Empty;Render();});
            }
            Action(String.IsNullOrEmpty(lesson.practiceExample)?"Apri un diagramma vuoto":"Prova sul diagramma",Practice);
            Action(showSources?"Nascondi le fonti":"Fonti e approfondimenti",()=>{showSources=!showSources;Render();});
            if(!showSources)return;
            Paragraph("FONTI",9,Ui.Muted,true);
            foreach(var source in lesson.sources){
                var link=new LinkLabel{Text=source.title,Font=Ui.Font(9.5f),LinkColor=Ui.Blue,ActiveLinkColor=Ui.Blue,VisitedLinkColor=Ui.Blue,BackColor=Ui.Surface,UseMnemonic=false};
                int height=TextRenderer.MeasureText(link.Text,link.Font,new Size(WidthAvailable,Int32.MaxValue),TextFormatFlags.WordBreak).Height+8;
                link.SetBounds(24,y,WidthAvailable,height);y+=height+6;link.AccessibleName="Apri fonte: "+source.title;
                string url=source.url;link.LinkClicked+=delegate{try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){notice.Text="Fonte non aperta: "+ex.Message;}};
                content.Controls.Add(link);
            }
        }
        private void Question(){
            var q=lesson.questions[store.State.Question];Paragraph("Sfida "+(store.State.Question+1)+" di 3 · prevedi, poi scegli",10,Ui.Blue,true);
            Paragraph(q.prompt,15,Ui.Ink,true);Code(q.code);
            for(int i=0;i<q.options.Length;i++){
                int index=i;var button=new Button{Text=q.options[i],Font=Ui.Font(11),FlatStyle=FlatStyle.Flat,BackColor=Ui.Raised,ForeColor=Ui.Ink,
                    TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,8,12,8),UseVisualStyleBackColor=false,Enabled=store.State.Answer<0,AccessibleName=q.options[i],Tag="answer"};
                button.FlatAppearance.BorderColor=store.State.Answer==i?(store.State.Correct?Ui.Green:Ui.Amber):Ui.Line;
                int height=Math.Max(58,TextRenderer.MeasureText(q.options[i],button.Font,new Size(WidthAvailable-28,Int32.MaxValue),TextFormatFlags.WordBreak).Height+24);
                button.SetBounds(24,y,WidthAvailable,height);y+=height+10;
                button.Click+=delegate{if(store.State.Choose(index)){Save();Render();advance.Focus();}};content.Controls.Add(button);
            }
            if(store.State.Answer>=0){
                Paragraph(store.State.Correct?"Ci sei! Hai seguito il ragionamento.":"Quasi. Guarda cosa cambia.",12,store.State.Correct?Ui.Green:Ui.Amber,true);
                feedbackTarget=Paragraph(q.feedback[store.State.Answer],11);
                if(store.State.Correct)Primary(store.State.Question==2?"Completa la lezione":"Prossima domanda",()=>{bool wasDone=store.State.Done(lesson.id);store.State.Advance();completionNew=store.State.Finished&&!wasDone;Save();reader.AutoScrollPosition=Point.Empty;Render();});
                else Primary("Riprova questa domanda",()=>{store.State.Retry();Save();Render();});
            }else Primary("Scegli una risposta",null,false);
            Action("Rileggi la spiegazione",()=>{store.State.Question=store.State.Answer=-1;store.State.FirstTry=0;store.State.Mistake=false;introStep=0;Save();reader.AutoScrollPosition=Point.Empty;Render();});
        }
        private void Result(){
            Paragraph("Lezione completata",18,Ui.Green,true);
            Paragraph(completionNew?"+30 XP · un passo conquistato":"Ripasso riuscito · progressi confermati",15,Ui.Blue,true);
            Paragraph(store.State.FirstTry+" / 3 al primo tentativo. Ora spiega il percorso con parole tue e cambia un dato.",12);
            int done=LearningContent.Current.lessons.Count(l=>l.unitId==lesson.unitId&&store.State.Done(l.id));
            Paragraph(done==4?"Tappa conquistata: 4 lezioni completate!":"La tua tappa: "+done+" / 4 lezioni completate.",12,Ui.Green,true);
            if(store.State.Review.Count>0)Action("Allena le idee da ripassare ("+store.State.Review.Count+")",Review);
            Action(String.IsNullOrEmpty(lesson.practiceExample)?"Apri il laboratorio":"Prova sul diagramma",Practice);
            var all=LearningContent.Current.lessons;int index=Array.IndexOf(all,lesson);
            if(index+1<all.Length)Primary("Prossima lezione",()=>SelectLesson(all[index+1].id));else Primary("Torna al percorso",()=>{lesson=null;Render();});
            Action("Ripassa questa lezione",()=>{store.State.Begin(lesson.id,true);introStep=0;completionNew=false;Save();reader.AutoScrollPosition=Point.Empty;Render();});
        }
        private void Practice(){PracticeExample=lesson.practiceExample??"";DialogResult=DialogResult.OK;Close();}
        protected override void Dispose(bool disposing){if(disposing)Ui.ThemeChanged-=ThemeChanged;base.Dispose(disposing);}
    }
    internal sealed class LessonSteps : Control {
        internal int Completed;
        internal LessonSteps(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleName="Avanzamento delle tre sfide";TabStop=false;}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(Ui.Surface);for(int i=0;i<3;i++)using(var brush=new SolidBrush(i<Completed?Ui.Green:Ui.Line))e.Graphics.FillRectangle(brush,i*Width/3,1,Math.Max(0,Width/3-8),Math.Max(1,Height-2));
            AccessibleDescription=Completed+" di 3 sfide risolte";
        }
    }
}
