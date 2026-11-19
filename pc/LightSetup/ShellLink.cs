using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Frociart.LightSetup {
    // Read the actual .lnk through the Windows shell, not by matching file text.
    internal static class ShellLink {
        internal sealed class Target {internal string Path,Arguments,Directory;}
        internal static Target Read(string path){
            var file=new FileInfo(path);
            if(!file.Exists||file.Length<=0||file.Length>1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Il collegamento Start non è valido.");
            object shell=new ShellLinkObject();
            try {
                ((IPersistFile)shell).Load(file.FullName,0);
                var link=(IShellLinkW)shell;var target=new StringBuilder(32768);var args=new StringBuilder(32768);var directory=new StringBuilder(32768);
                link.GetPath(target,target.Capacity,IntPtr.Zero,4);link.GetArguments(args,args.Capacity);link.GetWorkingDirectory(directory,directory.Capacity);
                return new Target{Path=target.ToString(),Arguments=args.ToString(),Directory=directory.ToString()};
            }finally{Marshal.FinalReleaseComObject(shell);}
        }
        [ComImport,Guid("00021401-0000-0000-C000-000000000046")]sealed class ShellLinkObject { }
        [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW {
            void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder file,int count,IntPtr data,uint flags);
            void GetIDList(out IntPtr list);void SetIDList(IntPtr list);
            void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int count);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);
            void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder directory,int count);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string directory);
            void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder arguments,int count);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string arguments);
            void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int command);void SetShowCmd(int command);
            void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int count,out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string path,int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr window,uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
        }
    }
}
