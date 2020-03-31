using gsudo.Helpers;
using gsudo.Native;
using gsudo.Rpc;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class StatusCommand : ICommand
    {
        public Task<int> Execute()
        {
            Console.WriteLine($"Caller Pid: {ProcessHelper.GetCallerPid()}");

            var id = WindowsIdentity.GetCurrent();
            bool isAdmin = ProcessHelper.IsAdministrator();
            Console.Write($"Running as:\n  User: ");

            if (isAdmin)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(id.Name);

            Console.ResetColor();
            Console.Write($"  Sid: {id.User}\n  Is Admin: ");

            if (isAdmin)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(ProcessHelper.IsAdministrator());
            Console.ResetColor();

            Console.Write($"  Integrity Level: ");
            var integrity = ProcessHelper.GetCurrentIntegrityLevel();
            var integrityString = string.Empty;

            if (Enum.IsDefined(typeof(IntegrityLevel), integrity))
                integrityString = $"{((IntegrityLevel)integrity).ToString()}";

            if (integrity >= (int)IntegrityLevel.High)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"{integrityString} ({integrity})");
            Console.ResetColor();

            Console.WriteLine($"\nCredentials Cache:\n  Mode: {Settings.CacheMode.Value.ToString()}\n  Available for this process: {NamedPipeClient.IsServiceAvailable()}");
            var pipes = NamedPipeUtils.ListNamedPipes();
            Console.WriteLine($"  Total active cache sessions: {pipes.Count}");

            foreach (string s in pipes)
            {
                Console.WriteLine($"    {s}");
            }

            if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
                Console.WriteLine($"\nProcesses attached to the current **REDIRECTED** console:");
            else
                Console.WriteLine($"\nProcesses attached to the current console:");

            PrintConsoleProcessList();
                
            return Task.FromResult(0);
        }

        private void PrintConsoleProcessList()
        {
            var processIds = new uint[100];
            var ownPid = ProcessApi.GetCurrentProcessId();
            processIds = GetConsoleAttachedPids(processIds);
            const string unknown = "(Unknown)";
            Console.WriteLine($"{"PID".PadLeft(9)} {"Integrity".PadRight(10)} {"UserName".PadRight(25)} {"Name"}");

            foreach (var pid in processIds.Reverse())
            {
                Process p = null;
                string name = unknown;
                string integrity = unknown;
                string username = unknown;
                try
                {
                    p = Process.GetProcessById((int)pid);
                    name = p.GetExeName();
                    
                    try
                    {
                        var i = ProcessHelper.GetProcessIntegrityLevel(p.Handle);
                        integrity = i.ToString(CultureInfo.InvariantCulture);
                        if (Enum.IsDefined(typeof(IntegrityLevel), i))
                            integrity = ((IntegrityLevel)i).ToString();
                    }
                    catch
                    { }

                    try
                    {
                        username = p.GetProcessUser() ?? unknown;
                    }
                    catch
                    { }
                }
                catch
                { }

                Console.WriteLine($"{pid.ToString(CultureInfo.InvariantCulture).PadLeft(9)} {integrity.PadRight(10)} {username.PadRight(25)} {name}{((ownPid == pid) ? " (this gsudo status)" : null)}");
            }
        }

        private static uint[] GetConsoleAttachedPids(uint[] processIds)
        {
            var num = ConsoleApi.GetConsoleProcessList(processIds, 1);
            if (num == 0) throw new System.ComponentModel.Win32Exception();

            processIds = new UInt32[num];

            num = ConsoleApi.GetConsoleProcessList(processIds, (uint)processIds.Length);
            if (num == 0) throw new System.ComponentModel.Win32Exception();
            return processIds;
        }
    }
}
