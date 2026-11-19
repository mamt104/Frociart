using System;
using System.IO;
using System.Text;

namespace Frociart
{
    public enum FlowFileRequestKind { Read, Save }

    // A request is a capability for one suspended block, never an arbitrary filesystem path.
    public sealed class FlowFileRequest
    {
        public FlowFileRequestKind Kind { get; private set; }
        public string Name { get; private set; }
        public string Text { get; private set; }
        internal FlowFileRequest(FlowFileRequestKind kind,string name,string text=null){Kind=kind;Name=name;Text=text;}
    }

    /// <summary>Two program-wide channels. Output remains a recoverable draft until the host confirms a save.</summary>
    public sealed class FlowTextFiles
    {
        public interface IJournal { string Id {get;}void Append(string text);void Close();bool Saved(); }
        public string ActiveJournalId {get{return journal==null?null:journal.Id;}}
        public Func<string,IJournal> JournalFactory {get;set;}
        private IJournal journal;
        public bool RecoveryCopyRemains {get;private set;}
        public void CloseJournal(){if(journal!=null){journal.Close();journal=null;}}
        public const int MaxBytes=4194304, MaxCharacters=2097152, MaxLine=32768;
        private string input;
        private int position;
        private StringBuilder output;
        private int outputBytes;
        public string ReadName {get;private set;}
        public string WriteName {get;private set;}
        public bool HasRead {get{return input!=null;}}
        public bool HasWrite {get{return output!=null;}}
        public string OutputDraft {get{return output==null?null:output.ToString();}}
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);

        public bool Eof(){RequireRead();return position>=input.Length;}
        private void RequireRead(){if(!HasRead)throw new FlowException("Apri prima un file in lettura. EOF() controlla quel file.");}
        internal static string Name(object value)
        {
            var name=value as string;
            if(String.IsNullOrWhiteSpace(name)||name.Length>256)throw new FlowException("Il nome del file deve essere un testo da 1 a 256 caratteri.");
            foreach(char c in name)if(Char.IsControl(c))throw new FlowException("Il nome del file non può contenere righe nuove o caratteri di controllo.");
            ValidateText(name);
            return name;
        }
        internal void OpenRead(string name,string text)
        {
            if(HasRead)throw new FlowException("Un file è già aperto in lettura. Chiudilo prima di aprirne un altro.");
            ValidateText(text);input=text;position=0;ReadName=name;
        }
        internal void OpenWrite(string name)
        {
            if(HasWrite)throw new FlowException("Un file è già aperto in scrittura. Chiudilo e salvalo prima di aprirne un altro.");
            try{journal=JournalFactory==null?null:JournalFactory(name);}catch(IOException){throw new FlowException("Non riesco a preparare la bozza locale. Controlla lo spazio disponibile e File generati, poi riprova.");}catch(UnauthorizedAccessException){throw new FlowException("Non riesco a creare la bozza locale. Controlla l'accesso alla cartella di Frociart.");}
            RecoveryCopyRemains=false;
            output=new StringBuilder();outputBytes=0;WriteName=name;
        }
        public string PeekLine()
        {
            RequireRead();if(Eof())throw new FlowException("Il file è terminato. Usa non EOF() nella condizione del ciclo prima di leggere.");
            int end=position;while(end<input.Length&&input[end]!='\r'&&input[end]!='\n'){if(end-position>=MaxLine)throw new FlowException("Una riga del file supera 32768 caratteri.");end++;}
            return input.Substring(position,end-position);
        }
        internal void CommitRead()
        {
            string line=PeekLine();position+=line.Length;
            if(position<input.Length&&input[position]=='\r'){position++;if(position<input.Length&&input[position]=='\n')position++;}
            else if(position<input.Length&&input[position]=='\n')position++;
        }
        internal void Write(string text,bool newline)
        {
            if(!HasWrite)throw new FlowException("Apri prima un file in scrittura.");
            text=(text??"")+(newline?"\n":"");int bytes;
            try{bytes=Utf8.GetByteCount(text);}catch(EncoderFallbackException){throw new FlowException("Il file contiene caratteri Unicode non validi.");}
            if(output.Length+(long)text.Length>MaxCharacters||outputBytes+(long)bytes>MaxBytes)throw new FlowException("Il file generato supera il limite di 4 MiB o 2 milioni di caratteri.");
            try{if(journal!=null)journal.Append(text);}catch(IOException){throw new FlowException("Scrittura interrotta: non riesco a conservare la bozza locale. Il testo precedente resta in File generati.");}catch(UnauthorizedAccessException){throw new FlowException("Non riesco ad aggiornare la bozza locale. Il testo precedente resta in File generati.");}
            output.Append(text);outputBytes+=bytes;
        }
        internal void CloseRead(){RequireRead();input=null;position=0;ReadName=null;}
        internal void Saved(){if(journal!=null){RecoveryCopyRemains=!journal.Saved();journal=null;}output=null;outputBytes=0;WriteName=null;}
        internal void Reset(){CloseJournal();input=null;position=0;ReadName=null;output=null;outputBytes=0;WriteName=null;RecoveryCopyRemains=false;}
        private static void ValidateText(string text)
        {
            if(text==null)throw new FlowException("Il file di testo non è disponibile.");
            if(text.Length>MaxCharacters)throw new FlowException("Il file supera 2 milioni di caratteri.");
            try{if(Utf8.GetByteCount(text)>MaxBytes)throw new FlowException("Il file supera 4 MiB.");}
            catch(EncoderFallbackException){throw new FlowException("Il file contiene caratteri Unicode non validi.");}
        }
        // The caller owns the stream and grants access through its native file picker.
        public static string ReadText(Stream stream)
        {
            if(stream==null)throw new ArgumentNullException("stream");
            using(var bytes=new MemoryStream())
            {
                byte[] buffer=new byte[8192];int count;
                while((count=stream.Read(buffer,0,buffer.Length))>0){if(bytes.Length+count>MaxBytes)throw new FlowException("Il file supera 4 MiB.");bytes.Write(buffer,0,count);}
                byte[] data=bytes.ToArray();int start=0;Encoding encoding=Utf8;
                if(data.Length>=3&&data[0]==239&&data[1]==187&&data[2]==191)start=3;
                else if(data.Length>=2&&data[0]==255&&data[1]==254){start=2;encoding=new UnicodeEncoding(false,false,true);}
                else if(data.Length>=2&&data[0]==254&&data[1]==255){start=2;encoding=new UnicodeEncoding(true,false,true);}
                try{string text=encoding.GetString(data,start,data.Length-start);ValidateText(text);return text;}
                catch(DecoderFallbackException){throw new FlowException("Il file non è un testo UTF-8 o UTF-16 valido. Salvalo come UTF-8 e riprova.");}
            }
        }
        public static void WriteText(Stream stream,string text)
        {
            ValidateText(text);byte[] bytes=Utf8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush();
        }
        internal static bool Channel(string text)
        {
            string mode=(text??"").Trim().ToUpperInvariant();
            if(mode=="LETTURA"||mode=="READ")return true;
            if(mode=="SCRITTURA"||mode=="WRITE")return false;
            throw new FlowException("Scegli il canale LETTURA o SCRITTURA.");
        }
        internal static int ExpressionOffset(NodeKind kind,string text)
        {
            if(kind==NodeKind.CloseFile)return text.Length;
            int start=0;while(start<text.Length&&Char.IsWhiteSpace(text[start]))start++;
            int end=start;while(end<text.Length&&!Char.IsWhiteSpace(text[end]))end++;
            return kind==NodeKind.OpenFile||kind==NodeKind.WriteFile&&text.Substring(start,end-start).Equals("SENZA_A_CAPO",StringComparison.OrdinalIgnoreCase)?end:0;
        }
        internal static string OpenExpression(string text,out bool reading)
        {
            text=(text??"").Trim();int space=text.IndexOf(' ');
            if(space<0)throw new FlowException("Scrivi LETTURA \"dati.txt\" oppure SCRITTURA \"risultato.txt\".");
            reading=Channel(text.Substring(0,space));return text.Substring(space+1).Trim();
        }
        internal static string WriteExpression(string text,out bool newline)
        {
            text=(text??"").Trim();const string prefix="SENZA_A_CAPO ";
            if(text.Equals("SENZA_A_CAPO",StringComparison.OrdinalIgnoreCase))throw new FlowException("Dopo SENZA_A_CAPO scrivi il valore da salvare.");
            newline=!text.StartsWith(prefix,StringComparison.OrdinalIgnoreCase);
            return newline?text:text.Substring(prefix.Length).Trim();
        }
    }
}
