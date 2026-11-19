using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;
namespace Frociart.Tests {
public class FileIoTests {
 static int checks;static string current,root;
 static void eq(object expected,object actual){checks++;if(!Object.Equals(expected,actual))throw new Exception(current+": expected ["+expected+"] actual ["+actual+"]");}
 static string decode(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s));}
 static void rejects(Action a){bool rejected=false;try{a();}catch(FlowException){rejected=true;}eq(true,rejected);}
 static Interpreter untilFile(string source){var r=new Interpreter(FlowPseudocode.Decode(source));for(int i=0;i<100&&r.State!=RuntimeState.WaitingForFile&&r.State!=RuntimeState.Error;i++)r.Step();eq(RuntimeState.WaitingForFile,r.State);return r;}
 static void Shared(){
 foreach(string row in File.ReadAllLines(Path.Combine(root,"cases.tsv"))){var f=row.Split('\t');current=f[0];var doc=FlowPseudocode.Decode(decode(f[1]));Directory.CreateDirectory(Path.Combine(root,"pc-files"));File.WriteAllBytes(Path.Combine(root,"pc-files",current+".frociart"),DiagramIO.EncodeDocument(doc));string other=Path.Combine(root,"java-files",current+".frociart");
  for(int pass=0;pass<(File.Exists(other)?4:3);pass++){
   if(pass==1)doc=FlowPseudocode.Decode(FlowPseudocode.Encode(doc));if(pass==2)doc=DiagramIO.Clone(doc);if(pass==3)doc=DiagramIO.Load(other);
   var r=new Interpreter(doc);var reads=decode(f[2]).Split('\u001e');int read=0;var saved=new List<string>();
   for(int i=0;i<20000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;i++){
    if(r.State==RuntimeState.WaitingForFile){var request=r.PendingFile;int steps=r.Steps;eq(false,r.Step());eq(steps,r.Steps);
     if(request.Kind==FlowFileRequestKind.Read){eq(true,read<Int32.Parse(f[3]));string path=Path.Combine(root,"pc-read-fixture.txt");File.WriteAllText(path,reads[read++],new UTF8Encoding(false,true));using(var stream=File.OpenRead(path))eq(true,r.ProvideFile(request,FlowTextFiles.ReadText(stream)));}
     else {string path=Path.Combine(root,"pc-write-result.txt");using(var stream=File.Create(path))FlowTextFiles.WriteText(stream,request.Text);saved.Add(File.ReadAllText(path,new UTF8Encoding(false,true)));eq(true,r.ProvideFile(request));}
     eq(false,r.ProvideFile(request,"stale"));
    }else r.Step();
   }
   string error=decode(f[7]);eq(error.Length==0?RuntimeState.Completed:RuntimeState.Error,r.State);if(error.Length>0){if((r.Error??"").IndexOf(error,StringComparison.OrdinalIgnoreCase)<0)throw new Exception(current+": error mismatch: "+r.Error);checks++;}
   eq(decode(f[4]),String.Join("|",r.Output));eq(Int32.Parse(f[6]),saved.Count);eq(decode(f[5]),String.Join("\u001e",saved));eq(Int32.Parse(f[3]),read);
  }
 }
 }
 static void StateChecks(){
 current="cancel-retry-stale";var r=untilFile("APRI_FILE LETTURA \"x\"\nLEGGI_FILE x\nSTAMPA x");var request=r.PendingFile;eq(true,r.FileDeferred(request,null));eq(request,r.PendingFile);eq(false,r.ProvideFile(request,null));eq(request,r.PendingFile);eq(true,r.ProvideFile(request,"8"));eq(false,r.ProvideFile(request,"9"));while(r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error)r.Step();eq("8",String.Join("|",r.Output));
 r=untilFile("APRI_FILE LETTURA \"x\"\nLEGGI_FILE x");request=r.PendingFile;r.Reset();eq(false,r.ProvideFile(request,"late"));eq(false,r.FileDeferred(request,"late"));eq(RuntimeState.Ready,r.State);
 current="write-retry-retains-draft";r=untilFile("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE \"once\"\nCHIUDI_FILE SCRITTURA");request=r.PendingFile;eq("once\n",r.Files.OutputDraft);for(int i=0;i<5;i++){eq(true,r.FileDeferred(request,"Disco pieno"));eq(false,r.Step());eq("once\n",r.Files.OutputDraft);}rejects(()=>r.Reset());eq(request,r.PendingFile);eq(true,r.ProvideFile(request));eq(false,r.Files.HasWrite);
 current="reset-explicit-discard";r=untilFile("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE 1");request=r.PendingFile;r.Reset(true);eq(false,r.ProvideFile(request));eq(false,r.Files.HasWrite);
 current="bad-read-no-commit";r=untilFile("DICHIARA n: INTERO\nAPRI_FILE LETTURA \"x\"\nLEGGI_FILE n");r.ProvideFile(r.PendingFile,"bad\n12");r.Step();eq(RuntimeState.Error,r.State);eq("0",ValueFormatter.Format(r.Variables["n"]));eq("bad",r.Files.PeekLine());
 current="execution-failure-retains-draft";r=new Interpreter(FlowPseudocode.Decode("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE \"recover\"\nx=1/0"));while(r.State!=RuntimeState.Error)r.Step();eq("recover\n",r.Files.OutputDraft);rejects(()=>r.Reset());
 current="invalid-syntax";foreach(string s in new[]{"APRI_FILE unknown \"x\"","CHIUDI_FILE","CHIUDI_FILE x","SCRIVI_FILE SENZA_A_CAPO","STAMPA EOF(1)","LEGGI_FILE x+1"}){bool rejected=false;try{FlowPseudocode.Decode(s);}catch(ArgumentException){rejected=true;}eq(true,rejected);}
 }
 sealed class BrokenWrite:MemoryStream{public override void Write(byte[] b,int o,int c){throw new IOException("injected write failure");}}
 static void Streams(){
 current="encoding";string text=" 007 \r\n🦊 è 日本語\n";foreach(var encoding in new Encoding[]{new UTF8Encoding(false,true),new UTF8Encoding(true,true),new UnicodeEncoding(false,true,true),new UnicodeEncoding(true,true,true)}){byte[] data=encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();using(var stream=new MemoryStream(data)){eq(text,FlowTextFiles.ReadText(stream));eq(true,stream.CanRead);}}
 foreach(byte[] bad in new[]{new byte[]{0xc0,0xaf},new byte[]{0xff,0xfe,0},new byte[]{0xef,0xbb,0xbf,0xed,0xa0,0x80}})using(var stream=new MemoryStream(bad))rejects(()=>FlowTextFiles.ReadText(stream));
 using(var stream=new MemoryStream(new byte[FlowTextFiles.MaxBytes+1]))rejects(()=>FlowTextFiles.ReadText(stream));
 var r=untilFile("APRI_FILE SCRITTURA \"x\"\nSCRIVI_FILE \"safe\"");bool failed=false;using(var stream=new BrokenWrite()){try{FlowTextFiles.WriteText(stream,r.PendingFile.Text);}catch(IOException){failed=true;}}eq(true,failed);eq("safe\n",r.Files.OutputDraft);eq(RuntimeState.WaitingForFile,r.State);
 current="bounds";var files=new FlowTextFiles();files.OpenRead("x",new string('x',FlowTextFiles.MaxLine));eq(FlowTextFiles.MaxLine,files.PeekLine().Length);files.CommitRead();eq(true,files.Eof());files.CloseRead();rejects(()=>files.OpenRead("x",new string('x',FlowTextFiles.MaxCharacters+1)));rejects(()=>files.OpenRead("x","\ud800"));files.OpenWrite("x");rejects(()=>files.Write("\ud800",false));eq("",files.OutputDraft);string chunk=new string('x',32768);for(int i=0;i<64;i++)files.Write(chunk,false);eq(FlowTextFiles.MaxCharacters,files.OutputDraft.Length);rejects(()=>files.Write("x",false));eq(FlowTextFiles.MaxCharacters,files.OutputDraft.Length);
 }
 static void Debugger(){
 current="debugger-file-into";var doc=FlowPseudocode.Decode("APRI_FILE LETTURA \"x\"\nLEGGI_FILE x\nSTAMPA x");var r=new Interpreter(doc);r.Step();var d=new FlowDebugger(r,doc);d.Begin(FlowDebugger.Command.Into);d.Tick();eq(RuntimeState.WaitingForFile,r.State);eq(true,d.Active);eq(false,d.Tick());r.ProvideFile(r.PendingFile,"3");d.InputProvided();eq(false,d.Active);eq(NodeKind.ReadFile,r.CurrentDiagram.Nodes.First(n=>n.Id==r.CurrentNodeId).Kind);
 current="debugger-eof-breakpoint";var point=doc.Nodes.First(n=>n.Kind==NodeKind.ReadFile);point.Breakpoint=true;point.BreakpointCondition="non EOF()";d.UpdateBreakpoints(doc);d.Begin(FlowDebugger.Command.Continue);d.Tick();eq(true,d.AtBreakpoint);eq("3",r.Files.PeekLine());d.Begin(FlowDebugger.Command.Continue);for(int i=0;i<10&&d.Active;i++)d.Tick();eq(RuntimeState.Completed,r.State);eq("3",String.Join("|",r.Output));
 current="debugger-file-over-function";doc=FlowPseudocode.Decode("FUNZIONE apri()\nAPRI_FILE LETTURA \"x\"\nFINE FUNZIONE\nCHIAMA apri()\nLEGGI_FILE x");r=new Interpreter(doc);r.Step();d=new FlowDebugger(r,doc);d.Begin(FlowDebugger.Command.Over);for(int i=0;i<20&&r.State!=RuntimeState.WaitingForFile;i++)d.Tick();eq(RuntimeState.WaitingForFile,r.State);eq(2,r.CallDepth);r.ProvideFile(r.PendingFile,"9");d.InputProvided();eq(true,d.Active);for(int i=0;i<20&&d.Active;i++)d.Tick();eq(1,r.CallDepth);eq(false,d.Active);eq("9",r.Files.PeekLine());
 }
 static void Rename(){
 current="rename-protects-channel";var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"lettura","sorgente"},{"scrittura","destinazione"},{"SENZA_A_CAPO","valore"}};
 eq("  LETTURA sorgente",FlowDeclaration.RewriteNode(new FlowNode{Kind=NodeKind.OpenFile,Text="  LETTURA lettura"},names));
 eq("SCRITTURA",FlowDeclaration.RewriteNode(new FlowNode{Kind=NodeKind.CloseFile,Text="SCRITTURA"},names));
 eq("SENZA_A_CAPO destinazione",FlowDeclaration.RewriteNode(new FlowNode{Kind=NodeKind.WriteFile,Text="SENZA_A_CAPO scrittura"},names));
 var doc=FlowPseudocode.Decode("FUNZIONE apri(lettura: TESTO)\nAPRI_FILE LETTURA lettura\nFINE FUNZIONE\nCHIAMA apri(\"x\")\nLEGGI_FILE x");var changed=doc.Functions[0].Copy();changed.Parameters[0].Name="sorgente";var saved=FunctionEditing.Save(doc,changed,new Dictionary<string,string>{{"lettura","sorgente"}});eq("LETTURA sorgente",saved.Functions[0].Body.Nodes.First(n=>n.Kind==NodeKind.OpenFile).Text);eq(false,Validator.Validate(saved).Any(i=>i.Severity==Severity.Error));
 }
 public static int Main(){try{root=Path.GetFullPath(Environment.GetEnvironmentVariable("FILEIO_CASES_ROOT"));Shared();StateChecks();Streams();Debugger();Rename();Console.WriteLine("FileIoTests PASS "+checks+" assertions");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}}
