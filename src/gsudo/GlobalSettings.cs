using System;
using System.Text;

namespace gsudo
{
    class GlobalSettings
    {
        public static RegistrySetting<TimeSpan> CredentialsCacheDuration { get; set; } = new RegistrySetting<TimeSpan>("CredentialsCacheDuration", TimeSpan.FromSeconds(300));
        public static RegistrySetting<string> RootPrompt { get; set; } = new RegistrySetting<string>("RootPrompt", "$P# ");
        public static int BufferSize { get; set; } = 1024;

#if DEBUG
        public static RegistrySetting<LogLevel> LogLevel { get; set; } = new RegistrySetting<LogLevel>("LogLevel", gsudo.LogLevel.All);
#else
        public static RegistrySetting<LogLevel> LogLevel { get; set; } = new RegistrySetting<LogLevel>("LogLevel", gsudo.LogLevel.Info);
#endif
        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.UTF8;
//        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.Unicode;

        public static bool Debug { get; internal set; }
        public static bool NewWindow { get; internal set; }
        public static bool Wait { get; internal set; }
        public static RegistrySetting<bool> ForceRawConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceRawConsole), false);
        public static RegistrySetting<bool> ForceVTConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceVTConsole), false);

    }
}
