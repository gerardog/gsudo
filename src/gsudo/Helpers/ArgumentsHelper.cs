using gsudo.Commands;
using gsudo.Native;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace gsudo.Helpers
{
    public static class ArgumentsHelper
    {
        static readonly HashSet<string> CmdCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASSOC", "ATTRIB", "BREAK", "BCDEDIT", "CACLS", "CALL", "CD", "CHCP", "CHDIR", "CHKDSK", "CHKNTFS", "CLS", /*"CMD",*/ "COLOR", "COMP", "COMPACT", "CONVERT", "COPY", "DATE", "DEL", "DIR", "DISKPART", "DOSKEY", "DRIVERQUERY", "ECHO", "ENDLOCAL", "ERASE", "EXIT", "FC", "FIND", "FINDSTR", "FOR", "FORMAT", "FSUTIL", "FTYPE", "GOTO", "GPRESULT", "GRAFTABL", "HELP", "ICACLS", "IF", "LABEL", "MD", "MKDIR", "MKLINK", "MODE", "MORE", "MOVE", "OPENFILES", "PATH", "PAUSE", "POPD", "PRINT", "PROMPT", "PUSHD", "RD", "RECOVER", "REM", "REN", "RENAME", "REPLACE", "RMDIR", "ROBOCOPY", "SET", "SETLOCAL", "SC", "SCHTASKS", "SHIFT", "SHUTDOWN", "SORT", "START", "SUBST", "SYSTEMINFO", "TASKLIST", "TASKKILL", "TIME", "TITLE", "TREE", "TYPE", "VER", "VERIFY", "VOL", "XCOPY", "WMIC" };

        internal static string[] AugmentCommand(string[] args)
        {
            string currentShellExeName;
            Shell currentShell = ShellHelper.DetectInvokingShell(out currentShellExeName);

            if (currentShell.In(Shell.PowerShell, Shell.PowerShellCore, Shell.PowerShellCore623BuggedGlobalInstall))
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

                var newArgs = new List<string>();
                newArgs.Add($"\"{currentShellExeName}\"");

                if (currentShell == Shell.PowerShellCore623BuggedGlobalInstall)
                {
                    Logger.Instance.Log("Please update to PowerShell Core >= 6.2.4 to avoid profile loading.", LogLevel.Warning);
                }
                else
                {
                    newArgs.Add("-NoLogo");

                    if (args.Length > 0)
                    {
                        newArgs.Add("-NoProfile");
                        newArgs.Add("-Command");
                    }
                }

                if (args.Length > 0)
                    newArgs.AddMany(args);

                return newArgs.ToArray();
            }

            if (currentShell == Shell.Yori)
            {
                if (args.Length == 0)
                    return new[] {currentShellExeName};
                else
                    return new[] {currentShellExeName, "-c"}
                        .Concat(args).ToArray();
            }

            // Not Powershell, or Powershell Core, assume CMD.
            if (args.Length == 0)
            {
                return new string[]
                    { Environment.GetEnvironmentVariable("COMSPEC"), "/k" };
            }
            else
            {
                if (CmdCommands.Contains(args[0])) 
                    return new string[]
                        { Environment.GetEnvironmentVariable("COMSPEC"), "/c" }
                        .Concat(args).ToArray();

                var exename = ProcessFactory.FindExecutableInPath(UnQuote(args[0]));
                if (exename == null)
                {
                    // We don't know what command are we executing. It may be an invalid program...
                    // But let CMD decide that... Invoke using "CMD /C" prefix ..
                    return new string[]
                        { Environment.GetEnvironmentVariable("COMSPEC"), "/c" }
                        .Concat(args).ToArray();
                }
                else
                {
                    args[0] = $"\"{exename}\""; // replace command name with full+absolute+quoted path to resolve many issues, like "Batch files not started by create process if no extension is specified."
                    return args;
                }
            }
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

                SetTrueIf(arg, () => InputArguments.RunAsSystem = true, "-s", "--system") ||
                SetTrueIf(arg, () => InputArguments.Global = true, "--global") ||
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

            if (args.Count == 0)
            {
                if (InputArguments.KillCache)
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
            
            // Parse Command
            arg = dequeue();
            if (arg.Equals("help", StringComparison.OrdinalIgnoreCase))
                return new HelpCommand();

            if (arg.In("gsudoservice"))
            {
                    return new ServiceCommand()
                    {
                        AllowedPid = int.Parse(dequeue(), CultureInfo.InvariantCulture),
                        AllowedSid = dequeue(),
                        LogLvl = ExtensionMethods.ParseEnum<LogLevel>(dequeue()),
                        CacheDuration = Settings.TimeSpanParseWithInfinite(dequeue())
                    };
            }

            /*
            if (arg.In("gsudoservicehop"))
            {
                return new ServiceHopCommand()
                {
                    allowedPid = int.Parse(dequeue(), CultureInfo.InvariantCulture),
                    allowedSid = dequeue(),
                    LogLvl = ExtensionMethods.ParseEnum<LogLevel>(dequeue()),
                };
            }
            */

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

            /* Obsolete since TokenSwitch Mode
            if (arg.In("AttachRun"))
                return new AttachRun() { CommandToRun = args };
            */

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
                    else if (arg.In("-h", "/h" ,"--help", "help"))
                        cmd.Action = CacheCommandAction.Help;
                    else if (arg.In("-p", "--pid"))
                        cmd.AllowedPid = int.Parse(dequeue(), CultureInfo.InvariantCulture);
                    else if (arg.In("-d", "--duration"))
                        cmd.CacheDuration = Settings.TimeSpanParseWithInfinite(dequeue());
                    else 
                        throw new ApplicationException($"Unknown argument: {arg}");
                }

                return cmd;
            }

            if (arg.In("gsudoelevate"))
            {
                var cmd = new ElevateCommand();
                while (args.Count > 0)
                {
                    arg = dequeue();

                    if (arg.In("--pid"))
                        cmd.ProcessId = int.Parse(dequeue(), CultureInfo.InvariantCulture);
                }

                return cmd;
            }

            if (arg.In("run"))
                return new RunCommand() { CommandToRun = args };

            args.AddFirst(arg);
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
            Logger.Instance.Log($"Command Line: {commandLine}", LogLevel.Debug);

            if (commandLine[0] == '"')
                return commandLine.Substring(commandLine.IndexOf('"', 1) + 1).TrimStart(' ');
            else if (commandLine.IndexOf(' ', 1) >= 0)
                return commandLine.Substring(commandLine.IndexOf(' ', 1) + 1).TrimStart(' ');
            else
                return string.Empty;
        }

        public static string UnQuote(string v)
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
    }
}
