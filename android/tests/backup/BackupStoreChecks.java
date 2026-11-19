package it.frociart.app;

import android.content.Context;
import android.content.ContextWrapper;
import android.content.SharedPreferences;
import android.util.AtomicFile;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

/** Call from an Android instrumentation. Uses only its own cache folder and pref namespace. */
public final class BackupStoreChecks {
    private int checks;private final StringBuilder log=new StringBuilder();
    private interface Action{void run()throws Exception;}
    private void check(boolean pass,String message){checks++;log.append(pass?"PASS ":"FAIL ").append(message).append('\n');if(!pass)throw new AssertionError(message);}
    private void fails(Action action,String message)throws Exception{boolean rejected=false;try{action.run();}catch(IOException ex){rejected=true;}check(rejected,message);}
    private static final class IsolatedContext extends ContextWrapper{
        final File root;final String prefix;final Set<String> names=new HashSet<>();
        IsolatedContext(Context base,File root,String prefix){super(base);this.root=root;this.prefix=prefix;}
        @Override public Context getApplicationContext(){return this;}
        @Override public File getFilesDir(){return root;}
        @Override public SharedPreferences getSharedPreferences(String name,int mode){names.add(prefix+name);return super.getSharedPreferences(prefix+name,mode);}
    }
    public static String run(Context context)throws Exception{return new BackupStoreChecks().exercise(context);}
    private String exercise(Context base)throws Exception{
        String token=UUID.randomUUID().toString().replace("-","");File root=new File(base.getCacheDir(),"backup105-"+token);check(root.mkdirs(),"isolated native fixture directory created");
        IsolatedContext source=new IsolatedContext(base,new File(root,"source"),"backup105.source."+token+".");
        IsolatedContext destination=new IsolatedContext(base,new File(root,"destination"),"backup105.destination."+token+".");
        FlowStore store=null,target=null,reopened=null;
        try{
            store=new FlowStore(source);target=new FlowStore(destination);
            FlowModel.Document first=FlowExamples.parity();first.title="Lezione è 🦊 / 同名";first.version=4;first.find("check").comment="Nota del blocco";
            first.nodes.add(new FlowModel.Node(FlowModel.NOTE,"Nota libera",20,20));first.functions=new ArrayList<>();FlowModel.Function f=FlowFunctionEditing.create(first);f.name="calcola";first.functions.add(f);
            FlowModel.Document second=FlowExamples.sum();second.title=first.title;
            String firstId=store.newId(),secondId=store.newId();store.save(firstId,first);store.save(secondId,second);
            FlowDraftStore drafts=new FlowDraftStore(source.getSharedPreferences("frociart",Context.MODE_PRIVATE));drafts.put(firstId,"check",new FlowDraftStore.Draft("testo non applicato","","Sì","No","#39B985","#EF6C82"));
            String firstEncoded=FlowCodec.encode(first),secondEncoded=FlowCodec.encode(second);
            FlowBackup.Archive snapshot=store.createBackup();check(snapshot.count()==2,"snapshot contains both saved projects");
            ByteArrayOutputStream out=new ByteArrayOutputStream();FlowBackup.write(out,snapshot);FlowBackup.Archive preview=FlowBackup.read(new ByteArrayInputStream(out.toByteArray()));
            check(preview.count()==2&&preview.projects().get(0).title.equals(first.title)&&preview.projects().get(1).title.equals(first.title),"preview preserves duplicate Unicode titles");
            check(drafts.get(firstId,"check").text.equals("testo non applicato"),"backup leaves source unapplied draft untouched");
            FlowModel.Document original=FlowExamples.age();original.title=first.title;String originalId=target.newId();target.save(originalId,original);String originalBytes=FlowCodec.encode(original);
            List<FlowStore.Entry> restored=target.restoreBackup(preview);check(restored.size()==2&&target.entries().size()==3,"restore adds both projects beside existing same-title project");
            check(FlowCodec.encode(target.load(originalId)).equals(originalBytes),"existing project content unchanged");
            Set<String> importedIds=new HashSet<>();Set<String> actualContent=new HashSet<>();for(FlowStore.Entry entry:restored){check(!entry.id.equals(originalId)&&!entry.id.equals(firstId)&&!entry.id.equals(secondId),"restore uses fresh project identity");importedIds.add(entry.id);actualContent.add(FlowCodec.encode(target.load(entry.id)));}
            check(importedIds.size()==2&&actualContent.contains(firstEncoded)&&actualContent.contains(secondEncoded),"functions, notes, comments and full graphs restored exactly");
            FlowDraftStore destinationDrafts=new FlowDraftStore(destination.getSharedPreferences("frociart",Context.MODE_PRIVATE));for(String id:importedIds)check(destinationDrafts.lastNode(id)==null,"excluded unapplied drafts are not silently imported");
            target.close();target=null;reopened=new FlowStore(destination);check(reopened.entries().size()==3,"native close and reopen retains all restored projects");
            check(reopened.backupRecoveryWarning()==null,"successful restore has no recovery warning");
            byte[] broken=Arrays.copyOf(out.toByteArray(),out.size()-11);final FlowStore stable=reopened;fails(()->stable.restoreBackup(FlowBackup.read(new ByteArrayInputStream(broken))),"damaged archive rejected before storage import");check(stable.entries().size()==3&&FlowCodec.encode(stable.load(originalId)).equals(originalBytes),"damaged archive does not change existing projects");
            File projects=new File(destination.root,"projects"),journal=new File(projects,".restore.pending");
            String partialId=stable.newId(),unwrittenId=stable.newId();stable.save(partialId,FlowExamples.blank());atomic(journal,"FROCIART-RESTORE-1\n"+partialId+"\n"+unwrittenId+"\n");
            stable.close();reopened=new FlowStore(destination);check(reopened.entries().size()==3&&!new File(projects,partialId+".frociart").exists(),"interrupted multi-project restore rolls back only new partial copies");
            check(!journal.exists()&&reopened.backupRecoveryWarning()==null,"successful interrupted-restore recovery clears journal");check(FlowCodec.encode(reopened.load(originalId)).equals(originalBytes),"interrupted import recovery preserves pre-existing original");
            atomic(journal,"FROCIART-RESTORE-1\n../outside\n");reopened.close();reopened=new FlowStore(destination);check(reopened.backupRecoveryWarning()!=null,"corrupt internal journal reports recovery warning instead of crashing");check(FlowCodec.encode(reopened.load(originalId)).equals(originalBytes),"corrupt journal never deletes original");final FlowStore corrupt=reopened;fails(()->corrupt.restoreBackup(preview),"corrupt recovery journal blocks further restores safely");check(journal.delete(),"owned corrupt journal fixture removed");
            String blockedId=reopened.newId();File blocked=new File(projects,blockedId+".frociart");check(blocked.mkdir(),"prepare native deletion failure directory");File child=new File(blocked,"fixture");atomic(child,"owned");atomic(journal,"FROCIART-RESTORE-1\n"+blockedId+"\n");reopened.close();reopened=new FlowStore(destination);check(reopened.backupRecoveryWarning()!=null&&journal.exists(),"rollback deletion failure retains journal and exposes warning");check(FlowCodec.encode(reopened.load(originalId)).equals(originalBytes),"rollback deletion failure preserves previous project");check(child.delete()&&blocked.delete(),"owned deletion failure fixture removed");reopened.close();reopened=new FlowStore(destination);check(reopened.backupRecoveryWarning()==null&&!journal.exists(),"next native reopen finishes delayed rollback");
            String corruptId=store.newId();File corruptFile=new File(source.root,"projects/"+corruptId+".frociart");atomic(corruptFile,"{bad");final FlowStore sourceStore=store;fails(()->sourceStore.createBackup(),"unreadable saved project prevents silently incomplete backup");check(FlowCodec.encode(store.load(firstId)).equals(firstEncoded),"failed snapshot does not alter source project");
            check(drafts.get(firstId,"check").text.equals("testo non applicato"),"all restore and failure tests preserve source draft");
            log.append("BackupStoreChecks PASS ").append(checks).append(" assertions; actual Android AtomicFile, isolated fixtures. SAF UI tested separately.\n");return log.toString();
        }finally{
            if(store!=null)store.close();if(target!=null)target.close();if(reopened!=null)reopened.close();
            for(String name:source.names)base.deleteSharedPreferences(name);for(String name:destination.names)base.deleteSharedPreferences(name);
            removeOwned(root,root.getCanonicalPath());
        }
    }
    private static void atomic(File file,String text)throws IOException{AtomicFile atomic=new AtomicFile(file);FileOutputStream out=null;try{out=atomic.startWrite();out.write(text.getBytes(StandardCharsets.UTF_8));atomic.finishWrite(out);}catch(IOException ex){if(out!=null)atomic.failWrite(out);throw ex;}}
    private static void removeOwned(File file,String root)throws IOException{String path=file.getCanonicalPath();if(!path.equals(root)&&!path.startsWith(root+File.separator))throw new IOException("Fixture cleanup escaped its owned directory.");File[] children=file.listFiles();if(children!=null)for(File child:children)removeOwned(child,root);if(file.exists()&&!file.delete())throw new IOException("Unable to remove owned fixture: "+file.getName());}
}
