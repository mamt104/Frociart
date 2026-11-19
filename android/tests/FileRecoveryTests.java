package it.frociart.app;
import java.io.*;import java.nio.file.*;import java.nio.charset.*;import java.util.*;
public class FileRecoveryTests {
 static Path root;static String current;static int checks;
 static void eq(Object expected,Object actual){checks++;if(!Objects.equals(expected,actual))throw new AssertionError(current+": expected ["+expected+"] actual ["+actual+"]");}
 interface Action {void run()throws Exception;}
 static void fails(Action action)throws Exception{boolean failed=false;try{action.run();}catch(IOException|FlowEngine.Problem ex){failed=true;}eq(true,failed);}
 static FlowFileRecovery store(String name){current=name;return new FlowFileRecovery(root.resolve(name).toFile());}
 static FlowFileRecovery.Entry one(FlowFileRecovery store)throws Exception{eq(1,store.list().size());return store.list().get(0);}
 static void basic()throws Exception{
  FlowFileRecovery store=store("basic");eq(0,store.list().size());FlowTextFiles.Journal journal=store.create("../nome è 🦊.txt");String id=journal.id();eq(32,id.length());journal.append("  primo\r\n");journal.append("\n日本語🦊");FlowFileRecovery.Entry entry=one(store);eq("../nome è 🦊.txt",entry.name);eq(id,entry.id());FlowFileRecovery.Content content=store.load(entry);eq("  primo\r\n\n日本語🦊",content.text);eq(false,content.incomplete);journal.close();
  FlowFileRecovery reopened=new FlowFileRecovery(root.resolve("basic").toFile());eq(content.text,reopened.load(one(reopened)).text);fails(()->new FlowFileRecovery(root.resolve("other").toFile()).load(entry));fails(()->new FlowFileRecovery(root.resolve("other").toFile()).delete(entry));eq(true,entry.file.exists());
  journal=store.create("empty.txt");journal.close();eq("",store.load(store.list().stream().filter(e->e.name.equals("empty.txt")).findFirst().get()).text);journal=store.create("saved.txt");journal.append("kept externally");eq(true,journal.saved());eq(false,store.list().stream().anyMatch(e->e.name.equals("saved.txt")));store.delete(entry);eq(false,entry.file.exists());
 }
 static void damage()throws Exception{
  FlowFileRecovery source=store("damage-seed");FlowTextFiles.Journal journal=source.create("original.txt");journal.append("verified\n");journal.close();byte[] original=Files.readAllBytes(one(source).file.toPath());
  for(byte[] tail:new byte[][]{new byte[]{1},new byte[]{1,0,0,0},new byte[37],new byte[]{-1,-1,-1,-1}}){FlowFileRecovery store=store("tail-"+tail.length+"-"+tail[0]);Path dir=root.resolve(current);Files.createDirectories(dir);byte[] bytes=Arrays.copyOf(original,original.length+tail.length);System.arraycopy(tail,0,bytes,original.length,tail.length);Files.write(dir.resolve(UUID.randomUUID().toString().replace("-","")+".fiodraft"),bytes);FlowFileRecovery.Content c=store.load(one(store));eq("verified\n",c.text);eq(true,c.incomplete);}
  FlowFileRecovery broken=store("broken-frame");journal=broken.create("name.txt");journal.append("first");journal.append("second");journal.append("third");journal.close();FlowFileRecovery.Entry entry=one(broken);byte[] data=Files.readAllBytes(entry.file.toPath());int second=8+36+8+36+5;data[second+4]^=0x01;Files.write(entry.file.toPath(),data);eq("first",broken.load(entry).text);eq(true,broken.load(entry).incomplete);
  FlowFileRecovery header=store("bad-header");Files.createDirectories(root.resolve(current));Files.write(root.resolve(current).resolve(UUID.randomUUID().toString().replace("-","")+".fiodraft"),new byte[]{1,2,3});FlowFileRecovery.Entry bad=one(header);eq("File recuperato",bad.name);fails(()->header.load(bad));header.delete(bad);eq(0,header.list().size());
 }
 static void capacity()throws Exception{
  FlowFileRecovery store=store("large");FlowTextFiles.Journal journal=store.create("large.txt");char[] chars=new char[200000];Arrays.fill(chars,'è');String chunk=new String(chars);journal.append(chunk);journal.close();eq(chunk,store.load(one(store)).text);
  store=store("many-writes");journal=store.create("many.txt");long started=System.nanoTime();for(int i=0;i<10000;i++)journal.append("x");journal.close();long ms=(System.nanoTime()-started)/1000000;FlowFileRecovery.Entry entry=one(store);eq(10000,store.load(entry).text.length());eq(370052L,entry.file.length());System.out.println("journal 10000 tiny writes ms="+ms);
  final FlowFileRecovery quota=store("quota");for(int i=0;i<50;i++){journal=quota.create(i+".txt");journal.close();}eq(50,quota.list().size());fails(()->quota.create("overflow"));quota.delete(quota.list().get(0));journal=quota.create("after-removal");journal.close();eq(50,quota.list().size());
  Path path=root.resolve("not-a-folder");Files.write(path,"original".getBytes(StandardCharsets.UTF_8));FlowFileRecovery blocked=new FlowFileRecovery(path.toFile());fails(()->blocked.create("x"));fails(()->blocked.list());eq("original",new String(Files.readAllBytes(path),StandardCharsets.UTF_8));
 }
 static class FailingJournal implements FlowTextFiles.Journal {
  final FlowTextFiles.Journal inner;int writes;FailingJournal(FlowTextFiles.Journal inner){this.inner=inner;}public String id(){return inner.id();}
  public void append(String text)throws IOException{if(++writes==2)throw new IOException("injected disk failure");inner.append(text);}public void close()throws IOException{inner.close();}public boolean saved(){return inner.saved();}
 }
 static FlowEngine.Runner runner(FlowFileRecovery store,String code){FlowEngine.Runner runner=new FlowEngine.Runner(FlowPseudocode.decode(code));runner.files.journalFactory=store::create;return runner;}
 static void drive(FlowEngine.Runner runner){for(int i=0;i<100&&runner.state!=FlowEngine.State.ERROR&&runner.state!=FlowEngine.State.COMPLETED&&runner.state!=FlowEngine.State.WAITING_FILE;i++)runner.step();}
 static void lifecycle()throws Exception{
  FlowFileRecovery store=store("reset");FlowEngine.Runner runner=runner(store,"APRI_FILE SCRITTURA \"result.txt\"\nSCRIVI_FILE \"recupera\"");drive(runner);eq(FlowEngine.State.WAITING_FILE,runner.state);FlowTextFiles.Request pending=runner.pendingFile();eq("recupera\n",store.load(one(store)).text);FlowEngine.Runner retained=runner;fails(()->retained.reset());eq(pending,runner.pendingFile());runner.reset(true);eq(false,runner.provideFile(pending,null));eq("recupera\n",new FlowFileRecovery(root.resolve("reset").toFile()).load(one(store)).text);
  store=store("cancel-save");runner=runner(store,"APRI_FILE SCRITTURA \"result.txt\"\nSCRIVI_FILE 7");drive(runner);pending=runner.pendingFile();runner.fileDeferred(pending,null);eq(1,store.list().size());eq("7\n",runner.files.outputDraft());eq(true,runner.provideFile(pending,null));eq(0,store.list().size());eq(false,runner.files.hasWrite());
  final FlowFileRecovery failed=store("write-failure");runner=runner(failed,"APRI_FILE SCRITTURA \"result.txt\"\nSCRIVI_FILE \"first\"\nSCRIVI_FILE \"second\"");runner.files.journalFactory=name->new FailingJournal(failed.create(name));drive(runner);eq(FlowEngine.State.ERROR,runner.state);eq("first\n",runner.files.outputDraft());eq("first\n",failed.load(one(failed)).text);runner.files.closeJournal();eq("first\n",failed.load(one(failed)).text);
  store=store("bad-root");runner=runner(store,"APRI_FILE SCRITTURA \"result.txt\"");runner.files.journalFactory=name->{throw new IOException("injected");};drive(runner);eq(FlowEngine.State.ERROR,runner.state);eq(false,runner.files.hasWrite());eq(0,store.list().size());
 }
 public static void main(String[] args)throws Exception{root=Paths.get(args[0]).resolve("java-"+UUID.randomUUID().toString());Files.createDirectories(root);basic();damage();capacity();lifecycle();System.out.println("FileRecoveryTests PASS "+checks+" assertions; fixtures="+root);}
}
