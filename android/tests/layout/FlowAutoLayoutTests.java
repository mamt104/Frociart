package it.frociart.app;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Locale;
import java.util.Random;
import static it.frociart.app.FlowModel.*;

/** Behavioral tests: run on a plain JVM, with no Android or third party dependency. */
public final class FlowAutoLayoutTests {
    private static int checks;
    private static void check(boolean value,String message) {
        if(!value) throw new AssertionError(message);
        checks++;
    }
    private static void equal(double expected,double actual,String message) {
        check(Math.abs(expected-actual)<.001,message+": expected "+expected+", found "+actual);
    }
    private static Node node(Document d,String id,int kind) {
        Node n=new Node(kind,"text:"+id,13,29);n.id=id;n.prompt="prompt:"+id;
        n.trueLabel="OK & sì";n.falseLabel="No, riprova";n.trueColor="#ABCDEF";n.falseColor="#654321";
        d.nodes.add(n);return n;
    }
    private static Edge edge(Document d,String from,String to,String branch) {
        Edge e=new Edge(from,to,branch);e.id="edge:"+d.edges.size();d.edges.add(e);return e;
    }
    private static Edge edge(Document d,String from,String to) {return edge(d,from,to,"next");}
    private static double cx(Node n) {return n.x+width(n)/2;}
    private static double bottom(Node n) {return n.y+height(n);}
    private static void below(Node above,Node below,String label) {
        check(below.y-bottom(above)>=FlowAutoLayout.VERTICAL_GAP-.001,label+": required vertical gap");
    }
    private static String metadata(Document d) {
        StringBuilder b=new StringBuilder().append(d.version).append('|').append(d.title);
        for(Node n:d.nodes) b.append("\nN|").append(n.id).append('|').append(n.kind).append('|').append(n.text)
            .append('|').append(n.prompt).append('|').append(n.trueLabel).append('|').append(n.falseLabel)
            .append('|').append(n.trueColor).append('|').append(n.falseColor);
        for(Edge e:d.edges) {
            b.append("\nE|");if(e==null) b.append("null");
            else b.append(e.id).append('|').append(e.from).append('|').append(e.to).append('|').append(e.branch);
        }
        return b.toString();
    }
    private static float[] coordinates(Document d) {
        float[] p=new float[d.nodes.size()*2];int i=0;
        for(Node n:d.nodes){p[i++]=n.x;p[i++]=n.y;}return p;
    }
    private static void validGeometry(Document d,String label) {
        for(Node n:d.nodes) {
            if(!Float.isFinite(n.x)||!Float.isFinite(n.y)||Math.abs(n.x)>100000||Math.abs(n.y)>100000)
                throw new AssertionError(label+": unserializable position of "+n.id+" = "+n.x+","+n.y);
        }
        check(true,label+": all coordinates finite and within model limits");
        for(int i=0;i<d.nodes.size();i++)for(int j=i+1;j<d.nodes.size();j++) {
            Node a=d.nodes.get(i),b=d.nodes.get(j);
            boolean overlap=a.x<b.x+width(b)&&a.x+width(a)>b.x&&a.y<b.y+height(b)&&a.y+height(a)>b.y;
            if(overlap)throw new AssertionError(label+": overlapping blocks "+a.id+" and "+b.id);
        }
        check(true,label+": no pair of node bounds overlaps");
    }
    private static void exercise(Document d,String label) {
        String before=metadata(d);ArrayList<Node> nodes=new ArrayList<>(d.nodes);ArrayList<Edge> edges=new ArrayList<>(d.edges);
        FlowAutoLayout.apply(d);validGeometry(d,label);
        check(before.equals(metadata(d)),label+": identifiers, contents, branch styles and edges preserved");
        boolean identities=true;
        for(int i=0;i<nodes.size();i++)identities&=nodes.get(i)==d.nodes.get(i);
        for(int i=0;i<edges.size();i++)identities&=edges.get(i)==d.edges.get(i);
        check(identities,label+": node/edge object identity and order preserved");
        float[] first=coordinates(d);
        check(!FlowAutoLayout.apply(d),label+": second apply returns false");
        check(Arrays.equals(first,coordinates(d)),label+": exact idempotence");
    }
    private static Document chain(int count) {
        Document d=new Document();for(int i=0;i<count;i++) {
            node(d,"n"+i,i==0?START:i==count-1?END:PROCESS);
            if(i>0)edge(d,"n"+(i-1),"n"+i);
        }return d;
    }
    private static void newBlockKinds() {
        for(int kind=CALL;kind<=TURTLE_PEN;kind++) {
            Document d=chain(3);Node n=d.nodes.get(1);n.kind=kind;
            n.text=kindName(kind)+" con contenuto lungo che deve restare leggibile\nseconda riga";
            exercise(d,"new block "+kindName(kind));
            below(d.nodes.get(0),n,"start before "+kindName(kind));
            below(n,d.nodes.get(2),kindName(kind)+" before end");
            FlowCodec.check(d);
        }
        Document d=new Document();node(d,"start",START);node(d,"decision",DECISION);
        edge(d,"start","decision");String previous="decision";
        for(int kind=CALL;kind<=TURTLE_PEN;kind++) {
            String id="new"+kind;node(d,id,kind);
            edge(d,previous,id,previous.equals("decision")?"true":"next");previous=id;
        }
        node(d,"end",END);edge(d,"decision","end","false");edge(d,previous,"decision");
        exercise(d,"all new blocks in loop and alternate exit");
        for(int kind=CALL+1;kind<=TURTLE_PEN;kind++)below(d.find("new"+(kind-1)),d.find("new"+kind),"new block loop sequence");
        String encoded=FlowCodec.encode(d);Document reopened=FlowCodec.decode(encoded);
        check(Arrays.equals(coordinates(d),coordinates(reopened)),"new block positions survive save/reopen");
        check(metadata(d).equals(metadata(reopened)),"new block metadata survives save/reopen");
    }
    private static void basics() {
        Document empty=new Document();check(!FlowAutoLayout.apply(empty),"empty document is a no-op");
        Document single=new Document();node(single,"only",DECISION);
        check(FlowAutoLayout.apply(single),"first single-node layout reports change");
        equal(48,single.nodes.get(0).x,"single-node left margin");equal(48,single.nodes.get(0).y,"single-node top margin");
        exercise(single,"single block");
        Document d=FlowExamples.sum();exercise(d,"sum chain");
        for(int i=1;i<d.nodes.size();i++) {
            Node a=d.nodes.get(i-1),b=d.nodes.get(i);equal(cx(a),cx(b),"chain centers aligned");
            equal(FlowAutoLayout.VERTICAL_GAP,b.y-bottom(a),"chain has default vertical gap");
        }
        Document moved=d.copy();for(Node n:moved.nodes){n.x=Float.NaN;n.y=Float.POSITIVE_INFINITY;}
        check(FlowAutoLayout.apply(moved),"invalid old coordinates are repaired");
        check(Arrays.equals(coordinates(d),coordinates(moved)),"result ignores all old coordinates");
    }
    private static void branchesAndLoops() {
        Document d=FlowExamples.parity();exercise(d,"decision and merge");
        Node fork=d.find("check"),yes=d.find("even"),no=d.find("odd"),end=d.find("end");
        check(cx(yes)<cx(fork)&&cx(no)>cx(fork),"true left / false right of decision");
        equal(yes.y,no.y,"sibling branches share their layer");
        equal(FlowAutoLayout.HORIZONTAL_GAP,no.x-(yes.x+width(yes)),"default horizontal branch gap");
        equal((cx(yes)+cx(no))/2,cx(end),"merge centered between both branches");
        below(fork,yes,"decision before true");below(fork,no,"decision before false");below(yes,end,"merge after true");below(no,end,"merge after false");
        Document shuffled=d.copy();Collections.shuffle(shuffled.edges,new Random(1729));FlowAutoLayout.apply(shuffled);
        check(Arrays.equals(coordinates(d),coordinates(shuffled)),"edge list order does not alter placement");
        Document uneven=new Document();node(uneven,"start",START);node(uneven,"if",DECISION);
        node(uneven,"long1",PROCESS);node(uneven,"long2",OUTPUT);node(uneven,"short",OUTPUT);node(uneven,"merge",END);
        edge(uneven,"start","if");edge(uneven,"if","long1","true");edge(uneven,"long1","long2");
        edge(uneven,"if","short","false");edge(uneven,"long2","merge");edge(uneven,"short","merge");
        exercise(uneven,"unequal branch lengths");below(uneven.find("long2"),uneven.find("merge"),"merge follows longest branch");
        below(uneven.find("short"),uneven.find("merge"),"short path also precedes merge");
        Document countdown=FlowExamples.countdown();String original=metadata(countdown);exercise(countdown,"countdown loop");
        below(countdown.find("check"),countdown.find("count"),"loop decision above body");
        below(countdown.find("count"),countdown.find("decrement"),"decrement follows output");
        check(cx(countdown.find("count"))<cx(countdown.find("go")),"loop body and exit have separate ordered lanes");
        check(original.equals(metadata(countdown)),"loop return edge preserved exactly");
        Document cycle=chain(500);edge(cycle,"n499","n0");exercise(cycle,"500-node cycle");
        below(cycle.nodes.get(498),cycle.nodes.get(499),"large loop descends before returning");
        Document self=new Document();node(self,"self",DECISION);edge(self,"self","self","true");exercise(self,"self loop");
        Document pair=chain(2);edge(pair,"n1","n0");exercise(pair,"two-node cycle");
        Document shared=new Document();node(shared,"fork",DECISION);node(shared,"target",END);
        edge(shared,"fork","target","true");edge(shared,"fork","target","false");
        exercise(shared,"two branches with shared target");equal(cx(shared.nodes.get(0)),cx(shared.nodes.get(1)),"common branch target aligned");
        Document report=FlowExamples.report();exercise(report,"measurement report with nested decisions and returns");
        below(report.find("read"),report.find("sentinel"),"measurement Input precedes sentinel decision");
        below(report.find("sentinel"),report.find("accumulate"),"report loop body stays below loop entry");
        below(report.find("outmax"),report.find("end"),"report merge follows output sequence");
    }
    private static double[] bounds(Document d,String prefix) {
        double[] b={Double.POSITIVE_INFINITY,Double.POSITIVE_INFINITY,Double.NEGATIVE_INFINITY,Double.NEGATIVE_INFINITY};
        for(Node n:d.nodes)if(n.id.startsWith(prefix)) {
            b[0]=Math.min(b[0],n.x);b[1]=Math.min(b[1],n.y);b[2]=Math.max(b[2],n.x+width(n));b[3]=Math.max(b[3],bottom(n));
        }return b;
    }
    private static void disconnectedAndCapacity() {
        Document d=new Document();for(int c=0;c<4;c++)for(int i=0;i<c+2;i++) {
            String id="c"+c+"_"+i;node(d,id,(i+c)%6);if(i>0)edge(d,"c"+c+"_"+(i-1),id);
        }
        exercise(d,"four disconnected components");
        for(int a=0;a<4;a++)for(int b=a+1;b<4;b++) {
            double[] p=bounds(d,"c"+a+"_"),q=bounds(d,"c"+b+"_");
            double g=FlowAutoLayout.COMPONENT_GAP-.001;
            check(p[2]+g<=q[0]||q[2]+g<=p[0]||p[3]+g<=q[1]||q[3]+g<=p[1],"component bounds retain component gap");
        }
        Document chain=chain(500);exercise(chain,"500-node chain");
        for(int i=1;i<500;i++)if(chain.nodes.get(i).y-bottom(chain.nodes.get(i-1))<FlowAutoLayout.VERTICAL_GAP-.001)
            throw new AssertionError("500-node chain lost vertical spacing at "+i);
        check(true,"500-node chain preserves vertical gap throughout");
        Document star=new Document();node(star,"root",START);
        for(int i=1;i<500;i++){node(star,"leaf"+i,i%6);edge(star,"root","leaf"+i,i%2==0?"true":"false");}
        exercise(star,"500-node wide fork");check(star.nodes.get(1).x<0,"wide component centered to fit portable coordinate limits");
        Document isolated=new Document();for(int i=0;i<500;i++)node(isolated,"island"+i,i%6);exercise(isolated,"500 isolated blocks");
        Document dense=dense(500,1500,730201);exercise(dense,"500 nodes and 1500 mixed edges");
        Document unresolved=chain(3);edge(unresolved,"missing","n0");edge(unresolved,"n2","missing");unresolved.edges.add(null);
        exercise(unresolved,"unresolved endpoints retained for validation");
    }
    private static Document dense(int count,int edges,long seed) {
        Document d=new Document();Random random=new Random(seed);
        for(int i=0;i<count;i++)node(d,"n"+i,i%6);
        for(int i=0;i<edges;i++)edge(d,"n"+random.nextInt(count),"n"+random.nextInt(count),i%3==0?"true":i%3==1?"false":"next");
        return d;
    }
    private static void rejectsWithoutMutation(Document d,String label) {
        float[] before=coordinates(d);boolean rejected=false;
        try{FlowAutoLayout.apply(d);}catch(IllegalArgumentException expected){rejected=true;}
        check(rejected,label+": rejected");check(Arrays.equals(before,coordinates(d)),label+": no position was changed");
    }
    private static void invalidInputs() {
        boolean rejected=false;try{FlowAutoLayout.apply(null);}catch(IllegalArgumentException expected){rejected=true;}
        check(rejected,"null document rejected");
        Document duplicate=chain(3);duplicate.nodes.get(2).id=duplicate.nodes.get(0).id;rejectsWithoutMutation(duplicate,"duplicate ID");
        Document missing=chain(3);missing.nodes.get(1).id=null;rejectsWithoutMutation(missing,"missing ID");
        Document kind=chain(3);kind.nodes.get(2).kind=19;rejectsWithoutMutation(kind,"unknown node kind");
        rejectsWithoutMutation(chain(501),"501 nodes");
        Document edges=chain(1);for(int i=0;i<1501;i++)edge(edges,"n0","n0");rejectsWithoutMutation(edges,"1501 edges");
    }
    private static void benchmarks() {
        for(String name:new String[]{"chain500","dense500_1500","isolated500"}) {
            Document d=name.equals("chain500")?chain(500):name.equals("dense500_1500")?dense(500,1500,730201):new Document();
            if(name.equals("isolated500"))for(int i=0;i<500;i++)node(d,"n"+i,i%6);
            for(int i=0;i<8;i++)FlowAutoLayout.apply(d);
            double[] ms=new double[25];for(int i=0;i<ms.length;i++) {
                long started=System.nanoTime();FlowAutoLayout.apply(d);ms[i]=(System.nanoTime()-started)/1000000.0;
            }
            Arrays.sort(ms);System.out.printf(Locale.ROOT,"METRIC %s median_ms=%.3f p95_ms=%.3f samples=%d%n",name,ms[ms.length/2],ms[(int)Math.ceil(ms.length*.95)-1],ms.length);
        }
    }
    public static void main(String[] args) {
        newBlockKinds();basics();branchesAndLoops();disconnectedAndCapacity();invalidInputs();
        for(int i=0;i<12;i++){int n=new int[]{20,80,200,500}[i%4];exercise(dense(n,Math.min(1500,n*3),911L+i*1009),"seeded mixed graph "+i);}
        benchmarks();System.out.println("LAYOUT CHECKS PASSED: "+checks);
    }
}
