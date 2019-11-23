using System;
using System.Text;

namespace gsudo
{
    class Globals
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

        // All tokens must have small amount of chars, to avoid the token being split by the network chunking
        internal const string TOKEN_FOCUS = "\u0011"; 
        internal const string TOKEN_EXITCODE = "\u0012";
        internal const string TOKEN_ERROR = "\u0013";
        internal const string TOKEN_KEY_CTRLC = "\u0014";
        internal const string TOKEN_KEY_CTRLBREAK = "\u0015";

        internal const int GSUDO_ERROR_EXITCODE = 999;

        public static Logger Logger {get;} = new Logger();
        public static bool Debug { get; internal set; }
        public static bool NewWindow { get; internal set; }
        public static bool Wait { get; internal set; }
    }
}
