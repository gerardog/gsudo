using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using gsudo.AppSettings;
using gsudo.CredentialsCache;
using Microsoft.Win32;

namespace gsudo
{
    class Settings
    {
        public const string DefaultAnsiPrompt = "$p$e[1;31m# $e[0m";
        public const string DefaultAsciiPrompt = "$P# ";

        public const int BufferSize = 10240;
        public static readonly Encoding Encoding = new System.Text.UTF8Encoding(false);
        public static RegistrySetting<CacheMode> CacheMode { get; set; }
            = new RegistrySetting<CacheMode>(nameof(CacheMode), CredentialsCache.CacheMode.Explicit,
                deserializer: ExtensionMethods.ParseEnum< CacheMode>, 
                scope: RegistrySettingScope.GlobalOnly);

        public static RegistrySetting<TimeSpan> CacheDuration { get; }
            = new RegistrySetting<TimeSpan>(nameof(CacheDuration), 
                defaultValue: TimeSpan.FromSeconds(300),
                scope: RegistrySettingScope.GlobalOnly,
                deserializer: TimeSpanParseWithInfinite,
                serializer: TimeSpanWithInfiniteToString
                );

        public static RegistrySetting<string> PipedPrompt { get; }
            = new RegistrySetting<string>(nameof(PipedPrompt), 
                defaultValue: DefaultAsciiPrompt, 
                deserializer: (s) => s
                );

        public static RegistrySetting<string> Prompt { get; }
            = new RegistrySetting<string>(nameof(Prompt),
                defaultValue: GetPromptDefaultValue, 
                deserializer: (s) => s);

        public static RegistrySetting<LogLevel> LogLevel { get; }
            = new RegistrySetting<LogLevel>(nameof(LogLevel), 
                defaultValue: gsudo.LogLevel.Info, 
                deserializer: ExtensionMethods.ParseEnum<LogLevel>);

        public static RegistrySetting<bool> ForcePipedConsole { get; }
            = new RegistrySetting<bool>(nameof(ForcePipedConsole), 
                defaultValue: false, 
                deserializer: bool.Parse);

        public static RegistrySetting<bool> ForceAttachedConsole { get; }
            = new RegistrySetting<bool>(nameof(ForceAttachedConsole), 
                defaultValue: false, 
                deserializer: bool.Parse);

        public static RegistrySetting<bool> ForceVTConsole { get; }
            = new RegistrySetting<bool>(nameof(ForceVTConsole), 
                defaultValue: false, 
                deserializer: bool.Parse);

        public static RegistrySetting<bool> CopyEnvironmentVariables { get; }
            = new RegistrySetting<bool>(nameof(CopyEnvironmentVariables), 
                defaultValue: false, 
                deserializer: bool.Parse);

        public static RegistrySetting<bool> CopyNetworkShares { get; } =
            new RegistrySetting<bool>(nameof(CopyNetworkShares), 
                defaultValue: false, 
                deserializer: bool.Parse);

        public static RegistrySetting<bool> PowerShellLoadProfile { get; } =
            new RegistrySetting<bool>(nameof(PowerShellLoadProfile), 
                defaultValue: false, 
                bool.Parse);

        public static RegistrySetting<bool> SecurityEnforceUacIsolation { get; } =
            new RegistrySetting<bool>(nameof(SecurityEnforceUacIsolation), 
                defaultValue: false, 
                deserializer: bool.Parse,
                scope: RegistrySettingScope.GlobalOnly);

        public static RegistrySetting<string> ExceptionList { get; } =
            new RegistrySetting<string>(nameof(ExceptionList),
                defaultValue: "notepad.exe;powershell.exe;whoami.exe;",
                deserializer: (string s)=>s,
                scope: RegistrySettingScope.GlobalOnly);

        public static RegistrySetting<bool> NewWindow_Force { get; } =
            new RegistrySetting<bool>(nameof(NewWindow_Force),
                defaultValue: false,
                deserializer: bool.Parse,
                scope: RegistrySettingScope.Any);

        public static RegistrySetting<CloseBehaviour> NewWindow_CloseBehaviour { get; } =
            new RegistrySetting<CloseBehaviour>(nameof(NewWindow_CloseBehaviour),
                defaultValue: CloseBehaviour.OsDefault,
                deserializer: ExtensionMethods.ParseEnum<CloseBehaviour>,
                scope: RegistrySettingScope.Any);

        public static IDictionary<string, RegistrySetting> AllKeys =>
            new Dictionary<string, RegistrySetting>(StringComparer.OrdinalIgnoreCase)
                .Add(
                    CacheMode,
                    CacheDuration,
                    LogLevel,

                    NewWindow_Force,
                    NewWindow_CloseBehaviour,

                    Prompt,
                    PipedPrompt,

                    ForceAttachedConsole,
                    ForcePipedConsole,
                    ForceVTConsole,

                    CopyEnvironmentVariables,
                    CopyNetworkShares,

                    PowerShellLoadProfile,
                    SecurityEnforceUacIsolation,
                    ExceptionList
                );

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

        internal static string GetPromptDefaultValue()
        {
            const string REGKEY = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            const string Name = @"CurrentMajorVersionNumber";

            using (var subkey = Registry.LocalMachine.OpenSubKey(REGKEY, false))
            {
                if (subkey != null)
                {
                    var currentValue = subkey.GetValue(Name, null);
                    if (currentValue != null) // Key introduced in Windows 10 or later.
                        return DefaultAnsiPrompt;
                }
            }
            
            return DefaultAsciiPrompt;
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