using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;
namespace Frociart.Tests {internal static class IntrinsicTests {
 static int checks;static void Check(bool b,string name){if(!b)throw new Exception(name);checks++;}
 static string Decode(string b){return Encoding.UTF8.GetString(Convert.FromBase64String(b));}
 static Interpreter Run(string code){var d=FlowPseudocode.Decode(code);var r=new Interpreter(d);for(int i=0;i<1000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;i++)r.Step();return r;}
 public static int Main(){try{
 foreach(string line in File.ReadAllLines(Environment.GetEnvironmentVariable("INTRINSIC_CASES"))){var p=line.Split('\t');string expression=Decode(p[1]),expected=Decode(p[2]);object actual=null;bool error=false;try{actual=ExpressionLanguage.Evaluate(expression,null);}catch(FlowException){error=true;}Check(error==(p[0]=="error"),expression+" error="+error);if(error)continue;Check(p[0]=="text"?actual is string:actual is double,expression+" type");Check(ValueFormatter.Format(actual)==expected,expression+" expected="+expected+" actual="+ValueFormatter.Format(actual));
 var diagram=FlowPseudocode.Decode("risultato = "+expression+"\nSTAMPA risultato");for(int i=0;i<2;i++){var runtime=new Interpreter(diagram);for(int j=0;j<10&&runtime.State!=RuntimeState.Completed&&runtime.State!=RuntimeState.Error;j++)runtime.Step();Check(runtime.State==RuntimeState.Completed&&runtime.Output.Single()==expected,"Native graph roundtrip: "+expression);diagram=FlowPseudocode.Decode(FlowPseudocode.Encode(diagram));}}
 var values=new HashSet<double>();for(int i=0;i<256;i++){double n=(double)ExpressionLanguage.Evaluate("random(6)",null);Check(n>=0&&n<6&&n==Math.Floor(n),"Random range");values.Add(n);}Check(values.Count>1,"Random generator is not a constant stub");
 Check(ExpressionLanguage.GetVariables("sin(pi/2) + cos(π) + len(nome)").SequenceEqual(new[]{"nome"}),"Constants excluded from unresolved variables");
 Check(Run("pi = 3\nSTAMPA pi").Output.Single()=="3","Existing pi variable remains usable");
 Check(Run("STAMPA round(pi, 2)").Output.Single()=="3.14","Pi default constant in graph");
 var failed=Run("x = 7\nx = 2; y = log(0)");Check(failed.State==RuntimeState.Error&&(double)failed.Variables["x"]==7&&!failed.Variables.ContainsKey("y"),"Domain error rolls back entire operation block");
 Check(Run("STAMPA false and log(0)").Output.Single()=="falso","Short circuit preserves domain safety");
 Console.WriteLine("INTRINSIC_PASS checks="+checks);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}}
