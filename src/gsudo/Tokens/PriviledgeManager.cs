using System;
using System.ComponentModel;

namespace gsudo.Tokens
{
    //Enable a privilege by implementing the following line in your code:
    //Privileges.EnablePrivilege(SecurityEntity.SE_SHUTDOWN_NAME);

    //Needed code:
    public static class PrivilegesManager
    {
        public static void DisableAllPrivileges(IntPtr tokenHandle)
        {
            var TOKEN_PRIVILEGES = new NativeMethods.TOKEN_PRIVILEGES();
            
            if (!NativeMethods.AdjustTokenPrivileges(tokenHandle, true, ref TOKEN_PRIVILEGES, 1024, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception();
        }

    }

   
}