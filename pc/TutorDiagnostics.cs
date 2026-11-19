using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Frociart
{
    internal static class TutorDiagnostics
    {
        internal static string WithoutStrings(string text)
        {
            var result = new StringBuilder(); char quote = '\0'; bool escape = false;
            foreach (char c in text ?? "")
            {
                if (quote != '\0') { result.Append(' '); if (escape) escape = false; else if (c == '\\') escape = true; else if (c == quote) quote = '\0'; }
                else if (c == '\'' || c == '"') { quote = c; result.Append(' '); }
                else result.Append(c);
            }
            return result.ToString();
        }
        internal static TutorFeedback Before(FlowNode node)
        {
            if (node == null || node.Kind == NodeKind.Start || node.Kind == NodeKind.End) return null;
            string text = (node.Text ?? "").Trim(), code = WithoutStrings(text);
            if (text.Length == 0) return Tutor.No("empty", "La casella è ancora vuota", "Il blocco ha una forma, ma manca l'istruzione da eseguire.",
                node.Kind == NodeKind.Input ? "Scrivi il nome della variabile, per esempio eta." : node.Kind == NodeKind.Process ? "Scrivi nome = valore, per esempio punti = 5." : node.Kind == NodeKind.Decision ? "Scrivi una domanda, per esempio eta >= 18." : "Scrivi un valore o un testo fra virgolette: \"Ciao\".", node.Id);
            if (Regex.IsMatch(code, "[“”‘’]")) return Tutor.No("smart-quotes", "Servono virgolette dritte", "Le virgolette tipografiche non delimitano un testo nel motore.", "Usa il tasto \" della tastiera: \"Ciao\".", node.Id);
            string[] symbols = { "≥", ">=", "≤", "<=", "≠", "!=", "×", "*", "÷", "/", "←", "=", "−", "-" };
            for (int i = 0; i < symbols.Length; i += 2) if (code.Contains(symbols[i])) return Tutor.No("unicode-operator", "Qui il simbolo si scrive diversamente",
                "Il motore accetta " + symbols[i + 1] + " al posto di " + symbols[i] + ".", "Scrivi " + symbols[i + 1] + " con i tasti normali. Il testo fra virgolette può contenere simboli.", node.Id);
            if (node.Kind == NodeKind.Input)
            {
                if (text.Contains("=") || text.Contains("<-")) return Tutor.No("input-assignment", "Nell'Input va solo il nome", "L'Input chiede il valore durante l'esecuzione: non devi assegnarlo qui.", "Scrivi eta nel blocco. Durante Prova inserisci per esempio 21 nel campo del valore.", node.Id);
                if (!ExpressionLanguage.Identifier(text)) return Tutor.No("input-identifier", "Questo non è un nome di variabile valido", "Il nome ha al massimo 64 caratteri: comincia con una lettera o _, continua con lettere, cifre o _. Niente spazi, trattini, virgolette o parole riservate come vero, falso, and, or, not.", "Esempi: eta, numero_1, totale. La domanda leggibile va nel campo Domanda.", node.Id);
                return null;
            }
            if (code.Contains("**")) return Tutor.No("power-symbol", "La potenza usa ^", "Due asterischi non sono un operatore di Frociart.", "Per il quadrato scrivi numero ^ 2; per moltiplicare usa un solo *.", node.Id);
            if (Regex.IsMatch(code, @"^\s*[\p{L}_][\p{L}\p{N}_]*\s*(\+\+|--)\s*$")) return Tutor.No("increment", "L'aggiornamento va scritto per intero", "Frociart non usa ++ o --.", "Scrivi contatore = contatore + 1, oppure contatore = contatore - 1.", node.Id);
            if (Regex.IsMatch(code, @"^\s*[\p{L}_][\p{L}\p{N}_]*\s*[+*/%\-]=")) return Tutor.No("compound-assignment", "Scrivi anche il valore precedente", "Le abbreviazioni +=, -= e simili non sono supportate.", "Scrivi totale = totale + numero invece di totale += numero.", node.Id);
            if (Regex.IsMatch(code, @"\b(print|stampa|input|if|while|for)\s*\(")) return Tutor.No("language-command", "Il comando è già nella forma del blocco", "Non devi scrivere comandi di un altro linguaggio dentro una casella.", "Nell'Output scrivi solo l'espressione, come totale. Per un dato usa Input; per scegliere usa Decisione.", node.Id);
            if (node.Kind == NodeKind.Process)
            {
                var match = Regex.Match(code, @"^\s*([\p{L}_][\p{L}\p{N}_]*)\s*==");
                if (match.Success) return Tutor.No("compare-in-process", "== confronta, non assegna", "Un'Operazione deve conservare un valore in una variabile.", "Scrivi " + match.Groups[1].Value + " = valore. Per fare una domanda usa Decisione con ==.", node.Id);
                if (!Regex.IsMatch(code, @"^\s*[\p{L}_][\p{L}\p{N}_]*\s*(=|<-)")) return Tutor.No("assignment-missing", "Manca chi conserva il risultato", "Un calcolo da solo non dice dove mettere il valore.", "Scrivi totale = 2 + 3. Se vuoi soltanto mostrarlo, usa Output.", node.Id);
            }
            int parentheses = 0;
            foreach (char c in code) { if (c == '(') parentheses++; if (c == ')' && --parentheses < 0) return Tutor.No("parentheses-close", "Una parentesi si chiude troppo presto", "Ogni ) deve chiudere una ( precedente.", "Rileggi le coppie da sinistra: (a + b) / 2.", node.Id); }
            if (parentheses > 0) return Tutor.No("parentheses-open", "Manca una parentesi chiusa", "Hai aperto un gruppo senza chiuderlo.", "Aggiungi la ) del gruppo: (a + b) / 2.", node.Id);
            if (!code.Contains("(") && Regex.IsMatch(code, @"\d,\d")) return Tutor.No("decimal-comma", "Nel codice i decimali usano il punto", "La virgola separa gli argomenti delle funzioni.", "Nel blocco scrivi 1.5. Nel campo del valore Input puoi inserire anche 1,5.", node.Id);
            return null;
        }
        internal static TutorFeedback Issue(ValidationIssue issue)
        {
            string title = "Controlla questo blocco", why = issue.Message, fix = issue.Suggestion ?? "Rivedi la guida del blocco.";
            switch (issue.Code)
            {
                case "start-count": title = "Serve un solo punto di partenza"; why = "L'esecuzione deve sapere da quale Inizio partire."; fix = "Tieni un Inizio e collegalo al primo passaggio."; break;
                case "end-missing": title = "Manca la fine del percorso"; fix = "Aggiungi Fine e collega i percorsi che devono terminare."; break;
                case "start-incoming": title = "Il ciclo non torna in Inizio"; fix = "Fai tornare la freccia alla Decisione o al passaggio da ripetere."; break;
                case "end-outgoing": title = "Da Fine non si riparte"; fix = "Elimina la freccia che parte da Fine o cambia tipo di blocco."; break;
                case "decision-branches": title = "La domanda ha due risposte"; why = "Ogni Decisione richiede un'uscita per vero e una per falso."; fix = "Collega Sì/vero e No/falso. Il nome della freccia non cambia la condizione."; break;
                case "single-outgoing": title = "La vicinanza non basta: serve la freccia"; fix = "Collega l'uscita al blocco successivo. Soltanto Decisione ha due uscite."; break;
                case "no-end-path": title = "Questo percorso non può arrivare a Fine"; fix = "Segui il ramo e aggiungi l'uscita dal ciclo."; break;
                case "uninitialized": title = "Prima dai un valore alla variabile"; why += "\nUn nome non crea automaticamente un valore."; fix = "Aggiungi Input o un'Operazione prima di ogni percorso che legge il nome. Per una parola fissa usa le virgolette: \"Ciao\"."; break;
                case "unreachable": title = "Questa casella non verrà attraversata"; fix = "Collegala nel punto giusto oppure rimuovila se non serve."; break;
                case "syntax": return Runtime(issue.Message, issue.NodeId);
            }
            return Tutor.No(issue.Code, title, why, fix, issue.NodeId);
        }
        internal static TutorFeedback Runtime(string message, string id)
        {
            string text = message ?? "Il blocco non è eseguibile.", low = text.ToLowerInvariant();
            string code = "engine", title = "Rileggiamo questa istruzione", fix = "Apri la guida del blocco e controlla i dati.";
            if (low.Contains("divisione per zero")) { code = "divide-zero"; title = "Non si può dividere per zero"; fix = "Prima della divisione usa Decisione: divisore != 0. Calcola soltanto nel ramo vero."; }
            else if (low.Contains("resto") && low.Contains("zero")) { code = "mod-zero"; title = "Anche il resto richiede un divisore diverso da zero"; fix = "Controlla il divisore prima di %. % calcola il resto, non una percentuale."; }
            else if (low.Contains("non ha ancora un valore") || low.Contains("potrebbe essere letta")) { code = "undefined"; title = "Questa variabile non ha ancora un valore"; fix = "Usa Input oppure nome = valore prima di leggerla. Un testo fisso va fra virgolette."; }
            else if (low.Contains("virgolette") || low.Contains("escape")) { code = "quotes"; title = "Il testo non è chiuso correttamente"; fix = "Scrivi \"Ciao\". Ogni virgoletta aperta richiede la sua chiusura; per una virgoletta nel testo scrivi \\\"."; }
            else if (low.Contains("serve un numero") && (low.Contains("«vero»") || low.Contains("«falso»"))) { code = "boolean-number"; title = "Vero/falso non è un numero"; fix = "Non scrivere 0 <= eta <= 65. Unisci due domande con and: eta >= 0 and eta <= 65."; }
            else if (low.Contains("serve un numero") || low.Contains("richiede un testo")) { code = "type"; title = "Numero e testo non sono la stessa cosa"; fix = "5 è un numero, \"5\" è testo. Nel dato Input numerico scrivi 5, senza virgolette o nome."; }
            else if (low.Contains("condizione deve restituire")) { code = "condition-type"; title = "La Decisione deve scegliere vero o falso"; fix = "\"numero > 0\" resta testo. Per fare la domanda togli le virgolette: numero > 0. Zero vale falso, gli altri numeri valgono vero."; }
            else if (low.Contains("radice quadrata")) { code = "sqrt-domain"; title = "La radice richiede un numero non negativo"; fix = "Controlla numero >= 0 prima di sqrt(numero)."; }
            else if (low.Contains("funzione") && low.Contains("sconosciuta")) { code = "function-name"; title = "La funzione non è disponibile"; fix = "Usa abs, sqrt, min, max, floor, round e len. Non scrivere print o funzioni personali."; }
            else if (low.Contains("argomenti")) { code = "function-args"; title = "La funzione richiede un numero preciso di valori"; fix = "max(a,b) riceve almeno due valori; sqrt(numero) uno. La virgola separa i valori."; }
            else if (low.Contains("cifre decimali")) { code = "round-digits"; title = "Quante cifre vuoi conservare?"; fix = "Il secondo argomento di round è un intero da 0 a 15: round(3.14159,2)."; }
            else if (low.Contains("finito") || low.Contains("troppo grande")) { code = "finite"; title = "Il numero supera i limiti del motore"; fix = "Usa valori finiti più piccoli. NaN e infinito non sono dati numerici validi."; }
            else if (low.Contains("limite") && (low.Contains("blocchi") || low.Contains("esecuzione"))) { code = "loop-limit"; title = "Il ciclo potrebbe non terminare"; fix = "Aggiorna la variabile di controllo e fai in modo che prima o poi la Decisione raggiunga Fine."; }
            else if (low.Contains("inatteso") || low.Contains("incompleta")) { code = "incomplete-expression"; title = "La frase del blocco non è completa"; fix = "Ogni operatore richiede i suoi valori: 2 + 3, non 2 +. Usa la forma prevista per questo blocco."; }
            return Tutor.No(code, title, text, fix, id);
        }
        internal static string Misconception(TutorLesson lesson, FlowNode node)
        {
            string code = Regex.Replace(node.Text ?? "", @"\s+", "").ToLowerInvariant();
            if (lesson.id == "b07" && (code == "\"nome\"" || code == "'nome'")) return "Le virgolette mostrano la parola nome. Qui devi leggere il valore conservato nella variabile.";
            if (lesson.id == "b08" && code == "punti=1") return "Questa assegnazione sostituisce punti con 1 ogni volta. Per aumentarlo devi leggere anche il valore precedente.";
            if (lesson.id == "b09" && code == "resto=prezzo-pagato") return "Il resto parte dal denaro pagato e sottrae il prezzo: qui la sottrazione è invertita.";
            if (lesson.id == "b10" && code == "totale=prezzo+quantita") return "Il prezzo va ripetuto per tutti i pezzi: serve una moltiplicazione.";
            if (lesson.id == "b12" && code == "media=a+b/2") return "La divisione precede la somma. Raggruppa l'intera somma con le parentesi.";
            if ((lesson.id == "b13" || lesson.id == "b24") && code == "eta>18") return "Con > il valore 18 è escluso. La missione include chi ha esattamente 18 anni.";
            if (lesson.id == "b15" && code == "!0") return "!0 nega zero e produce sempre vero: non legge numero. != confronta invece due valori.";
            if (lesson.id == "b16" && code == "eta>=18oreta<=65") return "or richiede una sola parte vera: anche 100 passerebbe. Un intervallo richiede entrambi i limiti con and.";
            if (lesson.id == "b17" && code == "giorno==6or7") return "7 da solo è diverso da zero e quindi vale vero. Confronta giorno anche nella seconda domanda.";
            if (lesson.id == "b20" && code == "i<=n") return "Partendo da zero, <= fa un giro in più. Prova n = 0 per osservare il problema.";
            if (lesson.id == "b21" && code == "somma=i") return "somma perde i giri precedenti. Per accumulare includi la somma già conservata.";
            if (lesson.id == "b22" && (code == "numero/2==0" || code == "numero/2=0")) return "/ calcola il quoziente. La parità dipende dal resto, ottenuto con %.";
            return "";
        }
        internal static string Purpose(NodeKind kind)
        {
            switch (kind) {
                case NodeKind.Start: return "Punto di partenza: non legge né cambia variabili.";
                case NodeKind.End: return "Ferma il percorso: non legge né cambia variabili.";
                case NodeKind.Input: return "Chiede un dato durante Prova e lo conserva nel nome scritto qui.";
                case NodeKind.Process: return "Calcola la parte destra e la conserva nella variabile a sinistra.";
                case NodeKind.Decision: return "Legge i valori e sceglie una sola uscita: vero oppure falso.";
                case NodeKind.Declare:return "Crea variabili con un tipo preciso: intero, reale, testo o logico. Puoi indicare una dimensione per un vettore.";
                case NodeKind.Pause:return "Ferma la simulazione in questo punto, sempre o quando la condizione è vera. Riprendi conserva le variabili. Ignora pause permette di eseguire senza interruzioni.";
                case NodeKind.Call: return "Esegue una funzione con i parametri indicati, poi continua dal blocco successivo. Le variabili della funzione sono locali alla chiamata.";
                default: return "Legge un'espressione e mostra il risultato, senza cambiare le variabili.";
            }
        }
        internal static string Inspect(FlowNode node, IDictionary<string, object> values)
        {
            var reads = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var writes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try {
                var compiled = Compiler.Block(node); writes.UnionWith(compiled.Definitions());
                if (compiled.Expression != null) compiled.Expression.ReadVariables(reads);
                foreach (var assignment in compiled.Assignments) assignment.Expression.ReadVariables(reads);
            } catch (FlowException) { }
            Func<string, string> show = delegate(string name) { object value; return name.ToLowerInvariant() + (values != null && values.TryGetValue(name, out value) ? " = " + ValueFormatter.Format(value) : " — valore non ancora osservato"); };
            return Purpose(node.Kind) + "\n\nLegge: " + (reads.Count == 0 ? "nessuna variabile" : String.Join("; ", reads.Select(show))) +
                "\nConserva o cambia: " + (writes.Count == 0 ? "nessuna variabile" : String.Join("; ", writes.Select(show)));
        }
        internal static string Explain(FlowNode node, IDictionary<string, object> observed, IDictionary<string,FlowFunction> functions=null, FlowFunction owner=null)
        {
            if (node == null) return "Non c'è una prossima casella da eseguire.";
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (observed != null) foreach (var pair in observed) values[pair.Key] = pair.Value;
            string text = Ui.KindName(node.Kind) + ". Hai scritto: «" + node.Text + "».\n";
            var before = Before(node); if (before != null) return text + before.Text;
            try
            {
                var block = Compiler.Block(node,functions,owner);
                if (node.Kind == NodeKind.Start) return text + "Avvia il programma e segui la freccia. Non crea ancora dati.";
                if (node.Kind == NodeKind.End) return text + (owner==null?"Il programma si ferma qui. Non esegue altre caselle.":owner.ReturnType==FlowDataType.Void?"La funzione termina e torna al blocco chiamante.":"La funzione restituisce il valore di «"+owner.ReturnVariable+"» e torna al blocco chiamante.");
                if (node.Kind == NodeKind.Input) return text + "Il programma chiede un dato" + (String.IsNullOrWhiteSpace(node.Prompt) ? "." : ": «" + node.Prompt + "».") + "\nIl dato verrà conservato in " + block.InputVariable + ". Il nome non è il valore: per conoscere il valore devi inserirlo durante Prova.";
                var reads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (block.Expression != null) block.Expression.ReadVariables(reads);
                // In multi-assignment blocks, later statements can read a value defined above.
                foreach (var name in reads) { object value; text += values.TryGetValue(name, out value) ? "Legge " + name + " = " + ValueFormatter.Format(value) + " (" + Tutor.Type(value) + ").\n" : "Per conoscere il risultato serve prima un valore per " + name + ".\n"; }
                var expressions=new Stack<Expr>();if(block.Expression!=null)expressions.Push(block.Expression);foreach(var assignment in block.Assignments)expressions.Push(assignment.Expression);
                var called=new HashSet<string>();while(expressions.Count>0){var expression=expressions.Pop();var call=expression as CallExpr;if(call!=null&&call.Function!=null)called.Add(call.Function.Name);foreach(var child in ExpressionMachine.ChildrenOf(expression))expressions.Push(child);}
                if(called.Count>0)return text+"Richiama: "+String.Join(", ",called)+".\nOgni chiamata riceve i parametri e usa variabili locali. Esegui il diagramma per seguire i passaggi, gli eventuali input e il risultato restituito.";
                if (node.Kind == NodeKind.Process)
                {
                    foreach (var assignment in block.Assignments)
                    {
                        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase); assignment.Expression.ReadVariables(dependencies);
                        foreach (string name in dependencies) { object value; text += values.TryGetValue(name, out value) ? "Legge " + name + " = " + ValueFormatter.Format(value) + " (" + Tutor.Type(value) + ").\n" : "Serve prima un valore per " + name + ".\n"; }
                        object result = assignment.Expression.Eval(values); object previous; bool had = values.TryGetValue(assignment.Name, out previous);
                        text += "La parte destra produce " + ValueFormatter.Format(result) + " (" + Tutor.Type(result) + ").\n" + assignment.Name + (had ? " passa da " + ValueFormatter.Format(previous) + " a " : " riceve ") + ValueFormatter.Format(result) + ".\n";
                        values[assignment.Name] = result;
                    }
                }
                else
                {
                    object result = block.Expression.Eval(values);
                    if (node.Kind == NodeKind.Output) text += "L'Output mostra «" + ValueFormatter.Format(result) + "» (" + Tutor.Type(result) + "). Non cambia le variabili.";
                    else text += "La domanda vale " + (Values.Truth(result) ? "vero" : "falso") + ": viene seguita soltanto la freccia " + node.GetBranchLabel(Values.Truth(result) ? "true" : "false") + ".";
                }
                return text;
            }
            catch (FlowException ex) { return text + "\nCon questi dati non posso completare il calcolo: " + ex.Message + "\n" + Runtime(ex.Message, node.Id).Fix; }
        }
        internal static string Describe(Diagram document)
        {
            var text = new StringBuilder("Questa lettura descrive le istruzioni e i collegamenti effettivi. L'obiettivo di un problema si controlla anche con dati di prova.\n\n");
            foreach (var node in document.Nodes)
            {
                text.Append(Ui.KindName(node.Kind)).Append(" · ").Append(node.Text).Append("\n").Append(Purpose(node.Kind)).Append("\n");
                foreach (var edge in document.Edges.Where(e => e.From == node.Id)) { var next = Tutor.Find(document, edge.To); text.Append(edge.Branch == "true" ? "Se vero" : edge.Branch == "false" ? "Se falso" : "Poi").Append(" → ").Append(next == null ? "destinazione mancante" : Ui.KindName(next.Kind) + " · " + next.Text).Append("\n"); }
                text.Append("\n");
            }
            return text.ToString();
        }
    }
}
