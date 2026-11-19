using System;
using System.Linq;

namespace Frociart {
 internal static class FlowInsertion {
  internal static string Reason(Diagram d,string nodeId,string edgeId,int reservedEdges=0){
   var n=d==null?null:d.Nodes.FirstOrDefault(x=>x.Id==nodeId);var edge=d==null?null:d.Edges.FirstOrDefault(x=>x.Id==edgeId);
   if(n==null||edge==null)return "Il blocco o la freccia non sono più disponibili.";
   if(n.Kind==NodeKind.Note)return "Le note restano libere sul foglio e non si collegano alle frecce.";
   if(n.Kind==NodeKind.Start||n.Kind==NodeKind.End)return "Inizio e Fine restano agli estremi del percorso.";
   if(edge.From==nodeId||edge.To==nodeId)return "Questa freccia è già collegata al blocco.";
   if(edge.From==edge.To||!d.Nodes.Any(x=>x.Id==edge.From)||!d.Nodes.Any(x=>x.Id==edge.To))return "Scegli una freccia con due estremi validi.";
   if(d.Nodes.First(x=>x.Id==edge.From).Kind==NodeKind.End||d.Nodes.First(x=>x.Id==edge.To).Kind==NodeKind.Start)return "Scegli una freccia con partenza e arrivo validi.";
   var incoming=d.Edges.Where(x=>x.To==nodeId).ToArray();var outgoing=d.Edges.Where(x=>x.From==nodeId).ToArray();
   if(n.Kind==NodeKind.Decision){if(incoming.Length!=0||outgoing.Length!=0)return "Per inserirla qui, usa una decisione ancora scollegata.";}
   else{
    if(outgoing.Length>1||outgoing.Length==1&&outgoing[0].Branch!="next")return "Le uscite di questo blocco vanno prima corrette.";
    if(incoming.Length>0&&outgoing.Length!=1)return "Manca una continuazione per richiudere il percorso precedente.";
    if(outgoing.Length==1&&!d.Nodes.Any(x=>x.Id==outgoing[0].To&&x.Kind!=NodeKind.Start))return "La continuazione attuale non è valida.";
    if(outgoing.Length==1&&(outgoing[0].To==nodeId||incoming.Any(x=>x.From==outgoing[0].To)))return "Il vecchio percorso non può essere richiuso senza creare un collegamento su se stesso.";
   }
   if(n.Kind!=NodeKind.Decision&&outgoing.Length==0&&d.Edges.Count+reservedEdges>=DiagramIO.MaxEdges)return "Hai raggiunto il limite di collegamenti.";
   return null;
  }
  internal static bool Apply(Diagram d,string nodeId,string edgeId,string continuation,int reservedEdges=0){
   if(Reason(d,nodeId,edgeId,reservedEdges)!=null)return false;
   var n=d.Nodes.First(x=>x.Id==nodeId);if(n.Kind==NodeKind.Decision&&continuation!="true"&&continuation!="false"&&continuation!="none")return false;
   if(n.Kind==NodeKind.Decision&&continuation!="none"&&d.Edges.Count+reservedEdges>=DiagramIO.MaxEdges)return false;
   var edge=d.Edges.First(x=>x.Id==edgeId);string target=edge.To;
   var incoming=d.Edges.Where(x=>x.To==nodeId).ToArray();var outgoing=d.Edges.FirstOrDefault(x=>x.From==nodeId);
   if(outgoing!=null){string bypass=outgoing.To;foreach(var previous in incoming)previous.To=bypass;outgoing.To=target;}
   else if(n.Kind!=NodeKind.Decision||continuation!="none")d.Edges.Add(new FlowEdge{From=nodeId,To=target,Branch=n.Kind==NodeKind.Decision?continuation:"next"});
   edge.To=nodeId;return true;
  }
 }
}
