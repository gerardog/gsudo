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
            char[] buffer = new char[256];
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
            catch (Exception ex) { Globals.Logger.Log(ex.ToString(), LogLevel.Error); }
            finally
            {
                reader.Dispose();
                reader.BaseStream?.Dispose();
            }
        }

        public static async Task WriteAsync(this Stream stream, string text)
        {
            try
            {
                var bytes = Globals.Encoding.GetBytes(text);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            catch (Exception ex) { Globals.Logger.Log(ex.ToString(), LogLevel.Error); }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(IntPtr Pipe, out uint ClientProcessId);
        public static int GetClientProcessId(this System.IO.Pipes.NamedPipeServerStream pipeServer)
        {
            UInt32 nProcID;
            IntPtr hPipe = pipeServer.SafePipeHandle.DangerousGetHandle();
            if (GetNamedPipeClientProcessId(hPipe, out nProcID))
                return (int)nProcID;
            return 0;
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
