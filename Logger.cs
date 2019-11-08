using System;

namespace gsudo
{
    public enum LogLevel
    {
        All = 0,
        Debug = 1,
        Info = 2,
        Error = 3,
        None = 4
    }
    class Logger
    {
        public void Log(string message, LogLevel level)
        {
            if (level >= Settings.LogLevel)
            {
                Console.ForegroundColor = GetColor(level);
                Console.WriteLine($"{level.ToString()}: {message}");
                Console.ResetColor();
            }
        }

        private ConsoleColor GetColor(LogLevel level)
        {
            if (level <= LogLevel.Debug) return ConsoleColor.DarkGray;
            if (level == LogLevel.Info) return ConsoleColor.Gray;
            return ConsoleColor.Red;    
        }
    }
}
