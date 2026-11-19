package it.frociart.app;

import java.util.ArrayList;
import java.util.List;
import static it.frociart.app.FlowModel.*;

/** Large-project behavior and bounded allocations. Timing is diagnostic, not a pass threshold. */
public final class CapacityTests {
    static int checks;
    static void check(boolean result,String message){checks++;if(!result)throw new AssertionError(message);}
    static Node node(Document d,String id,int kind,String text){Node n=new Node(kind,text,0,d.nodes.size()*100);n.id=id;d.nodes.add(n);return n;}
    static void edge(Document d,String id,String from,String to,String branch){Edge e=new Edge(from,to,branch);e.id=id;d.edges.add(e);}
    static long errors(List<FlowEngine.Issue> issues){long n=0;for(FlowEngine.Issue issue:issues)if(issue.error)n++;return n;}
    public static void main(String[] args){
        Document d=new Document();node(d,"start",START,"Inizio");
        for(int i=0;i<498;i++)node(d,"p"+i,PROCESS,"x"+i+" = "+(i==0?"1":"x"+(i-1)+" + 1"));node(d,"end",END,"Fine");
        for(int i=1;i<d.nodes.size();i++)edge(d,"e"+i,d.nodes.get(i-1).id,d.nodes.get(i).id,"next");
        long started=System.nanoTime();List<FlowEngine.Issue> issues=FlowEngine.validate(d);double validation=(System.nanoTime()-started)/1e6;
        check(errors(issues)==0,"500-node variable chain validates");
        FlowEngine.Runner r=new FlowEngine.Runner(d);started=System.nanoTime();while(r.state!=FlowEngine.State.ERROR&&r.state!=FlowEngine.State.COMPLETED)r.step();double run=(System.nanoTime()-started)/1e6;
        check(r.state==FlowEngine.State.COMPLETED,"500-node chain runs");check(r.steps==500,"500 nodes exactly counted");check(r.variables.get("x497").format().equals("498"),"500-node chain result");
        String encoded=FlowCodec.encode(d);Document restored=FlowCodec.decode(encoded);check(restored.nodes.size()==500&&restored.edges.size()==499,"large file roundtrip");
        Document variables=new Document();node(variables,"start",START,"Inizio");for(int i=0;i<40;i++){StringBuilder text=new StringBuilder();for(int j=0;j<128;j++)text.append("x_").append(i).append('_').append(j).append("=1;");node(variables,"p"+i,PROCESS,text.toString());}node(variables,"end",END,"Fine");for(int i=1;i<variables.nodes.size();i++)edge(variables,"e"+i,variables.nodes.get(i-1).id,variables.nodes.get(i).id,"next");boolean limit=false;for(FlowEngine.Issue issue:FlowEngine.validate(variables))if(issue.code.equals("variable-limit"))limit=true;check(limit,"4096-variable limit enforced");
        Document capacity=d.copy();for(int i=capacity.edges.size();i<1500;i++)edge(capacity,"extra"+i,"start","end","next");check(FlowCodec.decode(FlowCodec.encode(capacity)).edges.size()==1500,"1500 edges accepted for editing");check(errors(FlowEngine.validate(capacity))>0,"capacity graph still receives structural diagnostics");
        edge(capacity,"over","start","end","next");boolean rejected=false;try{FlowCodec.encode(capacity);}catch(IllegalArgumentException ex){rejected=true;}check(rejected,"1501 edges rejected");
        Document manyIssues=new Document();node(manyIssues,"start",START,"Inizio");StringBuilder expression=new StringBuilder();for(int i=0;i<200;i++){if(i>0)expression.append('+');expression.append("unknown_").append(i);}for(int i=0;i<100;i++)node(manyIssues,"p"+i,OUTPUT,expression.toString());node(manyIssues,"end",END,"Fine");for(int i=1;i<manyIssues.nodes.size();i++)edge(manyIssues,"e"+i,manyIssues.nodes.get(i-1).id,manyIssues.nodes.get(i).id,"next");
        List<FlowEngine.Issue> capped=FlowEngine.validate(manyIssues);check(capped.size()==FlowEngine.MAX_ISSUES,"pathological diagnostics remain memory-bounded");check(capped.get(capped.size()-1).code.equals("issue-limit")&&capped.get(capped.size()-1).error,"diagnostic truncation is explicit and blocks execution");
        System.out.println("CAPACITY TESTS PASSED: "+checks+" checks");
        System.out.printf(java.util.Locale.ROOT,"HOST TIMINGS: 500-node validation %.3f ms; 500-node run %.3f ms (JVM, not phone performance).%n",validation,run);
    }
}
