// Runs CPython in-process: cancelling py.exe cannot leave an orphan Python backup.
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
internal static class PythonLauncher
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetDllDirectory(string path);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)]static extern IntPtr GetProcAddress(IntPtr module,string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int PyMain(int argc,IntPtr argv);
    static int Main(string[] args)
    {
        var memory=new List<IntPtr>();IntPtr argv=IntPtr.Zero;
        try {
            string runtime=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime");
            string dll=Path.Combine(runtime,"python313.dll");
            if(!File.Exists(dll)){Console.Error.WriteLine("Die mitgelieferte Python-Laufzeit fehlt. Windows-ZIP vollstaendig entpacken.");return 1;}
            SetDllDirectory(runtime);IntPtr library=LoadLibrary(dll);
            if(library==IntPtr.Zero)throw new InvalidOperationException("Python konnte nicht geladen werden.");
            var entry=GetProcAddress(library,"Py_Main");if(entry==IntPtr.Zero)throw new InvalidOperationException("Python-Einstieg fehlt.");
            var list=new List<string>{Path.Combine(runtime,"python.exe")};
            if(args.Length>0&&args[0]=="-3")list.AddRange(args.Skip(1));else list.AddRange(args);
            argv=Marshal.AllocHGlobal(IntPtr.Size*(list.Count+1));
            for(int i=0;i<list.Count;i++){var p=Marshal.StringToHGlobalUni(list[i]);memory.Add(p);Marshal.WriteIntPtr(argv,i*IntPtr.Size,p);}
            Marshal.WriteIntPtr(argv,list.Count*IntPtr.Size,IntPtr.Zero);
            var run=(PyMain)Marshal.GetDelegateForFunctionPointer(entry,typeof(PyMain));
            return run(list.Count,argv);
        }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
        finally{foreach(var p in memory)Marshal.FreeHGlobal(p);if(argv!=IntPtr.Zero)Marshal.FreeHGlobal(argv);}
    }
}
