package it.frociart.app;
import java.util.*;

/** View-local identities. Retired virtual IDs are never assigned to another item. */
final class FlowVirtualIds {
    static final class Entry {
        final int virtualId; final String key,id;
        final Object owner,model,from,to; final String[] semantics;
        Entry(int virtualId,String key,String id,Object owner,Object model,Object from,Object to,String[] semantics){
            this.virtualId=virtualId;this.key=key;this.id=id;this.owner=owner;this.model=model;this.from=from;this.to=to;this.semantics=semantics.clone();
        }
        boolean matches(Object owner,Object model,Object from,Object to,String... semantics){
            return this.owner==owner&&this.model==model&&this.from==from&&this.to==to&&Arrays.equals(this.semantics,semantics);
        }
    }
    private long next=1;
    private Object owner;
    private Map<String,Entry> live=new HashMap<>(),building;
    private Map<Integer,Entry> ids=new HashMap<>(),buildingIds;
    void begin(Object owner){this.owner=owner;building=new HashMap<>();buildingIds=new HashMap<>();}
    Entry bind(String key,String id,Object model,Object from,Object to,String... semantics){
        Entry entry=live.get(key);
        if(entry==null||!entry.matches(owner,model,from,to,semantics)){
            if(next>Integer.MAX_VALUE)return null;
            entry=new Entry((int)next++,key,id,owner,model,from,to,semantics);
        }
        building.put(key,entry);buildingIds.put(entry.virtualId,entry);return entry;
    }
    void finish(){live=building;ids=buildingIds;building=null;buildingIds=null;}
    Entry get(String key){return live.get(key);}
    Entry get(int id){return ids.get(id);}
}
