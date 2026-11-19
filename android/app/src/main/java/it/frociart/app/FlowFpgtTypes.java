package it.frociart.app;
import java.util.*;import static it.frociart.app.FlowModel.*;

/** Target-neutral static facts; never reads input/files or evaluates an expression. */
final class FlowFpgtTypes {
 static final class DataType {
  final int kind;final boolean array;
  DataType(int kind){this(kind,false);}DataType(int kind,boolean array){this.kind=kind;this.array=array;}
  boolean numeric(){return !array&&(kind==INTEGER||kind==REAL);}
  String flag(){return kind==INTEGER?"integer":kind==REAL?"real":kind==TEXT?"string":kind==BOOLEAN?"boolean":"none";}
  DataType element(){return new DataType(kind);}
 }
 static final class Symbol {
  final String name;String nodeId;DataType type=new DataType(AUTO);
  boolean parameter,reference,declared,fixed,input,constantPi,isReturn,inputChoice,arrayShape;
  FlowSource.Expression size;Integer literalLength;
  Symbol(String name,String nodeId){this.name=name;this.nodeId=nodeId;}
 }
 static final class Scope {
  final FlowSource.Scope source;DataType returnType;private final Map<String,Symbol> symbols=new LinkedHashMap<>();
  Scope(FlowSource.Scope source){this.source=source;returnType=new DataType(source.returnType,source.returnIsArray);}
  Collection<Symbol> symbols(){return Collections.unmodifiableCollection(symbols.values());}
  Symbol find(String name){return symbols.get(name.toLowerCase(Locale.ROOT));}
  Symbol add(String name,String node){String key=name.toLowerCase(Locale.ROOT);Symbol symbol=symbols.get(key);if(symbol==null){symbol=new Symbol(name,node);symbols.put(key,symbol);}return symbol;}
 }
 static final class Issue {
  final String functionId,nodeId,code,message;
  Issue(Scope scope,Symbol symbol,String code,String message){functionId=scope.source.id;nodeId=symbol.nodeId;this.code=code;this.message=message;}
 }
 final List<Scope> scopes;final List<Issue> issues;private final Map<String,Scope> byId=new LinkedHashMap<>();
 private static final DataType UNKNOWN=new DataType(AUTO),CONFLICT=new DataType(-1),T_INTEGER=new DataType(INTEGER),T_REAL=new DataType(REAL),T_TEXT=new DataType(TEXT),T_BOOL=new DataType(BOOLEAN),T_VOID=new DataType(VOID);
 static String key(String function,String name){return(function==null?"":function)+":"+name.toLowerCase(Locale.ROOT);}
 Scope find(String id){return byId.get(id==null?"":id);}
 private static boolean same(DataType a,DataType b){return a.kind==b.kind&&a.array==b.array;}
 private static DataType union(DataType a,DataType b){if(a.kind==-1||b.kind==-1)return CONFLICT;if(b.kind==AUTO)return a;if(a.kind==AUTO)return b;if(a.array!=b.array)return CONFLICT;if(a.kind==b.kind)return a;if((a.kind==INTEGER||a.kind==REAL)&&(b.kind==INTEGER||b.kind==REAL))return new DataType(REAL,a.array);return CONFLICT;}
 private static boolean merge(Symbol symbol,DataType type){DataType merged=union(symbol.type,type);if(same(merged,symbol.type))return false;symbol.type=merged;return true;}
 FlowFpgtTypes(FlowSource.Program program){this(program,null);}
 FlowFpgtTypes(FlowSource.Program program,Map<String,Integer> choices){
  if(program==null||!program.canGenerate())throw new IllegalArgumentException("Correggi il diagramma prima di generare il codice.");List<Scope> found=new ArrayList<>();for(FlowSource.Scope source:program.scopes){Scope scope=new Scope(source);found.add(scope);byId.put(source.id==null?"":source.id,scope);}scopes=Collections.unmodifiableList(found);
  for(Scope scope:scopes){Set<String> written=new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
   for(FlowSource.Parameter p:scope.source.parameters){Symbol symbol=scope.add(p.name,scope.source.startId);symbol.type=new DataType(p.type,p.isArray);symbol.parameter=symbol.fixed=true;symbol.reference=p.byReference;symbol.arrayShape=p.isArray;written.add(p.name);}
   if(scope.source.id!=null&&scope.source.returnType!=VOID){Symbol symbol=scope.add(scope.source.returnVariable,scope.source.startId);symbol.isReturn=true;symbol.type=scope.returnType;symbol.arrayShape=scope.source.returnIsArray;if(scope.source.returnType!=AUTO){symbol.type=scope.source.returnIsArray?new DataType(AUTO,true):scope.returnType;symbol.fixed=!scope.source.returnIsArray;}written.add(symbol.name);}
   for(FlowSource.Block block:scope.source.blocks){FlowSource.Operation op=block.operation;if(op.declaration!=null){FlowSource.Declaration d=op.declaration;for(String name:d.names){Symbol symbol=scope.add(name,block.id);symbol.declared=symbol.fixed=true;symbol.type=new DataType(d.type,d.isArray());symbol.size=d.size;symbol.arrayShape=d.isArray();if(d.size!=null&&d.size.kind==FlowSource.ExpressionKind.Number&&d.size.number>=0&&d.size.number<=1024&&d.size.number==Math.rint(d.size.number))symbol.literalLength=(int)d.size.number;written.add(name);}collect(scope,d.size,block.id);}
    if(op.input!=null){Symbol symbol=scope.add(op.input.name,block.id);symbol.input=true;if(op.input.index!=null)symbol.arrayShape=true;written.add(symbol.name);collect(scope,op.input.index,block.id);}
    for(FlowSource.Write write:op.writes){Symbol symbol=scope.add(write.target.name,block.id);written.add(symbol.name);if(write.target.index!=null||write.value.kind==FlowSource.ExpressionKind.Vector)symbol.arrayShape=true;if(write.target.index==null&&write.value.kind==FlowSource.ExpressionKind.Vector)symbol.literalLength=write.value.children.size();collect(scope,write.target.index,block.id);collect(scope,write.value,block.id);}collect(scope,op.value,block.id);
   }
   for(Symbol symbol:scope.symbols()){if(!written.contains(symbol.name)&&(symbol.name.equalsIgnoreCase("pi")||symbol.name.equals("π"))){symbol.constantPi=true;symbol.type=T_REAL;}if(!symbol.fixed&&!symbol.parameter&&!symbol.isReturn&&(symbol.name.equalsIgnoreCase("pi")||symbol.name.equals("π")))symbol.type=T_REAL;if(symbol.input&&!symbol.fixed)symbol.inputChoice=true;Integer choice=choices==null?null:choices.get(key(scope.source.id,symbol.name));if(choice!=null&&choice>=INTEGER&&choice<=BOOLEAN&&!symbol.declared&&(!symbol.parameter&&!symbol.isReturn||symbol.type.kind==AUTO)){symbol.type=new DataType(choice,symbol.type.array||symbol.arrayShape);symbol.fixed=true;symbol.inputChoice=false;}}
  }
  int budget=500000;for(int pass=0;pass<512;pass++){boolean changed=false;for(Scope scope:scopes){for(FlowSource.Block block:scope.source.blocks)for(FlowSource.Write write:block.operation.writes){if(--budget<0)throw new IllegalArgumentException("L’analisi dei tipi supera il limite di complessità. Dividi il programma in funzioni più piccole.");Symbol symbol=scope.find(write.target.name);if(!symbol.fixed){DataType type=infer(scope,write.value);if(write.target.index!=null)type=new DataType(type.kind,true);changed|=merge(symbol,type);}}if(scope.source.returnType==AUTO&&scope.source.returnVariable!=null){Symbol symbol=scope.find(scope.source.returnVariable);if(symbol!=null&&!same(scope.returnType,symbol.type)){scope.returnType=symbol.type;changed=true;}}}if(!changed)break;}
  for(Scope scope:scopes)if(scope.source.returnIsArray&&scope.returnType.kind>0){Symbol result=scope.find(scope.source.returnVariable);if(result!=null&&result.type.kind==AUTO)result.type=scope.returnType;}
  List<Issue> diagnostics=new ArrayList<>();for(Scope scope:scopes)for(Symbol symbol:scope.symbols()){if(symbol.inputChoice)diagnostics.add(new Issue(scope,symbol,"input-type","Scegli il tipo da leggere per «"+symbol.name+"», oppure aggiungi una dichiarazione al flowchart."));if(symbol.type.kind<0)diagnostics.add(new Issue(scope,symbol,"changing-type","«"+symbol.name+"» cambia tipo. Il modello richiede un tipo coerente per questa variabile."));if(symbol.type.kind==AUTO&&!symbol.constantPi)diagnostics.add(new Issue(scope,symbol,"unknown-type","Non riesco a determinare il tipo di «"+symbol.name+"». Dichiara il tipo oppure scegli quello della copia generata."));}issues=Collections.unmodifiableList(diagnostics);
 }
 static Boolean shortCircuit(FlowSource.Expression e){if(e.kind!=FlowSource.ExpressionKind.Binary||!e.text.equals("and")&&!e.text.equals("or"))return null;FlowSource.Expression left=e.children.get(0);boolean truth;if(left.kind==FlowSource.ExpressionKind.Boolean)truth=left.bool;else if(left.kind==FlowSource.ExpressionKind.Number)truth=left.number!=0;else return null;return e.text.equals("and")&&!truth||e.text.equals("or")&&truth?Boolean.valueOf(truth):null;}
 private static void collect(Scope scope,FlowSource.Expression expression,String node){if(expression==null)return;ArrayDeque<FlowSource.Expression> pending=new ArrayDeque<>();pending.push(expression);while(!pending.isEmpty()){FlowSource.Expression e=pending.pop();if(shortCircuit(e)!=null)continue;if(e.kind==FlowSource.ExpressionKind.Variable)scope.add(e.text,node);if(e.kind==FlowSource.ExpressionKind.Index&&e.children.get(0).kind==FlowSource.ExpressionKind.Variable)scope.add(e.children.get(0).text,node).arrayShape=true;for(FlowSource.Expression child:e.children)pending.push(child);}}
 DataType infer(Scope scope,FlowSource.Expression e){return infer(scope,e,0);}
 private DataType infer(Scope scope,FlowSource.Expression e,int depth){if(e==null)return T_VOID;if(depth>128)return UNKNOWN;switch(e.kind){
  case Number:return e.number==Math.rint(e.number)&&Math.abs(e.number)<=9007199254740991d?T_INTEGER:T_REAL;
  case Text:return T_TEXT;case Boolean:return T_BOOL;case Variable:Symbol symbol=scope.find(e.text);return symbol==null?UNKNOWN:symbol.type;
  case Vector:DataType item=UNKNOWN;for(FlowSource.Expression c:e.children)item=union(item,infer(scope,c,depth+1));return new DataType(item.kind,true);
  case Index:DataType indexed=infer(scope,e.children.get(0),depth+1);return indexed.array?indexed.element():UNKNOWN;
  case Unary:return e.text.equals("not")?T_BOOL:infer(scope,e.children.get(0),depth+1);
  case Binary:if(Arrays.asList("and","or","=","==","!=","<>","<","<=",">",">=").contains(e.text))return T_BOOL;DataType left=infer(scope,e.children.get(0),depth+1),right=infer(scope,e.children.get(1),depth+1);if(e.text.equals("&")||e.text.equals("+")&&(left.kind==TEXT||right.kind==TEXT))return T_TEXT;if(!left.numeric()||!right.numeric())return UNKNOWN;return e.text.equals("/")||e.text.equals("^")?T_REAL:union(left,right);
  case Call:if(e.functionId!=null){Scope target=find(e.functionId);return target==null?UNKNOWN:target.returnType;}switch(e.text){case "eof":return T_BOOL;case "len":case "size":case "tointeger":case "tocode":case "random":case "sgn":case "int":case "floor":return T_INTEGER;case "tostring":case "tofixed":case "tochar":case "char":return T_TEXT;case "vettore":case "array":return new DataType(e.children.size()>1?infer(scope,e.children.get(1),depth+1).kind:INTEGER,true);case "abs":return infer(scope,e.children.get(0),depth+1);case "min":case "max":case "sum":case "somma":DataType result=UNKNOWN;for(FlowSource.Expression argument:e.children){DataType type=infer(scope,argument,depth+1);result=union(result,type.array?type.element():type);}return result;default:return T_REAL;}
  default:return UNKNOWN;
 }}
}
