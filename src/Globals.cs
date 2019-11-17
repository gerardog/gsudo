using System;
using System.Text;

namespace gsudo
{
    class Globals
    {
        public static bool SharedService { get; set; } = true;
        public static TimeSpan ServerTimeout { get; set; } = TimeSpan.FromSeconds(300);
        public static int BufferSize { get; set; } = 1024;
        public static LogLevel LogLevel { get; set; } = LogLevel.Debug;

        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.UTF8;
        internal const string TOKEN_EXITCODE = "<GSUDO-EXITCODE>";
        internal const string TOKEN_ERROR = "<GSUDOERR>";
        internal const string TOKEN_KEY_CTRLC = "<GSUDOCTRLC>";
        internal const string TOKEN_KEY_CTRLBREAK = "<GSUDOCTRLBREAK>";

        public static Logger Logger {get;} = new Logger();
    }
}
