using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Frociart {
    internal static class FileRegistration {
        internal const string ProgId="Frociart.Diagram";
        internal const string Protocol="frociart";
        internal static string InstallDirectory {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Frociart");}}
        internal static string InstalledExe {get{return Path.Combine(InstallDirectory,"Frociart.exe");}}
        internal static string Command(string exe,bool protocol){return "\""+Path.GetFullPath(exe)+"\" "+(protocol?"--open-link":"--open")+" \"%1\"";}
        [DllImport("shell32.dll")]static extern void SHChangeNotify(uint change,uint flags,IntPtr first,IntPtr second);
        internal static void Register(string exe,string icon){RegisterAt(Registry.CurrentUser,@"Software\Classes",@"Software\Frociart",exe,icon);SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);}
        internal static void RegisterAt(RegistryKey root,string classes,string application,string exe,string icon){
            exe=Path.GetFullPath(exe);icon=Path.GetFullPath(icon);
            if(exe.IndexOf('"')>=0||icon.IndexOf('"')>=0)throw new InvalidDataException("Percorso di installazione non valido.");
            Set(root,classes+"\\"+ProgId,"","Progetto Frociart");Set(root,classes+"\\"+ProgId+@"\DefaultIcon","","\""+icon+"\",0");Set(root,classes+"\\"+ProgId+@"\shell\open\command","",Command(exe,false));
            // Register this private extension without changing UserChoice or any
            // association the user has explicitly assigned to a different app.
            string ext=classes+@"\.frociart";
            using(var key=root.CreateSubKey(ext)){string current=key.GetValue("") as string;if(String.IsNullOrEmpty(current)||current==ProgId)key.SetValue("",ProgId);key.SetValue("Content Type","application/x-frociart");}
            Set(root,ext+@"\OpenWithProgids",ProgId,"");
            Set(root,classes+@"\Applications\Frociart.exe","FriendlyAppName","Frociart");Set(root,classes+@"\Applications\Frociart.exe\SupportedTypes",".frociart","");Set(root,classes+@"\Applications\Frociart.exe\shell\open\command","",Command(exe,false));
            Set(root,classes+"\\"+Protocol,"","URL:Frociart Project");Set(root,classes+"\\"+Protocol,"URL Protocol","");Set(root,classes+"\\"+Protocol+@"\DefaultIcon","","\""+icon+"\",0");Set(root,classes+"\\"+Protocol+@"\shell\open\command","",Command(exe,true));
            Set(root,application+@"\Capabilities","ApplicationName","Frociart");Set(root,application+@"\Capabilities","ApplicationDescription","Crea e apri diagrammi di flusso sul computer.");Set(root,application+@"\Capabilities\FileAssociations",".frociart",ProgId);Set(root,application+@"\Capabilities\URLAssociations",Protocol,Protocol);
            Set(root,@"Software\RegisteredApplications","Frociart",application+@"\Capabilities");
        }
        internal static void Unregister(){UnregisterAt(Registry.CurrentUser,@"Software\Classes",@"Software\Frociart",InstalledExe);SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);}
        internal static void UnregisterAt(RegistryKey root,string classes,string application,string exe){
            if(Value(root,classes+"\\"+ProgId+@"\shell\open\command","")!=Command(exe,false))return;
            using(var ext=root.OpenSubKey(classes+@"\.frociart",true)){if(ext!=null&&Equals(ext.GetValue(""),ProgId))ext.DeleteValue("",false);}
            using(var choices=root.OpenSubKey(classes+@"\.frociart\OpenWithProgids",true)){if(choices!=null)choices.DeleteValue(ProgId,false);}
            root.DeleteSubKeyTree(classes+"\\"+ProgId,false);
            if(Value(root,classes+"\\"+Protocol+@"\shell\open\command","")==Command(exe,true))root.DeleteSubKeyTree(classes+"\\"+Protocol,false);
            if(Value(root,classes+@"\Applications\Frociart.exe\shell\open\command","")==Command(exe,false))root.DeleteSubKeyTree(classes+@"\Applications\Frociart.exe",false);
            if(Value(root,@"Software\RegisteredApplications","Frociart")==application+@"\Capabilities")using(var key=root.OpenSubKey(@"Software\RegisteredApplications",true)){if(key!=null)key.DeleteValue("Frociart",false);}
            root.DeleteSubKeyTree(application+@"\Capabilities",false);
        }
        static void Set(RegistryKey root,string path,string name,string value){using(var key=root.CreateSubKey(path))key.SetValue(name,value,RegistryValueKind.String);}
        static string Value(RegistryKey root,string path,string name){using(var key=root.OpenSubKey(path))return key==null?null:key.GetValue(name) as string;}
    }
}
