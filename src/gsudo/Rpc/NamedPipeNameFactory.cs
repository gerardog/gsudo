using gsudo.Helpers;
using System;
using System.Globalization;
using System.Security.Principal;
using System.Text;

namespace gsudo.Rpc
{
    static class NamedPipeNameFactory
    {
        public static string GetPipeName(string allowedSid, int allowedPid, string targetSid, bool isAdmin)
        {
            if (allowedPid < 0) allowedPid = 0;

            var ti = InputArguments.TrustedInstaller ? "_TI" : string.Empty;
            var s = InputArguments.RunAsSystem ? "_S" : string.Empty;
            var admin = !isAdmin ? "_NonAdmin" : string.Empty;

            var data = $"allowedSid-{allowedSid}_targetSid-{targetSid}{allowedPid}{s}{ti}{admin}";
#if !DEBUG
            data = GetHash(data);
#endif
            return $"{GetPipePrefix(isAdmin)}_{data}";
        }

        private static string GetHash(string data)
        {
            using (var hashingAlg = System.Security.Cryptography.SHA256.Create())
            {
                var hash = hashingAlg.ComputeHash(UTF8Encoding.UTF8.GetBytes(data));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("X2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        private static string GetPipePrefix(bool isAdmin)
        {
            const string PROTECTED = "ProtectedPrefix\\Administrators\\gsudo";
            const string REGULAR = "gsudo";
            if (isAdmin)
                return PROTECTED;
            else
                return REGULAR;
        }    
    }
}
