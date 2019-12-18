using gsudo.Commands;
using gsudo.Helpers;
using System;
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

            cmd = ArgumentsHelper.ParseCommand(args);

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
            finally
            {
                try
                {
                    // cleanup console before returning.
                    Console.CursorVisible = true;
                    Console.ResetColor();
                }
                catch { }
            }
        }

    }
}
