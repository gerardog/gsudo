using System.Threading.Tasks;
using static gsudo.Native.ConsoleApi;

namespace gsudo.Commands
{
    class CtrlCCommand : ICommand
    {
        public int pid { get; set; }

        public Task<int> Execute()
        {            
            FreeConsole();

            if (AttachConsole((uint)pid))
            {
                GenerateConsoleCtrlEvent(Native.ConsoleApi.CtrlTypes.CTRL_C_EVENT, 0);
                FreeConsole();
            }
            else
            {
                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }

            return Task.FromResult(0);
        }
    }
}
