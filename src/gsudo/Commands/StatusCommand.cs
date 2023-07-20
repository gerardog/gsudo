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

        public Task<int> Execute()
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            result["CallerPid"] = ProcessHelper.GetCallerPid();

            var id = WindowsIdentity.GetCurrent();
            result["UserName"] = id.Name;
            result["UserSid"] = id.User.ToString();

            bool isAdmin = SecurityHelper.IsAdministrator();
            result["IsElevated"] = isAdmin;
            bool isAdminMember = SecurityHelper.IsMemberOfLocalAdmins();
            result["IsAdminMember"] = isAdminMember;

            var integrity = SecurityHelper.GetCurrentIntegrityLevel();
            var integrityString = string.Empty;

            if (Enum.IsDefined(typeof(IntegrityLevel), integrity))
                integrityString = ((IntegrityLevel)integrity).ToString();

            result["IntegrityLevelNumeric"] = integrity;
            result["IntegrityLevel"] = integrityString;
            result["CacheMode"] = Settings.CacheMode.Value.ToString();
            bool isCacheAvailable = NamedPipeClient.IsServiceAvailable();
            result["CacheAvailable"] = isCacheAvailable;

            //            ---------
            var pipes = NamedPipeUtils.ListNamedPipes();
            result["CacheSessionsCount"] = pipes.Count;
            result["CacheSessions"] = pipes.ToArray();

            result["IsRedirected"] = Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected;

            if (!string.IsNullOrEmpty(Key))
            {
                if (result.ContainsKey(Key))
                {
                    var val = result[Key];
                    if (val is string)
                        Console.WriteLine(val);
                    else
                    {
                        Console.WriteLine(GetJsonValue(val));
                    }

                    switch (Key.ToLowerInvariant())
                    {
                        case "cacheavailable":
                            if (!isCacheAvailable)
                                return Task.FromResult(1);
                            break;

                        case "iselevated":
                            if (!isAdmin)
                                return Task.FromResult(1);
                            break;

                        case "isadminmember":
                            if (!isAdminMember)
                                return Task.FromResult(1);
                            break;
                    }
                }
                else
                {
                    throw new ApplicationException($"\"{Key}\" is not a valid Status Key. Valid keys are: {String.Join(", ", result.Keys.ToArray())}");
                }
            }
            else if (AsJson)
            {
                Console.WriteLine("{");
                foreach (var kv in result.ToList())
                {
                    Console.Write($" \"{kv.Key}\":{GetJsonValue(kv.Value)},\n");
                }
                Console.Write($" \"ConsoleProcesses\": [\n");
                PrintConsoleProcessList();
                Console.WriteLine("\n ]\n}");
            }
            else
            {
                PrintToConsole(result);
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
                Console.WriteLine($"{"PID".PadLeft(9)} {"PPID".PadLeft(9)} {"Integrity".PadRight(20)} {"UserName".PadRight(25)} {"Name"}");

            bool first = true;
            foreach (var pid in processIds.Reverse())
            {
                Process p = null;
                string name = unknown;
                string integrity = unknown;
                string integrityString = unknown;
                string username = unknown;
                int ppid = 0;

                try
                {
                    p = Process.GetProcessById((int)pid);
                    name = p.GetExeName();
                    ppid = ProcessHelper.GetParentProcessId((int)pid);

                    try
                    {
                        var i = ProcessHelper.GetProcessIntegrityLevel(p.Handle);
                        integrity = i.ToString(CultureInfo.InvariantCulture);
                        if (Enum.IsDefined(typeof(IntegrityLevel), i))
                            integrityString = ((IntegrityLevel)i).ToString();
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
                {
                    integrity = $"{integrityString} ({integrity})";
                    Console.WriteLine($"{pid.ToString(CultureInfo.InvariantCulture).PadLeft(9)} {ppid.ToString(CultureInfo.InvariantCulture).PadLeft(9)} {integrity.PadRight(20)} {username.PadRight(25)} {name}{((ownPid == pid) ? " (this gsudo status)" : null)}");
                }
                else
                {
                    if (!first) Console.WriteLine(",");
                    Console.Write($"   {{\"Pid\":{pid}, \"Ppid\":{ppid}, \"IntegrityLevelNumeric\":{integrity}, \"IntegrityLevel\":\"{integrityString}\", \"UserName\":{GetJsonValue(username)}, \"Executable\":{GetJsonValue(name)}}}");
                }
                first = false;
            }
        }
    }
}
