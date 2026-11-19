using System;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;

namespace Frociart.LightSetup {
    internal sealed class WindowsSetupHost:ISetupHost {
        byte[] icon,managedPayload;
        public void ValidatePlatform(){RuntimeDetection.ValidatePlatform();}
        public bool RuntimePresent(){return RuntimeDetection.Probe(RuntimeDetection.RegisteredRoot());}
        public void ValidatePayload(){
            icon=InstallIntegration.Resource("Frociart.Document.ico");
            using(var file=new MemoryStream(icon,false))SetupEngine.VerifyHash(file,PackageIdentity.IconSha256,false);
            managedPayload=InstallIntegration.Resource("Frociart.ManagedPayload.exe");
            using(var file=new MemoryStream(managedPayload,false))SetupEngine.VerifyHash(file,PackageIdentity.ManagedPayloadSha256,false);
        }
        public string CreateTemporaryFolder(){
            string folder=Path.Combine(Path.GetTempPath(),"Frociart-Setup-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);return folder;
        }
        public void Download(string path,Action<long,long> progress,CancellationToken cancellation){MicrosoftDownload.Download(PackageIdentity.RuntimeUrl,path,progress,cancellation);}
        public void VerifyDependency(string path,Stream file){SetupEngine.VerifyHash(file,PackageIdentity.RuntimeSha512,true);MicrosoftSignature.Verify(path);}
        public int InstallDependency(string path){
            try {using(var process=Process.Start(new ProcessStartInfo(path,"/install /passive /norestart"){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(path)})){
                if(process==null)throw new SetupProblem("Windows non ha avviato l'installazione Microsoft. Puoi riprovare.");
                // Do not abort or kill an installer after it has begun changing
                // system components. Microsoft handles its own cancel/recovery.
                process.WaitForExit();return process.ExitCode;
            }}catch(Win32Exception error){if(error.NativeErrorCode==1223)throw new SetupProblem("Autorizzazione Windows annullata. Frociart non è stato aggiornato. Puoi riprovare.");throw;}
        }
        public void InstallApplication(){ManagedInstallation.Install(managedPayload,PackageIdentity.ManagedPayloadSha256,PackageIdentity.ApplicationSha256,PackageIdentity.IconSha256,PackageIdentity.ApplicationVersion);}
        public void CleanTemporaryFolder(string folder){
            // Never traverse a directory or remove anything other than the one
            // owned, fixed-name download. A failed cleanup must not fake a failed
            // installation that has already completed.
            try {string expected=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                string full=Path.GetFullPath(folder);if(!full.StartsWith(expected,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("Frociart-Setup-",StringComparison.Ordinal))return;
                if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)return;
                string path=Path.Combine(full,"MicrosoftDesktopRuntime.exe");if(File.Exists(path))File.Delete(path);
                if(Directory.GetFileSystemEntries(full).Length==0)Directory.Delete(full);
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
