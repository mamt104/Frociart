package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Data-only refactor, isolated candidate and stale-document guard. No runtime or I/O. */
final class FlowVariableRename {
 static boolean sameSymbol(String first,String second){return FlowFunctions.key(first).equals(FlowFunctions.key(second));}
 static TreeMap<String,String> symbolMap(){return new TreeMap<>((first,second)->FlowFunctions.key(first).compareTo(FlowFunctions.key(second)));}
 static String rewriteSymbols(String text,Map<String,String> names,boolean calls){
  if(text==null)text="";StringBuilder out=new StringBuilder();int i=0;boolean operand=true;
  while(i<text.length()){int start=i;char c=text.charAt(i++);
   if(c=='\''||c=='"'){boolean escape=false;while(i<text.length()){char next=text.charAt(i++);if(escape)escape=false;else if(next=='\\')escape=true;else if(next==c)break;}operand=false;}
   else if(Character.isDigit(c)||c=='.'&&i<text.length()&&Character.isDigit(text.charAt(i))){while(i<text.length()&&(Character.isDigit(text.charAt(i))||text.charAt(i)=='.'))i++;if(i<text.length()&&(text.charAt(i)=='e'||text.charAt(i)=='E')){i++;if(i<text.length()&&(text.charAt(i)=='+'||text.charAt(i)=='-'))i++;while(i<text.length()&&Character.isDigit(text.charAt(i)))i++;}operand=false;}
   else if(Character.isLetter(c)||c=='_'){
    while(i<text.length()&&(Character.isLetterOrDigit(text.charAt(i))||text.charAt(i)=='_'))i++;String token=text.substring(start,i),replacement=null;for(Map.Entry<String,String> entry:names.entrySet())if(sameSymbol(entry.getKey(),token)){replacement=entry.getValue();break;}int next=i;while(next<text.length()&&Character.isWhitespace(text.charAt(next)))next++;
    boolean wordOperator=token.equalsIgnoreCase("mod")&&!operand;
    if(!wordOperator&&(next<text.length()&&text.charAt(next)=='(')==calls&&replacement!=null)out.append(replacement);else out.append(text,start,i);
    String lower=token.toLowerCase(Locale.ROOT);operand=wordOperator||lower.equals("and")||lower.equals("or")||lower.equals("not")||lower.equals("e")||lower.equals("o")||lower.equals("non");continue;
   }else if(!Character.isWhitespace(c))operand=c!=')'&&c!=']';
   out.append(text,start,i);
  }return out.toString();
 }
 static final class Change {
  final String nodeId,field,before,after;
  Change(String node,String field,String before,String after){nodeId=node;this.field=field;this.before=before;this.after=after;}
 }
 static final class Plan {
  private final String original,candidate;final String functionId,scope,oldName,newName;final List<Change> changes;
  Plan(String original,String candidate,String function,String scope,String oldName,String newName,List<Change> changes){this.original=original;this.candidate=candidate;functionId=function;this.scope=scope;this.oldName=oldName;this.newName=newName;this.changes=Collections.unmodifiableList(new ArrayList<>(changes));}
  int blocks(){Set<String> ids=new HashSet<>();for(Change change:changes)if(change.nodeId!=null)ids.add(change.nodeId);return ids.size();}
  String summary(){int count=blocks();return count+" "+(count==1?"blocco":"blocchi")+" · "+changes.size()+" "+(changes.size()==1?"campo da aggiornare":"campi da aggiornare");}
  Document apply(Document current,String function){if(!Objects.equals(function,functionId)||!original.equals(FlowCodec.encode(current)))throw error("Il foglio è cambiato. Aggiorna l'anteprima prima di rinominare.");return FlowCodec.decode(candidate);}
 }
 static List<String> names(Document project,String function){Function owner=owner(project,function);Document graph=FlowFunctionEditing.graph(project,function);Set<String> names=new TreeSet<>((first,second)->FlowFunctions.key(first).compareTo(FlowFunctions.key(second)));
  if(owner!=null){for(Parameter parameter:owner.parameters)names.add(parameter.name);if(owner.returnType!=VOID)names.add(owner.returnVariable);}
  Map<String,Function> functions=FlowFunctions.catalog(project);
  for(Node node:graph.nodes)try{FlowEngine.Block block=FlowEngine.compile(node,functions,owner);names.addAll(block.definitions());if(block.expression!=null)block.expression.reads(names);if(block.declarationSize!=null)block.declarationSize.reads(names);if(block.inputVariable!=null)names.add(block.inputVariable);if(block.inputIndex!=null)block.inputIndex.reads(names);for(FlowEngine.Assignment assignment:block.assignments){names.add(assignment.name);assignment.expression.reads(names);if(assignment.index!=null)assignment.index.reads(names);}}catch(IllegalArgumentException ignored){}
  Map<String,String> display=new LinkedHashMap<>();if(owner!=null){for(Parameter parameter:owner.parameters)display.put(FlowFunctions.key(parameter.name),parameter.name);if(owner.returnType!=VOID)display.put(FlowFunctions.key(owner.returnVariable),owner.returnVariable);}
  java.util.regex.Pattern words=java.util.regex.Pattern.compile("[\\p{L}_][\\p{L}\\p{Nd}_]*");
  for(Node node:graph.nodes)for(String text:new String[]{code(node)?node.text:"",node.breakpointCondition})if(text!=null){java.util.regex.Matcher matcher=words.matcher(text);while(matcher.find()){String raw=matcher.group();if(FlowEngine.identifier(raw)&&names.contains(raw))display.putIfAbsent(FlowFunctions.key(raw),raw);}}
  for(String name:names)if(FlowEngine.identifier(name))display.putIfAbsent(FlowFunctions.key(name),name);
  ArrayList<String> result=new ArrayList<>(display.values());result.sort((first,second)->FlowFunctions.key(first).compareTo(FlowFunctions.key(second)));return result;
 }
 private static Function owner(Document project,String function){if(project==null)throw error("Apri un progetto prima di rinominare una variabile.");Function owner=FlowFunctionEditing.find(project,function);if(function!=null&&owner==null)throw error("La funzione non è più disponibile. Riapri Rinomina variabile.");return owner;}
 private static boolean code(Node node){return node.kind!=START&&node.kind!=END&&node.kind!=NOTE&&node.kind!=PAUSE;}
 static Plan prepare(Document project,String function,String oldName,String newName){
  if(!FlowEngine.identifier(oldName)||!FlowEngine.identifier(newName))throw error("Usa un nome da 1 a 64 caratteri: lettera o _ iniziale, poi lettere, cifre o _. Non usare parole riservate.");
  if((sameSymbol(oldName,"pi")||sameSymbol(oldName,"π"))&&!names(project,function).stream().anyMatch(n->sameSymbol(n,oldName)))throw error("Pi greco è una costante. Scegli una variabile del foglio.");
  String original=FlowCodec.encode(project);Document candidate=FlowCodec.decode(original);Function owner=owner(candidate,function);Document graph=FlowFunctionEditing.graph(candidate,function);
  if(!sameSymbol(oldName,newName)){
   Map<String,String> probe=symbolMap();probe.put(newName,"\u0001");boolean collision=false;
   for(Node node:graph.nodes)if(code(node)&&!FlowDeclaration.rewriteNode(node,node.text,probe).equals(node.text==null?"":node.text)||node.breakpointCondition!=null&&!FlowFunctionEditing.rewrite(node.breakpointCondition,probe,false).equals(node.breakpointCondition))collision=true;
   if(owner!=null){for(Parameter parameter:owner.parameters)if(sameSymbol(parameter.name,newName))collision=true;if(owner.returnType!=VOID&&sameSymbol(owner.returnVariable,newName))collision=true;}
   if(collision)throw error("«"+newName+"» è già usato nel foglio. Scegli un nome libero per non unire due variabili.");
  }
  Map<String,String> map=symbolMap();map.put(oldName,newName);ArrayList<Change> changes=new ArrayList<>();
  for(Node node:graph.nodes){
   if(code(node)){String next=FlowDeclaration.rewriteNode(node,node.text,map);if(!next.equals(node.text==null?"":node.text)){changes.add(new Change(node.id,"Contenuto",node.text==null?"":node.text,next));node.text=next;}}
   if(node.breakpointCondition!=null){String next=FlowFunctionEditing.rewrite(node.breakpointCondition,map,false);if(!next.equals(node.breakpointCondition)){changes.add(new Change(node.id,"Condizione di pausa",node.breakpointCondition,next));node.breakpointCondition=next;}}
  }
  if(owner!=null){for(Parameter parameter:owner.parameters)if(sameSymbol(parameter.name,oldName)&&!parameter.name.equals(newName)){changes.add(new Change(null,"Parametro",parameter.name,newName));parameter.name=newName;}
   if(owner.returnType!=VOID&&sameSymbol(owner.returnVariable,oldName)&&!owner.returnVariable.equals(newName)){changes.add(new Change(null,"Risultato della funzione",owner.returnVariable,newName));owner.returnVariable=newName;}}
  if(changes.isEmpty())throw error("Nessun riferimento da cambiare. Scegli una variabile del foglio e un nome diverso.");
  for(Change change:changes)if(change.after.length()>8192)throw error("La rinomina renderebbe un blocco troppo lungo. Scegli un nome più breve.");
  return new Plan(original,FlowCodec.encode(candidate),function,owner==null?"Principale":owner.name,oldName,newName,changes);
 }
 private static IllegalArgumentException error(String message){return new IllegalArgumentException(message);}
}
