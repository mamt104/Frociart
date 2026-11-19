using System;
using System.IO;
using System.Reflection;

namespace Frociart {
    internal static class InstallIntegration {
        internal const long MaxApplicationBytes=256L*1024*1024;
        internal static string CurrentExecutable {
            get {
#if NET8_0_OR_GREATER
                string path=Environment.ProcessPath;
#else
                string path=Assembly.GetExecutingAssembly().Location;
#endif
                if(String.IsNullOrEmpty(path))throw new InvalidDataException("Non riesco a individuare il programma da configurare.");
                return Path.GetFullPath(path);
            }
        }
        internal static byte[] Resource(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)throw new InvalidDataException("Il pacchetto non contiene tutti i file necessari.");using(var copy=new MemoryStream()){stream.CopyTo(copy);return copy.ToArray();}}}
        internal static void InstallCurrent(){
            string current=CurrentExecutable;
            byte[] icon=Resource("Frociart.Document.ico");
            GuardRoot();
            CopyCurrent(current,FileRegistration.InstalledExe,icon);
            FileRegistration.Register(FileRegistration.InstalledExe,Path.Combine(FileRegistration.InstallDirectory,"Frociart.Document.ico"));
        }
        internal static void InstallPackage(byte[] application,byte[] icon){
            GuardRoot();WritePayload(FileRegistration.InstalledExe,application);WritePayload(Path.Combine(FileRegistration.InstallDirectory,"Frociart.Document.ico"),icon);
            FileRegistration.Register(FileRegistration.InstalledExe,Path.Combine(FileRegistration.InstallDirectory,"Frociart.Document.ico"));
        }
        internal static void WritePayload(string destination,byte[] bytes){
            if(bytes==null||bytes.Length==0||bytes.Length>16*1024*1024)throw new InvalidDataException("Il pacchetto è incompleto.");
            using(var source=new MemoryStream(bytes,false))WriteStream(destination,source);
        }
        internal static void CopyCurrent(string current,string destination,byte[] icon){
            current=Path.GetFullPath(current);destination=Path.GetFullPath(destination);
            if(!String.Equals(current,destination,StringComparison.OrdinalIgnoreCase)){
                using(var source=new FileStream(current,FileMode.Open,FileAccess.Read,FileShare.Read)){
                    if(source.Length==0||source.Length>MaxApplicationBytes)throw new InvalidDataException("Il programma da configurare è incompleto o supera il limite supportato.");
                    WriteStream(destination,source);
                }
            }
            WritePayload(Path.Combine(Path.GetDirectoryName(destination),"Frociart.Document.ico"),icon);
        }
        private static void WriteStream(string destination,Stream source){
            string folder=Path.GetDirectoryName(Path.GetFullPath(destination));Directory.CreateDirectory(folder);
            if(File.Exists(destination)&&(File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Il file di installazione non può essere un collegamento a un'altra posizione.");
            string temporary=Path.Combine(folder,".frociart-install-"+Guid.NewGuid().ToString("N")+".tmp");
            try{using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){source.CopyTo(file,81920);file.Flush(true);}if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal static void Remove(){
            GuardRoot();
            // Only these two installation files are removed. Project files and
            // learning progress live elsewhere and are never traversed/deleted.
            foreach(string name in new[]{"Frociart.exe","Frociart.Document.ico"}){
                string path=Path.Combine(FileRegistration.InstallDirectory,name);
                if(File.Exists(path))using(var check=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
            }
            FileRegistration.Unregister();
            foreach(string name in new[]{"Frociart.exe","Frociart.Document.ico"}){string path=Path.Combine(FileRegistration.InstallDirectory,name);if(File.Exists(path))File.Delete(path);}
            if(Directory.Exists(FileRegistration.InstallDirectory)&&Directory.GetFileSystemEntries(FileRegistration.InstallDirectory).Length==0)Directory.Delete(FileRegistration.InstallDirectory);
        }
        internal static void GuardRoot(){
            string root=Path.GetFullPath(FileRegistration.InstallDirectory),expected=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Frociart"));
            if(!String.Equals(root,expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Cartella di installazione non valida.");
            foreach(string path in new[]{Path.GetDirectoryName(root),root})if(Directory.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("La cartella di installazione non può essere un collegamento a un'altra posizione.");
        }
    }
}
