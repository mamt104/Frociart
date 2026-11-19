package it.frociart.app;

import java.util.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import static it.frociart.app.FlowModel.*;

/** Native PowerShell control flow with the shared, bounded .NET value support. */
final class FlowPowerShell {
    private FlowPowerShell() {}
    static String templateNumber(double value,FlowSourceText source){java.util.regex.Matcher m=java.util.regex.Pattern.compile("\\bpublic static class (FrociartPS_[a-f0-9]{24}) \\{").matcher(source==null?"":source.text);if(!m.find())throw new IllegalArgumentException("Manca il supporto PowerShell nel modello.");return "["+m.group(1)+"]::Number('"+hex(Double.doubleToRawLongBits(value),16)+"')";}
    static FlowSourceText generate(FlowSource.Program program,String runtime){
        if(program==null||!program.canGenerate())throw new IllegalArgumentException("Correggi il diagramma prima di generare il codice.");
        if(runtime==null||runtime.trim().isEmpty())throw new IllegalArgumentException("Il supporto PowerShell è vuoto.");
        return new Writer(program).generate(runtime);
    }
    static String literal(String text){
        if(text==null)return "$null";List<String> pieces=new ArrayList<>();StringBuilder b=new StringBuilder("\"");
        for(int i=0;i<text.length();i++){char c=text.charAt(i);boolean pair=Character.isHighSurrogate(c)&&i+1<text.length()&&Character.isLowSurrogate(text.charAt(i+1));
            if(pair){b.append(c).append(text.charAt(++i));continue;}
            if(Character.isSurrogate(c)||c<32&&c!='\n'&&c!='\r'&&c!='\t'||c=='\u2028'||c=='\u2029'){
                pieces.add(b.append('"').toString());pieces.add("[string][char]0x"+hex(c,4));b=new StringBuilder("\"");
            // PowerShell recognizes curly quotes, including U+201E, as delimiters.
            }else if(c=='"'||c=='`'||c=='$'||c>='\u2018'&&c<='\u201e')b.append('`').append(c);else if(c=='\n')b.append("`n");else if(c=='\r')b.append("`r");else if(c=='\t')b.append("`t");else b.append(c);
        }pieces.add(b.append('"').toString());return pieces.size()==1?pieces.get(0):"("+String.join(" + ",pieces)+")";
    }
    static String hex(long n,int width){String s=Long.toHexString(n);StringBuilder b=new StringBuilder();for(int i=s.length();i<width;i++)b.append('0');return b.append(s).toString();}
    static String bool(boolean value){return value?"$true":"$false";}
    static String identifier(String name){StringBuilder b=new StringBuilder();for(char c:(name==null?"":name).toCharArray())if(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_')b.append(c);else b.append("_u").append(hex(c,4));if(b.length()==0||Character.isDigit(b.charAt(0)))b.insert(0,"value_");return b.toString();}
    static String unique(String seed,Set<String> used){String name=seed;for(int i=2;!used.add(name);i++)name=seed+"_"+i;return name;}
    static <T> Map<String,T> caseMap(){return new TreeMap<>((a,b)->a.toLowerCase(Locale.ROOT).compareTo(b.toLowerCase(Locale.ROOT)));}
    static Set<String> caseSet(){return new TreeSet<>((a,b)->a.toLowerCase(Locale.ROOT).compareTo(b.toLowerCase(Locale.ROOT)));}
    static boolean equalName(String a,String b){return a.toLowerCase(Locale.ROOT).equals(b.toLowerCase(Locale.ROOT));}
    static final class Symbol {final String name,original;Symbol(String name,String original){this.name=name;this.original=original;}}
    static final class Helper {String name;FlowSource.Expression expression;FlowSource.Block block;Set<String> staged;boolean track;}
    static final class Writer {
        final FlowSource.Program program;final FlowSourceText.Builder output;final Map<String,String> functions=new HashMap<>();final boolean references;
        FlowSource.Scope scope;Map<String,Symbol> names;List<Symbol> symbols;List<Helper> helpers;int temporary,helperSerial;String support;
        static final String FC="$script:_fc";
        Writer(FlowSource.Program p){program=p;output=new FlowSourceText.Builder(p,"powershell");boolean refs=false;for(FlowSource.Scope s:p.scopes)for(FlowSource.Parameter a:s.parameters)refs|=a.byReference;references=refs;Set<String> used=caseSet();used.add("Invoke-FrociartMain");for(FlowSource.Scope s:p.scopes)if(s.id!=null)functions.put(s.id,unique("Invoke-Flow_"+identifier(s.name),used));}
        void line(String text){line(text,0,null);}void line(String text,int indent){line(text,indent,null);}
        void line(String text,int indent,FlowSource.Block block){output.addLine(text,indent,block==null?new FlowSourceText.Origin[0]:new FlowSourceText.Origin[]{new FlowSourceText.Origin(scope.id,block.id)});}
        String type(String name){return "["+support+"+"+name+"]";}
        String number(double n){return "["+support+"]::Number('"+hex(Double.doubleToRawLongBits(n),16)+"')";}
        String spec(int kind,boolean array){return spec(kind,array,-1);}String spec(int kind,boolean array,int length){return "["+support+"]::Spec("+kind+", "+bool(array)+", "+length+")";}
        FlowSourceText generate(String runtime){
            String body=runtime.replace("\r\n","\n").replace('\r','\n').replaceAll("\\binternal\\b","public").replaceAll("(?m)^(\\s*)((?:(?:sealed|static)\\s+)?class\\s+)","$1public $2").replace("void showDrawing()","public void showDrawing()");
            try{byte[] digest=MessageDigest.getInstance("SHA-256").digest((body+"bridge-v1").getBytes(StandardCharsets.UTF_8));StringBuilder b=new StringBuilder("FrociartPS_");for(int i=0;i<12;i++)b.append(hex(digest[i]&255,2));support=b.toString();}catch(java.security.NoSuchAlgorithmException e){throw new IllegalStateException(e);}
            List<String> imports=new ArrayList<>();for(String n:new String[]{"System","System.IO","System.Text","System.Linq","System.Collections.Generic","System.Collections.ObjectModel","System.Globalization","System.Numerics","System.Text.RegularExpressions"})imports.add("using "+n+";");
            String code=String.join("\n",imports)+"\npublic static class "+support+" {\n"+body+"\npublic static _fc_Runtime CreateRuntime(){return new _fc_Runtime();}\npublic static Value Number(string bits){return Value.number(BitConverter.Int64BitsToDouble(unchecked((long)UInt64.Parse(bits,NumberStyles.HexNumber,CultureInfo.InvariantCulture))));}\npublic static TypeSpec Spec(int kind,bool array,int length){return new TypeSpec(kind,array,length);}\n}\n";
            for(String s:code.replace("\r\n","\n").split("\n",-1))if(s.equals("'@"))throw new IllegalArgumentException("Supporto PowerShell non valido.");
            line("# Generato con Frociart — autore del software: mamt104");line("# Progetto: "+FlowCSharp.literal(program.title));line("# PowerShell 5.1 / 7+. Salva in UTF-8 con BOM ed esegui con powershell -File Programma.ps1 oppure pwsh -File Programma.ps1.");line("# Il flusso è PowerShell; Add-Type prepara localmente il supporto .NET condiviso, senza dipendenze da Frociart.");line("Set-StrictMode -Version Latest");line("$ErrorActionPreference = 'Stop'");line("");
            line("# --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---");line("if (-not ('"+support+"' -as [type])) {");line("$_fc_source = @'");for(String s:code.replace("\r\n","\n").replace('\r','\n').split("\n",-1))line(s);line("'@");line("if ($PSVersionTable.PSVersion.Major -lt 7) { Add-Type -TypeDefinition $_fc_source -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Numerics.dll') }",1);line("else { Add-Type -TypeDefinition $_fc_source }",1);line("}");line("# --- Programma ---");line(FC+" = ["+support+"]::CreateRuntime()");
            for(FlowSource.Scope s:program.scopes)writeScope(s);
            line("$_fc_exit = 0");line("$_fc_inputEncoding = [Console]::InputEncoding");line("$_fc_outputEncoding = [Console]::OutputEncoding");line("try {");line("[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)",1);line("[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)",1);line("$null = Invoke-FrociartMain",1);line(FC+".showDrawing()",1);line("} catch {");line("$_fc_error = $_.Exception.GetBaseException()",1);line("[Console]::Error.WriteLine($_fc_error.Message)",1);line("$_fc_exit = 1",1);line("} finally {");line("try { "+FC+".files.CloseJournal() } catch { [Console]::Error.WriteLine('Non riesco a chiudere la bozza.') }",1);line("if ($null -ne "+FC+".recoveryPath) { [Console]::Error.WriteLine('Bozza recuperabile: ' + "+FC+".recoveryPath) }",1);line("[Console]::InputEncoding = $_fc_inputEncoding",1);line("[Console]::OutputEncoding = $_fc_outputEncoding",1);line("}");line("exit $_fc_exit");return output.build();
        }
        void add(String n,Set<String> used){if(n==null||names.containsKey(n))return;String seed=identifier(n);if(seed.toLowerCase(Locale.ROOT).startsWith("_fc")||used.contains(seed))seed="value_"+seed;Symbol sym=new Symbol("$"+unique(seed,used),n);names.put(n,sym);symbols.add(sym);}
        void collect(FlowSource.Expression e,Set<String> used){if(e==null)return;if(e.kind==FlowSource.ExpressionKind.Variable)add(e.text,used);for(FlowSource.Expression c:e.children)collect(c,used);}
        FlowSource.Parameter findParameter(String n){for(FlowSource.Parameter p:scope.parameters)if(equalName(p.name,n))return p;return null;}
        void writeScope(FlowSource.Scope current){
            scope=current;temporary=0;helpers=new ArrayList<>();names=caseMap();symbols=new ArrayList<>();Set<String> used=caseSet();Collections.addAll(used,("args input error host home null true false ConsoleFileName IsWindows IsLinux IsMacOS IsCoreCLR EnabledExperimentalFeatures PSDefaultParameterValues PSSenderInfo PSSessionApplicationName PSSessionConfigurationName PSSessionOption PSModuleAutoLoadingPreference Matches Event EventArgs EventSubscriber Sender psitem _ this foreach switch matches pwd pid profile shellid executioncontext psboundparameters pscmdlet pscommandpath psscriptroot myinvocation lastexitcode stacktrace nestedpromptlevel psdebugcontext psCulture psUICulture psVersionTable psHome psEdition psStyle psNativeCommandArgumentPassing ErrorActionPreference OutputEncoding MaximumHistoryCount ConfirmPreference WhatIfPreference DebugPreference VerbosePreference WarningPreference InformationPreference ProgressPreference ErrorView FormatEnumerationLimit NativePSVersionTable").split(" "));
            for(FlowSource.Parameter p:scope.parameters)add(p.name,used);for(FlowSource.Block b:scope.blocks){FlowSource.Operation o=b.operation;if(o.input!=null){add(o.input.name,used);collect(o.input.index,used);}if(o.declaration!=null){for(String n:o.declaration.names)add(n,used);collect(o.declaration.size,used);}for(FlowSource.Write w:o.writes){add(w.target.name,used);collect(w.target.index,used);collect(w.value,used);}collect(o.value,used);}
            line("");for(FlowSource.Block note:scope.notes)line("# Annotazione: "+FlowCSharp.literal(note.text),0,note);for(FlowSource.Block b:scope.blocks)if(b.comment!=null&&!b.comment.isEmpty())line("# Nota: "+FlowCSharp.literal(b.comment),0,b);line("function "+(scope.id==null?"Invoke-FrociartMain":functions.get(scope.id))+" {");List<String> parameters=new ArrayList<>();int index=0;for(FlowSource.Parameter p:scope.parameters)parameters.add(type("Value")+" $_fc_arg"+(index++));line("param("+String.join(", ",parameters)+")",1);line(FC+".guardCall()",1);
            index=0;for(FlowSource.Parameter p:scope.parameters)line(names.get(p.name).name+" = "+FC+"."+(p.byReference?"checkReference":"checkType")+"($_fc_arg"+(index++)+", "+p.type+(p.byReference?"":", "+bool(p.isArray))+")",1);
            for(Symbol sym:symbols)if(findParameter(sym.original)==null)line(sym.name+" = $null",1);line("$_fc_types = @{}",1);
            List<String> originals=new ArrayList<>(),args=new ArrayList<>();index=0;for(FlowSource.Parameter p:scope.parameters){originals.add(literal(names.get(p.name).original));args.add("$_fc_arg"+(index++));}
            line(FC+".enter("+(scope.id==null?"[System.Management.Automation.Language.NullString]::Value":literal(scope.name))+", [string[]]@("+String.join(", ",originals)+"), "+type("Value[]")+"@("+String.join(", ",args)+"), "+bool(references)+")",1);
            line("try {",1);line(FC+".checkMemory()",2);FlowSourcePlan plan=FlowSourcePlan.create(scope);if(plan.structured)statements(plan.statements,2);else explicit(2);for(FlowSource.Block b:scope.blocks)if(!b.reachable)line("# Blocco non raggiungibile: "+FlowCSharp.literal(b.text),2,b);line("} catch {",1);line(FC+".captureError()",2);line("throw",2);line("} finally { "+FC+".leave() }",1);line("}");
            for(int i=0;i<helpers.size();i++){Helper h=helpers.get(i);line("function "+h.name+" {",0,h.block);String expression=flat(h.expression,1,h.block,h.staged,h.track);line("return "+expression,1,h.block);line("}",0,h.block);}
        }
        boolean calls(FlowSource.Expression e){if(e==null)return false;if(e.functionId!=null)return true;for(FlowSource.Expression c:e.children)if(calls(c))return true;return false;}
        String name(String n,Set<String> staged){Symbol sym=names.get(n);return references?FC+".lookup("+literal(sym.original)+")":FC+".value("+(staged.contains(n)?FC+".staged("+literal(sym.original)+")":sym.name)+", "+literal(sym.original)+")";}
        String declaration(String n){return "$_fc_types["+literal(names.get(n).name)+"]";}
        String parameter(String n){FlowSource.Parameter p=findParameter(n);return p==null?"$null":spec(p.type,p.isArray);}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block){return expression(e,indent,block,null,0,false);}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block,Set<String> staged){return expression(e,indent,block,staged,0,false);}
        String child(FlowSource.Expression e,int indent,FlowSource.Block block,Set<String> staged,int depth,boolean track,int key){String result=expression(e,indent,block,staged,depth+1,track);return "("+type("Value")+(track?FC+".hold("+key+", "+result+")":result)+")";}
        String finish(FlowSource.Expression e,String value,boolean track,int key){return "("+type("Value")+(track&&!e.children.isEmpty()?FC+".release("+key+", "+value+")":value)+")";}
        FlowSource.Scope callee(String id){for(FlowSource.Scope s:program.scopes)if(Objects.equals(s.id,id))return s;throw new IllegalArgumentException("Funzione non disponibile.");}
        String expression(FlowSource.Expression e,int indent,FlowSource.Block block,Set<String> staged,int depth,boolean track){
            if(e==null)return "$null";if(staged==null)staged=caseSet();if(depth==0)track|=calls(e);if(depth>=2&&!e.children.isEmpty()){Helper h=new Helper();h.name="Invoke-FrociartExpr_"+(++helperSerial);h.expression=e;h.block=block;h.staged=caseSet();h.staged.addAll(staged);h.track=track;helpers.add(h);return "("+h.name+")";}
            int key=track?++temporary:0;String result;
            switch(e.kind){
                case Number:return number(e.number);
                case Text:return type("Value")+"::text("+literal(e.text)+")";
                case Boolean:return type("Value")+"::logical("+bool(e.bool)+")";
                case Variable:return name(e.text,staged);
                case Vector:List<String> values=new ArrayList<>();for(FlowSource.Expression c:e.children)values.add(child(c,indent,block,staged,depth,track,key));result=type("Value")+"::vector("+type("Value[]")+"@("+String.join(", ",values)+"))";break;
                case Index:String target=child(e.children.get(0),indent,block,staged,depth,track,key);result=references&&e.children.get(0).kind==FlowSource.ExpressionKind.Variable?FC+".atName("+literal(names.get(e.children.get(0).text).original)+", [Func"+type("Value")+"]{ "+child(e.children.get(1),indent,block,staged,depth,track,key)+" })":FC+".at("+target+", [Func"+type("Value")+"]{ "+child(e.children.get(1),indent,block,staged,depth,track,key)+" })";break;
                case Unary:result=FC+".unary("+literal(e.text)+", "+child(e.children.get(0),indent,block,staged,depth,track,key)+")";break;
                case Call:FlowSource.Scope called=e.functionId==null?null:callee(e.functionId);List<String> args=new ArrayList<>();for(int i=0;i<e.children.size();i++){FlowSource.Expression c=e.children.get(i);args.add(called!=null&&called.parameters.get(i).byReference?FC+".hold("+key+", "+FC+".reference("+literal(names.get(c.text).original)+", "+declaration(c.text)+", "+parameter(c.text)+"))":child(c,indent,block,staged,depth,track,key));}
                    if(called==null){result=FC+".intrinsic("+literal(e.text)+", "+type("Value[]")+"@("+String.join(", ",args)+"))";break;}
                    List<String> wrapped=new ArrayList<>();for(String a:args)wrapped.add("("+a+")");return FC+".call("+key+", [Func"+type("Value")+"]{ "+functions.get(e.functionId)+(args.isEmpty()?"":" "+String.join(" ",wrapped))+" })";
                case Binary:String left=child(e.children.get(0),indent,block,staged,depth,track,key),right=child(e.children.get(1),indent,block,staged,depth,track,key);if(e.text.equals("and")||e.text.equals("or"))result=type("Value")+"::logical((("+left+").asBoolean() "+(e.text.equals("and")?"-and":"-or")+" ("+right+").asBoolean()))";else result=FC+".binary("+literal(e.text)+", "+left+", "+right+")";break;
                default:throw new IllegalArgumentException("Espressione PowerShell non supportata.");
            }return finish(e,result,track,key);
        }
        String flatStore(String code,int indent,FlowSource.Block block){String local="$_fc_flat"+(++temporary);line(type("Value")+" "+local+" = "+code,indent,block);return local;}
        void flatHold(String local,int key,int indent,FlowSource.Block block,boolean track){if(track)line(local+" = "+FC+".hold("+key+", "+local+")",indent,block);}
        // Evaluate each complex expression in one helper frame while preserving lazy operands.
        String flat(FlowSource.Expression e,int indent,FlowSource.Block block,Set<String> staged,boolean track){
            if(e.kind==FlowSource.ExpressionKind.Number||e.kind==FlowSource.ExpressionKind.Text||e.kind==FlowSource.ExpressionKind.Boolean||e.kind==FlowSource.ExpressionKind.Variable)return flatStore(expression(e,indent,block,staged,0,track),indent,block);
            int retained=track?++temporary:0;List<String> children=new ArrayList<>();String result;
            if(e.kind==FlowSource.ExpressionKind.Index){
                boolean dynamicName=references&&e.children.get(0).kind==FlowSource.ExpressionKind.Variable;String target=null;
                if(!dynamicName){target=flat(e.children.get(0),indent,block,staged,track);flatHold(target,retained,indent,block,track);children.add(target);}
                result="$_fc_flat"+(++temporary);line(type("Value")+" "+result+" = "+FC+(dynamicName?".atName("+literal(names.get(e.children.get(0).text).original):".at("+target)+", [Func"+type("Value")+"]{",indent,block);
                String index=flat(e.children.get(1),indent+1,block,staged,track);line("return "+(track?FC+".hold("+retained+", "+index+")":index),indent+1,block);line("})",indent,block);
            }else if(e.kind==FlowSource.ExpressionKind.Binary&&(e.text.equals("and")||e.text.equals("or"))){
                String left=flat(e.children.get(0),indent,block,staged,track);flatHold(left,retained,indent,block,track);children.add(left);boolean conjunction=e.text.equals("and");result=flatStore(type("Value")+"::logical("+bool(!conjunction)+")",indent,block);
                line("if ("+(conjunction?"":"-not ")+left+".asBoolean()) {",indent,block);String right=flat(e.children.get(1),indent+1,block,staged,track);if(track)line(right+" = "+FC+".hold("+retained+", "+right+")",indent+1,block);line(result+" = "+type("Value")+"::logical("+right+".asBoolean())",indent+1,block);line(right+" = $null",indent+1,block);line("}",indent,block);
            }else{
                FlowSource.Scope called=e.functionId==null?null:callee(e.functionId);
                for(int i=0;i<e.children.size();i++){FlowSource.Expression child=e.children.get(i);String value=called!=null&&called.parameters.get(i).byReference?flatStore(FC+".reference("+literal(names.get(child.text).original)+", "+declaration(child.text)+", "+parameter(child.text)+")",indent,block):flat(child,indent,block,staged,track);flatHold(value,retained,indent,block,track);children.add(value);}
                switch(e.kind){
                    case Vector:result=flatStore(type("Value")+"::vector("+type("Value[]")+"@("+String.join(", ",children)+"))",indent,block);break;
                    case Unary:result=flatStore(FC+".unary("+literal(e.text)+", "+children.get(0)+")",indent,block);break;
                    case Binary:result=flatStore(FC+".binary("+literal(e.text)+", "+children.get(0)+", "+children.get(1)+")",indent,block);break;
                    case Call:if(called==null)result=flatStore(FC+".intrinsic("+literal(e.text)+", "+type("Value[]")+"@("+String.join(", ",children)+"))",indent,block);else{List<String> wrapped=new ArrayList<>();for(String v:children)wrapped.add("("+v+")");result=flatStore(FC+".call("+retained+", [Func"+type("Value")+"]{ "+functions.get(e.functionId)+(children.isEmpty()?"":" "+String.join(" ",wrapped))+" })",indent,block);}break;
                    default:throw new IllegalArgumentException("Espressione PowerShell non supportata.");
                }
            }
            if(track&&!e.children.isEmpty()&&e.functionId==null)line(result+" = "+FC+".release("+retained+", "+result+")",indent,block);
            for(String child:children)line(child+" = $null",indent,block);return result;
        }
        String test(FlowSource.Block block,int indent){return FC+".test([Func"+type("Value")+"]{ "+expression(block.operation.value,indent,block)+" })";}
        void statements(List<FlowSourcePlan.Statement> statements,int indent){for(FlowSourcePlan.Statement s:statements){FlowSource.Block b=s.block;if(s.kind==FlowSourcePlan.Kind.Instruction){instruction(b,indent);continue;}if(s.kind==FlowSourcePlan.Kind.Repeat){line("do {",indent,s.entryBlock());statements(s.body,indent+1);String test=test(b,indent);line("} while ("+(s.loopOnTrue?"":"-not ")+test+")",indent,b);continue;}String condition=test(b,indent);if(s.kind==FlowSourcePlan.Kind.While){line("while ("+(s.loopOnTrue?"":"-not ")+condition+") {",indent,b);statements(s.body,indent+1);line("}",indent,b);}else{line("if ("+condition+") {",indent,b);statements(s.body,indent+1);line("} else {",indent,b);statements(s.otherwise,indent+1);line("}",indent,b);}}}
        void explicit(int indent){line("# Collegamenti espliciti per conservare questo grafo.",indent);line("$_fc_block = "+literal(scope.startId),indent);line("while ($true) {",indent);line("switch -CaseSensitive ($_fc_block) {",indent+1);for(FlowSource.Block b:scope.blocks){line(literal(b.id)+" {",indent+2,b);if(b.kind==DECISION)line("if ("+test(b,indent+3)+") { $_fc_block = "+literal(b.next("true"))+" } else { $_fc_block = "+literal(b.next("false"))+" }",indent+3,b);else{instruction(b,indent+3);if(b.kind!=END)line("$_fc_block = "+literal(b.next("next")),indent+3,b);}line("break",indent+3,b);line("}",indent+2,b);}line("default { throw 'Collegamento non valido nel sorgente modificato.' }",indent+2);line("}",indent+1);line("}",indent);}
        void emit(String text,int indent,FlowSource.Block b){line(text,indent,b);}
        void instruction(FlowSource.Block b,int indent){FlowSource.Operation o=b.operation;emit(FC+(b.kind==INPUT?".guard()":".step()"),indent,b);if(b.kind==START||b.kind==PAUSE)return;
            if(b.kind==END){if(scope.id==null){emit(FC+".finishFiles()",indent,b);emit("return $null",indent,b);}else if(scope.returnType==VOID)emit("return $null",indent,b);else emit("return "+FC+".checkType("+expression(o.value,indent,b)+", "+scope.returnType+", "+bool(scope.returnIsArray)+")",indent,b);return;}
            if(b.kind==PROCESS){emit(FC+".begin()",indent,b);Set<String> staged=caseSet();List<String> order=new ArrayList<>();for(FlowSource.Write w:o.writes){String value=expression(w.value,indent,b,staged);if(w.target.index!=null)value=references?FC+".setAfterName("+value+", "+literal(names.get(w.target.name).original)+", [Func"+type("Value")+"]{ "+expression(w.target.index,indent,b,staged)+" })":FC+".setAfter("+value+", "+name(w.target.name,staged)+", [Func"+type("Value")+"]{ "+expression(w.target.index,indent,b,staged)+" })";emit("$null = "+FC+".stage("+literal(names.get(w.target.name).original)+", "+FC+".convert("+value+", "+declaration(w.target.name)+", "+parameter(w.target.name)+"), $true, "+bool(references&&w.target.index!=null)+")",indent,b);if(staged.add(w.target.name))order.add(w.target.name);}emit(FC+".commit()",indent,b);for(String n:order)emit(names.get(n).name+" = "+FC+".variable("+literal(names.get(n).original)+")",indent,b);return;}
            if(b.kind==DECLARE){FlowSource.Declaration d=o.declaration;String size="$_fc_size"+(++temporary);emit(size+" = "+type("Intrinsics")+"::integer("+(d.isArray()?expression(d.size,indent,b):number(0))+", 0, 1024, 'La dimensione')",indent,b);emit(FC+".begin()",indent,b);for(String n:d.names)emit("$null = "+FC+".stage("+literal(names.get(n).original)+", "+FC+".initial("+d.type+", "+bool(d.isArray())+", "+size+"), $false)",indent,b);emit(FC+".commit($true)",indent,b);for(String n:d.names){emit(names.get(n).name+" = "+FC+".variable("+literal(names.get(n).original)+")",indent,b);emit(declaration(n)+" = ["+support+"]::Spec("+d.type+", "+bool(d.isArray())+", "+size+")",indent,b);}return;}
            if(b.kind==INPUT||b.kind==READ_FILE){FlowSource.Target t=o.input;String vector=t.index==null?"$null":name(t.name,caseSet()),index=t.index==null?"$null":"[Func"+type("Value")+"]{ "+expression(t.index,indent,b)+" }";emit(names.get(t.name).name+" = "+FC+"."+(b.kind==INPUT?"read":"readFile")+"("+literal(names.get(t.name).original)+", "+(b.kind==INPUT?literal(b.prompt==null?"":b.prompt)+", ":"")+declaration(t.name)+", "+parameter(t.name)+", "+vector+", "+index+")",indent,b);if(b.kind==INPUT)emit(FC+".step()",indent,b);return;}
            if(b.kind==OPEN_FILE)emit(FC+".requireClosed("+bool(o.fileReading)+")",indent,b);if(b.kind==WRITE_FILE)emit(FC+".requireWrite()",indent,b);String value=expression(o.value,indent,b),code;
            switch(b.kind){case OUTPUT:code=FC+".emit("+value+", "+bool(o.outputNewline)+")";break;case CALL:code="$null = "+value;break;case OPEN_FILE:code=FC+".openFile("+bool(o.fileReading)+", "+value+")";break;case WRITE_FILE:code=FC+".writeFile("+value+", "+bool(o.fileNewline)+")";break;case CLOSE_FILE:code=FC+".closeFile("+bool(o.fileReading)+")";break;case TURTLE_FORWARD:case TURTLE_TURN:case TURTLE_HOME:case TURTLE_CLEAR:case TURTLE_PEN:code=FC+".turtle("+b.kind+", "+value+", "+bool(b.kind==TURTLE_FORWARD?o.turtleDraw:o.turtleLeft)+")";break;default:throw new IllegalArgumentException("Blocco PowerShell non supportato.");}emit(code,indent,b);
        }
    }
}
