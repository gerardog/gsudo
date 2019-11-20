using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace gsudo.Helpers
{
    static class ProcessStarter
    {
        public static Process StartElevatedDetached(string filename, string arguments, bool hidden)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(filename, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            };

            if (hidden)
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            else
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;

            process.Start();
            return process;
        }

        public static Process StartInProcessAtached(string filename, string arguments)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(filename)
            {
                Arguments = arguments,
                UseShellExecute = false,
            };
            process.Start();
            return process;
        }

        public static Process StartDetached(string filename, string arguments, bool hidden = true)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(filename)
            {
                Arguments = arguments,
            };

            if (hidden)
            {
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
            }
            else
            {
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                process.Start();
                for (int i = 0; process.MainWindowHandle == IntPtr.Zero && i<30; i++)
                    System.Threading.Thread.Sleep(10);

                // check if the window is hidden / minimized
                //if (process.MainWindowHandle == IntPtr.Zero)
                //{
                //    // the window is hidden so try to restore it before setting focus.
                //    ShowWindow(process.Handle, ShowWindowEnum.Restore);
                //}

                // set user the focus to the window
                SetForegroundWindow(process.MainWindowHandle);
                
                //IntPtr mainWindow = process.MainWindowHandle;
                //IntPtr newPos = new IntPtr(-1);  // 0 puts it on top of Z order.   You can do new IntPtr(-1) to force it to a topmost window, instead.
                //SetWindowPos(mainWindow, new IntPtr(0), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);


            }

            return process;
        }

        //[System.Runtime.InteropServices.DllImport("user32.dll")]
        //[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        //private static extern bool ShowWindow(IntPtr hWnd, ShowWindowEnum flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hwnd);

        //private enum ShowWindowEnum
        //{
        //    Hide = 0,
        //    ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3,
        //    Maximize = 3, ShowNormalNoActivate = 4, Show = 5,
        //    Minimize = 6, ShowMinNoActivate = 7, ShowNoActivate = 8,
        //    Restore = 9, ShowDefault = 10, ForceMinimized = 11
        //};

        //[DllImport("user32.dll")]
        //static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        //const UInt32 SWP_NOSIZE = 0x0001;
        //const UInt32 SWP_NOMOVE = 0x0002;
        //const UInt32 SWP_SHOWWINDOW = 0x0040;



    }
}
