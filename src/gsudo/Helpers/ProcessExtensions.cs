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
using gsudo.Native;

namespace gsudo.Helpers
{
    public static class ProcessExtensions
    {
        public static void SendCtrlC(this Process proc, bool sendSigBreak = false)
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

        /// <summary>
        /// The function gets the integrity level of the current process.
        /// </summary>
        /// <returns>
        /// Returns the integrity level of the current process. It is usually one of
        /// these values:
        ///
        ///    SECURITY_MANDATORY_UNTRUSTED_RID - means untrusted level
        ///    SECURITY_MANDATORY_LOW_RID - means low integrity level.
        ///    SECURITY_MANDATORY_MEDIUM_RID - means medium integrity level.
        ///    SECURITY_MANDATORY_HIGH_RID - means high integrity level.
        ///    SECURITY_MANDATORY_SYSTEM_RID - means system integrity level.
        ///
        /// </returns>
        /// <exception cref="System.ComponentModel.Win32Exception">
        /// When any native Windows API call fails, the function throws a Win32Exception
        /// with the last error code.
        /// </exception>
        static internal int GetProcessIntegrityLevel()
        {
            /*
             * https://docs.microsoft.com/en-us/previous-versions/dotnet/articles/bb625963(v=msdn.10)?redirectedfrom=MSDN
             * https://support.microsoft.com/en-us/help/243330/well-known-security-identifiers-in-windows-operating-systems
            S-1-16-0		Untrusted Mandatory Level	An untrusted integrity level.
            S-1-16-4096		Low Mandatory Level	A low integrity level.
            S-1-16-8192		Medium Mandatory Level	A medium integrity level.
            S-1-16-8448		Medium Plus Mandatory Level	A medium plus integrity level.
            S-1-16-12288	High Mandatory Level	A high integrity level.
            S-1-16-16384	System Mandatory Level	A system integrity level.
            S-1-16-20480	Protected Process Mandatory Level	A protected-process integrity level.
            S-1-16-28672	Secure Process Mandatory Level	A secure process integrity level.
            */
            int IL = -1;
            //SafeWaitHandle hToken = null;
            IntPtr hToken = IntPtr.Zero;

            int cbTokenIL = 0;
            IntPtr pTokenIL = IntPtr.Zero;

            try
            {
                // Open the access token of the current process with TOKEN_QUERY.
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                    Native.TokensApi.TOKEN_QUERY, out hToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // Then we must query the size of the integrity level information
                // associated with the token. Note that we expect GetTokenInformation
                // to return false with the ERROR_INSUFFICIENT_BUFFER error code
                // because we've given it a null buffer. On exit cbTokenIL will tell
                // the size of the group information.
                if (!Native.TokensApi.GetTokenInformation(hToken,
                    Native.TokensApi.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, IntPtr.Zero, 0,
                    out cbTokenIL))
                {
                    int error = Marshal.GetLastWin32Error();
                    const int ERROR_INSUFFICIENT_BUFFER = 0x7a;
                    if (error != ERROR_INSUFFICIENT_BUFFER)
                    {
                        // When the process is run on operating systems prior to
                        // Windows Vista, GetTokenInformation returns false with the
                        // ERROR_INVALID_PARAMETER error code because
                        // TokenIntegrityLevel is not supported on those OS's.
                        throw new Win32Exception(error);
                    }
                }

                // Now we allocate a buffer for the integrity level information.
                pTokenIL = Marshal.AllocHGlobal(cbTokenIL);
                if (pTokenIL == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // Now we ask for the integrity level information again. This may fail
                // if an administrator has added this account to an additional group
                // between our first call to GetTokenInformation and this one.
                if (!TokensApi.GetTokenInformation(hToken,
                    TokensApi.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, pTokenIL, cbTokenIL,
                    out cbTokenIL))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // Marshal the TOKEN_MANDATORY_LABEL struct from native to .NET object.
                TokensApi.TOKEN_MANDATORY_LABEL tokenIL = (TokensApi.TOKEN_MANDATORY_LABEL)
                    Marshal.PtrToStructure(pTokenIL, typeof(TokensApi.TOKEN_MANDATORY_LABEL));

                IntPtr pIL = TokensApi.GetSidSubAuthority(tokenIL.Label.Sid, 0);
                IL = Marshal.ReadInt32(pIL);
            }
            finally
            {
                // Centralized cleanup for all allocated resources. Clean up only
                // those which were allocated, and clean them up in the right order.

                if (hToken != IntPtr.Zero)
                {
                    CloseHandle(hToken);
//                    Marshal.FreeHGlobal(hToken);
//                    hToken.Close();
//                    hToken = null;
                }

                if (pTokenIL != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pTokenIL);
                    pTokenIL = IntPtr.Zero;
                    cbTokenIL = 0;
                }
            }

            return IL;
        }
    }
}
