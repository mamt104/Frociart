using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Frociart.Tests {
 internal static class RecoveryOwnershipTests {
  static int checks;
  static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
  static object Call(object target,string name,params object[] args){return target.GetType().GetMethods(F).First(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(target,args);}
  static void Set(object target,string name,object value){target.GetType().GetField(name,F).SetValue(target,value);}
  static T Get<T>(object target,string name){return (T)target.GetType().GetField(name,F).GetValue(target);}
  static void Pump(int ms=30){var until=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();Thread.Sleep(2);}while(DateTime.UtcNow<until);}
  static Diagram Document(string title){var d=DiagramIO.CreateBlank();d.Title=title;return d;}
  static void EditTitle(MainForm f,string title){f.Canvas.SelectNode(null);Get<TextBox>(f,"titleEdit").Text=title;f.CommitInspector();}
  static MainForm Open(string recovery){
   // Safe startup prevents production recovery migration or prompts. Only the
   // private temporary path below is enabled after the native window is shown.
   var f=new MainForm(null,true){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-3000),Size=new Size(1180,820)};
   Set(f,"recoveryPath",recovery);f.Show();Pump();Set(f,"ignoreRecovery",false);return f;
  }
  static void Close(MainForm f){Set(f,"ignoreRecovery",true);Set(f,"dirty",false);f.Close();f.Dispose();Pump();}
  static bool Same(string path,byte[] expected){return File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(expected);}
  static void AnswerNo(MainForm owner,Action action,DialogResult answer=DialogResult.No){
   bool answered=false;
   using(var timer=new System.Windows.Forms.Timer{Interval=15}){
    timer.Tick+=delegate{var dialog=Application.OpenForms.OfType<FlowMessageDialog>().FirstOrDefault();if(dialog==null)return;timer.Stop();answered=true;dialog.DialogResult=answer;dialog.Close();};
    timer.Start();action();
   }
   Check(answered,"The native dialog accepts the intended answer");
  }
  [STAThread]static int Main(){try{
   Application.EnableVisualStyles();UiMotion.SetEnabled(true);Ui.SetDarkMode(true,false);
   string folder=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"recovery-ownership-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   string recovery=Path.Combine(folder,"recupero.frociart"),saved=Path.Combine(folder,"other.frociart");
   byte[] foreign=DiagramIO.EncodeDocument(Document("Unrelated unsaved project"));
   File.WriteAllBytes(recovery,foreign);
   using(var f=Open(recovery)){
    f.SetDocument(Document("Another clean project"),saved,false);
    f.Close();Pump();
    Check(Same(recovery,foreign),"Closing another clean document preserves a pre-existing recovery byte for byte");
   }
   using(var f=Open(recovery)){
    f.SetDocument(Document("Another saved project"),saved,false);
    Check((bool)Call(f,"SaveDocument",false),"Saving another file succeeds");
    Check(Same(recovery,foreign),"Saving another file preserves the unrelated recovery");
    Check(DiagramIO.Load(saved).Title=="Another saved project","Explicit save writes the requested actual project");
    f.SetDocument(Document("Another unsaved project"),null,true);Call(f,"SaveRecovery");
    Check(Same(recovery,foreign),"Automatic recovery refuses to overwrite a foreign snapshot");
    Check(Get<Label>(f,"status").Text=="Recupero automatico non disponibile. Usa Salva per conservare il diagramma.","Blocked autosave gives the existing truthful manual-save instruction");
    AnswerNo(f,()=>Call(f,"NewDocument"));
    Check(Same(recovery,foreign),"Discarding the current document cannot delete somebody else's recovery");
    Close(f);
   }
   File.Delete(recovery);
   using(var f=Open(recovery)){
    f.SetDocument(Document("Owned draft"),saved,true);Call(f,"SaveRecovery");
    Check(File.Exists(recovery)&&DiagramIO.Load(recovery).Title=="Owned draft","A new recovery saves real document data");
    EditTitle(f,"Owned draft updated");Call(f,"SaveRecovery");
    Check(DiagramIO.Load(recovery).Title=="Owned draft updated","Owned recovery can be updated by its window");
    Check((bool)Call(f,"SaveDocument",false),"Owned document can be saved explicitly");
    Check(!File.Exists(recovery)&&DiagramIO.Load(saved).Title=="Owned draft updated","Explicit save clears only the owned snapshot and preserves the saved document");
    Close(f);
   }
   using(var f=Open(recovery)){
    f.SetDocument(Document("Original owner"),saved,true);Call(f,"SaveRecovery");
    File.WriteAllBytes(recovery,foreign);EditTitle(f,"Owner edited after replacement");Call(f,"SaveRecovery");
    Check(Same(recovery,foreign),"An externally replaced recovery cannot be overwritten by the former owner");
    Check((bool)Call(f,"SaveDocument",false),"Explicit saving remains available after recovery replacement");
    Check(Same(recovery,foreign),"The former owner cannot delete a replacement when saving");
    f.Close();Pump();Check(Same(recovery,foreign),"The former owner cannot delete a replacement when closing");
   }
   using(var f=Open(recovery)){
    Call(f,"LoadRecovery");Set(f,"currentPath",saved);
    Check(f.Document.Title=="Unrelated unsaved project"&&Get<bool>(f,"dirty"),"Accepted recovery loads the exact validated snapshot as unsaved work");
    Check((bool)Call(f,"SaveDocument",false)&&!File.Exists(recovery),"Saving an explicitly accepted recovery cleans up its owned snapshot");
    Close(f);
   }
   using(var f=Open(recovery)){
    f.SetDocument(Document("Old owned document"),saved,true);Call(f,"SaveRecovery");
    byte[] beforeCanceledOpen=File.ReadAllBytes(recovery);
    AnswerNo(f,()=>Check((bool)Call(f,"ConfirmDiscard"),"Open can receive an explicit discard choice before file selection"));
    Check(Same(recovery,beforeCanceledOpen)&&Get<bool>(f,"dirty"),"Canceling the subsequent open leaves the active document and its recovery intact");
    string invalid=Path.Combine(folder,"invalid.frociart");File.WriteAllText(invalid,"invalid");
    AnswerNo(f,()=>Check(!f.LoadPath(invalid),"Invalid file selection is rejected"),DialogResult.OK);
    Check(Same(recovery,beforeCanceledOpen)&&Get<bool>(f,"dirty"),"A failed open also preserves the active document and its recovery");
    AnswerNo(f,()=>Call(f,"NewDocument"));
    Check(!File.Exists(recovery),"New after explicit discard removes the old owned recovery before switching documents");
    EditTitle(f,"New document draft");Call(f,"SaveRecovery");
    Check(File.Exists(recovery)&&DiagramIO.Load(recovery).Title=="New document draft","The new document can autosave after the old owned recovery is discarded");
    DiagramIO.Save(saved,Document("Opened file"));
    AnswerNo(f,()=>{Check((bool)Call(f,"ConfirmDiscard"),"Open path accepts explicit discard");Check(f.LoadPath(saved),"Opening a local file succeeds after discard");});
    Check(!File.Exists(recovery)&&f.Document.Title=="Opened file","Open after discard also releases old ownership before loading the next file");
    EditTitle(f,"Opened file edited");Call(f,"SaveRecovery");
    Check(DiagramIO.Load(recovery).Title=="Opened file edited","The newly opened document can autosave");
    AnswerNo(f,()=>f.Close());Pump();Check(!File.Exists(recovery),"Explicit discard on close cleans the owned recovery");
   }
   using(var f=Open(recovery)){
    f.SetDocument(Document("Owned before direct switch"),saved,true);Call(f,"SaveRecovery");
    f.SetDocument(Document("Unrelated direct replacement"),saved,false);Call(f,"SaveDocument",false);
    Check(!File.Exists(recovery),"An actual document switch cleans the old owned recovery before resetting ownership");
    Close(f);
   }
   using(var first=Open(recovery))using(var second=Open(recovery)){
    first.SetDocument(Document("First window draft"),saved,true);Call(first,"SaveRecovery");
    Call(second,"LoadRecovery");EditTitle(first,"First window newer draft");Call(first,"SaveRecovery");byte[] newer=File.ReadAllBytes(recovery);
    EditTitle(second,"Second window divergent draft");Call(second,"SaveRecovery");
    Check(Same(recovery,newer),"A second window holding an older accepted snapshot cannot overwrite a newer recovery");
    Set(second,"dirty",false);second.Close();Pump();
    Check(Same(recovery,newer),"Closing the second window cannot delete the first window's newer recovery");
    using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)){
     Exception workerError=null;var worker=new Thread(()=>{try{Call(first,"WithRecoveryLock",(Action)(()=>{entered.Set();release.WaitOne(5000);}));}catch(Exception error){workerError=error;entered.Set();}});
     worker.Start();Check(entered.WaitOne(2000)&&workerError==null,"Another writer can hold the same recovery lock");
     try{EditTitle(first,"Write waits for owner");Call(first,"SaveRecovery");Check(Same(recovery,newer),"Autosave refuses safely while a different thread holds the recovery lease");}finally{release.Set();worker.Join(2500);}
     Check(workerError==null&&!worker.IsAlive,"Recovery lock releases without leaving a blocked writer");
    }
    Call(first,"SaveRecovery");Check(DiagramIO.Load(recovery).Title=="Write waits for owner","Autosave succeeds again after the lock becomes available");
    Set(first,"currentPath",saved);Check((bool)Call(first,"SaveDocument",false)&&!File.Exists(recovery),"Owned cleanup still works after concurrent access is refused");
    Close(first);
   }
   Console.WriteLine("RECOVERY_OWNERSHIP_FINISHED checks="+checks+" errors=0; isolated temporary recovery paths only");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
