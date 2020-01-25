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

    internal static class Logger
    {
        public static void Log(string message, LogLevel level)
        {
            try
            {
                if (level < GlobalSettings.LogLevel) return;
                Console.ForegroundColor = GetColor(level);
                Console.Error.WriteLine($"{level.ToString()}: {message}");
                Console.ResetColor();
            }
            catch { }
        }

        private static ConsoleColor GetColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Info => ConsoleColor.Gray,
                LogLevel.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.Red
            };
        }
    }
}
