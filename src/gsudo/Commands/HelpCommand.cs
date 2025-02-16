﻿using System;
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

            /*
 -h | --help\t\tShows this help
 -v | --version\t\tShows gsudo version
             */

            Console.WriteLine(@"
Usage:
 gsudo [options]\t\t\tElevates your current shell
 gsudo [options] {command} [args]\tRuns {command} with elevated permissions
 gsudo cache [on | off | help] \t\tStarts/Stops an elevated cache session. (reduced UAC popups)
 gsudo status [--json]\t\t\tShows current user, cache and console status. 
 gsudo status {key} [--no-output]\tShows status filtered by json {key}. Boolean keys also returned as exit codes.
 gsudo !!\t\t\t\tRe-run last command as admin. (YMMV)

New Window options:
 -n | --new             Starts the command in a new console (and returns immediately).
 -w | --wait            When in new console, wait for the command to end and return the exitcode.
 --keepShell            Keep elevated shell open after running {command}.
 --keepWindow           When in new console, ask for keypress before closing the console.
 --close                Override settings and always close new window at end.

Security options:
 -i | --integrity {v}   Run with integrity level: Untrusted, Low, Medium, MediumPlus, High (default), System
 -u | --user {username} Run as the specified user. Asks for password. For local admins shows UAC unless '-i Medium'.
 -s | --system          Run as Local System account (NT AUTHORITY\SYSTEM).
 --ti                   Run as member of NT SERVICE\TrustedInstaller group.
 -k | --reset-timestamp Kills all cached credentials. The next time gsudo is run a UAC popup will be appear.

Shell related options:
 -d | --direct          Skip Shell detection. Assume CMD shell or CMD {command}.

Other options:
 --loglevel {val}       Set minimum log level to display: All, Debug, Info, Warning, Error, None.
 --debug                Enable debug mode.
 --copyns               Connect network drives to the elevated user. Warning: Interactive asks for credentials.
 --copyev               (deprecated) Copy all environment variables to the elevated process.
 --chdir {dir}          Change the current directory to {dir} before running the command.

Configuration:
 gsudo config\t\t\t\tShow current configuration settings & values.
 gsudo config {key} [--global] [value] \tRead or write a configuration setting.
 gsudo config {key} [--global] --reset \tReset a specific setting to its default value.
 gsudo config --reset-all \t\tReset all user and global settings to their default values.
 --global\t\t\t\tApplies to all users (overrides user-specific settings)

 (Note: User settings are stored at HKCU\Software\gsudo and globals at HKLM\Software\gsudo)

Usage from PowerShell: 
 gsudo [options] [--loadProfile] { ScriptBlock } [-args $argument1 [..., $argumentN]]
  { ScriptBlock }\t\tMust be wrapped in { curly brackets }
  --loadProfile\t\tWhen elevating PowerShell commands, load user profile.
 
 Example: gsudo { Write-Output ""Hello World"" } -args
 Tip: Add `Import-Module gsudoModule` to your $PROFILE for  tab auto-complete.
    
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
