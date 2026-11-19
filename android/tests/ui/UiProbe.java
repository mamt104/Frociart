package it.frociart.uichecks;

import android.app.*;
import android.content.*;
import android.content.pm.ActivityInfo;
import android.graphics.*;
import android.os.*;
import android.util.Log;
import android.view.*;
import android.view.inputmethod.InputMethodManager;
import android.widget.*;
import java.io.*;
import java.lang.reflect.*;
import java.util.*;

/** Native integration checks against the installed release APK, not a mock UI. */
public final class UiProbe extends Instrumentation {
    private Activity activity;
    private File directory;
    private String scenario="portrait",phase="full",originalId,testId;
    private Object originalDoc;
    private Map<String,?> originalPreferences,originalLearningPreferences;
    private boolean originalHub;
    private final Set<String> testProjects=new HashSet<>();
    private final StringBuilder report=new StringBuilder();
    private int checks,errors;
    private interface Action { void run() throws Exception; }
    private interface Condition { boolean value() throws Exception; }

    @Override public void onCreate(Bundle args){super.onCreate(args);if(args!=null){scenario=args.getString("scenario","portrait");phase=args.getString("phase","full");}start();}
    @Override public void onStart(){
        directory=new File(getTargetContext().getExternalFilesDir(null),"uiqa-1.4.0"+(phase.equals("full")?"":"-"+phase)+"/"+scenario);directory.mkdirs();
        try {
            android.accessibilityservice.AccessibilityServiceInfo service=getUiAutomation().getServiceInfo();service.flags|=android.accessibilityservice.AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS;getUiAutomation().setServiceInfo(service);
            File[] previous=directory.listFiles();if(previous!=null)for(File old:previous)if(old.isFile()&&(old.getName().equals("report.txt")||old.getName().matches("[0-9]{2}.*\\.(png|txt)")))if(!old.delete())throw new IOException("Cannot clear previous test capture: "+old.getName());
            Intent intent=new Intent().setClassName(getTargetContext().getPackageName(),"it.frociart.app.MainActivity").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK|Intent.FLAG_ACTIVITY_CLEAR_TASK);
            activity=startActivitySync(intent);pause(600);
            if(phase.equals("upgrade-seed")){upgradeSeed();return;}
            if(phase.equals("upgrade-check")){upgradeCheck();return;}
            ui(()->{
                originalId=(String)get(activity,"projectId");originalDoc=call(get(activity,"document"),"copy");
                originalPreferences=new HashMap<>(((SharedPreferences)get(activity,"prefs")).getAll());originalHub=(Boolean)get(activity,"hubVisible");
                originalLearningPreferences=new HashMap<>(activity.getSharedPreferences("frociart_learning",Context.MODE_PRIVATE).getAll());
                dismiss();SharedPreferences.Editor isolated=((SharedPreferences)get(activity,"prefs")).edit();for(int unit=1;unit<=6;unit++)isolated.remove(String.format(java.util.Locale.ROOT,"mission_project_u%02d",unit));if(!isolated.commit())throw new IllegalStateException("Cannot isolate exercise pointers");testId=(String)call(get(activity,"store"),"newId");
                testProjects.add(testId);call(activity,"openEditor");call(activity,"switchDocument",call(activity,"starter"),testId);
            });
            portrait();theme("Chiaro");
            if(phase.equals("study")){learning();return;}
            if(phase.equals("mobile")){mobileReview();return;}
            if(phase.equals("details")){detailsReview();return;}
            if(phase.equals("files")){filesJourney();return;}
            if(phase.equals("drafts")){draftJourney();return;}
            if(phase.equals("journey")){connectionJourney();return;}
            if(phase.equals("project-delete")){
                // Reuse the current-project regression without replaying all lessons.
                // Only fixture setup uses the lesson controller; deletion uses native UI.
                ui(()->{
                    Object lesson=Class.forName("it.frociart.app.FlowLearning",true,activity.getClassLoader()).getMethod("lesson",String.class).invoke(null,"l01");
                    call(activity,"practiceLesson",lesson);testProjects.add((String)get(activity,"projectId"));
                });settle();deletePracticeThroughUi();return;
            }
            if(!phase.equals("full")){focusedLayout();return;}
            ui(()->{
                float widthDp=activity.getResources().getDisplayMetrics().widthPixels/activity.getResources().getDisplayMetrics().density;
                float fontScale=activity.getResources().getConfiguration().fontScale;
                report.append("CONFIG widthDp=").append(widthDp).append(" fontScale=").append(fontScale).append('\n');
                check(Math.abs(widthDp-(scenario.equals("narrow")?320:384))<1,"Requested phone width is active");
                check(Math.abs(fontScale-(scenario.equals("large")?2f:scenario.equals("narrow")?1.3f:1f))<.02f,"Requested system font scale is active");
                check(full(view("runButton")),"Prova available in main dock");
                check(full(requiredDescription(decor(),"Menu progetto")),"Project/file menu reachable in header");
                check(full(view("gridButton"))&&full(requiredDescription(decor(),"Mostra tutto il diagramma")),"Grid and full overview reachable in stable utility row");
                for(String label:new String[]{"Blocco","Prova","Riordina","Tutto"})check(textFits(requiredText(decor(),label)),"Main action label is not truncated: "+label);
            });
            snap("01-main-light");selectionBounds();picker();starterRun();
            theme("Scuro");snap("05-main-dark");inputRun();errorRecovery();decisionEditor();mobileReview();
        }catch(Throwable error){errors++;report.append("UI_UPDATE_ERROR ").append(Log.getStackTraceString(error)).append('\n');Log.e("FROCIART_UI","UI update checks failed",error);try{snap("99-failure");}catch(Exception ignored){}}
        finally {
            try{if(!phase.startsWith("upgrade-"))restore();}catch(Throwable error){errors++;report.append("UI_UPDATE_ERROR cleanup ").append(Log.getStackTraceString(error)).append('\n');}
            report.append("UI_UPDATE_FINISHED ").append(checks).append(" errors=").append(errors).append('\n');
            try(FileOutputStream out=new FileOutputStream(new File(directory,"report.txt"))){out.write(report.toString().getBytes("UTF-8"));}catch(Exception error){Log.e("FROCIART_UI","Writing report failed",error);}
            Log.i("FROCIART_UI",report.toString());Bundle result=new Bundle();result.putString("stream",report.toString()+"\nScreenshots: "+directory+"\n");finish(errors==0?Activity.RESULT_OK:Activity.RESULT_CANCELED,result);
        }
    }

    /** Isolated QA upgrade only: leave a genuine 1.2.0 editor draft for install -r. */
    private void upgradeSeed()throws Exception{
        ui(()->{dismiss();call(activity,"openEditor");call(activity,"switchDocument",call(activity,"starter"),call(get(activity,"store"),"newId"));});settle();
        renameThroughUi("Frociart upgrade QA");final String[] id={null};ui(()->id[0]=(String)get(nodeOfKind("PROCESS"),"id"));
        editThroughUi(id[0]);setSheetField("Operazione","totale = 19 + 23");tapSheetDescription("Chiudi pannello");
        ui(()->{
            SharedPreferences legacy=(SharedPreferences)get(activity,"prefs");
            check(legacy.getString("draft_text","").equals("totale = 19 + 23"),"J36 genuine 1.2.0 editor writes a legacy draft");
            check(activity.getSharedPreferences("frociart_upgrade_test",Context.MODE_PRIVATE).edit()
                .putString("project",(String)get(activity,"projectId")).putString("node",id[0]).putString("document",encodedDocument()).commit(),"J36 upgrade fixture metadata is persisted");
            check((Boolean)call(activity,"saveNow"),"J36 original project is saved before package replacement");
        });
        snap("60-legacy-draft-seeded");
    }
    private void upgradeCheck()throws Exception{
        final String[] node={null};ui(()->{
            SharedPreferences expected=activity.getSharedPreferences("frociart_upgrade_test",Context.MODE_PRIVATE);
            check(expected.getString("project","").equals(get(activity,"projectId")),"J36 install -r retains the current local project ID");
            check(expected.getString("document","").equals(encodedDocument()),"J36 install -r retains the entire saved diagram without data loss");
            node[0]=expected.getString("node","");call(activity,"openEditor");
        });
        if(!await(()->get(activity,"activeSheet")!=null,6000))editThroughUi(node[0]);
        ui(()->check(findEdit(sheetRoot(),"Operazione").getText().toString().equals("totale = 19 + 23"),"J36 1.2.1 editor recovers the migrated legacy draft"));
        snap("61-upgrade-draft-recovered");tapSheetText("Applica modifiche");
        ui(()->{check(get(call(get(activity,"document"),"find",node[0]),"text").equals("totale = 19 + 23"),"J36 migrated draft applies to its original node");check(!((SharedPreferences)get(activity,"prefs")).contains("draft_text"),"J36 legacy slot is cleared after successful migration");});
    }

    /** Real touchscreen journey: new project, + insertion, branches, destinations and line controls. */
    private void connectionJourney()throws Exception{
        selectMode(true);newProjectThroughUi();
        final String[] start={null},first={null},end={null},input={null},decision={null},other={null};
        ui(()->start[0]=(String)get(nodeOfKind(0),"id"));
        selectThroughUi(start[0]);check(await(()->!((RectF)get(canvas(),"plusHit")).isEmpty(),6000),"J02 Start exposes + after creating a project from group mode");
        snap("40-start-visible-plus");tapPlus();
        ui(()->{
            check(find(sheetRoot(),"Aggiungi un passaggio",false)!=null,"J03 single tap + opens the block picker immediately");
            check(find(sheetRoot(),"Aggiungi Inizio",true)==null,"J03 linked picker excludes a second Start");
        });snap("41-direct-plus-picker");tapSheetDescription("Aggiungi Output");
        ui(()->first[0]=(String)get(nodeOfKind("OUTPUT"),"id"));setSheetField("Valore o testo da mostrare","\"positivo\"");tapSheetText("Applica modifiche");
        assertConnection(start[0],"next",first[0],"J03 new Output is linked to Start");
        selectThroughUi(first[0]);tapPlus();tapSheetDescription("Aggiungi Fine");ui(()->end[0]=(String)get(nodeOfKind("END"),"id"));
        assertConnection(first[0],"next",end[0],"J03 + from Output closes the initial chain with Fine");
        selectThroughUi(start[0]);selectMode(true);
        check(await(()->!((RectF)get(canvas(),"plusHit")).isEmpty(),6000),"J02 Start + remains available for a single selected node in group mode");
        tapPlus();tapSheetDescription("Aggiungi Input");ui(()->input[0]=(String)get(nodeOfKind("INPUT"),"id"));tapSheetText("Applica modifiche");
        assertConnection(start[0],"next",input[0],"J05 insertion connects Start to the new Input");assertConnection(input[0],"next",first[0],"J05 insertion keeps the original continuation");
        tapDescription("Annulla ultima modifica");awaitCanvasSettled();assertConnection(start[0],"next",first[0],"J05 a single Undo restores the original Start edge");
        ui(()->check(call(get(activity,"document"),"find",input[0])==null,"J05 a single Undo removes the inserted Input"));
        tapDescription("Ripeti modifica");awaitCanvasSettled();assertConnection(input[0],"next",first[0],"J05 Redo restores the continuation and Input together");
        selectThroughUi(input[0]);tapPlus();tapSheetDescription("Aggiungi Decisione");
        ui(()->check(find(sheetRoot(),"Come continua il flusso?",false)!=null,"J06 inserting a Decision asks which branch keeps the continuation"));
        tapSheetText("Sì");ui(()->decision[0]=(String)get(nodeOfKind(3),"id"));
        final String trueName="Questo ramo ha un nome lungo da leggere fino alla fine: VERO";
        final String falseName="Questo ramo ha un nome lungo da leggere fino in fondo: FALSO";
        setSheetField("Nome dell'uscita: quando è vera",trueName);setSheetField("Nome dell'uscita: quando è falsa",falseName);
        tapSheetDescription("Quando è vera: colore Viola");tapSheetDescription("Quando è falsa: colore Arancione");tapSheetText("Applica modifiche");
        assertConnection(input[0],"next",decision[0],"J06 Decision is inserted after Input");assertConnection(decision[0],"true",first[0],"J06 chosen true branch retains the previous Output");
        selectThroughUi(decision[0]);connectSelection();
        ui(()->{check(textFits(requiredText(sheetRoot(),trueName)),"J15 complete long true label is readable in branch choice");check(textFits(requiredText(sheetRoot(),falseName)),"J15 complete long false label is readable in branch choice");});
        snap("42-complete-branch-labels");tapSheetDescription("Chiudi pannello");tapSidePlus(false);ui(()->check(find(sheetRoot(),"Scegli il ramo",false)==null,"J14 lateral No plus already determines the branch"));tapSheetDescription("Aggiungi Output");
        ui(()->{for(Object node:(List<?>)get(get(activity,"document"),"nodes"))if((Integer)get(node,"kind")==kind("OUTPUT")&&!get(node,"id").equals(first[0]))other[0]=(String)get(node,"id");});
        setSheetField("Valore o testo da mostrare","\"non positivo\"");tapSheetText("Applica modifiche");
        assertConnection(decision[0],"false",other[0],"J14 false branch leads to its own Output");
        openDestinationThroughUi(other[0],"next");chooseDestinationThroughUi(end[0]);
        assertConnection(other[0],"next",end[0],"J09 destination list connects to an existing shared Fine");
        openDestinationThroughUi(start[0],"next");chooseDestinationThroughUi(end[0]);
        ui(()->check(find(sheetRoot(),"Cambiare l’arrivo?",false)!=null,"J12 changing an occupied output asks before replacing its destination"));
        tapSheetText("Tieni il collegamento");assertConnection(start[0],"next",input[0],"J12 keeping the connection preserves the existing chain");
        openDestinationThroughUi(start[0],"next");chooseDestinationThroughUi(end[0]);tapSheetText("Cambia collegamento");
        assertConnection(start[0],"next",end[0],"J12 explicit replacement changes only the chosen output");tapDescription("Annulla ultima modifica");awaitCanvasSettled();
        assertConnection(start[0],"next",input[0],"J12 Undo restores the replaced destination");
        selectMode(false);tapEdgeThroughUi(start[0],"next");
        ui(()->{check(find(sheetRoot(),"Collegamento",false)!=null,"J16 tapping the drawn arrow opens its controls");check(full(requiredText(sheetRoot(),"Cambia arrivo")),"J16 Change destination is available from the arrow");});
        snap("43-arrow-controls");tapSheetText("Scollega");
        ui(()->check(call(get(activity,"document"),"outgoing",start[0],"next")==null,"J16 unlink removes just the selected arrow"));
        tapDescription("Annulla ultima modifica");awaitCanvasSettled();assertConnection(start[0],"next",input[0],"J16 Undo restores the removed arrow");
        menuAction("Verifica diagramma");check(await(()->get(activity,"activeSheet")!=null,15000),"J18 validation opens on the diagram built through the UI");ui(()->check(find(sheetRoot(),"Pronto da provare",false)!=null,"J18 user-built flow passes structure and expression validation"));snap("44-validation-of-built-flow");tapSheetDescription("Chiudi pannello");
        tap(viewOnUi("runButton"));settle();tap(viewOnUi("playPause"));
        check(await(()->state().equals("WAITING"),20000),"J39 the user-built flow reaches its Input");ui(()->((EditText)view("runInput")).setText("3"));tapDescription("Invia valore");
        check(await(()->state().equals("COMPLETED"),20000),"J39 true branch of the user-built flow completes");ui(()->check(text("outputText").trim().equals("positivo"),"J39 true branch gives the expected output"));snap("45-user-built-flow-run");
        tapDescription("Ricomincia esecuzione");tap(viewOnUi("playPause"));check(await(()->state().equals("WAITING"),15000),"J40 restarted flow reaches Input again");
        ui(()->((EditText)view("runInput")).setText("-2"));tapDescription("Invia valore");check(await(()->state().equals("COMPLETED"),20000),"J40 false branch of the user-built flow completes");
        ui(()->check(text("outputText").trim().equals("non positivo"),"J40 negative input selects the alternate branch correctly"));
        ui(()->call(activity,"closeRun"));settle();
    }
    private void tapText(String text)throws Exception{final View[] target={null};ui(()->target[0]=requiredText(decor(),text));reach(target[0]);tap(target[0]);settle();}
    private void selectThroughUi(String id)throws Exception{final float[][] point={null};ui(()->call(canvas(),"focusNodeAnimated",id));awaitCanvasSettled();ui(()->{call(canvas(),"clearSelection");point[0]=nodePoint(id);});settle();tapAt(point[0][0],point[0][1]);settle();ui(()->check(((Set<?>)call(canvas(),"getSelectedIds")).contains(id),"Native touch selects the requested visible node"));}
    private void tapPlus()throws Exception{check(await(()->!((RectF)get(canvas(),"plusHit")).isEmpty(),6000),"The selected node has a visible + touch target");final float[] p=new float[2];ui(()->{RectF r=(RectF)get(canvas(),"plusHit");Rect c=bounds((View)canvas());p[0]=c.left+r.centerX();p[1]=c.top+r.centerY();check(p[0]>=c.left&&p[0]<c.right&&p[1]>=c.top&&p[1]<c.bottom,"+ target lies inside the visible canvas");});tapAt(p[0],p[1]);check(await(()->get(activity,"activeSheet")!=null,8000),"A real + tap opens its native panel");settle();}
    private void assertConnection(String source,String branch,String target,String message){ui(()->{Object edge=call(get(activity,"document"),"outgoing",source,branch);check(edge!=null&&get(edge,"to").equals(target),message);});}
    private void tapSidePlus(boolean yes)throws Exception{String name=yes?"truePlusHit":"falsePlusHit";check(await(()->!((RectF)get(canvas(),name)).isEmpty(),6000),"Decision exposes its lateral "+(yes?"Sì":"No")+" plus");final float[] p=new float[2];ui(()->{RectF r=(RectF)get(canvas(),name);Rect c=bounds((View)canvas());p[0]=c.left+r.centerX();p[1]=c.top+r.centerY();check(p[0]>=c.left&&p[0]<c.right&&p[1]>=c.top&&p[1]<c.bottom,"Lateral plus is visible on the actual canvas");});tapAt(p[0],p[1]);check(await(()->get(activity,"activeSheet")!=null,8000),"Native side-plus tap opens its branch-specific block picker");settle();}
    private int choiceColor(View row)throws Exception{return ((android.graphics.drawable.GradientDrawable)((View)get(row,"marker")).getBackground()).getColor().getDefaultColor();}
    private View findTag(View root,Object tag){if(tag.equals(root.getTag()))return root;if(root instanceof ViewGroup)for(int i=0;i<((ViewGroup)root).getChildCount();i++){View found=findTag(((ViewGroup)root).getChildAt(i),tag);if(found!=null)return found;}return null;}
    private void tapSheetTag(String tag)throws Exception{final View[] target={null};check(await(()->get(activity,"activeSheet")!=null&&findTag(sheetRoot(),tag)!=null,10000),"Destination or branch row is present");ui(()->target[0]=findTag(sheetRoot(),tag));reach(target[0]);tap(target[0]);settle();}
    private void openDestinationThroughUi(String source,String branch)throws Exception{selectThroughUi(source);connectSelection();tapSheetTag(branch);ui(()->check(find(sheetRoot(),"Scegli l’arrivo",false)!=null,"J09 Collega opens a readable list of existing destinations"));}
    private void chooseDestinationThroughUi(String id)throws Exception{final String[] name={null};ui(()->name[0]=(String)get(call(get(activity,"document"),"find",id),"text"));setSheetField("Cerca un blocco",name[0]);tapSheetTag(id);}
    private void tapEdgeThroughUi(String source,String branch)throws Exception{
        ui(()->{call(canvas(),"focusNodeAnimated",source);call(canvas(),"clearSelection");});awaitCanvasSettled();settle();final float[] point={Float.NaN,Float.NaN};
        ui(()->{
            Object edge=call(get(activity,"document"),"outgoing",source,branch),visual=((Map<?,?>)get(canvas(),"edgeIndex")).get(get(edge,"id"));
            PathMeasure measure=new PathMeasure((Path)get(visual,"path"),false);float[] xy=new float[2];Rect c=bounds((View)canvas());float density=activity.getResources().getDisplayMetrics().density;
            for(int i=2;i<19;i++){measure.getPosTan(measure.getLength()*i/20f,xy,null);float sx=(Float)call(canvas(),"screenX",xy[0]),sy=(Float)call(canvas(),"screenY",xy[1]);
                if(sx<12*density||sx>c.width()-12*density||sy<70*density||sy>c.height()-84*density||call(canvas(),"hitNode",xy[0],xy[1])!=null)continue;
                Object hit=call(canvas(),"hitEdge",xy[0],xy[1]);if(hit==visual){point[0]=c.left+sx;point[1]=c.top+sy;break;}
            }
            check(!Float.isNaN(point[0]),"J16 an exposed arrow segment is visible and distinct from nodes/buttons");
        });
        if(Float.isNaN(point[0]))throw new IllegalStateException("No native arrow tap point");tapAt(point[0],point[1]);check(await(()->get(activity,"activeSheet")!=null,8000),"J16 real tap on exposed segment opens a panel");settle();
    }

    /** J31-J35: actual project menu, Storage Access Framework and chooser round trip. */
    private void filesJourney()throws Exception{
        fixture("parity");
        final String title="Frociart-QA-"+scenario+"-"+System.currentTimeMillis();
        renameThroughUi(title);
        ui(()->check(get(get(activity,"document"),"title").equals(title),"J31 rename from project menu is applied"));
        final String[] savedId={null},savedDocument={null};ui(()->{savedId[0]=(String)get(activity,"projectId");savedDocument[0]=encodedDocument();});
        for(String[] format:new String[][]{{"Progetto modificabile · .frociart","frociart"},{"PDF · pagina unica","pdf"},{"PDF · più pagine A4","pdf","-A4"},{"Immagine PNG","png"},{"Disegno vettoriale SVG","svg"}}){
            menuAction("Esporta o condividi");tapSheetText(format[0]);
            check(awaitSystemPackage("com.android.documentsui",20000),"J31 native save selector opens for "+format[1]);
            check(systemTextContains(title+"."+format[1]),"J31 save selector suggests the correct "+format[1]+" filename");
            if(format.length>2){android.view.accessibility.AccessibilityNodeInfo root=getUiAutomation().getRootInActiveWindow(),filename=null;if(root!=null)for(android.view.accessibility.AccessibilityNodeInfo node:root.findAccessibilityNodeInfosByViewId("android:id/title"))if(node.isEditable()&&node.isVisibleToUser()&&node.getText()!=null&&(title+"."+format[1]).contentEquals(node.getText())){filename=node;break;}Bundle replacement=new Bundle();replacement.putCharSequence(android.view.accessibility.AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE,title+format[2]+"."+format[1]);check(filename!=null&&filename.performAction(android.view.accessibility.AccessibilityNodeInfo.ACTION_SET_TEXT,replacement),"J31 native A4 filename can be edited to keep both PDFs");}
            if(format[1].equals("frociart"))snapExternal("50-native-save-project");
            clickSystemButton("com.android.documentsui:id/button_save",new String[]{"SAVE","Save","SALVA","Salva"});
            check(awaitSystemPackage(getTargetContext().getPackageName(),20000),"J31 returns to editor after saving "+format[1]);
            check(await(()->get(activity,"pendingExportPath")==null,10000),"J31 save result accepted for "+format[1]);
            pause(600);
        }
        menuAction("Esporta o condividi");tapSheetText("Condividi progetto .frociart");
        check(await(()->!(Boolean)get(activity,"saving"),15000),"J35 share export preparation completes");
        check(awaitSystemNotPackage(getTargetContext().getPackageName(),12000),"J35 native Android share chooser opens");
        snapExternal("51-native-share-chooser");sendKeyDownUpSync(KeyEvent.KEYCODE_BACK);
        check(awaitSystemPackage(getTargetContext().getPackageName(),12000),"J35 cancelling chooser returns without selecting a recipient");
        tapDescription("Menu progetto");tapSheetText("I miei progetti");tapSheetText("Apri dal telefono");
        check(awaitSystemPackage("com.android.documentsui",15000),"J32 native open selector opens");
        check(awaitSystemText(title+".frociart",15000),"J32 saved .frociart is present in native files");
        clickSystemText(title+".frociart");
        check(awaitSystemPackage(getTargetContext().getPackageName(),20000),"J32 returns from native file selection");
        check(await(()->get(activity,"lastImportedUri")!=null&&!savedId[0].equals(get(activity,"projectId")),20000),"J32 saved project imports as a separate local project");
        ui(()->{
            testProjects.add((String)get(activity,"projectId"));Object doc=get(activity,"document");
            check(get(doc,"title").equals(title)&&((List<?>)get(doc,"nodes")).size()==6,"J32 import preserves project title and all six nodes");
            check(savedDocument[0].equals(encodedDocument()),"J32 complete codec fingerprint matches: node IDs, kinds, texts, prompts, coordinates, branch names, colors and all edges");
            check(!(Boolean)get(activity,"autoArrange"),"J32 import retains manual placement mode");
            android.net.Uri uri=(android.net.Uri)get(activity,"lastImportedUri");
            try(InputStream in=activity.getContentResolver().openInputStream(uri)){
                check(in!=null&&in.read()=='{',"J32 actual SAF document contains editable JSON bytes");
            }
        });
        snap("52-imported-project");
        menuAction("Esporta o condividi");tapSheetText("Progetto modificabile · .frociart");
        check(awaitSystemPackage("com.android.documentsui",15000),"J34 cancel-save selector opens");
        sendKeyDownUpSync(KeyEvent.KEYCODE_BACK);check(awaitSystemPackage(getTargetContext().getPackageName(),10000),"J34 cancelling save returns safely to editor");
        ui(()->check(get(get(activity,"document"),"title").equals(title),"J34 cancel-save leaves project unchanged"));
    }

    /** J24-J25: two separate node drafts survive changing projects and recreation. */
    private void draftJourney()throws Exception{
        final String title="Frociart-bozze-"+scenario+"-"+System.currentTimeMillis();renameThroughUi(title);
        final String[] process={null},output={null};final String[] oldOutput={null};
        ui(()->{process[0]=(String)get(nodeOfKind(2),"id");output[0]=(String)get(nodeOfKind("OUTPUT"),"id");oldOutput[0]=(String)get(nodeOfKind("OUTPUT"),"text");});
        editThroughUi(process[0]);setSheetField("Operazione","totale = 19 + 23");tapSheetDescription("Chiudi pannello");
        editThroughUi(output[0]);setSheetField("Valore o testo da mostrare","\"Bozza del risultato\"");tapSheetDescription("Chiudi pannello");
        newProjectThroughUi();
        tapDescription("Scegli progetto");check(await(()->find(sheetRoot(),title,false)!=null,10000),"J24 original project appears in the local project list");tapSheetText(title);
        check(await(()->get(activity,"activeSheet")==null&&get(get(activity,"document"),"title").equals(title),12000),"J24 original project reopens from its visible row");
        editThroughUi(process[0]);
        ui(()->check(findEdit(sheetRoot(),"Operazione").getText().toString().equals("totale = 19 + 23"),"J24 first node draft survives second draft and switching projects"));
        tapSheetText("Applica modifiche");
        ui(()->check(get(call(get(activity,"document"),"find",process[0]),"text").equals("totale = 19 + 23"),"J24 applying the recovered draft changes the correct node"));
        editThroughUi(output[0]);
        ui(()->check(findEdit(sheetRoot(),"Valore o testo da mostrare").getText().toString().equals("\"Bozza del risultato\""),"J24 second draft remains after applying the first"));
        snap("53-restored-node-draft");tapSheetText("Scarta questa modifica");editThroughUi(output[0]);
        ui(()->check(findEdit(sheetRoot(),"Valore o testo da mostrare").getText().toString().equals(oldOutput[0]),"J25 discard removes only this draft and restores committed content"));
        tapSheetDescription("Chiudi pannello");editThroughUi(process[0]);setSheetField("Operazione","totale = 40 + 2");
        recreateActivity();
        check(await(()->get(activity,"activeSheet")!=null,10000),"J24 node editor returns after Activity recreation");
        ui(()->check(findEdit(sheetRoot(),"Operazione").getText().toString().equals("totale = 40 + 2"),"J24 in-progress draft survives a real Activity recreation"));
        tapSheetText("Applica modifiche");
    }

    private void renameThroughUi(String title)throws Exception{
        tapDescription("Menu progetto");tapSheetText("Rinomina progetto");setSheetField("Nome del progetto",title);tapSheetText("Salva nome");
    }
    private void newProjectThroughUi()throws Exception{
        tapDescription("Scegli progetto");tapSheetText("Nuovo diagramma");
        check(await(()->get(activity,"activeSheet")==null&&((List<?>)get(get(activity,"document"),"nodes")).size()==1,10000),"J02 new project opens from the real project menu");
        ui(()->testProjects.add((String)get(activity,"projectId")));awaitCanvasSettled();
    }
    private void editThroughUi(String id)throws Exception{
        final float[][] point={null};ui(()->call(canvas(),"focusNodeAnimated",id));awaitCanvasSettled();
        ui(()->{call(canvas(),"clearSelection");point[0]=nodePoint(id);});settle();tapAt(point[0][0],point[0][1]);settle();
        tap(viewOnUi("selectionEdit"));check(await(()->get(activity,"activeSheet")!=null,8000),"Node editor opens from the selected node toolbar");
    }
    private void setSheetField(String name,String value)throws Exception{ui(()->findEdit(sheetRoot(),name).setText(value));settle();}
    private void menuAction(String label)throws Exception{tapDescription("Menu progetto");tapSheetText(label);}
    private void connectSelection()throws Exception{
        final boolean[] direct={false};ui(()->direct[0]=view("selectionConnect").getVisibility()==View.VISIBLE);
        if(direct[0])tap(viewOnUi("selectionConnect"));else{tapDescription("Altre azioni sulla selezione");tapSheetText("Collegamenti");}settle();
    }
    private void selectMode(boolean selected)throws Exception{
        final boolean[] current={false};ui(()->current[0]=(Boolean)call(canvas(),"isSelectionMode"));
        if(current[0]!=selected)menuAction(current[0]?"Termina selezione multipla":"Seleziona più blocchi");
    }
    private void mobileReview()throws Exception{
        ui(()->{dismiss();if((Boolean)get(activity,"runVisible"))call(activity,"closeRun");call(canvas(),"clearSelection");});settle();
        ui(()->{
            check(((ViewGroup)view("canvasFrame")).getChildCount()==1,"Drawing has no overlaid command panels");
            check(view("selectionBar").getParent()==view("actionDock"),"Selection actions have their dedicated bottom dock");
            check(!Rect.intersects(bounds((View)canvas()),bounds(view("actionDock"))),"Bottom actions do not cover the diagram");
            check(full(view("gridButton"))&&full(requiredDescription(decor(),"Mostra tutto il diagramma")),"Grid and overview visible on phone");
            check(textFits(view("gridButton")),"Grid state remains legible at the configured text size");
            check(full(view("arrangeButton"))&&textFits(view("arrangeButton")),"Riordina is directly visible and legible");
        });snap("80-mobile-hierarchy");
        arrangementReview();constructionReview();
        menuAction("Esempi da provare");tapSheetText("Report di misure");settle();ui(()->testProjects.add((String)get(activity,"projectId")));
        tapDescription("Mostra tutto il diagramma");
        ui(()->{
            android.graphics.RectF b=(android.graphics.RectF)call(canvas(),"getDiagramBounds");
            float scale=(Float)get(canvas(),"zoom")*activity.getResources().getDisplayMetrics().density;
            float x=(Float)get(canvas(),"panX"),y=(Float)get(canvas(),"panY");
            check(b.left*scale+x>=-1&&b.right*scale+x<=((View)canvas()).getWidth()+1,"Full overview contains horizontal routes");
            check(b.top*scale+y>=-1&&b.bottom*scale+y<=((View)canvas()).getHeight()+1,"Full overview contains first and last blocks");
            check((Float)get(canvas(),"zoom")<.8f,"Phone overview crosses the former 80 percent limit");
        });snap("81-full-report-overview");
        menuAction("Esempi da provare");tapSheetText("Maggiorenne o minorenne");settle();ui(()->testProjects.add((String)get(activity,"projectId")));tapText("Prova");
        for(int age:new int[]{17,18,19}){
            if(age!=17)tapDescription("Ricomincia esecuzione");tapText("Esegui");
            check(await(()->get(get(activity,"runner"),"state").toString().equals("WAITING"),12000),"Age example asks for input");
            final String value=Integer.toString(age);ui(()->((EditText)view("runInput")).setText(value));tapDescription("Invia valore");
            check(await(()->get(get(activity,"runner"),"state").toString().equals("COMPLETED"),12000),"Age example completes: "+age);
            ui(()->check(((TextView)view("outputText")).getText().toString().contains(age<18?"Minorenne":"Maggiorenne"),"Actual UI result respects age threshold"));
        }snap("82-age-boundary-result");ui(()->call(activity,"closeRun"));settle();
    }

    /** A real tap performs a single, reversible coordinate change without changing auto mode. */
    private void arrangementReview()throws Exception{
        final String[] before={null};final int[] history={0};
        ui(()->{
            call(activity,"setAutoArrange",false);call(activity,"onBeforeChange");Object first=((List<?>)get(get(activity,"document"),"nodes")).get(0);
            set(first,"x",-710f);set(first,"y",460f);call(activity,"changed","QA manual placement");
            before[0]=encodedDocument();history[0]=((Collection<?>)get(activity,"undo")).size();
        });settle();tapDescription("Riordina i blocchi una volta");
        ui(()->{
            check(!before[0].equals(encodedDocument()),"Riordina changes actual document coordinates");
            check(!(Boolean)get(activity,"autoArrange"),"One-shot arrange preserves free placement preference");
            check(((Collection<?>)get(activity,"undo")).size()==history[0]+1,"Arrange adds exactly one undo transaction");
        });tapDescription("Annulla ultima modifica");ui(()->check(before[0].equals(encodedDocument()),"One Undo restores the complete pre-arrange document"));
        tapDescription("Ripeti modifica");ui(()->history[0]=((Collection<?>)get(activity,"undo")).size());
        tapDescription("Riordina i blocchi una volta");ui(()->check(((Collection<?>)get(activity,"undo")).size()==history[0],"Already arranged diagram does not add empty history"));
    }
    /** Construct, check and resume a real exercise; correct feedback alone cannot complete a blank seed. */
    private void constructionReview()throws Exception{
        final String[] id={null},saved={null},input={null};
        ui(()->{
            Object lesson=Class.forName("it.frociart.app.FlowLearning",true,activity.getClassLoader()).getMethod("lesson",String.class).invoke(null,"l01");
            call(activity,"practiceLesson",lesson);id[0]=(String)get(activity,"projectId");testProjects.add(id[0]);
            check(get(activity,"mission")!=null,"Lesson opens an actual construction mission");
            check(!((List<?>)get(get(activity,"document"),"nodes")).isEmpty(),"Mission opens editable starter blocks");
            for(Object n:(List<?>)get(get(activity,"document"),"nodes"))if(get(n,"text").equals("b"))input[0]=(String)get(n,"id");
        });tapSheetText("Costruisci il flowchart");tapDescription("Controlla il flowchart dell'esercizio");
        check(await(()->get(activity,"activeSheet")!=null,10000),"Actual checker returns recoverable feedback for incomplete diagram");
        ui(()->check(find(sheetRoot(),"Flowchart riuscito!",false)==null,"Incomplete starter never shows completion"));tapSheetText("Torna al foglio");
        selectThroughUi(input[0]);tapDescription("Aggiungi un passaggio dopo il blocco selezionato");tapSheetDescription("Aggiungi Operazione");
        setSheetField("Operazione","totale = a + b");tapSheetText("Applica modifiche");
        final String[] calc={null};ui(()->{for(Object n:(List<?>)get(get(activity,"document"),"nodes"))if(get(n,"text").equals("totale = a + b"))calc[0]=(String)get(n,"id");});
        selectThroughUi(calc[0]);tapDescription("Aggiungi un passaggio dopo il blocco selezionato");tapSheetDescription("Aggiungi Output");
        setSheetField("Valore o testo da mostrare","totale");tapSheetText("Applica modifiche");
        tapDescription("Controlla il flowchart dell'esercizio");check(await(()->get(activity,"activeSheet")!=null,10000),"Built diagram is checked through the real lesson action");
        ui(()->{check(find(sheetRoot(),"Flowchart riuscito!",false)!=null,"Created flowchart passes actual execution cases");saved[0]=encodedDocument();});tapSheetText("Torna al foglio");
        recreateActivity();ui(()->{check(id[0].equals(get(activity,"projectId")),"Exercise project survives recreation");check(saved[0].equals(encodedDocument()),"Built diagram survives save/reopen unchanged");check(get(activity,"mission")!=null,"Exercise objective survives recreation");});snap("83-built-learning-flowchart");
    }
    private void tapDescription(String label)throws Exception{final View[] target={null};ui(()->target[0]=requiredDescription(decor(),label));reach(target[0]);tap(target[0]);settle();}
    private void tapSheetText(String label)throws Exception{final View[] target={null};check(await(()->get(activity,"activeSheet")!=null,8000),"Native sheet is open for "+label);ui(()->target[0]=requiredText(sheetRoot(),label));reach(target[0]);tap(target[0]);settle();}
    private void tapSheetDescription(String label)throws Exception{final View[] target={null};ui(()->target[0]=requiredDescription(sheetRoot(),label));reach(target[0]);tap(target[0]);settle();}
    private String systemPackage(){android.view.accessibility.AccessibilityNodeInfo root=getUiAutomation().getRootInActiveWindow();return root==null?"":String.valueOf(root.getPackageName());}
    private boolean awaitSystemPackage(String name,long timeout){long end=SystemClock.uptimeMillis()+timeout;do{if(name.equals(systemPackage()))return true;pause(180);}while(SystemClock.uptimeMillis()<end);return false;}
    private boolean awaitSystemNotPackage(String name,long timeout){long end=SystemClock.uptimeMillis()+timeout;do{String pkg=systemPackage();if(!pkg.isEmpty()&&!pkg.equals(name)&&!pkg.equals("com.android.inputmethod.latin"))return true;pause(180);}while(SystemClock.uptimeMillis()<end);return false;}
    private android.view.accessibility.AccessibilityNodeInfo systemText(String text){android.view.accessibility.AccessibilityNodeInfo root=getUiAutomation().getRootInActiveWindow();if(root==null)return null;for(android.view.accessibility.AccessibilityNodeInfo node:root.findAccessibilityNodeInfosByText(text))if(node.isVisibleToUser()&&node.getText()!=null&&text.contentEquals(node.getText()))return node;return null;}
    private boolean awaitSystemText(String text,long timeout){long end=SystemClock.uptimeMillis()+timeout;do{if(systemText(text)!=null)return true;pause(180);}while(SystemClock.uptimeMillis()<end);return false;}
    private boolean systemTextContains(String text){return systemText(text)!=null;}
    private void clickSystemText(String text)throws Exception{
        for(int attempt=0;attempt<8;attempt++){
            android.view.accessibility.AccessibilityNodeInfo root=getUiAutomation().getRootInActiveWindow(),node=systemText(text);Rect window=new Rect(),r=new Rect();if(root!=null)root.getBoundsInScreen(window);
            if(node!=null){android.view.accessibility.AccessibilityNodeInfo target=node;for(android.view.accessibility.AccessibilityNodeInfo parent=node.getParent();parent!=null;parent=parent.getParent())if("com.android.documentsui:id/item_root".equals(parent.getViewIdResourceName())){target=parent;break;}target.getBoundsInScreen(r);int inset=(int)(48*activity.getResources().getDisplayMetrics().density);if(r.height()>0&&r.top>=window.top&&r.bottom<=window.bottom-inset){report.append("FILE_CARD ").append(target.getViewIdResourceName()).append(' ').append(r).append('\n');if(target.isClickable()&&target.performAction(android.view.accessibility.AccessibilityNodeInfo.ACTION_CLICK)){check(true,"Native file card accepts its accessible Open action");settle();return;}tapAt(r.exactCenterX(),r.exactCenterY());settle();return;}if(target.performAction(android.view.accessibility.AccessibilityNodeInfo.AccessibilityAction.ACTION_SHOW_ON_SCREEN.getId())){settle();continue;}}
            boolean scrolled=false;if(root!=null)for(android.view.accessibility.AccessibilityNodeInfo list:root.findAccessibilityNodeInfosByViewId("com.android.documentsui:id/dir_list"))if(list.performAction(android.view.accessibility.AccessibilityNodeInfo.ACTION_SCROLL_FORWARD)){scrolled=true;break;}
            if(!scrolled)break;settle();
        }throw new IllegalStateException("Native file title could not be brought fully on screen: "+text);
    }
    private void clickSystemButton(String resource,String[] labels)throws Exception{
        android.view.accessibility.AccessibilityNodeInfo root=getUiAutomation().getRootInActiveWindow(),chosen=null;
        if(root!=null)for(android.view.accessibility.AccessibilityNodeInfo node:root.findAccessibilityNodeInfosByViewId(resource))if(node.isVisibleToUser()&&node.isEnabled()){chosen=node;break;}
        if(chosen==null)for(String label:labels){chosen=systemText(label);if(chosen!=null)break;}
        if(chosen==null)throw new IllegalStateException("Native system action missing: "+Arrays.toString(labels));Rect r=new Rect();chosen.getBoundsInScreen(r);tapAt(r.exactCenterX(),r.exactCenterY());settle();
    }
    private void snapExternal(String name)throws Exception{
        pause(650);Bitmap bitmap=getUiAutomation().takeScreenshot();if(bitmap==null)throw new IOException("Missing native screenshot");try(FileOutputStream out=new FileOutputStream(new File(directory,name+".png"))){bitmap.compress(Bitmap.CompressFormat.PNG,100,out);}bitmap.recycle();
        StringBuilder description=new StringBuilder("ACTIVE_PACKAGE "+systemPackage()+"\n");systemTree(getUiAutomation().getRootInActiveWindow(),description);
        try(FileOutputStream out=new FileOutputStream(new File(directory,name+".txt"))){out.write(description.toString().getBytes("UTF-8"));}
    }
    private void systemTree(android.view.accessibility.AccessibilityNodeInfo node,StringBuilder out){if(node==null)return;Rect r=new Rect();node.getBoundsInScreen(r);out.append(node.getClassName()).append(' ').append(r).append(' ').append(node.getViewIdResourceName()).append(" text=").append(node.getText()).append(" desc=").append(node.getContentDescription()).append('\n');for(int i=0;i<node.getChildCount();i++)systemTree(node.getChild(i),out);}

    /** Rechecks only the concrete final layout/capture issues, not all 90 gates. */
    private void focusedLayout()throws Exception{
        if(phase.equals("layout"))starterRun();
        fixture("parity");theme("Scuro");ui(()->call(activity,"editNode",get(nodeOfKind(3),"id")));settle();
        android.accessibilityservice.AccessibilityServiceInfo info=getUiAutomation().getServiceInfo();info.flags|=android.accessibilityservice.AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS;getUiAutomation().setServiceInfo(info);
        openKeyboard("Condizione",true);
        final Rect[] last={null};final int[] stable={0};
        boolean shown=await(()->{
            Rect rect=keyboardBounds();if(rect==null||rect.height()<120){stable[0]=0;return false;}
            if(rect.equals(last[0]))stable[0]++;else stable[0]=0;last[0]=rect;
            return imeVisible(sheetRoot())&&stable[0]>=4;
        },15000);
        check(shown,"Editor IME has a stable visible native window");pause(1200);
        ui(()->{
            Rect ime=keyboardBounds(),apply=bounds(requiredText(sheetRoot(),"Applica modifiche"));
            report.append("EDITOR_IME_BOUNDS ").append(ime).append(" APPLY_BOUNDS ").append(apply).append('\n');
            check(ime!=null&&apply.bottom<=ime.top,"Editor Apply lies completely above native IME window");
            check(full(requiredText(sheetRoot(),"Applica modifiche")),"Editor Apply fully inside its visible window");
        });snap("14-editor-ime-stable");hideKeyboard();
        if(phase.equals("layout")){
            landscape();ui(()->{check(full(requiredText(sheetRoot(),"Applica modifiche")),"Landscape editor Apply fits system navigation insets");check(full(sheetRoot()),"Landscape editor sheet fits available window");});snap("15-editor-landscape-insets");
            portrait();ui(()->check(get(activity,"activeSheet")!=null,"Editor remains open after returning to portrait"));
        }
    }

    private Rect keyboardBounds(){for(android.view.accessibility.AccessibilityWindowInfo window:getUiAutomation().getWindows())if(window.getType()==android.view.accessibility.AccessibilityWindowInfo.TYPE_INPUT_METHOD){Rect bounds=new Rect();window.getBoundsInScreen(bounds);return bounds;}return null;}

    private void selectionBounds()throws Exception{
        final String[] id={null};final Rect[] before={null};
        ui(()->{Object node=nodeOfKind(2);id[0]=(String)get(node,"id");call(canvas(),"focusNode",id[0]);call(canvas(),"clearSelection");});settle();
        final float[][] target={null};ui(()->{check(((Set<?>)call(canvas(),"getSelectedIds")).isEmpty(),"No selection exists before native tap");before[0]=bounds((View)canvas());target[0]=nodePoint(id[0]);});tapAt(target[0][0],target[0][1]);settle();
        ui(()->{
            check(((Set<?>)call(canvas(),"getSelectedIds")).contains(id[0]),"Real touch selects the focused block");
            check(view("selectionBar").getVisibility()==View.VISIBLE,"Selection actions appear in the bottom dock");
            check(before[0].equals(bounds((View)canvas())),"Selecting a block does not resize or move canvas");
            check(full(view("selectionEdit"))&&textFits(view("selectionEdit")),"Selection edit is visible and its label fits");
            if((Boolean)call(activity,"inlineConnectionFits"))check(full(view("selectionConnect"))&&textFits(view("selectionConnect")),"Direct connect action is visible and fits");
            else check(full(view("selectionMore")),"Large text keeps connection actions reachable through selection menu");
        });snap("02-selection-overlay");
        ui(()->view("selectionClose").performClick());settle();
        ui(()->check(before[0].equals(bounds((View)canvas())),"Clearing selection keeps canvas bounds"));selectMode(true);ui(()->check((Boolean)call(canvas(),"isSelectionMode"),"Menu enables group mode"));selectMode(false);ui(()->check(!(Boolean)call(canvas(),"isSelectionMode"),"Menu restores pan mode"));
    }

    private void picker()throws Exception{
        ui(()->requiredText(decor(),"Blocco").performClick());settle();
        ui(()->{for(String label:new String[]{"Inizio","Input","Operazione","Decisione","Output","Fine"})check(full(requiredDescription(sheetRoot(),"Aggiungi "+label)),"Picker option fully visible: "+label);});
        snap("03-six-block-picker");ui(()->dismiss());
    }

    private void starterRun()throws Exception{
        ui(()->view("runButton").performClick());settle();
        ui(()->{
            check((Boolean)get(activity,"runVisible"),"Prova opens integrated panel");check(get(activity,"activeSheet")==null,"Prova keeps the drawing outside modal dialogs");
            check((Boolean)call(canvas(),"isReadOnly"),"Prova makes canvas read-only");check(!(Boolean)get(activity,"running"),"Opening Prova waits for Esegui or Un passo");
            check(full(view("playPause"))&&full(view("stepButton")),"Run and step controls fully visible");check(textFits(view("playPause"))&&textFits(view("stepButton")),"Run and step labels are not truncated");view("playPause").performClick();
        });
        check(await(()->state().equals("COMPLETED"),25000),"Starter run reaches COMPLETED");settle();
        ui(()->{check(text("outputText").trim().equals("5"),"Starter output is 5");check(full(view("outputText")),"Output 5 fully visible without scrolling");check(full((View)canvas())&&((View)canvas()).getHeight()>0,"Diagram remains visible with Prova");});snap("04-run-output-five");
        ui(()->{call(activity,"closeRun");check(!(Boolean)call(canvas(),"isReadOnly"),"Closing Prova re-enables editing");check(view("dock").getVisibility()==View.VISIBLE,"Closing Prova restores main actions");});settle();
    }
    private void detailsReview()throws Exception{
        ui(()->{view("runButton").performClick();});settle();
        ui(()->{check(full(view("runDetailsButton")),"Result details action is fully visible");check(textFits(view("runDetailsButton")),"Result details label fits the system font scale");view("runDetailsButton").performClick();});settle();
        ui(()->check(textFits(view("runDetailsButton")),"Expanded result details label also fits the system font scale"));
        final View[] velocity={null};ui(()->velocity[0]=requiredDescription(decor(),"Cambia velocità esecuzione"));reach(velocity[0]);settle();
        ui(()->{check(full(velocity[0]),"Speed control is reachable in the expanded details");check(textFits(velocity[0]),"Speed label fits the system font scale");});snap("84-details-expanded");
    }

    private void inputRun()throws Exception{
        fixture("sum");ui(()->view("runButton").performClick());settle();
        for(int i=0;i<6&&!readState().equals("WAITING");i++){ui(()->view("stepButton").performClick());settle();}
        ui(()->{check(state().equals("WAITING"),"Un passo reaches Input");check(!(Boolean)get(activity,"running"),"Manual Input begins paused");check(view("runControls").getVisibility()==View.GONE,"Waiting for input hides disabled run buttons");});
        openKeyboard("runInput",false);
        ui(()->{((EditText)view("runInput")).setText("7");check(full(view("runInput")),"Input field fully visible above keyboard in portrait");check(full(requiredDescription(decor(),"Invia valore")),"Input send button fully visible above keyboard in portrait");});snap("06-input-ime-portrait");
        hideKeyboard();theme("Chiaro");
        ui(()->{check(text("runInput").equals("7"),"Typed Input survives theme change");check((Boolean)get(activity,"runVisible")&&(Boolean)call(canvas(),"isReadOnly"),"Theme change preserves integrated Prova and read-only state");});snap("07-input-light");landscape();
        ui(()->{check(text("runInput").equals("7"),"Typed Input survives landscape rotation");check((Boolean)get(activity,"runVisible"),"Landscape preserves Prova");check((Boolean)call(canvas(),"isReadOnly"),"Landscape keeps Prova canvas read-only");});openKeyboard("runInput",false);
        ui(()->{check(full(view("runInput")),"Input field fully visible above keyboard in landscape");check(full(requiredDescription(decor(),"Invia valore")),"Input send button fully visible above keyboard in landscape");check(((View)canvas()).getWidth()>0&&((View)canvas()).getHeight()>0,"Landscape keyboard leaves a drawing viewport");});snap("08-input-ime-landscape");
        sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_BACK);check(await(()->!imeVisible(decor()),8000),"Native Back closes landscape keyboard");settle();
        ui(()->{check((Boolean)get(activity,"runVisible"),"Back from keyboard keeps Prova open");check(full(view("projectHeader"))&&full(view("runHeader")),"Project and Prova headers return after keyboard Back");});snap("08b-input-landscape-after-back");openKeyboard("runInput",false);
        final View[] send={null};final int[] steps={0};ui(()->{send[0]=requiredDescription(decor(),"Invia valore");steps[0]=(Integer)get(get(activity,"runner"),"steps");});tap(send[0]);settle();
        ui(()->{check(!(Boolean)get(activity,"running"),"Submitting Input reached by Un passo stays paused");check(state().equals("RUNNING"),"Valid Input is accepted by a real tap on Invia");check((Integer)get(get(activity,"runner"),"steps")==steps[0]+1,"Submitting Input advances exactly one block");Object value=((Map<?,?>)get(get(activity,"runner"),"variables")).get("a");check(value!=null&&call(value,"format").equals("7"),"Submitted value is stored in variable a");});pause(1200);
        ui(()->{check(!(Boolean)get(activity,"running"),"Manual execution does not restart later");check(steps[0]+1==(Integer)get(get(activity,"runner"),"steps"),"No automatic step after manual Input");});hideKeyboard();portrait();ui(()->call(activity,"closeRun"));
    }

    private void errorRecovery()throws Exception{
        ui(()->{Object d=call(activity,"starter");for(Object node:(List<?>)get(d,"nodes"))if(((Integer)get(node,"kind"))==2)set(node,"text","totale = 1 / 0");call(activity,"switchDocument",d,testId);view("runButton").performClick();view("playPause").performClick();});
        check(await(()->state().equals("ERROR"),20000),"Runtime division by zero reaches ERROR");
        ui(()->{call(activity,"closeRun");view("runButton").performClick();check((Boolean)get(activity,"runVisible")&&state().equals("ERROR"),"Runtime error can reopen Prova");check(requiredDescription(decor(),"Ricomincia esecuzione").isEnabled(),"Reset is available after reopening error");requiredDescription(decor(),"Ricomincia esecuzione").performClick();check(state().equals("READY"),"Reset clears runtime error");check(view("playPause").isEnabled()&&view("stepButton").isEnabled(),"Reset restores both execution controls");});settle();snap("09-error-reset");ui(()->call(activity,"closeRun"));
    }

    private void decisionEditor()throws Exception{
        fixture("parity");theme("Scuro");final String[] id={null};ui(()->{id[0]=(String)get(nodeOfKind(3),"id");call(activity,"editNode",id[0]);});settle();
        ui(()->{
            findEdit(sheetRoot(),"Condizione").setText("numero % 2 = 0");findEdit(sheetRoot(),"Nome dell'uscita: quando è vera").setText("Pari & zero");findEdit(sheetRoot(),"Nome dell'uscita: quando è falsa").setText("Dispari");
            requiredDescription(sheetRoot(),"Quando è vera: colore Viola").performClick();requiredDescription(sheetRoot(),"Quando è falsa: colore Arancione").performClick();findEdit(sheetRoot(),"Condizione").requestRectangleOnScreen(new Rect(0,0,1,1),true);
        });settle();openKeyboard("Condizione",true);ui(()->check(full(requiredText(sheetRoot(),"Applica modifiche")),"Editor primary action visible with native keyboard"));snap("10-editor-ime-dark");hideKeyboard();landscape();
        ui(()->{
            check(findEdit(sheetRoot(),"Condizione").getText().toString().equals("numero % 2 = 0"),"Condition draft survives rotation");check(findEdit(sheetRoot(),"Nome dell'uscita: quando è vera").getText().toString().equals("Pari & zero"),"Custom branch label and ampersand survive rotation");
            check(requiredDescription(sheetRoot(),"Quando è vera: colore Viola").isSelected(),"True branch color draft survives rotation");check(requiredDescription(sheetRoot(),"Quando è falsa: colore Arancione").isSelected(),"False branch color draft survives rotation");check(full(requiredText(sheetRoot(),"Applica modifiche")),"Editor primary action fully visible in landscape");
        });snap("11-editor-landscape-dark");portrait();ui(()->requiredText(sheetRoot(),"Applica modifiche").performClick());settle();
        ui(()->{Object node=nodeOfKind(3);check(get(node,"trueLabel").equals("Pari & zero")&&get(node,"falseLabel").equals("Dispari"),"Applying decision keeps custom branch names");check(get(node,"trueColor").equals("#9771DC")&&get(node,"falseColor").equals("#D68A18"),"Applying decision keeps custom branch colors");call(canvas(),"fitToContent");});snap("12-custom-branches-dark");ui(()->call(activity,"onConnectRequested",id[0]));settle();
        final View[] trueRow={null},falseRow={null};ui(()->{trueRow[0]=findTag(sheetRoot(),"true");falseRow[0]=findTag(sheetRoot(),"false");});
        reach(trueRow[0]);ui(()->{check(textFits(requiredText(sheetRoot(),"Pari & zero")),"Branch list presents literal custom true label");check(choiceColor(trueRow[0])==Color.parseColor("#9771DC"),"Branch list marker uses true branch custom color");});
        reach(falseRow[0]);ui(()->{check(textFits(requiredText(sheetRoot(),"Dispari")),"Branch list presents custom false label");check(choiceColor(falseRow[0])==Color.parseColor("#D68A18"),"Branch list marker uses false branch custom color");});snap("13-custom-branch-picker");ui(()->dismiss());
    }

    /** The real offline lesson journey, plus the editor actions introduced in 1.2.0. */
    private void learning()throws Exception{
        ui(()->{
            activity.getSharedPreferences("frociart_learning",Context.MODE_PRIVATE).edit().clear().commit();
            call(progress(),"restore",Collections.emptySet(),"",-1,-1,false,0);call(progress(),"restorePractices",Collections.emptySet());call(activity,"showHome");
        });settle();
        ui(()->{
            check((Boolean)get(activity,"hubVisible")&&call(study(),"page").equals("home"),"Home is the opening surface");
            check(full(requiredText(decor(),"IMPARA")),"Learning card visible on opening home");
            check(textFits(requiredText(decor(),"Impara costruendo.")),"Home heading fits preferred font size");
        });snap("20-home-light");
        reachText("Apri il foglio");ui(()->check(textFits(requiredText(decor(),"Apri il foglio")),"Free laboratory action reachable and not truncated"));snap("21-home-laboratory");
        reachText("Inizia a imparare");clickText("Inizia a imparare");
        ui(()->{
            check(call(study(),"page").equals("lesson")&&(Integer)get(progress(),"question")==-1,"Learning action opens lesson explanation first");
            check(textFits(requiredText(decor(),"Continua")),"Lesson idea footer visible and not truncated");
        });snap("22-lesson-idea");clickText("Continua");snap("22b-lesson-trick");clickText("Mettiti alla prova");
        final int[] correct={0},wrong={0};final String[] lessonId={null},wrongFeedback={null};
        ui(()->{
            Object question=question();correct[0]=(Integer)get(question,"correct");wrong[0]=(correct[0]+1)%((Object[])get(question,"options")).length;
            wrongFeedback[0]=((String[])get(question,"feedback"))[wrong[0]];lessonId[0]=(String)get(progress(),"activeId");
            check((Integer)get(progress(),"question")==0,"Quiz starts at question one");
            check(!((View)get(study(),"continueButton")).isEnabled(),"Quiz cannot continue before an answer");
        });chooseAnswer(wrong[0]);
        ui(()->{
            check(!(Boolean)call(progress(),"correct")&&(Boolean)get(progress(),"mistake"),"Wrong choice records a retryable mistake");
            check(((TextView)get(study(),"feedbackBody")).getText().toString().equals(wrongFeedback[0]),"Wrong choice shows its own explanatory feedback");
            check(full((View)get(study(),"feedbackTitle")),"Feedback heading scrolls into view");
            check(textFits(requiredText(decor(),"Riprova")),"Retry action remains visible at preferred font size");
        });snap("23-quiz-wrong-feedback");clickText("Riprova");
        ui(()->{
            check((Integer)get(progress(),"answer")==-1&&(Integer)get(progress(),"question")==0,"Retry clears answer without skipping question");
            boolean enabled=true;for(Object choice:(Object[])get(study(),"options"))enabled&=((View)choice).isEnabled();check(enabled,"Retry re-enables every answer");
        });chooseAnswer(correct[0]);
        ui(()->check((Boolean)call(progress(),"correct"),"Correct retry is accepted"));landscape();
        ui(()->{
            check((Integer)get(progress(),"question")==0&&(Integer)get(progress(),"answer")==correct[0],"Rotation retains question and selected correct answer");
            check(textFits(requiredText(decor(),"Continua")),"Quiz Continue remains visible in landscape");
        });snap("24-quiz-landscape");portrait();recreateActivity();
        ui(()->{
            check((Boolean)get(activity,"hubVisible")&&call(study(),"page").equals("lesson"),"Activity recreation restores active lesson page");
            check((Integer)get(progress(),"question")==0&&(Integer)get(progress(),"answer")==correct[0]&&(Boolean)get(progress(),"mistake"),"Saved quiz answer and previous mistake survive recreation");
        });clickText("Continua");
        for(int expected=1;expected<3;expected++){
            final int step=expected;ui(()->check((Integer)get(progress(),"question")==step,"Quiz advances exactly to question "+(step+1)));
            chooseCorrect();clickText(expected==2?"Completa la lezione":"Continua");
        }
        ui(()->{
            check((Boolean)call(progress(),"finished"),"All three questions complete the lesson");
            check((Integer)call(progress(),"xp")==30,"First completed lesson awards exactly 30 XP");
            check((Integer)get(progress(),"firstTry")==2,"Retry is excluded from first-attempt count");
            check((Boolean)call(progress(),"done",lessonId[0]),"Completion is recorded for the lesson");
            check(activity.getSharedPreferences("frociart_learning",Context.MODE_PRIVATE).getStringSet("completed",Collections.emptySet()).contains(lessonId[0]),"Completion is persisted offline");
            check(textFits(requiredText(decor(),"Crea il flowchart")),"Result footer visible and not truncated");
        });snap("25-lesson-result");
        clickText("Torna al percorso");ui(()->{
            check(call(study(),"page").equals("path"),"Result returns to the learning path");
            check(countDescriptions(decor(),"Lezione ")==24,"Path contains all 24 lesson actions");
        });
        final View[] first={null},last={null};ui(()->{first[0]=descriptionPrefix(decor(),"Lezione 1:");last[0]=descriptionPrefix(decor(),"Lezione 24:");});
        check(first[0]!=null&&last[0]!=null,"First and final lessons exist in the path");
        boolean reached=false;for(int i=0;i<28;i++){
            final boolean[] visible={false};ui(()->visible[0]=full(last[0]));if(visible[0]){reached=true;break;}
            swipeStudyPath();
        }
        check(reached,"Native swipes reach lesson 24");snap("26-path-final-lessons");
        reach(first[0]);tap(first[0]);settle();clickText("Continua");clickText("Mettiti alla prova");
        for(int i=0;i<3;i++){chooseCorrect();clickText(i==2?"Completa la lezione":"Continua");}
        ui(()->{
            check((Integer)call(progress(),"xp")==30,"Repeating a completed lesson does not duplicate XP");
            check((Integer)get(progress(),"firstTry")==3,"Repeat attempt has an independent three-answer score");
        });
        clickText("Crea il flowchart");ui(()->{
            String practiceId=(String)get(activity,"projectId");if(!practiceId.equals(originalId))testProjects.add(practiceId);
            check(!(Boolean)get(activity,"hubVisible"),"Lesson practice opens the real laboratory");
            check(get(activity,"practiceLessonId").equals(lessonId[0]),"Laboratory remembers which lesson opened its example");
        });snap("27-lesson-practice");deletePracticeThroughUi();
        ui(()->call(activity,"showHome"));theme("Scuro");snap("28-home-dark");
        reachText("Apri il foglio");clickText("Apri il foglio");editorEnhancements();
    }

    /** J29-J30: cancel and confirm deletion only for this synthetic lesson project. */
    private void deletePracticeThroughUi()throws Exception{
        ui(()->dismiss());settle();renameThroughUi("QA pratica da eliminare");
        final String[] removed={null};ui(()->{removed[0]=(String)get(activity,"projectId");check(!get(activity,"practiceLessonId").equals(""),"J30 deletion starts from a real lesson practice project");});
        if(await(()->(Boolean)get(activity,"autoArrange"),1000))tap(viewOnUi("arrangeButton"));
        selectMode(true);
        openDeleteThroughUi("QA pratica da eliminare");tapSheetText("Tieni il progetto");
        ui(()->{check(removed[0].equals(get(activity,"projectId")),"J29 cancelling deletion keeps the current project");check(projectExists(removed[0]),"J29 cancelling deletion preserves its saved file");});
        openDeleteThroughUi("QA pratica da eliminare");tapSheetText("Elimina progetto");
        check(await(()->!removed[0].equals(get(activity,"projectId")),12000),"J30 confirming deletion opens a separate clean starter project");
        ui(()->{
            testProjects.add((String)get(activity,"projectId"));check(!projectExists(removed[0]),"J30 deleted practice project is removed from local files");
            check(get(activity,"practiceLessonId").equals(""),"J30 clean project does not inherit the previous lesson context");
            check((Boolean)get(activity,"autoArrange"),"J30 clean starter restores default automatic placement");
            check(!(Boolean)call(canvas(),"isSelectionMode"),"J30 clean starter leaves group mode");
            check(((List<?>)get(get(activity,"document"),"nodes")).size()==4,"J30 clean starter contains its four expected blocks");dismiss();
        });
        snap("32-after-deleting-practice");recreateActivity();
        ui(()->{check(!projectExists(removed[0]),"J30 deleted project stays removed after activity restart");check(get(activity,"practiceLessonId").equals("")&&(Boolean)get(activity,"autoArrange"),"J30 clean placement and lesson state persist after restart");});
    }
    private boolean projectExists(String id)throws Exception{for(Object entry:(List<?>)call(get(activity,"store"),"entries"))if(id.equals(get(entry,"id")))return true;return false;}
    private void openDeleteThroughUi(String title)throws Exception{tapDescription("Scegli progetto");check(await(()->find(sheetRoot(),"Elimina progetto "+title,true)!=null,10000),"J29 synthetic project delete action is visible");tapSheetDescription("Elimina progetto "+title);ui(()->check(find(sheetRoot(),"Eliminare il progetto?",false)!=null,"J29 deleting a project requires its explicit confirmation"));}

    private void editorEnhancements()throws Exception{
        ui(()->{
            ((SharedPreferences)get(activity,"prefs")).edit().putBoolean("auto_"+testId,false).apply();
            call(activity,"switchDocument",call(activity,"starter"),testId);
        });settle();
        final float[][] before={null},arranged={null};final int[] history={0};
        ui(()->{
            check(!(Boolean)get(activity,"hubVisible"),"Free mode opens editor");
            check(full(view("arrangeButton"))&&textFits(view("arrangeButton")),"Ordina toggle fully visible at preferred font size");
            check(textFits(requiredText(decor(),"Trucco")),"Trucco action fully visible at preferred font size");
            check(!(Boolean)get(activity,"autoArrange"),"Existing manual project initially keeps its positions");
            before[0]=positions();history[0]=((Collection<?>)get(activity,"undo")).size();
        });tap(viewOnUi("arrangeButton"));settle();awaitCanvasSettled();
        ui(()->{
            arranged[0]=positions();check((Boolean)get(activity,"autoArrange"),"Native Ordina tap enables automatic layout");
            check(!Arrays.equals(before[0],arranged[0]),"Enabling Ordina changes the irregular starter positions");
            check(((Collection<?>)get(activity,"undo")).size()==history[0]+1,"Enabling layout creates exactly one undo snapshot");
            check(((SharedPreferences)get(activity,"prefs")).getBoolean("auto_"+testId,false),"Layout mode is persisted for this project");
        });tap(viewOnUi("undoButton"));settle();
        ui(()->check(Arrays.equals(before[0],positions()),"Undo restores every original position"));tap(viewOnUi("redoButton"));settle();
        ui(()->check(Arrays.equals(arranged[0],positions()),"Redo restores every arranged position"));
        final String[] id={null};final float[][] point={null};
        ui(()->{id[0]=(String)get(nodeOfKind(2),"id");call(canvas(),"focusNode",id[0]);});settle();
        ui(()->{
            point[0]=nodePoint(id[0]);Object node=call(get(activity,"document"),"find",id[0]);
            float wx=(Float)get(node,"x")+106,wy=(Float)get(node,"y")+44;Object hit=call(canvas(),"hitNode",wx,wy);
            check(((Set<?>)call(canvas(),"getSelectedIds")).contains(id[0]),"Manual drag begins with the requested block selected");
            check(hit!=null&&get(get(hit,"node"),"id").equals(id[0]),"Manual drag point hits the requested model block");
            report.append("MANUAL_DRAG_POINT ").append(Arrays.toString(point[0])).append(" CANVAS ").append(bounds((View)canvas())).append('\n');
        });drag(point[0][0],point[0][1],point[0][0]+56,point[0][1],8);settle();
        ui(()->{
            check(!(Boolean)get(activity,"autoArrange"),"Dragging a block switches to free placement");
            check(!((SharedPreferences)get(activity,"prefs")).getBoolean("auto_"+testId,true),"Manual placement mode is persisted");
        });
        final RectF[] plus={null};final Rect[] canvasRect={null};final float[][] drop={null};final int[] nodes={0};
        check(await(()->!((RectF)get(canvas(),"plusHit")).isEmpty(),6000),"Selected block exposes the plus connector");
        ui(()->{
            plus[0]=new RectF((RectF)get(canvas(),"plusHit"));canvasRect[0]=bounds((View)canvas());
            float density=activity.getResources().getDisplayMetrics().density,scale=(Float)get(canvas(),"zoom")*density;
            float[] ys={plus[0].centerY()+12*density,plus[0].centerY()+28*density,plus[0].centerY()-80*density};
            float[] xs={canvasRect[0].width()-34*density,34*density,plus[0].centerX()+100*density,plus[0].centerX()-100*density};
            outer:for(float ly:ys)for(float lx:xs){
                float sx=canvasRect[0].left+lx,sy=canvasRect[0].top+ly;
                if(lx<12*density||lx>canvasRect[0].width()-12*density||ly<80*density||ly>canvasRect[0].height()-100*density)continue;
                if(view("selectionBar").isShown()&&bounds(view("selectionBar")).contains((int)sx,(int)sy))continue;
                float wx=(lx-(Float)get(canvas(),"panX"))/scale,wy=(ly-(Float)get(canvas(),"panY"))/scale;
                if(call(canvas(),"hitNode",wx,wy)==null){drop[0]=new float[]{sx,sy};break outer;}
            }
            check(drop[0]!=null,"Plus drop target is empty canvas outside nodes and overlays");if(drop[0]==null)throw new IllegalStateException("No visible empty connector drop target");
            report.append("PLUS_DRAG ").append(plus[0]).append(" TO ").append(Arrays.toString(drop[0])).append('\n');
            nodes[0]=((List<?>)get(get(activity,"document"),"nodes")).size();
        });
        drag(canvasRect[0].left+plus[0].centerX(),canvasRect[0].top+plus[0].centerY(),drop[0][0],drop[0][1],10);
        check(await(()->get(activity,"activeSheet")!=null,8000),"Dragging plus to empty canvas opens block picker");
        ui(()->check(full(requiredDescription(sheetRoot(),"Aggiungi Output")),"Dragged connection presents a visible block choice"));snap("29-drag-plus-picker");
        final View[] add={null};ui(()->add[0]=requiredDescription(sheetRoot(),"Aggiungi Output"));tap(add[0]);settle();awaitCanvasSettled();
        ui(()->{
            Object d=get(activity,"document");List<?> list=(List<?>)get(d,"nodes");check(list.size()==nodes[0]+1,"Choosing a block after drag adds exactly one node");
            Object added=list.get(list.size()-1);String to=(String)get(added,"id");boolean linked=false;
            for(Object edge:(List<?>)get(d,"edges"))if(get(edge,"from").equals(id[0])&&get(edge,"to").equals(to))linked=true;
            check((Integer)get(added,"kind")==5&&linked,"Dragged plus creates the requested Output and its real edge");
        });snap("30-editor-dragged-connection");
        ui(()->call(activity,"switchDocument",call(activity,"starter"),testId));settle();
        ui(()->{
            Class<?> style=Class.forName("it.frociart.app.Ui",true,activity.getClassLoader());style.getField("motion").setBoolean(null,true);
            call(canvas(),"setMotionEnabled",true);view("runButton").performClick();
        });settle();
        ui(()->{
            check(android.animation.ValueAnimator.areAnimatorsEnabled(),"System animations are enabled for the integrated motion gate");
            view("stepButton").performClick();Object runner=get(activity,"runner");String last=(String)get(runner,"lastEdgeId"),destination=null;
            for(Object e:(List<?>)get(get(activity,"document"),"edges"))if(get(e,"id").equals(last))destination=(String)get(e,"to");
            check(destination!=null&&destination.equals(get(canvas(),"runDestination")),"Un passo animates the destination of the edge actually executed");
            check(get(canvas(),"runAnimator")!=null,"Integrated Prova starts the finite edge animation");
            check((Boolean)call(canvas(),"isReadOnly"),"Animated Prova retains read-only gestures");
            call(activity,"closeRun");check(get(canvas(),"runAnimator")==null,"Closing Prova cancels its animation");
        });
        clickText("Trucco");ui(()->check(get(activity,"activeSheet")!=null,"Trucco opens contextual learning help"));snap("31-editor-trick");ui(()->dismiss());
    }

    private Object study()throws Exception{return get(activity,"study");}
    private Object progress()throws Exception{return get(study(),"progress");}
    private Object question()throws Exception{return((Object[])get(call(progress(),"active"),"questions"))[(Integer)get(progress(),"question")];}
    private void chooseCorrect()throws Exception{final int[] i={0};ui(()->i[0]=(Integer)get(question(),"correct"));chooseAnswer(i[0]);}
    private void chooseAnswer(int index)throws Exception{final View[] option={null};ui(()->option[0]=(View)((Object[])get(study(),"options"))[index]);reach(option[0]);tap(option[0]);settle();}
    private void reach(View target){ui(()->target.requestRectangleOnScreen(new Rect(0,0,target.getWidth(),target.getHeight()),true));settle();}
    private void reachText(String label){final View[] target={null};ui(()->target[0]=requiredText(decor(),label));reach(target[0]);}
    private void clickText(String label)throws Exception{final View[] target={null};ui(()->target[0]=requiredText(decor(),label));reach(target[0]);tap(target[0]);settle();}
    private View viewOnUi(String name){final View[] target={null};ui(()->target[0]=view(name));return target[0];}
    private int countDescriptions(View root,String prefix){int n=root.getContentDescription()!=null&&root.getContentDescription().toString().startsWith(prefix)?1:0;if(root instanceof ViewGroup)for(int i=0;i<((ViewGroup)root).getChildCount();i++)n+=countDescriptions(((ViewGroup)root).getChildAt(i),prefix);return n;}
    private View descriptionPrefix(View root,String prefix){if(root.getContentDescription()!=null&&root.getContentDescription().toString().startsWith(prefix))return root;if(root instanceof ViewGroup)for(int i=0;i<((ViewGroup)root).getChildCount();i++){View found=descriptionPrefix(((ViewGroup)root).getChildAt(i),prefix);if(found!=null)return found;}return null;}
    private void swipeStudyPath()throws Exception{final Rect[] area={null};ui(()->area[0]=bounds((View)get(study(),"scrolling")));Rect r=area[0];drag(r.exactCenterX(),r.top+r.height()*.84f,r.exactCenterX(),r.top+r.height()*.20f,9);settle();}
    private void drag(float x1,float y1,float x2,float y2,int moves)throws Exception{
        long start=SystemClock.uptimeMillis();
        for(int i=0;i<=moves+1;i++){
            int action=i==0?MotionEvent.ACTION_DOWN:i==moves+1?MotionEvent.ACTION_UP:MotionEvent.ACTION_MOVE;
            float part=i==0?0:Math.max(.5f,Math.min(1,i/(float)moves));long now=SystemClock.uptimeMillis();
            MotionEvent event=MotionEvent.obtain(start,now,action,x1+(x2-x1)*part,y1+(y2-y1)*part,0);event.setSource(InputDevice.SOURCE_TOUCHSCREEN);
            // Software rendering can make a synchronous call per tiny MOVE
            // last longer than long-press timeout. Cross slop on the first
            // MOVE; enqueue DOWN/MOVE and synchronize only the final UP.
            boolean accepted=getUiAutomation().injectInputEvent(event,action==MotionEvent.ACTION_UP);event.recycle();if(!accepted)throw new IllegalStateException("Native drag rejected");if(i>0)pause(20);
        }
    }
    private float[] nodePoint(String id)throws Exception{
        Object node=call(get(activity,"document"),"find",id);int kind=(Integer)get(node,"kind");float width=kind==0||kind==1?164:kind==2?212:kind==3?232:226,height=kind==0||kind==1?58:kind==3?136:kind==2?88:92;
        float scale=(Float)get(canvas(),"zoom")*activity.getResources().getDisplayMetrics().density;Rect rect=bounds((View)canvas());
        return new float[]{rect.left+((Float)get(node,"x")+width/2)*scale+(Float)get(canvas(),"panX"),rect.top+((Float)get(node,"y")+height/2)*scale+(Float)get(canvas(),"panY")};
    }
    private float[] positions()throws Exception{List<?> nodes=(List<?>)get(get(activity,"document"),"nodes");float[] p=new float[nodes.size()*2];int i=0;for(Object n:nodes){p[i++]=(Float)get(n,"x");p[i++]=(Float)get(n,"y");}return p;}
    private void awaitCanvasSettled(){check(await(()->get(canvas(),"layoutAnimator")==null&&get(canvas(),"cameraAnimator")==null,10000),"Finite layout and camera animations complete");}
    private void recreateActivity()throws Exception{
        ActivityMonitor monitor=addMonitor("it.frociart.app.MainActivity",null,false);ui(()->activity.recreate());
        Activity next=waitForMonitorWithTimeout(monitor,15000);removeMonitor(monitor);if(next==null)throw new IllegalStateException("Activity recreation timed out");activity=next;settle();
    }

    private void fixture(String name){ui(()->{Class<?> c=Class.forName("it.frociart.app.FlowExamples",true,activity.getClassLoader());call(activity,"switchDocument",c.getMethod(name).invoke(null),testId);});settle();}
    private void theme(String label){ui(()->{call(activity,"showTheme");requiredText(sheetRoot(),label).performClick();});settle();ui(()->{SharedPreferences prefs=(SharedPreferences)get(activity,"prefs");check(prefs.getString("theme","").equals(label.equals("Scuro")?"dark":"light"),"Theme applied: "+label);});
        check(await(()->{WindowInsetsController controller=activity.getWindow().getInsetsController();if(controller==null)return false;boolean darkIcons=(controller.getSystemBarsAppearance()&WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS)!=0;return darkIcons==label.equals("Chiaro");},6000),"Status bar icon appearance follows theme: "+label);
    }
    private void portrait(){rotate(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT,false);}
    private void landscape(){rotate(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE,true);}
    private void rotate(int orientation,boolean landscape){ui(()->activity.setRequestedOrientation(orientation));check(await(()->(decor().getWidth()>decor().getHeight())==landscape,12000),landscape?"Device rotates to landscape":"Device rotates to portrait");settle();pause(1200);}
    private void openKeyboard(String field,boolean inSheet)throws Exception{
        final View[] target={null};ui(()->{target[0]=inSheet?findEdit(sheetRoot(),field):view(field);target[0].requestRectangleOnScreen(new Rect(0,0,target[0].getWidth(),target[0].getHeight()),true);});settle();tap(target[0]);
        check(await(()->imeVisible(inSheet?sheetRoot():decor()),12000),"Real Android keyboard visible: "+(inSheet?"editor":getOrientation()));settle();
        final Rect[] last={null};final int[] stable={0};
        check(await(()->{Rect current=keyboardBounds();if(current==null||current.height()<120){stable[0]=0;return false;}stable[0]=current.equals(last[0])?stable[0]+1:0;last[0]=current;return stable[0]>=3;},12000),"Keyboard has stable native input-method window bounds");pause(700);
        ui(()->{
            Rect ime=keyboardBounds(),button=bounds(inSheet?requiredText(sheetRoot(),"Applica modifiche"):requiredDescription(decor(),"Invia valore"));
            report.append("NATIVE_IME ").append(getOrientation()).append(" window=").append(ime).append(" action=").append(button).append('\n');
            check(ime!=null&&button.bottom<=ime.top,"Primary action is wholly above the real native keyboard window");
        });
    }
    private String getOrientation(){return activity.getResources().getConfiguration().orientation==2?"landscape":"portrait";}
    private void hideKeyboard(){ui(()->{View current=activity.getCurrentFocus();Object sheet=get(activity,"activeSheet");if(sheet!=null)current=((Dialog)get(sheet,"dialog")).getCurrentFocus();if(current==null)current=decor();((InputMethodManager)activity.getSystemService(Context.INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(current.getWindowToken(),0);});await(()->!imeVisible(decor()),5000);settle();}
    private boolean imeVisible(View view){WindowInsets insets=view.getRootWindowInsets();return insets!=null&&insets.isVisible(WindowInsets.Type.ime());}
    private void tap(View target)throws Exception{
        if(!awaitSystemPackage(getTargetContext().getPackageName(),2000))throw new IllegalStateException("Touch prevented: target app is not active");
        final float[] point=new float[2];ui(()->{Rect rect=bounds(target);point[0]=rect.exactCenterX();point[1]=rect.exactCenterY();});tapAt(point[0],point[1]);
    }
    private void tapAt(float x,float y)throws Exception{
        float[] point={x,y};long start=SystemClock.uptimeMillis();
        MotionEvent down=MotionEvent.obtain(start,start,MotionEvent.ACTION_DOWN,point[0],point[1],0);down.setSource(InputDevice.SOURCE_TOUCHSCREEN);boolean accepted=getUiAutomation().injectInputEvent(down,true);down.recycle();
        MotionEvent up=MotionEvent.obtain(start,start+60,MotionEvent.ACTION_UP,point[0],point[1],0);up.setSource(InputDevice.SOURCE_TOUCHSCREEN);accepted=getUiAutomation().injectInputEvent(up,true)&&accepted;up.recycle();if(!accepted)throw new IllegalStateException("Native touch rejected");
    }
    private void restore(){if(activity==null||originalDoc==null)return;hideKeyboard();portrait();ui(()->{
        dismiss();call(activity,"closeRun");if(testProjects.contains((String)get(activity,"projectId")))call(get(activity,"drafts"),"removeProject",get(activity,"projectId"));call(activity,"switchDocument",originalDoc,originalId);
        for(String id:testProjects)if(!id.equals(originalId))call(get(activity,"store"),"delete",id);
        SharedPreferences prefs=(SharedPreferences)get(activity,"prefs");restorePreferences(prefs,originalPreferences);
        restorePreferences(activity.getSharedPreferences("frociart_learning",Context.MODE_PRIVATE),originalLearningPreferences);
        Class<?> style=Class.forName("it.frociart.app.Ui",true,activity.getClassLoader());String mode=prefs.getString("theme","system");style.getField("dark").setBoolean(null,mode.equals("dark")||(mode.equals("system")&&(activity.getResources().getConfiguration().uiMode&48)==32));style.getField("motion").setBoolean(null,prefs.getBoolean("animations",true));
        SharedPreferences learning=activity.getSharedPreferences("frociart_learning",Context.MODE_PRIVATE);
        call(progress(),"restore",learning.getStringSet("completed",new HashSet<>()),learning.getString("active",""),learning.getInt("question",-1),learning.getInt("answer",-1),learning.getBoolean("mistake",false),learning.getInt("first_try",0));
        call(progress(),"restoreReview",learning.getStringSet("review",new HashSet<>()));call(progress(),"restorePractices",learning.getStringSet("built",new HashSet<>()));call(activity,"buildChrome");if(originalHub)call(activity,"showHome");
    });}
    @SuppressWarnings("unchecked") private void restorePreferences(SharedPreferences prefs,Map<String,?> snapshot){if(snapshot==null)return;SharedPreferences.Editor edit=prefs.edit().clear();for(Map.Entry<String,?> e:snapshot.entrySet()){
        Object v=e.getValue();String k=e.getKey();if(v instanceof String)edit.putString(k,(String)v);else if(v instanceof Boolean)edit.putBoolean(k,(Boolean)v);else if(v instanceof Integer)edit.putInt(k,(Integer)v);else if(v instanceof Long)edit.putLong(k,(Long)v);else if(v instanceof Float)edit.putFloat(k,(Float)v);else if(v instanceof Set)edit.putStringSet(k,new HashSet<>((Set<String>)v));
    }if(!edit.commit())throw new IllegalStateException("Failed to restore preferences");}
    private void check(boolean pass,String name){checks++;if(!pass)errors++;String line=(pass?"PASS ":"FAIL ")+name;report.append(line).append('\n');Log.i("FROCIART_UI",line);}
    private void pause(long ms){SystemClock.sleep(ms);}
    private void settle(){ui(()->{});pause(350);}
    private boolean await(Condition condition,long timeout){long end=SystemClock.uptimeMillis()+timeout;do{final boolean[] result={false};ui(()->result[0]=condition.value());if(result[0])return true;pause(160);}while(SystemClock.uptimeMillis()<end);return false;}
    private void ui(Action action){final Throwable[] error={null};runOnMainSync(()->{try{action.run();}catch(Throwable t){error[0]=t;}});if(error[0]!=null)throw new RuntimeException(error[0]);}
    private Object get(Object target,String name)throws Exception{Field field=target.getClass().getDeclaredField(name);field.setAccessible(true);return field.get(target);}
    private void set(Object target,String name,Object value)throws Exception{Field field=target.getClass().getDeclaredField(name);field.setAccessible(true);field.set(target,value);}
    private Object call(Object target,String name,Object... args)throws Exception{for(Method method:target.getClass().getDeclaredMethods())if(method.getName().equals(name)&&method.getParameterTypes().length==args.length){method.setAccessible(true);return method.invoke(target,args);}throw new NoSuchMethodException(name);}
    private View view(String name)throws Exception{return(View)get(activity,name);}
    private Object canvas()throws Exception{return get(activity,"canvas");}
    private View decor(){return activity.getWindow().getDecorView();}
    private String state()throws Exception{return get(get(activity,"runner"),"state").toString();}
    private String readState(){final String[] state={null};ui(()->state[0]=state());return state[0];}
    private String text(String field)throws Exception{return((TextView)view(field)).getText().toString();}
    private int kind(String name)throws Exception{return Class.forName("it.frociart.app.FlowModel",true,activity.getClassLoader()).getField(name).getInt(null);}
    private Object nodeOfKind(String name)throws Exception{return nodeOfKind(kind(name));}
    private String encodedDocument()throws Exception{Object doc=get(activity,"document");return(String)Class.forName("it.frociart.app.FlowCodec",true,activity.getClassLoader()).getMethod("encode",doc.getClass()).invoke(null,doc);}
    private Object nodeOfKind(int kind)throws Exception{for(Object node:(List<?>)get(get(activity,"document"),"nodes"))if(((Integer)get(node,"kind"))==kind)return node;throw new IllegalStateException("Missing node kind "+kind);}
    private void dismiss()throws Exception{Object sheet=get(activity,"activeSheet");if(sheet!=null)((Dialog)get(sheet,"dialog")).dismiss();}
    private View sheetRoot()throws Exception{Object sheet=get(activity,"activeSheet");if(sheet==null)throw new IllegalStateException("Expected an open sheet");return(View)get(sheet,"root");}
    private Rect bounds(View view){int[] xy=new int[2];view.getLocationOnScreen(xy);return new Rect(xy[0],xy[1],xy[0]+view.getWidth(),xy[1]+view.getHeight());}
    private boolean full(View view){Rect visible=new Rect();return view.isShown()&&view.getWidth()>0&&view.getHeight()>0&&view.getGlobalVisibleRect(visible)&&visible.width()>=view.getWidth()-2&&visible.height()>=view.getHeight()-2;}
    private boolean textFits(View view){if(!(view instanceof TextView)||!full(view))return false;TextView text=(TextView)view;android.text.Layout layout=text.getLayout();if(layout==null||layout.getLineCount()==0)return false;for(int i=0;i<layout.getLineCount();i++)if(layout.getEllipsisCount(i)>0)return false;return layout.getHeight()<=view.getHeight()-text.getCompoundPaddingTop()-text.getCompoundPaddingBottom()+2;}
    private View find(View root,String value,boolean description){CharSequence candidate=description?root.getContentDescription():root instanceof TextView?((TextView)root).getText():null;if(candidate!=null&&value.contentEquals(candidate))return root;if(root instanceof ViewGroup)for(int i=0;i<((ViewGroup)root).getChildCount();i++){View found=find(((ViewGroup)root).getChildAt(i),value,description);if(found!=null)return found;}return null;}
    private View requiredText(View root,String value){View view=find(root,value,false);if(view==null)throw new IllegalStateException("Text missing: "+value);return view;}
    private View requiredDescription(View root,String value){View view=find(root,value,true);if(view==null)throw new IllegalStateException("Description missing: "+value);return view;}
    private EditText findEdit(View root,String value){View view=requiredDescription(root,value);if(!(view instanceof EditText))throw new IllegalStateException("Field missing: "+value);return(EditText)view;}
    private void snap(String name)throws Exception{settle();pause(500);Bitmap bitmap=getUiAutomation().takeScreenshot();if(bitmap==null)throw new IOException("No screenshot");try(FileOutputStream out=new FileOutputStream(new File(directory,name+".png"))){bitmap.compress(Bitmap.CompressFormat.PNG,100,out);}bitmap.recycle();StringBuilder tree=new StringBuilder();ui(()->{tree(decor(),tree);Object sheet=get(activity,"activeSheet");if(sheet!=null)tree((View)get(sheet,"root"),tree);});try(FileOutputStream out=new FileOutputStream(new File(directory,name+".txt"))){out.write(tree.toString().getBytes("UTF-8"));}Log.i("FROCIART_UI","SNAP "+scenario+"/"+name);android.view.accessibility.AccessibilityNodeInfo active=getUiAutomation().getRootInActiveWindow();String packageName=active==null?"missing":String.valueOf(active.getPackageName());if(!packageName.equals(getTargetContext().getPackageName())&&!packageName.equals("com.android.inputmethod.latin"))throw new IllegalStateException("Device overlay covers tested UI: "+packageName+" at "+name);}
    private void tree(View view,StringBuilder tree){if(view.getVisibility()!=View.VISIBLE)return;Rect visible=new Rect();boolean shown=view.getGlobalVisibleRect(visible);tree.append(view.getClass().getSimpleName()).append(' ').append(bounds(view)).append(" visible=").append(shown).append(' ').append(visible).append(" full=").append(full(view));if(view instanceof TextView)tree.append(" text=").append(((TextView)view).getText());if(view.getContentDescription()!=null)tree.append(" desc=").append(view.getContentDescription());tree.append('\n');if(view instanceof ViewGroup)for(int i=0;i<((ViewGroup)view).getChildCount();i++)tree(((ViewGroup)view).getChildAt(i),tree);}
}
