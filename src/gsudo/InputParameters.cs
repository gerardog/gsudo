using gsudo.Helpers;
using gsudo.AppSettings;

namespace gsudo
{
    public static class InputArguments
    {
        // Show debug info
        public const bool Debug = false;

        // Open in new window
        public const bool NewWindow = false;
        
        // When elevating a command, keep the elevated shell open afterwards.
        public const bool KeepShellOpen = false;
        public const bool KeepWindowOpen = false;
        public const bool CloseNewWindow = true;

        // Wait for new process to end
        public const bool Wait = true;

        // In `gsudo --global config Key Value` --global means save as machine setting. 
        public const bool Global = false;

        // Kill credentials cache after running.
        public static bool KillCache { get; internal set; }

        // Skip shell detection and asume called from CMD.
        public static bool Direct { get; internal set; }

        // Target Integrity Level
        public const IntegrityLevel IntegrityLevel = gsudo.IntegrityLevel.High;

        // Elevate as "NT Authority\System" 
        public const bool RunAsSystem = false;

        // Elevate as "NT Authority\System" but member of "NT SERVICE\TrustedInstaller" group (run whoami /groups)
        public const bool TrustedInstaller = false;

        // User to Impersonate
        public const string UserName = null;
        // SID of User to Impersonate
        public const string UserSid = null;

        public static IntegrityLevel GetIntegrityLevel() => IntegrityLevel;

        internal static void Clear() // added for tests repeatability
        {
            // Wait = false;
            // RunAsSystem = false;
            // Global = false;
            KillCache = false;
            Direct = false; 
            // TrustedInstaller = false;
            // IntegrityLevel = null;
            // UserName = null;
            // UserSid = null;
        }

        internal static void SetUserName(string username)
        {
            // UserName = LoginHelper.ValidateUserName(username);
            // UserSid = LoginHelper.GetSidFromUserName(UserName);
        }
    }
}
