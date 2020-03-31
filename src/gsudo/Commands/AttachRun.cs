#if TO_BE_REMOVED_10
using System;
using gsudo.Helpers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using gsudo.Native;
using Microsoft.Win32.SafeHandles;

namespace gsudo.Commands
{
    /// <summary>
    /// This command attaches to the parent console, then executes the command.
    /// This works even if the parent has higher integrity level than us.
    /// This must be launched by the non-elevated gsudo and not the elevated service, because the parent id must match.
    /// </summary>
    [Obsolete("No longer needed since TokenSwitch was added.")] // TODO: Remove in 1.0
    class AttachRun : ICommand
    {
        public IEnumerable<string> CommandToRun { get; set; }
        
        public Task<int> Execute()
        {
            Native.ConsoleApi.FreeConsole();
            if (!Native.ConsoleApi.AttachConsole(-1))
            {
                Native.ConsoleApi.AllocConsole();
                Logger.Instance.Log($"Failed to attach console.\r\n{new Win32Exception().ToString()}", LogLevel.Error);
            }

            var app = CommandToRun.First();
            var args = string.Join(" ", CommandToRun.Skip(1).ToArray());

            if (InputArguments.IntegrityLevel.HasValue &&
                (int) InputArguments.IntegrityLevel != ProcessHelper.GetCurrentIntegrityLevel() &&
                Environment.GetEnvironmentVariable("gsudoAttachRun") != "1")
            {
                Environment.SetEnvironmentVariable("gsudoAttachRun", "1"); // prevents infinite loop on machines with UAC disabled.
                
                var process = ProcessFactory.StartAttachedWithIntegrity(
                    InputArguments.GetIntegrityLevel(), app, args, Directory.GetCurrentDirectory(), false, true);

                process.GetProcessWaitHandle().WaitOne();

                if (ProcessApi.GetExitCodeProcess(process, out var exitCode))
                    return Task.FromResult(exitCode);
            }
            else
            {
                Helpers.ProcessFactory.StartAttached(app, args).WaitForExit();
            }

            return Task.FromResult(0);
        }
    }
}
#endif