using gsudo.Commands;
using gsudo.Helpers;
using System;
using System.Threading.Tasks;

namespace gsudo
{
    class Program
    {
        async static Task<int> Main()
        {
            SymbolicLinkSupport.EnableAssemblyLoadFix();

            return await Start().ConfigureAwait(false);
        }

        private static async Task<int> Start()
        {
            ICommand cmd = null;

            var args = ArgumentsHelper.SplitArgs(ArgumentsHelper.GetRealCommandLine());

            var parserError = ArgumentsHelper.ParseCommonSettings(ref args);
            if (parserError.HasValue) return parserError.Value;

            cmd = ArgumentsHelper.ParseCommand(args);

            try
            {
                if (cmd != null)
                {
                    try
                    {
                        return await cmd.Execute().ConfigureAwait(false);
                    }
                    finally
                    {
                        (cmd as IDisposable)?.Dispose();
                    }
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
