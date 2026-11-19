using System;
internal static class RuntimeProbe {
    [STAThread]static int Main(){
        // Resolve both GUI frameworks without creating a window, changing user
        // settings or invoking any Frociart/project code. Host resolution checks
        // the exact same minimum/roll-forward contract as the distributed app.
        if(typeof(System.Windows.Forms.Form).Assembly==null||typeof(System.Windows.Media.DrawingGroup).Assembly==null)return 1;
        Console.Write("FROCIART_RUNTIME_READY");return 0;
    }
}
