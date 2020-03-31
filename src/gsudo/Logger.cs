using System;
using System.Diagnostics;
using System.IO;

namespace gsudo
{
    public enum LogLevel
    {
        All = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        None = 5,
    }

    class Logger
    {
        public static int ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
        public static readonly Logger Instance = new Logger();

        private Logger() { }

        public void Log(string message, LogLevel level)
        {
            try
            {
                if (level >= Settings.LogLevel)
                {
                    Console.ForegroundColor = GetColor(level);
                    Console.Error.WriteLine($"{level.ToString()}: {message}");
                    Console.ResetColor();
                }

                //File.AppendAllText("C:\\test\\gsudolog.txt", $"{ProcessId}\t{level.ToString()}: {message}\r\n");
            }
            catch { }
        }

        private static ConsoleColor GetColor(LogLevel level)
        {
            if (level <= LogLevel.Debug) return ConsoleColor.DarkGray;
            if (level == LogLevel.Info) return ConsoleColor.Gray;
            if (level == LogLevel.Warning) return ConsoleColor.Yellow;
            return ConsoleColor.Red;    
        }
    }
}
