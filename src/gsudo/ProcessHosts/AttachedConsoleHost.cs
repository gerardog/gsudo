using System;
using System.Security.Principal;
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
    // This mode is not enabled unless you use --attached.
    class AttachedConsoleHost : IProcessHost
    {
        public bool SupportsSimultaneousElevations { get; } = false;

        public async Task Start(Connection connection, ElevationRequest elevationRequest)
        {
            var exitCode = 0;

            if (Settings.SecurityEnforceUacIsolation)
                throw new Exception("Attached mode not supported when SecurityEnforceUacIsolation is set.");

            try
            {
                Native.ConsoleApi.FreeConsole();
                int pid = elevationRequest.ConsoleProcessId;
                if (Native.ConsoleApi.AttachConsole(pid))
                {
                    Native.ConsoleApi.SetConsoleCtrlHandler(ConsoleHelper.IgnoreConsoleCancelKeyPress, true);

                    try
                    {
                        try
                        {
                            System.Environment.CurrentDirectory = elevationRequest.StartFolder;
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            throw new ApplicationException($"User \"{WindowsIdentity.GetCurrent().Name}\" can not access current directory \"{elevationRequest.StartFolder}\"");
                        }

                        var process = Helpers.ProcessFactory.StartAttached(elevationRequest.FileName, elevationRequest.Arguments);

                        WaitHandle.WaitAny(new WaitHandle[] { process.GetProcessWaitHandle(), connection.DisconnectedWaitHandle });
                        if (process.HasExited)
                            exitCode = process.ExitCode;

                        await Task.Delay(1).ConfigureAwait(false);
                    }
                    catch (ApplicationException ex)
                    {
                        await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: {ex.Message}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
                        exitCode = Constants.GSUDO_ERROR_EXITCODE;
                    }
                    catch (Exception ex)
                    {
                        await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: {ex.ToString()}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
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