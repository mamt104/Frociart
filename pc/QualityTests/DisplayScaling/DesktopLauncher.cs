using System;using System.Text;using System.ComponentModel;using System.Runtime.InteropServices;
// QA only: create the process on its private desktop before CLR/STA creates
// any native windows. Never calls SwitchDesktop or changes user settings.
class DesktopLauncher {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Startup {public int Size;public string Reserved,Desktop,Title;public int X,Y,Width,Height,CharsX,CharsY,Fill,Flags;public short Show,ReservedSize;public IntPtr ReservedBytes,Input,Output,Error;}
 [StructLayout(LayoutKind.Sequential)]struct ProcessInfo {public IntPtr Process,Thread;public int ProcessId,ThreadId;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
 [DllImport("user32.dll")]static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string app,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref Startup start,out ProcessInfo process);
 [DllImport("kernel32.dll")]static extern IntPtr GetStdHandle(int id);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint ms);
 [DllImport("kernel32.dll")]static extern bool GetExitCodeProcess(IntPtr h,out uint code);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 static string Quote(string value){if(value.IndexOf('"')>=0||value.EndsWith("\\"))throw new ArgumentException("Unsupported test argument");return "\""+value+"\"";}
 static int Main(string[] args){IntPtr desktop=IntPtr.Zero;ProcessInfo child=new ProcessInfo();try{
  string name="FrociartQA_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+Guid.NewGuid().ToString("N");desktop=CreateDesktop(name,IntPtr.Zero,IntPtr.Zero,0,0x01FF,IntPtr.Zero);if(desktop==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
  var command=new StringBuilder();foreach(var arg in args){if(command.Length>0)command.Append(' ');command.Append(Quote(arg));}
  var start=new Startup{Size=Marshal.SizeOf(typeof(Startup)),Desktop="WinSta0\\"+name,Flags=0x0100,Input=GetStdHandle(-10),Output=GetStdHandle(-11),Error=GetStdHandle(-12)};
  if(!CreateProcess(args[0],command,IntPtr.Zero,IntPtr.Zero,true,0x08000000,IntPtr.Zero,Environment.CurrentDirectory,ref start,out child))throw new Win32Exception(Marshal.GetLastWin32Error());
  Console.WriteLine("PRIVATE_NATIVE_DESKTOP child created; user input desktop unchanged");Console.Out.Flush();
  if(WaitForSingleObject(child.Process,240000)!=0)throw new Exception("Owned native test did not finish in 240 seconds");uint code;if(!GetExitCodeProcess(child.Process,out code))throw new Win32Exception();return(int)code;
 }catch(Exception e){Console.WriteLine(e);return 1;}finally{if(child.Thread!=IntPtr.Zero)CloseHandle(child.Thread);if(child.Process!=IntPtr.Zero)CloseHandle(child.Process);if(desktop!=IntPtr.Zero)CloseDesktop(desktop);}}
}
