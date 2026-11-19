using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Frociart
{
    public enum NodeKind { Start, End, Process, Decision, Input, Output, Call, Declare, OpenFile, ReadFile, WriteFile, CloseFile, TurtleForward, TurtleTurn, TurtleHome, TurtleClear, TurtlePen, Pause, Note }
    public enum Severity { Error, Warning, Info }
    public enum RuntimeState { Ready, Running, WaitingForInput, Completed, Error, WaitingForFile }

    [DataContract]
    public sealed class Diagram
    {
        [DataMember(Order = 0)] public int Version = 1;
        [DataMember(Order = 1)] public string Title = "Nuovo diagramma";
        [DataMember(Order = 2)] public List<FlowNode> Nodes = new List<FlowNode>();
        [DataMember(Order = 3)] public List<FlowEdge> Edges = new List<FlowEdge>();
        [DataMember(Order = 4, EmitDefaultValue = false)] public List<FlowFunction> Functions;
        [DataMember(Order = 5, EmitDefaultValue = false)] public string VariablePolicy;
        public bool RequiresDeclarations {get{return VariablePolicy=="declared";}}
    }

    [DataContract]
    public sealed class FlowNode
    {
        [DataMember(Order = 0)] public string Id = Guid.NewGuid().ToString("N");
        [DataMember(Order = 1)] public NodeKind Kind;
        [DataMember(Order = 2)] public string Text = "";
        internal string PreviewComment;
        internal string PreviewText; // Never serialized or interpreted.
        [DataMember(Order = 3)] public string Prompt = "";
        [DataMember(Order = 4)] public float X;
        [DataMember(Order = 5)] public float Y;
        // Optional metadata: the version-1 desktop and Android formats remain compatible.
        // DataContract deserialization bypasses field initializers, so legacy defaults live in helpers.
        [DataMember(Order = 6, EmitDefaultValue = false)] public string TrueLabel;
        [DataMember(Order = 7, EmitDefaultValue = false)] public string FalseLabel;
        [DataMember(Order = 8, EmitDefaultValue = false)] public string TrueColor;
        [DataMember(Order = 9, EmitDefaultValue = false)] public string FalseColor;
        [DataMember(Order = 10, EmitDefaultValue = false)] public bool Breakpoint;
        [DataMember(Order = 11, EmitDefaultValue = false)] public string BreakpointCondition;
        [DataMember(Order = 12, EmitDefaultValue = false)] public string Comment;
        [DataMember(Order = 13, EmitDefaultValue = false)] public bool FoldedCycle;

        public string GetBranchLabel(string branch)
        {
            return branch == "true" ? (TrueLabel ?? "Sì") : branch == "false" ? (FalseLabel ?? "No") : "";
        }

        public string GetBranchColor(string branch)
        {
            return branch == "true" ? (TrueColor ?? "#39B985") : branch == "false" ? (FalseColor ?? "#EF6C82") : "#7A8BA5";
        }
    }

    internal static class IoInstruction
    {
        internal static bool Separate;
        internal static bool IsIo(NodeKind kind){return kind==NodeKind.Input||kind==NodeKind.Output;}
        internal static void Parse(NodeKind original,string value,out NodeKind kind,out string operand){
            kind=original;operand=value??"";if(!IsIo(original))return;
            string trimmed=operand.Trim();int end=0;while(end<trimmed.Length&&!Char.IsWhiteSpace(trimmed[end]))end++;
            string word=trimmed.Substring(0,end);
            if(word.Equals("leggi",StringComparison.OrdinalIgnoreCase))kind=NodeKind.Input;
            else if(word.Equals("stampa_continua",StringComparison.OrdinalIgnoreCase)){kind=NodeKind.Output;operand=FlowOutput.Store(trimmed.Substring(end).Trim(),false);return;}
            else if(word.Equals("stampa",StringComparison.OrdinalIgnoreCase)||word.Equals("scrivi",StringComparison.OrdinalIgnoreCase))kind=NodeKind.Output;
            else return;
            operand=trimmed.Substring(end).Trim();
        }
        internal static string Editor(FlowNode node){if(node.Kind==NodeKind.Pause)return node.BreakpointCondition??"";return IsIo(node.Kind)?(node.Kind==NodeKind.Input?"leggi "+(node.Text??""):FlowOutput.Editor(node.Text)):node.Text??"";}
        internal static bool Changed(FlowNode node,string value){if(node.Kind==NodeKind.Pause)return (node.BreakpointCondition??"")!=(value??"");NodeKind kind;string text;Parse(node.Kind,value,out kind,out text);return node.Kind!=kind||node.Text!=text;}
        internal static void Apply(FlowNode node,string value){if(node.Kind==NodeKind.Pause){node.BreakpointCondition=value;return;}NodeKind kind;string text;Parse(node.Kind,value,out kind,out text);node.Kind=kind;node.Text=text;}
        internal static NodeKind VisualKind(FlowNode node){if(node.PreviewText==null||!IsIo(node.Kind))return node.Kind;NodeKind kind;string text;Parse(node.Kind,node.PreviewText??Editor(node),out kind,out text);return kind;}
        internal static string Display(FlowNode node){if(node.Kind==NodeKind.Pause){string condition=node.PreviewText??node.BreakpointCondition;return !node.Breakpoint?"Disattivata":String.IsNullOrWhiteSpace(condition)?"Sempre":"Se "+condition;}return node.PreviewText??(Separate?(node.Kind==NodeKind.Output?FlowOutput.Display(node.Text):node.Text):Editor(node))??"";}
        internal const string Hint="Scrivi leggi numero per ricevere un dato, oppure stampa numero per mostrarlo. Per un messaggio: stampa \"Ciao!\".";
    }

    [DataContract]
    public sealed class FlowEdge
    {
        [DataMember(Order = 0)] public string Id = Guid.NewGuid().ToString("N");
        [DataMember(Order = 1)] public string From = "";
        [DataMember(Order = 2)] public string To = "";
        [DataMember(Order = 3)] public string Branch = "next";
    }

    public sealed class ValidationIssue
    {
        public Severity Severity;
        public string NodeId;
        public string Message;
        public string Suggestion;
        public string Code;
        public string FunctionId;
        public override string ToString() { return Message; }
    }

    public static class ValueFormatter
    {
        // Round the exact binary64 value to 15 significant digits, midpoint away from zero.
        // Do not delegate to G15: its midpoint policy changed across .NET generations.
        private static string FormatNumber(double value)
        {
            if(value==0)return "0";
            if(Double.IsNaN(value)||Double.IsInfinity(value))return value.ToString(CultureInfo.InvariantCulture);
            if(Math.Abs(value)<1e15&&value==Math.Truncate(value))return value.ToString("0",CultureInfo.InvariantCulture);
            bool negative=value<0;long bits=BitConverter.DoubleToInt64Bits(Math.Abs(value));int power=(int)((bits>>52)&2047);
            ulong mantissa=(ulong)(bits&4503599627370495L);if(power!=0)mantissa|=4503599627370496UL;
            int shift=(power==0?-1022:power-1023)-52,scale=0;BigInteger coefficient=new BigInteger(mantissa);
            if(shift>=0)coefficient<<=shift;else{scale=-shift;coefficient*=BigInteger.Pow(new BigInteger(5),scale);}
            string digits=coefficient.ToString(CultureInfo.InvariantCulture);int exponent=digits.Length-scale-1;
            if(digits.Length>15){ulong head=UInt64.Parse(digits.Substring(0,15),CultureInfo.InvariantCulture);if(digits[15]>='5')head++;digits=head.ToString(CultureInfo.InvariantCulture);if(digits.Length>15){exponent++;digits=digits.Substring(0,15);}}
            digits=digits.TrimEnd('0');string body;
            if(exponent>=15||exponent< -4)body=digits.Substring(0,1)+(digits.Length>1?"."+digits.Substring(1):"")+"E"+(exponent>=0?"+":"-")+Math.Abs(exponent).ToString("D2",CultureInfo.InvariantCulture);
            else{int at=exponent+1;body=at<=0?"0."+new String('0',-at)+digits:at>=digits.Length?digits+new String('0',at-digits.Length):digits.Substring(0,at)+"."+digits.Substring(at);}
            return(negative?"-":"")+body;
        }
        public static string Format(object value)
        {
            if (value == null) return "";
            if (value is VectorValue) return ((VectorValue)value).Format();
            if (value is bool) return (bool)value ? "vero" : "falso";
            if (value is double)
            {
                double number = (double)value;
                if (number == 0) return "0";
                return FormatNumber(number);
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public static class DiagramIO
    {
        public const int MaxNodes = 500;
        public const int MaxEdges = 1500;
        public const int MaxFileBytes = 4 * 1024 * 1024;
        public const int MaxBlockText = 8192;
        internal const int MaxJsonDepth = 32;

        public static Diagram CreateBlank()
        {
            Diagram d = new Diagram();
            FlowNode start = new FlowNode { Kind = NodeKind.Start, Text = "Inizio", X = 350, Y = 80 };
            FlowNode end = new FlowNode { Kind = NodeKind.End, Text = "Fine", X = 350, Y = 270 };
            d.Nodes.Add(start);
            d.Nodes.Add(end);
            d.Edges.Add(new FlowEdge { From = start.Id, To = end.Id });
            return d;
        }

        public static void Save(string path, Diagram diagram)
        {
            CheckDocument(diagram);
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Scegli un nome per il file.");
            byte[] bytes = Serialize(diagram);
            if (bytes.Length > MaxFileBytes) throw new InvalidDataException("Il diagramma supera il limite di 4 MB.");
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("La cartella scelta non esiste.");
            string temporary = Path.Combine(directory, ".frociart-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static Diagram Load(string path)
        {
            path=DocumentLaunch.LocalPath(path);
            byte[] bytes;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaxFileBytes)
                    throw new InvalidDataException("Il file è vuoto o supera il limite di 4 MB.");
                bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new InvalidDataException("Lettura del file incompleta.");
                    offset += count;
                }
            }
            Diagram result = String.Equals(Path.GetExtension(path),".fprg",StringComparison.OrdinalIgnoreCase) ? FlowgorithmImport.Decode(bytes) : Deserialize(bytes);
            if(String.Equals(Path.GetExtension(path),".fprg",StringComparison.OrdinalIgnoreCase)){string title=Path.GetFileNameWithoutExtension(path);if(!String.IsNullOrWhiteSpace(title))result.Title=title.Length>200?title.Substring(0,200):title;}
            CheckDocument(result);
            return result;
        }

        internal static byte[] EncodeDocument(Diagram document){
            CheckDocument(document);byte[] bytes=Serialize(document);
            if(bytes.Length>MaxFileBytes)throw new InvalidDataException("Il progetto supera il limite di 4 MB.");return bytes;
        }
        internal static Diagram DecodeDocument(byte[] bytes){
            if(bytes==null||bytes.Length==0||bytes.Length>MaxFileBytes)throw new InvalidDataException("Il progetto è vuoto o supera il limite di 4 MB.");
            Diagram document=Deserialize(bytes);CheckDocument(document);return document;
        }

        public static Diagram Clone(Diagram diagram)
        {
            CheckDocument(diagram);
            // History and execution need an isolated model, not a JSON round trip.
            // Strings are immutable; lists, nodes and edges must be new instances.
            Diagram copy = new Diagram
            {
                Version = diagram.Version,
                VariablePolicy = diagram.VariablePolicy,
                Title = diagram.Title,
                Nodes = new List<FlowNode>(diagram.Nodes.Count),
                Edges = new List<FlowEdge>(diagram.Edges.Count)
            };
            foreach (FlowNode node in diagram.Nodes)
                copy.Nodes.Add(new FlowNode
                {
                    Id = node.Id, Kind = node.Kind, Text = node.Text, Prompt = node.Prompt,
                    X = node.X, Y = node.Y, TrueLabel = node.TrueLabel, FalseLabel = node.FalseLabel,
                    TrueColor = node.TrueColor, FalseColor = node.FalseColor, Breakpoint = node.Breakpoint, BreakpointCondition = node.BreakpointCondition, Comment=node.Comment, FoldedCycle=node.FoldedCycle
                });
            foreach (FlowEdge edge in diagram.Edges)
                copy.Edges.Add(new FlowEdge
                {
                    Id = edge.Id, From = edge.From, To = edge.To, Branch = edge.Branch
                });
            if(diagram.Functions!=null)copy.Functions=diagram.Functions.Select(f=>f.Copy()).ToList();
            return copy;
        }

        private static byte[] Serialize(Diagram diagram)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Serializer().WriteObject(stream, diagram);
                return stream.ToArray();
            }
        }

        private static Diagram Deserialize(byte[] bytes)
        {
            try
            {
                CheckJsonEnvelope(bytes);
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, JsonQuotas(MaxFileBytes)))
                {
                    Diagram diagram = Serializer().ReadObject(reader) as Diagram;
                    if (diagram == null) throw new InvalidDataException("Il file non contiene un diagramma.");
                    return diagram;
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex)
            {
                if (!(ex is SerializationException) && !(ex is ArgumentException) &&
                    !(ex is System.Xml.XmlException) && !(ex is FormatException))
                    throw;
                throw new InvalidDataException("Progetto Frociart non valido o danneggiato.", ex);
            }
        }

        private static DataContractJsonSerializer Serializer()
        {
            return new DataContractJsonSerializer(typeof(Diagram),
                new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 40000 });
        }

        internal static System.Xml.XmlDictionaryReaderQuotas JsonQuotas(int maxBytes)
        {
            return new System.Xml.XmlDictionaryReaderQuotas {
                MaxDepth = MaxJsonDepth, MaxStringContentLength = maxBytes,
                MaxArrayLength = 40000, MaxBytesPerRead = 4096,
                MaxNameTableCharCount = maxBytes
            };
        }

        // The framework JSON reader stops at the first root and does not reject
        // trailing bytes. Bound the envelope before it constructs any objects;
        // the framework reader remains responsible for full JSON syntax/types.
        internal static void CheckJsonEnvelope(byte[] bytes)
        {
            Encoding encoding = bytes.Length > 1 && bytes[0] == 0
                ? new UnicodeEncoding(true, false, true)
                : bytes.Length > 1 && bytes[1] == 0
                    ? (Encoding)new UnicodeEncoding(false, false, true) : new UTF8Encoding(false, true);
            string text = encoding.GetString(bytes);
            int depth = 0; bool started = false, complete = false, quoted = false, escaped = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool space = c == ' ' || c == '\t' || c == '\r' || c == '\n';
                if (!started) {
                    if (space) continue;
                    if (c != '{') throw new InvalidDataException("Il file JSON deve contenere un oggetto.");
                    started = true; depth = 1; continue;
                }
                if (complete) {
                    if (!space) throw new InvalidDataException("Il file contiene dati dopo il progetto JSON.");
                    continue;
                }
                if (quoted) {
                    if (escaped) {
                        escaped = false;
                        if (c == 'u') {
                            int code = JsonCodeUnit(text, i + 1);
                            if (code >= 0xd800 && code <= 0xdbff) {
                                if (i + 10 >= text.Length || text[i + 5] != '\\' || text[i + 6] != 'u')
                                    throw new InvalidDataException("Il file JSON contiene Unicode non valido.");
                                int low = JsonCodeUnit(text, i + 7);
                                if (low < 0xdc00 || low > 0xdfff) throw new InvalidDataException("Il file JSON contiene Unicode non valido.");
                                i += 10;
                            }
                            else {
                                if (code >= 0xdc00 && code <= 0xdfff) throw new InvalidDataException("Il file JSON contiene Unicode non valido.");
                                i += 4;
                            }
                        }
                    }
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == '{' || c == '[') {
                    if (++depth > MaxJsonDepth) throw new InvalidDataException("Il file JSON supera il limite di 32 livelli.");
                }
                else if (c == '}' || c == ']') { if (--depth == 0) complete = true; }
            }
            if (!complete || quoted) throw new InvalidDataException("Il progetto JSON è incompleto.");
        }

        private static int JsonCodeUnit(string text, int start)
        {
            int value;
            if (start + 4 > text.Length || !Int32.TryParse(text.Substring(start, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
                throw new InvalidDataException("Il file JSON contiene un escape Unicode non valido.");
            return value;
        }

        internal static void CheckDocument(Diagram d)
        {
            CheckGraph(d);
            FunctionModel.CheckProject(d);
            if(d.Version<4&&FlowAnnotations.Has(d))throw new InvalidDataException("Le note libere richiedono il formato 4. Aggiorna il progetto prima di salvarlo.");
        }

        internal static void CheckGraph(Diagram d)
        {
            if (d == null) throw new InvalidDataException("Il diagramma è vuoto.");
            if (d.Version != 1 && d.Version != 2 && d.Version != 3 && d.Version != 4 && d.Version != 5) throw new InvalidDataException("Versione del file non supportata. Aggiorna Frociart prima di aprirlo.");
            if(d.VariablePolicy!=null&&d.VariablePolicy!="auto"&&d.VariablePolicy!="declared")throw new InvalidDataException("Modalità delle variabili non supportata.");
            if(d.RequiresDeclarations&&d.Version!=5||d.Version==5&&!d.RequiresDeclarations)throw new InvalidDataException("Le dichiarazioni obbligatorie richiedono il formato 5. Aggiorna il progetto prima di salvarlo.");
            if (d.Nodes == null || d.Edges == null) throw new InvalidDataException("Nel file mancano l'elenco dei blocchi o i collegamenti.");
            if (d.Nodes.Count > MaxNodes || d.Edges.Count > MaxEdges)
                throw new InvalidDataException("Limite superato: massimo 500 blocchi e 1500 collegamenti.");
            CheckText(d.Title, 200, "titolo");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowNode n in d.Nodes)
            {
                if (n == null) throw new InvalidDataException("Il file contiene un blocco nullo.");
                CheckText(n.Id, 128, "identificatore");
                if (String.IsNullOrWhiteSpace(n.Id) || !ids.Add(n.Id))
                    throw new InvalidDataException("Il file contiene identificatori di blocco vuoti o duplicati.");
                if (!Enum.IsDefined(typeof(NodeKind), n.Kind))
                    throw new InvalidDataException("Il file contiene un tipo di blocco sconosciuto.");
                if(n.Kind==NodeKind.Note&&n.Breakpoint)throw new InvalidDataException("Una nota libera non può avere punti di interruzione.");
                CheckText(n.Comment,2048,"commento del blocco");
                CheckText(n.Text, MaxBlockText, "testo del blocco");
                CheckText(n.Prompt, 1024, "domanda");
                CheckText(n.BreakpointCondition, MaxBlockText, "condizione del punto di interruzione");
                CheckText(n.TrueLabel, 60, "etichetta del ramo vero");
                CheckText(n.FalseLabel, 60, "etichetta del ramo falso");
                if ((n.TrueColor != null && !BranchColor(n.TrueColor)) ||
                    (n.FalseColor != null && !BranchColor(n.FalseColor)))
                    throw new InvalidDataException("Il colore del ramo deve avere il formato #RRGGBB.");
                if (Single.IsNaN(n.X) || Single.IsInfinity(n.X) ||
                    Single.IsNaN(n.Y) || Single.IsInfinity(n.Y) ||
                    Math.Abs(n.X) > 100000 || Math.Abs(n.Y) > 100000)
                    throw new InvalidDataException("Il file contiene coordinate di blocco non valide.");
            }
            HashSet<string> edgeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowEdge edge in d.Edges)
            {
                if (edge == null) throw new InvalidDataException("Il file contiene un collegamento nullo.");
                CheckText(edge.Id, 128, "identificatore del collegamento");
                CheckText(edge.From, 128, "partenza del collegamento");
                CheckText(edge.To, 128, "arrivo del collegamento");
                CheckText(edge.Branch, 16, "ramo");
                if (String.IsNullOrWhiteSpace(edge.Id) || !edgeIds.Add(edge.Id))
                    throw new InvalidDataException("Il file contiene identificatori di collegamento vuoti o duplicati.");
                if(d.Nodes.Any(n=>n.Kind==NodeKind.Note&&(n.Id==edge.From||n.Id==edge.To)))throw new InvalidDataException("Una nota libera non può avere collegamenti di esecuzione.");
                if (!ids.Contains(edge.From ?? "") || !ids.Contains(edge.To ?? ""))
                    throw new InvalidDataException("Un collegamento punta a un blocco che non esiste.");
                if (edge.Branch != "next" && edge.Branch != "true" && edge.Branch != "false")
                    throw new InvalidDataException("Il file contiene un ramo sconosciuto.");
            }
        }

        private static void CheckText(string text, int max, string name)
        {
            if (text != null && text.Length > max)
                throw new InvalidDataException("Testo troppo lungo: " + name + " (massimo " + max + " caratteri).");
        }

        private static bool BranchColor(string color)
        {
            if (color.Length != 7 || color[0] != '#') return false;
            for (int i = 1; i < color.Length; i++)
            {
                char c = color[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }
    }

    internal sealed class FlowException : Exception
    {
        public FlowException(string message) : base(message) { }
    }

    // Immutable values preserve assignment-by-value and transactional block execution.
    public sealed class VectorValue
    {
        public const int MaxLength=1024;
        private readonly object[] items;
        public int Count {get{return items.Length;}}
        internal VectorValue(IEnumerable<object> values){items=values.ToArray();if(items.Length>MaxLength)throw new FlowException("Un vettore può contenere al massimo 1024 elementi.");int kind=-1,total=2;foreach(object value in items){int type=value is bool?1:value is string?2:Values.Numeric(value)?0:-1;if(type<0)throw new FlowException("I vettori contengono numeri, testi o valori logici; i vettori annidati non sono supportati.");if(kind<0)kind=type;else if(kind!=type)throw new FlowException("Tutti gli elementi del vettore devono avere lo stesso tipo.");if(type==0)Values.Number(value);total+=Element(value).Length+2;if(total>Values.MaxStringLength)throw new FlowException("Il contenuto del vettore supera 32768 caratteri.");}}
        private static string Element(object value){return value is string?"\""+((string)value).Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t")+"\"":ValueFormatter.Format(value);}
        public string Format(){return "["+String.Join(", ",items.Select(Element))+ "]";}
        internal static int Integer(object value,string label){double n=Values.Number(value);if(n<0||n>Int32.MaxValue||n!=Math.Floor(n))throw new FlowException(label+" deve essere un intero maggiore o uguale a 0.");return(int)n;}
        internal int Index(object index){int i=Integer(index,"L’indice");if(i>=Count)throw new FlowException(Count==0?"Il vettore è vuoto: non contiene elementi.":"Indice "+i+" fuori dal vettore. Usa un indice da 0 a "+(Count-1)+".");return i;}
        public object At(int index){return items[Index((double)index)];}
        internal static int ScalarKind(object value){int kind=value is bool?1:value is string?2:Values.Numeric(value)?0:-1;if(kind<0)throw new FlowException("Il valore iniziale deve essere un numero, un testo o un valore logico.");if(kind==0)Values.Number(value);return kind;}
        internal VectorValue Set(int index,object value){Index((double)index);if(ScalarKind(items[index])!=ScalarKind(value))throw new FlowException("Il nuovo valore deve avere lo stesso tipo degli elementi del vettore.");object[] next=(object[])items.Clone();next[index]=value;return new VectorValue(next);}
        internal static VectorValue Require(object value){var vector=value as VectorValue;if(vector==null)throw new FlowException("Serve un vettore, per esempio valori = [10, 20, 30].");return vector;}
        public override bool Equals(object value){var other=value as VectorValue;return other!=null&&items.SequenceEqual(other.items);}
        public override int GetHashCode(){int hash=17;foreach(object item in items)hash=unchecked(hash*31+item.GetHashCode());return hash;}
        public override string ToString(){return Format();}
    }

    internal static class Values
    {
        public const int MaxStringLength = 32768;

        public static double Number(object value)
        {
            if (value is double) return Finite((double)value);
            if (value is int || value is long || value is float || value is decimal)
                return Finite(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            throw new FlowException("Serve un numero, ma il valore è «" + Short(value) + "».");
        }

        public static bool Truth(object value)
        {
            if (value is bool) return (bool)value;
            if (value is double || value is int || value is long || value is float || value is decimal)
                return Number(value) != 0;
            throw new FlowException("La condizione deve restituire vero/falso o un numero, non un testo.");
        }

        public static double Finite(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                throw new FlowException("Il risultato non è un numero finito: controlla dominio e grandezza dei valori.");
            return value;
        }

        public static string String(string value)
        {
            if (value.Length > MaxStringLength)
                throw new FlowException("Il testo supera il limite di " + MaxStringLength + " caratteri.");
            return value;
        }

        public static string Short(object value)
        {
            string text = ValueFormatter.Format(value);
            return text.Length <= 60 ? text : text.Substring(0, 57) + "...";
        }

        public static object Input(string raw)
        {
            if (raw == null) raw = "";
            if (raw.Length > MaxStringLength) throw new FlowException("Il valore inserito è troppo lungo.");
            string trimmed = raw.Trim();
            double n;
            if (Double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ||
                Double.TryParse(trimmed, NumberStyles.Float, CultureInfo.GetCultureInfo("it-IT"), out n))
            {
                if((Double.IsNaN(n)||Double.IsInfinity(n))&&(trimmed.IndexOf('\0')>=0||trimmed.Equals("+NaN",StringComparison.OrdinalIgnoreCase)||trimmed.Equals("-NaN",StringComparison.OrdinalIgnoreCase)))return raw;
                return Finite(n);
            }
            if (LooksNumeric(trimmed))
                throw new FlowException("Il numero inserito è troppo grande. Usa un numero finito più piccolo.");
            if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("vero", StringComparison.OrdinalIgnoreCase)) return true;
            if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("falso", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.Equals("NaN", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Infinity", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("+Infinity", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("-Infinity", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("infinito", StringComparison.OrdinalIgnoreCase))
                throw new FlowException("Inserisci un numero finito.");
            return raw;
        }

        private static bool LooksNumeric(string text)
        {
            int i = 0, digits = 0;
            if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9') { i++; digits++; }
            if (i < text.Length && (text[i] == '.' || text[i] == ','))
            {
                i++;
                while (i < text.Length && text[i] >= '0' && text[i] <= '9') { i++; digits++; }
            }
            if (digits == 0) return false;
            if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
            {
                i++;
                if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
                int exponentStart = i;
                while (i < text.Length && text[i] >= '0' && text[i] <= '9') i++;
                if (i == exponentStart) return false;
            }
            return i == text.Length;
        }

        public static bool Numeric(object value)
        {
            return value is double || value is int || value is long || value is float || value is decimal;
        }
    }

    internal abstract class Expr
    {
        internal abstract object Eval(IDictionary<string, object> variables);
        internal abstract void ReadVariables(HashSet<string> names);
    }

    internal sealed class LiteralExpr : Expr
    {
        internal readonly object Value;
        internal LiteralExpr(object value) { Value = value; }
        internal override object Eval(IDictionary<string, object> variables) { return Value; }
        internal override void ReadVariables(HashSet<string> names) { }
    }

    internal sealed class VariableExpr : Expr
    {
        internal readonly string name;
        internal VariableExpr(string name) { this.name = name; }
        internal override object Eval(IDictionary<string, object> variables)
        {
            object value;
            if (!variables.TryGetValue(name, out value))
            {
                if (name.Equals("pi", StringComparison.OrdinalIgnoreCase) || name == "π") return Math.PI;
                throw new FlowException("La variabile «" + name + "» non ha ancora un valore.");
            }
            return value;
        }
        internal override void ReadVariables(HashSet<string> names) { if (!name.Equals("pi", StringComparison.OrdinalIgnoreCase) && name != "π") names.Add(name); }
    }

    internal sealed class VectorExpr : Expr {
        internal readonly List<Expr> items;internal VectorExpr(List<Expr> items){this.items=items;}
        internal override object Eval(IDictionary<string,object> variables){return new VectorValue(items.Select(item=>item.Eval(variables)));}
        internal override void ReadVariables(HashSet<string> names){foreach(Expr item in items)item.ReadVariables(names);}
    }
    internal sealed class IndexExpr : Expr {
        internal readonly Expr Target,Index;internal IndexExpr(Expr target,Expr index){Target=target;Index=index;}
        internal override object Eval(IDictionary<string,object> variables){var vector=VectorValue.Require(Target.Eval(variables));return vector.At(vector.Index(Index.Eval(variables)));}
        internal override void ReadVariables(HashSet<string> names){Target.ReadVariables(names);Index.ReadVariables(names);}
    }
    internal sealed class UnaryExpr : Expr
    {
        internal readonly string op;
        internal readonly Expr operand;
        internal UnaryExpr(string op, Expr operand) { this.op = op; this.operand = operand; }
        internal override object Eval(IDictionary<string, object> variables)
        {
            object value = operand.Eval(variables);
            if (op == "not") return !Values.Truth(value);
            double n = Values.Number(value);
            return op == "-" ? -n : n;
        }
        internal override void ReadVariables(HashSet<string> names) { operand.ReadVariables(names); }
    }

    internal sealed class BinaryExpr : Expr
    {
        internal readonly string op;
        internal readonly Expr left, right;
        internal BinaryExpr(string op, Expr left, Expr right) { this.op = op; this.left = left; this.right = right; }
        internal override object Eval(IDictionary<string, object> variables)
        {
            object a = left.Eval(variables);
            if (op == "and") return Values.Truth(a) && Values.Truth(right.Eval(variables));
            if (op == "or") return Values.Truth(a) || Values.Truth(right.Eval(variables));
            object b = right.Eval(variables);
            if (op == "&" || (op == "+" && (a is string || b is string)))
            {
                string first = ValueFormatter.Format(a), second = ValueFormatter.Format(b);
                if (first.Length + second.Length > Values.MaxStringLength)
                    throw new FlowException("Il testo prodotto supera il limite di " + Values.MaxStringLength + " caratteri.");
                return first + second;
            }
            if (op == "=" || op == "==" || op == "!=" || op == "<>")
            {
                bool same;
                if (Values.Numeric(a) && Values.Numeric(b)) same = Values.Number(a) == Values.Number(b);
                else same = Object.Equals(a, b);
                return op == "!=" || op == "<>" ? !same : same;
            }
            if (op == "<" || op == "<=" || op == ">" || op == ">=")
            {
                int comparison;
                if (a is string && b is string)
                    comparison = System.String.CompareOrdinal((string)a, (string)b);
                else comparison = Values.Number(a).CompareTo(Values.Number(b));
                switch (op)
                {
                    case "<": return comparison < 0;
                    case "<=": return comparison <= 0;
                    case ">": return comparison > 0;
                    default: return comparison >= 0;
                }
            }
            double x = Values.Number(a), y = Values.Number(b);
            switch (op)
            {
                case "+": return Values.Finite(x + y);
                case "-": return Values.Finite(x - y);
                case "*": return Values.Finite(x * y);
                case "/":
                    if (y == 0) throw new FlowException("Divisione per zero: il divisore deve essere diverso da 0.");
                    return Values.Finite(x / y);
                case "%":
                    if (y == 0) throw new FlowException("Resto con divisore zero: usa un divisore diverso da 0.");
                    return Values.Finite(x % y);
                case "^": return Values.Finite(Math.Pow(x, y));
                default: throw new FlowException("Operatore non riconosciuto.");
            }
        }

        internal override void ReadVariables(HashSet<string> names)
        {
            left.ReadVariables(names);
            LiteralExpr literal = left as LiteralExpr;
            if (literal != null && (literal.Value is bool || Values.Numeric(literal.Value)))
            {
                bool truth = Values.Truth(literal.Value);
                if ((op == "and" && !truth) || (op == "or" && truth)) return;
            }
            right.ReadVariables(names);
        }
    }

    internal sealed class CallExpr : Expr
    {
        internal readonly string name;
        internal readonly List<Expr> arguments;
        internal readonly FlowFunction Function;
        internal CallExpr(string name, List<Expr> arguments, IDictionary<string,FlowFunction> functions=null)
        {
            this.name = name.ToLowerInvariant();
            this.arguments = arguments;
            int count = arguments.Count;
            FlowFunction function;
            if(functions!=null&&functions.TryGetValue(name,out function))
            {
                Function=function;
                if(count!=function.Parameters.Count)throw new FlowException("«"+function.Name+"» richiede "+function.Parameters.Count+" argomenti; ne hai scritti "+count+".");
                for(int i=0;i<count;i++)if(function.Parameters[i].ByReference&&!(arguments[i] is VariableExpr))throw new FlowException("Il parametro per riferimento «"+function.Parameters[i].Name+"» richiede il nome di un vettore, non un elemento o un'espressione.");
                return;
            }
            bool valid;
            switch (this.name)
            {
                case "abs": case "sqrt": case "floor": case "len": case "sum": case "somma":
                case "sin": case "cos": case "tan": case "arcsin": case "arccos": case "arctan":
                case "ln": case "log": case "log10": case "sgn": case "random": case "size":
                case "tochar": case "tocode": case "tointeger": case "toreal": case "tostring": valid = count == 1; break;
                case "int": valid = count == 1 || count == 2; break;
                case "eof": valid = count == 0; break;
                case "char": case "tofixed": valid = count == 2; break;
                case "round": case "array": case "vettore": valid = count == 1 || count == 2; break;
                case "min": case "max": valid = count >= 1 && count <= 16; break;
                default: throw new FlowException("Funzione «" + name + "» sconosciuta. Apri Funzioni ed esempi per vedere quelle disponibili.");
            }
            if (!valid) throw new FlowException("Numero di argomenti non valido per «" + name + "».");
        }
        internal override object Eval(IDictionary<string, object> variables)
        {
            if(Function!=null)throw new FlowException("Questa funzione va eseguita nel suo progetto.");
            if(name=="eof")throw new FlowException("EOF() richiede un file aperto nel programma in esecuzione.");
            object first = arguments[0].Eval(variables);
            if(name=="array"||name=="vettore"){int length=VectorValue.Integer(first,"La dimensione");if(length>VectorValue.MaxLength)throw new FlowException("Un vettore può contenere al massimo 1024 elementi.");object initial=arguments.Count==2?arguments[1].Eval(variables):0.0;VectorValue.ScalarKind(initial);return new VectorValue(Enumerable.Repeat(initial,length));}
            if(name=="len"){if(first is VectorValue)return(double)((VectorValue)first).Count;if(first is string)return(double)IntrinsicOperations.CharacterCount((string)first);throw new FlowException("len richiede un testo o un vettore.");}
            if(IntrinsicOperations.Handles(name))return IntrinsicOperations.Evaluate(name,arguments.Count==1?new[]{first}:new[]{first,arguments[1].Eval(variables)});
            if(name=="sum"||name=="somma"||(name=="min"||name=="max")&&arguments.Count==1){var vector=VectorValue.Require(first);if(vector.Count==0&&name!="sum"&&name!="somma")throw new FlowException("Il vettore è vuoto: min e max richiedono almeno un elemento.");double result=0;for(int i=0;i<vector.Count;i++){double n=Values.Number(vector.At(i));result=name=="min"?(i==0?n:Math.Min(result,n)):name=="max"?(i==0?n:Math.Max(result,n)):Values.Finite(result+n);}return result;}
            double number = Values.Number(first);
            switch (name)
            {
                case "abs": return Math.Abs(number);
                case "sqrt":
                    if (number < 0) throw new FlowException("La radice quadrata richiede un numero maggiore o uguale a 0.");
                    return Math.Sqrt(number);
                case "floor": return Math.Floor(number);
                case "round":
                    int digits = 0;
                    if (arguments.Count == 2)
                    {
                        double places = Values.Number(arguments[1].Eval(variables));
                        if (places < 0 || places > 15 || places != Math.Floor(places))
                            throw new FlowException("Le cifre decimali di round devono essere un intero tra 0 e 15.");
                        digits = (int)places;
                    }
                    return Math.Round(number, digits, MidpointRounding.AwayFromZero);
                case "min": case "max":
                    for (int i = 1; i < arguments.Count; i++)
                    {
                        double next = Values.Number(arguments[i].Eval(variables));
                        number = name == "min" ? Math.Min(number, next) : Math.Max(number, next);
                    }
                    return number;
                default: throw new FlowException("Funzione sconosciuta.");
            }
        }
        internal override void ReadVariables(HashSet<string> names)
        {
            foreach (Expr argument in arguments) argument.ReadVariables(names);
        }
    }

    internal static class IntrinsicOperations
    {
        // Framework's x87 trigonometry loses range reduction (and returns x for huge inputs).
        // Windows 10/11 already includes UCRT. Restrict native resolution to System32;
        // modern .NET and non-Windows hosts retain their platform Math implementation.
        private static readonly bool UseSystemTrigonometry = System.Environment.OSVersion.Platform == System.PlatformID.Win32NT && typeof(object).Assembly.GetName().Name == "mscorlib";
        [System.Runtime.InteropServices.DllImport("ucrtbase.dll", EntryPoint="sin", ExactSpelling=true, CallingConvention=System.Runtime.InteropServices.CallingConvention.Cdecl)]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
        private static extern double SystemSin(double value);
        [System.Runtime.InteropServices.DllImport("ucrtbase.dll", EntryPoint="cos", ExactSpelling=true, CallingConvention=System.Runtime.InteropServices.CallingConvention.Cdecl)]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
        private static extern double SystemCos(double value);
        [System.Runtime.InteropServices.DllImport("ucrtbase.dll", EntryPoint="tan", ExactSpelling=true, CallingConvention=System.Runtime.InteropServices.CallingConvention.Cdecl)]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
        private static extern double SystemTan(double value);
        internal static double Sin(double value) { return UseSystemTrigonometry ? SystemSin(value) : Math.Sin(value); }
        internal static double Cos(double value) { return UseSystemTrigonometry ? SystemCos(value) : Math.Cos(value); }
        internal static double Tan(double value) { return UseSystemTrigonometry ? SystemTan(value) : Math.Tan(value); }
        private static readonly Random RandomSource = new Random();
        private static readonly HashSet<string> Names = new HashSet<string>(new[]{"sin","cos","tan","arcsin","arccos","arctan","int","ln","log","log10","sgn","random","size","char","tochar","tocode","tofixed","tointeger","toreal","tostring"});
        internal static bool Handles(string name)
        {
            return Names.Contains(name);
        }
        private static string Text(object value)
        {if(!(value is string))throw new FlowException("Questa funzione richiede un testo tra virgolette o una variabile di testo.");return(string)value;}
        internal static List<string> Characters(string text)
        {
            var result=new List<string>();
            for(int i=0;i<text.Length;i++)
            {
                if(Char.IsHighSurrogate(text[i]))
                {if(i+1>=text.Length||!Char.IsLowSurrogate(text[i+1]))throw new FlowException("Il testo contiene un carattere Unicode incompleto.");result.Add(text.Substring(i++,2));}
                else {if(Char.IsLowSurrogate(text[i]))throw new FlowException("Il testo contiene un carattere Unicode incompleto.");result.Add(text[i].ToString());}
            }
            return result;
        }
        internal static int CharacterCount(string text)
        {
            int count=0;for(int i=0;i<text.Length;i++,count++)
            {if(Char.IsHighSurrogate(text[i])){if(i+1>=text.Length||!Char.IsLowSurrogate(text[i+1]))throw new FlowException("Il testo contiene un carattere Unicode incompleto.");i++;}else if(Char.IsLowSurrogate(text[i]))throw new FlowException("Il testo contiene un carattere Unicode incompleto.");}
            return count;
        }
        private static int Integer(object value,int minimum,int maximum,string label)
        {
            double n=Values.Number(value);
            if(n<minimum||n>maximum||n!=Math.Truncate(n))throw new FlowException(label+" deve essere un intero da "+minimum+" a "+maximum+".");
            return(int)n;
        }
        // Framework's Double.ToString("F") keeps only 15 significant digits.
        // Format the already rounded binary value exactly, matching Android's
        // BigDecimal(double) without losing large integer or requested fractional digits.
        private static string Fixed(double number,int digits)
        {
            double rounded=Math.Round(number,digits,MidpointRounding.AwayFromZero);
            long bits=BitConverter.DoubleToInt64Bits(Math.Abs(rounded));
            int exponent=(int)((bits>>52)&0x7ff);
            long mantissa=bits&0xfffffffffffffL;
            if(exponent!=0)mantissa|=1L<<52;
            int shift=(exponent==0?-1022:exponent-1023)-52;
            BigInteger scaled=new BigInteger(mantissa)*BigInteger.Pow(10,digits);
            if(shift>=0)scaled<<=shift;
            else
            {
                BigInteger denominator=BigInteger.One<<-shift,remainder;
                scaled=BigInteger.DivRem(scaled,denominator,out remainder);
                if(remainder*2>=denominator)scaled+=BigInteger.One;
            }
            string text=scaled.ToString(CultureInfo.InvariantCulture);
            if(digits>0){text=text.PadLeft(digits+1,'0');text=text.Insert(text.Length-digits,".");}
            return rounded<0&&!scaled.IsZero?"-"+text:text;
        }
        internal static object Evaluate(string name,object[] args)
        {
            object first=args[0];
            if(name=="size")return(double)VectorValue.Require(first).Count;
            if(name=="char")
            {
                var chars=Characters(Text(first));int index=Integer(args[1],0,Int32.MaxValue,"L’indice");
                if(index>=chars.Count)throw new FlowException(chars.Count==0?"Il testo è vuoto: non contiene caratteri.":"Indice fuori dal testo. Usa un indice da 0 a "+(chars.Count-1)+".");
                return chars[index];
            }
            if(name=="tocode")
            {
                var chars=Characters(Text(first));if(chars.Count!=1)throw new FlowException("tocode richiede esattamente un carattere.");
                return(double)Char.ConvertToUtf32(chars[0],0);
            }
            // The optional mode validates generated FOR steps without reserving a
            // new function name that could conflict with an existing user project.
            if(name=="int" && args.Length==2) {
                if(!(args[1] is string)||!String.Equals((string)args[1],"passo",StringComparison.OrdinalIgnoreCase))throw new FlowException("Il secondo argomento di int deve essere \"passo\" per controllare il passo di un ciclo.");
                if(!(first is double||first is int||first is long||first is float||first is decimal))throw new FlowException("Il passo del ciclo deve essere un numero intero diverso da zero.");
                double step=Values.Number(first);if(step==0||step!=Math.Truncate(step)||step<Int32.MinValue||step>Int32.MaxValue)throw new FlowException("Il passo del ciclo deve essere un intero diverso da zero, da -2147483648 a 2147483647.");return step;
            }
            if(name=="tointeger"||name=="toreal")
            {
                string text=Text(first).Trim();double value;
                string pattern=name=="tointeger"?@"^[+-]?[0-9]+$":@"^[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?$";
                if(!System.Text.RegularExpressions.Regex.IsMatch(text,pattern)||!Double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out value))
                    throw new FlowException(name=="tointeger"?"Scrivi un testo che contenga un numero intero, per esempio \"42\".":"Scrivi un testo che contenga un numero, per esempio \"12.5\".");
                value=Values.Finite(value);
                if(name=="tointeger"&&(value<Int32.MinValue||value>Int32.MaxValue))throw new FlowException("tointeger accetta valori da -2147483648 a 2147483647.");
                return value;
            }
            double n=Values.Number(first);
            switch(name)
            {
                case "sin":return Values.Finite(Sin(n));
                case "cos":return Values.Finite(Cos(n));
                case "tan":return Values.Finite(Tan(n));
                case "arcsin":case "arccos":
                    if(n < -1||n > 1)throw new FlowException(name+" richiede un numero tra -1 e 1.");
                    return name=="arcsin"?Math.Asin(n):Math.Acos(n);
                case "arctan":return Math.Atan(n);
                case "int":return Math.Truncate(n);
                case "sgn":return(double)Math.Sign(n);
                case "ln":case "log":case "log10":
                    if(n<=0)throw new FlowException("Il logaritmo richiede un numero maggiore di 0.");
                    return name=="log10"?Math.Log10(n):Math.Log(n);
                case "random":
                    int bound=Integer(first,1,Int32.MaxValue,"Il limite di random");
                    lock(RandomSource)return(double)RandomSource.Next(bound);
                case "tochar":
                    int code=Integer(first,0,0x10FFFF,"Il codice Unicode");
                    if(code>=0xD800&&code<=0xDFFF)throw new FlowException("Il codice non rappresenta un carattere Unicode completo.");
                    return Char.ConvertFromUtf32(code);
                case "tostring":return ValueFormatter.Format(n);
                case "tofixed":
                    int digits=Integer(args[1],0,15,"Le cifre decimali");
                    return Fixed(n,digits);
                default:throw new FlowException("Funzione sconosciuta.");
            }
        }
    }

    internal sealed class Token
    {
        internal string Kind, Text;
        internal object Value;
        internal int Position;
    }

    public static class ExpressionLanguage
    {
        public static object Evaluate(string expression, IDictionary<string, object> variables)
        {
            Dictionary<string, object> normalized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (variables != null)
                foreach (KeyValuePair<string, object> pair in variables) normalized[pair.Key] = pair.Value;
            return Parse(expression).Eval(normalized);
        }

        public static IList<string> GetVariables(string expression)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Parse(expression).ReadVariables(names);
            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        }

        // Same UTF16 letter/digit contract as Identifier and the expression lexer.
        internal const string IdentifierPattern = @"[\p{L}_][\p{L}\p{Nd}_]{0,63}";
        internal const string IdentifierWordPattern = @"[\p{L}_][\p{L}\p{Nd}_]*";
        internal static bool Identifier(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 64 ||
                !(Char.IsLetter(name[0]) || name[0] == '_')) return false;
            for (int i = 1; i < name.Length; i++)
                if (!(Char.IsLetterOrDigit(name[i]) || name[i] == '_')) return false;
            string lower = name.ToLowerInvariant();
            return lower != "true" && lower != "false" && lower != "vero" && lower != "falso" &&
                lower != "and" && lower != "or" && lower != "not" &&
                lower != "e" && lower != "o" && lower != "non";
        }

        internal static Expr Parse(string expression,IDictionary<string,FlowFunction> functions=null)
        {
            if (String.IsNullOrWhiteSpace(expression)) throw new FlowException("Scrivi un'espressione.");
            if (expression.Length > DiagramIO.MaxBlockText) throw new FlowException("L'espressione è troppo lunga.");
            return new Parser(expression,functions).Read();
        }

        private sealed class Parser
        {
            private readonly List<Token> tokens;
            private readonly IDictionary<string,FlowFunction> functions;
            private int index, depth, astCount;
            internal Parser(string text,IDictionary<string,FlowFunction> functions) { tokens = Lex(text);this.functions=functions; }
            private Token Current { get { return tokens[index]; } }

            internal Expr Read()
            {
                Expr result = Or();
                if (Current.Kind != "end")
                    Fail("Elemento inatteso «" + Current.Text + "».");
                return result;
            }
            private void Enter()
            {
                if (++depth > 80) throw new FlowException("Troppe parentesi o operazioni annidate (limite 80).");
            }
            private Expr Node(Expr expr)
            {
                if (++astCount > 512) throw new FlowException("Espressione troppo complessa (limite 512 elementi).");
                return expr;
            }
            private bool Take(string text)
            {
                if (Current.Kind == text || (Current.Kind == "op" && Current.Text == text)) { index++; return true; }
                return false;
            }
            private void Expect(string text)
            {
                if (!Take(text)) Fail("Manca «" + text + "».");
            }
            private void Fail(string text) { throw new FlowException(text + " Posizione " + (Current.Position + 1) + "."); }
            private Expr Or()
            {
                Expr result = And();
                while (Take("or")) result = Node(new BinaryExpr("or", result, And()));
                return result;
            }
            private Expr And()
            {
                Expr result = Equality();
                while (Take("and")) result = Node(new BinaryExpr("and", result, Equality()));
                return result;
            }
            private Expr Equality()
            {
                Expr result = Relation();
                while (Current.Kind == "op" && (Current.Text == "=" || Current.Text == "==" || Current.Text == "!=" || Current.Text == "<>"))
                {
                    string op = Current.Text; index++;
                    result = Node(new BinaryExpr(op, result, Relation()));
                }
                return result;
            }
            private Expr Relation()
            {
                Expr result = Concat();
                while (Current.Kind == "op" && (Current.Text == "<" || Current.Text == "<=" || Current.Text == ">" || Current.Text == ">="))
                {
                    string op = Current.Text; index++;
                    result = Node(new BinaryExpr(op, result, Concat()));
                }
                return result;
            }
            private Expr Concat()
            {
                Expr result = Sum();
                while (Take("&")) result = Node(new BinaryExpr("&", result, Sum()));
                return result;
            }
            private Expr Sum()
            {
                Expr result = Product();
                while (Current.Kind == "op" && (Current.Text == "+" || Current.Text == "-"))
                {
                    string op = Current.Text; index++;
                    result = Node(new BinaryExpr(op, result, Product()));
                }
                return result;
            }
            private Expr Product()
            {
                Expr result = Unary();
                // Keep mod usable as an existing variable or function name; it is an operator only after a left operand.
                while (Current.Kind == "op" && (Current.Text == "*" || Current.Text == "/" || Current.Text == "%") || Current.Kind == "id" && Current.Text.Equals("mod", StringComparison.OrdinalIgnoreCase))
                {
                    string op = Current.Kind == "id" ? "%" : Current.Text; index++;
                    result = Node(new BinaryExpr(op, result, Unary()));
                }
                return result;
            }
            private Expr Unary()
            {
                Enter();
                try
                {
                    if (Current.Kind == "op" && (Current.Text == "-" || Current.Text == "+" || Current.Text == "not"))
                    {
                        string op = Current.Text; index++;
                        return Node(new UnaryExpr(op, Unary()));
                    }
                    return Power();
                }
                finally { depth--; }
            }
            private Expr Power()
            {
                Expr result = Primary();
                if (Take("^")) result = Node(new BinaryExpr("^", result, Unary()));
                return result;
            }
            private Expr Primary(){Expr result=Atom();while(Take("[")){Expr at=Or();Expect("]");result=Node(new IndexExpr(result,at));}return result;}
            private Expr Atom()
            {
                Token token = Current;
                if(Take("[")){var values=new List<Expr>();if(!Take("]")){do{if(values.Count>=VectorValue.MaxLength)Fail("Massimo 1024 elementi per vettore.");values.Add(Or());}while(Take(","));Expect("]");}return Node(new VectorExpr(values));}
                if (Take("("))
                {
                    Expr nested = Or();
                    Expect(")");
                    return nested;
                }
                if (token.Kind == "number" || token.Kind == "string" || token.Kind == "bool")
                {
                    index++;
                    return Node(new LiteralExpr(token.Value));
                }
                if (token.Kind == "id")
                {
                    index++;
                    if (Take("("))
                    {
                        List<Expr> args = new List<Expr>();
                        if (!Take(")"))
                        {
                            do
                            {
                                if (args.Count >= 16) Fail("Massimo 16 argomenti per funzione.");
                                args.Add(Or());
                            } while (Take(","));
                            Expect(")");
                        }
                        return Node(new CallExpr(token.Text, args, functions));
                    }
                    return Node(new VariableExpr(token.Text));
                }
                Fail(token.Kind == "end" ? "Espressione incompleta." : "Elemento inatteso «" + token.Text + "».");
                return null;
            }

            private static List<Token> Lex(string text)
            {
                List<Token> result = new List<Token>();
                int i = 0;
                while (i < text.Length)
                {
                    char c = text[i];
                    if (Char.IsWhiteSpace(c)) { i++; continue; }
                    if (result.Count >= 1024) throw new FlowException("Espressione troppo lunga (massimo 1024 elementi).");
                    int start = i;
                    if (Char.IsDigit(c) || (c == '.' && i + 1 < text.Length && Char.IsDigit(text[i + 1])))
                    {
                        while (i < text.Length && Char.IsDigit(text[i])) i++;
                        if (i < text.Length && text[i] == '.') { i++; while (i < text.Length && Char.IsDigit(text[i])) i++; }
                        if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
                        {
                            i++;
                            if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
                            int exponentStart = i;
                            while (i < text.Length && Char.IsDigit(text[i])) i++;
                            if (i == exponentStart) throw new FlowException("Esponente numerico incompleto alla posizione " + (start + 1) + ".");
                        }
                        string literal = text.Substring(start, i - start);
                        double value;
                        if (!Double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                            throw new FlowException("Numero non valido «" + literal + "».");
                        result.Add(new Token { Kind = "number", Text = literal, Value = Values.Finite(value), Position = start });
                        continue;
                    }
                    if (Char.IsLetter(c) || c == '_')
                    {
                        i++;
                        while (i < text.Length && (Char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                        string id = text.Substring(start, i - start);
                        if (id.Length > 64) throw new FlowException("Nome variabile troppo lungo (massimo 64 caratteri).");
                        string lower = id.ToLowerInvariant();
                        if (lower == "true" || lower == "false" || lower == "vero" || lower == "falso")
                            result.Add(new Token { Kind = "bool", Text = id, Value = lower == "true" || lower == "vero", Position = start });
                        else if (lower == "and" || lower == "e" || lower == "or" || lower == "o" || lower == "not" || lower == "non")
                        {
                            string op = lower == "e" ? "and" : lower == "o" ? "or" : lower == "non" ? "not" : lower;
                            result.Add(new Token { Kind = "op", Text = op, Position = start });
                        }
                        else result.Add(new Token { Kind = "id", Text = id, Position = start });
                        continue;
                    }
                    if (c == '"' || c == '\'')
                    {
                        char quote = c; i++;
                        StringBuilder content = new StringBuilder();
                        bool closed = false;
                        while (i < text.Length)
                        {
                            char next = text[i++];
                            if (next == quote) { closed = true; break; }
                            if (next == '\\')
                            {
                                if (i >= text.Length) throw new FlowException("Sequenza di escape incompleta nel testo.");
                                char escape = text[i++];
                                switch (escape)
                                {
                                    case 'n': content.Append('\n'); break;
                                    case 'r': content.Append('\r'); break;
                                    case 't': content.Append('\t'); break;
                                    case '\\': content.Append('\\'); break;
                                    case '"': content.Append('"'); break;
                                    case '\'': content.Append('\''); break;
                                    default: throw new FlowException("Escape non valido: \\" + escape + ". Usa \\\\ per una barra.");
                                }
                            }
                            else content.Append(next);
                        }
                        if (!closed) throw new FlowException("Chiudi le virgolette aperte alla posizione " + (start + 1) + ".");
                        result.Add(new Token { Kind = "string", Text = text.Substring(start, i - start), Value = content.ToString(), Position = start });
                        continue;
                    }
                    if (c == '(' || c == ')' || c == ',' || c == '[' || c == ']')
                    {
                        i++;
                        result.Add(new Token { Kind = c.ToString(), Text = c.ToString(), Position = start });
                        continue;
                    }
                    string symbol = c.ToString();
                    if (i + 1 < text.Length)
                    {
                        string two = text.Substring(i, 2);
                        if (two == "==" || two == "!=" || two == "<>" || two == "<=" || two == ">=" || two == "&&" || two == "||")
                        { symbol = two; i++; }
                    }
                    switch (symbol) {
                        case "×": symbol = "*"; break; case "÷": symbol = "/"; break; case "↑": symbol = "^"; break;
                        case "≤": symbol = "<="; break; case "≥": symbol = ">="; break; case "≠": symbol = "!="; break;
                        case "¬": symbol = "not"; break; case "∧": symbol = "and"; break; case "∨": symbol = "or"; break;
                    }
                    if (symbol == "&&") symbol = "and";
                    if (symbol == "||") symbol = "or";
                    if (symbol == "!") symbol = "not";
                    if (symbol != "&" && symbol != "+" && symbol != "-" && symbol != "*" && symbol != "/" && symbol != "%" && symbol != "^" &&
                        symbol != "=" && symbol != "==" && symbol != "!=" && symbol != "<>" &&
                        symbol != "<" && symbol != "<=" && symbol != ">" && symbol != ">=" &&
                        symbol != "and" && symbol != "or" && symbol != "not")
                        throw new FlowException("Carattere non valido «" + c + "» alla posizione " + (start + 1) + ".");
                    i++;
                    result.Add(new Token { Kind = "op", Text = symbol, Position = start });
                }
                result.Add(new Token { Kind = "end", Text = "", Position = text.Length });
                return result;
            }
        }
    }

    internal sealed class Assignment
    {
        internal string Name;
        internal Expr Expression,Index;
        internal Expr TargetRead(){return Index==null?null:new IndexExpr(new VariableExpr(Name),Index);}
        internal void Write(IDictionary<string,object> variables,object value){if(Index==null){variables[Name]=value;return;}object target;if(!variables.TryGetValue(Name,out target))throw new FlowException("La variabile «"+Name+"» non ha ancora un valore.");var vector=VectorValue.Require(target);variables[Name]=vector.Set(vector.Index(Index.Eval(variables)),value);}
    }

    internal sealed class CompiledBlock
    {
        internal Expr Expression;
        internal List<Assignment> Assignments = new List<Assignment>();
        internal string InputVariable;
        internal Expr InputIndex;
        internal FlowDeclaration Declaration;internal Expr DeclarationSize;
        internal bool FileReading, FileNewline=true, OutputNewline=true;
        internal FlowTurtle.Spec Turtle;
        internal HashSet<string> Definitions()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(Declaration!=null)result.UnionWith(Declaration.Names);
            if (InputVariable != null && InputIndex==null) result.Add(InputVariable);
            foreach (Assignment a in Assignments) if(a.Index==null)result.Add(a.Name);
            return result;
        }
    }

    internal static class Compiler
    {
        internal static Assignment Target(string text,IDictionary<string,FlowFunction> functions=null){Expr expr=ExpressionLanguage.Parse(text,functions);var variable=expr as VariableExpr;var indexed=expr as IndexExpr;if(variable!=null)return new Assignment{Name=variable.name};if(indexed!=null&&indexed.Target is VariableExpr)return new Assignment{Name=((VariableExpr)indexed.Target).name,Index=indexed.Index};throw new FlowException("Usa un nome di variabile o un elemento del vettore, per esempio valori[i].");}
        internal static int AssignmentAt(string text){int nesting=0;char quote='\0';bool escape=false;for(int i=0;i<text.Length;i++){char c=text[i];if(quote!='\0'){if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote='\0';continue;}if(c=='\''||c=='"'){quote=c;continue;}if(c=='('||c=='[')nesting++;else if(c==')'||c==']')nesting--;if(nesting==0&&(c=='='&&(i+1==text.Length||text[i+1]!='=')||c=='<'&&i+1<text.Length&&text[i+1]=='-'))return i;}throw new FlowException("Usa = o <- per assegnare un valore, per esempio totale = 0.");}
        internal static CompiledBlock Block(FlowNode node,IDictionary<string,FlowFunction> functions=null,FlowFunction owner=null)
        {
            CompiledBlock block = new CompiledBlock();
            string text = (node.Text ?? "").Trim();
            if (node.Kind == NodeKind.Decision || node.Kind == NodeKind.Output || node.Kind==NodeKind.Call)
            {
                if(node.Kind==NodeKind.Output){block.OutputNewline=FlowOutput.Newline(text);text=FlowOutput.Expression(text);}
                block.Expression = ExpressionLanguage.Parse(text,functions);
                if(node.Kind==NodeKind.Call && !(block.Expression is CallExpr)) throw new FlowException("Scrivi una chiamata, per esempio saluta(nome).");
            }
            else if(node.Kind==NodeKind.End && owner!=null && owner.ReturnType!=FlowDataType.Void)
                block.Expression=new VariableExpr(owner.ReturnVariable);
            else if (node.Kind == NodeKind.Input || node.Kind==NodeKind.ReadFile)
            {
                var target=Target(text,functions);block.InputVariable=target.Name;block.InputIndex=target.Index;
            }
            else if(FlowTurtle.IsTurtle(node.Kind)){block.Turtle=FlowTurtle.Parse(node.Kind,text);if(block.Turtle.Expression!=null)block.Expression=ExpressionLanguage.Parse(block.Turtle.Expression,functions);}
            else if(node.Kind==NodeKind.OpenFile){block.Expression=ExpressionLanguage.Parse(FlowTextFiles.OpenExpression(text,out block.FileReading),functions);}
            else if(node.Kind==NodeKind.WriteFile){block.Expression=ExpressionLanguage.Parse(FlowTextFiles.WriteExpression(text,out block.FileNewline),functions);}
            else if(node.Kind==NodeKind.CloseFile){block.FileReading=FlowTextFiles.Channel(text);}
            else if(node.Kind==NodeKind.Pause&&node.Breakpoint){new FlowDebugger.DebugCondition(node.BreakpointCondition);}
            else if(node.Kind==NodeKind.Declare){block.Declaration=FlowDeclaration.Parse(text);if(block.Declaration.IsArray)block.DeclarationSize=ExpressionLanguage.Parse(block.Declaration.SizeText,functions);}
            else if (node.Kind == NodeKind.Process)
            {
                List<string> statements = SplitStatements(text);
                if (statements.Count == 0) throw new FlowException("Scrivi un'assegnazione, per esempio totale = a + b.");
                foreach (string statement in statements)
                {
                    int i=AssignmentAt(statement);var target=Target(statement.Substring(0,i).Trim(),functions);int width=statement[i]=='<'?2:1;target.Expression=ExpressionLanguage.Parse(statement.Substring(i+width),functions);block.Assignments.Add(target);
                }
            }
            if(owner!=null){var references=new HashSet<string>(owner.Parameters.Where(p=>p.ByReference).Select(p=>p.Name),StringComparer.OrdinalIgnoreCase);if(block.Assignments.Any(a=>a.Index==null&&references.Contains(a.Name))||block.InputVariable!=null&&block.InputIndex==null&&references.Contains(block.InputVariable)||block.Declaration!=null&&block.Declaration.Names.Any(references.Contains))throw new FlowException("Modifica gli elementi del parametro per riferimento, per esempio valori[i]. Il vettore non può essere riassegnato o ridichiarato.");}
            ExpressionMachine.CheckVoid(block.Expression,node.Kind==NodeKind.Call);
            ExpressionMachine.CheckVoid(block.InputIndex,false);ExpressionMachine.CheckVoid(block.DeclarationSize,false);
            foreach(var assignment in block.Assignments){ExpressionMachine.CheckVoid(assignment.Expression,false);ExpressionMachine.CheckVoid(assignment.Index,false);}
            return block;
        }

        private static List<string> SplitStatements(string text)
        {
            List<string> statements = new List<string>();
            int start = 0, nesting = 0;
            char quote = '\0';
            bool escape = false;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length)
                {
                    string remaining = text.Substring(start).Trim();
                    if (remaining.Length > 0) statements.Add(remaining);
                    break;
                }
                char c = text[i];
                if (quote != '\0')
                {
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '\'' || c == '"') { quote = c; continue; }
                if (c == '(' || c == '[') nesting++;
                else if (c == ')' || c == ']') nesting--;
                if (nesting == 0 && (c == ';' || c == '\n' || c == '\r'))
                {
                    string statement = text.Substring(start, i - start).Trim();
                    if (statement.Length > 0) statements.Add(statement);
                    start = i + 1;
                    if (statements.Count > 128) throw new FlowException("Massimo 128 assegnazioni per blocco.");
                }
            }
            if (statements.Count > 128) throw new FlowException("Massimo 128 assegnazioni per blocco.");
            return statements;
        }
    }

    public static class Validator
    {
        public static List<ValidationIssue> Validate(Diagram diagram)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            try { DiagramIO.CheckDocument(diagram); }
            catch (Exception ex)
            {
                if (!(ex is InvalidDataException)) throw;
                Add(issues, Severity.Error, null, "document", ex.Message, "Correggi il file oppure apri un diagramma valido.");
                return issues;
            }
            var functions=FunctionModel.Catalog(diagram);
            issues.AddRange(ValidateGraph(diagram,functions,null,diagram.RequiresDeclarations));
            foreach(var function in functions.Values)
            {
                var found=ValidateGraph(function.Body,functions,function,diagram.RequiresDeclarations);
                foreach(var issue in found)issue.FunctionId=function.Id;
                issues.AddRange(found);
            }
            return issues;
        }

        private static List<ValidationIssue> ValidateGraph(Diagram diagram,IDictionary<string,FlowFunction> functions,FlowFunction owner,bool requireDeclarations)
        {
            diagram=FlowAnnotations.Executable(diagram);
            var issues=new List<ValidationIssue>();
            Dictionary<string, FlowNode> nodes = diagram.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            Dictionary<string, List<FlowEdge>> outgoing = nodes.Keys.ToDictionary(k => k, k => new List<FlowEdge>(), StringComparer.Ordinal);
            Dictionary<string, List<FlowEdge>> incoming = nodes.Keys.ToDictionary(k => k, k => new List<FlowEdge>(), StringComparer.Ordinal);
            foreach (FlowEdge edge in diagram.Edges)
            {
                outgoing[edge.From].Add(edge);
                incoming[edge.To].Add(edge);
            }
            List<FlowNode> starts = diagram.Nodes.Where(n => n.Kind == NodeKind.Start).ToList();
            List<FlowNode> ends = diagram.Nodes.Where(n => n.Kind == NodeKind.End).ToList();
            if (starts.Count != 1)
                Add(issues, Severity.Error, starts.Count > 0 ? starts[0].Id : null, "start-count",
                    "Serve esattamente un blocco Inizio; ne hai " + starts.Count + ".", "Aggiungi un solo Inizio ed elimina gli eventuali duplicati.");
            if (ends.Count == 0)
                Add(issues, Severity.Error, null, "end-missing", "Manca il blocco Fine.", "Aggiungi una Fine e collega i percorsi che devono terminare.");
            Dictionary<string, CompiledBlock> compiled = new Dictionary<string, CompiledBlock>(StringComparer.Ordinal);
            foreach (FlowNode node in diagram.Nodes)
            {
                List<FlowEdge> edges = outgoing[node.Id];
                if (node.Kind == NodeKind.Start && incoming[node.Id].Count > 0)
                    Add(issues, Severity.Error, node.Id, "start-incoming", "Un collegamento rientra in Inizio.", "Fai tornare il ciclo al controllo o al processo, senza rientrare in Inizio.");
                if (node.Kind == NodeKind.End)
                {
                    if (edges.Count > 0) Add(issues, Severity.Error, node.Id, "end-outgoing",
                        "Fine non può avere collegamenti in uscita.", "Rimuovi i collegamenti che partono da Fine.");
                }
                else if (node.Kind == NodeKind.Decision)
                {
                    int yes = edges.Count(e => e.Branch == "true"), no = edges.Count(e => e.Branch == "false");
                    if (yes != 1 || no != 1 || edges.Count != 2)
                        Add(issues, Severity.Error, node.Id, "decision-branches",
                            "La decisione richiede un ramo Sì e un ramo No (Sì: " + yes + ", No: " + no + ").",
                            "Collega una sola uscita Sì e una sola uscita No. Possono anche arrivare allo stesso blocco.");
                }
                else if (edges.Count != 1 || edges[0].Branch != "next")
                    Add(issues, Severity.Error, node.Id, "single-outgoing",
                        "Questo blocco richiede esattamente un collegamento normale in uscita.",
                        "Collega il blocco successivo con un solo ramo normale.");
                try { compiled[node.Id] = Compiler.Block(node,functions,owner); }
                catch (FlowException ex)
                {
                    Add(issues, Severity.Error, node.Id, "syntax", ex.Message, SyntaxSuggestion(node.Kind));
                }
            }
            var declared=new HashSet<string>(owner==null?new string[0]:owner.Parameters.Select(p=>p.Name),StringComparer.OrdinalIgnoreCase);
            foreach(var entry in compiled)if(entry.Value.Declaration!=null)foreach(string name in entry.Value.Declaration.Names)if(!declared.Add(name))Add(issues,Severity.Error,entry.Key,"duplicate-declaration","«"+name+"» è già un parametro o una variabile dichiarata.","Usa un solo blocco Dichiara per ciascun nome nella stessa funzione.");
            if (starts.Count == 1)
            {
                HashSet<string> reached = Reach(starts.Select(s => s.Id), outgoing, false);
                foreach (FlowNode node in diagram.Nodes.Where(n => !reached.Contains(n.Id)))
                    Add(issues, Severity.Warning, node.Id, "unreachable", "Questo blocco non è raggiungibile da Inizio.",
                        "Collegalo al percorso oppure rimuovilo se non serve.");
                if (ends.Count > 0)
                {
                    HashSet<string> canEnd = Reach(ends.Select(n => n.Id), incoming, true);
                    foreach (FlowNode node in diagram.Nodes.Where(n => reached.Contains(n.Id) && !canEnd.Contains(n.Id)))
                        Add(issues, Severity.Error, node.Id, "no-end-path", "Da questo blocco non esiste alcun percorso verso Fine.",
                            "Aggiungi un'uscita dal ciclo o collega il percorso a Fine.");
                }
                CheckDefiniteAssignments(starts[0].Id, reached, incoming, compiled, nodes, issues,owner==null?new string[0]:owner.Parameters.Select(p=>p.Name),requireDeclarations,owner==null?null:owner.ReturnVariable);
                CheckCycles(reached, outgoing, issues);
            }
            return issues;
        }

        private static string SyntaxSuggestion(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Process: return "Esempio: somma = a + b. Per più istruzioni usa una riga per assegnazione.";
                case NodeKind.Decision: return "Esempio: numero % 2 = 0 oppure eta >= 18 e biglietto = vero.";
                case NodeKind.Input: return "Scrivi il nome della variabile, per esempio numero. La domanda va nel campo Domanda.";
                case NodeKind.Output: return "Esempio: \"Risultato: \" + totale. I testi fissi vanno tra virgolette.";
                default: return "Controlla il contenuto del blocco.";
            }
        }

        private static void Add(List<ValidationIssue> issues, Severity severity, string nodeId, string code, string message, string suggestion)
        {
            issues.Add(new ValidationIssue { Severity = severity, NodeId = nodeId, Code = code, Message = message, Suggestion = suggestion });
        }

        private static HashSet<string> Reach(IEnumerable<string> seeds, Dictionary<string, List<FlowEdge>> graph, bool reverse)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();
            foreach (string seed in seeds) if (visited.Add(seed)) queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (FlowEdge edge in graph[current])
                {
                    string next = reverse ? edge.From : edge.To;
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited;
        }

        private static void CheckDefiniteAssignments(string start, HashSet<string> reached,
            Dictionary<string, List<FlowEdge>> incoming, Dictionary<string, CompiledBlock> compiled,
            Dictionary<string, FlowNode> nodes, List<ValidationIssue> issues,IEnumerable<string> parameterNames,bool requireDeclarations,string resultName)
        {
            var declared=new HashSet<string>(compiled.Values.Where(b=>b.Declaration!=null).SelectMany(b=>b.Declaration.Names),StringComparer.OrdinalIgnoreCase);
            var parameters=new HashSet<string>(parameterNames,StringComparer.OrdinalIgnoreCase);
            HashSet<string> required=null;
            if(requireDeclarations){required=new HashSet<string>(declared,StringComparer.OrdinalIgnoreCase);required.UnionWith(parameters);if(!String.IsNullOrEmpty(resultName))required.Add(resultName);
                // PER snapshot values are compiler-owned, proven by the intact graph pattern,
                // never by their names alone. The user's counter still needs a declaration.
                var forGraph=new Diagram{Nodes=nodes.Values.ToList(),Edges=incoming.Values.SelectMany(e=>e).ToList()};foreach(var node in nodes.Values.Where(n=>n.Kind==NodeKind.Process&&n.Text!=null&&n.Text.IndexOf("limite_ciclo_",StringComparison.OrdinalIgnoreCase)>=0)){var pattern=FlowPseudocode.InspectFor(forGraph,node);if(pattern!=null)required.UnionWith(pattern.HelperNames);}
            }
            HashSet<string> universe = new HashSet<string>(parameters,StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> definitions = new Dictionary<string, HashSet<string>>();
            foreach (string id in reached)
            {
                CompiledBlock block;
                HashSet<string> defs = compiled.TryGetValue(id, out block) ? block.Definitions() : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if(block==null||block.Declaration==null)defs.ExceptWith(declared);
                definitions[id] = defs;
                universe.UnionWith(defs);
            }
            if (universe.Count > 4096)
            {
                Add(issues, Severity.Error, null, "variable-limit", "Troppe variabili: massimo 4096.", "Semplifica il diagramma.");
                return;
            }
            Dictionary<string, HashSet<string>> before = new Dictionary<string, HashSet<string>>();
            Dictionary<string, HashSet<string>> after = new Dictionary<string, HashSet<string>>();
            foreach (string id in reached)
            {
                before[id] = new HashSet<string>(id == start ? parameters : universe, StringComparer.OrdinalIgnoreCase);
                after[id] = new HashSet<string>(before[id], StringComparer.OrdinalIgnoreCase);
                after[id].UnionWith(definitions[id]);
            }
            bool changed;
            do
            {
                changed = false;
                foreach (string id in reached)
                {
                    HashSet<string> nextIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if(id==start)nextIn.UnionWith(parameters);
                    else
                    {
                        bool first = true;
                        foreach (FlowEdge edge in incoming[id])
                        {
                            if (!reached.Contains(edge.From)) continue;
                            if (first) { nextIn.UnionWith(after[edge.From]); first = false; }
                            else nextIn.IntersectWith(after[edge.From]);
                        }
                    }
                    HashSet<string> nextOut = new HashSet<string>(nextIn, StringComparer.OrdinalIgnoreCase);
                    nextOut.UnionWith(definitions[id]);
                    if (!before[id].SetEquals(nextIn) || !after[id].SetEquals(nextOut))
                    { before[id] = nextIn; after[id] = nextOut; changed = true; }
                }
            } while (changed);
            foreach (string id in reached)
            {
                CompiledBlock block;
                if (!compiled.TryGetValue(id, out block)) continue;
                HashSet<string> available = new HashSet<string>(before[id], StringComparer.OrdinalIgnoreCase);
                HashSet<string> reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if(block.DeclarationSize!=null)CheckReads(block.DeclarationSize,available,reported,id,issues,required);
                if(block.InputVariable!=null)CheckDeclaredTarget(block.InputVariable,required,reported,id,issues);
                if(block.InputVariable!=null&&declared.Contains(block.InputVariable))CheckReads(new VariableExpr(block.InputVariable),available,reported,id,issues,required);
                if (block.Expression != null) CheckReads(block.Expression, available, reported, id, issues,required);
                if(block.InputIndex!=null)CheckReads(new IndexExpr(new VariableExpr(block.InputVariable),block.InputIndex),available,reported,id,issues,required);
                foreach (Assignment assignment in block.Assignments)
                {
                    CheckDeclaredTarget(assignment.Name,required,reported,id,issues);
                    if(declared.Contains(assignment.Name))CheckReads(new VariableExpr(assignment.Name),available,reported,id,issues,required);
                    CheckReads(assignment.Expression, available, reported, id, issues,required);
                    if(assignment.Index!=null)CheckReads(assignment.TargetRead(),available,reported,id,issues,required);else if(!declared.Contains(assignment.Name))available.Add(assignment.Name);
                }
            }
        }

        private static void CheckReads(Expr expr, HashSet<string> available, HashSet<string> reported, string id, List<ValidationIssue> issues,HashSet<string> required)
        {
            HashSet<string> reads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            expr.ReadVariables(reads);
            foreach (string name in reads){
                if(required!=null&&!required.Contains(name)){CheckDeclaredTarget(name,required,reported,id,issues);continue;}
                if (!available.Contains(name) && reported.Add(name))
                    Add(issues, Severity.Error, id, "uninitialized",
                        "La variabile «" + name + "» potrebbe essere letta prima di ricevere un valore.",
                        required==null?"Inserisci un Ingresso o un'assegnazione prima di questo blocco, in tutti i rami che ci arrivano.":"Metti Dichiara prima dell’uso, in tutti i percorsi che arrivano a questo blocco; poi assegna il valore richiesto.");
            }
        }

        private static void CheckDeclaredTarget(string name,HashSet<string> required,HashSet<string> reported,string id,List<ValidationIssue> issues){
            if(required!=null&&!required.Contains(name)&&reported.Add(name))Add(issues,Severity.Error,id,"undeclared","La variabile «"+name+"» non è dichiarata.","Inserisci Dichiara per «"+name+"» e scegli il tipo prima di usarla. Se il nome è scritto per errore, correggi questo blocco.");
        }

        private static void CheckCycles(HashSet<string> reached, Dictionary<string, List<FlowEdge>> graph, List<ValidationIssue> issues)
        {
            Dictionary<string, int> index = new Dictionary<string, int>(), low = new Dictionary<string, int>();
            HashSet<string> onStack = new HashSet<string>();
            Stack<string> stack = new Stack<string>();
            int next = 0;
            Action<string> visit = null;
            visit = delegate(string current)
            {
                index[current] = low[current] = next++;
                stack.Push(current); onStack.Add(current);
                foreach (FlowEdge edge in graph[current])
                {
                    string child = edge.To;
                    if (!reached.Contains(child)) continue;
                    if (!index.ContainsKey(child)) { visit(child); low[current] = Math.Min(low[current], low[child]); }
                    else if (onStack.Contains(child)) low[current] = Math.Min(low[current], index[child]);
                }
                if (low[current] == index[current])
                {
                    List<string> component = new List<string>();
                    string item;
                    do { item = stack.Pop(); onStack.Remove(item); component.Add(item); } while (item != current);
                    if (component.Count > 1 || graph[current].Any(e => e.To == current))
                        Add(issues, Severity.Warning, current, "loop",
                            "È presente un ciclo. La verifica strutturale non dimostra che termini.",
                            "Controlla che la condizione possa diventare falsa e prova valori limite. L'esecuzione si ferma dopo 10000 blocchi.");
                }
            };
            foreach (string id in reached) if (!index.ContainsKey(id)) visit(id);
        }
    }

}
