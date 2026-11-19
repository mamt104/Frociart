// Focused integration checks for editable branch labels/colors. Excluded from production.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Frociart.Tests
{
    internal static class BranchStyleTests
    {
        private static int checks;
        private static string folder;
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
        private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
        private static void Call(object target,string name){target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
        private static void Pump(){Application.DoEvents();System.Threading.Thread.Sleep(70);Application.DoEvents();}
        private static void Shot(Form form,string name){Pump();using(var b=new Bitmap(form.ClientSize.Width,form.ClientSize.Height))using(var g=Graphics.FromImage(b)){g.CopyFromScreen(form.PointToScreen(Point.Empty),Point.Empty,form.ClientSize);b.Save(Path.Combine(folder,name));}}
        private static void CheckTextFits(Control control,int horizontalInset,int verticalInset)
        {
            using(var g=control.CreateGraphics())
            {
                Size text=TextRenderer.MeasureText(g,control.Text,control.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
                Check(text.Width<=control.ClientSize.Width-horizontalInset&&text.Height<=control.ClientSize.Height-verticalInset,
                    "complete text fits: "+control.Text+" ("+text.Width+"x"+text.Height+" in "+(control.ClientSize.Width-horizontalInset)+"x"+(control.ClientSize.Height-verticalInset)+")");
            }
        }
        [STAThread] private static int Main(string[] args)
        {
            folder=args[0];Directory.CreateDirectory(folder);
            try
            {
#if NET5_0_OR_GREATER
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                foreach(bool dark in new[]{false,true})
                {
                    Ui.SetDarkMode(dark,false);
                    using(var main=new MainForm(null,true))
                    {
                        main.Show();Pump();main.EnterEditor();main.Canvas.SelectNode("check");Pump();
                        var control=Field<FlowButton>(main,"branchStyleButton");Check(control.Visible&&control.Enabled,"decision exposes label/color editor");
                        var node=main.Document.Nodes.Single(n=>n.Id=="check");string original=node.GetBranchLabel("true");
                        Exception failure=null;bool visited=false;
                        using(var timer=new Timer{Interval=80})
                        {
                            timer.Tick+=delegate
                            {
                                var dialog=Application.OpenForms.OfType<BranchStyleDialog>().FirstOrDefault();if(dialog==null)return;timer.Stop();
                                try
                                {
                                    visited=true;var yes=Field<TextBox>(dialog,"trueText");var no=Field<TextBox>(dialog,"falseText");
                                    Check(yes.MaxLength==60&&no.MaxLength==60,"labels have format-compatible length limits");
                                    Check(dialog.Controls.Cast<Control>().Where(c=>c.Visible).All(c=>dialog.ClientRectangle.Contains(c.Bounds)),"all branch dialog controls fit at native DPI");
                                    using(var g=dialog.CreateGraphics())
                                    {
                                        float scale=g.DpiX/96f;
                                        Check(Math.Abs(dialog.ClientSize.Width-510*scale)<=2&&Math.Abs(dialog.ClientSize.Height-430*scale)<=2,
                                            "dialog geometry follows native DPI: "+g.DpiX+", "+dialog.ClientSize);
                                    }
                                    CheckTextFits(dialog.Controls.OfType<Label>().Single(c=>c.Text=="Dai un nome ai due percorsi"),0,0);
                                    foreach(var colorButton in dialog.Controls.OfType<FlowButton>().Where(b=>b.AccessibleName!=null&&b.AccessibleName.StartsWith("Uscita ")))
                                        CheckTextFits(colorButton,10,4);
                                    Check(!dark||yes.BackColor.GetBrightness()<.5f&&no.BackColor.GetBrightness()<.5f,"dark branch editor has themed input backgrounds");
                                    yes.Text="Pari & zero";no.Text="Dispari";
                                    dialog.Controls.OfType<FlowButton>().Single(b=>b.AccessibleName=="Uscita vera: Blu").PerformClick();
                                    dialog.Controls.OfType<FlowButton>().Single(b=>b.AccessibleName=="Uscita falsa: Viola").PerformClick();
                                    Check(dialog.TrueColor=="#5B8DEF"&&dialog.FalseColor=="#9771DC","palette assigns independent branch colors");
                                    Shot(dialog,dark?"branch-dialog-dark.png":"branch-dialog-light.png");
                                    dialog.Controls.OfType<FlowButton>().Single(b=>b.Text=="Applica").PerformClick();
                                }
                                catch(Exception e){failure=e;dialog.DialogResult=DialogResult.Cancel;dialog.Close();}
                            };
                            timer.Start();Call(main,"EditBranchStyles");
                        }
                        if(failure!=null)throw failure;Check(visited,"actual modal editor opened");
                        Check(node.TrueLabel=="Pari & zero"&&node.FalseLabel=="Dispari","Apply updates both display labels");
                        Check(node.TrueColor=="#5B8DEF"&&node.FalseColor=="#9771DC","Apply updates both display colors");
                        Check(Field<FlowButton>(main,"yesButton").Text.Contains("Pari & zero"),"inspector shows custom true label");
                        Check(!Field<FlowButton>(main,"yesButton").UseMnemonic&&!Field<FlowButton>(main,"noButton").UseMnemonic,"custom labels preserve literal ampersands");
                        Check(Field<FlowButton>(main,"noButton").Text.Contains("Dispari"),"inspector shows custom false label");
                        Call(main,"Undo");Check(main.Document.Nodes.Single(n=>n.Id=="check").GetBranchLabel("true")==original,"one Undo restores original labels");
                        Call(main,"Redo");Check(main.Document.Nodes.Single(n=>n.Id=="check").TrueLabel=="Pari & zero","one Redo restores custom labels");
                        Shot(main,dark?"branches-main-dark.png":"branches-main-light.png");
                        main.Canvas.SelectNode("start");Pump();Check(!control.Visible,"non-decision blocks hide branch editor");
                        main.SetDocument(Examples.Parity(),null,false);main.Close();
                    }
                }
                Console.WriteLine("BRANCH UI TESTS PASSED: "+checks);return 0;
            }
            catch(Exception e){Console.Error.WriteLine(e);return 1;}
            finally{Ui.SetDarkMode(false,false);}
        }
    }
}
