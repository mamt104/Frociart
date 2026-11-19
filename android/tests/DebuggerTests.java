package it.frociart.app;
import java.util.*;import java.nio.file.*;import java.nio.charset.StandardCharsets;import static it.frociart.app.FlowModel.*;
public final class DebuggerTests {
 static Document d;static FlowEngine.Runner r;static FlowDebugger debug;static String current;static Path exchange;static int checks;
 static String dec(String text){return new String(Base64.getDecoder().decode(text),StandardCharsets.UTF_8);}
 static void eq(String a,String b){checks++;if(!Objects.equals(a,b))throw new AssertionError(current+": expected ["+a+"] actual ["+b+"]");}
 static Document graph(String name){if(name.isEmpty())return d;for(Function f:d.functions)if(f.name.equals(name))return f.body;throw new AssertionError("Missing function "+name);}
 static List<Document> graphs(){List<Document> result=new ArrayList<>();result.add(d);if(d.functions!=null)for(Function f:d.functions)result.add(f.body);return result;}
 static void ensure(){if(r!=null)return;r=new FlowEngine.Runner(d);eq("READY",r.state.toString());debug=new FlowDebugger(r,d);}
 static void drive(){ensure();int guard=0;while(debug.active()&&r.state!=FlowEngine.State.WAITING){if(++guard>25000)throw new AssertionError("Debugger did not yield/terminate");debug.tick();}}
 static String title(String s){return s.substring(0,1)+s.substring(1).toLowerCase(java.util.Locale.ROOT);}
 static String field(String key){Document g=d;if(r.currentFunctionId!=null)for(Function f:d.functions)if(f.id.equals(r.currentFunctionId))g=f.body;Node n=g.find(r.currentNodeId);switch(key){case "state":return r.state==FlowEngine.State.WAITING?"WaitingForInput":title(r.state.toString());case "mode":return title(debug.mode().toString());case "point":return debug.atBreakpoint()?"True":"False";case "text":return n.text;case "kind":return new String[]{"Start","End","Process","Decision","Input","Output","Call","Declare"}[n.kind];case "function":return r.currentFunctionName;case "depth":return ""+r.callDepth();case "steps":return ""+r.steps;case "output":return String.join("|",r.output);default:return r.variables.containsKey(key)?r.variables.get(key).format():"<missing>";}}
 public static void main(String[] args)throws Exception{
 Path path=Paths.get(args[0]);exchange=path.toAbsolutePath().getParent();Files.createDirectories(exchange.resolve("java-files"));
 for(String line:Files.readAllLines(path,StandardCharsets.UTF_8)){String[] parts=line.split("\t",-1);current=parts[0];String op=parts[1],a=dec(parts[2]),b=dec(parts[3]);switch(op){
 case "source":d=FlowPseudocode.decode(a);r=null;debug=null;break;
 case "mark":String[] target=a.split("\\|",-1);boolean found=false;for(Node n:graph(target[0]).nodes)if(n.text.equals(target[1])){n.breakpoint=true;n.breakpointCondition=b;found=true;}eq("true",""+found);break;
 case "markKind":for(Node n:d.nodes)if(new String[]{"Start","End","Process","Decision","Input","Output","Call","Declare"}[n.kind].equals(a))n.breakpoint=true;break;
 case "selfloop":for(Node n:d.nodes)if(n.text.equals(a))for(Edge e:d.edges)if(e.from.equals(n.id)&&e.branch.equals(b))e.to=n.id;break;
 case "begin":ensure();eq("true",""+debug.begin(FlowDebugger.Command.valueOf(a.toUpperCase(java.util.Locale.ROOT))));break;
 case "drive":drive();break;
 case "tick":ensure();debug.tick();break;
 case "pause":debug.pause();break;
 case "reset":r.reset();break;
 case "ignore":ensure();debug.ignoreBreakpoints=a.equals("true");break;
 case "update":debug.updateBreakpoints(d);break;
 case "clear":for(Document g:graphs())for(Node n:g.nodes){n.breakpoint=false;n.breakpointCondition=null;}debug.updateBreakpoints(d);break;
 case "expect":ensure();eq(b,field(a));break;
 case "missing":eq("false",""+r.variables.containsKey(a));break;
 case "message":eq("true",""+(debug.message()!=null&&debug.message().contains(a)));break;
 case "input":boolean accepted=r.submitInput(a);eq(b,""+accepted);if(accepted)debug.inputProvided();break;
 case "save":String original=FlowCodec.encode(d);d=d.copy();eq(original,FlowCodec.encode(d));Path file=exchange.resolve("java-files").resolve(current+".frociart");Files.write(file,FlowCodec.encode(d).getBytes(StandardCharsets.UTF_8));d=FlowCodec.decode(new String(Files.readAllBytes(file),StandardCharsets.UTF_8));eq(original,FlowCodec.encode(d));Path other=exchange.resolve("pc-files").resolve(current+".frociart");if(Files.exists(other)){Document old=d;d=FlowCodec.decode(new String(Files.readAllBytes(other),StandardCharsets.UTF_8));eq(""+old.nodes.size(),""+d.nodes.size());}break;
 default:throw new AssertionError("Unknown action "+op);
 }}
 current="invalid-condition-validation";for(String text:new String[]{"random(2)>0","falso e random(2)>0","missingFunction()","x +"}){boolean rejected=false;try{new FlowEngine.DebugCondition(text);}catch(IllegalArgumentException ex){rejected=true;}eq("true",""+rejected);}
 current="out-at-main";d=FlowPseudocode.decode("STAMPA 1");r=new FlowEngine.Runner(d);debug=new FlowDebugger(r,d);eq("false",""+debug.begin(FlowDebugger.Command.OUT));eq("0",""+r.steps);
 current="rename-condition";d=FlowPseudocode.decode("FUNZIONE prova(n: INTERO) -> INTERO risultato\nrisultato=n+1\nFINE FUNZIONE\nSTAMPA prova(2)");Function f=d.functions.get(0);f.body.nodes.get(0).breakpoint=true;f.body.nodes.get(0).breakpointCondition="n = 2 e \"n\" = \"n\"";Function next=f.copy();next.parameters.get(0).name="numero";Map<String,String> names=new HashMap<>();names.put("n","numero");d=FlowFunctionEditing.save(d,next,names);eq("numero = 2 e \"n\" = \"n\"",d.functions.get(0).body.nodes.get(0).breakpointCondition);
 current="condition-bounds";char[] chars=new char[8193];Arrays.fill(chars,'x');d.nodes.get(0).breakpointCondition=new String(chars);boolean rejectedSize=false;try{FlowCodec.encode(d);}catch(IllegalArgumentException ex){rejectedSize=true;}eq("true",""+rejectedSize);
 System.out.println("DebuggerTests PASS "+checks+" assertions, 30 scenarios; project persistence and counterpart exchange when present");
 }
}
