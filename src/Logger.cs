using System;

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
        public void Log(string message, LogLevel level)
        {
            if (level >= Globals.LogLevel)
            {
                Console.ForegroundColor = GetColor(level);
                Console.Error.WriteLine($"{level.ToString()}: {message}");
                Console.ResetColor();
            }
        }

        private ConsoleColor GetColor(LogLevel level)
        {
            if (level <= LogLevel.Debug) return ConsoleColor.DarkGray;
            if (level == LogLevel.Info) return ConsoleColor.Gray;
            if (level == LogLevel.Warning) return ConsoleColor.Yellow;
            return ConsoleColor.Red;    
        }
    }
}
