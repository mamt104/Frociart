using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Frociart.Tests {
internal static class HomeScreenTests {
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks;static string output;static Exception modalError;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,Flags).GetValue(o);}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control c in root.Controls)foreach(var item in All(c))yield return item;}
    static void Pump(){for(int i=0;i<10;i++)Application.DoEvents();}
    static void PumpFor(int milliseconds){var watch=System.Diagnostics.Stopwatch.StartNew();do{Application.DoEvents();System.Threading.Thread.Sleep(2);}while(watch.ElapsedMilliseconds<milliseconds);}
    static byte[] Bytes(Diagram diagram,string name){string path=Path.Combine(output,name+".frociart");DiagramIO.Save(path,diagram);return File.ReadAllBytes(path);}
    static void Capture(Form form,string name){Pump();using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(Path.Combine(output,name+".png"));}}
    static bool Inside(Control child,Control parent){var r=parent.RectangleToClient(child.RectangleToScreen(child.ClientRectangle));return parent.ClientRectangle.Contains(r);}
    static void DialogAction(MainForm main,Action click,Func<Form,bool> match,Action<Form> inspect){
        int ticks=0;modalError=null;using(var timer=new Timer{Interval=50}){
            timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=main&&match(f));
                if(dialog==null){if(++ticks>150){modalError=new Exception("Expected native dialog did not open");timer.Stop();foreach(var f in Application.OpenForms.Cast<Form>().Where(f=>f!=main).ToArray())f.Close();}return;}
                timer.Stop();try{inspect(dialog);}catch(Exception error){modalError=error;}finally{dialog.Close();}};
            timer.Start();click();timer.Stop();if(modalError!=null)throw modalError;
        }Pump();
    }
    [STAThread]static int Main(){try{
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Ui.SetDarkMode(false,false);UiMotion.SetEnabled(false);TutorFeedbackSignals.SetEnabled(false);
        output=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"home-screen");Directory.CreateDirectory(output);
        string data=Environment.GetEnvironmentVariable("FROCIART_DATA_DIR");Check(!String.IsNullOrEmpty(data),"QA has an isolated data directory");Environment.SetEnvironmentVariable("FROCIART_DATA_DIR",Path.Combine(data,"home-"+Guid.NewGuid().ToString("N")));
        using(var main=new MainForm(null,true)){
            main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-3000,-2500);main.ShowInTaskbar=false;main.Show();Pump();
            var home=Field<HomeScreen>(main,"homeScreen");var scroll=Field<Panel>(main,"homePanel");
            Check(scroll.Visible&&!Field<Panel>(main,"commandHost").Visible,"Startup opens the actual home without editor commands");
            Check(!home.LessonResume.Visible,"Fresh home does not invent an active lesson");
            byte[] original=Bytes(main.Document,"original");
            foreach(var size in new[]{new Size(1420,900),new Size(1120,720),new Size(1920,1080)})foreach(bool dark in new[]{false,true}){
                main.Size=size;Ui.SetDarkMode(dark,false);main.ShowHome();Pump();
                Check(home.Columns,"Desktop width lays the main choices side by side");
                Check(Inside(home.LearnButton,scroll)&&Inside(home.NewButton,scroll),"Both primary actions are visible without scrolling: "+size+" dark="+dark);
                Check(All(home).OfType<HomeActionRow>().Where(row=>row.Visible).All(row=>row.Height>=row.MeasureHeight(row.Width)),"All visible action descriptions fit their row height");
                Check(home.LearnButton.Parent.Right<home.NewButton.Parent.Left,"Learn and Create cards have distinct, nonoverlapping bounds");
                foreach(var row in new[]{home.DocumentResume,home.OpenProject,home.Workshop,home.Glossary,home.Advanced}){scroll.ScrollControlIntoView(row);Pump();Check(Inside(row,scroll),"Secondary action can be scrolled fully into view: "+row.Text);}
                scroll.AutoScrollPosition=Point.Empty;Pump();Check(!scroll.HorizontalScroll.Visible,"Home has no horizontal scrollbar after resizing and navigation");Capture(main,"home-"+size.Width+"-"+(dark?"dark":"light"));
            }
            Check(original.SequenceEqual(Bytes(main.Document,"after-layout")),"Home, themes and resizing preserve the entire open diagram");
            var tutorStore=new TutorStore();var lesson=Tutor.Lesson("b03");var session=new TutorSession{Id="b03",Document=lesson.Seed(),Step=0};
            var target=session.Document.Nodes.First(n=>n.Kind==NodeKind.Output);session.Drafts.Add(new TutorDraft{Node=target.Id,Text="2 + 3",Prompt=""});
            Check(tutorStore.Save(session),"Resume fixture is durably saved with an unapplied draft");
            main.ShowHome();Pump();Check(home.LessonResume.Visible&&home.LessonResume.Detail==lesson.title,"Resume uses the title of the real stored unfinished lesson");
            DialogAction(main,()=>home.LessonResume.PerformClick(),f=>f is TutorDialog&&((TutorDialog)f).Current!=null,f=>{
                var dialog=(TutorDialog)f;Check(dialog.Current.Id=="b03","Resume opens exactly the stored lesson");Check(dialog.Current.Drafts.Any(d=>d.Text=="2 + 3"),"Resume keeps the unapplied draft intact");});
            Check(tutorStore.Peek("b03").Drafts.Any(d=>d.Text=="2 + 3"),"Closing the lesson preserves its draft on disk");
            DialogAction(main,()=>home.LearnButton.PerformClick(),f=>f is TutorDialog,f=>Check(All(f).OfType<TutorUnitButton>().Count()==6,"Learn opens all six units of the real course"));
            DialogAction(main,()=>home.Workshop.PerformClick(),f=>f is TutorDialog&&((TutorDialog)f).Current!=null,f=>Check(((TutorDialog)f).Current.Id=="workshop","Laboratory opens its independent guided workspace"));
            DialogAction(main,()=>home.Glossary.PerformClick(),f=>f.Text.StartsWith("Blocchi, simboli"),f=>Check(f.Owner==main,"Glossary opens directly from the home without an extra course window"));
            Check(original.SequenceEqual(Bytes(main.Document,"after-study")),"Learning, laboratory and reference leave the free diagram unchanged");
            main.ShowHome();home.NewButton.PerformClick();Pump();
            Check(main.Document.Nodes.Count==2&&main.Document.Nodes.Any(n=>n.Kind==NodeKind.Start)&&main.Document.Nodes.Any(n=>n.Kind==NodeKind.End)&&main.Document.Edges.Count==1,"New project creates the real PC blank template with Start and End");
            Check(!scroll.Visible,"New project opens the free editor");
            var blank=main.Document;main.ShowHome();home.DocumentResume.PerformClick();Pump();Check(Object.ReferenceEquals(blank,main.Document)&&!scroll.Visible,"Resume returns to the same current document");
            main.MinimumSize=new Size(1000,700);main.Size=new Size(1080,900);Pump();
            var focus=Field<FlowButton>(main,"focusCanvasButton");var grid=Field<FlowButton>(main,"gridButton");
            Check(focus.RectangleToScreen(focus.ClientRectangle).Right+4<=grid.RectangleToScreen(grid.ClientRectangle).Left,"Portrait monitor keeps Solo foglio and Griglia separated");
            var diagram=Examples.Parity();foreach(var node in diagram.Nodes){node.X+=1200;node.Y+=700;}main.SetDocument(diagram,null,false);main.Canvas.Zoom=2.4f;Pump();byte[] beforeFit=Bytes(diagram,"before-fit");
            All(main).OfType<FlowButton>().First(b=>b.Text=="Inquadra").PerformClick();Pump();
            Check(main.Canvas.Zoom<2.4f,"Tutto adjusts zoom to fit the diagram");Check(beforeFit.SequenceEqual(Bytes(diagram,"after-fit")),"Tutto changes no node positions, contents, branches or edges");
            Check(All(main).OfType<FlowButton>().Any(b=>b.AccessibleName=="Pagina iniziale"),"Home is directly reachable from the persistent toolbar");
            UiMotion.SetEnabled(true);main.ShowHome();
            var entrance=home.Controls.OfType<PageEntrance>().Single();Check(entrance.HasFrames&&entrance.Progress<1,"Home starts a real staggered entrance before its first animation frame");
            var settledBounds=home.LearnButton.Parent.Bounds;PumpFor(80);Check(home.Controls.OfType<PageEntrance>().Any()&&entrance.Progress>0&&entrance.Progress<1,"Initial choices have intermediate animation frames");
            Check(home.LearnButton.Parent.Bounds==settledBounds&&home.LearnButton.Enabled,"Entrance keeps native action geometry, focus and commands available");
            using(var middleFrame=new Bitmap(home.Width,home.Height))using(var finalFrame=new Bitmap(home.Width,home.Height)){
                entrance.DrawToBitmap(middleFrame,new Rectangle(Point.Empty,entrance.Size));middleFrame.Save(Path.Combine(output,"home-entrance-middle.png"));PumpFor(650);home.DrawToBitmap(finalFrame,new Rectangle(Point.Empty,home.Size));finalFrame.Save(Path.Combine(output,"home-entrance-settled.png"));int changed=0;for(int y=0;y<home.Height;y+=5)for(int x=0;x<home.Width;x+=5)if(middleFrame.GetPixel(x,y)!=finalFrame.GetPixel(x,y))changed++;
                Check(changed>40,"Rendered home entrance differs visibly from the settled page, not just timer state");
            }
            PumpFor(650);Check(!home.Controls.OfType<PageEntrance>().Any()&&FlowTransitions.Active==0&&PopupGhost.ActiveCount==0,"Home entrance settles and releases all snapshots and timers");
            home.DocumentResume.PerformClick();Check(FlowTransitions.Active>0,"Opening the editor holds the outgoing home before painting its replacement");PumpFor(400);
            main.ShowHome();Check(FlowTransitions.Active>0&&home.Controls.OfType<PageEntrance>().Any(),"Returning home combines the outgoing page fade with card entrance");
            UiMotion.SetEnabled(false);PumpFor(40);Check(!home.Controls.OfType<PageEntrance>().Any()&&PopupGhost.ActiveCount==0,"Disabling motion immediately releases home and page animations");
            main.ShowHome();main.Size=new Size(1420,900);Ui.SetDarkMode(false,false);Capture(main,"home-with-resume-light");Ui.SetDarkMode(true,false);Capture(main,"home-with-resume-dark");main.Close();
        }
        Console.WriteLine("HOME_SCREEN_FINISHED checks="+checks+" errors=0; native controls and real stores; external PC input checked separately.");return 0;
    }catch(Exception error){Console.WriteLine(error);return 1;}}
}}
