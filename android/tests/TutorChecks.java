package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Runs the real recovered interpreter, rather than duplicating its language in a test mock. */
public final class TutorChecks {
    static int checks;static void ok(boolean b,String message){checks++;if(!b)throw new AssertionError(message);}
    static FlowTutor.Lesson lesson(int n){return FlowTutorContent.lesson(String.format(java.util.Locale.ROOT,"b%02d",n));}
    static Node node(FlowTutor.Lesson l,Document d,int kind){for(Node n:d.nodes)if(n.kind==kind)return n;throw new AssertionError("kind");}
    static void error(int kind,String code,String expected){Node n=new Node(kind,code,0,0);FlowTutor.Feedback f=FlowTutorDiagnostics.before(n);ok(f!=null&&!f.pass&&f.code.equals(expected),"diagnostic "+code+": "+(f==null?null:f.code));}
    static void caseFailure(int number,String target,String bad,String expectedCode){FlowTutor.Lesson l=lesson(number);Document d=l.solution();d.find(target).text=bad;String original=FlowCodec.encode(d);FlowTutor.Feedback f=FlowTutor.check(l,d);ok(!f.pass,"wrong solution rejected: "+number+" "+bad);if(expectedCode!=null)ok(f.code.equals(expectedCode),"error code "+expectedCode+" vs "+f.code);ok(original.equals(FlowCodec.encode(d)),"failure preserves document");}
    static void runtimeFailure(String expression,String expectedCode){
        Document d=lesson(2).solution();d.find("messaggio").text=expression;String before=FlowCodec.encode(d);
        FlowTutor.Feedback f=FlowTutor.check(lesson(2),d);
        ok(!f.pass&&f.code.equals(expectedCode),"real engine error "+expression+": "+f.code);
        ok(f.nodeId.equals("messaggio")&&!f.fix.isEmpty()&&!f.why.isEmpty(),"error points to block and teaches recovery");
        ok(before.equals(FlowCodec.encode(d)),"runtime failure preserves text");
    }
    static void structural(Document d,String code){
        String before=FlowCodec.encode(d);boolean found=false;
        for(FlowEngine.Issue issue:FlowEngine.validate(d))if(issue.code.equals(code)){
            FlowTutor.Feedback f=FlowTutorDiagnostics.issue(issue);found=true;
            ok(!f.pass&&!f.why.isEmpty()&&!f.fix.isEmpty(),"structure has cause and recovery: "+code);
            ok(Objects.equals(f.nodeId,issue.nodeId),"structure identifies actual block: "+code);break;
        }
        ok(found,"actual engine found structure issue "+code);ok(before.equals(FlowCodec.encode(d)),"structure diagnosis preserves graph");
    }
    static void preset(FlowTutorGuide.Preset p,String initialize,String output,String[] inputs,String[] expected){
        Document d=new Document();Node start=new Node(START,"Inizio",0,0),block=new Node(p.kind,p.text,0,100),end=new Node(END,"Fine",0,300);
        d.nodes.add(start);Node previous=start;
        if(!initialize.isEmpty()){Node init=new Node(PROCESS,initialize,0,50);d.nodes.add(init);d.edges.add(new Edge(previous.id,init.id,"next"));previous=init;}
        d.nodes.add(block);d.edges.add(new Edge(previous.id,block.id,"next"));
        if(p.kind==DECISION){
            Node yes=new Node(OUTPUT,"\"si\"",0,200),no=new Node(OUTPUT,"\"no\"",300,200);d.nodes.add(yes);d.nodes.add(no);
            d.edges.add(new Edge(block.id,yes.id,"true"));d.edges.add(new Edge(block.id,no.id,"false"));d.edges.add(new Edge(yes.id,end.id,"next"));d.edges.add(new Edge(no.id,end.id,"next"));
        }else if(p.kind!=END){
            previous=block;
            if(!output.isEmpty()){Node out=new Node(OUTPUT,output,0,200);d.nodes.add(out);d.edges.add(new Edge(block.id,out.id,"next"));previous=out;}
            d.edges.add(new Edge(previous.id,end.id,"next"));
        }
        if(p.kind!=END)d.nodes.add(end);
        FlowTutor.Feedback result=FlowTutor.execute(d,new FlowTutor.Trial(inputs,expected),0,1);
        ok(result.pass,"actual preset execution "+p.id+": "+result.why);
    }
    public static void main(String[] args){
        Set<String> taught=new HashSet<>(),ids=new HashSet<>();int trials=0,steps=0;
        ok(FlowTutorContent.all().length==24,"24 beginner lessons");
        for(FlowTutor.Lesson l:FlowTutorContent.all()){
            ok(ids.add(l.id),"unique stable ID");for(String t:l.introduces){ok(FlowTutorGuide.term(t)!=null,"introduced term has a guide: "+t);taught.add(t);}
            for(String t:l.terms)ok(taught.contains(t),"no assumed prerequisite before "+l.id+": "+t);
            for(FlowTutor.Part p:l.parts){Node n=new Node(p.kind,p.answer,0,0);for(FlowTutorGuide.Term t:FlowTutorGuide.inspect(n).symbols)ok(taught.contains(t.id),"syntax explained before use: "+l.id+" "+p.answer+" "+t.id);}
            Document solved=l.solution();String before=FlowCodec.encode(solved);FlowTutor.Feedback complete=FlowTutor.check(l,solved);
            ok(complete.pass,l.id+" reference runs: "+complete.title+" "+complete.why);ok(complete.passed==l.trials.length,"all cases really run");ok(before.equals(FlowCodec.encode(solved)),"checker does not mutate reference");trials+=l.trials.length;
            Document learner=l.seed();ok(!FlowTutor.check(l,learner).pass,"starter is incomplete "+l.id);
            for(int i=0;i<l.steps.length;i++){
                FlowTutor.Step s=l.steps[i];FlowTutor.Part p=l.part(s.node);
                if(s.action==FlowTutor.ADD||s.action==FlowTutor.WRITE||s.action==FlowTutor.CONNECT)ok(FlowTutor.actionRequired(l,i,learner),"primary action opens the actual unfinished task");
                if(s.action==FlowTutor.ADD){ok(!FlowTutor.checkStep(l,i,learner,"").pass,"must add actual block first");int count=learner.nodes.size();ok(FlowTutor.insert(l,learner,s.node,p.kind),"add succeeds");ok(learner.nodes.size()==count+1,"real model node added");}
                else if(s.action==FlowTutor.WRITE){learner.find(s.node).text=p.answer;}
                else if(s.action==FlowTutor.CONNECT){FlowTutor.connect(learner,s.from,s.branch,s.to);}
                String snapshot=FlowCodec.encode(learner);FlowTutor.Feedback feedback=FlowTutor.checkStep(l,i,learner,s.action==FlowTutor.VALUE?s.hint:"");
                ok(feedback.pass,"guided user journey "+l.id+" step "+i+": "+feedback.title+" "+feedback.why);ok(snapshot.equals(FlowCodec.encode(learner)),"step check does not fill future answers");steps++;
                ok(!FlowTutor.actionRequired(l,i,learner),"after the task, primary action offers verification");
            }
            ok(FlowTutor.check(l,learner).pass,"constructed user diagram completes "+l.id);
            ok(!l.explanation.isEmpty()&&!l.reading.isEmpty()&&!l.warning.isEmpty(),"explanation, reading and common error present");
        }
        caseFailure(2,"messaggio","Ciao","uninitialized");caseFailure(3,"risultato","\"5\"","wrong-result");
        caseFailure(5,"memoria","punti == 5","compare-in-process");caseFailure(6,"eta","eta = 21","input-assignment");
        caseFailure(7,"saluto","\"nome\"","learning-goal");caseFailure(8,"aggiorna","punti = 1","wrong-result");
        caseFailure(9,"calcola","resto = prezzo - pagato","wrong-result");caseFailure(10,"calcola","totale = prezzo + quantita","wrong-result");
        caseFailure(11,"calcola","meta = numero / 0","divide-zero");caseFailure(12,"calcola","media = a + b / 2","wrong-result");
        caseFailure(13,"domanda","eta > 18","wrong-result");caseFailure(14,"domanda","numero != 0","wrong-result");
        caseFailure(15,"domanda","!0","wrong-result");caseFailure(16,"domanda","eta >= 18 or eta <= 65","wrong-result");
        caseFailure(17,"domanda","giorno == 6 or 7","wrong-result");caseFailure(18,"domanda","not (numero != 0)","wrong-result");
        caseFailure(19,"domanda","denominatore == 0","wrong-result");caseFailure(20,"domanda","i <= n","wrong-result");
        caseFailure(21,"accumula","somma = i","wrong-result");caseFailure(22,"domanda","numero / 2 == 0","wrong-result");
        caseFailure(23,"scegli","massimo = max(a)","function-args");
        error(INPUT,"7nome","input-identifier");error(INPUT,"primo nome","input-identifier");error(INPUT,"e","input-identifier");
        error(PROCESS,"x += 1","compound-assignment");error(PROCESS,"x++","increment");error(PROCESS,"x = 2 ** 3","power-symbol");
        error(PROCESS,"x = 2 × 3","unicode-operator");error(DECISION,"eta ≥ 18","unicode-operator");
        error(OUTPUT,"“Ciao”","smart-quotes");error(OUTPUT,"2 + (3","parentheses-open");error(OUTPUT,"2 + 3)","parentheses-close");
        error(PROCESS,"x = 1,5","decimal-comma");error(OUTPUT,"print(3)","language-command");
        ok(FlowTutorDiagnostics.before(new Node(OUTPUT,"\"2 × 3 e ≥\"",0,0))==null,"symbols inside text are not code errors");
        ok(FlowTutorDiagnostics.before(new Node(OUTPUT,"max(2,3)",0,0))==null,"function comma is not mistaken for decimal");
        ok(FlowTutor.literal("!0").format().equals("vero"),"unary !0 meaning verified in actual engine");
        ok(FlowTutor.literal("4 != 0").format().equals("vero")&&FlowTutor.literal("0 != 0").format().equals("falso"),"inequality verified independently");
        ok(FlowTutor.literal("not (0 == 0)").format().equals("falso"),"explicit not grouping");
        ok(FlowTutor.literal("0.1 + 0.2 == 0.3").format().equals("falso"),"floating point caveat actually holds");
        FlowTutor.Lesson input=lesson(6);Document alternate=input.solution();alternate.find("eta").text="ETA";ok(FlowTutor.checkStep(input,1,alternate,"").pass,"case-insensitive valid input accepted");
        Document commutative=lesson(10).solution();commutative.find("calcola").text="totale = quantita * prezzo";ok(FlowTutor.check(lesson(10),commutative).pass,"equivalent formula accepted");
        Document alias=lesson(15).solution();alias.find("domanda").text="numero <> 0";ok(FlowTutor.check(lesson(15),alias).pass,"equivalent inequality accepted");
        Document equals=lesson(14).solution();equals.find("domanda").text="numero = 0";ok(FlowTutor.check(lesson(14),equals).pass,"single equals in Decision is valid language");
        Document cycle=lesson(20).solution();cycle.find("aumenta").text="i = i";ok(FlowTutor.check(lesson(20),cycle).code.equals("loop-limit"),"non-progressing loop stops with an explanation");
        Document threshold=lesson(13).solution();threshold.find("domanda").text="eta > 18";ok("domanda".equals(FlowTutor.check(lesson(13),threshold).nodeId),"logical threshold points to actual Decision");
        Document reversed=lesson(9).solution();reversed.find("calcola").text="resto = prezzo - pagato";ok("calcola".equals(FlowTutor.check(lesson(9),reversed).nodeId),"first differing state points to the calculation, not a later Output");
        Document textNumber=lesson(3).solution();textNumber.find("risultato").text="\"5\"";ok("risultato".equals(FlowTutor.check(lesson(3),textNumber).nodeId),"type mismatch points to its Output");
        ok(FlowTutor.check(lesson(13),threshold).fix.contains("18 è escluso"),"boundary correction explains the excluded value");
        Document bang=lesson(15).solution();bang.find("domanda").text="!0";ok(FlowTutor.check(lesson(15),bang).fix.contains("produce sempre vero"),"!0 correction distinguishes constant negation from comparison");
        Document orphan=lesson(17).solution();orphan.find("domanda").text="giorno == 6 or 7";ok(FlowTutor.check(lesson(17),orphan).fix.contains("seconda domanda"),"or correction explains the missing second comparison");
        runtimeFailure("5 / 0","divide-zero");runtimeFailure("5 % 0","mod-zero");
        runtimeFailure("sqrt(-1)","sqrt-domain");runtimeFailure("len(5)","type");runtimeFailure("\"5\" * 2","type");
        runtimeFailure("round(2.3, -1)","round-digits");runtimeFailure("max(2)","function-args");
        runtimeFailure("inventata(2)","function-name");runtimeFailure("\"Ciao","quotes");
        runtimeFailure("\"\\q\"","quotes");runtimeFailure("2 +","incomplete-expression");
        runtimeFailure("10 ^ 999","finite");runtimeFailure("sconosciuta","uninitialized");
        caseFailure(13,"domanda","\"eta >= 18\"","condition-type");
        caseFailure(16,"domanda","0 <= eta <= 65","boolean-number");
        Document noStart=lesson(1).solution();noStart.nodes.remove(0);noStart.edges.clear();structural(noStart,"start-count");
        Document twoStarts=lesson(1).solution();twoStarts.nodes.add(new Node(START,"Altro",0,0));structural(twoStarts,"start-count");
        Document noEnd=lesson(1).solution();noEnd.nodes.remove(1);noEnd.edges.clear();structural(noEnd,"end-missing");
        Document noArrow=lesson(1).solution();noArrow.edges.clear();structural(noArrow,"single-outgoing");
        Document branches=lesson(13).solution();branches.edges.removeIf(e->e.branch.equals("false"));structural(branches,"decision-branches");
        Document backToStart=lesson(1).solution();backToStart.edges.add(new Edge("fine","inizio","next"));structural(backToStart,"start-incoming");structural(backToStart,"end-outgoing");
        Document closed=lesson(13).solution();closed.edges.removeIf(e->e.from.equals("si"));closed.edges.add(new Edge("si","domanda","next"));closed.edges.removeIf(e->e.branch.equals("false"));closed.edges.add(new Edge("domanda","si","false"));structural(closed,"no-end-path");
        Document unused=lesson(1).solution();unused.nodes.add(new Node(OUTPUT,"5",0,0));structural(unused,"unreachable");
        Document unassigned=lesson(2).solution();unassigned.find("messaggio").text="totale";structural(unassigned,"uninitialized");
        ok(FlowTutor.literal("falso and (5 / 0 > 0)").format().equals("falso"),"and short circuit guards a dangerous right side");
        ok(FlowTutor.literal("vero or (5 / 0 > 0)").format().equals("vero"),"or short circuit guards a dangerous right side");
        FlowTutorGuide.Inspection comparison=FlowTutorGuide.inspect(new Node(OUTPUT,"5 = 5",0,0));boolean hasEquality=false;for(FlowTutorGuide.Term t:comparison.symbols){ok(!t.id.equals("assign"),"Output comparison is never described as assignment");hasEquality|=t.id.equals("equal");}ok(hasEquality,"Output equality explained");
        ok(FlowTutorGuide.token("||").id.equals("or"),"accepted || operator has a glossary entry");
        Document wrongShape=lesson(1).seed();FlowTutor.insert(lesson(1),wrongShape,"fine",OUTPUT);
        ok(!FlowTutor.actionRequired(lesson(1),0,wrongShape),"wrong shape can be checked instead of silently reopening the picker");
        ok(FlowTutor.checkStep(lesson(1),0,wrongShape,"").code.equals("block-kind"),"wrong shape explains its different purpose");
        FlowTutorGuide.Inspection capitals=FlowTutorGuide.inspect(new Node(PROCESS,"PUNTI = punti + 1",0,0));
        ok(capitals.reads.contains("punti")&&capitals.writes.contains("punti")&&capitals.writes.size()==1,"case aliases are described as the same read/write variable");
        Document question=lesson(6).solution();question.find("eta").prompt="Quanti anni hai?";
        Document roundTrip=FlowCodec.decode(FlowCodec.encode(question));
        ok(roundTrip.find("eta").prompt.equals("Quanti anni hai?"),"human question survives document encoding");
        FlowEngine.Runner prompted=new FlowEngine.Runner(roundTrip);for(int i=0;i<8&&prompted.state!=FlowEngine.State.WAITING;i++)prompted.step();
        ok(prompted.state==FlowEngine.State.WAITING&&prompted.inputPrompt.equals("Quanti anni hai?"),"real runner separates the human question from the variable name");
        FlowTutor.Lesson valueLesson=lesson(6);int valueStep=valueLesson.steps.length-1;
        FlowTutor.Feedback anotherNumber=FlowTutor.checkStep(valueLesson,valueStep,valueLesson.solution(),"22");
        ok(anotherNumber.pass,"a valid different number follows the echo learning goal");
        ok(FlowTutor.checkStep(valueLesson,valueStep,valueLesson.solution(),"eta").code.equals("input-type"),"typing a variable name into numeric runtime input receives a type explanation");
        ok(FlowTutor.checkStep(valueLesson,valueStep,valueLesson.solution(),"21,0").pass,"an equivalent input number with decimal comma is accepted");
        ok(FlowTutor.inputRule(lesson(6),"eta","2 + 3").code.equals("input-type"),"runtime input is data, not an evaluated expression");
        ok(FlowTutor.inputRule(lesson(22),"numero","2.5").code.equals("input-integer"),"parity lesson explains integer precondition");
        ok(FlowTutor.inputRule(lesson(20),"n","-1").code.equals("input-range"),"nonnegative cycle precondition explained");
        ok(FlowTutor.inputRule(lesson(6),"eta","21")==null&&FlowTutor.inputRule(lesson(7),"nome","Ada")==null,"valid runtime data accepted");
        ok(FlowTutor.recoverStep(lesson(2),2,lesson(2).seed())==0,"stale progress rewinds when an added block is missing");
        FlowTutor.Workspace phases=new FlowTutor.Workspace(lesson(2).seed());phases.before(0);FlowTutor.insert(lesson(2),phases.document,"messaggio",OUTPUT);ok(phases.undo(1)&&phases.restoredPhase==0,"undo restores the teaching step together with the graph");ok(phases.redo(0)&&phases.restoredPhase==1,"redo restores graph and phase together");
        Node multi=new Node(PROCESS,"totale = a + b\nmedia = totale / 2",0,0);FlowTutorGuide.Inspection vars=FlowTutorGuide.inspect(multi);ok(vars.writes.contains("totale")&&vars.writes.contains("media")&&vars.reads.contains("a")&&vars.reads.contains("b"),"block guide explains all reads and writes across statements");
        FlowTutor.Workspace w=new FlowTutor.Workspace(lesson(1).seed());String original=FlowCodec.encode(w.document);w.before();FlowTutor.insert(lesson(1),w.document,"fine",END);ok(w.undo()&&FlowCodec.encode(w.document).equals(original),"one undo restores inserted block and edges");ok(w.redo()&&FlowTutor.check(lesson(1),w.document).pass,"redo restores complete transaction");
        for(FlowTutorGuide.Preset p:FlowTutorGuide.presets()){ok(!p.explanation.isEmpty()&&!p.needs.isEmpty()&&!p.example.isEmpty(),"preset teaches action, dependencies and example");ok(FlowTutorDiagnostics.before(new Node(p.kind,p.text,0,0))==null,"preset syntax supported "+p.id);}
        for(FlowTutorGuide.Preset p:FlowTutorGuide.presets()){
            switch(p.id){
                case "input-number":preset(p,"","numero",new String[]{"7"},new String[]{"7"});break;
                case "input-age":preset(p,"","eta",new String[]{"18"},new String[]{"18"});break;
                case "zero":preset(p,"","contatore",new String[0],new String[]{"0"});break;
                case "increment":preset(p,"contatore = 2","contatore",new String[0],new String[]{"3"});break;
                case "double":preset(p,"numero = 4","doppio",new String[0],new String[]{"8"});break;
                case "sum":preset(p,"a = 2\nb = 3","totale",new String[0],new String[]{"5"});break;
                case "accumulate":preset(p,"somma = 8\nnumero = 3","somma",new String[0],new String[]{"11"});break;
                case "age":preset(p,"eta = 17","",new String[0],new String[]{"\"no\""});preset(p,"eta = 18","",new String[0],new String[]{"\"si\""});break;
                case "nonzero":preset(p,"numero = 0","",new String[0],new String[]{"\"no\""});preset(p,"numero = 4","",new String[0],new String[]{"\"si\""});break;
                case "positive":preset(p,"numero = -2","",new String[0],new String[]{"\"no\""});preset(p,"numero = 0","",new String[0],new String[]{"\"no\""});preset(p,"numero = 5","",new String[0],new String[]{"\"si\""});break;
                case "output-value":preset(p,"numero = 7","",new String[0],new String[]{"7"});break;
                case "output-text":preset(p,"","",new String[0],new String[]{"\"Ciao!\""});break;
                case "end":preset(p,"","",new String[0],new String[0]);break;
                default:throw new AssertionError("Preset has no authored execution case: "+p.id);
            }
        }
        FlowTutor.Lesson greeting=lesson(2);
        for(String text:new String[]{"\"ciao\"","\"CIAO\"","\"Benvenuto Christian\"","'salve'","\"Ciao\" + \"!\"","\"5\""}){
            Document d=greeting.solution();d.find("messaggio").text=text;ok(FlowTutor.check(greeting,d).pass,"message chosen by learner accepted: "+text);ok(FlowTutor.checkStep(greeting,1,d,"").pass,"same grading rule in step: "+text);
        }
        for(String text:new String[]{"5","vero","\"\"","\"   \"","ciao","“ciao”"}){Document d=greeting.solution();d.find("messaggio").text=text;ok(!FlowTutor.check(greeting,d).pass,"wrong type, blank and invalid syntax remain errors: "+text);}
        for(FlowTutor.Lesson l:FlowTutorContent.all()){
            ok(FlowTutor.criteria(l).length()>80,"criteria disclosed for "+l.id);
            if(FlowTutor.labelOutput(l)){
                Document d=l.solution();for(Node n:d.nodes)if(n.kind==OUTPUT)try{FlowEngine.Value value=FlowTutor.literal(n.text);if(value.isText())n.text="\"  "+value.format().toUpperCase(java.util.Locale.ROOT)+"  \"";}catch(IllegalArgumentException ignored){}
                ok(FlowTutor.check(l,d).pass,"label formatting accepted "+l.id);
                d=l.solution();for(Node n:d.nodes)if(n.kind==OUTPUT){n.text="\"un altro risultato\"";break;}ok(!FlowTutor.check(l,d).pass,"different label is not silently accepted "+l.id);
            }
        }
        Map<String,FlowEngine.Value> values=new HashMap<>();values.put("punti",FlowTutor.literal("4"));values.put("eta",FlowTutor.literal("17"));values.put("nome",FlowTutor.literal("\"Ada\""));
        String explanation=FlowTutorGuide.explain(new Node(PROCESS,"punti = punti + 1",0,0),values);
        ok(explanation.contains("passa da 4 a 5")&&explanation.contains("Legge punti = 4"),"explain actual old/new values");ok(values.get("punti").format().equals("4"),"explanation does not mutate runtime values");
        explanation=FlowTutorGuide.explain(new Node(DECISION,"eta >= 18",0,0),values);ok(explanation.contains("falso"),"decision explains actual result at 17");
        values.put("eta",FlowTutor.literal("18"));ok(FlowTutorGuide.explain(new Node(DECISION,"eta >= 18",0,0),values).contains("vero"),"decision explanation includes threshold 18");
        ok(FlowTutorGuide.explain(new Node(OUTPUT,"nome",0,0),values).contains("«Ada»"),"variable output explains stored data");ok(FlowTutorGuide.explain(new Node(OUTPUT,"\"nome\"",0,0),values).contains("«nome»"),"literal output explains text");
        ok(FlowTutorGuide.explain(new Node(OUTPUT,"totale",0,0),Collections.<String,FlowEngine.Value>emptyMap()).contains("serve prima un valore per totale"),"missing values are not invented");
        ok(FlowTutorGuide.explain(new Node(INPUT,"eta",0,0),null).contains("Il nome non è il valore"),"input name is not runtime value");
        ok(FlowTutorGuide.explain(new Node(PROCESS,"x = 2\ny = x * 3",0,0),null).contains("y riceve 6"),"multi-assignment explanation in order");
        ok(FlowTutorGuide.explain(new Node(OUTPUT,"1 / 0",0,0),null).contains("divisore != 0"),"division error includes actionable explanation");
        for(FlowTutor.Lesson l:FlowTutorContent.all()){
            ok(l.criteria.length()>100&&!l.assessment.isEmpty(),"declared contract "+l.id);
            Document d=l.solution();Map<String,String> idsMap=new HashMap<>();for(Node n:d.nodes)idsMap.put(n.id,"other_"+n.id);for(Node n:d.nodes)n.id=idsMap.get(n.id);for(Edge e:d.edges){e.from=idsMap.get(e.from);e.to=idsMap.get(e.to);}
            ok(FlowTutor.check(l,d).pass,"independent IDs accepted "+l.id);
            d=l.solution();Map<String,String> renameMap=new HashMap<>();for(String name:FlowTutor.variableNames(d,false))renameMap.put(name,"dato_"+name);for(Node n:d.nodes)if(n.kind!=START&&n.kind!=END)n.text=FlowTutor.rewrite(n.text,renameMap);
            ok(FlowTutor.check(l,d).pass,"consistent variable aliases accepted "+l.id);
            for(int phase=0;phase<l.steps.length;phase++)if(l.steps[phase].action==FlowTutor.WRITE)ok(FlowTutor.checkStep(l,phase,d,"").pass,"guided step accepts alias-based actual solution "+l.id+"/"+phase);
            d=l.solution();for(Node n:d.nodes)if(n.kind==DECISION){n.text="not ("+n.text+")";for(Edge e:d.edges)if(e.from.equals(n.id))e.branch=e.branch.equals("true")?"false":"true";}
            ok(FlowTutor.check(l,d).pass,"negated condition and swapped branches accepted "+l.id);
            d=l.solution();d.edges.remove(0);ok(!FlowTutor.check(l,d).pass,"missing connection rejected "+l.id);
            if(FlowTutor.labelOutput(l)){
                d=l.solution();for(Node n:d.nodes)if(n.kind==OUTPUT)try{FlowEngine.Value value=FlowTutor.literal(n.text);if(value.isText())n.text="\"  "+value.format().toUpperCase(java.util.Locale.ROOT).replace(" ","   ")+"!!!  \"";}catch(IllegalArgumentException ignored){}
                ok(FlowTutor.check(l,d).pass,"display formatting does not invalidate a label "+l.id);
            }
            for(Node n:l.solution().nodes)if(n.kind==DECISION){d=l.solution();d.find(n.id).text="vero";FlowTutor.Feedback f=FlowTutor.check(l,d);ok(!f.pass&&f.total==l.trials.length&&f.passed<f.total,"all counterexamples counted "+l.id);ok(f.trace.contains("Decisione"),"actual failed route available "+l.id);break;}
        }
        Document renamedInput=lesson(6).solution();renamedInput.find("eta").text="anni";ok(FlowTutor.checkStep(lesson(6),1,renamedInput,"").pass,"alternative valid Input name accepted");ok(FlowTutor.rename(renamedInput,"eta","anni")>0&&FlowTutor.check(lesson(6),renamedInput).pass,"dangling old variable reads repaired coherently");
        Map<String,String> lexical=new HashMap<>();lexical.put("max","valore");lexical.put("e3","oops");lexical.put("x","nuovo");String rewritten=FlowTutor.rewrite("x = max(max, 1e-3)\ny = \"max e3 x\"\nz = 'x'",lexical);ok(rewritten.startsWith("nuovo =")&&rewritten.contains("max(valore, 1e-3)")&&rewritten.contains("\"max e3 x\"")&&rewritten.contains("'x'"),"rename respects function names, exponents and quoted text");
        Document collision=lesson(9).solution();String originalCollision=FlowCodec.encode(collision);boolean blocked=false;try{FlowTutor.rename(collision,"pagato","prezzo");}catch(IllegalArgumentException expected){blocked=true;}ok(blocked&&FlowCodec.encode(collision).equals(originalCollision),"variable collision rejected without partial changes");
        System.out.println("PASS "+checks+" assertions; walked "+steps+" construction steps and executed "+trials+" goal cases (71 authored and 293 independently generated). JVM model/diagnostics only, not Android UI.");
    }
}
