using gsudo.AppSettings;
using gsudo.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Principal;

namespace gsudo.Helpers
{
    // Why not use a parsing library? 
    // When gsudo was built on .Net Framework 4.x, loading the parsing library took significant time at startup.
    // This may no longer be the case on modern .net, but confirming that would require coding and comparing performance.
    public class CommandLineParser
    {
        LinkedList<string> args;

        public CommandLineParser(string args)
        {
            this.args = new LinkedList<string>(ArgumentsHelper.SplitArgs(args));
        }

        public CommandLineParser(IEnumerable<string> argsEnumerable)
        {
            args = new LinkedList<string>(argsEnumerable);
        }

        public ICommand Parse()
        {
            InputArguments.Clear();

            if (Settings.NewWindow_Force)
                InputArguments.NewWindow = true;

            // syntax: gsudo [options] [verb] [command to run]:

            ICommand command = ParseOptions();  // Parse [options]
            
            if (command == null)
                command = ParseVerb(); // Parse [verb]:

            ThrowIfInvalidInput();

            return command;
        }

        private void ThrowIfInvalidInput()
        {
            if ((InputArguments.TrustedInstaller || InputArguments.RunAsSystem || InputArguments.IntegrityLevel >= IntegrityLevel.System) && InputArguments.UserName != null)
                throw new ApplicationException("Can't use '-u' with `-s` or `--ti`.");
        }

        private string DeQueueArg()
        {
            if (args.Count == 0) throw new ApplicationException("Missing argument");
            var ret = args.First.Value;
            args.RemoveFirst();
            return ret;
        }

        private ICommand ParseOptions()
        {
            string arg;

            while (args.Count > 0)
            {
                arg = DeQueueArg();

                if (arg.In("-h", "/h", "/?", "-?", "--help", "help")) // Some are actually not-posix, but let's be nice and help.
                    return new HelpCommand();
                else if (arg == "--")
                {
                    // The -- argument terminates options parsing.
                    return null;
                }
                else if (arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
                {
                    var c = ParseOption(null, arg, out var skip);
                    if (c != null)
                        return c;
                }
                else if (arg.In("-noninteractive")) { } // ignore due to gerardog/gsudo#305
                else if (arg.StartsWith("-", StringComparison.OrdinalIgnoreCase)
                            && arg.NotIn("-encodedCommand")) // -encodedCommand is not posix compliant, but is what powershell sends on: gsudo { script block }
                                                             // So treat -encodedCommand as part of the CommandToRun, for gerardog/gsudo#160
                {
                    foreach (string option in arg.Skip(1).Select(c => c.ToString(CultureInfo.InvariantCulture)))
                    {
                        var c = ParseOption(option, arg, out var skipRemainingChars);
                        if (skipRemainingChars)
                            break;
                        if (c != null)
                            return c;
                    }
                }
                else
                {
                    args.AddFirst(arg);
                    return null;
                }
            }
            return null;
        }

        ICommand ParseOption(string argChar, string argWord, out bool skipRemainingChars)
        {
            skipRemainingChars = false;

            Func<string, string, bool> match = (string shortName, string longName)
                => CommandLineParser.IsOptionMatch(argChar, argWord, shortName, longName);

            if (IsOptionMatchWithArgument(argWord, null, "--loglevel", out var optionArg))
            {
                Settings.LogLevel.Value = ExtensionMethods.ParseEnum<LogLevel>(optionArg);
                skipRemainingChars = true;
            }
            else if (IsOptionMatchWithArgument(argWord, "i", "--integrity", out optionArg))
            {
                // InputArguments.IntegrityLevel = ExtensionMethods.ParseEnum<IntegrityLevel>(optionArg);
                skipRemainingChars = true;
            }
            else if (IsOptionMatchWithArgument(argWord, "u", "--user", out optionArg))
            {
                // InputArguments.SetUserName(optionArg);
                skipRemainingChars = true;
            }
            else if (match("n", "--new")) { InputArguments.NewWindow = true; }
            else if (match("w", "--wait")) { InputArguments.Wait = true; }

            // else if (match(null, "--keepshell")) { InputArguments.KeepShellOpen = true; InputArguments.KeepWindowOpen = false; }
            // else if (match(null, "--keepwindow")) { InputArguments.KeepWindowOpen = true; InputArguments.KeepShellOpen = false; }
            // else if (match(null, "--close")) { InputArguments.CloseNewWindow = true; InputArguments.KeepWindowOpen = false; InputArguments.KeepShellOpen = false; }

            // else if (match("s", "--system")) { InputArguments.RunAsSystem = true; }
            // else if (match("d", "--direct")) { InputArguments.Direct = true; }
            else if (match("k", "--reset-timestamp")) { InputArguments.KillCache = true; }
            // else if (match(null, "--global")) { InputArguments.Global = true; }
            // else if (match(null, "--ti")) { InputArguments.TrustedInstaller = InputArguments.RunAsSystem = true; }
            else if (match(null, "--loadProfile")) { Settings.PowerShellLoadProfile.Value = true; }
            // else if (match(null, "--piped")) { Settings.ForcePipedConsole.Value = true; }
            // else if (match(null, "--attached")) { Settings.ForceAttachedConsole.Value = true; }
            // else if (match(null, "--vt")) { Settings.ForceVTConsole.Value = true; }
            // else if (match(null, "--copyEV")) { Settings.CopyEnvironmentVariables.Value = true; }
            // else if (match(null, "--copyNS")) { Settings.CopyNetworkShares.Value = true; }
            else if (match(null, "--debug")) { Settings.LogLevel.Value = LogLevel.All; /*InputArguments.Debug = true;*/ }
            else if (match("v", "--version")) { return new ShowVersionHelpCommand(); }
            else if (match("h", "--help")) return new HelpCommand();
            else if (argWord.StartsWith("-", StringComparison.Ordinal))
            {
                if (argChar != null)
                    throw new ApplicationException($"Invalid option: {argChar} in {argWord}");

                throw new ApplicationException($"Invalid option: {argWord}");
            }
            else
            {
                // arg is not an option, requeue and parse as a command.
                args.AddFirst(argWord);
            }

            return null;
        }

        private ICommand ParseVerb()
        {
            // Parse `gsudo -k` as if `-k` was KillCache verb.
            if (args.Count == 0)
            {
                if (InputArguments.KillCache
                    && (!InputArguments.NewWindow || Settings.NewWindow_Force)
                    && !InputArguments.RunAsSystem
                    && !InputArguments.Wait
                    && !InputArguments.TrustedInstaller
                    && !InputArguments.Direct
                    && InputArguments.UserName == null
                    )
                {
                    // support for "-k" as command
                    // return a verbose command, instead of a silent argument.
                    // this is overly complicated because it supports a sudo-like experience like 'sudo -k' (kill as a verb/command)
                    // or 'sudo -k command' (kill as an argument)
                    InputArguments.KillCache = false;
                    return new KillCacheCommand(verbose: true);
                }

                return new RunCommand(commandToRun: Array.Empty<string>());
            }

            string arg;
            arg = DeQueueArg();

            if (arg.In("help"))
                return new HelpCommand();

            if (arg.In("gsudoservice", "gsudoelevate"))
                return new ServiceCommand()
                {
                    AllowedPid = int.Parse(DeQueueArg(), CultureInfo.InvariantCulture),
                    AllowedSid = DeQueueArg(),
                    LogLvl = ExtensionMethods.ParseEnum<LogLevel>(DeQueueArg()),
                    CacheDuration = Settings.TimeSpanParseWithInfinite(DeQueueArg()),
                    SingleUse = arg.In("gsudoelevate"),
                };

            // Internal use from Piped and VT mode:
            // gsudo gsudoctrlc {pid} {sendSigBreak: true/false}
            if (arg.In("gsudoctrlc"))
                return new CtrlCCommand()
                {
                    Pid = int.Parse(DeQueueArg(), CultureInfo.InvariantCulture),
                    SendSigBreak = bool.Parse(DeQueueArg())
                };

            if (arg.In("config"))
                return new HelpCommand();
                //return new ConfigCommand() { key = args.FirstOrDefault(), value = args.Skip(1) };

            if (arg.In("status"))
                return new HelpCommand();
            /*{
                var cmd = new StatusCommand();

                while (args.Count>0)
                {
                    arg = DeQueueArg();
                    if (arg.In("--json"))
                        cmd.AsJson = true;
                    else if (arg.In("--no-output"))
                        cmd.NoOutput = true;
                    else if(string.IsNullOrEmpty(cmd.Key))
                        cmd.Key = arg;
                    else throw new ApplicationException($"Invalid option: {arg}");
                };

                return cmd;
            }*/

            if (arg.In("cache"))
            {
                var cmd = new CacheCommand();
                while (args.Count > 0)
                {
                    arg = DeQueueArg();
                        
                    if (arg.In("on"))
                        cmd.Action = CacheCommandAction.On;
                    else if (arg.In("off"))
                        cmd.Action = CacheCommandAction.Off;
                    else if (arg.In("-h", "/h", "/?", "-?", "--help", "help"))
                        cmd.Action = CacheCommandAction.Help;
                    else if (IsOptionMatchWithArgument(arg, "p", "--pid", out string v))
                    {
                        int suppliedId = int.Parse(v, CultureInfo.InvariantCulture);
                        int parentId = IntegrityHelpers.GetParentProcess()?.Id ?? -1;
                        Logger.Instance.Log($"Using parent process PID ({parentId}) instead of supplied PID ({suppliedId})", LogLevel.Warning);
                        cmd.AllowedPid = parentId;
                    }
                    else if (IsOptionMatchWithArgument(arg, "d", "--duration", out v))
                    {
                        cmd.CacheDuration = Settings.TimeSpanParseWithInfinite(v);
                    }
                    else if (arg.In("-k"))
                        InputArguments.KillCache = true;
                    else
                        throw new ApplicationException($"Unknown argument: {arg}");
                }

                if (cmd.AllowedPid is null)
                {
                    int parentId = IntegrityHelpers.GetParentProcess()?.Id ?? -1;
                    Logger.Instance.Log($"Using parent process PID ({parentId}) as no PID was supplied", LogLevel.Warning);
                    cmd.AllowedPid = parentId;
                }
                
                return cmd;
            }

            if (arg.In("run"))
                return new RunCommand(commandToRun: args.ToArray());

            args.AddFirst(arg);

            if (arg == "!!" || arg.StartsWith("!", StringComparison.InvariantCulture))
                return new BangBangCommand() { Pattern = string.Join(" ", args) };

            return new RunCommand(commandToRun: args.ToArray());
        }

        #region Posix option matching functions
        private bool IsOptionMatchWithArgument(string argWord, string optionShortLetter, string optionLongName, out string argument)
        {
            var short1 = $"-{optionShortLetter}";
            if (optionShortLetter != null && argWord.StartsWith(short1, StringComparison.OrdinalIgnoreCase))
            {
                return IsOptionMatchWithArgument(argWord, short1, out argument);
            }
            else if (optionLongName != null && argWord.StartsWith(optionLongName, StringComparison.OrdinalIgnoreCase))
            {
                return IsOptionMatchWithArgument(argWord, optionLongName, out argument);
            }
            argument = null;
            return false;
        }

        private bool IsOptionMatchWithArgument(string argWord, string optionName, out string argument)
        {
            if (argWord.Length == optionName.Length)
            {
                argument = DeQueueArg();
                return true;
            }
            else
            {
                var startIndex = optionName.Length;
                if (argWord[startIndex] == '=')
                    startIndex++;
                else if (optionName.Length != 2)
                {
                    argument = null;
                    return false;
                }

                var optionArg = argWord.Substring(startIndex).Trim();

                argument = optionArg;
                return true;
            }
        }

        private static bool IsOptionMatch(string argChar, string argWord, string optionShortName, string optionLongName)
        {
            if (
                argChar?.Equals(optionShortName, StringComparison.OrdinalIgnoreCase)
                ?? optionLongName?.Equals(argWord, StringComparison.OrdinalIgnoreCase)
                ?? false
                )
            {
                return true;
            }
            return false;
        }

        #endregion
    }
}