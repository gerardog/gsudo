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

        internal static void ShowVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{assembly.GetName().Name} v{GitVersionInformation.FullSemVer} ({GitVersionInformation.FullBuildMetaData})");

            Console.ResetColor();
            Console.WriteLine("Copyright(c) 2019-2022 Gerardo Grignoli and GitHub contributors");
        }

        internal static void ShowHelp()
        {
            ShowVersion();
            Console.WriteLine(@"
Usage:
------
gsudo [options]\t\t\t\tElevates your current shell
gsudo [options] {command} [args]\tRuns {command} with elevated permissions
gsudo [-h | --help]\t\t\tShows this help
gsudo [-v | --version]\t\t\tShows gsudo version
gsudo cache [on | off | help] \t\tStarts/Stops an elevated cache session. (reduced UAC popups)
gsudo config\t\t\t\tShow current config settings & values.
gsudo config {key} [--global] [value] \tRead or write a user setting
gsudo config {key} [--global] --reset \tReset config to default value
gsudo status\t\t\t\tShow status about current user, security, integrity level or other gsudo relevant data.

General options:
 -n | --new             Starts the command in a new console (and returns immediately).
 -w | --wait            When in new console, force wait for the command to end.

Security options:
 -i | --integrity {v}   Specify integrity level: Untrusted, Low, Medium, MediumPlus, High (default), System
 -k | --reset-timestamp Kills all cached credentials. The next time gsudo is run a UAC popup will be appear.
 -s | --system          Run as Local System account (NT AUTHORITY\SYSTEM).
 --ti                   Run as member of NT SERVICE\TrustedInstaller

Shell related options:
 -d | --direct          Execute {command} directly. Bypass shell wrapper (Pwsh/Yori/etc).
 --loadProfile          When elevating PowerShell commands, load user profile.

Other options:
 --loglevel {val}       Set minimum log level to display: All, Debug, Info, Warning, Error, None
 --debug                Enable debug mode.
 --copyns               Connect network drives to the elevated user. Warning: Verbose, interactive asks for credentials
 --copyev               (deprecated) Copy environment variables to the elevated process. (not needed on default console mode)

Learn more about security considerations of using gsudo at: https://gerardog.github.io/gsudo/docs/security".ReplaceOrdinal("\\t", "\t"));

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
