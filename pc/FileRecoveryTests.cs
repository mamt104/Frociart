using System;using System.IO;using System.Linq;using System.Text;using System.Diagnostics;
namespace Frociart.Tests {
public static class FileRecoveryTests {
 static string root,current;static int checks;
 static void eq(object expected,object actual){checks++;if(!Object.Equals(expected,actual))throw new Exception(current+": expected ["+expected+"] actual ["+actual+"]");}
 static void fails(Action action){bool failed=false;try{action();}catch(IOException){failed=true;}catch(InvalidDataException){failed=true;}catch(FlowException){failed=true;}eq(true,failed);}
 static FlowFileRecovery Store(string name){current=name;return new FlowFileRecovery(Path.Combine(root,name));}
 static string PathOf(string group,FlowFileRecovery.Entry entry){return Path.Combine(root,group,entry.Id+".fiodraft");}
 static void Basic(){
  var store=Store("basic");eq(0,store.List().Count);var file=store.Create("../nome è 🦊.txt");string id=file.Id;eq(32,id.Length);file.Append("  primo\r\n");file.Append("\n日本語🦊");var entry=store.List().Single();eq("../nome è 🦊.txt",entry.Name);eq(id,entry.Id);var content=store.Load(entry);eq("  primo\r\n\n日本語🦊",content.Text);eq(false,content.Incomplete);file.Close();
  store=new FlowFileRecovery(Path.Combine(root,"basic"));eq(content.Text,store.Load(store.List().Single()).Text);fails(()=>new FlowFileRecovery(Path.Combine(root,"other")).Load(entry));fails(()=>new FlowFileRecovery(Path.Combine(root,"other")).Delete(entry));eq(true,File.Exists(PathOf("basic",entry)));
  file=store.Create("empty.txt");file.Close();eq("",store.Load(store.List().Single(e=>e.Name=="empty.txt")).Text);var saved=store.Create("saved.txt");saved.Append("kept externally");eq(true,saved.Saved());eq(false,store.List().Any(e=>e.Name=="saved.txt"));store.Delete(entry);eq(false,File.Exists(PathOf("basic",entry)));
 }
 static void Damage(){
  var source=Store("damage-seed");var journal=source.Create("original.txt");journal.Append("verified\n");journal.Close();byte[] original=File.ReadAllBytes(PathOf("damage-seed",source.List().Single()));
  foreach(var tail in new[]{new byte[]{1},new byte[]{1,0,0,0},new byte[37],new byte[]{255,255,255,255}}){var store=Store("tail-"+tail.Length+"-"+tail[0]);string dir=Path.Combine(root,current);Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,Guid.NewGuid().ToString("N")+".fiodraft"),original.Concat(tail).ToArray());var c=store.Load(store.List().Single());eq("verified\n",c.Text);eq(true,c.Incomplete);}
  var broken=Store("broken-frame");journal=broken.Create("name.txt");journal.Append("first");journal.Append("second");journal.Append("third");journal.Close();var entry=broken.List().Single();var path=PathOf("broken-frame",entry);byte[] data=File.ReadAllBytes(path);int second=8+36+Encoding.UTF8.GetByteCount("name.txt")+36+5;data[second+4]^=0x01;File.WriteAllBytes(path,data);eq("first",broken.Load(entry).Text);eq(true,broken.Load(entry).Incomplete);
  var header=Store("bad-header");Directory.CreateDirectory(Path.Combine(root,current));File.WriteAllBytes(Path.Combine(root,current,Guid.NewGuid().ToString("N")+".fiodraft"),new byte[]{1,2,3});entry=header.List().Single();eq("File recuperato",entry.Name);fails(()=>header.Load(entry));header.Delete(entry);eq(0,header.List().Count);
 }
 static void Capacity(){
  var store=Store("large");var journal=store.Create("large.txt");string chunk=new string('è',200000);journal.Append(chunk);journal.Close();eq(chunk,store.Load(store.List().Single()).Text);
  var watch=Stopwatch.StartNew();store=Store("many-writes");journal=store.Create("many.txt");for(int i=0;i<10000;i++)journal.Append("x");journal.Close();watch.Stop();var entry=store.List().Single();eq(10000,store.Load(entry).Text.Length);eq(370052L,new FileInfo(PathOf("many-writes",entry)).Length);Console.WriteLine("journal 10000 tiny writes ms="+watch.ElapsedMilliseconds);
  store=Store("quota");for(int i=0;i<50;i++){journal=store.Create(i+".txt");journal.Close();}eq(50,store.List().Count);fails(()=>store.Create("overflow"));store.Delete(store.List()[0]);journal=store.Create("after-removal");journal.Close();eq(50,store.List().Count);
  string path=Path.Combine(root,"not-a-folder");File.WriteAllText(path,"original");var blocked=new FlowFileRecovery(path);fails(()=>blocked.Create("x"));eq("original",File.ReadAllText(path));
 }
 sealed class FailingJournal:FlowTextFiles.IJournal {
  readonly FlowTextFiles.IJournal inner;public FailingJournal(FlowTextFiles.IJournal inner){this.inner=inner;}public string Id{get{return inner.Id;}}int writes;
  public void Append(string text){if(++writes==2)throw new IOException("injected disk failure");inner.Append(text);}public void Close(){inner.Close();}public bool Saved(){return inner.Saved();}
 }
 static Interpreter Run(FlowFileRecovery store,string code){var r=new Interpreter(FlowPseudocode.Decode(code));r.Files.JournalFactory=store.Create;return r;}
 static void Drive(Interpreter runner){for(int i=0;i<100&&runner.State!=RuntimeState.Error&&runner.State!=RuntimeState.Completed&&runner.State!=RuntimeState.WaitingForFile;i++)runner.Step();}
 static void Lifecycle(){
  var store=Store("reset");var runner=Run(store,"APRI_FILE SCRITTURA \"result.txt\"\nSCRIVI_FILE \"recupera\"");Drive(runner);eq(RuntimeState.WaitingForFile,runner.State);var pending=runner.PendingFile;eq("recupera\n",store.Load(store.List().Single()).Text);fails(()=>runner.Reset());eq(pending,runner.PendingFile);runner.Reset(true);eq(false,runner.ProvideFile(pending));eq("recupera\n",new FlowFileRecovery(Path.Combine(root,"reset")).Load(store.List().Single()).Text);
  store=Store("cancel-save");runner=Run(store,"APRI_FILE SCRITTURA \"result.txt\"\nSCRIVI_FILE 7");Drive(runner);pending=runner.PendingFile;runner.FileDeferred(pending,null);eq(1,store.List().Count);eq("7\n",runner.Files.OutputDraft);eq(true,runner.ProvideFile(pending));eq(0,store.List().Count);eq(false,runner.Files.HasWrite);
  store=Store("write-failure");runner=Run(store,"APRI_FILE SCRITTURA \"result.txt\"\nSCRIVI_FILE \"first\"\nSCRIVI_FILE \"second\"");runner.Files.JournalFactory=name=>new FailingJournal(store.Create(name));Drive(runner);eq(RuntimeState.Error,runner.State);eq("first\n",runner.Files.OutputDraft);eq("first\n",store.Load(store.List().Single()).Text);runner.Files.CloseJournal();eq("first\n",store.Load(store.List().Single()).Text);
  store=Store("bad-root");runner=Run(store,"APRI_FILE SCRITTURA \"result.txt\"");runner.Files.JournalFactory=name=>{throw new IOException("injected");};Drive(runner);eq(RuntimeState.Error,runner.State);eq(false,runner.Files.HasWrite);eq(0,store.List().Count);
 }
 static void Storage(){
  current="atomic-storage";string path=Path.Combine(root,"export.txt");FlowFileStorage.Save(path,"first");eq("first",FlowFileStorage.Read(path));FlowFileStorage.Save(path,"second 🦊\n");eq("second 🦊\n",FlowFileStorage.Read(path));fails(()=>FlowFileStorage.Save(path,"\ud800"));eq("second 🦊\n",FlowFileStorage.Read(path));
  using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))fails(()=>FlowFileStorage.Save(path,"must not replace"));eq("second 🦊\n",File.ReadAllText(path));eq(0,Directory.GetFiles(root,".frociart-text-*.tmp").Length);
  string dir=Path.Combine(root,"destination-folder");Directory.CreateDirectory(dir);fails(()=>FlowFileStorage.Save(dir,"text"));eq(true,Directory.Exists(dir));eq(0,Directory.GetFiles(root,".frociart-text-*.tmp").Length);eq("result.txt",FlowFileStorage.SuggestedName("../folder/result.txt"));eq("risultato.txt",FlowFileStorage.SuggestedName(""));
 }
 public static int Main(){try{root=Path.Combine(Environment.GetEnvironmentVariable("RECOVERY_TEST_ROOT")??Path.GetTempPath(),"pc-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Basic();Damage();Capacity();Lifecycle();Storage();Console.WriteLine("FileRecoveryTests PASS "+checks+" assertions; fixtures="+root);return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
}
