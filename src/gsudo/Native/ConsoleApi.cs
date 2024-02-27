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

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        /// <summary>
        ///     Retrieves a handle to the Shell's desktop window.
        ///     <para>
        ///     Go to https://msdn.microsoft.com/en-us/library/windows/desktop/ms633512%28v=vs.85%29.aspx for more
        ///     information
        ///     </para>
        /// </summary>
        /// <returns>
        ///     C++ ( Type: HWND )<br />The return value is the handle of the Shell's desktop window. If no Shell process is
        ///     present, the return value is NULL.
        /// </returns>
        [DllImport("user32.dll")]
        internal static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetConsoleScreenBufferInfoEx(
            IntPtr hConsoleOutput,
            ref CONSOLE_SCREEN_BUFFER_INFO_EX ConsoleScreenBufferInfo
            );

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_SCREEN_BUFFER_INFO_EX
        {
            public int cbSize;
            public COORD dwSize;
            public COORD dwCursorPosition;
            public short wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;

            public ushort wPopupAttributes;
            public bool bFullscreenSupported;

            internal COLORREF black;
            internal COLORREF darkBlue;
            internal COLORREF darkGreen;
            internal COLORREF darkCyan;
            internal COLORREF darkRed;
            internal COLORREF darkMagenta;
            internal COLORREF darkYellow;
            internal COLORREF gray;
            internal COLORREF darkGray;
            internal COLORREF blue;
            internal COLORREF green;
            internal COLORREF cyan;
            internal COLORREF red;
            internal COLORREF magenta;
            internal COLORREF yellow;
            internal COLORREF white;

            // has been a while since I did this, test before use
            // but should be something like:
            //
            // [MarshalAs(UnmanagedType.ByValArray, SizeConst=16)]
            // public COLORREF[] ColorTable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COLORREF
        {
            public uint ColorDWORD;
        }
        public struct SMALL_RECT
        {

            public short Left;
            public short Top;
            public short Right;
            public short Bottom;

        }
    }
}
