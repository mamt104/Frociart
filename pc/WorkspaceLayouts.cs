using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Frociart {
    // Presentation preferences only: never serialized into a diagram or its history.
    internal sealed class WorkspaceLayout {
        internal int Mode;
        internal bool Advanced,Files,Turtle,Code,Notes,Debug;
        internal static readonly string[] Names={"Essenziale","Sviluppo","Completo"};
        internal static readonly string[] Descriptions={
            "Più spazio per iniziare. Blocchi comuni in primo piano; gli altri gruppi si aprono quando servono.",
            "Variabili, chiamate e codice a vista. File e disegno restano raccolti nei loro gruppi.",
            "Tutti i gruppi di blocchi aperti e codice a vista. Puoi comunque richiudere i pannelli."};
        internal static string PathName {get{return Path.Combine(Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart"),"workspace.txt");}}
        internal void Preset(int mode){if(mode<0||mode>2)throw new ArgumentOutOfRangeException("mode");Mode=mode;Advanced=Code=Debug=mode>0;Files=Turtle=Notes=mode==2;}
        internal static WorkspaceLayout Load(){var result=new WorkspaceLayout();try{var file=new FileInfo(PathName);if(!file.Exists||file.Length>64)return result;var s=File.ReadAllText(PathName).Trim().Split(',');int mode;if((s.Length!=5&&s.Length!=7)||!Int32.TryParse(s[0],out mode)||mode<0||mode>2||s.Skip(1).Any(x=>x!="0"&&x!="1"))return result;result.Preset(mode);result.Advanced=s[1]=="1";result.Files=s[2]=="1";result.Turtle=s[3]=="1";result.Code=s[4]=="1";if(s.Length==7){result.Notes=s[5]=="1";result.Debug=s[6]=="1";}}catch(IOException){}catch(UnauthorizedAccessException){}return result;}
        internal bool Save(){string temp=null;try{Directory.CreateDirectory(Path.GetDirectoryName(PathName));temp=PathName+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,Mode+","+(Advanced?1:0)+","+(Files?1:0)+","+(Turtle?1:0)+","+(Code?1:0)+","+(Notes?1:0)+","+(Debug?1:0));if(File.Exists(PathName))File.Replace(temp,PathName,null);else File.Move(temp,PathName);return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}finally{if(temp!=null&&File.Exists(temp))try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}}
    }

    internal sealed partial class MainForm {
        private readonly WorkspaceLayout workspaceLayout=WorkspaceLayout.Load();
        private FlowButton workspaceButton,workspaceCodeToggle;
        private TableLayoutPanel workspaceSidebar;
        internal int WorkspaceMode {get{return workspaceLayout.Mode;}}
        private void ChooseWorkspaceLayout(){
            if(Canvas.IsPointerGestureActive||Canvas.IsConnecting){SetStatus("Termina il trascinamento o il collegamento prima di cambiare layout.");return;}
            var menu=PrepareTaskMenu("Vista");var layouts=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Name=="LayoutChoices");
            moreMenu=menu;Ui.StyleMenu(menu);menu.Show(workspaceButton,new Point(0,workspaceButton.Height));layouts.Select();layouts.ShowDropDown();
        }
        internal void SetWorkspaceLayout(int mode,bool persist){
            if(Canvas.IsPointerGestureActive||Canvas.IsConnecting)return;
            if(mode<0||mode>2)throw new ArgumentOutOfRangeException("mode");
            FlowTransitions.Change(editorBody);workspaceLayout.Preset(mode);PopulateWorkspacePalette(false);ApplyWorkspacePresentation();
            bool saved=!persist||workspaceLayout.Save();
            SetStatus("Layout "+WorkspaceLayout.Names[mode]+". Tutte le funzioni restano disponibili."+(saved?"":" Preferenza non salvata: verrà mantenuta solo in questa sessione."));
        }
        private void SaveWorkspacePresentation(){if(!workspaceLayout.Save())SetStatus("Layout aggiornato. Non riesco a salvare la preferenza per il prossimo avvio.");}
        private void InstallWorkspaceCode(TableLayoutPanel sidebar){
            workspaceSidebar=sidebar;
            var host=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
            workspaceCodeToggle=Ui.Button("",300,delegate{FlowTransitions.Change(sidebar);workspaceLayout.Code=!workspaceLayout.Code;ApplyWorkspacePresentation();SaveWorkspacePresentation();});
            workspaceCodeToggle.Name="WorkspaceCodeToggle";workspaceCodeToggle.Dock=DockStyle.Top;workspaceCodeToggle.Height=36;
            host.Controls.Add(livePseudocode);host.Controls.Add(workspaceCodeToggle);sidebar.Controls.Add(host,0,1);ApplyWorkspacePresentation();
        }
        private void ApplyWorkspacePresentation(){
            if(workspaceButton!=null){workspaceButton.Text="Layout: "+WorkspaceLayout.Names[workspaceLayout.Mode]+" ▾";workspaceButton.AccessibleName="Layout di lavoro: "+WorkspaceLayout.Names[workspaceLayout.Mode];}
            if(workspaceSidebar==null)return;
            workspaceSidebar.SuspendLayout();livePseudocode.Visible=workspaceLayout.Code;
            workspaceCodeToggle.Text=workspaceLayout.Code?"Codice a vista  ▾":"Mostra pseudocodice e codice  ▸";
            workspaceCodeToggle.AccessibleDescription="Mostra o nasconde l'anteprima del codice senza modificare il diagramma.";
            workspaceSidebar.RowStyles[0].SizeType=SizeType.Percent;workspaceSidebar.RowStyles[0].Height=workspaceLayout.Code?50:100;
            workspaceSidebar.RowStyles[1].SizeType=workspaceLayout.Code?SizeType.Percent:SizeType.Absolute;workspaceSidebar.RowStyles[1].Height=workspaceLayout.Code?50:S(40);
            workspaceSidebar.ResumeLayout(true);if(workspaceLayout.Code)livePseudocode.Schedule();
        }
        private void PopulateWorkspacePalette(bool preserveScroll=true){
            var palette=ioPalette;
            var scroll=preserveScroll?new Point(-palette.AutoScrollPosition.X,-palette.AutoScrollPosition.Y):Point.Empty;
            // Child bounds are document coordinates. Rebuilding while scrolled lets
            // WinForms apply the old display offset a second time to the new rows.
            palette.AutoScrollPosition=Point.Empty;palette.AutoScrollMinSize=Size.Empty;
            palette.SuspendLayout();
            try {
                foreach(Control old in palette.Controls.Cast<Control>().ToArray()){palette.Controls.Remove(old);old.Dispose();}
                var caption=Ui.Label("Aggiungi un blocco",11,Ui.Ink,true);SetBox(caption,15,18,162,25);palette.Controls.Add(caption);
                var desc=Ui.Label("Clic: blocco libero\nTrascina sulla freccia",8.5f,Ui.Muted);SetBox(desc,15,45,162,36);palette.Controls.Add(desc);
                int y=92;AddWorkspaceHeading(palette,"BLOCCHI DI BASE",ref y);
                foreach(var kind in new[]{NodeKind.Start,NodeKind.Input,NodeKind.Output,NodeKind.Process,NodeKind.Decision,NodeKind.End}){
                    if(kind==NodeKind.Output&&!IoInstruction.Separate)continue;AddWorkspaceBlock(palette,kind,ref y);
                }
                AddWorkspaceHeading(palette,"STRUTTURE E DATI",ref y);
                var loops=Ui.Button("Ciclo guidato…",164,ShowLoopEditor);loops.Name="GuidedLoop";loops.AccessibleDescription="Inserisci Per, Mentre o Ripeti nella freccia scelta. Un'unica operazione annullabile.";SetBox(loops,13,y,164,40);palette.Controls.Add(loops);y+=47;
                AddWorkspaceGroup(palette,"Variabili e funzioni", "AdvancedPaletteGroup",workspaceLayout.Advanced,()=>workspaceLayout.Advanced=!workspaceLayout.Advanced,ref y);
                if(workspaceLayout.Advanced)foreach(var kind in new[]{NodeKind.Declare,NodeKind.Call})AddWorkspaceBlock(palette,kind,ref y);
                AddWorkspaceGroup(palette,"File", "FilePaletteGroup",workspaceLayout.Files,()=>workspaceLayout.Files=!workspaceLayout.Files,ref y);
                if(workspaceLayout.Files)foreach(var kind in FileBlocks.Kinds)AddWorkspaceBlock(palette,kind,ref y);
                AddWorkspaceGroup(palette,"Disegno · tartaruga", "TurtlePaletteGroup",workspaceLayout.Turtle,()=>workspaceLayout.Turtle=!workspaceLayout.Turtle,ref y);
                if(workspaceLayout.Turtle)foreach(var kind in FlowTurtle.Kinds)AddWorkspaceBlock(palette,kind,ref y);
                AddWorkspaceHeading(palette,"ANNOTAZIONI E DEBUG",ref y);
                AddWorkspaceGroup(palette,"Annotazioni", "NotesPaletteGroup",workspaceLayout.Notes,()=>workspaceLayout.Notes=!workspaceLayout.Notes,ref y);
                if(workspaceLayout.Notes)AddWorkspaceBlock(palette,NodeKind.Note,ref y);
                AddWorkspaceGroup(palette,"Debug", "DebugPaletteGroup",workspaceLayout.Debug,()=>workspaceLayout.Debug=!workspaceLayout.Debug,ref y);
                if(workspaceLayout.Debug)AddWorkspaceBlock(palette,NodeKind.Pause,ref y);
                var hint=Ui.Label("Apri un gruppo per gli altri blocchi.\n\nInput / Output: preferenze in Vista.",9,Ui.Muted);SetBox(hint,15,y+8,162,82);palette.Controls.Add(hint);palette.AutoScrollMinSize=new Size(0,S(y+102));
            }finally{palette.ResumeLayout(true);palette.AutoScrollPosition=scroll;}
        }
        private void SetIoPresentation(bool separate){
            if(Canvas.IsPointerGestureActive||Canvas.IsConnecting){SetStatus("Termina il gesto prima di cambiare la presentazione dei blocchi.");return;}
            if(IoInstruction.Separate==separate)return;
            CommitInspector();Canvas.CancelConnection();FlowTransitions.Change(editorBody);Ui.SetSeparateIo(separate);Canvas.RefreshIoPresentation();LoadInspector();PopulatePalette();
            SetStatus(separate?"Input e Output separati nella palette.":"Un solo blocco Input / Output: scrivi leggi o stampa.");
        }
        private void AddWorkspaceHeading(FlowScrollPanel palette,string text,ref int y){var heading=Ui.Label(text,8.2f,Ui.Muted,true);heading.Name="PaletteHeading";SetBox(heading,15,y+7,162,24);palette.Controls.Add(heading);y+=36;}
        private void AddWorkspaceGroup(FlowScrollPanel palette,string text,string name,bool expanded,Action toggle,ref int y){
            var button=Ui.Button(text+(expanded?"  ▾":"  ▸"),164,delegate{FlowTransitions.Change(palette.Parent);bool restore=palette.ContainsFocus;toggle();PopulatePalette();SaveWorkspacePresentation();if(restore){var next=palette.Controls.Find(name,false).FirstOrDefault();if(next!=null){palette.ScrollControlIntoView(next);next.Focus();}}});button.Name=name;button.AccessibleDescription="Mostra o nasconde questo gruppo; disponibile in tutti i layout.";SetBox(button,13,y,164,38);palette.Controls.Add(button);y+=44;
        }
        private void AddWorkspaceBlock(FlowScrollPanel palette,NodeKind kind,ref int y){
            string description=kind==NodeKind.Note?"Testo sul foglio":FileBlocks.IsFile(kind)?FileBlocks.Description(kind):FlowTurtle.IsTurtle(kind)?FlowTurtle.Description(kind):kind==NodeKind.Start?"Il punto di partenza":kind==NodeKind.Input?(IoInstruction.Separate?"Leggi un dato":"Leggi o stampa"):kind==NodeKind.Output?"Stampa un risultato":kind==NodeKind.Process?"Calcola o assegna":kind==NodeKind.Decision?"Scegli un percorso":kind==NodeKind.Declare?"Scegli nomi e tipi":kind==NodeKind.Pause?"Ferma la simulazione":kind==NodeKind.Call?"Richiama una funzione":"Termina il percorso";
            var button=new BlockButton(kind,description);button.Name="WorkspaceBlock"+kind;SetBox(button,13,y,164,60);button.AccessibleDescription=kind==NodeKind.Note?"Scrivi una spiegazione libera sul foglio, fuori dal percorso di esecuzione.":"Trascina sulla freccia per creare e collegare. Un clic aggiunge una casella scollegata.";tips.SetToolTip(button,button.AccessibleDescription);Canvas.WirePalette(button,kind,delegate{CommitInspector();Canvas.AddLooseNode(kind);tabs.SelectedTab=blockTab;},CommitInspector,kind==NodeKind.Input&&!IoInstruction.Separate);palette.Controls.Add(button);y+=65;
        }
    }
}
