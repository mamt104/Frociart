package it.frociart.app;

import java.io.*;
import java.nio.*;
import java.nio.charset.*;
import java.security.*;
import java.util.*;

/** Private bounded append journal. Checked frames allow recovery after a partial final write. */
public final class FlowFileRecovery {
    private static final byte[] MAGIC="FRCIOD01".getBytes(StandardCharsets.US_ASCII);
    private static final int MAX_JOURNALS=50,MAX_JOURNAL_BYTES=4600000;
    private final File folder;
    public static final class Entry {
        public final String name;public final long modified;final File file;
        public String id(){return file.getName().substring(0,32);}
        Entry(String name,File file){this.name=name;this.file=file;modified=file.lastModified();}
    }
    public static final class Content {
        public final String name,text;public final boolean incomplete;
        Content(String name,String text,boolean incomplete){this.name=name;this.text=text;this.incomplete=incomplete;}
    }
    public FlowFileRecovery(File folder){this.folder=folder.getAbsoluteFile();}
    private File[] paths()throws IOException{if(!folder.exists())return new File[0];File[] files=folder.listFiles(f->f.getName().matches("[0-9a-fA-F]{32}\\.fiodraft"));if(files==null)throw new IOException("Cartella delle bozze non leggibile.");return files;}
    private boolean owns(Entry entry)throws IOException{return entry!=null&&entry.file.getName().matches("[0-9a-fA-F]{32}\\.fiodraft")&&entry.file.getCanonicalFile().getParentFile().equals(folder.getCanonicalFile());}
    public List<Entry> list()throws IOException{
        ArrayList<Entry> result=new ArrayList<>();File[] paths=paths();
        for(int i=0;i<paths.length&&i<1000;i++){File file=paths[i];String name="File recuperato";try(InputStream input=new FileInputStream(file)){header(input);name=decode(readFrame(input,1024));}catch(IOException ignored){}result.add(new Entry(name,file));}
        Collections.sort(result,(a,b)->Long.compare(b.modified,a.modified));return Collections.unmodifiableList(result);
    }
    public FlowTextFiles.Journal create(String name)throws IOException{
        if(!folder.isDirectory()&&!folder.mkdirs())throw new IOException("Cartella delle bozze non disponibile.");
        if(paths().length>=MAX_JOURNALS)throw new IOException("Sono presenti 50 file da recuperare. Salvali o rimuovili da File generati.");
        File file=new File(folder,UUID.randomUUID().toString().replace("-","")+".fiodraft");if(!file.createNewFile())throw new IOException("Non riesco a creare la bozza.");return new Writer(file,name);
    }
    public Content load(Entry entry)throws IOException{
        if(!owns(entry))throw new IOException("File di recupero non valido.");if(entry.file.length()>MAX_JOURNAL_BYTES)throw new IOException("Il file di recupero supera il limite previsto.");
        try(BufferedInputStream input=new BufferedInputStream(new FileInputStream(entry.file))){
            header(input);String name=decode(readFrame(input,1024));StringBuilder text=new StringBuilder();int bytes=0;boolean incomplete=false;
            while(true){input.mark(1);if(input.read()==-1)break;input.reset();try{byte[] data=readFrame(input,FlowTextFiles.MAX_BYTES);String chunk=decode(data);if(text.length()+(long)chunk.length()>FlowTextFiles.MAX_CHARACTERS||bytes+(long)data.length>FlowTextFiles.MAX_BYTES)throw new IOException("Limite della bozza superato.");text.append(chunk);bytes+=data.length;}catch(IOException ex){incomplete=true;break;}}
            return new Content(name,text.toString(),incomplete);
        }
    }
    public void delete(Entry entry)throws IOException{if(!owns(entry))throw new IOException("File di recupero non valido.");if(entry.file.exists()&&!entry.file.delete())throw new IOException("Non riesco a rimuovere la bozza.");}
    private static void header(InputStream input)throws IOException{if(!Arrays.equals(MAGIC,exact(input,8)))throw new IOException("Formato di recupero sconosciuto.");}
    private static byte[] exact(InputStream input,int count)throws IOException{byte[] result=new byte[count];int read=0;while(read<count){int n=input.read(result,read,count-read);if(n<0)throw new EOFException("Scrittura interrotta.");read+=n;}return result;}
    private static byte[] digest(byte[] bytes){try{return MessageDigest.getInstance("SHA-256").digest(bytes);}catch(NoSuchAlgorithmException ex){throw new IllegalStateException(ex);}}
    private static byte[] readFrame(InputStream input,int max)throws IOException{int length=ByteBuffer.wrap(exact(input,4)).order(ByteOrder.LITTLE_ENDIAN).getInt();if(length<0||length>max)throw new IOException("Dimensione del frammento non valida.");byte[] hash=exact(input,32),data=exact(input,length);if(!Arrays.equals(hash,digest(data)))throw new IOException("Frammento incompleto.");return data;}
    private static String decode(byte[] bytes)throws IOException{return StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(bytes)).toString();}
    private static byte[] frame(String text)throws IOException{ByteBuffer raw=StandardCharsets.UTF_8.newEncoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).encode(CharBuffer.wrap(text));byte[] data=new byte[raw.remaining()];raw.get(data);ByteBuffer result=ByteBuffer.allocate(36+data.length).order(ByteOrder.LITTLE_ENDIAN);result.putInt(data.length).put(digest(data)).put(data);return result.array();}
    private static final class Writer implements FlowTextFiles.Journal {
        public String id(){return file.getName().substring(0,32);}
        private RandomAccessFile stream;private final File file;
        Writer(File file,String name)throws IOException{this.file=file;try{stream=new RandomAccessFile(file,"rw");stream.write(MAGIC);append(name);}catch(IOException ex){try{close();}catch(IOException closeError){ex.addSuppressed(closeError);}file.delete();throw ex;}}
        public void append(String text)throws IOException{if(stream==null)throw new IOException("La bozza non è più aperta.");byte[] bytes=frame(text);long start=stream.getFilePointer();try{stream.write(bytes);}catch(IOException ex){try{stream.setLength(start);stream.seek(start);}catch(IOException ignored){}throw ex;}}
        public void close()throws IOException{if(stream!=null){try{stream.close();}finally{stream=null;}}}
        public boolean saved(){try{close();return !file.exists()||file.delete();}catch(IOException ex){return false;}}
    }
}
