using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Frociart
{
    /// <summary>Private append journal: one checked frame per successful write, retained across execution changes.</summary>
    public sealed class FlowFileRecovery
    {
        private static readonly byte[] Magic=Encoding.ASCII.GetBytes("FRCIOD01");
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        private readonly string folder;
        private const int MaxJournals=50,MaxJournalBytes=4600000;
        public sealed class Entry
        {
            public string Id {get{return System.IO.Path.GetFileNameWithoutExtension(Path);}}
            public string Name {get;internal set;}
            public DateTime Modified {get;internal set;}
            internal string Path;
        }
        public sealed class Content
        {
            public string Name {get;internal set;}
            public string Text {get;internal set;}
            public bool Incomplete {get;internal set;}
        }
        public FlowFileRecovery(string folder){this.folder=System.IO.Path.GetFullPath(folder);}
        private string[] Paths(){return Directory.Exists(folder)?Directory.GetFiles(folder,"*.fiodraft").Where(p=>GuidName(p)).ToArray():new string[0];}
        private static bool GuidName(string path){Guid id;return Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(path),"N",out id);}
        private bool Owns(Entry entry){return entry!=null&&GuidName(entry.Path)&&String.Equals(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(entry.Path)),folder,StringComparison.OrdinalIgnoreCase);}
        public IList<Entry> List()
        {
            var result=new List<Entry>();
            foreach(string path in Paths().Take(1000))
            {
                string name="File recuperato";
                try{using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){Header(stream);name=Utf8.GetString(ReadFrame(stream,1024));}}
                catch(IOException){}catch(InvalidDataException){}catch(ArgumentException){}
                result.Add(new Entry{Name=name,Path=path,Modified=File.GetLastWriteTimeUtc(path)});
            }
            return result.OrderByDescending(e=>e.Modified).ToList().AsReadOnly();
        }
        public FlowTextFiles.IJournal Create(string name)
        {
            Directory.CreateDirectory(folder);if(Paths().Length>=MaxJournals)throw new IOException("Sono presenti 50 file da recuperare. Salvali o rimuovili da File generati.");
            string path=System.IO.Path.Combine(folder,Guid.NewGuid().ToString("N")+".fiodraft");
            return new Journal(path,name);
        }
        public Content Load(Entry entry)
        {
            if(!Owns(entry))throw new IOException("File di recupero non valido.");
            using(var stream=File.Open(entry.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            {
                if(stream.Length>MaxJournalBytes)throw new IOException("Il file di recupero supera il limite previsto.");
                Header(stream);string name=Utf8.GetString(ReadFrame(stream,1024));var text=new StringBuilder();int totalBytes=0;bool incomplete=false;
                while(stream.Position<stream.Length)
                {
                    try{byte[] bytes=ReadFrame(stream,FlowTextFiles.MaxBytes);string chunk=Utf8.GetString(bytes);if(text.Length+(long)chunk.Length>FlowTextFiles.MaxCharacters||totalBytes+(long)bytes.Length>FlowTextFiles.MaxBytes)throw new InvalidDataException("Limite della bozza superato.");text.Append(chunk);totalBytes+=bytes.Length;}
                    catch(IOException){incomplete=true;break;}catch(InvalidDataException){incomplete=true;break;}catch(ArgumentException){incomplete=true;break;}
                }
                return new Content{Name=name,Text=text.ToString(),Incomplete=incomplete};
            }
        }
        public void Delete(Entry entry){if(!Owns(entry))throw new IOException("File di recupero non valido.");File.Delete(entry.Path);}
        private static void Header(Stream stream){byte[] b=ReadExact(stream,8);if(!b.SequenceEqual(Magic))throw new InvalidDataException("Formato di recupero sconosciuto.");}
        private static byte[] ReadExact(Stream stream,int count){byte[] bytes=new byte[count];int read=0;while(read<count){int n=stream.Read(bytes,read,count-read);if(n==0)throw new EndOfStreamException("Scrittura interrotta.");read+=n;}return bytes;}
        private static byte[] ReadFrame(Stream stream,int max)
        {
            byte[] size=ReadExact(stream,4);int length=size[0]|size[1]<<8|size[2]<<16|size[3]<<24;if(length<0||length>max)throw new InvalidDataException("Dimensione del frammento non valida.");
            byte[] expected=ReadExact(stream,32),data=ReadExact(stream,length);using(var hash=SHA256.Create())if(!hash.ComputeHash(data).SequenceEqual(expected))throw new InvalidDataException("Frammento incompleto.");return data;
        }
        private static byte[] Frame(string text)
        {
            byte[] data=Utf8.GetBytes(text),result=new byte[36+data.Length];int n=data.Length;for(int i=0;i<4;i++)result[i]=(byte)(n>>(8*i));using(var hash=SHA256.Create())Buffer.BlockCopy(hash.ComputeHash(data),0,result,4,32);Buffer.BlockCopy(data,0,result,36,data.Length);return result;
        }
        private sealed class Journal:FlowTextFiles.IJournal
        {
            public string Id {get{return System.IO.Path.GetFileNameWithoutExtension(path);}}
            private FileStream stream;private readonly string path;
            internal Journal(string path,string name)
            {
                this.path=path;stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                try{stream.Write(Magic,0,Magic.Length);Append(name);}catch{stream.Dispose();stream=null;try{File.Delete(path);}catch(IOException){}throw;}
            }
            public void Append(string text)
            {
                if(stream==null)throw new IOException("La bozza non è più aperta.");byte[] bytes=Frame(text);long start=stream.Position;
                try{stream.Write(bytes,0,bytes.Length);stream.Flush();}catch{try{stream.SetLength(start);stream.Position=start;stream.Flush();}catch(IOException){}throw;}
            }
            public void Close(){if(stream!=null){try{stream.Dispose();}finally{stream=null;}}}
            public bool Saved(){try{Close();File.Delete(path);return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
        }
    }
}
