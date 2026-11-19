using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed partial class MainForm : Form
    {
        internal readonly FlowCanvas Canvas = new FlowCanvas();
        private string currentPath;
        private string pseudocodeDraft;
        private Diagram pseudocodeSource;
        private PseudocodePreview livePseudocode;
        private bool dirty, loadingInspector, automatic, changingDocument, ignoreRecovery, ticking, editingTitle;
        private string editingNodeId;
        private float dpiScale=1f;
        private readonly Stack<ProjectHistory> undo = new Stack<ProjectHistory>();
        private readonly Stack<ProjectHistory> redo = new Stack<ProjectHistory>();
        private readonly Timer runTimer = new Timer();
        private readonly Timer recoveryTimer = new Timer();
        private readonly ToolTip tips = new ToolTip { AutoPopDelay=12000, InitialDelay=400, ReshowDelay=100 };
        private ContextMenuStrip moreMenu;
        private ToolStripMenuItem moreUndo, moreRedo;
        private readonly SidebarTabs tabs = new SidebarTabs();
        private readonly Panel blockTab = new Panel(), verifyTab = new Panel(), runTab = new Panel();
        private readonly FlowScrollPanel inspector = new FlowScrollPanel(), issuePanel = new ValidationIssuesView(), runtimePanel = new FlowScrollPanel();
        private readonly Label documentTitle = Ui.Label("", 12, null, true);
        private readonly Label statsLabel = Ui.Label("", 8.5f, Ui.Muted);
        private readonly Label status = Ui.Label("Pronto", 8.8f, Ui.Muted);
        private readonly Label zoomLabel = Ui.Label("100%", 9, Ui.Muted);
        private readonly Label selectionTitle = Ui.Label("Il tuo diagramma", 16, null, true);
        private readonly Label selectionHelp = Ui.Label("", 9, Ui.Muted);
        private readonly Label groupHelp = Ui.Label("Sposta i blocchi trascinandone uno.\n\nCtrl + clic aggiunge o toglie un blocco.\nMaiusc + clic aggiunge alla selezione.\nCtrl+A seleziona tutti i blocchi.\nCtrl+C copia · Ctrl+X taglia · Ctrl+V incolla.\nCtrl+D duplica il gruppo.\nCanc elimina la selezione.\nCtrl+Z annulla l'ultima modifica.",9,Ui.Muted);
        private readonly Label expressionLabel = Ui.Label("Contenuto del blocco",9,Ui.Muted,true);
        private readonly TextBox expression = Ui.TextBox(true), prompt = Ui.TextBox(), titleEdit = Ui.TextBox();
        private readonly Label commentLabel=Ui.Label("COMMENTO · FACOLTATIVO",8.4f,Ui.Muted,true);
        private readonly TextBox comment=Ui.TextBox(true);
        private readonly Label syntaxHelp = Ui.Label("", 8.8f, Ui.Muted);
        private readonly Label promptLabel = Ui.Label("Domanda da mostrare",9,Ui.Muted,true);
        private readonly Label linksLabel = Ui.Label("COLLEGAMENTI IN USCITA",8.4f,Ui.Muted,true);
        private FlowButton cycleButton;
        private FlowButton applyButton, nextButton, yesButton, noButton, disconnectButton, deleteButton, duplicateButton, branchStyleButton, functionButton;
        private FlowButton checkButton, runButton, stepButton, pauseButton, resetButton, undoButton, redoButton, cancelLinkButton;
        private readonly Label contextTitle = Ui.Label("",9.4f,null,true);
        private readonly Label contextDetail = Ui.Label("",8.3f,Ui.Muted);
        private readonly ContextCard contextPanel = new ContextCard();
        private readonly Label runtimeTitle = Ui.Label("Pronto a partire",16,null,true);
        private readonly Label runtimeInfo = Ui.Label("Premi Esegui oppure Avanti.",9,Ui.Muted);
        private readonly Label stepLabel = Ui.Label("0 passi",8.5f,Ui.Muted);
        private readonly Panel inputPanel = new Panel();
        private readonly Label inputQuestion = Ui.Label("Inserisci il valore",9,Ui.Ink,true);
        private readonly Label varsLabel = Ui.Label("VARIABILI",8.4f,Ui.Muted,true);
        private readonly Label outputLabel = Ui.Label("OUTPUT",8.4f,Ui.Muted,true);
        private FlowButton copyOutputButton, locateButton;
        private readonly TextBox inputValue = Ui.TextBox();
        private readonly ListView variables = new FlowListView();
        private readonly TextBox output = Ui.TextBox(true);
        private readonly ComboBox speed = new FlowComboBox();
        private readonly Dictionary<string,ListViewItem> runtimeRows = new Dictionary<string,ListViewItem>(StringComparer.OrdinalIgnoreCase);
        private Interpreter displayedRunner;
        private int displayedOutputCount;
        private string displayedInputNode;
        private int displayedInputStep=-1;
        private readonly Label verifySummary = Ui.Label("Controlla il diagramma",15,null,true);
        private readonly Label verifyIntro = Ui.Label("La verifica controlla struttura, espressioni e variabili. Prova dati confronta anche i risultati.",9,Ui.Muted);
        private readonly Panel titlePanel = new Panel();
        private TableLayoutPanel workspaceCenter;
        private Panel inspectorFooter;
        private bool compactWorkspaceHeader,arrangingWorkspaceHeader;
        private List<ValidationIssue> issues = new List<ValidationIssue>();
        private readonly string recoveryPath;
        private readonly FlowRecoverySession recoverySession;
        private byte[] ownedRecovery;
        private readonly LearningStore learningStore=new LearningStore();
        private TableLayoutPanel shell;
        private Panel commandHost,workspaceHost;
        private FlowScrollPanel homePanel;
        private TableLayoutPanel editorBody;
        private FlowchartHome homeScreen;
        private Panel homeFrame;
        private string homeLessonToResume;
        private bool arrangingHome;
        private FlowButton gridButton;
        private bool compactViewBar,arrangingInspector;

        public MainForm(string path = null, bool skipRecovery = false)
        {
            SuspendLayout();
            ignoreRecovery=skipRecovery;
            recoverySession=new FlowRecoverySession();recoveryPath=recoverySession.SnapshotPath;
            Text="Frociart"; StartPosition=FormStartPosition.CenterScreen;
            Size=new Size(1420,900); MinimumSize=new Size(1120,720);
            AutoScaleMode=AutoScaleMode.Dpi; AutoScaleDimensions=new SizeF(96,96);
            Font=Ui.Font(); ForeColor=Ui.Ink; BackColor=Ui.Paper; KeyPreview=true;
            try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BuildInterface();InstallOutputEditor();InitializeDocumentPresentation();InstallDocumentDrop(this);
            runTimer.Interval=650; runTimer.Tick+=delegate { RunTick(); };
            recoveryTimer.Interval=3500; recoveryTimer.Tick+=delegate { SaveRecovery(); };
            Canvas.OtherNodeCount=()=>Project==null?0:FunctionEditing.Graphs(Project).Where(g=>g!=Document).Sum(g=>g.Nodes.Count);
            Canvas.OtherEdgeCount=()=>Project==null?0:FunctionEditing.Graphs(Project).Where(g=>g!=Document).Sum(g=>g.Edges.Count);
            Canvas.DiagramChanging+=delegate { CommitInspector(); PrepareMutation(); };
            Canvas.DiagramChanged+=delegate { MarkDirty(); LoadInspector(); };
            Canvas.CyclePresentationChanged+=delegate { MarkDirty(); };
            Canvas.PrepareClipboardSnapshot=delegate(Diagram snapshot){
                if(loadingInspector)return;var node=snapshot.Nodes.FirstOrDefault(n=>n.Id==editingNodeId);
                if(node!=null){IoInstruction.Apply(node,expression.Text);node.Prompt=prompt.Text;}
            };
            Canvas.SelectionChanged+=delegate { if(!changingDocument){CommitInspector();FlowTransitions.Change(inspector);LoadInspector();if(livePseudocode!=null)livePseudocode.Highlight(ActiveFunctionId,Canvas.SelectedNodeId,false);} };
            Canvas.BreakpointRequested+=delegate{EditBreakpoint();};Canvas.RunToRequested+=delegate{RunToSelected();};Canvas.RunToEnabled=CanRunToSelected;
            Canvas.EditRequested+=delegate { if(SelectedNode!=null&&SelectedNode.Kind==NodeKind.Pause){EditBreakpoint();return;}SetCanvasFocus(false);tabs.SelectedTab=blockTab;expression.Focus();expression.SelectAll(); };
            Canvas.StatusChanged+=(s,e)=>SetStatus(e.Message);
            Canvas.ViewChanged+=delegate { string value=(Canvas.Zoom*100).ToString("0")+"%";if(zoomLabel.Text!=value)zoomLabel.Text=value; };
            Canvas.ConnectionStateChanged+=delegate { UpdateContext(); };
            Ui.ThemeChanged+=OnThemeChanged;
            expression.TextChanged+=delegate { InspectorEdited(); };
            comment.TextChanged+=delegate { InspectorEdited(); };
            prompt.TextChanged+=delegate { InspectorEdited(); };
            titleEdit.TextChanged+=delegate { InspectorEdited(); };
            FormClosing+=OnClosing;
            Activated+=delegate{SyncRuntimeInputChat();};
            Shown+=delegate {
                Ui.FitStartupToScreen(this);
                bool loaded=false;
                if(!String.IsNullOrWhiteSpace(path))loaded=LoadPath(path);
                if(!loaded&&!ignoreRecovery)loaded=TryRecover(false);
                if(!loaded&&String.IsNullOrWhiteSpace(path))ShowHome();
                Canvas.FrameOpeningView();
            };
            SetDocument(Examples.Parity(),null,false);
            AdaptOpeningText(false);
            Ui.ApplyTheme(this);ResumeLayout(true);
        }

        private void BuildInterface()
        {
            shell=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty,Padding=Padding.Empty};
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute,72));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
            Controls.Add(shell);
            var header=new Panel {Dock=DockStyle.Fill,BackColor=Ui.Surface,Margin=Padding.Empty};shell.Controls.Add(header,0,0);
            header.Controls.Add(new BrandMark {Location=new Point(21,16)});
            var brand=Ui.Label("Frociart",20,Ui.Ink,true);brand.SetBounds(73,11,128,32);header.Controls.Add(brand);
            var tagline=Ui.Label("La logica prende forma.",8.8f,Ui.Muted);tagline.SetBounds(75,44,220,19);header.Controls.Add(tagline);
            var version=Ui.Label("v1",8.3f,Ui.Muted,true);version.SetBounds(208,24,60,20);header.Controls.Add(version);version.BringToFront();
            var files=new FlowLayoutPanel {AutoSize=true,WrapContents=false,Height=37,Anchor=AnchorStyles.Top|AnchorStyles.Right,Top=20,FlowDirection=FlowDirection.LeftToRight};
            foreach(string section in new[]{"Progetto","Modifica","Esegui","Codice","Vista","Aiuto"}){
                string name=section;var button=Ui.Button("&"+section+"  ▾",section=="Progetto"?106:section=="Modifica"?100:90,()=>ShowTaskMenu(name,files.Controls.Find("Task"+name,false)[0]));
                button.Name="Task"+section;button.AccessibleName=section;button.AccessibleDescription="Apri i comandi di "+section;files.Controls.Add(button);
            }
            var findCommand=Ui.Button("Trova  Ctrl+K",120,ShowCommandPalette);findCommand.Name="QuickCommands";findCommand.AccessibleName="Cerca un comando";files.Controls.Add(findCommand);
            foreach(FlowButton fileButton in files.Controls)fileButton.PreserveOriginalStyle=false;
            header.Controls.Add(files);header.Resize+=delegate {files.Left=header.ClientSize.Width-files.PreferredSize.Width-18;};
            files.Left=Width-files.PreferredSize.Width-18;
            header.Paint+=(s,e)=>{using(var pen=new Pen(Ui.Line))e.Graphics.DrawLine(pen,0,header.Height-1,header.Width,header.Height-1);};
            commandHost=new Panel {Dock=DockStyle.Fill,BackColor=Ui.Surface,Margin=Padding.Empty};shell.Controls.Add(commandHost,0,1);
            var command=new FlowLayoutPanel {AutoSize=true,WrapContents=false,Location=new Point(20,11),Margin=Padding.Empty};commandHost.Controls.Add(command);
            checkButton=Ui.Button("Verifica",112,()=>Verify(true),true);checkButton.Glyph=ButtonGlyph.Check;command.Controls.Add(checkButton);
            runButton=Ui.Button("Esegui",106,StartRun,true);runButton.Glyph=ButtonGlyph.Play;runButton.Accent=Ui.Green;command.Controls.Add(runButton);
            stepButton=Ui.Button("Avanti",120,SingleStep);stepButton.Glyph=ButtonGlyph.Step;command.Controls.Add(stepButton);
            command.Controls.Add(new Panel{Width=1,Height=22,Margin=new Padding(9,6,16,0),BackColor=Ui.Line,Tag="line"});
            undoButton=Ui.Button("",37,Undo);undoButton.Glyph=ButtonGlyph.Undo;undoButton.AccessibleName="Annulla";
            redoButton=Ui.Button("",37,Redo);redoButton.Glyph=ButtonGlyph.Redo;redoButton.AccessibleName="Ripeti";command.Controls.Add(undoButton);command.Controls.Add(redoButton);
            tips.SetToolTip(undoButton,"Annulla (Ctrl+Z)");tips.SetToolTip(redoButton,"Ripeti (Ctrl+Y)");
            tips.SetToolTip(checkButton,"Verifica struttura, espressioni e variabili (F6)");tips.SetToolTip(stepButton,"Avanza entrando nelle funzioni (F11). F10 avanza senza entrare.");tips.SetToolTip(runButton,"Avvia o riprendi la simulazione (F5)");
            var utility=new FlowLayoutPanel {AutoSize=true,WrapContents=false,Top=11,Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=Padding.Empty};
            var saveQuick=Ui.Button("Salva",90,()=>SaveDocument(false));saveQuick.Name="QuickSave";saveQuick.AccessibleDescription="Salva il progetto modificabile (Ctrl+S).";utility.Controls.Add(saveQuick);
            var exportButton=Ui.Button("Esporta",109,ShowExport);exportButton.Glyph=ButtonGlyph.Export;utility.Controls.Add(exportButton);
            tips.SetToolTip(exportButton,"Esporta il diagramma in PDF, SVG o PNG. Salva conserva il progetto .frociart.");
            commandHost.Controls.Add(utility);
            Action arrangeCommand=delegate {utility.Left=commandHost.ClientSize.Width-utility.PreferredSize.Width-12;};
            commandHost.Resize+=delegate {arrangeCommand();};Shown+=delegate {arrangeCommand();};
            var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=Padding.Empty,Margin=Padding.Empty};
            editorBody=body;
            body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,198));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,324));
            workspaceHost=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};shell.Controls.Add(workspaceHost,0,2);workspaceHost.Controls.Add(body);
            BuildPalette(body);
            var center=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=Ui.Paper,Tag="paper"};
            center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            workspaceCenter=center;center.SizeChanged+=delegate{ArrangeWorkspaceHeader();};Shown+=delegate{ArrangeWorkspaceHeader();};
            center.RowStyles.Add(new RowStyle(SizeType.Absolute,64));center.RowStyles.Add(new RowStyle(SizeType.Absolute,42));center.RowStyles.Add(new RowStyle(SizeType.Absolute,57));center.RowStyles.Add(new RowStyle(SizeType.Percent,100));center.RowStyles.Add(new RowStyle(SizeType.Absolute,44));body.Controls.Add(center,1,0);
            titlePanel.Dock=DockStyle.Fill;titlePanel.BackColor=Ui.Paper;titlePanel.Margin=Padding.Empty;titlePanel.Tag="paper";
            documentTitle.SetBounds(20,10,500,28);documentTitle.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;
            documentTitle.Cursor=Cursors.Hand;documentTitle.Click+=delegate {Canvas.SelectNode(null);tabs.SelectedTab=blockTab;titleEdit.Focus();titleEdit.SelectAll();};
            statsLabel.SetBounds(20,39,500,20);statsLabel.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            titlePanel.Controls.Add(documentTitle);titlePanel.Controls.Add(statsLabel);center.Controls.Add(titlePanel,0,0);
            contextPanel.Dock=DockStyle.Fill;contextPanel.BackColor=Ui.Surface;contextPanel.Margin=new Padding(12,0,12,9);
            contextTitle.SetBounds(12,6,350,20);contextTitle.AutoEllipsis=true;contextDetail.SetBounds(12,27,350,17);contextDetail.AutoEllipsis=true;
            contextPanel.Controls.Add(contextTitle);contextPanel.Controls.Add(contextDetail);
            cancelLinkButton=Ui.Button("Annulla · Esc",112,()=>{Canvas.CancelConnection();Canvas.Focus();});cancelLinkButton.Height=32;cancelLinkButton.Top=8;cancelLinkButton.Visible=false;contextPanel.Controls.Add(cancelLinkButton);
            contextPanel.Resize+=delegate {ArrangeContext();};center.Controls.Add(contextPanel,0,2);BuildFunctionNavigation(center);
            Canvas.Dock=DockStyle.Fill;Canvas.Margin=Padding.Empty;center.Controls.Add(Canvas,0,3);
            var viewbar=new Panel {Dock=DockStyle.Fill,BackColor=Ui.Surface,Margin=Padding.Empty};center.Controls.Add(viewbar,0,4);
            var viewHint=Ui.Label("Rotella premuta: sposta · Destro: menu",8.2f,Ui.Muted);viewHint.AutoEllipsis=true;viewHint.SetBounds(15,13,365,20);viewbar.Controls.Add(viewHint);viewHint.Visible=false;focusCanvasButton=Ui.Button("Solo foglio",112,()=>SetCanvasFocus(!canvasFocus));focusCanvasButton.SetBounds(12,6,112,32);viewbar.Controls.Add(focusCanvasButton);tips.SetToolTip(focusCanvasButton,"F8: nasconde o ripristina i pannelli. Rotella premuta: sposta. Destro: menu. Destro trascinato sulle frecce: taglia i collegamenti. Rotella: zoom.");
            tips.SetToolTip(viewHint,"Trascina il sinistro sul foglio per selezionare. Trascina con la rotella premuta per spostare la vista. Destro: menu. Rotella: zoom. Ctrl o Maiusc aggiunge alla selezione.");
            var views=new FlowLayoutPanel {WrapContents=false,Width=438,Height=34,Top=4,Anchor=AnchorStyles.Top|AnchorStyles.Right};
            gridButton=Ui.Button("Griglia: ON",110,()=>SetGrid(!Canvas.SnapToGrid,true));views.Controls.Add(gridButton);
            tips.SetToolTip(gridButton,"Allinea alla griglia di 24 unità al rilascio. Alt: posizionamento libero. I gruppi mantengono le distanze interne.");SetGrid(learningStore.State.Grid,false);
            views.Controls.Add(Ui.Button("−",33,()=>Canvas.ZoomBy(.85f)));zoomLabel.Size=new Size(46,32);zoomLabel.TextAlign=ContentAlignment.MiddleCenter;zoomLabel.Margin=Padding.Empty;views.Controls.Add(zoomLabel);
            zoomLabel.Cursor=Cursors.Hand;zoomLabel.Click+=delegate {Canvas.Zoom=1f;};tips.SetToolTip(zoomLabel,"Clicca per tornare al 100%");
            views.Controls.Add(Ui.Button("+",33,()=>Canvas.ZoomBy(1.18f)));var whole=Ui.Button("Inquadra",88,()=>Canvas.FitToView());whole.AccessibleDescription="Centra il diagramma e adatta lo zoom senza spostare i blocchi. Per diagrammi molto grandi può servire scorrere.";tips.SetToolTip(whole,"Centra il diagramma e adatta lo zoom. Per diagrammi molto grandi può servire scorrere. I blocchi restano al loro posto.");views.Controls.Add(whole);views.Controls.Add(Ui.Button("Riordina",80,ArrangeDiagram));
            viewbar.Controls.Add(views);Action arrangeView=delegate {
                compactViewBar=viewbar.ClientSize.Width<S(550);gridButton.Visible=!compactViewBar;views.Controls[5].Visible=!compactViewBar;int[] widths=new[]{110,33,46,33,88,80};int total=0;
                for(int i=0;i<views.Controls.Count;i++){var control=views.Controls[i];control.Width=S(widths[i]);control.Margin=control==zoomLabel?Padding.Empty:new Padding(0,0,S(8),0);if(control.Visible)total+=control.Width+control.Margin.Horizontal;}
                views.SetBounds(viewbar.ClientSize.Width-total-S(8),S(4),total,S(34));
                focusCanvasButton.SetBounds(S(12),S(6),Math.Min(S(112),Math.Max(S(56),views.Left-S(20))),S(32));
                focusCanvasButton.Text=canvasFocus?"Pannelli · F8":"Solo foglio";
                focusCanvasButton.AccessibleName=canvasFocus?"Mostra i pannelli, F8":"Nascondi i pannelli, F8";viewHint.Width=Math.Max(S(70),views.Left-S(26));
            };viewbar.Resize+=delegate {arrangeView();};Shown+=delegate {arrangeView();};
            tabs.Dock=DockStyle.Fill;tabs.Font=Ui.Font(9,FontStyle.Bold);tabs.Margin=Padding.Empty;
            tabs.AddPage(blockTab,"Blocco");tabs.AddPage(verifyTab,"Verifica");tabs.AddPage(runTab,"Esecuzione");var sidebar=new TableLayoutPanel {Name="RightSidebar",Dock=DockStyle.Fill,Margin=Padding.Empty,ColumnCount=1,RowCount=2};sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));sidebar.RowStyles.Add(new RowStyle(SizeType.Percent,60));sidebar.RowStyles.Add(new RowStyle(SizeType.Percent,40));sidebar.Controls.Add(tabs,0,0);
            livePseudocode=new PseudocodePreview(delegate{if(Document==null)return null;var snapshot=DiagramIO.Clone(Project);if(!loadingInspector&&!changingDocument){var edited=FunctionEditing.Graph(snapshot,ActiveFunctionId).Nodes.FirstOrDefault(n=>n.Id==editingNodeId);if(edited!=null){IoInstruction.Apply(edited,expression.Text);edited.Prompt=prompt.Text;}}return snapshot;});livePseudocode.TemplatesRequested+=delegate{ShowCodeTemplates(true);};InstallWorkspaceCode(sidebar);body.Controls.Add(sidebar,2,0);
            livePseudocode.BlockRequested+=(function,node)=>{NavigateToOrigin(function,node);};
            BuildInspector();BuildValidation();BuildRuntime();
            var footer=new Panel {Dock=DockStyle.Fill,BackColor=Ui.Surface,Margin=Padding.Empty};status.Dock=DockStyle.Fill;status.Padding=new Padding(16,5,10,0);status.AutoEllipsis=true;footer.Controls.Add(status);shell.Controls.Add(footer,0,3);
            BuildHome();
        }

        private void BuildHome()
        {
            homePanel=new FlowScrollPanel{Dock=DockStyle.Fill,BackColor=Ui.Paper,Tag="paper",Visible=false,Name="HomeScroll",AccessibleName="Pagina iniziale di Frociart"};workspaceHost.Controls.Add(homePanel);
            homeScreen=new FlowchartHome(NewDocument,OpenDocument,ShowPseudocode,EnterEditor,
                delegate(string path){if(ConfirmDiscard())LoadPath(path);},delegate{Ui.SetDarkMode(!Ui.IsDark);},ShowRecovery);
            homeFrame=new Panel{Dock=DockStyle.Top,BackColor=Ui.Paper,Tag="paper",Margin=Padding.Empty};homePanel.Controls.Add(homeFrame);
            homeScreen.Name="HomeContent";homeFrame.Controls.Add(homeScreen);
            homePanel.Resize+=delegate{LayoutHome();};homeFrame.SizeChanged+=delegate{LayoutHome();};homeScreen.SizeChanged+=delegate{LayoutHome();};
        }
        private void LayoutHome()
        {
            if(arrangingHome||homeScreen==null||homePanel.ClientSize.Width<100)return;arrangingHome=true;
            try{float scale=NativeTheme.DpiScale(homePanel);int inset=(int)Math.Round(24*scale);
                int available=homeFrame.ClientSize.Width>0?homeFrame.ClientSize.Width:homePanel.ClientSize.Width;
                int width=Math.Min((int)Math.Round(1160*scale),Math.Max(280,available-inset*2));
                homeScreen.Width=width;homeScreen.PerformLayout();homeFrame.Height=homeScreen.Height+inset*2;
                homePanel.AutoScrollMinSize=new Size(0,homeFrame.Height);
                homeScreen.Left=Math.Max(inset,(available-width)/2);homeScreen.Top=inset;
            }finally{arrangingHome=false;}
        }
        private void RefreshHomeState()
        {
            if(homeScreen==null)return;
            homeScreen.SetState(Project==null?"Il tuo foglio":Project.Title,Project==null?0:FunctionEditing.Graphs(Project).Sum(g=>g.Nodes.Count),dirty,RecentProjects.Read());
            try{homeScreen.SetRecoveryAvailable(!ignoreRecovery&&recoverySession.Find(true).Count>0);}catch(IOException){homeScreen.SetRecoveryAvailable(!ignoreRecovery);}catch(UnauthorizedAccessException){homeScreen.SetRecoveryAvailable(!ignoreRecovery);}
            homeScreen.RefreshPalette();LayoutHome();
        }
        private void ResumeHomeLesson()
        {if(String.IsNullOrEmpty(homeLessonToResume)){ShowLearning();return;}string id=homeLessonToResume;ShowLearningPage(delegate(TutorDialog dialog){dialog.SelectLesson(id);});}
        internal void ShowHome()
        {
            CloseRuntimeConsole();CloseVariableInspector();ResetRuntimeInputChat();
            if(homePanel!=null&&!homePanel.Visible)FlowTransitions.Change(shell);
            CommitInspector();PauseRun();RefreshHomeState();
            editorBody.Visible=false;homePanel.Visible=true;homePanel.BringToFront();commandHost.Visible=false;shell.RowStyles[1].Height=0;LayoutHome();
            FlowTransitions.Enter(homeScreen);
            SetStatus(learningStore.Error??"Crea o riprendi un flowchart. Il lavoro resta sul computer.");
        }
        internal void EnterEditor()
        {
            if(homePanel!=null&&homePanel.Visible)FlowTransitions.Change(shell);
            if(homePanel==null)return;homePanel.Visible=false;editorBody.Visible=true;commandHost.Visible=true;shell.RowStyles[1].Height=S(58);Canvas.Focus();SyncRuntimeInputChat();
        }
        private void ShowLearning(){ShowLearningPage(null);}
        private void ShowLearningPage(Action<TutorDialog> destination)
        {
            CommitInspector();PauseRun();
            using(var dialog=new TutorDialog(new TutorStore(),learningStore)){
                if(destination!=null)dialog.Shown+=delegate{destination(dialog);};
                if(dialog.ShowDialog(this)==DialogResult.OK&&ConfirmDiscard()){SetDocument(dialog.EditorDocument??LearningContent.Current.Practice(dialog.PracticeExample),null,dialog.EditorDocument!=null);EnterEditor();Canvas.FitToView();}
            }
            RefreshHomeState();
        }
        private void ArrangeDiagram()
        {
            CommitInspector();Diagram planned;
            try{planned=DiagramIO.Clone(Document);Tutor.Arrange(planned);DiagramIO.CheckDocument(planned);}
            catch(FlowException ex){ShowError("Impossibile riordinare",ex);return;}
            catch(InvalidDataException ex){ShowError("Impossibile riordinare",ex);return;}
            Canvas.ArrangeAnimated(()=>{PrepareMutation();for(int i=0;i<Document.Nodes.Count;i++){Document.Nodes[i].X=planned.Nodes[i].X;Document.Nodes[i].Y=planned.Nodes[i].Y;}MarkDirty();});
            LoadInspector();SetStatus("Caselle riordinate. Ctrl+Z per annullare.");
        }
        private void ExplainSelectedBlock()
        {
            CommitInspector();var node=SelectedNode;
            if(node==null){SetStatus("Scegli una casella per leggerne la spiegazione.");return;}
            string context=Runner==null?"Non ci sono ancora valori osservati.\n\n":"Se esegui questa casella con i valori dell'ultimo passo:\n\n";
            TutorTextDialog.ShowText(this,"La tua casella, spiegata",context+TutorDiagnostics.Explain(node,Runner==null||Runner.CurrentFunctionId!=ActiveFunctionId?null:Runner.Variables,FunctionModel.Catalog(Project),FunctionEditing.Find(Project,ActiveFunctionId)));
        }
        private void ShowLegacyLearning()
        {
            CommitInspector();PauseRun();
            using(var dialog=new LearningDialog(learningStore))
                if(dialog.ShowDialog(this)==DialogResult.OK&&ConfirmDiscard())SetDocument(LearningContent.Current.Practice(dialog.PracticeExample),null,false);
            RefreshHomeState();
        }
        private void SetGrid(bool enabled,bool persist)
        {
            Canvas.SnapToGrid=enabled;gridButton.Text=enabled?"Griglia: ON":"Griglia: OFF";gridButton.AccessibleName=enabled?"Snap alla griglia attivo":"Snap alla griglia disattivo";
            if(persist){learningStore.State.Grid=enabled;learningStore.Save();SetStatus(learningStore.Error??(enabled?"Griglia attiva: allineamento al rilascio. Alt per spostare liberamente.":"Griglia disattivata: posizionamento libero."));}
        }

        private FlowScrollPanel ioPalette;
        private void BuildPalette(TableLayoutPanel body)
        {
            var palette=new FlowScrollPanel {Dock=DockStyle.Fill,BackColor=Ui.Surface,Margin=Padding.Empty,AutoScroll=true};body.Controls.Add(palette,0,0);ioPalette=palette;PopulatePalette();
        }
        private void PopulatePalette(){PopulateWorkspacePalette();}

        private void BuildInspector()
        {
            inspector.Dock=DockStyle.Fill;inspector.AutoScroll=true;blockTab.Controls.Add(inspector);
            inspector.ClientSizeChanged+=delegate{if(arrangingInspector||loadingInspector||Document==null)return;bool group=Canvas.SelectionCount>1;LayoutInspector(SelectedNode,group,SelectedNode==null&&!group&&!String.IsNullOrEmpty(Canvas.SelectedEdgeId));if(expression.Focused)inspector.ScrollControlIntoView(expression);};
            selectionTitle.SetBounds(17,18,279,35);inspector.Controls.Add(selectionTitle);
            selectionHelp.SetBounds(18,57,273,51);selectionHelp.AutoEllipsis=true;inspector.Controls.Add(selectionHelp);
            groupHelp.SetBounds(18,214,272,200);groupHelp.Visible=false;inspector.Controls.Add(groupHelp);
            var titleCaption=Ui.Label("NOME DEL DIAGRAMMA",8.4f,Ui.Muted,true);titleCaption.SetBounds(18,133,274,24);titleCaption.Tag="title";inspector.Controls.Add(titleCaption);
            titleEdit.SetBounds(18,159,272,30);titleEdit.MaxLength=150;titleEdit.Tag="title";inspector.Controls.Add(titleEdit);
            var blankHelp=Ui.Label("1  Premi + su Inizio o sul blocco precedente.\n\n2  Clicca sul foglio e scegli la casella da inserire nel percorso.\n\n3  Scrivi a destra, poi premi Verifica ed Esegui. Per una casella scollegata usa i pulsanti a sinistra.",9.7f,Ui.Muted);blankHelp.SetBounds(18,219,272,192);blankHelp.Tag="title";inspector.Controls.Add(blankHelp);
            expressionLabel.SetBounds(18,127,272,24);inspector.Controls.Add(expressionLabel);
            expression.SetBounds(18,157,272,107);expression.AcceptsTab=false;expression.MaxLength=DiagramIO.MaxBlockText;inspector.Controls.Add(expression);
            cycleButton=Ui.Button("Cicli: riepilogo e modifica…",272,ShowCycleManager);inspector.Controls.Add(cycleButton);
            comment.MaxLength=2048;comment.Font=new Font("Segoe UI",10,FontStyle.Regular,GraphicsUnit.Point);comment.AccessibleName="Commento del blocco";commentLabel.AccessibleName="Commento facoltativo, non viene eseguito";
            inspector.Controls.Add(commentLabel);inspector.Controls.Add(comment);
            tips.SetToolTip(comment,"Spiega questo passaggio. Il commento appare nel diagramma e non viene eseguito.");
            expression.KeyDown+=(s,e)=>{if(e.Control&&e.KeyCode==Keys.Enter){CommitInspector();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.Space){e.Handled=e.SuppressKeyPress=true;FunctionLibraryDialog.InsertInto(this,expression,true);}};
            functionButton=Ui.Button("Variabili e funzioni…",272,InsertExpressionFunction);functionButton.Name="FunctionLibrary";inspector.Controls.Add(functionButton);
            promptLabel.SetBounds(18,277,270,22);inspector.Controls.Add(promptLabel);prompt.SetBounds(18,303,272,28);prompt.MaxLength=1024;inspector.Controls.Add(prompt);
            syntaxHelp.SetBounds(18,282,272,96);inspector.Controls.Add(syntaxHelp);
            applyButton=Ui.Button("Aggiorna blocco",272,()=>{CommitInspector();LoadInspector();SetStatus("Contenuto aggiornato. Premi Verifica per controllarlo.");},true);applyButton.SetBounds(18,389,272,35);inspectorFooter=new Panel{Dock=DockStyle.Bottom,Height=50,Padding=new Padding(18,6,18,8),BackColor=Ui.Surface,TabIndex=1};applyButton.Dock=DockStyle.Fill;applyButton.TabIndex=0;inspectorFooter.Controls.Add(applyButton);blockTab.Controls.Add(inspectorFooter);inspector.BringToFront();inspector.TabIndex=0;tips.SetToolTip(applyButton,"Aggiorna il contenuto (Ctrl+Invio). Le modifiche vengono applicate anche quando cambi selezione.");
            linksLabel.SetBounds(18,450,272,22);inspector.Controls.Add(linksLabel);
            nextButton=Ui.Button("+  Collega o aggiungi",272,()=>BeginLink("next"));nextButton.SetBounds(18,480,272,38);inspector.Controls.Add(nextButton);
            yesButton=Ui.Button("Sì  →  Collega",272,()=>BeginLink("true"));yesButton.Accent=Ui.Green;yesButton.SetBounds(18,480,272,38);inspector.Controls.Add(yesButton);
            noButton=Ui.Button("No  →  Collega",272,()=>BeginLink("false"));noButton.Accent=Ui.Red;noButton.SetBounds(18,524,272,38);inspector.Controls.Add(noButton);
            yesButton.UseMnemonic=noButton.UseMnemonic=false;
            branchStyleButton=Ui.Button("Etichette e colori delle uscite",272,EditBranchStyles);inspector.Controls.Add(branchStyleButton);
            disconnectButton=Ui.Button("Scollega le uscite",272,Disconnect);disconnectButton.SetBounds(18,574,272,34);inspector.Controls.Add(disconnectButton);
            foreach(var linkButton in new[]{nextButton,yesButton,noButton})tips.SetToolTip(linkButton,"Clicca un blocco di arrivo, oppure un punto vuoto per scegliere un nuovo blocco già collegato. Esc annulla.");
            duplicateButton=Ui.Button("Duplica",131,()=>{CommitInspector();Canvas.DuplicateSelected();});duplicateButton.SetBounds(18,631,131,34);inspector.Controls.Add(duplicateButton);
            deleteButton=Ui.Button("Elimina",131,()=>{CommitInspector();Canvas.DeleteSelection();});deleteButton.Accent=Ui.Red;deleteButton.SetBounds(159,631,131,34);inspector.Controls.Add(deleteButton);
            inspector.AutoScrollMinSize=new Size(0,688);
        }

        private void BuildValidation()
        {
            var host=new Panel {Dock=DockStyle.Fill,AutoScroll=false};verifyTab.Controls.Add(host);
            var head=new Panel {Dock=DockStyle.Top,Height=165};verifySummary.SetBounds(17,17,279,51);head.Controls.Add(verifySummary);
            verifyIntro.SetBounds(18,73,274,70);head.Controls.Add(verifyIntro);host.Controls.Add(head);
            issuePanel.Dock=DockStyle.Fill;issuePanel.AutoScroll=true;host.Controls.Add(issuePanel);issuePanel.BringToFront();
            ((ValidationIssuesView)issuePanel).IssueActivated+=issue=>{
                if(String.IsNullOrEmpty(issue.NodeId))return;
                NavigateToOrigin(issue.FunctionId,issue.NodeId);
                tabs.SelectedTab=blockTab;SetStatus(issue.Message+" "+issue.Suggestion);
            };
            var foot=new Panel{Dock=DockStyle.Bottom,Height=51};var b=Ui.Button("Ricontrolla",272,()=>Verify(true),true);b.SetBounds(18,7,272,35);foot.Controls.Add(b);host.Controls.Add(foot);foot.BringToFront();
        }

        private void BuildRuntime()
        {
            BuildRuntimeDeck();
        }

        internal void SetDocument(Diagram diagram, string path, bool isDirty)
        {
            sourceDocumentEpoch++;
            FinishDocumentPresentation();
            ResetRuntimeInputChat();
            CloseRuntimeConsole();CloseVariableInspector();watchExpressions.Clear();ResetNavigationHistory();
            if(!ignoreRecovery)DeleteRecovery();
            EnterEditor();
            changingDocument=true;loadingInspector=true;runTimer.Stop();automatic=false;Runner=null;
            ownedRecovery=null;ActiveFunctionId=null;Project=diagram;graphViews.Clear();currentPath=path;dirty=isDirty;editingNodeId=null;undo.Clear();redo.Clear();issues.Clear();
            Canvas.Diagram=Document;Canvas.SelectNode(null);Canvas.SelectEdge(null);Canvas.CurrentNodeId=null;Canvas.CurrentEdgeId=null;Canvas.IssueNodeIds=new HashSet<string>();tabs.SelectedTab=blockTab;
            changingDocument=false;loadingInspector=false;LoadInspector();UpdateDocumentLabels();ClearValidation();RefreshRuntime();RefreshFunctionChoices();
            BeginInvokeIfReady(()=>Canvas.FrameOpeningView());SetStatus("Trascina sul foglio per selezionare. F6 verifica · F11 avanza · F10 non entra nelle funzioni.");
            if(isDirty&&!ignoreRecovery)recoveryTimer.Start();
        }

        private void BeginInvokeIfReady(Action action) { if(IsHandleCreated)BeginInvoke(action); }
        private FlowNode SelectedNode { get {return Document==null?null:Document.Nodes.FirstOrDefault(n=>n.Id==Canvas.SelectedNodeId);} }
        private void LoadInspector()
        {
            inspector.SuspendLayout();
            try
            {
            loadingInspector=true;var node=SelectedNode;editingNodeId=node==null?null:node.Id;
            bool group=Canvas.SelectionCount>1;
            bool edge=node==null&&!group&&!String.IsNullOrEmpty(Canvas.SelectedEdgeId);
            editingTitle=node==null&&!edge&&!group;
            foreach(Control c in inspector.Controls)if((string)c.Tag=="title")c.Visible=node==null&&!edge&&!group;
            groupHelp.Visible=group;cycleButton.Visible=node!=null&&FlowCycle.Find(Document).Any(c=>c.Members.Contains(node.Id));
            titleEdit.Text=Project==null?"":Project.Title;
            expression.Visible=expressionLabel.Visible=syntaxHelp.Visible=applyButton.Visible=comment.Visible=commentLabel.Visible=node!=null;inspectorFooter.Visible=node!=null;
            functionButton.Visible=node!=null&&node.Kind!=NodeKind.Start&&node.Kind!=NodeKind.End&&node.Kind!=NodeKind.Note;
            comment.Visible=commentLabel.Visible=node!=null&&node.Kind!=NodeKind.Note;
            prompt.Visible=promptLabel.Visible=node!=null&&node.Kind==NodeKind.Input;
            nextButton.Visible=node!=null&&node.Kind!=NodeKind.End&&node.Kind!=NodeKind.Decision&&node.Kind!=NodeKind.Note;
            yesButton.Visible=noButton.Visible=branchStyleButton.Visible=node!=null&&node.Kind==NodeKind.Decision;
            linksLabel.Visible=disconnectButton.Visible=node!=null&&node.Kind!=NodeKind.End&&node.Kind!=NodeKind.Note;
            duplicateButton.Visible=node!=null||group;deleteButton.Visible=node!=null||edge||group;
            SetBox(duplicateButton,18,group?146:631,131,34);SetBox(deleteButton,159,group||edge?146:631,131,34);
            duplicateButton.Text=group?"Duplica gruppo":"Duplica";deleteButton.Text=group?"Elimina gruppo":"Elimina";
            if(node==null)
            {
                selectionTitle.Text=group?Canvas.SelectionCount+" blocchi selezionati":edge?"Collegamento":"Il tuo diagramma";
                selectionHelp.Text=group?"La selezione si muove e si duplica insieme. I collegamenti interni vengono mantenuti.":edge?"Questo collegamento unisce due blocchi. Premi Elimina per rimuoverlo.":"Dai un nome al progetto e costruisci il tuo algoritmo sul foglio.";
                expression.Text="";prompt.Text="";comment.Text="";
            }
            else
            {
                selectionTitle.Text=node.Kind==NodeKind.Input||node.Kind==NodeKind.Output?"Input / Output":Ui.KindName(node.Kind);selectionTitle.ForeColor=Ui.KindColor(node.Kind);
                var problem=issues.FirstOrDefault(i=>i.NodeId==node.Id&&i.Severity==Severity.Error);
                selectionHelp.Text=problem==null?KindDescription(node.Kind):problem.Message;selectionHelp.ForeColor=problem==null?Ui.Muted:Ui.Red;expression.Text=IoInstruction.Editor(node);prompt.Text=node.Prompt??"";comment.Text=CommentText(node.Comment).Replace("\n","\r\n");
                functionButton.Text=FlowTurtle.IsTurtle(node.Kind)?"Imposta il disegno…":FileBlocks.IsFile(node.Kind)?"Imposta il file…":node.Kind==NodeKind.Declare?"Nome, tipo e vettore…":"Variabili e funzioni…";
                expressionLabel.Text=node.Kind==NodeKind.Note?"TESTO DELLA NOTA":node.Kind==NodeKind.Pause?"CONDIZIONE DI PAUSA · VUOTA = SEMPRE":FlowTurtle.IsTurtle(node.Kind)?"ISTRUZIONE TARTARUGA":FileBlocks.IsFile(node.Kind)?"ISTRUZIONE FILE":IoInstruction.IsIo(node.Kind)?"ISTRUZIONE · LEGGI O STAMPA":node.Kind==NodeKind.Decision?"CONDIZIONE":node.Kind==NodeKind.Declare?"VARIABILI E TIPO":node.Kind==NodeKind.Process?"OPERAZIONI":node.Kind==NodeKind.Call?"CHIAMATA DI FUNZIONE":"ETICHETTA";
                syntaxHelp.Text=SyntaxHint(node.Kind);syntaxHelp.Top=S(node.Kind==NodeKind.Input?344:282);syntaxHelp.Height=S(node.Kind==NodeKind.Input?39:96);
                nextButton.Text=LinkLabel("next","+  Collega o aggiungi");yesButton.Text=LinkLabel("true",node.GetBranchLabel("true")+"  →  Collega o aggiungi");noButton.Text=LinkLabel("false",node.GetBranchLabel("false")+"  →  Collega o aggiungi");
                yesButton.Accent=ColorTranslator.FromHtml(node.GetBranchColor("true"));noButton.Accent=ColorTranslator.FromHtml(node.GetBranchColor("false"));
                disconnectButton.Enabled=Document.Edges.Any(e=>e.From==node.Id);
            }
            if(node==null){selectionTitle.ForeColor=Ui.Ink;selectionHelp.ForeColor=Ui.Muted;}
            UpdateOutputEditor(node);LayoutInspector(node,group,edge);
            }
            finally{loadingInspector=false;inspector.ResumeLayout(true);}
            applyButton.Enabled=InspectorHasChanges();UpdateContext();
        }

        private void LayoutInspector(FlowNode node,bool group,bool edge)
        {
            if(arrangingInspector)return;arrangingInspector=true;
            try{
            inspector.SuspendLayout();inspector.AutoScrollPosition=Point.Empty;
            if(node!=null)
            {
                int top=117;if(cycleButton.Visible){SetBox(cycleButton,18,top,272,32);top+=42;}
                SetBox(expressionLabel,18,top,272,21);top+=26;
                int height=node.Kind==NodeKind.Note?156:node.Kind==NodeKind.Process?96:node.Kind==NodeKind.Decision?72:node.Kind==NodeKind.Output?76:44;
                height=Math.Min(height,Math.Max(28,(int)(inspector.ClientSize.Height/Math.Max(1f,dpiScale))-12));
                SetBox(expression,18,top,272,height);top+=height+12;
                if(functionButton.Visible){SetBox(functionButton,18,top,272,32);top+=40;}
                if(prompt.Visible)
                {SetBox(promptLabel,18,top,272,21);top+=26;SetBox(prompt,18,top,272,28);top+=40;}
                if(outputNewline.Visible){SetBox(outputNewline,18,top,272,40);top+=48;}
                if(comment.Visible){SetBox(commentLabel,18,top,272,21);top+=26;SetBox(comment,18,top,272,64);top+=76;}
                int hintHeight=node.Kind==NodeKind.Input?72:node.Kind==NodeKind.Start||node.Kind==NodeKind.End?38:node.Kind==NodeKind.Process?83:72;
                inspectorFooter.Height=S(50);inspectorFooter.Padding=new Padding(S(18),S(6),S(18),S(8));
                SetBox(syntaxHelp,18,top,272,hintHeight);top+=hintHeight+10;
                if(node.Kind!=NodeKind.End&&node.Kind!=NodeKind.Note)
                {
                    SetBox(linksLabel,18,top,272,20);top+=25;
                    SetBox(nextButton,18,top,272,36);SetBox(yesButton,18,top,272,36);top+=44;
                    if(node.Kind==NodeKind.Decision){SetBox(noButton,18,top,272,36);top+=44;SetBox(branchStyleButton,18,top,272,30);top+=38;}
                    top+=11;
                }
                if(node.Kind!=NodeKind.End&&node.Kind!=NodeKind.Note){SetBox(disconnectButton,18,top,272,31);top+=42;}
                SetBox(duplicateButton,18,top,131,34);SetBox(deleteButton,159,top,131,34);
                inspector.AutoScrollMinSize=new Size(0,S(top+50));
            }
            else
            {
                SetBox(duplicateButton,18,146,131,34);SetBox(deleteButton,159,146,131,34);
                inspector.AutoScrollMinSize=new Size(0,S(group?443:edge?210:433));
            }
            inspector.ResumeLayout();
            }finally{arrangingInspector=false;}
        }

        private void ArrangeWorkspaceHeader()
        {
            if(arrangingWorkspaceHeader||workspaceCenter==null||cancelLinkButton==null||canvasFocus)return;
            arrangingWorkspaceHeader=true;
            try{
                compactWorkspaceHeader=ClientSize.Height<S(790);
                workspaceCenter.RowStyles[0].Height=S(compactWorkspaceHeader?36:64);
                workspaceCenter.RowStyles[1].Height=S(42);
                workspaceCenter.RowStyles[2].Height=S(compactWorkspaceHeader?38:57);
                contextPanel.Margin=new Padding(S(12),0,S(12),S(compactWorkspaceHeader?4:9));
                documentTitle.AutoEllipsis=statsLabel.AutoEllipsis=true;
                int width=Math.Max(1,titlePanel.ClientSize.Width-S(40));
                if(compactWorkspaceHeader){int stats=Math.Min(Math.Max(S(150),width-S(150)),TextRenderer.MeasureText(statsLabel.Text,statsLabel.Font).Width+S(4));documentTitle.SetBounds(S(12),S(4),Math.Max(S(100),width-stats),S(28));statsLabel.SetBounds(documentTitle.Right+S(12),S(7),Math.Max(1,titlePanel.ClientSize.Width-documentTitle.Right-S(24)),S(22));}
                else{documentTitle.SetBounds(S(20),S(10),width,S(28));statsLabel.SetBounds(S(20),S(39),width,S(20));}
                tips.SetToolTip(documentTitle,documentTitle.Text);tips.SetToolTip(statsLabel,statsLabel.Text);ArrangeContext();
            }finally{arrangingWorkspaceHeader=false;}
        }
        private void ArrangeContext()
        {
            if(cancelLinkButton==null)return;
            cancelLinkButton.Left=contextPanel.ClientSize.Width-cancelLinkButton.Width-S(8);
            int width=Math.Max(S(50),contextPanel.ClientSize.Width-S(24)-(cancelLinkButton.Visible?cancelLinkButton.Width+S(12):0));
            cancelLinkButton.Top=Math.Max(0,(contextPanel.ClientSize.Height-cancelLinkButton.Height)/2);
            if(compactWorkspaceHeader){int titleWidth=Math.Max(S(80),Math.Min(width/2,TextRenderer.MeasureText(contextTitle.Text,contextTitle.Font).Width+S(6)));contextTitle.SetBounds(S(12),Math.Max(0,(contextPanel.Height-S(20))/2),titleWidth,S(20));contextDetail.SetBounds(S(24)+titleWidth,Math.Max(0,(contextPanel.Height-S(17))/2),Math.Max(0,width-titleWidth-S(12)),S(17));}
            else{contextTitle.SetBounds(S(12),S(6),width,S(20));contextDetail.SetBounds(S(12),S(27),width,S(17));}
            contextTitle.AccessibleDescription=contextDetail.Text;tips.SetToolTip(contextTitle,contextTitle.Text+" · "+contextDetail.Text);
        }
        private void UpdateContext()
        {
            if(cancelLinkButton==null)return;
            contextPanel.Emphasized=Canvas.IsConnecting;
            contextPanel.BackColor=Canvas.IsConnecting?Ui.Tint:Ui.Surface;
            contextTitle.BackColor=contextDetail.BackColor=contextPanel.BackColor;
            cancelLinkButton.Visible=Canvas.IsConnecting;
            if(Canvas.IsConnecting)
            {
                var source=Document.Nodes.FirstOrDefault(n=>n.Id==Canvas.ConnectionSourceId);
                string branch=source!=null&&source.Kind==NodeKind.Decision?"Ramo "+source.GetBranchLabel(Canvas.ConnectionBranch):"Prossimo passaggio";
                contextTitle.Text=Canvas.IsBlockPickerOpen?"Scegli il blocco da aggiungere":branch+" · scegli dove continuare";
                contextDetail.Text="Clicca un blocco, oppure il foglio per crearne uno collegato.";
                contextTitle.ForeColor=Ui.Blue;
            }
            else
            {
                contextTitle.ForeColor=Ui.Ink;
                contextTitle.Text=Canvas.SelectionCount>1?Canvas.SelectionCount+" blocchi selezionati":SelectedNode!=null?Ui.KindName(SelectedNode.Kind)+" selezionato":"Trascina il sinistro per selezionare";
            contextDetail.Text=Canvas.SelectionCount>1?"Trascinane uno per spostare il gruppo · Ctrl+D duplica":SelectedNode!=null?(SelectedNode.Kind==NodeKind.Note?"Modifica a destra · Trascina per spostare la nota":"Modifica a destra · Premi + sul blocco per continuare"):"Rotella premuta: sposta · Destro: menu · Trascina col destro sulle frecce: taglia";
            }
            tips.SetToolTip(contextDetail,contextDetail.Text);ArrangeContext();contextPanel.Invalidate();
        }
        private void OnThemeChanged(object sender,EventArgs e)
        {if(IsDisposed||Disposing)return;if(homeScreen!=null)homeScreen.RefreshPalette();foreach(var menu in taskMenus.Values)Ui.StyleMenu(menu);UpdateContext();Invalidate(true);}
        private void LayoutTabs()
        {
            tabs.SetScale(dpiScale);
        }

        private string LinkLabel(string branch,string fallback)
        {
            var e=Document.Edges.FirstOrDefault(x=>x.From==Canvas.SelectedNodeId&&x.Branch==branch);
            var n=e==null?null:Document.Nodes.FirstOrDefault(x=>x.Id==e.To);
            if(n==null)return fallback;
            string t=(n.Text??Ui.KindName(n.Kind)).Replace("\r","").Replace("\n"," ");if(t.Length>19)t=t.Substring(0,17)+"…";
            return (SelectedNode!=null&&SelectedNode.Kind==NodeKind.Decision?SelectedNode.GetBranchLabel(branch):"Prossimo")+" → "+t+" · Cambia";
        }
        private static string KindDescription(NodeKind k)
        {
            if(k==NodeKind.Note)return "Una spiegazione libera sul foglio. Non si collega alle frecce e non viene eseguita.";
            if(FlowTurtle.IsTurtle(k))return FlowTurtle.Description(k);if(FileBlocks.IsFile(k))return FileBlocks.Description(k);
            switch(k)
            {
                case NodeKind.Start:return "Ogni diagramma ha un solo punto di partenza. Collegalo al primo passaggio.";
                case NodeKind.End:return "Il programma termina quando arriva qui. Puoi usare più blocchi Fine.";
                case NodeKind.Input:return "Leggi: chiede un valore a chi esegue il programma e lo salva in una variabile.";
                case NodeKind.Output:return "Stampa: mostra un valore, un calcolo oppure un messaggio tra virgolette.";
                case NodeKind.Declare:return "Scegli un tipo per una o più variabili. I vettori conservano un numero fisso di elementi.";
                case NodeKind.Pause:return "La simulazione si ferma qui se la pausa è attiva. Doppio clic per attivarla o disattivarla.";
                case NodeKind.Call:return "Richiama una funzione con i suoi parametri. Al termine il programma continua dal blocco successivo.";
                case NodeKind.Decision:return "Valuta una condizione e sceglie uno dei due percorsi. Puoi dare un nome e un colore a ogni uscita.";
                default:return "Assegna valori e fai calcoli. Per più istruzioni, vai a capo.";
            }
        }
        private static string SyntaxHint(NodeKind k)
        {
            if(k==NodeKind.Note)return "Scrivi liberamente, anche su più righe. Puoi spostare, copiare ed esportare la nota insieme al diagramma.";
            if(FlowTurtle.IsTurtle(k))return FlowTurtle.Help(k);if(FileBlocks.IsFile(k))return FileBlocks.Help(k);
            switch(k)
            {
                case NodeKind.Declare:return "Esempi: totale, resto: INTERO\nprezzi: REALE[10]\n\nTipi: INTERO, REALE, TESTO, LOGICO.";
                case NodeKind.Pause:return "Condizione facoltativa: contatore >= 5. Lascia vuoto per una pausa sempre. Doppio clic apre le impostazioni; F9 attiva o disattiva la pausa.";
                case NodeKind.Call:return "Esempio: saluta(nome)\n\nPer usare il risultato, scrivi totale = calcola(a, b) in un blocco Operazione.";
                case NodeKind.Process:return "Esempio:\ntotale = totale + numero\ni = i + 1\n\nUsa il punto per i decimali: 2.5";
                case NodeKind.Decision:return "Esempi: n > 0   oppure   n % 2 == 0\n\nConfronti: ==  !=  <  <=  >  >=\nPiù condizioni: n >= 1 e n <= 10";
                case NodeKind.Input:return IoInstruction.Hint;
                case NodeKind.Output:return IoInstruction.Hint;
                default:return "L'etichetta è descrittiva: non contiene calcoli.";
            }
        }

        private static string CommentText(string text){return (text??"").Replace("\r\n","\n").Replace('\r','\n');}
        internal void CommitInspector()
        {
            if(loadingInspector||changingDocument||Document==null)return;
            var node=Document.Nodes.FirstOrDefault(n=>n.Id==editingNodeId);
            if(node!=null)
            {
                if(!IoInstruction.Changed(node,expression.Text)&&node.Prompt==prompt.Text&&CommentText(node.Comment)==CommentText(comment.Text))return;
                if(node.Kind==NodeKind.Pause&&node.Prompt==prompt.Text&&CommentText(node.Comment)==CommentText(comment.Text)){bool valid=true;try{if(node.Breakpoint)new FlowDebugger.DebugCondition(expression.Text);}catch(FlowException){valid=false;}if(valid){SetBreakpoint(node,node.Breakpoint,expression.Text);return;}}
                PrepareMutation();Canvas.ApplyTextEdit(node,()=>{IoInstruction.Apply(node,expression.Text);node.Prompt=prompt.Text;node.Comment=comment.Text.Length==0?null:CommentText(comment.Text);});MarkDirty();Canvas.Invalidate();if(applyButton!=null)applyButton.Enabled=false;
            }
            else if(editingTitle)
            {
                string title=String.IsNullOrWhiteSpace(titleEdit.Text)?"Senza titolo":titleEdit.Text.Trim();
                if(Project.Title!=title){PrepareMutation();Project.Title=title;MarkDirty();}
            }
        }
        private bool InspectorHasChanges()
        {
            if(loadingInspector||changingDocument||Document==null)return false;
            var node=Document.Nodes.FirstOrDefault(n=>n.Id==editingNodeId);
            if(node!=null)return IoInstruction.Changed(node,expression.Text)||node.Prompt!=prompt.Text||CommentText(node.Comment)!=CommentText(comment.Text);
            if(!editingTitle)return false;
            string title=String.IsNullOrWhiteSpace(titleEdit.Text)?"Senza titolo":titleEdit.Text.Trim();
            return Project.Title!=title;
        }
        private void InspectorEdited()
        {
            if(loadingInspector||changingDocument||Document==null)return;
            Canvas.PreviewText(editingNodeId,expression.Text);Canvas.PreviewComment(editingNodeId,CommentText(comment.Text));var node=Document.Nodes.FirstOrDefault(n=>n.Id==editingNodeId);if(node!=null){var detected=IoInstruction.VisualKind(node);selectionTitle.ForeColor=Ui.KindColor(detected);prompt.Visible=promptLabel.Visible=detected==NodeKind.Input;}
            bool wasOutput=outputNewline.Visible;UpdateOutputEditor(node);if(node!=null&&wasOutput!=outputNewline.Visible)LayoutInspector(node,false,false);
            if(applyButton!=null)applyButton.Enabled=InspectorHasChanges();
            UpdateDocumentLabels();
            if(!ignoreRecovery){recoveryTimer.Stop();recoveryTimer.Start();}
        }
        private void PrepareMutation()
        {
            if(changingDocument||Document==null)return;
            undo.Push(new ProjectHistory(Project,ActiveFunctionId));if(undo.Count>80){var kept=undo.Take(80).Reverse().ToArray();undo.Clear();foreach(var d in kept)undo.Push(d);}
            redo.Clear();ResetRun(false);issues.Clear();Canvas.IssueNodeIds=new HashSet<string>();ClearValidation();
        }
        private void MarkDirty(){sourceDocumentEpoch++;FlowAnnotations.Promote(Project);Canvas.NotifyFoldModelChanged();Canvas.NotifyAccessibleDocumentChanged();dirty=true;UpdateDocumentLabels();RefreshNavigationAvailability();if(!ignoreRecovery){recoveryTimer.Stop();recoveryTimer.Start();}}
        private void UpdateDocumentLabels()
        {
            if(livePseudocode!=null)livePseudocode.Schedule();
            bool modified=dirty||InspectorHasChanges();
            documentTitle.Text=Project.Title+(modified?"  •":"");statsLabel.Text=Document.Nodes.Count+(Document.Nodes.Count==1?" blocco   ·   ":" blocchi   ·   ")+Document.Edges.Count+(Document.Edges.Count==1?" collegamento   ·   ":" collegamenti   ·   ")+(currentPath==null?"Progetto locale":Path.GetFileName(currentPath));
            ArrangeWorkspaceHeader();
            Text=(modified?"* ":"")+Project.Title+" — Frociart v1";undoButton.Enabled=undo.Count>0;redoButton.Enabled=redo.Count>0;
        }
        private void Undo()
        {
            Canvas.CancelConnection();if(undo.Count==0)return;if(undo.Peek().Appearance==null)CommitInspector();var target=undo.Pop();var current=new ProjectHistory(Project,ActiveFunctionId);if(target.Appearance!=null)current.Appearance=Canvas.AppearanceStyle;redo.Push(current);ApplyHistory(target);SetStatus("Modifica annullata.");
        }
        private void Redo(){Canvas.CancelConnection();if(redo.Count==0)return;if(redo.Peek().Appearance==null)CommitInspector();if(redo.Count==0)return;var d=redo.Pop();var current=new ProjectHistory(Project,ActiveFunctionId);if(d.Appearance!=null)current.Appearance=Canvas.AppearanceStyle;undo.Push(current);ApplyHistory(d);SetStatus("Modifica ripetuta.");}
        private void ApplyHistory(ProjectHistory history)
        {
            if(history.Appearance!=null){ApplyDesignPositions(history.Project,history.Appearance);return;}
            var selected=Canvas.SelectedNodeIds.ToArray();string selectedEdge=Canvas.SelectedEdgeId;
            changingDocument=true;ResetRun(false);Project=history.Project;ActiveFunctionId=FunctionEditing.Find(Project,history.FunctionId)==null?null:history.FunctionId;var d=Document;Canvas.Diagram=d;Canvas.SelectNodes(selected);RefreshFunctionChoices();
            if(!String.IsNullOrEmpty(selectedEdge)&&d.Edges.Any(edge=>edge.Id==selectedEdge))Canvas.SelectEdge(selectedEdge);
            editingNodeId=null;changingDocument=false;
            issues.Clear();Canvas.IssueNodeIds=new HashSet<string>();ClearValidation();LoadInspector();MarkDirty();Canvas.Invalidate();
        }
        private void BeginLink(string branch)
        {CommitInspector();if(SelectedNode!=null){Canvas.BeginConnect(SelectedNode.Id,branch);Canvas.Focus();SetStatus("Clicca un blocco, oppure un punto vuoto per scegliere un nuovo blocco già collegato. Esc annulla.");}}
        private void EditBranchStyles()
        {
            CommitInspector();var node=SelectedNode;if(node==null||node.Kind!=NodeKind.Decision)return;
            using(var dialog=new BranchStyleDialog(node))
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                if(node.GetBranchLabel("true")==dialog.TrueLabel&&node.GetBranchLabel("false")==dialog.FalseLabel&&node.GetBranchColor("true")==dialog.TrueColor&&node.GetBranchColor("false")==dialog.FalseColor)return;
                PrepareMutation();node.TrueLabel=dialog.TrueLabel;node.FalseLabel=dialog.FalseLabel;node.TrueColor=dialog.TrueColor;node.FalseColor=dialog.FalseColor;
                MarkDirty();Canvas.Invalidate();LoadInspector();SetStatus("Etichette e colori aggiornati.");
            }
        }
        private void Disconnect()
        {
            CommitInspector();if(SelectedNode==null)return;string id=SelectedNode.Id;
            if(!Document.Edges.Any(e=>e.From==id))return;PrepareMutation();Document.Edges.RemoveAll(e=>e.From==id);MarkDirty();LoadInspector();Canvas.Invalidate();SetStatus("Collegamenti in uscita rimossi.");
        }

        internal bool Verify(bool show)
        {
            SetCanvasFocus(false);
            CommitInspector();issues=Validator.Validate(Project);Canvas.IssueNodeIds=new HashSet<string>(issues.Where(i=>i.Severity==Severity.Error&&!String.IsNullOrEmpty(i.NodeId)).Select(i=>i.NodeId));Canvas.Invalidate();
            RenderIssues();if(show)tabs.SelectedTab=verifyTab;
            int errors=issues.Count(x=>x.Severity==Severity.Error);SetStatus(errors==0?"Nessun errore bloccante. Esegui o prova i tuoi dati per controllare il risultato.":errors+(errors==1?" errore":" errori")+" da correggere. Clicca una segnalazione per trovare il blocco.");
            return errors==0;
        }
        private void ClearValidation()
        {
            ((ValidationIssuesView)issuePanel).SetIssues(null,false);verifySummary.Text="Controlla il diagramma";verifySummary.ForeColor=Ui.Ink;
            verifyIntro.Text="La verifica controlla struttura, espressioni e variabili. Prova dati confronta anche i risultati.";
        }
        private void RenderIssues()
        {
            int errors=issues.Count(i=>i.Severity==Severity.Error),warnings=issues.Count(i=>i.Severity==Severity.Warning);
            verifySummary.Text=errors==0?"Pronto per la prova":errors+" "+(errors==1?"errore da correggere":"errori da correggere");verifySummary.ForeColor=errors==0?Ui.Green:Ui.Red;
            verifyIntro.Text=errors==0?"Struttura e sintassi: nessun errore. "+(warnings>0?warnings+" avvisi da valutare. ":"")+"Esegui l'algoritmo con i tuoi dati.":"Clicca una segnalazione per raggiungere il blocco. Correggi il problema e premi Ricontrolla.";
            var displayed=issues.Select(issue=>{var function=FunctionEditing.Find(Project,issue.FunctionId);return function==null?issue:new ValidationIssue{Severity=issue.Severity,NodeId=issue.NodeId,FunctionId=issue.FunctionId,Code=issue.Code,Message="Funzione «"+function.Name+"»: "+issue.Message,Suggestion=issue.Suggestion};}).ToList();
            ((ValidationIssuesView)issuePanel).SetIssues(displayed,true);
        }

        private bool EnsureRunner()
        {
            if(Canvas.IsPointerGestureActive){SetStatus("Rilascia il blocco o il collegamento prima di eseguire il programma.");return false;}
            CommitInspector();if(Runner!=null&&Runner.State!=RuntimeState.Completed&&Runner.State!=RuntimeState.Error)return true;
            if(!Verify(false)){tabs.SelectedTab=verifyTab;return false;}
            Runner=new Interpreter(Project);GetDebugger();automatic=false;RefreshRuntime();return Runner.State!=RuntimeState.Error;
        }
        internal void StartRun()
        {
            SetCanvasFocus(false);
            if(Runner!=null&&Runner.State==RuntimeState.WaitingForInput&&GetDebugger().Mode==FlowDebugger.Command.ToBlock){tabs.SelectedTab=runTab;RefreshRuntime();FocusRuntimeInputResponse();return;}
            if(Runner!=null&&Runner.State==RuntimeState.WaitingForFile){tabs.SelectedTab=runTab;RefreshRuntime();runtimeFileAction.Focus();return;}
            if(Runner!=null&&(Runner.State==RuntimeState.Completed||Runner.State==RuntimeState.Error))ResetRun(false);
            if(!EnsureRunner())return;tabs.SelectedTab=runTab;automatic=true;GetDebugger().Begin(FlowDebugger.Command.Continue);runTimer.Interval=new[]{1000,650,180,15}[Math.Max(0,speed.SelectedIndex)];
            if(Runner.State==RuntimeState.WaitingForInput){RefreshRuntime();FocusRuntimeInputResponse();return;}if(Runner.State==RuntimeState.WaitingForFile){RefreshRuntime();runtimeFileAction.Focus();return;}
            runTimer.Start();RefreshRuntime();
        }
        internal void SingleStep(){DebugStep(FlowDebugger.Command.Into);}
        private void RunTick()
        {
            if(ticking||Runner==null||Canvas.IsPointerGestureActive)return;
            var control=GetDebugger();bool fastDebug=control.Mode==FlowDebugger.Command.Over||control.Mode==FlowDebugger.Command.Out||control.Mode==FlowDebugger.Command.ToBlock;
            if(!fastDebug&&speed.SelectedIndex!=3&&Canvas.HasFlowAnimation)return;
            ticking=true;
            try
            {
                int batch=speed.SelectedIndex==3||fastDebug?240:1;
                var budget=Stopwatch.StartNew();
                for(int i=0;i<batch;i++)
                {
                    if(Runner.State==RuntimeState.Completed||Runner.State==RuntimeState.Error||Runner.State==RuntimeState.WaitingForInput||Runner.State==RuntimeState.WaitingForFile)break;
                    control.Tick();if(!control.Active)break;
                    // Yield back to input/paint messages during instant runs.
                    // The step limit belongs to Interpreter and stays unchanged.
                    if(batch>1&&budget.ElapsedMilliseconds>=6)break;
                }
                if(!control.Active||Runner.State==RuntimeState.Completed||Runner.State==RuntimeState.Error||Runner.State==RuntimeState.WaitingForInput||Runner.State==RuntimeState.WaitingForFile)runTimer.Stop();
                RefreshRuntime();if(control.AtTarget)Canvas.CenterOnNodeAnimated(Runner.CurrentNodeId);
            }finally{ticking=false;}
        }
        private void PauseRun(){runTimer.Stop();automatic=false;if(GetDebugger()!=null)debugger.Pause();RefreshRuntime();}
        private void ResetRun(bool focus)
        {
            bool retained=Runner!=null&&Runner.Files.HasWrite;runTimer.Stop();automatic=false;Runner=null;Canvas.CurrentNodeId=null;Canvas.CurrentEdgeId=null;RefreshRuntime();if(focus){tabs.SelectedTab=runTab;SetStatus(retained?"Simulazione azzerata. La bozza è in File generati e recupero.":"Simulazione azzerata. Puoi ricominciare con nuovi valori.");}
        }
        internal void SubmitInput()
        {
            if(Runner==null||Runner.State!=RuntimeState.WaitingForInput)return;
            bool accepted=Runner.ProvideInput(inputValue.Text);
            if(accepted){inputValue.Clear();GetDebugger().InputProvided();if(automatic&&debugger.Active&&Runner.State!=RuntimeState.Completed&&Runner.State!=RuntimeState.Error)runTimer.Start();}
            RefreshRuntime();
        }
        private void RefreshRuntime()
        {
            RefreshConsole();
            if(runtimePanel==null)return;
            FollowRuntimeFunction();
            AnimateRuntimeDeck();
            if(!Object.ReferenceEquals(displayedRunner,Runner))
            {
                displayedRunner=Runner;displayedOutputCount=0;runtimeRows.Clear();displayedInputNode=null;displayedInputStep=-1;
                observedFlowStep=-1;
                variables.Items.Clear();output.Clear();
            }
            if(Runner==null)
            {runtimeTitle.Text="Pronto a partire";runtimeTitle.ForeColor=Ui.Ink;runtimeInfo.Text="Premi Esegui per seguire il flusso oppure Avanti per avanzare di un blocco.";stepLabel.Text="0 passi";inputPanel.Visible=false;}
            else
            {
                var state=Runner.State;
                runtimeTitle.Text=state==RuntimeState.Completed?"Esecuzione finita":state==RuntimeState.Error?"C'è un problema":state==RuntimeState.WaitingForFile?"Scegli il file":state==RuntimeState.WaitingForInput?"Serve un valore":runTimer.Enabled?"In esecuzione":GetDebugger().AtTarget?"Blocco raggiunto":"In pausa";
                runtimeTitle.ForeColor=state==RuntimeState.Completed?Ui.Green:state==RuntimeState.Error?Ui.Red:Ui.Blue;
                runtimeInfo.Text=state==RuntimeState.Error?Runner.Error:state==RuntimeState.WaitingForInput||state==RuntimeState.WaitingForFile?Runner.LastMessage:GetDebugger().Message??Runner.LastMessage;stepLabel.Text=Runner.Steps+(Runner.Steps==1?" passo eseguito":" passi eseguiti")+"  ·  limite 10.000";
                bool waiting=state==RuntimeState.WaitingForInput;inputPanel.Visible=false;
                if(waiting){
                    // Synchronize the question even when its tab is hidden. A refresh of the
                    // same waiting step must preserve the user's draft and correction text.
                    inputQuestion.Text=Runner.InputPrompt;inputValue.AccessibleName="Valore per "+Runner.InputVariable;
                    bool nextInput=displayedInputNode!=Runner.CurrentNodeId||displayedInputStep!=Runner.Steps;
                    if(nextInput){inputValue.Clear();if(sourceViewer!=null&&sourceViewer.Visible&&sourceViewer.ContainsFocus){sourceViewer.RefreshSourceRuntime();sourceViewer.FocusSourceInput();}else if(variableInspector!=null&&variableInspector.Visible&&variableInspector.ContainsFocus){variableInspector.RefreshState();variableInspector.FocusPendingInput();}else if(runtimeConsole!=null&&runtimeConsole.Visible&&runtimeConsole.ContainsFocus)runtimeConsole.FocusPendingInput();}
                    displayedInputNode=Runner.CurrentNodeId;displayedInputStep=Runner.Steps;
                }else{displayedInputNode=null;displayedInputStep=-1;}
                UpdateRuntimeVariables();AppendRuntimeOutput();
                string currentNode=!runTimer.Enabled||state==RuntimeState.WaitingForInput||Runner.LastFunctionId!=Runner.CurrentFunctionId?Runner.CurrentNodeId:Runner.LastNodeId??Runner.CurrentNodeId;
                string activeEdge=(GetDebugger().AtBreakpoint||GetDebugger().AtTarget)?null:Runner.LastFunctionId==Runner.CurrentFunctionId?Runner.LastEdgeId:null;
                // These setters already invalidate the previous and current highlights.
                // A whole-canvas Invalidate here would discard the retained frame at every step.
                if(observedFlowStep!=Runner.Steps||Canvas.CurrentNodeId!=currentNode||Canvas.CurrentEdgeId!=activeEdge)
                {Canvas.CurrentNodeId=currentNode;Canvas.AnimateFlowStep(activeEdge,automatic?Math.Max(65,runTimer.Interval-35):470);observedFlowStep=Runner.Steps;}
            }
            LayoutRuntime();pauseButton.Enabled=runTimer.Enabled;resetButton.Enabled=Runner!=null;
            runButton.Text=Runner!=null&&Runner.State==RuntimeState.Running&&!runTimer.Enabled?"Riprendi":"Esegui";
            runButton.Enabled=!runTimer.Enabled&&(Runner==null||Runner.State!=RuntimeState.WaitingForFile);stepButton.Enabled=runButton.Enabled;
            if(Runner!=null)UpdateRuntimeStatus();
            RefreshVariableInspector();RefreshSourceRuntime();if(livePseudocode!=null)livePseudocode.Highlight(Runner==null?ActiveFunctionId:Runner.CurrentFunctionId,Runner==null?Canvas.SelectedNodeId:Canvas.CurrentNodeId,Runner!=null);
            SyncRuntimeInputChat();
        }
        private void UpdateRuntimeStatus()
        {
            if((Runner.State==RuntimeState.Ready||Runner.State==RuntimeState.Running)&&GetDebugger().Message!=null){SetStatus(debugger.Message);return;}
            switch(Runner.State)
            {
                case RuntimeState.Completed: SetStatus("Esecuzione finita. "+Runner.Steps+(Runner.Steps==1?" blocco eseguito":" blocchi eseguiti")+". Premi Riprova per ricominciare.");break;
                case RuntimeState.Error: SetStatus("Esecuzione interrotta: "+Runner.Error+" Premi Riprova dopo la correzione.");break;
                case RuntimeState.WaitingForFile: SetStatus(Runner.LastMessage);break;
                case RuntimeState.WaitingForInput: SetStatus("In attesa del dato per «"+Runner.InputVariable+"». Inseriscilo e premi Invia.");break;
                case RuntimeState.Ready: SetStatus("Pronto per la prova. Premi Esegui oppure Avanti.");break;
                default: SetStatus(runTimer.Enabled?"Simulazione in corso. Pausa ferma l'avanzamento; Ricomincia riparte da zero.":"Simulazione in pausa. Esegui riprende; Avanti avanza di un blocco.");break;
            }
        }

        private void UpdateRuntimeVariables()
        {
            // Keep native rows and their selection/scroll position alive. Most
            // execution steps touch only one variable or none at all.
            bool updating=false;
            try
            {
                var variableTypes=Runner.VariableTypes;
                foreach(var item in Runner.Variables)
                {
                    ListViewItem row;var vector=item.Value as VectorValue;string value=vector==null?ValueFormatter.Format(item.Value):"["+vector.Count+" elementi]";
                    if(runtimeRows.TryGetValue(item.Key,out row))
                    {
                        if(row.SubItems[1].Text==value&&row.SubItems[2].Text==variableTypes[item.Key]&&row.ToolTipText==item.Key+" = "+ValueFormatter.Format(item.Value))continue;
                        if(!updating){variables.BeginUpdate();updating=true;}
                        row.SubItems[1].Text=value;row.SubItems[2].Text=variableTypes[item.Key];row.ToolTipText=item.Key+" = "+ValueFormatter.Format(item.Value);
                    }
                    else
                    {
                        if(!updating){variables.BeginUpdate();updating=true;}
                        row=new ListViewItem(item.Key);row.SubItems.Add(value);row.SubItems.Add(variableTypes[item.Key]);row.ToolTipText=item.Key+" = "+ValueFormatter.Format(item.Value);
                        int index=0;
                        while(index<variables.Items.Count&&StringComparer.CurrentCulture.Compare(variables.Items[index].Text,item.Key)<0)index++;
                        variables.Items.Insert(index,row);runtimeRows.Add(item.Key,row);
                    }
                }
                if(runtimeRows.Count!=Runner.Variables.Count)
                {
                    foreach(string name in runtimeRows.Keys.Where(key=>!Runner.Variables.ContainsKey(key)).ToArray())
                    {
                        if(!updating){variables.BeginUpdate();updating=true;}
                        variables.Items.Remove(runtimeRows[name]);runtimeRows.Remove(name);
                    }
                }
            }
            finally{if(updating)variables.EndUpdate();}
        }

        private void AppendRuntimeOutput()
        {
            int count=Runner.Output.Count;
            if(count<displayedOutputCount){output.Clear();displayedOutputCount=0;}
            if(count==displayedOutputCount)return;
            int selectedStart=output.SelectionStart,selectedLength=output.SelectionLength;
            bool follow=selectedLength==0&&selectedStart==output.TextLength;
            var added=new StringBuilder();
            for(int i=displayedOutputCount;i<count;i++)
            {if(i>0&&Runner.OutputNewlines[i-1])added.Append(Environment.NewLine);added.Append(Runner.Output[i]);}
            output.AppendText(added.ToString());displayedOutputCount=count;
            if(follow){output.SelectionStart=output.TextLength;output.ScrollToCaret();}
            else output.Select(Math.Min(selectedStart,output.TextLength),Math.Min(selectedLength,Math.Max(0,output.TextLength-selectedStart)));
        }
        private void LayoutRuntime()
        {
            LayoutRuntimeDeck();
        }
        private int S(int value){return (int)Math.Round(value*dpiScale);}
        private void SetBox(Control c,int x,int y,int w,int h){c.SetBounds(S(x),S(y),S(w),S(h));}
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);using(var graphics=CreateGraphics())dpiScale=graphics.DpiX/96f;
            LayoutRuntime();LoadInspector();ArrangeContext();LayoutTabs();
        }

        private void ShowTests(){CommitInspector();if(!Verify(false)){tabs.SelectedTab=verifyTab;return;}PauseRun();using(var dialog=new TestDialog(Project))dialog.ShowDialog(this);}
        private void NewDocument(){if(!ConfirmDiscard())return;SetDocument(DiagramIO.CreateBlank(),null,false);Canvas.FitToView();}
        private void OpenDocument()
        {
            if(!ConfirmDiscard())return;using(var dlg=new OpenFileDialog{Filter="Diagrammi compatibili (*.frociart; *.flow; *.fprg)|*.frociart;*.flow;*.fprg|Flowgorithm (*.fprg)|*.fprg|Progetti Frociart (*.frociart)|*.frociart|Diagrammi precedenti (*.flow)|*.flow|Tutti i file (*.*)|*.*",Title="Apri un diagramma",CheckFileExists=true})
            if(dlg.ShowDialog(this)==DialogResult.OK)LoadPath(dlg.FileName);
        }
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control,object> documentDropControls=new System.Runtime.CompilerServices.ConditionalWeakTable<Control,object>();
        private bool openingDroppedDocument;
        private void InstallDocumentDrop(Control control)
        {
            object found;if(documentDropControls.TryGetValue(control,out found))return;
            documentDropControls.Add(control,new object());control.AllowDrop=true;
            control.DragEnter+=DocumentDragOver;control.DragOver+=DocumentDragOver;control.DragDrop+=DocumentDropped;control.DragLeave+=DocumentDragLeave;
            control.MouseDown+=delegate{FinishDocumentPresentation();};control.MouseWheel+=delegate{FinishDocumentPresentation();};control.KeyDown+=delegate{FinishDocumentPresentation();};
            control.ControlAdded+=delegate(object sender,ControlEventArgs e){InstallDocumentDrop(e.Control);};
            foreach(Control child in control.Controls)InstallDocumentDrop(child);
        }
        private static bool IsDocumentDrop(IDataObject data){return data!=null&&data.GetDataPresent(DataFormats.FileDrop,false);}
        private static string DroppedDocumentPath(IDataObject data)
        {
            var paths=data.GetData(DataFormats.FileDrop,false) as string[];
            if(paths==null||paths.Length!=1)throw new InvalidDataException("Trascina un solo diagramma alla volta (.frociart, .flow o .fprg).");
            return DocumentLaunch.Parse(new[]{"--open",paths[0]}).Path;
        }
        private void DocumentDragOver(object sender,DragEventArgs e)
        {
            if(!IsDocumentDrop(e.Data)){HideDocumentDropCue();return;}e.Effect=DragDropEffects.None;
            if(openingDroppedDocument||Canvas.IsPointerGestureActive||Canvas.IsConnecting){HideDocumentDropCue();return;}
            try{string path=DroppedDocumentPath(e.Data);if((e.AllowedEffect&DragDropEffects.Copy)!=0){if(Canvas.DocumentEntranceActive)Canvas.FinishDocumentEntrance();e.Effect=DragDropEffects.Copy;ShowDocumentDropCue(path);}else HideDocumentDropCue();}
            catch(InvalidDataException){HideDocumentDropCue();}catch(ArgumentException){HideDocumentDropCue();}catch(NotSupportedException){HideDocumentDropCue();}catch(System.Security.SecurityException){HideDocumentDropCue();}
        }
        private void DocumentDropped(object sender,DragEventArgs e)
        {
            FinishDocumentPresentation();
            if(!IsDocumentDrop(e.Data)||openingDroppedDocument)return;e.Effect=DragDropEffects.None;
            if(Canvas.IsPointerGestureActive||Canvas.IsConnecting){SetStatus("Termina il gesto sul foglio, poi trascina il file per aprirlo.");return;}
            openingDroppedDocument=true;
            try{string path=DroppedDocumentPath(e.Data);if(!ConfirmDiscard())return;if(LoadPath(path)){e.Effect=DragDropEffects.Copy;QueueDroppedDocumentEntrance();}}
            catch(Exception error){ShowError("Impossibile aprire il diagramma trascinato",error);}
            finally{openingDroppedDocument=false;}
        }
        internal bool LoadPath(string path)
        {try{var d=DiagramIO.Load(path);bool imported=String.Equals(Path.GetExtension(path),".fprg",StringComparison.OrdinalIgnoreCase);SetDocument(d,imported?null:path,imported);bool adapted=AdaptOpeningText(true);RecentProjects.Remember(path);if(imported){MarkDirty();Canvas.FitToView();SetStatus("Importato da Flowgorithm. Salva la copia come .frociart; il file originale resta intatto."+(adapted?" Spaziatura adattata al testo. Ctrl+Z ripristina le posizioni.":""));}return true;}catch(Exception ex){ShowError("Impossibile aprire il diagramma",ex);return false;}}
        private bool SaveDocument(bool saveAs)
        {
            CommitInspector();string path=currentPath;
            if(path==null||saveAs||!String.Equals(Path.GetExtension(path),".frociart",StringComparison.OrdinalIgnoreCase))
            {
                using(var dlg=new SaveFileDialog{Filter="Progetto Frociart (*.frociart)|*.frociart",Title="Salva il diagramma",DefaultExt="frociart",AddExtension=true,FileName=SafeName(Project.Title)+".frociart",OverwritePrompt=true})
                {if(dlg.ShowDialog(this)!=DialogResult.OK)return false;path=dlg.FileName;}
            }
            try{DiagramIO.Save(path,Project);RecentProjects.Remember(path);currentPath=path;dirty=false;if(!ignoreRecovery)DeleteRecovery();UpdateDocumentLabels();SetStatus("Salvato: "+Path.GetFileName(path));return true;}catch(Exception ex){ShowError("Salvataggio non riuscito",ex);return false;}
        }
        private bool ConfirmDiscard()
        {
            CommitInspector();if(!dirty)return true;PauseRun();var result=FlowMessageDialog.Show(this,"Vuoi salvare le modifiche a «"+Project.Title+"»?","Modifiche da salvare",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            return result==DialogResult.No||(result==DialogResult.Yes&&SaveDocument(false));
        }
        private void ChooseExample()
        {
            using(var dialog=new ExamplesDialog())
            {if(dialog.ShowDialog(this)!=DialogResult.OK)return;if(!ConfirmDiscard())return;SetDocument(dialog.Selected,null,false);AdaptOpeningText(true);Canvas.FitToView();}
        }
        private readonly Dictionary<string,ContextMenuStrip> taskMenus=new Dictionary<string,ContextMenuStrip>();
        private void ShowMore(Control anchor){ShowTaskMenu("Progetto",anchor);}
        private void ShowTaskMenu(string section,Control anchor)
        {
            if(IsDisposed||Disposing)return;
            var menu=PrepareTaskMenu(section);
            foreach(var other in taskMenus.Values)if(other!=menu&&other.Visible)other.Close();
            moreMenu=menu;Ui.StyleMenu(menu);
            menu.Show(anchor,new Point(0,anchor.Height));
        }
        private ContextMenuStrip BuildTaskMenu(string section)
        {
            var menu=new FlowContextMenu{Name="TaskMenu"+section,AccessibleName=section,Font=Ui.Font(9.5f),ShowImageMargin=false,Padding=new Padding(4),BackColor=Ui.Surface,ForeColor=Ui.Ink};
            var items=menu.Items;
            switch(section){
                case "Progetto":
                    items.Add("Nuovo diagramma",null,delegate{NewDocument();});
                    items.Add("Apri progetto…",null,delegate{OpenDocument();});
                    items.Add("Salva   Ctrl+S",null,delegate{SaveDocument(false);});
                    items.Add("Salva con nome…",null,delegate{SaveDocument(true);});
                    items.Add("Rinomina diagramma",null,delegate{EnterEditor();Canvas.SelectNode(null);tabs.SelectedTab=blockTab;titleEdit.Focus();titleEdit.SelectAll();});
                    items.Add("Variabili del progetto…",null,delegate{ChooseVariablePolicy();}).Name="VariablePolicy";
                    items.Add(new ToolStripSeparator());
                    items.Add("Home e progetti recenti",null,delegate{ShowHome();});
                    items.Add("Recupera lavoro non salvato…",null,delegate{ShowRecovery();});
                    items.Add("Esempi…",null,delegate{ChooseExample();});
                    items.Add(new ToolStripSeparator());
                    items.Add("Esporta…   PDF, SVG, PNG",null,delegate{ShowExport();});
                    items.Add("Stampa…   Ctrl+P",null,delegate{ShowPrint();});
                    items.Add("Flowgorithm · salva una copia .fprg…",null,delegate{ShowFlowgorithmExport();});
                    items.Add(new ToolStripSeparator());
                    items.Add("Attiva icona e doppio clic dei progetti",null,delegate{try{InstallIntegration.InstallCurrent();SetStatus("Apertura dei file attivata. I progetti .frociart usano l'icona Frociart e si aprono con doppio clic.");FlowMessageDialog.Show(this,"Frociart è pronto per aprire i progetti con doppio clic.\n\nSe Windows aveva già un'altra app predefinita, scegli Frociart da Apri con. I progetti e i progressi sono conservati.","File Frociart",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){ShowError("Configurazione non completata",ex);}});
                    break;
                case "Modifica":
                    moreUndo=(ToolStripMenuItem)items.Add("Annulla modifica   Ctrl+Z",null,delegate{Undo();});
                    moreRedo=(ToolStripMenuItem)items.Add("Ripeti modifica   Ctrl+Y",null,delegate{Redo();});
                    items.Add(new ToolStripSeparator());
                    items.Add("Taglia blocchi   Ctrl+X",null,delegate{CommitInspector();Canvas.CopySelection(true);}).Name="SelectionCut";
                    items.Add("Copia blocchi   Ctrl+C",null,delegate{CommitInspector();Canvas.CopySelection();}).Name="SelectionCopy";
                    items.Add("Incolla blocchi   Ctrl+V",null,delegate{CommitInspector();Canvas.PasteSelection();}).Name="SelectionPaste";
                    items.Add("Duplica blocchi   Ctrl+D",null,delegate{CommitInspector();Canvas.DuplicateSelected();}).Name="SelectionDuplicate";
                    items.Add("Elimina blocchi   Canc",null,delegate{CommitInspector();Canvas.DeleteSelection();}).Name="SelectionDelete";
                    items.Add("Seleziona tutti i blocchi   Ctrl+A",null,delegate{Canvas.SelectAllNodes();}).Name="SelectionAll";
                    items.Add(new ToolStripSeparator());
                    items.Add("Cerca nel progetto…   Ctrl+F",null,delegate{ShowProjectSearch();});
                    items.Add("Rinomina variabile…",null,delegate{RenameDocumentVariable();});
                    items.Add("Funzioni del progetto…",null,delegate{ManageFunctions();});
                    var cycles=new ToolStripMenuItem("Cicli"){Name="CycleTools"}; items.Add(cycles); cycles.DropDownItems.Add("Inserisci un ciclo guidato…",null,delegate{ShowLoopEditor();}); cycles.DropDownItems.Add("Cicli: riepilogo e modifica…",null,delegate{ShowCycleManager();}); cycles.DropDownItems.Add("Panoramica cicli · espandi e riduci…",null,delegate{ShowCycleOverview();}); cycles.DropDownItems.Add("Riduci tutti i cicli sul foglio",null,delegate{CommitInspector();Canvas.FoldAllCycles(true);}); cycles.DropDownItems.Add("Espandi tutti i cicli sul foglio",null,delegate{CommitInspector();Canvas.FoldAllCycles(false);});
                    items.Add(new ToolStripSeparator());
                    items.Add("Riordina i blocchi",null,delegate{ArrangeDiagram();});
                    break;
                case "Esegui":
                    items.Add("Verifica diagramma   F6",null,delegate{Verify(true);});
                    items.Add("Esegui / riprendi   F5",null,delegate{StartRun();});
                    items.Add("Esegui fino al blocco selezionato   Ctrl+F10",null,delegate{RunToSelected();}).Name="RunToSelection";
                    items.Add("Pausa",null,delegate{PauseRun();}).Name="PauseExecution";
                    items.Add("Ricomincia la prova",null,delegate{ResetRun(true);}).Name="ResetExecution";
                    items.Add(new ToolStripSeparator());
                    items.Add("Avanti · entra nella funzione   F11",null,delegate{DebugStep(FlowDebugger.Command.Into);});
                    items.Add("Avanti · senza entrare   F10",null,delegate{DebugStep(FlowDebugger.Command.Over);});
                    items.Add("Esci dalla funzione   Maiusc+F11",null,delegate{DebugStep(FlowDebugger.Command.Out);}).Name="StepOutExecution";
                    items.Add(new ToolStripSeparator());
                    items.Add("Variabili e chiamate · F7",null,delegate{ShowVariableInspector();});
                    items.Add("Console · letture e stampe…",null,delegate{ShowRuntimeConsole();});
                    items.Add("File generati e recupero…",null,delegate{ShowGeneratedFiles();});
                    items.Add(new ToolStripSeparator());
                    items.Add("Prova con dati di test…",null,delegate{ShowTests();});
                    break;
                case "Codice":
                    items.Add("Importa / modifica pseudocodice…",null,delegate{ShowPseudocode();});
                    var languages=new ToolStripMenuItem("Genera codice"){Name="SourceLanguages"};items.Add(languages);foreach(var available in FlowSourceTarget.All){var target=available;languages.DropDownItems.Add("Codice "+target.Name+" · visualizza ed esporta…",null,delegate{ShowSourceTarget(target);});}
                    items.Add("Modelli di codice personali…",null,delegate{ShowCodeTemplates();});
                    break;
                case "Vista":
                    var navigationItem=items.Add("Torna al punto precedente   Alt+←",null,delegate{NavigateBack();});navigationItem.Name="NavigationBack";navigationItem.Enabled=CanNavigateBack;
                    var layouts=new ToolStripMenuItem("Layout di lavoro"){Name="LayoutChoices"};items.Add(layouts);for(int i=0;i<WorkspaceLayout.Names.Length;i++){int mode=i;layouts.DropDownItems.Add(WorkspaceLayout.Names[i],null,delegate{SetWorkspaceLayout(mode,true);});}
                    items.Add("Solo foglio / pannelli   F8",null,delegate{EnterEditor();SetCanvasFocus(!canvasFocus);});
                    items.Add("Codice a vista",null,delegate{workspaceCodeToggle.PerformClick();}).Name="CodeVisibility";
                    items.Add("Separa Input e Output",null,delegate{SetIoPresentation(!IoInstruction.Separate);}).Name="SeparateIo";
                    items.Add(new ToolStripSeparator());
                    items.Add("Inquadra il diagramma",null,delegate{Canvas.FitToView();});
                    items.Add("Griglia di allineamento",null,delegate{SetGrid(!Canvas.SnapToGrid,true);}).Name="GridVisibility";
                    items.Add(new ToolStripSeparator());
                    items.Add("Stile dei diagrammi…",null,delegate{ChooseDiagramAppearance();});
                    items.Add("Tema chiaro / scuro",null,delegate{Ui.SetDarkMode(!Ui.IsDark);}).Name="ThemePreference";
                    var motion=new ToolStripMenuItem("Animazioni dell’interfaccia"){Name="MotionPreference",Checked=UiMotion.PreferredEnabled,CheckOnClick=true};motion.Click+=delegate{if(!UiMotion.SetEnabled(motion.Checked))SetStatus("Non riesco a salvare la preferenza delle animazioni.");};items.Add(motion);
                    break;
                case "Aiuto":
                    items.Add("Cerca un comando…   Ctrl+K",null,delegate{ShowCommandPalette();}).Name="FindCommand";
                    items.Add("Guida a Frociart…",null,delegate{ShowGuide();});
                    items.Add("Leggi il mio flowchart",null,delegate{CommitInspector();TutorTextDialog.ShowText(this,"Il tuo flowchart, spiegato",TutorDiagnostics.Describe(Document));});
                    items.Add("Spiega la casella selezionata",null,delegate{ExplainSelectedBlock();});
                    items.Add("Comandi rapidi",null,delegate{ShowShortcuts();});
                    items.Add(new ToolStripSeparator());
                    items.Add("Autore e condizioni d’uso",null,delegate{TutorTextDialog.ShowText(this,"Frociart · mamt104",RecentProjects.License);});
                    items.Add("Informazioni su Frociart",null,delegate{FlowMessageDialog.Show(this,"Frociart v1\nUn progetto di mamt104.\nCopyright © 2026 mamt104. Tutti i diritti riservati.\n\nEditor e simulatore di diagrammi di flusso.\n\nTasto sinistro: seleziona. Rotella premuta: sposta la vista. Tasto destro: menu.\nClicca + per aggiungere un passaggio, oppure trascinalo sul foglio.\nTema chiaro e scuro con preferenza ricordata.\n\nSalva e riapri progetti .frociart, importa anche .flow.\nEsporta PDF, SVG e PNG. Il lavoro resta sul computer.\n\nLa verifica aiuta a trovare errori di struttura e sintassi. I risultati vanno controllati con dati di prova.","Frociart v1",MessageBoxButtons.OK,MessageBoxIcon.Information);});
                    break;
                default:menu.Dispose();throw new ArgumentException("Sezione non valida","section");
            }
            OrganizeTaskMenu(menu,section);return menu;
        }
        private void ChooseVariablePolicy(){
            if(Canvas.IsPointerGestureActive||Canvas.IsConnecting){SetStatus("Termina il gesto prima di cambiare le variabili del progetto.");return;}
            PauseRun();const string automatic="Dichiarazione automatica",declared="Dichiarazioni obbligatorie";
            string selected=TutorChoiceDialog.Choose(this,"Variabili del progetto",new[]{automatic,declared},v=>v,v=>v==automatic?"Una variabile nasce quando la leggi o le assegni un valore. Puoi usare anche Dichiara per scegliere un tipo.":"Definisci le variabili con Dichiara prima dell’uso. Il controllo trova subito i nomi scritti per errore. La scelta vale anche nelle funzioni e si salva nel progetto. Aggiorna Frociart anche sull’altro dispositivo per riaprire questi progetti.",Project.RequiresDeclarations?declared:automatic);
            if(selected!=null)SetVariablePolicy(selected==declared);
        }
        internal void SetVariablePolicy(bool required){
            if(Canvas.IsPointerGestureActive||Canvas.IsConnecting||Project.RequiresDeclarations==required)return;
            CommitInspector();PauseRun();PrepareMutation();Project.VariablePolicy=required?"declared":null;FlowAnnotations.Promote(Project);MarkDirty();if(livePseudocode!=null)livePseudocode.Schedule();
            SetStatus(required?"Dichiarazioni obbligatorie attive. Premi Verifica per trovare i nomi da dichiarare o correggere.":"Dichiarazione automatica attiva. Puoi continuare a usare Dichiara per scegliere i tipi.");
        }
        private void ChooseDiagramAppearance()
        {
            if(Canvas.IsPointerGestureActive)return;
            string chosen=TutorChoiceDialog.Choose(this,"Stile dei diagrammi · preferenza del dispositivo",FlowAppearance.Names,n=>n,n=>FlowAppearance.Descriptions[Array.IndexOf(FlowAppearance.Names,n)],FlowAppearance.Names[Canvas.AppearanceStyle.Mode]);
            if(chosen==FlowAppearance.Names[3]){using(var dialog=new DiagramDesignDialog(Document,Canvas.AppearanceStyle.CustomDesign)){dialog.ApplyDesign=design=>ApplyDiagramDesign(3,design);dialog.ShowDialog(this);}}
            else if(chosen!=null)ApplyDiagramAppearance(Array.IndexOf(FlowAppearance.Names,chosen));
        }
        internal void ApplyDiagramAppearance(int mode)
        {
            try{ApplyDiagramDesign(mode,FlowAppearance.CurrentDesign);}catch(FlowException ex){SetStatus("Aspetto non applicato. "+ex.Message);}
        }
        private bool AdaptOpeningText(bool recordHistory){
            try{
                var plan=FlowTextSpacing.Plan(Project,Canvas.AppearanceStyle.Design);if(plan.Count==0)return false;
                var nodes=FunctionEditing.Graphs(Project).SelectMany(g=>g.Nodes).ToDictionary(n=>n.Id);var before=nodes.ToDictionary(p=>p.Key,p=>new PointF(p.Value.X,p.Value.Y));var expected=Project;
                if(recordHistory){undo.Push(new ProjectHistory(Project,ActiveFunctionId){Appearance=Canvas.AppearanceStyle});redo.Clear();}
                Action apply=delegate{foreach(var n in FunctionEditing.Graphs(Project).SelectMany(g=>g.Nodes)){PointF p;if(plan.TryGetValue(n.Id,out p)){n.X=p.X;n.Y=p.Y;}}};
                apply();
                if(recordHistory){Canvas.RefreshOpeningGeometry();if(IsHandleCreated)BeginInvoke((Action)delegate{if(!IsDisposed&&ReferenceEquals(expected,Project)&&plan.All(p=>nodes[p.Key].X==p.Value.X&&nodes[p.Key].Y==p.Value.Y))Canvas.AnimateOpeningPositions(before);});}
                else Canvas.Diagram=Document;
                if(recordHistory){MarkDirty();SetStatus("Spaziatura adattata al testo. Ctrl+Z ripristina le posizioni originali.");}
                return true;
            }catch(FlowException ex){SetStatus("Spaziatura originale mantenuta. "+ex.Message);return false;}
        }
        internal void ApplyDiagramDesign(int mode,FlowDesign design){
            if(Canvas.IsPointerGestureActive||Canvas.IsConnecting)return;
            var style=new FlowAppearance(mode,design);var old=Canvas.AppearanceStyle;if(old.Mode==style.Mode&&old.CustomDesign.Same(style.CustomDesign))return;
            bool geometry=old.Design.FontKey!=style.Design.FontKey;var plan=DiagramIO.Clone(Project);
            if(geometry){var originals=FunctionEditing.Graphs(Project).ToArray();var planned=FunctionEditing.Graphs(plan).ToArray();var source=originals.SelectMany(g=>g.Nodes).ToDictionary(n=>n.Id);for(int i=0;i<planned.Length;i++){var graph=planned[i];foreach(var node in graph.Nodes){FlowNode original;if(source.TryGetValue(node.Id,out original)){node.PreviewText=original.PreviewText;node.PreviewComment=original.PreviewComment;}}PointF? focus=ReferenceEquals(originals[i],Document)&&Canvas.Visible?(PointF?)Canvas.ClientToWorld(new Point(Canvas.Width/2,Canvas.Height/2)):null;FlowLayout.ArrangeAnchored(graph,originals[i],old.Design,style.Design,focus);}}
            var history=new ProjectHistory(Project,ActiveFunctionId){Appearance=old};undo.Push(history);while(undo.Count>80){var keep=undo.Take(80).Reverse().ToArray();undo.Clear();foreach(var item in keep)undo.Push(item);}redo.Clear();
            bool saved=ApplyDesignPositions(plan,style);SetStatus("Stile "+FlowAppearance.Names[style.Mode]+(geometry?" · spaziatura adattata. Ctrl+Z annulla.":" · istruzioni e collegamenti invariati.")+(saved?"":" Preferenza non salvata: resta attiva per questa sessione."));
        }
        private bool ApplyDesignPositions(Diagram plan,FlowAppearance style){
            var positions=FunctionEditing.Graphs(plan).SelectMany(g=>g.Nodes).ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));bool moved=false;
            bool saved=FlowAppearance.SetSettings(style.Mode,style.CustomDesign);Canvas.ChangeDesignAnimated(style,delegate{foreach(var node in FunctionEditing.Graphs(Project).SelectMany(g=>g.Nodes)){PointF p;if(positions.TryGetValue(node.Id,out p)){moved|=node.X!=p.X||node.Y!=p.Y;node.X=p.X;node.Y=p.Y;}}});if(moved)MarkDirty();else UpdateDocumentLabels();return saved;
        }
        private void ShowPrint()
        {
            if(Canvas.IsPointerGestureActive)return;
            CommitInspector();PauseRun();
            try {using(var dialog=new DiagramPrintDialog(Canvas))dialog.ShowDialog(this);}
            catch(Exception ex){ShowError("Anteprima non disponibile",ex);}
        }
        private void ShowExport()
        {
            CommitInspector();using(var dialog=new ExportDialog(Canvas))
            if(dialog.ShowDialog(this)==DialogResult.OK)SetStatus("Esportato: "+Path.GetFileName(dialog.ExportedPath));
        }
        internal void ShowPseudocode()
        {
            CommitInspector();PauseRun();Canvas.CancelConnection();
            if(pseudocodeSource!=Project){pseudocodeSource=Project;pseudocodeDraft=null;}
            PseudocodeDialog dialog=null;
            using(dialog=new PseudocodeDialog(Project,pseudocodeDraft,delegate(Diagram generated){
                if(!ConfirmDiscard())return;
                SetDocument(generated,null,true);Canvas.FitToView();SetStatus("Diagramma creato dal pseudocodice. Verifica ed esegui per provarlo.");dialog.Close();
            },text=>pseudocodeDraft=text))dialog.ShowDialog(this);
        }
        internal void ShowPythonSource(){ShowSource(false);}
        internal void ShowJavaSource(){ShowSource(true);}
        internal void ShowCSharpSource(){ShowSourceTarget(FlowSourceTarget.CSharp);}
        internal void ShowCppSource(){ShowSourceTarget(FlowSourceTarget.Cpp);}
        internal void ShowJavaScriptSource(){ShowSourceTarget(FlowSourceTarget.JavaScript);}
        internal void ShowTypeScriptSource(){ShowSourceTarget(FlowSourceTarget.TypeScript);}
        private void ShowSource(bool javaCode){ShowSourceTarget(javaCode?FlowSourceTarget.Java:FlowSourceTarget.Python);}
        private void ShowSourceTarget(FlowSourceTarget target)
        {
            CommitInspector();PauseRun();Canvas.CancelConnection();
            using(var dialog=new SourceCodeDialog(DiagramIO.Clone(Project),(function,node)=>{NavigateToOrigin(function,node);},target)){
                AttachSourceRuntime(dialog);
                dialog.SetOpeningOrigin(Runner!=null&&tabs.SelectedTab==runTab?Runner.CurrentFunctionId:ActiveFunctionId,Runner!=null&&tabs.SelectedTab==runTab?Runner.CurrentNodeId:Canvas.SelectedNodeId);dialog.ShowDialog(this);
            }
        }
        internal void ShowFlowgorithmExport(){
            CommitInspector();PauseRun();Canvas.CancelConnection();
            if(Canvas.IsPointerGestureActive){SetStatus("Termina il trascinamento prima di esportare.");return;}
            using(var dialog=new FlowgorithmExportDialog(Project,(function,node)=>{NavigateToOrigin(function,node);}))dialog.ShowDialog(this);
        }
        private void ShowGuide(){using(var guide=new GuideDialog())guide.ShowDialog(this);}
        private void OnClosing(object sender,FormClosingEventArgs e){FinishDocumentPresentation();if(!ConfirmDiscard()){e.Cancel=true;return;}runtimeChatClosing=true;ResetRuntimeInputChat();runTimer.Stop();Runner=null;if(!ignoreRecovery)DeleteRecovery();}
        private void SaveRecovery()
        {
            recoveryTimer.Stop();if(ignoreRecovery)return;
            if(Canvas.IsPointerGestureActive){recoveryTimer.Start();return;}
            CommitInspector();recoveryTimer.Stop();
            if(!dirty)return;try{
                recoverySession.EnsureLease();
                WithRecoveryLock(delegate{
                    if(File.Exists(recoveryPath)&&!OwnsCurrentRecovery())throw new IOException("Recupero di un altro documento.");
                    byte[] snapshot=DiagramIO.EncodeDocument(Project);
                    Directory.CreateDirectory(Path.GetDirectoryName(recoveryPath));DiagramIO.Save(recoveryPath,Project);ownedRecovery=snapshot;recoverySession.WriteInfo(Project.Title);
                });
            }catch{SetStatus("Recupero automatico non disponibile. Usa Salva per conservare il diagramma.");}
        }
        private void ShowRecovery(){if(Canvas.IsPointerGestureActive)return;TryRecover(true);}
        private bool TryRecover(bool includeArchived){
            try{
                var copies=recoverySession.Find(includeArchived);if(copies.Count==0){if(includeArchived)FlowMessageDialog.Show(this,"Non ci sono copie di recupero disponibili. Il lavoro aperto in un'altra finestra resta in quella finestra.","Recupera lavoro",MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
                var chosen=TutorChoiceDialog.Choose(this,"Recupera lavoro · Annulla conserva le copie per un altro momento",copies.ToArray(),c=>c.Label,c=>c.Detail);
                if(chosen==null)return false;
                using(var claim=recoverySession.Open(chosen)){
                    Diagram recovered=claim.Document();if(!ConfirmDiscard())return false;SetDocument(recovered,null,true);SaveRecovery();
                    if(!OwnsCurrentRecovery()){SetStatus("Copia aperta. Usa Salva: il recupero automatico non è disponibile. La copia precedente è ancora conservata.");return true;}
                    try{claim.Archive();SetStatus("Lavoro recuperato. Salva per creare il tuo file; la copia precedente resta in Recupera lavoro.");}
                    catch(IOException){SetStatus("Lavoro recuperato. La copia precedente rimane disponibile: non riesco ad archiviarla.");}
                    catch(UnauthorizedAccessException){SetStatus("Lavoro recuperato. La copia precedente rimane disponibile: non riesco ad archiviarla.");}
                }
                return true;
            }catch(Exception ex){ShowError("Recupero non riuscito",ex);return false;}
        }
        private void WithRecoveryLock(Action action){
            string name=FlowRecoverySession.MutexName(recoveryPath);
            using(var gate=new System.Threading.Mutex(false,name)){
                bool held=false;
                try{
                    try{held=gate.WaitOne(0);}catch(System.Threading.AbandonedMutexException){held=true;}
                    if(!held)throw new IOException("Recupero occupato da un'altra finestra.");
                    action();
                }finally{if(held)gate.ReleaseMutex();}
            }
        }
        private byte[] ReadRecovery(){
            return FlowRecoverySession.ReadBounded(recoveryPath);
        }
        private bool OwnsCurrentRecovery(){return ownedRecovery!=null&&File.Exists(recoveryPath)&&new FileInfo(recoveryPath).Length==ownedRecovery.Length&&ReadRecovery().SequenceEqual(ownedRecovery);}
        private void DeleteRecovery(){recoveryTimer.Stop();try{if(ownedRecovery!=null)WithRecoveryLock(delegate{if(OwnsCurrentRecovery())File.Delete(recoveryPath);});}catch{}finally{ownedRecovery=null;}}
        private static string SafeName(string name){return ExportDialog.SafeFileName(name);}
        private void SetStatus(string message){status.Text=message??"Pronto";}
        private void ShowError(string title,Exception ex){FlowMessageDialog.Show(this,ex.Message,title,MessageBoxButtons.OK,MessageBoxIcon.Warning);SetStatus(title+": "+ex.Message);}

        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            FinishDocumentPresentation();
            if(HandleNavigationKey(keyData))return true;
            if(keyData==(Keys.Control|Keys.K)){ShowCommandPalette();return true;}
            if(keyData==(Keys.Control|Keys.F10)){EnterEditor();RunToSelected();return true;}
            if((keyData==Keys.F9||keyData==Keys.F11||keyData==(Keys.Shift|Keys.F11))&&Canvas.IsPointerGestureActive)return true;
            if(keyData==Keys.F7){ShowVariableInspector();return true;}
            if(keyData==Keys.F9){ToggleBreakpoint();return true;}
            if(keyData==Keys.F11){EnterEditor();DebugStep(FlowDebugger.Command.Into);return true;}
            if(keyData==(Keys.Shift|Keys.F11)){EnterEditor();DebugStep(FlowDebugger.Command.Out);return true;}
            if(keyData==Keys.F8){EnterEditor();SetCanvasFocus(!canvasFocus);return true;}
            if(keyData==Keys.Escape&&canvasFocus&&!Canvas.IsPointerGestureActive&&!Canvas.IsConnecting){SetCanvasFocus(false);return true;}
            if(keyData==Keys.Escape&&Canvas.IsConnecting){Canvas.CancelConnection();return true;}
            if(Canvas.IsPointerGestureActive&&((keyData&Keys.Control)!=0||keyData==Keys.Delete||keyData==Keys.F5||keyData==Keys.F6||keyData==Keys.F10))return true;
            if(keyData==(Keys.Control|Keys.S)){SaveDocument(false);return true;}
            if(keyData==(Keys.Control|Keys.Shift|Keys.S)){SaveDocument(true);return true;}
            if(keyData==(Keys.Control|Keys.P)){EnterEditor();ShowPrint();return true;}
            if(keyData==(Keys.Control|Keys.F)){ShowProjectSearch();return true;}
            if(keyData==(Keys.Control|Keys.O)){OpenDocument();return true;}
            if(keyData==(Keys.Control|Keys.N)){NewDocument();return true;}
            if(keyData==(Keys.Control|Keys.Tab)||keyData==(Keys.Control|Keys.Shift|Keys.Tab))
            {tabs.SelectedIndex=(tabs.SelectedIndex+((keyData&Keys.Shift)!=0?2:1))%3;return true;}
            if(keyData==Keys.F6){EnterEditor();Verify(true);return true;}if(keyData==Keys.F5){EnterEditor();StartRun();return true;}if(keyData==Keys.F10){EnterEditor();DebugStep(FlowDebugger.Command.Over);return true;}if(keyData==Keys.F1){ShowGuide();return true;}
            bool textFocused=ActiveTextBox(this)!=null;
            if(!textFocused&&editorBody.Visible)
            {
                if(Canvas.HandleClipboardShortcut(keyData))return true;
                if(keyData==(Keys.Control|Keys.Z)){Undo();return true;}if(keyData==(Keys.Control|Keys.Y)||keyData==(Keys.Control|Keys.Shift|Keys.Z)){Redo();return true;}
                if(keyData==(Keys.Control|Keys.A)){CommitInspector();Canvas.SelectAllNodes();Canvas.Focus();return true;}
                if(keyData==(Keys.Control|Keys.D)){CommitInspector();Canvas.DuplicateSelected();return true;}
                if(keyData==Keys.Delete){Canvas.DeleteSelection();return true;}
            }
            return base.ProcessCmdKey(ref msg,keyData);
        }
        private static TextBoxBase ActiveTextBox(ContainerControl parent)
        {
            Control c=parent.ActiveControl;while(c is ContainerControl)c=((ContainerControl)c).ActiveControl;return c as TextBoxBase;
        }
        protected override void Dispose(bool disposing)
        {if(disposing){DisposeDocumentPresentation();runtimeChatClosing=true;ResetRuntimeInputChat();Ui.ThemeChanged-=OnThemeChanged;runTimer.Dispose();recoveryTimer.Dispose();if(recoverySession!=null)recoverySession.Dispose();tips.Dispose();foreach(var menu in taskMenus.Values)menu.Dispose();taskMenus.Clear();}base.Dispose(disposing);}
    }
}



