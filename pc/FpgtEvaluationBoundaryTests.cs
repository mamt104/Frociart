using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using Frociart;
public static class FpgtEvaluationBoundaryTests {
 static int checks;static void Check(bool v,string s){checks++;if(!v)throw new Exception(s);}static string B(string v){return Convert.ToBase64String(Encoding.UTF8.GetBytes(v));}
 public static int Main(string[] a){try{string root=a[0];Directory.CreateDirectory(Path.Combine(root,"boundary"));var rows=new List<string>();
  string[] names={"real-dimension","string-dimension","boolean-dimension"};
  string[] pseudos={"FUNZIONE dimensione() -> REALE r\nr=2.5\nFINE FUNZIONE\nDICHIARA a,b: INTERO[dimensione()]\nSTAMPA 99", "DICHIARA n: TESTO\nn=\"2\"\nDICHIARA a,b: INTERO[n]", "DICHIARA n: LOGICO\nn=vero\nDICHIARA a,b: INTERO[n]"};
  for(int i=0;i<names.Length;i++){var doc=FlowPseudocode.Decode(pseudos[i]);byte[] before=DiagramIO.EncodeDocument(doc);File.WriteAllBytes(Path.Combine(root,"boundary",names[i]+".frociart"),before);var p=FlowSource.Compile(doc);Check(p.CanGenerate,names[i]+" valid source model");var runtime=new Interpreter(doc);for(int step=0;step<300&&runtime.State!=RuntimeState.Completed&&runtime.State!=RuntimeState.Error;step++)runtime.Step();Check(runtime.State==RuntimeState.Error,names[i]+" original runtime rejects nonintegral/nonnumeric size");
   foreach(string target in new[]{"OwnPython","OwnJava","OwnCSharp"}){var model=FlowFpgtSyntax.Parse(File.ReadAllText(Path.Combine(root,"portable-models",target+".fpgt")));string result;try{var text=FlowFpgt.Generate(model,p);Check(i==0,"Nonnumeric dimension must be rejected");Check(text.Text.Contains("fcLength = dimensione()"),"Capture must not truncate a real dimension");if(target!="OwnPython")Check(text.Text.Contains("double fcLength"),"Real capture keeps original numeric type");File.WriteAllText(Path.Combine(root,"boundary",target+".real."+model.Extension),text.Text,new UTF8Encoding(false));result="TEXT\t"+B(text.Text);}catch(FlowFpgt.GenerationError e){Check(i!=0&&e.Message.Contains("tipo numerico"),"Specific numeric dimension diagnostic");Check(e.FunctionId==null&&doc.Nodes.Any(n=>n.Kind==NodeKind.Declare&&n.Id==e.NodeId&&n.Text.Contains("[n]")),"Diagnostic linked to original declaration");result="REJECT\t"+B(e.Message)+"\t"+e.NodeId;}
    Check(before.SequenceEqual(DiagramIO.EncodeDocument(doc)),"Dimension outcome preserves original bytes");rows.Add(names[i]+"\t"+target+"\t"+result);
   }
  }
  File.WriteAllLines(Path.Combine(root,"boundary-csharp.tsv"),rows,new UTF8Encoding(false));Console.WriteLine("FPGT_EVALUATION_BOUNDARY_PC PASS checks="+checks+" outcomes="+rows.Count);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
