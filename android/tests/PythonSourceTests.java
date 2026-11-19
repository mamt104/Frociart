package it.frociart.app;
import java.util.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;

public final class PythonSourceTests {
    static int checks,programs;
    static void eq(Object a,Object b,String label){checks++;if(!Objects.equals(a,b))throw new AssertionError(label);}
    static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8);}
    public static void main(String[] args)throws Exception{
        Path root=Paths.get(args[0]);String runtime=read(Paths.get(args[1]));Path code=root.resolve("java-code");Files.createDirectories(code);
        List<Path> fixtures=new ArrayList<>();try(DirectoryStream<Path> stream=Files.newDirectoryStream(root.resolve("fixtures"),"*.frociart")){for(Path f:stream)fixtures.add(f);}Collections.sort(fixtures);
        List<String> outcomes=new ArrayList<>();
        for(Path f:fixtures){String name=f.getFileName().toString().replace(".frociart","");FlowModel.Document d=FlowCodec.decode(read(f));String before=FlowCodec.encode(d);FlowSource.Program p=FlowSource.compile(d);
            if(!p.canGenerate()){boolean rejected=false;try{FlowPython.generate(p,runtime);}catch(IllegalArgumentException ex){rejected=true;}eq(true,rejected,name+" invalid project rejected");eq(false,Files.exists(root.resolve("pc-code").resolve(name+".py")),name+" native compile decision");outcomes.add(name+"\tinvalid");continue;}
            FlowSourceText result=FlowPython.generate(p,runtime);eq(before,FlowCodec.encode(d),name+" original unchanged");eq(result.text,FlowPython.generate(p,runtime).text,name+" deterministic");
            eq("python",result.language,name+" language");
            for(FlowSource.Scope scope:p.scopes)for(FlowSource.Block block:scope.blocks)eq(true,!result.forBlock(scope.id,block.id).isEmpty(),name+" mapped node");
            for(FlowSourceText.Line line:result.lines){eq(true,line.start>=0&&line.start+line.length<result.text.length(),name+" valid source range");eq('\n',result.text.charAt(line.start+line.length),name+" newline offset");}
            Files.write(code.resolve(name+".py"),result.text.getBytes(StandardCharsets.UTF_8));
            eq(read(root.resolve("pc-code").resolve(name+".py")),result.text,name+" C# and Java identical Python");
            programs++;outcomes.add(name+"\tvalid");
        }
        Files.write(root.resolve("java-outcomes.tsv"),outcomes,StandardCharsets.UTF_8);
        eq("'\\U0001f422\\u0000\\'\\\\'",FlowPython.literal("🐢\0'\\"),"literal escaping");
        System.out.println("PYTHON_SOURCE_ANDROID PASS checks="+checks+" programs="+programs);
    }
}
