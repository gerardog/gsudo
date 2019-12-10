using CommandLine;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using gsudo.Rpc;
using gsudo.ProcessHosts;
using static gsudo.Helpers.ProcessExtensions;
using static gsudo.Native.ConsoleApi;

namespace gsudo.Commands
{
    [Verb("gsudoctrlc")]
    class CtrlCCommand : ICommand
    {
        [Value(0)]
        public int pid { get; set; }

        [Value(1)]
        public LogLevel? LogLvl { get; set; }

        Timer ShutdownTimer;
        void EnableTimer() => ShutdownTimer.Change((int)GlobalSettings.CredentialsCacheDuration.Value.TotalMilliseconds, Timeout.Infinite);
        void DisableTimer() => ShutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);

        public async Task<int> Execute()
        {
            // service mode
            if (LogLvl.HasValue) GlobalSettings.LogLevel.Value = LogLvl.Value;

            Logger.Instance.Log("Service started", LogLevel.Info);

            FreeConsole();

            if (AttachConsole((uint)pid))
            {
                ConsoleCancelEventHandler dele = null;

                Console.CancelKeyPress += dele;

                GenerateConsoleCtrlEvent(Helpers.ProcessExtensions.CtrlTypes.CTRL_C_EVENT, 0);
                FreeConsole();
            }
            else
            {
                Logger.Instance.Log($"AttachConsole failed", LogLevel.Error);
                return Constants.GSUDO_ERROR_EXITCODE;
            }

            return 0;
        }
    }
}
