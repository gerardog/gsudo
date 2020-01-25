using static gsudo.Native.ProcessApi;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace gsudo.Helpers
{
    public static class ProcessExtensions
    {
        public static void SendCtrlC(this Process proc)
        {
            // Sending Ctrl-C in windows is tricky.
            // Your process must be attached to the target process console.
            // There is no way to do that without loosing your currently attached console, and that generates a lot of issues.
            // So the best we can do is create a new process that will attach and send Ctrl-C to the target process.

            using (var p = ProcessFactory.StartDetached
                (Process.GetCurrentProcess().MainModule.FileName, $"gsudoctrlc {proc.Id.ToString(CultureInfo.InvariantCulture)}", Environment.CurrentDirectory, true))
            {
                p.WaitForExit();
            }
        }

        public static Process ParentProcess(this Process process)
        {
            try
            {
                var parentPid = process.ParentProcessId();
                if (parentPid == 0)
                    return null;
                return Process.GetProcessById(parentPid);
            }
            catch
            {
                return null;
            }
        }

        public static int ParentProcessId(this Process process) // ExcludingShim
        {
            var parentId = ParentProcessId(process.Id);
            Process parent;
            try
            {
                parent = Process.GetProcessById(parentId);
            }
            catch 
            {
                return 0;
            }

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
                if (Environment.GetEnvironmentVariable("GSUDO-TESTMODE-NOELEVATE") == "1") return false; // special mode for unit tests

                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

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
