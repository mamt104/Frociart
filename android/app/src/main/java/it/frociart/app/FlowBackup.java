package it.frociart.app;

import java.io.*;
import java.nio.ByteBuffer;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.*;
import java.util.zip.*;

/** Offline, user-selected copies of saved projects. No entry is extracted as a path. */
public final class FlowBackup {
    public static final int MAX_PROJECTS=100, MAX_TOTAL_BYTES=32*1024*1024;
    public static final int MAX_ARCHIVE_BYTES=MAX_TOTAL_BYTES+1024*1024;
    private static final String MANIFEST="frociart-backup.txt", MAGIC="FROCIART-BACKUP\nVERSION=1\nDRAFTS=EXCLUDED\n";
    private FlowBackup(){}

    public static final class ProjectInfo {
        public final String title;public final int nodes;
        private ProjectInfo(FlowModel.Document document){title=document.title;nodes=document.totalNodes();}
    }
    public static final class Archive {
        private final List<byte[]> content;
        private final List<ProjectInfo> infos;
        private final long bytes;
        private Archive(List<byte[]> content,List<ProjectInfo> infos,long bytes){this.content=Collections.unmodifiableList(content);this.infos=Collections.unmodifiableList(infos);this.bytes=bytes;}
        public int count(){return content.size();}
        public List<ProjectInfo> projects(){return infos;}
        public long totalBytes(){return bytes;}
        byte[] projectBytes(int index){return content.get(index).clone();}
    }

    /** Copies and validates each document; subsequent editor changes cannot alter this snapshot. */
    public static Archive create(List<FlowModel.Document> documents)throws IOException{
        if(documents==null||documents.isEmpty()||documents.size()>MAX_PROJECTS)throw invalid("Seleziona da 1 a 100 progetti per il backup.");
        Builder builder=new Builder();for(FlowModel.Document document:documents)builder.add(document);return builder.build();
    }
    /** Incremental snapshot: storage never needs to retain all decoded documents together. */
    static final class Builder{
        private final List<byte[]> content=new ArrayList<>();private final List<ProjectInfo> infos=new ArrayList<>();private long total;
        long remainingBytes(){return MAX_TOTAL_BYTES-total;}
        void add(FlowModel.Document document)throws IOException{
            if(content.size()>=MAX_PROJECTS)throw invalid("Il backup supporta al massimo 100 progetti.");
            byte[] bytes;try{bytes=FlowCodec.encode(document).getBytes(StandardCharsets.UTF_8);}catch(RuntimeException ex){throw invalid("Un progetto non è valido. Nessun backup è stato creato.",ex);}
            if(bytes.length>MAX_TOTAL_BYTES-total)throw invalid("Il backup supera il limite complessivo di 32 MB.");
            total+=bytes.length;content.add(bytes);infos.add(new ProjectInfo(document));
        }
        Archive build()throws IOException{if(content.isEmpty())throw invalid("Non ci sono progetti da includere nel backup.");return new Archive(new ArrayList<>(content),new ArrayList<>(infos),total);}
    }

    /** Closes the selected destination, including on failure. A write failure is never a success. */
    public static void write(OutputStream destination,Archive archive)throws IOException{
        if(destination==null)throw invalid("La destinazione del backup non è disponibile.");
        try(OutputStream owned=destination;ZipOutputStream zip=new ZipOutputStream(owned,StandardCharsets.UTF_8)){
            requireArchive(archive);StringBuilder manifest=new StringBuilder(MAGIC).append("COUNT=").append(archive.count()).append('\n');
            for(int i=0;i<archive.count();i++)manifest.append(name(i)).append(' ').append(hash(archive.content.get(i))).append('\n');
            zip.putNextEntry(new ZipEntry(MANIFEST));zip.write(manifest.toString().getBytes(StandardCharsets.UTF_8));zip.closeEntry();
            for(int i=0;i<archive.count();i++){zip.putNextEntry(new ZipEntry(name(i)));zip.write(archive.content.get(i));zip.closeEntry();}
            zip.finish();
        }
    }

    /** Reads the complete container before offering an import; invalid entries cannot be partly restored. */
    public static Archive read(InputStream source)throws IOException{
        if(source==null)throw invalid("Il backup non è leggibile.");
        Map<String,byte[]> entries=new HashMap<>();Map<String,ZipEntry> metadata=new HashMap<>();long total=0;
        try(TrackedInput input=new TrackedInput(source);ZipInputStream zip=new ZipInputStream(input,StandardCharsets.UTF_8)){
            ZipEntry entry;while((entry=zip.getNextEntry())!=null){String path=entry.getName();
                if(entry.isDirectory()||(!MANIFEST.equals(path)&&!path.matches("projects/[0-9]{3}\\.frociart"))||entries.containsKey(path))throw invalid("Il backup contiene elementi non previsti o duplicati.");
                if(entries.size()>=MAX_PROJECTS+1)throw invalid("Il backup contiene troppi progetti.");
                int limit=MANIFEST.equals(path)?16384:FlowModel.MAX_BYTES;
                byte[] data=readBytes(zip,limit,"Un elemento del backup supera il limite consentito.");
                total+=data.length;if(total>MAX_TOTAL_BYTES+16384)throw invalid("Il backup decompresso supera il limite di 32 MB.");
                entries.put(path,data);zip.closeEntry();metadata.put(path,entry);
            }
            // ZipInputStream stops at the central directory. Drain its underlying source to
            // verify that directory and the final marker without retaining compressed payloads.
            byte[] buffer=new byte[8192];while(input.read(buffer)!=-1){}
            checkContainer(input.tail(),input.count,metadata);
        }catch(ZipException|IllegalArgumentException ex){throw invalid("Il backup è danneggiato o usa un formato ZIP non supportato.",ex);}
        byte[] manifestBytes=entries.remove(MANIFEST);if(manifestBytes==null)throw invalid("Questo file non è un backup di Frociart.");
        String manifest=utf8(manifestBytes);if(!manifest.startsWith(MAGIC))throw invalid("Versione del backup non supportata. Aggiorna Frociart prima di aprirlo.");
        String[] lines=manifest.substring(MAGIC.length()).split("\n",-1);
        if(lines.length<3||!lines[0].matches("COUNT=[1-9][0-9]{0,2}")||!lines[lines.length-1].isEmpty())throw invalid("L'indice del backup non è valido.");
        int count=Integer.parseInt(lines[0].substring(6));if(count>MAX_PROJECTS||entries.size()!=count||lines.length!=count+2)throw invalid("Il backup è incompleto o contiene troppi progetti.");
        List<byte[]> content=new ArrayList<>();List<ProjectInfo> infos=new ArrayList<>();long bytes=0;
        for(int i=0;i<count;i++){
            String path=name(i);byte[] data=entries.get(path);
            if(data==null||!lines[i+1].equals(path+" "+hash(data)))throw invalid("Un progetto del backup è mancante o danneggiato.");
            FlowModel.Document document;try{document=FlowCodec.decode(utf8(data));}catch(RuntimeException ex){throw invalid("Il backup contiene un progetto non valido.",ex);}
            bytes+=data.length;if(bytes>MAX_TOTAL_BYTES)throw invalid("Il backup decompresso supera il limite di 32 MB.");
            content.add(data);infos.add(new ProjectInfo(document));
        }
        return new Archive(content,infos,bytes);
    }

    private static void requireArchive(Archive archive)throws IOException{if(archive==null||archive.count()<1||archive.count()>MAX_PROJECTS||archive.totalBytes()>MAX_TOTAL_BYTES)throw invalid("Il backup non è valido.");}
    private static String name(int index){return String.format(Locale.ROOT,"projects/%03d.frociart",index+1);}
    private static byte[] readBytes(InputStream input,int limit,String message)throws IOException{
        ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int n;
        while((n=input.read(buffer))!=-1){if(bytes.size()>limit-n)throw invalid(message);bytes.write(buffer,0,n);}return bytes.toByteArray();
    }
    private static String utf8(byte[] bytes)throws IOException{try{return StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(bytes)).toString();}catch(java.nio.charset.CharacterCodingException ex){throw invalid("Il backup non contiene testo UTF-8 valido.",ex);}}
    private static String hash(byte[] bytes){try{byte[] digest=MessageDigest.getInstance("SHA-256").digest(bytes);StringBuilder out=new StringBuilder(64);for(byte value:digest)out.append(String.format(Locale.ROOT,"%02x",value&255));return out.toString();}catch(NoSuchAlgorithmException ex){throw new IllegalStateException(ex);}}
    private static int u16(byte[] bytes,int at){return(bytes[at]&255)|((bytes[at+1]&255)<<8);}
    private static long u32(byte[] bytes,int at){return (u16(bytes,at)&65535L)|((long)u16(bytes,at+2)<<16);}
    private static void checkContainer(byte[] bytes,long containerBytes,Map<String,ZipEntry> entries)throws IOException{
        // Our format has no ZIP comments, split disks or ZIP64. Require a complete central directory.
        int end=bytes.length-22;
        if(end<0||u32(bytes,end)!=0x06054b50L||u16(bytes,end+20)!=0||u16(bytes,end+4)!=0||u16(bytes,end+6)!=0||u16(bytes,end+8)!=u16(bytes,end+10))throw invalid("Il backup ZIP è incompleto o non supportato.");
        int count=u16(bytes,end+10);long size=u32(bytes,end+12),offset=u32(bytes,end+16);
        if(count<2||count>MAX_PROJECTS+1||count!=entries.size()||size>65536||offset+size!=containerBytes-22||offset<0||size>end)throw invalid("L'indice ZIP del backup è danneggiato.");
        int at=end-(int)size;Set<String> seen=new HashSet<>();for(int i=0;i<count;i++){
            if(at+46>end||u32(bytes,at)!=0x02014b50L)throw invalid("L'indice ZIP del backup è incompleto.");
            int nameLength=u16(bytes,at+28),extraLength=u16(bytes,at+30),commentLength=u16(bytes,at+32),next=at+46+nameLength+extraLength+commentLength;
            if(next>end||u16(bytes,at+34)!=0)throw invalid("L'indice ZIP del backup non è supportato.");
            String name=utf8(Arrays.copyOfRange(bytes,at+46,at+46+nameLength));ZipEntry local=entries.get(name);
            if(local==null||!seen.add(name)||u32(bytes,at+16)!=local.getCrc()||u32(bytes,at+20)!=local.getCompressedSize()||u32(bytes,at+24)!=local.getSize()||u16(bytes,at+10)!=local.getMethod())throw invalid("L'indice ZIP non corrisponde ai progetti del backup.");
            at=next;
        }
        if(at!=end)throw invalid("L'indice ZIP del backup non è valido.");
    }
    private static final class TrackedInput extends FilterInputStream{
        private final byte[] ring=new byte[65536+22];private int cursor,used;private long count;
        TrackedInput(InputStream input){super(input);}
        @Override public int read()throws IOException{int value=in.read();if(value!=-1){if(++count>MAX_ARCHIVE_BYTES)throw invalid("Il file supera il limite di 33 MB.");ring[cursor]=(byte)value;cursor=(cursor+1)%ring.length;used=Math.min(ring.length,used+1);}return value;}
        @Override public int read(byte[] buffer,int offset,int length)throws IOException{
            int read=in.read(buffer,offset,length),n=read;if(n>0){count+=n;if(count>MAX_ARCHIVE_BYTES)throw invalid("Il file supera il limite di 33 MB.");int start=offset;if(n>ring.length){start+=n-ring.length;n=ring.length;}int first=Math.min(n,ring.length-cursor);System.arraycopy(buffer,start,ring,cursor,first);System.arraycopy(buffer,start+first,ring,0,n-first);cursor=(cursor+n)%ring.length;used=Math.min(ring.length,used+n);}return read;
        }
        byte[] tail(){byte[] result=new byte[used];int start=(cursor-used+ring.length)%ring.length,first=Math.min(used,ring.length-start);System.arraycopy(ring,start,result,0,first);System.arraycopy(ring,0,result,first,used-first);return result;}
    }
    private static IOException invalid(String message){return new IOException(message);}
    private static IOException invalid(String message,Exception cause){return new IOException(message,cause);}
}
