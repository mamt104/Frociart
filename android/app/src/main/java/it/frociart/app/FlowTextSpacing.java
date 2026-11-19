package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Geometry-only opening adaptation; graph structure and free notes stay intact. */
public final class FlowTextSpacing {
 private FlowTextSpacing(){}
 private static final class Box {Node node;NodeSize before,after;float x,y;}
 public static Map<String,float[]> plan(Document project,FlowDesign design){
  Map<String,float[]> result=new HashMap<>();if(design.fontKey()==FlowDesign.DEFAULT.fontKey())return result;
  Sizing before=FlowCanvasView.fontSizing(FlowDesign.DEFAULT),after=FlowCanvasView.fontSizing(design);
  for(Document graph:FlowFunctionEditing.graphs(project))result.putAll(graph(graph,before,after));return result;
 }
 private static int root(int[] parent,int i){return parent[i]==i?i:(parent[i]=root(parent,parent[i]));}
 private static void join(int[] parent,boolean[] active,int i,int j){i=root(parent,i);j=root(parent,j);if(i==j)return;int a=Math.min(i,j),b=Math.max(i,j);parent[b]=a;active[a]|=active[b];}
 public static Map<String,float[]> graph(Document graph,Sizing before,Sizing after){
  ArrayList<Box> rows=new ArrayList<>();Map<String,Integer> ids=new HashMap<>();
  for(Node n:graph.nodes)if(n.kind!=NOTE){Box row=new Box();row.node=n;row.x=n.x;row.y=n.y;row.before=size(n,before);row.after=size(n,after);ids.put(n.id,rows.size());rows.add(row);}
  int count=rows.size();Map<String,float[]> result=new HashMap<>();if(count<2)return result;
  int[] parent=new int[count];boolean[] active=new boolean[count];for(int i=0;i<count;i++)parent[i]=i;
  for(Edge e:graph.edges){Integer a=ids.get(e.from),b=ids.get(e.to);if(a!=null&&b!=null)join(parent,active,a,b);}
  boolean[][] baseline=new boolean[count][count];float[][] positions=new float[count][2];
  for(int i=0;i<count;i++)for(int j=i+1;j<count;j++){Box a=rows.get(i),b=rows.get(j);baseline[i][j]=overlap(a.x,a.y,a.before,b.x,b.y,b.before,0);}
  boolean adjusted=false;
  for(int pass=0;pass<=count*2;pass++){
   Map<Integer,List<Integer>> groups=new TreeMap<>();for(int i=0;i<count;i++)groups.computeIfAbsent(root(parent,i),k->new ArrayList<>()).add(i);
   for(Map.Entry<Integer,List<Integer>> group:groups.entrySet()){
    List<Integer> members=group.getValue();if(!active[group.getKey()]){for(int i:members){positions[i][0]=rows.get(i).x;positions[i][1]=rows.get(i).y;}continue;}
    int anchor=-1;for(int i:members)if(rows.get(i).node.kind==START){anchor=i;break;}
    if(anchor<0){anchor=members.get(0);for(int i:members){Box a=rows.get(i),b=rows.get(anchor);if(a.y<b.y||a.y==b.y&&(a.x<b.x||a.x==b.x&&a.node.id.compareTo(b.node.id)<0))anchor=i;}}
    float sx=1,sy=1;for(int i:members){Box r=rows.get(i);sx=Math.max(sx,r.after.width/r.before.width);sy=Math.max(sy,r.after.height/r.before.height);}if(sx>1)sx*=1.005f;if(sy>1)sy*=1.005f;
    Box a=rows.get(anchor);float ax=a.x+a.before.width/2,ay=a.y+a.before.height/2;
    for(int i:members){Box r=rows.get(i);positions[i][0]=ax+(r.x+r.before.width/2-ax)*sx-r.after.width/2;positions[i][1]=ay+(r.y+r.before.height/2-ay)*sy-r.after.height/2;}
   }
   boolean changed=false,unresolved=false;
   for(int i=0;i<count;i++)for(int j=i+1;j<count;j++){
    if(baseline[i][j]||!overlap(positions[i][0],positions[i][1],rows.get(i).after,positions[j][0],positions[j][1],rows.get(j).after,1))continue;
    int a=root(parent,i),b=root(parent,j);if(a==b&&active[a]){unresolved=true;continue;}
    join(parent,active,a,b);active[root(parent,a)]=true;changed=adjusted=true;
   }
   if(changed)continue;
   if(unresolved)throw new IllegalArgumentException("Non riesco ad adattare questi blocchi senza cambiare la disposizione. Usa Riordina oppure riduci il testo.");
   if(!adjusted)return result;
   for(int i=0;i<count;i++){float x=positions[i][0],y=positions[i][1];if(Float.isNaN(x)||Float.isInfinite(x)||Float.isNaN(y)||Float.isInfinite(y)||Math.abs(x)>100000||Math.abs(y)>100000)throw new IllegalArgumentException("L’adattamento supera i limiti del foglio. Riduci il testo o dividi il diagramma in funzioni.");Box r=rows.get(i);if(Math.abs(x-r.x)>.001f||Math.abs(y-r.y)>.001f)result.put(r.node.id,new float[]{x,y});}
   return result;
  }
  throw new IllegalArgumentException("Non riesco ad adattare la spaziatura di questo diagramma. La disposizione originale è stata mantenuta.");
 }
 private static boolean overlap(float x,float y,NodeSize a,float xx,float yy,NodeSize b,float tolerance){return Math.min(x+a.width,xx+b.width)-Math.max(x,xx)>tolerance&&Math.min(y+a.height,yy+b.height)-Math.max(y,yy)>tolerance;}
}
