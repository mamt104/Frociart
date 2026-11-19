using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Frociart.LightSetup {
    internal static class LightSetupProgram {
        [STAThread]static void Main(){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            bool owner;using(var single=new Mutex(true,@"Local\Frociart.LightSetup."+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out owner)){
                if(!owner){MessageBox.Show("L'installer di Frociart è già aperto.","Frociart v1",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
                try{Application.Run(new LightSetupWindow(new WindowsSetupHost()));}finally{single.ReleaseMutex();}
            }
        }
    }
    internal sealed class LightSetupWindow:Form {
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int length);
        readonly ISetupHost host;readonly Label heading=new Label(),description=new Label(),status=new Label(),details=new Label();
        internal readonly Button Primary=new Button(),CancelAction=new Button();internal readonly DownloadProgress ProgressBar=new DownloadProgress();
        internal bool Busy {get;private set;}internal SetupStage Stage {get;private set;}
        SetupControl cancellation;bool ready;System.Windows.Forms.Timer fade;
        internal string StatusText {get{return status.Text;}}
        internal LightSetupWindow(ISetupHost host){
            this.host=host;Text="Installa Frociart v1";ClientSize=new Size(670,510);MinimumSize=new Size(620,500);
            StartPosition=FormStartPosition.CenterScreen;MaximizeBox=false;BackColor=Color.FromArgb(23,24,28);ForeColor=Color.FromArgb(243,244,248);Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Font;
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch(ArgumentException){}
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=1,RowCount=6};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,70));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,22));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));Controls.Add(layout);
            var title=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};title.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,62));title.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var mark=new PictureBox{Dock=DockStyle.Fill,TabStop=false,AccessibleRole=AccessibleRole.None};mark.Paint+=(s,e)=>BrandArtwork.Draw(e.Graphics,new RectangleF(0,0,48,48),Color.FromArgb(117,172,255));title.Controls.Add(mark,0,0);
            heading.Text="Frociart v1";heading.Font=new Font("Segoe UI",23,FontStyle.Bold);heading.Dock=DockStyle.Fill;heading.AccessibleRole=AccessibleRole.StaticText;title.Controls.Add(heading,1,0);layout.Controls.Add(title,0,0);
            description.Text="La logica prende forma.\n\nInstalla Frociart per il tuo utente: lo trovi nel menu Start e nelle App installate di Windows.\nI tuoi diagrammi e le impostazioni esistenti vengono conservati.";
            description.AutoSize=true;description.Dock=DockStyle.Fill;description.Margin=new Padding(0,6,0,20);layout.Controls.Add(description,0,1);
            status.Text="Il setup controlla i componenti e scarica da Microsoft soltanto quelli mancanti.";status.Dock=DockStyle.Fill;status.Padding=new Padding(0,8,0,8);status.AccessibleName="Stato dell'installazione";status.AccessibleRole=AccessibleRole.StaticText;layout.Controls.Add(status,0,2);
            ProgressBar.Dock=DockStyle.Fill;ProgressBar.Visible=false;ProgressBar.AccessibleName="Download del componente Microsoft";layout.Controls.Add(ProgressBar,0,3);
            details.Text="Internet serve solo se manca .NET Desktop Runtime 10 (x64).\nPer quel componente Windows può chiedere l'autorizzazione.\nNessun account. Il programma funziona poi anche offline.";
            details.AutoSize=true;details.Dock=DockStyle.Fill;details.ForeColor=Color.FromArgb(185,189,201);details.Margin=new Padding(0,8,0,14);layout.Controls.Add(details,0,4);
            var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
            ConfigureButton(CancelAction,"Chiudi",false);ConfigureButton(Primary,"Installa Frociart",true);CancelAction.Margin=new Padding(0,4,12,4);Primary.Margin=new Padding(0,4,0,4);
            actions.Controls.Add(CancelAction,0,0);actions.Controls.Add(Primary,1,0);layout.Controls.Add(actions,0,5);AcceptButton=Primary;CancelButton=CancelAction;
            FontChanged+=(s,e)=>{heading.Font=new Font(Font.FontFamily,Font.Size*2.3f,FontStyle.Bold);Primary.Font=new Font(Font,FontStyle.Bold);CancelAction.Font=new Font(Font,FontStyle.Bold);};
            Primary.Click+=async(s,e)=>{if(ready){try{Process.Start(new ProcessStartInfo(FileRegistration.InstalledExe){UseShellExecute=false});Close();}catch(Exception error){if(!(error is IOException||error is System.ComponentModel.Win32Exception||error is UnauthorizedAccessException))throw;ready=false;Primary.Text="Ripristina Frociart";status.Text="Non riesco ad aprire Frociart. Chiudi eventuali finestre dell'app e premi Ripristina Frociart.";}return;}await Install();};
            CancelAction.Click+=(s,e)=>{if(!Busy)Close();else RequestCancel();};
            FormClosing+=(s,e)=>{if(Busy){e.Cancel=true;RequestCancel();}};
            Shown+=(s,e)=>{Primary.Focus();if(SystemInformation.IsMenuAnimationEnabled){Opacity=0;var clock=Stopwatch.StartNew();fade=new System.Windows.Forms.Timer{Interval=15};fade.Tick+=(o,a)=>{double t=Math.Min(1,clock.Elapsed.TotalMilliseconds/180);Opacity=1-Math.Pow(1-t,3);if(t>=1){fade.Stop();fade.Dispose();fade=null;}};fade.Start();}};
            FormClosed+=(s,e)=>{if(fade!=null){fade.Stop();fade.Dispose();fade=null;}};
        }
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);try{int value=1;if(DwmSetWindowAttribute(Handle,20,ref value,4)!=0)DwmSetWindowAttribute(Handle,19,ref value,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
        static void ConfigureButton(Button button,string text,bool main){button.Text=text;button.AccessibleName=text;button.Dock=DockStyle.Fill;button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=main?0:1;button.BackColor=main?Color.FromArgb(117,172,255):Color.FromArgb(38,40,46);button.ForeColor=main?Color.FromArgb(12,25,47):Color.FromArgb(243,244,248);button.UseVisualStyleBackColor=false;button.Font=new Font("Segoe UI",10,FontStyle.Bold);}
        internal void RequestCancel(){
            if(!Busy||cancellation==null)return;
            if(cancellation.TryCancel()){status.Text="Annullamento e pulizia dei file temporanei…";CancelAction.Enabled=false;}
            else status.Text=Stage==SetupStage.InstallingRuntime?"Microsoft sta installando il componente. Attendi la chiusura della sua finestra: non verrà forzato alcun riavvio.":"Sto completando la configurazione dei file. Attendi ancora un momento.";
        }
        async Task Install(){
            if(Busy)return;Busy=true;Stage=SetupStage.Checking;ready=false;cancellation=new SetupControl();Primary.Enabled=false;CancelAction.Text="Annulla";CancelAction.AccessibleName="Annulla installazione";
            try {
                IProgress<SetupProgress> progress=new Progress<SetupProgress>(UpdateProgress);
                var result=await Task.Run(()=>new SetupEngine(host).Run(progress.Report,cancellation));
                UpdateProgress(new SetupProgress(result));ready=result==SetupStage.Complete;
                Primary.Text=ready?"Apri Frociart":"Riprova dopo il riavvio";
            }catch(OperationCanceledException){status.Text="Installazione annullata. I file di Frociart e i progetti non sono stati aggiornati.";Primary.Text="Riprova";}
            catch(SetupProblem error){status.Text=error.Message;Primary.Text="Riprova";}
            catch(IOException){status.Text="Non riesco a completare i file. Chiudi Frociart, controlla lo spazio disponibile e riprova. I progetti non sono stati modificati.";Primary.Text="Riprova";}
            catch(UnauthorizedAccessException){status.Text="Scrittura non consentita nella cartella dell'utente. Controlla i permessi e riprova.";Primary.Text="Riprova";}
            catch(Exception){status.Text="Configurazione non completata. Puoi riprovare o scaricare una nuova copia dell'installer. I progetti non sono stati modificati.";Primary.Text="Riprova";}
            finally{Busy=false;cancellation.Dispose();cancellation=null;Primary.Enabled=true;Primary.AccessibleName=Primary.Text;CancelAction.Enabled=true;CancelAction.Text="Chiudi";CancelAction.AccessibleName="Chiudi installer";ProgressBar.Visible=false;Primary.Focus();}
        }
        void UpdateProgress(SetupProgress progress){
            Stage=progress.Stage;CancelAction.Enabled=progress.CanCancel;
            ProgressBar.Visible=progress.Stage==SetupStage.Downloading;
            switch(progress.Stage){
                case SetupStage.Checking:status.Text="Controllo del pacchetto e dei componenti già presenti…";break;
                case SetupStage.Downloading:
                    ProgressBar.SetProgress(progress.Received,progress.Total);
                    status.Text=progress.Total>0?"Download del componente Microsoft: "+(progress.Received/1048576.0).ToString("0.0")+" / "+(progress.Total/1048576.0).ToString("0.0")+" MB":"Download del componente Microsoft…";break;
                case SetupStage.Verifying:status.Text="Verifica dell'integrità e della firma Microsoft…";break;
                case SetupStage.InstallingRuntime:status.Text="Installazione del componente Microsoft. Se Windows lo chiede, autorizza l'operazione. Non verrà forzato alcun riavvio.";break;
                case SetupStage.InstallingApp:status.Text="Installazione di Frociart, collegamento Start e apertura dei progetti…";break;
                case SetupStage.Complete:status.Text="Frociart è installato. Lo trovi nel menu Start e puoi rimuoverlo dalle App installate di Windows conservando progetti e impostazioni. Per i file .frociart, un'altra app predefinita resta conservata: puoi scegliere Frociart da Apri con.";break;
                case SetupStage.RestartRequired:status.Text="Microsoft richiede un riavvio di Windows. Salva il tuo lavoro, riavvia quando preferisci e riapri questo installer. I file di Frociart non sono stati aggiornati.";break;
            }
        }
    }
}
