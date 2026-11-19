using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Frociart {
 internal sealed class RuntimeSurface:Panel {
    internal bool Raised;
    internal RuntimeSurface(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Ui.Surface;}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Ui.Paper:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Ui.Rounded(new RectangleF(1,1,Width-3,Height-3),13))using(var b=new SolidBrush(Raised?Ui.Raised:Ui.Surface))using(var p=new Pen(Ui.Line)){e.Graphics.FillPath(b,path);e.Graphics.DrawPath(p,path);}}
 }
 internal sealed partial class MainForm {
    private RuntimeSurface runtimeStatusCard,runtimeOutputCard,runtimeDetailsCard;
    private FlowButton runtimePlay,runtimeDetailsToggle,runtimeSpeedButton,runtimeSnapshotButton;
    private Label runtimeEmptyVariables,runtimeEmptyOutput;
    private bool runtimeDetailsOpen=true,runtimeDeckReady;
    private string runtimeVisualKey="";
    private int observedFlowStep=-1;
    private void BuildRuntimeDeck(){
        runtimePanel.Dock=DockStyle.Fill;runtimePanel.AutoScroll=true;runtimePanel.BackColor=Ui.Paper;runtimePanel.Tag="paper";runTab.Controls.Add(runtimePanel);
        runtimeStatusCard=new RuntimeSurface();runtimePanel.Controls.Add(runtimeStatusCard);
        runtimeTitle.Font=Ui.Font(17,FontStyle.Bold);runtimeStatusCard.Controls.Add(runtimeTitle);runtimeStatusCard.Controls.Add(stepLabel);runtimeStatusCard.Controls.Add(runtimeInfo);runtimeInfo.Cursor=Cursors.Hand;runtimeInfo.Click+=delegate{if(Runner!=null)TutorTextDialog.ShowText(this,"Questo passo",Runner.State==RuntimeState.Error?Runner.Error:GetDebugger().Message??Runner.LastMessage);};
        runtimePlay=Ui.Button("Esegui",130,delegate{if(Runner!=null&&(Runner.State==RuntimeState.Completed||Runner.State==RuntimeState.Error))ResetRun(false);StartRun();},true);runtimePlay.Glyph=ButtonGlyph.Play;runtimePanel.Controls.Add(runtimePlay);
        pauseButton=Ui.Button("Pausa",130,PauseRun,true);pauseButton.Glyph=ButtonGlyph.Pause;runtimePanel.Controls.Add(pauseButton);
        resetButton=Ui.Button("Ricomincia",130,()=>ResetRun(true));runtimePanel.Controls.Add(resetButton);
        runtimeOutputCard=new RuntimeSurface{Raised=true,BackColor=Ui.Raised,Tag="raised"};runtimePanel.Controls.Add(runtimeOutputCard);outputLabel.Text="IL RISULTATO";outputLabel.Font=Ui.Font(9,FontStyle.Bold);runtimeOutputCard.Controls.Add(outputLabel);
        output.ReadOnly=true;output.MaxLength=Int32.MaxValue;output.BorderStyle=BorderStyle.None;output.Font=Ui.Mono(13);output.BackColor=Ui.Raised;output.AccessibleName="Risultato effettivo del tuo flowchart";runtimeOutputCard.Controls.Add(output);
        runtimeEmptyOutput=Ui.Label("Il risultato apparirà qui.\nEsegui il flowchart e segui il percorso blu.",10,Ui.Muted);runtimeOutputCard.Controls.Add(runtimeEmptyOutput);
        copyOutputButton=Ui.Button("Copia",80,CopyRuntimeOutput);copyOutputButton.AccessibleName="Copia il risultato";copyOutputButton.AccessibleDescription="Copia negli appunti tutto il risultato dell’esecuzione.";runtimeOutputCard.Controls.Add(copyOutputButton);copyOutputButton.BringToFront();
        copiedOutputTimer.Tick+=delegate{copiedOutputTimer.Stop();copiedRuntimeOutput=null;UpdateCopyOutputState();};copyOutputButton.Disposed+=delegate{copiedOutputTimer.Stop();copiedOutputTimer.Dispose();};
        debuggerButton=Ui.Button("Debugger · passi e pause",230,ShowDebuggerMenu);debuggerButton.Name="Debugger";debuggerButton.AccessibleDescription="Segui le funzioni e imposta punti di interruzione sui blocchi.";runtimePanel.Controls.Add(debuggerButton);
        runtimeSnapshotButton=Ui.Button("Vista da condividere",230,ShowExecutionSnapshot);runtimeSnapshotButton.AccessibleDescription="Mostra diagramma, variabili e risultato in una immagine leggibile e salvabile.";runtimePanel.Controls.Add(runtimeSnapshotButton);
        runtimeDetailsToggle=Ui.Button("Mostra dettagli",130,delegate{FlowTransitions.Change(runtimePanel);runtimeDetailsOpen=!runtimeDetailsOpen;LayoutRuntimeDeck();});runtimePanel.Controls.Add(runtimeDetailsToggle);
        locateButton=Ui.Button("Centra il blocco attivo",180,delegate{if(Runner!=null){FollowRuntimeFunction();Canvas.CenterOnNode(Runner.CurrentNodeId??Runner.LastNodeId);}});runtimePanel.Controls.Add(locateButton);
        callStackButton=Ui.Button("Variabili e chiamate",230,ShowCallStackFrames);callStackButton.Name="CallStack";runtimePanel.Controls.Add(callStackButton);
        runtimeDetailsCard=new RuntimeSurface();runtimePanel.Controls.Add(runtimeDetailsCard);varsLabel.Text="VARIABILI · doppio clic per aprire";runtimeDetailsCard.Controls.Add(varsLabel);
        variables.View=View.Details;variables.FullRowSelect=true;variables.GridLines=false;variables.BorderStyle=BorderStyle.None;variables.HeaderStyle=ColumnHeaderStyle.None;variables.Font=Ui.Mono(12);variables.ShowItemToolTips=true;variables.ItemActivate+=delegate{ShowVariableValue();};variables.Columns.Add("Variabile",120);variables.Columns.Add("Valore",150);variables.Columns.Add("Tipo",90);runtimeDetailsCard.Controls.Add(variables);
        runtimeEmptyVariables=Ui.Label("Le variabili compariranno dopo un Input o un’operazione.",9.5f,Ui.Muted);runtimeDetailsCard.Controls.Add(runtimeEmptyVariables);
        var speedLabel=Ui.Label("Velocità del flusso",9,Ui.Muted);speedLabel.Name="RuntimeSpeedLabel";speedLabel.Visible=false;runtimeDetailsCard.Controls.Add(speedLabel);speed.DropDownStyle=ComboBoxStyle.DropDownList;speed.Items.AddRange(new object[]{"Lenta","Normale","Veloce","Istantanea"});speed.SelectedIndex=1;speed.SelectedIndexChanged+=delegate{if(speed.SelectedIndex>=0&&speed.SelectedIndex<4)runTimer.Interval=new[]{1000,650,180,15}[speed.SelectedIndex];if(runtimeSpeedButton!=null)runtimeSpeedButton.Text="Velocità: "+speed.Text;};
        runtimeSpeedButton=Ui.Button("Velocità: Normale",180,delegate{var menu=new FlowContextMenu();for(int i=0;i<speed.Items.Count;i++){int index=i;var item=FlowMenus.Command(menu,this,speed.Items[i].ToString(),delegate{speed.SelectedIndex=index;});item.Checked=i==speed.SelectedIndex;}FlowMenus.ShowTransient(menu,this,runtimeSpeedButton.PointToScreen(new Point(0,runtimeSpeedButton.Height)));});runtimePanel.Controls.Add(runtimeSpeedButton);
        inputPanel.BackColor=Ui.Tint;inputPanel.Tag="tint";runtimePanel.Controls.Add(inputPanel);inputQuestion.Font=Ui.Font(10,FontStyle.Bold);inputPanel.Controls.Add(inputQuestion);inputValue.Font=Ui.Font(12);inputPanel.Controls.Add(inputValue);inputValue.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){SubmitInput();e.SuppressKeyPress=true;}};var send=Ui.Button("Invia",88,SubmitInput,true);send.Name="RuntimeSend";inputPanel.Controls.Add(send);inputPanel.Visible=false;
        BuildRuntimeFiles();BuildRuntimeTurtle();InstallConsole();runtimePanel.Resize+=delegate{LayoutRuntimeDeck();};runtimeDeckReady=true;LayoutRuntimeDeck();
    }
    private readonly Timer copiedOutputTimer=new Timer{Interval=1800};
    private string copiedRuntimeOutput,copyRuntimeErrorFor;
    internal Action<string> RuntimeClipboardWrite=WriteRuntimeClipboard;
    private static void WriteRuntimeClipboard(string text){
        // Unicode, including an actual empty output line. Retry only the idempotent write.
        var data=new DataObject();data.SetData(DataFormats.UnicodeText,false,text);
        Clipboard.SetDataObject(data,true,5,40);
    }
    private string RuntimeOutputText(){return Runner==null?"":Runner.OutputText;}
    private void UpdateCopyOutputState(){
        if(copyOutputButton==null)return;
        bool available=Runner!=null&&Runner.Output.Count>0;
        if(!available||copiedRuntimeOutput!=RuntimeOutputText()){copiedOutputTimer.Stop();copiedRuntimeOutput=null;}
        copyOutputButton.Visible=copyOutputButton.Enabled=available;
        if(!available||copyRuntimeErrorFor!=RuntimeOutputText())copyRuntimeErrorFor=null;
        bool copyError=copyRuntimeErrorFor!=null;outputLabel.Text=copyError?"COPIA NON RIUSCITA":"IL RISULTATO";outputLabel.ForeColor=copyError?Ui.Amber:Ui.Muted;
        bool copied=copiedRuntimeOutput!=null;
        copyOutputButton.Text=copied?"Copiato":"Copia";
        copyOutputButton.Accent=copied?(Color?)Ui.Green:null;
        copyOutputButton.AccessibleDescription=copied?"Risultato copiato negli appunti.":copyError?"Gli appunti sono occupati. Premi Copia per riprovare.":"Copia negli appunti tutto il risultato dell’esecuzione.";
        tips.SetToolTip(copyOutputButton,copied?"Risultato copiato negli appunti":copyError?"Gli appunti sono occupati: premi Copia per riprovare":"Copia tutto il risultato, comprese le righe vuote");copyOutputButton.Invalidate();
    }
    private void CopyRuntimeOutput(){
        if(Runner==null||Runner.Output.Count==0)return;
        string text=RuntimeOutputText();
        try{
            RuntimeClipboardWrite(text);FlowTransitions.Change(copyOutputButton);copiedRuntimeOutput=text;copyRuntimeErrorFor=null;UpdateCopyOutputState();copiedOutputTimer.Stop();copiedOutputTimer.Start();SetStatus("Risultato copiato negli appunti.");
        }catch(System.Runtime.InteropServices.ExternalException){
            copiedOutputTimer.Stop();copiedRuntimeOutput=null;copyRuntimeErrorFor=text;UpdateCopyOutputState();SetStatus("Gli appunti sono occupati. Il risultato resta qui: premi Copia per riprovare.");
        }catch(Exception ex){copiedOutputTimer.Stop();copiedRuntimeOutput=null;UpdateCopyOutputState();ShowError("Copia non riuscita",ex);}
    }
    private void AnimateRuntimeDeck(){
        string key=Runner==null?"ready":Runner.State+":"+Runner.Steps+":"+Runner.Output.Count+":"+Runner.InputVariable;
        if(key!=runtimeVisualKey){FlowTransitions.Change(runtimePanel);runtimeVisualKey=key;}
    }
    private void LayoutRuntimeDeck(){
        if(!runtimeDeckReady)return;runtimePanel.SuspendLayout();
        int w=Math.Max(220,runtimePanel.ClientSize.Width-S(24)),inner=w-S(28),gap=S(12);int y=S(12);
        runtimeStatusCard.SetBounds(S(12),y,w,S(116));runtimeTitle.SetBounds(S(16),S(15),inner,S(32));stepLabel.SetBounds(S(17),S(51),inner,S(22));runtimeInfo.SetBounds(S(17),S(80),inner,S(28));y+=S(128);
        int half=(w-gap)/2;runtimePlay.SetBounds(S(12),y,half,S(40));pauseButton.Bounds=runtimePlay.Bounds;resetButton.SetBounds(S(12)+half+gap,y,w-half-gap,S(40));y+=S(52);
        runtimeConsoleButton.SetBounds(S(12),y,w,S(38));y+=S(50);
        y=LayoutRuntimeFiles(y,w);y=LayoutRuntimeTurtle(y,w);debuggerButton.SetBounds(S(12),y,w,S(38));y+=S(50);
        runtimeSnapshotButton.SetBounds(S(12),y,w,S(38));y+=S(50);
        bool waiting=Runner!=null&&Runner.State==RuntimeState.WaitingForInput;inputPanel.Visible=waiting;if(waiting){inputPanel.SetBounds(S(12),y,w,S(116));inputQuestion.SetBounds(S(12),S(10),w-S(24),S(42));inputValue.SetBounds(S(12),S(65),w-S(120),S(32));inputPanel.Controls["RuntimeSend"].SetBounds(w-S(98),S(60),S(86),S(40));y+=S(128);}
        runtimeDetailsCard.Visible=runtimeDetailsOpen;if(runtimeDetailsOpen){int rows=Math.Max(1,Math.Min(6,variables.Items.Count)),tableHeight=S(28*rows+10),cardHeight=S(60)+tableHeight;runtimeDetailsCard.SetBounds(S(12),y,w,cardHeight);varsLabel.SetBounds(S(16),S(12),inner,S(25));variables.SetBounds(S(12),S(44),w-S(24),tableHeight);if(variables.Columns.Count>1){variables.Columns[0].Width=Math.Max(S(68),(w-S(120))/2);variables.Columns[1].Width=Math.Max(S(72),(w-S(120))/2);variables.Columns[2].Width=S(86);}runtimeEmptyVariables.Bounds=variables.Bounds;runtimeEmptyVariables.Visible=variables.Items.Count==0;variables.Visible=variables.Items.Count>0;y+=cardHeight+S(12);}
        runtimeOutputCard.SetBounds(S(12),y,w,S(172));outputLabel.SetBounds(S(16),S(16),inner-S(90),S(23));copyOutputButton.SetBounds(w-S(96),S(10),S(80),S(34));output.SetBounds(S(16),S(52),inner,S(101));runtimeEmptyOutput.Bounds=output.Bounds;runtimeEmptyOutput.Visible=output.TextLength==0;output.Visible=output.TextLength>0;
        bool hasResult=Runner!=null&&Runner.Output.Count>0;
        runtimeEmptyOutput.Text=hasResult?"Il programma ha prodotto una riga vuota.":Runner!=null&&Runner.State==RuntimeState.Completed?"Il programma non ha mostrato risultati.\nAggiungi un blocco Output per mostrare un valore.":"Il risultato apparirà qui.\nPremi Esegui per avviare il flowchart.";
        UpdateCopyOutputState();y+=S(184);
        runtimeDetailsToggle.Text=runtimeDetailsOpen?"Nascondi variabili":"Mostra variabili";runtimeDetailsToggle.SetBounds(S(12),y,w,S(34));y+=S(46);

        callStackButton.Visible=Runner!=null&&Project.Functions!=null;if(callStackButton.Visible){callStackButton.SetBounds(S(12),y,w,S(36));y+=S(48);}
        locateButton.SetBounds(S(12),y,w,S(36));locateButton.Enabled=Runner!=null;y+=S(50);runtimeSpeedButton.SetBounds(S(12),y,w,S(36));y+=S(48);runtimePanel.AutoScrollMinSize=new Size(0,y);
        bool running=runTimer.Enabled;runtimePlay.Visible=!running;pauseButton.Visible=running;runtimePlay.Enabled=!waiting&&(Runner==null||Runner.State!=RuntimeState.WaitingForFile);runtimePlay.Text=Runner!=null&&(Runner.State==RuntimeState.Completed||Runner.State==RuntimeState.Error)?"Riprova":Runner!=null&&Runner.Steps>0?"Riprendi":"Esegui";
        runtimePanel.ResumeLayout();
    }
 }
}
