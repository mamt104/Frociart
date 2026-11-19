using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Frociart {
 // A data-only refactor: prepare and validate an isolated whole project, retain
 // a frozen base/candidate, then commit only if scope and document still match.
 internal static class FlowVariableRename {
  internal static string RewriteSymbols(string text,IDictionary<string,string> names,bool calls){
   text=text??"";var output=new StringBuilder();int i=0;bool operand=true;
   while(i<text.Length){int start=i;char c=text[i++];
    if(c=='\''||c=='"'){bool escape=false;while(i<text.Length){char next=text[i++];if(escape)escape=false;else if(next=='\\')escape=true;else if(next==c)break;}operand=false;}
    else if(Char.IsDigit(c)||c=='.'&&i<text.Length&&Char.IsDigit(text[i])){while(i<text.Length&&(Char.IsDigit(text[i])||text[i]=='.'))i++;if(i<text.Length&&(text[i]=='e'||text[i]=='E')){i++;if(i<text.Length&&(text[i]=='+'||text[i]=='-'))i++;while(i<text.Length&&Char.IsDigit(text[i]))i++;}operand=false;}
    else if(Char.IsLetter(c)||c=='_'){
     while(i<text.Length&&(Char.IsLetterOrDigit(text[i])||text[i]=='_'))i++;string token=text.Substring(start,i-start),replacement;int next=i;while(next<text.Length&&Char.IsWhiteSpace(text[next]))next++;
     bool wordOperator=token.Equals("mod",StringComparison.OrdinalIgnoreCase)&&!operand;
     if(!wordOperator&&(next<text.Length&&text[next]=='(')==calls&&names.TryGetValue(token,out replacement)){output.Append(replacement);}else output.Append(text,start,i-start);
     string lower=token.ToLowerInvariant();operand=wordOperator||lower=="and"||lower=="or"||lower=="not"||lower=="e"||lower=="o"||lower=="non";continue;
    }else if(!Char.IsWhiteSpace(c))operand=c!=')'&&c!=']';
    output.Append(text,start,i-start);
   }return output.ToString();
  }
  internal sealed class Change {
   internal readonly string NodeId,Field,Before,After;
   internal Change(string node,string field,string before,string after){NodeId=node;Field=field;Before=before;After=after;}
  }
  internal sealed class Plan {
   readonly byte[] original,candidate;internal readonly string FunctionId,Scope,OldName,NewName;
   internal readonly ReadOnlyCollection<Change> Changes;
   internal int Blocks {get{return Changes.Where(c=>c.NodeId!=null).Select(c=>c.NodeId).Distinct().Count();}}
   internal string Summary {get{return Blocks+" "+(Blocks==1?"blocco":"blocchi")+" · "+Changes.Count+" "+(Changes.Count==1?"campo da aggiornare":"campi da aggiornare");}}
   internal Plan(byte[] original,byte[] candidate,string function,string scope,string oldName,string newName,List<Change> changes){this.original=original;this.candidate=candidate;FunctionId=function;Scope=scope;OldName=oldName;NewName=newName;Changes=changes.AsReadOnly();}
   internal Diagram Apply(Diagram current,string function){if(function!=FunctionId||!original.SequenceEqual(DiagramIO.EncodeDocument(current)))throw new FlowException("Il foglio è cambiato. Aggiorna l'anteprima prima di rinominare.");return DiagramIO.DecodeDocument(candidate);}
  }
  internal static string[] Names(Diagram project,string function){
   var owner=Owner(project,function);var graph=FunctionEditing.Graph(project,function);var functions=FunctionModel.Catalog(project);var names=TutorVariables.Names(graph,false,functions,owner);
   foreach(var node in graph.Nodes)try{var block=Compiler.Block(node,functions,owner);if(block.InputVariable!=null)names.Add(block.InputVariable);if(block.InputIndex!=null)block.InputIndex.ReadVariables(names);foreach(var assignment in block.Assignments){names.Add(assignment.Name);if(assignment.Index!=null)assignment.Index.ReadVariables(names);}}catch(FlowException){}
   var display=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);if(owner!=null){foreach(var parameter in owner.Parameters)display[parameter.Name]=parameter.Name;if(owner.ReturnType!=FlowDataType.Void)display[owner.ReturnVariable]=owner.ReturnVariable;}
   foreach(var node in graph.Nodes)foreach(string text in new[]{Code(node)?node.Text:"",node.BreakpointCondition})if(text!=null)foreach(Match match in Regex.Matches(text,ExpressionLanguage.IdentifierWordPattern)){string raw=match.Value;if(ExpressionLanguage.Identifier(raw)&&names.Contains(raw)&&!display.ContainsKey(raw))display.Add(raw,raw);}
   foreach(string name in names)if(ExpressionLanguage.Identifier(name)&&!display.ContainsKey(name))display.Add(name,name);
   return display.Values.OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToArray();
  }
  static FlowFunction Owner(Diagram project,string function){if(project==null)throw new FlowException("Apri un progetto prima di rinominare una variabile.");var owner=FunctionEditing.Find(project,function);if(function!=null&&owner==null)throw new FlowException("La funzione non è più disponibile. Riapri Rinomina variabile.");return owner;}
  static bool Code(FlowNode node){return node.Kind!=NodeKind.Start&&node.Kind!=NodeKind.End&&node.Kind!=NodeKind.Note&&node.Kind!=NodeKind.Pause;}
  internal static Plan Prepare(Diagram project,string function,string oldName,string newName){
   if(!ExpressionLanguage.Identifier(oldName)||!ExpressionLanguage.Identifier(newName))throw new FlowException("Usa un nome da 1 a 64 caratteri: lettera o _ iniziale, poi lettere, cifre o _. Non usare parole riservate.");
   if((oldName.Equals("pi",StringComparison.OrdinalIgnoreCase)||oldName.Equals("π",StringComparison.OrdinalIgnoreCase))&&!Names(project,function).Any(n=>String.Equals(n,oldName,StringComparison.OrdinalIgnoreCase)))throw new FlowException("Pi greco è una costante. Scegli una variabile del foglio.");
   var original=DiagramIO.EncodeDocument(project);var candidate=DiagramIO.DecodeDocument(original);var owner=Owner(candidate,function);var graph=FunctionEditing.Graph(candidate,function);
   if(!String.Equals(oldName,newName,StringComparison.OrdinalIgnoreCase)){
    var probe=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{newName,"\u0001"}};
    bool collision=graph.Nodes.Any(n=>Code(n)&&FlowDeclaration.RewriteNode(n,probe)!=(n.Text??"")||n.BreakpointCondition!=null&&TutorVariables.Rewrite(n.BreakpointCondition,probe)!=n.BreakpointCondition);
    collision|=owner!=null&&(owner.Parameters.Any(p=>String.Equals(p.Name,newName,StringComparison.OrdinalIgnoreCase))||owner.ReturnType!=FlowDataType.Void&&String.Equals(owner.ReturnVariable,newName,StringComparison.OrdinalIgnoreCase));
    if(collision)throw new FlowException("«"+newName+"» è già usato nel foglio. Scegli un nome libero per non unire due variabili.");
   }
   var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{oldName,newName}};var changes=new List<Change>();
   foreach(var node in graph.Nodes){
    if(Code(node)){string next=FlowDeclaration.RewriteNode(node,map);if(next!=(node.Text??"")){changes.Add(new Change(node.Id,"Contenuto",node.Text??"",next));node.Text=next;}}
    if(node.BreakpointCondition!=null){string next=TutorVariables.Rewrite(node.BreakpointCondition,map);if(next!=node.BreakpointCondition){changes.Add(new Change(node.Id,"Condizione di pausa",node.BreakpointCondition,next));node.BreakpointCondition=next;}}
   }
   if(owner!=null){foreach(var parameter in owner.Parameters)if(String.Equals(parameter.Name,oldName,StringComparison.OrdinalIgnoreCase)&&parameter.Name!=newName){changes.Add(new Change(null,"Parametro",parameter.Name,newName));parameter.Name=newName;}
    if(owner.ReturnType!=FlowDataType.Void&&String.Equals(owner.ReturnVariable,oldName,StringComparison.OrdinalIgnoreCase)&&owner.ReturnVariable!=newName){changes.Add(new Change(null,"Risultato della funzione",owner.ReturnVariable,newName));owner.ReturnVariable=newName;}}
   if(changes.Count==0)throw new FlowException("Nessun riferimento da cambiare. Scegli una variabile del foglio e un nome diverso.");
   foreach(var change in changes)if(change.After.Length>DiagramIO.MaxBlockText)throw new FlowException("La rinomina renderebbe un blocco troppo lungo. Scegli un nome più breve.");
   return new Plan(original,DiagramIO.EncodeDocument(candidate),function,owner==null?"Principale":owner.Name,oldName,newName,changes);
  }
 }
}
