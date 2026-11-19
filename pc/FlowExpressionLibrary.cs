using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Frociart {
    // Passive editor snapshot. No interpreter, filesystem, runtime values or writes.
    internal static class FlowExpressionLibrary {
        internal sealed class Catalog {
            internal readonly ReadOnlyCollection<string[]> Entries;
            internal readonly string ScopeName;
            internal readonly bool TargetOnly, CallsOnly;
            internal Catalog(IEnumerable<string[]> entries,string scope,bool targets,bool calls){Entries=entries.ToList().AsReadOnly();ScopeName=scope;TargetOnly=targets;CallsOnly=calls;}
        }
        private sealed class Symbol {
            internal string Name,Role="Variabile del foglio";
            internal FlowDataType Type;
            internal bool Array;
        }
        internal static Catalog Build(Diagram project,string functionId=null,string nodeId=null,string draft=null,bool callsOnly=false,bool callArguments=false) {
            var symbols=new Dictionary<string,Symbol>(StringComparer.OrdinalIgnoreCase);
            var rows=new List<string[]>();string scope="Principale";bool targetOnly=false;
            if(project!=null){
                var snapshot=DiagramIO.Clone(project);var owner=FunctionEditing.Find(snapshot,functionId);
                if(functionId!=null&&owner==null)throw new FlowException("La funzione non è più disponibile. Riapri il blocco.");
                var graph=FunctionEditing.Graph(snapshot,functionId);var edited=graph.Nodes.FirstOrDefault(n=>n.Id==nodeId);
                if(nodeId!=null&&edited==null)throw new FlowException("Il blocco non è più disponibile. Riapri l’editor.");
                if(edited!=null&&draft!=null){if(draft.Length>DiagramIO.MaxBlockText)throw new FlowException("Il testo del blocco è troppo lungo.");IoInstruction.Apply(edited,draft);}
                targetOnly=edited!=null&&(edited.Kind==NodeKind.Input||edited.Kind==NodeKind.ReadFile);
                callsOnly=(callsOnly||edited!=null&&edited.Kind==NodeKind.Call)&&!callArguments;
                scope=owner==null?"Principale":owner.Name;
                var functions=FunctionModel.Catalog(snapshot);
                if(owner!=null){foreach(var p in owner.Parameters)symbols[p.Name]=new Symbol{Name=p.Name,Type=p.Type,Array=p.IsArray,Role=p.ByReference?"Parametro per riferimento":"Parametro"};
                    if(owner.ReturnType!=FlowDataType.Void){Symbol result;if(!symbols.TryGetValue(owner.ReturnVariable,out result)){result=new Symbol{Name=owner.ReturnVariable,Type=owner.ReturnType,Array=owner.ReturnIsArray};symbols.Add(result.Name,result);}result.Role=result.Role.StartsWith("Parametro",StringComparison.Ordinal)?result.Role+" · risultato":"Risultato della funzione";}}
                foreach(var node in graph.Nodes){
                    try{
                        var block=Compiler.Block(node,functions,owner);
                        foreach(string name in block.Definitions())if(!symbols.ContainsKey(name))symbols.Add(name,new Symbol{Name=OriginalName(node.Text,name)});
                        if(block.Declaration!=null)foreach(string name in block.Declaration.Names){Symbol symbol;if(!symbols.TryGetValue(name,out symbol)){symbol=new Symbol{Name=name};symbols.Add(name,symbol);}if(symbol.Type==FlowDataType.Auto&&symbol.Role=="Variabile del foglio"){symbol.Name=name;symbol.Type=block.Declaration.Type;symbol.Array=block.Declaration.IsArray;symbol.Role="Dichiarata nel foglio";}}
                    }catch(FlowException){/* An unfinished block does not hide other valid definitions. */}
                }
                if(!callsOnly)foreach(var symbol in symbols.Values.OrderBy(s=>s.Name,StringComparer.OrdinalIgnoreCase)){
                    string type=TypeName(symbol.Type)+(symbol.Array?" · vettore":"");
                    string description=symbol.Role+" · "+scope+"\n"+type+". Il valore si controlla durante l’esecuzione.";
                    if(!targetOnly||!symbol.Array)rows.Add(new[]{"Variabili",symbol.Name,description,symbol.Name,""});
                    if(symbol.Array)rows.Add(new[]{"Variabili",symbol.Name+"[indice]",description+"\nSostituisci 0 con l’indice desiderato. Il primo elemento ha indice 0.",symbol.Name+"[0]",""});
                }
                if(!targetOnly)foreach(var f in (snapshot.Functions??new List<FlowFunction>()).OrderBy(f=>f.Name,StringComparer.OrdinalIgnoreCase)){
                    if(!callsOnly&&f.ReturnType==FlowDataType.Void)continue;
                    string call=f.Name+"("+String.Join(", ",f.Parameters.Select(p=>p.Name))+")";
                    rows.Add(new[]{"Le tue funzioni",f.Name,FlowPseudocode.Signature(f)+"\nSostituisci i nomi dei parametri con i dati da passare.",call,""});
                }
            }
            if(!targetOnly&&!callsOnly)rows.AddRange(FunctionLibraryData.Entries);
            return new Catalog(rows,scope,targetOnly,callsOnly);
        }
        private static string OriginalName(string text,string name){
            text=text??"";int i=0;while(i<text.Length){char c=text[i++];
                if(c=='\''||c=='"'){bool escape=false;while(i<text.Length){char next=text[i++];if(escape)escape=false;else if(next=='\\')escape=true;else if(next==c)break;}}
                else if(c>='0'&&c<='9'||c=='.'&&i<text.Length&&Char.IsDigit(text[i])){while(i<text.Length&&(Char.IsDigit(text[i])||text[i]=='.'))i++;if(i<text.Length&&(text[i]=='e'||text[i]=='E')){i++;if(i<text.Length&&(text[i]=='+'||text[i]=='-'))i++;while(i<text.Length&&Char.IsDigit(text[i]))i++;}}
                else if(c=='_'||Char.IsLetter(c)){int start=i-1;while(i<text.Length&&Word(text[i]))i++;string token=text.Substring(start,i-start);if(String.Equals(token,name,StringComparison.OrdinalIgnoreCase))return token;}
            }return name;
        }
        private static string TypeName(FlowDataType type){switch(type){case FlowDataType.Integer:return "Intero";case FlowDataType.Real:return "Reale";case FlowDataType.Text:return "Testo";case FlowDataType.Boolean:return "Logico";default:return "Tipo automatico";}}
        internal static List<string[]> Filter(Catalog catalog,string query,int group=0){
            string[] words=(query??"").Trim().Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            return catalog.Entries.Where(row=>(group==0||group==1&&row[0]=="Variabili"||group==2&&row[0]!="Variabili")&&words.All(word=>String.Join(" ",row).IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
        }
        internal sealed class Selection {
            internal readonly int Start,Length;internal readonly string Query;
            internal Selection(int start,int length,string query){Start=start;Length=length;Query=query;}
        }
        internal static Selection CompleteAt(string text,int start,int length){
            if(text==null||start<0||length<0||start>text.Length-length)throw new ArgumentOutOfRangeException("start");
            if(length>0){string selected=text.Substring(start,length);return new Selection(start,length,Regex.IsMatch(selected,@"\A"+ExpressionLanguage.IdentifierPattern+@"\z")?selected:"");}
            char quote='\0';bool escaped=false;
            for(int i=0;i<start;i++){char c=text[i];if(quote!='\0'){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote='\0';}else if(c=='\''||c=='"')quote=c;}
            if(quote!='\0')return new Selection(start,0,"");
            int left=start,right=start;while(left>0&&Word(text[left-1]))left--;while(right<text.Length&&Word(text[right]))right++;
            if(left==start)return new Selection(start,0,"");
            string word=text.Substring(left,right-left);if(!Regex.IsMatch(word,@"\A"+ExpressionLanguage.IdentifierPattern+@"\z"))return new Selection(start,0,"");
            return new Selection(left,right-left,text.Substring(left,start-left));
        }
        internal static bool ArgumentsAt(string text,int start,int length){
            if(text==null||start<0||length<0||start>text.Length-length)throw new ArgumentOutOfRangeException("start");
            int depth=0,atStart=0;char quote='\0';bool escaped=false;
            for(int i=0;i<start+length;i++){if(i==start)atStart=depth;char c=text[i];if(quote!='\0'){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote='\0';}else if(c=='\''||c=='"')quote=c;else if(c=='(')depth++;else if(c==')')depth=Math.Max(0,depth-1);}
            if(length==0)atStart=depth;return atStart>0&&depth>0;
        }
        private static bool Word(char c){return Char.IsLetterOrDigit(c)||c=='_';}
    }
}
