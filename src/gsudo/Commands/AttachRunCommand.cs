using System;
using gsudo.Helpers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using gsudo.Native;

namespace gsudo.Commands
{
    /// <summary>
    /// This command attaches to the parent console, then executes the command.
    /// This works even if the parent has higher integrity level than us.
    /// This must be launched by the caller gsudo and not the elevated service, because the parent process id must have the user console.
    /// </summary>
    class AttachRunCommand : ICommand
    {
        public IEnumerable<string> CommandToRun { get; private set; }

        public AttachRunCommand(IEnumerable<string> commandToRun)
        {
            CommandToRun = commandToRun;
        }
        
        public Task<int> Execute()
        {
            ConsoleApi.FreeConsole();
            if (!ConsoleApi.AttachConsole(-1))
            {
                ConsoleApi.AllocConsole();
                throw new ApplicationException($"Failed to attach console: {new Win32Exception()}");
            }

            var app = CommandToRun.First();
            var args = string.Join(" ", CommandToRun.Skip(1).ToArray());

            if (InputArguments.IntegrityLevel.HasValue &&
                (int) InputArguments.IntegrityLevel != SecurityHelper.GetCurrentIntegrityLevel() &&
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
                ProcessFactory.StartAttached(app, args).WaitForExit();
            }

            return Task.FromResult(0);
        }
    }
}
