using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Frociart {
    // Transfer definitions, never execute source projects. All changes are staged
    // on independent documents, with dependency closure and one simultaneous rename.
    internal static class FunctionTransfer {
        internal sealed class Plan {
            internal readonly Diagram Project;
            internal readonly string SelectedId;
            internal readonly string BaseDocument;
            internal readonly ReadOnlyCollection<string> Added;
            internal readonly ReadOnlyCollection<string> Renamed;
            internal Plan(Diagram project,string selected,IEnumerable<string> added,IEnumerable<string> renamed,string basis){Project=project;SelectedId=selected;Added=added.ToList().AsReadOnly();Renamed=renamed.ToList().AsReadOnly();BaseDocument=basis;}
            internal string Description {get{return "Funzioni incluse: "+String.Join(", ",Added)+"."+(Renamed.Count==0?"":"\n\nNomi già occupati:\n"+String.Join("\n",Renamed))+"\n\nI diagrammi già presenti restano intatti. Puoi annullare l'importazione con un solo Annulla.";}}
        }
        private static IEnumerable<FlowSource.Expression> Roots(FlowSource.Operation op){
            yield return op.Value;if(op.Input!=null)yield return op.Input.Index;
            if(op.Declaration!=null)yield return op.Declaration.Size;
            foreach(var w in op.Writes){yield return w.Value;yield return w.Target.Index;}
        }
        private static List<FlowFunction> Closure(Diagram source,string id){
            DiagramIO.CheckDocument(source);var selected=FunctionEditing.Find(source,id);
            if(selected==null)throw new InvalidDataException("Scegli una funzione presente nel progetto di origine.");
            var catalog=FunctionModel.Catalog(source);var byId=catalog.Values.ToDictionary(f=>f.Id,StringComparer.Ordinal);
            var required=new HashSet<string>(StringComparer.Ordinal);var pending=new Queue<FlowFunction>();pending.Enqueue(selected);
            while(pending.Count>0){var f=pending.Dequeue();if(!required.Add(f.Id))continue;
                foreach(var node in f.Body.Nodes){
                    FlowSource.Operation op;
                    try{op=FlowSource.CompileOperation(node,catalog,f);}catch(FlowException e){throw new InvalidDataException("Correggi «"+f.Name+"» prima di importarla: "+e.Message,e);}
                    var expressions=new Stack<FlowSource.Expression>(Roots(op).Where(x=>x!=null));
                    while(expressions.Count>0){var expression=expressions.Pop();if(expression.FunctionId!=null)pending.Enqueue(byId[expression.FunctionId]);foreach(var child in expression.Children)expressions.Push(child);}
                }
            }
            return source.Functions.Where(f=>required.Contains(f.Id)).ToList();
        }
        internal static Diagram Export(Diagram source,string id){
            var required=Closure(source,id);var library=DiagramIO.CreateBlank();library.Title="Funzioni · "+FunctionEditing.Find(source,id).Name;
            library.Functions=required.Select(f=>f.Copy()).ToList();library.VariablePolicy=source.VariablePolicy;FlowAnnotations.Promote(library);
            // Main is deliberately blank; unrelated incomplete diagrams are not exported.
            DiagramIO.CheckDocument(library);
            var problem=Validator.Validate(library).FirstOrDefault(i=>i.Severity==Severity.Error);
            if(problem!=null)throw new InvalidDataException("La funzione non è pronta: "+problem.Message);
            DiagramIO.EncodeDocument(library);return library;
        }
        internal static Plan Import(Diagram target,Diagram source,string id){
            DiagramIO.CheckDocument(target);var library=Export(source,id);var candidate=DiagramIO.Clone(target);
            var originalNames=new HashSet<string>((target.Functions??new List<FlowFunction>()).Select(f=>f.Name),StringComparer.OrdinalIgnoreCase);
            var used=new HashSet<string>(originalNames,StringComparer.OrdinalIgnoreCase);foreach(var f in library.Functions)used.Add(f.Name);
            var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var f in library.Functions){string name=f.Name;if(originalNames.Contains(name)){
                for(int i=2;;i++){string suffix="_"+i;string next=name.Substring(0,Math.Min(name.Length,64-suffix.Length))+suffix;if(used.Add(next)){name=next;break;}}
            }names.Add(f.Name,name);}
            if(candidate.Functions==null)candidate.Functions=new List<FlowFunction>();candidate.Version=2;
            string selected=null;var added=new List<string>();var renamed=new List<string>();
            foreach(var f in library.Functions){var copy=f.Copy();copy.Id=Guid.NewGuid().ToString("N");copy.Name=names[f.Name];copy.Body.Title=copy.Name;
                if(f.Id==id)selected=copy.Id;if(f.Name!=copy.Name)renamed.Add(f.Name+" → "+copy.Name);added.Add(copy.Name);
                var ids=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var node in copy.Body.Nodes){string next=Guid.NewGuid().ToString("N");ids.Add(node.Id,next);node.Id=next;if(node.Kind!=NodeKind.Start&&node.Kind!=NodeKind.End&&node.Kind!=NodeKind.Note)node.Text=RewriteCalls(node.Text,names);if(node.BreakpointCondition!=null)node.BreakpointCondition=RewriteCalls(node.BreakpointCondition,names);}
                foreach(var edge in copy.Body.Edges){edge.Id=Guid.NewGuid().ToString("N");edge.From=ids[edge.From];edge.To=ids[edge.To];}
                candidate.Functions.Add(copy);
            }
            FlowAnnotations.Promote(candidate);DiagramIO.CheckDocument(candidate);DiagramIO.EncodeDocument(candidate);
            return new Plan(candidate,selected,added,renamed,Convert.ToBase64String(DiagramIO.EncodeDocument(target)));
        }
        internal static string RewriteCalls(string text,IDictionary<string,string> names){
            text=text??"";var result=new StringBuilder();int i=0;
            while(i<text.Length){int start=i;char c=text[i++];
                if(c=='\''||c=='"'){bool escape=false;while(i<text.Length){char n=text[i++];if(escape)escape=false;else if(n=='\\')escape=true;else if(n==c)break;}}
                else if(Char.IsDigit(c)||c=='.'&&i<text.Length&&Char.IsDigit(text[i])){
                    while(i<text.Length&&(Char.IsDigit(text[i])||text[i]=='.'))i++;
                    if(i<text.Length&&(text[i]=='e'||text[i]=='E')){i++;if(i<text.Length&&(text[i]=='+'||text[i]=='-'))i++;while(i<text.Length&&Char.IsDigit(text[i]))i++;}
                }else if(Char.IsLetter(c)||c=='_'){
                    while(i<text.Length&&(Char.IsLetterOrDigit(text[i])||text[i]=='_'))i++;string name=text.Substring(start,i-start);int after=i;while(after<text.Length&&Char.IsWhiteSpace(text[after]))after++;
                    string replacement;if(after<text.Length&&text[after]=='('&&names.TryGetValue(name,out replacement)){result.Append(replacement);continue;}
                }result.Append(text,start,i-start);
            }return result.ToString();
        }
    }
}
