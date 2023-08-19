using gsudo.Helpers;
using gsudo.Native;
using gsudo.Rpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class StatusCommand : ICommand
    {
        public bool AsJson { get; set; }
        public string Key { get; set; }
        public bool NoOutput { get; set; }

        public Task<int> Execute()
        {
            var status = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            status["CallerPid"] = ProcessHelper.GetCallerPid();

            var id = WindowsIdentity.GetCurrent();
            status["UserName"] = id.Name;
            status["UserSid"] = id.User.ToString();

            bool isAdmin = SecurityHelper.IsAdministrator();
            status["IsElevated"] = isAdmin;
            status["IsAdminMember"] = SecurityHelper.IsMemberOfLocalAdmins();

            var integrity = SecurityHelper.GetCurrentIntegrityLevel();
            var integrityString = string.Empty;

            if (Enum.IsDefined(typeof(IntegrityLevel), integrity))
                integrityString = ((IntegrityLevel)integrity).ToString();

            status["IntegrityLevelNumeric"] = integrity;
            status["IntegrityLevel"] = integrityString;
            status["CacheMode"] = Settings.CacheMode.Value.ToString();
            status["CacheAvailable"] = NamedPipeClient.IsServiceAvailable();

            //            ---------
            var pipes = NamedPipeUtils.ListNamedPipes();
            status["CacheSessionsCount"] = pipes.Count;
            status["CacheSessions"] = pipes.ToArray();

            status["IsRedirected"] = Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected;

            if (!string.IsNullOrEmpty(Key))
            {
                if (status.ContainsKey(Key))
                {
                    var val = status[Key];

                    if (!NoOutput)
                    {
                        if (val is string)
                            Console.WriteLine(val);
                        else
                            Console.WriteLine(GetJsonValue(val));
                    }

                    // If the value is true, process returns success (exitcode 0) 
                    // If the value is false, process returns failure (exitcode 1)
                    if (val is bool) 
                        return Task.FromResult((bool)val ? 0 : 1);
                }
                else
                    throw new ApplicationException($"\"{Key}\" is not a valid Status Key. Valid keys are: {String.Join(", ", status.Keys.ToArray())}");
            }
            else if (AsJson)
            {
                Console.WriteLine("{");
                foreach (var kv in status.ToList())
                {
                    Console.Write($" \"{kv.Key}\":{GetJsonValue(kv.Value)},\n");
                }
                Console.Write($" \"ConsoleProcesses\": [\n");
                PrintConsoleProcessList();
                Console.WriteLine("\n ]\n}");
            }
            else
            {
                PrintToConsole(status);
                PrintConsoleProcessList();
            }

            return Task.FromResult(0);
        }

        private static string GetJsonValue(object Value)
        {
            if (Value is string)
                return ($"\"{Value.ToString().Replace("\\", "\\\\")}\"");
            else if (Value is bool)
                return ($"{Value.ToString().ToLowerInvariant()}");
            else if (Value is Array)
            {
                var sb = new StringBuilder();
                sb.Append($"[");
                bool first = true;
                foreach (string s in Value as string[])
                {
                    if (!first)
                        sb.Append(", ");

                    first = false;
                    sb.Append(GetJsonValue(s));
                }
                sb.Append($"]");
                return sb.ToString();
            }
            else
                return $"{Value}";
        }

        private static void PrintToConsole(Dictionary<string, object> result)
        {
            bool isElevated = (bool)result["IsElevated"];
            int integrity = (int)result["IntegrityLevelNumeric"];

            Console.WriteLine($"Caller Pid: {result["CallerPid"]}");
            Console.Write($"Running as:\n  User: ");

            if (isElevated)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(result["UserName"]);

            Console.ResetColor();
            Console.Write($"  Sid: {result["UserSid"]}\n  Is Admin: ");

            if (isElevated)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(result["IsElevated"]);
            Console.ResetColor();

            Console.Write($"  Integrity Level: ");

            if (integrity >= (int)IntegrityLevel.High)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"{result["IntegrityLevel"]} ({result["IntegrityLevelNumeric"]})");
            Console.ResetColor();

            Console.WriteLine($"\nCredentials Cache:\n  Mode: {result["CacheMode"]}\n  Available for this process: {result["CacheAvailable"]}");
            Console.WriteLine($"  Total active cache sessions: {result["CacheSessionsCount"]}");

            foreach (string s in result["CacheSessions"] as string[])
            {
                Console.WriteLine($"    {s},");
            }

            if ((bool)result["IsRedirected"])
                Console.WriteLine($"\nProcesses attached to the current **REDIRECTED** console:");
            else
                Console.WriteLine($"\nProcesses attached to the current console:");
        }

        private void PrintConsoleProcessList()
        {
            var ownPid = ProcessApi.GetCurrentProcessId();
            var processIds = ConsoleHelper.GetConsoleAttachedPids();
            const string unknown = "(Unknown)";

            if (!AsJson)
                Console.WriteLine($"{"PID".PadLeft(9)} {"PPID".PadLeft(9)} {"Integrity".PadRight(10)} {"UserName".PadRight(25)} {"Name"}");

            bool first = true;
            foreach (var pid in processIds.Reverse())
            {
                Process p = null;
                string name = unknown;
                string integrityString = unknown;
                int integrity = 0;
                string username = unknown;
                int ppid = 0;

                try
                {
                    p = Process.GetProcessById((int)pid);
                    name = p.GetExeName();
                    ppid = ProcessHelper.GetParentProcessId((int)pid);

                    try
                    {
                        integrity = ProcessHelper.GetProcessIntegrityLevel(p.Handle);
                        integrityString = integrity.ToString(CultureInfo.InvariantCulture);
                        if (Enum.IsDefined(typeof(IntegrityLevel), integrity))
                            integrityString = ((IntegrityLevel)integrity).ToString();
                    }
                    catch
                    { }

                    try 
                    {
                        username = p.GetProcessUser()?.Name ?? unknown;
                    }
                    catch
                    { }
                }
                catch
                { }

                if (!AsJson)
                    Console.WriteLine($"{pid.ToString(CultureInfo.InvariantCulture).PadLeft(9)} {ppid.ToString(CultureInfo.InvariantCulture).PadLeft(9)} {integrityString.PadRight(10)} {username.PadRight(25)} {name}{((ownPid == pid) ? " (this gsudo status)" : null)}");
                else
                {
                    if (!first) Console.WriteLine(",");
                    Console.Write($"   {{\"Pid\":{pid}, \"Ppid\":{ppid}, \"IntegrityLevel\":\"{integrityString}\", \"IntegrityLevelNumeric\":{integrity}, \"UserName\":{GetJsonValue(username)}, \"Executable\":{GetJsonValue(name)}}}");
                }

                first = false;
            }
        }
    }
}
