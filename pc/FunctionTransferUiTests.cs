using System;using System.IO;using System.Linq;using System.Drawing;using System.Reflection;using System.Threading;using System.Windows.Forms;using Frociart;
internal static class FunctionTransferUiTests {
 static int checks;const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static T Field<T>(object o,string name){return(T)o.GetType().GetField(name,F).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,F).Invoke(o,args);}
 static void Check(bool yes,string why){if(!yes)throw new Exception(why);checks++;Console.WriteLine("PASS "+why);}
 static void Pump(int ms=300){var end=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();Thread.Sleep(2);}while(DateTime.UtcNow<end);}
 static string Bytes(Diagram d){return Convert.ToBase64String(DiagramIO.EncodeDocument(d));}
 static void Shot(Control c,string path){using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);b.Save(path);}}
 [STAThread]static int Main(string[] args){try{string root=args[0];Application.EnableVisualStyles();Ui.SetSeparateIo(false,false);UiMotion.SetEnabled(true);
  var source=DiagramIO.Load(Path.Combine(root,"cross","simultaneous-collision-source.frociart"));var target=DiagramIO.Load(Path.Combine(root,"cross","simultaneous-collision-target.frociart"));
  foreach(bool dark in new[]{true,false}){Ui.SetDarkMode(dark,false);using(var f=new MainForm(null,true)){
   f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-3000,-3000);f.Show();Pump();f.EnterEditor();f.SetWorkspaceLayout(1,false);f.SetDocument(DiagramIO.Clone(target),null,false);Pump();string original=Bytes(f.Project);
   int dialogs=0;using(var timer=new System.Windows.Forms.Timer{Interval=80}){
    timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x!=f);if(dialog==null)return;
     if(dialog.Text=="Quale funzione vuoi importare?"){var list=dialog.Controls.Find("TutorChoiceList",true).OfType<ListBox>().Single();list.SelectedIndex=list.FindStringExact("A");dialogs++;dialog.AcceptButton.PerformClick();}
     else if(dialog.Text.StartsWith("Importa 3 funzioni —")){dialogs++;Shot(dialog,Path.Combine(root,"import-confirm-"+dark+".png"));dialog.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b=>b.Text=="Importa").PerformClick();}
    };timer.Start();f.ChooseFunctionImport(source);
   }
   Pump();Check(dialogs==2,"Native chooser and review both completed");Check(f.Project.Functions.Count==5&&FunctionEditing.Find(f.Project,f.ActiveFunctionId).Name=="A_3","Import opens selected renamed graph");Check(Field<System.Collections.ICollection>(f,"undo").Count==1,"One import creates one undo step");Shot(f,Path.Combine(root,"imported-function-"+dark+".png"));
   string imported=Bytes(f.Project);Call(f,"Undo");Check(Bytes(f.Project)==original&&f.ActiveFunctionId==null,"Undo restores original project and main");Call(f,"Redo");Check(Bytes(f.Project)==imported&&f.ActiveFunctionId!=null,"Redo restores all imported dependencies");
   Call(f,"Undo");int count=Field<System.Collections.ICollection>(f,"undo").Count;
   using(var timer=new System.Windows.Forms.Timer{Interval=80}){timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x!=f);if(dialog==null)return;if(dialog.Text=="Quale funzione vuoi importare?"){var list=dialog.Controls.Find("TutorChoiceList",true).OfType<ListBox>().Single();list.SelectedIndex=list.FindStringExact("A");dialog.AcceptButton.PerformClick();}else if(dialog.Text.StartsWith("Importa 3 funzioni —"))dialog.CancelButton.PerformClick();};timer.Start();f.ChooseFunctionImport(source);}
   Check(Bytes(f.Project)==original&&Field<System.Collections.ICollection>(f,"undo").Count==count,"Cancelling review preserves project and history");
   var chosen=source.Functions.Single(x=>x.Name=="A");var stale=FunctionTransfer.Import(f.Project,source,chosen.Id);f.Project.Title="Titolo più recente";string before=Bytes(f.Project);bool rejected=false;try{f.ApplyFunctionImport(stale);}catch(InvalidDataException){rejected=true;}Check(rejected&&Bytes(f.Project)==before,"Stale plan cannot overwrite newer work");
   // Manager contains import/export in every layout. The native menu itself is exercised.
   foreach(int mode in new[]{0,2}){f.SetWorkspaceLayout(mode,false);bool found=false;using(var timer=new System.Windows.Forms.Timer{Interval=80}){timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x!=f&&x.Text=="Funzioni del progetto");if(dialog==null)return;var list=dialog.Controls.Find("TutorChoiceList",true).OfType<ListBox>().Single();found=list.Items.Cast<string>().Contains("Importa da un progetto…")&&list.Items.Cast<string>().Contains("Esporta una funzione…");dialog.CancelButton.PerformClick();};timer.Start();Call(f,"ManageFunctions");}Check(found,"Import and export discoverable in layout "+mode);}
   f.SetDocument(DiagramIO.CreateBlank(),null,false);f.Close();Pump();
  }}
  // Android exports are actual .frociart bytes; the desktop loads and executes them.
  var expected=File.ReadAllLines(Path.Combine(root,"cases.tsv")).Select(line=>line.Split('\t')).ToDictionary(parts=>System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[0])),parts=>System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[6])));
  foreach(string file in Directory.GetFiles(Path.Combine(root,"cross"),"*-android.frociart")){var doc=DiagramIO.Load(file);var runner=new Interpreter(doc);for(int n=0;n<2000&&runner.State!=RuntimeState.Completed&&runner.State!=RuntimeState.Error;n++){if(runner.State==RuntimeState.WaitingForInput)runner.ProvideInput("7");else runner.Step();}Check(runner.State==RuntimeState.Completed&&String.Join("|",runner.Output)==expected[Path.GetFileName(file).Replace("-android.frociart", "")],"Android export executes with exact output on Windows: "+Path.GetFileName(file));}
  Console.WriteLine("FUNCTION_TRANSFER_UI_PC PASS checks="+checks);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
