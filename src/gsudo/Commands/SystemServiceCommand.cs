using System;
using System.Threading.Tasks;
using gsudo.Helpers;
using System.Diagnostics;

namespace gsudo.Commands
{
    /// <summary>
    /// We can only spawn a process as system account if we were elevated first. 
    /// So, 
    /// Non-elevated Gsudo client -(elevates)-> Gsudo SystemService -(runs as System)-> Gsudo Service.
    /// Then..
    /// Non-elevated Gsudo client connects to Gsudo Service running as system.
    /// </summary>
    class SystemServiceCommand : ICommand
    {
        public int allowedPid { get; set; }
        public string allowedSid { get; set; }
        public LogLevel? LogLvl { get; set; }

        public Task<int> Execute()
        {
            // service mode
            if (LogLvl.HasValue) Settings.LogLevel.Value = LogLvl.Value;

            var dbg = InputArguments.Debug ? "--debug " : string.Empty;

            if (ProcessExtensions.IsAdministrator())
            {
                var process = ProcessFactory.StartAsSystem(Process.GetCurrentProcess().MainModule.FileName, $"{dbg}-s gsudoservice {allowedPid} {allowedSid} {Settings.LogLevel}", Environment.CurrentDirectory, !InputArguments.Debug);
                if (process == null)
                {
                    Logger.Instance.Log("Failed to start elevated instance.", LogLevel.Error);
                    return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
                }

                Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
            }

            return Task.FromResult(0);
        }
    }
}
