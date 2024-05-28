using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using static gsudo.Native.PseudoConsoleApi;

namespace gsudo.Native
{
    /// <summary>
    /// PInvoke signatures for win32 console api
    /// </summary>
    static class ConsoleApi
    {
        internal enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
    }
}
