using gsudo.Native;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace gsudo.Helpers
{
    internal class UACWindowFocusHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        internal static void StartBackgroundThreadToFocusUacWindow()
        {
            var focusThread = new Thread(UACWindowFocusHelper.FocusUacWindow);
            focusThread.IsBackground = true;
            focusThread.Start();
        }

        internal static void FocusUacWindow()
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    // Wait a moment to allow the UAC prompt to appear
                    System.Threading.Thread.Sleep(100);

                    // Find the UAC window
                    string classname = "Credential Dialog Xaml Host"; // Found using Visual Studio spyxx_amd64.exe, this is the value for Windows 10 & 11.
                    IntPtr uacWindow = FindWindow(classname, null);
                    if (uacWindow != IntPtr.Zero)
                    {
                        // Set focus to the UAC window
                        WindowApi.SetForegroundWindow(uacWindow);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error searching for UAC Window: " + ex.ToString(), LogLevel.Debug);
            }
        }
    }
}
