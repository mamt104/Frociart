using System;using System.IO;using System.Linq;using System.Text;using System.Reflection;using System.Collections.Generic;using Frociart;
public static class FpgtRendererCoreTests {
 static int checks;static string root,model;static readonly List<string> entries=new List<string>();
 static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
 static string B(string s){return Convert.ToBase64String(Encoding.UTF8.GetBytes(s??""));}
 static void Case(string name,string pseudo,string template,string expectedError=null,bool profile=false){
  var doc=FlowPseudocode.Decode(pseudo);var program=FlowSource.Compile(doc);Check(program.CanGenerate,name+" valid source model");byte[] before=DiagramIO.EncodeDocument(doc);File.WriteAllBytes(Path.Combine(root,"renderer-core",name+".frociart"),before);File.WriteAllText(Path.Combine(root,"renderer-core",name+".fpgt"),template,new UTF8Encoding(false));
  string outcome;
  try{var code=FlowFpgt.Generate(FlowFpgtSyntax.Parse(template),program,profile?new Dictionary<string,FlowDataType>{{FlowFpgtTypes.Key(null,"x"),FlowDataType.Integer}}:null);Check(expectedError==null,name+" should reject "+expectedError);outcome="PASS\t"+B(code.Text);}
  catch(ArgumentException error){Check(expectedError!=null&&error.Message.IndexOf(expectedError,StringComparison.OrdinalIgnoreCase)>=0,name+" unexpected diagnostic: "+error.Message);var linked=error as FlowFpgt.GenerationError;if(linked!=null)Check(linked.NodeId!=null,name+" block-linked diagnostic");outcome="REJECT\t"+B(error.Message);}
  Check(before.SequenceEqual(DiagramIO.EncodeDocument(doc)),name+" preserves original diagram on every outcome");entries.Add(name+"\t"+profile.ToString().ToLowerInvariant()+"\t"+outcome);
 }
 public static int Main(string[] args){try{
  root=args[0];Directory.CreateDirectory(Path.Combine(root,"renderer-core"));model=File.ReadAllText(Path.Combine(root,"portable-models/OwnPython.fpgt")).Replace("\r\n","\n");
  Case("field","STAMPA 1",model.Replace("print({Expression}) | newline","print({missing}) | newline"),"Campo non disponibile");
  Case("unknown-flag","STAMPA 1",model.Replace("print({Expression}) | newline","print({Expression}) | ~output, inventedflag"),"Flag non riconosciuto");
  Case("negative-indent","STAMPA 1",model.Replace("print({Expression}) | newline","print({Expression}) | newline | -32"),"rientro");
  Case("no-body","STAMPA 1",model.Replace("[Main]\nText = pass\n = -->BLOCK","[Main]\nText = pass"),"-->BLOCK");
  Case("no-statement","STAMPA 1",model.Replace("print({Expression}) | newline","print({Expression}) | ~output").Replace("print({Expression}, end=\"\") | ~newline","print({Expression}, end=\"\") | ~output"),"istruzione eseguibile");
  Case("comment-only","STAMPA 1",model.Replace("print({Expression}) | newline","# Operazione omessa | newline"),"istruzione eseguibile");
  Case("input-choice","LEGGI x\nSTAMPA x",model,"Scegli il tipo");
  Case("chosen-input","LEGGI x\nSTAMPA x",model,null,true);
  Case("vector-literal","v=[1,2]\nSTAMPA size(v)",model,"vettore letterale");
  Case("array-copy","FUNZIONE f(a: INTERO[]) -> INTERO r\nr=a[0]\nFINE FUNZIONE\nDICHIARA v: INTERO[1]\nSTAMPA f(v)",model,"copia del vettore");
  Case("vector-return","FUNZIONE f() -> INTERO[] r\nr=[1,2]\nFINE FUNZIONE\nSTAMPA size(f())",model,"risultati vettoriali");
  Case("turtle-stub","AVANZA 3",File.ReadAllText(Path.Combine(root,"references/installed/Template_CSharp.fpgt")),"istruzione eseguibile");
  Case("turtle-python","PENNA \"blu\"\nAVANZA 3\nRUOTA DESTRA 90",File.ReadAllText(Path.Combine(root,"references/installed/Template_Python.fpgt")));
  Case("turtle-bad-color","PENNA \"colore-inesistente\"",model,"Colore non riconosciuto");
  Case("unexpanded-text","STAMPA \"{Expression} {{ source }} C:\\\\a \\\"b\\\"\"",model);
  Case("multiline-string","STAMPA \"A\\nB\\tC\\rD\"",model);
  string many=String.Concat(Enumerable.Repeat("STAMPA 1\n",200));string expanded=model.Replace("print({Expression}) | newline",String.Join("\n = ",Enumerable.Repeat("print({Expression}) | newline",300)));Case("output-limit",many,expanded,"limite di dimensione");
  File.WriteAllLines(Path.Combine(root,"renderer-core-csharp.tsv"),entries,new UTF8Encoding(false));
  var engineType=typeof(FlowFpgt).GetNestedType("Engine",BindingFlags.NonPublic);var engine=Activator.CreateInstance(engineType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{FlowFpgtSyntax.Parse(model),FlowSource.Compile(FlowPseudocode.Decode("STAMPA 1")),null},null);var format=engineType.GetMethod("DecimalNumber",BindingFlags.Instance|BindingFlags.NonPublic);
  var numbers=new List<string>();ulong bits=0x3490511289059431UL;for(int i=0;i<3000;i++){bits=unchecked(bits*6364136223846793005UL+1442695040888963407UL);if((bits&0x7ff0000000000000UL)==0x7ff0000000000000UL)continue;double number=BitConverter.Int64BitsToDouble(unchecked((long)bits));numbers.Add(bits.ToString("x16")+"\t"+(string)format.Invoke(engine,new object[]{number}));}foreach(double number in new[]{0d,Double.Epsilon,Double.MaxValue,Double.MinValue,1e23,1e-300,0.1,1.2345678901234567})numbers.Add(unchecked((ulong)BitConverter.DoubleToInt64Bits(number)).ToString("x16")+"\t"+(string)format.Invoke(engine,new object[]{number}));File.WriteAllLines(Path.Combine(root,"renderer-numbers-csharp.tsv"),numbers,new UTF8Encoding(false));
  Console.WriteLine("FPGT_RENDERER_CORE PASS checks="+checks+" cases="+entries.Count+" numbers="+numbers.Count);return 0;
 }catch(Exception error){Console.WriteLine(error);return 1;}}
}
