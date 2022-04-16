using gsudo.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class BangBangCommand : ICommand
    {
        public string Pattern { get; internal set; }

        public Task<int> Execute()
        {
            var caller = Process.GetCurrentProcess().GetShellProcess().MainModule.ModuleName;
            var length = (int)NativeMethods.GetConsoleCommandHistoryLength(caller);

            if (length == 0)
                throw new ApplicationException("Failed to find last invoked command (GetConsoleCommandHistoryLength==0)");

            IntPtr CommandBuffer = Marshal.AllocHGlobal(length);
            var ret = NativeMethods.GetConsoleCommandHistory(CommandBuffer, length, caller);

            if (ret == 0)
                throw new ApplicationException($"Failed to find last invoked command (GetConsoleCommandHistory=0; LastErr={Marshal.GetLastWin32Error()})");

            string commandToElevate;

            var commandHistory = Marshal.PtrToStringAuto(CommandBuffer, length / 2)
                .Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Reverse() // look for last commands first
                .Skip(1) // skip gsudo call
                ;

            if (Pattern == "!!")
            {
                commandToElevate = commandHistory.FirstOrDefault();
            }
            else if (Pattern.StartsWith("!?", StringComparison.OrdinalIgnoreCase))
            {
                commandToElevate = commandHistory.FirstOrDefault(s => s.ToUpperInvariant().Contains(Pattern.Substring(2).Trim().ToUpperInvariant()));
            }
            else // Pattern.StartsWith ("!command")
            {
                commandToElevate = commandHistory.FirstOrDefault(s => s.StartsWith(Pattern.Substring(1).Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (commandToElevate == null)
                throw new ApplicationException("Failed to find last invoked command in history.");

            Logger.Instance.Log("Command to run: " + commandToElevate, LogLevel.Info);

            return new RunCommand()
                { CommandToRun = ArgumentsHelper.SplitArgs(commandToElevate) }
            .Execute();
        }

        class NativeMethods
        {
            // Many thanks to comment from eryk sun for posting here: https://www.hanselman.com/blog/ForgottenButAwesomeWindowsCommandPromptFeatures.aspx
            // (Otherwise I wouldnt be able to find this undocumented api.)

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern UInt32 GetConsoleCommandHistoryLength(string ExeName);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern UInt32 GetConsoleCommandHistory(
                                 IntPtr CommandBuffer,
                                 int CommandBufferLength,
                                 string ExeName);

        }
    }
}
