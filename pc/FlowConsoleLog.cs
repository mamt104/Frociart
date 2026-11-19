using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Frociart
{
    /// <summary>Bounded, temporary execution evidence. Never serialized into a project.</summary>
    public sealed class FlowConsoleLog
    {
        public enum EntryKind { Input, Output }
        public sealed class Entry
        {
            public long Sequence { get; private set; }
            public EntryKind Kind { get; private set; }
            public string Text { get; private set; }
            public string Prompt { get; private set; }
            public string Variable { get; private set; }
            public string NodeId { get; private set; }
            public string FunctionId { get; private set; }
            public string FunctionName { get; private set; }
            public int Step { get; private set; }
            public bool Newline { get; private set; }
            public bool TextLimited { get; private set; }
            internal readonly int Cost;
            internal Entry(long sequence, EntryKind kind, string text, string prompt, string variable,
                string nodeId, string functionId, string functionName, int step, bool newline)
            {
                Sequence=sequence; Kind=kind; Prompt=Bound(prompt,1024); Variable=Bound(variable,128);
                NodeId=Bound(nodeId,128); FunctionId=functionId==null?null:Bound(functionId,128);
                FunctionName=String.IsNullOrEmpty(functionName)?"Principale":Bound(functionName,128);
                Step=step; Newline=newline;
                int metadata=Prompt.Length+Variable.Length+NodeId.Length+(FunctionId??"").Length+FunctionName.Length;
                Text=Bound(text,MaxCharacters-metadata); TextLimited=(text??"").Length>Text.Length;
                Cost=metadata+Text.Length;
            }
            public string Label { get { return Kind==EntryKind.Input?"LEGGI · "+Variable:"STAMPA"+(Newline?"":" · senza a capo"); } }
            public string Context { get { return FunctionName+" · passo "+Step; } }
            public override string ToString() { return Label+": "+(Text.Length==0?"(testo vuoto)":Text); }
        }

        public const int MaxEntries=4096, MaxCharacters=524288;
        private readonly Entry[] entries=new Entry[MaxEntries];
        private int start,count,characters;
        private long nextSequence;
        public int Count { get { return count; } }
        public int Discarded { get; private set; }
        public long Revision { get; private set; }
        public Entry this[int index]
        {
            get { if(index<0||index>=count)throw new ArgumentOutOfRangeException("index");return entries[(start+index)%MaxEntries]; }
        }
        public Entry Find(long sequence)
        {
            if(count==0)return null;
            long index=sequence-this[0].Sequence;
            return index<0||index>=count?null:this[(int)index];
        }
        public ReadOnlyCollection<Entry> Snapshot()
        {
            var result=new List<Entry>(count);for(int i=0;i<count;i++)result.Add(this[i]);return result.AsReadOnly();
        }
        internal void Add(EntryKind kind,string text,string prompt,string variable,string nodeId,
            string functionId,string functionName,int step,bool newline)
        {
            var entry=new Entry(++nextSequence,kind,text,prompt,variable,nodeId,functionId,functionName,step,newline);
            while(count>0&&(count==MaxEntries||characters+entry.Cost>MaxCharacters))
            { characters-=entries[start].Cost;entries[start]=null;start=(start+1)%MaxEntries;count--;Discarded++; }
            entries[(start+count)%MaxEntries]=entry;count++;characters+=entry.Cost;Revision++;
        }
        // Clearing presentation never resets variables, files, output or the program counter.
        public void Clear()
        { Array.Clear(entries,0,entries.Length);start=count=characters=Discarded=0;Revision++; }
        public string ToText()
        {
            var text=new StringBuilder();
            if(Discarded>0)text.Append("[Limite console: ").Append(Discarded).Append(" messaggi precedenti rimossi]\n\n");
            for(int i=0;i<count;i++)
            {
                Entry entry=this[i];text.Append(entry.Label).Append(" — ").Append(entry.Context).Append('\n');
                if(entry.Kind==EntryKind.Input&&entry.Prompt.Length>0)text.Append(entry.Prompt).Append('\n');
                if(entry.Kind==EntryKind.Input)text.Append("> ");
                text.Append(entry.Text.Length==0?"(testo vuoto)":entry.Text);
                if(entry.TextLimited)text.Append("\n[Testo limitato dalla memoria della console]");
                text.Append("\n\n");
            }
            return text.ToString();
        }
        private static string Bound(string text,int max)
        {
            text=text??"";if(text.Length<=max)return text;
            if(max>0&&Char.IsHighSurrogate(text[max-1]))max--;
            return text.Substring(0,max);
        }
    }
}
