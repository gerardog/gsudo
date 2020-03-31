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
    [Obsolete("Superseded by TokenSwitch mode")] // TODO: Possible remove in 1.0
    class AttachedConsoleHost : IProcessHost
    {
        public async Task Start(Connection connection, ElevationRequest elevationRequest)
        {
            var exitCode = 0;

            if (Settings.SecurityEnforceUacIsolation)
                throw new Exception("Attached mode not supported when SecurityEnforceUacIsolation is set.");

            try
            {
                Native.ConsoleApi.SetConsoleCtrlHandler(ConsoleHelper.IgnoreConsoleCancelKeyPress, true);
                Native.ConsoleApi.FreeConsole();
                int pid = elevationRequest.ConsoleProcessId;
                if (Native.ConsoleApi.AttachConsole(pid))
                {
                    System.Environment.CurrentDirectory = elevationRequest.StartFolder;

                    try
                    {
                        var process = Helpers.ProcessFactory.StartAttached(elevationRequest.FileName, elevationRequest.Arguments);

                        WaitHandle.WaitAny(new WaitHandle[] { process.GetProcessWaitHandle(), connection.DisconnectedWaitHandle });
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
                    var ex = new System.ComponentModel.Win32Exception();
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: Attach Console Failed.\r\n{ex.Message}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
                    Logger.Instance.Log("Attach Console Failed.", LogLevel.Error);
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
                Native.ConsoleApi.SetConsoleCtrlHandler(ConsoleHelper.IgnoreConsoleCancelKeyPress, false);
                Native.ConsoleApi.FreeConsole();
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
        }
    }
}