using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using gsudo.Helpers;
using gsudo.Native;
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
        public async Task Start(Connection connection, ElevationRequest elevationRequest)
        {
            if (Settings.SecurityEnforceUacIsolation)
                throw new Exception("TokenSwitch mode not supported when SecurityEnforceUacIsolation is set.");

            try
            {
                TokenManager
                    .CreateFromSystemAccount()
                    .EnablePrivilege(Privilege.SeAssignPrimaryTokenPrivilege, true)
                    .Impersonate(() =>
                    {
                        SafeTokenHandle desiredToken = null;
                        desiredToken = GetDesiredToken(elevationRequest);

                        var tokenInfo = new TokensApi.PROCESS_ACCESS_TOKEN();
                        tokenInfo.Token = desiredToken.DangerousGetHandle();
                        tokenInfo.Thread = IntPtr.Zero;

                        IntPtr hProcess = ProcessApi.OpenProcess(ProcessApi.PROCESS_SET_INFORMATION, true,
                            (uint) elevationRequest.TargetProcessId);
                        TokensApi._PROCESS_INFORMATION_CLASS processInformationClass =
                            TokensApi._PROCESS_INFORMATION_CLASS.ProcessAccessToken;

                        int res = TokensApi.NtSetInformationProcess(hProcess, processInformationClass, ref tokenInfo,
                            Marshal.SizeOf<TokensApi.PROCESS_ACCESS_TOKEN>());
                        Logger.Instance.Log($"NtSetInformationProcess returned {res}", LogLevel.Debug);
                        if (res < 0)
                            throw new Win32Exception();

                        ProcessApi.CloseHandle(hProcess);
                        desiredToken.Close();
                    });
                
                await connection.ControlStream.WriteAsync(Constants.TOKEN_SUCCESS).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ex.ToString()}", LogLevel.Debug);
                await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: Setting token failed.\r\n{ex.Message}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
            }
            finally
            {
                Native.ConsoleApi.SetConsoleCtrlHandler(ConsoleHelper.IgnoreConsoleCancelKeyPress, false);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
        }

        private static SafeTokenHandle GetDesiredToken(ElevationRequest elevationRequest)
        {
            TokenManager tm;
            SafeTokenHandle desiredToken;

            if (elevationRequest.IntegrityLevel == IntegrityLevel.System)
            {
                tm = TokenManager.CreateFromSystemAccount().Duplicate();

                /* God Mode:
                tm.EnablePrivileges(false,
                    Privilege.SeAssignPrimaryTokenPrivilege, Privilege.SeIncreaseQuotaPrivilege,
                    Privilege.SeTcbPrivilege, Privilege.SeSecurityPrivilege, Privilege.SeTakeOwnershipPrivilege,
                    Privilege.SeLoadDriverPrivilege, Privilege.SeProfileSingleProcessPrivilege,
                    Privilege.SeIncreaseBasePriorityPrivilege, Privilege.SeCreatePermanentPrivilege,
                    Privilege.SeBackupPrivilege, Privilege.SeRestorePrivilege, Privilege.SeShutdownPrivilege,
                    Privilege.SeDebugPrivilege, Privilege.SeAuditPrivilege, Privilege.SeSystemEnvironmentPrivilege,
                    Privilege.SeChangeNotifyPrivilege, Privilege.SeUndockPrivilege, Privilege.SeManageVolumePrivilege,
                    Privilege.SeImpersonatePrivilege, Privilege.SeCreateGlobalPrivilege,
                    Privilege.SeTrustedCredManAccessPrivilege);
                */
            }
            else if (elevationRequest.IntegrityLevel.In(IntegrityLevel.Medium, IntegrityLevel.MediumPlus))
            {
                tm = TokenManager.CreateUnelevated().SetIntegrity(elevationRequest.IntegrityLevel);
            }
            else
            {
                tm = TokenManager.CreateFromCurrentProcessToken(TokenManager.MAXIMUM_ALLOWED);
                if (ProcessHelper.GetCurrentIntegrityLevel() != (int)elevationRequest.IntegrityLevel)
                {
                    tm.SetIntegrity(elevationRequest.IntegrityLevel);
                }
            }

            desiredToken = tm.GetToken();
            return desiredToken;
        }
    }
}
