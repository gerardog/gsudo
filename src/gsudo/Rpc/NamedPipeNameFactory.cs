using System.Globalization;
using System.Text;

namespace gsudo.Rpc
{
    static class NamedPipeNameFactory
    {
        public static string GetPipeName(string connectingUser, int connectingPid)
        {
            if (connectingPid < 0) connectingPid = 0;
            string integrity = InputArguments.GetIntegrityLevel().ToString();
            var data = $"{connectingUser}_{connectingPid}";
#if !DEBUG
            data = GetHash(data);
#endif
            return $"{GetPipePrefix()}_{data}";
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

        private static string GetPipePrefix()
        {
//            if ((InputArguments.GetIntegrityLevel()) >= IntegrityLevel.High)
                return "ProtectedPrefix\\Administrators\\gsudo";
//            else
//                return "gsudo";
        }
    }
}
