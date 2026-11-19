using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading;

namespace Frociart.LightSetup {
    internal static class MicrosoftDownload {
        internal const long MaximumBytes=128L*1024*1024;
        internal static bool Allowed(Uri uri){return uri!=null&&uri.Scheme=="https"&&uri.Host=="builds.dotnet.microsoft.com"&&uri.IsDefaultPort&&String.IsNullOrEmpty(uri.UserInfo)&&String.IsNullOrEmpty(uri.Query)&&String.IsNullOrEmpty(uri.Fragment)&&uri.AbsolutePath.StartsWith("/dotnet/WindowsDesktop/",StringComparison.Ordinal);}
        internal static void Download(string address,string destination,Action<long,long> progress,CancellationToken cancellation){
            Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri)||!Allowed(uri))throw new SetupProblem("L'indirizzo del componente Microsoft non è valido. Scarica una nuova copia dell'installer.");
            ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
            var request=(HttpWebRequest)WebRequest.Create(uri);
            request.AllowAutoRedirect=false;request.Timeout=30000;request.ReadWriteTimeout=30000;
            request.UserAgent="Frociart-Setup/1";request.AutomaticDecompression=DecompressionMethods.None;
            using(var abort=cancellation.Register(request.Abort))
            using(var deadline=new Timer(_=>request.Abort(),null,180000,Timeout.Infinite)){
                try {
                    using(var response=(HttpWebResponse)request.GetResponse()){
                        if(response.StatusCode!=HttpStatusCode.OK||response.ResponseUri!=uri)throw new SetupProblem("Il server Microsoft non ha restituito il componente previsto. Nessun file è stato eseguito. Riprova più tardi.");
                        using(var input=response.GetResponseStream())using(var file=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                            Copy(input,file,response.ContentLength,progress,cancellation);file.Flush(true);
                        }
                    }
                }catch(WebException){cancellation.ThrowIfCancellationRequested();throw new SetupProblem("Download Microsoft non riuscito. Controlla la connessione e riprova, oppure usa il pacchetto completo offline. Nessun file scaricato è stato eseguito.");}
                catch(IOException){cancellation.ThrowIfCancellationRequested();throw new SetupProblem("Download non completato. Controlla la connessione e lo spazio disponibile, poi riprova oppure usa il pacchetto completo offline. Nessun file scaricato è stato eseguito.");}
            }
        }
        internal static void Copy(Stream input,Stream output,long length,Action<long,long> progress,CancellationToken cancellation){
            if(length==0||length>MaximumBytes)throw new SetupProblem("La dimensione del download non è valida. Nessun file è stato eseguito.");
            byte[] buffer=new byte[81920];long received=0;int count;var update=Stopwatch.StartNew();
            progress(0,length);
            while(true){cancellation.ThrowIfCancellationRequested();count=input.Read(buffer,0,buffer.Length);if(count==0)break;received+=count;
                if(received>MaximumBytes||(length>=0&&received>length))throw new SetupProblem("Il download supera la dimensione prevista. Nessun file è stato eseguito.");
                output.Write(buffer,0,count);if(update.ElapsedMilliseconds>=80){progress(received,length);update.Restart();}
            }
            cancellation.ThrowIfCancellationRequested();
            if(received==0||(length>=0&&received!=length))throw new SetupProblem("Il download è stato interrotto prima del completamento. Nessun file è stato eseguito. Riprova.");
            progress(received,length);
        }
    }
}
