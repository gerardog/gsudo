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
        TakeCommand
    }

    static class ShellHelper
    {
        private static Shell? _invokingShell;
        private static string _invokingShellFullPath;

        public static string InvokingShellFullPath 
        {
            get
            {
                if (!IsIntialized)
                    Initialize(out _invokingShell, out _invokingShellFullPath);
                return _invokingShellFullPath;
            }
        }

        public static Shell InvokingShell
        {
            get
            {
                if (!IsIntialized)
                    Initialize(out _invokingShell,out _invokingShellFullPath);
                return _invokingShell.Value;
            }
        }

        public static bool IsIntialized { get; internal set; }

        private static void Initialize(out Shell? invokingShell, out string invokingShellFullPath)
        {
            var parentProcess = Process.GetCurrentProcess().GetParentProcessExcludingShim();

            if (parentProcess != null)
            {
                invokingShellFullPath = parentProcess.GetExeName();
                string parentExeName = Path.GetFileName(invokingShellFullPath).ToUpperInvariant();

                if (parentExeName == "POWERSHELL.EXE")
                {
                    invokingShell = Shell.PowerShell;
                }
                else if (parentExeName == "PWSH.EXE")
                {
                    invokingShell = Shell.PowerShellCore;
                }
                else if (parentExeName == "YORI.EXE")
                {
                    invokingShell = Shell.Yori;
                }
                else if (parentExeName == "WSL.EXE")
                {
                    invokingShell = Shell.Wsl;
                }
                else if (parentExeName == "BASH.EXE")
                {
                    invokingShell = Shell.Bash;
                }
                else if (parentExeName == "TCC.EXE")
                {
                    invokingShell = Shell.TakeCommand;
                }
                else if (parentExeName == "CMD.EXE")
                {
                    // CMD.EXE can be
                    //   %windir%\System32\cmd.exe => 64-bit CMD.
                    // or 
                    //   %windir%\SysWoW64\cmd.exe => 32-bit CMD

                    // So lets keep shellFullPath = ParentProcess.FullPath
                    // in order to keep the same bitness.
                    invokingShell = Shell.Cmd;
                }
                else 
                {
                    // Depending on how pwsh was installed, Pwsh.exe -calls-> dotnet -calls-> gsudo.
                    var grandParentProcess = parentProcess.GetParentProcessExcludingShim();
                    if (grandParentProcess != null)
                    {
                        var grandParentExeName = Path.GetFileName(grandParentProcess.GetExeName()).ToUpperInvariant();
                        if (grandParentExeName == "PWSH.EXE")
                        {
                            invokingShellFullPath = grandParentProcess.GetExeName();

                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(invokingShellFullPath);

                            if (Version.Parse(versionInfo.FileVersion) <= Version.Parse("6.2.3.0") && invokingShellFullPath.EndsWith(".dotnet\\tools\\pwsh.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                invokingShell = Shell.PowerShellCore623BuggedGlobalInstall;
                                IsIntialized = true;
                                return;
                            }

                            invokingShell = Shell.PowerShellCore;
                            IsIntialized = true;
                            return;
                        }
                    }
                }

            }

            // Unknown Shell. 
            // (We couldnt get info about caller process).
            // => Assume CMD.
            invokingShellFullPath = Environment.GetEnvironmentVariable("COMSPEC");
            invokingShell = Shell.Cmd;
            IsIntialized = true;
        }
    }
}
