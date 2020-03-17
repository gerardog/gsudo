using System;
using gsudo.Native;
using static gsudo.Native.ProcessApi;
using static gsudo.Native.TokensApi;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using gsudo.Helpers;

namespace gsudo.Tokens
{
    internal class TokenManager : IDisposable
    {
        public const uint MAXIMUM_ALLOWED = 0x02000000;

        private SafeTokenHandle Token;
        public SafeTokenHandle GetToken() => Token;
        private TokenManager() { }

        public static TokenManager CreateFromSystemAccount()
        {
            var winlogon = Process.GetProcesses().Where(p => p.ProcessName.In("winlogon")).FirstOrDefault();
            return CreateFromProcessToken(winlogon.Id);
        }

        public static TokenManager CreateFromProcessToken(int pidWithToken, uint tokenAccess = MAXIMUM_ALLOWED)
        {
            IntPtr existingProcessHandle = OpenProcess(PROCESS_QUERY_INFORMATION, true, (uint)pidWithToken);
            if (existingProcessHandle == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            IntPtr existingProcessToken;
            try
            {
                if (!ProcessApi.OpenProcessToken(existingProcessHandle,
                    TOKEN_DUPLICATE
                    //TOKEN_ALL_ACCESS,
//                        TOKEN_DUPLICATE
//                        // TokensApi.TOKEN_ADJUST_DEFAULT |
//                        | TokensApi.TOKEN_QUERY 
//                        | TokensApi.TOKEN_ASSIGN_PRIMARY
//                    | TOKEN_QUERY_SOURCE
//                    | TOKEN_IMPERSONATE
//                    | TOKEN_READ
////                    | TOKEN_ALL_ACCESS ==> access denied
////                    | STANDARD_RIGHTS_REQUIRED ==> access denied.
                    ,
                    out existingProcessToken))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                CloseHandle(existingProcessHandle);
            }
            if (existingProcessToken == IntPtr.Zero) return null;

            var sa = new SECURITY_ATTRIBUTES();
            sa.nLength = 0;
            uint desiredAccess = MAXIMUM_ALLOWED;

            SafeTokenHandle newToken;

            if (!TokensApi.DuplicateTokenEx(existingProcessToken, desiredAccess, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out newToken))
            {
                throw new Win32Exception();
            }

            return new TokenManager()
            {
                Token = newToken
            };
        }

        public static TokenManager CreateFromSaferApi(SaferLevels saferLevel)
        {
            IntPtr hSaferLevel;
            SafeTokenHandle hToken;

            if (!TokensApi.SaferCreateLevel(TokensApi.SaferScopes.User, saferLevel, 1, out hSaferLevel, IntPtr.Zero))
                throw new Win32Exception();

            try
            {
                if (!TokensApi.SaferComputeTokenFromLevel(hSaferLevel, IntPtr.Zero, out hToken, TokensApi.SaferComputeTokenFlags.None, IntPtr.Zero))
                    throw new Win32Exception();
            }
            finally
            {
                SaferCloseLevel(hSaferLevel);
            }

            return new TokenManager() { Token = hToken };        
        }

        public static TokenManager CreateFromCurrentProcessToken(uint access = 
                TOKEN_DUPLICATE | TOKEN_ADJUST_DEFAULT |
                TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY
                | TOKEN_ADJUST_PRIVILEGES
                | TOKEN_ADJUST_GROUPS)
        {
            var tm = OpenCurrentProcessToken(access);
            return tm.Duplicate(MAXIMUM_ALLOWED);
        }

        public TokenManager Duplicate(uint desiredAccess = 0x02000000)
        {
            if (!TokensApi.DuplicateTokenEx(Token.DangerousGetHandle(), desiredAccess, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out var newToken))
                throw new Win32Exception();

            Token.Close();
            Token = newToken;
            return this;
        }

        public static TokenManager OpenCurrentProcessToken(uint access =
                TOKEN_DUPLICATE | TOKEN_ADJUST_DEFAULT |
                TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY
                | TOKEN_ADJUST_PRIVILEGES
                | TOKEN_ADJUST_GROUPS)
        {
            IntPtr existingProcessToken;

            if (!ProcessApi.OpenProcessToken(Process.GetCurrentProcess().Handle,
                access
                //        internal const UInt32 TOKEN_ADJUST_SESSIONID = 0x0100;
                ,
                out existingProcessToken))
                throw new Win32Exception();

            if (existingProcessToken == IntPtr.Zero)
                throw new Win32Exception();

            return new TokenManager() { Token = new SafeTokenHandle(existingProcessToken) };
        }

        public TokenManager GetLinkedToken(uint desiredAccess = MAXIMUM_ALLOWED)
        {
            // Now we allocate a buffer for the integrity level information.
            int cb = Marshal.SizeOf<TOKEN_LINKED_TOKEN>();
            var pLinkedToken = Marshal.AllocHGlobal(cb);
            if (pLinkedToken == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            try
            {
                // Now we ask for the integrity level information again. This may fail
                // if an administrator has added this account to an additional group
                // between our first call to GetTokenInformation and this one.
                if (!TokensApi.GetTokenInformation(Token.DangerousGetHandle(),
                    TokensApi.TOKEN_INFORMATION_CLASS.TokenLinkedToken, pLinkedToken, cb,
                    out cb))
                {
                    throw new Win32Exception();
                }

                // Marshal the TOKEN_MANDATORY_LABEL struct from native to .NET object.
                TOKEN_LINKED_TOKEN linkedTokenStruct = (TOKEN_LINKED_TOKEN)
                    Marshal.PtrToStructure(pLinkedToken, typeof(TOKEN_LINKED_TOKEN));

                var sa = new SECURITY_ATTRIBUTES();
                sa.nLength = 0;

                SafeTokenHandle newToken;

                if (!TokensApi.DuplicateTokenEx(linkedTokenStruct.LinkedToken, desiredAccess, IntPtr.Zero, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out newToken))
                {
                    throw new Win32Exception();
                }

                CloseHandle(linkedTokenStruct.LinkedToken);
                this.Token.Close();
                this.Token = newToken;
            }
            finally
            {
                Marshal.FreeHGlobal(pLinkedToken);
            }
            return this;
        }

        internal static TokenManager CreateUnelevated()
        {
            if (ProcessHelper.IsAdministrator())
            {
                //try
                //{
                //    return TokenManager.CreateFromCurrentProcessToken().GetLinkedToken().Duplicate();
                //}
                //catch (Exception ex)
                //{
                //    Logger.Instance.Log(ex.ToString(), LogLevel.Warning);
                //}

                //if (WindowsIdentity.GetCurrent().IsSystem)
                {
                    IntPtr hwnd = ConsoleApi.GetShellWindow();
                    _ = ConsoleApi.GetWindowThreadProcessId(hwnd, out uint pid);
                    return TokenManager.CreateFromProcessToken((int) pid);
                }
            }
            else
            {
                return TokenManager.CreateFromCurrentProcessToken();
            }
        }

        public TokenManager SetIntegrity(IntegrityLevel integrityLevel)
        {
            return SetIntegrity((int)integrityLevel);
        }

        public TokenManager SetIntegrity(int integrityLevel)
        {
            string integritySid = "S-1-16-" + integrityLevel.ToString(System.Globalization.CultureInfo.InvariantCulture);
            IntPtr pIntegritySid;
            if (!ConvertStringSidToSid(integritySid, out pIntegritySid))
                throw new Win32Exception();

            TOKEN_MANDATORY_LABEL TIL = new TOKEN_MANDATORY_LABEL();
            TIL.Label.Attributes = 0x00000020 /* SE_GROUP_INTEGRITY */;
            TIL.Label.Sid = pIntegritySid;

            var pTIL = Marshal.AllocHGlobal(Marshal.SizeOf<TOKEN_MANDATORY_LABEL>());
            Marshal.StructureToPtr(TIL, pTIL, false);

            if (!SetTokenInformation(Token.DangerousGetHandle(),
               TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
               pTIL,
               (uint)(Marshal.SizeOf<TOKEN_MANDATORY_LABEL>() + GetLengthSid(pIntegritySid))))
                throw new Win32Exception();

            return this;
        }

        /// <summary>
        /// Use this only to restrict a non-elevated token.
        /// This method is useless if you are already elevated because it can't 
        /// remove you from Admin's group. (You are still administrator able to write in C:\windows).
        /// </summary>
        /// <param name="newToken"></param>
        public TokenManager RestrictTokenMaxPrivilege(bool ignoreErrors = false)
        {
            //            System.Security.Principal.WellKnownSidType.WorldSid;
            uint DISABLE_MAX_PRIVILEGE = 0x1;
            uint LUA_TOKEN = 0x4;
            uint WRITE_RESTRICTED = 0x8;
            SafeTokenHandle result;
            /*
            string adminSid = "S-1-5-32-544";
            IntPtr pAdminSid;
            if (!ConvertStringSidToSid(adminSid, out pAdminSid))
                throw new Win32Exception();

            SID_AND_ATTRIBUTES sa = new SID_AND_ATTRIBUTES();
            sa.Sid = pAdminSid;
            sa.Attributes = 0;

            var pSA = Marshal.AllocHGlobal(Marshal.SizeOf<SID_AND_ATTRIBUTES>());
            Marshal.StructureToPtr(sa, pSA, false);
            */
            if (!TokensApi.CreateRestrictedToken(
              Token,
              LUA_TOKEN | DISABLE_MAX_PRIVILEGE,
              //WRITE_RESTRICTED,
              0,
              IntPtr.Zero, //pSA,
              0,
              IntPtr.Zero,
              0,
              IntPtr.Zero,
              out result) && !ignoreErrors)
                throw new Win32Exception();

            Token.Close();
            Token = result;
            return this;
        }

        public TokenManager Impersonate(Action ActionImpersonated)
        {
            using (var ctx = System.Security.Principal.WindowsIdentity.Impersonate(GetToken().DangerousGetHandle()))
            {
                try
                {
                    ActionImpersonated();
                }
                finally
                {
                    ctx.Undo();
                }
            }
            return this;
        }

        public T Impersonate<T> (Func<T> ActionImpersonated)

        {
            using (var ctx = System.Security.Principal.WindowsIdentity.Impersonate(GetToken().DangerousGetHandle()))
            {
                try
                { 
                    return ActionImpersonated();
                }
                finally
                {
                    ctx.Undo();
                }
            }
        }

        public TokenManager EnablePrivileges(bool throwOnFailure, params Privilege[] priviledgesList)
        {   
            // todo: rewrite to use just 1 api call, handle exceptions,  
            foreach (var priv in priviledgesList)
                EnablePrivilege(priv, throwOnFailure);

            return this;
        }

        public TokenManager EnablePrivilege(Privilege securityEntity, bool throwOnFailure)
        {
            // todo: rewrite to use just 1 api call, handle exceptions,  
            var locallyUniqueIdentifier = new NativeMethods.LUID();

            if (!NativeMethods.LookupPrivilegeValue(null, securityEntity.ToString(), ref locallyUniqueIdentifier))
                throw new Win32Exception();

            var TOKEN_PRIVILEGES = new NativeMethods.TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES.PrivilegeCount = 1;
            TOKEN_PRIVILEGES.Attributes = NativeMethods.SE_PRIVILEGE_ENABLED;
            TOKEN_PRIVILEGES.Luid = locallyUniqueIdentifier;

            if (!NativeMethods.AdjustTokenPrivileges(Token.DangerousGetHandle(), false, ref TOKEN_PRIVILEGES, 1024, IntPtr.Zero, IntPtr.Zero))
                if (throwOnFailure)
                    throw new Win32Exception();

            return this;
        }

        public TokenManager SetSessionId(int sessionId)
        {
            int size = Marshal.SizeOf<Int32>();
            IntPtr pValue = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.WriteInt32(pValue, sessionId);

                if (!SetTokenInformation(
                    Token.DangerousGetHandle(), TOKEN_INFORMATION_CLASS.TokenSessionId,
                    pValue, (uint) size))
                    throw new Win32Exception();

                return this;
            }
            finally
            {
                Marshal.FreeHGlobal(pValue);
            }
        }

        public int GetSessionId()
        {
            int size = Marshal.SizeOf<Int32>();
            IntPtr pValue = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(
                    Token.DangerousGetHandle(),
                    TOKEN_INFORMATION_CLASS.TokenSessionId, pValue, size, out size))
                throw new Win32Exception();

                return Marshal.ReadInt32(pValue);
            }
            finally
            {
                Marshal.FreeHGlobal(pValue);
            }
        }


        public void Dispose()
        {
            Token?.Close();
        }
    }
}
