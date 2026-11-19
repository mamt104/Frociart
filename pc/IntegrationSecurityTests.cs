using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Frociart.Tests {
static class IntegrationSecurityTests {
    static int count;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
    static void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
    static string Value(RegistryKey root,string path,string name=""){using(var key=root.OpenSubKey(path))return key==null?null:key.GetValue(name) as string;}
    static bool Exists(RegistryKey root,string path){using(var key=root.OpenSubKey(path))return key!=null;}
    [STAThread]static int Main(){try{
        Application.EnableVisualStyles();
        foreach(var example in Examples.All()){
            string link=DocumentLaunch.ToLink(example);Check(link!=null&&link.StartsWith(DocumentLaunch.Prefix),"Built-in project has a bounded app link: "+example.Title);
            var actual=DocumentLaunch.FromLink(link);Check(Convert.ToBase64String(DiagramIO.EncodeDocument(actual))==Convert.ToBase64String(DiagramIO.EncodeDocument(example)),"App link roundtrip preserves exact model: "+example.Title);
        }
        var unicode=Examples.Parity();unicode.Title="</script><img src=x onerror=alert(1)> Ω 👩‍💻";unicode.Nodes[1].Prompt="Caffè € e dati privati";
        var imported=DocumentLaunch.Parse(new[]{"--open-link",DocumentLaunch.ToLink(unicode)});Check(imported.Path==null&&imported.SafeMode&&imported.Imported.Title==unicode.Title,"Browser import remains data and bypasses automatic recovery");
        foreach(string attack in new[]{"https://example.invalid/project.frociart","file:///C:/Windows/win.ini","frociart://open?file=C:/Windows/win.ini","frociart://import/v1/AAA%22%20--configure-files","frociart://import/v1/../../Windows","frociart://import/v1/","frociart://import/v1/!!!!",DocumentLaunch.Prefix+new string('a',DocumentLaunch.MaxLinkCharacters)})Reject(()=>DocumentLaunch.FromLink(attack),"Reject non-data or malformed link before opening a file");
        Reject(()=>DocumentLaunch.Parse(new[]{"--open-link",DocumentLaunch.ToLink(unicode),"--configure-files"}),"Additional command-line arguments cannot escape the protocol request");
        Reject(()=>DocumentLaunch.Parse(new[]{"--unknown"}),"Unknown command-line switches are not treated as file paths");
        foreach(string path in new[]{@"\\server.invalid\share\test.frociart",@"\\?\UNC\server.invalid\share\test.frociart",@"\\.\pipe\test", "//server.invalid/share/test.frociart","file://server.invalid/share/test.frociart","https://example.invalid/test.frociart"}){
            Reject(()=>DocumentLaunch.LocalPath(path),"Reject remote/device path without probing its existence");Reject(()=>DiagramIO.Load(path),"Project loader rejects remote/device path before FileStream");
        }
        string root=Environment.GetEnvironmentVariable("REVIEW_OUTPUT");Directory.CreateDirectory(root);string local=Path.Combine(root,"A & B con spazi.frociart");DiagramIO.Save(local,unicode);
        Check(DocumentLaunch.Parse(new[]{"--open",local}).Path==Path.GetFullPath(local)&&DiagramIO.Load(local).Title==unicode.Title,"Local file with spaces and shell metacharacters opens as data");
        Check(DocumentLaunch.Parse(new[]{"--avvio-sicuro",local}).SafeMode,"Legacy safe-mode file invocation remains supported");
        Reject(()=>DocumentLaunch.Parse(new[]{"--open",Path.Combine(root,"program.exe")}),"File association never executes arbitrary extensions");
        Reject(()=>DiagramIO.DecodeDocument(new byte[0]),"Empty imported document is rejected");Reject(()=>DiagramIO.DecodeDocument(new byte[DiagramIO.MaxFileBytes+1]),"Oversized imported bytes are rejected before parsing");
        var large=Examples.Parity();large.Nodes[1].Prompt=new string('x',1024);foreach(var node in large.Nodes.Where(n=>n.Kind!=NodeKind.Start&&n.Kind!=NodeKind.End))node.Text=new string('x',8192);Check(DocumentLaunch.ToLink(large)==null,"Large projects use native files instead of overlong shell links");
        string sentinel="PRIVATE-SENTINEL-token-example-local-user-path";Exception error;try{throw new IOException(sentinel);}catch(Exception ex){error=ex;}
        string safe=StartupDiagnostics.SafeException(error);Check(!safe.Contains(sentinel)&&safe.Contains("System.IO.IOException")&&safe.Contains("HRESULT"),"Diagnostic record preserves error type/code without private message data");
        Check(!StartupDiagnostics.SafeException(new Exception(sentinel,new Exception(sentinel))).Contains(sentinel),"Inner-exception messages are excluded from diagnostics");
        string localData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);Check(StartupDiagnostics.DisplayLogPath(Path.Combine(localData,"Frociart","test.txt")).StartsWith("%LOCALAPPDATA%"),"Displayed diagnostic location does not expose the Windows user name");
        string payload=Path.Combine(root,"install-payload.exe");byte[] first={1,2,3},second={4,5,6,7};InstallIntegration.WritePayload(payload,first);InstallIntegration.WritePayload(payload,second);Check(File.ReadAllBytes(payload).SequenceEqual(second),"Installer writes and replaces payload atomically");
        Reject(()=>InstallIntegration.WritePayload(payload,new byte[0]),"Installer rejects empty payload before changing installed bytes");Check(File.ReadAllBytes(payload).SequenceEqual(second),"Rejected install preserves the prior payload");
        byte[] iconBytes=InstallIntegration.Resource("Frociart.Document.ico");using(var icon=new Icon(new MemoryStream(iconBytes),new Size(32,32))){Check(icon.Width==32&&icon.Height==32,"Embedded project icon decodes at shell size");}
        Check(FileRegistration.Command(local,false)=="\""+Path.GetFullPath(local)+"\" --open \"%1\"","Association quotes both executable and document arguments");
        string registryPath=@"Software\Frociart\IntegrationQA-"+Guid.NewGuid().ToString("N");
        try{using(var sandbox=Registry.CurrentUser.CreateSubKey(registryPath)){
            string exe=Path.Combine(root,"Frociart app.exe"),icon=Path.Combine(root,"Project icon.ico");
            FileRegistration.RegisterAt(sandbox,"Classes","Product",exe,icon);
            Check(Value(sandbox,@"Classes\.frociart")==FileRegistration.ProgId,"Native registry associates new private extension");
            Check(Value(sandbox,@"Classes\Frociart.Diagram\DefaultIcon")=="\""+Path.GetFullPath(icon)+"\",0","Native registry has the document-specific icon");
            Check(Value(sandbox,@"Classes\Frociart.Diagram\shell\open\command")==FileRegistration.Command(exe,false),"Native registry opens documents with the explicit data-only switch");
            Check(Value(sandbox,@"Classes\frociart\shell\open\command")==FileRegistration.Command(exe,true)&&Value(sandbox,@"Classes\frociart","URL Protocol")=="","Native registry registers the bounded browser protocol");
            Check(Value(sandbox,@"Classes\Applications\Frociart.exe\SupportedTypes",".frociart")=="","Application appears as a supported Open With choice");
            using(var extension=sandbox.OpenSubKey(@"Classes\.frociart",true))extension.SetValue("","AnotherApp.Project");
            using(var choice=sandbox.CreateSubKey(@"Classes\.frociart\UserChoice"))choice.SetValue("Sentinel",sentinel);
            FileRegistration.RegisterAt(sandbox,"Classes","Product",exe,icon);
            Check(Value(sandbox,@"Classes\.frociart")=="AnotherApp.Project"&&Value(sandbox,@"Classes\.frociart\UserChoice","Sentinel")==sentinel,"Registration preserves other defaults and never edits protected user choice");
            FileRegistration.UnregisterAt(sandbox,"Classes","Product",exe+".other");Check(Exists(sandbox,@"Classes\Frociart.Diagram"),"Unregister refuses to remove another executable's registration");
            using(var protocol=sandbox.OpenSubKey(@"Classes\frociart\shell\open\command",true))protocol.SetValue("","Another app");
            FileRegistration.UnregisterAt(sandbox,"Classes","Product",exe);
            Check(!Exists(sandbox,@"Classes\Frociart.Diagram")&&Value(sandbox,@"Classes\.frociart")=="AnotherApp.Project","Unregister removes own class while preserving a later extension choice");
            Check(Value(sandbox,@"Classes\frociart\shell\open\command")=="Another app"&&Value(sandbox,@"Classes\.frociart\UserChoice","Sentinel")==sentinel,"Unregister preserves later protocol owner and user metadata");
        }}finally{Registry.CurrentUser.DeleteSubKeyTree(registryPath,false);}
        Check(!Exists(Registry.CurrentUser,registryPath),"Registry QA subtree is removed after the test; real associations untouched");
        Console.WriteLine("INTEGRATION_SECURITY_PASS checks="+count+"; real HKCU isolated subtree, local atomic files, native icon and bounded links; physical shell double click NOT_RUN here");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
}
