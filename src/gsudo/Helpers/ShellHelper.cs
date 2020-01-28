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
            var parentProcess = Process.GetCurrentProcess().ParentProcess();
            if (parentProcess != null)
            {
                var parentExeName = Path.GetFileName(parentProcess.MainModule.FileName).ToUpperInvariant();
                if (parentExeName == "POWERSHELL.EXE")
                {
                    ShellExeName = parentProcess.MainModule.FileName;
                    return Shell.PowerShell;
                }
                else if (parentExeName == "PWSH.EXE")
                {
                    ShellExeName = parentProcess.MainModule.FileName;
                    return Shell.PowerShellCore;
                }
                else
                {
                    // Depending on how pwsh was installed, Pwsh.exe -calls-> dotnet -calls-> gsudo.
                    var grandParentProcess = parentProcess.ParentProcess();
                    if (grandParentProcess != null)
                    {
                        var grandParentExeName = Path.GetFileName(grandParentProcess.MainModule.FileName).ToUpperInvariant();
                        if (grandParentExeName == "PWSH.EXE")
                        {
                            ShellExeName = grandParentProcess.MainModule.FileName;

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
