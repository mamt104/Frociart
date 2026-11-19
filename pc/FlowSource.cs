using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Frociart
{
    // Target-independent, immutable compilation for source backends. Never evaluates expressions,
    // opens files, advances random state, or modifies the editor's document.
    internal static class FlowSource
    {
        internal enum ExpressionKind { Number, Text, Boolean, Variable, Vector, Index, Unary, Binary, Call }

        internal sealed class Expression
        {
            internal readonly ExpressionKind Kind;
            internal readonly string Text, FunctionId;
            internal readonly double Number;
            internal readonly bool Boolean;
            internal readonly ReadOnlyCollection<Expression> Children;
            internal Expression(ExpressionKind kind, string text, double number, bool boolean,
                string functionId, IEnumerable<Expression> children)
            { Kind=kind; Text=text; Number=number; Boolean=boolean; FunctionId=functionId; Children=Freeze(children); }
        }

        internal sealed class Target
        {
            internal readonly string Name;
            internal readonly Expression Index;
            internal Target(string name, Expression index) { Name=name; Index=index; }
        }

        internal sealed class Write
        {
            internal readonly Target Target;
            internal readonly Expression Value;
            internal Write(Target target, Expression value) { Target=target; Value=value; }
        }

        internal sealed class Declaration
        {
            internal readonly ReadOnlyCollection<string> Names;
            internal readonly FlowDataType Type;
            internal readonly Expression Size;
            internal bool IsArray { get { return Size!=null; } }
            internal Declaration(IEnumerable<string> names, FlowDataType type, Expression size)
            { Names=Freeze(names); Type=type; Size=size; }
        }

        internal sealed class Operation
        {
            internal readonly Expression Value;
            internal readonly Target Input;
            // One process block is one transaction. Evaluate each RHS before its target index;
            // later assignments read staged earlier writes. Commit only after the entire block.
            internal readonly ReadOnlyCollection<Write> Writes;
            internal readonly Declaration Declaration;
            internal readonly bool FileReading, FileNewline, TurtleDraw, TurtleLeft, OutputNewline;
            internal Operation(Expression value, Target input, IEnumerable<Write> writes, Declaration declaration,
                bool fileReading, bool fileNewline, bool turtleDraw, bool turtleLeft, bool outputNewline)
            { Value=value; Input=input; Writes=Freeze(writes); Declaration=declaration;
              FileReading=fileReading; FileNewline=fileNewline; TurtleDraw=turtleDraw; TurtleLeft=turtleLeft; OutputNewline=outputNewline; }
        }

        internal sealed class Link
        {
            internal readonly string Id, To, Branch;
            internal Link(FlowEdge edge) { Id=edge.Id; To=edge.To; Branch=edge.Branch; }
        }

        internal sealed class Block
        {
            internal readonly string Id, Text, Prompt, Comment;
            internal readonly NodeKind Kind;
            internal readonly Operation Operation;
            internal readonly ReadOnlyCollection<Link> Links;
            internal readonly bool Reachable;
            internal Block(FlowNode node, Operation operation, IEnumerable<Link> links, bool reachable)
            { Id=node.Id; Text=node.Text; Prompt=node.Prompt; Comment=node.Comment; Kind=node.Kind; Operation=operation;
              Links=Freeze(links); Reachable=reachable; }
            internal string Next(string branch)
            { var link=Links.FirstOrDefault(e=>e.Branch==branch); return link==null?null:link.To; }
        }

        internal sealed class Parameter
        {
            internal readonly string Name;
            internal readonly FlowDataType Type;
            internal readonly bool IsArray,ByReference;
            internal Parameter(FlowParameter parameter) { Name=parameter.Name; Type=parameter.Type; IsArray=parameter.IsArray;ByReference=parameter.ByReference; }
        }

        internal sealed class Scope
        {
            // null identifies the main program; a function's persistent ID identifies every other scope.
            internal readonly string Id, Name, StartId, ReturnVariable;
            internal readonly FlowDataType ReturnType;
            internal readonly bool ReturnIsArray;
            internal readonly ReadOnlyCollection<Parameter> Parameters;
            internal readonly ReadOnlyCollection<Block> Blocks,Notes;
            private readonly Dictionary<string,Block> byId;
            internal Scope(Diagram graph, IDictionary<string,FlowFunction> functions, FlowFunction owner)
            {
                Id=owner==null?null:owner.Id; Name=owner==null?"principale":owner.Name;
                ReturnVariable=owner==null?null:owner.ReturnVariable;
                ReturnType=owner==null?FlowDataType.Void:owner.ReturnType; ReturnIsArray=owner!=null&&owner.ReturnIsArray;
                Parameters=Freeze(owner==null?Enumerable.Empty<Parameter>():owner.Parameters.Select(p=>new Parameter(p)));
                StartId=graph.Nodes.Single(n=>n.Kind==NodeKind.Start).Id;
                var outgoing=graph.Nodes.ToDictionary(n=>n.Id,n=>new List<Link>(),StringComparer.Ordinal);
                foreach(var edge in graph.Edges)outgoing[edge.From].Add(new Link(edge));
                var reached=new HashSet<string>(StringComparer.Ordinal); var pending=new Stack<string>(); pending.Push(StartId);
                while(pending.Count>0) { string id=pending.Pop(); if(!reached.Add(id))continue; foreach(var edge in outgoing[id])pending.Push(edge.To); }
                Notes=Freeze(graph.Nodes.Where(n=>n.Kind==NodeKind.Note).Select(n=>new Block(n,CompileOperation(n,functions,owner),Enumerable.Empty<Link>(),false)));
                Blocks=Freeze(graph.Nodes.Where(n=>n.Kind!=NodeKind.Note).Select(n=>new Block(n,CompileOperation(n,functions,owner),outgoing[n.Id],reached.Contains(n.Id))));
                byId=Blocks.ToDictionary(n=>n.Id,StringComparer.Ordinal);
            }
            internal Block Find(string id) { Block block; return id!=null&&byId.TryGetValue(id,out block)?block:null; }
        }

        internal sealed class Diagnostic
        {
            internal readonly bool Error;
            internal readonly string FunctionId, NodeId, Code, Message, Suggestion;
            internal Diagnostic(ValidationIssue issue)
            { Error=issue.Severity==Severity.Error; FunctionId=issue.FunctionId; NodeId=issue.NodeId;
              Code=issue.Code; Message=issue.Message; Suggestion=issue.Suggestion; }
        }

        internal sealed class Program
        {
            internal readonly string Title;
            internal readonly ReadOnlyCollection<Scope> Scopes;
            internal readonly ReadOnlyCollection<Diagnostic> Diagnostics;
            internal bool CanGenerate { get { return Scopes.Count>0&&!Diagnostics.Any(d=>d.Error); } }
            internal Program(string title, IEnumerable<Scope> scopes, IEnumerable<Diagnostic> diagnostics)
            { Title=title; Scopes=Freeze(scopes); Diagnostics=Freeze(diagnostics); }
        }

        internal static Program Compile(Diagram document)
        {
            var issues=Validator.Validate(document).Select(i=>new Diagnostic(i)).ToList();
            var scopes=new List<Scope>();
            if(!issues.Any(i=>i.Error))
            {
                var functions=FunctionModel.Catalog(document);
                scopes.Add(new Scope(document,functions,null));
                if(document.Functions!=null)foreach(var function in document.Functions)scopes.Add(new Scope(function.Body,functions,function));
            }
            return new Program(document==null?null:document.Title,scopes,issues);
        }

        private static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> items)
        { return new List<T>(items??Enumerable.Empty<T>()).AsReadOnly(); }

        internal static Operation CompileOperation(FlowNode node, IDictionary<string,FlowFunction> functions, FlowFunction owner)
        {
            var block=Compiler.Block(node,functions,owner);
            var input=block.InputVariable==null?null:new Target(block.InputVariable,ConvertExpression(block.InputIndex));
            var declaration=block.Declaration==null?null:new Declaration(block.Declaration.Names,block.Declaration.Type,ConvertExpression(block.DeclarationSize));
            return new Operation(ConvertExpression(block.Expression),input,
                block.Assignments.Select(a=>new Write(new Target(a.Name,ConvertExpression(a.Index)),ConvertExpression(a.Expression))),
                declaration,block.FileReading,block.FileNewline,block.Turtle==null||block.Turtle.Draw,block.Turtle!=null&&block.Turtle.Left,block.OutputNewline);
        }

        private static Expression ConvertExpression(Expr root)
        {
            if(root==null)return null;
            // Iterative postorder also handles long, left-associated expressions without using the host call stack.
            var stack=new Stack<Expr>(); var converted=new Dictionary<Expr,Expression>(); stack.Push(root);
            while(stack.Count>0)
            {
                var current=stack.Peek(); var children=ExpressionMachine.ChildrenOf(current);
                var missing=children.FirstOrDefault(child=>!converted.ContainsKey(child));
                if(missing!=null) { stack.Push(missing); continue; }
                ExpressionKind kind; string text=null,functionId=null; double number=0; bool boolean=false;
                var literal=current as LiteralExpr; var variable=current as VariableExpr;
                var unary=current as UnaryExpr; var binary=current as BinaryExpr; var call=current as CallExpr;
                if(literal!=null)
                {
                    if(literal.Value is string) { kind=ExpressionKind.Text; text=(string)literal.Value; }
                    else if(literal.Value is bool) { kind=ExpressionKind.Boolean; boolean=(bool)literal.Value; }
                    else { kind=ExpressionKind.Number; number=Values.Number(literal.Value); }
                }
                else if(variable!=null) { kind=ExpressionKind.Variable; text=variable.name; }
                else if(unary!=null) { kind=ExpressionKind.Unary; text=unary.op; }
                else if(binary!=null) { kind=ExpressionKind.Binary; text=binary.op; }
                else if(call!=null) { kind=ExpressionKind.Call; text=call.name; functionId=call.Function==null?null:call.Function.Id; }
                else if(current is VectorExpr)kind=ExpressionKind.Vector;
                else if(current is IndexExpr)kind=ExpressionKind.Index;
                else throw new InvalidOperationException("Espressione non supportata dal generatore di codice.");
                converted.Add(current,new Expression(kind,text,number,boolean,functionId,children.Select(c=>converted[c])));
                stack.Pop();
            }
            return converted[root];
        }
    }
}
