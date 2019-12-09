using CommandLine;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    [Verb("help")]
    class HelpCommand : ICommand
    {
        public Task<int> Execute()
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        internal void ShowVersion()
        {
            var asm = Assembly.GetExecutingAssembly().GetName();
            Console.WriteLine($"{asm.Name} v{Regex.Replace(asm.Version.ToString(), "^(.*?)(\\.0)*$", "$1")}");
            Console.WriteLine("Copyright(c) 2019 Gerardo Grignoli");
        }

        internal void ShowHelp()
        {
            ShowVersion();
            Console.WriteLine("");
            Console.WriteLine("gsudo [options] {command} [arguments] \r\n\t Runs {command} with elevated permissions");
            Console.WriteLine("gsudo \t Runs a cmd shell with elevated permissions");
            Console.WriteLine("gsudo [-h | --help] \t Shows this help");
            Console.WriteLine("gsudo [-v | --version] \t Shows gsudo version");
            Console.WriteLine();
            Console.WriteLine("Valid options:");
            Console.WriteLine(" --loglevel {All, Debug, Info, Warning, Error, None}\r\n      Log level");
            Console.WriteLine(" --debug\r\n      Enable debug mode. (makes gsudo service window visible)");
            Console.WriteLine(" -n | --new\r\n      Starts the command in a new console with elevated rights and returns immediately.");
            Console.WriteLine(" -w | --wait\r\n      Force wait for the process to end. (valid only for windows apps or combined with -n, otherwise will always wait).");

            Console.WriteLine(" --raw\r\n      Full terminal Use VT 100 ");
            Console.WriteLine(" --vt \r\n      Full terminal emulator (Enable debug mode. (makes gsudo service window visible)");

            return;
        }
    }
}
