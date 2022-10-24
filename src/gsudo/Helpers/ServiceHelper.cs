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
    internal static class ServiceHelper
    {
        internal static IRpcClient GetRpcClient()
        {
            // future Tcp implementations should be plugged here.
            return new NamedPipeClient();
        }

        internal static async Task<Connection> Connect(int? callingPid = null, SafeProcessHandle serviceHandle = null)
        {
            IRpcClient rpcClient = GetRpcClient();

            try
            {
                return await rpcClient.Connect(callingPid, serviceHandle).ConfigureAwait(false);
            }
            catch (System.IO.IOException) { }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                if (callingPid.HasValue)
                    Logger.Instance.Log(ex.ToString(), LogLevel.Warning);
            }

            return null;
        }

        internal static SafeProcessHandle StartService(int? allowedPid, TimeSpan? cacheDuration = null, string allowedSid = null, bool singleUse = false)
        {
            var currentSid = WindowsIdentity.GetCurrent().User.Value;

            allowedPid = allowedPid ?? Process.GetCurrentProcess().GetCacheableRootProcessId();
            allowedSid = allowedSid ?? Process.GetProcessById(allowedPid.Value)?.GetProcessUser()?.User.Value ?? currentSid;

            string verb;
            SafeProcessHandle ret;

            Logger.Instance.Log($"Caller SID: {allowedSid}", LogLevel.Debug);

            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            if (!InputArguments.RunAsSystem && InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
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
                    if (SecurityHelper.IsMemberOfLocalAdmins() && InputArguments.GetIntegrityLevel() >= IntegrityLevel.High)
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
