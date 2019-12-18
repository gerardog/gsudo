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
using System.Threading;

namespace gsudo.Helpers
{
    static class ProcessExtensions
    {
        public static void SendCtrlC(this Process proc, bool sendSigBreak = false)
        {
            // Sending Ctrl-C in windows is tricky.
            // Your process must be attached to the target process console.
            // There is no way to do that without loosing your currently attached console, and that generates a lot of issues.
            // So the best we can do is create a new process that will attach and send Ctrl-C to the target process.

            using (var p = ProcessFactory.StartDetached
                (Process.GetCurrentProcess().MainModule.FileName, $"gsudoctrlc {proc.Id.ToString()}", Environment.CurrentDirectory, true))
            {
                p.WaitForExit();
            }
        }

        public static Process ParentProcess(this Process process)
        {
            try
            {
                return Process.GetProcessById(process.ParentProcessId());
            }
            catch
            {
                return null;
            }
        }

        public static int ParentProcessId(this Process process) // ExcludingShim
        {
            var parentId = ParentProcessId(process.Id);
            var parent = Process.GetProcessById(parentId);

            // workaround for chocolatey shim.
            if (Path.GetFileName(parent.MainModule.FileName).In("gsudo.exe", "sudo.exe"))
            {
                return ParentProcessId(parentId);
            }
            return parentId;
        }

        public static int GetClientProcessId(this System.IO.Pipes.NamedPipeServerStream pipeServer)
        {
            UInt32 nProcID;
            IntPtr hPipe = pipeServer.SafePipeHandle.DangerousGetHandle();
            if (Native.ProcessApi.GetNamedPipeClientProcessId(hPipe, out nProcID))
                return (int)nProcID;
            return 0;
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

        public static void Terminate(this Process process)
        {
            if (process.HasExited) return;

            Logger.Instance.Log($"Killing process {process.Id} {process.ProcessName}", LogLevel.Debug);

            process.SendCtrlC(false);
            process.CloseMainWindow();

            process.WaitForExit(300);

            if (!process.HasExited)
            {
                process.Kill();
                /*
                var p = Process.Start(new ProcessStartInfo()
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {process.Id} /T",    
                    WindowStyle = ProcessWindowStyle.Hidden

                });
                p.WaitForExit();
                */
            }
        }

        /// <summary>
        /// Get an AutoResetEvent that signals when the process exits
        /// </summary>
        public static AutoResetEvent GetWaitHandle(this Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.Handle, ownsHandle: false)
            };

    }
}
