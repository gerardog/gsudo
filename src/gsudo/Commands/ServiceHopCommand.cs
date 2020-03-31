#if TO_BE_REMOVED_10
using System;
using System.Threading.Tasks;
using gsudo.Helpers;

namespace gsudo.Commands
{
    /// <summary>
    /// Intermediate command to be used when two process jumps are needed.
    /// For example from non-admin to system: non-admin 'elevates' -> gsudo as admin 'CreatesAsUser' -> System.
    /// or from non-admin medium integrity to MediumPlus: Medium -> Admin(high) -> MediumPlus
    /// </summary>
    [Obsolete("No longer needed since TokenSwitch was added.")] // TODO: Remove in 1.0
    class ServiceHopCommand : ICommand
    {
        public int allowedPid { get; set; }
        public string allowedSid { get; set; }
        public LogLevel? LogLvl { get; set; }

        public Task<int> Execute()
        {
            // service mode
            if (LogLvl.HasValue) Settings.LogLevel.Value = LogLvl.Value;

            var @params = InputArguments.Debug ? "--debug " : string.Empty;
            if (InputArguments.IntegrityLevel.HasValue) @params += $"-i {InputArguments.IntegrityLevel.Value} ";
            if (InputArguments.RunAsSystem) @params += "-s ";

            var app = ProcessHelper.GetOwnExeName();
            var args = $"{@params}gsudoservice {allowedPid} {allowedSid} {Settings.LogLevel}";

            if (ProcessHelper.IsAdministrator())
            {
                if (InputArguments.RunAsSystem)
                {
                    var process = ProcessFactory.StartAsSystem(app, args, Environment.CurrentDirectory, !InputArguments.Debug);
                    if (process == null)
                    {
                        Logger.Instance.Log("Failed to start instance as System.", LogLevel.Error);
                        return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
                    }
                }
                else // lower integrity
                {
                    var p = ProcessFactory.StartAttachedWithIntegrity(InputArguments.IntegrityLevel.Value, app, args, null, true, !InputArguments.Debug);
                    if (p == null || p.IsInvalid)
                        return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);

                    return Task.FromResult(0);
                }
                Logger.Instance.Log("Elevated instance started.", LogLevel.Debug);
            }
            else
                Logger.Instance.Log($"{nameof(ServiceHopCommand)} is not supported if not elevated.", LogLevel.Error);


            return Task.FromResult(0);
        }
    }
}
#endif