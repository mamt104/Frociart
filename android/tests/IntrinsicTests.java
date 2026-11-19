package it.frociart.app;
import java.nio.file.*;import java.nio.charset.*;import java.util.*;
public final class IntrinsicTests {
 static int checks;static void check(boolean b,String name){if(!b)throw new AssertionError(name);checks++;}
 static String decode(String b){return new String(Base64.getDecoder().decode(b),StandardCharsets.UTF_8);}
 static FlowEngine.Runner run(String code){FlowEngine.Runner r=new FlowEngine.Runner(FlowPseudocode.decode(code));for(int i=0;i<1000&&r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR;i++)r.step();return r;}
 public static void main(String[] args)throws Exception {
 for(String line:Files.readAllLines(Paths.get(args[0]),StandardCharsets.US_ASCII)){String[] p=line.split("\t",-1);String expression=decode(p[1]),expected=decode(p[2]);FlowEngine.Value actual=null;boolean error=false;try{actual=FlowEngine.evaluate(expression,null);}catch(IllegalArgumentException e){error=true;}check(error==p[0].equals("error"),expression+" error="+error);if(error)continue;check(p[0].equals("text")?actual.isText():actual.isNumber(),expression+" type");check(actual.format().equals(expected),expression+" expected="+expected+" actual="+actual.format());
 FlowModel.Document diagram=FlowPseudocode.decode("risultato = "+expression+"\nSTAMPA risultato");for(int i=0;i<2;i++){FlowEngine.Runner runtime=new FlowEngine.Runner(diagram);for(int j=0;j<10&&runtime.state!=FlowEngine.State.COMPLETED&&runtime.state!=FlowEngine.State.ERROR;j++)runtime.step();check(runtime.state==FlowEngine.State.COMPLETED&&runtime.output.size()==1&&runtime.output.get(0).equals(expected),"Native graph roundtrip: "+expression);diagram=FlowPseudocode.decode(FlowPseudocode.encode(diagram));}}
 Set<Double> values=new HashSet<>();for(int i=0;i<256;i++){double n=FlowEngine.evaluate("random(6)",null).asNumber();check(n>=0&&n<6&&n==Math.floor(n),"Random range");values.add(n);}check(values.size()>1,"Random generator is not a constant stub");
 check(FlowEngine.referencedVariables("sin(pi/2) + cos(π) + len(nome)").equals(Arrays.asList("nome")),"Constants excluded from unresolved variables");
 check(run("pi = 3\nSTAMPA pi").output.get(0).equals("3"),"Existing pi variable remains usable");
 check(run("STAMPA round(pi, 2)").output.get(0).equals("3.14"),"Pi default constant in graph");
 FlowEngine.Runner failed=run("x = 7\nx = 2; y = log(0)");check(failed.state==FlowEngine.State.ERROR&&failed.variables.get("x").asNumber()==7&&!failed.variables.containsKey("y"),"Domain error rolls back entire operation block");
 check(run("STAMPA false and log(0)").output.get(0).equals("falso"),"Short circuit preserves domain safety");
 System.out.println("INTRINSIC_PASS checks="+checks);
 }
}
