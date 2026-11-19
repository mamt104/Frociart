using System;using System.Drawing;using System.Drawing.Imaging;using System.IO;using System.Linq;using System.Reflection;using System.Threading;using System.Windows.Forms;
namespace Frociart.Tests {
 internal class StickerFrameCanvas:FlowCanvas {
  internal Bitmap Frame(){var b=new Bitmap(Width,Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b))OnPaint(new PaintEventArgs(g,ClientRectangle));return b;}
  internal void Down(Point p){OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
  internal new void Move(Point p){OnMouseMove(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
  internal void Up(Point p){OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
 }
 internal static class StickerFrameTests {
  static int checks,errors;static string output;static FlowCanvas current;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  static void Check(bool ok,string text){checks++;if(!ok){errors++;Console.WriteLine("STATE moved="+Get<bool>(current,"_dragMoved")+" fill="+Get<float>(current,"holdFill")+" hold="+Get<long>(current,"holdStart")+" down="+Get<Point>(current,"_mouseDown")+" last="+Get<Point>(current,"_lastMouse"));}Console.WriteLine((ok?"PASS ":"FAIL ")+text);}
  static void Pump(int ms=0){var until=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();Thread.Sleep(4);}while(DateTime.UtcNow<until);}
  static T Get<T>(FlowCanvas c,string name){return(T)typeof(FlowCanvas).GetField(name,flags).GetValue(c);}
  static bool Same(Bitmap a,Bitmap b){for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
  static bool SameRegion(Bitmap a,Bitmap b,Rectangle r){r.Intersect(new Rectangle(0,0,a.Width,a.Height));for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
  static Diagram Fixture(){var d=new Diagram{Title="Stacco PC"};d.Nodes.AddRange(new[]{new FlowNode{Id="s",Kind=NodeKind.Start,Text="Inizio",X=220,Y=20},new FlowNode{Id="m",Kind=NodeKind.Process,Text="x = 1",X=196,Y=200},new FlowNode{Id="e",Kind=NodeKind.End,Text="Fine",X=220,Y=500}});d.Edges.AddRange(new[]{new FlowEdge{Id="in",From="s",To="m"},new FlowEdge{Id="out",From="m",To="e"}});return d;}
  static Point Center(FlowCanvas c){var r=FlowCanvas.GetNodeBounds(c.Diagram.Nodes[1]);return Point.Round(c.WorldToClient(new PointF(r.Left+r.Width/2,r.Top+r.Height/2)));}
  static void Reset(StickerFrameCanvas c){current=c;c.Diagram=Fixture();c.SnapToGrid=false;c.SelectNode("m");c.FitToView();Pump(400);using(var b=c.Frame()){} }
  [STAThread]static int Main(){try{Application.EnableVisualStyles();Ui.SetDarkMode(true,false);UiMotion.SetEnabled(true);output=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"sticker-frames");Directory.CreateDirectory(output);
   using(var f=new Form{ClientSize=new Size(960,760),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-3000,-2500)})using(var c=new StickerFrameCanvas{Dock=DockStyle.Fill}){f.Controls.Add(c);f.Show();Reset(c);var p=Center(c);c.Down(p);Pump();
    using(var start=c.Frame()){start.Save(Path.Combine(output,"01-start.png"));Pump(1220);using(var filling=c.Frame()){filling.Save(Path.Combine(output,"02-fill.png"));Check(!Same(start,filling),"Cached frame shows visible hold progress without moving the mouse");var node=c.Diagram.Nodes[1];var b=FlowCanvas.GetNodeBounds(node);var tl=c.WorldToClient(b.Location);var br=c.WorldToClient(new PointF(b.Right,b.Bottom));var body=Rectangle.FromLTRB((int)tl.X+8,(int)tl.Y+8,(int)br.X-8,(int)br.Y-8);Check(!SameRegion(start,filling,body),"The block itself visibly fills, independently of the text indicator");}}
    Pump(650);using(var b=c.Frame())b.Save(Path.Combine(output,"03-lift.png"));var free=new Point(p.X+270,p.Y+15);c.Move(free);c.Up(free);using(var b=c.Frame())b.Save(Path.Combine(output,"04-release.png"));Pump(550);using(var settled=c.Frame()){settled.Save(Path.Combine(output,"05-settled.png"));c.Invalidate();using(var repaired=c.Frame()){repaired.Save(Path.Combine(output,"06-forced-repaint.png"));Check(Same(settled,repaired),"Landing ends with exactly normal pixels; no stuck peeled frame");}}
    Check(Get<string>(c,"landingId")==null&&Get<long>(c,"holdStart")<0&&!Get<bool>(c,"stickerDetached"),"Release clears every transient sticker state");
    Reset(c);p=Center(c);float oldX=c.Diagram.Nodes[1].X,oldY=c.Diagram.Nodes[1].Y;c.Down(p);Pump(1800);c.Up(p);Pump(450);Check(c.Diagram.Edges.Count==1&&c.Diagram.Edges[0].To=="e","Completed stationary hold detaches on release instead of reinserting itself");Check(c.Diagram.Nodes[1].X==oldX&&c.Diagram.Nodes[1].Y==oldY,"Stationary detachment does not snap or shift the block");
    Reset(c);p=Center(c);c.Down(p);c.Move(new Point(p.X+3,p.Y+2));Pump(1500);Check(Get<float>(c,"holdFill")>=1,"Tiny stationary mouse jitter does not cancel the hold");c.Up(p);Pump(450);
    Reset(c);p=Center(c);c.Down(p);c.Move(new Point(p.X+30,p.Y+12));Pump(1500);Check(Get<long>(c,"holdStart")<0&&!Get<bool>(c,"stickerDetached"),"A real drag cancels the hold and never rearms it");c.Up(new Point(p.X+30,p.Y+12));Pump(450);Check(c.Diagram.Edges.Count==2,"Ordinary drag keeps both connections");
    c.Diagram=new Diagram();f.Close();
   }Console.WriteLine("STICKER_FRAMES_FINISHED checks="+checks+" errors="+errors);return errors==0?0:1;
  }catch(Exception ex){Console.WriteLine(ex);return 1;}}
 }
}
