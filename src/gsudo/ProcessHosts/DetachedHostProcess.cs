using gsudo.Helpers;
using gsudo.Rpc;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo.ProcessHosts
{
    class DetachedHostProcess : IProcessHost
    {
        private Process process;

        public async Task Start(Connection connection, ElevationRequest request)
        {
            try
            {
                int exitCode = 0;
                process = ProcessFactory.StartDetached(request.FileName, request.Arguments, request.StartFolder, false);

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
                Logger.Log(ex.ToString(), LogLevel.Error);
                if (connection.IsAlive)
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: {ex.ToString()}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);

                await connection.FlushAndCloseAll().ConfigureAwait(false);
                return;
            }
        }

    }
}
