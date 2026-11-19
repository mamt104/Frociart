using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using Frociart;
namespace Frociart.Tests {public class DeclarationTests {
 static int checks;static string current;
 static void eq(object expected,object actual){checks++;if(!Object.Equals(expected,actual))throw new Exception(current+": expected ["+expected+"] actual ["+actual+"]");}
 static string decode(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s));}
 static Interpreter run(Diagram d,string[] inputs){var r=new Interpreter(d);int input=0;for(int i=0;i<1000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;i++){if(r.State==RuntimeState.WaitingForInput){if(input>=inputs.Length)throw new Exception(current+": missing input");eq(true,r.ProvideInput(inputs[input++]));}else r.Step();}return r;}
 public static int Main(){try{
 foreach(string line in File.ReadAllLines(Environment.GetEnvironmentVariable("DECLARATION_CASES"))){var f=line.Split('\t');current=f[0];var doc=FlowPseudocode.Decode(decode(f[1]));string exchange=Path.GetDirectoryName(Environment.GetEnvironmentVariable("DECLARATION_CASES"));Directory.CreateDirectory(Path.Combine(exchange,"pc-files"));File.WriteAllBytes(Path.Combine(exchange,"pc-files",current+".frociart"),DiagramIO.EncodeDocument(doc));string other=Path.Combine(exchange,"java-files",current+".frociart");for(int pass=0;pass<(File.Exists(other)?4:3);pass++){
  if(pass==3)doc=DiagramIO.Load(other);
  var r=f[2]=="Invalid"?new Interpreter(doc):run(doc,decode(f[4]).Split('\u001e'));
  eq(f[2]=="Completed"?RuntimeState.Completed:RuntimeState.Error,r.State);eq(decode(f[3]),String.Join("|",r.Output));
  if(f[2]=="Invalid")eq(0,r.Steps);
  foreach(string type in decode(f[5]).Split(';').Where(s=>s.Length>0)){var pair=type.Split('=');eq(pair[1],r.VariableTypes[pair[0]]);}
  if(f[2]=="Completed")doc=pass==0?FlowPseudocode.Decode(FlowPseudocode.Encode(doc)):DiagramIO.Clone(doc);
 }}
 current="invalid-input-retry";var runner=new Interpreter(FlowPseudocode.Decode("DICHIARA x: INTERO\nLEGGI x\nSTAMPA x"));while(runner.State!=RuntimeState.WaitingForInput)runner.Step();eq(false,runner.ProvideInput("abc"));eq(RuntimeState.WaitingForInput,runner.State);eq("0",ValueFormatter.Format(runner.Variables["x"]));eq(true,runner.ProvideInput("2.8"));eq("2",ValueFormatter.Format(runner.Variables["x"]));
 current="transaction";runner=run(FlowPseudocode.Decode("DICHIARA a,b: INTERO\na=2; b=\"bad\""),new string[0]);eq(RuntimeState.Error,runner.State);eq("0",ValueFormatter.Format(runner.Variables["a"]));
 current="prefix-roundtrip";var graph=FlowPseudocode.Decode("DICHIARA a: INTERO\nSTAMPA a");graph.Nodes.First(n=>n.Kind==NodeKind.Declare).Text="DECLARE a: INTEGER";eq(RuntimeState.Completed,run(FlowPseudocode.Decode(FlowPseudocode.Encode(graph)),new string[0]).State);
 current="syntax-reject";foreach(string s in new[]{"", "x", "x,x: INTERO", "x: AUTO", "x: INTERO[]", "x: INTERO[2", "x,: REALE", "1x: TESTO"}){bool rejected=false;try{FlowDeclaration.Parse(s);}catch(FlowException){rejected=true;}eq(true,rejected);}
 Console.WriteLine("DeclarationTests PASS "+checks+" assertions");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}}
