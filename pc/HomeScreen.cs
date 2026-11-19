using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Frociart
{
    internal static class RecentProjects {
        internal const string License="Frociart — Condizioni d’uso\nCopyright © 2026 mamt104. Tutti i diritti riservati.\n\nFrociart è un progetto di mamt104.\nÈ consentito scaricare e utilizzare gratuitamente le versioni ufficiali per uso personale, didattico e professionale. I diagrammi e gli elaborati creati dall’utente restano dell’utente.\n\nSenza autorizzazione scritta dell’autore non è consentito rimuovere o alterare l’attribuzione, rivendere il software, distribuirne versioni modificate o ripubblicarlo presentandolo come un proprio prodotto. Per condividere l’app, usa il collegamento al progetto ufficiale: https://github.com/mamt104/Frociart\n\nNon viene concessa una licenza sul codice sorgente. Restano salvi i diritti inderogabili previsti dalla legge e quelli espressamente concessi dai termini delle piattaforme che ospitano il progetto. I componenti di terzi mantengono i propri titolari e le rispettive condizioni.\n\nIl software è fornito senza garanzie, nei limiti consentiti dalla legge. Conserva copie dei tuoi progetti e verifica i risultati con dati di prova.\n";
        private static string Folder {get{return Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart");}}
        private static string FilePath {get{return Path.Combine(Folder,"recent-projects.txt");}}
        private static bool LocalProject(string path){try{return !String.IsNullOrEmpty(path)&&Path.IsPathRooted(path)&&!path.StartsWith(@"\\",StringComparison.Ordinal)&&path.IndexOfAny(new[]{'\r','\n','\0'})<0&&(String.Equals(Path.GetExtension(path),".frociart",StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetExtension(path),".flow",StringComparison.OrdinalIgnoreCase))&&new DriveInfo(Path.GetPathRoot(path)).DriveType!=DriveType.Network;}catch{return false;}}
        internal static string[] Read(){
            var paths=new List<string>();try{if(File.Exists(FilePath)&&new FileInfo(FilePath).Length<=65536)paths.AddRange(File.ReadAllLines(FilePath));}catch(IOException){}catch(UnauthorizedAccessException){}
            if(Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")==null&&paths.Count==0){
                try{string documents=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);foreach(string folder in new[]{documents,Path.Combine(documents,"Frociart")})if(Directory.Exists(folder))paths.AddRange(Directory.EnumerateFiles(folder).Where(LocalProject).OrderByDescending(File.GetLastWriteTimeUtc).Take(12));}catch(IOException){}catch(UnauthorizedAccessException){}
                // Read targets of existing Windows Recent shortcuts. Never launch or save a shortcut.
                object shell=null;try{var type=Type.GetTypeFromProgID("WScript.Shell");if(type!=null){shell=Activator.CreateInstance(type);string recent=Environment.GetFolderPath(Environment.SpecialFolder.Recent);if(Directory.Exists(recent))foreach(var link in Directory.EnumerateFiles(recent,"*.lnk").Where(p=>p.EndsWith(".frociart.lnk",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".flow.lnk",StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc).Take(24)){
                    object shortcut=null;try{shortcut=type.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{link});string target=(string)shortcut.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.GetProperty,null,shortcut,null);if(LocalProject(target))paths.Add(target);}catch(System.Runtime.InteropServices.COMException){}catch(System.Reflection.TargetInvocationException){}finally{if(shortcut!=null&&System.Runtime.InteropServices.Marshal.IsComObject(shortcut))System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);}
                }}}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Runtime.InteropServices.COMException){}finally{if(shell!=null&&System.Runtime.InteropServices.Marshal.IsComObject(shell))System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
            }
            return paths.Where(LocalProject).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
        }
        internal static void Remember(string path){if(!LocalProject(path))return;string temporary=null;try{var paths=new[]{Path.GetFullPath(path)}.Concat(Read()).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();Directory.CreateDirectory(Folder);temporary=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllLines(temporary,paths,new System.Text.UTF8Encoding(false));if(File.Exists(FilePath))File.Replace(temporary,FilePath,null);else File.Move(temporary,FilePath);}catch(IOException){}catch(UnauthorizedAccessException){}finally{if(temporary!=null&&File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}}}
    }
    internal sealed class FlowchartHome : Panel {
        private readonly Label title=Ui.Label("I tuoi flowchart",27,null,true),intro=Ui.Label("Crea, collega e prova le tue idee. · Un progetto di mamt104",11,Ui.Muted),recentTitle=Ui.Label("Progetti recenti",17,null,true),empty=Ui.Label("I progetti che apri o salvi appariranno qui.",10,Ui.Muted);
        private readonly FlowButton create,open,code,appearance,recovery;
        private readonly HomeActionRow resume;
        private readonly List<HomeActionRow> rows=new List<HomeActionRow>();private readonly Action<string> openRecent;private bool arranging,hasRecovery;
        internal FlowchartHome(Action newProject,Action openProject,Action pseudocode,Action resumeProject,Action<string> recent,Action theme,Action recover){
            BackColor=Ui.Paper;Tag="paper";openRecent=recent;
            create=Ui.Button("Nuovo flowchart",210,newProject,true);open=Ui.Button("Apri file…",170,openProject);code=Ui.Button("Importa pseudocodice",210,pseudocode);appearance=Ui.Button("Aspetto",130,theme);
            recovery=Ui.Button("Recupera lavoro…",180,recover);recovery.Name="HomeRecovery";recovery.Visible=false;recovery.AccessibleDescription="Apri una copia locale non salvata o già recuperata.";
            resume=new HomeActionRow{Text="Riprendi il foglio aperto",Green=true};resume.Click+=delegate{resumeProject();};
            foreach(Control control in new Control[]{title,intro,create,open,code,recovery,appearance,resume,recentTitle,empty})Controls.Add(control);RefreshPalette();
        }
        internal void SetRecoveryAvailable(bool available){hasRecovery=available;recovery.Visible=available;PerformLayout();}
        internal void SetState(string document,int nodes,bool unsaved,string[] paths){
            resume.Detail=document+" · "+nodes+" blocchi"+(unsaved?" · modifiche da salvare":"");
            foreach(var row in rows){Controls.Remove(row);row.Dispose();}rows.Clear();
            foreach(string path in paths){string captured=path;bool exists=File.Exists(path);var row=new HomeActionRow{Text=Path.GetFileNameWithoutExtension(path),Detail=exists?path:"File non trovato · usa Apri file per ritrovarlo",Enabled=exists,AccessibleName="Apri progetto "+Path.GetFileNameWithoutExtension(path)};row.Click+=delegate{openRecent(captured);};rows.Add(row);Controls.Add(row);}
            empty.Visible=rows.Count==0;PerformLayout();
        }
        internal void RefreshPalette(){BackColor=Ui.Paper;foreach(var label in new[]{title,recentTitle})label.ForeColor=Ui.Ink;intro.ForeColor=empty.ForeColor=Ui.Muted;appearance.Text=Ui.IsDark?"Tema chiaro":"Tema scuro";Invalidate(true);}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(arranging||create==null)return;arranging=true;try{float scale=NativeTheme.DpiScale(this);int pad=(int)(24*scale),gap=(int)(12*scale),w=Math.Max(320,Width-pad*2),y=pad;title.SetBounds(pad,y,w-(int)(150*scale),(int)(46*scale));appearance.SetBounds(Width-pad-(int)(135*scale),y,(int)(135*scale),(int)(35*scale));y+=(int)(54*scale);intro.SetBounds(pad,y,w,(int)(30*scale));y+=(int)(48*scale);
            var buttons=new[]{create,open,code,recovery};int x=pad;foreach(var button in buttons){if(button==recovery&&!hasRecovery)continue;int width=(int)((button==open?170:button==recovery?180:220)*scale);if(x+width>Width-pad){x=pad;y+=(int)(55*scale);}button.SetBounds(x,y,width,(int)(43*scale));x+=width+gap;}y+=(int)(70*scale);resume.SetBounds(pad,y,w,resume.MeasureHeight(w));y=resume.Bottom+(int)(28*scale);recentTitle.SetBounds(pad,y,w,(int)(34*scale));y+=(int)(46*scale);foreach(var row in rows){row.SetBounds(pad,y,w,row.MeasureHeight(w));y=row.Bottom+gap;}if(rows.Count==0){empty.SetBounds(pad,y,w,(int)(45*scale));y=empty.Bottom;}Height=y+pad;
        }finally{arranging=false;}}
    }

    // Native, responsive home. Every action delegates to the existing real workflow.
    internal sealed class HomeScreen : Panel
    {
        private readonly Label heading = Ui.Label("Cosa vuoi fare?", 27, null, true);
        private readonly Label intro = Ui.Label("Impara con una guida o crea un progetto tuo.", 11, Ui.Muted);
        private readonly HomeCard learn = new HomeCard(true), create = new HomeCard(false);
        private readonly Label learnCategory = Ui.Label("IMPARA", 9, null, true), createCategory = Ui.Label("CREA", 9, null, true);
        private readonly Label learnTitle = Ui.Label("Impara passo passo", 20, null, true), createTitle = Ui.Label("Il tuo foglio libero", 20, null, true);
        private readonly Label learnBody = Ui.Label("Costruisci i tuoi primi flowchart con spiegazioni e controlli a ogni passo.", 10.5f, Ui.Muted);
        private readonly Label createBody = Ui.Label("Aggiungi blocchi, collegali e prova il risultato. Decidi tu cosa costruire.", 10.5f, Ui.Muted);
        private readonly Label progress = Ui.Label("", 10, null, true);
        private readonly Label workHeading = Ui.Label("I tuoi lavori", 16, null, true), exploreHeading = Ui.Label("Esplora e consulta", 16, null, true);
        private readonly Label offline = Ui.Label("Funziona offline · Salva i progetti sul computer o esportali per portarli con te.", 9, Ui.Muted);
        internal readonly FlowButton LearnButton, NewButton, AppearanceButton;
        internal readonly HomeActionRow LessonResume, DocumentResume, OpenProject, Workshop, Glossary, Advanced;
        private bool arranging;
        internal bool Columns { get; private set; }
        private float scale = 1;

        internal HomeScreen(Action learning, Action resumeLesson, Action newProject, Action resumeDocument, Action openProject,
            Action workshop, Action glossary, Action advanced, Action appearance)
        {
            BackColor = Ui.Paper; Tag = "paper"; TabStop = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            LearnButton = Ui.Button("Apri le lezioni", 280, learning, true); LearnButton.Primary = true; LearnButton.Font = Ui.Font(10.5f, FontStyle.Bold);
            NewButton = Ui.Button("Nuovo progetto", 280, newProject, true); NewButton.Font = Ui.Font(10.5f, FontStyle.Bold);
            AppearanceButton = Ui.Button("Aspetto", 146, appearance); AppearanceButton.AccessibleDescription = "Passa fra tema chiaro e scuro. La scelta viene ricordata.";
            LessonResume = Row("Riprendi la lezione", "", false, resumeLesson); LessonResume.Visible = false;
            DocumentResume = Row("Riprendi il foglio aperto", "", true, resumeDocument);
            OpenProject = Row("Apri un progetto", "Apri .frociart, .flow oppure Flowgorithm .fprg dal computer.", true, openProject);
            Workshop = Row("Laboratorio guidato", "Sperimenta con blocchi già scritti e scopri come funzionano.", false, workshop);
            Glossary = Row("Blocchi e simboli", "Cerca il significato di una parola o di un simbolo.", false, glossary);
            Advanced = Row("Approfondimenti", "Ritrova le lezioni e i quiz del percorso precedente.", false, advanced);
            foreach (Control control in new Control[] {heading, intro, AppearanceButton, learn, create, workHeading, exploreHeading, DocumentResume, OpenProject, Workshop, Glossary, Advanced, offline}) Controls.Add(control);
            foreach (Control control in new Control[] {learnCategory, learnTitle, learnBody, progress, LearnButton, LessonResume}) learn.Controls.Add(control);
            foreach (Control control in new Control[] {createCategory, createTitle, createBody, NewButton}) create.Controls.Add(control);
            learn.AccessibleName = "Impara passo passo"; create.AccessibleName = "Il tuo foglio libero";
            LearnButton.TabIndex=0; LessonResume.TabIndex=1; NewButton.TabIndex=2; DocumentResume.TabIndex=3;
            OpenProject.TabIndex=4; Workshop.TabIndex=5; Glossary.TabIndex=6; Advanced.TabIndex=7; AppearanceButton.TabIndex=8;
            RefreshPalette();
        }
        private static HomeActionRow Row(string title, string detail, bool green, Action click)
        { var row = new HomeActionRow { Text=title, Detail=detail, Green=green }; row.Click += delegate { click(); }; return row; }
        internal void SetState(int completed, string resumeLessonTitle, string documentTitle, int blocks, bool unsaved)
        {
            progress.Text = completed == 0 ? "24 lezioni · Si parte da zero" : completed + " di 24 lezioni completate";
            LessonResume.Visible = !String.IsNullOrWhiteSpace(resumeLessonTitle); LessonResume.Detail = resumeLessonTitle ?? "";
            DocumentResume.Detail = documentTitle + " · " + blocks + (blocks==1?" blocco":" blocchi") + (unsaved?" · modifiche da salvare":"");
            PerformLayout(); Invalidate(true);
        }
        internal void RefreshPalette()
        {
            BackColor=Ui.Paper; learn.BackColor=Ui.Tint; create.BackColor=Ui.Surface;
            LearnButton.Accent=Ui.IsDark?Ui.Blue:Color.FromArgb(47,101,216);
            NewButton.Accent=Ui.IsDark?Ui.Green:Color.FromArgb(25,121,83);
            foreach(Label text in new[]{heading,learnTitle,createTitle,workHeading,exploreHeading}) text.ForeColor=Ui.Ink;
            foreach(Label text in new[]{intro,learnBody,createBody,offline}) text.ForeColor=Ui.IsDark?Ui.Muted:Color.FromArgb(98,100,109);
            foreach(Label text in new[]{learnCategory,progress}) text.ForeColor=Ui.IsDark?Ui.Blue:Color.FromArgb(34,89,196);
            createCategory.ForeColor=Ui.IsDark?Ui.Green:Color.FromArgb(25,121,83);
            AppearanceButton.Text=Ui.IsDark?"Tema chiaro":"Tema scuro"; AppearanceButton.Glyph=Ui.IsDark?ButtonGlyph.Sun:ButtonGlyph.Moon;
            AppearanceButton.AccessibleName="Attiva il "+AppearanceButton.Text.ToLowerInvariant();
            Invalidate(true);
        }
        private int S(float value) { return (int)Math.Round(value*scale); }
        private int LabelAt(Label label,int x,int y,int width)
        {
            int height=TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(1,width),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+S(3);
            label.SetBounds(x,y,width,Math.Max(S(18),height)); return label.Bottom;
        }
        private int LayoutCard(HomeCard card,bool learning,int width)
        {
            int inset=S(22), inner=width-inset*2, y=S(20);
            y=LabelAt(learning?learnCategory:createCategory,inset,y,inner-S(36))+S(12);
            y=LabelAt(learning?learnTitle:createTitle,inset,y,inner)+S(10);
            y=LabelAt(learning?learnBody:createBody,inset,y,inner)+S(14);
            if(learning)y=LabelAt(progress,inset,y,inner)+S(13);
            var button=learning?LearnButton:NewButton; button.SetBounds(inset,y,inner,S(48)); y=button.Bottom;
            if(learning&&LessonResume.Visible){y+=S(12);int h=LessonResume.MeasureHeight(inner);LessonResume.SetBounds(inset,y,inner,h);y+=h;}
            return y+S(22);
        }
        private int RowAt(HomeActionRow row,int x,int y,int width)
        { int height=row.MeasureHeight(width);row.SetBounds(x,y,width,height);return row.Bottom+S(10); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(arranging||Width<150||LearnButton==null)return;arranging=true;
            try {
                scale=NativeTheme.DpiScale(this);int width=ClientSize.Width, gap=S(20), y=S(3);
                AppearanceButton.SetBounds(width-S(150),y,S(150),S(38));
                y=LabelAt(heading,0,y,Math.Max(S(200),AppearanceButton.Left-S(20)))+S(6);
                y=LabelAt(intro,0,y,width)+S(20); Columns=width>=S(820);
                int cardWidth=Columns?(width-gap)/2:width;
                int leftHeight=LayoutCard(learn,true,cardWidth), rightHeight=LayoutCard(create,false,cardWidth);
                learn.SetBounds(0,y,cardWidth,leftHeight);
                create.SetBounds(Columns?cardWidth+gap:0,Columns?y:learn.Bottom+gap,cardWidth,rightHeight);
                y=Math.Max(learn.Bottom,create.Bottom)+S(26);
                int workWidth=Columns?cardWidth:width, rightX=Columns?cardWidth+gap:0;
                int workY=LabelAt(workHeading,0,y,workWidth)+S(10);
                workY=RowAt(DocumentResume,0,workY,workWidth); workY=RowAt(OpenProject,0,workY,workWidth);
                int exploreY=LabelAt(exploreHeading,rightX,Columns?y:workY+S(16),workWidth)+S(10);
                exploreY=RowAt(Workshop,rightX,exploreY,workWidth);exploreY=RowAt(Glossary,rightX,exploreY,workWidth);exploreY=RowAt(Advanced,rightX,exploreY,workWidth);
                y=Math.Max(workY,exploreY)+S(12);y=LabelAt(offline,0,y,width)+S(20);
                if(Height!=y)Height=y;
            }finally{arranging=false;}
        }
    }

    internal sealed class HomeCard : Panel
    {
        private readonly bool learning;
        internal HomeCard(bool learning)
        {
            this.learning=learning;Tag=learning?"tint":"surface";AccessibleRole=AccessibleRole.Grouping;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?Ui.Paper:Parent.BackColor);if(Width<3||Height<3)return;
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=e.Graphics.DpiX/96f;
            using(var path=Ui.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),20*s))
            using(var brush=new SolidBrush(learning?Ui.Tint:Ui.Surface))using(var border=new Pen(learning?Ui.Tint:Ui.Line))
            {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(border,path);}
            var save=e.Graphics.Save();e.Graphics.TranslateTransform(Width-52*s,18*s);e.Graphics.ScaleTransform(s,s);
            using(var pen=new Pen(learning?(Ui.IsDark?Ui.Blue:Color.FromArgb(34,89,196)):Ui.Green,1.8f){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round}) {
                if(learning){e.Graphics.DrawLines(pen,new[]{new PointF(13,6),new PointF(4,3),new PointF(1,4),new PointF(1,23),new PointF(4,22),new PointF(13,25),new PointF(22,22),new PointF(25,23),new PointF(25,4),new PointF(22,3),new PointF(13,6),new PointF(13,25)});}
                else {e.Graphics.DrawLines(pen,new[]{new PointF(3,24),new PointF(9,22),new PointF(24,7),new PointF(19,2),new PointF(4,17),new PointF(3,24)});e.Graphics.DrawLine(pen,16,5,21,10);}
            }e.Graphics.Restore(save);
        }
    }
    internal sealed class HomeActionRow : Button
    {
        [System.ComponentModel.Browsable(false),System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal string Detail { get {return detail;} set {detail=value??"";AccessibleDescription=detail;Invalidate();} }
        internal bool Green;
        private string detail="";private bool hover;
        private readonly Font titleFont=Ui.Font(10.5f,FontStyle.Bold), detailFont=Ui.Font(9.5f);
        internal HomeActionRow()
        {
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;UseMnemonic=false;AccessibleRole=AccessibleRole.PushButton;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        internal int MeasureHeight(int width)
        {
            float scale=NativeTheme.DpiScale(this);int inset=(int)Math.Round(52*scale),textWidth=Math.Max(30,width-inset);
            return Math.Max((int)Math.Round(76*scale),TextRenderer.MeasureText(Text,titleFont,new Size(textWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+TextRenderer.MeasureText(Detail,detailFont,new Size(textWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+(int)Math.Round(30*scale));
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?Ui.Paper:Parent.BackColor);if(Width<4||Height<4)return;
            float s=e.Graphics.DpiX/96f;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Ui.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),13*s))using(var fill=new SolidBrush(hover?Ui.Hover:Ui.Raised))using(var pen=new Pen(Focused&&ShowFocusCues?Ui.Blue:Ui.Line,Focused&&ShowFocusCues?2:1))
            {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
            int x=(int)(22*s),width=Width-(int)(51*s),top=(int)(14*s);
            var titleSize=TextRenderer.MeasureText(Text,titleFont,new Size(width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics,Text,titleFont,new Rectangle(x,top,width,titleSize.Height),Ui.Ink,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics,Detail,detailFont,new Rectangle(x,top+titleSize.Height+(int)(5*s),width,Height-top-titleSize.Height-(int)(12*s)),Ui.IsDark?Ui.Muted:Color.FromArgb(98,100,109),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            using(var pen=new Pen(Green?Ui.Green:(Ui.IsDark?Ui.Blue:Color.FromArgb(34,89,196)),3*s)){e.Graphics.DrawLine(pen,10*s,17*s,10*s,Height-17*s);}
            using(var pen=new Pen(Ui.Muted,1.7f*s)){float cx=Width-17*s,cy=Height/2f;e.Graphics.DrawLines(pen,new[]{new PointF(cx-3*s,cy-5*s),new PointF(cx+2*s,cy),new PointF(cx-3*s,cy+5*s)});}
        }
        protected override void Dispose(bool disposing){if(disposing){titleFont.Dispose();detailFont.Dispose();}base.Dispose(disposing);}
    }
}
