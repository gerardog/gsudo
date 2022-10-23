using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo
{
    static class ExtensionMethods
    {
        private static readonly TaskFactory _taskFactory = new TaskFactory();

        public static Task ConsumeOutput(this StreamReader reader, Func<string, Task> callback, Func<Task> eofCallback = null)
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

                    await (eofCallback?.Invoke() ?? Task.CompletedTask).ConfigureAwait(false);
                }
                catch (InvalidOperationException) { }
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

        public static bool In(this string toSearch, string list)
        {
            return list.Equals(toSearch, StringComparison.OrdinalIgnoreCase);
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

        public static TEnum ParseEnum<TEnum>(string inString) where TEnum : struct
         => ParseEnum<TEnum>(inString, ignoreCase: true);

        public static TEnum ParseEnum<TEnum>(string inString, bool ignoreCase = true) where TEnum : struct
        {
            if (Enum.TryParse<TEnum>(inString, true, out var result))
                return result;

            throw new ApplicationException($"\"{inString}\" is not a valid {typeof(TEnum).Name}. Valid values are: {String.Join(", ", Enum.GetNames(typeof(TEnum)))}");
        }

        static public string ReplaceOrdinal(this string original, string pattern, string replacement)
        {
            return original.Replace(pattern, replacement, StringComparison.Ordinal);
        }

#if NETFRAMEWORK
        static public string Replace(this string original, string pattern, string replacement, StringComparison comparisonType, int stringBuilderInitialSize = -1)
        {
            if (original == null)
            {
                return null;
            }

            if (String.IsNullOrEmpty(pattern))
            {
                return original;
            }


            int posCurrent = 0;
            int lenPattern = pattern.Length;
            int idxNext = original.IndexOf(pattern, comparisonType);
            StringBuilder result = new StringBuilder(stringBuilderInitialSize < 0 ? Math.Min(4096, original.Length) : stringBuilderInitialSize);

            while (idxNext >= 0)
            {
                result.Append(original, posCurrent, idxNext - posCurrent);
                result.Append(replacement);

                posCurrent = idxNext + lenPattern;

                idxNext = original.IndexOf(pattern, posCurrent, comparisonType);
            }

            result.Append(original, posCurrent, original.Length - posCurrent);

            return result.ToString();
        }

        public static bool Contains(this string source, char pattern, StringComparison comparisonType)
        {
            return source.IndexOf(pattern.ToString(), comparisonType) >= 0;
        }
#endif
    }
}
