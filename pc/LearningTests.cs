using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace Frociart.Tests {
internal sealed class SnapProbe:FlowCanvas {
 internal Keys Mods;
 protected override Keys GetInteractionModifiers(){return Mods;}
 internal void Mouse(string phase,Point p){var e=new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0);if(phase=="down")OnMouseDown(e);else if(phase=="move")OnMouseMove(e);else OnMouseUp(e);}
}
internal static class LearningTests {
 static int checks;static string dir;static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static void Check(bool value,string text){if(!value)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static void Pump(){for(int i=0;i<3;i++)Application.DoEvents();}
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,flags).GetValue(value);}
 static Control Find(Control root,string text){foreach(Control c in root.Controls){if(c.Text==text)return c;var found=Find(c,text);if(found!=null)return found;}return null;}
 static void Click(Control root,string text){var b=Find(root,text) as ButtonBase;if(b==null)throw new Exception("Missing control: "+text);if(b is Button)((Button)b).PerformClick();else ((FlowButton)b).PerformClick();Pump();}
 static void Capture(Form form,string name){Pump();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(Path.Combine(dir,name));}}
 static void Complete(LearningState s){s.Start();while(!s.Finished){s.Choose(s.Lesson.questions[s.Question].correct);s.Advance();}}
 static bool OpenStudy(MainForm main,bool practice,DialogResult discard){
  bool seen=false,asked=false;int ticks=0;using(var timer=new Timer{Interval=25}){
   timer.Tick+=delegate{
    var study=Application.OpenForms.Cast<Form>().OfType<LearningDialog>().FirstOrDefault();
    var question=Application.OpenForms.Cast<Form>().OfType<FlowMessageDialog>().FirstOrDefault();
    if(study!=null&&!seen){seen=true;if(practice){var button=Find(study,"Prova sul diagramma") as Button;button.PerformClick();}else study.Close();}
    else if(question!=null){asked=true;question.DialogResult=discard;question.Close();}
    if(++ticks>300){foreach(Form f in Application.OpenForms.Cast<Form>().Where(f=>f!=main).ToArray())f.Close();timer.Stop();}
   };timer.Start();main.GetType().GetMethod("ShowLegacyLearning",flags).Invoke(main,null);timer.Stop();
  }Check(seen,"Impara opens from actual main window");return asked;
 }
 static void StateTests(){
  var c=LearningContent.Current;Check(c.units.Length==6&&c.lessons.Length==24&&c.lessons.Sum(l=>l.questions.Length)==72,"embedded curriculum: 6 units / 24 lessons / 72 questions");
  Check(c.lessons.All(l=>l.sources.Length>0&&l.explanation.Length>50&&l.tip.Length>10&&l.counterexample.Length>10),"all lessons include explanation, trick, counterexample and sources");
  foreach(var e in c.examples){var d=c.Practice(e.title);Check(d.Nodes.Count>1&&d.Edges.Count>0,"practice JSON opens: "+e.title);}
  var state=new LearningState();state.Begin(c.lessons[0].id,false);state.Start();var q=state.Lesson.questions[0];
  state.Choose((q.correct+1)%q.options.Length);Check(!state.Correct&&!state.Advance()&&state.Question==0,"wrong answer cannot advance");
  Check(!state.Choose(q.correct),"answer cannot change before explicit retry");state.Retry();state.Choose(q.correct);Check(state.Correct&&state.Advance()&&state.FirstTry==0,"retry succeeds without counting first attempt");Complete(state);Check(state.Xp==30&&state.Completed.Count==1,"complete lesson earns 30 XP");
  state.Begin(c.lessons[0].id,true);Complete(state);Check(state.Xp==30,"repeating lesson never duplicates XP");
  string file=Path.Combine(dir,"progress-"+Guid.NewGuid().ToString("N")+".json");var store=new LearningStore(file);store.State=state;Check(store.Save(),"progress saves atomically");
  store.State.Begin(c.lessons[1].id,false);store.State.Start();store.State.Choose(0);store.State.Grid=false;Check(store.Save(),"quiz and grid preference save");
  var reopened=new LearningStore(file);Check(reopened.State.Active==c.lessons[1].id&&reopened.State.Question==0&&reopened.State.Answer==0&&!reopened.State.Grid,"restart restores quiz and grid preference");
  string legacy=Path.Combine(dir,"legacy-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(legacy,"{\"Version\":1,\"Grid\":false,\"Completed\":[\"l01\"],\"Active\":\"l02\",\"Question\":0,\"Answer\":-1}");var migrated=new LearningStore(legacy);Check(migrated.State.Version==2&&migrated.State.Xp==30&&!migrated.State.Grid&&migrated.State.Active=="l02","version 1 progress migrates without losing lessons, quiz or grid");
  store.State.Review.Add(c.lessons[1].questions[0].id);Check(store.Save()&&new LearningStore(file).State.Review.Count==1,"personalized review IDs persist to real storage");
  File.WriteAllText(file,"broken");var recovered=new LearningStore(file);Check(recovered.State.Xp==30&&recovered.Error!=null,"corrupt primary recovers last valid backup");Check(recovered.Save(),"recovered backup can safely replace corrupt primary");
  File.WriteAllText(file,"broken");File.WriteAllText(file+".bak","broken backup");var corrupt=new LearningStore(file);Check(!corrupt.Save()&&File.ReadAllText(file)=="broken","unreadable progress is preserved without overwrite");
  string newer="{\"Version\":3}";File.WriteAllText(file,newer);var future=new LearningStore(file);Check(!future.Save()&&File.ReadAllText(file)==newer,"progress from another version is never replaced by an older build");
  string locked=Path.Combine(dir,"blocked");File.WriteAllText(locked,"keep");var failed=new LearningStore(Path.Combine(locked,"p.json"));Check(!failed.Save()&&File.ReadAllText(locked)=="keep","save failure is reported and existing file survives");
 }
 static void NativeTests(){
  Ui.SetDarkMode(false,false);
  using(var main=new MainForm(null,true)){
   main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-2500,-2000);main.Show();Pump();
   Check(Field<Panel>(main,"homePanel").Visible&&!Field<Panel>(main,"commandHost").Visible,"fresh launch presents Impara and free mode");Capture(main,"home.png");
   var original=main.Document;Click(main,"Riprendi il foglio aperto");Check(!Field<Panel>(main,"homePanel").Visible&&ReferenceEquals(original,main.Document),"home enters editor without replacing work");
   var grid=Field<FlowButton>(main,"gridButton");bool before=main.Canvas.SnapToGrid;grid.PerformClick();Check(main.Canvas.SnapToGrid!=before&&Field<LearningStore>(main,"learningStore").State.Grid==!before,"visible grid button changes and saves actual snap state");grid.PerformClick();
   main.Size=new Size(1120,720);Pump();Capture(main,"editor-minimum.png");
   var command=Find(main,"Impara").Parent;var utilities=Find(main,"Esporta").Parent;Check(command.Right<=utilities.Left,"editor commands fit minimum width without overlap");
   main.Canvas.AddNode(NodeKind.Process);original=main.Document;
   Check(!OpenStudy(main,false,DialogResult.Cancel)&&ReferenceEquals(original,main.Document),"reading and closing Impara preserves dirty work without discard prompt");
   Check(OpenStudy(main,true,DialogResult.Cancel)&&ReferenceEquals(original,main.Document),"cancelling practice replacement preserves dirty work");
   Check(OpenStudy(main,true,DialogResult.No)&&!ReferenceEquals(original,main.Document)&&main.Document.Title=="Somma di due numeri","confirmed practice opens the real lesson example");
   original=main.Document;main.ShowHome();Check(ReferenceEquals(original,main.Document),"returning home preserves document");Capture(main,"home-minimum.png");main.SetDocument(DiagramIO.CreateBlank(),null,false);main.Close();
  }
  var store=new LearningStore(Path.Combine(dir,"native-progress-"+Guid.NewGuid().ToString("N")+".json"));
  using(var study=new LearningDialog(store)){
   study.ShowInTaskbar=false;study.StartPosition=FormStartPosition.Manual;study.Location=new Point(-2500,-2000);study.Show();Pump();
   Check(Find(study,"Continua")!=null&&Find(study,"Prova le 3 domande")==null,"native lesson starts with one focused idea");Capture(study,"lesson.png");Click(study,"Continua");Check(Find(study,"Un trucco da provare")!=null,"trick and counterexample form the next short step");Capture(study,"lesson-trick.png");Click(study,"Prova le 3 domande");
   var question=store.State.Lesson.questions[0];int wrong=(question.correct+1)%question.options.Length;Click(study,question.options[wrong]);
   Check(Find(study,question.feedback[wrong])!=null&&Find(study,"Riprova questa domanda")!=null,"native wrong answer shows specific explanation and retry");Capture(study,"quiz-wrong.png");
   Click(study,"Riprova questa domanda");
   while(!store.State.Finished){question=store.State.Lesson.questions[store.State.Question];Click(study,question.options[question.correct]);Click(study,store.State.Question==2?"Completa la lezione":"Prossima domanda");}
   Check(Find(study,"Lezione completata")!=null&&store.State.Xp==30,"native controls complete quiz and persist XP");Capture(study,"lesson-complete.png");
   Check(Find(study,"Allena le idee da ripassare (1)")!=null&&store.State.Review.Count==1,"mistake produces a real personalized review action");
   Click(study,"Allena le idee da ripassare (1)");Click(study,"Continua");Click(study,"Prova le 3 domande");
   question=store.State.Lesson.questions[0];Click(study,question.options[question.correct]);Check(store.State.Review.Count==0,"clean retrieval in a later review clears the remembered mistake");
   Click(study,"Prossima domanda");while(!store.State.Finished){question=store.State.Lesson.questions[store.State.Question];Click(study,question.options[question.correct]);Click(study,store.State.Question==2?"Completa la lezione":"Prossima domanda");}Check(store.State.Xp==30,"personalized review does not duplicate XP");
   Ui.SetDarkMode(true,false);study.Size=new Size(900,660);Capture(study,"learning-dark-minimum.png");var reader=Field<FlowScrollPanel>(study,"reader");Check(!reader.HorizontalScroll.Visible,"minimum learning window has no unnecessary horizontal scroll");Check(new LearningStore(store.PathName).State.Xp==30,"native quiz progress survives reload");study.Close();
  }
  Ui.SetDarkMode(false,false);
 }
 static void DragTests(){
  using(var form=new Form{ClientSize=new Size(800,600),ShowInTaskbar=false,Location=new Point(-2500,-2000),StartPosition=FormStartPosition.Manual})
  using(var canvas=new SnapProbe{Dock=DockStyle.Fill}){
   form.Controls.Add(canvas);form.Show();Pump();int changes=0;canvas.DiagramChanged+=delegate{changes++;};
   foreach(var mode in new[]{0,1,2}){
    var d=new Diagram();var a=new FlowNode{Id="a",Kind=NodeKind.Process,Text="a=1",X=5,Y=7};var b=new FlowNode{Id="b",Kind=NodeKind.Process,Text="b=2",X=308,Y=212};d.Nodes.Add(a);d.Nodes.Add(b);canvas.Diagram=d;canvas.ResetView();canvas.Zoom=1;canvas.SnapToGrid=mode!=1;canvas.Mods=mode==2?Keys.Alt:Keys.None;canvas.SelectNodes(new[]{"a","b"});changes=0;
    Point p=Point.Round(canvas.WorldToClient(new PointF(85,37))),q=new Point(p.X+32,p.Y+30);canvas.Mouse("down",p);canvas.Mouse("move",q);
    Check(Math.Abs(a.X-37)<.1&&Math.Abs(a.Y-37)<.1,"drag follows pointer continuously mode "+mode);canvas.Mouse("up",q);
    Check(mode==0?a.X==48&&a.Y==48:a.X==37&&a.Y==37,"release respects grid/free/Alt mode "+mode);
    Check(b.X-a.X==303&&b.Y-a.Y==205&&changes==1,"group distances and single history change preserved mode "+mode);
    float x=a.X,y=a.Y;p=Point.Round(canvas.WorldToClient(new PointF(a.X+80,a.Y+30)));canvas.Mouse("down",p);canvas.Mouse("up",p);Check(a.X==x&&a.Y==y,"tap never moves block mode "+mode);
   }form.Close();
  }
 }
 [STAThread]static int Main(string[] args){try{dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);Environment.SetEnvironmentVariable("FROCIART_DATA_DIR",Path.Combine(dir,"settings"));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);StateTests();NativeTests();DragTests();Console.WriteLine("CHECKS PASSED "+checks);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}}
