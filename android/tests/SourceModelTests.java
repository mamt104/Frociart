package it.frociart.app;
import java.util.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import static it.frociart.app.FlowModel.*;

public final class SourceModelTests {
    static int checks;static Path root;
    static final Set<Integer> kinds=new HashSet<>();
    static final Set<FlowSource.ExpressionKind> expressionKinds=new HashSet<>();
    static int structuredPlans,explicitPlans,traceChecks;
    static void eq(Object expected,Object actual,String label){checks++;if(!Objects.equals(expected,actual))throw new AssertionError(label+": expected "+expected+", actual "+actual);}
    static void reject(Runnable action,String label){boolean rejected=false;try{action.run();}catch(IllegalArgumentException|UnsupportedOperationException ex){rejected=true;}eq(true,rejected,label);}
    static String s(String value){return value==null?"~":Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8));}
    static void row(StringBuilder b,Object... values){for(int i=0;i<values.length;i++){if(i>0)b.append('\t');Object v=values[i];b.append(v instanceof Boolean?((Boolean)v?"1":"0"):v);}b.append('\n');}
    static void expr(StringBuilder b,FlowSource.Expression e){if(e==null){row(b,"null");return;}expressionKinds.add(e.kind);
        row(b,e.kind,s(e.text),String.format(Locale.ROOT,"%016x",Double.doubleToLongBits(e.number)),e.bool,s(e.functionId),e.children.size());for(FlowSource.Expression c:e.children)expr(b,c);}
    static void target(StringBuilder b,FlowSource.Target t){row(b,"target",s(t==null?null:t.name));expr(b,t==null?null:t.index);}
    static String dump(FlowSource.Program p){StringBuilder b=new StringBuilder();row(b,"program",s(p.title),p.canGenerate(),p.scopes.size());
        for(FlowSource.Scope scope:p.scopes){row(b,"scope",s(scope.id),s(scope.name),s(scope.startId),s(scope.returnVariable),scope.returnType,scope.returnIsArray);
            for(FlowSource.Parameter a:scope.parameters)row(b,"parameter",s(a.name),a.type,a.isArray);
            for(FlowSource.Block n:scope.blocks){kinds.add(n.kind);row(b,"block",s(n.id),n.kind,s(n.text),s(n.prompt),n.reachable);
                FlowSource.Operation o=n.operation;row(b,"flags",o.fileReading,o.fileNewline,o.turtleDraw,o.turtleLeft);expr(b,o.value);target(b,o.input);
                row(b,"writes",o.writes.size());for(FlowSource.Write w:o.writes){target(b,w.target);expr(b,w.value);}
                row(b,"declaration",o.declaration!=null);if(o.declaration!=null){row(b,o.declaration.type,o.declaration.isArray(),o.declaration.names.size());for(String name:o.declaration.names)row(b,s(name));expr(b,o.declaration.size);}
                for(FlowSource.Link e:n.links)row(b,"edge",s(e.id),s(e.to),s(e.branch));
            }
            FlowSourcePlan plan=FlowSourcePlan.create(scope);row(b,"structured",plan.structured);dumpPlan(b,plan.statements);
        }return b.toString();
    }
    static void dumpPlan(StringBuilder b,List<FlowSourcePlan.Statement> statements){for(FlowSourcePlan.Statement s:statements){row(b,"statement",s.kind,s(s.block.id),s.loopOnTrue,s.body.size(),s.otherwise.size());dumpPlan(b,s.body);dumpPlan(b,s.otherwise);}}
    static boolean choice(String id,int step,int seed){int hash=seed+1;for(char c:id.toCharArray())hash=hash*31+c;hash+=step*0x27d4eb2d;hash=(hash^(hash>>>16))*0x45d9f3b;hash=(hash^(hash>>>16))*0x45d9f3b;return ((hash^(hash>>>16))&1)!=0;}
    static final class StopTrace extends RuntimeException {}
    static void visit(List<String> trace,FlowSource.Block n){if(trace.size()==512)throw new StopTrace();trace.add(n.id);if(n.kind==END)throw new StopTrace();}
    static void walk(List<FlowSourcePlan.Statement> statements,List<String> trace,int seed){for(FlowSourcePlan.Statement s:statements){
        if(s.kind==FlowSourcePlan.Kind.While){while(true){visit(trace,s.block);if(choice(s.block.id,trace.size(),seed)!=s.loopOnTrue)break;walk(s.body,trace,seed);}}
        else{visit(trace,s.block);if(s.kind==FlowSourcePlan.Kind.If)walk(choice(s.block.id,trace.size(),seed)?s.body:s.otherwise,trace,seed);}
    }}
    static void planChecks(FlowSource.Scope scope){FlowSourcePlan plan=FlowSourcePlan.create(scope);if(!plan.structured){explicitPlans++;eq(0,plan.statements.size(),"fallback no partial tree");return;}structuredPlans++;
        for(int seed=0;seed<32;seed++){List<String> direct=new ArrayList<>();String id=scope.startId;
            for(int i=0;i<512;i++){FlowSource.Block n=scope.find(id);direct.add(id);if(n.kind==END)break;id=n.next(n.kind==DECISION?(choice(id,direct.size(),seed)?"true":"false"):"next");}
            List<String> actual=new ArrayList<>();try{walk(plan.statements,actual,seed);}catch(StopTrace ex){}
            eq(String.join("|",direct),String.join("|",actual),"structured trace seed "+seed);traceChecks++;
        }
    }
    static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8);}
    static Document load(String name)throws Exception{return FlowCodec.decode(read(root.resolve("fixtures").resolve(name+".frociart")));}
    static FlowSource.Program compile(String name)throws Exception{FlowSource.Program p=FlowSource.compile(load(name));String error="";for(FlowSource.Diagnostic d:p.diagnostics)error+=d.message+";";eq(true,p.canGenerate(),name+" compiles "+error);return p;}
    static FlowSource.Block kind(FlowSource.Scope scope,int kind){for(FlowSource.Block b:scope.blocks)if(b.kind==kind)return b;throw new AssertionError("missing block "+kind);}
    static FlowSource.Expression output(String name)throws Exception{return kind(compile(name).scopes.get(0),OUTPUT).operation.value;}
    static void structuralOracles()throws Exception{
        FlowSource.Expression a=output("expr-0");eq("+",a.text,"sum precedence");eq("*",a.children.get(1).text,"multiply nested");eq(3.0,a.children.get(1).children.get(1).number,"numeric leaf");
        a=output("expr-1");eq("*",a.text,"parentheses root");eq("+",a.children.get(0).text,"parentheses retained");
        a=output("expr-2");eq("^",a.text,"power root");eq("^",a.children.get(1).text,"right associative power");
        a=output("expr-7");eq("and",a.text,"short circuit retained");eq(false,a.children.get(0).bool,"false literal");eq("/",a.children.get(1).children.get(0).text,"unexecuted zero division retained");
        eq("random",output("expr-9").text,"random not evaluated");eq("eof",output("expr-10").text,"EOF no file access");eq("int",output("expr-11").text,"invalid dynamic step not evaluated");
        eq(FlowSource.ExpressionKind.Variable,output("expr-14").children.get(0).kind,"pi remains shadowable");
        FlowSource.Program p=compile("all-operations");eq(2,p.scopes.size(),"main and function");FlowSource.Scope scope=p.scopes.get(0);
        eq("greeting",kind(scope,CALL).operation.value.functionId,"resolved function ID");eq(TEXT,p.scopes.get(1).parameters.get(0).type,"parameter type");
        FlowSource.Block d=kind(scope,DECISION);eq(2,d.links.size(),"both branches retained");eq(d.next("true"),d.next("false"),"convergence retained");
        eq(false,kind(scope,TURTLE_FORWARD).operation.turtleDraw,"pen up");eq(true,kind(scope,TURTLE_TURN).operation.turtleLeft,"left turn");
        eq(false,kind(scope,WRITE_FILE).operation.fileNewline,"write without newline");
        FlowSource.Declaration array=null;FlowSource.Block lastWrite=null;for(FlowSource.Block b:scope.blocks){if(b.kind==DECLARE)array=b.operation.declaration;if(b.kind==WRITE_FILE)lastWrite=b;}
        eq(true,lastWrite.operation.fileNewline,"write newline");eq(true,array.isArray(),"array declaration");eq("+",array.size.text,"size expression");
        eq("i",kind(scope,INPUT).operation.input.index.text,"indexed input");
        List<FlowSource.Write> writes=kind(compile("transaction").scopes.get(0),PROCESS).operation.writes;
        eq(5,writes.size(),"transaction retains writes");eq("a",writes.get(2).target.name,"indexed target");eq("i",writes.get(2).target.index.text,"target after RHS");eq("+",writes.get(2).value.text,"RHS separate from index");
        eq(false,compile("unreachable").scopes.get(0).find("unused").reachable,"unreachable retained");eq("n1",compile("self-loop").scopes.get(0).find("n1").next("true"),"self loop retained");
        FlowSourcePlan reverse=FlowSourcePlan.create(compile("false-loop").scopes.get(0));for(FlowSourcePlan.Statement st:reverse.statements)if(st.kind==FlowSourcePlan.Kind.While)eq(false,st.loopOnTrue,"false branch loop polarity");
        eq(true,FlowSourcePlan.create(compile("two-ends").scopes.get(0)).structured,"separate return paths");
        eq(false,FlowSourcePlan.create(compile("deep-control").scopes.get(0)).structured,"bounded nesting keeps explicit graph");
        reject(()->p.scopes.clear(),"immutable scopes");reject(()->scope.blocks.clear(),"immutable blocks");reject(()->d.links.clear(),"immutable links");reject(()->d.operation.value.children.clear(),"immutable expression");
        final FlowSource.Declaration declaration=array;reject(()->declaration.names.clear(),"immutable declaration");eq(false,FlowSource.compile(null).canGenerate(),"null diagnostic");
        FlowSource.Program invalid=FlowSource.compile(load("missing-branch"));reject(()->new FlowSourceText.Builder(invalid,"python"),"invalid program cannot produce source");
    }
    static void sourceMaps()throws Exception{
        FlowSource.Program p=compile("all-operations");FlowSourceText.Builder builder=new FlowSourceText.Builder(p,"python");
        FlowSourceText.Origin main=new FlowSourceText.Origin(null,"n1"),function=new FlowSourceText.Origin("greeting","f-n1");
        builder.addLine("# generated",0);builder.addLine("print('è 🐢')",1,main,function,main);builder.addLine("",0);
        FlowSourceText code=builder.build();eq("# generated\n    print('è 🐢')\n\n",code.text,"source exact text");eq(3,code.lines.size(),"physical lines");
        eq(2,code.lines.get(1).origins.size(),"deduplicated origins");eq(1,code.forBlock(null,"n1").size(),"node to line");eq(1,code.forBlock("greeting","f-n1").size(),"function node to line");eq(0,code.forBlock(null,"f-n1").size(),"scope isolation");
        eq(true,code.forBlock(null,"n1")==code.forBlock(null,"n1"),"indexed lookup reuses immutable lines");reject(()->code.forBlock(null,"n1").clear(),"indexed mapping immutable");
        for(int i=0;i<code.text.length();i++){FlowSourceText.Line line=code.atOffset(i);eq(true,line!=null,"every UTF16 unit mapped");eq(true,i>=line.start&&i<=line.start+line.length,"offset span");}
        eq(null,code.atOffset(-1),"negative offset");eq(null,code.atOffset(code.text.length()),"EOF offset");
        reject(()->builder.addLine("bad\nline",0,main),"embedded newline");reject(()->builder.addLine("bad",129,main),"indent cap");reject(()->builder.addLine("bad",0,new FlowSourceText.Origin("wrong","n1")),"invalid origin");
        eq(code.text,builder.build().text,"failed writes atomic");builder.addLine("# later",0);eq(3,code.lines.size(),"immutable source snapshot");reject(()->code.lines.clear(),"immutable source lines");reject(()->code.lines.get(1).origins.clear(),"immutable origins");
        FlowSourceText.Builder limit=new FlowSourceText.Builder(p,"python");char[] huge=new char[FlowSourceText.Builder.MAX_CHARACTERS-1];Arrays.fill(huge,'x');limit.addLine(new String(huge),0);
        reject(()->limit.addLine("",0),"source size cap");eq(FlowSourceText.Builder.MAX_CHARACTERS,limit.build().text.length(),"size rejection atomic");
        FlowSourceText.Builder many=new FlowSourceText.Builder(p,"python");for(int i=0;i<FlowSourceText.Builder.MAX_LINES;i++)many.addLine("",0);
        reject(()->many.addLine("",0),"line count cap");eq(FlowSourceText.Builder.MAX_LINES,many.build().lines.size(),"line cap atomic");
    }
    public static void main(String[] args)throws Exception{
        root=Paths.get(args[0]);Path output=root.resolve("java-models");Files.createDirectories(output);int compared=0;StringBuilder outcomes=new StringBuilder();
        for(String row:Files.readAllLines(root.resolve("expected-valid.tsv"),StandardCharsets.UTF_8)){
            String[] columns=row.split("\t");String name=columns[0];Document document=load(name);String before=FlowCodec.encode(document);
            boolean expected=true;for(FlowEngine.Issue issue:FlowEngine.validate(document))if(issue.error)expected=false;FlowSource.Program p=FlowSource.compile(document);
            eq(expected,p.canGenerate(),name+" validation gate");if(!columns[1].equals("?"))eq(columns[1].equals("true"),p.canGenerate(),name+" fixture validity");
            row(outcomes,name,p.canGenerate());
            eq(before,FlowCodec.encode(document),name+" original unchanged");
            if(!p.canGenerate()){eq(0,p.scopes.size(),name+" no partial source");continue;}
            eq(document.nodes.size(),p.scopes.get(0).blocks.size(),name+" all main blocks");
            for(int i=0;i<p.scopes.size();i++){Document g=i==0?document:document.functions.get(i-1).body;int edges=0;for(FlowSource.Block n:p.scopes.get(i).blocks)edges+=n.links.size();eq(g.edges.size(),edges,name+" all edges");}
            for(FlowSource.Scope scope:p.scopes)planChecks(scope);
            String result=dump(p);Files.write(output.resolve(name+".txt"),result.getBytes(StandardCharsets.UTF_8));
            eq(read(root.resolve("pc-models").resolve(name+".txt")),result,name+" portable model");compared++;
            document.title="edited";document.nodes.get(0).text="edited";if(!document.edges.isEmpty())document.edges.get(0).to="edited";
            if(document.functions!=null)for(Function f:document.functions){f.name="edited";f.parameters.clear();}
            eq(result,dump(p),name+" snapshot detached");
        }
        Files.write(root.resolve("java-outcomes.tsv"),outcomes.toString().getBytes(StandardCharsets.UTF_8));eq(read(root.resolve("pc-outcomes.tsv")),outcomes.toString(),"cross-platform validation outcomes");
        structuralOracles();sourceMaps();eq(17,kinds.size(),"every node kind");eq(9,expressionKinds.size(),"every expression kind");
        System.out.println("SOURCE_MODEL PASS checks="+checks+" crossPlatform="+compared+" structured="+structuredPlans+" explicit="+explicitPlans+" traces="+traceChecks);
    }
}
