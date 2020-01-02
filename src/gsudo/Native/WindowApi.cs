using System;

namespace gsudo.Native
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1060:Move pinvokes to native methods class", Justification = "<Pending>")]
    static class WindowApi
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hwnd);

    }
}
