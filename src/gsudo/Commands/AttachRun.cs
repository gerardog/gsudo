using gsudo.Helpers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    /// <summary>
    /// This command attaches to the parent console, then executes the command.
    /// This works even if the parent has higher integrity level than us.
    /// This can't be launched by the elevated service, because the parent id must match
    /// </summary>
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

            if (InputArguments.IntegrityLevel.HasValue && (int)InputArguments.IntegrityLevel != ProcessHelper.GetCurrentIntegrityLevel())
            {
                Helpers.ProcessFactory.StartWithIntegrity(InputArguments.GetIntegrityLevel(), app, args, Directory.GetCurrentDirectory(), false, true)
                    .GetProcessWaitHandle()
                    .WaitOne();
            }
            else
            {
                Helpers.ProcessFactory.StartInProcessAtached(app, args).WaitForExit();
            }

            return Task.FromResult(0);
        }
    }
}
