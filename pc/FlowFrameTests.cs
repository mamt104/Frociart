using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace Frociart.Tests {
 internal sealed class FrameCanvas:FlowCanvas {internal void PaintFrame(Graphics g){OnPaint(new PaintEventArgs(g,ClientRectangle));}}
 internal static class FlowFrameTests {
  [STAThread]static int Main(){try{
   Application.EnableVisualStyles();UiMotion.SetEnabled(true);string dir=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"flow-frames");Directory.CreateDirectory(dir);
   using(var form=new Form{ClientSize=new Size(1080,700),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-2400,-2000)})using(var canvas=new FrameCanvas{Dock=DockStyle.Fill}){
    form.Controls.Add(canvas);form.Show();Application.DoEvents();canvas.Diagram=Examples.Parity();canvas.FitToView();canvas.FlowAnimationDuration=1200;
    var edge=canvas.Diagram.Edges.First(x=>canvas.Diagram.Nodes.Any(n=>n.Id==x.From&&n.Kind==NodeKind.Decision));canvas.CurrentEdgeId=edge.Id;
    var times=new List<double>();var progresses=new List<float>();using(var bmp=new Bitmap(canvas.Width,canvas.Height))using(var g=Graphics.FromImage(bmp)){
     canvas.PaintFrame(g);for(int i=0;i<60;i++){Application.DoEvents();var watch=Stopwatch.StartNew();canvas.PaintFrame(g);watch.Stop();times.Add(watch.Elapsed.TotalMilliseconds);progresses.Add(canvas.FlowProgress);if(i%4==0)bmp.Save(Path.Combine(dir,"frame-"+(i/4).ToString("00")+".png"));Thread.Sleep(12);}
    }
    if(progresses.Zip(progresses.Skip(1),(a,b)=>b>=a).Any(x=>!x))throw new Exception("Flow progress moved backwards.");
    if(UiMotion.Enabled&&progresses.Distinct().Count()<15)throw new Exception("Expected distinct intermediate fill frames.");
    times.Sort();string report="Frames: 60\nCanvas: 1080 x 700\nMedian CPU paint ms: "+times[times.Count/2].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\nP95 CPU paint ms: "+times[(int)(times.Count*.95)-1].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\nSystem animation enabled: "+UiMotion.Enabled+"\nThis measures offscreen native canvas paint cost, not display FPS or GPU composition.\n";File.WriteAllText(Path.Combine(dir,"measurements.txt"),report);Console.WriteLine(report);
    canvas.CurrentNodeId=null;canvas.CurrentEdgeId=null;form.Close();
   }
   Console.WriteLine("PASS FLOW FRAMES; real canvas render, monotonic progress and intermediate frames.");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
