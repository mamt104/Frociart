using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Frociart
{
    // Immutable, transient text index. No model references, writes, regex or file access.
    internal sealed class FlowSearch
    {
        internal const int MaxQuery = 256, MaxResults = 200;
        internal sealed class Entry
        {
            internal readonly string FunctionId, NodeId, Graph, Kind, Field, Text;
            internal Entry(string function, string node, string graph, string kind, string field, string text)
            { FunctionId = function; NodeId = node; Graph = graph; Kind = kind; Field = field; Text = text; }
        }
        internal sealed class Hit
        {
            internal readonly Entry Origin;
            internal readonly int Start, Length, SnippetStart;
            internal readonly string Snippet;
            internal string Context { get { return Origin.Graph + " · " + Origin.Kind + " · " + Origin.Field; } }
            internal Hit(Entry entry, int start, int length)
            {
                Origin = entry; Start = start; Length = length;
                int from = Math.Max(0, start - 36), to = Math.Min(entry.Text.Length, start + length + 72);
                if (from > 0 && Char.IsLowSurrogate(entry.Text[from]) && Char.IsHighSurrogate(entry.Text[from - 1])) from--;
                if (to < entry.Text.Length && to > 0 && Char.IsHighSurrogate(entry.Text[to - 1])) to++;
                string left = from > 0 ? "…" : "";
                Snippet = left + new String(entry.Text.Substring(from, to - from).Select(c => Char.IsControl(c) ? ' ' : c).ToArray()) + (to < entry.Text.Length ? "…" : "");
                SnippetStart = left.Length + start - from;
            }
            public override string ToString() { return Context + ": " + Snippet; }
        }
        internal sealed class Results
        {
            internal readonly ReadOnlyCollection<Hit> Hits;
            internal readonly bool Limited;
            internal Results(List<Hit> hits, bool limited) { Hits = hits.AsReadOnly(); Limited = limited; }
            internal string Summary { get { return Limited ? "Primi " + MaxResults + " risultati · restringi la ricerca" : Hits.Count == 0 ? "Nessun risultato" : Hits.Count == 1 ? "1 risultato" : Hits.Count + " risultati"; } }
        }
        private readonly ReadOnlyCollection<Entry> entries;
        private FlowSearch(List<Entry> entries) { this.entries = entries.AsReadOnly(); }
        internal static FlowSearch Capture(Diagram project)
        {
            var entries = new List<Entry>();
            AddGraph(entries, project, null, "Principale");
            if (project.Functions != null) foreach (var function in project.Functions)
            {
                Add(entries, function.Id, null, function.Name, "Funzione", "Definizione", FlowPseudocode.Signature(function));
                AddGraph(entries, function.Body, function.Id, function.Name);
            }
            return new FlowSearch(entries);
        }
        private static void AddGraph(List<Entry> result, Diagram graph, string function, string name)
        {
            foreach (var node in graph.Nodes)
            {
                string kind = Ui.KindName(node.Kind);
                if (node.Kind != NodeKind.Pause) Add(result, function, node.Id, name, kind, "Testo", IoInstruction.Editor(node));
                Add(result, function, node.Id, name, kind, "Domanda di lettura", node.Prompt);
                Add(result, function, node.Id, name, kind, "Nota", node.Comment);
                if (node.Kind == NodeKind.Decision)
                {
                    Add(result, function, node.Id, name, kind, "Ramo vero", node.GetBranchLabel("true"));
                    Add(result, function, node.Id, name, kind, "Ramo falso", node.GetBranchLabel("false"));
                }
                Add(result, function, node.Id, name, kind, "Condizione di pausa", node.BreakpointCondition);
            }
        }
        private static void Add(List<Entry> entries, string function, string node, string graph, string kind, string field, string text)
        { if (!String.IsNullOrWhiteSpace(text)) entries.Add(new Entry(function, node, graph, kind, field, text)); }
        internal Results Find(string query, bool matchCase, bool wholeWord, bool currentOnly, string function, CancellationToken cancellation)
        {
            if (query != null && query.Length > MaxQuery) throw new ArgumentException("La ricerca può contenere al massimo 256 caratteri.");
            var result = new List<Hit>();
            if (String.IsNullOrWhiteSpace(query)) return new Results(result, false);
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var entry in entries)
            {
                cancellation.ThrowIfCancellationRequested();
                if (currentOnly && entry.FunctionId != function) continue;
                int at = 0;
                while (at <= entry.Text.Length - query.Length)
                {
                    int found = entry.Text.IndexOf(query, at, comparison); if (found < 0) break;
                    if (!wholeWord || (!WordBefore(entry.Text, found) && !WordAt(entry.Text, found + query.Length)))
                    {
                        if (result.Count == MaxResults) return new Results(result, true);
                        result.Add(new Hit(entry, found, query.Length)); break;
                    }
                    at = found + 1;
                }
            }
            return new Results(result, false);
        }
        private static bool WordBefore(string value, int at)
        { if (at <= 0) return false; at--; if (at > 0 && Char.IsLowSurrogate(value[at]) && Char.IsHighSurrogate(value[at - 1])) at--; return WordAt(value, at); }
        private static bool WordAt(string value, int at)
        {
            if (at >= value.Length) return false;
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(value, at);
            return category == UnicodeCategory.UppercaseLetter || category == UnicodeCategory.LowercaseLetter || category == UnicodeCategory.TitlecaseLetter ||
                category == UnicodeCategory.ModifierLetter || category == UnicodeCategory.OtherLetter || category == UnicodeCategory.DecimalDigitNumber ||
                category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark || category == UnicodeCategory.ConnectorPunctuation;
        }
    }
}
