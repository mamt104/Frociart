package it.frociart.app;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import static it.frociart.app.FlowModel.*;

/** Host-side integration and adversarial tests. Run with Java 17, without an Android runtime. */
public final class EngineTests {
    static int checks;
    static void check(boolean ok,String message){checks++;if(!ok)throw new AssertionError(message);}
    static void eq(Object expected,Object actual,String message){check(expected==null?actual==null:expected.equals(actual),message+" | expected="+expected+" actual="+actual);}
    interface Action{void run()throws Exception;}
    static void rejects(Action action,String message){boolean failed=false;try{action.run();}catch(IllegalArgumentException ex){failed=true;}catch(Exception ex){throw new RuntimeException(ex);}check(failed,message);}
    static long errors(Document d){long n=0;for(FlowEngine.Issue i:FlowEngine.validate(d))if(i.error)n++;return n;}
    static boolean issue(Document d,String code,String node){for(FlowEngine.Issue i:FlowEngine.validate(d))if(i.code.equals(code)&&(node==null||node.equals(i.nodeId)))return true;return false;}
    static Node node(Document d,String id,int kind,String text){Node n=new Node(kind,text,300,100+d.nodes.size()*140);n.id=id;d.nodes.add(n);return n;}
    static void edge(Document d,String a,String b,String branch){Edge e=new Edge(a,b,branch);e.id=a+"_"+branch+"_"+b;d.edges.add(e);}
    static void edge(Document d,String a,String b){edge(d,a,b,"next");}
    static Document line(int middleKind,String text){Document d=new Document();node(d,"start",START,"Inizio");node(d,"middle",middleKind,text);node(d,"end",END,"Fine");edge(d,"start","middle");edge(d,"middle","end");return d;}
    static FlowEngine.Runner run(Document d,String... inputs){FlowEngine.Runner r=new FlowEngine.Runner(d);int input=0,guard=0;while(r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR&&guard++<10020){if(r.state==FlowEngine.State.WAITING){if(input>=inputs.length)break;r.submitInput(inputs[input++]);}else r.step();}return r;}
    static void expression(String e,String result){eq(result,FlowEngine.evaluate(e,null).format(),"expression "+e);}
    static String repeated(String text,int count){StringBuilder s=new StringBuilder();for(int i=0;i<count;i++)s.append(text);return s.toString();}
    public static void main(String[] args)throws Exception {
        // Stable desktop language, precedence, input types, Unicode and short-circuit semantics.
        expression("2 + 3 * 4","14");expression("(2 + 3) * 4","20");expression("2 ^ 3 ^ 2","512");expression("-2^2","-4");expression("2^-2","0.25");expression("(-2)^2","4");
        expression("0.1 + 0.2","0.3");expression(".5 + 1.5","2");expression("1e3 + 2E-1","1000.2");expression("-5 % 2","-1");expression("5 / 2","2.5");expression("-0 = 0","vero");expression("-0 >= 0","vero");
        expression("true e non falso","vero");expression("0 o 2","vero");expression("vero && !falso","vero");expression("false and missing","falso");expression("true or (1 / 0)","vero");expression("false && sqrt(-1)","falso");
        expression("3 <> 4","vero");expression("3 == 3","vero");expression("3 != 3","falso");expression("3 <= 3","vero");expression("'a' < 'b'","vero");expression("'3' = 3","falso");expression("true = 1","falso");
        expression("\"Somma: \" + 7","Somma: 7");expression("7 + ' amici'","7 amici");expression("'vero: ' + true","vero: vero");expression("'a\\nb\\t\\\\'","a\nb\t\\");expression("\"è 🌙\"","è 🌙");
        expression("abs(-3)","3");expression("sqrt(9)","3");expression("floor(-2.1)","-3");expression("round(-2.5)","-3");expression("round(2.5)","3");expression("round(1.235,2)","1.24");expression("min(9,2,-5)","-5");expression("MAX(9,2,-5)","9");expression("len('è🌙')","2"); // len counts Unicode code points, consistently with char; surrogate pairs no longer count twice.
        expression("round(1.005,2)","1");expression("round(-1.005,2)","-1");expression("round(9999999999999998,0)","1E+16");
        rejects(()->FlowEngine.evaluate("sqrt(-1)",null),"negative sqrt");rejects(()->FlowEngine.evaluate("1 / 0",null),"division by zero");rejects(()->FlowEngine.evaluate("1 % 0",null),"modulo by zero");rejects(()->FlowEngine.evaluate("10 ^ 10000",null),"power overflow");rejects(()->FlowEngine.evaluate("1e999",null),"literal overflow");
        rejects(()->FlowEngine.evaluate("sqrt(1,2)",null),"wrong arity");rejects(()->FlowEngine.evaluate("min(1)",null),"min arity");rejects(()->FlowEngine.evaluate("round(1,16)",null),"round digit range");rejects(()->FlowEngine.evaluate("round(1,.5)",null),"round fractional digits");rejects(()->FlowEngine.evaluate("len(1)",null),"len type");
        rejects(()->FlowEngine.evaluate("'a' - 1",null),"text arithmetic type");rejects(()->FlowEngine.evaluate("non 'a'",null),"text boolean type");rejects(()->FlowEngine.evaluate("'hello",null),"unclosed string");rejects(()->FlowEngine.evaluate("'\\u1234'",null),"unsupported language escape");rejects(()->FlowEngine.evaluate("1e+",null),"incomplete exponent");
        rejects(()->FlowEngine.evaluate("Runtime.getRuntime().exec('ls')",null),"no Java execution");rejects(()->FlowEngine.evaluate("eval('1')",null),"no arbitrary evaluator");rejects(()->FlowEngine.evaluate("new File('x')",null),"no file access");rejects(()->FlowEngine.evaluate("a[0]",null),"no unimplemented arrays");
        rejects(()->FlowEngine.evaluate(repeated("(",81)+"1"+repeated(")",81),null),"bounded nesting");rejects(()->FlowEngine.evaluate(repeated("1+",256)+"1",null),"bounded AST");rejects(()->FlowEngine.evaluate(repeated("x",65),null),"bounded variable identifier");
        eq("1.25",FlowEngine.parseInput(" 1,25 ").format(),"Italian decimal input");eq("-250",FlowEngine.parseInput(" -2,5e2 ").format(),"Italian decimal exponent");eq("vero",FlowEngine.parseInput(" VERO ").format(),"Italian boolean input");eq("falso",FlowEngine.parseInput(" False ").format(),"English boolean input");eq("  testo  ",FlowEngine.parseInput("  testo  ").format(),"literal input preserves spaces");eq("1+2",FlowEngine.parseInput("1+2").format(),"input is not evaluated");eq("1.000,3",FlowEngine.parseInput("1.000,3").format(),"thousands not misread");
        rejects(()->FlowEngine.parseInput("NaN"),"NaN input");rejects(()->FlowEngine.parseInput("-Infinity"),"infinite input");rejects(()->FlowEngine.parseInput("1e999"),"overflow input");rejects(()->FlowEngine.parseInput(repeated("x",32769)),"bounded input string");
        LinkedHashMap<String,FlowEngine.Value> vars=new LinkedHashMap<>();vars.put("Numero",FlowEngine.Value.number(8));expression("true","vero");eq("9",FlowEngine.evaluate("NUMERO + 1",vars).format(),"case-insensitive expression lookup");

        // Complete executable samples, including loop and each decision branch.
        for(Document d:FlowExamples.all())if(!d.title.startsWith("Esercizio"))eq(0L,errors(d),"valid example "+d.title);
        FlowEngine.Runner sum=run(FlowExamples.sum(),"3,5","4.25");eq(FlowEngine.State.COMPLETED,sum.state,"sum completed");eq("Somma: 7.75",sum.output.get(0),"sum result");eq(6,sum.steps,"input counted once");
        eq("Pari",run(FlowExamples.parity(),"8").output.get(0),"even branch");eq("Dispari",run(FlowExamples.parity(),"9").output.get(0),"odd branch");eq("Pari",run(FlowExamples.parity(),"-4").output.get(0),"negative parity");
        FlowEngine.Runner count=run(FlowExamples.countdown(),"3");eq(FlowEngine.State.COMPLETED,count.state,"countdown completed");eq("[3, 2, 1, Via!]",count.output.toString(),"countdown sequence");check(issue(FlowExamples.countdown(),"loop",null),"loop diagnostic is present");
        eq("Maggiore: 8",run(FlowExamples.maximum(),"8","3").output.get(0),"maximum first");eq("Maggiore: 8",run(FlowExamples.maximum(),"3","8").output.get(0),"maximum second");eq("Maggiore: 3",run(FlowExamples.maximum(),"3","3").output.get(0),"maximum equal");
        for(String age:new String[]{"0","1","17","17.9","18","19","100"}){
            Document ageExample=FlowExamples.age();String encoded=FlowCodec.encode(ageExample);
            FlowEngine.Runner ageRun=run(ageExample,age);
            eq(FlowEngine.State.COMPLETED,ageRun.state,"age example completes: "+age);
            eq(Double.parseDouble(age)>=18?"Maggiorenne":"Minorenne",ageRun.output.get(0),"age boundary: "+age);
            eq(encoded,FlowCodec.encode(ageExample),"running a preset never mutates its diagram");
        }
        check(issue(FlowExamples.missingBranch(),"decision-branches","check"),"missing branch detected");check(issue(FlowExamples.missingBranch(),"unreachable","odd"),"unreachable branch detected");eq(FlowEngine.State.ERROR,new FlowEngine.Runner(FlowExamples.missingBranch()).state,"invalid graph runner is safe");
        Document custom=FlowExamples.parity();custom.find("check").trueLabel="Divisibile";custom.find("check").falseLabel="Resto";custom.find("check").trueColor="#123AbC";custom.find("check").falseColor="#CDEF12";
        FlowEngine.Runner labeled=run(custom,"4");eq("Pari",labeled.output.get(0),"labels do not change branching");
        FlowEngine.Runner paused=new FlowEngine.Runner(custom);paused.step();check(!paused.step(),"step input waits");eq(1,paused.steps,"waiting does not count as executed");eq(FlowEngine.State.WAITING,paused.state,"waiting state");check(!paused.submitInput("Infinity"),"invalid input retry");eq(FlowEngine.State.WAITING,paused.state,"invalid input remains waiting");eq(0,paused.variables.size(),"invalid input writes no variable");check(paused.errorMessage!=null,"invalid input explains why");check(paused.submitInput("6"),"valid retry accepted");eq(null,paused.errorMessage,"successful retry clears inline error");eq(2,paused.steps,"retry counts once");paused.step();check(paused.lastMessage.contains("Divisibile"),"runtime shows custom label");
        paused.reset();eq(FlowEngine.State.READY,paused.state,"reset state");eq(0,paused.steps,"reset steps");eq(0,paused.variables.size(),"reset variables");eq(0,paused.output.size(),"reset output");eq(null,paused.inputVariable,"reset pending input");
        FlowEngine.Runner snapshot=new FlowEngine.Runner(FlowExamples.parity());Document mutable=FlowExamples.sum();FlowEngine.Runner isolated=new FlowEngine.Runner(mutable);mutable.nodes.clear();mutable.edges.clear();isolated.step();eq(FlowEngine.State.RUNNING,isolated.state,"runner owns isolated model");

        // Per-block atomicity and definite assignment on all predecessors, including loops.
        Document rollback=line(PROCESS,"a = 1; b = 2; broken = 1/0");FlowEngine.Runner failed=run(rollback);eq(FlowEngine.State.ERROR,failed.state,"process failure detected");eq(0,failed.variables.size(),"all writes in failed block rolled back");
        Document caseDoc=line(PROCESS,"Counter = 1; counter = COUNTER + 2; text = 'a;b'; other = text + '\\n'");FlowEngine.Runner cased=run(caseDoc);eq(FlowEngine.State.COMPLETED,cased.state,"multi-line assignments and string separators");eq("3",cased.variables.get("COUNTER").format(),"case-insensitive assignments");check(cased.variables.containsKey("counter"),"case-insensitive containsKey");eq(3,cased.variables.size(),"case duplicate shares variable");eq("a;b\n",cased.variables.get("other").format(),"escaped newline is not statement break");
        check(issue(line(PROCESS,"x = x + 1"),"uninitialized","middle"),"self reference before assignment");check(issue(line(OUTPUT,"unknown"),"uninitialized","middle"),"unknown output variable");eq(0L,errors(line(OUTPUT,"false and unknown")),"literal short circuit ignores impossible read");
        Document branches=FlowExamples.parity();branches.find("even").kind=PROCESS;branches.find("even").text="x = 1";branches.find("odd").kind=PROCESS;branches.find("odd").text="y = 2";node(branches,"result",OUTPUT,"x");for(Edge e:branches.edges)if(e.to.equals("end"))e.to="result";edge(branches,"result","end");check(issue(branches,"uninitialized","result"),"definition on one branch rejected");branches.find("odd").text="x = 2";eq(0L,errors(branches),"definition on all branches accepted");
        Document entryLoop=FlowExamples.countdown();entryLoop.find("input").kind=PROCESS;entryLoop.find("input").text="seed = 1";check(issue(entryLoop,"uninitialized","check"),"assignment later in loop does not satisfy first iteration");
        Document endless=FlowExamples.countdown();endless.find("decrement").text="n = n + 1";FlowEngine.Runner infinite=run(endless,"1");eq(FlowEngine.State.ERROR,infinite.state,"infinite loop reaches execution budget");eq(10000,infinite.steps,"exact step limit");check(infinite.errorMessage.contains("10000"),"step limit explained");
        Document noExit=FlowExamples.countdown();noExit.edges.removeIf(e->e.from.equals("check")&&e.branch.equals("false"));edge(noExit,"check","count","false");check(issue(noExit,"no-end-path","start"),"non-terminating graph lacks end path");
        Document doubleStart=FlowExamples.blank();node(doubleStart,"other_start",START,"Inizio");check(issue(doubleStart,"start-count",null),"multiple starts");
        Document intoStart=FlowExamples.blank();edge(intoStart,"end","start");check(issue(intoStart,"start-incoming","start"),"start incoming");check(issue(intoStart,"end-outgoing","end"),"end outgoing");
        Document manyStatements=line(PROCESS,repeated("x = 1;",129));check(issue(manyStatements,"syntax","middle"),"bounded statements");
        Document textGrowth=line(PROCESS,"x = 'a'; "+repeated("x = x + x;",16));FlowEngine.Runner hugeText=run(textGrowth);eq(FlowEngine.State.ERROR,hugeText.state,"text growth bounded");eq(0,hugeText.variables.size(),"text overflow block remains atomic");
        Document outputCap=FlowExamples.countdown();outputCap.find("input").kind=PROCESS;outputCap.find("input").text="n = 100; t = 'a'; "+repeated("t=t+t;",14);outputCap.find("count").text="t";FlowEngine.Runner capped=run(outputCap);eq(FlowEngine.State.ERROR,capped.state,"output cap reached");eq(16,capped.output.size(),"output cap keeps only committed lines");check(capped.errorMessage.contains("256 KiB"),"output cap explanation");

        // Portable project format: current files, desktop exports and adversarial JSON.
        String encoded=FlowCodec.encode(custom);Document decoded=FlowCodec.decode(encoded);eq(custom.title,decoded.title,"JSON title roundtrip");eq("Divisibile",decoded.find("check").trueLabel,"custom label roundtrip");eq("#123AbC",decoded.find("check").trueColor,"custom color roundtrip");eq(encoded,FlowCodec.encode(decoded),"canonical JSON stable");
        Document unicode=line(OUTPUT,"\"Caffè 🌙\\n\\t\"");unicode.title="Test \"unicode\" \\ 🌙";eq(unicode.title,FlowCodec.decode(FlowCodec.encode(unicode)).title,"JSON escapes Unicode roundtrip");eq("Caffè 🌙\n\t",run(FlowCodec.decode(FlowCodec.encode(unicode))).output.get(0),"nested JSON/language escapes");
        eq(custom.title,FlowCodec.decode("\ufeff"+encoded).title,"UTF8 BOM accepted");
        rejects(()->FlowCodec.decode(encoded+"garbage"),"trailing garbage");rejects(()->FlowCodec.decode(encoded.replace("\"Version\":1","\"Version\":1,\"Version\":1")),"duplicate JSON fields");rejects(()->FlowCodec.decode(encoded.replace("\"Version\":1","\"Version\":99")),"unknown format version");rejects(()->FlowCodec.decode(encoded.replace("\"Version\":1","\"Version\":1.5")),"integer version required");rejects(()->FlowCodec.decode(encoded.replace("\"Version\":1","\"Version\":01")),"leading zero disallowed");
        rejects(()->FlowCodec.decode("{\"Version\":1,\"Nodes\":[],\"Edges\":null}"),"null edge list");rejects(()->FlowCodec.decode("[]"),"root is object");rejects(()->FlowCodec.decode("{\"Version\":1e999}"),"JSON infinity");rejects(()->FlowCodec.decode(repeated("[",40)+"0"+repeated("]",40)),"bounded JSON nesting");rejects(()->FlowCodec.decode(repeated("x",MAX_BYTES+1)),"bounded file size");
        Document duplicate=FlowExamples.parity();duplicate.nodes.add(duplicate.nodes.get(0).copy());rejects(()->FlowCodec.encode(duplicate),"duplicate node IDs");Document dangling=FlowExamples.parity();dangling.edges.get(0).to="not-there";rejects(()->FlowCodec.encode(dangling),"dangling edge");Document wrongKind=FlowExamples.parity();wrongKind.nodes.get(0).kind=99;rejects(()->FlowCodec.encode(wrongKind),"unknown node kind");
        Document wrongBranch=FlowExamples.parity();wrongBranch.edges.get(0).branch="yes";rejects(()->FlowCodec.encode(wrongBranch),"unknown branch");Document badColor=custom.copy();badColor.find("check").trueColor="red";rejects(()->FlowCodec.encode(badColor),"invalid custom color");Document badLabel=custom.copy();badLabel.find("check").trueLabel=repeated("x",61);rejects(()->FlowCodec.encode(badLabel),"bounded custom label");
        Document badCoordinate=custom.copy();badCoordinate.nodes.get(0).x=Float.NaN;rejects(()->FlowCodec.encode(badCoordinate),"NaN coordinate");Document largeCoordinate=custom.copy();largeCoordinate.nodes.get(0).y=100001;rejects(()->FlowCodec.encode(largeCoordinate),"large coordinate");Document tooMany=FlowExamples.blank();for(int i=0;i<500;i++)node(tooMany,"many"+i,END,"Fine");rejects(()->FlowCodec.encode(tooMany),"node count bounded");
        eq(FlowEngine.State.ERROR,new FlowEngine.Runner(null).state,"null document safe runner");check(issue(null,"document",null),"null document validation");
        if(args.length>0&&!args[0].equals("--no-desktop-fixtures")){Path dir=Paths.get(args[0]);String[] files={"01_Somma.frociart","02_Pari_o_dispari.frociart","03_Conto_alla_rovescia.frociart","04_Maggiore.frociart","05_Trova_l_errore.frociart"};for(int i=0;i<files.length;i++){Document desktop=FlowCodec.decode(Files.readString(dir.resolve(files[i]),StandardCharsets.UTF_8));check(!desktop.nodes.isEmpty(),"desktop file decoded "+files[i]);eq("Sì",desktop.nodes.get(0).trueLabel,"legacy branch label default "+files[i]);if(i<4)eq(0L,errors(desktop),"desktop sample validates "+files[i]);}Document desktopParity=FlowCodec.decode(Files.readString(dir.resolve(files[1]),StandardCharsets.UTF_8));eq("Pari",run(desktopParity,"100").output.get(0),"real desktop project executed");}
        Path output=Paths.get(args.length>1?args[1]:"/tmp/frociart-android-engine-test-output");Files.createDirectories(output);Files.writeString(output.resolve("android-custom.frociart"),encoded,StandardCharsets.UTF_8);Files.writeString(output.resolve("android-unicode.frociart"),FlowCodec.encode(unicode),StandardCharsets.UTF_8);
        System.out.println("ENGINE TESTS PASSED: "+checks+" checks");
    }
}

