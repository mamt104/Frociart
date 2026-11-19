using System;
using System.Diagnostics;
using System.Threading;
internal static class ExeSmoke {
    static int Main(string[] args){
        var start=new ProcessStartInfo(args[0],"--avvio-sicuro \""+args[1]+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        using(var app=Process.Start(start)){
            for(int i=0;i<100&&!app.HasExited;i++){app.Refresh();if(app.MainWindowHandle!=IntPtr.Zero&&app.MainWindowTitle.Contains("Nuovo diagramma"))break;Thread.Sleep(100);}
            app.Refresh();
            if(app.HasExited||app.MainWindowHandle==IntPtr.Zero){Console.WriteLine("FAIL no main window");if(!app.HasExited)app.Kill();return 1;}
            Console.WriteLine("WINDOW "+app.MainWindowTitle);
            bool correct=app.MainWindowTitle.Contains("Frociart 1.11.2")&&app.MainWindowTitle.Contains("Nuovo diagramma");
            Console.WriteLine("VERSION "+correct);
            bool close=app.CloseMainWindow();Console.WriteLine("CLOSE_REQUEST "+close);
            if(!app.WaitForExit(8000)){app.Kill();Console.WriteLine("FAIL close timeout");return 1;}
            Console.WriteLine("EXIT "+app.ExitCode);
            return correct&&close&&app.ExitCode==0?0:1;
        }
    }
}
