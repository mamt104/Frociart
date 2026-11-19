using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Frociart;

internal static class SourceViewerTests {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static int checks;
 static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,Flags).GetValue(obj);}
 static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,Flags).Invoke(obj,args);}
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
 static void Pump(int ms=230){var end=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();Thread.Sleep(3);}while(DateTime.UtcNow<end);}
 static string Bytes(Diagram d){return Convert.ToBase64String(DiagramIO.EncodeDocument(d));}
 static readonly string Example="FUNZIONE chiedi() -> INTERO valore\nLEGGI valore\nFINE FUNZIONE\nx = chiedi()\nSE x > 0 ALLORA\nSTAMPA \"Positivo 🐢\"\nALTRIMENTI\nSTAMPA \"Altro\"\nFINE SE";
 [STAThread] static int Main(){try{
  Application.EnableVisualStyles();Ui.SetSeparateIo(false,false);
  using(var f=new MainForm(null,true)){
   f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-3000,-3000);f.Size=new Size(1420,900);f.Show();Pump();f.EnterEditor();f.SetWorkspaceLayout(1,false);
   var doc=FlowPseudocode.Decode(Example);f.SetDocument(doc,null,false);Pump();string original=Bytes(doc);
   bool java=Environment.GetEnvironmentVariable("JAVA_SOURCE_VIEW")=="1";string requested=Environment.GetEnvironmentVariable("SOURCE_VIEW_LANGUAGE")??(java?"java":"python");
   var preview=Field<PseudocodePreview>(f,"livePseudocode");Check(!preview.SourceMode&&preview.Code.Visible,"Pseudocode remains default");preview.SetLanguage(requested);Pump();
   var view=preview.PythonCode;Check(view.Visible&&view.ReadOnly&&!preview.Code.Visible&&view.Source!=null&&view.Source.Language==requested,"Requested source language selectable in existing sidebar");Check(Bytes(doc)==original,"Opening source leaves document unchanged");
   Check(view.Text==SourceCodeDialog.ProgramText(view.Source),"RichTextBox preserves exact LF and UTF16 source offsets");
   var decision=doc.Nodes.Single(n=>n.Kind==NodeKind.Decision);f.Canvas.SelectNode(decision.Id);Pump();Check(view.HighlightedNode==decision.Id&&view.HighlightedFunction==null,"Canvas selection highlights decision source");
   var mapped=view.Source.ForBlock(null,decision.Id);Check(mapped.Count>1,"Decision has disjoint mapped source lines");
   foreach(var line in mapped){view.Select(line.Start,Math.Min(1,line.Length));Check(view.SelectionBackColor.ToArgb()==Ui.Tint.ToArgb(),"Mapped decision line painted "+line.Number);Check(view.SelectedOrigin().NodeId==decision.Id,"Caret maps to decision "+line.Number);}
   var output=doc.Nodes.First(n=>n.Kind==NodeKind.Output);var outputLine=view.Source.ForBlock(null,output.Id).First();view.Select(outputLine.Start,1);Check(view.SelectionBackColor.ToArgb()==view.BackColor.ToArgb(),"Decision does not paint intervening output block");
   int updates=view.HighlightUpdates,generations=preview.Generations;preview.Highlight(null,decision.Id,false);Check(view.HighlightUpdates==updates,"Unchanged identity does not repaint");
   var fn=doc.Functions[0];var input=fn.Body.Nodes.Single(n=>n.Kind==NodeKind.Input);var fnLine=view.Source.ForBlock(fn.Id,input.Id).First();view.Select(fnLine.Start,0);typeof(Control).GetMethod("OnKeyDown",Flags).Invoke(view,new object[]{new KeyEventArgs(Keys.Control|Keys.Enter)});Pump();Check(f.ActiveFunctionId==fn.Id&&f.Canvas.SelectedNodeId==input.Id,"Ctrl+Enter source navigation opens owning function and block");Check(Bytes(f.Project)==original,"Source navigation preserves entire document");
   f.ActivateFunction(null,false);Call(f,"ResetRun",false);bool entered=false,waiting=false;
   for(int i=0;i<35;i++){
    f.SingleStep();Pump(50);Check(view.HighlightedFunction==f.Runner.CurrentFunctionId&&view.HighlightedNode==f.Canvas.CurrentNodeId,"Step source matches current graph "+i);
    Check(preview.Generations==generations,"Stepping does not regenerate source "+i);entered|=f.Runner.CurrentFunctionId==fn.Id;
    if(f.Runner.State==RuntimeState.WaitingForInput){waiting=true;Check(view.HighlightedNode==input.Id&&preview.State.Text.Contains("riga"),"Input highlights actual function input");Field<TextBox>(f,"inputValue").Text="3";f.SubmitInput();}
    if(f.Runner.State==RuntimeState.Completed||f.Runner.State==RuntimeState.Error)break;
   }
   Check(entered&&waiting&&f.Runner.State==RuntimeState.Completed&&f.Runner.Output.Single()=="Positivo 🐢","Step into, input and return execute actual program");
   Call(f,"ResetRun",false);f.ActivateFunction(null,false);f.SetBreakpoint(decision,true,"x > 0");f.StartRun();Field<ComboBox>(f,"speed").SelectedIndex=3;
   for(int i=0;i<100&&Field<System.Windows.Forms.Timer>(f,"runTimer").Enabled;i++)Call(f,"RunTick");Check(f.Runner.State==RuntimeState.WaitingForInput,"Automatic run stops at input");Field<TextBox>(f,"inputValue").Text="3";f.SubmitInput();
   for(int i=0;i<100&&Field<System.Windows.Forms.Timer>(f,"runTimer").Enabled;i++)Call(f,"RunTick");Check(Field<FlowDebugger>(f,"debugger").AtBreakpoint&&view.HighlightedNode==decision.Id&&view.HighlightedFunction==null,"Breakpoint highlights exact unexecuted decision");f.StartRun();
   for(int i=0;i<100&&Field<System.Windows.Forms.Timer>(f,"runTimer").Enabled;i++)Call(f,"RunTick");Check(f.Runner.State==RuntimeState.Completed&&view.HighlightedNode==f.Runner.CurrentNodeId,"Automatic execution finishes with matching final highlight");f.ClearBreakpoints();
   Call(f,"ResetRun",false);f.ActivateFunction(null,false);f.Canvas.SelectNode(output.Id);var text=Field<TextBox>(f,"expression");text.Text="stampa \"";Pump();Check(view.Source==null&&view.TextLength==0&&!Field<FlowButton>(preview,"copy").Enabled,"Invalid draft clears source and disables export");text.Text="stampa \"Ripreso\"";Pump();Check(view.Source!=null&&view.Text.Contains("Ripreso"),"Valid draft recovers live code");Check(Bytes(f.Project)==original,"Live draft does not commit document");f.CommitInspector();Pump();
   generations=preview.Generations;for(int i=0;i<40;i++){f.Project.Nodes[0].X++;Call(f,"MarkDirty");}Pump();Check(preview.Generations==generations&&!Field<System.Windows.Forms.Timer>(preview,"timer").Enabled,"Position changes do not regenerate and timer rests");
   foreach(bool dark in new[]{true,false}){Ui.SetDarkMode(dark,false);foreach(var size in new[]{new Size(1420,900),new Size(1120,720)}){f.Size=size;Pump();f.Canvas.SelectNode(output.Id);Pump(30);var line=view.Source.ForBlock(null,output.Id).First();view.Select(line.Start,1);Check(view.SelectionBackColor.ToArgb()==Ui.Tint.ToArgb(),"Theme preserves highlight dark="+dark);Check(view.Width>=250&&view.Height>=100,"Source pane remains usable at "+size);using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,f.Size));bitmap.Save(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"source-"+dark+"-"+size.Width+".png"));}}}
   generations=preview.Generations;f.Project.Nodes.First(n=>n.Kind==NodeKind.Output).Comment="Spiega il risultato";Call(f,"MarkDirty");Pump();Check(preview.Generations==generations+1&&view.Source.Text.Contains("Spiega il risultato"),"Attached-comment edits refresh the live source");
   var many=FlowPseudocode.Decode(string.Join("\n",Enumerable.Range(0,90).Select(i=>"x = "+i)));f.SetDocument(many,null,false);Pump();f.Canvas.Focus();view.Select(0,0);var last=many.Nodes.Last(n=>n.Kind==NodeKind.Process);f.Canvas.SelectNode(last.Id);Pump();int mappedLine=view.Source.ForBlock(null,last.Id).First().Start;Point position=view.GetPositionFromCharIndex(mappedLine);Check(position.Y>=0&&position.Y<view.ClientSize.Height,"Follow brings distant source line into visible viewport");Check(view.SelectionStart==0,"Following preserves user's caret");
   f.SetDocument(DiagramIO.CreateBlank(),null,false);f.Close();
  }
  Console.WriteLine("SOURCE_VIEWER_PASS checks="+checks);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
