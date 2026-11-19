using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
namespace Frociart.Tests {
internal static class DesktopUxTests {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool good,string message){if(!good)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var x in All(child))yield return x;}
    static void Pump(){for(int i=0;i<5;i++)Application.DoEvents();}
    static void Capture(Form f,string file){Pump();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,f.Size));bmp.Save(file);}}
    [STAThread] static int Main(string[] args){try{
        Application.EnableVisualStyles();Ui.SetDarkMode(false,false);TutorFeedbackSignals.SetEnabled(false);
        var directory=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"desktop-ux");Directory.CreateDirectory(directory);
        using(var form=new TutorDialog(new TutorStore(Path.Combine(directory,"state-"+Guid.NewGuid().ToString("N"))),new LearningStore(Path.Combine(directory,"legacy.json")))){
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2500,-2200);form.Show();Pump();
            foreach(var c in All(form))Console.WriteLine(c.GetType().Name+" "+c.Bounds+" visible="+c.Visible+" text="+c.Text.Replace('\n',' '));
            Capture(form,Path.Combine(directory,"home.png"));
            var tiles=All(form).OfType<TutorUnitButton>().ToArray();Check(tiles.Length==6&&tiles.All(x=>x.Visible&&x.Width>350&&x.Height>=95&&x.Parent.ClientSize.Height>=x.Bottom),"six readable unit targets are not clipped by their parent");
            tiles[0].PerformClick();Pump();Capture(form,Path.Combine(directory,"unit.png"));
            var lessons=All(form).OfType<TutorUnitButton>().ToArray();Check(lessons.Length==4&&lessons.All(x=>x.Width>700),"unit opens four readable lesson rows");
            Check(lessons.OrderBy(x=>x.Top).Zip(lessons.OrderBy(x=>x.Top).Skip(1),(a,b)=>a.Bottom<=b.Top).All(x=>x),"lesson targets do not overlap");
            lessons[0].PerformClick();Pump();Check(form.Current.Id=="b01","native lesson row opens correct lesson");
            ((FlowButton)form.GetType().GetField("primary",flags).GetValue(form)).PerformClick();Pump();Check(form.Canvas.Width>=800,"workspace gives the chart the desktop width");
            Check(!All(form).OfType<TreeView>().Any(),"workspace has no permanent lesson tree");
            var tabs=(TabControl)form.GetType().GetField("inspectorTabs",flags).GetValue(form);var hit=tabs.GetTabRect(1);var position=new IntPtr(((hit.Top+hit.Height/2)<<16)|(hit.Left+hit.Width/2));SendMessage(tabs.Handle,0x201,new IntPtr(1),position);SendMessage(tabs.Handle,0x202,IntPtr.Zero,position);Pump();Check(tabs.SelectedIndex==1,"painted tabs retain native mouse navigation");
            Check(!((FlowButton)form.GetType().GetField("primary",flags).GetValue(form)).Visible,"Prova hides lesson verification");
            Check(!All(form).OfType<Button>().Any(x=>x.Visible&&(x.Text=="Aggiungi"||x.Text=="Collega"||x.Text=="Riordina"||x.Text=="Annulla")),"Prova hides editing tools");
            Check(All(form).OfType<Button>().Any(x=>x.Visible&&x.Text=="Inquadra")&&form.Canvas.Enabled,"trial retains chart framing and zoom");
            Check(!((ListView)form.GetType().GetField("runVariables",flags).GetValue(form)).Visible,"variables are disclosed only on request");
            Capture(form,Path.Combine(directory,"trial-simple.png"));
            All(form).OfType<Button>().First(x=>x.Visible&&x.Text=="Dettagli").PerformClick();Pump();
            Check(((ListView)form.GetType().GetField("runVariables",flags).GetValue(form)).Visible,"trial details reveal real variables");
            tabs.Focus();SendMessage(tabs.Handle,0x100,new IntPtr((int)Keys.Left),IntPtr.Zero);SendMessage(tabs.Handle,0x101,new IntPtr((int)Keys.Left),IntPtr.Zero);Pump();Check(tabs.SelectedIndex==0,"painted tabs retain native keyboard navigation");
            Check(((FlowButton)form.GetType().GetField("primary",flags).GetValue(form)).Visible&&All(form).OfType<Button>().Any(x=>x.Visible&&x.Text=="Riordina"),"return to Scrivi restores check and editing commands");
            Check(!All(form).OfType<TextBox>().Any(t=>t.Name=="TutorExpression"),"Start does not expose a distracting code field");
            form.Size=new Size(1050,720);Pump();Check(form.Canvas.Width>=600&&form.Canvas.Height>=350,"minimum-size canvas remains usable");
            Capture(form,Path.Combine(directory,"minimum.png"));Ui.SetDarkMode(true,false);Pump();Capture(form,Path.Combine(directory,"dark-minimum.png"));Ui.SetDarkMode(false,false);form.Close();
        }
        byte[] ok=TutorFeedbackSignals.Wave(true,false),bad=TutorFeedbackSignals.Wave(false,false),done=TutorFeedbackSignals.Wave(true,true);
        Check(System.Text.Encoding.ASCII.GetString(ok,0,4)=="RIFF"&&BitConverter.ToInt32(ok,4)==ok.Length-8,"generated feedback has a valid WAV container");
        Check(!ok.SequenceEqual(bad)&&done.Length>ok.Length,"success, correction and completion have distinct audio");
        Check(TutorFeedbackSignals.SetEnabled(false)&&!TutorFeedbackSignals.Enabled,"sound preference can be disabled and reloaded");
        Console.WriteLine("PASS DESKTOP UX "+checks+" checks; real native controls rendered. Audio perception and external human usability: NOT_RUN.");return 0;
    }catch(Exception e){Console.WriteLine(e);return 1;}}
}}
