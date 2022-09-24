using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    internal class CommandToRunGenerator
    {
        static readonly HashSet<string> CmdCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASSOC", "BREAK", "CALL", "CD", "CHDIR", "CLS", "COLOR", "COPY", "DATE", "DEL", "DIR", "ECHO", "ENDLOCAL", "ERASE", "EXIT", "FOR", "FTYPE", "GOTO", "IF", "MD", "MKDIR", "MKLINK", "MOVE", "PATH", "PAUSE", "POPD", "PROMPT", "PUSHD", "RD", "REM", "REN", "RENAME", "RMDIR", "SET", "SETLOCAL", "SHIFT", "START", "TIME", "TITLE", "TYPE", "VER", "VERIFY", "VOL" };
        static readonly HashSet<string> CreateProcessSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".CMD", ".EXE", ".BAT", ".COM" };

        internal static string[] AugmentCommand(string[] args)
        {
            string currentShellExeName = ShellHelper.InvokingShellFullPath;
            Shell currentShell = ShellHelper.InvokingShell;

            Logger.Instance.Log($"Invoking Shell: {currentShell}", LogLevel.Debug);

            if (!InputArguments.Direct)
            {
                if (currentShell == Shell.PowerShellCore623BuggedGlobalInstall)
                {
                    // PowerShell Core 6.0.0 to 6.2.3 does not supports command line arguments.

                    // See:
                    // https://github.com/PowerShell/PowerShell/pull/10461#event-2959890147
                    // https://github.com/gerardog/gsudo/issues/10

                    /*                 
                    Running ./gsudo from powershell should elevate the current shell, which means:
                        => On PowerShell, run => powershell -NoLogo 
                        => On PowerShellCore => pwsh -NoLogo 
                        => On PowerShellCore623BuggedGlobalInstall => pwsh 

                    Running ./gsudo {command}   should elevate the powershell command.
                        => On PowerShell => powershell -NoLogo -NoProfile -Command {command} 
                        => On PowerShellCore => pwsh -NoLogo -NoProfile -Command {command}
                        => On PowerShellCore623BuggedGlobalInstall => pwsh {command}
                     */

                    Logger.Instance.Log("Please update to PowerShell Core >= 6.2.4 to avoid profile loading.", LogLevel.Warning);

                    var newArgs = new List<string>
                    {
                        $"\"{currentShellExeName}\""
                    };
                    newArgs.AddMany(args);

                    return newArgs.ToArray();
                }
                else if (currentShell.In(Shell.PowerShell, Shell.PowerShellCore))
                {
                    var newArgs = new List<string>
                    {
                        $"\"{currentShellExeName}\"",
                        "-NoLogo"
                    };

                    if (args.Length > 0)
                    {
                        if (!Settings.PowerShellLoadProfile)
                            newArgs.Add("-NoProfile");

                        if (args[0] == "-encodedCommand")
                        {
                            newArgs.AddMany(args);
                        }
                        else
                        {
                            newArgs.Add("-Command");

                            int last = args.Length - 1;


                            if (args[0].StartsWith("\"", StringComparison.Ordinal) &&
                                args[last].EndsWith("\"", StringComparison.Ordinal))
                            {
                                args[0] = args[0].Substring(1);
                                args[last] = args[last].Substring(0, args[last].Length - 1);
                            }

                            //-- Fix issue in powershell with commands ending in \" as in "C:\Windows\"
                            if (args[last].EndsWith("\\", StringComparison.Ordinal))
                                args[last] += "\\";

                            if (currentShell == Shell.PowerShell) // Windows Powershell extra issues (not core)
                            {
                                //See https://stackoverflow.com/a/59960203/97471
                                for (int i = 0; i < args.Length; i++)
                                    if (args[i].EndsWith("\\\"", StringComparison.Ordinal))
                                        args[i] = args[i].Substring(0, args[i].Length - 2) + "\\\\\"";
                            }
                            // ----

                            string pscommand = string.Join(" ", args)
                                            .Replace("\"", "\\\"")
                                            .Quote();

                            newArgs.Add(pscommand);
                        }
                    }

                    return DoFixIfIsMicrosoftStoreApp(currentShellExeName, newArgs.ToArray());
                }
                else if (currentShell == Shell.Yori)
                {
                    if (args.Length == 0)
                        return new[] { currentShellExeName };
                    else
                        return new[] { currentShellExeName, "-c" }
                            .Concat(args).ToArray();
                }
                else if (currentShell == Shell.Wsl)
                {
                    // these variables should come from WSL, via gsudo.extras\gsudo bash script
                    string wsl_distro = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
                    string wsl_user = Environment.GetEnvironmentVariable("USER");

                    if (!string.IsNullOrEmpty(wsl_user) && !string.IsNullOrEmpty(wsl_distro))
                    {
                        return new[] { currentShellExeName, // wsl.exe
                                        "-d", wsl_distro,
                                        "-u", wsl_user,
                                        "--" }
                                        .Concat(args).ToArray();
                    }
                }
                else if (currentShell == Shell.Bash)
                {
                    if (args.Length == 0)
                        return new[] { currentShellExeName };
                    else
                        return new[] { currentShellExeName, "-c",
                            $"\"{ String.Join(" ", args).Replace("\"", "\\\"") }\"" };
                }
                else if (currentShell == Shell.TakeCommand)
                {
                    if (args.Length == 0)
                        return new[] { currentShellExeName, "/k" };
                    else
                        return new[] { currentShellExeName, "/c" }
                            .Concat(args).ToArray();
                }
            }

            if (currentShell != Shell.Cmd)
            {
                // Fall back to CMD.
                currentShellExeName = Environment.GetEnvironmentVariable("COMSPEC");
            }

            // Not Powershell, or Powershell Core, assume CMD.
            if (args.Length == 0)
            {
                return new string[]
                    { currentShellExeName, "/k" };
            }
            else
            {
                if (CmdCommands.Contains(args[0])) // We want cmd commands to be run with CMD /c, not search for .EXE
                    return new string[]
                        { currentShellExeName, "/c" }
                        .Concat(args).ToArray();

                var exename = ProcessFactory.FindExecutableInPath(ArgumentsHelper.UnQuote(args[0]));
                if (exename == null || !CreateProcessSupportedExtensions.Contains(Path.GetExtension(exename)))
                {
                    // We don't know what command are we executing. It may be an invalid program...
                    // Or a non-executable file with a valid file association..
                    // Let CMD decide that... Invoke using "CMD /C" prefix ..
                    return new string[]
                        { currentShellExeName, "/c" }
                        .Concat(args).ToArray();
                }
                else
                {
                    args[0] = $"\"{exename}\"";
                    var newArgs = DoFixIfIsMicrosoftStoreApp(exename, args);
                    return newArgs.ToArray();
                }
            }
        }

        private static string[] DoFixIfIsMicrosoftStoreApp(string targetExe, string[] args)
        {
            // -- Workaround for https://github.com/gerardog/gsudo/issues/65

            // ISSUE: Apps installed via Microsoft Store, need a special attribute in it's security token to work (WIN://SYSAPPID),
            // That attrib is inserted by CreateProcess() Api, but gsudo replaces the special token with regular but elevated one
            // which doesnt have the attribute. So the app fails to load.

            // WORKAROUND: The CreateProcess(pwsh.exe) call must be already elevated so that Api can manipulate the final token, 
            // and the easiest way I found is delegate the final CreateProcess to an elevated CMD instance: To elevate "cmd /c pwsh.exe" instead.

            if (targetExe.IndexOf("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0) // Terrible but cheap Microsoft Store App detection.
            {
                Logger.Instance.Log("Applying workaround for target app installed via MSStore.", LogLevel.Debug);
                return new string[] {
                    Environment.GetEnvironmentVariable("COMSPEC"),
                    "/s /c" ,
                    $"\"{string.Join(" ", args)}\""};
            }
            else
                return args;
            // -- End of workaround.
        }
    }
}
