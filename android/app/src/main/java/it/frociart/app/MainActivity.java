package it.frociart.app;

import android.app.*;
import android.content.*;
import android.content.res.Configuration;
import android.graphics.Color;
import android.net.Uri;
import android.os.*;
import android.text.*;
import android.view.*;
import android.widget.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.*;
import java.util.concurrent.*;

import it.frociart.app.FlowModel.Document;
import it.frociart.app.FlowModel.Node;
import it.frociart.app.FlowModel.Edge;

public final class MainActivity extends Activity implements FlowCanvasView.Listener {
    private static final int OPEN=100,SAVE=101,PSEUDO_OPEN=102,FUNCTION_OPEN=103,TEMPLATE_OPEN=104,BACKUP_CREATE=105,BACKUP_OPEN=106;
    private FlowTemplateLibrary templateLibrary,pendingTemplateImport;
    private int functionImportEpoch;private FunctionImportTicket pendingFunctionImport;
    private static final class FunctionImportTicket {
        final Document original,snapshot;final String projectId;final long revision;final int epoch;
        FunctionImportTicket(Document original,String projectId,long revision,int epoch){this.original=original;snapshot=original.copy();this.projectId=projectId;this.revision=revision;this.epoch=epoch;}
    }
    private FlowPseudocodePanel pseudocodePanel;private String pseudocodeDraft,pseudocodeProject;private int pseudocodeReadRequest;
    private FlowDataTestPanel dataTestPanel;
    private FlowSourcePanel sourcePanel;
    private FlowgorithmExportPanel flowgorithmPanel;
    private String appBuild(){try{return appVersion()+" · build "+getPackageManager().getPackageInfo(getPackageName(),0).versionCode;}catch(android.content.pm.PackageManager.NameNotFoundException ignored){return appVersion();}}
    private String appVersion(){try{return getPackageManager().getPackageInfo(getPackageName(),0).versionName;}catch(android.content.pm.PackageManager.NameNotFoundException ignored){return "locale";}}
    private final FlowOpenGuard openGuard=new FlowOpenGuard();
    private FlowOpenGuard.Ticket pendingOpen;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final ExecutorService io=Executors.newSingleThreadExecutor();
    private final ArrayDeque<FlowFunctionEditing.History> undo=new ArrayDeque<>(),redo=new ArrayDeque<>();
    private FlowStore store;private FlowDraftStore drafts;private SharedPreferences prefs;private String projectId;
    private FlowStudy study;private boolean hubVisible=true,autoArrange;private Ui.Toggle gridButton;private Ui.Button arrangeButton;private LinearLayout assistBar,statusBar,practiceBar;
    private FlowPractice.Mission mission;private Ui.Button practiceCheck;private TextView practiceTitle;private boolean checkingPractice;
    private String practiceLessonId="";
    private Document project,document;private String activeFunctionId;private Ui.Button graphButton;private FlowFunctionEditor functionEditor;private TextView variablesLabel;private FlowCanvasView canvas;private LinearLayout root,projectHeader,selectionBar,dock,runPanel,runHeader,runMore,runControls;
    private Ui.Sheet navigationSheet;private String navigationRoute;
    private Ui.Button workspaceLayoutButton;private HorizontalScrollView workspaceTools;
    private Ui.Workspace workspace;private FrameLayout canvasFrame,actionDock;
    private TextView projectTitle,saveStatus,hint,selectionTitle,inputLabel;
    private View editorToolsContainer,projectIdentityArea,graphToolbar,runtimeInputGap;private LinearLayout runtimeInputRow;private Ui.Button runInputSend,runInputError;private int inputRevealEpoch;private ViewTreeObserver inputRevealObserver;private ViewTreeObserver.OnPreDrawListener inputRevealListener;private boolean inlineRuntimeInput;private boolean keyboardVisible;private Ui.Button navigationBack;
    private final ArrayDeque<NavigationPoint> navigationHistory=new ArrayDeque<>();private boolean restoringNavigation;private int navigationViewportEpoch;private ViewTreeObserver.OnPreDrawListener navigationViewportListener;private ViewTreeObserver navigationViewportObserver;
    private final LinkedHashMap<String,FlowWatchExpressions.Session> watchSessions=new LinkedHashMap<String,FlowWatchExpressions.Session>(32,.75f,true){protected boolean removeEldestEntry(Map.Entry<String,FlowWatchExpressions.Session> e){return size()>32;}};
    private boolean backupBusy;private String backupProject;private long backupRevision;private FlowBackup.Archive pendingBackup;
    private LinearLayout saveErrorBar;
    private Ui.Button selectionEdit,selectionAdd,selectionConnect,selectionMore,selectionClose,runDetailsButton;
    private String selectedNodeId,connectionSource,connectionBranch;
    private String inputRequest;private boolean runVisible,runExpanded=true,saveFailed,resumeAfterInput,landscapeIme,compactRunLayout;private boolean compactEditorActions;private Ui.Button compactAdd;private int projectOpenRequest;private ScrollView runScroll;
    private Ui.Button undoButton,redoButton,runButton;private Ui.Sheet activeSheet;private FlowLoopEditor loopEditor;
    private FlowDebugger debugger;private boolean ignoreBreakpoints;
    private FlowFileUi fileUi;private boolean foreground;
    private Ui.Sheet turtleSheet;private LinearLayout turtleCard;private FlowTurtleView turtleView,turtleLarge;private TextView turtleReadout;private Ui.Button turtlePlay;
    private FlowConsolePanel consolePanel;
    private FlowVariablePanel variablePanel;
    private FlowVariableRenamePanel variableRename;private FlowProjectSearch projectSearch;private FlowDesignEditor designEditor;
    private boolean openingSpacingAdjusted;
    private FlowEngine.Runner runner;private TextView runStatus,runDetails,outputText,variablesText;private EditText runInput;private LinearLayout inputArea;private Ui.Button playPause,stepButton;
    private boolean running=false,dirty=false,saving=false,destroyed=false;private int speed=1,outputShown=0;private String pendingExportPath,visibleEditorId;private Uri lastImportedUri;
    private final Runnable saveTask=()->saveNow();private final Runnable runTick=new Runnable(){@Override public void run(){advanceRun();}};
    private long documentRevision;private int validationRequest;
    private final String[] COLORS={"#39B985","#EF6C82","#5B8DEF","#D68A18","#9771DC","#22A5B8"};

    @Override public void onCreate(Bundle saved){super.onCreate(saved);prefs=getSharedPreferences("frociart",MODE_PRIVATE);FlowDesign storedDesign=FlowDesign.DEFAULT;try{String raw=prefs.getString("diagram_design",null);if(raw!=null)storedDesign=FlowDesign.decode(raw);}catch(IllegalArgumentException ignored){}FlowAppearance.setSettings(prefs.getInt("diagram_style",0),storedDesign);FlowCanvasView.installSizing();String mode=prefs.getString("theme","system");Ui.dark=mode.equals("dark")||(mode.equals("system")&&(getResources().getConfiguration().uiMode&Configuration.UI_MODE_NIGHT_MASK)==Configuration.UI_MODE_NIGHT_YES);Ui.motion=prefs.getBoolean("animations",true);FlowModel.separateIo=prefs.getBoolean("separate_io",false);store=new FlowStore(this);
        fileUi=new FlowFileUi(this,io,handler,new FlowFileUi.Host(){
            public FlowEngine.Runner runner(){return runner;}
            public boolean alive(){return !destroyed&&!isFinishing();}
            public boolean foreground(){return foreground;}
            public Ui.Sheet sheet(String title,String text){return MainActivity.this.sheet(title,text);}
            public void message(String title,String text){MainActivity.this.message(title,text);}
            public void changed(boolean completed){
                if(completed&&runner!=null){getDebugger().inputProvided();running=runVisible&&foreground&&resumeAfterInput&&debugger.active()&&runner.state!=FlowEngine.State.COMPLETED&&runner.state!=FlowEngine.State.ERROR;handler.removeCallbacks(runTick);}
                refreshRun();if(completed&&running)handler.post(runTick);
            }
        });
        drafts=new FlowDraftStore(prefs);projectId=saved!=null?saved.getString("projectId",prefs.getString("current",null)):prefs.getString("current",null);
        if(projectId!=null)try{String previous=projectId;FlowStore.EditorDocument opened=store.openEditor(projectId,null);document=opened.document;projectId=opened.id;if(opened.copied){copyEditorView(previous,projectId);prefs.edit().putString("current",projectId).apply();toast("Progetto già aperto: questa è una copia dell’ultimo salvataggio.");}}catch(Exception e){showLater("Non riesco ad aprire il progetto precedente. Il file è rimasto nell'archivio per il recupero.");}
        if(document==null){document=starter();projectId=store.newId();try{store.openEditor(projectId,document);}catch(IOException e){showLater("Non riesco ad aprire il progetto: "+e.getMessage());}FlowAutoLayout.apply(document);prefs.edit().putBoolean("auto_"+projectId,true).apply();dirty=true;project=document;saveNow();}
        project=document;activeFunctionId=prefs.getString("graph_"+projectId,null);if(FlowFunctionEditing.find(project,activeFunctionId)==null)activeFunctionId=null;document=FlowFunctionEditing.graph(project,activeFunctionId);
        if(saved!=null)pendingExportPath=saved.getString("pendingExport");
        autoArrange=prefs.getBoolean("auto_"+projectId,false);practiceLessonId="";mission=null;
        canvas=new FlowCanvasView(this);canvas.setListener(this);canvas.setSnapToGrid(prefs.getBoolean("snap_grid",true));canvas.setDark(Ui.dark);canvas.setMotionEnabled(Ui.motion);canvas.setMobileViewport(true);canvas.setDocument(document,true);canvas.setCapacityProvider(()->project.totalNodes()-document.nodes.size(),()->project.totalEdges()-document.edges.size());
        openingSpacingAdjusted=adaptOpeningText();
        study=new FlowStudy(this,new FlowStudy.Host(){
            public void installStudy(LinearLayout page){installStudyPage(page);}
            public void openEditor(){MainActivity.this.openEditor();}
            public void newProject(){createProject();}
            public void openProjects(){showProjects();}
            public void openSettings(){showAppSettings();}
            public void openSources(FlowLearning.Lesson lesson){showLearningSources(lesson);}
            public void practice(FlowLearning.Lesson lesson){practiceLesson(lesson);}
        });study.setProject(project.title,project.totalNodes());
        if(saved==null&&getIntent()!=null&&getIntent().getData()!=null){buildChrome();importUri(getIntent().getData());}
        else if(saved!=null&&!saved.getBoolean("hub",true))openEditor();
        else showHome();
        restoreSavedNavigation(saved);
        if(saved!=null&&projectId.equals(saved.getString("pendingOpenProject")))pendingOpen=openGuard.begin(project,documentRevision,projectId);
        if(saved!=null&&!hubVisible&&saved.getBoolean("data_test_open")){handler.post(()->{if(!destroyed&&!hubVisible&&activeSheet==null){showDataTests();if(dataTestPanel!=null)dataTestPanel.restore(saved);}});}
        if(saved!=null&&!hubVisible&&saved.containsKey("command_query")){String query=saved.getString("command_query","");int start=saved.getInt("command_start",0),end=saved.getInt("command_end",0);handler.post(()->{if(!destroyed&&!hubVisible&&activeSheet==null){showCommandPalette(query);if(commandsPanel!=null)commandsPanel.restoreSelection(start,end);}});}
        if(saved!=null&&!hubVisible&&saved.containsKey("variable_rename")){FlowVariableRenamePanel.State state=FlowVariableRenamePanel.State.read(saved);handler.post(()->{if(!destroyed&&!hubVisible&&activeSheet==null)showVariableRename(state);});}
        if(saved!=null&&!hubVisible&&saved.containsKey("search_query")){FlowProjectSearch.State searchState=FlowProjectSearch.State.read(saved);handler.post(()->{if(!destroyed&&!hubVisible&&activeSheet==null){showProjectSearch();if(projectSearch!=null)projectSearch.restore(searchState);}});}
        if(saved!=null&&!hubVisible&&saved.containsKey("diagramDesignDraft")){Bundle draft=saved.getBundle("diagramDesignDraft");handler.post(()->{if(!destroyed&&!hubVisible&&activeSheet==null)showDesignEditor(draft);});}
        if(saved!=null&&!hubVisible&&saved.containsKey("functionEditor")){String key=saved.getString("functionEditor");handler.post(()->{if(!destroyed&&!hubVisible&&activeSheet==null)showFunctionEditor("new".equals(key)?null:key);});}
    }
    private Document starter(){Document d=new Document();d.title="Somma veloce";Node a=new Node(FlowModel.START,"Inizio",34,20),b=new Node(FlowModel.PROCESS,"totale = 2 + 3",10,142),c=new Node(FlowModel.OUTPUT,"totale",3,300),e=new Node(FlowModel.END,"Fine",34,452);d.nodes.add(a);d.nodes.add(b);d.nodes.add(c);d.nodes.add(e);d.edges.add(new Edge(a.id,b.id,"next"));d.edges.add(new Edge(b.id,c.id,"next"));d.edges.add(new Edge(c.id,e.id,"next"));return d;}
    private void showLater(String s){handler.postDelayed(()->message("Recupero progetto",s),600);}
    private int dp(float v){return Ui.dp(this,v);}
    private void installStudyPage(LinearLayout page){
        openGuard.cancel();validationRequest++;if(runVisible)closeRun();stopRun();canvas.cancelConnection();saveNow();if(activeSheet!=null)activeSheet.dismiss();visibleEditorId=null;
        if(root!=null)((android.view.inputmethod.InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(root.getWindowToken(),0);
        if(canvas.getParent()!=null)((ViewGroup)canvas.getParent()).removeView(canvas);
        hubVisible=true;root=page;Ui.installContent(this,root);root.post(()->{if(!destroyed)Ui.systemBars(getWindow());});
    }
    private void createProject(){Document fresh=new Document();fresh.nodes.add(new Node(FlowModel.START,"Inizio",30,40));switchDocument(fresh,null);}
    private void copyEditorView(String from,String to){SharedPreferences.Editor edit=prefs.edit().putBoolean("auto_"+to,prefs.getBoolean("auto_"+from,false));String graph=prefs.getString("graph_"+from,null);if(graph!=null)edit.putString("graph_"+to,graph);edit.apply();}
    private void homeAction(LinearLayout parent,String label,String icon,boolean primary,Runnable action){Ui.Button button=new Ui.Button(this,label,icon,primary?2:0);button.setOnClickListener(v->action.run());button.setSingleLine(false);button.setMaxLines(3);button.setEllipsize(TextUtils.TruncateAt.END);button.setMinHeight(dp(54));parent.addView(button,new LinearLayout.LayoutParams(-1,-2));Ui.gap(parent,10);}
    private void showHome(){
        if(!saveNow())return;final LinearLayout page=Ui.column(this);page.setBackgroundColor(Ui.bg());
        LinearLayout heading=Ui.row(this);Ui.pad(heading,20,12,12,8);heading.addView(Ui.text(this,"Frociart",26,true),Ui.weight(this,-2));heading.addView(iconButton("settings","Preferenze e guida",()->showAppSettings()),Ui.lp(this,48,48));page.addView(heading);
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);LinearLayout content=Ui.column(this);Ui.pad(content,20,8,20,24);scroll.addView(content);page.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
        boolean wide=getResources().getConfiguration().screenWidthDp>=Math.max(720,(int)Math.ceil(680*getResources().getConfiguration().fontScale));
        LinearLayout board=new LinearLayout(this){@Override protected void onMeasure(int width,int height){int available=MeasureSpec.getSize(width);super.onMeasure(MeasureSpec.makeMeasureSpec(Math.min(available,dp(1040)),MeasureSpec.getMode(width)),height);}};board.setOrientation(wide?LinearLayout.HORIZONTAL:LinearLayout.VERTICAL);board.setGravity(Gravity.TOP);board.setTag("home-columns");LinearLayout actions=Ui.column(this),projects=Ui.column(this);actions.setTag("home-actions");projects.setTag("home-projects");
        LinearLayout.LayoutParams boardSize=new LinearLayout.LayoutParams(-1,-2);boardSize.gravity=Gravity.CENTER_HORIZONTAL;content.addView(board,boardSize);
        board.addView(actions,wide?new LinearLayout.LayoutParams(0,-2,1):new LinearLayout.LayoutParams(-1,-2));if(wide)board.addView(new View(this),Ui.lp(this,24,1));else Ui.gap(board,20);board.addView(projects,wide?new LinearLayout.LayoutParams(0,-2,1):new LinearLayout.LayoutParams(-1,-2));
        actions.addView(Ui.text(this,"I tuoi flowchart",24,true));Ui.gap(actions,6);actions.addView(Ui.note(this,"Crea, collega e prova le tue idee. Apri anche file Flowgorithm .fprg."));Ui.gap(actions,20);
        homeAction(actions,"Nuovo flowchart","add",true,()->createProject());homeAction(actions,"Prova un esempio","play",false,()->showExamples());homeAction(actions,"Apri dal telefono","folder",false,()->openDocument());homeAction(actions,"Importa pseudocodice","edit",false,()->showPseudocode());homeAction(actions,"Riprendi: "+project.title,"play",false,()->openEditor());
        projects.addView(Ui.text(this,"Progetti recenti",19,true));Ui.gap(projects,10);final LinearLayout recent=Ui.column(this);projects.addView(recent);final TextView loading=Ui.note(this,"Leggo i progetti…");recent.addView(loading);
        Ui.gap(projects,12);homeAction(projects,"Tutti i progetti","folder",false,()->showProjects());homeAction(projects,"Backup dei progetti","save",false,()->showBackup());TextView credit=Ui.note(this,"Un progetto di mamt104 · v1\nCopyright © 2026 mamt104");credit.setGravity(Gravity.CENTER);projects.addView(credit);homeAction(projects,"Autore e condizioni d’uso","help",false,()->showLicense());
        installStudyPage(page);page.setOnApplyWindowInsetsListener((v,insets)->{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});page.requestApplyInsets();
        io.execute(()->{List<FlowStore.Entry> entries=store.entries();handler.post(()->{if(destroyed||!hubVisible||root!=page)return;recent.removeView(loading);SimpleDateFormat date=new SimpleDateFormat("d MMM · HH:mm",Locale.ITALIAN);
            if(entries.isEmpty())recent.addView(Ui.note(this,"I tuoi progetti appariranno qui appena crei il primo flowchart."));
            for(FlowStore.Entry entry:entries.subList(0,Math.min(8,entries.size()))){LinearLayout row=Ui.column(this);Ui.pad(row,16,14,16,14);Ui.card(row,Ui.surface(),18);row.addView(Ui.text(this,entry.title,17,true));Ui.gap(row,5);row.addView(Ui.note(this,(entry.nodes<0?"File da recuperare":entry.nodes+" blocchi")+" · "+date.format(new Date(entry.modified))));row.setTag("recent:"+entry.id);row.setFocusable(true);row.setContentDescription("Apri progetto "+entry.title);row.setOnClickListener(v->{final int request=++projectOpenRequest;final Document expected=document;row.setEnabled(false);io.execute(()->{try{Document loaded=store.load(entry.id);handler.post(()->{row.setEnabled(true);if(!destroyed&&hubVisible&&root==page&&request==projectOpenRequest&&document==expected)switchDocument(loaded,entry.id);});}catch(Exception ex){handler.post(()->{row.setEnabled(true);if(!destroyed&&hubVisible&&root==page&&request==projectOpenRequest)message("Non riesco ad aprirlo",ex.getMessage());});}});});Ui.press(row);recent.addView(row);Ui.gap(recent,10);}
            Ui.enter(recent,0);
        });});
    }
    private void openEditor(){
        if(hubVisible)buildChrome();canvas.post(()->{if(!hubVisible)focusReadingStart();});
        if(openingSpacingAdjusted)showHint("Spaziatura adattata al testo · Annulla ripristina le posizioni originali");
        String id=drafts.lastNode(projectId,node->FlowFunctionEditing.graph(project,FlowFunctionEditing.ownerOf(project,node)).find(node)!=null);final String expected=projectId;if(id!=null&&FlowFunctionEditing.graph(project,FlowFunctionEditing.ownerOf(project,id)).find(id)!=null)handler.postDelayed(()->{if(!destroyed&&!hubVisible&&expected.equals(projectId)&&activeSheet==null){activateGraph(FlowFunctionEditing.ownerOf(project,id),false);editNode(id);}},320);
    }
    private void focusReadingStart(){String target=selectedNodeId;if(document.find(target)==null){target=null;for(Node n:document.nodes)if(n.kind==FlowModel.START){target=n.id;break;}if(target==null&&!document.nodes.isEmpty())target=document.nodes.get(0).id;}if(target!=null)canvas.frameNodeAnimated(target);}
    private void buildChrome(){
        cancelNavigationViewportRestore();cancelRuntimeInputReveal();editorToolsContainer=null;projectIdentityArea=null;graphToolbar=null;runtimeInputRow=null;inlineRuntimeInput=false;navigationBack=null;validationRequest++;hubVisible=false;canvas.setMotionEnabled(Ui.motion);canvas.setMobileViewport(true);
        canvas.setReadOnly(false);
        if(canvas.getParent()!=null)((ViewGroup)canvas.getParent()).removeView(canvas);
        Ui.systemBars(getWindow());root=Ui.column(this);root.setBackgroundColor(Ui.bg());root.setClipChildren(false);Ui.installContent(this,root);root.post(()->{if(!destroyed)Ui.systemBars(getWindow());});
        root.setOnApplyWindowInsetsListener((v,insets)->{
            v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());
            boolean ime=Build.VERSION.SDK_INT>=30?insets.isVisible(WindowInsets.Type.ime()):insets.getSystemWindowInsetBottom()>dp(100);
            keyboardVisible=ime;if(projectIdentityArea!=null)projectIdentityArea.setVisibility(ime&&runVisible?View.GONE:View.VISIBLE);landscapeIme=ime&&getResources().getConfiguration().orientation==Configuration.ORIENTATION_LANDSCAPE;updateRunKeyboardLayout();return insets;
        });if(Build.VERSION.SDK_INT>=30)root.setWindowInsetsAnimationCallback(new WindowInsetsAnimation.Callback(WindowInsetsAnimation.Callback.DISPATCH_MODE_CONTINUE_ON_SUBTREE){@Override public WindowInsets onProgress(WindowInsets insets,List<WindowInsetsAnimation> animations){return insets;}@Override public void onEnd(WindowInsetsAnimation animation){if((animation.getTypeMask()&WindowInsets.Type.ime())!=0&&compactRunLayout)revealRuntimeInputAfterLayout();}});root.requestApplyInsets();
        boolean phone=getResources().getConfiguration().screenWidthDp<600;boolean shortLandscape=compactHeight()&&getResources().getConfiguration().orientation==Configuration.ORIENTATION_LANDSCAPE;compactEditorActions=shortLandscape;compactAdd=null;LinearLayout compactTools=shortLandscape?Ui.row(this):null;LinearLayout header=Ui.actionRow(this);projectHeader=header;Ui.pad(header,12,shortLandscape?4:8,8,shortLandscape?4:8);
        header.addView(iconButton("home","Home e progetti recenti",()->showHome()),Ui.lp(this,48,48));
        LinearLayout titleArea=Ui.column(this);Ui.pad(titleArea,10,0,4,0);
        projectTitle=Ui.text(this,project.title,19,true);projectTitle.setSingleLine(!phone||shortLandscape);projectTitle.setMaxLines(phone&&!shortLandscape?2:1);projectTitle.setEllipsize(TextUtils.TruncateAt.END);
        projectTitle.setCompoundDrawablePadding(dp(4));projectTitle.setCompoundDrawablesRelativeWithIntrinsicBounds(null,null,new Ui.Icon(this,"down",Ui.muted(),15),null);
        titleArea.addView(projectTitle);saveStatus=Ui.text(this,"",11,false);saveStatus.setTextColor(Ui.muted());saveStatus.setSingleLine(!phone||shortLandscape);saveStatus.setMaxLines(phone&&!shortLandscape?3:1);saveStatus.setEllipsize(TextUtils.TruncateAt.END);Ui.pad(saveStatus,0,5,0,0);titleArea.addView(saveStatus);if(shortLandscape)saveStatus.setVisibility(View.GONE);
        titleArea.setMinimumHeight(dp(shortLandscape?48:52));titleArea.setGravity(Gravity.CENTER_VERTICAL);titleArea.setFocusable(true);titleArea.setOnClickListener(v->showProjectIdentity());titleArea.setContentDescription("Informazioni sul progetto aperto");
        if(phone&&!shortLandscape){boolean hideBrand=getResources().getConfiguration().screenWidthDp<360&&getResources().getConfiguration().fontScale>=1.5f;TextView brand=Ui.text(this,hideBrand?"":"Frociart",16,true);brand.setTag("app-brand");if(hideBrand)brand.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);brand.setGravity(Gravity.CENTER_VERTICAL);brand.setSingleLine(true);brand.setEllipsize(TextUtils.TruncateAt.END);header.addView(brand,Ui.weight(this,48));navigationBack=iconButton("back","Torna alla posizione precedente sul foglio, senza annullare modifiche",this::goBackNavigation);navigationBack.setTag("navigation-back");header.addView(navigationBack,Ui.lp(this,48,48));}else header.addView(titleArea,Ui.weight(this,-2));Ui.Button menuButton=new Ui.Button(this,"Menu",null,0);menuButton.setContentDescription("Menu: Progetto, Modifica, Esecuzione, Codice, Vista e Aiuto");menuButton.setOnClickListener(v->showMenu());header.addView(menuButton,Ui.lp(this,getResources().getConfiguration().fontScale>=1.5f?100:76,48));root.addView(header);if(phone&&!shortLandscape){Ui.pad(titleArea,16,0,16,4);titleArea.setTag("project-heading");projectIdentityArea=titleArea;root.addView(titleArea,new LinearLayout.LayoutParams(-1,-2));}
        boolean stackSaveError=getResources().getConfiguration().screenWidthDp<360&&getResources().getConfiguration().fontScale>=1.5f;saveErrorBar=stackSaveError?Ui.column(this):Ui.row(this);Ui.pad(saveErrorBar,12,0,12,8);
        TextView saveError=Ui.text(this,"Il lavoro è ancora aperto. Riprova a salvarlo o esporta una copia.",12,false);saveError.setTextColor(Ui.danger());
        saveErrorBar.addView(saveError,stackSaveError?new LinearLayout.LayoutParams(-1,-2):Ui.weight(this,-2));
        Ui.Button retrySave=new Ui.Button(this,"Riprova","save",2);fitSmallLabel(retrySave);retrySave.setContentDescription("Riprova il salvataggio del progetto");retrySave.setOnClickListener(v->{if(saveNow())toast("Progetto salvato");});
        LinearLayout.LayoutParams retrySize=Ui.lp(this,stackSaveError?-1:112,48);if(stackSaveError)retrySize.topMargin=dp(8);saveErrorBar.addView(retrySave,retrySize);saveErrorBar.setVisibility(saveFailed?View.VISIBLE:View.GONE);root.addView(saveErrorBar);

        workspace=new Ui.Workspace(this);canvasFrame=new FrameLayout(this);Ui.card(canvasFrame,Ui.surface(),26);canvasFrame.setClipToOutline(true);
        canvasFrame.addView(canvas,new FrameLayout.LayoutParams(-1,-1));workspace.drawing=canvasFrame;workspace.addView(canvasFrame,new LinearLayout.LayoutParams(-1,0,1));
        practiceBar=Ui.row(this);Ui.pad(practiceBar,12,2,12,8);
        practiceTitle=Ui.text(this,"",13,true);practiceTitle.setMaxLines(2);practiceTitle.setTextColor(Ui.accentInk());practiceTitle.setMinimumHeight(dp(48));practiceTitle.setGravity(Gravity.CENTER_VERTICAL);practiceTitle.setFocusable(true);practiceTitle.setOnClickListener(v->showPracticeGoal());practiceBar.addView(practiceTitle,Ui.weight(this,-2));
        practiceCheck=new Ui.Button(this,"Controlla",null,2);practiceCheck.setTextSize(13);Ui.pad(practiceCheck,8,10,8,10);practiceCheck.setOnClickListener(v->checkPractice());practiceBar.addView(practiceCheck,Ui.lp(this,getResources().getConfiguration().fontScale>=1.5f?148:112,48));root.addView(practiceBar);refreshPracticeBar();
        // Frequent document actions have fixed positions, outside the drawing.
        assistBar=Ui.actionRow(this);Ui.pad(assistBar,10,0,10,8);
        boolean largeText=getResources().getConfiguration().fontScale>=1.5f;
        undoButton=iconButton("undo","Annulla ultima modifica",()->undo());redoButton=iconButton("redo","Ripeti modifica",()->redo());assistBar.addView(undoButton,Ui.lp(this,48,48));assistBar.addView(redoButton,Ui.lp(this,48,48));
        arrangeButton=new Ui.Button(this,"Riordina",null,2);arrangeButton.setTextSize(13);Ui.pad(arrangeButton,6,8,6,8);arrangeButton.setContentDescription("Riordina i blocchi una volta");arrangeButton.setOnClickListener(v->arrangeOnce());LinearLayout.LayoutParams arrangeSize=Ui.weight(this,48);arrangeSize.weight=1.4f;assistBar.addView(arrangeButton,arrangeSize);
        Ui.Button all=new Ui.Button(this,largeText?"":"Inquadra",largeText?"center":null,0);all.setTextSize(13);Ui.pad(all,largeText?13:6,8,largeText?13:6,8);all.setContentDescription("Inquadra il diagramma");all.setOnClickListener(v->canvas.fitToContent());LinearLayout.LayoutParams frameSize=Ui.weight(this,48);if(largeText){frameSize.width=dp(48);frameSize.weight=0;}assistBar.addView(all,frameSize);if(compactTools!=null){Ui.pad(assistBar,10,0,4,0);Ui.pad(compactTools,0,0,6,8);compactTools.addView(assistBar,new LinearLayout.LayoutParams(0,-2,1.8f));root.addView(compactTools);}else if(!phone)root.addView(assistBar);
        graphButton=new Ui.Button(this,graphName(),"down",0);graphButton.setTag("function-navigation");graphButton.setContentDescription("Scegli diagramma o gestisci funzioni");graphButton.setOnClickListener(v->showGraphs());LinearLayout graphRow=Ui.actionRow(this);graphRow.addView(graphButton,Ui.weight(this,48));
        workspaceLayoutButton=new Ui.Button(this,"", "down",0);workspaceLayoutButton.setTag("workspace-layout");workspaceLayoutButton.setOnClickListener(v->showWorkspaceLayouts());graphRow.addView(workspaceLayoutButton,Ui.lp(this,146,48));if(compactTools!=null)compactTools.addView(graphRow,new LinearLayout.LayoutParams(0,-2,1));else if(phone){HorizontalScrollView tools=new HorizontalScrollView(this);tools.setHorizontalScrollBarEnabled(false);tools.setContentDescription("Strumenti del foglio e funzioni, scorri in orizzontale");editorToolsContainer=tools;arrangeButton.getLayoutParams().width=dp(largeText?156:96);((LinearLayout.LayoutParams)arrangeButton.getLayoutParams()).weight=0;all.getLayoutParams().width=dp(largeText?48:96);((LinearLayout.LayoutParams)all.getLayoutParams()).weight=0;graphRow.removeView(graphButton);graphRow.removeView(workspaceLayoutButton);assistBar.addView(graphButton,new LinearLayout.LayoutParams(-2,dp(48)));assistBar.addView(workspaceLayoutButton,Ui.lp(this,48,48));tools.addView(assistBar,new HorizontalScrollView.LayoutParams(-2,-2));root.addView(tools,new LinearLayout.LayoutParams(-1,-2));}else root.addView(graphRow);graphToolbar=compactTools!=null?compactTools:phone?editorToolsContainer:graphRow;
        workspaceTools=new HorizontalScrollView(this);workspaceTools.setHorizontalScrollBarEnabled(false);workspaceTools.setContentDescription("Comandi rapidi del layout, scorri in orizzontale");root.addView(workspaceTools,new LinearLayout.LayoutParams(-1,dp(64)));refreshWorkspaceLayout(false);

        // Selection actions replace the bottom actions without covering the diagram.
        actionDock=new FrameLayout(this);actionDock.setBackgroundColor(Ui.surface());
        int footerHeight=compactHeight()?64:Math.max(78,(int)Math.ceil(56+22*getResources().getConfiguration().fontScale));
        selectionBar=Ui.column(this);selectionBar.setMinimumHeight(dp(footerHeight));Ui.pad(selectionBar,12,6,12,8);
        selectionTitle=Ui.text(this,"",compactHeight()?12:11,true);selectionTitle.setTextColor(Ui.muted());selectionTitle.setSingleLine(true);selectionTitle.setEllipsize(TextUtils.TruncateAt.END);Ui.pad(selectionTitle,4,0,0,compactHeight()?0:3);if(!compactHeight())selectionBar.addView(selectionTitle);
        boolean splitSelection=!compactHeight()&&(getResources().getConfiguration().screenWidthDp<360||largeText);LinearLayout actions=Ui.actionRow(this);
        if(compactHeight()){selectionTitle.setGravity(Gravity.CENTER_VERTICAL);actions.addView(selectionTitle,new LinearLayout.LayoutParams(0,-1,1));}
        selectionEdit=new Ui.Button(this,"Modifica",null,2);fitSmallLabel(selectionEdit);selectionEdit.setOnClickListener(v->{
            if(connectionSource!=null)showDestinationPicker(connectionSource,connectionBranch);
            else if(selectedNodeId!=null)editNode(selectedNodeId);
        });
        selectionAdd=iconButton("add","Aggiungi un passaggio dopo il blocco selezionato",()->{if(selectedNodeId!=null)onAddAfterRequested(selectedNodeId);});
        selectionConnect=new Ui.Button(this,"Collega",null,0);fitSmallLabel(selectionConnect);selectionConnect.setOnClickListener(v->{if(selectedNodeId!=null)onConnectRequested(selectedNodeId);});
        selectionMore=iconButton("more","Altre azioni sulla selezione",()->showSelectionActions());
        selectionClose=iconButton("close","Deseleziona",()->{if(canvas.isConnecting())canvas.cancelConnection();else{canvas.clearSelection();setSelectionTool(false);}});
        actions.addView(selectionEdit,Ui.weight(this,48));if(!splitSelection)actions.addView(selectionAdd,Ui.lp(this,48,48));actions.addView(selectionConnect,Ui.weight(this,48));
        if(splitSelection){selectionBar.addView(actions);Ui.gap(selectionBar,8);LinearLayout extra=Ui.actionRow(this);extra.addView(selectionAdd,Ui.weight(this,48));extra.addView(selectionMore,Ui.weight(this,48));extra.addView(selectionClose,Ui.weight(this,48));selectionBar.addView(extra);}else{actions.addView(selectionMore,Ui.lp(this,48,48));actions.addView(selectionClose,Ui.lp(this,48,48));selectionBar.addView(actions);}
        actionDock.addView(selectionBar,new FrameLayout.LayoutParams(-1,-2,Gravity.BOTTOM));
        runPanel=Ui.column(this);Ui.card(runPanel,Ui.surface(),24);runPanel.setVisibility(View.GONE);workspace.panel=runPanel;workspace.addView(runPanel,new LinearLayout.LayoutParams(-1,-2));
        LinearLayout.LayoutParams wp=new LinearLayout.LayoutParams(-1,0,1);wp.setMargins(dp(10),0,dp(10),0);root.addView(workspace,wp);

        statusBar=Ui.row(this);Ui.pad(statusBar,10,2,10,2);if(compactHeight()||phone)statusBar.setVisibility(View.GONE);
        gridButton=new Ui.Toggle(this,"Griglia",null);gridButton.setTextSize(11);gridButton.setSingleLine(!largeText);gridButton.setMaxLines(largeText?2:1);if(largeText)gridButton.setMinHeight(dp(64));Ui.pad(gridButton,6,8,6,8);gridButton.setOnClickListener(v->{boolean enabled=!canvas.isSnapToGrid();canvas.setSnapToGrid(enabled);prefs.edit().putBoolean("snap_grid",enabled).apply();updateToolState();showHint(enabled?"I blocchi si allineano al rilascio.":"Posizionamento libero.");});statusBar.addView(gridButton,Ui.lp(this,largeText?108:100,largeText?-2:48));
        hint=Ui.text(this,"Tocca un blocco per continuare",11,false);hint.setTextColor(Ui.muted());hint.setMaxLines(largeText?3:2);Ui.pad(hint,10,2,0,2);statusBar.addView(hint,Ui.weight(this,-2));root.addView(statusBar);
        boolean wrapDock=largeText&&getResources().getConfiguration().screenWidthDp<360&&!shortLandscape;dock=Ui.row(this);if(wrapDock)dock.setBaselineAligned(false);dock.setMinimumHeight(dp(footerHeight));dock.setGravity(Gravity.CENTER_VERTICAL);Ui.pad(dock,12,compactHeight()?4:8,12,compactHeight()?6:12);
        Ui.Button add=new Ui.Button(this,"Aggiungi","add",1);fitSmallLabel(add);add.setContentDescription("Aggiungi dopo la selezione o Inizio; scegli Blocco libero per non collegarlo");add.setOnClickListener(v->showAddForContext());if(wrapDock){add.setSingleLine(false);add.setMaxLines(2);add.setMinHeight(dp(54));}dock.addView(add,Ui.weight(this,wrapDock?-2:54));
        dock.addView(new View(this),Ui.lp(this,10,1));runButton=new Ui.Button(this,"Apri prova","play",2);fitSmallLabel(runButton);runButton.setContentDescription("Apri prova senza avviare l’esecuzione");runButton.setOnClickListener(v->showRun(false));if(wrapDock){runButton.setSingleLine(false);runButton.setMaxLines(2);runButton.setMinHeight(dp(54));}dock.addView(runButton,Ui.weight(this,wrapDock?-2:54));actionDock.addView(dock,new FrameLayout.LayoutParams(-1,-2,Gravity.BOTTOM));root.addView(actionDock);
        if(compactEditorActions){
            dock.removeView(add);dock.removeView(runButton);compactAdd=add;add.setLabel("");add.setIcon("add");add.setContentDescription("Aggiungi un blocco");add.setTooltipText("Aggiungi un blocco");add.setTag("compact-add-block");
            runButton.setLabel("");runButton.setIcon("play");runButton.setContentDescription("Prova il flowchart");runButton.setTooltipText("Prova il flowchart");runButton.setTag("compact-run");
            header.addView(add,header.getChildCount()-1,Ui.lp(this,48,48));header.addView(runButton,header.getChildCount()-1,Ui.lp(this,48,48));
        }

        runVisible=false;refreshHeader();onSelectionChanged();updateToolState();canvas.setDark(Ui.dark);Ui.enter(dock,50);Ui.enter(assistBar,80);
    }
    private int workspaceMode(){return FlowWorkspaceLayout.valid(prefs.getInt("workspace_layout",0));}
    private void showWorkspaceLayouts(){
        if(canvas.isInteracting()||canvas.isConnecting()){showHint("Termina il gesto o il collegamento prima di cambiare layout.");return;}
        Ui.Sheet s=sheet("Layout di lavoro","Lo stesso progetto, con più o meno strumenti a vista. Tutte le funzioni restano disponibili in ogni layout.");
        for(int i=0;i<3;i++){final int mode=i;s.choice(FlowWorkspaceLayout.NAMES[i]+(workspaceMode()==i?" · attuale":""),FlowWorkspaceLayout.DESCRIPTIONS[i],Ui.accentInk(),()->{s.dismiss();setWorkspaceLayout(mode);});}s.show();
    }
    private void setWorkspaceLayout(int mode){
        if(canvas.isInteracting()||canvas.isConnecting())return;
        if(mode<0||mode>2)throw new IllegalArgumentException("Layout non valido");
        prefs.edit().putInt("workspace_layout",mode).apply();refreshWorkspaceLayout(true);
        showHint("Layout "+FlowWorkspaceLayout.NAMES[mode]+" · tutte le funzioni disponibili");
    }
    private void refreshWorkspaceLayout(boolean animate){
        if(workspaceLayoutButton==null||workspaceTools==null)return;int mode=workspaceMode();
        if(animate&&root!=null){android.transition.TransitionManager.endTransitions(root);if(Ui.animations()&&root.isLaidOut()){android.transition.TransitionSet transition=new android.transition.TransitionSet().setOrdering(android.transition.TransitionSet.ORDERING_TOGETHER);transition.addTransition(new android.transition.ChangeBounds());transition.addTransition(new android.transition.Fade());transition.setDuration(320);transition.setInterpolator(Ui.SPRING);android.transition.TransitionManager.beginDelayedTransition(root,transition);}}
        boolean large=getResources().getConfiguration().fontScale>=1.5f;
        boolean compactLayout=large||compactHeight()||getResources().getConfiguration().screenWidthDp<600;workspaceLayoutButton.setLabel(compactLayout?"":FlowWorkspaceLayout.NAMES[mode]);workspaceLayoutButton.setIcon(compactLayout?"settings":"down");workspaceLayoutButton.getLayoutParams().width=dp(compactLayout?48:146);workspaceLayoutButton.requestLayout();workspaceLayoutButton.setContentDescription("Layout di lavoro: "+FlowWorkspaceLayout.NAMES[mode]+". Cambia layout");
        workspaceTools.removeAllViews();LinearLayout row=Ui.actionRow(this);Ui.pad(row,10,8,10,8);workspaceTools.addView(row,new HorizontalScrollView.LayoutParams(-2,-1));
        addWorkspaceTool(row,"Modifica","edit",()->showDrawTools());addWorkspaceTool(row,"Esecuzione","play",()->showRunTools());addWorkspaceTool(row,"Codice","edit",()->showCodeTools());
        if(mode==2)addWorkspaceTool(row,"Vista","settings",()->showViewTools());
        workspaceTools.setVisibility(mode>0&&!compactHeight()&&!runVisible?View.VISIBLE:View.GONE);
        if(animate)Ui.enter(workspaceLayoutButton,0);
    }
    private void addWorkspaceTool(LinearLayout row,String name,String icon,Runnable action){
        Ui.Button button=new Ui.Button(this,name,icon,0);button.setOnClickListener(v->action.run());button.setContentDescription(name);row.addView(button,new LinearLayout.LayoutParams(-2,dp(48)));
    }
    private void setSelectionTool(boolean selected){
        if(canvas.isConnecting())canvas.cancelConnection();canvas.setSelectionMode(selected);updateToolState();
        showHint(selected?"Gruppo: trascina un riquadro o tocca i blocchi.":"Trascina per spostare · pizzica per lo zoom");
    }
    private void updateToolState(){
        if(gridButton==null)return;boolean enabled=canvas.isSnapToGrid();gridButton.setChecked(enabled);gridButton.setLabel("Griglia"+(getResources().getConfiguration().fontScale>=1.5f?"\n":" ")+(enabled?"ON":"OFF"));gridButton.setContentDescription("Snap alla griglia: "+(enabled?"attivo":"disattivo"));
    }
    private void showSelectionActions(){
        Ui.Sheet s=sheet("Selezione",canvas.getSelectedIds().size()+" blocchi selezionati");
        if(canvas.canFoldSelection())s.action(canvas.selectionCycleFolded()?"Espandi ciclo sul foglio":"Riduci ciclo sul foglio","undo",2,()->{s.dismiss();canvas.toggleSelectedCycle();});
        if(selectedNodeId!=null&&document.find(selectedNodeId)!=null&&document.find(selectedNodeId).kind!=FlowModel.END&&document.find(selectedNodeId).kind!=FlowModel.NOTE)s.action("Aggiungi un passaggio","add",2,()->{s.dismiss();onAddAfterRequested(selectedNodeId);});
        if(selectedNodeId!=null&&document.find(selectedNodeId)!=null&&document.find(selectedNodeId).kind!=FlowModel.END&&document.find(selectedNodeId).kind!=FlowModel.NOTE)s.action("Collegamenti","decision",0,()->{s.dismiss();onConnectRequested(selectedNodeId);});
        if(selectedNodeId!=null&&document.find(selectedNodeId)!=null&&document.find(selectedNodeId).kind!=FlowModel.NOTE){Ui.Button reach=s.action("Esegui fino a questo blocco","step",0,()->{s.dismiss();runToSelected();});reach.setEnabled(canRunToSelected());}
        if(selectedNodeId!=null&&document.find(selectedNodeId)!=null&&document.find(selectedNodeId).kind!=FlowModel.NOTE)s.action("Punto di interruzione…","pause",0,()->{String id=selectedNodeId;s.dismiss();editBreakpoint(id);});
        s.action("Duplica selezione","duplicate",0,()->{s.dismiss();canvas.duplicateSelection();});
        s.action("Elimina selezione","trash",3,()->{s.dismiss();Set<String> deleting=new HashSet<>(canvas.getSelectedIds());canvas.deleteSelection();for(String id:deleting)if(document.find(id)==null)drafts.remove(projectId,id);});s.show();
    }
    private Ui.Button iconButton(String icon,String description,Runnable action){Ui.Button b=new Ui.Button(this,"",icon,0);b.setContentDescription(description);b.setOnClickListener(v->action.run());return b;}
    private boolean compactLabels(){return getResources().getConfiguration().screenWidthDp<360||getResources().getConfiguration().fontScale>1.15f;}
    private boolean compactHeight(){return getResources().getConfiguration().screenHeightDp<440;}
    private boolean inlineConnectionFits(){
        int width=root.getWidth()>0?root.getWidth()-root.getPaddingLeft()-root.getPaddingRight():dp(getResources().getConfiguration().screenWidthDp);
        boolean split=!compactHeight()&&(getResources().getConfiguration().screenWidthDp<360||getResources().getConfiguration().fontScale>=1.5f);float slot=split?(width-dp(32))/2f:(width-dp(24+32+144))/(compactHeight()?3f:2f);
        return slot>=dp(48)&&selectionEdit.getPaint().measureText("Modifica")+selectionEdit.getPaddingLeft()+selectionEdit.getPaddingRight()<=slot&&selectionConnect.getPaint().measureText("Collega")+selectionConnect.getPaddingLeft()+selectionConnect.getPaddingRight()<=slot;
    }
    private void fitSmallLabel(Ui.Button b){if(compactLabels()){b.setIcon(null);int vertical=getResources().getConfiguration().fontScale>=1.5f?6:10;Ui.pad(b,4,vertical,4,vertical);}}
    private Ui.Sheet sheet(String title,String subtitle){openGuard.cancel();validationRequest++;if(runVisible){stopRun();refreshRun();}if(activeSheet!=null&&activeSheet.dialog.isShowing())activeSheet.dismiss();Ui.Sheet s=new Ui.Sheet(this,title,subtitle);activeSheet=s;s.dialog.setOnDismissListener(d->{if(activeSheet==s)activeSheet=null;});return s;}
    private void message(String title,String text){Ui.Sheet s=sheet(title,null);s.paragraph(text);s.primary("Ho capito","check",s::dismiss);s.show();}
    private void toast(String text){Toast.makeText(this,text,Toast.LENGTH_SHORT).show();}
    private void showHint(String text){if(hint!=null)hint.setText(text);}
    String expressionScopeId(){return activeFunctionId;}
    String expressionEditorId(){return visibleEditorId;}
    boolean expressionContextCurrent(Document expected,String scope,String node){return project==expected&&java.util.Objects.equals(activeFunctionId,scope)&&(node==null||java.util.Objects.equals(visibleEditorId,node)&&document.find(node)!=null);}
    private String graphName(){FlowModel.Function f=FlowFunctionEditing.find(project,activeFunctionId);return f==null?"Principale · Funzioni…":f.name+" · Funzioni…";}
    private static final class NavigationPoint {
        final String function;final ArrayList<String> selection;final float[] viewport;
        NavigationPoint(String function,Collection<String> selection,float[] viewport){this.function=function;this.selection=new ArrayList<>(selection);this.viewport=viewport.clone();}
        boolean same(NavigationPoint other){return Objects.equals(function,other.function)&&selection.equals(other.selection)&&Arrays.equals(viewport,other.viewport);}
    }
    private void rememberNavigation(){if(restoringNavigation||canvas==null||canvas.getWidth()==0||hubVisible||canvas.isInteracting())return;NavigationPoint point=new NavigationPoint(activeFunctionId,canvas.getSelectedIds(),canvas.captureViewport());if(navigationHistory.isEmpty()||!navigationHistory.peekLast().same(point)){navigationHistory.addLast(point);while(navigationHistory.size()>64)navigationHistory.removeFirst();}if(navigationBack!=null)navigationBack.setEnabled(true);}
    // Runtime selection is complete; recreation uses a bounded view-only snapshot.
    private static final int NAVIGATION_BUNDLE_BUDGET=64*1024;
    private static long selectionStateBytes(Collection<String> ids){long bytes=128;for(String id:ids)bytes+=64L+2L*(id==null?0:id.length());return bytes;}
    private static long navigationStateBytes(NavigationPoint point){return 256L+2L*(point.function==null?0:point.function.length())+selectionStateBytes(point.selection);}
    private void saveNavigation(Bundle out){
        out.putString("projectId",projectId);ArrayList<Bundle> list=new ArrayList<>();long used=128;Iterator<NavigationPoint> recent=navigationHistory.descendingIterator();boolean limited=false;
        while(recent.hasNext()){NavigationPoint point=recent.next();long bytes=navigationStateBytes(point);if(used+bytes>NAVIGATION_BUNDLE_BUDGET){limited=true;continue;}Bundle b=new Bundle();b.putString("function",point.function);b.putStringArrayList("selection",point.selection);b.putFloatArray("viewport",point.viewport);list.add(0,b);used+=bytes;}
        out.putParcelableArrayList("viewHistory",list);out.putFloatArray("currentViewport",canvas.captureViewport());ArrayList<String> selected=new ArrayList<>(canvas.getSelectedIds());if(selectionStateBytes(selected)<=NAVIGATION_BUNDLE_BUDGET)out.putStringArrayList("currentSelection",selected);else limited=true;out.putBoolean("viewHistoryLimited",limited);
    }
    private void cancelNavigationViewportRestore(){navigationViewportEpoch++;if(navigationViewportListener!=null&&navigationViewportObserver!=null&&navigationViewportObserver.isAlive())navigationViewportObserver.removeOnPreDrawListener(navigationViewportListener);navigationViewportListener=null;navigationViewportObserver=null;}
    private void restoreNavigationViewport(Document expected,String function,float[] viewport,Runnable restored){
        cancelNavigationViewportRestore();final int epoch=navigationViewportEpoch;final FlowCanvasView target=canvas;final LinearLayout expectedRoot=root;final Set<String> selected=canvas.getSelectedIds();final float[] state=viewport.clone();
        ViewTreeObserver observer=target.getViewTreeObserver();navigationViewportObserver=observer;
        navigationViewportListener=new ViewTreeObserver.OnPreDrawListener(){
            @Override public boolean onPreDraw(){
                boolean current=epoch==navigationViewportEpoch&&!destroyed&&!hubVisible&&project==expected&&Objects.equals(activeFunctionId,function)&&root==expectedRoot&&canvas==target&&selected.equals(target.getSelectedIds())&&!target.isInteracting();
                if(current&&(expectedRoot.isLayoutRequested()||target.isLayoutRequested()))return true;
                if(observer.isAlive())observer.removeOnPreDrawListener(this);if(navigationViewportListener==this){navigationViewportListener=null;navigationViewportObserver=null;}
                if(current&&target.restoreViewport(state)){refreshHeader();if(restored!=null)restored.run();}return true;
            }
        };observer.addOnPreDrawListener(navigationViewportListener);target.invalidate();
    }
    private void restoreSavedNavigation(Bundle saved){
        if(saved==null||!Objects.equals(projectId,saved.getString("projectId")))return;ArrayList<Bundle> list=saved.getParcelableArrayList("viewHistory");if(list!=null)for(Bundle b:list){float[] viewport=b.getFloatArray("viewport");ArrayList<String> selected=b.getStringArrayList("selection");if(viewport!=null&&viewport.length==3&&selected!=null){navigationHistory.addLast(new NavigationPoint(b.getString("function"),selected,viewport));while(navigationHistory.size()>64)navigationHistory.removeFirst();}}
        float[] viewport=saved.getFloatArray("currentViewport");ArrayList<String> selected=saved.getStringArrayList("currentSelection");if(viewport!=null&&!hubVisible){if(selected!=null)canvas.selectGroup(selected);restoreNavigationViewport(project,activeFunctionId,viewport,()->{if(saved.getBoolean("viewHistoryLimited"))showHint("Vista ripristinata. Una selezione o parte della cronologia molto ampia non è stata conservata dopo la ricreazione.");});}
    }
    private void goBackNavigation(){
        if(canvas.isInteracting()||canvas.isConnecting())return;NavigationPoint point=null;
        while(!navigationHistory.isEmpty()){NavigationPoint candidate=navigationHistory.removeLast();if(candidate.function==null||FlowFunctionEditing.find(project,candidate.function)!=null){Document graph=FlowFunctionEditing.graph(project,candidate.function);boolean survives=candidate.selection.isEmpty();for(String id:candidate.selection)if(graph.find(id)!=null)survives=true;if(survives){point=candidate;break;}}}
        if(point==null){refreshHeader();showHint("Nessuna posizione precedente in questo progetto.");return;}
        final NavigationPoint restore=point;final Document expected=project;restoringNavigation=true;
        try{if(runVisible)closeRun();if(!activateGraph(restore.function,false))return;canvas.selectGroup(restore.selection);restoreNavigationViewport(expected,restore.function,restore.viewport,()->showHint("Posizione precedente ripristinata · il diagramma non è stato modificato"));}finally{restoringNavigation=false;refreshHeader();}
    }
    private boolean activateGraph(String id,boolean following){
        if(FlowFunctionEditing.find(project,id)==null)id=null;
        Document target=FlowFunctionEditing.graph(project,id);if(document==target)return true;
        if(canvas.isInteracting())return false;
        if(!following){rememberNavigation();openGuard.cancel();validationRequest++;if(runVisible)closeRun();if(activeSheet!=null)activeSheet.dismiss();}
        cancelNavigationViewportRestore();activeFunctionId=id;document=target;selectedNodeId=null;canvas.setSelectionMode(false);canvas.setDocument(document,true);canvas.setReadOnly(runVisible);
        if(!following)prefs.edit().putString("graph_"+projectId,id).apply();
        if(Ui.motion&&(!following||speed!=2))Ui.enter(canvas,0);refreshHeader();return true;
    }
    private void restoreHistory(FlowFunctionEditing.History history){
        cancelNavigationViewportRestore();if(runVisible)closeRun();openGuard.cancel();validationRequest++;project=history.project;activeFunctionId=history.functionId;document=FlowFunctionEditing.graph(project,activeFunctionId);selectedNodeId=null;
        prefs.edit().putString("graph_"+projectId,activeFunctionId).apply();canvas.setDocument(document,true);Ui.enter(canvas,0);
    }
    private void showGraphs(){
        if(canvas.isInteracting())return;if(runVisible)closeRun();Ui.Sheet s=sheet("Diagrammi e funzioni","Principale avvia il programma. Le funzioni sono diagrammi riutilizzabili con parametri e variabili proprie.");
        s.action("Principale","home",activeFunctionId==null?2:0,()->{s.dismiss();activateGraph(null,false);});
        if(project.functions!=null)for(FlowModel.Function function:project.functions){
            s.choice(function.name,FlowPseudocode.signature(function),Ui.accentInk(),()->{s.dismiss();activateGraph(function.id,false);});
            s.action("Modifica definizione · "+function.name,"edit",0,()->{s.dismiss();showFunctionEditor(function.id);});
        }
        String draft=prefs.getString("function_draft_last_"+projectId,null);
        if(draft!=null&&prefs.contains("function_draft_"+projectId+"_"+draft)&&("new".equals(draft)||FlowFunctionEditing.find(project,draft)!=null))s.action("Riprendi definizione non applicata","edit",0,()->{s.dismiss();showFunctionEditor("new".equals(draft)?null:draft);});
        s.action("Importa da un progetto…","folder",0,()->{s.dismiss();openFunctionImport();});
        if(project.functions!=null&&!project.functions.isEmpty())s.action("Esporta una funzione…","export",0,()->{s.dismiss();chooseFunctionExport();});
        if(project.functions==null||project.functions.size()<FlowFunctions.MAX_FUNCTIONS)s.primary("Nuova funzione","add",()->{s.dismiss();showFunctionEditor(null);});
        else s.paragraph("Hai raggiunto il limite di 32 funzioni.");s.show();
    }
    private void showFunctionEditor(String id){
        if(canvas.isInteracting())return;if(runVisible)closeRun();
        FlowModel.Function function=FlowFunctionEditing.find(project,id);if(id!=null&&function==null)return;
        final Document expected=project;final long revision=documentRevision;final String currentProject=projectId,key=id==null?"new":id;
        final String storage="function_draft_"+currentProject+"_"+key,last="function_draft_last_"+currentProject;
        Ui.Sheet s=sheet(function==null?"Nuova funzione":"Definizione di "+function.name,"La X conserva la bozza. Applica apre il diagramma della funzione.");
        functionEditor=new FlowFunctionEditor(this,s,project,function,prefs.getString(storage,null),text->prefs.edit().putString(storage,text).putString(last,key).apply(),()->{SharedPreferences.Editor edit=prefs.edit().remove(storage);if(key.equals(prefs.getString(last,null)))edit.remove(last);edit.apply();},(candidate,selected,rename)->{
            if(project!=expected||documentRevision!=revision||!projectId.equals(currentProject))throw new IllegalArgumentException("Il progetto è cambiato. Chiudi e riapri la definizione: la bozza è conservata.");
            saveFunctionChange(candidate,rename,false);
            onBeforeChange();undo.getLast().rename=rename;project=candidate;activeFunctionId=selected;document=FlowFunctionEditing.graph(project,selected);selectedNodeId=null;canvas.setDocument(document,true);prefs.edit().putString("graph_"+projectId,selected).apply();changed(selected==null?"Funzione eliminata":"Funzione aggiornata");saveNow();Ui.enter(canvas,0);
        });
        final FlowFunctionEditor current=functionEditor;s.dialog.setOnDismissListener(d->{current.closeChild();if(functionEditor==current)functionEditor=null;if(activeSheet==s)activeSheet=null;});
    }
    private boolean currentFunctionImport(FunctionImportTicket ticket){return ticket!=null&&!destroyed&&ticket.original==project&&ticket.revision==documentRevision&&ticket.projectId.equals(projectId)&&ticket.epoch==functionImportEpoch;}
    private void openFunctionImport(){
        if(canvas.isInteracting()||!saveNow())return;
        pendingFunctionImport=new FunctionImportTicket(project,projectId,documentRevision,++functionImportEpoch);
        Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("*/*");
        try{startActivityForResult(intent,FUNCTION_OPEN);}catch(ActivityNotFoundException e){pendingFunctionImport=null;message("Selettore non disponibile","Non trovo un'app per scegliere il progetto .frociart.");}
    }
    private void readFunctionImport(Uri uri,FunctionImportTicket ticket){
        if(!currentFunctionImport(ticket)){toast("Importazione annullata: il progetto è cambiato.");return;}
        showHint("Leggo le funzioni…");io.execute(()->{try{Document source=store.importUri(uri);handler.post(()->{if(currentFunctionImport(ticket))chooseFunctionImport(source,ticket);});}
        catch(Exception e){handler.post(()->{if(currentFunctionImport(ticket))message("Importazione non riuscita",e.getMessage());});}});
    }
    private void chooseFunctionImport(Document source,FunctionImportTicket ticket){
        if(!currentFunctionImport(ticket))return;
        Ui.Sheet s=sheet("Importa una funzione","Scegli cosa riusare. Le funzioni richiamate sono incluse automaticamente.");
        if(source.functions==null||source.functions.isEmpty())s.paragraph("Il file non contiene funzioni. Scegli un progetto in cui ne hai già creata una.");
        else for(FlowModel.Function f:source.functions)s.choice(f.name,FlowPseudocode.signature(f),Ui.accentInk(),()->{
            s.dismiss();if(!currentFunctionImport(ticket))return;showHint("Preparo l'importazione…");
            io.execute(()->{try{FlowFunctionTransfer.Plan plan=FlowFunctionTransfer.prepare(ticket.snapshot,source,f.id);handler.post(()->{if(currentFunctionImport(ticket))confirmFunctionImport(plan,ticket);});}
            catch(Exception e){handler.post(()->{if(currentFunctionImport(ticket))message("Funzione non importata",e.getMessage());});}});
        });s.show();
    }
    private void confirmFunctionImport(FlowFunctionTransfer.Plan plan,FunctionImportTicket ticket){
        if(!currentFunctionImport(ticket))return;Ui.Sheet s=sheet(plan.added.size()==1?"Importa funzione":"Importa "+plan.added.size()+" funzioni","Controlla le definizioni e gli eventuali nuovi nomi.");s.paragraph(plan.description());
        s.primary("Importa funzioni","check",()->{
            if(!currentFunctionImport(ticket)){s.dismiss();toast("Importazione annullata: il progetto è cambiato.");return;}
            try{applyFunctionImport(plan);functionImportEpoch++;s.dismiss();}catch(IllegalArgumentException e){s.paragraph(e.getMessage());}
        });s.show();
    }
    private void applyFunctionImport(FlowFunctionTransfer.Plan plan){
        if(!FlowCodec.encode(project).equals(plan.baseDocument))throw new IllegalArgumentException("Il progetto è cambiato. Riapri l'importazione per conservare le modifiche più recenti.");FlowCodec.check(plan.project);saveFunctionChange(plan.project,null,false);onBeforeChange();project=plan.project;activeFunctionId=plan.selectedId;document=FlowFunctionEditing.graph(project,activeFunctionId);selectedNodeId=null;canvas.setDocument(document,true);prefs.edit().putString("graph_"+projectId,activeFunctionId).apply();changed("Importazione completata · Annulla ripristina il progetto");saveNow();Ui.enter(canvas,0);
    }
    private void chooseFunctionExport(){
        if(project.functions==null||project.functions.isEmpty())return;
        final Document snapshot=project.copy();Ui.Sheet s=sheet("Esporta una funzione","Il file include le funzioni richiamate e si può importare su PC e telefono.");
        for(FlowModel.Function f:snapshot.functions)s.choice(f.name,FlowPseudocode.signature(f),Ui.accentInk(),()->{
            try{Document library=FlowFunctionTransfer.export(snapshot,f.id);s.dismiss();saveTextExport(FlowCodec.encode(library),"Funzioni_"+f.name+".frociart",".frociart");}
            catch(IllegalArgumentException e){s.paragraph(e.getMessage());}
        });s.show();
    }
    private void showCallStack(){
        showVariables();
    }
    private void showVariables(){
        if(canvas.isInteracting())return;if(!runVisible)showRun(false);if(!runVisible||!ensureRunner())return;
        if(variablePanel!=null&&variablePanel.sheet.dialog.isShowing()){variablePanel.refresh();return;}
        stopRun();resumeAfterInput=false;refreshRun();Ui.Sheet s=sheet("Variabili",null);
        FlowVariablePanel panel=new FlowVariablePanel(this,s,new FlowVariablePanel.Host(){
            public FlowEngine.Runner runner(){return MainActivity.this.runner;}
            public FlowWatchExpressions.Session watchExpressions(){FlowWatchExpressions.Session session=watchSessions.get(projectId);if(session==null){session=new FlowWatchExpressions.Session();watchSessions.put(projectId,session);}return session;}
            public boolean running(){return MainActivity.this.running;}
            public String pending(){return runInput==null?"":runInput.getText().toString();}
            public void pending(String value){if(runInput!=null)runInput.setText(value);}
            public void submit(String value){if(runner==null||runner.state!=FlowEngine.State.WAITING||runInput==null)return;runInput.setText(value);submitInput();}
            public void pause(){stopRun();resumeAfterInput=false;}
            public void play(){if(running){stopRun();resumeAfterInput=false;}else{if(runner!=null&&runner.state==FlowEngine.State.ERROR&&!resetRunner())return;startRun();}refreshRun();}
            public void step(){debugStep(FlowDebugger.Command.INTO);}
            public void file(){fileUi.chooseCurrent();}
            public void save(FlowVariableInspection value){stopRun();resumeAfterInput=false;refreshRun();saveTextExport(()->{String text=value.exportText();if(text.length()>8*1024*1024)throw new IOException("Le variabili di questa chiamata superano il limite di 8 milioni di caratteri.");return text;},"Frociart_variabili.txt",".txt");}
            public void locate(String function,String node){rememberNavigation();stopRun();resumeAfterInput=false;refreshRun();activateGraph(function,true);canvas.select(node);canvas.focusNodeAnimated(node);}
        });variablePanel=panel;s.dialog.setOnDismissListener(d->{panel.close();if(variablePanel==panel)variablePanel=null;if(activeSheet==s)activeSheet=null;});s.show();panel.refresh();
    }
    private void closeVariables(){FlowVariablePanel panel=variablePanel;variablePanel=null;if(panel!=null){panel.close();panel.sheet.dismissNow();if(activeSheet==panel.sheet)activeSheet=null;}}
    private void showProjectIdentity(){Ui.Sheet s=sheet("Progetto aperto",null);s.paragraph(project.title);s.paragraph(saveFailed?"Salvataggio non riuscito. Il lavoro è ancora aperto: riprova o esporta una copia.":dirty?"Salvataggio in attesa.":"Salvato sul telefono.");s.paragraph(project.totalNodes()+" blocchi · "+FlowWorkspaceLayout.NAMES[workspaceMode()]);s.action("I miei progetti","folder",0,()->{s.dismiss();showProjects();});s.action("Rinomina progetto","edit",0,()->{s.dismiss();rename();});s.show();}
    private void refreshHeader(){
        if(compactAdd!=null){compactAdd.setVisibility(runVisible?View.GONE:View.VISIBLE);if(runButton!=null)runButton.setVisibility(runVisible?View.GONE:View.VISIBLE);}
        if(hubVisible||projectTitle==null)return;if(workspaceTools!=null)workspaceTools.setVisibility(workspaceMode()>0&&!compactHeight()&&!runVisible?View.VISIBLE:View.GONE);projectTitle.setText(project.title);projectTitle.setContentDescription("Progetto aperto: "+project.title+". Tocca per leggere tutti i dettagli.");if(projectIdentityArea!=null)projectIdentityArea.setContentDescription("Dettagli del progetto "+project.title);if(graphButton!=null){graphButton.setLabel(graphName());graphButton.setEnabled(!runVisible);}
        if(navigationBack!=null)navigationBack.setEnabled(!navigationHistory.isEmpty());undoButton.setEnabled(!undo.isEmpty());redoButton.setEnabled(!redo.isEmpty());updateArrangeButton();
        String status=saveFailed?"Salvataggio non riuscito":dirty?"Salvataggio…":"Salvato sul telefono";
        saveStatus.setText(status+" · "+project.totalNodes()+(project.totalNodes()==1?" blocco":" blocchi"));saveStatus.setTextColor(saveFailed?Ui.danger():Ui.muted());
        if(saveErrorBar!=null)saveErrorBar.setVisibility(saveFailed?View.VISIBLE:View.GONE);
    }
    @Override public void onBeforeChange(){openGuard.cancel();if(runVisible)closeRun();stopRun();setRunner(null);canvas.clearRunAnimation();canvas.setHighlight(null);undo.addLast(new FlowFunctionEditing.History(project,activeFunctionId));while(undo.size()>60)undo.removeFirst();redo.clear();}
    @Override public void onCyclePresentationChanged(){documentRevision++;dirty=true;refreshHeader();saveNow();}
    @Override public void onDiagramChanged(String label){FlowAnnotations.promote(project);if(autoArrange)reflow();documentRevision++;dirty=true;saveFailed=false;refreshHeader();onSelectionChanged();handler.removeCallbacks(saveTask);handler.postDelayed(saveTask,650);}
    private Map<String,float[]> positions(){Map<String,float[]> result=new LinkedHashMap<>();for(Node n:document.nodes)result.put(n.id,new float[]{n.x,n.y});return result;}
    private void reflow(){Map<String,float[]> before=positions();if(FlowAutoLayout.apply(document)){canvas.notifyModelChanged();canvas.animateLayout(before,positions(),300);}}
    private void updateArrangeButton(){if(arrangeButton!=null)arrangeButton.setEnabled(!document.nodes.isEmpty());}
    private void arrangeOnce(){
        if(hubVisible||runVisible)return;Document planned=document.copy();Map<String,float[]> before=positions();
        if(FlowAutoLayout.apply(planned)){
            onBeforeChange();for(int i=0;i<document.nodes.size();i++){document.nodes.get(i).x=planned.nodes.get(i).x;document.nodes.get(i).y=planned.nodes.get(i).y;}
            changed("Blocchi riordinati");canvas.arrangeAnimated(before);showHint("Blocchi riordinati · Annulla per ripristinare");
        }else showHint("I blocchi sono già ordinati.");
    }
    private void setAutoArrange(boolean enabled){
        if(autoArrange==enabled)return;
        if(enabled){Document planned=document.copy();if(FlowAutoLayout.apply(planned)){onBeforeChange();Map<String,float[]> before=positions();for(int i=0;i<document.nodes.size();i++){document.nodes.get(i).x=planned.nodes.get(i).x;document.nodes.get(i).y=planned.nodes.get(i).y;}autoArrange=true;changed("Blocchi ordinati");canvas.animateLayout(before,positions(),340);}}
        autoArrange=enabled;prefs.edit().putBoolean("auto_"+projectId,enabled).apply();updateArrangeButton();
        showHint(enabled?"Ordina attivo: spazi regolari a ogni nuovo passaggio.":"Posizione libera: trascina i blocchi dove preferisci.");
        if(enabled)canvas.post(()->focusReadingStart());
    }
    @Override public void onManualNodeMove(){if(autoArrange){autoArrange=false;prefs.edit().putBoolean("auto_"+projectId,false).apply();updateArrangeButton();showHint("Posizione libera · Riordina per riallineare");}}
    private void changed(String label){canvas.notifyModelChanged();onDiagramChanged(label);}
    private void undo(){moveHistory(undo,redo,true);}
    private void redo(){moveHistory(redo,undo,false);}
    private void moveHistory(ArrayDeque<FlowFunctionEditing.History> from,ArrayDeque<FlowFunctionEditing.History> to,boolean reverse){
        if(from.isEmpty())return;FlowFunctionEditing.History target=from.peekLast();
        try{if(target.rename!=null)saveFunctionChange(target.project,target.rename,reverse);}
        catch(IllegalArgumentException e){message("Bozza da conservare",e.getMessage());return;}
        if(target.appearance!=null){openingSpacingAdjusted=false;FlowFunctionEditing.History current=new FlowFunctionEditing.History(project,activeFunctionId);current.appearance=canvas.appearanceStyle();to.addLast(current);from.removeLast();applyDesignPositions(target.project,target.appearance);showHint(reverse?"Modifica annullata · Ripeti per ripristinarla":"Modifica ripetuta");return;}
        stopRun();setRunner(null);FlowFunctionEditing.History current=new FlowFunctionEditing.History(project,activeFunctionId);current.rename=target.rename;to.addLast(current);from.removeLast();restoreHistory(target);documentRevision++;dirty=true;saveNow();refreshHeader();if(reverse)showHint("Modifica annullata · Ripeti per ripristinarla");
    }
    private void saveFunctionChange(Document candidate,FlowFunctionEditing.Rename rename,boolean reverse){
        if(!saveNow())throw new IllegalArgumentException("Il progetto non è ancora salvato. Riprova il salvataggio prima di continuare.");
        try{if(rename==null)store.save(projectId,candidate);else store.saveWithDrafts(projectId,candidate,(node,text)->rename.rewriteDraft(project,node,text,reverse));}
        catch(java.io.IOException e){throw new IllegalArgumentException("Non riesco a salvare la modifica. Il progetto e le bozze precedenti sono conservati: riprova quando c’è spazio disponibile.",e);}
    }
    private boolean saveNow(){handler.removeCallbacks(saveTask);if(store==null||project==null||projectId==null)return true;if(!dirty)return true;try{store.save(projectId,project);prefs.edit().putString("current",projectId).apply();dirty=false;saveFailed=false;refreshHeader();return true;}catch(Exception e){dirty=true;saveFailed=true;refreshHeader();toast("Non riesco a salvare: "+e.getMessage());return false;}}
    private boolean switchDocument(Document d,String id){return switchDocument(d,id,id==null);}
    private boolean switchDocument(Document d,String id,boolean arrangeNew){
        if(!saveNow())return false;
        final FlowStore.EditorDocument opened;
        try{opened=store.openEditor(id,d);}
        catch(Exception ex){message("Non riesco ad aprire il progetto",ex.getMessage());return false;}
        if(opened.copied)copyEditorView(id,opened.id);
        openGuard.cancel();if(runVisible)closeRun();documentRevision++;validationRequest++;
        stopRun();setRunner(null);if(activeSheet!=null)activeSheet.dismiss();
        cancelNavigationViewportRestore();navigationHistory.clear();project=opened.document;document=project;activeFunctionId=null;projectId=opened.id;mission=null;
        autoArrange=id==null?arrangeNew:prefs.getBoolean("auto_"+projectId,false);practiceLessonId="";
        prefs.edit().putBoolean("auto_"+projectId,autoArrange).remove("graph_"+projectId).apply();
        if(autoArrange)FlowAutoLayout.apply(document);
        undo.clear();redo.clear();dirty=true;selectedNodeId=null;canvas.setSelectionMode(false);canvas.setDocument(document,true);
        if(hubVisible)buildChrome();openingSpacingAdjusted=adaptOpeningText();saveNow();refreshHeader();updateArrangeButton();refreshPracticeBar();
        canvas.post(()->{if(!hubVisible)focusReadingStart();});
        showHint(openingSpacingAdjusted?"Spaziatura adattata al testo · Annulla ripristina le posizioni originali":opened.copied?"Progetto già aperto: lavori su una copia dell’ultimo salvataggio.":"Tocca un blocco · usa il + per continuare");
        return true;
    }
    @Override public void onSelectionChanged(){
        if(hubVisible||selectionBar==null)return;Set<String> selected=canvas.getSelectedIds();
        boolean connecting=canvas.isConnecting()&&connectionSource!=null;
        Node n=selected.size()==1?document.find(selected.iterator().next()):null;selectedNodeId=n==null?null:n.id;
        boolean newlyShown=selectionBar.getVisibility()!=View.VISIBLE;selectionBar.setVisibility(!runVisible&&(connecting||!selected.isEmpty())?View.VISIBLE:View.GONE);dock.setVisibility(!runVisible&&!compactEditorActions&&selectionBar.getVisibility()!=View.VISIBLE?View.VISIBLE:View.GONE);if(newlyShown&&selectionBar.getVisibility()==View.VISIBLE)Ui.enter(selectionBar,0);
        String selectionContext=connecting?"Collega: "+branchName(document.find(connectionSource),connectionBranch):n==null?selected.size()+" blocchi selezionati":FlowModel.kindName(n.kind)+" selezionato";
        selectionTitle.setText(compactHeight()?(connecting?selectionContext:n==null?selected.size()+" blocchi":FlowModel.kindName(n.kind)):selectionContext);selectionTitle.setContentDescription(selectionContext);selectionTitle.setTooltipText(selectionContext);
        selectionEdit.setLabel(connecting?"Destinazioni":"Modifica");selectionEdit.setVisibility(connecting||n!=null?View.VISIBLE:View.GONE);
        selectionConnect.setVisibility(!connecting&&n!=null&&n.kind!=FlowModel.END&&n.kind!=FlowModel.NOTE&&inlineConnectionFits()?View.VISIBLE:View.GONE);
        selectionAdd.setVisibility(!connecting&&n!=null&&n.kind!=FlowModel.END&&n.kind!=FlowModel.NOTE&&n.kind!=FlowModel.DECISION?View.VISIBLE:View.GONE);
        selectionMore.setVisibility(connecting?View.GONE:View.VISIBLE);selectionClose.setContentDescription(connecting?"Annulla collegamento":"Deseleziona");updateToolState();updateRunKeyboardLayout();
    }
    @Override public void onEditRequested(String id){editNode(id);}
    @Override public boolean onSelectionMenuRequested(){if(hubVisible||runVisible||canvas.isInteracting()||canvas.getSelectedIds().isEmpty())return false;showSelectionActions();return true;}
    @Override public void onAddRequested(float x,float y,String source,String branch){showAdd(x,y,source,branch);}
    @Override public void onConnectionStateChanged(String source,String branch){
        connectionSource=source;connectionBranch=branch;
        if(source!=null)showHint("Tocca il blocco di arrivo · × per annullare");
        else showHint(canvas.isSelectionMode()?"Trascina un riquadro o tocca i blocchi.":"Trascina per spostare · pizzica per lo zoom");
        onSelectionChanged();
    }
    @Override public void onHint(String text){showHint(text);}
    private String branchName(Node n,String branch){if(n!=null&&n.kind==FlowModel.DECISION)return "true".equals(branch)?n.trueLabel:n.falseLabel;return "Continua";}
    private String branchChoiceName(Node n,String branch){return n!=null&&n.kind==FlowModel.DECISION?"Ramo "+branchName(n,branch):branchName(n,branch);}
    private int branchColor(Node n,String branch){return n!=null&&n.kind==FlowModel.DECISION?Ui.parseColor("true".equals(branch)?n.trueColor:n.falseColor,Ui.ACCENT):Ui.ACCENT;}
    private String nodeName(Node n){if(n==null)return "Blocco non disponibile";String text=n.text==null?"":n.text.trim().replaceAll("\\s+"," ");return text.isEmpty()?FlowModel.kindName(n.kind):text.length()>160?text.substring(0,157)+"…":text;}
    private String branchDescription(Node n,String branch){return n.kind==FlowModel.DECISION?("true".equals(branch)?"Quando la condizione è vera":"Quando la condizione è falsa"):"Il prossimo passaggio";}
    private boolean validSource(Node n){return !destroyed&&!hubVisible&&!runVisible&&n!=null&&document.find(n.id)==n&&n.kind!=FlowModel.END&&n.kind!=FlowModel.NOTE;}
    private float[] pointAfter(Node n,String branch){float offset=n.kind==FlowModel.DECISION?("true".equals(branch)?-180:180):0;return new float[]{n.x+FlowModel.width(n)/2+offset,n.y+FlowModel.height(n)+132};}
    private void chooseBranch(Node n,String subtitle,java.util.function.Consumer<String> chosen){
        if(!validSource(n))return;if(n.kind!=FlowModel.DECISION){chosen.accept("next");return;}
        final Document expected=document;Ui.Sheet s=sheet("Quale uscita?","Ogni ramo ha il proprio percorso.");s.paragraph(subtitle);
        for(String branch:new String[]{"true","false"}){Edge edge=document.outgoing(n.id,branch);String detail=branchDescription(n,branch)+(edge==null?" · Ancora libera":"\nArrivo: "+nodeName(document.find(edge.to)));s.choice(branchChoiceName(n,branch),detail,branchColor(n,branch),()->{s.dismiss();if(document==expected&&validSource(n))chosen.accept(branch);}).setTag(branch);}
        s.show();
    }
    @Override public void onAddAfterRequested(String id){Node n=document.find(id);if(!validSource(n))return;chooseBranch(n,"Scegli il ramo a cui aggiungere un passaggio.",branch->{float[] p=pointAfter(n,branch);showAdd(p[0],p[1],n.id,branch);});}
    @Override public void onAddAfterRequested(String id,String branch){Node n=document.find(id);if(!validSource(n))return;float[] p=pointAfter(n,branch);showAdd(p[0],p[1],id,branch);}
    @Override public void onConnectRequested(String id){
        Node n=document.find(id);if(!validSource(n))return;final Document expected=document;Ui.Sheet s=sheet("Collegamenti","Gestisci le uscite del blocco.");s.paragraph("Da: "+nodeName(n));
        for(String branch:n.kind==FlowModel.DECISION?new String[]{"true","false"}:new String[]{"next"}){
            Edge edge=document.outgoing(id,branch);String detail=branchDescription(n,branch)+"\n"+(edge==null?"Nessun arrivo · Tocca per collegare":"Arrivo: "+nodeName(document.find(edge.to))+"\nTocca per cambiare arrivo");
            s.choice(branchChoiceName(n,branch),detail,branchColor(n,branch),()->{s.dismiss();if(document==expected&&validSource(n))showDestinationPicker(id,branch);}).setTag(branch);
            if(edge!=null){Ui.Button unlink=new Ui.Button(this,"Scollega uscita","link",0);unlink.setContentDescription("Scollega uscita "+branchName(n,branch)+" da "+nodeName(document.find(edge.to)));unlink.setOnClickListener(v->{s.dismiss();if(document==expected)removeConnection(edge);});s.body.addView(unlink,new LinearLayout.LayoutParams(-1,dp(48)));Ui.gap(s.body,10);}
        }
        s.paragraph("Per creare un nuovo passaggio, usa il + del blocco. Puoi toccare una freccia per modificarla.");s.show();
    }
    private void showDestinationPicker(String source,String branch){
        Node n=document.find(source);if(!validSource(n))return;final Document expected=document;canvas.cancelConnection();
        Ui.Sheet s=sheet("Scegli l’arrivo","Scegli un blocco già presente.");s.paragraph("Da: "+nodeName(n)+"\nUscita: "+branchName(n,branch));
        s.action("Scegli sul foglio","center",2,()->{s.dismiss();if(document==expected&&validSource(n))canvas.beginConnection(source,branch);});
        ArrayList<Node> candidates=new ArrayList<>();for(Node target:document.nodes)if(target.kind!=FlowModel.START&&target.kind!=FlowModel.NOTE)candidates.add(target);
        if(candidates.isEmpty()){s.paragraph("Non hai ancora altri blocchi da collegare. Aggiungi il prossimo passaggio.");s.primary("Aggiungi un passaggio","add",()->{s.dismiss();if(document==expected&&validSource(n)){float[] p=pointAfter(n,branch);showAdd(p[0],p[1],source,branch);}});s.show();return;}
        EditText search=Ui.edit(this,"","Cerca un blocco",false);s.body.addView(search,new LinearLayout.LayoutParams(-1,-2));Ui.gap(s.body,10);
        ArrayList<Node> filtered=new ArrayList<>(candidates);ListView list=new ListView(this);list.setDivider(new android.graphics.drawable.ColorDrawable(Color.TRANSPARENT));list.setDividerHeight(dp(8));list.setVerticalScrollBarEnabled(false);
        BaseAdapter adapter=new BaseAdapter(){public int getCount(){return filtered.size();}public Object getItem(int p){return filtered.get(p);}public long getItemId(int p){return p;}
            public View getView(int p,View recycled,ViewGroup parent){Node target=filtered.get(p);Ui.Choice row=recycled instanceof Ui.Choice?(Ui.Choice)recycled:new Ui.Choice(MainActivity.this);Edge current=document.outgoing(source,branch);row.bind(nodeName(target),FlowModel.kindName(target.kind)+(target==n?" · Questo blocco":"")+(current!=null&&current.to.equals(target.id)?" · Arrivo attuale":""),kindColor(target.kind));row.setTag(target.id);row.setOnClickListener(v->{s.dismiss();if(document==expected&&validSource(n))requestConnection(source,branch,target.id,target.x,target.y);});return row;}}
        ;list.setAdapter(adapter);list.setOnTouchListener((v,e)->{v.getParent().requestDisallowInterceptTouchEvent(e.getActionMasked()!=android.view.MotionEvent.ACTION_UP&&e.getActionMasked()!=android.view.MotionEvent.ACTION_CANCEL);return false;});
        s.body.addView(list,new LinearLayout.LayoutParams(-1,Math.min(dp(340),Math.max(dp(96),candidates.size()*dp(96)))));TextView empty=Ui.note(this,"Nessun blocco corrisponde alla ricerca.");empty.setVisibility(View.GONE);s.body.addView(empty);
        search.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence t,int a,int c,int next){}public void afterTextChanged(Editable e){}public void onTextChanged(CharSequence value,int start,int before,int count){String q=value.toString().trim().toLowerCase(Locale.ITALIAN);filtered.clear();for(Node target:candidates)if((target.text+" "+FlowModel.kindName(target.kind)).toLowerCase(Locale.ITALIAN).contains(q))filtered.add(target);adapter.notifyDataSetChanged();empty.setVisibility(filtered.isEmpty()?View.VISIBLE:View.GONE);list.setVisibility(filtered.isEmpty()?View.GONE:View.VISIBLE);}});
        s.show();
    }
    @Override public void onConnectionDropRequested(String source,String target,float x,float y){Node n=document.find(source);if(!validSource(n))return;chooseBranch(n,target==null?"Scegli il ramo a cui aggiungere un passaggio.":"Arrivo: "+nodeName(document.find(target)),branch->{if(target==null)showAdd(x,y,source,branch);else requestConnection(source,branch,target,x,y);});}
    @Override public boolean onConnectionTargetRequested(String source,String branch,String target,float x,float y){if(target==null){showHint("Tocca un blocco di arrivo o apri Destinazioni.");return true;}requestConnection(source,branch,target,x,y);return true;}
    @Override public void onConnectionDropRequested(String source,String branch,String target,float x,float y){
        Node n=document.find(source);if(!validSource(n))return;
        if(target==null){showAdd(x,y,source,branch);return;}
        Edge old=document.outgoing(source,branch);if(old==null||!old.to.equals(target))onManualConnectionChange();canvas.completeConnectionDrop(source,branch,target,x,y);
    }
    private void requestConnection(String source,String branch,String target,float x,float y){
        Node from=document.find(source),to=document.find(target);if(!validSource(from)||to==null)return;
        if(to.kind==FlowModel.NOTE){toast("Le note non ricevono collegamenti.");return;}
        if(to.kind==FlowModel.START){toast("Inizio non può ricevere collegamenti.");return;}
        Edge old=document.outgoing(source,branch);if(old==null||old.to.equals(target)){canvas.completeConnectionDrop(source,branch,target,x,y);return;}
        canvas.cancelConnection();final Document expected=document;Ui.Sheet s=sheet("Cambiare l’arrivo?","Questa uscita è già collegata.");
        s.paragraph("Da: "+nodeName(from)+"\nUscita: "+branchName(from,branch)+"\nArrivo attuale: "+nodeName(document.find(old.to))+"\nNuovo arrivo: "+nodeName(to));
        s.primary("Cambia collegamento","link",()->{s.dismiss();if(document==expected&&validSource(from)&&document.find(target)==to)canvas.completeConnectionDrop(source,branch,target,x,y);});
        s.action("Tieni il collegamento","close",0,()->{s.dismiss();canvas.cancelConnection();});s.show();
    }
    @Override public void onEdgeEditRequested(String id){
        if(hubVisible||runVisible)return;Edge edge=null;for(Edge e:document.edges)if(e.id.equals(id)){edge=e;break;}if(edge==null)return;
        final Edge selected=edge;final Document expected=document;Node n=document.find(edge.from);if(n==null)return;canvas.select(n.id);
        Ui.Sheet s=sheet("Collegamento","Modifica questa freccia.");s.paragraph("Da: "+nodeName(n)+"\nUscita: "+branchName(n,edge.branch)+"\nA: "+nodeName(document.find(edge.to)));
        if(validSource(n))s.primary("Cambia arrivo","link",()->{s.dismiss();if(document==expected)showDestinationPicker(selected.from,selected.branch);});
        else s.paragraph("Fine chiude il percorso. Rimuovi questa uscita per correggere il diagramma.");
        s.action("Scollega","link",3,()->{s.dismiss();if(document==expected)removeConnection(selected);});s.show();
    }
    private static FlowCanvasView.InsertionChoice answerOnce(FlowCanvasView.InsertionChoice choice){
        final boolean[] answered={false};return branch->{if(answered[0])return;answered[0]=true;choice.choose(branch);};
    }
    @Override public void onNodeInsertionRequested(String nodeId,String edgeId,FlowCanvasView.InsertionChoice choice){
        final FlowCanvasView.InsertionChoice answer=answerOnce(choice);
        Node node=document.find(nodeId);Edge edge=null;for(Edge e:document.edges)if(e.id.equals(edgeId)){edge=e;break;}if(node==null||node.kind!=FlowModel.DECISION||edge==null||runVisible||hubVisible){answer.choose(null);return;}
        Ui.Sheet s=sheet("Quale ramo prosegue?","L’altro ramo della decisione resta libero.");s.paragraph("Il percorso continua verso: "+nodeName(document.find(edge.to)));
        for(String branch:new String[]{"true","false"})s.choice(branchChoiceName(node,branch),"Continua verso il blocco successivo",branchColor(node,branch),()->{answer.choose(branch);s.dismiss();});
        s.choice("Nessuno","Entrambi i rami restano liberi; il blocco successivo resta sul foglio",Ui.muted(),()->{answer.choose("none");s.dismiss();});s.action("Annulla inserimento","close",0,()->{answer.choose(null);s.dismiss();});s.dialog.setOnDismissListener(d->{answer.choose("skip");if(activeSheet==s)activeSheet=null;});s.show();
    }
    @Override public void onPaletteInsertionRequested(Node candidate,String edgeId,FlowCanvasView.InsertionChoice choice){
        final FlowCanvasView.InsertionChoice answer=answerOnce(choice);
        Node node=candidate;Edge edge=null;for(Edge e:document.edges)if(e.id.equals(edgeId)){edge=e;break;}if(node==null||node.kind!=FlowModel.DECISION||edge==null||runVisible||hubVisible){answer.choose(null);return;}
        Ui.Sheet s=sheet("Quale ramo prosegue?","L’altro ramo della decisione resta libero.");s.paragraph("Il percorso continua verso: "+nodeName(document.find(edge.to)));
        for(String branch:new String[]{"true","false"})s.choice(branchChoiceName(node,branch),"Continua verso il blocco successivo",branchColor(node,branch),()->{answer.choose(branch);s.dismiss();});
        s.choice("Nessuno","Entrambi i rami restano liberi; il blocco successivo resta sul foglio",Ui.muted(),()->{answer.choose("none");s.dismiss();});s.action("Annulla inserimento","close",0,()->{answer.choose(null);s.dismiss();});s.dialog.setOnDismissListener(d->{answer.choose("skip");if(activeSheet==s)activeSheet=null;});s.show();
    }
    @Override public void onIoPaletteRequested(Node node,java.util.function.IntConsumer choice){final Document expected=document;choice.accept(FlowModel.INPUT);if(document.find(node.id)==node)canvas.post(()->{if(document==expected&&!hubVisible&&document.find(node.id)==node)editNode(node.id);});}
    private void removeConnection(Edge edge){if(!document.edges.contains(edge)||hubVisible||runVisible)return;onBeforeChange();document.edges.remove(edge);canvas.cancelConnection();changed("Collegamento rimosso");canvas.select(edge.from);showHint("Freccia rimossa · Annulla per ripristinarla");}
    private void showAddForContext(){
        if(runVisible||canvas.isInteracting())return;Node from=document.find(selectedNodeId);
        if(!validSource(from)&&canvas.getSelectedIds().isEmpty()){from=null;for(Node n:document.nodes)if(n.kind==FlowModel.START){if(from!=null){from=null;break;}from=n;}}
        if(validSource(from)){onAddAfterRequested(from.id);return;}
        float[] center=canvas.getViewportCenter();showAdd(center[0],center[1],null,null);
    }
    private void showAdd(float x,float y,String source,String branch){showAdd(x,y,source,branch,false);}
    private void showAdd(float x,float y,String source,String branch,boolean files){showAdd(x,y,source,branch,files?1:0);}
    private void showAdd(float x,float y,String source,String branch,int group){boolean files=group==1,turtle=group==2;
        Node from=document.find(source);if(source!=null&&!validSource(from))return;Edge continuation=source==null?null:document.outgoing(source,branch);
        String subtitle=source==null?"Blocco libero: verrà aggiunto senza collegamenti. Puoi collegarlo dopo.":"Dopo: "+nodeName(from)+(from.kind==FlowModel.DECISION?" · "+branchName(from,branch):"")+(continuation==null?"":"\nIl flusso continuerà verso "+nodeName(document.find(continuation.to))+".");
        Ui.Sheet s=sheet(group==5?"Annotazioni":group==4?"Debug":group==3?"Variabili e funzioni":turtle?"Disegno · tartaruga":files?"File · leggi e scrivi":source==null?"Aggiungi un blocco":"Aggiungi un passaggio",source==null?"Tocco: crea un blocco libero. Trascina su una freccia per inserirlo nel percorso.":"Tocco: inserisce un passaggio già collegato nel percorso scelto.");s.paragraph(subtitle);if(source!=null)s.action("Crea invece un blocco libero","add",0,()->{s.dismiss();float[] center=canvas.getViewportCenter();showAdd(center[0],center[1],null,null,group);});
        int[] kinds=FlowWorkspaceLayout.kinds(workspaceMode(),group,FlowModel.separateIo);
        LinearLayout row=null;int column=0;String category=null;
        for(int i=0;i<kinds.length;i++){
            final int kind=kinds[i];if(source!=null&&kind==FlowModel.START)continue;
            String nextCategory=FlowFileBlocks.isFile(kind)?"FILE · LEGGI E SCRIVI":FlowTurtle.isTurtle(kind)?"DISEGNO · TARTARUGA":kind==FlowModel.PAUSE?"DEBUG":kind==FlowModel.DECLARE||kind==FlowModel.CALL?"VARIABILI E FUNZIONI":kind==FlowModel.NOTE?"ANNOTAZIONI":"BLOCCHI DI BASE";
            if(!nextCategory.equals(category)){if(column%2==1&&row!=null)row.addView(new View(this),Ui.weight(this,1));column=0;category=nextCategory;s.section(category);}
            if(column%2==0){row=Ui.row(this);row.setGravity(Gravity.TOP);s.body.addView(row,new LinearLayout.LayoutParams(-1,-2));if(column>0)((LinearLayout.LayoutParams)row.getLayoutParams()).topMargin=dp(8);}
            LinearLayout tile=Ui.column(this);Ui.pad(tile,12,12,12,12);tile.setMinimumHeight(dp(106));
            android.graphics.drawable.GradientDrawable fill=Ui.round(Ui.bg(),19,this);fill.setStroke(dp(1),Ui.line());
            tile.setBackground(new android.graphics.drawable.RippleDrawable(android.content.res.ColorStateList.valueOf(Ui.dark?0x335B8DEF:0x225B8DEF),fill,null));
            TextView shape=Ui.text(this,"",15,true);shape.setCompoundDrawablesWithIntrinsicBounds(new Ui.Icon(this,FlowWorkspaceLayout.icon(kind),kindColor(kind),25),null,null,null);tile.addView(shape,Ui.lp(this,-1,27));
            TextView title=Ui.text(this,kind==FlowModel.INPUT&&!FlowModel.separateIo?"Input / Output":FlowModel.kindName(kind),15,true);Ui.pad(title,0,8,0,0);tile.addView(title);
            TextView description=Ui.text(this,FlowWorkspaceLayout.description(kind),12,false);description.setTextColor(Ui.muted());Ui.pad(description,0,5,0,0);tile.addView(description);
            tile.setContentDescription("Aggiungi "+(kind==FlowModel.INPUT&&!FlowModel.separateIo?"Input / Output":FlowModel.kindName(kind)));tile.setFocusable(true);
            tile.setOnClickListener(v->addNode(kind,x,y,source,branch,s));canvas.wirePalette(tile,kind,null,s::dismiss,kind==FlowModel.INPUT&&!FlowModel.separateIo);Ui.press(tile);Ui.enter(tile,column*35);row.addView(tile,Ui.weight(this,-2));
            if(column%2==0)row.addView(new View(this),Ui.lp(this,8,1));column++;
        }
        if(column%2==1&&row!=null)row.addView(new View(this),Ui.weight(this,1));
        final Document expected=document;
        if(group!=0)s.action("Blocchi di base","back",0,()->{s.dismiss();if(document==expected&&!hubVisible&&!runVisible)showAdd(x,y,source,branch,0);});
        if(group==0){
            s.section("STRUTTURE E DATI");
            final Document loopOwner=document;s.action("Ciclo guidato · Per, Mentre, Ripeti","undo",2,()->{s.dismiss();canvas.cancelConnection();if(document==loopOwner&&!hubVisible&&!runVisible)showLoopEditorAt(continuation==null?null:continuation.id);});
            s.action("Variabili e funzioni","call",0,()->{s.dismiss();if(document==expected&&!hubVisible&&!runVisible)showAdd(x,y,source,branch,3);});
            s.action("File · leggi e scrivi","folder",0,()->{s.dismiss();if(document==expected&&!hubVisible&&!runVisible)showAdd(x,y,source,branch,1);});
            s.action("Disegno · tartaruga","edit",0,()->{s.dismiss();if(document==expected&&!hubVisible&&!runVisible)showAdd(x,y,source,branch,2);});
            s.section("ANNOTAZIONI E DEBUG");
            if(source==null)s.action("Annotazioni · testo sul foglio","edit",0,()->{s.dismiss();if(document==expected&&!hubVisible&&!runVisible)showAdd(x,y,null,null,5);});
            s.action("Debug · pausa nel percorso","pause",0,()->{s.dismiss();if(document==expected&&!hubVisible&&!runVisible)showAdd(x,y,source,branch,4);});
        }
        if(continuation!=null)s.action("Scollega questo collegamento","link",3,()->{if(document==expected&&document.outgoing(source,branch)==continuation)removeConnection(continuation);s.dismiss();});
        s.dialog.setOnDismissListener(d->{canvas.cancelConnection();if(activeSheet==s)activeSheet=null;});s.show();
    }
    private void addNode(int kind,float x,float y,String source,String branch,Ui.Sheet s){
        if(project.totalNodes()>=FlowModel.MAX_NODES){toast("Massimo 500 blocchi per progetto.");return;}
        if(source!=null&&document.find(source)==null){s.dismiss();return;}
        Edge old=source==null?null:document.outgoing(source,branch);
        int extraEdges=source==null?0:old==null||kind!=FlowModel.END?1:0;
        if(kind!=FlowModel.DECISION&&project.totalEdges()+extraEdges>FlowModel.MAX_EDGES){toast("Massimo 1.500 collegamenti.");return;}
        final Document expected=document;
        if(old!=null&&kind==FlowModel.DECISION){s.dismiss();Ui.Sheet choice=sheet("Come continua il flusso?","Scegli il ramo della nuova decisione.");choice.paragraph("Stai inserendo una decisione prima di "+nodeName(document.find(old.to))+". Quale uscita prosegue verso quel blocco?");
            Node preview=new Node();preview.kind=FlowModel.DECISION;for(String next:new String[]{"true","false"})choice.choice(branchChoiceName(preview,next),branchDescription(preview,next),branchColor(preview,next),()->{if(document!=expected){choice.dismiss();return;}commitAddNode(kind,x,y,source,branch,next,choice);});choice.choice("Nessuno","Entrambi i rami restano liberi; il blocco successivo resta sul foglio",Ui.muted(),()->{if(document==expected)commitAddNode(kind,x,y,source,branch,"none",choice);else choice.dismiss();});choice.action("Annulla inserimento","close",0,choice::dismiss);choice.paragraph("L’altra uscita resta libera: collegala dopo aver scritto la condizione.");choice.show();return;}
        if(old!=null&&kind==FlowModel.END){s.dismiss();Ui.Sheet choice=sheet("Terminare qui il percorso?","Fine chiude questa uscita.");choice.paragraph("Il collegamento verso "+nodeName(document.find(old.to))+" verrà interrotto. I blocchi successivi restano sul foglio e potrai ricollegarli.");choice.primary("Aggiungi Fine","end",()->{if(document==expected)commitAddNode(kind,x,y,source,branch,null,choice);else choice.dismiss();});choice.action("Scegli un altro blocco","back",0,()->{choice.dismiss();if(document==expected)showAdd(x,y,source,branch);});choice.show();return;}
        commitAddNode(kind,x,y,source,branch,"next",s);
    }
    private void commitAddNode(int kind,float x,float y,String source,String branch,String continuationBranch,Ui.Sheet s){
        if(project.totalNodes()>=FlowModel.MAX_NODES||source!=null&&!validSource(document.find(source))){s.dismiss();return;}
        Edge old=source==null?null:document.outgoing(source,branch);int extra=source==null?0:old==null?1:kind!=FlowModel.END&&!"none".equals(continuationBranch)?1:0;
        if(project.totalEdges()+extra>FlowModel.MAX_EDGES){toast("Massimo 1.500 collegamenti.");return;}
        Map<String,float[]> placementBefore=positions();onManualNodeMove();onBeforeChange();String oldTarget=old==null?null:old.to;Node added=new Node(kind,defaultText(kind),Math.max(-100000,Math.min(100000,x-FlowModel.width(kind)/2)),Math.max(-100000,Math.min(100000,y-FlowModel.height(kind)/2)));
        if(canvas.isSnapToGrid()){added.x=Math.max(-100000,Math.min(100000,FlowGridSnap.round(added.x)));added.y=Math.max(-100000,Math.min(100000,FlowGridSnap.round(added.y)));}
        if(kind==FlowModel.INPUT)added.prompt="Inserisci un valore";document.nodes.add(added);
        if(source!=null){if(old==null)document.edges.add(new Edge(source,added.id,branch));else{String next=old.to;old.to=added.id;if(kind!=FlowModel.END&&!"none".equals(continuationBranch))document.edges.add(new Edge(added.id,next,kind==FlowModel.DECISION?continuationBranch:"next"));}}
        FlowPlacement.reflow(document,added.id,source,oldTarget,source!=null);
        canvas.cancelConnection();canvas.select(added.id);changed("Blocco aggiunto");placementBefore.put(added.id,new float[]{added.x,added.y+16});canvas.animateLayout(placementBefore,positions(),280);s.dismiss();
        canvas.post(()->{if(!hubVisible&&document.find(added.id)!=null)canvas.focusNodeAnimated(added.id);});
        if(kind!=FlowModel.START&&kind!=FlowModel.END)editNode(added.id);
    }
    private String defaultText(int k){if(k==FlowModel.NOTE)return "Scrivi una nota";if(k==FlowModel.PAUSE)return "Pausa";if(FlowTurtle.isTurtle(k))return FlowTurtle.defaultText(k);if(FlowFileBlocks.isFile(k))return FlowFileBlocks.defaultText(k);return k==FlowModel.START?"Inizio":k==FlowModel.END?"Fine":k==FlowModel.INPUT?"numero":k==FlowModel.PROCESS?"risultato = 0":k==FlowModel.DECISION?"numero > 0":k==FlowModel.DECLARE?"numero: INTERO":k==FlowModel.CALL?"nomeFunzione()":"\"Ciao!\"";}
    private int kindColor(int kind){switch(kind){case FlowModel.NOTE:return Ui.muted();case FlowModel.PAUSE:return Ui.danger();case FlowModel.START:return Ui.dark?0xFF73D5AE:0xFF238768;case FlowModel.END:return Ui.dark?0xFFAAB3C3:0xFF737E91;case FlowModel.INPUT:return Ui.dark?0xFF75BEEC:0xFF388DBD;case FlowModel.OUTPUT:return Ui.dark?0xFFBC99FF:0xFF814BCE;case FlowModel.DECISION:return Ui.dark?0xFFE9C06E:0xFFAD7B24;default:return Ui.dark?0xFF94B4FF:0xFF5479C6;}}
    private void fieldLabel(LinearLayout parent,String label){TextView t=Ui.text(this,label,12,true);t.setTextColor(Ui.muted());Ui.pad(t,2,10,0,8);parent.addView(t);}
    private void editNode(String id){if(runVisible)closeRun();Node node=document.find(id);if(node==null)return;if(node.kind==FlowModel.NOTE){editAnnotation(node);return;}if(node.kind==FlowModel.PAUSE){editBreakpoint(id);return;}Ui.Sheet s=sheet(node.kind==FlowModel.INPUT||node.kind==FlowModel.OUTPUT?"Input / Output":FlowModel.kindName(node.kind),FlowModel.isIo(node.kind)?FlowModel.IO_HINT:node.kind==FlowModel.DECISION?"Scrivi la condizione e personalizza le due uscite.":"Modifica il contenuto di questo passaggio.");visibleEditorId=id;FlowDraftStore.Draft recovered=drafts.get(projectId,id);
        String field=FlowTurtle.isTurtle(node.kind)?"Testo dell’istruzione tartaruga":FlowFileBlocks.isFile(node.kind)?"Testo dell’istruzione file":FlowModel.isIo(node.kind)?"Istruzione: leggi o stampa":node.kind==FlowModel.DECISION?"Condizione":node.kind==FlowModel.PROCESS?"Operazione":node.kind==FlowModel.DECLARE?"Testo della dichiarazione":node.kind==FlowModel.CALL?"Chiamata: nomeFunzione(argomenti)":node.kind==FlowModel.OUTPUT?"Valore o testo da mostrare":"Etichetta";fieldLabel(s.body,field);EditText expression=Ui.edit(this,recovered!=null?recovered.text:FlowModel.editorText(node),field,node.kind==FlowModel.PROCESS||node.kind==FlowModel.OUTPUT);expression.setFilters(new InputFilter[]{new InputFilter.LengthFilter(8192)});s.body.addView(expression,new LinearLayout.LayoutParams(-1,-2));if(FlowModel.isIo(node.kind))new FlowOutputEditor(this,s.body,expression,node);if(node.kind==FlowModel.DECLARE)new FlowDeclarationEditor(this,s.body,expression);else if(FlowTurtle.isTurtle(node.kind))new FlowTurtleEditor(this,s.body,expression,node,project);else if(FlowFileBlocks.isFile(node.kind))new FlowFileEditor(this,s.body,expression,node,project);else if(node.kind!=FlowModel.START&&node.kind!=FlowModel.END){Ui.Button functions=new Ui.Button(this,"Variabili e funzioni",null,0);functions.setOnClickListener(v->FlowFunctionLibrary.show(this,expression,project,node.kind==FlowModel.CALL,activeFunctionId,id));s.body.addView(functions,new LinearLayout.LayoutParams(-1,dp(48)));}FlowBlockPreview preview=new FlowBlockPreview(this,node);s.body.addView(preview,new LinearLayout.LayoutParams(-1,-2));preview.text(expression.getText().toString());
        fieldLabel(s.body,"Commento (facoltativo, non viene eseguito)");EditText comment=Ui.edit(this,recovered!=null?recovered.comment:node.comment,"Spiega questo passaggio",true);comment.setFilters(new InputFilter[]{new InputFilter.LengthFilter(2048)});comment.setContentDescription("Commento del blocco");s.body.addView(comment);preview.text(expression.getText().toString(),comment.getText().toString());
        EditText prompt=null;if(FlowModel.isIo(node.kind)){fieldLabel(s.body,"Domanda per LEGGI (facoltativa)");prompt=Ui.edit(this,recovered!=null?recovered.prompt:node.prompt,"Domanda",true);prompt.setFilters(new InputFilter[]{new InputFilter.LengthFilter(1024)});s.body.addView(prompt);}
        if(node.kind==FlowModel.PROCESS||node.kind==FlowModel.OUTPUT||node.kind==FlowModel.DECISION){Ui.gap(s.body,9);s.paragraph(node.kind==FlowModel.PROCESS?"Esempio: totale = prezzo * quantità\nUna o più assegnazioni, una per riga.":node.kind==FlowModel.OUTPUT?FlowModel.IO_HINT:"Esempi: numero > 0   oppure   numero % 2 = 0");}
        final Runnable[] keepDraft={null};final String[] tColor={recovered!=null?recovered.trueColor:node.trueColor},fColor={recovered!=null?recovered.falseColor:node.falseColor};EditText tLabel=null,fLabel=null;
        if(node.kind==FlowModel.DECISION){tLabel=branchEditor(s.body,"Quando è vera",recovered!=null?recovered.trueLabel:node.trueLabel,tColor,()->{if(keepDraft[0]!=null)keepDraft[0].run();});fLabel=branchEditor(s.body,"Quando è falsa",recovered!=null?recovered.falseLabel:node.falseLabel,fColor,()->{if(keepDraft[0]!=null)keepDraft[0].run();});}
        final EditText p=prompt,tl=tLabel,fl=fLabel;final String editorProject=projectId;
        final View promptHeading=p==null?null:s.body.getChildAt(s.body.indexOfChild(p)-1);
        final Runnable updateIoFields=()->{if(p==null)return;int visibility=FlowModel.ioText(node,expression.getText().toString()).kind==FlowModel.INPUT?View.VISIBLE:View.GONE;p.setVisibility(visibility);promptHeading.setVisibility(visibility);};updateIoFields.run();
        java.util.function.BooleanSupplier hasChanges=()->!comment.getText().toString().equals(node.comment==null?"":node.comment)||FlowModel.editChanged(node,expression.getText().toString())||(p!=null&&!p.getText().toString().equals(node.prompt))||(tl!=null&&(!tl.getText().toString().equals(node.trueLabel)||!fl.getText().toString().equals(node.falseLabel)||!tColor[0].equalsIgnoreCase(node.trueColor)||!fColor[0].equalsIgnoreCase(node.falseColor)));
        Runnable stash=()->{if(!editorProject.equals(projectId)||document.find(id)!=node)return;if(!hasChanges.getAsBoolean()){clearDraftFor(id);return;}drafts.put(editorProject,id,new FlowDraftStore.Draft(expression.getText().toString(),p==null?node.prompt:p.getText().toString(),tl==null?node.trueLabel:tl.getText().toString(),fl==null?node.falseLabel:fl.getText().toString(),tColor[0],fColor[0],comment.getText().toString()));};keepDraft[0]=stash;
        TextWatcher watcher=new TextWatcher(){public void beforeTextChanged(CharSequence t,int a,int c,int n){}public void onTextChanged(CharSequence t,int a,int before,int count){stash.run();updateIoFields.run();preview.text(expression.getText().toString(),comment.getText().toString());canvas.previewText(id,expression.getText().toString());canvas.previewComment(id,comment.getText().toString());}public void afterTextChanged(Editable e){}};expression.addTextChangedListener(watcher);comment.addTextChangedListener(watcher);if(p!=null)p.addTextChangedListener(watcher);if(tl!=null){tl.addTextChangedListener(watcher);fl.addTextChangedListener(watcher);}stash.run();s.dialog.setOnDismissListener(d->{if(activeSheet!=s)return;stash.run();canvas.clearTextPreview(id);visibleEditorId=null;activeSheet=null;if(editorProject.equals(projectId)&&document.find(id)==node&&hasChanges.getAsBoolean())showHint("Bozza conservata: riapri il blocco per continuare.");});
        s.primary("Applica modifiche","check",()->{if(document.find(id)!=node){s.dismiss();return;}if(expression.getText().toString().trim().isEmpty()){expression.setError("Scrivi il contenuto del blocco.");return;}if(node.kind==FlowModel.DECLARE){try{FlowDeclaration declaration=FlowDeclaration.parse(expression.getText().toString());if(declaration.isArray())FlowEngine.checkExpression(declaration.sizeText,FlowFunctions.catalog(project));}catch(IllegalArgumentException ex){expression.setVisibility(View.VISIBLE);s.body.getChildAt(s.body.indexOfChild(expression)-1).setVisibility(View.VISIBLE);expression.setError(ex.getMessage());expression.requestFocus();return;}}if(FlowFileBlocks.isFile(node.kind)||FlowTurtle.isTurtle(node.kind)){try{FlowEngine.checkBlock(new Node(node.kind,expression.getText().toString(),0,0),FlowFunctions.catalog(project),null);}catch(IllegalArgumentException ex){expression.setVisibility(View.VISIBLE);s.body.getChildAt(s.body.indexOfChild(expression)-1).setVisibility(View.VISIBLE);expression.setError(ex.getMessage());expression.requestFocus();return;}}if(tl!=null&&(tl.getText().toString().trim().isEmpty()||fl.getText().toString().trim().isEmpty())){toast("Dai un nome a entrambe le uscite.");return;}if(!hasChanges.getAsBoolean()){clearDraftFor(id);s.dismiss();return;}onManualNodeMove();onBeforeChange();Map<String,float[]> sizeBefore=positions();node.previewText=null;node.previewComment=null;float oldWidth=FlowModel.width(node),oldHeight=FlowModel.height(node);FlowModel.applyText(node,expression.getText().toString());node.comment=comment.getText().length()==0?null:comment.getText().toString();if(p!=null)node.prompt=p.getText().toString();if(tl!=null){node.trueLabel=tl.getText().toString().trim();node.falseLabel=fl.getText().toString().trim();node.trueColor=tColor[0];node.falseColor=fColor[0];}if(oldWidth!=FlowModel.width(node)||oldHeight!=FlowModel.height(node))FlowPlacement.grow(document,id);changed("Blocco aggiornato");canvas.animateLayout(sizeBefore,positions(),320);s.dialog.setOnDismissListener(d->{canvas.clearTextPreview(id);if(activeSheet==s){visibleEditorId=null;activeSheet=null;}});clearDraftFor(id);s.dismiss();saveNow();});
        Ui.gap(s.body,8);Ui.Button discard=new Ui.Button(this,"Scarta questa modifica",null,0);discard.setOnClickListener(v->{s.dialog.setOnDismissListener(d->{canvas.clearTextPreview(id);if(activeSheet==s){visibleEditorId=null;activeSheet=null;}});clearDraftFor(id);s.dismiss();});s.body.addView(discard,new LinearLayout.LayoutParams(-1,dp(48)));s.show();
    }
    private void editAnnotation(final Node node){
        final String id=node.id,editorProject=projectId;final Document expected=document;
        Ui.Sheet s=sheet("Nota libera","Scrivi una spiegazione sul foglio. Non modifica l’esecuzione e non si collega alle frecce.");visibleEditorId=id;
        FlowDraftStore.Draft recovered=drafts.get(editorProject,id);fieldLabel(s.body,"Testo della nota");
        EditText text=Ui.edit(this,recovered==null?node.text:recovered.text,"Scrivi liberamente, anche su più righe",true);
        text.setTag("annotation-text");text.setContentDescription("Testo della nota libera");text.setFilters(new InputFilter[]{new InputFilter.LengthFilter(8192)});text.setMinLines(5);s.body.addView(text,new LinearLayout.LayoutParams(-1,-2));
        java.util.function.BooleanSupplier current=()->editorProject.equals(projectId)&&document==expected&&document.find(id)==node;
        java.util.function.BooleanSupplier modified=()->!text.getText().toString().equals(node.text==null?"":node.text);
        Runnable stash=()->{if(!current.getAsBoolean())return;if(!modified.getAsBoolean()){clearDraftFor(id);return;}drafts.put(editorProject,id,new FlowDraftStore.Draft(text.getText().toString(),node.prompt,node.trueLabel,node.falseLabel,node.trueColor,node.falseColor,node.comment));};
        text.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence t,int a,int c,int n){}public void afterTextChanged(Editable e){}public void onTextChanged(CharSequence t,int a,int b,int c){stash.run();if(current.getAsBoolean())canvas.previewText(id,t.toString());}});
        Runnable clear=()->{if(activeSheet==s){canvas.clearTextPreview(id);activeSheet=null;visibleEditorId=null;}};
        s.dialog.setOnDismissListener(d->{if(activeSheet==s){stash.run();clear.run();}});
        s.primary("Salva nota","check",()->{if(!current.getAsBoolean()){s.dismiss();return;}if(modified.getAsBoolean()){onBeforeChange();node.previewText=null;node.text=text.getText().toString();changed("Nota aggiornata");saveNow();}clearDraftFor(id);canvas.clearTextPreview(id);s.dialog.setOnDismissListener(d->clear.run());s.dismiss();});
        s.action("Scarta questa modifica","close",0,()->{if(current.getAsBoolean()){clearDraftFor(id);canvas.clearTextPreview(id);}s.dialog.setOnDismissListener(d->clear.run());s.dismiss();});
        stash.run();s.show();
    }
    private EditText branchEditor(LinearLayout body,String title,String value,String[] selectedColor,Runnable changed){
        Ui.gap(body,10);LinearLayout card=Ui.column(this);Ui.pad(card,12,12,12,10);Ui.card(card,Ui.bg(),20);
        LinearLayout heading=Ui.row(this);TextView labelTitle=Ui.text(this,title,12,true);heading.addView(labelTitle,Ui.weight(this,24));labelTitle.setGravity(Gravity.CENTER_VERTICAL);
        View currentColor=new View(this);Ui.card(currentColor,Ui.parseColor(selectedColor[0],Ui.ACCENT),8);heading.addView(currentColor,Ui.lp(this,16,16));card.addView(heading);Ui.gap(card,6);
        EditText label=Ui.edit(this,value,"Nome dell'uscita: "+title.toLowerCase(Locale.ROOT),false);label.setFilters(new InputFilter[]{new InputFilter.LengthFilter(60)});card.addView(label,new LinearLayout.LayoutParams(-1,-2));Ui.gap(card,6);
        int columns=getResources().getDisplayMetrics().widthPixels/getResources().getDisplayMetrics().density<352?3:6;
        ArrayList<TextView> swatches=new ArrayList<>();String[] names={"Verde","Rosa","Blu","Arancione","Viola","Turchese"};LinearLayout row=null;
        for(int i=0;i<COLORS.length;i++){
            if(i%columns==0){row=Ui.row(this);card.addView(row,new LinearLayout.LayoutParams(-1,dp(48)));}
            final String hex=COLORS[i];TextView sw=Ui.text(this,hex.equalsIgnoreCase(selectedColor[0])?"✓":"",20,true);sw.setGravity(Gravity.CENTER);sw.setTextColor(0xFF161616);sw.setSelected(hex.equalsIgnoreCase(selectedColor[0]));sw.setContentDescription(title+": colore "+names[i]);
            android.graphics.drawable.GradientDrawable fill=Ui.round(Color.parseColor(hex),13,this);sw.setBackground(new android.graphics.drawable.InsetDrawable(fill,dp(4),dp(5),dp(4),dp(5)));
            sw.setOnClickListener(v->{selectedColor[0]=hex;for(TextView other:swatches){other.setText("");other.setSelected(false);}sw.setText("✓");sw.setSelected(true);Ui.card(currentColor,Color.parseColor(hex),8);changed.run();});
            sw.setClickable(true);sw.setFocusable(true);row.addView(sw,Ui.weight(this,48));swatches.add(sw);
        }
        body.addView(card,new LinearLayout.LayoutParams(-1,-2));return label;
    }
    private void clearDraftFor(String id){drafts.remove(projectId,id);}
    private void showAppSettings(){
        Ui.Sheet s=sheet("Fatto per te","Aspetto e movimento, sempre sotto controllo.");rememberNavigation(s,"Preferenze");
        s.action("Aspetto: "+(Ui.dark?"scuro":"chiaro"),Ui.dark?"moon":"sun",0,()->{s.dismiss();showTheme();});
        s.action("Animazioni: "+(Ui.motion?"attive":"ridotte"),"spark",Ui.motion?2:0,()->{Ui.motion=!Ui.motion;prefs.edit().putBoolean("animations",Ui.motion).apply();canvas.setMotionEnabled(Ui.motion);if(!Ui.motion&&s.dialog.getWindow()!=null)s.dialog.getWindow().setWindowAnimations(0);s.dismiss();showAppSettings();});
        s.paragraph("Le animazioni seguono anche le preferenze di accessibilità del telefono.");
        s.action("File generati e recupero","save",0,()->{s.dismiss();fileUi.showRecovery();});
        s.action("Come usare Frociart","help",0,()->{s.dismiss();showGuide();});
        s.action("Autore e condizioni d’uso","help",0,()->{s.dismiss();showLicense();});
        s.action("Informazioni e novità","help",0,()->{s.dismiss();showAbout();});s.paragraph("Frociart "+appBuild()+"\nUn progetto di mamt104.\nCopyright © 2026 mamt104.\nI progetti funzionano offline e restano sul telefono.");s.show();
    }
    private void showAbout(){Ui.Sheet s=sheet("Informazioni e novità","Frociart "+appBuild());
        s.paragraph("Questa è la versione installata sul dispositivo. Gli aggiornamenti si installano soltanto quando li scegli tu, dal sito o dalla release ufficiale.");
        s.section("NOVITÀ DELLA BUILD 105");s.paragraph("Più spazio per il diagramma sul telefono e titolo leggibile con testo grande. Inserimento dopo selezione o Inizio, con Blocco libero esplicito. Etichetta input persistente, Torna alla posizione precedente, espressioni osservate e backup dei progetti salvati.");
        s.paragraph("Torna ripristina la vista precedente; Annulla ripristina una modifica del diagramma. I backup si creano e si ripristinano soltanto su tua richiesta.");s.action("Autore e condizioni d’uso","help",0,()->{s.dismiss();showLicense();});s.show();}
    private void showLicense(){try(InputStream in=getAssets().open("LICENSE.txt")){ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] buffer=new byte[2048];int n;while((n=in.read(buffer))!=-1)out.write(buffer,0,n);message("Frociart · mamt104",new String(out.toByteArray(),StandardCharsets.UTF_8));}catch(IOException ex){message("Frociart · mamt104","Copyright © 2026 mamt104. Tutti i diritti riservati. Le condizioni sono disponibili su github.com/mamt104/Frociart.");}}
    private void showLearningSources(FlowLearning.Lesson lesson){
        if(lesson==null)return;Ui.Sheet s=sheet("Capisci da dove viene",lesson.title);
        s.paragraph("Spiegazioni ed esercizi sono scritti per Frociart. I principi di questa lezione sono documentati nelle fonti qui sotto.");
        for(FlowLearning.Source source:lesson.sources){
            LinearLayout card=Ui.column(this);Ui.pad(card,14,14,14,14);Ui.card(card,Ui.bg(),18);TextView title=Ui.text(this,source.title,16,true);title.setTextColor(Ui.accentInk());card.addView(title);Ui.gap(card,7);TextView url=Ui.note(this,source.url);url.setTextSize(12);card.addView(url);card.setContentDescription("Apri fonte: "+source.title);card.setFocusable(true);card.setOnClickListener(v->openSource(source.url));Ui.press(card);s.body.addView(card);Ui.gap(s.body,10);
        }
        s.primary("Torna alla lezione","back",s::dismiss);s.show();
    }
    private void openSource(String address){Uri uri=Uri.parse(address);if(!"https".equals(uri.getScheme())&&!"http".equals(uri.getScheme()))return;try{startActivity(new Intent(Intent.ACTION_VIEW,uri));}catch(ActivityNotFoundException e){message("Fonte non disponibile","Non trovo un browser per aprire questa pagina:\n"+address);}}
    private void practiceLesson(FlowLearning.Lesson lesson){
        FlowPractice.Mission task=FlowPractice.forLesson(lesson);if(task==null||!saveNow())return;
        String id=prefs.getString("mission_project_"+task.id,"");Document next=null;
        if(id.equals(projectId)&&mission!=null&&mission.id.equals(task.id)){practiceLessonId=lesson.id;prefs.edit().putString("practice_"+id,lesson.id).apply();openEditor();showPracticeGoal();return;}
        if(!id.isEmpty())try{next=store.load(id);}catch(Exception ex){toast("Riparto da una nuova copia. Il progetto precedente resta nell'archivio.");}
        if(next==null){next=FlowPractice.seed(task);id=store.newId();prefs.edit().putBoolean("auto_"+id,true).putString("mission_"+id,task.id).apply();}
        prefs.edit().putString("practice_"+id,lesson.id).apply();if(!switchDocument(next,id))return;
        prefs.edit().putString("mission_project_"+task.id,id).apply();showPracticeGoal();
    }
    private void refreshPracticeBar(){
        if(practiceBar==null)return;practiceBar.setVisibility(mission==null||runVisible?View.GONE:View.VISIBLE);if(mission==null)return;
        practiceTitle.setText(mission.title);practiceTitle.setContentDescription("Obiettivo dell'esercizio: "+mission.title+". Tocca per le istruzioni.");
        practiceCheck.setLabel(checkingPractice?"Controllo…":"Controlla");practiceCheck.setEnabled(!checkingPractice);practiceCheck.setContentDescription("Controlla il flowchart dell'esercizio");
    }
    private void showPracticeGoal(){
        if(mission==null)return;FlowPractice.Mission task=mission;Ui.Sheet s=sheet(task.title,"Laboratorio della tappa · costruisci sul foglio");
        s.paragraph(task.goal);s.paragraph(task.example());s.paragraph(task.repair?"Il diagramma contiene un errore da correggere. Il tuo lavoro si salva automaticamente.":"Trovi le letture già collegate a Inizio e Fine. Aggiungi tu calcoli, scelte e risultati: il diagramma non è già risolto.");
        s.primary(task.repair?"Correggi il flowchart":"Costruisci il flowchart","brand",s::dismiss);
        s.action("Un suggerimento","spark",0,()->{s.dismiss();message("Come iniziare",task.hint);});s.paragraph("Usa Controlla per provare "+task.cases()+" casi. I tentativi non consumano punti. Ogni laboratorio assegna 20 XP una sola volta.");s.show();
    }
    private void checkPractice(){
        if(mission==null||checkingPractice||runVisible||!saveNow())return;
        final FlowPractice.Mission task=mission;final Document original=document,snapshot=document.copy();final long revision=documentRevision;final String id=projectId;
        checkingPractice=true;refreshPracticeBar();
        io.execute(()->{FlowPractice.Result result=null;try{result=FlowPractice.check(task,snapshot);}catch(RuntimeException ex){android.util.Log.e("Frociart","Controllo laboratorio non riuscito: "+ex.getClass().getSimpleName());}
            final FlowPractice.Result checked=result;handler.post(()->{
                checkingPractice=false;if(destroyed)return;refreshPracticeBar();
                if(hubVisible||document!=original||!projectId.equals(id)||documentRevision!=revision||mission!=task){showHint("Il diagramma è cambiato: premi Controlla per provare la versione attuale.");return;}
                if(checked==null){message("Controllo non riuscito","Il diagramma è rimasto intatto. Riprova; puoi anche usare Prova per seguirne l'esecuzione.");return;}
                if(activeSheet!=null||runVisible){showHint("Controllo concluso. Premi Controlla per rivederlo.");return;}
                boolean awarded=false;if(checked.success)try{awarded=study.completePractice(task.id);}catch(IllegalStateException ex){message("Risultato non salvato","I casi di prova sono superati, ma non riesco a conservare il progresso. Il flowchart è salvato: riprova il controllo.");return;}
                Ui.Sheet s=sheet(checked.title,checked.success?(awarded?"+20 XP · laboratorio completato":"Laboratorio già completato"):checked.passed+" di "+checked.total+" casi superati");s.paragraph(checked.detail);
                s.primary("Torna al foglio","back",()->{s.dismiss();if(checked.nodeId!=null&&document.find(checked.nodeId)!=null)canvas.focusNodeAnimated(checked.nodeId);});
                if(checked.success)s.action("Continua a imparare","learn",0,()->{s.dismiss();study.showPath();});else s.action("Rileggi l'obiettivo","help",0,()->{s.dismiss();showPracticeGoal();});s.show();
            });
        });
    }
    private void showFreeTip(){
        FlowLearning.Lesson chosen=FlowLearning.lesson(practiceLessonId);Node selected=document.find(selectedNodeId);
        if(chosen==null){String id=selected==null?"l04":selected.kind==FlowModel.DECISION?"l09":selected.kind==FlowModel.PROCESS?"l05":selected.kind==FlowModel.INPUT?"l02":selected.kind==FlowModel.OUTPUT?"l02":"l01";chosen=FlowLearning.lesson(id);}
        if(chosen==null)return;final FlowLearning.Lesson lesson=chosen;Ui.Sheet s=sheet("Un’idea da provare",lesson.title);
        s.body.addView(Ui.text(this,"Prima prevedi, poi esegui.",20,true));Ui.gap(s.body,10);s.paragraph(lesson.goal);s.paragraph(lesson.tip);if(lesson.practiceExample==null||lesson.practiceExample.isEmpty())s.paragraph(FlowLearning.PSEUDOCODE_NOTE);
        s.body.addView(Ui.text(this,"Mettilo alla prova",16,true));Ui.gap(s.body,8);s.paragraph(lesson.counterexample);
        s.action("Fonti di questo trucco","learn",0,()->{s.dismiss();showLearningSources(lesson);});
        s.action("Fai i 3 esercizi guidati","spark",2,()->{s.dismiss();study.startLesson(lesson.id);});
        s.primary("Torno al diagramma","check",s::dismiss);s.show();
    }
    private FlowCommandPalette commandsPanel;
    private String commandQuery="";
    private void addTaskCommand(List<FlowCommandSearch.Entry> result,String group,String label,String icon,int tone,Runnable action){boolean editing=!runVisible&&!canvas.isInteracting()&&!canvas.isConnecting();boolean enabled=(!group.equals("Modifica")&&!group.equals("Cicli")||editing||label.equals("Cerca nel progetto"))&&(!group.equals("Modifica")||(!label.equals("Annulla modifica")||!undo.isEmpty())&&(!label.equals("Ripeti modifica")||!redo.isEmpty())&&(!label.equals("Azioni sulla selezione")||!canvas.getSelectedIds().isEmpty()));if(label.equals("Esegui fino al blocco selezionato"))enabled&=canRunToSelected();if(label.equals("Torna alla posizione precedente"))enabled&=!navigationHistory.isEmpty();String path=group.equals("Cicli")?"Modifica › Cicli":group.equals("Linguaggi")?"Codice › Genera codice":group;result.add(new FlowCommandSearch.Entry(group+"."+result.size(),label,path,icon,tone,enabled,action));}
    private List<FlowCommandSearch.Entry> taskCommands(String group,Runnable close){
        List<FlowCommandSearch.Entry> result=new ArrayList<>();switch(group){
            case "Progetto":
        if(mission!=null)addTaskCommand(result,group,"Obiettivo dell’esercizio","brand",0,()->{close.run();showPracticeGoal();});
        addTaskCommand(result,group,"Nuovo diagramma","add",0,()->{close.run();createProject();});
        addTaskCommand(result,group,"Apri file…","folder",0,()->{close.run();openDocument();});
        addTaskCommand(result,group,"I miei progetti","folder",0,()->{close.run();showProjects();});
        addTaskCommand(result,group,"Backup dei progetti","save",0,()->{close.run();showBackup();});
        addTaskCommand(result,group,"Rinomina progetto","edit",0,()->{close.run();rename();});
        addTaskCommand(result,group,"Variabili: "+(project.requiresDeclarations()?"dichiarate":"automatiche"),"settings",0,()->{close.run();showVariablePolicy();});
        addTaskCommand(result,group,"Esporta o condividi","export",0,()->{close.run();showExport();});
        addTaskCommand(result,group,"Flowgorithm · salva una copia .fprg","export",0,()->{close.run();showFlowgorithmExport();});
        addTaskCommand(result,group,"Esempi da provare","decision",0,()->{close.run();showExamples();});
                break;
            case "Modifica":
        addTaskCommand(result,group,"Annulla modifica","undo",0,()->{close.run();undo();});
        addTaskCommand(result,group,"Ripeti modifica","redo",0,()->{close.run();redo();});
        addTaskCommand(result,group,"Aggiungi un passaggio","add",0,()->{close.run();showAddForContext();});
        addTaskCommand(result,group,"Crea un blocco libero","add",0,()->{close.run();float[] c=canvas.getViewportCenter();showAdd(c[0],c[1],null,null);});
        addTaskCommand(result,group,canvas.isSelectionMode()?"Termina selezione multipla":"Seleziona più blocchi","select",0,()->{close.run();setSelectionTool(!canvas.isSelectionMode());});
        addTaskCommand(result,group,"Seleziona tutti i blocchi","select",0,()->{close.run();List<String> ids=new ArrayList<>();for(Node n:document.nodes)ids.add(n.id);canvas.selectGroup(ids);});
        addTaskCommand(result,group,"Azioni sulla selezione","more",0,()->{close.run();showSelectionActions();});
        addTaskCommand(result,group,"Cerca nel progetto","edit",0,()->{close.run();showProjectSearch();});
        addTaskCommand(result,group,"Rinomina variabile","edit",0,()->{close.run();showVariableRename(null);});
        addTaskCommand(result,group,"Diagrammi e funzioni","call",0,()->{close.run();showGraphs();});
        addTaskCommand(result,group,"Cicli","undo",0,()->{close.run();showCycleTools();});
        addTaskCommand(result,group,"Riordina i blocchi","align",0,()->{close.run();arrangeOnce();});
        addTaskCommand(result,group,"Disposizione automatica","align",0,()->{close.run();showArrangeOptions();});
                break;
            case "Cicli":
        addTaskCommand(result,group,"Inserisci un ciclo guidato","undo",0,()->{close.run();showLoopEditor();});
        addTaskCommand(result,group,"Cicli: riepilogo e modifica","undo",0,()->{close.run();showCycleManager();});
        addTaskCommand(result,group,"Panoramica cicli · espandi e riduci","undo",0,()->{close.run();showCycleOverview();});
        addTaskCommand(result,group,"Riduci tutti i cicli sul foglio","undo",0,()->{close.run();canvas.foldAllCycles(true);});
        addTaskCommand(result,group,"Espandi tutti i cicli sul foglio","undo",0,()->{close.run();canvas.foldAllCycles(false);});
                break;
            case "Esegui":
        addTaskCommand(result,group,"Verifica diagramma","check",0,()->{close.run();showValidation();});
        addTaskCommand(result,group,"Apri prova · poi scegli Esegui o Avanti","play",0,()->{close.run();showRun(false);});
        addTaskCommand(result,group,"Esegui fino al blocco selezionato","step",0,()->{close.run();runToSelected();});
        addTaskCommand(result,group,"Variabili e chiamate","call",0,()->{close.run();showVariables();});
        addTaskCommand(result,group,"Console · letture e stampe","more",0,()->{close.run();showConsole();});
        addTaskCommand(result,group,"File generati e recupero","save",0,()->{close.run();fileUi.showRecovery();});
        addTaskCommand(result,group,"Prova con dati di test","check",0,()->{close.run();showDataTests();});
                break;
            case "Codice":
        addTaskCommand(result,group,"Pseudocodice collegato · segui i blocchi","edit",0,()->{close.run();showSourceTarget(null);});
        addTaskCommand(result,group,"Pseudocodice ↔ diagramma","edit",0,()->{close.run();showPseudocode();});
        addTaskCommand(result,group,"Genera codice","edit",0,()->{close.run();showLanguageTools();});
        addTaskCommand(result,group,"Modelli di codice personali","edit",0,()->{close.run();showCodeTemplates();});
                break;
            case "Linguaggi":
        for(FlowSourceTarget target:FlowSourceTarget.ALL)addTaskCommand(result,group,"Codice "+target.name+" · visualizza ed esporta","edit",0,()->{close.run();showSourceTarget(target);});
                break;
            case "Vista":
        addTaskCommand(result,group,"Torna alla posizione precedente","back",0,()->{close.run();goBackNavigation();});
        addTaskCommand(result,group,"Layout · "+FlowWorkspaceLayout.NAMES[workspaceMode()],"settings",0,()->{close.run();showWorkspaceLayouts();});
        addTaskCommand(result,group,"Inquadra il diagramma","center",0,()->{close.run();canvas.fitToContent();});
        addTaskCommand(result,group,"Zoom e griglia","center",0,()->{close.run();showCanvasOptions();});
        addTaskCommand(result,group,"Input / Output: "+(FlowModel.separateIo?"separati":"unificati"),"input",0,()->{close.run();showIoPresentation();});
        addTaskCommand(result,group,"Stile dei diagrammi","edit",0,()->{close.run();showDiagramAppearance();});
        addTaskCommand(result,group,"Tema chiaro / scuro","moon",0,()->{close.run();showTheme();});
        addTaskCommand(result,group,"Animazioni: "+(Ui.motion?"attive":"ridotte"),"spark",0,()->{Ui.motion=!Ui.motion;prefs.edit().putBoolean("animations",Ui.motion).apply();canvas.setMotionEnabled(Ui.motion);if(!Ui.motion&&activeSheet!=null&&activeSheet.dialog.getWindow()!=null)activeSheet.dialog.getWindow().setWindowAnimations(0);close.run();showViewTools();});
                break;
            case "Aiuto":
        addTaskCommand(result,group,"Come usare Frociart","help",0,()->{close.run();showGuide();});
        addTaskCommand(result,group,"Informazioni e novità","help",0,()->{close.run();showAbout();});
        addTaskCommand(result,group,"Autore e condizioni d’uso","help",0,()->{close.run();showLicense();});
                break;
        }return result;
    }
    private List<FlowCommandSearch.Entry> allTaskCommands(Runnable close){List<FlowCommandSearch.Entry> result=new ArrayList<>();for(String group:new String[]{"Progetto","Modifica","Cicli","Esegui","Codice","Linguaggi","Vista","Aiuto"})result.addAll(taskCommands(group,close));return result;}
    private void showCommandPalette(String initial){
        if(destroyed||hubVisible||canvas.isInteracting()||canvas.isConnecting()){showHint("Termina il gesto prima di cercare un comando.");return;}
        final Document expected=project;final String expectedId=projectId;Ui.Sheet s=sheet("Cerca un comando","Trova una funzione nei menu.");s.compactHeader(true);
        FlowCommandPalette panel=new FlowCommandPalette(this,s,()->allTaskCommands(s::dismiss),selected->{if(destroyed||hubVisible||project!=expected||!Objects.equals(projectId,expectedId))return false;for(FlowCommandSearch.Entry e:allTaskCommands(s::dismiss))if(e.id.equals(selected.id)&&e.enabled){e.action.run();return true;}return false;},initial==null?commandQuery:initial);commandsPanel=panel;
        s.dialog.setOnDismissListener(d->{commandQuery=panel.query.getText().toString();panel.close();if(commandsPanel==panel)commandsPanel=null;if(activeSheet==s)activeSheet=null;});s.show();panel.showKeyboard();
    }
    private void showMenu(){
        Ui.Sheet s=sheet("Menu","Scegli cosa vuoi fare.");rememberNavigation(s,"Menu");
        s.headerAction("search","Cerca un comando",()->{s.dismiss();showCommandPalette(null);});
        s.choice("Progetto","Apri, rinomina, esporta e recupera i tuoi file",Ui.accentInk(),()->{s.dismiss();showProjectTools();});
        s.choice("Modifica","Annulla, seleziona, modifica e organizza i blocchi",Ui.accentInk(),()->{s.dismiss();showDrawTools();});
        s.choice("Esecuzione","Apri la prova, verifica, variabili e console",Ui.accentInk(),()->{s.dismiss();showRunTools();});
        s.choice("Codice","Pseudocodice, linguaggi e modelli personali",Ui.accentInk(),()->{s.dismiss();showCodeTools();});
        s.section("VISTA E AIUTO");s.action("Vista","settings",0,()->{s.dismiss();showViewTools();});
        s.action("Aiuto","help",0,()->{s.dismiss();showHelpTools();});s.show();
    }
    private Ui.Sheet taskSheet(String title,String detail){
        Ui.Sheet s=sheet(title,detail);rememberNavigation(s,title);
        s.headerAction("back","Menu principale",()->{s.dismiss();showMenu();});return s;
    }
    private void rememberNavigation(Ui.Sheet s,String route){navigationSheet=s;navigationRoute=route;}
    private void restoreNavigation(String route){
        if(route==null)return;switch(route){case "Menu":showMenu();break;case "Progetto":showProjectTools();break;case "Modifica":showDrawTools();break;case "Esegui":case "Esecuzione":showRunTools();break;case "Codice":showCodeTools();break;case "Vista":showViewTools();break;case "Aiuto":showHelpTools();break;case "Cicli":showCycleTools();break;case "Genera codice":showLanguageTools();break;case "Preferenze":showAppSettings();break;}
    }
    private void renderTaskCommands(Ui.Sheet s,String group){for(FlowCommandSearch.Entry e:taskCommands(group,s::dismiss)){
        if(group.equals("Progetto")&&e.label.equals("Esporta o condividi"))s.section("CONDIVIDI");
        if(group.equals("Modifica")&&e.label.equals("Aggiungi un passaggio"))s.section("BLOCCHI E SELEZIONE");
        if(group.equals("Modifica")&&e.label.equals("Diagrammi e funzioni"))s.section("STRUTTURE RIUTILIZZABILI");
        if(group.equals("Esegui")&&e.label.equals("Variabili e chiamate"))s.section("ISPEZIONA L’ESECUZIONE");
        if(group.equals("Vista")&&e.label.equals("Stile dei diagrammi"))s.section("ASPETTO E ANIMAZIONI");
        Ui.Button command=s.action(e.label,e.icon,e.tone,()->{if(e.enabled)e.action.run();});command.setEnabled(e.enabled);
    }}
    private void showProjectTools(){
        Ui.Sheet s=taskSheet("Progetto","Il lavoro si salva automaticamente sul telefono.");
        renderTaskCommands(s,"Progetto");if(store.backupRecoveryWarning()!=null)s.paragraph(store.backupRecoveryWarning());
        s.show();
    }
    private void showIoPresentation(){
        if(canvas.isInteracting()||canvas.isConnecting()){showHint("Termina il gesto prima di cambiare la presentazione dei blocchi.");return;}
        Ui.Sheet s=sheet("Input e Output","Cambia la palette e i colori, mantenendo le istruzioni del diagramma.");
        s.choice("Un solo parallelogramma"+(!FlowModel.separateIo?" · attuale":""),"Scrivi leggi oppure stampa nel blocco. Il colore segue l’istruzione.",Ui.accentInk(),()->{setIoPresentation(false);s.dismiss();});
        s.choice("Input e Output separati"+(FlowModel.separateIo?" · attuale":""),"Due scelte nella palette: lettura azzurra e stampa viola.",Ui.accentInk(),()->{setIoPresentation(true);s.dismiss();});s.show();
    }
    private void setIoPresentation(boolean separate){
        if(canvas.isInteracting()||canvas.isConnecting())return;
        FlowModel.separateIo=separate;prefs.edit().putBoolean("separate_io",separate).apply();canvas.notifyModelChanged();Ui.enter(canvas,0);
    }
    private void showVariablePolicy(){
        if(canvas.isInteracting()||canvas.isConnecting()){showHint("Termina il gesto prima di cambiare le variabili del progetto.");return;}final Document expected=project;
        Ui.Sheet s=sheet("Variabili del progetto","La scelta si salva nel progetto e vale anche nelle funzioni.");
        s.choice("Dichiarazione automatica"+(!project.requiresDeclarations()?" · attuale":""),"Una variabile nasce quando la leggi o le assegni un valore. Puoi usare Dichiara per scegliere il tipo.",Ui.accentInk(),()->{s.dismiss();if(project==expected)setVariablePolicy(false);});
        s.choice("Dichiarazioni obbligatorie"+(project.requiresDeclarations()?" · attuale":""),"Definisci le variabili con Dichiara prima dell’uso. Il controllo trova subito i nomi scritti per errore.",Ui.accentInk(),()->{s.dismiss();if(project==expected)setVariablePolicy(true);});
        s.paragraph("Parametri e nome del risultato sono definiti dalla firma della funzione. I cicli PER gestiscono i propri valori ausiliari; dichiara il contatore. Per riaprire questi progetti, aggiorna Frociart anche sull’altro dispositivo.");s.show();
    }
    private void setVariablePolicy(boolean required){
        if(destroyed||hubVisible||canvas.isInteracting()||canvas.isConnecting()||project.requiresDeclarations()==required)return;
        onBeforeChange();project.variablePolicy=required?"declared":null;FlowAnnotations.promote(project);boolean arranging=autoArrange;
        try{autoArrange=false;changed(required?"Dichiarazioni obbligatorie attive":"Dichiarazione automatica attiva");}finally{autoArrange=arranging;}saveNow();
        showHint(required?"Usa Dichiara prima delle variabili · Verifica trova i nomi da correggere":"Le variabili nascono quando le leggi o assegni · Dichiara resta disponibile");
    }
    private void showDrawTools(){
        Ui.Sheet s=taskSheet("Modifica","Selezione, cronologia e blocchi.");
        renderTaskCommands(s,"Modifica");
        s.show();
    }
    private void showCycleTools(){
        Ui.Sheet s=taskSheet("Cicli","Crea, modifica e leggi i cicli del foglio.");
        renderTaskCommands(s,"Cicli");
        s.show();
    }
    private void showDataTests(){
        if(destroyed||hubVisible||canvas.isInteracting()||canvas.isConnecting())return;
        stopRun();resumeAfterInput=false;if(runVisible)refreshRun();Ui.Sheet s=sheet("Prova dati",null);FlowDataTestPanel panel=new FlowDataTestPanel(this,s,project,prefs,projectId);dataTestPanel=panel;
        s.dialog.setOnDismissListener(d->{panel.close();if(dataTestPanel==panel)dataTestPanel=null;if(activeSheet==s)activeSheet=null;});s.show();
    }
    private void showRunTools(){
        Ui.Sheet s=taskSheet("Esecuzione","Apri prova prepara il pannello. Esegui lo avvia; Avanti procede di un passaggio.");
        renderTaskCommands(s,"Esegui");
        s.show();
    }
    private void showCodeTools(){
        Ui.Sheet s=taskSheet("Codice","Trasforma il diagramma in testo e codice.");
        renderTaskCommands(s,"Codice");
        s.show();
    }
    private void showLanguageTools(){
        Ui.Sheet s=taskSheet("Genera codice","Scegli il linguaggio da visualizzare ed esportare.");
        renderTaskCommands(s,"Linguaggi");
        s.show();
    }
    private void showViewTools(){
        Ui.Sheet s=taskSheet("Vista","Cambia la presentazione del foglio.");
        renderTaskCommands(s,"Vista");
        s.show();
    }
    private void showHelpTools(){
        Ui.Sheet s=taskSheet("Aiuto","Guida e informazioni su Frociart.");
        renderTaskCommands(s,"Aiuto");
        s.action("Informazioni e novità","help",0,()->{s.dismiss();showAbout();});s.paragraph("Frociart "+appBuild()+" · Un progetto di mamt104.\nI progetti funzionano offline e restano sul telefono.");
        s.show();
    }
    private FlowCycleOverview cycleOverview;
    private void showCycleOverview(){if(canvas.isInteracting())return;if(runVisible)closeRun();final String currentProject=projectId,currentFunction=activeFunctionId,snapshot=FlowCodec.encode(project);Ui.Sheet s=sheet("Panoramica cicli","Tocca un ciclo per espanderlo. Apri il foglio per modificare o eseguire.");FlowCycleOverview view=new FlowCycleOverview(this,s,document,n->{if(!projectId.equals(currentProject)||!java.util.Objects.equals(activeFunctionId,currentFunction)||!FlowCodec.encode(project).equals(snapshot)){showHint("Il progetto è cambiato. Riapri la panoramica.");return;}if(n.parameters){for(FlowCycle.Info c:FlowCycle.find(document))if(c.check.equals(n.checkId)){showCycleEditor(c);break;}}else if(document.find(n.nodeId)!=null){rememberNavigation();canvas.select(n.nodeId);canvas.focusNodeAnimated(n.nodeId);showHint("Blocco aperto sul foglio · due tocchi per modificarlo");}});cycleOverview=view;s.dialog.setOnDismissListener(d->{view.close();if(cycleOverview==view)cycleOverview=null;if(activeSheet==s)activeSheet=null;});s.show();}
    private void showCycleManager(){if(canvas.isInteracting())return;if(runVisible)closeRun();java.util.List<FlowCycle.Info> cycles=FlowCycle.find(document);FlowCycle.Info selected=null;for(FlowCycle.Info c:cycles)if(c.members.contains(selectedNodeId)&&(selected==null||c.members.size()<selected.members.size()))selected=c;if(selected!=null){showCycleEditor(selected);return;}Ui.Sheet s=sheet("Cicli del diagramma",cycles.isEmpty()?"Non ci sono cicli con un solo ingresso e una sola uscita. Usa Ciclo guidato oppure modifica i singoli blocchi.":"Scegli un ciclo per i parametri o per selezionarlo insieme.");s.action("Panoramica cicli · espandi e riduci","expand",0,()->{s.dismiss();showCycleOverview();});for(FlowCycle.Info c:cycles)s.choice(c.header.length()>120?c.header.substring(0,117)+"…":c.header,c.members.size()+" blocchi · Parametri e selezione",Ui.accentInk(),()->{s.dismiss();showCycleEditor(c);});s.show();}
    private void showCycleEditor(FlowCycle.Info info){final String originalProject=projectId,snapshot=FlowCodec.encode(project);Ui.Sheet s=sheet("Modifica ciclo","Il corpo resta modificabile nei singoli blocchi sul foglio.");FlowLoopEditor current=new FlowLoopEditor(this,s,project,activeFunctionId,null,plan->{if(!projectId.equals(originalProject))throw new IllegalArgumentException("Il progetto è cambiato. Riapri il riepilogo del ciclo.");applyLoopPlan(plan);},info,ids->{if(projectId.equals(originalProject)&&FlowCodec.encode(project).equals(snapshot)){canvas.selectGroup(ids);showHint("Ciclo selezionato · trascina un blocco per spostare il gruppo");}});loopEditor=current;s.dialog.setOnDismissListener(d->{current.close();if(loopEditor==current)loopEditor=null;if(activeSheet==s)activeSheet=null;});s.show();}
    private void showLoopEditor(){showLoopEditorAt(null);}
    private void showLoopEditorAt(String preferredEdge){
        if(canvas.isInteracting())return;if(runVisible)closeRun();final String originalProject=projectId;Ui.Sheet s=sheet("Inserisci un ciclo","Il ciclo entra nella freccia scelta. Il resto del progetto viene conservato.");String selected=null;for(Edge e:document.edges)if(e.from.equals(selectedNodeId)){selected=e.id;break;}
        FlowLoopEditor current=new FlowLoopEditor(this,s,project,activeFunctionId,preferredEdge==null?selected:preferredEdge,plan->{if(!projectId.equals(originalProject))throw new IllegalArgumentException("Il progetto è cambiato. Riapri Inserisci ciclo.");applyLoopPlan(plan);});loopEditor=current;s.dialog.setOnDismissListener(d->{current.close();if(loopEditor==current)loopEditor=null;if(activeSheet==s)activeSheet=null;});s.show();
    }
    private void applyLoopPlan(FlowLoopBuilder.Plan plan){
        if(!java.util.Objects.equals(activeFunctionId,plan.functionId)||!FlowCodec.encode(project).equals(plan.baseDocument))throw new IllegalArgumentException("Il progetto è cambiato. Riapri Inserisci ciclo per conservare il lavoro più recente.");FlowCodec.encode(plan.project);if(plan.editing&&FlowCodec.encode(project).equals(FlowCodec.encode(plan.project))){showHint("Il ciclo è già aggiornato");return;}Map<String,float[]> before=positions(),oldSizes=new java.util.HashMap<>();for(Node n:document.nodes)oldSizes.put(n.id,new float[]{FlowModel.width(n),FlowModel.height(n)});onBeforeChange();project=plan.project;document=FlowFunctionEditing.graph(project,activeFunctionId);canvas.setDocument(document,false);canvas.select(plan.entryId);for(Node n:document.nodes)if(!before.containsKey(n.id))before.put(n.id,plan.origin.clone());boolean arrange=autoArrange;try{autoArrange=false;changed(plan.editing?"Ciclo aggiornato":"Ciclo inserito");}finally{autoArrange=arrange;}canvas.animateLayout(before,positions(),420);canvas.animateSizesFrom(oldSizes);saveNow();showHint(plan.editing?"Ciclo aggiornato · Annulla ripristina la modifica":"Ciclo inserito · "+plan.added+" blocchi modificabili · Annulla ripristina tutto");
    }
    private void showArrangeOptions(){
        Ui.Sheet s=sheet("Disposizione dei blocchi","Queste azioni cambiano la posizione delle caselle. Annulla ripristina il diagramma.");
        s.action("Riordina adesso","align",2,()->{s.dismiss();arrangeOnce();});
        s.action("Disposizione automatica: "+(autoArrange?"attiva":"disattivata"),"align",autoArrange?2:0,()->{s.dismiss();setAutoArrange(!autoArrange);});
        s.paragraph("Quando è attiva, i nuovi passaggi mantengono spazi regolari. Trascinare un blocco torna alla posizione libera.");s.show();
    }
    private void showCanvasOptions(){
        Ui.Sheet s=sheet("Zoom e griglia","Cambia la vista senza spostare i blocchi.");
        s.action("Griglia: "+(canvas.isSnapToGrid()?"ON":"OFF"),"align",canvas.isSnapToGrid()?2:0,()->{boolean enabled=!canvas.isSnapToGrid();canvas.setSnapToGrid(enabled);prefs.edit().putBoolean("snap_grid",enabled).apply();updateToolState();s.dismiss();showCanvasOptions();});

        s.action("Inquadra il diagramma","center",0,()->{s.dismiss();canvas.fitToContent();});
        s.action("Torna all'inizio, leggibile","center",0,()->{s.dismiss();focusReadingStart();});
        s.action("Riduci zoom","minus",0,()->{s.dismiss();canvas.zoomBy(.7f);});
        s.action("Aumenta zoom","add",0,()->{s.dismiss();canvas.zoomBy(1.4f);});s.show();
    }
    private void rename(){Ui.Sheet s=sheet("Rinomina progetto",null);EditText name=Ui.edit(this,project.title,"Nome del progetto",false);name.setFilters(new InputFilter[]{new InputFilter.LengthFilter(120)});s.body.addView(name);s.primary("Salva nome","check",()->{String n=name.getText().toString().trim();if(n.isEmpty()){name.setError("Scegli un nome.");return;}onBeforeChange();project.title=n;changed("Progetto rinominato");saveNow();s.dismiss();});s.show();}
    private void showProjects(){
        saveNow();Ui.Sheet s=sheet("I miei progetti","Salvati automaticamente su questo telefono.");
        s.action("Nuovo diagramma","add",2,()->{s.dismiss();createProject();});
        s.action("Apri dal telefono","folder",0,()->{s.dismiss();openDocument();});
        TextView loading=Ui.note(this,"Leggo i progetti…");s.body.addView(loading);s.show();
        io.execute(()->{List<FlowStore.Entry> entries=store.entries();handler.post(()->{if(destroyed||!s.dialog.isShowing())return;s.body.removeView(loading);SimpleDateFormat date=new SimpleDateFormat("d MMM · HH:mm",Locale.ITALIAN);
            ListView list=new ListView(this);list.setDivider(new android.graphics.drawable.ColorDrawable(Color.TRANSPARENT));list.setDividerHeight(dp(8));list.setVerticalScrollBarEnabled(false);list.setOnTouchListener((v,e)->{v.getParent().requestDisallowInterceptTouchEvent(e.getActionMasked()!=android.view.MotionEvent.ACTION_UP&&e.getActionMasked()!=android.view.MotionEvent.ACTION_CANCEL);return false;});
            list.setAdapter(new BaseAdapter(){public int getCount(){return entries.size();}public Object getItem(int p){return entries.get(p);}public long getItemId(int p){return p;}
                public View getView(int position,View convert,ViewGroup parent){FlowStore.Entry e=entries.get(position);LinearLayout row;if(convert instanceof LinearLayout)row=(LinearLayout)convert;else{row=Ui.row(MainActivity.this);Ui.pad(row,12,10,8,10);Ui.card(row,Ui.bg(),16);LinearLayout labels=Ui.column(MainActivity.this);labels.addView(Ui.text(MainActivity.this,"",16,true));TextView sub=Ui.note(MainActivity.this,"");Ui.pad(sub,0,5,0,0);labels.addView(sub);row.addView(labels,Ui.weight(MainActivity.this,-2));row.addView(iconButton("trash","Elimina progetto",()->{}),Ui.lp(MainActivity.this,48,48));}
                    LinearLayout labels=(LinearLayout)row.getChildAt(0);((TextView)labels.getChildAt(0)).setText(e.title);((TextView)labels.getChildAt(1)).setText((e.nodes<0?"File da recuperare":e.nodes+(e.nodes==1?" blocco":" blocchi"))+" · "+date.format(new Date(e.modified)));View delete=row.getChildAt(1);delete.setContentDescription("Elimina progetto "+e.title);delete.setOnClickListener(v->confirmDelete(e,s));row.setOnClickListener(v->{final int request=++projectOpenRequest;final Document expected=document;io.execute(()->{try{Document d=store.load(e.id);handler.post(()->{if(!destroyed&&request==projectOpenRequest&&s.dialog.isShowing()&&document==expected){s.dismiss();switchDocument(d,e.id);}});}catch(Exception ex){handler.post(()->{if(!destroyed&&request==projectOpenRequest&&s.dialog.isShowing()&&document==expected)message("Non riesco ad aprirlo",ex.getMessage());});}});});return row;}
            });s.body.addView(list,new LinearLayout.LayoutParams(-1,Math.min(dp(380),Math.max(dp(80),entries.size()*dp(82)))));
        });});
    }
    private void confirmDelete(FlowStore.Entry e,Ui.Sheet original){original.dismiss();Ui.Sheet s=sheet("Eliminare il progetto?","“"+e.title+"” sarà rimosso dal telefono.");s.action("Elimina progetto","trash",3,()->{
        boolean current=e.id.equals(projectId);if(current){stopRun();if(!saveNow())return;}
        if(!store.delete(e.id)){toast("Non riesco a eliminare il progetto. Se è aperto in un’altra finestra, chiudila e riprova.");return;}
        drafts.removeProject(e.id);SharedPreferences.Editor draftCleanup=prefs.edit();for(String key:prefs.getAll().keySet())if(key.startsWith("function_draft_"+e.id+"_")||key.equals("function_draft_last_"+e.id))draftCleanup.remove(key);draftCleanup.remove("graph_"+e.id).apply();String task=prefs.getString("mission_"+e.id,"");SharedPreferences.Editor preferences=prefs.edit().remove("auto_"+e.id).remove("practice_"+e.id).remove("mission_"+e.id);if(!task.isEmpty()&&e.id.equals(prefs.getString("mission_project_"+task,"")))preferences.remove("mission_project_"+task);if(current)preferences.remove("current");preferences.apply();
        s.dismiss();if(current){dirty=false;switchDocument(starter(),null,true);}showProjects();
    });s.primary("Tieni il progetto","check",s::dismiss);s.show();}
    private void showExamples(){
        Ui.Sheet s=sheet("Esempi e sfide","Ogni esempio si apre come un nuovo progetto, senza modificare quello precedente.");
        List<Document> examples=FlowExamples.all();
        for(int section=0;section<2;section++){
            fieldLabel(s.body,section==0?"PRONTI DA ESEGUIRE":"SFIDA DA CORREGGERE");
            for(Document d:examples){final boolean exercise=FlowExamples.isExercise(d);if(exercise!=(section==1))continue;
                s.action(exercise?d.title+" · da correggere":d.title,exercise?"decision":"folder",0,()->{s.dismiss();if(switchDocument(d.copy(),null)&&exercise)showValidation();});s.paragraph(FlowExamples.description(d));
            }
        }s.show();
    }
    @Override public boolean onKeyShortcut(int keyCode,KeyEvent event){if(keyCode==KeyEvent.KEYCODE_K&&event.isCtrlPressed()&&!event.isAltPressed()&&!hubVisible&&activeSheet==null){showCommandPalette(null);return true;}if(keyCode==KeyEvent.KEYCODE_F&&event.isCtrlPressed()&&!event.isAltPressed()){showProjectSearch();return true;}return super.onKeyShortcut(keyCode,event);}
    private void showVariableRename(FlowVariableRenamePanel.State restored){
        if(canvas.isInteracting())return;if(restored!=null&&!Objects.equals(projectId,restored.projectId))return;if(runVisible)closeRun();
        final String expectedProject=projectId;final String scope=restored==null?activeFunctionId:restored.scope;
        if(!Objects.equals(scope,activeFunctionId))return;
        if(scope!=null&&prefs.contains("function_draft_"+projectId+"_"+scope)){
            Ui.Sheet blocked=sheet("Bozza della funzione da conservare","Applica o scarta la bozza della definizione prima di rinominare una variabile in questa funzione.");blocked.primary("Apri bozza della funzione","edit",()->{blocked.dismiss();showFunctionEditor(scope);});blocked.show();return;
        }
        try{if(FlowVariableRename.names(project,scope).isEmpty()){showHint("Non ci sono ancora variabili. Aggiungi un Input o un'Operazione.");return;}}
        catch(IllegalArgumentException error){showHint(error.getMessage());return;}
        Ui.Sheet s=sheet("Rinomina variabile",null);
        FlowVariableRenamePanel current=new FlowVariableRenamePanel(this,s,project,projectId,scope,plan->{
            if(!Objects.equals(projectId,expectedProject))throw new IllegalArgumentException("Il progetto è cambiato. Riapri Rinomina variabile.");
            Document candidate=plan.apply(project,activeFunctionId);FlowFunctionEditing.Rename rename=new FlowFunctionEditing.Rename(scope,plan.oldName,plan.newName);
            saveFunctionChange(candidate,rename,false);Set<String> selected=new LinkedHashSet<>(canvas.getSelectedIds());onBeforeChange();undo.getLast().rename=rename;
            project=candidate;document=FlowFunctionEditing.graph(project,activeFunctionId);canvas.setDocument(document,false);canvas.selectGroup(selected);changed("Variabile rinominata · "+plan.summary()+" · Annulla ripristina tutta la modifica");saveNow();Ui.enter(canvas,0);
        });variableRename=current;s.dialog.setOnDismissListener(d->{current.close();if(variableRename==current)variableRename=null;if(activeSheet==s)activeSheet=null;});s.show();if(restored!=null)current.restore(restored);
    }
    private void showProjectSearch(){
        if(canvas==null||canvas.isInteracting())return;if(hubVisible)openEditor();canvas.cancelConnection();
        Ui.Sheet s=sheet("Cerca nel progetto",null);final Document expected=project;
        FlowProjectSearch current=new FlowProjectSearch(this,s,FlowSearch.capture(project),activeFunctionId,hit->{if(project!=expected){showHint("Il progetto è cambiato. Riapri la ricerca.");return;}locateSearchHit(hit);});
        projectSearch=current;s.dialog.setOnDismissListener(d->{current.close();if(projectSearch==current)projectSearch=null;if(activeSheet==s)activeSheet=null;});current.show();
    }
    private boolean locateSearchHit(FlowSearch.Hit hit){
        if(hit==null||canvas.isInteracting())return false;FlowSearch.Entry origin=hit.origin;
        FlowModel.Function function=FlowFunctionEditing.find(project,origin.functionId);
        if(origin.functionId!=null&&function==null){showHint("La funzione non esiste più. Riapri Cerca nel progetto.");return false;}
        Document graph=function==null?project:function.body;
        if(origin.nodeId!=null&&graph.find(origin.nodeId)==null){showHint("Il blocco non esiste più. Riapri Cerca nel progetto.");return false;}
        rememberNavigation();stopRun();boolean trial=runVisible;if(!activateGraph(origin.functionId,trial))return false;
        canvas.setReadOnly(false);try{if(origin.nodeId!=null)canvas.focusNodeAnimated(origin.nodeId);else{canvas.clearSelection();String start=null;for(Node n:document.nodes)if(n.kind==FlowModel.START){start=n.id;break;}if(start==null&&!document.nodes.isEmpty())start=document.nodes.get(0).id;if(start!=null)canvas.frameNodeAnimated(start);}}finally{canvas.setReadOnly(trial);}
        showHint(hit.context());return true;
    }
    private void showDiagramAppearance(){if(canvas.isInteracting())return;Ui.Sheet s=sheet("Stile dei diagrammi","Preferenza del dispositivo. Il progetto e i colori salvati restano invariati; anche le esportazioni seguono lo stile.");for(int i=0;i<FlowAppearance.NAMES.length;i++){final int mode=i;s.choice(FlowAppearance.NAMES[i]+(canvas.getAppearance()==i?" · attivo":""),FlowAppearance.DESCRIPTIONS[i],Ui.accentInk(),()->{s.dismiss();if(mode==3)showDesignEditor(null);else applyDiagramAppearance(mode);});}s.show();}
    private void applyDiagramAppearance(int mode){try{applyDiagramDesign(mode,FlowAppearance.currentDesign());}catch(IllegalArgumentException e){message("Aspetto non applicato",e.getMessage());}}
    private void showDesignEditor(Bundle draft){
        if(canvas.isInteracting())return;Ui.Sheet s=sheet("Aspetto",null);final Document expected=project;
        FlowDesignEditor editor=new FlowDesignEditor(this,s,document,canvas.appearanceStyle().customDesign,draft,design->{if(project!=expected)throw new IllegalArgumentException("Il progetto è cambiato. Riapri Personalizza.");applyDiagramDesign(3,design);});designEditor=editor;
        s.dialog.setOnDismissListener(d->{editor.close();if(designEditor==editor)designEditor=null;if(activeSheet==s)activeSheet=null;});s.show();
    }
    private void applyDiagramDesign(int mode,FlowDesign design){
        if(canvas.isInteracting())return;FlowAppearance style=new FlowAppearance(mode,design),old=canvas.appearanceStyle();if(old.mode==style.mode&&old.customDesign.same(style.customDesign))return;
        Document plan=project.copy();boolean geometry=old.design().fontKey()!=style.design().fontKey();if(geometry){java.util.HashMap<String,Node> originals=new java.util.HashMap<>();for(Document g:FlowFunctionEditing.graphs(project))for(Node n:g.nodes)originals.put(n.id,n);java.util.List<Document> sourceGraphs=FlowFunctionEditing.graphs(project),plannedGraphs=FlowFunctionEditing.graphs(plan);for(int i=0;i<plannedGraphs.size();i++){Document g=plannedGraphs.get(i);for(Node n:g.nodes){Node original=originals.get(n.id);if(original!=null){n.previewText=original.previewText;n.previewComment=original.previewComment;}}FlowAutoLayout.applyAnchored(g,sourceGraphs.get(i),FlowCanvasView.fontSizing(old.design()),FlowCanvasView.fontSizing(style.design()),sourceGraphs.get(i)==document&&!hubVisible?canvas.getViewportCenter():null);}}
        FlowFunctionEditing.History history=new FlowFunctionEditing.History(project,activeFunctionId);history.appearance=old;undo.addLast(history);while(undo.size()>60)undo.removeFirst();redo.clear();boolean saved=applyDesignPositions(plan,style);
        showHint("Stile "+FlowAppearance.NAMES[style.mode]+(geometry?" · spaziatura adattata. Annulla ripristina.":" · istruzioni e collegamenti invariati.")+(saved?"":" Preferenza attiva solo per questa sessione."));
    }
    private boolean adaptOpeningText(){
        try{
            Map<String,float[]> plan=FlowTextSpacing.plan(project,canvas.appearanceStyle().design());if(plan.isEmpty())return false;
            FlowFunctionEditing.History history=new FlowFunctionEditing.History(project,activeFunctionId);history.appearance=canvas.appearanceStyle();undo.addLast(history);while(undo.size()>60)undo.removeFirst();redo.clear();
            Map<String,float[]> before=positions();
            for(Document graph:FlowFunctionEditing.graphs(project))for(Node n:graph.nodes){float[] p=plan.get(n.id);if(p!=null){n.x=p[0];n.y=p[1];}}
            canvas.notifyModelChanged();canvas.animateLayout(before,positions(),340);documentRevision++;dirty=true;
            return true;
        }catch(IllegalArgumentException ex){toast("Spaziatura originale mantenuta. "+ex.getMessage());return false;}
    }
    private boolean applyDesignPositions(Document plan,FlowAppearance style){
        Map<String,float[]> before=positions();java.util.HashMap<String,float[]> sizes=new java.util.HashMap<>(),target=new java.util.HashMap<>();for(Node n:document.nodes){FlowModel.NodeSize z=FlowModel.size(n,canvas.sizingProfile());sizes.put(n.id,new float[]{z.width,z.height});}for(Document g:FlowFunctionEditing.graphs(plan))for(Node n:g.nodes)target.put(n.id,new float[]{n.x,n.y});
        boolean moved=false;for(Document g:FlowFunctionEditing.graphs(project))for(Node n:g.nodes){float[] xy=target.get(n.id);if(xy!=null){moved|=n.x!=xy[0]||n.y!=xy[1];n.x=xy[0];n.y=xy[1];}}
        FlowAppearance.setSettings(style.mode,style.customDesign);FlowCanvasView.installSizing();boolean saved=prefs.edit().putInt("diagram_style",style.mode).putString("diagram_design",style.customDesign.encode()).commit();canvas.setAppearance(style);canvas.notifyModelChanged();canvas.animateSizesFrom(sizes);canvas.animateLayout(before,positions(),340);
        if(moved){documentRevision++;dirty=true;saveNow();}updateToolState();refreshHeader();return saved;
    }

    private void showTheme(){Ui.Sheet s=sheet("Aspetto","Scegli come vuoi vedere il foglio.");String[] labels={"Segui il telefono","Chiaro","Scuro"};String[] modes={"system","light","dark"};for(int i=0;i<labels.length;i++){String mode=modes[i];s.action(labels[i],i==2?"moon":"sun",prefs.getString("theme","system").equals(mode)?2:0,()->{prefs.edit().putString("theme",mode).apply();Ui.dark=mode.equals("dark")||(mode.equals("system")&&(getResources().getConfiguration().uiMode&Configuration.UI_MODE_NIGHT_MASK)==Configuration.UI_MODE_NIGHT_YES);boolean restore=runVisible;String typed=runInput==null?"":runInput.getText().toString();s.dismiss();canvas.setDark(Ui.dark);if(hubVisible)showHome();else{buildChrome();if(restore){showRun(false);runInput.setText(typed);}}});}s.show();}
    private void showGuide(){Ui.Sheet s=sheet("Guida","Disegna, collega e prova.");String[][] rules={{"Il primo flowchart","Dalla Home scegli Nuovo flowchart, oppure Prova un esempio per partire da un algoritmo già collegato. Il progetto si salva automaticamente sul telefono."},{"Tocca, collega e prova","Tocca un blocco e usa il + per aggiungere il passaggio successivo già collegato. Due tocchi modificano il testo. Nel menu Blocco puoi anche trascinare una casella su una freccia. Premi Apri prova, poi Esegui per seguire il percorso; Avanti procede di un passaggio."},{"Con una tastiera","Sul foglio usa le frecce per scegliere una casella, PagSu/PagGiù per seguire l’ordine del flowchart e Home/End per gli estremi. Invio modifica il blocco o espande il ciclo ridotto; durante Collega conferma l’arrivo. Maiusc+F10 apre le azioni della selezione. Esc annulla Collega o deseleziona. Tab e Maiusc+Tab passano agli altri strumenti."},{"Prova dal codice","In Menu > Codice > Genera codice scegli un linguaggio, poi Segui l’esecuzione. Avanti ed Esegui usano la prova del diagramma: inserisci i valori nel pannello, leggi o copia il risultato e scegli i file richiesti. Chiudere mette in pausa e conserva il valore digitato. Disattiva Segui blocco per leggere altre righe. Sugli schermi larghi codice e risultato si affiancano; la rotazione conserva bozza e lettura."},{"Variabili del progetto","In Menu > Progetto > Variabili scegli Dichiarazione automatica oppure Dichiarazioni obbligatorie. La seconda segnala i nomi scritti per errore e richiede Dichiara prima dell’uso, anche nelle funzioni. Parametri e risultato sono definiti dalla firma. Dichiara il contatore dei cicli PER: limite e passo ausiliari sono gestiti dalla struttura. La scelta si salva nel progetto, si annulla con Annulla e vale in tutti i layout. Nel pseudocodice metti VARIABILI DICHIARATE prima di funzioni e programma. Aggiorna Frociart anche sull’altro dispositivo per riaprire questi progetti."},{"Modelli di codice","In Menu > Codice apri Modelli di codice personali. Crea un modello da Python, Java, C#, C++, JavaScript o TypeScript e inserisci {{ source }} per il programma completo. Formato libero espone i dati logici di blocchi, funzioni ed espressioni. Modifica conserva anche una sintassi incompleta come bozza; Salva modello aggiorna la versione da usare. Esporta .frotemplate per portare il modello sull’altro dispositivo. Nel Cestino puoi ripristinare modello e bozza. La guida nel pannello descrive campi, macro e limiti. I modelli non eseguono codice nell’app; puoi importare anche modelli Flowgorithm .fpgt, modificarne le sezioni originali e scegliere i tipi della sola copia esportata."},{"Esporta codice C++","In Menu > Codice > Genera codice scegli C++. Copia e Salva includono il sorgente completo. Il visualizzatore collega le righe ai blocchi anche quando nascondi il supporto. Salva Programma.cpp e compila con g++ -std=c++17 -O2 Programma.cpp -o Programma; su Linux/macOS aggiungi -pthread. File e SVG usano la cartella di esecuzione; prima di sostituire un file viene chiesta conferma. Le scritture interrotte restano in una bozza nella destinazione."},{"JavaScript e TypeScript","In Menu > Codice > Genera codice scegli il linguaggio. Copia tutto e Salva includono il programma completo. Sul computer usa Node.js 22 o successivo: salva Programma.cjs e avvia node Programma.cjs. Per TypeScript salva Programma.cts, compila con tsc --strict --target ES2020 --module node16 Programma.cts, poi avvia node Programma.cjs. Le righe seguono i blocchi della prova. I file usano i percorsi scritti nel programma e la cartella di esecuzione: prima di sostituire un file viene chiesta conferma. Una scrittura interrotta resta in una bozza di recupero nella destinazione. La tartaruga salva un SVG con nome univoco e ne mostra il percorso. Non è un programma da aprire nel browser."},{"Esporta codice C#","In Menu > Codice > Genera codice scegli C#. Il visualizzatore collega le righe ai blocchi; Salva .cs e Copia tutto includono il supporto autonomo. Adatta righe cambia solo la visualizzazione, conservando selezione e testo. Salva Programma.cs sul computer e usa csc /r:System.Numerics.dll Programma.cs con un compilatore C# disponibile. Il programma apre i percorsi scritti nel codice, dalla cartella in cui lo esegui: le autorizzazioni della prova non vengono trasferite. Chiede conferma prima di sostituire un file; le scritture non concluse restano in una bozza di recupero nella cartella di destinazione. I comandi tartaruga salvano un SVG con nome univoco; il percorso compare a fine esecuzione. I limiti di passi, memoria e ricorsione sono conservati."},{"Console della prova","In Prova apri Console · letture e stampe. Inserisci valori e usa Avanti o Esegui nello stesso pannello. Messaggi distingue LEGGI e STAMPA; Testo mostra la cronologia selezionabile con variabile, funzione e passo. Tocca un messaggio per leggere tutto, copiare il valore o raggiungere il blocco. Copia e Salva .txt includono anche gli input; il risultato originale resta separato. Svuota elimina solo la cronologia, Riprova avvia una nuova prova. Diagramma e dati include gli input conservati. La console è temporanea e non viene salvata nel progetto: al massimo 4096 messaggi e 524288 caratteri UTF-16, metadati inclusi. Quando il limite elimina messaggi precedenti, lo segnala. Testo e .txt riportano le etichette e le stampe senza a capo; per il solo output usa Copia nel risultato."},{"Stampa sulla stessa riga","Nella modifica del blocco disattiva Vai a capo dopo la stampa. Il valore successivo verrà unito senza spazi aggiunti. Nel testo puoi usare stampa_continua espressione; nel pseudocodice STAMPA_CONTINUA. STAMPA conclude la riga."},{"Apri progetti Flowgorithm",FlowgorithmImport.HELP},{"Disegna con la tartaruga","Apri Disegno · tartaruga nel menu dei blocchi. Avanza sposta la penna, Ruota cambia direzione, Colore penna sceglie il colore. Origine torna al centro senza traccia; Pulisci disegno cancella e torna al centro. Apri l’esempio Disegna un quadrato e premi Prova: il disegno segue ogni passo. Espandi per zoom e salvataggio PNG/SVG. Distanze e angoli possono usare le variabili del programma."},{"Leggi e scrivi file","Apri il gruppo File nel menu dei blocchi. Scegli il canale Lettura o Scrittura e un nome fra virgolette. In esecuzione scegli tu il documento e la destinazione: il nome non apre automaticamente un percorso. Dichiara riga: TESTO e usa non EOF() per leggere fino alla fine; l’esempio Copia un file di testo mostra il ciclo. Scrivi file aggiunge un valore, con A capo facoltativo. Chiudi file in Scrittura chiede dove salvare; annullare conserva una copia locale in File generati. I canali sono condivisi con le funzioni. Testo UTF-8 o UTF-16 con BOM, fino a 4 MB e 2 milioni di caratteri UTF-16; una riga fino a 32768 caratteri. Le righe scritte usano LF."},{"Scorri il percorso","Il foglio resta vicino ai tuoi blocchi. Scorri con un dito e usa due dita per lo zoom. Tutto mostra il diagramma intero. Riordina anima la disposizione dei blocchi e adatta la vista una volta, senza attivare un automatismo; trascinare un blocco passa alla posizione libera."},{"Modifica un blocco","Toccalo e premi Modifica, oppure fai doppio tocco. Trascina direttamente un blocco per spostarlo conservando le frecce. Tienilo fermo, poi completa il riempimento: al rilascio si stacca. Per una decisione con due percorsi scegli quale ricucire, oppure Nessuno; Annulla stacco conserva tutto. Nessuna casella dei rami viene eliminata."},{"Seleziona un gruppo","Apri Menu > Modifica > Seleziona più blocchi e trascina un riquadro. Puoi anche tenere premuto nel vuoto; in modalità selezione tieni premuto un blocco per aggiungerlo al gruppo."},{"Continua il flusso","Tocca un blocco, anche Inizio: il + pulsa. Tocca il + per scegliere subito il nuovo passaggio, oppure trascinalo dove vuoi crearlo. Se aggiungi un passaggio in mezzo, il resto del flusso rimane collegato. Rilascia il + sopra un blocco esistente per collegarlo: l’arrivo si illumina prima del rilascio."},{"Note libere","In Aggiungi scegli Nota libera o trascinala sul foglio. Scrivi spiegazioni su più righe: restano fuori dal percorso e non si collegano alle frecce. Puoi spostarle, duplicarle ed eliminarle con Annulla. Riordina mantiene la loro posizione. Le immagini e i progetti le includono anche nelle funzioni, su PC e Android."},{"Modifica una freccia","In modalità Sposta, tocca la linea o la sua etichetta: puoi cambiare arrivo o scollegarla. Trascina Sì/No o il punto della freccia per cambiare destinazione; rilascia nel vuoto per aggiungere un passaggio oppure scegli Scollega questo collegamento. Con Collega scegli una destinazione dall’elenco o direttamente sul foglio. Le uscite già occupate chiedono conferma prima di cambiare arrivo. Tutte queste modifiche si possono annullare."},{"Personalizza le decisioni","Nella modifica di una decisione scegli nome e colore per il risultato vero e quello falso. Il testo della condizione determina il percorso."},{"Verifica e prova","Verifica controlla struttura, sintassi e variabili. In Prova una luce segue la freccia effettivamente percorsa. Avanti avanza una volta; Esegui continua da solo. Variabili e risultato mostrano cosa cambia. Rapida esegue gruppi di passi e mostra l’ultimo passaggio di ogni gruppo."},{"Raggiungi un blocco","Seleziona un solo blocco, poi Menu > Esegui > Esegui fino al blocco selezionato. La prova si ferma prima del prossimo passaggio su quel blocco. Input, file e pause restano attivi. Pausa o un altro comando annullano la destinazione temporanea; non viene aggiunto un punto di interruzione al progetto. Se il percorso non passa di lì, viene spiegato al termine."},{"Scrivi le espressioni","Assegna: totale = 2 + 3\nConfronta: numero % 2 = 0\nTesto: \"Ciao!\"\nApri Funzioni ed esempi nella modifica del blocco: cerca numeri, testi, conversioni, trigonometria, logaritmi o vettori. Tocca un esempio per inserirlo nel punto in cui scrivi. Gli angoli sono in radianti; pi e π indicano pi greco."},{"Porta il lavoro sul PC","Scegli Menu > Progetto > Esporta o condividi > Progetto modificabile · .frociart. Aprila in Frociart Windows. PDF, SVG e PNG servono per stampare o condividere il disegno."},{"Il tuo lavoro resta qui","Salvataggio automatico e bozze separate per ogni blocco e progetto. Chiudere un campo conserva la bozza; Applica aggiorna il blocco, Scarta elimina solo quella modifica. Esporta i progetti prima di disinstallare l'app. Fino a 500 blocchi, 1.500 frecce, 4 MB e 10.000 passi."}};for(String[] r:rules){s.body.addView(Ui.text(this,r[0],16,true));Ui.gap(s.body,6);s.paragraph(r[1]);}s.primary("Inizia a creare","check",()->{s.dismiss();openEditor();});s.show();}
    private void showValidation(){
        stopRun();Document original=project,snapshot=project.copy();long revision=documentRevision;int request=++validationRequest;toast("Controllo il diagramma…");
        io.execute(()->{List<FlowEngine.Issue> issues=FlowEngine.validate(snapshot);handler.post(()->{if(destroyed||hubVisible||runVisible||activeSheet!=null||request!=validationRequest)return;if(project!=original||documentRevision!=revision){showHint("Diagramma aggiornato. Premi Verifica per controllare le modifiche.");return;}showValidationResults(issues);});});
    }
    private void showValidationResults(List<FlowEngine.Issue> issues){
        int errors=0;for(FlowEngine.Issue i:issues)if(i.error)errors++;
        Ui.Sheet s=sheet(errors==0?"Pronto da provare":errors+(errors==1?" cosa da sistemare":" cose da sistemare"),errors==0?"La struttura e le espressioni hanno superato i controlli.":"Tocca una segnalazione per trovare il blocco.");
        if(issues.isEmpty())s.paragraph("Nessun problema rilevato. Prova anche input diversi: il risultato dipende dai dati che inserisci.");
        else {ListView list=new ListView(this);list.setDivider(new android.graphics.drawable.ColorDrawable(Color.TRANSPARENT));list.setDividerHeight(dp(8));list.setVerticalScrollBarEnabled(false);list.setOnTouchListener((v,e)->{v.getParent().requestDisallowInterceptTouchEvent(e.getActionMasked()!=android.view.MotionEvent.ACTION_UP&&e.getActionMasked()!=android.view.MotionEvent.ACTION_CANCEL);return false;});
            list.setAdapter(new BaseAdapter(){public int getCount(){return issues.size();}public Object getItem(int position){return issues.get(position);}public long getItemId(int position){return position;}
                public View getView(int position,View convert,ViewGroup parent){LinearLayout card;if(convert instanceof LinearLayout)card=(LinearLayout)convert;else{card=Ui.column(MainActivity.this);Ui.pad(card,14,12,14,12);Ui.card(card,Ui.bg(),16);card.addView(Ui.text(MainActivity.this,"",15,true));Ui.gap(card,6);card.addView(Ui.note(MainActivity.this,""));}
                    FlowEngine.Issue issue=issues.get(position);TextView title=(TextView)card.getChildAt(0),detail=(TextView)card.getChildAt(2);title.setText((issue.functionId==null?"Principale":FlowFunctionEditing.find(project,issue.functionId).name)+" · "+issue.message);title.setTextColor(issue.error?Ui.danger():Ui.ink());detail.setText(issue.suggestion==null?"":issue.suggestion);detail.setVisibility(issue.suggestion==null||issue.suggestion.isEmpty()?View.GONE:View.VISIBLE);return card;}
            });list.setOnItemClickListener((parent,view,position,id)->{s.dismiss();FlowEngine.Issue issue=issues.get(position);if(issue.nodeId!=null){rememberNavigation();activateGraph(issue.functionId,false);canvas.focusNode(issue.nodeId);canvas.select(issue.nodeId);}});s.body.addView(list,new LinearLayout.LayoutParams(-1,Math.min(dp(400),Math.max(dp(110),issues.size()*dp(110)))));
        }
        if(errors==0)s.primary("Prova l'esecuzione","play",()->{s.dismiss();showRun(true);});s.show();
    }
    private FlowDebugger getDebugger(){if(runner==null)return null;if(debugger==null||debugger.runner!=runner){debugger=new FlowDebugger(runner,project);debugger.ignoreBreakpoints=ignoreBreakpoints;}return debugger;}
    private void debuggerHistory(){stopRun();resumeAfterInput=false;openGuard.cancel();undo.addLast(new FlowFunctionEditing.History(project,activeFunctionId));while(undo.size()>60)undo.removeFirst();redo.clear();}
    private void debuggerChanged(){if(getDebugger()!=null)debugger.updateBreakpoints(project);documentRevision++;dirty=true;canvas.notifyModelChanged();refreshHeader();onSelectionChanged();saveNow();if(runVisible)refreshRun();}
    private void editBreakpoint(String id){
        Node node=document.find(id);if(node==null||node.kind==FlowModel.NOTE||canvas.isInteracting())return;stopRun();resumeAfterInput=false;if(runVisible)refreshRun();
        Ui.Sheet s=sheet("Punto di interruzione","Pausa prima del blocco. Le variabili conservano i valori già calcolati.");
        android.widget.Switch enabled=new android.widget.Switch(this);enabled.setText("Ferma qui l'esecuzione");enabled.setTextColor(Ui.ink());enabled.setChecked(node.breakpoint||node.kind!=FlowModel.PAUSE);enabled.setMinHeight(dp(52));enabled.setTag("debug-enabled");s.body.addView(enabled,new LinearLayout.LayoutParams(-1,-2));
        fieldLabel(s.body,"CONDIZIONE FACOLTATIVA · VUOTA = SEMPRE");EditText condition=Ui.edit(this,node.breakpointCondition==null?"":node.breakpointCondition,"Esempio: contatore >= 5",true);condition.setFilters(new InputFilter[]{new InputFilter.LengthFilter(8192)});condition.setContentDescription("Condizione del punto di interruzione");condition.setTag("debug-condition");condition.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int start,int count,int after){}public void onTextChanged(CharSequence s,int start,int before,int count){condition.setError(null);}public void afterTextChanged(Editable e){}});s.body.addView(condition);
        s.body.addView(Ui.note(this,"Usa le variabili locali e le funzioni integrate, esclusa random. Le funzioni del progetto non sono ammesse nelle condizioni di pausa."));
        s.primary("Applica punto di interruzione","check",()->{if(!document.nodes.contains(node)){s.dismiss();return;}String text=condition.getText().toString();try{if(enabled.isChecked())new FlowEngine.DebugCondition(text);}catch(IllegalArgumentException ex){condition.setError(ex.getMessage());condition.requestFocus();return;}if(node.breakpoint!=enabled.isChecked()||!java.util.Objects.equals(node.breakpointCondition,text)){debuggerHistory();node.breakpoint=enabled.isChecked();node.breakpointCondition=text;debuggerChanged();}s.dismiss();});
        if(node.breakpoint)s.action("Rimuovi questo punto","trash",3,()->{debuggerHistory();node.breakpoint=false;node.breakpointCondition=null;debuggerChanged();s.dismiss();});s.show();
    }
    private void clearBreakpoints(){
        ArrayList<Document> graphs=new ArrayList<>();graphs.add(project);if(project.functions!=null)for(FlowModel.Function f:project.functions)graphs.add(f.body);boolean found=false;for(Document g:graphs)for(Node n:g.nodes)found|=n.breakpoint||n.breakpointCondition!=null;if(!found)return;
        debuggerHistory();for(Document g:graphs)for(Node n:g.nodes){n.breakpoint=false;n.breakpointCondition=null;}debuggerChanged();showHint("Punti rimossi · Annulla per ripristinarli");
    }
    private boolean canRunToSelected(){Node node=document.find(selectedNodeId);return node!=null&&node.kind!=FlowModel.NOTE&&canvas.getSelectedIds().size()==1&&!canvas.isInteracting()&&!canvas.isConnecting()&&(runner==null||runner.state!=FlowEngine.State.WAITING&&runner.state!=FlowEngine.State.WAITING_FILE);}
    private void runToSelected(){
        if(!canRunToSelected()){showHint("Seleziona un solo blocco eseguibile. Completa prima eventuali richieste di input o file.");return;}
        String node=selectedNodeId,function=activeFunctionId;if(!runVisible)showRun(false);if(!runVisible||!ensureRunner())return;
        if(runner.state==FlowEngine.State.COMPLETED||runner.state==FlowEngine.State.ERROR){if(!resetRunner())return;}
        handler.removeCallbacks(runTick);running=false;if(!getDebugger().runTo(function,node)){refreshRun();showHint("Il blocco da raggiungere non è disponibile nella prova.");return;}
        resumeAfterInput=true;running=true;refreshRun();handler.post(runTick);
    }
    private void debugStep(FlowDebugger.Command command){
        if(!runVisible||!ensureRunner()||canvas.isInteracting())return;
        if(runner.state==FlowEngine.State.WAITING_FILE){refreshRun();return;}
        stopRun();resumeAfterInput=false;
        if(runner.state==FlowEngine.State.COMPLETED){if(!resetRunner())return;outputShown=0;outputText.setText("");}
        if(!getDebugger().begin(command))return;resumeAfterInput=true;
        if(runner.state==FlowEngine.State.WAITING){refreshRun();if(sourcePanel!=null)sourcePanel.focusRuntimeInput();else runInput.requestFocus();return;}
        int before=runner.steps;debugger.tick();running=debugger.active()&&runner.state!=FlowEngine.State.WAITING&&runner.state!=FlowEngine.State.WAITING_FILE;refreshRun();if(runner.steps>before)animateRunEdge(runner.lastEdgeId,420);if(running)handler.post(runTick);
    }
    private void showConsole(){
        if(canvas.isInteracting())return;if(!runVisible)showRun(false);if(!runVisible||!ensureRunner())return;
        if(consolePanel!=null&&consolePanel.sheet.dialog.isShowing()){consolePanel.refresh();return;}
        final boolean wasRunning=running;Ui.Sheet s=sheet("Console",null);
        FlowConsolePanel panel=new FlowConsolePanel(this,s,new FlowConsolePanel.Host(){
            public FlowEngine.Runner runner(){return MainActivity.this.runner;}
            public boolean running(){return MainActivity.this.running;}
            public String pending(){return runInput==null?"":runInput.getText().toString();}
            public void pending(String value){if(runInput!=null)runInput.setText(value);}
            public void submit(String value){if(runner==null||runner.state!=FlowEngine.State.WAITING||runInput==null)return;runInput.setText(value);submitInput();}
            public void play(){if(running){stopRun();resumeAfterInput=false;}else{if(runner!=null&&runner.state==FlowEngine.State.ERROR&&!resetRunner())return;startRun();}refreshRun();}
            public void step(){debugStep(FlowDebugger.Command.INTO);}
            public void file(){fileUi.chooseCurrent();}
            public void save(String value){stopRun();resumeAfterInput=false;refreshRun();saveTextExport(value,"Frociart_console.txt",".txt");}
            public void locate(FlowConsoleLog.Entry entry){if(runner==null||runner.consoleLog.find(entry.sequence)!=entry)return;rememberNavigation();stopRun();resumeAfterInput=false;refreshRun();activateGraph(entry.functionId,true);canvas.select(entry.nodeId);canvas.focusNodeAnimated(entry.nodeId);}
        },prefs);consolePanel=panel;s.dialog.setOnDismissListener(d->{panel.close();if(consolePanel==panel)consolePanel=null;if(activeSheet==s)activeSheet=null;});s.show();panel.refresh();if(wasRunning)startRun();
    }
    private void closeConsole(){FlowConsolePanel current=consolePanel;consolePanel=null;if(current!=null){current.close();current.sheet.dismiss();if(activeSheet==current.sheet)activeSheet=null;}}
    private void showDebugger(){
        stopRun();resumeAfterInput=false;refreshRun();Ui.Sheet s=sheet("Debugger","Avanza tra i blocchi e controlla i valori nelle chiamate.");
        s.action("Avanti entrando nelle funzioni","step",0,()->{s.dismiss();debugStep(FlowDebugger.Command.INTO);});
        s.action("Avanti senza entrare","right",0,()->{s.dismiss();debugStep(FlowDebugger.Command.OVER);});
        if(runner!=null&&runner.callDepth()>1)s.action("Torna al chiamante","undo",0,()->{s.dismiss();debugStep(FlowDebugger.Command.OUT);});
        if(runner!=null)s.action("Pausa sul blocco attuale…","pause",0,()->{String id=runner.currentNodeId;s.dismiss();editBreakpoint(id);});
        s.action(ignoreBreakpoints?"Riattiva i punti di interruzione":"Ignora i punti di interruzione","pause",0,()->{ignoreBreakpoints=!ignoreBreakpoints;getDebugger().ignoreBreakpoints=ignoreBreakpoints;s.dismiss();refreshRun();});
        s.action("Disattiva tutti i punti di interruzione","trash",3,()->{s.dismiss();clearBreakpoints();});s.show();
    }
    private void setRunner(FlowEngine.Runner next){if(runner==next)return;if(fileUi!=null)fileUi.close(runner);runner=next;if(fileUi!=null)fileUi.bind(next);}
    private boolean resetRunner(){try{boolean retained=runner.files.hasWrite();runner.reset(true);fileUi.bind(runner);if(retained)toast("Esecuzione ricominciata. La bozza è in File generati.");return true;}catch(FlowEngine.Problem ex){message("Esecuzione conservata",ex.getMessage());return false;}}
    private boolean ensureRunner(){if(canvas.isInteracting())return false;if(runner==null)try{setRunner(new FlowEngine.Runner(project));if(runner.state==FlowEngine.State.ERROR){setRunner(null);showValidation();return false;}}catch(Exception e){message("Controlla il diagramma",e.getMessage());return false;}return true;}
    private void updateRuntimeInputLabel(){if(inputLabel==null||runner==null||runner.state!=FlowEngine.State.WAITING)return;String variable=runner.inputVariable==null?"Valore":runner.inputVariable;inputLabel.setText(inlineRuntimeInput?variable:"Valore per "+variable);inputLabel.setContentDescription("Valore per "+variable);inputLabel.setTooltipText("Valore per "+variable);}
    private void placeRuntimeInputLabel(boolean compact){
        if(runtimeInputRow==null||inputLabel==null||inputArea==null||inlineRuntimeInput==compact)return;
        ViewGroup parent=(ViewGroup)inputLabel.getParent();if(parent!=null)parent.removeView(inputLabel);inlineRuntimeInput=compact;runtimeInputRow.setBaselineAligned(!compact);runInput.setPadding(runInput.getPaddingLeft(),dp(compact?6:14),runInput.getPaddingRight(),dp(compact?6:14));
        if(compact){inputLabel.setMaxWidth(dp(112));inputLabel.setMaxLines(1);inputLabel.setEllipsize(TextUtils.TruncateAt.MIDDLE);inputLabel.setGravity(Gravity.CENTER_VERTICAL|Gravity.START);Ui.pad(inputLabel,0,0,8,0);LinearLayout.LayoutParams size=new LinearLayout.LayoutParams(-2,-2);size.gravity=Gravity.CENTER_VERTICAL;runtimeInputRow.addView(inputLabel,0,size);runtimeInputGap.setVisibility(View.GONE);}
        else{inputLabel.setMaxWidth(Integer.MAX_VALUE);inputLabel.setMaxLines(2);inputLabel.setEllipsize(TextUtils.TruncateAt.END);inputLabel.setGravity(Gravity.TOP|Gravity.START);Ui.pad(inputLabel,0,0,0,0);inputArea.addView(inputLabel,0,new LinearLayout.LayoutParams(-1,-2));runtimeInputGap.setVisibility(View.VISIBLE);}
        updateRuntimeInputLabel();updateRuntimeInputError();
    }
    private void updateRuntimeInputError(){
        if(runInputError==null||runInput==null)return;CharSequence error=runInput.getError();boolean visible=inlineRuntimeInput&&runner!=null&&runner.state==FlowEngine.State.WAITING&&error!=null&&error.length()>0;
        runInputError.setVisibility(visible?View.VISIBLE:View.GONE);runInputError.setContentDescription(visible?"Leggi errore: "+error:"Leggi errore");
    }
    private void showRuntimeInputError(){
        if(runInput==null||runner==null||runner.state!=FlowEngine.State.WAITING||runInput.getError()==null)return;
        String explanation=runInput.getError().toString();Ui.dismissKeyboard(runInput);Ui.Sheet detail=new Ui.Sheet(this,"Valore non valido",null);if(activeSheet!=null&&activeSheet.dialog.isShowing())activeSheet.dismiss();activeSheet=detail;detail.dialog.setOnDismissListener(d->{if(activeSheet==detail)activeSheet=null;});
        TextView message=Ui.text(this,explanation,16,false);message.setTextIsSelectable(true);message.setTag("runtime-input-error-detail");detail.body.addView(message);detail.show();
    }
    private void cancelRuntimeInputReveal(){inputRevealEpoch++;if(inputRevealListener!=null&&inputRevealObserver!=null&&inputRevealObserver.isAlive())inputRevealObserver.removeOnPreDrawListener(inputRevealListener);inputRevealListener=null;inputRevealObserver=null;}
    private static android.graphics.Rect screenBounds(View view){int[] point=new int[2];view.getLocationOnScreen(point);return new android.graphics.Rect(point[0],point[1],point[0]+view.getWidth(),point[1]+view.getHeight());}
    private void revealRuntimeInputAfterLayout(){
        cancelRuntimeInputReveal();if(!compactRunLayout||inputArea==null||runScroll==null||runInput==null||inputLabel==null||runInputSend==null)return;
        final int epoch=inputRevealEpoch;final Document expectedProject=project;final long revision=documentRevision;final LinearLayout expectedRoot=root,area=inputArea;final ScrollView scroll=runScroll;final EditText field=runInput;final TextView label=inputLabel;final Ui.Button send=runInputSend;final FlowEngine.Runner expectedRunner=runner;final String request=inputRequest;
        ViewTreeObserver observer=area.getViewTreeObserver();inputRevealObserver=observer;inputRevealListener=new ViewTreeObserver.OnPreDrawListener(){public boolean onPreDraw(){
            boolean current=epoch==inputRevealEpoch&&!destroyed&&!isFinishing()&&compactRunLayout&&runVisible&&keyboardVisible&&root==expectedRoot&&project==expectedProject&&documentRevision==revision&&inputArea==area&&runScroll==scroll&&runInput==field&&runner==expectedRunner&&runner!=null&&runner.state==FlowEngine.State.WAITING&&Objects.equals(inputRequest,request)&&area.getVisibility()==View.VISIBLE;
            if(current&&(expectedRoot.isLayoutRequested()||area.isLayoutRequested()||scroll.isLayoutRequested()))return true;
            if(observer.isAlive())observer.removeOnPreDrawListener(this);if(inputRevealListener==this){inputRevealListener=null;inputRevealObserver=null;}
            if(current&&scroll.getChildCount()>0){android.graphics.Rect viewport=screenBounds(scroll),frame=new android.graphics.Rect();expectedRoot.getWindowVisibleDisplayFrame(frame);viewport.top+=scroll.getPaddingTop();viewport.bottom-=scroll.getPaddingBottom();if(viewport.intersect(frame)){android.graphics.Rect group=screenBounds(field);group.union(screenBounds(label));group.union(screenBounds(send));int delta=group.top<viewport.top?group.top-viewport.top:group.bottom>viewport.bottom?group.bottom-viewport.bottom:0;int maximum=Math.max(0,scroll.getChildAt(0).getHeight()-scroll.getHeight()+scroll.getPaddingTop()+scroll.getPaddingBottom());int target=Math.max(0,Math.min(maximum,scroll.getScrollY()+delta));if(target!=scroll.getScrollY())scroll.scrollTo(scroll.getScrollX(),target);}}
            return true;
        }};observer.addOnPreDrawListener(inputRevealListener);area.invalidate();
    }
    private void updateRunKeyboardLayout(){
        boolean compact=runVisible&&landscapeIme,changed=compact!=compactRunLayout;compactRunLayout=compact;
        if(workspace!=null)workspace.setInputPriority(compact);
        if(projectHeader!=null)projectHeader.setVisibility(compact?View.GONE:View.VISIBLE);if(projectIdentityArea!=null)projectIdentityArea.setVisibility(runVisible&&keyboardVisible?View.GONE:View.VISIBLE);
        if(runHeader!=null)runHeader.setVisibility(compact?View.GONE:View.VISIBLE);
        if(assistBar!=null)assistBar.setVisibility(runVisible?View.GONE:View.VISIBLE);if(editorToolsContainer!=null)editorToolsContainer.setVisibility(runVisible?View.GONE:View.VISIBLE);if(graphToolbar!=null)graphToolbar.setVisibility(compact||graphToolbar==editorToolsContainer&&runVisible?View.GONE:View.VISIBLE);if(statusBar!=null)statusBar.setVisibility(runVisible||compactHeight()||getResources().getConfiguration().screenWidthDp<600?View.GONE:View.VISIBLE);refreshPracticeBar();
        placeRuntimeInputLabel(compact);if(runPanel!=null)Ui.pad(runPanel,12,compact?0:6,12,compact?0:10);
        if(changed){if(compact)revealRuntimeInputAfterLayout();else cancelRuntimeInputReveal();}
    }
    private void showRun(boolean auto){
        openGuard.cancel();
        if(!ensureRunner())return;if(activeSheet!=null)activeSheet.dismiss();
        if(getDebugger().suspendedRunTo()){running=false;resumeAfterInput=false;handler.removeCallbacks(runTick);canvas.clearRunAnimation();}else stopRun();canvas.cancelConnection();canvas.setReadOnly(true);runVisible=true;refreshHeader();outputShown=0;runPanel.removeAllViews();Ui.pad(runPanel,12,6,12,10);
        boolean splitRunHeader=getResources().getConfiguration().fontScale>=1.5f&&getResources().getConfiguration().screenWidthDp<360;LinearLayout head=splitRunHeader?Ui.column(this):Ui.actionRow(this);runHeader=head;TextView title=Ui.text(this,"Prova",17,true);title.setTag("runtime-heading");title.setGravity(Gravity.CENTER_VERTICAL);LinearLayout headActions;if(splitRunHeader){head.addView(title,new LinearLayout.LayoutParams(-1,-2));Ui.gap(head,8);headActions=Ui.actionRow(this);headActions.setGravity(Gravity.END|Gravity.CENTER_VERTICAL);head.addView(headActions,new LinearLayout.LayoutParams(-1,-2));}else{head.addView(title,Ui.weight(this,48));headActions=head;}
        Ui.Button reset=iconButton("undo","Ricomincia esecuzione",()->{stopRun();resumeAfterInput=false;if(!resetRunner())return;canvas.clearRunAnimation();outputShown=0;outputText.setText("");runInput.setText("");refreshRun();canvas.frameNodeAnimated(runner.currentNodeId);});headActions.addView(reset,Ui.lp(this,48,48));
        runDetailsButton=new Ui.Button(this,"Dettagli",null,0);runDetailsButton.setTextSize(12);runDetailsButton.setPadding(dp(8),dp(8),dp(8),dp(8));runDetailsButton.setOnClickListener(v->{runExpanded=!runExpanded;updateRunExpansion();});String detailsLabel=getResources().getConfiguration().fontScale>=1.5f?"Dati":"Dettagli";int detailsWidth=Math.max(dp(72),(int)Math.ceil(Math.max(runDetailsButton.getPaint().measureText(detailsLabel),runDetailsButton.getPaint().measureText("Meno")))+runDetailsButton.getPaddingLeft()+runDetailsButton.getPaddingRight()+dp(4));headActions.addView(runDetailsButton,new LinearLayout.LayoutParams(detailsWidth,dp(48)));
        headActions.addView(iconButton("close","Chiudi prova e torna al diagramma",()->closeRun()),Ui.lp(this,48,48));runPanel.addView(head);
        runScroll=new ScrollView(this);runScroll.setFillViewport(false);runScroll.setVerticalScrollBarEnabled(false);LinearLayout body=Ui.column(this);runScroll.addView(body,new ScrollView.LayoutParams(-1,-2));runPanel.addView(runScroll,new LinearLayout.LayoutParams(-1,0,1));
        runStatus=Ui.text(this,"",18,true);body.addView(runStatus);runDetails=Ui.note(this,"");Ui.pad(runDetails,0,4,0,8);body.addView(runDetails);body.addView(fileUi.createAction(),new LinearLayout.LayoutParams(-1,dp(52)));
        inlineRuntimeInput=false;inputArea=Ui.column(this);inputArea.setVisibility(View.GONE);inputLabel=Ui.text(this,"Valore da inserire",14,true);inputLabel.setTag("runtime-input-label");inputArea.addView(inputLabel,new LinearLayout.LayoutParams(-1,-2));runtimeInputGap=new View(this);inputArea.addView(runtimeInputGap,Ui.lp(this,1,6));runtimeInputRow=Ui.actionRow(this);LinearLayout inputRow=runtimeInputRow;inputArea.addView(inputRow,new LinearLayout.LayoutParams(-1,-2));runInput=Ui.edit(this,"","Valore di input",false);runInput.setImeOptions(android.view.inputmethod.EditorInfo.IME_ACTION_SEND|android.view.inputmethod.EditorInfo.IME_FLAG_NO_EXTRACT_UI);runInput.setId(View.generateViewId());inputLabel.setLabelFor(runInput.getId());inputRow.addView(runInput,Ui.weight(this,-2));
        runInputError=new Ui.Button(this,"Leggi errore",null,0);runInputError.setTextColor(Ui.danger());runInputError.setSingleLine(true);runInputError.setEllipsize(null);runInputError.setMinimumHeight(dp(52));runInputError.setTag("runtime-input-error-action");runInputError.setVisibility(View.GONE);runInputError.setOnClickListener(v->showRuntimeInputError());inputRow.addView(runInputError,new LinearLayout.LayoutParams(-2,-2));
        runInputSend=iconButton("right","Invia valore",()->submitInput());Ui.Button send=runInputSend;send.setTone(1);inputRow.addView(send,Ui.lp(this,52,52));Ui.inputSubmission(runInput,this::submitInput);
        body.addView(inputArea);Ui.gap(body,6);Ui.Button console=new Ui.Button(this,"Console · letture e stampe","more",0);console.setTag("runtime-console");console.setSingleLine(false);console.setMaxLines(3);console.setOnClickListener(v->showConsole());body.addView(console,new LinearLayout.LayoutParams(-1,-2));
        buildRuntimeTurtle(body);
        Ui.gap(body,12);TextView resultLabel=Ui.text(this,"Risultato",11,true);resultLabel.setTextColor(Ui.muted());Ui.pad(resultLabel,0,2,0,4);body.addView(resultLabel);
        outputText=Ui.text(this,"",16,false);outputText.setTypeface(android.graphics.Typeface.MONOSPACE);outputText.setTextIsSelectable(true);outputText.setContentDescription("Risultato dell'esecuzione");Ui.pad(outputText,12,10,12,10);Ui.card(outputText,Ui.bg(),14);body.addView(outputText,new LinearLayout.LayoutParams(-1,-2));
        boolean stackData=getResources().getConfiguration().screenWidthDp<360||getResources().getConfiguration().fontScale>=1.5f;LinearLayout dataTools=stackData?Ui.column(this):Ui.actionRow(this);Ui.Button inspect=new Ui.Button(this,"Variabili",null,0);inspect.setTag("runtime-variables");inspect.setOnClickListener(v->showVariables());dataTools.addView(inspect,stackData?new LinearLayout.LayoutParams(-1,-2):Ui.weight(this,48));if(stackData)Ui.gap(dataTools,8);Ui.Button capture=new Ui.Button(this,"Immagine con dati","export",0);capture.setTextSize(13);capture.setMaxLines(2);capture.setSingleLine(false);capture.setOnClickListener(v->showExecutionSnapshot());dataTools.addView(capture,stackData?new LinearLayout.LayoutParams(-1,-2):Ui.weight(this,48));Ui.gap(body,16);body.addView(dataTools);Ui.gap(body,8);Ui.Button stack=new Ui.Button(this,"Chiamate e variabili locali","call",0);stack.setOnClickListener(v->showCallStack());body.addView(stack,new LinearLayout.LayoutParams(-1,dp(48)));
        Ui.gap(body,8);Ui.Button debugButton=new Ui.Button(this,"Debugger · passi e pause","pause",0);debugButton.setTag("debugger");debugButton.setOnClickListener(v->showDebugger());body.addView(debugButton,new LinearLayout.LayoutParams(-1,dp(48)));
        Ui.gap(body,8);Ui.Button sourceButton=new Ui.Button(this,"Segui il codice Python","edit",0);sourceButton.setOnClickListener(v->showPythonSource());body.addView(sourceButton,new LinearLayout.LayoutParams(-1,dp(48)));
        Ui.gap(body,8);Ui.Button recovery=new Ui.Button(this,"File generati e recupero","save",0);recovery.setTag("generated-files");recovery.setOnClickListener(v->{stopRun();resumeAfterInput=false;refreshRun();fileUi.showRecovery();});body.addView(recovery,new LinearLayout.LayoutParams(-1,dp(48)));
        Ui.gap(body,16);fieldLabel(body,"VARIABILI");variablesLabel=(TextView)body.getChildAt(body.getChildCount()-1);variablesText=Ui.text(this,"",17,false);variablesText.setTypeface(android.graphics.Typeface.MONOSPACE);variablesText.setTextIsSelectable(true);Ui.pad(variablesText,12,10,12,10);Ui.card(variablesText,Ui.bg(),14);body.addView(variablesText);
        Ui.gap(body,16);runMore=Ui.column(this);
        LinearLayout options=Ui.row(this);TextView speedLabel=Ui.note(this,"Velocità");options.addView(speedLabel,Ui.weight(this,48));speedLabel.setGravity(Gravity.CENTER_VERTICAL);
        Ui.Button velocity=new Ui.Button(this,speed==0?"Lenta":speed==1?"Normale":"Rapida",null,0);fitSmallLabel(velocity);velocity.setContentDescription("Cambia velocità esecuzione");velocity.setOnClickListener(v->{speed=(speed+1)%3;velocity.setLabel(speed==0?"Lenta":speed==1?"Normale":"Rapida");});options.addView(velocity,Ui.lp(this,116,48));Ui.gap(runMore,6);runMore.addView(options);body.addView(runMore);
        LinearLayout controls=Ui.row(this);runControls=controls;Ui.pad(controls,0,12,0,0);
        boolean wrapRunControls=getResources().getConfiguration().fontScale>=1.5f&&getResources().getConfiguration().screenWidthDp<360;int runControlHeight=wrapRunControls?84:52;
        stepButton=new Ui.Button(this,"Avanti","step",0);fitSmallLabel(stepButton);stepButton.setOnClickListener(v->debugStep(FlowDebugger.Command.INTO));controls.addView(stepButton,Ui.weight(this,runControlHeight));controls.addView(new View(this),Ui.lp(this,8,1));
        playPause=new Ui.Button(this,"Esegui","play",4);fitSmallLabel(playPause);playPause.setOnClickListener(v->{if(running){stopRun();resumeAfterInput=false;}else startRun();refreshRun();});if(wrapRunControls){playPause.setSingleLine(false);playPause.setMaxLines(2);}controls.addView(playPause,Ui.weight(this,runControlHeight));runPanel.addView(controls);
        runPanel.setVisibility(View.VISIBLE);assistBar.setVisibility(View.GONE);if(editorToolsContainer!=null)editorToolsContainer.setVisibility(View.GONE);Ui.enter(runPanel,0);actionDock.setVisibility(View.GONE);dock.setVisibility(View.GONE);hint.setVisibility(View.GONE);selectionBar.setVisibility(View.GONE);
        updateRunExpansion();refreshRun();updateRunKeyboardLayout();canvas.post(()->{if(runVisible&&runner!=null)canvas.frameNodeAnimated(runner.currentNodeId);});if(auto&&runner.state!=FlowEngine.State.WAITING&&runner.state!=FlowEngine.State.COMPLETED)startRun();
    }
    private void updateRunExpansion(){
        if(runMore==null)return;runMore.setVisibility(runExpanded?View.VISIBLE:View.GONE);runDetailsButton.setChosen(runExpanded);runDetailsButton.setLabel(runExpanded?"Meno":getResources().getConfiguration().fontScale>=1.5f?"Dati":"Dettagli");workspace.panelExpanded=runExpanded;workspace.requestLayout();
        runDetailsButton.setContentDescription(runExpanded?"Compatta il pannello della prova":"Espandi la prova e mostra la velocità");outputText.setMaxLines(runExpanded?Integer.MAX_VALUE:3);outputText.setEllipsize(runExpanded?null:TextUtils.TruncateAt.END);
    }
    private void closeRun(){
        closeConsole();closeVariables();
        stopRun();if(!runVisible)return;
        Ui.dismissKeyboard(runInput);
        runVisible=false;resumeAfterInput=false;if(projectIdentityArea!=null)projectIdentityArea.setVisibility(View.VISIBLE);refreshHeader();canvas.setReadOnly(false);runPanel.setVisibility(View.GONE);assistBar.setVisibility(View.VISIBLE);if(editorToolsContainer!=null)editorToolsContainer.setVisibility(View.VISIBLE);Ui.enter(assistBar,0);actionDock.setVisibility(View.VISIBLE);hint.setVisibility(View.VISIBLE);canvas.setHighlight(null);onSelectionChanged();showHint("Trascina per spostare · pizzica per lo zoom");
    }
    private void startRun(){if(runner==null||!runVisible||canvas.isInteracting())return;if(runner.state==FlowEngine.State.COMPLETED){if(!resetRunner())return;outputShown=0;if(outputText!=null)outputText.setText("");}if(runner.state==FlowEngine.State.ERROR||runner.state==FlowEngine.State.WAITING||runner.state==FlowEngine.State.WAITING_FILE)return;if(!getDebugger().resumeRunTo())debugger.begin(FlowDebugger.Command.CONTINUE);running=true;resumeAfterInput=true;handler.removeCallbacks(runTick);handler.post(runTick);}
    private void stopRun(){running=false;handler.removeCallbacks(runTick);if(getDebugger()!=null)debugger.pause();if(canvas!=null)canvas.clearRunAnimation();}
    private long runInterval(){return speed==0?900:speed==1?450:16;}
    private void animateRunEdge(String edgeId,long duration){
        if(edgeId==null||runner==null||!runVisible)return;
        for(Edge e:document.edges)if(e.id.equals(edgeId)){canvas.animateRunStep(e.from,e.to,e.branch,duration);return;}
    }
    private void advanceRun(){
        if(!running||runner==null||!runVisible)return;
        if(canvas.isInteracting()){handler.postDelayed(runTick,32);return;}
        FlowDebugger control=getDebugger();boolean fastDebug=control.mode()==FlowDebugger.Command.OVER||control.mode()==FlowDebugger.Command.OUT||control.mode()==FlowDebugger.Command.TO_BLOCK;
        long until=System.nanoTime()+4000000L;int max=speed==2||fastDebug?160:1;String lastVisualEdge=null;
        for(int i=0;i<max;i++){int before=runner.steps;control.tick();if(runner.steps>before&&runner.lastEdgeId!=null)lastVisualEdge=runner.lastEdgeId;
            if(!control.active()||runner.state==FlowEngine.State.WAITING||runner.state==FlowEngine.State.WAITING_FILE||runner.state==FlowEngine.State.COMPLETED||runner.state==FlowEngine.State.ERROR){running=false;break;}if(System.nanoTime()>=until)break;
        }
        refreshRun();if(control.atTarget()){canvas.clearRunAnimation();final FlowEngine.Runner owner=runner;final String target=runner.currentNodeId;canvas.post(()->{if(runVisible&&runner==owner&&getDebugger().atTarget()&&target.equals(runner.currentNodeId))canvas.frameNodeAnimated(target);});}else animateRunEdge(lastVisualEdge,speed==0?650:speed==1?340:80);
        if(running)handler.postDelayed(runTick,fastDebug?16:runInterval());
    }
    private void submitInput(){
        if(runner==null||runner.state!=FlowEngine.State.WAITING)return;
        if(runner.submitInput(runInput.getText().toString())){
            Ui.dismissKeyboard(runInput);runInput.setText("");runInput.setError(null);inputRequest=null;
            getDebugger().inputProvided();running=resumeAfterInput&&debugger.active();handler.removeCallbacks(runTick);refreshRun();animateRunEdge(runner.lastEdgeId,speed==0?650:speed==1?340:80);
            if(running)handler.postDelayed(runTick,runInterval());
        }else{runInput.setError(runner.errorMessage==null?"Valore non valido.":runner.errorMessage);refreshRun();}
    }
    private void refreshRun(){
        if(runner==null||!runVisible)return;activateGraph(runner.currentFunctionId,true);FlowEngine.State state=runner.state;
        if(variablesLabel!=null)variablesLabel.setText("VARIABILI · "+runner.currentFunctionName);
        runStatus.setText(state==FlowEngine.State.COMPLETED?"Esecuzione completata":state==FlowEngine.State.ERROR?"C'è un problema":state==FlowEngine.State.WAITING?"Inserisci un valore":state==FlowEngine.State.WAITING_FILE?"Scegli il file":running?"In esecuzione":getDebugger().atTarget()?"Blocco raggiunto":getDebugger().atBreakpoint()?"Punto di interruzione":"In pausa");
        String steps=runner.steps+(runner.steps==1?" passo":" passi");
        String detail=state==FlowEngine.State.ERROR?runner.errorMessage:state==FlowEngine.State.WAITING?runner.inputPrompt:state==FlowEngine.State.WAITING_FILE?runner.lastMessage:state==FlowEngine.State.COMPLETED?(getDebugger().targetNotReached()?getDebugger().message()+" · "+steps:steps):(getDebugger().message()==null?runner.lastMessage:debugger.message())+" · "+steps;
        runDetails.setText(detail==null?"":(speed==2&&running?"Passi raggruppati · ":"")+detail);
        boolean wasWaiting=inputArea.getVisibility()==View.VISIBLE;
        inputArea.setVisibility(state==FlowEngine.State.WAITING?View.VISIBLE:View.GONE);runControls.setVisibility(state==FlowEngine.State.WAITING?View.GONE:View.VISIBLE);
        if(state==FlowEngine.State.WAITING&&!wasWaiting)runScroll.post(()->{if(runVisible&&runner!=null&&runner.state==FlowEngine.State.WAITING){if(compactRunLayout)revealRuntimeInputAfterLayout();else runScroll.scrollTo(0,runDetails.getTop());}});
        if(state==FlowEngine.State.WAITING){String request=runner.currentNodeId+":"+runner.steps+":"+runner.inputVariable;if(!Objects.equals(inputRequest,request)){runInput.setError(null);inputRequest=request;}String variable=runner.inputVariable==null?"Valore":runner.inputVariable;updateRuntimeInputLabel();runInput.setHint("Scrivi il valore");runInput.setContentDescription("Valore per "+variable);}
        updateRuntimeInputError();
        StringBuilder text=new StringBuilder();
        if(outputShown>runner.output.size()){outputShown=0;outputText.setText("");}
        if(outputShown==0&&runner.output.size()>0)outputText.setText("");
        for(int i=outputShown;i<runner.output.size();i++){if(i>0&&runner.outputNewlines.get(i-1))text.append('\n');text.append(runner.output.get(i));}
        if(text.length()>0)outputText.append(text);outputShown=runner.output.size();if(outputShown==0)outputText.setText("Il risultato apparirà qui.");
        String value=FlowExecutionSnapshot.summary(runner.variables,runner.variableTypes());if(!value.contentEquals(variablesText.getText()))variablesText.setText(value);
        playPause.setLabel(running?"Pausa":state==FlowEngine.State.ERROR?"Interrotta":state==FlowEngine.State.COMPLETED?"Riprova":runner.steps>0?"Riprendi":"Esegui");playPause.setIcon(compactLabels()?null:running?"pause":"play");
        refreshTurtle();fileUi.refresh();playPause.setEnabled(state!=FlowEngine.State.WAITING_FILE&&state!=FlowEngine.State.WAITING&&state!=FlowEngine.State.ERROR);stepButton.setEnabled(!running&&state!=FlowEngine.State.WAITING_FILE&&state!=FlowEngine.State.WAITING&&state!=FlowEngine.State.ERROR);canvas.setHighlight(runner.currentNodeId);
        refreshSourcePanel();if(consolePanel!=null)consolePanel.refresh();if(variablePanel!=null)variablePanel.refresh();
    }
    private boolean currentBackup(){return !destroyed&&!isFinishing()&&Objects.equals(projectId,backupProject)&&documentRevision==backupRevision;}
    private void showBackup(){
        Ui.Sheet s=sheet("Backup dei progetti","Una copia dei progetti salvati, in un file scelto da te.");
        s.paragraph("Il backup include diagrammi, funzioni, note e dichiarazioni già salvati. Le bozze di testo non ancora applicate e le preferenze del telefono non sono incluse. Conserva il file in una posizione sicura: contiene i tuoi progetti.");
        if(store.backupRecoveryWarning()!=null)s.paragraph(store.backupRecoveryWarning());
        s.action("Crea backup","save",1,()->{s.dismiss();createBackup();});
        s.action("Ripristina da backup","folder",0,()->{s.dismiss();openBackup();});
        s.paragraph("Il ripristino mostra prima l’elenco e crea nuove copie. I progetti presenti e quello aperto non vengono sostituiti.");if(backupBusy)s.paragraph("Un’operazione di backup è già in corso. Attendi o chiudi il selettore dei file.");s.show();
    }
    private boolean beginBackup(){if(backupBusy){toast("Un’operazione di backup è già in corso.");return false;}if(!saveNow())return false;backupBusy=true;backupProject=projectId;backupRevision=documentRevision;return true;}
    private void createBackup(){
        if(!beginBackup())return;toast("Preparo il backup dei progetti salvati…");
        io.execute(()->{try{FlowBackup.Archive archive=store.createBackup();handler.post(()->{if(!currentBackup()){backupBusy=false;if(!destroyed)message("Backup annullato","Il progetto è cambiato durante la preparazione. Crea un nuovo backup per includere l’ultimo salvataggio.");return;}pendingBackup=archive;Intent intent=new Intent(Intent.ACTION_CREATE_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType("application/zip");intent.putExtra(Intent.EXTRA_TITLE,"Frociart_Progetti_"+new SimpleDateFormat("yyyy-MM-dd",Locale.ROOT).format(new Date())+".frociartbackup");try{startActivityForResult(intent,BACKUP_CREATE);}catch(ActivityNotFoundException ex){pendingBackup=null;backupBusy=false;message("Selettore non disponibile","Non riesco a scegliere dove salvare il backup.");}});}catch(Exception ex){handler.post(()->{backupBusy=false;if(!destroyed)message("Backup non creato",ex.getMessage());});}});
    }
    private void openBackup(){if(!beginBackup())return;Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType("*/*");try{startActivityForResult(intent,BACKUP_OPEN);}catch(ActivityNotFoundException ex){backupBusy=false;message("Selettore non disponibile","Non riesco a scegliere il file di backup.");}}
    private void backupResult(int request,int result,Intent data){
        if(result!=RESULT_OK||data==null||data.getData()==null){pendingBackup=null;backupBusy=false;return;}
        final Uri uri=data.getData();if(!currentBackup()){pendingBackup=null;backupBusy=false;message("Operazione annullata","Il progetto è cambiato. Riapri Backup dei progetti per riprovare.");return;}
        if(request==BACKUP_CREATE){final FlowBackup.Archive archive=pendingBackup;pendingBackup=null;if(archive==null){backupBusy=false;message("Backup da ricreare","La preparazione non è più disponibile. Crea un nuovo backup; i progetti sul telefono sono conservati.");return;}
            io.execute(()->{try{OutputStream output=getContentResolver().openOutputStream(uri,"w");if(output==null)throw new IOException("La destinazione non è disponibile.");FlowBackup.write(output,archive);handler.post(()->{backupBusy=false;if(!destroyed)message("Backup salvato",archive.count()+(archive.count()==1?" progetto salvato":" progetti salvati")+" nel file scelto. Per controllarlo puoi riaprirlo con Ripristina da backup e leggere l’anteprima senza importare.");});}catch(Exception ex){handler.post(()->{backupBusy=false;if(!destroyed)message("Backup non salvato",ex.getMessage()+" Il file scelto potrebbe essere incompleto; i progetti sul telefono sono conservati.");});}});
        }else io.execute(()->{try{InputStream input=getContentResolver().openInputStream(uri);if(input==null)throw new IOException("Il file non è disponibile.");FlowBackup.Archive archive=FlowBackup.read(input);handler.post(()->{if(!currentBackup()){backupBusy=false;if(!destroyed)message("Anteprima annullata","Il progetto è cambiato. Riapri il backup per confermare il ripristino.");return;}previewBackup(archive);});}catch(Exception ex){handler.post(()->{backupBusy=false;if(!destroyed)message("Backup non importato",ex.getMessage()+" Nessun progetto è stato sostituito.");});}});
    }
    private void previewBackup(FlowBackup.Archive archive){
        Ui.Sheet s=sheet("Ripristina backup",archive.count()+(archive.count()==1?" progetto verificato":" progetti verificati")+" · "+archive.totalBytes()+" byte di dati");s.paragraph("Verranno create nuove copie con i titoli originali. Il progetto aperto e quelli già presenti restano al loro posto.");for(FlowBackup.ProjectInfo info:archive.projects())s.paragraph(info.title+" · "+info.nodes+(info.nodes==1?" blocco":" blocchi"));
        final boolean[] confirmed={false};s.primary("Importa "+archive.count()+(archive.count()==1?" copia":" copie"),"save",()->{if(confirmed[0])return;if(!currentBackup()){backupBusy=false;s.dismiss();message("Ripristino annullato","Il progetto è cambiato. Riapri il backup per confermare l’elenco.");return;}confirmed[0]=true;s.dismiss();
            io.execute(()->{try{List<FlowStore.Entry> restored=store.restoreBackup(archive);handler.post(()->{backupBusy=false;if(!destroyed)message("Backup ripristinato",restored.size()+(restored.size()==1?" nuova copia aggiunta":" nuove copie aggiunte")+" a I miei progetti. Il progetto attualmente aperto è rimasto invariato.");});}catch(Exception ex){handler.post(()->{backupBusy=false;if(!destroyed)message("Ripristino non completato",ex.getMessage()+" I progetti precedenti sono conservati.");});}});});
        s.action("Annulla ripristino","close",0,s::dismiss);s.dialog.setOnDismissListener(d->{if(!confirmed[0])backupBusy=false;if(activeSheet==s)activeSheet=null;});s.show();
    }
    private void openDocument(){if(!saveNow())return;pendingOpen=openGuard.begin(project,documentRevision,projectId);Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("*/*");try{startActivityForResult(intent,OPEN);}catch(ActivityNotFoundException e){pendingOpen=null;message("File non disponibili","Su questo dispositivo non trovo un selettore di documenti.");}}
    private void importUri(Uri uri){
        final FlowOpenGuard.Ticket request=openGuard.begin(project,documentRevision,projectId);
        toast("Apro il progetto…");
        io.execute(()->{try{Document incoming=store.importUri(uri);handler.post(()->{
            if(destroyed||!openGuard.current(request,project,documentRevision,projectId))return;
            if(switchDocument(incoming,null,false)){lastImportedUri=uri;toast("Progetto importato");}
        });}catch(Exception e){handler.post(()->{if(!destroyed&&openGuard.current(request,project,documentRevision,projectId))message("Non riesco ad aprire il file",e.getMessage());});}});
    }
    private void showPseudocode(){
        stopRun();canvas.cancelConnection();
        if(!projectId.equals(pseudocodeProject)){pseudocodeProject=projectId;pseudocodeDraft=null;}
        Ui.Sheet s=sheet("Pseudocodice","Dal diagramma al testo, e ritorno.");
        pseudocodePanel=new FlowPseudocodePanel(this,s,project,pseudocodeDraft,value->pseudocodeDraft=value,
            generated->{FlowAutoLayout.apply(generated);try{FlowCodec.check(generated);}catch(IllegalArgumentException e){throw new IllegalArgumentException("Il diagramma è troppo esteso. Dividi il codice in progetti più piccoli.");}if(!switchDocument(generated,null,false))return false;toast("Diagramma creato: verifica ed esegui per provarlo.");return true;},
            ()->{Intent i=new Intent(Intent.ACTION_OPEN_DOCUMENT);i.addCategory(Intent.CATEGORY_OPENABLE);i.setType("text/*");try{startActivityForResult(i,PSEUDO_OPEN);}catch(ActivityNotFoundException e){pseudocodePanel.problem("Selettore non disponibile. Puoi incollare il testo nell'editor.");}},this::savePseudocode);
    }
    private void showFlowgorithmExport(){showFlowgorithmExport(project,java.util.Collections.emptyMap());}
    private void showFlowgorithmExport(Document exportSnapshot,java.util.Map<String,Integer> selectedTypes){showFlowgorithmExport(exportSnapshot,selectedTypes,java.util.Collections.emptyMap());}
    private void showFlowgorithmExport(Document exportSnapshot,java.util.Map<String,Integer> selectedTypes,java.util.Map<String,Integer> selectedColors){
        stopRun();canvas.cancelConnection();
        Ui.Sheet panel=sheet("Esporta per Flowgorithm","Apri una copia in Flowgorithm. L’originale .frociart resta intatto; l’aspetto segue Flowgorithm.");
        flowgorithmPanel=new FlowgorithmExportPanel(this,panel,exportSnapshot,io,text->saveTextExport(text,"Programma.fprg",".fprg"),
            (function,node)->{rememberNavigation();stopRun();if(runVisible)closeRun();activateGraph(function,false);canvas.focusNode(node);canvas.select(node);},selectedTypes,selectedColors);
        panel.dialog.setOnDismissListener(d->{if(flowgorithmPanel!=null&&flowgorithmPanel.sheet==panel)flowgorithmPanel=null;if(activeSheet==panel)activeSheet=null;});
    }
    private void showCodeTemplates(){
        stopRun();canvas.cancelConnection();if(activeSheet!=null&&activeSheet.dialog.isShowing())activeSheet.dismiss();if(templateLibrary!=null)templateLibrary.closeForDestroy();
        templateLibrary=new FlowTemplateLibrary(this,io,project,target->{templateLibrary=null;showSourceTarget(target);},(text,name)->saveTextExport(text,name,name.substring(name.lastIndexOf('.'))),this::pickCodeTemplate);templateLibrary.show();
    }
    private void pickCodeTemplate(){pendingTemplateImport=templateLibrary;Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("*/*");try{startActivityForResult(intent,TEMPLATE_OPEN);}catch(ActivityNotFoundException error){pendingTemplateImport=null;if(templateLibrary!=null)templateLibrary.importCancelled();message("Selettore non disponibile","Non trovo un'app per scegliere il file .frotemplate o .fpgt. Puoi creare un modello e incollare il corpo nell'editor.");}}
    private void showPythonSource(){showSource(false);}
    private void showSource(boolean javaCode){showSourceTarget(javaCode?FlowSourceTarget.JAVA:FlowSourceTarget.PYTHON);}
    private void showSourceTarget(FlowSourceTarget target){
        stopRun();canvas.cancelConnection();final Document sourceProject=project;final long sourceRevision=documentRevision;
        Ui.Sheet sourceSheet=sheet(target==null?"Pseudocodice collegato":target.template==null?"Codice "+target.name:"Modello di codice",target==null?"Il testo segue i blocchi del diagramma.":"Dal flowchart al programma.");
        sourcePanel=new FlowSourcePanel(this,sourceSheet,project,io,text->{stopRun();resumeAfterInput=false;refreshRun();saveTextExport(text,target==null?"Pseudocodice.txt":target.fileName(text),target==null?".txt":"."+target.extension);},
            (function,node)->{if(project!=sourceProject||documentRevision!=sourceRevision){toast("Il progetto è cambiato. Riapri il codice per ritrovare il blocco.");return;}rememberNavigation();stopRun();if(runVisible)closeRun();activateGraph(function,false);canvas.focusNode(node);canvas.select(node);},
            ()->beginSourceExecution(sourceSheet,sourceProject,sourceRevision),
            runVisible?()->debugStep(FlowDebugger.Command.INTO):null,
            ()->{if(project!=sourceProject||documentRevision!=sourceRevision)return;if(runner!=null&&runner.state==FlowEngine.State.WAITING){if(sourcePanel!=null)sourcePanel.focusRuntimeInput();return;}if(runner!=null&&runner.state==FlowEngine.State.WAITING_FILE){fileUi.chooseCurrent();refreshSourcePanel();return;}if(running){stopRun();resumeAfterInput=false;}else startRun();refreshRun();},this::refreshSourcePanel,target);
        final FlowSourcePanel shown=sourcePanel;
        if(runVisible)attachSourceExecution(shown,sourceProject,sourceRevision);
        sourceSheet.dialog.setOnDismissListener(d->{shown.close();if(sourcePanel==shown){sourcePanel=null;stopRun();resumeAfterInput=false;if(runVisible)refreshRun();}if(activeSheet==sourceSheet)activeSheet=null;});
    }
    private void beginSourceExecution(Ui.Sheet sourceSheet,Document sourceProject,long sourceRevision){
        if(sourcePanel==null||sourcePanel.sheet!=sourceSheet||project!=sourceProject||documentRevision!=sourceRevision)return;FlowSourcePanel shown=sourcePanel;
        if(activeSheet==sourceSheet)activeSheet=null;showRun(false);
        if(!runVisible){sourceSheet.dismiss();return;}
        if(activeSheet==null&&sourceSheet.dialog.isShowing())activeSheet=sourceSheet;
        if(sourcePanel==shown){attachSourceExecution(shown,sourceProject,sourceRevision);refreshSourcePanel();}
    }
    private void attachSourceExecution(FlowSourcePanel shown,Document sourceProject,long sourceRevision){
        shown.prepareExecutionControls(()->{if(project==sourceProject&&documentRevision==sourceRevision)debugStep(FlowDebugger.Command.INTO);},()->{if(project!=sourceProject||documentRevision!=sourceRevision)return;if(runner!=null&&runner.state==FlowEngine.State.WAITING){shown.focusRuntimeInput();return;}if(runner!=null&&runner.state==FlowEngine.State.WAITING_FILE){fileUi.chooseCurrent();refreshSourcePanel();return;}if(running){stopRun();resumeAfterInput=false;}else startRun();refreshRun();});
        shown.bindExecution(new FlowSourceExecution.Host(){
            public FlowEngine.Runner runner(){return runner;}
            public boolean running(){return running;}
            public boolean valid(){return !destroyed&&project==sourceProject&&documentRevision==sourceRevision&&runVisible;}
            public boolean fileBusy(){return fileUi.isBusy();}
            public String pending(){return runInput==null?"":runInput.getText().toString();}
            public void pending(String value){if(valid()&&runInput!=null)runInput.setText(value);}
            public void submit(String value){if(!valid()||runner==null||runner.state!=FlowEngine.State.WAITING)return;runInput.setText(value);submitInput();}
            public void pause(){stopRun();resumeAfterInput=false;refreshRun();}
            public void reset(){if(!valid())return;stopRun();resumeAfterInput=false;if(resetRunner()){outputShown=0;outputText.setText("");runInput.setText("");refreshRun();}}
            public void file(){if(valid()){fileUi.chooseCurrent();refreshSourcePanel();}}
            public void save(String value){if(valid()){pause();saveTextExport(value,"Frociart_risultato.txt",".txt");}}
            public String description(){String label=runStatus==null?"Prova del flowchart":runStatus.getText().toString();String details=runner==null?null:runner.state==FlowEngine.State.ERROR?runner.errorMessage:runner.state==FlowEngine.State.WAITING_FILE?runner.lastMessage:getDebugger()!=null&&(debugger.atBreakpoint()||debugger.atTarget()||debugger.targetNotReached())?debugger.message():null;return label+(runner==null?"":" · "+runner.currentFunctionName+" · "+runner.steps+(runner.steps==1?" passo":" passi"))+(details==null||details.isEmpty()?"":"\n"+details);}
        });
    }
    private void refreshSourcePanel(){
        if(sourcePanel==null||!sourcePanel.sheet.dialog.isShowing())return;
        sourcePanel.refreshRuntime();
        if(runner!=null&&runVisible){FlowEngine.State state=runner.state;
            boolean waiting=state==FlowEngine.State.WAITING||state==FlowEngine.State.WAITING_FILE;
            sourcePanel.highlight(runner.currentFunctionId,runner.currentNodeId,(state==FlowEngine.State.ERROR?"Blocco con errore":state==FlowEngine.State.COMPLETED?"Esecuzione completata":waiting?"In attesa":running?"In esecuzione":"Blocco della prova")+" · "+runner.currentFunctionName);
            sourcePanel.execution(state==FlowEngine.State.WAITING?"Inserisci valore":state==FlowEngine.State.WAITING_FILE?"Vai alla richiesta":running?"Pausa":state==FlowEngine.State.COMPLETED?"Riprova":"Esegui",!waiting&&state!=FlowEngine.State.ERROR,!waiting&&!running&&state!=FlowEngine.State.ERROR);
        }else{String node=selectedNodeId;if(node==null)for(Node first:document.nodes)if(first.kind==FlowModel.START){node=first.id;break;}sourcePanel.highlight(activeFunctionId,node,selectedNodeId==null?"Inizio algoritmo":"Blocco selezionato");}
    }
    private void readPseudocode(Uri uri){
        final FlowPseudocodePanel panel=pseudocodePanel;final int ticket=++pseudocodeReadRequest;
        if(panel==null)return;
        io.execute(()->{try{String text;try(InputStream in=getContentResolver().openInputStream(uri)){
            if(in==null)throw new IOException("Il file non è leggibile.");ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int count;
            while((count=in.read(buffer))!=-1){if(out.size()+count>FlowPseudocode.MAX_TEXT*4)throw new IOException("Il file è troppo grande.");out.write(buffer,0,count);}
            text=StandardCharsets.UTF_8.newDecoder().decode(java.nio.ByteBuffer.wrap(out.toByteArray())).toString();if(text.length()>FlowPseudocode.MAX_TEXT)throw new IOException("Massimo 256 Ki caratteri.");}
            final String result=text;handler.post(()->{if(!destroyed&&ticket==pseudocodeReadRequest&&panel==pseudocodePanel&&panel.sheet.dialog.isShowing())panel.replace(()->result);});
        }catch(Exception e){handler.post(()->{if(!destroyed&&ticket==pseudocodeReadRequest&&panel==pseudocodePanel)panel.problem("Non riesco a leggere il testo: "+e.getMessage());});}});
    }
    private void savePseudocode(String text){
        saveTextExport(text,"Pseudocodice.txt",".txt");
    }
    private void discardPreparedTextExport(String path){
        if(path==null)return;try{File boundary=new File(getCacheDir(),"exports").getCanonicalFile(),file=new File(path).getCanonicalFile();if(boundary.equals(file.getParentFile())&&file.getName().startsWith("Codice_")&&file.isFile())file.delete();}catch(IOException ignored){}
    }
    private void saveTextExport(String text,String fileName,String extension){
        saveTextExport(()->text,fileName,extension);
    }
    private void saveTextExport(java.util.concurrent.Callable<String> content,String fileName,String extension){
        if(saving||pendingExportPath!=null){toast("Attendi il file precedente.");return;}saving=true;
        io.execute(()->{try{File folder=new File(getCacheDir(),"exports");if(!folder.isDirectory()&&!folder.mkdirs())throw new IOException("Cartella non disponibile.");
            String text=content.call();
            File file=File.createTempFile("Codice_",extension,folder);try(OutputStream out=new FileOutputStream(file)){if(".ps1".equalsIgnoreCase(extension))out.write(new byte[]{(byte)0xef,(byte)0xbb,(byte)0xbf});out.write(text.getBytes(StandardCharsets.UTF_8));}
            handler.post(()->{saving=false;if(destroyed){discardPreparedTextExport(file.getAbsolutePath());return;}pendingExportPath=file.getAbsolutePath();Intent i=new Intent(Intent.ACTION_CREATE_DOCUMENT);i.addCategory(Intent.CATEGORY_OPENABLE);i.setType(".frociart".equals(extension)||".fprg".equals(extension)?"application/octet-stream":"text/plain");i.putExtra(Intent.EXTRA_TITLE,fileName);try{startActivityForResult(i,SAVE);}catch(ActivityNotFoundException e){pendingExportPath=null;discardPreparedTextExport(file.getAbsolutePath());message("Salvataggio non disponibile","Puoi usare Copia per conservare il testo.");}});
        }catch(Exception e){handler.post(()->{saving=false;if(!destroyed)message("Salvataggio non riuscito",e.getMessage());});}});
    }
    private boolean hasTurtle(){if(project==null)return false;for(Node n:project.nodes)if(FlowTurtle.isTurtle(n.kind))return true;if(project.functions!=null)for(FlowModel.Function f:project.functions)for(Node n:f.body.nodes)if(FlowTurtle.isTurtle(n.kind))return true;return false;}
    private void buildRuntimeTurtle(LinearLayout body){turtleCard=Ui.column(this);turtleCard.setTag("turtle-card");turtleReadout=Ui.note(this,"Disegno · tartaruga");turtleCard.addView(turtleReadout);turtleView=new FlowTurtleView(this);turtleCard.addView(turtleView,new LinearLayout.LayoutParams(-1,dp(230)));LinearLayout buttons=Ui.row(this);Ui.Button expand=new Ui.Button(this,"Espandi","center",0);expand.setOnClickListener(v->showTurtle());buttons.addView(expand,Ui.weight(this,48));Ui.Button save=new Ui.Button(this,"Salva SVG","save",0);save.setOnClickListener(v->exportTurtle(false));buttons.addView(save,Ui.weight(this,48));turtleCard.addView(buttons);body.addView(turtleCard);}
    private void refreshTurtle(){if(turtleSheet!=null&&runner!=null&&(runner.state==FlowEngine.State.WAITING||runner.state==FlowEngine.State.WAITING_FILE)){turtleSheet.dismiss();}if(turtleCard==null)return;boolean visible=hasTurtle()||runner!=null&&runner.turtle.used();turtleCard.setVisibility(visible?View.VISIBLE:View.GONE);FlowTurtle t=visible&&runner!=null?runner.turtle:null;turtleView.bind(t);if(turtleLarge!=null)turtleLarge.bind(t);if(turtlePlay!=null)turtlePlay.setLabel(running?"Pausa":"Esegui");turtleReadout.setText(t==null?"Disegno · tartaruga":"X "+FlowEngine.Value.number(t.x()).format()+" · Y "+FlowEngine.Value.number(t.y()).format()+" · "+FlowEngine.Value.number(t.heading()).format()+"°");}
    private void showTurtle(){Ui.Sheet s=sheet("Disegno della tartaruga","Il foglio conserva i colori della penna. Trascina per spostare e pizzica per ingrandire.");turtleSheet=s;FlowTurtleView view=new FlowTurtleView(this);turtleLarge=view;view.bind(runner==null?null:runner.turtle);s.body.addView(view,new LinearLayout.LayoutParams(-1,dp(310)));LinearLayout tools=Ui.row(this);Ui.Button fit=new Ui.Button(this,"Tutto",null,0);fit.setOnClickListener(v->view.fit());tools.addView(fit,Ui.weight(this,48));Ui.Button minus=new Ui.Button(this,"−",null,0);minus.setContentDescription("Riduci disegno");minus.setOnClickListener(v->view.zoom(1/1.2));tools.addView(minus,Ui.weight(this,48));Ui.Button plus=new Ui.Button(this,"+",null,0);plus.setContentDescription("Ingrandisci disegno");plus.setOnClickListener(v->view.zoom(1.2));tools.addView(plus,Ui.weight(this,48));s.body.addView(tools);
        LinearLayout controls=Ui.row(this);Ui.Button step=new Ui.Button(this,"Avanti","step",0);step.setOnClickListener(v->{debugStep(FlowDebugger.Command.INTO);if(runner!=null&&(runner.state==FlowEngine.State.WAITING||runner.state==FlowEngine.State.WAITING_FILE))s.dismiss();});controls.addView(step,Ui.weight(this,48));turtlePlay=new Ui.Button(this,"Esegui","play",1);turtlePlay.setOnClickListener(v->{if(running)stopRun();else startRun();refreshRun();if(runner!=null&&(runner.state==FlowEngine.State.WAITING||runner.state==FlowEngine.State.WAITING_FILE))s.dismiss();});controls.addView(turtlePlay,Ui.weight(this,48));s.footer.addView(controls);s.action("Salva disegno PNG","save",0,()->exportTurtle(true));s.action("Salva disegno SVG","save",0,()->exportTurtle(false));s.dialog.setOnDismissListener(d->{if(turtleLarge==view){turtleLarge=null;turtlePlay=null;turtleSheet=null;}if(activeSheet==s)activeSheet=null;});s.show();}
    private void exportTurtle(boolean png){if(saving){toast("Sto preparando il file precedente.");return;}if(runner==null||!runner.turtle.used()){toast("Esegui un comando tartaruga prima di salvare il disegno.");return;}stopRun();resumeAfterInput=false;refreshRun();final FlowTurtle.Snapshot snapshot=runner.turtle.capture();saving=true;toast("Preparo il disegno…");io.execute(()->{try{File dir=new File(getCacheDir(),"exports");if(!dir.isDirectory()&&!dir.mkdirs())throw new IOException("Non riesco a preparare il disegno.");File output=File.createTempFile("Tartaruga_",png?".png":".svg",dir);try(OutputStream out=new FileOutputStream(output)){if(png){android.graphics.Bitmap bitmap=FlowTurtleView.render(snapshot,1600,1200);try{if(!bitmap.compress(android.graphics.Bitmap.CompressFormat.PNG,100,out))throw new IOException("Immagine non creata.");}finally{bitmap.recycle();}}else out.write(FlowTurtle.svg(snapshot).getBytes(StandardCharsets.UTF_8));}handler.post(()->{saving=false;if(destroyed)return;pendingExportPath=output.getAbsolutePath();Intent intent=new Intent(Intent.ACTION_CREATE_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType(png?"image/png":"image/svg+xml");intent.putExtra(Intent.EXTRA_TITLE,png?"Disegno_tartaruga.png":"Disegno_tartaruga.svg");try{startActivityForResult(intent,SAVE);}catch(ActivityNotFoundException ex){message("Salvataggio non disponibile","Il selettore dei documenti non è disponibile.");}});}catch(Exception ex){handler.post(()->{saving=false;if(!destroyed)message("Disegno non salvato",ex.getMessage());});}});}
    private void showExecutionSnapshot(){stopRun();resumeAfterInput=false;refreshRun();Ui.Sheet s=sheet("Immagine con dati","Diagramma, variabili e risultato della prova in un PNG leggibile, senza menu.");s.primary("Salva immagine","save",()->{s.dismiss();exportExecutionSnapshot(false);});s.action("Condividi immagine","link",0,()->{s.dismiss();exportExecutionSnapshot(true);});s.show();}
    private void exportExecutionSnapshot(boolean share){
        if(saving){toast("Sto preparando il file precedente.");return;}stopRun();resumeAfterInput=false;saving=true;
        Document snapshot=document.copy();FlowExecutionSnapshot report=new FlowExecutionSnapshot(snapshot,runner);FlowCanvasView renderer=FlowExporter.createRenderer(this,snapshot);toast("Preparo diagramma e dati…");
        io.execute(()->{try{File file=report.save(this,renderer);handler.post(()->{saving=false;if(destroyed)return;if(share){Uri uri=Uri.parse("content://"+getPackageName()+".files/"+file.getName());Intent intent=new Intent(Intent.ACTION_SEND);intent.setType("image/png");intent.putExtra(Intent.EXTRA_STREAM,uri);intent.setClipData(ClipData.newUri(getContentResolver(),file.getName(),uri));intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);try{startActivity(Intent.createChooser(intent,"Condividi diagramma e dati"));}catch(ActivityNotFoundException e){message("Condivisione non disponibile","Nessuna app disponibile. Usa Salva immagine.");}}else{pendingExportPath=file.getAbsolutePath();Intent intent=new Intent(Intent.ACTION_CREATE_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("image/png");intent.putExtra(Intent.EXTRA_TITLE,FlowExporter.safeName(snapshot.title)+"_diagramma_e_dati.png");try{startActivityForResult(intent,SAVE);}catch(ActivityNotFoundException e){message("Salvataggio non disponibile","Il selettore dei documenti non è disponibile.");}}});}catch(Exception e){handler.post(()->{saving=false;if(!destroyed)message("Immagine non creata",e.getMessage());});}});
    }
    private void showExport(){Ui.Sheet s=sheet("Esporta il diagramma","Scegli un formato. Il file mantiene i colori delle uscite.");s.action("Progetto modificabile · .frociart","save",2,()->{s.dismiss();export("frociart",false,false);});s.action("Stampa…","export",2,()->{s.dismiss();showPrint();});s.action("PDF · pagina unica","export",0,()->{s.dismiss();export("pdf",false,false);});s.action("PDF · più pagine A4","export",0,()->{s.dismiss();export("pdf",true,false);});s.action("Immagine PNG","export",0,()->{s.dismiss();export("png",false,false);});s.action("Immagine con variabili e risultato","export",0,()->{s.dismiss();showExecutionSnapshot();});s.action("Disegno vettoriale SVG","export",0,()->{s.dismiss();export("svg",false,false);});Ui.gap(s.body,8);s.action("Condividi progetto .frociart","link",0,()->{s.dismiss();export("frociart",false,true);});s.action("Condividi immagine PNG","link",0,()->{s.dismiss();export("png",false,true);});s.show();}
    private void showPrint(){if(canvas.isInteracting())return;Ui.Sheet s=sheet("Stampa il diagramma","Anteprima di sistema, stampante o Salva come PDF. Il diagramma resta invariato.");s.choice("Pagine leggibili","Divide i diagrammi grandi; mantieni leggibili testi e variabili.",Ui.accentInk(),()->{s.dismiss();startPrint(true);});s.choice("Una pagina unica","Adatta tutto al foglio; i diagrammi grandi saranno più piccoli.",Ui.accentInk(),()->{s.dismiss();startPrint(false);});s.show();}
    private void startPrint(boolean tiled){if(canvas.isInteracting())return;try{FlowPrintAdapter.start(this,document,tiled);}catch(RuntimeException ex){message("Stampa non disponibile",ex.getMessage());}}
    private void export(String format,boolean tiled,boolean share){if(saving){toast("Sto preparando il file precedente.");return;}saveNow();saving=true;Document snapshot=("frociart".equals(format)?project:document).copy();FlowCanvasView exportRenderer=FlowExporter.createRenderer(this,snapshot);toast("Preparo il file…");io.execute(()->{try{File f=FlowExporter.create(this,snapshot,format,tiled,exportRenderer);handler.post(()->{saving=false;if(destroyed)return;if(share){Uri uri=Uri.parse("content://"+getPackageName()+".files/"+f.getName());Intent i=new Intent(Intent.ACTION_SEND);i.setType(FlowExporter.mime(format));i.putExtra(Intent.EXTRA_STREAM,uri);i.setClipData(ClipData.newUri(getContentResolver(),f.getName(),uri));i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);try{startActivity(Intent.createChooser(i,"Condividi diagramma"));}catch(ActivityNotFoundException ex){toast("Nessuna app disponibile per condividere.");}}else{pendingExportPath=f.getAbsolutePath();Intent i=new Intent(Intent.ACTION_CREATE_DOCUMENT);i.addCategory(Intent.CATEGORY_OPENABLE);i.setType(FlowExporter.mime(format));i.putExtra(Intent.EXTRA_TITLE,FlowExporter.safeName(snapshot.title)+"."+format);try{startActivityForResult(i,SAVE);}catch(ActivityNotFoundException e){message("Salvataggio non disponibile","Il selettore dei documenti non è disponibile.");}}});}catch(Exception e){handler.post(()->{saving=false;if(!destroyed)message("Esportazione non riuscita",e.getMessage());});}});}
    /** Return only after the destination has accepted both the bytes and its close. */
    private static void copyExport(File file,OutputStream target)throws IOException{if(target==null)throw new IOException("File non scrivibile.");try(OutputStream out=target;InputStream in=new FileInputStream(file)){byte[] buffer=new byte[32768];int n;while((n=in.read(buffer))!=-1)out.write(buffer,0,n);}}
    @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request==BACKUP_CREATE||request==BACKUP_OPEN){backupResult(request,result,data);return;}if(request==TEMPLATE_OPEN){FlowTemplateLibrary library=pendingTemplateImport;pendingTemplateImport=null;if(library!=null&&library==templateLibrary&&!library.closed){if(result==RESULT_OK&&data!=null&&data.getData()!=null)library.importUri(data.getData());else library.importCancelled();}return;}if(fileUi!=null&&fileUi.result(request,result,data))return;if(request==FUNCTION_OPEN){FunctionImportTicket ticket=pendingFunctionImport;pendingFunctionImport=null;if(result==RESULT_OK&&data!=null&&data.getData()!=null)readFunctionImport(data.getData(),ticket);return;}if(request==PSEUDO_OPEN){if(result==RESULT_OK&&data!=null&&data.getData()!=null)readPseudocode(data.getData());return;}if(request==OPEN){FlowOpenGuard.Ticket ticket=pendingOpen;pendingOpen=null;if(result!=RESULT_OK||data==null||data.getData()==null)return;if(!openGuard.current(ticket,project,documentRevision,projectId)){toast("Apertura annullata: il progetto è cambiato.");return;}importUri(data.getData());return;}if(result!=RESULT_OK||data==null||data.getData()==null){if(request==SAVE){String cancelled=pendingExportPath;pendingExportPath=null;discardPreparedTextExport(cancelled);}return;}if(request==SAVE&&pendingExportPath!=null){File file=new File(pendingExportPath);pendingExportPath=null;Uri uri=data.getData();io.execute(()->{try{copyExport(file,getContentResolver().openOutputStream(uri,"w"));discardPreparedTextExport(file.getAbsolutePath());handler.post(()->{if(!destroyed){if(hubVisible)toast("File salvato");else showHint("File salvato");}});}catch(Exception e){handler.post(()->{if(!destroyed)message("Salvataggio non riuscito",e.getMessage());});}});}}
    @Override public void onConfigurationChanged(Configuration config){
        super.onConfigurationChanged(config);Ui.Sheet.ReadingState restoreReading=activeSheet==null?null:activeSheet.snapshotReading();String restoreMenu=activeSheet==navigationSheet?navigationRoute:null;
        FlowDataTestPanel.Presentation restoreDataPresentation=dataTestPanel==null?null:dataTestPanel.snapshotPresentation();Bundle restoreData=null;if(dataTestPanel!=null){restoreData=new Bundle();dataTestPanel.snapshot(restoreData);}String restoreCommand=commandsPanel==null?null:commandsPanel.query.getText().toString();int commandStart=commandsPanel==null?0:commandsPanel.query.getSelectionStart(),commandEnd=commandsPanel==null?0:commandsPanel.query.getSelectionEnd();
        FlowVariableRenamePanel.State restoreRename=variableRename==null?null:variableRename.snapshot();
        Bundle restoreDesign=designEditor==null?null:designEditor.snapshot();FlowProjectSearch.State restoreSearch=projectSearch==null?null:projectSearch.snapshot();String restoreSearchGraph=activeFunctionId;FlowgorithmExportPanel restoreExport=flowgorithmPanel;String restoreFunction=functionEditor==null?null:prefs.getString("function_draft_last_"+projectId,null);boolean restorePseudocode=pseudocodePanel!=null&&pseudocodePanel.sheet.dialog.isShowing();String editor=visibleEditorId;FlowVariablePanel.State restoreVariables=variablePanel==null?null:variablePanel.snapshot();boolean restoreConsole=consolePanel!=null&&consolePanel.sheet.dialog.isShowing();FlowSourcePanel.State restoreSource=sourcePanel!=null&&sourcePanel.sheet.dialog.isShowing()?sourcePanel.snapshot():null;boolean restoreRun=runVisible,wasRunning=running;String input=runInput==null?"":runInput.getText().toString();boolean suspendedTarget=getDebugger()!=null&&debugger.suspendRunTo();if(suspendedTarget){running=false;resumeAfterInput=false;handler.removeCallbacks(runTick);canvas.clearRunAnimation();}else stopRun();
        closeVariables();if(activeSheet!=null)activeSheet.dismiss();if(prefs.getString("theme","system").equals("system"))Ui.dark=(config.uiMode&Configuration.UI_MODE_NIGHT_MASK)==Configuration.UI_MODE_NIGHT_YES;
        canvas.setDark(Ui.dark);if(hubVisible){showHome();if(restorePseudocode)showPseudocode();else restoreNavigation(restoreMenu);if(activeSheet!=null&&restoreReading!=null)activeSheet.restoreReading(restoreReading);return;}
        buildChrome();if(restoreRename!=null){showVariableRename(restoreRename);}else if(restoreDesign!=null){if(restoreRun){showRun(false);if(runInput!=null)runInput.setText(input);}showDesignEditor(restoreDesign);}else if(restoreSearch!=null){if(restoreRun){showRun(false);if(runInput!=null)runInput.setText(input);activateGraph(restoreSearchGraph,true);}showProjectSearch();if(projectSearch!=null)projectSearch.restore(restoreSearch);}else if(restoreFunction!=null)showFunctionEditor("new".equals(restoreFunction)?null:restoreFunction);else if(restorePseudocode)showPseudocode();else if(restoreExport!=null)showFlowgorithmExport(restoreExport.snapshot(),restoreExport.selectedTypes(),restoreExport.selectedColors());else if(editor!=null){editNode(editor);if(activeSheet!=null&&restoreReading!=null)activeSheet.restoreReading(restoreReading);}else if(restoreVariables!=null){showRun(false);if(runInput!=null)runInput.setText(input);showVariables();if(variablePanel!=null)variablePanel.restore(restoreVariables);if(wasRunning)startRun();}else if(restoreSource!=null){if(restoreRun){showRun(false);if(runInput!=null)runInput.setText(input);}showSourceTarget(restoreSource.target);if(sourcePanel!=null)sourcePanel.restore(restoreSource);}else if(restoreConsole){showRun(false);if(runInput!=null)runInput.setText(input);showConsole();if(wasRunning)startRun();}
        else if(restoreData!=null){if(restoreRun){showRun(false);if(runInput!=null)runInput.setText(input);}showDataTests();if(dataTestPanel!=null){dataTestPanel.restore(restoreData);dataTestPanel.restorePresentation(restoreDataPresentation);}if(activeSheet!=null&&restoreReading!=null)activeSheet.restoreReading(restoreReading);}
        else if(restoreCommand!=null){if(restoreRun){showRun(false);if(runInput!=null)runInput.setText(input);}showCommandPalette(restoreCommand);if(commandsPanel!=null)commandsPanel.restoreSelection(commandStart,commandEnd);}
        else{if(restoreRun){showRun(false);if(runInput!=null)runInput.setText(input);}restoreNavigation(restoreMenu);if(activeSheet!=null&&restoreReading!=null)activeSheet.restoreReading(restoreReading);if(restoreRun&&restoreMenu==null&&wasRunning&&!debugger.suspendedRunTo())startRun();}
    }
    @Override protected void onSaveInstanceState(Bundle out){if(canvas!=null)saveNavigation(out);if(variableRename!=null)variableRename.snapshot().save(out);if(dataTestPanel!=null)dataTestPanel.snapshot(out);if(commandsPanel!=null){out.putString("command_query",commandsPanel.query.getText().toString());out.putInt("command_start",commandsPanel.query.getSelectionStart());out.putInt("command_end",commandsPanel.query.getSelectionEnd());}if(designEditor!=null)out.putBundle("diagramDesignDraft",designEditor.snapshot());if(projectSearch!=null)projectSearch.snapshot().save(out);saveNow();out.putString("projectId",projectId);out.putString("pendingExport",pendingExportPath);out.putString("pendingOpenProject",openGuard.current(pendingOpen,project,documentRevision,projectId)?projectId:null);out.putBoolean("hub",hubVisible);if(functionEditor!=null)out.putString("functionEditor",prefs.getString("function_draft_last_"+projectId,null));if(study!=null){out.putString("study_page",study.page());study.saveState(out);}super.onSaveInstanceState(out);}
    @Override protected void onStart(){super.onStart();foreground=true;}
    @Override protected void onResume(){super.onResume();if(runVisible)refreshRun();}
    @Override protected void onNewIntent(Intent intent){super.onNewIntent(intent);if(intent!=null&&Intent.ACTION_VIEW.equals(intent.getAction())&&intent.getData()!=null&&!destroyed){setIntent(intent);importUri(intent.getData());}}
    @Override public void onWindowFocusChanged(boolean focused){super.onWindowFocusChanged(focused);if(focused&&!destroyed){Ui.systemBars(getWindow());getWindow().getDecorView().post(()->{if(!destroyed&&getWindow().getDecorView().hasWindowFocus())Ui.systemBars(getWindow());});}}
    @Override protected void onStop(){super.onStop();if(dataTestPanel!=null)dataTestPanel.background();if(templateLibrary!=null)templateLibrary.flushOnStop();foreground=false;if(fileUi==null||!fileUi.isPicking()){stopRun();resumeAfterInput=false;}else{running=false;handler.removeCallbacks(runTick);}saveNow();}
    @Override protected void onDestroy(){cancelNavigationViewportRestore();cancelRuntimeInputReveal();if(variableRename!=null)variableRename.close();if(dataTestPanel!=null)dataTestPanel.close();if(commandsPanel!=null)commandsPanel.close();if(designEditor!=null)designEditor.close();if(projectSearch!=null)projectSearch.close();if(templateLibrary!=null)templateLibrary.closeForDestroy();pendingTemplateImport=null;closeConsole();closeVariables();destroyed=true;if(fileUi!=null)fileUi.close(runner);if(activeSheet!=null)activeSheet.dismiss();if(study!=null)study.dispose();handler.removeCallbacksAndMessages(null);if(store!=null)store.close();io.shutdown();super.onDestroy();}
    @Override public void onBackPressed(){if(Build.VERSION.SDK_INT>=30&&root!=null&&root.getRootWindowInsets()!=null&&root.getRootWindowInsets().isVisible(WindowInsets.Type.ime())){((android.view.inputmethod.InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(root.getWindowToken(),0);return;}if(hubVisible){if(!saveNow())return;super.onBackPressed();return;}if(runVisible){closeRun();return;}if(canvas!=null&&canvas.isConnecting()){canvas.cancelConnection();return;}if(canvas!=null&&!canvas.getSelectedIds().isEmpty()){canvas.clearSelection();return;}if(canvas!=null&&canvas.isSelectionMode()){setSelectionTool(false);return;}saveNow();showHome();}
    public void onNodeDetachmentRequested(String nodeId,FlowCanvasView.InsertionChoice choice){
        final FlowCanvasView.InsertionChoice answer=answerOnce(choice);
        Node node=document.find(nodeId);if(node==null||node.kind!=FlowModel.DECISION||runVisible||hubVisible){answer.choose(null);return;}
        Ui.Sheet s=sheet("Stacca la decisione","Scegli quale ramo ricuce il punto precedente. Tutte le caselle restano sul foglio.");
        for(String branch:new String[]{"true","false"}){FlowModel.Document planned=document.copy();if(!FlowPlacement.detachVia(planned,nodeId,branch))continue;
            s.choice(branchChoiceName(node,branch),"Ricuce il punto precedente verso questo ramo",branchColor(node,branch),()->{answer.choose(branch);s.dismiss();});
        }
        s.choice("Nessuno","Lascia liberi i collegamenti precedenti; tutte le caselle restano",Ui.muted(),()->{answer.choose("none");s.dismiss();});
        s.action("Annulla stacco","close",0,s::dismiss);s.dialog.setOnDismissListener(d->answer.choose(null));s.show();
    }

}

