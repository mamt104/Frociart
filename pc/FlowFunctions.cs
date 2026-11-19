using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Frociart
{
    public enum FlowDataType { Auto, Integer, Real, Text, Boolean, Void }

    [DataContract]
    public sealed class FlowParameter
    {
        [DataMember(Order=0)] public string Name = "valore";
        [DataMember(Order=1)] public FlowDataType Type = FlowDataType.Auto;
        [DataMember(Order=2)] public bool IsArray;
        [DataMember(Order=3,EmitDefaultValue=false)] public bool ByReference;
        public FlowParameter Copy() { return new FlowParameter { Name=Name, Type=Type, IsArray=IsArray, ByReference=ByReference }; }
    }

    [DataContract]
    public sealed class FlowFunction
    {
        [DataMember(Order=0)] public string Id = Guid.NewGuid().ToString("N");
        [DataMember(Order=1)] public string Name = "miaFunzione";
        [DataMember(Order=2)] public List<FlowParameter> Parameters = new List<FlowParameter>();
        [DataMember(Order=3)] public FlowDataType ReturnType = FlowDataType.Void;
        [DataMember(Order=4)] public string ReturnVariable = "";
        [DataMember(Order=5)] public Diagram Body = DiagramIO.CreateBlank();
        [DataMember(Order=6)] public bool ReturnIsArray;
        public FlowFunction Copy()
        {
            return new FlowFunction { Id=Id, Name=Name, Parameters=Parameters.Select(p=>p.Copy()).ToList(),
                ReturnType=ReturnType, ReturnVariable=ReturnVariable, ReturnIsArray=ReturnIsArray, Body=DiagramIO.Clone(Body) };
        }
    }

    internal static class FunctionModel
    {
        internal const int MaxFunctions=32, MaxParameters=16, MaxCallDepth=128;
        private static readonly HashSet<string> Reserved = new HashSet<string>(
            ("abs sqrt floor round len sum somma min max array vettore sin cos tan arcsin arccos arctan int ln log log10 sgn random size char tochar tocode tointeger toreal tostring tofixed eof pi π main principale inizio fine leggi stampa scrivi se allora altrimenti mentre per funzione restituisci chiama").Split(' '), StringComparer.OrdinalIgnoreCase);

        internal static int ProjectVersion(IEnumerable<FlowFunction> functions,IEnumerable<FlowNode> nodes=null){return FlowAnnotations.Version(functions,nodes);}

        internal static Dictionary<string,FlowFunction> Catalog(Diagram project)
        {
            var result=new Dictionary<string,FlowFunction>(StringComparer.OrdinalIgnoreCase);
            if(project.Functions!=null) foreach(var f in project.Functions) result.Add(f.Name,f);
            return result;
        }

        internal static void CheckProject(Diagram project)
        {
            if(project.Functions==null || project.Functions.Count==0) return;
            if(project.Version!=2&&project.Version!=3&&project.Version!=4&&project.Version!=5) throw new InvalidDataException("Un progetto con funzioni richiede il formato 2.");
            if(project.Version<3&&project.Functions.Any(f=>f!=null&&f.Parameters!=null&&f.Parameters.Any(v=>v!=null&&v.ByReference)))throw new InvalidDataException("I parametri per riferimento richiedono il formato 3. Aggiorna il progetto prima di salvarlo.");
            if(project.Functions.Count>MaxFunctions) throw new InvalidDataException("Massimo 32 funzioni per progetto.");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var nodeIds=new HashSet<string>(project.Nodes.Select(n=>n.Id),StringComparer.Ordinal);
            var edgeIds=new HashSet<string>(project.Edges.Select(e=>e.Id),StringComparer.Ordinal);
            int nodes=project.Nodes.Count, edges=project.Edges.Count;
            foreach(var f in project.Functions)
            {
                if(f==null || String.IsNullOrWhiteSpace(f.Id) || f.Id.Length>128 || !ids.Add(f.Id))
                    throw new InvalidDataException("Identificatore di funzione mancante o duplicato.");
                if(!ExpressionLanguage.Identifier(f.Name) || Reserved.Contains(f.Name) || !names.Add(f.Name))
                    throw new InvalidDataException("Nome di funzione non valido, riservato o duplicato. Usa un nome distinto di massimo 64 caratteri.");
                if(f.Parameters==null || f.Parameters.Count>MaxParameters) throw new InvalidDataException("Una funzione può avere da 0 a 16 parametri.");
                var parameters=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var p in f.Parameters)
                    if(p==null || !ExpressionLanguage.Identifier(p.Name) || !parameters.Add(p.Name) || !Enum.IsDefined(typeof(FlowDataType),p.Type) || p.Type==FlowDataType.Void || p.ByReference&&!p.IsArray)
                        throw new InvalidDataException("Parametro non valido o duplicato in «"+f.Name+"».");
                if(!Enum.IsDefined(typeof(FlowDataType),f.ReturnType) || (f.ReturnType!=FlowDataType.Void && !ExpressionLanguage.Identifier(f.ReturnVariable)) ||
                    (f.ReturnType==FlowDataType.Void && (f.ReturnIsArray || !String.IsNullOrEmpty(f.ReturnVariable))))
                    throw new InvalidDataException("Definisci correttamente il risultato di «"+f.Name+"».");
                if(f.Body==null || f.Body.VariablePolicy!=null || f.Body.Functions!=null || f.Body.Version!=1&&f.Body.Version!=4) throw new InvalidDataException("Il diagramma di una funzione non può contenere altre definizioni di funzione.");
                DiagramIO.CheckGraph(f.Body);if(f.Body.Version<4&&f.Body.Nodes.Any(n=>n.Kind==NodeKind.Note))throw new InvalidDataException("Le note della funzione richiedono il formato 4.");
                nodes+=f.Body.Nodes.Count; edges+=f.Body.Edges.Count;
                if(nodes>DiagramIO.MaxNodes || edges>DiagramIO.MaxEdges) throw new InvalidDataException("Il progetto completo può contenere al massimo 500 blocchi e 1500 collegamenti.");
                foreach(var node in f.Body.Nodes) if(!nodeIds.Add(node.Id)) throw new InvalidDataException("Identificatore di blocco duplicato tra funzioni.");
                foreach(var edge in f.Body.Edges) if(!edgeIds.Add(edge.Id)) throw new InvalidDataException("Identificatore di collegamento duplicato tra funzioni.");
            }
        }

        internal static void CheckType(object value, FlowDataType type, bool array, string label)
        {
            var vector=value as VectorValue;
            if(array)
            {
                if(vector==null) throw new FlowException(label+" richiede un vettore.");
                for(int i=0;i<vector.Count;i++) CheckType(vector.At(i),type,false,label+"["+i+"]");
                return;
            }
            bool valid=vector==null && value!=null;
            if(type==FlowDataType.Void) valid=false;
            else if(type==FlowDataType.Integer) valid=Values.Numeric(value) && Math.Truncate(Values.Number(value))==Values.Number(value);
            else if(type==FlowDataType.Real) valid=Values.Numeric(value);
            else if(type==FlowDataType.Text) valid=value is string;
            else if(type==FlowDataType.Boolean) valid=value is bool;
            if(!valid) throw new FlowException(label+" richiede "+TypeName(type)+".");
        }

        internal static string TypeName(FlowDataType type)
        {
            switch(type) { case FlowDataType.Integer:return "un numero intero"; case FlowDataType.Real:return "un numero";
                case FlowDataType.Text:return "un testo"; case FlowDataType.Boolean:return "un valore logico"; default:return "un valore singolo"; }
        }
    }

    // The AST remains immutable. Each pending expression has its own explicit stack,
    // so suspending for input or recursion never repeats an already evaluated operand.
    internal sealed class ExpressionMachine
    {
        private sealed class Frame
        {
            internal Expr Expr;
            internal List<Expr> Children;
            internal readonly List<object> Values=new List<object>();
            internal Dictionary<int,object> References;
            internal Frame(Expr expr) { Expr=expr; Children=ChildrenOf(expr); }
        }
        private readonly Stack<Frame> stack=new Stack<Frame>();
        private bool awaiting;
        internal bool Done {get;private set;}
        internal object Result {get;private set;}
        internal FlowFunction RequestedFunction {get;private set;}
        internal IList<object> Arguments {get;private set;}
        internal IList<object> ArgumentReferences {get;private set;}
        private readonly Func<string,object> reference;
        private readonly Func<bool> eof;
        internal ExpressionMachine(Expr expr,Func<bool> eof=null,Func<string,object> reference=null) { this.eof=eof;this.reference=reference;stack.Push(new Frame(expr)); }

        internal static List<Expr> ChildrenOf(Expr expr)
        {
            var unary=expr as UnaryExpr; if(unary!=null)return new List<Expr>{unary.operand};
            var binary=expr as BinaryExpr; if(binary!=null)return new List<Expr>{binary.left,binary.right};
            var index=expr as IndexExpr; if(index!=null)return new List<Expr>{index.Target,index.Index};
            var vector=expr as VectorExpr; if(vector!=null)return vector.items;
            var call=expr as CallExpr; if(call!=null)return call.arguments;
            return new List<Expr>();
        }

        internal static void CheckVoid(Expr expr, bool allowRootVoid)
        {
            if(expr==null)return;
            var pending=new Stack<Expr>(); pending.Push(expr);
            while(pending.Count>0)
            {
                var current=pending.Pop(); var call=current as CallExpr;
                if(call!=null && call.Function!=null && call.Function.ReturnType==FlowDataType.Void && !(allowRootVoid && Object.ReferenceEquals(current,expr)))
                    throw new FlowException("«"+call.Function.Name+"» non restituisce un valore. Usa un blocco Chiamata.");
                foreach(var child in ChildrenOf(current))pending.Push(child);
            }
        }

        internal bool Advance(IDictionary<string,object> variables)
        {
            if(awaiting) throw new InvalidOperationException("La chiamata deve terminare prima di riprendere l'espressione.");
            while(!Done)
            {
                var f=stack.Peek(); var binary=f.Expr as BinaryExpr;
                if(binary!=null && f.Values.Count==1 && (binary.op=="and" || binary.op=="or"))
                {
                    bool truth=Values.Truth(f.Values[0]);
                    if(binary.op=="and"&&!truth || binary.op=="or"&&truth){Finish(truth);continue;}
                }
                if(f.Expr is IndexExpr && f.Values.Count==1) VectorValue.Require(f.Values[0]);
                if(f.Values.Count<f.Children.Count) { stack.Push(new Frame(f.Children[f.Values.Count])); continue; }
                var call=f.Expr as CallExpr;
                if(call!=null && call.Function!=null)
                {
                    awaiting=true; RequestedFunction=call.Function; Arguments=f.Values.AsReadOnly(); ArgumentReferences=f.References==null?null:Enumerable.Range(0,f.Values.Count).Select(i=>f.References.ContainsKey(i)?f.References[i]:null).ToList().AsReadOnly(); return false;
                }
                if(call!=null&&call.name=="eof"){if(eof==null)throw new FlowException("EOF() richiede un file aperto nel programma in esecuzione.");Finish(eof());continue;}
                Expr evaluated;
                if(binary!=null) evaluated=new BinaryExpr(binary.op,new LiteralExpr(f.Values[0]),new LiteralExpr(f.Values[1]));
                else if(f.Expr is UnaryExpr) evaluated=new UnaryExpr(((UnaryExpr)f.Expr).op,new LiteralExpr(f.Values[0]));
                else if(f.Expr is IndexExpr){var index=(IndexExpr)f.Expr;object target=f.Values[0];var variable=index.Target as VariableExpr;if(reference!=null&&variable!=null)target=variable.Eval(variables);evaluated=new IndexExpr(new LiteralExpr(target),new LiteralExpr(f.Values[1]));}
                else if(f.Expr is VectorExpr) evaluated=new VectorExpr(f.Values.Select(v=>(Expr)new LiteralExpr(v)).ToList());
                else if(call!=null) evaluated=new CallExpr(call.name,f.Values.Select(v=>(Expr)new LiteralExpr(v)).ToList());
                else evaluated=f.Expr;
                object value=evaluated.Eval(variables);
                if(f.Expr is VariableExpr&&stack.Count>1){var parent=stack.Skip(1).First();var parentCall=parent.Expr as CallExpr;int arg=parent.Values.Count;if(parentCall!=null&&parentCall.Function!=null&&parentCall.Function.Parameters[arg].ByReference){if(reference==null)throw new FlowException("Un riferimento richiede il programma in esecuzione.");if(parent.References==null)parent.References=new Dictionary<int,object>();parent.References[arg]=reference(((VariableExpr)f.Expr).name);}}
                Finish(value);
            }
            return true;
        }

        internal void Receive(object value)
        {
            if(!awaiting)throw new InvalidOperationException("Nessuna chiamata in attesa.");
            awaiting=false;RequestedFunction=null;Arguments=null;ArgumentReferences=null;Finish(value);
        }
        private void Finish(object value)
        {
            stack.Pop();if(stack.Count==0){Done=true;Result=value;}else stack.Peek().Values.Add(value);
        }
        internal IEnumerable<object> RetainedValues()
        {
            if(Done)yield return Result;
            foreach(var f in stack)foreach(var value in f.Values)yield return value;
        }
    }

    public sealed class CallFrameSnapshot
    {
        public long FrameId {get;internal set;}
        public string FunctionId {get;internal set;}
        public string FunctionName {get;internal set;}
        public string NodeId {get;internal set;}
        public IList<string> ParameterNames {get;internal set;}
        public string ResultName {get;internal set;}
        public IDictionary<string,object> Variables {get;internal set;}
        public IDictionary<string,string> VariableTypes {get;internal set;}
    }
    public sealed class CallFrameInfo
    {
        public long FrameId {get;internal set;}
        public string FunctionId {get;internal set;}
        public string FunctionName {get;internal set;}
        public string NodeId {get;internal set;}
        public int VariableCount {get;internal set;}
    }
}
