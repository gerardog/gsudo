using gsudo.Helpers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class NamedPipeClient : IRpcClient
    {
        public async Task<Connection> Connect(int? clientPid, bool failFast)
        {
            int timeoutMilliseconds = failFast ? 300 : 5000;
            var server = ".";

            string pipeName = null;
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            NamedPipeClientStream dataPipe = null;
            NamedPipeClientStream controlPipe = null;

            try
            {
                if (clientPid.HasValue)
                {
                    pipeName = NamedPipeNameFactory.GetPipeName(user, clientPid.Value);
                    if (!NamedPipeUtils.ExistsNamedPipe(pipeName) && failFast)
                    {
                        // fail fast without timeout.
                        return null;
                    }
                }
                else
                {
                    var callerProcessId = Process.GetCurrentProcess().Id;
                    while (callerProcessId > 0)
                    {
                        callerProcessId = ProcessHelper.GetParentProcessId(callerProcessId);
                        pipeName = NamedPipeNameFactory.GetPipeName(user, callerProcessId);
                        // Does the pipe exists?
                        if (NamedPipeUtils.ExistsNamedPipe(pipeName))
                            break;

                        pipeName = null;
                        // try grandfather.
                    }
                }

                if (pipeName == null) return null;

                dataPipe = new NamedPipeClientStream(server, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification, HandleInheritability.None);
                await dataPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                controlPipe = new NamedPipeClientStream(server, pipeName + "_control", PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification, HandleInheritability.None);
                await controlPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                Logger.Instance.Log($"Connected via Named Pipe {pipeName}.", LogLevel.Debug);

                var conn = new Connection()
                {
                    ControlStream = controlPipe,
                    DataStream = dataPipe,
                };

                return conn;
            }
            catch (System.TimeoutException)
            {
                dataPipe?.Dispose();
                controlPipe?.Dispose();
                return null;
            }
            catch
            {
                dataPipe?.Dispose();
                controlPipe?.Dispose();
                throw;
            }
        }

        public static bool IsServiceAvailable()
        {
            string pipeName = null;
            var callerProcessId = Process.GetCurrentProcess().Id;
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;

            while (callerProcessId > 0)
            {
                callerProcessId = ProcessHelper.GetParentProcessId(callerProcessId);
                pipeName = NamedPipeNameFactory.GetPipeName(user, callerProcessId);
                // Does the pipe exists?
                if (NamedPipeUtils.ExistsNamedPipe(pipeName))
                    break;

                pipeName = null;
                // try grandfather.
            }

            return pipeName != null ;
        }
    }
}