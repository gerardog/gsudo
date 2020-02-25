using gsudo.Helpers;
using System;
using System.Globalization;
using System.Threading.Tasks;
using static gsudo.Native.ConsoleApi;

namespace gsudo.Commands
{
    class CtrlCCommand : ICommand
    {
        public int Pid { get; set; }
        public bool sendSigBreak { get; set; }

        public Task<int> Execute()
        {            
            FreeConsole();

            if (AttachConsole(Pid))
            {
                if (sendSigBreak)
                    GenerateConsoleCtrlEvent(CtrlTypes.CTRL_BREAK_EVENT, 0);
                else
                    GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);

                FreeConsole();
            }
            else
            {
                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }

            return Task.FromResult(0);
        }


        public static void Invoke(int procId, bool sendSigBreak = false)
        {
            // Sending Ctrl-C in windows is tricky.
            // Your process must be attached to the target process console.
            // There is no way to do that without loosing your currently attached console, and that generates a lot of issues.
            // So the best we can do is create a new process that will attach and send Ctrl-C to the target process.

            using (var p = ProcessFactory.StartDetached
                (ProcessHelper.GetOwnExeName(), $"gsudoctrlc {procId.ToString(CultureInfo.InvariantCulture)} {sendSigBreak}", Environment.CurrentDirectory, true))
            {
                p.WaitForExit();
            }
        }

    }
}
