using System.Threading.Tasks;
using System.Threading;
using static gsudo.Native.ConsoleApi;

namespace gsudo.Commands
{
    class CtrlCCommand : ICommand
    {
        public int pid { get; set; }

        Timer ShutdownTimer;
        void EnableTimer() => ShutdownTimer.Change((int)GlobalSettings.CredentialsCacheDuration.Value.TotalMilliseconds, Timeout.Infinite);
        void DisableTimer() => ShutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);

        public async Task<int> Execute()
        {            
            FreeConsole();

            if (AttachConsole((uint)pid))
            {
                GenerateConsoleCtrlEvent(Native.ConsoleApi.CtrlTypes.CTRL_C_EVENT, 0);
                FreeConsole();
            }
            else
            {
                return Constants.GSUDO_ERROR_EXITCODE;
            }

            return 0;
        }
    }
}
