package it.frociart.app;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.*;
import android.graphics.drawable.GradientDrawable;
import android.view.*;
import android.widget.*;
import java.util.HashSet;

/** Phone-first home and a fully offline, sourced learning path. */
public final class FlowStudy {
    public interface Host {
        void installStudy(LinearLayout root);
        void openEditor();
        void newProject();
        void openProjects();
        void openSettings();
        void openSources(FlowLearning.Lesson lesson);
        void practice(FlowLearning.Lesson lesson);
    }
    private final Activity activity;
    private final Host host;
    private final SharedPreferences prefs;
    public final FlowLearningProgress progress=new FlowLearningProgress();
    private String page="home",projectTitle="Il tuo progetto";
    private int projectNodes;
    // FROCIART_TUTOR_LAYER_V1: preserve the previous lessons, progress and practice host.
    private FlowTutorPanel tutorPanel;private boolean tutorVisible;
    private FlowTutorPanel tutor(){
        if(tutorPanel==null)tutorPanel=new FlowTutorPanel(activity,new FlowTutorPanel.Host(){
            public void install(LinearLayout root){host.installStudy(root);}
            public void editor(){tutorVisible=false;host.openEditor();}
            public void newProject(){host.newProject();}
            public void projects(){host.openProjects();}
            public void settings(){host.openSettings();}
            public void advanced(){tutorVisible=false;page="path";render();}
        });tutorPanel.setProject(projectTitle,projectNodes);return tutorPanel;
    }

    private ScrollView scrolling;
    private LinearLayout feedback;
    private TextView feedbackTitle,feedbackBody;
    private TextView[] options;
    private Ui.Button continueButton;
    private boolean completionNew;
    private int introStep;

    public FlowStudy(Activity activity,Host host){
        this.activity=activity;this.host=host;prefs=activity.getSharedPreferences("frociart_learning",Context.MODE_PRIVATE);
        progress.restore(prefs.getStringSet("completed",new HashSet<>()),prefs.getString("active",""),prefs.getInt("question",-1),prefs.getInt("answer",-1),prefs.getBoolean("mistake",false),prefs.getInt("first_try",0));
        progress.restoreReview(prefs.getStringSet("review",new HashSet<>()));
        progress.restorePractices(prefs.getStringSet("built",new HashSet<>()));
    }
    public boolean completePractice(String id){
        if(FlowPractice.mission(id)==null||progress.practiceDone(id))return false;
        HashSet<String> built=new HashSet<>(progress.practices());built.add(id);
        if(!prefs.edit().putStringSet("built",built).commit())throw new IllegalStateException("Progresso non salvato");
        return progress.completePractice(id);
    }
    private int dp(float n){return Ui.dp(activity,n);}
    public String page(){return tutorVisible?"tutor":page;}
    public void setProject(String title,int count){projectTitle=title;projectNodes=count;if(tutorPanel!=null)tutorPanel.setProject(title,count);}
    public void showHome(){tutorVisible=true;tutor().showHome();}
    public void showPath(){tutorVisible=true;tutor().showPath();}
    public void restorePage(String target){if(!"lesson".equals(target)&&!"path".equals(target)){showHome();return;}tutorVisible=false;page=("path".equals(target)||"lesson".equals(target)&&progress.active()!=null)?target:"home";render();}
    public void saveState(android.os.Bundle out){out.putBoolean("study_tutor",tutorVisible);if(tutorVisible)tutor().saveState(out);}
    public boolean restoreState(android.os.Bundle state){if(!state.getBoolean("study_tutor",false))return false;tutorVisible=true;tutor().restoreState(state);return true;}
    public void dispose(){if(tutorPanel!=null)tutorPanel.dispose();}
    public void rebuild(){if(tutorVisible)tutor().rebuild();else render();}
    public boolean back(){if(tutorVisible)return tutor().back();if(page.equals("home"))return false;if(page.equals("lesson"))showPath();else showHome();return true;}
    public void startLesson(String id){tutorVisible=false;progress.begin(id,false);introStep=0;save();page="lesson";completionNew=false;render();}
    private void reviewLesson(){FlowLearning.Lesson lesson=progress.reviewLesson();if(lesson==null)return;progress.begin(lesson.id,true);startLesson(lesson.id);}
    private void reviewAction(LinearLayout body){if(progress.review().isEmpty())return;Ui.Button review=new Ui.Button(activity,"Allena "+progress.review().size()+" idee da ripassare","spark",2);review.setOnClickListener(v->reviewLesson());body.addView(review,new LinearLayout.LayoutParams(-1,dp(54)));Ui.gap(body,10);}
    private void save(){prefs.edit().putStringSet("completed",new HashSet<>(progress.completed())).putStringSet("review",new HashSet<>(progress.review())).putString("active",progress.activeId).putInt("question",progress.question).putInt("answer",progress.answer).putBoolean("mistake",progress.mistake).putInt("first_try",progress.firstTry).apply();}
    private LinearLayout shell(){
        Ui.systemBars(activity.getWindow());LinearLayout root=Ui.column(activity);root.setGravity(Gravity.CENTER_HORIZONTAL);root.setBackgroundColor(Ui.bg());
        root.setOnApplyWindowInsetsListener((v,i)->{v.setPadding(i.getSystemWindowInsetLeft(),i.getSystemWindowInsetTop(),i.getSystemWindowInsetRight(),i.getSystemWindowInsetBottom());return i;});
        LinearLayout content=Ui.column(activity);root.addView(content,new LinearLayout.LayoutParams(activity.getResources().getConfiguration().screenWidthDp<680?-1:dp(680),-1));
        host.installStudy(root);root.requestApplyInsets();return content;
    }
    private LinearLayout scrollBody(LinearLayout parent){
        scrolling=new ScrollView(activity);scrolling.setFillViewport(false);scrolling.setClipToPadding(false);scrolling.setVerticalScrollBarEnabled(false);scrolling.setOverScrollMode(View.OVER_SCROLL_IF_CONTENT_SCROLLS);
        LinearLayout body=Ui.column(activity);Ui.pad(body,20,8,20,28);scrolling.addView(body,new ScrollView.LayoutParams(-1,-2));parent.addView(scrolling,new LinearLayout.LayoutParams(-1,0,1));return body;
    }
    private Ui.Button icon(String name,String description,Runnable action){Ui.Button b=new Ui.Button(activity,"",name,0);b.setContentDescription(description);b.setOnClickListener(v->action.run());return b;}
    private void heading(LinearLayout parent,String title,Runnable back){
        LinearLayout row=Ui.row(activity);Ui.pad(row,12,8,12,8);
        if(back!=null)row.addView(icon("back","Indietro",back),Ui.lp(activity,48,48));
        else{TextView mark=Ui.text(activity,"",18,true);mark.setCompoundDrawablesWithIntrinsicBounds(new Ui.Icon(activity,"brand",Color.WHITE,23),null,null,null);Ui.pad(mark,9,9,8,8);Ui.card(mark,Ui.ACCENT,13);mark.setContentDescription("Frociart");row.addView(mark,Ui.lp(activity,40,40));}
        TextView name=Ui.text(activity,title,19,true);Ui.pad(name,back==null?10:2,0,2,0);name.setMaxLines(2);row.addView(name,Ui.weight(activity,-2));
        row.addView(icon(back==null?"settings":"home",back==null?"Aspetto e animazioni":"Torna alla home",back==null?host::openSettings:this::showHome),Ui.lp(activity,48,48));parent.addView(row);
    }
    private TextView label(String text){TextView t=Ui.text(activity,text,11,true);t.setLetterSpacing(.10f);t.setTextColor(Ui.muted());return t;}
    private LinearLayout card(int color,int padding){LinearLayout l=Ui.column(activity);Ui.pad(l,padding,padding,padding,padding);Ui.card(l,color,24);return l;}
    private void actionable(View v,String description,Runnable action){v.setFocusable(true);v.setContentDescription(description);v.setOnClickListener(x->action.run());Ui.press(v);v.setAccessibilityDelegate(new View.AccessibilityDelegate(){@Override public void onInitializeAccessibilityNodeInfo(View view,android.view.accessibility.AccessibilityNodeInfo info){super.onInitializeAccessibilityNodeInfo(view,info);info.setClassName(android.widget.Button.class.getName());}});}
    private void paragraph(LinearLayout body,String text,int top){TextView t=Ui.note(activity,text);t.setTextSize(15);Ui.pad(t,0,top,0,0);body.addView(t);}
    private void render(){if(page.equals("path"))renderPath();else if(page.equals("lesson")&&progress.active()!=null)renderLesson();else{page="home";renderHome();}}

    private void renderHome(){
        LinearLayout screen=shell();heading(screen,"Frociart",null);LinearLayout body=scrollBody(screen);
        body.addView(label("PENSA. COLLEGA. PROVA."));Ui.gap(body,10);
        TextView title=Ui.text(activity,"Impara costruendo.",26,true);title.setLineSpacing(dp(2),1);body.addView(title);paragraph(body,"Capisci l’idea, costruisci il flowchart, mettilo alla prova.",10);Ui.gap(body,18);
        FlowLearning.Lesson next=progress.recommended();
        LinearLayout learn=card(Ui.ACCENT,20);LinearLayout lead=Ui.row(activity);TextView badge=Ui.text(activity,"IMPARA",11,true);badge.setLetterSpacing(.1f);badge.setTextColor(0xFFE1EBFF);lead.addView(badge,Ui.weight(activity,-2));TextView glyph=Ui.text(activity,"",18,true);glyph.setCompoundDrawablesWithIntrinsicBounds(new Ui.Icon(activity,"learn",Color.WHITE,26),null,null,null);lead.addView(glyph);learn.addView(lead);Ui.gap(learn,10);
        TextView ltitle=Ui.text(activity,progress.completed().size()==FlowLearning.lessons().length?"Ripassa e costruisci":next.title,22,true);ltitle.setTextColor(Color.WHITE);learn.addView(ltitle);TextView lsub=Ui.text(activity,"24 lezioni · 6 flowchart da costruire",13,false);lsub.setTextColor(0xFFE4EDFF);Ui.pad(lsub,0,8,0,14);learn.addView(lsub);
        ProgressLine line=new ProgressLine(activity,0x55FFFFFF,Color.WHITE);line.setFraction(progress.completed().size()/(float)FlowLearning.lessons().length,true);learn.addView(line,new LinearLayout.LayoutParams(-1,dp(6)));Ui.gap(learn,10);
        LinearLayout stats=Ui.row(activity);TextView count=Ui.text(activity,progress.completed().size()+" / 24 lezioni",12,true);count.setTextColor(Color.WHITE);stats.addView(count,Ui.weight(activity,-2));TextView xp=Ui.text(activity,progress.xp()+" XP",12,true);xp.setTextColor(Color.WHITE);stats.addView(xp);learn.addView(stats);Ui.gap(learn,16);
        Ui.Button start=new Ui.Button(activity,progress.active()!=null&&!progress.finished()?"Continua":"Inizia a imparare","right",0);start.setTextColor(0xFF2259C4);start.setCompoundDrawablesRelativeWithIntrinsicBounds(new Ui.Icon(activity,"right",0xFF2259C4,21),null,null,null);start.setSingleLine(false);start.setMaxLines(2);start.setBackground(Ui.round(Color.WHITE,15,activity));start.setOnClickListener(v->startLesson(next.id));learn.addView(start,new LinearLayout.LayoutParams(-1,dp(Math.max(54,32+24*activity.getResources().getConfiguration().fontScale))));body.addView(learn);
        Ui.Button path=new Ui.Button(activity,"Lezioni e laboratori",null,0);path.setSingleLine(false);path.setMaxLines(2);path.setBackgroundColor(Color.TRANSPARENT);path.setTextColor(Ui.accentInk());path.setOnClickListener(v->showPath());body.addView(path,new LinearLayout.LayoutParams(-1,-2));Ui.gap(body,4);
        reviewAction(body);
        LinearLayout free=card(Ui.surface(),20);LinearLayout freeHead=Ui.row(activity);TextView freeLabel=label("IL TUO FOGLIO");freeHead.addView(freeLabel,Ui.weight(activity,-2));free.addView(freeHead);Ui.gap(free,10);
        free.addView(Ui.text(activity,projectTitle,19,true));paragraph(free,"Riprendi il disegno o crea un progetto dai tuoi file.",8);Ui.gap(free,12);Ui.Button open=new Ui.Button(activity,"Apri il foglio","brand",2);open.setOnClickListener(v->host.openEditor());free.addView(open,new LinearLayout.LayoutParams(-1,dp(54)));body.addView(free);Ui.gap(body,18);
        Ui.Button projects=new Ui.Button(activity,"I miei progetti","folder",0);projects.setOnClickListener(v->host.openProjects());projects.setSingleLine(false);projects.setMaxLines(2);body.addView(projects,new LinearLayout.LayoutParams(-1,-2));
        // Animate only the entrance, with no continuously drawing home decoration.
        Ui.stagger(body);
    }
    private void renderPath(){
        LinearLayout screen=shell();heading(screen,"Il tuo percorso",this::showHome);LinearLayout body=scrollBody(screen);
        body.addView(Ui.text(activity,"Capisci. Costruisci. Prova.",26,true));paragraph(body,"Ogni tappa unisce 4 lezioni e un flowchart da costruire o correggere sul foglio.",10);Ui.gap(body,16);
        LinearLayout stats=Ui.row(activity);TextView done=Ui.text(activity,progress.completed().size()+" di 24 completate",13,true);stats.addView(done,Ui.weight(activity,-2));TextView xp=Ui.text(activity,progress.xp()+" XP",13,true);xp.setTextColor(Ui.accentInk());stats.addView(xp);body.addView(stats);Ui.gap(body,10);
        ProgressLine total=new ProgressLine(activity,Ui.line(),Ui.ACCENT);total.setFraction(progress.completed().size()/(float)FlowLearning.lessons().length,true);body.addView(total,new LinearLayout.LayoutParams(-1,dp(7)));paragraph(body,progress.practices().size()+" di 6 laboratori completati",10);Ui.gap(body,22);
        reviewAction(body);
        FlowLearning.Lesson next=progress.recommended();int unitIndex=0,lessonNumber=0;
        for(FlowLearning.Unit unit:FlowLearning.units()){
            body.addView(label("TAPPA "+(++unitIndex)));Ui.gap(body,7);body.addView(Ui.text(activity,unit.title,22,true));paragraph(body,unit.subtitle,6);Ui.gap(body,14);
            for(FlowLearning.Lesson lesson:FlowLearning.lessons())if(lesson.unitId.equals(unit.id)){
                final int number=++lessonNumber;boolean complete=progress.done(lesson.id),recommended=lesson.id.equals(next.id);
                LinearLayout tile=Ui.row(activity);Ui.pad(tile,12,14,12,14);Ui.card(tile,recommended?Ui.tint():Ui.surface(),20);
                TextView n=Ui.text(activity,complete?"✓":String.valueOf(number),16,true);n.setGravity(Gravity.CENTER);n.setTextColor(complete?Ui.success():recommended?Color.WHITE:Ui.muted());Ui.card(n,complete?Ui.successTint():recommended?Ui.ACCENT:Ui.bg(),15);tile.addView(n,Ui.lp(activity,44,44));
                LinearLayout labels=Ui.column(activity);Ui.pad(labels,12,0,8,0);labels.addView(Ui.text(activity,lesson.title,16,true));TextView state=Ui.note(activity,complete?"Completata · ripassa":recommended?"Il tuo prossimo passo":"3 esercizi · esplora");state.setTextColor(recommended?Ui.accentInk():Ui.muted());Ui.pad(state,0,6,0,0);labels.addView(state);tile.addView(labels,Ui.weight(activity,-2));TextView arrow=Ui.text(activity,"",14,false);arrow.setCompoundDrawablesWithIntrinsicBounds(new Ui.Icon(activity,"right",Ui.muted(),18),null,null,null);tile.addView(arrow,Ui.lp(activity,20,24));
                actionable(tile,"Lezione "+number+": "+lesson.title+(complete?", completata":""),()->startLesson(lesson.id));body.addView(tile,new LinearLayout.LayoutParams(-1,-2));Ui.gap(body,9);
            }
            FlowPractice.Mission task=FlowPractice.mission(unit.id);if(task!=null){
                FlowLearning.Lesson entry=null;for(FlowLearning.Lesson l:FlowLearning.lessons())if(l.unitId.equals(unit.id)){entry=l;break;}final FlowLearning.Lesson target=entry;
                Ui.Choice build=new Ui.Choice(activity);build.bind(task.title,progress.practiceDone(task.id)?"Laboratorio completato · riprendi il flowchart":(task.repair?"LABORATORIO · Correggi il flowchart":"LABORATORIO · Costruisci il flowchart"),progress.practiceDone(task.id)?Ui.success():Ui.ACCENT);build.setOnClickListener(v->host.practice(target));body.addView(build,new LinearLayout.LayoutParams(-1,-2));
            }Ui.gap(body,20);
        }
        LinearLayout closing=card(Ui.surface(),18);closing.addView(Ui.text(activity,"Le basi per andare oltre.",18,true));paragraph(closing,"L’ultima tappa apre a correttezza, efficienza e progettazione. La padronanza cresce costruendo progetti e provando casi sempre nuovi.",8);body.addView(closing);
        Ui.enter(body,0);
    }
    private void renderLesson(){
        FlowLearning.Lesson lesson=progress.active();if(lesson==null){showPath();return;}
        if(progress.finished()){renderResult(lesson);return;}
        LinearLayout screen=shell();heading(screen,progress.question<0?"Una nuova idea":"Metti a fuoco",this::showPath);
        LinearLayout strip=Ui.row(activity);Ui.pad(strip,20,0,20,6);for(int i=0;i<lesson.questions.length;i++){ProgressLine segment=new ProgressLine(activity,Ui.line(),Ui.ACCENT);segment.setFraction(progress.question>i?1:0,true);strip.addView(segment,Ui.weight(activity,5));if(i+1<lesson.questions.length)strip.addView(new View(activity),Ui.lp(activity,5,1));}screen.addView(strip);
        LinearLayout body=scrollBody(screen);body.addView(label(progress.question<0?"PRIMA DI PROVARE":"ESERCIZIO "+(progress.question+1)+" DI "+lesson.questions.length));Ui.gap(body,10);
        if(progress.question<0){
            body.addView(Ui.text(activity,lesson.title,28,true));paragraph(body,lesson.goal,10);Ui.gap(body,16);
            body.addView(label(introStep==0?"LA TUA MISSIONE · 3 SFIDE · +30 XP":"IL TRUCCO · POI TOCCA A TE"));Ui.gap(body,14);
            if(introStep==0){
                LinearLayout idea=card(Ui.surface(),18);idea.addView(Ui.text(activity,"L’idea da portare con te",17,true));paragraph(idea,lesson.explanation,12);body.addView(idea);
                primaryFooter(screen,"Continua","right",()->{introStep=1;render();});
            }else{
                LinearLayout tip=card(Ui.tint(),18);TextView tipHead=Ui.text(activity,"Un trucco, con metodo",16,true);tipHead.setTextColor(Ui.accentInk());tip.addView(tipHead);paragraph(tip,lesson.tip,10);body.addView(tip);Ui.gap(body,14);
                body.addView(Ui.text(activity,"Occhio a questo caso",17,true));paragraph(body,lesson.counterexample,10);
                primaryFooter(screen,"Mettiti alla prova","right",()->{progress.start();save();render();});
            }
            Ui.gap(body,8);
            sourceAction(body,lesson);
            Ui.stagger(body);return;
        }
        FlowLearning.Question q=lesson.questions[progress.question];TextView question=Ui.text(activity,q.prompt,24,true);question.setLineSpacing(dp(3),1);body.addView(question);
        if(q.code!=null&&!q.code.isEmpty()){
            Ui.gap(body,18);TextView code=Ui.text(activity,q.code,15,false);code.setTypeface(Typeface.MONOSPACE);code.setLineSpacing(dp(4),1);Ui.pad(code,16,16,16,16);Ui.card(code,Ui.surface(),19);code.setTextIsSelectable(true);body.addView(code);
        }
        Ui.gap(body,20);options=new TextView[q.options.length];
        for(int i=0;i<q.options.length;i++){
            final int option=i;TextView choice=Ui.text(activity,q.options[i],16,true);choice.setGravity(Gravity.CENTER_VERTICAL);choice.setLineSpacing(dp(3),1);choice.setMinimumHeight(dp(62));Ui.pad(choice,16,15,16,15);actionable(choice,"Risposta "+(i+1)+": "+q.options[i],()->{if(progress.choose(option)){save();updateAnswer(true);}});options[i]=choice;body.addView(choice,new LinearLayout.LayoutParams(-1,-2));Ui.gap(body,10);
        }
        feedback=card(Ui.tint(),18);feedbackTitle=Ui.text(activity,"",18,true);feedback.addView(feedbackTitle);feedbackBody=Ui.note(activity,"");feedbackBody.setTextSize(15);Ui.pad(feedbackBody,0,8,0,0);feedback.addView(feedbackBody);feedback.setVisibility(View.GONE);body.addView(feedback);Ui.gap(body,8);sourceAction(body,lesson);
        continueButton=primaryFooter(screen,"Scegli una risposta","right",()->{
            if(progress.answer<0)return;
            if(progress.correct()){boolean wasDone=progress.done(lesson.id);progress.advance();completionNew=progress.finished()&&!wasDone;save();render();}
            else{progress.retry();save();updateAnswer(false);}
        });updateAnswer(false);Ui.stagger(body);
    }
    private void updateAnswer(boolean animate){
        FlowLearning.Lesson lesson=progress.active();if(options==null||lesson==null||progress.question<0||progress.question>=lesson.questions.length)return;
        boolean answered=progress.answer>=0,correct=progress.correct();
        for(int i=0;i<options.length;i++){
            TextView view=options[i];boolean selected=progress.answer==i;int color=selected?(correct?Ui.successTint():Ui.dark?0xFF422832:0xFFFCE9EE):Ui.surface();
            GradientDrawable bg=Ui.round(color,18,activity);bg.setStroke(dp(selected?2:1),selected?(correct?Ui.success():Ui.danger()):Ui.line());view.setBackground(bg);view.setTextColor(selected?(correct?Ui.success():Ui.danger()):Ui.ink());view.setSelected(selected);view.setEnabled(!answered);view.setAlpha(1);
        }
        if(continueButton==null)return;continueButton.setEnabled(answered);continueButton.setLabel(!answered?"Scegli una risposta":correct?(progress.question+1==lesson.questions.length?"Completa la lezione":"Continua"):"Riprova");
        if(!answered){feedback.setVisibility(View.GONE);return;}
        feedback.setVisibility(View.VISIBLE);Ui.card(feedback,correct?Ui.successTint():Ui.tint(),20);feedbackTitle.setText(correct?"Ci sei!":"Fermiamoci su questo punto.");feedbackTitle.setTextColor(correct?Ui.success():Ui.accentInk());feedbackBody.setText(lesson.questions[progress.question].feedback[progress.answer]);
        if(animate){Ui.enter(feedback,0);Ui.announce(feedback,feedbackTitle.getText()+" "+feedbackBody.getText());scrolling.post(()->{if(feedback.getParent()!=null)scrolling.smoothScrollTo(0,Math.max(0,feedback.getBottom()-scrolling.getHeight()+dp(22)));});}
    }
    private Ui.Button primaryFooter(LinearLayout screen,String text,String icon,Runnable action){LinearLayout footer=Ui.column(activity);Ui.pad(footer,20,10,20,14);Ui.Button b=new Ui.Button(activity,text,icon,1);b.setTextSize(14);b.setSingleLine(false);b.setMaxLines(2);b.setOnClickListener(v->action.run());footer.addView(b,new LinearLayout.LayoutParams(-1,dp(Math.max(54,32+24*activity.getResources().getConfiguration().fontScale))));screen.addView(footer);return b;}
    private void sourceAction(LinearLayout body,FlowLearning.Lesson lesson){Ui.Button source=new Ui.Button(activity,"Perché funziona · fonti","learn",0);source.setSingleLine(false);source.setMaxLines(2);source.setBackgroundColor(Color.TRANSPARENT);source.setTextColor(Ui.accentInk());source.setOnClickListener(v->host.openSources(lesson));body.addView(source,new LinearLayout.LayoutParams(-1,-2));}
    private void renderResult(FlowLearning.Lesson lesson){
        LinearLayout screen=shell();heading(screen,"Dalla teoria al foglio",this::showPath);LinearLayout body=scrollBody(screen);
        CompletionMark mark=new CompletionMark(activity);body.addView(mark,new LinearLayout.LayoutParams(-1,dp(84)));Ui.gap(body,8);
        TextView title=Ui.text(activity,"Adesso costruiscilo.",26,true);title.setGravity(Gravity.CENTER);body.addView(title);TextView name=Ui.note(activity,lesson.title);name.setGravity(Gravity.CENTER);Ui.pad(name,0,10,0,0);body.addView(name);Ui.gap(body,18);
        LinearLayout stats=card(Ui.surface(),18);TextView reward=Ui.text(activity,completionNew?"+30 XP · lezione completata":"Lezione completata",18,true);reward.setTextColor(Ui.success());stats.addView(reward);paragraph(stats,progress.firstTry+" di "+lesson.questions.length+" al primo tentativo. Ogni errore è un indizio per capire meglio.",9);body.addView(stats);Ui.gap(body,14);
        int unitDone=0;for(FlowLearning.Lesson item:FlowLearning.lessons())if(item.unitId.equals(lesson.unitId)&&progress.done(item.id))unitDone++;
        TextView milestone=Ui.text(activity,unitDone==4?"Teoria della tappa: 4 lezioni completate!":"La tua tappa: "+unitDone+" / 4 lezioni completate",16,true);milestone.setTextColor(Ui.success());body.addView(milestone);Ui.gap(body,14);
        reviewAction(body);
        LinearLayout tip=card(Ui.tint(),18);tip.addView(Ui.text(activity,"Il dettaglio da ricordare",17,true));paragraph(tip,lesson.counterexample,10);body.addView(tip);Ui.gap(body,12);
        FlowPractice.Mission task=FlowPractice.forLesson(lesson);if(task!=null){body.addView(Ui.text(activity,task.title,19,true));paragraph(body,task.goal,8);}
        Ui.Button path=new Ui.Button(activity,"Torna al percorso",null,0);path.setOnClickListener(v->showPath());body.addView(path,new LinearLayout.LayoutParams(-1,dp(54)));Ui.gap(body,6);sourceAction(body,lesson);
        primaryFooter(screen,"Crea il flowchart","brand",()->host.practice(lesson));Ui.stagger(body);
    }
    /** Compositor-paced progress; one animator per explicit value change. */
    public static final class ProgressLine extends View {
        private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);private final int track,fill;private float fraction;
        private android.animation.ValueAnimator animation;
        public ProgressLine(Context c,int track,int fill){super(c);this.track=track;this.fill=fill;setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);}
        public void setFraction(float next,boolean animate){if(animation!=null)animation.cancel();next=Math.max(0,Math.min(1,next));if(animate&&Ui.animations()){animation=android.animation.ValueAnimator.ofFloat(fraction,next);animation.setDuration(280);animation.addUpdateListener(a->{fraction=(float)a.getAnimatedValue();invalidate();});animation.start();}else{fraction=next;invalidate();}}
        @Override protected void onDraw(Canvas canvas){float r=getHeight()/2f;paint.setColor(track);canvas.drawRoundRect(0,0,getWidth(),getHeight(),r,r,paint);paint.setColor(fill);if(fraction>0)canvas.drawRoundRect(0,0,Math.max(getHeight(),getWidth()*fraction),getHeight(),r,r,paint);}
        @Override protected void onDetachedFromWindow(){if(animation!=null)animation.cancel();super.onDetachedFromWindow();}
    }
    /** Finite celebration, stopped on detach; no idle particle loop. */
    private static final class CompletionMark extends View {
        private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);private final Path check=new Path();private float phase=1;
        private android.animation.ValueAnimator animation;
        CompletionMark(Context c){super(c);setContentDescription("Lezione completata");}
        @Override protected void onAttachedToWindow(){super.onAttachedToWindow();if(Ui.animations()){animation=android.animation.ValueAnimator.ofFloat(0,1);animation.setDuration(850);animation.setInterpolator(new android.view.animation.DecelerateInterpolator());animation.addUpdateListener(a->{phase=(float)a.getAnimatedValue();invalidate();});animation.start();}}
        @Override protected void onDetachedFromWindow(){if(animation!=null)animation.cancel();super.onDetachedFromWindow();}
        @Override protected void onDraw(Canvas c){float x=getWidth()/2f,y=getHeight()/2f,r=Ui.dp(getContext(),41);paint.setStyle(Paint.Style.FILL);paint.setColor(Ui.successTint());float scale=.65f+.35f*Math.min(1,phase*2);c.drawCircle(x,y,r*scale,paint);paint.setColor(Ui.success());paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(Ui.dp(getContext(),5));paint.setStrokeCap(Paint.Cap.ROUND);paint.setStrokeJoin(Paint.Join.ROUND);check.reset();check.moveTo(x-r*.40f,y);check.lineTo(x-r*.08f,y+r*.3f);check.lineTo(x+r*.45f,y-r*.29f);c.save();c.scale(scale,scale,x,y);c.drawPath(check,paint);c.restore();paint.setStyle(Paint.Style.FILL);if(phase<1)for(int i=0;i<16;i++){double a=i*Math.PI/8;float radius=r*(1.1f+phase*.65f);paint.setColor(i%2==0?Ui.ACCENT:Ui.success());paint.setAlpha((int)(255*(1-phase)));c.drawCircle(x+(float)Math.cos(a)*radius,y+(float)Math.sin(a)*radius,Ui.dp(getContext(),2.5f),paint);}paint.setAlpha(255);}
    }
}
