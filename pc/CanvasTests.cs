// Independent native canvas regression harness. Exclude from the production executable.
// Compile: mcs -langversion:6 -r:System.Windows.Forms -r:System.Drawing -r:System.Core -r:System.Runtime.Serialization -out:CanvasTests.exe Core.cs Examples.cs Theme.cs BlockPicker.cs FlowCanvas.cs CanvasTests.cs
// Linux: xvfb-run -a mono CanvasTests.exe; Windows: CanvasTests.exe
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Frociart;
public class ProbeCanvas : FlowCanvas {
 public void Down(Point p,MouseButtons b=MouseButtons.Left){OnMouseDown(new MouseEventArgs(b,1,p.X,p.Y,0));}
 public void Up(Point p,MouseButtons b=MouseButtons.Left){OnMouseUp(new MouseEventArgs(b,1,p.X,p.Y,0));}
 public void MoveTo(Point p,MouseButtons b=MouseButtons.None){OnMouseMove(new MouseEventArgs(b,0,p.X,p.Y,0));}
 public void Wheel(Point p,int delta){OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,p.X,p.Y,delta));}
 public void Key(Keys key){OnKeyDown(new KeyEventArgs(key));}
 public void ReleaseKey(Keys key){OnKeyUp(new KeyEventArgs(key));}
 public Point ScreenPoint(float x,float y){return Point.Round(WorldToClient(new PointF(x,y)));}
}
public class CanvasTests {
 static int passes;
 static string Artifact(string name){return Path.Combine(Path.GetTempPath(),name);}
 static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);passes++;}
 [STAThread] public static void Main(){
 Application.EnableVisualStyles(); QaDialogs.Start();
 using(var form=new Form{ClientSize=new Size(1100,820)})
 using(var canvas=new ProbeCanvas{Dock=DockStyle.Fill}){
 form.Controls.Add(canvas);form.Show();Application.DoEvents();
 canvas.Diagram=Examples.Parity();canvas.FitToView();Application.DoEvents();
 using(var bmp=new Bitmap(1100,820)){canvas.DrawToBitmap(bmp,new Rectangle(0,0,1100,820));bmp.Save(Artifact("FlowCanvas-preview.png"));}
 int before=0,after=0;canvas.DiagramChanging+=(s,e)=>before++;canvas.DiagramChanged+=(s,e)=>after++;
 var n=canvas.AddNode(NodeKind.Process);
 Check(n!=null && canvas.Diagram.Nodes.Contains(n) && before==1 && after==1,"add fires one mutation pair");
 PointF addCenter=canvas.WorldToClient(new PointF(n.X+106,n.Y+44));
 Check(addCenter.X>=0 && addCenter.X<=canvas.Width && addCenter.Y>=0 && addCenter.Y<=canvas.Height,"new node appears in current viewport");
 var dup=canvas.DuplicateSelected();Check(dup.Id!=n.Id && dup.Text==n.Text && before==2 && after==2,"duplicate distinct identity and undo hooks");
 canvas.DeleteSelection();Check(!canvas.Diagram.Nodes.Contains(dup),"delete duplicate");canvas.SelectNode(n.Id);canvas.DeleteSelection();
 var decision=canvas.Diagram.Nodes.First(x=>x.Kind==NodeKind.Decision);var odd=canvas.Diagram.Nodes.First(x=>x.Id=="odd");
 canvas.BeginConnect(decision.Id,"true");Point oddCenter=canvas.ScreenPoint(odd.X+113,odd.Y+46);canvas.Down(oddCenter);canvas.Up(oddCenter);
 Check(canvas.Diagram.Edges.Count(x=>x.From==decision.Id && x.Branch=="true")==1 && canvas.Diagram.Edges.First(x=>x.From==decision.Id && x.Branch=="true").To==odd.Id,"replace source branch without duplicates");
 Check(canvas.Diagram.Edges.Count(x=>x.From==decision.Id && x.To==odd.Id)==2,"both decision branches may share destination");
 canvas.BeginConnect(decision.Id,"false");canvas.Key(Keys.Escape);Check(!canvas.IsConnecting,"escape cancels connection");
 canvas.Zoom=0.85f;canvas.CenterOnNode("input");
 var input=canvas.Diagram.Nodes.First(x=>x.Id=="input");var oldX=input.X;var oldY=input.Y;
 Point p=canvas.ScreenPoint(input.X+113,input.Y+46);int beforeDrag=before,afterDrag=after;canvas.Down(p);canvas.MoveTo(new Point(p.X+51,p.Y+51),MouseButtons.Left);canvas.Up(new Point(p.X+51,p.Y+51));
 Check(Math.Abs(input.X-oldX)>=48 && Math.Abs(input.Y-oldY)>=48,"drag honors zoom and world coordinates");
 Check(before==beforeDrag+1 && after==afterDrag+1,"drag creates one undo record");
 Point panPoint=new Point(50,50);var viewPoint=canvas.ClientToWorld(panPoint);canvas.Down(panPoint,MouseButtons.Middle);canvas.MoveTo(new Point(150,120),MouseButtons.Middle);canvas.Up(new Point(150,120),MouseButtons.Middle);var pannedPoint=canvas.ClientToWorld(panPoint);
 Check(Math.Abs(viewPoint.X-pannedPoint.X)>100 && Math.Abs(viewPoint.Y-pannedPoint.Y)>70,"pan moves viewport");
 Point anchor=new Point(430,280);var anchorBefore=canvas.ClientToWorld(anchor);canvas.Wheel(anchor,120);var anchorAfter=canvas.ClientToWorld(anchor);
 Check(Math.Abs(anchorBefore.X-anchorAfter.X)<0.01 && Math.Abs(anchorBefore.Y-anchorAfter.Y)<0.01,"wheel zoom preserves world point at cursor");
 
 Point blank=new Point(15,15);var blankBefore=canvas.ClientToWorld(blank);canvas.Down(blank);canvas.MoveTo(new Point(75,45),MouseButtons.Left);canvas.Up(new Point(75,45));var blankAfter=canvas.ClientToWorld(blank);
 Check(Math.Abs(blankBefore.X-blankAfter.X)<0.01 && Math.Abs(blankBefore.Y-blankAfter.Y)<0.01,"left background marquee preserves viewport");
 var firstEdge=canvas.Diagram.Edges.First();var sourceNode=canvas.Diagram.Nodes.First(x=>x.Id==firstEdge.From);var targetNode=canvas.Diagram.Nodes.First(x=>x.Id==firstEdge.To);
 var routeMethod=typeof(FlowCanvas).GetMethod("RouteEdge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var route=(List<PointF>)routeMethod.Invoke(canvas,new object[]{sourceNode,targetNode,firstEdge.Branch});
 Point hit=canvas.ScreenPoint((route[0].X+route[1].X)/2,(route[0].Y+route[1].Y)/2);canvas.Down(hit);canvas.Up(hit);
 Check(canvas.SelectedEdgeId==firstEdge.Id,"edge hit test after zoom and pan");
 canvas.SelectNode("odd");canvas.CurrentNodeId="odd";canvas.IssueNodeIds=new HashSet<string>{"odd"};canvas.ExportPng(Artifact("FlowCanvas-export-a.png"));
 canvas.SelectNode(null);canvas.CurrentNodeId=null;canvas.IssueNodeIds=new HashSet<string>();canvas.ExportPng(Artifact("FlowCanvas-export-b.png"));
 Check(File.ReadAllBytes(Artifact("FlowCanvas-export-a.png")).SequenceEqual(File.ReadAllBytes(Artifact("FlowCanvas-export-b.png"))),"PNG excludes selection, validation badges, and simulation highlights");
 canvas.SelectNode("odd");canvas.DeleteSelection();Check(!canvas.Diagram.Edges.Any(x=>x.To=="odd"||x.From=="odd"),"deleting node removes incoming and outgoing edges");
 
 canvas.Diagram=Examples.Countdown();canvas.FitToView();canvas.ExportPng(Artifact("FlowCanvas-loop.png"));
 var boundsMethod=typeof(FlowCanvas).GetMethod("GetContentBounds",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var graphBounds=(RectangleF)boundsMethod.Invoke(canvas,null);
 RectangleF nodeBounds=FlowCanvas.GetNodeBounds(canvas.Diagram.Nodes[0]);foreach(var graphNode in canvas.Diagram.Nodes)nodeBounds=RectangleF.Union(nodeBounds,FlowCanvas.GetNodeBounds(graphNode));
 Check(graphBounds.Left<nodeBounds.Left || graphBounds.Right>nodeBounds.Right,"content bounds include external loop route");
 using(var loopImage=new Bitmap(Artifact("FlowCanvas-loop.png")))Check(loopImage.Width>=(graphBounds.Width+103)*2 && loopImage.Height>=(graphBounds.Height+103)*2,"export includes routed graph and safe margins");
 canvas.Diagram=new Diagram();canvas.FitToView();using(var bmp=new Bitmap(1100,820)){canvas.DrawToBitmap(bmp,new Rectangle(0,0,1100,820));bmp.Save(Artifact("FlowCanvas-empty.png"));}
 Check(true,"empty diagram renders");

 // Bound regressions: blocked actions must never enter undo history or produce unsavable documents.
 var limited=new Diagram();
 for(int i=0;i<DiagramIO.MaxNodes;i++)limited.Nodes.Add(new FlowNode{Id="limit-"+i,Kind=NodeKind.Process,Text="x = 0",X=(i%25)*240,Y=(i/25)*110});
 canvas.Diagram=limited;canvas.SelectNode(limited.Nodes[0].Id);
 int beforeLimit=before,afterLimit=after;
 Check(canvas.AddNode(NodeKind.Process)==null && canvas.DuplicateSelected()==null && limited.Nodes.Count==DiagramIO.MaxNodes,"500-node limit blocks add and duplicate");
 Check(before==beforeLimit && after==afterLimit,"node-limit rejection creates no mutation or undo record");
 Check(DiagramIO.Clone(limited).Nodes.Count==DiagramIO.MaxNodes,"node-limit rejection preserves a serializable document");
 canvas.DeleteSelection();var afterDelete=canvas.AddNode(NodeKind.Output);
 Check(afterDelete!=null && limited.Nodes.Count==DiagramIO.MaxNodes,"editing continues after limit rejection and deletion");
 canvas.Diagram=new Diagram();
 var addAt=typeof(FlowCanvas).GetMethod("AddNodeAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var extreme=(FlowNode)addAt.Invoke(canvas,new object[]{NodeKind.Process,new PointF(1000000000,-1000000000)});
 Check(extreme.X==99900 && extreme.Y==-99900,"adding far outside the viewport clamps both coordinate bounds");
 canvas.SelectNode(extreme.Id);var extremeCopy=canvas.DuplicateSelected();
 Check(Math.Abs(extremeCopy.X)<=99900 && Math.Abs(extremeCopy.Y)<=99900,"duplicate clamps coordinates at the document boundary");
 var collisionA=(FlowNode)addAt.Invoke(canvas,new object[]{NodeKind.Process,new PointF(1000000000,1000000000)});
 var collisionB=(FlowNode)addAt.Invoke(canvas,new object[]{NodeKind.Process,new PointF(1000000000,1000000000)});
 Check(Math.Abs(collisionA.X)<=99900 && Math.Abs(collisionA.Y)<=99900 && collisionB.X==99900 && collisionB.Y==99900,"local collision resolution keeps every coordinate within bounds and pins the new block");
 Check(!FlowCanvas.GetNodeBounds(collisionA).IntersectsWith(FlowCanvas.GetNodeBounds(collisionB)),"local placement resolves a collision inward at the document boundary");
 canvas.SelectNode(extreme.Id);extreme.X=99900;extreme.Y=99900;int beforeArrow=before,afterArrow=after;
 canvas.Key(Keys.Right|Keys.Shift);canvas.Key(Keys.Down|Keys.Shift);
 Check(extreme.X==99900 && extreme.Y==99900 && before==beforeArrow && after==afterArrow,"keyboard cannot exceed upper bound or create an empty undo record");
 extreme.X=-99900;extreme.Y=-99900;canvas.Key(Keys.Left|Keys.Shift);canvas.Key(Keys.Up|Keys.Shift);
 Check(extreme.X==-99900 && extreme.Y==-99900,"keyboard cannot exceed lower coordinate bound");
 canvas.Diagram=new Diagram();var dragBound=new FlowNode{Id="drag-bound",Kind=NodeKind.Process,Text="x = 0",X=99888,Y=99888};canvas.Diagram.Nodes.Add(dragBound);canvas.Zoom=1;canvas.CenterOnNode(dragBound.Id);
 Point dragOrigin=canvas.ScreenPoint(dragBound.X+106,dragBound.Y+44);canvas.Down(dragOrigin);canvas.MoveTo(new Point(dragOrigin.X+600,dragOrigin.Y+600),MouseButtons.Left);canvas.Up(new Point(dragOrigin.X+600,dragOrigin.Y+600));
 Check(dragBound.X==99900 && dragBound.Y==99900,"drag cannot exceed upper coordinate bound");
 dragBound.X=-99888;dragBound.Y=-99888;canvas.CenterOnNode(dragBound.Id);dragOrigin=canvas.ScreenPoint(dragBound.X+106,dragBound.Y+44);canvas.Down(dragOrigin);canvas.MoveTo(new Point(dragOrigin.X-600,dragOrigin.Y-600),MouseButtons.Left);canvas.Up(new Point(dragOrigin.X-600,dragOrigin.Y-600));
 Check(dragBound.X==-99900 && dragBound.Y==-99900,"drag cannot exceed lower coordinate bound");
 Check(DiagramIO.Clone(canvas.Diagram).Nodes.Count==1,"bounded drag preserves save and undo compatibility");
 var edgeLimited=new Diagram();var edgeA=new FlowNode{Id="edge-a",Kind=NodeKind.Process,Text="x = 0",X=60,Y=60};var edgeB=new FlowNode{Id="edge-b",Kind=NodeKind.Process,Text="x = 1",X=60,Y=300};var edgeD=new FlowNode{Id="edge-d",Kind=NodeKind.Decision,Text="x > 0",X=390,Y=150};
 edgeLimited.Nodes.Add(edgeA);edgeLimited.Nodes.Add(edgeB);edgeLimited.Nodes.Add(edgeD);
 for(int i=0;i<DiagramIO.MaxEdges;i++)edgeLimited.Edges.Add(new FlowEdge{Id="max-edge-"+i,From=edgeA.Id,To=edgeB.Id,Branch="next"});
 canvas.Diagram=edgeLimited;canvas.FitToView();canvas.BeginConnect(edgeD.Id,"true");beforeLimit=before;afterLimit=after;
 Point edgeTarget=canvas.ScreenPoint(edgeA.X+106,edgeA.Y+44);canvas.Down(edgeTarget);canvas.Up(edgeTarget);
 Check(edgeLimited.Edges.Count==DiagramIO.MaxEdges && before==beforeLimit && after==afterLimit,"edge-limit rejection creates no mutation or undo record");
 canvas.CancelConnection();canvas.BeginConnect(edgeA.Id,"next");edgeTarget=canvas.ScreenPoint(edgeD.X+116,edgeD.Y+68);canvas.Down(edgeTarget);canvas.Up(edgeTarget);
 Check(edgeLimited.Edges.Count==1 && edgeLimited.Edges[0].To==edgeD.Id,"existing branch can still be replaced at the edge limit");
 Check(DiagramIO.Clone(edgeLimited).Edges.Count==1,"edge-limit recovery preserves a serializable document");
 Console.WriteLine(passes+" canvas checks passed.");
 }
 }
}

