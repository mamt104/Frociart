package it.frociart.app;
import java.util.*;
public class CapabilityTests {
 static int count;static void eq(Object a,Object b){count++;if(!Objects.equals(a,b))throw new AssertionError("Expected "+a+" actual "+b);}
 static String fmt(String e){return FlowEngine.evaluate(e,null).format();}
 static void rejectExpr(String s){boolean error=false;try{fmt(s);}catch(IllegalArgumentException e){error=true;}eq(true,error);}
 static void rejectCode(String s){boolean error=false;try{FlowPseudocode.decode(s);}catch(IllegalArgumentException e){error=true;}eq(true,error);}
 static FlowEngine.Runner run(FlowModel.Document d,String[] inputs){FlowEngine.Runner r=new FlowEngine.Runner(d);int input=0;for(int i=0;i<1000&&r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR;i++){if(r.state==FlowEngine.State.WAITING){if(input>=inputs.length)throw new AssertionError("Unexpected input");eq(true,r.submitInput(inputs[input++]));}else r.step();}return r;}
 static void program(String s,String expected,String[] inputs){FlowModel.Document d=FlowPseudocode.decode(s);for(int n=0;n<3;n++){FlowEngine.Runner r=run(d,inputs);eq(FlowEngine.State.COMPLETED,r.state);eq(expected,String.join("|",r.output));d=n==0?FlowPseudocode.decode(FlowPseudocode.encode(d)):FlowCodec.decode(FlowCodec.encode(d));}}
 public static void main(String[] args){eq("[10, 20, 30]",fmt("[10,20,30]"));
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
program("v = [1,2,3]\nv[1] = 9\nSTAMPA v","[1, 9, 3]",new String[]{});
program("v = [1]\nb = v\nv[0] = 9\nSTAMPA b","[1]",new String[]{});
program("v = [1,2]\ni = 1\nLEGGI v[min(i, 0)], \"Valore?\"\nSTAMPA v","[7, 2]",new String[]{"7"});
program("V = [1]\nv[0] = 5\nSTAMPA V[0]","5",new String[]{});
program("totale = 0\nPER i DA 1 A 5 FAI\n totale = totale + i\nFINE PER\nSTAMPA totale","15",new String[]{});
program("FOR i = 3 TO 1 STEP -1 DO\n STAMPA i\nEND FOR","3|2|1",new String[]{});
program("PER i DA 1 A 5 PASSO 2 FAI\n STAMPA i\nFINE PER","1|3|5",new String[]{});
program("PER i DA 2 A 1 FAI\n STAMPA i\nFINE PER\nSTAMPA \"fine\"","fine",new String[]{});
program("n = 3\nPER i DA 1 A n FAI\n n = 0\n STAMPA i\nFINE PER","1|2|3",new String[]{});
program("a = 1\nb = 2\nPER i DA a A b FAI\n STAMPA i\nFINE PER","1|2",new String[]{});
program("limite_ciclo_1 = 17\nPER i DA 1 A 2 FAI\n STAMPA i\nFINE PER\nSTAMPA limite_ciclo_1","1|2|17",new String[]{});
program("PER i DA 1 A 2 FAI\n PER j DA 1 A 2 FAI\n  STAMPA i * 10 + j\n FINE PER\nFINE PER","11|12|21|22",new String[]{});
program("v = [4,8,12]\nPER i DA 0 A len(v)-1 FAI\n STAMPA v[i]\nFINE PER","4|8|12",new String[]{});
program("PER i DA max(1,2) A min(3,4) FAI\n STAMPA i\nFINE PER","2|3",new String[]{});
program("PER i DA 1 A 3 FAI\nFINE PER\nSTAMPA i","4",new String[]{});
rejectCode("PER i DA 1 A 2 PASSO 0 FAI\nFINE PER");
program("x=1\nPER i DA 1 A 2 PASSO x FAI\nSTAMPA i\nFINE PER","1|2",new String[]{});
rejectCode("PER i DA 1 A 2 PASSO 1.5 FAI\nFINE PER");
rejectCode("PER i DA 1 A 2 FAI");
rejectCode("FINE PER");
rejectCode("LEGGI v[0][1]");
rejectCode("1 + 2 = 3");
rejectCode("PER i DA 1 A 2 PASSO 999999999999999 FAI\nFINE PER");
 FlowEngine.Runner r=run(FlowPseudocode.decode("v = [1]\nv[0] = 2; x = 1/0"),new String[]{});eq(FlowEngine.State.ERROR,r.state);eq("[1]",r.variables.get("v").format());
 r=run(FlowPseudocode.decode("v = [1]\nv[0] = \"a\""),new String[]{});eq(FlowEngine.State.ERROR,r.state);eq("[1]",r.variables.get("v").format());
 for(String s:new String[]{"v[0] = 1","v = [1]\nv[i] = 2","LEGGI v[0]"})eq(FlowEngine.State.ERROR,new FlowEngine.Runner(FlowPseudocode.decode(s)).state);
 System.out.println("CAPABILITIES PASS "+count);}
}