using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Security.Principal;

namespace gsudo.Helpers
{
    static class ProcessExtensions
    {
        public static void SendCtrlC(this Process proc, bool sendSigBreak = false)
        {
            var signal = sendSigBreak ? "SIGBREAK" : "SIGINT";

            using (var p = ProcessStarter.StartDetached
                ("cmd.exe", "/c \"" 
                + Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "windows-kill.exe") 
                + $"\" -{signal} {proc.Id.ToString()}", Environment.CurrentDirectory, true))
            {
                p.WaitForExit();
            }
        }

        public static int ParentProcessId(this Process process) // ExcludingShim
        {
            var parentId = ParentProcessId(process.Id);
            var parent = Process.GetProcessById(parentId);

            // workaround for chocolatey shim.
            if (Path.GetFileName(parent.MainModule.FileName).In("gsudo.exe","sudo.exe"))
            {
                return ParentProcessId(parentId);
            }
            return parentId;
        }

        public static int ParentProcessId(int Id)
        {
            PROCESSENTRY32 pe32 = new PROCESSENTRY32 { };
            pe32.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            using (var hSnapshot = CreateToolhelp32Snapshot(SnapshotFlags.Process, (uint)Id))
            {
                if (hSnapshot.IsInvalid)
                    throw new Win32Exception();

                if (!Process32First(hSnapshot, ref pe32))
                {
                    int errno = Marshal.GetLastWin32Error();
                    if (errno == ERROR_NO_MORE_FILES)
                        return -1;
                    throw new Win32Exception(errno);
                }
                do
                {
                    if (pe32.th32ProcessID == (uint)Id)
                        return (int)pe32.th32ParentProcessID;
                } while (Process32Next(hSnapshot, ref pe32));
            }
            return -1;
        }

        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Win32 api

        private const int ERROR_NO_MORE_FILES = 0x12;
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeSnapshotHandle CreateToolhelp32Snapshot(SnapshotFlags flags, uint id);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

        [Flags]
        private enum SnapshotFlags : uint
        {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            All = (HeapList | Process | Thread | Module),
            Inherit = 0x80000000,
            NoHeaps = 0x40000000
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        };
        [SuppressUnmanagedCodeSecurity, HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeSnapshotHandle : SafeHandleMinusOneIsInvalid
        {
            internal SafeSnapshotHandle() : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            internal SafeSnapshotHandle(IntPtr handle) : base(true)
            {
                base.SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success), DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
            private static extern bool CloseHandle(IntPtr handle);
        }

        #endregion
    }
}
