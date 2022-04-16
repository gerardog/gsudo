using static gsudo.Native.ProcessApi;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using gsudo.Native;
using System.Linq;

namespace gsudo.Helpers
{
    public static class ProcessHelper
    {
        private static int? _cacheGetCurrentIntegrityLevelCache;
        private static bool? _cacheIsAdmin;

        public static string GetOwnExeName()
        {
            return SymbolicLinkSupport.ResolveSymbolicLink(System.Reflection.Assembly.GetEntryAssembly().Location);
        }

        public static string GetExeName(this Process process)
        {
            var exeName = string.Empty;
            try
            {
                exeName = process.ProcessName;
                exeName = process.MainModule?.FileName ?? exeName;
            }
            catch { }
            return exeName;
        }

        public static string GetProcessUser(this Process process)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                OpenProcessToken(process.Handle, 8, out processHandle);
                WindowsIdentity wi = new WindowsIdentity(processHandle);
                string user = wi.Name;
                return user; //.Contains(@"\") ? user.Substring(user.IndexOf(@"\", StringComparison.OrdinalIgnoreCase) + 1) : user;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        public static Process GetShellProcess(this Process process)
        {
            if (ShellHelper.InvokingShell!=Shell.Bash)
                return GetParentProcessExcludingShim(process);

            // Unable to get a caller pid for the cache.
            // This is common in MSYS/git-bash/cygwin
            // Fallback to first process attached to the current console.
            var pids = ConsoleHelper.GetConsoleAttachedPids();
            return Process.GetProcessById((int) pids[pids.Length - 1]);
        }

        public static int GetCacheableRootProcessId(this Process process)
        {
            var parent = GetParentProcessId(process);

            if (ShellHelper.InvokingShell == Shell.Bash)
            {
                if (parent == 0) 
                    return process.Id;

                try
                {
                    var pparent = Process.GetProcessById(parent);
                    if (pparent.MainModule.FileName.EndsWith("\\BASH.EXE", StringComparison.OrdinalIgnoreCase))
                    {
                        var grandparent = GetParentProcess(pparent);
                        if (!grandparent.MainModule.FileName.EndsWith("\\BASH.EXE", StringComparison.OrdinalIgnoreCase))
                        {
                            return parent;
                        }
                        return GetCacheableRootProcessId(pparent);
                    }
                }
                catch
                { }

                return parent;

            }
        
            return GetParentProcessIdExcludingShim(process);
        }

        public static Process GetParentProcessExcludingShim(this Process process)
        {
            try
            {
                var parentPid = ProcessHelper.GetParentProcessIdExcludingShim(process);
                if (parentPid == 0)
                    return null;
                return Process.GetProcessById(parentPid);
            }
            catch
            {
                return null;
            }
        }

        public static int GetParentProcessIdExcludingShim(this Process process)
        {
            var parentId = GetParentProcessId(process);
            Process parent;
            try
            {
                parent = Process.GetProcessById(parentId);
                var filename = parent.MainModule.FileName;

                if (IsShim(filename))
                    return GetParentProcessIdExcludingShim(parent);
            }
            catch (Exception ex)
            {
                // For example: System.ArgumentException: Process with an Id of 18312 is not running.
                return parentId;
            }

            return parentId;
        }

        public static int GetParentProcessId(int pid)
        {
            IntPtr hProcess = ProcessApi.OpenProcess(ProcessApi.PROCESS_QUERY_INFORMATION, true, (uint)pid);

            try
            {
                return GetParentProcessId(hProcess);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        public static int GetParentProcessId(this Process process) => GetParentProcessId(process.Handle);

        public static Process GetParentProcess(this Process process) => Process.GetProcessById(GetParentProcessId(process.Handle));

        public static int GetParentProcessId(IntPtr hProcess)
        {
            if (hProcess == IntPtr.Zero) return 0;

            var pbi = new NtDllApi.PROCESS_BASIC_INFORMATION();
            int returnLength;

            if (NtDllApi.NativeMethods.NtQueryInformationProcess(hProcess, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength) != 0)
                return 0;

            return (int)pbi.InheritedFromUniqueProcessId;
        }

        /// <summary>
        /// Gets the PID that will be allowed in the cache.
        /// </summary>
        internal static int GetCallerPid()
        {
            return Process.GetCurrentProcess().GetCacheableRootProcessId();
        }

        public static bool IsHighIntegrity()
        {
            return ProcessHelper.GetCurrentIntegrityLevel() >= (int)IntegrityLevel.High;
        }

        public static bool IsAdministrator()
        {
            if (_cacheIsAdmin.HasValue) return _cacheIsAdmin.Value;

            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                _cacheIsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                return _cacheIsAdmin.Value;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsMemberOfLocalAdmins()
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            var claims = principal.Claims;
            return claims.Any(c => c.Value == "S-1-5-32-544");
        }

        public static void Terminate(this Process process)
        {
            if (process.HasExited) return;

            Logger.Instance.Log($"Killing process {process.Id} {process.ProcessName}", LogLevel.Debug);

            Commands.CtrlCCommand.Invoke(process.Id);
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
        public static AutoResetEvent GetProcessWaitHandle(this Process process) =>
            GetProcessWaitHandle(process.Handle);

        public static AutoResetEvent GetProcessWaitHandle(IntPtr processHandle) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(processHandle, ownsHandle: false)
            };

        public static AutoResetEvent GetProcessWaitHandle(this SafeProcessHandle processHandle) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(processHandle.DangerousGetHandle(), ownsHandle: false)
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
        static internal int GetCurrentIntegrityLevel()
        {
            if (_cacheGetCurrentIntegrityLevelCache.HasValue) return _cacheGetCurrentIntegrityLevelCache.Value;

            return GetProcessIntegrityLevel(ProcessApi.GetCurrentProcess());
        }

        static internal int GetProcessIntegrityLevel(IntPtr processHandle)
        {
            /*
             * https://docs.microsoft.com/en-us/previous-versions/dotnet/articles/bb625963(v=msdn.10)?redirectedfrom=MSDN
             * https://support.microsoft.com/en-us/help/243330/well-known-security-identifiers-in-windows-operating-systems
            S-1-16-0		Untrusted Mandatory Level	An untrusted integrity level.
            S-1-16-4096		Low Mandatory Level	A low integrity level.
            S-1-16-8192		Medium Mandatory Level	A medium integrity level.
            S-1-16-8448		Medium Plus Mandatory Level	A medium plus integrity level.
            S-1-16-12288    	High Mandatory Level	A high integrity level.
            S-1-16-16384	    System Mandatory Level	A system integrity level.
            S-1-16-20480	    Protected Process Mandatory Level	A protected-process integrity level.
            S-1-16-28672	    Secure Process Mandatory Level	A secure process integrity level.
            */
            int IL = -1;
            //SafeWaitHandle hToken = null;
            IntPtr hToken = IntPtr.Zero;

            int cbTokenIL = 0;
            IntPtr pTokenIL = IntPtr.Zero;

            try
            {
                // Open the access token of the current process with TOKEN_QUERY.
                if (!OpenProcessToken(processHandle,
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

            return (_cacheGetCurrentIntegrityLevelCache = IL).Value;
        }

        /// <summary>
        /// Naive Shim Detection.
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private static bool IsShim(string fileName)
        {
            try
            {
                if (!fileName.Equals(ProcessHelper.GetOwnExeName(), StringComparison.OrdinalIgnoreCase)
                    && (fileName.EndsWith("\\SUDO.EXE", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith("\\GSUDO.EXE", StringComparison.OrdinalIgnoreCase)
                        ))
                    return true;
            }
            catch { } // fails to get parent.MainModule if our parent process is elevated and we are not.

            return false;
        }
    }
}
