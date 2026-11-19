package it.frociart.app;
import java.util.*;import static it.frociart.app.FlowModel.*;
final class FlowAnnotations {
 private FlowAnnotations(){}
 static boolean has(Document d){for(Node n:d.nodes)if(n.kind==NOTE)return true;if(d.functions!=null)for(Function f:d.functions)if(f!=null&&f.body!=null&&has(f.body))return true;return false;}
 static int version(List<Function> functions,List<Node> nodes){if(nodes!=null)for(Node n:nodes)if(n.kind==NOTE)return 4;if(functions!=null)for(Function f:functions)if(f!=null&&f.body!=null&&has(f.body))return 4;if(functions==null||functions.isEmpty())return 1;for(Function f:functions)if(f!=null&&f.parameters!=null)for(Parameter p:f.parameters)if(p!=null&&p.byReference)return 3;return 2;}
 static Document executable(Document graph){Document d=new Document();d.version=graph.version;d.variablePolicy=graph.variablePolicy;d.title=graph.title;for(Node n:graph.nodes)if(n.kind!=NOTE)d.nodes.add(n);d.edges.addAll(graph.edges);d.functions=graph.functions;return d;}
 static void promote(Document project){if(project.functions!=null)for(Function f:project.functions)if(f!=null&&f.body!=null)promote(f.body);project.version=project.requiresDeclarations()?5:version(project.functions,project.nodes);}
 static void placeImported(Document graph){float x=350;for(Node n:graph.nodes)if(n.kind!=NOTE)x=Math.max(x,n.x+width(n));x+=64;float y=80,column=0;for(Node note:graph.nodes)if(note.kind==NOTE){if(y>80&&y+height(note)>18000){x+=column+64;y=80;column=0;}note.x=Math.min(99900,x);note.y=Math.min(99900,y);y+=height(note)+28;column=Math.max(column,width(note));}}
}
