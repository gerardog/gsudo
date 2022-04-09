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
                if (_invokingShellFullPath==null)
                    Initialize();
                return _invokingShellFullPath;
            }
        }

        public static Shell InvokingShell
        {
            get
            {
                if (!_invokingShell.HasValue)
                    Initialize();
                return _invokingShell.Value;
            }
        }

        private static void Initialize()
        {
            var parentProcess = Process.GetCurrentProcess().GetParentProcessExcludingShim(false); // must be false to avoid a stack overflow exception.

            if (parentProcess != null)
            {
                _invokingShellFullPath = parentProcess.GetExeName();
                string parentExeName = Path.GetFileName(_invokingShellFullPath).ToUpperInvariant();

                if (parentExeName == "POWERSHELL.EXE")
                {
                    _invokingShell = Shell.PowerShell;
                }
                else if (parentExeName == "PWSH.EXE")
                {
                    _invokingShell = Shell.PowerShellCore;
                }
                else if (parentExeName == "YORI.EXE")
                {
                    _invokingShell = Shell.Yori;
                }
                else if (parentExeName == "WSL.EXE")
                {
                    _invokingShell = Shell.Wsl;
                }
                else if (parentExeName == "BASH.EXE")
                {
                    _invokingShell = Shell.Bash;
                }
                else if (parentExeName == "TCC.EXE")
                {
                    _invokingShell = Shell.TakeCommand;
                }
                else if (parentExeName == "CMD.EXE")
                {
                    // CMD.EXE can be
                    //   %windir%\System32\cmd.exe => 64-bit CMD.
                    // or 
                    //   %windir%\SysWoW64\cmd.exe => 32-bit CMD

                    // So lets keep shellFullPath = ParentProcess.FullPath
                    // in order to keep the same bitness.
                    _invokingShell = Shell.Cmd;
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
                            _invokingShellFullPath = grandParentProcess.GetExeName();

                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(_invokingShellFullPath);

                            if (Version.Parse(versionInfo.FileVersion) <= Version.Parse("6.2.3.0") && _invokingShellFullPath.EndsWith(".dotnet\\tools\\pwsh.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                _invokingShell = Shell.PowerShellCore623BuggedGlobalInstall;
                                return;
                            }

                            _invokingShell = Shell.PowerShellCore;
                        }
                    }
                }

                return;
            }

            // Unknown Shell. 
            // (We couldnt get info about caller process).
            // => Assume CMD.
            _invokingShellFullPath = Environment.GetEnvironmentVariable("COMSPEC");
            _invokingShell = Shell.Cmd;
        }
    }
}
