package it.frociart.app;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.ByteBuffer;
import java.nio.CharBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.Charset;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

/** Program-wide read/write channels. Only native pickers grant filesystem access. */
public final class FlowTextFiles {
    public interface Journal {String id();void append(String text)throws IOException;void close()throws IOException;boolean saved();}
    public String activeJournalId(){return journal==null?null:journal.id();}
    public interface JournalFactory {Journal create(String name)throws IOException;}
    public JournalFactory journalFactory;
    private Journal journal;
    public boolean recoveryCopyRemains;
    public void closeJournal()throws IOException{if(journal!=null){journal.close();journal=null;}}
    public static final int MAX_BYTES=4194304,MAX_CHARACTERS=2097152,MAX_LINE=32768;
    public enum Kind { READ, SAVE }
    public static final class Request {
        public final Kind kind;
        public final String name,text;
        Request(Kind kind,String name,String text){this.kind=kind;this.name=name;this.text=text;}
    }
    private String input,readName,writeName;
    private int position,outputBytes;
    private StringBuilder output;
    public boolean hasRead(){return input!=null;}
    public boolean hasWrite(){return output!=null;}
    public String readName(){return readName;}
    public String writeName(){return writeName;}
    public String outputDraft(){return output==null?null:output.toString();}
    private void requireRead(){if(!hasRead())throw error("Apri prima un file in lettura. EOF() controlla quel file.");}
    public boolean eof(){requireRead();return position>=input.length();}
    static String name(FlowEngine.Value value){
        if(!value.isText())throw error("Il nome del file deve essere un testo da 1 a 256 caratteri.");
        String name=value.format();
        if(name.trim().isEmpty()||name.length()>256)throw error("Il nome del file deve essere un testo da 1 a 256 caratteri.");
        for(int i=0;i<name.length();i++)if(Character.isISOControl(name.charAt(i)))throw error("Il nome del file non può contenere righe nuove o caratteri di controllo.");
        validateText(name);
        return name;
    }
    void openRead(String name,String text){
        if(hasRead())throw error("Un file è già aperto in lettura. Chiudilo prima di aprirne un altro.");
        validateText(text);input=text;position=0;readName=name;
    }
    void openWrite(String name){
        if(hasWrite())throw error("Un file è già aperto in scrittura. Chiudilo e salvalo prima di aprirne un altro.");
        try{journal=journalFactory==null?null:journalFactory.create(name);}catch(IOException|SecurityException ex){throw error("Non riesco a preparare la bozza locale. Controlla lo spazio disponibile e File generati, poi riprova.");}
        recoveryCopyRemains=false;
        output=new StringBuilder();outputBytes=0;writeName=name;
    }
    public String peekLine(){
        requireRead();if(eof())throw error("Il file è terminato. Usa non EOF() nella condizione del ciclo prima di leggere.");
        int end=position;
        while(end<input.length()&&input.charAt(end)!='\r'&&input.charAt(end)!='\n'){if(end-position>=MAX_LINE)throw error("Una riga del file supera 32768 caratteri.");end++;}
        return input.substring(position,end);
    }
    void commitRead(){
        position+=peekLine().length();
        if(position<input.length()&&input.charAt(position)=='\r'){position++;if(position<input.length()&&input.charAt(position)=='\n')position++;}
        else if(position<input.length()&&input.charAt(position)=='\n')position++;
    }
    void write(String text,boolean newline){
        if(!hasWrite())throw error("Apri prima un file in scrittura.");
        text=(text==null?"":text)+(newline?"\n":"");int bytes=utf8(text).length;
        if(output.length()+(long)text.length()>MAX_CHARACTERS||outputBytes+(long)bytes>MAX_BYTES)throw error("Il file generato supera il limite di 4 MiB o 2 milioni di caratteri.");
        try{if(journal!=null)journal.append(text);}catch(IOException|SecurityException ex){throw error("Scrittura interrotta: non riesco a conservare la bozza locale. Il testo precedente resta in File generati.");}
        output.append(text);outputBytes+=bytes;
    }
    void closeRead(){requireRead();input=null;position=0;readName=null;}
    void saved(){if(journal!=null){recoveryCopyRemains=!journal.saved();journal=null;}output=null;outputBytes=0;writeName=null;}
    void reset(){try{closeJournal();}catch(IOException ex){throw error("Non riesco a chiudere la bozza locale. Il testo è conservato in File generati.");}input=null;position=0;readName=null;output=null;outputBytes=0;writeName=null;recoveryCopyRemains=false;}
    private static byte[] utf8(String text){
        try{ByteBuffer bytes=StandardCharsets.UTF_8.newEncoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).encode(CharBuffer.wrap(text));byte[] result=new byte[bytes.remaining()];bytes.get(result);return result;}
        catch(CharacterCodingException ex){throw error("Il file contiene caratteri Unicode non validi.");}
    }
    private static void validateText(String text){
        if(text==null)throw error("Il file di testo non è disponibile.");
        if(text.length()>MAX_CHARACTERS)throw error("Il file supera 2 milioni di caratteri.");
        if(utf8(text).length>MAX_BYTES)throw error("Il file supera 4 MiB.");
    }
    // Streams are owned by the caller and are not closed here.
    public static String readText(InputStream stream)throws IOException{
        ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int count;
        while((count=stream.read(buffer))!=-1){if(bytes.size()+(long)count>MAX_BYTES)throw error("Il file supera 4 MiB.");bytes.write(buffer,0,count);}
        byte[] data=bytes.toByteArray();int start=0;Charset encoding=StandardCharsets.UTF_8;
        if(data.length>=3&&(data[0]&255)==239&&(data[1]&255)==187&&(data[2]&255)==191)start=3;
        else if(data.length>=2&&(data[0]&255)==255&&(data[1]&255)==254){start=2;encoding=StandardCharsets.UTF_16LE;}
        else if(data.length>=2&&(data[0]&255)==254&&(data[1]&255)==255){start=2;encoding=StandardCharsets.UTF_16BE;}
        try{String text=encoding.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(data,start,data.length-start)).toString();validateText(text);return text;}
        catch(CharacterCodingException ex){throw error("Il file non è un testo UTF-8 o UTF-16 valido. Salvalo come UTF-8 e riprova.");}
    }
    public static void writeText(OutputStream stream,String text)throws IOException{validateText(text);stream.write(utf8(text));stream.flush();}
    static boolean channel(String text){
        String mode=text==null?"":text.trim().toUpperCase(Locale.ROOT);
        if(mode.equals("LETTURA")||mode.equals("READ"))return true;
        if(mode.equals("SCRITTURA")||mode.equals("WRITE"))return false;
        throw error("Scegli il canale LETTURA o SCRITTURA.");
    }
    static final class Open {
        final boolean reading;final String expression;
        Open(String text){text=text==null?"":text.trim();int space=text.indexOf(' ');if(space<0)throw error("Scrivi LETTURA \"dati.txt\" oppure SCRITTURA \"risultato.txt\".");reading=channel(text.substring(0,space));expression=text.substring(space+1).trim();}
    }
    static int expressionOffset(int kind,String text){
        if(kind==FlowModel.CLOSE_FILE)return text.length();
        int start=0;while(start<text.length()&&Character.isWhitespace(text.charAt(start)))start++;
        int end=start;while(end<text.length()&&!Character.isWhitespace(text.charAt(end)))end++;
        return kind==FlowModel.OPEN_FILE||kind==FlowModel.WRITE_FILE&&text.substring(start,end).equalsIgnoreCase("SENZA_A_CAPO")?end:0;
    }
    static final class Write {
        final boolean newline;final String expression;
        Write(String text){text=text==null?"":text.trim();if(text.equalsIgnoreCase("SENZA_A_CAPO"))throw error("Dopo SENZA_A_CAPO scrivi il valore da salvare.");String prefix="SENZA_A_CAPO ";newline=!text.toUpperCase(Locale.ROOT).startsWith(prefix);expression=newline?text:text.substring(prefix.length()).trim();}
    }
    private static FlowEngine.Problem error(String text){return new FlowEngine.Problem(text);}
}
