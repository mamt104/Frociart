package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Emits standalone Python functions and control flow, with bounded semantic support. */
final class FlowPython {
    static String templateNumber(double value){return number(value);}
    private FlowPython() {}
    static FlowSourceText generate(FlowSource.Program program,String runtime) {
        if(runtime==null||runtime.trim().isEmpty())throw new IllegalArgumentException("Il supporto Python è vuoto.");
        return new Writer(program).generate(runtime);
    }
    static String literal(String text) {
        if(text==null)return "None";StringBuilder b=new StringBuilder("'");
        for(int i=0;i<text.length();i++) {
            char c=text.charAt(i);
            if(c=='\'')b.append("\\'");else if(c=='\\')b.append("\\\\");
            else if(c>=32&&c<=126)b.append(c);
            else if(Character.isHighSurrogate(c)&&i+1<text.length()&&Character.isLowSurrogate(text.charAt(i+1)))
                b.append("\\U").append(hex(Character.toCodePoint(c,text.charAt(++i)),8));
            else b.append("\\u").append(hex(c,4));
        }
        return b.append('\'').toString();
    }
    private static String hex(long number,int width){String s=Long.toHexString(number);StringBuilder b=new StringBuilder();for(int i=s.length();i<width;i++)b.append('0');return b.append(s).toString();}
    private static String number(double number) {
        if(number==0)return "0";
        if(Math.abs(number)<=9000000000L){long power=1;
            for(int digits=0;digits<=6;digits++,power*=10){double scaled=number*power;if(scaled!=Math.rint(scaled))continue;
                String s=Long.toString((long)Math.abs(scaled));while(s.length()<digits+1)s="0"+s;
                if(digits>0)s=s.substring(0,s.length()-digits)+"."+s.substring(s.length()-digits);if(number<0)s="-"+s;
                if(Double.parseDouble(s)==number)return s;
            }
        }
        return "_fc.real(0x"+hex(Double.doubleToLongBits(number),16)+")";
    }
    private static String bool(boolean value){return value?"True":"False";}
    private static final Set<String> KEYWORDS=new HashSet<>(Arrays.asList(("False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield match case type").split(" ")));
    private static String identifier(String name){StringBuilder b=new StringBuilder();for(char c:name.toCharArray()){
        if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.append(c);else b.append("_u").append(hex(c,4));}
        String result=b.toString();if(result.isEmpty()||result.charAt(0)>='0'&&result.charAt(0)<='9')result="value_"+result;
        if(result.startsWith("_fc")||KEYWORDS.contains(result))result="value_"+result;return result;
    }
    private static String unique(String seed,Set<String> used){String name=seed;int suffix=2;while(!used.add(name))name=seed+"_"+(suffix++);return name;}
    private static final class Symbol {final String name,original;Symbol(String name,String original){this.name=name;this.original=original;}}
    private static final class Writer {
        final FlowSource.Program program;final FlowSourceText.Builder output;
        final Map<String,String> functions=new HashMap<>();final Set<String> globals=new HashSet<>(Arrays.asList("main","_fc","_fc_builtins"));
        Map<String,Symbol> names;List<Symbol> symbols;FlowSource.Scope scope;int temporary;final boolean references;
        Writer(FlowSource.Program program){this.program=program;boolean refs=false;for(FlowSource.Scope s:program.scopes)for(FlowSource.Parameter p:s.parameters)refs|=p.byReference;references=refs;output=new FlowSourceText.Builder(program,"python");
            for(FlowSource.Scope f:program.scopes)if(f.id!=null)functions.put(f.id,unique("flow_"+identifier(f.name),globals));}
        void line(String code){line(code,0,null);}void line(String code,int indent){line(code,indent,null);}
        void line(String code,int indent,FlowSource.Block block){output.addLine(code,indent,block==null?new FlowSourceText.Origin[0]:new FlowSourceText.Origin[]{new FlowSourceText.Origin(scope.id,block.id)});}
        FlowSourceText generate(String runtime){
            line("# -*- coding: utf-8 -*-");line("# Generato con Frociart — autore del software: mamt104");line("# Progetto: "+literal(program.title));
            line("# Python 3. Il programma non richiede Frociart né pacchetti esterni.");line("");
            for(FlowSource.Scope s:program.scopes)writeScope(s);
            line("# --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---");
            for(String s:runtime.replace("\r\n","\n").replace('\r','\n').split("\n",-1))line(s);
            line("");line("_fc = _fc_Runtime()");line("");line("if __name__ == '__main__':");line("_fc_sys.exit(0 if _fc.run(main) else 1)",1);
            return output.build();
        }
        void add(String name,Set<String> used){if(name!=null&&!names.containsKey(name)){Symbol symbol=new Symbol(unique(identifier(name),used),name);names.put(name,symbol);symbols.add(symbol);}}
        void collect(FlowSource.Expression e,Set<String> used){if(e==null)return;if(e.kind==FlowSource.ExpressionKind.Variable)add(e.text,used);for(FlowSource.Expression c:e.children)collect(c,used);}
        void writeScope(FlowSource.Scope s){
            scope=s;for(FlowSource.Block annotation:s.notes)line("# Annotazione: "+literal(annotation.text),0,annotation);for(FlowSource.Block note:s.blocks)if(note.comment!=null&&!note.comment.isEmpty())line("# Nota ("+(s.blocks.indexOf(note)+1)+"): "+literal(note.comment),0,note);temporary=0;names=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);symbols=new ArrayList<>();Set<String> used=new HashSet<>(globals);
            for(FlowSource.Parameter p:s.parameters)add(p.name,used);
            for(FlowSource.Block n:s.blocks){FlowSource.Operation o=n.operation;
                if(o.input!=null){add(o.input.name,used);collect(o.input.index,used);}if(o.declaration!=null){for(String name:o.declaration.names)add(name,used);collect(o.declaration.size,used);}
                for(FlowSource.Write w:o.writes){add(w.target.name,used);collect(w.value,used);collect(w.target.index,used);}collect(o.value,used);
            }
            List<String> args=new ArrayList<>(),parameters=new ArrayList<>();
            for(FlowSource.Parameter p:s.parameters){args.add(names.get(p.name).name);parameters.add(literal(names.get(p.name).original)+": "+names.get(p.name).name);}
            line("def "+(s.id==null?"main":functions.get(s.id))+"("+String.join(", ",args)+"):");line("_fc.guard_call()",1);
            for(FlowSource.Parameter p:s.parameters)line(p.byReference?"_fc.check_reference("+names.get(p.name).name+", "+p.type+")":"_fc.check_type("+names.get(p.name).name+", "+p.type+", "+bool(p.isArray)+")",1);
            line("_fc.enter("+literal(s.id==null?null:s.name)+", {"+String.join(", ",parameters)+"}"+(references?", True":"")+")",1);line("_fc_types = {}",1);
            for(Symbol sym:symbols)if((sym.original.equalsIgnoreCase("pi")||sym.original.equals("π"))&&findParameter(sym.original)==null)line(sym.name+" = _fc.unset",1);
            line("try:",1);line("_fc.check_memory()",2);
            FlowSourcePlan plan=FlowSourcePlan.create(s);if(plan.structured)writeStatements(plan.statements,2);else writeExplicit(2);
            line("finally:",1);line("_fc.leave()",2);
            for(FlowSource.Block n:s.blocks)if(!n.reachable){line("");line("# Blocco non raggiungibile, escluso dall'esecuzione: "+literal(n.text),0,n);}
            line("");line("");
        }
        FlowSource.Parameter findParameter(String name){for(FlowSource.Parameter p:scope.parameters)if(p.name.equalsIgnoreCase(name))return p;return null;}
        String name(String source,Map<String,String> staged){return references?"_fc.lookup("+literal(names.get(source).original)+")":staged!=null&&staged.containsKey(source)?staged.get(source):names.get(source).name;}
        FlowSource.Scope callee(String id){if(id!=null)for(FlowSource.Scope s:program.scopes)if(id.equals(s.id))return s;return null;}
        boolean callsFunction(FlowSource.Expression e){if(e==null)return false;if(e.functionId!=null)return true;for(FlowSource.Expression c:e.children)if(callsFunction(c))return true;return false;}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block){return expression(e,indent,block,null,0,false);}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block,Map<String,String> staged){return expression(e,indent,block,staged,0,false);}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block,Map<String,String> staged,int depth,boolean track){
            if(e==null)return "None";if(depth==0)track=track||callsFunction(e);
            if(depth>=24){String helper="_fc_expr_"+(++temporary);line("def "+helper+"():",indent,block);String result=expression(e,indent+1,block,staged,0,track);line("return "+result,indent+1,block);return helper+"()";}
            int key=track?++temporary:0;String result;
            switch(e.kind){
                case Number:return number(e.number);
                case Text:return literal(e.text);
                case Boolean:return bool(e.bool);
                case Variable:String n=name(e.text,staged);return e.text.equalsIgnoreCase("pi")||e.text.equals("π")?"_fc.value("+n+", "+literal(e.text)+")":n;
                default:break;
            }
            FlowSource.Scope called=callee(e.functionId);List<String> children=new ArrayList<>();for(int i=0;i<e.children.size();i++){FlowSource.Expression c=e.children.get(i);boolean ref=called!=null&&called.parameters.get(i).byReference;String child=ref?"_fc.hold("+key+", _fc.reference("+literal(names.get(c.text).original)+", "+declaration(c.text)+", "+parameter(c.text)+"))":expression(c,indent,block,staged,depth+1,track);children.add(track&&!ref?"_fc.hold("+key+", "+child+")":child);}
            switch(e.kind){
                case Vector:result="_fc.vector(["+String.join(", ",children)+"])";break;
                case Index:result=references&&e.children.get(0).kind==FlowSource.ExpressionKind.Variable?"_fc.at_name("+literal(names.get(e.children.get(0).text).original)+", lambda: "+children.get(1)+")":"_fc.at("+children.get(0)+", lambda: "+children.get(1)+")";break;
                case Unary:String v=children.get(0);result=e.text.equals("not")?"(not _fc.truth("+v+"))":e.text.equals("-")?"(-_fc.number("+v+"))":"_fc.number("+v+")";break;
                case Call:String args=String.join(", ",children);
                    if(e.functionId!=null)return "_fc.call("+key+", "+functions.get(e.functionId)+(args.isEmpty()?"":", "+args)+")";
                    result="_fc.intrinsic("+literal(e.text)+(args.isEmpty()?"":", "+args)+")";break;
                case Binary:String a=children.get(0),b=children.get(1),op=e.text;
                    if(op.equals("and")||op.equals("or"))result="(_fc.truth("+a+") "+op+" _fc.truth("+b+"))";
                    else if(op.equals("=")||op.equals("=="))result="_fc.equal("+a+", "+b+")";
                    else if(op.equals("!=")||op.equals("<>"))result="(not _fc.equal("+a+", "+b+"))";
                    else if(Arrays.asList("<","<=",">",">=").contains(op))result="_fc.compare("+a+", "+b+", "+literal(op)+")";
                    else {String method=op.equals("&")?"concat":op.equals("+")?"add":op.equals("-")?"sub":op.equals("*")?"mul":op.equals("/")?"div":op.equals("%")?"mod":"power";result="_fc."+method+"("+a+", "+b+")";}break;
                default:throw new IllegalArgumentException("Espressione Python non supportata.");
            }
            return track&&!e.children.isEmpty()?"_fc.release("+key+", "+result+")":result;
        }
        String test(FlowSource.Block n,int indent){String e=expression(n.operation.value,indent,n);return "_fc.test(lambda: "+e+")";}
        void writeStatements(List<FlowSourcePlan.Statement> statements,int indent){for(FlowSourcePlan.Statement s:statements){
            if(s.kind==FlowSourcePlan.Kind.Instruction){writeInstruction(s.block,indent);continue;}if(s.kind==FlowSourcePlan.Kind.Repeat){line("while True:",indent,s.entryBlock());writeBody(s.body,indent+1,s.block);String condition=test(s.block,indent+1);line("if "+(s.loopOnTrue?"not ":"")+condition+":",indent+1,s.block);line("break",indent+2,s.block);continue;}String test=test(s.block,indent);
            if(s.kind==FlowSourcePlan.Kind.While){line("while "+(s.loopOnTrue?"":"not ")+test+":",indent,s.block);writeBody(s.body,indent+1,s.block);}
            else{line("if "+test+":",indent,s.block);writeBody(s.body,indent+1,s.block);line("else:",indent,s.block);writeBody(s.otherwise,indent+1,s.block);}
        }}
        void writeBody(List<FlowSourcePlan.Statement> body,int indent,FlowSource.Block owner){if(body.isEmpty())line("pass",indent,owner);else writeStatements(body,indent);}
        void writeExplicit(int indent){line("# Collegamenti espliciti per conservare questo grafo.",indent);line("_fc_block = "+literal(scope.startId),indent);line("while True:",indent);
            boolean first=true;for(FlowSource.Block n:scope.blocks){line((first?"if ":"elif ")+"_fc_block == "+literal(n.id)+":",indent+1,n);first=false;
                if(n.kind==DECISION){String test=test(n,indent+2);line("if "+test+":",indent+2,n);line("_fc_block = "+literal(n.next("true")),indent+3,n);line("else:",indent+2,n);line("_fc_block = "+literal(n.next("false")),indent+3,n);}
                else {writeInstruction(n,indent+2);if(n.kind!=END)line("_fc_block = "+literal(n.next("next")),indent+2,n);}
            }
            line("else:",indent+1);line("raise _fc_Error('Collegamento non valido nel sorgente modificato.')",indent+2);
        }
        String declaration(String name){return "_fc_types.get("+literal(names.get(name).name)+")";}
        String parameter(String name){FlowSource.Parameter p=findParameter(name);return p==null?"None":"("+p.type+", "+bool(p.isArray)+")";}
        String convert(String value,String name){return "_fc.convert("+value+", "+declaration(name)+", "+parameter(name)+")";}
        void writeInstruction(FlowSource.Block n,int indent){FlowSource.Operation o=n.operation;
            line(n.kind==INPUT?"_fc.guard()":"_fc.step()",indent,n);
            if(n.kind==START||n.kind==PAUSE)return;
            if(n.kind==END){if(scope.id==null){line("_fc.finish_files()",indent,n);line("return",indent,n);}
                else if(scope.returnType==VOID)line("return",indent,n);
                else{String result=expression(o.value,indent,n);line("return _fc.check_type("+result+", "+scope.returnType+", "+bool(scope.returnIsArray)+")",indent,n);}return;}
            if(n.kind==PROCESS){line("_fc.begin()",indent,n);Map<String,String> staged=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);List<String> order=new ArrayList<>(),temps=new ArrayList<>();
                for(FlowSource.Write write:o.writes){String value=expression(write.value,indent,n,staged);
                    if(write.target.index!=null){String index=expression(write.target.index,indent,n,staged);value=references?"_fc.set_after_name("+value+", "+literal(names.get(write.target.name).original)+", lambda: "+index+")":"_fc.set_after("+value+", "+name(write.target.name,staged)+", lambda: "+index+")";}
                    value="_fc.stage("+literal(names.get(write.target.name).original)+", "+convert(value,write.target.name)+(references&&write.target.index!=null?", indexed=True":"")+")";
                    if(o.writes.size()==1){line(names.get(write.target.name).name+" = "+value,indent,n);continue;}
                    String temp="_fc_tmp_"+(++temporary);temps.add(temp);line(temp+" = "+value,indent,n);if(!staged.containsKey(write.target.name))order.add(write.target.name);staged.put(write.target.name,temp);
                }
                if(o.writes.size()>1){List<String> left=new ArrayList<>(),right=new ArrayList<>();for(String name:order){left.add(names.get(name).name);right.add(staged.get(name));}
                    line(String.join(", ",left)+" = "+String.join(", ",right),indent,n);line("del "+String.join(", ",temps),indent,n);}
                line("_fc.commit()",indent,n);return;
            }
            if(n.kind==DECLARE){FlowSource.Declaration d=o.declaration;String size=d.isArray()?expression(d.size,indent,n):"0",temp="_fc_size_"+(++temporary);
                line(temp+" = _fc.integer("+size+", 0, 1024, 'La dimensione')",indent,n);line("_fc.begin()",indent,n);List<String> values=new ArrayList<>();
                for(String name:d.names){String value="_fc_tmp_"+(++temporary);values.add(value);line(value+" = _fc.stage("+literal(names.get(name).original)+", _fc.initial("+d.type+", "+bool(d.isArray())+", "+temp+"), False)",indent,n);}
                line("_fc.commit(True)",indent,n);
                for(int i=0;i<d.names.size();i++){String name=d.names.get(i);line(names.get(name).name+" = "+values.get(i),indent,n);line("_fc_types["+literal(names.get(name).name)+"] = ("+d.type+", "+bool(d.isArray())+", "+temp+")",indent,n);}
                line("del "+String.join(", ",values),indent,n);return;
            }
            if(n.kind==INPUT||n.kind==READ_FILE){FlowSource.Target target=o.input;String suffix=declaration(target.name)+", "+parameter(target.name);
                if(target.index!=null)suffix+=", "+name(target.name,null)+", lambda: "+expression(target.index,indent,n);
                String read=n.kind==INPUT?"_fc.read("+literal(names.get(target.name).original)+", "+literal(n.prompt==null?"":n.prompt)+", "+suffix+")":"_fc.read_file("+literal(names.get(target.name).original)+", "+suffix+")";
                line(names.get(target.name).name+" = "+read,indent,n);if(n.kind==INPUT)line("_fc.step()",indent,n);return;
            }
            if(n.kind==OPEN_FILE)line("_fc.require_closed("+bool(o.fileReading)+")",indent,n);
            if(n.kind==WRITE_FILE)line("_fc.require_write()",indent,n);
            String e=expression(o.value,indent,n),code;
            switch(n.kind){
                case OUTPUT:code="_fc.emit("+e+(o.outputNewline?"":", False")+")";break;
                case CALL:code=e;break;
                case OPEN_FILE:code="_fc.open_file("+bool(o.fileReading)+", "+e+")";break;
                case WRITE_FILE:code="_fc.write_file("+e+", "+bool(o.fileNewline)+")";break;
                case CLOSE_FILE:code="_fc.close_file("+bool(o.fileReading)+")";break;
                case TURTLE_FORWARD:code="_fc.turtle_forward("+e+", "+bool(o.turtleDraw)+")";break;
                case TURTLE_TURN:code="_fc.turtle_turn("+e+", "+bool(o.turtleLeft)+")";break;
                case TURTLE_HOME:code="_fc.turtle_home()";break;
                case TURTLE_CLEAR:code="_fc.turtle_clear()";break;
                case TURTLE_PEN:code="_fc.turtle_pen("+e+")";break;
                default:throw new IllegalArgumentException("Blocco Python non supportato.");
            }
            line(code,indent,n);
        }
    }
}
