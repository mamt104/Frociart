using System;using System.Linq;using System.Collections.Generic;
namespace Frociart {
 internal static class FlowAnnotations {
  internal static bool Has(Diagram document){return document.Nodes.Any(n=>n.Kind==NodeKind.Note)||document.Functions!=null&&document.Functions.Any(f=>f!=null&&f.Body!=null&&Has(f.Body));}
  internal static int Version(IEnumerable<FlowFunction> functions,IEnumerable<FlowNode> nodes=null){if(nodes!=null&&nodes.Any(n=>n.Kind==NodeKind.Note)||functions!=null&&functions.Any(f=>f!=null&&f.Body!=null&&Has(f.Body)))return 4;return functions==null||!functions.Any()?1:functions.Any(f=>f!=null&&f.Parameters!=null&&f.Parameters.Any(p=>p!=null&&p.ByReference))?3:2;}
  internal static Diagram Executable(Diagram graph){return new Diagram{Version=graph.Version,VariablePolicy=graph.VariablePolicy,Title=graph.Title,Nodes=graph.Nodes.Where(n=>n.Kind!=NodeKind.Note).ToList(),Edges=graph.Edges,Functions=graph.Functions};}
  internal static void Promote(Diagram project){if(project.Functions!=null)foreach(var f in project.Functions)if(f!=null&&f.Body!=null)Promote(f.Body);project.Version=project.RequiresDeclarations?5:Version(project.Functions,project.Nodes);}
  internal static void PlaceImported(Diagram graph){float x=graph.Nodes.Where(n=>n.Kind!=NodeKind.Note).Select(n=>n.X+FlowSizing.Size(n).Width).DefaultIfEmpty(350).Max()+64,y=80,column=0;foreach(var note in graph.Nodes.Where(n=>n.Kind==NodeKind.Note)){var size=FlowSizing.Size(note);if(y>80&&y+size.Height>18000){x+=column+64;y=80;column=0;}note.X=Math.Min(99900,x);note.Y=Math.Min(99900,y);y+=size.Height+28;column=Math.Max(column,size.Width);}}
 }
}
