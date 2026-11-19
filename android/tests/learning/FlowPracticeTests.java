package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Exercises the actual interpreter/checker, including failed solutions and preserved documents. */
public final class FlowPracticeTests {
    private static int checks;
    private static void check(boolean value,String message){if(!value)throw new AssertionError(message);checks++;}
    private static Node add(Document d,int kind,String text){Node n=new Node(kind,text,15,d.nodes.size()*100);d.nodes.add(n);return n;}
    private static void edge(Document d,Node a,Node b,String branch){d.edges.add(new Edge(a.id,b.id,branch));}
    private static Node kind(Document d,int kind){for(Node n:d.nodes)if(n.kind==kind)return n;throw new AssertionError("missing kind");}
    private static Node text(Document d,String text){for(Node n:d.nodes)if(n.text.equals(text))return n;throw new AssertionError("missing text "+text);}
    private static Node detachEnd(Document d){Node end=kind(d,END),last=null;for(Edge e:new ArrayList<>(d.edges))if(e.to.equals(end.id)){last=d.find(e.from);d.edges.remove(e);}return last;}
    private static String snapshot(Document d){StringBuilder b=new StringBuilder(d.title);for(Node n:d.nodes)b.append(n.id).append(n.kind).append(n.text).append(n.prompt).append(n.x).append(n.y).append(n.trueLabel).append(n.falseLabel).append(n.trueColor).append(n.falseColor);for(Edge e:d.edges)b.append(e.id).append(e.from).append(e.to).append(e.branch);return b.toString();}
    private static Document solution(String id){
        Document d=FlowPractice.seed(FlowPractice.mission(id));
        if(id.equals("u05")){kind(d,DECISION).text="n > 0";return d;}
        Node last=detachEnd(d),end=kind(d,END);
        if(id.equals("u01")||id.equals("u02")){
            Node calc=add(d,PROCESS,id.equals("u01")?"totale = a + b":"secondi = minuti * 60"),out=add(d,OUTPUT,id.equals("u01")?"totale":"secondi");edge(d,last,calc,"next");edge(d,calc,out,"next");edge(d,out,end,"next");
        }else if(id.equals("u03")){
            Node choose=add(d,DECISION,"eta >= 18"),yes=add(d,OUTPUT,"\"maggiorenne\""),no=add(d,OUTPUT,"\"minorenne\"");edge(d,last,choose,"next");edge(d,choose,yes,"true");edge(d,choose,no,"false");edge(d,yes,end,"next");edge(d,no,end,"next");
        }else if(id.equals("u04")){
            Node init=add(d,PROCESS,"somma = 0\ni = 1"),test=add(d,DECISION,"i <= n"),update=add(d,PROCESS,"somma = somma + i\ni = i + 1"),out=add(d,OUTPUT,"somma");edge(d,last,init,"next");edge(d,init,test,"next");edge(d,test,update,"true");edge(d,update,test,"next");edge(d,test,out,"false");edge(d,out,end,"next");
        }else{
            Node init=add(d,PROCESS,"somma = a + b + c\nmassimo = max(a,b,c)\npositivi = 0");edge(d,last,init,"next");last=init;
            for(String variable:new String[]{"a","b","c"}){Node decision=add(d,DECISION,variable+" > 0"),inc=add(d,PROCESS,"positivi = positivi + 1"),join=add(d,PROCESS,"positivi = positivi");edge(d,last,decision,"next");edge(d,decision,inc,"true");edge(d,decision,join,"false");edge(d,inc,join,"next");last=join;}
            for(String value:new String[]{"somma","massimo","positivi"}){Node out=add(d,OUTPUT,value);edge(d,last,out,"next");last=out;}edge(d,last,end,"next");
        }return d;
    }
    public static void main(String[] args){
        check(FlowPractice.all().length==6,"six distinct laboratories");
        for(FlowLearning.Lesson lesson:FlowLearning.lessons())check(FlowPractice.forLesson(lesson)!=null,"lesson has a build task: "+lesson.id);
        for(FlowPractice.Mission m:FlowPractice.all()){
            check(m.cases()>=4,"multiple input cases "+m.id);check(!m.example().isEmpty(),"concrete example");
            Document seed=FlowPractice.seed(m);String original=snapshot(seed);FlowPractice.Result missing=FlowPractice.check(m,seed);
            check(!missing.success,"starter is not a precompleted exercise "+m.id);check(original.equals(snapshot(seed)),"failed check preserves user's starter "+m.id);
            Document solved=solution(m.id);String saved=snapshot(solved);FlowPractice.Result result=FlowPractice.check(m,solved);
            check(result.success,m.id+" real solution accepted: "+result.title+" "+result.detail);check(result.passed==m.cases(),"all cases executed "+m.id);check(saved.equals(snapshot(solved)),"successful check does not change model "+m.id);
            check(FlowPractice.check(m,solved).success,"rechecking remains deterministic "+m.id);
        }
        Document age=solution("u03");kind(age,DECISION).text="eta > 18";FlowPractice.Result threshold=FlowPractice.check(FlowPractice.mission("u03"),age);check(!threshold.success&&threshold.detail.contains("18")&&threshold.detail.contains("maggiorenne"),"wrong adult boundary reports actual failing case");
        Document loop=solution("u04");text(loop,"somma = somma + i\ni = i + 1").text="somma = somma + i";FlowPractice.Result infinite=FlowPractice.check(FlowPractice.mission("u04"),loop);check(!infinite.success&&infinite.title.contains("conclude"),"nonterminating loop is bounded and explained");
        Document fewer=solution("u01");text(fewer,"b").kind=PROCESS;text(fewer,"b").text="b = 3";check(!FlowPractice.check(FlowPractice.mission("u01"),fewer).success,"hardcoded second input rejected");
        Document disconnected=solution("u01");disconnected.edges.remove(0);FlowPractice.Result broken=FlowPractice.check(FlowPractice.mission("u01"),disconnected);check(!broken.success&&broken.nodeId!=null,"missing connection points to a block");
        Document formula=solution("u04");text(formula,"i <= n").text="false";text(formula,"somma = 0\ni = 1").text="somma = n * (n + 1) / 2\ni = 1";check(!FlowPractice.check(FlowPractice.mission("u04"),formula).success,"correct formula does not pretend the cycle mission was constructed");
        Document report=solution("u06");text(report,"a > 0").text="a >= 0";check(!FlowPractice.check(FlowPractice.mission("u06"),report).success,"report checks zero is not positive");
        check(!FlowPractice.check(null,report).success,"unknown mission is recoverable");check(!FlowPractice.check(FlowPractice.mission("u01"),null).success,"missing document is recoverable");
        FlowLearningProgress p=new FlowLearningProgress();check(p.completePractice("u01"),"first practice completion");check(!p.completePractice("u01"),"no duplicate XP");check(p.xp()==20,"practice XP reflects real completed task");check(!p.completePractice("unknown"),"unknown task earns no XP");
        p.restorePractices(new HashSet<>(Arrays.asList("u01","u02","unknown")));check(p.practices().size()==2&&p.xp()==40,"restore filters unknown progress");
        p.restore(new HashSet<>(Arrays.asList("l01")),"l01",3,-1,false,3);check(p.done("l01")&&p.practiceDone("u01")&&p.xp()==70,"old quiz progress and new construction progress coexist");
        System.out.println("PASS "+checks+" practice assertions: actual execution, failing cases, bounded loops, immutable documents and one-time progress.");
    }
}
