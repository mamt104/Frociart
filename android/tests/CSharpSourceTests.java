package it.frociart.app;
import java.util.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;

public final class CSharpSourceTests {
    static int checks,programs,invalid;
    static void eq(Object a,Object b,String label){checks++;if(!Objects.equals(a,b))throw new AssertionError(label);}
    static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8);}
    public static void main(String[] args)throws Exception{
        eq("4.94065645841247E-324",FlowEngine.Value.number(Double.MIN_VALUE).format(),"minimum positive subnormal retains precision");
        eq("-4.94065645841247E-324",FlowEngine.Value.number(-Double.MIN_VALUE).format(),"minimum negative subnormal retains precision");
        Path corpus=Paths.get(args[0]),root=Paths.get(args[1]);String runtime=read(Paths.get(args[2]));Path code=root.resolve("android-code");Files.createDirectories(code);
        List<Path> fixtures=new ArrayList<>();try(DirectoryStream<Path> stream=Files.newDirectoryStream(corpus.resolve("fixtures"),"*.frociart")){for(Path f:stream)fixtures.add(f);}Collections.sort(fixtures);
        List<String> outcomes=new ArrayList<>();
        for(Path f:fixtures){String name=f.getFileName().toString().replace(".frociart","");FlowModel.Document d=FlowCodec.decode(read(f));String before=FlowCodec.encode(d);FlowSource.Program p=FlowSource.compile(d);
            if(!p.canGenerate()){boolean rejected=false;try{FlowCSharp.generate(p,runtime);}catch(IllegalArgumentException ex){rejected=true;}eq(true,rejected,name+" invalid project rejected");invalid++;outcomes.add(name+"\tinvalid");continue;}
            String className="Case"+programs;FlowSourceText result=FlowCSharp.generate(p,runtime,className);eq(before,FlowCodec.encode(d),name+" original unchanged");eq(result.text,FlowCSharp.generate(p,runtime,className).text,name+" deterministic");
            eq("csharp",result.language,name+" language");
            for(FlowSource.Scope scope:p.scopes)for(FlowSource.Block block:scope.blocks)eq(true,!result.forBlock(scope.id,block.id).isEmpty(),name+" mapped node");
            for(FlowSource.Scope scope:p.scopes)for(FlowSource.Block note:scope.notes)eq(true,!result.forBlock(scope.id,note.id).isEmpty(),name+" mapped note");
            for(FlowSourceText.Line line:result.lines){eq(true,line.start>=0&&line.start+line.length<result.text.length(),name+" valid source range");eq('\n',result.text.charAt(line.start+line.length),name+" newline offset");}
            Files.write(code.resolve(className+".cs"),result.text.getBytes(StandardCharsets.UTF_8));
            eq(read(root.resolve("pc-code").resolve(className+".cs")),result.text,name+" identical Windows and Android C# output");
            programs++;outcomes.add(name+"\t"+className);
        }
        Files.write(root.resolve("java-outcomes.tsv"),outcomes,StandardCharsets.UTF_8);
        System.out.println("CSHARP_SOURCE_ANDROID PASS checks="+checks+" programs="+programs+" invalid="+invalid);
    }
}
