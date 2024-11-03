using gsudo.Helpers;
using gsudo.AppSettings;

namespace gsudo
{
    public static class InputArguments
    {
        // Show debug info
        public const bool Debug = false;

        // Open in new window
        public static bool NewWindow { get; internal set; }
        
        // When elevating a command, keep the elevated shell open afterwards.
        public static bool KeepShellOpen { get; internal set; }
        public static bool KeepWindowOpen { get; internal set; }
        public static bool CloseNewWindow { get; internal set; }

        // Wait for new process to end
        public static bool Wait { get; internal set; }

        // In `gsudo --global config Key Value` --global means save as machine setting. 
        public const bool Global = false;

        // Kill credentials cache after running.
        public static bool KillCache { get; internal set; }

        // Skip shell detection and asume called from CMD.
        public static bool Direct { get; internal set; }

        // Target Integrity Level
        public const IntegrityLevel IntegrityLevel = gsudo.IntegrityLevel.High;
        // public static IntegrityLevel IntegrityLevel { get; internal set; } = gsudo.IntegrityLevel.High;

        // Elevate as "NT Authority\System" 
        public const bool RunAsSystem = false;

        // Elevate as "NT Authority\System" but member of "NT SERVICE\TrustedInstaller" group (run whoami /groups)
        public const bool TrustedInstaller = false;

        // User to Impersonate
        public const string UserName = null;
        // SID of User to Impersonate
        public const string UserSid = null;

        // public static IntegrityLevel GetIntegrityLevel() => (RunAsSystem ? gsudo.IntegrityLevel.System : IntegrityLevel ?? gsudo.IntegrityLevel.High);

        internal static void Clear() // added for tests repeatability
        {
            NewWindow = false;
            Wait = false;
            KillCache = false;
            Direct = false;
            // IntegrityLevel = IntegrityLevel.High;
        }
    }
}
