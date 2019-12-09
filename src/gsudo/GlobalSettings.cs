using System;
using System.Text;

namespace gsudo
{
    class GlobalSettings
    {
        public static bool SharedService { get; set; } = true;
        public static TimeSpan ServerTimeout { get; set; } = TimeSpan.FromSeconds(300);
        public static int BufferSize { get; set; } = 1024;

#if DEBUG
        public static LogLevel LogLevel { get; set; } = LogLevel.All;
#else
        public static LogLevel LogLevel { get; set; } = LogLevel.Info;
#endif
        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.UTF8;
//        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.Unicode;

        public static bool Debug { get; internal set; }
        public static bool NewWindow { get; internal set; }
        public static bool Wait { get; internal set; }
        public static bool PreferRawConsole { get; internal set; }
        public static bool PreferVTConsole { get; internal set; }
    }
}
