using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace gsudo.Helpers
{
    static class ProcessStarter
    {
        public static Process StartElevatedDetached(string filename, string arguments, bool hidden)
        {
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
                    SetForegroundWindow(process.MainWindowHandle);
            }

            return process;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hwnd);


        public static bool IsWindowsApp(string exe)
        {
            var path = FindExecutableInPath(exe);
            var shinfo = new SHFILEINFO();
            const int SHGFI_EXETYPE = 0x000002000;
            var fileInfo = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_EXETYPE);
            var retval = (fileInfo.ToInt64() & 0xFFFF0000) >0 ;
            Globals.Logger.Log($"IsWindowsApp(\"{exe}\") = {retval} (\"{path}\")", LogLevel.Debug);
            return retval;
        }

        #region IsWindowsApp Win32 Api
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        #endregion

        public static string FindExecutableInPath(string exe)
        {
            exe = Environment.ExpandEnvironmentVariables(exe);

            if (File.Exists(exe))
            {
                return Path.GetFullPath(exe);
            }

            if (Path.GetDirectoryName(exe) == String.Empty)
            {
                exe = Path.GetFileName(exe);

                var validExtensions = Environment.GetEnvironmentVariable("PATHEXT", EnvironmentVariableTarget.Process)
                    .Split(';'); ;

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
    }
}
