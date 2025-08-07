using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TerraFX.Interop.Windows;

namespace JustBackup;
internal static partial class Interop
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetPriorityClass(nint handle, ProcessPriorityClass priorityClass);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetThreadPriority(nint handle, ThreadPriorityClass priorityClass);

    public enum ProcessPriorityClass : uint
    {
        ABOVE_NORMAL_PRIORITY_CLASS = 0x8000,
        BELOW_NORMAL_PRIORITY_CLASS = 0x4000,
        HIGH_PRIORITY_CLASS = 0x80,
        IDLE_PRIORITY_CLASS = 0x40,
        NORMAL_PRIORITY_CLASS = 0x20,
        PROCESS_MODE_BACKGROUND_BEGIN = 0x100000,// 'Windows Vista/2008 and higher
        PROCESS_MODE_BACKGROUND_END = 0x200000,//   'Windows Vista/2008 and higher
        REALTIME_PRIORITY_CLASS = 0x100
    }

    public enum ThreadPriorityClass : int
    {
        THREAD_MODE_BACKGROUND_BEGIN = 0x00010000,
        THREAD_MODE_BACKGROUND_END = 0x00020000
    }



    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentThread")]
    public static partial HANDLE GetCurrentThread();

    [LibraryImport("kernel32.dll", EntryPoint = "GetLastError")]
    public static partial uint GetLastError();
}
