using System;using System.IO;using System.Linq;using System.Drawing;using System.Reflection;using System.Collections.Generic;using System.Windows.Forms;using Frociart;
internal class DeclarationEditorTests {
 static int checks;static string output=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;Console.WriteLine("PASS "+reason);}
 static void Capture(Form form,string name){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-3000,-3000);form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,name+".png"));}}
 static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(obj);}
 static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(obj,args);}
 [STAThread] static int Main(){try{Application.EnableVisualStyles();
 var catalog=FunctionModel.Catalog(DiagramIO.CreateBlank());
 foreach(bool dark in new[]{true,false}){Ui.SetDarkMode(dark,false);using(var dialog=new DeclarationEditorDialog("totale, resto: REALE[10]",catalog)){
  Check(dialog.Names.Text=="totale, resto"&&dialog.ValueType.SelectedIndex==1&&dialog.IsVector.Checked,"Guided fields restore names, type and vector");dialog.Size=new Size(520,590);Capture(dialog,"declaration-dialog-"+dark);
  dialog.Names.Text="a,a";dialog.Save();Check(dialog.Result==null&&dialog.Error.Text.Length>0&&dialog.Names.Text=="a,a","Invalid declaration remains editable");
  dialog.Names.Text="valori";dialog.Dimension.Text="2+";dialog.Save();Check(dialog.Result==null&&dialog.Dimension.Text=="2+","Invalid dimension remains editable");
  dialog.ValueType.SelectedIndex=3;dialog.Dimension.Text="2+3";dialog.Save();Check(dialog.Result=="valori: LOGICO[2+3]","Guided controls produce boolean vector declaration");
 }}
 using(var dialog=new DeclarationEditorDialog("testo: TESTO",catalog)){dialog.Names.Text="bozza";dialog.Close();Check(dialog.Result==null,"Cancel does not apply draft");}
 var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"intero","dimensione"},{"n","nomi"}};
 Check(FlowDeclaration.RewriteVariables("  DICHIARA n: INTERO[intero]  ",map)=="  DICHIARA nomi: INTERO[dimensione]  ","Rename preserves type keyword, command and whitespace");
 Check(FlowDeclaration.RewriteVariables("x: INTERO",map)=="x: INTERO","Type keyword is not a variable reference");
 var project=FlowPseudocode.Decode("FUNZIONE calcolo(intero: INTERO) -> INTERO risultato\nDICHIARA a: INTERO[intero]\nDICHIARA risultato: INTERO\nrisultato=len(a)\nFINE FUNZIONE\nSTAMPA calcolo(3)");var changed=project.Functions[0].Copy();changed.Parameters[0].Name="dimensione";var renamed=FunctionEditing.Save(project,changed,new Dictionary<string,string>{{"intero","dimensione"}});Check(renamed.Functions[0].Body.Nodes.Any(n=>n.Text=="a: INTERO[dimensione]"),"Function signature rename updates only size expression");
 using(var form=new MainForm(null,true)){form.ShowInTaskbar=false;form.Location=new Point(-3000,-3000);form.Show();form.EnterEditor();form.SetWorkspaceLayout(1,false);var doc=FlowPseudocode.Decode("DICHIARA codice: TESTO\nSTAMPA codice");form.SetDocument(doc,null,false);var node=doc.Nodes.Single(n=>n.Kind==NodeKind.Declare);form.Canvas.SelectNode(node.Id);Application.DoEvents();Check(Field<FlowButton>(form,"functionButton").Text=="Nome, tipo e vettore…","Inspector exposes guided declaration editor");
  Field<TextBox>(form,"expression").Text="codice: REALE[2]";form.CommitInspector();Check(node.Text=="codice: REALE[2]","Declaration edit committed");Call(form,"Undo");Check(form.Document.Nodes.Single(n=>n.Id==node.Id).Text=="codice: TESTO","Undo restores declaration");Call(form,"Redo");Check(form.Document.Nodes.Single(n=>n.Id==node.Id).Text=="codice: REALE[2]","Redo restores declaration");
  string path=Path.Combine(output,"declarations.frociart");form.SetDocument(form.Project,path,false);Call(form,"SaveDocument",false);Check(DiagramIO.Load(path).Nodes.Single(n=>n.Id==node.Id).Text=="codice: REALE[2]","Declaration survives actual save and reopen");
  var runner=new Interpreter(form.Project);while(runner.State!=RuntimeState.Completed&&runner.State!=RuntimeState.Error)runner.Step();Check(runner.State==RuntimeState.Completed,"Saved declaration executes");var snapshot=new ExecutionSnapshot(form.Document,runner);var row=snapshot.Rows.Single(r=>r.Name=="codice");Check(row.Type=="Reale[2]"&&row.DisplayName.Contains("Reale[2]"),"Shareable report retains explicit vector type");using(var image=snapshot.Render(form.Canvas))ExecutionSnapshot.Save(image,Path.Combine(output,"declaration-snapshot.png"));
  form.SingleStep();for(int i=0;i<20&&form.Runner.State!=RuntimeState.Completed&&form.Runner.State!=RuntimeState.Error;i++)form.SingleStep();Application.DoEvents();var variables=Field<ListView>(form,"variables");Check(variables.Items.Count==1&&variables.Items[0].SubItems.Count==3&&variables.Items[0].SubItems[2].Text=="Reale[2]","Native variable list shows declared vector type");
  foreach(bool separate in new[]{false,true}){Ui.SetSeparateIo(separate);Call(form,"PopulatePalette");var palette=Field<Panel>(form,"ioPalette");Check(palette.Controls.OfType<BlockButton>().Count()==(separate?8:7),"All block types present in palette separate="+separate);var toggle=palette.Controls.OfType<CheckBox>().Single();Check(palette.Controls.OfType<BlockButton>().All(button=>button.Bottom<toggle.Top),"Palette toggle does not overlap blocks separate="+separate);}
  form.SetDocument(DiagramIO.CreateBlank(),null,false);form.Close();
 }
 Console.WriteLine("DECLARATION_EDITOR_PASS checks="+checks);return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
