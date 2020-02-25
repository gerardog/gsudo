using gsudo.Native;
using System;
using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static gsudo.Native.ProcessApi;
using static gsudo.Native.TokensApi;

namespace gsudo.Helpers
{
	//https://csharp.hotexamples.com/examples/CSCreateLowIntegrityProcess/PROCESS_INFORMATION/-/php-process_information-class-examples.html
	
    static class ProcessFactory
    {
        public static Process StartElevatedDetached(string filename, string arguments, bool hidden)
        {
            Logger.Instance.Log($"Elevating process: {filename} {arguments}", LogLevel.Debug);

            var process = new Process();
            process.StartInfo = new ProcessStartInfo(filename, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            };

            if (Environment.GetEnvironmentVariable("GSUDO-TESTMODE-NOELEVATE") == "1")
                process.StartInfo.Verb = null;

            if (hidden)
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            else
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;

            process.Start();
            return process;
        }

        public static Process StartInProcessRedirected(string fileName, string arguments, string startFolder)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(fileName)
            {
                Arguments = arguments,
                WorkingDirectory = startFolder,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            process.Start();
            return process;
        }
        
        public static Process StartInProcessAtached(string filename, string arguments)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(filename)
            {
                Arguments = arguments,
                UseShellExecute = false,
            };
            process.Start();
            return process;
        }

        public static Process StartDetached(string filename, string arguments, string startFolder, bool hidden = true)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(filename)
            {
                Arguments = arguments,
                UseShellExecute = true,
                WorkingDirectory = startFolder,
            };

            if (hidden)
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
            }
            else
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                process.Start();
                
                for (int i = 0; process.MainWindowHandle == IntPtr.Zero && i<30; i++)
                    System.Threading.Thread.Sleep(10);

                // set user the focus to the window, if there is one.
                if (process.MainWindowHandle != IntPtr.Zero)
                    _ = WindowApi.SetForegroundWindow(process.MainWindowHandle);
            }

            return process;
        }

        public static bool IsWindowsApp(string exe)
        {
            var path = FindExecutableInPath(ArgumentsHelper.UnQuote(exe));
            var shinfo = new Native.FileApi.SHFILEINFO();
            const int SHGFI_EXETYPE = 0x000002000;
            var fileInfo = Native.FileApi.SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_EXETYPE);
            var retval = (fileInfo.ToInt64() & 0xFFFF0000) >0 ;
            Logger.Instance.Log($"IsWindowsApp(\"{exe}\") = {retval} (\"{path}\")", LogLevel.Debug);
            return retval;
        }

        public static string FindExecutableInPath(string exe)
        {
            exe = Environment.ExpandEnvironmentVariables(exe);

            try
            {

                if (File.Exists(exe))
                {
                    return Path.GetFullPath(exe);
                }

                if (string.IsNullOrEmpty(Path.GetDirectoryName(exe)))
                {
                    exe = Path.GetFileName(exe);

                    var validExtensions = Environment.GetEnvironmentVariable("PATHEXT", EnvironmentVariableTarget.Process)
                        .Split(';');

                    var possibleNames = new List<string>();

                    if (Path.GetExtension(exe).In(validExtensions))
                        possibleNames.Add(exe);

                    possibleNames.AddRange(validExtensions.Select((ext) => exe + ext));

                    var paths = new List<string>();
                    paths.Add(Environment.CurrentDirectory);
                    paths.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'));

                    foreach (string test in paths)
                    {
                        foreach (string file in possibleNames)
                        {
                            string path = Path.Combine(test, file);
                            if (!String.IsNullOrEmpty(path) && File.Exists(path))
                                return Path.GetFullPath(path);
                        }
                    }
                }
                return null;
            }
            catch
            { 
                return null;
            }
        }

        #region PseudoConsole ConPty
        public static PseudoConsole.PseudoConsoleProcess StartPseudoConsole(string command, IntPtr attributes, IntPtr hPC, string startFolder)
        {
            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, command, startFolder);
            return new PseudoConsole.PseudoConsoleProcess(startupInfo, processInfo);
        }

        private static STARTUPINFOEX ConfigureProcessThread(IntPtr hPC, IntPtr attributes)
        {
            // this method implements the behavior described in https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session#preparing-for-creation-of-the-child-process

            var lpSize = IntPtr.Zero;
            var success = InitializeProcThreadAttributeList(
                lpAttributeList: IntPtr.Zero,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );
            if (success || lpSize == IntPtr.Zero) // we're not expecting `success` here, we just want to get the calculated lpSize
            {
                throw new InvalidOperationException("Could not calculate the number of bytes for the attribute list. " + Marshal.GetLastWin32Error());
            }

            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

            success = InitializeProcThreadAttributeList(
                lpAttributeList: startupInfo.lpAttributeList,
                dwAttributeCount: 1,
                dwFlags: 0,
                lpSize: ref lpSize
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not set up attribute list. " + Marshal.GetLastWin32Error());
            }

            success = UpdateProcThreadAttribute(
                lpAttributeList: startupInfo.lpAttributeList,
                dwFlags: 0,
                attribute: attributes,
                lpValue: hPC,
                cbSize: (IntPtr)IntPtr.Size,
                lpPreviousValue: IntPtr.Zero,
                lpReturnSize: IntPtr.Zero
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not set pseudoconsole thread attribute. " + Marshal.GetLastWin32Error());
            }

            return startupInfo;
        }

        private static PROCESS_INFORMATION RunProcess(ref STARTUPINFOEX sInfoEx, string commandLine, string startFolder)
        {
            int securityAttributeSize = Marshal.SizeOf<SECURITY_ATTRIBUTES>();
            var pSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var tSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var success = CreateProcess(
                lpApplicationName: null,
                lpCommandLine: commandLine,
                lpProcessAttributes: ref pSec,
                lpThreadAttributes: ref tSec,
                bInheritHandles: false,
                dwCreationFlags: EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment: IntPtr.Zero,
                lpCurrentDirectory: startFolder,
                lpStartupInfo: ref sInfoEx,
                lpProcessInformation: out PROCESS_INFORMATION pInfo
            );
            if (!success)
            {
                throw new InvalidOperationException("Could not create process. " + Marshal.GetLastWin32Error());
            }

            return pInfo;
        }
        #endregion

        #region RunAsUser

        public static SafeProcessHandle StartAsSystem(string appToRun, string args, string startupFolder, bool hidden)
        {
            Logger.Instance.Log($"{nameof(StartAsSystem)}: {appToRun} {args}", LogLevel.Debug);
            var winlogon = Process.GetProcesses().Where(p => p.ProcessName.In("winlogon")).FirstOrDefault();
            return StartAsProcess(winlogon.Id, appToRun, args, startupFolder, hidden).SafeHandle;
        }

        //public static SafeProcessHandle StartAsExplorerDetached(string appToRun, string args, string startupFolder, bool newWindow, bool hidden)
        //{
        //    Logger.Instance.Log($"{nameof(StartAsExplorerDetached)}: {appToRun} {args}", LogLevel.Debug);
        //    var explorer = Process.GetProcesses().Where(p => p.ProcessName.In("explorer")).FirstOrDefault();
        //    using (var newToken = GetTokenFromProcess(explorer.Id))
        //    {
        //        return StartWithProcessToken(newToken, appToRun, args, startupFolder, newWindow, hidden);
        //    }
        //}

        public static SafeProcessHandle StartWithIntegrity(IntegrityLevel integrityLevel, string appToRun, string args, string startupFolder, bool newWindow, bool hidden)
        {
            // must return a process Handle because we cant create a Process() from a handle and get the exit code. 
            Logger.Instance.Log($"{nameof(StartWithIntegrity)}: {appToRun} {args}", LogLevel.Debug);
            using (var newToken = DuplicateOurToken())
            {
                if (!AdjustedTokenIntegrity(newToken, integrityLevel))
                    return null;

                return StartWithToken(newToken, appToRun, args, startupFolder, newWindow, hidden);
            }
        }

        private static SafeProcessHandle StartWithToken(SafeTokenHandle newToken, string appToRun, string args, string startupFolder, bool newWindow, bool hidden)
        {
            var si = new STARTUPINFO();

            if (newWindow)
            {
                si.dwFlags = 0x00000001; // STARTF_USESHOWWINDOW
                si.wShowWindow = (short)(hidden ? 0 : 1);
            }

            si.cb = Marshal.SizeOf(si);

            var pi = new PROCESS_INFORMATION();
            uint dwCreationFlags = newWindow ? (uint)0x00000010 /*CREATE_NEW_CONSOLE*/: 0;

            if (!TokensApi.CreateProcessAsUser(newToken, null, $"{appToRun} {args}",
                IntPtr.Zero, IntPtr.Zero, false, dwCreationFlags, IntPtr.Zero, startupFolder, ref si,
                out pi))
            {
                throw new Win32Exception();
            }

            CloseHandle(pi.hThread);
            return new SafeProcessHandle(pi.hProcess, true);
        }

        private static SafeTokenHandle GetTokenFromProcess(int pidWithToken)
        {
            IntPtr existingProcessHandle = OpenProcess(PROCESS_QUERY_INFORMATION, true, (uint)pidWithToken);
            if (existingProcessHandle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr existingProcessToken;
            try
            {
                if (!ProcessApi.OpenProcessToken(existingProcessHandle,
                    TokensApi.TOKEN_DUPLICATE,
                    out existingProcessToken))
                {
                    return null;
                }
            }
            finally
            {
                CloseHandle(existingProcessHandle);
            }
            if (existingProcessToken == IntPtr.Zero) return null;

            var sa = new SECURITY_ATTRIBUTES();
            sa.nLength = 0;
            const uint MAXIMUM_ALLOWED = 0x02000000;
            const uint desiredAccess = 0;

            SafeTokenHandle newToken;

            if (!TokensApi.DuplicateTokenEx(existingProcessToken, desiredAccess, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out newToken))
            {
                return null;
            }

            return newToken;
        }

        private static SafeTokenHandle DuplicateOurToken()
        {
            IntPtr existingProcessToken;
            
            if (!ProcessApi.OpenProcessToken(Process.GetCurrentProcess().Handle,
                TokensApi.TOKEN_DUPLICATE | TokensApi.TOKEN_ADJUST_DEFAULT |
                TokensApi.TOKEN_QUERY | TokensApi.TOKEN_ASSIGN_PRIMARY,
                out existingProcessToken))
            {
                return null;
            }

            if (existingProcessToken == IntPtr.Zero) return null;

            var sa = new SECURITY_ATTRIBUTES();
            sa.nLength = 0;
            const uint MAXIMUM_ALLOWED = 0x02000000;
            const uint desiredAccess = 0;

            SafeTokenHandle newToken;

            if (!TokensApi.DuplicateTokenEx(existingProcessToken, desiredAccess, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out newToken))
            {
                return null;
            }

            return newToken;
        }

        private static bool AdjustedTokenIntegrity(SafeTokenHandle newToken, IntegrityLevel integrityLevel)
        {
            string integritySid = "S-1-16-" + ((int)integrityLevel).ToString(CultureInfo.InvariantCulture);
            IntPtr pIntegritySid;
            if (!ConvertStringSidToSid(integritySid, out pIntegritySid))
                return false;

            TOKEN_MANDATORY_LABEL TIL = new TOKEN_MANDATORY_LABEL();
            TIL.Label.Attributes = 0x00000020 /* SE_GROUP_INTEGRITY */;
            TIL.Label.Sid = pIntegritySid;

            var pTIL = Marshal.AllocHGlobal(Marshal.SizeOf<TOKEN_MANDATORY_LABEL>());
            Marshal.StructureToPtr(TIL, pTIL, false);

            if (!SetTokenInformation(newToken.DangerousGetHandle(),
               TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
               pTIL,
               (uint)(Marshal.SizeOf<TOKEN_MANDATORY_LABEL>() + GetLengthSid(pIntegritySid))))
                return false;

            STARTUPINFO StartupInfo = new STARTUPINFO();

            return true;
        }

        private static Process StartAsProcess(int pidWithToken, string appToRun, string args, string startupFolder, bool hidden)
        {
            IntPtr existingProcessHandle = OpenProcess(PROCESS_QUERY_INFORMATION, true, (uint)pidWithToken);
            if (existingProcessHandle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr existingProcessToken, newToken;
            try
            {
                if (!OpenProcessToken(existingProcessHandle, TOKEN_DUPLICATE, out existingProcessToken))
                {
                    return null;
                }
            }
            finally
            {
                CloseHandle(existingProcessHandle);
            }
            if (existingProcessToken == IntPtr.Zero) return null;

            var sa = new SECURITY_ATTRIBUTES();
            const uint MAXIMUM_ALLOWED = 0x02000000;

            if (!TokensApi.DuplicateTokenEx(existingProcessToken, MAXIMUM_ALLOWED, ref sa, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out newToken))
            {
                return null;
            }

            var STARTF_USESHOWWINDOW = 0x00000001;
            var startupInfo = new STARTUPINFO()
            {
                cb = (int)Marshal.SizeOf(typeof(STARTUPINFO)),
                dwFlags = STARTF_USESHOWWINDOW,
                wShowWindow = (short)(hidden ? 0 : 1),
            };

            PROCESS_INFORMATION processInformation;
            if (!CreateProcessWithTokenW(newToken, 0, appToRun, $"{appToRun} {args}", 0, IntPtr.Zero, startupFolder, ref startupInfo, out processInformation))
            {
                return null;
            }
            return Process.GetProcessById(processInformation.dwProcessId);
        }
        #endregion
    }
}

