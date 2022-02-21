using gsudo.Rpc;
using System;
using System.Diagnostics;

namespace gsudo.Helpers
{
    internal static class ServiceHelper
    {
        internal static bool IsServiceAvailable()
        {
            return NamedPipeClient.IsServiceAvailable();
        }

        internal static bool StartElevatedService(int? allowedPid, TimeSpan? cacheDuration)
        {
            var callingSid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            var callingPid = allowedPid ?? ProcessHelper.GetCallerPid();

            Logger.Instance.Log($"Caller SID: {callingSid}", LogLevel.Debug);

            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            //            if (InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
            //            if (InputArguments.RunAsSystem) @params += "-s ";
            if (!cacheDuration.HasValue) cacheDuration = Settings.CacheDuration;

            bool isAdmin = ProcessHelper.IsHighIntegrity();

            string commandLine = $"{@params}gsudoservice {callingPid} {callingSid} {Settings.LogLevel} {Settings.TimeSpanWithInfiniteToString(cacheDuration.Value)}";

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
