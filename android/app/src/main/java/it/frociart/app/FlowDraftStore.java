package it.frociart.app;

import android.content.SharedPreferences;
import java.util.ArrayList;
import java.util.Base64;
import java.util.Map;

/** Independent, offline editor drafts. No project file is modified by this store. */
public final class FlowDraftStore {
    private static final String PREFIX="frociart.drafts.v2.";
    private static final String RECORD=PREFIX+"entry.";
    private static final String SEQUENCE=PREFIX+"sequence";
    private static final String PENDING=PREFIX+"rename.";
    private static final String[] LEGACY={"draft_project","draft_node","draft_text","draft_prompt",
            "draft_tl","draft_fl","draft_tc","draft_fc"};
    private static final int MAX_RECORD=40000;
    private final SharedPreferences prefs;

    /** Nullable optional fields use the same defaults as a newly created block. */
    public static final class Draft {
        public final String text,prompt,trueLabel,falseLabel,trueColor,falseColor,comment;
        public Draft(String text,String prompt,String trueLabel,String falseLabel,String trueColor,String falseColor){
            this(text,prompt,trueLabel,falseLabel,trueColor,falseColor,"");
        }
        public Draft(String text,String prompt,String trueLabel,String falseLabel,String trueColor,String falseColor,String comment){
            this.comment=comment==null?"":comment;
            this.text=text==null?"":text;this.prompt=prompt==null?"":prompt;
            this.trueLabel=trueLabel==null?"Sì":trueLabel;this.falseLabel=falseLabel==null?"No":falseLabel;
            this.trueColor=trueColor==null?"#39B985":trueColor;this.falseColor=falseColor==null?"#EF6C82":falseColor;
        }
    }
    private static final class Stored {
        final long order;final Draft draft;
        Stored(long order,Draft draft){this.order=order;this.draft=draft;}
    }
    private static final class LegacyDraft {
        final String project,node;final Draft draft;
        LegacyDraft(String project,String node,Draft draft){this.project=project;this.node=node;this.draft=draft;}
    }

    public FlowDraftStore(SharedPreferences prefs){
        if(prefs==null)throw new IllegalArgumentException("Preferenze mancanti.");
        this.prefs=prefs;synchronized(prefs){migrateLegacy();}
    }

    public Draft get(String project,String node){
        if(!validId(project)||!validId(node))return null;
        synchronized(prefs){
            Map<String,?> all=prefs.getAll();Stored stored=read(all.get(key(project,node)));
            if(stored!=null)return stored.draft;
            // A failed one-time disk migration must not make the previous draft disappear.
            LegacyDraft legacy=legacy(all);
            return legacy!=null&&project.equals(legacy.project)&&node.equals(legacy.node)?legacy.draft:null;
        }
    }

    /** Most recently edited draft still present in this project; null when none exists. */
    public String lastNode(String project){return lastNode(project,node->true);}
    /** Skip removed blocks while retaining their drafts for Undo. */
    public String lastNode(String project,java.util.function.Predicate<String> exists){
        if(!validId(project))return null;
        synchronized(prefs){
            Map<String,?> all=prefs.getAll();String prefix=projectPrefix(project),last=null;long order=-1;
            for(Map.Entry<String,?> entry:all.entrySet())if(entry.getKey().startsWith(prefix)){
                String node=decode(entry.getKey().substring(prefix.length()));Stored stored=read(entry.getValue());
                if(validId(node)&&stored!=null&&exists.test(node)&&(stored.order>order||stored.order==order&&(last==null||node.compareTo(last)<0))){
                    last=node;order=stored.order;
                }
            }
            if(last!=null)return last;
            LegacyDraft legacy=legacy(all);return legacy!=null&&project.equals(legacy.project)&&exists.test(legacy.node)?legacy.node:null;
        }
    }

    public void put(String project,String node,Draft draft){
        if(!validId(project)||!validId(node)||!valid(draft))throw new IllegalArgumentException("Bozza non valida.");
        synchronized(prefs){
            long order=nextSequence(prefs.getAll());
            prefs.edit().putString(key(project,node),encode(new Stored(order,draft))).putLong(SEQUENCE,order).apply();
        }
    }

    /** Remove only this block's draft, including a matching legacy slot after a failed migration. */
    public void remove(String project,String node){
        if(!validId(project)||!validId(node))return;
        synchronized(prefs){
            Map<String,?> all=prefs.getAll();SharedPreferences.Editor edit=prefs.edit().remove(key(project,node));
            if(project.equals(all.get("draft_project"))&&node.equals(all.get("draft_node")))clearLegacy(edit);
            edit.apply();
        }
    }

    public void removeProject(String project){
        if(!validId(project))return;
        synchronized(prefs){
            Map<String,?> all=prefs.getAll();String prefix=projectPrefix(project);SharedPreferences.Editor edit=prefs.edit();
            for(String key:all.keySet())if(key.startsWith(prefix)||key.startsWith(pendingPrefix(project)))edit.remove(key);
            if(project.equals(all.get("draft_project")))clearLegacy(edit);
            edit.apply();
        }
    }

    public interface ProjectWrite {void write()throws java.io.IOException;}

    public boolean hasPendingRewrite(String project){synchronized(prefs){return prefs.contains(pendingPrefix(project)+"target");}}

    /** Complete or discard a staged rename using the actual, atomically saved project fingerprint. */
    public void recoverRewrite(String project,String currentVersion)throws java.io.IOException{
        synchronized(prefs){
            Map<String,?> all=prefs.getAll();String pending=pendingPrefix(project),target=pending+"target";
            if(!all.containsKey(target))return;
            if(!(all.get(target) instanceof String))throw new java.io.IOException("Il recupero delle bozze richiede attenzione. I testi originali sono conservati.");
            boolean saved=all.get(target).equals(currentVersion);SharedPreferences.Editor edit=prefs.edit();
            for(Map.Entry<String,?> entry:all.entrySet())if(entry.getKey().startsWith(pending)){
                String suffix=entry.getKey().substring(pending.length());
                if(suffix.startsWith("value.")){
                    String key=suffix.substring(6);
                    if(!(entry.getValue() instanceof String)||!(key.startsWith(projectPrefix(project))&&read(entry.getValue())!=null||key.equals("draft_text")))throw new java.io.IOException("Una bozza da recuperare non è leggibile. Gli originali sono conservati.");
                    if(saved)edit.putString(key,(String)entry.getValue());
                }
                edit.remove(entry.getKey());
            }
            // A failed cleanup still leaves the durable journal on disk. The in-memory
            // state is coherent, and the next launch retries the same recovery.
            edit.commit();
        }
    }

    /** Stage all new drafts durably before writing the project; a restart can finish either outcome. */
    public void rewriteAndSave(String project,java.util.function.BiFunction<String,String,String> rewrite,String targetVersion,ProjectWrite save)throws java.io.IOException{
        if(!validId(project))throw new IllegalArgumentException("Progetto non valido.");
        synchronized(prefs){
            if(hasPendingRewrite(project))throw new java.io.IOException("Completa il recupero del progetto prima di rinominare.");
            Map<String,?> all=prefs.getAll();String prefix=projectPrefix(project);
            java.util.LinkedHashMap<String,String> updates=new java.util.LinkedHashMap<>();
            for(Map.Entry<String,?> entry:all.entrySet())if(entry.getKey().startsWith(prefix)){
                String node=decode(entry.getKey().substring(prefix.length()));Stored stored=read(entry.getValue());
                if(!validId(node)||stored==null)continue;Draft d=stored.draft;String text=rewrite.apply(node,d.text);
                Draft result=new Draft(text,d.prompt,d.trueLabel,d.falseLabel,d.trueColor,d.falseColor,d.comment);
                if(text==null||!valid(result))throw new IllegalArgumentException("La rinomina rende una bozza troppo lunga o non valida.");
                if(!text.equals(d.text))updates.put(entry.getKey(),encode(new Stored(stored.order,result)));
            }
            LegacyDraft legacy=legacy(all);String legacyText=null;
            if(legacy!=null&&project.equals(legacy.project)&&read(all.get(key(project,legacy.node)))==null){
                legacyText=rewrite.apply(legacy.node,legacy.draft.text);Draft d=legacy.draft;
                if(legacyText==null||!valid(new Draft(legacyText,d.prompt,d.trueLabel,d.falseLabel,d.trueColor,d.falseColor,d.comment)))throw new IllegalArgumentException("La rinomina rende una bozza troppo lunga o non valida.");
            }
            if(legacyText!=null&&!legacyText.equals(legacy.draft.text))updates.put("draft_text",legacyText);
            if(updates.isEmpty()){save.write();return;}
            if(targetVersion==null||targetVersion.isEmpty())throw new IllegalArgumentException("Versione del progetto mancante.");
            String pending=pendingPrefix(project);SharedPreferences.Editor stage=prefs.edit().putString(pending+"target",targetVersion);
            for(Map.Entry<String,String> entry:updates.entrySet())stage.putString(pending+"value."+entry.getKey(),entry.getValue());
            if(!stage.commit()){
                recoverRewrite(project,"");
                throw new java.io.IOException("Non riesco a conservare le bozze. Il progetto non è stato modificato; riprova il salvataggio.");
            }
            save.write();recoverRewrite(project,targetVersion);
        }
    }

    private static String pendingPrefix(String project){return PENDING+encode(project)+".";}

    private void migrateLegacy(){
        Map<String,?> all=prefs.getAll();LegacyDraft legacy=legacy(all);if(legacy==null)return;
        String key=key(legacy.project,legacy.node);Stored existing=read(all.get(key));
        if(existing==null){
            long order=nextSequence(all);String encoded=encode(new Stored(order,legacy.draft));
            // Two phases, once per upgrade. If the write fails, retain every old key.
            if(!prefs.edit().putString(key,encoded).putLong(SEQUENCE,order).commit())return;
            Object saved=prefs.getAll().get(key);
            if(!encoded.equals(saved)||read(saved)==null)return;
        }
        // A valid new-format draft always wins over an older legacy copy.
        SharedPreferences.Editor edit=prefs.edit();clearLegacy(edit);edit.commit();
    }

    private static LegacyDraft legacy(Map<String,?> all){
        Object project=all.get("draft_project"),node=all.get("draft_node"),text=all.get("draft_text");
        if(!(project instanceof String)||!(node instanceof String)||!(text instanceof String)
                ||!validId((String)project)||!validId((String)node))return null;
        for(String optional:new String[]{"draft_prompt","draft_tl","draft_fl","draft_tc","draft_fc"})
            if(all.containsKey(optional)&&!(all.get(optional) instanceof String))return null;
        Draft draft=new Draft((String)text,(String)all.get("draft_prompt"),(String)all.get("draft_tl"),
                (String)all.get("draft_fl"),(String)all.get("draft_tc"),(String)all.get("draft_fc"));
        return valid(draft)?new LegacyDraft((String)project,(String)node,draft):null;
    }
    private static void clearLegacy(SharedPreferences.Editor edit){for(String key:LEGACY)edit.remove(key);}
    private static boolean validId(String id){return id!=null&&!id.trim().isEmpty()&&id.length()<=128;}
    private static boolean valid(Draft d){
        return d!=null&&d.comment.length()<=2048&&d.text.length()<=8192&&d.prompt.length()<=1024&&d.trueLabel.length()<=60
                &&d.falseLabel.length()<=60&&color(d.trueColor)&&color(d.falseColor);
    }
    private static boolean color(String value){
        if(value==null||value.length()!=7||value.charAt(0)!='#')return false;
        for(int i=1;i<7;i++){char c=value.charAt(i);if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;}
        return true;
    }
    private static String projectPrefix(String project){return RECORD+encode(project)+".";}
    private static String key(String project,String node){return projectPrefix(project)+encode(node);}
    private long nextSequence(Map<String,?> all){
        Object saved=all.get(SEQUENCE);long last=saved instanceof Long?(Long)saved:0;
        // Recover ordering if metadata was lost/corrupted; only parse headers, not every draft body.
        for(Map.Entry<String,?> entry:all.entrySet())if(entry.getKey().startsWith(RECORD))last=Math.max(last,order(entry.getValue()));
        if(last==Long.MAX_VALUE){
            // Damaged metadata must not block typing. Renumber valid records, preserving
            // their relative order and payloads; leave unreadable entries untouched.
            ArrayList<Map.Entry<String,?>> entries=new ArrayList<>();
            for(Map.Entry<String,?> entry:all.entrySet())if(entry.getKey().startsWith(RECORD)&&read(entry.getValue())!=null)entries.add(entry);
            entries.sort((a,b)->{int result=Long.compare(order(a.getValue()),order(b.getValue()));return result!=0?result:a.getKey().compareTo(b.getKey());});
            SharedPreferences.Editor edit=prefs.edit();last=0;
            for(Map.Entry<String,?> entry:entries)edit.putString(entry.getKey(),encode(new Stored(++last,read(entry.getValue()).draft)));
            edit.putLong(SEQUENCE,last).apply();
        }
        return Math.max(0,last)+1;
    }
    private static long order(Object raw){
        if(!(raw instanceof String))return -1;String value=(String)raw;
        if(value.length()>MAX_RECORD||!(value.startsWith("1|")||value.startsWith("2|")))return -1;
        int end=value.indexOf('|',2);if(end<0)return -1;
        try{long n=Long.parseLong(value.substring(2,end));return n>0?n:-1;}catch(NumberFormatException ignored){return -1;}
    }
    private static String encode(Stored stored){
        Draft d=stored.draft;return "2|"+stored.order+"|"+encode(d.text)+"|"+encode(d.prompt)+"|"+encode(d.trueLabel)
                +"|"+encode(d.falseLabel)+"|"+encode(d.trueColor)+"|"+encode(d.falseColor)+"|"+encode(d.comment);
    }
    private static Stored read(Object raw){
        long order=order(raw);if(order<0)return null;String[] fields=((String)raw).split("\\|",-1);if(fields.length!=(fields[0].equals("1")?8:9))return null;
        String[] decoded=new String[7];decoded[6]="";for(int i=0;i<fields.length-2;i++){decoded[i]=decode(fields[i+2]);if(decoded[i]==null)return null;}
        Draft d=new Draft(decoded[0],decoded[1],decoded[2],decoded[3],decoded[4],decoded[5],decoded[6]);
        return valid(d)?new Stored(order,d):null;
    }

    // Encode UTF-16 code units exactly, including unfinished surrogate input in an editor.
    // The URL-safe alphabet excludes '.' and '|', so IDs cannot collide with separators.
    private static String encode(String text){
        byte[] bytes=new byte[text.length()*2];for(int i=0;i<text.length();i++){char c=text.charAt(i);bytes[i*2]=(byte)(c>>>8);bytes[i*2+1]=(byte)c;}
        return Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
    }
    private static String decode(String text){
        if(text==null||text.length()>MAX_RECORD)return null;
        try{byte[] bytes=Base64.getUrlDecoder().decode(text);if((bytes.length&1)!=0)return null;char[] chars=new char[bytes.length/2];
            for(int i=0;i<chars.length;i++)chars[i]=(char)(((bytes[i*2]&255)<<8)|(bytes[i*2+1]&255));return new String(chars);
        }catch(IllegalArgumentException ignored){return null;}
    }
}
