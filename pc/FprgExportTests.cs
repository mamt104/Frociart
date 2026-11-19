using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using Frociart;
internal static class FprgExportTests {
 static int checks;
 static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static Interpreter Execute(Diagram d,string[] row){
  var run=new Interpreter(d);int input=0;
  for(int i=0;i<4000&&run.State!=RuntimeState.Completed&&run.State!=RuntimeState.Error;i++){
   if(run.State==RuntimeState.WaitingForInput)run.ProvideInput(row[5].Split('|')[input++]);else run.Step();
  }
  Check((run.State==RuntimeState.Error)==(row[2]=="error"),row[0]+" terminal state "+run.State+": "+run.Error);
  Check(run.State==RuntimeState.Completed||run.State==RuntimeState.Error,row[0]+" bounded completion");
  Check(run.OutputText.Replace("\r\n","\n")==row[3],row[0]+" exact output: "+run.OutputText);return run;
 }
 static int Main(string[] args){try{
  string root=args[0];Directory.CreateDirectory(Path.Combine(root,"pc"));
  foreach(string title in new[]{"A & B <C> \"D\"\n🐢","\u0001","\uD800"}){
   var probe=FlowPseudocode.Decode("STAMPA 1");probe.Title=title;var result=FlowgorithmExport.Analyze(probe);
   if(title[0]=='A'){Check(result.CanExport,"XML escaping valid");var xml=new System.Xml.XmlDocument();xml.XmlResolver=null;xml.LoadXml(result.Xml);Check(xml.SelectSingleNode("/flowgorithm/attributes/attribute[@name='name']/@value").Value==title,"Exact title after XML decode");Check(!result.Xml.Contains("<!DOCTYPE")&&!result.Xml.Contains("name=\"authors\"")&&!result.Xml.Contains("name=\"saved\""),"Passive XML without history");}
   else Check(!result.CanExport&&result.Xml==null&&result.Issues.Any(i=>i.Code=="xml-text"),"Invalid XML text rejected");
  }
  var inputProbe=FlowPseudocode.Decode("LEGGI x\nSTAMPA x");var missing=FlowgorithmExport.Analyze(inputProbe,new Dictionary<string,FlowDataType>{{":x",FlowDataType.Void}});
  Check(!missing.CanExport&&missing.RequiredTypes.Count==1&&missing.RequiredTypes[0].NodeId!=null,"Invalid profile requires deliberate type");
  var invalid=FlowPseudocode.Decode("x=1\nSTAMPA x");var bad=invalid.Nodes.Single(n=>n.Kind==NodeKind.Process);bad.Text="broken +";var blocked=FlowgorithmExport.Analyze(invalid);Check(!blocked.CanExport&&blocked.Xml==null&&blocked.Issues.Any(i=>i.NodeId==bad.Id),"Error points to original block");
  foreach(var line in File.ReadAllLines(Path.Combine(root,"cases.tsv"))){
   var raw=line.Split('\t');var row=new[]{raw[0]}.Concat(raw.Skip(1).Select(s=>Encoding.UTF8.GetString(Convert.FromBase64String(s)))).ToArray();
   var d=FlowPseudocode.Decode(row[1]);byte[] before=DiagramIO.EncodeDocument(d);var profile=new Dictionary<string,FlowDataType>();
   if(row[4].Length>0)foreach(string pair in row[4].Split('|')){var p=pair.Split('=');profile[p[0]]=(FlowDataType)Int32.Parse(p[1]);}
   var result=FlowgorithmExport.Analyze(d,profile);Check(before.SequenceEqual(DiagramIO.EncodeDocument(d)),row[0]+" original unchanged");
   if(row[2]=="reject"){Check(!result.CanExport&&result.Xml==null&&result.Issues.Any(i=>i.Error),row[0]+" atomic rejection");Console.WriteLine("PASS rejected "+row[0]+": "+String.Join("; ",result.Issues.Where(i=>i.Error).Select(i=>i.Code)));continue;}
   Check(result.CanExport,row[0]+" valid export: "+String.Join("; ",result.Issues.Select(i=>i.Message)));Check(result.Xml==FlowgorithmExport.Analyze(d,profile).Xml,row[0]+" deterministic");
   var original=Execute(d,row);var converted=Execute(FlowgorithmImport.DecodeText(result.Xml),row);
   var values=original.CallStack[0].Variables;var actual=converted.CallStack[0].Variables;var watch=new List<string>();
   foreach(var pair in values){var mapping=result.Names.Single(n=>n.FunctionId==null&&n.Original.Equals(pair.Key,StringComparison.OrdinalIgnoreCase));object value;Check(actual.TryGetValue(mapping.Exported,out value)&&ValueFormatter.Format(value)==ValueFormatter.Format(pair.Value),row[0]+" original variable/rollback "+pair.Key);watch.Add(mapping.Exported+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(ValueFormatter.Format(pair.Value))));}
   File.WriteAllLines(Path.Combine(root,"pc",row[0]+".watch.tsv"),watch,new UTF8Encoding(false));File.WriteAllText(Path.Combine(root,"pc",row[0]+".fprg"),result.Xml,new UTF8Encoding(false));Console.WriteLine("PASS exported "+row[0]);
  }
  Console.WriteLine("FPRG_EXPORT_PC_CORE PASS checks="+checks);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
