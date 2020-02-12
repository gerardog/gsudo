using System;
using System.Collections.Generic;
using System.Text;

namespace gsudo
{
    class Settings
    {
        public static RegistrySetting<TimeSpan> CredentialsCacheDuration { get; set; } = new RegistrySetting<TimeSpan>("CredentialsCacheDuration", TimeSpan.FromSeconds(300), TimeSpan.Parse, RegistrySettingScope.GlobalOnly);
        public static RegistrySetting<string> RawPrompt { get; set; } = new RegistrySetting<string>("RawPrompt", "$P# ", (s) => s);
        public static RegistrySetting<string> Prompt { get; set; } = new RegistrySetting<string>("Prompt", "$p$e[1;31;40m# $e[0;37;40m", (s) => s);
        public const int BufferSize = 10240;
        public static RegistrySetting<LogLevel> LogLevel { get; set; } = new RegistrySetting<LogLevel>("LogLevel", gsudo.LogLevel.Info, (s) => (LogLevel)Enum.Parse(typeof(LogLevel), s, true));

        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.UTF8;

        public static RegistrySetting<bool> ForceRawConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceRawConsole), false, bool.Parse);
        public static RegistrySetting<bool> ForceVTConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceVTConsole), false, bool.Parse);
        public static RegistrySetting<bool> CopyEnvironmentVariables { get; internal set; } = new RegistrySetting<bool>(nameof(CopyEnvironmentVariables), false, bool.Parse);
        public static RegistrySetting<bool> CopyNetworkShares { get; internal set; } = new RegistrySetting<bool>(nameof(CopyNetworkShares), false, bool.Parse);
        public static RegistrySetting<bool> SecurityEnforceUacIsolation { get; internal set; } = new RegistrySetting<bool>(nameof(SecurityEnforceUacIsolation), false, bool.Parse, RegistrySettingScope.GlobalOnly);

        public static IDictionary<string, RegistrySetting> AllKeys => new Dictionary<string, RegistrySetting>(StringComparer.OrdinalIgnoreCase)
            .Add(
                CredentialsCacheDuration,
                LogLevel,
                Prompt,
                RawPrompt,
                ForceRawConsole,
                ForceVTConsole,
                CopyEnvironmentVariables,
                CopyNetworkShares,
                SecurityEnforceUacIsolation);

        public static bool KillCache { get; internal set; }
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
