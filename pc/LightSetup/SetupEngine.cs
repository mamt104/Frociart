using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Frociart.LightSetup {
    internal enum SetupStage { Checking, Downloading, Verifying, InstallingRuntime, InstallingApp, Complete, RestartRequired }
    internal sealed class SetupProgress {
        internal readonly SetupStage Stage; internal readonly long Received,Total;
        internal SetupProgress(SetupStage stage,long received=0,long total=0){Stage=stage;Received=received;Total=total;}
        internal bool CanCancel {get{return Stage==SetupStage.Checking||Stage==SetupStage.Downloading||Stage==SetupStage.Verifying;}}
    }
    internal sealed class SetupProblem:Exception {
        internal SetupProblem(string message):base(message){}
    }
    internal sealed class SetupControl:IDisposable {
        readonly object gate=new object();readonly CancellationTokenSource source=new CancellationTokenSource();bool cancellable=true;
        internal CancellationToken Token {get{return source.Token;}}
        internal bool TryCancel(){lock(gate){if(!cancellable)return false;source.Cancel();return true;}}
        internal void BeginCommit(){lock(gate){source.Token.ThrowIfCancellationRequested();cancellable=false;}}
        public void Dispose(){source.Dispose();}
    }
    // The production host has no command-line/environment bypasses. Tests supply
    // a different host in their own executable; no test runtime is distributed.
    internal interface ISetupHost {
        void ValidatePlatform();
        bool RuntimePresent();
        void ValidatePayload();
        string CreateTemporaryFolder();
        void Download(string path,Action<long,long> progress,CancellationToken cancellation);
        void VerifyDependency(string path,Stream lockedFile);
        int InstallDependency(string path);
        void InstallApplication();
        void CleanTemporaryFolder(string folder);
    }
    internal sealed class SetupEngine {
        readonly ISetupHost host;
        internal SetupEngine(ISetupHost host){this.host=host;}
        internal SetupStage Run(Action<SetupProgress> progress,SetupControl control){
            string folder=null;CancellationToken cancellation=control.Token;
            try {
                progress(new SetupProgress(SetupStage.Checking));cancellation.ThrowIfCancellationRequested();
                host.ValidatePlatform();host.ValidatePayload();
                if(!host.RuntimePresent()){
                    cancellation.ThrowIfCancellationRequested();folder=host.CreateTemporaryFolder();
                    string path=Path.Combine(folder,"MicrosoftDesktopRuntime.exe");
                    progress(new SetupProgress(SetupStage.Downloading));
                    host.Download(path,(received,total)=>progress(new SetupProgress(SetupStage.Downloading,received,total)),cancellation);
                    cancellation.ThrowIfCancellationRequested();progress(new SetupProgress(SetupStage.Verifying));
                    // Deny writing/replacement from hash verification until the
                    // Microsoft installer has exited. Never execute a partial file.
                    using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
                        host.VerifyDependency(path,file);cancellation.ThrowIfCancellationRequested();
                        control.BeginCommit();progress(new SetupProgress(SetupStage.InstallingRuntime));
                        int result=host.InstallDependency(path);
                        if(result==3010||result==1641){progress(new SetupProgress(SetupStage.RestartRequired));return SetupStage.RestartRequired;}
                        if(result==1602)throw new SetupProblem("Installazione del componente annullata. Frociart non è stato aggiornato. Puoi riprovare.");
                        if(result==1618)throw new SetupProblem("Windows sta completando un'altra installazione. Attendi e riprova.");
                        if(result!=0)throw new SetupProblem("Microsoft non ha completato l'installazione del componente (codice "+result+"). Frociart non è stato aggiornato. Puoi riprovare.");
                    }
                    if(!host.RuntimePresent())throw new SetupProblem("Il componente Microsoft non è ancora disponibile. Riavvia Windows e riapri questo installer. Frociart non è stato aggiornato.");
                }
                control.BeginCommit();progress(new SetupProgress(SetupStage.InstallingApp));
                host.InstallApplication();progress(new SetupProgress(SetupStage.Complete));return SetupStage.Complete;
            } finally {if(folder!=null)host.CleanTemporaryFolder(folder);}
        }
        internal static string Hash(Stream stream,HashAlgorithm algorithm){using(algorithm)return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        internal static void VerifyHash(Stream stream,string expected,bool sha512){
            string actual=Hash(stream,sha512?(HashAlgorithm)SHA512.Create():SHA256.Create());
            if(!String.Equals(actual,expected,StringComparison.OrdinalIgnoreCase))throw new SetupProblem("Il file scaricato o il pacchetto è incompleto o non corrisponde all'originale. Non è stato eseguito. Scarica una nuova copia e riprova.");
        }
    }
}
