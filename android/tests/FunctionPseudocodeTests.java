package it.frociart.app;
import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.util.*;
import static it.frociart.app.FlowModel.*;
public final class FunctionPseudocodeTests {
 static int checks;static void check(boolean b,String message){checks++;if(!b)throw new AssertionError(message);}
 static void equal(Object expected,Object actual,String message){check(Objects.equals(expected,actual),message+" expected="+expected+" actual="+actual);}
 static String decode(String s){return new String(Base64.getDecoder().decode(s),StandardCharsets.UTF_8);}
 static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8);}
 static FlowEngine.Runner run(Document d,String input){FlowEngine.Runner r=new FlowEngine.Runner(d);String[] values=input.isEmpty()?new String[0]:input.split("\u001f",-1);int at=0;for(int i=0;i<30000&&r.state!=FlowEngine.State.ERROR&&r.state!=FlowEngine.State.COMPLETED;i++){if(r.state==FlowEngine.State.WAITING){check(at<values.length,"input available");check(r.submitInput(values[at++]),"input accepted");}else r.step();}check(r.state==FlowEngine.State.COMPLETED||r.state==FlowEngine.State.ERROR,"bounded run");return r;}
 public static void main(String[] args)throws Exception{
  Path root=Paths.get(args[0]),out=root.resolve("android");Files.createDirectories(out);List<String> cases=Files.readAllLines(Paths.get(args[1]),StandardCharsets.UTF_8);int index=0;
  for(String line:cases){String[] c=line.split("\t",-1);Path file=root.resolve("shared-"+index+".pseudo");String code=read(file);Document d=FlowPseudocode.decode(code);equal(code,FlowPseudocode.encode(d),"canonical fixture "+index);FlowEngine.Runner r=run(d,decode(c[1]));equal(c[2].toUpperCase(Locale.ROOT),r.state.toString(),"state "+index);equal(decode(c[3]),String.join("\u001e",r.output),"output "+index);equal(Integer.parseInt(c[4]),r.steps,"steps "+index);Files.write(out.resolve(file.getFileName()),FlowPseudocode.encode(d).getBytes(StandardCharsets.UTF_8));index++;}
  int valid=0;for(String line:Files.readAllLines(root.resolve("valid.tsv"),StandardCharsets.UTF_8)){String[] c=line.split("\t",-1);Document d=FlowPseudocode.decode(decode(c[0]));FlowEngine.Runner r=run(d,decode(c[1]));equal(FlowEngine.State.COMPLETED,r.state,"valid syntax "+r.errorMessage);equal(decode(c[2]),String.join("\u001e",r.output),"example output");String code=FlowPseudocode.encode(d);equal(code,FlowPseudocode.encode(FlowPseudocode.decode(code)),"canonical typed signature");valid++;}
  int invalid=0;for(String line:Files.readAllLines(root.resolve("invalid.tsv"),StandardCharsets.UTF_8)){String[] c=line.split("\t",-1);boolean failed=false;try{FlowPseudocode.decode(decode(c[0]));}catch(FlowPseudocode.ParseError e){failed=true;equal(Integer.parseInt(c[1]),e.line,"source line "+e.getMessage());}check(failed,"invalid syntax rejected");invalid++;}
  String detached=read(root.resolve("detached.pseudo"));equal(detached,FlowPseudocode.encode(FlowPseudocode.decode(detached)),"disconnected graph inside function preserved");Files.write(out.resolve("detached.pseudo"),detached.getBytes(StandardCharsets.UTF_8));
  System.out.println("FUNCTION_PSEUDOCODE_PASS checks="+checks+" shared="+index+" valid="+valid+" invalid="+invalid);
 }
}
