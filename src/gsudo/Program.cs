using CommandLine;
using gsudo.Commands;
using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo
{
    class Program
    {
        async static Task<int> Main(string[] args)
        {
            SymbolicLinkSupport.EnableAssemblyLoadFix();

            return await Start(args).ConfigureAwait(false);
        }

        private static async Task<int> Start(string[] args)
        {
            ICommand cmd = null;

            var exitCode = ArgumentsHelper.ParseCommonSettings(ref args);
            if (exitCode.HasValue) return exitCode.Value;
            
            var errors = new List<Error>();
            using (var parser = new Parser(settings => settings.AutoHelp = false))
            {
                parser.ParseArguments<ServiceCommand, ConfigCommand, HelpCommand, CtrlCCommand, RunCommand>(args)
                    .WithParsed<ICommand>((c) => cmd = c)
                    .WithNotParsed(e => errors.AddRange(e));
            }

            if (errors.Any(e => e.Tag.In(ErrorType.BadVerbSelectedError, ErrorType.NoVerbSelectedError)))
            {
                cmd = new RunCommand()
                {
                    CommandToRun = args
                };
            }
            else if (cmd == null)
            {
                errors.ForEach((e) => Logger.Instance.Log($"Error parsing arguments: {e}", LogLevel.Error));
                cmd = new HelpCommand();
            }

            try
            {
                if (cmd != null)
                {
                    return await cmd.Execute().ConfigureAwait(false);
                }
                else
                    return await new HelpCommand().Execute().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Error);
                return Constants.GSUDO_ERROR_EXITCODE;
            }
        }

    }
}
