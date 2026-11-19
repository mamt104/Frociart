using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Frociart
{
    public sealed class Interpreter
    {
        private sealed class Graph
        {
            internal Diagram Diagram;
            internal FlowFunction Function;
            internal Dictionary<string,FlowNode> Nodes;
            internal Dictionary<string,CompiledBlock> Blocks;
            internal Dictionary<string,List<FlowEdge>> Outgoing;
            internal Graph(Diagram diagram,FlowFunction function,IDictionary<string,FlowFunction> catalog)
            {
                Diagram=diagram;Function=function;Nodes=diagram.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
                Blocks=diagram.Nodes.ToDictionary(n=>n.Id,n=>Compiler.Block(n,catalog,function),StringComparer.Ordinal);
                Outgoing=diagram.Nodes.ToDictionary(n=>n.Id,n=>new List<FlowEdge>(),StringComparer.Ordinal);
                foreach(var edge in diagram.Edges)Outgoing[edge.From].Add(edge);
            }
        }
        private sealed class Frame
        {
            internal Graph Graph;
            internal string NodeId;
            internal long Id, Visit;
            internal readonly Dictionary<string,object> Variables=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
            internal readonly IDictionary<string,object> ReadOnlyVariables;
            internal readonly Dictionary<string,FlowDeclaration> Declarations=new Dictionary<string,FlowDeclaration>(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<string,object> Transaction;
            internal readonly Dictionary<string,ArrayCell> ArrayBindings=new Dictionary<string,ArrayCell>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<ArrayCell,ArrayCell> ArrayOverlays=new Dictionary<ArrayCell,ArrayCell>();
            internal readonly Dictionary<string,ArrayCell> NewArrays=new Dictionary<string,ArrayCell>(StringComparer.OrdinalIgnoreCase);
            internal readonly HashSet<string> WholeWrites=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            internal ExpressionMachine Evaluation;
            internal int AssignmentIndex;
            internal object AssignmentValue;
            internal bool HasAssignmentValue,Counted;
            internal int? InputIndex;
            internal Frame(Graph graph)
            {
                Graph=graph;NodeId=graph.Diagram.Nodes.First(n=>n.Kind==NodeKind.Start).Id;
                ReadOnlyVariables=new ReadOnlyDictionary<string,object>(Variables);
            }
            internal void ClearPending(){Transaction=null;ArrayOverlays.Clear();NewArrays.Clear();WholeWrites.Clear();Evaluation=null;AssignmentIndex=0;AssignmentValue=null;HasAssignmentValue=false;Counted=false;InputIndex=null;}
        }
        private sealed class ArrayCell {internal object Value;internal FlowDataType Type;internal Frame Owner;internal string Name;internal ArrayCell Parent;}
        private readonly Dictionary<string,Graph> graphs=new Dictionary<string,Graph>(StringComparer.Ordinal);
        private readonly List<Frame> frames=new List<Frame>();
        private readonly List<string> output=new List<string>();
        private readonly List<bool> outputNewlines=new List<bool>();
        private readonly StringBuilder consoleOutput=new StringBuilder();private string cachedOutput="";
        private readonly int maxSteps;
        private readonly IDictionary<string,object> emptyVariables=new ReadOnlyDictionary<string,object>(new Dictionary<string,object>());
        private Graph main;
        private bool hasArrayReferences;
        private string initialError;
        private int outputCharacters;
        private long nextFrameId;
        public long Generation {get;private set;}
        public long CurrentFrameId {get{return Active==null?0:Active.Id;}}
        public long CurrentVisit {get{return Active==null?0:Active.Visit;}}
        public int CallDepth {get{return frames.Count;}}
        public bool CurrentBlockStarted {get{return Active!=null&&(Active.Counted||Active.Evaluation!=null||State==RuntimeState.WaitingForInput||State==RuntimeState.WaitingForFile);}}
        public bool HasFrame(long id){return frames.Any(f=>f.Id==id);}
        private Frame Active {get{return frames.Count==0?null:frames[frames.Count-1];}}

        public readonly FlowTextFiles Files=new FlowTextFiles();
        public readonly FlowTurtle Turtle=new FlowTurtle();
        public FlowFileRequest PendingFile {get;private set;}
        public RuntimeState State {get;private set;}
        public string CurrentNodeId {get{return Active==null?null:Active.NodeId;}}
        public string CurrentFunctionId {get{return Active==null||Active.Graph.Function==null?null:Active.Graph.Function.Id;}}
        public string CurrentFunctionName {get{return Active==null||Active.Graph.Function==null?"Principale":Active.Graph.Function.Name;}}
        public Diagram CurrentDiagram {get{return Active==null?null:DiagramIO.Clone(Active.Graph.Diagram);}}
        public string LastNodeId {get;private set;}
        public string LastFunctionId {get;private set;}
        public string LastEdgeId {get;private set;}
        public int Steps {get;private set;}
        public string LastMessage {get;private set;}
        public string Error {get;private set;}
        public string InputPrompt {get;private set;}
        public string InputVariable {get;private set;}
        public IDictionary<string,object> Variables {get{return Active==null?emptyVariables:Active.ReadOnlyVariables;}}
        public IList<string> Output {get;private set;}
        public IList<bool> OutputNewlines {get;private set;}
        public string OutputText {get{return cachedOutput??(cachedOutput=consoleOutput.ToString());}}
        private readonly FlowConsoleLog consoleLog=new FlowConsoleLog();
        public FlowConsoleLog ConsoleLog {get{return consoleLog;}}
        public IDictionary<string,string> VariableTypes {get{return DescribeTypes(Active);}}
        public IList<CallFrameSnapshot> CallStack
        {
            get{return frames.Select(CaptureFrame).ToList().AsReadOnly();}
        }
        // Headers never copy variable values/types. Inspect only the selected call,
        // rather than rebuilding every suspended frame on each UI refresh.
        public IList<CallFrameInfo> FrameInfos {get{return frames.Select(f=>new CallFrameInfo{FrameId=f.Id,FunctionId=f.Graph.Function==null?null:f.Graph.Function.Id,FunctionName=f.Graph.Function==null?"Principale":f.Graph.Function.Name,NodeId=f.NodeId,VariableCount=f.Variables.Count}).ToList().AsReadOnly();}}
        public CallFrameSnapshot InspectFrame(long id){var f=id==0?Active:frames.FirstOrDefault(frame=>frame.Id==id);return f==null?null:CaptureFrame(f);}
        private static CallFrameSnapshot CaptureFrame(Frame f){var function=f.Graph.Function;return new CallFrameSnapshot{FrameId=f.Id,FunctionId=function==null?null:function.Id,FunctionName=function==null?"Principale":function.Name,NodeId=f.NodeId,ParameterNames=(function==null?new List<string>():function.Parameters.Select(p=>p.Name).ToList()).AsReadOnly(),ResultName=function==null?"":function.ReturnVariable,Variables=new ReadOnlyDictionary<string,object>(new Dictionary<string,object>(f.Variables,StringComparer.OrdinalIgnoreCase)),VariableTypes=DescribeTypes(f)};}

        public Interpreter(Diagram diagram,int maxSteps=10000)
        {
            this.maxSteps=Math.Max(1,Math.Min(10000,maxSteps));Output=output.AsReadOnly();OutputNewlines=outputNewlines.AsReadOnly();
            var firstError=Validator.Validate(diagram).FirstOrDefault(p=>p.Severity==Severity.Error);
            if(firstError!=null)initialError=firstError.Message;
            else
            {
                var copy=DiagramIO.Clone(diagram);var catalog=FunctionModel.Catalog(copy);
                hasArrayReferences=catalog.Values.Any(f=>f.Parameters.Any(v=>v.ByReference));main=new Graph(copy,null,catalog);
                foreach(var f in catalog.Values)graphs.Add(f.Id,new Graph(f.Body,f,catalog));
            }
            Reset();
        }

        public void Reset(){Reset(false);}
        public void Reset(bool discardFileOutput)
        {
            if(Files.HasWrite&&!discardFileOutput)throw new FlowException("Il file generato non è ancora salvato. Salvalo oppure scegli esplicitamente di scartarlo prima di ricominciare.");
            Files.Reset();Turtle.Reset();PendingFile=null;
            foreach(var f in frames)f.Variables.Clear();
            frames.Clear();output.Clear();outputNewlines.Clear();consoleOutput.Clear();ConsoleLog.Clear();cachedOutput="";outputCharacters=0;Steps=0;Generation++;
            LastNodeId=null;LastFunctionId=null;LastEdgeId=null;Error=null;InputPrompt=null;InputVariable=null;
            State=RuntimeState.Ready;LastMessage="Pronto. Premi un passo per eseguire Inizio.";
            if(initialError!=null){Fail(initialError);return;}
            frames.Add(new Frame(main){Id=++nextFrameId});
        }

        private static string UiTurtleMessage(NodeKind kind){return kind==NodeKind.TurtleForward?"Tartaruga spostata.":kind==NodeKind.TurtleTurn?"Direzione della tartaruga aggiornata.":kind==NodeKind.TurtleHome?"Tartaruga tornata all’origine, senza traccia.":kind==NodeKind.TurtleClear?"Disegno pulito. Tartaruga all’origine.":"Colore della penna aggiornato.";}
        public bool Step()
        {
            if(State==RuntimeState.Error||State==RuntimeState.Completed||State==RuntimeState.WaitingForInput||State==RuntimeState.WaitingForFile)return false;
            var f=Active;
            if(!f.Counted && Steps>=maxSteps){Fail("Limite di "+maxSteps+" blocchi raggiunto. Il ciclo potrebbe non terminare: controlla condizione e aggiornamento delle variabili.");return false;}
            var node=f.Graph.Nodes[f.NodeId];var block=f.Graph.Blocks[f.NodeId];
            State=RuntimeState.Running;LastEdgeId=null;
            try
            {
                if(node.Kind!=NodeKind.Input && !f.Counted){Steps++;f.Counted=true;MarkLast(f);}
                object value;
                switch(node.Kind)
                {
                    case NodeKind.Start:
                        LastMessage="Inizio dell'esecuzione.";Advance(f,"next");return true;
                    case NodeKind.Pause:
                        LastMessage=node.Breakpoint?"Punto di pausa attraversato.":"Pausa disattivata.";Advance(f,"next");return true;
                    case NodeKind.Input:
                        f.InputIndex=null;InputVariable=block.InputVariable;
                        if(block.InputIndex!=null)
                        {
                            var vector=ReadVector(f.Variables,block.InputVariable);
                            if(!Evaluate(f,block.InputIndex,f.Variables,out value))return true;
                            f.InputIndex=vector.Index(value);InputVariable+="["+f.InputIndex.Value+"]";
                        }
                        InputPrompt=String.IsNullOrWhiteSpace(node.Prompt)||node.Prompt=="Inserisci un valore per x"?"Inserisci un valore per "+InputVariable:node.Prompt;
                        State=RuntimeState.WaitingForInput;LastMessage="In attesa di un valore per «"+InputVariable+"».";return false;
                    case NodeKind.TurtleForward:case NodeKind.TurtleTurn:case NodeKind.TurtleHome:case NodeKind.TurtleClear:case NodeKind.TurtlePen:
                        value=null;if(block.Expression!=null&&!Evaluate(f,block.Expression,f.Variables,out value))return true;
                        Turtle.Apply(block.Turtle,value);LastMessage=UiTurtleMessage(node.Kind);Advance(f,"next");return true;
                    case NodeKind.OpenFile:
                        if(block.FileReading?Files.HasRead:Files.HasWrite)throw new FlowException("Il canale è già aperto. Chiudilo prima di aprire un altro file.");
                        if(!Evaluate(f,block.Expression,f.Variables,out value))return true;
                        string fileName=FlowTextFiles.Name(value);
                        if(block.FileReading){PendingFile=new FlowFileRequest(FlowFileRequestKind.Read,fileName);State=RuntimeState.WaitingForFile;LastMessage="Scegli il file da leggere: «"+fileName+"».";return false;}
                        Files.OpenWrite(fileName);LastMessage="File «"+fileName+"» preparato. Sarà salvato alla chiusura.";Advance(f,"next");return true;
                    case NodeKind.ReadFile:
                        int? fileIndex=null;
                        if(block.InputIndex!=null){var vector=ReadVector(f.Variables,block.InputVariable);if(!Evaluate(f,block.InputIndex,f.Variables,out value))return true;fileIndex=vector.Index(value);}
                        string line=Files.PeekLine();
                        object readValue=TextInput(f,block.InputVariable,fileIndex.HasValue)?(object)line:Values.Input(line);
                        value=fileIndex.HasValue?ReadVector(f.Variables,block.InputVariable).Set(fileIndex.Value,readValue):readValue;
                        value=ConvertWrite(f,block.InputVariable,value);
                        f.Transaction=new Dictionary<string,object>(f.Variables,StringComparer.OrdinalIgnoreCase);StageWrite(f,block.InputVariable,value,block.InputIndex!=null);CheckMemory();CommitTransaction(f);
                        Files.CommitRead();LastMessage="Letta una riga in «"+block.InputVariable+"».";Advance(f,"next");return true;
                    case NodeKind.WriteFile:
                        if(!Files.HasWrite)throw new FlowException("Apri prima un file in scrittura.");
                        if(!Evaluate(f,block.Expression,f.Variables,out value))return true;
                        Files.Write(ValueFormatter.Format(value),block.FileNewline);LastMessage="Testo aggiunto al file «"+Files.WriteName+"» da salvare.";Advance(f,"next");return true;
                    case NodeKind.CloseFile:
                        if(block.FileReading){Files.CloseRead();LastMessage="File in lettura chiuso.";Advance(f,"next");return true;}
                        if(!Files.HasWrite)throw new FlowException("Nessun file aperto in scrittura da chiudere.");
                        RequestSave();return false;
                    case NodeKind.Process:
                        if(f.Transaction==null)f.Transaction=new Dictionary<string,object>(f.Variables,StringComparer.OrdinalIgnoreCase);
                        while(f.AssignmentIndex<block.Assignments.Count)
                        {
                            var assignment=block.Assignments[f.AssignmentIndex];
                            if(!f.HasAssignmentValue)
                            {
                                if(!Evaluate(f,assignment.Expression,f.Transaction,out value))return true;
                                f.AssignmentValue=value;f.HasAssignmentValue=true;
                            }
                            value=f.AssignmentValue;
                            if(assignment.Index!=null)
                            {
                                var vector=ReadVector(f.Transaction,assignment.Name);object index;
                                if(!Evaluate(f,assignment.Index,f.Transaction,out index))return true;
                                if(hasArrayReferences)vector=ReadVector(f.Transaction,assignment.Name);value=vector.Set(vector.Index(index),value);
                            }
                            value=ConvertWrite(f,assignment.Name,value);
                            StageWrite(f,assignment.Name,value,assignment.Index!=null);f.AssignmentValue=null;f.HasAssignmentValue=false;f.AssignmentIndex++;
                            CheckMemory();
                        }
                        CommitTransaction(f);
                        LastMessage=block.Assignments.Count==1?block.Assignments[0].Name+" = "+ValueFormatter.Format(f.Variables[block.Assignments[0].Name]):"Eseguite "+block.Assignments.Count+" assegnazioni.";
                        Advance(f,"next");return true;
                    case NodeKind.Declare:
                        int length=0;if(block.DeclarationSize!=null){if(!Evaluate(f,block.DeclarationSize,f.Variables,out value))return true;length=FlowDeclaration.Length(value);}
                        f.Transaction=new Dictionary<string,object>(f.Variables,StringComparer.OrdinalIgnoreCase);
                        foreach(string name in block.Declaration.Names)StageWrite(f,name,block.Declaration.Initial(length),false);
                        CheckMemory();CommitTransaction(f);
                        foreach(string name in block.Declaration.Names)f.Declarations[name]=block.Declaration;
                        LastMessage="Dichiarate: "+String.Join(", ",block.Declaration.Names)+" · "+FlowDeclaration.Label(block.Declaration.Type);Advance(f,"next");return true;
                    case NodeKind.Decision:
                        if(!Evaluate(f,block.Expression,f.Variables,out value))return true;
                        bool result=Values.Truth(value);LastMessage="Condizione "+(result?"vera":"falsa")+": segui il ramo "+node.GetBranchLabel(result?"true":"false")+".";
                        Advance(f,result?"true":"false");return true;
                    case NodeKind.Output:
                        if(!Evaluate(f,block.Expression,f.Variables,out value))return true;
                        string text=ValueFormatter.Format(value);
                        if(outputCharacters+text.Length>262144)throw new FlowException("L'uscita supera il limite di 256 KiB. Controlla eventuali stampe ripetute nel ciclo.");
                        if(output.Count>0&&outputNewlines[outputNewlines.Count-1])consoleOutput.Append(Environment.NewLine);consoleOutput.Append(text);cachedOutput=null;output.Add(text);outputNewlines.Add(block.OutputNewline);outputCharacters+=text.Length;ConsoleLog.Add(FlowConsoleLog.EntryKind.Output,text,null,null,node.Id,CurrentFunctionId,CurrentFunctionName,Steps,block.OutputNewline);LastMessage="Uscita: "+(text.Length>100?text.Substring(0,97)+"...":text);
                        Advance(f,"next");return true;
                    case NodeKind.Call:
                        if(!Evaluate(f,block.Expression,f.Variables,out value))return true;
                        LastMessage="Chiamata completata.";Advance(f,"next");return true;
                    case NodeKind.End:
                        MarkLast(f);
                        if(f.Graph.Function==null){if(Files.HasWrite){RequestSave();return false;}if(Files.HasRead)Files.CloseRead();State=RuntimeState.Completed;LastMessage="Esecuzione completata in "+Steps+" blocchi.";return true;}
                        value=null;var function=f.Graph.Function;
                        if(function.ReturnType!=FlowDataType.Void)
                        {
                            if(!Evaluate(f,block.Expression,f.Variables,out value))return true;
                            FunctionModel.CheckType(value,function.ReturnType,function.ReturnIsArray,"Il risultato di «"+function.Name+"»");
                        }
                        frames.RemoveAt(frames.Count-1);Active.Evaluation.Receive(value);CheckMemory();
                        LastMessage="Ritorno da «"+function.Name+"»"+(function.ReturnType==FlowDataType.Void?".":": "+ValueFormatter.Format(value));return true;
                    default:throw new FlowException("Tipo di blocco non supportato.");
                }
            }
            catch(FlowException ex){Fail(ex.Message);return false;}
            catch(OverflowException){Fail("Il valore numerico è troppo grande.");return false;}
        }

        private bool Evaluate(Frame caller,Expr expression,IDictionary<string,object> variables,out object value)
        {
            value=null;if(caller.Evaluation==null)caller.Evaluation=new ExpressionMachine(expression,Files.Eof,hasArrayReferences?(Func<string,object>)(name=>ReferenceArgument(caller,variables,name)):null);
            if(caller.Evaluation.Advance(variables))
            {
                value=caller.Evaluation.Result;caller.Evaluation=null;return true;
            }
            if(frames.Count>=FunctionModel.MaxCallDepth)throw new FlowException("Troppe chiamate annidate: massimo 128 livelli. Controlla la condizione di arresto della ricorsione.");
            var function=caller.Evaluation.RequestedFunction;
            var next=new Frame(graphs[function.Id]){Id=++nextFrameId};
            for(int i=0;i<function.Parameters.Count;i++)
            {
                var parameter=function.Parameters[i];var argument=caller.Evaluation.Arguments[i];
                FunctionModel.CheckType(argument,parameter.Type,parameter.IsArray,"Il parametro «"+parameter.Name+"» di «"+function.Name+"»");
                if(parameter.ByReference){var cell=caller.Evaluation.ArgumentReferences[i] as ArrayCell;if(cell==null)throw new FlowException("Parametro per riferimento non valido.");if(cell.Type!=FlowDataType.Auto&&parameter.Type!=FlowDataType.Auto&&cell.Type!=parameter.Type)throw new FlowException("Il tipo del vettore per riferimento «"+parameter.Name+"» deve coincidere con quello originale.");if(cell.Type==FlowDataType.Auto&&parameter.Type!=FlowDataType.Auto)cell.Type=parameter.Type;argument=cell.Value;FunctionModel.CheckType(argument,parameter.Type,true,"Il parametro «"+parameter.Name+"»");next.ArrayBindings.Add(parameter.Name,cell);}
                next.Variables.Add(parameter.Name,argument);
            }
            frames.Add(next);CheckMemory();InputPrompt=null;InputVariable=null;
            LastMessage="Chiamata a «"+function.Name+"».";return false;
        }

        private FlowDataType ArrayType(Frame owner,string name){FlowDeclaration declaration;if(owner.Declarations.TryGetValue(name,out declaration))return declaration.Type;var parameter=owner.Graph.Function==null?null:owner.Graph.Function.Parameters.FirstOrDefault(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));return parameter==null?FlowDataType.Auto:parameter.Type;}
        private ArrayCell ReferenceArgument(Frame owner,IDictionary<string,object> variables,string name){
            object value;if(!variables.TryGetValue(name,out value))throw new FlowException("Il vettore «"+name+"» non ha ancora un valore.");VectorValue.Require(value);ArrayCell cell;
            if(owner.Transaction!=null&&Object.ReferenceEquals(variables,owner.Transaction)&&(owner.WholeWrites.Contains(name)||!owner.Variables.ContainsKey(name))){if(!owner.NewArrays.TryGetValue(name,out cell)){cell=new ArrayCell{Value=value,Type=ArrayType(owner,name),Owner=owner,Name=name};owner.NewArrays.Add(name,cell);}return cell;}
            if(!owner.ArrayBindings.TryGetValue(name,out cell)){object original=owner.Variables[name];VectorValue.Require(original);cell=new ArrayCell{Value=original,Type=ArrayType(owner,name),Owner=owner,Name=name};owner.ArrayBindings.Add(name,cell);}
            if(owner.Transaction!=null&&Object.ReferenceEquals(variables,owner.Transaction)){
                ArrayCell overlay;if(owner.ArrayOverlays.TryGetValue(cell,out overlay))return overlay;
                if(!Object.ReferenceEquals(value,cell.Value)){overlay=Overlay(owner,cell,value);return overlay;}
            }return cell;
        }
        private ArrayCell Overlay(Frame owner,ArrayCell parent,object value){ArrayCell overlay;if(!owner.ArrayOverlays.TryGetValue(parent,out overlay)){overlay=new ArrayCell{Value=value,Type=parent.Type,Owner=owner,Parent=parent};owner.ArrayOverlays.Add(parent,overlay);}return overlay;}
        private void Propagate(ArrayCell cell){
            foreach(var frame in frames){foreach(var pair in frame.ArrayBindings.Where(p=>Object.ReferenceEquals(p.Value,cell)).ToArray()){frame.Variables[pair.Key]=cell.Value;if(frame.Transaction!=null&&!frame.WholeWrites.Contains(pair.Key)){ArrayCell overlay;frame.Transaction[pair.Key]=frame.ArrayOverlays.TryGetValue(cell,out overlay)?overlay.Value:cell.Value;}}
                if(frame.Transaction!=null){foreach(var pair in frame.NewArrays.Where(p=>Object.ReferenceEquals(p.Value,cell)))frame.Transaction[pair.Key]=cell.Value;foreach(var pair in frame.ArrayOverlays.Where(p=>Object.ReferenceEquals(p.Value,cell)))foreach(var alias in frame.ArrayBindings.Where(p=>Object.ReferenceEquals(p.Value,pair.Key)))if(!frame.WholeWrites.Contains(alias.Key))frame.Transaction[alias.Key]=cell.Value;}}
        }
        private void CheckCell(ArrayCell cell,object value){var vector=VectorValue.Require(value);if(vector.Count!=VectorValue.Require(cell.Value).Count)throw new FlowException("Un vettore per riferimento conserva la dimensione originale.");FunctionModel.CheckType(value,cell.Type,true,"Il vettore originale");foreach(var frame in frames)foreach(var pair in frame.ArrayBindings.Where(p=>Object.ReferenceEquals(p.Value,cell))){FlowDeclaration declaration;if(frame.Declarations.TryGetValue(pair.Key,out declaration))FunctionModel.CheckType(value,declaration.Type,true,"Il vettore «"+pair.Key+"»");CheckParameter(frame,pair.Key,value);}}
        private void StageWrite(Frame frame,string name,object value,bool indexed){
            if(!hasArrayReferences){frame.Transaction[name]=value;return;}
            if(!indexed){frame.Transaction[name]=value;frame.WholeWrites.Add(name);frame.NewArrays.Remove(name);return;}
            ArrayCell cell;if(frame.WholeWrites.Contains(name)&&frame.NewArrays.TryGetValue(name,out cell)){CheckCell(cell,value);cell.Value=value;Propagate(cell);return;}
            if(!frame.WholeWrites.Contains(name)&&frame.ArrayBindings.TryGetValue(name,out cell)){ArrayCell overlay=Overlay(frame,cell,frame.Transaction[name]);CheckCell(overlay,value);overlay.Value=value;Propagate(overlay);return;}frame.Transaction[name]=value;
        }
        private void CommitTransaction(Frame frame){
            if(!hasArrayReferences){frame.Variables.Clear();foreach(var pair in frame.Transaction)frame.Variables.Add(pair.Key,pair.Value);return;}
            foreach(var pair in frame.ArrayOverlays)CheckCell(pair.Key,pair.Value.Value);
            foreach(var pair in frame.NewArrays)CheckCell(pair.Value,pair.Value.Value);
            foreach(string name in frame.WholeWrites)frame.ArrayBindings.Remove(name);
            foreach(var pair in frame.ArrayOverlays){pair.Key.Value=pair.Value.Value;Propagate(pair.Key);}
            foreach(var pair in frame.Transaction)frame.Variables[pair.Key]=pair.Value;
            foreach(var pair in frame.NewArrays)frame.ArrayBindings[pair.Key]=pair.Value;
        }

        private void RequestSave(){PendingFile=new FlowFileRequest(FlowFileRequestKind.Save,Files.WriteName,Files.OutputDraft);State=RuntimeState.WaitingForFile;LastMessage="Salva il file «"+Files.WriteName+"» per continuare.";}
        public bool ProvideFile(FlowFileRequest request,string readText=null)
        {
            if(request==null||!Object.ReferenceEquals(request,PendingFile)||State!=RuntimeState.WaitingForFile)return false;
            try
            {
                if(request.Kind==FlowFileRequestKind.Read)Files.OpenRead(request.Name,readText);else Files.Saved();
                var f=Active;PendingFile=null;State=RuntimeState.Running;LastMessage=request.Kind==FlowFileRequestKind.Read?"File pronto per la lettura.":"File salvato.";
                if(f.Graph.Nodes[f.NodeId].Kind==NodeKind.End){if(Files.HasRead)Files.CloseRead();State=RuntimeState.Completed;LastMessage="Esecuzione completata in "+Steps+" blocchi.";}else Advance(f,"next");return true;
            }
            catch(FlowException ex){LastMessage=ex.Message;return false;}
        }
        public bool FileDeferred(FlowFileRequest request,string message)
        {
            if(request==null||!Object.ReferenceEquals(request,PendingFile)||State!=RuntimeState.WaitingForFile)return false;
            LastMessage=String.IsNullOrWhiteSpace(message)?"Operazione annullata. Puoi riprovare; il file generato è ancora disponibile.":message;return true;
        }

        public bool ProvideInput(string raw)
        {
            if(State!=RuntimeState.WaitingForInput)return false;
            if(Steps>=maxSteps){Fail("Limite di esecuzione raggiunto.");return false;}
            var f=Active;var block=f.Graph.Blocks[f.NodeId];
            try
            {
                if(raw!=null&&raw.Length>Values.MaxStringLength)throw new FlowException("Il valore inserito è troppo lungo.");
                object entered=TextInput(f,block.InputVariable,f.InputIndex.HasValue)?(object)(raw??""):Values.Input(raw);
                object value=f.InputIndex.HasValue?ReadVector(f.Variables,block.InputVariable).Set(f.InputIndex.Value,entered):entered;
                value=ConvertWrite(f,block.InputVariable,value);entered=f.InputIndex.HasValue?((VectorValue)value).At(f.InputIndex.Value):value;
                // Validate the candidate before committing; invalid input remains editable.
                f.Transaction=new Dictionary<string,object>(f.Variables,StringComparer.OrdinalIgnoreCase);
                StageWrite(f,block.InputVariable,value,f.InputIndex.HasValue);CheckMemory();CommitTransaction(f);
                Steps++;MarkLast(f);
                ConsoleLog.Add(FlowConsoleLog.EntryKind.Input,ValueFormatter.Format(entered),InputPrompt,InputVariable,f.NodeId,CurrentFunctionId,CurrentFunctionName,Steps,true);
                LastMessage=InputVariable+" = "+ValueFormatter.Format(entered);InputPrompt=null;InputVariable=null;State=RuntimeState.Running;
                Advance(f,"next");return true;
            }
            catch(FlowException ex){f.Transaction=null;f.ArrayOverlays.Clear();f.NewArrays.Clear();f.WholeWrites.Clear();LastMessage=ex.Message;return false;}
        }

        private static VectorValue ReadVector(IDictionary<string,object> variables,string name)
        {
            object value;if(!variables.TryGetValue(name,out value))throw new FlowException("La variabile «"+name+"» non ha ancora un valore.");return VectorValue.Require(value);
        }
        private static void CheckParameter(Frame f,string name,object value)
        {
            if(f.Graph.Function==null)return;
            var p=f.Graph.Function.Parameters.FirstOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
            if(p!=null)FunctionModel.CheckType(value,p.Type,p.IsArray,"Il parametro «"+p.Name+"»");
        }
        private static object ConvertWrite(Frame f,string name,object value)
        {
            FlowDeclaration declaration;if(f.Declarations.TryGetValue(name,out declaration))value=declaration.ConvertValue(value,declaration.IsArray?ReadVector(f.Variables,name).Count:0);
            CheckParameter(f,name,value);return value;
        }
        private static bool TextInput(Frame f,string name,bool element)
        {
            FlowDeclaration declaration;if(f.Declarations.TryGetValue(name,out declaration))return declaration.Type==FlowDataType.Text&&(element||!declaration.IsArray);
            var p=f.Graph.Function==null?null:f.Graph.Function.Parameters.FirstOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));return p!=null&&p.Type==FlowDataType.Text&&(element||!p.IsArray);
        }
        private static IDictionary<string,string> DescribeTypes(Frame f)
        {
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if(f!=null)foreach(var pair in f.Variables){FlowDeclaration declaration;FlowDataType type=FlowDataType.Auto;if(f.Declarations.TryGetValue(pair.Key,out declaration))type=declaration.Type;else if(f.Graph.Function!=null){var p=f.Graph.Function.Parameters.FirstOrDefault(x=>x.Name.Equals(pair.Key,StringComparison.OrdinalIgnoreCase));if(p!=null)type=p.Type;}result.Add(pair.Key,FlowDeclaration.Describe(pair.Value,type));}
            return new ReadOnlyDictionary<string,string>(result);
        }
        private void MarkLast(Frame f){LastNodeId=f.NodeId;LastFunctionId=f.Graph.Function==null?null:f.Graph.Function.Id;}
        private void Advance(Frame f,string branch)
        {
            var edge=f.Graph.Outgoing[f.NodeId].FirstOrDefault(e=>e.Branch==branch);
            if(edge==null)throw new FlowException("Manca il collegamento successivo.");
            MarkLast(f);LastEdgeId=edge.Id;f.NodeId=edge.To;f.Visit++;f.ClearPending();
        }
        private void Fail(string message){Error=message;LastMessage=message;State=RuntimeState.Error;}

        private void CheckMemory()
        {
            // Bound retained values across suspended calls, not just each local scope.
            long size=outputCharacters;int slots=0;
            foreach(var frame in frames)
            {
                slots+=frame.Variables.Count+(frame.Transaction==null?0:frame.Transaction.Count);
                foreach(var value in frame.Variables.Values)size+=ValueSize(value);
                if(frame.Transaction!=null)foreach(var value in frame.Transaction.Values)size+=ValueSize(value);
                if(frame.Evaluation!=null)foreach(var value in frame.Evaluation.RetainedValues())size+=ValueSize(value);
                if(frame.HasAssignmentValue)size+=ValueSize(frame.AssignmentValue);
                if(size>2097152 || slots>16384)throw new FlowException("La memoria dei valori supera il limite di esecuzione. Riduci i dati conservati o le chiamate ricorsive.");
            }
        }
        private static long ValueSize(object value)
        {
            if(value is string)return ((string)value).Length;
            var vector=value as VectorValue;
            if(vector!=null){long size=vector.Count*8L;for(int i=0;i<vector.Count;i++)if(vector.At(i) is string)size+=((string)vector.At(i)).Length;return size;}
            return 8;
        }
    }
}
