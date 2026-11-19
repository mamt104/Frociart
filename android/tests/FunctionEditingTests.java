package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;
public final class FunctionEditingTests {
 static int checks;
 static void check(boolean value,String why){checks++;if(!value)throw new AssertionError(why);}
 static Document fixture(){return FlowPseudocode.decode("FUNZIONE doppio(valore: REALE) -> REALE risultato\nINIZIO\nrisultato = valore * 2\nFINE\nFINE FUNZIONE\nINIZIO\nSTAMPA doppio(4)\nFINE");}
 static String run(Document project){FlowEngine.Runner r=new FlowEngine.Runner(project);for(int i=0;i<80&&r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR;i++)r.step();check(r.state==FlowEngine.State.COMPLETED,"edited project executes: "+r.errorMessage);return r.output.toString();}
 static void reject(Runnable edit,String why){try{edit.run();throw new AssertionError(why);}catch(IllegalArgumentException expected){checks++;}}
 static Node process(Document d){for(Node n:d.nodes)if(n.kind==PROCESS)return n;throw new AssertionError("process");}
 static void drafts(){
  Document d=fixture();Function before=d.functions.get(0),after=before.copy();after.name="triplo";after.parameters.get(0).name="dato";after.returnVariable="finale";
  Map<String,String> names=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);names.put("valore","dato");names.put("risultato","finale");
  FlowFunctionEditing.Rename rename=new FlowFunctionEditing.Rename(before,after,names);String local=process(before.body).id,main=d.nodes.get(1).id;
  String raw="risultato = doppio(valore) + 1e3 + \"valore doppio(3)\"";
  String rewritten=rename.rewriteDraft(d,local,raw,false);
  check(rewritten.equals("finale = triplo(dato) + 1e3 + \"valore doppio(3)\""),"draft variables and calls renamed without touching literals or exponents");
  check(rename.rewriteDraft(d,local,rewritten,true).equals(raw),"inverse rename restores draft semantics and whitespace");
  check(rename.rewriteDraft(d,main,"valore + doppio(7)",false).equals("valore + triplo(7)"),"main draft calls update without renaming main locals");
  Function other=FlowFunctionEditing.create(d);d.functions.add(other);Node op=new Node(PROCESS,"x = 1",0,0);other.body.nodes.add(op);
  check(rename.rewriteDraft(d,op.id,"risultato = doppio(valore)",false).equals("risultato = triplo(valore)"),"other function keeps its locals");
  check(rename.rewriteDraft(d,"removed","doppio(valore)",false).equals("doppio(valore)"),"orphan draft kept for unrelated history");
  check(rename.rewriteDraft(d,before.body.nodes.get(0).id,"doppio(valore)",false).equals("doppio(valore)"),"start label is not code");
  check(rename.rewriteDraft(d,local,"finale = dato + 99",true).equals("risultato = valore + 99"),"undo preserves newer text typed after rename");
  reject(()->rename.rewriteDraft(d,local,"risultato = valore + dato",false),"destination local in draft blocks merging");
  reject(()->rename.rewriteDraft(d,local,"finale = dato + valore",true),"undo destination collision blocks merging");
  reject(()->rename.rewriteDraft(d,main,"doppio(1) + triplo(2)",false),"destination call already in draft blocks merging");
  reject(()->rename.rewriteDraft(d,main,"triplo(1) + doppio(2)",true),"reverse call collision blocks undo");
  Map<String,String> duplicate=new HashMap<>();duplicate.put("a","c");duplicate.put("b","C");reject(()->new FlowFunctionEditing.Rename(before,after,duplicate),"non-reversible rename rejected");
  Map<String,String> swap=new HashMap<>();swap.put("a","b");swap.put("b","a");FlowFunctionEditing.Rename swapped=new FlowFunctionEditing.Rename(before,before,swap);
  check(swapped.rewriteDraft(d,local,"a + b",false).equals("b + a"),"simultaneous swap preserves both names");check(swapped.rewriteDraft(d,local,"b + a",true).equals("a + b"),"simultaneous swap reverses");
  Function same=before.copy();same.name="DOPPIO";Map<String,String> casing=Collections.singletonMap("valore","VALORE");FlowFunctionEditing.Rename caseRename=new FlowFunctionEditing.Rename(before,same,casing);
  check(caseRename.rewriteDraft(d,local,"doppio(valore)",false).equals("DOPPIO(VALORE)"),"case-only change is not a collision");
  Node input=new Node(INPUT,"valore",0,0);before.body.nodes.add(input);FlowFunctionEditing.Rename command=new FlowFunctionEditing.Rename(before,after,Collections.singletonMap("leggi","dato"));
  check(command.rewriteDraft(d,input.id,"  LeGgI\tleggi",false).equals("  LeGgI\tdato"),"I/O prefix survives a same-named parameter");
  Node output=new Node(OUTPUT,"valore",0,0);before.body.nodes.add(output);
  check(rename.rewriteDraft(d,output.id,"stampa doppio(valore)",false).equals("stampa triplo(dato)"),"output editor syntax and expression preserved");
  StringBuilder big=new StringBuilder();for(int i=0;i<1365;i++)big.append("valore"); // one long token stays untouched
  check(rename.rewriteDraft(d,local,big.toString(),false).equals(big.toString()),"partial identifiers do not rename");
  Map<String,String> longMap=Collections.singletonMap("a","lunghissimo");FlowFunctionEditing.Rename grows=new FlowFunctionEditing.Rename(before,before,longMap);StringBuilder repeated=new StringBuilder();for(int i=0;i<1000;i++)repeated.append("a+");
  reject(()->grows.rewriteDraft(d,local,repeated.toString(),false),"oversized rewritten draft rejected");
 }
 public static void main(String[] args){
  drafts();
  Document original=fixture();String unchanged=FlowCodec.encode(original);Function f=original.functions.get(0);String id=f.id;
  check(original.totalNodes()==6&&original.totalEdges()==4,"whole project capacity includes functions");
  check(FlowFunctionEditing.graph(original,id)==f.body,"active graph belongs to project");check(FlowFunctionEditing.graph(original,null)==original,"main is root");
  check(FlowFunctionEditing.ownerOf(original,f.body.nodes.get(1).id).equals(id),"draft node resolves function owner");
  Function edited=f.copy();edited.name="moltiplica";edited.parameters.get(0).name="dato";edited.returnVariable="finale";
  Map<String,String> names=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);names.put("valore","dato");names.put("risultato","finale");
  Document saved=FlowFunctionEditing.save(original,edited,names);check(FlowCodec.encode(original).equals(unchanged),"successful edit leaves original untouched");
  check(process(saved.functions.get(0).body).text.equals("finale = dato * 2"),"parameter and return uses rewritten together");check(run(saved).equals("[8]"),"actual caller updated after rename");
  check(run(FlowCodec.decode(FlowCodec.encode(saved))).equals("[8]"),"save/reload preserves main and function body");
  check(FlowFunctionEditing.rewriteCalls("doppio(2) + DOPPIO (3) + doppio + \"doppio(4)\"", "doppio","triplo").equals("triplo(2) + triplo (3) + doppio + \"doppio(4)\""),"call rename is token-aware and quote-preserving");
  Map<String,String> lexical=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);lexical.put("e","altro");lexical.put("a","b");lexical.put("b","a");
  check(FlowFunctionEditing.rewrite("a + b + 1e3 + e + a(2) + \"a\\\"b\"",lexical,false).equals("b + a + 1e3 + altro + a(2) + \"a\\\"b\""),"simultaneous rename preserves exponent and function identifiers");
  Document collisions=fixture();process(collisions.functions.get(0).body).text="locale = valore + 1\nrisultato = locale * 2";Function colliding=collisions.functions.get(0).copy();colliding.parameters.get(0).name="locale";
  Map<String,String> conflict=Collections.singletonMap("valore","locale");String exact=FlowCodec.encode(collisions);reject(()->FlowFunctionEditing.save(collisions,colliding,conflict),"local merge rejected");check(FlowCodec.encode(collisions).equals(exact),"failed rename preserves all nodes");
  for(String invalid:new String[]{"sin","MAIN","doppio()","", "name with spaces"}){Function bad=f.copy();bad.name=invalid;reject(()->FlowFunctionEditing.save(original,bad,null),"invalid/reserved signature rejected "+invalid);}
  Function duplicate=f.copy();duplicate.id=FlowModel.id();reject(()->FlowFunctionEditing.save(original,duplicate,null),"duplicate name or IDs rejected");
  Function badParameter=f.copy();badParameter.parameters.add(badParameter.parameters.get(0).copy());reject(()->FlowFunctionEditing.save(original,badParameter,null),"duplicate parameter rejected");
  Function noReturn=f.copy();noReturn.returnVariable="";reject(()->FlowFunctionEditing.save(original,noReturn,null),"missing result rejected");
  reject(()->FlowFunctionEditing.delete(original,id),"used function cannot be deleted");check(FlowCodec.encode(original).equals(unchanged),"rejected delete does not mutate source");
  Document unused=fixture();unused.nodes.get(1).text="\"doppio(4)\"";process(unused.functions.get(0).body).text="risultato = doppio(valore)";
  Document removed=FlowFunctionEditing.delete(unused,unused.functions.get(0).id);check(removed.functions==null&&removed.version==1,"unused recursive body removable; format downgraded");check(removed.nodes.size()==3,"main remains after delete");
  FlowFunctionEditing.History history=new FlowFunctionEditing.History(saved,id);process(saved.functions.get(0).body).text="finale = 77";check(run(history.project).equals("[8]"),"history deeply snapshots all graphs");check(history.functionId.equals(id),"history remembers active function");
  FlowFunctionEditing.History main=new FlowFunctionEditing.History(original,"missing");check(main.functionId==null,"stale active ID falls back to main");
  Function newFunction=FlowFunctionEditing.create(original);Document expanded=FlowFunctionEditing.save(original,newFunction,null);check(expanded.functions.size()==2&&expanded.totalNodes()==8,"new function starts with independent start/end");check(!newFunction.body.nodes.get(0).id.equals(f.body.nodes.get(0).id),"new function gets independent IDs");
  Function next=FlowFunctionEditing.create(expanded);check(!next.name.equals(newFunction.name),"new function receives unique name");
  for(int n=expanded.totalNodes();n<MAX_NODES;n++)expanded.nodes.add(new Node(PROCESS,"x = 1",0,0));check(expanded.totalNodes()==MAX_NODES,"global full capacity");reject(()->FlowFunctionEditing.save(expanded,next,null),"creating function cannot exceed project capacity");
  check(FlowCodec.encode(original).equals(unchanged),"all rejection paths preserve original project");
  Document graph=new Document();Node a=new Node(START,"Inizio",0,0),b=new Node(END,"Fine",0,200),c=new Node(CALL,"miaFunzione()",200,100);graph.nodes.add(a);graph.nodes.add(b);graph.nodes.add(c);Edge edge=new Edge(a.id,b.id,"next");graph.edges.add(edge);
  check(!FlowInsertion.apply(graph,c.id,edge.id,"next",MAX_EDGES-1),"insertion counts edges reserved by other graphs");check(graph.edges.size()==1&&edge.to.equals(b.id),"overcapacity splice leaves connections intact");check(FlowInsertion.apply(graph,c.id,edge.id,"next",MAX_EDGES-2),"call inserts into available capacity");check(graph.outgoing(c.id,"next").to.equals(b.id),"call insertion preserves continuation");
  check(FlowModel.kindName(CALL).equals("Chiamata"),"call is named in native selection");
  System.out.println("FUNCTION_EDITING PASS checks="+checks);
 }
}
