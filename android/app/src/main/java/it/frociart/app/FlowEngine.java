package it.frociart.app;

import java.math.BigDecimal;
import java.math.MathContext;
import java.math.RoundingMode;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Collections;
import java.util.BitSet;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import static it.frociart.app.FlowModel.*;

/** Bounded, local flowchart compiler/interpreter. It does not execute Java, JavaScript or system commands. */
public final class FlowEngine {
    private FlowEngine() {}
    public static final int MAX_ISSUES=2000;
    public enum State { READY, RUNNING, WAITING, COMPLETED, ERROR, WAITING_FILE }
    public static final class Issue {
        public String nodeId,message,suggestion,code,functionId;
        public boolean error;
        public Issue(boolean error,String nodeId,String code,String message,String suggestion){this.error=error;this.nodeId=nodeId;this.code=code;this.message=message;this.suggestion=suggestion;}
        @Override public String toString(){return message;}
    }
    public static final class Value {
        private final Object raw;
        private Value(Object raw){this.raw=raw;}
        public static Value number(double n){return new Value(finite(n));}
        public static Value text(String s){if(s==null)s="";if(s.length()>32768)throw problem("Il testo supera il limite di 32768 caratteri.");return new Value(s);}
        public static Value bool(boolean b){return new Value(b);}
        public static Value vector(List<Value> items){return new Value(new Vector(items));}
        public boolean isVector(){return raw instanceof Vector;}
        public int vectorSize(){return vector().items.size();}
        public Value element(int index){return vector().at(index);}
        private Vector vector(){if(!isVector())throw problem("Serve un vettore, per esempio valori = [10, 20, 30].");return(Vector)raw;}
        public boolean isNumber(){return raw instanceof Double;}
        public boolean isText(){return raw instanceof String;}
        public boolean isBoolean(){return raw instanceof Boolean;}
        public double asNumber(){if(!isNumber())throw problem("Serve un numero, ma il valore è «"+shortText(format(),60)+"».");return(Double)raw;}
        public boolean asBoolean(){if(isBoolean())return(Boolean)raw;if(isNumber())return asNumber()!=0;throw problem("La condizione deve restituire vero/falso o un numero, non un testo.");}
        public String format(){
            if(isVector())return vector().format();
            if(isBoolean())return(Boolean)raw?"vero":"falso";
            if(isNumber()){
                double n=(Double)raw;if(n==0)return "0";if(Math.abs(n)<1e15&&n==Math.rint(n))return Long.toString((long)n);
                BigDecimal v=new BigDecimal(n).round(new MathContext(15,RoundingMode.HALF_UP)).stripTrailingZeros();int exponent=v.precision()-v.scale()-1;
                if(exponent>=15||exponent<-4){String digits=v.unscaledValue().abs().toString();int e=Math.abs(exponent);return(v.signum()<0?"-":"")+digits.substring(0,1)+(digits.length()>1?"."+digits.substring(1):"")+"E"+(exponent>=0?"+":"-")+(e<10?"0":"")+e;}
                return v.toPlainString();
            }
            return(String)raw;
        }
        @Override public String toString(){return format();}
    }
    private static final class Vector {
        static final int MAX=1024;final List<Value> items;
        Vector(List<Value> values){if(values.size()>MAX)throw problem("Un vettore può contenere al massimo 1024 elementi.");ArrayList<Value> copy=new ArrayList<>(values);int kind=-1,total=2;for(Value value:copy){int type=scalar(value);if(kind<0)kind=type;else if(kind!=type)throw problem("Tutti gli elementi del vettore devono avere lo stesso tipo.");total+=elementText(value).length()+2;if(total>32768)throw problem("Il contenuto del vettore supera 32768 caratteri.");}items=java.util.Collections.unmodifiableList(copy);}
        static int scalar(Value value){if(value.isNumber())return 0;if(value.isBoolean())return 1;if(value.isText())return 2;throw problem("I vettori contengono numeri, testi o valori logici; i vettori annidati non sono supportati.");}
        static String elementText(Value value){return value.isText()?"\""+((String)value.raw).replace("\\","\\\\").replace("\"","\\\"").replace("\n","\\n").replace("\r","\\r").replace("\t","\\t")+"\"":value.format();}
        String format(){StringBuilder text=new StringBuilder("[");for(Value value:items){if(text.length()>1)text.append(", ");text.append(elementText(value));}return text.append(']').toString();}
        static int integer(Value value,String label){double n=value.asNumber();if(n<0||n>Integer.MAX_VALUE||n!=Math.floor(n))throw problem(label+" deve essere un intero maggiore o uguale a 0.");return(int)n;}
        int index(Value value){int i=integer(value,"L’indice");if(i>=items.size())throw problem(items.isEmpty()?"Il vettore è vuoto: non contiene elementi.":"Indice "+i+" fuori dal vettore. Usa un indice da 0 a "+(items.size()-1)+".");return i;}
        Value at(int i){return items.get(index(Value.number(i)));}
        Value set(int i,Value value){Value old=at(i);if(scalar(old)!=scalar(value))throw problem("Il nuovo valore deve avere lo stesso tipo degli elementi del vettore.");ArrayList<Value> next=new ArrayList<>(items);next.set(i,value);return Value.vector(next);}
        @Override public boolean equals(Object object){if(!(object instanceof Vector))return false;Vector other=(Vector)object;if(items.size()!=other.items.size())return false;for(int i=0;i<items.size();i++)if(items.get(i).isNumber()&&other.items.get(i).isNumber()?items.get(i).asNumber()!=other.items.get(i).asNumber():!items.get(i).raw.equals(other.items.get(i).raw))return false;return true;}
        @Override public int hashCode(){int hash=17;for(Value value:items)hash=hash*31+value.raw.hashCode();return hash;}
    }
    static final class Problem extends IllegalArgumentException {Problem(String m){super(m);}}
    private static Problem problem(String m){return new Problem(m);}
    private static double finite(double n){if(Double.isNaN(n)||Double.isInfinite(n))throw problem("Il risultato non è un numero finito: controlla dominio e grandezza dei valori.");return n;}
    private static String lower(String s){return s.toLowerCase(Locale.ROOT);}
    private static String shortText(String s,int length){return s.length()<=length?s:s.substring(0,length-3)+"...";}
    private static boolean digit(char c){return c>='0'&&c<='9';}
    // Restrict regex code-point matching to the existing UTF16 char contract.
    static final String IDENTIFIER_PATTERN="[\\p{L}_&&[\\x{0000}-\\x{FFFF}]][\\p{L}\\p{Nd}_&&[\\x{0000}-\\x{FFFF}]]{0,63}";
    static final String IDENTIFIER_WORD_PATTERN="[\\p{L}_&&[\\x{0000}-\\x{FFFF}]][\\p{L}\\p{Nd}_&&[\\x{0000}-\\x{FFFF}]]*";
    public static boolean identifier(String s){
        if(s==null||s.length()==0||s.length()>64||!(Character.isLetter(s.charAt(0))||s.charAt(0)=='_'))return false;
        for(int i=1;i<s.length();i++)if(!(Character.isLetterOrDigit(s.charAt(i))||s.charAt(i)=='_'))return false;
        String n=lower(s);return !n.equals("true")&&!n.equals("false")&&!n.equals("vero")&&!n.equals("falso")&&!n.equals("and")&&!n.equals("or")&&!n.equals("not")&&!n.equals("e")&&!n.equals("o")&&!n.equals("non");
    }
    public static Value evaluate(String expression,Map<String,Value> variables){return parse(expression).eval(new Variables(variables));}
    public static List<String> referencedVariables(String expression){LinkedHashSet<String> names=new LinkedHashSet<>();parse(expression).reads(names);return new ArrayList<>(names);}
    public static Value parseInput(String raw){
        if(raw==null)raw="";if(raw.length()>32768)throw problem("Il valore inserito è troppo lungo (massimo 32768 caratteri).");String s=trimInput(raw),numeric=numericInputText(s);
        if(numericInput(numeric)) {try{double value=Double.parseDouble(numeric.replace(',','.'));if((Double.isInfinite(value)||Double.isNaN(value))&&!numeric.equals(s))return Value.text(raw);return Value.number(value);}catch(NumberFormatException e){throw problem("Il numero inserito è troppo grande. Usa un numero finito più piccolo.");}}
        String n=lower(s);if(n.equals("true")||n.equals("vero"))return Value.bool(true);if(n.equals("false")||n.equals("falso"))return Value.bool(false);
        if(n.equals("nan")||n.equals("infinity")||n.equals("+infinity")||n.equals("-infinity")||n.equals("infinito"))throw problem("Inserisci un numero finito.");
        return Value.text(raw);
    }
    // Invariant whitespace matches the Windows engine; String.trim also strips control data
    // and misses NBSP/Unicode separators. Keep the original text if it is not numeric.
    private static boolean inputSpace(char c){return c>=9&&c<=13||c==32||c==0x85||c==0xa0||c==0x1680||c>=0x2000&&c<=0x200a||c==0x2028||c==0x2029||c==0x202f||c==0x205f||c==0x3000;}
    private static String trimInput(String s){int first=0,last=s.length();while(first<last&&inputSpace(s.charAt(first)))first++;while(last>first&&inputSpace(s.charAt(last-1)))last--;return s.substring(first,last);}
    private static String numericInputText(String s){int last=s.length();while(last>0&&s.charAt(last-1)==0)last--;while(last>0&&(s.charAt(last-1)==32||s.charAt(last-1)>=9&&s.charAt(last-1)<=13))last--;return s.substring(0,last);}
    private static boolean numericInput(String s){int p=0,count=0;if(p<s.length()&&(s.charAt(p)=='+'||s.charAt(p)=='-'))p++;while(p<s.length()&&digit(s.charAt(p))){p++;count++;}if(p<s.length()&&(s.charAt(p)=='.'||s.charAt(p)==',')){p++;while(p<s.length()&&digit(s.charAt(p))){p++;count++;}}if(count==0)return false;if(p<s.length()&&(s.charAt(p)=='e'||s.charAt(p)=='E')){p++;if(p<s.length()&&(s.charAt(p)=='+'||s.charAt(p)=='-'))p++;int begin=p;while(p<s.length()&&digit(s.charAt(p)))p++;if(p==begin)return false;}return p==s.length();}

    /** Case-insensitive lookup while retaining the spelling and ordering used on first assignment. */
    private static final class Variables extends LinkedHashMap<String,Value> {
        private final HashMap<String,String> keys=new HashMap<>();
        Variables(){}
        Variables(Map<String,Value> source){if(source!=null)putAll(source);}
        @Override public Value put(String key,Value value){String normalized=lower(key),original=keys.get(normalized);if(original==null){original=key;keys.put(normalized,key);}return super.put(original,value);}
        @Override public void putAll(Map<? extends String,? extends Value> values){for(Map.Entry<? extends String,? extends Value> e:values.entrySet())put(e.getKey(),e.getValue());}
        @Override public Value get(Object key){return key instanceof String?super.get(keys.get(lower((String)key))):null;}
        @Override public boolean containsKey(Object key){return key instanceof String&&keys.containsKey(lower((String)key));}
        @Override public Value remove(Object key){if(!(key instanceof String))return null;String original=keys.remove(lower((String)key));return super.remove(original);}
        @Override public void clear(){super.clear();keys.clear();}
    }
    // Package-private compiler views are shared with the source backend; they are never evaluated by it.
    interface Expr {Value eval(Map<String,Value> vars);void reads(Set<String> names);}
    static final class Literal implements Expr {
        final Value value;Literal(Value value){this.value=value;}public Value eval(Map<String,Value> vars){return value;}public void reads(Set<String> names){}
    }
    static final class Variable implements Expr {
        final String name;Variable(String name){this.name=name;}public Value eval(Map<String,Value> vars){Value value=vars.get(name);if(value==null){if(name.equalsIgnoreCase("pi")||name.equals("π"))return Value.number(Math.PI);throw problem("La variabile «"+name+"» non ha ancora un valore.");}return value;}public void reads(Set<String> names){if(!name.equalsIgnoreCase("pi")&&!name.equals("π"))names.add(lower(name));}
    }
    static final class VectorExpr implements Expr {
        final List<Expr> items;VectorExpr(List<Expr> items){this.items=items;}public Value eval(Map<String,Value> vars){ArrayList<Value> values=new ArrayList<>();for(Expr item:items)values.add(item.eval(vars));return Value.vector(values);}public void reads(Set<String> names){for(Expr item:items)item.reads(names);}
    }
    static final class IndexExpr implements Expr {
        final Expr target,index;IndexExpr(Expr target,Expr index){this.target=target;this.index=index;}public Value eval(Map<String,Value> vars){Vector vector=target.eval(vars).vector();return vector.at(vector.index(index.eval(vars)));}public void reads(Set<String> names){target.reads(names);index.reads(names);}
    }
    static final class Unary implements Expr {
        final String op;final Expr operand;Unary(String op,Expr operand){this.op=op;this.operand=operand;}
        public Value eval(Map<String,Value> vars){Value v=operand.eval(vars);if(op.equals("not"))return Value.bool(!v.asBoolean());return Value.number(op.equals("-")?-v.asNumber():v.asNumber());}
        public void reads(Set<String> names){operand.reads(names);}
    }
    static final class Binary implements Expr {
        final String op;final Expr left,right;Binary(String op,Expr left,Expr right){this.op=op;this.left=left;this.right=right;}
        public Value eval(Map<String,Value> vars){
            Value a=left.eval(vars);if(op.equals("and"))return Value.bool(a.asBoolean()&&right.eval(vars).asBoolean());if(op.equals("or"))return Value.bool(a.asBoolean()||right.eval(vars).asBoolean());Value b=right.eval(vars);
            if(op.equals("&")||op.equals("+")&&(a.isText()||b.isText())){String x=a.format(),y=b.format();if(x.length()+y.length()>32768)throw problem("Il testo prodotto supera il limite di 32768 caratteri.");return Value.text(x+y);}
            if(op.equals("=")||op.equals("==")||op.equals("!=")||op.equals("<>")){boolean same=a.isNumber()&&b.isNumber()?a.asNumber()==b.asNumber():a.raw.equals(b.raw);return Value.bool(op.equals("!=")||op.equals("<>")?!same:same);}
            if(op.equals("<")||op.equals("<=")||op.equals(">")||op.equals(">=")){int c=a.isText()&&b.isText()?((String)a.raw).compareTo((String)b.raw):Double.compare(a.asNumber()==0?0:a.asNumber(),b.asNumber()==0?0:b.asNumber());return Value.bool(op.equals("<")?c<0:op.equals("<=")?c<=0:op.equals(">")?c>0:c>=0);}
            double x=a.asNumber(),y=b.asNumber();switch(op){case "+":return Value.number(x+y);case "-":return Value.number(x-y);case "*":return Value.number(x*y);case "/":if(y==0)throw problem("Divisione per zero: il divisore deve essere diverso da 0.");return Value.number(x/y);case "%":if(y==0)throw problem("Resto con divisore zero: usa un divisore diverso da 0.");return Value.number(x%y);case "^":return Value.number(Math.pow(x,y));default:throw problem("Operatore non riconosciuto.");}
        }
        public void reads(Set<String> names){left.reads(names);if(left instanceof Literal){Value a=((Literal)left).value;if(a.isNumber()||a.isBoolean()){boolean truth=a.asBoolean();if((op.equals("and")&&!truth)||(op.equals("or")&&truth))return;}}right.reads(names);}
    }
    static final class Call implements Expr {
        final String name;final ArrayList<Expr> args;final Function function;
        Call(String name,ArrayList<Expr> args){this(name,args,null);}
        Call(String name,ArrayList<Expr> args,Map<String,Function> functions){this.name=lower(name);this.args=args;function=functions==null?null:functions.get(this.name);int n=args.size();if(function!=null){if(n!=function.parameters.size())throw problem("«"+function.name+"» richiede "+function.parameters.size()+" argomenti; ne hai scritti "+n+".");for(int i=0;i<n;i++)if(function.parameters.get(i).byReference&&!(args.get(i) instanceof Variable))throw problem("Il parametro per riferimento «"+function.parameters.get(i).name+"» richiede il nome di un vettore, non un elemento o un’espressione.");return;}boolean valid;switch(this.name){case "abs":case "sqrt":case "floor":case "len":case "sum":case "somma":case "sin":case "cos":case "tan":case "arcsin":case "arccos":case "arctan":case "ln":case "log":case "log10":case "sgn":case "random":case "size":case "tochar":case "tocode":case "tointeger":case "toreal":case "tostring":valid=n==1;break;case "int":valid=n==1||n==2;break;case "eof":valid=n==0;break;case "char":case "tofixed":valid=n==2;break;case "round":case "array":case "vettore":valid=n==1||n==2;break;case "min":case "max":valid=n>=1&&n<=16;break;default:throw problem("Funzione «"+name+"» sconosciuta. Apri Funzioni ed esempi per vedere quelle disponibili.");}if(!valid)throw problem("Numero di argomenti non valido per «"+name+"».");}
        public Value eval(Map<String,Value> vars){
            if(function!=null)throw problem("Questa funzione va eseguita nel suo progetto.");if(name.equals("eof"))throw problem("EOF() richiede un file aperto nel programma in esecuzione.");Value first=args.get(0).eval(vars);
            if(name.equals("array")||name.equals("vettore")){int length=Vector.integer(first,"La dimensione");if(length>Vector.MAX)throw problem("Un vettore può contenere al massimo 1024 elementi.");Value initial=args.size()==2?args.get(1).eval(vars):Value.number(0);Vector.scalar(initial);return Value.vector(java.util.Collections.nCopies(length,initial));}
            if(name.equals("len")){if(first.isVector())return Value.number(first.vectorSize());if(first.isText())return Value.number(Intrinsics.characterCount((String)first.raw));throw problem("len richiede un testo o un vettore.");}
            if(name.equals("sum")||name.equals("somma")||(name.equals("min")||name.equals("max"))&&args.size()==1){Vector vector=first.vector();if(vector.items.isEmpty()&&!name.equals("sum")&&!name.equals("somma"))throw problem("Il vettore è vuoto: min e max richiedono almeno un elemento.");double result=0;for(int i=0;i<vector.items.size();i++){double value=vector.at(i).asNumber();result=name.equals("min")?(i==0?value:Math.min(result,value)):name.equals("max")?(i==0?value:Math.max(result,value)):finite(result+value);}return Value.number(result);}
            if(Intrinsics.handles(name))return Intrinsics.evaluate(name,args.size()==1?new Value[]{first}:new Value[]{first,args.get(1).eval(vars)});
            double n=first.asNumber();
            switch(name){case "abs":return Value.number(Math.abs(n));case "sqrt":if(n<0)throw problem("La radice quadrata richiede un numero maggiore o uguale a 0.");return Value.number(Math.sqrt(n));case "floor":return Value.number(Math.floor(n));
                case "round":int digits=0;if(args.size()==2){double places=args.get(1).eval(vars).asNumber();if(places<0||places>15||places!=Math.floor(places))throw problem("Le cifre decimali di round devono essere un intero tra 0 e 15.");digits=(int)places;}return Value.number(roundAway(n,digits));
                case "min":case "max":for(int i=1;i<args.size();i++){double next=args.get(i).eval(vars).asNumber();n=name.equals("min")?Math.min(n,next):Math.max(n,next);}return Value.number(n);default:throw problem("Funzione sconosciuta.");}
        }
        public void reads(Set<String> names){for(Expr a:args)a.reads(names);}
    }
    private static final class Intrinsics {
        private static final Set<String> NAMES=new HashSet<>(java.util.Arrays.asList("sin","cos","tan","arcsin","arccos","arctan","int","ln","log","log10","sgn","random","size","char","tochar","tocode","tofixed","tointeger","toreal","tostring"));
        private static final java.util.regex.Pattern INTEGER=java.util.regex.Pattern.compile("^[+-]?[0-9]+$");
        private static final java.util.regex.Pattern REAL=java.util.regex.Pattern.compile("^[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?$");
        static boolean handles(String name){return NAMES.contains(name);}
        static String text(Value value){if(!value.isText())throw problem("Questa funzione richiede un testo tra virgolette o una variabile di testo.");return(String)value.raw;}
        static List<String> characters(String text){
            ArrayList<String> result=new ArrayList<>();
            for(int i=0;i<text.length();i++){
                char ch=text.charAt(i);
                if(Character.isHighSurrogate(ch)){if(i+1>=text.length()||!Character.isLowSurrogate(text.charAt(i+1)))throw problem("Il testo contiene un carattere Unicode incompleto.");result.add(text.substring(i,i+2));i++;}
                else{if(Character.isLowSurrogate(ch))throw problem("Il testo contiene un carattere Unicode incompleto.");result.add(String.valueOf(ch));}
            }
            return result;
        }
        static int characterCount(String text){int count=0;for(int i=0;i<text.length();i++,count++){char ch=text.charAt(i);if(Character.isHighSurrogate(ch)){if(i+1>=text.length()||!Character.isLowSurrogate(text.charAt(i+1)))throw problem("Il testo contiene un carattere Unicode incompleto.");i++;}else if(Character.isLowSurrogate(ch))throw problem("Il testo contiene un carattere Unicode incompleto.");}return count;}
        static int integer(Value value,int minimum,int maximum,String label){double n=value.asNumber();if(n<minimum||n>maximum||n!=Math.floor(n))throw problem(label+" deve essere un intero da "+minimum+" a "+maximum+".");return(int)n;}
        static Value evaluate(String name,Value[] args){
            Value first=args[0];
            if(name.equals("size"))return Value.number(first.vectorSize());
            if(name.equals("char")){List<String> chars=characters(text(first));int i=integer(args[1],0,Integer.MAX_VALUE,"L’indice");if(i>=chars.size())throw problem(chars.isEmpty()?"Il testo è vuoto: non contiene caratteri.":"Indice fuori dal testo. Usa un indice da 0 a "+(chars.size()-1)+".");return Value.text(chars.get(i));}
            if(name.equals("tocode")){List<String> chars=characters(text(first));if(chars.size()!=1)throw problem("tocode richiede esattamente un carattere.");return Value.number(chars.get(0).codePointAt(0));}
            if(name.equals("int")&&args.length==2){if(!(args[1].raw instanceof String)||!((String)args[1].raw).equalsIgnoreCase("passo"))throw problem("Il secondo argomento di int deve essere \"passo\" per controllare il passo di un ciclo.");if(!(first.raw instanceof Double))throw problem("Il passo del ciclo deve essere un numero intero diverso da zero.");double step=(Double)first.raw;if(Double.isNaN(step)||Double.isInfinite(step)||step==0||step!=Math.rint(step)||step<Integer.MIN_VALUE||step>Integer.MAX_VALUE)throw problem("Il passo del ciclo deve essere un intero diverso da zero, da -2147483648 a 2147483647.");return Value.number(step);}
            if(name.equals("tointeger")||name.equals("toreal")){
                String source=trimInput(text(first));boolean whole=name.equals("tointeger");
                if(!(whole?INTEGER:REAL).matcher(source).matches())throw problem(whole?"Scrivi un testo che contenga un numero intero, per esempio \"42\".":"Scrivi un testo che contenga un numero, per esempio \"12.5\".");
                double value;try{value=finite(Double.parseDouble(source.replace(',','.')));}catch(NumberFormatException e){throw problem("Il numero da convertire non è valido.");}
                if(whole&&(value<Integer.MIN_VALUE||value>Integer.MAX_VALUE))throw problem("tointeger accetta valori da -2147483648 a 2147483647.");
                return Value.number(value);
            }
            double n=first.asNumber();
            switch(name){
                case "sin":return Value.number(Math.sin(n));case "cos":return Value.number(Math.cos(n));case "tan":return Value.number(Math.tan(n));
                case "arcsin":case "arccos":if(n < -1||n > 1)throw problem(name+" richiede un numero tra -1 e 1.");return Value.number(name.equals("arcsin")?Math.asin(n):Math.acos(n));
                case "arctan":return Value.number(Math.atan(n));case "int":return Value.number(n<0?Math.ceil(n):Math.floor(n));case "sgn":return Value.number(n<0?-1:n>0?1:0);
                case "ln":case "log":case "log10":if(n<=0)throw problem("Il logaritmo richiede un numero maggiore di 0.");return Value.number(name.equals("log10")?Math.log10(n):Math.log(n));
                case "random":return Value.number(java.util.concurrent.ThreadLocalRandom.current().nextInt(integer(first,1,Integer.MAX_VALUE,"Il limite di random")));
                case "tochar":int code=integer(first,0,0x10FFFF,"Il codice Unicode");if(code>=0xD800&&code<=0xDFFF)throw problem("Il codice non rappresenta un carattere Unicode completo.");return Value.text(new String(Character.toChars(code)));
                case "tostring":return Value.text(first.format());
                case "tofixed":int digits=integer(args[1],0,15,"Le cifre decimali");return Value.text(new BigDecimal(roundAway(n,digits)).setScale(digits,RoundingMode.HALF_UP).toPlainString());
                default:throw problem("Funzione sconosciuta.");
            }
        }
    }
    // Match System.Math.Round(double, digits, AwayFromZero), including binary-double boundaries.
    private static double roundAway(double value,int digits){
        if(Math.abs(value)>=1e16)return value;
        double factor=Math.pow(10,digits),scaled=value*factor,absolute=Math.abs(scaled),whole=Math.floor(absolute);
        if(absolute-whole>=0.5)whole+=1;
        return Math.copySign(whole,scaled)/factor;
    }
    private static final class Token {String kind,text;Value value;int position;Token(String kind,String text,Value value,int position){this.kind=kind;this.text=text;this.value=value;this.position=position;}}
    /** Compiled, read-only debugger condition. User functions and randomness cannot change execution. */
    public static final class DebugCondition {
        private final Expr expression;
        public DebugCondition(String text){
            expression=text==null||text.trim().isEmpty()?null:parse(text);
            if(expression==null)return;
            ArrayDeque<Expr> pending=new ArrayDeque<>();pending.push(expression);
            while(!pending.isEmpty()){
                Expr current=pending.pop();
                if(current instanceof Call&&((Call)current).name.equals("random"))throw problem("Una condizione di pausa non può usare random. Usa un valore già calcolato nel diagramma.");
                for(Expr child:ExpressionMachine.childrenOf(current))pending.push(child);
            }
        }
        public boolean matches(Map<String,Value> variables){return matches(variables,null);}
        public boolean matches(Map<String,Value> variables,java.util.function.BooleanSupplier eof){if(expression==null)return true;ExpressionMachine evaluation=new ExpressionMachine(expression,eof);evaluation.advance(variables);Value value=evaluation.result;if(!value.isBoolean())throw problem("La condizione di pausa deve restituire vero o falso.");return value.asBoolean();}
    }
    /** A pure expression on inspection values; no function catalog or random state. */
    public static final class InspectionExpression {
        private final Expr expression;
        public InspectionExpression(String text){expression=parse(text);ArrayDeque<Expr> pending=new ArrayDeque<>();pending.push(expression);while(!pending.isEmpty()){Expr current=pending.pop();if(current instanceof Call&&((Call)current).name.equals("random"))throw problem("La valutazione non usa numeri casuali. Leggi una variabile già calcolata dal programma.");for(Expr child:ExpressionMachine.childrenOf(current))pending.push(child);}}
        public Value evaluate(Map<String,Value> variables,java.util.function.BooleanSupplier eof){ExpressionMachine evaluation=new ExpressionMachine(expression,eof);evaluation.advance(new Variables(variables));return evaluation.result;}
    }
    private static Expr parse(String expression){return parse(expression,null);}
    private static Expr parse(String expression,Map<String,Function> functions){if(expression==null||expression.trim().isEmpty())throw problem("Scrivi un'espressione.");if(expression.length()>8192)throw problem("L'espressione è troppo lunga.");return new Parser(expression,functions).read();}
    private static final class Parser {
        final ArrayList<Token> tokens;int index,depth,astCount;
        final Map<String,Function> functions;Parser(String text,Map<String,Function> functions){tokens=lex(text);this.functions=functions;}
        Token current(){return tokens.get(index);}
        Expr read(){Expr e=or();if(!current().kind.equals("end"))fail("Elemento inatteso «"+current().text+"».");return e;}
        Expr node(Expr e){if(++astCount>512)throw problem("Espressione troppo complessa (limite 512 elementi).");return e;}
        void enter(){if(++depth>80)throw problem("Troppe parentesi o operazioni annidate (limite 80).");}
        boolean take(String s){Token t=current();if(t.kind.equals(s)||(t.kind.equals("op")&&t.text.equals(s))){index++;return true;}return false;}
        void expect(String s){if(!take(s))fail("Manca «"+s+"».");}
        void fail(String s){throw problem(s+" Posizione "+(current().position+1)+".");}
        Expr or(){Expr e=and();while(take("or"))e=node(new Binary("or",e,and()));return e;}
        Expr and(){Expr e=equality();while(take("and"))e=node(new Binary("and",e,equality()));return e;}
        boolean op(String... choices){if(!current().kind.equals("op"))return false;for(String choice:choices)if(current().text.equals(choice))return true;return false;}
        Expr equality(){Expr e=relation();while(op("=","==","!=","<>")){String s=current().text;index++;e=node(new Binary(s,e,relation()));}return e;}
        Expr relation(){Expr e=concat();while(op("<","<=",">",">=")){String s=current().text;index++;e=node(new Binary(s,e,concat()));}return e;}
        Expr concat(){Expr e=sum();while(take("&"))e=node(new Binary("&",e,sum()));return e;}
        Expr sum(){Expr e=product();while(op("+","-")){String s=current().text;index++;e=node(new Binary(s,e,product()));}return e;}
        // Contextual infix mod preserves existing variable/function names.
        Expr product(){Expr e=unary();while(op("*","/","%")||current().kind.equals("id")&&current().text.equalsIgnoreCase("mod")){String s=current().kind.equals("id")?"%":current().text;index++;e=node(new Binary(s,e,unary()));}return e;}
        Expr unary(){enter();try{if(op("+","-","not")){String s=current().text;index++;return node(new Unary(s,unary()));}return power();}finally{depth--;}}
        Expr power(){Expr e=primary();if(take("^"))e=node(new Binary("^",e,unary()));return e;}
        Expr primary(){Expr result=atom();while(take("[")){Expr at=or();expect("]");result=node(new IndexExpr(result,at));}return result;}
        Expr atom(){
            Token t=current();if(take("[")){ArrayList<Expr> values=new ArrayList<>();if(!take("]")){do{if(values.size()>=Vector.MAX)fail("Massimo 1024 elementi per vettore.");values.add(or());}while(take(","));expect("]");}return node(new VectorExpr(values));}if(take("(")){Expr e=or();expect(")");return e;}
            if(t.kind.equals("number")||t.kind.equals("string")||t.kind.equals("bool")){index++;return node(new Literal(t.value));}
            if(t.kind.equals("id")){index++;if(take("(")){ArrayList<Expr> args=new ArrayList<>();if(!take(")")){do{if(args.size()>=16)fail("Massimo 16 argomenti per funzione.");args.add(or());}while(take(","));expect(")");}return node(new Call(t.text,args,functions));}return node(new Variable(t.text));}
            fail(t.kind.equals("end")?"Espressione incompleta.":"Elemento inatteso «"+t.text+"».");return null;
        }
        static ArrayList<Token> lex(String text){
            ArrayList<Token> result=new ArrayList<>();int i=0;
            while(i<text.length()){
                char c=text.charAt(i);if(Character.isWhitespace(c)){i++;continue;}if(result.size()>=1024)throw problem("Espressione troppo lunga (massimo 1024 elementi).");int start=i;
                if(digit(c)||(c=='.'&&i+1<text.length()&&digit(text.charAt(i+1)))){
                    while(i<text.length()&&digit(text.charAt(i)))i++;if(i<text.length()&&text.charAt(i)=='.'){i++;while(i<text.length()&&digit(text.charAt(i)))i++;}
                    if(i<text.length()&&(text.charAt(i)=='e'||text.charAt(i)=='E')){i++;if(i<text.length()&&(text.charAt(i)=='+'||text.charAt(i)=='-'))i++;int begin=i;while(i<text.length()&&digit(text.charAt(i)))i++;if(i==begin)throw problem("Esponente numerico incompleto alla posizione "+(start+1)+".");}
                    String s=text.substring(start,i);double value;try{value=Double.parseDouble(s);}catch(NumberFormatException e){throw problem("Numero non valido «"+shortText(s,60)+"».");}result.add(new Token("number",s,Value.number(value),start));continue;
                }
                if(Character.isLetter(c)||c=='_'){
                    i++;while(i<text.length()&&(Character.isLetterOrDigit(text.charAt(i))||text.charAt(i)=='_'))i++;String name=text.substring(start,i);if(name.length()>64)throw problem("Nome variabile troppo lungo (massimo 64 caratteri).");String n=lower(name);
                    if(n.equals("true")||n.equals("false")||n.equals("vero")||n.equals("falso"))result.add(new Token("bool",name,Value.bool(n.equals("true")||n.equals("vero")),start));
                    else if(n.equals("and")||n.equals("e")||n.equals("or")||n.equals("o")||n.equals("not")||n.equals("non"))result.add(new Token("op",n.equals("e")?"and":n.equals("o")?"or":n.equals("non")?"not":n,null,start));
                    else result.add(new Token("id",name,null,start));continue;
                }
                if(c=='"'||c=='\''){
                    char quote=c;i++;StringBuilder content=new StringBuilder();boolean closed=false;
                    while(i<text.length()){char next=text.charAt(i++);if(next==quote){closed=true;break;}if(next=='\\'){if(i>=text.length())throw problem("Sequenza di escape incompleta nel testo.");char escape=text.charAt(i++);switch(escape){case 'n':content.append('\n');break;case 'r':content.append('\r');break;case 't':content.append('\t');break;case '\\':case '"':case '\'':content.append(escape);break;default:throw problem("Escape non valido: \\"+escape+". Usa \\\\ per una barra.");}}else content.append(next);}
                    if(!closed)throw problem("Chiudi le virgolette aperte alla posizione "+(start+1)+".");result.add(new Token("string",text.substring(start,i),Value.text(content.toString()),start));continue;
                }
                if(c=='('||c==')'||c==','||c=='['||c==']'){i++;result.add(new Token(String.valueOf(c),String.valueOf(c),null,start));continue;}
                String symbol=String.valueOf(c);if(i+1<text.length()){String two=text.substring(i,i+2);if(two.equals("==")||two.equals("!=")||two.equals("<>")||two.equals("<=")||two.equals(">=")||two.equals("&&")||two.equals("||")){symbol=two;i++;}}
                switch(symbol){case "×":symbol="*";break;case "÷":symbol="/";break;case "↑":symbol="^";break;case "≤":symbol="<=";break;case "≥":symbol=">=";break;case "≠":symbol="!=";break;case "¬":symbol="not";break;case "∧":symbol="and";break;case "∨":symbol="or";break;}
                if(symbol.equals("&&"))symbol="and";if(symbol.equals("||"))symbol="or";if(symbol.equals("!"))symbol="not";
                if(!symbol.equals("&")&&!symbol.equals("+")&&!symbol.equals("-")&&!symbol.equals("*")&&!symbol.equals("/")&&!symbol.equals("%")&&!symbol.equals("^")&&!symbol.equals("=")&&!symbol.equals("==")&&!symbol.equals("!=")&&!symbol.equals("<>")&&!symbol.equals("<")&&!symbol.equals("<=")&&!symbol.equals(">")&&!symbol.equals(">=")&&!symbol.equals("and")&&!symbol.equals("or")&&!symbol.equals("not"))throw problem("Carattere non valido «"+c+"» alla posizione "+(start+1)+".");
                i++;result.add(new Token("op",symbol,null,start));
            }result.add(new Token("end","",null,text.length()));return result;
        }
    }
    static final class Assignment {String name;Expr expression,index;Assignment(String n,Expr e){name=n;expression=e;}
        Expr targetRead(){return index==null?null:new IndexExpr(new Variable(name),index);}
        void write(Map<String,Value> vars,Value value){if(index==null){vars.put(name,value);return;}Value target=vars.get(name);if(target==null)throw problem("La variabile «"+name+"» non ha ancora un valore.");Vector vector=target.vector();vars.put(name,vector.set(vector.index(index.eval(vars)),value));}
    }
    private static Assignment target(String text,Map<String,Function> functions){Expr expr=parse(text,functions);if(expr instanceof Variable)return new Assignment(((Variable)expr).name,null);if(expr instanceof IndexExpr&&((IndexExpr)expr).target instanceof Variable){IndexExpr index=(IndexExpr)expr;Assignment a=new Assignment(((Variable)index.target).name,null);a.index=index.index;return a;}throw problem("Usa un nome di variabile o un elemento del vettore, per esempio valori[i].");}
    private static int assignmentAt(String text){int nesting=0;char quote=0;boolean escape=false;for(int i=0;i<text.length();i++){char c=text.charAt(i);if(quote!=0){if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote=0;continue;}if(c=='\''||c=='"'){quote=c;continue;}if(c=='('||c=='[')nesting++;else if(c==')'||c==']')nesting--;if(nesting==0&&(c=='='&&(i+1==text.length()||text.charAt(i+1)!='=')||c=='<'&&i+1<text.length()&&text.charAt(i+1)=='-'))return i;}throw problem("Usa = o <- per assegnare un valore, per esempio totale = 0.");}
    static final class Block {
        Expr expression,inputIndex,declarationSize;FlowDeclaration declaration;FlowTurtle.Spec turtle;boolean fileReading,fileNewline=true,outputNewline=true;String inputVariable;ArrayList<Assignment> assignments=new ArrayList<>();
        LinkedHashSet<String> definitions(){LinkedHashSet<String> result=new LinkedHashSet<>();if(declaration!=null)for(String name:declaration.names)result.add(lower(name));if(inputVariable!=null&&inputIndex==null)result.add(lower(inputVariable));for(Assignment a:assignments)if(a.index==null)result.add(lower(a.name));return result;}
    }
    static void checkBlock(Node node){compile(node);}
    static void checkBlock(Node node,Map<String,Function> functions,Function owner){compile(node,functions,owner);}
    static void checkExpression(String expression,Map<String,Function> functions){parse(expression,functions);}
    private static Block compile(Node node){return compile(node,null,null);}
    static Block compile(Node node,Map<String,Function> functions,Function owner){
        Block b=new Block();String text=node.text==null?"":node.text.trim();
        if(node.kind==DECISION||node.kind==OUTPUT||node.kind==CALL){if(node.kind==OUTPUT){b.outputNewline=FlowOutput.newline(text);text=FlowOutput.expression(text);}b.expression=parse(text,functions);if(node.kind==CALL&&!(b.expression instanceof Call))throw problem("Scrivi una chiamata, per esempio saluta(nome).");}
        else if(node.kind==END&&owner!=null&&owner.returnType!=VOID)b.expression=new Variable(owner.returnVariable);
        else if(node.kind==INPUT||node.kind==READ_FILE){Assignment target=target(text,functions);b.inputVariable=target.name;b.inputIndex=target.index;}
        else if(FlowTurtle.isTurtle(node.kind)){b.turtle=FlowTurtle.parse(node.kind,text);if(b.turtle.expression!=null)b.expression=parse(b.turtle.expression,functions);}
        else if(node.kind==OPEN_FILE){FlowTextFiles.Open spec=new FlowTextFiles.Open(text);b.fileReading=spec.reading;b.expression=parse(spec.expression,functions);}
        else if(node.kind==WRITE_FILE){FlowTextFiles.Write spec=new FlowTextFiles.Write(text);b.fileNewline=spec.newline;b.expression=parse(spec.expression,functions);}
        else if(node.kind==CLOSE_FILE){b.fileReading=FlowTextFiles.channel(text);}
        else if(node.kind==PAUSE&&node.breakpoint){new DebugCondition(node.breakpointCondition);}
        else if(node.kind==DECLARE){b.declaration=FlowDeclaration.parse(text);if(b.declaration.isArray())b.declarationSize=parse(b.declaration.sizeText,functions);}
        else if(node.kind==PROCESS){List<String> statements=splitStatements(text);if(statements.isEmpty())throw problem("Scrivi un'assegnazione, per esempio totale = a + b.");for(String statement:statements){int i=assignmentAt(statement);Assignment target=target(statement.substring(0,i).trim(),functions);target.expression=parse(statement.substring(i+(statement.charAt(i)=='<'?2:1)),functions);b.assignments.add(target);}}
        if(owner!=null){Set<String> refs=new HashSet<>();for(Parameter p:owner.parameters)if(p.byReference)refs.add(lower(p.name));boolean invalid=b.inputVariable!=null&&b.inputIndex==null&&refs.contains(lower(b.inputVariable));for(Assignment a:b.assignments)invalid|=a.index==null&&refs.contains(lower(a.name));if(b.declaration!=null)for(String name:b.declaration.names)invalid|=refs.contains(lower(name));if(invalid)throw problem("Modifica gli elementi del parametro per riferimento, per esempio valori[i]. Il vettore non può essere riassegnato o ridichiarato.");}
        ExpressionMachine.checkVoid(b.expression,node.kind==CALL);ExpressionMachine.checkVoid(b.inputIndex,false);ExpressionMachine.checkVoid(b.declarationSize,false);for(Assignment a:b.assignments){ExpressionMachine.checkVoid(a.expression,false);ExpressionMachine.checkVoid(a.index,false);}
        return b;
    }
    private static List<String> splitStatements(String text){
        ArrayList<String> result=new ArrayList<>();int start=0,nesting=0;char quote=0;boolean escape=false;
        for(int i=0;i<=text.length();i++){if(i==text.length()){String remaining=text.substring(start).trim();if(!remaining.isEmpty())result.add(remaining);break;}char c=text.charAt(i);if(quote!=0){if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote=0;continue;}if(c=='\''||c=='"'){quote=c;continue;}if(c=='('||c=='[')nesting++;else if(c==')'||c==']')nesting--;if(nesting==0&&(c==';'||c=='\n'||c=='\r')){String s=text.substring(start,i).trim();if(!s.isEmpty())result.add(s);start=i+1;if(result.size()>128)throw problem("Massimo 128 assegnazioni per blocco.");}}
        if(result.size()>128)throw problem("Massimo 128 assegnazioni per blocco.");return result;
    }

    public static List<Issue> validate(Document d){
        ArrayList<Issue> issues=new ArrayList<>();try{FlowCodec.check(d);}catch(IllegalArgumentException ex){add(issues,true,null,"document",ex.getMessage(),"Correggi il file oppure apri un diagramma valido.");return issues;}
        Map<String,Function> functions=FlowFunctions.catalog(d);
        issues.addAll(validateGraph(d,functions,null,d.requiresDeclarations()));
        for(Function f:functions.values()){
            List<Issue> found=validateGraph(f.body,functions,f,d.requiresDeclarations());
            for(Issue issue:found){issue.functionId=f.id;if(issues.size()<MAX_ISSUES)issues.add(issue);else add(issues,true,null,"issue-limit","La verifica ha raggiunto il limite di 2000 segnalazioni.","Correggi gli errori elencati e ripeti la verifica.");}
        }
        return issues;
    }
    private static List<Issue> validateGraph(Document d,Map<String,Function> functions,Function owner,boolean requireDeclarations){
        d=FlowAnnotations.executable(d);
        ArrayList<Issue> issues=new ArrayList<>();
        Graph g=new Graph(d);ArrayList<String> starts=new ArrayList<>(),ends=new ArrayList<>();HashMap<String,Block> compiled=new HashMap<>();
        for(Node n:d.nodes){if(n.kind==START)starts.add(n.id);if(n.kind==END)ends.add(n.id);}
        if(starts.size()!=1)add(issues,true,null,"start-count","Serve esattamente un blocco Inizio (presenti: "+starts.size()+").","Aggiungi un solo Inizio ed elimina gli eventuali duplicati.");
        if(ends.isEmpty())add(issues,true,null,"end-missing","Manca il blocco Fine.","Aggiungi una Fine e collega i percorsi che devono terminare.");
        for(Node n:d.nodes){List<Edge> edges=g.outgoing.get(n.id);
            if(n.kind==START&&!g.incoming.get(n.id).isEmpty())add(issues,true,n.id,"start-incoming","Un collegamento rientra in Inizio.","Fai tornare il ciclo al controllo o all'operazione, senza rientrare in Inizio.");
            if(n.kind==END){if(!edges.isEmpty())add(issues,true,n.id,"end-outgoing","Fine non può avere collegamenti in uscita.","Rimuovi i collegamenti che partono da Fine.");}
            else if(n.kind==DECISION){int yes=0,no=0;for(Edge e:edges){if(e.branch.equals("true"))yes++;if(e.branch.equals("false"))no++;}if(yes!=1||no!=1||edges.size()!=2)add(issues,true,n.id,"decision-branches","La decisione richiede un ramo vero e un ramo falso (presenti: "+yes+" e "+no+").","Collega una sola uscita per ciascun ramo. Le etichette personalizzate non cambiano il risultato della condizione.");}
            else if(edges.size()!=1||!edges.get(0).branch.equals("next"))add(issues,true,n.id,"single-outgoing","Questo blocco richiede esattamente un collegamento normale in uscita.","Collega il blocco successivo con un solo ramo normale.");
            try{compiled.put(n.id,compile(n,functions,owner));}catch(Problem ex){add(issues,true,n.id,"syntax",ex.getMessage(),syntaxSuggestion(n.kind));}
        }
        Set<String> declared=new HashSet<>();if(owner!=null)for(Parameter p:owner.parameters)declared.add(lower(p.name));
        for(Map.Entry<String,Block> entry:compiled.entrySet())if(entry.getValue().declaration!=null)for(String name:entry.getValue().declaration.names)if(!declared.add(lower(name)))add(issues,true,entry.getKey(),"duplicate-declaration","«"+name+"» è già un parametro o una variabile dichiarata.","Usa un solo blocco Dichiara per ciascun nome nella stessa funzione.");
        if(starts.size()==1){Set<String> reached=reach(starts,g.outgoing,false);for(Node n:d.nodes)if(!reached.contains(n.id))add(issues,false,n.id,"unreachable","Questo blocco non è raggiungibile da Inizio.","Collegalo al percorso oppure rimuovilo se non serve.");
            if(!ends.isEmpty()){Set<String> canEnd=reach(ends,g.incoming,true);for(Node n:d.nodes)if(reached.contains(n.id)&&!canEnd.contains(n.id))add(issues,true,n.id,"no-end-path","Da questo blocco non esiste alcun percorso verso Fine.","Aggiungi un'uscita dal ciclo o collega il percorso a Fine.");}
            definiteAssignments(starts.get(0),reached,g,compiled,issues,owner,requireDeclarations);cycles(reached,g,issues);
        }return issues;
    }
    private static final class Graph {
        LinkedHashMap<String,Node> nodes=new LinkedHashMap<>();HashMap<String,ArrayList<Edge>> outgoing=new HashMap<>(),incoming=new HashMap<>();
        Graph(Document d){for(Node n:d.nodes){nodes.put(n.id,n);outgoing.put(n.id,new ArrayList<Edge>());incoming.put(n.id,new ArrayList<Edge>());}for(Edge e:d.edges){outgoing.get(e.from).add(e);incoming.get(e.to).add(e);}}
    }
    private static String syntaxSuggestion(int kind){switch(kind){case PROCESS:return "Esempio: somma = a + b. Per più istruzioni usa una riga per assegnazione.";case DECISION:return "Esempio: numero % 2 = 0 oppure eta >= 18 e biglietto = vero.";case INPUT:return "Scrivi il nome della variabile, per esempio numero. La domanda va nel campo Domanda.";case OUTPUT:return "Esempio: \"Risultato: \" + totale. I testi fissi vanno tra virgolette.";default:return "Controlla il contenuto del blocco.";}}
    private static void add(List<Issue> issues,boolean error,String id,String code,String message,String suggestion){
        if(issues.size()<MAX_ISSUES){issues.add(new Issue(error,id,code,message,suggestion));return;}
        if(!issues.get(MAX_ISSUES-1).code.equals("issue-limit"))issues.set(MAX_ISSUES-1,new Issue(true,null,"issue-limit","La verifica ha raggiunto il limite di 2000 segnalazioni.","Correggi gli errori elencati e ripeti la verifica per vedere le segnalazioni successive."));
    }
    private static Set<String> reach(List<String> seeds,Map<String,ArrayList<Edge>> graph,boolean reverse){LinkedHashSet<String> visited=new LinkedHashSet<>();ArrayDeque<String> queue=new ArrayDeque<>();for(String s:seeds)if(visited.add(s))queue.add(s);while(!queue.isEmpty()){String id=queue.removeFirst();for(Edge e:graph.get(id)){String next=reverse?e.from:e.to;if(visited.add(next))queue.addLast(next);}}return visited;}
    /** Intersection of predecessor definitions; BitSets bound memory even for large diagrams. */
    private static void definiteAssignments(String start,Set<String> reached,Graph graph,Map<String,Block> compiled,List<Issue> issues,Function owner,boolean requireDeclarations){
        Set<String> declared=new HashSet<>();for(Block b:compiled.values())if(b.declaration!=null)for(String name:b.declaration.names)declared.add(lower(name));
        Set<String> required=null;
        if(requireDeclarations){required=new HashSet<>(declared);if(owner!=null){for(Parameter p:owner.parameters)required.add(lower(p.name));if(!owner.returnVariable.isEmpty())required.add(lower(owner.returnVariable));}
            Document forGraph=new Document();forGraph.nodes.addAll(graph.nodes.values());for(ArrayList<Edge> edges:graph.incoming.values())forGraph.edges.addAll(edges);
            for(Node n:graph.nodes.values())if(n.kind==PROCESS&&n.text!=null&&lower(n.text).contains("limite_ciclo_")){FlowPseudocode.ForPattern pattern=FlowPseudocode.inspectFor(forGraph,n);if(pattern!=null)for(String helper:pattern.helperNames)required.add(lower(helper));}
        }
        LinkedHashMap<String,Integer> variables=new LinkedHashMap<>();HashMap<String,Set<String>> names=new HashMap<>();BitSet parameters=new BitSet();if(owner!=null)for(Parameter p:owner.parameters){parameters.set(variables.size());variables.put(lower(p.name),variables.size());}
        for(String id:reached){Block b=compiled.get(id);Set<String> defs=b==null?new LinkedHashSet<String>():b.definitions();if(b==null||b.declaration==null)defs.removeAll(declared);names.put(id,defs);for(String n:defs)if(!variables.containsKey(n))variables.put(n,variables.size());}
        if(variables.size()>4096){add(issues,true,null,"variable-limit","Troppe variabili: massimo 4096.","Semplifica il diagramma.");return;}
        HashMap<String,BitSet> definitions=new HashMap<>(),before=new HashMap<>(),after=new HashMap<>();
        for(String id:reached){BitSet def=new BitSet(),in=new BitSet();for(String n:names.get(id))def.set(variables.get(n));definitions.put(id,def);if(!id.equals(start))in.set(0,variables.size());else in.or(parameters);before.put(id,in);BitSet out=(BitSet)in.clone();out.or(def);after.put(id,out);}
        boolean changed;do{changed=false;for(String id:reached){BitSet in=new BitSet();if(id.equals(start))in.or(parameters);else{boolean first=true;for(Edge e:graph.incoming.get(id)){if(!reached.contains(e.from))continue;if(first){in.or(after.get(e.from));first=false;}else in.and(after.get(e.from));}}BitSet out=(BitSet)in.clone();out.or(definitions.get(id));if(!in.equals(before.get(id))||!out.equals(after.get(id))){before.put(id,in);after.put(id,out);changed=true;}}}while(changed);
        for(String id:reached){Block b=compiled.get(id);if(b==null)continue;BitSet available=(BitSet)before.get(id).clone();HashSet<String> reported=new HashSet<>();if(b.declarationSize!=null)checkReads(b.declarationSize,variables,available,reported,id,issues,required);if(b.inputVariable!=null)checkDeclaredTarget(b.inputVariable,required,reported,id,issues);if(b.inputVariable!=null&&declared.contains(lower(b.inputVariable)))checkReads(new Variable(b.inputVariable),variables,available,reported,id,issues,required);if(b.expression!=null)checkReads(b.expression,variables,available,reported,id,issues,required);if(b.inputIndex!=null)checkReads(new IndexExpr(new Variable(b.inputVariable),b.inputIndex),variables,available,reported,id,issues,required);for(Assignment a:b.assignments){checkDeclaredTarget(a.name,required,reported,id,issues);if(declared.contains(lower(a.name)))checkReads(new Variable(a.name),variables,available,reported,id,issues,required);checkReads(a.expression,variables,available,reported,id,issues,required);if(a.index!=null)checkReads(a.targetRead(),variables,available,reported,id,issues,required);else if(!declared.contains(lower(a.name)))available.set(variables.get(lower(a.name)));}}
    }
    private static void checkReads(Expr expression,Map<String,Integer> variables,BitSet available,Set<String> reported,String id,List<Issue> issues,Set<String> required){LinkedHashSet<String> reads=new LinkedHashSet<>();expression.reads(reads);for(String name:reads){if(required!=null&&!required.contains(lower(name))){checkDeclaredTarget(name,required,reported,id,issues);continue;}Integer bit=variables.get(name);if((bit==null||!available.get(bit))&&reported.add(name))add(issues,true,id,"uninitialized","La variabile «"+name+"» potrebbe essere letta prima di ricevere un valore.",required==null?"Inserisci un input o un'assegnazione prima di questo blocco, in tutti i rami che ci arrivano.":"Metti Dichiara prima dell’uso, in tutti i percorsi che arrivano a questo blocco; poi assegna il valore richiesto.");}}
    private static void checkDeclaredTarget(String name,Set<String> required,Set<String> reported,String id,List<Issue> issues){if(required!=null&&!required.contains(lower(name))&&reported.add(lower(name)))add(issues,true,id,"undeclared","La variabile «"+name+"» non è dichiarata.","Inserisci Dichiara per «"+name+"» e scegli il tipo prima di usarla. Se il nome è scritto per errore, correggi questo blocco.");}
    /** Iterative DFS: no graph-recursion stack pressure on the phone. One warning per detected cycle entry. */
    private static void cycles(Set<String> reached,Graph graph,List<Issue> issues){
        HashMap<String,Integer> colors=new HashMap<>();HashSet<String> reported=new HashSet<>();ArrayList<String> stack=new ArrayList<>();ArrayList<Integer> positions=new ArrayList<>();
        for(String seed:reached){if(colors.containsKey(seed))continue;stack.add(seed);positions.add(0);colors.put(seed,1);while(!stack.isEmpty()){int top=stack.size()-1;String id=stack.get(top);int p=positions.get(top);List<Edge> edges=graph.outgoing.get(id);if(p>=edges.size()){colors.put(id,2);stack.remove(top);positions.remove(top);continue;}positions.set(top,p+1);String to=edges.get(p).to;if(!reached.contains(to))continue;Integer state=colors.get(to);if(state==null){colors.put(to,1);stack.add(to);positions.add(0);}else if(state==1&&reported.add(to))add(issues,false,to,"loop","È presente un ciclo. La verifica strutturale non dimostra che termini.","Controlla che la condizione possa diventare falsa e prova valori limite. L'esecuzione si ferma dopo 10000 blocchi.");}}
    }

    private static final class ExpressionMachine {
        private static final class Frame {
            final Expr expression;final List<Expr> children;final ArrayList<Value> values=new ArrayList<>();Map<Integer,Object> references;
            Frame(Expr expression){this.expression=expression;children=childrenOf(expression);}
        }
        private final ArrayDeque<Frame> stack=new ArrayDeque<>();
        boolean done,awaiting;Value result;Function requestedFunction;List<Value> arguments;List<Object> argumentReferences;
        private final java.util.function.BooleanSupplier eof;
        private final java.util.function.Function<String,Object> reference;
        ExpressionMachine(Expr expression){this(expression,null);}
        ExpressionMachine(Expr expression,java.util.function.BooleanSupplier eof){this(expression,eof,null);}
        ExpressionMachine(Expr expression,java.util.function.BooleanSupplier eof,java.util.function.Function<String,Object> reference){this.eof=eof;this.reference=reference;stack.push(new Frame(expression));}
        static List<Expr> childrenOf(Expr e){
            if(e instanceof Unary)return java.util.Arrays.asList(((Unary)e).operand);
            if(e instanceof Binary)return java.util.Arrays.asList(((Binary)e).left,((Binary)e).right);
            if(e instanceof IndexExpr)return java.util.Arrays.asList(((IndexExpr)e).target,((IndexExpr)e).index);
            if(e instanceof VectorExpr)return ((VectorExpr)e).items;
            if(e instanceof Call)return ((Call)e).args;
            return java.util.Collections.emptyList();
        }
        static void checkVoid(Expr root,boolean allowRootVoid){
            if(root==null)return;ArrayDeque<Expr> pending=new ArrayDeque<>();pending.push(root);
            while(!pending.isEmpty()){Expr e=pending.pop();if(e instanceof Call){Function f=((Call)e).function;if(f!=null&&f.returnType==VOID&&!(allowRootVoid&&e==root))throw problem("«"+f.name+"» non restituisce un valore. Usa un blocco Chiamata.");}for(Expr child:childrenOf(e))pending.push(child);}
        }
        boolean advance(Map<String,Value> variables){
            if(awaiting)throw new IllegalStateException("La chiamata deve terminare prima di riprendere l'espressione.");
            while(!done){
                Frame f=stack.peek();Binary binary=f.expression instanceof Binary?(Binary)f.expression:null;
                if(binary!=null&&f.values.size()==1&&(binary.op.equals("and")||binary.op.equals("or"))){boolean truth=f.values.get(0).asBoolean();if(binary.op.equals("and")&&!truth||binary.op.equals("or")&&truth){finish(Value.bool(truth));continue;}}
                if(f.expression instanceof IndexExpr&&f.values.size()==1)f.values.get(0).vector();
                if(f.values.size()<f.children.size()){stack.push(new Frame(f.children.get(f.values.size())));continue;}
                Call call=f.expression instanceof Call?(Call)f.expression:null;
                if(call!=null&&call.function!=null){awaiting=true;requestedFunction=call.function;arguments=java.util.Collections.unmodifiableList(f.values);argumentReferences=null;if(f.references!=null){argumentReferences=new ArrayList<>();for(int i=0;i<f.values.size();i++)argumentReferences.add(f.references.get(i));}return false;}
                if(call!=null&&call.name.equals("eof")){if(eof==null)throw problem("EOF() richiede un file aperto nel programma in esecuzione.");finish(Value.bool(eof.getAsBoolean()));continue;}
                Expr evaluated=f.expression;
                if(binary!=null)evaluated=new Binary(binary.op,new Literal(f.values.get(0)),new Literal(f.values.get(1)));
                else if(f.expression instanceof Unary)evaluated=new Unary(((Unary)f.expression).op,new Literal(f.values.get(0)));
                else if(f.expression instanceof IndexExpr){IndexExpr index=(IndexExpr)f.expression;Value target=reference!=null&&index.target instanceof Variable?index.target.eval(variables):f.values.get(0);evaluated=new IndexExpr(new Literal(target),new Literal(f.values.get(1)));}
                else if(f.expression instanceof VectorExpr||call!=null){ArrayList<Expr> args=new ArrayList<>();for(Value value:f.values)args.add(new Literal(value));evaluated=call==null?new VectorExpr(args):new Call(call.name,args);}
                Value value=evaluated.eval(variables);if(f.expression instanceof Variable&&stack.size()>1){java.util.Iterator<Frame> path=stack.iterator();path.next();Frame parent=path.next();if(parent.expression instanceof Call){Call pc=(Call)parent.expression;int arg=parent.values.size();if(pc.function!=null&&pc.function.parameters.get(arg).byReference){if(reference==null)throw problem("Un riferimento richiede il programma in esecuzione.");if(parent.references==null)parent.references=new HashMap<>();parent.references.put(arg,reference.apply(((Variable)f.expression).name));}}}finish(value);
            }return true;
        }
        void receive(Value value){if(!awaiting)throw new IllegalStateException("Nessuna chiamata in attesa.");awaiting=false;requestedFunction=null;arguments=null;argumentReferences=null;finish(value);}
        private void finish(Value value){stack.pop();if(stack.isEmpty()){done=true;result=value;}else stack.peek().values.add(value);}
        long retainedSize(){long size=done?valueSize(result):0;for(Frame f:stack)for(Value value:f.values)size+=valueSize(value);return size;}
    }
    private static long valueSize(Value value){
        if(value==null)return 8;if(value.isText())return ((String)value.raw).length();
        if(value.isVector()){long size=value.vectorSize()*8L;for(Value item:value.vector().items)if(item.isText())size+=((String)item.raw).length();return size;}return 8;
    }
    public static final class CallFrameSnapshot {
        public final long frameId;
        public final String functionId,functionName,nodeId;
        public final List<String> parameterNames;
        public final String resultName;
        public final Map<String,Value> variables;
        public final Map<String,String> variableTypes;
        private CallFrameSnapshot(Runner.Frame f){FlowModel.Function function=f.program.function;frameId=f.id;functionId=function==null?null:function.id;functionName=function==null?"Principale":function.name;nodeId=f.nodeId;List<String> parameters=new ArrayList<>();if(function!=null)for(FlowModel.Parameter p:function.parameters)parameters.add(p.name);parameterNames=Collections.unmodifiableList(parameters);resultName=function==null?"":function.returnVariable;variables=java.util.Collections.unmodifiableMap(new Variables(f.variables));variableTypes=Runner.describeTypes(f);}
    }
    public static final class CallFrameInfo {
        public final long frameId;public final String functionId,functionName,nodeId;public final int variableCount;
        private CallFrameInfo(Runner.Frame f){frameId=f.id;functionId=f.program.function==null?null:f.program.function.id;functionName=f.program.function==null?"Principale":f.program.function.name;nodeId=f.nodeId;variableCount=f.variables.size();}
    }
    public static final class Runner {
        private static final class Program {
            final Document document;final Function function;final Graph graph;final Map<String,Block> blocks=new HashMap<>();
            Program(Document document,Function function,Map<String,Function> catalog){this.document=document;this.function=function;graph=new Graph(document);for(Node n:document.nodes)blocks.put(n.id,compile(n,catalog,function));}
        }
        private static final class Frame {
            final Program program;final Variables variables=new Variables();String nodeId;long id,visit;
            final Map<String,FlowDeclaration> declarations=new HashMap<>();
            final Map<String,ArrayCell> arrayBindings=new LinkedHashMap<>(),newArrays=new LinkedHashMap<>();final Map<ArrayCell,ArrayCell> arrayOverlays=new LinkedHashMap<>();final Set<String> wholeWrites=new HashSet<>();
            Variables transaction;ExpressionMachine evaluation;int assignmentIndex;Value assignmentValue;boolean hasAssignmentValue,counted;Integer inputIndex;
            Frame(Program program){this.program=program;for(Node n:program.document.nodes)if(n.kind==START){nodeId=n.id;break;}}
            void clearPending(){transaction=null;arrayOverlays.clear();newArrays.clear();wholeWrites.clear();evaluation=null;assignmentIndex=0;assignmentValue=null;hasAssignmentValue=false;counted=false;inputIndex=null;}
        }
        private static final class ArrayCell{Value value;int type;Frame owner;String name;ArrayCell parent;}
        public final FlowTextFiles files=new FlowTextFiles();
        public final FlowTurtle turtle=new FlowTurtle();
        private FlowTextFiles.Request pendingFile;
        public FlowTextFiles.Request pendingFile(){return pendingFile;}
        public State state;
        public LinkedHashMap<String,Value> variables=new Variables();
        public final FlowConsoleLog consoleLog=new FlowConsoleLog();
        public final ArrayList<String> output=new ArrayList<>();
        private final ArrayList<Boolean> outputNewlineData=new ArrayList<>();public final List<Boolean> outputNewlines=java.util.Collections.unmodifiableList(outputNewlineData);
        private final StringBuilder consoleOutput=new StringBuilder();private String cachedOutput="";public String outputText(){if(cachedOutput==null)cachedOutput=consoleOutput.toString();return cachedOutput;}
        public String currentNodeId,lastNodeId,lastEdgeId,errorMessage,lastMessage,inputPrompt,inputVariable,currentFunctionId,lastFunctionId;
        public String currentFunctionName="Principale";
        public int steps;
        private final Map<String,Program> programs=new HashMap<>();
        private final ArrayList<Frame> frames=new ArrayList<>();
        private Program main;private boolean hasArrayReferences;private String initialError;private int outputCharacters;
        private long nextFrameId,generation;
        public long generation(){return generation;}
        public long currentFrameId(){return active()==null?0:active().id;}
        public long currentVisit(){return active()==null?0:active().visit;}
        public int callDepth(){return frames.size();}
        public boolean currentBlockStarted(){Frame f=active();return f!=null&&(f.counted||f.evaluation!=null||state==State.WAITING||state==State.WAITING_FILE);}
        public boolean hasFrame(long id){for(Frame f:frames)if(f.id==id)return true;return false;}
        private Frame newFrame(Program p){Frame f=new Frame(p);f.id=++nextFrameId;return f;}
        public Runner(Document source){
            for(Issue issue:validate(source))if(issue.error){initialError=issue.message;break;}
            if(initialError==null){Document copy=source.copy();Map<String,Function> catalog=FlowFunctions.catalog(copy);for(Function f:catalog.values())for(Parameter p:f.parameters)hasArrayReferences|=p.byReference;main=new Program(copy,null,catalog);for(Function f:catalog.values())programs.put(f.id,new Program(f.body,f,catalog));}reset();
        }
        private Frame active(){return frames.isEmpty()?null:frames.get(frames.size()-1);}
        private void sync(){Frame f=active();currentNodeId=f==null?null:f.nodeId;currentFunctionId=f==null||f.program.function==null?null:f.program.function.id;currentFunctionName=f==null||f.program.function==null?"Principale":f.program.function.name;variables=f==null?new Variables():f.variables;}
        public Document currentDiagram(){return active()==null?null:active().program.document.copy();}
        public List<CallFrameSnapshot> callStack(){ArrayList<CallFrameSnapshot> result=new ArrayList<>();for(Frame f:frames)result.add(new CallFrameSnapshot(f));return java.util.Collections.unmodifiableList(result);}
        public List<CallFrameInfo> frameInfos(){ArrayList<CallFrameInfo> result=new ArrayList<>();for(Frame f:frames)result.add(new CallFrameInfo(f));return Collections.unmodifiableList(result);}
        public CallFrameSnapshot inspectFrame(long id){if(id==0)return active()==null?null:new CallFrameSnapshot(active());for(Frame f:frames)if(f.id==id)return new CallFrameSnapshot(f);return null;}
        public Map<String,String> variableTypes(){return describeTypes(active());}
        public void reset(){reset(false);}
        public void reset(boolean discardFileOutput){
            if(files.hasWrite()&&!discardFileOutput)throw problem("Il file generato non è ancora salvato. Salvalo oppure scegli esplicitamente di scartarlo prima di ricominciare.");
            files.reset();turtle.reset();pendingFile=null;
            for(Frame f:frames)f.variables.clear();frames.clear();output.clear();outputNewlineData.clear();consoleOutput.setLength(0);consoleLog.clear();cachedOutput="";outputCharacters=0;steps=0;generation++;currentNodeId=lastNodeId=lastEdgeId=currentFunctionId=lastFunctionId=errorMessage=inputPrompt=inputVariable=null;
            state=State.READY;lastMessage="Pronto. Tocca Passo per eseguire Inizio.";if(initialError!=null)fail(initialError);else frames.add(newFrame(main));sync();
        }
        public boolean step(){
            if(state==State.ERROR||state==State.COMPLETED||state==State.WAITING||state==State.WAITING_FILE)return false;
            Frame f=active();if(!f.counted&&steps>=MAX_STEPS){fail("Limite di 10000 blocchi raggiunto. Il ciclo potrebbe non terminare: controlla condizione e aggiornamento delle variabili.");return false;}
            Node n=f.program.graph.nodes.get(f.nodeId);Block b=f.program.blocks.get(f.nodeId);state=State.RUNNING;lastEdgeId=null;errorMessage=null;
            try{
                if(n.kind!=INPUT&&!f.counted){steps++;f.counted=true;markLast(f);}
                Value value;
                switch(n.kind){
                    case START:lastMessage="Inizio dell'esecuzione.";advance(f,"next");return true;
                    case INPUT:
                        f.inputIndex=null;inputVariable=b.inputVariable;
                        if(b.inputIndex!=null){Vector vector=readVector(f.variables,b.inputVariable);if(!evaluate(f,b.inputIndex,f.variables))return true;f.inputIndex=vector.index(takeResult(f));inputVariable+="["+f.inputIndex+"]";}
                        inputPrompt=n.prompt==null||n.prompt.trim().isEmpty()||n.prompt.equals("Inserisci un valore per x")?"Inserisci un valore per "+inputVariable:n.prompt;state=State.WAITING;lastMessage="In attesa di un valore per «"+inputVariable+"».";return false;
                    case PAUSE:lastMessage=n.breakpoint?"Punto di pausa attraversato.":"Pausa disattivata.";advance(f,"next");return true;
                    case TURTLE_FORWARD:case TURTLE_TURN:case TURTLE_HOME:case TURTLE_CLEAR:case TURTLE_PEN:
                        value=null;if(b.expression!=null){if(!evaluate(f,b.expression,f.variables))return true;value=takeResult(f);}turtle.apply(b.turtle,value);lastMessage=n.kind==TURTLE_FORWARD?"Tartaruga spostata.":n.kind==TURTLE_TURN?"Direzione della tartaruga aggiornata.":n.kind==TURTLE_HOME?"Tartaruga tornata all’origine, senza traccia.":n.kind==TURTLE_CLEAR?"Disegno pulito. Tartaruga all’origine.":"Colore della penna aggiornato.";advance(f,"next");return true;
                    case OPEN_FILE:
                        if(b.fileReading?files.hasRead():files.hasWrite())throw problem("Il canale è già aperto. Chiudilo prima di aprire un altro file.");
                        if(!evaluate(f,b.expression,f.variables))return true;
                        String fileName=FlowTextFiles.name(takeResult(f));
                        if(b.fileReading){pendingFile=new FlowTextFiles.Request(FlowTextFiles.Kind.READ,fileName,null);state=State.WAITING_FILE;lastMessage="Scegli il file da leggere: «"+fileName+"».";return false;}
                        files.openWrite(fileName);lastMessage="File «"+fileName+"» preparato. Sarà salvato alla chiusura.";advance(f,"next");return true;
                    case READ_FILE:
                        Integer fileIndex=null;
                        if(b.inputIndex!=null){Vector vector=readVector(f.variables,b.inputVariable);if(!evaluate(f,b.inputIndex,f.variables))return true;fileIndex=vector.index(takeResult(f));}
                        String line=files.peekLine();Value readValue=textInput(f,b.inputVariable,fileIndex!=null)?Value.text(line):parseInput(line);
                        value=fileIndex==null?readValue:readVector(f.variables,b.inputVariable).set(fileIndex,readValue);value=convertWrite(f,b.inputVariable,value);
                        f.transaction=new Variables(f.variables);stageWrite(f,b.inputVariable,value,b.inputIndex!=null);checkMemory();commitTransaction(f);
                        files.commitRead();lastMessage="Letta una riga in «"+b.inputVariable+"».";advance(f,"next");return true;
                    case WRITE_FILE:
                        if(!files.hasWrite())throw problem("Apri prima un file in scrittura.");
                        if(!evaluate(f,b.expression,f.variables))return true;
                        files.write(takeResult(f).format(),b.fileNewline);lastMessage="Testo aggiunto al file «"+files.writeName()+"» da salvare.";advance(f,"next");return true;
                    case CLOSE_FILE:
                        if(b.fileReading){files.closeRead();lastMessage="File in lettura chiuso.";advance(f,"next");return true;}
                        if(!files.hasWrite())throw problem("Nessun file aperto in scrittura da chiudere.");requestSave();return false;
                    case PROCESS:
                        if(f.transaction==null)f.transaction=new Variables(f.variables);
                        while(f.assignmentIndex<b.assignments.size()){
                            Assignment a=b.assignments.get(f.assignmentIndex);
                            if(!f.hasAssignmentValue){if(!evaluate(f,a.expression,f.transaction))return true;f.assignmentValue=takeResult(f);f.hasAssignmentValue=true;}
                            value=f.assignmentValue;
                            if(a.index!=null){Vector vector=readVector(f.transaction,a.name);if(!evaluate(f,a.index,f.transaction))return true;Value index=takeResult(f);if(hasArrayReferences)vector=readVector(f.transaction,a.name);value=vector.set(vector.index(index),value);}
                            value=convertWrite(f,a.name,value);stageWrite(f,a.name,value,a.index!=null);f.assignmentValue=null;f.hasAssignmentValue=false;f.assignmentIndex++;checkMemory();
                        }
                        commitTransaction(f);lastMessage=b.assignments.size()==1?b.assignments.get(0).name+" = "+f.variables.get(b.assignments.get(0).name).format():"Eseguite "+b.assignments.size()+" assegnazioni.";advance(f,"next");return true;
                    case DECLARE:
                        int length=0;if(b.declarationSize!=null){if(!evaluate(f,b.declarationSize,f.variables))return true;length=FlowDeclaration.length(takeResult(f));}
                        f.transaction=new Variables(f.variables);for(String name:b.declaration.names)stageWrite(f,name,b.declaration.initial(length),false);checkMemory();commitTransaction(f);
                        for(String name:b.declaration.names)f.declarations.put(lower(name),b.declaration);lastMessage="Dichiarate: "+String.join(", ",b.declaration.names)+" · "+FlowDeclaration.label(b.declaration.type);advance(f,"next");return true;
                    case DECISION:
                        if(!evaluate(f,b.expression,f.variables))return true;boolean result=takeResult(f).asBoolean();String label=result?n.trueLabel:n.falseLabel;lastMessage="Condizione "+(result?"vera":"falsa")+": segui il ramo «"+label+"».";advance(f,result?"true":"false");return true;
                    case OUTPUT:
                        if(!evaluate(f,b.expression,f.variables))return true;String text=takeResult(f).format();if(outputCharacters+text.length()>262144)throw problem("L'output supera il limite di 256 KiB. Controlla eventuali stampe ripetute nel ciclo.");if(!output.isEmpty()&&outputNewlineData.get(outputNewlineData.size()-1))consoleOutput.append('\n');consoleOutput.append(text);cachedOutput=null;output.add(text);outputNewlineData.add(b.outputNewline);outputCharacters+=text.length();consoleLog.add(FlowConsoleLog.EntryKind.OUTPUT,text,null,null,n.id,currentFunctionId,currentFunctionName,steps,b.outputNewline);lastMessage="Output: "+shortText(text,100);advance(f,"next");return true;
                    case CALL:
                        if(!evaluate(f,b.expression,f.variables))return true;takeResult(f);lastMessage="Chiamata completata.";advance(f,"next");return true;
                    case END:
                        markLast(f);Function function=f.program.function;if(function==null){if(files.hasWrite()){requestSave();return false;}if(files.hasRead())files.closeRead();state=State.COMPLETED;lastMessage="Esecuzione completata in "+steps+" blocchi.";return true;}
                        value=null;if(function.returnType!=VOID){if(!evaluate(f,b.expression,f.variables))return true;value=takeResult(f);FlowFunctions.checkType(value,function.returnType,function.returnIsArray,"Il risultato di «"+function.name+"»");}
                        frames.remove(frames.size()-1);active().evaluation.receive(value);checkMemory();lastMessage="Ritorno da «"+function.name+"»"+(function.returnType==VOID?".":": "+value.format());return true;
                    default:throw problem("Tipo di blocco sconosciuto.");
                }
            }catch(Problem ex){fail(ex.getMessage());return false;}catch(ArithmeticException ex){fail("Il valore numerico è troppo grande.");return false;}finally{sync();}
        }
        private boolean evaluate(Frame caller,Expr expression,Map<String,Value> variables){
            if(caller.evaluation==null)caller.evaluation=new ExpressionMachine(expression,files::eof,hasArrayReferences?name->referenceArgument(caller,variables,name):null);
            if(caller.evaluation.advance(variables))return true;
            if(frames.size()>=FlowFunctions.MAX_DEPTH)throw problem("Troppe chiamate annidate: massimo 128 livelli. Controlla la condizione di arresto della ricorsione.");
            Function function=caller.evaluation.requestedFunction;Frame next=newFrame(programs.get(function.id));
            for(int i=0;i<function.parameters.size();i++){Parameter p=function.parameters.get(i);Value argument=caller.evaluation.arguments.get(i);FlowFunctions.checkType(argument,p.type,p.isArray,"Il parametro «"+p.name+"» di «"+function.name+"»");if(p.byReference){ArrayCell cell=(ArrayCell)caller.evaluation.argumentReferences.get(i);if(cell==null)throw problem("Parametro per riferimento non valido.");if(cell.type!=AUTO&&p.type!=AUTO&&cell.type!=p.type)throw problem("Il tipo del vettore per riferimento «"+p.name+"» deve coincidere con quello originale.");if(cell.type==AUTO&&p.type!=AUTO)cell.type=p.type;argument=cell.value;FlowFunctions.checkType(argument,p.type,true,"Il parametro «"+p.name+"»");next.arrayBindings.put(lower(p.name),cell);}next.variables.put(p.name,argument);}
            frames.add(next);checkMemory();inputPrompt=inputVariable=null;lastMessage="Chiamata a «"+function.name+"».";return false;
        }
        private Value takeResult(Frame frame){Value result=frame.evaluation.result;frame.evaluation=null;return result;}
        private int arrayType(Frame owner,String name){FlowDeclaration d=owner.declarations.get(lower(name));if(d!=null)return d.type;if(owner.program.function!=null)for(Parameter p:owner.program.function.parameters)if(p.name.equalsIgnoreCase(name))return p.type;return AUTO;}
        private ArrayCell cell(Value value,int type,Frame owner,String name,ArrayCell parent){ArrayCell c=new ArrayCell();c.value=value;c.type=type;c.owner=owner;c.name=name;c.parent=parent;return c;}
        private ArrayCell referenceArgument(Frame owner,Map<String,Value> vars,String name){Value value=vars.get(name);if(value==null)throw problem("Il vettore «"+name+"» non ha ancora un valore.");value.vector();String key=lower(name);ArrayCell c;
            if(owner.transaction!=null&&vars==owner.transaction&&(owner.wholeWrites.contains(key)||!owner.variables.containsKey(name))){c=owner.newArrays.get(key);if(c==null){c=cell(value,arrayType(owner,name),owner,key,null);owner.newArrays.put(key,c);}return c;}
            c=owner.arrayBindings.get(key);if(c==null){Value original=owner.variables.get(name);original.vector();c=cell(original,arrayType(owner,name),owner,key,null);owner.arrayBindings.put(key,c);}if(owner.transaction!=null&&vars==owner.transaction){ArrayCell overlay=owner.arrayOverlays.get(c);if(overlay!=null)return overlay;if(value!=c.value)return overlay(owner,c,value);}return c;
        }
        private ArrayCell overlay(Frame owner,ArrayCell parent,Value value){ArrayCell c=owner.arrayOverlays.get(parent);if(c==null){c=cell(value,parent.type,owner,null,parent);owner.arrayOverlays.put(parent,c);}return c;}
        private void propagate(ArrayCell cell){for(Frame f:frames){for(Map.Entry<String,ArrayCell> p:f.arrayBindings.entrySet())if(p.getValue()==cell){f.variables.put(p.getKey(),cell.value);if(f.transaction!=null&&!f.wholeWrites.contains(p.getKey())){ArrayCell over=f.arrayOverlays.get(cell);f.transaction.put(p.getKey(),over==null?cell.value:over.value);}}if(f.transaction!=null){for(Map.Entry<String,ArrayCell> p:f.newArrays.entrySet())if(p.getValue()==cell)f.transaction.put(p.getKey(),cell.value);for(Map.Entry<ArrayCell,ArrayCell> p:f.arrayOverlays.entrySet())if(p.getValue()==cell)for(Map.Entry<String,ArrayCell> alias:f.arrayBindings.entrySet())if(alias.getValue()==p.getKey()&&!f.wholeWrites.contains(alias.getKey()))f.transaction.put(alias.getKey(),cell.value);}}}
        private void checkCell(ArrayCell cell,Value value){if(value.vectorSize()!=cell.value.vectorSize())throw problem("Un vettore per riferimento conserva la dimensione originale.");FlowFunctions.checkType(value,cell.type,true,"Il vettore originale");for(Frame f:frames)for(Map.Entry<String,ArrayCell> p:f.arrayBindings.entrySet())if(p.getValue()==cell){FlowDeclaration d=f.declarations.get(p.getKey());if(d!=null)FlowFunctions.checkType(value,d.type,true,"Il vettore «"+p.getKey()+"»");checkParameter(f,p.getKey(),value);}}
        private void stageWrite(Frame f,String name,Value value,boolean indexed){if(!hasArrayReferences){f.transaction.put(name,value);return;}String key=lower(name);if(!indexed){f.transaction.put(name,value);f.wholeWrites.add(key);f.newArrays.remove(key);return;}ArrayCell c=f.newArrays.get(key);if(f.wholeWrites.contains(key)&&c!=null){checkCell(c,value);c.value=value;propagate(c);return;}c=f.arrayBindings.get(key);if(!f.wholeWrites.contains(key)&&c!=null){ArrayCell over=overlay(f,c,f.transaction.get(name));checkCell(over,value);over.value=value;propagate(over);return;}f.transaction.put(name,value);}
        private void commitTransaction(Frame f){if(!hasArrayReferences){f.variables.clear();f.variables.putAll(f.transaction);return;}for(Map.Entry<ArrayCell,ArrayCell> p:f.arrayOverlays.entrySet())checkCell(p.getKey(),p.getValue().value);for(ArrayCell c:f.newArrays.values())checkCell(c,c.value);for(String name:f.wholeWrites)f.arrayBindings.remove(name);for(Map.Entry<ArrayCell,ArrayCell> p:f.arrayOverlays.entrySet()){p.getKey().value=p.getValue().value;propagate(p.getKey());}f.variables.putAll(f.transaction);f.arrayBindings.putAll(f.newArrays);}

        private void requestSave(){pendingFile=new FlowTextFiles.Request(FlowTextFiles.Kind.SAVE,files.writeName(),files.outputDraft());state=State.WAITING_FILE;lastMessage="Salva il file «"+files.writeName()+"» per continuare.";}
        public boolean provideFile(FlowTextFiles.Request request,String readText){
            if(request==null||request!=pendingFile||state!=State.WAITING_FILE)return false;
            try{
                if(request.kind==FlowTextFiles.Kind.READ)files.openRead(request.name,readText);else files.saved();
                Frame f=active();pendingFile=null;state=State.RUNNING;lastMessage=request.kind==FlowTextFiles.Kind.READ?"File pronto per la lettura.":"File salvato.";
                if(f.program.graph.nodes.get(f.nodeId).kind==END){if(files.hasRead())files.closeRead();state=State.COMPLETED;lastMessage="Esecuzione completata in "+steps+" blocchi.";}else advance(f,"next");return true;
            }catch(Problem ex){lastMessage=ex.getMessage();return false;}finally{sync();}
        }
        public boolean fileDeferred(FlowTextFiles.Request request,String message){
            if(request==null||request!=pendingFile||state!=State.WAITING_FILE)return false;
            lastMessage=message==null||message.trim().isEmpty()?"Operazione annullata. Puoi riprovare; il file generato è ancora disponibile.":message;return true;
        }
        public boolean submitInput(String raw){
            if(state!=State.WAITING)return false;if(steps>=MAX_STEPS){fail("Limite di esecuzione raggiunto.");return false;}
            Frame f=active();Block b=f.program.blocks.get(f.nodeId);
            try{
                Value entered=textInput(f,b.inputVariable,f.inputIndex!=null)?Value.text(raw):parseInput(raw),value=f.inputIndex==null?entered:readVector(f.variables,b.inputVariable).set(f.inputIndex,entered);value=convertWrite(f,b.inputVariable,value);entered=f.inputIndex==null?value:value.element(f.inputIndex);
                f.transaction=new Variables(f.variables);stageWrite(f,b.inputVariable,value,f.inputIndex!=null);checkMemory();commitTransaction(f);
                steps++;markLast(f);consoleLog.add(FlowConsoleLog.EntryKind.INPUT,entered.format(),inputPrompt,inputVariable,f.nodeId,currentFunctionId,currentFunctionName,steps,true);lastMessage=inputVariable+" = "+entered.format();inputPrompt=inputVariable=errorMessage=null;state=State.RUNNING;advance(f,"next");return true;
            }catch(Problem ex){f.transaction=null;f.arrayOverlays.clear();f.newArrays.clear();f.wholeWrites.clear();errorMessage=ex.getMessage();lastMessage=errorMessage;return false;}finally{sync();}
        }
        private static Vector readVector(Map<String,Value> vars,String name){Value value=vars.get(name);if(value==null)throw problem("La variabile «"+name+"» non ha ancora un valore.");return value.vector();}
        private static void checkParameter(Frame f,String name,Value value){if(f.program.function==null)return;for(Parameter p:f.program.function.parameters)if(p.name.equalsIgnoreCase(name)){FlowFunctions.checkType(value,p.type,p.isArray,"Il parametro «"+p.name+"»");return;}}
        private static Value convertWrite(Frame f,String name,Value value){FlowDeclaration declaration=f.declarations.get(lower(name));if(declaration!=null)value=declaration.convert(value,declaration.isArray()?readVector(f.variables,name).items.size():0);checkParameter(f,name,value);return value;}
        private static boolean textInput(Frame f,String name,boolean element){FlowDeclaration declaration=f.declarations.get(lower(name));if(declaration!=null)return declaration.type==TEXT&&(element||!declaration.isArray());if(f.program.function!=null)for(Parameter p:f.program.function.parameters)if(p.name.equalsIgnoreCase(name))return p.type==TEXT&&(element||!p.isArray);return false;}
        private static Map<String,String> describeTypes(Frame f){Map<String,String> result=new LinkedHashMap<>();if(f!=null)for(Map.Entry<String,Value> entry:f.variables.entrySet()){int type=AUTO;FlowDeclaration declaration=f.declarations.get(lower(entry.getKey()));if(declaration!=null)type=declaration.type;else if(f.program.function!=null)for(Parameter p:f.program.function.parameters)if(p.name.equalsIgnoreCase(entry.getKey())){type=p.type;break;}result.put(entry.getKey(),FlowDeclaration.describe(entry.getValue(),type));}return java.util.Collections.unmodifiableMap(result);}
        private void markLast(Frame f){lastNodeId=f.nodeId;lastFunctionId=f.program.function==null?null:f.program.function.id;}
        private void advance(Frame f,String branch){for(Edge edge:f.program.graph.outgoing.get(f.nodeId))if(edge.branch.equals(branch)){markLast(f);lastEdgeId=edge.id;f.nodeId=edge.to;f.visit++;f.clearPending();return;}throw problem("Manca il collegamento successivo.");}
        private void fail(String message){errorMessage=message;lastMessage=message;state=State.ERROR;}
        private void checkMemory(){
            long size=outputCharacters;int slots=0;
            for(Frame f:frames){slots+=f.variables.size()+(f.transaction==null?0:f.transaction.size());for(Value value:f.variables.values())size+=valueSize(value);if(f.transaction!=null)for(Value value:f.transaction.values())size+=valueSize(value);if(f.evaluation!=null)size+=f.evaluation.retainedSize();if(f.hasAssignmentValue)size+=valueSize(f.assignmentValue);if(size>2097152||slots>16384)throw problem("La memoria dei valori supera il limite di esecuzione. Riduci i dati conservati o le chiamate ricorsive.");}
        }
    }
}

