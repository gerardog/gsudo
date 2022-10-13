using gsudo.Rpc;
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

        internal static async Task<Connection> Connect(int? callingPid)
        {
            IRpcClient rpcClient = GetRpcClient();

            try
            {
                return await rpcClient.Connect(callingPid).ConfigureAwait(false);
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

        internal static void StartService(int? allowedPid, TimeSpan? cacheDuration = null, string allowedSid = null, bool singleUse = false)
        {
            allowedPid = allowedPid ?? Process.GetCurrentProcess().GetCacheableRootProcessId();
            allowedSid = allowedSid ?? Process.GetProcessById(allowedPid.Value).GetProcessUser().User.Value;

            string verb;

            Logger.Instance.Log($"Caller SID: {allowedSid}", LogLevel.Debug);

            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            //            if (InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
            if (InputArguments.RunAsSystem && allowedSid != System.Security.Principal.WindowsIdentity.GetCurrent().User.Value) @params += "-s ";
            if (InputArguments.TrustedInstaller) @params += "--ti ";
            if (!string.IsNullOrEmpty(InputArguments.UserName)) @params += $"-u {InputArguments.UserName} ";

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

            bool isAdmin = ProcessHelper.IsHighIntegrity();

            string commandLine = $"{@params}{verb} {allowedPid} {allowedSid} {Settings.LogLevel} {Settings.TimeSpanWithInfiniteToString(cacheDuration.Value)}";

            string ownExe = ProcessHelper.GetOwnExeName();
            if (InputArguments.TrustedInstaller && isAdmin && !WindowsIdentity.GetCurrent().Claims.Any(c => c.Value == Constants.TI_SID))
            {
                StartTrustedInstallerService(commandLine, allowedPid.Value);
            }
            else if (InputArguments.RunAsSystem && isAdmin)
            {
                ProcessFactory.StartAsSystem(ownExe, commandLine, Environment.CurrentDirectory, !InputArguments.Debug).Close();
            }
            else if (InputArguments.UserName != null)
            {
                if (isAdmin)
                {
                    var password = ConsoleHelper.ReadConsolePassword();
                    ProcessFactory.StartWithCredentials(ownExe, commandLine, InputArguments.UserName, password);
                }
                else if(ProcessHelper.IsMemberOfLocalAdmins())
                {
                    // First elevate in place, then 
                    // Call: gsudo gsudo -u UserName MyCommand
                    ProcessFactory.StartAttached(ownExe, $"-d {ArgumentsHelper.Quote(ownExe)} {commandLine}");
                }
            }
            else
            {
                _ = ProcessFactory.StartElevatedDetached(ownExe, commandLine, !InputArguments.Debug);
            }

            Logger.Instance.Log("Service process started.", LogLevel.Debug);
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
