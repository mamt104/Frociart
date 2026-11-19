using System;using System.IO;using System.Linq;using Frociart;
internal static class FunctionNativeExportTests {
 static int Main(string[] args){try{
  string folder=Directory.Exists(Path.Combine(args[0],"android-captures-delivery"))?"android-captures-delivery":"android-captures";var library=DiagramIO.Load(Path.Combine(args[0],folder,"Funzioni_A_3.frociart"));if(library.Functions.Count!=3||library.Nodes.Count!=2)throw new Exception("Native export closure mismatch");
  var fn=library.Functions.Single(f=>f.Name=="A_3");var target=FlowPseudocode.Decode("STAMPA 0");var plan=FunctionTransfer.Import(target,library,fn.Id);plan.Project.Nodes.Single(n=>n.Kind==NodeKind.Output).Text="A_3(4)";var runner=new Interpreter(plan.Project);
  for(int i=0;i<200&&runner.State!=RuntimeState.Completed&&runner.State!=RuntimeState.Error;i++)runner.Step();if(runner.State!=RuntimeState.Completed||String.Join("|",runner.Output)!="helper(n)|13")throw new Exception("Native Android library did not execute on PC");
  Console.WriteLine("NATIVE_ANDROID_EXPORT_ON_PC PASS checks=2");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
