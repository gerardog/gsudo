using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class HelpCommand : ICommand
    {
        public Task<int> Execute()
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
            Console.WriteLine("gsudo \n\tElevates your current shell in the current console window\r\n");
            Console.WriteLine("gsudo [options] {command} [arguments] \n\tRuns {command} with elevated permissions\r\n");
            Console.WriteLine("gsudo config\r\n\tShow current-user settings.\r\n");
            Console.WriteLine("gsudo config {key} [value | --reset] \r\n\tRead or write a user setting\r\n");
//            Console.WriteLine("gsudo confighelp \tGet additional help about config settings.");
            Console.WriteLine("gsudo [-h | --help]\tShows this help");
            Console.WriteLine("gsudo [-v | --version]\tShows gsudo version");
            Console.WriteLine("gsudo -k\t\tClears any cached credentials.");

            Console.WriteLine();
            Console.WriteLine("General options:");
            Console.WriteLine(" -n | --new        Starts the command in a new console (and returns immediately).");
            Console.WriteLine(" -w | --wait       Force wait for the command to end.");
            Console.WriteLine(" -s | --system     Run As Local System account (\"NT AUTHORITY\\SYSTEM\").");
            Console.WriteLine(" --copyev          Copy environment variables to the elevated process before executing.");
            Console.WriteLine(" --copyns          Connect current network drives to the elevated user. Warning! This is verbose, affects the elevated user system-wide, and can prompt for credentials interactively.");
            Console.WriteLine(" --piped           Force use of piped StdIn/Out/Err.");
            Console.WriteLine(" --vt              Force use of piped VT100 terminal emulator (experimental).");
            Console.WriteLine(" --loglevel {val}  Only show logs where level is at least the value specified. Valid values are: All, Debug, Info, Warning, Error, None");
            Console.WriteLine(" --debug           Enables debug mode. (makes gsudo service window visible and forces --loglevel All)");
            Console.WriteLine();
            Console.WriteLine("Credentials Cache options:");
            Console.WriteLine(" --nocache         Do not cache your elevated credentials.");
            Console.WriteLine(" --unsafe          Cache can be used by any process, and lasts until logoff or until 'gsudo -k' is ran.");
            Console.WriteLine($"\r\n  If no cache option is specified, your credentials will be cached for {Settings.CredentialsCacheDuration.Value.TotalSeconds} seconds,\r\n  and accessible only from the invoking process.");
            return;
        }
    }
}
