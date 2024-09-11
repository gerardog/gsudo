using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using static gsudo.Native.TokensApi;
using static gsudo.Tokens.NativeMethods;

namespace gsudo.Tokens
{
    //Enable a privilege in the current thread by implementing the following line in your code:
    //PrivilegeManager.EnablePrivilege(SecurityEntity.SE_SHUTDOWN_NAME);

    public static class PrivilegeManager
    {
        public static void DisableAllPrivileges(IntPtr tokenHandle)
        {
            var TOKEN_PRIVILEGES = new NativeMethods.TOKEN_PRIVILEGES();
            
            if (!NativeMethods.AdjustTokenPrivileges(tokenHandle, true, ref TOKEN_PRIVILEGES, 0, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception();
        }

        public static void SetPrivilegeState(Privilege securityEntity, bool enabled)
        {
            const int ERROR_NO_TOKEN = 0x3f0;

            var locallyUniqueIdentifier = new LUID();

            if (!LookupPrivilegeValue(null, securityEntity.ToString(), ref locallyUniqueIdentifier))
                throw new Win32Exception();

            if (!OpenThreadToken(GetCurrentThread(), TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges, true, out var token))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ERROR_NO_TOKEN)
                {
                    throw new Win32Exception(error);
                }

                // No token is on the thread, copy from process
                if (!OpenProcessToken(GetCurrentProcess(), (uint)TokenAccessLevels.Duplicate, out var processToken))
                {
                    throw new Win32Exception();
                }

                if (!DuplicateTokenEx(processToken, (uint)(TokenAccessLevels.Impersonate | TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges),
                    IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenImpersonation, out token))
                {
                    throw new Win32Exception();
                }
                
                if (!SetThreadToken(IntPtr.Zero, token))
                {
                    throw new Win32Exception();
                }
            }

            var tp = new NativeMethods.TOKEN_PRIVILEGES();
            tp.PrivilegeCount = 1;

            tp.Privileges = new LUID_AND_ATTRIBUTES[1];
            tp.Privileges[0].Attributes = (uint) (enabled ? NativeMethods.SE_PRIVILEGE_ENABLED : NativeMethods.SE_PRIVILEGE_DISABLED);
            tp.Privileges[0].Luid = locallyUniqueIdentifier;
            
            if (!NativeMethods.AdjustTokenPrivileges(token.DangerousGetHandle(), false, ref tp, (uint)Marshal.SizeOf(tp),
                IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception();

            Logger.Instance.Log($"Privilege {securityEntity} was {(enabled ? "ENABLED" : "DISABLED")}", LogLevel.Warning);
            token.Close();
            // todo: proper close.            
        }
    }   
}