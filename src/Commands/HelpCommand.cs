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
        public Task Execute()
        {
            ShowHelp();
            return Task.CompletedTask;
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
            Console.WriteLine("\t --loglevel {All, Debug, Info, Warning, Error, None}\r\n\t\t Log level");
            Console.WriteLine("\t --debug\r\n\t\t Enable diagnostics");
            Console.WriteLine("\t -e | --elevateonly\r\n\t\t Starts the command in a new console with elevated rights and returns immediately.");

            return;
        }
    }
}
