using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Frociart
{
    // All positions are UTF-16 offsets (WinForms and Android text selections use the same unit).
    // Every physical line, including unmapped runtime helpers, has an entry. One generated line can
    // originate from several blocks, e.g. the initializer/check/increment of a structured FOR.
    internal sealed class FlowSourceText
    {
        internal sealed class Origin
        {
            internal readonly string FunctionId, NodeId;
            internal Origin(string functionId,string nodeId){FunctionId=functionId;NodeId=nodeId;}
            internal bool Matches(string functionId,string nodeId){return FunctionId==functionId&&NodeId==nodeId;}
        }
        internal sealed class Line
        {
            internal readonly int Number, Start, Length;
            internal readonly ReadOnlyCollection<Origin> Origins;
            internal Line(int number,int start,int length,IEnumerable<Origin> origins)
            {Number=number;Start=start;Length=length;Origins=new List<Origin>(origins).AsReadOnly();}
        }
        internal readonly string Language, Text;
        internal readonly ReadOnlyCollection<Line> Lines;
        internal readonly ReadOnlyCollection<string> Notices;
        private readonly Dictionary<Tuple<string,string>,ReadOnlyCollection<Line>> byBlock=new Dictionary<Tuple<string,string>,ReadOnlyCollection<Line>>();
        private static readonly ReadOnlyCollection<Line> EmptyLines=new List<Line>().AsReadOnly();
        private volatile FlowSourceText programView;
        private string PrefixSupportMarker {get{return (Language=="powershell"?"#":"//")+" --- Supporto dei tipi, dei limiti e delle operazioni di Frociart ---";}}
        // Prefix-support languages need complete type definitions before their program. Project the hidden
        // prefix and rebuild visible offsets rather than reusing full-source positions.
        internal FlowSourceText ProgramView(){
            if(Language!="cpp"&&Language!="powershell")return this;if(programView!=null)return programView;
            string support=PrefixSupportMarker;
            string body=(Language=="powershell"?"#":"//")+" --- Programma ---";
            int start=Text.IndexOf("\n"+support+"\n",StringComparison.Ordinal);if(start<0)return programView=this;start++;
            int end=Text.IndexOf("\n"+body+"\n",start,StringComparison.Ordinal);if(end<0)return programView=this;end++;
            var text=new StringBuilder();var visible=new List<Line>();
            foreach(var line in Lines){if(line.Start>=start&&line.Start<end)continue;visible.Add(new Line(visible.Count+1,text.Length,line.Length,line.Origins));text.Append(Text,line.Start,line.Length).Append('\n');}
            return programView=new FlowSourceText(Language,text.ToString(),visible,Notices);
        }
        private FlowSourceText(string language,string text,IEnumerable<Line> lines,IEnumerable<string> notices=null)
        {
            Language=language;Text=text;Lines=new List<Line>(lines).AsReadOnly();
            Notices=(notices??new string[0]).Distinct(StringComparer.Ordinal).OrderBy(n=>n,StringComparer.Ordinal).ToList().AsReadOnly();
            var index=new Dictionary<Tuple<string,string>,List<Line>>();
            foreach(var line in Lines)foreach(var origin in line.Origins){var key=Tuple.Create(origin.FunctionId,origin.NodeId);List<Line> list;if(!index.TryGetValue(key,out list)){list=new List<Line>();index.Add(key,list);}list.Add(line);}
            foreach(var pair in index)byBlock.Add(pair.Key,pair.Value.AsReadOnly());
        }
        // Pseudocode also represents unfinished/disconnected graphs. Validate mappings
        // against the document, without requiring an executable source program.
        internal static FlowSourceText FromPseudocode(Diagram doc,string text,IList<Origin[]> mapping){
            if(doc==null||text==null||text.Length>FlowPseudocode.MaxText||text.IndexOf('\r')>=0)throw new ArgumentException("Pseudocodice collegato non valido.");
            var valid=new HashSet<Tuple<string,string>>();
            foreach(var n in doc.Nodes)valid.Add(Tuple.Create((string)null,n.Id));
            if(doc.Functions!=null)foreach(var f in doc.Functions)foreach(var n in f.Body.Nodes)valid.Add(Tuple.Create(f.Id,n.Id));
            var lines=new List<Line>();int offset=0;
            while(offset<text.Length){int end=text.IndexOf('\n',offset);if(end<0)throw new ArgumentException("Manca la fine riga del pseudocodice.");
                if(mapping==null||lines.Count>=mapping.Count)throw new ArgumentException("Mappa del pseudocodice incompleta.");
                var origins=mapping[lines.Count];if(origins==null||origins.Any(o=>o==null||!valid.Contains(Tuple.Create(o.FunctionId,o.NodeId))))throw new ArgumentException("Blocco del pseudocodice non presente.");
                lines.Add(new Line(lines.Count+1,offset,end-offset,origins));offset=end+1;
            }
            if(mapping==null||mapping.Count!=lines.Count)throw new ArgumentException("Mappa del pseudocodice non valida.");
            return new FlowSourceText("pseudocode",text,lines);
        }
        internal ReadOnlyCollection<Line> ForBlock(string functionId,string nodeId)
        {ReadOnlyCollection<Line> result;return byBlock.TryGetValue(Tuple.Create(functionId,nodeId),out result)?result:EmptyLines;}
        internal FlowSourceText WithNotices(IEnumerable<string> notices){return new FlowSourceText(Language,Text,Lines,notices);}
        // A reading position belongs to the complete source, even when the visible C++
        // projection omits its support prefix. Semantic anchors also survive regeneration.
        internal sealed class Position {
            internal readonly FlowSourceText Owner,View;
            internal readonly Origin Origin;
            internal readonly int Offset,LineNumber,Column,Index;
            internal readonly bool Comment,AtVisibleEnd;
            internal Position(FlowSourceText owner,FlowSourceText view,int offset,int visibleLength){
                Owner=owner;View=view;Offset=Math.Max(0,Math.Min(offset,view.Text.Length));AtVisibleEnd=Offset==visibleLength;
                var line=view.AtOffset(Math.Min(Offset,Math.Max(0,view.Text.Length-1)));
                if(line==null)return;LineNumber=line.Number;Column=Math.Max(0,Offset-line.Start);Origin=line.Origins.FirstOrDefault();Comment=view.IsComment(line);
                if(Origin!=null)Index=view.ForBlock(Origin.FunctionId,Origin.NodeId).Where(l=>view.IsComment(l)==Comment).TakeWhile(l=>!Object.ReferenceEquals(l,line)).Count();
            }
        }
        internal Position Capture(FlowSourceText view,int offset,int visibleLength){return view==null||view.Text.Length==0||visibleLength<=0?null:new Position(this,view,offset,visibleLength);}
        internal int? Restore(FlowSourceText view,Position position,int visibleLength){
            if(view==null||position==null)return null;visibleLength=Math.Max(0,Math.Min(visibleLength,view.Text.Length));
            if(Object.ReferenceEquals(position.Owner,this)){
                int offset=position.Offset;var compact=ProgramView();int removed=Text.Length-compact.Text.Length;
                if(removed>0){int start=Text.IndexOf("\n"+PrefixSupportMarker+"\n",StringComparison.Ordinal)+1;
                    if(position.View.Text.Length<Text.Length&&offset>=start)offset+=removed;
                    if(view.Text.Length<Text.Length&&offset>=start)offset=offset<start+removed?start:offset-removed;
                }
                return Math.Max(0,Math.Min(offset,visibleLength));
            }
            if(position.AtVisibleEnd)return visibleLength;
            Line chosen=null;
            if(position.Origin!=null){
                var all=view.ForBlock(position.Origin.FunctionId,position.Origin.NodeId);var candidates=all.Where(l=>view.IsComment(l)==position.Comment).ToList();if(candidates.Count==0)candidates=all.ToList();if(candidates.Count==0)return null;
                var old=position.View.Lines.FirstOrDefault(l=>l.Number==position.LineNumber);string text=old==null?null:position.View.Text.Substring(old.Start,old.Length);
                chosen=candidates.Select((line,index)=>new{line,index}).Where(item=>view.Text.Substring(item.line.Start,item.line.Length)==text).OrderBy(item=>Math.Abs(item.index-position.Index)).Select(item=>item.line).FirstOrDefault()??candidates[Math.Min(position.Index,candidates.Count-1)];
            }else if(Language==position.Owner.Language&&view.Lines.Count>0)chosen=view.Lines[Math.Min(Math.Max(0,position.LineNumber-1),view.Lines.Count-1)];
            return chosen==null?(int?)null:Math.Min(visibleLength,chosen.Start+Math.Min(position.Column,chosen.Length));
        }
        internal Line OpeningLine(string function,string node){
            var exact=node==null?EmptyLines:ForBlock(function,node);var result=exact.FirstOrDefault(l=>!IsComment(l))??exact.FirstOrDefault();if(result!=null)return result;
            var scoped=Lines.Where(l=>l.Origins.Any(o=>o.FunctionId==function)).ToList();result=scoped.FirstOrDefault(l=>!IsComment(l))??scoped.FirstOrDefault();if(result!=null)return result;
            return Lines.FirstOrDefault(l=>l.Origins.Count>0&&!IsComment(l))??Lines.FirstOrDefault(l=>l.Origins.Count>0)??Lines.FirstOrDefault();
        }
        private bool IsComment(Line line){string value=Text.Substring(line.Start,line.Length).TrimStart();return value.Length==0||value.StartsWith("//",StringComparison.Ordinal)||value.StartsWith("#",StringComparison.Ordinal)||value.StartsWith("'",StringComparison.Ordinal)||value.StartsWith("/*",StringComparison.Ordinal)||value.StartsWith("*",StringComparison.Ordinal)||value.StartsWith("--",StringComparison.Ordinal);}
        internal Line AtOffset(int offset)
        {
            if(offset<0||offset>=Text.Length)return null;
            int lo=0,hi=Lines.Count-1;
            while(lo<=hi){int mid=lo+(hi-lo)/2;var line=Lines[mid];
                if(offset<line.Start)hi=mid-1;
                else if(offset>line.Start+line.Length)lo=mid+1;
                else return line; // The terminating LF belongs to its preceding line.
            }
            return null;
        }
        internal sealed class Builder
        {
            internal const int MaxCharacters=2097152, MaxLines=50000;
            private readonly FlowSource.Program program;
            private readonly string language;
            private readonly StringBuilder text=new StringBuilder();
            private readonly List<Line> lines=new List<Line>();
            internal Builder(FlowSource.Program program,string language)
            {
                if(program==null||!program.CanGenerate)throw new ArgumentException("Correggi il diagramma prima di generare il codice.");
                if(String.IsNullOrWhiteSpace(language)||language.Length>64)throw new ArgumentException("Linguaggio di destinazione non valido.");
                this.program=program;this.language=language;
            }
            internal void AddLine(string code,int indent,params Origin[] origins)
            {
                if(code==null||code.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("Aggiungi una sola riga di codice per volta.");
                if(indent<0||indent>128)throw new ArgumentOutOfRangeException("indent");
                if(lines.Count>=MaxLines||code.Length>MaxCharacters-text.Length-indent*4-1)
                    throw new FlowException("Il codice generato supera il limite di dimensione.");
                var unique=new List<Origin>();
                foreach(var origin in origins??new Origin[0])
                {
                    var scope=origin==null?null:program.Scopes.FirstOrDefault(s=>s.Id==origin.FunctionId);
                    if(scope==null||(scope.Find(origin.NodeId)==null&&!scope.Notes.Any(n=>n.Id==origin.NodeId)))throw new ArgumentException("La riga fa riferimento a un blocco non presente nel programma.");
                    if(!unique.Any(o=>o.Matches(origin.FunctionId,origin.NodeId)))unique.Add(origin);
                }
                var line=new Line(lines.Count+1,text.Length,indent*4+code.Length,unique);
                text.Append(' ',indent*4).Append(code).Append('\n');lines.Add(line);
            }
            internal FlowSourceText Build(){return new FlowSourceText(language,text.ToString(),lines);}
        }
    }
}
