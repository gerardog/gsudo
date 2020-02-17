using System;
using System.Threading.Tasks;
using gsudo.Helpers;
using System.Diagnostics;

namespace gsudo.Commands
{
    /// <summary>
    /// Intermediate command that to be used as intermediary to launch as Local System.
    /// </summary>
    /// <remarks>
    /// We can only spawn a process as system account if we were elevated first. 
    /// Here is how it works:
    /// 'Non-elevated Gsudo client' ELEVATES-> new Gsudo SystemService.
    /// Then elevated Gsudo SystemService STARTS-AS-SYSTEM a -> Gsudo Service.
    /// Finally..
    /// Non-elevated Gsudo client connects to Gsudo Service running as system.
    /// </remarks>
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

            if (ProcessHelper.IsAdministrator())
            {
                var process = ProcessFactory.StartAsSystem(ProcessHelper.GetOwnExeName(), $"{dbg}-s gsudoservice {allowedPid} {allowedSid} {Settings.LogLevel}", Environment.CurrentDirectory, !InputArguments.Debug);
                if (process == null)
                {
                    Logger.Instance.Log("Failed to start instance as System.", LogLevel.Error);
                    return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
                }

                Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
            }
            else
                Logger.Instance.Log($"{nameof(SystemServiceCommand)} is not supported if not elevated.", LogLevel.Error);


            return Task.FromResult(0);
        }
    }
}
