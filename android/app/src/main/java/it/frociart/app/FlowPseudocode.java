package it.frociart.app;

import java.util.*;
import java.util.regex.*;
import static it.frociart.app.FlowModel.*;

/** Offline, bounded translation. Expressions are parsed by the existing interpreter, never executed here. */
public final class FlowPseudocode {
    private FlowPseudocode() {}
    public static final int MAX_TEXT=262144, MAX_LINES=5000, MAX_DEPTH=48;
    public static final String EXAMPLE="INIZIO\nLEGGI numero\nSE numero >= 0 ALLORA\n    STAMPA \"Positivo o zero\"\nALTRIMENTI\n    STAMPA \"Negativo\"\nFINE SE\nFINE\n";
    public static final String VECTOR_EXAMPLE="INIZIO\nvalori = [10, 20, 30]\nPER i DA 0 A len(valori) - 1 FAI\n    STAMPA valori[i]\nFINE PER\nSTAMPA \"Totale: \" + somma(valori)\nFINE\n";
    public static final String HELP="Una istruzione per riga. Maiuscole e rientri sono liberi.\n\nINIZIO / FINE\nLEGGI numero\nSTAMPA \"Risultato: \" + numero\ntotale <- totale + numero\n\nSE condizione ALLORA\n    ...\nALTRIMENTI\n    ...\nFINE SE\n\nMENTRE condizione FAI\n    ...\nFINE MENTRE\n\nRIPETI\n    ...\nFINCHÉ condizione\n\nSono accettati anche IF/THEN/ELSE/END IF, WHILE/DO/END WHILE, REPEAT/UNTIL, READ/WRITE e BEGIN/END. SCRIVI resta accettato come sinonimo di STAMPA.\nSTAMPA_CONTINUA espressione continua sulla stessa riga; STAMPA conclude la riga.\nPAUSA ferma la simulazione; PAUSA SE contatore >= 5 ferma solo quando è vero. Riprendi continua con gli stessi valori. PAUSA_DISATTIVATA conserva un punto disattivato. Le esportazioni del codice attraversano le pause senza fermarsi.\nUsa =, <- o ← per assegnare; e, o, non per la logica. Commenti liberi: // testo. Per conservare una nota su un blocco, scrivi NOTA \"spiegazione\" immediatamente prima della sua istruzione (anche INIZIO o FERMA).\n\nPer percorsi complessi: etichetta: e VAI A etichetta. FERMA termina un percorso; SCOLLEGATO conserva un'uscita libera.\nLEGGI numero, \"Quanto vale?\" conserva la domanda. ESEGUI raggruppa più assegnazioni separate da ;.\n\nLa conversione crea un nuovo progetto. Posizioni, colori e nomi personalizzati dei rami non sono trasferiti: usa .frociart per conservare l'aspetto. Vettori: valori = [10, 20, 30], valori[i] = 5, LEGGI valori[i]. Indici da 0; elementi dello stesso tipo. vettore(5) crea cinque zeri; len, somma, min e max leggono un vettore.\n\nPER i DA 0 A len(valori) - 1 PASSO 1 FAI\n    STAMPA valori[i]\nFINE PER\nAnche FOR i = 1 TO 5 STEP 1 DO / END FOR. PASSO può essere un numero intero diverso da zero oppure una variabile o espressione che restituisce un intero da -2147483648 a 2147483647. Un passo negativo conta all’indietro. Inizio, limite e passo vengono valutati in questo ordine una sola volta a ogni ingresso nel ciclo. Il limite e il passo calcolato restano visibili come limite_ciclo_N e passo_ciclo_N. I blocchi sono modificabili: il pseudocodice mostra PER finché la struttura resta riconoscibile; altrimenti conserva le istruzioni effettive.\n\nFUNZIONE doppio(valore: REALE) -> REALE risultato\n    risultato = valore * 2\nFINE FUNZIONE\n\nSTAMPA doppio(3)\n\nI tipi sono VALORE (automatico), INTERO, REALE, TESTO e LOGICO. Aggiungi [] per un vettore. Usa RIF prima del tipo per modificare il vettore originale, per esempio FUNZIONE aggiorna(valori: RIF INTERO[]). Senza RIF il parametro resta una copia. Nei riferimenti puoi cambiare gli elementi valori[i], con dimensione e tipo originali. Il risultato è la variabile indicata dopo il tipo; se omessa si chiama risultato. Senza -> la funzione non restituisce un valore: usa CHIAMA saluta(nome). Le funzioni hanno variabili locali e possono richiamarsi, anche ricorsivamente. Definiscile fuori da INIZIO / FINE del programma principale. Anche FUNCTION / END FUNCTION e CALL sono accettati.\n\nDICHIARA totale, resto: INTERO\nDICHIARA prezzi: REALE[10]\nTipi: INTERO, REALE, TESTO, LOGICO (anche INTEGER, REAL, STRING, BOOLEAN). Gli interi scartano la parte decimale verso zero; valori iniziali 0, testo vuoto o falso. I vettori hanno dimensione fissa da 0 a 1024. La dichiarazione deve precedere ogni uso. Anche DECLARE è accettato. Le vecchie variabili automatiche restano disponibili." + "\n\nVARIABILI DICHIARATE, prima di funzioni e programma, richiede Dichiara per ogni variabile. Parametri e risultato sono definiti dalla firma. Dichiara anche il contatore PER; limite e passo ausiliari sono gestiti dal ciclo. Puoi scegliere questa modalità anche in Progetto → Variabili. I progetti importati da Flowgorithm mantengono le dichiarazioni obbligatorie." + FlowTurtle.SYNTAX;
    public static final class ParseError extends IllegalArgumentException {
        public final int line;
        ParseError(int line,String detail){super("Riga "+line+": "+detail);this.line=line;}
    }
    private static ParseError error(int line,String detail){return new ParseError(line,detail);}
    private static final class Line {String text,note;int number;Line(String text,int number){this.text=text;this.number=number;}}
    private static final class Port {Node node;String branch;Port(Node n,String b){node=n;branch=b;}}
    private static final class Part {String entry;ArrayList<Port> exits=new ArrayList<>();Part(String entry){this.entry=entry;}}
    private static final class Jump {Part part;String label;int line;Jump(Part p,String label,int line){part=p;this.label=label;this.line=line;}}
    private static final class Label {String name;Part part;int line;Label(String n,Part p,int l){name=n;part=p;line=l;}}

    private static final class FunctionSource {Function function;int line;final ArrayList<Line> body=new ArrayList<>();}
    public static final String FUNCTION_EXAMPLE="FUNZIONE doppio(valore: REALE) -> REALE risultato\nINIZIO\n    risultato = valore * 2\nFINE\nFINE FUNZIONE\n\nINIZIO\nLEGGI numero\nSTAMPA doppio(numero)\nFINE\n";
    public static Document decode(String source){
        ArrayList<Line> lines=sourceLines(source),main=new ArrayList<>();ArrayList<FunctionSource> parts=new ArrayList<>();ArrayList<Function> definitions=new ArrayList<>();
        FunctionSource active=null;int depth=0;boolean inMain=false,requireDeclarations=false;
        for(Line line:lines){String u=upper(line.text);
            if(one(u,"VARIABILI DICHIARATE","REQUIRE DECLARATIONS")){if(requireDeclarations||active!=null||depth>0||inMain||!main.isEmpty()||!definitions.isEmpty()||line.note!=null)throw error(line.number,"Metti VARIABILI DICHIARATE una sola volta, prima di funzioni e programma.");requireDeclarations=true;continue;}
            if(line.note!=null&&(prefix(u,"FUNZIONE ","FUNCTION ")||one(u,"FINE FUNZIONE","FINEFUNZIONE","END FUNCTION","ENDFUNCTION")))throw error(line.number,"Metti NOTA prima di un blocco, per esempio INIZIO, non prima della firma della funzione.");
            if(prefix(u,"FUNZIONE ","FUNCTION ")){
                if(active!=null||depth>0||inMain)throw error(line.number,"Definisci le funzioni fuori dal programma principale e dalle altre funzioni.");
                active=new FunctionSource();active.function=readSignature(line);active.line=line.number;parts.add(active);definitions.add(active.function);
                Document shell=new Document();shell.version=FlowFunctions.projectVersion(definitions);shell.functions=definitions;try{FlowFunctions.checkProject(shell);}catch(IllegalArgumentException ex){throw error(line.number,ex.getMessage());}continue;
            }
            if(one(u,"FINE FUNZIONE","FINEFUNZIONE","END FUNCTION","ENDFUNCTION")){if(active==null)throw error(line.number,"FINE FUNZIONE senza una funzione aperta.");active=null;continue;}
            if(active!=null){active.body.add(line);continue;}
            if(one(u,"INIZIO","BEGIN"))inMain=true;else if(one(u,"FINE","END"))inMain=false;
            if(prefix(u,"SE ","IF ","MENTRE ","WHILE ","PER ","FOR ")||one(u,"RIPETI","REPEAT"))depth++;
            else if(one(u,"FINE SE","FINESE","END IF","ENDIF","FINE MENTRE","FINEMENTRE","END WHILE","ENDWHILE","FINE PER","FINEPER","END FOR","ENDFOR")||prefix(u,"FINCHÉ ","FINCHE ","UNTIL "))depth=Math.max(0,depth-1);
            main.add(line);
        }
        if(active!=null)throw error(active.line,"Manca FINE FUNZIONE.");
        Document shell=new Document();shell.version=FlowFunctions.projectVersion(definitions);shell.functions=definitions.isEmpty()?null:definitions;Map<String,Function> catalog=FlowFunctions.catalog(shell);int used=0;
        for(FunctionSource part:parts){part.function.body=new Reader(part.body,catalog,part.function,MAX_NODES-used,part.line).read();part.function.body.title=part.function.name;used+=part.function.body.nodes.size();}
        Document result=new Reader(main,catalog,null,MAX_NODES-used,1).read();result.functions=shell.functions;result.variablePolicy=requireDeclarations?"declared":null;FlowAnnotations.promote(result);
        try{FlowCodec.check(result);}catch(IllegalArgumentException ex){throw error(lines.get(lines.size()-1).number,ex.getMessage());}return result;
    }
    static Document decodeFragment(String source,Document project,Function owner,Iterable<String> reserved){
        Reader reader=new Reader(sourceLines(source),FlowFunctions.catalog(project),owner,MAX_NODES,1);
        for(String name:reserved)if(name!=null)reader.identifiers.add(name.toLowerCase(Locale.ROOT));
        return reader.read();
    }
    private static ArrayList<Line> sourceLines(String source){
        if(source==null||source.length()>MAX_TEXT)throw error(1,"Massimo 256 Ki caratteri.");
        source=source.replace("\r\n","\n").replace('\r','\n');if(source.startsWith("\uFEFF"))source=source.substring(1);
        String[] raw=source.split("\n",-1);if(raw.length>MAX_LINES)throw error(MAX_LINES,"Troppe righe (massimo 5000).");
        String note=null;ArrayList<Line> result=new ArrayList<>();for(int i=0;i<raw.length;i++){String value=comment(raw[i]).trim();if(value.isEmpty())continue;if(prefix(upper(value),"NOTA ","NOTE ")){String literal=value.substring(5).trim();if(!noteLiteral(literal))throw error(i+1,"Usa NOTA seguito da un testo fra virgolette.");String text=quoted(literal,i+1);note=note==null?text:note+"\n"+text;if(note.length()>2048)throw error(i+1,"Il commento supera 2048 caratteri.");continue;}Line line=new Line(value,i+1);line.note=note;result.add(line);note=null;}if(note!=null)throw error(raw.length,"Dopo NOTA inserisci un blocco.");
        if(result.isEmpty())throw error(1,"Scrivi o incolla il tuo pseudocodice.");return result;
    }
    private static Function readSignature(Line line){
        Matcher m=Pattern.compile("\\A(?:FUNZIONE|FUNCTION)\\s+([^\\s(]+)\\s*\\(([^()]*)\\)\\s*(?:->\\s*(.+))?\\z",Pattern.CASE_INSENSITIVE).matcher(line.text);
        if(!m.matches())throw error(line.number,"Usa FUNZIONE nome(valore: REALE) -> REALE risultato. Ometti la freccia se la funzione non restituisce un valore.");
        Function f=new Function();f.name=m.group(1);String parameters=m.group(2).trim();
        if(!parameters.isEmpty())for(String raw:parameters.split(",",-1)){
            String[] pair=raw.split(":",-1);if(pair.length>2||pair[0].trim().isEmpty())throw error(line.number,"Parametro non valido: usa nome: TIPO.");
            String spec=pair.length==1?"VALORE":pair[1].trim();Parameter p=new Parameter();p.name=pair[0].trim();Matcher reference=Pattern.compile("^(?:RIF|REF)\\s+",Pattern.CASE_INSENSITIVE).matcher(spec);p.byReference=reference.find();if(p.byReference)spec=spec.substring(reference.end()).trim();p.type=readType(spec,line.number);p.isArray=spec.endsWith("[]");f.parameters.add(p);
        }
        if(m.group(3)!=null){Matcher ret=Pattern.compile("\\A([^\\s]+)(?:\\s+([^\\s]+))?\\z").matcher(m.group(3).trim());if(!ret.matches())throw error(line.number,"Dopo -> usa TIPO nomeRisultato.");f.returnType=readType(ret.group(1),line.number);f.returnIsArray=ret.group(1).endsWith("[]");f.returnVariable=ret.group(2)!=null?ret.group(2):f.returnType==VOID?"":"risultato";}
        return f;
    }
    private static int readType(String text,int line){
        text=text.trim().toUpperCase(Locale.ROOT);if(text.endsWith("[]"))text=text.substring(0,text.length()-2);
        switch(text){case "VALORE":case "AUTO":case "ANY":return AUTO;case "INTERO":case "INTEGER":return INTEGER;case "REALE":case "REAL":return REAL;case "TESTO":case "STRING":case "TEXT":return TEXT;case "LOGICO":case "BOOLEAN":case "BOOL":return BOOLEAN;case "NESSUNO":case "VOID":return VOID;default:throw error(line,"Tipo sconosciuto. Usa VALORE, INTERO, REALE, TESTO o LOGICO; aggiungi [] per un vettore.");}
    }
    private static String typeCode(int type,boolean array){return new String[]{"VALORE","INTERO","REALE","TESTO","LOGICO","NESSUNO"}[type]+(array?"[]":"");}
    public static String signature(Function f){StringBuilder s=new StringBuilder("FUNZIONE ").append(f.name).append('(');for(int i=0;i<f.parameters.size();i++){if(i>0)s.append(", ");Parameter p=f.parameters.get(i);s.append(p.name).append(": ").append(p.byReference?"RIF ":"").append(typeCode(p.type,p.isArray));}s.append(')');if(f.returnType!=VOID)s.append(" -> ").append(typeCode(f.returnType,f.returnIsArray)).append(' ').append(f.returnVariable);return s.toString();}
    private static final class Reader {
        final ArrayList<Line> lines;final Document doc=new Document();final Map<String,Function> functions;final Function owner;final int maxNodes;
        final HashSet<Integer> notesUsed=new HashSet<>();
        final ArrayList<Jump> jumps=new ArrayList<>();final ArrayList<Label> labels=new ArrayList<>();
        final HashMap<String,Integer> sourceLines=new HashMap<>();final HashSet<String> identifiers=new HashSet<>();int index,forCount;
        Reader(ArrayList<Line> source,Map<String,Function> functions,Function owner,int maxNodes,int fallbackLine){
            lines=source;this.functions=functions;this.owner=owner;this.maxNodes=maxNodes;
            if(lines.isEmpty()){lines.add(new Line("INIZIO",fallbackLine));lines.add(new Line("FINE",fallbackLine));}
            if(owner!=null){for(Parameter parameter:owner.parameters)identifiers.add(parameter.name.toLowerCase(Locale.ROOT));if(owner.returnVariable!=null)identifiers.add(owner.returnVariable.toLowerCase(Locale.ROOT));}
            for(Line line:lines){Matcher names=Pattern.compile(FlowEngine.IDENTIFIER_WORD_PATTERN).matcher(line.text);while(names.find())identifiers.add(names.group().toLowerCase(Locale.ROOT));}
        }
        Document read(){
            boolean begin=index<lines.size()&&one(lines.get(index).text,"INIZIO","BEGIN");if(begin)index++;
            Node start=node(START,"Inizio",begin?lines.get(0).number:-1);Part body=sequence(0);
            int endLine=index<lines.size()?lines.get(index).number:lines.get(lines.size()-1).number;
            if(index<lines.size()&&one(lines.get(index).text,"FINE","END"))index++;
            if(index!=lines.size())throw error(lines.get(index).number,"Chiusura o istruzione fuori posto.");
            if(body.entry==null||!body.exits.isEmpty()||doc.nodes.stream().noneMatch(n->n.kind==END)){Node end=node(END,"Fine",endLine);finish(body,end.id);}connect(new Port(start,"next"),body.entry);
            HashMap<String,String> targets=new HashMap<>();
            for(Label label:labels){String key=label.name.toUpperCase(Locale.ROOT);if(targets.containsKey(key))throw error(label.line,"Etichetta duplicata: "+label.name);targets.put(key,label.part.entry);}
            // References are resolved only after every structured region has its continuation.
            HashMap<String,String> aliases=new HashMap<>();for(Jump j:jumps){String target=targets.get(j.label.toUpperCase(Locale.ROOT));if(target==null)throw error(j.line,"Etichetta sconosciuta: "+j.label);aliases.put(j.part.entry,target);}
            for(Edge edge:doc.edges)edge.to=resolve(edge.to,aliases,endLine);
            // SCOLLEGATO is an explicit missing edge, not a hidden terminal block.
            doc.edges.removeIf(e->e.to.equals("?"));
            for(Line line:lines)if(line.note!=null&&!notesUsed.contains(line.number))throw error(line.number,"Metti NOTA immediatamente prima di un blocco.");
            doc.title="Da pseudocodice";FlowAnnotations.promote(doc);FlowAnnotations.placeImported(doc);if(owner==null)FlowCodec.check(doc);else FlowCodec.checkGraph(doc);return doc;
        }
        String resolve(String id,Map<String,String> aliases,int line){HashSet<String> seen=new HashSet<>();while(id.startsWith("@")){if(!seen.add(id))throw error(line,"Ciclo di soli VAI A: aggiungi almeno un blocco al ciclo.");String next=aliases.get(id);if(next==null)throw error(line,"Destinazione mancante.");id=next;}return id;}
        Node node(int kind,String text,int line){
            if(doc.nodes.size()>=maxNodes)throw error(line,"Massimo 500 blocchi nell'intero progetto.");
            if(text.length()>8192)throw error(line,"Un blocco supera 8192 caratteri.");
            Node n=new Node(kind,text,350,80+doc.nodes.size()*150);for(Line annotation:lines)if(annotation.number==line&&notesUsed.add(line)){n.comment=annotation.note;break;}doc.nodes.add(n);sourceLines.put(n.id,line);
            try{FlowEngine.checkBlock(n,functions,owner);}catch(IllegalArgumentException ex){throw error(line,ex.getMessage());}return n;
        }
        void connect(Port port,String target){doc.edges.add(new Edge(port.node.id,target,port.branch));}
        void finish(Part part,String target){if(part.entry==null)part.entry=target;for(Port p:part.exits)connect(p,target);part.exits.clear();}
        Part sequence(int depth){
            if(depth>MAX_DEPTH)throw error(index<lines.size()?lines.get(index).number:1,"Troppi livelli annidati (massimo 48).");
            Part result=new Part(null);ArrayList<Label> pending=new ArrayList<>();
            while(index<lines.size()){
                Line line=lines.get(index);String u=upper(line.text);
                if(one(u,"ALTRIMENTI","ELSE","FINE SE","FINESE","END IF","ENDIF","FINE MENTRE","FINEMENTRE","END WHILE","ENDWHILE","FINE PER","FINEPER","END FOR","ENDFOR","FINE","END")||prefix(u,"FINCHÉ ","FINCHE ","UNTIL "))break;
                if(line.text.matches(""+FlowEngine.IDENTIFIER_PATTERN+":")){Label label=new Label(line.text.substring(0,line.text.length()-1),null,line.number);pending.add(label);labels.add(label);index++;continue;}
                if(prefix(u,"ANNOTAZIONE ","ANNOTATION ")){String literal=line.text.substring(line.text.indexOf(' ')+1).trim();if(!noteLiteral(literal))throw error(line.number,"Usa ANNOTAZIONE seguito da un testo fra virgolette.");node(NOTE,annotationText(literal,line.number),line.number);index++;continue;}
                Part next=statement(depth);for(Label label:pending)label.part=next;pending.clear();
                if(result.entry==null)result.entry=next.entry;for(Port p:result.exits)connect(p,next.entry);result.exits=new ArrayList<>(next.exits);
            }
            if(!pending.isEmpty())throw error(pending.get(0).line,"Dopo l'etichetta inserisci un'istruzione (FERMA per terminare).");return result;
        }
        Part statement(int depth){
            Line line=lines.get(index++);String s=line.text,u=upper(s);Part part;
            if(prefix(u,"SE ","IF ")){
                String expression=condition(s,one(u.substring(0,2),"SE")?3:3,"ALLORA","THEN",line.number);
                Node n=node(DECISION,expression,line.number);Part yes=sequence(depth+1),no=new Part(null);
                if(index<lines.size()&&one(lines.get(index).text,"ALTRIMENTI","ELSE")){index++;no=sequence(depth+1);}
                require("FINE SE",line.number,"FINE SE","FINESE","END IF","ENDIF");
                part=new Part(n.id);arm(part,n,"true",yes);arm(part,n,"false",no);return part;
            }
            if(prefix(u,"MENTRE ","WHILE ")){
                Node n=node(DECISION,condition(s,u.startsWith("MENTRE ")?7:6,"FAI","DO",line.number),line.number);
                Part body=sequence(depth+1);require("FINE MENTRE",line.number,"FINE MENTRE","FINEMENTRE","END WHILE","ENDWHILE");finish(body,n.id);connect(new Port(n,"true"),body.entry);part=new Part(n.id);part.exits.add(new Port(n,"false"));return part;
            }
            if(prefix(u,"PER ","FOR ")){
                boolean italian=u.startsWith("PER ");String header=condition(s,4,"FAI","DO",line.number);
                Matcher m=Pattern.compile(italian?"^("+FlowEngine.IDENTIFIER_PATTERN+")\\s+DA\\s+(.+)$":"^("+FlowEngine.IDENTIFIER_PATTERN+")\\s*=\\s*(.+)$",Pattern.CASE_INSENSITIVE).matcher(header);
                if(!m.matches())throw error(line.number,"Usa PER i DA 1 A 5 PASSO 1 FAI.");
                String name=m.group(1),rest=m.group(2);int split=topToken(rest,italian?"A":"TO");
                if(split<0)throw error(line.number,"Manca il limite: usa DA 1 A 5 oppure = 1 TO 5.");
                String from=rest.substring(0,split).trim(),to=rest.substring(split+(italian?1:2)).trim();int stepAt=topToken(to,italian?"PASSO":"STEP");String stepText=stepAt<0?"1":to.substring(stepAt+(italian?5:4)).trim();if(stepAt>=0)to=to.substring(0,stepAt).trim();
                int step=0;boolean constant=false;try{step=Integer.parseInt(stepText);constant=true;}catch(NumberFormatException ignored){}
                if((constant&&step==0)||(!constant&&stepText.matches("[+-]?[0-9.]+")))throw error(line.number,"PASSO deve essere un intero diverso da zero, da -2147483648 a 2147483647.");
                try{FlowEngine.checkExpression(from,functions);FlowEngine.checkExpression(to,functions);FlowEngine.checkExpression(stepText,functions);}catch(IllegalArgumentException ex){throw error(line.number,ex.getMessage());}
                String limit,stride;do{forCount++;limit="limite_ciclo_"+forCount;stride="passo_ciclo_"+forCount;}while(identifiers.contains(limit)||identifiers.contains(stride));identifiers.add(limit);identifiers.add(stride);
                Node init=node(PROCESS,name+" = "+from+"; "+limit+" = "+to+(constant?"":"; "+stride+" = int("+stepText+", \"passo\")"),line.number),check=node(DECISION,constant?name+(step>0?" <= ":" >= ")+limit:forCondition(name,limit,stride),line.number);
                Part body=sequence(depth+1);require("FINE PER",line.number,"FINE PER","FINEPER","END FOR","ENDFOR");
                Node increment=node(PROCESS,constant?name+" = "+name+" + ("+step+")":name+" = "+name+" + "+stride,line.number);finish(body,increment.id);connect(new Port(init,"next"),check.id);connect(new Port(check,"true"),body.entry);connect(new Port(increment,"next"),check.id);part=new Part(init.id);part.exits.add(new Port(check,"false"));return part;
            }
            if(one(u,"RIPETI","REPEAT")){
                Part body=sequence(depth+1);if(index>=lines.size()||!prefix(upper(lines.get(index).text),"FINCHÉ ","FINCHE ","UNTIL "))throw error(line.number,"RIPETI richiede FINCHÉ condizione.");
                Line close=lines.get(index++);String expr=close.text.substring(close.text.indexOf(' ')+1).trim();Node n=node(DECISION,expr,close.number);finish(body,n.id);connect(new Port(n,"false"),body.entry);part=new Part(body.entry);part.exits.add(new Port(n,"true"));return part;
            }
            if(prefix(u,"VAI A ","GOTO ")){String name=s.substring(u.startsWith("VAI A ")?6:5).trim();if(!name.matches(""+FlowEngine.IDENTIFIER_PATTERN+""))throw error(line.number,"Dopo VAI A scrivi un'etichetta.");part=new Part("@jump"+jumps.size());jumps.add(new Jump(part,name,line.number));return part;}
            if(one(u,"SCOLLEGATO"))return new Part("?");
            if(one(u,"FERMA","STOP"))return new Part(node(END,"Fine",line.number).id);
            if(one(u,"PAUSA","PAUSA_DISATTIVATA")||prefix(u,"PAUSA SE ","PAUSA_DISATTIVATA SE ")){
                boolean enabled=!u.startsWith("PAUSA_DISATTIVATA");int offset=enabled?6:18;String condition=s.length()<=offset?"":s.substring(offset+3).trim();
                if(condition.length()>8192)throw error(line.number,"La condizione supera 8192 caratteri.");
                try{if(enabled)new FlowEngine.DebugCondition(condition);}catch(IllegalArgumentException ex){throw error(line.number,ex.getMessage());}
                Node pause=node(PAUSE,"Pausa",line.number);pause.breakpoint=enabled;pause.breakpointCondition=condition;part=new Part(pause.id);part.exits.add(new Port(pause,"next"));return part;
            }
            int kind=PROCESS;String content=s,prompt="";
            if(prefix(u,"AVANZA ","FORWARD ")){kind=TURTLE_FORWARD;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"RUOTA ","TURN ")){kind=TURTLE_TURN;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"PENNA ","PEN ")){kind=TURTLE_PEN;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(one(u,"ORIGINE_TARTARUGA","HOME_TURTLE")){kind=TURTLE_HOME;content=FlowTurtle.defaultText(kind);}
            else if(one(u,"PULISCI_DISEGNO","CLEAR_DRAWING")){kind=TURTLE_CLEAR;content=FlowTurtle.defaultText(kind);}
            else if(prefix(u,"APRI_FILE ","OPEN_FILE ")){kind=OPEN_FILE;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"LEGGI_FILE ","READ_FILE ")){kind=READ_FILE;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"SCRIVI_FILE ","WRITE_FILE ")){kind=WRITE_FILE;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"CHIUDI_FILE ","CLOSE_FILE ")){kind=CLOSE_FILE;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"LEGGI ","READ ")){kind=INPUT;content=s.substring(u.startsWith("LEGGI ")?6:5).trim();int comma=topToken(content,",");if(comma>=0){prompt=quoted(content.substring(comma+1).trim(),line.number);content=content.substring(0,comma).trim();if(prompt.length()>1024)throw error(line.number,"La domanda supera 1024 caratteri.");}}
            else if(prefix(u,"STAMPA_CONTINUA ","WRITE_CONTINUE ","OUTPUT_CONTINUE ")){kind=OUTPUT;content=FlowOutput.store(s.substring(s.indexOf(' ')+1).trim(),false);}
            else if(prefix(u,"STAMPA ","SCRIVI ","WRITE ","OUTPUT ")){kind=OUTPUT;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"CHIAMA ","CALL ")){kind=CALL;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"DICHIARA ","DECLARE ")){kind=DECLARE;content=s.substring(s.indexOf(' ')+1).trim();}
            else if(prefix(u,"ESEGUI "))content=s.substring(7).trim();
            if(kind==PROCESS)content=arrows(content);
            Node n=node(kind,content,line.number);n.prompt=prompt;part=new Part(n.id);part.exits.add(new Port(n,"next"));return part;
        }
        void arm(Part result,Node n,String branch,Part body){if(body.entry==null)result.exits.add(new Port(n,branch));else{connect(new Port(n,branch),body.entry);result.exits.addAll(body.exits);}}
        void require(String expected,int line,String... choices){if(index>=lines.size()||!one(lines.get(index).text,choices))throw error(index<lines.size()?lines.get(index).number:line,"Manca "+expected+".");index++;}
    }
    private static int topToken(String s,String token){char quote=0;boolean escape=false;int depth=0;for(int i=0;i<s.length();i++){char c=s.charAt(i);if(quote!=0){if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote=0;}else if(c=='\''||c=='"')quote=c;else if(c=='('||c=='[')depth++;else if(c==')'||c==']')depth--;else if(depth==0&&i+token.length()<=s.length()&&s.regionMatches(true,i,token,0,token.length())&&(token.equals(",")||(i>0&&Character.isWhitespace(s.charAt(i-1))&&i+token.length()<s.length()&&Character.isWhitespace(s.charAt(i+token.length())))))return i;}return -1;}
    private static String condition(String value,int offset,String a,String b,int line){String result=value.substring(offset).trim(),u=upper(result);if(u.endsWith(" "+a))result=result.substring(0,result.length()-a.length()).trim();else if(u.endsWith(" "+b))result=result.substring(0,result.length()-b.length()).trim();return result;}
    private static boolean noteLiteral(String value){if(value.length()<2||value.charAt(0)!='"'||value.charAt(value.length()-1)!='"')return false;for(int i=1;i<value.length()-1;i++){if(value.charAt(i)=='"')return false;if(value.charAt(i)=='\\'&&++i>=value.length()-1)return false;}return true;}
        private static String annotationText(String value,int line){if(value.length()>16386||!noteLiteral(value))throw new ParseError(line,"Usa ANNOTAZIONE seguito da un testo fra virgolette (massimo 8192 caratteri).");StringBuilder text=new StringBuilder();for(int i=1;i<value.length()-1;i++){char c=value.charAt(i);if(c=='\\'){c=value.charAt(++i);switch(c){case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;case '\\':case '"':case (char)39:break;default:throw new ParseError(line,"Escape non valido nella nota: usa \\\\ per una barra.");}}text.append(c);if(text.length()>8192)throw new ParseError(line,"La nota supera 8192 caratteri.");}return text.toString();}
    private static String quoted(String value,int line){try{FlowEngine.Value v=FlowEngine.evaluate(value,Collections.emptyMap());if(!v.isText()||!value.startsWith("\""))throw error(line,"La domanda deve essere un testo fra virgolette.");return v.format();}catch(IllegalArgumentException e){throw error(line,"La domanda deve essere un testo fra virgolette.");}}
    private static String upper(String s){return s.trim().toUpperCase(Locale.ROOT).replaceAll(" +"," ");}
    private static boolean one(String s,String... values){for(String value:values)if(upper(s).equals(value))return true;return false;}
    private static boolean prefix(String s,String... values){for(String value:values)if(s.startsWith(value))return true;return false;}
    private static String comment(String s){char quote=0;boolean escape=false;for(int i=0;i<s.length();i++){char c=s.charAt(i);if(quote!=0){if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote=0;}else if(c=='\''||c=='"')quote=c;else if(c=='/'&&i+1<s.length()&&s.charAt(i+1)=='/')return s.substring(0,i);}return s;}
    private static String arrows(String s){StringBuilder b=new StringBuilder();char quote=0;boolean escape=false;for(int i=0;i<s.length();i++){char c=s.charAt(i);if(quote!=0){b.append(c);if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote=0;}else if(c=='\''||c=='"'){quote=c;b.append(c);}else if(c=='←')b.append("<-");else b.append(c);}return b.toString();}
    private static String literal(String s){return "\""+s.replace("\\","\\\\").replace("\"","\\\"").replace("\n","\\n").replace("\r","\\r").replace("\t","\\t")+"\"";}
    private static String flat(String s,boolean process){StringBuilder b=new StringBuilder();char quote=0;boolean escape=false;for(int i=0;i<s.length();i++){char c=s.charAt(i);if(quote!=0){if(c=='\n')b.append("\\n");else if(c=='\r')b.append("\\r");else b.append(c);if(escape)escape=false;else if(c=='\\')escape=true;else if(c==quote)quote=0;}else if(c=='\''||c=='"'){quote=c;b.append(c);}else if(c=='\n'||c=='\r'){b.append(process?"; ":" ");if(c=='\r'&&i+1<s.length()&&s.charAt(i+1)=='\n')i++;}else b.append(c);}return b.toString().trim();}

    public static String encode(Document doc){return encodeMapped(doc,false).text;}
    static FlowSourceText generate(Document doc){Mapped result=encodeMapped(doc,true);return FlowSourceText.fromPseudocode(doc,result.text,result.mapping);}
    private static final class Mapped {String text;List<List<FlowSourceText.Origin>> mapping;}
    private static Mapped encodeMapped(Document doc,boolean mapped){
        FlowCodec.check(doc);Map<String,Function> functions=FlowFunctions.catalog(doc);StringBuilder text=new StringBuilder();Mapped result=new Mapped();List<List<FlowSourceText.Origin>> mapping=mapped?new ArrayList<>():null;result.mapping=mapping;if(doc.requiresDeclarations()){text.append("VARIABILI DICHIARATE\n\n");if(mapped){mapping.add(Collections.emptyList());mapping.add(Collections.emptyList());}}
        for(Function f:functions.values()){text.append(signature(f)).append('\n');if(mapped){Node start=null;for(Node n:f.body.nodes)if(n.kind==START){start=n;break;}mapping.add(Collections.singletonList(new FlowSourceText.Origin(f.id,start.id)));}Writer body=new Writer(f.body,functions,f,true,mapped);String value=body.structured();text.append(value==null?body.explicit():value).append("FINE FUNZIONE\n\n");if(mapped){mapping.addAll(body.mapping);mapping.add(Collections.emptyList());mapping.add(Collections.emptyList());}}
        Writer writer=new Writer(doc,functions,null,true,mapped);String value=writer.structured();text.append(value==null?writer.explicit():value);if(mapped)mapping.addAll(writer.mapping);
        if(text.length()>MAX_TEXT)throw new IllegalArgumentException("Il pseudocodice supera 256 Ki caratteri. Dividi il diagramma.");result.text=text.toString();return result;
    }
    private static final class NotStructured extends RuntimeException {}

    static String forCondition(String name,String limit,String stride){return "("+stride+" > 0 e "+name+" <= "+limit+") o ("+stride+" < 0 e "+name+" >= "+limit+")";}
    static String compact(String text){return text.replaceAll("\\s+","").toLowerCase(Locale.ROOT);}
    static boolean mentions(String text,String word){
        char quote=0;boolean escaped=false;for(int i=0;i<text.length();i++){char c=text.charAt(i);if(quote!=0){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote=0;continue;}if(c=='\''||c=='"'){quote=c;continue;}if(Character.isLetter(c)||c=='_'){int start=i;while(i+1<text.length()&&(Character.isLetterOrDigit(text.charAt(i+1))||text.charAt(i+1)=='_'))i++;if(text.substring(start,i+1).equalsIgnoreCase(word))return true;}}return false;
    }
    static ArrayList<String[]> assignments(String text){
        ArrayList<String[]> result=new ArrayList<>();char quote=0;boolean escaped=false;int start=0,depth=0;
        for(int i=0;i<=text.length();i++){char c=i==text.length()?';':text.charAt(i);if(quote!=0){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c==quote)quote=0;continue;}if(c=='\''||c=='"'){quote=c;continue;}if(c=='('||c=='[')depth++;if(c==')'||c==']')depth--;
            if(depth==0&&(c==';'||c=='\n'||c=='\r')){String line=text.substring(start,i).trim();start=i+1;if(line.isEmpty())continue;Matcher m=Pattern.compile("\\A("+FlowEngine.IDENTIFIER_PATTERN+")\\s*(?:=|<-)\\s*(.+)\\z",Pattern.DOTALL).matcher(line);if(!m.matches())return new ArrayList<>();result.add(new String[]{m.group(1),m.group(2)});}}
        return result;
    }
    static final class ForPattern {Node check,increment;String body,exit,header,variable,from,to,step;String[] helperNames;}
    static ForPattern inspectFor(Document d,Node n){if(n==null||n.text==null||n.text.trim().isEmpty())return null;try{return new Writer(d,null,null,false).recognizeFor(n);}catch(IllegalArgumentException ex){return null;}}

    private static final class Writer {
        final Document doc;final List<Node> annotations;final Function owner;final HashMap<String,Node> nodes=new HashMap<>();final HashMap<String,String> names=new HashMap<>();final HashMap<String,String> edges=new HashMap<>();final HashSet<String> seen=new HashSet<>();final StringBuilder out=new StringBuilder();Node start;
        Writer(Document doc,Map<String,Function> functions,Function owner){this(doc,functions,owner,true);}
        final List<List<FlowSourceText.Origin>> mapping;
        Writer(Document doc,Map<String,Function> functions,Function owner,boolean validate){this(doc,functions,owner,validate,false);}
        Writer(Document doc,Map<String,Function> functions,Function owner,boolean validate,boolean mapped){mapping=mapped?new ArrayList<>():null;annotations=new ArrayList<>();for(Node n:doc.nodes)if(n.kind==NOTE)annotations.add(n);doc=FlowAnnotations.executable(doc);this.doc=doc;this.owner=owner;for(Node n:doc.nodes){nodes.put(n.id,n);names.put(n.id,"B"+(names.size()+1));if(n.kind==START){if(start!=null)throw new IllegalArgumentException("Mantieni un solo blocco Inizio per la conversione.");start=n;}}
            if(start==null)throw new IllegalArgumentException("Aggiungi un blocco Inizio prima della conversione.");
            for(Edge e:doc.edges){Node n=nodes.get(e.from);String key=e.from+"/"+e.branch;if(edges.containsKey(key)||n.kind==END||(n.kind==DECISION?!one(e.branch.toUpperCase(Locale.ROOT),"TRUE","FALSE"):!e.branch.equals("next"))||nodes.get(e.to).kind==START)throw new IllegalArgumentException("Correggi i collegamenti duplicati o non validi prima della conversione.");edges.put(key,e.to);}
            if(validate)for(Node n:doc.nodes)try{FlowEngine.checkBlock(n,functions,owner);}catch(IllegalArgumentException e){throw new IllegalArgumentException((owner==null?"":"Funzione «"+owner.name+"», ")+"blocco "+names.get(n.id)+": "+e.getMessage());}
        }

        ForPattern recognizeFor(Node init){
            if(init.kind!=PROCESS)return null;ArrayList<String[]> a=assignments(init.text);if(a.size()!=2&&a.size()!=3)return null;
            String name=a.get(0)[0],from=a.get(0)[1],limit=a.get(1)[0],to=a.get(1)[1],stride=a.size()==3?a.get(2)[0]:null,step=null;
            if(!limit.toLowerCase(Locale.ROOT).matches("limite_ciclo_[0-9]+")||name.equalsIgnoreCase(limit))return null;
            String next=next(init,"next");if(next==null)return null;Node check=nodes.get(next);if(check.kind!=DECISION)return null;
            ArrayList<Edge> incoming=new ArrayList<>();for(Edge e:doc.edges)if(e.to.equals(check.id))incoming.add(e);if(incoming.size()!=2)return null;
            Node increment=null;int initial=0;for(Edge e:incoming)if(e.from.equals(init.id))initial++;else increment=nodes.get(e.from);
            if(initial!=1||increment==null||increment.kind!=PROCESS||!check.id.equals(next(increment,"next")))return null;
            if(a.size()==3){
                if(!stride.toLowerCase(Locale.ROOT).matches("passo_ciclo_[0-9]+")||name.equalsIgnoreCase(stride))return null;
                Matcher guard=Pattern.compile("\\Aint\\s*\\((.*),\\s*\"passo\"\\s*\\)\\z",Pattern.DOTALL|Pattern.CASE_INSENSITIVE).matcher(a.get(2)[1]);if(!guard.matches())return null;step=guard.group(1).trim();
                if(!compact(check.text).equals(compact(forCondition(name,limit,stride)))||!compact(increment.text).equals(compact(name+"="+name+"+"+stride)))return null;
            }else{
                ArrayList<String[]> inc=assignments(increment.text);if(inc.size()!=1||!inc.get(0)[0].equalsIgnoreCase(name))return null;
                Matcher m=Pattern.compile("\\A"+Pattern.quote(name.toLowerCase(Locale.ROOT))+"\\+\\(([+-]?[0-9]+)\\)\\z").matcher(compact(inc.get(0)[1]));if(!m.matches())return null;int value;try{value=Integer.parseInt(m.group(1));}catch(NumberFormatException ex){return null;}if(value==0)return null;step=Integer.toString(value);
                if(!compact(check.text).equals(compact(name+(value>0?"<=":">=")+limit)))return null;
            }
            for(String helper:new String[]{limit,stride})if(helper!=null){
                if(mentions(from,helper)||mentions(to,helper)||mentions(step,helper))return null;
                if(owner!=null){for(Parameter p:owner.parameters)if(p.name.equalsIgnoreCase(helper))return null;if(owner.returnVariable.equalsIgnoreCase(helper))return null;}
                for(Node n:doc.nodes)if(n!=init&&n!=check&&n!=increment&&mentions(n.text==null?"":n.text,helper))return null;
            }
            String body=next(check,"true"),exit=next(check,"false");if(body==null||exit==null||exit.equals(increment.id)||exit.equals(check.id))return null;
            HashSet<String> region=new HashSet<>();ArrayDeque<String> queue=new ArrayDeque<>();queue.add(body);
            while(!queue.isEmpty()){String id=queue.removeFirst();if(id.equals(increment.id))continue;if(id.equals(exit)||id.equals(check.id)||id.equals(init.id))return null;if(!region.add(id))continue;for(String branch:new String[]{"next","true","false"}){String dest=next(nodes.get(id),branch);if(dest!=null)queue.add(dest);}}
            region.add(increment.id);for(Edge e:doc.edges)if(region.contains(e.to)&&!region.contains(e.from)&&!(e.from.equals(check.id)&&e.branch.equals("true")))return null;
            ForPattern f=new ForPattern();f.helperNames=stride==null?new String[]{limit}:new String[]{limit,stride};f.check=check;f.increment=increment;f.body=body;f.exit=exit;f.variable=name;f.from=from;f.to=to;f.step=step;f.header="PER "+name+" DA "+flat(from,false)+" A "+flat(to,false)+" PASSO "+flat(step,false)+" FAI";return f;
        }

        String next(Node n,String branch){return edges.get(n.id+"/"+branch);}
        void line(int depth,String text,Node... origins){for(int i=0;i<depth;i++)out.append("    ");out.append(text).append('\n');if(mapping!=null){List<FlowSourceText.Origin> row=new ArrayList<>();for(Node n:origins)row.add(new FlowSourceText.Origin(owner==null?null:owner.id,n.id));mapping.add(row);}}
        void annotationHeader(){for(Node note:annotations)line(0,"ANNOTAZIONE "+literal(note.text==null?"":note.text),note);}
        void note(int depth,Node n){if(n.comment!=null&&!n.comment.isEmpty())line(depth,"NOTA "+literal(n.comment),n);}
        String command(Node n){if(n.kind==PAUSE)return (n.breakpoint?"PAUSA":"PAUSA_DISATTIVATA")+(n.breakpointCondition==null||n.breakpointCondition.trim().isEmpty()?"":" SE "+flat(n.breakpointCondition,false));if(n.kind==TURTLE_FORWARD)return "AVANZA "+flat(n.text,false);if(n.kind==TURTLE_TURN)return "RUOTA "+flat(n.text,false);if(n.kind==TURTLE_PEN)return "PENNA "+flat(n.text,false);if(n.kind==TURTLE_HOME)return "ORIGINE_TARTARUGA";if(n.kind==TURTLE_CLEAR)return "PULISCI_DISEGNO";if(n.kind==OPEN_FILE)return "APRI_FILE "+flat(n.text,false);if(n.kind==READ_FILE)return "LEGGI_FILE "+flat(n.text,false);if(n.kind==WRITE_FILE)return "SCRIVI_FILE "+flat(n.text,false);if(n.kind==CLOSE_FILE)return "CHIUDI_FILE "+flat(n.text,false);if(n.kind==INPUT)return "LEGGI "+n.text.trim()+(n.prompt==null||n.prompt.isEmpty()?"":", "+literal(n.prompt));if(n.kind==OUTPUT)return (FlowOutput.newline(n.text)?"STAMPA ":"STAMPA_CONTINUA ")+flat(FlowOutput.expression(n.text),false);if(n.kind==DECLARE)return "DICHIARA "+flat(FlowDeclaration.body(n.text),false);if(n.kind==CALL)return "CHIAMA "+flat(n.text,false);if(n.kind==PROCESS)return "ESEGUI "+flat(n.text,true);return "FERMA";}
        String structured(){try{seen.add(start.id);note(0,start);line(0,"INIZIO",start);annotationHeader();walk(next(start,"next"),null,0,new HashSet<String>());if(seen.size()!=nodes.size())throw new NotStructured();line(0,"FINE");return out.toString();}catch(NotStructured ex){return null;}}
        void walk(String id,String stop,int depth,Set<String> boundaries){
            if(depth>MAX_DEPTH)throw new NotStructured();
            while(stop==null||!Objects.equals(id,stop)){
                if(id==null||boundaries.contains(id)||!seen.add(id))throw new NotStructured();Node n=nodes.get(id);note(depth,n);
                if(n.kind==END){line(depth,"FERMA",n);return;}
                ForPattern loop=recognizeFor(n);if(loop!=null&&(loop.check.comment==null||loop.check.comment.isEmpty())&&(loop.increment.comment==null||loop.increment.comment.isEmpty())){
                    if(!seen.add(loop.check.id)||seen.contains(loop.increment.id))throw new NotStructured();
                    HashSet<String> inside=new HashSet<>(boundaries);if(stop!=null)inside.add(stop);inside.add(loop.check.id);inside.add(loop.exit);
                    line(depth,loop.header,n,loop.check,loop.increment);walk(loop.body,loop.increment.id,depth+1,inside);seen.add(loop.increment.id);line(depth,"FINE PER",loop.increment);id=loop.exit;continue;
                }
                if(n.kind!=DECISION){line(depth,command(n),n);id=next(n,"next");continue;}
                String yes=next(n,"true"),no=next(n,"false");if(yes==null||no==null)throw new NotStructured();
                HashSet<String> block=new HashSet<>(boundaries);if(stop!=null)block.add(stop);block.add(n.id);
                boolean yloop=reach(yes,n.id,no,block),nloop=reach(no,n.id,yes,block);
                if(yloop!=nloop){String body=yloop?yes:no,exit=yloop?no:yes;line(depth,"MENTRE "+(yloop?flat(n.text,false):"non ("+flat(n.text,false)+")")+" FAI",n);walk(body,n.id,depth+1,block);line(depth,"FINE MENTRE",n);id=exit;continue;}
                String join=join(yes,no,block);if(join==null)throw new NotStructured();line(depth,"SE "+flat(n.text,false)+" ALLORA",n);walk(yes,join,depth+1,block);line(depth,"ALTRIMENTI",n);walk(no,join,depth+1,block);line(depth,"FINE SE",n);id=join;
            }
        }
        boolean reach(String from,String target,String avoid,Set<String> blocked){HashSet<String> seen=new HashSet<>();ArrayDeque<String> q=new ArrayDeque<>();q.add(from);while(!q.isEmpty()){String id=q.removeFirst();if(id.equals(target))return true;if(id.equals(avoid)||blocked.contains(id)||!seen.add(id))continue;Node n=nodes.get(id);for(String b:new String[]{"next","true","false"}){String to=next(n,b);if(to!=null)q.add(to);}}return false;}
        HashMap<String,Integer> distances(String from,Set<String> blocked){HashMap<String,Integer> distance=new HashMap<>();ArrayDeque<String> q=new ArrayDeque<>();distance.put(from,0);q.add(from);while(!q.isEmpty()){String id=q.removeFirst();if(blocked.contains(id))continue;Node n=nodes.get(id);for(String b:new String[]{"next","true","false"}){String to=next(n,b);if(to!=null&&!distance.containsKey(to)){distance.put(to,distance.get(id)+1);q.add(to);}}}return distance;}
        String join(String yes,String no,Set<String> blocked){Map<String,Integer> a=distances(yes,blocked),b=distances(no,blocked);String best=null;int score=Integer.MAX_VALUE;for(Node n:doc.nodes){Integer x=a.get(n.id),y=b.get(n.id);if(x==null||y==null||seen.contains(n.id))continue;int cost=Math.max(x,y)*1000+x+y;if(cost<score){score=cost;best=n.id;}}return best;}
        String destination(Node n,String branch){String to=next(n,branch);return to==null?"SCOLLEGATO":"VAI A "+names.get(to);}
        String explicit(){out.setLength(0);if(mapping!=null)mapping.clear();line(0,"// Percorsi espliciti: le etichette conservano cicli, rami e blocchi separati.");note(0,start);line(0,"INIZIO",start);annotationHeader();line(0,destination(start,"next"),start);for(Node n:doc.nodes){if(n==start)continue;line(0,"");line(0,names.get(n.id)+":",n);note(0,n);if(n.kind==DECISION){line(0,"SE "+flat(n.text,false)+" ALLORA",n);line(1,destination(n,"true"),n);line(0,"ALTRIMENTI",n);line(1,destination(n,"false"),n);line(0,"FINE SE",n);}else{line(0,command(n),n);if(n.kind!=END)line(0,destination(n,"next"),n);}}line(0,"FINE");return out.toString();}
    }
}
