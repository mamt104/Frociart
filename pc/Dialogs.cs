using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed class ExamplesDialog : Form
    {
        private readonly List<Diagram> diagrams=Examples.All();
        private readonly ListBox list=new ListBox();
        private readonly FlowCanvas preview=new FlowCanvas();
        private readonly Label caption=Ui.Label("",12,null,true),description=Ui.Label("",9.5f,Ui.Muted);
        internal Diagram Selected { get; private set; }
        internal ExamplesDialog()
        {
            SuspendLayout();
            Text="Esempi — Frociart";Size=new Size(930,686);MinimumSize=new Size(850,630);StartPosition=FormStartPosition.CenterParent;
            Font=Ui.Font();BackColor=Ui.Paper;MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;
            AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
            var header=new Panel {Dock=DockStyle.Top,Height=96,BackColor=Ui.Surface};Controls.Add(header);
            var title=Ui.Label("Scegli da dove partire",21,null,true);title.SetBounds(25,15,850,36);header.Controls.Add(title);
            var subtitle=Ui.Label("Esplora un algoritmo, cambia i valori e osserva cosa succede.",10,Ui.Muted);subtitle.SetBounds(26,59,820,25);header.Controls.Add(subtitle);
            var footer=new Panel {Dock=DockStyle.Bottom,Height=67,BackColor=Ui.Surface};Controls.Add(footer);
            var footerActions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(20,16,20,0)};footer.Controls.Add(footerActions);
            var cancel=Ui.Button("Annulla",100,()=>{DialogResult=DialogResult.Cancel;Close();});
            var open=Ui.Button("Apri esempio",150,OpenSelected,true);footerActions.Controls.Add(open);footerActions.Controls.Add(cancel);AcceptButton=open;CancelButton=cancel;
            var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(20,17,20,17)};
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,280));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(body);body.BringToFront();
            list.Dock=DockStyle.Fill;list.BorderStyle=BorderStyle.None;list.BackColor=Ui.Paper;list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=80;list.IntegralHeight=false;list.Font=Ui.Font(10);
            list.DrawItem+=(s,e)=>{
                if(e.Index<0||e.Index>=diagrams.Count)return;float scale=e.Graphics.DpiX/96f;Func<int,int> px=v=>(int)Math.Round(v*scale);bool selected=(e.State&DrawItemState.Selected)!=0;var r=e.Bounds;r.Inflate(-px(3),-px(4));
                using(var p=Ui.Rounded(new RectangleF(r.Left,r.Top,r.Width-px(3),r.Height),px(9)))
                using(var b=new SolidBrush(selected?Ui.Hover:Ui.Surface))e.Graphics.FillPath(b,p);
                using(var f=Ui.Font(10,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,diagrams[e.Index].Title,f,new Rectangle(r.Left+px(14),r.Top+px(12),r.Width-px(24),px(28)),selected?Ui.Blue:Ui.Ink,TextFormatFlags.EndEllipsis);
                using(var f=Ui.Font(8.8f))TextRenderer.DrawText(e.Graphics,Category(diagrams[e.Index]),f,new Rectangle(r.Left+px(14),r.Top+px(43),r.Width-px(24),px(21)),Ui.Muted);
            };
            foreach(var d in diagrams)list.Items.Add(d.Title);list.SelectedIndexChanged+=delegate{UpdatePreview();};list.DoubleClick+=delegate{OpenSelected();};body.Controls.Add(list,0,0);
            var right=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(17,0,0,0)};
            right.RowStyles.Add(new RowStyle(SizeType.Absolute,35));right.RowStyles.Add(new RowStyle(SizeType.Absolute,57));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(right,1,0);
            caption.Dock=DockStyle.Fill;description.Dock=DockStyle.Fill;right.Controls.Add(caption,0,0);right.Controls.Add(description,0,1);
            preview.Dock=DockStyle.Fill;preview.Enabled=false;right.Controls.Add(preview,0,2);list.SelectedIndex=1;
            Shown+=delegate{using(var g=CreateGraphics())list.ItemHeight=(int)Math.Round(80*g.DpiX/96f);UpdatePreview();};preview.Resize+=delegate{preview.FitToView();};
            Ui.ApplyTheme(this);
            ResumeLayout(true);
        }
        private void UpdatePreview()
        {
            if(list.SelectedIndex<0)return;var d=diagrams[list.SelectedIndex];caption.Text=d.Title;
            description.Text=Description(d);
            preview.Diagram=DiagramIO.Clone(d);preview.FitToView();
        }
        internal static string Category(Diagram diagram){switch(diagram.Title){case "Somma di due numeri":return "Calcoli e variabili";case "Pari o dispari":return "Decisioni e rami";case "Conto alla rovescia":return "Cicli e contatori";case "Maggiore tra due numeri":return "Confronti";case "Esercizio: trova l'errore":return "Trova e ripara";case "Copia un file di testo":return "File · lettura e scrittura";case "Disegna un quadrato":return "Disegno · tartaruga";default:return "Esplora un algoritmo";}}
        internal static string Description(Diagram diagram){switch(diagram.Title){case "Somma di due numeri":return "Chiedi due numeri, sommali e mostra il risultato.";case "Pari o dispari":return "Leggi un intero e segui il ramo Sì o No in base al resto della divisione.";case "Conto alla rovescia":return "Ripeti i passaggi finché il contatore raggiunge lo zero.";case "Maggiore tra due numeri":return "Confronta due valori e mostra quello maggiore. Prova anche due numeri uguali.";case "Esercizio: trova l'errore":return "C'è un collegamento mancante. Usa Verifica per scoprirlo e poi ripara il diagramma.";case "Copia un file di testo":return "Apri il file da leggere, scegli dove salvare la copia e segui il ciclo che conserva ogni riga, anche quelle vuote.";case "Disegna un quadrato":return "Segui quattro lati con un ciclo e osserva il disegno della tartaruga. Modifica la lunghezza o l'angolo per provare altre forme.";default:return "Apri il diagramma, modifica i valori e segui la sua esecuzione.";}}
        private void OpenSelected(){if(list.SelectedIndex<0)return;Selected=DiagramIO.Clone(diagrams[list.SelectedIndex]);DialogResult=DialogResult.OK;Close();}
    }

    internal sealed class GuideDialog : Form
    {
        private readonly RichTextBox text=new FlowRichTextBox();
        private readonly List<Tuple<int,int>> headings=new List<Tuple<int,int>>();
        internal GuideDialog()
        {
            SuspendLayout();
            Text="Guida — Frociart";Size=new Size(865,740);MinimumSize=new Size(640,480);StartPosition=FormStartPosition.CenterParent;
            BackColor=Ui.Surface;Font=Ui.Font();ShowInTaskbar=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
            var header=new Panel{Dock=DockStyle.Top,Height=85};var title=Ui.Label("Impara facendo",22,null,true);title.SetBounds(26,14,700,41);header.Controls.Add(title);
            var sub=Ui.Label("Una prova concreta, un passaggio alla volta.",10,Ui.Muted);sub.SetBounds(27,57,700,23);header.Controls.Add(sub);Controls.Add(header);
            var footer=new Panel{Dock=DockStyle.Bottom,Height=58};var close=Ui.Button("Ho capito",118,Close,true);close.SetBounds(26,10,118,35);footer.Controls.Add(close);Controls.Add(footer);AcceptButton=close;CancelButton=close;
            text.Dock=DockStyle.Fill;text.ReadOnly=true;text.BorderStyle=BorderStyle.None;text.BackColor=Ui.Surface;text.ForeColor=Ui.Ink;text.Font=Ui.Font(10.5f);text.DetectUrls=false;text.WordWrap=true;
            var panel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(27,12,23,10)};panel.Controls.Add(text);Controls.Add(panel);panel.BringToFront();
            Ui.ApplyTheme(this);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Frociart.Guide.txt"))
            {if(stream!=null)using(var reader=new StreamReader(stream))text.Text=reader.ReadToEnd();else text.Text="Aggiungi i blocchi, collegali e premi Verifica. Usa Un passo per seguire il percorso. Salva conserva il diagramma modificabile.";}
            foreach(string line in text.Lines)
            {
                if(line.Length>3&&line==line.ToUpperInvariant()&&line.Any(Char.IsLetter))
                {
                    int start=text.Find(line,RichTextBoxFinds.MatchCase);if(start>=0)headings.Add(Tuple.Create(start,line.Length));
                }
            }
            ApplyTextTheme();text.Select(0,0);text.ScrollToCaret();
            Shown+=delegate{ApplyTextTheme();};Ui.ThemeChanged+=OnThemeChanged;
            ResumeLayout(true);
        }
        private void OnThemeChanged(object sender,EventArgs e){ApplyTextTheme();}
        private void ApplyTextTheme()
        {
            int start=text.SelectionStart,length=text.SelectionLength;
            text.ForeColor=Ui.Ink;text.SelectAll();text.SelectionColor=Ui.Ink;
            using(var headingFont=Ui.Font(11,FontStyle.Bold))
                foreach(var heading in headings)
                {text.Select(heading.Item1,heading.Item2);text.SelectionFont=headingFont;text.SelectionColor=Ui.Blue;}
            text.Select(Math.Min(start,text.TextLength),Math.Min(length,Math.Max(0,text.TextLength-start)));
        }
        protected override void Dispose(bool disposing)
        {if(disposing)Ui.ThemeChanged-=OnThemeChanged;base.Dispose(disposing);}
    }

    internal sealed class TestResult
    {
        internal bool Passed;
        internal string Title, Message;
        internal string[] Actual;
        internal int Steps;
    }

    internal sealed class TestSession
    {
        internal readonly Interpreter Runner;
        private readonly string[] inputs,expected;
        private int consumed;
        internal TestResult Result { get; private set; }
        internal TestSession(Diagram document,string inputText,string expectedText)
        {Runner=new Interpreter(document);inputs=Lines(inputText);expected=Lines(expectedText);}
        internal static string[] Lines(string text)
        {
            if(String.IsNullOrEmpty(text))return new string[0];
            string normalized=text.Replace("\r\n","\n").Replace("\r","\n");
            if(normalized.EndsWith("\n",StringComparison.Ordinal))normalized=normalized.Substring(0,normalized.Length-1);
            return normalized.Split('\n').Select(DecodeLine).ToArray();
        }
        // Escape only the reserved empty-row marker family; other backslashes stay literal.
        private static string DecodeLine(string value){if(value=="\"\"")return "";return value.Length>2&&value.EndsWith("\"\"",StringComparison.Ordinal)&&value.Take(value.Length-2).All(c=>c=='\\')?value.Substring(1):value;}
        internal bool Tick(int budget=100)
        {
            if(budget<1)throw new ArgumentOutOfRangeException("budget","Serve un budget di esecuzione positivo.");
            if(Result!=null)return true;
            for(int i=0;i<budget;i++)
            {
                if(Runner.State==RuntimeState.Error){Finish(false,"Prova interrotta",Runner.Error);return true;}
                if(Runner.State==RuntimeState.WaitingForFile){Finish(false,"Serve un file","Questa prova richiede di scegliere o salvare un file. Apri Esegui per provarla con i documenti. Nessun file è stato aperto o salvato dalla prova dati.");return true;}
                if(Runner.State==RuntimeState.Completed)
                {
                    if(consumed<inputs.Length){Finish(false,"Input in più", "Input usati: "+consumed+". Input forniti: "+inputs.Length+". Correggi i dati della prova.");return true;}
                    var actual=ActualLines();bool match=actual.SequenceEqual(expected,StringComparer.Ordinal);
                    string detail=(actual.Length==0?"Il programma non ha prodotto output, come previsto.":actual.Length==1?"La riga di output coincide con quella attesa.":"Tutte le "+actual.Length+" righe di output coincidono.")+" Questo esito riguarda i dati di questa prova.";
                    if(!match)
                    {
                        int first=0;while(first<Math.Min(actual.Length,expected.Length)&&actual[first]==expected[first])first++;
                        detail="La prima differenza è alla riga "+(first+1)+". Righe attese: "+expected.Length+"; ottenute: "+actual.Length+". Controlla anche spazi e maiuscole.";
                    }
                    Finish(match,match?"Prova superata":"Risultato diverso",detail);return true;
                }
                if(Runner.State==RuntimeState.WaitingForInput)
                {
                    if(consumed>=inputs.Length){Finish(false,"Manca un input","Serve un altro valore per «"+Runner.InputVariable+"». Inseriscilo alla riga "+(consumed+1)+".");return true;}
                    if(!Runner.ProvideInput(inputs[consumed++])){Finish(false,"Input non accettato",Runner.LastMessage);return true;}
                }
                else Runner.Step();
            }
            return false;
        }
        private void Finish(bool passed,string title,string message)
        {Result=new TestResult {Passed=passed,Title=title,Message=message,Actual=ActualLines(),Steps=Runner.Steps};}
        private string[] ActualLines()
        {return Runner.Output.Count==0?new string[0]:Runner.OutputText.Replace("\r\n","\n").Replace("\r","\n").Split('\n');}
    }

    internal sealed class TestDialog : Form
    {
        private readonly Diagram document;
        private readonly TextBox inputs=Ui.TextBox(true),expected=Ui.TextBox(true),actual=Ui.TextBox(true);
        private readonly Label resultTitle=Ui.Label("Prepara la tua prova",16,null,true),resultInfo=Ui.Label("",9.3f,Ui.Muted);
        private readonly Timer timer=new Timer{Interval=15};
        private readonly FlowButton start,stop;
        private TestSession session;
        internal TestDialog(Diagram diagram)
        {
            SuspendLayout();
            document=DiagramIO.Clone(diagram);Text="Prova dati — Frociart";Size=new Size(1000,657);MinimumSize=new Size(850,580);StartPosition=FormStartPosition.CenterParent;
            Font=Ui.Font();BackColor=Ui.Surface;ShowInTaskbar=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(24,14,24,16)};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,91));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,132));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(layout);
            var header=new Panel{Dock=DockStyle.Fill};var title=Ui.Label("Il risultato è quello che ti aspettavi?",21,null,true);title.SetBounds(0,0,930,39);header.Controls.Add(title);
            var sub=Ui.Label("Una riga per ogni valore. Confronto esatto di testo, spazi e maiuscole. Per una riga vuota scrivi \"\".",9.4f,Ui.Muted);sub.SetBounds(1,48,930,35);header.Controls.Add(sub);layout.Controls.Add(header,0,0);
            header.Resize+=delegate{title.Width=header.ClientSize.Width;sub.Width=Math.Max(1,header.ClientSize.Width-sub.Left);};
            var editors=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};for(int i=0;i<3;i++)editors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));layout.Controls.Add(editors,0,1);
            string[] heads={"1   INPUT, IN ORDINE","2   OUTPUT ATTESO","3   OUTPUT OTTENUTO"};
            string[] hints={"I valori da fornire al programma.","Scrivilo prima di avviare la prova.","Si aggiorna alla fine dell'esecuzione."};
            TextBox[] boxes={inputs,expected,actual};
            for(int i=0;i<3;i++)
            {
                var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(i==0?0:8,0,i==2?0:8,0)};
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute,27));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,34));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                var label=Ui.Label(heads[i],8.8f,i==2?Ui.Green:Ui.Blue,true);label.Dock=DockStyle.Fill;panel.Controls.Add(label,0,0);
                var hint=Ui.Label(hints[i],8.6f,Ui.Muted);hint.Dock=DockStyle.Fill;panel.Controls.Add(hint,0,1);boxes[i].Dock=DockStyle.Fill;panel.Controls.Add(boxes[i],0,2);editors.Controls.Add(panel,i,0);
            }
            inputs.MaxLength=262144;expected.MaxLength=262144;actual.MaxLength=262144;actual.ReadOnly=true;actual.BackColor=Ui.Paper;
            var result=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,16,0,0)};resultTitle.SetBounds(0,18,920,35);resultInfo.SetBounds(1,59,920,63);result.Controls.Add(resultTitle);result.Controls.Add(resultInfo);layout.Controls.Add(result,0,2);
            result.Resize+=delegate{resultTitle.Width=result.ClientSize.Width;resultInfo.Width=Math.Max(1,result.ClientSize.Width-resultInfo.Left);};
            resultInfo.Text="La prova esegue fino a 10.000 blocchi. Un risultato corretto con questi dati non garantisce la correttezza per ogni input.";
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};start=Ui.Button("Avvia prova",145,Start,true);stop=Ui.Button("Interrompi",105,Stop);stop.Enabled=false;actions.Controls.Add(start);actions.Controls.Add(stop);
            actions.Controls.Add(Ui.Button("Chiudi",92,Close));layout.Controls.Add(actions,0,3);
            if(diagram.Title=="Pari o dispari"){inputs.Text="7";expected.Text="Dispari";}
            else if(diagram.Title=="Somma di due numeri"){inputs.Text="2\r\n3";expected.Text="Somma: 5";}
            else if(diagram.Title=="Conto alla rovescia"){inputs.Text="3";expected.Text="3\r\n2\r\n1\r\nVia!";}
            else if(diagram.Title=="Maggiore tra due numeri"){inputs.Text="-8\r\n-2";expected.Text="Maggiore: -2";}
            EventHandler edited=delegate{if(timer.Enabled||session==null)return;session=null;actual.Text="";resultTitle.Text="Prepara la tua prova";resultTitle.ForeColor=Ui.Ink;resultInfo.Text="I dati sono cambiati: avvia di nuovo la prova.";};inputs.TextChanged+=edited;expected.TextChanged+=edited;
            timer.Tick+=delegate{
                if(session==null)return;
                if(session.Tick())
                {
                    timer.Stop();SetRunning(false);var r=session.Result;
                    resultTitle.Text=(r.Passed?"✓  ":"•  ")+r.Title;resultTitle.ForeColor=r.Passed?Ui.Green:Ui.Red;
                    resultInfo.Text=r.Message+"\n"+r.Steps+(r.Steps==1?" blocco eseguito.":" blocchi eseguiti.");actual.Text=String.Join(Environment.NewLine,r.Actual.Select(s=>s.Length==0?"(riga vuota)":s));
                }
                else resultInfo.Text=session.Runner.Steps+(session.Runner.Steps==1?" blocco eseguito":" blocchi eseguiti")+"… Puoi interrompere in qualsiasi momento.";
            };
            Ui.ApplyTheme(this);
            ResumeLayout(true);
        }
        private void Start()
        {session=new TestSession(document,inputs.Text,expected.Text);actual.Text="";resultTitle.Text="Prova in corso…";resultTitle.ForeColor=Ui.Blue;SetRunning(true);timer.Start();}
        private void Stop(){timer.Stop();SetRunning(false);resultTitle.Text="Prova interrotta";resultTitle.ForeColor=Ui.Muted;resultInfo.Text="Nessun esito assegnato. Puoi modificare i valori e riprovare.";}
        private void SetRunning(bool running){start.Enabled=!running;stop.Enabled=running;inputs.ReadOnly=expected.ReadOnly=running;}
        protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
    }
}
