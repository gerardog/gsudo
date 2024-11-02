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


#if !DEBUG || !DISABLE_INTEGRITY
            bool PassingIntegrity = IntegrityHelpers.VerifyCallerProcess();
            if (!PassingIntegrity)
            {
                Logger.Instance.Log("The Elevator was not called from a trusted process", LogLevel.Error); // one liner errors.
                return -1;
            }
#endif

            var commandLine = ArgumentsHelper.GetRealCommandLine();
            var args = ArgumentsHelper.SplitArgs(commandLine);

            try
            {
                try
                {
                    cmd = new CommandLineParser(args).Parse();
                }
                finally
                {
                    Logger.Instance.Log($"Command Line: {commandLine}", LogLevel.Debug);
                }

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
                    await Task.Delay(1).ConfigureAwait(false); // force reset color on WSL.

                    if (InputArguments.Debug && !Console.IsInputRedirected)
                    {
                        if (cmd.GetType() == typeof(ServiceCommand))
                        {
                            Console.WriteLine("Service shutdown. This window will close in 10 seconds");
                            System.Threading.Thread.Sleep(10000);
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
