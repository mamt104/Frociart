using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Frociart.Tests {
internal static class InputPromptTests {
    static int checks;
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static T Field<T>(object owner,string name){return(T)owner.GetType().GetField(name,flags).GetValue(owner);}
    static void Refresh(MainForm form){typeof(MainForm).GetMethod("RefreshRuntime",flags).Invoke(form,null);}
    static void Pump(int ms=40){var w=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(2);}while(w.ElapsedMilliseconds<ms);}
    static Diagram Graph(string name="A",string prompt="Inserisci un valore per x"){
        var d=new Diagram{Title="QA domande Input A e B"};
        d.Nodes.AddRange(new[]{new FlowNode{Id="s",Kind=NodeKind.Start,Text="Inizio",X=200},new FlowNode{Id="a",Kind=NodeKind.Input,Text=name,Prompt=prompt,X=170,Y=140},new FlowNode{Id="b",Kind=NodeKind.Input,Text="B",Prompt="Inserisci un valore per x",X=170,Y=290},new FlowNode{Id="o",Kind=NodeKind.Output,Text="\"fine\"",X=170,Y=440},new FlowNode{Id="e",Kind=NodeKind.End,Text="Fine",X=200,Y=580}});
        for(int n=1;n<d.Nodes.Count;n++)d.Edges.Add(new FlowEdge{From=d.Nodes[n-1].Id,To=d.Nodes[n].Id});
        return d;
    }
    static Interpreter Waiting(Diagram d){var r=new Interpreter(d);r.Step();r.Step();Check(r.State==RuntimeState.WaitingForInput,"Actual interpreter waits at the first Input");return r;}
    static void Capture(Control c,string name){Pump(330);string dir=Environment.GetEnvironmentVariable("REVIEW_OUTPUT");Directory.CreateDirectory(dir);using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);b.Save(Path.Combine(dir,name+".png"));}}
    [STAThread]static int Main(){try{
        Application.EnableVisualStyles();Ui.SetDarkMode(true,false);UiMotion.SetEnabled(true);
        var legacy=Graph();var old=Waiting(legacy);
        Console.WriteLine("OBSERVED variable="+old.InputVariable+"; question="+old.InputPrompt);
        Check(old.InputPrompt=="Inserisci un valore per A","Legacy default x prompt follows the actual variable A");
        Check(legacy.Nodes[1].Prompt=="Inserisci un valore per x","Running a legacy document does not rewrite the saved prompt");
        foreach(string name in new[]{"A","B","x","temperatura","_valore2"}){
            foreach(string prompt in new[]{"",null," \t"}){
                var r=Waiting(Graph(name,prompt));Check(r.InputVariable==name&&r.InputPrompt=="Inserisci un valore per "+name,"Automatic question uses exact current identifier "+name);
            }
        }
        string custom="Quanto vale x nel problema?\nScrivi il primo numero.";
        var customGraph=Graph("A",custom);Check(Waiting(customGraph).InputPrompt==custom,"Custom questions are preserved verbatim, including a different mentioned name");
        var directory=Environment.GetEnvironmentVariable("REVIEW_OUTPUT");Directory.CreateDirectory(directory);string file=Path.Combine(directory,"synthetic-input-legacy.frociart");DiagramIO.Save(file,legacy);var loaded=DiagramIO.Load(file);Check(Waiting(loaded).InputPrompt=="Inserisci un valore per A","Existing saved projects use the correct variable after native save/load");
        using(var canvas=new FlowCanvas{Diagram=new Diagram(),Size=new Size(500,500)}){
            var created=canvas.AddLooseNode(NodeKind.Input);Check(created!=null&&created.Text=="x"&&String.IsNullOrEmpty(created.Prompt),"New palette Input stores an automatic question, never a fixed x prompt");
            created.Id="a";created.Text="A";var d=Graph("A",created.Prompt);d.Nodes[1]=created;Check(Waiting(d).InputPrompt=="Inserisci un valore per A","Renamed newly created Input asks for its new name");
        }
        using(var form=new MainForm(null,true)){
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2800,-2200);form.Size=new Size(1120,760);form.Show();Pump(330);form.EnterEditor();Pump(330);
            form.SetDocument(Graph("x",""),null,false);form.Canvas.SelectNode("a");Field<TextBox>(form,"expression").Text="A";form.CommitInspector();Check(form.Document.Nodes.First(n=>n.Id=="a").Text=="A","Native inspector commits the renamed Input");
            form.SingleStep();form.SingleStep();Pump(330);
            var question=Field<Label>(form,"inputQuestion");var value=Field<TextBox>(form,"inputValue");var panel=Field<Panel>(form,"inputPanel");var tabs=Field<SidebarTabs>(form,"tabs");
            Check(panel.Visible&&question.Visible&&question.Text=="Inserisci un valore per A","Visible native question agrees with selected Input A");
            Check(value.AccessibleName=="Valore per A","Native value field identifies the current variable for accessibility");Capture(panel,"input-prompt-A-dark");
            value.Text="Infinity";form.SubmitInput();Check(form.Runner.State==RuntimeState.WaitingForInput&&value.Text=="Infinity"&&question.Text.EndsWith(" A"),"Rejected input keeps the question and entered text for correction");
            value.Text="7";form.SubmitInput();Check((double)form.Runner.Variables["A"]==7&&!panel.Visible,"Accepted value is stored in A and its input card closes");
            tabs.SelectedTab=Field<Panel>(form,"blockTab");form.Runner.Step();Refresh(form);
            Check(!panel.Visible&&question.Text=="Inserisci un valore per B","Next question updates to B even while the runtime tab is hidden");
            value.Text="bozza B";Refresh(form);Refresh(form);Check(value.Text=="bozza B","Hidden runtime refresh preserves the input draft for the same waiting step");
            tabs.SelectedTab=Field<Panel>(form,"runTab");Pump(330);Check(panel.Visible&&question.Text=="Inserisci un valore per B"&&value.AccessibleName=="Valore per B","Reopening runtime shows B without a stale A question");Capture(panel,"input-prompt-B-dark");
            value.Text="5";form.SubmitInput();Check((double)form.Runner.Variables["A"]==7&&(double)form.Runner.Variables["B"]==5,"Consecutive input values reach their respective variables A and B");
            form.SingleStep();form.SingleStep();Check(form.Runner.State==RuntimeState.Completed&&form.Runner.Output.Single()=="fine","Consecutive Input graph completes normally");
            form.SetDocument(Graph("A",custom),null,false);form.SingleStep();form.SingleStep();Check(question.Text==custom,"Native runtime displays a custom question unchanged");
            form.SetDocument(Graph(),null,false);form.SingleStep();form.SingleStep();Check(question.Text=="Inserisci un valore per A"&&value.Text=="","New runner clears the old input draft and resolves legacy prompt");
            Ui.SetDarkMode(false,false);Refresh(form);Check(question.Text=="Inserisci un valore per A"&&question.Visible,"Theme change preserves the current variable question");Capture(panel,"input-prompt-A-light");
            form.SetDocument(Graph(),null,false);form.Close();Pump(330);
        }
        Check(FlowTransitions.Active==0,"Closing releases all runtime transition overlays");
        Console.WriteLine("INPUT_PROMPT_PASS checks="+checks+"; real interpreter, native controls and synthetic save/load; external hardware mouse NOT_RUN");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
}
