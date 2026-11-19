package it.frociart.app;
import java.util.*;import static it.frociart.app.FlowModel.*;
/** Bounded program-wide drawing state, independent of Android rendering and storage. */
public final class FlowTurtle {
 static final int[] KINDS={TURTLE_FORWARD,TURTLE_TURN,TURTLE_HOME,TURTLE_CLEAR,TURTLE_PEN};
 static String description(int kind){switch(kind){case TURTLE_FORWARD:return "Sposta e disegna una linea";case TURTLE_TURN:return "Cambia direzione in gradi";case TURTLE_HOME:return "Torna al centro senza traccia";case TURTLE_CLEAR:return "Cancella il disegno e riparti";case TURTLE_PEN:return "Scegli il colore delle linee";default:return "";}}
 static String help(int kind){switch(kind){case TURTLE_FORWARD:return "La tartaruga parte da (0, 0) rivolta verso l'alto. Indica una distanza o un'espressione; un valore negativo la fa arretrare. Disattiva Disegna per spostarla senza lasciare traccia.";case TURTLE_TURN:return "Indica l'angolo in gradi o un'espressione. 90 è un quarto di giro; 360 è un giro completo. Destra ruota in senso orario, Sinistra in senso antiorario.";case TURTLE_HOME:return "Torna a (0, 0), rivolta verso l'alto, senza disegnare il percorso di ritorno. Conserva le linee e il colore della penna.";case TURTLE_CLEAR:return "Cancella tutte le linee e torna a (0, 0), rivolta verso l'alto. Conserva il colore della penna. Il nuovo disegno comincia dal blocco successivo.";case TURTLE_PEN:return "Usa un nome fra virgolette, per esempio \"blu\", un codice \"#3366FF\" oppure una variabile di testo. Il colore cambia per le linee successive.";default:return "";}}
 static String fieldLabel(int kind){return kind==TURTLE_FORWARD?"Distanza o espressione":kind==TURTLE_TURN?"Angolo in gradi o espressione":"Colore o espressione di testo";}
 static final String EXAMPLE="PENNA \"blu\"\nPER lato DA 1 A 4\n    AVANZA 100\n    RUOTA DESTRA 90\nFINE PER\n";
 static final String SYNTAX="\n\nGRAFICA TARTARUGA\nAVANZA 100\nAVANZA SENZA_TRACCIA 50\nRUOTA DESTRA 90\nRUOTA SINISTRA angolo\nPENNA \"blu\"\nORIGINE_TARTARUGA\nPULISCI_DISEGNO\n\nLa tartaruga parte da (0, 0), rivolta verso l’alto (90 gradi). Origine torna al centro senza traccia; Pulisci elimina le linee e torna al centro. Il colore resta invariato in entrambi. Sono accettati nomi di colori italiani/inglesi e codici #RGB o #RRGGBB fra virgolette. Distanza e angolo possono contenere espressioni e chiamate di funzione. Anche FORWARD, TURN RIGHT/LEFT, PEN, HOME_TURTLE, CLEAR_DRAWING e PEN_UP sono accettati.";

 public static final int MAX_SEGMENTS=10000;public static final double MAX_COORDINATE=1000000;
 public static final class Segment {public final double x1,y1,x2,y2;public final String color;Segment(double x1,double y1,double x2,double y2,String color){this.x1=x1;this.y1=y1;this.x2=x2;this.y2=y2;this.color=color;}}
 public static final class Snapshot {public final double x,y,heading;public final String color;public final long revision;public final List<Segment> segments;Snapshot(FlowTurtle owner){x=owner.x;y=owner.y;heading=owner.heading;color=owner.color;revision=owner.revision;segments=Collections.unmodifiableList(new ArrayList<>(owner.lines));}}
  static String svg(Snapshot snapshot){
   double minX=Math.min(0,snapshot.x),maxX=Math.max(0,snapshot.x),minY=Math.min(0,snapshot.y),maxY=Math.max(0,snapshot.y);for(Segment line:snapshot.segments){minX=Math.min(minX,Math.min(line.x1,line.x2));maxX=Math.max(maxX,Math.max(line.x1,line.x2));minY=Math.min(minY,Math.min(line.y1,line.y2));maxY=Math.max(maxY,Math.max(line.y1,line.y2));}
   double margin=Math.max(10,Math.max(maxX-minX,maxY-minY)*.05),width=Math.max(1,maxX-minX)+2*margin,height=Math.max(1,maxY-minY)+2*margin;
   java.util.function.DoubleFunction<String> n=v->Double.toString(v);StringBuilder b=new StringBuilder();b.append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"").append(n.apply(minX-margin)).append(' ').append(n.apply(-maxY-margin)).append(' ').append(n.apply(width)).append(' ').append(n.apply(height)).append("\"><title>Disegno tartaruga — Frociart</title><rect x=\"").append(n.apply(minX-margin)).append("\" y=\"").append(n.apply(-maxY-margin)).append("\" width=\"").append(n.apply(width)).append("\" height=\"").append(n.apply(height)).append("\" fill=\"white\"/>");
   for(Segment line:snapshot.segments)b.append("<line x1=\"").append(n.apply(line.x1)).append("\" y1=\"").append(n.apply(-line.y1)).append("\" x2=\"").append(n.apply(line.x2)).append("\" y2=\"").append(n.apply(-line.y2)).append("\" stroke=\"").append(line.color).append("\" stroke-width=\"2\" stroke-linecap=\"round\" vector-effect=\"non-scaling-stroke\"/>");return b.append("</svg>").toString();
  }
 static final class Spec {int kind;String expression;boolean draw=true,left;}
 private final ArrayList<Segment> lines=new ArrayList<>();private final List<Segment> view=Collections.unmodifiableList(lines);
 private double x,y,heading;private String color;private long revision;private boolean used;
 public FlowTurtle(){reset();}public double x(){return x;}public double y(){return y;}public double heading(){return heading;}public String color(){return color;}public long revision(){return revision;}public boolean used(){return used;}public List<Segment> segments(){return view;}public Snapshot capture(){return new Snapshot(this);}
 void reset(){lines.clear();x=y=0;heading=90;color="#000000";used=false;revision++;}
 static boolean isTurtle(int kind){return kind>=TURTLE_FORWARD&&kind<=TURTLE_PEN;}
 static String defaultText(int kind){return kind==TURTLE_FORWARD?"100":kind==TURTLE_TURN?"DESTRA 90":kind==TURTLE_PEN?"\"blu\"":kind==TURTLE_HOME?"Origine":"Pulisci";}
 private static int wordEnd(String text,int start){int end=start;while(end<text.length()&&!Character.isWhitespace(text.charAt(end)))end++;return end;}
 private static int skip(String text,int start){while(start<text.length()&&Character.isWhitespace(text.charAt(start)))start++;return start;}
 static int expressionOffset(int kind,String text){if(kind==TURTLE_HOME||kind==TURTLE_CLEAR)return text.length();int start=skip(text,0),end=wordEnd(text,start);String word=text.substring(start,end).toUpperCase(Locale.ROOT);if(kind==TURTLE_TURN&&(word.equals("DESTRA")||word.equals("SINISTRA")||word.equals("RIGHT")||word.equals("LEFT"))||kind==TURTLE_FORWARD&&(word.equals("SENZA_TRACCIA")||word.equals("PEN_UP")))return skip(text,end);return 0;}
 static Spec parse(int kind,String source){Spec spec=new Spec();spec.kind=kind;String text=source==null?"":source.trim();if(kind==TURTLE_HOME||kind==TURTLE_CLEAR)return spec;
  if(kind==TURTLE_FORWARD){int offset=expressionOffset(kind,text);spec.draw=offset==0;spec.expression=text.substring(offset);}
  else if(kind==TURTLE_TURN){int end=wordEnd(text,0);String word=text.substring(0,end).toUpperCase(Locale.ROOT);if(!word.equals("DESTRA")&&!word.equals("SINISTRA")&&!word.equals("RIGHT")&&!word.equals("LEFT"))throw problem("Scegli DESTRA o SINISTRA e un angolo in gradi, per esempio DESTRA 90.");spec.left=word.equals("SINISTRA")||word.equals("LEFT");spec.expression=text.substring(skip(text,end));}
  else if(kind==TURTLE_PEN)spec.expression=text;else throw problem("Comando tartaruga non riconosciuto.");
  if(spec.expression.trim().isEmpty())throw problem("Scrivi il valore per il comando tartaruga.");return spec;
 }
 void apply(Spec spec,FlowEngine.Value value){switch(spec.kind){
  case TURTLE_FORWARD:
   double distance=number(value),radians=heading*Math.PI/180;double cos=heading==90||heading==270?0:heading==0?1:heading==180?-1:Math.cos(radians);double sin=heading==0||heading==180?0:heading==90?1:heading==270?-1:Math.sin(radians);double nx=x+distance*cos,ny=y+distance*sin;coordinate(nx);coordinate(ny);boolean line=spec.draw&&(nx!=x||ny!=y);if(line&&lines.size()>=MAX_SEGMENTS)throw problem("Il disegno supera 10000 tratti. Controlla il ciclo o usa Pulisci disegno.");if(line)lines.add(new Segment(x,y,nx,ny,color));x=nx;y=ny;break;
  case TURTLE_TURN:double angle=number(value)%360;heading=((heading+(spec.left?angle:-angle))%360+360)%360;break;
  case TURTLE_HOME:x=y=0;heading=90;break;
  case TURTLE_CLEAR:lines.clear();x=y=0;heading=90;break;
  case TURTLE_PEN:color=parseColor(value);break;
  default:throw problem("Comando tartaruga non riconosciuto.");
 }used=true;revision++;}
 private static double number(FlowEngine.Value value){if(value==null||!value.isNumber())throw problem("La distanza e l'angolo richiedono un numero.");double n=value.asNumber();if(!Double.isFinite(n))throw problem("Il valore della tartaruga deve essere finito.");return n;}
 private static void coordinate(double value){if(!Double.isFinite(value)||Math.abs(value)>MAX_COORDINATE)throw problem("Il disegno supera lo spazio disponibile: coordinate tra -1000000 e 1000000.");}
 static final String[][] COLORS={{"nero","black","#000000"},{"bianco","white","#FFFFFF"},{"grigio","gray","#808080"},{"rosso","red","#FF0000"},{"arancione","orange","#FF8C00"},{"giallo","yellow","#FFFF00"},{"verde","green","#008000"},{"lime","lime","#00FF00"},{"turchese","teal","#008080"},{"ciano","cyan","#00FFFF"},{"blu","blue","#0000FF"},{"azzurro","skyblue","#00BFFF"},{"viola","purple","#800080"},{"magenta","magenta","#FF00FF"},{"rosa","pink","#FF69B4"},{"marrone","brown","#A52A2A"}};
 static String trimColor(String text){int first=0,last=text.length();while(first<last&&colorSpace(text.charAt(first)))first++;while(last>first&&colorSpace(text.charAt(last-1)))last--;return text.substring(first,last);}
 private static boolean colorSpace(char c){return c>=9&&c<=13||c==0x85||Character.isSpaceChar(c);}
 static String parseColor(FlowEngine.Value value){if(value==null||!value.isText())throw problem("Il colore richiede un testo, per esempio \"blu\" o \"#3366FF\".");String text=trimColor(value.format());for(String[] item:COLORS)if(text.equalsIgnoreCase(item[0])||text.equalsIgnoreCase(item[1]))return item[2];if(text.equalsIgnoreCase("grey"))return "#808080";if(text.matches("#[0-9a-fA-F]{6}"))return text.toUpperCase(Locale.ROOT);if(text.matches("#[0-9a-fA-F]{3}"))return ("#"+text.charAt(1)+text.charAt(1)+text.charAt(2)+text.charAt(2)+text.charAt(3)+text.charAt(3)).toUpperCase(Locale.ROOT);throw problem("Colore non riconosciuto. Usa un nome, per esempio \"rosso\", oppure #RRGGBB fra virgolette.");}
 private static FlowEngine.Problem problem(String message){return new FlowEngine.Problem(message);}
}
