using System;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Frociart.LightSetup {
    internal static class RuntimeDetection {
        [StructLayout(LayoutKind.Sequential)]struct SystemInfo {internal ushort Architecture,Reserved;internal uint PageSize;internal IntPtr Minimum,Maximum,Mask;internal uint Processors,Type,Granularity;internal ushort Level,Revision;}
        [DllImport("kernel32.dll")]static extern void GetNativeSystemInfo(out SystemInfo information);
        internal static void ValidatePlatform(){
            SystemInfo information;GetNativeSystemInfo(out information);
            if(information.Architecture!=9)throw new SetupProblem("Questo installer è per PC Windows x64. Su questo dispositivo usa una versione di Frociart adatta al sistema.");
            if(Environment.OSVersion.Version.Major<10)throw new SetupProblem("Questo installer richiede Windows 10 o successivo. Non sono stati installati componenti.");
        }
        internal static bool CompatibleVersion(string text){
            Version version,minimum=new Version(PackageIdentity.RuntimeVersion);return Version.TryParse(text,out version)&&version.Major==minimum.Major&&version.Minor==minimum.Minor&&version.CompareTo(minimum)>=0&&version.Revision<1;
        }
        internal static bool CompatibleRoot(string root){
            if(String.IsNullOrEmpty(root))return false;
            try {
                if(!File.Exists(Path.Combine(root,"dotnet.exe")))return false;
                if(!HasFramework(root,"Microsoft.NETCore.App","coreclr.dll"))return false;
                return HasFramework(root,"Microsoft.WindowsDesktop.App","System.Windows.Forms.dll");
            } catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}
        }
        static bool HasFramework(string root,string framework,string library){
            string folder=Path.Combine(root,"shared",framework);if(!Directory.Exists(folder))return false;
            foreach(string child in Directory.GetDirectories(folder))if(CompatibleVersion(Path.GetFileName(child))&&File.Exists(Path.Combine(child,library))&&File.Exists(Path.Combine(child,framework+".deps.json")))return true;
            return false;
        }
        internal static string RegisteredRoot(){
            using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
            using(var key=machine.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64")){
                string root=key==null?null:key.GetValue("InstallLocation") as string;
                if(!String.IsNullOrEmpty(root))return root;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet");
        }
        internal static bool Present(){return CompatibleRoot(RegisteredRoot());}
        internal static bool Probe(string root){
            if(!CompatibleRoot(root))return false;
            string folder=Path.Combine(Path.GetTempPath(),"Frociart-RuntimeCheck-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try {
                string dll=Path.Combine(folder,"Frociart.RuntimeProbe.dll"),config=Path.Combine(folder,"Frociart.RuntimeProbe.runtimeconfig.json");
                byte[] assembly=InstallIntegration.Resource("Frociart.RuntimeProbe.dll"),configuration=InstallIntegration.Resource("Frociart.RuntimeProbe.runtimeconfig.json");
                using(var stream=new MemoryStream(assembly,false))SetupEngine.VerifyHash(stream,PackageIdentity.ProbeSha256,false);
                using(var stream=new MemoryStream(configuration,false))SetupEngine.VerifyHash(stream,PackageIdentity.ProbeConfigSha256,false);
                File.WriteAllBytes(dll,assembly);File.WriteAllBytes(config,configuration);
                using(var assemblyLock=new FileStream(dll,FileMode.Open,FileAccess.Read,FileShare.Read))using(var configurationLock=new FileStream(config,FileMode.Open,FileAccess.Read,FileShare.Read)){
                    var start=new ProcessStartInfo(Path.Combine(Path.GetFullPath(root),"dotnet.exe"),"exec \""+dll+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=folder};
                    foreach(string name in new[]{"DOTNET_ROOT","DOTNET_ROOT_X64","DOTNET_ROOT_X86","DOTNET_ROLL_FORWARD","DOTNET_ROLL_FORWARD_TO_PRERELEASE","DOTNET_ADDITIONAL_DEPS","DOTNET_SHARED_STORE","DOTNET_STARTUP_HOOKS"})start.EnvironmentVariables.Remove(name);
                    using(var process=Process.Start(start)){
                        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                        if(!process.WaitForExit(15000)){process.Kill();process.WaitForExit();return false;}
                        Task.WaitAll(output,error);return process.ExitCode==0&&output.Result=="FROCIART_RUNTIME_READY"&&String.IsNullOrWhiteSpace(error.Result);
                    }
                }
            }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(System.ComponentModel.Win32Exception){return false;}
            finally {
                foreach(string name in new[]{"Frociart.RuntimeProbe.dll","Frociart.RuntimeProbe.runtimeconfig.json"})try{string path=Path.Combine(folder,name);if(File.Exists(path))File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}
                try{if(Directory.GetFileSystemEntries(folder).Length==0)Directory.Delete(folder);}catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
    }
}
