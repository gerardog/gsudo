using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using gsudo.Helpers;
using gsudo.Native;
using static gsudo.Native.TokensApi;
using static gsudo.Native.NativeMethods;

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
                var tokenInfo = new NtDllApi.PROCESS_ACCESS_TOKEN();
                tokenInfo.Token = desiredToken.DangerousGetHandle();
                tokenInfo.Thread = IntPtr.Zero;

                // We need System account to replace  process
                // To set an elevated process token we don't need to impersonate System...
                // But to set a System token to a process, we do need SeAssignPrimaryTokenPrivilege.
                TokenProvider
                    .CreateFromSystemAccount()
                    .EnablePrivilege(Privilege.SeAssignPrimaryTokenPrivilege, false)
                    .EnablePrivilege(Privilege.SeTcbPrivilege, false)
                    .EnablePrivilege(Privilege.SeIncreaseQuotaPrivilege, false)
                    .Impersonate(() =>
                    {
                        IntPtr securityDescriptor = IntPtr.Zero;
                        UIntPtr securityDescriptorSize = UIntPtr.Zero;
                        IntPtr hProcess = IntPtr.Zero;
                        try
                        {
                            hProcess = ProcessApi.OpenProcess(ProcessApi.PROCESS_SET_INFORMATION | ProcessApi.PROCESS_QUERY_INFORMATION | ProcessApi.READ_CONTROL | ProcessApi.WRITE_DAC, true,
                                (uint)elevationRequest.TargetProcessId);

                            // Tighten the security descriptor of the process to elevate, before elevating its process token
                            string sddl = "D:(D;;GAFAWD;;;S-1-1-0)S:(ML;;NW;;;HI)"; // Deny all to everyone. SACL requires High Integrity.
                            Native.TokensApi.ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, StringSDRevision: 1, out securityDescriptor, out securityDescriptorSize);

                            // https://learn.microsoft.com/en-us/windows/win32/secauthz/low-level-security-descriptor-functions
                            if (!SetKernelObjectSecurity(hProcess, (uint)SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, securityDescriptor))
                                throw new InvalidOperationException("Failed to tighten security descriptor.", new Win32Exception());

                            // Replace the Process access token with an elevated one.
                            var processInformationClass = NtDllApi.PROCESS_INFORMATION_CLASS.ProcessAccessToken;

                            int res = NtDllApi.NativeMethods.NtSetInformationProcess(hProcess, processInformationClass,
                                ref tokenInfo, Marshal.SizeOf<NtDllApi.PROCESS_ACCESS_TOKEN>());

                            if (res < 0)
                                throw new Win32Exception();

                            Logger.Instance.Log($"Process token replaced", LogLevel.Debug);
                        }
                        finally
                        {
                            if (hProcess != IntPtr.Zero)
                                ProcessApi.CloseHandle(hProcess);

                            // Cleanup: Free the security descriptor pointer if it's not null
                            if (securityDescriptor != IntPtr.Zero)
                                ProcessApi.LocalFree(securityDescriptor);
                        }
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

            if (InputArguments.TrustedInstaller)
            {
                tm = TokenProvider.CreateFromCurrentProcessToken();
            }
            else if (elevationRequest.IntegrityLevel == IntegrityLevel.System)
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
                // Therefore we can use this to run unelevated with a manifest with "requireAdministrator"
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
                if (SecurityHelper.GetCurrentIntegrityLevel() != (int)elevationRequest.IntegrityLevel)
                {
                    tm.SetIntegrity(elevationRequest.IntegrityLevel);
                }
            }

            desiredToken = tm.GetToken();
            return desiredToken;
        }
    }
}
