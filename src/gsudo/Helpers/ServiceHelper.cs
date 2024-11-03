using gsudo.Rpc;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    class ServiceLocation
    {
        public string PipeName { get; set; }
        public bool IsHighIntegrity { get; set; }
    }

    internal static class ServiceHelper
    {
        internal static IRpcClient GetRpcClient()
        {
            // future Tcp implementations should be plugged here.
            return new NamedPipeClient();
        }

        /// <summary>
        /// Establishes a connection to a named pipe server.
        /// </summary>
        /// <param name="clientPid">Optional client process ID.</param>
        /// <returns>A <see cref="Connection"/> object representing the connected named pipe, or null if connection fails.</returns>
        public static async Task<ServiceLocation> WaitForNewService(int clientPid)
        {
            int timeoutMilliseconds = 10000;
            ServiceLocation service;

            string user = WindowsIdentity.GetCurrent().User.Value;
            do
            {
                service = FindServiceByIntegrity(clientPid, user);
                if (service != null)
                    return service;

                // Retry until service has started.
                await Task.Delay(50).ConfigureAwait(false);
                timeoutMilliseconds -= 50;
            }
            while (service == null && timeoutMilliseconds > 0);

            return service;
        }

        public static async Task<ServiceLocation> FindAnyServiceFast()
        {
            string user = WindowsIdentity.GetCurrent().User.Value;
            var callerProcessId = Process.GetCurrentProcess().Id;
            // Loop to search for a cache for the current process or its ancestors
            int maxIterations = 20; // To avoid potential PID tree loops where an ancestor process has the same PID. (gerardog/gsudo#155)
            while (callerProcessId > 0 && maxIterations-- > 0)
            {
                callerProcessId = ProcessHelper.GetParentProcessId(callerProcessId);

                var service = FindServiceByIntegrity(callerProcessId, user);
                if (service != null)
                    return service;
            }
            return null;
        }

        private static ServiceLocation FindServiceByIntegrity(int? clientPid, string user)
        {
            var anyIntegrity = InputArguments.UserName != null;
            var tryHighIntegrity = false || InputArguments.IntegrityLevel >= IntegrityLevel.High;
            var tryLowIntegrity = false || InputArguments.IntegrityLevel < IntegrityLevel.High;

            var targetUserSid = InputArguments.RunAsSystem ? "S-1-5-18" : InputArguments.UserSid;

            if (tryHighIntegrity)
            {
                var pipeName = NamedPipeClient.TryGetServicePipe(user, clientPid.Value, true, null);
                if (pipeName != null)
                {
                    return new ServiceLocation
                    {
                        PipeName = pipeName,
                        IsHighIntegrity = true
                    };
                }
            }

            if (tryLowIntegrity)
            {
                var pipeName = NamedPipeClient.TryGetServicePipe(user, clientPid.Value, false);
                if (pipeName != null)
                {
                    return new ServiceLocation
                    {
                        PipeName = pipeName,
                        IsHighIntegrity = false
                    };
                }
            }
            return null;
        }

        internal static async Task<Connection> Connect(ServiceLocation service)
        {
            IRpcClient rpcClient = GetRpcClient();

            try
            {
                return await rpcClient.Connect(service).ConfigureAwait(false);
            }
            catch (System.IO.IOException) { }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Warning);
            }

            return null;
        }

        internal static SafeProcessHandle StartService(int? allowedPid, TimeSpan? cacheDuration = null, string allowedSid = null, bool singleUse = false)
        {
            var currentSid = WindowsIdentity.GetCurrent().User.Value;

            allowedPid = allowedPid ?? Process.GetCurrentProcess().GetCacheableRootProcessId();
            allowedSid = allowedSid ?? Process.GetProcessById(allowedPid.Value)?.GetProcessUser()?.User?.Value ?? currentSid;

            string verb;
            SafeProcessHandle ret;

            Logger.Instance.Log($"Caller SID: {allowedSid}", LogLevel.Debug);

            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            if (!InputArguments.RunAsSystem && true) @params += $"-i {InputArguments.IntegrityLevel} ";
            if (InputArguments.RunAsSystem) @params += "-s ";
            if (InputArguments.TrustedInstaller) @params += "--ti ";
            if (InputArguments.UserName != null) @params += $"-u {InputArguments.UserName} ";

            verb = "gsudoservice";

            if (!cacheDuration.HasValue || singleUse)
            {
                if (!Settings.CacheMode.Value.In(CredentialsCache.CacheMode.Auto) || singleUse)
                {
                    verb = "gsudoelevate";
                    cacheDuration = TimeSpan.Zero;
                }
                else
                    cacheDuration = Settings.CacheDuration;
            }

            bool isAdmin = SecurityHelper.IsHighIntegrity();

            string commandLine = $"{@params}{verb} {allowedPid} {allowedSid} {Settings.LogLevel} {Settings.TimeSpanWithInfiniteToString(cacheDuration.Value)}";

            string ownExe = ProcessHelper.GetOwnExeName();
            if (InputArguments.TrustedInstaller && isAdmin && !WindowsIdentity.GetCurrent().Claims.Any(c => c.Value == Constants.TI_SID))
            {
                StartTrustedInstallerService(commandLine, allowedPid.Value);
                ret = null;
            }
            else if (InputArguments.RunAsSystem && isAdmin)
            {
                ret = ProcessFactory.StartAsSystem(ownExe, commandLine, Environment.CurrentDirectory, !InputArguments.Debug);
            }
            else if (InputArguments.UserName != null)
            {
                if (InputArguments.UserName != WindowsIdentity.GetCurrent().Name)
                {
                    var password = ConsoleHelper.ReadConsolePassword(InputArguments.UserName);
                    ret = ProcessFactory.StartWithCredentials(ownExe, commandLine, InputArguments.UserName, password).GetSafeProcessHandle();
                }
                else
                {
                    if (SecurityHelper.IsMemberOfLocalAdmins() && InputArguments.IntegrityLevel >= IntegrityLevel.High)
                        ret = ProcessFactory.StartElevatedDetached(ownExe, commandLine, !InputArguments.Debug).GetSafeProcessHandle();
                    else
                        ret = ProcessFactory.StartDetached(ownExe, commandLine, null, !InputArguments.Debug).GetSafeProcessHandle();
                }
            }
            else
            {
                ret = ProcessFactory.StartElevatedDetached(ownExe, commandLine, !InputArguments.Debug).GetSafeProcessHandle();
            }

            Logger.Instance.Log("Service process started.", LogLevel.Debug);
            return ret;
        }

        private static void StartTrustedInstallerService(string commandLine, int pid)
        {
            string name = $"gsudo TI Cache for PID {pid}";

            string args = $"/Create /ru \"NT SERVICE\\TrustedInstaller\" /TN \"{name}\" /TR \"\\\"{ProcessHelper.GetOwnExeName()}\\\" {commandLine}\" /sc ONCE /st 00:00 /f\"";
            Logger.Instance.Log($"Running: schtasks {args}", LogLevel.Debug);
            Process p;

            p = InputArguments.Debug
                ? ProcessFactory.StartAttached("schtasks", args)
                : ProcessFactory.StartRedirected("schtasks", args, null);

            p.WaitForExit();
            if (p.ExitCode != 0) throw new ApplicationException($"Error creating a scheduled task for TrustedInstaller: {p.ExitCode}");

            try
            {
                args = $"/run /I /TN \"{name}\"";
                p = InputArguments.Debug
                    ? ProcessFactory.StartAttached("schtasks", args)
                    : ProcessFactory.StartRedirected("schtasks", args, null);
                p.WaitForExit();
                if (p.ExitCode != 0) throw new ApplicationException($"Error starting scheduled task for TrustedInstaller: {p.ExitCode}");
            }
            finally
            {
                args = $"/delete /F /TN \"{name}\"";
                p = InputArguments.Debug
                    ? ProcessFactory.StartAttached("schtasks", args)
                    : ProcessFactory.StartRedirected("schtasks", args, null);
                p.WaitForExit();
            }
        }
    }
}

