using System;
using System.Security.Principal;

namespace gsudo.Helpers
{
    internal static class LoginHelper
    {
        internal static string UserNameToSid(string userName)
        {
            try
            {
                return new NTAccount(userName).Translate(typeof(SecurityIdentifier)).Translate(typeof(NTAccount)).Value;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Value \"{userName}\" is not a valid Username.", ex );
            }
        }

    }
}
