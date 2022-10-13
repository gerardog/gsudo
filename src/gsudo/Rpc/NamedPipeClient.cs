using gsudo.Helpers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class NamedPipeClient : IRpcClient
    {
        public async Task<Connection> Connect(int? clientPid)
        {
            int timeoutMilliseconds;
            var server = ".";

            string pipeName = null;
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            NamedPipeClientStream dataPipe = null;
            NamedPipeClientStream controlPipe = null;

            try
            {
                if (clientPid.HasValue)
                {
                    timeoutMilliseconds = 5000; // service just started. Larger Timeout 
                    pipeName = NamedPipeNameFactory.GetPipeName(user, clientPid.Value, InputArguments.UserSid);
                }
                else
                {
                    timeoutMilliseconds = 300;
                    var callerProcessId = Process.GetCurrentProcess().Id;
                    int maxRecursion = 20;
                    while (callerProcessId > 0 && maxRecursion-- > 0)
                    {
                        callerProcessId = ProcessHelper.GetParentProcessId(callerProcessId);
                        pipeName = NamedPipeNameFactory.GetPipeName(user, callerProcessId, InputArguments.UserSid);
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

                var conn = new Connection(controlPipe, dataPipe);
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

        public static bool IsServiceAvailable(int? callerPid = null, string callerSid = null, string targetSid = null)
        {
            string pipeName = null;

            callerPid = callerPid ?? ProcessHelper.GetParentProcessId(Process.GetCurrentProcess().Id);
            callerSid = callerSid ?? System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            targetSid = targetSid ?? InputArguments.UserSid ?? callerSid;

            int maxIterations = 20;
            while (callerPid.Value > 0 && maxIterations-- > 0)
            {
                pipeName = NamedPipeNameFactory.GetPipeName(callerSid, callerPid.Value, targetSid);
                // Does the pipe exists?
                if (NamedPipeUtils.ExistsNamedPipe(pipeName))
                    break;

                callerPid = ProcessHelper.GetParentProcessId(callerPid.Value);
                pipeName = null;
                // try grandfather.
            }

            return pipeName != null;
        }
    }
}