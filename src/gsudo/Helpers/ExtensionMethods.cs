using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo
{
    static class ExtensionMethods
    {
        private static readonly TaskFactory _taskFactory = new TaskFactory();

        public static Task ConsumeOutput(this StreamReader reader, Func<string, Task> callback)
        {
            return _taskFactory.StartNew(async () =>
            {
                // important for ProcessStart buffers: buffer must be larger than Process Win32Api internal buffer, or else the output is truncated on 1% of the non-interactive elevations I.E. gsudo dir C:\ > output.txt 
                char[] buffer = new char[10240];
                int cch;

                try
                {
                    while ((cch = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        await callback(new string(buffer, 0, cch)).ConfigureAwait(false);
                    }
                }
                catch (ObjectDisposedException) { }
                catch (IOException) { }
                catch (Exception ex) { Logger.Instance.Log(ex.ToString(), LogLevel.Error); }
                finally
                {
                    reader.Dispose();
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static async Task WriteAsync(this Stream stream, string text)
        {
            var bytes = Settings.Encoding.GetBytes(text);
            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }
        
        public static bool NotIn(this string toSearch, params string[] list)
        {
            return !In(toSearch, list);
        }

        public static bool In(this string toSearch, params string[] list)
        {
            return list.Contains(toSearch, StringComparer.OrdinalIgnoreCase);
        }

        public static bool In<T>(this T toSearch, params T[] list)
        {
            return list.Contains(toSearch);
        }

        public static void AddMany<T>(this List<T> list, params T[] items)
        {
            list.AddRange(items);
        }

        public static T ParseEnum<T>(string inString, bool ignoreCase = true) where T : struct
        {
            if (!Enum.TryParse<T>(inString, ignoreCase, out var returnEnum))
                throw new ApplicationException($"\"{inString}\" is not a valid {typeof(T).Name}");
            return returnEnum;
        }
    }
}
