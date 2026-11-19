// Focused appearance/state regression harness. Exclude from production builds.
// Build with the application's sources and StartupObject=Frociart.Tests.ThemeTests.
// Arguments: output-directory [expected-dpi] [visual-only|footer-picker]
// .NET 10 Windows/Wine is used for the final appearance checks.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Frociart.Tests
{
    internal static class ThemeTests
    {
        private static int checks;
        private static string folder;
        private static float dpiScale = 1;

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAIL: " + message); checks++; Console.WriteLine("PASS: " + message); }
        private static T Field<T>(object instance, string name)
        { return (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance); }
        private static void Call(object instance, string name)
        { instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, null); }
        private static void Key(Control control,Keys key)
        {control.GetType().GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(control,new object[]{new KeyEventArgs(key)});}
        private static void Pump()
        { Application.DoEvents(); System.Threading.Thread.Sleep(90); Application.DoEvents(); }
        private static IEnumerable<Control> Walk(Control parent)
        { yield return parent; foreach (Control child in parent.Controls) foreach (Control nested in Walk(child)) yield return nested; }
        private static Control Find(Control parent, string text)
        { return Walk(parent).FirstOrDefault(c => c.Text == text); }
        private static string Snapshot(Diagram diagram)
        {
            using (MemoryStream stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(Diagram)).WriteObject(stream, diagram); return Convert.ToBase64String(stream.ToArray()); }
        }
        private static void Shot(Form form, string name)
        {
            Pump();
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(folder, name)); }
        }
        private static void ResizeLogical(Form form, int width, int height)
        { form.Size = new Size((int)Math.Round(width * dpiScale), (int)Math.Round(height * dpiScale)); Pump(); }
        private static double Brightness(Color color)
        { return color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722; }
        private static bool IsInside(Control child)
        {
            if (!child.Visible || child.Width <= 0 || child.Height <= 0) return false;
            Rectangle visible = child.RectangleToScreen(child.ClientRectangle);
            for (Control ancestor = child.Parent; ancestor != null; ancestor = ancestor.Parent)
                visible = Rectangle.Intersect(visible, ancestor.RectangleToScreen(ancestor.ClientRectangle));
            return visible.Width >= child.Width - 2 && visible.Height >= child.Height - 2;
        }
        private static void CheckTextInputs(Control parent, string scope)
        {
            TextBoxBase[] inputs = Walk(parent).OfType<TextBoxBase>().Where(c => c.Visible).ToArray();
            Check(inputs.All(c => Math.Abs(Brightness(c.ForeColor) - Brightness(c.BackColor)) >= 65), scope + " has distinct input text and background colors");
            if (Ui.IsDark) Check(inputs.All(c => Brightness(c.BackColor) < 110), scope + " has no white input fields in dark mode");
        }

        [STAThread]
        private static int Main(string[] args)
        {
            folder = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "frociart-theme-qa");
            Directory.CreateDirectory(folder);
            float expectedDpi = args.Length > 1 ? Single.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 96;
            bool visualOnly = args.Length > 2 && args[2] == "visual-only";
            bool focused = args.Length > 2 && args[2] == "footer-picker";
            try
            {
#if NET5_0_OR_GREATER
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                
                string preference = Ui.ThemePreferencePath;
                byte[] previousPreference = File.Exists(preference) ? File.ReadAllBytes(preference) : null;
                Ui.SetDarkMode(false, false);
                using (MainForm main = new MainForm(null, true))
                {
                    // Pixel probes need the full 1420px fixture on one monitor.
                    main.StartPosition=FormStartPosition.Manual;
                    main.Location=Screen.AllScreens.OrderByDescending(s=>s.WorkingArea.Width).First().WorkingArea.Location;
                    // A hidden test launch can override the first ShowWindow request.
                    // Show again so screen-pixel checks sample the actual probe.
                    main.Show(); main.Hide(); main.Show(); main.Activate(); Pump();
                    using (Graphics graphics = main.CreateGraphics())
                    { dpiScale = graphics.DpiX / 96f; Check(Math.Abs(graphics.DpiX - expectedDpi) <= 1, "native display DPI is " + expectedDpi); }
                    ResizeLogical(main, 1420, 900); main.Canvas.FitToView(); main.Canvas.SelectNode("check"); Pump();
                    if(focused)
                    {
                        Ui.SetDarkMode(true,false);Pump();CheckExamples(main);CheckPicker(main);
                        main.SetDocument(Examples.Parity(),null,false);main.Close();
                    }
                    else
                    {
                    Check(!Ui.IsDark && Brightness(Ui.Surface) > 200 && Brightness(Ui.Ink) < 100, "light palette is active");
                    Control fileBar = Walk(main).First(c=>c.Name=="TaskProgetto").Parent;
                    string[] fileNames = fileBar.Controls.Cast<Control>().Select(c => c.Text).ToArray();
                    Rectangle[] fileBounds = fileBar.Controls.Cast<Control>().Select(c => c.Bounds).ToArray();
                    Check(fileBar.Controls.Cast<Control>().Where(c=>c.Name.StartsWith("Task",StringComparison.Ordinal)).Select(c=>c.Name).SequenceEqual(new[]{"TaskProgetto","TaskModifica","TaskEsegui","TaskCodice","TaskVista","TaskAiuto"}), "six activity menus retain a stable, predictable order");
                    Check(fileBar.Controls.Cast<Control>().Any(c=>c.Name=="QuickCommands"&&c.AccessibleName=="Cerca un comando"), "command search remains separate from the six task categories");
                    Shot(main, "01-light-normal.png");

                    if (!visualOnly) CheckStateAndExports(main);
                    Ui.SetDarkMode(true, false); Pump();
                    Check(Ui.IsDark && Brightness(Ui.Surface) < 100 && Brightness(Ui.Ink) > 180, "dark palette is active");
                    Check(fileBar.Controls.Cast<Control>().Select(c => c.Text).SequenceEqual(fileNames) &&
                        fileBar.Controls.Cast<Control>().Select(c => c.Bounds).SequenceEqual(fileBounds), "theme preserves the six file commands' names, order and bounds");
                    CheckTextInputs(main, "main window"); Shot(main, "02-dark-normal.png");
                    CheckShellChrome(main);
                    ResizeLogical(main, 1120, 720); main.Canvas.FitToView(); Pump();
                    Shot(main, "03-dark-minimum.png");
                    Console.WriteLine("Minimum canvas: " + main.Canvas.ClientSize + "; scale: " + dpiScale);
                    Check(fileBar.Controls.Cast<Control>().All(IsInside), "all six file commands are reachable at minimum size");
                    Check(main.Canvas.Width >= 450 * dpiScale && main.Canvas.Height >= 350 * dpiScale, "minimum size retains a usable diagram surface");
                    Check(IsInside(main.Canvas)&&IsInside(Find(main,"Inquadra")),"the full canvas and zoom actions stay inside the minimum window");
                    FlowButton[] actions = Walk(main).OfType<FlowButton>().Where(b => b.Visible &&
                        (b.Text == "Aggiorna blocco" || b.Text == "Applica" || b.Text == "Duplica" || b.Text == "Elimina")).ToArray();
                    Check(actions.Any(b => b.Text == "Aggiorna blocco" || b.Text == "Applica") &&
                        actions.Where(b => b.Text == "Aggiorna blocco" || b.Text == "Applica").All(IsInside), "the inspector apply action is fully reachable at minimum size");
                    Shot(main, "03-dark-minimum.png");
                    Ui.SetDarkMode(false, false); Pump(); Shot(main, "04-light-minimum.png");

                    ResizeLogical(main, 1420, 900); Ui.SetDarkMode(true, false); Pump();
                    CheckDialogs(main, !visualOnly);
                    CheckNativeControls(main);
                    CheckConfirmation(main);
                    CheckValidationAppearance(main);
                    CheckPicker(main);
                    Ui.SetDarkMode(false, false); Pump();
                    Check(!Ui.IsDark && Brightness(main.BackColor) > 200, "roundtrip returns the main form to light mode");
                    main.SetDocument(Examples.Parity(), null, false); main.Close();
                    }
                }
                byte[] afterPreference = File.Exists(preference) ? File.ReadAllBytes(preference) : null;
                Check(previousPreference == null ? afterPreference == null : afterPreference != null && previousPreference.SequenceEqual(afterPreference),
                    "tests leave the saved appearance preference unchanged");
                Console.WriteLine("THEME TESTS PASSED: " + checks + "; DPI " + expectedDpi + "; artifacts: " + folder); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { Ui.SetDarkMode(false, false); }
        }

        private static void CheckStateAndExports(MainForm main)
        {
            string snapshot = Snapshot(main.Document);
            string[] selected = main.Canvas.SelectedNodeIds.ToArray(); float zoom = main.Canvas.Zoom;
            PointF world = main.Canvas.ClientToWorld(new Point(70, 85));
            TextBox expression = Field<TextBox>(main, "expression"); string originalExpression = expression.Text;
            expression.Text = "numero >= 2";
            Ui.SetDarkMode(true, false); Pump();
            Check(expression.Text == "numero >= 2", "theme switch preserves an uncommitted inspector edit");
            expression.Text = originalExpression;
            Ui.SetDarkMode(false, false); Pump();
            Check(Snapshot(main.Document) == snapshot && main.Canvas.SelectedNodeIds.SequenceEqual(selected) &&
                main.Canvas.Zoom == zoom && main.Canvas.ClientToWorld(new Point(70, 85)) == world, "theme roundtrip preserves diagram, selection, zoom and pan");

            main.SingleStep(); main.SingleStep();
            Check(main.Runner.State == RuntimeState.WaitingForInput, "runtime waits for a value before the theme check");
            TextBox input = Field<TextBox>(main, "inputValue"); input.Text = "17"; int steps = main.Runner.Steps;
            Ui.SetDarkMode(true, false); Pump();
            Check(input.Text == "17" && main.Runner.Steps == steps && main.Runner.State == RuntimeState.WaitingForInput,
                "theme switch preserves pending runtime input and execution position");
            main.SetDocument(Examples.Parity(), null, false); main.Canvas.SelectNode("check");
            main.Canvas.CurrentNodeId = "odd"; main.Canvas.IssueNodeIds = new HashSet<string> { "check" };
            Ui.SetDarkMode(false, false); Pump();
            SaveExports(main.Canvas, "light");
            Ui.SetDarkMode(true, false); Pump(); SaveExports(main.Canvas, "dark");
            foreach (string ext in new[] { "pdf", "svg", "png" })
                Check(File.ReadAllBytes(Path.Combine(folder, "light." + ext)).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "dark." + ext))),
                    ext.ToUpperInvariant() + " remains byte-identical and light in both application themes");
            using (Bitmap image = new Bitmap(Path.Combine(folder, "dark.png")))
                Check(image.GetPixel(0, 0).ToArgb() == Color.White.ToArgb(), "exported PNG keeps a white paper background");
            main.Canvas.CurrentNodeId = null; main.Canvas.IssueNodeIds = new HashSet<string>();
        }

        private static void SaveExports(FlowCanvas canvas, string prefix)
        {
            // Prime native GDI glyph/shape caches before comparing export bytes.
            // Cold/warm rasterization differed on ten antialiased title pixels;
            // SVG geometry and content were identical. Keep the exact assertions.
            using(var warmup=canvas.RenderExportBitmap(2f)){}
            DiagramExport.ExportPdf(canvas, Path.Combine(folder, prefix + ".pdf"));
            DiagramExport.ExportSvg(canvas, Path.Combine(folder, prefix + ".svg"));
            DiagramExport.ExportPng(canvas, Path.Combine(folder, prefix + ".png"));
        }

        private static void CheckDialogs(MainForm main, bool testRoundtrip)
        {
            CheckExamples(main);
            using (TestDialog dialog = new TestDialog(Examples.Parity()))
            {
                dialog.Show(main); Pump(); CheckTextInputs(dialog, "test-data dialog");
                if (testRoundtrip)
                {
                    Call(dialog, "Start"); for (int i = 0; i < 12 && Field<TestSession>(dialog, "session").Result == null; i++) Pump();
                    Check(Field<TestSession>(dialog, "session").Result.Passed, "data test remains usable in dark mode");
                    string text = Field<TextBox>(dialog, "expected").Text;
                    Ui.SetDarkMode(false, false); Ui.SetDarkMode(true, false); Pump();
                    Check(Field<TextBox>(dialog, "expected").Text == text && Field<Label>(dialog, "resultTitle").ForeColor == Ui.Green,
                        "test result and semantic success color survive a theme roundtrip");
                }
                dialog.Size = dialog.MinimumSize; Pump();
                Check(IsInside(Field<FlowButton>(dialog, "start")), "data-test action stays reachable at minimum size");
                Shot(dialog, "06-dark-test-data.png"); dialog.Close();
            }
            using (ExportDialog dialog = new ExportDialog(main.Canvas))
            {
                dialog.Show(main); Pump();
                RadioButton radio = Field<RadioButton>(dialog, "pdf");
                Check(Brightness(radio.Parent.BackColor) < 110, "export format cards open with dark surfaces");
                dialog.Size = dialog.MinimumSize; Pump();
                Check(IsInside(Field<FlowButton>(dialog, "export")), "export action stays reachable at minimum size");
                Shot(dialog, "07-dark-export.png"); dialog.Close();
            }
            using (GuideDialog dialog = new GuideDialog())
            {
                dialog.Show(main); Pump(); CheckTextInputs(dialog, "guide dialog");
                RichTextBox guide=Field<RichTextBox>(dialog,"text");
                int paragraph=guide.Text.IndexOf("Frociart è",StringComparison.Ordinal);
                if(paragraph<0)paragraph=guide.Text.IndexOf('\n')+1;
                guide.Select(paragraph,1);
                Check(guide.SelectionColor==Ui.Ink,"guide paragraph text is readable in dark mode");
                Ui.SetDarkMode(false,false);Ui.SetDarkMode(true,false);Pump();
                Check(guide.SelectionColor==Ui.Ink,"guide paragraph formatting follows a live theme roundtrip");
                guide.Select(0,0);guide.ScrollToCaret();
                dialog.Size = dialog.MinimumSize; Pump(); Shot(dialog, "08-dark-guide.png"); dialog.Close();
            }
        }

        private static void CheckExamples(MainForm main)
        {
            using(ExamplesDialog dialog=new ExamplesDialog())
            {
                dialog.Show(main);Pump();
                Check(Brightness(dialog.BackColor)<110&&Brightness(Field<ListBox>(dialog,"list").BackColor)<110,
                    "examples dialog opens with dark surfaces");
                dialog.Size=dialog.MinimumSize;Pump();
                Check(IsInside(Find(dialog,"Apri esempio"))&&IsInside(Find(dialog,"Annulla")),
                    "example open and cancel actions stay inside the minimum dialog");
                Shot(dialog,"05-dark-examples.png");
                ((FlowButton)Find(dialog,"Apri esempio")).PerformClick();Pump();
                Check(dialog.Selected!=null&&dialog.Selected.Title=="Pari o dispari","the example open action returns the selected diagram");
            }
        }

        private static void CheckShellChrome(MainForm main)
        {
            Check(!Walk(main).OfType<TabControl>().Any(),"sidebar contains no native tab frame");
            SidebarTabs tabs=Field<SidebarTabs>(main,"tabs");
            Control segments=Field<Control>(tabs,"segments");
            tabs.SelectedIndex=0;segments.Focus();Key(segments,Keys.Right);Pump();
            Check(tabs.SelectedIndex==1,"sidebar panels can be changed with the right-arrow key");
            Key(segments,Keys.End);Key(segments,Keys.Home);Pump();
            Check(tabs.SelectedIndex==0&&segments.AccessibilityObject.GetChildCount()==3&&
                segments.AccessibilityObject.GetChild(0).Role==AccessibleRole.PageTab,"sidebar preserves keyboard Home/End and accessible page tabs");
            ContextCard card=Field<ContextCard>(main,"contextPanel");
            Label detail=Field<Label>(main,"contextDetail");
            Point point=new Point(Math.Max(2,detail.Width-3),Math.Max(2,detail.Height-2));
            Check(ScreenPixel(detail.PointToScreen(point)).ToArgb()==card.BackColor.ToArgb(),"context card and child-label backgrounds form one continuous fill");
            main.Canvas.BeginConnect("check","true");Pump();
            Check(detail.BackColor==card.BackColor&&ScreenPixel(detail.PointToScreen(point)).ToArgb()==card.BackColor.ToArgb(),"connection hint keeps a continuous highlighted background");
            main.Canvas.CancelConnection();main.Canvas.Focus();Pump();
            CheckFrame(Field<TextBox>(main,"expression"),Ui.Line,"the unfocused inspector field has the same dark border on all four sides");
        }

        private static void CheckFrame(Control control,Color expected,string message)
        {
            Point origin=control.Parent.PointToScreen(control.Location);
            Point[] points={new Point(origin.X,origin.Y+control.Height/2),
                new Point(origin.X+control.Width-1,origin.Y+control.Height/2),
                new Point(origin.X+control.Width/2,origin.Y),
                new Point(origin.X+control.Width/2,origin.Y+control.Height-1)};
            Check(points.All(p=>ScreenPixel(p).ToArgb()==expected.ToArgb()),message);
        }

        private static void CheckValidationAppearance(MainForm main)
        {
            main.SetDocument(Examples.MissingBranch(),null,false);main.Verify(true);main.Canvas.FitToView();Pump();
            ValidationIssuesView list=Field<Panel>(main,"issuePanel") as ValidationIssuesView;
            Check(list!=null&&list.IssueCount>0&&list.Controls.Count==0&&Brightness(list.BackColor)<110,
                "verification uses the dark virtual issue list without separate native row controls");
            Shot(main,"13-dark-verification.png");
            ResizeLogical(main,1120,720);main.Canvas.FitToView();Pump();
            Check(IsInside(Find(main,"Ricontrolla")),"the verification action remains visible at minimum size");
            Shot(main,"14-dark-verification-minimum.png");
            ResizeLogical(main,1420,900);main.SetDocument(Examples.Parity(),null,false);main.Canvas.SelectNode("check");Pump();
        }

        private static void CheckConfirmation(MainForm main)
        {
            Exception problem=null;bool clicked=false;
            using(Timer timer=new Timer{Interval=120})
            {
                timer.Tick+=delegate{
                    Form dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f is FlowMessageDialog);
                    if(dialog==null)return;timer.Stop();
                    try
                    {
                        Check(Brightness(dialog.BackColor)<110,"application confirmation opens in the dark palette");
                        FlowButton cancel=(FlowButton)Find(dialog,"Annulla");
                        Check(Walk(dialog).OfType<FlowButton>().All(IsInside),"all confirmation choices are visible at the current DPI");
                        Check(dialog.ActiveControl is FlowButton,"confirmation gives keyboard focus to an action");
                        Shot(dialog,"12-dark-confirmation.png");cancel.PerformClick();clicked=true;
                    }
                    catch(Exception ex){problem=ex;dialog.Close();}
                };
                timer.Start();
                DialogResult result=FlowMessageDialog.Show(main,"Vuoi salvare le modifiche al diagramma? Puoi anche continuare senza modificarlo.","Modifiche da salvare",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(problem!=null)throw problem;
                Check(clicked&&result==DialogResult.Cancel,"confirmation returns the clicked Cancel choice without side effects");
            }
        }

        private static void CheckNativeControls(MainForm main)
        {
            using(Form probe=new Form())
            {
                probe.SuspendLayout();probe.AutoScaleDimensions=new SizeF(96,96);probe.AutoScaleMode=AutoScaleMode.Dpi;
                probe.Font=Ui.Font();probe.ClientSize=new Size(960,585);probe.StartPosition=FormStartPosition.CenterParent;
                probe.Text="Campi e scorrimento — controllo visivo";
                TextBox edit=Ui.TextBox(true);edit.SetBounds(22,48,285,205);
                edit.Text=String.Join("\r\n",Enumerable.Range(1,250).Select(i=>"Riga "+i+" · totale = totale + 1"));
                var rich=new FlowRichTextBox{Font=Ui.Font(),ReadOnly=true,ScrollBars=RichTextBoxScrollBars.ForcedVertical,Text=String.Join("\n",Enumerable.Range(1,250).Select(i=>"Paragrafo "+i+" della guida."))};rich.SetBounds(328,48,285,205);
                var list=new FlowListView{View=View.Details,Font=Ui.Font()};list.SetBounds(634,48,303,205);list.Columns.Add("Nome",130);list.Columns.Add("Valore",140);
                for(int i=0;i<100;i++)list.Items.Add(new ListViewItem(new[]{"variabile"+i,i.ToString()}));
                var scroll=new FlowScrollPanel{AutoScroll=true,AutoScrollMinSize=new Size(0,1250)};scroll.SetBounds(22,300,285,240);
                for(int i=0;i<20;i++){Label row=Ui.Label("Blocco "+(i+1),10);row.SetBounds(10,i*60,235,32);scroll.Controls.Add(row);}
                var combo=new FlowComboBox{Font=Ui.Font()};combo.SetBounds(328,314,285,34);for(int i=0;i<24;i++)combo.Items.Add("Opzione "+(i+1));combo.SelectedIndex=3;
                var check=new FlowCheckBox{Text="Dividi in pagine A4",Font=Ui.Font(),Checked=true};check.SetBounds(328,372,270,36);
                var radio=new FlowRadioButton{Text="PDF · pronto da stampare",Font=Ui.Font(),Checked=true};radio.SetBounds(328,428,290,38);
                Control[] controls={edit,rich,list,scroll,combo,check,radio};probe.Controls.AddRange(controls);
                string[] names={"Espressione modificabile","Guida con testo lungo","Variabili","Pannello lungo"};int[] lefts={22,328,634,22};int[] tops={17,17,17,269};
                for(int i=0;i<names.Length;i++){Label label=Ui.Label(names[i],10,null,true);label.SetBounds(lefts[i],tops[i],280,26);probe.Controls.Add(label);}
                Ui.ApplyTheme(probe);probe.ResumeLayout(true);probe.Show(main);Pump();
                edit.Select(0,0);edit.ScrollToCaret();
                rich.SelectAll();rich.SelectionColor=Ui.Ink;rich.Select(0,0);
                Check(controls.All(c=>c.IsHandleCreated),"native-themed controls retain working Windows handles");
                Check(edit is TextBox&&combo is ComboBox&&list is ListView,"themed inputs retain their native editing and accessibility bases");
                Shot(probe,"11-dark-native-controls.png");
                CheckFrame(edit,Ui.Blue,"the focused expression field has a complete blue border including the scrollbar edge");
                foreach(Control control in new Control[]{edit,rich,list,scroll})
                {
                    NativeBar bar=ReadBar(control);
                    Point sample=new Point(bar.Rectangle.Right-2,bar.Rectangle.Top+bar.LineButton+4);
                    Check(Brightness(ScreenPixel(sample))<120,control.GetType().Name+" has a dark scrollbar surface");
                    int previous=ReadScrollPosition(control);
                    Console.WriteLine(control.GetType().Name+" scrollbar data: line="+bar.LineButton+", thumb="+bar.ThumbTop+".."+bar.ThumbBottom+", position="+previous);
                    DragScrollbar(control,bar);Pump();
                    Check(ReadScrollPosition(control)>previous,control.GetType().Name+" scrolls when its thumb is dragged");
                    Check(!control.Capture,control.GetType().Name+" releases pointer capture after scrolling");
                }
                edit.Select(7,12);string before=edit.Text;int selection=edit.SelectionStart,length=edit.SelectionLength;
                Ui.SetDarkMode(false,false);Ui.SetDarkMode(true,false);Pump();
                Check(edit.Text==before&&edit.SelectionStart==selection&&edit.SelectionLength==length,"native input theme roundtrip preserves text and selection without recreating it");
                check.Focus();NativeTheme.SendMessage(check.Handle,0x0100,new IntPtr((int)Keys.Space),IntPtr.Zero);NativeTheme.SendMessage(check.Handle,0x0101,new IntPtr((int)Keys.Space),IntPtr.Zero);Pump();
                Check(!check.Checked,"themed checkbox retains Space-key activation");
                combo.Focus();combo.DroppedDown=true;Pump();
                NativeTheme.ComboInfo comboInfo=new NativeTheme.ComboInfo{Size=Marshal.SizeOf(typeof(NativeTheme.ComboInfo))};
                Check(NativeTheme.GetComboBoxInfo(combo.Handle,ref comboInfo)&&comboInfo.List!=IntPtr.Zero,"themed combobox keeps its native accessible dropdown");
                Shot(probe,"11-dark-native-dropdown.png");combo.DroppedDown=false;Pump();
                Check(combo.SelectedIndex==3,"opening a themed dropdown preserves the selected value");
                probe.Close();
            }
            main.Activate();main.Canvas.Focus();Pump();
        }

        // Sample only this test's live native control. Other desktop windows must not
        // turn an occluded screen pixel into a false theme failure or enter a capture.
        private static Color ScreenPixel(Point screen)
        {
            Control target=Application.OpenForms.Cast<Form>().LastOrDefault(f=>f.Visible&&f.Bounds.Contains(screen));
            if(target==null)throw new Exception("Pixel does not belong to a test form");
            Point origin=target.Location;
            bool descending=true;
            while(descending){descending=false;foreach(Control child in target.Controls){if(!child.Visible)continue;Point childOrigin=target.PointToScreen(child.Location);if(new Rectangle(childOrigin,child.Size).Contains(screen)){target=child;origin=childOrigin;descending=true;break;}}}
            using(var bitmap=new Bitmap(target.Width,target.Height)){target.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));return bitmap.GetPixel(screen.X-origin.X,screen.Y-origin.Y);}
        }
        private static NativeBar ReadBar(Control control)
        {
            NativeBar bar=new NativeBar{Size=Marshal.SizeOf(typeof(NativeBar)),States=new int[6]};
            if(!GetScrollBarInfo(control.Handle,-5,ref bar)||(bar.States[0]&0x18000)!=0)throw new Exception("Expected visible scrollbar: "+control.GetType().Name);
            return bar;
        }
        private static int ReadScrollPosition(Control control)
        {
            ScrollData data=new ScrollData{Size=Marshal.SizeOf(typeof(ScrollData)),Mask=0x17};
            if(!GetScrollInfo(control.Handle,1,ref data))throw new Exception("Cannot read scrollbar position");return data.Position;
        }
        private static void DragScrollbar(Control control,NativeBar bar)
        {
            ScrollData info=new ScrollData{Size=Marshal.SizeOf(typeof(ScrollData)),Mask=0x17};GetScrollInfo(control.Handle,1,ref info);
            int arrow=bar.Rectangle.Right-bar.Rectangle.Left;
            int track=bar.Rectangle.Bottom-bar.Rectangle.Top-2*arrow;
            int maximum=Math.Max(info.Minimum,info.Maximum-(int)Math.Max(1u,info.Page)+1);
            int thumb=Math.Max(Math.Min(12,track),Math.Min(track,(int)Math.Round(track*Math.Min((double)(info.Maximum-info.Minimum+1),info.Page)/(info.Maximum-info.Minimum+1))));
            int offset=maximum<=info.Minimum?0:(int)Math.Round((track-thumb)*(double)(info.Position-info.Minimum)/(maximum-info.Minimum));
            Point start=new Point((bar.Rectangle.Left+bar.Rectangle.Right)/2,bar.Rectangle.Top+arrow+offset+thumb/2);
            Point end=new Point(start.X,bar.Rectangle.Bottom-arrow-2);
            NativeTheme.SendMessage(control.Handle,0x0084,IntPtr.Zero,Pack(start));
            NativeTheme.SendMessage(control.Handle,0x0201,new IntPtr(1),Pack(control.PointToClient(start)));
            NativeTheme.SendMessage(control.Handle,0x0200,new IntPtr(1),Pack(control.PointToClient(end)));
            NativeTheme.SendMessage(control.Handle,0x0202,IntPtr.Zero,Pack(control.PointToClient(end)));
        }
        private static IntPtr Pack(Point point){return new IntPtr((point.X&0xffff)|((point.Y&0xffff)<<16));}
        [StructLayout(LayoutKind.Sequential)] private struct NativeBar
        {internal int Size;internal NativeTheme.NativeRect Rectangle;internal int LineButton,ThumbTop,ThumbBottom,Reserved;[MarshalAs(UnmanagedType.ByValArray,SizeConst=6)]internal int[] States;}
        [StructLayout(LayoutKind.Sequential)] private struct ScrollData
        {internal int Size,Mask,Minimum,Maximum;internal uint Page;internal int Position,TrackPosition;}
        [DllImport("user32.dll")]private static extern bool GetScrollBarInfo(IntPtr window,int objectId,ref NativeBar info);
        [DllImport("user32.dll")]private static extern bool GetScrollInfo(IntPtr window,int bar,ref ScrollData info);

        private static void CheckPicker(MainForm main)
        {
            
            main.Activate();main.BringToFront();main.Canvas.Focus();Pump();
            main.Canvas.FitToView(); Ui.SetDarkMode(true, false); Pump();
            string before = Snapshot(main.Document);
            main.Canvas.BeginConnect("check", "true");
            Point point = new Point(40, Math.Max(90, main.Canvas.Height / 2));
            foreach (string name in new[] { "OnMouseDown", "OnMouseUp" })
                typeof(FlowCanvas).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main.Canvas,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) });
            Pump();
            ContextMenuStrip picker = Field<ContextMenuStrip>(main.Canvas, "_blockPicker");
            Check(main.Canvas.IsBlockPickerOpen && picker.Visible, "block picker is visible after clicking an empty destination");
            Check(Brightness(picker.BackColor) < 110 && picker.Items.Cast<ToolStripItem>().Where(i => i.Tag is NodeKind).All(i => Brightness(i.ForeColor) > 180),
                "block picker uses the dark palette for all creation choices");
            Check(Screen.FromControl(main).WorkingArea.Contains(picker.Bounds) && picker.Items.Cast<ToolStripItem>().All(i =>
                i.Bounds.Right <= picker.ClientSize.Width && i.Bounds.Bottom <= picker.ClientSize.Height), "all picker actions fit inside the visible popup");
            Shot(main, "09-dark-picker.png"); ShotPicker(picker, "09-dark-picker-menu.png");
            Ui.SetDarkMode(false, false); Pump();
            Check(picker.Visible && Brightness(picker.BackColor) > 200, "an open picker follows the theme without losing its choices");
            Shot(main, "10-light-picker.png"); ShotPicker(picker, "10-light-picker-menu.png");
            main.Canvas.CancelBlockPicker(); Pump();
            Check(Snapshot(main.Document) == before, "theme changes and picker cancellation leave the diagram intact");
        }

        private static void ShotPicker(ContextMenuStrip picker, string name)
        {
            Pump();
            using (Bitmap bitmap = new Bitmap(picker.Width, picker.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            { picker.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(folder, name)); }
        }

    }
}
