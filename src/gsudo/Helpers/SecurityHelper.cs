using gsudo.Native;
using System;
using System.Linq;
using System.Security.Principal;

namespace gsudo.Helpers
{
    internal static class SecurityHelper
    {
        public static bool IsMemberOfLocalAdmins()
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            var claims = principal.Claims;
            return claims.Any(c => c.Value == "S-1-5-32-544");
        }

        private static int? _cacheGetCurrentIntegrityLevelCache;
        public static bool IsHighIntegrity()
        {
            return GetCurrentIntegrityLevel() >= (int)IntegrityLevel.High;
        }

        /// <summary>
        /// The function gets the integrity level of the current process.
        /// </summary>
        /// <returns>
        /// Returns the integrity level of the current process. It is usually one of
        /// these values:
        ///
        ///    SECURITY_MANDATORY_UNTRUSTED_RID - means untrusted level
        ///    SECURITY_MANDATORY_LOW_RID - means low integrity level.
        ///    SECURITY_MANDATORY_MEDIUM_RID - means medium integrity level.
        ///    SECURITY_MANDATORY_HIGH_RID - means high integrity level.
        ///    SECURITY_MANDATORY_SYSTEM_RID - means system integrity level.
        ///
        /// </returns>
        /// <exception cref="System.ComponentModel.Win32Exception">
        /// When any native Windows API call fails, the function throws a Win32Exception
        /// with the last error code.
        /// </exception>
        static internal int GetCurrentIntegrityLevel()
        {
            if (_cacheGetCurrentIntegrityLevelCache.HasValue) return _cacheGetCurrentIntegrityLevelCache.Value;
            _cacheGetCurrentIntegrityLevelCache = ProcessHelper.GetProcessIntegrityLevel(ProcessApi.GetCurrentProcess());

            return _cacheGetCurrentIntegrityLevelCache.Value;
        }

        private static bool? _cacheIsAdmin;
        public static bool IsAdministrator()
        {
            if (_cacheIsAdmin.HasValue) return _cacheIsAdmin.Value;

            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                _cacheIsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                return _cacheIsAdmin.Value;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
