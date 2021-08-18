using gsudo.Native;
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

        internal static SetConsoleCtrlEventHandler IgnoreConsoleCancelKeyPress;

        private static bool IgnoreConsoleCancelKeyPressMethod(CtrlTypes ctrlType)
        {
            if (ctrlType.In(CtrlTypes.CTRL_C_EVENT, CtrlTypes.CTRL_BREAK_EVENT))
                return true;

            return false;
        }

        static ConsoleHelper()
        {
            IgnoreConsoleCancelKeyPress += IgnoreConsoleCancelKeyPressMethod;
        }

        public static uint[] GetConsoleAttachedPids()
        {
            var processIds = new uint[1];
            var num = ConsoleApi.GetConsoleProcessList(processIds, 1);
            if (num == 0) throw new System.ComponentModel.Win32Exception();

            processIds = new UInt32[num];

            num = ConsoleApi.GetConsoleProcessList(processIds, (uint)processIds.Length);
            if (num == 0) throw new System.ComponentModel.Win32Exception();
            return processIds;
        }
    }
}
