using System;

namespace gsudo.Native
{
    static class WindowApi
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hwnd);

    }
}
