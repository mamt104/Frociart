package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Emits C# methods and structured control flow. No diagram interpreter is exported. */
final class FlowCSharp {
    static String templateNumber(double value){return number(value);}
    private FlowCSharp(){}
    static FlowSourceText generate(FlowSource.Program program,String runtime){return generate(program,runtime,"Programma");}
    static FlowSourceText generate(FlowSource.Program program,String runtime,String className){
        if(runtime==null||runtime.trim().isEmpty())throw new IllegalArgumentException("Il supporto C# è vuoto.");
        if(className==null||!className.matches("[A-Za-z_][A-Za-z_0-9]*")||KEYWORDS.contains(className)||className.startsWith("_fc")||RESERVED_CLASSES.contains(className))throw new IllegalArgumentException("Nome della classe C# non valido.");
        return new Writer(program).generate(runtime,className);
    }
    static String literal(String text){if(text==null)return "null";StringBuilder b=new StringBuilder("\"");for(int i=0;i<text.length();i++){char c=text.charAt(i);if(c=='"')b.append("\\\"");else if(c=='\\')b.append("\\\\");else if(c=='\n')b.append("\\n");else if(c=='\r')b.append("\\r");else if(c=='\t')b.append("\\t");else if(c<32||c>126)b.append(String.format(Locale.ROOT,"\\u%04x",(int)c));else b.append(c);}return b.append('"').toString();}
    private static final Set<String> KEYWORDS=new HashSet<>(Arrays.asList(("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while add alias ascending async await by descending dynamic equals file from get global group init into join let managed nameof nint not notnull nuint on or orderby partial record remove required scoped select set unmanaged value var when where with yield").split(" ")));
    private static final Set<String> RESERVED_CLASSES=new HashSet<>(Arrays.asList("Value Values VectorValue ValueFormatter TypeSpec Intrinsics IntrinsicOperations FlowException FlowFileRequestKind FlowFileRequest FlowTextFiles FlowTurtle Variables ArrayCell ArrayArgument System Console BitConverter String Math Object Double Int32 Int64 Boolean Array List Dictionary HashSet Func Action File FileStream FileMode FileAccess FileShare Path Stream Encoding UTF8Encoding UnicodeEncoding StringBuilder CultureInfo NumberStyles BigInteger Regex Guid Exception IOException UnauthorizedAccessException ArgumentException Enumerable MidpointRounding ReadOnlyCollection Random".split(" ")));
    private static String identifier(String name){StringBuilder b=new StringBuilder();for(char c:name.toCharArray())if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.append(c);else b.append(String.format(Locale.ROOT,"_u%04x",(int)c));String value=b.toString();if(value.isEmpty()||Character.isDigit(value.charAt(0)))value="value_"+value;if(value.startsWith("_fc")||KEYWORDS.contains(value))value="value_"+value;return value;}
    private static String unique(String seed,Set<String> names){String value=seed;int n=2;while(!names.add(value))value=seed+"_"+(n++);return value;}
    private static String number(double n){if(n==0)return "Value.number(0)";if(Math.abs(n)<=9000000000d){long power=1;for(int digits=0;digits<=6;digits++,power*=10){double scaled=n*power;if(scaled!=Math.floor(scaled))continue;String text=Long.toString((long)Math.abs(scaled));while(text.length()<digits+1)text="0"+text;if(digits>0)text=text.substring(0,text.length()-digits)+"."+text.substring(text.length()-digits);if(n<0)text="-"+text;if(Double.parseDouble(text)==n)return "Value.number("+text+(digits==0?"d":"")+")";}}return String.format(Locale.ROOT,"Value.number(BitConverter.Int64BitsToDouble(unchecked((long)0x%016xUL)))",Double.doubleToLongBits(n));}
    private static final class Symbol {final String name,original;Symbol(String name,String original){this.name=name;this.original=original;}}
    private static final class Helper {final String name;final FlowSource.Expression expression;final FlowSource.Block block;final Set<String> staged;final boolean track;Helper(String name,FlowSource.Expression e,FlowSource.Block b,Set<String> s,boolean t){this.name=name;expression=e;block=b;staged=new HashSet<>(s);track=t;}}
    private static final class Writer {
        final FlowSource.Program program;final FlowSourceText.Builder output;
        final Map<String,String> functions=new HashMap<>(),classes=new HashMap<>();
        final Set<String> globals=new HashSet<>(Arrays.asList("Value","Values","VectorValue","ValueFormatter","TypeSpec","Intrinsics","IntrinsicOperations","FlowException","Double","System","Console","BitConverter","_fc","Programma","Main","run"));
        Map<String,Symbol> names;List<Symbol> symbols;Map<String,String> blockMethods;List<Helper> helpers;FlowSource.Scope scope;int temporary;boolean inlineScope;final boolean references;
        Writer(FlowSource.Program p){program=p;boolean refs=false;for(FlowSource.Scope s:p.scopes)for(FlowSource.Parameter v:s.parameters)refs|=v.byReference;references=refs;output=new FlowSourceText.Builder(p,"csharp");int i=0;for(FlowSource.Scope s:p.scopes){classes.put(s.id,"_fc_Scope"+(i++));if(s.id!=null)functions.put(s.id,unique("flow_"+identifier(s.name),globals));}}
        void line(String text,int indent,FlowSource.Block block){output.addLine(text,indent,block==null?new FlowSourceText.Origin[0]:new FlowSourceText.Origin[]{new FlowSourceText.Origin(scope.id,block.id)});}
        void line(String text,int indent){line(text,indent,null);}
        FlowSourceText generate(String runtime,String className){
            line("// Generato con Frociart — autore del software: mamt104",0);line("// Progetto: "+literal(program.title),0);line("// C# 5+. Salva come "+className+".cs; compila con csc /r:System.Numerics.dll "+className+".cs.",0);
            for(String imp:new String[]{"System","System.IO","System.Text","System.Linq","System.Collections.Generic","System.Collections.ObjectModel","System.Globalization","System.Numerics","System.Text.RegularExpressions"})line("using "+imp+";",0);line("",0);
            line("public sealed class "+className+" {",0);line("static _fc_Runtime _fc = new _fc_Runtime();",1);line("public static int Main(string[] args) {",1);line("Console.InputEncoding = new UTF8Encoding(false);",2);line("Console.OutputEncoding = new UTF8Encoding(false);",2);line("return _fc.run(() => new _fc_Scope0().run(), true) ? 0 : 1;",2);line("}",1);
            for(FlowSource.Scope s:program.scopes){if(s.id!=null){List<String> ps=new ArrayList<>(),args=new ArrayList<>();Set<String> used=new HashSet<>(globals);for(FlowSource.Parameter p:s.parameters){String name=unique(identifier(p.name),used);ps.add("Value "+name);args.add(name);}line("static Value "+functions.get(s.id)+"("+String.join(", ",ps)+") {",1);line("return new "+classes.get(s.id)+"().run("+String.join(", ",args)+");",2);line("}",1);}writeScope(s);}
            line("",1);line("// --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---",1);
            for(String text:runtime.replace("\r\n","\n").replace('\r','\n').split("\n",-1))line(text,1);
            line("}",0);return output.build();
        }
        void collect(FlowSource.Expression e,java.util.function.Consumer<String> add){if(e==null)return;if(e.kind==FlowSource.ExpressionKind.Variable)add.accept(e.text);for(FlowSource.Expression child:e.children)collect(child,add);}
        void writeScope(FlowSource.Scope s){
            scope=s;inlineScope=canInlineScope(s);for(FlowSource.Block annotation:s.notes)line("// Annotazione: "+literal(annotation.text),1,annotation);for(FlowSource.Block note:s.blocks)if(note.comment!=null&&!note.comment.isEmpty())line("// Nota ("+(s.blocks.indexOf(note)+1)+"): "+literal(note.comment),1,note);temporary=0;names=new LinkedHashMap<>();symbols=new ArrayList<>();helpers=new ArrayList<>();blockMethods=new LinkedHashMap<>();Set<String> used=new HashSet<>(globals);
            java.util.function.Consumer<String> add=name->{if(name!=null&&!names.containsKey(name.toLowerCase(Locale.ROOT))){Symbol sym=new Symbol(unique(identifier(name),used),name);names.put(name.toLowerCase(Locale.ROOT),sym);symbols.add(sym);}};
            for(FlowSource.Parameter p:s.parameters)add.accept(p.name);
            int index=0;for(FlowSource.Block b:s.blocks){blockMethods.put(b.id,"_fc_block"+(index++));FlowSource.Operation o=b.operation;if(o.input!=null){add.accept(o.input.name);collect(o.input.index,add);}if(o.declaration!=null){for(String n:o.declaration.names)add.accept(n);collect(o.declaration.size,add);}for(FlowSource.Write w:o.writes){add.accept(w.target.name);collect(w.value,add);collect(w.target.index,add);}collect(o.value,add);}
            line("",1);line("sealed class "+classes.get(s.id)+" {",1);
            for(Symbol sym:symbols){line("Value "+sym.name+";",2);line("TypeSpec _fc_type_"+sym.name+";",2);}
            List<String> args=new ArrayList<>(),pnames=new ArrayList<>(),pvalues=new ArrayList<>();index=0;for(FlowSource.Parameter p:s.parameters){String a="_fc_arg"+(index++);args.add("Value "+a);pnames.add(literal(symbol(p.name).original));pvalues.add(a);}
            line("internal Value run("+String.join(", ",args)+") {",2);line("_fc.guardCall();",3);index=0;for(FlowSource.Parameter p:s.parameters){line(symbol(p.name).name+" = _fc."+(p.byReference?"checkReference":"checkType")+"(_fc_arg"+(index++)+", "+p.type+(p.byReference?"":", "+p.isArray)+");",3);}
            line("_fc.enter("+literal(s.id==null?null:s.name)+", new string[]{"+String.join(", ",pnames)+"}, new Value[]{"+String.join(", ",pvalues)+"}"+(references?", true":"")+");",3);line("try {",3);line("_fc.checkMemory();",4);
            FlowSourcePlan plan=FlowSourcePlan.create(s);if(plan.structured)statements(plan.statements,4);else explicit(4);
            line("} catch (Exception) {",3);line("_fc.captureError(); throw;",4);line("} finally { _fc.leave(); }",3);line("}",2);
            if(!inlineScope)for(FlowSource.Block b:s.blocks)instruction(b);
            else for(FlowSource.Block b:s.blocks)if(!b.reachable)line("// Blocco non raggiungibile, escluso dall’esecuzione: "+literal(b.text),2,b);
            for(int i=0;i<helpers.size();i++){Helper h=helpers.get(i);line("private Value "+h.name+"() {",2,h.block);String e=expression(h.expression,h.block,h.staged,0,h.track);line("return "+e+";",3,h.block);line("}",2,h.block);}
            line("}",1);
        }
        Symbol symbol(String name){return names.get(name.toLowerCase(Locale.ROOT));}
        // Conservative emitted-method work budget: retain split helpers for large scopes.
        boolean canInlineScope(FlowSource.Scope current){
            int cost=0;Deque<FlowSource.Expression> pending=new ArrayDeque<>();
            java.util.function.Consumer<FlowSource.Expression> add=e->{if(e!=null)pending.push(e);};
            for(FlowSource.Block block:current.blocks){FlowSource.Operation o=block.operation;cost+=64+o.writes.size()*48;
                add.accept(o.value);if(o.input!=null)add.accept(o.input.index);
                if(o.declaration!=null){cost+=o.declaration.names.size()*32;add.accept(o.declaration.size);}
                for(FlowSource.Write w:o.writes){add.accept(w.value);add.accept(w.target.index);}
                while(!pending.isEmpty()){FlowSource.Expression e=pending.pop();cost+=32;if(cost>24000)return false;for(FlowSource.Expression child:e.children)add.accept(child);}
                if(cost>24000)return false;
            }
            return true;
        }
        String condition(FlowSource.Block block){return inlineScope?"_fc.test(() => "+expression(block.operation.value,block)+")":blockMethods.get(block.id)+"().asBoolean()";}
        String name(String name,Set<String> staged){Symbol sym=symbol(name);return references?"_fc.lookup("+literal(sym.original)+")":"_fc.value("+(staged.contains(name.toLowerCase(Locale.ROOT))?"_fc.staged("+literal(sym.original)+")":sym.name)+", "+literal(sym.original)+")";}
        FlowSource.Scope callee(String id){if(id!=null)for(FlowSource.Scope s:program.scopes)if(id.equals(s.id))return s;return null;}
        boolean calls(FlowSource.Expression e){if(e==null)return false;if(e.functionId!=null)return true;for(FlowSource.Expression c:e.children)if(calls(c))return true;return false;}
        String expression(FlowSource.Expression e,FlowSource.Block block,Set<String> staged,int depth,boolean track){
            if(e==null)return "null";if(depth==0)track|=calls(e);if(depth>=24){String helper="_fc_expr"+(++temporary);helpers.add(new Helper(helper,e,block,staged,track));return helper+"()";}
            final boolean tracked=track;final int key=track?++temporary:0;
            java.util.function.Function<FlowSource.Expression,String> child=c->{String value=expression(c,block,staged,depth+1,tracked);return tracked?"_fc.hold("+key+", "+value+")":value;};
            java.util.function.Function<String,String> finish=value->tracked&&!e.children.isEmpty()?"_fc.release("+key+", "+value+")":value;
            switch(e.kind){
                case Number:return number(e.number);case Text:return "Value.text("+literal(e.text)+")";case Boolean:return "Value.logical("+e.bool+")";
                case Variable:return name(e.text,staged);
                case Vector:{List<String> values=new ArrayList<>();for(FlowSource.Expression c:e.children)values.add(child.apply(c));return finish.apply("Value.vector(new Value[]{"+String.join(", ",values)+"})");}
                case Index:{String target=child.apply(e.children.get(0));return finish.apply(references&&e.children.get(0).kind==FlowSource.ExpressionKind.Variable?"_fc.atName("+literal(symbol(e.children.get(0).text).original)+", () => "+child.apply(e.children.get(1))+")":"_fc.at("+target+", () => "+child.apply(e.children.get(1))+")");}
                case Unary:return finish.apply("_fc.unary("+literal(e.text)+", "+child.apply(e.children.get(0))+")");
                case Call:{FlowSource.Scope called=callee(e.functionId);List<String> values=new ArrayList<>();for(int i=0;i<e.children.size();i++){FlowSource.Expression c=e.children.get(i);values.add(called!=null&&called.parameters.get(i).byReference?"_fc.hold("+key+", _fc.reference("+literal(symbol(c.text).original)+", "+declaration(c.text)+", "+parameter(c.text)+"))":child.apply(c));}String args=String.join(", ",values);return e.functionId==null?finish.apply("_fc.intrinsic("+literal(e.text)+(args.isEmpty()?"":", "+args)+")"):"_fc.call("+key+", () => "+functions.get(e.functionId)+"("+args+"))";}
                case Binary:{String a=child.apply(e.children.get(0)),b=child.apply(e.children.get(1));if(e.text.equals("and")||e.text.equals("or"))return finish.apply("Value.logical("+a+".asBoolean() "+(e.text.equals("and")?"&&":"||")+" "+b+".asBoolean())");return finish.apply("_fc.binary("+literal(e.text)+", "+a+", "+b+")");}
                default:throw new IllegalArgumentException("Espressione C# non supportata.");
            }
        }
        String expression(FlowSource.Expression e,FlowSource.Block block){return expression(e,block,Collections.emptySet(),0,false);}
        void statements(List<FlowSourcePlan.Statement> list,int indent){for(FlowSourcePlan.Statement s:list){if(s.kind==FlowSourcePlan.Kind.Instruction){if(inlineScope)instruction(s.block,indent,true);else line((s.block.kind==END?"return ":"")+blockMethods.get(s.block.id)+"();",indent,s.block);}else if(s.kind==FlowSourcePlan.Kind.Repeat){line("do {",indent,s.entryBlock());statements(s.body,indent+1);line("} while ("+(s.loopOnTrue?"":"!")+condition(s.block)+");",indent,s.block);}else if(s.kind==FlowSourcePlan.Kind.While){line("while ("+(s.loopOnTrue?"":"!")+condition(s.block)+") {",indent,s.block);statements(s.body,indent+1);line("}",indent,s.block);}else{line("if ("+condition(s.block)+") {",indent,s.block);statements(s.body,indent+1);line("} else {",indent,s.block);statements(s.otherwise,indent+1);line("}",indent,s.block);}}}
        void explicit(int indent){line("// Collegamenti espliciti per conservare questo grafo.",indent);line("string _fc_next = "+literal(scope.startId)+";",indent);line("while (true) {",indent);line("switch (_fc_next) {",indent+1);for(FlowSource.Block b:scope.blocks){line("case "+literal(b.id)+":",indent+2,b);if(b.kind==DECISION)line("_fc_next = "+condition(b)+" ? "+literal(b.next("true"))+" : "+literal(b.next("false"))+"; break;",indent+3,b);else if(b.kind==END){if(inlineScope)instruction(b,indent+3,true);else line("return "+blockMethods.get(b.id)+"();",indent+3,b);}else{if(inlineScope)instruction(b,indent+3,true);else line(blockMethods.get(b.id)+"();",indent+3,b);line("_fc_next = "+literal(b.next("next"))+"; break;",indent+3,b);}}line("default: throw problem(\"Collegamento non valido nel sorgente modificato.\");",indent+2);line("}",indent+1);line("}",indent);}
        String declaration(String n){return "_fc_type_"+symbol(n).name;}
        String parameter(String n){for(FlowSource.Parameter p:scope.parameters)if(p.name.equalsIgnoreCase(n))return "new TypeSpec("+p.type+", "+p.isArray+", 0)";return "null";}
        String convert(String value,String n){return "_fc.convert("+value+", "+declaration(n)+", "+parameter(n)+")";}
        void instruction(FlowSource.Block b){instruction(b,2,false);}
        void instruction(FlowSource.Block b,int indent,boolean inline){FlowSource.Operation o=b.operation;String method=blockMethods.get(b.id);if(!inline)line("private Value "+method+"() {",indent,b);else if(b.kind==DECLARE)line("{",indent,b);final int bodyIndent=indent+(inline&&b.kind!=DECLARE?0:1);java.util.function.Consumer<String> emit=text->line(text,bodyIndent,b);
            if(b.kind==DECISION){emit.accept("return Value.logical(_fc.test(() => "+expression(o.value,b)+"));");line("}",2,b);return;}
            emit.accept(b.kind==INPUT?"_fc.guard();":"_fc.step();");
            if(b.kind==END){if(scope.id==null)emit.accept("_fc.finishFiles();");emit.accept(scope.id==null||scope.returnType==VOID?"return null;":"return _fc.checkType("+expression(o.value,b)+", "+scope.returnType+", "+scope.returnIsArray+");");if(!inline)line("}",indent,b);return;}
            if(b.kind==PROCESS&&inline){
                emit.accept("_fc.begin();");Set<String> staged=new LinkedHashSet<>();
                for(FlowSource.Write w:o.writes){String value=expression(w.value,b,staged,0,false);
                    if(w.target.index!=null)value=references?"_fc.setAfterName("+value+", "+literal(symbol(w.target.name).original)+", () => "+expression(w.target.index,b,staged,0,false)+")":"_fc.setAfter("+value+", "+name(w.target.name,staged)+", () => "+expression(w.target.index,b,staged,0,false)+")";
                    emit.accept("_fc.stage("+literal(symbol(w.target.name).original)+", "+convert(value,w.target.name)+(references&&w.target.index!=null?", true, true":"")+");");staged.add(w.target.name.toLowerCase(Locale.ROOT));
                }
                emit.accept("_fc.commit();");staged.clear();for(FlowSource.Write w:o.writes)if(staged.add(w.target.name.toLowerCase(Locale.ROOT)))emit.accept(symbol(w.target.name).name+" = _fc.variable("+literal(symbol(w.target.name).original)+");");return;
            }
            if(b.kind==PROCESS){emit.accept("_fc.begin();");Set<String> staged=new LinkedHashSet<>();List<FlowSource.Write> writes=o.writes;int start=temporary;
                List<String> assignmentNames=new ArrayList<>();for(int i=0;i<writes.size();i++){String part="_fc_assign"+(++temporary);assignmentNames.add(part);emit.accept(part+"();");}
                emit.accept("_fc.commit();");for(FlowSource.Write w:writes)if(staged.add(w.target.name.toLowerCase(Locale.ROOT)))emit.accept(symbol(w.target.name).name+" = _fc.variable("+literal(symbol(w.target.name).original)+");");emit.accept("return null;");line("}",2,b);staged.clear();
                for(int i=0;i<writes.size();i++){FlowSource.Write w=writes.get(i);line("private void "+assignmentNames.get(i)+"() {",2,b);String value=expression(w.value,b,staged,0,false);if(w.target.index!=null)value=references?"_fc.setAfterName("+value+", "+literal(symbol(w.target.name).original)+", () => "+expression(w.target.index,b,staged,0,false)+")":"_fc.setAfter("+value+", "+name(w.target.name,staged)+", () => "+expression(w.target.index,b,staged,0,false)+")";line("_fc.stage("+literal(symbol(w.target.name).original)+", "+convert(value,w.target.name)+(references&&w.target.index!=null?", true, true":"")+");",3,b);line("}",2,b);staged.add(w.target.name.toLowerCase(Locale.ROOT));}return;
            }
            if(b.kind==DECLARE){FlowSource.Declaration d=o.declaration;emit.accept("int _fc_size = Intrinsics.integer("+(d.isArray()?expression(d.size,b):"Value.number(0)")+", 0, 1024, \"La dimensione\");");emit.accept("_fc.begin();");for(String n:d.names)emit.accept("_fc.stage("+literal(symbol(n).original)+", _fc.initial("+d.type+", "+d.isArray()+", _fc_size), false);");emit.accept("_fc.commit(true);");for(String n:d.names){emit.accept(symbol(n).name+" = _fc.variable("+literal(symbol(n).original)+");");emit.accept(declaration(n)+" = new TypeSpec("+d.type+", "+d.isArray()+", _fc_size);");}}
            else if(b.kind==INPUT||b.kind==READ_FILE){FlowSource.Target t=o.input;String suffix=declaration(t.name)+", "+parameter(t.name)+", "+(t.index==null?"null, null":name(t.name,Collections.emptySet())+", () => "+expression(t.index,b));emit.accept(symbol(t.name).name+" = _fc."+(b.kind==INPUT?"read(":"readFile(")+literal(symbol(t.name).original)+", "+(b.kind==INPUT?literal(b.prompt==null?"":b.prompt)+", ":"")+suffix+");");if(b.kind==INPUT)emit.accept("_fc.step();");}
            else if(b.kind!=START&&b.kind!=PAUSE){if(b.kind==OPEN_FILE)emit.accept("_fc.requireClosed("+o.fileReading+");");if(b.kind==WRITE_FILE)emit.accept("_fc.requireWrite();");String e=expression(o.value,b);switch(b.kind){case OUTPUT:emit.accept("_fc.emit("+e+(o.outputNewline?"":", false")+");");break;case CALL:emit.accept(e+";");break;case OPEN_FILE:emit.accept("_fc.openFile("+o.fileReading+", "+e+");");break;case WRITE_FILE:emit.accept("_fc.writeFile("+e+", "+o.fileNewline+");");break;case CLOSE_FILE:emit.accept("_fc.closeFile("+o.fileReading+");");break;case TURTLE_FORWARD:case TURTLE_TURN:case TURTLE_HOME:case TURTLE_CLEAR:case TURTLE_PEN:emit.accept("_fc.turtle("+b.kind+", "+e+", "+(b.kind==TURTLE_FORWARD?o.turtleDraw:o.turtleLeft)+");");break;default:throw new IllegalArgumentException("Blocco C# non supportato.");}}
            if(!inline){emit.accept("return null;");line("}",indent,b);}else if(b.kind==DECLARE)line("}",indent,b);
        }
    }
}
