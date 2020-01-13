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
        PowerShellCore6,
        Cmd,
        PowerShellCore7
    }

    static class ShellHelper
    {
        public static Shell DetectInvokingShell(out string ShellExeName)
        {
            // Is our current shell Powershell ? (Powershell.exe -calls-> gsudo)
            var parentProcess = Process.GetCurrentProcess().ParentProcess();
            var parentExeName = Path.GetFileName(parentProcess.MainModule.FileName).ToUpperInvariant();
            if (parentExeName == "POWERSHELL.EXE")
            {
                ShellExeName = parentProcess.MainModule.FileName;
                return Shell.PowerShell;
            }
            else
            {
                // Is our current shell Powershell Core? (Pwsh.exe -calls-> dotnet -calls-> gsudo)
                var grandParentProcess = parentProcess.ParentProcess();
                if (grandParentProcess != null)
                {
                    var grandParentExeName = Path.GetFileName(grandParentProcess.MainModule.FileName).ToUpperInvariant();
                    if (grandParentExeName == "PWSH.EXE")
                    {
                        ShellExeName = grandParentProcess.MainModule.FileName;

                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(ShellExeName);
                        if (versionInfo.FileMajorPart < 7)
                            return Shell.PowerShellCore6;
                        else
                            return Shell.PowerShellCore7;
                    }
                }
            }

            ShellExeName = Environment.GetEnvironmentVariable("COMSPEC");
            return Shell.Cmd;
        }
    }
}
