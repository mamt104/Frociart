package it.frociart.app;

/** Rejects a file result after another request, edit, project switch or navigation. */
final class FlowOpenGuard {
    private long generation;
    static final class Ticket {
        final long generation, revision;
        final Object document;
        final String project;
        Ticket(long g,Object d,long r,String p){generation=g;document=d;revision=r;project=p;}
    }
    Ticket begin(Object document,long revision,String project){return new Ticket(++generation,document,revision,project);}
    void cancel(){generation++;}
    boolean current(Ticket ticket,Object document,long revision,String project){
        return ticket!=null&&ticket.generation==generation&&ticket.document==document&&ticket.revision==revision&&
            java.util.Objects.equals(ticket.project,project);
    }
}
