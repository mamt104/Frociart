package it.frociart.app;
import java.util.*;import java.nio.file.*;import java.nio.charset.StandardCharsets;
public final class JavaScriptSourceTests {
    static int checks;
    static void eq(Object a,Object b,String label){checks++;if(!Objects.equals(a,b))throw new AssertionError(label);}
    static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8);}
    public static void main(String[] args)throws Exception{
        Path root=Paths.get(args[0]);List<Path> fixtures=new ArrayList<>();try(DirectoryStream<Path> stream=Files.newDirectoryStream(root.resolve("fixtures"),"*.frociart")){for(Path f:stream)fixtures.add(f);}Collections.sort(fixtures);
        int total=0,rejected=0;Path code=root.resolve("android-code");Files.createDirectories(code);
        for(boolean typed:new boolean[]{false,true}){String ext=typed?"cts":"cjs",language=typed?"typescript":"javascript";String runtime=read(Paths.get(args[1]).resolve(typed?"TypeScriptRuntime.cts":"JavaScriptRuntime.cjs"));int programs=0,invalid=0;
            for(Path f:fixtures){String name=f.getFileName().toString();FlowModel.Document d=FlowCodec.decode(read(f));String before=FlowCodec.encode(d);FlowSource.Program p=FlowSource.compile(d);
                if(!p.canGenerate()){boolean bad=false;try{FlowJavaScript.generate(p,runtime,typed);}catch(IllegalArgumentException ex){bad=true;}eq(true,bad,name+" invalid rejected");invalid++;continue;}
                String file="Case"+programs+"."+ext;FlowSourceText result=FlowJavaScript.generate(p,runtime,typed);eq(before,FlowCodec.encode(d),name+" unchanged");eq(language,result.language,name+" language");eq(result.text,FlowJavaScript.generate(p,runtime,typed).text,name+" deterministic");
                for(FlowSource.Scope scope:p.scopes){for(FlowSource.Block b:scope.blocks)eq(true,!result.forBlock(scope.id,b.id).isEmpty(),name+" block mapped");for(FlowSource.Block note:scope.notes)eq(true,!result.forBlock(scope.id,note.id).isEmpty(),name+" note mapped");}
                for(FlowSourceText.Line line:result.lines){eq(true,line.start>=0&&line.start+line.length<result.text.length(),name+" source range");eq('\n',result.text.charAt(line.start+line.length),name+" newline offset");}
                Files.write(code.resolve(file),result.text.getBytes(StandardCharsets.UTF_8));eq(read(root.resolve("pc-code").resolve(file)),result.text,name+" identical PC/Android "+ext);programs++;
            }total+=programs;rejected+=invalid;
        }
        System.out.println("JAVASCRIPT_SOURCE_ANDROID PASS assertions="+checks+" generated="+total+" invalid="+rejected);
    }
}
