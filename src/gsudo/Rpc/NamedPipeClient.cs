using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class NamedPipeClient : IRpcClient
    {
        public async Task<Connection> Connect(ElevationRequest elevationRequest, int? clientPid, int timeoutMilliseconds)
        {
            var localServer = true;
            var server = ".";

            string pipeName = null;
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            NamedPipeClientStream dataPipe = null;
            NamedPipeClientStream controlPipe = null;

            try
            {
                if (clientPid.HasValue)
                {
                    pipeName = NamedPipeServer.GetPipeName(user, clientPid.Value);
                    if (!System.IO.Directory.EnumerateFiles(@"\\.\pipe\", pipeName).Any() && timeoutMilliseconds <= 300)
                    {
                        // fail fast without timeout.
                        return null;
                    }
                }
                else if (localServer)
                {
                    var callerProcess = Process.GetCurrentProcess().ParentProcess();
                    while (callerProcess != null)
                    {
                        pipeName = NamedPipeServer.GetPipeName(user, callerProcess.Id);
                        // Does the pipe exists?
                        if (System.IO.Directory.EnumerateFiles(@"\\.\pipe\", pipeName).Any() && timeoutMilliseconds <= 300)
                            break;

                        // try grandfather.
                        callerProcess = callerProcess.ParentProcess();
                    }
                }

                if (pipeName == null) return null;

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