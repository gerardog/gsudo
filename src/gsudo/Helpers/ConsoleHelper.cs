using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static gsudo.Native.ConsoleApi;

namespace gsudo.Helpers
{
    class ConsoleHelper
    { 
        public static bool EnableVT()
        {
            // If we are using ConEmu or Cmder, we can safely use their VT implementation.
            // which is much more stable than Windows 10, at least before 21H1.
            if (TerminalHelper.TerminalHasBuiltInVTSupport()) return true;

            var hStdOut = Native.ConsoleApi.GetStdHandle(Native.ConsoleApi.STD_OUTPUT_HANDLE);
            if (!Native.ConsoleApi.GetConsoleMode(hStdOut, out uint outConsoleMode))
            {
                Logger.Instance.Log("Could not get console mode", LogLevel.Debug);
                return false;
            }

            outConsoleMode |= Native.ConsoleApi.ENABLE_VIRTUAL_TERMINAL_PROCESSING;// | Native.ConsoleApi.DISABLE_NEWLINE_AUTO_RETURN;
            if (!Native.ConsoleApi.SetConsoleMode(hStdOut, outConsoleMode))
            {
                Logger.Instance.Log("Could not enable virtual terminal processing", LogLevel.Error);
                return false;
            }

            Logger.Instance.Log("Console VT mode enabled.", LogLevel.Debug);
            return true;
        }

        internal static bool IgnoreConsoleCancelKeyPress(CtrlTypes ctrlType)
        {
            if (ctrlType.In(CtrlTypes.CTRL_C_EVENT, CtrlTypes.CTRL_BREAK_EVENT))
                return true;

            return false;
        }


    }
}
