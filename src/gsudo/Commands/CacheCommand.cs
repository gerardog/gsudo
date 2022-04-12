using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using gsudo.Enums;
using gsudo.Helpers;
using gsudo.Rpc;

namespace gsudo.Commands
{
    enum CacheCommandAction
    {
        On,
        Off,
        Help
    };

    class CacheCommand : ICommand
    {
        public CacheCommandAction? Action { get; set; }
        public int? AllowedPid { get; set; }
        public TimeSpan? CacheDuration { get; set; }

        public async Task<int> Execute()
        {
            if (!AllowedPid.HasValue)
            {
                AllowedPid = Process.GetCurrentProcess().GetCacheableRootProcessId();
            }

            if (!Action.HasValue || Action == CacheCommandAction.Help)
            {
                return await CacheHelp().ConfigureAwait(false);
            }
            else if (Action == CacheCommandAction.Off)
            {
                if (InputArguments.KillCache)
                    return await new KillCacheCommand(true).Execute().ConfigureAwait(false);
                if (CredentialsCacheLifetimeManager.ClearCredentialsCache(AllowedPid))
                    Logger.Instance.Log("Cache session closed.", LogLevel.Info);
                else
                {
                    Logger.Instance.Log(
                        "No cache session available for this process. (Use `-k' to close all sessions)`",
                        LogLevel.Info);
                }

                return 0;
            }
            else // CacheCommandAction.On
            {
                if (Settings.CacheMode.Value == CacheMode.Disabled ||
                    Math.Abs(Settings.CacheDuration.Value.TotalSeconds) < 1)
                {
                    Logger.Instance.Log("Unable to start a gsudo Cache session because CacheMode setting is 'Disabled' or CacheDuration is 0. Run `gsudo cache help` for more information.", LogLevel.Error);
                    return 1;
                }


                if (!ProcessHelper.IsAdministrator() && NamedPipeClient.IsServiceAvailable())
                {
                    var commandToRun = new List<string>();
                    commandToRun.Add($"\"{ProcessHelper.GetOwnExeName()}\"");
                    if (InputArguments.Debug) commandToRun.Add("--debug");

                    commandToRun.AddRange(new[]
                        {"cache", "on", "--pid", AllowedPid.ToString()});

                    InputArguments.Direct = true;
                    return await new RunCommand() {CommandToRun = commandToRun}
                        .Execute().ConfigureAwait(false);
                }
                else
                {
                    if (!ServiceHelper.StartElevatedService(AllowedPid.Value, CacheDuration ?? Settings.CacheDuration))
                    {
                        return Constants.GSUDO_ERROR_EXITCODE;
                    }
                    if (AllowedPid.Value != 0)
                        Logger.Instance.Log($"Elevation allowed for process Id {AllowedPid.Value} and children.",
                            LogLevel.Info);
                    else
                        Logger.Instance.Log($"Elevation allowed for any process from same-user.", LogLevel.Warning);

                    Logger.Instance.Log("Cache is a security risk. Use `gsudo cache off` (or `-k`) to go back to safety.",
                        LogLevel.Warning);
                }

                return 0;
            }
        }

        private Task<int> CacheHelp()
        {
            Console.ResetColor();
            Console.WriteLine($@"Cache Help:
-----------
An active credentials cache session, is a running elevated instance of gsudo that allows certain processes to elevate without a UAC pop-up.");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write("Important Warning: ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;

            Console.WriteLine(
                $@"If a malicious process is running as your user it could circumvent all gsudo protections and request elevation unnoticed. (e.g. inject a Dll into the whitelisted process to call gsudo)");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                "**Therefore: Consider that enabling the cache is equivalent to temporarily disabling windows UAC/UIPI protection.**");
            Console.ResetColor();

            Console.WriteLine($@"
Usage:
------
gsudo cache {{on | off}} [-p {{pid}}] [-d {{time}}]   Start/stop a gsudo cache session.
  -p | --pid {{pid}}            Specify which process can use the cache. (Use 0 for any, Default=caller pid)
  -d | --duration {{hh:mm:ss}}  Max time the cache can stay idle before closing. 
                              Use '-1' to keep open until logoff (or `cache off`, or `-k`).
                              Current idle duration is: {Settings.CacheDuration.GetStringValue()}

gsudo -k                       Stops all active cache sessions.
gsudo status                   Shows info regarding the user, elevation, and cache status
gsudo config CacheMode {{mode}}  Change the cache mode. 

Available Cache Modes:
  * Disabled: Every elevation shows a UAC popup. 
  * Explicit: (default) Every elevation shows a UAC popup, unless a cache session is started with `gsudo cache on`
  * Auto: Simil-unix-sudo. The first elevation shows a UAC Popup and starts a cache session automatically.
Current Cache Mode is: {Settings.CacheMode.ToString()}");

            return Task.FromResult(0);
        }
    }
}