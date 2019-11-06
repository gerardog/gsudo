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
        public static readonly Encoding Encoding = System.Text.UTF8Encoding.UTF8;
        internal const string EXITCODE_TOKEN = "<GSUDO-EXITCODE>";
    }
}
