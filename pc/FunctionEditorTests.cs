using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Frociart;

internal static class FunctionEditorTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,Flags).GetValue(obj);}
    static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,Flags).Invoke(obj,args);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static void Pump(int ms=240){var end=DateTime.UtcNow.AddMilliseconds(ms);do{Application.DoEvents();Thread.Sleep(3);}while(DateTime.UtcNow<end);}
    static string Bytes(Diagram d){return Convert.ToBase64String(DiagramIO.EncodeDocument(d));}
    static readonly string Example="FUNZIONE doppio(n: INTERO) -> INTERO risultato\nINIZIO\nrisultato = n * 2\nFINE\nFINE FUNZIONE\nINIZIO\nSTAMPA doppio(3)\nFINE";
    static Diagram Project(){return FlowPseudocode.Decode(Example);}
    static string Run(Diagram d){var r=new Interpreter(d);while(r.State!=RuntimeState.Completed&&r.State!=RuntimeState.Error)r.Step();Check(r.State==RuntimeState.Completed,"Edited project executes");return String.Join("\n",r.Output);}
    static void Rejected(Action action,string text){try{action();throw new Exception("Accepted: "+text);}catch(FlowException){checks++;Console.WriteLine("PASS "+text);}catch(InvalidDataException){checks++;Console.WriteLine("PASS "+text);}}
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles();ModelChecks();
            using(var form=new MainForm(null,true))
            {
                form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-3000,-3000);form.Show();Pump();form.EnterEditor();form.SetWorkspaceLayout(1,false);
                var project=Project();form.SetDocument(project,null,false);Pump();string id=project.Functions[0].Id;
                var selector=Field<ComboBox>(form,"graphChoice");Check(selector.Items.Count==2&&form.Document==form.Project,"Selector contains main and function");
                selector.SelectedIndex=1;Pump();Check(form.ActiveFunctionId==id&&form.Document==form.Project.Functions[0].Body&&form.Canvas.Diagram==form.Document,"Function navigation uses owned graph");
                var op=form.Document.Nodes.Single(n=>n.Kind==NodeKind.Process);form.Canvas.SelectNode(op.Id);var editor=Field<TextBox>(form,"expression");editor.Text="risultato = n * 3";Pump();
                var preview=Field<PseudocodePreview>(form,"livePseudocode");Check(preview.Code.Text.Contains("n * 3")&&preview.Code.Text.Contains("STAMPA doppio(3)"),"Live code includes draft in function and main");Check(op.Text.Contains("n * 2"),"Live function preview does not commit draft");
                selector.SelectedIndex=0;Pump();Check(form.Project.Functions[0].Body.Nodes.Single(n=>n.Kind==NodeKind.Process).Text.Contains("n * 3"),"Navigation commits function draft");
                Call(form,"Undo");Pump();Check(form.ActiveFunctionId==id&&form.Project.Functions.Count==1&&form.Document.Nodes.Single(n=>n.Kind==NodeKind.Process).Text.Contains("n * 2"),"Undo restores entire project and owning diagram");
                Call(form,"Redo");Pump();Check(Run(form.Project)=="9","Redo restores function edit and caller");
                string saved=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"functions-editor-save.frociart");form.SetDocument(form.Project,saved,false);form.ActivateFunction(id,false);Call(form,"SaveDocument",false);var loaded=DiagramIO.Load(saved);Check(loaded.Functions.Count==1&&Run(loaded)=="9","Saving from a function retains main and all bodies");
                var changed=form.Project.Functions[0].Copy();changed.Name="triplo";form.ApplyFunctionProject(FunctionEditing.Save(form.Project,changed,null),id);Pump();Check(FlowPseudocode.Encode(form.Project).Contains("STAMPA triplo(3)"),"Editor applies rename across caller and metadata");Call(form,"Undo");Pump();Check(form.Project.Functions[0].Name=="doppio"&&Run(form.Project)=="9","Undo of rename restores calls and signature");Call(form,"Redo");Pump();Check(form.Project.Functions[0].Name=="triplo","Redo of rename restores signature");
                PendingRenameChecks(form);RuntimeChecks(form);Limits(form);DialogChecks();HistoryChecks(form);RecoveryChecks(form);
                foreach(bool dark in new[]{true,false}){Ui.SetDarkMode(dark,false);form.SetDocument(Project(),null,false);form.ActivateFunction(form.Project.Functions[0].Id,false);foreach(var size in new[]{new Size(1420,900),new Size(1120,720)}){form.Size=size;Pump();var bar=Field<Panel>(form,"graphBar");var manager=Field<FlowButton>(form,"functionManager");Check(manager.Right<=bar.Width&&selector.Right<manager.Left,"Navigation fits at "+size.Width+" dark="+dark);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(Path.GetDirectoryName(saved),"function-editor-"+dark+"-"+size.Width+".png"));}}}
                form.SetDocument(DiagramIO.CreateBlank(),null,false);form.Close();
            }
            Console.WriteLine("FUNCTION_EDITOR_PASS checks="+checks);return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
    static void ModelChecks()
    {
        var p=Project();string before=Bytes(p);var f=p.Functions[0].Copy();f.Name="calcola";f.Parameters[0].Name="valore";f.ReturnVariable="totale";
        var edited=FunctionEditing.Save(p,f,new Dictionary<string,string>{{"n","valore"},{"risultato","totale"}});Check(Bytes(p)==before,"Metadata edit leaves original untouched");Check(Run(edited)=="6","Function and parameter/result rename preserves execution");
        Check(FunctionEditing.RewriteCalls("doppio + DOPPIO (2) + \"doppio(3)\" + 'doppio(4)'","doppio","calcola")=="doppio + calcola (2) + \"doppio(3)\" + 'doppio(4)'","Rename changes calls only, preserving variables and quoted text");
        Rejected(()=>FunctionEditing.Delete(p,p.Functions[0].Id),"Referenced function cannot be deleted");Check(Bytes(p)==before,"Rejected deletion leaves original intact");
        f.Name="sin";Rejected(()=>FunctionEditing.Save(p,f,null),"Reserved function name rejected");
        f=p.Functions[0].Copy();f.Parameters.Add(f.Parameters[0].Copy());Rejected(()=>FunctionEditing.Save(p,f,null),"Duplicate parameters rejected");
        var unused=Project();unused.Nodes.Single(n=>n.Kind==NodeKind.Output).Text="\"doppio(3)\"";var deleted=FunctionEditing.Delete(unused,unused.Functions[0].Id);Check(deleted.Functions==null&&deleted.Version==1,"Deleting unreferenced function preserves quoted name and legacy format");
        f=p.Functions[0].Copy();f.Parameters[0].Name="risultato";Rejected(()=>FunctionEditing.Save(p,f,new Dictionary<string,string>{{"n","risultato"}}),"Parameter rename cannot merge an existing local variable");
    }
    static void PendingRenameChecks(MainForm form)
    {
        var p=Project();form.SetDocument(p,null,false);string id=p.Functions[0].Id;form.ActivateFunction(id,false);
        var op=form.Document.Nodes.Single(n=>n.Kind==NodeKind.Process);form.Canvas.SelectNode(op.Id);Field<TextBox>(form,"expression").Text="risultato = n * 5";
        bool dialogSeen=false,draftCommitted=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=50})
        {
            timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=form);if(dialog==null)return;dialogSeen=true;draftCommitted=op.Text=="risultato = n * 5";dialog.Close();timer.Stop();};
            timer.Start();Call(form,"ManageFunctions");
        }
        Check(dialogSeen&&draftCommitted,"Opening function manager commits pending inspector text before signature choices");
        using(var editor=new FunctionEditorDialog(form.Project,form.Project.Functions[0]))
        {
            editor.FunctionName.Text="calcola";editor.Parameters[0].Name.Text="dato";editor.ResultName.Text="finale";editor.Save();Check(editor.EditedProject!=null,"Metadata form accepts rename after pending block edit");form.ApplyFunctionProject(editor.EditedProject,id);
        }
        Check(form.Document.Nodes.Single(n=>n.Kind==NodeKind.Process).Text=="finale = dato * 5"&&Run(form.Project)=="15","Renamed body retains exact pending expression and executes");
        Call(form,"Undo");Check(form.Project.Functions[0].Name=="doppio"&&form.Document.Nodes.Single(n=>n.Kind==NodeKind.Process).Text=="risultato = n * 5","Undo rename retains newly committed draft");
        Call(form,"Undo");Check(form.Document.Nodes.Single(n=>n.Kind==NodeKind.Process).Text=="risultato = n * 2","Second Undo reverses only inspector edit");
        Call(form,"Redo");Call(form,"Redo");Check(form.Project.Functions[0].Name=="calcola"&&Run(form.Project)=="15","Redo restores block edit then rename without losing expression");
    }
    static void RuntimeChecks(MainForm form)
    {
        var p=FlowPseudocode.Decode("FUNZIONE chiedi() -> INTERO valore\nINIZIO\nLEGGI valore\nFINE\nFINE FUNZIONE\nINIZIO\nrisultato = chiedi()\nSTAMPA risultato\nFINE");form.SetDocument(p,null,false);form.ActivateFunction(p.Functions[0].Id,false);
        bool functionSeen=false,waiting=false;for(int i=0;i<25;i++)
        {
            form.SingleStep();Pump(20);Check(form.Canvas.Diagram==FunctionEditing.Graph(form.Project,form.Runner.CurrentFunctionId),"Canvas follows active frame at step "+i);
            string active=form.Canvas.CurrentNodeId;Check(active==null||form.Document.Nodes.Any(n=>n.Id==active),"Runtime highlight belongs to visible diagram "+i);
            if(form.Runner.CurrentFunctionId!=null){functionSeen=true;Check(Field<Label>(form,"varsLabel").Text.Contains("chiedi"),"Local variables identify current function");}
            if(form.Runner.State==RuntimeState.WaitingForInput){waiting=true;Check(Field<FlowButton>(form,"callStackButton").Text.Contains("2"),"Call stack identifies suspended caller");Field<TextBox>(form,"inputValue").Text="7";form.SubmitInput();}
            if(form.Runner.State==RuntimeState.Completed||form.Runner.State==RuntimeState.Error)break;
        }
        Check(functionSeen&&waiting&&form.Runner.State==RuntimeState.Completed&&form.Runner.Output.Single()=="7","Input in function returns correctly to main");Check(form.ActiveFunctionId==null&&form.Runner.Variables.ContainsKey("risultato")&&!form.Runner.Variables.ContainsKey("valore"),"Return switches canvas and local variables to caller");
    }
    static void Limits(MainForm form)
    {
        var p=Project();var body=p.Functions[0].Body;while(FunctionEditing.Graphs(p).Sum(g=>g.Nodes.Count)<DiagramIO.MaxNodes)body.Nodes.Add(new FlowNode{Kind=NodeKind.Process,Text="x = 0"});form.SetDocument(p,null,false);
        int before=p.Nodes.Count;Check(form.Canvas.AddLooseNode(NodeKind.Call)==null&&p.Nodes.Count==before,"Main insertion respects blocks in functions");form.ActivateFunction(p.Functions[0].Id,false);before=body.Nodes.Count;Check(form.Canvas.AddLooseNode(NodeKind.Call)==null&&body.Nodes.Count==before,"Function insertion respects whole project limit");
    }
    static void HistoryChecks(MainForm form)
    {
        var p=Project();form.SetDocument(p,null,false);form.ActivateFunction(p.Functions[0].Id,false);
        Check((bool)Call(form,"ApplyVariableRename","n","input"),"Rename variable acts on function parameter");Check(form.Project.Functions[0].Parameters[0].Name=="input"&&Run(form.Project)=="6","Renaming parameter updates signature and body");Call(form,"Undo");Check(form.Project.Functions[0].Parameters[0].Name=="n","Undo restores parameter metadata");
        var added=new FlowFunction{Name="saluta"};string original=Bytes(form.Project);form.ApplyFunctionProject(FunctionEditing.Save(form.Project,added,null),added.Id);Check(form.Project.Functions.Count==2&&form.ActiveFunctionId==added.Id,"Creating function opens its graph");Call(form,"Undo");Check(Bytes(form.Project)==original&&form.Document==form.Project.Functions[0].Body,"Undo creation restores previous active function");Call(form,"Redo");Check(form.ActiveFunctionId==added.Id&&form.Project.Functions.Count==2,"Redo recreates function and restores navigation");form.ApplyFunctionProject(FunctionEditing.Delete(form.Project,added.Id),null);Check(form.Project.Functions.Count==1&&form.ActiveFunctionId==null,"Deleting unused function returns to main");Call(form,"Undo");Check(form.Project.Functions.Count==2&&form.ActiveFunctionId==added.Id,"Undo deletion restores function body and active diagram");
        var body=Project();body.Functions.Add(added.Copy());var call=body.Nodes.Single(n=>n.Kind==NodeKind.Output);call.Kind=NodeKind.Call;call.Text="saluta()";form.SetDocument(body,null,false);for(int i=0;i<12&&(form.Runner==null||form.Runner.State!=RuntimeState.Completed);i++)form.SingleStep();Check(form.Runner.State==RuntimeState.Completed,"Call block invokes void function from editor");
        string svg=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"function-call.svg");DiagramExport.ExportSvg(form.Canvas,svg);Check(File.ReadAllText(svg).Contains("CHIAMATA")&&File.ReadAllText(svg).Contains("saluta()"),"SVG export preserves call label and expression");
        var fragment=new Diagram();fragment.Nodes.Add(call);var copied=FlowClipboard.Decode(FlowClipboard.Encode(fragment));Check(copied.Nodes.Single().Kind==NodeKind.Call&&copied.Nodes.Single().Text=="saluta()","Clipboard packet preserves call blocks without accessing OS clipboard");
    }
    static void RecoveryChecks(MainForm form)
    {
        string path=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"function-recovery-"+Guid.NewGuid().ToString("N")+".frociart");
        typeof(MainForm).GetField("recoveryPath",Flags).SetValue(form,path);typeof(MainForm).GetField("ignoreRecovery",Flags).SetValue(form,false);
        try{
            var p=Project();form.SetDocument(p,null,false);form.ActivateFunction(p.Functions[0].Id,false);form.Canvas.SelectNode(form.Document.Nodes.Single(n=>n.Kind==NodeKind.Process).Id);Field<TextBox>(form,"expression").Text="risultato = n + 4";Call(form,"SaveRecovery");
            var recovered=DiagramIO.Load(path);Check(recovered.Functions.Count==1&&Run(recovered)=="7","Recovery from a function preserves draft and all diagrams");Call(form,"LoadRecovery");Check(form.ActiveFunctionId==null&&form.Project.Functions.Count==1&&Run(form.Project)=="7","Recovery reopens entire project in main");
            form.ActivateFunction(form.Project.Functions[0].Id,false);typeof(FlowCanvas).GetField("_panning",Flags).SetValue(form.Canvas,true);form.SingleStep();Check(form.Runner==null,"Active pointer gesture cannot start function execution");typeof(FlowCanvas).GetField("_panning",Flags).SetValue(form.Canvas,false);
            var node=form.Project.Nodes.Single(n=>n.Kind==NodeKind.Output);Check(TutorDiagnostics.Explain(node,null,FunctionModel.Catalog(form.Project)).Contains("Richiama: doppio"),"Explanation recognizes a user call without executing it");
        }finally{typeof(FlowCanvas).GetField("_panning",Flags).SetValue(form.Canvas,false);Call(form,"DeleteRecovery");typeof(MainForm).GetField("ignoreRecovery",Flags).SetValue(form,true);}
    }
    static void DialogChecks()
    {
        var p=Project();foreach(bool dark in new[]{true,false})
        {
            Ui.SetDarkMode(dark,false);using(var dialog=new FunctionEditorDialog(p,p.Functions[0]))
            {
                dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-3000,-3000);dialog.Show();Pump();dialog.FunctionName.Text="sin";dialog.Save();Check(dialog.EditedProject==null&&!dialog.IsDisposed,"Invalid metadata remains editable");dialog.FunctionName.Text="calcola";dialog.Parameters[0].Name.Text="dato";dialog.ResultName.Text="totale";dialog.Save();Check(dialog.EditedProject!=null&&Run(dialog.EditedProject)=="6","Parameter form produces executable project dark="+dark);
            }
            using(var dialog=new FunctionEditorDialog(p,null))
            {
                dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-3000,-3000);dialog.Show();for(int i=0;i<16;i++)dialog.AddParameter(new FlowParameter{Name="dato"+i},null);Pump();Check(dialog.Parameters.Count==16&&!Field<FlowButton>(dialog,"add").Enabled,"Parameter limit is visible and enforced");
                foreach(var size in new[]{new Size(760,750),new Size(650,610)}){dialog.Size=size;Pump();var panel=Field<FlowScrollPanel>(dialog,"parameterPanel");Check(!panel.HorizontalScroll.Visible,"Parameter rows fit without horizontal scrolling at "+size);Check(dialog.Apply.Visible&&dialog.Apply.Parent.Bottom<=dialog.ClientSize.Height,"Save remains reachable at "+size);using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"function-dialog-"+dark+"-"+size.Width+".png"));}}
                dialog.Close();
            }
        }
    }
}
