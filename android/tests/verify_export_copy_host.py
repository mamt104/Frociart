"""Exercise the exact production copy routine with real files and failing destination streams.

This is a JVM boundary check, not an Android document-provider/UI execution.
"""
import argparse, subprocess
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--java-home',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
project=Path(__file__).resolve().parents[1]
source=(project/'app/src/main/java/it/frociart/app/MainActivity.java').read_text(encoding='utf-8-sig')
start=source.index('private static void copyExport(')
end=source.index('{',start)+1
depth=1
while depth:
    if source[end]=='{':depth+=1
    elif source[end]=='}':depth-=1
    end+=1
routine=source[start:end]
out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
java='''import java.io.*;import java.nio.file.*;import java.util.*;
public class ExportCopyChecks {
ROUTINE
static int checks;static void ok(boolean b,String s){checks++;if(!b)throw new AssertionError(s);System.out.println("PASS "+s);}
static class Destination extends OutputStream {ByteArrayOutputStream data=new ByteArrayOutputStream();boolean closed,failWrite,failClose;public void write(int b)throws IOException{if(failWrite)throw new IOException("write failure");data.write(b);}public void close()throws IOException{closed=true;if(failClose)throw new IOException("close failure");}}
static boolean copyFails(File file,OutputStream out){try{copyExport(file,out);return false;}catch(IOException expected){return true;}}
public static void main(String[] args)throws Exception{
 Path folder=Paths.get(args[0]);Files.createDirectories(folder);byte[] original=new byte[100003];new Random(42).nextBytes(original);Path file=folder.resolve("source.bin");Files.write(file,original);
 Destination success=new Destination();copyExport(file.toFile(),success);ok(Arrays.equals(original,success.data.toByteArray()),"all bytes are written across several buffers");ok(success.closed,"success returns after destination close");
 Destination writeError=new Destination();writeError.failWrite=true;ok(copyFails(file.toFile(),writeError),"write failure propagates instead of reporting success");ok(writeError.closed,"destination is closed after a write failure");
 Destination closeError=new Destination();closeError.failClose=true;ok(copyFails(file.toFile(),closeError),"close failure propagates instead of reporting success");ok(closeError.closed,"destination close was attempted");ok(Arrays.equals(original,closeError.data.toByteArray()),"a late close error remains an error even after every byte was written");
 Destination missing=new Destination();ok(copyFails(folder.resolve("missing.bin").toFile(),missing),"missing source is reported");ok(missing.closed,"destination closes even when the source cannot open");ok(copyFails(file.toFile(),null),"null destination is reported");ok(Arrays.equals(original,Files.readAllBytes(file)),"all failed copies leave the original source intact");
 System.out.println("EXPORT COPY HOST PASS "+checks+" checks; Android provider and UI are separate tests.");
}}
'''.replace('ROUTINE',routine)
test=out/'ExportCopyChecks.java';test.write_text(java,encoding='utf-8')
suffix='.exe' if (args.java_home/'bin/java.exe').exists() else ''
for name,command in [('compile',[args.java_home/('bin/javac'+suffix),'--release','8','-encoding','UTF-8','-d',out,test]),('runtime',[args.java_home/('bin/java'+suffix),'-cp',out,'ExportCopyChecks',out/'fixture'])]:
    result=subprocess.run(list(map(str,command)),capture_output=True,timeout=90)
    (out/(name+'.log')).write_bytes(result.stdout+result.stderr)
    print((result.stdout+result.stderr).decode('utf-8',errors='replace'),end='')
    if result.returncode:raise SystemExit(result.returncode)
