using gsudo.Native;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using static gsudo.Native.ConsoleApi;
using Windows.Win32;
using Windows.Win32.System.Console;
using Microsoft.Win32.SafeHandles;
using Windows.Win32.Foundation;

namespace gsudo.Helpers
{
    class ConsoleHelper
    {
        const UInt32 ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        static ConsoleHelper()
        {
            IgnoreConsoleCancelKeyPress += IgnoreConsoleCancelKeyPressMethod; // ensure no garbage collection
        }

        public static unsafe bool EnableVT()
        {
            var hStdOut = PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
            CONSOLE_MODE outConsoleMode;
            if (!PInvoke.GetConsoleMode(hStdOut, &outConsoleMode))
            {
                Logger.Instance.Log("Could not get console mode", LogLevel.Debug);
                return false;
            }

            outConsoleMode |= CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING;// | CONSOLE_MODE.DISABLE_NEWLINE_AUTO_RETURN;
            if (!PInvoke.SetConsoleMode(hStdOut, outConsoleMode))
            {
                Logger.Instance.Log("Could not enable virtual terminal processing", LogLevel.Error);
                return false;
            }

            Logger.Instance.Log("Console VT mode enabled.", LogLevel.Debug);
            return true;
        }

        internal static PHANDLER_ROUTINE IgnoreConsoleCancelKeyPress;

        private static BOOL IgnoreConsoleCancelKeyPressMethod(uint ctrlType)
        {
            if (ctrlType == (uint)CtrlTypes.CTRL_C_EVENT || ctrlType == (uint)CtrlTypes.CTRL_BREAK_EVENT)
                return true;

            return false;
        }

        public static uint[] GetConsoleAttachedPids()
        {
            var processIds = new uint[1];
            var num = ConsoleApi.GetConsoleProcessList(processIds, 1);
            if (num == 0) throw new System.ComponentModel.Win32Exception();

            processIds = new uint[num];

            num = ConsoleApi.GetConsoleProcessList(processIds, num);
            if (num == 0) throw new System.ComponentModel.Win32Exception();

            //** weird workaround for .net 7.0 NativeAOT + git-bash **
            if (processIds[0] == 0)
                num = ConsoleApi.GetConsoleProcessList(processIds, num);
            if (processIds[0] == 0)
                num = ConsoleApi.GetConsoleProcessList(processIds, num);
            //**************************************************
            return processIds;
        }

        public static void GetConsoleInfo(out int width, out int height, out int cursorLeftPos, out int cursorTopPos)
        {
            if (Console.IsOutputRedirected && Console.IsErrorRedirected)
            {
                var hConsole = Native.FileApi.CreateFile("CONOUT$",
                    FileApi.GENERIC_READ, 0, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

                if (hConsole == Native.FileApi.INVALID_HANDLE_VALUE)
                    throw new System.ComponentModel.Win32Exception();

                var consoleScreenBufferInfoEx = new CONSOLE_SCREEN_BUFFER_INFO_EX();
                consoleScreenBufferInfoEx.cbSize = Marshal.SizeOf<CONSOLE_SCREEN_BUFFER_INFO_EX>();

                if (!GetConsoleScreenBufferInfoEx(hConsole, ref consoleScreenBufferInfoEx))
                    throw new System.ComponentModel.Win32Exception();

                width = consoleScreenBufferInfoEx.srWindow.Right - consoleScreenBufferInfoEx.srWindow.Left + 1;
                height = consoleScreenBufferInfoEx.srWindow.Bottom - consoleScreenBufferInfoEx.srWindow.Top + 1;
                cursorLeftPos = consoleScreenBufferInfoEx.dwCursorPosition.X;
                cursorTopPos = consoleScreenBufferInfoEx.dwCursorPosition.Y;

                FileApi.CloseHandle(hConsole);
            }
            else
            {
                width = Console.WindowWidth;
                height = Console.WindowHeight;
                cursorLeftPos = Console.CursorLeft;
                cursorTopPos = Console.CursorTop;
            }
        }

        internal static SecureString ReadConsolePassword(string userName)
        {
            Console.Error.Write($"Password for user {userName}: ");

            var pass = new SecureString();
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    Console.Error.Write("\b \b");
                    pass.RemoveAt(pass.Length - 1);
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Error.Write("*");
                    pass.AppendChar(keyInfo.KeyChar);
                }
            } while (key != ConsoleKey.Enter);
            Console.Error.Write("\n");
            return pass;
        }

        internal static void SetPrompt(ElevationRequest elevationRequest)
        {
            if (!string.IsNullOrEmpty(elevationRequest.Prompt))
            {
                if (elevationRequest.IntegrityLevel < IntegrityLevel.High)
                    Environment.SetEnvironmentVariable("PROMPT", Environment.GetEnvironmentVariable("PROMPT", EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable("PROMPT", EnvironmentVariableTarget.Machine) ?? "$P$G");
                else
                    Environment.SetEnvironmentVariable("PROMPT", Environment.ExpandEnvironmentVariables(elevationRequest.Prompt));
            }
        }
    }
}
