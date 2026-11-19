using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Frociart")]
[assembly: AssemblyDescription("Editor e simulatore di diagrammi di flusso")]
[assembly: AssemblyCompany("mamt104")]
[assembly: AssemblyCopyright("Copyright © 2026 mamt104. Tutti i diritti riservati.")]
[assembly: AssemblyProduct("Frociart")]
[assembly: AssemblyInformationalVersion("v1")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.83.0.0")]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#if !NET8_0_OR_GREATER
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName=".NET Framework 4.8")]
#endif
#if NET8_0_OR_GREATER
[assembly: SupportedOSPlatform("windows10.0")]
#endif

namespace Frociart
{
    internal static class Program
    {
        private static bool windowReady;
        private static int exitCode;

        [STAThread]
        private static int Main(string[] args)
        {
            // Keep the bootstrap free of WinForms calls. The separate, non-inlined
            // method also lets this catch report failures while loading the UI.
            try
            {
                StartupDiagnostics.Begin();
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    StartupDiagnostics.Report(e.ExceptionObject as Exception,
                        e.IsTerminating ? "Arresto imprevisto" : "Errore in un'attività", true);
                };
                return RunApplication(args);
            }
            catch(Exception ex)
            {
                StartupDiagnostics.Report(ex,
                    windowReady ? "Frociart si è interrotto durante l'uso" : "Frociart non è riuscito ad avviarsi", true);
                return 1;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RunApplication(string[] args)
        {
            StartupDiagnostics.Stage("Inizializzazione dell'interfaccia");
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                bool startupFailure = !windowReady;
                StartupDiagnostics.Report(e.Exception,
                    startupFailure ? "Frociart non è riuscito ad aprire la finestra" : "L'operazione non è riuscita",
                    startupFailure);
                if(startupFailure) { exitCode=1; Application.Exit(); }
            };
#if NET8_0_OR_GREATER
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if(args.Length==1&&args[0]=="--configure-files"){InstallIntegration.InstallCurrent();return 0;}
            DocumentLaunch request;
            try{request=DocumentLaunch.Parse(args);}
            catch(Exception ex){if(!(ex is IOException)&&!(ex is ArgumentException)&&!(ex is NotSupportedException))throw;FlowMessageDialog.Show(null,"Non riesco ad aprire questa richiesta.\n\n"+ex.Message,"Apri progetto",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 2;}
            string file=request.Path;
            bool safeMode=request.SafeMode;
            StartupDiagnostics.Stage(safeMode ? "Costruzione della finestra (senza recupero automatico)" : "Costruzione della finestra");
            using(var main=new MainForm(file,safeMode))
            {
                main.Shown += delegate
                {
                    if(request.Imported!=null){main.SetDocument(request.Imported,null,true);main.EnterEditor();main.Canvas.FitToView();}
                    windowReady=true;
                    StartupDiagnostics.MarkWindowReady();
                };
                StartupDiagnostics.Stage("Apertura della finestra");
                Application.Run(main);
            }
            StartupDiagnostics.Stage(exitCode==0 ? "Chiusura regolare" : "Chiusura dopo un errore di avvio");
            return exitCode;
        }
    }
}



