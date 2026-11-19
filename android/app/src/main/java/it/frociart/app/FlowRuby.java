package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Emits standalone Ruby functions and control flow, with bounded semantic support. */
final class FlowRuby {
    static String templateNumber(double value){return number(value);}
    private FlowRuby() {}
    static FlowSourceText generate(FlowSource.Program program,String runtime) {
        if(program==null||!program.canGenerate())throw new IllegalArgumentException("Correggi il diagramma prima di generare il codice.");
        if(runtime==null||runtime.trim().isEmpty())throw new IllegalArgumentException("Il supporto Ruby è vuoto.");
        return new Writer(program).generate(runtime);
    }
    static String literal(String text){
        if(text==null)return "nil";
        for(int i=0;i<text.length();i++)if(Character.isSurrogate(text.charAt(i))){if(Character.isHighSurrogate(text.charAt(i))&&i+1<text.length()&&Character.isLowSurrogate(text.charAt(i+1))){i++;continue;}List<String> units=new ArrayList<>();for(char c:text.toCharArray())units.add(Integer.toString(c));return "FcRuntime.utf16_units(["+String.join(", ",units)+"])";}
        StringBuilder b=new StringBuilder("\"");for(int i=0;i<text.length();i++){char c=text.charAt(i);if(c=='"'||c=='\\'||c=='#')b.append('\\').append(c);else if(c>=32&&c<=126)b.append(c);else if(Character.isHighSurrogate(c))b.append("\\u{").append(Integer.toHexString(Character.toCodePoint(c,text.charAt(++i)))).append('}');else b.append("\\u{").append(Integer.toHexString(c)).append('}');}return b.append('"').toString();
    }
    private static String hex(long number,int width){String s=Long.toHexString(number);StringBuilder b=new StringBuilder();for(int i=s.length();i<width;i++)b.append('0');return b.append(s).toString();}
    private static String number(double value){if(value==0)return "0.0";String simple=FlowPython.templateNumber(value);if(simple.startsWith("_fc.real("))return "@fc.real("+simple.substring(9);return !simple.contains(".")&&!simple.contains("e")&&!simple.contains("E")?simple+".0":simple;}
    private static String bool(boolean value){return value?"true":"false";}
    private static final Set<String> KEYWORDS=new HashSet<>(Arrays.asList(("BEGIN END __ENCODING__ __LINE__ __FILE__ alias and begin break case class def defined do else elsif end ensure false for if in module next nil not or redo rescue retry return self super then true undef unless until when while yield main initialize runtime method").split(" ")));
    private static String identifier(String name){StringBuilder b=new StringBuilder();for(char c:name.toCharArray()){
        if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.append(c);else b.append("_u").append(hex(c,4));}
        String result=b.toString();if(result.isEmpty()||result.charAt(0)>='0'&&result.charAt(0)<='9'||result.charAt(0)>='A'&&result.charAt(0)<='Z')result="value_"+result;
        if(result.startsWith("_fc")||KEYWORDS.contains(result))result="value_"+result;return result;
    }
    private static String unique(String seed,Set<String> used){String name=seed;int suffix=2;while(!used.add(name))name=seed+"_"+(suffix++);return name;}
    private static final class Symbol {final String name,original;Symbol(String name,String original){this.name=name;this.original=original;}}
    private static final class Writer {
        final FlowSource.Program program;final FlowSourceText.Builder output;
        final Map<String,String> functions=new HashMap<>();final Set<String> globals=new HashSet<>(Arrays.asList("main","runtime","initialize","method"));
        Map<String,Symbol> names;List<Symbol> symbols;FlowSource.Scope scope;int temporary;final boolean references;
        Writer(FlowSource.Program program){this.program=program;boolean refs=false;for(FlowSource.Scope s:program.scopes)for(FlowSource.Parameter p:s.parameters)refs|=p.byReference;references=refs;output=new FlowSourceText.Builder(program,"ruby");
            for(FlowSource.Scope f:program.scopes)if(f.id!=null)functions.put(f.id,unique("flow_"+identifier(f.name),globals));}
        void line(String code){line(code,0,null);}void line(String code,int indent){line(code,indent,null);}
        void line(String code,int indent,FlowSource.Block block){output.addLine(code,indent,block==null?new FlowSourceText.Origin[0]:new FlowSourceText.Origin[]{new FlowSourceText.Origin(scope.id,block.id)});}
        FlowSourceText generate(String runtime){
            line("# encoding: UTF-8");line("# Generato con Frociart — autore del software: mamt104");line("# Progetto: "+literal(program.title));line("# Ruby 3.3+. Il programma non richiede Frociart né gem esterne.");line("");
            line("class Programma");line("def initialize(runtime)",1);line("@fc = runtime",2);line("end",1);line("");for(FlowSource.Scope s:program.scopes)writeScope(s);line("end");line("");
            line("# --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---");for(String value:runtime.replace("\r\n","\n").replace('\r','\n').split("\n",-1))line(value);
            line("");line("if $PROGRAM_NAME == __FILE__");line("runtime = FcRuntime.new",1);line("program = Programma.new(runtime)",1);line("exit(runtime.run(program.method(:main)) ? 0 : 1)",1);line("end");return output.build();
        }
        void add(String name,Set<String> used){if(name!=null&&!names.containsKey(name)){Symbol symbol=new Symbol(unique(identifier(name),used),name);names.put(name,symbol);symbols.add(symbol);}}
        void collect(FlowSource.Expression e,Set<String> used){if(e==null)return;if(e.kind==FlowSource.ExpressionKind.Variable)add(e.text,used);for(FlowSource.Expression c:e.children)collect(c,used);}
        void writeScope(FlowSource.Scope s){
            scope=s;for(FlowSource.Block n:s.notes)line("# Annotazione: "+literal(n.text),1,n);for(FlowSource.Block n:s.blocks)if(n.comment!=null&&!n.comment.isEmpty())line("# Nota ("+(s.blocks.indexOf(n)+1)+"): "+literal(n.comment),1,n);temporary=0;names=caseMap();symbols=new ArrayList<>();Set<String> used=new HashSet<>(globals);
            for(FlowSource.Parameter p:s.parameters)add(p.name,used);
            for(FlowSource.Block n:s.blocks){FlowSource.Operation o=n.operation;if(o.input!=null){add(o.input.name,used);collect(o.input.index,used);}if(o.declaration!=null){for(String name:o.declaration.names)add(name,used);collect(o.declaration.size,used);}for(FlowSource.Write w:o.writes){add(w.target.name,used);collect(w.value,used);collect(w.target.index,used);}collect(o.value,used);}
            List<String> args=new ArrayList<>(),parameters=new ArrayList<>();for(FlowSource.Parameter p:s.parameters){args.add(names.get(p.name).name);parameters.add(literal(names.get(p.name).original)+" => "+names.get(p.name).name);}
            line("def "+(s.id==null?"main":functions.get(s.id))+"("+String.join(", ",args)+")",1);line("@fc.guard_call",2);
            for(FlowSource.Parameter p:s.parameters)line(p.byReference?"@fc.check_reference("+names.get(p.name).name+", "+p.type+")":"@fc.check_type("+names.get(p.name).name+", "+p.type+", "+bool(p.isArray)+")",2);
            line("@fc.enter("+literal(s.id==null?null:s.name)+", {"+String.join(", ",parameters)+"}"+(references?", true":"")+")",2);line("_fc_types = {}",2);
            for(Symbol sym:symbols)if(findParameter(sym.original)==null)line(sym.name+" = @fc.unset",2);
            line("begin",2);line("@fc.check_memory",3);FlowSourcePlan plan=FlowSourcePlan.create(s);if(plan.structured)writeStatements(plan.statements,3);else writeExplicit(3);line("ensure",2);line("@fc.leave",3);line("end",2);line("end",1);
            for(FlowSource.Block n:s.blocks)if(!n.reachable){line("");line("# Blocco non raggiungibile, escluso dall'esecuzione: "+literal(n.text),1,n);}line("");
        }
        static <T> Map<String,T> caseMap(){return new TreeMap<>((a,b)->a.toLowerCase(Locale.ROOT).compareTo(b.toLowerCase(Locale.ROOT)));}
        FlowSource.Parameter findParameter(String name){for(FlowSource.Parameter p:scope.parameters)if(p.name.toLowerCase(Locale.ROOT).equals(name.toLowerCase(Locale.ROOT)))return p;return null;}
        String name(String source,Map<String,String> staged){if(references)return "@fc.lookup("+literal(names.get(source).original)+")";String value=staged!=null&&staged.containsKey(source)?staged.get(source):names.get(source).name;return "@fc.value("+value+", "+literal(names.get(source).original)+")";}
        FlowSource.Scope callee(String id){if(id!=null)for(FlowSource.Scope s:program.scopes)if(id.equals(s.id))return s;return null;}
        boolean callsFunction(FlowSource.Expression e){if(e==null)return false;if(e.functionId!=null)return true;for(FlowSource.Expression c:e.children)if(callsFunction(c))return true;return false;}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block){return expression(e,indent,block,null,0,false);}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block,Map<String,String> staged){return expression(e,indent,block,staged,0,false);}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block,Map<String,String> staged,int depth,boolean track){
            if(e==null)return "nil";if(depth==0)track=track||callsFunction(e);
            if(depth>=24){String helper="_fc_expr_"+(++temporary);line(helper+" = -> do",indent,block);String result=expression(e,indent+1,block,staged,0,track);line(result,indent+1,block);line("end",indent,block);return helper+".call";}
            int key=track?++temporary:0;String result;
            switch(e.kind){
                case Number:return number(e.number);
                case Text:return "@fc.text("+literal(e.text)+")";
                case Boolean:return bool(e.bool);
                case Variable:return name(e.text,staged);
                default:break;
            }
            FlowSource.Scope called=callee(e.functionId);List<String> children=new ArrayList<>();for(int i=0;i<e.children.size();i++){FlowSource.Expression c=e.children.get(i);boolean ref=called!=null&&called.parameters.get(i).byReference;String child=ref?"@fc.hold("+key+", @fc.reference("+literal(names.get(c.text).original)+", "+declaration(c.text)+", "+parameter(c.text)+"))":expression(c,indent,block,staged,depth+1,track);children.add(track&&!ref?"@fc.hold("+key+", "+child+")":child);}
            switch(e.kind){
                case Vector:result="@fc.vector(["+String.join(", ",children)+"])";break;
                case Index:result=references&&e.children.get(0).kind==FlowSource.ExpressionKind.Variable?"@fc.at_name("+literal(names.get(e.children.get(0).text).original)+", -> { "+children.get(1)+" })":"@fc.at("+children.get(0)+", -> { "+children.get(1)+" })";break;
                case Unary:String v=children.get(0);result=e.text.equals("not")?"(!@fc.truth("+v+"))":e.text.equals("-")?"(-@fc.number("+v+"))":"@fc.number("+v+")";break;
                case Call:String args=String.join(", ",children);
                    if(e.functionId!=null)return "@fc.call("+key+", method(:"+functions.get(e.functionId)+")"+(args.isEmpty()?"":", "+args)+")";
                    result="@fc.intrinsic("+literal(e.text)+(args.isEmpty()?"":", "+args)+")";break;
                case Binary:String a=children.get(0),b=children.get(1),op=e.text;
                    if(op.equals("and")||op.equals("or"))result="(@fc.truth("+a+") "+(op.equals("and")?"&&":"||")+" @fc.truth("+b+"))";
                    else if(op.equals("=")||op.equals("=="))result="@fc.equal("+a+", "+b+")";
                    else if(op.equals("!=")||op.equals("<>"))result="(!@fc.equal("+a+", "+b+"))";
                    else if(Arrays.asList("<","<=",">",">=").contains(op))result="@fc.compare("+a+", "+b+", "+literal(op)+")";
                    else {String method=op.equals("&")?"concat":op.equals("+")?"add":op.equals("-")?"sub":op.equals("*")?"mul":op.equals("/")?"div":op.equals("%")?"mod":"power";result="@fc."+method+"("+a+", "+b+")";}break;
                default:throw new IllegalArgumentException("Espressione Ruby non supportata.");
            }
            return track&&!e.children.isEmpty()?"@fc.release("+key+", "+result+")":result;
        }
        String test(FlowSource.Block n,int indent){return "@fc.test(-> { "+expression(n.operation.value,indent,n)+" })";}
        void writeStatements(List<FlowSourcePlan.Statement> statements,int indent){for(FlowSourcePlan.Statement s:statements){
            if(s.kind==FlowSourcePlan.Kind.Instruction){writeInstruction(s.block,indent);continue;}
            if(s.kind==FlowSourcePlan.Kind.Repeat){line("loop do",indent,s.entryBlock());writeBody(s.body,indent+1,s.block);line("break if "+(s.loopOnTrue?"!":"")+test(s.block,indent+1),indent+1,s.block);line("end",indent,s.block);continue;}
            String test=test(s.block,indent);if(s.kind==FlowSourcePlan.Kind.While){line("while "+(s.loopOnTrue?"":"!")+test,indent,s.block);writeBody(s.body,indent+1,s.block);line("end",indent,s.block);}else{line("if "+test,indent,s.block);writeBody(s.body,indent+1,s.block);line("else",indent,s.block);writeBody(s.otherwise,indent+1,s.block);line("end",indent,s.block);}
        }}
        void writeBody(List<FlowSourcePlan.Statement> body,int indent,FlowSource.Block owner){if(body.isEmpty())line("nil # Nessuna operazione",indent,owner);else writeStatements(body,indent);}
        void writeExplicit(int indent){line("# Collegamenti espliciti per conservare questo grafo.",indent);line("_fc_block = "+literal(scope.startId),indent);line("loop do",indent);line("case _fc_block",indent+1);
            for(FlowSource.Block n:scope.blocks){line("when "+literal(n.id),indent+1,n);if(n.kind==DECISION){line("if "+test(n,indent+2),indent+2,n);line("_fc_block = "+literal(n.next("true")),indent+3,n);line("else",indent+2,n);line("_fc_block = "+literal(n.next("false")),indent+3,n);line("end",indent+2,n);}else{writeInstruction(n,indent+2);if(n.kind!=END)line("_fc_block = "+literal(n.next("next")),indent+2,n);}}
            line("else",indent+1);line("raise FcError, 'Collegamento non valido nel sorgente modificato.'",indent+2);line("end",indent+1);line("end",indent);
        }
        String declaration(String name){return "_fc_types["+literal(names.get(name).name)+"]";}
        String parameter(String name){FlowSource.Parameter p=findParameter(name);return p==null?"nil":"["+p.type+", "+bool(p.isArray)+"]";}
        String convert(String value,String name){return "@fc.convert("+value+", "+declaration(name)+", "+parameter(name)+")";}
        void writeInstruction(FlowSource.Block n,int indent){FlowSource.Operation o=n.operation;
            line(n.kind==INPUT?"@fc.guard":"@fc.step",indent,n);
            if(n.kind==START||n.kind==PAUSE)return;
            if(n.kind==END){if(scope.id==null){line("@fc.finish_files",indent,n);line("return",indent,n);}
                else if(scope.returnType==VOID)line("return",indent,n);
                else{String result=expression(o.value,indent,n);line("return @fc.check_type("+result+", "+scope.returnType+", "+bool(scope.returnIsArray)+")",indent,n);}return;}
            if(n.kind==PROCESS){line("@fc.begin_values",indent,n);Map<String,String> staged=caseMap();List<String> order=new ArrayList<>(),temps=new ArrayList<>();
                for(FlowSource.Write write:o.writes){String value=expression(write.value,indent,n,staged);
                    if(write.target.index!=null){String index=expression(write.target.index,indent,n,staged);value=references?"@fc.set_after_name("+value+", "+literal(names.get(write.target.name).original)+", -> { "+index+" })":"@fc.set_after("+value+", "+name(write.target.name,staged)+", -> { "+index+" })";}
                    value="@fc.stage("+literal(names.get(write.target.name).original)+", "+convert(value,write.target.name)+(references&&write.target.index!=null?", true, true":"")+")";
                    if(o.writes.size()==1){line(names.get(write.target.name).name+" = "+value,indent,n);continue;}
                    String temp="_fc_tmp_"+(++temporary);temps.add(temp);line(temp+" = "+value,indent,n);if(!staged.containsKey(write.target.name))order.add(write.target.name);staged.put(write.target.name,temp);
                }
                if(o.writes.size()>1){List<String> left=new ArrayList<>(),right=new ArrayList<>();for(String name:order){left.add(names.get(name).name);right.add(staged.get(name));}
                    line(String.join(", ",left)+" = "+String.join(", ",right),indent,n);for(String temp:temps)line(temp+" = nil",indent,n);}
                line("@fc.commit",indent,n);return;
            }
            if(n.kind==DECLARE){FlowSource.Declaration d=o.declaration;String size=d.isArray()?expression(d.size,indent,n):"0.0",temp="_fc_size_"+(++temporary);
                line(temp+" = @fc.integer("+size+", 0, 1024, 'La dimensione')",indent,n);line("@fc.begin_values",indent,n);List<String> values=new ArrayList<>();
                for(String name:d.names){String value="_fc_tmp_"+(++temporary);values.add(value);line(value+" = @fc.stage("+literal(names.get(name).original)+", @fc.initial("+d.type+", "+bool(d.isArray())+", "+temp+"), false)",indent,n);}
                line("@fc.commit(true)",indent,n);
                for(int i=0;i<d.names.size();i++){String name=d.names.get(i);line(names.get(name).name+" = "+values.get(i),indent,n);line("_fc_types["+literal(names.get(name).name)+"] = ["+d.type+", "+bool(d.isArray())+", "+temp+"]",indent,n);}
                for(String value:values)line(value+" = nil",indent,n);return;
            }
            if(n.kind==INPUT||n.kind==READ_FILE){FlowSource.Target target=o.input;String suffix=declaration(target.name)+", "+parameter(target.name);
                if(target.index!=null)suffix+=", "+name(target.name,null)+", -> { "+expression(target.index,indent,n)+" }";
                String read=n.kind==INPUT?"@fc.read("+literal(names.get(target.name).original)+", "+literal(n.prompt==null?"":n.prompt)+", "+suffix+")":"@fc.read_file("+literal(names.get(target.name).original)+", "+suffix+")";
                line(names.get(target.name).name+" = "+read,indent,n);if(n.kind==INPUT)line("@fc.step",indent,n);return;
            }
            if(n.kind==OPEN_FILE)line("@fc.require_closed("+bool(o.fileReading)+")",indent,n);
            if(n.kind==WRITE_FILE)line("@fc.require_write",indent,n);
            String e=expression(o.value,indent,n),code;
            switch(n.kind){
                case OUTPUT:code="@fc.emit("+e+(o.outputNewline?"":", false")+")";break;
                case CALL:code=e;break;
                case OPEN_FILE:code="@fc.open_file("+bool(o.fileReading)+", "+e+")";break;
                case WRITE_FILE:code="@fc.write_file("+e+", "+bool(o.fileNewline)+")";break;
                case CLOSE_FILE:code="@fc.close_file("+bool(o.fileReading)+")";break;
                case TURTLE_FORWARD:code="@fc.turtle_forward("+e+", "+bool(o.turtleDraw)+")";break;
                case TURTLE_TURN:code="@fc.turtle_turn("+e+", "+bool(o.turtleLeft)+")";break;
                case TURTLE_HOME:code="@fc.turtle_home";break;
                case TURTLE_CLEAR:code="@fc.turtle_clear";break;
                case TURTLE_PEN:code="@fc.turtle_pen("+e+")";break;
                default:throw new IllegalArgumentException("Blocco Ruby non supportato.");
            }
            line(code,indent,n);
        }
    }
}
