using gsudo.Native;
using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using static gsudo.Native.ConsoleApi;

namespace gsudo.Helpers
{
    class ConsoleHelper
    { 
        static ConsoleHelper()
        {
            IgnoreConsoleCancelKeyPress += IgnoreConsoleCancelKeyPressMethod; // ensure no garbage collection
        }

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

        internal static SecureString ReadPasswordFromNamedPipe(string pipeName)
        {
            try
            {
                // named pipe name should *not* include \\.\pipe\ path prefix
                using (var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In))
                {
                    pipeClient.Connect();

                    using (var reader = new StreamReader(pipeClient, Encoding.UTF8))
                    {
                        string data = reader.ReadToEnd();
                        data = data.TrimEnd('\r', '\n');

                        SecureString securePassword = new SecureString();
                        foreach (char c in data)
                        {
                            securePassword.AppendChar(c);
                        }

                        return securePassword;
                    }
                }
            }
            catch (IOException)
            {
                return null;
            }
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
