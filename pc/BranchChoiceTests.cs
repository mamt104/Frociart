using System;using System.Collections.Generic;using System.Drawing;using System.IO;using System.Linq;using System.Reflection;using System.Threading;using System.Windows.Forms;
namespace Frociart.Tests {internal static class BranchChoiceTests {
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control c){yield return c;foreach(Control child in c.Controls)foreach(var n in Walk(child))yield return n;}
 static void Pump(int milliseconds){var stop=DateTime.UtcNow.AddMilliseconds(milliseconds);do{Application.DoEvents();Thread.Sleep(2);}while(DateTime.UtcNow<stop);}
 [STAThread]static int Main(){try{Application.EnableVisualStyles();UiMotion.SetEnabled(true);string folder=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"branch-choice");Directory.CreateDirectory(folder);
  foreach(bool dark in new[]{false,true})foreach(string chosen in new[]{"Continua la lezione","Nessuno","Annulla inserimento"}){
   Ui.SetDarkMode(dark,false);var n=new FlowNode{Kind=NodeKind.Decision,TrueLabel="Continua la lezione",FalseLabel="Ripassa il passaggio",TrueColor="#365CD9",FalseColor="#BA3058"};using(var d=new BranchContinuationDialog(n,"Quale ramo continua verso il blocco successivo?","Inserisci la decisione")){
    d.ShowInTaskbar=false;d.StartPosition=FormStartPosition.Manual;d.Location=new Point(-3000,-2500);d.Show();Pump(220);var buttons=Walk(d).OfType<Button>().ToArray();var yes=buttons.First(x=>x.Text==n.TrueLabel);var no=buttons.First(x=>x.Text==n.FalseLabel);var none=buttons.First(x=>x.Text=="Nessuno");var cancel=buttons.First(x=>x.Text=="Annulla inserimento");
    Check(yes.AccessibleName=="Ramo "+n.TrueLabel&&no.AccessibleName=="Ramo "+n.FalseLabel,"Both choices expose the actual branch names");Check(yes.Width==no.Width&&yes.Height==no.Height,"Branches have equal prominence");
    var f=BindingFlags.NonPublic|BindingFlags.Instance;Check((Color)yes.GetType().GetField("swatch",f).GetValue(yes)==ColorTranslator.FromHtml(n.TrueColor)&&(Color)no.GetType().GetField("swatch",f).GetValue(no)==ColorTranslator.FromHtml(n.FalseColor),"Distinct choice swatches match actual branch colours");Check(none.Bottom+30<cancel.Top&&d.CancelButton==cancel,"None is a distinct option; cancellation is separate and owns Escape");
    if(chosen=="Nessuno")using(var b=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(b,new Rectangle(Point.Empty,d.Size));b.Save(Path.Combine(folder,dark?"02-dark.png":"01-light.png"));}
    buttons.First(b=>b.Text==chosen).PerformClick();Check(d.Branch==(chosen==n.TrueLabel?"true":chosen=="Nessuno"?"none":null),"Choosing "+chosen+" returns its own distinct result");
   }
  }
  Ui.SetDarkMode(false,false);Console.WriteLine("BRANCH_CHOICE_FINISHED checks="+checks+" errors=0");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}}
