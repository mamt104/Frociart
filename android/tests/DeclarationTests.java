package it.frociart.app;
import java.util.*;import java.nio.file.*;import java.nio.charset.StandardCharsets;
public class DeclarationTests {
 static int checks;static String current;
 static void eq(Object expected,Object actual){checks++;if(!Objects.equals(expected,actual))throw new AssertionError(current+": expected ["+expected+"] actual ["+actual+"]");}
 static String decode(String s){return new String(Base64.getDecoder().decode(s),StandardCharsets.UTF_8);}
 static FlowEngine.Runner run(FlowModel.Document d,String[] inputs){FlowEngine.Runner r=new FlowEngine.Runner(d);int input=0;for(int i=0;i<1000&&r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR;i++){if(r.state==FlowEngine.State.WAITING){if(input>=inputs.length)throw new AssertionError(current+": missing input");eq(true,r.submitInput(inputs[input++]));}else r.step();}return r;}
 public static void main(String[] args)throws Exception{
 for(String line:Files.readAllLines(Paths.get(args[0]),StandardCharsets.UTF_8)){String[] f=line.split("\t",-1);current=f[0];FlowModel.Document doc=FlowPseudocode.decode(decode(f[1]));Path exchange=Paths.get(args[0]).toAbsolutePath().getParent();Files.createDirectories(exchange.resolve("java-files"));Files.write(exchange.resolve("java-files").resolve(current+".frociart"),FlowCodec.encode(doc).getBytes(StandardCharsets.UTF_8));Path other=exchange.resolve("pc-files").resolve(current+".frociart");for(int pass=0;pass<(Files.exists(other)?4:3);pass++){
  if(pass==3)doc=FlowCodec.decode(new String(Files.readAllBytes(other),StandardCharsets.UTF_8));
  FlowEngine.Runner r=f[2].equals("Invalid")?new FlowEngine.Runner(doc):run(doc,decode(f[4]).split("\u001e",-1));
  eq(f[2].equals("Completed")?FlowEngine.State.COMPLETED:FlowEngine.State.ERROR,r.state);eq(decode(f[3]),String.join("|",r.output));if(f[2].equals("Invalid"))eq(0,r.steps);
  for(String type:decode(f[5]).split(";"))if(!type.isEmpty()){String[] pair=type.split("=");eq(pair[1],r.variableTypes().get(pair[0]));}
  if(f[2].equals("Completed"))doc=pass==0?FlowPseudocode.decode(FlowPseudocode.encode(doc)):FlowCodec.decode(FlowCodec.encode(doc));
 }}
 current="invalid-input-retry";FlowEngine.Runner runner=new FlowEngine.Runner(FlowPseudocode.decode("DICHIARA x: INTERO\nLEGGI x\nSTAMPA x"));while(runner.state!=FlowEngine.State.WAITING)runner.step();eq(false,runner.submitInput("abc"));eq(FlowEngine.State.WAITING,runner.state);eq("0",runner.variables.get("x").format());eq(true,runner.submitInput("2.8"));eq("2",runner.variables.get("x").format());
 current="transaction";runner=run(FlowPseudocode.decode("DICHIARA a,b: INTERO\na=2; b=\"bad\""),new String[0]);eq(FlowEngine.State.ERROR,runner.state);eq("0",runner.variables.get("a").format());
 current="prefix-roundtrip";FlowModel.Document graph=FlowPseudocode.decode("DICHIARA a: INTERO\nSTAMPA a");for(FlowModel.Node n:graph.nodes)if(n.kind==FlowModel.DECLARE)n.text="DECLARE a: INTEGER";eq(FlowEngine.State.COMPLETED,run(FlowPseudocode.decode(FlowPseudocode.encode(graph)),new String[0]).state);
 current="syntax-reject";for(String s:new String[]{"", "x", "x,x: INTERO", "x: AUTO", "x: INTERO[]", "x: INTERO[2", "x,: REALE", "1x: TESTO"}){boolean rejected=false;try{FlowDeclaration.parse(s);}catch(IllegalArgumentException ex){rejected=true;}eq(true,rejected);}
 current="declaration-renames";Map<String,String> map=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);map.put("intero","dimensione");map.put("n","nomi");eq("  DICHIARA nomi: INTERO[dimensione]  ",FlowDeclaration.rewriteVariables("  DICHIARA n: INTERO[intero]  ",map));eq("x: INTERO",FlowDeclaration.rewriteVariables("x: INTERO",map));
 FlowModel.Document project=FlowPseudocode.decode("FUNZIONE calcolo(intero: INTERO) -> INTERO risultato\nDICHIARA a: INTERO[intero]\nDICHIARA risultato: INTERO\nrisultato=len(a)\nFINE FUNZIONE\nSTAMPA calcolo(3)");FlowModel.Function old=project.functions.get(0),changed=old.copy();changed.parameters.get(0).name="dimensione";Map<String,String> rename=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);rename.put("intero","dimensione");FlowModel.Document saved=FlowFunctionEditing.save(project,changed,rename);eq(FlowEngine.State.COMPLETED,run(saved,new String[0]).state);FlowFunctionEditing.Rename change=new FlowFunctionEditing.Rename(old,changed,rename);String declarationId=null;for(FlowModel.Node n:saved.functions.get(0).body.nodes)if(n.kind==FlowModel.DECLARE&&n.text.startsWith("a:"))declarationId=n.id;eq("a: INTERO[dimensione + 1]",change.rewriteDraft(saved,declarationId,"a: INTERO[intero + 1]",false));eq("a: INTERO[intero + 1]",change.rewriteDraft(saved,declarationId,"a: INTERO[dimensione + 1]",true));
 System.out.println("DeclarationTests PASS "+checks+" assertions");
 }
}
