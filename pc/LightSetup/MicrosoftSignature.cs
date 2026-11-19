using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Frociart.LightSetup {
    internal static class MicrosoftSignature {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct FileInfo {internal uint Size;[MarshalAs(UnmanagedType.LPWStr)]internal string Path;internal IntPtr File,KnownSubject;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct TrustData {internal uint Size;internal IntPtr Policy,Sip;internal uint Ui,Revocation,Choice;internal IntPtr File;internal uint StateAction;internal IntPtr State,Url;internal uint Flags,Context;internal IntPtr SignatureSettings;}
        [DllImport("wintrust.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
        internal static void Verify(string path){
            var information=new FileInfo{Size=(uint)Marshal.SizeOf(typeof(FileInfo)),Path=path};
            IntPtr pointer=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo)));Marshal.StructureToPtr(information,pointer,false);
            var data=new TrustData{Size=(uint)Marshal.SizeOf(typeof(TrustData)),Ui=2,Revocation=1,Choice=1,File=pointer,StateAction=1,Flags=0x80};
            Guid action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try {
                if(WinVerifyTrust(new IntPtr(-1),ref action,ref data)!=0)throw new SetupProblem("Windows non ha confermato la firma del componente Microsoft. Nessun file è stato eseguito. Controlla la connessione e riprova.");
                using(var certificate=new X509Certificate2(X509Certificate.CreateFromSignedFile(path))){
                    if(!MicrosoftPublisher(certificate))throw new SetupProblem("Il componente non ha la firma Microsoft prevista. Nessun file è stato eseguito.");
                }
            } finally {data.StateAction=2;WinVerifyTrust(new IntPtr(-1),ref action,ref data);Marshal.DestroyStructure(pointer,typeof(FileInfo));Marshal.FreeHGlobal(pointer);}
        }
        internal static bool MicrosoftPublisher(X509Certificate2 certificate){
            string name=certificate.GetNameInfo(X509NameType.SimpleName,false);
            if(name!="Microsoft Corporation"&&name!=".NET")return false;
            string decoded=certificate.SubjectName.Decode(X500DistinguishedNameFlags.UseNewLines);
            int organizations=0;
            foreach(string line in decoded.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))if(line.Trim().StartsWith("O=",StringComparison.Ordinal)){
                if(line.Trim()!="O=Microsoft Corporation")return false;organizations++;
            }
            return organizations==1;
        }
    }
}
