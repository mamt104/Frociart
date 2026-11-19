package it.frociart.app;

import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Beginner tasks operate on real .frociart documents and the existing interpreter. */
public final class FlowTutor {
    private FlowTutor() {}
    public static final int ADD=0,WRITE=1,CONNECT=2,VALUE=3;
    public static final class Part {
        public final String id,answer,initial;public final int kind;public final boolean present;
        public Part(String id,int kind,String answer,String initial,boolean present){this.id=id;this.kind=kind;this.answer=answer;this.initial=initial;this.present=present;}
    }
    public static final class Link {
        public final String from,to,branch;public final boolean present;
        public Link(String from,String to,String branch,boolean present){this.from=from;this.to=to;this.branch=branch;this.present=present;}
    }
    public static final class Step {
        public final int action;public final String node,title,instruction,hint,from,branch,to;
        public Step(int action,String node,String title,String instruction,String hint,String from,String branch,String to){this.action=action;this.node=node;this.title=title;this.instruction=instruction;this.hint=hint;this.from=from;this.branch=branch;this.to=to;}
    }
    public static final class Trial {
        public final String[] inputs,literals;
        public Trial(String[] inputs,String[] literals){this.inputs=inputs.clone();this.literals=literals.clone();}
    }
    public static final class Lesson {
        public final String id,unit,title,goal,explanation,reading,example,warning,assessment,criteria;
        public final String[] terms,introduces,variables,skills;
        public final Part[] parts;public final Link[] links;public final Step[] steps;public final Trial[] trials;
        public final boolean guidedLinks;
        public Lesson(String id,String unit,String title,String goal,String explanation,String reading,String example,String warning,String[] terms,String[] introduces,String[] variables,Part[] parts,Link[] links,Step[] steps,Trial[] trials,boolean guidedLinks,String assessment,String criteria,String[] skills){
            this.assessment=assessment;this.criteria=criteria;this.skills=skills.clone();this.id=id;this.unit=unit;this.title=title;this.goal=goal;this.explanation=explanation;this.reading=reading;this.example=example;this.warning=warning;this.terms=terms.clone();this.introduces=introduces.clone();this.variables=variables.clone();this.parts=parts.clone();this.links=links.clone();this.steps=steps.clone();this.trials=trials.clone();this.guidedLinks=guidedLinks;
        }
        public Part part(String id){for(Part p:parts)if(p.id.equals(id))return p;return null;}
        public Document seed(){Document d=new Document();d.title=title;for(Part p:parts)if(p.present)d.nodes.add(node(p,p.initial));for(Link e:links)if(e.present&&d.find(e.from)!=null&&d.find(e.to)!=null)d.edges.add(new Edge(e.from,e.to,e.branch));layout(d);return d;}
        public Document solution(){Document d=new Document();d.title=title;for(Part p:parts)d.nodes.add(node(p,p.answer));for(Link e:links)d.edges.add(new Edge(e.from,e.to,e.branch));layout(d);return d;}
    }
    private static Node node(Part p,String text){Node n=new Node(p.kind,text,0,0);n.id=p.id;return n;}
    public static void layout(Document d){FlowAutoLayout.apply(d);}
    public static boolean insert(Lesson lesson,Document d,String target,int kind){
        Part p=lesson.part(target);if(p==null||d.find(target)!=null||d.nodes.size()>=MAX_NODES)return false;
        Node n=new Node(kind,kind==START?"Inizio":kind==END?"Fine":"",0,0);n.id=p.id;d.nodes.add(n);
        if(lesson.guidedLinks)for(Link e:lesson.links)if((e.from.equals(target)||e.to.equals(target))&&d.find(e.from)!=null&&d.find(e.to)!=null&&d.outgoing(e.from,e.branch)==null)d.edges.add(new Edge(e.from,e.to,e.branch));
        layout(d);return true;
    }
    public static boolean connect(Document d,String from,String branch,String to){
        Node a=d.find(from),b=d.find(to);if(a==null||b==null||a.kind==END||b.kind==START)return false;
        if(a.kind==DECISION?!branch.equals("true")&&!branch.equals("false"):!branch.equals("next"))return false;
        Edge old=d.outgoing(from,branch);if(old!=null&&old.to.equals(to))return false;if(old!=null)d.edges.remove(old);d.edges.add(new Edge(from,to,branch));return true;
    }
    public static int recoverStep(Lesson lesson,int requested,Document d){
        int limit=Math.max(0,Math.min(requested,lesson.steps.length));for(int i=0;i<limit;i++){Step s=lesson.steps[i];Node n=d.find(s.node);Part p=lesson.part(s.node);
            if(s.action==ADD&&(n==null||n.kind!=p.kind))return i;
            if(s.action==WRITE&&(n==null||n.text==null||n.text.trim().isEmpty()))return i;
            if(s.action==CONNECT){Edge e=d.outgoing(s.from,s.branch);if(e==null||!e.to.equals(s.to))return i;}
        }return limit;
    }
    public static final class Feedback {
        public boolean pass;public String code,title,why,fix,nodeId;public int passed,total;public String trace="";public Set<String> executed=new HashSet<>();
        public Feedback(boolean pass,String code,String title,String why,String fix,String nodeId,int passed,int total){this.pass=pass;this.code=code;this.title=title;this.why=why;this.fix=fix;this.nodeId=nodeId;this.passed=passed;this.total=total;}
    }
    public static Feedback no(String code,String title,String why,String fix,String node){return new Feedback(false,code,title,why,fix,node,0,0);}
    public static Feedback yes(String title,String why){return new Feedback(true,"ok",title,why,"",null,0,0);}
    public static Feedback inputRule(Lesson lesson,String variable,String raw){
        if(lesson==null)return null;FlowEngine.Value v;try{v=FlowEngine.parseInput(raw);}catch(IllegalArgumentException e){return FlowTutorDiagnostics.runtime(e.getMessage(),null);}
        for(String role:lesson.variables){String[] cells=role.split("\\|",-1);if(cells.length<3||!cells[0].equalsIgnoreCase(variable))continue;String expected=cells[2];
            if(expected.startsWith("numero")&&!v.isNumber())return no("input-type","Questo esercizio chiede un numero","Il dato inserito viene letto come "+type(v)+", non come numero.","Scrivi un valore come 5, senza nome della variabile, operazioni o virgolette.",null);
            if(expected.equals("testo")&&!v.isText())return no("input-type","Qui proviamo un testo","Il dato inserito viene riconosciuto come "+type(v)+".","Per questo esempio scrivi un nome come Ada, senza virgolette.",null);
            if(v.isNumber()&&expected.contains("intero")&&v.asNumber()!=Math.floor(v.asNumber()))return no("input-integer","Serve un intero","Un intero non ha una parte decimale. L'obiettivo di questa lezione è definito per gli interi.","Prova 0, 1, 2, 3 e così via, oppure gli altri interi consentiti dalla missione.",null);
            if(v.isNumber()&&expected.contains("non negativo")&&v.asNumber()<0)return no("input-range","Qui il valore parte da zero","La missione richiede un valore non negativo: zero o maggiore.","Scegli 0 oppure un numero positivo, come 3.",null);
        }return null;
    }
    public static boolean actionRequired(Lesson lesson,int index,Document d){
        if(lesson==null||index<0||index>=lesson.steps.length)return false;
        Step s=lesson.steps[index];Node n=d.find(s.node);
        if(s.action==ADD)return n==null;
        if(s.action==WRITE)return n!=null&&(n.text==null||n.text.trim().isEmpty());
        if(s.action==CONNECT){Edge e=d.outgoing(s.from,s.branch);return e==null||!e.to.equals(s.to);}
        return false;
    }
    public static Feedback checkStep(Lesson lesson,int index,Document source,String runtimeValue){
        if(index<0||index>=lesson.steps.length)return no("step","Passo non disponibile","Il percorso non contiene questo passo.","Riapri la lezione.",null);
        Step s=lesson.steps[index];Part p=lesson.part(s.node);Node n=source.find(s.node);
        if(s.action==VALUE){
            Feedback rule=inputRule(lesson,p==null?"":p.answer,runtimeValue);
            if(rule!=null)return new Feedback(false,rule.code,rule.title,rule.why,rule.fix+"\n"+s.instruction,s.node,0,0);
            FlowEngine.Value expected=FlowEngine.parseInput(runtimeValue);
            Trial t=new Trial(new String[]{runtimeValue},new String[]{valueLiteral(expected)});return execute(source,t,0,1);

        }
        if(s.action==CONNECT){Edge e=source.outgoing(s.from,s.branch);return e!=null&&e.to.equals(s.to)?yes("Freccia collegata","Il percorso ora va dal blocco di partenza all'arrivo scelto."):no("connection","Manca questa freccia","Le caselle vicine non sono automaticamente collegate.",s.instruction,s.from);}
        if(n==null)return no("missing-block","Aggiungi il blocco","Questo passaggio deve esistere nel tuo diagramma.",s.instruction,s.node);
        if(p==null||n.kind!=p.kind)return no("block-kind","Questo blocco ha un altro compito","Hai scelto "+kindName(n.kind)+"; per questo passo serve "+(p==null?"un altro blocco":kindName(p.kind))+".",s.instruction,s.node);
        if(s.action==ADD)return yes("Blocco aggiunto",lesson.guidedLinks?"La guida lo ha agganciato al punto indicato. Il contenuto lo scriverai tu nel passo successivo.":"Ora puoi scrivere il contenuto e collegare le frecce richieste.");
        Feedback syntax=FlowTutorDiagnostics.before(n);if(syntax!=null)return syntax;
        if(n.kind==INPUT)return yes("Nome valido","Il blocco conserverà il dato in "+n.text.trim()+". Usa lo stesso nome nelle caselle che leggono questo dato. Rinomina variabile aggiorna gli usi senza modificare i messaggi.");
        boolean ready=true;for(FlowEngine.Issue issue:FlowEngine.validate(source))if(issue.error){ready=false;break;}if(ready&&check(lesson,source).pass)return yes("Il tuo percorso funziona","Ho eseguito il tuo foglio così com’è su "+lesson.trials.length+" casi. Non devi copiare il mio esempio.");
        Document context=statementContext(lesson,source,n);
        Feedback result=check(lesson,context);if(result.pass)return yes("Istruzione verificata","Ho provato la tua casella nel resto dell'esempio già pronto, usando "+lesson.trials.length+" dati di prova. Non ho completato il tuo foglio. La prova finale eseguirà tutto il tuo flowchart.");
        result.nodeId=n.id;result.why="Controllo della tua casella nel contesto preparato dell'esempio.\n"+result.why;result.fix=s.hint+"\n"+result.fix;result.trace=result.trace.replace("del tuo diagramma","dell'esempio con la tua casella");return result;

    }
    public static Feedback check(Lesson lesson,Document source){
        if(lesson==null||source==null)return no("missing-lesson","Manca la lezione","Non è stato trovato un esercizio da controllare.","Torna al percorso.",null);
        for(Node n:source.nodes){Feedback syntax=FlowTutorDiagnostics.before(n);if(syntax!=null)return syntax;}
        for(FlowEngine.Issue issue:FlowEngine.validate(source))if(issue.error)return FlowTutorDiagnostics.issue(issue);
        Feedback skill=checkSkills(lesson,source);if(skill!=null)return skill;
        int passed=0;Feedback first=null;
        for(Trial t:lesson.trials){Feedback result=execute(source,t,passed,lesson.trials.length,lesson);if(result.pass)passed++;else if(first==null)first=result;}
        if(first!=null){first.passed=passed;first.total=lesson.trials.length;first.why=passed+" prove superate su "+lesson.trials.length+". Il foglio e i passi completati restano al loro posto.\n\nUn caso da correggere:\n"+first.why;
            for(Node n:source.nodes)if(first.executed.contains(n.id)){String hint=FlowTutorDiagnostics.misconception(lesson,n);if(!hint.isEmpty())first.fix+="\n\nRegola da rileggere in «"+n.text+"»: "+hint;}return first;}
        return new Feedback(true,"complete","Flowchart completato",passed+" casi superati sul tuo diagramma. Hai costruito un percorso eseguibile.","Puoi provare altri valori con Prova passo passo.",null,passed,lesson.trials.length);
    }
    private static String valueLiteral(FlowEngine.Value value){return value.isText()?"\""+value.format().replace("\\","\\\\").replace("\"","\\\"").replace("\n","\\n").replace("\r","\\r").replace("\t","\\t")+"\"":value.format();}
    private static String snippet(String text){text=text==null?"":text.replace('\n',' ').replace('\r',' ');return text.length()<=150?text:text.substring(0,147)+"…";}
    private static Feedback evidence(Feedback f,ArrayDeque<String> trace,Set<String> visited,int passed,int total){f.executed=visited;f.passed=passed;f.total=total;f.trace=trace.isEmpty()?"":"Ultimi passaggi del tuo diagramma in questa prova:\n"+String.join("\n",trace);return f;}
    public static Feedback execute(Document source,Trial trial,int passed,int total){return execute(source,trial,passed,total,null);}
    public static Feedback execute(Document source,Trial trial,int passed,int total,Lesson assessment){
        FlowEngine.Runner r=new FlowEngine.Runner(source);ArrayList<FlowEngine.Value> actual=new ArrayList<>();ArrayList<String> outputNodes=new ArrayList<>();HashSet<String> visited=new HashSet<>();ArrayDeque<String> trace=new ArrayDeque<>();String wrongNode=null,focus=null,lastDecision=null;Map<String,String> origins=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);Map<String,FlowEngine.Value> wrongValues=null;int input=0;
        while(r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR&&r.steps<2000){
            Node n=source.find(r.currentNodeId);String current=n==null?"":kindName(n.kind)+" «"+snippet(n.text)+"»";
            if(r.state==FlowEngine.State.WAITING){if(input>=trial.inputs.length)return evidence(no("extra-input","Chiedi un dato di troppo",testContext(trial),"Controlla gli Input attraversati, anche nei cicli.",r.currentNodeId),trace,visited,passed,total);r.submitInput(trial.inputs[input++]);}
            else{if(n!=null&&n.kind==OUTPUT)try{FlowEngine.Value value=FlowEngine.evaluate(n.text,r.variables);int i=actual.size();if(wrongNode==null&&(i>=trial.literals.length||!matches(assessment,value,literal(trial.literals[i])))){wrongNode=n.id;focus=n.id;wrongValues=new LinkedHashMap<>(r.variables);for(String name:FlowEngine.referencedVariables(n.text))if(origins.containsKey(name)){focus=origins.get(name);break;}if(focus.equals(n.id)&&lastDecision!=null&&labelOutput(assessment)&&value.isText()){boolean known=false;for(Trial candidate:assessment.trials)for(String wanted:candidate.literals){FlowEngine.Value expected=literal(wanted);if(expected.isText()&&labelKey(value.format()).equalsIgnoreCase(labelKey(expected.format())))known=true;}if(known)focus=lastDecision;}}actual.add(value);outputNodes.add(n.id);}catch(IllegalArgumentException ignored){}r.step();}
            if(n!=null){visited.add(n.id);if(r.state!=FlowEngine.State.ERROR&&r.state!=FlowEngine.State.WAITING){if(n.kind==PROCESS||n.kind==INPUT)for(String name:FlowTutorGuide.inspect(n).writes)origins.put(name,n.id);if(n.kind==DECISION)lastDecision=n.id;}}if(r.state!=FlowEngine.State.WAITING){trace.addLast(current+" → "+snippet(r.lastMessage));if(trace.size()>12)trace.removeFirst();}
        }
        if(r.state==FlowEngine.State.ERROR){Feedback failure=FlowTutorDiagnostics.runtime(r.errorMessage,r.currentNodeId);failure.why=testContext(trial)+"\n"+failure.why;return evidence(failure,trace,visited,passed,total);}
        if(r.state!=FlowEngine.State.COMPLETED)return evidence(no("loop-limit","Il ciclo continua senza finire",testContext(trial)+"\nLa prova si è fermata a 2000 passaggi.","Controlla se la variabile della Decisione cambia e se esiste un'uscita verso Fine.",r.currentNodeId),trace,visited,passed,total);
        if(input!=trial.inputs.length)return evidence(no("missing-input","Un dato non viene letto",testContext(trial),"Attraversa tutti gli Input richiesti, nello stesso ordine.",null),trace,visited,passed,total);
        boolean good=actual.size()==trial.literals.length;if(good)for(int i=0;i<actual.size();i++)if(!matches(assessment,actual.get(i),literal(trial.literals[i]))){good=false;break;}
        if(!good){ArrayList<String> wanted=new ArrayList<>(),got=new ArrayList<>();for(String text:trial.literals)wanted.add(typed(literal(text)));for(FlowEngine.Value value:actual)got.add(typed(value));
            String fix=(focus!=null&&!focus.equals(wrongNode)?"Casella evidenziata: "+kindName(source.find(focus).kind)+" «"+source.find(focus).text+"». È l’ultima scrittura del dato letto oppure l’ultima Decisione prima del risultato.\n\n":"")+(wrongNode==null?"":FlowTutorGuide.explain(source.find(wrongNode),wrongValues)+"\n\n")+"Segui Prova passo passo con questi dati. Controlla dove il valore o il ramo cambia rispetto alla missione.";
            Feedback failure=new Feedback(false,"wrong-result","Questo caso dà un risultato diverso",testContext(trial)+"\nAtteso: "+(freeMessage(assessment)?"un messaggio di testo non vuoto, a tua scelta":wanted.isEmpty()?"nessun output":String.join(" · ",wanted))+"\nOttenuto: "+(got.isEmpty()?"nessun output":String.join(" · ",got)),fix,focus!=null?focus:wrongNode!=null?wrongNode:outputNodes.isEmpty()?null:outputNodes.get(outputNodes.size()-1),passed,total);return evidence(failure,trace,visited,passed,total);}
        return new Feedback(true,"case","Caso superato",testContext(trial),"",null,passed+1,total);
    }
    private static String typed(FlowEngine.Value value){return (value.isText()?"«"+value.format()+"»":value.format())+" ("+type(value)+")";}
    private static String testContext(Trial trial){return "Input della prova: "+(trial.inputs.length==0?"nessuno":String.join(" · ",trial.inputs));}
    public static String rewrite(String text,Map<String,String> names){
        StringBuilder out=new StringBuilder();if(text==null)text="";int i=0;
        while(i<text.length()){int start=i;char c=text.charAt(i++);
            if(c=='\''||c=='"'){boolean escape=false;while(i<text.length()){char next=text.charAt(i++);if(escape)escape=false;else if(next=='\\')escape=true;else if(next==c)break;}}
            else if(Character.isDigit(c)||c=='.'&&i<text.length()&&Character.isDigit(text.charAt(i))){while(i<text.length()&&(Character.isDigit(text.charAt(i))||text.charAt(i)=='.'))i++;if(i<text.length()&&(text.charAt(i)=='e'||text.charAt(i)=='E')){i++;if(i<text.length()&&(text.charAt(i)=='+'||text.charAt(i)=='-'))i++;while(i<text.length()&&Character.isDigit(text.charAt(i)))i++;}}
            else if(Character.isLetter(c)||c=='_'){while(i<text.length()&&(Character.isLetterOrDigit(text.charAt(i))||text.charAt(i)=='_'))i++;String token=text.substring(start,i),replacement=null;for(Map.Entry<String,String> entry:names.entrySet())if(entry.getKey().equalsIgnoreCase(token)){replacement=entry.getValue();break;}int next=i;while(next<text.length()&&Character.isWhitespace(text.charAt(next)))next++;if((next==text.length()||text.charAt(next)!='(')&&replacement!=null){out.append(replacement);continue;}}
            out.append(text,start,i);
        }return out.toString();
    }
    public static Set<String> variableNames(Document d,boolean definitionsOnly){TreeSet<String> names=new TreeSet<>(String.CASE_INSENSITIVE_ORDER);for(Node n:d.nodes){FlowTutorGuide.Inspection info=FlowTutorGuide.inspect(n);names.addAll(info.writes);if(!definitionsOnly)names.addAll(info.reads);}return names;}
    public static int rename(Document d,String oldName,String newName){
        if(!FlowEngine.identifier(oldName)||!FlowEngine.identifier(newName))throw new IllegalArgumentException("Scegli un nome valido: una lettera o _ iniziale; poi lettere, cifre o _. Evita parole riservate.");
        if(!oldName.equalsIgnoreCase(newName)&&variableNames(d,true).contains(oldName)&&variableNames(d,false).contains(newName))throw new IllegalArgumentException("Il nome "+newName+" è già usato da un altro dato. Scegli un nome libero per non unire due variabili diverse.");
        Map<String,String> names=Collections.singletonMap(oldName,newName);int changed=0;for(Node n:d.nodes)if(n.kind!=START&&n.kind!=END){String rewritten=rewrite(n.text,names);if(!rewritten.equals(n.text)){n.text=rewritten;changed++;}}return changed;
    }
    private static Document statementContext(Lesson lesson,Document actual,Node submitted){
        Document context=lesson.solution();Map<String,String> aliases=new LinkedHashMap<>();
        for(Part p:lesson.parts){Node before=context.find(p.id),after=actual.find(p.id);if(after==null||before.kind!=after.kind)continue;
            if(after.kind==INPUT&&FlowEngine.identifier(after.text.trim()))aliases.put(before.text.trim(),after.text.trim());
            else if(after.kind==PROCESS){List<String> a=FlowTutorGuide.inspect(before).writes,b=FlowTutorGuide.inspect(after).writes;if(a.size()==b.size())for(int i=0;i<a.size();i++)aliases.put(a.get(i),b.get(i));}}
        for(Node n:context.nodes)if(n.kind!=START&&n.kind!=END)n.text=rewrite(n.text,aliases);context.find(submitted.id).text=submitted.text;return context;
    }
    public static String canonicalInput(Lesson lesson,Document d,String variable){if(lesson==null)return variable;for(Node n:d.nodes)if(n.kind==INPUT&&n.text.trim().equalsIgnoreCase(variable)){Part p=lesson.part(n.id);if(p!=null)return p.answer;int count=0;String canonical=variable;for(Part part:lesson.parts)if(part.kind==INPUT){count++;canonical=part.answer;}return count==1?canonical:variable;}return variable;}
    private static Feedback checkSkills(Lesson lesson,Document d){
        HashSet<String> reached=new HashSet<>();ArrayDeque<String> queue=new ArrayDeque<>();for(Node n:d.nodes)if(n.kind==START){reached.add(n.id);queue.addLast(n.id);}
        while(!queue.isEmpty()){String id=queue.removeFirst();for(Edge edge:d.edges)if(edge.from.equals(id)&&reached.add(edge.to))queue.addLast(edge.to);}
        for(String skill:lesson.skills){boolean found=false;String description=skill;
            for(Node n:d.nodes)if(reached.contains(n.id)){
                if(skill.equals("input")){found|=n.kind==INPUT;description="leggere i dati con Input";}
                if(skill.equals("output")){found|=n.kind==OUTPUT;description="mostrare un risultato con Output";}
                if(skill.equals("decision")){found|=n.kind==DECISION;description="scegliere il percorso con Decisione";}
                if(skill.equals("assignment")){found|=n.kind==PROCESS&&!FlowTutorGuide.inspect(n).writes.isEmpty();description="conservare un calcolo in una variabile";}
                if(skill.equals("variable-output")){found|=n.kind==OUTPUT&&!FlowTutorGuide.inspect(n).reads.isEmpty();description="leggere una variabile nell'Output";}
                if(skill.equals("function")){found|=java.util.regex.Pattern.compile("\\b(abs|sqrt|min|max|floor|round|len)\\s*\\(",java.util.regex.Pattern.CASE_INSENSITIVE).matcher(FlowTutorDiagnostics.withoutStrings(n.text)).find();description="usare una funzione disponibile";}
            }
            if(skill.equals("cycle")){HashMap<String,Integer> degree=new HashMap<>();for(String id:reached)degree.put(id,0);for(Edge edge:d.edges)if(reached.contains(edge.from)&&reached.contains(edge.to))degree.put(edge.to,degree.get(edge.to)+1);ArrayDeque<String> ready=new ArrayDeque<>();for(Map.Entry<String,Integer> entry:degree.entrySet())if(entry.getValue()==0)ready.addLast(entry.getKey());int removed=0;
                while(!ready.isEmpty()){String id=ready.removeFirst();removed++;for(Edge edge:d.edges)if(edge.from.equals(id)&&degree.containsKey(edge.to)){degree.put(edge.to,degree.get(edge.to)-1);if(degree.get(edge.to)==0)ready.addLast(edge.to);}}found=removed<reached.size();description="costruire una ripetizione con un ciclo raggiungibile da Inizio";}
            if(!found)return no("learning-goal","Manca una parte dell'obiettivo","Questa lezione chiede anche di "+description+".","Puoi scegliere nomi, formule e disposizione diversi, ma usa la competenza indicata nella missione.",null);
        }return null;
    }
    private static String labelKey(String text){return text.replaceAll("\\s+"," ").trim().replaceAll("[.!?,;:]+$","").trim();}
    public static boolean freeMessage(Lesson lesson){return lesson!=null&&lesson.assessment.equals("message");}
    public static boolean labelOutput(Lesson lesson){return lesson!=null&&lesson.assessment.equals("labels");}
    public static boolean matches(Lesson lesson,FlowEngine.Value actual,FlowEngine.Value expected){
        if(freeMessage(lesson))return actual.isText()&&!actual.format().trim().isEmpty();
        if(labelOutput(lesson)&&actual.isText()&&expected.isText())return labelKey(actual.format()).equalsIgnoreCase(labelKey(expected.format()));
        return same(actual,expected);
    }
    public static String criteria(Lesson lesson){return lesson.criteria+"\nProve previste: "+lesson.trials.length+".";}
    public static FlowEngine.Value literal(String text){return FlowEngine.evaluate(text,Collections.<String,FlowEngine.Value>emptyMap());}
    public static String type(FlowEngine.Value v){return v.isNumber()?"numero":v.isText()?"testo":"vero/falso";}
    public static boolean same(FlowEngine.Value a,FlowEngine.Value b){if(a.isNumber()!=b.isNumber()||a.isText()!=b.isText()||a.isBoolean()!=b.isBoolean())return false;if(a.isNumber())return Math.abs(a.asNumber()-b.asNumber())<=1e-10*Math.max(1,Math.abs(b.asNumber()));return a.format().equals(b.format());}
    /** Small, bounded undo history; persistence is confirmed by the native host before progress advances. */
    public static final class Workspace {
        private static final class Snapshot {final Document document;final int phase;Snapshot(Document d,int phase){document=d.copy();this.phase=phase;}}
        public Document document;private final ArrayDeque<Snapshot> undo=new ArrayDeque<>(),redo=new ArrayDeque<>();public long revision;public int restoredPhase=-1;
        public Workspace(Document source){document=source.copy();}
        public boolean canUndo(){return !undo.isEmpty();}public boolean canRedo(){return !redo.isEmpty();}
        public void before(){before(-1);}public void before(int phase){undo.addLast(new Snapshot(document,phase));while(undo.size()>30)undo.removeFirst();redo.clear();revision++;}
        public boolean undo(){return undo(-1);}public boolean undo(int phase){if(undo.isEmpty())return false;redo.addLast(new Snapshot(document,phase));Snapshot s=undo.removeLast();document=s.document;restoredPhase=s.phase;revision++;return true;}
        public boolean redo(){return redo(-1);}public boolean redo(int phase){if(redo.isEmpty())return false;undo.addLast(new Snapshot(document,phase));Snapshot s=redo.removeLast();document=s.document;restoredPhase=s.phase;revision++;return true;}
    }
}
