using gsudo.Native;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace gsudo.Helpers
{
    public static class ArgumentsHelper
    {
        /// <summary>
        /// Splits arguments. Quoted segments remain joined and quotes preserved.
        /// </summary>
        /// <example>
        /// SplitArgs("\"my exe name\" \"my params\" OtherParam1") => new string[] { "\"my exe name\"", "\"my params\"", "OtherParam1"};
        /// </example>
        public static IList<string> SplitArgs(string args)
        {   
            args = args.Trim();
            var results = new List<string>();
            int pushed = 0;
            int curr = 0;
            bool insideQuotes = false;
            while (curr < args.Length)
            {
                if (args[curr] == '"')
                    insideQuotes = !insideQuotes;
                else if (args[curr] == ' ' && !insideQuotes)
                {
                    if ((curr - pushed) > 0)
                        results.Add(args.Substring(pushed, curr - pushed));
                    pushed = curr + 1;
                }
                curr++;
            }

            if (pushed < curr)
                results.Add(args.Substring(pushed, curr - pushed));
            return results;
        }

        internal static string GetRealCommandLine()
        {
            System.IntPtr ptr = ConsoleApi.GetCommandLine();
            string commandLine = Marshal.PtrToStringAuto(ptr).TrimStart();

            if (commandLine[0] == '"')
                return commandLine.Substring(commandLine.IndexOf('"', 1) + 1).TrimStart(' ');
            else if (commandLine.IndexOf(' ', 1) >= 0)
                return commandLine.Substring(commandLine.IndexOf(' ', 1) + 1).TrimStart(' ');
            else
                return string.Empty;
        }

        public static string UnQuote(this string v)
        {
            if (string.IsNullOrEmpty(v))
                return v;
            if (v[0] == '"' && v[v.Length - 1] == '"')
                return v.Substring(1, v.Length - 2);
            if (v[0] == '"' && v.Trim().EndsWith("\"", StringComparison.Ordinal))
                return UnQuote(v.Trim());
            if (v[0] == '"')
                return v.Substring(1);
            else
                return v;
        }

        public static string Quote(this string v)
        {
            return $"\"{v}\"";
        }
    }
}