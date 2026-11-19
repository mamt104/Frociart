using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Frociart.LightSetup {
    // All production paths come from the current user's known folders. The
    // parameterized core is internal and is exercised with a distinct QA product.
    internal static class ManagedInstallation {
        internal sealed class Paths {
            internal readonly string Directory,Shortcut,Classes,Application,Registered,RegisteredName,Uninstall,DisplayName;
            internal Paths(string directory,string shortcut,string classes,string application,string registered,string registeredName,string uninstall,string displayName){
                Directory=Path.GetFullPath(directory);Shortcut=Path.GetFullPath(shortcut);Classes=classes;Application=application;Registered=registered;RegisteredName=registeredName;Uninstall=uninstall;DisplayName=displayName;
            }
            internal static Paths CurrentUser(){return new Paths(FileRegistration.InstallDirectory,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Frociart.lnk"),@"Software\Classes",@"Software\Frociart",@"Software\RegisteredApplications","Frociart",@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Frociart.User_is1","Frociart v1");}
        }
        internal static void Install(byte[] payload,string payloadHash,string appHash,string iconHash,string appVersion){
            InstallIntegration.GuardRoot();InstallAt(Paths.CurrentUser(),payload,payloadHash,appHash,iconHash,appVersion);
        }
        internal static void InstallAt(Paths paths,byte[] payload,string payloadHash,string appHash,string iconHash,string appVersion){
            if(payload==null||payload.Length==0||payload.Length>16*1024*1024)throw new SetupProblem("Il pacchetto dell'app è incompleto. Scarica una nuova copia del setup.");
            using(var data=new MemoryStream(payload,false))SetupEngine.VerifyHash(data,payloadHash,false);
            RefuseNewerVersion(paths,appVersion);
            string folder=Path.Combine(Path.GetTempPath(),"Frociart-App-Setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            string installer=Path.Combine(folder,"Frociart.ApplicationPayload.exe");
            try {
                InstallIntegration.WritePayload(installer,payload);
                using(var locked=new FileStream(installer,FileMode.Open,FileAccess.Read,FileShare.Read)){
                    SetupEngine.VerifyHash(locked,payloadHash,false);
                    var start=new ProcessStartInfo(installer,"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /NOFORCECLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /SP-"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder};
                    using(var process=Process.Start(start)){
                        if(process==null)throw new SetupProblem("Windows non ha avviato l'installazione dell'app. Puoi riprovare.");
                        // The installer owns rollback. Never kill it or force a reboot.
                        process.WaitForExit();
                        if(process.ExitCode!=0)throw new SetupProblem("Installazione dell'app non conclusa. Chiudi Frociart e controlla che il collegamento Start non sia stato assegnato a un'altra app, poi riprova. Progetti e impostazioni non vengono rimossi.");
                    }
                }
                VerifyAt(paths,appHash,iconHash);
            }finally{CleanTemporary(folder,installer);}
        }
        internal static void RefuseNewerVersion(Paths paths,string version){
            Version expected;if(!Version.TryParse(version,out expected))throw new SetupProblem("La versione del pacchetto non è valida.");
            string installed=Path.Combine(paths.Directory,"Frociart.exe");if(!File.Exists(installed))return;
            if((File.GetAttributes(installed)&FileAttributes.ReparsePoint)!=0)throw new SetupProblem("Il programma installato non può essere un collegamento a un'altra posizione.");
            var info=FileVersionInfo.GetVersionInfo(installed);Version previous;
            if(!String.Equals(info.ProductName,"Frociart",StringComparison.Ordinal)||!Version.TryParse(info.FileVersion,out previous))throw new SetupProblem("La cartella contiene un programma diverso o incompleto. Non è stato sostituito.");
            if(previous>expected)throw new SetupProblem("È già presente una versione più recente di Frociart. Usa un installer aggiornato.");
        }
        internal static void VerifyAt(Paths paths,string appHash,string iconHash){
            VerifyFile(Path.Combine(paths.Directory,"Frociart.exe"),appHash,"Non riesco a confermare i file installati. Riprova il setup.");
            VerifyFile(Path.Combine(paths.Directory,"Frociart.Document.ico"),iconHash,"Non riesco a confermare l'icona dei progetti. Riprova il setup.");
            ShellLink.Target link;
            try{link=ShellLink.Read(paths.Shortcut);}catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException||error is COMException))throw;throw new SetupProblem("Il collegamento nel menu Start non è disponibile. Riprova il setup.");}
            if(!SamePath(link.Path,Path.Combine(paths.Directory,"Frociart.exe"))||link.Arguments.Length!=0||!SamePath(link.Directory,paths.Directory))throw new SetupProblem("Il collegamento Start non apre questa copia di Frociart. Controllalo e riprova il setup.");
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64)){
                if(Value(root,paths.Uninstall,"DisplayName")!=paths.DisplayName||Value(root,paths.Uninstall,"DisplayVersion")!="v1"||Value(root,paths.Uninstall,"Publisher")!="mamt104"||!SamePath(Value(root,paths.Uninstall,"InstallLocation"),paths.Directory))throw new SetupProblem("La voce di Frociart nelle App installate non è completa. Riprova il setup.");
                string uninstall=Value(root,paths.Uninstall,"UninstallString");
                if(String.IsNullOrEmpty(uninstall)||uninstall.Length<3||uninstall[0]!='"'||uninstall[uninstall.Length-1]!='"')throw new SetupProblem("Il comando di disinstallazione non è completo. Riprova il setup.");
                string executable=uninstall.Substring(1,uninstall.Length-2);
                if(executable.IndexOf('"')>=0||!SamePath(Path.GetDirectoryName(executable),paths.Directory)||!Regex.IsMatch(Path.GetFileName(executable),@"\Aunins[0-9]{3}\.exe\z",RegexOptions.CultureInvariant)||!File.Exists(executable)||!File.Exists(Path.ChangeExtension(executable,"dat"))||(File.GetAttributes(executable)&FileAttributes.ReparsePoint)!=0)throw new SetupProblem("Non riesco a confermare il programma di disinstallazione. Riprova il setup.");
                string command=FileRegistration.Command(Path.Combine(paths.Directory,"Frociart.exe"),false);
                if(Value(root,paths.Classes+@"\Frociart.Diagram\shell\open\command","")!=command||Value(root,paths.Registered,paths.RegisteredName)!=paths.Application+@"\Capabilities"||Value(root,paths.Application+@"\Installation","Engine")!="Frociart.InnoSetup.1")throw new SetupProblem("La configurazione dei progetti non è completa. Riprova il setup.");
            }
        }
        static void VerifyFile(string path,string hash,string message){
            if(!File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new SetupProblem(message);
            try{using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))SetupEngine.VerifyHash(file,hash,false);}catch(IOException){throw new SetupProblem(message);}
        }
        static bool SamePath(string first,string second){try{return !String.IsNullOrEmpty(first)&&String.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase);}catch(ArgumentException){return false;}catch(NotSupportedException){return false;}}
        static string Value(RegistryKey root,string path,string name){using(var key=root.OpenSubKey(path))return key==null?null:key.GetValue(name) as string;}
        static void CleanTemporary(string folder,string installer){
            try {
                string temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,full=Path.GetFullPath(folder);
                if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(Path.GetFileName(full),@"\AFrociart-App-Setup-[a-f0-9]{32}\z",RegexOptions.CultureInvariant)||(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)return;
                if(Path.GetFullPath(installer)!=Path.Combine(full,"Frociart.ApplicationPayload.exe"))return;
                if(File.Exists(installer))File.Delete(installer);
                if(Directory.GetFileSystemEntries(full).Length==0)Directory.Delete(full);
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
