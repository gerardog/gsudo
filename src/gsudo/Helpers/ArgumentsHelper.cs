using gsudo.Commands;
using gsudo.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace gsudo.Helpers
{
    public static class ArgumentsHelper
    {
        static readonly HashSet<string> CMD_COMMANDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASSOC", "ATTRIB", "BREAK", "BCDEDIT", "CACLS", "CALL", "CD", "CHCP", "CHDIR", "CHKDSK", "CHKNTFS", "CLS", /*"CMD",*/ "COLOR", "COMP", "COMPACT", "CONVERT", "COPY", "DATE", "DEL", "DIR", "DISKPART", "DOSKEY", "DRIVERQUERY", "ECHO", "ENDLOCAL", "ERASE", "EXIT", "FC", "FIND", "FINDSTR", "FOR", "FORMAT", "FSUTIL", "FTYPE", "GOTO", "GPRESULT", "GRAFTABL", "HELP", "ICACLS", "IF", "LABEL", "MD", "MKDIR", "MKLINK", "MODE", "MORE", "MOVE", "OPENFILES", "PATH", "PAUSE", "POPD", "PRINT", "PROMPT", "PUSHD", "RD", "RECOVER", "REM", "REN", "RENAME", "REPLACE", "RMDIR", "ROBOCOPY", "SET", "SETLOCAL", "SC", "SCHTASKS", "SHIFT", "SHUTDOWN", "SORT", "START", "SUBST", "SYSTEMINFO", "TASKLIST", "TASKKILL", "TIME", "TITLE", "TREE", "TYPE", "VER", "VERIFY", "VOL", "XCOPY", "WMIC" };

        internal static string[] AugmentCommand(string[] args)
        {
            string currentShellExeName;
            Shell currentShell = ShellHelper.DetectInvokingShell(out currentShellExeName);

            // Is our current shell Powershell ? (Powershell.exe -calls-> gsudo)
            if (currentShell.In(Shell.PowerShell, Shell.PowerShellCore6, Shell.PowerShellCore7))
            {
                var newArgs = new List<string>();
                newArgs.Add(currentShellExeName);

                if (currentShell == Shell.PowerShell && !string.IsNullOrEmpty(GlobalSettings.PowerShellArguments)) 
                    newArgs.Add(GlobalSettings.PowerShellArguments);

                if (currentShell == Shell.PowerShellCore6 && !string.IsNullOrEmpty(GlobalSettings.PowerShellCore6Arguments)) 
                    newArgs.Add(GlobalSettings.PowerShellCore6Arguments);

                if (currentShell == Shell.PowerShellCore7 && !string.IsNullOrEmpty(GlobalSettings.PowerShellCore7Arguments))
                    newArgs.Add(GlobalSettings.PowerShellCore7Arguments);

                if (args.Length>0)
                    newArgs.AddMany(args);

                return newArgs.ToArray();
            }
            
            // Not Powershell, or Powershell Core, assume CMD.
            if (args.Length == 0)
            {
                return new string[]
                    { Environment.GetEnvironmentVariable("COMSPEC"), "/k" };
            }
            else
            {
                if (CMD_COMMANDS.Contains(args[0]))
                    return new string[]
                        { Environment.GetEnvironmentVariable("COMSPEC"), "/c" }
                        .Concat(args).ToArray();

                var exename = ProcessFactory.FindExecutableInPath(UnQuote(args[0]));
                if (exename==null)
                {
                    // add "CMD /C" prefix to commands such as MD, CD, DIR..
                    // We are sure this will not be an interactive experience, 

                    return new string[]
                        { Environment.GetEnvironmentVariable("COMSPEC"), "/c" }
                        .Concat(args).ToArray();
                }
                else
                {
                    args[0] = exename; // Batch files not started by create process if no extension is specified.
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
                    insideQuotes=!insideQuotes;
                else if (args[curr] == ' ' && !insideQuotes)
                {
                    results.Add(args.Substring(pushed, curr-pushed));
                    pushed = curr+1;
                }
                curr++;                
            }

            if (pushed < curr)
                results.Add(args.Substring(pushed, curr - pushed));
            return results;
        }

        internal static int? ParseCommonSettings(ref IEnumerable<string> args)
        {
            Stack<string> stack = new Stack<string>(args.Reverse());

            while (stack.Any())
            {
                var arg = stack.Peek();
                if (arg.In("-v", "--version"))
                {
                    HelpCommand.ShowVersion();
                    return 0;
                }
                else if (arg.In("-h", "--help", "help", "/?"))
                {
                    HelpCommand.ShowHelp();
                    return 0;
                }
                else if (arg.In("-n", "--new"))
                {
                    GlobalSettings.NewWindow = true;
                    stack.Pop();
                }
                else if (arg.In("-w", "--wait"))
                {
                    GlobalSettings.Wait = true;
                    stack.Pop();
                }
                else if (arg.In("--debug"))
                {
                    GlobalSettings.Debug = true;
                    GlobalSettings.LogLevel.Value = LogLevel.All;
                    stack.Pop();
                }
                else if (arg.In("--loglevel"))
                {
                    stack.Pop();
                    arg = stack.Pop();
                    try
                    {
                        GlobalSettings.LogLevel.Value = (LogLevel)Enum.Parse(typeof(LogLevel), arg, true);
                    }
                    catch
                    {
                        Logger.Instance.Log($"\"{arg}\" is not a valid LogLevel. Valid values are: All, Debug, Info, Warning, Error, None", LogLevel.Error);
                        return Constants.GSUDO_ERROR_EXITCODE;
                    }
                }
                else if (arg.In("--raw"))
                {
                    GlobalSettings.ForceRawConsole.Value = true;
                    stack.Pop();
                }
                else if (arg.In("--vt"))
                {
                    GlobalSettings.ForceVTConsole.Value = true;
                    stack.Pop();
                }
                else if (arg.In("--copyEV"))
                {
                    GlobalSettings.CopyEnvironmentVariables.Value = true;
                    stack.Pop();
                }
                else if (arg.In("--copyNS"))
                {
                    GlobalSettings.CopyNetworkShares.Value = true;
                    stack.Pop();
                }
                else if (arg.In("-s", "--system"))
                {
                    GlobalSettings.RunAsSystem = true;
                    stack.Pop();
                }
                else if (arg.StartsWith("-", StringComparison.Ordinal))
                {
                    Logger.Instance.Log($"Invalid option: {arg}", LogLevel.Error);
                    return Constants.GSUDO_ERROR_EXITCODE;
                }
                else
                {
                    break;
                }
            }

            args = stack.ToArray();
            return null;
        }

        internal static ICommand ParseCommand(IEnumerable<string> argsEnumerable)
        {
            var args = argsEnumerable.ToArray();
            if (args.Length == 0) return new RunCommand() { CommandToRun = Array.Empty<string>() };

            if (args[0].Equals("run", StringComparison.OrdinalIgnoreCase))
                return new RunCommand() { CommandToRun = args.Skip(1) };

            if (args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
                return new HelpCommand();

            if (args[0].In("gsudoservice", "gsudosystemservice"))
            {
                bool hasLoglevel = false;
                LogLevel logLevel = LogLevel.Info;
                if (args.Length>3)
                {
                    hasLoglevel = Enum.TryParse<LogLevel>(args[3], true, out logLevel);
                }
                var allowedPid = int.Parse(args[1], CultureInfo.InvariantCulture);
                var allowedSid = args[2];

                if (args[0].In("gsudoservice"))
                {
                    return new ServiceCommand()
                    {
                        allowedPid = allowedPid,
                        allowedSid = allowedSid,
                        LogLvl = hasLoglevel ? logLevel : (LogLevel?)null,
                    };
                }
                else
                {
                    return new SystemServiceCommand()
                    {
                        allowedPid = allowedPid,
                        allowedSid = allowedSid,
                        LogLvl = hasLoglevel ? logLevel : (LogLevel?)null,
                    };
                }
            }

            if (args[0].Equals("gsudoctrlc", StringComparison.OrdinalIgnoreCase))
                return new CtrlCCommand() { pid = int.Parse(args[1], CultureInfo.InvariantCulture) };

            if (args[0].Equals("config", StringComparison.OrdinalIgnoreCase))
                return new ConfigCommand() { key = args.Skip(1).FirstOrDefault(), value = args.Skip(2) };

            return new RunCommand() { CommandToRun = args };
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
