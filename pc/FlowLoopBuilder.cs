using System;using System.Collections.Generic;using System.Drawing;using System.Linq;using System.Text.RegularExpressions;
namespace Frociart {
 internal static class FlowLoopBuilder {
  internal sealed class Spec {
   internal int Kind;internal string Variable="i",From="1",To="5",Step="1",Initialize="i = 0",Condition="i < 5",Body="STAMPA i";
   internal static Spec Default(int kind){return new Spec{Kind=kind,Body=kind==0?"STAMPA i":"STAMPA i\ni = i + 1"};}
  }
  internal sealed class Plan {internal bool Editing;internal Diagram Project;internal string FunctionId,EntryId,BaseDocument,Code;internal int Added;internal PointF Origin;}
  private static string Line(string value,string label){value=(value??"").Trim();if(value.Length==0||value.Length>8192||value.Any(c=>Char.IsControl(c)))throw new FlowException("Completa «"+label+"» con una sola riga.");return value;}
  internal static string Source(Spec s){
   if(s==null||s.Kind<0||s.Kind>2)throw new FlowException("Scegli Per, Mentre o Ripeti.");
   string body=(s.Body??"").Trim();if(body.Length==0||body.Length>32768)throw new FlowException("Scrivi le istruzioni del corpo del ciclo (massimo 32768 caratteri).");
   string code;
   if(s.Kind==0){string variable=Line(s.Variable,"Variabile");if(!Regex.IsMatch(variable,@"\A"+ExpressionLanguage.IdentifierPattern+@"\z"))throw new FlowException("Il contatore deve essere un nome: per esempio i, indice o contatore.");code="PER "+variable+" DA "+Line(s.From,"Da")+" A "+Line(s.To,"A")+" PASSO "+Line(s.Step,"Passo")+" FAI\n"+body+"\nFINE PER";}
   else{string condition=Line(s.Condition,"Condizione");code=s.Kind==1?"MENTRE "+condition+" FAI\n"+body+"\nFINE MENTRE":"RIPETI\n"+body+"\nFINCHÉ non ("+condition+")";if(!String.IsNullOrWhiteSpace(s.Initialize))code=Line(s.Initialize,"Prima del ciclo")+"\n"+code;}
   return "INIZIO\n"+code+"\nFINE\n";
  }
  internal static Plan Prepare(Diagram project,string functionId,string edgeId,Spec spec){
   string before=Convert.ToBase64String(DiagramIO.EncodeDocument(project));var owner=FunctionEditing.Find(project,functionId);if(functionId!=null&&owner==null)throw new FlowException("La funzione non è più disponibile.");
   var candidate=DiagramIO.Clone(project);var graph=FunctionEditing.Graph(candidate,functionId);var edge=graph.Edges.FirstOrDefault(e=>e.Id==edgeId);
   if(edge==null)throw new FlowException("Scegli una freccia per inserire il ciclo.");var source=graph.Nodes.First(n=>n.Id==edge.From);var target=graph.Nodes.First(n=>n.Id==edge.To);
   if(source.Kind==NodeKind.End||target.Kind==NodeKind.Start)throw new FlowException("Scegli una freccia che continua il percorso.");
   string code=Source(spec);var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var g in FunctionEditing.Graphs(project))foreach(var n in g.Nodes)foreach(Match m in Regex.Matches(n.Text??"",ExpressionLanguage.IdentifierWordPattern))names.Add(m.Value);
   if(owner!=null){foreach(var parameter in owner.Parameters)names.Add(parameter.Name);names.Add(owner.ReturnVariable);}
   var fragment=FlowPseudocode.DecodeFragment(code,project,owner,names);var starts=fragment.Nodes.Where(n=>n.Kind==NodeKind.Start).ToArray();var ends=fragment.Nodes.Where(n=>n.Kind==NodeKind.End).ToArray();
   if(starts.Length!=1||ends.Length!=1)throw new FlowException("Nel corpo usa istruzioni che tornano al ciclo: rimuovi FERMA e i salti fuori dal ciclo.");
   string start=starts[0].Id,end=ends[0].Id;var entryEdge=fragment.Edges.Single(e=>e.From==start);string entry=entryEdge.To;
   var added=fragment.Nodes.Where(n=>n.Id!=start&&n.Id!=end).ToList();var links=fragment.Edges.Where(e=>e.From!=start&&e.From!=end).ToList();
   if(added.Count==0||!links.Any(e=>e.To==end)||added.Any(n=>n.Kind!=NodeKind.Decision&&!links.Any(e=>e.From==n.Id))||added.Any(n=>n.Kind==NodeKind.Decision&&(!links.Any(e=>e.From==n.Id&&e.Branch=="true")||!links.Any(e=>e.From==n.Id&&e.Branch=="false"))))throw new FlowException("Il ciclo contiene un'uscita scollegata. Completa tutti i rami del corpo.");
   if(FunctionEditing.Graphs(candidate).Sum(g=>g.Nodes.Count)+added.Count>DiagramIO.MaxNodes||FunctionEditing.Graphs(candidate).Sum(g=>g.Edges.Count)+links.Count>DiagramIO.MaxEdges)throw new FlowException("Il ciclo supera il limite di 500 blocchi o 1500 collegamenti del progetto.");
   Tutor.Arrange(fragment);string previous=edge.To;edge.To=entry;foreach(var e in links)if(e.To==end)e.To=previous;
   Place(graph,added,source,target,edge.Branch,entry);graph.Nodes.AddRange(added);graph.Edges.AddRange(links);DiagramIO.EncodeDocument(candidate);
   return new Plan{Project=candidate,FunctionId=functionId,EntryId=entry,Added=added.Count,BaseDocument=before,Code=code,Origin=new PointF(source.X,source.Y+FlowSizing.Size(source).Height)};
  }
  private static HashSet<string> Reach(Diagram d,string id,bool reverse,HashSet<string> blocked){var result=new HashSet<string>();var q=new Queue<string>();q.Enqueue(id);while(q.Count>0){string n=q.Dequeue();if(blocked!=null&&blocked.Contains(n)||!result.Add(n))continue;foreach(var e in d.Edges)if((reverse?e.To:e.From)==n)q.Enqueue(reverse?e.From:e.To);}return result;}
  private static RectangleF Bounds(IEnumerable<FlowNode> nodes){var b=RectangleF.Empty;bool first=true;foreach(var n in nodes){var r=FlowCanvas.GetNodeBounds(n);b=first?r:RectangleF.Union(b,r);first=false;}return b;}
  private static void Move(IEnumerable<FlowNode> nodes,float x,float y){foreach(var n in nodes){n.X+=x;n.Y+=y;}}
  private static void Place(Diagram d,List<FlowNode> added,FlowNode source,FlowNode target,string branch,string entry){
   var protectedIds=Reach(d,source.Id,true,null);var movingIds=Reach(d,target.Id,false,protectedIds);var moving=d.Nodes.Where(n=>movingIds.Contains(n.Id)).ToList();var fixedNodes=d.Nodes.Where(n=>!movingIds.Contains(n.Id)).ToList();
   var first=added.First(n=>n.Id==entry);var sb=FlowCanvas.GetNodeBounds(source);float center=sb.Left+sb.Width/2;if(source.Kind==NodeKind.Decision)center+=(branch=="true"?-1:1)*(sb.Width/2+Bounds(added).Width/2+88);
   Move(added,center-first.X-FlowSizing.Size(first).Width/2,sb.Bottom+72-first.Y);
   var bounds=Bounds(added);foreach(var n in fixedNodes.OrderBy(n=>n.X)){var obstacle=FlowCanvas.GetNodeBounds(n);if(bounds.IntersectsWith(obstacle)){float dx=obstacle.Right+88-bounds.Left;Move(added,dx,0);bounds.X+=dx;}}
   if(moving.Count>0){var b=Bounds(moving);float dy=Math.Max(0,bounds.Bottom+72-b.Top);Move(moving,0,dy);b.Y+=dy;foreach(var n in fixedNodes.OrderBy(n=>n.Y)){var obstacle=FlowCanvas.GetNodeBounds(n);if(b.IntersectsWith(obstacle)){dy=obstacle.Bottom+72-b.Top;Move(moving,0,dy);b.Y+=dy;}}}
  }
 }
}
