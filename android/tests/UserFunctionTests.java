package it.frociart.app;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import static it.frociart.app.FlowModel.*;

public final class UserFunctionTests {
    private static int checks;
    private static void check(boolean value,String message){checks++;if(!value)throw new AssertionError(message);}
    private static void equal(Object expected,Object actual,String message){check(Objects.equals(expected,actual),message+" expected="+expected+" got="+actual);}
    private static String decode(String value){return new String(Base64.getDecoder().decode(value),StandardCharsets.UTF_8);}
    private static void drain(FlowEngine.Runner r,String... inputs){int guard=0,input=0;while(r.state!=FlowEngine.State.ERROR&&r.state!=FlowEngine.State.COMPLETED){check(++guard<30000,"bounded execution");if(r.state==FlowEngine.State.WAITING){check(input<inputs.length,"input available");check(r.submitInput(inputs[input++]),"input accepted");}else r.step();}}
    private static String variables(FlowEngine.Runner r){ArrayList<String> keys=new ArrayList<>(r.variables.keySet());keys.sort(String.CASE_INSENSITIVE_ORDER);ArrayList<String> entries=new ArrayList<>();for(String key:keys)entries.add(key+"\u001d"+r.variables.get(key).format());return String.join("\u001e",entries);}
    private static Node n(int kind,String text){return new Node(kind,text,0,0);}
    private static Document linear(Node... nodes){Document d=new Document();d.nodes.add(n(START,"Inizio"));d.nodes.addAll(Arrays.asList(nodes));d.nodes.add(n(END,"Fine"));for(int i=1;i<d.nodes.size();i++)d.edges.add(new Edge(d.nodes.get(i-1).id,d.nodes.get(i).id,"next"));return d;}
    private static Function fn(String name,int type,Document body){Function f=new Function();f.name=name;f.returnType=type;f.returnVariable=type==VOID?"":"risultato";f.body=body;return f;}
    private static Document project(Document main,Function... fs){main.version=2;main.functions=new ArrayList<>(Arrays.asList(fs));return main;}
    private static void reject(Document d,String message){boolean rejected=false;try{FlowCodec.encode(d);}catch(IllegalArgumentException ex){rejected=true;}check(rejected,message);}
    public static void main(String[] args)throws Exception{
        Path out=Paths.get(args[1]);Files.createDirectories(out);int index=0;
        for(String line:Files.readAllLines(Paths.get(args[0]),StandardCharsets.UTF_8)){
            String[] c=line.split("\t",-1);Document d=FlowCodec.decode(decode(c[0]));String input=decode(c[1]);String[] inputs=input.isEmpty()?new String[0]:input.split("\u001f",-1);
            for(int roundtrip=0;roundtrip<2;roundtrip++){
                for(FlowEngine.Issue issue:FlowEngine.validate(d))check(!issue.error,"valid shared fixture "+index+": "+issue.message);
                FlowEngine.Runner r=new FlowEngine.Runner(d);drain(r,inputs);
                equal(c[2].toUpperCase(Locale.ROOT),r.state.toString(),"state fixture "+index);
                equal(decode(c[3]),String.join("\u001e",r.output),"output fixture "+index);
                equal(Integer.parseInt(c[4]),r.steps,"steps fixture "+index);
                equal(decode(c[5]),variables(r),"variables fixture "+index);
                d=FlowCodec.decode(FlowCodec.encode(d));
            }
            Files.write(out.resolve(index+".frociart"),FlowCodec.encode(d).getBytes(StandardCharsets.UTF_8));index++;
        }
        Function reader=fn("numero",INTEGER,linear(n(INPUT,"x"),n(PROCESS,"risultato=x")));Parameter p=new Parameter();p.name="x";p.type=INTEGER;reader.parameters.add(p);
        Document d=project(linear(n(OUTPUT,"numero(2)")),reader);FlowEngine.Runner r=new FlowEngine.Runner(d);
        for(int i=0;i<30&&r.state!=FlowEngine.State.WAITING&&r.state!=FlowEngine.State.ERROR;i++)r.step();
        equal(FlowEngine.State.WAITING,r.state,"callee input");equal(reader.id,r.currentFunctionId,"current function");equal("x",r.inputVariable,"input variable");
        List<FlowEngine.CallFrameSnapshot> snapshot=r.callStack();equal(2,snapshot.size(),"two frames");
        boolean immutable=false;try{snapshot.get(1).variables.clear();}catch(UnsupportedOperationException ex){immutable=true;}check(immutable,"snapshot read-only");
        check(!r.submitInput("wrong"),"invalid typed input rejected");equal(FlowEngine.State.WAITING,r.state,"retry remains available");equal("2",r.variables.get("x").format(),"invalid input preserves variable");
        check(r.submitInput("7"),"valid retry");Document body=r.currentDiagram();body.nodes.clear();check(!r.currentDiagram().nodes.isEmpty(),"diagram snapshot isolated");
        drain(r);equal("7",r.output.get(0),"returned input");equal("2",snapshot.get(1).variables.get("x").format(),"stack snapshot stable");
        r.reset();equal(0,r.steps,"reset steps");equal(0,r.variables.size(),"reset variables");equal(0,r.output.size(),"reset output");equal(1,r.callStack().size(),"reset main frame");
        reader.body.nodes.get(1).text="alterato";drain(r,"8");equal("8",r.output.get(0),"execution snapshot isolated from source");
        d.version=1;reject(d,"version guard");d.version=2;reader.body.functions=new ArrayList<>();reader.body.functions.add(reader);reject(d,"recursive metadata rejected");reader.body.functions=null;
        reader.parameters.add(p.copy());reject(d,"duplicate parameter");reader.parameters.remove(1);reader.name="sqrt";reject(d,"built-in reserved");reader.name="numero";
        reader.returnType=99;reject(d,"invalid return type");reader.returnType=INTEGER;
        Function hello=fn("saluta",VOID,linear(n(OUTPUT,"'Ciao'")));d=project(linear(n(OUTPUT,"saluta()")),hello);
        check(FlowEngine.validate(d).stream().anyMatch(i->i.error&&i.message.contains("non restituisce")),"void value rejected");
        hello=fn("manca",INTEGER,linear(n(PROCESS,"altro=1")));d=project(linear(n(OUTPUT,"manca()")),hello);String functionId=hello.id;
        check(FlowEngine.validate(d).stream().anyMatch(i->i.error&&functionId.equals(i.functionId)&&i.message.contains("risultato")),"return assignment diagnosed in function");
        System.out.println("USER_FUNCTIONS_PASS checks="+checks+" sharedFixtures="+index+" nativeAndroidUI=NOT_RUN");
    }
}
