package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Target-independent immutable compilation for source backends. No expression evaluation or I/O. */
final class FlowSource {
    private FlowSource() {}
    enum ExpressionKind { Number, Text, Boolean, Variable, Vector, Index, Unary, Binary, Call }

    static final class Expression {
        final ExpressionKind kind;
        final String text,functionId;
        final double number;
        final boolean bool;
        final List<Expression> children;
        Expression(ExpressionKind kind,String text,double number,boolean bool,String functionId,List<Expression> children) {
            this.kind=kind;this.text=text;this.number=number;this.bool=bool;this.functionId=functionId;this.children=freeze(children);
        }
    }
    static final class Target {
        final String name;final Expression index;
        Target(String name,Expression index){this.name=name;this.index=index;}
    }
    static final class Write {
        final Target target;final Expression value;
        Write(Target target,Expression value){this.target=target;this.value=value;}
    }
    static final class Declaration {
        final List<String> names;final int type;final Expression size;
        Declaration(String[] names,int type,Expression size){this.names=freeze(Arrays.asList(names));this.type=type;this.size=size;}
        boolean isArray(){return size!=null;}
    }
    static final class Operation {
        final Expression value;final Target input;final List<Write> writes;final Declaration declaration;
        final boolean fileReading,fileNewline,turtleDraw,turtleLeft,outputNewline;
        // Process writes form one transaction: RHS before target index, staged writes visible to later
        // assignments, commit only after the entire block. Backends must preserve this contract.
        Operation(Expression value,Target input,List<Write> writes,Declaration declaration,
                  boolean fileReading,boolean fileNewline,boolean turtleDraw,boolean turtleLeft,boolean outputNewline){
            this.value=value;this.input=input;this.writes=freeze(writes);this.declaration=declaration;
            this.fileReading=fileReading;this.fileNewline=fileNewline;this.turtleDraw=turtleDraw;this.turtleLeft=turtleLeft;this.outputNewline=outputNewline;
        }
    }
    static final class Link {
        final String id,to,branch;
        Link(Edge edge){id=edge.id;to=edge.to;branch=edge.branch;}
    }
    static final class Block {
        final String id,text,prompt,comment;final int kind;final Operation operation;
        final List<Link> links;final boolean reachable;
        Block(Node node,Operation operation,List<Link> links,boolean reachable){
            id=node.id;text=node.text;prompt=node.prompt;comment=node.comment;kind=node.kind;this.operation=operation;
            this.links=freeze(links);this.reachable=reachable;
        }
        String next(String branch){for(Link edge:links)if(edge.branch.equals(branch))return edge.to;return null;}
    }
    static final class Parameter {
        final String name;final int type;final boolean isArray,byReference;
        Parameter(FlowModel.Parameter parameter){name=parameter.name;type=parameter.type;isArray=parameter.isArray;byReference=parameter.byReference;}
    }
    static final class Scope {
        // null is main; other scopes use their persistent function ID.
        final String id,name,startId,returnVariable;final int returnType;final boolean returnIsArray;
        final List<Parameter> parameters;final List<Block> blocks,notes;
        private final Map<String,Block> byId=new HashMap<>();
        Scope(Document graph,Map<String,Function> functions,Function owner){
            id=owner==null?null:owner.id;name=owner==null?"principale":owner.name;
            returnVariable=owner==null?null:owner.returnVariable;returnType=owner==null?VOID:owner.returnType;
            returnIsArray=owner!=null&&owner.returnIsArray;
            List<Parameter> args=new ArrayList<>();if(owner!=null)for(FlowModel.Parameter p:owner.parameters)args.add(new Parameter(p));parameters=freeze(args);
            String start=null;Map<String,List<Link>> outgoing=new HashMap<>();
            for(Node n:graph.nodes){if(n.kind==START)start=n.id;outgoing.put(n.id,new ArrayList<Link>());}startId=start;
            for(Edge e:graph.edges)outgoing.get(e.from).add(new Link(e));
            Set<String> reached=new HashSet<>();ArrayDeque<String> pending=new ArrayDeque<>();pending.push(startId);
            while(!pending.isEmpty()){String next=pending.pop();if(!reached.add(next))continue;for(Link e:outgoing.get(next))pending.push(e.to);}
            List<Block> result=new ArrayList<>();
            List<Block> annotations=new ArrayList<>();for(Node n:graph.nodes){Block block=new Block(n,compileOperation(n,functions,owner),outgoing.get(n.id),reached.contains(n.id));if(n.kind==NOTE)annotations.add(block);else{result.add(block);byId.put(block.id,block);}}
            blocks=freeze(result);notes=freeze(annotations);
        }
        Block find(String id){return byId.get(id);}
    }
    static final class Diagnostic {
        final boolean error;final String functionId,nodeId,code,message,suggestion;
        Diagnostic(FlowEngine.Issue issue){error=issue.error;functionId=issue.functionId;nodeId=issue.nodeId;code=issue.code;message=issue.message;suggestion=issue.suggestion;}
    }
    static final class Program {
        final String title;final List<Scope> scopes;final List<Diagnostic> diagnostics;
        Program(String title,List<Scope> scopes,List<Diagnostic> diagnostics){this.title=title;this.scopes=freeze(scopes);this.diagnostics=freeze(diagnostics);}
        boolean canGenerate(){if(scopes.isEmpty())return false;for(Diagnostic d:diagnostics)if(d.error)return false;return true;}
    }
    static Program compile(Document document){
        List<Diagnostic> issues=new ArrayList<>();boolean error=false;
        for(FlowEngine.Issue issue:FlowEngine.validate(document)){issues.add(new Diagnostic(issue));error|=issue.error;}
        List<Scope> scopes=new ArrayList<>();
        if(!error){Map<String,Function> functions=FlowFunctions.catalog(document);scopes.add(new Scope(document,functions,null));
            if(document.functions!=null)for(Function f:document.functions)scopes.add(new Scope(f.body,functions,f));}
        return new Program(document==null?null:document.title,scopes,issues);
    }
    private static <T> List<T> freeze(Collection<T> values){return Collections.unmodifiableList(new ArrayList<T>(values));}

    static Operation compileOperation(Node node,Map<String,Function> functions,Function owner){
        FlowEngine.Block block=FlowEngine.compile(node,functions,owner);
        Target input=block.inputVariable==null?null:new Target(block.inputVariable,convertExpression(block.inputIndex));
        Declaration declaration=block.declaration==null?null:new Declaration(block.declaration.names,block.declaration.type,convertExpression(block.declarationSize));
        List<Write> writes=new ArrayList<>();for(FlowEngine.Assignment a:block.assignments)writes.add(new Write(new Target(a.name,convertExpression(a.index)),convertExpression(a.expression)));
        return new Operation(convertExpression(block.expression),input,writes,declaration,block.fileReading,block.fileNewline,
            block.turtle==null||block.turtle.draw,block.turtle!=null&&block.turtle.left,block.outputNewline);
    }
    private static List<FlowEngine.Expr> childrenOf(FlowEngine.Expr e){
        if(e instanceof FlowEngine.Unary)return Arrays.asList(((FlowEngine.Unary)e).operand);
        if(e instanceof FlowEngine.Binary)return Arrays.asList(((FlowEngine.Binary)e).left,((FlowEngine.Binary)e).right);
        if(e instanceof FlowEngine.IndexExpr)return Arrays.asList(((FlowEngine.IndexExpr)e).target,((FlowEngine.IndexExpr)e).index);
        if(e instanceof FlowEngine.VectorExpr)return ((FlowEngine.VectorExpr)e).items;
        if(e instanceof FlowEngine.Call)return ((FlowEngine.Call)e).args;
        return Collections.emptyList();
    }
    private static Expression convertExpression(FlowEngine.Expr root){
        if(root==null)return null;
        ArrayDeque<FlowEngine.Expr> stack=new ArrayDeque<>();Map<FlowEngine.Expr,Expression> converted=new IdentityHashMap<>();stack.push(root);
        while(!stack.isEmpty()){
            FlowEngine.Expr e=stack.peek();List<FlowEngine.Expr> children=childrenOf(e);FlowEngine.Expr missing=null;
            for(FlowEngine.Expr child:children)if(!converted.containsKey(child)){missing=child;break;}
            if(missing!=null){stack.push(missing);continue;}
            ExpressionKind kind;String text=null,functionId=null;double number=0;boolean bool=false;
            if(e instanceof FlowEngine.Literal){FlowEngine.Value value=((FlowEngine.Literal)e).value;
                if(value.isText()){kind=ExpressionKind.Text;text=value.format();}
                else if(value.isBoolean()){kind=ExpressionKind.Boolean;bool=value.asBoolean();}
                else {kind=ExpressionKind.Number;number=value.asNumber();}}
            else if(e instanceof FlowEngine.Variable){kind=ExpressionKind.Variable;text=((FlowEngine.Variable)e).name;}
            else if(e instanceof FlowEngine.Unary){kind=ExpressionKind.Unary;text=((FlowEngine.Unary)e).op;}
            else if(e instanceof FlowEngine.Binary){kind=ExpressionKind.Binary;text=((FlowEngine.Binary)e).op;}
            else if(e instanceof FlowEngine.Call){kind=ExpressionKind.Call;FlowEngine.Call call=(FlowEngine.Call)e;text=call.name;functionId=call.function==null?null:call.function.id;}
            else if(e instanceof FlowEngine.VectorExpr)kind=ExpressionKind.Vector;
            else if(e instanceof FlowEngine.IndexExpr)kind=ExpressionKind.Index;
            else throw new IllegalStateException("Espressione non supportata dal generatore di codice.");
            List<Expression> args=new ArrayList<>();for(FlowEngine.Expr child:children)args.add(converted.get(child));
            converted.put(e,new Expression(kind,text,number,bool,functionId,args));stack.pop();
        }
        return converted.get(root);
    }
}
