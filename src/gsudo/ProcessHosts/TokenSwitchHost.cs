using System;
using System.Threading.Tasks;
using gsudo.Helpers;
using gsudo.Rpc;
using gsudo.Tokens;

namespace gsudo.ProcessHosts
{
    /// <summary>
    /// Replaces the token on a process started in suspended mode.
    /// Based on https://stackoverflow.com/questions/5141997/is-there-a-way-to-set-a-token-for-another-process
    /// </summary>
    class TokenSwitchHost : IProcessHost
    {
        public bool SupportsSimultaneousElevations { get; } = true;

        public async Task Start(Connection connection, ElevationRequest elevationRequest)
        {
            if (Settings.SecurityEnforceUacIsolation && !elevationRequest.NewWindow)
                throw new Exception("TokenSwitch mode not supported when SecurityEnforceUacIsolation is set.");

            try
            {
                TokenSwitcher.ReplaceProcessToken(elevationRequest);

                await connection.ControlStream.WriteAsync(Constants.TOKEN_SUCCESS).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ex.ToString()}", LogLevel.Debug);
                await connection.ControlStream
                    .WriteAsync(
                        $"{Constants.TOKEN_ERROR}Server Error: Setting token failed.\r\n{ex.Message}\r\n{Constants.TOKEN_ERROR}")
                    .ConfigureAwait(false);
            }
            finally
            {
                Native.ConsoleApi.SetConsoleCtrlHandler(ConsoleHelper.IgnoreConsoleCancelKeyPress, false);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
        }

    }
}
