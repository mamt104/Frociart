using System;using System.Linq;using System.Collections.Generic;using System.Collections.ObjectModel;
namespace Frociart {
 // Target-neutral facts used by .fpgt flags and casts. Analysis never evaluates a program.
 internal sealed class FlowFpgtTypes {
  internal sealed class DataType {
   internal readonly int Kind;internal readonly bool Array;
   internal DataType(int kind,bool array=false){Kind=kind;Array=array;}
   internal bool Numeric {get{return !Array&&(Kind==1||Kind==2);}}
   internal string Flag {get{return Kind==1?"integer":Kind==2?"real":Kind==3?"string":Kind==4?"boolean":"none";}}
   internal DataType Element {get{return new DataType(Kind);}}
  }
  internal sealed class Symbol {
   internal readonly string Name;internal string NodeId;internal DataType Type=new DataType(0);
   internal bool Parameter,Reference,Declared,Fixed,Input,ConstantPi,Return,InputChoice,ArrayShape;
   internal FlowSource.Expression Size;internal int? LiteralLength;
   internal Symbol(string name,string nodeId){Name=name;NodeId=nodeId;}
  }
  internal sealed class Scope {
   internal readonly FlowSource.Scope Source;internal DataType ReturnType;
   readonly Dictionary<string,Symbol> symbols=new Dictionary<string,Symbol>(StringComparer.OrdinalIgnoreCase);
   internal IEnumerable<Symbol> Symbols {get{return symbols.Values;}}
   internal Scope(FlowSource.Scope source){Source=source;ReturnType=new DataType((int)source.ReturnType,source.ReturnIsArray);}
   internal Symbol Find(string name){Symbol symbol;return symbols.TryGetValue(name,out symbol)?symbol:null;}
   internal Symbol Add(string name,string nodeId){Symbol symbol;if(!symbols.TryGetValue(name,out symbol)){symbol=new Symbol(name,nodeId);symbols.Add(name,symbol);}return symbol;}
  }
  internal sealed class Issue {
   internal readonly string FunctionId,NodeId,Code,Message;
   internal Issue(Scope scope,Symbol symbol,string code,string message){FunctionId=scope.Source.Id;NodeId=symbol.NodeId;Code=code;Message=message;}
  }
  internal readonly ReadOnlyCollection<Scope> Scopes;internal readonly ReadOnlyCollection<Issue> Issues;
  readonly Dictionary<string,Scope> byId=new Dictionary<string,Scope>(StringComparer.Ordinal);
  static readonly DataType Unknown=new DataType(0),Conflict=new DataType(-1),Integer=new DataType(1),Real=new DataType(2),Text=new DataType(3),Boolean=new DataType(4),Void=new DataType(5);
  internal static string Key(string functionId,string name){return(functionId??"")+":"+name.ToLowerInvariant();}
  internal Scope Find(string id){Scope scope;return byId.TryGetValue(id??"",out scope)?scope:null;}
  static bool Same(DataType a,DataType b){return a.Kind==b.Kind&&a.Array==b.Array;}
  static DataType Union(DataType a,DataType b){if(a.Kind==-1||b.Kind==-1)return Conflict;if(b.Kind==0)return a;if(a.Kind==0)return b;if(a.Array!=b.Array)return Conflict;if(a.Kind==b.Kind)return a;if((a.Kind==1||a.Kind==2)&&(b.Kind==1||b.Kind==2))return new DataType(2,a.Array);return Conflict;}
  static bool Merge(Symbol symbol,DataType type){DataType merged=Union(symbol.Type,type);if(Same(merged,symbol.Type))return false;symbol.Type=merged;return true;}
  internal FlowFpgtTypes(FlowSource.Program program,IDictionary<string,FlowDataType> choices=null){
   if(program==null||!program.CanGenerate)throw new ArgumentException("Correggi il diagramma prima di generare il codice.");var scopes=new List<Scope>();foreach(var source in program.Scopes){var scope=new Scope(source);scopes.Add(scope);byId.Add(source.Id??"",scope);}Scopes=scopes.AsReadOnly();
   foreach(var scope in scopes){var written=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(var p in scope.Source.Parameters){var symbol=scope.Add(p.Name,scope.Source.StartId);symbol.Type=new DataType((int)p.Type,p.IsArray);symbol.Parameter=symbol.Fixed=true;symbol.Reference=p.ByReference;symbol.ArrayShape=p.IsArray;written.Add(p.Name);}
    if(scope.Source.Id!=null&&scope.Source.ReturnType!=FlowDataType.Void){var symbol=scope.Add(scope.Source.ReturnVariable,scope.Source.StartId);symbol.Return=true;symbol.Type=scope.ReturnType;symbol.ArrayShape=scope.Source.ReturnIsArray;if(scope.Source.ReturnType!=FlowDataType.Auto){symbol.Type=scope.Source.ReturnIsArray?new DataType(0,true):scope.ReturnType;symbol.Fixed=!scope.Source.ReturnIsArray;}written.Add(symbol.Name);}
    foreach(var block in scope.Source.Blocks){var operation=block.Operation;if(operation.Declaration!=null){var d=operation.Declaration;foreach(var name in d.Names){var symbol=scope.Add(name,block.Id);symbol.Declared=symbol.Fixed=true;symbol.Type=new DataType((int)d.Type,d.IsArray);symbol.Size=d.Size;symbol.ArrayShape=d.IsArray;if(d.Size!=null&&d.Size.Kind==FlowSource.ExpressionKind.Number&&d.Size.Number>=0&&d.Size.Number<=1024&&d.Size.Number==Math.Truncate(d.Size.Number))symbol.LiteralLength=(int)d.Size.Number;written.Add(name);}Collect(scope,d.Size,block.Id);}
     if(operation.Input!=null){var symbol=scope.Add(operation.Input.Name,block.Id);symbol.Input=true;if(operation.Input.Index!=null)symbol.ArrayShape=true;written.Add(symbol.Name);Collect(scope,operation.Input.Index,block.Id);}
     foreach(var write in operation.Writes){var symbol=scope.Add(write.Target.Name,block.Id);written.Add(symbol.Name);if(write.Target.Index!=null||write.Value.Kind==FlowSource.ExpressionKind.Vector)symbol.ArrayShape=true;if(write.Target.Index==null&&write.Value.Kind==FlowSource.ExpressionKind.Vector)symbol.LiteralLength=write.Value.Children.Count;Collect(scope,write.Target.Index,block.Id);Collect(scope,write.Value,block.Id);}Collect(scope,operation.Value,block.Id);
    }
    foreach(var symbol in scope.Symbols){if(!written.Contains(symbol.Name)&&(symbol.Name.Equals("pi",StringComparison.OrdinalIgnoreCase)||symbol.Name=="π")){symbol.ConstantPi=true;symbol.Type=Real;}if(!symbol.Fixed&&!symbol.Parameter&&!symbol.Return&&(symbol.Name.Equals("pi",StringComparison.OrdinalIgnoreCase)||symbol.Name=="π"))symbol.Type=Real;if(symbol.Input&&!symbol.Fixed)symbol.InputChoice=true;FlowDataType choice;if(choices!=null&&choices.TryGetValue(Key(scope.Source.Id,symbol.Name),out choice)&&(int)choice>=1&&(int)choice<=4&&!symbol.Declared&&(!symbol.Parameter&&!symbol.Return||symbol.Type.Kind==0)){symbol.Type=new DataType((int)choice,symbol.Type.Array||symbol.ArrayShape);symbol.Fixed=true;symbol.InputChoice=false;}}
   }
   int budget=500000;for(int pass=0;pass<512;pass++){bool changed=false;foreach(var scope in scopes){foreach(var block in scope.Source.Blocks)foreach(var write in block.Operation.Writes){if(--budget<0)throw new ArgumentException("L’analisi dei tipi supera il limite di complessità. Dividi il programma in funzioni più piccole.");var symbol=scope.Find(write.Target.Name);if(!symbol.Fixed){var type=Infer(scope,write.Value);if(write.Target.Index!=null)type=new DataType(type.Kind,true);changed|=Merge(symbol,type);}}if(scope.Source.ReturnType==FlowDataType.Auto&&scope.Source.ReturnVariable!=null){var symbol=scope.Find(scope.Source.ReturnVariable);if(symbol!=null&&!Same(scope.ReturnType,symbol.Type)){scope.ReturnType=symbol.Type;changed=true;}}}if(!changed)break;}
   foreach(var scope in scopes)if(scope.Source.ReturnIsArray&&scope.ReturnType.Kind>0){var result=scope.Find(scope.Source.ReturnVariable);if(result!=null&&result.Type.Kind==0)result.Type=scope.ReturnType;}
   var issues=new List<Issue>();foreach(var scope in scopes)foreach(var symbol in scope.Symbols){if(symbol.InputChoice)issues.Add(new Issue(scope,symbol,"input-type","Scegli il tipo da leggere per «"+symbol.Name+"», oppure aggiungi una dichiarazione al flowchart."));if(symbol.Type.Kind<0)issues.Add(new Issue(scope,symbol,"changing-type","«"+symbol.Name+"» cambia tipo. Il modello richiede un tipo coerente per questa variabile."));if(symbol.Type.Kind==0&&!symbol.ConstantPi)issues.Add(new Issue(scope,symbol,"unknown-type","Non riesco a determinare il tipo di «"+symbol.Name+"». Dichiara il tipo oppure scegli quello della copia generata."));}Issues=issues.AsReadOnly();
  }
  internal static bool ShortCircuit(FlowSource.Expression e,out bool value){value=false;if(e.Kind!=FlowSource.ExpressionKind.Binary||e.Text!="and"&&e.Text!="or")return false;var left=e.Children[0];bool truth;if(left.Kind==FlowSource.ExpressionKind.Boolean)truth=left.Boolean;else if(left.Kind==FlowSource.ExpressionKind.Number)truth=left.Number!=0;else return false;value=truth;return e.Text=="and"?!truth:truth;}
  static void Collect(Scope scope,FlowSource.Expression expression,string node){if(expression==null)return;var pending=new Stack<FlowSource.Expression>();pending.Push(expression);while(pending.Count>0){var e=pending.Pop();bool ignored;if(ShortCircuit(e,out ignored))continue;if(e.Kind==FlowSource.ExpressionKind.Variable)scope.Add(e.Text,node);if(e.Kind==FlowSource.ExpressionKind.Index&&e.Children[0].Kind==FlowSource.ExpressionKind.Variable)scope.Add(e.Children[0].Text,node).ArrayShape=true;foreach(var child in e.Children)pending.Push(child);}}
  internal DataType Infer(Scope scope,FlowSource.Expression e,int depth=0){
   if(e==null)return Void;if(depth>128)return Unknown;Func<FlowSource.Expression,DataType> child=c=>Infer(scope,c,depth+1);switch(e.Kind){
    case FlowSource.ExpressionKind.Number:return e.Number==Math.Truncate(e.Number)&&Math.Abs(e.Number)<=9007199254740991d?Integer:Real;
    case FlowSource.ExpressionKind.Text:return Text;case FlowSource.ExpressionKind.Boolean:return Boolean;
    case FlowSource.ExpressionKind.Variable:var symbol=scope.Find(e.Text);return symbol==null?Unknown:symbol.Type;
    case FlowSource.ExpressionKind.Vector:DataType item=Unknown;foreach(var c in e.Children)item=Union(item,child(c));return new DataType(item.Kind,true);
    case FlowSource.ExpressionKind.Index:var indexed=child(e.Children[0]);return indexed.Array?indexed.Element:Unknown;
    case FlowSource.ExpressionKind.Unary:return e.Text=="not"?Boolean:child(e.Children[0]);
    case FlowSource.ExpressionKind.Binary:if(new[]{"and","or","=","==","!=","<>","<","<=",">",">="}.Contains(e.Text))return Boolean;var left=child(e.Children[0]);var right=child(e.Children[1]);if(e.Text=="&"||e.Text=="+"&&(left.Kind==3||right.Kind==3))return Text;if(!left.Numeric||!right.Numeric)return Unknown;return e.Text=="/"||e.Text=="^"?Real:Union(left,right);
    case FlowSource.ExpressionKind.Call:if(e.FunctionId!=null){var target=Find(e.FunctionId);return target==null?Unknown:target.ReturnType;}switch(e.Text){case "eof":return Boolean;case "len":case "size":case "tointeger":case "tocode":case "random":case "sgn":case "int":case "floor":return Integer;case "tostring":case "tofixed":case "tochar":case "char":return Text;case "vettore":case "array":return new DataType(e.Children.Count>1?child(e.Children[1]).Kind:1,true);case "abs":return child(e.Children[0]);case "min":case "max":case "sum":case "somma":DataType result=Unknown;foreach(var argument in e.Children){var type=child(argument);result=Union(result,type.Array?type.Element:type);}return result;default:return Real;}
   }return Unknown;
  }
 }
}
