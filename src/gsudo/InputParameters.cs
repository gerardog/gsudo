using gsudo.Helpers;
using System;
using System.Security;

namespace gsudo
{
    public static class InputArguments
    {
        // Show debug info
        public static bool Debug { get; internal set; }

        // Open in new window
        public static bool NewWindow { get; internal set; }

        // Wait for new process to end
        public static bool Wait { get; internal set; }

        // Elevate as NT Authority\System
        public static bool RunAsSystem { get; internal set; }

        // In `gsudo --global config Key Value` --global means save as machine setting. 
        public static bool Global { get; internal set; }
        public static bool KillCache { get; internal set; }
        public static bool Direct { get; internal set; }
        public static IntegrityLevel? IntegrityLevel { get; internal set; }
        public static bool TrustedInstaller { get; internal set; }


        public static string UserName { get; private set; }
        public static string UserSid { get; private set; }

        public static IntegrityLevel GetIntegrityLevel() => (RunAsSystem ? gsudo.IntegrityLevel.System : IntegrityLevel ?? gsudo.IntegrityLevel.High);

        internal static void Clear() // added for tests repeatability
        {
            Debug = false;
            NewWindow = false;
            Wait = false;
            RunAsSystem = false; 
            Global = false;
            KillCache = false;
            Direct = false; 
            TrustedInstaller = false;
            IntegrityLevel = null;
            UserName = null;
            UserSid = null;
        }

        internal static void SetUserName(string username)
        {
            UserName = LoginHelper.ValidateUserName(username);
            UserSid = LoginHelper.GetSidFromUserName(UserName);
        }
    }
}
