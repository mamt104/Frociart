using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Frociart {
    internal sealed class DocumentLaunch {
        internal const int MaxLinkCharacters=24000;
        internal const string Prefix="frociart://import/v1/";
        internal string Path;
        internal Diagram Imported;
        internal bool SafeMode;
        internal static string LocalPath(string path){
            if(String.IsNullOrWhiteSpace(path)||path.IndexOf('\0')>=0)throw new InvalidDataException("Scegli un file locale Frociart.");
            // Do not probe UNC/device paths: even File.Exists can contact a remote
            // SMB server and disclose Windows network credentials before parsing.
            if(path.StartsWith(@"\\",StringComparison.Ordinal)||path.StartsWith("//",StringComparison.Ordinal)||path.IndexOf("://",StringComparison.Ordinal)>=0)
                throw new InvalidDataException("Apri una copia locale del progetto. I percorsi di rete non vengono caricati automaticamente.");
            string full=System.IO.Path.GetFullPath(path);
            if(full.StartsWith(@"\\",StringComparison.Ordinal))throw new InvalidDataException("Scegli una copia locale del progetto.");
            return full;
        }
        internal static DocumentLaunch Parse(string[] args){
            var result=new DocumentLaunch();if(args==null||args.Length==0)return result;
            int index=0;
            if(args[0]=="--avvio-sicuro"){result.SafeMode=true;index++;if(index==args.Length)return result;}
            bool link=args[index]=="--open-link";
            if(link||args[index]=="--open")index++;
            if(index>=args.Length||index+1!=args.Length)throw new InvalidDataException("Richiesta di apertura non valida. Apri un solo progetto alla volta.");
            string value=args[index];
            if(link){result.Imported=FromLink(value);result.SafeMode=true;}
            else{
                if(value.StartsWith("--",StringComparison.Ordinal))throw new InvalidDataException("Comando di apertura non riconosciuto.");
                result.Path=LocalPath(value);
                string extension=System.IO.Path.GetExtension(result.Path);
                if(!String.Equals(extension,".frociart",StringComparison.OrdinalIgnoreCase)&&!String.Equals(extension,".flow",StringComparison.OrdinalIgnoreCase)&&!String.Equals(extension,".fprg",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Apri un file .frociart, .flow o Flowgorithm .fprg.");
            }
            return result;
        }
        internal static string ToLink(Diagram document){
            string encoded=Convert.ToBase64String(DiagramIO.EncodeDocument(document)).TrimEnd('=').Replace('+','-').Replace('/','_');
            if(encoded.Length+Prefix.Length>MaxLinkCharacters)return null;return Prefix+encoded;
        }
        internal static Diagram FromLink(string value){
            if(value==null||value.Length>MaxLinkCharacters||!value.StartsWith(Prefix,StringComparison.Ordinal))throw new InvalidDataException("Il collegamento Frociart non è valido o è troppo lungo. Scarica e apri il file .frociart.");
            string encoded=value.Substring(Prefix.Length);
            if(encoded.Length==0||!Regex.IsMatch(encoded,@"\A[A-Za-z0-9_-]+\z"))throw new InvalidDataException("Il collegamento contiene dati non validi.");
            encoded=encoded.Replace('-','+').Replace('_','/');encoded+=new string('=',(4-encoded.Length%4)%4);
            byte[] bytes;try{bytes=Convert.FromBase64String(encoded);}catch(FormatException){throw new InvalidDataException("Il collegamento è incompleto.");}
            // Data only. No file URLs, commands, auto-run or remote fetches.
            return DiagramIO.DecodeDocument(bytes);
        }
    }
}
