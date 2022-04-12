using gsudo.Rpc;
using System;
using System.Diagnostics;
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
                if (!StartElevatedService(callingPid))
                    return null;

                connection = await rpcClient.Connect(callingPid, false).ConfigureAwait(false);
            }
            return connection;
        }

        internal static bool StartElevatedService(int? allowedPid, TimeSpan? cacheDuration = null)
        {
            var callingSid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            var callingPid = allowedPid ?? Process.GetCurrentProcess().GetCacheableRootProcessId();
            string verb;

            Logger.Instance.Log($"Caller SID: {callingSid}", LogLevel.Debug);

            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            //            if (InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
            //            if (InputArguments.RunAsSystem) @params += "-s ";

            verb = "gsudoservice";

            if (!cacheDuration.HasValue)
            {
                if (!Settings.CacheMode.Value.In(Enums.CacheMode.Auto))
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
                if (InputArguments.RunAsSystem && isAdmin)
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

        internal static bool StartSingleUseElevatedService(int callingPid)
        {
            var @params = string.Empty;

            if (InputArguments.Debug) @params = "--debug ";
            if (InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
            if (InputArguments.RunAsSystem) @params += "-s ";

            bool isAdmin = ProcessHelper.IsHighIntegrity();
            string ownExe = ProcessHelper.GetOwnExeName();

            string commandLine;
            commandLine = $"{@params}gsudoelevate --pid {callingPid}";

            Process p;

            try
            {
                p = ProcessFactory.StartElevatedDetached(ownExe, commandLine, !InputArguments.Debug);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.Instance.Log(ex.Message, LogLevel.Error);
                return false;
            }

            if (p == null)
            {
                Logger.Instance.Log("Failed to start elevated instance.", LogLevel.Error);
                return false;
            }

            Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);

            p.WaitForExit();

            if (p.ExitCode == 0)
            {
                return true;
            }

            return false;
        }

    }
}
