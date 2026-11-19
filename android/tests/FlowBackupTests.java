package it.frociart.app;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;
import java.util.zip.*;

/** Real portable backup codec tests on the JVM. Android SAF/storage are tested separately. */
public final class FlowBackupTests {
    private static int checks;
    private interface Action{void run()throws Exception;}
    private static void check(boolean pass,String message){checks++;if(!pass)throw new AssertionError(message);}
    private static void fails(Action action,String message)throws Exception{boolean failed=false;try{action.run();}catch(IOException ex){failed=true;}check(failed,message);}
    private static byte[] bytes(String text){return text.getBytes(StandardCharsets.UTF_8);}
    private static byte[] save(FlowBackup.Archive archive)throws IOException{ByteArrayOutputStream out=new ByteArrayOutputStream();FlowBackup.write(out,archive);return out.toByteArray();}
    private static FlowBackup.Archive read(byte[] bytes)throws IOException{return FlowBackup.read(new ByteArrayInputStream(bytes));}
    private static Map<String,byte[]> unpack(byte[] bytes)throws Exception{Map<String,byte[]> parts=new LinkedHashMap<>();try(ZipInputStream in=new ZipInputStream(new ByteArrayInputStream(bytes))){ZipEntry e;while((e=in.getNextEntry())!=null){ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1)out.write(b,0,n);parts.put(e.getName(),out.toByteArray());}}return parts;}
    private static byte[] zip(Map<String,byte[]> parts)throws Exception{ByteArrayOutputStream out=new ByteArrayOutputStream();try(ZipOutputStream z=new ZipOutputStream(out)){for(Map.Entry<String,byte[]> p:parts.entrySet()){z.putNextEntry(new ZipEntry(p.getKey()));z.write(p.getValue());z.closeEntry();}}return out.toByteArray();}
    private static String hash(byte[] data)throws Exception{StringBuilder b=new StringBuilder();for(byte value:MessageDigest.getInstance("SHA-256").digest(data))b.append(String.format(Locale.ROOT,"%02x",value&255));return b.toString();}
    private static void repairHash(Map<String,byte[]> parts,String name,byte[] value)throws Exception{String manifest=new String(parts.get("frociart-backup.txt"),StandardCharsets.UTF_8);String old=hash(parts.get(name));parts.put(name,value);parts.put("frociart-backup.txt",bytes(manifest.replace(old,hash(value))));}
    private static int u16(byte[] data,int i){return(data[i]&255)|((data[i+1]&255)<<8);}
    private static int u32(byte[] data,int i){return u16(data,i)|(u16(data,i+2)<<16);}
    private static FlowModel.Document complex(){
        FlowModel.Document doc=FlowExamples.parity();doc.title="Lezione è 🦊 / 同名";doc.version=4;doc.find("check").comment="Nota del blocco";
        FlowModel.Node note=new FlowModel.Node(FlowModel.NOTE,"Nota libera da conservare",40,20);doc.nodes.add(note);
        doc.functions=new ArrayList<>();FlowModel.Function f=FlowFunctionEditing.create(doc);f.name="calcola";doc.functions.add(f);return doc;
    }
    private static void positive()throws Exception{
        FlowModel.Document first=complex(),second=FlowExamples.sum();second.title=first.title;
        String original=FlowCodec.encode(first);FlowBackup.Archive snapshot=FlowBackup.create(Arrays.asList(first,second));first.title="modificato dopo snapshot";
        check(snapshot.count()==2,"two saved projects");check(snapshot.projects().get(0).title.equals(second.title),"snapshot retains original Unicode title");
        byte[] container=save(snapshot);FlowBackup.Archive restored=read(container);check(restored.count()==2,"round-trip count");
        check(restored.projects().get(0).title.equals(restored.projects().get(1).title),"duplicate titles allowed as distinct projects");
        check(original.equals(new String(restored.projectBytes(0),StandardCharsets.UTF_8)),"all project content, functions, note and comment survive byte-for-byte");
        byte[] detached=restored.projectBytes(0);detached[0]='!';check(restored.projectBytes(0)[0]=='{',"restore data cannot be mutated through returned arrays");
        check(restored.totalBytes()==snapshot.totalBytes(),"exact uncompressed byte count");
        check(new String(unpack(container).get("frociart-backup.txt"),StandardCharsets.UTF_8).contains("DRAFTS=EXCLUDED"),"draft exclusion recorded in file contract");
        try{restored.projects().clear();throw new AssertionError("mutable preview");}catch(UnsupportedOperationException expected){checks++;}
        List<FlowModel.Document> hundred=new ArrayList<>();for(int i=0;i<100;i++)hundred.add(FlowExamples.blank());check(read(save(FlowBackup.create(hundred))).count()==100,"100 project boundary accepted");
        hundred.add(FlowExamples.blank());fails(()->FlowBackup.create(hundred),"101 projects refused");
        fails(()->FlowBackup.create(Collections.emptyList()),"empty backup refused");
        FlowModel.Document invalid=FlowExamples.blank();invalid.nodes.get(0).x=Float.NaN;fails(()->FlowBackup.create(Collections.singletonList(invalid)),"invalid project refused before writing");
    }
    private static void negative()throws Exception{
        byte[] valid=save(FlowBackup.create(Arrays.asList(complex(),FlowExamples.sum())));
        fails(()->read(new byte[0]),"empty container");fails(()->read(bytes("not a ZIP")),"wrong file type");
        fails(()->read(Arrays.copyOf(valid,valid.length-10)),"truncated central directory");
        byte[] damaged=valid.clone();damaged[damaged.length-6]^=1;fails(()->read(damaged),"corrupted central offset");
        byte[] wrongCount=valid.clone();int end=wrongCount.length-22;wrongCount[end+8]=2;wrongCount[end+10]=2;fails(()->read(wrongCount),"central/local entry count mismatch");
        byte[] wrongName=valid.clone();int central=u32(wrongName,wrongName.length-6),next=central+46+u16(wrongName,central+28)+u16(wrongName,central+30)+u16(wrongName,central+32);wrongName[next+46+11]='9';fails(()->read(wrongName),"central/local project name mismatch");
        byte[] wrongCrc=valid.clone();wrongCrc[central+16]^=1;fails(()->read(wrongCrc),"central/local CRC mismatch");
        byte[] wrongSize=valid.clone();wrongSize[central+24]^=1;fails(()->read(wrongSize),"central/local size mismatch");
        byte[] badName=valid.clone();badName[30]=(byte)0xc3;badName[31]=(byte)0x28;fails(()->read(badName),"invalid UTF8 entry name becomes an import error");
        Map<String,byte[]> parts=unpack(valid);parts.put("../outside.txt",bytes("never extract"));final byte[] traversal=zip(parts);fails(()->read(traversal),"path traversal rejected");
        parts=unpack(valid);parts.put("/absolute",bytes("never extract"));final byte[] absolute=zip(parts);fails(()->read(absolute),"absolute path rejected");
        parts=unpack(valid);parts.put("unknown.txt",bytes("hidden data"));final byte[] extra=zip(parts);fails(()->read(extra),"unknown entry rejected");
        parts=unpack(valid);parts.remove("projects/002.frociart");final byte[] missing=zip(parts);fails(()->read(missing),"missing project rejects entire archive");
        parts=unpack(valid);parts.put("frociart-backup.txt",bytes(new String(parts.get("frociart-backup.txt"),StandardCharsets.UTF_8).replace("VERSION=1","VERSION=2")));final byte[] newer=zip(parts);fails(()->read(newer),"newer backup format rejected");
        parts=unpack(valid);byte[] content=parts.get("projects/001.frociart").clone();content[content.length/2]^=1;parts.put("projects/001.frociart",content);final byte[] changed=zip(parts);fails(()->read(changed),"changed project rejected by digest");
        parts=unpack(valid);repairHash(parts,"projects/001.frociart",bytes("{invalid}"));final byte[] invalid=zip(parts);fails(()->read(invalid),"malformed project rejected even with valid digest");
        parts=unpack(valid);repairHash(parts,"projects/001.frociart",new byte[]{(byte)0xc3,(byte)0x28});final byte[] utf8=zip(parts);fails(()->read(utf8),"malformed UTF8 rejected");
        parts=unpack(valid);parts.put("projects/001.frociart",new byte[FlowModel.MAX_BYTES+1]);final byte[] bomb=zip(parts);fails(()->read(bomb),"high-ratio compressed entry capped before JSON parsing");
        byte[] duplicate=valid.clone();byte[] from=bytes("projects/002.frociart"),to=bytes("projects/001.frociart");for(int i=0;i<=duplicate.length-from.length;i++){boolean match=true;for(int j=0;j<from.length;j++)if(duplicate[i+j]!=from[j]){match=false;break;}if(match)System.arraycopy(to,0,duplicate,i,to.length);}fails(()->read(duplicate),"duplicate ZIP names rejected");
        fails(()->read(new byte[FlowBackup.MAX_ARCHIVE_BYTES+1]),"compressed container byte limit");
        List<FlowModel.Document> many=new ArrayList<>();FlowModel.Document large=FlowExamples.blank();large.version=4;StringBuilder text=new StringBuilder();for(int i=0;i<8192;i++)text.append('x');for(int i=0;i<450;i++)large.nodes.add(new FlowModel.Node(FlowModel.NOTE,text.toString(),i,0));for(int i=0;i<10;i++)many.add(large);fails(()->FlowBackup.create(many),"combined project data capped at 32 MB");
    }
    private static void streams()throws Exception{
        final boolean[] closed={false};InputStream failed=new InputStream(){public int read()throws IOException{throw new IOException("fixture read failure");}public void close(){closed[0]=true;}};fails(()->FlowBackup.read(failed),"input failure propagated");check(closed[0],"failed source closed");
        closed[0]=false;OutputStream out=new OutputStream(){int written;public void write(int value)throws IOException{if(++written>20)throw new IOException("fixture disk failure");}public void close(){closed[0]=true;}};fails(()->FlowBackup.write(out,FlowBackup.create(Collections.singletonList(FlowExamples.blank()))),"output failure propagated");check(closed[0],"failed destination closed");
    }
    public static void main(String[] args)throws Exception{positive();negative();streams();System.out.println("FlowBackupTests PASS "+checks+" assertions (JVM codec only; Android storage/SAF separately).");}
}
