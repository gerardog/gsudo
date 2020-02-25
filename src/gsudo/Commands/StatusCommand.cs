using gsudo.Helpers;
using gsudo.Native;
using gsudo.Rpc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class StatusCommand : ICommand
    {
        public Task<int> Execute()
        {
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

            if (integrity > (int)IntegrityLevel.MediumPlus)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"{integrityString} ({integrity})");
            Console.ResetColor();

            Console.WriteLine($"\nActive {nameof(gsudo)} sessions (Credentials Cache):");

            var pipes = NamedPipeUtils.ListNamedPipes();
            foreach (string s in pipes)
            {
                Console.WriteLine($"  {s}");
            }
            if (pipes.Count == 0)
                Console.WriteLine($"  No active gsudo processes");

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

            foreach (var pid in processIds.Reverse())
            {
                Process p = null;
                string name = "(Unknown)";
                try
                {
                    p = Process.GetProcessById((int)pid);
                    name = p.ProcessName;
                    name = p.MainModule.FileName;
                }
                catch
                { }

                Console.WriteLine($"  {pid}\t{name}{((ownPid == pid) ? " (own)" : null)}");
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
