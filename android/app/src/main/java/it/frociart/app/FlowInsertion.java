package it.frociart.app;

import java.util.ArrayList;
import static it.frociart.app.FlowModel.*;

/** Atomic graph splice used by dragging. Never changes coordinates or branch metadata. */
public final class FlowInsertion {
    private FlowInsertion(){}
    public static String reason(Document d,String nodeId,String edgeId){return reason(d,nodeId,edgeId,0);}
    public static String reason(Document d,String nodeId,String edgeId,int reservedEdges){
        Node n=d==null?null:d.find(nodeId);Edge edge=null;
        if(d!=null)for(Edge e:d.edges)if(e.id.equals(edgeId)){edge=e;break;}
        if(n==null||edge==null)return "Il blocco o la freccia non sono più disponibili.";
        if(n.kind==NOTE)return "Le note restano libere sul foglio e non si collegano alle frecce.";
        if(n.kind==START||n.kind==END)return "Inizio e Fine restano agli estremi del percorso.";
        if(edge.from.equals(nodeId)||edge.to.equals(nodeId))return "Questa freccia è già collegata al blocco.";
        Node source=d.find(edge.from),target=d.find(edge.to);
        if(source==null||target==null||source==target||source.kind==END||target.kind==START)return "Scegli una freccia con due estremi validi.";
        ArrayList<Edge> incoming=new ArrayList<>(),outgoing=new ArrayList<>();
        for(Edge e:d.edges){if(e.to.equals(nodeId))incoming.add(e);if(e.from.equals(nodeId))outgoing.add(e);}
        if(n.kind==DECISION){if(!incoming.isEmpty()||!outgoing.isEmpty())return "Per inserirla qui, usa una decisione ancora scollegata.";}
        else{
            if(outgoing.size()>1||outgoing.size()==1&&!outgoing.get(0).branch.equals("next"))return "Le uscite di questo blocco vanno prima corrette.";
            if(!incoming.isEmpty()&&outgoing.size()!=1)return "Manca una continuazione per richiudere il percorso precedente.";
            if(outgoing.size()==1){Edge next=outgoing.get(0);Node arrival=d.find(next.to);
                if(arrival==null||arrival.kind==START)return "La continuazione attuale non è valida.";
                if(next.to.equals(nodeId))return "Il blocco torna già su se stesso.";
                for(Edge e:incoming)if(e.from.equals(next.to))return "Il vecchio percorso non può essere richiuso senza un collegamento su se stesso.";
            }
        }
        if(n.kind!=DECISION&&outgoing.isEmpty()&&d.edges.size()+reservedEdges>=MAX_EDGES)return "Hai raggiunto il limite di collegamenti.";
        return null;
    }
    public static boolean apply(Document d,String nodeId,String edgeId,String continuation){return apply(d,nodeId,edgeId,continuation,0);}
    public static boolean apply(Document d,String nodeId,String edgeId,String continuation,int reservedEdges){
        if(reason(d,nodeId,edgeId,reservedEdges)!=null)return false;Node n=d.find(nodeId);
        if(n.kind==DECISION&&!"true".equals(continuation)&&!"false".equals(continuation)&&!"none".equals(continuation))return false;
        if(n.kind==DECISION&&!"none".equals(continuation)&&d.edges.size()+reservedEdges>=MAX_EDGES)return false;
        Edge edge=null,outgoing=null;ArrayList<Edge> incoming=new ArrayList<>();
        for(Edge e:d.edges){if(e.id.equals(edgeId))edge=e;if(e.from.equals(nodeId))outgoing=e;if(e.to.equals(nodeId))incoming.add(e);}
        String target=edge.to;
        if(outgoing!=null){String bypass=outgoing.to;for(Edge old:incoming)old.to=bypass;outgoing.to=target;}
        else if(n.kind!=DECISION||!"none".equals(continuation))d.edges.add(new Edge(nodeId,target,n.kind==DECISION?continuation:"next"));
        edge.to=nodeId;return true;
    }
}
