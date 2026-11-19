using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text;
namespace Frociart.Tests {
 public static class FunctionPseudocodeTests {
  static int checks;static string root;static List<string> valid=new List<string>(),invalid=new List<string>();
  static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
  static void Equal(object expected,object actual,string message){Check(Object.Equals(expected,actual),message+": expected="+expected+" actual="+actual);}
  static string B64(string s){return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));}static string Decode(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s));}
  static Interpreter Run(Diagram d,string input){var r=new Interpreter(d);string[] values=input.Length==0?new string[0]:input.Split('\u001f');int at=0;for(int i=0;i<30000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;i++){if(r.State==RuntimeState.WaitingForInput){Check(at<values.Length,"input available");Check(r.ProvideInput(values[at++]),"input accepted");}else r.Step();}Check(r.State==RuntimeState.Completed||r.State==RuntimeState.Error,"bounded run");return r;}
  static void Valid(string source,string input,string expected){var d=FlowPseudocode.Decode(source);var r=Run(d,input);Equal(RuntimeState.Completed,r.State,"valid example completes: "+r.Error);Equal(expected,String.Join("\u001e",r.Output),"valid example output");CheckRoundtrip(d,input);valid.Add(B64(source)+"\t"+B64(input)+"\t"+B64(expected));}
  static void Invalid(string source,int line){bool error=false;try{FlowPseudocode.Decode(source);}catch(FlowPseudocode.ParseError e){error=true;Equal(line,e.Line,"source line: "+e.Message);}Check(error,"reject invalid pseudocode");invalid.Add(B64(source)+"\t"+line);}
  static void CheckRoundtrip(Diagram d,string input){
   byte[] before=DiagramIO.EncodeDocument(d);string code=FlowPseudocode.Encode(d);var back=FlowPseudocode.Decode(code);Equal(code,FlowPseudocode.Encode(back),"canonical stable");Check(before.SequenceEqual(DiagramIO.EncodeDocument(d)),"export does not mutate source");
   Equal(d.Functions==null?0:d.Functions.Count,back.Functions==null?0:back.Functions.Count,"function count");Equal(d.Nodes.Count,back.Nodes.Count,"main blocks");Equal(d.Edges.Count,back.Edges.Count,"main edges");
   if(d.Functions!=null)for(int i=0;i<d.Functions.Count;i++){Equal(FlowPseudocode.Signature(d.Functions[i]),FlowPseudocode.Signature(back.Functions[i]),"signature");Equal(d.Functions[i].Body.Nodes.Count,back.Functions[i].Body.Nodes.Count,"function blocks");Equal(d.Functions[i].Body.Edges.Count,back.Functions[i].Body.Edges.Count,"function edges");}
   var a=Run(d,input);var b=Run(back,input);Equal(a.State,b.State,"execution state preserved");Equal(String.Join("\u001e",a.Output),String.Join("\u001e",b.Output),"output preserved");Equal(a.Steps,b.Steps,"execution steps preserved");
  }
  public static int Main(){try{
   root=Environment.GetEnvironmentVariable("FUNCTION_PSEUDOCODE_QA");Directory.CreateDirectory(root);int index=0;
   foreach(string line in File.ReadAllLines(Environment.GetEnvironmentVariable("FUNCTION_CASES"))){var c=line.Split('\t');var d=DiagramIO.DecodeDocument(Convert.FromBase64String(c[0]));CheckRoundtrip(d,Decode(c[1]));string code=FlowPseudocode.Encode(d);File.WriteAllText(Path.Combine(root,"shared-"+index+".pseudo"),code,new UTF8Encoding(false));index++;}
   Valid(FlowPseudocode.FunctionExample,"6","12");
   Valid("INIZIO\nSTAMPA doppio(3)\nFINE\nFUNCTION doppio(x: REAL) -> REAL risultato\nrisultato=x*2\nEND FUNCTION","","6");
   Valid("FUNCTION saluta(nome: STRING)\nWRITE nome\nEND FUNCTION\nCALL saluta('Ada')","","Ada");
   Valid("FUNZIONE minimo(v: INTERO[]) -> INTERO\nrisultato = min(v)\nFINE FUNZIONE\nSTAMPA minimo([4,2,6])","","2");
   Valid("FUNZIONE crea() -> TESTO[] parole\nparole=['a','b']\nFINE FUNZIONE\nSTAMPA crea()[1]","","b");
   Valid("FUNZIONE identità(x) -> VALORE copia\ncopia=x\nFINE FUNZIONE\nSTAMPA identità('Caffè 🌙')","","Caffè 🌙");
   Valid("FUNZIONE niente() -> NESSUNO\nFINE FUNZIONE\nCHIAMA niente()","","");
   Valid("FUNZIONE archivio()\nFINE FUNZIONE","","");
   Valid("FUNZIONE limite() -> INTERO risultato\nrisultato=3\nFINE FUNZIONE\nPER i DA 1 A limite() FAI\nSTAMPA i\nFINE PER","","1\u001e2\u001e3");
   Valid("FUNZIONE testo() -> TESTO\nrisultato='FUNZIONE x() // niente'\nFINE FUNZIONE\nSTAMPA testo() // commento","","FUNZIONE x() // niente");
   Invalid("FUNZIONE f()\nFUNZIONE g()\nFINE FUNZIONE\nFINE FUNZIONE",2);
   Invalid("INIZIO\nFUNZIONE f()\nFINE FUNZIONE\nFINE",2);
   Invalid("SE vero ALLORA\nFUNZIONE f()\nFINE FUNZIONE\nFINE SE",2);
   Invalid("FUNZIONE f()\nSTAMPA 1",1);
   Invalid("FINE FUNZIONE",1);
   Invalid("FUNZIONE f(x: INTERO,x: REALE)\nFINE FUNZIONE",1);
   Invalid("FUNZIONE f()\nFINE FUNZIONE\nFUNZIONE F()\nFINE FUNZIONE",3);
   Invalid("FUNZIONE sqrt()\nFINE FUNZIONE",1);
   Invalid("FUNZIONE f(x: TIPO)\nFINE FUNZIONE",1);
   Invalid("FUNZIONE f(x: NESSUNO)\nFINE FUNZIONE",1);
   Invalid("FUNZIONE f() -> INTERO[] a b\nFINE FUNZIONE",1);
   Invalid("FUNZIONE f() -> NESSUNO[]\nFINE FUNZIONE",1);
   Invalid("FUNZIONE f()\n// qui\nSTAMPA sconosciuta(2)\nFINE FUNZIONE",3);
   Invalid("FUNZIONE f(x)\nFINE FUNZIONE\nCHIAMA f()",3);
   Invalid("FUNZIONE f()\nFINE FUNZIONE\nSTAMPA f()",3);
   Invalid("FUNZIONE f(x,)\nFINE FUNZIONE",1);
   Invalid("FUNZIONE f("+String.Join(",",Enumerable.Range(0,17).Select(i=>"x"+i))+ ")\nFINE FUNZIONE",1);
   Invalid(String.Join("\n",Enumerable.Range(0,33).Select(i=>"FUNZIONE f"+i+"()\nFINE FUNZIONE")),65);
   Invalid("FUNZIONE f()\n"+String.Concat(Enumerable.Repeat("x=1\n",250))+"FINE FUNZIONE\n"+String.Concat(Enumerable.Repeat("x=1\n",250)),500);
   var open=FlowPseudocode.Decode("FUNZIONE aperta(x: INTERO) -> INTERO risultato\nSE x>0 ALLORA\nSCOLLEGATO\nALTRIMENTI\nrisultato=0\nFINE SE\nFINE FUNZIONE\nSTAMPA aperta(1)");string explicitCode=FlowPseudocode.Encode(open);Check(explicitCode.Contains("SCOLLEGATO"),"open function port retained");Equal(open.Functions[0].Body.Edges.Count,FlowPseudocode.Decode(explicitCode).Functions[0].Body.Edges.Count,"open function graph preserved");
   var detached=FlowPseudocode.Decode("FUNZIONE staccati()\nINIZIO\nFERMA\nseparato:\nSTAMPA 7\nFERMA\nFINE\nFINE FUNZIONE\nCHIAMA staccati()");CheckRoundtrip(detached,"");File.WriteAllText(Path.Combine(root,"detached.pseudo"),FlowPseudocode.Encode(detached),new UTF8Encoding(false));
   File.WriteAllLines(Path.Combine(root,"valid.tsv"),valid,new UTF8Encoding(false));File.WriteAllLines(Path.Combine(root,"invalid.tsv"),invalid,new UTF8Encoding(false));
   string java=Path.Combine(root,"android");if(Directory.Exists(java)){foreach(string file in Directory.GetFiles(java,"*.pseudo")){string expected=File.ReadAllText(Path.Combine(root,Path.GetFileName(file)));Equal(expected,File.ReadAllText(file),"Android canonical pseudocode");}}
   Console.WriteLine("FUNCTION_PSEUDOCODE_PASS checks="+checks+" shared="+index+" valid="+valid.Count+" invalid="+invalid.Count);return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
