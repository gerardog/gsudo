using System;
using System.Diagnostics;
using System.Threading.Tasks;
using gsudo.Enums;
using gsudo.Helpers;

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

        public Task<int> Execute()
        {
            if (!Action.HasValue || Action == CacheCommandAction.Help)
            {
                return CacheHelp();
            }
            else if (Action == CacheCommandAction.Off)
            {
                return new KillCacheCommand().Execute();
            }
            else // CacheCommandAction.On
            {
                if (Settings.CacheMode.Value == CacheMode.Disabled || Math.Abs(Settings.CacheDuration.Value.TotalSeconds) < 1)
                    throw new ApplicationException(
                        "Unable to start a gsudo Cache session because CacheMode setting is 'Disabled' or CacheDuration is 0. Run `gsudo cache help` for more information.");

                if (!AllowedPid.HasValue)
                    AllowedPid = Process.GetCurrentProcess().GetParentProcessExcludingShim().Id;

                if (!RunCommand.StartElevatedService(AllowedPid.Value, CacheDuration))
                {
                    return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
                }

                if (AllowedPid.Value != 0)
                    Logger.Instance.Log($"Elevation allowed for process Id {AllowedPid.Value} and children.",
                        LogLevel.Info);
                else
                    Logger.Instance.Log($"Elevation allowed for any process from same-user.", LogLevel.Warning);

                Logger.Instance.Log("Cache is a security risk. Use `gsudo cache off` (or `-k`) to go back to safety.",
                    LogLevel.Warning);
                return Task.FromResult(0);
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
gsudo cache on [-p {{pid}}] [-d {{time}}]  Starts a gsudo cache session.
  -p | --pid {{pid}}            Specify which process can use the cache. (Use 0 for any, Default=caller pid)
  -d | --duration {{hh:mm:ss}}  Max time the cache can stay idle before closing. 
                              Use '-1' to keep open until logoff, or `cache off`, or `-k`.

gsudo cache off                Stops all cache sessions. (Same as `gsudo -k`)
gsudo status                   Shows info regarding the user, elevation, and cache status
gsudo config CacheMode {{mode}}  Change the cache mode. Current Cache Mode is: {Settings.CacheMode.ToString()}

Available Cache Modes:
  * Disabled: Every elevation request shows a UAC popup. 
  * Explicit: (default) Every elevation shows a UAC popup, unless a cache session is started with `gsudo cache on`
  * Auto: Simil-unix-sudo. The first elevation shows a UAC Popup and starts a cache session automatically.");

            return Task.FromResult(0);
        }
    }
}