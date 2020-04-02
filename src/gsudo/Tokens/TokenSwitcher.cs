using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using gsudo.Helpers;
using gsudo.Native;

namespace gsudo.Tokens
{
    class TokenSwitcher
    {
        public static void ReplaceProcessToken(ElevationRequest elevationRequest)
        {
            SafeTokenHandle desiredToken;
            desiredToken = GetDesiredToken(elevationRequest);
            try
            {
                var tokenInfo = new TokensApi.PROCESS_ACCESS_TOKEN();
                tokenInfo.Token = desiredToken.DangerousGetHandle();
                tokenInfo.Thread = IntPtr.Zero;

                TokenProvider
                    .CreateFromSystemAccount()
                    .EnablePrivilege(Privilege.SeAssignPrimaryTokenPrivilege, true)
                    .Impersonate(() =>
                    {
                        IntPtr hProcess = ProcessApi.OpenProcess(ProcessApi.PROCESS_SET_INFORMATION, true,
                            (uint)elevationRequest.TargetProcessId);
                        TokensApi._PROCESS_INFORMATION_CLASS processInformationClass =
                            TokensApi._PROCESS_INFORMATION_CLASS.ProcessAccessToken;

                        int res = TokensApi.NtSetInformationProcess(hProcess, processInformationClass,
                            ref tokenInfo,
                            Marshal.SizeOf<TokensApi.PROCESS_ACCESS_TOKEN>());
                        Logger.Instance.Log($"NtSetInformationProcess returned {res}", LogLevel.Debug);
                        if (res < 0)
                            throw new Win32Exception();

                        ProcessApi.CloseHandle(hProcess);
                    });
            }
            finally
            {
                desiredToken?.Close();
            }
        }

        private static SafeTokenHandle GetDesiredToken(ElevationRequest elevationRequest)
        {
            TokenProvider tm = null;
            SafeTokenHandle desiredToken;

            if (elevationRequest.IntegrityLevel == IntegrityLevel.System)
            {
                tm = TokenProvider.CreateFromSystemAccount().Duplicate();

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
            else if (elevationRequest.IntegrityLevel == IntegrityLevel.MediumRestricted)
            {
                // This is an experiment based on a problem present at least in Win10 1909.
                // Safer api generates what IMHO is a broken token and we can profit from this.
                // The safer api, when invoked from an elevated process, generates a non-admin token
                // but with the elevation flag set.
                // A process using this flag that tries to elevate/use the RunAs Verb, will get a "success"
                // but the process will be ran without real elevation.
                // Therefore we can use this to run software with a manifest with "requireAdministrator"
                // that tipically can only be run elevated
                tm = TokenProvider.CreateFromSaferApi(TokensApi.SaferLevels.NormalUser)
                    .SetIntegrity(elevationRequest.IntegrityLevel);
            }
            else if (elevationRequest.IntegrityLevel >= IntegrityLevel.Medium &&
                     elevationRequest.IntegrityLevel < IntegrityLevel.High)
            {
                tm = TokenProvider.CreateUnelevated(elevationRequest.IntegrityLevel);
            }
            else
            {
                tm = TokenProvider.CreateFromCurrentProcessToken(TokenProvider.MAXIMUM_ALLOWED);
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
