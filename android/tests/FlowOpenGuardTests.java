package it.frociart.app;

public final class FlowOpenGuardTests {
    static int checks;
    static void check(boolean condition,String label){if(!condition)throw new AssertionError(label);checks++;System.out.println("PASS "+label);}
    public static void main(String[] args){
        FlowOpenGuard guard=new FlowOpenGuard();Object doc=new Object();String project="project-a";
        FlowOpenGuard.Ticket first=guard.begin(doc,10,project);
        check(guard.current(first,doc,10,new String(project)),"unchanged project accepts its file result");
        FlowOpenGuard.Ticket second=guard.begin(doc,10,project);
        check(!guard.current(first,doc,10,project),"late first import cannot replace a newer request");
        check(guard.current(second,doc,10,project),"newest import is accepted");
        check(!guard.current(second,doc,11,project),"editing while importing rejects old result");
        check(!guard.current(second,new Object(),10,project),"replaced document rejects old result");
        check(!guard.current(second,doc,10,"project-b"),"switching project rejects old result");
        guard.cancel();check(!guard.current(second,doc,10,project),"navigation rejects both stale success and stale error");
        check(!guard.current(null,doc,10,project),"missing chooser context is rejected");
        FlowOpenGuard.Ticket restored=guard.begin(doc,10,project);
        check(guard.current(restored,doc,10,project),"restored chooser can receive its result");
        guard.cancel();check(!guard.current(restored,doc,10,project),"cancelled chooser cannot be resurrected");
        System.out.println("OPEN GUARD TESTS PASSED: "+checks);
    }
}
