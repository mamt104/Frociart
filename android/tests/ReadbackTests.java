package it.frociart.app;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import static it.frociart.app.FlowModel.*;

/** Final leg of Android -> actual C# desktop Save -> Android interoperability. */
public final class ReadbackTests {
    public static void main(String[] args)throws Exception {
        Document d=FlowCodec.decode(Files.readString(Paths.get(args[0],"desktop-roundtrip.frociart"),StandardCharsets.UTF_8));
        Node n=d.find("check");
        if(!n.trueLabel.equals("Divisibile")||!n.falseLabel.equals("Resto"))throw new AssertionError("Branch labels lost in desktop roundtrip");
        if(!n.trueColor.equals("#123AbC")||!n.falseColor.equals("#CDEF12"))throw new AssertionError("Branch colors lost in desktop roundtrip");
        FlowEngine.Runner r=new FlowEngine.Runner(d);int guard=0;
        while(r.state!=FlowEngine.State.COMPLETED&&r.state!=FlowEngine.State.ERROR&&guard++<30){if(r.state==FlowEngine.State.WAITING)r.submitInput("4");else r.step();}
        if(r.state!=FlowEngine.State.COMPLETED||r.output.size()!=1||!r.output.get(0).equals("Pari"))throw new AssertionError("Desktop roundtrip changed execution");
        System.out.println("ANDROID READBACK TESTS PASSED: 3 checks");
    }
}
