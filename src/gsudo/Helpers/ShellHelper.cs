using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    public enum Shell
    {
        PowerShell,
        PowerShellCore,
        PowerShellCore623BuggedGlobalInstall,
        Cmd,
    }

    static class ShellHelper
    {
        public static Shell DetectInvokingShell(out string ShellExeName)
        {
            // Is our current shell Powershell ? (Powershell.exe -calls-> gsudo)
            var parentProcess = Process.GetCurrentProcess().GetParentProcessExcludingShim();
            if (parentProcess != null)
            {
                string parentExeName = null;
                try
                {
                    parentExeName = Path.GetFileName(parentProcess.GetExeName()).ToUpperInvariant();
                }
                catch { }

                if (parentExeName == "POWERSHELL.EXE")
                {
                    ShellExeName = parentProcess.GetExeName();
                    return Shell.PowerShell;
                }
                else if (parentExeName == "PWSH.EXE")
                {
                    ShellExeName = parentProcess.GetExeName();
                    return Shell.PowerShellCore;
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
                            ShellExeName = grandParentProcess.GetExeName();

                            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(ShellExeName);

                            if (Version.Parse(versionInfo.FileVersion) <= Version.Parse("6.2.3.0") && ShellExeName.EndsWith(".dotnet\\tools\\pwsh.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                return Shell.PowerShellCore623BuggedGlobalInstall;
                            }

                            return Shell.PowerShellCore;
                        }
                    }
                }
            }

            ShellExeName = Environment.GetEnvironmentVariable("COMSPEC");
            return Shell.Cmd;
        }
    }
}
