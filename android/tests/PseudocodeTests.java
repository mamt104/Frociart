package it.frociart.app;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import static it.frociart.app.FlowModel.*;
public final class PseudocodeTests {
    static int checks;static Path folder,out;
    static void check(boolean value,String message){checks++;if(!value)throw new AssertionError(message);}
    static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8).replace("\r\n","\n");}
    static String run(Document d,String input){FlowEngine.Runner r=new FlowEngine.Runner(d);String[] values=input.split("\n",-1);int i=0;
        for(int step=0;step<11000&&r.state!=FlowEngine.State.ERROR&&r.state!=FlowEngine.State.COMPLETED;step++){if(r.state==FlowEngine.State.WAITING){check(i<values.length,"unexpected input");check(r.submitInput(values[i++]),"input accepted");}else r.step();}
        check(r.state==FlowEngine.State.COMPLETED,"execution completed: "+r.errorMessage);return String.join("\n",r.output);
    }
    static void expectError(String text,int line){try{FlowPseudocode.decode(text);throw new AssertionError("accepted invalid input");}catch(FlowPseudocode.ParseError e){check(e.line==line,"error line "+e.line+" expected "+line+": "+e.getMessage());}}
    public static void main(String[] args)throws Exception{
        folder=Paths.get(args[0]);out=Paths.get(args[1]);Files.createDirectories(out);
        ArrayList<Path> cases=new ArrayList<>();try(java.util.stream.Stream<Path> stream=Files.list(folder)){stream.filter(p->p.toString().endsWith(".pseudo")).sorted().forEach(cases::add);}
        for(Path file:cases){String name=file.getFileName().toString().replace(".pseudo",""),input=read(folder.resolve(name+".inputs")),expected=read(folder.resolve(name+".expected"));Document d=FlowPseudocode.decode(read(file));String before=FlowCodec.encode(d),code=FlowPseudocode.encode(d);Document restored=FlowPseudocode.decode(code);
            check(run(d,input).equals(expected),name+" original output");check(run(restored,input).equals(expected),name+" roundtrip output");check(before.equals(FlowCodec.encode(d)),name+" original unchanged");
            check(d.nodes.size()==restored.nodes.size(),name+" node count "+d.nodes.size()+" / "+restored.nodes.size());check(d.edges.size()==restored.edges.size(),name+" edge count");
            if(name.equals("while"))check(code.contains("MENTRE "),"readable while");if(name.equals("positive"))check(code.contains("SE numero"),"readable branch");
            if(name.equals("prompt"))check(restored.nodes.stream().anyMatch(n->n.prompt.equals("Valore di n?")),"prompt preserved");
            Files.write(out.resolve(name+".txt"),code.getBytes(StandardCharsets.UTF_8));Files.write(out.resolve(name+".frociart"),FlowCodec.encode(d).getBytes(StandardCharsets.UTF_8));
        }
        // Open ports and disconnected nodes must never become an implicit connection.
        Document open=FlowPseudocode.decode("x=1\nSE x>0 ALLORA\nSCOLLEGATO\nALTRIMENTI\nSCRIVI x\nFINE SE");String code=FlowPseudocode.encode(open);check(code.contains("SCOLLEGATO"),"open port exported");Document again=FlowPseudocode.decode(code);check(again.edges.size()==open.edges.size(),"missing branch retained");
        Document loop=FlowPseudocode.decode("x=1\ncheck:\nSE x>0 ALLORA\nVAI A check\nALTRIMENTI\nFERMA\nFINE SE");Document same=FlowPseudocode.decode(FlowPseudocode.encode(loop));check(same.edges.stream().anyMatch(e->e.from.equals(e.to)),"self loop retained");
        expectError("",1);expectError("INIZIO\nxyz\nFINE",2);expectError("SE vero ALLORA\nx=1",1);expectError("MENTRE vero FAI\nFINE",2);expectError("VAI A manca",1);expectError("a:\nx=1\na:\nx=2",3);expectError("a:\nVAI A a",2);expectError("RIPETI\nx=1",1);expectError("ALTRIMENTI",1);expectError("x=System.exit(0)",1);expectError("SCRIVI unknown(2)",1);expectError("ultimo:",1);
        expectError(String.join("",Collections.nCopies(FlowPseudocode.MAX_TEXT+1,"x")),1);
        expectError(String.join("\n",Collections.nCopies(501,"x=1")),500);
        expectError(String.join("",Collections.nCopies(49,"SE vero ALLORA\n"))+"x=1\n"+String.join("",Collections.nCopies(49,"FINE SE\n")),50);
        fuzz();System.out.println("PASS PseudocodeTests: "+checks+" assertions; "+cases.size()+" execution fixtures and 300 random graphs.");
    }
    static void fuzz(){Random random=new Random(41021);for(int trial=0;trial<300;trial++){
        Document d=new Document();Node start=new Node(START,"Inizio",0,0),init=new Node(PROCESS,"x=0",0,0),end=new Node(END,"Fine",0,0);d.nodes.add(start);d.nodes.add(init);
        for(int i=0;i<8;i++)d.nodes.add(new Node(i%3==0?DECISION:PROCESS,i%3==0?"x < "+(2+random.nextInt(8)):"x=x+1",0,0));d.nodes.add(end);d.edges.add(new Edge(start.id,init.id,"next"));d.edges.add(new Edge(init.id,d.nodes.get(2).id,"next"));
        for(int i=2;i<d.nodes.size()-1;i++){Node n=d.nodes.get(i);if(n.kind==DECISION){d.edges.add(new Edge(n.id,d.nodes.get(2+random.nextInt(d.nodes.size()-2)).id,"true"));d.edges.add(new Edge(n.id,d.nodes.get(i+1).id,"false"));}else d.edges.add(new Edge(n.id,d.nodes.get(i+1).id,"next"));}
        Document r=FlowPseudocode.decode(FlowPseudocode.encode(d));FlowEngine.Runner a=new FlowEngine.Runner(d),b=new FlowEngine.Runner(r);
        for(int step=0;step<10001;step++){check(a.state==b.state,"fuzz state "+trial+"/"+step);check(a.variables.toString().equals(b.variables.toString()),"fuzz variables "+trial+"/"+step);if(a.state==FlowEngine.State.ERROR||a.state==FlowEngine.State.COMPLETED)break;a.step();b.step();}
        check(d.nodes.size()==r.nodes.size(),"fuzz nodes retained "+trial);check(d.edges.size()==r.edges.size(),"fuzz edges retained "+trial);
    }}
}
