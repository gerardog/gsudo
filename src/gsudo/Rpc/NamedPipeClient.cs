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
        /// <summary>
        /// Establishes a connection to a named pipe server.
        /// </summary>
        /// <param name="clientPid">Optional client process ID.</param>
        /// <returns>A <see cref="Connection"/> object representing the connected named pipe, or null if connection fails.</returns>
        public async Task<Connection> Connect(int? clientPid)
        {
            int timeoutMilliseconds = 10000;
            var server = ".";

            string pipeName = null;
            bool isHighIntegrity;
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            NamedPipeClientStream dataPipe = null;
            NamedPipeClientStream controlPipe = null;

            try
            {
                if (clientPid.HasValue)
                {
                    do
                    {
                        pipeName = FindServicePipeName(user, clientPid.Value, out isHighIntegrity);

                        if (pipeName == null)
                        {
                            await Task.Delay(50).ConfigureAwait(false);
                            timeoutMilliseconds -= 50;
                        }
                    }
                    while (pipeName == null && timeoutMilliseconds > 0);
                 }
                else
                {
                    isHighIntegrity = false;
                    var callerProcessId = Process.GetCurrentProcess().Id;
                    int maxRecursion = 20;
                    while (callerProcessId > 0 && maxRecursion-- > 0)
                    {
                        callerProcessId = ProcessHelper.GetParentProcessId(callerProcessId);

                        // Search for Credentials Cache
                        pipeName = FindServicePipeName(user, callerProcessId, out isHighIntegrity);

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

                var conn = new Connection(controlPipe, dataPipe, isHighIntegrity);
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
        /// Finds the elevated service pipe based on the security identifier (SID) and process ID (PID).
        /// </summary>
        /// <param name="allowedSid">The SID of the requesting user.</param>
        /// <param name="allowedPid">The PID of the requesting process.</param>
        /// <param name="isHighIntegrity">Output parameter indicating whether the pipe is high integrity.</param>
        /// <param name="targetUserSid">Optional SID that the new process will impersonate.</param>
        /// <returns>The name of the pipe if found, otherwise null.</returns>
        public static string FindServicePipeName(string allowedSid, int allowedPid, out bool isHighIntegrity, string targetUserSid = null)
        {
            targetUserSid = targetUserSid ?? InputArguments.UserSid;
            string pipeName;

            if (!InputArguments.IntegrityLevel.HasValue || InputArguments.IntegrityLevel.Value >= IntegrityLevel.High)
            {
                pipeName = NamedPipeNameFactory.GetPipeName(allowedSid, allowedPid, targetUserSid, true);
                if (NamedPipeUtils.ExistsNamedPipe(pipeName))
                {
                    isHighIntegrity = true;
                    InputArguments.IntegrityLevel = InputArguments.IntegrityLevel ?? IntegrityLevel.High;
                    return pipeName;
                }
            }

            if (!InputArguments.IntegrityLevel.HasValue || InputArguments.IntegrityLevel.Value < IntegrityLevel.High)
            {
                pipeName = NamedPipeNameFactory.GetPipeName(allowedSid, allowedPid, targetUserSid, false);
                if (NamedPipeUtils.ExistsNamedPipe(pipeName))
                {
                    isHighIntegrity = false;
                    InputArguments.IntegrityLevel = InputArguments.IntegrityLevel ?? IntegrityLevel.Low;
                    return pipeName;
                }
            }

            isHighIntegrity = false;
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
            if (NamedPipeClient.FindServicePipeName(allowedSid, 0, out _, targetSid) != null)
                return true;

            // Loop to search for a cache for the current process or its ancestors
            int maxIterations = 20; // To avoid potential PID tree loops where an ancestor process has the same PID. (gerardog/gsudo#155)
            while (allowedPid.Value > 0 && maxIterations-- > 0)
            {
                pipeName = NamedPipeClient.FindServicePipeName(allowedSid, allowedPid.Value, out _, targetSid);
                if (pipeName != null)
                    break;

                allowedPid = ProcessHelper.GetParentProcessId(allowedPid.Value);
            }

            return pipeName != null;
        }
    }
}