using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Frociart
{
    // Offline translation. Only the existing expression parser handles block contents.
    internal static class FlowPseudocode
    {
        internal const int MaxText=262144, MaxLines=5000, MaxDepth=48;
        internal const string Example="INIZIO\nLEGGI numero\nSE numero >= 0 ALLORA\n    STAMPA \"Positivo o zero\"\nALTRIMENTI\n    STAMPA \"Negativo\"\nFINE SE\nFINE\n";
        internal const string VectorExample="INIZIO\nvalori = [10, 20, 30]\nPER i DA 0 A len(valori) - 1 FAI\n    STAMPA valori[i]\nFINE PER\nSTAMPA \"Totale: \" + somma(valori)\nFINE\n";
        internal const string Help="Una istruzione per riga. Maiuscole e rientri sono liberi.\n\nINIZIO / FINE\nLEGGI numero\nSTAMPA \"Risultato: \" + numero\ntotale <- totale + numero\n\nSE condizione ALLORA\n    ...\nALTRIMENTI\n    ...\nFINE SE\n\nMENTRE condizione FAI\n    ...\nFINE MENTRE\n\nRIPETI\n    ...\nFINCHÉ condizione\n\nSono accettati anche IF/THEN/ELSE/END IF, WHILE/DO/END WHILE, REPEAT/UNTIL, READ/WRITE e BEGIN/END. SCRIVI resta accettato come sinonimo di STAMPA.\nSTAMPA_CONTINUA espressione continua sulla stessa riga; STAMPA conclude la riga.\nPAUSA ferma la simulazione; PAUSA SE contatore >= 5 ferma solo quando è vero. Riprendi continua con gli stessi valori. PAUSA_DISATTIVATA conserva un punto disattivato. Le esportazioni del codice attraversano le pause senza fermarsi.\nUsa =, <- o ← per assegnare; e, o, non per la logica. Commenti liberi: // testo. Per conservare una nota su un blocco, scrivi NOTA \"spiegazione\" immediatamente prima della sua istruzione (anche INIZIO o FERMA).\n\nPer percorsi complessi: etichetta: e VAI A etichetta. FERMA termina un percorso; SCOLLEGATO conserva un'uscita libera.\nLEGGI numero, \"Quanto vale?\" conserva la domanda. ESEGUI raggruppa più assegnazioni separate da ;.\n\nLa conversione crea un nuovo progetto. Posizioni, colori e nomi personalizzati dei rami non sono trasferiti: usa .frociart per conservare l'aspetto. Vettori: valori = [10, 20, 30], valori[i] = 5, LEGGI valori[i]. Indici da 0; elementi dello stesso tipo. vettore(5) crea cinque zeri; len, somma, min e max leggono un vettore.\n\nPER i DA 0 A len(valori) - 1 PASSO 1 FAI\n    STAMPA valori[i]\nFINE PER\nAnche FOR i = 1 TO 5 STEP 1 DO / END FOR. PASSO può essere un numero intero diverso da zero oppure una variabile o espressione che restituisce un intero da -2147483648 a 2147483647. Un passo negativo conta all’indietro. Inizio, limite e passo vengono valutati in questo ordine una sola volta a ogni ingresso nel ciclo. Il limite e il passo calcolato restano visibili come limite_ciclo_N e passo_ciclo_N. I blocchi sono modificabili: il pseudocodice mostra PER finché la struttura resta riconoscibile; altrimenti conserva le istruzioni effettive.\n\nFUNZIONE doppio(valore: REALE) -> REALE risultato\n    risultato = valore * 2\nFINE FUNZIONE\n\nSTAMPA doppio(3)\n\nI tipi sono VALORE (automatico), INTERO, REALE, TESTO e LOGICO. Aggiungi [] per un vettore. Usa RIF prima del tipo per modificare il vettore originale, per esempio FUNZIONE aggiorna(valori: RIF INTERO[]). Senza RIF il parametro resta una copia. Nei riferimenti puoi cambiare gli elementi valori[i], con dimensione e tipo originali. Il risultato è la variabile indicata dopo il tipo; se omessa si chiama risultato. Senza -> la funzione non restituisce un valore: usa CHIAMA saluta(nome). Le funzioni hanno variabili locali e possono richiamarsi, anche ricorsivamente. Definiscile fuori da INIZIO / FINE del programma principale. Anche FUNCTION / END FUNCTION e CALL sono accettati.\n\nDICHIARA totale, resto: INTERO\nDICHIARA prezzi: REALE[10]\nTipi: INTERO, REALE, TESTO, LOGICO (anche INTEGER, REAL, STRING, BOOLEAN). Gli interi scartano la parte decimale verso zero; valori iniziali 0, testo vuoto o falso. I vettori hanno dimensione fissa da 0 a 1024. La dichiarazione deve precedere ogni uso. Anche DECLARE è accettato. Le vecchie variabili automatiche restano disponibili." + "\n\nVARIABILI DICHIARATE, prima di funzioni e programma, richiede Dichiara per ogni variabile. Parametri e risultato sono definiti dalla firma. Dichiara anche il contatore PER; limite e passo ausiliari sono gestiti dal ciclo. Puoi scegliere questa modalità anche in Progetto → Variabili. I progetti importati da Flowgorithm mantengono le dichiarazioni obbligatorie." + FlowTurtle.Syntax;
        internal sealed class ParseError : ArgumentException
        {
            internal readonly int Line;
            internal ParseError(int line,string detail):base("Riga "+line+": "+detail){Line=line;}
        }
        private sealed class Line {internal string Text,Note;internal int Number;internal Line(string t,int n){Text=t;Number=n;}}
        private sealed class Port {internal FlowNode Node;internal string Branch;internal Port(FlowNode n,string b){Node=n;Branch=b;}}
        private sealed class Part {internal string Entry;internal List<Port> Exits=new List<Port>();internal Part(string e){Entry=e;}}
        private sealed class Jump {internal Part Part;internal string Label;internal int Line;internal Jump(Part p,string l,int n){Part=p;Label=l;Line=n;}}
        private sealed class Label {internal string Name;internal Part Part;internal int Line;internal Label(string n,Part p,int l){Name=n;Part=p;Line=l;}}
        private sealed class FunctionSource {internal FlowFunction Function;internal int Line;internal readonly List<Line> Body=new List<Line>();}
        internal const string FunctionExample="FUNZIONE doppio(valore: REALE) -> REALE risultato\nINIZIO\n    risultato = valore * 2\nFINE\nFINE FUNZIONE\n\nINIZIO\nLEGGI numero\nSTAMPA doppio(numero)\nFINE\n";
        internal static Diagram Decode(string source){
            var lines=SourceLines(source);var main=new List<Line>();var parts=new List<FunctionSource>();var definitions=new List<FlowFunction>();
            FunctionSource active=null;int depth=0;bool inMain=false,requireDeclarations=false;
            foreach(var line in lines){
                string u=Upper(line.Text);
                if(One(u,"VARIABILI DICHIARATE","REQUIRE DECLARATIONS")){if(requireDeclarations||active!=null||depth>0||inMain||main.Count>0||definitions.Count>0||line.Note!=null)throw new ParseError(line.Number,"Metti VARIABILI DICHIARATE una sola volta, prima di funzioni e programma.");requireDeclarations=true;continue;}
                if(line.Note!=null&&(Prefix(u,"FUNZIONE ","FUNCTION ")||One(u,"FINE FUNZIONE","FINEFUNZIONE","END FUNCTION","ENDFUNCTION")))throw new ParseError(line.Number,"Metti NOTA prima di un blocco, per esempio INIZIO, non prima della firma della funzione.");
                if(Prefix(u,"FUNZIONE ","FUNCTION ")){
                    if(active!=null||depth>0||inMain)throw new ParseError(line.Number,"Definisci le funzioni fuori dal programma principale e dalle altre funzioni.");
                    active=new FunctionSource{Function=ReadSignature(line),Line=line.Number};parts.Add(active);definitions.Add(active.Function);
                    try{FunctionModel.CheckProject(new Diagram{Version=FunctionModel.ProjectVersion(definitions),Functions=definitions});}catch(System.IO.InvalidDataException e){throw new ParseError(line.Number,e.Message);}continue;
                }
                if(One(u,"FINE FUNZIONE","FINEFUNZIONE","END FUNCTION","ENDFUNCTION")){
                    if(active==null)throw new ParseError(line.Number,"FINE FUNZIONE senza una funzione aperta.");active=null;continue;
                }
                if(active!=null){active.Body.Add(line);continue;}
                if(One(u,"INIZIO","BEGIN"))inMain=true;
                else if(One(u,"FINE","END"))inMain=false;
                if(Prefix(u,"SE ","IF ","MENTRE ","WHILE ","PER ","FOR ")||One(u,"RIPETI","REPEAT"))depth++;
                else if(One(u,"FINE SE","FINESE","END IF","ENDIF","FINE MENTRE","FINEMENTRE","END WHILE","ENDWHILE","FINE PER","FINEPER","END FOR","ENDFOR")||Prefix(u,"FINCHÉ ","FINCHE ","UNTIL "))depth=Math.Max(0,depth-1);
                main.Add(line);
            }
            if(active!=null)throw new ParseError(active.Line,"Manca FINE FUNZIONE.");
            var shell=new Diagram{Version=FunctionModel.ProjectVersion(definitions),Functions=definitions.Count==0?null:definitions};var catalog=FunctionModel.Catalog(shell);int used=0;
            foreach(var part in parts){part.Function.Body=new Reader(part.Body,catalog,part.Function,DiagramIO.MaxNodes-used,part.Line).Read();part.Function.Body.Title=part.Function.Name;used+=part.Function.Body.Nodes.Count;}
            var result=new Reader(main,catalog,null,DiagramIO.MaxNodes-used,1).Read();result.Functions=shell.Functions;result.VariablePolicy=requireDeclarations?"declared":null;FlowAnnotations.Promote(result);
            try{DiagramIO.CheckDocument(result);}catch(System.IO.InvalidDataException e){throw new ParseError(lines[lines.Count-1].Number,e.Message);}return result;
        }
        internal static Diagram DecodeFragment(string source,Diagram project,FlowFunction owner,IEnumerable<string> reserved){
            var reader=new Reader(SourceLines(source),FunctionModel.Catalog(project),owner,DiagramIO.MaxNodes,1);
            foreach(string name in reserved)reader.Reserve(name);
            return reader.Read();
        }
        private static List<Line> SourceLines(string source){
            if(source==null||source.Length>MaxText)throw new ParseError(1,"Massimo 256 Ki caratteri.");
            source=source.Replace("\r\n","\n").Replace('\r','\n');if(source.StartsWith("\uFEFF",StringComparison.Ordinal))source=source.Substring(1);
            var raw=source.Split('\n');if(raw.Length>MaxLines)throw new ParseError(MaxLines,"Troppe righe (massimo 5000).");
            string note=null;var result=new List<Line>();for(int i=0;i<raw.Length;i++){string value=Comment(raw[i]).Trim();if(value.Length==0)continue;if(Prefix(Upper(value),"NOTA ","NOTE ")){string literal=value.Substring(5).Trim();if(!NoteLiteral(literal))throw new ParseError(i+1,"Usa NOTA seguito da un testo fra virgolette.");string text=Quoted(literal,i+1);note=note==null?text:note+"\n"+text;if(note.Length>2048)throw new ParseError(i+1,"Il commento supera 2048 caratteri.");continue;}result.Add(new Line(value,i+1){Note=note});note=null;}if(note!=null)throw new ParseError(raw.Length,"Dopo NOTA inserisci un blocco.");
            if(result.Count==0)throw new ParseError(1,"Scrivi o incolla il tuo pseudocodice.");return result;
        }
        private static FlowFunction ReadSignature(Line line){
            var m=Regex.Match(line.Text,@"\A(?:FUNZIONE|FUNCTION)\s+([^\s(]+)\s*\(([^()]*)\)\s*(?:->\s*(.+))?\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            if(!m.Success)throw new ParseError(line.Number,"Usa FUNZIONE nome(valore: REALE) -> REALE risultato. Ometti la freccia se la funzione non restituisce un valore.");
            var f=new FlowFunction{Name=m.Groups[1].Value};string parameters=m.Groups[2].Value.Trim();
            if(parameters.Length>0)foreach(var raw in parameters.Split(',')){
                var pair=raw.Split(':');if(pair.Length>2||pair[0].Trim().Length==0)throw new ParseError(line.Number,"Parametro non valido: usa nome: TIPO.");
                string spec=pair.Length==1?"VALORE":pair[1].Trim();var reference=Regex.Match(spec,@"\A(?:RIF|REF)\s+",RegexOptions.IgnoreCase);bool byReference=reference.Success;if(byReference)spec=spec.Substring(reference.Length).Trim();bool array;FlowDataType type=ReadType(spec,line.Number,out array);
                f.Parameters.Add(new FlowParameter{Name=pair[0].Trim(),Type=type,IsArray=array,ByReference=byReference});
            }
            if(m.Groups[3].Success){
                var ret=Regex.Match(m.Groups[3].Value.Trim(),@"\A([^\s]+)(?:\s+([^\s]+))?\z");if(!ret.Success)throw new ParseError(line.Number,"Dopo -> usa TIPO nomeRisultato.");
                bool array;f.ReturnType=ReadType(ret.Groups[1].Value,line.Number,out array);f.ReturnIsArray=array;
                f.ReturnVariable=ret.Groups[2].Success?ret.Groups[2].Value:f.ReturnType==FlowDataType.Void?"":"risultato";
            }return f;
        }
        private static FlowDataType ReadType(string text,int line,out bool array){
            text=text.Trim().ToUpperInvariant();array=text.EndsWith("[]",StringComparison.Ordinal);if(array)text=text.Substring(0,text.Length-2);
            switch(text){case "VALORE":case "AUTO":case "ANY":return FlowDataType.Auto;case "INTERO":case "INTEGER":return FlowDataType.Integer;case "REALE":case "REAL":return FlowDataType.Real;case "TESTO":case "STRING":case "TEXT":return FlowDataType.Text;case "LOGICO":case "BOOLEAN":case "BOOL":return FlowDataType.Boolean;case "NESSUNO":case "VOID":return FlowDataType.Void;default:throw new ParseError(line,"Tipo sconosciuto. Usa VALORE, INTERO, REALE, TESTO o LOGICO; aggiungi [] per un vettore.");}
        }
        private static string TypeCode(FlowDataType type,bool array){return new[]{"VALORE","INTERO","REALE","TESTO","LOGICO","NESSUNO"}[(int)type]+(array?"[]":"");}
        internal static string Signature(FlowFunction f){return "FUNZIONE "+f.Name+"("+String.Join(", ",f.Parameters.Select(p=>p.Name+": "+(p.ByReference?"RIF ":"")+TypeCode(p.Type,p.IsArray)))+")"+(f.ReturnType==FlowDataType.Void?"":" -> "+TypeCode(f.ReturnType,f.ReturnIsArray)+" "+f.ReturnVariable);}
        private sealed class Reader
        {
            private readonly List<Line> lines;private readonly Diagram doc=new Diagram();private readonly IDictionary<string,FlowFunction> functions;private readonly FlowFunction owner;private readonly int maxNodes;
            private readonly HashSet<int> notesUsed=new HashSet<int>();
            private readonly List<Jump> jumps=new List<Jump>();private readonly List<Label> labels=new List<Label>();private int index,forCount;private readonly HashSet<string> identifiers=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            internal Reader(List<Line> source,IDictionary<string,FlowFunction> functions,FlowFunction owner,int maxNodes,int fallbackLine){
                this.lines=source;this.functions=functions;this.owner=owner;this.maxNodes=maxNodes;
                if(lines.Count==0){lines.Add(new Line("INIZIO",fallbackLine));lines.Add(new Line("FINE",fallbackLine));}
                if(owner!=null){foreach(var parameter in owner.Parameters)identifiers.Add(parameter.Name);identifiers.Add(owner.ReturnVariable);}
                foreach(var line in lines)foreach(Match name in Regex.Matches(line.Text,ExpressionLanguage.IdentifierWordPattern))identifiers.Add(name.Value);
            }
            internal void Reserve(string name){identifiers.Add(name);}
            internal Diagram Read(){
                bool begin=index<lines.Count&&One(lines[index].Text,"INIZIO","BEGIN");if(begin)index++;
                FlowNode start=Node(NodeKind.Start,"Inizio",begin?lines[0].Number:-1);Part body=Sequence(0);
                int endLine=index<lines.Count?lines[index].Number:lines[lines.Count-1].Number;
                if(index<lines.Count&&One(lines[index].Text,"FINE","END"))index++;
                if(index!=lines.Count)throw new ParseError(lines[index].Number,"Chiusura o istruzione fuori posto.");
                if(body.Entry==null||body.Exits.Count>0||!doc.Nodes.Any(n=>n.Kind==NodeKind.End)){FlowNode end=Node(NodeKind.End,"Fine",endLine);Finish(body,end.Id);}Connect(new Port(start,"next"),body.Entry);
                var targets=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach(Label label in labels){if(targets.ContainsKey(label.Name))throw new ParseError(label.Line,"Etichetta duplicata: "+label.Name);targets.Add(label.Name,label.Part.Entry);}
                var aliases=new Dictionary<string,string>();foreach(Jump jump in jumps){string target;if(!targets.TryGetValue(jump.Label,out target))throw new ParseError(jump.Line,"Etichetta sconosciuta: "+jump.Label);aliases.Add(jump.Part.Entry,target);}
                foreach(FlowEdge edge in doc.Edges)edge.To=Resolve(edge.To,aliases,endLine);
                foreach(var line in lines)if(line.Note!=null&&!notesUsed.Contains(line.Number))throw new ParseError(line.Number,"Metti NOTA immediatamente prima di un blocco.");
                doc.Edges.RemoveAll(e=>e.To=="?");doc.Title="Da pseudocodice";FlowAnnotations.Promote(doc);FlowAnnotations.PlaceImported(doc);if(owner==null)DiagramIO.CheckDocument(doc);else DiagramIO.CheckGraph(doc);return doc;
            }
            private string Resolve(string id,Dictionary<string,string> aliases,int line){var seen=new HashSet<string>();while(id.StartsWith("@",StringComparison.Ordinal)){if(!seen.Add(id))throw new ParseError(line,"Ciclo di soli VAI A: aggiungi almeno un blocco al ciclo.");string next;if(!aliases.TryGetValue(id,out next))throw new ParseError(line,"Destinazione mancante.");id=next;}return id;}
            private FlowNode Node(NodeKind kind,string text,int line){
                if(doc.Nodes.Count>=maxNodes)throw new ParseError(line,"Massimo 500 blocchi nell'intero progetto.");if(text.Length>8192)throw new ParseError(line,"Un blocco supera 8192 caratteri.");
                var annotation=lines.FirstOrDefault(x=>x.Number==line);
                var n=new FlowNode{Comment=annotation!=null&&notesUsed.Add(line)?annotation.Note:null,Kind=kind,Text=text,X=350,Y=80+doc.Nodes.Count*150};doc.Nodes.Add(n);
                try{Compiler.Block(n,functions,owner);}catch(FlowException ex){throw new ParseError(line,ex.Message);}return n;
            }
            private void Connect(Port p,string target){doc.Edges.Add(new FlowEdge{From=p.Node.Id,To=target,Branch=p.Branch});}
            private void Finish(Part p,string target){if(p.Entry==null)p.Entry=target;foreach(Port exit in p.Exits)Connect(exit,target);p.Exits.Clear();}
            private Part Sequence(int depth){
                if(depth>MaxDepth)throw new ParseError(index<lines.Count?lines[index].Number:1,"Troppi livelli annidati (massimo 48).");
                Part result=new Part(null);var pending=new List<Label>();
                while(index<lines.Count){Line line=lines[index];string u=Upper(line.Text);
                    if(One(u,"ALTRIMENTI","ELSE","FINE SE","FINESE","END IF","ENDIF","FINE MENTRE","FINEMENTRE","END WHILE","ENDWHILE","FINE PER","FINEPER","END FOR","ENDFOR","FINE","END")||Prefix(u,"FINCHÉ ","FINCHE ","UNTIL "))break;
                    if(Regex.IsMatch(line.Text,@"\A"+ExpressionLanguage.IdentifierPattern+@":\z")){var label=new Label(line.Text.Substring(0,line.Text.Length-1),null,line.Number);pending.Add(label);labels.Add(label);index++;continue;}
                    if(Prefix(u,"ANNOTAZIONE ","ANNOTATION ")){string literal=line.Text.Substring(line.Text.IndexOf(' ')+1).Trim();if(!NoteLiteral(literal))throw new ParseError(line.Number,"Usa ANNOTAZIONE seguito da un testo fra virgolette.");Node(NodeKind.Note,AnnotationText(literal,line.Number),line.Number);index++;continue;}
                    Part next=Statement(depth);foreach(Label label in pending)label.Part=next;pending.Clear();
                    if(result.Entry==null)result.Entry=next.Entry;foreach(Port p in result.Exits)Connect(p,next.Entry);result.Exits=new List<Port>(next.Exits);
                }
                if(pending.Count>0)throw new ParseError(pending[0].Line,"Dopo l'etichetta inserisci un'istruzione (FERMA per terminare).");return result;
            }
            private Part Statement(int depth){
                Line line=lines[index++];string s=line.Text,u=Upper(s);Part part;
                if(Prefix(u,"SE ","IF ")){
                    FlowNode n=Node(NodeKind.Decision,Condition(s,3,"ALLORA","THEN"),line.Number);Part yes=Sequence(depth+1),no=new Part(null);
                    if(index<lines.Count&&One(lines[index].Text,"ALTRIMENTI","ELSE")){index++;no=Sequence(depth+1);}Require("FINE SE",line.Number,"FINE SE","FINESE","END IF","ENDIF");
                    part=new Part(n.Id);Arm(part,n,"true",yes);Arm(part,n,"false",no);return part;
                }
                if(Prefix(u,"MENTRE ","WHILE ")){
                    FlowNode n=Node(NodeKind.Decision,Condition(s,u.StartsWith("MENTRE ",StringComparison.Ordinal)?7:6,"FAI","DO"),line.Number);Part body=Sequence(depth+1);
                    Require("FINE MENTRE",line.Number,"FINE MENTRE","FINEMENTRE","END WHILE","ENDWHILE");Finish(body,n.Id);Connect(new Port(n,"true"),body.Entry);part=new Part(n.Id);part.Exits.Add(new Port(n,"false"));return part;
                }
                if(Prefix(u,"PER ","FOR ")){
                    bool italian=u.StartsWith("PER ",StringComparison.Ordinal);string header=Condition(s,4,"FAI","DO");
                    Match m=Regex.Match(header,italian?@"^("+ExpressionLanguage.IdentifierPattern+@")\s+DA\s+(.+)$":@"^("+ExpressionLanguage.IdentifierPattern+@")\s*=\s*(.+)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
                    if(!m.Success)throw new ParseError(line.Number,"Usa PER i DA 1 A 5 PASSO 1 FAI.");
                    string name=m.Groups[1].Value,rest=m.Groups[2].Value;int split=TopToken(rest,italian?"A":"TO");if(split<0)throw new ParseError(line.Number,"Manca il limite: usa DA 1 A 5 oppure = 1 TO 5.");
                    string from=rest.Substring(0,split).Trim(),to=rest.Substring(split+(italian?1:2)).Trim();int stepAt=TopToken(to,italian?"PASSO":"STEP");string stepText=stepAt<0?"1":to.Substring(stepAt+(italian?5:4)).Trim();if(stepAt>=0)to=to.Substring(0,stepAt).Trim();int step;bool constant=Int32.TryParse(stepText,System.Globalization.NumberStyles.AllowLeadingSign,System.Globalization.CultureInfo.InvariantCulture,out step);
                    if((constant&&step==0)||(!constant&&Regex.IsMatch(stepText,@"\A[+-]?[0-9.]+\z")))throw new ParseError(line.Number,"PASSO deve essere un intero diverso da zero, da -2147483648 a 2147483647.");
                    try{ExpressionLanguage.Parse(from,functions);ExpressionLanguage.Parse(to,functions);ExpressionLanguage.Parse(stepText,functions);}catch(FlowException ex){throw new ParseError(line.Number,ex.Message);}
                    string limit,stride;do{forCount++;limit="limite_ciclo_"+forCount;stride="passo_ciclo_"+forCount;}while(identifiers.Contains(limit)||identifiers.Contains(stride));identifiers.Add(limit);identifiers.Add(stride);
                    FlowNode init=Node(NodeKind.Process,name+" = "+from+"; "+limit+" = "+to+(constant?"":"; "+stride+" = int("+stepText+", \"passo\")"),line.Number),check=Node(NodeKind.Decision,constant?name+(step>0?" <= ":" >= ")+limit:ForCondition(name,limit,stride),line.Number);
                    Part body=Sequence(depth+1);Require("FINE PER",line.Number,"FINE PER","FINEPER","END FOR","ENDFOR");FlowNode increment=Node(NodeKind.Process,constant?name+" = "+name+" + ("+step+")":name+" = "+name+" + "+stride,line.Number);Finish(body,increment.Id);Connect(new Port(init,"next"),check.Id);Connect(new Port(check,"true"),body.Entry);Connect(new Port(increment,"next"),check.Id);part=new Part(init.Id);part.Exits.Add(new Port(check,"false"));return part;
                }
                if(One(u,"RIPETI","REPEAT")){
                    Part body=Sequence(depth+1);if(index>=lines.Count||!Prefix(Upper(lines[index].Text),"FINCHÉ ","FINCHE ","UNTIL "))throw new ParseError(line.Number,"RIPETI richiede FINCHÉ condizione.");
                    Line close=lines[index++];FlowNode n=Node(NodeKind.Decision,close.Text.Substring(close.Text.IndexOf(' ')+1).Trim(),close.Number);Finish(body,n.Id);Connect(new Port(n,"false"),body.Entry);part=new Part(body.Entry);part.Exits.Add(new Port(n,"true"));return part;
                }
                if(Prefix(u,"VAI A ","GOTO ")){string name=s.Substring(u.StartsWith("VAI A ",StringComparison.Ordinal)?6:5).Trim();if(!Regex.IsMatch(name,@"\A"+ExpressionLanguage.IdentifierPattern+@"\z"))throw new ParseError(line.Number,"Dopo VAI A scrivi un'etichetta.");part=new Part("@jump"+jumps.Count);jumps.Add(new Jump(part,name,line.Number));return part;}
                if(One(u,"SCOLLEGATO"))return new Part("?");if(One(u,"FERMA","STOP"))return new Part(Node(NodeKind.End,"Fine",line.Number).Id);
                if(One(u,"PAUSA","PAUSA_DISATTIVATA")||Prefix(u,"PAUSA SE ","PAUSA_DISATTIVATA SE ")){
                    bool enabled=!u.StartsWith("PAUSA_DISATTIVATA",StringComparison.Ordinal);int offset=enabled?6:18;string condition=s.Length<=offset?"":s.Substring(offset+3).Trim();
                    if(condition.Length>8192)throw new ParseError(line.Number,"La condizione supera 8192 caratteri.");
                    try{if(enabled)new FlowDebugger.DebugCondition(condition);}catch(FlowException ex){throw new ParseError(line.Number,ex.Message);}
                    FlowNode pause=Node(NodeKind.Pause,"Pausa",line.Number);pause.Breakpoint=enabled;pause.BreakpointCondition=condition;part=new Part(pause.Id);part.Exits.Add(new Port(pause,"next"));return part;
                }
                NodeKind kind=NodeKind.Process;string content=s,prompt="";
                if(Prefix(u,"AVANZA ","FORWARD ")){kind=NodeKind.TurtleForward;content=s.Substring(s.IndexOf(' ')+1).Trim();}
                else if(Prefix(u,"RUOTA ","TURN ")){kind=NodeKind.TurtleTurn;content=s.Substring(s.IndexOf(' ')+1).Trim();}
                else if(Prefix(u,"PENNA ","PEN ")){kind=NodeKind.TurtlePen;content=s.Substring(s.IndexOf(' ')+1).Trim();}
                else if(One(u,"ORIGINE_TARTARUGA","HOME_TURTLE")){kind=NodeKind.TurtleHome;content=FlowTurtle.DefaultText(kind);}
                else if(One(u,"PULISCI_DISEGNO","CLEAR_DRAWING")){kind=NodeKind.TurtleClear;content=FlowTurtle.DefaultText(kind);}
                else if(Prefix(u,"APRI_FILE ","OPEN_FILE ")){kind=NodeKind.OpenFile;content=s.Substring(s.IndexOf(' ')+1).Trim();}
                else if(Prefix(u,"LEGGI_FILE ","READ_FILE ")){kind=NodeKind.ReadFile;content=s.Substring(s.IndexOf(' ')+1).Trim();}
                else if(Prefix(u,"SCRIVI_FILE ","WRITE_FILE ")){kind=NodeKind.WriteFile;content=s.Substring(s.IndexOf(' ')+1).Trim();}
                else if(Prefix(u,"CHIUDI_FILE ","CLOSE_FILE ")){kind=NodeKind.CloseFile;content=s.Substring(s.IndexOf(' ')+1).Trim();}
                else if(Prefix(u,"LEGGI ","READ ")){kind=NodeKind.Input;content=s.Substring(u.StartsWith("LEGGI ",StringComparison.Ordinal)?6:5).Trim();int comma=TopToken(content,",");if(comma>=0){prompt=Quoted(content.Substring(comma+1).Trim(),line.Number);content=content.Substring(0,comma).Trim();if(prompt.Length>1024)throw new ParseError(line.Number,"La domanda supera 1024 caratteri.");}}
                else if(Prefix(u,"STAMPA_CONTINUA ","WRITE_CONTINUE ","OUTPUT_CONTINUE ")){kind=NodeKind.Output;content=FlowOutput.Store(s.Substring(s.IndexOf(' ')+1).Trim(),false);}
                else if(Prefix(u,"STAMPA ","SCRIVI ","WRITE ","OUTPUT ")){kind=NodeKind.Output;content=s.Substring(s.IndexOf(' ')+1).Trim();}else if(Prefix(u,"CHIAMA ","CALL ")){kind=NodeKind.Call;content=s.Substring(s.IndexOf(' ')+1).Trim();}else if(Prefix(u,"DICHIARA ","DECLARE ")){kind=NodeKind.Declare;content=s.Substring(s.IndexOf(' ')+1).Trim();}else if(Prefix(u,"ESEGUI "))content=s.Substring(7).Trim();
                if(kind==NodeKind.Process)content=Arrows(content);FlowNode node=Node(kind,content,line.Number);node.Prompt=prompt;part=new Part(node.Id);part.Exits.Add(new Port(node,"next"));return part;
            }
            private void Arm(Part result,FlowNode n,string branch,Part body){if(body.Entry==null)result.Exits.Add(new Port(n,branch));else{Connect(new Port(n,branch),body.Entry);result.Exits.AddRange(body.Exits);}}
            private void Require(string expected,int line,params string[] choices){if(index>=lines.Count||!One(lines[index].Text,choices))throw new ParseError(index<lines.Count?lines[index].Number:line,"Manca "+expected+".");index++;}
        }
        private static int TopToken(string s,string token){char quote='\0';bool escape=false;int depth=0;for(int i=0;i<s.Length;i++){char c=s[i];if(quote!='\0'){if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote='\0';}else if(c=='\''||c=='"')quote=c;else if(c=='('||c=='[')depth++;else if(c==')'||c==']')depth--;else if(depth==0&&i+token.Length<=s.Length&&String.Compare(s,i,token,0,token.Length,StringComparison.OrdinalIgnoreCase)==0&&(token==","||(i>0&&Char.IsWhiteSpace(s[i-1])&&i+token.Length<s.Length&&Char.IsWhiteSpace(s[i+token.Length]))))return i;}return -1;}
        private static string Condition(string value,int offset,string a,string b){string result=value.Substring(offset).Trim(),u=Upper(result);if(u.EndsWith(" "+a,StringComparison.Ordinal))result=result.Substring(0,result.Length-a.Length).Trim();else if(u.EndsWith(" "+b,StringComparison.Ordinal))result=result.Substring(0,result.Length-b.Length).Trim();return result;}
        private static bool NoteLiteral(string value){if(value.Length<2||value[0]!='"'||value[value.Length-1]!='"')return false;for(int i=1;i<value.Length-1;i++){if(value[i]=='"')return false;if(value[i]=='\\'&&++i>=value.Length-1)return false;}return true;}
        private static string AnnotationText(string value,int line){if(value.Length>16386||!NoteLiteral(value))throw new ParseError(line,"Usa ANNOTAZIONE seguito da un testo fra virgolette (massimo 8192 caratteri).");var text=new StringBuilder();for(int i=1;i<value.Length-1;i++){char c=value[i];if(c=='\\'){c=value[++i];switch(c){case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;case '\\':case '"':case (char)39:break;default:throw new ParseError(line,"Escape non valido nella nota: usa \\\\ per una barra.");}}text.Append(c);if(text.Length>8192)throw new ParseError(line,"La nota supera 8192 caratteri.");}return text.ToString();}
        private static string Quoted(string value,int line){try{object v=ExpressionLanguage.Evaluate(value,null);if(!(v is string)||!value.StartsWith("\"",StringComparison.Ordinal))throw new FlowException("");return (string)v;}catch(FlowException){throw new ParseError(line,"La domanda deve essere un testo fra virgolette.");}}
        private static string Upper(string s){return Regex.Replace(s.Trim().ToUpperInvariant()," +"," ");}
        private static bool One(string s,params string[] values){return values.Contains(Upper(s));}
        private static bool Prefix(string s,params string[] values){return values.Any(v=>s.StartsWith(v,StringComparison.Ordinal));}
        private static string Comment(string s){char quote='\0';bool escape=false;for(int i=0;i<s.Length;i++){char c=s[i];if(quote!='\0'){if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote='\0';}else if(c=='\''||c=='"')quote=c;else if(c=='/'&&i+1<s.Length&&s[i+1]=='/')return s.Substring(0,i);}return s;}
        private static string Arrows(string s){var b=new StringBuilder();char quote='\0';bool escape=false;foreach(char c in s){if(quote!='\0'){b.Append(c);if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote='\0';}else if(c=='\''||c=='"'){quote=c;b.Append(c);}else if(c=='←')b.Append("<-");else b.Append(c);}return b.ToString();}
        private static string Literal(string s){return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t")+"\"";}
        private static string Flat(string s,bool process){var b=new StringBuilder();char quote='\0';bool escape=false;for(int i=0;i<s.Length;i++){char c=s[i];if(quote!='\0'){if(c=='\n')b.Append("\\n");else if(c=='\r')b.Append("\\r");else b.Append(c);if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote='\0';}else if(c=='\''||c=='"'){quote=c;b.Append(c);}else if(c=='\n'||c=='\r'){b.Append(process?"; ":" ");if(c=='\r'&&i+1<s.Length&&s[i+1]=='\n')i++;}else b.Append(c);}return b.ToString().Trim();}
        internal static string Encode(Diagram doc){List<FlowSourceText.Origin[]> mapping;return EncodeInternal(doc,false,out mapping);}
        internal static FlowSourceText Generate(Diagram doc){List<FlowSourceText.Origin[]> mapping;string text=EncodeInternal(doc,true,out mapping);return FlowSourceText.FromPseudocode(doc,text,mapping);}
        private static string EncodeInternal(Diagram doc,bool mapped,out List<FlowSourceText.Origin[]> mapping){
            DiagramIO.CheckDocument(doc);var functions=FunctionModel.Catalog(doc);var text=new StringBuilder();mapping=mapped?new List<FlowSourceText.Origin[]>():null;if(doc.RequiresDeclarations){text.Append("VARIABILI DICHIARATE\n\n");if(mapped){mapping.Add(new FlowSourceText.Origin[0]);mapping.Add(new FlowSourceText.Origin[0]);}}
            foreach(var f in functions.Values){text.Append(Signature(f)).Append('\n');if(mapped)mapping.Add(new[]{new FlowSourceText.Origin(f.Id,f.Body.Nodes.First(n=>n.Kind==NodeKind.Start).Id)});var body=new Writer(f.Body,functions,f,true,mapped);text.Append(body.Structured()??body.Explicit()).Append("FINE FUNZIONE\n\n");if(mapped){mapping.AddRange(body.Mapping);mapping.Add(new FlowSourceText.Origin[0]);mapping.Add(new FlowSourceText.Origin[0]);}}
            var writer=new Writer(doc,functions,null,true,mapped);text.Append(writer.Structured()??writer.Explicit());if(mapped)mapping.AddRange(writer.Mapping);if(text.Length>MaxText)throw new ArgumentException("Il pseudocodice supera 256 Ki caratteri. Dividi il diagramma.");return text.ToString();
        }

        internal static string ForCondition(string name,string limit,string stride){return "("+stride+" > 0 e "+name+" <= "+limit+") o ("+stride+" < 0 e "+name+" >= "+limit+")";}
        private static string Compact(string text){return Regex.Replace(text,@"\s+","").ToLowerInvariant();}
        private static bool Mentions(string text,string word){
            char quote='\0';bool escaped=false;for(int i=0;i<text.Length;i++){char c=text[i];if(quote!='\0'){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote='\0';continue;}if(c=='\''||c=='"'){quote=c;continue;}if(Char.IsLetter(c)||c=='_'){int start=i;while(i+1<text.Length&&(Char.IsLetterOrDigit(text[i+1])||text[i+1]=='_'))i++;if(String.Equals(text.Substring(start,i-start+1),word,StringComparison.OrdinalIgnoreCase))return true;}}return false;
        }
        internal static List<string[]> Assignments(string text){
            var result=new List<string[]>();char quote='\0';bool escaped=false;int start=0,depth=0;
            for(int i=0;i<=text.Length;i++){char c=i==text.Length?';':text[i];if(quote!='\0'){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote='\0';continue;}if(c=='\''||c=='"'){quote=c;continue;}if(c=='('||c=='[')depth++;if(c==')'||c==']')depth--;
                if(depth==0&&(c==';'||c=='\n'||c=='\r')){string line=text.Substring(start,i-start).Trim();start=i+1;if(line.Length==0)continue;var m=Regex.Match(line,@"\A("+ExpressionLanguage.IdentifierPattern+@")\s*(?:=|<-)\s*(.+)\z",RegexOptions.Singleline);if(!m.Success)return new List<string[]>();result.Add(new[]{m.Groups[1].Value,m.Groups[2].Value});}}
            return result;
        }
        internal sealed class ForPattern {internal FlowNode Check,Increment;internal string Body,Exit,Header,Variable,From,To,Step;internal string[] HelperNames;}
        internal static ForPattern InspectFor(Diagram d,FlowNode n){if(n==null||String.IsNullOrWhiteSpace(n.Text))return null;try{return new Writer(d,null,null,false).RecognizeFor(n);}catch(ArgumentException){return null;}}

        private sealed class NotStructured : Exception {}
        private sealed class Writer
        {
            private readonly Diagram doc;private readonly List<FlowNode> annotations;private readonly FlowFunction owner;private readonly Dictionary<string,FlowNode> nodes=new Dictionary<string,FlowNode>();private readonly Dictionary<string,string> names=new Dictionary<string,string>(),edges=new Dictionary<string,string>();private readonly HashSet<string> seen=new HashSet<string>();private readonly StringBuilder output=new StringBuilder();private FlowNode start;
            internal readonly List<FlowSourceText.Origin[]> Mapping;
            internal Writer(Diagram doc,IDictionary<string,FlowFunction> functions,FlowFunction owner,bool validate=true,bool mapped=false){Mapping=mapped?new List<FlowSourceText.Origin[]>():null;annotations=doc.Nodes.Where(n=>n.Kind==NodeKind.Note).ToList();doc=FlowAnnotations.Executable(doc);this.doc=doc;this.owner=owner;foreach(FlowNode n in doc.Nodes){nodes.Add(n.Id,n);names.Add(n.Id,"B"+(names.Count+1));if(n.Kind==NodeKind.Start){if(start!=null)throw new ArgumentException("Mantieni un solo blocco Inizio per la conversione.");start=n;}}if(start==null)throw new ArgumentException("Aggiungi un blocco Inizio prima della conversione.");
                foreach(FlowEdge e in doc.Edges){FlowNode n=nodes[e.From];string key=e.From+"/"+e.Branch;if(edges.ContainsKey(key)||n.Kind==NodeKind.End||(n.Kind==NodeKind.Decision?e.Branch!="true"&&e.Branch!="false":e.Branch!="next")||nodes[e.To].Kind==NodeKind.Start)throw new ArgumentException("Correggi i collegamenti duplicati o non validi prima della conversione.");edges.Add(key,e.To);}
                if(validate)foreach(FlowNode n in doc.Nodes)try{Compiler.Block(n,functions,owner);}catch(FlowException e){throw new ArgumentException((owner==null?"Blocco ":"Funzione «"+owner.Name+"», blocco ")+names[n.Id]+": "+e.Message);}
            }

            // A PER is only a readable view of an intact editable graph. Never infer it
            // from the loop's shape alone: changed helpers, extra entrances and branches
            // must retain their actual operations in the exported source.
            internal ForPattern RecognizeFor(FlowNode init){
                if(init.Kind!=NodeKind.Process)return null;var a=Assignments(init.Text);if(a.Count!=2&&a.Count!=3)return null;
                string name=a[0][0],from=a[0][1],limit=a[1][0],to=a[1][1],stride=a.Count==3?a[2][0]:null,step=null;
                if(!Regex.IsMatch(limit,@"\Alimite_ciclo_[0-9]+\z",RegexOptions.IgnoreCase)||String.Equals(name,limit,StringComparison.OrdinalIgnoreCase))return null;
                string next=Next(init,"next");if(next==null)return null;FlowNode check=nodes[next];if(check.Kind!=NodeKind.Decision)return null;
                var incoming=doc.Edges.Where(e=>e.To==check.Id).ToList();if(incoming.Count!=2||incoming.Count(e=>e.From==init.Id)!=1)return null;
                FlowNode increment=nodes[incoming.First(e=>e.From!=init.Id).From];if(increment.Kind!=NodeKind.Process||Next(increment,"next")!=check.Id)return null;
                if(a.Count==3){
                    if(!Regex.IsMatch(stride,@"\Apasso_ciclo_[0-9]+\z",RegexOptions.IgnoreCase)||String.Equals(name,stride,StringComparison.OrdinalIgnoreCase))return null;
                    var guard=Regex.Match(a[2][1],"\\Aint\\s*\\((.*),\\s*\"passo\"\\s*\\)\\z",RegexOptions.Singleline|RegexOptions.IgnoreCase);if(!guard.Success)return null;step=guard.Groups[1].Value.Trim();
                    if(Compact(check.Text)!=Compact(ForCondition(name,limit,stride))||Compact(increment.Text)!=Compact(name+"="+name+"+"+stride))return null;
                }else{
                    var inc=Assignments(increment.Text);if(inc.Count!=1||!String.Equals(inc[0][0],name,StringComparison.OrdinalIgnoreCase))return null;
                    var m=Regex.Match(Compact(inc[0][1]),"\\A"+Regex.Escape(name.ToLowerInvariant())+@"\+\(([+-]?[0-9]+)\)\z");int value;if(!m.Success||!Int32.TryParse(m.Groups[1].Value,out value)||value==0)return null;step=value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if(Compact(check.Text)!=Compact(name+(value>0?"<=":">=")+limit))return null;
                }
                foreach(string helper in new[]{limit,stride})if(helper!=null){
                    if(Mentions(from,helper)||Mentions(to,helper)||Mentions(step,helper))return null;
                    if(owner!=null&&(owner.Parameters.Any(p=>String.Equals(p.Name,helper,StringComparison.OrdinalIgnoreCase))||String.Equals(owner.ReturnVariable,helper,StringComparison.OrdinalIgnoreCase)))return null;
                    foreach(var n in doc.Nodes)if(n!=init&&n!=check&&n!=increment&&Mentions(n.Text??"",helper))return null;
                }
                string body=Next(check,"true"),exit=Next(check,"false");if(body==null||exit==null||exit==increment.Id||exit==check.Id)return null;
                var region=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(body);
                while(queue.Count>0){string id=queue.Dequeue();if(id==increment.Id)continue;if(id==exit||id==check.Id||id==init.Id)return null;if(!region.Add(id))continue;foreach(string branch in new[]{"next","true","false"}){string dest=Next(nodes[id],branch);if(dest!=null)queue.Enqueue(dest);}}
                region.Add(increment.Id);foreach(var e in doc.Edges)if(region.Contains(e.To)&&!region.Contains(e.From)&&!(e.From==check.Id&&e.Branch=="true"))return null;
                return new ForPattern{HelperNames=new[]{limit,stride}.Where(v=>v!=null).ToArray(),Check=check,Increment=increment,Body=body,Exit=exit,Variable=name,From=from,To=to,Step=step,Header="PER "+name+" DA "+Flat(from,false)+" A "+Flat(to,false)+" PASSO "+Flat(step,false)+" FAI"};
            }

            private string Next(FlowNode n,string branch){string to;return edges.TryGetValue(n.Id+"/"+branch,out to)?to:null;}
            private void Line(int depth,string text,params FlowNode[] origins){for(int i=0;i<depth;i++)output.Append("    ");output.Append(text).Append('\n');if(Mapping!=null)Mapping.Add(origins.Select(n=>new FlowSourceText.Origin(owner==null?null:owner.Id,n.Id)).ToArray());}
            private void AnnotationHeader(){foreach(var note in annotations)Line(0,"ANNOTAZIONE "+Literal(note.Text??""),note);}
            private void Note(int depth,FlowNode n){if(!String.IsNullOrEmpty(n.Comment))Line(depth,"NOTA "+Literal(n.Comment),n);}
            private string Command(FlowNode n){if(n.Kind==NodeKind.Pause)return (n.Breakpoint?"PAUSA":"PAUSA_DISATTIVATA")+(String.IsNullOrWhiteSpace(n.BreakpointCondition)?"":" SE "+Flat(n.BreakpointCondition,false));if(n.Kind==NodeKind.TurtleForward)return "AVANZA "+Flat(n.Text,false);if(n.Kind==NodeKind.TurtleTurn)return "RUOTA "+Flat(n.Text,false);if(n.Kind==NodeKind.TurtlePen)return "PENNA "+Flat(n.Text,false);if(n.Kind==NodeKind.TurtleHome)return "ORIGINE_TARTARUGA";if(n.Kind==NodeKind.TurtleClear)return "PULISCI_DISEGNO";if(n.Kind==NodeKind.OpenFile)return "APRI_FILE "+Flat(n.Text,false);if(n.Kind==NodeKind.ReadFile)return "LEGGI_FILE "+Flat(n.Text,false);if(n.Kind==NodeKind.WriteFile)return "SCRIVI_FILE "+Flat(n.Text,false);if(n.Kind==NodeKind.CloseFile)return "CHIUDI_FILE "+Flat(n.Text,false);if(n.Kind==NodeKind.Input)return "LEGGI "+n.Text.Trim()+(String.IsNullOrEmpty(n.Prompt)?"":", "+Literal(n.Prompt));if(n.Kind==NodeKind.Output)return (FlowOutput.Newline(n.Text)?"STAMPA ":"STAMPA_CONTINUA ")+Flat(FlowOutput.Expression(n.Text),false);if(n.Kind==NodeKind.Declare)return "DICHIARA "+Flat(FlowDeclaration.Body(n.Text),false);if(n.Kind==NodeKind.Call)return "CHIAMA "+Flat(n.Text,false);if(n.Kind==NodeKind.Process)return "ESEGUI "+Flat(n.Text,true);return "FERMA";}
            internal string Structured(){try{seen.Add(start.Id);Note(0,start);Line(0,"INIZIO",start);AnnotationHeader();Walk(Next(start,"next"),null,0,new HashSet<string>());if(seen.Count!=nodes.Count)throw new NotStructured();Line(0,"FINE");return output.ToString();}catch(NotStructured){return null;}}
            private void Walk(string id,string stop,int depth,HashSet<string> boundaries){
                if(depth>MaxDepth)throw new NotStructured();while(stop==null||id!=stop){
                    if(id==null||boundaries.Contains(id)||!seen.Add(id))throw new NotStructured();FlowNode n=nodes[id];Note(depth,n);if(n.Kind==NodeKind.End){Line(depth,"FERMA",n);return;}
                    var loop=RecognizeFor(n);if(loop!=null&&String.IsNullOrEmpty(loop.Check.Comment)&&String.IsNullOrEmpty(loop.Increment.Comment)){
                        if(!seen.Add(loop.Check.Id)||seen.Contains(loop.Increment.Id))throw new NotStructured();
                        var inside=new HashSet<string>(boundaries);if(stop!=null)inside.Add(stop);inside.Add(loop.Check.Id);inside.Add(loop.Exit);
                        Line(depth,loop.Header,n,loop.Check,loop.Increment);Walk(loop.Body,loop.Increment.Id,depth+1,inside);seen.Add(loop.Increment.Id);Line(depth,"FINE PER",loop.Increment);id=loop.Exit;continue;
                    }
                    if(n.Kind!=NodeKind.Decision){Line(depth,Command(n),n);id=Next(n,"next");continue;}
                    string yes=Next(n,"true"),no=Next(n,"false");if(yes==null||no==null)throw new NotStructured();var block=new HashSet<string>(boundaries);if(stop!=null)block.Add(stop);block.Add(n.Id);
                    bool yloop=Reach(yes,n.Id,no,block),nloop=Reach(no,n.Id,yes,block);
                    if(yloop!=nloop){string body=yloop?yes:no,exit=yloop?no:yes;Line(depth,"MENTRE "+(yloop?Flat(n.Text,false):"non ("+Flat(n.Text,false)+")")+" FAI",n);Walk(body,n.Id,depth+1,block);Line(depth,"FINE MENTRE",n);id=exit;continue;}
                    string join=Join(yes,no,block);if(join==null)throw new NotStructured();Line(depth,"SE "+Flat(n.Text,false)+" ALLORA",n);Walk(yes,join,depth+1,block);Line(depth,"ALTRIMENTI",n);Walk(no,join,depth+1,block);Line(depth,"FINE SE",n);id=join;
                }
            }
            private bool Reach(string from,string target,string avoid,HashSet<string> blocked){var visited=new HashSet<string>();var q=new Queue<string>();q.Enqueue(from);while(q.Count>0){string id=q.Dequeue();if(id==target)return true;if(id==avoid||blocked.Contains(id)||!visited.Add(id))continue;foreach(string b in new[]{"next","true","false"}){string to=Next(nodes[id],b);if(to!=null)q.Enqueue(to);}}return false;}
            private Dictionary<string,int> Distances(string from,HashSet<string> blocked){var distance=new Dictionary<string,int>();var q=new Queue<string>();distance.Add(from,0);q.Enqueue(from);while(q.Count>0){string id=q.Dequeue();if(blocked.Contains(id))continue;foreach(string b in new[]{"next","true","false"}){string to=Next(nodes[id],b);if(to!=null&&!distance.ContainsKey(to)){distance.Add(to,distance[id]+1);q.Enqueue(to);}}}return distance;}
            private string Join(string yes,string no,HashSet<string> blocked){var a=Distances(yes,blocked);var b=Distances(no,blocked);string best=null;int score=Int32.MaxValue;foreach(FlowNode n in doc.Nodes){int x,y;if(!a.TryGetValue(n.Id,out x)||!b.TryGetValue(n.Id,out y)||seen.Contains(n.Id))continue;int cost=Math.Max(x,y)*1000+x+y;if(cost<score){score=cost;best=n.Id;}}return best;}
            private string Destination(FlowNode n,string branch){string to=Next(n,branch);return to==null?"SCOLLEGATO":"VAI A "+names[to];}
            internal string Explicit(){output.Length=0;if(Mapping!=null)Mapping.Clear();Line(0,"// Percorsi espliciti: le etichette conservano cicli, rami e blocchi separati.");Note(0,start);Line(0,"INIZIO",start);AnnotationHeader();Line(0,Destination(start,"next"),start);foreach(FlowNode n in doc.Nodes){if(n==start)continue;Line(0,"");Line(0,names[n.Id]+":",n);Note(0,n);if(n.Kind==NodeKind.Decision){Line(0,"SE "+Flat(n.Text,false)+" ALLORA",n);Line(1,Destination(n,"true"),n);Line(0,"ALTRIMENTI",n);Line(1,Destination(n,"false"),n);Line(0,"FINE SE",n);}else{Line(0,Command(n),n);if(n.Kind!=NodeKind.End)Line(0,Destination(n,"next"),n);}}Line(0,"FINE");return output.ToString();}
        }
    }
}
