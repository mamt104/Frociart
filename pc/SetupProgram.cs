using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Frociart {
    internal static class SetupProgram {
        [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SetupWindow());}
        sealed class SetupWindow:Form {
            [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int length);
            protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);try{int enabled=1;if(DwmSetWindowAttribute(Handle,20,ref enabled,4)!=0)DwmSetWindowAttribute(Handle,19,ref enabled,4);}catch{}}
            readonly Button install=new Button(),remove=new Button();readonly Label result=new Label();bool busy,ready;
            internal SetupWindow(){
                Text="Frociart v1 — Configura sul PC";ClientSize=new Size(600,410);MinimumSize=new Size(560,430);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(23,24,28);ForeColor=Color.FromArgb(243,244,248);Font=new Font("Segoe UI",10);MaximizeBox=false;
                try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
                if(SystemInformation.IsMenuAnimationEnabled){Opacity=0;Shown+=delegate{var fade=new Timer{Interval=15};var time=Stopwatch.StartNew();fade.Tick+=delegate{double t=Math.Min(1,time.Elapsed.TotalMilliseconds/190);Opacity=1-Math.Pow(1-t,3);if(t>=1){fade.Stop();fade.Dispose();}};FormClosed+=delegate{fade.Dispose();};fade.Start();};}
                var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(30),ColumnCount=1,RowCount=5};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,105));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));Controls.Add(layout);
                var heading=new Panel{Dock=DockStyle.Fill};
                var mark=new PictureBox{Location=new Point(0,0),Size=new Size(48,48)};mark.Paint+=(s,e)=>BrandArtwork.Draw(e.Graphics,mark.ClientRectangle,Color.FromArgb(117,172,255));heading.Controls.Add(mark);
                heading.Controls.Add(new Label{Text="Frociart v1",Font=new Font("Segoe UI",21,FontStyle.Bold),Location=new Point(60,0),AutoSize=true});
                heading.Controls.Add(new Label{Text="La logica prende forma.",Font=new Font("Segoe UI",9),ForeColor=Color.FromArgb(183,189,206),Location=new Point(63,39),AutoSize=true});layout.Controls.Add(heading,0,0);
                layout.Controls.Add(new Label{Text="Installa per aprire i progetti con doppio clic e riconoscerli dalla loro icona.\n\nNessun account, connessione o accesso amministratore richiesto.",Dock=DockStyle.Fill},0,1);
                result.Dock=DockStyle.Fill;result.ForeColor=Color.FromArgb(185,189,201);result.Text="I progetti e i progressi esistenti vengono conservati.";result.AccessibleRole=AccessibleRole.StaticText;layout.Controls.Add(result,0,2);
                install.Text="Installa Frociart";install.Dock=DockStyle.Fill;install.BackColor=Color.FromArgb(117,172,255);install.ForeColor=Color.FromArgb(12,25,47);install.FlatStyle=FlatStyle.Flat;install.FlatAppearance.BorderSize=0;install.Font=new Font("Segoe UI",11,FontStyle.Bold);layout.Controls.Add(install,0,3);
                remove.Text="Rimuovi Frociart da questo PC";remove.Dock=DockStyle.Fill;remove.FlatStyle=FlatStyle.Flat;remove.FlatAppearance.BorderSize=0;remove.ForeColor=Color.FromArgb(185,189,201);remove.Visible=remove.Enabled=File.Exists(FileRegistration.InstalledExe);layout.Controls.Add(remove,0,4);
                install.Click+=async delegate{if(ready){try{Process.Start(new ProcessStartInfo(FileRegistration.InstalledExe){UseShellExecute=false});Close();}catch{ready=false;install.Text="Installa Frociart";result.Text="Non riesco ad aprire l'app. Premi Installa Frociart per ripristinare i file.";}return;}await Run(false);};remove.Click+=async delegate{await Run(true);};FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
            }
            async Task Run(bool uninstall){
                if(busy)return;busy=true;install.Enabled=remove.Enabled=false;result.Text=uninstall?"Rimozione dei file dell'app…":"Configurazione dei file Frociart…";
                try{await Task.Run(()=>{if(uninstall)InstallIntegration.Remove();else InstallIntegration.InstallPackage(InstallIntegration.Resource("Frociart.Payload.exe"),InstallIntegration.Resource("Frociart.Document.ico"));});
                    ready=!uninstall;install.Text=ready?"Apri Frociart":"Installa Frociart";result.Text=uninstall?"App e associazioni rimosse. Progetti e progressi sono stati conservati.":"Frociart è installato. I progetti .frociart hanno la loro icona e si aprono con doppio clic. Se Windows aveva già un'altra app predefinita, scegli Frociart da Apri con.";
                }catch(IOException){result.Text="Chiudi le finestre di Frociart e riprova. I progetti non sono stati modificati.";}
                catch(UnauthorizedAccessException){result.Text="Non riesco a scrivere nella cartella dell'utente. Controlla i permessi e riprova.";}
                catch(Exception){result.Text="La configurazione non è stata completata. Riprova con una nuova copia del pacchetto.";}
                finally{busy=false;install.Enabled=true;remove.Visible=remove.Enabled=File.Exists(FileRegistration.InstalledExe);}
            }
        }
    }
}

