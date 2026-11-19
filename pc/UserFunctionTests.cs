using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Frociart.Tests
{
    public static class UserFunctionTests
    {
        private static int checks,failed;
        private static readonly List<string> cases=new List<string>();
        public static int Main()
        {
            Test("Metadata, persistence and isolated snapshots",Persistence);
            Test("Calls, local variables and typed parameters",Calls);
            Test("Recursive factorial and mutually recursive functions",Recursion);
            Test("Input suspension, expression order and atomic assignments",Suspension);
            Test("Vectors, indexed calls and value semantics",Vectors);
            Test("Validation and runtime diagnostics",Errors);
            Test("Depth, global step and retained memory limits",Limits);
            string destination=Environment.GetEnvironmentVariable("FUNCTION_CASES");
            if(destination!=null)File.WriteAllLines(destination,cases,new UTF8Encoding(false));
            string roundtrip=Environment.GetEnvironmentVariable("ANDROID_FUNCTION_ROUNDTRIP");
            if(roundtrip!=null)Test("Android saved projects return to Windows",()=>CheckAndroidRoundtrip(roundtrip));
            Console.WriteLine(checks+" checks; "+failed+" failed groups.");return failed==0?0:1;
        }
        private static void Test(string name,Action run){try{run();Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex);}}
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        private static void Equal(object expected,object actual,string message){Check(Object.Equals(expected,actual),message+": expected "+expected+", got "+actual);}
        private static FlowNode Node(NodeKind kind,string text){return new FlowNode{Kind=kind,Text=text};}
        private static Diagram Linear(params FlowNode[] nodes)
        {
            var d=new Diagram();d.Nodes.Add(Node(NodeKind.Start,"Inizio"));d.Nodes.AddRange(nodes);d.Nodes.Add(Node(NodeKind.End,"Fine"));
            for(int i=1;i<d.Nodes.Count;i++)d.Edges.Add(new FlowEdge{From=d.Nodes[i-1].Id,To=d.Nodes[i].Id});return d;
        }
        private static FlowFunction Function(string name,FlowDataType type,Diagram body,params FlowParameter[] parameters)
        {
            return new FlowFunction{Name=name,ReturnType=type,ReturnVariable=type==FlowDataType.Void?"":"risultato",Body=body,Parameters=parameters.ToList()};
        }
        private static FlowParameter Parameter(string name,FlowDataType type=FlowDataType.Auto,bool array=false){return new FlowParameter{Name=name,Type=type,IsArray=array};}
        private static Diagram Project(Diagram main,params FlowFunction[] functions){main.Version=2;main.Functions=functions.ToList();return main;}
        private static Interpreter Run(Diagram project,params string[] inputs)
        {
            var errors=Validator.Validate(project).Where(i=>i.Severity==Severity.Error).ToList();
            Check(errors.Count==0,"valid project: "+String.Join(";",errors.Select(i=>i.Message)));
            var runner=new Interpreter(project);Drain(runner,inputs);
            cases.Add(Convert.ToBase64String(DiagramIO.EncodeDocument(project))+"\t"+B64(String.Join("\u001f",inputs))+"\t"+runner.State+"\t"+B64(String.Join("\u001e",runner.Output))+"\t"+runner.Steps+"\t"+B64(VariableText(runner.Variables)));
            return runner;
        }
        private static string B64(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
        private static string UnB64(string value){return Encoding.UTF8.GetString(Convert.FromBase64String(value));}
        private static string VariableText(IDictionary<string,object> variables){return String.Join("\u001e",variables.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(p=>p.Key+"\u001d"+ValueFormatter.Format(p.Value)));}
        private static void CheckAndroidRoundtrip(string directory){for(int i=0;i<cases.Count;i++){var columns=cases[i].Split('\t');var p=DiagramIO.Load(Path.Combine(directory,i+".frociart"));var r=new Interpreter(p);string input=UnB64(columns[1]);Drain(r,input.Length==0?new string[0]:input.Split('\u001f'));Equal(columns[2],r.State.ToString(),"Android state "+i);Equal(UnB64(columns[3]),String.Join("\u001e",r.Output),"Android output "+i);Equal(UnB64(columns[5]),VariableText(r.Variables),"Android variables "+i);}}
        private static void Drain(Interpreter runner,params string[] inputs)
        {
            int input=0,guard=0;
            while(runner.State!=RuntimeState.Completed && runner.State!=RuntimeState.Error)
            {
                Check(++guard<30000,"bounded execution");
                if(runner.State==RuntimeState.WaitingForInput)
                {
                    Check(input<inputs.Length,"expected input available");Check(runner.ProvideInput(inputs[input++]),"valid input accepted: "+runner.LastMessage);
                }
                else runner.Step();
            }
        }
        private static void RejectFile(Diagram project,string message)
        {
            bool rejected=false;try{DiagramIO.EncodeDocument(project);}catch(InvalidDataException){rejected=true;}Check(rejected,message);
        }
        private static void Invalid(Diagram project,string fragment)
        {
            var errors=Validator.Validate(project).Where(i=>i.Severity==Severity.Error).ToList();
            Check(errors.Any(e=>e.Message.IndexOf(fragment,StringComparison.OrdinalIgnoreCase)>=0),"diagnostic: "+fragment+" actual: "+String.Join(";",errors.Select(e=>e.Message)));
            Equal(RuntimeState.Error,new Interpreter(project).State,"invalid project cannot execute");
        }
        private static void Persistence()
        {
            var f=Function("doppio",FlowDataType.Real,Linear(Node(NodeKind.Process,"risultato = valore * 2")),Parameter("valore",FlowDataType.Real));
            var p=Project(Linear(Node(NodeKind.Output,"doppio(4)")),f);
            byte[] bytes=DiagramIO.EncodeDocument(p);Check(Encoding.UTF8.GetString(bytes).Contains("\"Version\":2"),"format 2 persisted");
            var decoded=DiagramIO.DecodeDocument(bytes);Equal(1,decoded.Functions.Count,"function survives JSON");Equal(f.Id,decoded.Functions[0].Id,"stable function ID");
            Equal("8",Run(decoded).Output.Single(),"loaded execution");
            var cloned=DiagramIO.Clone(p);cloned.Functions[0].Parameters[0].Name="altra";cloned.Functions[0].Body.Nodes[1].Text="risultato = 7";
            Equal("valore",f.Parameters[0].Name,"parameter copy isolated");Equal("risultato = valore * 2",f.Body.Nodes[1].Text,"body copy isolated");
            var r=new Interpreter(p);f.Body.Nodes[1].Text="risultato = 999";Drain(r);Equal("8",r.Output.Single(),"running snapshot isolated");
            var old=Linear(Node(NodeKind.Output,"1"));byte[] oldBytes=DiagramIO.EncodeDocument(old);Check(!Encoding.UTF8.GetString(oldBytes).Contains("Functions"),"legacy format unchanged");
            Equal(1,DiagramIO.DecodeDocument(oldBytes).Version,"legacy projects supported");
            p.Version=1;RejectFile(p,"functions require format guard");p.Version=2;
            p.Functions[0].Body.Functions=new List<FlowFunction>{p.Functions[0]};RejectFile(p,"cyclic nested function rejected before traversal");p.Functions[0].Body.Functions=null;
            p.Functions[0].Body.Nodes[0].Id=p.Nodes[0].Id;RejectFile(p,"global node IDs cannot collide");
        }
        private static void Calls()
        {
            var f=Function("sommaDue",FlowDataType.Real,Linear(Node(NodeKind.Process,"risultato = a + b; a = 99")),Parameter("a",FlowDataType.Real),Parameter("b",FlowDataType.Real));
            var hello=Function("saluta",FlowDataType.Void,Linear(Node(NodeKind.Output,"'Ciao ' + nome")),Parameter("nome",FlowDataType.Text));
            var zero=Function("costante",FlowDataType.Integer,Linear(Node(NodeKind.Process,"risultato = 7")));
            var p=Project(Linear(Node(NodeKind.Process,"a = 4; b = 5; x = sommaDue(a,b) + costante()"),Node(NodeKind.Call,"saluta('Ada')"),Node(NodeKind.Call,"costante()"),Node(NodeKind.Output,"[a,b,x]")),f,hello,zero);
            var r=Run(p);Equal(RuntimeState.Completed,r.State,"calls complete");Equal("Ciao Ada",r.Output[0],"void function side effect");Equal("[4, 5, 16]",r.Output[1],"locals do not escape");
            Check(!r.Variables.ContainsKey("risultato"),"return variable remains local");Equal(1,r.CallStack.Count,"all calls returned");
            r.Reset();Equal(0,r.Output.Count,"reset output");Equal(0,r.Steps,"reset steps");Equal(0,r.Variables.Count,"reset variables");Drain(r);Equal("[4, 5, 16]",r.Output[1],"repeat same project");
            var r2=new Interpreter(p);r2.Step();r2.Step();Check(r2.CallStack.Count==2,"call frame enters");Equal(f.Id,r2.CurrentFunctionId,"current function identity");
            var snapshot=r2.CallStack;Check(snapshot[1].Variables.IsReadOnly,"read-only call stack values");Equal(4.0,snapshot[1].Variables["a"],"argument visible");
            var body=r2.CurrentDiagram;body.Nodes.Clear();r2.Step();Check(r2.CurrentDiagram.Nodes.Count>0,"public diagram snapshot isolated");
            Drain(r2);Equal(4.0,snapshot[1].Variables["a"],"call stack snapshot not live-mutated");
        }
        private static Diagram Branch(string condition,string yes,string no)
        {
            var d=Linear(Node(NodeKind.Decision,condition),Node(NodeKind.Process,yes),Node(NodeKind.Process,no));
            d.Edges.Clear();var n=d.Nodes;
            d.Edges.Add(new FlowEdge{From=n[0].Id,To=n[1].Id});
            d.Edges.Add(new FlowEdge{From=n[1].Id,To=n[2].Id,Branch="true"});d.Edges.Add(new FlowEdge{From=n[1].Id,To=n[3].Id,Branch="false"});
            d.Edges.Add(new FlowEdge{From=n[2].Id,To=n[4].Id});d.Edges.Add(new FlowEdge{From=n[3].Id,To=n[4].Id});return d;
        }
        private static void Recursion()
        {
            var fact=Function("fattoriale",FlowDataType.Integer,Branch("n <= 1","risultato = 1","risultato = n * fattoriale(n - 1)"),Parameter("n",FlowDataType.Integer));
            var r=Run(Project(Linear(Node(NodeKind.Output,"fattoriale(0)"),Node(NodeKind.Output,"fattoriale(8) + fattoriale(4)")),fact));
            Equal("1",r.Output[0],"base case");Equal("40344",r.Output[1],"recursive nested expression");
            var even=Function("pari",FlowDataType.Boolean,Branch("n = 0","risultato = vero","risultato = dispari(n-1)"),Parameter("n",FlowDataType.Integer));
            var odd=Function("dispari",FlowDataType.Boolean,Branch("n = 0","risultato = falso","risultato = pari(n-1)"),Parameter("n",FlowDataType.Integer));
            r=Run(Project(Linear(Node(NodeKind.Output,"pari(10)"),Node(NodeKind.Output,"dispari(10)"),Node(NodeKind.Output,"dispari(9)")),even,odd));
            Equal("vero,falso,vero",String.Join(",",r.Output),"mutual recursion");
            var body=Branch("n=0","risultato=0","risultato=valore+sommaLetti(n-1)");
            var input=Node(NodeKind.Input,"valore");var no=body.Edges.Single(e=>e.Branch=="false");string destination=no.To;no.To=input.Id;body.Nodes.Add(input);body.Edges.Add(new FlowEdge{From=input.Id,To=destination});
            var sum=Function("sommaLetti",FlowDataType.Integer,body,Parameter("n",FlowDataType.Integer));
            r=Run(Project(Linear(Node(NodeKind.Output,"sommaLetti(2)")),sum),"4","5");Equal("9",r.Output.Single(),"recursive local inputs resume independently");
        }
        private static void Suspension()
        {
            var emit=Function("traccia",FlowDataType.Integer,Linear(Node(NodeKind.Output,"n"),Node(NodeKind.Process,"risultato=n")),Parameter("n",FlowDataType.Integer));
            var read=Function("chiedi",FlowDataType.Integer,Linear(Node(NodeKind.Input,"risultato")));
            var p=Project(Linear(Node(NodeKind.Process,"a=9"),Node(NodeKind.Process,"a=traccia(1); b=traccia(2)+chiedi()+traccia(3)"),Node(NodeKind.Output,"a+b")),emit,read);
            var r=new Interpreter(p);int guard=0;while(r.State!=RuntimeState.WaitingForInput&&r.State!=RuntimeState.Error){r.Step();Check(++guard<100,"wait for function input");}
            Equal(read.Id,r.CurrentFunctionId,"input in callee");Equal("risultato",r.InputVariable,"callee input name");
            Equal("1,2",String.Join(",",r.Output),"completed subcalls occur once before input");Equal(9.0,r.CallStack[0].Variables["a"],"caller assignment remains atomic");
            Check(r.ProvideInput("4"),"input accepted");Drain(r);Equal("1,2,3,10",String.Join(",",r.Output),"resume without repeating calls");
            r=Run(Project(Linear(Node(NodeKind.Output,"false and traccia(8)"),Node(NodeKind.Output,"true or traccia(9)")),emit));
            Equal("falso,vero",String.Join(",",r.Output),"short circuit skips user calls");
            p=Project(Linear(Node(NodeKind.Process,"a=10"),Node(NodeKind.Process,"a=traccia(1); b=1/0")),emit);r=Run(p);
            Equal(RuntimeState.Error,r.State,"transaction error");Equal(10.0,r.Variables["a"],"failed block rolls back assignments");Equal("1",r.Output.Single(),"completed output not rolled back or repeated");
        }
        private static void Vectors()
        {
            var change=Function("modifica",FlowDataType.Integer,Linear(Node(NodeKind.Process,"v[0]=99; risultato=v[0]")),Parameter("v",FlowDataType.Integer,true));
            var index=Function("indice",FlowDataType.Integer,Linear(Node(NodeKind.Process,"risultato=1")));
            var vector=Function("crea",FlowDataType.Text,Linear(Node(NodeKind.Process,"risultato=['a','b']")));vector.ReturnIsArray=true;
            var p=Project(Linear(Node(NodeKind.Process,"v=[2,3]; x=modifica(v); v[indice()]=x"),Node(NodeKind.Input,"v[indice()]"),Node(NodeKind.Output,"v"),Node(NodeKind.Output,"crea()[indice()]")),change,index,vector);
            var r=Run(p,"7");Equal("[2, 7]",r.Output[0],"vector argument copy-on-write and input index");Equal("b",r.Output[1],"vector return and nested indexing");
            var input=Function("chiediIndice",FlowDataType.Integer,Linear(Node(NodeKind.Input,"risultato")));
            p=Project(Linear(Node(NodeKind.Process,"v=[10,20]; v[chiediIndice()] = modifica(v)"),Node(NodeKind.Output,"v")),change,input);
            r=Run(p,"1");Equal("[10, 99]",r.Output.Single(),"assignment RHS preserved during suspended index");
        }
        private static void Errors()
        {
            var f=Function("calcola",FlowDataType.Integer,Linear(Node(NodeKind.Process,"risultato=x")),Parameter("x",FlowDataType.Integer));
            Invalid(Project(Linear(Node(NodeKind.Output,"calcola()")),f),"argomenti");
            Invalid(Project(Linear(Node(NodeKind.Output,"sconosciuta(3)")),f),"sconosciuta");
            var r=Run(Project(Linear(Node(NodeKind.Output,"calcola(1.5)")),f));Equal(RuntimeState.Error,r.State,"fraction rejected by integer parameter");Check(r.Error.Contains("intero"),"typed diagnostic");
            f.Body.Nodes[1].Text="risultato='testo'";r=Run(Project(Linear(Node(NodeKind.Output,"calcola(1)")),f));Equal(RuntimeState.Error,r.State,"wrong return type rejected");
            f.Body.Nodes[1].Text="x=2.5; risultato=x";r=Run(Project(Linear(Node(NodeKind.Output,"calcola(1)")),f));Equal(RuntimeState.Error,r.State,"parameter remains typed on assignment");
            f.Body=Branch("x>0","risultato=x","altro=x");var p=Project(Linear(Node(NodeKind.Output,"calcola(1)")),f);Invalid(p,"risultato");
            Check(Validator.Validate(p).Any(i=>i.Severity==Severity.Error&&i.FunctionId==f.Id),"diagnostic points to function");
            f=Function("saluta",FlowDataType.Void,Linear(Node(NodeKind.Output,"'Ciao'")));
            Invalid(Project(Linear(Node(NodeKind.Output,"saluta()")),f),"non restituisce");
            Invalid(Project(Linear(Node(NodeKind.Call,"1+2")),f),"chiamata");
            Invalid(Project(Linear(Node(NodeKind.Call,"min(1,saluta())")),f),"non restituisce");
            p=Project(Linear(),f);f.Name="sqrt";RejectFile(p,"built-in name reserved");f.Name="saluta";
            f.Parameters.Add(Parameter("x"));f.Parameters.Add(Parameter("X"));RejectFile(p,"duplicate parameter case-insensitive");f.Parameters.Clear();
            f.Parameters.Add(Parameter("x",FlowDataType.Void));RejectFile(p,"void parameter invalid");f.Parameters.Clear();
            var reader=Function("numero",FlowDataType.Integer,Linear(Node(NodeKind.Input,"x"),Node(NodeKind.Process,"risultato=x")),Parameter("x",FlowDataType.Integer));
            r=new Interpreter(Project(Linear(Node(NodeKind.Output,"numero(2)")),reader));int guard=0;while(r.State!=RuntimeState.WaitingForInput&&r.State!=RuntimeState.Error){r.Step();Check(++guard<50,"typed input reached");}
            Check(!r.ProvideInput("non numero"),"wrong typed input rejected");Equal(RuntimeState.WaitingForInput,r.State,"input can retry");Equal(2.0,r.Variables["x"],"invalid input preserves variable");Check(r.ProvideInput("7"),"typed input retry");Drain(r);Equal("7",r.Output.Single(),"typed input completes");
        }
        private static void Limits()
        {
            var f=Function("ricorsiva",FlowDataType.Integer,Linear(Node(NodeKind.Process,"risultato=ricorsiva()")));
            var p=Project(Linear(Node(NodeKind.Output,"ricorsiva()")),f);var r=Run(p);
            Equal(RuntimeState.Error,r.State,"recursive depth stops");Check(r.Error.Contains("128"),"depth diagnostic");Check(r.CallStack.Count<=128,"host stack not used for recursion");
            r=new Interpreter(p,12);Drain(r);Equal(12,r.Steps,"global step budget");Check(r.Error.Contains("12 blocchi"),"step budget applies to callees");
            var many=Function("ripeti",FlowDataType.Text,Branch("n=0","risultato=t","risultato=ripeti(n-1,t)"),Parameter("n",FlowDataType.Integer),Parameter("t",FlowDataType.Text));
            p=Project(Linear(Node(NodeKind.Process,"t='"+new string('a',2048)+"'; t=t+t; t=t+t; t=t+t; t=t+t"),Node(NodeKind.Output,"ripeti(1000,t)")),many);
            r=Run(p);Equal(RuntimeState.Error,r.State,"retained memory bounded");Check(r.Error.Contains("memoria"),"memory diagnostic");Check(r.CallStack.Count<128,"memory limit before maximum call depth");
        }
    }
}
