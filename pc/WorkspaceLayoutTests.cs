using System;using System.Drawing;using System.IO;using System.Linq;using System.Reflection;using System.Threading;using System.Windows.Forms;using Frociart;
internal static class WorkspaceLayoutTests {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static int checks;static string root;
 static T Field<T>(object o,string n){return(T)o.GetType().GetField(n,F).GetValue(o);}
 static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;Console.WriteLine("PASS "+why);}
 static void Pump(int ms=300){var end=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();Thread.Sleep(2);}while(DateTime.UtcNow<end);}
 static string Bytes(Diagram d){return Convert.ToBase64String(DiagramIO.EncodeDocument(d));}
 static void Capture(Control c,string name){using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);b.Save(Path.Combine(root,name+".png"));}}
 static FlowButton Button(Control c,string name){return c.Controls.OfType<FlowButton>().Single(x=>x.Name==name);}
 [STAThread] static int Main(string[] args){try{
  root=args[0];Application.EnableVisualStyles();Ui.SetSeparateIo(false,false);Ui.SetDarkMode(true,false);UiMotion.SetEnabled(true);
  var defaults=WorkspaceLayout.Load();Check(defaults.Mode==0&&!defaults.Advanced&&!defaults.Code,"First start uses Essential");
  using(var f=new MainForm(null,true)){
   f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-3000,-3000);f.Size=new Size(1420,900);f.Show();Pump();f.EnterEditor();f.SetDocument(Examples.Parity(),null,false);Pump();
   var palette=Field<FlowScrollPanel>(f,"ioPalette");var preview=Field<PseudocodePreview>(f,"livePseudocode");
   Check(!preview.Visible&&palette.Controls.OfType<BlockButton>().Count()==5,"Essential shows common blocks and collapses code");
   foreach(string name in new[]{"AdvancedPaletteGroup","FilePaletteGroup","TurtlePaletteGroup","NotesPaletteGroup","DebugPaletteGroup"})Button(palette,name).PerformClick();
   Check(palette.Controls.OfType<BlockButton>().Select(b=>b.Kind).Distinct().Count()==Enum.GetValues(typeof(NodeKind)).Length-1,"Every block remains available without changing Essential mode");
   Check(f.WorkspaceMode==0,"Expanding groups does not force a different mode");
   Field<FlowButton>(f,"workspaceCodeToggle").PerformClick();Pump();Check(preview.Visible&&preview.Code.Text.Length>0,"Essential can open live code directly");
   f.SetWorkspaceLayout(0,false);Pump();int generated=preview.Generations;preview.Schedule();Pump();Check(preview.Generations==generated,"Collapsed preview does not generate on idle");
   f.Canvas.SelectNode("input");var editor=Field<TextBox>(f,"expression");editor.Text="draft_not_committed +";
   string graph=Bytes(f.Project),selection=f.Canvas.SelectedNodeId;var undo=Field<System.Collections.ICollection>(f,"undo").Count;
   foreach(int mode in new[]{1,2,0,2,1}){
    f.SetWorkspaceLayout(mode,true);Pump();Check(Bytes(f.Project)==graph&&f.Canvas.SelectedNodeId==selection&&editor.Text=="draft_not_committed +","Mode switch preserves graph, selection and uncommitted draft "+mode);
    Check(Field<System.Collections.ICollection>(f,"undo").Count==undo,"Layout is not a document edit "+mode);
    Check(preview.Visible==(mode>0),"Preview follows requested layout "+mode);
    Check(palette.Controls.OfType<BlockButton>().Count()==(mode==0?5:mode==1?8:Enum.GetValues(typeof(NodeKind)).Length-1),"Preset groups match layout "+mode);
   }
   Check(WorkspaceLayout.Load().Mode==1,"Chosen layout persists");
   // The layout button now opens the Vista menu and its layout submenu.
   Field<FlowButton>(f,"workspaceButton").PerformClick();Pump();
   var layoutMenu=Field<ContextMenuStrip>(f,"moreMenu");
   var layouts=layoutMenu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Name=="LayoutChoices");
   bool opened=layoutMenu.Visible&&layouts.DropDown.Visible;layouts.HideDropDown();layoutMenu.Close();Pump();
   Check(opened&&f.WorkspaceMode==1&&Bytes(f.Project)==graph,"Native layout menu cancel preserves state");
   editor.Text="x";f.SetDocument(Examples.Parity(),null,false);f.SingleStep();f.SingleStep();var runner=f.Runner;var state=runner.State;Field<TextBox>(f,"inputValue").Text="17";
   foreach(int mode in new[]{2,0,1}){f.SetWorkspaceLayout(mode,false);Check(ReferenceEquals(runner,f.Runner)&&runner.State==state&&Field<TextBox>(f,"inputValue").Text=="17","Layout preserves execution and typed input "+mode);}
   f.SetDocument(DiagramIO.CreateBlank(),null,false);
   foreach(bool dark in new[]{true,false})foreach(int width in new[]{1120,1420})foreach(int mode in new[]{0,1,2}){
    Ui.SetDarkMode(dark,false);f.Size=new Size(width,900);f.SetWorkspaceLayout(mode,false);Pump();
    var bar=Field<Panel>(f,"graphBar");var profile=Field<FlowButton>(f,"workspaceButton");var manager=Field<FlowButton>(f,"functionManager");
    Check(profile.Right<=bar.ClientSize.Width&&manager.Right<=profile.Left,"Graph and layout controls do not overlap "+dark+"/"+width+"/"+mode);
    var toggle=Field<FlowButton>(f,"workspaceCodeToggle");Check(toggle.Height>=30&&toggle.Width>250,"Code reveal control remains usable");
    Check(palette.Controls.OfType<BlockButton>().All(b=>b.Left>=0&&b.Right<=palette.ClientSize.Width),"Palette blocks fit available width");
    if(width==1420)Capture(f,"workspace-"+(dark?"dark":"light")+"-"+mode);
   }
   UiMotion.SetEnabled(false);f.SetWorkspaceLayout(0,false);Pump();Check(FlowTransitions.Active==0,"Reduced motion settles without overlays");
   f.SetDocument(DiagramIO.CreateBlank(),null,false);f.Close();Pump();
  }
  using(var f=new MainForm(null,true)){Check(f.WorkspaceMode==1,"Next window restores saved mode, not later temporary test modes");}
  File.WriteAllText(WorkspaceLayout.PathName,"invalid");Check(WorkspaceLayout.Load().Mode==0,"Malformed layout preference recovers to Essential");
  Console.WriteLine("WORKSPACE_LAYOUT_PASS checks="+checks+"; native controls, no hardware mouse");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
