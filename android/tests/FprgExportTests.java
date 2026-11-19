package it.frociart.app;
import java.util.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import static it.frociart.app.FlowModel.*;

public final class FprgExportTests {
 static int checks;
 static void check(boolean b,String text){checks++;if(!b)throw new AssertionError(text);}
 static String dec(String s){return new String(Base64.getDecoder().decode(s),StandardCharsets.UTF_8);}
 static FlowEngine.Runner execute(Document d,String[] row){
  FlowEngine.Runner r=new FlowEngine.Runner(d);int input=0;
  for(int i=0;i<4000&&r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR;i++){
   if(r.state==FlowEngine.State.WAITING)r.submitInput(row[5].split("\\|",-1)[input++]);else r.step();
  }
  check((r.state==FlowEngine.State.ERROR)==row[2].equals("error"),row[0]+" terminal state "+r.state+": "+r.errorMessage);
  check(r.state==FlowEngine.State.COMPLETED||r.state==FlowEngine.State.ERROR,row[0]+" bounded completion");
  check(r.outputText().replace("\r\n","\n").equals(row[3]),row[0]+" exact output: "+r.outputText());return r;
 }
 public static void main(String[] args)throws Exception{
  Path root=Paths.get(args[0]),out=root.resolve("android");Files.createDirectories(out);
  for(String title:new String[]{"A & B <C> \"D\"\n🐢","\u0001","\uD800"}){
   Document probe=FlowPseudocode.decode("STAMPA 1");probe.title=title;FlowgorithmExport.Result result=FlowgorithmExport.analyze(probe);
   if(title.charAt(0)=='A'){check(result.canExport(),"XML escaping valid");javax.xml.parsers.DocumentBuilderFactory factory=javax.xml.parsers.DocumentBuilderFactory.newInstance();factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl",true);org.w3c.dom.Document xml=factory.newDocumentBuilder().parse(new org.xml.sax.InputSource(new java.io.StringReader(result.xml)));check(((org.w3c.dom.Element)xml.getElementsByTagName("attribute").item(0)).getAttribute("value").equals(title),"Exact title after XML decode");check(!result.xml.contains("<!DOCTYPE")&&!result.xml.contains("name=\"authors\"")&&!result.xml.contains("name=\"saved\""),"Passive XML without history");}
   else check(!result.canExport()&&result.xml==null&&result.issues.stream().anyMatch(i->i.code.equals("xml-text")),"Invalid XML text rejected");
  }
  Document inputProbe=FlowPseudocode.decode("LEGGI x\nSTAMPA x");Map<String,Integer> badProfile=new HashMap<>();badProfile.put(":x",VOID);FlowgorithmExport.Result missing=FlowgorithmExport.analyze(inputProbe,badProfile);check(!missing.canExport()&&missing.requiredTypes.size()==1&&missing.requiredTypes.get(0).nodeId!=null,"Invalid profile requires deliberate type");
  Document invalid=FlowPseudocode.decode("x=1\nSTAMPA x");Node bad=null;for(Node n:invalid.nodes)if(n.kind==PROCESS)bad=n;bad.text="broken +";final String badId=bad.id;FlowgorithmExport.Result blocked=FlowgorithmExport.analyze(invalid);check(!blocked.canExport()&&blocked.xml==null&&blocked.issues.stream().anyMatch(i->badId.equals(i.nodeId)),"Error points to original block");
  for(String line:Files.readAllLines(root.resolve("cases.tsv"),StandardCharsets.UTF_8)){
   String[] raw=line.split("\t",-1),row=new String[raw.length];row[0]=raw[0];for(int i=1;i<raw.length;i++)row[i]=dec(raw[i]);
   Document d=FlowPseudocode.decode(row[1]);String before=FlowCodec.encode(d);Map<String,Integer> profile=new LinkedHashMap<>();
   if(!row[4].isEmpty())for(String pair:row[4].split("\\|")){String[] p=pair.split("=");profile.put(p[0],Integer.parseInt(p[1]));}
   FlowgorithmExport.Result result=FlowgorithmExport.analyze(d,profile);check(before.equals(FlowCodec.encode(d)),row[0]+" original unchanged");
   if(row[2].equals("reject")){boolean error=false;for(FlowgorithmExport.Issue i:result.issues)error|=i.error;check(!result.canExport()&&result.xml==null&&error,row[0]+" atomic rejection");System.out.println("PASS rejected "+row[0]);continue;}
   check(result.canExport(),row[0]+" valid export: "+result.issues);check(result.xml.equals(FlowgorithmExport.analyze(d,profile).xml),row[0]+" deterministic");
   FlowEngine.Runner original=execute(d,row),converted=execute(FlowgorithmImport.decode(result.xml),row);
   Map<String,FlowEngine.Value> values=original.callStack().get(0).variables,actual=converted.callStack().get(0).variables;List<String> watch=new ArrayList<>();
   for(Map.Entry<String,FlowEngine.Value> pair:values.entrySet()){
    FlowgorithmExport.NameMapping mapping=null;for(FlowgorithmExport.NameMapping m:result.names)if(m.functionId==null&&m.original.equalsIgnoreCase(pair.getKey()))mapping=m;
    check(mapping!=null,row[0]+" name mapping "+pair.getKey());FlowEngine.Value value=actual.get(mapping.exported);
    check(value!=null&&value.format().equals(pair.getValue().format()),row[0]+" original variable/rollback "+pair.getKey());
    watch.add(mapping.exported+"\t"+Base64.getEncoder().encodeToString(pair.getValue().format().getBytes(StandardCharsets.UTF_8)));
   }
   Files.write(out.resolve(row[0]+".watch.tsv"),watch,StandardCharsets.UTF_8);Files.write(out.resolve(row[0]+".fprg"),result.xml.getBytes(StandardCharsets.UTF_8));System.out.println("PASS exported "+row[0]);
  }
  System.out.println("FPRG_EXPORT_ANDROID_CORE PASS checks="+checks);
 }
}
