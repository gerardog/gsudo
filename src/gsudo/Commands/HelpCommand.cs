using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class HelpCommand : ICommand
    {
        public virtual Task<int> Execute()
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        internal static void ShowVersion()
        {
            var asm = Assembly.GetExecutingAssembly().GetName();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{asm.Name} v{Regex.Replace(asm.Version.ToString(), "^(.*?)(\\.0)*$", "$1")}");
            Console.ResetColor();
            Console.WriteLine("Copyright(c) 2019-2020 Gerardo Grignoli and GitHub contributors");
        }

        internal static void ShowHelp()
        {
            ShowVersion();
            Console.WriteLine("\nUsage:\n------");
            Console.WriteLine("gsudo \t\t\t\t\tElevates your current shell");
            Console.WriteLine("gsudo [options] {command} [args]\tRuns {command} with elevated permissions");
            Console.WriteLine("gsudo [-h | --help]\t\t\tShows this help");
            Console.WriteLine("gsudo [-v | --version]\t\t\tShows gsudo version");
            Console.WriteLine("gsudo -k\t\t\t\tClears any cached credentials.");
            Console.WriteLine("gsudo config\t\t\t\tShow current-user settings.");
            Console.WriteLine("gsudo config {key} [--global] [value] \tRead or write a user setting");
            Console.WriteLine("gsudo config {key} [--global] --reset \tReset config to default value");
            //            Console.WriteLine("gsudo confighelp \tGet additional help about config settings.");
            Console.WriteLine();
            Console.WriteLine("General options:");
            Console.WriteLine(" -n | --new             Starts the command in a new console (and returns immediately).");
            Console.WriteLine(" -w | --wait            When in new console, force wait for the command to end.");
            Console.WriteLine(" -s | --system          Run As Local System account (\"NT AUTHORITY\\SYSTEM\").");
            Console.WriteLine(" -i | --integrity {v}   Specify integrity level: Untrusted, Low, Medium, MediuPlus, High (default), System");
            Console.WriteLine(" -k | --reset-timestamp Kills all cached credentials. The next time gsudo is run a UAC popup will be appear.");
            Console.WriteLine(" --copyev               Copy environment variables to the elevated process");
            Console.WriteLine(" --copyns               Connect network drives to the elevated user. Warning: Verbose, interactive asks for credentials");
            Console.WriteLine();
            Console.WriteLine("Credentials Cache options:");
            Console.WriteLine(" --nocache              Do not cache your elevated credentials.");
            Console.WriteLine(" --unsafe               Cache can be used by any process, and lasts until logoff or until 'gsudo -k' is ran.");
            Console.WriteLine($"  If no cache option is specified, your credentials will be cached for {Settings.CredentialsCacheDuration.Value.TotalSeconds} seconds.");
            Console.WriteLine();
            Console.WriteLine("Other options:");
            Console.WriteLine(" --loglevel {val}       Set minimum log level to display: All, Debug, Info, Warning, Error, None");
            Console.WriteLine(" --piped                Set console mode to piped StdIn/Out/Err.");
            Console.WriteLine(" --vt                   Set console mode to piped VT100 ConPty/PseudoConsole (experimental).");
            Console.WriteLine(" --debug                Enable debug mode.");

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
