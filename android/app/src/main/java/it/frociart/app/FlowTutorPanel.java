package it.frociart.app;

import android.app.Activity;
import android.content.*;
import android.graphics.Color;
import android.os.*;
import android.text.*;
import android.util.AtomicFile;
import android.view.*;
import android.widget.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;
import static it.frociart.app.FlowModel.*;

/** Native beginner path and preset workshop, isolated from the existing user's free editor. */
public final class FlowTutorPanel implements FlowCanvasView.Listener {
    public interface Host {void install(LinearLayout root);void editor();void newProject();void projects();void settings();void advanced();}
    private final Activity a;private final Host host;private final SharedPreferences prefs;private final File directory;
    private final Handler handler=new Handler(Looper.getMainLooper());private static final ExecutorService WORKER=Executors.newSingleThreadExecutor();
    private String projectTitle="Il mio foglio";private int projectNodes;
    private String route="home",activeId="",selected,editingNode;private FlowTutor.Lesson lesson;private int step;
    private FlowTutor.Workspace work;private FlowCanvasView canvas;private LinearLayout screen,body;private TextView task,status;
    private Ui.Button check,edit,add,link,undoAction,redoAction,retrySave,repair;private Ui.Sheet sheet;private boolean checking,workshop,saveFailed;
    private LinearLayout taskArea,workspaceTools,workspaceActions,workspaceFooter,runPanel;private TextView runState,runSummary,runOutput,resultNotice;private EditText inlineInput;private Ui.Button runAdvance,runSend,expandCanvas;
    private boolean inlineRunOpen;private boolean expandedCanvas,expandedBeforeRun,fastRun,runAnimating,runCompletedFramed;private Ui.Button runSkip,runFinish;private FlowTutor.Feedback completionFeedback;private long verifiedRevision=-1;private String verifiedId;private FlowEngine.Runner activeRunner;private String runExplanation="";private FlowTutor.Feedback lastFeedback;
    private Map<String,FlowEngine.Value> lastVariables=new LinkedHashMap<>();private long lastRunRevision=-1;
    public FlowTutorPanel(Activity activity,Host host){a=activity;this.host=host;prefs=a.getSharedPreferences("frociart_tutor_v1",Context.MODE_PRIVATE);directory=new File(a.getFilesDir(),"tutor-documents");}
    private int dp(float x){return Ui.dp(a,x);}
    public void setProject(String title,int nodes){projectTitle=title;projectNodes=nodes;}
    private LinearLayout shell(String title,Runnable back){
        screen=Ui.column(a);screen.setBackgroundColor(Ui.bg());screen.setOnApplyWindowInsetsListener((v,i)->{v.setPadding(i.getSystemWindowInsetLeft(),i.getSystemWindowInsetTop(),i.getSystemWindowInsetRight(),i.getSystemWindowInsetBottom());return i;});
        LinearLayout container=Ui.column(a);container.setGravity(Gravity.CENTER_HORIZONTAL);container.addView(screen,new LinearLayout.LayoutParams(a.getResources().getConfiguration().screenWidthDp<680?-1:dp(680),-1));container.setBackgroundColor(Ui.bg());
        LinearLayout bar=Ui.row(a);Ui.pad(bar,16,8,16,8);
        if(back==null){
            ImageView brand=new ImageView(a);brand.setImageDrawable(new Ui.Icon(a,"brand",Ui.accentInk(),27));Ui.pad(brand,10,10,10,10);Ui.card(brand,Ui.tint(),15);brand.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);bar.addView(brand,Ui.lp(a,46,46));
        }else{
            Ui.Button first=new Ui.Button(a,"","back",0);first.setContentDescription("Indietro");first.setOnClickListener(v->back.run());bar.addView(first,Ui.lp(a,48,48));
        }
        TextView name=Ui.text(a,title,19,true);Ui.pad(name,12,0,6,0);name.setMaxLines(2);if(back==null){LinearLayout identity=Ui.column(a);identity.addView(name,new LinearLayout.LayoutParams(-1,-2));TextView tagline=Ui.text(a,"La logica prende forma.",11,false);tagline.setTextColor(Ui.muted());Ui.pad(tagline,12,2,6,0);identity.addView(tagline);bar.addView(identity,Ui.weight(a,-2));}else bar.addView(name,Ui.weight(a,-2));
        Ui.Button guide=new Ui.Button(a,"",back==null?"settings":"help",0);guide.setContentDescription(back==null?"Aspetto e guida dell’app":"Blocchi, simboli e parole");guide.setOnClickListener(v->{if(back==null)host.settings();else glossary();});bar.addView(guide,Ui.lp(a,48,48));screen.addView(bar);
        ScrollView scroll=new ScrollView(a);scroll.setFillViewport(false);body=Ui.column(a);Ui.pad(body,18,10,18,20);scroll.addView(body,new ScrollView.LayoutParams(-1,-2));screen.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));host.install(container);screen.requestApplyInsets();Ui.systemBars(a.getWindow());final LinearLayout presented=screen,entries=body;screen.post(()->{if(screen==presented&&entries.getParent()!=null){Ui.enter(bar,0);Ui.stagger(entries);}});return body;
    }
    private void p(LinearLayout parent,String text){TextView t=Ui.note(a,text);t.setTextSize(15);Ui.pad(t,0,7,0,10);parent.addView(t);}
    private void heading(String title){Ui.gap(body,10);body.addView(Ui.text(a,title,22,true));}
    private Ui.Button action(LinearLayout parent,String title,int tone,Runnable run){Ui.Button b=new Ui.Button(a,title,null,tone);b.setSingleLine(false);b.setMaxLines(3);b.setMinimumHeight(dp(52));b.setOnClickListener(v->run.run());parent.addView(b,new LinearLayout.LayoutParams(-1,-2));Ui.gap(parent,8);return b;}
    private void footer(String title,Runnable run){LinearLayout footer=Ui.column(a);workspaceFooter=footer;Ui.pad(footer,18,8,18,12);check=action(footer,title,1,run);screen.addView(footer);}
    private Ui.Sheet dialog(String title,String subtitle){if(sheet!=null)sheet.dismiss();sheet=new Ui.Sheet(a,title,subtitle);return sheet;}
    private void message(String title,String text){Ui.Sheet s=dialog(title,null);s.paragraph(text);s.primary("Ho capito","check",s::dismiss);s.show();}
    private TextView homeText(LinearLayout parent,String value,float size,boolean bold,int color){
        TextView label=Ui.text(a,value,size,bold);label.setTextColor(color);label.setLineSpacing(dp(2),1);parent.addView(label,new LinearLayout.LayoutParams(-1,-2));return label;
    }
    private void homeHeading(LinearLayout parent,String title){
        TextView heading=homeText(parent,title,18,true,Ui.ink());if(Build.VERSION.SDK_INT>=28)heading.setAccessibilityHeading(true);
    }
    private LinearLayout homeCard(String category,String title,String icon,boolean highlighted){
        LinearLayout card=Ui.column(a);Ui.pad(card,18,18,18,18);
        android.graphics.drawable.GradientDrawable shape=Ui.round(highlighted?Ui.tint():Ui.surface(),22,a);if(!highlighted)shape.setStroke(dp(1),Ui.line());card.setBackground(shape);
        LinearLayout top=Ui.row(a);top.setGravity(Gravity.CENTER_VERTICAL);
        TextView tag=Ui.text(a,category,11,true);tag.setLetterSpacing(.10f);tag.setTextColor(highlighted?Ui.accentInk():Ui.success());top.addView(tag,Ui.weight(a,-2));
        ImageView mark=new ImageView(a);mark.setImageDrawable(new Ui.Icon(a,icon,highlighted?Ui.accentInk():Ui.success(),24));mark.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);top.addView(mark,Ui.lp(a,28,28));card.addView(top);Ui.gap(card,7);
        TextView heading=homeText(card,title,21,true,Ui.ink());if(Build.VERSION.SDK_INT>=28)heading.setAccessibilityHeading(true);Ui.gap(card,7);return card;
    }
    private Ui.Button homeButton(LinearLayout parent,String title,String icon,int tone,Runnable run){
        Ui.Button button=new Ui.Button(a,title,icon,tone);button.setSingleLine(false);button.setMaxLines(Integer.MAX_VALUE);button.setMinimumHeight(dp(50));button.setOnClickListener(v->run.run());parent.addView(button,new LinearLayout.LayoutParams(-1,-2));return button;
    }
    private void homeChoice(LinearLayout parent,String title,String explanation,int color,Runnable run){
        Ui.Choice choice=new Ui.Choice(a);choice.bind(title,explanation,color);choice.setOnClickListener(v->run.run());parent.addView(choice,new LinearLayout.LayoutParams(-1,-2));Ui.gap(parent,9);
    }
    public void showHome(){
        route="home";shell("Frociart v1",null);Ui.pad(body,18,8,18,24);
        boolean landscape=a.getResources().getConfiguration().orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE;
        TextView welcome=homeText(body,"Cosa vuoi fare?",landscape?22:26,true,Ui.ink());if(Build.VERSION.SDK_INT>=28)welcome.setAccessibilityHeading(true);
        if(!landscape){Ui.gap(body,6);homeText(body,"Impara con una guida o crea un progetto tuo.",14,false,Ui.muted());}Ui.gap(body,landscape?12:20);
        Set<String> completed=prefs.getStringSet("completed",Collections.<String>emptySet());int done=0;for(FlowTutor.Lesson item:FlowTutorContent.all())if(completed.contains(item.id))done++;
        FlowTutor.Lesson previous=FlowTutorContent.lesson(prefs.getString("active",""));
        LinearLayout learning=homeCard("IMPARA","Impara passo passo","learn",true);
        homeText(learning,"Costruisci i tuoi primi flowchart con spiegazioni e controlli a ogni passo.",14,false,Ui.muted());Ui.gap(learning,10);
        homeText(learning,done==0?"24 lezioni · Si parte da zero":done+" di 24 lezioni completate",12,true,Ui.accentInk());Ui.gap(learning,14);
        homeButton(learning,"Apri le lezioni","right",1,this::showPath);
        if(previous!=null){Ui.gap(learning,12);homeChoice(learning,"Riprendi la lezione",previous.title,Ui.ACCENT,()->openLesson(previous.id));}
        LinearLayout creation=homeCard("CREA","Il tuo foglio libero","edit",false);
        homeText(creation,"Aggiungi blocchi, collegali e prova il risultato. Decidi tu cosa costruire.",14,false,Ui.muted());Ui.gap(creation,14);
        homeButton(creation,"Nuovo progetto","add",2,host::newProject);
        LinearLayout choices=Ui.column(a);boolean columns=a.getResources().getConfiguration().screenWidthDp>=600&&a.getResources().getConfiguration().fontScale<=1.3f;
        if(columns){choices.setOrientation(LinearLayout.HORIZONTAL);LinearLayout.LayoutParams left=new LinearLayout.LayoutParams(0,-2,1);left.rightMargin=dp(12);choices.addView(learning,left);choices.addView(creation,new LinearLayout.LayoutParams(0,-2,1));}
        else{choices.addView(learning,new LinearLayout.LayoutParams(-1,-2));Ui.gap(choices,14);choices.addView(creation,new LinearLayout.LayoutParams(-1,-2));}
        body.addView(choices,new LinearLayout.LayoutParams(-1,-2));Ui.gap(body,24);
        homeHeading(body,"I tuoi lavori");Ui.gap(body,10);
        String detail=projectTitle+" · "+projectNodes+(projectNodes==1?" blocco":" blocchi");
        homeChoice(body,"Riprendi il tuo foglio",detail,Ui.success(),host::editor);
        homeChoice(body,"I miei progetti","Apri e gestisci i progetti salvati sul telefono.",Ui.success(),host::projects);
        Ui.gap(body,14);homeHeading(body,"Esplora e consulta");Ui.gap(body,10);
        homeChoice(body,"Laboratorio guidato","Sperimenta con blocchi già scritti e scopri come funzionano.",Ui.ACCENT,this::showWorkshop);
        homeChoice(body,"Blocchi e simboli","Cerca il significato di una parola o di un simbolo.",Ui.ACCENT,this::glossary);
        homeChoice(body,"Approfondimenti","Ritrova il percorso precedente e i tuoi progressi.",Ui.ACCENT,host::advanced);
        Ui.gap(body,10);homeText(body,"Funziona offline · I progetti restano sul telefono",12,false,Ui.muted());Ui.enter(learning,0);Ui.enter(creation,45);
    }
    public void showPath(){
        route="path";shell("Impara da zero",this::showHome);Set<String> done=prefs.getStringSet("completed",Collections.<String>emptySet());p(body,done.size()+" di 24 lezioni pratiche completate. Puoi riprendere ogni esercizio e consultare le parole che non conosci.");
        String[] units={"Il foglio ti risponde","Memoria e dati","Calcola con i numeri","Fai domande chiare","Scegli e ripeti","Metti insieme i blocchi"};int index=0;String unit="";
        for(FlowTutor.Lesson l:FlowTutorContent.all()){if(!l.unit.equals(unit)){unit=l.unit;heading(units[index++]);}Ui.Choice row=new Ui.Choice(a);row.bind(l.title,done.contains(l.id)?"Completata · riprendi il tuo flowchart":l.goal,done.contains(l.id)?Ui.success():Ui.ACCENT);row.setOnClickListener(v->openLesson(l.id));body.addView(row,new LinearLayout.LayoutParams(-1,-2));Ui.gap(body,8);}
    }
    private void openLesson(String id){
        lesson=FlowTutorContent.lesson(id);if(lesson==null)return;workshop=false;activeId=id;step=Math.max(0,Math.min(lesson.steps.length,prefs.getInt("step_"+id,0)));route="lesson";prefs.edit().putString("active",id).apply();
        shell(lesson.title,this::showPath);heading("La tua missione");p(body,lesson.goal);heading("Prima capiamolo");p(body,lesson.explanation);heading("Come si legge");p(body,lesson.reading);heading("Un esempio");p(body,lesson.example);heading("L’errore da evitare");p(body,lesson.warning);heading("Cosa controlla questa prova");p(body,FlowTutor.criteria(lesson));
        for(String idTerm:lesson.terms){FlowTutorGuide.Term term=FlowTutorGuide.term(idTerm);if(term!=null)action(body,"Spiegami: "+term.title,0,()->term(term));}
        footer(step==0?"Prova sul foglio":"Riprendi il flowchart",()->{if(loadWorkspace(activeId,lesson.seed()))showWorkspace();});
    }
    private void showWorkshop(){
        route="workshop-intro";shell("Laboratorio guidato",this::showHome);heading("Scegli un blocco. Capisci il suo lavoro.");p(body,"Il foglio è distinto dai tuoi esercizi. Puoi inserire blocchi pronti, modificare i valori e provare il percorso. Ogni blocco mostra le variabili che legge e quelle che cambia.");
        action(body,"Apri il laboratorio",1,()->{workshop=true;lesson=null;activeId="workshop";Document d=new Document();d.title="Il mio foglio guidato";Node start=new Node(START,"Inizio",0,0),end=new Node(END,"Fine",0,180);d.nodes.add(start);d.nodes.add(end);d.edges.add(new Edge(start.id,end.id,"next"));if(loadWorkspace(activeId,d))showWorkspace();});
        heading("Esempi da riconoscere");for(FlowTutorGuide.Preset preset:FlowTutorGuide.presets())action(body,preset.title,0,()->presetGuide(preset,false));
    }
    private boolean loadWorkspace(String key,Document fallback){
        try{File file=file(key);Document d=fallback;if(file.exists()||new File(file.getPath()+".bak").exists()){try(FileInputStream in=new AtomicFile(file).openRead()){byte[] bytes=read(in);d=FlowCodec.decode(new String(bytes,StandardCharsets.UTF_8));}}completionFeedback=null;verifiedRevision=-1;verifiedId=null;work=new FlowTutor.Workspace(d);if(!workshop&&!prefs.getStringSet("completed",Collections.<String>emptySet()).contains(activeId))step=FlowTutor.recoverStep(lesson,step,d);selected=null;lastVariables.clear();lastRunRevision=-1;saveFailed=false;return true;}
        catch(Exception e){message("Non riesco a riaprire l’esercizio","La copia salvata è rimasta intatta. Riprova; non verrà sostituita con un foglio vuoto.");return false;}
    }
    private File file(String key){if(!key.matches("b[0-9]{2}|workshop"))throw new IllegalArgumentException("lesson ID");return new File(directory,key+".frociart");}
    private byte[] read(InputStream in)throws IOException{ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int n;while((n=in.read(buffer))!=-1){if(out.size()+n>MAX_BYTES)throw new IOException("size");out.write(buffer,0,n);}return out.toByteArray();}
    private boolean save(){
        if(work==null)return true;FileOutputStream out=null;AtomicFile file=new AtomicFile(file(activeId));
        try{FlowCodec.check(work.document);if(!directory.isDirectory()&&!directory.mkdirs())throw new IOException("directory");byte[] bytes=FlowCodec.encode(work.document).getBytes(StandardCharsets.UTF_8);if(bytes.length>MAX_BYTES)throw new IOException("size");out=file.startWrite();out.write(bytes);file.finishWrite(out);out=null;try(FileInputStream verify=file.openRead()){if(!Arrays.equals(bytes,read(verify)))throw new IOException("write verification");}saveFailed=false;return true;}
        catch(Exception e){saveFailed=true;if(out!=null)file.failWrite(out);android.util.Log.e("FrociartTutor","Save failed: "+e.getClass().getSimpleName());if(status!=null)status.setText("Salvataggio non riuscito · il foglio è ancora aperto");if(retrySave!=null)retrySave.setVisibility(View.VISIBLE);Toast.makeText(a,"Non riesco a salvare. Il foglio è ancora aperto: riprova.",Toast.LENGTH_LONG).show();return false;}
    }
    private void showWorkspace(){
        route="workspace";shell(workshop?"Foglio guidato":lesson.title,()->{if(save()){if(workshop)showWorkshop();else openLesson(activeId);}});
        Ui.Button settings=(Ui.Button)((LinearLayout)screen.getChildAt(0)).getChildAt(2);settings.setIcon("settings");settings.setContentDescription("Strumenti del foglio");settings.setOnClickListener(v->workspaceCommands());
        // The workspace fills the available height; only ancillary pages scroll.
        screen.removeViewAt(1);body=Ui.column(a);Ui.pad(body,12,0,12,0);screen.addView(body,new LinearLayout.LayoutParams(-1,0,1));
        taskArea=Ui.column(a);task=Ui.text(a,"",15,true);task.setMinLines(2);task.setMaxLines(2);taskArea.addView(task);status=Ui.note(a,"");status.setMinLines(2);status.setMaxLines(2);status.setEllipsize(TextUtils.TruncateAt.END);Ui.pad(status,0,4,0,8);taskArea.addView(status);taskArea.setFocusable(true);taskArea.setOnClickListener(v->message("Il tuo compito",workshop?"Inserisci i blocchi e prova il percorso.":step<lesson.steps.length?lesson.steps[step].instruction:lesson.goal));body.addView(taskArea);
        resultNotice=Ui.text(a,"",15,true);resultNotice.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);resultNotice.setVisibility(View.GONE);Ui.pad(resultNotice,10,8,10,8);body.addView(resultNotice);
        canvas=new FlowCanvasView(a);canvas.setListener(this);canvas.setDark(Ui.dark);canvas.setMotionEnabled(Ui.motion);canvas.setSnapToGrid(true);canvas.setMobileViewport(true);canvas.setDocument(work.document,true);
        final FlowCanvasView presentedCanvas=canvas;canvas.addOnLayoutChangeListener((view,left,top,right,bottom,oldLeft,oldTop,oldRight,oldBottom)->{if(right>left&&bottom>top&&(right-left!=oldRight-oldLeft||bottom-top!=oldBottom-oldTop))view.post(()->{if(canvas==presentedCanvas)frameTeachingNode();});});
        FrameLayout frame=new FrameLayout(a);Ui.card(frame,Ui.surface(),16);frame.setClipToOutline(true);frame.addView(canvas,new FrameLayout.LayoutParams(-1,-1));
        LinearLayout drawing=Ui.column(a);drawing.addView(frame,new LinearLayout.LayoutParams(-1,0,1));body.addView(drawing,new LinearLayout.LayoutParams(-1,0,1));expandCanvas=new Ui.Button(a,"","png:expand",0);expandCanvas.setContentDescription("Espandi il flowchart");expandCanvas.setOnClickListener(v->{animateWorkspaceLayout();setExpandedCanvas(!expandedCanvas);});
        workspaceTools=Ui.row(a);Ui.Button undo=new Ui.Button(a,"","undo",0);undo.setContentDescription("Annulla");undoAction=undo;undo.setOnClickListener(v->{if(work.undo(step)){if(!workshop&&work.restoredPhase>=0)step=work.restoredPhase;changed();}});workspaceTools.addView(undo,Ui.lp(a,48,48));
        Ui.Button redo=new Ui.Button(a,"","redo",0);redo.setContentDescription("Ripeti");redoAction=redo;redo.setOnClickListener(v->{if(work.redo(step)){if(!workshop&&work.restoredPhase>=0)step=work.restoredPhase;changed();}});workspaceTools.addView(redo,Ui.lp(a,48,48));
        Ui.Button all=new Ui.Button(a,"Tutto",null,2);all.setTextSize(12);Ui.pad(all,4,10,4,10);all.setOnClickListener(v->canvas.fitToContent());
        Ui.Button arrange=new Ui.Button(a,"Riordina",null,1);arrange.setTextSize(12);Ui.pad(arrange,4,10,4,10);arrange.setOnClickListener(v->{Document planned=work.document.copy();if(FlowAutoLayout.apply(planned)){work.before(workshop?-1:step);Map<String,float[]> before=FlowPlacement.positions(work.document);for(int i=0;i<work.document.nodes.size();i++){work.document.nodes.get(i).x=planned.nodes.get(i).x;work.document.nodes.get(i).y=planned.nodes.get(i).y;}canvas.notifyModelChanged();save();updateTask();canvas.arrangeAnimated(before);}});int toolHeight=Math.max(48,(int)(30*a.getResources().getConfiguration().fontScale+20));arrange.setSingleLine(false);arrange.setMaxLines(2);all.setSingleLine(false);all.setMaxLines(2);arrange.setContentDescription("Riordina i blocchi");all.setContentDescription("Mostra tutto il flowchart");workspaceTools.addView(arrange,Ui.weight(a,toolHeight));workspaceTools.addView(all,Ui.weight(a,toolHeight));workspaceTools.addView(expandCanvas,Ui.lp(a,48,toolHeight));for(int i=1;i<workspaceTools.getChildCount();i++)((LinearLayout.LayoutParams)workspaceTools.getChildAt(i).getLayoutParams()).leftMargin=dp(4);drawing.addView(workspaceTools,0);
        retrySave=action(body,"Riprova il salvataggio",3,()->{save();updateTask();});repair=action(body,"Ripristina una casella iniziale",0,this::restoreSeed);
        buildInlineRunner();
        workspaceActions=Ui.row(a);add=new Ui.Button(a,"Aggiungi",null,0);add.setTextSize(12);add.setOnClickListener(v->palette(null,"next"));workspaceActions.addView(add,Ui.weight(a,48));
        edit=new Ui.Button(a,"Scrivi",null,0);edit.setTextSize(12);edit.setOnClickListener(v->{String target=targetNode();if(target==null)pickBlock(this::editNode);else editNode(target);});workspaceActions.addView(edit,Ui.weight(a,48));link=null;
        Ui.Button test=new Ui.Button(a,"Prova",null,0);test.setTextSize(12);test.setOnClickListener(v->run());workspaceActions.addView(test,Ui.weight(a,48));
        body.addView(workspaceActions);
        for(int i=0;i<workspaceActions.getChildCount();i++)Ui.pad(workspaceActions.getChildAt(i),4,10,4,10);
        footer(workshop?"Controlla il percorso":"Controlla questo passo",this::primaryTask);arrangeWorkspaceLayout(drawing);inlineRunOpen=false;expandedCanvas=false;activeRunner=null;lastFeedback=null;updateTask();
    }
    /** Landscape keeps the diagram beside scrollable controls, with its primary action pinned. */
    private void arrangeWorkspaceLayout(View frame){
        if(a.getResources().getConfiguration().orientation!=android.content.res.Configuration.ORIENTATION_LANDSCAPE)return;
        screen.removeView(body);screen.removeView(workspaceFooter);body.removeView(frame);
        LinearLayout split=Ui.row(a);split.setGravity(Gravity.TOP);LinearLayout side=Ui.column(a);ScrollView scroll=new ScrollView(a);scroll.setFillViewport(false);scroll.addView(body,new ScrollView.LayoutParams(-1,-2));side.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));side.addView(workspaceFooter,new LinearLayout.LayoutParams(-1,-2));
        LinearLayout.LayoutParams drawing=new LinearLayout.LayoutParams(0,-1,1);drawing.setMargins(dp(12),0,dp(8),dp(8));split.addView(frame,drawing);int width=Math.min(340,(int)(a.getResources().getConfiguration().screenWidthDp*.46f));split.addView(side,new LinearLayout.LayoutParams(dp(width),-1));screen.addView(split,new LinearLayout.LayoutParams(-1,0,1));
    }
    private void animateWorkspaceLayout(){if(screen!=null)android.transition.TransitionManager.endTransitions(screen);if(Ui.animations()&&screen!=null&&screen.isLaidOut()){android.transition.TransitionSet transition=new android.transition.TransitionSet().setOrdering(android.transition.TransitionSet.ORDERING_TOGETHER);transition.addTransition(new android.transition.ChangeBounds());transition.addTransition(new android.transition.Fade());transition.setDuration(320);transition.setInterpolator(Ui.SPRING);android.transition.TransitionManager.beginDelayedTransition(screen,transition);}}
    private void setExpandedCanvas(boolean expanded){
        expandedCanvas=expanded;taskArea.setVisibility(expanded?View.GONE:View.VISIBLE);workspaceTools.setVisibility(View.VISIBLE);for(int i=0;i<workspaceTools.getChildCount();i++)workspaceTools.getChildAt(i).setVisibility(expanded&&workspaceTools.getChildAt(i)!=expandCanvas?View.GONE:View.VISIBLE);workspaceActions.setVisibility(expanded?View.GONE:View.VISIBLE);screen.getChildAt(0).setVisibility(expanded?View.GONE:View.VISIBLE);expandCanvas.setIcon(expanded?"png:collapse":"png:expand");expandCanvas.setContentDescription(expanded?"Ripristina comandi e compito":"Espandi il flowchart");updateTrialChrome();canvas.post(this::frameTeachingNode);
    }
    private String framedTaskKey="";private long selectionEpoch;
    private void frameTeachingNode(){if(editingNode!=null||canvas!=null&&canvas.isInteracting()||!trialVisible()&&selected!=null)return;if(canvas==null||!route.equals("workspace")||work==null)return;if(trialVisible()&&activeRunner!=null&&activeRunner.state==FlowEngine.State.COMPLETED){canvas.fitToContent();return;}String id=trialVisible()&&activeRunner!=null?activeRunner.currentNodeId:targetNode();if(work.document.find(id)==null){id=null;for(Node node:work.document.nodes)if(node.kind==START){id=node.id;break;}}if(id!=null)canvas.frameNodeAnimated(id);}
    private boolean trialVisible(){return inlineRunOpen&&runPanel!=null&&route.equals("workspace");}
    private void updateTrialChrome(){
        boolean trial=trialVisible();
        if(workspaceFooter!=null)workspaceFooter.setVisibility(trial?View.GONE:View.VISIBLE);
        if(workspaceTools!=null)workspaceTools.setVisibility(trial?View.GONE:View.VISIBLE);
        if(retrySave!=null)retrySave.setVisibility(!trial&&saveFailed?View.VISIBLE:View.GONE);
        if(repair!=null)repair.setVisibility(!trial&&hasMissingSeed()?View.VISIBLE:View.GONE);
        if(resultNotice!=null)resultNotice.setVisibility(!trial&&lastFeedback!=null?View.VISIBLE:View.GONE);
    }
    private void workspaceCommands(){
        Ui.Sheet s=dialog("Comandi del foglio","La missione e il pulsante in basso indicano il passo corrente.");
        s.action("Blocchi, simboli e parole","help",0,()->{s.dismiss();glossary();});
        s.action("Suoni e vibrazione",null,0,()->{s.dismiss();feedbackSettings();});
        s.action("Spiegami il blocco","help",0,()->{s.dismiss();String target=targetNode();if(target==null)pickBlock(this::blockGuide);else blockGuide(target);});
        s.action("Prova un passo alla volta","step",0,()->{s.dismiss();run();});
        s.action("Leggi il mio flowchart","help",0,()->message("Il tuo flowchart, spiegato",FlowTutorGuide.describe(work.document)));
        s.action("Rinomina variabile","edit",0,this::renameVariable);
        if(!workshop)s.action("Prova direttamente il mio flowchart","check",0,()->{s.dismiss();checkWithValue("",true);});
        s.action("Trova e modifica una casella",null,0,()->{s.dismiss();pickBlock(this::editNode);});
        s.action("Collega le caselle","link",0,()->{s.dismiss();connectPicker();});
        s.action("Salva una copia nei miei progetti",null,0,()->{s.dismiss();publishCopy();});s.show();
    }
    private void primaryTask(){
        if(checking)return;
        if(verifiedRunPending()){run();return;}
        if(lastFeedback!=null&&lastFeedback.pass){showFeedback(lastFeedback);return;}
        if(FlowTutor.actionRequired(lesson,step,work.document)){
            FlowTutor.Step current=lesson.steps[step];
            if(current.action==FlowTutor.ADD)palette(null,"next");
            else if(current.action==FlowTutor.WRITE)editNode(current.node);
            else if(current.action==FlowTutor.CONNECT)chooseTarget(current.from,current.branch);
            return;
        }check();
    }
    private String targetNode(){if(selected!=null&&work.document.find(selected)!=null)return selected;if(!workshop&&step<lesson.steps.length)return lesson.steps[step].node;return null;}
    private void updateTask(){
        updateResultNotice();
        if(task==null||work==null)return;boolean pending=!workshop&&step<lesson.steps.length;FlowTutor.Step current=pending?lesson.steps[step]:null;
        task.setText(workshop?"Il tuo esperimento":pending?"Passo "+(step+1)+" di "+lesson.steps.length+" · "+current.title:"Metti alla prova il tuo flowchart");
        if(status!=null)status.setText(saveFailed?"Non salvato: usa Riprova il salvataggio prima di uscire.":workshop?"Inserisci un blocco pronto, poi prova il percorso.":pending?current.instruction:"Il flowchart è pronto per il controllo finale. Poi seguiamo l’esecuzione.");
        if(undoAction!=null)undoAction.setEnabled(work.canUndo());if(redoAction!=null)redoAction.setEnabled(work.canRedo());if(retrySave!=null)retrySave.setVisibility(saveFailed?View.VISIBLE:View.GONE);if(repair!=null)repair.setVisibility(hasMissingSeed()?View.VISIBLE:View.GONE);
        if(add!=null)add.setVisibility(workshop?View.VISIBLE:View.GONE);
        if(edit!=null){edit.setVisibility(!trialVisible()&&(!pending||selected!=null)?View.VISIBLE:View.GONE);edit.setLabel(a.getResources().getConfiguration().fontScale>=1.5f?"Modifica":"Modifica il blocco");edit.setContentDescription("Modifica il blocco");}
        if(link!=null)link.setVisibility(workshop||!pending?View.VISIBLE:View.GONE);
        boolean action=FlowTutor.actionRequired(lesson,step,work.document);
        if(check!=null){check.setEnabled(!checking);check.setLabel(checking?"Controllo…":workshop?"Controlla il percorso":action?current.action==FlowTutor.ADD?"Aggiungi una casella":current.action==FlowTutor.WRITE?"Scrivi nella casella":"Collega le caselle":pending&&current.action==FlowTutor.VALUE?"Inserisci il valore":pending?"Controlla questo passo":"Controlla il flowchart");}
        if(check!=null&&lastFeedback!=null&&lastFeedback.pass)check.setLabel(verifiedRunPending()?"Continua l’esecuzione":workshop?"Struttura valida · dettagli":"Corretto · continua");
        updateTrialChrome();String key=activeId+"/"+step;
        if(!key.equals(framedTaskKey)){framedTaskKey=key;final FlowCanvasView view=canvas;final int phase=step;final long epoch=selectionEpoch;final String id=pending?current.node:null;
            if(!trialVisible()&&pending&&selected==null&&editingNode==null&&work.document.find(id)!=null)view.post(()->{if(!trialVisible()&&canvas==view&&step==phase&&epoch==selectionEpoch&&selected==null&&editingNode==null&&!view.isInteracting())view.frameNodeAnimated(id);});}

    }
    private void palette(String source,String branch){
        if(source!=null)selected=source;
        if(workshop){Ui.Sheet s=dialog("Blocchi pronti","Leggi cosa fa il blocco prima di inserirlo.");for(FlowTutorGuide.Preset preset:FlowTutorGuide.presets()){s.action(preset.title,null,0,()->presetGuide(preset,true,source,branch));View tile=s.body.getChildAt(s.body.getChildCount()-2);canvas.wirePalette(tile,preset.kind,preset.text,s::dismiss);}
            final Document expected=work.document;final Edge existing=source==null?null:expected.outgoing(source,branch);
            if(existing!=null)s.action("Scollega questo collegamento","link",3,()->{if(work.document==expected&&expected.outgoing(source,branch)==existing){work.before(-1);expected.edges.remove(existing);canvas.cancelConnection();changed();}s.dismiss();});
            s.show();return;}
        if(step>=lesson.steps.length||lesson.steps[step].action!=FlowTutor.ADD){message("Segui il passo corrente","Per questo passo non serve una nuova casella. Puoi rileggere l’obiettivo o aprire la spiegazione del blocco.");return;}
        FlowTutor.Step current=lesson.steps[step];Ui.Sheet s=dialog("Quale blocco serve?",current.instruction);
        final Document expected=work.document;final int expectedStep=step;
        java.util.function.IntConsumer insert=chosen->{if(work.document!=expected||step!=expectedStep)return;Node existing=work.document.find(current.node);work.before(workshop?-1:step);if(existing!=null){existing.kind=chosen;existing.text=chosen==END?"Fine":"";}else FlowTutor.insert(lesson,work.document,current.node,chosen);selected=current.node;changed();};
        for(int kind:FlowModel.separateIo?new int[]{INPUT,OUTPUT,PROCESS,DECISION,END}:new int[]{INPUT,PROCESS,DECISION,END}){s.choice(kind==INPUT&&!FlowModel.separateIo?"Input / Output":kindName(kind),kind==INPUT?Ui.IO_EXPLANATION:FlowTutorGuide.purpose(kind),Ui.ACCENT,()->{s.dismiss();insert.accept(!FlowModel.separateIo&&kind==INPUT&&isIo(lesson.part(current.node).kind)?lesson.part(current.node).kind:kind);});}s.show();
    }
    private void presetGuide(FlowTutorGuide.Preset preset,boolean insert){Ui.Sheet s=dialog(preset.title,kindName(preset.kind));s.paragraph(preset.text);s.paragraph(preset.explanation);s.paragraph("Prima di usarlo: "+preset.needs);s.paragraph(preset.example);if(insert)s.primary("Inserisci nel foglio","add",()->{s.dismiss();pickInsertionPoint((source,branch)->insertPreset(preset,source,branch));});else s.primary("Ho capito","check",s::dismiss);s.show();}
    private void presetGuide(FlowTutorGuide.Preset preset,boolean insert,String source,String branch){
        if(source==null||branch==null||"next".equals(branch)){presetGuide(preset,insert);return;}
        Ui.Sheet s=dialog(preset.title,kindName(preset.kind));s.paragraph(preset.text);s.paragraph(preset.explanation);s.paragraph("Prima di usarlo: "+preset.needs);s.paragraph(preset.example);
        s.primary("Inserisci nel ramo "+branchName(branch),"add",()->{s.dismiss();Node n=work.document.find(source);if(n!=null&&n.kind==DECISION)insertPreset(preset,source,branch);});s.show();
    }
    private boolean hasMissingSeed(){if(lesson==null||workshop)return false;for(FlowTutor.Part p:lesson.parts)if(p.present&&work.document.find(p.id)==null)return true;return false;}
    private void restoreSeed(){
        if(lesson==null)return;Ui.Sheet s=dialog("Ripristina una casella iniziale","Ripristino solo il contenuto consegnato dalla guida, senza risolvere i passi da scrivere.");
        for(FlowTutor.Part p:lesson.parts)if(p.present&&work.document.find(p.id)==null)s.choice(kindName(p.kind),p.initial.isEmpty()?"Da completare":p.initial,Ui.ACCENT,()->{
            work.before(step);FlowTutor.insert(lesson,work.document,p.id,p.kind);work.document.find(p.id).text=p.initial;
            for(FlowTutor.Link edge:lesson.links)if(edge.present&&(edge.from.equals(p.id)||edge.to.equals(p.id))&&work.document.find(edge.from)!=null&&work.document.find(edge.to)!=null&&work.document.outgoing(edge.from,edge.branch)==null)work.document.edges.add(new Edge(edge.from,edge.to,edge.branch));
            s.dismiss();selected=p.id;changed();
        });s.show();
    }
    private void publishCopy(){
        if(!save())return;try{FlowStore archive=new FlowStore(a);String id=archive.newId();Document copy=work.document.copy();archive.save(id,copy);if(!FlowCodec.encode(copy).equals(FlowCodec.encode(archive.load(id))))throw new IOException("archive verification");Toast.makeText(a,"Copia salvata nei tuoi progetti",Toast.LENGTH_SHORT).show();host.projects();}
        catch(Exception e){android.util.Log.e("FrociartTutor","Archive copy failed: "+e.getClass().getSimpleName());message("Copia non salvata","Il foglio didattico rimane aperto. Riprova il salvataggio nei progetti.");}
    }
    private interface Source {void chosen(String id,String branch);}
    private void pickInsertionPoint(Source action){
        Ui.Sheet s=dialog("Dopo quale blocco?","Il nuovo blocco viene inserito su questa uscita. Il percorso successivo viene conservato.");
        for(Node n:work.document.nodes)if(n.kind!=END)s.choice(kindName(n.kind),n.text.isEmpty()?"Ancora da scrivere":n.text,Ui.ACCENT,()->{selected=n.id;s.dismiss();chooseSource(action);});s.show();
    }
    private void chooseSource(Source action){
        Node n=work.document.find(selected);if(n==null||n.kind==END){pickInsertionPoint(action);return;}
        final Node source=n;if(n.kind!=DECISION){action.chosen(n.id,"next");return;}Ui.Sheet s=dialog("In quale ramo?","Vero e falso sono due percorsi distinti.");s.action(source.trueLabel,"right",0,()->{s.dismiss();action.chosen(source.id,"true");});s.action(source.falseLabel,"right",0,()->{s.dismiss();action.chosen(source.id,"false");});s.show();
    }
    private void insertPreset(FlowTutorGuide.Preset preset,String source,String branch){
        if(work.document.nodes.size()>=MAX_NODES){message("Limite del foglio raggiunto","Il foglio contiene già 500 blocchi. Puoi modificare o rimuovere quelli presenti.");return;}
        Edge old=work.document.outgoing(source,branch);if(preset.kind==END&&old!=null&&work.document.find(old.to).kind==END){message("Questo ramo termina già","La sua uscita arriva a Fine. Puoi inserire un altro passaggio prima di Fine.");return;}
        Map<String,float[]> placementBefore=FlowPlacement.positions(work.document);Node previous=work.document.find(source);String oldTarget=old==null?null:old.to;
        work.before(workshop?-1:step);Node n=new Node(preset.kind,preset.text,previous.x+(width(previous)-width(preset.kind))/2+(previous.kind==DECISION?("true".equals(branch)?-180:180):0),previous.y+height(previous)+72);work.document.nodes.add(n);if(old!=null)work.document.edges.remove(old);work.document.edges.add(new Edge(source,n.id,branch));
        if(n.kind!=END&&old!=null){work.document.edges.add(new Edge(n.id,old.to,n.kind==DECISION?"true":"next"));if(n.kind==DECISION){Node end=null;for(Node possible:work.document.nodes)if(possible.kind==END){end=possible;break;}if(end!=null)work.document.edges.add(new Edge(n.id,end.id,"false"));}}
        FlowPlacement.reflow(work.document,n.id,source,oldTarget,true);selected=n.id;changed();placementBefore.put(n.id,new float[]{n.x,n.y+16});canvas.animateLayout(placementBefore,FlowPlacement.positions(work.document),280);blockGuide(n.id);
    }
    private void pickBlock(java.util.function.Consumer<String> action){Ui.Sheet s=dialog("Scegli la casella","Puoi aprire le caselle anche da questo elenco.");for(Node n:work.document.nodes)s.choice(kindName(n.kind),n.text.isEmpty()?"Ancora da scrivere":n.text,Ui.ACCENT,()->{selected=n.id;s.dismiss();action.accept(n.id);});s.show();}
    private void editNode(String id){
        Node n=work.document.find(id);if(n==null){message("Prima aggiungi il blocco","La casella richiesta non è ancora presente sul foglio.");return;}selected=id;
        Ui.Sheet s=dialog(isIo(n.kind)?"Input / Output":"Scrivi: "+kindName(n.kind),isIo(n.kind)?IO_HINT:FlowTutorGuide.purpose(n.kind));String key="draft_"+activeId+"_"+n.id;EditText value=Ui.edit(a,prefs.getString(key,editorText(n)),n.kind==INPUT?"Nome della variabile":n.kind==DECISION?"Domanda":n.kind==PROCESS?"Assegnazione":n.kind==START||n.kind==END?"Etichetta":"Valore o testo",n.kind==PROCESS);value.setFilters(new InputFilter[]{new InputFilter.LengthFilter(8192)});s.body.addView(value,new LinearLayout.LayoutParams(-1,-2));FlowBlockPreview preview=new FlowBlockPreview(a,n);s.body.addView(preview,new LinearLayout.LayoutParams(-1,-2));preview.text(value.getText().toString());
        EditText prompt=isIo(n.kind)?Ui.edit(a,prefs.getString(key+"_prompt",n.prompt),"Domanda da mostrare (facoltativa)",false):null;
        if(prompt!=null){s.paragraph("Il nome conserva il dato. La domanda è la frase mostrata durante Prova: per esempio Quanti anni hai?");prompt.setFilters(new InputFilter[]{new InputFilter.LengthFilter(1024)});s.body.addView(prompt,new LinearLayout.LayoutParams(-1,-2));prompt.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence t,int x,int y,int z){}public void onTextChanged(CharSequence t,int x,int y,int z){prefs.edit().putString(key+"_prompt",t.toString()).apply();}public void afterTextChanged(Editable e){}});}
        value.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence t,int x,int y,int z){}public void onTextChanged(CharSequence t,int x,int y,int z){lastFeedback=null;stopInlineRunner();prefs.edit().putString(key,t.toString()).apply();preview.text(t.toString());canvas.previewText(id,t.toString());updateTask();}public void afterTextChanged(Editable e){}});
        s.action("Spiegami simboli e variabili","help",0,()->{Node draft=n.copy();applyText(draft,value.getText().toString());s.dismiss();blockGuide(draft,true);});
        if(n.kind==INPUT||n.kind==PROCESS)s.action("Rinomina variabile nel foglio","edit",0,this::renameVariable);
        if(workshop&&n.kind!=START)s.action("Rimuovi dal foglio","trash",3,()->{work.before(workshop?-1:step);work.document.nodes.remove(n);work.document.edges.removeIf(e->e.from.equals(n.id)||e.to.equals(n.id));selected=null;s.dismiss();changed();});
        s.primary("Applica al blocco","check",()->{Map<String,float[]> sizeBefore=FlowPlacement.positions(work.document);String text=value.getText().toString(),question=prompt==null?n.prompt:prompt.getText().toString();if(editChanged(n,text)||!question.equals(n.prompt)){work.before(workshop?-1:step);n.previewText=null;float oldWidth=width(n),oldHeight=height(n);applyText(n,text);n.prompt=question;if(oldWidth!=width(n)||oldHeight!=height(n))FlowPlacement.grow(work.document,id);}if(save()){prefs.edit().remove(key).remove(key+"_prompt").apply();s.dismiss();canvas.notifyModelChanged();canvas.animateLayout(sizeBefore,FlowPlacement.positions(work.document),320);updateTask();}else{value.setError("La modifica è ancora in memoria. Riprova il salvataggio.");}});editingNode=id;s.dialog.setOnDismissListener(ignored->{canvas.clearTextPreview(id);if(sheet==s)editingNode=null;});s.show();
    }
    private void blockGuide(String id){
        Node n=work.document.find(id);if(n!=null)blockGuide(n,false);
    }
    private void renameVariable(){
        Ui.Sheet s=dialog("Quale variabile vuoi rinominare?","Aggiorno le caselle applicate al foglio. Testi fra virgolette e domande leggibili rimangono intatti. Le bozze non applicate restano conservate come le hai scritte.");
        Set<String> names=FlowTutor.variableNames(work.document,false);if(names.isEmpty())s.paragraph("Non trovo ancora variabili riconosciute. Scrivi prima un Input oppure un'Operazione.");
        for(String oldName:names)s.action(oldName,null,0,()->{
            Ui.Sheet ask=dialog("Nuovo nome","Il valore conservato non cambia. Lettera o _ iniziale, poi lettere, cifre o _. Annulla ripristina il foglio.");
            EditText name=Ui.edit(a,oldName,"Nuovo nome della variabile",false);name.setFilters(new InputFilter[]{new InputFilter.LengthFilter(64)});ask.body.addView(name);
            ask.primary("Rinomina nel foglio","check",()->{Document candidate=work.document.copy();int count;
                try{count=FlowTutor.rename(candidate,oldName,name.getText().toString().trim());}catch(IllegalArgumentException error){name.setError(error.getMessage());return;}
                if(count==0){name.setError("Non trovo usi di questo nome da aggiornare.");return;}work.before(workshop?-1:step);work.document=candidate;ask.dismiss();changed();if(!saveFailed)message("Variabile rinominata",count+" caselle aggiornate. Annulla ripristina l'intera modifica. Le bozze non applicate restano in memoria separate dal foglio.");
            });ask.show();
        });s.show();
    }
    private void blockGuide(Node n,boolean draft){
        FlowTutorGuide.Inspection info=FlowTutorGuide.inspect(n);Ui.Sheet s=dialog(kindName(n.kind)+": come funziona",n.text.isEmpty()?"Casella ancora da scrivere":n.text);s.paragraph(FlowTutorGuide.purpose(n.kind));s.paragraph((lastRunRevision==work.revision?"Se esegui questa casella con i valori dell’ultimo passo:\n":"Nessun valore ancora osservato:\n")+FlowTutorGuide.explain(n,lastRunRevision==work.revision?lastVariables:Collections.<String,FlowEngine.Value>emptyMap()));if(draft)s.paragraph("Sto spiegando la bozza che hai appena scritto. Il blocco sul foglio non è ancora stato cambiato.");
        if(info.reads.isEmpty()&&info.writes.isEmpty())s.paragraph("Questa istruzione non usa variabili riconosciute, oppure è ancora da completare.");
        LinkedHashSet<String> variables=new LinkedHashSet<>();variables.addAll(info.reads);variables.addAll(info.writes);
        for(String name:variables){String meaning="Nome scelto nel tuo diagramma.";if(lesson!=null)for(String row:lesson.variables){String[] fields=row.split("\\|",-1);if(fields[0].equalsIgnoreCase(name)){meaning=fields[1]+" Tipo atteso: "+fields[2]+".";break;}}
            String role=(info.reads.contains(name.toLowerCase(Locale.ROOT))||info.reads.contains(name)?"Legge questo valore. ":"")+(info.writes.contains(name)?"Scrive o aggiorna questo valore. ":"");FlowEngine.Value known=null;for(Map.Entry<String,FlowEngine.Value> e:(lastRunRevision==work.revision?lastVariables:Collections.<String,FlowEngine.Value>emptyMap()).entrySet())if(e.getKey().equalsIgnoreCase(name))known=e.getValue();s.paragraph(name+" — "+meaning+"\n"+role+"\n"+(known==null?"Nessun valore osservato nella prova corrente.":"All'ultimo passo eseguito: "+known.format()+" ("+FlowTutor.type(known)+")."));}
        for(FlowTutorGuide.Term symbol:info.symbols)s.action(symbol.title,null,0,()->term(symbol));s.primary(draft?"Torna alla bozza":"Torna al foglio","back",()->{s.dismiss();if(draft)editNode(n.id);});s.show();
    }
    private void term(FlowTutorGuide.Term term){Ui.Sheet s=dialog(term.title,null);s.paragraph(term.meaning);s.paragraph("Esempio: "+term.example);s.paragraph("Attenzione: "+term.trap);s.primary("Ho capito","check",s::dismiss);s.show();}
    private void glossary(){Ui.Sheet s=dialog("Blocchi e simboli","Cerca una parola o un simbolo che non conosci.");EditText search=Ui.edit(a,"","Per esempio !=, Input, variabile",false);s.body.addView(search);LinearLayout results=Ui.column(a);s.body.addView(results);Runnable refresh=()->{results.removeAllViews();String q=search.getText().toString().trim().toLowerCase(Locale.ITALIAN);for(FlowTutorGuide.Term t:FlowTutorGuide.terms())if((t.title+" "+t.aliases+" "+t.meaning).toLowerCase(Locale.ITALIAN).contains(q))action(results,t.title,0,()->term(t));if(results.getChildCount()==0)p(results,"Nessuna voce trovata. Prova il nome del blocco o un simbolo più breve.");};search.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence x,int y,int z,int w){}public void onTextChanged(CharSequence x,int y,int z,int w){refresh.run();}public void afterTextChanged(Editable e){}});refresh.run();s.show();}
    private void connectPicker(){
        if(!workshop&&step<lesson.steps.length&&lesson.steps[step].action==FlowTutor.CONNECT){FlowTutor.Step current=lesson.steps[step];chooseTarget(current.from,current.branch);return;}
        pickBlock(id->{Node source=work.document.find(id);if(source.kind==END){message("Da Fine non parte una freccia","Scegli il blocco che deve avere un passaggio successivo.");return;}selected=id;chooseSource(this::chooseTarget);});
    }
    private void chooseTarget(String source,String branch){Ui.Sheet s=dialog("Scegli dove arrivare","Partenza: "+label(source)+" · uscita "+branchName(branch));for(Node n:work.document.nodes)if(n.kind!=START)s.choice(kindName(n.kind)+(n.id.equals(source)?" · Questo blocco":""),n.text.isEmpty()?"Da completare":n.text,Ui.ACCENT,()->{Edge old=work.document.outgoing(source,branch);if(old!=null&&!old.to.equals(n.id)){Ui.Sheet confirm=dialog("Cambiare l’arrivo?","Il collegamento attuale verrà sostituito. Puoi annullare la modifica.");confirm.paragraph("Prima: "+label(old.to)+"\nDopo: "+label(n.id));confirm.primary("Cambia collegamento","link",()->{confirm.dismiss();connect(source,branch,n.id);});confirm.show();}else{s.dismiss();connect(source,branch,n.id);}});s.show();}
    private String label(String id){Node n=work.document.find(id);return n==null?id:kindName(n.kind)+" · "+(n.text.isEmpty()?"da scrivere":n.text);}
    private String branchName(String branch){return branch.equals("true")?"Sì/vero":branch.equals("false")?"No/falso":"continua";}
    private void connect(String from,String branch,String to){Edge old=work.document.outgoing(from,branch);if(old!=null&&old.to.equals(to)){message("Collegamento già presente","Questa uscita arriva già alla casella scelta.");return;}work.before(workshop?-1:step);if(FlowTutor.connect(work.document,from,branch,to))changed();else{work.undo();message("Collegamento invariato","Quell’arrivo non è consentito. Il collegamento precedente è stato conservato.");}}
    private void check(){
        if(checking||!save())return;if(!workshop&&step<lesson.steps.length&&lesson.steps[step].action==FlowTutor.VALUE){Ui.Sheet s=dialog("Ora inserisci il dato",lesson.steps[step].instruction);String draft="runtime_"+activeId+"_"+step;EditText v=Ui.edit(a,prefs.getString(draft,""),"Valore da conservare",false);v.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence x,int y,int z,int w){}public void onTextChanged(CharSequence x,int y,int z,int w){prefs.edit().putString(draft,x.toString()).apply();}public void afterTextChanged(Editable e){}});s.body.addView(v);Runnable send=()->{if(checking)return;String raw=v.getText().toString();Ui.dismissKeyboard(v);s.dismiss();checkWithValue(raw);};Ui.inputSubmission(v,send);s.primary("Invia il valore","right",send);s.show();return;}checkWithValue("");
    }
    private void checkWithValue(String runtime){checkWithValue(runtime,false);}
    private void checkWithValue(String runtime,boolean finalRequested){
        if(checking||work==null||!save())return;lastFeedback=null;stopInlineRunner();
        final String key=activeId;final int checkedPhase=step;final int stage=finalRequested&&!workshop?lesson.steps.length:step;final long revision=work.revision;final FlowTutor.Workspace expected=work;final Document snapshot=work.document.copy();final FlowTutor.Lesson target=lesson;final boolean free=workshop;checking=true;updateTask();
        WORKER.execute(()->{FlowTutor.Feedback result;
            try{if(free){result=FlowTutor.yes("Percorso verificato","Non trovo errori bloccanti nella struttura o nella sintassi. La prova deve ancora verificare valori e cicli. Per capire se risolvi un problema serve un obiettivo e casi di prova.");for(Node n:snapshot.nodes){FlowTutor.Feedback f=FlowTutorDiagnostics.before(n);if(f!=null){result=f;break;}}if(result.pass)for(FlowEngine.Issue i:FlowEngine.validate(snapshot))if(i.error){result=FlowTutorDiagnostics.issue(i);break;}}
            else result=stage<target.steps.length?FlowTutor.checkStep(target,stage,snapshot,runtime):FlowTutor.check(target,snapshot);}
            catch(RuntimeException e){android.util.Log.e("FrociartTutor","Check failed: "+e.getClass().getSimpleName());result=FlowTutor.no("check-error","Controllo non riuscito","Il foglio non è stato modificato.","Riprova; puoi anche usare Prova un passo alla volta.",null);}
            final FlowTutor.Feedback feedback=result;handler.post(()->{checking=false;if(a.isFinishing()||a.isDestroyed()||!route.equals("workspace")||work!=expected||!activeId.equals(key))return;updateTask();if(work.revision!=revision||step!=checkedPhase){message("Il foglio è cambiato","Premi Controlla per provare la versione attuale.");return;}signalFeedback(feedback.pass);showFeedback(feedback);});
        });
    }
    private void showFeedback(FlowTutor.Feedback f){
        if(f.pass&&!workshop&&f.code.equals("complete")){if(verifiedRunPending())run();else beginVerifiedRun(f);return;}
        lastFeedback=f;updateTask();
        boolean unavailable=f.code.equals("check-error")||f.code.equals("save-failed");
        String verdict=unavailable?"Verifica non completata":f.pass?f.code.equals("complete")?"✓ Flowchart corretto":workshop?"✓ Struttura valida":"✓ Passo corretto":"! Da correggere";
        Ui.Sheet s=dialog(verdict,f.pass?"Il controllo è superato. Puoi continuare.":"Il tuo lavoro è conservato. Leggi cosa rivedere e riprova.");
        TextView outcome=Ui.text(a,unavailable?"RIPROVA IL CONTROLLO":f.pass?"CORRETTO":"DA CORREGGERE",20,true);outcome.setTextColor(f.pass?Ui.success():Ui.danger());s.body.addView(outcome);
        s.paragraph(f.title);s.paragraph(f.why);
        if(f.trace!=null&&!f.trace.isEmpty())s.action("Capisci questa prova","help",0,()->{Ui.Sheet details=dialog("Perché questa prova fallisce","Questi passaggi sono stati eseguiti con i dati indicati.");details.paragraph(f.fix);details.paragraph(f.trace);details.primary("Torna alla correzione","back",()->showFeedback(f));details.show();});
        else if(!f.fix.isEmpty())s.paragraph("Come continuare: "+f.fix);
        if(f.nodeId!=null&&work.document.find(f.nodeId)!=null)s.paragraph("Dove: "+label(f.nodeId));
        if(f.pass&&!workshop){final boolean[] committed={false};final int checkedStep=step;s.primary("Prossimo passo","right",()->{
            if(committed[0]||step!=checkedStep||!save())return;SharedPreferences.Editor record=prefs.edit();record.putInt("step_"+activeId,step+1);
            if(!record.commit()){message("Progresso non salvato","Il diagramma è salvo ma non riesco a conservare il passo. Riprova il controllo.");return;}committed[0]=true;s.dismiss();step++;selected=null;lastFeedback=null;updateTask();
        });}else s.primary("Torna al foglio","back",()->{s.dismiss();if(f.nodeId!=null&&work.document.find(f.nodeId)!=null){selected=f.nodeId;canvas.focusNodeAnimated(f.nodeId);}});
        if(!f.pass&&f.nodeId!=null&&work.document.find(f.nodeId)!=null)s.action("Correggi questo passo",null,0,()->{s.dismiss();if(!workshop&&step<lesson.steps.length&&lesson.steps[step].action==FlowTutor.VALUE)check();else if(!workshop&&step<lesson.steps.length&&lesson.steps[step].action==FlowTutor.ADD)palette(null,"next");else editNode(f.nodeId);});
        if(!workshop&&step<lesson.steps.length)s.action("Non ho capito: rileggi il suggerimento","help",0,()->message("Un passo alla volta",lesson.steps[step].hint.isEmpty()?lesson.steps[step].instruction:lesson.steps[step].hint));s.show();
    }
    private void run(){
        if(checking)return;
        for(Node n:work.document.nodes){FlowTutor.Feedback f=FlowTutorDiagnostics.before(n);if(f!=null){signalFeedback(false);showFeedback(f);return;}}
        for(FlowEngine.Issue i:FlowEngine.validate(work.document))if(i.error){signalFeedback(false);showFeedback(FlowTutorDiagnostics.issue(i));return;}
        if(activeRunner==null||lastRunRevision!=work.revision){activeRunner=new FlowEngine.Runner(work.document);runCompletedFramed=false;lastRunRevision=work.revision;runExplanation="";}
        boolean opening=!inlineRunOpen;if(opening){animateWorkspaceLayout();expandedBeforeRun=expandedCanvas;}
        inlineRunOpen=true;runPanel.setVisibility(View.VISIBLE);setExpandedCanvas(true);canvas.setReadOnly(true);workspaceActions.setVisibility(View.GONE);refreshInlineRun();if(opening)Ui.enter(runPanel,0);if(fastRun)continueFastRun();
    }
    private void buildInlineRunner(){
        runPanel=Ui.column(a);Ui.card(runPanel,Ui.surface(),14);Ui.pad(runPanel,12,8,12,8);runPanel.setVisibility(View.GONE);
        LinearLayout heading=Ui.row(a);runState=Ui.text(a,"Prova",16,true);runState.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);heading.addView(runState,Ui.weight(a,-2));Ui.Button close=new Ui.Button(a,"Fine prova",null,0);close.setContentDescription("Fine prova: torna all’esercizio");close.setOnClickListener(v->closeInlineRun());heading.addView(close,new LinearLayout.LayoutParams(-2,dp(48)));runPanel.addView(heading);
        runSummary=Ui.note(a,"");runSummary.setMaxLines(2);runSummary.setEllipsize(TextUtils.TruncateAt.END);runPanel.addView(runSummary);runOutput=Ui.text(a,"",15,true);runOutput.setMaxLines(3);runOutput.setEllipsize(TextUtils.TruncateAt.END);runOutput.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);runPanel.addView(runOutput);
        LinearLayout inputRow=Ui.row(a);inlineInput=Ui.edit(a,"","Valore Input",false);inputRow.addView(inlineInput,Ui.weight(a,-2));runSend=new Ui.Button(a,"Invia",null,2);runSend.setOnClickListener(v->submitInlineRun());Ui.inputSubmission(inlineInput,this::submitInlineRun);inputRow.addView(runSend,Ui.lp(a,88,48));runPanel.addView(inputRow);inputRow.setVisibility(View.GONE);
        LinearLayout commands=Ui.row(a);runAdvance=new Ui.Button(a,"Avanti","right",1);runAdvance.setOnClickListener(v->advanceInlineRun());commands.addView(runAdvance,Ui.weight(a,48));Ui.Button details=new Ui.Button(a,"Dettagli",null,0);details.setOnClickListener(v->runDetails());commands.addView(details,Ui.weight(a,48));runPanel.addView(commands);runSkip=new Ui.Button(a,"Salta animazione","right",2);runSkip.setVisibility(View.GONE);runSkip.setEnabled(false);runSkip.setOnClickListener(v->{if(!demonstrated())return;fastRun=true;refreshInlineRun();continueFastRun();});runPanel.addView(runSkip,new LinearLayout.LayoutParams(-1,dp(48)));runFinish=new Ui.Button(a,"Continua il percorso","right",1);runFinish.setVisibility(View.GONE);runFinish.setOnClickListener(v->finishVerifiedRun());runPanel.addView(runFinish,new LinearLayout.LayoutParams(-1,dp(48)));body.addView(runPanel);
    }
    private void advanceInlineRun(){
        if(activeRunner==null||runAnimating)return;FlowEngine.Runner runner=activeRunner;
        if((runner.state!=FlowEngine.State.READY&&runner.state!=FlowEngine.State.RUNNING)||runner.steps>=2000)return;
        String from=runner.currentNodeId;runExplanation=FlowTutorGuide.explain(work.document.find(from),runner.variables);runner.step();
        String branch="next";for(Edge edge:work.document.edges)if(edge.from.equals(from)&&edge.to.equals(runner.currentNodeId)){branch=edge.branch;break;}
        canvas.animateRunStep(from,runner.currentNodeId,branch,Ui.motion?360:0);runAnimating=Ui.motion;refreshInlineRun();if(runAnimating)handler.postDelayed(()->{if(activeRunner==runner){runAnimating=false;refreshInlineRun();}},360);
    }
    private void submitInlineRun(){
        if(activeRunner==null||activeRunner.state!=FlowEngine.State.WAITING)return;String raw=inlineInput.getText().toString(),name=activeRunner.inputVariable;
        FlowTutor.Feedback rule=FlowTutor.inputRule(lesson,FlowTutor.canonicalInput(lesson,work.document,name),raw);if(rule!=null){inlineInput.setError(rule.why+" "+rule.fix);signalFeedback(false);return;}
        if(activeRunner.submitInput(raw)){FlowEngine.Value value=activeRunner.variables.get(name);runExplanation="Input conserva "+value.format()+" ("+FlowTutor.type(value)+") in "+name+".";Ui.dismissKeyboard(inlineInput);inlineInput.setText("");inlineInput.setError(null);}else inlineInput.setError(activeRunner.errorMessage);refreshInlineRun();if(fastRun)continueFastRun();
    }
    private void refreshInlineRun(){
        if(activeRunner==null)return;FlowEngine.Runner r=activeRunner;lastVariables=new LinkedHashMap<>(r.variables);boolean waiting=r.state==FlowEngine.State.WAITING;
        String state=waiting?"Inserisci un dato":r.state==FlowEngine.State.ERROR?"Prova interrotta":r.state==FlowEngine.State.COMPLETED?"Esecuzione terminata":r.steps>=2000?"Limite di passaggi raggiunto":"Passo "+r.steps;
        runState.setText(state);runState.setTextColor(r.state==FlowEngine.State.ERROR?Ui.danger():r.state==FlowEngine.State.COMPLETED?Ui.success():Ui.ink());
        runSummary.setText(waiting?(r.inputPrompt==null?"":r.inputPrompt+" · ")+"Variabile: "+r.inputVariable:r.state==FlowEngine.State.ERROR?r.errorMessage:r.state==FlowEngine.State.COMPLETED?verifiedRunPending()?"Hai seguito il tuo flowchart fino alla fine. Ora puoi continuare.":"Prova finita. Usa Fine prova per tornare all’esercizio.":r.state==FlowEngine.State.READY?"Premi Avanti: segui il percorso e osserva i valori.":r.lastMessage);
        ((View)inlineInput.getParent()).setVisibility(waiting?View.VISIBLE:View.GONE);inlineInput.setHint(waiting?"Valore per "+r.inputVariable:"Valore Input");boolean complete=r.state==FlowEngine.State.COMPLETED;if(complete&&!runCompletedFramed){runCompletedFramed=true;final FlowCanvasView expected=canvas;canvas.post(()->{if(canvas==expected&&activeRunner==r&&trialVisible())canvas.fitToContent();});}runAdvance.setVisibility(waiting||complete||fastRun?View.GONE:View.VISIBLE);runAdvance.setEnabled(!runAnimating&&(r.state==FlowEngine.State.READY||r.state==FlowEngine.State.RUNNING)&&r.steps<2000);runSkip.setEnabled(!complete&&!fastRun&&demonstrated());runSkip.setVisibility(!complete&&!fastRun&&demonstrated()?View.VISIBLE:View.GONE);runFinish.setVisibility(complete&&verifiedRunPending()?View.VISIBLE:View.GONE);if(complete&&!workshop&&!demonstrated()){HashSet<String> seen=new HashSet<>(prefs.getStringSet("demonstrated",Collections.<String>emptySet()));seen.add(activeId);if(!prefs.edit().putStringSet("demonstrated",seen).commit())runSummary.setText("Esecuzione terminata. Non riesco a salvare il ripasso: riprova prima di uscire.");}runOutput.setVisibility(r.output.isEmpty()?View.GONE:View.VISIBLE);if(!r.output.isEmpty())runOutput.setText("Risultato: "+String.join(" · ",r.output.subList(Math.max(0,r.output.size()-3),r.output.size())));updateTrialChrome();
    }
    private boolean demonstrated(){return !workshop&&prefs.getStringSet("demonstrated",Collections.<String>emptySet()).contains(activeId);}
    private boolean verifiedRunPending(){return completionFeedback!=null&&work!=null&&verifiedRevision==work.revision&&Objects.equals(verifiedId,activeId);}
    private void beginVerifiedRun(FlowTutor.Feedback feedback){
        if(sheet!=null)sheet.dismiss();completionFeedback=feedback;verifiedId=activeId;verifiedRevision=work.revision;lastFeedback=feedback;activeRunner=null;fastRun=runAnimating=false;updateTask();run();
    }
    private void finishVerifiedRun(){
        if(!verifiedRunPending()||activeRunner==null||activeRunner.state!=FlowEngine.State.COMPLETED||!save())return;
        HashSet<String> completed=new HashSet<>(prefs.getStringSet("completed",Collections.<String>emptySet()));completed.add(activeId);
        if(!prefs.edit().putStringSet("completed",completed).putInt("step_"+activeId,lesson.steps.length).commit()){message("Progresso non salvato","Il flowchart è salvo. Riprova Continua il percorso.");return;}
        completionFeedback=null;closeInlineRun();showPath();
    }
    private void continueFastRun(){
        final FlowEngine.Runner expected=activeRunner;if(!fastRun||expected==null||!trialVisible())return;
        handler.post(()->{if(!fastRun||activeRunner!=expected||!trialVisible())return;int budget=32;while(budget-->0&&(expected.state==FlowEngine.State.READY||expected.state==FlowEngine.State.RUNNING)&&expected.steps<2000)expected.step();refreshInlineRun();if((expected.state==FlowEngine.State.READY||expected.state==FlowEngine.State.RUNNING)&&expected.steps<2000)continueFastRun();});
    }
    private void runDetails(){
        if(activeRunner==null)return;FlowEngine.Runner r=activeRunner;Ui.Sheet s=dialog("Questo passo, spiegato","I valori provengono dal tuo flowchart.");s.paragraph(runExplanation);s.paragraph(r.state==FlowEngine.State.ERROR?FlowTutorDiagnostics.runtime(r.errorMessage,r.currentNodeId).why:r.lastMessage);
        StringBuilder text=new StringBuilder("Variabili dopo il passo:\n");for(Map.Entry<String,FlowEngine.Value> e:r.variables.entrySet())text.append(e.getKey()).append(" = ").append(e.getValue().format()).append(" (").append(FlowTutor.type(e.getValue())).append(")\n");text.append("\nOutput:\n").append(String.join("\n",r.output));s.paragraph(text.toString());
        s.action("Ricomincia la prova","undo",0,()->{r.reset();runCompletedFramed=fastRun=runAnimating=false;runExplanation="";inlineInput.setText("");s.dismiss();refreshInlineRun();});s.primary("Torna al flowchart","back",s::dismiss);s.show();
    }
    private void closeInlineRun(){animateWorkspaceLayout();inlineRunOpen=false;Ui.dismissKeyboard(inlineInput);runPanel.setVisibility(View.GONE);canvas.setReadOnly(false);setExpandedCanvas(expandedBeforeRun);updateTask();}
    private void stopInlineRunner(){fastRun=runAnimating=false;activeRunner=null;if(trialVisible())closeInlineRun();else if(runPanel!=null)runPanel.setVisibility(View.GONE);if(canvas!=null)canvas.clearRunAnimation();if(workspaceActions!=null&&!expandedCanvas)workspaceActions.setVisibility(View.VISIBLE);}
    private void updateResultNotice(){if(resultNotice==null)return;resultNotice.setVisibility(lastFeedback==null?View.GONE:View.VISIBLE);if(lastFeedback!=null){resultNotice.setText(lastFeedback.pass?lastFeedback.code.equals("complete")?"✓ Flowchart corretto":workshop?"✓ Struttura valida":"✓ Passo corretto":"! Da correggere");resultNotice.setTextColor(lastFeedback.pass?Ui.success():Ui.danger());resultNotice.setOnClickListener(v->showFeedback(lastFeedback));}}
    private void feedbackSettings(){
        Ui.Sheet s=dialog("Feedback delle verifiche","L’esito resta sempre visibile anche senza suoni o vibrazione.");android.widget.Switch sound=new android.widget.Switch(a);sound.setText("Suoni");sound.setChecked(prefs.getBoolean("feedback_sound",true));Ui.pad(sound,0,12,0,12);s.body.addView(sound);android.widget.Switch haptic=new android.widget.Switch(a);haptic.setText("Feedback aptico");haptic.setChecked(prefs.getBoolean("feedback_haptic",true));Ui.pad(haptic,0,12,0,12);s.body.addView(haptic);s.primary("Salva preferenze","check",()->{if(prefs.edit().putBoolean("feedback_sound",sound.isChecked()).putBoolean("feedback_haptic",haptic.isChecked()).commit())s.dismiss();else message("Preferenze non salvate","Riprova. Le impostazioni precedenti sono conservate.");});s.show();
    }
    private void signalFeedback(boolean correct){
        if(a.isFinishing()||a.isDestroyed())return;
        if(prefs.getBoolean("feedback_haptic",true)&&canvas!=null)canvas.performHapticFeedback(Build.VERSION.SDK_INT>=30?(correct?HapticFeedbackConstants.CONFIRM:HapticFeedbackConstants.REJECT):(correct?HapticFeedbackConstants.CONTEXT_CLICK:HapticFeedbackConstants.LONG_PRESS));
        if(prefs.getBoolean("feedback_sound",true))try{final android.media.ToneGenerator tone=new android.media.ToneGenerator(android.media.AudioManager.STREAM_MUSIC,35);tone.startTone(correct?android.media.ToneGenerator.TONE_PROP_ACK:android.media.ToneGenerator.TONE_PROP_NACK,correct?150:100);handler.postDelayed(tone::release,400);}catch(RuntimeException ignored){}
    }
    private void changed(){lastFeedback=null;stopInlineRunner();String prior=selected;canvas.setDocument(work.document,false);if(prior!=null&&work.document.find(prior)!=null){selected=prior;canvas.select(prior);}if(save())prefs.edit().putInt("step_"+activeId,step).commit();updateTask();}
    /** Rebuild native views without resetting the task, execution or text being edited. */
    public void rebuild(){
        String editor=editingNode;View focused=sheet!=null&&sheet.dialog.isShowing()?sheet.root.findFocus():inlineInput;
        boolean focusedInput=focused instanceof EditText&&focused.hasFocus();String field=focusedInput?String.valueOf(focused.getContentDescription()):null;
        int start=focusedInput?((EditText)focused).getSelectionStart():-1,end=focusedInput?((EditText)focused).getSelectionEnd():-1;
        FlowEngine.Runner previous=activeRunner;FlowTutor.Feedback feedback=lastFeedback;boolean wasFast=fastRun;boolean trial=trialVisible(),expanded=expandedCanvas,priorExpanded=expandedBeforeRun;
        String input=inlineInput==null?"":inlineInput.getText().toString();
        if(sheet!=null&&sheet.dialog.isShowing())sheet.dismiss();
        if(route.equals("path"))showPath();else if(route.equals("lesson"))openLesson(activeId);else if(route.equals("workspace")){
            showWorkspace();lastFeedback=feedback;activeRunner=previous;updateTask();
            if(trial&&previous!=null){run();fastRun=wasFast;runAnimating=false;expandedBeforeRun=priorExpanded;inlineInput.setText(input);refreshInlineRun();if(fastRun)continueFastRun();}else if(expanded)setExpandedCanvas(true);
            if(editor!=null&&work.document.find(editor)!=null)editNode(editor);
            EditText target=editor!=null&&sheet!=null?findEditor(sheet.root,field):trial?inlineInput:null;
            if(focusedInput&&target!=null){target.requestFocus();int length=target.length();if(start>=0&&end>=0)target.setSelection(Math.min(start,length),Math.min(end,length));}
        }else if(route.equals("workshop-intro"))showWorkshop();else showHome();
    }
    private EditText findEditor(View view,String description){if(view instanceof EditText&&Objects.equals(description,String.valueOf(view.getContentDescription())))return(EditText)view;if(view instanceof ViewGroup)for(int i=0;i<((ViewGroup)view).getChildCount();i++){EditText found=findEditor(((ViewGroup)view).getChildAt(i),description);if(found!=null)return found;}return null;}
    public void saveState(Bundle out){out.putString("tutor_route",route);out.putString("tutor_id",activeId);out.putInt("tutor_step",step);out.putString("tutor_editor",editingNode);out.putBoolean("tutor_expanded",expandedCanvas);if(route.equals("workspace")&&work!=null)save();}
    public void dispose(){if(sheet!=null)sheet.dismiss();}
    public void restoreState(Bundle state){
        String destination=state.getString("tutor_route","home"),id=state.getString("tutor_id","");
        if(destination.equals("path")){showPath();return;}if(destination.equals("workshop-intro")){showWorkshop();return;}
        if(destination.equals("lesson")){openLesson(id);if(lesson==null)showHome();return;}
        if(destination.equals("workspace")){
            lesson=FlowTutorContent.lesson(id);workshop=id.equals("workshop");if(lesson==null&&!workshop){showHome();return;}activeId=id;step=workshop?0:Math.max(0,Math.min(lesson.steps.length,state.getInt("tutor_step",0)));
            Document fallback;if(workshop){fallback=new Document();fallback.title="Il mio foglio guidato";Node first=new Node(START,"Inizio",0,0),last=new Node(END,"Fine",0,180);fallback.nodes.add(first);fallback.nodes.add(last);fallback.edges.add(new Edge(first.id,last.id,"next"));}else fallback=lesson.seed();
            if(!loadWorkspace(id,fallback)){showHome();return;}showWorkspace();if(state.getBoolean("tutor_expanded",false))setExpandedCanvas(true);String editor=state.getString("tutor_editor");if(editor!=null&&work.document.find(editor)!=null)editNode(editor);return;
        }showHome();
    }
    public boolean back(){if(sheet!=null&&sheet.dialog.isShowing()){sheet.dismiss();return true;}if(trialVisible()){closeInlineRun();return true;}if(expandedCanvas&&route.equals("workspace")){setExpandedCanvas(false);return true;}if(route.equals("home"))return false;if(route.equals("workspace")&&!save())return true;if(route.equals("lesson")){showPath();return true;}showHome();return true;}
    public void onBeforeChange(){lastFeedback=null;stopInlineRunner();work.before(workshop?-1:step);}
    public void onDiagramChanged(String label){lastFeedback=null;stopInlineRunner();save();updateTask();}
    public void onReadOnlySelection(String id){stopInlineRunner();selected=id;canvas.select(id);editNode(id);}
    public boolean onReadOnlyNodeTap(String id){onReadOnlySelection(id);return true;}
    public void onSelectionChanged(){selectionEpoch++;if(canvas!=null&&canvas.getSelectedIds().size()==1)selected=canvas.getSelectedIds().iterator().next();else selected=null;if(edit!=null)edit.setVisibility(!trialVisible()&&(selected!=null||workshop||lesson!=null&&step>=lesson.steps.length)?View.VISIBLE:View.GONE);}
    public void onEditRequested(String id){editNode(id);}
    public void onAddRequested(float x,float y,String source,String branch){palette(source,branch);}
    public void onAddAfterRequested(String id){selected=id;palette(id,"next");}
    public void onAddAfterRequested(String id,String branch){selected=id;palette(id,branch);}
    public void onConnectRequested(String id){selected=id;chooseSource(this::chooseTarget);}
    public void onConnectionStateChanged(String source,String branch){if(status!=null&&source!=null)status.setText("Scegli l’arrivo del ramo "+branchName(branch));}
    public void onHint(String text){if(status!=null&&(canvas==null||!canvas.isInteracting()))status.setText(text);}
    public void onConnectionDropRequested(String source,String target,float x,float y){selected=source;if(target==null)palette(source,"next");else chooseSource((id,branch)->connect(id,branch,target));}
    public void onConnectionDropRequested(String source,String branch,String target,float x,float y){selected=source;if(target==null)palette(source,branch);else canvas.completeConnectionDrop(source,branch,target,x,y);}
    public boolean onConnectionTargetRequested(String source,String branch,String target,float x,float y){if(target!=null)connect(source,branch,target);return true;}
    public void onEdgeEditRequested(String id){for(Edge e:work.document.edges)if(e.id.equals(id)){chooseTarget(e.from,e.branch);return;}}
    private int branchChoiceColor(Node n,boolean yes){try{String color=yes?n.trueColor:n.falseColor;return color==null?(yes?0xFF39B985:0xFFEF6C82):android.graphics.Color.parseColor(color);}catch(IllegalArgumentException invalid){return yes?0xFF39B985:0xFFEF6C82;}}
    public void onNodeInsertionRequested(String nodeId,String edgeId,FlowCanvasView.InsertionChoice choice){
        Node node=work.document.find(nodeId);Edge edge=null;for(Edge e:work.document.edges)if(e.id.equals(edgeId)){edge=e;break;}if(node==null||edge==null)return;
        Ui.Sheet s=dialog("Quale ramo prosegue?","L’altro ramo della decisione resta libero.");Node arrival=work.document.find(edge.to);s.paragraph("Il percorso continua verso: "+(arrival==null?"il blocco successivo":arrival.text));
        s.choice(node.trueLabel,"Continua verso il blocco successivo",branchChoiceColor(node,true),()->{choice.choose("true");s.dismiss();});s.choice(node.falseLabel,"Continua verso il blocco successivo",branchChoiceColor(node,false),()->{choice.choose("false");s.dismiss();});s.choice("Nessuno","Entrambi i rami restano liberi; il blocco successivo resta sul foglio",Ui.muted(),()->{choice.choose("none");s.dismiss();});s.action("Annulla inserimento","close",0,()->{choice.choose(null);s.dismiss();});s.dialog.setOnDismissListener(d->choice.choose("skip"));s.show();
    }
    public void onPaletteInsertionRequested(Node candidate,String edgeId,FlowCanvasView.InsertionChoice choice){
        Node node=candidate;Edge edge=null;for(Edge e:work.document.edges)if(e.id.equals(edgeId)){edge=e;break;}if(node==null||edge==null)return;
        Ui.Sheet s=dialog("Quale ramo prosegue?","L’altro ramo della decisione resta libero.");Node arrival=work.document.find(edge.to);s.paragraph("Il percorso continua verso: "+(arrival==null?"il blocco successivo":arrival.text));
        s.choice(node.trueLabel,"Continua verso il blocco successivo",branchChoiceColor(node,true),()->{choice.choose("true");s.dismiss();});s.choice(node.falseLabel,"Continua verso il blocco successivo",branchChoiceColor(node,false),()->{choice.choose("false");s.dismiss();});s.choice("Nessuno","Entrambi i rami restano liberi; il blocco successivo resta sul foglio",Ui.muted(),()->{choice.choose("none");s.dismiss();});s.action("Annulla inserimento","close",0,()->{choice.choose(null);s.dismiss();});s.dialog.setOnDismissListener(d->choice.choose("skip"));s.show();
    }
    public void onNodeDetachmentRequested(String nodeId,FlowCanvasView.InsertionChoice choice){
        Node node=work.document.find(nodeId);if(node==null){choice.choose(null);return;}
        Ui.Sheet s=dialog("Stacca la decisione","Scegli quale ramo ricuce il punto precedente. Tutte le caselle restano sul foglio.");
        for(String branch:new String[]{"true","false"}){FlowModel.Document planned=work.document.copy();if(!FlowPlacement.detachVia(planned,nodeId,branch))continue;
            s.choice("Ramo "+("true".equals(branch)?node.trueLabel:node.falseLabel),"Ricuce il punto precedente verso questo ramo",branchChoiceColor(node,"true".equals(branch)),()->{choice.choose(branch);s.dismiss();});
        }
        s.choice("Nessuno","Lascia liberi i collegamenti precedenti; tutte le caselle restano",Ui.muted(),()->{choice.choose("none");s.dismiss();});
        s.action("Annulla stacco","close",0,s::dismiss);s.dialog.setOnDismissListener(d->choice.choose(null));s.show();
    }

}
