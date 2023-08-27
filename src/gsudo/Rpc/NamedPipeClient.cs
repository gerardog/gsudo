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
        public async Task<Connection> Connect(ServiceLocation service)
        {
            var server = ".";
            NamedPipeClientStream dataPipe = null;
            NamedPipeClientStream controlPipe = null;
            var timeoutMilliseconds = 10000;

            try
            { 
                dataPipe = new NamedPipeClientStream(server, service.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification, HandleInheritability.None);
                await dataPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                controlPipe = new NamedPipeClientStream(server, service.PipeName + "_control", PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification, HandleInheritability.None);
                await controlPipe.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);

                Logger.Instance.Log($"Connected via Named Pipe {service.PipeName}.", LogLevel.Debug);

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

        /// <summary>
        /// Checks if a service pipe exists based on the security identifier (SID) and process ID (PID).
        /// </summary>
        /// <param name="allowedSid">The SID of the requesting user.</param>
        /// <param name="allowedPid">The PID of the requesting process.</param>
        /// <param name="isHighIntegrity">Indicating whether the pipe is high integrity.</param>
        /// <param name="targetUserSid">Optional SID that the new process will impersonate.</param>
        /// <returns>The name of the pipe if found, otherwise null.</returns>
        public static string TryGetServicePipe(string allowedSid, int allowedPid, bool isHighIntegrity, string targetUserSid = null)
        {
            targetUserSid = targetUserSid ?? InputArguments.UserSid;
            string pipeName;

            pipeName = NamedPipeNameFactory.GetPipeName(allowedSid, allowedPid, targetUserSid, isHighIntegrity);
            if (NamedPipeUtils.ExistsNamedPipe(pipeName))
            {
                return pipeName;
            }
            return null;
        }

        /// <summary>
        /// Checks if a service is available for default elevation or the optional specified PID and SID.
        /// </summary>
        /// <param name="allowedPid">Optional requester PID that needs a service.</param>
        /// <param name="allowedSid">Optional requester SID that needs a service.</param>
        /// <param name="targetSid">Optional SID that the new process will impersonate.</param>
        /// <returns>True if a cache service is available, otherwise false.</returns>
        public static bool IsServiceAvailable(int? allowedPid = null, string allowedSid = null, string targetSid = null)
        {
            string pipeName = null;

            allowedPid = allowedPid ?? ProcessHelper.GetParentProcessId(Process.GetCurrentProcess().Id);
            allowedSid = allowedSid ?? System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            targetSid = targetSid ?? InputArguments.UserSid;

            // Try cache for any process
            if (NamedPipeClient.TryGetServicePipe(allowedSid, 0, true, targetSid) != null)
                return true;
            //if (NamedPipeClient.TryGetServicePipe(allowedSid, 0, false, targetSid) != null)
            //    return true;

            // Loop to search for a cache for the current process or its ancestors
            int maxIterations = 20; // To avoid potential PID tree loops where an ancestor process has the same PID. (gerardog/gsudo#155)
            while (allowedPid.Value > 0 && maxIterations-- > 0)
            {
                if (NamedPipeClient.TryGetServicePipe(allowedSid, allowedPid.Value, true, targetSid) != null)
                    return true;
                //if (NamedPipeClient.TryGetServicePipe(allowedSid, allowedPid.Value, false, targetSid) != null)
                //    return true;

                allowedPid = ProcessHelper.GetParentProcessId(allowedPid.Value);
            }

            return pipeName != null;
        }
    }
}