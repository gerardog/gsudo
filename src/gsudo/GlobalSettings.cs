using System;
using System.Collections.Generic;
using System.Text;

namespace gsudo
{
    class GlobalSettings
    {
        public static RegistrySetting<TimeSpan> CredentialsCacheDuration { get; set; } = new RegistrySetting<TimeSpan>("CredentialsCacheDuration", TimeSpan.FromSeconds(300), TimeSpan.Parse);
        public static RegistrySetting<string> RawPrompt { get; set; } = new RegistrySetting<string>("RawPrompt", "$P# ", (s) => s);
        public static RegistrySetting<string> Prompt { get; set; } = new RegistrySetting<string>("Prompt", "$p$e[1;31;40m# $e[0;37;40m", (s) => s);
        public static int BufferSize { get; set; } = 1024;
        public static RegistrySetting<LogLevel> LogLevel { get; set; } = new RegistrySetting<LogLevel>("LogLevel", gsudo.LogLevel.Info, (s) => (LogLevel)Enum.Parse(typeof(LogLevel), s, true));

        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.UTF8;

        public static bool Debug { get; internal set; }
        public static bool NewWindow { get; internal set; }
        public static bool Wait { get; internal set; }
        public static bool RunAsSystem { get; internal set; }

        public static RegistrySetting<bool> ForceRawConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceRawConsole), false, bool.Parse);
        public static RegistrySetting<bool> ForceVTConsole { get; internal set; } = new RegistrySetting<bool>(nameof(ForceVTConsole), false, bool.Parse);
        public static RegistrySetting<bool> CopyEnvironmentVariables { get; internal set; } = new RegistrySetting<bool>(nameof(CopyEnvironmentVariables), false, bool.Parse);
        public static RegistrySetting<bool> CopyNetworkShares { get; internal set; } = new RegistrySetting<bool>(nameof(CopyNetworkShares), false, bool.Parse);
        public static RegistrySetting<string> PowerShellArguments { get; set; } = new RegistrySetting<string>(nameof(PowerShellArguments), "-NoProfile", (s) => s);
        public static RegistrySetting<string> PowerShellCore6Arguments { get; set; } = new RegistrySetting<string>(nameof(PowerShellCore6Arguments), "-NoProfile -Command", (s) => s);
        public static RegistrySetting<string> PowerShellCore7Arguments { get; set; } = new RegistrySetting<string>(nameof(PowerShellCore7Arguments), "-NoProfile -Command", (s) => s);
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
                PowerShellArguments,
                PowerShellCore6Arguments,
                PowerShellCore7Arguments);
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
