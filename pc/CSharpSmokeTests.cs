using System;using System.IO;using Frociart;
internal static class CSharpSmokeTests {
 static int Main(string[] args){try{var project=FlowPseudocode.Decode("x=2\nSTAMPA x+1");var source=FlowCSharp.Generate(FlowSource.Compile(project));File.WriteAllText(Path.Combine(args[0],"Programma.cs"),source.Text,new System.Text.UTF8Encoding(false));Console.WriteLine("CSHARP_SMOKE_SOURCE PASS "+source.Text.Length);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}