using System;
using System.Threading;
using System.Threading.Tasks;
using gsudo.Helpers;
using gsudo.Rpc;
using static gsudo.Native.ConsoleApi;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// Hosts a process that uses Win32 'AttachConsole' Api so its i/o is natively attached to the
    /// client console. 
    /// </summary>
    class AttachedConsoleHost : IProcessHost
    {
        public async Task Start(Connection connection, ElevationRequest elevationRequest)
        {
            var exitCode = 0;
            try
            {
                Native.ConsoleApi.FreeConsole();
                uint pid = (uint)elevationRequest.ConsoleProcessId;
                
                if (Native.ConsoleApi.AttachConsole(pid))
                {
                    Native.ConsoleApi.SetConsoleCtrlHandler(HandleConsoleCancelKeyPress, true);
                    System.Environment.CurrentDirectory = elevationRequest.StartFolder;

                    try
                    {
                        var process = Helpers.ProcessFactory.StartInProcessAtached(elevationRequest.FileName, elevationRequest.Arguments);

                        WaitHandle.WaitAny(new WaitHandle[] { process.GetWaitHandle(), connection.DisconnectedWaitHandle });
                        if (process.HasExited)
                            exitCode = process.ExitCode;

                        await Task.Delay(1).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error:{ex.ToString()}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
                        exitCode = Constants.GSUDO_ERROR_EXITCODE;
                    }
                }
                else
                {   
                    exitCode = Constants.GSUDO_ERROR_EXITCODE;
                }

                if (connection.IsAlive)
                {
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_EXITCODE}{exitCode}{Constants.TOKEN_EXITCODE}").ConfigureAwait(false);
                }

                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
            finally
            {
                Native.ConsoleApi.SetConsoleCtrlHandler(HandleConsoleCancelKeyPress, false);
                Native.ConsoleApi.FreeConsole();
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
        }

        private static bool HandleConsoleCancelKeyPress(CtrlTypes ctrlType)
        {
            if (ctrlType.In(CtrlTypes.CTRL_C_EVENT, CtrlTypes.CTRL_C_EVENT))
                return true;

            return false;
        }
    }
}