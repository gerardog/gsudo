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
        public async static Task ConsumeOutput(this StreamReader reader, Func<string, Task> callback)
        {
            // important: buffer must be larger than Process class/Win32Api internal buffer, or else the output is truncated on 1% of the non-interactive elevations I.E. gsudo dir C:\ > output.txt 
            char[] buffer = new char[10240];
            int cch;

            try
            {
                while ((cch = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await callback(new string(buffer, 0, cch));
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            catch (Exception ex) { Logger.Instance.Log(ex.ToString(), LogLevel.Error); }
            finally
            {
                reader.Dispose();
            }
        }

        public static async Task WriteAsync(this Stream stream, string text)
        {
            var bytes = GlobalSettings.Encoding.GetBytes(text);
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
    }
}
