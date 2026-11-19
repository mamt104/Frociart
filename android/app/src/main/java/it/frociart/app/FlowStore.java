package it.frociart.app;

import android.content.Context;
import android.net.Uri;
import android.util.AtomicFile;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.charset.CodingErrorAction;
import java.nio.ByteBuffer;
import java.util.*;
import java.lang.ref.WeakReference;

public final class FlowStore {
    // MainActivity instances share one application process and AtomicFile set.
    // A store's monitor alone cannot protect a different activity's reader.
    private static final Object IO_LOCK=new Object();
    private static final Map<String,WeakReference<FlowStore>> EDITORS=new HashMap<>();
    private final Context context; private final File projects;private final FlowDraftStore drafts;
    private String editingId;private boolean closed;private String backupRecoveryWarning;
    public FlowStore(Context c){context=c.getApplicationContext();drafts=new FlowDraftStore(context.getSharedPreferences("frociart",Context.MODE_PRIVATE));projects=new File(c.getFilesDir(),"projects");if(!projects.isDirectory()&&!projects.mkdirs())throw new IllegalStateException("Non riesco a creare l'archivio dei progetti.");synchronized(IO_LOCK){try{recoverBackupRestore();}catch(IOException ex){backupRecoveryWarning="Il recupero di un backup interrotto richiede attenzione. I progetti precedenti sono conservati. "+ex.getMessage();}}}
    public String backupRecoveryWarning(){synchronized(IO_LOCK){return backupRecoveryWarning;}}
    public String newId(){return FlowModel.id();}
    private File projectFile(String id){if(id==null||!id.matches("[a-f0-9]{32}"))throw new IllegalArgumentException("Identificativo progetto non valido.");return new File(projects,id+".frociart");}
    public FlowModel.Document load(String id)throws IOException{synchronized(IO_LOCK){FlowModel.Document doc=readProject(projectFile(id));drafts.recoverRewrite(id,fingerprint(FlowCodec.encode(doc)));return doc;}}
    public void save(String id,FlowModel.Document doc)throws IOException{synchronized(IO_LOCK){writable(id);recoverDrafts(id);writeAtomic(projectFile(id),FlowCodec.encode(doc).getBytes(StandardCharsets.UTF_8));}}
    public void saveWithDrafts(String id,FlowModel.Document doc,java.util.function.BiFunction<String,String,String> rewrite)throws IOException{synchronized(IO_LOCK){
        writable(id);
        recoverDrafts(id);final File file=projectFile(id);final String encoded=FlowCodec.encode(doc);
        try{drafts.rewriteAndSave(id,rewrite,fingerprint(encoded),()->writeAtomic(file,encoded.getBytes(StandardCharsets.UTF_8)));}
        catch(IOException e){recoverDrafts(id);throw e;}
    }}
    public static final class EditorDocument {
        public final String id;public final FlowModel.Document document;public final boolean copied;
        private EditorDocument(String id,FlowModel.Document document,boolean copied){this.id=id;this.document=document;this.copied=copied;}
    }
    public EditorDocument openEditor(String id,FlowModel.Document candidate)throws IOException{synchronized(IO_LOCK){
        if(closed)throw new IOException("Questa finestra è stata chiusa.");
        String selected=id==null?newId():id;File file=projectFile(selected);
        FlowModel.Document doc=id!=null&&(file.exists()||new File(file.getPath()+".bak").exists())?load(id):candidate;
        if(doc==null)throw new FileNotFoundException("Il progetto non è più disponibile.");
        FlowStore owner=owner(selected);boolean copied=owner!=null&&owner!=this;
        if(copied){
            // Save the separate copy before releasing this window's old claim.
            doc=doc.copy();String title=doc.title==null?"Nuovo flowchart":doc.title;
            int end=Math.min(190,title.length());if(end>0&&Character.isHighSurrogate(title.charAt(end-1)))end--;
            doc.title="Copia · "+title.substring(0,end);selected=newId();
            writeAtomic(projectFile(selected),FlowCodec.encode(doc).getBytes(StandardCharsets.UTF_8));
        }
        releaseEditor();editingId=selected;EDITORS.put(projectFile(selected).getAbsolutePath(),new WeakReference<>(this));
        return new EditorDocument(selected,doc,copied);
    }}
    private FlowStore owner(String id){String key=projectFile(id).getAbsolutePath();WeakReference<FlowStore> ref=EDITORS.get(key);FlowStore value=ref==null?null:ref.get();if(value==null||value.closed){EDITORS.remove(key);return null;}return value;}
    private void writable(String id)throws IOException{if(closed)throw new IOException("Questa finestra è stata chiusa.");FlowStore owner=owner(id);if(owner!=null&&owner!=this)throw new IOException("Il progetto è aperto in un'altra finestra. Aprilo come copia prima di modificarlo.");}
    private void releaseEditor(){if(editingId!=null){String key=projectFile(editingId).getAbsolutePath();WeakReference<FlowStore> ref=EDITORS.get(key);if(ref!=null&&ref.get()==this)EDITORS.remove(key);editingId=null;}}
    public void close(){synchronized(IO_LOCK){releaseEditor();closed=true;}}
    private void recoverDrafts(String id)throws IOException{if(drafts.hasPendingRewrite(id))load(id);}
    private static String fingerprint(String value){try{byte[] hash=java.security.MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8));return java.util.Base64.getEncoder().encodeToString(hash);}catch(java.security.NoSuchAlgorithmException e){throw new IllegalStateException(e);}}
    public List<Entry> entries(){List<Entry> result=new ArrayList<>();File[] files=projects.listFiles();Set<String> ids=new HashSet<>();if(files!=null)for(File f:files){if(!f.getName().matches("[a-f0-9]{32}\\.frociart(?:\\.bak)?"))continue;ids.add(f.getName().substring(0,32));}for(String id:ids){synchronized(IO_LOCK){File f=projectFile(id);try{FlowModel.Document d=readProject(f);result.add(new Entry(id,d.title,d.totalNodes(),f.lastModified()));}catch(Exception ignored){result.add(new Entry(id,"Progetto da recuperare",-1,f.lastModified()));}}}Collections.sort(result,(a,b)->Long.compare(b.modified,a.modified));return result;}
    public boolean delete(String id){synchronized(IO_LOCK){FlowStore owner=owner(id);if(closed||owner!=null&&owner!=this)return false;File f=projectFile(id);new AtomicFile(f).delete();return !f.exists()&&!new File(f.getPath()+".bak").exists()&&!new File(f.getPath()+".new").exists();}}
    /** Only saved project files are included. Unapplied block drafts remain on this device. */
    public FlowBackup.Archive createBackup()throws IOException{synchronized(IO_LOCK){
        if(closed)throw new IOException("Questa finestra è stata chiusa.");
        recoverBackupRestore();File[] files=projects.listFiles();if(files==null)throw new IOException("L'archivio dei progetti non è leggibile.");
        Set<String> found=new HashSet<>();for(File file:files)if(file.getName().matches("[a-f0-9]{32}\\.frociart(?:\\.bak)?"))found.add(file.getName().substring(0,32));
        List<String> saved=new ArrayList<>(found);Collections.sort(saved);
        if(saved.isEmpty())throw new IOException("Non ci sono progetti salvati da includere nel backup.");
        if(saved.size()>FlowBackup.MAX_PROJECTS)throw new IOException("Il backup supporta al massimo 100 progetti. Esporta separatamente i progetti aggiuntivi.");
        FlowBackup.Builder builder=new FlowBackup.Builder();for(String id:saved){
            File file=projectFile(id),backup=new File(file.getPath()+".bak");File committed=backup.exists()?backup:file;
            if(committed.length()>builder.remainingBytes())throw new IOException("Il backup supera il limite complessivo di 32 MB. Esporta separatamente alcuni progetti.");
            try{builder.add(load(id));}catch(IOException|RuntimeException ex){throw new IOException("Un progetto salvato non è leggibile o supera lo spazio rimasto nel backup. Nessun backup è stato creato: controlla l'elenco dei progetti.",ex);}
        }
        return builder.build();
    }}
    /** Restore as separate copies, never over existing IDs. Journal recovery removes only these new copies. */
    public List<Entry> restoreBackup(FlowBackup.Archive archive)throws IOException{synchronized(IO_LOCK){
        if(closed)throw new IOException("Questa finestra è stata chiusa.");
        recoverBackupRestore();if(archive==null||archive.count()<1||archive.count()>FlowBackup.MAX_PROJECTS)throw new IOException("Il backup non è valido.");
        List<String> ids=new ArrayList<>();Set<String> allocated=new HashSet<>();
        for(int i=0;i<archive.count();i++){String id;do{id=newId();}while(allocated.contains(id)||existsAtomic(projectFile(id))||owner(id)!=null);allocated.add(id);ids.add(id);}
        StringBuilder journal=new StringBuilder("FROCIART-RESTORE-1\n");for(String id:ids)journal.append(id).append('\n');
        File pending=new File(projects,".restore.pending");writeAtomic(pending,journal.toString().getBytes(StandardCharsets.UTF_8));
        try{
            List<Entry> restored=new ArrayList<>();
            for(int i=0;i<ids.size();i++){String id=ids.get(i);File file=projectFile(id);writeAtomic(file,archive.projectBytes(i));FlowBackup.ProjectInfo info=archive.projects().get(i);restored.add(new Entry(id,info.title,info.nodes,file.lastModified()));}
            deleteAtomic(pending);return restored;
        }catch(IOException|RuntimeException ex){
            try{recoverBackupRestore();}catch(IOException recovery){ex.addSuppressed(recovery);}
            throw new IOException("Ripristino non completato. I progetti precedenti non sono stati sostituiti.",ex);
        }
    }}
    private static boolean existsAtomic(File file){return file.exists()||new File(file.getPath()+".bak").exists()||new File(file.getPath()+".new").exists();}
    private static void deleteAtomic(File file)throws IOException{new AtomicFile(file).delete();if(existsAtomic(file))throw new IOException("Non riesco a completare il recupero dei file del backup.");}
    private void recoverBackupRestore()throws IOException{
        File pending=new File(projects,".restore.pending");if(!existsAtomic(pending))return;
        String journal=readLimited(new AtomicFile(pending).openRead());String[] lines=journal.split("\n",-1);
        if(lines.length<3||lines.length>FlowBackup.MAX_PROJECTS+2||!lines[0].equals("FROCIART-RESTORE-1")||!lines[lines.length-1].isEmpty())throw new IOException("L'indice di recupero del backup non è valido.");
        Set<String> ids=new HashSet<>();for(int i=1;i<lines.length-1;i++)if(!lines[i].matches("[a-f0-9]{32}")||!ids.add(lines[i]))throw new IOException("L'indice di recupero del backup contiene identificativi non validi.");
        for(String id:ids)deleteAtomic(projectFile(id));deleteAtomic(pending);backupRecoveryWarning=null;
    }
    public FlowModel.Document importUri(Uri uri)throws IOException{InputStream in=context.getContentResolver().openInputStream(uri);if(in==null)throw new IOException("Il file non è leggibile.");String text=readLimited(in);FlowModel.Document imported=decodeImport(text);if(text.replace("\uFEFF","").trim().startsWith("<")){try(android.database.Cursor cursor=context.getContentResolver().query(uri,new String[]{android.provider.OpenableColumns.DISPLAY_NAME},null,null,null)){if(cursor!=null&&cursor.moveToFirst()){String title=cursor.getString(0);if(title!=null){title=title.replace('\r',' ').replace('\n',' ').trim();if(title.toLowerCase(java.util.Locale.ROOT).endsWith(".fprg"))title=title.substring(0,title.length()-5);if(!title.isEmpty())imported.title=title.substring(0,Math.min(title.length(),200));}}}catch(RuntimeException nameUnavailable){/* Display names are optional; parsed program remains usable. */}}return imported;}
    public static FlowModel.Document decodeImport(String text){String head=text==null?"":text.replace("\uFEFF","").trim();return head.startsWith("<")?FlowgorithmImport.decode(text):FlowCodec.decode(text);}
    public static String readLimited(InputStream stream)throws IOException{try(InputStream in=stream;ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] buffer=new byte[8192];int n;while((n=in.read(buffer))!=-1){if(out.size()+n>FlowModel.MAX_BYTES)throw new IOException("Il progetto supera il limite di 4 MB.");out.write(buffer,0,n);}try{return StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(out.toByteArray())).toString();}catch(java.nio.charset.CharacterCodingException ex){throw new IOException("Il progetto non contiene testo UTF-8 valido.",ex);}}}
    private static FlowModel.Document readProject(File file)throws IOException{return FlowCodec.decode(readLimited(new AtomicFile(file).openRead()));}
    private static void writeAtomic(File file,byte[] bytes)throws IOException{if(bytes.length>FlowModel.MAX_BYTES)throw new IOException("Il progetto supera il limite di 4 MB.");AtomicFile af=new AtomicFile(file);FileOutputStream stream=null;try{stream=af.startWrite();stream.write(bytes);af.finishWrite(stream);}catch(IOException e){if(stream!=null)af.failWrite(stream);throw e;}}
    public static final class Entry{public final String id,title;public final int nodes;public final long modified;Entry(String i,String t,int n,long m){id=i;title=t;nodes=n;modified=m;}}
}
