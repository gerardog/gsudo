using System;
using System.Text;

namespace gsudo
{
    class Settings
    {
        public static string PipeName => $"gsudo_{System.Security.Principal.WindowsIdentity.GetCurrent().User.Value}";
        public static bool SharedService { get; set; } = true;
        public static TimeSpan ServerTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public static int BufferSize { get; set; } = 1024;
        public static LogLevel LogLevel { get; set; } = LogLevel.All;

        public static readonly Encoding Encoding = System.Text.UnicodeEncoding.UTF8;
        internal const string TOKEN_EXITCODE = "<GSUDO-EXITCODE>";
        internal const string TOKEN_ERROR = "<GSUDOERR>";
        
        public static Logger Logger {get;} = new Logger();
    }
}
