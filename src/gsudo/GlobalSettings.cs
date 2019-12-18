using System;
using System.Collections.Generic;
using System.Text;

namespace gsudo
{
    class GlobalSettings
    {
        public static RegistrySetting<TimeSpan> CredentialsCacheDuration { get; set; } = new RegistrySetting<TimeSpan>("CredentialsCacheDuration", TimeSpan.FromSeconds(300), TimeSpan.Parse);
        public static RegistrySetting<string> Prompt { get; set; } = new RegistrySetting<string>("Prompt", "$P# ", (s) => s);
        public static RegistrySetting<string> VTPrompt { get; set; } = new RegistrySetting<string>("VTPrompt", "$p$e[1;31;40m# $e[0;37;40m", (s) => s);
        public static int BufferSize { get; set; } = 1024;
        public static RegistrySetting<LogLevel> LogLevel { get; set; } = new RegistrySetting<LogLevel>("LogLevel", gsudo.LogLevel.Info, (s) => (LogLevel)Enum.Parse(typeof(LogLevel), s, true));

        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.UTF8;

        public static bool Debug { get; internal set; }
        public static bool NewWindow { get; internal set; }
        public static bool Wait { get; internal set; }
        public static RegistrySetting<bool> ForceRawConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceRawConsole), false, bool.Parse);
        public static RegistrySetting<bool> ForceVTConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceVTConsole), false, bool.Parse);

        public static IDictionary<string, RegistrySetting> AllKeys => new Dictionary<string, RegistrySetting>(StringComparer.OrdinalIgnoreCase)
            .Add(
                CredentialsCacheDuration,
                LogLevel,
                VTPrompt,
                Prompt,
                ForceRawConsole,
                ForceVTConsole);
    }

    static class Extension
    {
        public static IDictionary<string, RegistrySetting> Add(this IDictionary<string, RegistrySetting> dict, params RegistrySetting[] settings)
        {
            foreach (var s in settings)
                dict.Add(s.Name, s);
            return dict;
        }
    }
}
