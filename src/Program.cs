using CommandLine;
using gsudo.Commands;
using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo
{
    class Program
    {
        async static Task Main(string[] args)
        {
            Environment.SetEnvironmentVariable("PROMPT", "$P# ");
            ICommand cmd = null;

            Stack<string> stack = new Stack<string>(args.Reverse());

            while (stack.Any())
            {
                var arg = stack.Peek();
                if (arg.In("-v", "--version"))
                {
                    new HelpCommand().ShowVersion();
                    return;
                }
                else if (arg.In("-h", "--help", "help"))
                {
                    new HelpCommand().ShowHelp();
                    return;
                }
                if (arg.In("-e", "--elevateonly"))
                {
                    Globals.ElevateOnly = true;
                    stack.Pop();
                }
                else if (arg.In("--debug"))
                {
                    Globals.Debug = true;
                    Globals.LogLevel = LogLevel.All;
                    stack.Pop();
                }
                else if (arg.In("--loglevel"))
                {
                    stack.Pop();
                    arg = stack.Pop();
                    try
                    {
                        Globals.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), arg, true);
                    }
                    catch
                    {
                        Globals.Logger.Log($"\"{arg}\" is not a valid LogLevel. Valid values are: All, Debug, Info, Warning, Error, None", LogLevel.Error);
                        return;
                    }
                }
                else
                {
                    break;
                }
            }

            args = stack.ToArray();

            var parser = new Parser(settings => settings.AutoHelp = false);
            var errors = new List<Error>();

            parser.ParseArguments<ServiceCommand, ConfigCommand, HelpCommand>(args)
                .WithParsed<ICommand>((c) => cmd = c)
                .WithNotParsed(e => errors.AddRange(e));

            if (errors.Any(e => e.Tag.In(ErrorType.BadVerbSelectedError, ErrorType.NoVerbSelectedError)))
            {
                cmd = new RunCommand()
                {
                    Arguments = args
                };
            }
            else if (cmd == null)
            {
                errors.ForEach((e) => Globals.Logger.Log($"Error parsing arguments: {e}", LogLevel.Error));
                cmd = new HelpCommand();
            }

            try
            {
                if (cmd != null)
                    await cmd.Execute().ConfigureAwait(false);
                else
                    await new HelpCommand().Execute().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Globals.Logger.Log(ex.ToString(), LogLevel.Error);
            }
        }
    }
}
