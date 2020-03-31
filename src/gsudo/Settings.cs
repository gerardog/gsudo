using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using gsudo.Enums;

namespace gsudo
{
    class Settings
    {
        public const int BufferSize = 10240;
        public static readonly Encoding Encoding = System.Text.Encoding.UTF8;
        
        public static RegistrySetting<CacheMode> CacheMode { get; set; }
            = new RegistrySetting<CacheMode>(nameof(CacheMode), Enums.CacheMode.Explicit,
                (s) => (CacheMode) Enum.Parse(typeof(CacheMode), s, true), RegistrySettingScope.GlobalOnly);

        public static RegistrySetting<TimeSpan> CacheDuration { get; set; }
            = new RegistrySetting<TimeSpan>(nameof(CacheDuration), TimeSpan.FromSeconds(300), TimeSpanParseWithInfinite,
                RegistrySettingScope.GlobalOnly, TimeSpanWithInfiniteToString);

        public static RegistrySetting<string> PipedPrompt { get; set; }
            = new RegistrySetting<string>(nameof(PipedPrompt), "$P# ", (s) => s);

        public static RegistrySetting<string> Prompt { get; set; }
            = new RegistrySetting<string>(nameof(Prompt), "$p$e[1;31;40m# $e[0;37;40m", (s) => s);

        public static RegistrySetting<LogLevel> LogLevel { get; set; }
            = new RegistrySetting<LogLevel>(nameof(LogLevel), gsudo.LogLevel.Info,
                (s) => (LogLevel) Enum.Parse(typeof(LogLevel), s, true));

        public static RegistrySetting<bool> ForcePipedConsole { get; set; }
            = new RegistrySetting<bool>(nameof(ForcePipedConsole), false, bool.Parse);

        public static RegistrySetting<bool> ForceAttachedConsole { get; internal set; }
            = new RegistrySetting<bool>(nameof(ForceAttachedConsole), false, bool.Parse);

        public static RegistrySetting<bool> ForceVTConsole { get; internal set; }
            = new RegistrySetting<bool>(nameof(ForceVTConsole), false, bool.Parse);

        public static RegistrySetting<bool> CopyEnvironmentVariables { get; internal set; }
            = new RegistrySetting<bool>(nameof(CopyEnvironmentVariables), false, bool.Parse);

        public static RegistrySetting<bool> CopyNetworkShares { get; internal set; } =
            new RegistrySetting<bool>(nameof(CopyNetworkShares), false, bool.Parse);

        public static RegistrySetting<bool> SecurityEnforceUacIsolation { get; internal set; } =
            new RegistrySetting<bool>(nameof(SecurityEnforceUacIsolation), false, bool.Parse,
                RegistrySettingScope.GlobalOnly);

        public static IDictionary<string, RegistrySetting> AllKeys =>
            new Dictionary<string, RegistrySetting>(StringComparer.OrdinalIgnoreCase)
                .Add(
                    CacheMode,
                    CacheDuration,
                    LogLevel,
                    Prompt,
                    PipedPrompt,
                    ForceAttachedConsole,
                    ForcePipedConsole,
                    ForceVTConsole,
                    CopyEnvironmentVariables,
                    CopyNetworkShares,
                    SecurityEnforceUacIsolation);

        internal static TimeSpan TimeSpanParseWithInfinite(string value)
        {
            if (value.In("-1", "Infinite"))
                return TimeSpan.MaxValue;
            else
                return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        }

        internal static string TimeSpanWithInfiniteToString(TimeSpan value)
        {
            if (value == TimeSpan.MaxValue)
                return "Infinite";
            else
                return value.ToString();
        }
    }

    static class Extension
    {
        public static IDictionary<string, RegistrySetting> Add(this IDictionary<string, RegistrySetting> dict,
            params RegistrySetting[] settings)
        {
            foreach (var s in settings)
                dict.Add(s.Name, s);
            return dict;
        }
    }
}