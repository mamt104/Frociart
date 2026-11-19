package it.frociart.app;
import java.util.*;import java.nio.file.*;import java.nio.charset.*;import java.io.*;
import static it.frociart.app.FlowModel.*;
public class FileIoTests {
 static int checks;static String current;static Path root;
 static void eq(Object expected,Object actual){checks++;if(!Objects.equals(expected,actual))throw new AssertionError(current+": expected ["+expected+"] actual ["+actual+"]");}
 static String decode(String s){return new String(Base64.getDecoder().decode(s),StandardCharsets.UTF_8);}
 interface Action {void run()throws Exception;}
 static void rejects(Action a)throws Exception{boolean rejected=false;try{a.run();}catch(IllegalArgumentException ex){rejected=true;}eq(true,rejected);}
 static FlowEngine.Runner untilFile(String source){FlowEngine.Runner r=new FlowEngine.Runner(FlowPseudocode.decode(source));for(int i=0;i<100&&r.state!=FlowEngine.State.WAITING_FILE&&r.state!=FlowEngine.State.ERROR;i++)r.step();eq(FlowEngine.State.WAITING_FILE,r.state);return r;}
 static void shared()throws Exception{
 for(String row:Files.readAllLines(root.resolve("cases.tsv"),StandardCharsets.UTF_8)){String[] f=row.split("\t",-1);current=f[0];Document doc=FlowPseudocode.decode(decode(f[1]));Files.createDirectories(root.resolve("java-files"));Files.write(root.resolve("java-files").resolve(current+".frociart"),FlowCodec.encode(doc).getBytes(StandardCharsets.UTF_8));Path other=root.resolve("pc-files").resolve(current+".frociart");
  for(int pass=0;pass<(Files.exists(other)?4:3);pass++){
   if(pass==1)doc=FlowPseudocode.decode(FlowPseudocode.encode(doc));if(pass==2)doc=FlowCodec.decode(FlowCodec.encode(doc));if(pass==3)doc=FlowCodec.decode(new String(Files.readAllBytes(other),StandardCharsets.UTF_8));
   FlowEngine.Runner r=new FlowEngine.Runner(doc);String[] reads=decode(f[2]).split("\u001e",-1);int read=0;ArrayList<String> saved=new ArrayList<>();
   for(int i=0;i<20000&&r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR;i++){
    if(r.state==FlowEngine.State.WAITING_FILE){FlowTextFiles.Request request=r.pendingFile();int steps=r.steps;eq(false,r.step());eq(steps,r.steps);
     if(request.kind==FlowTextFiles.Kind.READ){eq(true,read<Integer.parseInt(f[3]));Path path=root.resolve("java-read-fixture.txt");Files.write(path,reads[read++].getBytes(StandardCharsets.UTF_8));try(InputStream stream=Files.newInputStream(path)){eq(true,r.provideFile(request,FlowTextFiles.readText(stream)));}}
     else {Path path=root.resolve("java-write-result.txt");try(OutputStream stream=Files.newOutputStream(path)){FlowTextFiles.writeText(stream,request.text);}saved.add(new String(Files.readAllBytes(path),StandardCharsets.UTF_8));eq(true,r.provideFile(request,null));}
     eq(false,r.provideFile(request,"stale"));
    }else r.step();
   }
   String error=decode(f[7]);eq(error.isEmpty()?FlowEngine.State.COMPLETED:FlowEngine.State.ERROR,r.state);if(!error.isEmpty()){if(r.errorMessage==null||!r.errorMessage.toLowerCase(Locale.ROOT).contains(error.toLowerCase(Locale.ROOT)))throw new AssertionError(current+": error mismatch: "+r.errorMessage);checks++;}
   eq(decode(f[4]),String.join("|",r.output));eq(Integer.parseInt(f[6]),saved.size());eq(decode(f[5]),String.join("\u001e",saved));eq(Integer.parseInt(f[3]),read);
  }
 }
 }
 static void states()throws Exception{
 current="cancel-retry-stale";FlowEngine.Runner r=untilFile("APRI_FILE LETTURA \"x\"\nLEGGI_FILE x\nSTAMPA x");FlowTextFiles.Request request=r.pendingFile();eq(true,r.fileDeferred(request,null));eq(request,r.pendingFile());eq(false,r.provideFile(request,null));eq(request,r.pendingFile());eq(true,r.provideFile(request,"8"));eq(false,r.provideFile(request,"9"));while(r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR)r.step();eq("8",String.join("|",r.output));
 r=untilFile("APRI_FILE LETTURA \"x\"\nLEGGI_FILE x");request=r.pendingFile();r.reset();eq(false,r.provideFile(request,"late"));eq(false,r.fileDeferred(request,"late"));eq(FlowEngine.State.READY,r.state);
 current="write-retry-retains-draft";r=untilFile("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE \"once\"\nCHIUDI_FILE SCRITTURA");request=r.pendingFile();eq("once\n",r.files.outputDraft());for(int i=0;i<5;i++){eq(true,r.fileDeferred(request,"Disco pieno"));eq(false,r.step());eq("once\n",r.files.outputDraft());}final FlowEngine.Runner preserve=r;rejects(preserve::reset);eq(request,r.pendingFile());eq(true,r.provideFile(request,null));eq(false,r.files.hasWrite());
 current="reset-explicit-discard";r=untilFile("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE 1");request=r.pendingFile();r.reset(true);eq(false,r.provideFile(request,null));eq(false,r.files.hasWrite());
 current="bad-read-no-commit";r=untilFile("DICHIARA n: INTERO\nAPRI_FILE LETTURA \"x\"\nLEGGI_FILE n");r.provideFile(r.pendingFile(),"bad\n12");r.step();eq(FlowEngine.State.ERROR,r.state);eq("0",r.variables.get("n").format());eq("bad",r.files.peekLine());
 current="execution-failure-retains-draft";r=new FlowEngine.Runner(FlowPseudocode.decode("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE \"recover\"\nx=1/0"));while(r.state!=FlowEngine.State.ERROR)r.step();eq("recover\n",r.files.outputDraft());final FlowEngine.Runner failed=r;rejects(failed::reset);
 current="invalid-syntax";for(String s:new String[]{"APRI_FILE unknown \"x\"","CHIUDI_FILE","CHIUDI_FILE x","SCRIVI_FILE SENZA_A_CAPO","STAMPA EOF(1)","LEGGI_FILE x+1"})rejects(()->FlowPseudocode.decode(s));
 }
 static class BrokenWrite extends OutputStream {public void write(int b)throws IOException{throw new IOException("injected write failure");}}
 static void streams()throws Exception{
 current="encoding";String text=" 007 \r\n🦊 è 日本語\n";Charset[] encodings={StandardCharsets.UTF_8,StandardCharsets.UTF_8,StandardCharsets.UTF_16LE,StandardCharsets.UTF_16BE};byte[][] marks={{},{(byte)239,(byte)187,(byte)191},{(byte)255,(byte)254},{(byte)254,(byte)255}};
 for(int i=0;i<encodings.length;i++){ByteArrayOutputStream bytes=new ByteArrayOutputStream();bytes.write(marks[i]);bytes.write(text.getBytes(encodings[i]));eq(text,FlowTextFiles.readText(new ByteArrayInputStream(bytes.toByteArray())));}
 for(byte[] bad:new byte[][]{{(byte)0xc0,(byte)0xaf},{(byte)0xff,(byte)0xfe,0},{(byte)0xef,(byte)0xbb,(byte)0xbf,(byte)0xed,(byte)0xa0,(byte)0x80}})rejects(()->FlowTextFiles.readText(new ByteArrayInputStream(bad)));
 rejects(()->FlowTextFiles.readText(new ByteArrayInputStream(new byte[FlowTextFiles.MAX_BYTES+1])));
 FlowEngine.Runner r=untilFile("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE \"safe\"");boolean failed=false;try(OutputStream stream=new BrokenWrite()){try{FlowTextFiles.writeText(stream,r.pendingFile().text);}catch(IOException ex){failed=true;}}eq(true,failed);eq("safe\n",r.files.outputDraft());eq(FlowEngine.State.WAITING_FILE,r.state);
 current="bounds";FlowTextFiles files=new FlowTextFiles();char[] maxLine=new char[FlowTextFiles.MAX_LINE];Arrays.fill(maxLine,'x');files.openRead("x",new String(maxLine));eq(FlowTextFiles.MAX_LINE,files.peekLine().length());files.commitRead();eq(true,files.eof());files.closeRead();rejects(()->files.openRead("x",new String(new char[FlowTextFiles.MAX_CHARACTERS+1])));rejects(()->files.openRead("x","\ud800"));files.openWrite("x");rejects(()->files.write("\ud800",false));eq("",files.outputDraft());String chunk=new String(maxLine);for(int i=0;i<64;i++)files.write(chunk,false);eq(FlowTextFiles.MAX_CHARACTERS,files.outputDraft().length());rejects(()->files.write("x",false));eq(FlowTextFiles.MAX_CHARACTERS,files.outputDraft().length());
 }
 static void debugger(){
 current="debugger-file-into";Document doc=FlowPseudocode.decode("APRI_FILE LETTURA \"x\"\nLEGGI_FILE x\nSTAMPA x");FlowEngine.Runner r=new FlowEngine.Runner(doc);r.step();FlowDebugger d=new FlowDebugger(r,doc);d.begin(FlowDebugger.Command.INTO);d.tick();eq(FlowEngine.State.WAITING_FILE,r.state);eq(true,d.active());eq(false,d.tick());r.provideFile(r.pendingFile(),"3");d.inputProvided();eq(false,d.active());eq(READ_FILE,r.currentDiagram().find(r.currentNodeId).kind);
 current="debugger-eof-breakpoint";for(Node n:doc.nodes)if(n.kind==READ_FILE){n.breakpoint=true;n.breakpointCondition="non EOF()";}d.updateBreakpoints(doc);d.begin(FlowDebugger.Command.CONTINUE);d.tick();eq(true,d.atBreakpoint());eq("3",r.files.peekLine());d.begin(FlowDebugger.Command.CONTINUE);for(int i=0;i<10&&d.active();i++)d.tick();eq(FlowEngine.State.COMPLETED,r.state);eq("3",String.join("|",r.output));
 current="debugger-file-over-function";doc=FlowPseudocode.decode("FUNZIONE apri()\nAPRI_FILE LETTURA \"x\"\nFINE FUNZIONE\nCHIAMA apri()\nLEGGI_FILE x");r=new FlowEngine.Runner(doc);r.step();d=new FlowDebugger(r,doc);d.begin(FlowDebugger.Command.OVER);for(int i=0;i<20&&r.state!=FlowEngine.State.WAITING_FILE;i++)d.tick();eq(FlowEngine.State.WAITING_FILE,r.state);eq(2,r.callDepth());r.provideFile(r.pendingFile(),"9");d.inputProvided();eq(true,d.active());for(int i=0;i<20&&d.active();i++)d.tick();eq(1,r.callDepth());eq(false,d.active());eq("9",r.files.peekLine());
 }
 static void rename(){
 current="rename-protects-channel";Map<String,String> names=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);names.put("lettura","sorgente");names.put("scrittura","destinazione");names.put("SENZA_A_CAPO","valore");Node node=new Node();node.kind=OPEN_FILE;eq("  LETTURA sorgente",FlowDeclaration.rewriteNode(node,"  LETTURA lettura",names));node.kind=CLOSE_FILE;eq("SCRITTURA",FlowDeclaration.rewriteNode(node,"SCRITTURA",names));node.kind=WRITE_FILE;eq("SENZA_A_CAPO destinazione",FlowDeclaration.rewriteNode(node,"SENZA_A_CAPO scrittura",names));
 Document doc=FlowPseudocode.decode("FUNZIONE apri(lettura: TESTO)\nAPRI_FILE LETTURA lettura\nFINE FUNZIONE\nCHIAMA apri(\"x\")\nLEGGI_FILE x");Function changed=doc.functions.get(0).copy();changed.parameters.get(0).name="sorgente";Map<String,String> rename=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);rename.put("lettura","sorgente");Document saved=FlowFunctionEditing.save(doc,changed,rename);for(Node n:saved.functions.get(0).body.nodes)if(n.kind==OPEN_FILE)eq("LETTURA sorgente",n.text);eq(FlowEngine.State.READY,new FlowEngine.Runner(saved).state);
 }
 public static void main(String[] args)throws Exception{root=Paths.get(args[0]).toAbsolutePath();shared();states();streams();debugger();rename();System.out.println("FileIoTests PASS "+checks+" assertions");}
}
