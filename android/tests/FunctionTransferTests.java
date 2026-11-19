package it.frociart.app;
import java.util.*;import java.nio.file.*;import java.nio.charset.StandardCharsets;import static it.frociart.app.FlowModel.*;
public final class FunctionTransferTests {
 static int checks;static String current;
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(current+": "+why);checks++;}
 static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8);}
 static String run(Document d,String inputs){FlowEngine.Runner r=new FlowEngine.Runner(d);ArrayDeque<String> values=new ArrayDeque<>();if(!inputs.isEmpty())values.addAll(Arrays.asList(inputs.split("\\|")));for(int i=0;i<2000&&r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR;i++){if(r.state==FlowEngine.State.WAITING){check(!values.isEmpty(),"input supplied");check(r.submitInput(values.remove()),"input accepted");}else r.step();}check(r.state==FlowEngine.State.COMPLETED,"execution completed");return String.join("|",r.output);}
 interface Task{void run();}static void reject(Task task,String why){boolean bad=false;try{task.run();}catch(IllegalArgumentException e){bad=true;}check(bad,why);}
 public static void main(String[] args)throws Exception{
  Path root=Paths.get(args[0]),cross=root.resolve("cross");int count=0;
  for(String row:Files.readAllLines(root.resolve("cases.tsv"),StandardCharsets.US_ASCII)){String[] c=row.split("\t",-1);for(int i=0;i<c.length;i++)c[i]=new String(Base64.getDecoder().decode(c[i]),StandardCharsets.UTF_8);current=c[0];count++;
   Document source=FlowPseudocode.decode(c[1]),target=FlowPseudocode.decode(c[2]);Function selected=null;for(Function f:source.functions)if(f.name.equals(c[3]))selected=f;
   for(Node n:selected.body.nodes){n.prompt="Domanda helper(n) · €";n.trueLabel="Percorso A()";n.falseLabel="Altro";n.trueColor="#35A96C";n.falseColor="#DD5C79";}
   String beforeSource=FlowCodec.encode(source),beforeTarget=FlowCodec.encode(target);FlowFunctionTransfer.Plan plan=FlowFunctionTransfer.prepare(target,source,selected.id);Function imported=FlowFunctionEditing.find(plan.project,plan.selectedId);
   check(beforeSource.equals(FlowCodec.encode(source))&&beforeTarget.equals(FlowCodec.encode(target)),"originals unchanged");check(plan.added.size()==Integer.parseInt(c[7]),"dependency closure count");check(imported.name.equals(c[4]),"selected name");
   Document original=plan.project.copy();original.functions=target.functions;original.version=target.version;check(FlowCodec.encode(original).equals(beforeTarget),"existing definitions/main unchanged");
   Set<String> nodes=new HashSet<>(),edges=new HashSet<>();for(Document g:FlowFunctionEditing.graphs(plan.project)){for(Node n:g.nodes)check(nodes.add(n.id),"global unique node ID");for(Edge e:g.edges)check(edges.add(e.id),"global unique edge ID");}
   for(int i=0;i<selected.body.nodes.size();i++){Node a=selected.body.nodes.get(i),b=imported.body.nodes.get(i);check(!a.id.equals(b.id)&&a.x==b.x&&a.y==b.y&&a.prompt.equals(b.prompt)&&a.trueColor.equals(b.trueColor)&&a.falseColor.equals(b.falseColor),"fresh copy preserves metadata");}
   check(run(target,"").equals(run(plan.project,"")),"old main result unchanged");
   Document exercise=FlowPseudocode.decode("STAMPA 0");for(Node n:exercise.nodes)if(n.kind==OUTPUT)n.text=c[5];exercise.functions=plan.project.functions;exercise.version=2;check(run(exercise,c[8]).equals(c[6]),"imported result");
   Document library=FlowFunctionTransfer.export(source,selected.id);check(library.functions.size()==Integer.parseInt(c[7])&&library.nodes.size()==2,"export closure with blank main");
   check(run(FlowCodec.decode(read(cross.resolve(c[0]+".frociart"))),c[8]).equals(c[6]),"actual PC file executes on Android");
   Files.write(cross.resolve(c[0]+"-android.frociart"),FlowCodec.encode(exercise).getBytes(StandardCharsets.UTF_8));
   Document pcSource=FlowCodec.decode(read(cross.resolve(c[0]+"-source.frociart"))),pcTarget=FlowCodec.decode(read(cross.resolve(c[0]+"-target.frociart")));Function pcSelected=null;for(Function f:pcSource.functions)if(f.name.equals(c[3]))pcSelected=f;check(FlowFunctionTransfer.prepare(pcTarget,pcSource,pcSelected.id).added.size()==plan.added.size(),"PC sources produce same closure");
  }
  current="failures";Document good=FlowPseudocode.decode("FUNZIONE buona() -> INTERO valore\nvalore=1\nFINE FUNZIONE\nSTAMPA 0"),empty=FlowPseudocode.decode("STAMPA 0");String id=good.functions.get(0).id,clean=FlowCodec.encode(good),cleanTarget=FlowCodec.encode(empty);
  reject(()->FlowFunctionTransfer.prepare(empty,good,"missing"),"missing selection rejected");check(FlowCodec.encode(good).equals(clean)&&FlowCodec.encode(empty).equals(cleanTarget),"failed selection atomic");
  Function unrelated=good.functions.get(0).copy();unrelated.id=id();unrelated.name="rotta";unrelated.body=FlowPseudocode.decode("STAMPA 0");for(Node n:unrelated.body.nodes)if(n.kind==OUTPUT)n.text="assente()";good.functions.add(unrelated);
  check(FlowFunctionTransfer.prepare(empty,good,id).added.size()==1,"unrelated incomplete function ignored");reject(()->FlowFunctionTransfer.prepare(empty,good,unrelated.id),"selected unknown call rejected");
  for(Node n:good.functions.get(0).body.nodes)if(n.kind==PROCESS)n.text="valore=";String badBefore=FlowCodec.encode(good);reject(()->FlowFunctionTransfer.prepare(empty,good,id),"malformed function rejected");check(badBefore.equals(FlowCodec.encode(good))&&cleanTarget.equals(FlowCodec.encode(empty)),"parse failure atomic");
  Document valid=FlowPseudocode.decode("FUNZIONE buona() -> INTERO valore\nvalore=1\nFINE FUNZIONE\nSTAMPA 0");String validId=valid.functions.get(0).id;
  StringBuilder fullCode=new StringBuilder();for(int i=0;i<32;i++)fullCode.append("FUNZIONE fn").append(i).append("() -> INTERO valore\nvalore=0\nFINE FUNZIONE\n");fullCode.append("STAMPA 0");Document full=FlowPseudocode.decode(fullCode.toString());String fullBefore=FlowCodec.encode(full);reject(()->FlowFunctionTransfer.prepare(full,valid,validId),"function capacity");check(FlowCodec.encode(full).equals(fullBefore),"capacity failure atomic");
  Document crowded=FlowPseudocode.decode("STAMPA 0");while(crowded.nodes.size()<500)crowded.nodes.add(new Node(PROCESS,"x=0",0,0));reject(()->FlowFunctionTransfer.prepare(crowded,valid,validId),"global nodes capacity");
  Document edgeFull=FlowPseudocode.decode("STAMPA 0");while(edgeFull.edges.size()<1500)edgeFull.edges.add(new Edge(edgeFull.nodes.get(0).id,edgeFull.nodes.get(1).id,"next"));reject(()->FlowFunctionTransfer.prepare(edgeFull,valid,validId),"global edge capacity");
  System.out.println("FUNCTION_TRANSFER_ANDROID PASS checks="+checks+" cases="+count);
 }
}
