using gsudo.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace gsudo.Helpers
{
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
            // syntax gsudo [gsudo options] [verb] [command to run]:

            // Parse [gsudo options]:
            return ParseOptions()
                // Parse [gsudo verb]:
                ?? ParseVerb()
                // Default = Run {command}:
                ?? new RunCommand() { CommandToRun = args };
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

                if (arg.In("help", "/?", "-?", "/h")) // Are actually illegal, but let's be nice and help.
                    return new HelpCommand();
                else if (arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
                {
                    var c = ParseOption(null, arg);
                    if (c != null)
                        return c;
                }
                else if (arg.StartsWith("-", StringComparison.OrdinalIgnoreCase)
                            && arg.NotIn("-encodedCommand")) // -encodedCommand is the start of the command in pwsh> gsudo { script block }
                {
                    foreach (string option in arg.Skip(1).Select(c => c.ToString(CultureInfo.InvariantCulture)))
                    {
                        var c = ParseOption(option, arg);
                        if (c!=null)
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

        ICommand ParseOption(string singleChar, string arg)
        {
            Func<string, string, bool> match = (string letter, string word) =>
            {
                if (
                    singleChar?.Equals(letter, StringComparison.OrdinalIgnoreCase)
                    ?? word?.Equals(arg, StringComparison.OrdinalIgnoreCase)
                    ?? false
                    )
                {
                    return true;
                }
                return false;
            };

            if (arg.In("--loglevel"))
            {
                arg = DeQueueArg();
                if (!Enum.TryParse<LogLevel>(arg, true, out var val))
                    throw new ApplicationException($"\"{arg}\" is not a valid LogLevel. Valid values are: All, Debug, Info, Warning, Error, None");

                Settings.LogLevel.Value = val;
            }
            else if (match("i", "--integrity"))
            {
                arg = DeQueueArg();
                if (!Enum.TryParse<IntegrityLevel>(arg, true, out var val))
                    throw new ApplicationException($"\"{arg}\" is not a valid IntegrityLevel. Valid values are: \n" +
                        $"Untrusted, Low, Medium, MediuPlus, High, System, Protected (unsupported), Secure (unsupported)");

                InputArguments.IntegrityLevel = val;
            }
            else if (match("n", "--new")) { InputArguments.NewWindow = true; }
            else if (match("w", "--wait")) { InputArguments.Wait = true; }
            else if (match("s", "--system")) { InputArguments.RunAsSystem = true; }
            else if (match("d", "--direct")) { InputArguments.Direct = true; }
            else if (match("k", "--reset-timestamp")) { InputArguments.KillCache = true; }
            else if (match(null, "--global")) { InputArguments.Global = true; }
            else if (match(null, "--ti")) { InputArguments.TrustedInstaller = InputArguments.RunAsSystem = true; }
            else if (match(null, "--loadProfile")) { Settings.PowerShellLoadProfile.Value = true; }
            else if (match(null, "--piped")) { Settings.ForcePipedConsole.Value = true; }
            else if (match(null, "--attached")) { Settings.ForceAttachedConsole.Value = true; }
            else if (match(null, "--vt")) { Settings.ForceVTConsole.Value = true; }
            else if (match(null, "--copyEV")) { Settings.CopyEnvironmentVariables.Value = true; }
            else if (match(null, "--copyNS")) { Settings.CopyNetworkShares.Value = true; }
            else if (match(null, "--debug")) { Settings.LogLevel.Value = LogLevel.All; InputArguments.Debug = true; }
            else if (match("v", "--version")) { return new ShowVersionHelpCommand(); }
            else if (match("h", "--help")) return new HelpCommand();
            else if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                if (singleChar != null)
                    throw new ApplicationException($"Invalid option: -{singleChar}");

                throw new ApplicationException($"Invalid option: {arg}");
            }
            else
            {
                // arg is not an option, requeue and parse as a command.
                args.AddFirst(arg);
            }

            return null;
        }

        private ICommand ParseVerb()
        {
            if (args.Count == 0)
            {
                if (InputArguments.KillCache
                    && !InputArguments.NewWindow
                    && !InputArguments.RunAsSystem
                    && !InputArguments.Wait
                    && !InputArguments.TrustedInstaller
                    && !InputArguments.Direct
                    )
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

            string arg;
            arg = DeQueueArg ();
            if (arg.Equals("help", StringComparison.OrdinalIgnoreCase))
                return new HelpCommand();

            if (arg.In("gsudoservice", "gsudoelevate"))
            {
                return new ServiceCommand()
                {
                    AllowedPid = int.Parse(DeQueueArg(), CultureInfo.InvariantCulture),
                    AllowedSid = DeQueueArg(),
                    LogLvl = ExtensionMethods.ParseEnum<LogLevel>(DeQueueArg()),
                    CacheDuration = Settings.TimeSpanParseWithInfinite(DeQueueArg()),
                    SingleUse = arg.In("gsudoelevate"),
                };
            }

            if (arg.In("gsudoctrlc"))
                return new CtrlCCommand()
                {
                    Pid = int.Parse(DeQueueArg(), CultureInfo.InvariantCulture),
                    sendSigBreak = bool.Parse(DeQueueArg())
                };

            if (arg.In("config"))
                return new ConfigCommand() { key = args.FirstOrDefault(), value = args.Skip(1) };

            if (arg.In("status"))
                return new StatusCommand();

            if (arg.In("cache"))
            {
                var cmd = new CacheCommand();
                while (args.Count > 0)
                {
                    arg = DeQueueArg();

                    if (arg.In("ON"))
                        cmd.Action = CacheCommandAction.On;
                    else if (arg.In("OFF"))
                        cmd.Action = CacheCommandAction.Off;
                    else if (arg.In("-h", "/h", "--help", "help")) 
                        cmd.Action = CacheCommandAction.Help;
                    else if (arg.In("-p", "--pid"))
                        cmd.AllowedPid = int.Parse(DeQueueArg(), CultureInfo.InvariantCulture);
                    else if (arg.In("-d", "--duration"))
                        cmd.CacheDuration = Settings.TimeSpanParseWithInfinite(DeQueueArg());
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

            return null;
        }
    }
}
