using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class NamedPipeClient : IRpcClient
    {
        public async Task<Connection> Connect(ElevationRequest elevationRequest, int timeoutMilliseconds)
        {
            var pipeName = NamedPipeServer.GetPipeName();

            var server = ".";

            NamedPipeClientStream dataPipe = null;
            NamedPipeClientStream controlPipe = null;

            try
            {
                // Does the pipe exists?
                if (server == "." && !System.IO.Directory.EnumerateFiles(@"\\.\pipe\", pipeName).Any() && timeoutMilliseconds <= 300)
                {
                    // fail fast without timeout.
                    return null;
                }

                dataPipe = new NamedPipeClientStream(server, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, HandleInheritability.None);
                await dataPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                controlPipe = new NamedPipeClientStream(server, pipeName + "_control", PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation, HandleInheritability.None);
                await controlPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                Logger.Instance.Log($"Connected via Named Pipe {pipeName}.", LogLevel.Debug);

                var conn = new Connection()
                {
                    ControlStream = controlPipe,
                    DataStream = dataPipe,
                };

                return conn;
            }
            catch
            {
                dataPipe?.Dispose();
                controlPipe?.Dispose();
                throw;
            }
        }
    }
}