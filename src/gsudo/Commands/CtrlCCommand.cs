using gsudo.Helpers;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Win32;
using static gsudo.Native.ConsoleApi;

namespace gsudo.Commands
{
    // Required for sending Ctrl-C / Ctrl-Break to the elevated process on VT & piped Mode.
    class CtrlCCommand : ICommand
    {
        public uint Pid { get; set; }

        public bool SendSigBreak { get; set; }

        public Task<int> Execute()
        {            
            PInvoke.FreeConsole();

            if (PInvoke.AttachConsole(Pid))
            {
                if (SendSigBreak)
                    PInvoke.GenerateConsoleCtrlEvent((uint)CtrlTypes.CTRL_BREAK_EVENT, 0);
                else
                    PInvoke.GenerateConsoleCtrlEvent((uint)CtrlTypes.CTRL_C_EVENT, 0);

                PInvoke.FreeConsole();
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
