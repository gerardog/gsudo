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

            try
            {
                cmd = ArgumentsHelper.ParseCommand(args);
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
                return 0;
            }
            catch (ApplicationException ex)
            {
                Logger.Instance.Log(ex.Message, LogLevel.Error); // one liner errors.
                return Constants.GSUDO_ERROR_EXITCODE;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Error); // verbose errors.
                return Constants.GSUDO_ERROR_EXITCODE;
            }
            finally
            {
                if (InputArguments.KillCache)
                {
                    await new KillCacheCommand(verbose: false).Execute().ConfigureAwait(false);
                }

                try
                {
                    // cleanup console before returning.
                    Console.CursorVisible = true;
                    Console.ResetColor();

                    if (InputArguments.Debug && !Console.IsInputRedirected && cmd.GetType().In(typeof(ServiceCommand), typeof(ElevateCommand)))
                    {
                        Console.WriteLine("Press any key to exit.");
                        Console.ReadKey();
                    }
                }
                catch { }
            }
        }
    }
}
