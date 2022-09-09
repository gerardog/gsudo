using gsudo.Rpc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    internal static class ServiceHelper
    {
        internal static bool IsServiceAvailable()
        {
            return NamedPipeClient.IsServiceAvailable();
        }

        internal static IRpcClient GetRpcClient()
        {
            // future Tcp implementations should be plugged here.
            return new NamedPipeClient();
        }

        internal static async Task<Connection> ConnectStartElevatedService()
        {
            var callingPid = ProcessHelper.GetCallerPid();

            Logger.Instance.Log($"Caller PID: {callingPid}", LogLevel.Debug);

            if (InputArguments.IntegrityLevel.HasValue && InputArguments.IntegrityLevel.Value == IntegrityLevel.System && !InputArguments.RunAsSystem)
            {
                Logger.Instance.Log($"Elevating as System because of IntegrityLevel=System parameter.", LogLevel.Warning);
                InputArguments.RunAsSystem = true;
            }

            IRpcClient rpcClient = GetRpcClient();

            Connection connection = null;
            try
            {
                connection = await rpcClient.Connect(null, true).ConfigureAwait(false);
            }
            catch (System.IO.IOException) { }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Warning);
            }

            if (connection == null) // service is not running or listening.
            {
                if (!StartElevatedService(callingPid, singleUse: InputArguments.KillCache))
                    return null;

                connection = await rpcClient.Connect(callingPid, false).ConfigureAwait(false);
            }
            return connection;
        }

        internal static bool StartElevatedService(int? allowedPid, TimeSpan? cacheDuration = null, string allowedSid=null, bool singleUse = false)
        {
            var callingSid = allowedSid ?? System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            var callingPid = allowedPid ?? Process.GetCurrentProcess().GetCacheableRootProcessId();
            string verb;

            Logger.Instance.Log($"Caller SID: {callingSid}", LogLevel.Debug);

            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            //            if (InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
            if (InputArguments.RunAsSystem && allowedSid != System.Security.Principal.WindowsIdentity.GetCurrent().User.Value) @params += "-s ";
            if (InputArguments.TrustedInstaller) @params += "--ti ";

            verb = "gsudoservice";

            if (!cacheDuration.HasValue || singleUse)
            {
                if (!Settings.CacheMode.Value.In(Enums.CacheMode.Auto) || singleUse)
                {
                    verb = "gsudoelevate";
                    cacheDuration = TimeSpan.Zero;
                }
                else
                    cacheDuration = Settings.CacheDuration;
            }

            bool isAdmin = ProcessHelper.IsHighIntegrity();

            string commandLine = $"{@params}{verb} {callingPid} {callingSid} {Settings.LogLevel} {Settings.TimeSpanWithInfiniteToString(cacheDuration.Value)}";

            bool success = false;

            try
            {
                string ownExe = ProcessHelper.GetOwnExeName();
                if (InputArguments.TrustedInstaller && isAdmin && !WindowsIdentity.GetCurrent().Claims.Any(c => c.Value == Constants.TI_SID))
                {
                    return StartTrustedInstallerService(commandLine, callingPid);
                }
                else if (InputArguments.RunAsSystem && isAdmin)
                {
                    success = null != ProcessFactory.StartAsSystem(ownExe, commandLine, Environment.CurrentDirectory, !InputArguments.Debug);
                }
                else
                {
                    success = null != ProcessFactory.StartElevatedDetached(ownExe, commandLine, !InputArguments.Debug);
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.Instance.Log(ex.Message, LogLevel.Error);
                return false;
            }

            if (!success)
            {
                Logger.Instance.Log("Failed to start elevated instance.", LogLevel.Error);
                return false;
            }

            Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
            return true;
        }

        private static bool StartTrustedInstallerService(string commandLine, int pid)
        {
            string name = $"gsudo TI Cache for PID {pid}";

            string args = $"/Create /ru \"NT SERVICE\\TrustedInstaller\" /TN \"{name}\" /TR \"\\\"{ProcessHelper.GetOwnExeName()}\\\" {commandLine}\" /sc ONCE /st 00:00 /f\"";
            Logger.Instance.Log($"Running: schtasks {args}", LogLevel.Debug);
            Process p;

            p = InputArguments.Debug
                ? ProcessFactory.StartAttached("schtasks", args)
                : ProcessFactory.StartRedirected("schtasks", args, null);

            p.WaitForExit();
            if (p.ExitCode != 0) return false;

            try
            {
                args = $"/run /I /TN \"{name}\"";
                p = InputArguments.Debug
                    ? ProcessFactory.StartAttached("schtasks", args)
                    : ProcessFactory.StartRedirected("schtasks", args, null);
                p.WaitForExit();
                if (p.ExitCode != 0) return false;
            }
            finally
            {
                args = $"/delete /F /TN \"{name}\"";
                p = InputArguments.Debug
                    ? ProcessFactory.StartAttached("schtasks", args)
                    : ProcessFactory.StartRedirected("schtasks", args, null);
                p.WaitForExit();
            }

            return true;
        }
    }
}
