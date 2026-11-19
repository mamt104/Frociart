using System;using System.Collections.Generic;using System.Collections.ObjectModel;using System.Globalization;using System.Linq;using System.Text.RegularExpressions;
namespace Frociart {
 /// <summary>Program-wide drawing state. Commands never call an OS drawing or scripting API.</summary>
 public sealed class FlowTurtle {
  internal static readonly NodeKind[] Kinds={NodeKind.TurtleForward,NodeKind.TurtleTurn,NodeKind.TurtleHome,NodeKind.TurtleClear,NodeKind.TurtlePen};
  internal static string Description(NodeKind kind){switch(kind){case NodeKind.TurtleForward:return "Sposta e disegna una linea";case NodeKind.TurtleTurn:return "Cambia direzione in gradi";case NodeKind.TurtleHome:return "Torna al centro senza traccia";case NodeKind.TurtleClear:return "Cancella il disegno e riparti";case NodeKind.TurtlePen:return "Scegli il colore delle linee";default:return "";}}
  internal static string Help(NodeKind kind){switch(kind){case NodeKind.TurtleForward:return "La tartaruga parte da (0, 0) rivolta verso l'alto. Indica una distanza o un'espressione; un valore negativo la fa arretrare. Disattiva Disegna per spostarla senza lasciare traccia.";case NodeKind.TurtleTurn:return "Indica l'angolo in gradi o un'espressione. 90 è un quarto di giro; 360 è un giro completo. Destra ruota in senso orario, Sinistra in senso antiorario.";case NodeKind.TurtleHome:return "Torna a (0, 0), rivolta verso l'alto, senza disegnare il percorso di ritorno. Conserva le linee e il colore della penna.";case NodeKind.TurtleClear:return "Cancella tutte le linee e torna a (0, 0), rivolta verso l'alto. Conserva il colore della penna. Il nuovo disegno comincia dal blocco successivo.";case NodeKind.TurtlePen:return "Usa un nome fra virgolette, per esempio \"blu\", un codice \"#3366FF\" oppure una variabile di testo. Il colore cambia per le linee successive.";default:return "";}}
  internal static string FieldLabel(NodeKind kind){return kind==NodeKind.TurtleForward?"Distanza o espressione":kind==NodeKind.TurtleTurn?"Angolo in gradi o espressione":"Colore o espressione di testo";}
  internal const string Example="PENNA \"blu\"\nPER lato DA 1 A 4\n    AVANZA 100\n    RUOTA DESTRA 90\nFINE PER\n";
  internal const string Syntax="\n\nGRAFICA TARTARUGA\nAVANZA 100\nAVANZA SENZA_TRACCIA 50\nRUOTA DESTRA 90\nRUOTA SINISTRA angolo\nPENNA \"blu\"\nORIGINE_TARTARUGA\nPULISCI_DISEGNO\n\nLa tartaruga parte da (0, 0), rivolta verso l’alto (90 gradi). Origine torna al centro senza traccia; Pulisci elimina le linee e torna al centro. Il colore resta invariato in entrambi. Sono accettati nomi di colori italiani/inglesi e codici #RGB o #RRGGBB fra virgolette. Distanza e angolo possono contenere espressioni e chiamate di funzione. Anche FORWARD, TURN RIGHT/LEFT, PEN, HOME_TURTLE, CLEAR_DRAWING e PEN_UP sono accettati.";

  public const int MaxSegments=10000;public const double MaxCoordinate=1000000;
  public sealed class Segment {
   public double X1{get;private set;}public double Y1{get;private set;}public double X2{get;private set;}public double Y2{get;private set;}public string Color{get;private set;}
   internal Segment(double x1,double y1,double x2,double y2,string color){X1=x1;Y1=y1;X2=x2;Y2=y2;Color=color;}
  }
  public sealed class Snapshot {
   public double X{get;private set;}public double Y{get;private set;}public double Heading{get;private set;}public string Color{get;private set;}public long Revision{get;private set;}public IList<Segment> Segments{get;private set;}
   internal Snapshot(FlowTurtle owner){X=owner.X;Y=owner.Y;Heading=owner.Heading;Color=owner.Color;Revision=owner.Revision;Segments=Array.AsReadOnly(owner.lines.ToArray());}
  }
  internal static string Svg(Snapshot snapshot){
   double minX=Math.Min(0,snapshot.X),maxX=Math.Max(0,snapshot.X),minY=Math.Min(0,snapshot.Y),maxY=Math.Max(0,snapshot.Y);foreach(var line in snapshot.Segments){minX=Math.Min(minX,Math.Min(line.X1,line.X2));maxX=Math.Max(maxX,Math.Max(line.X1,line.X2));minY=Math.Min(minY,Math.Min(line.Y1,line.Y2));maxY=Math.Max(maxY,Math.Max(line.Y1,line.Y2));}
   double margin=Math.Max(10,Math.Max(maxX-minX,maxY-minY)*.05),width=Math.Max(1,maxX-minX)+2*margin,height=Math.Max(1,maxY-minY)+2*margin;
   Func<double,string> n=v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture);var b=new System.Text.StringBuilder();b.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"").Append(n(minX-margin)).Append(' ').Append(n(-maxY-margin)).Append(' ').Append(n(width)).Append(' ').Append(n(height)).Append("\"><title>Disegno tartaruga — Frociart</title><rect x=\"").Append(n(minX-margin)).Append("\" y=\"").Append(n(-maxY-margin)).Append("\" width=\"").Append(n(width)).Append("\" height=\"").Append(n(height)).Append("\" fill=\"white\"/>");
   foreach(var line in snapshot.Segments)b.Append("<line x1=\"").Append(n(line.X1)).Append("\" y1=\"").Append(n(-line.Y1)).Append("\" x2=\"").Append(n(line.X2)).Append("\" y2=\"").Append(n(-line.Y2)).Append("\" stroke=\"").Append(line.Color).Append("\" stroke-width=\"2\" stroke-linecap=\"round\" vector-effect=\"non-scaling-stroke\"/>");return b.Append("</svg>").ToString();
  }
  internal sealed class Spec {internal NodeKind Kind;internal string Expression;internal bool Draw=true,Left;}
  readonly List<Segment> lines=new List<Segment>();readonly ReadOnlyCollection<Segment> view;
  public double X{get;private set;}public double Y{get;private set;}public double Heading{get;private set;}public string Color{get;private set;}public long Revision{get;private set;}
  public IList<Segment> Segments{get{return view;}}public bool Used{get;private set;}
  public FlowTurtle(){view=lines.AsReadOnly();Reset();}
  public Snapshot Capture(){return new Snapshot(this);}
  internal void Reset(){lines.Clear();X=Y=0;Heading=90;Color="#000000";Used=false;Revision++;}
  internal static bool IsTurtle(NodeKind kind){return kind>=NodeKind.TurtleForward&&kind<=NodeKind.TurtlePen;}
  internal static string DefaultText(NodeKind kind){return kind==NodeKind.TurtleForward?"100":kind==NodeKind.TurtleTurn?"DESTRA 90":kind==NodeKind.TurtlePen?"\"blu\"":kind==NodeKind.TurtleHome?"Origine":"Pulisci";}
  static int WordEnd(string text,int start){int end=start;while(end<text.Length&&!Char.IsWhiteSpace(text[end]))end++;return end;}
  static int Skip(string text,int start){while(start<text.Length&&Char.IsWhiteSpace(text[start]))start++;return start;}
  internal static int ExpressionOffset(NodeKind kind,string text){
   if(kind==NodeKind.TurtleHome||kind==NodeKind.TurtleClear)return text.Length;
   int start=Skip(text,0),end=WordEnd(text,start);string word=text.Substring(start,end-start).ToUpperInvariant();
   if(kind==NodeKind.TurtleTurn&&(word=="DESTRA"||word=="SINISTRA"||word=="RIGHT"||word=="LEFT")||kind==NodeKind.TurtleForward&&(word=="SENZA_TRACCIA"||word=="PEN_UP"))return Skip(text,end);
   return 0;
  }
  internal static Spec Parse(NodeKind kind,string source){
   var spec=new Spec{Kind=kind};string text=(source??"").Trim();if(kind==NodeKind.TurtleHome||kind==NodeKind.TurtleClear)return spec;
   if(kind==NodeKind.TurtleForward){int offset=ExpressionOffset(kind,text);spec.Draw=offset==0;spec.Expression=text.Substring(offset);}
   else if(kind==NodeKind.TurtleTurn){int end=WordEnd(text,0);string word=text.Substring(0,end).ToUpperInvariant();if(word!="DESTRA"&&word!="SINISTRA"&&word!="RIGHT"&&word!="LEFT")throw new FlowException("Scegli DESTRA o SINISTRA e un angolo in gradi, per esempio DESTRA 90.");spec.Left=word=="SINISTRA"||word=="LEFT";spec.Expression=text.Substring(Skip(text,end));}
   else if(kind==NodeKind.TurtlePen)spec.Expression=text;
   else throw new FlowException("Comando tartaruga non riconosciuto.");
   if(String.IsNullOrWhiteSpace(spec.Expression))throw new FlowException("Scrivi il valore per il comando tartaruga.");return spec;
  }
  internal void Apply(Spec spec,object value){
   switch(spec.Kind){
    case NodeKind.TurtleForward:
     double distance=Number(value),radians=Heading*Math.PI/180;
     double cos=Heading==90||Heading==270?0:Heading==0?1:Heading==180?-1:IntrinsicOperations.Cos(radians);
     double sin=Heading==0||Heading==180?0:Heading==90?1:Heading==270?-1:IntrinsicOperations.Sin(radians);
     double nx=X+distance*cos,ny=Y+distance*sin;Coordinate(nx);Coordinate(ny);
     bool line=spec.Draw&&(nx!=X||ny!=Y);if(line&&lines.Count>=MaxSegments)throw new FlowException("Il disegno supera 10000 tratti. Controlla il ciclo o usa Pulisci disegno.");
     if(line)lines.Add(new Segment(X,Y,nx,ny,Color));X=nx;Y=ny;break;
    case NodeKind.TurtleTurn:double angle=Number(value)%360;Heading=((Heading+(spec.Left?angle:-angle))%360+360)%360;break;
    case NodeKind.TurtleHome:X=Y=0;Heading=90;break;
    case NodeKind.TurtleClear:lines.Clear();X=Y=0;Heading=90;break;
    case NodeKind.TurtlePen:Color=ParseColor(value);break;
    default:throw new FlowException("Comando tartaruga non riconosciuto.");
   }Used=true;Revision++;
  }
  static double Number(object value){if(!Values.Numeric(value))throw new FlowException("La distanza e l'angolo richiedono un numero.");double n=Values.Number(value);if(Double.IsNaN(n)||Double.IsInfinity(n))throw new FlowException("Il valore della tartaruga deve essere finito.");return n;}
  static void Coordinate(double value){if(Double.IsNaN(value)||Double.IsInfinity(value)||Math.Abs(value)>MaxCoordinate)throw new FlowException("Il disegno supera lo spazio disponibile: coordinate tra -1000000 e 1000000.");}
  internal static readonly string[][] Colors={
   new[]{"nero","black","#000000"},new[]{"bianco","white","#FFFFFF"},new[]{"grigio","gray","#808080"},new[]{"rosso","red","#FF0000"},new[]{"arancione","orange","#FF8C00"},new[]{"giallo","yellow","#FFFF00"},new[]{"verde","green","#008000"},new[]{"lime","lime","#00FF00"},new[]{"turchese","teal","#008080"},new[]{"ciano","cyan","#00FFFF"},new[]{"blu","blue","#0000FF"},new[]{"azzurro","skyblue","#00BFFF"},new[]{"viola","purple","#800080"},new[]{"magenta","magenta","#FF00FF"},new[]{"rosa","pink","#FF69B4"},new[]{"marrone","brown","#A52A2A"}
  };
  internal static string ParseColor(object value){string text=value as string;if(text==null)throw new FlowException("Il colore richiede un testo, per esempio \"blu\" o \"#3366FF\".");text=text.Trim();foreach(var item in Colors)if(text.Equals(item[0],StringComparison.OrdinalIgnoreCase)||text.Equals(item[1],StringComparison.OrdinalIgnoreCase))return item[2];if(text.Equals("grey",StringComparison.OrdinalIgnoreCase))return "#808080";if(Regex.IsMatch(text,@"\A#[0-9a-fA-F]{6}\z"))return text.ToUpperInvariant();if(Regex.IsMatch(text,@"\A#[0-9a-fA-F]{3}\z"))return ("#"+text[1]+text[1]+text[2]+text[2]+text[3]+text[3]).ToUpperInvariant();throw new FlowException("Colore non riconosciuto. Usa un nome, per esempio \"rosso\", oppure #RRGGBB fra virgolette.");}
 }
}
