using System;using System.Linq;using Frociart;
namespace Frociart.Tests {public class CapabilityTests {
 static int count;static void eq(object a,object b){count++;if(!Object.Equals(a,b))throw new Exception("Expected "+a+" actual "+b);}
 static string fmt(string e){return ValueFormatter.Format(ExpressionLanguage.Evaluate(e,null));}
 static void rejectExpr(string s){bool error=false;try{fmt(s);}catch(FlowException){error=true;}eq(true,error);}
 static void rejectCode(string s){bool error=false;try{FlowPseudocode.Decode(s);}catch(ArgumentException){error=true;}eq(true,error);}
 static Interpreter run(Diagram d,string[] inputs){var r=new Interpreter(d);int input=0;for(int i=0;i<1000&&r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error;i++){if(r.State==RuntimeState.WaitingForInput){if(input>=inputs.Length)throw new Exception("Unexpected input");eq(true,r.ProvideInput(inputs[input++]));}else r.Step();}return r;}
 static void program(string s,string expected,string[] inputs){var d=FlowPseudocode.Decode(s);for(int n=0;n<3;n++){var r=run(d,inputs);eq(RuntimeState.Completed,r.State);eq(expected,String.Join("|",r.Output));d=n==0?FlowPseudocode.Decode(FlowPseudocode.Encode(d)):DiagramIO.Clone(d);}}
 public static int Main(){try{eq("[10, 20, 30]",fmt("[10,20,30]"));
eq("[]",fmt("[]"));
eq("[vero, falso]",fmt("[true,false]"));
eq("[\"a\", \"b\"]",fmt("[\"a\", \"b\"]"));
eq("20",fmt("[10,20][1]"));
eq("2",fmt("len([1,2])"));
eq("6",fmt("sum([1,2,3])"));
eq("0",fmt("somma([])"));
eq("1",fmt("min([3,1,2])"));
eq("-1",fmt("max([-3,-1,-2])"));
eq("[0, 0, 0]",fmt("vettore(3)"));
eq("[\"x\", \"x\"]",fmt("array(2, \"x\")"));
eq("1024",fmt("len(vettore(1024))"));
eq("vero",fmt("[0] = [-0]"));
eq("vero",fmt("[1,2] != [1,3]"));
eq("vero",fmt("[] = []"));
eq("3",fmt("len(\"abc\")"));
rejectExpr("[1,\"x\"]");
rejectExpr("[[1]]");
rejectExpr("[1][1]");
rejectExpr("[1][-1]");
rejectExpr("[1][0.5]");
rejectExpr("[][0]");
rejectExpr("vettore(1025)");
rejectExpr("vettore(-1)");
rejectExpr("vettore(0,[])");
rejectExpr("vettore(1.5)");
rejectExpr("min([])");
rejectExpr("sum([\"x\"])");
rejectExpr("[1]+[2]");
rejectExpr("len(1)");
rejectExpr("vettore(2,\"x\") < 3");
rejectExpr("[1,]");
rejectExpr("[");
rejectExpr("1[0]");
rejectExpr("vettore(1024,\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\")");
program("v = [1,2,3]\nv[1] = 9\nSTAMPA v","[1, 9, 3]",new string[]{});
program("v = [1]\nb = v\nv[0] = 9\nSTAMPA b","[1]",new string[]{});
program("v = [1,2]\ni = 1\nLEGGI v[min(i, 0)], \"Valore?\"\nSTAMPA v","[7, 2]",new string[]{"7"});
program("V = [1]\nv[0] = 5\nSTAMPA V[0]","5",new string[]{});
program("totale = 0\nPER i DA 1 A 5 FAI\n totale = totale + i\nFINE PER\nSTAMPA totale","15",new string[]{});
program("FOR i = 3 TO 1 STEP -1 DO\n STAMPA i\nEND FOR","3|2|1",new string[]{});
program("PER i DA 1 A 5 PASSO 2 FAI\n STAMPA i\nFINE PER","1|3|5",new string[]{});
program("PER i DA 2 A 1 FAI\n STAMPA i\nFINE PER\nSTAMPA \"fine\"","fine",new string[]{});
program("n = 3\nPER i DA 1 A n FAI\n n = 0\n STAMPA i\nFINE PER","1|2|3",new string[]{});
program("a = 1\nb = 2\nPER i DA a A b FAI\n STAMPA i\nFINE PER","1|2",new string[]{});
program("limite_ciclo_1 = 17\nPER i DA 1 A 2 FAI\n STAMPA i\nFINE PER\nSTAMPA limite_ciclo_1","1|2|17",new string[]{});
program("PER i DA 1 A 2 FAI\n PER j DA 1 A 2 FAI\n  STAMPA i * 10 + j\n FINE PER\nFINE PER","11|12|21|22",new string[]{});
program("v = [4,8,12]\nPER i DA 0 A len(v)-1 FAI\n STAMPA v[i]\nFINE PER","4|8|12",new string[]{});
program("PER i DA max(1,2) A min(3,4) FAI\n STAMPA i\nFINE PER","2|3",new string[]{});
program("PER i DA 1 A 3 FAI\nFINE PER\nSTAMPA i","4",new string[]{});
rejectCode("PER i DA 1 A 2 PASSO 0 FAI\nFINE PER");
program("x=1\nPER i DA 1 A 2 PASSO x FAI\nSTAMPA i\nFINE PER","1|2",new string[]{});
rejectCode("PER i DA 1 A 2 PASSO 1.5 FAI\nFINE PER");
rejectCode("PER i DA 1 A 2 FAI");
rejectCode("FINE PER");
rejectCode("LEGGI v[0][1]");
rejectCode("1 + 2 = 3");
rejectCode("PER i DA 1 A 2 PASSO 999999999999999 FAI\nFINE PER");
 var r=run(FlowPseudocode.Decode("v = [1]\nv[0] = 2; x = 1/0"),new string[]{});eq(RuntimeState.Error,r.State);eq("[1]",ValueFormatter.Format(r.Variables["v"]));
 r=run(FlowPseudocode.Decode("v = [1]\nv[0] = \"a\""),new string[]{});eq(RuntimeState.Error,r.State);eq("[1]",ValueFormatter.Format(r.Variables["v"]));
 foreach(string s in new[]{"v[0] = 1","v = [1]\nv[i] = 2","LEGGI v[0]"})eq(RuntimeState.Error,new Interpreter(FlowPseudocode.Decode(s)).State);
 Console.WriteLine("CAPABILITIES PASS "+count);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}}