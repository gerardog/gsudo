using gsudo.Native;
using gsudo.Tokens;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using static gsudo.Native.ProcessApi;
using static gsudo.Native.TokensApi;

namespace gsudo.Helpers
{
    public static class ProcessFactory
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

            if (hidden)
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            else
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    throw new ApplicationException("The operation was canceled by the user.");

                throw;
            }
            return process;
        }

        public static Process StartRedirected(string fileName, string arguments, string startFolder)
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
                StandardOutputEncoding = Settings.Encoding,
                StandardErrorEncoding = Settings.Encoding,
            };
            process.Start();
            return process;
        }

        public static Process StartAttached(string filename, string arguments)
        {
            Logger.Instance.Log($"Process Start: {filename} {arguments}", LogLevel.Debug    );
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

                for (int i = 0; process.MainWindowHandle == IntPtr.Zero && i < 30; i++)
                    System.Threading.Thread.Sleep(10);

                // set user the focus to the window, if there is one.
                if (process.MainWindowHandle != IntPtr.Zero)
                    _ = WindowApi.SetForegroundWindow(process.MainWindowHandle);
            }

            return process;
        }

        public static Process StartWithCredentials(string filename, string arguments, string user, SecureString password)
        {
            Logger.Instance.Log($"Starting process as {user}: {filename} {arguments}", LogLevel.Debug);
            var usr = InputArguments.UserName.Split('\\');

            try
            {
                return Process.Start(new ProcessStartInfo()
                {
                    UserName = usr[1],
                    Domain = usr[0],
                    Password = password,
                    Arguments = arguments,
                    FileName = filename,
                    LoadUserProfile = true,
                    UseShellExecute = false,
                    CreateNoWindow = !InputArguments.Debug,
                });
            }
            catch(Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1326)
                    throw new ApplicationException("The user name or password is incorrect.");

                throw;
            }
        }

        public static bool IsWindowsApp(string exe)
        {
            var path = FindExecutableInPath(ArgumentsHelper.UnQuote(exe));
            var shinfo = new Native.FileApi.SHFILEINFO();
            const int SHGFI_EXETYPE = 0x000002000;
            var fileInfo = Native.FileApi.SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_EXETYPE);
            var retval = (fileInfo.ToInt64() & 0xFFFF0000) > 0;
            return retval;
        }

        public static string FindExecutableInPath(string input)
        {
            input = Environment.ExpandEnvironmentVariables(input);

            try
            {
                if (File.Exists(input))
                {
                    return Path.GetFullPath(input);
                }

                var inputFolder = Path.GetDirectoryName(input);
                var inputFileName = Path.GetFileName(input);

                var validExtensions = Environment.GetEnvironmentVariable("PATHEXT", EnvironmentVariableTarget.Process)
                    .Split(';');

                var possibleNames = new List<string>();

                if (validExtensions.Contains(Path.GetExtension(inputFileName), StringComparer.OrdinalIgnoreCase))
                {
                    possibleNames.Add(inputFileName);
                }

                possibleNames.AddRange(validExtensions.Select((ext) => inputFileName + ext));

                var pathsToSearch = new List<string>();

                if (!string.IsNullOrEmpty(inputFolder))
                {
                    pathsToSearch.Add(Path.Combine(Environment.CurrentDirectory, inputFolder));
                }
                else
                {
                    pathsToSearch.Add(Environment.CurrentDirectory);
                    pathsToSearch.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'));
                }

                foreach (string pathCandidate in pathsToSearch)
                {
                    foreach (string fileNameCandidate in possibleNames)
                    {
                        string path = Path.Combine(pathCandidate, fileNameCandidate);
                        if (!String.IsNullOrEmpty(path) && File.Exists(path))
                            return Path.GetFullPath(path);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static SafeProcessHandle StartAsSystem(string appToRun, string args, string startupFolder, bool hidden)
        {
            Logger.Instance.Log($"{nameof(StartAsSystem)}: {appToRun} {args}", LogLevel.Debug);
            using (var tm = TokenProvider.CreateFromSystemAccount())
            {
                using (var token = tm.GetToken())
                {
                    return CreateProcessWithToken(token.DangerousGetHandle(), appToRun, args, startupFolder, hidden);
                }
            }
        }

        /// <summary>
        /// You can only call this if you are elevated.
        /// </summary>
        public static SafeProcessHandle StartAttachedWithIntegrity(IntegrityLevel integrityLevel, string appToRun, string args, string startupFolder, bool newWindow, bool hidden)
        {
            // must return a process Handle because we cant create a Process() from a handle and get the exit code. 
            Logger.Instance.Log($"{nameof(StartAttachedWithIntegrity)}: {appToRun} {args}", LogLevel.Debug);
            int currentIntegrity = SecurityHelper.GetCurrentIntegrityLevel();
            SafeTokenHandle newToken;

            if ((int)integrityLevel == currentIntegrity)
            {
                return new SafeProcessHandle(StartAttached(appToRun, args).Handle, true);
            }

            if (integrityLevel >= IntegrityLevel.Medium) // Unelevation request.
            {
                try
                {
                    return TokenProvider
                        .CreateFromSystemAccount()
                        .EnablePrivilege(Privilege.SeIncreaseQuotaPrivilege, false)
                        .EnablePrivilege(Privilege.SeAssignPrimaryTokenPrivilege, false)
                        .Impersonate(() =>
                        {
                            newToken = TokenProvider.CreateFromCurrentProcessToken().GetLinkedToken()
                                .SetIntegrity(integrityLevel)
                                .GetToken();

                            using (newToken)
                            {
                                return CreateProcessAsUser(newToken, appToRun, args, startupFolder, newWindow, hidden);
                            }
                        });
                }
                catch (Exception e)
                {
                    Logger.Instance.Log("Unable to get unelevated token. (Is UAC enabled?) Fallback to SaferApi Token but this process won't be able to elevate." + e.Message, LogLevel.Debug);
                    newToken = TokenProvider.CreateFromSaferApi(SaferLevels.NormalUser)
                        .SetIntegrity(integrityLevel)
                        .GetToken();
                }

                using (newToken)
                {
                    return CreateProcessAsUser(newToken, appToRun, args, startupFolder, newWindow, hidden);
                }
            }
            else
            {
                // Integrity < Medium
                var tf = TokenProvider.CreateFromSaferApi(integrityLevel.ToSaferLevel())
                    .SetIntegrity(integrityLevel);

                newToken = tf.GetToken();
            }

            using (newToken)
            {
                return CreateProcessAsUser(newToken, appToRun, args, startupFolder, newWindow, hidden);
            }
        }

        private static SafeProcessHandle CreateProcessAsUser(SafeTokenHandle newToken, string appToRun, string args, string startupFolder, bool newWindow, bool hidden)
        {
            var si = new STARTUPINFO();

            if (newWindow)
            {
                si.dwFlags = 0x00000001; // STARTF_USESHOWWINDOW
                si.wShowWindow = (short)(hidden ? 0 : 1);
            }

            si.cb = Marshal.SizeOf(si);

            var pi = new PROCESS_INFORMATION();
            uint dwCreationFlags = newWindow ? (uint)CreateProcessFlags.CREATE_NEW_CONSOLE : 0;

            if (!TokensApi.CreateProcessAsUser(newToken, ArgumentsHelper.UnQuote(appToRun), $"{appToRun} {args}",
                IntPtr.Zero, IntPtr.Zero, false, dwCreationFlags, IntPtr.Zero, startupFolder, ref si,
                out pi))
            {
                throw new Win32Exception();
            }

            CloseHandle(pi.hThread);
            return new SafeProcessHandle(pi.hProcess, true);
        }

        private static SafeProcessHandle CreateProcessWithToken(IntPtr newToken, string appToRun, string args, string startupFolder, bool hidden)
        {
            var STARTF_USESHOWWINDOW = 0x00000001;
            var STARTF_USESTDHANDLES = 0x00000100;

            var startupInfo = new STARTUPINFO()
            {
                cb = (int)Marshal.SizeOf(typeof(STARTUPINFO)),
                dwFlags = STARTF_USESHOWWINDOW,
                wShowWindow = (short)(hidden ? 0 : 1),
            };

            if (Console.IsErrorRedirected | Console.IsInputRedirected | Console.IsOutputRedirected)
            {
                startupInfo.dwFlags |= STARTF_USESTDHANDLES;
                startupInfo.hStdOutput = ConsoleApi.GetStdHandle(ConsoleApi.STD_OUTPUT_HANDLE);
                startupInfo.hStdInput = ConsoleApi.GetStdHandle(ConsoleApi.STD_INPUT_HANDLE);
                startupInfo.hStdError = ConsoleApi.GetStdHandle(ConsoleApi.STD_ERROR_HANDLE);
            }

            PROCESS_INFORMATION processInformation;
            if (!CreateProcessWithTokenW(newToken, 0, null, $"{appToRun} {args}", (UInt32)0, IntPtr.Zero, startupFolder, ref startupInfo, out processInformation))
            {
                throw new Win32Exception();
            }
            return new SafeProcessHandle(processInformation.hProcess, true);
        }

        internal static SafeProcessHandle CreateProcessAsUserWithFlags(string lpApplicationName, string args, ProcessApi.CreateProcessFlags dwCreationFlags, out PROCESS_INFORMATION pInfo)
        {
            var sInfoEx = new ProcessApi.STARTUPINFOEX();
            sInfoEx.StartupInfo.cb = Marshal.SizeOf(sInfoEx);

            var pSec = new ProcessApi.SECURITY_ATTRIBUTES();
            var tSec = new ProcessApi.SECURITY_ATTRIBUTES();
            pSec.nLength = Marshal.SizeOf(pSec);
            tSec.nLength = Marshal.SizeOf(tSec);

            // Set more restrictive Security Descriptor
            string sddl = "D:(D;;GAFAWD;;;S-1-1-0)"; // Deny Generic-All, File-All, and Write-Dac to everyone.

            IntPtr sd_ptr = new IntPtr();
            UIntPtr sd_size_ptr = new UIntPtr();
            Native.TokensApi.ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, StringSDRevision: 1, out sd_ptr, out sd_size_ptr);

            pSec.lpSecurityDescriptor = sd_ptr;
            tSec.lpSecurityDescriptor = sd_ptr;

            var command = $"{lpApplicationName} {args}";

            Logger.Instance.Log($"{nameof(CreateProcessAsUserWithFlags)}: {lpApplicationName} {args}", LogLevel.Debug);
            if (!ProcessApi.CreateProcess(null, command, ref pSec, ref tSec, false, dwCreationFlags, IntPtr.Zero, null, ref sInfoEx, out pInfo))
            {
                throw new Win32Exception((int)ConsoleApi.GetLastError());
            }

            return new SafeProcessHandle(pInfo.hProcess, true);
        }

    }
}

