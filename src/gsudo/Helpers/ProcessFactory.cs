using gsudo.Native;
using System;
using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static gsudo.Native.ProcessApi;
using static gsudo.Native.TokensApi;
using gsudo.Tokens;

namespace gsudo.Helpers
{
    //https://csharp.hotexamples.com/examples/CSCreateLowIntegrityProcess/PROCESS_INFORMATION/-/php-process_information-class-examples.html

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

            process.Start();
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
            };
            process.Start();
            return process;
        }

        public static Process StartAttached(string filename, string arguments)
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

                for (int i = 0; process.MainWindowHandle == IntPtr.Zero && i < 30; i++)
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
            var retval = (fileInfo.ToInt64() & 0xFFFF0000) > 0;
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

        public static SafeProcessHandle StartAsSystem(string appToRun, string args, string startupFolder, bool hidden)
        {
            Logger.Instance.Log($"{nameof(StartAsSystem)}: {appToRun} {args}", LogLevel.Debug);
            using (var tm = TokenProvider.CreateFromSystemAccount())
            {
                using (var token = tm.GetToken())
                {
                    return CreateProcessWithToken(token.DangerousGetHandle() , appToRun, args, startupFolder, hidden);
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
            int currentIntegrity = ProcessHelper.GetCurrentIntegrityLevel();
            SafeTokenHandle newToken;

            if ((int)integrityLevel == currentIntegrity)
            {
                return new SafeProcessHandle(StartAttached(appToRun, args).Handle, true);
            }

            if (integrityLevel >= IntegrityLevel.Medium ) // Unelevation request.
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
                // Lower integrity
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
            const uint DETACHED_PROCESS = 0x00000008;

            var startupInfo = new STARTUPINFO()
            {
                cb = (int)Marshal.SizeOf(typeof(STARTUPINFO)),
                dwFlags = STARTF_USESHOWWINDOW,
                wShowWindow = (short)(hidden ? 0 : 1),
            };

            if (Console.IsErrorRedirected | Console.IsInputRedirected | Console.IsOutputRedirected)
            {
                startupInfo.dwFlags |= STARTF_USESTDHANDLES;
                startupInfo.hStdOutput = ConsoleApi.GetStdHandle(ConsoleApi.STD_OUTPUT_HANDLE).DangerousGetHandle();
                startupInfo.hStdInput = ConsoleApi.GetStdHandle(ConsoleApi.STD_INPUT_HANDLE).DangerousGetHandle();
                startupInfo.hStdError = ConsoleApi.GetStdHandle(ConsoleApi.STD_ERROR_HANDLE).DangerousGetHandle();
            }

            PROCESS_INFORMATION processInformation;
            if (!CreateProcessWithTokenW(newToken, 0, null, $"{appToRun} {args}",(UInt32) 0, IntPtr.Zero, startupFolder, ref startupInfo, out processInformation))
            {
                throw new Win32Exception();
            }
            return new SafeProcessHandle(processInformation.hProcess, true);
        }

        internal static SafeProcessHandle CreateProcessAsUserWithFlags(string lpApplicationName, string args, ProcessApi.CreateProcessFlags dwCreationFlags, out PROCESS_INFORMATION pInfo)
        {
            var sInfoEx = new ProcessApi.STARTUPINFOEX();
            sInfoEx.StartupInfo.cb = Marshal.SizeOf(sInfoEx);
            IntPtr lpValue = IntPtr.Zero;

            var pSec = new ProcessApi.SECURITY_ATTRIBUTES();
            var tSec = new ProcessApi.SECURITY_ATTRIBUTES();
            pSec.nLength = Marshal.SizeOf(pSec);
            tSec.nLength = Marshal.SizeOf(tSec);

            var command = $"{lpApplicationName} {args}";
            
            Logger.Instance.Log($"{nameof(CreateProcessAsUser)}: {lpApplicationName} {args}", LogLevel.Debug);
            if (!ProcessApi.CreateProcess(null, command, ref pSec, ref tSec, false, dwCreationFlags, IntPtr.Zero, null, ref sInfoEx, out pInfo))
                throw new Win32Exception();

            return new SafeProcessHandle(pInfo.hProcess, true);
        }

    }
}

