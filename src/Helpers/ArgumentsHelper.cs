using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    class ArgumentsHelper
    {
        static readonly HashSet<string> CMD_COMMANDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASSOC", "ATTRIB", "BREAK", "BCDEDIT", "CACLS", "CALL", "CD", "CHCP", "CHDIR", "CHKDSK", "CHKNTFS", "CLS", "CMD", "COLOR", "COMP", "COMPACT", "CONVERT", "COPY", "DATE", "DEL", "DIR", "DISKPART", "DOSKEY", "DRIVERQUERY", "ECHO", "ENDLOCAL", "ERASE", "EXIT", "FC", "FIND", "FINDSTR", "FOR", "FORMAT", "FSUTIL", "FTYPE", "GOTO", "GPRESULT", "GRAFTABL", "HELP", "ICACLS", "IF", "LABEL", "MD", "MKDIR", "MKLINK", "MODE", "MORE", "MOVE", "OPENFILES", "PATH", "PAUSE", "POPD", "PRINT", "PROMPT", "PUSHD", "RD", "RECOVER", "REM", "REN", "RENAME", "REPLACE", "RMDIR", "ROBOCOPY", "SET", "SETLOCAL", "SC", "SCHTASKS", "SHIFT", "SHUTDOWN", "SORT", "START", "SUBST", "SYSTEMINFO", "TASKLIST", "TASKKILL", "TIME", "TITLE", "TREE", "TYPE", "VER", "VERIFY", "VOL", "XCOPY", "WMIC" };
        internal string[] AugmentCommand(string[] args)
        {
            if (args.Length == 0)
            {
                // If zero args specified, Try to "elevate the current shell".
                // Which is impossible. So we launch the current shell elevated.

                // Is our current shell Powershell ? (Powershell.exe -calls-> gsudo)
                var parentProcess = Process.GetCurrentProcess().ParentProcess();
                var parentExeName = Path.GetFileName(parentProcess.MainModule.FileName).ToUpperInvariant();
                if (parentExeName == "POWERSHELL.EXE")
                    return new string[] { parentProcess.MainModule.FileName };

                // Is our current shell Powershell Core? (Pwsh.exe -calls-> dotnet -calls-> gsudo)
                var grandParentProcess = parentProcess.ParentProcess();
                var grandParentExeName = Path.GetFileName(grandParentProcess.MainModule.FileName).ToUpperInvariant();
                if (grandParentExeName == "PWSH.EXE")
                    return new string[] { grandParentProcess.MainModule.FileName };

                // Default, our current shell is CMD.
                return new string[] { Environment.GetEnvironmentVariable("COMSPEC"), "/k" };
            }

            if (CMD_COMMANDS.Contains(args[0]))
                return new string[] 
                    { Environment.GetEnvironmentVariable("COMSPEC"), "/c" }
                    .Concat(args).ToArray();

            return args;
        }
    }
}
