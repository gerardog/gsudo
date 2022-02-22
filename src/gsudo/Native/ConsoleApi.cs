using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using static gsudo.Native.PseudoConsoleApi;

namespace gsudo.Native
{
    /// <summary>
    /// PInvoke signatures for win32 console api
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class", Justification = "Done")]
    static class ConsoleApi
    {
        internal const int STD_INPUT_HANDLE = -10;
        internal const int STD_OUTPUT_HANDLE = -11;
        internal const int STD_ERROR_HANDLE = -12;

        internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        internal const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        internal delegate bool ConsoleEventDelegate(CtrlTypes ctrlType);

        internal enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        internal delegate bool SetConsoleCtrlEventHandler(CtrlTypes sig);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler callback, bool add);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [DllImport("kernel32.dll")]
        internal static extern bool SetConsoleCursorPosition(IntPtr hConsoleOutput, PseudoConsoleApi.COORD CursorPosition);


        // send ctrl-c
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool AllocConsole();

        // Enumerated type for the control messages sent to the handler routine

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern System.IntPtr GetCommandLine();


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
