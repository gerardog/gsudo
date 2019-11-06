using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo
{
    static class ExtensionMethods
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    await task;  // Very important in order to propagate exceptions
                }
                else
                {
//                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }


        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }

        public async static Task ConsumeOutput(this StreamReader reader, Func<string, Task> callback)
        {
            char[] buffer = new char[256];
            int cch;

            while ((cch = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await callback(new string(buffer, 0, cch));
            }
        }

        public static Task WriteAsync(this Stream stream, byte[] bytes)
        {
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }

        public static async Task WriteAsync(this Stream stream, string text)
        {
            await stream.WriteAsync(ProcessHost.MyEncoding.GetBytes(text));
            await stream.FlushAsync();
        }

    }
}
