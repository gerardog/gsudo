using gsudo.Commands;
using gsudo.Native;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace gsudo.Helpers
{
    public static class ArgumentsHelper
    {
        static readonly HashSet<string> CmdCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASSOC", "ATTRIB", "BREAK", "BCDEDIT", "CACLS", "CALL", "CD", "CHCP", "CHDIR", "CHKDSK", "CHKNTFS", "CLS", /*"CMD",*/ "COLOR", "COMP", "COMPACT", "CONVERT", "COPY", "DATE", "DEL", "DIR", "DISKPART", "DOSKEY", "DRIVERQUERY", "ECHO", "ENDLOCAL", "ERASE", "EXIT", "FC", "FIND", "FINDSTR", "FOR", "FORMAT", "FSUTIL", "FTYPE", "GOTO", "GPRESULT", "GRAFTABL", "HELP", "ICACLS", "IF", "LABEL", "MD", "MKDIR", "MKLINK", "MODE", "MORE", "MOVE", "OPENFILES", "PATH", "PAUSE", "POPD", "PRINT", "PROMPT", "PUSHD", "RD", "RECOVER", "REM", "REN", "RENAME", "REPLACE", "RMDIR", "ROBOCOPY", "SET", "SETLOCAL", "SC", "SCHTASKS", "SHIFT", "SHUTDOWN", "SORT", "START", "SUBST", "SYSTEMINFO", "TASKLIST", "TASKKILL", "TIME", "TITLE", "TREE", "TYPE", "VER", "VERIFY", "VOL", "XCOPY", "WMIC" };
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

                    var newArgs = new List<string>();
                    newArgs.Add($"\"{currentShellExeName}\"");
                    newArgs.AddMany(args);

                    return newArgs.ToArray();
                }
                else if (currentShell.In(Shell.PowerShell, Shell.PowerShellCore))
                {
                    var newArgs = new List<string>();

                    newArgs.Add($"\"{currentShellExeName}\"");
                    newArgs.Add("-NoLogo");

                    if (args.Length > 0)
                    {
                        if (!Settings.PowerShellLoadProfile)
                            newArgs.Add("-NoProfile");
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

                        string pscommand = string.Join(" ", args);

                        pscommand = pscommand
                                        .Replace("\"", "\\\"")
                                        .Quote();
                        newArgs.Add(pscommand);
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
                if (CmdCommands.Contains(args[0])) 
                    return new string[]
                        { currentShellExeName, "/c" }
                        .Concat(args).ToArray();

                var exename = ProcessFactory.FindExecutableInPath(UnQuote(args[0]));
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

        public static IEnumerable<string> SplitArgs(string args)
        {
            args = args.Trim();
            var results = new List<string>();
            int pushed = 0;
            int curr = 0;
            bool insideQuotes = false;
            while (curr < args.Length)
            {
                if (args[curr] == '"')
                    insideQuotes = !insideQuotes;
                else if (args[curr] == ' ' && !insideQuotes)
                {
                    if ((curr - pushed) > 0)
                        results.Add(args.Substring(pushed, curr - pushed));
                    pushed = curr + 1;
                }
                curr++;
            }

            if (pushed < curr)
                results.Add(args.Substring(pushed, curr - pushed));
            return results;
        }

        internal static ICommand ParseCommand(IEnumerable<string> argsEnumerable)
        {
            // Parse Options
            var args = new LinkedList<string>(argsEnumerable);
            Func<string> dequeue = () =>
            {
                if (args.Count == 0) throw new ApplicationException("Missing argument");
                var ret = args.First.Value;
                args.RemoveFirst();
                return ret;
            };

            // syntax gsudo [gsudo options] [verb] [command to run]:

            // Parse [gsudo options]:
            string arg;
            while (args.Count>0)
            {
                arg = dequeue();

                if (
                SetTrueIf(arg, () => InputArguments.NewWindow = true, "-n", "--new") ||
                SetTrueIf(arg, () => InputArguments.Wait = true, "-w", "--wait") ||

                // Legacy, now undocumented features.
                SetTrueIf(arg, () => Settings.ForcePipedConsole.Value = true, "--piped", "--raw" /*--raw for backward compat*/) ||
                SetTrueIf(arg, () => Settings.ForceAttachedConsole.Value = true, "--attached") ||
                SetTrueIf(arg, () => Settings.ForceVTConsole.Value = true, "--vt") ||
                SetTrueIf(arg, () => Settings.CopyEnvironmentVariables.Value = true, "--copyEV") ||
                SetTrueIf(arg, () => Settings.CopyNetworkShares.Value = true, "--copyNS") ||
                SetTrueIf(arg, () => Settings.PowerShellLoadProfile.Value = true, "--loadProfile") ||

                SetTrueIf(arg, () => InputArguments.RunAsSystem = true, "-s", "--system") ||
                SetTrueIf(arg, () => InputArguments.Global = true, "--global") ||
                SetTrueIf(arg, () => InputArguments.Direct = true, "-d", "--direct") ||
                SetTrueIf(arg, () => InputArguments.KillCache = true, "-k", "--reset-timestamp")
                   )
                { }
                else if (arg.In("-v", "--version"))
                    return new ShowVersionHelpCommand();
                else if (arg.In("-h", "--help", "help", "/?", "/h"))
                    return new HelpCommand();
                else if (arg.In("--debug"))
                {
                    InputArguments.Debug = true;
                    Settings.LogLevel.Value = LogLevel.All;
                }
                else if (arg.In("--loglevel"))
                {
                    arg = dequeue();
                    if (!Enum.TryParse<LogLevel>(arg, true, out var val))
                        throw new ApplicationException($"\"{arg}\" is not a valid LogLevel. Valid values are: All, Debug, Info, Warning, Error, None");

                    Settings.LogLevel.Value = val;
                }
                else if (arg.In("-i", "--integrity"))
                {
                    arg = dequeue();
                    if (!Enum.TryParse<IntegrityLevel>(arg, true, out var val))
                        throw new ApplicationException($"\"{arg}\" is not a valid IntegrityLevel. Valid values are: \n" +
                            $"Untrusted, Low, Medium, MediuPlus, High, System, Protected (unsupported), Secure (unsupported)");

                    InputArguments.IntegrityLevel = val;
                }
                else if (arg.StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ApplicationException($"Invalid option: {arg}");
                }
                else
                {
                    // arg is not an option, requeue and parse as a command.
                    args.AddFirst(arg);
                    break;
                }
            }

            // Parse [verb]:

            if (args.Count == 0)
            {
                if (InputArguments.KillCache && !InputArguments.NewWindow)
                {
                    // support for "-k" as command
                    // return a verbose command, instead of a silent argument.
                    // this is overly complicated because it supports a sudo-like experience like 'sudo -k' (kill as a verb/command)
                    // or 'sudo -k command' (kill as an argument)
                    InputArguments.KillCache = false; 
                    return new KillCacheCommand(verbose: true); 
                }

                return new RunCommand() { CommandToRun = Array.Empty<string>() };
            }
            
            arg = dequeue();
            if (arg.Equals("help", StringComparison.OrdinalIgnoreCase))
                return new HelpCommand();

            if (arg.In("gsudoservice", "gsudoelevate"))
            {
                    return new ServiceCommand()
                    {
                        AllowedPid = int.Parse(dequeue(), CultureInfo.InvariantCulture),
                        AllowedSid = dequeue(),
                        LogLvl = ExtensionMethods.ParseEnum<LogLevel>(dequeue()),
                        CacheDuration = Settings.TimeSpanParseWithInfinite(dequeue()),
                        SingleUse = arg.In("gsudoelevate")
                    };
            }

            if (arg.In("gsudoctrlc"))
                return new CtrlCCommand() 
                { 
                    Pid = int.Parse(dequeue(), CultureInfo.InvariantCulture), 
                    sendSigBreak = bool.Parse(dequeue()) 
                };

            if (arg.In("config"))
                return new ConfigCommand() { key = args.FirstOrDefault(), value = args.Skip(1) };

            if (arg.In("status"))
                return new StatusCommand();

            if (arg.In("Cache"))
            {
                var cmd = new CacheCommand();
                while (args.Count > 0)
                {
                    arg = dequeue();

                    if (arg.In("ON"))
                        cmd.Action = CacheCommandAction.On;
                    else if (arg.In("OFF"))
                        cmd.Action = CacheCommandAction.Off;
                    else if (arg.In("-h", "/h", "--help", "help"))
                        cmd.Action = CacheCommandAction.Help;
                    else if (arg.In("-p", "--pid"))
                        cmd.AllowedPid = int.Parse(dequeue(), CultureInfo.InvariantCulture);
                    else if (arg.In("-d", "--duration"))
                        cmd.CacheDuration = Settings.TimeSpanParseWithInfinite(dequeue());
                    else if (arg.In("-k"))
                        InputArguments.KillCache = true;
                    else
                        throw new ApplicationException($"Unknown argument: {arg}");
                }

                return cmd;
            }

            if (arg.In("run"))
                return new RunCommand() { CommandToRun = args };

            args.AddFirst(arg);
            
            if (arg == "!!" || arg.StartsWith("!", StringComparison.InvariantCulture))
                return new BangBangCommand() { Pattern = string.Join(" ", args) };

            // Parse {command}:

            return new RunCommand() { CommandToRun = args };
        }

        private static bool SetTrueIf(string arg, Action setting, params string[] v)
        {
            if (arg.In(v))
            {
                setting();
                return true;
            }
            return false;
        }

        internal static string GetRealCommandLine()
        {
            System.IntPtr ptr = ConsoleApi.GetCommandLine();
            string commandLine = Marshal.PtrToStringAuto(ptr).TrimStart();

            if (commandLine[0] == '"')
                return commandLine.Substring(commandLine.IndexOf('"', 1) + 1).TrimStart(' ');
            else if (commandLine.IndexOf(' ', 1) >= 0)
                return commandLine.Substring(commandLine.IndexOf(' ', 1) + 1).TrimStart(' ');
            else
                return string.Empty;
        }

        public static string UnQuote(this string v)
        {
            if (string.IsNullOrEmpty(v))
                return v;
            if (v[0] == '"' && v[v.Length - 1] == '"')
                return v.Substring(1, v.Length - 2);
            if (v[0] == '"' && v.Trim().EndsWith("\"", StringComparison.Ordinal))
                return UnQuote(v.Trim());
            if (v[0] == '"')
                return v.Substring(1);
            else
                return v;
        }

        public static string Quote(this string v)
        {
            return $"\"{v}\"";
        }
    }
}
