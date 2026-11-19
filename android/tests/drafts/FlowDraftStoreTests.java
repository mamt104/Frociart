package it.frociart.app;

import android.content.SharedPreferences;
import java.util.ArrayList;
import java.util.Base64;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Objects;
import java.util.Set;

/** Public-contract tests; the in-memory adapter models failed commits, not Android UI. */
public final class FlowDraftStoreTests {
    private static int checks;
    private static void check(boolean ok,String message){checks++;if(!ok)throw new AssertionError(message);}
    private static void eq(Object wanted,Object actual,String message){check(Objects.equals(wanted,actual),message+" | expected="+wanted+" actual="+actual);}
    private static FlowDraftStore.Draft draft(String text){return new FlowDraftStore.Draft(text,"Domanda?","Procedi","Fermati","#22a5b8","#EF6C82");}
    private static void same(FlowDraftStore.Draft wanted,FlowDraftStore.Draft actual,String message){
        check(actual!=null,message+" exists");eq(wanted.text,actual.text,message+" text");eq(wanted.prompt,actual.prompt,message+" prompt");
        eq(wanted.trueLabel,actual.trueLabel,message+" true label");eq(wanted.falseLabel,actual.falseLabel,message+" false label");
        eq(wanted.trueColor,actual.trueColor,message+" true color");eq(wanted.falseColor,actual.falseColor,message+" false color");
        eq(wanted.comment,actual.comment,message+" comment");
    }
    private static String repeat(String text,int count){StringBuilder b=new StringBuilder();for(int i=0;i<count;i++)b.append(text);return b.toString();}
    private static String recordKey(FakePrefs prefs){for(Map.Entry<String,?> e:prefs.getAll().entrySet())if(e.getValue() instanceof String&&(((String)e.getValue()).startsWith("1|")||((String)e.getValue()).startsWith("2|")))return e.getKey();throw new AssertionError("No record");}
    private static String sequenceKey(FakePrefs prefs){for(Map.Entry<String,?> e:prefs.getAll().entrySet())if(e.getValue() instanceof Long)return e.getKey();throw new AssertionError("No sequence");}

    private static void independentDrafts(){
        FakePrefs prefs=new FakePrefs();prefs.edit().putString("theme","dark").putBoolean("animations",false).apply();FlowDraftStore store=new FlowDraftStore(prefs);
        eq(null,store.get("A","one"),"No implicit draft");eq(null,store.lastNode("A"),"Empty project has no last draft");
        store.put("A","one",draft("prima"));store.put("A","two",draft("seconda"));store.put("B","one",draft("altro progetto"));
        same(draft("prima"),store.get("A","one"),"First draft survives another block");same(draft("seconda"),store.get("A","two"),"Second draft independent");
        same(draft("altro progetto"),store.get("B","one"),"Same node ID in another project independent");
        eq("two",store.lastNode("A"),"Last node within project A");eq("one",store.lastNode("B"),"Last node within project B");
        store.put("A","one",draft("prima modificata"));eq("one",store.lastNode("A"),"Updating old draft makes it most recent");
        store.remove("A","one");eq(null,store.get("A","one"),"Remove only intended draft");eq("two",store.lastNode("A"),"Last node falls back to remaining draft");
        same(draft("altro progetto"),store.get("B","one"),"Remove does not cross projects");
        FlowDraftStore restored=new FlowDraftStore(prefs.restart());same(draft("seconda"),restored.get("A","two"),"New instance restores other block");
        restored.remove("A","absent");same(draft("seconda"),restored.get("A","two"),"Removing absent block is harmless");
        store.removeProject("A");eq(null,store.lastNode("A"),"Remove project clears its drafts");same(draft("altro progetto"),store.get("B","one"),"Project removal keeps other project");
        eq("dark",prefs.getString("theme",null),"Draft cleanup preserves theme");check(!prefs.getBoolean("animations",true),"Draft cleanup preserves animation preference");
        store.removeProject("B");eq(null,store.lastNode("B"),"Last project can be removed");store.put("B","new",draft("riparto"));eq("new",store.lastNode("B"),"Storage reusable after removing all drafts");
    }

    private static void exactTextAndKeys(){
        FakePrefs prefs=new FakePrefs();FlowDraftStore store=new FlowDraftStore(prefs);
        String special="virgolette \" / barre \\ | punti .\nseconda riga\r\tSì 😀 \uD83D";
        FlowDraftStore.Draft all=new FlowDraftStore.Draft(special,special,""," No | 😀 ","#abcdef","#012345");
        store.put("p./|😀","n./|\uD83D",all);same(all,new FlowDraftStore(prefs.restart()).get("p./|😀","n./|\uD83D"),"Exact text including unfinished Unicode survives restart");
        eq("n./|\uD83D",store.lastNode("p./|😀"),"Special ID decoded exactly");
        store.put("a.b","c",draft("uno"));store.put("a","b.c",draft("due"));store.put("a","b",draft("tre"));
        same(draft("uno"),store.get("a.b","c"),"Project and node separators cannot collide");same(draft("due"),store.get("a","b.c"),"Node separator cannot collide");
        store.removeProject("a");same(draft("uno"),store.get("a.b","c"),"Removing project prefix keeps longer project name");
        store.put("functions","main",draft("main draft"));store.put("functions","deletedFunctionBlock",draft("function draft"));
        eq("main",store.lastNode("functions",id->id.equals("main")),"A deleted function draft does not hide the last existing block");
        eq(null,store.lastNode("functions",id->false),"No existing nodes yields no draft to reopen");
        eq("deletedFunctionBlock",store.lastNode("functions"),"Skipped function draft retained for Undo");
        FlowDraftStore.Draft empty=new FlowDraftStore.Draft("","","","","#000000","#ffffff");store.put("empty","node",empty);same(empty,store.get("empty","node"),"Incomplete editing fields can be empty");
        FlowDraftStore.Draft maximum=new FlowDraftStore.Draft(repeat("字",8192),repeat("à",1024),repeat("v",60),repeat("f",60),"#123ABC","#000000");store.put("max","node",maximum);
        same(maximum,new FlowDraftStore(prefs.restart()).get("max","node"),"Full supported field lengths round trip");
        FlowDraftStore.Draft defaults=new FlowDraftStore.Draft(null,null,null,null,null,null);
        same(new FlowDraftStore.Draft("","","Sì","No","#39B985","#EF6C82"),defaults,"Null optional values use model defaults");
    }

    private static void legacy(FakePrefs prefs,String text){prefs.edit().putString("draft_project","old").putString("draft_node","node").putString("draft_text",text).apply();}
    private static void migratedLegacy(){
        FakePrefs prefs=new FakePrefs();legacy(prefs,"totale = 7");prefs.edit().putString("draft_prompt","Scrivi un numero").putString("draft_tl","Supera la soglia")
                .putString("draft_fl","Riprova").putString("draft_tc","#9771DC").putString("draft_fc","#D68A18").putString("theme","dark").apply();
        FlowDraftStore.Draft wanted=new FlowDraftStore.Draft("totale = 7","Scrivi un numero","Supera la soglia","Riprova","#9771DC","#D68A18");
        prefs.commits.clear();FlowDraftStore store=new FlowDraftStore(prefs);same(wanted,store.get("old","node"),"Legacy draft migrated exactly");eq("node",store.lastNode("old"),"Migrated draft can reopen automatically");
        for(String k:new String[]{"draft_project","draft_node","draft_text","draft_prompt","draft_tl","draft_fl","draft_tc","draft_fc"})check(!prefs.contains(k),"Legacy key cleaned after success: "+k);
        eq(2,prefs.commits.size(),"Migration writes a durable new record before legacy cleanup");
        check(prefs.commits.get(0).containsKey("draft_text"),"First commit still contains legacy text");check(!prefs.commits.get(1).containsKey("draft_text"),"Second commit removes legacy text");
        same(wanted,new FlowDraftStore(prefs.restart()).get("old","node"),"Migrated draft survives restart");eq("dark",prefs.getString("theme",null),"Migration leaves unrelated preferences intact");
        FakePrefs minimal=new FakePrefs();legacy(minimal,"");FlowDraftStore minimalStore=new FlowDraftStore(minimal);
        same(new FlowDraftStore.Draft("","","Sì","No","#39B985","#EF6C82"),minimalStore.get("old","node"),"Minimal legacy draft gets only missing optional defaults");
        FakePrefs mixed=new FakePrefs();FlowDraftStore existing=new FlowDraftStore(mixed);existing.put("old","node",draft("nuova bozza"));legacy(mixed,"vecchia bozza");
        same(draft("nuova bozza"),new FlowDraftStore(mixed).get("old","node"),"Valid new record wins over legacy duplicate");check(!mixed.contains("draft_text"),"Obsolete valid legacy duplicate cleaned");
    }

    private static void invalidLegacyRetained(){
        for(int variant=0;variant<7;variant++){
            FakePrefs prefs=new FakePrefs();legacy(prefs,"testo da non perdere");SharedPreferences.Editor edit=prefs.edit();
            if(variant==0)edit.remove("draft_node");if(variant==1)edit.putInt("draft_text",17);if(variant==2)edit.putString("draft_project","");
            if(variant==3)edit.putBoolean("draft_prompt",true);if(variant==4)edit.putString("draft_tc","rosso");
            if(variant==5)edit.putString("draft_tl",repeat("x",61));if(variant==6)edit.putString("draft_text",repeat("x",8193));edit.apply();
            Map<String,?> before=prefs.getAll();FlowDraftStore store=new FlowDraftStore(prefs);eq(before,prefs.getAll(),"Invalid legacy preserved untouched case "+variant);
            eq(null,store.get("old","node"),"Invalid legacy not exposed as a usable draft case "+variant);
        }
    }

    private static void failedMigration(){
        FakePrefs first=new FakePrefs();legacy(first,"da conservare");first.failCommit=1;FlowDraftStore one=new FlowDraftStore(first);
        check(first.contains("draft_text"),"First failed commit retains legacy keys in memory");check(first.disk.containsKey("draft_text"),"First failed commit retains legacy keys on disk");
        eq("da conservare",one.get("old","node").text,"Failed migration does not hide draft this session");
        FakePrefs restarted=first.restart();same(new FlowDraftStore.Draft("da conservare",null,null,null,null,null),new FlowDraftStore(restarted).get("old","node"),"Next launch retries failed migration");
        check(!restarted.contains("draft_text"),"Successful retry can clean legacy");
        FakePrefs second=new FakePrefs();legacy(second,"commit finale fallito");second.failCommit=2;FlowDraftStore two=new FlowDraftStore(second);
        check(second.disk.containsKey("draft_text"),"Cleanup commit failure leaves recoverable old disk copy");eq("commit finale fallito",two.get("old","node").text,"New draft remains readable after cleanup failure");
        FakePrefs reboot=second.restart();FlowDraftStore recovered=new FlowDraftStore(reboot);eq("commit finale fallito",recovered.get("old","node").text,"Restart after cleanup failure keeps draft");check(!reboot.contains("draft_text"),"Retry cleans legacy without overwriting new draft");
        FakePrefs discard=new FakePrefs();legacy(discard,"scarta");discard.failCommit=1;FlowDraftStore discardStore=new FlowDraftStore(discard);discardStore.remove("old","node");
        eq(null,new FlowDraftStore(discard.restart()).get("old","node"),"Explicit discard cannot resurrect a legacy draft after failed migration");
        FakePrefs delete=new FakePrefs();legacy(delete,"elimina progetto");delete.failCommit=1;FlowDraftStore deleteStore=new FlowDraftStore(delete);deleteStore.removeProject("old");
        eq(null,new FlowDraftStore(delete.restart()).lastNode("old"),"Project removal also clears matching legacy slot after failure");
    }

    private static void corruptRecordsAndMetadata(){
        FakePrefs prefs=new FakePrefs();FlowDraftStore store=new FlowDraftStore(prefs);store.put("A","keep",draft("rimane"));
        Set<String> oldKeys=new HashSet<>(prefs.getAll().keySet());store.put("A","bad",draft("modifica"));String key=null;for(String candidate:prefs.getAll().keySet())if(!oldKeys.contains(candidate))key=candidate;
        check(key!=null,"Second draft has its own record");String valid=prefs.getString(key,null);
        Object[] corrupt={Boolean.TRUE,"broken",valid.replaceFirst("[12]\\|","9|"),valid.replaceFirst("[12]\\|[0-9]+\\|","2|-4|"),valid+"|extra",valid.substring(0,valid.lastIndexOf('|')),"1|9|%%%|||||",repeat("x",40001)};
        for(int i=0;i<corrupt.length;i++){
            prefs.memory.put(key,corrupt[i]);eq(null,store.get("A","bad"),"Corrupt record not used case "+i);eq("keep",store.lastNode("A"),"Bad newest record does not hide older draft case "+i);
            check(prefs.contains(key),"Unreadable entry not silently deleted case "+i);
        }
        store.put("A","bad",draft("riparata"));same(draft("riparata"),store.get("A","bad"),"Editing can replace damaged draft");eq("bad",store.lastNode("A"),"Repaired record is most recent");
        String sequence=sequenceKey(prefs);prefs.edit().putString(sequence,"metadato non valido").apply();store.put("A","keep",draft("ultima"));eq("keep",store.lastNode("A"),"Sequence recovers after metadata type corruption");
        prefs.edit().putLong(sequence,Long.MAX_VALUE).apply();store.put("A","bad",draft("dopo overflow"));eq("bad",store.lastNode("A"),"Corrupt maximum counter cannot block typing");
        same(draft("ultima"),store.get("A","keep"),"Counter repair keeps other payload");
        FakePrefs fallback=new FakePrefs();FlowDraftStore initial=new FlowDraftStore(fallback);initial.put("old","node",draft("rotta"));fallback.edit().putString(recordKey(fallback),"unreadable").apply();legacy(fallback,"recuperata");
        eq("recuperata",new FlowDraftStore(fallback).get("old","node").text,"Valid legacy can recover a corrupt new-format target");
    }

    private static void invalidArguments(){
        FakePrefs prefs=new FakePrefs();FlowDraftStore store=new FlowDraftStore(prefs);store.put("A","one",draft("salva"));
        eq(null,store.get(null,"one"),"Null project lookup harmless");eq(null,store.get("A",null),"Null node lookup harmless");eq(null,store.lastNode(""),"Empty project lookup harmless");
        store.remove(null,"one");store.removeProject(null);same(draft("salva"),store.get("A","one"),"Invalid removals do not change other drafts");
        int caught=0;try{store.put("A","one",null);}catch(IllegalArgumentException expected){caught++;}
        try{store.put("","one",draft("x"));}catch(IllegalArgumentException expected){caught++;}
        try{store.put("A","one",new FlowDraftStore.Draft("x","","Sì","No","#GGGGGG","#000000"));}catch(IllegalArgumentException expected){caught++;}
        try{store.put("A","one",draft(repeat("x",8193)));}catch(IllegalArgumentException expected){caught++;}
        eq(4,caught,"Invalid writes rejected before touching existing draft");same(draft("salva"),store.get("A","one"),"Rejected writes retain original draft");
    }

    private static void rewriteDrafts()throws Exception{
        FakePrefs prefs=new FakePrefs();FlowDraftStore store=new FlowDraftStore(prefs);
        store.put("A","first",draft("old(1)"));store.put("A","second",draft("old(2)"));store.put("B","first",draft("old(3)"));
        String order=store.lastNode("A");Map<String,?> original=prefs.getAll();int[] writes={0};
        try{store.rewriteAndSave("A",(node,text)->{if(node.equals("second"))throw new IllegalArgumentException("collision");return "new(1)";},"new",()->writes[0]++);throw new AssertionError("Must reject all rewrites");}catch(IllegalArgumentException expected){checks++;}
        eq(original,prefs.getAll(),"Collision leaves every draft and metadata unchanged");eq(0,writes[0],"Collision prevents project save");
        for(String invalid:new String[]{null,repeat("x",8193)}){
            try{store.rewriteAndSave("A",(node,text)->invalid,"new",()->writes[0]++);throw new AssertionError("Invalid rewrite accepted");}catch(IllegalArgumentException expected){checks++;}
            eq(original,prefs.getAll(),"Invalid rewrite is atomic");
        }
        store.rewriteAndSave("A",(node,text)->text.replace("old","new"),"new",()->{writes[0]++;check(store.hasPendingRewrite("A"),"Journal durable before project save");same(draft("old(1)"),store.get("A","first"),"Old draft stays until project saved");});
        same(draft("new(1)"),store.get("A","first"),"First draft rewritten with other fields preserved");same(draft("new(2)"),store.get("A","second"),"Second draft rewritten");same(draft("old(3)"),store.get("B","first"),"Other project untouched");eq(order,store.lastNode("A"),"Recovery order unchanged");check(!store.hasPendingRewrite("A"),"Successful rename removes journal");
        store.put("A","first",draft("new(99)"));store.rewriteAndSave("A",(node,text)->text.replace("new","old"),"old",()->writes[0]++);
        same(draft("old(99)"),new FlowDraftStore(prefs.restart()).get("A","first"),"Undo rewrite keeps newer draft edits through restart");
        int commits=prefs.commitCount;store.rewriteAndSave("A",(node,text)->text,"same",()->writes[0]++);eq(commits,prefs.commitCount,"No draft changes need no journal");
        FakePrefs corrupt=new FakePrefs();FlowDraftStore damaged=new FlowDraftStore(corrupt);damaged.put("A","one",draft("x"));String key=recordKey(corrupt);corrupt.edit().putString(key,"unreadable").apply();damaged.rewriteAndSave("A",(node,text)->"y","new",()->{});eq("unreadable",corrupt.getString(key,null),"Unknown draft data preserved exactly");
    }

    private static void interruptedRename()throws Exception{
        for(boolean saved:new boolean[]{false,true}){
            FakePrefs prefs=new FakePrefs();FlowDraftStore store=new FlowDraftStore(prefs);store.put("A","one",draft("old"));store.put("B","one",draft("other"));
            try{store.rewriteAndSave("A",(node,text)->"new","new-project",()->{throw new java.io.IOException("simulated interruption");});throw new AssertionError("Missing interruption");}catch(java.io.IOException expected){checks++;}
            FakePrefs disk=prefs.restart();FlowDraftStore recovered=new FlowDraftStore(disk);check(recovered.hasPendingRewrite("A"),"Interrupted edit leaves durable journal");
            recovered.recoverRewrite("A",saved?"new-project":"old-project");same(draft(saved?"new":"old"),recovered.get("A","one"),"Recovery chooses actual project version: "+saved);
            same(draft("other"),recovered.get("B","one"),"Recovery keeps unrelated project");check(!recovered.hasPendingRewrite("A"),"Recovery clears journal");
            same(draft(saved?"new":"old"),new FlowDraftStore(disk.restart()).get("A","one"),"Recovery itself survives second restart");
        }
        FakePrefs denied=new FakePrefs();FlowDraftStore store=new FlowDraftStore(denied);store.put("A","one",draft("old"));denied.failCommit=denied.commitCount+1;boolean[] saved={false};
        try{store.rewriteAndSave("A",(node,text)->"new","new-project",()->saved[0]=true);throw new AssertionError("Failed journal accepted");}catch(java.io.IOException expected){checks++;}
        check(!saved[0],"Failed journal prevents project mutation");same(draft("old"),new FlowDraftStore(denied.restart()).get("A","one"),"Failed journal preserves old draft on disk");
        FakePrefs cleanup=new FakePrefs();FlowDraftStore retry=new FlowDraftStore(cleanup);retry.put("A","one",draft("old"));cleanup.failCommit=cleanup.commitCount+2;
        retry.rewriteAndSave("A",(node,text)->"new","new-project",()->{});same(draft("new"),retry.get("A","one"),"Failed cleanup keeps coherent in-memory draft");
        FakePrefs reboot=cleanup.restart();FlowDraftStore recovery=new FlowDraftStore(reboot);check(recovery.hasPendingRewrite("A"),"Failed cleanup retains journal on disk");recovery.recoverRewrite("A","new-project");same(draft("new"),recovery.get("A","one"),"Retry completes failed cleanup without losing text");
        FakePrefs deletion=new FakePrefs();FlowDraftStore discard=new FlowDraftStore(deletion);discard.put("A","one",draft("old"));try{discard.rewriteAndSave("A",(node,text)->"new","new",()->{throw new java.io.IOException("stop");});}catch(java.io.IOException expected){}
        discard.removeProject("A");check(!discard.hasPendingRewrite("A")&&discard.lastNode("A")==null,"Deleting project removes its pending journal too");
    }

    private static void comments()throws Exception{
        FakePrefs prefs=new FakePrefs();FlowDraftStore store=new FlowDraftStore(prefs);
        String note="Non riscrivere old(1) nel commento\n🐢 | "+repeat("x",1900);
        FlowDraftStore.Draft d=new FlowDraftStore.Draft("old(1)","Domanda?","Procedi","Fermati","#22a5b8","#EF6C82",note);
        store.put("A","comment",d);same(d,new FlowDraftStore(prefs.restart()).get("A","comment"),"Comment draft survives restart");
        store.rewriteAndSave("A",(node,text)->text.replace("old","new"),"updated",()->{});
        eq(note,store.get("A","comment").comment,"Function rename preserves note literally");eq("new(1)",store.get("A","comment").text,"Function rename changes instruction");
        String key=recordKey(prefs),record=prefs.getString(key,null);prefs.edit().putString(key,record.substring(0,record.lastIndexOf('|')).replaceFirst("2\\|","1|")).apply();
        eq("",new FlowDraftStore(prefs).get("A","comment").comment,"Previous eight-field record remains readable");
        store.put("A","comment",new FlowDraftStore.Draft("x",null,null,null,null,null,repeat("x",2048)));eq(2048,store.get("A","comment").comment.length(),"Full note fits draft record");
        try{store.put("A","comment",new FlowDraftStore.Draft("x",null,null,null,null,null,repeat("x",2049)));throw new AssertionError("Oversize note accepted");}catch(IllegalArgumentException expected){checks++;}
    }
    public static void main(String[] args)throws Exception{
        independentDrafts();exactTextAndKeys();migratedLegacy();invalidLegacyRetained();failedMigration();corruptRecordsAndMetadata();invalidArguments();
        rewriteDrafts();interruptedRename();comments();
        System.out.println("DRAFT STORE CHECKS PASSED: "+checks);
    }

    private static final class FakePrefs implements SharedPreferences {
        final Map<String,Object> memory=new LinkedHashMap<>(),disk=new LinkedHashMap<>();
        final ArrayList<Map<String,Object>> commits=new ArrayList<>();int commitCount,failCommit=-1;
        FakePrefs restart(){FakePrefs next=new FakePrefs();next.memory.putAll(disk);next.disk.putAll(disk);return next;}
        public Map<String,?> getAll(){return new LinkedHashMap<>(memory);}
        public String getString(String key,String fallback){Object v=memory.get(key);return v==null?fallback:(String)v;}
        @SuppressWarnings("unchecked") public Set<String> getStringSet(String key,Set<String> fallback){Object v=memory.get(key);return v==null?fallback:new HashSet<>((Set<String>)v);}
        public int getInt(String key,int fallback){Object v=memory.get(key);return v==null?fallback:(Integer)v;}
        public long getLong(String key,long fallback){Object v=memory.get(key);return v==null?fallback:(Long)v;}
        public float getFloat(String key,float fallback){Object v=memory.get(key);return v==null?fallback:(Float)v;}
        public boolean getBoolean(String key,boolean fallback){Object v=memory.get(key);return v==null?fallback:(Boolean)v;}
        public boolean contains(String key){return memory.containsKey(key);}
        public void registerOnSharedPreferenceChangeListener(OnSharedPreferenceChangeListener listener){}
        public void unregisterOnSharedPreferenceChangeListener(OnSharedPreferenceChangeListener listener){}
        public Editor edit(){return new Edit();}
        private final class Edit implements Editor {
            final Map<String,Object> changes=new HashMap<>();final Set<String> removals=new HashSet<>();boolean clear;
            private Editor value(String k,Object v){if(v==null)return remove(k);removals.remove(k);changes.put(k,v);return this;}
            public Editor putString(String k,String v){return value(k,v);}
            public Editor putStringSet(String k,Set<String> v){return value(k,v==null?null:new HashSet<>(v));}
            public Editor putInt(String k,int v){return value(k,v);}public Editor putLong(String k,long v){return value(k,v);}
            public Editor putFloat(String k,float v){return value(k,v);}public Editor putBoolean(String k,boolean v){return value(k,v);}
            public Editor remove(String k){changes.remove(k);removals.add(k);return this;}public Editor clear(){clear=true;return this;}
            private void updateMemory(){if(clear)memory.clear();for(String k:removals)memory.remove(k);memory.putAll(changes);}
            public boolean commit(){updateMemory();commitCount++;commits.add(new LinkedHashMap<>(memory));if(commitCount==failCommit)return false;disk.clear();disk.putAll(memory);return true;}
            public void apply(){updateMemory();disk.clear();disk.putAll(memory);}
        }
    }
}
