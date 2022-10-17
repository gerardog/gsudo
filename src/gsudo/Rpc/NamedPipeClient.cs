using gsudo.Helpers;
using gsudo.Native;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace gsudo.Rpc
{
    class NamedPipeClient : IRpcClient
    {
        public async Task<Connection> Connect(int? clientPid, SafeProcessHandle serviceProcessHandle)
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
                    int retryLefts = 3;
                    do
                    {
                        if (ProcessApi.WaitForSingleObject(serviceProcessHandle.DangerousGetHandle(), 1) == 0) // original service process is dead, but may have started an elevated service that we don't have handle.
                            retryLefts--;

                        pipeName = FindService(user, clientPid.Value);

                        if (pipeName == null)
                            await Task.Delay(50).ConfigureAwait(false);
                    }
                    while (pipeName == null && retryLefts>0);

                    timeoutMilliseconds = 5000; // service just started. Larger Timeout 
                }
                else
                {
                    timeoutMilliseconds = 300;
                    var callerProcessId = Process.GetCurrentProcess().Id;
                    int maxRecursion = 20;
                    while (callerProcessId > 0 && maxRecursion-- > 0)
                    {
                        callerProcessId = ProcessHelper.GetParentProcessId(callerProcessId);

                        // Search for Credentials Cache

                        //Try Admin
                        pipeName = FindService(user, callerProcessId);

                        if (pipeName!=null)
                            break;
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

        public static string FindService(string user, int clientPid, string userSid = null)
        {
            userSid = userSid ?? InputArguments.UserSid;
            string pipeName;

            if (!InputArguments.IntegrityLevel.HasValue || InputArguments.IntegrityLevel.Value >= IntegrityLevel.High)
            {
                pipeName = NamedPipeNameFactory.GetPipeName(user, clientPid, userSid, true);
                if (NamedPipeUtils.ExistsNamedPipe(pipeName))
                    return pipeName;
            }

            if (!InputArguments.IntegrityLevel.HasValue || InputArguments.IntegrityLevel.Value < IntegrityLevel.High)
            {
                pipeName = NamedPipeNameFactory.GetPipeName(user, clientPid, userSid, false);
                if (NamedPipeUtils.ExistsNamedPipe(pipeName))
                    return pipeName;
            }

            return null;                
        }

        public static bool IsServiceAvailable(int? allowedPid = null, string allowedSid = null, string targetSid = null)
        {
            string pipeName = null;

            allowedPid = allowedPid ?? ProcessHelper.GetParentProcessId(Process.GetCurrentProcess().Id);
            allowedSid = allowedSid ?? System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            targetSid = targetSid ?? InputArguments.UserSid ?? allowedSid;

            int maxIterations = 20;
            while (allowedPid.Value > 0 && maxIterations-- > 0)
            {
                pipeName = NamedPipeClient.FindService(allowedSid, allowedPid.Value, targetSid);
                if (pipeName != null)
                    break;

                allowedPid = ProcessHelper.GetParentProcessId(allowedPid.Value);
                // try grandfather.
            }

            return pipeName != null;
        }
    }
}