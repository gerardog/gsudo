using gsudo.Helpers;
using gsudo.Rpc;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// Hosts a process, in a new window. 
    /// So no I/O streaming is needed, other than process exit code.
    /// </summary>
    [Obsolete("Superseded by TokenSwitch mode")] // TODO: Possible remove in 1.0
    class NewWindowProcessHost : IProcessHost
    {
        private Process process;

        public async Task Start(Connection connection, ElevationRequest request)
        {
            try
            {
                int exitCode = 0;
                process = ProcessFactory.StartDetached(request.FileName, request.Arguments, request.StartFolder, false);

                // Windows allows us to SetFocus on the new window if only if
                // this gsudo (service) has the focus. So we request the gsudo client to set focus as well.
                await connection.ControlStream
                    .WriteAsync($"{Constants.TOKEN_FOCUS}{process.MainWindowHandle}{Constants.TOKEN_FOCUS}")
                    .ConfigureAwait(false);

                if (request.Wait)
                {
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                await connection.ControlStream
                    .WriteAsync($"{Constants.TOKEN_EXITCODE}{exitCode}{Constants.TOKEN_EXITCODE}")
                    .ConfigureAwait(false);

                await connection.FlushAndCloseAll().ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Error);
                if (connection.IsAlive)
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: {ex.ToString()}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);

                await connection.FlushAndCloseAll().ConfigureAwait(false);
                return;
            }
        }

    }
}
