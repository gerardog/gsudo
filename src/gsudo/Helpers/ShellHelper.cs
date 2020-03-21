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
    }

    static class ShellHelper
    {
        public static Shell DetectInvokingShell(out string shellFullPath)
        {
            var parentProcess = Process.GetCurrentProcess().GetParentProcessExcludingShim();
            if (parentProcess != null)
            {
                string parentExeName = null;
                shellFullPath = parentProcess.GetExeName();
                parentExeName = Path.GetFileName(shellFullPath).ToUpperInvariant();

                if (parentExeName == "POWERSHELL.EXE")
                {
                    return Shell.PowerShell;
                }
                else if (parentExeName == "PWSH.EXE")
                {
                    return Shell.PowerShellCore;
                }
                else if (parentExeName == "YORI.EXE")
                {
                    return Shell.Yori;
                }
                else if (parentExeName != "CMD.EXE")
                {
                    // Depending on how pwsh was installed, Pwsh.exe -calls-> dotnet -calls-> gsudo.
                    var grandParentProcess = parentProcess.GetParentProcessExcludingShim();
                    if (grandParentProcess != null)
                    {
                        var grandParentExeName = Path.GetFileName(grandParentProcess.GetExeName()).ToUpperInvariant();
                        if (grandParentExeName == "PWSH.EXE")
                        {
                            shellFullPath = grandParentProcess.GetExeName();

                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(shellFullPath);

                            if (Version.Parse(versionInfo.FileVersion) <= Version.Parse("6.2.3.0") && shellFullPath.EndsWith(".dotnet\\tools\\pwsh.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                return Shell.PowerShellCore623BuggedGlobalInstall;
                            }

                            return Shell.PowerShellCore;
                        }
                    }
                }
            }

            shellFullPath = Environment.GetEnvironmentVariable("COMSPEC");
            return Shell.Cmd;
        }
    }
}
