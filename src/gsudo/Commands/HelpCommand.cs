using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    public class HelpCommand : ICommand
    {
        public virtual Task<int> Execute()
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        internal static void ShowVersion(bool verbose = true)
        {
            var assembly = Assembly.GetExecutingAssembly();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{assembly.GetName().Name} v{GitVersionInformation.FullSemVer} ({GitVersionInformation.FullBuildMetaData})");

            Console.ResetColor();
            if (verbose) Console.WriteLine("Copyright(c) 2019-2022 Gerardo Grignoli and GitHub contributors");
        }

        internal static void ShowHelp()
        {
            ShowVersion(false);
            Console.WriteLine(@"
Usage:
 gsudo [options]\t\t\tElevates your current shell
 gsudo [options] {command} [args]\tRuns {command} with elevated permissions

General options:
 -n | --new             Starts the command in a new console (and returns immediately).
 -w | --wait            When in new console, wait for the command to end.

Security options:
 -i | --integrity {v}   Specify integrity level: Untrusted, Low, Medium, MediumPlus, High (default), System
 -k | --reset-timestamp Kills all cached credentials. The next time gsudo is run a UAC popup will be appear.
 -u | --user {username} Run as the specified username. Asks for password. For admins, shows UAC unless '-i Medium'.
 -s | --system          Run as Local System account (NT AUTHORITY\SYSTEM).
 --ti                   Run as member of NT SERVICE\TrustedInstaller group

Shell related options:
 -d | --direct          Skip Shell detection. Asume CMD shell or CMD {command}.

Other options:
 --loglevel {val}       Set minimum log level to display: All, Debug, Info, Warning, Error, None
 --debug                Enable debug mode.
 --copyns               Connect network drives to the elevated user. Warning: Interactive asks for credentials
 --copyev               (deprecated) Copy all environment variables to the elevated process.
 -h | --help\t\tShows this help
 -v | --version\t\tShows gsudo version

Configuration:
 gsudo config\t\t\t\tShow current config settings & values.
 gsudo config {key} [--global] [value] \tRead or write a user setting
 gsudo config {key} [--global] --reset \tReset config to default value
 --global\t\t\t\tAffects all users (overrides user settings)

 gsudo status\t\t\t\tShows current user, cache and console status.
 gsudo cache [on | off | help] \t\tStarts/Stops an elevated cache session. (reduced UAC popups)
 gsudo !!\t\t\t\tRe-run last command as admin. (YMMV)

PowerShell: 
  gsudo [options] { Write-Output @args } -args ""Hello"", ""World"" 
  Wrap the script block in { curly brackets }
  --loadProfile          When elevating PowerShell commands, load user profile.

Warning: A running instance of gsudo can be hijacked by unprivileged software that runs on the same desktop.
Warning: A malicious process running on the same desktop may escalate privileges hacking a running gsudo instance.
Warning: Unprivileged software running on the same desktop may escalate privileges hacking a running gsudo instance.

Learn about security considerations of using gsudo at: https://gerardog.github.io/gsudo/docs/security
Learn more about security considerations of using gsudo at: https://gerardog.github.io/gsudo/docs/security
"

.ReplaceOrdinal("\\t", "\t"));
            return;
        }
    }

    class ShowVersionHelpCommand : HelpCommand
    {
        public override Task<int> Execute()
        {
            ShowVersion();
            return Task.FromResult(0);
        }

    }
}
