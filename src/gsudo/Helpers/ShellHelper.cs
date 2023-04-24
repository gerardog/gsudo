using System;
using System.Diagnostics;
using System.IO;

namespace gsudo.Helpers
{
    public enum Shell
    {
        PowerShell,
        PowerShellCore,
        PowerShellCore623BuggedGlobalInstall,
        Cmd,
        Yori,
        Wsl,
        Bash,
        TakeCommand,
        NuShell,

        WindowsApp, // => called from a windows app without console, like Win+R.
    }

    static class ShellHelper
    {
        private static Shell? _invokingShell;
        private static string _invokingShellFullPath;

        public static string InvokingShellFullPath
        {
            get
            {
                if (!IsIntialized) Initialize();
                return _invokingShellFullPath;
            }
        }

        public static Shell InvokingShell
        {
            get
            {
                if (!IsIntialized) Initialize();
                return _invokingShell.Value;
            }
        }

        private static bool IsIntialized { get; set; }

        private static void Initialize()
        {
            _invokingShell = InitializeInternal(out _invokingShellFullPath);
            IsIntialized = true;
        }

        private static Shell InitializeInternal(out string invokingShellFullPath)
        {
            var parentProcess = Process.GetCurrentProcess().GetParentProcessExcludingShim();

            if (parentProcess != null)
            {
                invokingShellFullPath = parentProcess.GetExeName();

                var shell = DetectShellByFileName(invokingShellFullPath);

                if (shell.HasValue)
                    return shell.Value;
                // Depending on how pwsh was installed, Pwsh.exe -calls-> dotnet -calls-> gsudo.
                var grandParentProcess = parentProcess.GetParentProcessExcludingShim();
                if (grandParentProcess != null)
                {
                    var grandParentExeName = Path.GetFileName(grandParentProcess.GetExeName()).ToUpperInvariant();
                    if (grandParentExeName == "PWSH.EXE" || grandParentExeName == "PWSH")
                    {
                        invokingShellFullPath = grandParentProcess.GetExeName();

                        Version fileVersion = GetInvokingShellVersion();

                        if (fileVersion <= new Version(6, 2, 3) && invokingShellFullPath.EndsWith(".dotnet\\tools\\pwsh.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            return Shell.PowerShellCore623BuggedGlobalInstall;
                        }

                        return Shell.PowerShellCore;
                    }
                }

                if (ProcessFactory.IsWindowsApp(invokingShellFullPath))
                {
                    invokingShellFullPath = Environment.GetEnvironmentVariable("COMSPEC");
                    return Shell.WindowsApp; // Called from explorer.exe, task mgr, etc.
                }
            }

            // Unknown Shell. 
            // (We couldnt get info about caller process).
            // => Assume CMD.
            invokingShellFullPath = Environment.GetEnvironmentVariable("COMSPEC");
            return Shell.Cmd;
        }
    

        public static Version GetInvokingShellVersion()
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(_invokingShellFullPath);
            var fileVersion = new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart);
            return fileVersion;
        }

        public static Shell? DetectShellByFileName(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return null;
            var parentExeName = Path.GetFileName(filename).ToUpperInvariant();

            if (parentExeName.EndsWith(".EXE", StringComparison.Ordinal)) // trim ending (this happens when invoking gsudo 32 bits build from cmd 64 bits)
                parentExeName = parentExeName.Substring(0, parentExeName.Length - 4);

            // If user is running gsudo x86-version on x64 windows (not recommedned),
            // Open process fails, and so we fail to get the process FullName (it just returns filename without extension).
            if (parentExeName == "POWERSHELL")
            {
                return Shell.PowerShell;
            }
            else if (parentExeName == "PWSH")
            {
                return Shell.PowerShellCore;
            }
            else if (parentExeName == "YORI")
            {
                return Shell.Yori;
            }
            else if (parentExeName == "WSL")
            {
                return Shell.Wsl;
            }
            else if (parentExeName == "BASH" || parentExeName == "SH")
            {
                return Shell.Bash;
            }
            else if (parentExeName == "TCC")
            {
                return Shell.TakeCommand;
            }
            else if (parentExeName == "NU")
            {
                return Shell.NuShell;
            }
            else if (parentExeName == "CMD")
            {
                // CMD.EXE can be
                //   %windir%\System32\cmd.exe => 64-bit CMD.
                // or 
                //   %windir%\SysWoW64\cmd.exe => 32-bit CMD

                // So lets keep shellFullPath = ParentProcess.FullPath
                // (instead of using COMSPEC)
                // in order to keep the same bitness.
                return Shell.Cmd;
            }
            else
                return null;
        }
    }
}
