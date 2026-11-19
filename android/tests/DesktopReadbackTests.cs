using System;
using System.Linq;
using Frociart;

// Compile with the actual Frociart/Core.cs: verifies Android JSON -> desktop decoder and interpreter.
internal static class DesktopReadbackTests
{
    private static int count;
    private static void Check(bool ok,string message) { count++;if(!ok)throw new Exception(message); }
    public static void Main(string[] args)
    {
        Diagram parity=DiagramIO.Load(System.IO.Path.Combine(args[0],"android-custom.frociart"));
        Check(parity.Version==1,"format version");
        Check(parity.Nodes.Count==6 && parity.Edges.Count==6,"model shape");
        Check(!Validator.Validate(parity).Any(i=>i.Severity==Severity.Error),"Android project validates on desktop");
        FlowNode decision = parity.Nodes.Single(n => n.Kind == NodeKind.Decision);
        Check(decision.TrueLabel == "Divisibile" && decision.FalseLabel == "Resto", "desktop retains Android branch labels");
        Check(decision.TrueColor == "#123AbC" && decision.FalseColor == "#CDEF12", "desktop retains Android branch colors");
        Diagram cloned = DiagramIO.Clone(parity);
        Check(cloned.Nodes.Single(n => n.Kind == NodeKind.Decision).TrueColor == "#123AbC", "desktop snapshot preserves branch metadata");
        string saved = System.IO.Path.Combine(args[0], "desktop-roundtrip.frociart");
        DiagramIO.Save(saved, cloned);
        Diagram reloaded = DiagramIO.Load(saved);
        Check(reloaded.Nodes.Single(n => n.Kind == NodeKind.Decision).TrueLabel == "Divisibile", "desktop save and reopen preserve branch metadata");
        Interpreter runner=new Interpreter(parity);
        int guard=0;
        while(runner.State!=RuntimeState.Completed && runner.State!=RuntimeState.Error && guard++<30)
        {
            if(runner.State==RuntimeState.WaitingForInput)runner.ProvideInput("4");else runner.Step();
        }
        Check(runner.State==RuntimeState.Completed,"Android project runs on desktop");
        Check(runner.Output.Count==1 && runner.Output[0]=="Pari","branch metadata does not change desktop execution");
        Diagram unicode=DiagramIO.Load(System.IO.Path.Combine(args[0],"android-unicode.frociart"));
        Check(unicode.Title=="Test \"unicode\" \\ 🌙","Unicode title roundtrip");
        Interpreter textRunner=new Interpreter(unicode);
        while(textRunner.State!=RuntimeState.Completed && textRunner.State!=RuntimeState.Error)textRunner.Step();
        Check(textRunner.State==RuntimeState.Completed,"Unicode project runs on desktop");
        Check(textRunner.Output[0]=="Caffè 🌙\n\t","Unicode and nested escapes roundtrip");
        Check(ValueFormatter.Format(ExpressionLanguage.Evaluate("round(1.005,2)", null)) == "1", "desktop rounding boundary matches Android");
        Check(ValueFormatter.Format(ExpressionLanguage.Evaluate("round(-1.005,2)", null)) == "-1", "desktop negative rounding boundary matches Android");
        Check(ValueFormatter.Format(ExpressionLanguage.Evaluate("round(9999999999999998,0)", null)) == "1E+16", "desktop large rounding value matches Android");
        Console.WriteLine("DESKTOP READBACK TESTS PASSED: "+count+" checks");
    }
}
