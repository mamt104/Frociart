using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Frociart
{
    /// <summary>
    /// Bootstrap diagnostics use only the base class library. A native Windows
    /// message box remains available even when Windows Forms cannot initialize.
    /// Diagnostic failures must never prevent the application from opening.
    /// </summary>
    internal static class StartupDiagnostics
    {
        private static readonly object gate = new object();
        private static string logPath;
        private static string phase = "Avvio";
        private static int reporting;
        private static int windowOpened;

        internal static string LogPath { get { return logPath; } }

        internal static void MarkWindowReady()
        {
            Interlocked.Exchange(ref windowOpened,1);
            Stage("Finestra aperta: avvio completato");
        }

        internal static string ErrorCaption(bool fatal)
        {
            if(!fatal) return "Frociart — Errore";
            return Volatile.Read(ref windowOpened)==0
                ? "Frociart — Problema di avvio" : "Frociart — Arresto imprevisto";
        }

        internal static void Begin()
        {
            try
            {
                string root=Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if(!String.IsNullOrWhiteSpace(root)) TryOpenLog(Path.Combine(root,"Frociart","Diagnostica"));
            }
            catch { }
            if(logPath==null)
            {
                try { TryOpenLog(Path.Combine(Path.GetTempPath(),"Frociart","Diagnostica")); }
                catch { }
            }
            Write("FROCIART "+typeof(StartupDiagnostics).Assembly.GetName().Version);
            var build=(System.Reflection.AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(typeof(StartupDiagnostics).Assembly,typeof(System.Reflection.AssemblyFileVersionAttribute));
            if(build!=null)Write("Build: "+build.Version);
            Write("Sistema: "+Environment.OSVersion);
            Write("Runtime: "+Environment.Version+"; processo "+(Environment.Is64BitProcess ? "64" : "32")+" bit");
            Write("Questo file resta sul dispositivo. Non viene inviato automaticamente.");
            Stage("Avvio");
        }

        private static void TryOpenLog(string directory)
        {
            Directory.CreateDirectory(directory);
            int pid;
            using(var process=Process.GetCurrentProcess()) pid=process.Id;
            string path=Path.Combine(directory,"avvio-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+pid+".txt");
            File.WriteAllText(path,"",new UTF8Encoding(true));
            logPath=path;
            // Bound storage to a small number of our own old startup logs.
            try
            {
                var files=new DirectoryInfo(directory).GetFiles("avvio-*.txt");
                Array.Sort(files,delegate(FileInfo a,FileInfo b){return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc);});
                for(int i=19;i<files.Length;i++) if(files[i].FullName!=logPath) files[i].Delete();
            }
            catch { }
        }

        internal static void Stage(string value)
        {
            phase=value;
            Write("Fase: "+value);
        }

        private static void Write(string message)
        {
            try
            {
                lock(gate)
                {
                    if(logPath!=null) File.AppendAllText(logPath,DateTime.Now.ToString("HH:mm:ss.fff")+"  "+message+Environment.NewLine,Encoding.UTF8);
                }
            }
            catch { }
        }

        internal static void Report(Exception exception,string title,bool fatal)
        {
            if(Interlocked.Exchange(ref reporting,1)!=0) return;
            try
            {
                Write("ERRORE — "+title+"; fase: "+phase);
                Write(SafeException(exception));
                Exception cause=exception;
                while(cause!=null&&cause.InnerException!=null) cause=cause.InnerException;
                string detail=cause==null ? "Errore imprevisto." : cause.Message;
                if(detail.Length>1000) detail=detail.Substring(0,1000)+"…";
                string text="Frociart "+typeof(StartupDiagnostics).Assembly.GetName().Version.ToString(3)+"\n\n"+title+".\n\n"+detail+"\n\n";
                if(logPath!=null) text+="Dettagli salvati in:\n"+DisplayLogPath(logPath)+"\n\nPer individuare il problema, condividi questo file o una foto di questa finestra.";
                else text+="Non è stato possibile salvare il dettaglio dell'errore. Per assistenza, condividi una foto di questa finestra.";
                if(!fatal) text+="\n\nSe la finestra risponde, salva il diagramma prima di riavviare.";
                try { Console.Error.WriteLine("Frociart: "+title+". "+SafeException(exception)); } catch { }
                if(Environment.OSVersion.Platform==PlatformID.Win32NT)
                {
                    try { NativeMessageBox(IntPtr.Zero,text,ErrorCaption(fatal),0x00000000u|0x00000010u|0x00040000u); }
                    catch { }
                }
            }
            catch { }
            finally { Interlocked.Exchange(ref reporting,0); }
        }

        internal static string DisplayLogPath(string path){
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return !String.IsNullOrEmpty(local)&&path.StartsWith(local+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)?"%LOCALAPPDATA%"+path.Substring(local.Length):path;
        }
        internal static string SafeException(Exception exception){
            if(exception==null)return "Dettaglio dell'eccezione non disponibile.";
            var text=new StringBuilder();int count=0;
            for(var current=exception;current!=null&&count++<8;current=current.InnerException){
                text.AppendLine(current.GetType().FullName+"; HRESULT=0x"+current.HResult.ToString("X8"));
                // Keep useful method names, without messages, user values,
                // source paths, file names, URL query strings or credentials.
                var frames=new StackTrace(current,false).GetFrames();if(frames!=null)foreach(var frame in frames){var method=frame.GetMethod();if(method!=null)text.AppendLine("  "+(method.DeclaringType==null?"":method.DeclaringType.FullName+".")+method.Name);}
            }
            return text.ToString();
        }

        [DllImport("user32.dll",EntryPoint="MessageBoxW",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern int NativeMessageBox(IntPtr owner,string text,string caption,uint type);
    }
}
