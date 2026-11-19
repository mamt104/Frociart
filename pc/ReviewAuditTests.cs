using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace Frociart.Tests {
 internal static class ReviewAuditTests {
  static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  static string directory;
  static void Pump(){for(int i=0;i<4;i++)Application.DoEvents();}
  static void Capture(Form f,string name){Pump();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(Path.Combine(directory,name+".png"));}}
  [STAThread]static int Main(){try{
   directory=Environment.GetEnvironmentVariable("REVIEW_OUTPUT");Directory.CreateDirectory(directory);Application.EnableVisualStyles();UiMotion.SetEnabled(false);Ui.SetDarkMode(false,false);
   using(var f=new MainForm(null,true)){
    f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-2600,-2000);f.Show();Pump();Capture(f,"01-home");f.EnterEditor();f.SetDocument(Examples.Parity(),null,false);f.Canvas.FitToView();Capture(f,"02-free");
    f.Canvas.SelectNode("check");Pump();Capture(f,"03-decision");f.Size=new Size(1120,720);Pump();Capture(f,"04-minimum-decision");
    typeof(MainForm).GetMethod("Verify",flags).Invoke(f,new object[]{true});Capture(f,"05-verify");
    f.SetDocument(Examples.Sum(),null,false);typeof(MainForm).GetMethod("StartRun",flags).Invoke(f,null);typeof(MainForm).GetMethod("PauseRun",flags).Invoke(f,null);typeof(MainForm).GetMethod("SingleStep",flags).Invoke(f,null);Capture(f,"06-runtime");
    f.SetDocument(Examples.Parity(),null,false);f.Close();
   }
   Console.WriteLine("PASS baseline walkthrough: home, free editor, decision, minimum, validation and runtime.");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
